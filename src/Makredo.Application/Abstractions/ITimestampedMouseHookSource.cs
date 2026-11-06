namespace Makredo.Application.Abstractions;

public interface ITimestampedMouseHookSource : IMouseHookSource
{
    event Action<MouseActivityInfo>? TimestampedMouseActivityReceived;
}
