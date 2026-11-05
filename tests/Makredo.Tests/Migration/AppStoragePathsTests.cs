using System;
using System.IO;
using Makredo.WinForms.Composition;
using Xunit;

namespace Makredo.Tests.Migration;

public sealed class AppStoragePathsTests
{
    [Fact]
    public void CreateDefault_PreservesLegacyDataLocationUntilMigration()
    {
        string expectedRootPath = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Polly"));

        AppStoragePaths paths = AppStoragePaths.CreateDefault();

        Assert.Equal(expectedRootPath, paths.RootDirectoryPath);
        Assert.Equal(Path.Combine(expectedRootPath, "macros"), paths.MacroLibraryDirectoryPath);
        Assert.Equal(Path.Combine(expectedRootPath, "logs"), paths.LogDirectoryPath);
    }
}
