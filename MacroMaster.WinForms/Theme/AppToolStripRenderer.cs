using System.Drawing.Drawing2D;

namespace MacroMaster.WinForms.Theme;

internal enum AppToolStripMenuDensity
{
    Standard,
    Comfortable
}

internal sealed class AppToolStripRenderer : ToolStripProfessionalRenderer
{
    public static readonly AppToolStripRenderer Instance = new();

    private static readonly Color DropDownBackground = DesignTokens.Surface;
    private static readonly Color DropDownBorder = DesignTokens.Border;
    private static readonly Color ItemHoverBackground = DesignTokens.SurfaceHover;
    private static readonly Color ItemPressedBackground = DesignTokens.Surface3;
    private static readonly Color DisabledText = DesignTokens.TextMuted;

    private AppToolStripRenderer()
        : base(new AppToolStripColorTable())
    {
        RoundedEdges = false;
    }

    public static void ApplyTo(
        ContextMenuStrip menu,
        AppToolStripMenuDensity density = AppToolStripMenuDensity.Standard)
    {
        ArgumentNullException.ThrowIfNull(menu);

        ConfigureDropDown(menu, density);
        ApplyItems(menu.Items, density);
        NormalizeDropDownLayout(menu, density);

        menu.ItemAdded += (_, eventArgs) =>
        {
            if (eventArgs.Item is not null)
            {
                ApplyItem(eventArgs.Item, density);
                NormalizeDropDownLayout(menu, density);
            }
        };

        menu.Opening += (_, _) =>
        {
            ConfigureDropDown(menu, density);
            ApplyItems(menu.Items, density);
            NormalizeDropDownLayout(menu, density);
            menu.PerformLayout();
        };
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        Rectangle bounds = new(Point.Empty, e.ToolStrip.Size);
        if (bounds.Width <= 1 || bounds.Height <= 1)
        {
            return;
        }

        ApplyRoundedDropDownRegion(e.ToolStrip);

        using GraphicsPath path = CreateRoundedRectanglePath(
            Rectangle.Inflate(bounds, -1, -1),
            ResolveDropDownCornerRadius());
        using var brush = new SolidBrush(DropDownBackground);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
        e.Graphics.SmoothingMode = SmoothingMode.None;
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.ToolStrip.Width <= 1 || e.ToolStrip.Height <= 1)
        {
            return;
        }

        Rectangle borderBounds = new(
            0,
            0,
            e.ToolStrip.Width - 1,
            e.ToolStrip.Height - 1);

        using GraphicsPath path = CreateRoundedRectanglePath(
            borderBounds,
            ResolveDropDownCornerRadius());
        using var borderPen = new Pen(DropDownBorder);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawPath(borderPen, path);
        e.Graphics.SmoothingMode = SmoothingMode.None;
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        using var brush = new SolidBrush(DropDownBackground);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        Rectangle bounds = new(Point.Empty, e.Item.Size);
        using var backgroundBrush = new SolidBrush(DropDownBackground);
        e.Graphics.FillRectangle(backgroundBrush, bounds);

        int horizontalInset = ResolveSeparatorHorizontalInset(e.Item);
        int y = Math.Max(0, e.Item.Height / 2);
        int right = Math.Max(horizontalInset, e.Item.Width - horizontalInset);

        using var pen = new Pen(DesignTokens.BorderSoft);
        e.Graphics.DrawLine(pen, horizontalInset, y, right, y);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        Rectangle bounds = new(Point.Empty, e.Item.Size);
        using var backgroundBrush = new SolidBrush(DropDownBackground);
        e.Graphics.FillRectangle(backgroundBrush, bounds);

        if (!e.Item.Selected && !e.Item.Pressed)
        {
            return;
        }

        Rectangle itemBounds = ResolveItemBackgroundBounds(e.Item);

        if (itemBounds.IsEmpty)
        {
            return;
        }

        using GraphicsPath path = CreateRoundedRectanglePath(itemBounds, DesignTokens.Scale(7));
        using var brush = new SolidBrush(e.Item.Pressed ? ItemPressedBackground : ItemHoverBackground);
        using var borderPen = new Pen(Color.FromArgb(110, DesignTokens.Accent));

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(borderPen, path);
        e.Graphics.SmoothingMode = SmoothingMode.None;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        Color textColor = e.Item.Enabled
            ? DesignTokens.TextPrimary
            : DisabledText;

