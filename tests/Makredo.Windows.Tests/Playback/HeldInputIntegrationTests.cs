using System.Runtime.InteropServices;
using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Makredo.Platform.Windows.Interop;
using Xunit;
using Xunit.Abstractions;

namespace Makredo.Windows.Tests.Playback;

[CollectionDefinition("Native input", DisableParallelization = true)]
public sealed class NativeInputIsolation;

[Collection("Native input")]
public sealed class HeldInputIntegrationTests(ITestOutputHelper output)
{
    [HostedWindowsInputFact]
    [Trait("Category", "NativeInput")]
    public async Task R3_Stop_ReleasesInjectedLeftShiftBeforePlaybackCompletes()
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        await RunOnStaAsync(async () =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15), TimeProvider.System);
            using var observer = new LeftShiftHook();
            observer.Install();
            Assert.False(IsLeftShiftHeld(), "LShift must initially be released before this test owns it.");

            var adapter = new GuardedPlaybackAdapter(observer);
            var state = new ApplicationStateService();
            using var playback = new MacroPlaybackService(adapter, adapter, state);
            var session = new MacroSession { Name = "R3 held LShift" };
            session.Events.Add(LeftShift(KeyboardActionType.KeyDown));
            session.Events.Add(new MacroEvent { EventType = MacroEventType.System });
            Task? play = null;

            try
            {
                play = Task.Run(() => playback.PlayAsync(session, new PlaybackSettings
                {
                    SimulationMode = false,
                    PreserveOriginalTiming = false
                }, deadline.Token));

                await Task.WhenAll(observer.Down, adapter.BlockedAfterDown).WaitAsync(deadline.Token);
                await NextMessageLoopIdleAsync(deadline.Token);
                Assert.True(IsLeftShiftHeld(), "The real injected LShift down must reach the OS before Stop.");
                output.WriteLine($"LShift down: flags=0x{observer.DownFlags:X}, GetAsyncKeyState held=true.");

                await playback.StopAsync(deadline.Token);
                await play.WaitAsync(deadline.Token);
                await NextMessageLoopIdleAsync(deadline.Token);

                bool heldAfterStop = IsLeftShiftHeld();
                output.WriteLine($"Before test cleanup: GetAsyncKeyState held={heldAfterStop}, injected up observed={observer.Up.IsCompletedSuccessfully}, up flags=0x{observer.UpFlags:X}.");
                Assert.False(heldAfterStop, "R3: LShift remains held after Stop and completed playback.");
                Assert.True(observer.Up.IsCompletedSuccessfully, "Playback must send an injected LShift up before completing.");
                Assert.Equal(1, adapter.SuccessfulKeyUps);
                Assert.Equal(AppState.Idle, state.CurrentState);
            }
            finally
            {
                try
                {
                    await playback.StopAsync();
                    if (play is not null)
                    {
                        await play.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
                    }
                }
                finally
                {
                    // A failed regression must not leave its test-owned modifier pressed on the runner.
                    if (adapter.DownAttempted)
                    {
                        await adapter.ReleaseTestOwnedShiftAsync();
                        output.WriteLine("The test-owned LShift fallback cleanup completed after the assertions.");
                    }
                }
            }
        });
    }

    private static bool IsLeftShiftHeld() => (NativeMethods.GetAsyncKeyState(NativeMethods.LeftShift) & 0x8000) != 0;

    private static MacroEvent LeftShift(KeyboardActionType action) => new()
    {
        EventType = MacroEventType.Keyboard,
        KeyboardActionType = action,
        KeyCode = NativeMethods.LeftShift,
        ScanCode = 0x2A,
        IsExtendedKey = false
    };

    private static async Task NextMessageLoopIdleAsync(CancellationToken cancellationToken)
    {
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler handler = (_, _) => idle.TrySetResult();
        System.Windows.Forms.Application.Idle += handler;
        try
        {
            await idle.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            System.Windows.Forms.Application.Idle -= handler;
        }
    }

    private static Task RunOnStaAsync(Func<Task> test)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            Exception? failure = null;
            try
            {
                using var host = new Form { ShowInTaskbar = false, Opacity = 0 };
                host.Shown += async (_, _) =>
                {
                    try
                    {
                        await test();
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                    finally
                    {
                        host.Close();
                    }
                };
                System.Windows.Forms.Application.Run(host);
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if (failure is null)
            {
                completion.TrySetResult();
            }
            else
            {
                completion.TrySetException(failure);
            }
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(30), TimeProvider.System);
    }

    private sealed class GuardedPlaybackAdapter(LeftShiftHook observer) : IInputPlaybackAdapter, ICursorPositionProvider
    {
        private readonly WindowsInputPlaybackAdapter _native = new();
        private readonly TaskCompletionSource _blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _never = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task BlockedAfterDown => _blocked.Task;
        public bool DownAttempted { get; private set; }
        public int SuccessfulKeyUps { get; private set; }

        public async Task PlayEventAsync(MacroEvent macroEvent, CancellationToken cancellationToken = default)
        {
            HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
            if (macroEvent.EventType == MacroEventType.System)
            {
                _blocked.TrySetResult();
                await _never.Task.WaitAsync(cancellationToken);
                return;
            }

            DownAttempted |= macroEvent.KeyboardActionType == KeyboardActionType.KeyDown;
            await _native.PlayEventAsync(macroEvent, cancellationToken);
            if (macroEvent.KeyboardActionType == KeyboardActionType.KeyUp)
            {
                SuccessfulKeyUps++;
                await observer.Up.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, cancellationToken);
            }
        }

        public Task<CursorPosition> GetCursorPositionAsync(CancellationToken cancellationToken = default)
        {
            HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
            return _native.GetCursorPositionAsync(cancellationToken);
        }

        public async Task ReleaseTestOwnedShiftAsync()
        {
            HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
            await _native.PlayEventAsync(LeftShift(KeyboardActionType.KeyUp));
            await observer.Up.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
        }
    }

    private sealed class LeftShiftHook : IDisposable
    {
        private readonly NativeMethods.HookCallback _callback;
        private readonly TaskCompletionSource _down = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _up = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IntPtr _hook;

        internal LeftShiftHook() => _callback = OnKeyboard;
        internal Task Down => _down.Task;
        internal Task Up => _up.Task;
        internal uint DownFlags { get; private set; }
        internal uint UpFlags { get; private set; }

        internal void Install()
        {
            HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
            Assert.True(System.Windows.Forms.Application.MessageLoop, "The hook owner must pump Windows messages.");
            _hook = NativeMethods.SetWindowsHookEx(13, _callback, NativeMethods.GetModuleHandle(null), 0);
            Assert.True(_hook != IntPtr.Zero, $"WH_KEYBOARD_LL install failed: Win32 {Marshal.GetLastPInvokeError()}.");
        }

        private IntPtr OnKeyboard(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0)
            {
                NativeMethods.KeyboardHookData keyboard = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(data);
                if (keyboard.VirtualKey == NativeMethods.LeftShift && (keyboard.Flags & 0x10) != 0)
                {
                    if (message.ToInt32() is 0x0100 or 0x0104)
                    {
                        DownFlags = keyboard.Flags;
                        _down.TrySetResult();
                    }
                    else if (message.ToInt32() is 0x0101 or 0x0105)
                    {
                        UpFlags = keyboard.Flags;
                        _up.TrySetResult();
                    }
                }
            }
            return NativeMethods.CallNextHookEx(_hook, code, message, data);
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                bool unhooked = NativeMethods.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
                Assert.True(unhooked, $"WH_KEYBOARD_LL cleanup failed: Win32 {Marshal.GetLastPInvokeError()}.");
            }
            GC.KeepAlive(_callback);
        }
    }

    private static class NativeMethods
    {
        internal const int LeftShift = 0xA0;
        internal delegate IntPtr HookCallback(int code, IntPtr message, IntPtr data);

        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int key);

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
        internal struct KeyboardHookData
        {
            public uint VirtualKey;
            public uint ScanCode;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }
    }
}

public sealed class HostedWindowsInputFactAttribute : FactAttribute
{
    public HostedWindowsInputFactAttribute()
    {
        if (!IsHostedWindowsCi)
        {
            Skip = "Real input is restricted to GitHub Actions Windows X64/ARM64 runners.";
        }
    }

    internal static bool IsHostedWindowsCi =>
        OperatingSystem.IsWindows()
        && string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.Ordinal)
        && string.Equals(Environment.GetEnvironmentVariable("RUNNER_OS"), "Windows", StringComparison.Ordinal)
        && Environment.GetEnvironmentVariable("RUNNER_ARCH") is "X64" or "ARM64";

    internal static void RequireHostedWindowsCi()
    {
        if (!IsHostedWindowsCi)
        {
            throw new InvalidOperationException("Real input is restricted to GitHub Actions Windows X64/ARM64 runners.");
        }
    }
}
