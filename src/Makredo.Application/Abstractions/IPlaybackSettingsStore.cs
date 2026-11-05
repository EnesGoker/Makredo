using Makredo.Domain.Models;

namespace Makredo.Application.Abstractions;

public interface IPlaybackSettingsStore
{
    Task<PlaybackSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        PlaybackSettings settings,
        CancellationToken cancellationToken = default);
}
