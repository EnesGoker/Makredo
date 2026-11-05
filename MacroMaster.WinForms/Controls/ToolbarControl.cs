using MacroMaster.WinForms.Theme;
using System.Drawing.Drawing2D;

namespace MacroMaster.WinForms.Controls;

public enum PlaybackButtonState
{
    Play,
    Pause,
    Resume
}

public readonly record struct ToolbarButtonState(
    bool CanRecord,
    bool CanStop,
    bool CanPlayback,
    bool CanSaveLibrary,
    bool CanSaveJson,
    bool CanSaveXml,
    bool CanLoadJson,
    bool CanLoadXml,
    bool CanEditHotkeys);

public partial class ToolbarControl : UserControl
{
    private readonly ContextMenuStrip _saveMenu = new();
    private readonly ContextMenuStrip _loadMenu = new();
    private readonly ToolStripMenuItem _saveLibraryMenuItem = new("Kütüphaneye Kaydet");
    private readonly ToolStripMenuItem _saveJsonMenuItem = new("JSON Kaydet");
    private readonly ToolStripMenuItem _saveXmlMenuItem = new("XML Kaydet");
    private readonly ToolStripMenuItem _saveHtmlReportMenuItem = new("HTML Rapor Oluştur");
    private readonly ToolStripMenuItem _saveTextReportMenuItem = new("TXT Rapor Oluştur");
    private readonly ToolStripMenuItem _loadJsonMenuItem = new("JSON Yükle");
    private readonly ToolStripMenuItem _loadXmlMenuItem = new("XML Yükle");
    private string? _recordHotkey;
    private string? _stopHotkey;
    private string? _playbackHotkey;
    private string? _hotkeysHotkey;
    private string _recordLabel = "Kaydı Başlat";
    private string _playbackLabel = "Oynat";

    public event EventHandler? RecordToggleClicked;
    public event EventHandler? StopClicked;
    public event EventHandler? PlaybackToggleClicked;
    public event EventHandler? SaveLibraryClicked;
    public event EventHandler? SaveJsonClicked;
    public event EventHandler? SaveXmlClicked;
    public event EventHandler? SaveHtmlReportClicked;
    public event EventHandler? SaveTextReportClicked;
    public event EventHandler? LoadJsonClicked;
    public event EventHandler? LoadXmlClicked;
    public event EventHandler? HotkeysClicked;

    public ToolbarControl()
    {
        InitializeComponent();
        loadButton.Text = "Yükle";
        ConfigureMenus();
        ApplyTheme();
        WireEvents();
        SetHotkeyHints(null, null, null, null);
    }

    public void UpdateRecordButton(bool isRecording)
    {
        _recordLabel = isRecording ? "Kaydı Durdur" : "Kaydı Başlat";
        SetButtonText(recordButton, _recordLabel, _recordHotkey);
        ApplyButtonAccent(recordButton, DesignTokens.AccentRed, DesignTokens.AccentRedSoft);
    }

    public void UpdatePlaybackButton(PlaybackButtonState state)
    {
        _playbackLabel = state switch
        {
            PlaybackButtonState.Pause => "Duraklat",
            PlaybackButtonState.Resume => "Devam Et",
            _ => "Oynat"
        };

        SetButtonText(playbackButton, _playbackLabel, _playbackHotkey);
        ApplyButtonAccent(playbackButton, DesignTokens.Accent, DesignTokens.AccentSoft);
    }

    public void SetButtonsEnabled(ToolbarButtonState state)
    {
        recordButton.Enabled = state.CanRecord;
        stopButton.Enabled = state.CanStop;
        playbackButton.Enabled = state.CanPlayback;
        saveButton.Enabled = state.CanSaveLibrary || state.CanSaveJson || state.CanSaveXml;
        loadButton.Enabled = state.CanLoadJson || state.CanLoadXml;
        hotkeysButton.Enabled = state.CanEditHotkeys;

        _saveLibraryMenuItem.Enabled = state.CanSaveLibrary;
        _saveJsonMenuItem.Enabled = state.CanSaveJson;
        _saveXmlMenuItem.Enabled = state.CanSaveXml;
        _saveHtmlReportMenuItem.Enabled = state.CanSaveLibrary || state.CanSaveJson || state.CanSaveXml;
        _saveTextReportMenuItem.Enabled = state.CanSaveLibrary || state.CanSaveJson || state.CanSaveXml;
        _loadJsonMenuItem.Enabled = state.CanLoadJson;
        _loadXmlMenuItem.Enabled = state.CanLoadXml;
    }

