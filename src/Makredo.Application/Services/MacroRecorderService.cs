using System.Globalization;
using Makredo.Application.Abstractions;
using Makredo.Application.Hotkeys;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;

namespace Makredo.Application.Services;

public sealed class MacroRecorderService : IMacroRecorderService
{
    private readonly object _syncRoot = new();
    private readonly object _lifecycleSyncRoot = new();
    private Task? _startFlight;
    private Task? _stopFlight;
    private readonly IKeyboardHookSource _keyboardHookSource;
    private readonly IMouseHookSource _mouseHookSource;
    private readonly ITimestampedMouseHookSource? _timestampedMouseHookSource;
    private readonly TimeProvider _timeProvider;
    private bool _acceptingEvents;
    private readonly IApplicationStateService _applicationStateService;
    private readonly IHotkeyConfiguration _hotkeyConfiguration;
    private readonly IAppLogger _logger;

    private readonly List<MacroEvent> _recordedEvents = [];
    private HotkeyRecordingFilter _hotkeyFilter = new([]);

    private MacroSession? _currentSession;
    private DateTime _lastEventTimestampUtc;

    public MacroRecorderService(
        IKeyboardHookSource keyboardHookSource,
        IMouseHookSource mouseHookSource,
        IApplicationStateService applicationStateService,
        IHotkeyConfiguration hotkeyConfiguration,
        IAppLogger? logger = null,
        TimeProvider? timeProvider = null)
    {
        _keyboardHookSource = keyboardHookSource;
        _mouseHookSource = mouseHookSource;
        _timestampedMouseHookSource = mouseHookSource as ITimestampedMouseHookSource;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _applicationStateService = applicationStateService;
        _hotkeyConfiguration = hotkeyConfiguration;
        _logger = logger ?? NullAppLogger.Instance;
    }

    private volatile bool _recordInjectedInput = true;
    public bool RecordInjectedInput { get => _recordInjectedInput; set => _recordInjectedInput = value; }

    public bool IsRecording => _applicationStateService.IsState(AppState.Recording);

    public MacroSession? CurrentSession
    {
        get
        {
            lock (_syncRoot)
            {
                // CurrentSession intentionally remains populated with the most recently
                // completed recording until Clear is called.
                return _currentSession;
            }
        }
    }

    public event Action? RecordingStarted;
    public event Action<MacroEvent>? EventRecorded;
    public event Action<MacroSession>? RecordingStopped;

    public Task StartAsync(string? sessionName = null, CancellationToken cancellationToken = default)
        => StartAsync(sessionName, null, cancellationToken);

    public Task StartAsync(string? sessionName, IReadOnlyCollection<int>? startingHotkeyKeys, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource completion;
        lock (_lifecycleSyncRoot)
        {
            if (_stopFlight is not null)
            {
                return Task.CompletedTask;
            }

            if (_startFlight is not null)
            {
                return _startFlight;
            }

            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _startFlight = completion.Task;
        }

        // Reserve before the core can publish state or call external hook implementations.
        _ = CompleteStartFlightAsync(sessionName, completion, startingHotkeyKeys, cancellationToken);
        return completion.Task;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        TaskCompletionSource completion;
        Task? pendingStart;
        lock (_lifecycleSyncRoot)
        {
            if (_stopFlight is not null)
            {
                return _stopFlight;
            }

            pendingStart = _startFlight;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _stopFlight = completion.Task;
        }

        _ = CompleteStopFlightAsync(pendingStart, completion);
        return completion.Task;
    }

    private async Task CompleteStartFlightAsync(
        string? sessionName,
        TaskCompletionSource completion,
        IReadOnlyCollection<int>? startingHotkeyKeys,
        CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            await StartCoreAsync(sessionName, startingHotkeyKeys, cancellationToken);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        CompleteLifecycleFlight(completion, starting: true, failure);
    }

