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

namespace Makredo.Core.Tests.Playback;

public sealed class MacroPlaybackServiceTests
{
    [Fact(DisplayName = "MacroPlaybackService rebases mouse coordinates when relative playback is enabled")]
    public async Task MacroPlaybackService_UseRelativeCoordinates_RebasesMouseCoordinates()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter
        {
            CurrentCursorPosition = new CursorPosition(500, 300)
        };
        using var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            new ApplicationStateService());

        var session = new MacroSession
        {
            Name = "RelativeMouse",
            Events =
            {
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.Move,
                    X = 100,
                    Y = 200,
                    Description = "Move anchor"
                },
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftDown,
                    X = 130,
                    Y = 240,
                    Description = "Click relative"
                }
            }
        };

        await playbackService.PlayAsync(
            session,
            new PlaybackSettings { UseRelativeCoordinates = true });

        Assert.Equal(1, inputPlaybackAdapter.CursorPositionReadCount);
        Assert.Equal(2, inputPlaybackAdapter.PlayedEvents.Count);
        Assert.Equal(500, inputPlaybackAdapter.PlayedEvents[0].X);
        Assert.Equal(300, inputPlaybackAdapter.PlayedEvents[0].Y);
        Assert.Equal(530, inputPlaybackAdapter.PlayedEvents[1].X);
        Assert.Equal(340, inputPlaybackAdapter.PlayedEvents[1].Y);
    }

    [Fact(DisplayName = "MacroPlaybackService re-anchors relative mouse playback for each repeat iteration")]
    public async Task MacroPlaybackService_UseRelativeCoordinates_ReanchorsEachIteration()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        inputPlaybackAdapter.CursorPositions.Enqueue(new CursorPosition(500, 300));
        inputPlaybackAdapter.CursorPositions.Enqueue(new CursorPosition(1000, 200));

        using var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            new ApplicationStateService());

        var session = new MacroSession
        {
            Name = "RelativeRepeat",
            Events =
            {
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.Move,
                    X = 100,
                    Y = 200,
                    Description = "Anchor"
                },
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftUp,
                    X = 130,
                    Y = 240,
                    Description = "Delta"
                }
            }
        };

        await playbackService.PlayAsync(
            session,
            new PlaybackSettings
            {
                UseRelativeCoordinates = true,
                RepeatCount = 2
            });

        Assert.Equal(2, inputPlaybackAdapter.CursorPositionReadCount);
        Assert.Equal(4, inputPlaybackAdapter.PlayedEvents.Count);
        Assert.Equal(500, inputPlaybackAdapter.PlayedEvents[0].X);
        Assert.Equal(530, inputPlaybackAdapter.PlayedEvents[1].X);
        Assert.Equal(1000, inputPlaybackAdapter.PlayedEvents[2].X);
        Assert.Equal(1030, inputPlaybackAdapter.PlayedEvents[3].X);
    }

    [Fact(DisplayName = "MacroPlaybackService does not query cursor anchors when no relative mouse anchor exists")]
    public async Task MacroPlaybackService_UseRelativeCoordinates_NoMouseAnchor_DoesNotQueryCursor()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter
        {
            CurrentCursorPosition = new CursorPosition(500, 300)
        };
        using var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            new ApplicationStateService());

        var session = new MacroSession
        {
            Name = "RelativeWithoutMouse",
            Events =
            {
                new MacroEvent
                {
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyDown,
                    KeyCode = 0x41,
                    ScanCode = 0x1E,
                    Description = "Keyboard down"
                }
            }
        };

        await playbackService.PlayAsync(
            session,
            new PlaybackSettings { UseRelativeCoordinates = true });

        Assert.Equal(0, inputPlaybackAdapter.CursorPositionReadCount);
        Assert.Single(inputPlaybackAdapter.PlayedEvents);
        Assert.Equal(MacroEventType.Keyboard, inputPlaybackAdapter.PlayedEvents[0].EventType);
        Assert.False(inputPlaybackAdapter.PlayedEvents[0].X.HasValue, "Keyboard events should not receive synthetic X coordinates.");
        Assert.False(inputPlaybackAdapter.PlayedEvents[0].Y.HasValue, "Keyboard events should not receive synthetic Y coordinates.");
    }

    [Fact(DisplayName = "MacroPlaybackService keeps the active relative anchor for paused single-step playback")]
    public async Task MacroPlaybackService_UseRelativeCoordinates_PausedStepUsesActiveAnchor()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        inputPlaybackAdapter.CursorPositions.Enqueue(new CursorPosition(500, 300));
        var applicationStateService = new ApplicationStateService();
        using var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            applicationStateService);
        var firstEventPlayed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var session = new MacroSession
        {
            Name = "RelativePausedStep",
            Events =
            {
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.Move,
                    X = 100,
                    Y = 200,
                    Description = "Anchor"
                },
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftDown,
                    X = 150,
                    Y = 220,
                    Description = "Manual step"
                },
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftUp,
                    X = 170,
                    Y = 250,
                    Description = "Resume step"
                }
            }
        };

        playbackService.EventPlayed += macroEvent =>
        {
            if (macroEvent.Id != session.Events[0].Id
                || firstEventPlayed.Task.IsCompleted)
            {
                return;
            }

            Assert.True(playbackService.PauseAsync().IsCompletedSuccessfully, "Pause must complete before the synchronous input callback returns.");
            inputPlaybackAdapter.CurrentCursorPosition = new CursorPosition(900, 900);
            firstEventPlayed.TrySetResult(true);
        };

        Task playTask = playbackService.PlayAsync(
            session,
            new PlaybackSettings
            {
                UseRelativeCoordinates = true,
                StopOnError = true
            });

        await firstEventPlayed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await playbackService.WaitForPlaybackNavigationReadyAsync();
        await playbackService.PlayEventAtAsync(
            session,
            new PlaybackSettings
            {
                UseRelativeCoordinates = true,
                StopOnError = true
            },
            eventIndex: 1,
            logicalEventIndex: 1);
        await playbackService.SeekPlaybackCursorAsync(2);
        await playbackService.ResumeAsync();
        await playTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, inputPlaybackAdapter.CursorPositionReadCount);
        int[] expectedXCoordinates = [500, 550, 570];
        int[] expectedYCoordinates = [300, 320, 350];

        Assert.Equal(
            expectedXCoordinates,
            inputPlaybackAdapter.PlayedEvents.Select(macroEvent => macroEvent.X!.Value).ToArray());
        Assert.Equal(
            expectedYCoordinates,
            inputPlaybackAdapter.PlayedEvents.Select(macroEvent => macroEvent.Y!.Value).ToArray());
    }

    [Fact(DisplayName = "MacroPlaybackService re-anchors paused relative single-step playback across repeat iterations")]
    public async Task MacroPlaybackService_UseRelativeCoordinates_PausedStepReanchorsNewIteration()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        inputPlaybackAdapter.CursorPositions.Enqueue(new CursorPosition(500, 300));
        var applicationStateService = new ApplicationStateService();
        using var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            applicationStateService);
        var firstIterationCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var session = new MacroSession
        {
            Name = "RelativePausedRepeatStep",
            Events =
            {
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.Move,
                    X = 100,
                    Y = 200,
                    Description = "Anchor"
                },
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftUp,
                    X = 130,
                    Y = 240,
                    Description = "Delta"
                }
            }
        };

        playbackService.EventPlayed += macroEvent =>
        {
            if (macroEvent.Id != session.Events[1].Id
                || firstIterationCompleted.Task.IsCompleted)
            {
                return;
            }

            Assert.True(playbackService.PauseAsync().IsCompletedSuccessfully, "Pause must complete before the synchronous input callback returns.");
            inputPlaybackAdapter.CurrentCursorPosition = new CursorPosition(1000, 200);
            firstIterationCompleted.TrySetResult(true);
        };

        Task playTask = playbackService.PlayAsync(
            session,
            new PlaybackSettings
            {
                UseRelativeCoordinates = true,
                RepeatCount = 2,
                StopOnError = true
            });

        await firstIterationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await playbackService.WaitForPlaybackNavigationReadyAsync();
        await playbackService.PlayEventAtAsync(
            session,
            new PlaybackSettings
            {
                UseRelativeCoordinates = true,
                RepeatCount = 2,
                StopOnError = true
            },
            eventIndex: 0,
            logicalEventIndex: 2);
        await playbackService.SeekPlaybackCursorAsync(3);
        await playbackService.ResumeAsync();
        await playTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, inputPlaybackAdapter.CursorPositionReadCount);
        int[] expectedXCoordinates = [500, 530, 1000, 1030];
        int[] expectedYCoordinates = [300, 340, 200, 240];

        Assert.Equal(
            expectedXCoordinates,
            inputPlaybackAdapter.PlayedEvents.Select(macroEvent => macroEvent.X!.Value).ToArray());
        Assert.Equal(
            expectedYCoordinates,
            inputPlaybackAdapter.PlayedEvents.Select(macroEvent => macroEvent.Y!.Value).ToArray());
    }

    [Fact(DisplayName = "MacroPlaybackService preserves absolute mouse coordinates when relative playback is disabled")]
    public async Task MacroPlaybackService_UseRelativeCoordinatesFalse_PreservesAbsoluteCoordinates()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter
        {
            CurrentCursorPosition = new CursorPosition(900, 900)
        };
        using var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            new ApplicationStateService());

        var session = new MacroSession
        {
            Name = "AbsoluteMouse",
            Events =
            {
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.Move,
                    X = 42,
                    Y = 64,
                    Description = "Absolute move"
                }
            }
        };

        await playbackService.PlayAsync(
            session,
            new PlaybackSettings { UseRelativeCoordinates = false });

        Assert.Equal(0, inputPlaybackAdapter.CursorPositionReadCount);
        Assert.Single(inputPlaybackAdapter.PlayedEvents);
        Assert.Equal(42, inputPlaybackAdapter.PlayedEvents[0].X);
        Assert.Equal(64, inputPlaybackAdapter.PlayedEvents[0].Y);
    }

    [Fact(DisplayName = "MacroPlaybackService simulation mode advances without sending input")]
    public async Task MacroPlaybackService_SimulationMode_AdvancesWithoutSendingInput()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter
        {
            CurrentCursorPosition = new CursorPosition(900, 900)
        };
        using var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            new ApplicationStateService());
        List<Guid> simulatedEventIds = [];

        playbackService.EventPlayed += macroEvent => simulatedEventIds.Add(macroEvent.Id);

        MacroSession session = CreatePlaybackSession();

        await playbackService.PlayAsync(
            session,
            new PlaybackSettings
            {
                SimulationMode = true,
                UseRelativeCoordinates = true,
                PreserveOriginalTiming = false,
                SpeedMultiplier = 4.0,
                StopOnError = true
            });

        Assert.Empty(inputPlaybackAdapter.AttemptedEventIds);
        Assert.Equal(0, inputPlaybackAdapter.CursorPositionReadCount);
        Assert.Equal(
            session.Events.Select(macroEvent => macroEvent.Id).ToArray(),
            simulatedEventIds);
    }

    [Fact(DisplayName = "MacroPlaybackService honors repeat count")]
    public async Task MacroPlaybackService_RepeatCount_ReplaysEntireSession()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        using var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            new ApplicationStateService());

        MacroSession session = CreatePlaybackSession();

        await playbackService.PlayAsync(
            session,
            new PlaybackSettings
            {
                RepeatCount = 3,
                StopOnError = true
            });

        Assert.Equal(6, inputPlaybackAdapter.PlayedEvents.Count);
        Assert.Equal(6, inputPlaybackAdapter.SuccessfulEventIds.Count);
    }

    [Fact(DisplayName = "MacroPlaybackService pauses until resume is requested")]
    public async Task MacroPlaybackService_PauseResume_WaitsForResume()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        var applicationStateService = new ApplicationStateService();
        using var playbackService = new MacroPlaybackService(inputPlaybackAdapter, inputPlaybackAdapter, applicationStateService);
        var firstEventPlayed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var session = new MacroSession
        {
            Name = "DuraklatDevamEt",
            Events =
            {
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyDown,
                    KeyCode = 0x41,
                    ScanCode = 0x1E,
                    Description = "Ilk olay"
                },
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyUp,
                    KeyCode = 0x41,
                    ScanCode = 0x1E,
                    Description = "Ikinci olay"
                }
            }
        };

        playbackService.EventPlayed += macroEvent =>
        {
            if (macroEvent.Id != session.Events[0].Id)
            {
                return;
            }

            Assert.True(playbackService.PauseAsync().IsCompletedSuccessfully, "Pause must complete before the synchronous input callback returns.");
            firstEventPlayed.TrySetResult(true);
        };

        Task playTask = playbackService.PlayAsync(
            session,
            new PlaybackSettings
            {
                PreserveOriginalTiming = true,
                StopOnError = true
            });

        await firstEventPlayed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await playbackService.WaitForPlaybackNavigationReadyAsync();
        Assert.False(playTask.IsCompleted);

        Assert.Single(inputPlaybackAdapter.PlayedEvents);
        Assert.Equal(AppState.Paused, applicationStateService.CurrentState);

        await playbackService.ResumeAsync();
        await playTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, inputPlaybackAdapter.PlayedEvents.Count);
        Assert.Equal(AppState.Idle, applicationStateService.CurrentState);
    }

    [Fact(DisplayName = "MacroPlaybackService seeks paused playback before resume")]
    public async Task MacroPlaybackService_SeekPausedPlayback_ResumesFromCursor()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        var applicationStateService = new ApplicationStateService();
        using var playbackService = new MacroPlaybackService(inputPlaybackAdapter, inputPlaybackAdapter, applicationStateService);
        var firstEventPlayed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        MacroSession session = CreateThreeEventPlaybackSession();

        playbackService.EventPlayed += macroEvent =>
        {
            if (macroEvent.Id != session.Events[0].Id
                || firstEventPlayed.Task.IsCompleted)
            {
                return;
            }

            Assert.True(playbackService.PauseAsync().IsCompletedSuccessfully, "Pause must complete before the synchronous input callback returns.");
            firstEventPlayed.TrySetResult(true);
        };

        Task playTask = playbackService.PlayAsync(
            session,
            new PlaybackSettings { StopOnError = true });

        await firstEventPlayed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await playbackService.WaitForPlaybackNavigationReadyAsync();
        await playbackService.SeekPlaybackCursorAsync(0);
        await playbackService.ResumeAsync();
        await playTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(
            new[]
            {
                session.Events[0].Id,
                session.Events[0].Id,
                session.Events[1].Id,
                session.Events[2].Id
            },
            inputPlaybackAdapter.PlayedEvents.Select(macroEvent => macroEvent.Id).ToArray());
        Assert.Equal(AppState.Idle, applicationStateService.CurrentState);
    }

    [Fact(DisplayName = "MacroPlaybackService supports single stepping while paused")]
    public async Task MacroPlaybackService_PausedSingleStep_ContinuesAfterSteppedEvent()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        var applicationStateService = new ApplicationStateService();
        using var playbackService = new MacroPlaybackService(inputPlaybackAdapter, inputPlaybackAdapter, applicationStateService);
        var firstEventPlayed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        MacroSession session = CreateThreeEventPlaybackSession();

        playbackService.EventPlayed += macroEvent =>
        {
            if (macroEvent.Id != session.Events[0].Id
                || firstEventPlayed.Task.IsCompleted)
            {
                return;
            }

            Assert.True(playbackService.PauseAsync().IsCompletedSuccessfully, "Pause must complete before the synchronous input callback returns.");
            firstEventPlayed.TrySetResult(true);
        };

        Task playTask = playbackService.PlayAsync(
            session,
            new PlaybackSettings { StopOnError = true });

        await firstEventPlayed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await playbackService.WaitForPlaybackNavigationReadyAsync();
        await playbackService.PlayEventAtAsync(session, new PlaybackSettings { StopOnError = true }, eventIndex: 1);
        await playbackService.SeekPlaybackCursorAsync(2);
        await playbackService.ResumeAsync();
        await playTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(
            new[]
            {
                session.Events[0].Id,
                session.Events[1].Id,
                session.Events[2].Id
            },
            inputPlaybackAdapter.PlayedEvents.Select(macroEvent => macroEvent.Id).ToArray());
        Assert.Equal(AppState.Idle, applicationStateService.CurrentState);
    }

    [Fact(DisplayName = "MacroPlaybackService does not replay a stale delayed event after paused stepping")]
    public async Task MacroPlaybackService_PausedStepDuringDelay_DoesNotReplayStaleEvent()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        var applicationStateService = new ApplicationStateService();
        var timeProvider = new SignalingFakeTimeProvider();
        using var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            applicationStateService,
            timeProvider: timeProvider);
        var firstEventPlayed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        MacroSession session = CreateThreeEventPlaybackSession();
        session.Events[1].DelayMs = 250;

        playbackService.EventPlayed += macroEvent =>
        {
            if (macroEvent.Id == session.Events[0].Id)
            {
                firstEventPlayed.TrySetResult(true);
            }
        };

        Task playTask = playbackService.PlayAsync(
            session,
            new PlaybackSettings
            {
                PreserveOriginalTiming = true,
                StopOnError = true
            });

        await firstEventPlayed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        TimeSpan scheduledDelay = await timeProvider.TimerScheduled.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromMilliseconds(250), scheduledDelay);
        Assert.False(playTask.IsCompleted);
        await playbackService.PauseAsync();
        await playbackService.PlayEventAtAsync(
            session,
            new PlaybackSettings
            {
                PreserveOriginalTiming = true,
                StopOnError = true
            },
            eventIndex: 1);
        await playbackService.SeekPlaybackCursorAsync(2);
        await playbackService.ResumeAsync();
        timeProvider.Advance(scheduledDelay);
        await playTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(
            new[]
            {
                session.Events[0].Id,
                session.Events[1].Id,
                session.Events[2].Id
            },
            inputPlaybackAdapter.PlayedEvents.Select(macroEvent => macroEvent.Id).ToArray());
    }

    [Fact(DisplayName = "MacroPlaybackService stops immediately on playback errors when StopOnError is enabled")]
    public async Task MacroPlaybackService_StopOnErrorTrue_StopsImmediately()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        var applicationStateService = new ApplicationStateService();
        using var playbackService = new MacroPlaybackService(inputPlaybackAdapter, inputPlaybackAdapter, applicationStateService);
        List<AppState> observedStates = [];

        applicationStateService.StateChanged += observedStates.Add;

        MacroSession session = CreatePlaybackSession();
        inputPlaybackAdapter.FailOnEventIds.Add(session.Events[0].Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => playbackService.PlayAsync(session, new PlaybackSettings { StopOnError = true }));

        Assert.Single(inputPlaybackAdapter.AttemptedEventIds);
        Assert.Equal(
            new[] { AppState.Playing, AppState.Error, AppState.Idle },
            observedStates);
        Assert.Equal(AppState.Idle, applicationStateService.CurrentState);
    }

    [Fact(DisplayName = "MacroPlaybackService continues and reports failures when StopOnError is disabled")]
    public async Task MacroPlaybackService_StopOnErrorFalse_ContinuesAndReports()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        var applicationStateService = new ApplicationStateService();
        using var playbackService = new MacroPlaybackService(inputPlaybackAdapter, inputPlaybackAdapter, applicationStateService);
        List<AppState> observedStates = [];

        applicationStateService.StateChanged += observedStates.Add;

        MacroSession session = CreatePlaybackSession();
        inputPlaybackAdapter.FailOnEventIds.Add(session.Events[0].Id);

        await Assert.ThrowsAsync<AggregateException>(
            () => playbackService.PlayAsync(session, new PlaybackSettings { StopOnError = false }));

        Assert.Equal(2, inputPlaybackAdapter.AttemptedEventIds.Count);
        Assert.Single(inputPlaybackAdapter.SuccessfulEventIds);
        Assert.Equal(
            new[] { AppState.Playing, AppState.Error, AppState.Idle },
            observedStates);
        Assert.Equal(AppState.Idle, applicationStateService.CurrentState);
    }
}
