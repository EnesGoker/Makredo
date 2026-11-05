using MacroMaster.Application.Abstractions;
using MacroMaster.Domain.Enums;
using MacroMaster.Domain.Models;

namespace MacroMaster.Application.Services;

public sealed class MacroPlaybackService : IMacroPlaybackService, IDisposable
{
    private readonly IInputPlaybackAdapter _inputPlaybackAdapter;
    private readonly ICursorPositionProvider _cursorPositionProvider;
    private readonly IApplicationStateService _applicationStateService;
    private readonly IAppLogger _logger;
    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _eventExecutionGate = new(1, 1);

    private bool _stopRequested;
    private CancellationTokenSource? _playbackCancellationTokenSource;
    private TaskCompletionSource<bool> _resumeSignal = CreateResumeSignal(signaled: true);
    private PlaybackCoordinateResolver? _activeCoordinateResolver;
    private int _playbackNextLogicalIndex;
    private int? _playbackTotalLogicalEventCount;

    public MacroPlaybackService(
        IInputPlaybackAdapter inputPlaybackAdapter,
        ICursorPositionProvider cursorPositionProvider,
        IApplicationStateService applicationStateService,
        IAppLogger? logger = null)
    {
        _inputPlaybackAdapter = inputPlaybackAdapter;
        _cursorPositionProvider = cursorPositionProvider;
        _applicationStateService = applicationStateService;
        _logger = logger ?? NullAppLogger.Instance;
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
        PlaybackCoordinateResolver? activeCoordinateResolver;

        lock (_syncRoot)
        {
            activeCoordinateResolver = _activeCoordinateResolver;
            _activeCoordinateResolver = null;
        }

        activeCoordinateResolver?.Dispose();
        _eventExecutionGate.Dispose();
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

        if (!_applicationStateService.TryTransitionTo(AppState.Playing, AppState.Idle))
        {
            return;
        }

        PlaybackSettings effectiveSettings = NormalizeSettingsForExecution(settings);
        PlaybackCoordinateResolver coordinateResolver =
            PlaybackCoordinateResolver.Create(session, effectiveSettings);
        CancellationTokenSource? playbackCancellationTokenSource = null;
        bool playbackStarted = false;
        List<Exception> playbackErrors = [];
        int repeatCount = effectiveSettings.LoopIndefinitely
            ? int.MaxValue
            : Math.Max(effectiveSettings.RepeatCount, 1);
        int? totalLogicalEventCount = effectiveSettings.LoopIndefinitely
            ? null
            : session.Events.Count * repeatCount;

        try
        {
            lock (_syncRoot)
            {
                if (_playbackCancellationTokenSource is not null)
                {
                    _applicationStateService.TryTransitionTo(AppState.Idle, AppState.Playing);
                    return;
                }

                _stopRequested = false;
                _resumeSignal = CreateResumeSignal(signaled: true);
                _playbackCancellationTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                playbackCancellationTokenSource = _playbackCancellationTokenSource;
                _activeCoordinateResolver = coordinateResolver;
                _playbackNextLogicalIndex = 0;
                _playbackTotalLogicalEventCount = totalLogicalEventCount;
            }

            playbackStarted = true;
            _logger.Log(
                AppLogLevel.Information,
                nameof(MacroPlaybackService),
                $"Oynatma baslatildi. Oturum: {session.Name}, olay sayisi: {session.Events.Count}.");
            SafeInvokePlaybackStarted();

            CancellationToken playbackCancellationToken =
                playbackCancellationTokenSource.Token;

            if (effectiveSettings.InitialDelayMs > 0)
            {
                await Task.Delay(effectiveSettings.InitialDelayMs, playbackCancellationToken);
            }

            playbackCancellationToken.ThrowIfCancellationRequested();

            while (true)
            {
                playbackCancellationToken.ThrowIfCancellationRequested();
                ThrowIfStopRequested();
                await WaitIfPausedAsync(playbackCancellationToken);

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
                    playbackCancellationToken);

                MacroEvent macroEvent = session.Events[eventIndex];
                MacroEvent playbackEvent = coordinateResolver.Resolve(macroEvent);
                int delayMs = ResolveDelayMs(playbackEvent, effectiveSettings);

                if (delayMs > 0)
                {
                    await Task.Delay(delayMs, playbackCancellationToken);
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
                        playbackCancellationToken);
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
            TrySetErrorState();
            _logger.Log(
                AppLogLevel.Error,
                nameof(MacroPlaybackService),
                "Oynatma hata nedeniyle sonlandirildi.",
                ex);
            throw;
        }
        finally
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(
                        _playbackCancellationTokenSource,
                        playbackCancellationTokenSource))
                {
                    _playbackCancellationTokenSource = null;
                }

