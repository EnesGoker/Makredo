using MacroMaster.Domain.Models;

namespace MacroMaster.Application.Abstractions;

public interface IHotkeyService
{
    bool IsRegistered { get; }

    bool IsHotkeyRegistered(HotkeyBinding hotkeyBinding)
    {
        ArgumentNullException.ThrowIfNull(hotkeyBinding);
        return IsRegistered;
    }

    Task RegisterAsync(CancellationToken cancellationToken = default);

    Task UnregisterAsync(CancellationToken cancellationToken = default);

    event Action? RecordToggleRequested;
    event Action? PlaybackToggleRequested;
    event Action? StopRequested;
    event Action? HotkeySettingsRequested;
}
