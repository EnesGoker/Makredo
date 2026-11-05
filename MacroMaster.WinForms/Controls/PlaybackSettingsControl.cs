using MacroMaster.Domain.Models;
using MacroMaster.WinForms.Theme;

namespace MacroMaster.WinForms.Controls;

public partial class PlaybackSettingsControl : UserControl
{
    private bool _isApplyingSettings;

    public event EventHandler? SettingsChanged;

    public PlaybackSettingsControl()
    {
        InitializeComponent();
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        DoubleBuffered = true;
        ApplyTheme();
        PopulateSpeedOptions();
        WireEvents();
        ApplySettings(new PlaybackSettings());
    }

    public PlaybackSettings GetCurrentSettings()
    {
        return new PlaybackSettings
        {
            SpeedMultiplier = preserveTimingCheckBox.Checked
                ? 1.0
                : GetSelectedSpeedMultiplier(),
            RepeatCount = Decimal.ToInt32(repeatCountNumericUpDown.Value),
            InitialDelayMs = Decimal.ToInt32(initialDelayNumericUpDown.Value),
            LoopIndefinitely = loopIndefinitelyCheckBox.Checked,
            UseRelativeCoordinates = relativeCoordinatesCheckBox.Checked,
            SimulationMode = simulationModeCheckBox.Checked,
            StopOnError = stopOnErrorCheckBox.Checked,
            PreserveOriginalTiming = preserveTimingCheckBox.Checked
        };
    }

