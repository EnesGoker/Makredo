using MacroMaster.Application.Abstractions;
using MacroMaster.WinForms.Theme;
using System.Drawing.Drawing2D;

namespace MacroMaster.WinForms.Controls;

internal sealed class HotkeyKeyInput : Control
{
    private bool _isHovered;
    private bool _isPressed;
    private bool _isCapturing;
    private int _virtualKeyCode;

    public HotkeyKeyInput()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.UserPaint,
            true);

        BackColor = DesignTokens.SurfaceInset;
        Cursor = Cursors.Hand;
        Font = DesignTokens.FontUiNormal;
        ForeColor = DesignTokens.TextPrimary;
        MinimumSize = new Size(DesignTokens.Scale(120), DesignTokens.Scale(30));
        TabStop = true;
        AccessibleName = "Kısayol tuşu";
        AccessibleRole = AccessibleRole.Text;
    }

    public event EventHandler? VirtualKeyCodeChanged;

    public int VirtualKeyCode
    {
        get => _virtualKeyCode;
        set => SetVirtualKeyCode(value);
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

        if (e.Button == MouseButtons.Left && Enabled && ClientRectangle.Contains(e.Location))
        {
            BeginCapture();
        }

        base.OnMouseUp(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        EndCapture();
        base.OnLostFocus(e);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        if (_isCapturing)
        {
            return true;
        }

        Keys keyCode = keyData & Keys.KeyCode;
        return keyCode is Keys.Space or Keys.Enter || base.IsInputKey(keyData);
    }

    protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
    {
        if (_isCapturing)
        {
            e.IsInputKey = true;
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!Enabled)
        {
            base.OnKeyDown(e);
            return;
        }

        if (_isCapturing)
        {
            if (TrySetCapturedKey(e.KeyCode))
            {
                EndCapture();
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            BeginCapture();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        if (!Enabled)
        {
            EndCapture();
        }

        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? DesignTokens.Surface);

        Rectangle bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using GraphicsPath path = CreateRoundPath(bounds, DesignTokens.Scale(7));
        using var fillBrush = new SolidBrush(ResolveFillColor());
        using var borderPen = new Pen(ResolveBorderColor(), Math.Max(1f, DesignTokens.DensityScale));
        graphics.FillPath(fillBrush, path);
        graphics.DrawPath(borderPen, path);

        Rectangle textBounds = new(
            bounds.Left + DesignTokens.Scale(11),
            bounds.Top,
            Math.Max(0, bounds.Width - DesignTokens.Scale(22)),
            bounds.Height);

        TextRenderer.DrawText(
            graphics,
            ResolveDisplayText(),
            Font,
            textBounds,
            ResolveTextColor(),
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
    }

    private void BeginCapture()
    {
        if (_isCapturing)
        {
            return;
        }

        _isCapturing = true;
        Invalidate();
    }

    private void EndCapture()
    {
        if (!_isCapturing)
        {
            return;
        }

        _isCapturing = false;
        Invalidate();
    }

    private bool TrySetCapturedKey(Keys keyCode)
    {
        int virtualKeyCode = (int)(keyCode & Keys.KeyCode);
        if (!IsSupportedVirtualKeyCode(virtualKeyCode))
        {
            return false;
        }

        SetVirtualKeyCode(virtualKeyCode);
        return true;
    }

    private void SetVirtualKeyCode(int virtualKeyCode)
    {
        if (!IsSupportedVirtualKeyCode(virtualKeyCode))
        {
            throw new InvalidOperationException(
                $"Kısayol tuşu desteklenmiyor: {virtualKeyCode}.");
        }

        if (_virtualKeyCode == virtualKeyCode)
        {
            return;
        }

        _virtualKeyCode = virtualKeyCode;
        Invalidate();
        VirtualKeyCodeChanged?.Invoke(this, EventArgs.Empty);
    }

    private string ResolveDisplayText()
    {
        return _isCapturing
            ? "Tuşa bas..."
            : VirtualKeyDisplayNameFormatter.Format(_virtualKeyCode);
    }

    private Color ResolveTextColor()
    {
        if (!Enabled)
        {
            return DesignTokens.TextMuted;
        }

        return _isCapturing
            ? DesignTokens.Accent
            : ForeColor;
    }

    private Color ResolveFillColor()
    {
        if (!Enabled)
        {
            return Color.FromArgb(110, DesignTokens.SurfaceInset);
        }

        if (_isPressed || _isCapturing)
        {
            return DesignTokens.Surface3;
        }

        return _isHovered
            ? DesignTokens.SurfaceHover
            : DesignTokens.SurfaceInset;
    }

    private Color ResolveBorderColor()
    {
        if (!Enabled)
        {
            return Color.FromArgb(90, DesignTokens.BorderSoft);
        }

        if (Focused || _isCapturing)
        {
            return DesignTokens.Accent;
        }

        return _isHovered
            ? DesignTokens.BorderBright
            : DesignTokens.Border;
    }

    private static bool IsSupportedVirtualKeyCode(int virtualKeyCode)
    {
        return virtualKeyCode is > 0 and <= 0xFE
            && !HotkeySettingsValidator.UnsupportedPrimaryVirtualKeys.Contains(virtualKeyCode);
    }

    private static GraphicsPath CreateRoundPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));

        if (diameter <= 1)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new Rectangle(bounds.Left, bounds.Top, diameter, diameter);
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
