using System.Runtime.InteropServices;
using Makredo.Domain.Enums;
using Makredo.Platform.Windows.Hooks;
using Makredo.Windows.Tests.Playback;
using Xunit;
using Xunit.Abstractions;

namespace Makredo.Windows.Tests.Hooks;

[Collection("Native input")]
public sealed class HookThreadIntegrationTests(ITestOutputHelper output)
{
    private static readonly TimeSpan InputDeadline = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan UiBlockDuration = TimeSpan.FromSeconds(2);

    [HostedWindowsInputFact]
    [Trait("Category", "NativeInput")]
    public Task KeyboardInputIsConsumedPromptlyWhileTheInstallingUiThreadIsBlocked()
        => VerifyUiIndependenceAsync(keyboard: true);

    [HostedWindowsInputFact]
    [Trait("Category", "NativeInput")]
    public Task MouseInputIsConsumedPromptlyWhileTheInstallingUiThreadIsBlocked()
        => VerifyUiIndependenceAsync(keyboard: false);

    private Task VerifyUiIndependenceAsync(bool keyboard)
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        return RunOnStaAsync(async () =>
        {
            int installingThread = Environment.CurrentManagedThreadId;
            using var keyboardSource = new WindowsKeyboardHookSource();
            using var mouseSource = new WindowsMouseHookSource();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25), TimeProvider.System);
            var blockState = new UiBlockState();
            ProbeCapture? activeProbe = null;
            keyboardSource.KeyActivityReceived += activity =>
            {
                if (activity.VirtualKeyCode == NativeInput.VirtualKeyF24 && activity.IsKeyDown)
                {
                    Volatile.Read(ref activeProbe)?.Observe(blockState.IsBlocked);
                }
            };
            mouseSource.MouseActivityReceived += (action, _, _, _) =>
            {
                if (action == MouseActionType.Move)
                {
                    Volatile.Read(ref activeProbe)?.Observe(blockState.IsBlocked);
                }
            };

