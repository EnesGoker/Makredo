using MacroMaster.WinForms.Platform;
using MacroMaster.WinForms.Theme;

namespace MacroMaster.WinForms.Forms;

internal sealed class ThemedMessageDialog : Form
{
    private readonly ThemedDialogButton _defaultButton;

    private ThemedMessageDialog(
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        MessageContent content = ResolveContent(text, icon);
        IReadOnlyList<MessageButtonSpec> buttonSpecs = ResolveButtons(buttons);

        Text = string.IsNullOrWhiteSpace(caption)
            ? "Polly"
            : caption.Trim();
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = DesignTokens.Surface;
        ForeColor = DesignTokens.TextPrimary;
        Font = DesignTokens.FontUiNormal;

        Size clientSize = ResolveClientSize(content);
        ClientSize = clientSize;
        MinimumSize = SizeFromClientSize(clientSize);

        _defaultButton = BuildLayout(content, buttonSpecs);
    }

    public static DialogResult Show(
        IWin32Window? owner,
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        using var dialog = new ThemedMessageDialog(text, caption, buttons, icon);
        return owner is null
            ? dialog.ShowDialog()
            : dialog.ShowDialog(owner);
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
        _defaultButton.Focus();
    }

    private ThemedDialogButton BuildLayout(
        MessageContent content,
        IReadOnlyList<MessageButtonSpec> buttonSpecs)
    {
        var rootLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = DesignTokens.Surface,
            Margin = Padding.Empty,
            Padding = new Padding(DesignTokens.Scale(18))
        };
        rootLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.Scale(50)));

        FlowLayoutPanel buttonFooterPanel = CreateButtonFooter(buttonSpecs, out ThemedDialogButton defaultButton);

        rootLayoutPanel.Controls.Add(CreateTextLayoutPanel(content), 0, 0);
        rootLayoutPanel.Controls.Add(buttonFooterPanel, 0, 1);
        Controls.Add(rootLayoutPanel);

        return defaultButton;
    }

    private static TableLayoutPanel CreateTextLayoutPanel(MessageContent content)
    {
        var textLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        textLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        textLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, ResolveHeadingRowHeight(content.Heading)));
        textLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        textLayoutPanel.Controls.Add(
            new Label
            {
                Dock = DockStyle.Fill,
                Text = content.Heading,
                Font = DesignTokens.FontUiBold,
                ForeColor = DesignTokens.TextPrimary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopLeft,
                AutoEllipsis = true,
                UseMnemonic = false
            },
            0,
            0);
        textLayoutPanel.Controls.Add(
            new Label
            {
                Dock = DockStyle.Fill,
                Text = content.Message,
                Font = DesignTokens.FontUiNormal,
                ForeColor = DesignTokens.TextSecondary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopLeft,
                AutoEllipsis = true,
                UseMnemonic = false
            },
            0,
            1);

        return textLayoutPanel;
    }

    private FlowLayoutPanel CreateButtonFooter(
        IReadOnlyList<MessageButtonSpec> buttonSpecs,
        out ThemedDialogButton defaultButton)
    {
        var buttonFooterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(0, DesignTokens.Scale(14), 0, 0)
        };

        defaultButton = null!;

        foreach (MessageButtonSpec spec in buttonSpecs)
        {
            ThemedDialogButton button = CreateDialogButton(spec);
            buttonFooterPanel.Controls.Add(button);

            if (spec.IsDefault)
            {
                AcceptButton = button;
                defaultButton = button;
            }

            if (spec.IsCancel)
            {
                CancelButton = button;
            }
        }

        defaultButton ??= buttonSpecs.Count > 0
            ? (ThemedDialogButton)buttonFooterPanel.Controls[0]
            : throw new InvalidOperationException("Mesaj penceresi için en az bir buton gerekir.");

        return buttonFooterPanel;
    }

    private static ThemedDialogButton CreateDialogButton(MessageButtonSpec spec)
    {
        return new ThemedDialogButton(spec.Style)
        {
            Text = spec.Text,
            DialogResult = spec.Result,
            Width = DesignTokens.Scale(116),
            Height = DesignTokens.Scale(34),
            Margin = new Padding(DesignTokens.Scale(8), 0, 0, 0)
        };
    }

    private static Size ResolveClientSize(MessageContent content)
    {
        int width = DesignTokens.Scale(460);
        int textWidth = width - (DesignTokens.Scale(18) * 2);

        Size headingSize = TextRenderer.MeasureText(
            content.Heading,
            DesignTokens.FontUiBold,
            new Size(textWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        Size messageSize = TextRenderer.MeasureText(
            content.Message,
            DesignTokens.FontUiNormal,
            new Size(textWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        int contentHeight = Math.Max(
            DesignTokens.Scale(56),
            headingSize.Height + DesignTokens.Scale(8) + messageSize.Height);
        int height = DesignTokens.Scale(18)
            + contentHeight
            + DesignTokens.Scale(50)
            + DesignTokens.Scale(18);

        return new Size(
            width,
            Math.Clamp(height, DesignTokens.Scale(174), DesignTokens.Scale(340)));
    }

    private static int ResolveHeadingRowHeight(string heading)
    {
        Size textSize = TextRenderer.MeasureText(
            heading,
            DesignTokens.FontUiBold,
            new Size(DesignTokens.Scale(392), int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        return Math.Clamp(textSize.Height + DesignTokens.Scale(6), DesignTokens.Scale(28), DesignTokens.Scale(58));
    }

    private static MessageContent ResolveContent(string text, MessageBoxIcon icon)
    {
        string normalizedText = string.IsNullOrWhiteSpace(text)
            ? ResolveDefaultMessage(icon)
            : text.Trim();
        string[] parts = normalizedText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split(
                ["\n\n"],
                2,
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 2)
        {
            return new MessageContent(parts[0], parts[1]);
        }

        return new MessageContent(ResolveDefaultHeading(icon), normalizedText);
    }

    private static IReadOnlyList<MessageButtonSpec> ResolveButtons(MessageBoxButtons buttons)
    {
        ThemedDialogButtonStyle primaryStyle = ThemedDialogButtonStyle.Primary;

        return buttons switch
        {
            MessageBoxButtons.OK =>
            [
                new("Tamam", DialogResult.OK, primaryStyle, IsDefault: true, IsCancel: true)
            ],
            MessageBoxButtons.OKCancel =>
            [
                new("Tamam", DialogResult.OK, primaryStyle, IsDefault: true, IsCancel: false),
                new("İptal", DialogResult.Cancel, ThemedDialogButtonStyle.Secondary, IsDefault: false, IsCancel: true)
            ],
            MessageBoxButtons.YesNo =>
            [
                new("Evet", DialogResult.Yes, primaryStyle, IsDefault: true, IsCancel: false),
                new("Hayır", DialogResult.No, ThemedDialogButtonStyle.Secondary, IsDefault: false, IsCancel: true)
            ],
            MessageBoxButtons.YesNoCancel =>
            [
                new("Evet", DialogResult.Yes, primaryStyle, IsDefault: true, IsCancel: false),
                new("Hayır", DialogResult.No, ThemedDialogButtonStyle.Secondary, IsDefault: false, IsCancel: false),
                new("İptal", DialogResult.Cancel, ThemedDialogButtonStyle.Secondary, IsDefault: false, IsCancel: true)
            ],
            MessageBoxButtons.RetryCancel =>
            [
                new("Yeniden Dene", DialogResult.Retry, primaryStyle, IsDefault: true, IsCancel: false),
                new("İptal", DialogResult.Cancel, ThemedDialogButtonStyle.Secondary, IsDefault: false, IsCancel: true)
            ],
            MessageBoxButtons.AbortRetryIgnore =>
            [
                new("Durdur", DialogResult.Abort, ThemedDialogButtonStyle.Destructive, IsDefault: false, IsCancel: true),
                new("Yeniden Dene", DialogResult.Retry, primaryStyle, IsDefault: true, IsCancel: false),
                new("Yok Say", DialogResult.Ignore, ThemedDialogButtonStyle.Secondary, IsDefault: false, IsCancel: false)
            ],
            MessageBoxButtons.CancelTryContinue =>
            [
                new("İptal", DialogResult.Cancel, ThemedDialogButtonStyle.Secondary, IsDefault: false, IsCancel: true),
                new("Dene", DialogResult.TryAgain, primaryStyle, IsDefault: true, IsCancel: false),
                new("Devam Et", DialogResult.Continue, ThemedDialogButtonStyle.Secondary, IsDefault: false, IsCancel: false)
            ],
            _ =>
            [
                new("Tamam", DialogResult.OK, primaryStyle, IsDefault: true, IsCancel: true)
            ]
        };
    }

    private static string ResolveDefaultHeading(MessageBoxIcon icon)
    {
        return icon switch
        {
            MessageBoxIcon.Error => "İşlem başarısız oldu",
            MessageBoxIcon.Warning => "Uyarı",
            MessageBoxIcon.Information => "Bilgi",
            MessageBoxIcon.Question => "Onay",
            _ => "Polly"
        };
    }

    private static string ResolveDefaultMessage(MessageBoxIcon icon)
    {
        return icon switch
        {
            MessageBoxIcon.Error => "Beklenmeyen bir hata oluştu.",
            MessageBoxIcon.Warning => "Bu işlem için dikkat gerekiyor.",
            MessageBoxIcon.Information => "İşlem tamamlandı.",
            MessageBoxIcon.Question => "Devam etmek istiyor musunuz?",
            _ => "İşlem tamamlandı."
        };
    }

    private sealed record MessageContent(string Heading, string Message);

    private sealed record MessageButtonSpec(
        string Text,
        DialogResult Result,
        ThemedDialogButtonStyle Style,
        bool IsDefault,
        bool IsCancel);

}
