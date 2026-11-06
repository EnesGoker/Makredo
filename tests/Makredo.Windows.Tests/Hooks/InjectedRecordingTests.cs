using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Makredo.Platform.Windows.Hooks;
using Makredo.Platform.Windows.Interop;
using Makredo.Windows.Tests.Playback;
using Xunit;
using Xunit.Abstractions;

namespace Makredo.Windows.Tests.Hooks;

[Collection("Native input")]
public sealed class InjectedRecordingTests(ITestOutputHelper output)
{
    [HostedWindowsInputFact]
    public async Task PlaybackIncludingCleanupIsExcludedButForeignInjectedInputIsRecorded()
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        var (keyboard, mouse) = WindowsHookSources.Create();
        await using var keyboardOwner = keyboard;
        await using var mouseOwner = mouse;
        var recorder = new MacroRecorderService(keyboard, mouse, new ApplicationStateService(), new MutableHotkeyConfiguration());
        var native = new WindowsInputPlaybackAdapter();
        await using var playback = new MacroPlaybackService(native, native, new ApplicationStateService());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15), TimeProvider.System);
        var keys = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var buttons = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int keyCount = 0, buttonCount = 0;
        keyboard.KeyActivityReceived += item => { if (item.VirtualKeyCode == 0x87 && ++keyCount == 2) { keys.TrySetResult(); } };
        mouse.MouseActivityReceived += (action, _, _, _) => { if (action is MouseActionType.MiddleDown or MouseActionType.MiddleUp && ++buttonCount == 2) { buttons.TrySetResult(); } };
        try
        {
            await recorder.StartAsync("Injected recording", deadline.Token);
            CursorPosition cursor = await native.GetCursorPositionAsync(deadline.Token);
            var session = new MacroSession();
            session.Events.Add(new MacroEvent { EventType = MacroEventType.Keyboard, KeyboardActionType = KeyboardActionType.KeyDown, KeyCode = 0x87, ScanCode = 0x76 });
            session.Events.Add(new MacroEvent { EventType = MacroEventType.Mouse, MouseActionType = MouseActionType.MiddleDown, X = cursor.X, Y = cursor.Y });
            HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
            await playback.PlayAsync(session, new PlaybackSettings(), deadline.Token);
            await Task.WhenAll(keys.Task, buttons.Task).WaitAsync(deadline.Token);
            await recorder.StopAsync(deadline.Token);
            Assert.Empty(recorder.CurrentSession!.Events);
            output.WriteLine("Real playback keyboard/button downs and cleanup ups were observed by sources and excluded from recording.");

            keys = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            keyCount = 0;
            await recorder.StartAsync("Foreign input", deadline.Token);
            HookThreadIntegrationTests.NativeInput.SendProbe(true);
            await keys.Task.WaitAsync(deadline.Token);
            await recorder.StopAsync(deadline.Token);
            Assert.Equal(2, recorder.CurrentSession!.Events.Count(item => item.KeyCode == 0x87));
            output.WriteLine("Foreign injected F24 down/up were recorded with the default setting.");
        }
        finally
        {
            await recorder.StopAsync();
            HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
            await native.ReleaseEventAsync(new MacroEvent { EventType = MacroEventType.Keyboard, KeyboardActionType = KeyboardActionType.KeyUp, KeyCode = 0x87, ScanCode = 0x76 });
            await native.ReleaseEventAsync(new MacroEvent { EventType = MacroEventType.Mouse, MouseActionType = MouseActionType.MiddleUp });
        }
    }
}
