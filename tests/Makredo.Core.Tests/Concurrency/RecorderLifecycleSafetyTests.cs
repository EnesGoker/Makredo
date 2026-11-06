using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Xunit;

namespace Makredo.Core.Tests.Concurrency;

public sealed class RecorderLifecycleSafetyTests
{
    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task StopFromRecordingStatePublicationWaitsForSessionPreparation()
    {
        var keyboard = new GatedKeyboardHook();
        var mouse = new CountingMouseHook();
        var state = new ApplicationStateService();
        MacroRecorderService recorder = ConcurrencyTestData.CreateRecorder(keyboard, mouse, state);
        var stopRequested = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> notifications = [];
        bool stopWaitedForStartup = false;
        state.StateChanged += value =>
        {
            if (value != AppState.Recording) { return; }
            Task stop = recorder.StopAsync();
            stopWaitedForStartup = !stop.IsCompleted;
            stopRequested.TrySetResult(stop);
        };
        recorder.RecordingStarted += () => notifications.Add("started");
        recorder.RecordingStopped += _ => notifications.Add("stopped");

        await recorder.StartAsync("first");
        Task stopTask = await stopRequested.Task.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System);
        await ConcurrencyTestData.CompleteWithinDeadline(stopTask);

        Assert.True(stopWaitedForStartup);
        string[] expectedNotifications = ["started", "stopped"];
        Assert.Equal(expectedNotifications, notifications);
        Assert.Equal("first", Assert.IsType<MacroSession>(recorder.CurrentSession).Name);
        Assert.Equal(1, keyboard.StartCalls);
        Assert.Equal(1, mouse.StartCalls);
        Assert.Equal(1, keyboard.StopCalls);
        Assert.Equal(1, mouse.StopCalls);
        Assert.Equal(0, keyboard.SubscriberCount);
        Assert.Equal(0, mouse.SubscriberCount);
        Assert.False(keyboard.IsRunning);
        Assert.False(mouse.IsRunning);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task StopDuringStartupWaitsForHookStartupBeforeCleanup()
    {
        var startGate = new AsyncCallGate();
        var keyboard = new GatedKeyboardHook { StartGate = startGate };
        var mouse = new CountingMouseHook();
        var state = new ApplicationStateService();
        MacroRecorderService recorder = ConcurrencyTestData.CreateRecorder(keyboard, mouse, state);
        Task start = recorder.StartAsync("first");
        Task stop = Task.CompletedTask;
        try
        {
            await ConcurrencyTestData.CompleteWithinDeadline(startGate.Entered);
            stop = recorder.StopAsync();
            Assert.False(stop.IsCompleted);
            Assert.Equal(0, keyboard.StopCalls);
            Assert.Equal(0, mouse.StopCalls);
        }
        finally
        {
            startGate.Release();
            await ConcurrencyTestData.CompleteWithinDeadline(Task.WhenAll(start, stop));
        }

        Assert.Equal(1, keyboard.StartCalls);
        Assert.Equal(1, mouse.StartCalls);
        Assert.Equal(1, keyboard.StopCalls);
        Assert.Equal(1, mouse.StopCalls);
        Assert.False(keyboard.IsRunning);
        Assert.False(mouse.IsRunning);
        Assert.Equal(0, keyboard.SubscriberCount);
        Assert.Equal(0, mouse.SubscriberCount);
        Assert.Equal("first", Assert.IsType<MacroSession>(recorder.CurrentSession).Name);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task OverlappingStartupFailuresAreSharedAndCanBeRetried()
    {
        var keyboard = new FailingStartupKeyboardHook();
        var mouse = new CountingMouseHook();
        var state = new ApplicationStateService();
        MacroRecorderService recorder = ConcurrencyTestData.CreateRecorder(keyboard, mouse, state);
        Task first = recorder.StartAsync("failed");
        Task second;
        try
        {
            await ConcurrencyTestData.CompleteWithinDeadline(keyboard.Gate.Entered);
            second = recorder.StartAsync("duplicate");
            Assert.Same(first, second);
        }
        finally
        {
            keyboard.Gate.Release();
        }

        var firstFailure = await Assert.ThrowsAsync<InvalidOperationException>(() => ConcurrencyTestData.CompleteWithinDeadline(first));
        var secondFailure = await Assert.ThrowsAsync<InvalidOperationException>(() => ConcurrencyTestData.CompleteWithinDeadline(second));
        Assert.Same(keyboard.Failure, firstFailure);
        Assert.Same(firstFailure, secondFailure);
        Assert.Null(recorder.CurrentSession);
        Assert.Equal(0, keyboard.SubscriberCount);
        Assert.Equal(0, mouse.SubscriberCount);
        Assert.Equal(1, keyboard.StartCalls);
        Assert.Equal(0, mouse.StartCalls);
        Assert.Equal(AppState.Idle, state.CurrentState);

        await recorder.StartAsync("retry");
        await recorder.StopAsync();

        Assert.Equal(2, keyboard.StartCalls);
        Assert.Equal(1, mouse.StartCalls);
        Assert.Equal("retry", Assert.IsType<MacroSession>(recorder.CurrentSession).Name);
        Assert.Equal(0, keyboard.SubscriberCount);
        Assert.Equal(0, mouse.SubscriberCount);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task StartDuringStopDoesNotQueueANewRecording()
    {
        var stopGate = new AsyncCallGate();
        var keyboard = new GatedKeyboardHook { StopGate = stopGate };
        var mouse = new CountingMouseHook();
        var state = new ApplicationStateService();
        MacroRecorderService recorder = ConcurrencyTestData.CreateRecorder(keyboard, mouse, state);
        await recorder.StartAsync("retained");
        keyboard.EmitPair(0x41);
        MacroSession? originalSession = recorder.CurrentSession;
        Task stop = recorder.StopAsync();
        try
        {
            await ConcurrencyTestData.CompleteWithinDeadline(stopGate.Entered);
            await ConcurrencyTestData.CompleteWithinDeadline(recorder.StartAsync("must-not-start"));
            Assert.Equal(1, keyboard.StartCalls);
            Assert.Equal(1, mouse.StartCalls);
        }
        finally
        {
            stopGate.Release();
            await ConcurrencyTestData.CompleteWithinDeadline(stop);
        }

        Assert.Same(originalSession, recorder.CurrentSession);
        MacroSession session = Assert.IsType<MacroSession>(recorder.CurrentSession);
        Assert.Equal("retained", session.Name);
        Assert.Equal(new int?[] { 0x41, 0x41 }, session.Events.Select(item => item.KeyCode));
        Assert.False(keyboard.IsRunning);
        Assert.False(mouse.IsRunning);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task StopDuringFailedStartupCompletesAfterStartupRollback()
    {
        var keyboard = new FailingStartupKeyboardHook();
        var mouse = new CountingMouseHook();
        var state = new ApplicationStateService();
        MacroRecorderService recorder = ConcurrencyTestData.CreateRecorder(keyboard, mouse, state);
        Task start = recorder.StartAsync("failed");
        Task stop = Task.CompletedTask;
        try
        {
            await ConcurrencyTestData.CompleteWithinDeadline(keyboard.Gate.Entered);
            stop = recorder.StopAsync();
            Assert.False(stop.IsCompleted);
        }
        finally
        {
            keyboard.Gate.Release();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => ConcurrencyTestData.CompleteWithinDeadline(start));
        await ConcurrencyTestData.CompleteWithinDeadline(stop);

        Assert.Null(recorder.CurrentSession);
        Assert.Equal(0, keyboard.SubscriberCount);
        Assert.Equal(0, mouse.SubscriberCount);
        Assert.Equal(0, keyboard.StopCalls);
        Assert.Equal(0, mouse.StartCalls);
        Assert.Equal(0, mouse.StopCalls);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task StartupStatePublicationFailurePreservesThePreviousSessionAndAllowsRetry()
    {
        var keyboard = new GatedKeyboardHook();
        var mouse = new CountingMouseHook();
        var state = new ApplicationStateService();
        MacroRecorderService recorder = ConcurrencyTestData.CreateRecorder(keyboard, mouse, state);
        await recorder.StartAsync("retained");
        keyboard.EmitPair(0x41);
        await recorder.StopAsync();
        MacroSession? retained = recorder.CurrentSession;
        var failure = new InvalidOperationException("Recording observer failed.");
        var rollbackFailure = new InvalidOperationException("Idle observer failed during rollback.");
        void FailRecording(AppState value)
        {
            if (value == AppState.Recording) { throw failure; }
            if (value == AppState.Idle) { throw rollbackFailure; }
        }

        state.StateChanged += FailRecording;
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => recorder.StartAsync("failed"));
        state.StateChanged -= FailRecording;

        Assert.Same(failure, observed);
        Assert.Same(retained, recorder.CurrentSession);
        Assert.Equal(new int?[] { 0x41, 0x41 }, Assert.IsType<MacroSession>(retained).Events.Select(item => item.KeyCode));
        Assert.Equal(1, keyboard.StartCalls);
        Assert.Equal(1, mouse.StartCalls);
        Assert.Equal(0, keyboard.SubscriberCount);
        Assert.Equal(0, mouse.SubscriberCount);
        Assert.Equal(AppState.Idle, state.CurrentState);

        await recorder.StartAsync("retry");
        keyboard.EmitPair(0x42);
        await recorder.StopAsync();

        MacroSession session = Assert.IsType<MacroSession>(recorder.CurrentSession);
        Assert.Equal("retry", session.Name);
        Assert.Equal(new int?[] { 0x42, 0x42 }, session.Events.Select(item => item.KeyCode));
        Assert.Equal(2, keyboard.StartCalls);
        Assert.Equal(2, mouse.StartCalls);
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task StoppingStatePublicationFailureStillCompletesSharedCleanup()
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
        var failure = new InvalidOperationException("Stopping observer failed.");
        state.StateChanged += value =>
        {
            if (value == AppState.Stopping) { throw failure; }
        };

        Task first = recorder.StopAsync();
        Task second;
        try
        {
            await ConcurrencyTestData.CompleteWithinDeadline(stopGate.Entered);
            second = recorder.StopAsync();
            Assert.Same(first, second);
        }
        finally
        {
            stopGate.Release();
        }

        var firstFailure = await Assert.ThrowsAsync<AggregateException>(() => ConcurrencyTestData.CompleteWithinDeadline(first));
        var secondFailure = await Assert.ThrowsAsync<AggregateException>(() => ConcurrencyTestData.CompleteWithinDeadline(second));
        Assert.Same(firstFailure, secondFailure);
        Assert.Contains(failure, firstFailure.InnerExceptions);
        Assert.Equal(new int?[] { 0x41, 0x41 }, Assert.IsType<MacroSession>(recorder.CurrentSession).Events.Select(item => item.KeyCode));
        Assert.Equal(1, keyboard.StopCalls);
        Assert.Equal(1, mouse.StopCalls);
        Assert.Equal(1, stoppedNotifications);
        Assert.Equal(0, keyboard.SubscriberCount);
        Assert.Equal(0, mouse.SubscriberCount);
        Assert.Equal(AppState.Idle, state.CurrentState);
    }

    private sealed class FailingStartupKeyboardHook : IKeyboardHookSource
    {
        private Action<KeyboardActivityInfo>? _activity;
        private int _failNextStart = 1;
        private int _startCalls;
        private int _stopCalls;

        internal AsyncCallGate Gate { get; } = new();
        internal InvalidOperationException Failure { get; } = new("Synthetic hook startup failed.");
        internal int StartCalls => Volatile.Read(ref _startCalls);
        internal int StopCalls => Volatile.Read(ref _stopCalls);
        internal int SubscriberCount => _activity?.GetInvocationList().Length ?? 0;
        public bool IsRunning { get; private set; }

        public event Action<KeyboardActivityInfo>? KeyActivityReceived
        {
            add => _activity += value;
            remove => _activity -= value;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _startCalls);
            await Gate.EnterAsync(cancellationToken);
            if (Interlocked.Exchange(ref _failNextStart, 0) != 0) { throw Failure; }
            IsRunning = true;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _stopCalls);
            IsRunning = false;
            return Task.CompletedTask;
        }
    }
}
