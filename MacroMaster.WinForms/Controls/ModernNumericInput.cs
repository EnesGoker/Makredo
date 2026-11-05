using MacroMaster.WinForms.Theme;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;

namespace MacroMaster.WinForms.Controls;

internal sealed class ModernNumericInput : UserControl
{
    private readonly TextBox _textBox;
    private readonly SpinGlyphButton _incrementButton;
    private readonly SpinGlyphButton _decrementButton;
    private decimal _increment = 1;
    private decimal _maximum = 100;
    private decimal _minimum;
    private decimal _value;
    private bool _isUpdatingText;
    private bool _isHovered;

    public ModernNumericInput()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        DoubleBuffered = true;
        BackColor = DesignTokens.SurfaceInset;
        ForeColor = DesignTokens.TextPrimary;
        Font = DesignTokens.FontUiNormal;
        MinimumSize = new Size(DesignTokens.Scale(90), DesignTokens.Scale(30));

        _textBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = DesignTokens.SurfaceInset,
            ForeColor = DesignTokens.TextPrimary,
            Font = DesignTokens.FontUiNormal,
            TextAlign = HorizontalAlignment.Left,
            Margin = Padding.Empty,
            TabStop = true
        };

        _incrementButton = new SpinGlyphButton(isIncrement: true);
        _decrementButton = new SpinGlyphButton(isIncrement: false);

        _incrementButton.Click += (_, _) => ChangeValueBy(Increment);
        _decrementButton.Click += (_, _) => ChangeValueBy(-Increment);
        _textBox.KeyDown += textBox_KeyDown;
        _textBox.Leave += (_, _) => CommitText();
        _textBox.TextChanged += textBox_TextChanged;

        Controls.Add(_textBox);
        Controls.Add(_incrementButton);
        Controls.Add(_decrementButton);
        Value = 0;
    }

    public event EventHandler? ValueChanged;

    public decimal Increment
    {
        get => _increment;
        set => _increment = value <= 0 ? 1 : value;
    }

    public decimal Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            if (_minimum > _maximum)
            {
                _minimum = _maximum;
            }

            Value = _value;
        }
    }

    public decimal Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < _minimum)
            {
                _maximum = _minimum;
            }

            Value = _value;
        }
    }

    public decimal Value
    {
        get => _value;
        set => SetValue(value, raiseEvent: true);
    }

    public HorizontalAlignment TextAlign
    {
        get => _textBox.TextAlign;
        set => _textBox.TextAlign = value;
    }

    [AllowNull]
    public override string Text
    {
        get => _textBox.Text;
        set
        {
            _textBox.Text = value ?? string.Empty;
            CommitText();
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);

        if (_textBox is null)
        {
            return;
        }

        _textBox.Font = Font;
        PerformLayout();
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);

        if (_textBox is null)
        {
            return;
        }

        _textBox.ForeColor = Enabled ? ForeColor : DesignTokens.TextMuted;
        Invalidate();
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);

        if (_textBox is null)
        {
            return;
        }

        _textBox.BackColor = BackColor;
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);

        if (_textBox is null || _incrementButton is null || _decrementButton is null)
        {
            return;
        }

        _textBox.Enabled = Enabled;
        _textBox.ForeColor = Enabled ? ForeColor : DesignTokens.TextMuted;
        _incrementButton.Enabled = Enabled;
        _decrementButton.Enabled = Enabled;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _isHovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _isHovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (Enabled)
        {
            ChangeValueBy(e.Delta > 0 ? Increment : -Increment);
        }

        base.OnMouseWheel(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutChildren();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Parent?.BackColor ?? DesignTokens.Surface);

        Rectangle bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
        using GraphicsPath path = CreateRoundPath(bounds, DesignTokens.Scale(7));
        using var fillBrush = new SolidBrush(Enabled ? BackColor : Color.FromArgb(110, BackColor));
        using var borderPen = new Pen(ResolveBorderColor(), Math.Max(1f, DesignTokens.DensityScale));

        graphics.FillPath(fillBrush, path);
        graphics.DrawPath(borderPen, path);
    }

    private void LayoutChildren()
    {
        if (_textBox is null || _incrementButton is null || _decrementButton is null)
        {
            return;
        }

        int borderPadding = DesignTokens.Scale(10);
        int spinWidth = Math.Min(DesignTokens.Scale(24), Math.Max(18, Width / 5));
        int buttonHeight = Math.Max(1, (Height - DesignTokens.Scale(4)) / 2);
        int buttonLeft = Math.Max(borderPadding, Width - spinWidth - DesignTokens.Scale(4));

        int textHeight = Math.Min(_textBox.PreferredHeight, Math.Max(1, Height - DesignTokens.Scale(8)));
        _textBox.Bounds = new Rectangle(
            borderPadding,
            Math.Max(0, (Height - textHeight) / 2),
            Math.Max(0, buttonLeft - borderPadding - DesignTokens.Scale(4)),
            textHeight);

        _incrementButton.Bounds = new Rectangle(
            buttonLeft,
            DesignTokens.Scale(3),
            spinWidth,
            buttonHeight);

        _decrementButton.Bounds = new Rectangle(
            buttonLeft,
            DesignTokens.Scale(3) + buttonHeight,
            spinWidth,
            Math.Max(1, Height - DesignTokens.Scale(6) - buttonHeight));
    }

    private void ChangeValueBy(decimal delta)
    {
        if (!Enabled)
        {
            return;
        }

        SetValue(_value + delta, raiseEvent: true);
    }

    private void SetValue(decimal value, bool raiseEvent)
    {
        decimal clampedValue = Math.Min(Math.Max(value, Minimum), Maximum);
        if (_value == clampedValue)
        {
            UpdateText();
            return;
        }

        _value = clampedValue;
        UpdateText();
        Invalidate();

        if (raiseEvent)
        {
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CommitText()
    {
        if (_isUpdatingText)
        {
            return;
        }

        string input = _textBox.Text.Trim();
        if (!decimal.TryParse(input, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal parsedValue) &&
            !decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedValue))
        {
            UpdateText();
            return;
        }

        SetValue(parsedValue, raiseEvent: true);
    }

    private void UpdateText()
    {
        _isUpdatingText = true;
        try
        {
            _textBox.Text = decimal.Truncate(_value).ToString("0", CultureInfo.CurrentCulture);
        }
        finally
        {
            _isUpdatingText = false;
        }
    }

    private void textBox_KeyDown(object? sender, KeyEventArgs e)
    {
        _ = sender;

        if (e.KeyCode == Keys.Enter)
        {
            CommitText();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Up)
        {
            ChangeValueBy(Increment);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        if (e.KeyCode == Keys.Down)
        {
            ChangeValueBy(-Increment);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void textBox_TextChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_isUpdatingText)
        {
            return;
        }

        if (decimal.TryParse(_textBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal parsedValue) ||
            decimal.TryParse(_textBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedValue))
        {
            decimal clampedValue = Math.Min(Math.Max(parsedValue, Minimum), Maximum);
            if (_value != clampedValue)
            {
                _value = clampedValue;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private Color ResolveBorderColor()
    {
        if (!Enabled)
        {
            return Color.FromArgb(80, DesignTokens.BorderSoft);
        }

        if (_textBox.Focused)
        {
            return DesignTokens.Accent;
        }

        return _isHovered
            ? DesignTokens.BorderBright
            : DesignTokens.Border;
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

    private sealed class SpinGlyphButton : Control
    {
        private readonly bool _isIncrement;
        private bool _isHovered;
        private bool _isPressed;

        public SpinGlyphButton(bool isIncrement)
        {
            _isIncrement = isIncrement;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            Cursor = Cursors.Hand;
            BackColor = DesignTokens.SurfaceInset;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            _isPressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }

            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _isPressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Parent?.BackColor ?? DesignTokens.SurfaceInset);

            if (_isHovered || _isPressed)
            {
                using var hoverBrush = new SolidBrush(_isPressed ? DesignTokens.Surface3 : DesignTokens.SurfaceHover);
                using GraphicsPath hoverPath = CreateRoundPath(Rectangle.Inflate(ClientRectangle, -1, -1), DesignTokens.Scale(4));
                graphics.FillPath(hoverBrush, hoverPath);
            }

            DrawGlyph(graphics);
        }

        private void DrawGlyph(Graphics graphics)
        {
            int arrowSize = Math.Max(3, DesignTokens.Scale(4));
            int centerX = Width / 2;
            int centerY = Height / 2;

            Point[] points = _isIncrement
                ? [
                    new(centerX - arrowSize, centerY + arrowSize / 2),
                    new(centerX + arrowSize, centerY + arrowSize / 2),
                    new(centerX, centerY - arrowSize / 2)
                ]
                : [
                    new(centerX - arrowSize, centerY - arrowSize / 2),
                    new(centerX + arrowSize, centerY - arrowSize / 2),
                    new(centerX, centerY + arrowSize / 2)
                ];

            using var arrowBrush = new SolidBrush(Enabled ? DesignTokens.TextSecondary : DesignTokens.TextMuted);
            graphics.FillPolygon(arrowBrush, points);
        }
    }
}
