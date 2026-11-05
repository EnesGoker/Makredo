using MacroMaster.WinForms.Theme;
using System.Drawing.Drawing2D;

namespace MacroMaster.WinForms.Controls;

internal sealed class ModernScrollBar : Control
{
    private const int MinimumThumbHeight = 28;

    private bool _isDragging;
    private int _dragOffset;
    private int _largeChange = 1;
    private int _maximum;
    private int _value;

    public ModernScrollBar()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        Width = DesignTokens.Scale(12);
        MinimumSize = new Size(DesignTokens.Scale(10), 0);
        Cursor = Cursors.Hand;
        BackColor = DesignTokens.Surface;
    }

    public event EventHandler? ValueChanged;

    public int LargeChange
    {
        get => _largeChange;
        set
        {
            _largeChange = Math.Max(1, value);
            Value = _value;
            Invalidate();
        }
    }

    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(0, value);
            Value = _value;
            Invalidate();
        }
    }

    public int Value
    {
        get => _value;
        set => SetValue(value, raiseEvent: true);
    }

    public void SetRange(int maximum, int largeChange, int value)
    {
        _maximum = Math.Max(0, maximum);
        _largeChange = Math.Max(1, largeChange);
        SetValue(value, raiseEvent: false);
        Invalidate();
    }

    public void SetValueSilently(int value)
    {
        SetValue(value, raiseEvent: false);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left || _maximum <= 0)
        {
            return;
        }

        Rectangle thumbBounds = GetThumbBounds();
        if (thumbBounds.Contains(e.Location))
        {
            _isDragging = true;
            _dragOffset = e.Y - thumbBounds.Top;
            Capture = true;
            return;
        }

        Value += e.Y < thumbBounds.Top ? -_largeChange : _largeChange;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_isDragging || _maximum <= 0)
        {
            return;
        }

        Rectangle trackBounds = GetTrackBounds();
        Rectangle thumbBounds = GetThumbBounds();
        int movableHeight = Math.Max(1, trackBounds.Height - thumbBounds.Height);
        int thumbTop = Math.Clamp(e.Y - _dragOffset, trackBounds.Top, trackBounds.Bottom - thumbBounds.Height);
        float positionRatio = (thumbTop - trackBounds.Top) / (float)movableHeight;
        Value = (int)Math.Round(positionRatio * _maximum);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _isDragging = false;
        Capture = false;
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (!_isDragging)
        {
            Invalidate();
        }

        base.OnMouseLeave(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? DesignTokens.Surface);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Rectangle trackBounds = GetTrackBounds();
        if (trackBounds.Width <= 0 || trackBounds.Height <= 0)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var trackPath = CreateRoundPath(trackBounds, trackBounds.Width / 2);
        using var trackBrush = new SolidBrush(Color.FromArgb(42, DesignTokens.BorderSoft));
        e.Graphics.FillPath(trackBrush, trackPath);

        if (_maximum <= 0)
        {
            return;
        }

        Rectangle thumbBounds = GetThumbBounds();
        using var thumbPath = CreateRoundPath(thumbBounds, thumbBounds.Width / 2);
        using var thumbBrush = new SolidBrush(ResolveThumbColor());
        e.Graphics.FillPath(thumbBrush, thumbPath);
    }

    private Color ResolveThumbColor()
    {
        if (_isDragging)
        {
            return Color.FromArgb(210, DesignTokens.Accent);
        }

        return ClientRectangle.Contains(PointToClient(Cursor.Position))
            ? Color.FromArgb(170, DesignTokens.TextSecondary)
            : Color.FromArgb(120, DesignTokens.TextMuted);
    }

    private void SetValue(int value, bool raiseEvent)
    {
        int nextValue = Math.Clamp(value, 0, _maximum);
        if (_value == nextValue)
        {
            return;
        }

        _value = nextValue;
        Invalidate();

        if (raiseEvent)
        {
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private Rectangle GetTrackBounds()
    {
        int width = Math.Max(DesignTokens.Scale(6), Math.Min(DesignTokens.Scale(8), Width - DesignTokens.Scale(4)));
        return new Rectangle(
            (Width - width) / 2,
            DesignTokens.Scale(4),
            width,
            Math.Max(0, Height - DesignTokens.Scale(8)));
    }

    private Rectangle GetThumbBounds()
    {
        Rectangle trackBounds = GetTrackBounds();
        if (_maximum <= 0)
        {
            return Rectangle.Empty;
        }

        int thumbHeight = Math.Max(
            DesignTokens.Scale(MinimumThumbHeight),
            (int)Math.Round(trackBounds.Height * (_largeChange / (float)(_maximum + _largeChange))));
        thumbHeight = Math.Min(trackBounds.Height, thumbHeight);

        int movableHeight = Math.Max(1, trackBounds.Height - thumbHeight);
        int top = trackBounds.Top + (int)Math.Round(movableHeight * (_value / (float)_maximum));
        return new Rectangle(trackBounds.Left, top, trackBounds.Width, thumbHeight);
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
