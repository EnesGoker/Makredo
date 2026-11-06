using System.Drawing.Imaging;

namespace Makredo.CiDiscovery;

internal sealed class DiscoveryForm : Form
{
    internal static readonly Color FirstMarkerColor = Color.FromArgb(231, 83, 143);
    internal static readonly Color SecondMarkerColor = Color.FromArgb(31, 211, 157);
    internal static readonly Rectangle FirstMarkerBounds = new(24, 24, 120, 60);
    internal static readonly Rectangle SecondMarkerBounds = new(496, 276, 120, 60);
    private readonly TaskCompletionSource _firstPaint = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal DiscoveryForm(string architecture)
    {
        Text = $"Makredo CI discovery — {architecture}";
        ClientSize = new Size(640, 360);
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(64, 64);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        TopMost = true;
        BackColor = Color.FromArgb(25, 45, 70);
        ForeColor = Color.White;
        Controls.Add(new Label
        {
            AutoSize = false,
            Location = new Point(24, 116),
            Size = new Size(592, 132),
            Text = $"Makredo CI discovery\n\nNative process: {architecture}\nVisible screen capture with two color markers"
        });
    }

    internal Task FirstPaint => _firstPaint.Task;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var firstBrush = new SolidBrush(FirstMarkerColor);
        using var secondBrush = new SolidBrush(SecondMarkerColor);
        e.Graphics.FillRectangle(firstBrush, FirstMarkerBounds);
        e.Graphics.FillRectangle(secondBrush, SecondMarkerBounds);
        _firstPaint.TrySetResult();
    }
}

internal static class ScreenshotProbe
{
    internal static async Task<ScreenshotProbeResult> CaptureAsync(
        DiscoveryForm form,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var result = new ScreenshotProbeResult
        {
            Visible = form.Visible,
            HandleCreated = form.IsHandleCreated,
            DeviceDpi = form.DeviceDpi,
            FileName = "screenshot-form.png"
        };
        try
        {
            await form.FirstPaint.WaitAsync(TimeSpan.FromSeconds(3), TimeProvider.System, cancellationToken);
            result.PaintObserved = true;
            form.Activate();
            form.BringToFront();
            form.Update();
            Rectangle bounds = form.RectangleToScreen(form.ClientRectangle);
            result.Bounds = $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";
            for (int attempt = 1; attempt <= 10; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // DWM presentation is asynchronous; poll observed pixels rather than assume a frame was presented.
                await Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider.System, cancellationToken);
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                }

                Color first = SampleMarker(bitmap, DiscoveryForm.FirstMarkerBounds);
                Color second = SampleMarker(bitmap, DiscoveryForm.SecondMarkerBounds);
                result.Attempts = attempt;
                result.FirstObservedColor = first.ToArgb().ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
                result.SecondObservedColor = second.ToArgb().ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
                result.MarkersMatch = Matches(first, DiscoveryForm.FirstMarkerColor) && Matches(second, DiscoveryForm.SecondMarkerColor);
                bitmap.Save(Path.Combine(outputDirectory, result.FileName), ImageFormat.Png);
                if (result.MarkersMatch)
                {
                    result.Supported = result.Visible && result.HandleCreated && result.PaintObserved;
                    break;
                }
            }
        }
        catch (Exception exception)
        {
            result.Error = exception.ToString();
        }

        return result;
    }

    private static Color SampleMarker(Bitmap bitmap, Rectangle marker) => bitmap.GetPixel(marker.Left + marker.Width / 2, marker.Top + marker.Height / 2);

    private static bool Matches(Color actual, Color expected) =>
        Math.Abs(actual.R - expected.R) <= 3 && Math.Abs(actual.G - expected.G) <= 3 && Math.Abs(actual.B - expected.B) <= 3;
}

internal sealed class ScreenshotProbeResult
{
    public bool Supported { get; set; }
    public bool Visible { get; init; }
    public bool HandleCreated { get; init; }
    public bool PaintObserved { get; set; }
    public int DeviceDpi { get; init; }
    public string? Bounds { get; set; }
    public required string FileName { get; init; }
    public int Attempts { get; set; }
    public bool MarkersMatch { get; set; }
    public string? FirstObservedColor { get; set; }
    public string? SecondObservedColor { get; set; }
    public string? Error { get; set; }
}
