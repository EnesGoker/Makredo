using Makredo.Application.Abstractions;
using Makredo.Application.Filters;
using Makredo.Application.Reporting;
using Makredo.Application.Services;
using Makredo.Core.Tests.Support;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Makredo.Infrastructure.Diagnostics;
using Makredo.Infrastructure.Persistence;
using Xunit;

namespace Makredo.Core.Tests.Support;

internal sealed class TestInputPlaybackAdapter : IInputPlaybackAdapter, ICursorPositionProvider
{
    public CursorPosition CurrentCursorPosition { get; set; } = new(0, 0);

    public int CursorPositionReadCount { get; private set; }

    public Queue<CursorPosition> CursorPositions { get; } = new();

    public List<Guid> AttemptedEventIds { get; } = [];

    public List<Guid> SuccessfulEventIds { get; } = [];

    public HashSet<Guid> FailOnEventIds { get; } = [];

    public List<MacroEvent> PlayedEvents { get; } = [];

    public Task<CursorPosition> GetCursorPositionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CursorPositionReadCount++;
        CursorPosition cursorPosition = CursorPositions.Count > 0
            ? CursorPositions.Dequeue()
            : CurrentCursorPosition;
        return Task.FromResult(cursorPosition);
    }

    public Task PlayEventAsync(MacroEvent macroEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AttemptedEventIds.Add(macroEvent.Id);

        if (FailOnEventIds.Contains(macroEvent.Id))
        {
            throw new InvalidOperationException(
                $"Simulated playback failure for event {macroEvent.Description}.");
        }

        SuccessfulEventIds.Add(macroEvent.Id);
        PlayedEvents.Add(CloneEvent(macroEvent));
        return Task.CompletedTask;
    }

    private static MacroEvent CloneEvent(MacroEvent source)
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
            X = source.X,
            Y = source.Y,
            WheelDelta = source.WheelDelta,
            Description = source.Description
        };
    }
}
