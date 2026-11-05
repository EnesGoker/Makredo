using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MacroMaster.Application.Abstractions;
using MacroMaster.Application.Services;
using MacroMaster.Domain.Enums;
using MacroMaster.Domain.Models;
using MacroMaster.Infrastructure.Diagnostics;
using MacroMaster.Infrastructure.Hooks;
using MacroMaster.Infrastructure.Persistence;
using MacroMaster.WinForms;
using MacroMaster.WinForms.Composition;
using MacroMaster.WinForms.Controls;
using MacroMaster.WinForms.Forms;
using MacroMaster.WinForms.Reporting;
using MacroMaster.WinForms.Theme;
using Xunit;
using Assert = global::MacroMaster.Tests.Expect;

namespace MacroMaster.Tests;

public sealed class MacroMasterTests
{
    [Fact(DisplayName = "ApplicationStateService allows valid transitions")]
    public Task ApplicationStateService_AllowsValidTransitions() => ApplicationStateService_AllowsValidTransitionsAsync();

    [Fact(DisplayName = "ApplicationStateService rejects invalid transitions")]
    public Task ApplicationStateService_RejectsInvalidTransitions() => ApplicationStateService_RejectsInvalidTransitionsAsync();

    [Fact(DisplayName = "JsonMacroStorageService round-trips a valid session")]
    public Task JsonMacroStorageService_RoundTripsSession() => JsonMacroStorageService_RoundTripsSessionAsync();

    [Fact(DisplayName = "XmlMacroStorageService round-trips a valid session")]
    public Task XmlMacroStorageService_RoundTripsSession() => XmlMacroStorageService_RoundTripsSessionAsync();

    [Fact(DisplayName = "JsonMacroStorageService rejects unsupported format versions")]
    public Task JsonMacroStorageService_RejectsUnsupportedVersion() => JsonMacroStorageService_RejectsUnsupportedVersionAsync();

    [Fact(DisplayName = "JsonMacroStorageService rejects system events during save")]
    public Task JsonMacroStorageService_RejectsSystemEventOnSave() => JsonMacroStorageService_RejectsSystemEventOnSaveAsync();

    [Fact(DisplayName = "JsonMacroStorageService rejects system events during load")]
    public Task JsonMacroStorageService_RejectsSystemEventOnLoad() => JsonMacroStorageService_RejectsSystemEventOnLoadAsync();

    [Fact(DisplayName = "JsonPlaybackSettingsStore returns defaults when no file exists")]
    public Task JsonPlaybackSettingsStore_ReturnsDefaultsWhenMissing() => JsonPlaybackSettingsStore_ReturnsDefaultsWhenMissingAsync();

    [Fact(DisplayName = "JsonPlaybackSettingsStore round-trips playback settings")]
    public Task JsonPlaybackSettingsStore_RoundTripsSettings() => JsonPlaybackSettingsStore_RoundTripsSettingsAsync();

    [Fact(DisplayName = "JsonPlaybackSettingsStore normalizes speed when original timing is preserved")]
    public Task JsonPlaybackSettingsStore_NormalizesPreservedTiming() => JsonPlaybackSettingsStore_NormalizesPreservedTimingAsync();

    [Fact(DisplayName = "JsonPlaybackSettingsStore rejects invalid persisted settings")]
    public Task JsonPlaybackSettingsStore_RejectsInvalidSettings() => JsonPlaybackSettingsStore_RejectsInvalidSettingsAsync();

    [Fact(DisplayName = "JsonHotkeySettingsStore returns defaults when no file exists")]
    public Task JsonHotkeySettingsStore_ReturnsDefaultsWhenMissing() => JsonHotkeySettingsStore_ReturnsDefaultsWhenMissingAsync();

    [Fact(DisplayName = "JsonHotkeySettingsStore round-trips hotkey settings")]
    public Task JsonHotkeySettingsStore_RoundTripsSettings() => JsonHotkeySettingsStore_RoundTripsSettingsAsync();

    [Fact(DisplayName = "JsonHotkeySettingsStore rejects duplicate bindings")]
    public Task JsonHotkeySettingsStore_RejectsDuplicateBindings() => JsonHotkeySettingsStore_RejectsDuplicateBindingsAsync();

    [Fact(DisplayName = "JsonMacroLibraryUserStateStore round-trips normalized state")]
    public Task JsonMacroLibraryUserStateStore_RoundTripsNormalizedState() => JsonMacroLibraryUserStateStore_RoundTripsNormalizedStateAsync();

    [Fact(DisplayName = "MacroLibraryService reads duration metadata")]
    public Task MacroLibraryService_ReadsDurationMetadata() => MacroLibraryService_ReadsDurationMetadataAsync();

    [Fact(DisplayName = "MacroLibraryService imports XML files into the library")]
    public Task MacroLibraryService_ImportsXmlFilesIntoLibrary() => MacroLibraryService_ImportsXmlFilesIntoLibraryAsync();

    [Fact(DisplayName = "MacroLibraryFilterEngine applies search and library filters")]
    public Task MacroLibraryFilterEngine_AppliesSearchAndFilters() => MacroLibraryFilterEngine_AppliesSearchAndFiltersAsync();

    [Fact(DisplayName = "EventListFilterEngine applies search and smart filters")]
    public Task EventListFilterEngine_AppliesSearchAndSmartFilters() => EventListFilterEngine_AppliesSearchAndSmartFiltersAsync();

    [Fact(DisplayName = "EventListFilterEngine identifies optimization candidates and invalid events")]
    public Task EventListFilterEngine_IdentifiesOptimizationCandidatesAndInvalidEvents() => EventListFilterEngine_IdentifiesOptimizationCandidatesAndInvalidEventsAsync();

    [Fact(DisplayName = "EventListFilterEngine supports structured search expressions")]
    public Task EventListFilterEngine_SupportsStructuredSearchExpressions() => EventListFilterEngine_SupportsStructuredSearchExpressionsAsync();

    [Fact(DisplayName = "MacroOptimizationService removes redundant mouse moves and preserves timing")]
    public Task MacroOptimizationService_RemovesRedundantMouseMovesAndPreservesTiming() => MacroOptimizationService_RemovesRedundantMouseMovesAndPreservesTimingAsync();

    [Fact(DisplayName = "MacroOptimizationService preserves protected events")]
    public Task MacroOptimizationService_PreservesProtectedEvents() => MacroOptimizationService_PreservesProtectedEventsAsync();

    [Fact(DisplayName = "MacroOptimizationService keeps balanced mouse path samples")]
    public Task MacroOptimizationService_KeepsBalancedMousePathSamples() => MacroOptimizationService_KeepsBalancedMousePathSamplesAsync();

    [Fact(DisplayName = "MacroReportGenerator summarizes session analysis")]
    public Task MacroReportGenerator_SummarizesSessionAnalysis() => MacroReportGenerator_SummarizesSessionAnalysisAsync();

    [Fact(DisplayName = "MacroReportGenerator escapes HTML report values")]
    public Task MacroReportGenerator_EscapesHtmlReportValues() => MacroReportGenerator_EscapesHtmlReportValuesAsync();

    [Fact(DisplayName = "HotkeySettingsDialog exposes all controls within the dialog bounds")]
    public Task HotkeySettingsDialog_UsesStableLayout() => HotkeySettingsDialog_UsesStableLayoutAsync();

    [Fact(DisplayName = "MacroPreviewMapDialog keeps preview content within popup bounds")]
    public Task MacroPreviewMapDialog_UsesStableLayout() => MacroPreviewMapDialog_UsesStableLayoutAsync();

    [Fact(DisplayName = "MacroPreviewMapDialog clamps its size to the working area")]
    public Task MacroPreviewMapDialog_ClampsSizeToWorkingArea() => MacroPreviewMapDialog_ClampsSizeToWorkingAreaAsync();

    [Fact(DisplayName = "AppShellLayoutProfile keeps expanded layout when the designed shell fits")]
    public Task AppShellLayoutProfile_UsesExpandedLayoutWhenDesignedShellFits() => AppShellLayoutProfile_UsesExpandedLayoutWhenDesignedShellFitsAsync();

    [Fact(DisplayName = "AppShellLayoutProfile constrains startup size on small high-DPI working areas")]
    public Task AppShellLayoutProfile_ConstrainsSmallHighDpiWorkingAreas() => AppShellLayoutProfile_ConstrainsSmallHighDpiWorkingAreasAsync();

    [Fact(DisplayName = "AppShellLayoutProfile keeps toolbar chrome metrics internally consistent")]
    public Task AppShellLayoutProfile_KeepsToolbarMetricsConsistent() => AppShellLayoutProfile_KeepsToolbarMetricsConsistentAsync();

    [Fact(DisplayName = "AppCompositionRoot honors injected app storage paths")]
    public Task AppCompositionRoot_UsesInjectedStoragePaths() => AppCompositionRoot_UsesInjectedStoragePathsAsync();

    [Fact(DisplayName = "FileLogger writes entries to the daily log file")]
    public Task FileLogger_WritesEntries() => FileLogger_WritesEntriesAsync();

    [Fact(DisplayName = "WindowsKeyboardHookSource swallows subscriber exceptions in hook callbacks")]
    public Task WindowsKeyboardHookSource_SwallowsSubscriberExceptionsInCallback() => WindowsKeyboardHookSource_SwallowsSubscriberExceptionsInCallbackAsync();

    [Fact(DisplayName = "WindowsMouseHookSource swallows subscriber exceptions in hook callbacks")]
    public Task WindowsMouseHookSource_SwallowsSubscriberExceptionsInCallback() => WindowsMouseHookSource_SwallowsSubscriberExceptionsInCallbackAsync();

    [Fact(DisplayName = "GlobalExceptionHandlerRegistration logs UI thread exceptions")]
    public Task GlobalExceptionHandlerRegistration_LogsThreadExceptions() => GlobalExceptionHandlerRegistration_LogsThreadExceptionsAsync();

    [Fact(DisplayName = "GlobalExceptionHandlerRegistration logs unobserved task exceptions")]
    public Task GlobalExceptionHandlerRegistration_LogsUnobservedTaskExceptions() => GlobalExceptionHandlerRegistration_LogsUnobservedTaskExceptionsAsync();

    [Fact(DisplayName = "JsonMacroStorageService rejects mouse click events without coordinates")]
    public Task JsonMacroStorageService_RejectsMouseClickWithoutCoordinates() => JsonMacroStorageService_RejectsMouseClickWithoutCoordinatesAsync();

    [Fact(DisplayName = "JsonMacroStorageService rejects invalid event type values")]
    public Task JsonMacroStorageService_RejectsInvalidEventType() => JsonMacroStorageService_RejectsInvalidEventTypeAsync();

    [Fact(DisplayName = "JsonMacroStorageService rejects invalid keyboard action values")]
    public Task JsonMacroStorageService_RejectsInvalidKeyboardAction() => JsonMacroStorageService_RejectsInvalidKeyboardActionAsync();

    [Fact(DisplayName = "JsonMacroStorageService rejects invalid mouse action values")]
    public Task JsonMacroStorageService_RejectsInvalidMouseAction() => JsonMacroStorageService_RejectsInvalidMouseActionAsync();

    [Fact(DisplayName = "MacroRecorderService suppresses modifier leakage for reserved hotkeys")]
    public Task MacroRecorderService_SuppressesReservedHotkeyModifiers() => MacroRecorderService_SuppressesReservedHotkeyModifiersAsync();

    [Fact(DisplayName = "MacroRecorderService preserves legitimate modifier combinations")]
    public Task MacroRecorderService_PreservesLegitimateModifierSequences() => MacroRecorderService_PreservesLegitimateModifierSequencesAsync();

    [Fact(DisplayName = "MacroRecorderService collects concurrent input safely")]
    public Task MacroRecorderService_ConcurrentInput_CollectsEvents() => MacroRecorderService_ConcurrentInput_CollectsEventsAsync();

    [Fact(DisplayName = "MacroRecorderService retains the completed session until clear")]
    public Task MacroRecorderService_StopAsync_RetainsCompletedSession() => MacroRecorderService_StopAsync_RetainsCompletedSessionAsync();

    [Fact(DisplayName = "MacroRecorderService clear removes the active session")]
    public Task MacroRecorderService_Clear_RemovesActiveSession() => MacroRecorderService_Clear_RemovesActiveSessionAsync();

    [Fact(DisplayName = "MacroRecorderService clear is rejected while recording")]
    public Task MacroRecorderService_Clear_WhileRecording_Throws() => MacroRecorderService_Clear_WhileRecording_ThrowsAsync();

    [Fact(DisplayName = "MacroPlaybackService rebases mouse coordinates when relative playback is enabled")]
    public Task MacroPlaybackService_UseRelativeCoordinates_RebasesMouseCoordinates() => MacroPlaybackService_UseRelativeCoordinates_RebasesMouseCoordinatesAsync();

    [Fact(DisplayName = "MacroPlaybackService re-anchors relative mouse playback for each repeat iteration")]
    public Task MacroPlaybackService_UseRelativeCoordinates_ReanchorsEachIteration() => MacroPlaybackService_UseRelativeCoordinates_ReanchorsEachIterationAsync();