    public void SetHotkeyHints(string? record, string? stop, string? playback, string? hotkeys)
    {
        _recordHotkey = record;
        _stopHotkey = stop;
        _playbackHotkey = playback;
        _hotkeysHotkey = hotkeys;

        SetButtonText(recordButton, _recordLabel, _recordHotkey);
        SetButtonText(stopButton, "Durdur", _stopHotkey);
        SetButtonText(playbackButton, _playbackLabel, _playbackHotkey);
        SetButtonText(hotkeysButton, "Kısayollar", _hotkeysHotkey);
    }

    internal void ApplyShellLayoutProfile(AppShellLayoutProfile profile)
    {
        MinimumSize = new Size(0, profile.Chrome.ToolbarControlMinimumHeight);
        toolbarLayoutPanel.Padding = profile.Chrome.ToolbarControlPadding;

        foreach (Control control in toolbarLayoutPanel.Controls)
        {
            if (control is ToolbarButton button)
            {
                button.Margin = profile.Chrome.ToolbarButtonMargin;
                button.Invalidate();
            }
        }

        PerformLayout();
    }

    private void WireEvents()
    {
        recordButton.Click += (_, _) => RecordToggleClicked?.Invoke(this, EventArgs.Empty);
        stopButton.Click += (_, _) => StopClicked?.Invoke(this, EventArgs.Empty);
        playbackButton.Click += (_, _) => PlaybackToggleClicked?.Invoke(this, EventArgs.Empty);
        saveButton.Click += (_, _) => ShowMenu(saveButton, _saveMenu);
        loadButton.Click += (_, _) => ShowMenu(loadButton, _loadMenu);
        hotkeysButton.Click += (_, _) => HotkeysClicked?.Invoke(this, EventArgs.Empty);

        _saveLibraryMenuItem.Click += (_, _) => SaveLibraryClicked?.Invoke(this, EventArgs.Empty);
        _saveJsonMenuItem.Click += (_, _) => SaveJsonClicked?.Invoke(this, EventArgs.Empty);
        _saveXmlMenuItem.Click += (_, _) => SaveXmlClicked?.Invoke(this, EventArgs.Empty);
        _saveHtmlReportMenuItem.Click += (_, _) => SaveHtmlReportClicked?.Invoke(this, EventArgs.Empty);
        _saveTextReportMenuItem.Click += (_, _) => SaveTextReportClicked?.Invoke(this, EventArgs.Empty);
        _loadJsonMenuItem.Click += (_, _) => LoadJsonClicked?.Invoke(this, EventArgs.Empty);
        _loadXmlMenuItem.Click += (_, _) => LoadXmlClicked?.Invoke(this, EventArgs.Empty);
    }

    private void ConfigureMenus()
    {
        components ??= new System.ComponentModel.Container();
        components.Add(_saveMenu);
        components.Add(_loadMenu);

        ConfigureMenu(_saveMenu);
        ConfigureMenu(_loadMenu);

        _saveMenu.Items.Add(_saveLibraryMenuItem);
        _saveMenu.Items.Add(new ToolStripSeparator());
        _saveMenu.Items.Add(_saveJsonMenuItem);
        _saveMenu.Items.Add(_saveXmlMenuItem);
        _saveMenu.Items.Add(new ToolStripSeparator());
        _saveMenu.Items.Add(_saveHtmlReportMenuItem);
        _saveMenu.Items.Add(_saveTextReportMenuItem);
        _loadMenu.Items.Add(_loadJsonMenuItem);
        _loadMenu.Items.Add(_loadXmlMenuItem);
    }

    private static void ConfigureMenu(ContextMenuStrip menu)
    {
        menu.ShowImageMargin = false;
        AppToolStripRenderer.ApplyTo(
            menu,
            AppToolStripMenuDensity.Comfortable);

        menu.Opening += (_, _) =>
        {
            foreach (ToolStripItem item in menu.Items)
            {
                item.ForeColor = item.Enabled
                    ? DesignTokens.TextPrimary
                    : DesignTokens.TextMuted;
            }
        };
    }

