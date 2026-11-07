using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Core.Tests.Concurrency;
using Makredo.Core.Tests.Support;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Xunit;

namespace Makredo.Core.Tests.Playback;

public sealed class EmergencyStopTests
{
    private const int Escape = 0x1B;
    private const int Pause = 0x13;
    private const int Cancel = 0x03;
    private const int TenMinutesMs = 600_000;
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LongDelay = TimeSpan.FromMilliseconds(TenMinutesMs);

    [Theory]
    [InlineData(Escape, HotkeyModifiers.Control, false)]
    [InlineData(Escape, HotkeyModifiers.Shift, false)]
    [InlineData(Escape, HotkeyModifiers.Alt, false)]
    [InlineData(Cancel, HotkeyModifiers.Control, false)]
    [InlineData(Pause, HotkeyModifiers.None, false)]
    [InlineData(Escape, HotkeyModifiers.Control, true)]
    public async Task EmergencyKeyInterruptsLongWaitAndReleasesOwnedInputs(int keyCode, HotkeyModifiers modifiers, bool injected)
    {
        var keyboard = new TestKeyboardHookSource();
        var adapter = new TestInputPlaybackAdapter();
        var state = new ApplicationStateService();
        var time = new SignalingFakeTimeProvider();
        await using var playback = new MacroPlaybackService(adapter, adapter, state, timeProvider: time, keyboardHookSource: keyboard);
        DateTimeOffset startedAt = time.GetUtcNow();
        var heldShift = new MacroEvent
        {
            EventType = MacroEventType.Keyboard,
            KeyboardActionType = KeyboardActionType.KeyDown,
            KeyCode = 0xA0,
            ScanCode = 0x2A
        };
        var heldButton = new MacroEvent { EventType = MacroEventType.Mouse, MouseActionType = MouseActionType.LeftDown };
        var delayedMove = TestData.CreateMouseMoveEvent(TenMinutesMs, 120, 240);
        var session = new MacroSession { Events = { heldShift, heldButton, delayedMove } };
        int sendsAtIdle = 0;
        bool hookRunningAtIdle = true;
        state.StateChanged += value =>
        {
            if (value == AppState.Idle)
            {
                sendsAtIdle = adapter.PlayedEvents.Count;
                hookRunningAtIdle = keyboard.IsRunning;
            }
        };

        Task play = playback.PlayAsync(session, new PlaybackSettings());
        Assert.Equal(LongDelay, await time.TimerScheduled.WaitAsync(Deadline, TimeProvider.System));
        Assert.True(keyboard.IsRunning);
        Assert.Equal(2, adapter.PlayedEvents.Count);

        keyboard.Emit(Activity(keyCode, modifiers, injected: injected));
        await play.WaitAsync(Deadline, TimeProvider.System);

        Assert.Equal(startedAt, time.GetUtcNow());
        time.Advance(LongDelay);
        Assert.Equal(AppState.Idle, state.CurrentState);
        Assert.False(hookRunningAtIdle);
        Assert.Equal(4, sendsAtIdle);
        Assert.Collection(adapter.PlayedEvents,
            down => Assert.Equal(heldShift.Id, down.Id),
            down => Assert.Equal(heldButton.Id, down.Id),
            up => Assert.Equal(MouseActionType.LeftUp, up.MouseActionType),
            up =>
            {
                Assert.Equal(KeyboardActionType.KeyUp, up.KeyboardActionType);
                Assert.Equal(heldShift.KeyCode, up.KeyCode);
                Assert.Equal(heldShift.ScanCode, up.ScanCode);
            });
    }

    [Theory]
    [InlineData(Escape, true, true, true)]
    [InlineData(Escape, true, false, true)]
    [InlineData(Escape, false, false, false)]
    [InlineData(0x42, true, false, false)]
    public async Task SignedEscReleaseAndOrdinaryKeyDoNotInterruptPlayback(int keyCode, bool down, bool injected, bool marked)
    {
        var keyboard = new TestKeyboardHookSource();
        var adapter = new TestInputPlaybackAdapter();
        var time = new SignalingFakeTimeProvider();
        await using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService(),
            timeProvider: time, keyboardHookSource: keyboard);
        MacroSession session = TestData.CreateSession("Ignore non-emergency input", TenMinutesMs);

