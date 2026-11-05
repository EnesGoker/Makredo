using Makredo.Domain.Models;

namespace Makredo.Application.Abstractions;

public interface IXmlMacroStorageService
{
    Task SaveAsync(
        MacroSession session,
        string filePath,
        CancellationToken cancellationToken = default);

    Task<MacroSession> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
