using MacroMaster.WinForms.Theme;
using System.Drawing.Drawing2D;

namespace MacroMaster.WinForms.Controls;

internal sealed class ModernCheckBox : Control
{
    private bool _isHovered;
    private bool _isPressed;
    private bool _checked;

    public ModernCheckBox()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        AccessibleRole = AccessibleRole.CheckButton;
        Cursor = Cursors.Hand;
        TabStop = true;
        BackColor = DesignTokens.Surface;
        ForeColor = DesignTokens.TextPrimary;
        MinimumSize = new Size(DesignTokens.Scale(120), DesignTokens.Scale(30));
    }

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseEnter(EventArgs eventargs)
    {
        _isHovered = true;
        Invalidate();
        base.OnMouseEnter(eventargs);
    }

    protected override void OnMouseLeave(EventArgs eventargs)
    {
        _isHovered = false;
        _isPressed = false;
        Invalidate();
        base.OnMouseLeave(eventargs);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (Enabled && mevent.Button == MouseButtons.Left)
        {
            _isPressed = true;
            Focus();
            Invalidate();
        }

        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        bool shouldToggle = Enabled &&
            _isPressed &&
            mevent.Button == MouseButtons.Left &&
            ClientRectangle.Contains(mevent.Location);

        _isPressed = false;
        Invalidate();

        if (shouldToggle)
        {
            Checked = !Checked;
        }

        base.OnMouseUp(mevent);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
        {
            Checked = !Checked;
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(Parent?.BackColor ?? DesignTokens.Surface);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        Graphics graphics = pevent.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle rowBounds = Rectangle.Inflate(ClientRectangle, -1, -1);
        using GraphicsPath rowPath = CreateRoundPath(rowBounds, DesignTokens.Scale(8));
        using var rowBrush = new SolidBrush(ResolveRowFillColor());
        using var rowBorderPen = new Pen(ResolveRowBorderColor(), Math.Max(1f, DesignTokens.DensityScale));
        graphics.FillPath(rowBrush, rowPath);
        graphics.DrawPath(rowBorderPen, rowPath);

        int boxSize = DesignTokens.Scale(16);
        var boxBounds = new Rectangle(
            DesignTokens.Scale(12),
            Math.Max(0, (Height - boxSize) / 2),
            boxSize,
            boxSize);

        using GraphicsPath boxPath = CreateRoundPath(boxBounds, DesignTokens.Scale(4));
        using var fillBrush = new SolidBrush(ResolveBoxFillColor());
        using var borderPen = new Pen(ResolveBorderColor(), Math.Max(1f, DesignTokens.DensityScale));
        graphics.FillPath(fillBrush, boxPath);
        graphics.DrawPath(borderPen, boxPath);

        if (Checked)
        {
            DrawCheckMark(graphics, boxBounds);
        }

        var textBounds = new Rectangle(
            boxBounds.Right + DesignTokens.Scale(12),
            0,
            Math.Max(0, Width - boxBounds.Right - DesignTokens.Scale(20)),
            Height);
        TextRenderer.DrawText(
            graphics,
            Text,
            Font,
            textBounds,
            Enabled ? ForeColor : DesignTokens.TextMuted,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
    }

    private Color ResolveRowFillColor()
    {
        if (!Enabled)
        {
            return Color.FromArgb(90, DesignTokens.SurfaceInset);
        }

        if (_isPressed)
        {
            return DesignTokens.Surface3;
        }

        if (_isHovered || Focused)
        {
            return DesignTokens.SurfaceHover;
        }

        return DesignTokens.SurfaceInset;
    }

    private Color ResolveRowBorderColor()
    {
        if (!Enabled)
        {
            return Color.FromArgb(70, DesignTokens.BorderSoft);
        }

        if (Focused)
        {
            return DesignTokens.BorderBright;
        }

        return _isHovered
            ? DesignTokens.Border
            : DesignTokens.BorderSoft;
    }

    private Color ResolveBoxFillColor()
    {
        if (!Enabled)
        {
            return Color.FromArgb(70, DesignTokens.SurfaceInset);
        }

        if (Checked)
        {
            return _isPressed
                ? DesignTokens.AccentDeep
                : DesignTokens.Accent;
        }

        if (_isHovered)
        {
            return DesignTokens.SurfaceHover;
        }

        return DesignTokens.SurfaceInset;
    }

    private Color ResolveBorderColor()
    {
        if (!Enabled)
        {
            return Color.FromArgb(80, DesignTokens.BorderSoft);
        }

        if (Checked)
        {
            return DesignTokens.Accent;
        }

        return _isHovered
            ? DesignTokens.BorderBright
            : DesignTokens.BorderSoft;
    }

    private static void DrawCheckMark(Graphics graphics, Rectangle boxBounds)
    {
        using var checkPen = new Pen(DesignTokens.TextPrimary, Math.Max(1.7f, DesignTokens.DensityScale * 1.6f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        Point point1 = new(boxBounds.Left + boxBounds.Width / 4, boxBounds.Top + boxBounds.Height / 2);
        Point point2 = new(boxBounds.Left + boxBounds.Width / 2 - DesignTokens.Scale(1), boxBounds.Bottom - boxBounds.Height / 4);
        Point point3 = new(boxBounds.Right - boxBounds.Width / 5, boxBounds.Top + boxBounds.Height / 4);
        graphics.DrawLines(checkPen, new[] { point1, point2, point3 });
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
