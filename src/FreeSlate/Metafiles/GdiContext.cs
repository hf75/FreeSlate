using System.Text;
using FreeSlate.Text;

namespace FreeSlate.Metafiles;

public sealed class GdiPen
{
    public int Style { get; init; }
    public double Width { get; init; }
    public uint Color { get; init; }
    public bool Geometric { get; init; }
    public double[]? UserDashes { get; init; }
    public GdiBrush? Brush { get; init; }

    public int Kind => Style & 0x0F;
    public bool IsNull => Kind == 5;
}

public sealed class GdiBrush
{
    public int Style { get; init; }
    public uint Color { get; init; }
    public int Hatch { get; init; }
    public Bitmap? Pattern { get; init; }

    public bool IsNull => Style == 1;
}

public sealed class GdiFont
{
    public double Height { get; init; }
    public double Width { get; init; }
    public int Escapement { get; init; }
    public int Weight { get; init; } = 400;
    public bool Italic { get; init; }
    public bool Underline { get; init; }
    public bool StrikeOut { get; init; }
    public int CharSet { get; init; }
    public string Face { get; init; } = "Arial";
}

/// <summary>Graphics state saved and restored by SaveDC/RestoreDC.</summary>
public sealed class GdiState
{
    public GdiPen Pen { get; set; } = new() { Style = 0, Width = 0, Color = 0 };
    public GdiBrush Brush { get; set; } = new() { Style = 0, Color = 0xFFFFFF };
    public GdiFont Font { get; set; } = new() { Height = -12, Face = "Arial" };
    public uint TextColor { get; set; }
    public uint BkColor { get; set; } = 0xFFFFFF;
    public int BkMode { get; set; } = 2;
    public int PolyFillMode { get; set; } = 1;
    public int TextAlign { get; set; }
    public int MapMode { get; set; } = 1;
    public (double X, double Y) WindowOrg { get; set; }
    public (double X, double Y) WindowExt { get; set; } = (1, 1);
    public (double X, double Y) ViewportOrg { get; set; }
    public (double X, double Y) ViewportExt { get; set; } = (1, 1);
    public Xform World { get; set; } = Xform.Identity;
    public (double X, double Y) Position { get; set; }
    public Clip? Clip { get; set; }
    public Clip? MetaClip { get; set; }
    public double MiterLimit { get; set; } = 10;
    public bool Clockwise { get; set; }
    public int Rop2 { get; set; } = 13;
    public int TextCharExtra { get; set; }

    public GdiState Clone() => (GdiState)MemberwiseClone();
}

/// <summary>
/// The GDI device context shared by the EMF and WMF players: objects, the state stack, the logical-to-device
/// transform and the drawing primitives, written as SVG in device units.
/// </summary>
public sealed class GdiContext(SvgWriter svg, double pixelsPerMm)
{
    readonly Stack<GdiState> saved = new();
    PathBuilder? path;

    public GdiState State { get; private set; } = new();
    public SvgWriter Svg { get; } = svg;
    public double PixelsPerMm { get; } = pixelsPerMm;
    public Dictionary<int, object> Objects { get; } = [];

    /// <summary>Device units per output pixel; hatch and pattern brushes are sized in output pixels.</summary>
    public double UnitsPerPixel { get; init; } = 1;

    /// <summary>EMF+ records drew the current part; the duplicate GDI drawing of dual metafiles is skipped.</summary>
    public bool SuppressDrawing { get; set; }

    // ------------------------------------------------------------------ transforms

