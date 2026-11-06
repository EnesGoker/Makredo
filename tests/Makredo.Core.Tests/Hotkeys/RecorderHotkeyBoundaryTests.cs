using Makredo.Application.Abstractions;
using Makredo.Application.Hotkeys;
using Makredo.Application.Services;
using Makredo.Core.Tests.Support;
using Makredo.Domain.Enums;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using static Makredo.Core.Tests.Hotkeys.HotkeyRecordingFilterTests;

namespace Makredo.Core.Tests.Hotkeys;

public sealed class RecorderHotkeyBoundaryTests
{
    [Fact]
    public async Task StartupReleasesAndStoppingChordAreExcludedFromCompletedSession()
    {
        var keyboard = new TestKeyboardHookSource();
        var settings = HotkeySettings.CreateDefault();
        var recorder = new MacroRecorderService(keyboard, new TestMouseHookSource(), new ApplicationStateService(), new MutableHotkeyConfiguration(settings));
        await recorder.StartAsync("Boundary", [0xA2, 0xA0, 0xA4, 0x52]);
        keyboard.Emit(Key(0x52, false)); keyboard.Emit(Key(0xA4, false)); keyboard.Emit(Key(0xA0, false)); keyboard.Emit(Key(0xA2, false));
        keyboard.Emit(Key(0x41)); keyboard.Emit(Key(0x41, false));
        keyboard.Emit(Key(0xA2, true, HotkeyModifiers.Control));
        keyboard.Emit(Key(0xA0, true, HotkeyModifiers.Control | HotkeyModifiers.Shift));
        keyboard.Emit(Key(0xA4, true, settings.RecordToggleHotkey.Modifiers));
        keyboard.Emit(Key(0x52, true, settings.RecordToggleHotkey.Modifiers));
        await recorder.StopAsync();
        Assert.Equal<int?>([0x41, 0x41], recorder.CurrentSession!.Events.Select(item => item.KeyCode));
    }

    [Fact]
    public async Task PendingUserModifierRetainsTimestampAndMouseDelay()
    {
        var keyboard = new TestKeyboardHookSource();
        var mouse = new TestMouseHookSource();
        var time = new FakeTimeProvider();
        var recorder = new MacroRecorderService(keyboard, mouse, new ApplicationStateService(), new MutableHotkeyConfiguration(), timeProvider: time);
        await recorder.StartAsync("Timing");
        time.Advance(TimeSpan.FromMilliseconds(10));
        keyboard.Emit(Key(0xA2, true, HotkeyModifiers.Control));
        time.Advance(TimeSpan.FromMilliseconds(15));
        mouse.Emit(MouseActionType.Move, 1, 2, null);
        time.Advance(TimeSpan.FromMilliseconds(10));
        keyboard.Emit(Key(0xA2, false));
        await recorder.StopAsync();
        Assert.Equal<int>([10, 15, 10], recorder.CurrentSession!.Events.Select(item => item.DelayMs));
    }
}
