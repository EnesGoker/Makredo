using Makredo.Application.Services;
using Makredo.Domain.Enums;
using Xunit;

namespace Makredo.Core.Tests.Concurrency;

public sealed class StateMachineNotificationSafetyTests
{
    [Fact]
    [Trait("Category", "Concurrency")]
    public void StateChanged_SubscriberFailureDoesNotStrandQueuedNotificationsOrFuturePublication()
    {
        var state = new ApplicationStateService();
        var failure = new InvalidOperationException("Recording subscriber failed.");
        List<AppState> observed = [];
        state.StateChanged += value =>
        {
            if (value == AppState.Recording)
            {
                state.SetState(AppState.Stopping);
                throw failure;
            }
        };
        state.StateChanged += observed.Add;

        InvalidOperationException actual = Assert.Throws<InvalidOperationException>(() => state.SetState(AppState.Recording));

        Assert.Same(failure, actual);
        Assert.Equal(new[] { AppState.Stopping }, observed);
        Assert.Equal(AppState.Stopping, state.CurrentState);

        state.SetState(AppState.Idle);

        Assert.Equal(new[] { AppState.Stopping, AppState.Idle }, observed);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public void StateChanged_FirstFailureIsRethrownAfterLaterQueuedNotificationsAreDrained()
    {
        var state = new ApplicationStateService();
        var firstFailure = new InvalidOperationException("Recording subscriber failed.");
        var laterFailure = new ArgumentException("Stopping subscriber failed.");
        List<AppState> observed = [];
        state.StateChanged += value =>
        {
            if (value == AppState.Recording)
            {
                state.SetState(AppState.Stopping);
                state.SetState(AppState.Idle);
                throw firstFailure;
            }

            if (value == AppState.Stopping)
            {
                throw laterFailure;
            }
        };
        state.StateChanged += observed.Add;

        InvalidOperationException actual = Assert.Throws<InvalidOperationException>(() => state.SetState(AppState.Recording));

        Assert.Same(firstFailure, actual);
        Assert.Equal(new[] { AppState.Idle }, observed);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public void StateChanged_QueuedTransitionsKeepTheSubscribersCapturedAtCommit()
    {
        var state = new ApplicationStateService();
        List<AppState> originalObserver = [];
        List<AppState> laterObserver = [];
        Action<AppState> original = originalObserver.Add;
        Action<AppState> later = laterObserver.Add;
        state.StateChanged += value =>
        {
            if (value == AppState.Recording)
            {
                state.SetState(AppState.Stopping);
                state.StateChanged -= original;
                state.StateChanged += later;
            }
        };
        state.StateChanged += original;

        state.SetState(AppState.Recording);

        Assert.Equal(new[] { AppState.Recording, AppState.Stopping }, originalObserver);
        Assert.Empty(laterObserver);

        state.SetState(AppState.Idle);

        Assert.Equal(new[] { AppState.Recording, AppState.Stopping }, originalObserver);
        Assert.Equal(new[] { AppState.Idle }, laterObserver);
    }
}