    /// <summary>Page (after world transform) to device: window/viewport mapping of the map mode.</summary>
    public Xform PageToDevice
    {
        get
        {
            var s = State;
            double sx, sy;
            switch (s.MapMode)
            {
                case 2: (sx, sy) = (PixelsPerMm / 10, -PixelsPerMm / 10); break;        // MM_LOMETRIC
                case 3: (sx, sy) = (PixelsPerMm / 100, -PixelsPerMm / 100); break;      // MM_HIMETRIC
                case 4: (sx, sy) = (PixelsPerMm * 0.254, -PixelsPerMm * 0.254); break;  // MM_LOENGLISH
                case 5: (sx, sy) = (PixelsPerMm * 0.0254, -PixelsPerMm * 0.0254); break; // MM_HIENGLISH
                case 6: (sx, sy) = (PixelsPerMm * 25.4 / 1440, -PixelsPerMm * 25.4 / 1440); break; // MM_TWIPS
                case 7:
                case 8:
                {
                    sx = s.WindowExt.X != 0 ? s.ViewportExt.X / s.WindowExt.X : 1;
                    sy = s.WindowExt.Y != 0 ? s.ViewportExt.Y / s.WindowExt.Y : 1;
                    if (s.MapMode == 7)
                    {
                        // Isotropic: equal scale with the smaller magnitude, signs kept.
                        var m = Math.Min(Math.Abs(sx), Math.Abs(sy));
                        sx = Math.Sign(sx) * m;
                        sy = Math.Sign(sy) * m;
                    }
                    break;
                }
                default: (sx, sy) = (1, 1); break;   // MM_TEXT
            }
            return new Xform(sx, 0, 0, sy, s.ViewportOrg.X - s.WindowOrg.X * sx, s.ViewportOrg.Y - s.WindowOrg.Y * sy);
        }
    }

    public Xform Total => State.World.Then(PageToDevice);

    public (double X, double Y) ToDevice(double x, double y) => Total.Apply(x, y);

    // ------------------------------------------------------------------ state

    public void Save() => saved.Push(State.Clone());

    public void Restore(int relative)
    {
        // RestoreDC(-1) restores the last saved state; positive values are absolute indexes.
        var count = relative < 0 ? -relative : saved.Count - relative + 1;
        for (var i = 0; i < count && saved.Count > 0; i++)
            State = saved.Pop();
    }

    public void SelectStock(int index)
    {
        switch (index)
        {
            case 0: State.Brush = new GdiBrush { Color = 0xFFFFFF }; break;
            case 1: State.Brush = new GdiBrush { Color = 0xC0C0C0 }; break;
            case 2: State.Brush = new GdiBrush { Color = 0x808080 }; break;
            case 3: State.Brush = new GdiBrush { Color = 0x404040 }; break;
            case 4: State.Brush = new GdiBrush { Color = 0 }; break;
            case 5: State.Brush = new GdiBrush { Style = 1 }; break;
            case 6: State.Pen = new GdiPen { Color = 0xFFFFFF }; break;
            case 7: State.Pen = new GdiPen { Color = 0 }; break;
            case 8: State.Pen = new GdiPen { Style = 5 }; break;
            case 10 or 11 or 12 or 13 or 14 or 16 or 17:
                State.Font = new GdiFont { Height = -12, Face = index is 10 or 11 or 16 ? "Courier New" : "Arial" };
                break;
        }
    }

    public void Select(object item)
    {
        switch (item)
        {
            case GdiPen pen: State.Pen = pen; break;
            case GdiBrush brush: State.Brush = brush; break;
            case GdiFont font: State.Font = font; break;
        }
    }

    // ------------------------------------------------------------------ clipping

    public void IntersectClip(string devicePath, bool evenOdd = false) =>
        State.Clip = new Clip { Parent = State.Clip, Path = devicePath, EvenOdd = evenOdd };

    public void IntersectClipRect(double l, double t, double r, double b) => IntersectClip(RectPath(l, t, r, b));

    const string Far = "M-10000000 -10000000H10000000V10000000H-10000000Z";

