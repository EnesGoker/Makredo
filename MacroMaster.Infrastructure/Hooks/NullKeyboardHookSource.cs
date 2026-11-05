using MacroMaster.Application.Abstractions;

namespace MacroMaster.Infrastructure.Hooks;

public sealed class NullKeyboardHookSource : IKeyboardHookSource
{
    public bool IsRunning { get; private set; }

    public event Action<KeyboardActivityInfo>? KeyActivityReceived
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