    [Fact(DisplayName = "MacroPlaybackService does not query cursor anchors when no relative mouse anchor exists")]
    public Task MacroPlaybackService_UseRelativeCoordinates_NoMouseAnchor_DoesNotQueryCursor() => MacroPlaybackService_UseRelativeCoordinates_NoMouseAnchor_DoesNotQueryCursorAsync();

    [Fact(DisplayName = "MacroPlaybackService keeps the active relative anchor for paused single-step playback")]
    public Task MacroPlaybackService_UseRelativeCoordinates_PausedStepUsesActiveAnchor() => MacroPlaybackService_UseRelativeCoordinates_PausedStepUsesActiveAnchorAsync();

    [Fact(DisplayName = "MacroPlaybackService re-anchors paused relative single-step playback across repeat iterations")]
    public Task MacroPlaybackService_UseRelativeCoordinates_PausedStepReanchorsNewIteration() => MacroPlaybackService_UseRelativeCoordinates_PausedStepReanchorsNewIterationAsync();

    [Fact(DisplayName = "MacroPlaybackService preserves absolute mouse coordinates when relative playback is disabled")]
    public Task MacroPlaybackService_UseRelativeCoordinatesFalse_PreservesAbsoluteCoordinates() => MacroPlaybackService_UseRelativeCoordinatesFalse_PreservesAbsoluteCoordinatesAsync();

    [Fact(DisplayName = "MacroPlaybackService simulation mode advances without sending input")]
    public Task MacroPlaybackService_SimulationMode_AdvancesWithoutSendingInput() => MacroPlaybackService_SimulationMode_AdvancesWithoutSendingInputAsync();

    [Fact(DisplayName = "MacroPlaybackService honors repeat count")]
    public Task MacroPlaybackService_RepeatCount_ReplaysEntireSession() => MacroPlaybackService_RepeatCount_ReplaysEntireSessionAsync();

    [Fact(DisplayName = "MacroPlaybackService pauses until resume is requested")]
    public Task MacroPlaybackService_PauseResume_WaitsForResume() => MacroPlaybackService_PauseResume_WaitsForResumeAsync();

    [Fact(DisplayName = "MacroPlaybackService seeks paused playback before resume")]
    public Task MacroPlaybackService_SeekPausedPlayback_ResumesFromCursor() => MacroPlaybackService_SeekPausedPlayback_ResumesFromCursorAsync();

    [Fact(DisplayName = "MacroPlaybackService supports single stepping while paused")]
    public Task MacroPlaybackService_PausedSingleStep_ContinuesAfterSteppedEvent() => MacroPlaybackService_PausedSingleStep_ContinuesAfterSteppedEventAsync();

    [Fact(DisplayName = "MacroPlaybackService does not replay a stale delayed event after paused stepping")]
    public Task MacroPlaybackService_PausedStepDuringDelay_DoesNotReplayStaleEvent() => MacroPlaybackService_PausedStepDuringDelay_DoesNotReplayStaleEventAsync();

    [Fact(DisplayName = "MacroPlaybackService stops immediately on playback errors when StopOnError is enabled")]
    public Task MacroPlaybackService_StopOnErrorTrue_StopsImmediately() => MacroPlaybackService_StopOnErrorTrue_StopsImmediatelyAsync();

    [Fact(DisplayName = "MacroPlaybackService continues and reports failures when StopOnError is disabled")]
    public Task MacroPlaybackService_StopOnErrorFalse_ContinuesAndReports() => MacroPlaybackService_StopOnErrorFalse_ContinuesAndReportsAsync();

    private static Task ApplicationStateService_AllowsValidTransitionsAsync()
    {
        var stateService = new ApplicationStateService();
        List<AppState> observedStates = [];

        stateService.StateChanged += observedStates.Add;

        Assert.Equal(AppState.Idle, stateService.CurrentState, "Initial state should be Idle.");
        Assert.True(stateService.TryTransitionTo(AppState.Recording, AppState.Idle), "Idle -> Recording should succeed.");
        Assert.True(stateService.TryTransitionTo(AppState.Stopping, AppState.Recording), "Recording -> Stopping should succeed.");
        Assert.True(stateService.TryTransitionTo(AppState.Idle, AppState.Stopping), "Stopping -> Idle should succeed.");
        Assert.Equal(
            new[] { AppState.Recording, AppState.Stopping, AppState.Idle },
            observedStates,
            "StateChanged should publish each successful transition.");

        return Task.CompletedTask;
    }

    private static Task ApplicationStateService_RejectsInvalidTransitionsAsync()
    {
        var stateService = new ApplicationStateService();

        Assert.Throws<InvalidOperationException>(
            () => stateService.SetState(AppState.Paused),
            "Idle -> Paused should be rejected.");

        Assert.Equal(AppState.Idle, stateService.CurrentState, "State must remain Idle after a rejected transition.");
        Assert.False(
            stateService.TryTransitionTo(AppState.Playing, AppState.Recording),
            "Allowed current state guard should reject transitions when current state does not match.");

        return Task.CompletedTask;
    }

