using System.Runtime.InteropServices;
using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Domain.Enums;
using Makredo.Platform.Windows.Hooks;
using Makredo.Platform.Windows.Hotkeys;
using Makredo.Platform.Windows.Interop;
using Makredo.Windows.Tests.Hooks;
using Makredo.Windows.Tests.Playback;
using Xunit;

namespace Makredo.Windows.Tests.Hotkeys;

[Collection("Native input")]
public sealed class HotkeyRecordingIntegrationTests
{
    [HostedWindowsInputFact]
    public Task RealStartAndStopChordsLeaveOnlyUserTypingInRecording() => HookThreadIntegrationTests.RunOnStaAsync(async () =>
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        var (keyboard, mouse) = WindowsHookSources.Create();
        await using var ownedKeyboard = keyboard;
        await using var ownedMouse = mouse;
        var configuration = new MutableHotkeyConfiguration();
        var recorder = new MacroRecorderService(keyboard, mouse, new ApplicationStateService(), configuration);
        using var service = new WindowsHotkeyService(configuration);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var typed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        recorder.EventRecorded += item => { if (item.KeyCode == 0x42 && item.KeyboardActionType == KeyboardActionType.KeyUp) { typed.TrySetResult(); } };
        service.RecordToggleRequested += async () =>
        {
            try
            {
                if (recorder.IsRecording) { await recorder.StopAsync(); stopped.TrySetResult(); }
                else { await recorder.StartAsync("Native boundary", WindowsHotkeyService.CapturePressedKeys(configuration.RecordToggleHotkey)); started.TrySetResult(); }
            }
            catch (Exception exception) { started.TrySetException(exception); stopped.TrySetException(exception); }
        };
        await service.RegisterAsync();
        Assert.Empty(service.RegistrationFailures);
        Assert.False(service.IsHotkeyRegistered(configuration.HotkeySettingsHotkey));
        try
        {
            await Task.Run(PressChord);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
            await Task.Run(() => { ReleaseChord(); Send(0x42, false); Send(0x42, true); });
            await typed.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
            Assert.True(recorder.IsRecording);
            await Task.Run(PressChord);
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
            Assert.False(recorder.IsRecording);
            Assert.Equal<int?>([0x42, 0x42], recorder.CurrentSession!.Events.Where(item => item.EventType == MacroEventType.Keyboard).Select(item => item.KeyCode));
        }
        finally
        {
            ReleaseChord(); Send(0x42, true);
            if (recorder.IsRecording) { await recorder.StopAsync(); }
        }
    });

    private static void PressChord() { Send(0xA2, false); Send(0xA0, false); Send(0xA4, false); Send(0x52, false); }
    private static void ReleaseChord() { Send(0x52, true); Send(0xA4, true); Send(0xA0, true); Send(0xA2, true); }
    private static void Send(ushort key, bool up)
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        NativeMethods.INPUT[] inputs = [new() { type = 1, U = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = key, dwFlags = up ? 2u : 0u } } }];
        Assert.Equal(1u, NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>()));
    }
}
