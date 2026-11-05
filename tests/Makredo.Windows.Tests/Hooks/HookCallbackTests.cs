using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Makredo.Platform.Windows.Hooks;
using Makredo.Windows.Tests.Support;
using Xunit;

namespace Makredo.Windows.Tests;

public sealed class HookCallbackTests
{
    [Fact(DisplayName = "WindowsKeyboardHookSource swallows subscriber exceptions in hook callbacks")]
    public Task WindowsKeyboardHookSource_SwallowsSubscriberExceptionsInCallback()
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

    [Fact(DisplayName = "WindowsMouseHookSource swallows subscriber exceptions in hook callbacks")]
    public Task WindowsMouseHookSource_SwallowsSubscriberExceptionsInCallback()
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
}

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
