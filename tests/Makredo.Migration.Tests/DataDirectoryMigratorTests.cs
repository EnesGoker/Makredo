using System.Text;
using System.Text.Json;
using Makredo.Application.Abstractions;
using Makredo.Infrastructure.Migration;
using Makredo.Infrastructure.Persistence;
using Xunit;

namespace Makredo.Migration.Tests;

public sealed class DataDirectoryMigratorTests
{
    [Fact]
    public void MigrateCopiesSelectedDataAndPreservesSource()
    {
        using var sandbox = new MigrationSandbox();
        sandbox.WriteSourceText("playback-settings.json", "{\"speedMultiplier\":2}");
        byte[] hotkeyContents = Encoding.UTF8.GetBytes("{\"record\":\"F8\",\"playback\":\"F9\",\"stop\":\"F10\"}\n");
        sandbox.WriteSourceFile("hotkey-settings.json", hotkeyContents);
        sandbox.WriteSourceText("library-state.json", "{\"favorites\":[\"synthetic\"]}");
        sandbox.WriteSourceText("macros/sample.json", "synthetic macro bytes");
        sandbox.WriteSourceText("macros/nested/sample.xml", "<synthetic />");
        sandbox.WriteSourceFile("macros/large.bin", Enumerable.Range(0, 200000).Select(index => (byte)(index % 251)).ToArray());
        Directory.CreateDirectory(Path.Combine(sandbox.SourcePath, "macros", "nested", "empty"));
        sandbox.WriteSourceText("logs/old.log", "synthetic log excluded from migration");
        sandbox.WriteSourceText("unrelated.txt", "unrelated root file");
        Dictionary<string, FileSnapshot> sourceSnapshot = CaptureFiles(sandbox.SourcePath);

        DataDirectoryMigrationResult result = new DataDirectoryMigrator().Migrate(
            sandbox.SourcePath,
            sandbox.DestinationPath);

        Assert.Equal(DataDirectoryMigrationResult.Migrated, result);
        Assert.Equal(
            new[]
            {
                DataDirectoryMigrator.CompletionMarkerFileName,
                "hotkey-settings.json",
                "library-state.json",
                "macros/large.bin",
                "macros/nested/sample.xml",
                "macros/sample.json",
                "playback-settings.json"
            }.Order(StringComparer.Ordinal),
            RelativeFiles(sandbox.DestinationPath));
        foreach (string relativePath in RelativeFiles(sandbox.DestinationPath))
        {
            if (relativePath == DataDirectoryMigrator.CompletionMarkerFileName)
            {
                continue;
            }

            Assert.Equal(
                File.ReadAllBytes(Path.Combine(sandbox.SourcePath, relativePath)),
                File.ReadAllBytes(Path.Combine(sandbox.DestinationPath, relativePath)));
            Assert.Equal(
                File.GetLastWriteTimeUtc(Path.Combine(sandbox.SourcePath, relativePath)),
                File.GetLastWriteTimeUtc(Path.Combine(sandbox.DestinationPath, relativePath)));
        }

        Assert.Equal(hotkeyContents, File.ReadAllBytes(Path.Combine(sandbox.DestinationPath, "hotkey-settings.json")));
        Assert.True(Directory.Exists(Path.Combine(sandbox.DestinationPath, "macros", "nested", "empty")));
        Assert.False(Directory.Exists(Path.Combine(sandbox.DestinationPath, "logs")));
        using JsonDocument marker = JsonDocument.Parse(File.ReadAllText(sandbox.CompletionMarkerPath));
        Assert.Equal(1, marker.RootElement.GetProperty("version").GetInt32());
        Assert.True(marker.RootElement.GetProperty("hotkeyMigrationPending").GetBoolean());
        AssertFilesUnchanged(sandbox.SourcePath, sourceSnapshot);
    }