        Task play = playback.PlayAsync(session, new PlaybackSettings());
        Assert.Equal(LongDelay, await time.TimerScheduled.WaitAsync(Deadline, TimeProvider.System));
        Assert.True(keyboard.IsRunning);

        keyboard.Emit(Activity(keyCode, HotkeyModifiers.Control, down, injected,
            marked ? InputOriginPolicy.PlaybackSignature : 0));
        Assert.True(playback.IsPlaying);
        time.Advance(LongDelay);
        await play.WaitAsync(Deadline, TimeProvider.System);

        Assert.Equal(session.Events[0].Id, Assert.Single(adapter.PlayedEvents).Id);
        Assert.False(keyboard.IsRunning);
    }

    [Fact]
    public async Task EscapeCancelsTheInitialDelayBeforeAnyInputIsSent()
    {
        var keyboard = new TestKeyboardHookSource();
        var adapter = new TestInputPlaybackAdapter();
        var state = new ApplicationStateService();
        var time = new SignalingFakeTimeProvider();
        await using var playback = new MacroPlaybackService(adapter, adapter, state, timeProvider: time, keyboardHookSource: keyboard);
        MacroSession session = TestData.CreatePlaybackSession();

        Task play = playback.PlayAsync(session, new PlaybackSettings { InitialDelayMs = TenMinutesMs });
        Assert.Equal(LongDelay, await time.TimerScheduled.WaitAsync(Deadline, TimeProvider.System));
        Assert.True(keyboard.IsRunning);
        keyboard.Emit(Activity(Escape, HotkeyModifiers.None));
        await play.WaitAsync(Deadline, TimeProvider.System);

        time.Advance(LongDelay);
        Assert.Empty(adapter.AttemptedEventIds);
        Assert.Equal(AppState.Idle, state.CurrentState);
        Assert.False(keyboard.IsRunning);
    }

    [Fact]
    public async Task EscapeStopsPausedPlaybackWithoutResumingIt()
    {
        var keyboard = new TestKeyboardHookSource();
        var adapter = new TestInputPlaybackAdapter();
        var state = new ApplicationStateService();
        await using var playback = new MacroPlaybackService(adapter, adapter, state, keyboardHookSource: keyboard);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MacroSession session = TestData.CreateThreeEventPlaybackSession();
        playback.EventPlayed += item =>
        {
            if (item.Id == session.Events[0].Id)
            {
                if (playback.PauseAsync().IsCompletedSuccessfully)
                {
                    paused.TrySetResult();
                }
            }
        };

        Task play = playback.PlayAsync(session, new PlaybackSettings());
        await paused.Task.WaitAsync(Deadline, TimeProvider.System);
        Assert.True(playback.IsPaused);
        Assert.True(keyboard.IsRunning);
        keyboard.Emit(Activity(Escape, HotkeyModifiers.None));
        await play.WaitAsync(Deadline, TimeProvider.System);

        Assert.Equal(AppState.Idle, state.CurrentState);
        Assert.False(keyboard.IsRunning);
        Assert.Collection(adapter.PlayedEvents,
            down => Assert.Equal(session.Events[0].Id, down.Id),
            up =>
            {
                Assert.Equal(KeyboardActionType.KeyUp, up.KeyboardActionType);
                Assert.Equal(session.Events[0].KeyCode, up.KeyCode);
            });
    }

    [Fact]
    public async Task PausedNavigationCannotSendUntilEmergencyHookIsArmed()
    {
        var startGate = new AsyncCallGate();
        var keyboard = new GatedKeyboardHook { StartGate = startGate };
        var adapter = new TestInputPlaybackAdapter();
        var state = new ApplicationStateService();
        await using var playback = new MacroPlaybackService(adapter, adapter, state, keyboardHookSource: keyboard);
        var armed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        playback.PlaybackStarted += () => armed.TrySetResult();
        MacroSession session = TestData.CreatePlaybackSession();
        var settings = new PlaybackSettings();

        Task play = playback.PlayAsync(session, settings);
        try
        {
            await startGate.Entered.WaitAsync(Deadline, TimeProvider.System);
            await playback.PauseAsync();
            Assert.True(playback.IsPaused);
            Assert.False(keyboard.IsRunning);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                playback.PlayEventAtAsync(session, settings, 0).WaitAsync(Deadline, TimeProvider.System));
            Assert.Empty(adapter.AttemptedEventIds);

            startGate.Release();
            await armed.Task.WaitAsync(Deadline, TimeProvider.System);
            Assert.True(keyboard.IsRunning);
            await playback.PlayEventAtAsync(session, settings, 0).WaitAsync(Deadline, TimeProvider.System);
            Assert.Equal(session.Events[0].Id, Assert.Single(adapter.AttemptedEventIds));
        }
        finally
        {
            startGate.Release();
            await playback.StopAsync();
            await play.WaitAsync(Deadline, TimeProvider.System);
        }
    }

    [Fact]
    public async Task EmergencyStopPreventsFurtherDoubleClickSendsAfterAnAcceptedDown()
    {
        var keyboard = new TestKeyboardHookSource();
        var adapter = new GatedDoubleClickAdapter();
        var state = new ApplicationStateService();
        await using var playback = new MacroPlaybackService(adapter, adapter, state, keyboardHookSource: keyboard);
        var session = new MacroSession
        {
            Events = { new MacroEvent { EventType = MacroEventType.Mouse, MouseActionType = MouseActionType.DoubleClick } }
        };
        Task play = playback.PlayAsync(session, new PlaybackSettings());
        try
        {
            await adapter.FirstSend.Entered.WaitAsync(Deadline, TimeProvider.System);
            keyboard.Emit(Activity(Escape, HotkeyModifiers.None));
            Assert.Equal(AppState.Stopping, state.CurrentState);
        }
        finally
        {
            adapter.FirstSend.Release();
        }

        await play.WaitAsync(Deadline, TimeProvider.System);
        Assert.Equal(MouseActionType.LeftDown, Assert.Single(adapter.Sends).MouseActionType);
        Assert.Equal(MouseActionType.LeftUp, Assert.Single(adapter.Releases).MouseActionType);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    public async Task EscapeIsRecordedWhileTheSharedKeyboardHookBelongsToRecording()
    {
        var keyboard = new TestKeyboardHookSource();
        var mouse = new TestMouseHookSource();
        var adapter = new TestInputPlaybackAdapter();
        var state = new ApplicationStateService();
        await using var playback = new MacroPlaybackService(adapter, adapter, state, keyboardHookSource: keyboard);
        var recorder = new MacroRecorderService(keyboard, mouse, state, new MutableHotkeyConfiguration());

        await recorder.StartAsync("Escape during recording");
        try
        {
            await playback.PlayAsync(TestData.CreatePlaybackSession(), new PlaybackSettings());
            keyboard.Emit(Activity(Escape, HotkeyModifiers.None));
            Assert.Equal(AppState.Recording, state.CurrentState);
            Assert.True(keyboard.IsRunning);
            Assert.Empty(adapter.AttemptedEventIds);
        }
        finally
        {
            await recorder.StopAsync();
        }

        MacroEvent recorded = Assert.Single(recorder.CurrentSession!.Events);
        Assert.Equal(KeyboardActionType.KeyDown, recorded.KeyboardActionType);
        Assert.Equal(Escape, recorded.KeyCode);
    }

    private static KeyboardActivityInfo Activity(int keyCode, HotkeyModifiers modifiers, bool down = true,
        bool injected = false, ulong signature = 0) =>
        new(keyCode, 0, down, false, modifiers, false, HotkeyModifiers.None, IsInjected: injected, ExtraInfo: signature);

    private sealed class GatedDoubleClickAdapter : IInputPlaybackAdapter, ICursorPositionProvider
    {
        internal AsyncCallGate FirstSend { get; } = new();
        internal List<MacroEvent> Sends { get; } = [];
        internal List<MacroEvent> Releases { get; } = [];

        public Task<CursorPosition> GetCursorPositionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CursorPosition(0, 0));

        public Task PlayEventAsync(MacroEvent macroEvent, CancellationToken cancellationToken = default)
        {
            Sends.Add(macroEvent);
            // An accepted send can finish after the cancellation request.
            return Sends.Count == 1 ? FirstSend.EnterAsync(CancellationToken.None) : Task.CompletedTask;
        }

        public Task ReleaseEventAsync(MacroEvent releaseEvent, CancellationToken cancellationToken = default)
        {
            Releases.Add(releaseEvent);
            return Task.CompletedTask;
        }
    }
}
