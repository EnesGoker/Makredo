using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Core.Tests.Support;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Makredo.Core.Tests.Hooks;

public sealed class HookRecorderTimingTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordedTimingUsesHookTimestampsInsteadOfDelayedConsumptionTime()
    {
        var keyboard = new FakeKeyboardSource();
        var mouse = new FakeTimestampedMouseSource();
        var time = new FakeTimeProvider(StartUtc);
        MacroRecorderService recorder = CreateRecorder(keyboard, mouse, time);
        await recorder.StartAsync("Hook timing");

        time.Advance(TimeSpan.FromSeconds(5));
        keyboard.Emit(Key(keyDown: true, StartUtc.AddMilliseconds(10).UtcDateTime));
        time.Advance(TimeSpan.FromSeconds(5));
        mouse.Emit(new MouseActivityInfo(MouseActionType.Move, 20, 30, null, StartUtc.AddMilliseconds(25).UtcDateTime));
        time.Advance(TimeSpan.FromSeconds(2));
        keyboard.Emit(Key(keyDown: false, StartUtc.AddMilliseconds(35).UtcDateTime));
        await recorder.StopAsync();

        MacroSession session = Assert.IsType<MacroSession>(recorder.CurrentSession);
        Assert.Equal<int>([10, 15, 10], session.Events.Select(item => item.DelayMs));
        Assert.Equal<DateTime>(
            [StartUtc.AddMilliseconds(10).UtcDateTime, StartUtc.AddMilliseconds(25).UtcDateTime, StartUtc.AddMilliseconds(35).UtcDateTime],
            session.Events.Select(item => item.TimestampUtc));
        Assert.Equal<MacroEventType>([MacroEventType.Keyboard, MacroEventType.Mouse, MacroEventType.Keyboard],
            session.Events.Select(item => item.EventType));
    }

    [Fact]
    public async Task StopDrainsBothSourcesBeforeRemovingSubscriptionsAndSealingTheSession()
    {
        var keyboard = new FakeKeyboardSource();
        var mouse = new FakeTimestampedMouseSource();
        var time = new FakeTimeProvider(StartUtc);
        var state = new ApplicationStateService();
        MacroRecorderService recorder = CreateRecorder(keyboard, mouse, time, state);
        keyboard.StopTail = () =>
        {
            Assert.Equal(AppState.Stopping, state.CurrentState);
            keyboard.Emit(Key(keyDown: false, StartUtc.AddMilliseconds(20).UtcDateTime));
        };
        mouse.StopTail = () =>
        {
            Assert.Equal(AppState.Stopping, state.CurrentState);
            mouse.Emit(new MouseActivityInfo(MouseActionType.Move, 20, 30, null, StartUtc.AddMilliseconds(30).UtcDateTime));
        };
        await recorder.StartAsync("Queued tail");
        keyboard.Emit(Key(keyDown: true, StartUtc.AddMilliseconds(10).UtcDateTime));
        time.Advance(TimeSpan.FromSeconds(2));

        await recorder.StopAsync();

        MacroSession session = Assert.IsType<MacroSession>(recorder.CurrentSession);
        Assert.Equal<int>([10, 10, 10], session.Events.Select(item => item.DelayMs));
        Assert.Equal<MacroEventType>([MacroEventType.Keyboard, MacroEventType.Keyboard, MacroEventType.Mouse],
            session.Events.Select(item => item.EventType));
        Assert.Equal(KeyboardActionType.KeyUp, session.Events[1].KeyboardActionType);
        Assert.Equal(MouseActionType.Move, session.Events[2].MouseActionType);
        Assert.Equal(0, keyboard.SubscriberCount);
        Assert.Equal(0, mouse.TimestampedSubscriberCount);
        Assert.Equal(0, mouse.LegacySubscriberCount);
        Assert.False(keyboard.IsRunning);
        Assert.False(mouse.IsRunning);
        Assert.Equal(AppState.Idle, state.CurrentState);

        keyboard.Emit(Key(keyDown: true, StartUtc.AddMilliseconds(40).UtcDateTime));
        mouse.Emit(new MouseActivityInfo(MouseActionType.Move, 40, 50, null, StartUtc.AddMilliseconds(50).UtcDateTime));
        Assert.Equal(3, session.Events.Count);
    }

    [Fact]
    public async Task LegacySourcesWithoutHookTimestampsUseTheInjectedTimeProvider()
    {
        var keyboard = new FakeKeyboardSource();
        var mouse = new FakeLegacyMouseSource();
        var time = new FakeTimeProvider(StartUtc);
        MacroRecorderService recorder = CreateRecorder(keyboard, mouse, time);
        await recorder.StartAsync("Legacy timing");
        time.Advance(TimeSpan.FromMilliseconds(10));
        keyboard.Emit(Key(keyDown: true));
        time.Advance(TimeSpan.FromMilliseconds(20));
        mouse.Emit(MouseActionType.Move, 20, 30, null);

        await recorder.StopAsync();

        MacroSession session = Assert.IsType<MacroSession>(recorder.CurrentSession);
        Assert.Equal<int>([10, 20], session.Events.Select(item => item.DelayMs));
        Assert.Equal<DateTime>([StartUtc.AddMilliseconds(10).UtcDateTime, StartUtc.AddMilliseconds(30).UtcDateTime],
            session.Events.Select(item => item.TimestampUtc));
    }

    private static MacroRecorderService CreateRecorder(
        IKeyboardHookSource keyboard,
        IMouseHookSource mouse,
        TimeProvider time,
        ApplicationStateService? state = null) => new(
            keyboard,
            mouse,
            state ?? new ApplicationStateService(),
            new TestHotkeyConfiguration(HotkeyBinding.None(0x77), HotkeyBinding.None(0x78), HotkeyBinding.None(0x79)),
            timeProvider: time);

    private static KeyboardActivityInfo Key(bool keyDown, DateTime? timestamp = null) => new(
        0x41, 0x1E, keyDown, false, HotkeyModifiers.None, false, HotkeyModifiers.None, timestamp);

    private sealed class FakeKeyboardSource : IKeyboardHookSource
    {
        public bool IsRunning { get; private set; }
        public event Action<KeyboardActivityInfo>? KeyActivityReceived;
        internal int SubscriberCount => KeyActivityReceived?.GetInvocationList().Length ?? 0;
        internal Action? StopTail { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopTail?.Invoke();
            IsRunning = false;
            return Task.CompletedTask;
        }

        internal void Emit(KeyboardActivityInfo activity) => KeyActivityReceived?.Invoke(activity);
    }

    private sealed class FakeTimestampedMouseSource : ITimestampedMouseHookSource
    {
        public bool IsRunning { get; private set; }
        public event Action<MouseActionType, int?, int?, int?>? MouseActivityReceived;
        public event Action<MouseActivityInfo>? TimestampedMouseActivityReceived;
        internal int TimestampedSubscriberCount => TimestampedMouseActivityReceived?.GetInvocationList().Length ?? 0;
        internal int LegacySubscriberCount => MouseActivityReceived?.GetInvocationList().Length ?? 0;
        internal Action? StopTail { get; set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopTail?.Invoke();
            IsRunning = false;
            return Task.CompletedTask;
        }

        internal void Emit(MouseActivityInfo activity)
        {
            TimestampedMouseActivityReceived?.Invoke(activity);
            MouseActivityReceived?.Invoke(activity.ActionType, activity.X, activity.Y, activity.WheelDelta);
        }
    }

    private sealed class FakeLegacyMouseSource : IMouseHookSource
    {
        public bool IsRunning { get; private set; }
        public event Action<MouseActionType, int?, int?, int?>? MouseActivityReceived;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IsRunning = false;
            return Task.CompletedTask;
        }

        internal void Emit(MouseActionType action, int? x, int? y, int? wheelDelta) => MouseActivityReceived?.Invoke(action, x, y, wheelDelta);
    }
}
