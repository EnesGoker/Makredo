using MacroMaster.Domain.Enums;
using MacroMaster.Domain.Models;
using MacroMaster.WinForms.Controls;
using MacroMaster.WinForms.Platform;
using MacroMaster.WinForms.Theme;

namespace MacroMaster.WinForms.Forms;

internal sealed class EventEditDialog : Form
{
    private readonly ModernNumericInput _delayNumericInput = new();
    private readonly ModernNumericInput _xNumericInput = new();
    private readonly ModernNumericInput _yNumericInput = new();
    private readonly ThemedDialogButton _cancelButton;
    private readonly bool _coordinatesEditable;

    public EventEditDialog(int eventIndex, MacroEvent macroEvent)
    {
        ArgumentNullException.ThrowIfNull(macroEvent);

        _coordinatesEditable = macroEvent.EventType == MacroEventType.Mouse
            || macroEvent.X.HasValue
            || macroEvent.Y.HasValue;

        Text = $"Olay #{eventIndex + 1:000} Düzenle";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = DesignTokens.Surface;
        ForeColor = DesignTokens.TextPrimary;
        Font = DesignTokens.FontUiNormal;
        ClientSize = new Size(DesignTokens.Scale(420), DesignTokens.Scale(264));
        MinimumSize = Size;

        _cancelButton = CreateDialogButton("İptal", ThemedDialogButtonStyle.Secondary);
        BuildLayout(macroEvent);
    }

    public EventEditResult EditResult { get; private set; } = new(0, null, null);

    private void BuildLayout(MacroEvent macroEvent)
    {
        var rootLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = DesignTokens.Surface,
            Padding = new Padding(DesignTokens.Scale(18)),
            Margin = Padding.Empty
        };
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.Scale(44)));
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.Scale(132)));
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.Scale(52)));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Olay ayarlarını düzenle",
            Font = DesignTokens.FontUiBold,
            ForeColor = DesignTokens.TextPrimary,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        var fieldsLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        fieldsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(136)));
        fieldsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        fieldsLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.Scale(44)));
        fieldsLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.Scale(44)));
        fieldsLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.Scale(44)));

        ConfigureNumericInput(_delayNumericInput, 0, 600000, macroEvent.DelayMs);
        ConfigureNumericInput(_xNumericInput, -100000, 100000, macroEvent.X ?? 0);
        ConfigureNumericInput(_yNumericInput, -100000, 100000, macroEvent.Y ?? 0);
        _xNumericInput.Enabled = _coordinatesEditable;
        _yNumericInput.Enabled = _coordinatesEditable;

        fieldsLayoutPanel.Controls.Add(CreateFieldLabel("Gecikme (ms)"), 0, 0);
        fieldsLayoutPanel.Controls.Add(_delayNumericInput, 1, 0);
        fieldsLayoutPanel.Controls.Add(CreateFieldLabel("X koordinatı"), 0, 1);
        fieldsLayoutPanel.Controls.Add(_xNumericInput, 1, 1);
        fieldsLayoutPanel.Controls.Add(CreateFieldLabel("Y koordinatı"), 0, 2);
        fieldsLayoutPanel.Controls.Add(_yNumericInput, 1, 2);

        var buttonLayoutPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, DesignTokens.Scale(10), 0, 0)
        };

        var saveButton = CreateDialogButton("Kaydet", ThemedDialogButtonStyle.Primary);

        saveButton.Click += (_, _) => SaveAndClose();
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = saveButton;
        CancelButton = _cancelButton;
        buttonLayoutPanel.Controls.Add(saveButton);
        buttonLayoutPanel.Controls.Add(_cancelButton);

        rootLayoutPanel.Controls.Add(titleLabel, 0, 0);
        rootLayoutPanel.Controls.Add(fieldsLayoutPanel, 0, 1);
        rootLayoutPanel.Controls.Add(buttonLayoutPanel, 0, 2);

        Controls.Add(rootLayoutPanel);
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

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _delayNumericInput.Focus();
    }

    private void SaveAndClose()
    {
        EditResult = new EventEditResult(
            Decimal.ToInt32(_delayNumericInput.Value),
            _coordinatesEditable ? Decimal.ToInt32(_xNumericInput.Value) : null,
            _coordinatesEditable ? Decimal.ToInt32(_yNumericInput.Value) : null);

        DialogResult = DialogResult.OK;
        Close();
    }

    private static void ConfigureNumericInput(
        ModernNumericInput numericInput,
        decimal minimum,
        decimal maximum,
        decimal value)
    {
        numericInput.Dock = DockStyle.Fill;
        numericInput.Minimum = minimum;
        numericInput.Maximum = maximum;
        numericInput.Increment = 1;
        numericInput.Value = Math.Min(Math.Max(value, minimum), maximum);
        numericInput.BackColor = DesignTokens.SurfaceInset;
        numericInput.ForeColor = DesignTokens.TextPrimary;
        numericInput.Font = DesignTokens.FontUiNormal;
        numericInput.TextAlign = HorizontalAlignment.Left;
        numericInput.Margin = new Padding(0, DesignTokens.Scale(4), 0, DesignTokens.Scale(4));
        numericInput.MinimumSize = new Size(0, DesignTokens.Scale(32));
    }

    private static Label CreateFieldLabel(string text)
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

    private static ThemedDialogButton CreateDialogButton(string text, ThemedDialogButtonStyle style)
    {
        return new ThemedDialogButton(style)
        {
            Text = text,
            Width = DesignTokens.Scale(112),
            Height = DesignTokens.Scale(34),
            Margin = new Padding(DesignTokens.Scale(8), 0, 0, 0)
        };
    }
}

internal sealed record EventEditResult(int DelayMs, int? X, int? Y);
