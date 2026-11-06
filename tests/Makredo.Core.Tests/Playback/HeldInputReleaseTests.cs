using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Core.Tests.Support;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Xunit;

namespace Makredo.Core.Tests.Playback;

public sealed class HeldInputReleaseTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task StopReleasesOwnedKeyboardAndMouseBeforeIdle()
    {
        var adapter = new HeldInputAdapter();
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state);
        var observations = ObserveCompletion(playback, state, adapter);
        MacroSession session = CreateBlockedSession(adapter);
        Task play = playback.PlayAsync(session, new PlaybackSettings());

        try
        {
            await WithinDeadline(adapter.GateEntered.Task);
            await playback.StopAsync();
            await WithinDeadline(play);
        }
        finally
        {
            adapter.ReleaseGate();
            await playback.StopAsync();
            await WithinDeadline(play);
        }

        AssertOwnedInputsReleased(adapter);
        Assert.Equal<int>([0, 0], observations);
    }

    [Fact]
    public async Task CallerCancellationReleasesOwnedKeyboardAndMouseWithUncancelledToken()
    {
        var adapter = new HeldInputAdapter();
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state);
        using var cancellation = new CancellationTokenSource();
        MacroSession session = CreateBlockedSession(adapter);
        Task play = playback.PlayAsync(session, new PlaybackSettings(), cancellation.Token);

        try
        {
            await WithinDeadline(adapter.GateEntered.Task);
            cancellation.Cancel();
            await WithinDeadline(play);
        }
        finally
        {
            cancellation.Cancel();
            adapter.ReleaseGate();
            await WithinDeadline(play);
        }

        AssertOwnedInputsReleased(adapter);
        Assert.All(adapter.ReleaseTokens, token => Assert.False(token.IsCancellationRequested));
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    public async Task PlaybackErrorReleasesOwnedKeyboardAndMouseBeforeIdle()
    {
        var adapter = new HeldInputAdapter();
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state);
        var observations = ObserveCompletion(playback, state, adapter);
        MacroEvent failure = new() { EventType = MacroEventType.System };
        adapter.Failures[failure.Id] = 1;
        MacroSession session = Session(KeyDown(0xA0, 0x2A), MouseDown(MouseActionType.LeftDown), failure);

        await Assert.ThrowsAsync<InvalidOperationException>(() => playback.PlayAsync(session, new PlaybackSettings()));

        AssertOwnedInputsReleased(adapter);
        Assert.Equal<int>([0, 0], observations);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    public async Task NormalCompletionReleasesOwnedKeyboardAndMouseBeforeIdle()
    {
        var adapter = new HeldInputAdapter();
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state);
        var observations = ObserveCompletion(playback, state, adapter);

        await playback.PlayAsync(Session(KeyDown(0xA0, 0x2A), MouseDown(MouseActionType.LeftDown)), new PlaybackSettings());

        AssertOwnedInputsReleased(adapter);
        Assert.Equal<int>([0, 0], observations);
    }

    [Fact]
    public async Task WindowShutdownWaitsForOwnedInputReleaseBeforeIdle()
    {
        var adapter = new HeldInputAdapter { HoldCleanupReleases = true };
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state);
        var observations = ObserveCompletion(playback, state, adapter);
        Task play = playback.PlayAsync(CreateBlockedSession(adapter), new PlaybackSettings());

        try
        {
            await WithinDeadline(adapter.GateEntered.Task);
            IAsyncDisposable closing = Assert.IsAssignableFrom<IAsyncDisposable>(playback);
            Task shutdown = closing.DisposeAsync().AsTask();
            await WithinDeadline(adapter.CleanupEntered.Task);
            Assert.False(shutdown.IsCompleted);
            Assert.NotEqual(AppState.Idle, state.CurrentState);
            adapter.AllowCleanup();
            await WithinDeadline(shutdown);
            await WithinDeadline(play);
        }
        finally
        {
            adapter.AllowCleanup();
            adapter.ReleaseGate();
            if (!play.IsCompleted) { await playback.StopAsync(); }
            await WithinDeadline(play);
        }

        AssertOwnedInputsReleased(adapter);
        Assert.Equal<int>([0, 0], observations);
    }

    [Fact]
    public async Task CleanupReleasesInputsInReversePressOrder()
    {
        var adapter = new HeldInputAdapter();
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());

        await playback.PlayAsync(
            Session(KeyDown(0xA2, 0x1D), KeyDown(0xA0, 0x2A), KeyDown(0x41, 0x1E), MouseDown(MouseActionType.LeftDown)),
            new PlaybackSettings());

        Assert.Equal<string>(["mouse:LeftUp", "key:65:30:False:KeyUp", "key:160:42:False:KeyUp", "key:162:29:False:KeyUp"],
            adapter.Attempts.Where(IsRelease).Select(Describe));
        Assert.Empty(adapter.HeldInputs);
    }

    [Theory]
    [InlineData(0xA0, 0x2A, false)]
    [InlineData(0xA1, 0x36, false)]
    [InlineData(0xA2, 0x1D, false)]
    [InlineData(0xA3, 0x1D, true)]
    [InlineData(0xA4, 0x38, false)]
    [InlineData(0xA5, 0x38, true)]
    public async Task ReleasePreservesModifierSideScanCodeAndExtendedFlag(int keyCode, int scanCode, bool extended)
    {
        var adapter = new HeldInputAdapter();
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());

        await playback.PlayAsync(Session(KeyDown(keyCode, scanCode, extended)), new PlaybackSettings());

        MacroEvent release = Assert.Single(adapter.Attempts, IsRelease);
        Assert.Equal(KeyboardActionType.KeyUp, release.KeyboardActionType);
        Assert.Equal(keyCode, release.KeyCode);
        Assert.Equal(scanCode, release.ScanCode);
        Assert.Equal(extended, release.IsExtendedKey);
        Assert.Empty(adapter.HeldInputs);
    }

    [Theory]
    [InlineData(MouseActionType.LeftDown, MouseActionType.LeftUp)]
    [InlineData(MouseActionType.RightDown, MouseActionType.RightUp)]
    [InlineData(MouseActionType.MiddleDown, MouseActionType.MiddleUp)]
    public async Task ReleaseUsesTheMatchingMouseButton(MouseActionType down, MouseActionType up)
    {
        var adapter = new HeldInputAdapter();
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());

        await playback.PlayAsync(Session(MouseDown(down)), new PlaybackSettings());

        MacroEvent release = Assert.Single(adapter.Attempts, IsRelease);
        Assert.Equal(MacroEventType.Mouse, release.EventType);
        Assert.Equal(up, release.MouseActionType);
        Assert.Empty(adapter.HeldInputs);
    }

    [Fact]
    public async Task FailedDownIsNotAddedToOwnedInputs()
    {
        var adapter = new HeldInputAdapter();
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());
        MacroEvent failedDown = KeyDown(0x41, 0x1E);
        adapter.Failures[failedDown.Id] = 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() => playback.PlayAsync(
            Session(MouseDown(MouseActionType.LeftDown), failedDown), new PlaybackSettings()));

        MacroEvent release = Assert.Single(adapter.Attempts, IsRelease);
        Assert.Equal(MouseActionType.LeftUp, release.MouseActionType);
        Assert.Empty(adapter.HeldInputs);
    }

    [Fact]
    public async Task FailedRecordedUpRetainsOwnershipAndIsRetriedDuringCleanup()
    {
        var adapter = new HeldInputAdapter();
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());
        MacroEvent failedUp = KeyUp(0xA3, 0x1D, extended: true);
        adapter.Failures[failedUp.Id] = 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() => playback.PlayAsync(
            Session(KeyDown(0xA3, 0x1D, extended: true), failedUp), new PlaybackSettings()));

        Assert.Equal(2, adapter.Attempts.Count(IsRelease));
        Assert.Single(adapter.Successful, IsRelease);
        Assert.Empty(adapter.HeldInputs);
    }

    [Fact]
    public async Task DoubleClickPartialFailureReleasesTheSuccessfullyPressedButton()
    {
        var adapter = new HeldInputAdapter { FailingMouseReleaseAttempt = 2 };
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => playback.PlayAsync(
            Session(new MacroEvent { EventType = MacroEventType.Mouse, MouseActionType = MouseActionType.DoubleClick }),
            new PlaybackSettings()));

        Assert.Equal<MouseActionType>([MouseActionType.LeftDown, MouseActionType.LeftUp, MouseActionType.LeftDown, MouseActionType.LeftUp, MouseActionType.LeftUp],
            adapter.Attempts.Select(item => item.MouseActionType));
        Assert.Empty(adapter.HeldInputs);
    }

    [Fact]
    public async Task FailedCleanupReleaseIsLoggedAndOtherInputsAreStillReleased()
    {
        var adapter = new HeldInputAdapter { FailingReleaseKey = 0x41 };
        var logger = new RecordingTestLogger();
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state, logger);

        await playback.PlayAsync(
            Session(KeyDown(0xA2, 0x1D), KeyDown(0xA0, 0x2A), KeyDown(0x41, 0x1E), MouseDown(MouseActionType.LeftDown)),
            new PlaybackSettings());

        Assert.Equal<string>(["mouse:LeftUp", "key:65:30:False:KeyUp", "key:160:42:False:KeyUp", "key:162:29:False:KeyUp"],
            adapter.Attempts.Where(IsRelease).Select(Describe));
        Assert.Equal<string>(["key:65:30:False"], adapter.HeldInputs);
        Assert.Contains(logger.Entries, entry => entry.LogLevel == AppLogLevel.Error && entry.Exception is IOException);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    public async Task SuccessfullyRecordedUpsDoNotCauseDuplicateCleanupReleases()
    {
        var adapter = new HeldInputAdapter();
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());
        MacroSession session = Session(
            KeyDown(0xA0, 0x2A), MouseDown(MouseActionType.LeftDown),
            KeyUp(0xA0, 0x2A), new MacroEvent { EventType = MacroEventType.Mouse, MouseActionType = MouseActionType.LeftUp });

        await playback.PlayAsync(session, new PlaybackSettings());

        Assert.Equal(session.Events.Select(item => item.Id), adapter.Attempts.Select(item => item.Id));
        Assert.Empty(adapter.HeldInputs);
    }

    [Fact]
    public async Task RepeatedDownDoesNotCauseDuplicateCleanupReleases()
    {
        var adapter = new HeldInputAdapter();
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());

        await playback.PlayAsync(Session(KeyDown(0x41, 0x1E), KeyDown(0x41, 0x1E)), new PlaybackSettings());

        Assert.Single(adapter.Attempts, IsRelease);
        Assert.Empty(adapter.HeldInputs);
    }

    [Fact]
    public async Task CleanupOnlyReleasesInputsPressedByThisPlayback()
    {
        var adapter = new HeldInputAdapter();
        adapter.HeldInputs.Add("key:66:48:False");
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());

        await playback.PlayAsync(Session(KeyDown(0x41, 0x1E)), new PlaybackSettings());

        MacroEvent release = Assert.Single(adapter.Attempts, IsRelease);
        Assert.Equal(0x41, release.KeyCode);
        Assert.Equal<string>(["key:66:48:False"], adapter.HeldInputs);
    }

    [Fact]
    public async Task SimulationDoesNotSendPressesOrCleanupReleases()
    {
        var adapter = new HeldInputAdapter();
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());

        await playback.PlayAsync(Session(KeyDown(0xA0, 0x2A), MouseDown(MouseActionType.LeftDown)),
            new PlaybackSettings { SimulationMode = true });

        Assert.Empty(adapter.Attempts);
        Assert.Empty(adapter.HeldInputs);
    }

    [Fact]
    public async Task PausedManualStepIsIncludedInTheActivePlaybackCleanup()
    {
        var adapter = new HeldInputAdapter();
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MacroEvent initial = new() { EventType = MacroEventType.System };
        MacroSession session = Session(initial, KeyDown(0xA3, 0x1D, extended: true));
        playback.EventPlayed += item =>
        {
            if (item.Id == initial.Id)
            {
                Task pause = playback.PauseAsync();
                Assert.True(pause.IsCompletedSuccessfully);
                paused.TrySetResult();
            }
        };
        Task play = playback.PlayAsync(session, new PlaybackSettings());

        try
        {
            await WithinDeadline(paused.Task);
            await playback.WaitForPlaybackNavigationReadyAsync();
            await playback.PlayEventAtAsync(session, new PlaybackSettings(), 1);
            await playback.StopAsync();
            await WithinDeadline(play);
        }
        finally
        {
            await playback.StopAsync();
            await WithinDeadline(play);
        }

        Assert.Single(adapter.Attempts, IsRelease);
        Assert.Empty(adapter.HeldInputs);
    }

    private static List<int> ObserveCompletion(MacroPlaybackService playback, ApplicationStateService state, HeldInputAdapter adapter)
    {
        List<int> observations = [];
        state.StateChanged += current =>
        {
            if (current == AppState.Idle) { observations.Add(adapter.HeldInputs.Count); }
        };
        playback.PlaybackStopped += () => observations.Add(adapter.HeldInputs.Count);
        return observations;
    }

    private static void AssertOwnedInputsReleased(HeldInputAdapter adapter)
    {
        Assert.Equal<string>(["mouse:LeftUp", "key:160:42:False:KeyUp"], adapter.Attempts.Where(IsRelease).Select(Describe));
        Assert.Empty(adapter.HeldInputs);
    }

    private static MacroSession CreateBlockedSession(HeldInputAdapter adapter)
    {
        MacroEvent gate = new() { EventType = MacroEventType.System };
        adapter.GateEventId = gate.Id;
        return Session(KeyDown(0xA0, 0x2A), MouseDown(MouseActionType.LeftDown), gate);
    }

    private static MacroSession Session(params MacroEvent[] events)
    {
        var session = new MacroSession { Name = "Held input regression" };
        session.Events.AddRange(events);
        return session;
    }
    private static Task WithinDeadline(Task task) => task.WaitAsync(Deadline, TimeProvider.System);
    private static MacroEvent KeyDown(int keyCode, int scanCode, bool extended = false) => new()
    {
        EventType = MacroEventType.Keyboard,
        KeyboardActionType = KeyboardActionType.KeyDown,
        KeyCode = keyCode,
        ScanCode = scanCode,
        IsExtendedKey = extended
    };
    private static MacroEvent KeyUp(int keyCode, int scanCode, bool extended = false) => new()
    {
        EventType = MacroEventType.Keyboard,
        KeyboardActionType = KeyboardActionType.KeyUp,
        KeyCode = keyCode,
        ScanCode = scanCode,
        IsExtendedKey = extended
    };
    private static MacroEvent MouseDown(MouseActionType action) => new() { EventType = MacroEventType.Mouse, MouseActionType = action };
    private static bool IsRelease(MacroEvent item) => item.KeyboardActionType == KeyboardActionType.KeyUp
        || item.MouseActionType is MouseActionType.LeftUp or MouseActionType.RightUp or MouseActionType.MiddleUp;
    private static string Describe(MacroEvent item) => item.EventType == MacroEventType.Keyboard
        ? $"key:{item.KeyCode}:{item.ScanCode}:{item.IsExtendedKey}:{item.KeyboardActionType}"
        : $"mouse:{item.MouseActionType}";

    private sealed class HeldInputAdapter : IInputPlaybackAdapter, ICursorPositionProvider
    {
        private readonly TaskCompletionSource _gateRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _cleanupRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _mouseReleaseAttempts;
        internal TaskCompletionSource GateEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource CleanupEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Guid? GateEventId { get; set; }
        internal int? FailingReleaseKey { get; init; }
        internal int? FailingMouseReleaseAttempt { get; init; }
        internal bool HoldCleanupReleases { get; init; }
        internal Dictionary<Guid, int> Failures { get; } = [];
        internal List<MacroEvent> Attempts { get; } = [];
        internal List<MacroEvent> Successful { get; } = [];
        internal List<CancellationToken> ReleaseTokens { get; } = [];
        internal HashSet<string> HeldInputs { get; } = [];

        internal void ReleaseGate() => _gateRelease.TrySetResult();
        internal void AllowCleanup() => _cleanupRelease.TrySetResult();

        public Task<CursorPosition> GetCursorPositionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CursorPosition(0, 0));
        }

        public async Task PlayEventAsync(MacroEvent macroEvent, CancellationToken cancellationToken = default)
        {
            Attempts.Add(macroEvent);
            if (IsRelease(macroEvent))
            {
                ReleaseTokens.Add(cancellationToken);
                if (HoldCleanupReleases)
                {
                    CleanupEntered.TrySetResult();
                    await _cleanupRelease.Task.WaitAsync(cancellationToken);
                }
            }
            if (GateEventId == macroEvent.Id)
            {
                GateEntered.TrySetResult();
                await _gateRelease.Task.WaitAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (Failures.TryGetValue(macroEvent.Id, out int remaining) && remaining > 0)
            {
                Failures[macroEvent.Id] = remaining - 1;
                throw new IOException("The test input send failed.");
            }
            if (macroEvent.KeyboardActionType == KeyboardActionType.KeyUp && macroEvent.KeyCode == FailingReleaseKey)
            {
                throw new IOException("The test release failed.");
            }
            if (macroEvent.MouseActionType == MouseActionType.LeftUp && ++_mouseReleaseAttempts == FailingMouseReleaseAttempt)
            {
                throw new IOException("The test mouse release failed.");
            }

            Successful.Add(macroEvent);
            if (macroEvent.EventType == MacroEventType.Keyboard)
            {
                string identity = $"key:{macroEvent.KeyCode}:{macroEvent.ScanCode}:{macroEvent.IsExtendedKey}";
                if (macroEvent.KeyboardActionType == KeyboardActionType.KeyDown) { HeldInputs.Add(identity); }
                if (macroEvent.KeyboardActionType == KeyboardActionType.KeyUp) { HeldInputs.Remove(identity); }
            }
            else if (macroEvent.EventType == MacroEventType.Mouse)
            {
                switch (macroEvent.MouseActionType)
                {
                    case MouseActionType.LeftDown: HeldInputs.Add("mouse:left"); break;
                    case MouseActionType.LeftUp: HeldInputs.Remove("mouse:left"); break;
                    case MouseActionType.RightDown: HeldInputs.Add("mouse:right"); break;
                    case MouseActionType.RightUp: HeldInputs.Remove("mouse:right"); break;
                    case MouseActionType.MiddleDown: HeldInputs.Add("mouse:middle"); break;
                    case MouseActionType.MiddleUp: HeldInputs.Remove("mouse:middle"); break;
                }
            }
        }
    }
}
