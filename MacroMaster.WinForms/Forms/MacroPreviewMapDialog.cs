using MacroMaster.Domain.Models;
using MacroMaster.WinForms.Controls;
using MacroMaster.WinForms.Platform;
using MacroMaster.WinForms.Theme;
using System.Drawing.Drawing2D;

namespace MacroMaster.WinForms.Forms;

internal sealed class MacroPreviewMapDialog : Form
{
    private const int PositionYOffset = 6;

    private readonly Label _inspectedEventValueLabel;
    private readonly Label _inspectedActionValueLabel;
    private readonly Label _inspectedPositionValueLabel;
    private readonly Label _inspectedDelayValueLabel;
    private readonly MacroPreviewMapControl _mapControl;

    public MacroPreviewMapDialog()
    {
        Text = "Makro Önizleme Haritası";
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = DesignTokens.Surface;
        ForeColor = DesignTokens.TextPrimary;
        Font = DesignTokens.FontUiNormal;
        ClientSize = ResolvePreferredClientSize();
        MinimumSize = SizeFromClientSize(ResolveMinimumClientSize());

        _inspectedEventValueLabel = CreateInspectionValueLabel();
        _inspectedActionValueLabel = CreateInspectionValueLabel();
        _inspectedPositionValueLabel = CreateInspectionValueLabel();
        _inspectedDelayValueLabel = CreateInspectionValueLabel();
        _mapControl = new MacroPreviewMapControl
        {
            AccessibleName = "Büyük makro önizleme haritası",
            AccessibleDescription = "Seçili makronun fare rotasını geniş harita olarak gösterir.",
            InspectionEnabled = true,
            Cursor = Cursors.Default
        };
        _mapControl.PointInspected += mapControl_PointInspected;

        BuildLayout();
        UpdateInspectedPoint(null);
    }

    public void PositionNear(
        Rectangle anchorScreenBounds,
        Rectangle ownerScreenBounds)
    {
        Rectangle referenceBounds = anchorScreenBounds.IsEmpty
            ? ownerScreenBounds
            : anchorScreenBounds;
        Rectangle workingArea = Screen.FromRectangle(referenceBounds).WorkingArea;
        int margin = DesignTokens.Scale(12);
        ApplyWorkingAreaSize(workingArea, margin);

        int preferredX = anchorScreenBounds.IsEmpty
            ? ownerScreenBounds.Left + (ownerScreenBounds.Width - Width) / 2
            : anchorScreenBounds.Left - Width - DesignTokens.Scale(16);

        if (preferredX < ownerScreenBounds.Left + margin)
        {
            preferredX = ownerScreenBounds.Left + (ownerScreenBounds.Width - Width) / 2;
        }

        int preferredY = anchorScreenBounds.IsEmpty
            ? ownerScreenBounds.Top + (ownerScreenBounds.Height - Height) / 2
            : anchorScreenBounds.Top + (anchorScreenBounds.Height - Height) / 2;
        preferredY += DesignTokens.Scale(PositionYOffset);

        Location = new Point(
            ClampScreenCoordinate(preferredX, workingArea.Left + margin, workingArea.Right - Width - margin),
            ClampScreenCoordinate(preferredY, workingArea.Top + margin, workingArea.Bottom - Height - margin));
    }

    public void UpdatePreview(
        SessionSummaryState state,
        IReadOnlyList<MacroEvent>? events,
        int? activeSourceEventIndex)
    {
        _mapControl.UpdatePreview(
            state.EventCount,
            state.TotalDurationMs,
            state.StatusText,
            events,
            activeSourceEventIndex);
        UpdateInspectedPoint(_mapControl.GetInspectedPointInfo());
    }

