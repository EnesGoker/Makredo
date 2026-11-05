using MacroMaster.Application.Abstractions;
using MacroMaster.WinForms.Theme;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace MacroMaster.WinForms.Controls;

internal sealed class MacroLibraryControl : UserControl
{
    private static readonly MacroLibraryFilterOption[] FilterOptions =
    [
        new(MacroLibraryFilterKind.All, "Tüm makrolar"),
        new(MacroLibraryFilterKind.Favorites, "Favoriler"),
        new(MacroLibraryFilterKind.Recent, "Son kullanılanlar"),
        new(MacroLibraryFilterKind.Json, "JSON dosyaları"),
        new(MacroLibraryFilterKind.Xml, "XML dosyaları"),
        new(MacroLibraryFilterKind.Short, "Kısa makrolar"),
        new(MacroLibraryFilterKind.Long, "Uzun makrolar")
    ];

    private readonly Panel _listViewportPanel;
    private readonly FlowLayoutPanel _macroListPanel;
    private readonly ModernScrollBar _listScrollBar;
    private readonly Label _emptyStateLabel;
    private readonly Label _totalMacroValueLabel;
    private readonly Label _totalEventValueLabel;
    private readonly TextBox _searchTextBox;
    private readonly FilterIconButton _filterButton;
    private TableLayoutPanel _rootLayoutPanel = null!;
    private TableLayoutPanel _headerLayoutPanel = null!;
    private TableLayoutPanel _footerLayoutPanel = null!;
    private TableLayoutPanel _listHostPanel = null!;
    private RoundedPanel _searchPanel = null!;
    private RoundedPanel _footerPanel = null!;
    private Button _addButton = null!;
    private Label _titleLabel = null!;
    private Label _totalMacroCaptionLabel = null!;
    private Label _totalEventCaptionLabel = null!;
    private RowStyle _headerRowStyle = null!;
    private RowStyle _searchRowStyle = null!;
    private RowStyle _footerRowStyle = null!;
    private ColumnStyle _filterButtonColumnStyle = null!;
    private ColumnStyle _addButtonColumnStyle = null!;
    private ColumnStyle _scrollColumnStyle = null!;
    private ToolStripDropDown? _filterDropDown;
    private IReadOnlyList<MacroLibraryViewItem> _items = [];
    private MacroLibraryFilterKind _selectedFilterKind = MacroLibraryFilterKind.All;
    private string? _selectedFilePath;

    public event EventHandler? AddRequested;
    public event EventHandler<MacroLibraryItemEventArgs>? LoadRequested;
    public event EventHandler<MacroLibraryItemEventArgs>? RenameRequested;
    public event EventHandler<MacroLibraryItemEventArgs>? DeleteRequested;
    public event EventHandler<MacroLibraryItemEventArgs>? FavoriteToggled;
    public event EventHandler<MacroLibraryItemEventArgs>? OptimizeRequested;

    public MacroLibraryControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        DoubleBuffered = true;
        BackColor = DesignTokens.Surface;
        ForeColor = DesignTokens.TextPrimary;
        Font = DesignTokens.FontUiNormal;

        _listViewportPanel = new Panel();
        _macroListPanel = new FlowLayoutPanel();
        _listScrollBar = new ModernScrollBar();
        _emptyStateLabel = CreateEmptyStateLabel();
        _totalMacroValueLabel = CreateFooterValueLabel();
        _totalEventValueLabel = CreateFooterValueLabel();
        _searchTextBox = CreateSearchTextBox();
        _filterButton = new FilterIconButton();

