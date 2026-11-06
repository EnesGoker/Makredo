using System.Text.Json.Nodes;
using Makredo.Application.Abstractions;
using Makredo.Application.Hotkeys;
using Makredo.Core.Tests.Support;
using Makredo.Infrastructure.Migration;
using Makredo.Infrastructure.Persistence;
using Xunit;
using static Makredo.Core.Tests.Support.TestData;

namespace Makredo.Core.Tests.Hotkeys;

public sealed class HotkeyMigrationTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task MigrationPreservesCustomSettingsAndRunsOnce(bool pending, bool custom)
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = Path.Combine(directory, "hotkey-settings.json");
            string marker = Path.Combine(directory, DataDirectoryMigrator.CompletionMarkerFileName);
            var store = new JsonHotkeySettingsStore(path);
            var original = HotkeyDefaultsMigration.LegacyPreset();
            if (custom) { original.RecordToggleHotkey = HotkeyBinding.None(0x70); }
            await store.SaveAsync(original);
            string before = await File.ReadAllTextAsync(path);
            string markerBefore = new JsonObject { ["hotkeyMigrationPending"] = pending, ["source"] = "Polly", ["unknown"] = 42 }.ToJsonString();
            await File.WriteAllTextAsync(marker, markerBefore);
            var loaded = await store.LoadAsync();
            bool upgrade = pending && !custom;
            Assert.Equal(upgrade ? HotkeySettings.DefaultRecordToggleHotkey : original.RecordToggleHotkey, loaded.RecordToggleHotkey);
            Assert.Equal(upgrade ? HotkeySettings.DefaultPlaybackToggleHotkey : original.PlaybackToggleHotkey, loaded.PlaybackToggleHotkey);
            JsonObject result = JsonNode.Parse(await File.ReadAllTextAsync(marker))!.AsObject();
            Assert.False(result["hotkeyMigrationPending"]!.GetValue<bool>());
            Assert.Equal(42, result["unknown"]!.GetValue<int>());
            if (!upgrade) { Assert.Equal(before, await File.ReadAllTextAsync(path)); }
            if (pending)
            {
                Assert.Equal(before, await File.ReadAllTextAsync(path + ".pre-t15.bak"));
                Assert.Equal(markerBefore, await File.ReadAllTextAsync(marker + ".pre-t15.bak"));
            }
            else { Assert.False(File.Exists(path + ".pre-t15.bak")); }
            await store.SaveAsync(original);
            Assert.Equal(original.RecordToggleHotkey, (await store.LoadAsync()).RecordToggleHotkey);
        }
        finally { DeleteDirectoryIfExists(directory); }
    }

    [Fact]
    public async Task FailedBackupLeavesFlagAndSettingsUntouchedAndNextLoadRetries()
    {
        string directory = CreateTempDirectory();
        try
        {
            string path = Path.Combine(directory, "hotkeys.json");
            string marker = Path.Combine(directory, DataDirectoryMigrator.CompletionMarkerFileName);
            var logger = new RecordingTestLogger();
            var store = new JsonHotkeySettingsStore(path, logger);
            await store.SaveAsync(HotkeyDefaultsMigration.LegacyPreset());
            string before = await File.ReadAllTextAsync(path);
            await File.WriteAllTextAsync(marker, "{\"hotkeyMigrationPending\":true}");
            Directory.CreateDirectory(path + ".pre-t15.bak");
            Assert.Equal(HotkeyBinding.None(0x77), (await store.LoadAsync()).RecordToggleHotkey);
            Assert.Equal(before, await File.ReadAllTextAsync(path));
            Assert.True(JsonNode.Parse(await File.ReadAllTextAsync(marker))!["hotkeyMigrationPending"]!.GetValue<bool>());
            Assert.Contains(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning);
            Directory.Delete(path + ".pre-t15.bak");
            Assert.Equal(HotkeySettings.DefaultRecordToggleHotkey, (await store.LoadAsync()).RecordToggleHotkey);
            Assert.False(JsonNode.Parse(await File.ReadAllTextAsync(marker))!["hotkeyMigrationPending"]!.GetValue<bool>());
        }
        finally { DeleteDirectoryIfExists(directory); }
    }

    [Fact]
    public async Task MissingMarkerDoesNotMigrateExistingLegacySettings()
    {
        string directory = CreateTempDirectory();
        try
        {
            var store = new JsonHotkeySettingsStore(Path.Combine(directory, "hotkeys.json"));
            await store.SaveAsync(HotkeyDefaultsMigration.LegacyPreset());
            Assert.Equal(HotkeyBinding.None(0x77), (await store.LoadAsync()).RecordToggleHotkey);
        }
        finally { DeleteDirectoryIfExists(directory); }
    }
}
