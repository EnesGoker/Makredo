using Makredo.Application.Hotkeys;
using Makredo.Domain.Models;

namespace Makredo.Application.Abstractions;

public interface IHotkeyService
{
    IReadOnlyList<HotkeyRegistrationFailure> RegistrationFailures => [];

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
}
