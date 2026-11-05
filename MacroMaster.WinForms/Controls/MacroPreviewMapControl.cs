using MacroMaster.Domain.Enums;
using MacroMaster.Domain.Models;
using MacroMaster.WinForms.Theme;
using System.Drawing.Drawing2D;

namespace MacroMaster.WinForms.Controls;

internal readonly record struct MacroPreviewMapState(
    int EventCount,
    int TotalDurationMs,
    string StatusText);

internal readonly record struct MacroPreviewMapPointInfo(
    int SourceEventIndex,
    int EventNumber,
    string ActionText,
    string DetailText,
    int X,
    int Y,
    int DelayMs);

internal sealed class MacroPreviewMapPointEventArgs : EventArgs
{
    public MacroPreviewMapPointEventArgs(MacroPreviewMapPointInfo? pointInfo)
    {
        PointInfo = pointInfo;
    }

    public MacroPreviewMapPointInfo? PointInfo { get; }
}

internal sealed class MacroPreviewMapControl : Control
{
    private const int MaxRoutePointCount = 260;
    private const int MaxMarkerCount = 80;

    private MacroPreviewMapState _state;
    private List<MapPoint> _mousePoints = [];
    private RectangleF _coordinateBounds = RectangleF.Empty;
    private int? _activeSourceEventIndex;
    private int? _hoveredMousePointIndex;
    private readonly System.Windows.Forms.Timer _pulseTimer;
    private float _pulsePhase;

    public bool InspectionEnabled { get; set; }

    public event EventHandler? PreviewRequested;
    public event EventHandler<MacroPreviewMapPointEventArgs>? PointInspected;