    private async Task CompleteStopFlightAsync(Task? pendingStart, TaskCompletionSource completion)
    {
        Exception? failure = null;
        try
        {
            if (pendingStart is not null)
            {
                try
                {
                    await pendingStart;
                }
                catch (Exception)
                {
                    // Startup owns its rollback and failure; stop must still finish cleanup afterward.
                }
            }

            await StopCoreAsync();
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        CompleteLifecycleFlight(completion, starting: false, failure);
    }

    private void CompleteLifecycleFlight(TaskCompletionSource completion, bool starting, Exception? failure)
    {
        lock (_lifecycleSyncRoot)
        {
            if (starting)
            {
                _startFlight = null;
            }
            else
            {
                _stopFlight = null;
            }

            if (failure is OperationCanceledException cancellation)
            {
                completion.TrySetCanceled(cancellation.CancellationToken);
            }
            else if (failure is not null)
            {
                completion.TrySetException(failure);
            }
            else
            {
                completion.TrySetResult();
            }
        }
    }

    private async Task StartCoreAsync(
        string? sessionName,
        IReadOnlyCollection<int>? startingHotkeyKeys,
        CancellationToken cancellationToken)
    {
        MacroSession session = new()
        {
            Name = string.IsNullOrWhiteSpace(sessionName)
                ? $"Macro_{_timeProvider.GetUtcNow().UtcDateTime:yyyyMMdd_HHmmss}"
                : sessionName.Trim()
        };

        bool claimedRecording = true;
        bool sessionPrepared = false;
        bool keyboardSubscribed = false;
        bool mouseSubscribed = false;
        bool keyboardHookStarted = false;
        bool mouseHookStarted = false;

        try
        {
            // StateChanged may reenter a lifecycle method or throw after committing Recording.
            if (!_applicationStateService.TryTransitionTo(AppState.Recording, AppState.Idle))
            {
                claimedRecording = false;
                return;
            }

            lock (_syncRoot)
            {
                _currentSession = session;
                ResetRecordingBuffersUnsafe(_timeProvider.GetUtcNow().UtcDateTime);
                _hotkeyFilter = new HotkeyRecordingFilter([_hotkeyConfiguration.RecordToggleHotkey, _hotkeyConfiguration.PlaybackToggleHotkey, _hotkeyConfiguration.StopHotkey], startingHotkeyKeys);
                _acceptingEvents = true;
                sessionPrepared = true;
            }

            _keyboardHookSource.KeyActivityReceived += OnKeyboardActivityReceived;
            keyboardSubscribed = true;
            SubscribeMouse();
            mouseSubscribed = true;

            await _keyboardHookSource.StartAsync(cancellationToken);
            keyboardHookStarted = true;

            await _mouseHookSource.StartAsync(cancellationToken);
            mouseHookStarted = true;

            _logger.Log(
                AppLogLevel.Information,
                nameof(MacroRecorderService),
                $"Kayit baslatildi. Oturum: {session.Name}.");
            SafeInvokeRecordingStarted();
        }
        catch (Exception ex)
        {
            if (!claimedRecording)
            {
                throw;
            }

            if (keyboardSubscribed)
            {
                _keyboardHookSource.KeyActivityReceived -= OnKeyboardActivityReceived;
            }

            if (mouseSubscribed)
            {
                UnsubscribeMouse();
            }

            if (mouseHookStarted)
            {
                await SafeStopAsync(_mouseHookSource.StopAsync);
            }

            if (keyboardHookStarted)
            {
                await SafeStopAsync(_keyboardHookSource.StopAsync);
            }

            if (sessionPrepared)
            {
                lock (_syncRoot)
                {
                    ResetRecordingBuffersUnsafe(_timeProvider.GetUtcNow().UtcDateTime);
                    _currentSession = null;
                }
            }

            try
            {
                _applicationStateService.TryTransitionTo(AppState.Idle, AppState.Recording, AppState.Stopping);
            }
            catch (Exception rollbackException)
            {
                _logger.Log(
                    AppLogLevel.Error,
                    nameof(MacroRecorderService),
                    "Kayıt başlatma hatasından sonra durum bildirimi yayımlanamadı.",
                    rollbackException);
            }

            _logger.Log(
                AppLogLevel.Error,
                nameof(MacroRecorderService),
                $"Kayit baslatilamadi. Oturum: {session.Name}.",
                ex);
            throw;
        }
    }

    private async Task StopCoreAsync()
    {
        MacroSession? completedSession = CurrentSession;
        if (completedSession is null)
        {
            return;
        }

        List<Exception> errors = [];
        try
        {
            if (!_applicationStateService.TryTransitionTo(AppState.Stopping, AppState.Recording))
            {
                return;
            }
        }
        catch (Exception exception)
        {
            // A committed transition's observer failure must not leave hooks or buffers active.
            errors.Add(exception);
        }

        List<MacroEvent> eventsToPublish = [];

        await TryStopHookAsync(
            _keyboardHookSource.StopAsync,
            "Klavye kancasi durdurulamadi.",
            errors);
        await TryStopHookAsync(
            _mouseHookSource.StopAsync,
            "Fare kancasi durdurulamadi.",
            errors);

        _keyboardHookSource.KeyActivityReceived -= OnKeyboardActivityReceived;
        UnsubscribeMouse();
        lock (_syncRoot)
        {
            _acceptingEvents = false;
            FlushPendingModifierEventsUnsafe(eventsToPublish);


            completedSession.ReplaceEvents(_recordedEvents);
            _recordedEvents.Clear();
            _hotkeyFilter = new HotkeyRecordingFilter([]);
            _currentSession = completedSession;
        }

        PublishRecordedEvents(eventsToPublish);

        TryCaptureError(
            errors,
            () => _applicationStateService.TryTransitionTo(AppState.Idle, AppState.Stopping),
            "Kayit durduktan sonra uygulama durumu bosa alinamadi.");
        TryCaptureError(
            errors,
            () => SafeInvokeRecordingStopped(completedSession),
            "Kayit durdu bildirimi yayinlanamadi.");

        if (errors.Count > 0)
        {
            AggregateException aggregateException = new(
                "Kayit durdurma islemi bir veya daha fazla hatayla tamamlandi.",
                errors);
            _logger.Log(
                AppLogLevel.Error,
                nameof(MacroRecorderService),
                "Kayit durdurulurken hata olustu.",
                aggregateException);
            throw aggregateException;
        }

        _logger.Log(
            AppLogLevel.Information,
            nameof(MacroRecorderService),
            $"Kayit durduruldu. Oturum: {completedSession.Name}, olay sayisi: {completedSession.Events.Count}.");
    }

    public void Clear()
    {
        if (IsRecording)
        {
            throw new InvalidOperationException(
                "Kayit devam ederken mevcut oturum temizlenemez.");
        }

        lock (_syncRoot)
        {
            _recordedEvents.Clear();
            _hotkeyFilter = new HotkeyRecordingFilter([]);

            _currentSession = null;
            _lastEventTimestampUtc = _timeProvider.GetUtcNow().UtcDateTime;
        }

        _logger.Log(
            AppLogLevel.Information,
            nameof(MacroRecorderService),
            "Gecerli kayit oturumu temizlendi.");
    }

    private void OnKeyboardActivityReceived(KeyboardActivityInfo keyboardActivity)
    {
        if (!InputOriginPolicy.ShouldRecord(keyboardActivity.IsInjected, keyboardActivity.ExtraInfo, RecordInjectedInput)) { return; }
        List<MacroEvent>? eventsToPublish = null;

        lock (_syncRoot)
        {
            if (!_acceptingEvents || _currentSession is null)
            {
                return;
            }

            eventsToPublish = [];
            KeyboardActivityInfo captured = keyboardActivity with { TimestampUtc = keyboardActivity.TimestampUtc ?? _timeProvider.GetUtcNow().UtcDateTime };
            foreach (KeyboardActivityInfo accepted in _hotkeyFilter.Process(captured))
            {
                CommitRecordedEventUnsafe(CreateKeyboardMacroEventUnsafe(accepted), eventsToPublish);
            }
        }

        PublishRecordedEvents(eventsToPublish);
    }

    private void OnMouseActivityReceived(MouseActionType mouseActionType, int? x, int? y, int? wheelDelta)
        => OnTimestampedMouseActivityReceived(new MouseActivityInfo(mouseActionType, x, y, wheelDelta, _timeProvider.GetUtcNow().UtcDateTime));

    private void OnTimestampedMouseActivityReceived(MouseActivityInfo activity)
    {
        if (!InputOriginPolicy.ShouldRecord(activity.IsInjected, activity.ExtraInfo, RecordInjectedInput)) { return; }
        List<MacroEvent>? eventsToPublish = null;

        lock (_syncRoot)
        {
            if (!_acceptingEvents || _currentSession is null)
            {
                return;
            }

            eventsToPublish = [];
            FlushPendingModifierEventsUnsafe(eventsToPublish);
            DateTime nowUtc = activity.TimestampUtc;

            MacroEvent macroEvent = new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = activity.ActionType,
                X = activity.X,
                Y = activity.Y,
                WheelDelta = activity.WheelDelta,
                TimestampUtc = nowUtc,
                DelayMs = CalculateDelayMsUnsafe(nowUtc),
                Description = FormatMouseAction(activity.ActionType)
            };

            CommitRecordedEventUnsafe(macroEvent, eventsToPublish);
        }

        PublishRecordedEvents(eventsToPublish);
    }

