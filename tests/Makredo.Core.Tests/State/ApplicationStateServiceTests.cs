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

namespace Makredo.Core.Tests.State;

public sealed class ApplicationStateServiceTests
{
    [Fact(DisplayName = "ApplicationStateService allows valid transitions")]
    public Task ApplicationStateService_AllowsValidTransitions()
    {
        var stateService = new ApplicationStateService();
        List<AppState> observedStates = [];

        stateService.StateChanged += observedStates.Add;

        Assert.Equal(AppState.Idle, stateService.CurrentState);
        Assert.True(stateService.TryTransitionTo(AppState.Recording, AppState.Idle), "Idle -> Recording should succeed.");
        Assert.True(stateService.TryTransitionTo(AppState.Stopping, AppState.Recording), "Recording -> Stopping should succeed.");
        Assert.True(stateService.TryTransitionTo(AppState.Idle, AppState.Stopping), "Stopping -> Idle should succeed.");
        Assert.Equal(
            new[] { AppState.Recording, AppState.Stopping, AppState.Idle },
            observedStates);

        return Task.CompletedTask;
    }

    [Fact(DisplayName = "ApplicationStateService rejects invalid transitions")]
    public Task ApplicationStateService_RejectsInvalidTransitions()
    {
        var stateService = new ApplicationStateService();

        Assert.Throws<InvalidOperationException>(
            () => stateService.SetState(AppState.Paused));

        Assert.Equal(AppState.Idle, stateService.CurrentState);
        Assert.False(
            stateService.TryTransitionTo(AppState.Playing, AppState.Recording),
            "Allowed current state guard should reject transitions when current state does not match.");

        return Task.CompletedTask;
    }
}
