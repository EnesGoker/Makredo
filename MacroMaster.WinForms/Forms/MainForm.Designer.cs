namespace MacroMaster.WinForms.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;
    private Label titleLabel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeDynamicResources();
            if (components is not null)
            {
                components.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        titleLabel = new Label();
        SuspendLayout();
        // 
        // titleLabel
        // 
        titleLabel.AutoSize = true;
        titleLabel.Location = new Point(20, 16);
        titleLabel.Name = "titleLabel";
        titleLabel.Size = new Size(177, 15);
        titleLabel.TabIndex = 0;
        titleLabel.Text = "MacroMaster Kontrol Merkezi";
        // 
        // MainForm
        // 
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(984, 761);
        Controls.Add(titleLabel);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "MacroMaster Kontrol Merkezi";
        ResumeLayout(false);
        PerformLayout();
    }
}
