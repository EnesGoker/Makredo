using System.Text;

namespace Makredo.Infrastructure.Migration;

public sealed class DataDirectoryMigrator
{
    public const string CompletionMarkerFileName = ".legacy-data-migration.json";

    private const string CompletionMarkerContent = "{\"version\":1,\"hotkeyMigrationPending\":true}";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly string[] SettingsFileNames =
    [
        "playback-settings.json",
        "hotkey-settings.json",
        "library-state.json"
    ];

    private readonly Action<string, string, CancellationToken> _copyFile;

    public DataDirectoryMigrator()
        : this(CopyFile)
    {
    }

    internal DataDirectoryMigrator(Action<string, string, CancellationToken> copyFile)
    {
        _copyFile = copyFile ?? throw new ArgumentNullException(nameof(copyFile));
    }

    public DataDirectoryMigrationResult Migrate(
        string sourceDirectoryPath,
        string destinationDirectoryPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string sourcePath = ResolveDirectoryPath(sourceDirectoryPath, nameof(sourceDirectoryPath));
        string destinationPath = ResolveDirectoryPath(destinationDirectoryPath, nameof(destinationDirectoryPath));
        StringComparison pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string? sourceParent = Path.GetDirectoryName(sourcePath);
        string? destinationParent = Path.GetDirectoryName(destinationPath);

        if (string.Equals(sourcePath, destinationPath, pathComparison)
            || sourceParent is null
            || destinationParent is null
            || !string.Equals(sourceParent, destinationParent, pathComparison))
        {
            throw new ArgumentException(
                "Source and destination must be different sibling directories.",
                nameof(destinationDirectoryPath));
        }

        if (GetAttributesIfExists(destinationPath) is not null)
        {
            return DataDirectoryMigrationResult.DestinationExists;
        }

        FileAttributes? sourceAttributes = GetAttributesIfExists(sourcePath);
        if (sourceAttributes is null)
        {
            return DataDirectoryMigrationResult.SourceMissing;
        }

        EnsureDirectory(sourcePath, sourceAttributes.Value);

        string stagingPath = Path.Combine(
            destinationParent,
            $".{Path.GetFileName(destinationPath)}.migration-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(stagingPath);
            CopySettings(sourcePath, stagingPath, cancellationToken);

            string sourceMacrosPath = Path.Combine(sourcePath, "macros");
            FileAttributes? macrosAttributes = GetAttributesIfExists(sourceMacrosPath);
            if (macrosAttributes is not null)
            {
                EnsureDirectory(sourceMacrosPath, macrosAttributes.Value);
                CopyDirectory(
                    sourceMacrosPath,
                    Path.Combine(stagingPath, "macros"),
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            WriteCompletionMarker(stagingPath);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // The sibling rename publishes the completed tree without replacing an existing target.
                Directory.Move(stagingPath, destinationPath);
            }
            catch (IOException) when (GetAttributesIfExists(destinationPath) is not null)
            {
                return DataDirectoryMigrationResult.DestinationExists;
            }

            return DataDirectoryMigrationResult.Migrated;
        }
        finally
        {
            DeleteOwnStagingDirectory(stagingPath);
        }
    }

    private void CopySettings(
        string sourcePath,
        string stagingPath,
        CancellationToken cancellationToken)
    {
        foreach (string fileName in SettingsFileNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string sourceFilePath = Path.Combine(sourcePath, fileName);
            FileAttributes? attributes = GetAttributesIfExists(sourceFilePath);
            if (attributes is null)
            {
                continue;
            }

            EnsureFile(sourceFilePath, attributes.Value);
            _copyFile(sourceFilePath, Path.Combine(stagingPath, fileName), cancellationToken);
        }
    }

    private void CopyDirectory(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(destinationPath);

        foreach (string sourceEntryPath in Directory.EnumerateFileSystemEntries(sourcePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileAttributes attributes = File.GetAttributes(sourceEntryPath);
            EnsureNotReparsePoint(sourceEntryPath, attributes);
            string destinationEntryPath = Path.Combine(destinationPath, Path.GetFileName(sourceEntryPath));

            if ((attributes & FileAttributes.Directory) != 0)
            {
                CopyDirectory(sourceEntryPath, destinationEntryPath, cancellationToken);
            }
            else
            {
                _copyFile(sourceEntryPath, destinationEntryPath, cancellationToken);
            }
        }
    }

    private static void CopyFile(
        string sourceFilePath,
        string destinationFilePath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(sourceFilePath);

        using (var source = new FileStream(
            sourceFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.SequentialScan))
        using (var destination = new FileStream(
            destinationFilePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.SequentialScan))
        {
            byte[] buffer = new byte[81920];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int bytesRead = source.Read(buffer);
                if (bytesRead == 0)
                {
                    break;
                }

                destination.Write(buffer, 0, bytesRead);
            }

            cancellationToken.ThrowIfCancellationRequested();
            destination.Flush(flushToDisk: true);
        }

        File.SetLastWriteTimeUtc(destinationFilePath, lastWriteTimeUtc);
    }

    private static void WriteCompletionMarker(string stagingPath)
    {
        using var stream = new FileStream(
            Path.Combine(stagingPath, CompletionMarkerFileName),
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        stream.Write(Utf8WithoutBom.GetBytes(CompletionMarkerContent));
        stream.Flush(flushToDisk: true);
    }

    private static string ResolveDirectoryPath(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static FileAttributes? GetAttributesIfExists(string path)
    {
        try
        {
            return File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static void EnsureDirectory(string path, FileAttributes attributes)
    {
        EnsureNotReparsePoint(path, attributes);
        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new IOException($"The migration source must be a directory: {path}");
        }
    }

    private static void EnsureFile(string path, FileAttributes attributes)
    {
        EnsureNotReparsePoint(path, attributes);
        if ((attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException($"The migration source must be a file: {path}");
        }
    }

    private static void EnsureNotReparsePoint(string path, FileAttributes attributes)
    {
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Migration does not follow symbolic links or reparse points: {path}");
        }
    }

    private static void DeleteOwnStagingDirectory(string stagingPath)
    {
        try
        {
            Directory.Delete(stagingPath, recursive: true);
        }
        catch (IOException)
        {
            // A failed cleanup remains isolated from the final destination and future attempts.
        }
        catch (UnauthorizedAccessException)
        {
            // Never change source permissions or remove another attempt's staging directory.
        }
    }
}
