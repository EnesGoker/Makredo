using Makredo.Application.Abstractions;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;

namespace Makredo.Application.Services;

public sealed class MacroPlaybackService : IMacroPlaybackService, IDisposable
{
    private readonly IInputPlaybackAdapter _inputPlaybackAdapter;
    private readonly ICursorPositionProvider _cursorPositionProvider;
    private readonly IApplicationStateService _applicationStateService;
    private readonly IAppLogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IKeyboardHookSource? _keyboardHookSource;
    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _eventExecutionGate = new(1, 1);

    private volatile bool _stopRequested;
    private bool _playbackCompleting;
    private bool _playbackInputReady;
    private TaskCompletionSource? _stopFlight;
    private TaskCompletionSource? _playbackCompletion;
    private TaskCompletionSource? _disposeFlight;
    private TaskCompletionSource? _manualOperationsDrained;
    private int _manualOperationCount;
    private bool _shutdownRequested;
    private bool _resourcesDisposed;
    private HeldInputTracker? _heldInputs;
    private CancellationTokenSource? _playbackCancellationTokenSource;
    private TaskCompletionSource<bool> _resumeSignal = CreateResumeSignal(signaled: true);
    private PlaybackCoordinateResolver? _activeCoordinateResolver;
    private int _playbackNextLogicalIndex;
    private int? _playbackTotalLogicalEventCount;

    public MacroPlaybackService(
        IInputPlaybackAdapter inputPlaybackAdapter,
        ICursorPositionProvider cursorPositionProvider,
        IApplicationStateService applicationStateService,
        IAppLogger? logger = null,
        TimeProvider? timeProvider = null,
        IKeyboardHookSource? keyboardHookSource = null)
    {
        _inputPlaybackAdapter = inputPlaybackAdapter;
        _cursorPositionProvider = cursorPositionProvider;
        _applicationStateService = applicationStateService;
        _logger = logger ?? NullAppLogger.Instance;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _keyboardHookSource = keyboardHookSource;
    }

    public bool IsPlaying => _applicationStateService.IsState(AppState.Playing);
    public bool IsPaused => _applicationStateService.IsState(AppState.Paused);

