using System.Runtime.InteropServices;

namespace Makredo.CiDiscovery;

internal static class NativeMethods
{
    internal const int KeyboardHook = 13;
    internal const int MouseHook = 14;
    internal const int KeyDown = 0x0100;
    internal const int KeyUp = 0x0101;
    internal const int MouseMove = 0x0200;
    internal const uint KeyboardInjected = 0x10;
    internal const uint MouseInjected = 0x01;
    internal const ushort VirtualKeyF24 = 0x87;
    internal static readonly UIntPtr InputMarker = new(0x4D4B5238u);

    internal delegate IntPtr HookCallback(int code, IntPtr message, IntPtr data);

    internal static bool IsHostedWindowsCi =>
        OperatingSystem.IsWindows()
        && string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.Ordinal)
        && string.Equals(Environment.GetEnvironmentVariable("RUNNER_OS"), "Windows", StringComparison.Ordinal)
        && Environment.GetEnvironmentVariable("RUNNER_ARCH") is "X64" or "ARM64";

    internal static uint SendTaggedInput(Input[] inputs)
    {
        if (!IsHostedWindowsCi)
        {
            throw new InvalidOperationException("Native input discovery is restricted to GitHub Actions Windows runners.");
        }

        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    internal static bool RestoreCursor(Point point)
    {
        if (!IsHostedWindowsCi)
        {
            throw new InvalidOperationException("Native input discovery is restricted to GitHub Actions Windows runners.");
        }

        return SetCursorPos(point.X, point.Y);
    }

    internal static Input KeyboardInput(bool keyUp) => new()
    {
        Type = 1,
        Data = new InputData
        {
            Keyboard = new KeyboardInputData
            {
                VirtualKey = VirtualKeyF24,
                Flags = keyUp ? 0x0002u : 0,
                ExtraInfo = InputMarker
            }
        }
    };

    internal static Input MouseInput(int deltaX) => new()
    {
        Type = 0,
        Data = new InputData
        {
            Mouse = new MouseInputData
            {
                DeltaX = deltaX,
                Flags = 0x0001 | 0x2000, // MOVE | MOVE_NOCOALESCE
                ExtraInfo = InputMarker
            }
        }
    };

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SendInput")]
    private static extern uint SendInput(uint count, Input[] inputs, int structureSize);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int hook, HookCallback callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? moduleName);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        public uint Type;
        public InputData Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputData
    {
        [FieldOffset(0)]
        public MouseInputData Mouse;

        [FieldOffset(0)]
        public KeyboardInputData Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInputData
    {
        public int DeltaX;
        public int DeltaY;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInputData
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardHookData
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseHookData
    {
        public Point Position;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