    [Fact]
    public void SecondStartupPreservesDestinationChangesAndDoesNotCopyAgain()
    {
        using var sandbox = new MigrationSandbox();
        sandbox.WriteSourceText("playback-settings.json", "original settings");
        sandbox.WriteSourceText("macros/sample.json", "original macro");
        Assert.Equal(
            DataDirectoryMigrationResult.Migrated,
            new DataDirectoryMigrator().Migrate(sandbox.SourcePath, sandbox.DestinationPath));
        File.WriteAllText(Path.Combine(sandbox.DestinationPath, "playback-settings.json"), "new user settings");
        sandbox.WriteSourceText("playback-settings.json", "changed legacy settings");
        sandbox.WriteSourceText("macros/late.json", "new legacy macro");
        Dictionary<string, FileSnapshot> destinationSnapshot = CaptureFiles(sandbox.DestinationPath);
        var migrator = new DataDirectoryMigrator((_, _, _) => throw new InvalidOperationException("Copy must not run twice."));

        DataDirectoryMigrationResult result = migrator.Migrate(sandbox.SourcePath, sandbox.DestinationPath);

        Assert.Equal(DataDirectoryMigrationResult.DestinationExists, result);
        AssertFilesUnchanged(sandbox.DestinationPath, destinationSnapshot);
        Assert.False(File.Exists(Path.Combine(sandbox.DestinationPath, "macros", "late.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingDestinationIsNeverReplaced(bool destinationIsFile)
    {
        using var sandbox = new MigrationSandbox();
        sandbox.WriteSourceText("playback-settings.json", "legacy settings");
        string existingPath;
        if (destinationIsFile)
        {
            existingPath = sandbox.DestinationPath;
        }
        else
        {
            Directory.CreateDirectory(sandbox.DestinationPath);
            existingPath = Path.Combine(sandbox.DestinationPath, "existing-user-data.json");
        }

        File.WriteAllText(existingPath, "preserve existing destination");
        DateTime lastWriteTime = File.GetLastWriteTimeUtc(existingPath);
        var migrator = new DataDirectoryMigrator((_, _, _) => throw new InvalidOperationException("Existing data must not be copied over."));

        DataDirectoryMigrationResult result = migrator.Migrate(sandbox.SourcePath, sandbox.DestinationPath);

        Assert.Equal(DataDirectoryMigrationResult.DestinationExists, result);
        Assert.Equal("preserve existing destination", File.ReadAllText(existingPath));
        Assert.Equal(lastWriteTime, File.GetLastWriteTimeUtc(existingPath));
        Assert.False(File.Exists(sandbox.CompletionMarkerPath));
        if (!destinationIsFile)
        {
            Assert.Equal("existing-user-data.json", Assert.Single(RelativeFiles(sandbox.DestinationPath)));
        }
    }

    [Fact]
    public void MissingSourceDoesNotCreateDestinationOrStaging()
    {
        using var sandbox = new MigrationSandbox();

        DataDirectoryMigrationResult result = new DataDirectoryMigrator().Migrate(
            sandbox.SourcePath,
            sandbox.DestinationPath);

        Assert.Equal(DataDirectoryMigrationResult.SourceMissing, result);
        Assert.Empty(Directory.EnumerateFileSystemEntries(sandbox.RootPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopyFailureDoesNotPublishAndTheNextAttemptStartsFresh(bool accessDenied)
    {
        using var sandbox = new MigrationSandbox();
        sandbox.WriteSourceText("playback-settings.json", "first version");
        sandbox.WriteSourceText("hotkey-settings.json", "legacy hotkeys");
        sandbox.WriteSourceText("macros/sample.json", "legacy macro");
        Dictionary<string, FileSnapshot> sourceSnapshot = CaptureFiles(sandbox.SourcePath);
        int copyCount = 0;
        string copiedRelativePath = string.Empty;
        var failingMigrator = new DataDirectoryMigrator((source, destination, token) =>
        {
            token.ThrowIfCancellationRequested();
            Assert.False(Directory.Exists(sandbox.DestinationPath));
            Assert.Empty(Directory.GetFiles(sandbox.RootPath, DataDirectoryMigrator.CompletionMarkerFileName, SearchOption.AllDirectories));
            copyCount++;
            if (copyCount == 2)
            {
                if (accessDenied)
                {
                    throw new UnauthorizedAccessException("Synthetic locked-file failure.");
                }

                File.WriteAllText(destination, "synthetic partial bytes before disk-full failure");
                throw new IOException("Synthetic full-disk failure.");
            }

            File.Copy(source, destination);
            copiedRelativePath = Path.GetRelativePath(sandbox.SourcePath, source);
        });

        if (accessDenied)
        {
            Assert.Throws<UnauthorizedAccessException>(() => failingMigrator.Migrate(sandbox.SourcePath, sandbox.DestinationPath));
        }
        else
        {
            Assert.Throws<IOException>(() => failingMigrator.Migrate(sandbox.SourcePath, sandbox.DestinationPath));
        }

        Assert.Equal(2, copyCount);
        Assert.NotEmpty(copiedRelativePath);
        AssertNotPublished(sandbox);
        Assert.Equal(sandbox.SourcePath, Assert.Single(Directory.GetDirectories(sandbox.RootPath)));
        AssertFilesUnchanged(sandbox.SourcePath, sourceSnapshot);
        sandbox.WriteSourceText(copiedRelativePath, "latest source version after interruption");
        sandbox.WriteSourceText("macros/after-interruption.json", "new macro after interruption");

        DataDirectoryMigrationResult retriedResult = new DataDirectoryMigrator().Migrate(
            sandbox.SourcePath,
            sandbox.DestinationPath);

        Assert.Equal(DataDirectoryMigrationResult.Migrated, retriedResult);
        Assert.Equal(
            "latest source version after interruption",
            File.ReadAllText(Path.Combine(sandbox.DestinationPath, copiedRelativePath)));
        Assert.Equal(
            "new macro after interruption",
            File.ReadAllText(Path.Combine(sandbox.DestinationPath, "macros", "after-interruption.json")));
        Assert.True(File.Exists(sandbox.CompletionMarkerPath));
    }

    [Fact]
    public void CancellationAfterPartialCopyDoesNotPublishAndCanBeRetried()
    {
        using var sandbox = new MigrationSandbox();
        sandbox.WriteSourceText("playback-settings.json", "legacy settings");
        sandbox.WriteSourceText("hotkey-settings.json", "legacy hotkeys");
        Dictionary<string, FileSnapshot> sourceSnapshot = CaptureFiles(sandbox.SourcePath);
        using var cancellation = new CancellationTokenSource();
        int copiedFiles = 0;
        var migrator = new DataDirectoryMigrator((source, destination, token) =>
        {
            token.ThrowIfCancellationRequested();
            File.Copy(source, destination);
            copiedFiles++;
            cancellation.Cancel();
        });

        Assert.Throws<OperationCanceledException>(() => migrator.Migrate(
            sandbox.SourcePath,
            sandbox.DestinationPath,
            cancellation.Token));

        Assert.Equal(1, copiedFiles);
        AssertNotPublished(sandbox);
        Assert.Equal(sandbox.SourcePath, Assert.Single(Directory.GetDirectories(sandbox.RootPath)));
        AssertFilesUnchanged(sandbox.SourcePath, sourceSnapshot);
        Assert.Equal(
            DataDirectoryMigrationResult.Migrated,
            new DataDirectoryMigrator().Migrate(sandbox.SourcePath, sandbox.DestinationPath));
        Assert.True(File.Exists(sandbox.CompletionMarkerPath));
    }

    [Fact]
    public void PreCanceledMigrationCreatesNothing()
    {
        using var sandbox = new MigrationSandbox();
        sandbox.WriteSourceText("playback-settings.json", "legacy settings");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var migrator = new DataDirectoryMigrator((_, _, _) => throw new InvalidOperationException("Canceled migration must not copy."));

        Assert.Throws<OperationCanceledException>(() => migrator.Migrate(
            sandbox.SourcePath,
            sandbox.DestinationPath,
            cancellation.Token));

        AssertNotPublished(sandbox);
        Assert.Equal(sandbox.SourcePath, Assert.Single(Directory.GetDirectories(sandbox.RootPath)));
    }

    [Fact]
    public void AbandonedStagingIsNotTreatedAsACompletedMigrationOrDeleted()
    {
        using var sandbox = new MigrationSandbox();
        sandbox.WriteSourceText("playback-settings.json", "complete source settings");
        string abandonedPath = Path.Combine(sandbox.RootPath, ".Makredo.migration-abandoned");
        Directory.CreateDirectory(abandonedPath);
        string abandonedFilePath = Path.Combine(abandonedPath, "playback-settings.json");
        File.WriteAllText(abandonedFilePath, "stale partial data owned by another attempt");

        DataDirectoryMigrationResult result = new DataDirectoryMigrator().Migrate(
            sandbox.SourcePath,
            sandbox.DestinationPath);

        Assert.Equal(DataDirectoryMigrationResult.Migrated, result);
        Assert.Equal("complete source settings", File.ReadAllText(Path.Combine(sandbox.DestinationPath, "playback-settings.json")));
        Assert.Equal("stale partial data owned by another attempt", File.ReadAllText(abandonedFilePath));
        Assert.True(File.Exists(sandbox.CompletionMarkerPath));
    }

    [Fact]
    public void DestinationCreatedDuringCopyIsPreserved()
    {
        using var sandbox = new MigrationSandbox();
        sandbox.WriteSourceText("playback-settings.json", "legacy settings");
        sandbox.WriteSourceText("macros/sample.json", "legacy macro");
        Dictionary<string, FileSnapshot> sourceSnapshot = CaptureFiles(sandbox.SourcePath);
        bool destinationCreated = false;
        var migrator = new DataDirectoryMigrator((source, destination, token) =>
        {
            token.ThrowIfCancellationRequested();
            File.Copy(source, destination);
            if (!destinationCreated)
            {
                Directory.CreateDirectory(sandbox.DestinationPath);
                File.WriteAllText(Path.Combine(sandbox.DestinationPath, "winner.txt"), "another instance's data");
                destinationCreated = true;
            }
        });

        DataDirectoryMigrationResult result = migrator.Migrate(sandbox.SourcePath, sandbox.DestinationPath);

        Assert.True(destinationCreated);
        Assert.Equal(DataDirectoryMigrationResult.DestinationExists, result);
        Assert.Equal("winner.txt", Assert.Single(RelativeFiles(sandbox.DestinationPath)));
        Assert.Equal("another instance's data", File.ReadAllText(Path.Combine(sandbox.DestinationPath, "winner.txt")));
        Assert.Empty(Directory.GetFiles(sandbox.RootPath, DataDirectoryMigrator.CompletionMarkerFileName, SearchOption.AllDirectories));
        AssertFilesUnchanged(sandbox.SourcePath, sourceSnapshot);
    }

    [UnixTheory]
    [InlineData("root")]
    [InlineData("directory")]
    [InlineData("file")]
    public void SymbolicLinksInSelectedSourceDataAbortMigration(string linkLocation)
    {
        using var sandbox = new MigrationSandbox();
        string externalPath = Path.Combine(sandbox.RootPath, "external");
        Directory.CreateDirectory(externalPath);
        string externalFilePath = Path.Combine(externalPath, "playback-settings.json");
        File.WriteAllText(externalFilePath, "data outside the selected source tree");
        DateTime lastWriteTime = File.GetLastWriteTimeUtc(externalFilePath);
        if (linkLocation == "root")
        {
            Directory.CreateSymbolicLink(sandbox.SourcePath, externalPath);
        }
        else if (linkLocation == "directory")
        {
            Directory.CreateDirectory(Path.Combine(sandbox.SourcePath, "macros"));
            Directory.CreateSymbolicLink(Path.Combine(sandbox.SourcePath, "macros", "linked"), externalPath);
        }
        else
        {
            Directory.CreateDirectory(sandbox.SourcePath);
            File.CreateSymbolicLink(Path.Combine(sandbox.SourcePath, "playback-settings.json"), externalFilePath);
        }

        Assert.Throws<IOException>(() => new DataDirectoryMigrator().Migrate(sandbox.SourcePath, sandbox.DestinationPath));

        AssertNotPublished(sandbox);
        Assert.Equal("data outside the selected source tree", File.ReadAllText(externalFilePath));
        Assert.Equal(lastWriteTime, File.GetLastWriteTimeUtc(externalFilePath));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData(" ", true)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    public void BlankPathsAreRejected(string invalidPath, bool invalidSource)
    {
        using var sandbox = new MigrationSandbox();

        Assert.ThrowsAny<ArgumentException>(() => new DataDirectoryMigrator().Migrate(
            invalidSource ? invalidPath : sandbox.SourcePath,
            invalidSource ? sandbox.DestinationPath : invalidPath));

        Assert.Empty(Directory.EnumerateFileSystemEntries(sandbox.RootPath));
    }

    [Theory]
    [InlineData("same")]
    [InlineData("nested")]
    [InlineData("different-parent")]
    public void NonSiblingOrIdenticalPathsAreRejected(string destinationKind)
    {
        using var sandbox = new MigrationSandbox();
        sandbox.WriteSourceText("playback-settings.json", "legacy settings");
        Dictionary<string, FileSnapshot> sourceSnapshot = CaptureFiles(sandbox.SourcePath);
        string destinationPath = destinationKind switch
        {
            "same" => sandbox.SourcePath,
            "nested" => Path.Combine(sandbox.SourcePath, "Makredo"),
            _ => Path.Combine(sandbox.RootPath, "other-parent", "Makredo")
        };

        Assert.ThrowsAny<ArgumentException>(() => new DataDirectoryMigrator().Migrate(sandbox.SourcePath, destinationPath));

        AssertFilesUnchanged(sandbox.SourcePath, sourceSnapshot);
        Assert.Equal(sandbox.SourcePath, Assert.Single(Directory.GetDirectories(sandbox.RootPath)));
    }

    [Theory]
    [InlineData("settings-directory")]
    [InlineData("macros-file")]
    [InlineData("source-file")]
    public void UnexpectedSelectedPathKindsAbortWithoutChangingSource(string sourceKind)
    {
        using var sandbox = new MigrationSandbox();
        bool sourceIsFile = sourceKind == "source-file";
        if (sourceKind == "settings-directory")
        {
            Directory.CreateDirectory(Path.Combine(sandbox.SourcePath, "playback-settings.json"));
        }
        else if (sourceKind == "macros-file")
        {
            sandbox.WriteSourceText("macros", "file in place of a macro directory");
        }
        else
        {
            File.WriteAllText(sandbox.SourcePath, "file in place of the source directory");
        }

        byte[] sourceFileContents = sourceIsFile ? File.ReadAllBytes(sandbox.SourcePath) : [];
        DateTime sourceFileWriteTime = sourceIsFile ? File.GetLastWriteTimeUtc(sandbox.SourcePath) : default;
        Dictionary<string, FileSnapshot> sourceSnapshot = sourceIsFile ? [] : CaptureFiles(sandbox.SourcePath);
        string[] sourceDirectories = sourceIsFile
            ? []
            : Directory.GetDirectories(sandbox.SourcePath, "*", SearchOption.AllDirectories);

        Assert.Throws<IOException>(() => new DataDirectoryMigrator().Migrate(sandbox.SourcePath, sandbox.DestinationPath));

        AssertNotPublished(sandbox);
        if (sourceIsFile)
        {
            Assert.Equal(sourceFileContents, File.ReadAllBytes(sandbox.SourcePath));
            Assert.Equal(sourceFileWriteTime, File.GetLastWriteTimeUtc(sandbox.SourcePath));
        }
        else
        {
            AssertFilesUnchanged(sandbox.SourcePath, sourceSnapshot);
            Assert.Equal(sourceDirectories, Directory.GetDirectories(sandbox.SourcePath, "*", SearchOption.AllDirectories));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyEligibleSourcePublishesOnlyTheCompletionMarker(bool sourceContainsOnlyLogs)
    {
        using var sandbox = new MigrationSandbox();
        Directory.CreateDirectory(sandbox.SourcePath);
        if (sourceContainsOnlyLogs)
        {
            sandbox.WriteSourceText("logs/old.log", "excluded legacy logs");
        }

        Dictionary<string, FileSnapshot> sourceSnapshot = CaptureFiles(sandbox.SourcePath);
        var migrator = new DataDirectoryMigrator((_, _, _) => throw new InvalidOperationException("There are no eligible files to copy."));

        DataDirectoryMigrationResult result = migrator.Migrate(sandbox.SourcePath, sandbox.DestinationPath);

        Assert.Equal(DataDirectoryMigrationResult.Migrated, result);
        Assert.Equal(DataDirectoryMigrator.CompletionMarkerFileName, Assert.Single(RelativeFiles(sandbox.DestinationPath)));
        Assert.False(Directory.Exists(Path.Combine(sandbox.DestinationPath, "logs")));
        AssertFilesUnchanged(sandbox.SourcePath, sourceSnapshot);
    }

    [Fact]
    public async Task LibraryStateReferencesFollowCopiedMacrosAndKeepLastUsedTime()
    {
        using var sandbox = new MigrationSandbox();
        sandbox.WriteSourceText("macros/nested/favorite.json", "synthetic macro bytes");
        string legacyMacroPath = Path.Combine(sandbox.SourcePath, "macros", "nested", "favorite.json");
        string migratedMacroPath = Path.Combine(sandbox.DestinationPath, "macros", "nested", "favorite.json");
        DateTime lastUsedUtc = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var sourceState = new MacroLibraryUserState
        {
            FavoriteFilePaths = [legacyMacroPath],
            LastUsedUtcByFilePath = new(StringComparer.OrdinalIgnoreCase)
            {
                [legacyMacroPath] = lastUsedUtc
            }
        };
        sandbox.WriteSourceText("library-state.json", JsonSerializer.Serialize(sourceState));
        Dictionary<string, FileSnapshot> sourceSnapshot = CaptureFiles(sandbox.SourcePath);

        DataDirectoryMigrationResult result = new DataDirectoryMigrator().Migrate(sandbox.SourcePath, sandbox.DestinationPath);

        Assert.Equal(DataDirectoryMigrationResult.Migrated, result);
        string migratedStatePath = Path.Combine(sandbox.DestinationPath, "library-state.json");
        MacroLibraryUserState migratedState = await new JsonMacroLibraryUserStateStore(migratedStatePath).LoadAsync();
        Assert.Equal(migratedMacroPath, Assert.Single(migratedState.FavoriteFilePaths));
        KeyValuePair<string, DateTime> recentEntry = Assert.Single(migratedState.LastUsedUtcByFilePath);
        Assert.Equal(migratedMacroPath, recentEntry.Key);
        Assert.Equal(lastUsedUtc, recentEntry.Value);
        Assert.True(File.Exists(migratedMacroPath));
        Assert.Equal(sourceSnapshot["library-state.json"].LastWriteTimeUtc, File.GetLastWriteTimeUtc(migratedStatePath));
        Assert.True(File.Exists(sandbox.CompletionMarkerPath));
        AssertFilesUnchanged(sandbox.SourcePath, sourceSnapshot);
    }

    [Fact]
    public void LibraryStateRebasingPreservesFieldCasingUnknownValuesAndUnrecognizedArrayItems()
    {
        using var sandbox = new MigrationSandbox();
        sandbox.WriteSourceText("macros/sample.json", "synthetic macro bytes");
        string legacyMacroPath = Path.Combine(sandbox.SourcePath, "macros", "sample.json");
        string migratedMacroPath = Path.Combine(sandbox.DestinationPath, "macros", "sample.json");
        const string recentTime = "2026-01-02T03:04:05Z";
        var state = new Dictionary<string, object?>
        {
            ["fAvOrItEfIlEpAtHs"] = new object?[] { legacyMacroPath, 7, null },
            ["lAsTuSeDuTcByFiLePaTh"] = new Dictionary<string, string> { [legacyMacroPath] = recentTime },
            ["FutureCount"] = 17,
            ["FutureFlag"] = false,
            ["FuturePayload"] = new Dictionary<string, object?>
            {
                ["rawPath"] = legacyMacroPath,
                ["sequence"] = new object?[] { null, 3, "keep" }
            }
        };
        sandbox.WriteSourceText("library-state.json", JsonSerializer.Serialize(state));
        Dictionary<string, FileSnapshot> sourceSnapshot = CaptureFiles(sandbox.SourcePath);

        Assert.Equal(
            DataDirectoryMigrationResult.Migrated,
            new DataDirectoryMigrator().Migrate(sandbox.SourcePath, sandbox.DestinationPath));

        using JsonDocument migrated = JsonDocument.Parse(File.ReadAllText(Path.Combine(sandbox.DestinationPath, "library-state.json")));
        JsonElement root = migrated.RootElement;
        JsonElement favorites = root.GetProperty("fAvOrItEfIlEpAtHs");
        Assert.Equal(migratedMacroPath, favorites[0].GetString());
        Assert.Equal(JsonValueKind.Number, favorites[1].ValueKind);
        Assert.Equal(7, favorites[1].GetInt32());
        Assert.Equal(JsonValueKind.Null, favorites[2].ValueKind);
        Assert.Equal(recentTime, root.GetProperty("lAsTuSeDuTcByFiLePaTh").GetProperty(migratedMacroPath).GetString());
        Assert.False(root.TryGetProperty("FavoriteFilePaths", out _));
        Assert.False(root.TryGetProperty("LastUsedUtcByFilePath", out _));
        Assert.Equal(JsonValueKind.Number, root.GetProperty("FutureCount").ValueKind);
        Assert.Equal(17, root.GetProperty("FutureCount").GetInt32());
        Assert.Equal(JsonValueKind.False, root.GetProperty("FutureFlag").ValueKind);
        JsonElement payload = root.GetProperty("FuturePayload");
        Assert.Equal(legacyMacroPath, payload.GetProperty("rawPath").GetString());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("sequence")[0].ValueKind);
        Assert.Equal(3, payload.GetProperty("sequence")[1].GetInt32());
        Assert.Equal("keep", payload.GetProperty("sequence")[2].GetString());
        AssertFilesUnchanged(sandbox.SourcePath, sourceSnapshot);
    }

    [Fact]
    public void LibraryStateRebasesOnlyAbsoluteReferencesInsideTheSourceMacroDirectory()
    {
        using var sandbox = new MigrationSandbox();
        sandbox.WriteSourceText("macros/sample.json", "synthetic macro bytes");
        string legacyMacroPath = Path.Combine(sandbox.SourcePath, "macros", "sample.json");
        string migratedMacroPath = Path.Combine(sandbox.DestinationPath, "macros", "sample.json");
        string externalPath = Path.Combine(sandbox.RootPath, "external", "sample.json");
        string siblingPrefixPath = Path.Combine(sandbox.SourcePath, "macros-other", "sample.json");
        string relativePath = Path.Combine("macros", "relative.json");
        const string recentTime = "2026-01-02T03:04:05Z";
        var state = new
        {
            FavoriteFilePaths = new[] { legacyMacroPath, externalPath, siblingPrefixPath, relativePath },
            LastUsedUtcByFilePath = new Dictionary<string, string>
            {
                [legacyMacroPath] = recentTime,
                [externalPath] = recentTime,
                [siblingPrefixPath] = recentTime,
                [relativePath] = recentTime
            }
        };
        sandbox.WriteSourceText("library-state.json", JsonSerializer.Serialize(state));
        Dictionary<string, FileSnapshot> sourceSnapshot = CaptureFiles(sandbox.SourcePath);

        Assert.Equal(
            DataDirectoryMigrationResult.Migrated,
            new DataDirectoryMigrator().Migrate(sandbox.SourcePath, sandbox.DestinationPath));

        using JsonDocument migrated = JsonDocument.Parse(File.ReadAllText(Path.Combine(sandbox.DestinationPath, "library-state.json")));
        Assert.Equal(
            new[] { migratedMacroPath, externalPath, siblingPrefixPath, relativePath },
            migrated.RootElement.GetProperty("FavoriteFilePaths").EnumerateArray().Select(value => value.GetString()!).ToArray());
        JsonElement recentEntries = migrated.RootElement.GetProperty("LastUsedUtcByFilePath");
        Assert.Equal(recentTime, recentEntries.GetProperty(migratedMacroPath).GetString());
        Assert.Equal(recentTime, recentEntries.GetProperty(externalPath).GetString());
        Assert.Equal(recentTime, recentEntries.GetProperty(siblingPrefixPath).GetString());
        Assert.Equal(recentTime, recentEntries.GetProperty(relativePath).GetString());
        Assert.False(recentEntries.TryGetProperty(legacyMacroPath, out _));
        AssertFilesUnchanged(sandbox.SourcePath, sourceSnapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConflictingRecentReferencesKeepTheMostRecentValidTimestamp(bool legacyTimestampIsNewer)
    {
        using var sandbox = new MigrationSandbox();
        sandbox.WriteSourceText("macros/sample.json", "synthetic macro bytes");
        string legacyMacroPath = Path.Combine(sandbox.SourcePath, "macros", "sample.json");
        string migratedMacroPath = Path.Combine(sandbox.DestinationPath, "macros", "sample.json");
        const string olderTime = "2026-01-01T12:00:00Z";
        const string newerTime = "2026-03-01T12:00:00Z";
        var state = new
        {
            FavoriteFilePaths = new[] { legacyMacroPath },
            LastUsedUtcByFilePath = new Dictionary<string, string>
            {
                [legacyMacroPath] = legacyTimestampIsNewer ? newerTime : olderTime,
                [migratedMacroPath] = legacyTimestampIsNewer ? olderTime : newerTime
            }
        };
        sandbox.WriteSourceText("library-state.json", JsonSerializer.Serialize(state));
        Dictionary<string, FileSnapshot> sourceSnapshot = CaptureFiles(sandbox.SourcePath);

        Assert.Equal(
            DataDirectoryMigrationResult.Migrated,
            new DataDirectoryMigrator().Migrate(sandbox.SourcePath, sandbox.DestinationPath));

        string migratedStatePath = Path.Combine(sandbox.DestinationPath, "library-state.json");
        MacroLibraryUserState migratedState = await new JsonMacroLibraryUserStateStore(migratedStatePath).LoadAsync();
        KeyValuePair<string, DateTime> recentEntry = Assert.Single(migratedState.LastUsedUtcByFilePath);
        Assert.Equal(migratedMacroPath, recentEntry.Key);
        Assert.Equal(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc), recentEntry.Value);
        AssertFilesUnchanged(sandbox.SourcePath, sourceSnapshot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConflictingRecentReferencesWithInvalidTimestampsKeepBothRawEntries(bool legacyTimestampIsInvalid)
    {
        using var sandbox = new MigrationSandbox();
        sandbox.WriteSourceText("macros/sample.json", "synthetic macro bytes");
        string legacyMacroPath = Path.Combine(sandbox.SourcePath, "macros", "sample.json");
        string migratedMacroPath = Path.Combine(sandbox.DestinationPath, "macros", "sample.json");
        string legacyTime = legacyTimestampIsInvalid ? "legacy-raw-invalid" : "2026-01-01T12:00:00Z";
        string destinationTime = legacyTimestampIsInvalid ? "2026-03-01T12:00:00Z" : "destination-raw-invalid";
        var state = new
        {
            LastUsedUtcByFilePath = new Dictionary<string, string>
            {
                [legacyMacroPath] = legacyTime,
                [migratedMacroPath] = destinationTime
            }
        };
        sandbox.WriteSourceText("library-state.json", JsonSerializer.Serialize(state));
        Dictionary<string, FileSnapshot> sourceSnapshot = CaptureFiles(sandbox.SourcePath);

        Assert.Equal(
            DataDirectoryMigrationResult.Migrated,
            new DataDirectoryMigrator().Migrate(sandbox.SourcePath, sandbox.DestinationPath));

        using JsonDocument migrated = JsonDocument.Parse(File.ReadAllText(Path.Combine(sandbox.DestinationPath, "library-state.json")));
        JsonElement recentEntries = migrated.RootElement.GetProperty("LastUsedUtcByFilePath");
        Assert.Equal(legacyTime, recentEntries.GetProperty(legacyMacroPath).GetString());
        Assert.Equal(destinationTime, recentEntries.GetProperty(migratedMacroPath).GetString());
        AssertFilesUnchanged(sandbox.SourcePath, sourceSnapshot);
    }

    [Theory]
    [InlineData(" { \"FavoriteFilePaths\" : [")]
    [InlineData(" [true, {\"future\":\"value\"}] \r\n")]
    [InlineData(" null \r\n")]
    [InlineData(" 42 \r\n")]
    [InlineData(" { \"FavoriteFilePaths\" : [\"macros/relative.json\"], \"LastUsedUtcByFilePath\" : {\"macros/relative.json\":\"2026-01-01T12:00:00Z\"}, \"unknown\" : 19 }\r\n")]
    public void LibraryStateWithoutApplicableReferencesKeepsExactBytesAndTimestamp(string contents)
    {
        using var sandbox = new MigrationSandbox();
        byte[] originalContents = Encoding.UTF8.GetBytes(contents);
        sandbox.WriteSourceFile("library-state.json", originalContents);
        Dictionary<string, FileSnapshot> sourceSnapshot = CaptureFiles(sandbox.SourcePath);

        Assert.Equal(
            DataDirectoryMigrationResult.Migrated,
            new DataDirectoryMigrator().Migrate(sandbox.SourcePath, sandbox.DestinationPath));

        string migratedStatePath = Path.Combine(sandbox.DestinationPath, "library-state.json");
        Assert.Equal(originalContents, File.ReadAllBytes(migratedStatePath));
        Assert.Equal(sourceSnapshot["library-state.json"].LastWriteTimeUtc, File.GetLastWriteTimeUtc(migratedStatePath));
        AssertFilesUnchanged(sandbox.SourcePath, sourceSnapshot);
    }

    private static void AssertNotPublished(MigrationSandbox sandbox)
    {
        Assert.False(Directory.Exists(sandbox.DestinationPath));
        Assert.False(File.Exists(sandbox.DestinationPath));
        Assert.Empty(Directory.GetFiles(sandbox.RootPath, DataDirectoryMigrator.CompletionMarkerFileName, SearchOption.AllDirectories));
    }

    private static string[] RelativeFiles(string rootPath)
    {
        return Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(rootPath, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static Dictionary<string, FileSnapshot> CaptureFiles(string rootPath)
    {
        return RelativeFiles(rootPath).ToDictionary(
            relativePath => relativePath,
            relativePath => new FileSnapshot(
                File.ReadAllBytes(Path.Combine(rootPath, relativePath)),
                File.GetLastWriteTimeUtc(Path.Combine(rootPath, relativePath))),
            StringComparer.Ordinal);
    }

    private static void AssertFilesUnchanged(string rootPath, Dictionary<string, FileSnapshot> snapshot)
    {
        Assert.True(Directory.Exists(rootPath));
        Assert.Equal(snapshot.Keys.Order(StringComparer.Ordinal), RelativeFiles(rootPath));
        foreach ((string relativePath, FileSnapshot file) in snapshot)
        {
            string path = Path.Combine(rootPath, relativePath);
            Assert.Equal(file.Contents, File.ReadAllBytes(path));
            Assert.Equal(file.LastWriteTimeUtc, File.GetLastWriteTimeUtc(path));
        }
    }

    private sealed record FileSnapshot(byte[] Contents, DateTime LastWriteTimeUtc);

    private sealed class MigrationSandbox : IDisposable
    {
        public MigrationSandbox()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "Makredo.Migration.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string SourcePath => Path.Combine(RootPath, "Polly");

        public string DestinationPath => Path.Combine(RootPath, "Makredo");

        public string CompletionMarkerPath => Path.Combine(DestinationPath, DataDirectoryMigrator.CompletionMarkerFileName);

        public void WriteSourceText(string relativePath, string contents)
        {
            WriteSourceFile(relativePath, Encoding.UTF8.GetBytes(contents));
        }

        public void WriteSourceFile(string relativePath, byte[] contents)
        {
            string path = Path.Combine(SourcePath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, contents);
            File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
        }

        public void Dispose()
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