            NativeInput.Point originalCursor = NativeInput.ReadCursor();
            bool keyboardAttempted = false;
            try
            {
                Assert.True(!keyboard || !NativeInput.IsF24Held(), "F24 must initially be released before this test owns it.");
                if (keyboard) { await keyboardSource.StartAsync(deadline.Token); }
                else { await mouseSource.StartAsync(deadline.Token); }

                var warmup = new ProbeCapture(installingThread, keyboard ? 1 : 2);
                Volatile.Write(ref activeProbe, warmup);
                keyboardAttempted |= keyboard;
                await Task.Run(() => NativeInput.SendProbe(keyboard), deadline.Token);
                ProbeSnapshot warmupResult = await warmup.Completion.WaitAsync(InputDeadline, TimeProvider.System, deadline.Token);
                output.WriteLine($"Unblocked warm-up: kind={(keyboard ? "keyboard" : "mouse")}, events={warmupResult.Events}, last latency={warmupResult.LastLatencyMs:F2}ms.");

                var uiBlocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                ProbeCapture? blockedProbe = null;
                Task sender = Task.Run(async () =>
                {
                    await uiBlocked.Task.WaitAsync(deadline.Token);
                    var probe = new ProbeCapture(installingThread, keyboard ? 1 : 2);
                    Volatile.Write(ref blockedProbe, probe);
                    Volatile.Write(ref activeProbe, probe);
                    NativeInput.SendProbe(keyboard);
                }, deadline.Token);

                using (var unblock = new ManualResetEvent(false))
                {
                    using ITimer timer = TimeProvider.System.CreateTimer(
                        _ => unblock.Set(), null, UiBlockDuration, Timeout.InfiniteTimeSpan);
                    blockState.SetBlocked(true);
                    uiBlocked.TrySetResult();
                    // Deliberately stop this UI pump for the requested two-second regression window.
                    bool released = unblock.WaitOne(TimeSpan.FromSeconds(10));
                    blockState.SetBlocked(false);
                    Assert.True(released, "The independent timer must release the intentional UI blockage.");
                }

                await sender.WaitAsync(InputDeadline, TimeProvider.System, deadline.Token);
                ProbeCapture observedProbe = Assert.IsType<ProbeCapture>(Volatile.Read(ref blockedProbe));
                ProbeSnapshot? result = null;
                try
                {
                    result = await observedProbe.Completion.WaitAsync(InputDeadline, TimeProvider.System, deadline.Token);
                }
                catch (TimeoutException)
                {
                    output.WriteLine("No complete source event sequence arrived after the blocked UI window; the old hook may have exceeded LowLevelHooksTimeout.");
                }

                output.WriteLine(result is null
                    ? $"Blocked UI: kind={(keyboard ? "keyboard" : "mouse")}, received={observedProbe.Received}, installing thread={installingThread}."
                    : $"Blocked UI: kind={(keyboard ? "keyboard" : "mouse")}, events={result.Events}, first latency={result.FirstLatencyMs:F2}ms, last latency={result.LastLatencyMs:F2}ms, all while UI blocked={result.AllWhileUiBlocked}, all off UI={result.AllOffUiThread}, consumer thread={result.LastThread}, installing thread={installingThread}.");
                Assert.NotNull(result);
                Assert.True(result.AllWhileUiBlocked, "F-008: source events were delayed until the installing UI thread resumed.");
                Assert.True(result.AllOffUiThread, "Source event processing must run independently of the installing UI thread.");
                Assert.True(result.LastLatencyMs < 1000, $"F-008: source delivery took {result.LastLatencyMs:F2}ms during a two-second UI blockage.");

                if (keyboard)
                {
                    await keyboardSource.StopAsync(deadline.Token);
                    Assert.False(keyboardSource.IsRunning);
                }
                else
                {
                    await mouseSource.StopAsync(deadline.Token);
                    Assert.False(mouseSource.IsRunning);
                }
            }
            finally
            {
                Volatile.Write(ref activeProbe, null);
                try
                {
                    try
                    {
                        if (keyboard) { await keyboardSource.StopAsync(); }
                        else { await mouseSource.StopAsync(); }
                    }
                    catch (Exception exception)
                    {
                        // Preserve the delivery assertion when the old timed-out hook is already gone.
                        output.WriteLine($"Final hook cleanup reported {exception.GetType().Name}: {exception.Message}");
                    }
                }
                finally
                {
                    if (keyboardAttempted) { NativeInput.ReleaseTestOwnedF24(); }
                    NativeInput.RestoreCursor(originalCursor);
                    output.WriteLine("Test-owned input and original cursor cleanup completed after the assertions.");
                }
            }
        });
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
                    try { await test(); }
                    catch (Exception exception) { failure = exception; }
                    finally { host.Close(); }
                };
                System.Windows.Forms.Application.Run(host);
            }
            catch (Exception exception) { failure = exception; }
            if (failure is null) { completion.TrySetResult(); }
            else { completion.TrySetException(failure); }
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(35), TimeProvider.System);
    }

    private sealed class UiBlockState
    {
        private int _blocked;
        internal bool IsBlocked => Volatile.Read(ref _blocked) != 0;
        internal void SetBlocked(bool blocked) => Volatile.Write(ref _blocked, blocked ? 1 : 0);
    }

    private sealed class ProbeCapture(int installingThread, int expectedEvents)
    {
        private readonly long _started = TimeProvider.System.GetTimestamp();
        private readonly TaskCompletionSource<ProbeSnapshot> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _received;
        private double _firstLatencyMs;
        private bool _allWhileUiBlocked = true;
        private bool _allOffUiThread = true;

        internal Task<ProbeSnapshot> Completion => _completion.Task;
        internal int Received => Volatile.Read(ref _received);

        internal void Observe(bool uiBlocked)
        {
            int received = Interlocked.Increment(ref _received);
            int thread = Environment.CurrentManagedThreadId;
            double elapsed = TimeProvider.System.GetElapsedTime(_started).TotalMilliseconds;
            if (received == 1) { _firstLatencyMs = elapsed; }
            _allWhileUiBlocked &= uiBlocked;
            _allOffUiThread &= thread != installingThread;
            if (received == expectedEvents)
            {
                _completion.TrySetResult(new ProbeSnapshot(received, _firstLatencyMs, elapsed,
                    _allWhileUiBlocked, _allOffUiThread, thread));
            }
        }
    }

    private sealed record ProbeSnapshot(int Events, double FirstLatencyMs, double LastLatencyMs,
        bool AllWhileUiBlocked, bool AllOffUiThread, int LastThread);

    private static class NativeInput
    {
        internal const int VirtualKeyF24 = 0x87;
        private static readonly IntPtr InputMarker = new(0x4D4B5233);

        internal static Point ReadCursor()
        {
            HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
            Assert.True(GetCursorPos(out Point point), $"GetCursorPos failed: Win32 {Marshal.GetLastPInvokeError()}.");
            return point;
        }

        internal static void RestoreCursor(Point point)
        {
            HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
            Assert.True(SetCursorPos(point.X, point.Y), $"SetCursorPos cleanup failed: Win32 {Marshal.GetLastPInvokeError()}.");
        }

        internal static bool IsF24Held()
        {
            HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
            return (GetAsyncKeyState(VirtualKeyF24) & 0x8000) != 0;
        }

        internal static void SendProbe(bool keyboard)
        {
            HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
            Send(keyboard ? [Keyboard(false), Keyboard(true)] : [Mouse(1), Mouse(-1)]);
        }

        internal static void ReleaseTestOwnedF24()
        {
            HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
            Send([Keyboard(true)]);
        }

        private static void Send(Input[] inputs)
        {
            HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
            uint accepted = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
            Assert.True(accepted == inputs.Length,
                $"SendInput accepted {accepted}/{inputs.Length}: Win32 {Marshal.GetLastPInvokeError()}.");
        }

        private static Input Keyboard(bool up) => new()
        {
            Type = 1,
            Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = VirtualKeyF24, Flags = up ? 2u : 0u, ExtraInfo = InputMarker } }
        };

        private static Input Mouse(int delta) => new()
        {
            Type = 0,
            Data = new InputUnion { Mouse = new MouseInput { X = delta, Flags = 0x0001 | 0x2000, ExtraInfo = InputMarker } }
        };

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, Input[] inputs, int size);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out Point point);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Point { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct Input { public uint Type; public InputUnion Data; }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public KeyboardInput Keyboard;
            [FieldOffset(0)] public MouseInput Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }
    }
}