                if (ReferenceEquals(_activeCoordinateResolver, coordinateResolver))
                {
                    _activeCoordinateResolver = null;
                }

                _stopRequested = false;
                _resumeSignal.TrySetResult(true);
                _resumeSignal = CreateResumeSignal(signaled: true);
            }

            playbackCancellationTokenSource?.Dispose();
            coordinateResolver.Dispose();

            if (playbackStarted
                || _applicationStateService.IsAny(
                    AppState.Playing,
                    AppState.Paused,
                    AppState.Stopping,
                    AppState.Error))
            {
                _applicationStateService.TryTransitionTo(
                    AppState.Idle,
                    AppState.Playing,
                    AppState.Paused,
                    AppState.Stopping,
                    AppState.Error);
                ResetPlaybackRuntime();
                SafeInvokePlaybackStopped();
            }
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

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CancellationTokenSource? playbackCancellationTokenSource;
        TaskCompletionSource<bool>? resumeSignal;

        lock (_syncRoot)
        {
            if (_playbackCancellationTokenSource is null
                && !_applicationStateService.IsAny(AppState.Playing, AppState.Paused))
            {
                return Task.CompletedTask;
            }

            _stopRequested = true;
            playbackCancellationTokenSource = _playbackCancellationTokenSource;
            resumeSignal = _resumeSignal;
        }

        _applicationStateService.TryTransitionTo(
            AppState.Stopping,
            AppState.Playing,
            AppState.Paused);

        resumeSignal?.TrySetResult(true);
        playbackCancellationTokenSource?.Cancel();
        _logger.Log(
            AppLogLevel.Information,
            nameof(MacroPlaybackService),
            "Oynatma durdurma istegi alindi.");

        return Task.CompletedTask;
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
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_applicationStateService.IsAny(AppState.Idle, AppState.Paused))
        {
            throw new InvalidOperationException(
                "Tek olay oynatma yalnizca uygulama bostayken veya duraklatilmisken kullanilabilir.");
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
        PlaybackCoordinateResolver? coordinateResolver = GetActiveCoordinateResolverIfPaused();
        bool shouldDisposeCoordinateResolver = coordinateResolver is null;

        coordinateResolver ??= PlaybackCoordinateResolver.Create(session, effectiveSettings);
        MacroEvent playbackEvent = sourceEvent;

        try
        {
            await coordinateResolver.PrepareForLogicalEventAsync(
                resolvedLogicalEventIndex,
                session.Events.Count,
                _cursorPositionProvider,
                cancellationToken);
            playbackEvent = coordinateResolver.Resolve(sourceEvent);

            await _eventExecutionGate.WaitAsync(cancellationToken);

            try
            {
                if (!effectiveSettings.SimulationMode)
                {
                    await _inputPlaybackAdapter.PlayEventAsync(playbackEvent, cancellationToken);
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
        finally
        {
            if (shouldDisposeCoordinateResolver)
            {
                coordinateResolver.Dispose();
            }
        }
    }

    private PlaybackCoordinateResolver? GetActiveCoordinateResolverIfPaused()
    {
        if (!_applicationStateService.IsState(AppState.Paused))
        {
            return null;
        }

        lock (_syncRoot)
        {
            return _activeCoordinateResolver;
        }
    }

    public async Task WaitForPlaybackNavigationReadyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_applicationStateService.IsState(AppState.Paused))
        {
            return;
        }

        await _eventExecutionGate.WaitAsync(cancellationToken);

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
        cancellationToken.ThrowIfCancellationRequested();

        if (!_applicationStateService.IsState(AppState.Paused))
        {
            return;
        }

        await _eventExecutionGate.WaitAsync(cancellationToken);

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

        await waitTask.WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfStopRequested();
    }

    private async Task PlayResolvedEventAsync(
        MacroEvent playbackEvent,
        PlaybackSettings effectiveSettings,
        CancellationToken cancellationToken)
    {
        await _eventExecutionGate.WaitAsync(cancellationToken);

        try
        {
            if (!effectiveSettings.SimulationMode)
            {
                await _inputPlaybackAdapter.PlayEventAsync(playbackEvent, cancellationToken);
            }

            SafeInvokeEventPlayed(playbackEvent);
        }
        finally
        {
            _eventExecutionGate.Release();
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
