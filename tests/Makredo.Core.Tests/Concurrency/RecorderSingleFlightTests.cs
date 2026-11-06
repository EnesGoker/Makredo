using Makredo.Application.Services;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Xunit;

namespace Makredo.Core.Tests.Concurrency;

public sealed class RecorderSingleFlightTests
{
    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task R2_RepeatedStartPreservesTheSessionAndAddsEachSubscriptionOnce()
    {
        var keyboard = new GatedKeyboardHook();
        var mouse = new CountingMouseHook();
        var state = new ApplicationStateService();
        MacroRecorderService recorder = ConcurrencyTestData.CreateRecorder(keyboard, mouse, state);
        await recorder.StartAsync("first");
        try
        {
            keyboard.EmitPair(0x41);
            await recorder.StartAsync("second");
            keyboard.EmitPair(0x42);
        }
        finally
        {
            await recorder.StopAsync();
        }

        MacroSession session = Assert.IsType<MacroSession>(recorder.CurrentSession);
        Assert.Equal("first", session.Name);
        Assert.Equal(new int?[] { 0x41, 0x41, 0x42, 0x42 }, session.Events.Select(item => item.KeyCode));
        Assert.Equal(
            new[] { KeyboardActionType.KeyDown, KeyboardActionType.KeyUp, KeyboardActionType.KeyDown, KeyboardActionType.KeyUp },
            session.Events.Select(item => item.KeyboardActionType));
        Assert.Equal(1, keyboard.StartCalls);
        Assert.Equal(1, mouse.StartCalls);
        Assert.Equal(1, keyboard.SubscriptionAdds);
        Assert.Equal(1, mouse.SubscriptionAdds);
        Assert.Equal(0, keyboard.SubscriberCount);
        Assert.Equal(0, mouse.SubscriberCount);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task R2_OverlappingStartsShareOneHookStartupAndPreserveTheFirstSession()
    {
        var startGate = new AsyncCallGate();
        var keyboard = new GatedKeyboardHook { StartGate = startGate };
        var mouse = new CountingMouseHook();
        var state = new ApplicationStateService();
        MacroRecorderService recorder = ConcurrencyTestData.CreateRecorder(keyboard, mouse, state);
        Task first = recorder.StartAsync("first");
        Task second = Task.CompletedTask;
        try
        {
            await ConcurrencyTestData.CompleteWithinDeadline(startGate.Entered);
            second = recorder.StartAsync("second");
        }
        finally
        {
            startGate.Release();
            await ConcurrencyTestData.CompleteWithinDeadline(Task.WhenAll(first, second));
        }

        keyboard.EmitPair(0x41);
        await recorder.StopAsync();

        MacroSession session = Assert.IsType<MacroSession>(recorder.CurrentSession);
        Assert.Equal("first", session.Name);
        Assert.Equal(new int?[] { 0x41, 0x41 }, session.Events.Select(item => item.KeyCode));
        Assert.Equal(1, keyboard.StartCalls);
        Assert.Equal(1, mouse.StartCalls);
        Assert.Equal(1, keyboard.SubscriptionAdds);
        Assert.Equal(1, mouse.SubscriptionAdds);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task R9_OverlappingStopsJoinOneCleanupAndPreserveEveryRecordedEvent()
    {
        var stopGate = new AsyncCallGate();
        var keyboard = new GatedKeyboardHook { StopGate = stopGate };
        var mouse = new CountingMouseHook();
        var state = new ApplicationStateService();
        MacroRecorderService recorder = ConcurrencyTestData.CreateRecorder(keyboard, mouse, state);
        int stoppedNotifications = 0;
        recorder.RecordingStopped += _ => Interlocked.Increment(ref stoppedNotifications);
        await recorder.StartAsync("retained");
        keyboard.EmitPair(0x41);
        MacroSession? originalSession = recorder.CurrentSession;
        Task first = recorder.StopAsync();
        Task second = Task.CompletedTask;
        bool secondWaitedForCleanup = false;
        try
        {
            await ConcurrencyTestData.CompleteWithinDeadline(stopGate.Entered);
            second = recorder.StopAsync();
            secondWaitedForCleanup = !second.IsCompleted;
        }
        finally
        {
            stopGate.Release();
            await ConcurrencyTestData.CompleteWithinDeadline(Task.WhenAll(first, second));
        }

        Assert.True(secondWaitedForCleanup, "An overlapping stop must not report completion before the shared cleanup finishes.");
        Assert.Same(originalSession, recorder.CurrentSession);
        MacroSession session = Assert.IsType<MacroSession>(recorder.CurrentSession);
        Assert.Equal(new int?[] { 0x41, 0x41 }, session.Events.Select(item => item.KeyCode));
        Assert.Equal(new[] { KeyboardActionType.KeyDown, KeyboardActionType.KeyUp }, session.Events.Select(item => item.KeyboardActionType));
        Assert.Equal(1, keyboard.StopCalls);
        Assert.Equal(1, mouse.StopCalls);
        Assert.Equal(1, stoppedNotifications);
        Assert.Equal(0, keyboard.SubscriberCount);
        Assert.Equal(0, mouse.SubscriberCount);
        Assert.Equal(AppState.Idle, state.CurrentState);

        await recorder.StopAsync();

        Assert.Equal(1, keyboard.StopCalls);
        Assert.Equal(1, mouse.StopCalls);
        Assert.Equal(1, stoppedNotifications);
        Assert.Equal(new int?[] { 0x41, 0x41 }, session.Events.Select(item => item.KeyCode));
    }
}