    public void ApplySettings(PlaybackSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _isApplyingSettings = true;

        try
        {
            SelectSpeedMultiplier(settings.SpeedMultiplier);
            repeatCountNumericUpDown.Value = ClampDecimal(
                settings.RepeatCount,
                repeatCountNumericUpDown.Minimum,
                repeatCountNumericUpDown.Maximum);
            initialDelayNumericUpDown.Value = ClampDecimal(
                settings.InitialDelayMs,
                initialDelayNumericUpDown.Minimum,
                initialDelayNumericUpDown.Maximum);
            preserveTimingCheckBox.Checked = settings.PreserveOriginalTiming;
            stopOnErrorCheckBox.Checked = settings.StopOnError;
            loopIndefinitelyCheckBox.Checked = settings.LoopIndefinitely;
            relativeCoordinatesCheckBox.Checked = settings.UseRelativeCoordinates;
            simulationModeCheckBox.Checked = settings.SimulationMode;
            RefreshDependentControlState();
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    public void SetControlsEnabled(bool enabled)
    {
        preserveTimingCheckBox.Enabled = enabled;
        stopOnErrorCheckBox.Enabled = enabled;
        loopIndefinitelyCheckBox.Enabled = enabled;
        relativeCoordinatesCheckBox.Enabled = enabled;
        simulationModeCheckBox.Enabled = enabled;
        initialDelayNumericUpDown.Enabled = enabled;
        speedComboBox.Enabled = enabled && !preserveTimingCheckBox.Checked;
        repeatCountNumericUpDown.Enabled = enabled && !loopIndefinitelyCheckBox.Checked;
    }

    private void ApplyTheme()
    {
        BackColor = DesignTokens.Surface;
        ForeColor = DesignTokens.TextPrimary;
        Font = DesignTokens.FontUiNormal;
        MinimumSize = new Size(0, DesignTokens.Scale(160));
        ApplyDensityScale();

        rootLayoutPanel.BackColor = DesignTokens.Surface;
        settingsLayoutPanel.BackColor = DesignTokens.Surface;
        initialDelayUnitLabel.ForeColor = DesignTokens.TextSecondary;
        initialDelayUnitLabel.BackColor = DesignTokens.Surface;
        initialDelayUnitLabel.Font = DesignTokens.FontUiNormal;

        foreach (Control control in settingsLayoutPanel.Controls)
        {
            ApplyChildTheme(control);
        }

        dividerPanel.BackColor = DesignTokens.BorderSoft;
    }

    private static void ApplyChildTheme(Control control)
    {
        control.ForeColor = DesignTokens.TextSecondary;
        control.BackColor = control is TextBox or ModernSelect or ModernNumericInput
            ? DesignTokens.SurfaceInset
            : DesignTokens.Surface;
        control.Font = DesignTokens.FontUiNormal;

        if (control is ModernNumericInput numericInput)
        {
            numericInput.BackColor = DesignTokens.SurfaceInset;
            numericInput.ForeColor = DesignTokens.TextPrimary;
            numericInput.TextAlign = HorizontalAlignment.Left;
            numericInput.MinimumSize = new Size(0, DesignTokens.Scale(32));
            return;
        }
        else if (control is ModernSelect select)
        {
            select.BackColor = DesignTokens.SurfaceInset;
            select.ForeColor = DesignTokens.TextPrimary;
            select.MinimumSize = new Size(0, DesignTokens.Scale(32));
            return;
        }
        else if (control is ModernCheckBox checkBox)
        {
            checkBox.BackColor = DesignTokens.Surface;
            checkBox.ForeColor = DesignTokens.TextPrimary;
            checkBox.MinimumSize = new Size(0, DesignTokens.Scale(30));
            return;
        }

        foreach (Control child in control.Controls)
        {
            ApplyChildTheme(child);
        }
    }

    private void ApplyDensityScale()
    {
        rootLayoutPanel.Padding = new Padding(
            0,
            DesignTokens.Scale(8),
            0,
            0);

        // Main columns: form fields, divider gutter, behavior toggles.
        settingsLayoutPanel.ColumnStyles[0].Width = 53F;
        settingsLayoutPanel.ColumnStyles[1].Width = DesignTokens.Scale(46);
        settingsLayoutPanel.ColumnStyles[2].Width = 47F;
        formLayoutPanel.ColumnStyles[0].Width = 36F;
        formLayoutPanel.ColumnStyles[1].Width = 64F;
        formLayoutPanel.ColumnStyles[2].Width = DesignTokens.Scale(34);

        int inputTopMargin = DesignTokens.Scale(4);
        speedLabel.Margin = new Padding(0, 0, DesignTokens.Scale(10), 0);
        repeatCountLabel.Margin = speedLabel.Margin;
        initialDelayLabel.Margin = speedLabel.Margin;
        speedComboBox.Margin = new Padding(0, inputTopMargin, DesignTokens.Scale(6), inputTopMargin);
        repeatCountNumericUpDown.Margin = speedComboBox.Margin;
        initialDelayValueLayoutPanel.Margin = speedComboBox.Margin;
        initialDelayValueLayoutPanel.ColumnStyles[1].Width = DesignTokens.Scale(30);
        initialDelayNumericUpDown.Margin = new Padding(0, 0, DesignTokens.Scale(6), 0);
        initialDelayUnitLabel.Margin = Padding.Empty;
        initialDelayUnitLabel.Padding = Padding.Empty;
        dividerPanel.Margin = new Padding(
            DesignTokens.Scale(20),
            DesignTokens.Scale(4),
            DesignTokens.Scale(20),
            DesignTokens.Scale(4));

        int optionLeftMargin = DesignTokens.Scale(22);
        int optionRightMargin = DesignTokens.Scale(30);
        int optionVerticalMargin = DesignTokens.Scale(2);
        foreach (ModernCheckBox optionCheckBox in new[]
        {
            preserveTimingCheckBox,
            loopIndefinitelyCheckBox,
            stopOnErrorCheckBox,
            relativeCoordinatesCheckBox,
            simulationModeCheckBox
        })
        {
            optionCheckBox.Margin = new Padding(
                optionLeftMargin,
                optionVerticalMargin,
                optionRightMargin,
                optionVerticalMargin);
            optionCheckBox.MinimumSize = new Size(0, DesignTokens.Scale(26));
        }
    }

    private void PopulateSpeedOptions()
    {
        speedComboBox.SetItems(["0.25x", "0.50x", "1.00x", "2.00x", "4.00x"]);
        speedComboBox.SelectedIndex = 2;
    }

    private void WireEvents()
    {
        speedComboBox.SelectedIndexChanged += OnSettingChanged;
        repeatCountNumericUpDown.ValueChanged += OnSettingChanged;
        initialDelayNumericUpDown.ValueChanged += OnSettingChanged;
        preserveTimingCheckBox.CheckedChanged += OnSettingChanged;
        stopOnErrorCheckBox.CheckedChanged += OnSettingChanged;
        loopIndefinitelyCheckBox.CheckedChanged += OnSettingChanged;
        relativeCoordinatesCheckBox.CheckedChanged += OnSettingChanged;
        simulationModeCheckBox.CheckedChanged += OnSettingChanged;
    }

    private void OnSettingChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        RefreshDependentControlState();

        if (!_isApplyingSettings)
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RefreshDependentControlState()
    {
        speedComboBox.Enabled = !preserveTimingCheckBox.Checked;
        repeatCountNumericUpDown.Enabled = !loopIndefinitelyCheckBox.Checked;
    }

    private double GetSelectedSpeedMultiplier()
    {
        return speedComboBox.SelectedItem?.ToString() switch
        {
            "0.25x" => 0.25,
            "0.50x" => 0.5,
            "2.00x" => 2.0,
            "4.00x" => 4.0,
            _ => 1.0
        };
    }

    private void SelectSpeedMultiplier(double speedMultiplier)
    {
        var targetText = speedMultiplier switch
        {
            <= 0.25 => "0.25x",
            <= 0.5 => "0.50x",
            >= 4.0 => "4.00x",
            >= 2.0 => "2.00x",
            _ => "1.00x"
        };

        speedComboBox.SelectedItem = targetText;
    }

    private static decimal ClampDecimal(
        decimal value,
        decimal minimum,
        decimal maximum)
    {
        return Math.Min(Math.Max(value, minimum), maximum);
    }
}