    public void UpdateActiveSourceEventIndex(int? activeSourceEventIndex)
    {
        _mapControl.UpdateActiveSourceEventIndex(activeSourceEventIndex);
        UpdateInspectedPoint(_mapControl.GetInspectedPointInfo());
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        WindowChromeNative.TryApplyDwmBoolAttribute(
            Handle,
            DwmWindowAttribute.UseImmersiveDarkMode,
            enabled: true);
        WindowChromeNative.TryApplyDwmCornerPreference(
            Handle,
            DwmWindowCornerPreference.Round);
        WindowChromeNative.TryApplyDwmColorAttribute(
            Handle,
            DwmWindowAttribute.BorderColor,
            DesignTokens.Border);
        WindowChromeNative.TryApplyDwmColorAttribute(
            Handle,
            DwmWindowAttribute.CaptionColor,
            DesignTokens.Surface);
        WindowChromeNative.TryApplyDwmColorAttribute(
            Handle,
            DwmWindowAttribute.TextColor,
            DesignTokens.TextPrimary);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);

        if (!IsDisposed && Visible)
        {
            Close();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRoundedRegion();
    }

    private void UpdateRoundedRegion()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        using GraphicsPath path = CreateRoundPath(
            new Rectangle(Point.Empty, ClientSize),
            DesignTokens.Scale(14));
        Region? previousRegion = Region;
        Region = new Region(path);
        previousRegion?.Dispose();
    }

    private void BuildLayout()
    {
        var rootLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = DesignTokens.Surface,
            Margin = Padding.Empty,
            Padding = ResolveRootPadding()
        };
        rootLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, ResolveInspectionRowHeight()));
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, ResolveLegendRowHeight()));

        rootLayoutPanel.Controls.Add(CreateInspectionPanel(), 0, 0);
        rootLayoutPanel.Controls.Add(CreateMapHost(), 0, 1);
        rootLayoutPanel.Controls.Add(CreateLegendPanel(), 0, 2);

        Controls.Add(rootLayoutPanel);
    }

    private void ApplyWorkingAreaSize(Rectangle workingArea, int margin)
    {
        Size minimumClientSize = ResolveMinimumClientSizeForWorkingArea(workingArea, margin);
        MinimumSize = SizeFromClientSize(minimumClientSize);

        Size nextClientSize = ResolveClientSizeForWorkingArea(workingArea, margin, minimumClientSize);
        if (ClientSize != nextClientSize)
        {
            ClientSize = nextClientSize;
        }
    }

    private RoundedPanel CreateInspectionPanel()
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.SurfaceInset,
            FillColor = DesignTokens.SurfaceInset,
            BorderColor = DesignTokens.BorderSoft,
            Margin = new Padding(0, 0, 0, DesignTokens.Scale(12)),
            Padding = ResolveInspectionPanelPadding()
        };

        var layoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = DesignTokens.SurfaceInset,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
        layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
        layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27f));
        layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 17f));

        layoutPanel.Controls.Add(CreateInspectionCell("Olay", _inspectedEventValueLabel), 0, 0);
        layoutPanel.Controls.Add(CreateInspectionCell("Aksiyon", _inspectedActionValueLabel), 1, 0);
        layoutPanel.Controls.Add(CreateInspectionCell("Konum", _inspectedPositionValueLabel), 2, 0);
        layoutPanel.Controls.Add(CreateInspectionCell("Gecikme", _inspectedDelayValueLabel), 3, 0);

        panel.Controls.Add(layoutPanel);
        return panel;
    }

    private RoundedPanel CreateMapHost()
    {
        var panel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.SurfaceInset,
            FillColor = DesignTokens.SurfaceInset,
            BorderColor = DesignTokens.BorderSoft,
            Margin = Padding.Empty,
            Padding = new Padding(DesignTokens.Scale(10))
        };
        panel.Controls.Add(_mapControl);
        return panel;
    }

    private static FlowLayoutPanel CreateLegendPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = DesignTokens.Surface,
            Margin = new Padding(0, DesignTokens.Scale(10), 0, 0),
            Padding = Padding.Empty
        };
        panel.Controls.Add(new LegendItem("Başlangıç", DesignTokens.AccentGreen));
        panel.Controls.Add(new LegendItem("Aktif nokta", DesignTokens.AccentOrange));
        panel.Controls.Add(new LegendItem("Tıklama", DesignTokens.AccentRed));
        panel.Controls.Add(new LegendItem("Kaydırma", DesignTokens.AccentPurple));
        return panel;
    }

    private static Label CreateInspectionValueLabel()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Font = DesignTokens.FontUiBold,
            ForeColor = DesignTokens.TextPrimary,
            BackColor = DesignTokens.SurfaceInset,
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = true,
            UseMnemonic = false
        };
    }

    private static TableLayoutPanel CreateInspectionCell(
        string caption,
        Label valueLabel)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = DesignTokens.SurfaceInset,
            Margin = new Padding(0, 0, DesignTokens.Scale(12), 0),
            Padding = Padding.Empty
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, ResolveInspectionCaptionRowHeight()));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        panel.Controls.Add(CreateInspectionCaptionLabel(caption), 0, 0);
        panel.Controls.Add(valueLabel, 0, 1);
        return panel;
    }

    private static int ResolveInspectionRowHeight()
    {
        Padding padding = ResolveInspectionPanelPadding();
        int contentHeight = ResolveInspectionCaptionRowHeight()
            + ResolveInspectionValueRowHeight();

        return Math.Max(
            DesignTokens.Scale(76),
            padding.Vertical
            + contentHeight
            + DesignTokens.Scale(12)
            + DesignTokens.Scale(2));
    }

    private static int ResolveLegendRowHeight()
    {
        return Math.Max(
            DesignTokens.Scale(44),
            DesignTokens.Scale(10)
            + DesignTokens.Scale(28)
            + DesignTokens.Scale(4));
    }

    private static Padding ResolveInspectionPanelPadding()
    {
        return new Padding(
            DesignTokens.Scale(55),
            DesignTokens.Scale(20),
            DesignTokens.Scale(12),
            DesignTokens.Scale(7));
    }

    private static int ResolveInspectionCaptionRowHeight()
    {
        return Math.Max(
            DesignTokens.Scale(18),
            TextRenderer.MeasureText(
                "Gecikme",
                DesignTokens.FontUiSmall,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height
            + DesignTokens.Scale(3));
    }

    private static int ResolveInspectionValueRowHeight()
    {
        return Math.Max(
            DesignTokens.Scale(24),
            TextRenderer.MeasureText(
                "Olay seçilmedi",
                DesignTokens.FontUiBold,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Height
            + DesignTokens.Scale(5));
    }

    private static Label CreateInspectionCaptionLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            Font = DesignTokens.FontUiSmall,
            ForeColor = DesignTokens.TextSecondary,
            BackColor = DesignTokens.SurfaceInset,
            TextAlign = ContentAlignment.BottomLeft,
            AutoEllipsis = true,
            UseMnemonic = false
        };
    }

    private void mapControl_PointInspected(object? sender, MacroPreviewMapPointEventArgs e)
    {
        _ = sender;
        UpdateInspectedPoint(e.PointInfo ?? _mapControl.GetInspectedPointInfo());
    }

    private void UpdateInspectedPoint(MacroPreviewMapPointInfo? pointInfo)
    {
        if (!pointInfo.HasValue)
        {
            _inspectedEventValueLabel.Text = "-";
            _inspectedActionValueLabel.Text = "Olay seçilmedi";
            _inspectedPositionValueLabel.Text = "-";
            _inspectedDelayValueLabel.Text = "-";
            return;
        }

        MacroPreviewMapPointInfo info = pointInfo.Value;
        _inspectedEventValueLabel.Text = FormattableString.Invariant($"#{info.EventNumber:000}");
        _inspectedActionValueLabel.Text = string.IsNullOrWhiteSpace(info.DetailText)
            ? info.ActionText
            : FormattableString.Invariant($"{info.ActionText} - {info.DetailText}");
        _inspectedPositionValueLabel.Text = FormattableString.Invariant($"X: {info.X}, Y: {info.Y}");
        _inspectedDelayValueLabel.Text = FormattableString.Invariant($"{info.DelayMs} ms");
    }

    private static int ClampScreenCoordinate(
        int value,
        int minimum,
        int maximum)
    {
        return minimum > maximum
            ? minimum
            : Math.Clamp(value, minimum, maximum);
    }

    internal static Size ResolvePreferredClientSize()
    {
        return new Size(DesignTokens.Scale(780), DesignTokens.Scale(580));
    }

    internal static Size ResolveMinimumClientSize()
    {
        Padding rootPadding = ResolveRootPadding();
        int minimumMapHeight = DesignTokens.Scale(260);

        return new Size(
            DesignTokens.Scale(560),
            Math.Max(
                DesignTokens.Scale(420),
                rootPadding.Vertical
                + ResolveInspectionRowHeight()
                + ResolveLegendRowHeight()
                + minimumMapHeight));
    }

    internal static Size ResolveMinimumClientSizeForWorkingArea(Rectangle workingArea, int margin)
    {
        Size minimumClientSize = ResolveMinimumClientSize();
        if (workingArea.Width <= 0 || workingArea.Height <= 0)
        {
            return minimumClientSize;
        }

        int availableWidth = Math.Max(DesignTokens.Scale(360), workingArea.Width - (margin * 2));
        int availableHeight = Math.Max(DesignTokens.Scale(320), workingArea.Height - (margin * 2));

        return new Size(
            Math.Min(minimumClientSize.Width, availableWidth),
            Math.Min(minimumClientSize.Height, availableHeight));
    }

    internal static Size ResolveClientSizeForWorkingArea(
        Rectangle workingArea,
        int margin,
        Size minimumClientSize)
    {
        Size preferredClientSize = ResolvePreferredClientSize();
        if (workingArea.Width <= 0 || workingArea.Height <= 0)
        {
            return preferredClientSize;
        }

        int availableWidth = Math.Max(minimumClientSize.Width, workingArea.Width - (margin * 2));
        int availableHeight = Math.Max(minimumClientSize.Height, workingArea.Height - (margin * 2));

        return new Size(
            Math.Clamp(preferredClientSize.Width, minimumClientSize.Width, availableWidth),
            Math.Clamp(preferredClientSize.Height, minimumClientSize.Height, availableHeight));
    }

    private static Padding ResolveRootPadding()
    {
        return new Padding(
            DesignTokens.Scale(22),
            DesignTokens.Scale(18),
            DesignTokens.Scale(22),
            DesignTokens.Scale(18));
    }

    private sealed class LegendItem : Control
    {
        private readonly string _text;
        private readonly Color _dotColor;

        public LegendItem(string text, Color dotColor)
        {
            _text = text;
            _dotColor = dotColor;
            Width = DesignTokens.Scale(132);
            Height = DesignTokens.Scale(28);
            Margin = new Padding(0, 0, DesignTokens.Scale(8), 0);
            BackColor = DesignTokens.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int radius = DesignTokens.Scale(4);
            int centerY = ClientRectangle.Top + ClientRectangle.Height / 2;
            using var brush = new SolidBrush(_dotColor);
            e.Graphics.FillEllipse(
                brush,
                DesignTokens.Scale(2),
                centerY - radius,
                radius * 2,
                radius * 2);
            TextRenderer.DrawText(
                e.Graphics,
                _text,
                DesignTokens.FontUiSmall,
                new Rectangle(
                    DesignTokens.Scale(16),
                    0,
                    Math.Max(0, Width - DesignTokens.Scale(16)),
                    Height),
                DesignTokens.TextSecondary,
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.Left |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }
    }

    private sealed class RoundedPanel : Panel
    {
        public Color FillColor { get; set; } = DesignTokens.SurfaceInset;
        public Color BorderColor { get; set; } = DesignTokens.BorderSoft;

        public RoundedPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? DesignTokens.Surface);
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

            using GraphicsPath path = CreateRoundPath(bounds, DesignTokens.Scale(12));
            using var fillBrush = new SolidBrush(FillColor);
            using var borderPen = new Pen(BorderColor);
            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);
        }
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
