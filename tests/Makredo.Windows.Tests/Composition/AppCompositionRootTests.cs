using System;
using System.IO;
using System.Threading.Tasks;
using Makredo.Application.Abstractions;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Makredo.Infrastructure.Persistence;
using Makredo.WinForms.Composition;
using Xunit;

namespace Makredo.Windows.Tests;

public sealed class AppCompositionRootTests
{
    [Fact(DisplayName = "AppCompositionRoot honors injected app storage paths")]
    public async Task AppCompositionRoot_UsesInjectedStoragePaths()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            AppStoragePaths storagePaths = AppStoragePaths.FromRootDirectory(directoryPath);

            using AppCompositionRoot compositionRoot = AppCompositionRoot.Create(storagePaths);

            await compositionRoot.PlaybackSettingsStore.SaveAsync(
                new PlaybackSettings
                {
                    SpeedMultiplier = 2.0,
                    RepeatCount = 2,
                    PreserveOriginalTiming = false
                });

            await compositionRoot.HotkeySettingsStore.SaveAsync(
                new HotkeySettings
                {
                    RecordToggleHotkey = new HotkeyBinding(0x70, HotkeyModifiers.Control),
                    PlaybackToggleHotkey = new HotkeyBinding(0x71, HotkeyModifiers.Shift),
                    StopHotkey = new HotkeyBinding(0x72, HotkeyModifiers.Alt)
                });
            await compositionRoot.MacroLibraryUserStateStore.SaveAsync(
                new MacroLibraryUserState
                {
                    FavoriteFilePaths = [Path.Combine(directoryPath, "macros", "favorite.json")]
                });

            Assert.True(
                File.Exists(storagePaths.PlaybackSettingsFilePath),
                "Playback settings should be persisted under the injected app storage root.");
            Assert.True(
                File.Exists(storagePaths.HotkeySettingsFilePath),
                "Hotkey settings should be persisted under the injected app storage root.");
            Assert.True(
                File.Exists(storagePaths.MacroLibraryStateFilePath),
                "Macro library state should be persisted under the injected app storage root.");
            Assert.True(
                storagePaths.LogDirectoryPath.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase),
                "Derived log directory should remain within the injected app storage root.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "Makredo.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
