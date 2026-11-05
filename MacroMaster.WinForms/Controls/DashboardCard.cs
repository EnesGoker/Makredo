using MacroMaster.WinForms.Theme;
using System.Drawing.Drawing2D;

namespace MacroMaster.WinForms.Controls;

internal sealed class DashboardCard : UserControl
{
    private readonly TableLayoutPanel _rootLayoutPanel;
    private readonly Label _titleLabel;
    private bool _showHeader = true;
    private int _headerHeight = DesignTokens.Scale(38);
    private Size _lastRegionSize;
    private int _lastRegionRadius = -1;

    public DashboardCard()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        DoubleBuffered = true;
        BackColor = DesignTokens.Surface;
        ForeColor = DesignTokens.TextPrimary;
        Font = DesignTokens.FontUiNormal;
        Padding = new Padding(1);

        _titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = DesignTokens.FontUiBold,
            ForeColor = DesignTokens.TextPrimary,
            BackColor = DesignTokens.Surface,
            Padding = new Padding(2, 0, 0, 2),
            TextAlign = ContentAlignment.MiddleLeft
        };

        Body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        _rootLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = DesignTokens.Surface,
            Margin = Padding.Empty,
            Padding = new Padding(DesignTokens.CardPadding)
        };
        _rootLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, _headerHeight));
        _rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _rootLayoutPanel.Controls.Add(_titleLabel, 0, 0);
        _rootLayoutPanel.Controls.Add(Body, 0, 1);

        Controls.Add(_rootLayoutPanel);
        UpdateHeaderVisibility();
    }

    public Panel Body { get; }

    public string Title
    {
        get => _titleLabel.Text;
        set => _titleLabel.Text = value;
    }

    public bool ShowHeader
    {
        get => _showHeader;
        set
        {
            if (_showHeader == value)
            {
                return;
            }

            _showHeader = value;
            UpdateHeaderVisibility();
        }
    }

    public Padding ContentPadding
    {
        get => _rootLayoutPanel.Padding;
        set => _rootLayoutPanel.Padding = value;
    }

    public int HeaderHeight
    {
        get => _headerHeight;
        set
        {
            int nextHeight = Math.Max(0, value);
            if (_headerHeight == nextHeight)
            {
                return;
            }

            _headerHeight = nextHeight;
            UpdateHeaderVisibility();
            Invalidate();
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? DesignTokens.Background);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var glowBounds = Rectangle.Inflate(bounds, -2, -2);
        using GraphicsPath path = CreateRoundedRectanglePath(bounds, DesignTokens.Radius);
        using GraphicsPath glowPath = CreateRoundedRectanglePath(glowBounds, Math.Max(2, DesignTokens.Radius - 2));
        using var fillBrush = new SolidBrush(DesignTokens.Surface);
        using var glowPen = new Pen(Color.FromArgb(38, DesignTokens.Accent), 1f);
        using var borderPen = new Pen(DesignTokens.BorderSoft);
        using var highlightPen = new Pen(Color.FromArgb(45, Color.White));

        e.Graphics.FillPath(fillBrush, path);
        e.Graphics.DrawPath(glowPen, glowPath);
        e.Graphics.DrawPath(borderPen, path);
        e.Graphics.DrawLine(highlightPen, bounds.Left + DesignTokens.Radius, bounds.Top, bounds.Right - DesignTokens.Radius, bounds.Top);

        if (_showHeader)
        {
            int dividerY = bounds.Top + _headerHeight;
            using var dividerPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1f);
            e.Graphics.DrawLine(dividerPen,
                bounds.Left + DesignTokens.Scale(12),
                dividerY,
                bounds.Right - DesignTokens.Scale(12),
                dividerY);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
        Invalidate();
    }

    private void UpdateHeaderVisibility()
    {
        _titleLabel.Visible = _showHeader;
        _rootLayoutPanel.RowStyles[0].Height = _showHeader ? _headerHeight : 0f;
    }

    private void UpdateRegion()
    {
        Rectangle bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            Region? emptyRegion = Region;
            Region = null;
            emptyRegion?.Dispose();
            _lastRegionSize = Size.Empty;
            _lastRegionRadius = -1;
            return;
        }

        int radius = DesignTokens.Radius;
        if (Region is not null
            && _lastRegionSize == bounds.Size
            && _lastRegionRadius == radius)
        {
            return;
        }

        using GraphicsPath path = CreateRoundedRectanglePath(
            Rectangle.Inflate(bounds, -1, -1),
            radius);
        Region? previousRegion = Region;
        Region = new Region(path);
        previousRegion?.Dispose();
        _lastRegionSize = bounds.Size;
        _lastRegionRadius = radius;
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
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
