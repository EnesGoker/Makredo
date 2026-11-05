using MacroMaster.WinForms.Theme;
using System.Drawing.Drawing2D;

namespace MacroMaster.WinForms.Forms;

internal enum ThemedDialogButtonStyle
{
    Secondary,
    Primary,
    Destructive
}

internal sealed class ThemedDialogButton : Control, IButtonControl
{
    private readonly ThemedDialogButtonStyle _style;
    private bool _isHovered;
    private bool _isPressed;
    private bool _isDefault;

    public ThemedDialogButton(ThemedDialogButtonStyle style)
    {
        _style = style;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.UserPaint,
            true);

        Cursor = Cursors.Hand;
        TabStop = true;
        Font = DesignTokens.FontUiBold;
        ForeColor = DesignTokens.TextPrimary;
        AccessibleRole = AccessibleRole.PushButton;
    }

    public DialogResult DialogResult { get; set; }

    public void NotifyDefault(bool value)
    {
        if (_isDefault == value)
        {
            return;
        }

        _isDefault = value;
        Invalidate();
    }

    public void PerformClick()
    {
        if (CanSelect)
        {
            OnClick(EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);

        if (DialogResult == DialogResult.None)
        {
            return;
        }

        Form? ownerForm = FindForm();
        if (ownerForm is null)
        {
            return;
        }

        ownerForm.DialogResult = DialogResult;
        ownerForm.Close();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _isHovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _isHovered = false;
        _isPressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && Enabled)
        {
            _isPressed = true;
            Focus();
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _isPressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        Keys keyCode = keyData & Keys.KeyCode;
        return keyCode is Keys.Space or Keys.Enter || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Enabled && e.KeyCode is Keys.Space or Keys.Enter)
        {
            PerformClick();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(ResolvePaintBackColor(Parent, DesignTokens.Surface));

        Rectangle bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using GraphicsPath path = CreateRoundedRectanglePath(bounds, DesignTokens.Scale(8));
        using var fillBrush = new SolidBrush(ResolveFillColor());
        using var borderPen = new Pen(ResolveBorderColor(), Math.Max(1f, DesignTokens.DensityScale));
        e.Graphics.FillPath(fillBrush, path);
        e.Graphics.DrawPath(borderPen, path);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            bounds,
            Enabled ? DesignTokens.TextPrimary : DesignTokens.TextMuted,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
    }

    private Color ResolveFillColor()
    {
        if (_isPressed)
        {
            return _style switch
            {
                ThemedDialogButtonStyle.Primary => DesignTokens.AccentDeep,
                ThemedDialogButtonStyle.Destructive => Color.FromArgb(108, DesignTokens.AccentRedSoft),
                _ => DesignTokens.Surface3
            };
        }

        if (_isHovered || Focused || _isDefault)
        {
            return _style switch
            {
                ThemedDialogButtonStyle.Primary => Color.FromArgb(232, DesignTokens.AccentDeep),
                ThemedDialogButtonStyle.Destructive => Color.FromArgb(88, DesignTokens.AccentRedSoft),
                _ => DesignTokens.SurfaceHover
            };
        }

        return _style switch
        {
            ThemedDialogButtonStyle.Primary => DesignTokens.AccentDeep,
            ThemedDialogButtonStyle.Destructive => DesignTokens.AccentRedSoft,
            _ => DesignTokens.Surface2
        };
    }

    private Color ResolveBorderColor()
    {
        return _style switch
        {
            ThemedDialogButtonStyle.Primary => DesignTokens.Accent,
            ThemedDialogButtonStyle.Destructive => DesignTokens.AccentRed,
            _ => DesignTokens.BorderBright
        };
    }

    private static Color ResolvePaintBackColor(Control? control, Color fallback)
    {
        while (control is not null)
        {
            Color backColor = control.BackColor;
            if (backColor.A == byte.MaxValue)
            {
                return backColor;
            }

            control = control.Parent;
        }

        return fallback;
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }
}
