using System.Collections.Concurrent;
using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Xunit;

namespace Makredo.Core.Tests.Playback;

public sealed class HeldInputLifecycleSafetyTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task SimultaneousLeftAndRightControlAreReleasedAsSeparateInputs()
    {
        var adapter = new SafetyPlaybackAdapter();
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());

        await playback.PlayAsync(Session(KeyDown(0xA2, 0x1D), KeyDown(0xA3, 0x1D, extended: true)), new PlaybackSettings());

        Assert.Equal<string>(["163:29:True", "162:29:False"], adapter.Releases.Select(Identity));
        Assert.Empty(adapter.HeldInputs);
    }

    [Fact]
    public async Task RecordedLeftControlUpDoesNotRemoveRightControlOwnership()
    {
        var adapter = new SafetyPlaybackAdapter();
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());

        await playback.PlayAsync(
            Session(KeyDown(0xA2, 0x1D), KeyDown(0xA3, 0x1D, extended: true), KeyUp(0xA2, 0x1D)),
            new PlaybackSettings());

        Assert.Equal<string>(["162:29:False", "163:29:True"], adapter.Releases.Select(Identity));
        Assert.Empty(adapter.HeldInputs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task VirtualKeyOnlyDownIsReleasedWithoutInventingAScanCode(int? scanCode)
    {
        var adapter = new SafetyPlaybackAdapter();
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());

        await playback.PlayAsync(Session(KeyDown(0x41, scanCode)), new PlaybackSettings());

        MacroEvent release = Assert.Single(adapter.Releases);
        Assert.Equal(0x41, release.KeyCode);
        Assert.True(release.ScanCode is null or 0);
        Assert.False(release.IsExtendedKey);
        Assert.Empty(adapter.HeldInputs);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, 0)]
    [InlineData(0, null)]
    [InlineData(0, 0)]
    public async Task VirtualKeyOnlyUpMatchesNullAndZeroScanEncodings(int? downScanCode, int? upScanCode)
    {
        var adapter = new SafetyPlaybackAdapter();
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());
        MacroEvent recordedUp = KeyUp(0x41, upScanCode);

        await playback.PlayAsync(Session(KeyDown(0x41, downScanCode), recordedUp), new PlaybackSettings());

        MacroEvent release = Assert.Single(adapter.Releases);
        Assert.Equal(recordedUp.Id, release.Id);
        Assert.Empty(adapter.HeldInputs);
    }

    [Fact]
    public async Task RepressAfterRecordedUpMovesTheInputToTheNewestReleasePosition()
    {
        var adapter = new SafetyPlaybackAdapter();
        using var playback = new MacroPlaybackService(adapter, adapter, new ApplicationStateService());

        await playback.PlayAsync(
            Session(KeyDown(0xA2, 0x1D), KeyDown(0xA0, 0x2A), KeyUp(0xA2, 0x1D), KeyDown(0xA2, 0x1D)),
            new PlaybackSettings());

        Assert.Equal<string>(["162:29:False", "162:29:False", "160:42:False"], adapter.Releases.Select(Identity));
        Assert.Empty(adapter.HeldInputs);
    }

    [Fact]
    public async Task QueuedPausedStepsCannotSendAfterTheOwnerBeginsCompletion()
    {
        var adapter = new SafetyPlaybackAdapter { GatedKeyCode = 0x42 };
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MacroEvent first = new() { EventType = MacroEventType.System };
        MacroSession session = Session(first, KeyDown(0x70, 0x3B));
        MacroSession steps = Session(KeyDown(0x42, 0x30), KeyDown(0x43, 0x2E), KeyDown(0x44, 0x20));
        List<int> idleObservations = [];
        state.StateChanged += current =>
        {
            if (current == AppState.Idle) { idleObservations.Add(adapter.HeldInputs.Count); }
        };
        playback.EventPlayed += item =>
        {
            if (item.Id == first.Id)
            {
                Assert.True(playback.PauseAsync().IsCompletedSuccessfully);
                paused.TrySetResult();
            }
        };
        Task play = playback.PlayAsync(session, new PlaybackSettings());
        Task<MacroEvent>? inFlight = null;
        Task<MacroEvent>? queuedFirst = null;
        Task<MacroEvent>? queuedSecond = null;

        try
        {
            await WithinDeadline(paused.Task);
            await playback.WaitForPlaybackNavigationReadyAsync();
            inFlight = playback.PlayEventAtAsync(steps, new PlaybackSettings(), 0);
            await WithinDeadline(adapter.SendEntered.Task);
            queuedFirst = playback.PlayEventAtAsync(steps, new PlaybackSettings(), 1);
            queuedSecond = playback.PlayEventAtAsync(steps, new PlaybackSettings(), 2);
            Assert.False(queuedFirst.IsCompleted);
            Assert.False(queuedSecond.IsCompleted);

            await playback.StopAsync();
            Assert.Equal(AppState.Stopping, state.CurrentState);
            adapter.AllowSend();
            await ObserveAdmissionResultAsync(inFlight);
            await ObserveAdmissionResultAsync(queuedFirst);
            await ObserveAdmissionResultAsync(queuedSecond);
            await WithinDeadline(play);
        }
        finally
        {
            adapter.AllowSend();
            adapter.AllowRelease();
            if (!play.IsCompleted) { await playback.StopAsync(); }
            if (inFlight is not null) { await ObserveAdmissionResultAsync(inFlight); }
            if (queuedFirst is not null) { await ObserveAdmissionResultAsync(queuedFirst); }
            if (queuedSecond is not null) { await ObserveAdmissionResultAsync(queuedSecond); }
            await WithinDeadline(play);
        }

        Assert.Equal<int?>([0x42, 0x42], adapter.Attempts.Where(item => item.EventType == MacroEventType.Keyboard).Select(item => item.KeyCode));
        Assert.Single(adapter.Releases);
        Assert.Empty(adapter.HeldInputs);
        Assert.Equal<int>([0], idleObservations);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    public async Task ConcurrentWindowShutdownCallsJoinAnAlreadyStoppingOwnerAndWaitForRelease()
    {
        var adapter = new SafetyPlaybackAdapter { GateSystemSend = true, GateRelease = true };
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state);
        Task play = playback.PlayAsync(Session(KeyDown(0xA0, 0x2A), new MacroEvent { EventType = MacroEventType.System }), new PlaybackSettings());
        Task? firstShutdown = null;
        Task? secondShutdown = null;

        try
        {
            await WithinDeadline(adapter.SendEntered.Task);
            await playback.StopAsync();
            Assert.Equal(AppState.Stopping, state.CurrentState);
            IAsyncDisposable closing = Assert.IsAssignableFrom<IAsyncDisposable>(playback);
            firstShutdown = closing.DisposeAsync().AsTask();
            secondShutdown = closing.DisposeAsync().AsTask();
            Assert.False(firstShutdown.IsCompleted);
            Assert.False(secondShutdown.IsCompleted);
            adapter.AllowSend();
            await WithinDeadline(adapter.ReleaseEntered.Task);
            Assert.False(firstShutdown.IsCompleted);
            Assert.False(secondShutdown.IsCompleted);
            Assert.NotEqual(AppState.Idle, state.CurrentState);
            adapter.AllowRelease();
            await WithinDeadline(Task.WhenAll(firstShutdown, secondShutdown, play));
        }
        finally
        {
            adapter.AllowSend();
            adapter.AllowRelease();
            if (!play.IsCompleted) { await playback.StopAsync(); }
            await WithinDeadline(play);
            if (firstShutdown is not null) { await WithinDeadline(firstShutdown); }
            if (secondShutdown is not null) { await WithinDeadline(secondShutdown); }
        }

        Assert.Single(adapter.Releases);
        Assert.Empty(adapter.HeldInputs);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    private static async Task ObserveAdmissionResultAsync(Task<MacroEvent> step)
    {
        try { await WithinDeadline(step); }
        catch (InvalidOperationException) { }
        catch (OperationCanceledException) { }
    }

    private static Task WithinDeadline(Task task) => task.WaitAsync(Deadline, TimeProvider.System);
    private static MacroSession Session(params MacroEvent[] events)
    {
        var session = new MacroSession { Name = "Held input lifecycle safety" };
        session.Events.AddRange(events);
        return session;
    }
    private static MacroEvent KeyDown(int keyCode, int? scanCode, bool extended = false) => new()
    {
        EventType = MacroEventType.Keyboard,
        KeyboardActionType = KeyboardActionType.KeyDown,
        KeyCode = keyCode,
        ScanCode = scanCode,
        IsExtendedKey = extended
    };
    private static MacroEvent KeyUp(int keyCode, int? scanCode, bool extended = false) => new()
    {
        EventType = MacroEventType.Keyboard,
        KeyboardActionType = KeyboardActionType.KeyUp,
        KeyCode = keyCode,
        ScanCode = scanCode,
        IsExtendedKey = extended
    };
    private static string Identity(MacroEvent item) => $"{item.KeyCode}:{(item.ScanCode is > 0 ? item.ScanCode : null)}:{item.IsExtendedKey}";

    private sealed class SafetyPlaybackAdapter : IInputPlaybackAdapter, ICursorPositionProvider
    {
        private readonly TaskCompletionSource _sendRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly object _syncRoot = new();
        private readonly HashSet<string> _held = [];

        internal int? GatedKeyCode { get; init; }
        internal bool GateSystemSend { get; init; }
        internal bool GateRelease { get; init; }
        internal TaskCompletionSource SendEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ConcurrentQueue<MacroEvent> Attempts { get; } = new();
        internal IEnumerable<MacroEvent> Releases => Attempts.Where(item => item.KeyboardActionType == KeyboardActionType.KeyUp);
        internal HashSet<string> HeldInputs
        {
            get { lock (_syncRoot) { return [.. _held]; } }
        }

        internal void AllowSend() => _sendRelease.TrySetResult();
        internal void AllowRelease() => _releaseRelease.TrySetResult();

        public Task<CursorPosition> GetCursorPositionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CursorPosition(0, 0));
        }

        public async Task PlayEventAsync(MacroEvent macroEvent, CancellationToken cancellationToken = default)
        {
            Attempts.Enqueue(macroEvent);
            if ((macroEvent.KeyboardActionType == KeyboardActionType.KeyDown && macroEvent.KeyCode == GatedKeyCode)
                || (GateSystemSend && macroEvent.EventType == MacroEventType.System))
            {
                SendEntered.TrySetResult();
                await _sendRelease.Task;
            }
            if (GateRelease && macroEvent.KeyboardActionType == KeyboardActionType.KeyUp)
            {
                ReleaseEntered.TrySetResult();
                await _releaseRelease.Task;
            }
            lock (_syncRoot)
            {
                if (macroEvent.KeyboardActionType == KeyboardActionType.KeyDown) { _held.Add(Identity(macroEvent)); }
                if (macroEvent.KeyboardActionType == KeyboardActionType.KeyUp) { _held.Remove(Identity(macroEvent)); }
            }
        }
    }
}