        Rectangle textBounds = ResolveTextBounds(e.Item);

        TextRenderer.DrawText(
            e.Graphics,
            e.Text,
            e.TextFont,
            textBounds,
            textColor,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.SingleLine |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.NoPadding);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        e.ArrowColor = e.Item?.Enabled != false
            ? DesignTokens.TextSecondary
            : DisabledText;

        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.Item is not ToolStripMenuItem { Checked: true })
        {
            return;
        }

        Rectangle dotBounds = ResolveCheckIndicatorBounds(e.Item);

        using var fillBrush = new SolidBrush(DesignTokens.Accent);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillEllipse(fillBrush, dotBounds);
        e.Graphics.SmoothingMode = SmoothingMode.None;
    }

    private static void ApplyRoundedDropDownRegion(ToolStrip toolStrip)
    {
        if (toolStrip.Width <= 1 || toolStrip.Height <= 1)
        {
            return;
        }

        // ContextMenuStrip/ToolStripDropDown windows are rectangular by default.
        // The renderer can draw rounded corners, but the native window region also
        // has to be clipped; otherwise the outer corners stay visually square.
        Rectangle regionBounds = new(Point.Empty, toolStrip.Size);
        using GraphicsPath path = CreateRoundedRectanglePath(
            regionBounds,
            ResolveDropDownCornerRadius());

        Region? previousRegion = toolStrip.Region;
        toolStrip.Region = new Region(path);
        previousRegion?.Dispose();
    }

    private static int ResolveDropDownCornerRadius()
    {
        return Math.Max(DesignTokens.Scale(10), DesignTokens.Radius);
    }

    private static void ConfigureDropDown(
        ToolStripDropDown dropDown,
        AppToolStripMenuDensity density)
    {
        dropDown.BackColor = DropDownBackground;
        dropDown.ForeColor = DesignTokens.TextPrimary;
        dropDown.Font = DesignTokens.FontUiNormal;
        dropDown.Padding = ResolveMenuPadding(density);
        dropDown.CanOverflow = false;
        dropDown.GripStyle = ToolStripGripStyle.Hidden;
        dropDown.LayoutStyle = ToolStripLayoutStyle.VerticalStackWithOverflow;
        dropDown.RenderMode = ToolStripRenderMode.Professional;
        dropDown.Renderer = Instance;
    }

    private static void ApplyItems(
        ToolStripItemCollection items,
        AppToolStripMenuDensity density)
    {
        foreach (ToolStripItem item in items)
        {
            ApplyItem(item, density);
        }
    }

    private static void ApplyItem(
        ToolStripItem item,
        AppToolStripMenuDensity density)
    {
        item.BackColor = DropDownBackground;
        item.ForeColor = item.Enabled
            ? DesignTokens.TextPrimary
            : DisabledText;
        item.Font = DesignTokens.FontUiNormal;
        item.Margin = item is ToolStripSeparator
            ? ResolveSeparatorMargin(density)
            : ResolveItemMargin(density);
        item.Padding = item is ToolStripSeparator
            ? Padding.Empty
            : ResolveItemPadding(density);
        item.AutoSize = false;

        if (item is ToolStripMenuItem menuItem)
        {
            menuItem.TextAlign = ContentAlignment.MiddleLeft;
            menuItem.TextDirection = ToolStripTextDirection.Horizontal;
        }

        if (item is ToolStripDropDownItem dropDownItem)
        {
            ConfigureDropDown(dropDownItem.DropDown, density);
            ApplyItems(dropDownItem.DropDownItems, density);
            NormalizeDropDownLayout(dropDownItem.DropDown, density);
        }
    }

    private static void NormalizeDropDownLayout(
        ToolStripDropDown dropDown,
        AppToolStripMenuDensity density)
    {
        if (dropDown.Items.Count == 0)
        {
            return;
        }

        int contentWidth = ResolveContentWidth(dropDown, density);
        int menuWidth = contentWidth + dropDown.Padding.Horizontal;

        dropDown.MinimumSize = new Size(menuWidth, 0);

        foreach (ToolStripItem item in dropDown.Items)
        {
            item.AutoSize = false;
            item.Width = contentWidth;
            item.Height = item is ToolStripSeparator
                ? ResolveSeparatorHeight(density)
                : ResolveItemHeight(density);
        }
    }

    private static int ResolveContentWidth(
        ToolStripDropDown dropDown,
        AppToolStripMenuDensity density)
    {
        int minimumWidth = density == AppToolStripMenuDensity.Comfortable
            ? DesignTokens.Scale(176)
            : DesignTokens.Scale(138);
        int maximumTextWidth = 0;

        foreach (ToolStripItem item in dropDown.Items)
        {
            if (item is ToolStripSeparator)
            {
                continue;
            }

            Size textSize = TextRenderer.MeasureText(
                item.Text,
                item.Font,
                Size.Empty,
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);
            maximumTextWidth = Math.Max(maximumTextWidth, textSize.Width);
        }

        int textLeft = ResolveTextLeftInset(dropDown, density);
        int textRight = ResolveTextRightInset(dropDown, density);
        int requiredWidth = textLeft + maximumTextWidth + textRight;

        return Math.Max(minimumWidth, requiredWidth);
    }

    private static int ResolveItemHeight(AppToolStripMenuDensity density)
    {
        return density == AppToolStripMenuDensity.Comfortable
            ? DesignTokens.Scale(50)
            : DesignTokens.Scale(38);
    }

    private static int ResolveSeparatorHeight(AppToolStripMenuDensity density)
    {
        return density == AppToolStripMenuDensity.Comfortable
            ? DesignTokens.Scale(16)
            : DesignTokens.Scale(10);
    }

    private static Padding ResolveMenuPadding(AppToolStripMenuDensity density)
    {
        return density == AppToolStripMenuDensity.Comfortable
            ? new Padding(DesignTokens.Scale(8), DesignTokens.Scale(7), DesignTokens.Scale(8), DesignTokens.Scale(7))
            : new Padding(DesignTokens.Scale(5));
    }

    private static Padding ResolveItemMargin(AppToolStripMenuDensity density)
    {
        return density == AppToolStripMenuDensity.Comfortable
            ? new Padding(0, DesignTokens.Scale(1), 0, DesignTokens.Scale(1))
            : Padding.Empty;
    }

    private static Padding ResolveItemPadding(AppToolStripMenuDensity density)
    {
        return density == AppToolStripMenuDensity.Comfortable
            ? new Padding(DesignTokens.Scale(14), 0, DesignTokens.Scale(18), 0)
            : new Padding(DesignTokens.Scale(11), 0, DesignTokens.Scale(14), 0);
    }

    private static Padding ResolveSeparatorMargin(AppToolStripMenuDensity density)
    {
        return density == AppToolStripMenuDensity.Comfortable
            ? new Padding(0, DesignTokens.Scale(4), 0, DesignTokens.Scale(4))
            : new Padding(0, DesignTokens.Scale(3), 0, DesignTokens.Scale(3));
    }

    private static Rectangle ResolveTextBounds(ToolStripItem item)
    {
        ToolStripDropDown? dropDown = item.Owner as ToolStripDropDown;
        AppToolStripMenuDensity density = ResolveDensityFromItem(item);
        int left = dropDown is null
            ? DesignTokens.Scale(18)
            : ResolveTextLeftInset(dropDown, density);
        int right = dropDown is null
            ? DesignTokens.Scale(18)
            : ResolveTextRightInset(dropDown, density);

        return new Rectangle(
            left,
            0,
            Math.Max(0, item.Width - left - right),
            item.Height);
    }

    private static int ResolveTextLeftInset(
        ToolStripDropDown dropDown,
        AppToolStripMenuDensity density)
    {
        int baseInset = density == AppToolStripMenuDensity.Comfortable
            ? DesignTokens.Scale(23)
            : DesignTokens.Scale(17);

        return HasCheckOrImageGutter(dropDown)
            ? baseInset + DesignTokens.Scale(26)
            : baseInset;
    }

    private static int ResolveTextRightInset(
        ToolStripDropDown dropDown,
        AppToolStripMenuDensity density)
    {
        int baseInset = density == AppToolStripMenuDensity.Comfortable
            ? DesignTokens.Scale(22)
            : DesignTokens.Scale(16);

        return HasSubMenuItems(dropDown)
            ? baseInset + DesignTokens.Scale(22)
            : baseInset;
    }

    private static Rectangle ResolveCheckIndicatorBounds(ToolStripItem item)
    {
        int dotSize = DesignTokens.Scale(6);
        AppToolStripMenuDensity density = ResolveDensityFromItem(item);
        int backgroundInset = ResolveItemHorizontalInset(density);
        int left = backgroundInset + DesignTokens.Scale(12);
        int top = Math.Max(0, (item.Height - dotSize) / 2);

        return new Rectangle(left, top, dotSize, dotSize);
    }

    private static Rectangle ResolveItemBackgroundBounds(ToolStripItem item)
    {
        AppToolStripMenuDensity density = ResolveDensityFromItem(item);
        int horizontalInset = ResolveItemHorizontalInset(density);
        int verticalInset = density == AppToolStripMenuDensity.Comfortable
            ? DesignTokens.Scale(5)
            : DesignTokens.Scale(3);

        Rectangle itemBounds = new(
            horizontalInset,
            verticalInset,
            Math.Max(0, item.Width - (horizontalInset * 2)),
            Math.Max(0, item.Height - (verticalInset * 2)));

        if (itemBounds.Width <= 0 || itemBounds.Height <= 0)
        {
            return Rectangle.Empty;
        }

        return itemBounds;
    }

    private static int ResolveItemHorizontalInset(AppToolStripMenuDensity density)
    {
        return density == AppToolStripMenuDensity.Comfortable
            ? DesignTokens.Scale(10)
            : DesignTokens.Scale(5);
    }

    private static int ResolveSeparatorHorizontalInset(ToolStripItem item)
    {
        AppToolStripMenuDensity density = ResolveDensityFromItem(item);
        int baseInset = density == AppToolStripMenuDensity.Comfortable
            ? DesignTokens.Scale(18)
            : DesignTokens.Scale(12);

        if (item.Owner is ToolStripDropDown dropDown && HasCheckOrImageGutter(dropDown))
        {
            return baseInset + DesignTokens.Scale(26);
        }

        return baseInset;
    }

    private static AppToolStripMenuDensity ResolveDensityFromItem(ToolStripItem item)
    {
        return item.Height >= DesignTokens.Scale(46) || item.Padding.Left >= DesignTokens.Scale(14)
            ? AppToolStripMenuDensity.Comfortable
            : AppToolStripMenuDensity.Standard;
    }

    private static bool HasCheckOrImageGutter(ToolStripDropDown dropDown)
    {
        if (dropDown is ToolStripDropDownMenu { ShowCheckMargin: true })
        {
            return true;
        }

        if (dropDown is ToolStripDropDownMenu { ShowImageMargin: true })
        {
            foreach (ToolStripItem item in dropDown.Items)
            {
                if (item.Image is not null)
                {
                    return true;
                }
            }
        }

        foreach (ToolStripItem item in dropDown.Items)
        {
            if (item is ToolStripMenuItem { Checked: true })
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSubMenuItems(ToolStripDropDown dropDown)
    {
        foreach (ToolStripItem item in dropDown.Items)
        {
            if (item is ToolStripDropDownItem { HasDropDownItems: true })
            {
                return true;
            }
        }

        return false;
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

    private sealed class AppToolStripColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => DropDownBackground;
        public override Color ImageMarginGradientBegin => DropDownBackground;
        public override Color ImageMarginGradientMiddle => DropDownBackground;
        public override Color ImageMarginGradientEnd => DropDownBackground;
        public override Color MenuBorder => DropDownBorder;
        public override Color MenuItemBorder => DesignTokens.Accent;
        public override Color MenuItemSelected => ItemHoverBackground;
        public override Color MenuItemSelectedGradientBegin => ItemHoverBackground;
        public override Color MenuItemSelectedGradientEnd => ItemHoverBackground;
        public override Color MenuItemPressedGradientBegin => ItemPressedBackground;
        public override Color MenuItemPressedGradientMiddle => ItemPressedBackground;
        public override Color MenuItemPressedGradientEnd => ItemPressedBackground;
        public override Color SeparatorDark => DesignTokens.BorderSoft;
        public override Color SeparatorLight => DropDownBackground;
    }
}
