using MacroMaster.Application.Abstractions;
using MacroMaster.Domain.Models;
using System.Globalization;
using System.Text.Json;
using System.Xml;

namespace MacroMaster.Infrastructure.Persistence;

public sealed class MacroLibraryService : IMacroLibraryService
{
    private static readonly string[] SupportedSearchPatterns = ["*.json", "*.xml"];
    private static readonly JsonDocumentOptions JsonMetadataOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };
    private static readonly XmlReaderSettings XmlMetadataReaderSettings = new()
    {
        Async = true,
        DtdProcessing = DtdProcessing.Prohibit,
        IgnoreComments = true,
        IgnoreWhitespace = true,
        XmlResolver = null
    };

    private readonly IMacroStorageService _macroStorageService;
    private readonly IAppLogger _logger;
    private readonly string _libraryDirectoryPath;
    private readonly object _metadataCacheLock = new();
    private readonly Dictionary<string, CachedMacroMetadata> _metadataCache = new(StringComparer.OrdinalIgnoreCase);

    public MacroLibraryService(
        IMacroStorageService macroStorageService,
        IAppLogger logger,
        string libraryDirectoryPath)
    {
        ArgumentNullException.ThrowIfNull(macroStorageService);
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrWhiteSpace(libraryDirectoryPath))
        {
            throw new ArgumentException("Makro kutuphane klasoru bos olamaz.", nameof(libraryDirectoryPath));
        }

        _macroStorageService = macroStorageService;
        _logger = logger;
        _libraryDirectoryPath = Path.GetFullPath(libraryDirectoryPath);
    }

    public async Task<IReadOnlyList<MacroLibraryEntry>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureLibraryDirectoryExists();

        var entries = new List<MacroLibraryEntry>();
        var activeFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string filePath in EnumerateMacroFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                FileInfo fileInfo = new(filePath);
                MacroLibraryFileFormat format = ResolveFormat(fileInfo.FullName);
                MacroLibraryMetadata metadata = await ReadMetadataAsync(
                    fileInfo,
                    format,
                    cancellationToken);
                string displayName = Path.GetFileNameWithoutExtension(fileInfo.FullName);

                activeFilePaths.Add(fileInfo.FullName);

                entries.Add(
                    new MacroLibraryEntry(
                        displayName,
                        fileInfo.FullName,
                        fileInfo.LastWriteTimeUtc,
                        metadata.EventCount,
                        metadata.TotalDurationMs,
                        format));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.Log(
                    AppLogLevel.Warning,
                    nameof(MacroLibraryService),
                    $"Makro kutuphanesindeki dosya okunamadi ve listeden atlandi: {filePath}",
                    ex);
            }
        }

        PruneMetadataCache(activeFilePaths);

        return entries
            .OrderByDescending(entry => entry.LastModifiedUtc)
            .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public Task<MacroSession> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        string resolvedFilePath = ResolveLibraryFilePath(filePath);
        return LoadLibrarySessionAsync(resolvedFilePath, cancellationToken);
    }

    public async Task<string> SaveAsync(
        MacroSession session,
        CancellationToken cancellationToken = default)
    {
        return await SaveAsync(session, MacroLibraryFileFormat.Json, cancellationToken);
    }

    public async Task<string> SaveAsync(
        MacroSession session,
        MacroLibraryFileFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        EnsureLibraryDirectoryExists();

        string extension = ResolveExtension(format);
        string filePath = Path.Combine(
            _libraryDirectoryPath,
            BuildSafeFileName(session.Name, extension));

        await SaveByFormatAsync(session, filePath, format, cancellationToken);
        return filePath;
    }

    public async Task<string> ImportAsync(
        string sourceFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentException("Ice aktarilacak makro dosya yolu bos olamaz.", nameof(sourceFilePath));
        }

        string resolvedSourceFilePath = Path.GetFullPath(sourceFilePath);
        AtomicFileWriteHelper.EnsureFileExists(resolvedSourceFilePath);

        EnsureLibraryDirectoryExists();

        MacroLibraryFileFormat format = ResolveFormat(resolvedSourceFilePath);
        string extension = ResolveExtension(format);
        MacroSession session = await LoadByExtensionAsync(resolvedSourceFilePath, cancellationToken);
        session.Name = NormalizeSessionName(Path.GetFileNameWithoutExtension(resolvedSourceFilePath));

        string filePath = BuildAvailableLibraryFilePath(
            session.Name,
            extension,
            resolvedSourceFilePath);

        await SaveByFormatAsync(session, filePath, format, cancellationToken);
        return filePath;
    }

    public async Task<string> RenameAsync(
        string filePath,
        string newName,
        CancellationToken cancellationToken = default)
    {
        string resolvedFilePath = ResolveLibraryFilePath(filePath);
        string normalizedName = NormalizeSessionName(newName);
        MacroLibraryFileFormat format = ResolveFormat(resolvedFilePath);
        string extension = Path.GetExtension(resolvedFilePath);
        string targetFilePath = Path.Combine(
            _libraryDirectoryPath,
            BuildSafeFileName(normalizedName, extension));

        if (!IsSamePath(resolvedFilePath, targetFilePath) && File.Exists(targetFilePath))
        {
            throw new InvalidOperationException(
                $"Bu isimde bir makro zaten var: {Path.GetFileName(targetFilePath)}");
        }

        MacroSession session = await LoadByExtensionAsync(resolvedFilePath, cancellationToken);
        session.Name = normalizedName;

        await SaveByFormatAsync(session, targetFilePath, format, cancellationToken);

        if (!IsSamePath(resolvedFilePath, targetFilePath))
        {
            File.Delete(resolvedFilePath);
        }

        return targetFilePath;
    }

    public Task DeleteAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string resolvedFilePath = ResolveLibraryFilePath(filePath);

        if (File.Exists(resolvedFilePath))
        {
            File.Delete(resolvedFilePath);
        }

        return Task.CompletedTask;
    }

    private IEnumerable<string> EnumerateMacroFiles()
    {
        foreach (string searchPattern in SupportedSearchPatterns)
        {
            foreach (string filePath in Directory.EnumerateFiles(
                         _libraryDirectoryPath,
                         searchPattern,
                         SearchOption.TopDirectoryOnly))
            {
                yield return filePath;
            }
        }
    }

    private async Task<MacroSession> LoadByExtensionAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        return ResolveFormat(filePath) switch
        {
            MacroLibraryFileFormat.Json => await _macroStorageService.LoadFromJsonAsync(filePath, cancellationToken),
            MacroLibraryFileFormat.Xml => await _macroStorageService.LoadFromXmlAsync(filePath, cancellationToken),
            _ => throw new NotSupportedException($"Desteklenmeyen makro dosya formati: {filePath}")
        };
    }

    private async Task<MacroSession> LoadLibrarySessionAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        MacroSession session = await LoadByExtensionAsync(filePath, cancellationToken);
        session.Name = NormalizeSessionName(Path.GetFileNameWithoutExtension(filePath));
        return session;
    }

    private async Task<MacroLibraryMetadata> ReadMetadataAsync(
        FileInfo fileInfo,
        MacroLibraryFileFormat format,
        CancellationToken cancellationToken)
    {
        var signature = new MacroFileSignature(
            fileInfo.LastWriteTimeUtc,
            fileInfo.Length);

        lock (_metadataCacheLock)
        {
            if (_metadataCache.TryGetValue(fileInfo.FullName, out CachedMacroMetadata? cachedMetadata)
                && cachedMetadata.Signature == signature)
            {
                return cachedMetadata.Metadata;
            }
        }

        MacroLibraryMetadata metadata = format switch
        {
            MacroLibraryFileFormat.Json => await ReadJsonMetadataAsync(fileInfo.FullName, cancellationToken),
            MacroLibraryFileFormat.Xml => await ReadXmlMetadataAsync(fileInfo.FullName, cancellationToken),
            _ => throw new NotSupportedException($"Desteklenmeyen makro dosya formati: {fileInfo.FullName}")
        };

        lock (_metadataCacheLock)
        {
            _metadataCache[fileInfo.FullName] = new CachedMacroMetadata(signature, metadata);
        }

        return metadata;
    }

    private static async Task<MacroLibraryMetadata> ReadJsonMetadataAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            filePath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });

        using JsonDocument document = await JsonDocument.ParseAsync(
            stream,
            JsonMetadataOptions,
            cancellationToken);

        JsonElement root = document.RootElement;
        string? name = null;
        int eventCount = 0;
        int totalDurationMs = 0;

        if (TryGetPropertyIgnoreCase(root, nameof(MacroSession.Name), out JsonElement nameElement)
            && nameElement.ValueKind == JsonValueKind.String)
        {
            name = nameElement.GetString();
        }

        if (TryGetPropertyIgnoreCase(root, nameof(MacroSession.Events), out JsonElement eventsElement)
            && eventsElement.ValueKind == JsonValueKind.Array)
        {
            eventCount = eventsElement.GetArrayLength();

            foreach (JsonElement eventElement in eventsElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (TryGetPropertyIgnoreCase(eventElement, nameof(MacroEvent.DelayMs), out JsonElement delayElement)
                    && delayElement.TryGetInt32(out int delayMs))
                {
                    totalDurationMs = AddClampedDuration(totalDurationMs, delayMs);
                }
            }
        }

        return new MacroLibraryMetadata(name, eventCount, totalDurationMs);
    }

    private static async Task<MacroLibraryMetadata> ReadXmlMetadataAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            filePath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });

        using XmlReader reader = XmlReader.Create(stream, XmlMetadataReaderSettings);

        string? name = null;
        int eventCount = 0;
        int totalDurationMs = 0;
        bool isInsideEvents = false;
        int eventsDepth = -1;
        bool isInsideMacroEvent = false;
        int macroEventDepth = -1;

        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reader.NodeType == XmlNodeType.Element)
            {
                if (name is null
                    && string.Equals(reader.LocalName, nameof(MacroSession.Name), StringComparison.Ordinal))
                {
                    name = await reader.ReadElementContentAsStringAsync();
                    continue;
                }

                if (string.Equals(reader.LocalName, nameof(MacroSession.Events), StringComparison.Ordinal))
                {
                    isInsideEvents = !reader.IsEmptyElement;
                    eventsDepth = reader.Depth;
                    continue;
                }

                if (isInsideEvents
                    && string.Equals(reader.LocalName, nameof(MacroEvent), StringComparison.Ordinal))
                {
                    eventCount++;
                    isInsideMacroEvent = !reader.IsEmptyElement;
                    macroEventDepth = reader.Depth;
                    continue;
                }

                if (isInsideMacroEvent
                    && string.Equals(reader.LocalName, nameof(MacroEvent.DelayMs), StringComparison.Ordinal))
                {
                    string rawDelay = await reader.ReadElementContentAsStringAsync();

                    if (int.TryParse(
                            rawDelay,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int delayMs))
                    {
                        totalDurationMs = AddClampedDuration(totalDurationMs, delayMs);
                    }
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement
                     && isInsideMacroEvent
                     && reader.Depth == macroEventDepth
                     && string.Equals(reader.LocalName, nameof(MacroEvent), StringComparison.Ordinal))
            {
                isInsideMacroEvent = false;
                macroEventDepth = -1;
            }
            else if (reader.NodeType == XmlNodeType.EndElement
                     && isInsideEvents
                     && reader.Depth == eventsDepth
                     && string.Equals(reader.LocalName, nameof(MacroSession.Events), StringComparison.Ordinal))
            {
                isInsideEvents = false;
                eventsDepth = -1;
            }
        }

        return new MacroLibraryMetadata(name, eventCount, totalDurationMs);
    }

    private static int AddClampedDuration(int currentTotalMs, int delayMs)
    {
        if (delayMs <= 0)
        {
            return currentTotalMs;
        }

        long nextTotalMs = (long)currentTotalMs + delayMs;
        return nextTotalMs >= int.MaxValue
            ? int.MaxValue
            : (int)nextTotalMs;
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private void PruneMetadataCache(HashSet<string> activeFilePaths)
    {
        lock (_metadataCacheLock)
        {
            foreach (string cachedFilePath in _metadataCache.Keys.ToArray())
            {
                if (!activeFilePaths.Contains(cachedFilePath))
                {
                    _metadataCache.Remove(cachedFilePath);
                }
            }
        }
    }

    private async Task SaveByFormatAsync(
        MacroSession session,
        string filePath,
        MacroLibraryFileFormat format,
        CancellationToken cancellationToken)
    {
        switch (format)
        {
            case MacroLibraryFileFormat.Json:
                await _macroStorageService.SaveAsJsonAsync(session, filePath, cancellationToken);
                break;
            case MacroLibraryFileFormat.Xml:
                await _macroStorageService.SaveAsXmlAsync(session, filePath, cancellationToken);
                break;
            default:
                throw new NotSupportedException($"Desteklenmeyen makro dosya formati: {filePath}");
        }
    }

    private string ResolveLibraryFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Makro dosya yolu bos olamaz.", nameof(filePath));
        }

        string resolvedFilePath = Path.GetFullPath(filePath);
        string libraryRoot = _libraryDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!resolvedFilePath.StartsWith(libraryRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Makro kutuphanesi disindaki dosyalar bu islem icin kullanilamaz: {resolvedFilePath}");
        }

        return resolvedFilePath;
    }

    private static MacroLibraryFileFormat ResolveFormat(string filePath)
    {
        string extension = Path.GetExtension(filePath);

        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            return MacroLibraryFileFormat.Json;
        }

        if (extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return MacroLibraryFileFormat.Xml;
        }

        throw new NotSupportedException($"Desteklenmeyen makro dosya uzantisi: {extension}");
    }

    private static string ResolveExtension(MacroLibraryFileFormat format)
    {
        return format switch
        {
            MacroLibraryFileFormat.Json => ".json",
            MacroLibraryFileFormat.Xml => ".xml",
            _ => throw new NotSupportedException($"Desteklenmeyen makro dosya formati: {format}")
        };
    }

    private void EnsureLibraryDirectoryExists()
    {
        Directory.CreateDirectory(_libraryDirectoryPath);
    }

    private string BuildAvailableLibraryFilePath(
        string sessionName,
        string extension,
        string? sourceFilePath)
    {
        string safeFileName = BuildSafeFileName(sessionName, extension);
        string baseName = Path.GetFileNameWithoutExtension(safeFileName);
        string candidateFilePath = Path.Combine(_libraryDirectoryPath, safeFileName);

        if (!File.Exists(candidateFilePath)
            || (sourceFilePath is not null && IsSamePath(sourceFilePath, candidateFilePath)))
        {
            return candidateFilePath;
        }

        for (int suffix = 2; suffix <= 999; suffix++)
        {
            candidateFilePath = Path.Combine(
                _libraryDirectoryPath,
                $"{baseName} ({suffix.ToString(CultureInfo.InvariantCulture)}){extension}");

            if (!File.Exists(candidateFilePath)
                || (sourceFilePath is not null && IsSamePath(sourceFilePath, candidateFilePath)))
            {
                return candidateFilePath;
            }
        }

        throw new InvalidOperationException(
            $"Makro kutuphanesi icin benzersiz dosya adi olusturulamadi: {baseName}{extension}");
    }

    private static string BuildSafeFileName(string sessionName, string extension)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitizedName = new(
            sessionName
                .Where(character => !invalidCharacters.Contains(character))
                .ToArray());

        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            sanitizedName = "MakroOturumu";
        }

        return sanitizedName + extension;
    }

    private static string NormalizeSessionName(string sessionName)
    {
        if (string.IsNullOrWhiteSpace(sessionName))
        {
            throw new ArgumentException("Makro adi bos olamaz.", nameof(sessionName));
        }

        string normalizedName = sessionName.Trim();

        if (normalizedName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            normalizedName = Path.GetFileNameWithoutExtension(normalizedName);
        }

        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        normalizedName = new string(
            normalizedName
                .Where(character => !invalidCharacters.Contains(character))
                .ToArray());

        normalizedName = normalizedName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("Makro adi gecerli bir dosya adi olmalidir.", nameof(sessionName));
        }

        return normalizedName;
    }

    private static bool IsSamePath(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed record MacroLibraryMetadata(
        string? Name,
        int EventCount,
        int TotalDurationMs);

    private sealed record MacroFileSignature(DateTime LastWriteTimeUtc, long Length);

    private sealed record CachedMacroMetadata(
        MacroFileSignature Signature,
        MacroLibraryMetadata Metadata);
}
