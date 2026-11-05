using Makredo.Application.Abstractions;
using Makredo.Application.Filters;
using Makredo.Application.Reporting;
using Makredo.Application.Services;
using Makredo.Core.Tests.Support;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Makredo.Infrastructure.Diagnostics;
using Makredo.Infrastructure.Persistence;
using Xunit;
using static Makredo.Core.Tests.Support.TestData;

namespace Makredo.Core.Tests.Diagnostics;

public sealed class FileLoggerTests
{
    [Fact(DisplayName = "FileLogger writes entries to the daily log file")]
    public async Task FileLogger_WritesEntries()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string logDirectoryPath = Path.Combine(directoryPath, "logs");
            var logger = new FileLogger(logDirectoryPath);
            var sampleException = new InvalidOperationException("Ornek hata ayrintisi");

            logger.Log(AppLogLevel.Information, "TestKaynak", "Bilgi kaydi");
            logger.Log(AppLogLevel.Error, "TestKaynak", "Hata kaydi", sampleException);
            logger.Dispose();

            string[] logFiles = Directory.GetFiles(logDirectoryPath, "makredo-*.log");
            Assert.Single(logFiles);

            string logContents = await File.ReadAllTextAsync(logFiles[0]);
            Assert.True(logContents.Contains("[Bilgi]"), "Log output should include the localized information level.");
            Assert.True(logContents.Contains("Bilgi kaydi"), "Log output should include the information message.");
            Assert.True(logContents.Contains("Hata kaydi"), "Log output should include the error message.");
            Assert.True(logContents.Contains("Ornek hata ayrintisi"), "Log output should include exception details.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }
}
