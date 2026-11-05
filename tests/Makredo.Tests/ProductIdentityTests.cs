using System;
using System.IO;
using System.Threading.Tasks;
using Makredo.Application.Abstractions;
using Makredo.Infrastructure.Diagnostics;
using Xunit;

namespace Makredo.Tests;

public sealed class ProductIdentityTests
{
    [Fact]
    public async Task FileLogger_UsesProductNameForDailyLogFile()
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "Makredo.Tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            using (var logger = new FileLogger(directoryPath))
            {
                logger.Log(AppLogLevel.Information, "IdentityTest", "Identity regression entry");
            }

            string logFilePath = Assert.Single(Directory.GetFiles(directoryPath));
            Assert.Matches(@"^makredo-\d{8}\.log$", Path.GetFileName(logFilePath));
            string contents = await File.ReadAllTextAsync(logFilePath);
            Assert.Contains("Identity regression entry", contents, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
    }
}