    private static async Task JsonMacroStorageService_RoundTripsSessionAsync()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "roundtrip.json");
            var storageService = new JsonMacroStorageService();
            var session = new MacroSession
            {
                Name = "RoundTrip",
                Events =
                {
                    new MacroEvent
                    {
                        EventType = MacroEventType.Keyboard,
                        KeyboardActionType = KeyboardActionType.KeyDown,
                        DelayMs = 15,
                        KeyCode = 0x41,
                        ScanCode = 0x1E,
                        IsExtendedKey = false,
                        KeyName = "A",
                        Description = "Keyboard A down"
                    },
                    new MacroEvent
                    {
                        EventType = MacroEventType.Mouse,
                        MouseActionType = MouseActionType.Move,
                        DelayMs = 20,
                        X = 320,
                        Y = 240,
                        Description = "Mouse move"
                    }
                }
            };

            await storageService.SaveAsync(session, filePath);
            MacroSession loadedSession = await storageService.LoadAsync(filePath);

            Assert.Equal(MacroSessionFormat.CurrentVersion, loadedSession.FormatVersion, "Saved session should use the current format version.");
            Assert.Equal(session.Name, loadedSession.Name, "Session name should round-trip.");
            Assert.Equal(2, loadedSession.Events.Count, "Event count should round-trip.");
            Assert.Equal(0x1E, loadedSession.Events[0].ScanCode, "Keyboard scan code should round-trip.");
            Assert.Equal(320, loadedSession.Events[1].X, "Mouse X coordinate should round-trip.");
            Assert.Equal(240, loadedSession.Events[1].Y, "Mouse Y coordinate should round-trip.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task XmlMacroStorageService_RoundTripsSessionAsync()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "roundtrip.xml");
            var storageService = new XmlMacroStorageService();
            var session = new MacroSession
            {
                Name = "XmlRoundTrip",
                Events =
                {
                    new MacroEvent
                    {
                        EventType = MacroEventType.Keyboard,
                        KeyboardActionType = KeyboardActionType.KeyDown,
                        DelayMs = 10,
                        KeyCode = 0x41,
                        ScanCode = 0x1E,
                        Description = "Keyboard A down"
                    },
                    new MacroEvent
                    {
                        EventType = MacroEventType.Mouse,
                        MouseActionType = MouseActionType.Move,
                        DelayMs = 25,
                        X = 640,
                        Y = 360,
                        Description = "Mouse move"
                    }
                }
            };

            await storageService.SaveAsync(session, filePath);
            MacroSession loadedSession = await storageService.LoadAsync(filePath);

            Assert.Equal(session.Name, loadedSession.Name, "XML session name should round-trip.");
            Assert.Equal(2, loadedSession.Events.Count, "XML event count should round-trip.");
            Assert.Equal(0x41, loadedSession.Events[0].KeyCode, "XML keyboard event data should round-trip.");
            Assert.Equal(640, loadedSession.Events[1].X, "XML mouse X coordinate should round-trip.");
            Assert.Equal(360, loadedSession.Events[1].Y, "XML mouse Y coordinate should round-trip.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task JsonMacroStorageService_RejectsUnsupportedVersionAsync()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "unsupported.json");

            string payload = JsonSerializer.Serialize(
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "Unsupported",
                    CreatedAtUtc = DateTime.UtcNow,
                    FormatVersion = "9.9",
                    Events = Array.Empty<object>()
                });

            await File.WriteAllTextAsync(filePath, payload);

            var storageService = new JsonMacroStorageService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.LoadAsync(filePath),
                "Unsupported session versions must be rejected during load.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task JsonMacroStorageService_RejectsSystemEventOnSaveAsync()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "system-event.json");
            var storageService = new JsonMacroStorageService();
            var session = new MacroSession
            {
                Name = "SystemEventSave",
                Events =
                {
                    new MacroEvent
                    {
                        EventType = MacroEventType.System,
                        DelayMs = 5,
                        Description = "System event"
                    }
                }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.SaveAsync(session, filePath),
                "System events must be rejected during save.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task JsonMacroStorageService_RejectsSystemEventOnLoadAsync()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "system-event-load.json");

            string payload = JsonSerializer.Serialize(
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "SystemEventLoad",
                    CreatedAtUtc = DateTime.UtcNow,
                    FormatVersion = MacroSessionFormat.CurrentVersion,
                    Events = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            EventType = MacroEventType.System,
                            DelayMs = 10,
                            Description = "Unsupported system event"
                        }
                    }
                });

            await File.WriteAllTextAsync(filePath, payload);

            var storageService = new JsonMacroStorageService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.LoadAsync(filePath),
                "System events must be rejected during load.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task JsonPlaybackSettingsStore_ReturnsDefaultsWhenMissingAsync()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "playback-settings.json");
            var settingsStore = new JsonPlaybackSettingsStore(filePath);

            PlaybackSettings settings = await settingsStore.LoadAsync();

            Assert.Equal(1.0, settings.SpeedMultiplier, "Missing settings files should fall back to the default speed multiplier.");
            Assert.Equal(1, settings.RepeatCount, "Missing settings files should fall back to the default repeat count.");
            Assert.Equal(0, settings.InitialDelayMs, "Missing settings files should fall back to zero initial delay.");
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

    private static async Task JsonPlaybackSettingsStore_RoundTripsSettingsAsync()
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

            Assert.Equal(expectedSettings.SpeedMultiplier, loadedSettings.SpeedMultiplier, "Playback speed multiplier should round-trip.");
            Assert.Equal(expectedSettings.RepeatCount, loadedSettings.RepeatCount, "Playback repeat count should round-trip.");
            Assert.Equal(expectedSettings.InitialDelayMs, loadedSettings.InitialDelayMs, "Playback initial delay should round-trip.");
            Assert.Equal(expectedSettings.LoopIndefinitely, loadedSettings.LoopIndefinitely, "Loop-indefinitely should round-trip.");
            Assert.Equal(expectedSettings.UseRelativeCoordinates, loadedSettings.UseRelativeCoordinates, "Relative coordinate mode should round-trip.");
            Assert.Equal(expectedSettings.SimulationMode, loadedSettings.SimulationMode, "Simulation mode should round-trip.");
            Assert.Equal(expectedSettings.StopOnError, loadedSettings.StopOnError, "Stop-on-error should round-trip.");
            Assert.Equal(expectedSettings.PreserveOriginalTiming, loadedSettings.PreserveOriginalTiming, "Preserve-original-timing should round-trip.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task JsonPlaybackSettingsStore_NormalizesPreservedTimingAsync()
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

            Assert.Equal(1.0, loadedSettings.SpeedMultiplier, "PreserveOriginalTiming should normalize the persisted speed multiplier to 1.0.");
            Assert.True(loadedSettings.PreserveOriginalTiming, "PreserveOriginalTiming should remain enabled after normalization.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task JsonPlaybackSettingsStore_RejectsInvalidSettingsAsync()
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
                () => settingsStore.LoadAsync(),
                "Invalid playback settings files must be rejected during load.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task JsonHotkeySettingsStore_ReturnsDefaultsWhenMissingAsync()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "hotkey-settings.json");
            var hotkeySettingsStore = new JsonHotkeySettingsStore(filePath);

            HotkeySettings settings = await hotkeySettingsStore.LoadAsync();

            Assert.Equal(
                HotkeySettings.DefaultRecordToggleHotkey,
                settings.RecordToggleHotkey,
                "Missing hotkey settings files should fall back to the default record hotkey.");
            Assert.Equal(
                HotkeySettings.DefaultPlaybackToggleHotkey,
                settings.PlaybackToggleHotkey,
                "Missing hotkey settings files should fall back to the default playback hotkey.");
            Assert.Equal(
                HotkeySettings.DefaultStopHotkey,
                settings.StopHotkey,
                "Missing hotkey settings files should fall back to the default stop hotkey.");
            Assert.Equal(
                HotkeySettings.DefaultHotkeySettingsHotkey,
                settings.HotkeySettingsHotkey,
                "Missing hotkey settings files should fall back to the default hotkey settings shortcut.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task JsonHotkeySettingsStore_RoundTripsSettingsAsync()
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

            Assert.Equal(expectedSettings.RecordToggleHotkey, loadedSettings.RecordToggleHotkey, "Record hotkey should round-trip.");
            Assert.Equal(expectedSettings.PlaybackToggleHotkey, loadedSettings.PlaybackToggleHotkey, "Playback hotkey should round-trip.");
            Assert.Equal(expectedSettings.StopHotkey, loadedSettings.StopHotkey, "Stop hotkey should round-trip.");
            Assert.Equal(expectedSettings.HotkeySettingsHotkey, loadedSettings.HotkeySettingsHotkey, "Hotkey settings shortcut should round-trip.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task JsonHotkeySettingsStore_RejectsDuplicateBindingsAsync()
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
                () => hotkeySettingsStore.LoadAsync(),
                "Duplicate hotkey bindings must be rejected during load.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task JsonMacroLibraryUserStateStore_RoundTripsNormalizedStateAsync()
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

            Assert.Equal(
                1,
                loadedState.FavoriteFilePaths.Count,
                "Favorite paths should be normalized and de-duplicated.");
            Assert.Equal(
                Path.GetFullPath(favoritePath),
                loadedState.FavoriteFilePaths[0],
                "Favorite paths should persist as full paths.");
            Assert.True(
                loadedState.LastUsedUtcByFilePath.ContainsKey(Path.GetFullPath(recentPath)),
                "Recent macro usage should round-trip.");
            Assert.Equal(
                DateTimeKind.Utc,
                loadedState.LastUsedUtcByFilePath[Path.GetFullPath(recentPath)].Kind,
                "Recent timestamps should be normalized to UTC.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task MacroLibraryService_ReadsDurationMetadataAsync()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            var storageService = new MacroStorageService(
                new JsonMacroStorageService(),
                new XmlMacroStorageService());
            var libraryService = new MacroLibraryService(
                storageService,
                new RecordingTestLogger(),
                directoryPath);

            await storageService.SaveAsJsonAsync(
                CreateSession("JsonMacro", 15, 20),
                Path.Combine(directoryPath, "json-macro.json"));
            await storageService.SaveAsXmlAsync(
                CreateSession("XmlMacro", 30, 45),
                Path.Combine(directoryPath, "xml-macro.xml"));

            IReadOnlyList<MacroLibraryEntry> entries = await libraryService.ListAsync();

            MacroLibraryEntry jsonEntry = entries.Single(entry => entry.Format == MacroLibraryFileFormat.Json);
            MacroLibraryEntry xmlEntry = entries.Single(entry => entry.Format == MacroLibraryFileFormat.Xml);

            Assert.Equal(2, jsonEntry.EventCount, "JSON event count should be read from metadata.");
            Assert.Equal(35, jsonEntry.TotalDurationMs, "JSON duration metadata should sum event delays.");
            Assert.Equal(2, xmlEntry.EventCount, "XML event count should be read from metadata.");
            Assert.Equal(75, xmlEntry.TotalDurationMs, "XML duration metadata should sum event delays.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task MacroLibraryService_ImportsXmlFilesIntoLibraryAsync()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string libraryDirectoryPath = Path.Combine(directoryPath, "library");
            string sourceDirectoryPath = Path.Combine(directoryPath, "source");
            Directory.CreateDirectory(sourceDirectoryPath);

            var storageService = new MacroStorageService(
                new JsonMacroStorageService(),
                new XmlMacroStorageService());
            var libraryService = new MacroLibraryService(
                storageService,
                new RecordingTestLogger(),
                libraryDirectoryPath);

            string sourceFilePath = Path.Combine(sourceDirectoryPath, "DesktopMacro.xml");
            await storageService.SaveAsXmlAsync(
                CreateSession("EmbeddedMacroName", 8, 13),
                sourceFilePath);

            string importedFilePath = await libraryService.ImportAsync(sourceFilePath);
            IReadOnlyList<MacroLibraryEntry> entries = await libraryService.ListAsync();
            MacroSession importedSession = await libraryService.LoadAsync(importedFilePath);

            Assert.True(
                importedFilePath.StartsWith(libraryDirectoryPath, StringComparison.OrdinalIgnoreCase),
                "Imported XML files should be copied into the managed library directory.");
            Assert.True(
                File.Exists(importedFilePath),
                "Imported XML file should exist in the managed library directory.");
            Assert.Equal(
                "DesktopMacro",
                entries.Single().Name,
                "Imported macro names should default to the selected file name instead of embedded session metadata.");
            Assert.Equal(
                "DesktopMacro.xml",
                Path.GetFileName(importedFilePath),
                "Imported XML file names should preserve the selected source file name.");
            Assert.Equal(
                "DesktopMacro",
                importedSession.Name,
                "Loaded library sessions should use the managed file name as the display name.");
            Assert.Equal(
                MacroLibraryFileFormat.Xml,
                entries.Single().Format,
                "Imported XML files should remain XML in the library.");
            Assert.Equal(
                21,
                entries.Single().TotalDurationMs,
                "Imported XML metadata should be visible through the library listing.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static Task MacroLibraryFilterEngine_AppliesSearchAndFiltersAsync()
    {
        DateTime now = new(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc);
        MacroLibraryViewItem[] items =
        [
            CreateLibraryViewItem("FavoriteJson", "favorite.json", now, 50, 5_000, MacroLibraryFileFormat.Json, isFavorite: true, lastUsedUtc: null),
            CreateLibraryViewItem("RecentXml", "recent.xml", now.AddMinutes(-1), 150, 12_000, MacroLibraryFileFormat.Xml, isFavorite: false, lastUsedUtc: now),
            CreateLibraryViewItem("LongJson", "long.json", now.AddMinutes(-2), 650, 90_000, MacroLibraryFileFormat.Json, isFavorite: false, lastUsedUtc: now.AddMinutes(-5))
        ];

        MacroLibraryViewItem[] favoriteItems = MacroLibraryFilterEngine.Apply(
            items,
            "json",
            MacroLibraryFilterKind.Favorites);
        MacroLibraryViewItem[] recentItems = MacroLibraryFilterEngine.Apply(
            items,
            null,
            MacroLibraryFilterKind.Recent);
        MacroLibraryViewItem[] longItems = MacroLibraryFilterEngine.Apply(
            items,
            null,
            MacroLibraryFilterKind.Long);
        MacroLibraryViewItem[] shortItems = MacroLibraryFilterEngine.Apply(
            items,
            "5000",
            MacroLibraryFilterKind.Short);

        Assert.Equal(
            ["FavoriteJson"],
            favoriteItems.Select(item => item.Entry.Name),
            "Favorite filter should combine with text search.");
        Assert.Equal(
            ["RecentXml", "LongJson"],
            recentItems.Select(item => item.Entry.Name),
            "Recent filter should sort by last-used timestamp.");
        Assert.Equal(
            ["LongJson"],
            longItems.Select(item => item.Entry.Name),
            "Long filter should include event-heavy or duration-heavy macros.");
        Assert.Equal(
            ["FavoriteJson"],
            shortItems.Select(item => item.Entry.Name),
            "Short filter should include compact macros and support duration search.");

        return Task.CompletedTask;
    }

    private static Task EventListFilterEngine_AppliesSearchAndSmartFiltersAsync()
    {
        MacroEvent[] events =
        [
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.Move,
                DelayMs = 10,
                X = 10,
                Y = 20,
                Description = "Fare hareketi"
            },
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.LeftDown,
                DelayMs = 30,
                X = 11,
                Y = 21,
                Description = "Sol tus basildi"
            },
            new()
            {
                EventType = MacroEventType.Keyboard,
                KeyboardActionType = KeyboardActionType.KeyDown,
                DelayMs = 5,
                KeyCode = 13,
                ScanCode = 28,
                KeyName = "Enter",
                Description = "Enter tusu"
            },
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.Wheel,
                DelayMs = 260,
                WheelDelta = 120,
                Description = "Fare tekerlegi"
            }
        ];

        EventListViewItem[] keyboardItems = EventListFilterEngine.Apply(
            events,
            new EventListFilterCriteria(null, EventListTypeFilterKind.Keyboard, EventListSmartFilterKind.All));
        EventListViewItem[] mouseItems = EventListFilterEngine.Apply(
            events,
            new EventListFilterCriteria(null, EventListTypeFilterKind.Mouse, EventListSmartFilterKind.All));
        EventListViewItem[] longDelayItems = EventListFilterEngine.Apply(
            events,
            new EventListFilterCriteria(null, EventListTypeFilterKind.All, EventListSmartFilterKind.LongDelays));
        EventListViewItem[] searchItems = EventListFilterEngine.Apply(
            events,
            new EventListFilterCriteria("enter", EventListTypeFilterKind.All, EventListSmartFilterKind.All));
        EventListViewItem[] mousePositionItems = EventListFilterEngine.Apply(
            events,
            new EventListFilterCriteria("21", EventListTypeFilterKind.Mouse, EventListSmartFilterKind.All));

        Assert.Equal(
            [2],
            keyboardItems.Select(item => item.SourceIndex),
            "Keyboard type filter should preserve source indexes.");
        Assert.Equal(
            [0, 1, 3],
            mouseItems.Select(item => item.SourceIndex),
            "Mouse type filter should include all mouse events without relying on analysis filters.");
        Assert.Equal(
            [3],
            longDelayItems.Select(item => item.SourceIndex),
            "Long delay smart filter should use the configured threshold.");
        Assert.Equal(
            [2],
            searchItems.Select(item => item.SourceIndex),
            "Search should match key names and details.");
        Assert.Equal(
            [1],
            mousePositionItems.Select(item => item.SourceIndex),
            "Type filter should combine with coordinate search.");

        return Task.CompletedTask;
    }

    private static Task EventListFilterEngine_IdentifiesOptimizationCandidatesAndInvalidEventsAsync()
    {
        MacroEvent[] optimizationEvents =
        [
            CreateMouseMoveEvent(8, 100, 200),
            CreateMouseMoveEvent(9, 101, 201),
            CreateMouseMoveEvent(10, 102, 202),
            CreateMouseMoveEvent(11, 103, 203),
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.LeftDown,
                DelayMs = 20,
                X = 103,
                Y = 203,
                Description = "Sol tus basildi"
            }
        ];
        MacroEvent[] invalidEvents =
        [
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.LeftDown,
                DelayMs = 10,
                Description = "Koordinatsiz tiklama"
            },
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.Wheel,
                DelayMs = 10,
                Description = "Eksik wheel delta"
            },
            new()
            {
                EventType = MacroEventType.Keyboard,
                KeyboardActionType = KeyboardActionType.KeyDown,
                DelayMs = 10,
                KeyName = "A"
            },
            new()
            {
                EventType = MacroEventType.Keyboard,
                KeyboardActionType = KeyboardActionType.None,
                DelayMs = 10,
                KeyCode = 65,
                ScanCode = 30
            },
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.None,
                DelayMs = 10
            }
        ];

        EventListViewItem[] optimizationCandidates = EventListFilterEngine.Apply(
            optimizationEvents,
            new EventListFilterCriteria(null, EventListTypeFilterKind.All, EventListSmartFilterKind.OptimizationCandidates));
        EventListViewItem[] invalidItems = EventListFilterEngine.Apply(
            invalidEvents,
            new EventListFilterCriteria(null, EventListTypeFilterKind.All, EventListSmartFilterKind.InvalidOrIncomplete));

        Assert.Equal(
            [1, 2],
            optimizationCandidates.Select(item => item.SourceIndex),
            "Optimization filter should protect first move, last move and click-adjacent position.");
        Assert.Equal(
            [0, 1, 2, 3, 4],
            invalidItems.Select(item => item.SourceIndex),
            "Invalid filter should catch incomplete keyboard and mouse events.");

        Assert.Equal(
            EventListRowInsight.OptimizationCandidate,
            EventListFilterEngine.GetInsight(optimizationEvents, 1),
            "Insight classification should identify optimization candidates.");
        Assert.Equal(
            EventListRowInsight.InvalidOrIncomplete,
            EventListFilterEngine.GetInsight(invalidEvents, 0),
            "Insight classification should prioritize invalid events.");

        return Task.CompletedTask;
    }

    private static Task EventListFilterEngine_SupportsStructuredSearchExpressionsAsync()
    {
        MacroEvent[] events =
        [
            CreateMouseMoveEvent(10, 680, 291),
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.LeftDown,
                DelayMs = 231,
                X = 521,
                Y = 288,
                Description = "Sol tus basildi"
            },
            new()
            {
                EventType = MacroEventType.Keyboard,
                KeyboardActionType = KeyboardActionType.KeyDown,
                DelayMs = 12,
                KeyCode = 13,
                ScanCode = 28,
                KeyName = "Enter",
                Description = "Enter tusu"
            },
            new()
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.Wheel,
                DelayMs = 320,
                WheelDelta = -120,
                Description = "Fare tekerlegi"
            }
        ];

        Assert.Equal(
            [1],
            EventListFilterEngine.Apply(
                    events,
                    new EventListFilterCriteria("x:521 y:288", EventListTypeFilterKind.All, EventListSmartFilterKind.All))
                .Select(item => item.SourceIndex),
            "Structured coordinate search should require all coordinate tokens.");
        Assert.Equal(
            [3],
            EventListFilterEngine.Apply(
                    events,
                    new EventListFilterCriteria("delay>=250 type:mouse", EventListTypeFilterKind.All, EventListSmartFilterKind.All))
                .Select(item => item.SourceIndex),
            "Structured comparison search should combine with type aliases.");
        Assert.Equal(
            [2],
            EventListFilterEngine.Apply(
                    events,
                    new EventListFilterCriteria("row:3 key:enter", EventListTypeFilterKind.All, EventListSmartFilterKind.All))
                .Select(item => item.SourceIndex),
            "Structured row and key search should preserve source indexes.");
        Assert.Equal(
            [0],
            EventListFilterEngine.Apply(
                    events,
                    new EventListFilterCriteria("action:move elapsed<20", EventListTypeFilterKind.All, EventListSmartFilterKind.All))
                .Select(item => item.SourceIndex),
            "Structured action search should combine with elapsed-time comparisons.");

        return Task.CompletedTask;
    }

    private static Task MacroOptimizationService_RemovesRedundantMouseMovesAndPreservesTimingAsync()
    {
        var session = new MacroSession
        {
            Name = "Optimize Test"
        };
        session.Events.AddRange(
            [
                CreateMouseMoveEvent(10, 100, 200),
                CreateMouseMoveEvent(20, 101, 201),
                CreateMouseMoveEvent(30, 102, 202),
                CreateMouseMoveEvent(40, 103, 203),
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftDown,
                    DelayMs = 5,
                    X = 103,
                    Y = 203,
                    Description = "Sol tus basildi"
                },
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftUp,
                    DelayMs = 5,
                    X = 103,
                    Y = 203,
                    Description = "Sol tus birakildi"
                }
            ]);

        var service = new MacroOptimizationService();
        MacroOptimizationPreview preview = service.Preview(session);

        Assert.True(preview.HasChanges, "Redundant move events should produce an optimization preview.");
        Assert.Equal(6, preview.OriginalEventCount, "Original event count should reflect the source session.");
        Assert.Equal(4, preview.OptimizedEventCount, "Optimizer should keep movement boundaries and click events.");
        Assert.Equal(2, preview.RemovedEventCount, "Only interior short mouse moves should be removed.");
        Assert.Equal(session.TotalDurationMs, preview.OptimizedDurationMs, "Removed delays should be transferred to the next kept event.");
        Assert.Equal(20, session.Events[1].DelayMs, "Preview generation must not mutate the source session.");
        Assert.Equal(90, preview.OptimizedEvents[1].DelayMs, "Removed move delays should be absorbed by the next kept movement.");
        Assert.Equal(103, preview.OptimizedEvents[1].X, "The last movement before click should be preserved.");

        return Task.CompletedTask;
    }

    private static Task MacroOptimizationService_PreservesProtectedEventsAsync()
    {
        var session = new MacroSession
        {
            Name = "Protected Events"
        };
        session.Events.AddRange(
            [
                CreateMouseMoveEvent(10, 100, 200),
                CreateMouseMoveEvent(20, 101, 201),
                CreateMouseMoveEvent(300, 102, 202),
                CreateMouseMoveEvent(20, 103, 203),
                new MacroEvent
                {
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyDown,
                    DelayMs = 10,
                    KeyCode = 65,
                    ScanCode = 30,
                    KeyName = "A",
                    Description = "Tus basildi - A"
                },
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.Wheel,
                    DelayMs = 30,
                    X = 103,
                    Y = 203,
                    WheelDelta = -120,
                    Description = "Fare tekerlegi"
                },
                CreateMouseMoveEvent(20, 104, 204)
            ]);

        var service = new MacroOptimizationService();
        MacroOptimizationPreview preview = service.Preview(session);

        Assert.Equal(1, preview.RemovedEventCount, "Only the interior short move should be removed.");
        Assert.Equal(session.TotalDurationMs, preview.OptimizedDurationMs, "Total timing should remain stable.");
        Assert.True(
            preview.OptimizedEvents.Any(macroEvent =>
                macroEvent.EventType == MacroEventType.Keyboard
                && macroEvent.KeyboardActionType == KeyboardActionType.KeyDown),
            "Keyboard events should be preserved.");
        Assert.True(
            preview.OptimizedEvents.Any(macroEvent =>
                macroEvent.EventType == MacroEventType.Mouse
                && macroEvent.MouseActionType == MouseActionType.Wheel),
            "Mouse wheel events should be preserved.");
        Assert.True(
            preview.OptimizedEvents.Any(macroEvent =>
                macroEvent.EventType == MacroEventType.Mouse
                && macroEvent.MouseActionType == MouseActionType.Move
                && macroEvent.DelayMs >= MacroOptimizationService.LongDelayThresholdMs),
            "Long-delay mouse movements should be preserved.");

        return Task.CompletedTask;
    }

    private static Task MacroOptimizationService_KeepsBalancedMousePathSamplesAsync()
    {
        var session = new MacroSession
        {
            Name = "Balanced Path"
        };

        for (int index = 0; index < 10; index++)
        {
            session.Events.Add(CreateMouseMoveEvent(50, index * 10, 100));
        }

        session.Events.Add(
            new MacroEvent
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.LeftDown,
                DelayMs = 10,
                X = 90,
                Y = 100,
                Description = "Sol tus basildi"
            });

        var service = new MacroOptimizationService();
        MacroOptimizationPreview preview = service.Preview(session);

        MacroEvent[] optimizedMoves = preview.OptimizedEvents
            .Where(macroEvent => macroEvent.EventType == MacroEventType.Mouse
                && macroEvent.MouseActionType == MouseActionType.Move)
            .ToArray();

        Assert.Equal(5, preview.OptimizedEventCount, "Balanced optimization should keep sampled movement points plus the click.");
        Assert.Equal(4, optimizedMoves.Length, "Balanced optimization should keep first, sampled, and final movement points.");
        Assert.Equal(
            [0, 40, 80, 90],
            optimizedMoves.Select(macroEvent => macroEvent.X!.Value),
            "Mouse path samples should be kept at stable elapsed intervals.");
        Assert.Equal(session.TotalDurationMs, preview.OptimizedDurationMs, "Balanced sampling should preserve total timing.");

        return Task.CompletedTask;
    }

    private static Task MacroReportGenerator_SummarizesSessionAnalysisAsync()
    {
        var session = new MacroSession
        {
            Name = "Report Session",
            CreatedAtUtc = new DateTime(2026, 5, 6, 9, 30, 0, DateTimeKind.Utc)
        };
        session.Events.AddRange(
            [
                CreateMouseMoveEvent(10, 100, 200),
                CreateMouseMoveEvent(20, 101, 201),
                CreateMouseMoveEvent(30, 102, 202),
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftDown,
                    DelayMs = 320,
                    X = 102,
                    Y = 202,
                    Description = "Sol tus basildi"
                },
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftUp,
                    DelayMs = 5,
                    X = 102,
                    Y = 202,
                    Description = "Sol tus birakildi"
                },
                new MacroEvent
                {
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyDown,
                    DelayMs = 50,
                    KeyCode = 13,
                    ScanCode = 28,
                    KeyName = "Enter",
                    Description = "Enter tusu"
                }
            ]);

        MacroReportStatistics statistics = MacroReportGenerator.Analyze(session, "/tmp/report-session.json");
        string reportText = MacroReportGenerator.GenerateText(session, "/tmp/report-session.json");

        Assert.Equal(6, statistics.TotalEventCount, "Report should count every macro event.");
        Assert.Equal(435, statistics.TotalDurationMs, "Report should sum normalized delays.");
        Assert.Equal(1, statistics.KeyboardEventCount, "Report should count keyboard events.");
        Assert.Equal(5, statistics.MouseEventCount, "Report should count mouse events.");
        Assert.Equal(3, statistics.MouseMoveCount, "Report should count mouse movement events.");
        Assert.Equal(2, statistics.MouseClickCount, "Report should count mouse click edges.");
        Assert.Equal(1, statistics.LongDelayCount, "Report should flag long delays.");
        Assert.Equal(1, statistics.OptimizationCandidateCount, "Report should reuse optimization-candidate analysis.");
        Assert.True(
            reportText.Contains("Toplam olay: 6", StringComparison.Ordinal),
            "Text report should include total event count.");
        Assert.True(
            reportText.Contains("Optimizasyon adayi: 1", StringComparison.Ordinal),
            "Text report should include optimization candidate count.");
        Assert.True(
            reportText.Contains("Olay Detaylari", StringComparison.Ordinal),
            "Text report should include the detailed event section.");
        Assert.True(
            reportText.Contains("001 | 00:00:00.010 | Fare | Move | X: 100, Y: 200 | 10 ms | Fare hareketi", StringComparison.Ordinal),
            "Text report should include detailed event rows.");

        return Task.CompletedTask;
    }

    private static Task MacroReportGenerator_EscapesHtmlReportValuesAsync()
    {
        var session = new MacroSession
        {
            Name = "<Macro & Test>",
            CreatedAtUtc = new DateTime(2026, 5, 6, 9, 30, 0, DateTimeKind.Utc)
        };
        session.Events.Add(
            new MacroEvent
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.Move,
                DelayMs = 10,
                X = 100,
                Y = 200,
                Description = "<detail & value>"
            });

        string reportHtml = MacroReportGenerator.GenerateHtml(session, "/tmp/source<unsafe>.json");

        Assert.True(
            reportHtml.Contains("&lt;Macro &amp; Test&gt;", StringComparison.Ordinal),
            "HTML report should encode the session name.");
        Assert.False(
            reportHtml.Contains("<Macro & Test>", StringComparison.Ordinal),
            "HTML report should not emit raw user-provided names.");
        Assert.True(
            reportHtml.Contains("source&lt;unsafe&gt;.json", StringComparison.Ordinal),
            "HTML report should encode file names.");
        Assert.True(
            reportHtml.Contains("&lt;detail &amp; value&gt;", StringComparison.Ordinal),
            "HTML report should encode event details.");
        Assert.True(
            reportHtml.Contains("<th>#</th>", StringComparison.Ordinal),
            "HTML report should include the detailed event table.");

        return Task.CompletedTask;
    }

    private static async Task HotkeySettingsDialog_UsesStableLayoutAsync()
    {
        _ = await RunOnStaThreadAsync(() =>
        {
            using var dialog = new HotkeySettingsDialog(HotkeySettings.CreateDefault());
            dialog.CreateControl();
            dialog.PerformLayout();

            Assert.True(
                dialog.ClientSize.Width >= 600,
                $"Hotkey settings dialog should allocate enough width to show all selector columns. Actual client size: {dialog.ClientSize.Width}x{dialog.ClientSize.Height}.");
            Assert.True(
                dialog.ClientSize.Height >= 344,
                $"Hotkey settings dialog should allocate enough height to show all settings rows. Actual client size: {dialog.ClientSize.Width}x{dialog.ClientSize.Height}.");

            List<ModernSelect> modifierSelects = GetDescendants<ModernSelect>(dialog).ToList();
            Assert.Equal(4, modifierSelects.Count, "Hotkey settings dialog should expose four modifier selectors.");

            List<HotkeyKeyInput> keyInputs = GetDescendants<HotkeyKeyInput>(dialog).ToList();
            Assert.Equal(4, keyInputs.Count, "Hotkey settings dialog should expose four key inputs.");

            List<ThemedDialogButton> buttons = GetDescendants<ThemedDialogButton>(dialog).ToList();
            Assert.Equal(3, buttons.Count, "Hotkey settings dialog should expose three action buttons.");

            foreach (Control control in modifierSelects
                .Cast<Control>()
                .Concat(keyInputs)
                .Concat(buttons))
            {
                Rectangle bounds = GetBoundsRelativeToAncestor(control, dialog);

                Assert.True(
                    bounds.Left >= 0 && bounds.Top >= 0,
                    $"{DescribeControl(control)} should remain within the dialog origin.");
                Assert.True(
                    bounds.Right <= dialog.ClientSize.Width,
                    $"{DescribeControl(control)} should be fully visible within the dialog width.");
                Assert.True(
                    bounds.Bottom <= dialog.ClientSize.Height,
                    $"{DescribeControl(control)} should be fully visible within the dialog height.");
            }
        });
    }

    private static async Task MacroPreviewMapDialog_UsesStableLayoutAsync()
    {
        _ = await RunOnStaThreadAsync(() =>
        {
            using var dialog = new MacroPreviewMapDialog();
            dialog.CreateControl();
            dialog.PerformLayout();

            Size minimumClientSize = MacroPreviewMapDialog.ResolveMinimumClientSize();
            Assert.True(
                dialog.ClientSize.Width >= minimumClientSize.Width,
                $"Preview map dialog should allocate at least its minimum width. Actual client size: {dialog.ClientSize.Width}x{dialog.ClientSize.Height}.");
            Assert.True(
                dialog.ClientSize.Height >= minimumClientSize.Height,
                $"Preview map dialog should allocate at least its minimum height. Actual client size: {dialog.ClientSize.Width}x{dialog.ClientSize.Height}.");

            MacroPreviewMapControl mapControl = GetDescendants<MacroPreviewMapControl>(dialog).Single();
            Rectangle mapBounds = GetBoundsRelativeToAncestor(mapControl, dialog);
            Assert.True(
                mapBounds.Height >= DesignTokens.Scale(260),
                $"Preview map should retain enough vertical room after popup chrome is laid out. Actual map bounds: {mapBounds}.");

            foreach (Control control in GetDescendants<Control>(dialog).Where(control => control.Visible))
            {
                Rectangle bounds = GetBoundsRelativeToAncestor(control, dialog);
                Assert.True(
                    bounds.Left >= 0 && bounds.Top >= 0,
                    $"{DescribeControl(control)} should remain within the preview dialog origin.");
                Assert.True(
                    bounds.Right <= dialog.ClientSize.Width,
                    $"{DescribeControl(control)} should be fully visible within the preview dialog width.");
                Assert.True(
                    bounds.Bottom <= dialog.ClientSize.Height,
                    $"{DescribeControl(control)} should be fully visible within the preview dialog height.");
            }
        });
    }

    private static async Task MacroPreviewMapDialog_ClampsSizeToWorkingAreaAsync()
    {
        _ = await RunOnStaThreadAsync(() =>
        {
            Rectangle workingArea = new(
                0,
                0,
                DesignTokens.Scale(520),
                DesignTokens.Scale(380));
            int margin = DesignTokens.Scale(12);
            Size minimumClientSize = MacroPreviewMapDialog.ResolveMinimumClientSizeForWorkingArea(
                workingArea,
                margin);
            Size clientSize = MacroPreviewMapDialog.ResolveClientSizeForWorkingArea(
                workingArea,
                margin,
                minimumClientSize);

            int availableWidth = workingArea.Width - (margin * 2);
            int availableHeight = workingArea.Height - (margin * 2);

            Assert.True(
                clientSize.Width <= availableWidth,
                $"Preview map dialog width should clamp to the working area. Client={clientSize.Width}, Available={availableWidth}.");
            Assert.True(
                clientSize.Height <= availableHeight,
                $"Preview map dialog height should clamp to the working area. Client={clientSize.Height}, Available={availableHeight}.");
            Assert.True(
                clientSize.Width >= minimumClientSize.Width,
                "Preview map dialog width should not shrink below its working-area-adjusted minimum.");
            Assert.True(
                clientSize.Height >= minimumClientSize.Height,
                "Preview map dialog height should not shrink below its working-area-adjusted minimum.");
        });
    }

    private static Task AppShellLayoutProfile_UsesExpandedLayoutWhenDesignedShellFitsAsync()
    {
        AppShellLayoutProfile profile = AppShellLayoutProfileResolver.Resolve(
            new Rectangle(0, 0, 3360, 2100),
            densityScale: 2f);

        Assert.Equal(
            AppShellLayoutMode.Expanded,
            profile.Mode,
            "A 3360x2100 working area at 200% scale should keep the full dashboard layout.");
        Assert.Equal(
            new Size(2560, 1520),
            profile.PreferredClientSize,
            "The expanded shell should preserve the designed 1280x760 baseline at 200% scale.");
        Assert.True(
            profile.FitRatio >= 0.94f,
            "Expanded layout should only be selected when the designed shell substantially fits.");
        Assert.Equal(
            108,
            profile.Chrome.TitleBarRowHeight,
            "Expanded chrome should preserve the safe 54px title-bar baseline at 200% scale.");
        Assert.Equal(
            212,
            profile.Chrome.ToolbarRowHeight,
            "Expanded chrome should preserve the original 106px toolbar-row baseline at 200% scale.");
        Assert.Equal(
            18f,
            profile.Main.SummaryColumnPercent,
            "Expanded main layout should preserve the designed summary-column ratio.");
        Assert.Equal(
            36,
            profile.Main.PrimaryCardContentPadding.Left,
            "Expanded main cards should preserve the designed 18px content padding at 200% scale.");

        return Task.CompletedTask;
    }

    private static Task AppShellLayoutProfile_ConstrainsSmallHighDpiWorkingAreasAsync()
    {
        AppShellLayoutProfile profile = AppShellLayoutProfileResolver.Resolve(
            new Rectangle(0, 0, 1920, 1200),
            densityScale: 2f);

        Assert.Equal(
            AppShellLayoutMode.Constrained,
            profile.Mode,
            "A 1920x1200 working area at 200% scale needs the constrained shell layout.");
        Assert.True(
            profile.PreferredClientSize.Width <= 1920,
            "Constrained startup width should fit inside the working area.");
        Assert.True(
            profile.PreferredClientSize.Height <= 1200,
            "Constrained startup height should fit inside the working area.");
        Assert.True(
            profile.MinimumClientSize.Width < 1920,
            "The minimum shell size should remain resizable below the constrained working area.");
        AppShellLayoutProfile expandedProfile = AppShellLayoutProfileResolver.Resolve(
            new Rectangle(0, 0, 3360, 2100),
            densityScale: 2f);
        Assert.True(
            profile.Chrome.TitleBarRowHeight < expandedProfile.Chrome.TitleBarRowHeight,
            "Constrained chrome should reclaim vertical space from the title bar.");
        Assert.True(
            profile.Chrome.ToolbarRowHeight < expandedProfile.Chrome.ToolbarRowHeight,
            "Constrained chrome should reclaim vertical space from the toolbar row.");
        Assert.True(
            profile.Chrome.ToolbarControlMinimumHeight <= profile.Chrome.ToolbarRowHeight,
            "Toolbar control minimum height should fit inside the constrained toolbar row.");
        Assert.True(
            profile.Main.SummaryColumnPercent > expandedProfile.Main.SummaryColumnPercent,
            "Constrained main layout should reserve more width for the summary column.");
        Assert.True(
            profile.Main.PrimaryCardContentPadding.Left < expandedProfile.Main.PrimaryCardContentPadding.Left,
            "Constrained main layout should reclaim content padding from the primary cards.");
        Assert.True(
            profile.Main.EventSearchWidth < expandedProfile.Main.EventSearchWidth,
            "Constrained event toolbar should use narrower fixed filter controls.");
        Assert.True(
            profile.Main.SummaryCaptionColumnWidth < expandedProfile.Main.SummaryCaptionColumnWidth,
            "Constrained summary details should reduce fixed caption width before truncating values.");

        return Task.CompletedTask;
    }

    private static Task AppShellLayoutProfile_KeepsToolbarMetricsConsistentAsync()
    {
        const int dashboardCardVerticalInset = 2;
        Rectangle[] workingAreas =
        [
            new Rectangle(0, 0, 1280, 760),
            new Rectangle(0, 0, 1920, 1200),
            new Rectangle(0, 0, 3360, 2100)
        ];
        float[] densityScales = [1f, 1.25f, 1.5f, 2f];

        foreach (Rectangle workingArea in workingAreas)
        {
            foreach (float densityScale in densityScales)
            {
                AppShellLayoutProfile profile = AppShellLayoutProfileResolver.Resolve(
                    workingArea,
                    densityScale);
                int availableToolbarHeight = profile.Chrome.ToolbarRowHeight
                    - profile.Chrome.ToolbarHostMargin.Vertical
                    - profile.Chrome.ToolbarContentPadding.Vertical
                    - dashboardCardVerticalInset;

                Assert.True(
                    profile.Chrome.ToolbarControlMinimumHeight > 0,
                    $"Toolbar minimum height should stay positive for {profile.Mode} at {densityScale:0.##}x.");
                Assert.True(
                    profile.Chrome.ToolbarControlMinimumHeight <= availableToolbarHeight,
                    $"Toolbar minimum height should fit its effective row. Mode={profile.Mode}; Density={densityScale:0.##}; Minimum={profile.Chrome.ToolbarControlMinimumHeight}; Available={availableToolbarHeight}.");
            }
        }

        return Task.CompletedTask;
    }

    private static async Task FileLogger_WritesEntriesAsync()
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

            string[] logFiles = Directory.GetFiles(logDirectoryPath, "macromaster-*.log");
            Assert.Equal(1, logFiles.Length, "Logger should create a single daily log file for the current date.");

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

    private static Task WindowsKeyboardHookSource_SwallowsSubscriberExceptionsInCallbackAsync()
    {
        var logger = new RecordingTestLogger();
        using var keyboardHookSource = new WindowsKeyboardHookSource(logger);
        keyboardHookSource.KeyActivityReceived += _ =>
            throw new InvalidOperationException("Klavye dinleyici hatasi");

        IntPtr hookDataPointer = Marshal.AllocHGlobal(Marshal.SizeOf<TestKeyboardHookStruct>());

        try
        {
            Marshal.StructureToPtr(
                new TestKeyboardHookStruct
                {
                    vkCode = 0x41,
                    scanCode = 0x1E,
                    flags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                },
                hookDataPointer,
                fDeleteOld: false);

            _ = InvokeHookCallback(
                keyboardHookSource,
                nCode: 0,
                wParam: 0x0100,
                hookDataPointer);
        }
        finally
        {
            Marshal.FreeHGlobal(hookDataPointer);
        }

        Assert.True(
            logger.Entries.Any(entry =>
                entry.Source == nameof(WindowsKeyboardHookSource)
                && entry.Message.Contains("dinleyicilere iletilirken", StringComparison.Ordinal)),
            "Keyboard hook callbacks should log and swallow subscriber exceptions.");
        return Task.CompletedTask;
    }

    private static Task WindowsMouseHookSource_SwallowsSubscriberExceptionsInCallbackAsync()
    {
        var logger = new RecordingTestLogger();
        using var mouseHookSource = new WindowsMouseHookSource(logger);
        mouseHookSource.MouseActivityReceived += (_, _, _, _) =>
            throw new InvalidOperationException("Fare dinleyici hatasi");

        IntPtr hookDataPointer = Marshal.AllocHGlobal(Marshal.SizeOf<TestMouseHookStruct>());

        try
        {
            Marshal.StructureToPtr(
                new TestMouseHookStruct
                {
                    pt = new TestPoint { x = 320, y = 240 },
                    mouseData = 0,
                    flags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                },
                hookDataPointer,
                fDeleteOld: false);

            _ = InvokeHookCallback(
                mouseHookSource,
                nCode: 0,
                wParam: 0x0200,
                hookDataPointer);
        }
        finally
        {
            Marshal.FreeHGlobal(hookDataPointer);
        }

        Assert.True(
            logger.Entries.Any(entry =>
                entry.Source == nameof(WindowsMouseHookSource)
                && entry.Message.Contains("dinleyicilere iletilirken", StringComparison.Ordinal)),
            "Mouse hook callbacks should log and swallow subscriber exceptions.");
        return Task.CompletedTask;
    }

    private static Task GlobalExceptionHandlerRegistration_LogsThreadExceptionsAsync()
    {
        var logger = new RecordingTestLogger();
        string? shownMessage = null;

        using var registration = new GlobalExceptionHandlerRegistration(
            logger,
            (message, _, _) => shownMessage = message,
            registerHandlers: false);

        registration.HandleThreadException(new InvalidOperationException("UI thread boom"));

        Assert.True(
            logger.Entries.Any(entry =>
                entry.Source == nameof(GlobalExceptionHandlerRegistration)
                && entry.Message.Contains("UI is parcaciginda", StringComparison.Ordinal)),
            "Global exception handler should log UI thread exceptions.");
        Assert.True(
            !string.IsNullOrWhiteSpace(shownMessage),
            "Global exception handler should surface a user-facing error message for UI thread exceptions.");
        return Task.CompletedTask;
    }

    private static Task GlobalExceptionHandlerRegistration_LogsUnobservedTaskExceptionsAsync()
    {
        var logger = new RecordingTestLogger();

        using var registration = new GlobalExceptionHandlerRegistration(
            logger,
            registerHandlers: false);

        registration.HandleUnobservedTaskException(
            new AggregateException(new InvalidOperationException("Task boom")));

        Assert.True(
            logger.Entries.Any(entry =>
                entry.Source == nameof(GlobalExceptionHandlerRegistration)
                && entry.Message.Contains("Gozlemlenmemis gorev hatasi", StringComparison.Ordinal)),
            "Global exception handler should log unobserved task exceptions.");
        return Task.CompletedTask;
    }

    private static async Task AppCompositionRoot_UsesInjectedStoragePathsAsync()
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

    private static async Task JsonMacroStorageService_RejectsMouseClickWithoutCoordinatesAsync()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "missing-click-coordinates.json");

            string payload = JsonSerializer.Serialize(
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "InvalidMouseClick",
                    CreatedAtUtc = DateTime.UtcNow,
                    FormatVersion = MacroSessionFormat.CurrentVersion,
                    Events = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            EventType = MacroEventType.Mouse,
                            MouseActionType = MouseActionType.LeftDown,
                            DelayMs = 10,
                            Description = "Missing coordinates"
                        }
                    }
                });

            await File.WriteAllTextAsync(filePath, payload);

            var storageService = new JsonMacroStorageService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.LoadAsync(filePath),
                "Mouse click events without coordinates must be rejected during load.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task JsonMacroStorageService_RejectsInvalidEventTypeAsync()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "invalid-event-type.json");

            string payload = JsonSerializer.Serialize(
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "InvalidEventType",
                    CreatedAtUtc = DateTime.UtcNow,
                    FormatVersion = MacroSessionFormat.CurrentVersion,
                    Events = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            EventType = 99,
                            DelayMs = 10,
                            Description = "Invalid event type"
                        }
                    }
                });

            await File.WriteAllTextAsync(filePath, payload);

            var storageService = new JsonMacroStorageService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.LoadAsync(filePath),
                "Invalid event type values must be rejected during load.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task JsonMacroStorageService_RejectsInvalidKeyboardActionAsync()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "invalid-keyboard-action.json");

            string payload = JsonSerializer.Serialize(
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "InvalidKeyboardAction",
                    CreatedAtUtc = DateTime.UtcNow,
                    FormatVersion = MacroSessionFormat.CurrentVersion,
                    Events = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            EventType = MacroEventType.Keyboard,
                            KeyboardActionType = 99,
                            DelayMs = 10,
                            KeyCode = 0x41,
                            Description = "Invalid keyboard action"
                        }
                    }
                });

            await File.WriteAllTextAsync(filePath, payload);

            var storageService = new JsonMacroStorageService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.LoadAsync(filePath),
                "Invalid keyboard action values must be rejected during load.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task JsonMacroStorageService_RejectsInvalidMouseActionAsync()
    {
        string directoryPath = CreateTempDirectory();

        try
        {
            string filePath = Path.Combine(directoryPath, "invalid-mouse-action.json");

            string payload = JsonSerializer.Serialize(
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "InvalidMouseAction",
                    CreatedAtUtc = DateTime.UtcNow,
                    FormatVersion = MacroSessionFormat.CurrentVersion,
                    Events = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            EventType = MacroEventType.Mouse,
                            MouseActionType = 99,
                            DelayMs = 10,
                            X = 100,
                            Y = 200,
                            Description = "Invalid mouse action"
                        }
                    }
                });

            await File.WriteAllTextAsync(filePath, payload);

            var storageService = new JsonMacroStorageService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => storageService.LoadAsync(filePath),
                "Invalid mouse action values must be rejected during load.");
        }
        finally
        {
            DeleteDirectoryIfExists(directoryPath);
        }
    }

    private static async Task MacroRecorderService_SuppressesReservedHotkeyModifiersAsync()
    {
        var keyboardHookSource = new TestKeyboardHookSource();
        var mouseHookSource = new TestMouseHookSource();
        var recorder = new MacroRecorderService(
            keyboardHookSource,
            mouseHookSource,
            new ApplicationStateService(),
            new TestHotkeyConfiguration(
                new HotkeyBinding(0x77, HotkeyModifiers.Control),
                HotkeyBinding.None(0x78),
                HotkeyBinding.None(0x79)));

        await recorder.StartAsync("ReservedHotkey");

        keyboardHookSource.Emit(new KeyboardActivityInfo(0xA2, 0x1D, true, false, HotkeyModifiers.Control, true, HotkeyModifiers.Control));
        keyboardHookSource.Emit(new KeyboardActivityInfo(0x77, 0x42, true, false, HotkeyModifiers.Control, false, HotkeyModifiers.None));
        keyboardHookSource.Emit(new KeyboardActivityInfo(0x77, 0x42, false, false, HotkeyModifiers.Control, false, HotkeyModifiers.None));
        keyboardHookSource.Emit(new KeyboardActivityInfo(0xA2, 0x1D, false, false, HotkeyModifiers.None, true, HotkeyModifiers.Control));

        await recorder.StopAsync();

        Assert.Equal(
            0,
            recorder.CurrentSession?.Events.Count ?? -1,
            "Reserved hotkeys must not leak modifier events into the recorded macro.");
    }

    private static async Task MacroRecorderService_PreservesLegitimateModifierSequencesAsync()
    {
        var keyboardHookSource = new TestKeyboardHookSource();
        var mouseHookSource = new TestMouseHookSource();
        var recorder = new MacroRecorderService(
            keyboardHookSource,
            mouseHookSource,
            new ApplicationStateService(),
            new TestHotkeyConfiguration(
                HotkeyBinding.None(0x77),
                HotkeyBinding.None(0x78),
                HotkeyBinding.None(0x79)));

        await recorder.StartAsync("ModifierSequence");

        keyboardHookSource.Emit(new KeyboardActivityInfo(0xA2, 0x1D, true, false, HotkeyModifiers.Control, true, HotkeyModifiers.Control));
        keyboardHookSource.Emit(new KeyboardActivityInfo(0x41, 0x1E, true, false, HotkeyModifiers.Control, false, HotkeyModifiers.None));
        keyboardHookSource.Emit(new KeyboardActivityInfo(0x41, 0x1E, false, false, HotkeyModifiers.Control, false, HotkeyModifiers.None));
        keyboardHookSource.Emit(new KeyboardActivityInfo(0xA2, 0x1D, false, false, HotkeyModifiers.None, true, HotkeyModifiers.Control));

        await recorder.StopAsync();

        MacroSession? session = recorder.CurrentSession;
        Assert.True(session is not null, "The recorder should produce a completed session.");
        Assert.Equal(4, session!.Events.Count, "Legitimate modifier combinations must remain in the recorded macro.");
        Assert.Equal(0xA2, session.Events[0].KeyCode, "Control key-down should be preserved.");
        Assert.Equal(0x41, session.Events[1].KeyCode, "The primary key-down should be preserved.");
        Assert.Equal(0x41, session.Events[2].KeyCode, "The primary key-up should be preserved.");
        Assert.Equal(0xA2, session.Events[3].KeyCode, "Control key-up should be preserved.");
    }

    private static async Task MacroRecorderService_ConcurrentInput_CollectsEventsAsync()
    {
        var keyboardHookSource = new TestKeyboardHookSource();
        var mouseHookSource = new TestMouseHookSource();
        var recorder = new MacroRecorderService(
            keyboardHookSource,
            mouseHookSource,
            new ApplicationStateService(),
            new TestHotkeyConfiguration(
                HotkeyBinding.None(0x77),
                HotkeyBinding.None(0x78),
                HotkeyBinding.None(0x79)));

        const int eventCountPerSource = 50;

        await recorder.StartAsync("ConcurrentInput");

        Task keyboardTask = Task.Run(() =>
        {
            for (int index = 0; index < eventCountPerSource; index++)
            {
                keyboardHookSource.Emit(
                    new KeyboardActivityInfo(
                        0x41 + (index % 5),
                        0x1E + (index % 5),
                        true,
                        false,
                        HotkeyModifiers.None,
                        false,
                        HotkeyModifiers.None));
            }
        });

        Task mouseTask = Task.Run(() =>
        {
            for (int index = 0; index < eventCountPerSource; index++)
            {
                mouseHookSource.Emit(MouseActionType.Move, index, index + 10, null);
            }
        });

        await Task.WhenAll(keyboardTask, mouseTask);
        await recorder.StopAsync();

        MacroSession? session = recorder.CurrentSession;
        Assert.True(session is not null, "The recorder should produce a completed session after concurrent input.");
        Assert.Equal(
            eventCountPerSource * 2,
            session!.Events.Count,
            "Concurrent keyboard and mouse input should be recorded without event loss.");
    }

    private static async Task MacroRecorderService_StopAsync_RetainsCompletedSessionAsync()
    {
        var keyboardHookSource = new TestKeyboardHookSource();
        var mouseHookSource = new TestMouseHookSource();
        var recorder = new MacroRecorderService(
            keyboardHookSource,
            mouseHookSource,
            new ApplicationStateService(),
            new TestHotkeyConfiguration(
                HotkeyBinding.None(0x77),
                HotkeyBinding.None(0x78),
                HotkeyBinding.None(0x79)));

        await recorder.StartAsync("RetainedSession");
        keyboardHookSource.Emit(new KeyboardActivityInfo(0x41, 0x1E, true, false, HotkeyModifiers.None, false, HotkeyModifiers.None));

        await recorder.StopAsync();

        MacroSession? session = recorder.CurrentSession;
        Assert.True(session is not null, "Stop should retain the completed session for follow-up operations.");
        Assert.Equal("RetainedSession", session!.Name, "The retained session should remain available through CurrentSession.");
        Assert.Equal(1, session.Events.Count, "The retained completed session should expose the finalized events.");
    }

    private static async Task MacroRecorderService_Clear_RemovesActiveSessionAsync()
    {
        var keyboardHookSource = new TestKeyboardHookSource();
        var mouseHookSource = new TestMouseHookSource();
        var recorder = new MacroRecorderService(
            keyboardHookSource,
            mouseHookSource,
            new ApplicationStateService(),
            new TestHotkeyConfiguration(
                HotkeyBinding.None(0x77),
                HotkeyBinding.None(0x78),
                HotkeyBinding.None(0x79)));

        await recorder.StartAsync("ClearSession");
        keyboardHookSource.Emit(new KeyboardActivityInfo(0x41, 0x1E, true, false, HotkeyModifiers.None, false, HotkeyModifiers.None));
        await recorder.StopAsync();

        Assert.True(recorder.CurrentSession is not null, "The recorder should have a completed session before clear.");

        recorder.Clear();

        Assert.True(recorder.CurrentSession is null, "Clear should remove the active session reference completely.");
    }

    private static async Task MacroRecorderService_Clear_WhileRecording_ThrowsAsync()
    {
        var keyboardHookSource = new TestKeyboardHookSource();
        var mouseHookSource = new TestMouseHookSource();
        var recorder = new MacroRecorderService(
            keyboardHookSource,
            mouseHookSource,
            new ApplicationStateService(),
            new TestHotkeyConfiguration(
                HotkeyBinding.None(0x77),
                HotkeyBinding.None(0x78),
                HotkeyBinding.None(0x79)));

        await recorder.StartAsync("ClearWhileRecording");

        try
        {
            Assert.Throws<InvalidOperationException>(
                recorder.Clear,
                "Clear should be rejected while recording is in progress.");
        }
        finally
        {
            await recorder.StopAsync();
        }
    }

    private static async Task MacroPlaybackService_UseRelativeCoordinates_RebasesMouseCoordinatesAsync()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter
        {
            CurrentCursorPosition = new CursorPosition(500, 300)
        };
        var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            new ApplicationStateService());

        var session = new MacroSession
        {
            Name = "RelativeMouse",
            Events =
            {
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.Move,
                    X = 100,
                    Y = 200,
                    Description = "Move anchor"
                },
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftDown,
                    X = 130,
                    Y = 240,
                    Description = "Click relative"
                }
            }
        };

        await playbackService.PlayAsync(
            session,
            new PlaybackSettings { UseRelativeCoordinates = true });

        Assert.Equal(1, inputPlaybackAdapter.CursorPositionReadCount, "Relative playback should read the current cursor position once per iteration.");
        Assert.Equal(2, inputPlaybackAdapter.PlayedEvents.Count, "Both mouse events should be played.");
        Assert.Equal(500, inputPlaybackAdapter.PlayedEvents[0].X, "The first mouse event should be rebased to the current cursor X position.");
        Assert.Equal(300, inputPlaybackAdapter.PlayedEvents[0].Y, "The first mouse event should be rebased to the current cursor Y position.");
        Assert.Equal(530, inputPlaybackAdapter.PlayedEvents[1].X, "Subsequent mouse X coordinates should preserve their relative delta.");
        Assert.Equal(340, inputPlaybackAdapter.PlayedEvents[1].Y, "Subsequent mouse Y coordinates should preserve their relative delta.");
    }

    private static async Task MacroPlaybackService_UseRelativeCoordinatesFalse_PreservesAbsoluteCoordinatesAsync()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter
        {
            CurrentCursorPosition = new CursorPosition(900, 900)
        };
        var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            new ApplicationStateService());

        var session = new MacroSession
        {
            Name = "AbsoluteMouse",
            Events =
            {
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.Move,
                    X = 42,
                    Y = 64,
                    Description = "Absolute move"
                }
            }
        };

        await playbackService.PlayAsync(
            session,
            new PlaybackSettings { UseRelativeCoordinates = false });

        Assert.Equal(0, inputPlaybackAdapter.CursorPositionReadCount, "Absolute playback should not query the current cursor position.");
        Assert.Equal(1, inputPlaybackAdapter.PlayedEvents.Count, "The mouse event should be played once.");
        Assert.Equal(42, inputPlaybackAdapter.PlayedEvents[0].X, "Absolute playback should preserve the recorded X coordinate.");
        Assert.Equal(64, inputPlaybackAdapter.PlayedEvents[0].Y, "Absolute playback should preserve the recorded Y coordinate.");
    }

    private static async Task MacroPlaybackService_SimulationMode_AdvancesWithoutSendingInputAsync()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter
        {
            CurrentCursorPosition = new CursorPosition(900, 900)
        };
        var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            new ApplicationStateService());
        List<Guid> simulatedEventIds = [];

        playbackService.EventPlayed += macroEvent => simulatedEventIds.Add(macroEvent.Id);

        MacroSession session = CreatePlaybackSession();

        await playbackService.PlayAsync(
            session,
            new PlaybackSettings
            {
                SimulationMode = true,
                UseRelativeCoordinates = true,
                PreserveOriginalTiming = false,
                SpeedMultiplier = 4.0,
                StopOnError = true
            });

        Assert.Equal(0, inputPlaybackAdapter.AttemptedEventIds.Count, "Simulation mode must not call the real input playback adapter.");
        Assert.Equal(0, inputPlaybackAdapter.CursorPositionReadCount, "Simulation mode should not query cursor anchors for relative playback.");
        Assert.Equal(
            session.Events.Select(macroEvent => macroEvent.Id).ToArray(),
            simulatedEventIds,
            "Simulation mode should still publish progress for every event.");
    }

    private static async Task MacroPlaybackService_UseRelativeCoordinates_ReanchorsEachIterationAsync()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        inputPlaybackAdapter.CursorPositions.Enqueue(new CursorPosition(500, 300));
        inputPlaybackAdapter.CursorPositions.Enqueue(new CursorPosition(1000, 200));

        var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            new ApplicationStateService());

        var session = new MacroSession
        {
            Name = "RelativeRepeat",
            Events =
            {
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.Move,
                    X = 100,
                    Y = 200,
                    Description = "Anchor"
                },
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftUp,
                    X = 130,
                    Y = 240,
                    Description = "Delta"
                }
            }
        };

        await playbackService.PlayAsync(
            session,
            new PlaybackSettings
            {
                UseRelativeCoordinates = true,
                RepeatCount = 2
            });

        Assert.Equal(2, inputPlaybackAdapter.CursorPositionReadCount, "Relative playback should re-read the cursor anchor for each repeat.");
        Assert.Equal(4, inputPlaybackAdapter.PlayedEvents.Count, "Two events across two repeats should be played.");
        Assert.Equal(500, inputPlaybackAdapter.PlayedEvents[0].X, "The first iteration should use the first cursor anchor.");
        Assert.Equal(530, inputPlaybackAdapter.PlayedEvents[1].X, "The first iteration delta should be preserved.");
        Assert.Equal(1000, inputPlaybackAdapter.PlayedEvents[2].X, "The second iteration should use the second cursor anchor.");
        Assert.Equal(1030, inputPlaybackAdapter.PlayedEvents[3].X, "The second iteration delta should be preserved.");
    }

    private static async Task MacroPlaybackService_UseRelativeCoordinates_NoMouseAnchor_DoesNotQueryCursorAsync()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter
        {
            CurrentCursorPosition = new CursorPosition(500, 300)
        };
        var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            new ApplicationStateService());

        var session = new MacroSession
        {
            Name = "RelativeWithoutMouse",
            Events =
            {
                new MacroEvent
                {
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyDown,
                    KeyCode = 0x41,
                    ScanCode = 0x1E,
                    Description = "Keyboard down"
                }
            }
        };

        await playbackService.PlayAsync(
            session,
            new PlaybackSettings { UseRelativeCoordinates = true });

        Assert.Equal(0, inputPlaybackAdapter.CursorPositionReadCount, "Relative playback should not query the cursor when the session has no mouse coordinate anchor.");
        Assert.Equal(1, inputPlaybackAdapter.PlayedEvents.Count, "The keyboard event should still be played.");
        Assert.Equal(MacroEventType.Keyboard, inputPlaybackAdapter.PlayedEvents[0].EventType, "Non-mouse events should be preserved.");
        Assert.False(inputPlaybackAdapter.PlayedEvents[0].X.HasValue, "Keyboard events should not receive synthetic X coordinates.");
        Assert.False(inputPlaybackAdapter.PlayedEvents[0].Y.HasValue, "Keyboard events should not receive synthetic Y coordinates.");
    }

    private static async Task MacroPlaybackService_UseRelativeCoordinates_PausedStepUsesActiveAnchorAsync()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        inputPlaybackAdapter.CursorPositions.Enqueue(new CursorPosition(500, 300));
        var applicationStateService = new ApplicationStateService();
        var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            applicationStateService);
        var firstEventPlayed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var session = new MacroSession
        {
            Name = "RelativePausedStep",
            Events =
            {
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.Move,
                    X = 100,
                    Y = 200,
                    Description = "Anchor"
                },
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftDown,
                    X = 150,
                    Y = 220,
                    Description = "Manual step"
                },
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftUp,
                    X = 170,
                    Y = 250,
                    Description = "Resume step"
                }
            }
        };

        playbackService.EventPlayed += macroEvent =>
        {
            if (macroEvent.Id != session.Events[0].Id
                || firstEventPlayed.Task.IsCompleted)
            {
                return;
            }

            playbackService.PauseAsync().GetAwaiter().GetResult();
            inputPlaybackAdapter.CurrentCursorPosition = new CursorPosition(900, 900);
            firstEventPlayed.TrySetResult(true);
        };

        Task playTask = playbackService.PlayAsync(
            session,
            new PlaybackSettings
            {
                UseRelativeCoordinates = true,
                StopOnError = true
            });

        await firstEventPlayed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await playbackService.WaitForPlaybackNavigationReadyAsync();
        await playbackService.PlayEventAtAsync(
            session,
            new PlaybackSettings
            {
                UseRelativeCoordinates = true,
                StopOnError = true
            },
            eventIndex: 1,
            logicalEventIndex: 1);
        await playbackService.SeekPlaybackCursorAsync(2);
        await playbackService.ResumeAsync();
        await playTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, inputPlaybackAdapter.CursorPositionReadCount, "Paused single-step playback should reuse the active relative anchor for the same iteration.");
        int[] expectedXCoordinates = [500, 550, 570];
        int[] expectedYCoordinates = [300, 320, 350];

        Assert.Equal(
            expectedXCoordinates,
            inputPlaybackAdapter.PlayedEvents.Select(macroEvent => macroEvent.X!.Value).ToArray(),
            "Paused single-step playback should preserve the active iteration's relative X offsets.");
        Assert.Equal(
            expectedYCoordinates,
            inputPlaybackAdapter.PlayedEvents.Select(macroEvent => macroEvent.Y!.Value).ToArray(),
            "Paused single-step playback should preserve the active iteration's relative Y offsets.");
    }

    private static async Task MacroPlaybackService_UseRelativeCoordinates_PausedStepReanchorsNewIterationAsync()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        inputPlaybackAdapter.CursorPositions.Enqueue(new CursorPosition(500, 300));
        var applicationStateService = new ApplicationStateService();
        var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            applicationStateService);
        var firstIterationCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var session = new MacroSession
        {
            Name = "RelativePausedRepeatStep",
            Events =
            {
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.Move,
                    X = 100,
                    Y = 200,
                    Description = "Anchor"
                },
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftUp,
                    X = 130,
                    Y = 240,
                    Description = "Delta"
                }
            }
        };

        playbackService.EventPlayed += macroEvent =>
        {
            if (macroEvent.Id != session.Events[1].Id
                || firstIterationCompleted.Task.IsCompleted)
            {
                return;
            }

            playbackService.PauseAsync().GetAwaiter().GetResult();
            inputPlaybackAdapter.CurrentCursorPosition = new CursorPosition(1000, 200);
            firstIterationCompleted.TrySetResult(true);
        };

        Task playTask = playbackService.PlayAsync(
            session,
            new PlaybackSettings
            {
                UseRelativeCoordinates = true,
                RepeatCount = 2,
                StopOnError = true
            });

        await firstIterationCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await playbackService.WaitForPlaybackNavigationReadyAsync();
        await playbackService.PlayEventAtAsync(
            session,
            new PlaybackSettings
            {
                UseRelativeCoordinates = true,
                RepeatCount = 2,
                StopOnError = true
            },
            eventIndex: 0,
            logicalEventIndex: 2);
        await playbackService.SeekPlaybackCursorAsync(3);
        await playbackService.ResumeAsync();
        await playTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, inputPlaybackAdapter.CursorPositionReadCount, "Paused single-step playback should re-anchor when it moves into a new repeat iteration.");
        int[] expectedXCoordinates = [500, 530, 1000, 1030];
        int[] expectedYCoordinates = [300, 340, 200, 240];

        Assert.Equal(
            expectedXCoordinates,
            inputPlaybackAdapter.PlayedEvents.Select(macroEvent => macroEvent.X!.Value).ToArray(),
            "Relative playback should preserve offsets after a paused step enters the next repeat iteration.");
        Assert.Equal(
            expectedYCoordinates,
            inputPlaybackAdapter.PlayedEvents.Select(macroEvent => macroEvent.Y!.Value).ToArray(),
            "Relative playback should preserve Y offsets after a paused step enters the next repeat iteration.");
    }

    private static async Task MacroPlaybackService_RepeatCount_ReplaysEntireSessionAsync()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        var playbackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            new ApplicationStateService());

        MacroSession session = CreatePlaybackSession();

        await playbackService.PlayAsync(
            session,
            new PlaybackSettings
            {
                RepeatCount = 3,
                StopOnError = true
            });

        Assert.Equal(6, inputPlaybackAdapter.PlayedEvents.Count, "RepeatCount should replay the entire session for each requested iteration.");
        Assert.Equal(6, inputPlaybackAdapter.SuccessfulEventIds.Count, "Every repeated event should complete successfully.");
    }

    private static async Task MacroPlaybackService_PauseResume_WaitsForResumeAsync()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        var applicationStateService = new ApplicationStateService();
        var playbackService = new MacroPlaybackService(inputPlaybackAdapter, inputPlaybackAdapter, applicationStateService);
        var firstEventPlayed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var session = new MacroSession
        {
            Name = "DuraklatDevamEt",
            Events =
            {
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyDown,
                    KeyCode = 0x41,
                    ScanCode = 0x1E,
                    Description = "Ilk olay"
                },
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyUp,
                    KeyCode = 0x41,
                    ScanCode = 0x1E,
                    Description = "Ikinci olay"
                }
            }
        };

        playbackService.EventPlayed += macroEvent =>
        {
            if (macroEvent.Id != session.Events[0].Id)
            {
                return;
            }

            playbackService.PauseAsync().GetAwaiter().GetResult();
            firstEventPlayed.TrySetResult(true);
        };

        Task playTask = playbackService.PlayAsync(
            session,
            new PlaybackSettings
            {
                PreserveOriginalTiming = true,
                StopOnError = true
            });

        await firstEventPlayed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(75);

        Assert.Equal(1, inputPlaybackAdapter.PlayedEvents.Count, "Playback should remain paused until resume is requested.");
        Assert.Equal(AppState.Paused, applicationStateService.CurrentState, "Playback state should be Paused while waiting for resume.");

        await playbackService.ResumeAsync();
        await playTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, inputPlaybackAdapter.PlayedEvents.Count, "Playback should continue after resume is requested.");
        Assert.Equal(AppState.Idle, applicationStateService.CurrentState, "Playback should return to Idle after resume completes.");
    }

    private static async Task MacroPlaybackService_SeekPausedPlayback_ResumesFromCursorAsync()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        var applicationStateService = new ApplicationStateService();
        var playbackService = new MacroPlaybackService(inputPlaybackAdapter, inputPlaybackAdapter, applicationStateService);
        var firstEventPlayed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        MacroSession session = CreateThreeEventPlaybackSession();

        playbackService.EventPlayed += macroEvent =>
        {
            if (macroEvent.Id != session.Events[0].Id
                || firstEventPlayed.Task.IsCompleted)
            {
                return;
            }

            playbackService.PauseAsync().GetAwaiter().GetResult();
            firstEventPlayed.TrySetResult(true);
        };

        Task playTask = playbackService.PlayAsync(
            session,
            new PlaybackSettings { StopOnError = true });

        await firstEventPlayed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await playbackService.WaitForPlaybackNavigationReadyAsync();
        await playbackService.SeekPlaybackCursorAsync(0);
        await playbackService.ResumeAsync();
        await playTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(
            new[]
            {
                session.Events[0].Id,
                session.Events[0].Id,
                session.Events[1].Id,
                session.Events[2].Id
            },
            inputPlaybackAdapter.PlayedEvents.Select(macroEvent => macroEvent.Id).ToArray(),
            "Seeking a paused playback cursor should make resume continue from the requested logical event.");
        Assert.Equal(AppState.Idle, applicationStateService.CurrentState, "Playback should finish after replaying from the requested cursor.");
    }

    private static async Task MacroPlaybackService_PausedSingleStep_ContinuesAfterSteppedEventAsync()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        var applicationStateService = new ApplicationStateService();
        var playbackService = new MacroPlaybackService(inputPlaybackAdapter, inputPlaybackAdapter, applicationStateService);
        var firstEventPlayed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        MacroSession session = CreateThreeEventPlaybackSession();

        playbackService.EventPlayed += macroEvent =>
        {
            if (macroEvent.Id != session.Events[0].Id
                || firstEventPlayed.Task.IsCompleted)
            {
                return;
            }

            playbackService.PauseAsync().GetAwaiter().GetResult();
            firstEventPlayed.TrySetResult(true);
        };

        Task playTask = playbackService.PlayAsync(
            session,
            new PlaybackSettings { StopOnError = true });

        await firstEventPlayed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await playbackService.WaitForPlaybackNavigationReadyAsync();
        await playbackService.PlayEventAtAsync(session, new PlaybackSettings { StopOnError = true }, eventIndex: 1);
        await playbackService.SeekPlaybackCursorAsync(2);
        await playbackService.ResumeAsync();
        await playTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(
            new[]
            {
                session.Events[0].Id,
                session.Events[1].Id,
                session.Events[2].Id
            },
            inputPlaybackAdapter.PlayedEvents.Select(macroEvent => macroEvent.Id).ToArray(),
            "A manually stepped paused event should not be replayed after resume.");
        Assert.Equal(AppState.Idle, applicationStateService.CurrentState, "Playback should return to Idle after the remaining event completes.");
    }

    private static async Task MacroPlaybackService_PausedStepDuringDelay_DoesNotReplayStaleEventAsync()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        var applicationStateService = new ApplicationStateService();
        var playbackService = new MacroPlaybackService(inputPlaybackAdapter, inputPlaybackAdapter, applicationStateService);
        var firstEventPlayed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        MacroSession session = CreateThreeEventPlaybackSession();
        session.Events[1].DelayMs = 250;

        playbackService.EventPlayed += macroEvent =>
        {
            if (macroEvent.Id == session.Events[0].Id)
            {
                firstEventPlayed.TrySetResult(true);
            }
        };

        Task playTask = playbackService.PlayAsync(
            session,
            new PlaybackSettings
            {
                PreserveOriginalTiming = true,
                StopOnError = true
            });

        await firstEventPlayed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(25);
        await playbackService.PauseAsync();
        await playbackService.PlayEventAtAsync(
            session,
            new PlaybackSettings
            {
                PreserveOriginalTiming = true,
                StopOnError = true
            },
            eventIndex: 1);
        await playbackService.SeekPlaybackCursorAsync(2);
        await playbackService.ResumeAsync();
        await playTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(
            new[]
            {
                session.Events[0].Id,
                session.Events[1].Id,
                session.Events[2].Id
            },
            inputPlaybackAdapter.PlayedEvents.Select(macroEvent => macroEvent.Id).ToArray(),
            "A delayed event that was manually stepped while paused must not be replayed by the background loop.");
    }

    private static async Task MacroPlaybackService_StopOnErrorTrue_StopsImmediatelyAsync()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        var applicationStateService = new ApplicationStateService();
        var playbackService = new MacroPlaybackService(inputPlaybackAdapter, inputPlaybackAdapter, applicationStateService);
        List<AppState> observedStates = [];

        applicationStateService.StateChanged += observedStates.Add;

        MacroSession session = CreatePlaybackSession();
        inputPlaybackAdapter.FailOnEventIds.Add(session.Events[0].Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => playbackService.PlayAsync(session, new PlaybackSettings { StopOnError = true }),
            "Playback should stop on the first adapter failure when StopOnError is enabled.");

        Assert.Equal(1, inputPlaybackAdapter.AttemptedEventIds.Count, "Only the failing event should be attempted when StopOnError is enabled.");
        Assert.Equal(
            new[] { AppState.Playing, AppState.Error, AppState.Idle },
            observedStates,
            "Playback failures should surface through Error before returning to Idle.");
        Assert.Equal(AppState.Idle, applicationStateService.CurrentState, "Playback should finish in Idle after cleanup.");
    }

    private static async Task MacroPlaybackService_StopOnErrorFalse_ContinuesAndReportsAsync()
    {
        var inputPlaybackAdapter = new TestInputPlaybackAdapter();
        var applicationStateService = new ApplicationStateService();
        var playbackService = new MacroPlaybackService(inputPlaybackAdapter, inputPlaybackAdapter, applicationStateService);
        List<AppState> observedStates = [];

        applicationStateService.StateChanged += observedStates.Add;

        MacroSession session = CreatePlaybackSession();
        inputPlaybackAdapter.FailOnEventIds.Add(session.Events[0].Id);

        await Assert.ThrowsAsync<AggregateException>(
            () => playbackService.PlayAsync(session, new PlaybackSettings { StopOnError = false }),
            "Playback should continue past event failures and report them as an aggregate error when StopOnError is disabled.");

        Assert.Equal(2, inputPlaybackAdapter.AttemptedEventIds.Count, "Playback should continue to later events when StopOnError is disabled.");
        Assert.Equal(1, inputPlaybackAdapter.SuccessfulEventIds.Count, "Only successful events should be counted as completed.");
        Assert.Equal(
            new[] { AppState.Playing, AppState.Error, AppState.Idle },
            observedStates,
            "Playback failures should still transition through Error before returning to Idle.");
        Assert.Equal(AppState.Idle, applicationStateService.CurrentState, "Playback should finish in Idle after cleanup.");
    }

    private static MacroSession CreatePlaybackSession()
    {
        return new MacroSession
        {
            Name = "Playback",
            Events =
            {
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyDown,
                    KeyCode = 0x41,
                    ScanCode = 0x1E,
                    Description = "A down"
                },
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyUp,
                    KeyCode = 0x41,
                    ScanCode = 0x1E,
                    Description = "A up"
                }
            }
        };
    }

    private static MacroSession CreateThreeEventPlaybackSession()
    {
        return new MacroSession
        {
            Name = "SteppablePlayback",
            Events =
            {
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyDown,
                    KeyCode = 0x41,
                    ScanCode = 0x1E,
                    Description = "A down"
                },
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyUp,
                    KeyCode = 0x41,
                    ScanCode = 0x1E,
                    Description = "A up"
                },
                new MacroEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyDown,
                    KeyCode = 0x42,
                    ScanCode = 0x30,
                    Description = "B down"
                }
            }
        };
    }

    private static Task<bool> RunOnStaThreadAsync(Action action)
    {
        var taskCompletionSource = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Thread staThread = new(() =>
        {
            try
            {
                action();
                taskCompletionSource.SetResult(true);
            }
            catch (Exception ex)
            {
                taskCompletionSource.SetException(ex);
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.IsBackground = true;
        staThread.Start();

        return taskCompletionSource.Task;
    }

    private static IEnumerable<TControl> GetDescendants<TControl>(Control root)
        where TControl : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is TControl typedChild)
            {
                yield return typedChild;
            }

            foreach (TControl descendant in GetDescendants<TControl>(child))
            {
                yield return descendant;
            }
        }
    }

    private static Rectangle GetBoundsRelativeToAncestor(Control control, Control ancestor)
    {
        int left = control.Left;
        int top = control.Top;
        Control? current = control.Parent;

        while (current is not null && current != ancestor)
        {
            left += current.Left;
            top += current.Top;
            current = current.Parent;
        }

        if (current != ancestor)
        {
            throw new InvalidOperationException("The requested control is not inside the expected ancestor.");
        }

        return new Rectangle(left, top, control.Width, control.Height);
    }

    private static string DescribeControl(Control control)
    {
        string controlText = string.IsNullOrWhiteSpace(control.Text)
            ? control.GetType().Name
            : control.Text;

        return $"{control.GetType().Name} '{controlText}'";
    }

    private static IntPtr InvokeHookCallback(
        object hookSource,
        int nCode,
        int wParam,
        IntPtr hookDataPointer)
    {
        MethodInfo callbackMethod = hookSource.GetType().GetMethod(
            "HookCallback",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Hook callback methodi bulunamadi.");

        object? result = callbackMethod.Invoke(
            hookSource,
            [nCode, (IntPtr)wParam, hookDataPointer]);

        return result is IntPtr pointer
            ? pointer
            : IntPtr.Zero;
    }

    private static MacroSession CreateSession(string name, params int[] delayValuesMs)
    {
        var session = new MacroSession
        {
            Name = name
        };

        for (int index = 0; index < delayValuesMs.Length; index++)
        {
            session.Events.Add(
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.Move,
                    DelayMs = delayValuesMs[index],
                    X = 100 + index,
                    Y = 200 + index,
                    Description = $"Mouse move {index}"
                });
        }

        return session;
    }

    private static MacroEvent CreateMouseMoveEvent(int delayMs, int x, int y)
    {
        return new MacroEvent
        {
            EventType = MacroEventType.Mouse,
            MouseActionType = MouseActionType.Move,
            DelayMs = delayMs,
            X = x,
            Y = y,
            Description = "Fare hareketi"
        };
    }

    private static MacroLibraryViewItem CreateLibraryViewItem(
        string name,
        string fileName,
        DateTime lastModifiedUtc,
        int eventCount,
        int totalDurationMs,
        MacroLibraryFileFormat format,
        bool isFavorite,
        DateTime? lastUsedUtc)
    {
        return new MacroLibraryViewItem(
            new MacroLibraryEntry(
                name,
                Path.Combine(Path.GetTempPath(), fileName),
                lastModifiedUtc,
                eventCount,
                totalDurationMs,
                format),
            isFavorite,
            lastUsedUtc);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "MacroMaster.Tests",
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

internal static class Expect
{
    public static void True(bool condition, string message)
    {
        Xunit.Assert.True(condition, message);
    }

    public static void False(bool condition, string message)
    {
        Xunit.Assert.False(condition, message);
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        _ = message;
        Xunit.Assert.Equal(expected, actual);
    }

    public static void Equal<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
    {
        _ = message;
        Xunit.Assert.Equal(expected, actual);
    }

    public static void Throws<TException>(Action action, string message)
        where TException : Exception
    {
        _ = message;
        Xunit.Assert.Throws<TException>(action);
    }

    public static async Task ThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        _ = message;
        await Xunit.Assert.ThrowsAsync<TException>(action);
    }
}

