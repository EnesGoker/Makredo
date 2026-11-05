namespace MacroMaster.WinForms.Forms;

internal static class ModalDialogOverlay
{
    private const double OverlayOpacity = 0.46d;

    public static DialogResult ShowDialog(IWin32Window? owner, Form dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        return ShowWithOptionalOverlay(
            owner,
            ownerForm => dialog.ShowDialog(ownerForm),
            () => owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner));
    }

    public static DialogResult ShowDialog(IWin32Window? owner, CommonDialog dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        return ShowWithOptionalOverlay(
            owner,
            ownerForm => dialog.ShowDialog(ownerForm),
            () => owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner));
    }

    public static DialogResult ShowMessage(
        IWin32Window? owner,
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        return ShowWithOptionalOverlay(
            owner,
            ownerForm => ThemedMessageDialog.Show(ownerForm, text, caption, buttons, icon),
            () => ThemedMessageDialog.Show(owner, text, caption, buttons, icon));
    }

    private static DialogResult ShowWithOptionalOverlay(
        IWin32Window? owner,
        Func<Form, DialogResult> showWithOwnerForm,
        Func<DialogResult> showWithoutOverlay)
    {
        if (!TryResolveOwnerForm(owner, out Form? ownerForm)
            || ownerForm is null
            || !CanShowOverlay(ownerForm))
        {
            return showWithoutOverlay();
        }

        using var overlay = new OverlayForm(ownerForm);
        overlay.Show(ownerForm);
        overlay.BringToFront();

        try
        {
            return showWithOwnerForm(ownerForm);
        }
        finally
        {
            if (!overlay.IsDisposed)
            {
                overlay.Close();
            }
        }
    }

    private static bool TryResolveOwnerForm(IWin32Window? owner, out Form? ownerForm)
    {
        ownerForm = owner as Form;

        if (ownerForm is not null)
        {
            return true;
        }

        if (owner is Control ownerControl)
        {
            ownerForm = ownerControl.FindForm();
            return ownerForm is not null;
        }

        return false;
    }

    private static bool CanShowOverlay(Form ownerForm)
    {
        return !ownerForm.IsDisposed
            && ownerForm.IsHandleCreated
            && ownerForm.Visible
            && ownerForm.WindowState != FormWindowState.Minimized;
    }

    private sealed class OverlayForm : Form
    {
        private readonly Form _ownerForm;

        public OverlayForm(Form ownerForm)
        {
            _ownerForm = ownerForm;

            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Black;
            FormBorderStyle = FormBorderStyle.None;
            Opacity = OverlayOpacity;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = false;

            UpdateBoundsFromOwner();
            SubscribeOwnerEvents();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                const int wsExNoActivate = 0x08000000;
                CreateParams createParams = base.CreateParams;
                createParams.ExStyle |= wsExNoActivate;
                return createParams;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnsubscribeOwnerEvents();
            }

            base.Dispose(disposing);
        }

        private void SubscribeOwnerEvents()
        {
            _ownerForm.LocationChanged += ownerForm_BoundsChanged;
            _ownerForm.SizeChanged += ownerForm_BoundsChanged;
            _ownerForm.Resize += ownerForm_Resize;
            _ownerForm.VisibleChanged += ownerForm_VisibleChanged;
            _ownerForm.FormClosed += ownerForm_FormClosed;
        }

        private void UnsubscribeOwnerEvents()
        {
            _ownerForm.LocationChanged -= ownerForm_BoundsChanged;
            _ownerForm.SizeChanged -= ownerForm_BoundsChanged;
            _ownerForm.Resize -= ownerForm_Resize;
            _ownerForm.VisibleChanged -= ownerForm_VisibleChanged;
            _ownerForm.FormClosed -= ownerForm_FormClosed;
        }

        private void ownerForm_BoundsChanged(object? sender, EventArgs e)
        {
            _ = sender;
            _ = e;
            UpdateBoundsFromOwner();
        }

        private void ownerForm_Resize(object? sender, EventArgs e)
        {
            _ = sender;
            _ = e;
            Visible = _ownerForm.WindowState != FormWindowState.Minimized && _ownerForm.Visible;
            UpdateBoundsFromOwner();
        }

        private void ownerForm_VisibleChanged(object? sender, EventArgs e)
        {
            _ = sender;
            _ = e;
            Visible = _ownerForm.Visible && _ownerForm.WindowState != FormWindowState.Minimized;
        }

        private void ownerForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            _ = sender;
            _ = e;
            Close();
        }

        private void UpdateBoundsFromOwner()
        {
            if (_ownerForm.IsDisposed || _ownerForm.WindowState == FormWindowState.Minimized)
            {
                return;
            }

            Bounds = _ownerForm.Bounds;
        }
    }
}
