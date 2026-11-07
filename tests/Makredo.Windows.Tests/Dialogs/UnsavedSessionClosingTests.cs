using System.Drawing.Imaging;
using Makredo.Application.Abstractions;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Makredo.Infrastructure.Persistence;
using Makredo.Windows.Tests.Hooks;
using Makredo.Windows.Tests.Playback;
using Makredo.WinForms.Composition;
using Makredo.WinForms.Forms;
using Xunit;

namespace Makredo.Windows.Tests.Dialogs;

[Collection("Native input")]
public sealed class UnsavedSessionClosingTests
{
    private const string PromptCaption = "Kaydedilmemiş değişiklikler";
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(6);

    [HostedWindowsInputFact]
    public Task T4ClosingAnUnsavedRecordingPromptsAndCancelKeepsItOpen()
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        return HookThreadIntegrationTests.RunOnStaAsync(async () =>
        {
            DirectoryInfo directory = Directory.CreateTempSubdirectory("Makredo.UnsavedSession.");
            try
            {
                using var root = AppCompositionRoot.Create(AppStoragePaths.FromRootDirectory(directory.FullName));
                using var form = root.CreateMainForm();
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                form.FormClosed += (_, _) => closed.TrySetResult();
                await ShowInitializedAsync(form, root.HotkeyService);

                MacroSession recording = await RecordTwoEventsAsync(root.MacroRecorderService);

                Assert.True(await AnswerClosePromptAsync(form, DialogResult.Cancel, capture: true),
                    "The window closed before offering to save its unsaved recording.");
                Assert.False(form.IsDisposed);
                Assert.True(form.Visible);
                Assert.Same(recording, root.MacroRecorderService.CurrentSession);

                Assert.True(await AnswerClosePromptAsync(form, DialogResult.No, capture: false),
                    "Cancel must keep the unsaved recording available for the next close.");
                await closed.Task.WaitAsync(Deadline, TimeProvider.System);
            }
            finally
            {
                directory.Delete(recursive: true);
            }
        });
    }

    private static async Task ShowInitializedAsync(Form form, IHotkeyService hotkeys)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler idle = (_, _) =>
        {
            if (hotkeys.IsRegistered || hotkeys.RegistrationFailures.Count > 0)
            {
                ready.TrySetResult();
            }
        };
        System.Windows.Forms.Application.Idle += idle;
        try
        {
            form.Show();
            await ready.Task.WaitAsync(Deadline, TimeProvider.System);
        }
        finally
        {
            System.Windows.Forms.Application.Idle -= idle;
        }
    }

    private static async Task<MacroSession> RecordTwoEventsAsync(IMacroRecorderService recorder)
    {
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Observe(MacroEvent item)
        {
            if (item is { KeyCode: HookThreadIntegrationTests.NativeInput.VirtualKeyF24, KeyboardActionType: KeyboardActionType.KeyUp })
            {
                released.TrySetResult();
            }
        }

        Assert.False(HookThreadIntegrationTests.NativeInput.IsF24Held());
        recorder.EventRecorded += Observe;
        try
        {
            await recorder.StartAsync("Kaydedilmemiş kayıt");
            try
            {
                HookThreadIntegrationTests.NativeInput.SendProbe(keyboard: true);
                await released.Task.WaitAsync(Deadline, TimeProvider.System);
            }
            finally
            {
                await recorder.StopAsync();
            }
        }
        finally
        {
            recorder.EventRecorded -= Observe;
            if (HookThreadIntegrationTests.NativeInput.IsF24Held())
            {
                HookThreadIntegrationTests.NativeInput.ReleaseTestOwnedF24();
            }
        }

        MacroSession session = Assert.IsType<MacroSession>(recorder.CurrentSession);
        Assert.Collection(session.Events.Where(item => item.KeyCode == HookThreadIntegrationTests.NativeInput.VirtualKeyF24),
            item => Assert.Equal(KeyboardActionType.KeyDown, item.KeyboardActionType),
            item => Assert.Equal(KeyboardActionType.KeyUp, item.KeyboardActionType));
        return session;
    }

    private static async Task<bool> AnswerClosePromptAsync(Form form, DialogResult choice, bool capture)
    {
        var prompt = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        FormClosedEventHandler closed = (_, _) => prompt.TrySetResult(false);
        form.FormClosed += closed;

        // The modal message loop dispatches the observer on the same STA as the dialog.
        using var observer = new System.Windows.Forms.Timer { Interval = 15 };
        observer.Tick += (_, _) =>
        {
            ThemedMessageDialog? dialog = System.Windows.Forms.Application.OpenForms
                .OfType<ThemedMessageDialog>()
                .FirstOrDefault(candidate => candidate.Visible && candidate.Owner == form);
            if (dialog is null)
            {
                return;
            }

            observer.Stop();
            try
            {
                Assert.Equal(PromptCaption, dialog.Text);
                ThemedDialogButton[] buttons = Descendants(dialog).OfType<ThemedDialogButton>().ToArray();
                Assert.Equal("Kaydet", Assert.Single(buttons, button => button.DialogResult == DialogResult.Yes).Text);
                Assert.Equal("Kaydetme", Assert.Single(buttons, button => button.DialogResult == DialogResult.No).Text);
                Assert.Equal("İptal", Assert.Single(buttons, button => button.DialogResult == DialogResult.Cancel).Text);
                if (capture)
                {
                    SaveScreenshot(dialog);
                }

                Assert.Single(buttons, button => button.DialogResult == choice).PerformClick();
                prompt.TrySetResult(true);
            }
            catch (Exception exception)
            {
                dialog.DialogResult = DialogResult.Cancel;
                prompt.TrySetException(exception);
            }
        };
        observer.Start();
        try
        {
            form.BeginInvoke(new Action(form.Close));
            return await prompt.Task.WaitAsync(Deadline, TimeProvider.System);
        }
        finally
        {
            form.FormClosed -= closed;
        }
    }

    private static IEnumerable<Control> Descendants(Control parent) =>
        parent.Controls.Cast<Control>().SelectMany(control => Descendants(control).Prepend(control));

    private static void SaveScreenshot(Form dialog)
    {
        string workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")
            ?? throw new InvalidOperationException("CI workspace is required.");
        string directory = Path.Combine(workspace, "artifacts", "test-results");
        Directory.CreateDirectory(directory);
        using var bitmap = new Bitmap(dialog.Width, dialog.Height);
        dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(directory, "unsaved-session-close.png"), ImageFormat.Png);
    }
}