internal sealed class TestKeyboardHookSource : IKeyboardHookSource
{
    public bool IsRunning { get; private set; }

    public event Action<KeyboardActivityInfo>? KeyActivityReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRunning = false;
        return Task.CompletedTask;
    }

    public void Emit(KeyboardActivityInfo keyboardActivity)
    {
        KeyActivityReceived?.Invoke(keyboardActivity);
    }
}

internal sealed class TestMouseHookSource : IMouseHookSource
{
    public bool IsRunning { get; private set; }

    public event Action<MouseActionType, int?, int?, int?>? MouseActivityReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsRunning = false;
        return Task.CompletedTask;
    }

    public void Emit(MouseActionType mouseActionType, int? x, int? y, int? wheelDelta)
    {
        MouseActivityReceived?.Invoke(mouseActionType, x, y, wheelDelta);
    }
}

internal sealed class TestInputPlaybackAdapter : IInputPlaybackAdapter, ICursorPositionProvider
{
    public CursorPosition CurrentCursorPosition { get; set; } = new(0, 0);

    public int CursorPositionReadCount { get; private set; }

    public Queue<CursorPosition> CursorPositions { get; } = new();

    public List<Guid> AttemptedEventIds { get; } = [];

    public List<Guid> SuccessfulEventIds { get; } = [];

