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

namespace Makredo.Core.Tests.Filtering;

public sealed class EventListFilterTests
{
    [Fact(DisplayName = "EventListFilterEngine applies search and smart filters")]
    public Task EventListFilterEngine_AppliesSearchAndSmartFilters()
    {
        MacroEvent[] events =
        [
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.Move,
                DelayMs = 10,
                X = 10,
                Y = 20,
                Description = "Fare hareketi"
            },
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.LeftDown,
                DelayMs = 30,
                X = 11,
                Y = 21,
                Description = "Sol tus basildi"
            },
            new()
            {
                EventType = MacroEventType.Keyboard,
                KeyboardActionType = KeyboardActionType.KeyDown,
                DelayMs = 5,
                KeyCode = 13,
                ScanCode = 28,
                KeyName = "Enter",
                Description = "Enter tusu"
            },
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.Wheel,
                DelayMs = 260,
                WheelDelta = 120,
                Description = "Fare tekerlegi"
            }
        ];

        EventListViewItem[] keyboardItems = EventListFilterEngine.Apply(
            events,
            new EventListFilterCriteria(null, EventListTypeFilterKind.Keyboard, EventListSmartFilterKind.All));
        EventListViewItem[] mouseItems = EventListFilterEngine.Apply(
            events,
            new EventListFilterCriteria(null, EventListTypeFilterKind.Mouse, EventListSmartFilterKind.All));
        EventListViewItem[] longDelayItems = EventListFilterEngine.Apply(
            events,
            new EventListFilterCriteria(null, EventListTypeFilterKind.All, EventListSmartFilterKind.LongDelays));
        EventListViewItem[] searchItems = EventListFilterEngine.Apply(
            events,
            new EventListFilterCriteria("enter", EventListTypeFilterKind.All, EventListSmartFilterKind.All));
        EventListViewItem[] mousePositionItems = EventListFilterEngine.Apply(
            events,
            new EventListFilterCriteria("21", EventListTypeFilterKind.Mouse, EventListSmartFilterKind.All));

        Assert.Equal(
            [2],
            keyboardItems.Select(item => item.SourceIndex));
        Assert.Equal(
            [0, 1, 3],
            mouseItems.Select(item => item.SourceIndex));
        Assert.Equal(
            [3],
            longDelayItems.Select(item => item.SourceIndex));
        Assert.Equal(
            [2],
            searchItems.Select(item => item.SourceIndex));
        Assert.Equal(
            [1],
            mousePositionItems.Select(item => item.SourceIndex));

        return Task.CompletedTask;
    }

    [Fact(DisplayName = "EventListFilterEngine identifies optimization candidates and invalid events")]
    public Task EventListFilterEngine_IdentifiesOptimizationCandidatesAndInvalidEvents()
    {
        MacroEvent[] optimizationEvents =
        [
            CreateMouseMoveEvent(8, 100, 200),
            CreateMouseMoveEvent(9, 101, 201),
            CreateMouseMoveEvent(10, 102, 202),
            CreateMouseMoveEvent(11, 103, 203),
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.LeftDown,
                DelayMs = 20,
                X = 103,
                Y = 203,
                Description = "Sol tus basildi"
            }
        ];
        MacroEvent[] invalidEvents =
        [
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.LeftDown,
                DelayMs = 10,
                Description = "Koordinatsiz tiklama"
            },
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.Wheel,
                DelayMs = 10,
                Description = "Eksik wheel delta"
            },
            new()
            {
                EventType = MacroEventType.Keyboard,
                KeyboardActionType = KeyboardActionType.KeyDown,
                DelayMs = 10,
                KeyName = "A"
            },
            new()
            {
                EventType = MacroEventType.Keyboard,
                KeyboardActionType = KeyboardActionType.None,
                DelayMs = 10,
                KeyCode = 65,
                ScanCode = 30
            },
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.None,
                DelayMs = 10
            }
        ];

        EventListViewItem[] optimizationCandidates = EventListFilterEngine.Apply(
            optimizationEvents,
            new EventListFilterCriteria(null, EventListTypeFilterKind.All, EventListSmartFilterKind.OptimizationCandidates));
        EventListViewItem[] invalidItems = EventListFilterEngine.Apply(
            invalidEvents,
            new EventListFilterCriteria(null, EventListTypeFilterKind.All, EventListSmartFilterKind.InvalidOrIncomplete));

        Assert.Equal(
            [1, 2],
            optimizationCandidates.Select(item => item.SourceIndex));
        Assert.Equal(
            [0, 1, 2, 3, 4],
            invalidItems.Select(item => item.SourceIndex));

        Assert.Equal(
            EventListRowInsight.OptimizationCandidate,
            EventListFilterEngine.GetInsight(optimizationEvents, 1));
        Assert.Equal(
            EventListRowInsight.InvalidOrIncomplete,
            EventListFilterEngine.GetInsight(invalidEvents, 0));

        return Task.CompletedTask;
    }

    [Fact(DisplayName = "EventListFilterEngine supports structured search expressions")]
    public Task EventListFilterEngine_SupportsStructuredSearchExpressions()
    {
        MacroEvent[] events =
        [
            CreateMouseMoveEvent(10, 680, 291),
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.LeftDown,
                DelayMs = 231,
                X = 521,
                Y = 288,
                Description = "Sol tus basildi"
            },
            new()
            {
                EventType = MacroEventType.Keyboard,
                KeyboardActionType = KeyboardActionType.KeyDown,
                DelayMs = 12,
                KeyCode = 13,
                ScanCode = 28,
                KeyName = "Enter",
                Description = "Enter tusu"
            },
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.Wheel,
                DelayMs = 320,
                WheelDelta = -120,
                Description = "Fare tekerlegi"
            }
        ];

        Assert.Equal(
            [1],
            EventListFilterEngine.Apply(
                    events,
                    new EventListFilterCriteria("x:521 y:288", EventListTypeFilterKind.All, EventListSmartFilterKind.All))
                .Select(item => item.SourceIndex));
        Assert.Equal(
            [3],
            EventListFilterEngine.Apply(
                    events,
                    new EventListFilterCriteria("delay>=250 type:mouse", EventListTypeFilterKind.All, EventListSmartFilterKind.All))
                .Select(item => item.SourceIndex));
        Assert.Equal(
            [2],
            EventListFilterEngine.Apply(
                    events,
                    new EventListFilterCriteria("row:3 key:enter", EventListTypeFilterKind.All, EventListSmartFilterKind.All))
                .Select(item => item.SourceIndex));
        Assert.Equal(
            [0],
            EventListFilterEngine.Apply(
                    events,
                    new EventListFilterCriteria("action:move elapsed<20", EventListTypeFilterKind.All, EventListSmartFilterKind.All))
                .Select(item => item.SourceIndex));

        return Task.CompletedTask;
    }
}
