using System.Text.Json;
using MacroMaster.Application.Abstractions;

namespace MacroMaster.Infrastructure.Persistence;

public sealed class JsonMacroLibraryUserStateStore : IMacroLibraryUserStateStore
{
    private const int MaxRecentEntries = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;

    public JsonMacroLibraryUserStateStore(string filePath)
    {
        _filePath = AtomicFileWriteHelper.ResolveFilePath(filePath);
    }

    public async Task<MacroLibraryUserState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new MacroLibraryUserState();
        }

        await using FileStream stream = new(
            _filePath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });

        MacroLibraryUserState? state = await JsonSerializer.DeserializeAsync<MacroLibraryUserState>(
            stream,
            JsonOptions,
            cancellationToken);

        return Normalize(state);
    }

    public async Task SaveAsync(
        MacroLibraryUserState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        MacroLibraryUserState normalizedState = Normalize(state);

        await AtomicFileWriteHelper.WriteAsync(
            _filePath,
            async (stream, token) =>
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    normalizedState,
                    JsonOptions,
                    token);
            },
            cancellationToken);
    }

    private static MacroLibraryUserState Normalize(MacroLibraryUserState? state)
    {
        if (state is null)
        {
            return new MacroLibraryUserState();
        }

        IEnumerable<string> favoriteFilePathCandidates =
            state.FavoriteFilePaths ?? [];
        IEnumerable<KeyValuePair<string, DateTime>> recentFilePathCandidates =
            state.LastUsedUtcByFilePath ?? [];

        string[] favoriteFilePaths = favoriteFilePathCandidates
            .Select(TryNormalizePath)
            .Where(path => path is not null)
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var recentFilePaths = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        foreach ((string filePath, DateTime lastUsedUtc) in recentFilePathCandidates)
        {
            string? normalizedPath = TryNormalizePath(filePath);

            if (normalizedPath is null)
            {
                continue;
            }

            DateTime normalizedTimestamp = NormalizeUtc(lastUsedUtc);

            if (!recentFilePaths.TryGetValue(normalizedPath, out DateTime existingTimestamp)
                || normalizedTimestamp > existingTimestamp)
            {
                recentFilePaths[normalizedPath] = normalizedTimestamp;
            }
        }

        return new MacroLibraryUserState
        {
            FavoriteFilePaths = favoriteFilePaths.ToList(),
            LastUsedUtcByFilePath = recentFilePaths
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Take(MaxRecentEntries)
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase)
        };
    }

    private static string? TryNormalizePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(filePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static DateTime NormalizeUtc(DateTime timestamp)
    {
        return timestamp.Kind switch
        {
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
            _ => timestamp
        };
    }
}