    public HashSet<Guid> FailOnEventIds { get; } = [];

    public List<MacroEvent> PlayedEvents { get; } = [];

    public Task<CursorPosition> GetCursorPositionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CursorPositionReadCount++;
        CursorPosition cursorPosition = CursorPositions.Count > 0
            ? CursorPositions.Dequeue()
            : CurrentCursorPosition;
        return Task.FromResult(cursorPosition);
    }

    public Task PlayEventAsync(MacroEvent macroEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AttemptedEventIds.Add(macroEvent.Id);

        if (FailOnEventIds.Contains(macroEvent.Id))
        {
            throw new InvalidOperationException(
                $"Simulated playback failure for event {macroEvent.Description}.");
        }

        SuccessfulEventIds.Add(macroEvent.Id);
        PlayedEvents.Add(CloneEvent(macroEvent));
        return Task.CompletedTask;
    }

    private static MacroEvent CloneEvent(MacroEvent source)
    {
        return new MacroEvent
        {
            Id = source.Id,
            EventType = source.EventType,
            KeyboardActionType = source.KeyboardActionType,
            MouseActionType = source.MouseActionType,
            DelayMs = source.DelayMs,
            TimestampUtc = source.TimestampUtc,
            KeyCode = source.KeyCode,
            ScanCode = source.ScanCode,
            IsExtendedKey = source.IsExtendedKey,
            KeyName = source.KeyName,
            X = source.X,
            Y = source.Y,
            WheelDelta = source.WheelDelta,
            Description = source.Description
        };
    }
}

