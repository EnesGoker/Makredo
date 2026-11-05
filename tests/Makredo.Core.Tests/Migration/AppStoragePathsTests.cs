using System;
using System.IO;
using Makredo.Domain;
using Makredo.Infrastructure.Persistence;
using Xunit;

namespace Makredo.Core.Tests.Migration;

public sealed class AppStoragePathsTests
{
    [Fact]
    public void CreateDefault_UsesProductDataDirectory()
    {
        string expectedRootPath = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductInfo.Name));

        AppStoragePaths paths = AppStoragePaths.CreateDefault();

        Assert.Equal(expectedRootPath, paths.RootDirectoryPath);
        Assert.Equal(Path.Combine(expectedRootPath, "macros"), paths.MacroLibraryDirectoryPath);
        Assert.Equal(Path.Combine(expectedRootPath, "logs"), paths.LogDirectoryPath);
    }
}