    private static void ShowMenu(Control owner, ContextMenuStrip menu)
    {
        menu.Show(owner, new Point(0, owner.Height + DesignTokens.Scale(4)));
    }

    private void ApplyTheme()
    {
        BackColor = DesignTokens.Surface;
        toolbarLayoutPanel.BackColor = DesignTokens.Surface;
        MinimumSize = new Size(0, DesignTokens.ToolbarHeight);
        MaximumSize = new Size(int.MaxValue, int.MaxValue);

        foreach (Control control in toolbarLayoutPanel.Controls)
        {
            if (control is ToolbarButton button)
            {
                ApplyToolbarButtonStyle(button);
            }
        }

        ApplyButtonAccent(recordButton, DesignTokens.AccentRed, DesignTokens.AccentRedSoft);
        ApplyButtonAccent(playbackButton, DesignTokens.Accent, DesignTokens.AccentSoft);
        ApplyButtonAccent(stopButton, DesignTokens.BorderBright, DesignTokens.Surface2);
    }

    private static void ApplyToolbarButtonStyle(ToolbarButton button)
    {
        button.FillColor = DesignTokens.Surface2;
        button.HoverFillColor = DesignTokens.SurfaceHover;
        button.PressedFillColor = DesignTokens.Surface3;
        button.BorderColor = DesignTokens.BorderBright;
        button.TextColor = DesignTokens.TextPrimary;
        button.DisabledFillColor = Color.FromArgb(21, 25, 38);
        button.DisabledBorderColor = DesignTokens.BorderSoft;
        button.DisabledTextColor = DesignTokens.TextMuted;
        button.BackColor = DesignTokens.Surface;
        button.Font = DesignTokens.FontUiBold;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Margin = new Padding(
            DesignTokens.Scale(5),
            DesignTokens.Scale(6),
            DesignTokens.Scale(5),
            DesignTokens.Scale(6));
        button.Cursor = Cursors.Hand;
    }

    private static void ApplyButtonAccent(ToolbarButton button, Color accent, Color fill)
    {
        button.BorderColor = accent;
        button.FillColor = fill;
        button.Invalidate();
    }

    private static void SetButtonText(Button button, string label, string? hotkey)
    {
        button.Text = string.IsNullOrWhiteSpace(hotkey)
            ? label
            : $"{label} ({hotkey})";
    }

    private sealed class ToolbarButton : Button
    {
        private bool _isPressed;

        public ToolbarButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
        }

        public Color FillColor { get; set; } = DesignTokens.Surface2;

        public Color HoverFillColor { get; set; } = DesignTokens.SurfaceHover;

        public Color PressedFillColor { get; set; } = DesignTokens.Surface3;

        public Color BorderColor { get; set; } = DesignTokens.BorderBright;

        public Color TextColor { get; set; } = DesignTokens.TextPrimary;

        public Color DisabledFillColor { get; set; } = DesignTokens.Surface;

        public Color DisabledBorderColor { get; set; } = DesignTokens.BorderSoft;

        public Color DisabledTextColor { get; set; } = DesignTokens.TextMuted;

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            _isPressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isPressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(Parent?.BackColor ?? DesignTokens.Surface);
            pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            bool isHovered = ClientRectangle.Contains(PointToClient(MousePosition));
            Color fill = ResolveFillColor(isHovered);
            Color border = Enabled ? BorderColor : DisabledBorderColor;
            Color text = Enabled ? TextColor : DisabledTextColor;

            using GraphicsPath path = CreateRoundedRectanglePath(bounds, DesignTokens.Scale(8));
            using var fillBrush = new SolidBrush(fill);
            using var borderPen = new Pen(border);
            pevent.Graphics.FillPath(fillBrush, path);
            pevent.Graphics.DrawPath(borderPen, path);

            TextRenderer.DrawText(
                pevent.Graphics,
                Text,
                Font,
                bounds,
                text,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }

        private Color ResolveFillColor(bool isHovered)
        {
            if (!Enabled)
            {
                return DisabledFillColor;
            }

            if (_isPressed)
            {
                return PressedFillColor;
            }

            return isHovered ? HoverFillColor : FillColor;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(Parent?.BackColor ?? DesignTokens.Surface);
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
}
