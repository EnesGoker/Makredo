using System;
using System.IO;
using System.Linq;
using Makredo.Domain;
using Makredo.WinForms.Composition;
using Makredo.WinForms.Composition.Migration;
using Xunit;
using ApplicationProgram = Makredo.WinForms.Program;

namespace Makredo.Tests.Migration;

public sealed class StartupMigrationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void FailedMigrationReportsOnceAndDoesNotReturnStartupPaths(int failureKind)
    {
        Exception failure = failureKind switch
        {
            0 => new IOException("Sentetik disk dolu hatası."),
            1 => new UnauthorizedAccessException("Sentetik erişim hatası."),
            2 => new OperationCanceledException("Sentetik iptal."),
            _ => new InvalidOperationException("Sentetik göç hatası.")
        };
        int reportCount = 0;
        Exception? reportedException = null;

        bool prepared = ApplicationProgram.TryPrepareStorage(
            () => throw failure,
            exception =>
            {
                reportCount++;
                reportedException = exception;
            },
            out AppStoragePaths? storagePaths);

        Assert.False(prepared);
        Assert.Null(storagePaths);
        Assert.Equal(1, reportCount);
        Assert.Same(failure, reportedException);
    }

    [Fact]
    public void SuccessfulMigrationReturnsPathsWithoutReportingFailure()
    {
        using var sandbox = new StartupSandbox();
        AppStoragePaths expectedPaths = AppStoragePaths.FromRootDirectory(Path.Combine(sandbox.RootPath, ProductInfo.Name));

        bool prepared = ApplicationProgram.TryPrepareStorage(
            () => expectedPaths,
            _ => throw new InvalidOperationException("Successful preparation must not report an error."),
            out AppStoragePaths? storagePaths);

        Assert.True(prepared);
        Assert.Same(expectedPaths, storagePaths);
        Assert.False(Directory.Exists(expectedPaths.RootDirectoryPath));
    }

    [Fact]
    public void FailureMessageExplainsPreservedLegacyDataRetryAndDiagnosticLocation()
    {
        string shownMessage = string.Empty;
        string shownCaption = string.Empty;
        int shownCount = 0;
        const string diagnosticPath = "synthetic-migration-diagnostic.log";
        var failure = new IOException("Dosya başka bir işlem tarafından kullanılıyor.");

        MigrationStartupFailureReporter.Report(
            failure,
            (message, caption) =>
            {
                shownCount++;
                shownMessage = message;
                shownCaption = caption;
            },
            _ => diagnosticPath);

        Assert.Equal(1, shownCount);
        Assert.Equal(ProductInfo.Name, shownCaption);
        Assert.Contains($"Eski {LegacyStoragePaths.DirectoryName} verileri taşınamadı", shownMessage, StringComparison.Ordinal);
        Assert.Contains($"%LOCALAPPDATA%\\{LegacyStoragePaths.DirectoryName} klasöründe dokunulmadan duruyor", shownMessage, StringComparison.Ordinal);
        Assert.Contains($"{LegacyStoragePaths.DirectoryName} açıksa kapatıp tekrar deneyin", shownMessage, StringComparison.Ordinal);
        Assert.Contains("Hata nedeni: " + failure.Message, shownMessage, StringComparison.Ordinal);
        Assert.Contains("Hata ayrıntıları: " + diagnosticPath, shownMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void FailureReasonIsSingleLineAndBoundedWhileDiagnosticsKeepTheFullException()
    {
        using var sandbox = new StartupSandbox();
        string shownMessage = string.Empty;
        string? diagnosticPath = null;
        var failure = new IOException("İlk satır\r\n\t" + new string('x', 600));

        MigrationStartupFailureReporter.Report(
            failure,
            (message, _) => shownMessage = message,
            exception => diagnosticPath = MigrationStartupFailureReporter.TryWriteDiagnosticLog(exception, sandbox.RootPath));

        string reasonLine = shownMessage.Split(Environment.NewLine).Single(line => line.StartsWith("Hata nedeni: ", StringComparison.Ordinal));
        Assert.DoesNotContain('\t', reasonLine);
        Assert.True(reasonLine.Length <= "Hata nedeni: ".Length + 241);
        Assert.EndsWith("…", reasonLine);
        Assert.NotNull(diagnosticPath);
        Assert.Contains(failure.ToString(), File.ReadAllText(diagnosticPath), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagnosticFailureStillShowsTheMigrationMessage(bool writerThrows)
    {
        string shownMessage = string.Empty;
        int shownCount = 0;

        MigrationStartupFailureReporter.Report(
            new UnauthorizedAccessException("Erişim reddedildi."),
            (message, _) =>
            {
                shownCount++;
                shownMessage = message;
            },
            _ => writerThrows ? throw new IOException("Geçici klasör kullanılamıyor.") : null);

        Assert.Equal(1, shownCount);
        Assert.Contains("verileri taşınamadı", shownMessage, StringComparison.Ordinal);
        Assert.Contains("Erişim reddedildi.", shownMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Hata ayrıntıları:", shownMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void TemporaryDiagnosticsUseUniqueFilesAndContainTheCompleteException()
    {
        using var sandbox = new StartupSandbox();
        var failure = new IOException("Sentetik kopyalama hatası.", new UnauthorizedAccessException("Sentetik iç neden."));

        string? firstPath = MigrationStartupFailureReporter.TryWriteDiagnosticLog(failure, sandbox.RootPath);
        string? secondPath = MigrationStartupFailureReporter.TryWriteDiagnosticLog(failure, sandbox.RootPath);

        Assert.NotNull(firstPath);
        Assert.NotNull(secondPath);
        Assert.NotEqual(firstPath, secondPath);
        Assert.Equal(2, Directory.GetFiles(sandbox.RootPath).Length);
        Assert.Equal(sandbox.RootPath, Path.GetDirectoryName(firstPath));
        Assert.StartsWith(ProductInfo.Name.ToLowerInvariant() + "-migration-", Path.GetFileName(firstPath));
        Assert.Equal(failure.ToString(), File.ReadAllText(firstPath));
        Assert.Equal(failure.ToString(), File.ReadAllText(secondPath));
    }

    [Fact]
    public void UnwritableTemporaryLocationDoesNotThrowOrCreateAnApplicationDirectory()
    {
        using var sandbox = new StartupSandbox();
        string blockedDirectoryPath = Path.Combine(sandbox.RootPath, "blocked");
        File.WriteAllText(blockedDirectoryPath, "synthetic path collision");

        string? diagnosticPath = MigrationStartupFailureReporter.TryWriteDiagnosticLog(
            new IOException("Sentetik hata."),
            blockedDirectoryPath);

        Assert.Null(diagnosticPath);
        Assert.Equal("synthetic path collision", File.ReadAllText(blockedDirectoryPath));
        Assert.Empty(Directory.GetDirectories(sandbox.RootPath));
    }

    private sealed class StartupSandbox : IDisposable
    {
        public StartupSandbox()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "Makredo.Startup.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void Dispose()
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