    private void SubscribeMouse()
    {
        if (_timestampedMouseHookSource is not null)
        {
            _timestampedMouseHookSource.TimestampedMouseActivityReceived += OnTimestampedMouseActivityReceived;
        }
        else { _mouseHookSource.MouseActivityReceived += OnMouseActivityReceived; }
    }

    private void UnsubscribeMouse()
    {
        if (_timestampedMouseHookSource is not null)
        {
            _timestampedMouseHookSource.TimestampedMouseActivityReceived -= OnTimestampedMouseActivityReceived;
        }
        else { _mouseHookSource.MouseActivityReceived -= OnMouseActivityReceived; }
    }

    private void ResetRecordingBuffersUnsafe(DateTime timestampUtc)
    {
        _acceptingEvents = false;
        _recordedEvents.Clear();
        _hotkeyFilter = new HotkeyRecordingFilter([]);

        _lastEventTimestampUtc = timestampUtc;
    }

    private MacroEvent CreateKeyboardMacroEventUnsafe(KeyboardActivityInfo keyboardActivity)
    {
        DateTime nowUtc = keyboardActivity.TimestampUtc ?? _timeProvider.GetUtcNow().UtcDateTime;
        string keyName = VirtualKeyDisplayNameFormatter.Format(keyboardActivity.VirtualKeyCode);

        return new MacroEvent
        {
            EventType = MacroEventType.Keyboard,
            KeyboardActionType = keyboardActivity.IsKeyDown
                ? KeyboardActionType.KeyDown
                : KeyboardActionType.KeyUp,
            KeyCode = keyboardActivity.VirtualKeyCode,
            ScanCode = keyboardActivity.ScanCode,
            IsExtendedKey = keyboardActivity.IsExtendedKey,
            KeyName = keyName,
            TimestampUtc = nowUtc,
            DelayMs = CalculateDelayMsUnsafe(nowUtc),
            Description = string.Create(
                CultureInfo.InvariantCulture,
                $"{(keyboardActivity.IsKeyDown ? "Tus basildi" : "Tus birakildi")} - {keyName}")
        };
    }

