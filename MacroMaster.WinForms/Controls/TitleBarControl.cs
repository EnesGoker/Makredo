using MacroMaster.WinForms.Theme;
using System.Drawing.Drawing2D;

namespace MacroMaster.WinForms.Controls;

internal sealed class TitleBarControl : UserControl
{
    private readonly Label _appNameLabel;
    private readonly LogoPanel _logoPanel;
    private readonly StatusPillControl _statusPill;
    private readonly CaptionButton _minimizeButton;
    private readonly CaptionButton _maximizeButton;
    private readonly CaptionButton _closeButton;
    private readonly TableLayoutPanel _rootLayoutPanel;
    private readonly ColumnStyle _logoColumnStyle;
    private readonly ColumnStyle _statusColumnStyle;
    private readonly ColumnStyle _minimizeColumnStyle;
    private readonly ColumnStyle _maximizeColumnStyle;
    private readonly ColumnStyle _closeColumnStyle;

    public TitleBarControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        DoubleBuffered = true;
        BackColor = DesignTokens.Background;
        ForeColor = DesignTokens.TextPrimary;
        Font = DesignTokens.FontUiNormal;
        MinimumSize = new Size(0, DesignTokens.TitleBarHeight);

        _rootLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1,
            BackColor = DesignTokens.Background,
            Margin = Padding.Empty,
            Padding = new Padding(
                0,
                0,
                DesignTokens.Scale(10),
                DesignTokens.Scale(2))
        };

        _logoColumnStyle = new ColumnStyle(SizeType.Absolute, DesignTokens.TitleBarIconSize + DesignTokens.Scale(8));
        _statusColumnStyle = new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(132));
        _minimizeColumnStyle = new ColumnStyle(SizeType.Absolute, DesignTokens.TitleBarButtonWidth);
        _maximizeColumnStyle = new ColumnStyle(SizeType.Absolute, DesignTokens.TitleBarButtonWidth);
        _closeColumnStyle = new ColumnStyle(SizeType.Absolute, DesignTokens.TitleBarButtonWidth);

        _rootLayoutPanel.ColumnStyles.Add(_logoColumnStyle);
        _rootLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _rootLayoutPanel.ColumnStyles.Add(_statusColumnStyle);
        _rootLayoutPanel.ColumnStyles.Add(_minimizeColumnStyle);
        _rootLayoutPanel.ColumnStyles.Add(_maximizeColumnStyle);
        _rootLayoutPanel.ColumnStyles.Add(_closeColumnStyle);
        _rootLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(2)));

        _logoPanel = new LogoPanel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Background,
            Margin = new Padding(0, 0, DesignTokens.Scale(8), 0)
        };

        var textLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = DesignTokens.Background,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        textLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _appNameLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Polly Kontrol Merkezi",
            Font = DesignTokens.FontUiLarge,
            ForeColor = DesignTokens.TextPrimary,
            BackColor = DesignTokens.Background,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        textLayoutPanel.Controls.Add(_appNameLabel, 0, 0);

        _statusPill = new StatusPillControl
        {
            Dock = DockStyle.Fill,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(DesignTokens.Scale(6), DesignTokens.Scale(2), DesignTokens.Scale(8), DesignTokens.Scale(2)),
            BackColor = Color.Transparent,
        };

        _minimizeButton = new CaptionButton(CaptionButtonKind.Minimize);
        _maximizeButton = new CaptionButton(CaptionButtonKind.Maximize);
        _closeButton = new CaptionButton(CaptionButtonKind.Close);

        _minimizeButton.Click += (_, _) => MinimizeRequested?.Invoke(this, EventArgs.Empty);
        _maximizeButton.Click += (_, _) => MaximizeRestoreRequested?.Invoke(this, EventArgs.Empty);
        _closeButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        _rootLayoutPanel.Controls.Add(_logoPanel, 0, 0);
        _rootLayoutPanel.Controls.Add(textLayoutPanel, 1, 0);
        _rootLayoutPanel.Controls.Add(_statusPill, 2, 0);
        _rootLayoutPanel.Controls.Add(_minimizeButton, 3, 0);
        _rootLayoutPanel.Controls.Add(_maximizeButton, 4, 0);
        _rootLayoutPanel.Controls.Add(_closeButton, 5, 0);

        Controls.Add(_rootLayoutPanel);
        AttachDragForwarding(this);
    }

    public event EventHandler? DragRequested;
    public event EventHandler? MinimizeRequested;
    public event EventHandler? MaximizeRestoreRequested;
    public event EventHandler? CloseRequested;

    internal void ApplyShellLayoutProfile(AppShellLayoutProfile profile)
    {
        MinimumSize = new Size(0, profile.Chrome.TitleBarRowHeight);
        _rootLayoutPanel.Padding = profile.Chrome.TitleBarPadding;
        _logoColumnStyle.Width = profile.Chrome.TitleBarIconColumnWidth;
        _statusColumnStyle.Width = profile.Chrome.TitleBarStatusWidth;
        _minimizeColumnStyle.Width = profile.Chrome.TitleBarButtonWidth;
        _maximizeColumnStyle.Width = profile.Chrome.TitleBarButtonWidth;
        _closeColumnStyle.Width = profile.Chrome.TitleBarButtonWidth;
        _logoPanel.Margin = profile.Chrome.TitleBarLogoMargin;
        _logoPanel.ApplyLayoutMetrics(
            profile.Chrome.TitleBarIconSize,
            profile.Chrome.TitleBarIconInset,
            profile.Chrome.TitleBarIconCornerRadius);
        var sm = profile.Chrome.TitleBarStatusMargin;
        _statusPill.Margin = new Padding(sm.Left, sm.Top, sm.Right, 0);
        _statusPill.MinimumSize = new Size(0, profile.Chrome.TitleBarButtonHeight);
        _appNameLabel.Margin = new Padding(0, 0, 0, profile.Chrome.TitleBarLogoMargin.Bottom);
        _statusPill.ApplyLayoutMetrics(
            profile.Chrome.TitleBarStatusCornerRadius,
            profile.Chrome.TitleBarStatusDotSize,
            profile.Chrome.TitleBarStatusDotInset,
            profile.Chrome.TitleBarStatusGlowInset,
            profile.Chrome.TitleBarStatusTextGap,
            profile.Chrome.TitleBarStatusTextRightInset);

        _minimizeButton.ApplyLayoutMetrics(
            profile.Chrome.TitleBarButtonWidth,
            profile.Chrome.TitleBarButtonHeight,
            profile.Chrome.TitleBarButtonMargin,
            profile.Chrome.TitleBarButtonCornerRadius,
            profile.Chrome.TitleBarButtonIconSize,
            profile.Chrome.TitleBarButtonHorizontalInset,
            profile.Chrome.TitleBarButtonTopInset,
            profile.Chrome.TitleBarButtonBottomInset,
            profile.Chrome.TitleBarButtonIconPenWidth);
        _maximizeButton.ApplyLayoutMetrics(
            profile.Chrome.TitleBarButtonWidth,
            profile.Chrome.TitleBarButtonHeight,
            profile.Chrome.TitleBarButtonMargin,
            profile.Chrome.TitleBarButtonCornerRadius,
            profile.Chrome.TitleBarButtonIconSize,
            profile.Chrome.TitleBarButtonHorizontalInset,
            profile.Chrome.TitleBarButtonTopInset,
            profile.Chrome.TitleBarButtonBottomInset,
            profile.Chrome.TitleBarButtonIconPenWidth);
        _closeButton.ApplyLayoutMetrics(
            profile.Chrome.TitleBarButtonWidth,
            profile.Chrome.TitleBarButtonHeight,
            profile.Chrome.TitleBarButtonMargin,
            profile.Chrome.TitleBarButtonCornerRadius,
            profile.Chrome.TitleBarButtonIconSize,
            profile.Chrome.TitleBarButtonHorizontalInset,
            profile.Chrome.TitleBarButtonTopInset,
            profile.Chrome.TitleBarButtonBottomInset,
            profile.Chrome.TitleBarButtonIconPenWidth);

        PerformLayout();
    }

    public void SetTitle(string title)
    {
        _appNameLabel.Text = title;
    }

    public void SetStatus(string status, Color color)
    {
        _statusPill.SetStatus(status, color);
    }

    public void SetMaximized(bool isMaximized)
    {
        _maximizeButton.Kind = isMaximized
            ? CaptionButtonKind.Restore
            : CaptionButtonKind.Maximize;
    }

    public bool IsInteractiveClientPoint(Point clientPoint)
    {
        Control? hitControl = FindDeepestChildAtPoint(this, clientPoint);
        return hitControl is Button;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(DesignTokens.Background);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var bottomPen = new Pen(DesignTokens.BorderSoft);
        e.Graphics.DrawLine(bottomPen, 0, Height - 1, Width, Height - 1);
    }

    private enum CaptionButtonKind
    {
        Minimize,
        Maximize,
        Restore,
        Close
    }

    private static Control? FindDeepestChildAtPoint(Control parent, Point point)
    {
        for (int index = parent.Controls.Count - 1; index >= 0; index--)
        {
            Control child = parent.Controls[index];
            if (!child.Visible || !child.Enabled || !child.Bounds.Contains(point))
            {
                continue;
            }

            var childPoint = new Point(point.X - child.Left, point.Y - child.Top);
            return FindDeepestChildAtPoint(child, childPoint) ?? child;
        }

        return null;
    }

    private void AttachDragForwarding(Control control)
    {
        control.MouseDown += titleBarControl_MouseDown;

        foreach (Control child in control.Controls)
        {
            AttachDragForwarding(child);
        }
    }

    private void titleBarControl_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Point screenPoint = ((Control)sender!).PointToScreen(e.Location);
        Point titleBarPoint = PointToClient(screenPoint);
        if (IsInteractiveClientPoint(titleBarPoint))
        {
            return;
        }

        DragRequested?.Invoke(this, EventArgs.Empty);
    }

    private sealed class StatusPillControl : Control
    {
        private string _statusText = "Hazır";
        private Color _dotColor = DesignTokens.AccentGreen;
        private int _cornerRadius = DesignTokens.Scale(8);
        private int _dotSize = DesignTokens.TitleBarStatusDotSize;
        private int _dotInset = DesignTokens.Scale(12);
        private int _glowInset = DesignTokens.Scale(2);
        private int _textGap = DesignTokens.Scale(8);
        private int _textRightInset = DesignTokens.Scale(16);

        public StatusPillControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.UserPaint,
                true);

            Font = DesignTokens.FontUiNormal;
            ForeColor = DesignTokens.TextSecondary;
        }

        public void SetStatus(string status, Color dotColor)
        {
            _statusText = string.IsNullOrWhiteSpace(status)
                ? "Hazır"
                : status;
            _dotColor = dotColor;
            Invalidate();
        }

        public void ApplyLayoutMetrics(
            int cornerRadius,
            int dotSize,
            int dotInset,
            int glowInset,
            int textGap,
            int textRightInset)
        {
            _cornerRadius = cornerRadius;
            _dotSize = dotSize;
            _dotInset = dotInset;
            _glowInset = glowInset;
            _textGap = textGap;
            _textRightInset = textRightInset;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor == Color.Transparent
                ? DesignTokens.Background
                : Parent?.BackColor ?? DesignTokens.Background);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Rectangle bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using var path = CreateRoundedRectanglePath(bounds, _cornerRadius);
            using var fillBrush = new SolidBrush(DesignTokens.SurfaceInset);
            using var borderPen = new Pen(DesignTokens.BorderSoft);

            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);

            int dotSize = _dotSize;
            int dotX = bounds.Left + _dotInset;
            int dotY = bounds.Top + (bounds.Height - dotSize) / 2;
            var dotBounds = new Rectangle(dotX, dotY, dotSize, dotSize);

            using var glowBrush = new SolidBrush(Color.FromArgb(55, _dotColor));
            using var dotBrush = new SolidBrush(_dotColor);
            e.Graphics.FillEllipse(glowBrush, Rectangle.Inflate(dotBounds, _glowInset, _glowInset));
            e.Graphics.FillEllipse(dotBrush, dotBounds);

            e.Graphics.SmoothingMode = SmoothingMode.None;

            var textBounds = new Rectangle(
                dotBounds.Right + _textGap,
                bounds.Top,
                Math.Max(0, bounds.Right - dotBounds.Right - _textRightInset),
                bounds.Height);

            TextRenderer.DrawText(
                e.Graphics,
                _statusText,
                Font,
                textBounds,
                ForeColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }
    }

    private sealed class LogoPanel : Control
    {
        private int _iconSize = DesignTokens.TitleBarIconSize;
        private int _iconInset = DesignTokens.Scale(4);
        private int _cornerRadius = DesignTokens.Scale(7);

        public LogoPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        public void ApplyLayoutMetrics(
            int iconSize,
            int iconInset,
            int cornerRadius)
        {
            _iconSize = iconSize;
            _iconInset = iconInset;
            _cornerRadius = cornerRadius;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(DesignTokens.Background);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int size = _iconSize;
            if (size <= 0)
            {
                return;
            }

            var bounds = new Rectangle(
                (Width - size) / 2,
                (Height - size) / 2,
                size,
                size);

            using var path = CreateRoundedRectanglePath(bounds, _cornerRadius);
            using var fillBrush = new SolidBrush(Color.FromArgb(28, 67, 138));
            using var borderPen = new Pen(Color.FromArgb(160, DesignTokens.Accent));
            using var textBrush = new SolidBrush(DesignTokens.Accent);
            using var font = new Font("Segoe UI", Math.Max(8f, size * 0.46f), FontStyle.Bold, GraphicsUnit.Pixel);
            using var stringFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);
            e.Graphics.DrawString("P", font, textBrush, bounds, stringFormat);
        }
    }

    private sealed class CaptionButton : Button
    {
        private bool _isPressed;
        private CaptionButtonKind _kind;
        private int _cornerRadius = DesignTokens.Scale(8);
        private int _iconSize = DesignTokens.Scale(12);
        private int _horizontalInset = DesignTokens.Scale(3);
        private int _topInset = DesignTokens.Scale(1);
        private int _bottomInset = DesignTokens.Scale(5);
        private float _iconPenWidth = Math.Max(1.6f, DesignTokens.DensityScale * 1.35f);

        public CaptionButton(CaptionButtonKind kind)
        {
            _kind = kind;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            Dock = DockStyle.Fill;
            FlatStyle = FlatStyle.Flat;
            Margin = new Padding(
                DesignTokens.Scale(2),
                DesignTokens.Scale(1),
                0,
                DesignTokens.Scale(7));
            MinimumSize = new Size(DesignTokens.TitleBarButtonWidth, DesignTokens.TitleBarButtonHeight);
            TabStop = false;
            Cursor = Cursors.Hand;
            FlatAppearance.BorderSize = 0;
            BackColor = DesignTokens.Background;
        }

        public void ApplyLayoutMetrics(
            int width,
            int height,
            Padding margin,
            int cornerRadius,
            int iconSize,
            int horizontalInset,
            int topInset,
            int bottomInset,
            float iconPenWidth)
        {
            MinimumSize = new Size(width, height);
            Margin = margin;
            _cornerRadius = cornerRadius;
            _iconSize = iconSize;
            _horizontalInset = horizontalInset;
            _topInset = topInset;
            _bottomInset = bottomInset;
            _iconPenWidth = iconPenWidth;
            Invalidate();
        }

        public CaptionButtonKind Kind
        {
            get => _kind;
            set
            {
                if (_kind == value)
                {
                    return;
                }

                _kind = value;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _isPressed = mevent.Button == MouseButtons.Left;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _isPressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isPressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(DesignTokens.Background);
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            pevent.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color fill = ResolveFillColor();
            Color icon = Enabled
                ? DesignTokens.TextPrimary
                : DesignTokens.TextMuted;

            Rectangle buttonBounds = new(
                _horizontalInset,
                _topInset,
                Math.Max(0, Width - (_horizontalInset * 2)),
                Math.Max(0, Height - _topInset - _bottomInset));
            using var path = CreateRoundedRectanglePath(buttonBounds, _cornerRadius);
            using var fillBrush = new SolidBrush(fill);
            pevent.Graphics.FillPath(fillBrush, path);

            using var iconPen = new Pen(icon, _iconPenWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            DrawIcon(pevent.Graphics, iconPen);
        }

        private Color ResolveFillColor()
        {
            if (!Enabled)
            {
                return Color.Transparent;
            }

            if (_kind == CaptionButtonKind.Close && ClientRectangle.Contains(PointToClient(Cursor.Position)))
            {
                return _isPressed
                    ? Color.FromArgb(190, 185, 38, 61)
                    : Color.FromArgb(150, 255, 79, 106);
            }

            if (_isPressed)
            {
                return DesignTokens.Surface3;
            }

            return ClientRectangle.Contains(PointToClient(Cursor.Position))
                ? DesignTokens.SurfaceHover
                : Color.Transparent;
        }

        private void DrawIcon(Graphics graphics, Pen pen)
        {
            int iconSize = _iconSize;
            int centerX = Width / 2;
            int centerY = Height / 2;
            int left = centerX - iconSize / 2;
            int top = centerY - iconSize / 2;
            int right = left + iconSize;
            int bottom = top + iconSize;

            switch (_kind)
            {
                case CaptionButtonKind.Minimize:
                    graphics.DrawLine(pen, left, centerY + iconSize / 4, right, centerY + iconSize / 4);
                    break;
                case CaptionButtonKind.Maximize:
                    graphics.DrawRectangle(pen, left, top, iconSize, iconSize);
                    break;
                case CaptionButtonKind.Restore:
                    graphics.DrawRectangle(pen, left + DesignTokens.Scale(3), top, iconSize - DesignTokens.Scale(3), iconSize - DesignTokens.Scale(3));
                    graphics.DrawRectangle(pen, left, top + DesignTokens.Scale(3), iconSize - DesignTokens.Scale(3), iconSize - DesignTokens.Scale(3));
                    break;
                case CaptionButtonKind.Close:
                    graphics.DrawLine(pen, left, top, right, bottom);
                    graphics.DrawLine(pen, right, top, left, bottom);
                    break;
            }
        }
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 1)
        {
            path.AddRectangle(bounds);
            return path;
        }

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
