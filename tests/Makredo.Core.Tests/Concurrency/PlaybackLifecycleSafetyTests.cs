using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Core.Tests.Support;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Xunit;

namespace Makredo.Core.Tests.Concurrency;

public sealed class PlaybackLifecycleSafetyTests
{
    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task StopFromPlayingNotificationCancelsTheReservedOwnerBeforeInput()
    {
        var adapter = new TestInputPlaybackAdapter();
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state);
        Task? stop = null;
        void RequestStop(AppState value)
        {
            if (value == AppState.Playing) { stop = playback.StopAsync(); }
        }
        state.StateChanged += RequestStop;

        await playback.PlayAsync(ConcurrencyTestData.CreatePlaybackSession("stopped during publication", 0x41), new PlaybackSettings());
        await Assert.IsAssignableFrom<Task>(stop);

        Assert.Empty(adapter.AttemptedEventIds);
        Assert.Equal(AppState.Idle, state.CurrentState);
        state.StateChanged -= RequestStop;
        await playback.PlayAsync(ConcurrencyTestData.CreatePlaybackSession("next", 0x42), new PlaybackSettings());
        Assert.Equal(2, adapter.AttemptedEventIds.Count);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task ReentrantPlayDuringFinalIdleNotificationDoesNotAcquireTheFinishingOwner()
    {
        var adapter = new TestInputPlaybackAdapter();
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state);
        MacroSession first = ConcurrencyTestData.CreatePlaybackSession("first", 0x41);
        MacroSession next = ConcurrencyTestData.CreatePlaybackSession("next", 0x42);
        Task reentrant = Task.CompletedTask;
        bool attempted = false;
        state.StateChanged += value =>
        {
            if (value == AppState.Idle && !attempted)
            {
                attempted = true;
                reentrant = playback.PlayAsync(next, new PlaybackSettings());
            }
        };

        await playback.PlayAsync(first, new PlaybackSettings());
        await reentrant;

        Assert.True(attempted);
        Assert.Equal(first.Events.Select(item => item.Id), adapter.AttemptedEventIds);
        await playback.PlayAsync(next, new PlaybackSettings());
        Assert.Equal(first.Events.Concat(next.Events).Select(item => item.Id), adapter.AttemptedEventIds);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task OverlappingStopRequestsWaitForOneStatePublicationAndCancellation()
    {
        var adapter = new AcceptedGatedPlaybackAdapter();
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state);
        using var barrier = new Barrier(2);
        var stoppingEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool publisherReleased = false;
        state.StateChanged += value =>
        {
            if (value == AppState.Stopping)
            {
                stoppingEntered.TrySetResult();
                publisherReleased = barrier.SignalAndWait(TimeSpan.FromSeconds(10));
            }
        };
        MacroSession session = ConcurrencyTestData.CreatePlaybackSession("active", 0x41);
        Task play = playback.PlayAsync(session, new PlaybackSettings());
        await ConcurrencyTestData.CompleteWithinDeadline(adapter.Gate.Entered);
        Task first = Task.Run(() => playback.StopAsync());
        Task second = Task.CompletedTask;
        bool secondWaited = false;
        bool callerReleased;
        try
        {
            await ConcurrencyTestData.CompleteWithinDeadline(stoppingEntered.Task);
            second = playback.StopAsync();
            secondWaited = !second.IsCompleted;
        }
        finally
        {
            callerReleased = barrier.SignalAndWait(TimeSpan.FromSeconds(10));
            adapter.Gate.Release();
            await ConcurrencyTestData.CompleteWithinDeadline(Task.WhenAll(first, second, play));
        }

