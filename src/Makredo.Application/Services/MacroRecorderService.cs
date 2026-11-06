using System.Globalization;
using Makredo.Application.Abstractions;
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
    private readonly List<MacroEvent> _pendingModifierEvents = [];
    private readonly HashSet<HotkeyModifiers> _suppressedHotkeyModifierReleases = [];
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

    public Task StartAsync(
        string? sessionName = null,
        CancellationToken cancellationToken = default)
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
        _ = CompleteStartFlightAsync(sessionName, completion, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        Exception? failure = null;
        try
        {
            await StartCoreAsync(sessionName, cancellationToken);
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
        string? sessionName = null,
        CancellationToken cancellationToken = default)
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
            _suppressedHotkeyModifierReleases.Clear();

            completedSession.ReplaceEvents(_recordedEvents);
            _recordedEvents.Clear();
            _pendingModifierEvents.Clear();
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
            _pendingModifierEvents.Clear();
            _suppressedHotkeyModifierReleases.Clear();
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
        List<MacroEvent>? eventsToPublish = null;

        lock (_syncRoot)
        {
            if (!_acceptingEvents || _currentSession is null)
            {
                return;
            }

            if (TryConsumeSuppressedModifierReleaseUnsafe(keyboardActivity))
            {
                return;
            }

            MacroEvent macroEvent = CreateKeyboardMacroEventUnsafe(keyboardActivity);

            if (keyboardActivity.IsModifierKey)
            {
                _pendingModifierEvents.Add(macroEvent);
                return;
            }

            if (TryConsumeReservedHotkeyUnsafe(keyboardActivity))
            {
                return;
            }

            eventsToPublish = [];
            FlushPendingModifierEventsUnsafe(eventsToPublish);
            CommitRecordedEventUnsafe(macroEvent, eventsToPublish);
        }

        PublishRecordedEvents(eventsToPublish);
    }

    private void OnMouseActivityReceived(MouseActionType mouseActionType, int? x, int? y, int? wheelDelta)
        => OnTimestampedMouseActivityReceived(new MouseActivityInfo(mouseActionType, x, y, wheelDelta, _timeProvider.GetUtcNow().UtcDateTime));

    private void OnTimestampedMouseActivityReceived(MouseActivityInfo activity)
    {
        List<MacroEvent>? eventsToPublish = null;

        lock (_syncRoot)
        {
            if (!_acceptingEvents || _currentSession is null)
            {
                return;
            }

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

            eventsToPublish = [];
            FlushPendingModifierEventsUnsafe(eventsToPublish);
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
        _pendingModifierEvents.Clear();
        _suppressedHotkeyModifierReleases.Clear();
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

    private bool TryConsumeSuppressedModifierReleaseUnsafe(KeyboardActivityInfo keyboardActivity)
    {
        if (!keyboardActivity.IsModifierKey
            || !_suppressedHotkeyModifierReleases.Contains(keyboardActivity.ModifierKey))
        {
            return false;
        }

        if (!keyboardActivity.IsKeyDown)
        {
            _suppressedHotkeyModifierReleases.Remove(keyboardActivity.ModifierKey);
        }

        return true;
    }

    private bool TryConsumeReservedHotkeyUnsafe(KeyboardActivityInfo keyboardActivity)
    {
        if (!TryGetMatchingHotkey(keyboardActivity, out HotkeyBinding hotkeyBinding))
        {
            return false;
        }

        SuppressPendingHotkeyModifiersUnsafe(hotkeyBinding.Modifiers);
        return true;
    }

    private bool TryGetMatchingHotkey(
        KeyboardActivityInfo keyboardActivity,
        out HotkeyBinding hotkeyBinding)
    {
        HotkeyBinding recordToggleHotkey = _hotkeyConfiguration.RecordToggleHotkey;
        HotkeyBinding playbackToggleHotkey = _hotkeyConfiguration.PlaybackToggleHotkey;
        HotkeyBinding stopHotkey = _hotkeyConfiguration.StopHotkey;
        HotkeyBinding hotkeySettingsHotkey = _hotkeyConfiguration.HotkeySettingsHotkey;

        if (MatchesHotkey(recordToggleHotkey, keyboardActivity))
        {
            hotkeyBinding = recordToggleHotkey;
            return true;
        }

        if (MatchesHotkey(playbackToggleHotkey, keyboardActivity))
        {
            hotkeyBinding = playbackToggleHotkey;
            return true;
        }

        if (MatchesHotkey(stopHotkey, keyboardActivity))
        {
            hotkeyBinding = stopHotkey;
            return true;
        }

        if (MatchesHotkey(hotkeySettingsHotkey, keyboardActivity))
        {
            hotkeyBinding = hotkeySettingsHotkey;
            return true;
        }

        hotkeyBinding = null!;
        return false;
    }

    private void SuppressPendingHotkeyModifiersUnsafe(HotkeyModifiers hotkeyModifiers)
    {
        if (hotkeyModifiers == HotkeyModifiers.None)
        {
            return;
        }

        HashSet<HotkeyModifiers> suppressedModifiers = [];

        for (int index = _pendingModifierEvents.Count - 1; index >= 0; index--)
        {
            MacroEvent pendingModifierEvent = _pendingModifierEvents[index];

            if (pendingModifierEvent.KeyboardActionType != KeyboardActionType.KeyDown
                || !pendingModifierEvent.KeyCode.HasValue
                || !TryMapModifierKey(pendingModifierEvent.KeyCode.Value, out HotkeyModifiers modifier)
                || !hotkeyModifiers.HasFlag(modifier))
            {
                continue;
            }

            _pendingModifierEvents.RemoveAt(index);
            suppressedModifiers.Add(modifier);
        }

        foreach (HotkeyModifiers modifier in suppressedModifiers)
        {
            _suppressedHotkeyModifierReleases.Add(modifier);
        }
    }

    private static bool MatchesHotkey(
        HotkeyBinding hotkeyBinding,
        KeyboardActivityInfo keyboardActivity)
    {
        return keyboardActivity.VirtualKeyCode == hotkeyBinding.VirtualKeyCode
            && keyboardActivity.ActiveModifiers == hotkeyBinding.Modifiers;
    }

    private void FlushPendingModifierEventsUnsafe(List<MacroEvent> eventsToPublish)
    {
        if (_pendingModifierEvents.Count == 0)
        {
            return;
        }

        foreach (MacroEvent pendingModifierEvent in _pendingModifierEvents)
        {
            CommitRecordedEventUnsafe(pendingModifierEvent, eventsToPublish);
        }

        _pendingModifierEvents.Clear();
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

    private static bool TryMapModifierKey(int virtualKeyCode, out HotkeyModifiers modifier)
    {
        switch (virtualKeyCode)
        {
            case 0x10:
            case 0xA0:
            case 0xA1:
                modifier = HotkeyModifiers.Shift;
                return true;

            case 0x11:
            case 0xA2:
            case 0xA3:
                modifier = HotkeyModifiers.Control;
                return true;

            case 0x12:
            case 0xA4:
            case 0xA5:
                modifier = HotkeyModifiers.Alt;
                return true;

            case 0x5B:
            case 0x5C:
                modifier = HotkeyModifiers.Windows;
                return true;

            default:
                modifier = HotkeyModifiers.None;
                return false;
        }
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
