using MacroMaster.WinForms.Theme;
using System.Drawing.Drawing2D;

namespace MacroMaster.WinForms.Controls;

public readonly record struct PlaybackTelemetrySnapshot(
    string StatusText,
    int PlayedEventCount,
    int TotalEventCount,
    int PlayedDurationMs,
    int TotalDurationMs,
    double SpeedMultiplier,
    int RepeatCount,
    bool LoopIndefinitely,
    int InitialDelayMs);

public readonly record struct PlaybackControlState(
    PlaybackTelemetrySnapshot Telemetry,
    bool CanPlayback,
    bool CanStop,
    bool CanNavigate,
    PlaybackButtonState PlaybackButtonState);

internal sealed class PlaybackControl : UserControl
{
    private readonly PlaybackProgressBar _progressBar;
    private readonly MetricCell _statusMetricCell;
    private readonly MetricCell _eventCounterMetricCell;
    private readonly MetricCell _elapsedTimeMetricCell;
    private readonly MetricCell _remainingTimeMetricCell;
    private readonly IconButton _skipBackButton;
    private readonly IconButton _stepBackButton;
    private readonly IconButton _playbackButton;
    private readonly IconButton _stepForwardButton;
    private readonly IconButton _stopButton;
    private readonly ToolTip _toolTip;

    public event EventHandler? SkipBackClicked;
    public event EventHandler? StepBackClicked;
    public event EventHandler? PlaybackClicked;
    public event EventHandler? StepForwardClicked;
    public event EventHandler? StopClicked;

    private readonly record struct NormalizedPlaybackTelemetry(
        string StatusText,
        int PlayedEventCount,
        int TotalEventCount,
        int PlayedDurationMs,
        int RemainingDurationMs,
        double Progress);

    public PlaybackControl()
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

        _progressBar = new PlaybackProgressBar();
        _statusMetricCell = new MetricCell("Durum");
        _eventCounterMetricCell = new MetricCell("Mevcut Olay");
        _elapsedTimeMetricCell = new MetricCell("Geçen Süre");
        _remainingTimeMetricCell = new MetricCell("Kalan Süre");
        _skipBackButton = new IconButton(IconButtonKind.SkipBack);
        _stepBackButton = new IconButton(IconButtonKind.StepBack);
        _playbackButton = new IconButton(IconButtonKind.Play) { ButtonSizeOverride = DesignTokens.Scale(52) };
        _stepForwardButton = new IconButton(IconButtonKind.StepForward);
        _stopButton = new IconButton(IconButtonKind.Stop);
        _toolTip = new ToolTip();