        Assert.True(callerReleased);
        Assert.True(publisherReleased);
        Assert.True(secondWaited);
        Assert.True(adapter.PlaybackToken.IsCancellationRequested);
        Assert.Collection(adapter.PlayedEvents,
            down =>
            {
                Assert.Equal(session.Events[0].Id, down.Id);
                Assert.Equal(MacroEventType.Keyboard, down.EventType);
                Assert.Equal(KeyboardActionType.KeyDown, down.KeyboardActionType);
                Assert.Equal(0x41, down.KeyCode);
                Assert.Null(down.ScanCode);
                Assert.False(down.IsExtendedKey);
            },
            release =>
            {
                Assert.Equal(MacroEventType.Keyboard, release.EventType);
                Assert.Equal(KeyboardActionType.KeyUp, release.KeyboardActionType);
                Assert.Equal(0x41, release.KeyCode);
                Assert.Null(release.ScanCode);
                Assert.False(release.IsExtendedKey);
                Assert.Equal(MouseActionType.None, release.MouseActionType);
            });
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task ThrowingPlayingSubscriberReleasesOwnershipAndAllowsTheNextPlay()
    {
        var adapter = new TestInputPlaybackAdapter();
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state);
        var failure = new InvalidOperationException("state publication failure");
        void FailPlaying(AppState value)
        {
            if (value == AppState.Playing) { throw failure; }
        }
        state.StateChanged += FailPlaying;

        InvalidOperationException observed = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            playback.PlayAsync(ConcurrencyTestData.CreatePlaybackSession("failed", 0x41), new PlaybackSettings()));

        Assert.Same(failure, observed);
        Assert.Equal(AppState.Idle, state.CurrentState);
        Assert.Empty(adapter.AttemptedEventIds);
        state.StateChanged -= FailPlaying;
        await playback.PlayAsync(ConcurrencyTestData.CreatePlaybackSession("next", 0x42), new PlaybackSettings());
        Assert.Equal(2, adapter.AttemptedEventIds.Count);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task ThrowingStoppingSubscriberStillCancelsAndReleasesTheOwner()
    {
        var adapter = new GatedPlaybackAdapter();
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state);
        using var cancellation = new CancellationTokenSource();
        var failure = new InvalidOperationException("Stopping observer failed.");
        void FailStopping(AppState value)
        {
            if (value == AppState.Stopping) { throw failure; }
        }
        state.StateChanged += FailStopping;
        Task play = playback.PlayAsync(ConcurrencyTestData.CreatePlaybackSession("active", 0x41), new PlaybackSettings(), cancellation.Token);
        try
        {
            await ConcurrencyTestData.CompleteWithinDeadline(adapter.Gate.Entered);
            InvalidOperationException observed = await Assert.ThrowsAsync<InvalidOperationException>(() => playback.StopAsync());
            Assert.Same(failure, observed);
            await ConcurrencyTestData.CompleteWithinDeadline(play);
            Assert.Equal(AppState.Idle, state.CurrentState);
        }
        finally
        {
            cancellation.Cancel();
            adapter.Gate.Release();
            await ConcurrencyTestData.CompleteWithinDeadline(play);
        }

        state.StateChanged -= FailStopping;
        await playback.PlayAsync(ConcurrencyTestData.CreatePlaybackSession("next", 0x42), new PlaybackSettings());
        Assert.Equal(3, adapter.AttemptedEvents.Count);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    private sealed class AcceptedGatedPlaybackAdapter : IInputPlaybackAdapter, ICursorPositionProvider
    {
        internal AsyncCallGate Gate { get; } = new();
        internal List<MacroEvent> PlayedEvents { get; } = [];
        internal CancellationToken PlaybackToken { get; private set; }

        public Task<CursorPosition> GetCursorPositionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CursorPosition(0, 0));
        }

        public async Task PlayEventAsync(MacroEvent macroEvent, CancellationToken cancellationToken = default)
        {
            if (macroEvent.KeyboardActionType == KeyboardActionType.KeyDown) { PlaybackToken = cancellationToken; }
            // A native send already accepted by the OS can finish after cancellation.
            await Gate.EnterAsync(CancellationToken.None);
            PlayedEvents.Add(macroEvent);
        }
    }
}