    /// <summary>
    /// Excludes a rectangle from the clip. Regions built from many excluded rectangles (Office writes hundreds in a
    /// row) merge into one even-odd path instead of nesting one clip per rectangle.
    /// </summary>
    public void ExcludeClipRect(double l, double t, double r, double b)
    {
        var rect = RectPath(l, t, r, b);
        State.Clip = State.Clip is { Exclusion: true, IsWritten: false } current
            ? new Clip { Parent = current.Parent, Path = current.Path + rect, EvenOdd = true, Exclusion = true }
            : new Clip { Parent = State.Clip, Path = Far + rect, EvenOdd = true, Exclusion = true };
    }

    /// <summary>Combines a device-space region with the clip (RGN_AND 1, OR 2, XOR 3, DIFF 4, COPY 5).</summary>
    public void SelectClip(string? devicePath, int mode)
    {
        var baseClip = State.MetaClip;
        switch (mode)
        {
            case 5:
                State.Clip = devicePath == null ? baseClip : new Clip { Parent = baseClip, Path = devicePath };
                break;
            case 1:
                if (devicePath != null) IntersectClip(devicePath);
                break;
            case 4:
                if (devicePath != null)
                    State.Clip = new Clip { Parent = State.Clip, Path = Far + devicePath, EvenOdd = true, Exclusion = true };
                break;
            default:
                // OR and XOR widen the clip; approximated by the union bounds (rarely used by Office).
                if (devicePath != null && State.Clip != null)
                    State.Clip = new Clip { Parent = baseClip, Path = State.Clip.Path + devicePath };
                break;
        }
    }

    public void SetMetaRegion()
    {
        State.MetaClip = State.Clip;
    }

    /// <summary>OffsetClipRgn: the clipping region (not the meta region) moves by a logical offset.</summary>
    public void OffsetClip(double dx, double dy)
    {
        var (ox, oy) = ToDevice(0, 0);
        var (ex, ey) = ToDevice(dx, dy);
        Clip? Moved(Clip? clip) => clip == null || ReferenceEquals(clip, State.MetaClip)
            ? clip
            : new Clip { Parent = Moved(clip.Parent), Path = clip.Path, EvenOdd = clip.EvenOdd, Exclusion = clip.Exclusion, Shift = (clip.Shift.X + ex - ox, clip.Shift.Y + ey - oy) };
        State.Clip = Moved(State.Clip);
    }

    /// <summary>A logical rectangle as device path data (it may be rotated by the transform).</summary>
    public string RectPath(double l, double t, double r, double b)
    {
        var p = new PathBuilder();
        p.MoveTo(ToDevice(l, t));
        p.LineTo(ToDevice(r, t));
        p.LineTo(ToDevice(r, b));
        p.LineTo(ToDevice(l, b));
        p.Close();
        return p.ToString();
    }

    // ------------------------------------------------------------------ paths

    public bool InPath => path != null;

    public void BeginPath() => path = new PathBuilder();

    public void EndPath() { }

    public void AbortPath() => path = null;

    public string? TakePath()
    {
        var d = path?.ToString();
        path = null;
        return string.IsNullOrEmpty(d) ? null : d;
    }

    /// <summary>The path being recorded, or a fresh builder for immediate drawing.</summary>
    PathBuilder Target() => path ?? new PathBuilder();

    // ------------------------------------------------------------------ primitives (logical coordinates)

    public void Polyline(IReadOnlyList<(double X, double Y)> points, bool closed, bool fill)
    {
        if (points.Count == 0)
            return;
        var p = Target();
        p.MoveTo(ToDevice(points[0].X, points[0].Y));
        for (var i = 1; i < points.Count; i++)
            p.LineTo(ToDevice(points[i].X, points[i].Y));
        if (closed) p.Close();
        if (!InPath) Draw(p.ToString(), fill, stroke: true);
    }

    public void PolyPolygon(IEnumerable<IReadOnlyList<(double X, double Y)>> polygons, bool closed, bool fill)
    {
        var p = Target();
        foreach (var points in polygons)
        {
            if (points.Count == 0) continue;
            p.MoveTo(ToDevice(points[0].X, points[0].Y));
            for (var i = 1; i < points.Count; i++)
                p.LineTo(ToDevice(points[i].X, points[i].Y));
            if (closed) p.Close();
        }
        if (!InPath) Draw(p.ToString(), fill, stroke: true);
    }

