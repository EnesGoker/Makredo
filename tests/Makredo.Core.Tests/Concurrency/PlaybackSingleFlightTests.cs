using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Xunit;

namespace Makredo.Core.Tests.Concurrency;

public sealed class PlaybackSingleFlightTests
{
    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task R4_OverlappingPlayDoesNotResetTheActiveOwnerOrStartAnotherSession()
    {
        var adapter = new GatedPlaybackAdapter();
        var state = new ApplicationStateService();
        using var playback = new MacroPlaybackService(adapter, adapter, state);
        using var cancellation = new CancellationTokenSource();
        MacroSession original = ConcurrencyTestData.CreatePlaybackSession("first", 0x41);
        MacroSession other = ConcurrencyTestData.CreatePlaybackSession("second", 0x42);
        int starts = 0;
        int stops = 0;
        playback.PlaybackStarted += () => Interlocked.Increment(ref starts);
        playback.PlaybackStopped += () => Interlocked.Increment(ref stops);
        Task first = playback.PlayAsync(original, new PlaybackSettings(), cancellation.Token);
        Task second = Task.CompletedTask;
        AppState observedState = AppState.Error;
        bool observedPlaying = false;
        try
        {
            await ConcurrencyTestData.CompleteWithinDeadline(adapter.Gate.Entered);
            second = playback.PlayAsync(other, new PlaybackSettings(), cancellation.Token);
            observedState = state.CurrentState;
            observedPlaying = playback.IsPlaying;
        }
        finally
        {
            cancellation.Cancel();
            adapter.Gate.Release();
            await ConcurrencyTestData.CompleteWithinDeadline(Task.WhenAll(first, second));
        }

        Assert.Equal(AppState.Playing, observedState);
        Assert.True(observedPlaying);
        Assert.Equal(new[] { original.Events[0].Id }, adapter.AttemptedEvents);
        Assert.Equal(1, starts);
        Assert.Equal(1, stops);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }
}
