using System.Collections.Concurrent;
using Makredo.Application.Services;
using Makredo.Domain.Enums;
using Xunit;

namespace Makredo.Core.Tests.Concurrency;

public sealed class StateMachineRegressionTests
{
    [Theory]
    [Trait("Category", "Concurrency")]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void R1_SameStateTransitionRequiresExplicitPermission(int permission, bool expected)
    {
        var state = new ApplicationStateService();
        state.SetState(AppState.Recording);
        int notifications = 0;
        state.StateChanged += _ => notifications++;
        AppState[] allowed = permission switch
        {
            0 => [],
            1 => [AppState.Idle],
            2 => [AppState.Recording],
            3 => [AppState.Idle, AppState.Recording],
            _ => throw new ArgumentOutOfRangeException(nameof(permission))
        };

        bool transitioned = state.TryTransitionTo(AppState.Recording, allowed);

        Assert.Equal(expected, transitioned);
        Assert.Equal(AppState.Recording, state.CurrentState);
        Assert.Equal(0, notifications);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public void StateChanged_ReentrantTransitionReachesEverySubscriberInCommitOrder()
    {
        var state = new ApplicationStateService();
        List<AppState> firstObserver = [];
        List<AppState> secondObserver = [];
        state.StateChanged += value =>
        {
            firstObserver.Add(value);
            if (value == AppState.Recording)
            {
                state.SetState(AppState.Stopping);
            }
        };
        state.StateChanged += secondObserver.Add;

        state.SetState(AppState.Recording);

        AppState[] expected = [AppState.Recording, AppState.Stopping];
        Assert.Equal(expected, firstObserver);
        Assert.Equal(expected, secondObserver);
        Assert.Equal(AppState.Stopping, state.CurrentState);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task StateChanged_ConcurrentTransitionsReachSubscribersInCommitOrder()
    {
        var state = new ApplicationStateService();
        using var publicationBarrier = new Barrier(2);
        var firstPublicationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var published = new ConcurrentQueue<AppState>();
        bool subscriberReleased = false;
        bool callerReleased = false;
        state.StateChanged += value =>
        {
            if (value == AppState.Recording)
            {
                firstPublicationEntered.TrySetResult();
                subscriberReleased = publicationBarrier.SignalAndWait(TimeSpan.FromSeconds(10));
            }
        };
        state.StateChanged += published.Enqueue;
        Task first = Task.Run(() => state.SetState(AppState.Recording));
        Task second = Task.CompletedTask;
        try
        {
            await ConcurrencyTestData.CompleteWithinDeadline(firstPublicationEntered.Task);
            second = Task.Run(() => state.SetState(AppState.Stopping));
            await ConcurrencyTestData.CompleteWithinDeadline(second);
        }
        finally
        {
            callerReleased = publicationBarrier.SignalAndWait(TimeSpan.FromSeconds(10));
            await ConcurrencyTestData.CompleteWithinDeadline(Task.WhenAll(first, second));
        }

        Assert.True(subscriberReleased);
        Assert.True(callerReleased);
        Assert.Equal(new[] { AppState.Recording, AppState.Stopping }, published);
        Assert.Equal(AppState.Stopping, state.CurrentState);
    }
}