internal sealed class TestHotkeyConfiguration : IHotkeyConfiguration
{
    public TestHotkeyConfiguration(
        HotkeyBinding recordToggleHotkey,
        HotkeyBinding playbackToggleHotkey,
        HotkeyBinding stopHotkey,
        HotkeyBinding? hotkeySettingsHotkey = null)
    {
        RecordToggleHotkey = recordToggleHotkey;
        PlaybackToggleHotkey = playbackToggleHotkey;
        StopHotkey = stopHotkey;
        HotkeySettingsHotkey = hotkeySettingsHotkey ?? HotkeySettings.DefaultHotkeySettingsHotkey;
    }

    public HotkeyBinding RecordToggleHotkey { get; }

    public HotkeyBinding PlaybackToggleHotkey { get; }

    public HotkeyBinding StopHotkey { get; }

    public HotkeyBinding HotkeySettingsHotkey { get; }
}

internal sealed class RecordingTestLogger : IAppLogger
{
    public List<RecordedLogEntry> Entries { get; } = [];

    public void Log(
        AppLogLevel logLevel,
        string source,
        string message,
        Exception? exception = null)
    {
        Entries.Add(new RecordedLogEntry(logLevel, source, message, exception));
    }
}

internal readonly record struct RecordedLogEntry(
    AppLogLevel LogLevel,
    string Source,
    string Message,
    Exception? Exception);

[StructLayout(LayoutKind.Sequential)]
internal struct TestKeyboardHookStruct
{
    public uint vkCode;
    public uint scanCode;
    public uint flags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TestPoint
{
    public int x;
    public int y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TestMouseHookStruct
{
    public TestPoint pt;
    public uint mouseData;
    public uint flags;
    public uint time;
    public IntPtr dwExtraInfo;
}
