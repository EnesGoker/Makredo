using MacroMaster.Application.Abstractions;

namespace MacroMaster.Infrastructure.Hooks;

public sealed class NullHotkeyService : IHotkeyService
{
    public bool IsRegistered => false;

    public event Action? RecordToggleRequested
    {
        add { }
        remove { }
    }

    public event Action? PlaybackToggleRequested
    {
        add { }
        remove { }
    }

    public event Action? StopRequested
    {
        add { }
        remove { }
    }

    public event Action? HotkeySettingsRequested
    {
        add { }
        remove { }
    }

    public bool IsHotkeyRegistered(HotkeyBinding hotkeyBinding)
    {
        ArgumentNullException.ThrowIfNull(hotkeyBinding);
        return false;
    }

    public Task RegisterAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task UnregisterAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
