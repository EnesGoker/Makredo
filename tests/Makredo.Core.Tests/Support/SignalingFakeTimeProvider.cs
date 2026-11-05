using Microsoft.Extensions.Time.Testing;

namespace Makredo.Core.Tests.Support;

internal sealed class SignalingFakeTimeProvider : FakeTimeProvider
{
    private readonly TaskCompletionSource<TimeSpan> _timerScheduled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<TimeSpan> TimerScheduled => _timerScheduled.Task;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ITimer timer = base.CreateTimer(callback, state, dueTime, period);
        _timerScheduled.TrySetResult(dueTime);
        return timer;
    }
}
