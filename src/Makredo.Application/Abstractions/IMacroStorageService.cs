using Makredo.Domain.Models;

namespace Makredo.Application.Abstractions;

public interface IMacroStorageService
{
    Task SaveAsJsonAsync(
        MacroSession session,
        string filePath,
        CancellationToken cancellationToken = default);

    Task<MacroSession> LoadFromJsonAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task SaveAsXmlAsync(
        MacroSession session,
        string filePath,
        CancellationToken cancellationToken = default);

    Task<MacroSession> LoadFromXmlAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
