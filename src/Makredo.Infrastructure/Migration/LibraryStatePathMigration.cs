using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Makredo.Infrastructure.Migration;

internal static class LibraryStatePathMigration
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    internal static void Rebase(
        string stagedLibraryStateFilePath,
        string sourceMacrosDirectoryPath,
        string destinationMacrosDirectoryPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] originalBytes;

        try
        {
            originalBytes = File.ReadAllBytes(stagedLibraryStateFilePath);
        }
        catch (FileNotFoundException)
        {
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(stagedLibraryStateFilePath);
        bool hasUtf8Bom = originalBytes.Length >= 3
            && originalBytes[0] == 0xef
            && originalBytes[1] == 0xbb
            && originalBytes[2] == 0xbf;
        JsonObject? state;
        bool changed = false;

        try
        {
            ReadOnlySpan<byte> json = hasUtf8Bom ? originalBytes.AsSpan(3) : originalBytes;
            state = JsonNode.Parse(json) as JsonObject;
            if (state is null)
            {
                return;
            }

            foreach ((string propertyName, JsonNode? propertyValue) in state.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.Equals(propertyName, "FavoriteFilePaths", StringComparison.OrdinalIgnoreCase)
                    && propertyValue is JsonArray favorites)
                {
                    changed |= RebaseFavorites(
                        favorites,
                        sourceMacrosDirectoryPath,
                        destinationMacrosDirectoryPath,
                        cancellationToken);
                }
                else if (string.Equals(propertyName, "LastUsedUtcByFilePath", StringComparison.OrdinalIgnoreCase)
                    && propertyValue is JsonObject recent)
                {
                    changed |= RebaseRecentPaths(
                        recent,
                        sourceMacrosDirectoryPath,
                        destinationMacrosDirectoryPath,
                        cancellationToken);
                }
            }
        }
        catch (JsonException)
        {
            // Keep malformed metadata byte-for-byte so migration does not change store error semantics.
            return;
        }
        catch (ArgumentException)
        {
            // JsonObject cannot materialize duplicate keys; leave unsupported metadata untouched.
            return;
        }

        if (!changed)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        byte[] updatedBytes = Encoding.UTF8.GetBytes(state.ToJsonString(JsonOptions));
        cancellationToken.ThrowIfCancellationRequested();

        using (var stream = new FileStream(
            stagedLibraryStateFilePath,
            FileMode.Truncate,
            FileAccess.Write,
            FileShare.None))
        {
            if (hasUtf8Bom)
            {
                stream.Write(originalBytes.AsSpan(0, 3));
            }

            stream.Write(updatedBytes);
            stream.Flush(flushToDisk: true);
        }

        File.SetLastWriteTimeUtc(stagedLibraryStateFilePath, lastWriteTimeUtc);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static bool RebaseFavorites(
        JsonArray favorites,
        string sourceMacrosDirectoryPath,
        string destinationMacrosDirectoryPath,
        CancellationToken cancellationToken)
    {
        bool changed = false;
        for (int index = 0; index < favorites.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (favorites[index] is JsonValue value
                && value.TryGetValue(out string? filePath)
                && filePath is not null
                && TryRebasePath(filePath, sourceMacrosDirectoryPath, destinationMacrosDirectoryPath, out string rebasedPath))
            {
                favorites[index] = rebasedPath;
                changed = true;
            }
        }

        return changed;
    }

    private static bool RebaseRecentPaths(
        JsonObject recent,
        string sourceMacrosDirectoryPath,
        string destinationMacrosDirectoryPath,
        CancellationToken cancellationToken)
    {
        StringComparer pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        List<RecentPathChange> changes = [];

        foreach ((string filePath, JsonNode? value) in recent.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryRebasePath(filePath, sourceMacrosDirectoryPath, destinationMacrosDirectoryPath, out string rebasedPath))
            {
                changes.Add(new RecentPathChange(filePath, rebasedPath, value));
            }
        }

        bool changed = false;
        foreach (IGrouping<string, RecentPathChange> group in changes.GroupBy(change => change.DestinationPath, pathComparer))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RecentPathChange[] sourceEntries = group.ToArray();
            KeyValuePair<string, JsonNode?>[] destinationEntries = recent
                .Where(entry => pathComparer.Equals(entry.Key, group.Key))
                .ToArray();

            if (sourceEntries.Length == 1 && destinationEntries.Length == 0)
            {
                recent.Remove(sourceEntries[0].SourcePath);
                recent[group.Key] = sourceEntries[0].Value;
                changed = true;
                continue;
            }

            if (!TryChooseNewestSource(sourceEntries, cancellationToken, out RecentPathChange newestSource, out DateTimeOffset sourceTimestamp)
                || !TryChooseNewestDestination(destinationEntries, cancellationToken, out string? existingDestinationPath, out DateTimeOffset destinationTimestamp))
            {
                // Do not drop an unsupported value when more than one key maps to the same path.
                continue;
            }

            JsonNode? winningSourceValue = newestSource.Value?.DeepClone();
            foreach (RecentPathChange sourceEntry in sourceEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                recent.Remove(sourceEntry.SourcePath);
            }

            if (existingDestinationPath is null)
            {
                recent[group.Key] = winningSourceValue;
            }
            else if (sourceTimestamp > destinationTimestamp)
            {
                recent[existingDestinationPath] = winningSourceValue;
            }

            changed = true;
        }

        return changed;
    }

    private static bool TryChooseNewestSource(
        RecentPathChange[] entries,
        CancellationToken cancellationToken,
        out RecentPathChange newest,
        out DateTimeOffset timestamp)
    {
        newest = entries[0];
        timestamp = default;
        bool hasTimestamp = false;

        foreach (RecentPathChange entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryGetTimestamp(entry.Value, out DateTimeOffset candidateTimestamp))
            {
                return false;
            }

            if (!hasTimestamp || candidateTimestamp > timestamp)
            {
                newest = entry;
                timestamp = candidateTimestamp;
                hasTimestamp = true;
            }
        }

        return true;
    }

    private static bool TryChooseNewestDestination(
        KeyValuePair<string, JsonNode?>[] entries,
        CancellationToken cancellationToken,
        out string? newestPath,
        out DateTimeOffset timestamp)
    {
        newestPath = null;
        timestamp = default;

        foreach ((string path, JsonNode? value) in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryGetTimestamp(value, out DateTimeOffset candidateTimestamp))
            {
                return false;
            }

            if (newestPath is null || candidateTimestamp > timestamp)
            {
                newestPath = path;
                timestamp = candidateTimestamp;
            }
        }

        return true;
    }

    private static bool TryGetTimestamp(JsonNode? node, out DateTimeOffset timestamp)
    {
        timestamp = default;
        return node is JsonValue value
            && value.TryGetValue(out string? text)
            && text is not null
            && value.TryGetValue<DateTimeOffset>(out _)
            && DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out timestamp);
    }

    private static bool TryRebasePath(
        string filePath,
        string sourceMacrosDirectoryPath,
        string destinationMacrosDirectoryPath,
        out string rebasedPath)
    {
        rebasedPath = filePath;
        if (!Path.IsPathFullyQualified(filePath))
        {
            return false;
        }

        try
        {
            string relativePath = Path.GetRelativePath(sourceMacrosDirectoryPath, Path.GetFullPath(filePath));
            if (relativePath == "."
                || relativePath == ".."
                || Path.IsPathRooted(relativePath)
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
            {
                return false;
            }

            rebasedPath = Path.GetFullPath(Path.Combine(destinationMacrosDirectoryPath, relativePath));
            return !string.Equals(filePath, rebasedPath, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private sealed record RecentPathChange(string SourcePath, string DestinationPath, JsonNode? Value);
}
