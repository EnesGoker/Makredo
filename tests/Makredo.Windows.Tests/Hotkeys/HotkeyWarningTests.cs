using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Makredo.Application.Abstractions;
using Makredo.Application.Hotkeys;
using Makredo.Infrastructure.Persistence;
using Makredo.Windows.Tests.Hooks;
using Makredo.Windows.Tests.Playback;
using Makredo.WinForms.Composition;
using Makredo.WinForms.Controls;
using Makredo.WinForms.Forms;
using Xunit;

namespace Makredo.Windows.Tests.Hotkeys;

[Collection("Native input")]
public sealed class HotkeyWarningTests
{
    [HostedWindowsInputFact]
    public Task MainFormShowsNonModalCollisionWarningAndDialogRetainsLegacyPreset() => HookThreadIntegrationTests.RunOnStaAsync(async () =>
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        string directory = Path.Combine(Path.GetTempPath(), "Makredo.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var conflict = new Form();
            Assert.True(RegisterHotKey(conflict.Handle, 77, 0, 0x77));
            using var root = AppCompositionRoot.Create(AppStoragePaths.FromRootDirectory(directory));
            await root.HotkeySettingsStore.SaveAsync(HotkeyDefaultsMigration.LegacyPreset());
            using var form = root.CreateMainForm();
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            HotkeyWarningBand band = Assert.Single(form.Controls.OfType<HotkeyWarningBand>());
            EventHandler idle = (_, _) => { if (band.Visible) { ready.TrySetResult(); } };
            System.Windows.Forms.Application.Idle += idle;
            try
            {
                form.Show();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System);
                Assert.True(form.Enabled);
                Assert.Contains("F8", band.Text, StringComparison.Ordinal);
                Assert.Contains("başka bir uygulama", band.Text, StringComparison.Ordinal);
                Assert.True(root.HotkeyService.IsHotkeyRegistered(HotkeyBinding.None(0x78)));
                Assert.False(root.HotkeyService.IsHotkeyRegistered(HotkeyBinding.None(0x7B)));
                Assert.True(form.ClientRectangle.Contains(band.Bounds));
                SaveScreenshot(form, "hotkey-warning.png");
                using var dialog = new HotkeySettingsDialog(HotkeySettings.CreateDefault());
                dialog.Show(); dialog.PerformLayout();
                Assert.Contains(Descendants(dialog), control => control.Text == "F8 / F9 / F10");
                SaveScreenshot(dialog, "hotkey-settings.png");
            }
            finally
            {
                System.Windows.Forms.Application.Idle -= idle;
                _ = UnregisterHotKey(conflict.Handle, 77);
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    });

    private static IEnumerable<Control> Descendants(Control parent) => parent.Controls.Cast<Control>().SelectMany(control => Descendants(control).Prepend(control));
    private static void SaveScreenshot(Form form, string name)
    {
        string path = Path.Combine(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")!, "artifacts", "test-results", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path, ImageFormat.Png);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
