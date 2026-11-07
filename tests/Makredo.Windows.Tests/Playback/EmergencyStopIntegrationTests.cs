using System.Runtime.InteropServices;
using Makredo.Application.Abstractions;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Makredo.Infrastructure.Persistence;
using Makredo.Platform.Windows.Hooks;
using Makredo.Platform.Windows.Interop;
using Makredo.Windows.Tests.Hooks;
using Makredo.WinForms.Composition;
using Xunit;

namespace Makredo.Windows.Tests.Playback;

[Collection("Native input")]
public sealed class EmergencyStopIntegrationTests
{
    private const ushort Escape = 0x1B;
    private const ushort LeftShift = 0xA0;
    private const ushort DelayedKey = HookThreadIntegrationTests.NativeInput.VirtualKeyF24;
    private static readonly TimeSpan InputDeadline = TimeSpan.FromSeconds(5);

    [HostedWindowsInputFact]
    [Trait("Category", "NativeInput")]
    public Task UnmarkedEscapeStopsAndReleasesPlaybackWhileItsUiThreadIsBlocked()
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        return HookThreadIntegrationTests.RunOnStaAsync(async () =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25), TimeProvider.System);
            DirectoryInfo temporaryStorage = Directory.CreateTempSubdirectory("Makredo.EmergencyStop.");
            try
            {
                using var composition = AppCompositionRoot.Create(AppStoragePaths.FromRootDirectory(temporaryStorage.FullName));
                await using var playback = composition.MacroPlaybackService;
                await using var observer = new WindowsKeyboardHookSource();
                var signedEscapeDown = NewSignal();
                var signedEscapeUp = NewSignal();
                var unmarkedEscapeUp = NewSignal();
                var signedShiftUp = NewSignal();
                observer.KeyActivityReceived += activity =>
                {
                    if (!activity.IsInjected)
                    {
                        return;
                    }

                    if (activity is { VirtualKeyCode: Escape, ExtraInfo: InputOriginPolicy.PlaybackSignature })
                    {
                        (activity.IsKeyDown ? signedEscapeDown : signedEscapeUp).TrySetResult();
                    }

                    if (activity is { VirtualKeyCode: Escape, IsKeyDown: false, ExtraInfo: 0 })
                    {
                        unmarkedEscapeUp.TrySetResult();
                    }

                    if (activity is { VirtualKeyCode: LeftShift, IsKeyDown: false, ExtraInfo: InputOriginPolicy.PlaybackSignature })
                    {
                        signedShiftUp.TrySetResult();
                    }
                };

                await observer.StartAsync(deadline.Token);
                Assert.False(IsHeld(LeftShift));
                Assert.False(IsHeld(Escape));
                Assert.False(IsHeld(DelayedKey));

                var playedEscape = NewSignal();
                var stopped = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                int playedEvents = 0;
                playback.EventPlayed += item =>
                {
                    Interlocked.Increment(ref playedEvents);
                    if (item is { KeyCode: Escape, KeyboardActionType: KeyboardActionType.KeyUp })
                    {
                        playedEscape.TrySetResult();
                    }
                };
                playback.PlaybackStopped += () => stopped.TrySetResult(Environment.CurrentManagedThreadId);

                var session = new MacroSession { Name = "Emergency stop while waiting" };
                session.Events.Add(Key(LeftShift, KeyboardActionType.KeyDown, scanCode: 0x2A));
                session.Events.Add(Key(Escape, KeyboardActionType.KeyDown, scanCode: 0x01));
                session.Events.Add(Key(Escape, KeyboardActionType.KeyUp, scanCode: 0x01));
                session.Events.Add(Key(DelayedKey, KeyboardActionType.KeyDown, delayMs: 600_000));

                int uiThread = Environment.CurrentManagedThreadId;
                Task? play = null;
                try
                {
                    play = playback.PlayAsync(session, new PlaybackSettings
                    {
                        SimulationMode = false,
                        PreserveOriginalTiming = true,
                        StopOnError = true
                    }, deadline.Token);

                    await Task.WhenAll(signedEscapeDown.Task, signedEscapeUp.Task, playedEscape.Task)
                        .WaitAsync(InputDeadline, TimeProvider.System, deadline.Token);
                    Assert.True(playback.IsPlaying, "Makredo's signed Esc must not stop playback.");
                    Assert.True(IsHeld(LeftShift), "Playback must hold left Shift before emergency stop.");

                    using var uiBlocked = new ManualResetEvent(false);
                    using var releaseUi = new ManualResetEvent(false);
                    Task sender = Task.Run(async () =>
                    {
                        try
                        {
                            Assert.True(uiBlocked.WaitOne(InputDeadline), "SendInput must start after the STA enters its blocking wait.");
                            Assert.True(playback.IsPlaying, "Playback stopped before the unmarked Esc was sent.");
                            SendKeys([Input(Escape), Input(Escape, keyUp: true)]);
                            await unmarkedEscapeUp.Task.WaitAsync(InputDeadline, TimeProvider.System, deadline.Token);
                            int stoppedThread = await stopped.Task.WaitAsync(InputDeadline, TimeProvider.System, deadline.Token);
                            await signedShiftUp.Task.WaitAsync(InputDeadline, TimeProvider.System, deadline.Token);

                            Assert.NotEqual(uiThread, stoppedThread);
                            Assert.Equal(AppState.Idle, composition.ApplicationStateService.CurrentState);
                            // Windows updates asynchronous key state after the low-level hook returns.
                            Assert.True(SpinWait.SpinUntil(() => !IsHeld(LeftShift) && !IsHeld(Escape), TimeSpan.FromSeconds(1)),
                                "Playback's held Shift and the injected Esc must be released before the UI unblocks.");
                            Assert.False(IsHeld(DelayedKey), "The event after the long delay must not be sent.");
                        }
                        finally
                        {
                            releaseUi.Set();
                        }
                    });

                    Assert.True(HookThreadIntegrationTests.NativeInput.BlockUiWithoutPumping(uiBlocked, releaseUi));
                    await sender;
                    await play.WaitAsync(InputDeadline, TimeProvider.System, deadline.Token);
                    Assert.Equal(3, Volatile.Read(ref playedEvents));
                }
                finally
                {
                    try
                    {
                        await playback.StopAsync().WaitAsync(InputDeadline, TimeProvider.System);
                        if (play is not null)
                        {
                            await play.WaitAsync(InputDeadline, TimeProvider.System);
                        }
                    }
                    finally
                    {
                        // Clean up only test-owned input, after checking the real stop path.
                        if (IsHeld(LeftShift)) { SendKeys([Input(LeftShift, keyUp: true)]); }
                        if (IsHeld(Escape)) { SendKeys([Input(Escape, keyUp: true)]); }
                        if (IsHeld(DelayedKey)) { SendKeys([Input(DelayedKey, keyUp: true)]); }
                    }
                }
            }
            finally
            {
                temporaryStorage.Delete(recursive: true);
            }
        });
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static MacroEvent Key(ushort key, KeyboardActionType action, int? scanCode = null, int delayMs = 0) => new()
    {
        EventType = MacroEventType.Keyboard,
        KeyboardActionType = action,
        KeyCode = key,
        ScanCode = scanCode,
        DelayMs = delayMs
    };

    private static NativeMethods.INPUT Input(ushort key, bool keyUp = false) => new()
    {
        type = NativeMethods.InputKeyboard,
        U = new NativeMethods.InputUnion
        {
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = key,
                dwFlags = keyUp ? NativeMethods.KeyEventFKeyUp : 0,
                dwExtraInfo = IntPtr.Zero
            }
        }
    };

    private static void SendKeys(NativeMethods.INPUT[] inputs)
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        uint accepted = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        Assert.True(accepted == inputs.Length,
            $"SendInput accepted {accepted}/{inputs.Length}. Win32 error {Marshal.GetLastPInvokeError()}.");
    }

    private static bool IsHeld(ushort key)
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        return (GetAsyncKeyState(key) & 0x8000) != 0;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
}
