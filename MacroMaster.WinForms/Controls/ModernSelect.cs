using MacroMaster.WinForms.Theme;
using System.Drawing.Drawing2D;

namespace MacroMaster.WinForms.Controls;

internal sealed class ModernSelect : Control
{
    private readonly List<string> _items = [];
    private readonly List<string> _dropDownItems = [];
    private bool _isHovered;
    private bool _isPressed;
    private bool _isDropDownOpen;
    private bool _showSelectedItemIndicator;
    private int _selectedIndex = -1;
    private ToolStripDropDown? _dropDownMenu;

    public ModernSelect()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable |
            ControlStyles.UserPaint,
            true);

        Cursor = Cursors.Hand;
        BackColor = DesignTokens.SurfaceInset;
        ForeColor = DesignTokens.TextPrimary;
        Font = DesignTokens.FontUiNormal;
        MinimumSize = new Size(DesignTokens.Scale(120), DesignTokens.Scale(30));
        TabStop = true;
    }

    public event EventHandler? SelectedIndexChanged;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetSelectedIndex(value);
    }

    public object? SelectedItem
    {
        get => _selectedIndex >= 0 && _selectedIndex < _items.Count
            ? _items[_selectedIndex]
            : null;
        set
        {
            string? targetValue = value?.ToString();
            int index = targetValue is null
                ? -1
                : _items.FindIndex(item => string.Equals(item, targetValue, StringComparison.Ordinal));
            SetSelectedIndex(index);
        }
    }

    public bool ShowSelectedItemIndicator
    {
        get => _showSelectedItemIndicator;
        set
        {
            if (_showSelectedItemIndicator == value)
            {
                return;
            }

            _showSelectedItemIndicator = value;
            CloseDropDown();
        }
    }

    public void SetItems(IEnumerable<string> items, IEnumerable<string>? dropDownItems = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        string? selectedValue = SelectedItem?.ToString();

        _items.Clear();
        _items.AddRange(items);
        _dropDownItems.Clear();
        _dropDownItems.AddRange(dropDownItems ?? _items);

        while (_dropDownItems.Count < _items.Count)
        {
            _dropDownItems.Add(_items[_dropDownItems.Count]);
        }

        if (_dropDownItems.Count > _items.Count)
        {
            _dropDownItems.RemoveRange(_items.Count, _dropDownItems.Count - _items.Count);
        }

        int selectedIndex = selectedValue is null
            ? -1
            : _items.FindIndex(item => string.Equals(item, selectedValue, StringComparison.Ordinal));

        if (selectedIndex < 0 && _items.Count > 0)
        {
            selectedIndex = 0;
        }

        SetSelectedIndex(selectedIndex, raiseEvent: false);
        Invalidate();
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

        if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location) && Enabled)
        {
            ShowDropDown();
        }

        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!Enabled)
        {
            base.OnKeyDown(e);
            return;
        }

        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            ShowDropDown();
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Up)
        {
            SetSelectedIndex(Math.Max(0, _selectedIndex - 1));
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.Down)
        {
            SetSelectedIndex(Math.Min(_items.Count - 1, _selectedIndex + 1));
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        Keys keyCode = keyData & Keys.KeyCode;
        return keyCode is Keys.Up or Keys.Down or Keys.Space or Keys.Enter || base.IsInputKey(keyData);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        if (!Enabled)
        {
            CloseDropDown();
        }

        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ToolStripDropDown? menu = _dropDownMenu;
            _dropDownMenu = null;
            menu?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? DesignTokens.Surface);

        Rectangle bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
        using GraphicsPath path = CreateRoundPath(bounds, DesignTokens.Scale(7));
        using var fillBrush = new SolidBrush(ResolveFillColor());
        using var borderPen = new Pen(ResolveBorderColor(), Math.Max(1f, DesignTokens.DensityScale));
        graphics.FillPath(fillBrush, path);
        graphics.DrawPath(borderPen, path);

        Rectangle textBounds = new(
            bounds.Left + DesignTokens.Scale(11),
            bounds.Top,
            Math.Max(0, bounds.Width - DesignTokens.Scale(42)),
            bounds.Height);

        TextRenderer.DrawText(
            graphics,
            SelectedItem?.ToString() ?? string.Empty,
            Font,
            textBounds,
            Enabled ? ForeColor : DesignTokens.TextMuted,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);

        DrawArrow(graphics, bounds);
    }

    private void ShowDropDown()
    {
        if (_items.Count == 0 || _isDropDownOpen)
        {
            return;
        }

        CloseDropDown();

        if (_showSelectedItemIndicator)
        {
            ShowIndicatorDropDown();
            return;
        }

        ShowStandardDropDown();
    }

    private void ShowStandardDropDown()
    {
        var menu = new ContextMenuStrip
        {
            ShowCheckMargin = false,
            ShowImageMargin = false,
            MinimumSize = new Size(Width, 0)
        };
        _dropDownMenu = menu;
        AppToolStripRenderer.ApplyTo(menu);

        for (int index = 0; index < _items.Count; index++)
        {
            var item = new ToolStripMenuItem(GetDropDownItemText(index))
            {
                Checked = index == _selectedIndex,
                Tag = index
            };
            item.Click += (_, _) =>
            {
                if (item.Tag is int itemIndex)
                {
                    SetSelectedIndex(itemIndex);
                }
            };
            menu.Items.Add(item);
        }

        _isDropDownOpen = true;
        Invalidate();
        menu.Closed += (_, _) =>
        {
            _isDropDownOpen = false;
            _isPressed = false;
            if (!IsDisposed)
            {
                Invalidate();
            }

            if (ReferenceEquals(_dropDownMenu, menu))
            {
                _dropDownMenu = null;
            }

            DisposeMenuAfterCurrentMessage(menu);
        };

        menu.Show(this, new Point(0, Height + DesignTokens.Scale(4)));
    }

    private void ShowIndicatorDropDown()
    {
        int rowHeight = DesignTokens.Scale(44);
        Size dropDownSize = CalculateIndicatorDropDownSize(rowHeight);
        var dropDownPanel = new IndicatorDropDownPanel(
            _dropDownItems.ToArray(),
            _selectedIndex,
            dropDownSize.Width,
            rowHeight);

        var host = new ToolStripControlHost(dropDownPanel)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Size = dropDownSize
        };

        var dropDown = new ToolStripDropDown
        {
            AutoClose = true,
            AutoSize = false,
            BackColor = DesignTokens.Surface,
            CanOverflow = false,
            DropShadowEnabled = false,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = Padding.Empty
        };
        dropDown.Items.Add(host);
        dropDown.Size = dropDownSize;

        dropDownPanel.ItemSelected += (_, itemIndex) =>
        {
            SetSelectedIndex(itemIndex);
            dropDown.Close(ToolStripDropDownCloseReason.ItemClicked);
        };
        dropDownPanel.CloseRequested += (_, _) => dropDown.Close(ToolStripDropDownCloseReason.CloseCalled);

        _isDropDownOpen = true;
        Invalidate();
        dropDown.Closed += (_, _) =>
        {
            _isDropDownOpen = false;
            _isPressed = false;
            if (!IsDisposed)
            {
                Invalidate();
            }

            if (ReferenceEquals(_dropDownMenu, dropDown))
            {
                _dropDownMenu = null;
            }

            DisposeMenuAfterCurrentMessage(dropDown);
        };

        _dropDownMenu = dropDown;
        dropDown.Show(this, new Point(0, Height + DesignTokens.Scale(4)));
        dropDownPanel.Focus();
    }

    private void CloseDropDown()
    {
        ToolStripDropDown? menu = _dropDownMenu;
        if (menu is null || menu.IsDisposed)
        {
            return;
        }

        menu.Close();
    }

    private void DisposeMenuAfterCurrentMessage(ToolStripDropDown menu)
    {
        if (menu.IsDisposed)
        {
            return;
        }

        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            menu.Dispose();
            return;
        }

        try
        {
            BeginInvoke((MethodInvoker)(() =>
            {
                if (!menu.IsDisposed)
                {
                    menu.Dispose();
                }
            }));
        }
        catch (ObjectDisposedException)
        {

        }
        catch (InvalidOperationException)
        {
            if (!menu.IsDisposed)
            {
                menu.Dispose();
            }
        }
    }

    private Size CalculateIndicatorDropDownSize(int rowHeight)
    {
        int preferredTextWidth = 0;

        foreach (string item in _dropDownItems)
        {
            preferredTextWidth = Math.Max(
                preferredTextWidth,
                TextRenderer.MeasureText(
                    item,
                    Font,
                    Size.Empty,
                    TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width);
        }

        int horizontalChrome = DesignTokens.Scale(64);
        int preferredWidth = preferredTextWidth + horizontalChrome;
        int width = Math.Max(Width, preferredWidth);
        int height = (_items.Count * rowHeight) + DesignTokens.Scale(2);

        return new Size(width, height);
    }

    private string GetDropDownItemText(int index)
    {
        return index >= 0 && index < _dropDownItems.Count
            ? _dropDownItems[index]
            : string.Empty;
    }

    private void SetSelectedIndex(int value, bool raiseEvent = true)
    {
        int normalizedValue = _items.Count == 0
            ? -1
            : Math.Clamp(value, 0, _items.Count - 1);

        if (_selectedIndex == normalizedValue)
        {
            return;
        }

        _selectedIndex = normalizedValue;
        Invalidate();

        if (raiseEvent)
        {
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private Color ResolveFillColor()
    {
        if (!Enabled)
        {
            return Color.FromArgb(110, DesignTokens.SurfaceInset);
        }

        if (_isPressed || _isDropDownOpen)
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

        if (Focused || _isDropDownOpen)
        {
            return DesignTokens.Accent;
        }

        return _isHovered
            ? DesignTokens.BorderBright
            : DesignTokens.Border;
    }

    private static void DrawArrow(Graphics graphics, Rectangle bounds)
    {
        int arrowSize = DesignTokens.Scale(5);
        int centerX = bounds.Right - DesignTokens.Scale(18);
        int centerY = bounds.Top + bounds.Height / 2 + DesignTokens.Scale(1);

        Point[] points =
        [
            new(centerX - arrowSize, centerY - arrowSize / 2),
            new(centerX + arrowSize, centerY - arrowSize / 2),
            new(centerX, centerY + arrowSize / 2)
        ];

        using var arrowBrush = new SolidBrush(DesignTokens.TextSecondary);
        graphics.FillPolygon(arrowBrush, points);
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

    private sealed class IndicatorDropDownPanel : Control
    {
        private readonly IReadOnlyList<string> _options;
        private readonly int _selectedIndex;
        private readonly int _rowHeight;
        private int _hoveredIndex = -1;
        private int _keyboardIndex;

        public event EventHandler<int>? ItemSelected;
        public event EventHandler? CloseRequested;

        public IndicatorDropDownPanel(
            IReadOnlyList<string> options,
            int selectedIndex,
            int width,
            int rowHeight)
        {
            ArgumentNullException.ThrowIfNull(options);

            _options = options;
            _selectedIndex = selectedIndex;
            _rowHeight = rowHeight;
            _keyboardIndex = selectedIndex >= 0
                ? selectedIndex
                : 0;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.UserPaint,
                true);

            BackColor = DesignTokens.Surface;
            Cursor = Cursors.Hand;
            Font = DesignTokens.FontUiNormal;
            ForeColor = DesignTokens.TextPrimary;
            Size = new Size(width, (options.Count * rowHeight) + DesignTokens.Scale(2));
            TabStop = true;
            AccessibleName = "Seçim seçenekleri";
            AccessibleRole = AccessibleRole.MenuPopup;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(BackColor);

            Rectangle outerBounds = Rectangle.Inflate(ClientRectangle, -1, -1);
            if (outerBounds.Width <= 0 || outerBounds.Height <= 0)
            {
                return;
            }

            using GraphicsPath outerPath = CreateRoundPath(outerBounds, DesignTokens.Scale(8));
            using var backgroundBrush = new SolidBrush(DesignTokens.Surface);
            using var borderPen = new Pen(DesignTokens.BorderSoft);
            e.Graphics.FillPath(backgroundBrush, outerPath);
            e.Graphics.DrawPath(borderPen, outerPath);

            for (int index = 0; index < _options.Count; index++)
            {
                DrawOption(e.Graphics, index);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int nextHoveredIndex = GetOptionIndexAt(e.Location);
            if (_hoveredIndex != nextHoveredIndex)
            {
                _hoveredIndex = nextHoveredIndex;
                if (nextHoveredIndex >= 0)
                {
                    _keyboardIndex = nextHoveredIndex;
                }

                Invalidate();
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoveredIndex = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Focus();
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int index = GetOptionIndexAt(e.Location);
                if (index >= 0)
                {
                    SelectOption(index);
                    return;
                }
            }

            base.OnMouseUp(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys keyCode = keyData & Keys.KeyCode;
            return keyCode is Keys.Up or Keys.Down or Keys.Enter or Keys.Space or Keys.Escape
                || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                    MoveKeyboardSelection(-1);
                    e.Handled = true;
                    return;

                case Keys.Down:
                    MoveKeyboardSelection(1);
                    e.Handled = true;
                    return;

                case Keys.Enter:
                case Keys.Space:
                    SelectOption(_keyboardIndex);
                    e.Handled = true;
                    return;

                case Keys.Escape:
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    return;
            }

            base.OnKeyDown(e);
        }

        private void DrawOption(Graphics graphics, int index)
        {
            bool isSelected = index == _selectedIndex;
            bool isHighlighted = index == _hoveredIndex || (Focused && index == _keyboardIndex);
            Rectangle rowBounds = new(
                DesignTokens.Scale(1),
                DesignTokens.Scale(1) + (index * _rowHeight),
                Math.Max(0, Width - DesignTokens.Scale(2)),
                _rowHeight);

            if (isHighlighted)
            {
                Rectangle highlightBounds = Rectangle.Inflate(
                    rowBounds,
                    -DesignTokens.Scale(6),
                    -DesignTokens.Scale(4));
                using GraphicsPath highlightPath = CreateRoundPath(highlightBounds, DesignTokens.Scale(7));
                using var highlightBrush = new SolidBrush(DesignTokens.SurfaceHover);
                graphics.FillPath(highlightBrush, highlightPath);
            }

            if (isSelected)
            {
                int dotSize = DesignTokens.Scale(7);
                Rectangle dotBounds = new(
                    rowBounds.Left + DesignTokens.Scale(16),
                    rowBounds.Top + ((rowBounds.Height - dotSize) / 2),
                    dotSize,
                    dotSize);
                using var dotBrush = new SolidBrush(DesignTokens.Accent);
                graphics.FillEllipse(dotBrush, dotBounds);
            }

            Rectangle textBounds = new(
                rowBounds.Left + DesignTokens.Scale(42),
                rowBounds.Top,
                Math.Max(0, rowBounds.Width - DesignTokens.Scale(56)),
                rowBounds.Height);

            TextRenderer.DrawText(
                graphics,
                _options[index],
                Font,
                textBounds,
                DesignTokens.TextPrimary,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix);
        }

        private int GetOptionIndexAt(Point location)
        {
            int index = (location.Y - DesignTokens.Scale(1)) / Math.Max(1, _rowHeight);
            return index >= 0 && index < _options.Count
                ? index
                : -1;
        }

        private void MoveKeyboardSelection(int delta)
        {
            if (_options.Count == 0)
            {
                return;
            }

            _keyboardIndex = (_keyboardIndex + delta + _options.Count) % _options.Count;
            _hoveredIndex = -1;
            Invalidate();
        }

        private void SelectOption(int index)
        {
            if (index < 0 || index >= _options.Count)
            {
                return;
            }

            ItemSelected?.Invoke(this, index);
        }
    }
}