        BuildLayout();
        SetItems([], null);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_filterDropDown is not null)
            {
                ToolStripDropDown dropDown = _filterDropDown;
                _filterDropDown = null;

                if (!dropDown.IsDisposed)
                {
                    dropDown.Dispose();
                }
            }
        }

        base.Dispose(disposing);
    }

    public void SetItems(
        IReadOnlyList<MacroLibraryViewItem> items,
        string? selectedFilePath)
    {
        ArgumentNullException.ThrowIfNull(items);

        _items = items;
        _selectedFilePath = selectedFilePath;
        ApplyFilter();
    }

    internal void ApplyShellLayoutProfile(AppShellLayoutProfile profile)
    {
        AppShellMainMetrics main = profile.Main;

        SuspendLayout();
        try
        {
            _headerRowStyle.Height = main.LibraryHeaderHeight;
            _searchRowStyle.Height = main.LibrarySearchHeight;
            _footerRowStyle.Height = main.LibraryFooterHeight;
            _headerLayoutPanel.Margin = main.LibraryHeaderMargin;
            _filterButtonColumnStyle.Width = main.LibraryIconButtonWidth;
            _addButtonColumnStyle.Width = main.LibraryIconButtonWidth;
            _filterButton.Margin = main.LibraryButtonMargin;
            _addButton.Margin = main.LibraryButtonMargin;
            _filterButton.MinimumSize = Size.Empty;
            _addButton.MinimumSize = Size.Empty;

            _searchPanel.Margin = main.LibrarySearchMargin;
            _searchPanel.Padding = main.LibrarySearchPadding;
            _scrollColumnStyle.Width = main.LibraryScrollColumnWidth;
            _listScrollBar.Margin = new Padding(Math.Max(1, main.LibraryButtonMargin.Left / 2), 0, 0, 0);

            _footerPanel.Margin = main.LibraryFooterMargin;
            _footerPanel.Padding = main.LibraryFooterPadding;
            _footerLayoutPanel.ColumnStyles[0].Width = main.LibraryFooterMacroCaptionPercent;
            _footerLayoutPanel.ColumnStyles[1].Width = main.LibraryFooterMacroValuePercent;
            _footerLayoutPanel.ColumnStyles[2].Width = main.LibraryFooterEventCaptionPercent;
            _footerLayoutPanel.ColumnStyles[3].Width = main.LibraryFooterEventValuePercent;
            _totalMacroCaptionLabel.Text = profile.Mode == AppShellLayoutMode.Constrained
                ? "Makro"
                : "Toplam Makro";
            _totalEventCaptionLabel.Text = profile.Mode == AppShellLayoutMode.Constrained
                ? "Olay"
                : "Toplam Olay";
        }
        finally
        {
            ResumeLayout(performLayout: true);
        }

        ResizeLibraryRows();
        UpdateListScrollLayout();
    }

    private void ApplyFilter()
    {
        string searchTerm = _searchTextBox.Text.Trim();
        MacroLibraryViewItem[] filteredItems =
            MacroLibraryFilterEngine.Apply(_items, searchTerm, _selectedFilterKind);

        _macroListPanel.SuspendLayout();

        try
        {
            ClearMacroListPanel();

            if (filteredItems.Length == 0)
            {
                _emptyStateLabel.Text = _items.Count == 0
                    ? "Kayıtlı makro yok. Kaydet butonu ile kütüphaneye ekleyebilirsin."
                    : "Filtreyle eşleşen makro bulunamadı.";
                _macroListPanel.Controls.Add(_emptyStateLabel);
            }
            else
            {
                foreach (MacroLibraryViewItem item in filteredItems)
                {
                    bool isSelected = IsSamePath(item.Entry.FilePath, _selectedFilePath);
                    var row = new MacroLibraryRow(item, isSelected);
                    row.Activated += (_, _) => LoadRequested?.Invoke(this, new MacroLibraryItemEventArgs(item));
                    row.RenameRequested += (_, _) => RenameRequested?.Invoke(this, new MacroLibraryItemEventArgs(item));
                    row.DeleteRequested += (_, _) => DeleteRequested?.Invoke(this, new MacroLibraryItemEventArgs(item));
                    row.FavoriteToggled += (_, _) => FavoriteToggled?.Invoke(this, new MacroLibraryItemEventArgs(item));
                    row.OptimizeRequested += (_, _) => OptimizeRequested?.Invoke(this, new MacroLibraryItemEventArgs(item));
                    WireMouseWheelForwarding(row);
                    _macroListPanel.Controls.Add(row);
                }
            }
        }
        finally
        {
            _macroListPanel.ResumeLayout(performLayout: false);
        }

        int totalEventCount = filteredItems.Sum(item => Math.Max(0, item.Entry.EventCount));
        _totalMacroValueLabel.Text = filteredItems.Length.ToString(CultureInfo.InvariantCulture);
        _totalEventValueLabel.Text = totalEventCount.ToString("N0", CultureInfo.GetCultureInfo("tr-TR"));
        ResizeLibraryRows();
        UpdateListScrollLayout();
        _macroListPanel.PerformLayout();
    }

    private void ClearMacroListPanel()
    {
        while (_macroListPanel.Controls.Count > 0)
        {
            Control control = _macroListPanel.Controls[0];
            _macroListPanel.Controls.RemoveAt(0);

            if (!ReferenceEquals(control, _emptyStateLabel))
            {
                control.Dispose();
            }
        }
    }

    private void BuildLayout()
    {
        _rootLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = DesignTokens.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _rootLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _headerRowStyle = new RowStyle(SizeType.Absolute, DesignTokens.Scale(42));
        _searchRowStyle = new RowStyle(SizeType.Absolute, DesignTokens.Scale(44));
        _footerRowStyle = new RowStyle(SizeType.Absolute, DesignTokens.Scale(50));
        _rootLayoutPanel.RowStyles.Add(_headerRowStyle);
        _rootLayoutPanel.RowStyles.Add(_searchRowStyle);
        _rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _rootLayoutPanel.RowStyles.Add(_footerRowStyle);

        _headerLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = DesignTokens.Surface,
            Margin = new Padding(0, 0, DesignTokens.Scale(12), 0),
            Padding = Padding.Empty
        };
        _headerLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _filterButtonColumnStyle = new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(38));
        _addButtonColumnStyle = new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(38));
        _headerLayoutPanel.ColumnStyles.Add(_filterButtonColumnStyle);
        _headerLayoutPanel.ColumnStyles.Add(_addButtonColumnStyle);

        _titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Makro Kütüphanesi",
            Font = DesignTokens.FontUiBold,
            ForeColor = DesignTokens.TextPrimary,
            BackColor = DesignTokens.Surface,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        _addButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "+",
            BackColor = DesignTokens.Surface2,
            ForeColor = DesignTokens.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Font = DesignTokens.FontUiBold,
            Margin = new Padding(DesignTokens.Scale(8), DesignTokens.Scale(3), 0, DesignTokens.Scale(7)),
            UseVisualStyleBackColor = false
        };
        _addButton.FlatAppearance.BorderColor = DesignTokens.BorderSoft;
        _addButton.FlatAppearance.BorderSize = 1;
        _addButton.Click += (_, _) => AddRequested?.Invoke(this, EventArgs.Empty);

        _filterButton.Dock = DockStyle.Fill;
        _filterButton.Margin = new Padding(DesignTokens.Scale(8), DesignTokens.Scale(3), 0, DesignTokens.Scale(7));
        _filterButton.Click += (_, _) => ShowFilterMenu();

        _headerLayoutPanel.Controls.Add(_titleLabel, 0, 0);
        _headerLayoutPanel.Controls.Add(_filterButton, 1, 0);
        _headerLayoutPanel.Controls.Add(_addButton, 2, 0);

        _searchPanel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.SurfaceInset,
            BorderColor = DesignTokens.BorderSoft,
            Margin = new Padding(
                0,
                DesignTokens.Scale(3),
                DesignTokens.Scale(12),
                DesignTokens.Scale(8)),
            Padding = new Padding(
                DesignTokens.Scale(14),
                DesignTokens.Scale(5),
                DesignTokens.Scale(10),
                DesignTokens.Scale(4))
        };
        _searchPanel.Click += (_, _) => _searchTextBox.Focus();
        _searchPanel.Controls.Add(_searchTextBox);

        _listHostPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = DesignTokens.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _listHostPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _scrollColumnStyle = new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(20));
        _listHostPanel.ColumnStyles.Add(_scrollColumnStyle);

        _listViewportPanel.Dock = DockStyle.Fill;
        _listViewportPanel.BackColor = DesignTokens.Surface;
        _listViewportPanel.Margin = Padding.Empty;
        _listViewportPanel.Padding = Padding.Empty;
        _listViewportPanel.Resize += (_, _) => UpdateListScrollLayout();
        _listViewportPanel.MouseWheel += ListViewportPanel_MouseWheel;

        _macroListPanel.Dock = DockStyle.None;
        _macroListPanel.FlowDirection = FlowDirection.TopDown;
        _macroListPanel.WrapContents = false;
        _macroListPanel.AutoScroll = false;
        _macroListPanel.BackColor = DesignTokens.Surface;
        _macroListPanel.Margin = Padding.Empty;
        _macroListPanel.Padding = new Padding(0, 2, 0, 0);
        _macroListPanel.Resize += (_, _) => ResizeLibraryRows();
        _macroListPanel.MouseWheel += ListViewportPanel_MouseWheel;

        _listScrollBar.Dock = DockStyle.Fill;
        _listScrollBar.Margin = new Padding(DesignTokens.Scale(4), 0, 0, 0);
        _listScrollBar.ValueChanged += (_, _) => ScrollListTo(_listScrollBar.Value);

        _listViewportPanel.Controls.Add(_macroListPanel);
        _listHostPanel.Controls.Add(_listViewportPanel, 0, 0);
        _listHostPanel.Controls.Add(_listScrollBar, 1, 0);

        _rootLayoutPanel.Controls.Add(_headerLayoutPanel, 0, 0);
        _rootLayoutPanel.Controls.Add(_searchPanel, 0, 1);
        _rootLayoutPanel.Controls.Add(_listHostPanel, 0, 2);
        _rootLayoutPanel.Controls.Add(CreateFooterPanel(), 0, 3);

        Controls.Add(_rootLayoutPanel);
    }

    private RoundedPanel CreateFooterPanel()
    {
        _footerPanel = new RoundedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.SurfaceInset,
            BorderColor = DesignTokens.BorderSoft,
            Margin = new Padding(0, DesignTokens.Scale(8), DesignTokens.Scale(12), 0),
            Padding = new Padding(DesignTokens.Scale(12), DesignTokens.Scale(5), DesignTokens.Scale(12), DesignTokens.Scale(5))
        };

        _footerLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _footerLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
        _footerLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12f));
        _footerLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
        _footerLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16f));

        _totalMacroCaptionLabel = CreateFooterCaptionLabel("Toplam Makro");
        _totalEventCaptionLabel = CreateFooterCaptionLabel("Toplam Olay");
        _footerLayoutPanel.Controls.Add(_totalMacroCaptionLabel, 0, 0);
        _footerLayoutPanel.Controls.Add(_totalMacroValueLabel, 1, 0);
        _footerLayoutPanel.Controls.Add(_totalEventCaptionLabel, 2, 0);
        _footerLayoutPanel.Controls.Add(_totalEventValueLabel, 3, 0);

        _footerPanel.Controls.Add(_footerLayoutPanel);
        return _footerPanel;
    }

    private void ResizeLibraryRows()
    {
        int rowWidth = Math.Max(
            180,
            _listViewportPanel.ClientSize.Width - DesignTokens.Scale(4));

        foreach (Control control in _macroListPanel.Controls)
        {
            control.Width = rowWidth;
        }
    }

    private void UpdateListScrollLayout()
    {
        if (_listViewportPanel.ClientSize.Width <= 0 || _listViewportPanel.ClientSize.Height <= 0)
        {
            return;
        }

        ResizeLibraryRows();

        int contentHeight = CalculateListContentHeight();
        int viewportHeight = _listViewportPanel.ClientSize.Height;
        int maximum = Math.Max(0, contentHeight - viewportHeight);
        int nextValue = Math.Min(_listScrollBar.Value, maximum);

        _listScrollBar.SetRange(maximum, viewportHeight, nextValue);
        _listScrollBar.Visible = maximum > 0;

        _macroListPanel.Bounds = new Rectangle(
            0,
            -nextValue,
            Math.Max(0, _listViewportPanel.ClientSize.Width - DesignTokens.Scale(4)),
            Math.Max(viewportHeight, contentHeight));
    }

    private int CalculateListContentHeight()
    {
        int contentHeight = _macroListPanel.Padding.Vertical;

        foreach (Control control in _macroListPanel.Controls)
        {
            contentHeight += control.Height + control.Margin.Vertical;
        }

        return contentHeight;
    }

    private void ScrollListTo(int value)
    {
        _macroListPanel.Top = -Math.Max(0, value);
    }

    private void ListViewportPanel_MouseWheel(object? sender, MouseEventArgs e)
    {
        _ = sender;

        int wheelStep = Math.Max(DesignTokens.Scale(48), SystemInformation.MouseWheelScrollLines * DesignTokens.Scale(18));
        _listScrollBar.Value -= Math.Sign(e.Delta) * wheelStep;
    }

    private void WireMouseWheelForwarding(Control control)
    {
        control.MouseWheel += ListViewportPanel_MouseWheel;

        foreach (Control child in control.Controls)
        {
            WireMouseWheelForwarding(child);
        }
    }

    private static Label CreateFooterCaptionLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = text,
            Font = DesignTokens.FontUiNormal,
            ForeColor = DesignTokens.TextSecondary,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
    }

    private static Label CreateFooterValueLabel()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Font = DesignTokens.FontUiBold,
            ForeColor = DesignTokens.Accent,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true
        };
    }

    private static Label CreateEmptyStateLabel()
    {
        return new Label
        {
            Height = DesignTokens.Scale(96),
            Font = DesignTokens.FontUiNormal,
            ForeColor = DesignTokens.TextMuted,
            BackColor = Color.Transparent,
            Text = "Kayıtlı makro yok. Kaydet butonu ile kütüphaneye ekleyebilirsin.",
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };
    }

    private TextBox CreateSearchTextBox()
    {
        var textBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            PlaceholderText = "Makro ara...",
            BackColor = DesignTokens.SurfaceInset,
            ForeColor = DesignTokens.TextPrimary,
            Font = DesignTokens.FontUiNormal,
            Margin = Padding.Empty
        };

        textBox.TextChanged += (_, _) => ApplyFilter();
        return textBox;
    }

    private void ShowFilterMenu()
    {
        CloseFilterDropDown();

        Size dropDownSize = CalculateFilterDropDownSize();
        int rowHeight = DesignTokens.Scale(44);
        var filterMenu = new FilterDropDownPanel(
            FilterOptions,
            _selectedFilterKind,
            dropDownSize.Width,
            rowHeight);

        var host = new ToolStripControlHost(filterMenu)
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

        filterMenu.FilterSelected += (_, filterKind) =>
        {
            SetFilter(filterKind);
            dropDown.Close(ToolStripDropDownCloseReason.ItemClicked);
        };
        filterMenu.CloseRequested += (_, _) => dropDown.Close(ToolStripDropDownCloseReason.CloseCalled);
        dropDown.Closed += (_, _) =>
        {
            if (ReferenceEquals(_filterDropDown, dropDown))
            {
                _filterDropDown = null;
            }

            DisposeFilterDropDownAfterCurrentMessage(dropDown);
        };

        _filterDropDown = dropDown;
        dropDown.Show(this, ResolveFilterDropDownLocation(dropDownSize));
        filterMenu.Focus();
    }

    private void CloseFilterDropDown()
    {
        if (_filterDropDown is null)
        {
            return;
        }

        ToolStripDropDown dropDown = _filterDropDown;
        _filterDropDown = null;

        if (!dropDown.IsDisposed)
        {
            dropDown.Close(ToolStripDropDownCloseReason.CloseCalled);
        }
    }

    private Size CalculateFilterDropDownSize()
    {
        int rowHeight = DesignTokens.Scale(44);
        int verticalPadding = DesignTokens.Scale(2);
        int horizontalChrome = DesignTokens.Scale(64);
        int preferredTextWidth = 0;

        foreach (MacroLibraryFilterOption option in FilterOptions)
        {
            preferredTextWidth = Math.Max(
                preferredTextWidth,
                TextRenderer.MeasureText(
                    option.Label,
                    DesignTokens.FontUiNormal,
                    Size.Empty,
                    TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width);
        }

        int minimumWidth = DesignTokens.Scale(188);
        int preferredWidth = preferredTextWidth + horizontalChrome;
        int maximumWidth = Math.Max(minimumWidth, ClientSize.Width - DesignTokens.Scale(28));
        int width = Math.Min(maximumWidth, Math.Max(minimumWidth, preferredWidth));
        int height = (FilterOptions.Length * rowHeight) + verticalPadding;

        return new Size(width, height);
    }

    private Point ResolveFilterDropDownLocation(Size dropDownSize)
    {
        int gap = DesignTokens.Scale(8);
        int outerMargin = DesignTokens.Scale(12);
        Point buttonLocation = PointToClient(_filterButton.PointToScreen(Point.Empty));

        int preferredX = buttonLocation.X + _filterButton.Width - dropDownSize.Width;
        int maxX = Math.Max(0, ClientSize.Width - dropDownSize.Width - outerMargin);
        int x = Math.Min(maxX, Math.Max(0, preferredX));
        int y = buttonLocation.Y + _filterButton.Height + gap;

        return new Point(x, y);
    }

    private void DisposeFilterDropDownAfterCurrentMessage(ToolStripDropDown dropDown)
    {
        if (dropDown.IsDisposed)
        {
            return;
        }

        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            dropDown.Dispose();
            return;
        }

        try
        {
            BeginInvoke((MethodInvoker)(() =>
            {
                if (!dropDown.IsDisposed)
                {
                    dropDown.Dispose();
                }
            }));
        }
        catch (ObjectDisposedException)
        {
            // The owner can be torn down while the drop-down is closing.
        }
        catch (InvalidOperationException)
        {
            if (!dropDown.IsDisposed)
            {
                dropDown.Dispose();
            }
        }
    }

    private void SetFilter(MacroLibraryFilterKind filterKind)
    {
        if (_selectedFilterKind == filterKind)
        {
            return;
        }

        _selectedFilterKind = filterKind;
        _filterButton.IsActive = filterKind != MacroLibraryFilterKind.All;
        ApplyFilter();
    }

    private void DisposeMenuAfterCurrentMessage(ContextMenuStrip menu)
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
            // The owner can be torn down while the drop-down is closing.
        }
        catch (InvalidOperationException)
        {
            if (!menu.IsDisposed)
            {
                menu.Dispose();
            }
        }
    }

    private sealed class FilterDropDownPanel : Control
    {
        private readonly IReadOnlyList<MacroLibraryFilterOption> _options;
        private readonly MacroLibraryFilterKind _selectedKind;
        private readonly int _rowHeight;
        private int _hoveredIndex = -1;
        private int _keyboardIndex;

        public event EventHandler<MacroLibraryFilterKind>? FilterSelected;
        public event EventHandler? CloseRequested;

        public FilterDropDownPanel(
            IReadOnlyList<MacroLibraryFilterOption> options,
            MacroLibraryFilterKind selectedKind,
            int width,
            int rowHeight)
        {
            ArgumentNullException.ThrowIfNull(options);

            _options = options;
            _selectedKind = selectedKind;
            _rowHeight = rowHeight;
            _keyboardIndex = Math.Max(0, FindSelectedIndex(options, selectedKind));

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.UserPaint,
                true);

            BackColor = DesignTokens.Surface;
            Cursor = Cursors.Hand;
            Size = new Size(width, (options.Count * rowHeight) + DesignTokens.Scale(2));
            TabStop = true;
            AccessibleName = "Makro filtre seçenekleri";
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
            MacroLibraryFilterOption option = _options[index];
            bool isSelected = option.Kind == _selectedKind;
            bool isHighlighted = index == _hoveredIndex || (Focused && index == _keyboardIndex);
            Rectangle rowBounds = new(
                DesignTokens.Scale(1),
                DesignTokens.Scale(1) + (index * _rowHeight),
                Math.Max(0, Width - DesignTokens.Scale(2)),
                _rowHeight);

            if (isHighlighted)
            {
                Rectangle highlightBounds = Rectangle.Inflate(rowBounds, -DesignTokens.Scale(6), -DesignTokens.Scale(4));
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
                option.Label,
                DesignTokens.FontUiNormal,
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

            FilterSelected?.Invoke(this, _options[index].Kind);
        }

        private static int FindSelectedIndex(
            IReadOnlyList<MacroLibraryFilterOption> options,
            MacroLibraryFilterKind selectedKind)
        {
            for (int index = 0; index < options.Count; index++)
            {
                if (options[index].Kind == selectedKind)
                {
                    return index;
                }
            }

            return 0;
        }
    }

    private sealed class FilterIconButton : Control
    {
        private bool _isHovered;
        private bool _isPressed;
        private bool _isActive;

        public FilterIconButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.UserPaint,
                true);

            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleName = "Makro filtresi";
            AccessibleRole = AccessibleRole.PushButton;
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value)
                {
                    return;
                }

                _isActive = value;
                Invalidate();
            }
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

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? DesignTokens.Surface);

            Rectangle bounds = Rectangle.Inflate(ClientRectangle, -1, -1);

            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            using GraphicsPath backgroundPath = CreateRoundPath(bounds, DesignTokens.Scale(4));
            using var fillBrush = new SolidBrush(ResolveFillColor());
            using var borderPen = new Pen(ResolveBorderColor());
            e.Graphics.FillPath(fillBrush, backgroundPath);
            e.Graphics.DrawPath(borderPen, backgroundPath);

            DrawFilterGlyph(e.Graphics, bounds);
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
                OnClick(EventArgs.Empty);
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        private Color ResolveFillColor()
        {
            if (_isPressed)
            {
                return DesignTokens.Surface3;
            }

            if (_isActive)
            {
                return Color.FromArgb(30, DesignTokens.Accent);
            }

            return _isHovered
                ? DesignTokens.SurfaceHover
                : DesignTokens.Surface2;
        }

        private Color ResolveBorderColor()
        {
            if (_isActive || Focused)
            {
                return DesignTokens.Accent;
            }

            return _isHovered
                ? DesignTokens.BorderBright
                : DesignTokens.BorderSoft;
        }

        private void DrawFilterGlyph(Graphics graphics, Rectangle bounds)
        {
            int iconWidth = DesignTokens.Scale(14);
            int iconHeight = DesignTokens.Scale(13);
            int left = bounds.Left + (bounds.Width - iconWidth) / 2;
            int top = bounds.Top + (bounds.Height - iconHeight) / 2;
            int right = left + iconWidth;
            int bottom = top + iconHeight;
            int centerX = left + iconWidth / 2;
            int neckY = top + DesignTokens.Scale(6);
            int stemBottom = bottom - DesignTokens.Scale(1);

            using var path = new GraphicsPath();
            path.AddLine(left, top, right, top);
            path.AddLine(right, top, centerX + DesignTokens.Scale(3), neckY);
            path.AddLine(centerX + DesignTokens.Scale(3), neckY, centerX + DesignTokens.Scale(1), neckY);
            path.AddLine(centerX + DesignTokens.Scale(1), neckY, centerX + DesignTokens.Scale(1), stemBottom);
            path.AddLine(centerX + DesignTokens.Scale(1), stemBottom, centerX - DesignTokens.Scale(1), stemBottom);
            path.AddLine(centerX - DesignTokens.Scale(1), stemBottom, centerX - DesignTokens.Scale(1), neckY);
            path.AddLine(centerX - DesignTokens.Scale(1), neckY, centerX - DesignTokens.Scale(3), neckY);
            path.CloseFigure();

            using var iconBrush = new SolidBrush(_isActive ? DesignTokens.Accent : DesignTokens.TextSecondary);
            graphics.FillPath(iconBrush, path);
        }
    }

    private sealed class MacroLibraryRow : RoundedPanel
    {
        private readonly MacroLibraryViewItem _item;
        private readonly bool _isSelected;

        public event EventHandler? Activated;
        public event EventHandler? RenameRequested;
        public event EventHandler? DeleteRequested;
        public event EventHandler? FavoriteToggled;
        public event EventHandler? OptimizeRequested;

        public MacroLibraryRow(MacroLibraryViewItem item, bool isSelected)
        {
            _item = item;
            _isSelected = isSelected;
            Height = DesignTokens.Scale(68);
            Width = 320;
            Margin = new Padding(0, 0, 0, DesignTokens.Scale(8));
            Padding = new Padding(
                DesignTokens.Scale(14),
                DesignTokens.Scale(9),
                DesignTokens.Scale(10),
                DesignTokens.Scale(9));
            BackColor = isSelected
                ? Color.FromArgb(20, 56, 98)
                : DesignTokens.SurfaceInset;
            BorderColor = isSelected
                ? Color.FromArgb(125, DesignTokens.Accent)
                : DesignTokens.BorderSoft;

            BuildRow();
            BuildContextMenu();
            WireActivation(this);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            UpdateWidthFromParent();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateWidthFromParent();
        }

        private void UpdateWidthFromParent()
        {
            if (Parent is not null)
            {
                Width = Math.Max(
                    180,
                    Parent.ClientSize.Width - DesignTokens.Scale(4));
            }
        }

        private void BuildRow()
        {
            var layoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(22)));

            var textLayoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            textLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.Scale(31)));
            textLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            textLayoutPanel.Controls.Add(
                new Label
                {
                    Dock = DockStyle.Fill,
                    Text = _item.Entry.Name,
                    Font = DesignTokens.FontUiBold,
                    ForeColor = DesignTokens.TextPrimary,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.BottomLeft,
                    AutoEllipsis = true
                },
                0,
                0);
            textLayoutPanel.Controls.Add(
                new Label
                {
                    Dock = DockStyle.Fill,
                    Text = FormatLastModified(_item.Entry.LastModifiedUtc),
                    Font = DesignTokens.FontUiNormal,
                    ForeColor = DesignTokens.TextSecondary,
                    BackColor = Color.Transparent,
                    TextAlign = ContentAlignment.TopLeft,
                    AutoEllipsis = true
                },
                0,
                1);

            layoutPanel.Controls.Add(textLayoutPanel, 0, 0);

            if (_item.IsFavorite)
            {
                layoutPanel.Controls.Add(
                    new FavoriteMarker
                    {
                        Dock = DockStyle.Fill,
                        Margin = new Padding(DesignTokens.Scale(4), 0, 0, 0)
                    },
                    1,
                    0);
            }

            Controls.Add(layoutPanel);
        }

        private void BuildContextMenu()
        {
            var contextMenu = new ContextMenuStrip
            {
                AutoClose = true,
                DropShadowEnabled = false,
                MinimumSize = new Size(DesignTokens.Scale(176), 0),
                ShowCheckMargin = false,
                ShowImageMargin = false
            };
            AppToolStripRenderer.ApplyTo(
                contextMenu,
                AppToolStripMenuDensity.Comfortable);

            var favoriteItem = new ToolStripMenuItem(_item.IsFavorite ? "Favoriden Çıkar" : "Favoriye Ekle")
            {
                AccessibleName = _item.IsFavorite ? "Favoriden çıkar" : "Favoriye ekle"
            };
            favoriteItem.Click += (_, _) => FavoriteToggled?.Invoke(this, EventArgs.Empty);

            var optimizeItem = new ToolStripMenuItem("Optimize Et")
            {
                AccessibleName = "Makroyu optimize et",
                Enabled = _item.Entry.EventCount > 0
            };
            optimizeItem.Click += (_, _) => OptimizeRequested?.Invoke(this, EventArgs.Empty);

            var renameItem = new ToolStripMenuItem("İsim Düzenle")
            {
                AccessibleName = "Makro ismini düzenle"
            };
            renameItem.Click += (_, _) => RenameRequested?.Invoke(this, EventArgs.Empty);

            var deleteItem = new ToolStripMenuItem("Sil")
            {
                AccessibleName = "Makroyu sil"
            };
            deleteItem.Click += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);
            contextMenu.Items.Add(favoriteItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(optimizeItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(renameItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(deleteItem);
            ContextMenuStrip = contextMenu;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ContextMenuStrip?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void WireActivation(Control control)
        {
            control.Click += (_, _) => Activated?.Invoke(this, EventArgs.Empty);
            control.ContextMenuStrip = ContextMenuStrip;

            foreach (Control child in control.Controls)
            {
                WireActivation(child);
            }
        }

        private static string FormatLastModified(DateTime lastModifiedUtc)
        {
            return lastModifiedUtc
                .ToLocalTime()
                .ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("tr-TR"));
        }

        private sealed class FavoriteMarker : Control
        {
            public FavoriteMarker()
            {
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.UserPaint,
                    true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.Clear(ResolveEffectiveBackColor());

                int starSize = DesignTokens.Scale(12);
                Rectangle bounds = new(
                    ClientRectangle.Left + (ClientRectangle.Width - starSize) / 2,
                    ClientRectangle.Top + DesignTokens.Scale(9),
                    starSize,
                    starSize);

                using GraphicsPath path = CreateStarPath(bounds);
                using var fillBrush = new SolidBrush(DesignTokens.Accent);
                e.Graphics.FillPath(fillBrush, path);
            }

            protected override void OnPaintBackground(PaintEventArgs pevent)
            {
                // The marker paints the effective row background itself to avoid
                // transparent TableLayoutPanel cells rendering as black rectangles.
            }

            private Color ResolveEffectiveBackColor()
            {
                for (Control? current = Parent; current is not null; current = current.Parent)
                {
                    if (current.BackColor.A > 0 && current.BackColor != Color.Transparent)
                    {
                        return current.BackColor;
                    }
                }

                return DesignTokens.SurfaceInset;
            }

            private static GraphicsPath CreateStarPath(Rectangle bounds)
            {
                var path = new GraphicsPath();
                PointF center = new(
                    bounds.Left + bounds.Width / 2f,
                    bounds.Top + bounds.Height / 2f);
                float outerRadius = bounds.Width / 2f;
                float innerRadius = outerRadius * 0.46f;
                var points = new PointF[10];

                for (int index = 0; index < points.Length; index++)
                {
                    double angle = -Math.PI / 2 + index * Math.PI / 5;
                    float radius = index % 2 == 0
                        ? outerRadius
                        : innerRadius;
                    points[index] = new PointF(
                        center.X + (float)Math.Cos(angle) * radius,
                        center.Y + (float)Math.Sin(angle) * radius);
                }

                path.AddPolygon(points);
                return path;
            }
        }
    }

    private class RoundedPanel : Panel
    {
        public Color BorderColor { get; set; } = DesignTokens.Border;

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

            using GraphicsPath path = CreateRoundPath(bounds, DesignTokens.Radius);
            using var fillBrush = new SolidBrush(BackColor);
            using var borderPen = new Pen(BorderColor);
            e.Graphics.FillPath(fillBrush, path);

            if (BorderColor.A > 0)
            {
                e.Graphics.DrawPath(borderPen, path);
            }
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

    private static bool IsSamePath(string left, string? right)
    {
        return !string.IsNullOrWhiteSpace(right)
            && string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
    }

}

internal sealed class MacroLibraryItemEventArgs : EventArgs
{
    public MacroLibraryItemEventArgs(MacroLibraryViewItem viewItem)
    {
        ViewItem = viewItem;
        Item = viewItem.Entry;
    }

    public MacroLibraryEntry Item { get; }

    public MacroLibraryViewItem ViewItem { get; }
}
