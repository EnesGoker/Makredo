using System.Runtime.InteropServices;
using System.Text.Json;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Makredo.Infrastructure.Persistence;
using Makredo.Windows.Tests.Hooks;
using Makredo.WinForms.Composition;
using Makredo.WinForms.Forms;
using Xunit;

namespace Makredo.Windows.Tests.Dialogs;

public sealed class WindowsSessionEndTests
{
    [Fact]
    public Task QueryEndSessionPersistsRecoveryBeforeAllowingShutdown() => HookThreadIntegrationTests.RunOnStaAsync(() =>
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("Makredo.SessionEnd.");
        try
        {
            AppStoragePaths paths = AppStoragePaths.FromRootDirectory(directory.FullName);
            using var root = AppCompositionRoot.Create(paths);
            using var form = root.CreateMainForm();
            nint handle = form.Handle;
            var input = new MacroEvent
            {
                EventType = MacroEventType.Keyboard,
                KeyboardActionType = KeyboardActionType.KeyDown,
                KeyCode = 0x41
            };
            var session = new MacroSession { Name = "Unsaved session" };
            session.Events.Add(input);
            root.SessionDocument.StartRecording(session);
            Assert.True(root.SessionDocument.IsDirty);
            Assert.False(File.Exists(paths.SessionRecoveryFilePath));

            CloseReason? closeReason = null;
            bool publishedDuringClosing = false;
            form.FormClosing += (_, e) =>
            {
                closeReason = e.CloseReason;
                publishedDuringClosing = File.Exists(paths.SessionRecoveryFilePath);
            };
            bool promptShown = false;
            using var observer = new System.Windows.Forms.Timer { Interval = 15 };
            observer.Tick += (_, _) =>
            {
                ThemedMessageDialog? dialog = System.Windows.Forms.Application.OpenForms
                    .OfType<ThemedMessageDialog>()
                    .FirstOrDefault(candidate => !candidate.InvokeRequired && candidate.Visible);
                if (dialog is not null)
                {
                    promptShown = true;
                    dialog.DialogResult = DialogResult.Cancel;
                }
            };

            observer.Start();
            const uint queryEndSession = 0x0011;
            nint allowed = SendMessage(handle, queryEndSession, 0, 0);
            observer.Stop();

            Assert.Equal(CloseReason.WindowsShutDown, closeReason);
            Assert.False(promptShown, "The shutdown query must not enter a modal prompt.");
            Assert.NotEqual(nint.Zero, allowed);
            Assert.True(publishedDuringClosing, "The checkpoint must be published inside the shutdown query.");

            // Read the published file without yielding to an asynchronous UI continuation.
            string checkpoint = File.ReadAllText(paths.SessionRecoveryFilePath);
            MacroSession recovered = Assert.IsType<MacroSession>(JsonSerializer.Deserialize<MacroSession>(checkpoint));
            Assert.Equal(session.Id, recovered.Id);
            Assert.Equal(session.Name, recovered.Name);
            Assert.Equal(input.Id, Assert.Single(recovered.Events).Id);
        }
        finally
        {
            directory.Delete(recursive: true);
        }

        return Task.CompletedTask;
    });

    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    private static extern nint SendMessage(nint window, uint message, nuint wParam, nint lParam);
}