    /// <summary>Bézier curves: start point plus groups of three points (or, with <paramref name="fromCurrent"/>, from the current position).</summary>
    public void PolyBezier(IReadOnlyList<(double X, double Y)> points, bool fromCurrent)
    {
        var p = Target();
        var start = 0;
        if (fromCurrent)
        {
            if (!InPath) p.MoveTo(ToDevice(State.Position.X, State.Position.Y));
        }
        else
        {
            if (points.Count == 0) return;
            p.MoveTo(ToDevice(points[0].X, points[0].Y));
            start = 1;
        }
        for (var i = start; i + 2 < points.Count; i += 3)
            p.CurveTo(ToDevice(points[i].X, points[i].Y), ToDevice(points[i + 1].X, points[i + 1].Y), ToDevice(points[i + 2].X, points[i + 2].Y));
        if (points.Count > 0) State.Position = points[^1];
        if (!InPath) Draw(p.ToString(), fill: false, stroke: true);
    }

    public void LineTo(double x, double y, bool asFigureStart = false)
    {
        if (InPath)
        {
            if (path!.IsEmpty || asFigureStart) path.MoveTo(ToDevice(State.Position.X, State.Position.Y));
            path.LineTo(ToDevice(x, y));
        }
        else
        {
            var p = new PathBuilder();
            p.MoveTo(ToDevice(State.Position.X, State.Position.Y));
            p.LineTo(ToDevice(x, y));
            Draw(p.ToString(), fill: false, stroke: true);
        }
        State.Position = (x, y);
    }

    public void MoveTo(double x, double y)
    {
        State.Position = (x, y);
        path?.MoveTo(ToDevice(x, y));
    }

    public void CloseFigure() => path?.Close();

    public void Rectangle(double l, double t, double r, double b)
    {
        var p = Target();
        p.Append(RectPath(Math.Min(l, r), Math.Min(t, b), Math.Max(l, r), Math.Max(t, b)));
        if (!InPath) Draw(p.ToString(), fill: true, stroke: true);
    }

    public void RoundRect(double l, double t, double r, double b, double w, double h)
    {
        (l, r) = (Math.Min(l, r), Math.Max(l, r));
        (t, b) = (Math.Min(t, b), Math.Max(t, b));
        var rx = Math.Min(Math.Abs(w) / 2, (r - l) / 2);
        var ry = Math.Min(Math.Abs(h) / 2, (b - t) / 2);
        var p = Target();
        p.MoveTo(ToDevice(l + rx, t));
        p.LineTo(ToDevice(r - rx, t));
        AddArc(p, r - rx, t + ry, rx, ry, -Math.PI / 2, Math.PI / 2);
        p.LineTo(ToDevice(r, b - ry));
        AddArc(p, r - rx, b - ry, rx, ry, 0, Math.PI / 2);
        p.LineTo(ToDevice(l + rx, b));
        AddArc(p, l + rx, b - ry, rx, ry, Math.PI / 2, Math.PI / 2);
        p.LineTo(ToDevice(l, t + ry));
        AddArc(p, l + rx, t + ry, rx, ry, Math.PI, Math.PI / 2);
        p.Close();
        if (!InPath) Draw(p.ToString(), fill: true, stroke: true);
    }

    public void Ellipse(double l, double t, double r, double b)
    {
        var (cx, cy, rx, ry) = ((l + r) / 2, (t + b) / 2, Math.Abs(r - l) / 2, Math.Abs(b - t) / 2);
        var p = Target();
        p.MoveTo(ToDevice(cx + rx, cy));
        AddArc(p, cx, cy, rx, ry, 0, 2 * Math.PI);
        p.Close();
        if (!InPath) Draw(p.ToString(), fill: true, stroke: true);
    }

