using System.Collections.Concurrent;
using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Core.Tests.Support;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;

namespace Makredo.Core.Tests.Concurrency;

internal static class ConcurrencyTestData
{
    internal static MacroRecorderService CreateRecorder(
        IKeyboardHookSource keyboard,
        IMouseHookSource mouse,
        ApplicationStateService state) => new(
            keyboard,
            mouse,
            state,
            new TestHotkeyConfiguration(
                HotkeySettings.DefaultRecordToggleHotkey,
                HotkeySettings.DefaultPlaybackToggleHotkey,
                HotkeySettings.DefaultStopHotkey));

    internal static Task CompleteWithinDeadline(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System);

    internal static MacroSession CreatePlaybackSession(string name, int keyCode) => new()
    {
        Name = name,
        Events =
        {
            new MacroEvent { EventType = MacroEventType.Keyboard, KeyboardActionType = KeyboardActionType.KeyDown, KeyCode = keyCode },
            new MacroEvent { EventType = MacroEventType.Keyboard, KeyboardActionType = KeyboardActionType.KeyUp, KeyCode = keyCode }
        }
    };
}

internal sealed class AsyncCallGate
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal Task Entered => _entered.Task;

    internal async Task EnterAsync(CancellationToken cancellationToken)
    {
        _entered.TrySetResult();
        await _release.Task.WaitAsync(cancellationToken);
    }

    internal void Release() => _release.TrySetResult();
}

internal sealed class GatedKeyboardHook : IKeyboardHookSource
{
    private readonly object _syncRoot = new();
    private Action<KeyboardActivityInfo>? _activity;
    private int _running;
    private int _startCalls;
    private int _stopCalls;
    private int _subscriptionAdds;

    internal AsyncCallGate? StartGate { get; init; }
    internal AsyncCallGate? StopGate { get; init; }
    internal int StartCalls => Volatile.Read(ref _startCalls);
    internal int StopCalls => Volatile.Read(ref _stopCalls);
    internal int SubscriptionAdds => Volatile.Read(ref _subscriptionAdds);
    public bool IsRunning => Volatile.Read(ref _running) != 0;
    internal int SubscriberCount
    {
        get
        {
            lock (_syncRoot) { return _activity?.GetInvocationList().Length ?? 0; }
        }
    }

    public event Action<KeyboardActivityInfo>? KeyActivityReceived
    {
        add
        {
            lock (_syncRoot) { _activity += value; }
            Interlocked.Increment(ref _subscriptionAdds);
        }
        remove { lock (_syncRoot) { _activity -= value; } }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _startCalls);
        if (StartGate is not null) { await StartGate.EnterAsync(cancellationToken); }
        Volatile.Write(ref _running, 1);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _stopCalls);
        if (StopGate is not null) { await StopGate.EnterAsync(cancellationToken); }
        Volatile.Write(ref _running, 0);
    }

    internal void EmitPair(int keyCode)
    {
        Emit(keyCode, keyDown: true);
        Emit(keyCode, keyDown: false);
    }

    private void Emit(int keyCode, bool keyDown)
    {
        Action<KeyboardActivityInfo>? activity;
        lock (_syncRoot) { activity = _activity; }
        activity?.Invoke(new KeyboardActivityInfo(keyCode, 0, keyDown, false, HotkeyModifiers.None, false, HotkeyModifiers.None));
    }
}

internal sealed class CountingMouseHook : IMouseHookSource
{
    private readonly object _syncRoot = new();
    private Action<MouseActionType, int?, int?, int?>? _activity;
    private int _startCalls;
    private int _stopCalls;
    private int _subscriptionAdds;

    public bool IsRunning { get; private set; }
    internal int StartCalls => Volatile.Read(ref _startCalls);
    internal int StopCalls => Volatile.Read(ref _stopCalls);
    internal int SubscriptionAdds => Volatile.Read(ref _subscriptionAdds);
    internal int SubscriberCount
    {
        get
        {
            lock (_syncRoot) { return _activity?.GetInvocationList().Length ?? 0; }
        }
    }

    public event Action<MouseActionType, int?, int?, int?>? MouseActivityReceived
    {
        add
        {
            lock (_syncRoot) { _activity += value; }
            Interlocked.Increment(ref _subscriptionAdds);
        }
        remove { lock (_syncRoot) { _activity -= value; } }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _startCalls);
        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _stopCalls);
        IsRunning = false;
        return Task.CompletedTask;
    }
}

internal sealed class GatedPlaybackAdapter : IInputPlaybackAdapter, ICursorPositionProvider
{
    internal AsyncCallGate Gate { get; } = new();
    internal ConcurrentQueue<Guid> AttemptedEvents { get; } = new();

    public Task<CursorPosition> GetCursorPositionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CursorPosition(0, 0));
    }

    public async Task PlayEventAsync(MacroEvent macroEvent, CancellationToken cancellationToken = default)
    {
        AttemptedEvents.Enqueue(macroEvent.Id);
        await Gate.EnterAsync(cancellationToken);
    }
}
