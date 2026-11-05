using MacroMaster.Application.Abstractions;
using MacroMaster.Domain.Enums;
using MacroMaster.Domain.Models;

namespace MacroMaster.Application.Services;

internal sealed class PlaybackCoordinateResolver : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly SemaphoreSlim _anchorGate = new(1, 1);
    private readonly bool _useRelativeCoordinates;
    private readonly CursorPosition? _recordedMouseAnchor;

    private CursorPosition? _playbackMouseAnchor;
    private int? _anchoredIteration;
    private bool _disposed;

    private PlaybackCoordinateResolver(
        bool useRelativeCoordinates,
        CursorPosition? recordedMouseAnchor)
    {
        _useRelativeCoordinates = useRelativeCoordinates;
        _recordedMouseAnchor = recordedMouseAnchor;
    }

    private bool CanResolveRelativeCoordinates =>
        _useRelativeCoordinates && _recordedMouseAnchor.HasValue;

    public static PlaybackCoordinateResolver Create(
        MacroSession session,
        PlaybackSettings settings)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settings);

        bool useRelativeCoordinates = settings.UseRelativeCoordinates
            && !settings.SimulationMode;
        CursorPosition? recordedMouseAnchor = useRelativeCoordinates
            ? GetRecordedMouseAnchor(session)
            : null;

        return new PlaybackCoordinateResolver(
            useRelativeCoordinates,
            recordedMouseAnchor);
    }

    public async Task PrepareForIterationAsync(
        int iteration,
        ICursorPositionProvider cursorPositionProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cursorPositionProvider);
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (!CanResolveRelativeCoordinates)
        {
            return;
        }

        int safeIteration = Math.Max(iteration, 0);

        await _anchorGate.WaitAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_syncRoot)
            {
                if (_anchoredIteration == safeIteration
                    && _playbackMouseAnchor.HasValue)
                {
                    return;
                }
            }

            CursorPosition playbackMouseAnchor =
                await cursorPositionProvider.GetCursorPositionAsync(cancellationToken);

            lock (_syncRoot)
            {
                _playbackMouseAnchor = playbackMouseAnchor;
                _anchoredIteration = safeIteration;
            }
        }
        finally
        {
            _anchorGate.Release();
        }
    }

    public Task PrepareForLogicalEventAsync(
        int logicalEventIndex,
        int eventCount,
        ICursorPositionProvider cursorPositionProvider,
        CancellationToken cancellationToken)
    {
        if (eventCount <= 0)
        {
            return Task.CompletedTask;
        }

        int safeLogicalEventIndex = Math.Max(logicalEventIndex, 0);
        int iteration = safeLogicalEventIndex / eventCount;

        return PrepareForIterationAsync(
            iteration,
            cursorPositionProvider,
            cancellationToken);
    }

    public MacroEvent Resolve(MacroEvent macroEvent)
    {
        ArgumentNullException.ThrowIfNull(macroEvent);
        ThrowIfDisposed();

        CursorPosition? playbackMouseAnchor;

        lock (_syncRoot)
        {
            playbackMouseAnchor = _playbackMouseAnchor;
        }

        if (!CanResolveRelativeCoordinates
            || playbackMouseAnchor is null
            || macroEvent.EventType != MacroEventType.Mouse
            || !macroEvent.X.HasValue
            || !macroEvent.Y.HasValue)
        {
            return macroEvent;
        }

        int resolvedX = ResolveRelativeCoordinate(
            macroEvent.X.Value,
            _recordedMouseAnchor!.Value.X,
            playbackMouseAnchor.Value.X);
        int resolvedY = ResolveRelativeCoordinate(
            macroEvent.Y.Value,
            _recordedMouseAnchor.Value.Y,
            playbackMouseAnchor.Value.Y);

        return CloneMacroEventWithCoordinates(macroEvent, resolvedX, resolvedY);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _anchorGate.Dispose();
        _disposed = true;
    }

    private static CursorPosition? GetRecordedMouseAnchor(MacroSession session)
    {
        foreach (MacroEvent macroEvent in session.Events)
        {
            if (macroEvent.EventType == MacroEventType.Mouse
                && macroEvent.X.HasValue
                && macroEvent.Y.HasValue)
            {
                return new CursorPosition(macroEvent.X.Value, macroEvent.Y.Value);
            }
        }

        return null;
    }

    private static int ResolveRelativeCoordinate(
        int recordedCoordinate,
        int recordedAnchor,
        int playbackAnchor)
    {
        long relativeOffset = (long)recordedCoordinate - recordedAnchor;
        long resolvedCoordinate = playbackAnchor + relativeOffset;

        if (resolvedCoordinate < int.MinValue || resolvedCoordinate > int.MaxValue)
        {
            throw new InvalidOperationException(
                $"Goreli fare oynatimi gecersiz bir koordinat uretti: {resolvedCoordinate}.");
        }

        return (int)resolvedCoordinate;
    }

    private static MacroEvent CloneMacroEventWithCoordinates(
        MacroEvent source,
        int x,
        int y)
    {
        return new MacroEvent
        {
            Id = source.Id,
            EventType = source.EventType,
            KeyboardActionType = source.KeyboardActionType,
            MouseActionType = source.MouseActionType,
            DelayMs = source.DelayMs,
            TimestampUtc = source.TimestampUtc,
            KeyCode = source.KeyCode,
            ScanCode = source.ScanCode,
            IsExtendedKey = source.IsExtendedKey,
            KeyName = source.KeyName,
            X = x,
            Y = y,
            WheelDelta = source.WheelDelta,
            Description = source.Description
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