        BuildLayout();
        WireEvents();
        UpdateState(new PlaybackControlState(
            new PlaybackTelemetrySnapshot("Hazır", 0, 0, 0, 0, 1, 1, false, 0),
            false,
            false,
            false,
            PlaybackButtonState.Play));
    }

    public void UpdateState(PlaybackControlState state)
    {
        NormalizedPlaybackTelemetry telemetry = NormalizeTelemetry(state.Telemetry);
        ApplyTelemetry(telemetry);

        _playbackButton.Kind = state.PlaybackButtonState switch
        {
            PlaybackButtonState.Pause => IconButtonKind.Pause,
            _ => IconButtonKind.Play
        };

        _playbackButton.Enabled = state.CanPlayback;
        _stopButton.Enabled = state.CanStop;
        _skipBackButton.Enabled = state.CanNavigate && telemetry.PlayedEventCount > 0;
        _stepBackButton.Enabled = state.CanNavigate && telemetry.PlayedEventCount > 0;
        _stepForwardButton.Enabled = state.CanNavigate && telemetry.PlayedEventCount < telemetry.TotalEventCount;

        _toolTip.SetToolTip(_playbackButton, state.PlaybackButtonState switch
        {
            PlaybackButtonState.Pause => "Duraklat",
            PlaybackButtonState.Resume => "Devam et",
            _ => "Oynat"
        });
        _toolTip.SetToolTip(_stopButton, "Durdur");
        _toolTip.SetToolTip(_skipBackButton, _skipBackButton.Enabled ? "Debug imlecini başa al" : "Başa almak için ilerleme gerekli");
        _toolTip.SetToolTip(_stepBackButton, _stepBackButton.Enabled ? "Debug imlecini bir olay geri al" : "Geri adım için ilerleme gerekli");
        _toolTip.SetToolTip(_stepForwardButton, _stepForwardButton.Enabled ? "Sıradaki olayı çalıştır" : "Oynatılacak olay yok");
    }

    public void UpdateTelemetry(PlaybackTelemetrySnapshot telemetry)
    {
        ApplyTelemetry(NormalizeTelemetry(telemetry));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        var rootLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = DesignTokens.Surface,
            Margin = Padding.Empty,
            Padding = new Padding(
                DesignTokens.Scale(16),
                DesignTokens.Scale(10),
                DesignTokens.Scale(16),
                DesignTokens.Scale(8))
        };
        rootLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, ResolveMetricRowHeight())); // status row
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.Scale(28))); // progress bar
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, DesignTokens.Scale(20))); // progress % label
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));                    // buttons

        var statusPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = DesignTokens.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        statusPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        statusPanel.Controls.Add(_statusMetricCell, 0, 0);
        statusPanel.Controls.Add(_eventCounterMetricCell, 1, 0);
        statusPanel.Controls.Add(_elapsedTimeMetricCell, 2, 0);
        statusPanel.Controls.Add(_remainingTimeMetricCell, 3, 0);

        var progressPercentLabel = new Label
        {
            Dock = DockStyle.Fill,
            Font = DesignTokens.FontUiSmall,
            ForeColor = DesignTokens.TextSecondary,
            BackColor = DesignTokens.Surface,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = true
        };
        _progressBar.PercentLabel = progressPercentLabel;

        var controlsLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1,
            BackColor = DesignTokens.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        controlsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        controlsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(52)));
        controlsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(52)));
        controlsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(72)));
        controlsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(52)));
        controlsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, DesignTokens.Scale(52)));
        controlsLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        _playbackButton.ButtonSizeOverride = DesignTokens.Scale(52);

        controlsLayoutPanel.Controls.Add(_skipBackButton, 1, 0);
        controlsLayoutPanel.Controls.Add(_stepBackButton, 2, 0);
        controlsLayoutPanel.Controls.Add(_playbackButton, 3, 0);
        controlsLayoutPanel.Controls.Add(_stepForwardButton, 4, 0);
        controlsLayoutPanel.Controls.Add(_stopButton, 5, 0);

        rootLayoutPanel.Controls.Add(statusPanel, 0, 0);
        rootLayoutPanel.Controls.Add(_progressBar, 0, 1);
        rootLayoutPanel.Controls.Add(progressPercentLabel, 0, 2);
        rootLayoutPanel.Controls.Add(controlsLayoutPanel, 0, 3);
        Controls.Add(rootLayoutPanel);
    }

    private static NormalizedPlaybackTelemetry NormalizeTelemetry(PlaybackTelemetrySnapshot telemetry)
    {
        int safeTotalEvents = Math.Max(0, telemetry.TotalEventCount);
        int safePlayedEvents = Math.Clamp(telemetry.PlayedEventCount, 0, safeTotalEvents);
        int safeTotalDurationMs = Math.Max(0, telemetry.TotalDurationMs);
        int safePlayedDurationMs = Math.Clamp(telemetry.PlayedDurationMs, 0, safeTotalDurationMs);
        double progress = ResolveProgressRatio(
            safePlayedEvents,
            safeTotalEvents,
            safePlayedDurationMs,
            safeTotalDurationMs);
        string statusText = string.IsNullOrWhiteSpace(telemetry.StatusText)
            ? "Hazır"
            : telemetry.StatusText;

        return new NormalizedPlaybackTelemetry(
            statusText,
            safePlayedEvents,
            safeTotalEvents,
            safePlayedDurationMs,
            Math.Max(0, safeTotalDurationMs - safePlayedDurationMs),
            progress);
    }

    private void ApplyTelemetry(NormalizedPlaybackTelemetry telemetry)
    {
        _progressBar.Progress = telemetry.Progress;
        _elapsedTimeMetricCell.ValueText = FormatDuration(telemetry.PlayedDurationMs);
        _remainingTimeMetricCell.ValueText = FormatDuration(telemetry.RemainingDurationMs);
        _statusMetricCell.ValueText = telemetry.StatusText;
        _statusMetricCell.ValueColor = ResolveStatusColor(telemetry.StatusText);
        _eventCounterMetricCell.ValueText = FormattableString.Invariant($"{telemetry.PlayedEventCount} / {telemetry.TotalEventCount}");
    }

    private void WireEvents()
    {
        _skipBackButton.Click += (_, _) => SkipBackClicked?.Invoke(this, EventArgs.Empty);
        _stepBackButton.Click += (_, _) => StepBackClicked?.Invoke(this, EventArgs.Empty);
        _playbackButton.Click += (_, _) => PlaybackClicked?.Invoke(this, EventArgs.Empty);
        _stepForwardButton.Click += (_, _) => StepForwardClicked?.Invoke(this, EventArgs.Empty);
        _stopButton.Click += (_, _) => StopClicked?.Invoke(this, EventArgs.Empty);
    }

    private static double ResolveProgressRatio(
        int playedEventCount,
        int totalEventCount,
        int playedDurationMs,
        int totalDurationMs)
    {
        if (totalDurationMs > 0)
        {
            return Math.Clamp((double)playedDurationMs / totalDurationMs, 0, 1);
        }

        return totalEventCount == 0
            ? 0
            : Math.Clamp((double)playedEventCount / totalEventCount, 0, 1);
    }

    private static string FormatDuration(int milliseconds)
    {
        TimeSpan timeSpan = TimeSpan.FromMilliseconds(Math.Max(0, milliseconds));
        return timeSpan.TotalHours >= 1
            ? FormattableString.Invariant($"{(int)timeSpan.TotalHours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}")
            : FormattableString.Invariant($"{timeSpan.Minutes:00}:{timeSpan.Seconds:00}");
    }

    private static Color ResolveStatusColor(string statusText)
    {
        return statusText is "Hazır" or "Hazir" or "Bos" or "Boş"
            ? Color.FromArgb(52, 199, 89)
            : DesignTokens.TextPrimary;
    }

    private static int ResolveMetricRowHeight()
    {
        int captionHeight = TextRenderer.MeasureText(
            "Mevcut Olay",
            DesignTokens.FontUiSmall,
            Size.Empty,
            TextFormatFlags.NoPadding).Height;
        int valueHeight = TextRenderer.MeasureText(
            "Simülasyon",
            DesignTokens.FontUiBold,
            Size.Empty,
            TextFormatFlags.NoPadding).Height;

        return Math.Max(
            DesignTokens.Scale(48),
            captionHeight + valueHeight + DesignTokens.Scale(10));
    }

    private sealed class MetricCell : Control
    {
        private readonly string _caption;
        private string _valueText = string.Empty;
        private Color _valueColor = DesignTokens.TextPrimary;

        public MetricCell(string caption)
        {
            _caption = caption;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            BackColor = DesignTokens.Surface;
            ForeColor = DesignTokens.TextPrimary;
            Font = DesignTokens.FontUiNormal;
        }

        public string ValueText
        {
            get => _valueText;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(_valueText, normalized, StringComparison.Ordinal))
                {
                    return;
                }

                _valueText = normalized;
                Invalidate();
            }
        }

        public Color ValueColor
        {
            get => _valueColor;
            set
            {
                if (_valueColor.ToArgb() == value.ToArgb())
                {
                    return;
                }

                _valueColor = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(DesignTokens.Surface);

            int captionHeight = TextRenderer.MeasureText(
                e.Graphics,
                _caption,
                DesignTokens.FontUiSmall,
                Size.Empty,
                TextFormatFlags.NoPadding).Height;
            int valueHeight = TextRenderer.MeasureText(
                e.Graphics,
                ValueText,
                DesignTokens.FontUiBold,
                Size.Empty,
                TextFormatFlags.NoPadding).Height;

            int gap = DesignTokens.Scale(2);
            int totalHeight = captionHeight + gap + valueHeight;
            int top = Math.Max(0, (Height - totalHeight) / 2);

            var captionBounds = new Rectangle(
                0,
                top,
                Width,
                captionHeight + DesignTokens.Scale(2));
            var valueBounds = new Rectangle(
                0,
                captionBounds.Bottom + gap,
                Width,
                Math.Max(0, Height - captionBounds.Bottom - gap));

            TextRenderer.DrawText(
                e.Graphics,
                _caption,
                DesignTokens.FontUiSmall,
                captionBounds,
                DesignTokens.TextSecondary,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);
            TextRenderer.DrawText(
                e.Graphics,
                ValueText,
                DesignTokens.FontUiBold,
                valueBounds,
                ValueColor,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);
        }
    }

    private sealed class PlaybackProgressBar : Control
    {
        private double _progress;

        public Label? PercentLabel { get; set; }

        public PlaybackProgressBar()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            Dock = DockStyle.Fill;
            Margin = Padding.Empty;
            BackColor = DesignTokens.Surface;
        }

        public double Progress
        {
            get => _progress;
            set
            {
                double normalized = Math.Clamp(value, 0, 1);
                if (Math.Abs(_progress - normalized) < 0.001) return;
                _progress = normalized;
                if (PercentLabel != null)
                    PercentLabel.Text = FormattableString.Invariant($"{_progress * 100:0}%");
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int trackHeight = DesignTokens.Scale(7);
            int thumbRadius = DesignTokens.Scale(7);
            int left = thumbRadius;
            int right = Width - thumbRadius;
            int y = Height / 2 - trackHeight / 2;
            int width = Math.Max(1, right - left);
            var trackBounds = new Rectangle(left, y, width, trackHeight);
            int fillWidth = (int)Math.Round(width * _progress);
            var fillBounds = new Rectangle(left, y, Math.Max(trackHeight, fillWidth), trackHeight);
            int thumbX = left + fillWidth;

            using var trackBrush = new SolidBrush(Color.FromArgb(70, 80, 96));
            using var fillBrush = new SolidBrush(DesignTokens.AccentDeep);
            using var thumbBrush = new SolidBrush(DesignTokens.Accent);
            using var thumbBorderPen = new Pen(DesignTokens.Surface, 2f);

            using GraphicsPath trackPath = CreateRoundPath(trackBounds, trackHeight);
            e.Graphics.FillPath(trackBrush, trackPath);

            if (fillWidth > 0)
            {
                using GraphicsPath fillPath = CreateRoundPath(fillBounds, trackHeight);
                e.Graphics.FillPath(fillBrush, fillPath);
            }

            var thumbBounds = new Rectangle(
                thumbX - thumbRadius,
                Height / 2 - thumbRadius,
                thumbRadius * 2,
                thumbRadius * 2);
            e.Graphics.FillEllipse(thumbBrush, thumbBounds);
            e.Graphics.DrawEllipse(thumbBorderPen, thumbBounds);
        }
    }

    private sealed class IconButton : Control
    {
        private IconButtonKind _kind;

        public IconButton(IconButtonKind kind)
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            _kind = kind;
            Dock = DockStyle.Fill;
            Margin = new Padding(DesignTokens.Scale(4));
            BackColor = DesignTokens.Surface;
            Cursor = Cursors.Hand;
            ButtonSizeOverride = DesignTokens.Scale(46);
        }

        public int ButtonSizeOverride { get; set; }

        public int ButtonSize
        {
            get => ButtonSizeOverride;
            init => ButtonSizeOverride = value;
        }

        public IconButtonKind Kind
        {
            get => _kind;
            set
            {
                if (_kind == value) return;
                _kind = value;
                Invalidate();
            }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
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
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int inset = DesignTokens.Scale(4);
            int size = Math.Min(ButtonSizeOverride, Math.Min(Width - inset, Height - inset));
            var bounds = new Rectangle(
                (Width - size) / 2,
                (Height - size) / 2,
                size,
                size);
            bool isPrimary = Kind is IconButtonKind.Play or IconButtonKind.Pause;
            Color iconColor = Enabled ? DesignTokens.TextPrimary : DesignTokens.TextMuted;
            Color background = isPrimary ? DesignTokens.AccentDeep : DesignTokens.Surface2;
            Color border = Enabled
                ? isPrimary ? DesignTokens.Accent : DesignTokens.BorderBright
                : DesignTokens.Border;

            if (ClientRectangle.Contains(PointToClient(MousePosition)) && Enabled)
                background = isPrimary ? Color.FromArgb(35, 116, 239) : DesignTokens.Surface3;

            using GraphicsPath backgroundPath = CreateRoundPath(bounds, Math.Min(20, size / 2));
            using var backgroundBrush = new SolidBrush(background);
            using var borderPen = new Pen(border, 1f);
            e.Graphics.FillPath(backgroundBrush, backgroundPath);
            e.Graphics.DrawPath(borderPen, backgroundPath);

            using var iconBrush = new SolidBrush(iconColor);
            DrawIcon(e.Graphics, bounds, iconBrush);
        }

        private void DrawIcon(Graphics g, Rectangle b, Brush brush)
        {
            switch (Kind)
            {
                case IconButtonKind.SkipBack:    DrawSkipBackIcon(g, b, brush); break;
                case IconButtonKind.StepBack:    DrawStepBackIcon(g, b, brush); break;
                case IconButtonKind.StepForward: DrawStepForwardIcon(g, b, brush); break;
                case IconButtonKind.Pause:       DrawPauseIcon(g, b, brush); break;
                case IconButtonKind.Stop:        DrawStopIcon(g, b, brush); break;
                default:                         DrawPlayIcon(g, b, brush); break;
            }
        }

        private static void DrawPlayIcon(Graphics g, Rectangle b, Brush brush)
        {
            int s = b.Width / 3;
            int x = b.Left + b.Width / 2 - s / 3;
            int y = b.Top + b.Height / 2;
            g.FillPolygon(brush, new Point[] {
                new(x - s / 2, y - s), new(x - s / 2, y + s), new(x + s, y)
            });
        }

        private static void DrawPauseIcon(Graphics g, Rectangle b, Brush brush)
        {
            int bw = Math.Max(4, b.Width / 9);
            int bh = b.Height / 3;
            int gap = bw;
            int x = b.Left + b.Width / 2 - bw - gap / 2;
            int y = b.Top + b.Height / 2 - bh / 2;
            g.FillRectangle(brush, x, y, bw, bh);
            g.FillRectangle(brush, x + bw + gap, y, bw, bh);
        }

        private static void DrawStopIcon(Graphics g, Rectangle b, Brush brush)
        {
            int s = b.Width / 4;
            g.FillRectangle(brush,
                b.Left + b.Width / 2 - s / 2,
                b.Top + b.Height / 2 - s / 2, s, s);
        }

        private static void DrawSkipBackIcon(Graphics g, Rectangle b, Brush brush)
        {
            int ih = b.Height / 3;
            int iw = b.Width / 3;
            int cx = b.Left + b.Width / 2;
            int cy = b.Top + b.Height / 2;
            int barW = Math.Max(3, b.Width / 12);
            g.FillRectangle(brush, cx - iw, cy - ih / 2, barW, ih);
            g.FillPolygon(brush, new Point[] {
                new(cx + iw / 2, cy - ih / 2),
                new(cx + iw / 2, cy + ih / 2),
                new(cx - iw / 2, cy)
            });
        }

        private static void DrawStepBackIcon(Graphics g, Rectangle b, Brush brush)
        {
            int ih = b.Height / 3;
            int iw = b.Width / 4;
            int cx = b.Left + b.Width / 2;
            int cy = b.Top + b.Height / 2;
            g.FillPolygon(brush, new Point[] {
                new(cx + iw, cy - ih / 2),
                new(cx + iw, cy + ih / 2),
                new(cx - iw, cy)
            });
        }

        private static void DrawStepForwardIcon(Graphics g, Rectangle b, Brush brush)
        {
            int ih = b.Height / 3;
            int iw = b.Width / 4;
            int cx = b.Left + b.Width / 2;
            int cy = b.Top + b.Height / 2;
            g.FillPolygon(brush, new Point[] {
                new(cx - iw, cy - ih / 2),
                new(cx - iw, cy + ih / 2),
                new(cx + iw, cy)
            });
        }

    } 

    private enum IconButtonKind
    {
        SkipBack,
        StepBack,
        Play,
        Pause,
        StepForward,
        Stop,
        Previous 
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
