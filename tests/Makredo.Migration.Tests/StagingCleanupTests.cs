using Makredo.Infrastructure.Migration;
using Xunit;

namespace Makredo.Migration.Tests;

public sealed class StagingCleanupTests
{
    [Fact]
    public void OldStagingAndInactiveLeaseAreRemovedBeforeMissingSourceReturns()
    {
        using var sandbox = new CleanupSandbox();
        string stagingPath = sandbox.CreateStaging(TimeSpan.FromHours(25));
        string leasePath = stagingPath + ".lock";
        File.WriteAllText(leasePath, string.Empty);

        DataDirectoryMigrationResult result = new DataDirectoryMigrator(sandbox.Clock).Migrate(
            sandbox.SourcePath,
            sandbox.DestinationPath);

        Assert.Equal(DataDirectoryMigrationResult.SourceMissing, result);
        Assert.False(Directory.Exists(stagingPath));
        Assert.False(File.Exists(leasePath));
        Assert.False(Directory.Exists(sandbox.DestinationPath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(sandbox.RootPath));
    }

    [Theory]
    [InlineData(23)]
    [InlineData(24)]
    [InlineData(-1)]
    public void FreshBoundaryAndFutureStagingArePreserved(int ageHours)
    {
        using var sandbox = new CleanupSandbox();
        string stagingPath = sandbox.CreateStaging(TimeSpan.FromHours(ageHours));
        string stagedFilePath = Path.Combine(stagingPath, "macros", "nested", "sample.json");
        DateTime lastWriteTime = Directory.GetLastWriteTimeUtc(stagingPath);

        Assert.Equal(
            DataDirectoryMigrationResult.SourceMissing,
            new DataDirectoryMigrator(sandbox.Clock).Migrate(sandbox.SourcePath, sandbox.DestinationPath));

        Assert.True(Directory.Exists(stagingPath));
        Assert.Equal("synthetic staged macro", File.ReadAllText(stagedFilePath));
        Assert.Equal(lastWriteTime, Directory.GetLastWriteTimeUtc(stagingPath));
        Assert.False(File.Exists(stagingPath + ".lock"));
    }

    [Theory]
    [InlineData(".Makredo.migration-abandoned")]
    [InlineData(".Makredo.migration-")]
    [InlineData(".Makredo.migration-gggggggggggggggggggggggggggggggg")]
    [InlineData(".Makredo.migration-123456781234123412341234567890ab-extra")]
    [InlineData(".OtherProduct.migration-123456781234123412341234567890ab")]
    public void UnrecognizedStagingNamesAreNeverDeleted(string directoryName)
    {
        using var sandbox = new CleanupSandbox();
        string path = sandbox.CreateStaging(TimeSpan.FromDays(2), directoryName);

        Assert.Equal(
            DataDirectoryMigrationResult.SourceMissing,
            new DataDirectoryMigrator(sandbox.Clock).Migrate(sandbox.SourcePath, sandbox.DestinationPath));

        Assert.True(Directory.Exists(path));
        Assert.Equal("synthetic staged macro", File.ReadAllText(Path.Combine(path, "macros", "nested", "sample.json")));
    }

    [Fact]
    public void ExistingDestinationStillAllowsCleanupWithoutChangingUserData()
    {
        using var sandbox = new CleanupSandbox();
        string sourceFile = CleanupSandbox.WriteText(sandbox.SourcePath, "playback-settings.json", "legacy settings");
        string destinationFile = CleanupSandbox.WriteText(sandbox.DestinationPath, "macros/kept.json", "existing user data");
        DateTime sourceWriteTime = File.GetLastWriteTimeUtc(sourceFile);
        DateTime destinationWriteTime = File.GetLastWriteTimeUtc(destinationFile);
        string stagingPath = sandbox.CreateStaging(TimeSpan.FromDays(2));

        DataDirectoryMigrationResult result = new DataDirectoryMigrator(sandbox.Clock).Migrate(
            sandbox.SourcePath,
            sandbox.DestinationPath);

        Assert.Equal(DataDirectoryMigrationResult.DestinationExists, result);
        Assert.False(Directory.Exists(stagingPath));
        Assert.Equal("legacy settings", File.ReadAllText(sourceFile));
        Assert.Equal(sourceWriteTime, File.GetLastWriteTimeUtc(sourceFile));
        Assert.Equal("existing user data", File.ReadAllText(destinationFile));
        Assert.Equal(destinationWriteTime, File.GetLastWriteTimeUtc(destinationFile));
        Assert.False(File.Exists(Path.Combine(sandbox.DestinationPath, DataDirectoryMigrator.CompletionMarkerFileName)));
        Assert.Equal(destinationFile, Assert.Single(Directory.GetFiles(sandbox.DestinationPath, "*", SearchOption.AllDirectories)));
    }

    [Fact]
    public void ActiveLeaseProtectsEvenStagingOlderThanTwentyFourHours()
    {
        using var sandbox = new CleanupSandbox();
        string stagingPath = sandbox.CreateStaging(TimeSpan.FromHours(25));
        string leasePath = stagingPath + ".lock";
        using var activeLease = new FileStream(leasePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);

        Assert.Equal(
            DataDirectoryMigrationResult.SourceMissing,
            new DataDirectoryMigrator(sandbox.Clock).Migrate(sandbox.SourcePath, sandbox.DestinationPath));

        Assert.True(Directory.Exists(stagingPath));
        Assert.True(File.Exists(leasePath));
        Assert.Equal("synthetic staged macro", File.ReadAllText(Path.Combine(stagingPath, "macros", "nested", "sample.json")));
    }

    [Fact]
    public void SourceWithAStagingLikeNameIsNeverCleanedUp()
    {
        using var sandbox = new CleanupSandbox();
        string sourcePath = sandbox.CreateStaging(TimeSpan.FromDays(2));
        string sourceFile = CleanupSandbox.WriteText(sourcePath, "playback-settings.json", "source must remain intact");
        Directory.SetLastWriteTimeUtc(sourcePath, sandbox.Clock.GetUtcNow().AddDays(-2).UtcDateTime);
        DateTime sourceWriteTime = File.GetLastWriteTimeUtc(sourceFile);

        DataDirectoryMigrationResult result = new DataDirectoryMigrator(sandbox.Clock).Migrate(sourcePath, sandbox.DestinationPath);

        Assert.Equal(DataDirectoryMigrationResult.Migrated, result);
        Assert.True(Directory.Exists(sourcePath));
        Assert.Equal("source must remain intact", File.ReadAllText(sourceFile));
        Assert.Equal(sourceWriteTime, File.GetLastWriteTimeUtc(sourceFile));
        Assert.Equal("source must remain intact", File.ReadAllText(Path.Combine(sandbox.DestinationPath, "playback-settings.json")));
    }

    [Fact]
    public void CaseVariantSourceWithAStagingLikeNameIsNeverCleanedUp()
    {
        using var sandbox = new CleanupSandbox();
        string sourcePath = sandbox.CreateStaging(TimeSpan.FromDays(2));
        string sourceFile = CleanupSandbox.WriteText(sourcePath, "playback-settings.json", "case-sensitive source protection");
        Directory.SetLastWriteTimeUtc(sourcePath, sandbox.Clock.GetUtcNow().AddDays(-2).UtcDateTime);
        string caseVariantSourcePath = Path.Combine(sandbox.RootPath, Path.GetFileName(sourcePath).ToUpperInvariant());
        bool sameDirectoryExists = Directory.Exists(caseVariantSourcePath);

        DataDirectoryMigrationResult result = new DataDirectoryMigrator(sandbox.Clock).Migrate(
            caseVariantSourcePath,
            sandbox.DestinationPath);

        Assert.Equal(
            sameDirectoryExists ? DataDirectoryMigrationResult.Migrated : DataDirectoryMigrationResult.SourceMissing,
            result);
        Assert.True(Directory.Exists(sourcePath));
        Assert.Equal("case-sensitive source protection", File.ReadAllText(sourceFile));
    }

    [Fact]
    public void ExistingDestinationSkipsCleanupWhenLegacyRootIsNotADirectory()
    {
        using var sandbox = new CleanupSandbox();
        File.WriteAllText(sandbox.SourcePath, "legacy path is a file");
        string destinationFile = CleanupSandbox.WriteText(sandbox.DestinationPath, "macros/kept.json", "existing data");
        string stagingPath = sandbox.CreateStaging(TimeSpan.FromDays(2));

        DataDirectoryMigrationResult result = new DataDirectoryMigrator(sandbox.Clock).Migrate(
            sandbox.SourcePath,
            sandbox.DestinationPath);

        Assert.Equal(DataDirectoryMigrationResult.DestinationExists, result);
        Assert.True(Directory.Exists(stagingPath));
        Assert.Equal("legacy path is a file", File.ReadAllText(sandbox.SourcePath));
        Assert.Equal("existing data", File.ReadAllText(destinationFile));
    }

    [UnixTheory]
    [InlineData("source")]
    [InlineData("destination")]
    [InlineData("source-with-existing-destination")]
    public void LinkedSourceOrDestinationRootsPreserveTheirOldStagingTargets(string linkKind)
    {
        using var sandbox = new CleanupSandbox();
        string linkedTargetPath = sandbox.CreateStaging(TimeSpan.FromDays(2));
        string markerPath = CleanupSandbox.WriteText(
            linkedTargetPath,
            DataDirectoryMigrator.CompletionMarkerFileName,
            "existing marker must remain intact");
        Directory.SetLastWriteTimeUtc(linkedTargetPath, sandbox.Clock.GetUtcNow().AddDays(-2).UtcDateTime);
        string otherStalePath = sandbox.CreateStaging(TimeSpan.FromDays(2));

        if (linkKind == "destination")
        {
            CleanupSandbox.WriteText(sandbox.SourcePath, "playback-settings.json", "legacy settings");
            Directory.CreateSymbolicLink(sandbox.DestinationPath, linkedTargetPath);

            Assert.Equal(
                DataDirectoryMigrationResult.DestinationExists,
                new DataDirectoryMigrator(sandbox.Clock).Migrate(sandbox.SourcePath, sandbox.DestinationPath));
        }
        else
        {
            Directory.CreateSymbolicLink(sandbox.SourcePath, linkedTargetPath);
            if (linkKind == "source-with-existing-destination")
            {
                string destinationFile = CleanupSandbox.WriteText(sandbox.DestinationPath, "macros/kept.json", "existing data");
                Assert.Equal(
                    DataDirectoryMigrationResult.DestinationExists,
                    new DataDirectoryMigrator(sandbox.Clock).Migrate(sandbox.SourcePath, sandbox.DestinationPath));
                Assert.Equal("existing data", File.ReadAllText(destinationFile));
                Assert.False(File.Exists(Path.Combine(sandbox.DestinationPath, DataDirectoryMigrator.CompletionMarkerFileName)));
            }
            else
            {
                Assert.Throws<IOException>(() => new DataDirectoryMigrator(sandbox.Clock).Migrate(
                    sandbox.SourcePath,
                    sandbox.DestinationPath));
                Assert.False(Directory.Exists(sandbox.DestinationPath));
            }
        }

        Assert.True(Directory.Exists(linkedTargetPath));
        Assert.True(Directory.Exists(otherStalePath));
        Assert.Equal("existing marker must remain intact", File.ReadAllText(markerPath));
        Assert.Equal(
            "synthetic staged macro",
            File.ReadAllText(Path.Combine(linkedTargetPath, "macros", "nested", "sample.json")));
    }

    [UnixTheory]
    [InlineData("source-macros")]
    [InlineData("source-logs")]
    [InlineData("destination-macros")]
    public void ProtectedChildLinksPreventCleanupOfReferencedStagingData(string linkKind)
    {
        using var sandbox = new CleanupSandbox();
        string linkedTargetPath = sandbox.CreateStaging(TimeSpan.FromDays(2));
        string otherStalePath = sandbox.CreateStaging(TimeSpan.FromDays(2));
        string sourceFile = CleanupSandbox.WriteText(sandbox.SourcePath, "playback-settings.json", "legacy settings");

        if (linkKind == "source-macros")
        {
            Directory.CreateSymbolicLink(Path.Combine(sandbox.SourcePath, "macros"), linkedTargetPath);

            Assert.Throws<IOException>(() => new DataDirectoryMigrator(sandbox.Clock).Migrate(
                sandbox.SourcePath,
                sandbox.DestinationPath));
            Assert.False(Directory.Exists(sandbox.DestinationPath));
        }
        else if (linkKind == "source-logs")
        {
            CleanupSandbox.WriteText(sandbox.SourcePath, "macros/kept.json", "legacy macro");
            Directory.CreateSymbolicLink(Path.Combine(sandbox.SourcePath, "logs"), linkedTargetPath);

            Assert.Equal(
                DataDirectoryMigrationResult.Migrated,
                new DataDirectoryMigrator(sandbox.Clock).Migrate(sandbox.SourcePath, sandbox.DestinationPath));
            Assert.Equal("legacy macro", File.ReadAllText(Path.Combine(sandbox.DestinationPath, "macros", "kept.json")));
            Assert.False(Directory.Exists(Path.Combine(sandbox.DestinationPath, "logs")));
        }
        else
        {
            Directory.CreateDirectory(sandbox.DestinationPath);
            Directory.CreateSymbolicLink(Path.Combine(sandbox.DestinationPath, "macros"), linkedTargetPath);

            Assert.Equal(
                DataDirectoryMigrationResult.DestinationExists,
                new DataDirectoryMigrator(sandbox.Clock).Migrate(sandbox.SourcePath, sandbox.DestinationPath));
            Assert.False(File.Exists(Path.Combine(sandbox.DestinationPath, DataDirectoryMigrator.CompletionMarkerFileName)));
        }

        Assert.True(Directory.Exists(linkedTargetPath));
        Assert.True(Directory.Exists(otherStalePath));
        Assert.Equal("legacy settings", File.ReadAllText(sourceFile));
        Assert.Equal(
            "synthetic staged macro",
            File.ReadAllText(Path.Combine(linkedTargetPath, "macros", "nested", "sample.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnAttemptLeaseIsRemovedAfterSuccessOrFailure(bool failCopy)
    {
        using var sandbox = new CleanupSandbox();
        CleanupSandbox.WriteText(sandbox.SourcePath, "playback-settings.json", "legacy settings");
        var migrator = new DataDirectoryMigrator((source, destination, token) =>
        {
            token.ThrowIfCancellationRequested();
            if (failCopy)
            {
                File.WriteAllText(destination, "partial copied bytes");
                throw new IOException("Synthetic write failure.");
            }

            File.Copy(source, destination);
        }, sandbox.Clock);

        if (failCopy)
        {
            Assert.Throws<IOException>(() => migrator.Migrate(sandbox.SourcePath, sandbox.DestinationPath));
            Assert.False(Directory.Exists(sandbox.DestinationPath));
        }
        else
        {
            Assert.Equal(DataDirectoryMigrationResult.Migrated, migrator.Migrate(sandbox.SourcePath, sandbox.DestinationPath));
        }

        Assert.Empty(Directory.GetFiles(sandbox.RootPath, "*.lock", SearchOption.TopDirectoryOnly));
        Assert.DoesNotContain(
            Directory.GetDirectories(sandbox.RootPath),
            path => Path.GetFileName(path).StartsWith(".Makredo.migration-", StringComparison.Ordinal));
        Assert.Equal("legacy settings", File.ReadAllText(Path.Combine(sandbox.SourcePath, "playback-settings.json")));
    }

    [Fact]
    public void MissingParentIsNotCreatedByCleanup()
    {
        using var sandbox = new CleanupSandbox();
        string missingParent = Path.Combine(sandbox.RootPath, "missing-parent");

        DataDirectoryMigrationResult result = new DataDirectoryMigrator(sandbox.Clock).Migrate(
            Path.Combine(missingParent, "Polly"),
            Path.Combine(missingParent, "Makredo"));

        Assert.Equal(DataDirectoryMigrationResult.SourceMissing, result);
        Assert.False(Directory.Exists(missingParent));
        Assert.Empty(Directory.EnumerateFileSystemEntries(sandbox.RootPath));
    }

    [UnixTheory]
    [InlineData("directory")]
    [InlineData("lease")]
    public void StagingOrLeaseSymbolicLinksDoNotExposeExternalDataToCleanup(string linkKind)
    {
        using var sandbox = new CleanupSandbox();
        string externalRoot = Path.Combine(sandbox.RootPath, "external");
        string externalFile = CleanupSandbox.WriteText(externalRoot, "preserved.json", "outside staging");
        DateTime externalWriteTime = File.GetLastWriteTimeUtc(externalFile);
        string stagingPath;
        if (linkKind == "directory")
        {
            Directory.SetLastWriteTimeUtc(externalRoot, sandbox.Clock.GetUtcNow().AddDays(-2).UtcDateTime);
            stagingPath = Path.Combine(sandbox.RootPath, ".Makredo.migration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateSymbolicLink(stagingPath, externalRoot);
        }
        else
        {
            stagingPath = sandbox.CreateStaging(TimeSpan.FromDays(2));
            File.CreateSymbolicLink(stagingPath + ".lock", externalFile);
        }

        Assert.Equal(
            DataDirectoryMigrationResult.SourceMissing,
            new DataDirectoryMigrator(sandbox.Clock).Migrate(sandbox.SourcePath, sandbox.DestinationPath));

        Assert.True(Directory.Exists(stagingPath));
        Assert.Equal("outside staging", File.ReadAllText(externalFile));
        Assert.Equal(externalWriteTime, File.GetLastWriteTimeUtc(externalFile));
        if (linkKind == "lease")
        {
            Assert.Equal("outside staging", File.ReadAllText(stagingPath + ".lock"));
        }
    }

    [UnixTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void CleanupDoesNotFollowLinksInsideAnAbandonedDirectory(bool linkedDirectory)
    {
        using var sandbox = new CleanupSandbox();
        string externalRoot = Path.Combine(sandbox.RootPath, "external");
        string externalFile = CleanupSandbox.WriteText(externalRoot, "preserved.json", "outside staging");
        DateTime externalWriteTime = File.GetLastWriteTimeUtc(externalFile);
        string stagingPath = sandbox.CreateStaging(TimeSpan.FromDays(2));
        string linkPath = Path.Combine(stagingPath, "external-link");
        if (linkedDirectory)
        {
            Directory.CreateSymbolicLink(linkPath, externalRoot);
        }
        else
        {
            File.CreateSymbolicLink(linkPath, externalFile);
        }

        Directory.SetLastWriteTimeUtc(stagingPath, sandbox.Clock.GetUtcNow().AddDays(-2).UtcDateTime);

        Assert.Equal(
            DataDirectoryMigrationResult.SourceMissing,
            new DataDirectoryMigrator(sandbox.Clock).Migrate(sandbox.SourcePath, sandbox.DestinationPath));

        Assert.False(Directory.Exists(stagingPath));
        Assert.Equal("outside staging", File.ReadAllText(externalFile));
        Assert.Equal(externalWriteTime, File.GetLastWriteTimeUtc(externalFile));
    }

    private sealed class CleanupSandbox : IDisposable
    {
        public CleanupSandbox()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "Makredo.StagingCleanup.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public string SourcePath => Path.Combine(RootPath, "Polly");

        public string DestinationPath => Path.Combine(RootPath, "Makredo");

        public FakeTimeProvider Clock { get; } = new();

        public string CreateStaging(TimeSpan age, string? directoryName = null)
        {
            string path = Path.Combine(RootPath, directoryName ?? ".Makredo.migration-" + Guid.NewGuid().ToString("N"));
            WriteText(path, "macros/nested/sample.json", "synthetic staged macro");
            Directory.SetLastWriteTimeUtc(path, Clock.GetUtcNow().Subtract(age).UtcDateTime);
            return path;
        }

        public static string WriteText(string rootPath, string relativePath, string contents)
        {
            string path = Path.Combine(rootPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
            File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc));
            return path;
        }

        public void Dispose()
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2030, 1, 10, 12, 0, 0, TimeSpan.Zero);
    }
}
