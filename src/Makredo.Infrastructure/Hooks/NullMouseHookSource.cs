using Makredo.Application.Abstractions;
using Makredo.Domain.Enums;

namespace Makredo.Infrastructure.Hooks;

public sealed class NullMouseHookSource : IMouseHookSource
{
    public bool IsRunning { get; private set; }

    public event Action<MouseActionType, int?, int?, int?>? MouseActivityReceived
    {
        add { }
        remove { }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = false;
        return Task.CompletedTask;
    }
}
