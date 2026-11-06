using System.Runtime.InteropServices;
using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Platform.Windows.Hotkeys;
using Makredo.Platform.Windows.Interop;
using Makredo.Windows.Tests.Hooks;
using Makredo.Windows.Tests.Playback;
using Makredo.Windows.Tests.Support;
using Xunit;

namespace Makredo.Windows.Tests.Hotkeys;

[Collection("Native input")]
public sealed class HotkeyIntegrationTests
{
    [HostedWindowsInputFact]
    public Task ConflictPreservesOtherRegisteredHotkeysAndReportsWarning() => HookThreadIntegrationTests.RunOnStaAsync(async () =>
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        using var conflict = new Form();
        Assert.True(RegisterHotKey(conflict.Handle, 77, 0, 0x77));
        var logger = new RecordingTestLogger();
        using var service = new WindowsHotkeyService(new MutableHotkeyConfiguration(Legacy()), logger);
        var playback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.PlaybackToggleRequested += () => playback.TrySetResult();
        try
        {
            Exception? failure = await Record.ExceptionAsync(() => service.RegisterAsync());
            Assert.Null(failure);
            Assert.False(service.IsHotkeyRegistered(HotkeyBinding.None(0x77)));
            Assert.True(service.IsHotkeyRegistered(HotkeyBinding.None(0x78)));
            Assert.Contains(logger.Entries, entry => entry.LogLevel == AppLogLevel.Warning && entry.Message.Contains("F8", StringComparison.Ordinal));
            Send(0x78, false); Send(0x78, true);
            await playback.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
        }
        finally { _ = UnregisterHotKey(conflict.Handle, 77); Send(0x78, true); }
    });

    [HostedWindowsInputFact]
    public Task HoldingRecordHotkeyDoesNotToggleRecordingRepeatedly() => HookThreadIntegrationTests.RunOnStaAsync(async () =>
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        using var service = new WindowsHotkeyService(new MutableHotkeyConfiguration(Legacy()));
        int toggles = 0;
        bool recording = false;
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.RecordToggleRequested += () => { toggles++; recording = !recording; first.TrySetResult(); };
        await service.RegisterAsync();
        try
        {
            Send(0x77, false);
            await first.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
            await Task.Run(() => { for (int i = 0; i < 5; i++) { Send(0x77, false); } Send(0x77, true); });
            var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler handler = (_, _) => idle.TrySetResult();
            System.Windows.Forms.Application.Idle += handler;
            try { await idle.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System); }
            finally { System.Windows.Forms.Application.Idle -= handler; }
            Assert.Equal(1, toggles);
            Assert.True(recording);
        }
        finally { Send(0x77, true); }
    });

    private static HotkeySettings Legacy() => new()
    {
        RecordToggleHotkey = HotkeyBinding.None(0x77),
        PlaybackToggleHotkey = HotkeyBinding.None(0x78),
        StopHotkey = HotkeyBinding.None(0x79),
        HotkeySettingsHotkey = HotkeyBinding.None(0x7B)
    };

    private static void Send(ushort key, bool up)
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        NativeMethods.INPUT[] inputs = [new() { type = 1, U = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = key, dwFlags = up ? 2u : 0u } } }];
        Assert.Equal(1u, NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>()));
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
