using MacroMaster.Domain.Models;

namespace MacroMaster.Application.Abstractions;

public interface IInputPlaybackAdapter
{
    Task PlayEventAsync(
        MacroEvent macroEvent,
        CancellationToken cancellationToken = default);
}
