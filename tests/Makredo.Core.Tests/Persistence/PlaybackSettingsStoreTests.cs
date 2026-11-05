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

public sealed class PlaybackSettingsStoreTests
{
    [Fact(DisplayName = "JsonPlaybackSettingsStore returns defaults when no file exists")]
    public async Task JsonPlaybackSettingsStore_ReturnsDefaultsWhenMissing()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "playback-settings.json");
            var settingsStore = new JsonPlaybackSettingsStore(filePath);

            PlaybackSettings settings = await settingsStore.LoadAsync();

            Assert.Equal(1.0, settings.SpeedMultiplier);
            Assert.Equal(1, settings.RepeatCount);
            Assert.Equal(0, settings.InitialDelayMs);
            Assert.False(settings.LoopIndefinitely, "Missing settings files should not enable looping by default.");
            Assert.False(settings.UseRelativeCoordinates, "Missing settings files should not enable relative coordinates by default.");
            Assert.True(settings.StopOnError, "Missing settings files should preserve the default stop-on-error behavior.");
            Assert.True(settings.PreserveOriginalTiming, "Missing settings files should preserve original timing by default.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact(DisplayName = "JsonPlaybackSettingsStore round-trips playback settings")]
    public async Task JsonPlaybackSettingsStore_RoundTripsSettings()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "playback-settings.json");
            var settingsStore = new JsonPlaybackSettingsStore(filePath);
            var expectedSettings = new PlaybackSettings
            {
                SpeedMultiplier = 1.75,
                RepeatCount = 3,
                InitialDelayMs = 500,
                LoopIndefinitely = true,
                UseRelativeCoordinates = true,
                SimulationMode = true,
                StopOnError = false,
                PreserveOriginalTiming = false
            };

            await settingsStore.SaveAsync(expectedSettings);
            PlaybackSettings loadedSettings = await settingsStore.LoadAsync();

            Assert.Equal(expectedSettings.SpeedMultiplier, loadedSettings.SpeedMultiplier);
            Assert.Equal(expectedSettings.RepeatCount, loadedSettings.RepeatCount);
            Assert.Equal(expectedSettings.InitialDelayMs, loadedSettings.InitialDelayMs);
            Assert.Equal(expectedSettings.LoopIndefinitely, loadedSettings.LoopIndefinitely);
            Assert.Equal(expectedSettings.UseRelativeCoordinates, loadedSettings.UseRelativeCoordinates);
            Assert.Equal(expectedSettings.SimulationMode, loadedSettings.SimulationMode);
            Assert.Equal(expectedSettings.StopOnError, loadedSettings.StopOnError);
            Assert.Equal(expectedSettings.PreserveOriginalTiming, loadedSettings.PreserveOriginalTiming);
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact(DisplayName = "JsonPlaybackSettingsStore normalizes speed when original timing is preserved")]
    public async Task JsonPlaybackSettingsStore_NormalizesPreservedTiming()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "normalized-playback-settings.json");
            var settingsStore = new JsonPlaybackSettingsStore(filePath);
            var settings = new PlaybackSettings
            {
                SpeedMultiplier = 2.5,
                RepeatCount = 2,
                InitialDelayMs = 150,
                LoopIndefinitely = false,
                UseRelativeCoordinates = false,
                StopOnError = true,
                PreserveOriginalTiming = true
            };

            await settingsStore.SaveAsync(settings);
            PlaybackSettings loadedSettings = await settingsStore.LoadAsync();

            Assert.Equal(1.0, loadedSettings.SpeedMultiplier);
            Assert.True(loadedSettings.PreserveOriginalTiming, "PreserveOriginalTiming should remain enabled after normalization.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    [Fact(DisplayName = "JsonPlaybackSettingsStore rejects invalid persisted settings")]
    public async Task JsonPlaybackSettingsStore_RejectsInvalidSettings()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "invalid-playback-settings.json");

            string payload = JsonSerializer.Serialize(
                new
                {
                    SpeedMultiplier = 0,
                    RepeatCount = 0,
                    InitialDelayMs = -1,
                    LoopIndefinitely = false,
                    UseRelativeCoordinates = false,
                    StopOnError = true,
                    PreserveOriginalTiming = true
                });

            await File.WriteAllTextAsync(filePath, payload);

            var settingsStore = new JsonPlaybackSettingsStore(filePath);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => settingsStore.LoadAsync());
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }
}
