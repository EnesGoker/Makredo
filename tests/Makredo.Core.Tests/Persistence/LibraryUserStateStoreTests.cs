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

namespace Makredo.Core.Tests.Persistence;

public sealed class LibraryUserStateStoreTests
{
    [Fact(DisplayName = "JsonMacroLibraryUserStateStore round-trips normalized state")]
    public async Task JsonMacroLibraryUserStateStore_RoundTripsNormalizedState()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "library-state.json");
            string favoritePath = Path.Combine(directoryPath, "Favorite.json");
            string recentPath = Path.Combine(directoryPath, "Recent.json");
            var stateStore = new JsonMacroLibraryUserStateStore(filePath);
            var expectedState = new MacroLibraryUserState
            {
                FavoriteFilePaths =
                [
                    favoritePath,
                    favoritePath.ToUpperInvariant()
                ],
                LastUsedUtcByFilePath = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase)
                {
                    [recentPath] = new DateTime(2026, 5, 6, 9, 30, 0, DateTimeKind.Utc),
                    [Path.Combine(directoryPath, "ignored.json")] = new DateTime(2026, 5, 5, 9, 30, 0, DateTimeKind.Local)
                }
            };

            await stateStore.SaveAsync(expectedState);
            MacroLibraryUserState loadedState = await stateStore.LoadAsync();

            Assert.Single(loadedState.FavoriteFilePaths);
            Assert.Equal(
                Path.GetFullPath(favoritePath),
                loadedState.FavoriteFilePaths[0]);
            Assert.True(
                loadedState.LastUsedUtcByFilePath.ContainsKey(Path.GetFullPath(recentPath)),
                "Recent macro usage should round-trip.");
            Assert.Equal(
                DateTimeKind.Utc,
                loadedState.LastUsedUtcByFilePath[Path.GetFullPath(recentPath)].Kind);
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }
}
