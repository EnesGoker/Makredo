using MacroMaster.WinForms.Theme;

namespace MacroMaster.WinForms.Controls;

partial class ToolbarControl
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel toolbarLayoutPanel = null!;
    private ToolbarButton recordButton = null!;
    private ToolbarButton stopButton = null!;
    private ToolbarButton playbackButton = null!;
    private ToolbarButton saveButton = null!;
    private ToolbarButton loadButton = null!;
    private ToolbarButton hotkeysButton = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components is not null)
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        toolbarLayoutPanel = new TableLayoutPanel();
        recordButton = new ToolbarButton();
        stopButton = new ToolbarButton();
        playbackButton = new ToolbarButton();
        saveButton = new ToolbarButton();
        loadButton = new ToolbarButton();
        hotkeysButton = new ToolbarButton();
        toolbarLayoutPanel.SuspendLayout();
        SuspendLayout();
        // 
        // toolbarLayoutPanel
        // 
        toolbarLayoutPanel.ColumnCount = 6;
        toolbarLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.666666F));
        toolbarLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.666666F));
        toolbarLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.666666F));
        toolbarLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.666666F));
        toolbarLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.666666F));
        toolbarLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16.666666F));
        toolbarLayoutPanel.Controls.Add(recordButton, 0, 0);
        toolbarLayoutPanel.Controls.Add(stopButton, 1, 0);
        toolbarLayoutPanel.Controls.Add(playbackButton, 2, 0);
        toolbarLayoutPanel.Controls.Add(saveButton, 3, 0);
        toolbarLayoutPanel.Controls.Add(loadButton, 4, 0);
        toolbarLayoutPanel.Controls.Add(hotkeysButton, 5, 0);
        toolbarLayoutPanel.Dock = DockStyle.Fill;
        toolbarLayoutPanel.Location = new Point(0, 0);
        toolbarLayoutPanel.Margin = Padding.Empty;
        toolbarLayoutPanel.Name = "toolbarLayoutPanel";
        toolbarLayoutPanel.Padding = new Padding(
            DesignTokens.Scale(12),
            DesignTokens.Scale(10),
            DesignTokens.Scale(12),
            DesignTokens.Scale(10));
        toolbarLayoutPanel.RowCount = 1;
        toolbarLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        toolbarLayoutPanel.Size = new Size(1200, 62);
        toolbarLayoutPanel.TabIndex = 0;
        // 
        // recordButton
        // 
        recordButton.Dock = DockStyle.Fill;
        recordButton.Location = new Point(15, 13);
        recordButton.Name = "recordButton";
        recordButton.Size = new Size(190, 36);
        recordButton.TabIndex = 0;
        recordButton.Text = "Kaydi Baslat   F8";
        // 
        // stopButton
        // 
        stopButton.Dock = DockStyle.Fill;
        stopButton.Location = new Point(211, 13);
        stopButton.Name = "stopButton";
        stopButton.Size = new Size(190, 36);
        stopButton.TabIndex = 1;
        stopButton.Text = "Durdur   F9";
        // 
        // playbackButton
        // 
        playbackButton.Dock = DockStyle.Fill;
        playbackButton.Location = new Point(407, 13);
        playbackButton.Name = "playbackButton";
        playbackButton.Size = new Size(190, 36);
        playbackButton.TabIndex = 2;
        playbackButton.Text = "Oynat   F10";
        // 
        // saveButton
        // 
        saveButton.Dock = DockStyle.Fill;
        saveButton.Location = new Point(603, 13);
        saveButton.Name = "saveButton";
        saveButton.Size = new Size(190, 36);
        saveButton.TabIndex = 3;
        saveButton.Text = "Kaydet";
        // 
        // loadButton
        // 
        loadButton.Dock = DockStyle.Fill;
        loadButton.Location = new Point(799, 13);
        loadButton.Name = "loadButton";
        loadButton.Size = new Size(190, 36);
        loadButton.TabIndex = 4;
        loadButton.Text = "Yukle";
        // 
        // hotkeysButton
        // 
        hotkeysButton.Dock = DockStyle.Fill;
        hotkeysButton.Location = new Point(995, 13);
        hotkeysButton.Name = "hotkeysButton";
        hotkeysButton.Size = new Size(190, 36);
        hotkeysButton.TabIndex = 5;
        hotkeysButton.Text = "Kisayollar   F12";
        // 
        // ToolbarControl
        // 
        AutoScaleMode = AutoScaleMode.None;
        Controls.Add(toolbarLayoutPanel);
        Name = "ToolbarControl";
        Size = new Size(1200, 62);
        toolbarLayoutPanel.ResumeLayout(false);
        ResumeLayout(false);
    }
}
