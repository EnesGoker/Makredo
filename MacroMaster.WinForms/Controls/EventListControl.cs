using MacroMaster.Domain.Enums;
using MacroMaster.Domain.Models;
using MacroMaster.WinForms.Theme;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace MacroMaster.WinForms.Controls;

internal sealed class EventListControl : UserControl
{
    private static readonly TypeFilterOption[] TypeFilterOptions =
    [
        new(EventListTypeFilterKind.All, "Tür: Tümü", "Tümü"),
        new(EventListTypeFilterKind.Keyboard, "Tür: Klavye", "Klavye"),
        new(EventListTypeFilterKind.Mouse, "Tür: Fare", "Fare")
    ];

    private static readonly SmartFilterOption[] SmartFilterOptions =
    [
        new(EventListSmartFilterKind.All, "Analiz: Tümü", "Tümü"),
        new(EventListSmartFilterKind.LongDelays, "Analiz: Uzun beklemeler", "Uzun beklemeler"),
        new(EventListSmartFilterKind.OptimizationCandidates, "Analiz: Optimizasyon", "Optimizasyon adayları"),
        new(EventListSmartFilterKind.InvalidOrIncomplete, "Analiz: Hatalı/eksik", "Hatalı/eksik")
    ];

    private readonly DataGridView _eventGridView;
    private readonly ModernScrollBar _eventScrollBar;
    private readonly RoundedGridHostPanel _gridHostPanel;
    private readonly ContextMenuStrip _eventContextMenu;
    private readonly Label _emptyStateLabel;
    private readonly Label _headerTitleLabel;
    private readonly TextBox _filterSearchTextBox;
    private readonly ModernSelect _typeFilterSelect;
    private readonly ModernSelect _smartFilterSelect;
    private TableLayoutPanel _rootLayoutPanel = null!;
    private TableLayoutPanel _toolbarPanel = null!;
    private RoundedInputHostPanel _searchPanel = null!;
    private RowStyle _headerRowStyle = null!;
    private ColumnStyle _searchColumnStyle = null!;
    private ColumnStyle _typeGapColumnStyle = null!;
    private ColumnStyle _typeFilterColumnStyle = null!;
    private ColumnStyle _smartGapColumnStyle = null!;
    private ColumnStyle _smartFilterColumnStyle = null!;
    private MacroSession? _displayedSession;
    private Guid? _displayedSessionId;
    private int _displayedEventCount;
    private int _displayedElapsedMs;
    private EventListTypeFilterKind _selectedTypeFilterKind = EventListTypeFilterKind.All;
    private EventListSmartFilterKind _selectedSmartFilterKind = EventListSmartFilterKind.All;

    public event EventHandler<EventEditRequestedEventArgs>? EventEditRequested;

    public EventListControl()
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

        _eventGridView = new DataGridView();
        _eventScrollBar = new ModernScrollBar();
        _gridHostPanel = new RoundedGridHostPanel();
        _eventContextMenu = BuildEventContextMenu();
        _emptyStateLabel = new Label();
        _headerTitleLabel = CreateHeaderTitleLabel();
        _filterSearchTextBox = CreateFilterSearchTextBox();
        _typeFilterSelect = new ModernSelect();
        _smartFilterSelect = new ModernSelect();

