using MacroMaster.Domain.Models;

namespace MacroMaster.Application.Abstractions;

public interface IMacroLibraryService
{
    Task<IReadOnlyList<MacroLibraryEntry>> ListAsync(CancellationToken cancellationToken = default);

    Task<MacroSession> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<string> SaveAsync(
        MacroSession session,
        CancellationToken cancellationToken = default);

    Task<string> SaveAsync(
        MacroSession session,
        MacroLibraryFileFormat format,
        CancellationToken cancellationToken = default);

    Task<string> ImportAsync(
        string sourceFilePath,
        CancellationToken cancellationToken = default);

    Task<string> RenameAsync(
        string filePath,
        string newName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
