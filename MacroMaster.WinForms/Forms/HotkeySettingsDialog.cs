using MacroMaster.Application.Abstractions;
using MacroMaster.WinForms.Controls;
using MacroMaster.WinForms.Platform;
using MacroMaster.WinForms.Theme;
using System.Drawing.Drawing2D;

namespace MacroMaster.WinForms.Forms;

public sealed class HotkeySettingsDialog : Form
{
    private static readonly Size MinimumClientSize = new(DesignTokens.Scale(600), DesignTokens.Scale(344));

    private static readonly IReadOnlyList<HotkeyModifierOption> ModifierOptions = CreateModifierOptions();

    private readonly ModernSelect _recordModifierSelect = CreateSelect();
    private readonly HotkeyKeyInput _recordKeyInput = CreateKeyInput();
    private readonly ModernSelect _playbackModifierSelect = CreateSelect();
    private readonly HotkeyKeyInput _playbackKeyInput = CreateKeyInput();
    private readonly ModernSelect _stopModifierSelect = CreateSelect();
    private readonly HotkeyKeyInput _stopKeyInput = CreateKeyInput();
    private readonly ModernSelect _hotkeySettingsModifierSelect = CreateSelect();
    private readonly HotkeyKeyInput _hotkeySettingsKeyInput = CreateKeyInput();
    private readonly ThemedDialogButton _cancelButton;

    public HotkeySettingsDialog(HotkeySettings currentSettings)
    {
        ArgumentNullException.ThrowIfNull(currentSettings);

        SelectedHotkeySettings = currentSettings.Clone();

        SuspendLayout();

        Text = "Polly";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        AutoSize = false;
        BackColor = DesignTokens.Surface;
        ForeColor = DesignTokens.TextPrimary;
        Font = DesignTokens.FontUiNormal;
        Padding = new Padding(DesignTokens.Scale(16));
        ClientSize = MinimumClientSize;
        MinimumSize = SizeFromClientSize(ClientSize);

        _cancelButton = CreateDialogButton("İptal", ThemedDialogButtonStyle.Secondary);

        PopulateSelect(_recordModifierSelect, ModifierOptions.Select(option => option.DisplayText));
        PopulateSelect(_playbackModifierSelect, ModifierOptions.Select(option => option.DisplayText));
        PopulateSelect(_stopModifierSelect, ModifierOptions.Select(option => option.DisplayText));
        PopulateSelect(_hotkeySettingsModifierSelect, ModifierOptions.Select(option => option.DisplayText));

        Controls.Add(CreateRootLayoutPanel());

        ApplyHotkeySettings(currentSettings);

        ResumeLayout(performLayout: true);
        PerformLayout();
    }