    public event Action? PlaybackStarted;
    public event Action? PlaybackPaused;
    public event Action? PlaybackResumed;
    public event Action? PlaybackStopped;
    public event Action<MacroEvent>? EventPlayed;

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_resourcesDisposed) { return; }
            if (_playbackCancellationTokenSource is not null || _manualOperationCount != 0)
            {
                throw new InvalidOperationException("Etkin oynatmayı kapatmak için DisposeAsync beklenmelidir.");
            }
            _shutdownRequested = true;
            _resourcesDisposed = true;
        }
        _eventExecutionGate.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource flight;
        Task playbackCompletion;
        Task manualOperations;
        lock (_syncRoot)
        {
            if (_disposeFlight is not null) { return new ValueTask(_disposeFlight.Task); }
            if (_resourcesDisposed) { return ValueTask.CompletedTask; }
            _shutdownRequested = true;
            flight = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeFlight = flight;
            playbackCompletion = _playbackCompletion?.Task ?? Task.CompletedTask;
            manualOperations = _manualOperationsDrained?.Task ?? Task.CompletedTask;
        }
        _ = CompleteDisposalAsync(flight, playbackCompletion, manualOperations);
        return new ValueTask(flight.Task);
    }

    private async Task CompleteDisposalAsync(TaskCompletionSource flight, Task playbackCompletion, Task manualOperations)
    {
        try
        {
            try
            {
                await StopAsync().ConfigureAwait(false);
            }
            finally
            {
                // Stop is request-only; shutdown also waits for release and final notifications.
                await playbackCompletion.ConfigureAwait(false);
                await manualOperations.ConfigureAwait(false);
                Dispose();
            }
            flight.TrySetResult();
        }
        catch (Exception exception)
        {
            flight.TrySetException(exception);
        }
    }

    public async Task PlayAsync(
        MacroSession session,
        PlaybackSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settings);

        if (session.Events.Count == 0)
        {
            return;
        }

        CancellationTokenSource playbackCancellationTokenSource;
        PlaybackCoordinateResolver? coordinateResolver = null;
        bool stateClaimed = false;
        bool playbackStarted = false;
        bool emergencyStopSubscribed = false;

        // Reserve the owner before publishing Playing; callbacks may request Stop.
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_shutdownRequested, this);
            if (_playbackCancellationTokenSource is not null)
            {
                return;
            }

            _playbackCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _heldInputs = new HeldInputTracker();
            playbackCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _playbackCancellationTokenSource = playbackCancellationTokenSource;
            _stopRequested = false;
            _playbackCompleting = false;
            _playbackInputReady = _keyboardHookSource is null;
            _stopFlight = null;
            _resumeSignal = CreateResumeSignal(signaled: true);
        }

        try
        {
            PlaybackSettings effectiveSettings = NormalizeSettingsForExecution(settings);
            coordinateResolver = PlaybackCoordinateResolver.Create(session, effectiveSettings);
            List<Exception> playbackErrors = [];
            int repeatCount = effectiveSettings.LoopIndefinitely
                ? int.MaxValue
                : Math.Max(effectiveSettings.RepeatCount, 1);
            int? totalLogicalEventCount = effectiveSettings.LoopIndefinitely
                ? null
                : session.Events.Count * repeatCount;

            lock (_syncRoot)
            {
                _activeCoordinateResolver = coordinateResolver;
                _playbackNextLogicalIndex = 0;
                _playbackTotalLogicalEventCount = totalLogicalEventCount;
            }

            // If a subscriber throws, the valid transition has already committed.
            stateClaimed = true;
            if (!_applicationStateService.TryTransitionTo(AppState.Playing, AppState.Idle))
            {
                stateClaimed = false;
                return;
            }

            CancellationToken playbackCancellationToken = playbackCancellationTokenSource.Token;
            if (_keyboardHookSource is not null)
            {
                _keyboardHookSource.KeyActivityReceived += OnEmergencyStopKey;
                emergencyStopSubscribed = true;
                await _keyboardHookSource.StartAsync(playbackCancellationToken).ConfigureAwait(false);
                playbackCancellationToken.ThrowIfCancellationRequested();
                ThrowIfStopRequested();
                lock (_syncRoot)
                {
                    _playbackInputReady = true;
                }
            }

            playbackStarted = true;
            _logger.Log(
                AppLogLevel.Information,
                nameof(MacroPlaybackService),
                $"Oynatma baslatildi. Oturum: {session.Name}, olay sayisi: {session.Events.Count}.");
            SafeInvokePlaybackStarted();

            if (effectiveSettings.InitialDelayMs > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(effectiveSettings.InitialDelayMs), _timeProvider, playbackCancellationToken).ConfigureAwait(false);
            }

            playbackCancellationToken.ThrowIfCancellationRequested();

            while (true)
            {
                playbackCancellationToken.ThrowIfCancellationRequested();
                ThrowIfStopRequested();
                await WaitIfPausedAsync(playbackCancellationToken).ConfigureAwait(false);

                int logicalEventIndex = GetPlaybackCursorSnapshot();

                if (IsPlaybackComplete(logicalEventIndex))
                {
                    break;
                }

                int iteration = logicalEventIndex / session.Events.Count;
                int eventIndex = logicalEventIndex % session.Events.Count;

                await coordinateResolver.PrepareForIterationAsync(
                    iteration,
                    _cursorPositionProvider,
                    playbackCancellationToken).ConfigureAwait(false);

                MacroEvent macroEvent = session.Events[eventIndex];
                MacroEvent playbackEvent = coordinateResolver.Resolve(macroEvent);
                int delayMs = ResolveDelayMs(playbackEvent, effectiveSettings);

                if (delayMs > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayMs), _timeProvider, playbackCancellationToken).ConfigureAwait(false);
                }

                playbackCancellationToken.ThrowIfCancellationRequested();
                ThrowIfStopRequested();

                if (_applicationStateService.IsState(AppState.Paused))
                {
                    continue;
                }

                if (GetPlaybackCursorSnapshot() != logicalEventIndex)
                {
                    continue;
                }

                try
                {
                    await PlayResolvedEventAsync(
                        playbackEvent,
                        effectiveSettings,
                        playbackCancellationToken).ConfigureAwait(false);
                    AdvancePlaybackCursor(logicalEventIndex);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Exception playbackException = CreatePlaybackException(
                        iteration,
                        eventIndex,
                        playbackEvent,
                        ex);

                    _logger.Log(
                        AppLogLevel.Error,
                        nameof(MacroPlaybackService),
                        "Olay oynatilirken hata olustu.",
                        playbackException);

                    if (effectiveSettings.StopOnError)
                    {
                        throw playbackException;
                    }

                    playbackErrors.Add(playbackException);
                    AdvancePlaybackCursor(logicalEventIndex);
                }
            }

            if (playbackErrors.Count > 0)
            {
                throw new AggregateException(
                    "Oynatma bir veya daha fazla olay hatasiyla tamamlandi.",
                    playbackErrors);
            }
        }
        catch (OperationCanceledException) when (_stopRequested || cancellationToken.IsCancellationRequested)
        {
            _logger.Log(
                AppLogLevel.Information,
                nameof(MacroPlaybackService),
                _stopRequested
                    ? "Oynatma kullanici istegiyle durduruldu."
                    : "Oynatma cagiran iptal belirteci tarafindan iptal edildi.");
        }
        catch (Exception ex)
        {
            if (stateClaimed)
            {
                TrySetErrorState();
            }
            _logger.Log(
                AppLogLevel.Error,
                nameof(MacroPlaybackService),
                "Oynatma hata nedeniyle sonlandirildi.",
                ex);
            throw;
        }
        finally
        {
            await CompletePlaybackAsync(
                playbackCancellationTokenSource, coordinateResolver, stateClaimed, playbackStarted, emergencyStopSubscribed)
                .ConfigureAwait(false);
        }
    }

    private async Task CompletePlaybackAsync(
        CancellationTokenSource owner,
        PlaybackCoordinateResolver? coordinateResolver,
        bool stateClaimed,
        bool playbackStarted,
        bool emergencyStopSubscribed)
    {
        Task? stopRequest;
        lock (_syncRoot)
        {
            _playbackCompleting = true;
            stopRequest = _stopFlight?.Task;
        }

        try
        {
            // A stop request retains this owner until its state publication and Cancel finish.
            if (stopRequest is not null)
            {
                try
                {
                    await stopRequest.ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.Log(AppLogLevel.Error, nameof(MacroPlaybackService), "Oynatma durdurma isteği tamamlanamadı.", exception);
                }
            }

            await _eventExecutionGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_heldInputs is not null) { await ReleaseHeldInputsAsync(_heldInputs).ConfigureAwait(false); }
            }
            finally
            {
                _eventExecutionGate.Release();
            }

            if (emergencyStopSubscribed)
            {
                _keyboardHookSource!.KeyActivityReceived -= OnEmergencyStopKey;
                try
                {
                    await _keyboardHookSource.StopAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.Log(AppLogLevel.Error, nameof(MacroPlaybackService), "Acil durdurma kancası kaldırılamadı.", exception);
                }
            }

            lock (_syncRoot)
            {
                _activeCoordinateResolver = null;
                _resumeSignal.TrySetResult(true);
                _resumeSignal = CreateResumeSignal(signaled: true);
            }
            ResetPlaybackRuntime();
            coordinateResolver?.Dispose();

            if (stateClaimed)
            {
                _applicationStateService.TryTransitionTo(
                    AppState.Idle,
                    AppState.Playing,
                    AppState.Paused,
                    AppState.Stopping,
                    AppState.Error);
            }
            if (playbackStarted)
            {
                SafeInvokePlaybackStopped();
            }
        }
        finally
        {
            // Keep the slot through final notifications so reentrant Play cannot be reset by this owner.
            TaskCompletionSource? completed;
            lock (_syncRoot)
            {
                completed = _playbackCompletion;
                _playbackCompletion = null;
                _heldInputs = null;
                _playbackCancellationTokenSource = null;
                _stopFlight = null;
                _stopRequested = false;
                _playbackCompleting = false;
                _playbackInputReady = false;
            }
            owner.Dispose();
            completed?.TrySetResult();
        }
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (_playbackCancellationTokenSource is null)
            {
                return Task.CompletedTask;
            }
        }

        if (!_applicationStateService.TryTransitionTo(AppState.Paused, AppState.Playing))
        {
            return Task.CompletedTask;
        }

        lock (_syncRoot)
        {
            if (_resumeSignal.Task.IsCompleted)
            {
                _resumeSignal = CreateResumeSignal(signaled: false);
            }
        }

        _logger.Log(
            AppLogLevel.Information,
            nameof(MacroPlaybackService),
            "Oynatma duraklatildi.");
        SafeInvokePlaybackPaused();
        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TaskCompletionSource<bool>? resumeSignal;

        lock (_syncRoot)
        {
            if (_playbackCancellationTokenSource is null)
            {
                return Task.CompletedTask;
            }

            resumeSignal = _resumeSignal;
        }

        if (!_applicationStateService.TryTransitionTo(AppState.Playing, AppState.Paused))
        {
            return Task.CompletedTask;
        }

        resumeSignal.TrySetResult(true);
        _logger.Log(
            AppLogLevel.Information,
            nameof(MacroPlaybackService),
            "Oynatma devam ettirildi.");
        SafeInvokePlaybackResumed();
        return Task.CompletedTask;
    }

    private async void OnEmergencyStopKey(KeyboardActivityInfo activity)
    {
        const int cancel = 0x03; // Ctrl+Pause
        const int pause = 0x13;
        const int escape = 0x1B;
        if (!activity.IsKeyDown || activity.ExtraInfo == InputOriginPolicy.PlaybackSignature
            || activity.VirtualKeyCode is not (cancel or pause or escape))
        {
            return;
        }

        try
        {
            // Request cancellation here. The playback owner drains the hook during cleanup.
            await StopAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            try
            {
                _logger.Log(AppLogLevel.Error, nameof(MacroPlaybackService), "Acil durdurma isteği işlenemedi.", exception);
            }
            catch (Exception)
            {
                // A logger failure cannot escape the hook event handler.
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CancellationTokenSource playbackCancellationTokenSource;
        TaskCompletionSource<bool> resumeSignal;
        TaskCompletionSource flight;

        lock (_syncRoot)
        {
            if (_playbackCancellationTokenSource is null)
            {
                return Task.CompletedTask;
            }

            if (_stopFlight is not null)
            {
                return _stopFlight.Task;
            }

            if (_playbackCompleting)
            {
                return Task.CompletedTask;
            }

            flight = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _stopFlight = flight;
            _stopRequested = true;
            playbackCancellationTokenSource = _playbackCancellationTokenSource;
            resumeSignal = _resumeSignal;
        }

        Exception? failure = null;
        try
        {
            _applicationStateService.TryTransitionTo(AppState.Stopping, AppState.Playing, AppState.Paused);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            resumeSignal.TrySetResult(true);
            playbackCancellationTokenSource.Cancel();
            _logger.Log(AppLogLevel.Information, nameof(MacroPlaybackService), "Oynatma durdurma istegi alindi.");
        }
        catch (Exception exception)
        {
            failure ??= exception;
        }

        if (failure is null)
        {
            flight.TrySetResult();
        }
        else
        {
            flight.TrySetException(failure);
        }
        return flight.Task;
    }

    public async Task StopAndWaitAsync(CancellationToken cancellationToken = default)
    {
        Task playbackCompletion;
        Task manualOperations;
        lock (_syncRoot)
        {
            playbackCompletion = _playbackCompletion?.Task ?? Task.CompletedTask;
            manualOperations = _manualOperationsDrained?.Task ?? Task.CompletedTask;
        }

        try
        {
            await StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await playbackCompletion.WaitAsync(cancellationToken).ConfigureAwait(false);
            await manualOperations.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task<MacroEvent> PlayEventAtAsync(
        MacroSession session,
        PlaybackSettings settings,
        int eventIndex,
        CancellationToken cancellationToken = default)
    {
        return PlayEventAtCoreAsync(
            session,
            settings,
            eventIndex,
            logicalEventIndex: null,
            cancellationToken);
    }

    public Task<MacroEvent> PlayEventAtAsync(
        MacroSession session,
        PlaybackSettings settings,
        int eventIndex,
        int logicalEventIndex,
        CancellationToken cancellationToken = default)
    {
        return PlayEventAtCoreAsync(
            session,
            settings,
            eventIndex,
            logicalEventIndex,
            cancellationToken);
    }

    private async Task<MacroEvent> PlayEventAtCoreAsync(
        MacroSession session,
        PlaybackSettings settings,
        int eventIndex,
        int? logicalEventIndex,
        CancellationToken cancellationToken)
    {
        using var operation = BeginManualOperation();
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        CancellationTokenSource? navigationOwner;
        lock (_syncRoot)
        {
            navigationOwner = _playbackCancellationTokenSource;
            if (navigationOwner is not null && !_playbackInputReady)
            {
                throw new InvalidOperationException("Oynatma hazırlanırken tek olay gönderilemez.");
            }
            if (_playbackCompleting || !_applicationStateService.IsAny(AppState.Idle, AppState.Paused)
                || (navigationOwner is not null && !_applicationStateService.IsState(AppState.Paused)))
            {
                throw new InvalidOperationException(
                    "Tek olay oynatma yalnizca uygulama bostayken veya duraklatilmisken kullanilabilir.");
            }
        }

        if (eventIndex < 0 || eventIndex >= session.Events.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eventIndex),
                eventIndex,
                "Oynatilacak olay dizin araligi disinda.");
        }

        int resolvedLogicalEventIndex = logicalEventIndex ?? eventIndex;

        if (resolvedLogicalEventIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(logicalEventIndex),
                logicalEventIndex,
                "Oynatilacak mantiksal olay dizini negatif olamaz.");
        }

        if (logicalEventIndex.HasValue
            && resolvedLogicalEventIndex % session.Events.Count != eventIndex)
        {
            throw new ArgumentException(
                "Mantiksal olay dizini kaynak olay diziniyle eslesmelidir.",
                nameof(logicalEventIndex));
        }

        PlaybackSettings effectiveSettings = NormalizeSettingsForExecution(settings);
        MacroEvent sourceEvent = session.Events[eventIndex];
        MacroEvent playbackEvent = sourceEvent;

        try
        {
            await _eventExecutionGate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // Completion cannot dispose the active resolver while a step is using it.
                PlaybackCoordinateResolver? activeResolver = GetActiveCoordinateResolverIfPaused(navigationOwner);
                using PlaybackCoordinateResolver? stepResolver = activeResolver is null
                    ? PlaybackCoordinateResolver.Create(session, effectiveSettings)
                    : null;
                PlaybackCoordinateResolver coordinateResolver = activeResolver ?? stepResolver!;
                await coordinateResolver.PrepareForLogicalEventAsync(
                    resolvedLogicalEventIndex,
                    session.Events.Count,
                    _cursorPositionProvider,
                    cancellationToken).ConfigureAwait(false);
                playbackEvent = coordinateResolver.Resolve(sourceEvent);

                HeldInputTracker tracker;
                bool standalone;
                lock (_syncRoot)
                {
                    ObjectDisposedException.ThrowIf(_shutdownRequested, this);
                    if (_playbackCompleting || _stopRequested || _playbackCancellationTokenSource != navigationOwner
                        || !_applicationStateService.IsAny(AppState.Idle, AppState.Paused))
                    {
                        throw new InvalidOperationException("Oynatma tamamlanırken tek olay gönderilemez.");
                    }
                    standalone = _playbackCancellationTokenSource is null;
                    if (!standalone && !_applicationStateService.IsState(AppState.Paused))
                    {
                        throw new InvalidOperationException("Etkin oynatmada tek olay yalnız duraklatılmışken gönderilebilir.");
                    }
                    tracker = standalone ? new HeldInputTracker() : _heldInputs!;
                }
                try
                {
                    if (!effectiveSettings.SimulationMode)
                    {
                        await SendTrackedEventAsync(playbackEvent, tracker, cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    if (standalone) { await ReleaseHeldInputsAsync(tracker).ConfigureAwait(false); }
                }
            }
            finally
            {
                _eventExecutionGate.Release();
            }

            _logger.Log(
                AppLogLevel.Information,
                nameof(MacroPlaybackService),
                effectiveSettings.SimulationMode
                    ? $"Tek olay simule edildi. Oturum: {session.Name}, olay: {eventIndex + 1}."
                    : $"Tek olay oynatildi. Oturum: {session.Name}, olay: {eventIndex + 1}.");
            return playbackEvent;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            InvalidOperationException playbackException = CreatePlaybackException(
                0,
                eventIndex,
                playbackEvent,
                ex);
            _logger.Log(
                AppLogLevel.Error,
                nameof(MacroPlaybackService),
                "Tek olay oynatilirken hata olustu.",
                playbackException);
            throw playbackException;
        }
    }

    private PlaybackCoordinateResolver? GetActiveCoordinateResolverIfPaused(CancellationTokenSource? navigationOwner)
    {
        lock (_syncRoot)
        {
            return navigationOwner is not null && _playbackCancellationTokenSource == navigationOwner
                && !_playbackCompleting && _applicationStateService.IsState(AppState.Paused)
                ? _activeCoordinateResolver
                : null;
        }
    }

    public async Task WaitForPlaybackNavigationReadyAsync(CancellationToken cancellationToken = default)
    {
        using var operation = BeginManualOperation();
        cancellationToken.ThrowIfCancellationRequested();

        if (!_applicationStateService.IsState(AppState.Paused))
        {
            return;
        }

        await _eventExecutionGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return;
        }
        finally
        {
            _eventExecutionGate.Release();
        }
    }

    public async Task SeekPlaybackCursorAsync(
        int logicalEventIndex,
        CancellationToken cancellationToken = default)
    {
        using var operation = BeginManualOperation();
        cancellationToken.ThrowIfCancellationRequested();

        if (!_applicationStateService.IsState(AppState.Paused))
        {
            return;
        }

        await _eventExecutionGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            lock (_syncRoot)
            {
                if (_playbackCancellationTokenSource is null
                    || !_applicationStateService.IsState(AppState.Paused))
                {
                    return;
                }

                _playbackNextLogicalIndex = ClampPlaybackCursorIndex(logicalEventIndex);
            }
        }
        finally
        {
            _eventExecutionGate.Release();
        }
    }

    private async Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        Task waitTask;

        lock (_syncRoot)
        {
            if (!_applicationStateService.IsState(AppState.Paused))
            {
                return;
            }

            waitTask = _resumeSignal.Task;
        }

        await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfStopRequested();
    }

    private async Task PlayResolvedEventAsync(
        MacroEvent playbackEvent,
        PlaybackSettings effectiveSettings,
        CancellationToken cancellationToken)
    {
        await _eventExecutionGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!effectiveSettings.SimulationMode)
            {
                await SendTrackedEventAsync(playbackEvent, _heldInputs!, cancellationToken).ConfigureAwait(false);
            }

            SafeInvokeEventPlayed(playbackEvent);
        }
        finally
        {
            _eventExecutionGate.Release();
        }
    }

    private async Task SendTrackedEventAsync(MacroEvent input, HeldInputTracker tracker, CancellationToken cancellationToken)
    {
        if (input.EventType == MacroEventType.Mouse && input.MouseActionType == MouseActionType.DoubleClick)
        {
            // Observe each successful sub-send even if a later double-click send fails.
            await SendMousePartAsync(MouseActionType.LeftDown).ConfigureAwait(false);
            await SendMousePartAsync(MouseActionType.LeftUp).ConfigureAwait(false);
            await SendMousePartAsync(MouseActionType.LeftDown).ConfigureAwait(false);
            await SendMousePartAsync(MouseActionType.LeftUp).ConfigureAwait(false);
            return;
        }
        await SendInputIfRunningAsync(input, cancellationToken).ConfigureAwait(false);
        tracker.RecordSuccessfulSend(input);

        async Task SendMousePartAsync(MouseActionType action)
        {
            var part = new MacroEvent
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = action,
                X = input.X,
                Y = input.Y,
                TimestampUtc = input.TimestampUtc
            };
            await SendInputIfRunningAsync(part, cancellationToken).ConfigureAwait(false);
            tracker.RecordSuccessfulSend(part);
        }
    }

    private Task SendInputIfRunningAsync(MacroEvent input, CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfStopRequested();
            // The Windows adapter sends synchronously. Admit each send under the same lock as Stop.
            return _inputPlaybackAdapter.PlayEventAsync(input, cancellationToken);
        }
    }

    private async Task ReleaseHeldInputsAsync(HeldInputTracker tracker)
    {
        foreach (MacroEvent release in tracker.TakeReleases())
        {
            try
            {
                await _inputPlaybackAdapter.ReleaseEventAsync(release, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                try
                {
                    _logger.Log(AppLogLevel.Error, nameof(MacroPlaybackService), "Oynatmanın basılı girdisi bırakılamadı.", exception);
                }
                catch (Exception)
                {
                    // A logging failure must not prevent the remaining releases.
                }
            }
        }
    }

    private IDisposable BeginManualOperation()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_shutdownRequested, this);
            if (_manualOperationCount++ == 0)
            {
                _manualOperationsDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            return new ManualOperation(this);
        }
    }

    private sealed class ManualOperation(MacroPlaybackService owner) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._syncRoot)
            {
                if (--owner._manualOperationCount == 0) { owner._manualOperationsDrained!.TrySetResult(); }
            }
        }
    }

    private int GetPlaybackCursorSnapshot()
    {
        lock (_syncRoot)
        {
            return ClampPlaybackCursorIndex(_playbackNextLogicalIndex);
        }
    }

    private bool IsPlaybackComplete(int logicalEventIndex)
    {
        lock (_syncRoot)
        {
            return _playbackTotalLogicalEventCount.HasValue
                && logicalEventIndex >= _playbackTotalLogicalEventCount.Value;
        }
    }

    private void AdvancePlaybackCursor(int playedLogicalEventIndex)
    {
        lock (_syncRoot)
        {
            int nextLogicalEventIndex = playedLogicalEventIndex + 1;

            if (nextLogicalEventIndex > _playbackNextLogicalIndex)
            {
                _playbackNextLogicalIndex = ClampPlaybackCursorIndex(nextLogicalEventIndex);
            }
        }
    }

    private int ClampPlaybackCursorIndex(int logicalEventIndex)
    {
        int safeLogicalEventIndex = Math.Max(logicalEventIndex, 0);

        return _playbackTotalLogicalEventCount.HasValue
            ? Math.Min(safeLogicalEventIndex, _playbackTotalLogicalEventCount.Value)
            : safeLogicalEventIndex;
    }

    private void ResetPlaybackRuntime()
    {
        lock (_syncRoot)
        {
            _playbackNextLogicalIndex = 0;
            _playbackTotalLogicalEventCount = null;
        }
    }

    private void TrySetErrorState()
    {
        try
        {
            _applicationStateService.TryTransitionTo(
                AppState.Error,
                AppState.Playing,
                AppState.Paused,
                AppState.Stopping);
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Error,
                nameof(MacroPlaybackService),
                "Oynatma hata durumuna gecirilirken ek bir hata olustu.",
                ex);
        }
    }

    private void ThrowIfStopRequested()
    {
        if (_stopRequested)
        {
            throw new OperationCanceledException("Oynatma icin durdurma istegi alindi.");
        }
    }

    private static PlaybackSettings NormalizeSettingsForExecution(PlaybackSettings settings)
    {
        return new PlaybackSettings
        {
            SpeedMultiplier = settings.PreserveOriginalTiming
                ? 1.0
                : settings.SpeedMultiplier > 0
                    ? settings.SpeedMultiplier
                    : 1.0,
            RepeatCount = Math.Max(settings.RepeatCount, 1),
            InitialDelayMs = Math.Max(settings.InitialDelayMs, 0),
            LoopIndefinitely = settings.LoopIndefinitely,
            UseRelativeCoordinates = settings.UseRelativeCoordinates,
            SimulationMode = settings.SimulationMode,
            StopOnError = settings.StopOnError,
            PreserveOriginalTiming = settings.PreserveOriginalTiming
        };
    }

    private static int ResolveDelayMs(
        MacroEvent playbackEvent,
        PlaybackSettings settings)
    {
        return settings.PreserveOriginalTiming
            ? Math.Max(playbackEvent.DelayMs, 0)
            : ConvertDelayBySpeed(playbackEvent.DelayMs, settings.SpeedMultiplier);
    }

    private static int ConvertDelayBySpeed(int originalDelayMs, double speedMultiplier)
    {
        if (originalDelayMs <= 0)
        {
            return 0;
        }

        if (speedMultiplier <= 0)
        {
            speedMultiplier = 1.0;
        }

        return (int)Math.Max(originalDelayMs / speedMultiplier, 0);
    }

    private static InvalidOperationException CreatePlaybackException(
        int iteration,
        int eventIndex,
        MacroEvent macroEvent,
        Exception innerException)
    {
        string eventDescription = string.IsNullOrWhiteSpace(macroEvent.Description)
            ? FormatEventType(macroEvent.EventType)
            : macroEvent.Description;

        return new InvalidOperationException(
            $"Oynatma {iteration + 1}. tekrarin {eventIndex + 1}. olayinda basarisiz oldu: {eventDescription}.",
            innerException);
    }

    private static string FormatEventType(MacroEventType eventType)
    {
        return eventType switch
        {
            MacroEventType.Keyboard => "klavye",
            MacroEventType.Mouse => "fare",
            MacroEventType.System => "sistem",
            _ => eventType.ToString()
        };
    }

    private static TaskCompletionSource<bool> CreateResumeSignal(bool signaled)
    {
        TaskCompletionSource<bool> taskCompletionSource = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (signaled)
        {
            taskCompletionSource.TrySetResult(true);
        }

        return taskCompletionSource;
    }

    private void SafeInvokePlaybackStarted()
    {
        try
        {
            PlaybackStarted?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Error,
                nameof(MacroPlaybackService),
                "Oynatma basladi bildirimi yayinlanirken hata olustu.",
                ex);
        }
    }

    private void SafeInvokePlaybackPaused()
    {
        try
        {
            PlaybackPaused?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Error,
                nameof(MacroPlaybackService),
                "Oynatma duraklatildi bildirimi yayinlanirken hata olustu.",
                ex);
        }
    }

    private void SafeInvokePlaybackResumed()
    {
        try
        {
            PlaybackResumed?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Error,
                nameof(MacroPlaybackService),
                "Oynatma devam etti bildirimi yayinlanirken hata olustu.",
                ex);
        }
    }

    private void SafeInvokePlaybackStopped()
    {
        try
        {
            PlaybackStopped?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Error,
                nameof(MacroPlaybackService),
                "Oynatma durdu bildirimi yayinlanirken hata olustu.",
                ex);
        }
    }

    private void SafeInvokeEventPlayed(MacroEvent playbackEvent)
    {
        try
        {
            EventPlayed?.Invoke(playbackEvent);
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Error,
                nameof(MacroPlaybackService),
                "Olay oynatildi bildirimi yayinlanirken hata olustu.",
                ex);
        }
    }
}