    /// <summary>Arc (open), chord (closed by a line) or pie (closed through the center) between two radial points.</summary>
    public void Arc(double l, double t, double r, double b, (double X, double Y) start, (double X, double Y) end, string kind, bool toCurrent = false)
    {
        var (cx, cy, rx, ry) = ((l + r) / 2, (t + b) / 2, Math.Abs(r - l) / 2, Math.Abs(b - t) / 2);
        if (rx <= 0 || ry <= 0)
            return;
        // GDI arcs run counterclockwise (in logical space with y down) unless the arc direction is clockwise.
        var a0 = Math.Atan2((start.Y - cy) / ry, (start.X - cx) / rx);
        var a1 = Math.Atan2((end.Y - cy) / ry, (end.X - cx) / rx);
        var sweep = State.Clockwise ? a1 - a0 : -(a0 - a1);
        if (State.Clockwise) { while (sweep <= 0) sweep += 2 * Math.PI; }
        else { sweep = a1 - a0; while (sweep >= 0) sweep -= 2 * Math.PI; }
        if (Math.Abs(a0 - a1) < 1e-9) sweep = State.Clockwise ? 2 * Math.PI : -2 * Math.PI;

        var p = Target();
        var first = (cx + rx * Math.Cos(a0), cy + ry * Math.Sin(a0));
        if (kind == "pie")
        {
            p.MoveTo(ToDevice(cx, cy));
            p.LineTo(ToDevice(first.Item1, first.Item2));
        }
        else if (toCurrent)
        {
            p.MoveTo(ToDevice(State.Position.X, State.Position.Y));
            p.LineTo(ToDevice(first.Item1, first.Item2));
        }
        else
        {
            p.MoveTo(ToDevice(first.Item1, first.Item2));
        }
        AddArc(p, cx, cy, rx, ry, a0, sweep);
        if (kind is "pie" or "chord") p.Close();
        if (toCurrent) State.Position = (cx + rx * Math.Cos(a0 + sweep), cy + ry * Math.Sin(a0 + sweep));
        if (!InPath) Draw(p.ToString(), fill: kind != "arc", stroke: true);
    }

    void AddArc(PathBuilder p, double cx, double cy, double rx, double ry, double start, double sweep)
    {
        foreach (var (c1, c2, end) in PathBuilder.ArcBeziers(cx, cy, rx, ry, start, sweep))
            p.CurveTo(ToDevice(c1.X, c1.Y), ToDevice(c2.X, c2.Y), ToDevice(end.X, end.Y));
    }

    // ------------------------------------------------------------------ painting

    /// <summary>Draws device path data with the current brush and/or pen.</summary>
    public void Draw(string d, bool fill, bool stroke)
    {
        if (SuppressDrawing || string.IsNullOrEmpty(d) || State.Rop2 == 11)
            return;
        var fillAttributes = fill ? FillPaint(State.Brush) : " fill=\"none\"";
        var strokeAttributes = stroke ? StrokePaint(State.Pen) : "";
        if (fillAttributes == " fill=\"none\"" && strokeAttributes.Length == 0)
            return;
        var rule = fill && State.PolyFillMode == 1 ? " fill-rule=\"evenodd\"" : "";
        Svg.Add($"<path d=\"{d}\"{fillAttributes}{rule}{strokeAttributes}/>", State.Clip);
    }

    public string FillPaint(GdiBrush brush)
    {
        if (brush.IsNull)
            return " fill=\"none\"";
        switch (brush.Style)
        {
            case 2:
                return $" fill=\"url(#{HatchPattern(brush)})\"";
            case 3 or 5 or 6 when brush.Pattern != null:
            {
                var id = Svg.NewId();
                var b = brush.Pattern;
                var scale = SvgWriter.Num(UnitsPerPixel);
                Svg.Def($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" width=\"{b.Width}\" height=\"{b.Height}\" patternTransform=\"scale({scale})\"><image width=\"{b.Width}\" height=\"{b.Height}\" href=\"{b.DataUri()}\" image-rendering=\"pixelated\"/></pattern>");
                return $" fill=\"url(#{id})\"";
            }
            default:
                return $" fill=\"{SvgWriter.Color(brush.Color)}\"";
        }
    }