    private void FlushPendingModifierEventsUnsafe(List<MacroEvent> eventsToPublish)
    {
        foreach (KeyboardActivityInfo pending in _hotkeyFilter.Flush())
        {
            CommitRecordedEventUnsafe(CreateKeyboardMacroEventUnsafe(pending), eventsToPublish);
        }
    }

    private void CommitRecordedEventUnsafe(
        MacroEvent macroEvent,
        List<MacroEvent> eventsToPublish)
    {
        _recordedEvents.Add(macroEvent);
        eventsToPublish.Add(macroEvent);
    }

    private void PublishRecordedEvents(List<MacroEvent>? eventsToPublish)
    {
        if (eventsToPublish is null || eventsToPublish.Count == 0)
        {
            return;
        }

        foreach (MacroEvent macroEvent in eventsToPublish)
        {
            try
            {
                EventRecorded?.Invoke(macroEvent);
            }
            catch (Exception ex)
            {
                _logger.Log(
                    AppLogLevel.Error,
                    nameof(MacroRecorderService),
                    "Olay kaydedildi bildirimi yayinlanirken hata olustu.",
                    ex);
            }
        }
    }

    private void SafeInvokeRecordingStarted()
    {
        try
        {
            RecordingStarted?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Error,
                nameof(MacroRecorderService),
                "Kayit basladi bildirimi yayinlanirken hata olustu.",
                ex);
        }
    }

    private void SafeInvokeRecordingStopped(MacroSession completedSession)
    {
        try
        {
            RecordingStopped?.Invoke(completedSession);
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Error,
                nameof(MacroRecorderService),
                "Kayit durdu bildirimi yayinlanirken hata olustu.",
                ex);
            throw;
        }
    }

    private static string FormatMouseAction(MouseActionType mouseActionType)
    {
        return mouseActionType switch
        {
            MouseActionType.Move => "Fare hareketi",
            MouseActionType.LeftDown => "Sol tus basildi",
            MouseActionType.LeftUp => "Sol tus birakildi",
            MouseActionType.RightDown => "Sag tus basildi",
            MouseActionType.RightUp => "Sag tus birakildi",
            MouseActionType.MiddleDown => "Orta tus basildi",
            MouseActionType.MiddleUp => "Orta tus birakildi",
            MouseActionType.Wheel => "Fare tekerlegi",
            MouseActionType.DoubleClick => "Cift tiklama",
            _ => mouseActionType.ToString()
        };
    }

    private int CalculateDelayMsUnsafe(DateTime currentTimestampUtc)
    {
        int delay = (int)(currentTimestampUtc - _lastEventTimestampUtc).TotalMilliseconds;
        _lastEventTimestampUtc = currentTimestampUtc;

        return Math.Max(delay, 0);
    }

    private static async Task SafeStopAsync(
        Func<CancellationToken, Task> stopAsync)
    {
        try
        {
            await stopAsync(CancellationToken.None);
        }
        catch
        {
            // Kismi baslatma sonrasinda en iyi caba ile temizlik yapilir.
        }
    }

    private static async Task TryStopHookAsync(
        Func<CancellationToken, Task> stopAsync,
        string message,
        List<Exception> errors)
    {
        try
        {
            await stopAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            errors.Add(new InvalidOperationException(message, ex));
        }
    }

    private static void TryCaptureError(
        List<Exception> errors,
        Action action,
        string message)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            errors.Add(new InvalidOperationException(message, ex));
        }
    }
}
