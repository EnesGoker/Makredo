using MacroMaster.Domain.Models;

namespace MacroMaster.Application.Abstractions;

public interface IPlaybackSettingsStore
{
    Task<PlaybackSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        PlaybackSettings settings,
        CancellationToken cancellationToken = default);
}