    string HatchPattern(GdiBrush brush)
    {
        var id = Svg.NewId();
        static (byte, byte, byte, byte) Rgba(uint colorRef) => ((byte)(colorRef & 0xFF), (byte)((colorRef >> 8) & 0xFF), (byte)((colorRef >> 16) & 0xFF), 255);
        // The hatch's pixels in its 8 × 8 tile (HS_HORIZONTAL … HS_DIAGCROSS); the background shows when opaque.
        bool Set(int x, int y) => brush.Hatch switch
        {
            0 => y == 3,
            1 => x == 3,
            2 => x == y,
            3 => x == 7 - y,
            4 => x == 3 || y == 3,
            _ => x == y || x == 7 - y,
        };
        var tile = SvgWriter.HatchTile(Set, Rgba(brush.Color), State.BkMode == 2 ? Rgba(State.BkColor) : null);
        // Hatches are 8 × 8 pixels of the output device, not of the reference device.
        var scale = SvgWriter.Num(UnitsPerPixel);
        Svg.Def($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" width=\"8\" height=\"8\" patternTransform=\"scale({scale})\">{tile}</pattern>");
        return id;
    }

    public string StrokePaint(GdiPen pen)
    {
        if (pen.IsNull)
            return "";
        var scale = Total.AverageScale;
        var width = pen.Geometric || pen.Width > 1 ? pen.Width * scale : 0;
        var color = pen.Brush is { Style: 0 } b ? b.Color : pen.Color;
        var sb = new StringBuilder($" stroke=\"{SvgWriter.Color(color)}\"");
        if (width < 1)
            sb.Append(" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\"");
        else
            sb.Append($" stroke-width=\"{SvgWriter.Num(width)}\"");
        if (pen.Geometric)
        {
            sb.Append((pen.Style & 0xF00) switch { 0x100 => " stroke-linecap=\"square\"", 0x200 => "", _ => " stroke-linecap=\"round\"" });
            sb.Append((pen.Style & 0xF000) switch { 0x1000 => " stroke-linejoin=\"bevel\"", 0x2000 => $" stroke-miterlimit=\"{SvgWriter.Num(Math.Max(1, State.MiterLimit))}\"", _ => " stroke-linejoin=\"round\"" });
        }
        var unit = Math.Max(width, 1);
        double[]? dashes = pen.Kind switch
        {
            1 => [3 * unit, unit],
            2 => [unit, unit],
            3 => [3 * unit, unit, unit, unit],
            4 => [3 * unit, unit, unit, unit, unit, unit],
            7 when pen.UserDashes is { Length: > 0 } user => user.Select(d => d * scale).ToArray(),
            _ => null,
        };
        if (dashes != null)
            sb.Append($" stroke-dasharray=\"{string.Join(' ', dashes.Select(SvgWriter.Num))}\"");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ text

    /// <summary>
    /// Draws a string at a logical reference point with optional advances (logical units), honouring the text
    /// alignment, the font's escapement and the background mode.
    /// </summary>
    public void Text(double x, double y, string text, double[]? advances, (double L, double T, double R, double B)? opaqueRect, (double L, double T, double R, double B)? clipRect)
    {
        if (SuppressDrawing)
            return;
        var s = State;
        var font = s.Font;
        if ((s.TextAlign & 1) != 0)
            (x, y) = s.Position;

        var resolved = FontResolver.Default.Resolve(font.Face, font.Weight >= 600, font.Italic);
        var data = resolved.Data;
        var winSum = data.WinAscent + data.WinDescent;
        var emLogical = font.Height < 0 ? -font.Height : font.Height > 0 ? font.Height / (winSum > 0 ? winSum : 1.2) : 12;
        var total = Total;

        if (opaqueRect is { } o)
        {
            Svg.Add($"<path d=\"{RectPath(o.L, o.T, o.R, o.B)}\" fill=\"{SvgWriter.Color(s.BkColor)}\"/>", s.Clip);
        }

        // Advances in logical units: given, else measured with the font (plus character extra).
        var count = text.Length;
        var widths = new double[count];
        if (advances != null && advances.Length >= count)
        {
            Array.Copy(advances, widths, count);
        }
        else
        {
            var measured = data.Advances(text, kerning: false);
            for (var i = 0; i < count; i++) widths[i] = measured[i] * emLogical + s.TextCharExtra;
        }
        var totalWidth = widths.Sum();

        // Device geometry: the world transform's rotation plus the escapement (counterclockwise); GDI keeps text
        // upright even when the map mode flips the y axis.
        var (vx, vy) = total.ApplyVector(1, 0);
        var (ux, uy) = total.ApplyVector(0, 1);
        var scaleX = Math.Sqrt(vx * vx + vy * vy);
        var scaleY = Math.Sqrt(ux * ux + uy * uy);
        if (scaleX <= 0 || scaleY <= 0)
            return;
        var angle = Math.Atan2(vy, vx) * 180 / Math.PI - font.Escapement / 10.0;
        var (sin, cos) = Math.SinCos(angle * Math.PI / 180);
        var (dirX, dirY) = (cos, sin);
        var (upX, upY) = (sin, -cos);
        var size = emLogical * scaleY;

        // Reference point to baseline start: horizontal alignment along the baseline, vertical across it.
        var alongOffset = (s.TextAlign & 6) switch { 2 => -totalWidth, 6 => -totalWidth / 2, _ => 0 } * scaleX;
        var belowOffset = (s.TextAlign & 24) switch { 24 => 0, 8 => -data.WinDescent * emLogical, _ => data.WinAscent * emLogical } * scaleY;
        var (rx, ry) = total.Apply(x, y);
        var px = rx + dirX * alongOffset - upX * belowOffset;
        var py = ry + dirY * alongOffset - upY * belowOffset;

        var xs = new StringBuilder();
        double cursor = 0;
        for (var i = 0; i < count; i++)
        {
            if (!char.IsLowSurrogate(text[i]))
            {
                if (xs.Length > 0) xs.Append(' ');
                xs.Append(SvgWriter.Num(cursor));
            }
            cursor += widths[i] * scaleX;
        }

        var transform = Math.Abs(angle) > 0.01
            ? $" transform=\"translate({SvgWriter.Num(px)} {SvgWriter.Num(py)}) rotate({SvgWriter.Num(angle)})\""
            : $" transform=\"translate({SvgWriter.Num(px)} {SvgWriter.Num(py)})\"";
        var decoration = ((font.Underline ? " underline" : "") + (font.StrikeOut ? " line-through" : "")).Trim();
        var element = $"<text x=\"{xs}\" y=\"0\"{transform} font-family=\"{SvgWriter.Escape(Family(font.Face))}\" font-size=\"{SvgWriter.Num(size)}\"" +
                      (font.Weight >= 600 ? " font-weight=\"bold\"" : "") + (font.Italic ? " font-style=\"italic\"" : "") +
                      (decoration.Length > 0 ? $" text-decoration=\"{decoration}\"" : "") +
                      $" fill=\"{SvgWriter.Color(s.TextColor)}\" xml:space=\"preserve\">{SvgWriter.Escape(text)}</text>";
        var clip = clipRect is { } r ? new Clip { Parent = s.Clip, Path = RectPath(r.L, r.T, r.R, r.B) } : s.Clip;
        Svg.Add(element, clip);
        if ((s.TextAlign & 1) != 0)
        {
            var (bx, by) = total.Inverse() is { } inverse ? inverse.ApplyVector(dirX * totalWidth * scaleX, dirY * totalWidth * scaleX) : (totalWidth, 0);
            s.Position = (x + bx, y + by);
        }
    }

    /// <summary>A CSS font family list with generic fallbacks for well-known Windows faces.</summary>
    public static string Family(string face)
    {
        var generic = face.Contains("Courier", StringComparison.OrdinalIgnoreCase) || face.Contains("Consolas", StringComparison.OrdinalIgnoreCase) ? "monospace"
            : face.Contains("Times", StringComparison.OrdinalIgnoreCase) || face.Contains("Georgia", StringComparison.OrdinalIgnoreCase) || face.Contains("Cambria", StringComparison.OrdinalIgnoreCase) ? "serif"
            : "sans-serif";
        return $"'{face.Replace("'", "")}', {generic}";
    }

    // ------------------------------------------------------------------ images

    /// <summary>Draws a bitmap into a logical destination rectangle (a source rectangle selects part of it).</summary>
    public void Image(Bitmap bitmap, double x, double y, double w, double h, (int X, int Y, int W, int H)? source = null)
    {
        if (SuppressDrawing || w == 0 || h == 0)
            return;
        var image = bitmap;
        if (source is { } src && bitmap.Rgba != null && (src.X != 0 || src.Y != 0 || src.W != bitmap.Width || src.H != bitmap.Height))
            image = Crop(bitmap, src);
        var t = Xform.Translate(x, y).Then(Total);
        var flipX = w < 0;
        var flipY = h < 0;
        var local = Xform.Scale(flipX ? -1 : 1, flipY ? -1 : 1).Then(Xform.Translate(flipX ? -w : 0, flipY ? -h : 0));
        var matrix = local.Then(t);
        Svg.Add($"<image width=\"{SvgWriter.Num(Math.Abs(w))}\" height=\"{SvgWriter.Num(Math.Abs(h))}\" transform=\"{matrix.Svg()}\" preserveAspectRatio=\"none\" href=\"{image.DataUri()}\"/>", State.Clip);
    }

    /// <summary>
    /// A bitmap mapped onto a parallelogram (PlgBlt): the source rectangle's upper-left, upper-right and lower-left
    /// corners go to <paramref name="a"/>, <paramref name="b"/> and <paramref name="c"/> (logical units).
    /// </summary>
    public void Image(Bitmap bitmap, (double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (int X, int Y, int W, int H) source)
    {
        if (SuppressDrawing || source.W == 0 || source.H == 0)
            return;
        var image = bitmap.Rgba != null && (source.X != 0 || source.Y != 0 || source.W != bitmap.Width || source.H != bitmap.Height) ? Crop(bitmap, source) : bitmap;
        var (w, h) = ((double)Math.Abs(source.W), (double)Math.Abs(source.H));
        var map = new Xform((b.X - a.X) / w, (b.Y - a.Y) / w, (c.X - a.X) / h, (c.Y - a.Y) / h, a.X, a.Y).Then(Total);
        Svg.Add($"<image width=\"{SvgWriter.Num(w)}\" height=\"{SvgWriter.Num(h)}\" transform=\"{map.Svg()}\" preserveAspectRatio=\"none\" href=\"{image.DataUri()}\"/>", State.Clip);
    }

    static Bitmap Crop(Bitmap bitmap, (int X, int Y, int W, int H) src)
    {
        var x0 = Math.Clamp(src.X, 0, bitmap.Width);
        var y0 = Math.Clamp(src.Y, 0, bitmap.Height);
        var w = Math.Clamp(src.W, 1, bitmap.Width - x0);
        var h = Math.Clamp(src.H, 1, bitmap.Height - y0);
        if (w <= 0 || h <= 0)
            return bitmap;
        var rgba = new byte[w * h * 4];
        for (var row = 0; row < h; row++)
            Array.Copy(bitmap.Rgba!, ((y0 + row) * bitmap.Width + x0) * 4, rgba, row * w * 4, w * 4);
        return new Bitmap { Width = w, Height = h, Rgba = rgba };
    }
}
