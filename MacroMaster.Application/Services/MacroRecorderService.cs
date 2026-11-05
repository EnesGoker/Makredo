using System.Globalization;
using MacroMaster.Application.Abstractions;
using MacroMaster.Domain.Enums;
using MacroMaster.Domain.Models;

namespace MacroMaster.Application.Services;

public sealed class MacroRecorderService : IMacroRecorderService
{
    private readonly object _syncRoot = new();
    private readonly IKeyboardHookSource _keyboardHookSource;
    private readonly IMouseHookSource _mouseHookSource;
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
        IAppLogger? logger = null)
    {
        _keyboardHookSource = keyboardHookSource;
        _mouseHookSource = mouseHookSource;
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

    public async Task StartAsync(
        string? sessionName = null,
        CancellationToken cancellationToken = default)
    {
        if (!_applicationStateService.TryTransitionTo(AppState.Recording, AppState.Idle))
        {
            return;
        }

        MacroSession session = new()
        {
            Name = string.IsNullOrWhiteSpace(sessionName)
                ? $"Macro_{DateTime.UtcNow:yyyyMMdd_HHmmss}"
                : sessionName.Trim()
        };

        lock (_syncRoot)
        {
            _currentSession = session;
            ResetRecordingBuffersUnsafe(DateTime.UtcNow);
        }

        _keyboardHookSource.KeyActivityReceived += OnKeyboardActivityReceived;
        _mouseHookSource.MouseActivityReceived += OnMouseActivityReceived;

        bool keyboardHookStarted = false;
        bool mouseHookStarted = false;

        try
        {
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
            _keyboardHookSource.KeyActivityReceived -= OnKeyboardActivityReceived;
            _mouseHookSource.MouseActivityReceived -= OnMouseActivityReceived;

            if (mouseHookStarted)
            {
                await SafeStopAsync(_mouseHookSource.StopAsync);
            }

            if (keyboardHookStarted)
            {
                await SafeStopAsync(_keyboardHookSource.StopAsync);
            }

            lock (_syncRoot)
            {
                ResetRecordingBuffersUnsafe(DateTime.UtcNow);
                _currentSession = null;
            }

            _applicationStateService.TryTransitionTo(AppState.Idle, AppState.Recording, AppState.Stopping);
            _logger.Log(
                AppLogLevel.Error,
                nameof(MacroRecorderService),
                $"Kayit baslatilamadi. Oturum: {session.Name}.",
                ex);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        MacroSession? completedSession = CurrentSession;

        if (completedSession is null
            || !_applicationStateService.TryTransitionTo(AppState.Stopping, AppState.Recording))
        {
            return;
        }

        List<Exception> errors = [];
        List<MacroEvent> eventsToPublish = [];

        _keyboardHookSource.KeyActivityReceived -= OnKeyboardActivityReceived;
        _mouseHookSource.MouseActivityReceived -= OnMouseActivityReceived;

        await TryStopHookAsync(
            _keyboardHookSource.StopAsync,
            "Klavye kancasi durdurulamadi.",
            errors);
        await TryStopHookAsync(
            _mouseHookSource.StopAsync,
            "Fare kancasi durdurulamadi.",
            errors);

        lock (_syncRoot)
        {
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
            _lastEventTimestampUtc = DateTime.UtcNow;
        }

        _logger.Log(
            AppLogLevel.Information,
            nameof(MacroRecorderService),
            "Gecerli kayit oturumu temizlendi.");
    }

    private void OnKeyboardActivityReceived(KeyboardActivityInfo keyboardActivity)
    {
        if (!IsRecording)
        {
            return;
        }

        List<MacroEvent>? eventsToPublish = null;

        lock (_syncRoot)
        {
            if (!_applicationStateService.IsState(AppState.Recording) || _currentSession is null)
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
    {
        if (!IsRecording)
        {
            return;
        }

        List<MacroEvent>? eventsToPublish = null;

        lock (_syncRoot)
        {
            if (!_applicationStateService.IsState(AppState.Recording) || _currentSession is null)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;

            MacroEvent macroEvent = new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = mouseActionType,
                X = x,
                Y = y,
                WheelDelta = wheelDelta,
                TimestampUtc = nowUtc,
                DelayMs = CalculateDelayMsUnsafe(nowUtc),
                Description = FormatMouseAction(mouseActionType)
            };

            eventsToPublish = [];
            FlushPendingModifierEventsUnsafe(eventsToPublish);
            CommitRecordedEventUnsafe(macroEvent, eventsToPublish);
        }

        PublishRecordedEvents(eventsToPublish);
    }

    private void ResetRecordingBuffersUnsafe(DateTime timestampUtc)
    {
        _recordedEvents.Clear();
        _pendingModifierEvents.Clear();
        _suppressedHotkeyModifierReleases.Clear();
        _lastEventTimestampUtc = timestampUtc;
    }

    private MacroEvent CreateKeyboardMacroEventUnsafe(KeyboardActivityInfo keyboardActivity)
    {
        DateTime nowUtc = DateTime.UtcNow;
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
