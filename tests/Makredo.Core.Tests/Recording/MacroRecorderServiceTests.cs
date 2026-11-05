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

namespace Makredo.Core.Tests.Recording;

public sealed class MacroRecorderServiceTests
{
    [Fact(DisplayName = "MacroRecorderService suppresses modifier leakage for reserved hotkeys")]
    public async Task MacroRecorderService_SuppressesReservedHotkeyModifiers()
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
            recorder.CurrentSession?.Events.Count ?? -1);
    }

    [Fact(DisplayName = "MacroRecorderService preserves legitimate modifier combinations")]
    public async Task MacroRecorderService_PreservesLegitimateModifierSequences()
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
        Assert.Equal(4, session!.Events.Count);
        Assert.Equal(0xA2, session.Events[0].KeyCode);
        Assert.Equal(0x41, session.Events[1].KeyCode);
        Assert.Equal(0x41, session.Events[2].KeyCode);
        Assert.Equal(0xA2, session.Events[3].KeyCode);
    }

    [Fact(DisplayName = "MacroRecorderService collects concurrent input safely")]
    public async Task MacroRecorderService_ConcurrentInput_CollectsEvents()
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
            session!.Events.Count);
    }

    [Fact(DisplayName = "MacroRecorderService retains the completed session until clear")]
    public async Task MacroRecorderService_StopAsync_RetainsCompletedSession()
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
        Assert.Equal("RetainedSession", session!.Name);
        Assert.Single(session.Events);
    }

    [Fact(DisplayName = "MacroRecorderService clear removes the active session")]
    public async Task MacroRecorderService_Clear_RemovesActiveSession()
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

    [Fact(DisplayName = "MacroRecorderService clear is rejected while recording")]
    public async Task MacroRecorderService_Clear_WhileRecording_Throws()
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
                recorder.Clear);
        }
        finally
        {
            await recorder.StopAsync();
        }
    }
}
