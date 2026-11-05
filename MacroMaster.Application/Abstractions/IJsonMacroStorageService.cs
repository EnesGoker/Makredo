using MacroMaster.Domain.Models;

namespace MacroMaster.Application.Abstractions;

public interface IJsonMacroStorageService
{
    Task SaveAsync(
        MacroSession session,
        string filePath,
        CancellationToken cancellationToken = default);

    Task<MacroSession> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
