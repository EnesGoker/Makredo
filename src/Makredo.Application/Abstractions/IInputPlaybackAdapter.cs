using Makredo.Domain.Models;

namespace Makredo.Application.Abstractions;

public interface IInputPlaybackAdapter
{
    Task PlayEventAsync(
        MacroEvent macroEvent,
        CancellationToken cancellationToken = default);

    Task ReleaseEventAsync(
        MacroEvent releaseEvent,
        CancellationToken cancellationToken = default)
    {
        return PlayEventAsync(releaseEvent, cancellationToken);
    }
}
