using System.Drawing.Imaging;
using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Domain.Models;
using Makredo.Platform.Windows.Hooks;
using Makredo.Windows.Tests.Playback;
using Makredo.WinForms.Controls;
using Xunit;
using Xunit.Abstractions;

namespace Makredo.Windows.Tests.Hooks;

[Collection("Native input")]
public sealed class InjectedSettingsTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(0, 0, 100, 327)]
    [InlineData(99, 0, 100, 65208)]
    [InlineData(-100, -100, 100, 327)]
    [InlineData(int.MinValue, 0, 100, 327)]
    [InlineData(int.MaxValue, 0, 100, 65208)]
    public void AbsoluteCoordinatesUseVirtualDesktopPixelCenters(int coordinate, int origin, int size, int expected)
        => Assert.Equal(expected, Makredo.Platform.Windows.Interop.WindowsInputPlaybackAdapter.NormalizeCoordinate(coordinate, origin, size));

    [Fact]
    public void SettingIsVisibleDefaultsOnAndCanBeChanged()
    {
        using var control = new PlaybackSettingsControl { Size = new Size(720, 440) };
        control.CreateControl();
        control.PerformLayout();
        Assert.True(control.GetCurrentSettings().RecordInjectedInput);
        control.ApplySettings(new PlaybackSettings { RecordInjectedInput = false });
        Assert.False(control.GetCurrentSettings().RecordInjectedInput);
        Control option = Assert.Single(control.Controls.Find("recordInjectedInputCheckBox", true));
        Assert.True(option.Visible);
        int changed = 0;
        control.SettingsChanged += (_, _) => changed++;
        ((ModernCheckBox)option).Checked = true;
        Assert.True(control.GetCurrentSettings().RecordInjectedInput);
        Assert.Equal(1, changed);
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
        {
            string workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? throw new InvalidOperationException("CI workspace is required.");
            string directory = Path.Combine(workspace, "artifacts", "test-results");
            Directory.CreateDirectory(directory);
            using var bitmap = new Bitmap(control.Width, control.Height);
            control.DrawToBitmap(bitmap, control.ClientRectangle);
            bitmap.Save(Path.Combine(directory, "injected-settings.png"), ImageFormat.Png);
        }
    }

    [HostedWindowsInputFact]
    public async Task ForeignKeyboardAndMouseRespectTheRecordingSwitch()
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        var (keyboard, mouse) = WindowsHookSources.Create();
        await using var keyOwner = keyboard;
        await using var mouseOwner = mouse;
        var recorder = new MacroRecorderService(keyboard, mouse, new ApplicationStateService(), new MutableHotkeyConfiguration());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15), TimeProvider.System);
        foreach (bool enabled in new[] { false, true })
        {
            output.WriteLine($"Foreign injected keyboard/mouse recording enabled={enabled}.");
            recorder.RecordInjectedInput = enabled;
            var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int events = 0;
            void Key(KeyboardActivityInfo item) { if (item.ExtraInfo == 0x4D4B5233 && item.IsInjected && Interlocked.Increment(ref events) == 4) { arrived.TrySetResult(); } }
            void Mouse(MouseActivityInfo item) { if (item.ExtraInfo == 0x4D4B5233 && item.IsInjected && Interlocked.Increment(ref events) == 4) { arrived.TrySetResult(); } }
            keyboard.KeyActivityReceived += Key;
            mouse.TimestampedMouseActivityReceived += Mouse;
            try
            {
                await recorder.StartAsync("Foreign input policy", deadline.Token);
                HookThreadIntegrationTests.NativeInput.SendProbe(true);
                HookThreadIntegrationTests.NativeInput.SendProbe(false);
                await arrived.Task.WaitAsync(deadline.Token);
                await recorder.StopAsync(deadline.Token);
                Assert.Equal(enabled ? 2 : 0, recorder.CurrentSession!.Events.Count(item => item.KeyCode == 0x87));
                Assert.Equal(enabled ? 2 : 0, recorder.CurrentSession.Events.Count(item => item.EventType == Makredo.Domain.Enums.MacroEventType.Mouse));
            }
            finally
            {
                await recorder.StopAsync();
                keyboard.KeyActivityReceived -= Key;
                mouse.TimestampedMouseActivityReceived -= Mouse;
                HookThreadIntegrationTests.NativeInput.ReleaseTestOwnedF24();
            }
        }
    }
}