    public HotkeySettings SelectedHotkeySettings { get; private set; }

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

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _recordModifierSelect.Focus();
    }

    private TableLayoutPanel CreateRootLayoutPanel()
    {
        var rootLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = DesignTokens.Surface,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };

        rootLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.Scale(54)));
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.Scale(48)));

        rootLayoutPanel.Controls.Add(CreateHeaderPanel(), 0, 0);
        rootLayoutPanel.Controls.Add(CreateSettingsSurfacePanel(), 0, 1);
        rootLayoutPanel.Controls.Add(CreateButtonFooterPanel(), 0, 2);

        return rootLayoutPanel;
    }

    private static TableLayoutPanel CreateHeaderPanel()
    {
        var headerLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = DesignTokens.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        headerLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        headerLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));
        headerLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));

        headerLayoutPanel.Controls.Add(
            new Label
            {
                Dock = DockStyle.Fill,
                Text = "Kısayol Ayarları",
                Font = DesignTokens.FontUiBold,
                ForeColor = DesignTokens.TextPrimary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            },
            0,
            0);
        headerLayoutPanel.Controls.Add(
            new Label
            {
                Dock = DockStyle.Fill,
                Text = "Global kısayolları seçin ve kaydedin.",
                Font = DesignTokens.FontUiSmall,
                ForeColor = DesignTokens.TextSecondary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            },
            0,
            1);

        return headerLayoutPanel;
    }

    private RoundedSurfacePanel CreateSettingsSurfacePanel()
    {
        var surfacePanel = new RoundedSurfacePanel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.SurfaceInset,
            BorderColor = DesignTokens.BorderSoft,
            Margin = Padding.Empty,
            Padding = new Padding(
                DesignTokens.Scale(14),
                DesignTokens.Scale(8),
                DesignTokens.Scale(14),
                DesignTokens.Scale(8))
        };

        surfacePanel.Controls.Add(CreateSettingsTableLayoutPanel());
        return surfacePanel;
    }

    private TableLayoutPanel CreateSettingsTableLayoutPanel()
    {
        var settingsTableLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = DesignTokens.SurfaceInset,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };

        settingsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
        settingsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
        settingsTableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));

        AddHeaderRow(settingsTableLayoutPanel);
        AddHotkeyRow(
            settingsTableLayoutPanel,
            1,
            "Kaydı Başlat/Durdur",
            _recordModifierSelect,
            _recordKeyInput);
        AddHotkeyRow(
            settingsTableLayoutPanel,
            2,
            "Oynat/Duraklat",
            _playbackModifierSelect,
            _playbackKeyInput);
        AddHotkeyRow(
            settingsTableLayoutPanel,
            3,
            "Durdur",
            _stopModifierSelect,
            _stopKeyInput);
        AddHotkeyRow(
            settingsTableLayoutPanel,
            4,
            "Kısayolları Aç",
            _hotkeySettingsModifierSelect,
            _hotkeySettingsKeyInput);

        return settingsTableLayoutPanel;
    }

    private TableLayoutPanel CreateButtonFooterPanel()
    {
        var resetDefaultsButton = CreateDialogButton("Varsayılan", ThemedDialogButtonStyle.Secondary);
        var applyButton = CreateDialogButton("Kaydet", ThemedDialogButtonStyle.Primary);

        _cancelButton.DialogResult = DialogResult.Cancel;
        applyButton.Click += applyButton_Click;
        resetDefaultsButton.Click += resetDefaultsButton_Click;

        AcceptButton = applyButton;
        CancelButton = _cancelButton;

        var buttonFooterPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = DesignTokens.Surface,
            Margin = Padding.Empty,
            Padding = new Padding(0, DesignTokens.Scale(12), 0, 0),
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        buttonFooterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(132)));
        buttonFooterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        buttonFooterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(126)));
        buttonFooterPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(126)));
        buttonFooterPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        resetDefaultsButton.Dock = DockStyle.Left;
        resetDefaultsButton.Margin = Padding.Empty;
        _cancelButton.Dock = DockStyle.Fill;
        _cancelButton.Margin = new Padding(DesignTokens.Scale(8), 0, 0, 0);
        applyButton.Dock = DockStyle.Fill;
        applyButton.Margin = new Padding(DesignTokens.Scale(8), 0, 0, 0);

        buttonFooterPanel.Controls.Add(resetDefaultsButton, 0, 0);
        buttonFooterPanel.Controls.Add(_cancelButton, 2, 0);
        buttonFooterPanel.Controls.Add(applyButton, 3, 0);

        return buttonFooterPanel;
    }

    private static void AddHeaderRow(TableLayoutPanel tableLayoutPanel)
    {
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.Scale(30)));

        tableLayoutPanel.Controls.Add(CreateHeaderLabel("İşlem"), 0, 0);
        tableLayoutPanel.Controls.Add(CreateHeaderLabel("Değiştirici"), 1, 0);
        tableLayoutPanel.Controls.Add(CreateHeaderLabel("Tuş"), 2, 0);
    }

    private static void AddHotkeyRow(
        TableLayoutPanel tableLayoutPanel,
        int rowIndex,
        string actionLabel,
        ModernSelect modifierSelect,
        HotkeyKeyInput keyInput)
    {
        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.Scale(40)));

        modifierSelect.Dock = DockStyle.Fill;
        modifierSelect.Margin = new Padding(0, DesignTokens.Scale(4), DesignTokens.Scale(10), DesignTokens.Scale(4));
        keyInput.Dock = DockStyle.Fill;
        keyInput.Margin = new Padding(0, DesignTokens.Scale(4), 0, DesignTokens.Scale(4));

        tableLayoutPanel.Controls.Add(
            new Label
            {
                Dock = DockStyle.Fill,
                Text = actionLabel,
                Font = DesignTokens.FontUiBold,
                ForeColor = DesignTokens.TextPrimary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, DesignTokens.Scale(12), 0),
                AutoEllipsis = true
            },
            0,
            rowIndex);
        tableLayoutPanel.Controls.Add(modifierSelect, 1, rowIndex);
        tableLayoutPanel.Controls.Add(keyInput, 2, rowIndex);
    }

    private void applyButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        try
        {
            HotkeySettings selectedHotkeySettings = BuildHotkeySettings();
            HotkeySettingsValidator.Validate(selectedHotkeySettings, "Kısayol ayarları penceresi uygulama");
            SelectedHotkeySettings = selectedHotkeySettings;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            ModalDialogOverlay.ShowMessage(
                this,
                ex.Message,
                "Polly",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void resetDefaultsButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyHotkeySettings(HotkeySettings.CreateDefault());
    }

    private void ApplyHotkeySettings(HotkeySettings hotkeySettings)
    {
        SelectModifier(_recordModifierSelect, hotkeySettings.RecordToggleHotkey.Modifiers);
        SelectModifier(_playbackModifierSelect, hotkeySettings.PlaybackToggleHotkey.Modifiers);
        SelectModifier(_stopModifierSelect, hotkeySettings.StopHotkey.Modifiers);
        SelectModifier(_hotkeySettingsModifierSelect, hotkeySettings.HotkeySettingsHotkey.Modifiers);
        SelectKey(_recordKeyInput, hotkeySettings.RecordToggleHotkey.VirtualKeyCode);
        SelectKey(_playbackKeyInput, hotkeySettings.PlaybackToggleHotkey.VirtualKeyCode);
        SelectKey(_stopKeyInput, hotkeySettings.StopHotkey.VirtualKeyCode);
        SelectKey(_hotkeySettingsKeyInput, hotkeySettings.HotkeySettingsHotkey.VirtualKeyCode);
    }

    private HotkeySettings BuildHotkeySettings()
    {
        return new HotkeySettings
        {
            RecordToggleHotkey = new HotkeyBinding(
                GetSelectedKey(_recordKeyInput),
                GetSelectedModifiers(_recordModifierSelect)),
            PlaybackToggleHotkey = new HotkeyBinding(
                GetSelectedKey(_playbackKeyInput),
                GetSelectedModifiers(_playbackModifierSelect)),
            StopHotkey = new HotkeyBinding(
                GetSelectedKey(_stopKeyInput),
                GetSelectedModifiers(_stopModifierSelect)),
            HotkeySettingsHotkey = new HotkeyBinding(
                GetSelectedKey(_hotkeySettingsKeyInput),
                GetSelectedModifiers(_hotkeySettingsModifierSelect))
        };
    }

    private static ThemedDialogButton CreateDialogButton(string text, ThemedDialogButtonStyle style)
    {
        return new ThemedDialogButton(style)
        {
            Text = text,
            Width = DesignTokens.Scale(108),
            Height = DesignTokens.Scale(32),
            Margin = new Padding(DesignTokens.Scale(8), 0, 0, 0)
        };
    }

    private static ModernSelect CreateSelect()
    {
        return new ModernSelect
        {
            BackColor = DesignTokens.SurfaceInset,
            ForeColor = DesignTokens.TextPrimary,
            Font = DesignTokens.FontUiNormal,
            MinimumSize = new Size(0, DesignTokens.Scale(30))
        };
    }

    private static HotkeyKeyInput CreateKeyInput()
    {
        return new HotkeyKeyInput
        {
            BackColor = DesignTokens.SurfaceInset,
            ForeColor = DesignTokens.TextPrimary,
            Font = DesignTokens.FontUiNormal,
            MinimumSize = new Size(0, DesignTokens.Scale(30))
        };
    }

    private static void PopulateSelect(
        ModernSelect select,
        IEnumerable<string> options)
    {
        select.SetItems(options);
    }

    private static Label CreateHeaderLabel(string text)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Font = DesignTokens.FontUiSmall,
            ForeColor = DesignTokens.TextSecondary,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, DesignTokens.Scale(12), DesignTokens.Scale(4)),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
    }

    private static void SelectModifier(
        ModernSelect select,
        HotkeyModifiers modifiers)
    {
        int selectedIndex = ModifierOptions
            .Select((option, index) => new { option, index })
            .FirstOrDefault(pair => pair.option.Modifiers == modifiers)?.index
            ?? throw new InvalidOperationException("Kısayol değiştiricisi desteklenmiyor.");

        select.SelectedIndex = selectedIndex;
    }

    private static void SelectKey(
        HotkeyKeyInput input,
        int virtualKeyCode)
    {
        input.VirtualKeyCode = virtualKeyCode;
    }

    private static HotkeyModifiers GetSelectedModifiers(ModernSelect select)
    {
        return select.SelectedIndex >= 0 && select.SelectedIndex < ModifierOptions.Count
            ? ModifierOptions[select.SelectedIndex].Modifiers
            : throw new InvalidOperationException("Bir kısayol değiştiricisi seçilmelidir.");
    }

    private static int GetSelectedKey(HotkeyKeyInput input)
    {
        return input.VirtualKeyCode;
    }

    private static List<HotkeyModifierOption> CreateModifierOptions()
    {
        return
        [
            new HotkeyModifierOption("Yok", HotkeyModifiers.None),
            new HotkeyModifierOption("Ctrl", HotkeyModifiers.Control),
            new HotkeyModifierOption("Shift", HotkeyModifiers.Shift),
            new HotkeyModifierOption("Alt", HotkeyModifiers.Alt),
            new HotkeyModifierOption("Win", HotkeyModifiers.Windows),
            new HotkeyModifierOption("Ctrl+Shift", HotkeyModifiers.Control | HotkeyModifiers.Shift),
            new HotkeyModifierOption("Ctrl+Alt", HotkeyModifiers.Control | HotkeyModifiers.Alt),
            new HotkeyModifierOption("Ctrl+Win", HotkeyModifiers.Control | HotkeyModifiers.Windows),
            new HotkeyModifierOption("Shift+Alt", HotkeyModifiers.Shift | HotkeyModifiers.Alt),
            new HotkeyModifierOption("Shift+Win", HotkeyModifiers.Shift | HotkeyModifiers.Windows),
            new HotkeyModifierOption("Alt+Win", HotkeyModifiers.Alt | HotkeyModifiers.Windows),
            new HotkeyModifierOption(
                "Ctrl+Shift+Alt",
                HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt),
            new HotkeyModifierOption(
                "Ctrl+Shift+Win",
                HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Windows),
            new HotkeyModifierOption(
                "Ctrl+Alt+Win",
                HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Windows),
            new HotkeyModifierOption(
                "Shift+Alt+Win",
                HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Windows),
            new HotkeyModifierOption(
                "Ctrl+Shift+Alt+Win",
                HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Windows)
        ];
    }

    private sealed record HotkeyModifierOption(
        string DisplayText,
        HotkeyModifiers Modifiers);

    private sealed class RoundedSurfacePanel : Panel
    {
        public RoundedSurfacePanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
        }

        public Color BorderColor { get; set; } = DesignTokens.BorderSoft;

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent?.BackColor ?? DesignTokens.Surface);

            Rectangle bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            using GraphicsPath path = CreateRoundPath(bounds, DesignTokens.Scale(8));
            using var fillBrush = new SolidBrush(BackColor);
            using var borderPen = new Pen(BorderColor, Math.Max(1f, DesignTokens.DensityScale));
            e.Graphics.FillPath(fillBrush, path);
            e.Graphics.DrawPath(borderPen, path);
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
}