        BuildLayout();
        ConfigureGrid();
        ApplyTheme();
        SetSession(null);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _eventContextMenu.Dispose();
        }

        base.Dispose(disposing);
    }

    public void SetSession(MacroSession? session, bool forceReload = false)
    {
        if (session is null)
        {
            ResetRowsForSession(null);
            UpdateVisibleState();
            UpdateEventScrollBar();
            return;
        }

        _displayedSession = session;

        bool canAppend = !forceReload
            && _displayedSessionId == session.Id
            && session.Events.Count >= _displayedEventCount;

        if (!canAppend)
        {
            ResetRowsForSession(session);
        }

        AppendRows(session, _displayedEventCount);
        UpdateVisibleState();
        SelectLastVisibleRow();
        UpdateEventScrollBar();
    }

    public bool TrySelectSourceEvent(int sourceIndex)
    {
        if (sourceIndex < 0 || _eventGridView.Rows.Count == 0)
        {
            return false;
        }

        bool selected = SelectSourceRow(sourceIndex);

        if (selected)
        {
            UpdateEventScrollBar();
        }

        return selected;
    }

    internal void ApplyShellLayoutProfile(AppShellLayoutProfile profile)
    {
        AppShellMainMetrics main = profile.Main;

        SuspendLayout();
        try
        {
            _headerRowStyle.Height = main.EventHeaderHeight;
            _toolbarPanel.Padding = main.EventHeaderPadding;
            _searchColumnStyle.Width = main.EventSearchWidth;
            _typeGapColumnStyle.Width = main.EventHeaderGap;
            _typeFilterColumnStyle.Width = main.EventTypeFilterWidth;
            _smartGapColumnStyle.Width = main.EventHeaderGap;
            _smartFilterColumnStyle.Width = main.EventSmartFilterWidth;
            _searchPanel.Padding = main.EventSearchPadding;
            _headerTitleLabel.Padding = new Padding(2, 0, main.EventHeaderGap, 2);

            int minimumControlHeight = Math.Max(0, main.EventHeaderHeight - main.EventHeaderPadding.Vertical);
            _filterSearchTextBox.MinimumSize = new Size(0, minimumControlHeight);
            _typeFilterSelect.MinimumSize = new Size(0, minimumControlHeight);
            _smartFilterSelect.MinimumSize = new Size(0, minimumControlHeight);
        }
        finally
        {
            ResumeLayout(performLayout: true);
        }

        LayoutGridViewport();
    }

    private void ResetRowsForSession(MacroSession? session)
    {
        _eventGridView.Rows.Clear();
        _displayedSession = session;
        _displayedSessionId = session?.Id;
        _displayedEventCount = 0;
        _displayedElapsedMs = 0;
    }

    private void RebuildRowsForCurrentSession(bool preserveSelection)
    {
        MacroSession? session = _displayedSession;
        int? selectedSourceIndex = preserveSelection
            ? GetSelectedSourceIndex()
            : null;

        if (session is null)
        {
            ResetRowsForSession(null);
            UpdateVisibleState();
            UpdateEventScrollBar();
            return;
        }

        ResetRowsForSession(session);
        AppendRows(session, 0);
        UpdateVisibleState();
        SelectSourceRowOrFallback(selectedSourceIndex);
        UpdateEventScrollBar();
    }

    private void AppendRows(MacroSession session, int startIndex)
    {
        if (startIndex >= session.Events.Count)
        {
            return;
        }

        EventListFilterCriteria criteria = BuildFilterCriteria();
        _eventGridView.SuspendLayout();
        try
        {
            for (int index = startIndex; index < session.Events.Count; index++)
            {
                MacroEvent macroEvent = session.Events[index];
                _displayedElapsedMs += Math.Max(0, macroEvent.DelayMs);

                if (!EventListFilterEngine.Matches(session.Events, index, _displayedElapsedMs, criteria))
                {
                    continue;
                }

                AddEventRow(new EventListViewItem(index, macroEvent, _displayedElapsedMs));
            }
        }
        finally
        {
            _displayedEventCount = session.Events.Count;
            _eventGridView.ResumeLayout();
        }
    }

    private void AddEventRow(EventListViewItem viewItem)
    {
        MacroEvent macroEvent = viewItem.Event;
        int rowIndex = _eventGridView.Rows.Add(
            (viewItem.SourceIndex + 1).ToString("000", CultureInfo.InvariantCulture),
            FormatElapsedTime(viewItem.ElapsedMs),
            FormatEventType(macroEvent.EventType),
            FormatAction(macroEvent),
            FormatPosition(macroEvent),
            FormattableString.Invariant($"{macroEvent.DelayMs} ms"),
            FormatDetail(macroEvent));
        EventListRowInsight insight = _displayedSession is null
            ? EventListRowInsight.None
            : EventListFilterEngine.GetInsight(_displayedSession.Events, viewItem.SourceIndex);
        DataGridViewRow row = _eventGridView.Rows[rowIndex];
        row.Tag = new EventRowTag(viewItem.SourceIndex, insight);
        ApplyInsightStyle(row, insight);
    }

    private void BuildLayout()
    {
        _rootLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = DesignTokens.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _rootLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _headerRowStyle = new RowStyle(SizeType.Absolute, DesignTokens.Scale(42));
        _rootLayoutPanel.RowStyles.Add(_headerRowStyle);
        _rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _gridHostPanel.Dock = DockStyle.Fill;
        _gridHostPanel.BackColor = DesignTokens.SurfaceInset;
        _gridHostPanel.Margin = Padding.Empty;
        _gridHostPanel.Padding = Padding.Empty;
        _gridHostPanel.Resize += (_, _) => LayoutGridViewport();

        _eventGridView.Dock = DockStyle.None;
        _emptyStateLabel.Dock = DockStyle.None;
        _emptyStateLabel.Text = "Seçili bir oturum yok. Yeni bir makro kaydedin veya diskten yükleyin.";
        _emptyStateLabel.TextAlign = ContentAlignment.MiddleCenter;

        _eventScrollBar.Dock = DockStyle.None;
        _eventScrollBar.BackColor = DesignTokens.SurfaceInset;
        _eventScrollBar.ValueChanged += (_, _) => ScrollGridTo(_eventScrollBar.Value);

        _gridHostPanel.Controls.Add(_eventGridView);
        _gridHostPanel.Controls.Add(_eventScrollBar);
        _gridHostPanel.Controls.Add(_emptyStateLabel);
        _rootLayoutPanel.Controls.Add(CreateHeaderToolbar(), 0, 0);
        _rootLayoutPanel.Controls.Add(_gridHostPanel, 0, 1);

        Controls.Add(_rootLayoutPanel);
    }

    private TableLayoutPanel CreateHeaderToolbar()
    {
        _toolbarPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = DesignTokens.Surface,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 0, DesignTokens.Scale(8))
        };
        _toolbarPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _searchColumnStyle = new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(220));
        _typeGapColumnStyle = new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(10));
        _typeFilterColumnStyle = new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(118));
        _smartGapColumnStyle = new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(10));
        _smartFilterColumnStyle = new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(172));
        _toolbarPanel.ColumnStyles.Add(_searchColumnStyle);
        _toolbarPanel.ColumnStyles.Add(_typeGapColumnStyle);
        _toolbarPanel.ColumnStyles.Add(_typeFilterColumnStyle);
        _toolbarPanel.ColumnStyles.Add(_smartGapColumnStyle);
        _toolbarPanel.ColumnStyles.Add(_smartFilterColumnStyle);
        _toolbarPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _searchPanel = new RoundedInputHostPanel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.SurfaceInset,
            BorderColor = DesignTokens.BorderSoft,
            Margin = Padding.Empty,
            Padding = new Padding(
                DesignTokens.Scale(12),
                DesignTokens.Scale(6),
                DesignTokens.Scale(10),
                DesignTokens.Scale(5))
        };
        _searchPanel.Click += (_, _) => _filterSearchTextBox.Focus();
        _searchPanel.Controls.Add(_filterSearchTextBox);

        _typeFilterSelect.Dock = DockStyle.Fill;
        _typeFilterSelect.Margin = Padding.Empty;
        _typeFilterSelect.ShowSelectedItemIndicator = true;
        _typeFilterSelect.SetItems(
            TypeFilterOptions.Select(option => option.SelectedLabel),
            TypeFilterOptions.Select(option => option.MenuLabel));
        _typeFilterSelect.SelectedIndexChanged += (_, _) => UpdateFiltersFromControls();

        _smartFilterSelect.Dock = DockStyle.Fill;
        _smartFilterSelect.Margin = Padding.Empty;
        _smartFilterSelect.ShowSelectedItemIndicator = true;
        _smartFilterSelect.SetItems(
            SmartFilterOptions.Select(option => option.SelectedLabel),
            SmartFilterOptions.Select(option => option.MenuLabel));
        _smartFilterSelect.SelectedIndexChanged += (_, _) => UpdateFiltersFromControls();

        _toolbarPanel.Controls.Add(_headerTitleLabel, 0, 0);
        _toolbarPanel.Controls.Add(_searchPanel, 1, 0);
        _toolbarPanel.Controls.Add(_typeFilterSelect, 3, 0);
        _toolbarPanel.Controls.Add(_smartFilterSelect, 5, 0);

        return _toolbarPanel;
    }

    private static Label CreateHeaderTitleLabel()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = "Olay Önizleme",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            BackColor = DesignTokens.Surface,
            ForeColor = DesignTokens.TextPrimary,
            Font = DesignTokens.FontUiBold,
            Padding = new Padding(2, 0, DesignTokens.Scale(12), 2)
        };
    }

    private void LayoutGridViewport()
    {
        int inset = DesignTokens.Scale(1);
        int scrollWidth = _eventScrollBar.Visible ? DesignTokens.Scale(14) : 0;
        int scrollGap = _eventScrollBar.Visible ? DesignTokens.Scale(6) : 0;
        int contentWidth = Math.Max(0, _gridHostPanel.ClientSize.Width - (inset * 2));
        int gridWidth = Math.Max(0, contentWidth - scrollWidth - scrollGap);
        int gridHeight = Math.Max(0, _gridHostPanel.ClientSize.Height - (inset * 2));

        _eventGridView.Bounds = new Rectangle(inset, inset, gridWidth, gridHeight);
        _emptyStateLabel.Bounds = new Rectangle(inset, inset, contentWidth, gridHeight);
        _eventScrollBar.Bounds = new Rectangle(
            inset + gridWidth + scrollGap,
            inset,
            scrollWidth,
            gridHeight);
        ApplyColumnWidths();
    }

    private void ConfigureGrid()
    {
        _eventGridView.AllowUserToAddRows = false;
        _eventGridView.AllowUserToDeleteRows = false;
        _eventGridView.AllowUserToResizeRows = false;
        _eventGridView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _eventGridView.BackgroundColor = DesignTokens.SurfaceInset;
        _eventGridView.BorderStyle = BorderStyle.None;
        _eventGridView.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        _eventGridView.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        _eventGridView.ColumnHeadersHeight = DesignTokens.Scale(42);
        _eventGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _eventGridView.EnableHeadersVisualStyles = false;
        _eventGridView.GridColor = DesignTokens.BorderSoft;
        _eventGridView.MultiSelect = false;
        _eventGridView.ReadOnly = true;
        _eventGridView.RowHeadersVisible = false;
        _eventGridView.RowTemplate.Height = DesignTokens.Scale(38);
        _eventGridView.ScrollBars = ScrollBars.None;
        _eventGridView.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _eventGridView.Resize += (_, _) => ApplyColumnWidths();
        _eventGridView.Resize += (_, _) => UpdateEventScrollBar();
        _eventGridView.Scroll += (_, _) => SyncEventScrollBarFromGrid();
        _eventGridView.MouseWheel += EventGridView_MouseWheel;
        _eventGridView.CellMouseDown += EventGridView_CellMouseDown;
        _eventGridView.CellPainting += EventGridView_CellPainting;

        _eventGridView.Columns.Add(CreateTextColumn("#", "#", 58, 6));
        _eventGridView.Columns.Add(CreateTextColumn("Time", "Zaman", 126, 15));
        _eventGridView.Columns.Add(CreateTextColumn("Type", "Tür", 78, 10));
        _eventGridView.Columns.Add(CreateTextColumn("Action", "Aksiyon", 92, 15));
        _eventGridView.Columns.Add(CreateTextColumn("Position", "Konum", 138, 22));
        _eventGridView.Columns.Add(CreateTextColumn("Delay", "Gecikme", 86, 11));
        _eventGridView.Columns.Add(CreateTextColumn("Detail", "Detay", 132, 21));
        ApplyColumnWidths();
    }

    private TextBox CreateFilterSearchTextBox()
    {
        var textBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill,
            PlaceholderText = "Olay ara...",
            BackColor = DesignTokens.SurfaceInset,
            ForeColor = DesignTokens.TextPrimary,
            Font = DesignTokens.FontUiNormal,
            Margin = Padding.Empty
        };

        textBox.TextChanged += (_, _) => UpdateFiltersFromControls();
        return textBox;
    }

    private void UpdateFiltersFromControls()
    {
        _selectedTypeFilterKind = GetSelectedTypeFilter();
        _selectedSmartFilterKind = GetSelectedSmartFilter();
        RebuildRowsForCurrentSession(preserveSelection: true);
    }

    private EventListFilterCriteria BuildFilterCriteria()
    {
        return new EventListFilterCriteria(
            _filterSearchTextBox.Text,
            _selectedTypeFilterKind,
            _selectedSmartFilterKind);
    }

    private bool HasActiveFilters()
    {
        return !string.IsNullOrWhiteSpace(_filterSearchTextBox.Text)
            || _selectedTypeFilterKind != EventListTypeFilterKind.All
            || _selectedSmartFilterKind != EventListSmartFilterKind.All;
    }

    private EventListTypeFilterKind GetSelectedTypeFilter()
    {
        int selectedIndex = _typeFilterSelect.SelectedIndex;
        return selectedIndex >= 0 && selectedIndex < TypeFilterOptions.Length
            ? TypeFilterOptions[selectedIndex].Kind
            : EventListTypeFilterKind.All;
    }

    private EventListSmartFilterKind GetSelectedSmartFilter()
    {
        int selectedIndex = _smartFilterSelect.SelectedIndex;
        return selectedIndex >= 0 && selectedIndex < SmartFilterOptions.Length
            ? SmartFilterOptions[selectedIndex].Kind
            : EventListSmartFilterKind.All;
    }

    private void UpdateVisibleState()
    {
        int totalEventCount = _displayedSession?.Events.Count ?? 0;
        int visibleEventCount = _eventGridView.Rows.Count;
        bool hasVisibleRows = visibleEventCount > 0;

        _eventGridView.Visible = hasVisibleRows;
        _emptyStateLabel.Visible = !hasVisibleRows;

        if (!hasVisibleRows)
        {
            _emptyStateLabel.Text = ResolveEmptyStateText(totalEventCount);
        }

    }

    private string ResolveEmptyStateText(int totalEventCount)
    {
        if (_displayedSession is null)
        {
            return "Seçili bir oturum yok. Yeni bir makro kaydedin veya diskten yükleyin.";
        }

        if (totalEventCount == 0)
        {
            return "Seçili oturumda olay yok.";
        }

        return HasActiveFilters()
            ? "Filtreyle eşleşen olay bulunamadı."
            : "Seçili oturumda gösterilecek olay yok.";
    }

    private void SelectSourceRowOrFallback(int? sourceIndex)
    {
        if (sourceIndex.HasValue && SelectSourceRow(sourceIndex.Value))
        {
            return;
        }

        SelectLastVisibleRow();
    }

    private bool SelectSourceRow(int sourceIndex)
    {
        for (int rowIndex = 0; rowIndex < _eventGridView.Rows.Count; rowIndex++)
        {
            if (GetSourceIndexForRow(rowIndex) != sourceIndex)
            {
                continue;
            }

            SelectRow(rowIndex);
            return true;
        }

        return false;
    }

    private void SelectLastVisibleRow()
    {
        int rowIndex = _eventGridView.Rows.Count - 1;

        if (rowIndex < 0)
        {
            return;
        }

        SelectRow(rowIndex);
    }

    private void SelectRow(int rowIndex)
    {
        _eventGridView.ClearSelection();
        _eventGridView.Rows[rowIndex].Selected = true;

        try
        {
            _eventGridView.FirstDisplayedScrollingRowIndex = Math.Max(0, rowIndex);
        }
        catch (InvalidOperationException)
        {
            // DataGridView can reject the scroll while it is rebuilding rows.
        }
    }

    private int? GetSelectedSourceIndex()
    {
        if (_eventGridView.SelectedRows.Count == 0)
        {
            return null;
        }

        return GetSourceIndexForRow(_eventGridView.SelectedRows[0].Index);
    }

    private int? GetSourceIndexForRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _eventGridView.Rows.Count)
        {
            return null;
        }

        object? tag = _eventGridView.Rows[rowIndex].Tag;
        return tag switch
        {
            EventRowTag rowTag => rowTag.SourceIndex,
            int sourceIndex => sourceIndex,
            _ => null
        };
    }

    private static EventListRowInsight GetInsightForRow(DataGridViewRow row)
    {
        return row.Tag is EventRowTag rowTag
            ? rowTag.Insight
            : EventListRowInsight.None;
    }

    private void EventGridView_MouseWheel(object? sender, MouseEventArgs e)
    {
        _ = sender;

        int wheelStep = Math.Max(1, SystemInformation.MouseWheelScrollLines);
        _eventScrollBar.Value -= Math.Sign(e.Delta) * wheelStep;
    }

    private ContextMenuStrip BuildEventContextMenu()
    {
        var contextMenu = new ContextMenuStrip
        {
            AutoClose = true,
            DropShadowEnabled = false,
            MinimumSize = new Size(DesignTokens.Scale(138), 0),
            ShowCheckMargin = false,
            ShowImageMargin = false
        };
        AppToolStripRenderer.ApplyTo(
            contextMenu,
            AppToolStripMenuDensity.Comfortable);

        var editItem = new ToolStripMenuItem("Düzenle")
        {
            AccessibleName = "Seçili olayı düzenle"
        };
        editItem.Click += (_, _) =>
        {
            if (_eventContextMenu.Tag is int rowIndex)
            {
                RequestEditForRow(rowIndex);
            }
        };
        contextMenu.Items.Add(editItem);

        return contextMenu;
    }

    private void EventGridView_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        _ = sender;

        if (e.Button != MouseButtons.Right || e.RowIndex < 0 || _displayedSession is null)
        {
            return;
        }

        _eventGridView.ClearSelection();
        _eventGridView.Rows[e.RowIndex].Selected = true;
        _eventGridView.CurrentCell = _eventGridView.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
        _eventContextMenu.Tag = e.RowIndex;
        _eventContextMenu.Show(_eventGridView, ResolveContextMenuLocation(e));
    }

    private Point ResolveContextMenuLocation(DataGridViewCellMouseEventArgs e)
    {
        int columnIndex = Math.Max(0, e.ColumnIndex);

        if (columnIndex < _eventGridView.Columns.Count && e.RowIndex < _eventGridView.Rows.Count)
        {
            Rectangle cellBounds = _eventGridView.GetCellDisplayRectangle(
                columnIndex,
                e.RowIndex,
                cutOverflow: false);

            if (!cellBounds.IsEmpty)
            {
                return new Point(
                    cellBounds.Left + Math.Max(0, e.X),
                    cellBounds.Top + Math.Max(0, e.Y));
            }
        }

        return _eventGridView.PointToClient(Control.MousePosition);
    }

    private void EventGridView_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        _ = sender;

        if (e.RowIndex < 0)
        {
            return;
        }

        if (e.ColumnIndex == _eventGridView.Columns["#"].Index)
        {
            PaintIndexCell(e);
            return;
        }

        if (e.ColumnIndex != _eventGridView.Columns["Type"].Index)
        {
            return;
        }

        string eventType = e.Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return;
        }

        Graphics? graphics = e.Graphics;
        Font font = e.CellStyle?.Font ?? _eventGridView.Font;
        if (graphics is null)
        {
            return;
        }

        e.Paint(
            e.CellBounds,
            DataGridViewPaintParts.Background |
            DataGridViewPaintParts.Border |
            DataGridViewPaintParts.SelectionBackground);

        Color accentColor = eventType switch
        {
            "Fare" => Color.FromArgb(79, 158, 255),
            "Klavye" => Color.FromArgb(123, 95, 255),
            "Sistem" => Color.FromArgb(52, 199, 89),
            _ => DesignTokens.TextSecondary
        };
        Color pillBackColor = Color.FromArgb(
            _eventGridView.Rows[e.RowIndex].Selected ? 44 : 26,
            accentColor);

        int horizontalPadding = DesignTokens.Scale(8);
        int verticalPadding = DesignTokens.Scale(7);
        Size textSize = TextRenderer.MeasureText(eventType, font);
        int pillWidth = Math.Min(
            e.CellBounds.Width - horizontalPadding * 2,
            Math.Max(DesignTokens.Scale(54), textSize.Width + DesignTokens.Scale(18)));
        int pillHeight = Math.Min(
            e.CellBounds.Height - verticalPadding * 2,
            Math.Max(DesignTokens.Scale(22), textSize.Height + DesignTokens.Scale(4)));

        var pillBounds = new Rectangle(
            e.CellBounds.Left + (e.CellBounds.Width - pillWidth) / 2,
            e.CellBounds.Top + (e.CellBounds.Height - pillHeight) / 2,
            Math.Max(1, pillWidth),
            Math.Max(1, pillHeight));

        using GraphicsPath pillPath = CreateRoundedRectanglePath(pillBounds, DesignTokens.Scale(10));
        using var backgroundBrush = new SolidBrush(pillBackColor);
        using var borderPen = new Pen(Color.FromArgb(120, accentColor));
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.FillPath(backgroundBrush, pillPath);
        graphics.DrawPath(borderPen, pillPath);

        TextRenderer.DrawText(
            graphics,
            eventType,
            font,
            pillBounds,
            accentColor,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);

        e.Handled = true;
    }

    private void PaintIndexCell(DataGridViewCellPaintingEventArgs e)
    {
        DataGridViewRow row = _eventGridView.Rows[e.RowIndex];
        EventListRowInsight insight = GetInsightForRow(row);

        if (insight == EventListRowInsight.None)
        {
            return;
        }

        Graphics? graphics = e.Graphics;
        if (graphics is null)
        {
            return;
        }

        e.Paint(
            e.CellBounds,
            DataGridViewPaintParts.Background |
            DataGridViewPaintParts.Border |
            DataGridViewPaintParts.SelectionBackground);

        Color accentColor = GetInsightAccentColor(insight);
        Rectangle accentBounds = new(
            e.CellBounds.Left + DesignTokens.Scale(5),
            e.CellBounds.Top + DesignTokens.Scale(8),
            Math.Max(DesignTokens.Scale(3), 2),
            Math.Max(DesignTokens.Scale(18), e.CellBounds.Height - DesignTokens.Scale(16)));

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using GraphicsPath accentPath = CreateRoundedRectanglePath(accentBounds, DesignTokens.Scale(2));
        using var accentBrush = new SolidBrush(accentColor);
        graphics.FillPath(accentBrush, accentPath);

        Rectangle textBounds = new(
            e.CellBounds.Left + DesignTokens.Scale(14),
            e.CellBounds.Top,
            Math.Max(0, e.CellBounds.Width - DesignTokens.Scale(16)),
            e.CellBounds.Height);

        TextRenderer.DrawText(
            graphics,
            e.Value?.ToString() ?? string.Empty,
            e.CellStyle?.Font ?? _eventGridView.Font,
            textBounds,
            e.CellStyle?.ForeColor ?? DesignTokens.TextPrimary,
            TextFormatFlags.Left |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);

        e.Handled = true;
    }

    private void RequestEditForRow(int rowIndex)
    {
        if (_displayedSession is null
            || rowIndex < 0
            || rowIndex >= _eventGridView.Rows.Count
            || GetSourceIndexForRow(rowIndex) is not int eventIndex
            || eventIndex < 0
            || eventIndex >= _displayedSession.Events.Count)
        {
            return;
        }

        EventEditRequested?.Invoke(
            this,
            new EventEditRequestedEventArgs(eventIndex, _displayedSession.Events[eventIndex]));
    }

    private static void ApplyInsightStyle(
        DataGridViewRow row,
        EventListRowInsight insight)
    {
        if (insight == EventListRowInsight.None)
        {
            return;
        }

        Color accentColor = GetInsightAccentColor(insight);
        row.DefaultCellStyle.BackColor = GetInsightBackColor(insight);
        row.DefaultCellStyle.SelectionBackColor = GetInsightSelectionBackColor(insight);
        row.DefaultCellStyle.ForeColor = DesignTokens.TextPrimary;
        row.DefaultCellStyle.SelectionForeColor = DesignTokens.TextPrimary;

        if (row.DataGridView?.Columns["Delay"] is DataGridViewColumn delayColumn)
        {
            row.Cells[delayColumn.Index].Style.ForeColor = accentColor;
            row.Cells[delayColumn.Index].Style.SelectionForeColor = DesignTokens.TextPrimary;
        }

        if (row.DataGridView?.Columns["Detail"] is DataGridViewColumn detailColumn
            && insight == EventListRowInsight.InvalidOrIncomplete)
        {
            row.Cells[detailColumn.Index].Style.ForeColor = accentColor;
            row.Cells[detailColumn.Index].Style.SelectionForeColor = DesignTokens.TextPrimary;
        }
    }

    private static Color GetInsightAccentColor(EventListRowInsight insight)
    {
        return insight switch
        {
            EventListRowInsight.LongDelay => DesignTokens.AccentOrange,
            EventListRowInsight.OptimizationCandidate => DesignTokens.Accent,
            EventListRowInsight.InvalidOrIncomplete => DesignTokens.AccentRed,
            _ => DesignTokens.TextSecondary
        };
    }

    private static Color GetInsightBackColor(EventListRowInsight insight)
    {
        return insight switch
        {
            EventListRowInsight.LongDelay => Color.FromArgb(24, 20, 14),
            EventListRowInsight.OptimizationCandidate => Color.FromArgb(11, 20, 32),
            EventListRowInsight.InvalidOrIncomplete => Color.FromArgb(28, 17, 24),
            _ => DesignTokens.SurfaceInset
        };
    }

    private static Color GetInsightSelectionBackColor(EventListRowInsight insight)
    {
        return insight switch
        {
            EventListRowInsight.LongDelay => Color.FromArgb(98, 70, 24),
            EventListRowInsight.OptimizationCandidate => Color.FromArgb(24, 93, 188),
            EventListRowInsight.InvalidOrIncomplete => Color.FromArgb(118, 38, 55),
            _ => Color.FromArgb(24, 93, 188)
        };
    }

    private void ApplyTheme()
    {
        _emptyStateLabel.BackColor = DesignTokens.SurfaceInset;
        _emptyStateLabel.ForeColor = DesignTokens.TextSecondary;
        _emptyStateLabel.Font = DesignTokens.FontUiNormal;

        _filterSearchTextBox.BackColor = DesignTokens.SurfaceInset;
        _filterSearchTextBox.ForeColor = DesignTokens.TextPrimary;
        _filterSearchTextBox.Font = DesignTokens.FontUiNormal;

        _eventGridView.DefaultCellStyle.BackColor = DesignTokens.SurfaceInset;
        _eventGridView.DefaultCellStyle.ForeColor = DesignTokens.TextPrimary;
        _eventGridView.DefaultCellStyle.Font = DesignTokens.FontUiNormal;
        _eventGridView.DefaultCellStyle.SelectionBackColor = Color.FromArgb(24, 93, 188);
        _eventGridView.DefaultCellStyle.SelectionForeColor = DesignTokens.TextPrimary;
        _eventGridView.DefaultCellStyle.Padding = new Padding(DesignTokens.Scale(6), 0, DesignTokens.Scale(6), 0);

        _eventGridView.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(12, 17, 27);
        _eventGridView.AlternatingRowsDefaultCellStyle.ForeColor = DesignTokens.TextPrimary;

        _eventGridView.ColumnHeadersDefaultCellStyle.BackColor = DesignTokens.Surface2;
        _eventGridView.ColumnHeadersDefaultCellStyle.ForeColor = DesignTokens.TextPrimary;
        _eventGridView.ColumnHeadersDefaultCellStyle.Font = DesignTokens.FontUiBold;
        _eventGridView.ColumnHeadersDefaultCellStyle.SelectionBackColor = DesignTokens.Surface2;
        _eventGridView.ColumnHeadersDefaultCellStyle.SelectionForeColor = DesignTokens.TextPrimary;
        _eventGridView.ColumnHeadersDefaultCellStyle.Padding = new Padding(
            DesignTokens.Scale(8),
            0,
            DesignTokens.Scale(8),
            0);
    }

    private static DataGridViewTextBoxColumn CreateTextColumn(
        string name,
        string headerText,
        int minimumWidth,
        int fillWeight)
    {
        int scaledMinimumWidth = DesignTokens.Scale(minimumWidth);
        int scaledWidth = DesignTokens.Scale(Math.Max(minimumWidth, fillWeight * 10));

        return new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = headerText,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            MinimumWidth = scaledMinimumWidth,
            Width = scaledWidth,
            FillWeight = fillWeight,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
    }

    private void ApplyColumnWidths()
    {
        if (_eventGridView.Columns.Count == 0)
        {
            return;
        }

        int availableWidth = _eventGridView.ClientSize.Width;

        if (availableWidth <= 0)
        {
            return;
        }

        float totalWeight = 0f;

        foreach (DataGridViewColumn column in _eventGridView.Columns)
        {
            totalWeight += Math.Max(1f, column.FillWeight);
        }

        int assignedWidth = 0;

        for (int index = 0; index < _eventGridView.Columns.Count; index++)
        {
            DataGridViewColumn column = _eventGridView.Columns[index];
            bool isLastColumn = index == _eventGridView.Columns.Count - 1;
            int targetWidth = isLastColumn
                ? availableWidth - assignedWidth
                : (int)Math.Round(availableWidth * Math.Max(1f, column.FillWeight) / totalWeight);

            column.Width = Math.Max(column.MinimumWidth, targetWidth);
            assignedWidth += column.Width;
        }
    }

    private void UpdateEventScrollBar()
    {
        if (_eventGridView.Rows.Count == 0 || !_eventGridView.Visible)
        {
            _eventScrollBar.SetRange(0, 1, 0);
            _eventScrollBar.Visible = false;
            LayoutGridViewport();
            return;
        }

        int visibleRows = Math.Max(1, _eventGridView.DisplayedRowCount(false));
        int maximum = Math.Max(0, _eventGridView.Rows.Count - visibleRows);
        int currentIndex = GetFirstDisplayedRowIndex();

        _eventScrollBar.SetRange(maximum, visibleRows, Math.Min(currentIndex, maximum));
        _eventScrollBar.Visible = maximum > 0;
        LayoutGridViewport();
        if (_eventScrollBar.Visible)
        {
            _eventScrollBar.BringToFront();
        }
    }

    private void SyncEventScrollBarFromGrid()
    {
        if (_eventGridView.Rows.Count == 0)
        {
            _eventScrollBar.SetValueSilently(0);
            return;
        }

        _eventScrollBar.SetValueSilently(GetFirstDisplayedRowIndex());
    }

    private void ScrollGridTo(int rowIndex)
    {
        if (_eventGridView.Rows.Count == 0)
        {
            return;
        }

        int targetRowIndex = Math.Clamp(rowIndex, 0, _eventGridView.Rows.Count - 1);
        try
        {
            _eventGridView.FirstDisplayedScrollingRowIndex = targetRowIndex;
        }
        catch (InvalidOperationException)
        {
            // DataGridView can reject the scroll while it is rebuilding rows.
        }
    }

    private int GetFirstDisplayedRowIndex()
    {
        try
        {
            return _eventGridView.FirstDisplayedScrollingRowIndex < 0
                ? 0
                : _eventGridView.FirstDisplayedScrollingRowIndex;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    private sealed record TypeFilterOption(
        EventListTypeFilterKind Kind,
        string SelectedLabel,
        string MenuLabel);

    private sealed record SmartFilterOption(
        EventListSmartFilterKind Kind,
        string SelectedLabel,
        string MenuLabel);

    private sealed record EventRowTag(
        int SourceIndex,
        EventListRowInsight Insight);

    private sealed class RoundedInputHostPanel : Panel
    {
        public Color BorderColor { get; set; } = DesignTokens.BorderSoft;

        public RoundedInputHostPanel()
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

            Rectangle bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using GraphicsPath path = CreateRoundedRectanglePath(bounds, DesignTokens.Scale(7));
            using var fillBrush = new SolidBrush(BackColor);
            using var borderPen = new Pen(BorderColor, Math.Max(1f, DesignTokens.DensityScale));
            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);
        }
    }

    private sealed class RoundedGridHostPanel : Panel
    {
        private Size _lastRegionSize;
        private int _lastRegionRadius = -1;

        public RoundedGridHostPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? DesignTokens.Surface);
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
            using GraphicsPath path = CreateRoundedRectanglePath(bounds, DesignTokens.Scale(8));
            using var fillBrush = new SolidBrush(DesignTokens.SurfaceInset);
            using var borderPen = new Pen(DesignTokens.BorderSoft);

            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);
            e.Graphics.SmoothingMode = SmoothingMode.None;
        }

        private void UpdateRegion()
        {
            Rectangle bounds = ClientRectangle;
            int radius = DesignTokens.Scale(8);

            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                Region? emptyRegion = Region;
                Region = null;
                emptyRegion?.Dispose();
                _lastRegionSize = Size.Empty;
                _lastRegionRadius = -1;
                return;
            }

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

    private static string FormatElapsedTime(int elapsedMs)
    {
        TimeSpan elapsed = TimeSpan.FromMilliseconds(elapsedMs);
        return FormattableString.Invariant(
            $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds:000}");
    }

    private static string FormatEventType(MacroEventType eventType)
    {
        return eventType switch
        {
            MacroEventType.Keyboard => "Klavye",
            MacroEventType.Mouse => "Fare",
            MacroEventType.System => "Sistem",
            _ => eventType.ToString()
        };
    }

    private static string FormatAction(MacroEvent macroEvent)
    {
        return macroEvent.EventType switch
        {
            MacroEventType.Keyboard => macroEvent.KeyboardActionType.ToString(),
            MacroEventType.Mouse => macroEvent.MouseActionType.ToString(),
            MacroEventType.System => "Sistem",
            _ => macroEvent.Description
        };
    }

    private static string FormatPosition(MacroEvent macroEvent)
    {
        if (macroEvent.X.HasValue && macroEvent.Y.HasValue)
        {
            return FormattableString.Invariant($"X: {macroEvent.X}, Y: {macroEvent.Y}");
        }

        return "-";
    }

    private static string FormatDetail(MacroEvent macroEvent)
    {
        return macroEvent.EventType switch
        {
            MacroEventType.Keyboard when !string.IsNullOrWhiteSpace(macroEvent.KeyName) => macroEvent.KeyName,
            MacroEventType.Mouse when macroEvent.MouseActionType == MouseActionType.Wheel => FormattableString.Invariant(
                $"{macroEvent.Description} ({macroEvent.WheelDelta ?? 0})"),
            _ => string.IsNullOrWhiteSpace(macroEvent.Description)
                ? "-"
                : macroEvent.Description
        };
    }
}

internal sealed class EventEditRequestedEventArgs : EventArgs
{
    public EventEditRequestedEventArgs(int eventIndex, MacroEvent macroEvent)
    {
        EventIndex = eventIndex;
        MacroEvent = macroEvent;
    }

    public int EventIndex { get; }

    public MacroEvent MacroEvent { get; }
}
