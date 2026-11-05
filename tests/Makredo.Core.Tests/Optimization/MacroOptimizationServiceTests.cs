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
using static Makredo.Core.Tests.Support.TestData;

namespace Makredo.Core.Tests.Optimization;

public sealed class MacroOptimizationServiceTests
{
    [Fact(DisplayName = "MacroOptimizationService removes redundant mouse moves and preserves timing")]
    public Task MacroOptimizationService_RemovesRedundantMouseMovesAndPreservesTiming()
    {
        var session = new MacroSession
        {
            Name = "Optimize Test"
        };
        session.Events.AddRange(
            [
                CreateMouseMoveEvent(10, 100, 200),
                CreateMouseMoveEvent(20, 101, 201),
                CreateMouseMoveEvent(30, 102, 202),
                CreateMouseMoveEvent(40, 103, 203),
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftDown,
                    DelayMs = 5,
                    X = 103,
                    Y = 203,
                    Description = "Sol tus basildi"
                },
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftUp,
                    DelayMs = 5,
                    X = 103,
                    Y = 203,
                    Description = "Sol tus birakildi"
                }
            ]);

        var service = new MacroOptimizationService();
        MacroOptimizationPreview preview = service.Preview(session);

        Assert.True(preview.HasChanges, "Redundant move events should produce an optimization preview.");
        Assert.Equal(6, preview.OriginalEventCount);
        Assert.Equal(4, preview.OptimizedEventCount);
        Assert.Equal(2, preview.RemovedEventCount);
        Assert.Equal(session.TotalDurationMs, preview.OptimizedDurationMs);
        Assert.Equal(20, session.Events[1].DelayMs);
        Assert.Equal(90, preview.OptimizedEvents[1].DelayMs);
        Assert.Equal(103, preview.OptimizedEvents[1].X);

        return Task.CompletedTask;
    }

    [Fact(DisplayName = "MacroOptimizationService preserves protected events")]
    public Task MacroOptimizationService_PreservesProtectedEvents()
    {
        var session = new MacroSession
        {
            Name = "Protected Events"
        };
        session.Events.AddRange(
            [
                CreateMouseMoveEvent(10, 100, 200),
                CreateMouseMoveEvent(20, 101, 201),
                CreateMouseMoveEvent(300, 102, 202),
                CreateMouseMoveEvent(20, 103, 203),
                new MacroEvent
                {
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyDown,
                    DelayMs = 10,
                    KeyCode = 65,
                    ScanCode = 30,
                    KeyName = "A",
                    Description = "Tus basildi - A"
                },
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.Wheel,
                    DelayMs = 30,
                    X = 103,
                    Y = 203,
                    WheelDelta = -120,
                    Description = "Fare tekerlegi"
                },
                CreateMouseMoveEvent(20, 104, 204)
            ]);

        var service = new MacroOptimizationService();
        MacroOptimizationPreview preview = service.Preview(session);

        Assert.Equal(1, preview.RemovedEventCount);
        Assert.Equal(session.TotalDurationMs, preview.OptimizedDurationMs);
        Assert.True(
            preview.OptimizedEvents.Any(macroEvent =>
                macroEvent.EventType == MacroEventType.Keyboard
                && macroEvent.KeyboardActionType == KeyboardActionType.KeyDown),
            "Keyboard events should be preserved.");
        Assert.True(
            preview.OptimizedEvents.Any(macroEvent =>
                macroEvent.EventType == MacroEventType.Mouse
                && macroEvent.MouseActionType == MouseActionType.Wheel),
            "Mouse wheel events should be preserved.");
        Assert.True(
            preview.OptimizedEvents.Any(macroEvent =>
                macroEvent.EventType == MacroEventType.Mouse
                && macroEvent.MouseActionType == MouseActionType.Move
                && macroEvent.DelayMs >= MacroOptimizationService.LongDelayThresholdMs),
            "Long-delay mouse movements should be preserved.");

        return Task.CompletedTask;
    }

    [Fact(DisplayName = "MacroOptimizationService keeps balanced mouse path samples")]
    public Task MacroOptimizationService_KeepsBalancedMousePathSamples()
    {
        var session = new MacroSession
        {
            Name = "Balanced Path"
        };

        for (int index = 0; index < 10; index++)
        {
            session.Events.Add(CreateMouseMoveEvent(50, index * 10, 100));
        }

        session.Events.Add(
            new MacroEvent
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.LeftDown,
                DelayMs = 10,
                X = 90,
                Y = 100,
                Description = "Sol tus basildi"
            });

        var service = new MacroOptimizationService();
        MacroOptimizationPreview preview = service.Preview(session);

        MacroEvent[] optimizedMoves = preview.OptimizedEvents
            .Where(macroEvent => macroEvent.EventType == MacroEventType.Mouse
                && macroEvent.MouseActionType == MouseActionType.Move)
            .ToArray();

        Assert.Equal(5, preview.OptimizedEventCount);
        Assert.Equal(4, optimizedMoves.Length);
        Assert.Equal(
            [0, 40, 80, 90],
            optimizedMoves.Select(macroEvent => macroEvent.X!.Value));
        Assert.Equal(session.TotalDurationMs, preview.OptimizedDurationMs);

        return Task.CompletedTask;
    }
}
