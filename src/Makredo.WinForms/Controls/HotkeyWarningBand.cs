using Makredo.Application.Hotkeys;

namespace Makredo.WinForms.Controls;

public sealed class HotkeyWarningBand : Label
{
    public HotkeyWarningBand()
    {
        Dock = DockStyle.Top;
        AutoSize = false;
        Visible = false;
        BackColor = Color.FromArgb(86, 53, 15);
        ForeColor = Color.White;
        Padding = new Padding(8);
    }

    public void ShowFailures(IReadOnlyList<HotkeyRegistrationFailure> failures)
    {
        Text = failures.Count == 0 ? string.Empty : "Global kısayol uyarısı: " + string.Join(Environment.NewLine, failures.Select(f => f.Reason));
        Visible = failures.Count > 0;
        FitHeight();
    }

    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); FitHeight(); }
    private void FitHeight()
    {
        int desired = TextRenderer.MeasureText(Text, Font, new Size(Math.Max(100, Width - Padding.Horizontal), int.MaxValue), TextFormatFlags.WordBreak).Height + Padding.Vertical;
        if (Height != desired) { Height = desired; }
    }
}
