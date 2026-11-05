using System.Text.Json;
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

public sealed class HotkeySettingsStoreTests
{
    [Fact(DisplayName = "JsonHotkeySettingsStore returns defaults when no file exists")]
    public async Task JsonHotkeySettingsStore_ReturnsDefaultsWhenMissing()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "hotkey-settings.json");
            var hotkeySettingsStore = new JsonHotkeySettingsStore(filePath);

            HotkeySettings settings = await hotkeySettingsStore.LoadAsync();

            Assert.Equal(
                HotkeySettings.DefaultRecordToggleHotkey,
                settings.RecordToggleHotkey);
            Assert.Equal(
                HotkeySettings.DefaultPlaybackToggleHotkey,
                settings.PlaybackToggleHotkey);
            Assert.Equal(
                HotkeySettings.DefaultStopHotkey,
                settings.StopHotkey);
            Assert.Equal(
                HotkeySettings.DefaultHotkeySettingsHotkey,
                settings.HotkeySettingsHotkey);
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact(DisplayName = "JsonHotkeySettingsStore round-trips hotkey settings")]
    public async Task JsonHotkeySettingsStore_RoundTripsSettings()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "hotkey-settings.json");
            var hotkeySettingsStore = new JsonHotkeySettingsStore(filePath);
            var expectedSettings = new HotkeySettings
            {
                RecordToggleHotkey = new HotkeyBinding(0x70, HotkeyModifiers.Control),
                PlaybackToggleHotkey = new HotkeyBinding(0x71, HotkeyModifiers.Control | HotkeyModifiers.Shift),
                StopHotkey = new HotkeyBinding(0x72, HotkeyModifiers.Alt),
                HotkeySettingsHotkey = new HotkeyBinding(0x73, HotkeyModifiers.Control | HotkeyModifiers.Alt)
            };

            await hotkeySettingsStore.SaveAsync(expectedSettings);
            HotkeySettings loadedSettings = await hotkeySettingsStore.LoadAsync();

            Assert.Equal(expectedSettings.RecordToggleHotkey, loadedSettings.RecordToggleHotkey);
            Assert.Equal(expectedSettings.PlaybackToggleHotkey, loadedSettings.PlaybackToggleHotkey);
            Assert.Equal(expectedSettings.StopHotkey, loadedSettings.StopHotkey);
            Assert.Equal(expectedSettings.HotkeySettingsHotkey, loadedSettings.HotkeySettingsHotkey);
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact(DisplayName = "JsonHotkeySettingsStore rejects duplicate bindings")]
    public async Task JsonHotkeySettingsStore_RejectsDuplicateBindings()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "invalid-hotkey-settings.json");

            string payload = JsonSerializer.Serialize(
                new HotkeySettings
                {
                    RecordToggleHotkey = new HotkeyBinding(0x70, HotkeyModifiers.Control),
                    PlaybackToggleHotkey = new HotkeyBinding(0x70, HotkeyModifiers.Control),
                    StopHotkey = new HotkeyBinding(0x72, HotkeyModifiers.Alt)
                });

            await File.WriteAllTextAsync(filePath, payload);

            var hotkeySettingsStore = new JsonHotkeySettingsStore(filePath);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => hotkeySettingsStore.LoadAsync());
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }
}
