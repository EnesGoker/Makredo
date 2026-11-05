using MacroMaster.Application.Abstractions;
using MacroMaster.Domain.Models;

namespace MacroMaster.Infrastructure.Persistence;

public sealed class MacroStorageService : IMacroStorageService
{
    private readonly IJsonMacroStorageService _jsonMacroStorageService;
    private readonly IXmlMacroStorageService _xmlMacroStorageService;

    public MacroStorageService(
        IJsonMacroStorageService jsonMacroStorageService,
        IXmlMacroStorageService xmlMacroStorageService)
    {
        _jsonMacroStorageService = jsonMacroStorageService;
        _xmlMacroStorageService = xmlMacroStorageService;
    }

    public Task SaveAsJsonAsync(
        MacroSession session,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return _jsonMacroStorageService.SaveAsync(
            session,
            filePath,
            cancellationToken);
    }

    public Task<MacroSession> LoadFromJsonAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return _jsonMacroStorageService.LoadAsync(
            filePath,
            cancellationToken);
    }

    public Task SaveAsXmlAsync(
        MacroSession session,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return _xmlMacroStorageService.SaveAsync(
            session,
            filePath,
            cancellationToken);
    }

    public Task<MacroSession> LoadFromXmlAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return _xmlMacroStorageService.LoadAsync(
            filePath,
            cancellationToken);
    }
}
