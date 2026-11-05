using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Makredo.Application.Abstractions;
using Makredo.Domain.Models;
using Makredo.WinForms.Controls;
using Makredo.WinForms.Forms;
using Makredo.WinForms.Theme;
using Xunit;

namespace Makredo.Windows.Tests;

public sealed class DialogLayoutTests
{
    [Fact(DisplayName = "HotkeySettingsDialog exposes all controls within the dialog bounds")]
    public async Task HotkeySettingsDialog_UsesStableLayout()
    {
        _ = await RunOnStaThreadAsync(() =>
        {
            using var dialog = new HotkeySettingsDialog(HotkeySettings.CreateDefault());
            dialog.CreateControl();
            dialog.PerformLayout();

            Assert.True(
                dialog.ClientSize.Width >= 600,
                $"Hotkey settings dialog should allocate enough width to show all selector columns. Actual client size: {dialog.ClientSize.Width}x{dialog.ClientSize.Height}.");
            Assert.True(
                dialog.ClientSize.Height >= 344,
                $"Hotkey settings dialog should allocate enough height to show all settings rows. Actual client size: {dialog.ClientSize.Width}x{dialog.ClientSize.Height}.");

            List<ModernSelect> modifierSelects = GetDescendants<ModernSelect>(dialog).ToList();
            Assert.Equal(4, modifierSelects.Count);

            List<HotkeyKeyInput> keyInputs = GetDescendants<HotkeyKeyInput>(dialog).ToList();
            Assert.Equal(4, keyInputs.Count);

            List<ThemedDialogButton> buttons = GetDescendants<ThemedDialogButton>(dialog).ToList();
            Assert.Equal(3, buttons.Count);

            foreach (Control control in modifierSelects
                .Cast<Control>()
                .Concat(keyInputs)
                .Concat(buttons))
            {
                Rectangle bounds = GetBoundsRelativeToAncestor(control, dialog);

                Assert.True(
                    bounds.Left >= 0 && bounds.Top >= 0,
                    $"{DescribeControl(control)} should remain within the dialog origin.");
                Assert.True(
                    bounds.Right <= dialog.ClientSize.Width,
                    $"{DescribeControl(control)} should be fully visible within the dialog width.");
                Assert.True(
                    bounds.Bottom <= dialog.ClientSize.Height,
                    $"{DescribeControl(control)} should be fully visible within the dialog height.");
            }
        });
    }

    [Fact(DisplayName = "MacroPreviewMapDialog keeps preview content within popup bounds")]
    public async Task MacroPreviewMapDialog_UsesStableLayout()
    {
        _ = await RunOnStaThreadAsync(() =>
        {
            using var dialog = new MacroPreviewMapDialog();
            dialog.CreateControl();
            dialog.PerformLayout();

            Size minimumClientSize = MacroPreviewMapDialog.ResolveMinimumClientSize();
            Assert.True(
                dialog.ClientSize.Width >= minimumClientSize.Width,
                $"Preview map dialog should allocate at least its minimum width. Actual client size: {dialog.ClientSize.Width}x{dialog.ClientSize.Height}.");
            Assert.True(
                dialog.ClientSize.Height >= minimumClientSize.Height,
                $"Preview map dialog should allocate at least its minimum height. Actual client size: {dialog.ClientSize.Width}x{dialog.ClientSize.Height}.");

            MacroPreviewMapControl mapControl = GetDescendants<MacroPreviewMapControl>(dialog).Single();
            Rectangle mapBounds = GetBoundsRelativeToAncestor(mapControl, dialog);
            Assert.True(
                mapBounds.Height >= DesignTokens.Scale(260),
                $"Preview map should retain enough vertical room after popup chrome is laid out. Actual map bounds: {mapBounds}.");

            foreach (Control control in GetDescendants<Control>(dialog).Where(control => control.Visible))
            {
                Rectangle bounds = GetBoundsRelativeToAncestor(control, dialog);
                Assert.True(
                    bounds.Left >= 0 && bounds.Top >= 0,
                    $"{DescribeControl(control)} should remain within the preview dialog origin.");
                Assert.True(
                    bounds.Right <= dialog.ClientSize.Width,
                    $"{DescribeControl(control)} should be fully visible within the preview dialog width.");
                Assert.True(
                    bounds.Bottom <= dialog.ClientSize.Height,
                    $"{DescribeControl(control)} should be fully visible within the preview dialog height.");
            }
        });
    }

    [Fact(DisplayName = "MacroPreviewMapDialog clamps its size to the working area")]
    public async Task MacroPreviewMapDialog_ClampsSizeToWorkingArea()
    {
        _ = await RunOnStaThreadAsync(() =>
        {
            Rectangle workingArea = new(
                0,
                0,
                DesignTokens.Scale(520),
                DesignTokens.Scale(380));
            int margin = DesignTokens.Scale(12);
            Size minimumClientSize = MacroPreviewMapDialog.ResolveMinimumClientSizeForWorkingArea(
                workingArea,
                margin);
            Size clientSize = MacroPreviewMapDialog.ResolveClientSizeForWorkingArea(
                workingArea,
                margin,
                minimumClientSize);

            int availableWidth = workingArea.Width - (margin * 2);
            int availableHeight = workingArea.Height - (margin * 2);

            Assert.True(
                clientSize.Width <= availableWidth,
                $"Preview map dialog width should clamp to the working area. Client={clientSize.Width}, Available={availableWidth}.");
            Assert.True(
                clientSize.Height <= availableHeight,
                $"Preview map dialog height should clamp to the working area. Client={clientSize.Height}, Available={availableHeight}.");
            Assert.True(
                clientSize.Width >= minimumClientSize.Width,
                "Preview map dialog width should not shrink below its working-area-adjusted minimum.");
            Assert.True(
                clientSize.Height >= minimumClientSize.Height,
                "Preview map dialog height should not shrink below its working-area-adjusted minimum.");
        });
    }

    private static Task<bool> RunOnStaThreadAsync(Action action)
    {
        var taskCompletionSource = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Thread staThread = new(() =>
        {
            try
            {
                action();
                taskCompletionSource.SetResult(true);
            }
            catch (Exception ex)
            {
                taskCompletionSource.SetException(ex);
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.IsBackground = true;
        staThread.Start();

        return taskCompletionSource.Task;
    }

    private static IEnumerable<TControl> GetDescendants<TControl>(Control root)
        where TControl : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is TControl typedChild)
            {
                yield return typedChild;
            }

            foreach (TControl descendant in GetDescendants<TControl>(child))
            {
                yield return descendant;
            }
        }
    }

    private static Rectangle GetBoundsRelativeToAncestor(Control control, Control ancestor)
    {
        int left = control.Left;
        int top = control.Top;
        Control? current = control.Parent;

        while (current is not null && current != ancestor)
        {
            left += current.Left;
            top += current.Top;
            current = current.Parent;
        }

        if (current != ancestor)
        {
            throw new InvalidOperationException("The requested control is not inside the expected ancestor.");
        }

        return new Rectangle(left, top, control.Width, control.Height);
    }

    private static string DescribeControl(Control control)
    {
        string controlText = string.IsNullOrWhiteSpace(control.Text)
            ? control.GetType().Name
            : control.Text;

        return $"{control.GetType().Name} '{controlText}'";
    }
}
