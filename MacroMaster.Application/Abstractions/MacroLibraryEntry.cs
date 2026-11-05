namespace MacroMaster.Application.Abstractions;

public sealed record MacroLibraryEntry(
    string Name,
    string FilePath,
    DateTime LastModifiedUtc,
    int EventCount,
    int TotalDurationMs,
    MacroLibraryFileFormat Format);
