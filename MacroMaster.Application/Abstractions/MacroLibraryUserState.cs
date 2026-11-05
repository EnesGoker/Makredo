namespace MacroMaster.Application.Abstractions;

public sealed class MacroLibraryUserState
{
    public List<string> FavoriteFilePaths { get; set; } = [];

    public Dictionary<string, DateTime> LastUsedUtcByFilePath { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