    public MacroPreviewMapControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint |
            ControlStyles.Selectable,
            true);

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        BackColor = DesignTokens.SurfaceInset;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleName = "Makro önizleme haritası";
        AccessibleDescription = "Seçili makronun fare rotasını küçük harita olarak gösterir.";

        _pulseTimer = new System.Windows.Forms.Timer
        {
            Interval = 80
        };
        _pulseTimer.Tick += PulseTimer_Tick;
        _pulseTimer.Start();
    }

    public void UpdatePreview(
        int eventCount,
        int durationMs,
        string statusText,
        IReadOnlyList<MacroEvent>? events = null,
        int? activeSourceEventIndex = null)
    {
        UpdatePreview(new MacroPreviewMapState(
            Math.Max(0, eventCount),
            Math.Max(0, durationMs),
            statusText), events, activeSourceEventIndex);
    }

    public void UpdatePreview(
        MacroPreviewMapState state,
        IReadOnlyList<MacroEvent>? events = null,
        int? activeSourceEventIndex = null)
    {
        _state = new MacroPreviewMapState(
            Math.Max(0, state.EventCount),
            Math.Max(0, state.TotalDurationMs),
            state.StatusText ?? string.Empty);
        _mousePoints = ExtractMousePoints(events);
        _coordinateBounds = CalculateCoordinateBounds(_mousePoints);
        _activeSourceEventIndex = NormalizeActiveSourceEventIndex(
            activeSourceEventIndex,
            events?.Count ?? 0);
        if (_hoveredMousePointIndex.HasValue
            && _hoveredMousePointIndex.Value >= _mousePoints.Count)
        {
            _hoveredMousePointIndex = null;
        }

        Invalidate();
    }

    public MacroPreviewMapPointInfo? GetActivePointInfo()
    {
        if (_mousePoints.Count == 0)
        {
            return null;
        }

        int currentMousePointIndex = ResolveCurrentMousePointIndex(
            _mousePoints,
            _activeSourceEventIndex);
        return CreatePointInfo(_mousePoints[currentMousePointIndex]);
    }

    public void UpdateActiveSourceEventIndex(int? activeSourceEventIndex)
    {
        int? normalizedActiveSourceEventIndex = NormalizeActiveSourceEventIndex(
            activeSourceEventIndex,
            _state.EventCount);
        if (_activeSourceEventIndex == normalizedActiveSourceEventIndex)
        {
            return;
        }

        _activeSourceEventIndex = normalizedActiveSourceEventIndex;
        Invalidate();
    }

    public MacroPreviewMapPointInfo? GetInspectedPointInfo()
    {
        if (_hoveredMousePointIndex.HasValue
            && _hoveredMousePointIndex.Value >= 0
            && _hoveredMousePointIndex.Value < _mousePoints.Count)
        {
            return CreatePointInfo(_mousePoints[_hoveredMousePointIndex.Value]);
        }

        return GetActivePointInfo();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);

        if (InspectionEnabled)
        {
            if (e is MouseEventArgs mouseEventArgs)
            {
                int? hitIndex = HitTestMousePoint(mouseEventArgs.Location);
                if (hitIndex.HasValue)
                {
                    SetHoveredMousePointIndex(hitIndex);
                    RaisePointInspected(hitIndex);
                }
            }

            return;
        }

        PreviewRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (InspectionEnabled)
        {
            return;
        }

        if (e.KeyCode is Keys.Enter or Keys.Space)
        {
            e.Handled = true;
            PreviewRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!InspectionEnabled)
        {
            return;
        }

        int? hitIndex = HitTestMousePoint(e.Location);
        if (hitIndex != _hoveredMousePointIndex)
        {
            SetHoveredMousePointIndex(hitIndex);
            RaisePointInspected(hitIndex);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);

        if (!InspectionEnabled)
        {
            return;
        }

        if (_hoveredMousePointIndex.HasValue)
        {
            SetHoveredMousePointIndex(null);
            PointInspected?.Invoke(this, new MacroPreviewMapPointEventArgs(null));
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(Parent?.BackColor ?? DesignTokens.Surface);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        Rectangle bounds = GetInnerBounds(ClientRectangle);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using GraphicsPath path = CreateRoundPath(bounds, DesignTokens.Scale(9));
        using var fillBrush = new SolidBrush(DesignTokens.SurfaceInset);
        using var borderPen = new Pen(DesignTokens.BorderSoft);
        graphics.FillPath(fillBrush, path);
        graphics.DrawPath(borderPen, path);

        Rectangle plotBounds = GetPlotBounds(ClientRectangle);
        DrawGrid(graphics, plotBounds);

        if (_state.EventCount <= 0)
        {
            DrawEmptyState(graphics, plotBounds, "Harita için makro seçin");
            return;
        }

        if (_mousePoints.Count == 0)
        {
            DrawEmptyState(graphics, plotBounds, "Fare rotası bulunamadı");
            return;
        }

        PointF[] resolvedPath = ResolvePath(plotBounds, _mousePoints, _coordinateBounds);
        int currentMousePointIndex = ResolveCurrentMousePointIndex(
            _mousePoints,
            _activeSourceEventIndex);
        PointF[] routePath = SampleRoute(resolvedPath);
        using var routePen = new Pen(Color.FromArgb(46, DesignTokens.Accent), Math.Max(1.5f, DesignTokens.Scale(2)))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        if (routePath.Length > 1)
        {
            graphics.DrawLines(routePen, routePath);
        }

        if (currentMousePointIndex > 0)
        {
            PointF[] activePath = SampleRoute(resolvedPath[..(currentMousePointIndex + 1)]);
            using var activeRouteGlowPen = new Pen(Color.FromArgb(42, DesignTokens.Accent), Math.Max(5f, DesignTokens.Scale(5)))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            using var activeRoutePen = new Pen(Color.FromArgb(224, DesignTokens.Accent), Math.Max(2.25f, DesignTokens.Scale(2)))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            if (activePath.Length > 1)
            {
                graphics.DrawLines(activeRouteGlowPen, activePath);
                graphics.DrawLines(activeRoutePen, activePath);
            }
        }

        DrawActionMarkers(graphics, plotBounds, _mousePoints, resolvedPath);
        DrawHoveredPoint(graphics, resolvedPath);

        PointF currentPoint = resolvedPath[currentMousePointIndex];
        DrawActivePointHalo(graphics, currentPoint);
        DrawPoint(graphics, resolvedPath[0], DesignTokens.AccentGreen, DesignTokens.Scale(5));
        if (currentMousePointIndex != resolvedPath.Length - 1)
        {
            DrawPoint(graphics, resolvedPath[^1], Color.FromArgb(170, DesignTokens.AccentOrange), DesignTokens.Scale(4));
        }

        DrawPoint(graphics, currentPoint, DesignTokens.AccentOrange, DesignTokens.Scale(6));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pulseTimer.Tick -= PulseTimer_Tick;
            _pulseTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void PulseTimer_Tick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        _pulsePhase += 0.28f;
        if (_pulsePhase > MathF.PI * 2f)
        {
            _pulsePhase -= MathF.PI * 2f;
        }

        if (_mousePoints.Count > 0 && IsHandleCreated && !IsDisposed)
        {
            Invalidate();
        }
    }

    private static void DrawGrid(Graphics graphics, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using var minorPen = new Pen(Color.FromArgb(22, DesignTokens.BorderBright));
        using var majorPen = new Pen(Color.FromArgb(44, DesignTokens.BorderBright));
        int minorStep = Math.Max(DesignTokens.Scale(10), 6);
        int majorStep = minorStep * 4;

        for (int x = bounds.Left; x <= bounds.Right; x += minorStep)
        {
            bool isMajor = (x - bounds.Left) % majorStep == 0;
            graphics.DrawLine(isMajor ? majorPen : minorPen, x, bounds.Top, x, bounds.Bottom);
        }

        for (int y = bounds.Top; y <= bounds.Bottom; y += minorStep)
        {
            bool isMajor = (y - bounds.Top) % majorStep == 0;
            graphics.DrawLine(isMajor ? majorPen : minorPen, bounds.Left, y, bounds.Right, y);
        }
    }

    private static void DrawEmptyState(
        Graphics graphics,
        Rectangle bounds,
        string text)
    {
        TextRenderer.DrawText(
            graphics,
            text,
            DesignTokens.FontUiSmall,
            bounds,
            DesignTokens.TextMuted,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
    }

    private static List<MapPoint> ExtractMousePoints(IReadOnlyList<MacroEvent>? events)
    {
        if (events is null || events.Count == 0)
        {
            return [];
        }

        var points = new List<MapPoint>(events.Count);
        for (int index = 0; index < events.Count; index++)
        {
            MacroEvent macroEvent = events[index];
            if (macroEvent.EventType != MacroEventType.Mouse
                || !macroEvent.X.HasValue
                || !macroEvent.Y.HasValue)
            {
                continue;
            }

            points.Add(new MapPoint(
                index,
                macroEvent.X.Value,
                macroEvent.Y.Value,
                macroEvent.MouseActionType,
                Math.Max(0, macroEvent.DelayMs),
                macroEvent.Description ?? string.Empty));
        }

        return points;
    }

    private static RectangleF CalculateCoordinateBounds(IReadOnlyList<MapPoint> points)
    {
        if (points.Count == 0)
        {
            return RectangleF.Empty;
        }

        int minX = points[0].X;
        int maxX = points[0].X;
        int minY = points[0].Y;
        int maxY = points[0].Y;

        for (int index = 1; index < points.Count; index++)
        {
            MapPoint point = points[index];
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minY = Math.Min(minY, point.Y);
            maxY = Math.Max(maxY, point.Y);
        }

        if (minX == maxX)
        {
            minX--;
            maxX++;
        }

        if (minY == maxY)
        {
            minY--;
            maxY++;
        }

        return RectangleF.FromLTRB(minX, minY, maxX, maxY);
    }

    private static PointF[] ResolvePath(
        Rectangle bounds,
        IReadOnlyList<MapPoint> points,
        RectangleF coordinateBounds)
    {
        PointF[] resolved = new PointF[points.Count];
        if (points.Count == 0 || coordinateBounds.IsEmpty)
        {
            return resolved;
        }

        float coordinateWidth = Math.Max(1f, coordinateBounds.Width);
        float coordinateHeight = Math.Max(1f, coordinateBounds.Height);
        float scale = Math.Min(
            bounds.Width / coordinateWidth,
            bounds.Height / coordinateHeight);
        float scaledWidth = coordinateWidth * scale;
        float scaledHeight = coordinateHeight * scale;
        float offsetX = bounds.Left + (bounds.Width - scaledWidth) / 2f;
        float offsetY = bounds.Top + (bounds.Height - scaledHeight) / 2f;

        for (int index = 0; index < points.Count; index++)
        {
            MapPoint point = points[index];
            resolved[index] = new PointF(
                offsetX + (point.X - coordinateBounds.Left) * scale,
                offsetY + (point.Y - coordinateBounds.Top) * scale);
        }

        return resolved;
    }

    private static PointF[] SampleRoute(PointF[] points)
    {
        if (points.Length <= MaxRoutePointCount)
        {
            return points;
        }

        var sampled = new List<PointF>(MaxRoutePointCount)
        {
            points[0]
        };

        int lastIntermediateIndex = points.Length - 2;
        int intermediateCount = Math.Max(0, MaxRoutePointCount - 2);

        for (int index = 1; index <= intermediateCount; index++)
        {
            int sourceIndex = (int)Math.Round(index * lastIntermediateIndex / (double)(intermediateCount + 1));
            sourceIndex = Math.Clamp(sourceIndex, 1, lastIntermediateIndex);
            sampled.Add(points[sourceIndex]);
        }

        sampled.Add(points[^1]);
        return sampled.ToArray();
    }

    private static void DrawActionMarkers(
        Graphics graphics,
        Rectangle plotBounds,
        IReadOnlyList<MapPoint> points,
        PointF[] resolvedPath)
    {
        if (points.Count == 0)
        {
            return;
        }

        var markers = new List<(PointF Point, Color Color)>();
        for (int index = 0; index < points.Count; index++)
        {
            Color? markerColor = ResolveMarkerColor(points[index].ActionType);
            if (markerColor.HasValue)
            {
                markers.Add((resolvedPath[index], markerColor.Value));
            }
        }

        foreach ((PointF point, Color color) in SampleMarkers(markers))
        {
            if (plotBounds.Contains(Point.Round(point)))
            {
                DrawPoint(graphics, point, color, DesignTokens.Scale(4));
            }
        }
    }

    private void DrawHoveredPoint(
        Graphics graphics,
        PointF[] resolvedPath)
    {
        if (!_hoveredMousePointIndex.HasValue
            || _hoveredMousePointIndex.Value < 0
            || _hoveredMousePointIndex.Value >= resolvedPath.Length)
        {
            return;
        }

        PointF point = resolvedPath[_hoveredMousePointIndex.Value];
        float outerRadius = DesignTokens.Scale(12);
        float innerRadius = DesignTokens.Scale(7);

        using var outerPen = new Pen(Color.FromArgb(210, DesignTokens.TextPrimary), Math.Max(1.4f, DesignTokens.DensityScale));
        using var glowBrush = new SolidBrush(Color.FromArgb(42, DesignTokens.Accent));
        graphics.FillEllipse(
            glowBrush,
            point.X - outerRadius,
            point.Y - outerRadius,
            outerRadius * 2f,
            outerRadius * 2f);
        graphics.DrawEllipse(
            outerPen,
            point.X - innerRadius,
            point.Y - innerRadius,
            innerRadius * 2f,
            innerRadius * 2f);
    }

    private static Color? ResolveMarkerColor(MouseActionType actionType)
    {
        return actionType switch
        {
            MouseActionType.LeftDown or
            MouseActionType.RightDown or
            MouseActionType.MiddleDown or
            MouseActionType.DoubleClick => DesignTokens.AccentRed,
            MouseActionType.Wheel => DesignTokens.AccentPurple,
            _ => null
        };
    }

    private int? HitTestMousePoint(Point location)
    {
        if (_mousePoints.Count == 0 || _coordinateBounds.IsEmpty)
        {
            return null;
        }

        Rectangle plotBounds = GetPlotBounds(ClientRectangle);
        if (plotBounds.Width <= 0 || plotBounds.Height <= 0)
        {
            return null;
        }

        PointF[] resolvedPath = ResolvePath(plotBounds, _mousePoints, _coordinateBounds);
        float hitRadius = Math.Max(DesignTokens.Scale(12), 10);
        float bestDistanceSquared = hitRadius * hitRadius;
        int? bestIndex = null;

        for (int index = 0; index < resolvedPath.Length; index++)
        {
            float deltaX = resolvedPath[index].X - location.X;
            float deltaY = resolvedPath[index].Y - location.Y;
            float distanceSquared = deltaX * deltaX + deltaY * deltaY;
            if (distanceSquared <= bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private void SetHoveredMousePointIndex(int? mousePointIndex)
    {
        if (_hoveredMousePointIndex == mousePointIndex)
        {
            return;
        }

        _hoveredMousePointIndex = mousePointIndex;
        Cursor = InspectionEnabled && mousePointIndex.HasValue
            ? Cursors.Hand
            : Cursors.Default;
        Invalidate();
    }

    private void RaisePointInspected(int? mousePointIndex)
    {
        MacroPreviewMapPointInfo? pointInfo = null;
        if (mousePointIndex.HasValue
            && mousePointIndex.Value >= 0
            && mousePointIndex.Value < _mousePoints.Count)
        {
            pointInfo = CreatePointInfo(_mousePoints[mousePointIndex.Value]);
        }

        PointInspected?.Invoke(this, new MacroPreviewMapPointEventArgs(pointInfo));
    }

    private static MacroPreviewMapPointInfo CreatePointInfo(MapPoint point)
    {
        return new MacroPreviewMapPointInfo(
            point.SourceIndex,
            point.SourceIndex + 1,
            FormatMouseAction(point.ActionType),
            string.IsNullOrWhiteSpace(point.Description)
                ? FormatMouseDetail(point.ActionType)
                : point.Description,
            point.X,
            point.Y,
            point.DelayMs);
    }

    private static string FormatMouseAction(MouseActionType actionType)
    {
        return actionType switch
        {
            MouseActionType.Move => "Hareket",
            MouseActionType.LeftDown => "Sol Basma",
            MouseActionType.LeftUp => "Sol Bırakma",
            MouseActionType.RightDown => "Sağ Basma",
            MouseActionType.RightUp => "Sağ Bırakma",
            MouseActionType.MiddleDown => "Orta Basma",
            MouseActionType.MiddleUp => "Orta Bırakma",
            MouseActionType.Wheel => "Kaydırma",
            MouseActionType.DoubleClick => "Çift Tıklama",
            _ => "Fare"
        };
    }

    private static string FormatMouseDetail(MouseActionType actionType)
    {
        return actionType switch
        {
            MouseActionType.LeftDown => "Sol tuş basıldı",
            MouseActionType.LeftUp => "Sol tuş bırakıldı",
            MouseActionType.RightDown => "Sağ tuş basıldı",
            MouseActionType.RightUp => "Sağ tuş bırakıldı",
            MouseActionType.MiddleDown => "Orta tuş basıldı",
            MouseActionType.MiddleUp => "Orta tuş bırakıldı",
            MouseActionType.Wheel => "Kaydırma hareketi",
            MouseActionType.DoubleClick => "Çift tıklama",
            _ => "Fare olayı"
        };
    }

    private static Rectangle GetInnerBounds(Rectangle clientRectangle)
    {
        return Rectangle.Inflate(clientRectangle, -1, -1);
    }

    private static Rectangle GetPlotBounds(Rectangle clientRectangle)
    {
        return Rectangle.Inflate(
            GetInnerBounds(clientRectangle),
            -DesignTokens.Scale(10),
            -DesignTokens.Scale(10));
    }

    private static IReadOnlyList<(PointF Point, Color Color)> SampleMarkers(
        IReadOnlyList<(PointF Point, Color Color)> markers)
    {
        if (markers.Count <= MaxMarkerCount)
        {
            return markers;
        }

        var sampled = new List<(PointF Point, Color Color)>(MaxMarkerCount);
        for (int index = 0; index < MaxMarkerCount; index++)
        {
            int sourceIndex = (int)Math.Round(index * (markers.Count - 1) / (double)(MaxMarkerCount - 1));
            sampled.Add(markers[sourceIndex]);
        }

        return sampled;
    }

    private static int? NormalizeActiveSourceEventIndex(
        int? activeSourceEventIndex,
        int eventCount)
    {
        if (!activeSourceEventIndex.HasValue || eventCount <= 0)
        {
            return null;
        }

        return Math.Clamp(activeSourceEventIndex.Value, 0, eventCount - 1);
    }

    private static int ResolveCurrentMousePointIndex(
        IReadOnlyList<MapPoint> points,
        int? activeSourceEventIndex)
    {
        if (points.Count == 0)
        {
            return 0;
        }

        if (!activeSourceEventIndex.HasValue)
        {
            return points.Count - 1;
        }

        int currentIndex = 0;
        for (int index = 0; index < points.Count; index++)
        {
            if (points[index].SourceIndex > activeSourceEventIndex.Value)
            {
                break;
            }

            currentIndex = index;
        }

        return Math.Clamp(currentIndex, 0, points.Count - 1);
    }

    private static void DrawPoint(
        Graphics graphics,
        PointF point,
        Color color,
        int radius)
    {
        using var brush = new SolidBrush(color);
        using var borderPen = new Pen(Color.FromArgb(180, DesignTokens.TextPrimary), Math.Max(1f, DesignTokens.DensityScale));
        var bounds = new RectangleF(
            point.X - radius,
            point.Y - radius,
            radius * 2f,
            radius * 2f);
        graphics.FillEllipse(brush, bounds);
        graphics.DrawEllipse(borderPen, bounds);
    }

    private void DrawActivePointHalo(
        Graphics graphics,
        PointF point)
    {
        float pulse = (MathF.Sin(_pulsePhase) + 1f) / 2f;
        float outerRadius = DesignTokens.Scale(16) + DesignTokens.Scale(7) * pulse;
        float innerRadius = DesignTokens.Scale(10);

        using var outerBrush = new SolidBrush(Color.FromArgb(18 + (int)(pulse * 34), DesignTokens.Accent));
        using var innerBrush = new SolidBrush(Color.FromArgb(48, DesignTokens.Accent));
        graphics.FillEllipse(
            outerBrush,
            point.X - outerRadius,
            point.Y - outerRadius,
            outerRadius * 2f,
            outerRadius * 2f);
        graphics.FillEllipse(
            innerBrush,
            point.X - innerRadius,
            point.Y - innerRadius,
            innerRadius * 2f,
            innerRadius * 2f);
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

    private readonly record struct MapPoint(
        int SourceIndex,
        int X,
        int Y,
        MouseActionType ActionType,
        int DelayMs,
        string Description);
}
