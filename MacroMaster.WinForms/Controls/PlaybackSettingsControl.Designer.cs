namespace MacroMaster.WinForms.Controls;

partial class PlaybackSettingsControl
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel rootLayoutPanel = null!;
    private TableLayoutPanel settingsLayoutPanel = null!;
    private TableLayoutPanel formLayoutPanel = null!;
    private TableLayoutPanel optionsLayoutPanel = null!;
    private Label speedLabel = null!;
    private ModernSelect speedComboBox = null!;
    private Label repeatCountLabel = null!;
    private ModernNumericInput repeatCountNumericUpDown = null!;
    private Label initialDelayLabel = null!;
    private TableLayoutPanel initialDelayValueLayoutPanel = null!;
    private ModernNumericInput initialDelayNumericUpDown = null!;
    private Label initialDelayUnitLabel = null!;
    private Panel dividerPanel = null!;
    private ModernCheckBox preserveTimingCheckBox = null!;
    private ModernCheckBox loopIndefinitelyCheckBox = null!;
    private ModernCheckBox stopOnErrorCheckBox = null!;
    private ModernCheckBox relativeCoordinatesCheckBox = null!;
    private ModernCheckBox simulationModeCheckBox = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components is not null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        rootLayoutPanel = new TableLayoutPanel();
        settingsLayoutPanel = new TableLayoutPanel();
        formLayoutPanel = new TableLayoutPanel();
        optionsLayoutPanel = new TableLayoutPanel();
        speedLabel = new Label();
        speedComboBox = new ModernSelect();
        repeatCountLabel = new Label();
        repeatCountNumericUpDown = new ModernNumericInput();
        initialDelayLabel = new Label();
        initialDelayValueLayoutPanel = new TableLayoutPanel();
        initialDelayNumericUpDown = new ModernNumericInput();
        initialDelayUnitLabel = new Label();
        dividerPanel = new Panel();
        preserveTimingCheckBox = new ModernCheckBox();
        loopIndefinitelyCheckBox = new ModernCheckBox();
        stopOnErrorCheckBox = new ModernCheckBox();
        relativeCoordinatesCheckBox = new ModernCheckBox();
        simulationModeCheckBox = new ModernCheckBox();

        rootLayoutPanel.SuspendLayout();
        settingsLayoutPanel.SuspendLayout();
        formLayoutPanel.SuspendLayout();
        initialDelayValueLayoutPanel.SuspendLayout();
        optionsLayoutPanel.SuspendLayout();
        SuspendLayout();

        // rootLayoutPanel
        rootLayoutPanel.ColumnCount = 1;
        rootLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayoutPanel.Controls.Add(settingsLayoutPanel, 0, 0);
        rootLayoutPanel.Dock = DockStyle.Fill;
        rootLayoutPanel.Margin = Padding.Empty;
        rootLayoutPanel.Name = "rootLayoutPanel";
        rootLayoutPanel.Padding = Padding.Empty;
        rootLayoutPanel.RowCount = 1;
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // settingsLayoutPanel: [form fields | divider | behavior toggles]
        settingsLayoutPanel.ColumnCount = 3;
        settingsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
        settingsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32F));
        settingsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
        settingsLayoutPanel.Controls.Add(formLayoutPanel, 0, 0);
        settingsLayoutPanel.Controls.Add(dividerPanel, 1, 0);
        settingsLayoutPanel.Controls.Add(optionsLayoutPanel, 2, 0);
        settingsLayoutPanel.Dock = DockStyle.Fill;
        settingsLayoutPanel.Margin = Padding.Empty;
        settingsLayoutPanel.Name = "settingsLayoutPanel";
        settingsLayoutPanel.RowCount = 1;
        settingsLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // formLayoutPanel
        formLayoutPanel.ColumnCount = 3;
        formLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        formLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
        formLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38F));
        formLayoutPanel.Controls.Add(speedLabel, 0, 0);
        formLayoutPanel.Controls.Add(speedComboBox, 1, 0);
        formLayoutPanel.Controls.Add(repeatCountLabel, 0, 1);
        formLayoutPanel.Controls.Add(repeatCountNumericUpDown, 1, 1);
        formLayoutPanel.Controls.Add(initialDelayLabel, 0, 2);
        formLayoutPanel.Controls.Add(initialDelayValueLayoutPanel, 1, 2);
        formLayoutPanel.Dock = DockStyle.Fill;
        formLayoutPanel.Margin = Padding.Empty;
        formLayoutPanel.Name = "formLayoutPanel";
        formLayoutPanel.RowCount = 4;
        formLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
        formLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
        formLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
        formLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));

        // optionsLayoutPanel
        optionsLayoutPanel.ColumnCount = 1;
        optionsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        optionsLayoutPanel.Controls.Add(preserveTimingCheckBox, 0, 0);
        optionsLayoutPanel.Controls.Add(loopIndefinitelyCheckBox, 0, 1);
        optionsLayoutPanel.Controls.Add(stopOnErrorCheckBox, 0, 2);
        optionsLayoutPanel.Controls.Add(relativeCoordinatesCheckBox, 0, 3);
        optionsLayoutPanel.Controls.Add(simulationModeCheckBox, 0, 4);
        optionsLayoutPanel.Dock = DockStyle.Fill;
        optionsLayoutPanel.Margin = Padding.Empty;
        optionsLayoutPanel.Name = "optionsLayoutPanel";
        optionsLayoutPanel.RowCount = 5;
        optionsLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        optionsLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        optionsLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        optionsLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        optionsLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));

        // initialDelayValueLayoutPanel
        initialDelayValueLayoutPanel.ColumnCount = 2;
        initialDelayValueLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        initialDelayValueLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34F));
        initialDelayValueLayoutPanel.Controls.Add(initialDelayNumericUpDown, 0, 0);
        initialDelayValueLayoutPanel.Controls.Add(initialDelayUnitLabel, 1, 0);
        initialDelayValueLayoutPanel.Dock = DockStyle.Fill;
        initialDelayValueLayoutPanel.Margin = Padding.Empty;
        initialDelayValueLayoutPanel.Name = "initialDelayValueLayoutPanel";
        initialDelayValueLayoutPanel.RowCount = 1;
        initialDelayValueLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        speedLabel.Dock = DockStyle.Fill;
        speedLabel.Text = "Hız";
        speedLabel.TextAlign = ContentAlignment.MiddleLeft;
        speedLabel.AutoEllipsis = true;
        speedLabel.Name = "speedLabel";
        speedLabel.TabIndex = 0;

        speedComboBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        speedComboBox.Name = "speedComboBox";
        speedComboBox.TabIndex = 1;

        repeatCountLabel.Dock = DockStyle.Fill;
        repeatCountLabel.Text = "Tekrar";
        repeatCountLabel.TextAlign = ContentAlignment.MiddleLeft;
        repeatCountLabel.AutoEllipsis = true;
        repeatCountLabel.Name = "repeatCountLabel";
        repeatCountLabel.TabIndex = 2;

        repeatCountNumericUpDown.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        repeatCountNumericUpDown.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
        repeatCountNumericUpDown.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        repeatCountNumericUpDown.Value = new decimal(new int[] { 1, 0, 0, 0 });
        repeatCountNumericUpDown.Name = "repeatCountNumericUpDown";
        repeatCountNumericUpDown.TabIndex = 3;

        initialDelayLabel.Dock = DockStyle.Fill;
        initialDelayLabel.Text = "Başlangıç Gecikmesi";
        initialDelayLabel.TextAlign = ContentAlignment.MiddleLeft;
        initialDelayLabel.AutoEllipsis = true;
        initialDelayLabel.Name = "initialDelayLabel";
        initialDelayLabel.TabIndex = 4;

        initialDelayNumericUpDown.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        initialDelayNumericUpDown.Increment = new decimal(new int[] { 100, 0, 0, 0 });
        initialDelayNumericUpDown.Maximum = new decimal(new int[] { 60000, 0, 0, 0 });
        initialDelayNumericUpDown.Name = "initialDelayNumericUpDown";
        initialDelayNumericUpDown.TabIndex = 5;

        initialDelayUnitLabel.Dock = DockStyle.Fill;
        initialDelayUnitLabel.Text = "ms";
        initialDelayUnitLabel.TextAlign = ContentAlignment.MiddleLeft;
        initialDelayUnitLabel.Name = "initialDelayUnitLabel";
        initialDelayUnitLabel.TabIndex = 6;

        dividerPanel.Dock = DockStyle.Fill;
        dividerPanel.Margin = Padding.Empty;
        dividerPanel.Name = "dividerPanel";

        preserveTimingCheckBox.Dock = DockStyle.Fill;
        preserveTimingCheckBox.Text = "Gerçek zamanlı";
        preserveTimingCheckBox.Name = "preserveTimingCheckBox";
        preserveTimingCheckBox.TabIndex = 7;

        loopIndefinitelyCheckBox.Dock = DockStyle.Fill;
        loopIndefinitelyCheckBox.Text = "Sonsuz döngü";
        loopIndefinitelyCheckBox.Name = "loopIndefinitelyCheckBox";
        loopIndefinitelyCheckBox.TabIndex = 8;

        stopOnErrorCheckBox.Dock = DockStyle.Fill;
        stopOnErrorCheckBox.Text = "Hata'da durdur";
        stopOnErrorCheckBox.Name = "stopOnErrorCheckBox";
        stopOnErrorCheckBox.TabIndex = 9;

        relativeCoordinatesCheckBox.Dock = DockStyle.Fill;
        relativeCoordinatesCheckBox.Text = "Göreceli koordinat";
        relativeCoordinatesCheckBox.Name = "relativeCoordinatesCheckBox";
        relativeCoordinatesCheckBox.TabIndex = 10;

        simulationModeCheckBox.Dock = DockStyle.Fill;
        simulationModeCheckBox.Text = "Simülasyon modu";
        simulationModeCheckBox.Name = "simulationModeCheckBox";
        simulationModeCheckBox.TabIndex = 11;

        AutoScaleMode = AutoScaleMode.None;
        Controls.Add(rootLayoutPanel);
        Name = "PlaybackSettingsControl";

        rootLayoutPanel.ResumeLayout(false);
        settingsLayoutPanel.ResumeLayout(false);
        formLayoutPanel.ResumeLayout(false);
        initialDelayValueLayoutPanel.ResumeLayout(false);
        optionsLayoutPanel.ResumeLayout(false);
        settingsLayoutPanel.PerformLayout();
        ResumeLayout(false);
    }
}
