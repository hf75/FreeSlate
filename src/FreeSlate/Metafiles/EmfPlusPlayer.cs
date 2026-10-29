using System.Buffers.Binary;
using System.Text;
using FreeSlate.Drawing;

namespace FreeSlate.Metafiles;

/// <summary>
/// Plays EMF+ records ([MS-EMFPLUS]) embedded in EMF comment records: objects (brushes, pens, paths, regions,
/// images, fonts), drawing records, world/page transforms, containers and clipping.
/// </summary>
public sealed class EmfPlusPlayer(byte[] data, GdiContext gdi, SvgWriter svg)
{
    /// <summary>A custom line cap: fill and line paths in units of the pen width, the y axis along the line.</summary>
    sealed class Cap
    {
        public PlusPath? Fill;
        public PlusPath? Line;
        public double WidthScale = 1;
    }

    sealed class Pen
    {
        public Cap? StartCustom, EndCustom;
        public double Width;
        public int Unit;
        public Brush Brush = new() { Color = 0xFF000000 };
        public int StartCap, EndCap, Join;
        public double MiterLimit = 10;
        public int DashStyle;
        public int DashCap;
        public double[]? Dashes;
        public double DashOffset;
    }

    sealed class Brush
    {
        public int Type;
        public uint Color;
        public uint Color2;
        public int Hatch;
        public (double X, double Y, double W, double H) Rect;
        public Xform Transform = Xform.Identity;
        public List<(double Position, uint Color)>? Stops;
        public int Wrap;
        public bool Gamma;
        public (double X, double Y) Center;
        public string? BoundaryPath;
        public string? Texture;
        public (int W, int H) TextureSize;
    }

    sealed class PlusPath
    {
        public required List<(double X, double Y)> Points;
        public required byte[] Types;
    }

    sealed class PlusFont
    {
        public double EmSize;
        public int Unit;
        public int Style;
        public string Family = "Arial";
    }

    sealed class PlusImage
    {
        public string? Uri;
        public double Width, Height;
    }

    sealed class StringFormat
    {
        public int Flags;
        public int Alignment;
        public int LineAlign;

        /// <summary>Space before and after each line in em (GenericDefault: 1/6).</summary>
        public double LeadingMargin = 1.0 / 6;
        public double TrailingMargin = 1.0 / 6;
    }

    sealed class SavedState
    {
        public Xform World;
        public int PageUnit;
        public double PageScale;
        public Clip? Clip;
    }

    /// <summary>Whether the picture is a Word document's, whose hatches keep their bits (see BrushPaint).</summary>
    public bool Word { get; init; }

    readonly Dictionary<int, object> objects = [];
    readonly Dictionary<int, SavedState> stack = [];
    readonly Dictionary<int, (int Type, List<byte> Bytes, int Total)> continued = [];
    Xform world = Xform.Identity;
    (int X, int Y) renderingOrigin;
    int pageUnit = 1;
    double pageScale = 1;
    double dpiX = 96, dpiY = 96;
    bool videoReference = true;
    bool dual;
    Clip? clip;

    ushort U16(int o) => o + 2 <= data.Length ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(o)) : (ushort)0;
    short I16(int o) => o + 2 <= data.Length ? BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(o)) : (short)0;
    int I32(int o) => o + 4 <= data.Length ? BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(o)) : 0;
    uint U32(int o) => o + 4 <= data.Length ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(o)) : 0;
    float F32(int o) => o + 4 <= data.Length ? BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(o)) : 0;

    static readonly HashSet<int> Drawing =
    [
        0x4009, 0x400A, 0x400B, 0x400C, 0x400D, 0x400E, 0x400F, 0x4010, 0x4011, 0x4012, 0x4013, 0x4014, 0x4015, 0x4016, 0x4017,
        0x4018, 0x4019, 0x401A, 0x401B, 0x401C, 0x4036, 0x4037,
    ];

    public void Play(int offset, int length)
    {
        var end = Math.Min(data.Length, offset + length);
        var o = offset;
        while (o + 12 <= end)
        {
            var type = U16(o);
            var flags = U16(o + 2);
            var size = (int)U32(o + 4);
            var dataSize = (int)U32(o + 8);
            if (size < 12 || o + size > end)
                break;
            try
            {
                Record(type, flags, o + 12, Math.Min(dataSize, size - 12));
            }
            catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException or OverflowException or InvalidOperationException)
            {
            }
            if (dual)
                gdi.SuppressDrawing = Drawing.Contains(type);
            if (type == 0x4004)
                gdi.SuppressDrawing = false;   // GetDC: the following GDI records are part of the picture
            o += size;
        }
    }

    // ------------------------------------------------------------------ transforms

    double UnitFactor(int unit, bool horizontal) => unit switch
    {
        0 => 1,                                            // World
        1 => videoReference ? 1 : (horizontal ? dpiX : dpiY) / 100,   // Display
        2 => 1,                                            // Pixel
        3 => (horizontal ? dpiX : dpiY) / 72,              // Point
        4 => horizontal ? dpiX : dpiY,                     // Inch
        5 => (horizontal ? dpiX : dpiY) / 300,             // Document
        6 => (horizontal ? dpiX : dpiY) / 25.4,            // Millimeter
        _ => 1,
    };

    Xform Page => Xform.Scale(pageScale * UnitFactor(pageUnit, true), pageScale * UnitFactor(pageUnit, false));

    Xform Total => world.Then(Page);

    (double X, double Y) Device(double x, double y) => Total.Apply(x, y);

    // ------------------------------------------------------------------ records

    void Record(int type, int flags, int o, int size)
    {
        switch (type)
        {
            case 0x4001:   // Header
                dual = (flags & 1) != 0;
                videoReference = (U32(o + 4) & 1) != 0;
                dpiX = Math.Max(1, U32(o + 8));
                dpiY = Math.Max(1, U32(o + 12));
                break;
            case 0x4008:
                Object(flags, o, size);
                break;
            case 0x4009:   // Clear
                svg.Add($"<rect x=\"-1e6\" y=\"-1e6\" width=\"2e6\" height=\"2e6\" fill=\"{Rgb(U32(o))}\"{Opacity(U32(o), "fill")}/>", clip);
                break;
            case 0x400A:   // FillRects
            {
                var count = I32(o + 4);
                var compressed = (flags & 0x4000) != 0;
                var p = new PathBuilder();
                for (var i = 0; i < count && i < 100000; i++)
                    AppendRect(p, Rect(o + 8, i, compressed));
                Fill(p.ToString(), (flags & 0x8000) != 0, U32(o), evenOdd: false);
                break;
            }
            case 0x400B:   // DrawRects
            {
                var count = I32(o);
                var compressed = (flags & 0x4000) != 0;
                var p = new PathBuilder();
                for (var i = 0; i < count && i < 100000; i++)
                    AppendRect(p, Rect(o + 4, i, compressed));
                Stroke(p.ToString(), flags & 0xFF);
                break;
            }
            case 0x400C:   // FillPolygon
            {
                var points = Points(o + 8, I32(o + 4), flags);
                Fill(PolygonData(points, close: true), (flags & 0x8000) != 0, U32(o), evenOdd: true);
                break;
            }
            case 0x400D:   // DrawLines
            {
                var points = Points(o + 4, I32(o), flags);
                Stroke(PolygonData(points, close: (flags & 0x2000) != 0), flags & 0xFF);
                if ((flags & 0x2000) == 0) Caps(points, flags & 0xFF);
                break;
            }
            case 0x400E:   // FillEllipse
            {
                var r = Rect(o + 4, 0, (flags & 0x4000) != 0);
                Fill(EllipseData(r), (flags & 0x8000) != 0, U32(o), evenOdd: false);
                break;
            }
            case 0x400F:   // DrawEllipse
                Stroke(EllipseData(Rect(o, 0, (flags & 0x4000) != 0)), flags & 0xFF);
                break;
            case 0x4010:   // FillPie
            {
                var r = Rect(o + 12, 0, (flags & 0x4000) != 0);
                Fill(ArcData(r, F32(o + 4), F32(o + 8), pie: true), (flags & 0x8000) != 0, U32(o), evenOdd: false);
                break;
            }
            case 0x4011 or 0x4012:   // DrawPie, DrawArc
            {
                var r = Rect(o + 8, 0, (flags & 0x4000) != 0);
                Stroke(ArcData(r, F32(o), F32(o + 4), pie: type == 0x4011), flags & 0xFF);
                break;
            }
            case 0x4013:   // FillRegion
                if (objects.GetValueOrDefault(flags & 0xFF) is string region)
                    Fill(region, (flags & 0x8000) != 0, U32(o), evenOdd: false, devicePath: true);
                break;
            case 0x4014:   // FillPath
                if (objects.GetValueOrDefault(flags & 0xFF) is PlusPath fillPath)
                    Fill(PathData(fillPath), (flags & 0x8000) != 0, U32(o), evenOdd: true);
                break;
            case 0x4015:   // DrawPath
                if (objects.GetValueOrDefault(flags & 0xFF) is PlusPath drawPath)
                {
                    Stroke(PathData(drawPath), (int)U32(o), Open(drawPath));
                    if (Open(drawPath)) Caps(drawPath.Points, (int)U32(o));
                }
                break;
            case 0x4016:   // FillClosedCurve
            {
                var points = Points(o + 12, I32(o + 8), flags);
                Fill(CurveData(points, F32(o + 4), closed: true), (flags & 0x8000) != 0, U32(o), evenOdd: (flags & 0x2000) == 0);
                break;
            }
            case 0x4017:   // DrawClosedCurve
            {
                var points = Points(o + 8, I32(o + 4), flags);
                Stroke(CurveData(points, F32(o), closed: true), flags & 0xFF);
                break;
            }
            case 0x4018:   // DrawCurve
            {
                var points = Points(o + 16, I32(o + 12), flags);
                var offset = I32(o + 4);
                var segments = I32(o + 8);
                var used = points.Skip(offset).Take(segments + 1).ToList();
                Stroke(CurveData(used.Count >= 2 ? used : points, F32(o), closed: false), flags & 0xFF);
                break;
            }
            case 0x4019:   // DrawBeziers
            {
                var points = Points(o + 4, I32(o), flags);
                if (points.Count >= 4)
                {
                    var p = new PathBuilder();
                    p.MoveTo(Device(points[0].X, points[0].Y));
                    for (var i = 1; i + 2 < points.Count; i += 3)
                        p.CurveTo(Device(points[i].X, points[i].Y), Device(points[i + 1].X, points[i + 1].Y), Device(points[i + 2].X, points[i + 2].Y));
                    Stroke(p.ToString(), flags & 0xFF);
                }
                break;
            }
            case 0x401A:   // DrawImage
            {
                var src = (X: F32(o + 8), Y: F32(o + 12), W: F32(o + 16), H: F32(o + 20));
                var dest = Rect(o + 24, 0, (flags & 0x4000) != 0);
                if (objects.GetValueOrDefault(flags & 0xFF) is PlusImage image)
                    DrawImage(image, src, (dest.X, dest.Y), (dest.X + dest.W, dest.Y), (dest.X, dest.Y + dest.H));
                break;
            }
            case 0x401B:   // DrawImagePoints
            {
                var src = (X: F32(o + 8), Y: F32(o + 12), W: F32(o + 16), H: F32(o + 20));
                var points = Points(o + 28, I32(o + 24), flags);
                if (points.Count >= 3 && objects.GetValueOrDefault(flags & 0xFF) is PlusImage image)
                    DrawImage(image, src, points[0], points[1], points[2]);
                break;
            }
            case 0x401C:   // DrawString
                DrawString(flags, o);
                break;
            case 0x401D:   // SetRenderingOrigin
                // Where hatch brushes start, in output pixels (measured: local/hatchprobe in PowerPoint, a diagonal cross and
                // a 30 % hatch with the origin at (3, 5) moved by 3 and 5 pixels).
                renderingOrigin = (I32(o), I32(o + 4));
                break;
            case 0x4025:   // Save
                stack[I32(o)] = new SavedState { World = world, PageUnit = pageUnit, PageScale = pageScale, Clip = clip };
                break;
            case 0x4026 or 0x4029:   // Restore, EndContainer
                if (stack.TryGetValue(I32(o), out var state))
                {
                    (world, pageUnit, pageScale, clip) = (state.World, state.PageUnit, state.PageScale, state.Clip);
                    stack.Remove(I32(o));
                }
                break;
            case 0x4027:   // BeginContainer: maps the source rectangle to the destination rectangle
            {
                var dest = (X: F32(o), Y: F32(o + 4), W: F32(o + 8), H: F32(o + 12));
                var src = (X: F32(o + 16), Y: F32(o + 20), W: F32(o + 24), H: F32(o + 28));
                stack[I32(o + 32)] = new SavedState { World = world, PageUnit = pageUnit, PageScale = pageScale, Clip = clip };
                var unit = UnitFactor(flags >> 8 & 0xFF, true) / UnitFactor(pageUnit, true);
                var sx = src.W != 0 ? dest.W / src.W : 1;
                var sy = src.H != 0 ? dest.H / src.H : 1;
                var container = Xform.Translate(-src.X, -src.Y).Then(Xform.Scale(sx * unit, sy * unit)).Then(Xform.Translate(dest.X, dest.Y));
                world = container.Then(world);
                break;
            }
            case 0x4028:   // BeginContainerNoParams
                stack[I32(o)] = new SavedState { World = world, PageUnit = pageUnit, PageScale = pageScale, Clip = clip };
                break;
            case 0x402A: world = Matrix(o); break;
            case 0x402B: world = Xform.Identity; break;
            case 0x402C: world = (flags & 0x2000) != 0 ? world.Then(Matrix(o)) : Matrix(o).Then(world); break;
            case 0x402D:
            {
                var t = Xform.Translate(F32(o), F32(o + 4));
                world = (flags & 0x2000) != 0 ? world.Then(t) : t.Then(world);
                break;
            }
            case 0x402E:
            {
                var t = Xform.Scale(F32(o), F32(o + 4));
                world = (flags & 0x2000) != 0 ? world.Then(t) : t.Then(world);
                break;
            }
            case 0x402F:
            {
                var t = Xform.Rotate(F32(o));
                world = (flags & 0x2000) != 0 ? world.Then(t) : t.Then(world);
                break;
            }
            case 0x4030:   // SetPageTransform
                pageUnit = flags & 0xFF;
                pageScale = F32(o);
                if (pageScale <= 0 || double.IsNaN(pageScale)) pageScale = 1;
                break;
            case 0x4031: clip = null; break;
            case 0x4032:   // SetClipRect
            {
                var r = (X: F32(o), Y: F32(o + 4), W: F32(o + 8), H: F32(o + 12));
                var p = new PathBuilder();
                AppendRect(p, r);
                CombineClip(p.ToString(), (flags >> 8) & 0xF);
                break;
            }
            case 0x4033:   // SetClipPath (paths fill with the alternate rule)
                if (objects.GetValueOrDefault(flags & 0xFF) is PlusPath clipPath)
                    CombineClip(PathData(clipPath), (flags >> 8) & 0xF, evenOdd: true);
                break;
            case 0x4034:   // SetClipRegion
                if (objects.GetValueOrDefault(flags & 0xFF) is string clipRegion)
                    CombineClip(clipRegion, (flags >> 8) & 0xF, devicePath: true);
                break;
            case 0x4036:   // DrawDriverString
                DrawDriverString(flags, o);
                break;
        }
    }

    // ------------------------------------------------------------------ objects

    void Object(int flags, int o, int size)
    {
        var id = flags & 0xFF;
        var type = (flags >> 8) & 0x7F;
        if ((flags & 0x8000) != 0)
        {
            // Continued object: the first four bytes give the total size; data arrives in several records.
            var total = I32(o);
            if (!continued.TryGetValue(id, out var part))
                part = (type, new List<byte>(Math.Max(0, Math.Min(total, 64 << 20))), total);
            part.Bytes.AddRange(data.AsSpan(o + 4, Math.Max(0, Math.Min(size - 4, data.Length - o - 4))).ToArray());
            continued[id] = part;
            if (part.Bytes.Count < part.Total)
                return;
            continued.Remove(id);
            var bytes = part.Bytes.ToArray();
            var player = new EmfPlusPlayer(bytes, gdi, svg) { dpiX = dpiX, dpiY = dpiY, videoReference = videoReference };
            objects[id] = player.ParseObject(type, 0, bytes.Length) ?? new object();
            return;
        }
        continued.Remove(id);
        objects[id] = ParseObject(type, o, size) ?? new object();
    }

    object? ParseObject(int type, int o, int size) => type switch
    {
        1 => ParseBrush(o, out _),
        2 => ParsePen(o, size),
        3 => ParsePath(o, out _),
        4 => ParseRegion(o),
        5 => ParseImage(o, size),
        6 => ParseFont(o),
        7 => new StringFormat { Flags = I32(o + 4), Alignment = I32(o + 12), LineAlign = I32(o + 16), LeadingMargin = F32(o + 36), TrailingMargin = F32(o + 40) },
        _ => null,
    };

    Brush ParseBrush(int o, out int length)
    {
        var type = I32(o + 4);
        var b = new Brush { Type = type };
        length = 8;
        switch (type)
        {
            case 0:
                b.Color = U32(o + 8);
                length = 12;
                break;
            case 1:
                b.Hatch = I32(o + 8);
                b.Color = U32(o + 12);
                b.Color2 = U32(o + 16);
                length = 20;
                break;
            case 2:   // texture
            {
                var brushFlags = I32(o + 8);
                b.Wrap = I32(o + 12);
                var p = o + 16;
                if ((brushFlags & 0x02) != 0) { b.Transform = Matrix(p); p += 24; }
                if (ParseImage(p, data.Length - p) is { } image)
                {
                    b.Texture = image.Uri;
                    b.TextureSize = ((int)image.Width, (int)image.Height);
                }
                length = data.Length - o;
                break;
            }
            case 3:   // path gradient
            {
                var brushFlags = I32(o + 8);
                b.Wrap = I32(o + 12);
                b.Color = U32(o + 16);
                b.Center = (F32(o + 20), F32(o + 24));
                var count = I32(o + 28);
                b.Color2 = count > 0 ? U32(o + 32) : b.Color;
                var p = o + 32 + Math.Max(0, count) * 4;
                if ((brushFlags & 0x01) != 0)
                {
                    var pathSize = I32(p);
                    var path = ParsePath(p + 4, out _);
                    b.BoundaryPath = PathData(path, Xform.Identity);
                    b.Rect = Bounds(path.Points);
                    p += 4 + pathSize;
                }
                else
                {
                    var n = I32(p);
                    var points = new List<(double X, double Y)>();
                    for (var i = 0; i < n && i < 100000; i++) points.Add((F32(p + 4 + i * 8), F32(p + 8 + i * 8)));
                    var pb = new PathBuilder();
                    if (points.Count > 0) { pb.MoveTo(points[0]); foreach (var pt in points.Skip(1)) pb.LineTo(pt); pb.Close(); }
                    b.BoundaryPath = pb.ToString();
                    b.Rect = Bounds(points);
                    p += 4 + n * 8;
                }
                if ((brushFlags & 0x02) != 0) { b.Transform = Matrix(p); p += 24; }
                if ((brushFlags & 0x04) != 0) b.Stops = PresetColors(p);
                length = p - o;
                break;
            }
            case 4:   // linear gradient
            {
                var brushFlags = I32(o + 8);
                b.Wrap = I32(o + 12);
                b.Rect = (F32(o + 16), F32(o + 20), F32(o + 24), F32(o + 28));
                b.Color = U32(o + 32);
                b.Color2 = U32(o + 36);
                b.Gamma = (brushFlags & 0x80) != 0;
                var p = o + 48;
                if ((brushFlags & 0x02) != 0) { b.Transform = Matrix(p); p += 24; }
                if ((brushFlags & 0x04) != 0)
                {
                    b.Stops = PresetColors(p);
                }
                else if ((brushFlags & 0x08) != 0 || (brushFlags & 0x10) != 0)
                {
                    // Blend factors mix the two colors; gamma-corrected brushes mix them in linear light.
                    var n = I32(p);
                    b.Stops = [];
                    for (var i = 0; i < n && i < 1000; i++)
                    {
                        var factor = F32(p + 4 + n * 4 + i * 4);
                        b.Stops.Add((F32(p + 4 + i * 4), b.Gamma ? LinearLerp(b.Color, b.Color2, factor) : Lerp(b.Color, b.Color2, factor)));
                    }
                    b.Gamma = false;   // the stops are final colors now
                }
                length = p - o;
                break;
            }
        }
        return b;
    }

    List<(double, uint)> PresetColors(int p)
    {
        var n = I32(p);
        var stops = new List<(double, uint)>();
        for (var i = 0; i < n && i < 1000; i++)
            stops.Add((F32(p + 4 + i * 4), U32(p + 4 + n * 4 + i * 4)));
        return stops;
    }

    Pen ParsePen(int o, int size)
    {
        var pen = new Pen();
        var flags = I32(o + 8);
        pen.Unit = I32(o + 12);
        pen.Width = F32(o + 16);
        var p = o + 20;
        if ((flags & 0x0001) != 0) p += 24;   // transform
        if ((flags & 0x0002) != 0) { pen.StartCap = I32(p); p += 4; }
        if ((flags & 0x0004) != 0) { pen.EndCap = I32(p); p += 4; }
        if ((flags & 0x0008) != 0) { pen.Join = I32(p); p += 4; }
        if ((flags & 0x0010) != 0) { pen.MiterLimit = F32(p); p += 4; }
        if ((flags & 0x0020) != 0) { pen.DashStyle = I32(p); p += 4; }
        if ((flags & 0x0040) != 0) { pen.DashCap = I32(p); p += 4; }
        if ((flags & 0x0080) != 0) { pen.DashOffset = F32(p); p += 4; }
        if ((flags & 0x0100) != 0)
        {
            var n = I32(p);
            pen.Dashes = Enumerable.Range(0, Math.Max(0, Math.Min(n, 64))).Select(i => (double)F32(p + 4 + i * 4)).ToArray();
            p += 4 + n * 4;
        }
        if ((flags & 0x0200) != 0) p += 4;   // alignment
        if ((flags & 0x0400) != 0) { var n = I32(p); p += 4 + n * 4; }   // compound line
        if ((flags & 0x0800) != 0) { var n = I32(p); pen.StartCustom = ParseCap(p + 4); p += 4 + n; }   // custom start cap
        if ((flags & 0x1000) != 0) { var n = I32(p); pen.EndCustom = ParseCap(p + 4); p += 4 + n; }     // custom end cap
        if (p < o + size)
            pen.Brush = ParseBrush(p, out _);
        return pen;
    }

    Cap? ParseCap(int o)
    {
        var type = I32(o + 4);
        var d = o + 8;
        if (type == 1)
        {
            // Adjustable arrow: a triangle (optionally with a middle inset) pointing along the line.
            var (w, h, inset) = (F32(d), F32(d + 4), F32(d + 8));
            var filled = I32(d + 12) != 0;
            var points = new List<(double X, double Y)> { (0, 0), (-w / 2, -h), (0, -h + inset), (w / 2, -h) };
            var path = new PlusPath { Points = points, Types = [0, 1, 1, 0x81] };
            return new Cap { Fill = filled ? path : null, Line = filled ? null : path, WidthScale = F32(d + 32) is var ws and > 0 ? ws : 1 };
        }
        var capFlags = I32(d);
        var cap = new Cap { WidthScale = F32(d + 28) is var scale and > 0 ? scale : 1 };
        var q = d + 48;
        if ((capFlags & 1) != 0) { var n = I32(q); cap.Fill = ParsePath(q + 4, out _); q += 4 + n; }
        if ((capFlags & 2) != 0) { var n = I32(q); cap.Line = ParsePath(q + 4, out _); }
        return cap;
    }

    PlusPath ParsePath(int o, out int length)
    {
        var count = I32(o + 4);
        var flags = I32(o + 8);
        var p = o + 12;
        var points = new List<(double X, double Y)>(Math.Max(0, Math.Min(count, 1 << 20)));
        if ((flags & 0x0800) != 0)   // relative, variable-length coordinates
        {
            double x = 0, y = 0;
            for (var i = 0; i < count; i++)
            {
                x += RelativeValue(ref p);
                y += RelativeValue(ref p);
                points.Add((x, y));
            }
        }
        else if ((flags & 0x4000) != 0)
        {
            for (var i = 0; i < count; i++) { points.Add((I16(p), I16(p + 2))); p += 4; }
        }
        else
        {
            for (var i = 0; i < count; i++) { points.Add((F32(p), F32(p + 4))); p += 8; }
        }
        var types = new byte[Math.Max(0, count)];
        if ((flags & 0x1000) != 0)   // run-length encoded point types
        {
            var i = 0;
            while (i < count && p + 2 <= data.Length)
            {
                var run = data[p] & 0x3F;
                var value = data[p + 1];
                for (var k = 0; k < run && i < count; k++) types[i++] = value;
                p += 2;
            }
        }
        else
        {
            for (var i = 0; i < count && p + i < data.Length; i++) types[i] = data[p + i];
            p += count;
        }
        length = p - o;
        return new PlusPath { Points = points, Types = types };
    }

    int RelativeValue(ref int p)
    {
        var first = data[p];
        if ((first & 0x80) == 0)
        {
            p += 1;
            return (sbyte)(first << 1) >> 1;
        }
        var value = (short)(((first & 0x7F) << 8) | data[p + 1]);
        p += 2;
        return (short)(value << 1) >> 1;
    }

    /// <summary>A region as device path data (union and intersection approximated by combining the shapes).</summary>
    string ParseRegion(int o)
    {
        var p = o + 8;
        return RegionNode(ref p);
    }

    string RegionNode(ref int p)
    {
        var type = U32(p);
        p += 4;
        switch (type)
        {
            case 0x10000000:   // rectangle
            {
                var r = (X: F32(p), Y: F32(p + 4), W: F32(p + 8), H: F32(p + 12));
                p += 16;
                var pb = new PathBuilder();
                AppendRect(pb, r);
                return pb.ToString();
            }
            case 0x10000001:   // path
            {
                var size = I32(p);
                var path = ParsePath(p + 4, out _);
                p += 4 + size;
                return PathData(path);
            }
            case 0x10000002: return "";
            case 0x10000003: return "M-1e6 -1e6H1e6V1e6H-1e6Z";
            default:
            {
                var left = RegionNode(ref p);
                var right = RegionNode(ref p);
                return type switch
                {
                    1 or 2 => left + right,   // union, xor (approximation)
                    0 => right.Length > 0 && left.Length > 0 ? right : left,   // intersection: keep the smaller operand (approximation)
                    3 => left,   // exclude
                    _ => left + right,
                };
            }
        }
    }

    PlusImage? ParseImage(int o, int size)
    {
        var type = I32(o + 4);
        if (type == 1)   // bitmap
        {
            var width = I32(o + 8);
            var height = I32(o + 12);
            var stride = I32(o + 16);
            var format = I32(o + 20);
            var kind = I32(o + 24);
            var start = o + 28;
            var length = Math.Max(0, Math.Min(size - 28, data.Length - start));
            if (kind == 1)
                return new PlusImage { Uri = Sniff(data.AsSpan(start, length)), Width = width, Height = height };
            var rgba = Pixels(start, width, height, stride, format);
            return rgba == null ? null : new PlusImage { Uri = Png.DataUri(width, height, rgba), Width = width, Height = height };
        }
        if (type == 2)   // metafile
        {
            var metafileType = I32(o + 8);
            var dataSize = I32(o + 12);
            var bytes = data.AsSpan(o + 16, Math.Max(0, Math.Min(dataSize, data.Length - o - 16))).ToArray();
            var inner = metafileType is 1 or 2 ? WmfPlayer.ToSvg(bytes, svg.NewId() + "w") : EmfPlayer.ToSvg(bytes, svg.NewId() + "e");
            if (inner == null) return null;
            return new PlusImage { Uri = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(inner)), Width = 0, Height = 0 };
        }
        return null;
    }

    static string? Sniff(ReadOnlySpan<byte> bytes)
    {
        string? mime = bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 ? "image/png"
            : bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 ? "image/jpeg"
            : bytes.Length > 3 && bytes[0] == 0x47 && bytes[1] == 0x49 ? "image/gif"
            : bytes.Length > 2 && bytes[0] == 0x42 && bytes[1] == 0x4D ? "image/bmp"
            : null;
        return mime == null ? null : $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
    }

    byte[]? Pixels(int start, int width, int height, int stride, int format)
    {
        if (width <= 0 || height <= 0 || width > 32768 || height > 32768)
            return null;
        var bpp = (format >> 8) & 0xFF;
        var rgba = new byte[width * height * 4];
        var indexed = (format & 0x00010000) != 0;
        var paletteCount = indexed ? I32(start + 4) : 0;
        var palette = start + 8;
        var pixels = indexed ? start + 8 + paletteCount * 4 : start;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var t = (y * width + x) * 4;
                var row = pixels + y * stride;
                uint argb;
                if (indexed)
                {
                    var bit = x * bpp;
                    var index = row + bit / 8 < data.Length ? (data[row + bit / 8] >> (8 - bpp - bit % 8)) & ((1 << bpp) - 1) : 0;
                    argb = index < paletteCount ? U32(palette + index * 4) : 0xFF000000;
                }
                else
                {
                    argb = bpp switch
                    {
                        32 => U32(row + x * 4) | ((format & 0x00040000) == 0 && (format & 0x00080000) == 0 && format == 0x00022009 ? 0xFF000000 : 0),
                        24 => (uint)(data[row + x * 3] | (data[row + x * 3 + 1] << 8) | (data[row + x * 3 + 2] << 16)) | 0xFF000000,
                        16 => Rgb565(U16(row + x * 2), format),
                        _ => 0xFF000000,
                    };
                }
                var a = (byte)(argb >> 24);
                if (format == 0x000E200B && a is > 0 and < 255)   // premultiplied
                {
                    rgba[t] = (byte)Math.Min(255, ((argb >> 16) & 0xFF) * 255 / a);
                    rgba[t + 1] = (byte)Math.Min(255, ((argb >> 8) & 0xFF) * 255 / a);
                    rgba[t + 2] = (byte)Math.Min(255, (argb & 0xFF) * 255 / a);
                }
                else
                {
                    rgba[t] = (byte)(argb >> 16);
                    rgba[t + 1] = (byte)(argb >> 8);
                    rgba[t + 2] = (byte)argb;
                }
                rgba[t + 3] = a;
            }
        }
        return rgba;
    }

    static uint Rgb565(ushort v, int format)
    {
        if (format == 0x00021005)   // 555
            return 0xFF000000 | (uint)((((v >> 10) & 0x1F) * 255 / 31) << 16 | (((v >> 5) & 0x1F) * 255 / 31) << 8 | ((v & 0x1F) * 255 / 31));
        return 0xFF000000 | (uint)((((v >> 11) & 0x1F) * 255 / 31) << 16 | (((v >> 5) & 0x3F) * 255 / 63) << 8 | ((v & 0x1F) * 255 / 31));
    }

    PlusFont ParseFont(int o)
    {
        var length = I32(o + 20);
        var family = Encoding.Unicode.GetString(data.AsSpan(o + 24, Math.Max(0, Math.Min(length * 2, data.Length - o - 24))));
        return new PlusFont { EmSize = F32(o + 4), Unit = I32(o + 8), Style = I32(o + 12), Family = family.Length > 0 ? family : "Arial" };
    }

    // ------------------------------------------------------------------ geometry

    Xform Matrix(int o) => new(F32(o), F32(o + 4), F32(o + 8), F32(o + 12), F32(o + 16), F32(o + 20));

    (double X, double Y, double W, double H) Rect(int o, int index, bool compressed) => compressed
        ? (I16(o + index * 8), I16(o + index * 8 + 2), I16(o + index * 8 + 4), I16(o + index * 8 + 6))
        : (F32(o + index * 16), F32(o + index * 16 + 4), F32(o + index * 16 + 8), F32(o + index * 16 + 12));

    List<(double X, double Y)> Points(int o, int count, int flags)
    {
        var points = new List<(double X, double Y)>(Math.Max(0, Math.Min(count, 1 << 20)));
        if ((flags & 0x0800) != 0)
        {
            var p = o;
            double x = 0, y = 0;
            for (var i = 0; i < count && i < 1 << 20; i++) { x += RelativeValue(ref p); y += RelativeValue(ref p); points.Add((x, y)); }
        }
        else if ((flags & 0x4000) != 0)
        {
            for (var i = 0; i < count && i < 1 << 20; i++) points.Add((I16(o + i * 4), I16(o + i * 4 + 2)));
        }
        else
        {
            for (var i = 0; i < count && i < 1 << 20; i++) points.Add((F32(o + i * 8), F32(o + i * 8 + 4)));
        }
        return points;
    }

    static (double X, double Y, double W, double H) Bounds(List<(double X, double Y)> points)
    {
        if (points.Count == 0) return (0, 0, 0, 0);
        var (x0, y0, x1, y1) = (points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
        return (x0, y0, x1 - x0, y1 - y0);
    }

    void AppendRect(PathBuilder p, (double X, double Y, double W, double H) r)
    {
        p.MoveTo(Device(r.X, r.Y));
        p.LineTo(Device(r.X + r.W, r.Y));
        p.LineTo(Device(r.X + r.W, r.Y + r.H));
        p.LineTo(Device(r.X, r.Y + r.H));
        p.Close();
    }

    string PolygonData(List<(double X, double Y)> points, bool close)
    {
        if (points.Count == 0) return "";
        var p = new PathBuilder();
        p.MoveTo(Device(points[0].X, points[0].Y));
        foreach (var pt in points.Skip(1)) p.LineTo(Device(pt.X, pt.Y));
        if (close) p.Close();
        return p.ToString();
    }

    string EllipseData((double X, double Y, double W, double H) r) => ArcData(r, 0, 360, pie: false, close: true);

    string ArcData((double X, double Y, double W, double H) r, double start, double sweep, bool pie, bool close = false)
    {
        var (cx, cy, rx, ry) = (r.X + r.W / 2, r.Y + r.H / 2, r.W / 2, r.H / 2);
        var a0 = start * Math.PI / 180;
        var a1 = sweep * Math.PI / 180;
        var p = new PathBuilder();
        if (pie) { p.MoveTo(Device(cx, cy)); p.LineTo(Device(cx + rx * Math.Cos(a0), cy + ry * Math.Sin(a0))); }
        else p.MoveTo(Device(cx + rx * Math.Cos(a0), cy + ry * Math.Sin(a0)));
        foreach (var (c1, c2, end) in PathBuilder.ArcBeziers(cx, cy, rx, ry, a0, a1))
            p.CurveTo(Device(c1.X, c1.Y), Device(c2.X, c2.Y), Device(end.X, end.Y));
        if (pie || close) p.Close();
        return p.ToString();
    }

    /// <summary>Cardinal spline through the points (GDI+ curves) as Bézier segments.</summary>
    string CurveData(List<(double X, double Y)> points, double tension, bool closed)
    {
        if (points.Count < 2) return "";
        var t = tension / 3;
        var p = new PathBuilder();
        p.MoveTo(Device(points[0].X, points[0].Y));
        var n = points.Count;
        var segments = closed ? n : n - 1;
        for (var i = 0; i < segments; i++)
        {
            var p0 = points[closed ? (i - 1 + n) % n : Math.Max(0, i - 1)];
            var p1 = points[i];
            var p2 = points[(i + 1) % n];
            var p3 = points[closed ? (i + 2) % n : Math.Min(n - 1, i + 2)];
            var c1 = (p1.X + (p2.X - p0.X) * t, p1.Y + (p2.Y - p0.Y) * t);
            var c2 = (p2.X - (p3.X - p1.X) * t, p2.Y - (p3.Y - p1.Y) * t);
            p.CurveTo(Device(c1.Item1, c1.Item2), Device(c2.Item1, c2.Item2), Device(p2.X, p2.Y));
        }
        if (closed) p.Close();
        return p.ToString();
    }

    string PathData(PlusPath path) => PathData(path, Total);

    string PathData(PlusPath path, Xform transform)
    {
        var p = new PathBuilder();
        var points = path.Points;
        for (var i = 0; i < points.Count; i++)
        {
            var type = path.Types[i] & 0x07;
            var pt = transform.Apply(points[i].X, points[i].Y);
            if (type == 0)
                p.MoveTo(pt);
            else if (type == 3 && i + 2 < points.Count)
            {
                p.CurveTo(pt, transform.Apply(points[i + 1].X, points[i + 1].Y), transform.Apply(points[i + 2].X, points[i + 2].Y));
                i += 2;
                if ((path.Types[i] & 0x80) != 0) p.Close();
                continue;
            }
            else
                p.LineTo(pt);
            if ((path.Types[i] & 0x80) != 0) p.Close();
        }
        return p.ToString();
    }

    static bool Open(PlusPath path) => path.Types.Length == 0 || (path.Types[^1] & 0x80) == 0;

    // ------------------------------------------------------------------ painting

    static string Rgb(uint argb) => $"#{(argb >> 16) & 0xFF:x2}{(argb >> 8) & 0xFF:x2}{argb & 0xFF:x2}";

    static string Opacity(uint argb, string attribute)
    {
        var a = (argb >> 24) & 0xFF;
        return a == 255 ? "" : $" {attribute}-opacity=\"{SvgWriter.Num(a / 255.0)}\"";
    }

    static uint Lerp(uint a, uint b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        uint Channel(int shift) => (uint)Math.Round(((a >> shift) & 0xFF) * (1 - t) + ((b >> shift) & 0xFF) * t) << shift;
        return Channel(24) | Channel(16) | Channel(8) | Channel(0);
    }

    void Fill(string d, bool isColor, uint brush, bool evenOdd, bool devicePath = false)
    {
        if (string.IsNullOrEmpty(d))
            return;
        var paint = isColor ? $" fill=\"{Rgb(brush)}\"{Opacity(brush, "fill")}"
            : objects.GetValueOrDefault((int)brush) is Brush b ? BrushPaint(b) : " fill=\"#000\"";
        svg.Add($"<path d=\"{d}\"{paint}{(evenOdd ? " fill-rule=\"evenodd\"" : "")}/>", clip);
    }

    void Stroke(string d, int penId, bool open = true)
    {
        if (string.IsNullOrEmpty(d) || objects.GetValueOrDefault(penId) is not Pen pen)
            return;
        svg.Add($"<path d=\"{d}\" fill=\"none\"{PenPaint(pen, open)}/>", clip);
    }

    /// <summary>Draws the custom start and end caps of a pen at the ends of an open figure (world coordinates).</summary>
    void Caps(List<(double X, double Y)> points, int penId)
    {
        if (objects.GetValueOrDefault(penId) is not Pen pen || (pen.StartCustom == null && pen.EndCustom == null) || points.Count < 2)
            return;
        void Draw(Cap? cap, (double X, double Y) end, (double X, double Y) previous)
        {
            if (cap == null) return;
            var (dx, dy) = (end.X - previous.X, end.Y - previous.Y);
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-9) return;
            (dx, dy) = (dx / length, dy / length);
            var scale = pen.Width * cap.WidthScale;
            // Cap space: y along the line (outwards), x across it.
            var toWorld = new Xform(-dy * scale, dx * scale, dx * scale, dy * scale, end.X, end.Y);
            var transform = toWorld.Then(Total);
            if (cap.Fill != null)
                svg.Add($"<path d=\"{PathData(cap.Fill, transform)}\"{BrushPaint(pen.Brush)}/>", clip);
            if (cap.Line != null)
                svg.Add($"<path d=\"{PathData(cap.Line, transform)}\" fill=\"none\"{PenPaint(pen, open: true)}/>", clip);
        }
        // The neighbouring point that differs from the end point gives the direction.
        var last = points.Count - 1;
        var before = last - 1;
        while (before > 0 && points[before] == points[last]) before--;
        var after = 1;
        while (after < last && points[after] == points[0]) after++;
        Draw(pen.EndCustom, points[last], points[before]);
        Draw(pen.StartCustom, points[0], points[after]);
    }

    string BrushPaint(Brush b, string attribute = "fill")
    {
        switch (b.Type)
        {
            case 0:
                return $" {attribute}=\"{Rgb(b.Color)}\"{Opacity(b.Color, attribute)}";
            case 1:
            {
                var id = svg.NewId();
                var preset = HatchPreset(b.Hatch);
                var tile = PatternPresets.Tiles.GetValueOrDefault(preset) ?? PatternPresets.Tiles["pct50"];
                static (byte, byte, byte, byte) Rgba(uint argb) => ((byte)((argb >> 16) & 0xFF), (byte)((argb >> 8) & 0xFF), (byte)(argb & 0xFF), (byte)(argb >> 24));
                // GDI+ draws its forward and backward diagonals and their cross antialiased (SvgWriter.HatchTile's lines;
                // measured in PowerPoint's rendering of generated/metafiles' diagonal cross, its 30 % hatch crisp; local/
                // hatchprobe: the forward and backward diagonals and their cross at 0.91 and 0.25 of their color whatever
                // the picture's smoothing mode, the light downward diagonal, the outlined diamond and the 30 % hatch
                // crisp); Word's are not measured.
                var image = SvgWriter.HatchTile((x, y) => (tile[y] & (0x80 >> x)) != 0, Rgba(b.Color), Rgba(b.Color2), lines: !Word && b.Hatch is 2 or 3 or 5);
                // Hatches are 8 × 8 pixels of the output device, not of the 600 dpi reference device, laid on its pixels as
                // GDI's brushes (GdiContext.BrushTransform; measured: generated/metafiles' diagonal cross at x 53.33 and
                // 30 % hatch at 453.33, y 173.33, start at 49, 449 and 169), moved by the rendering origin.
                svg.Def($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" width=\"8\" height=\"8\" patternTransform=\"{gdi.BrushTransform(renderingOrigin)}\">{image}</pattern>");
                return $" {attribute}=\"url(#{id})\"";
            }
            case 2 when b.Texture != null:
            {
                var id = svg.NewId();
                var transform = b.Transform.Then(Total);
                svg.Def($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" width=\"{b.TextureSize.W}\" height=\"{b.TextureSize.H}\" patternTransform=\"{transform.Svg()}\"><image width=\"{b.TextureSize.W}\" height=\"{b.TextureSize.H}\" href=\"{b.Texture}\"/></pattern>");
                return $" {attribute}=\"url(#{id})\"";
            }
            case 3:
            {
                // Path gradient: an elliptical radial gradient over the boundary's bounding box, focused on the
                // center point (exact for ellipses, an approximation for other boundaries).
                var id = svg.NewId();
                var (rx, ry) = (Math.Max(b.Rect.W / 2, 1e-6), Math.Max(b.Rect.H / 2, 1e-6));
                var (bx, by) = (b.Rect.X + rx, b.Rect.Y + ry);
                var unit = Xform.Scale(rx, ry).Then(Xform.Translate(bx, by));
                var transform = unit.Then(b.Transform).Then(Total);
                var (fx, fy) = ((b.Center.X - bx) / rx, (b.Center.Y - by) / ry);
                var stops = b.Stops is { Count: > 0 } preset
                    ? string.Concat(preset.Select(s => Stop(1 - s.Position, s.Color)).Reverse())
                    : Stop(0, b.Color) + Stop(1, b.Color2);
                svg.Def($"<radialGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" cx=\"0\" cy=\"0\" r=\"1\" fx=\"{SvgWriter.Num(fx)}\" fy=\"{SvgWriter.Num(fy)}\" gradientTransform=\"{transform.Svg()}\">{stops}</radialGradient>");
                return $" {attribute}=\"url(#{id})\"";
            }
            case 4:
            {
                var id = svg.NewId();
                var transform = b.Transform.Then(Total);
                var stops = b.Stops is { Count: > 0 } list ? list : [(0, b.Color), (1, b.Color2)];
                var stopMarkup = new StringBuilder();
                for (var i = 0; i < stops.Count; i++)
                {
                    stopMarkup.Append(Stop(stops[i].Position, stops[i].Color));
                    if (b.Gamma && i + 1 < stops.Count)
                    {
                        // Gamma-corrected blending: intermediate stops interpolated in linear light.
                        for (var k = 1; k < 8; k++)
                        {
                            var t = k / 8.0;
                            stopMarkup.Append(Stop(stops[i].Position + (stops[i + 1].Position - stops[i].Position) * t, LinearLerp(stops[i].Color, stops[i + 1].Color, t)));
                        }
                    }
                }
                var spread = b.Wrap switch { 0 => " spreadMethod=\"repeat\"", 1 or 2 or 3 => " spreadMethod=\"reflect\"", _ => "" };
                var r = b.Rect;
                svg.Def($"<linearGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" x1=\"{SvgWriter.Num(r.X)}\" y1=\"{SvgWriter.Num(r.Y)}\" x2=\"{SvgWriter.Num(r.X + r.W)}\" y2=\"{SvgWriter.Num(r.Y)}\" gradientTransform=\"{transform.Svg()}\"{spread}>{stopMarkup}</linearGradient>");
                return $" {attribute}=\"url(#{id})\"";
            }
            default:
                return $" {attribute}=\"{Rgb(b.Color)}\"";
        }
    }

    static string Stop(double offset, uint argb) =>
        $"<stop offset=\"{SvgWriter.Num(Math.Clamp(offset, 0, 1))}\" stop-color=\"{Rgb(argb)}\"{Opacity(argb, "stop")}/>";

    static uint LinearLerp(uint a, uint b, double t)
    {
        static double ToLinear(uint c, int shift) => Math.Pow(((c >> shift) & 0xFF) / 255.0, 2.2);
        uint Channel(int shift)
        {
            var v = ToLinear(a, shift) * (1 - t) + ToLinear(b, shift) * t;
            return (uint)Math.Round(Math.Pow(v, 1 / 2.2) * 255) << shift;
        }
        var alpha = (uint)Math.Round(((a >> 24) & 0xFF) * (1 - t) + ((b >> 24) & 0xFF) * t) << 24;
        return alpha | Channel(16) | Channel(8) | Channel(0);
    }

    /// <summary>GDI+ hatch styles in the order of the DrawingML preset patterns.</summary>
    static string HatchPreset(int hatch) => hatch switch
    {
        0 => "horz", 1 => "vert", 2 => "dnDiag", 3 => "upDiag", 4 => "cross", 5 => "diagCross", 6 => "pct5", 7 => "pct10", 8 => "pct20",
        9 => "pct25", 10 => "pct30", 11 => "pct40", 12 => "pct50", 13 => "pct60", 14 => "pct70", 15 => "pct75", 16 => "pct80",
        17 => "pct90", 18 => "ltDnDiag", 19 => "ltUpDiag", 20 => "dkDnDiag", 21 => "dkUpDiag", 22 => "wdDnDiag", 23 => "wdUpDiag",
        24 => "ltVert", 25 => "ltHorz", 26 => "narVert", 27 => "narHorz", 28 => "dkVert", 29 => "dkHorz", 30 => "dashDnDiag",
        31 => "dashUpDiag", 32 => "dashHorz", 33 => "dashVert", 34 => "smConfetti", 35 => "lgConfetti", 36 => "zigZag", 37 => "wave",
        38 => "diagBrick", 39 => "horzBrick", 40 => "weave", 41 => "plaid", 42 => "divot", 43 => "dotGrid", 44 => "dotDmnd",
        45 => "shingle", 46 => "trellis", 47 => "sphere", 48 => "smGrid", 49 => "smCheck", 50 => "lgCheck", 51 => "openDmnd",
        52 => "solidDmnd", _ => "pct50",
    };

    string PenPaint(Pen pen, bool open)
    {
        var scale = pen.Unit is 0 or 2 ? Total.AverageScale : UnitFactor(pen.Unit, true) * Page.AverageScale / Math.Max(1e-9, UnitFactor(pageUnit, true));
        var width = pen.Width * (pen.Unit is 0 ? Total.AverageScale : UnitFactor(pen.Unit, true));
        var sb = new StringBuilder(BrushPaint(pen.Brush, "stroke"));
        if (width < 1)
            sb.Append(" stroke-width=\"1\" vector-effect=\"non-scaling-stroke\"");
        else
            sb.Append($" stroke-width=\"{SvgWriter.Num(width)}\"");
        var dashed = pen.Dashes is { Length: > 0 } || pen.DashStyle is >= 1 and <= 4;
        // Dashes use the dash cap (flat by default); SVG has one cap for dashes and line ends alike.
        var cap = dashed
            ? pen.DashCap switch { 2 or 3 => "round", _ => "" }
            : pen.StartCap switch { 1 or 0x11 => "square", 2 or 0x12 => "round", _ => "" };
        if (open && cap.Length > 0) sb.Append($" stroke-linecap=\"{cap}\"");
        sb.Append(pen.Join switch
        {
            1 => " stroke-linejoin=\"bevel\"",
            2 => " stroke-linejoin=\"round\"",
            _ => $" stroke-linejoin=\"miter\" stroke-miterlimit=\"{SvgWriter.Num(Math.Max(1, pen.MiterLimit))}\"",
        });
        double[]? dashes = pen.Dashes is { Length: > 0 } custom ? custom : pen.DashStyle switch
        {
            1 => [3, 1],
            2 => [1, 1],
            3 => [3, 1, 1, 1],
            4 => [3, 1, 1, 1, 1, 1],
            _ => null,
        };
        if (dashes is { Length: > 0 })
        {
            var unit = Math.Max(width, 1);
            sb.Append($" stroke-dasharray=\"{string.Join(' ', dashes.Select(d => SvgWriter.Num(Math.Max(0.01, d) * unit)))}\"");
            if (pen.DashOffset != 0) sb.Append($" stroke-dashoffset=\"{SvgWriter.Num(pen.DashOffset * unit)}\"");
        }
        _ = scale;
        return sb.ToString();
    }

    void CombineClip(string d, int mode, bool devicePath = false, bool evenOdd = false)
    {
        switch (mode)
        {
            case 0:
                clip = new Clip { Path = d, EvenOdd = evenOdd };
                break;
            case 1:
                clip = new Clip { Parent = clip, Path = d, EvenOdd = evenOdd };
                break;
            case 4:
                clip = new Clip { Parent = clip, Path = "M-1e6 -1e6H1e6V1e6H-1e6Z" + d, EvenOdd = true };
                break;
            default:
                clip = clip == null ? null : new Clip { Path = clip.Path + d };
                break;
        }
    }

    // ------------------------------------------------------------------ images and text

    void DrawImage(PlusImage image, (double X, double Y, double W, double H) src, (double X, double Y) p0, (double X, double Y) p1, (double X, double Y) p2)
    {
        if (image.Uri == null)
            return;
        // Map the source rectangle onto the parallelogram p0 (top left), p1 (top right), p2 (bottom left).
        var w = Math.Abs(src.W) > 0 ? src.W : Math.Max(1, image.Width);
        var h = Math.Abs(src.H) > 0 ? src.H : Math.Max(1, image.Height);
        var local = new Xform((p1.X - p0.X) / w, (p1.Y - p0.Y) / w, (p2.X - p0.X) / h, (p2.Y - p0.Y) / h, p0.X, p0.Y);
        var matrix = local.Then(Total);
        var fullW = image.Width > 0 ? image.Width : w;
        var fullH = image.Height > 0 ? image.Height : h;
        var cropped = Math.Abs(src.X) > 0.01 || Math.Abs(src.Y) > 0.01 || Math.Abs(w - fullW) > 0.5 || Math.Abs(h - fullH) > 0.5;
        if (cropped && image.Width > 0)
        {
            var inner = $"<svg x=\"0\" y=\"0\" width=\"{SvgWriter.Num(w)}\" height=\"{SvgWriter.Num(h)}\" viewBox=\"{SvgWriter.Num(src.X)} {SvgWriter.Num(src.Y)} {SvgWriter.Num(w)} {SvgWriter.Num(h)}\" preserveAspectRatio=\"none\"><image width=\"{SvgWriter.Num(fullW)}\" height=\"{SvgWriter.Num(fullH)}\" href=\"{image.Uri}\" preserveAspectRatio=\"none\"/></svg>";
            svg.Add($"<g transform=\"{matrix.Svg()}\">{inner}</g>", clip);
            return;
        }
        svg.Add($"<image width=\"{SvgWriter.Num(w)}\" height=\"{SvgWriter.Num(h)}\" transform=\"{matrix.Svg()}\" preserveAspectRatio=\"none\" href=\"{image.Uri}\"/>", clip);
    }

    (double Size, string Attributes) FontAttributes(PlusFont font)
    {
        var size = font.EmSize * (font.Unit is 0 ? 1 : UnitFactor(font.Unit, false) / Math.Max(1e-9, UnitFactor(pageUnit, false) * pageScale));
        var sb = new StringBuilder($" font-family=\"{SvgWriter.Escape(GdiContext.Family(font.Family))}\"");
        if ((font.Style & 1) != 0) sb.Append(" font-weight=\"bold\"");
        if ((font.Style & 2) != 0) sb.Append(" font-style=\"italic\"");
        var decoration = ((font.Style & 4) != 0 ? "underline " : "") + ((font.Style & 8) != 0 ? "line-through" : "");
        if (decoration.Length > 0) sb.Append($" text-decoration=\"{decoration.Trim()}\"");
        return (size, sb.ToString());
    }

    void DrawString(int flags, int o)
    {
        if (objects.GetValueOrDefault(flags & 0xFF) is not PlusFont font)
            return;
        var brush = U32(o);
        var format = objects.GetValueOrDefault((int)U32(o + 4)) as StringFormat ?? new StringFormat();
        var length = I32(o + 8);
        var rect = (X: F32(o + 12), Y: F32(o + 16), W: F32(o + 20), H: F32(o + 24));
        var text = Encoding.Unicode.GetString(data.AsSpan(o + 28, Math.Max(0, Math.Min(length * 2, data.Length - o - 28))));
        var paint = (flags & 0x8000) != 0 ? $" fill=\"{Rgb(brush)}\"{Opacity(brush, "fill")}" : objects.GetValueOrDefault((int)brush) is Brush b ? BrushPaint(b) : " fill=\"#000\"";
        var (size, attributes) = FontAttributes(font);
        var resolved = Text.FontResolver.Default.Resolve(font.Family, (font.Style & 1) != 0, (font.Style & 2) != 0);
        var data2 = resolved.Data;
        var lineHeight = size * (data2.WinAscent + data2.WinDescent);

        // Lines: explicit breaks, then word wrapping within the layout rectangle.
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (rect.W <= 0 || (format.Flags & 0x1000) != 0)   // no wrap
            {
                lines.Add(paragraph);
                continue;
            }
            var current = "";
            foreach (var word in paragraph.Split(' '))
            {
                var candidate = current.Length == 0 ? word : current + " " + word;
                if (current.Length > 0 && data2.Width(candidate, false) * size > rect.W)
                {
                    lines.Add(current);
                    current = word;
                }
                else current = candidate;
            }
            lines.Add(current);
        }
        var totalHeight = lines.Count * lineHeight;
        var top = format.LineAlign switch { 1 => rect.Y + (rect.H - totalHeight) / 2, 2 => rect.Y + rect.H - totalHeight, _ => rect.Y };
        var sb = new StringBuilder();
        var transform = Total;
        var leading = format.LeadingMargin * size;
        var trailing = format.TrailingMargin * size;
        for (var i = 0; i < lines.Count; i++)
        {
            var w = data2.Width(lines[i], false) * size;
            var x = format.Alignment switch
            {
                1 => rect.X + (rect.W - w) / 2,
                2 => rect.X + rect.W - w - trailing,
                _ => rect.X + leading,
            };
            var baseline = top + i * lineHeight + data2.WinAscent * size;
            sb.Append($"<text x=\"{SvgWriter.Num(x)}\" y=\"{SvgWriter.Num(baseline)}\" font-size=\"{SvgWriter.Num(size)}\"{attributes}{paint} xml:space=\"preserve\">{SvgWriter.Escape(lines[i])}</text>");
        }
        svg.Add($"<g transform=\"{transform.Svg()}\">{sb}</g>", clip);
    }

    void DrawDriverString(int flags, int o)
    {
        if (objects.GetValueOrDefault(flags & 0xFF) is not PlusFont font)
            return;
        var brush = U32(o);
        var options = I32(o + 4);
        var matrixPresent = I32(o + 8) != 0;
        var count = I32(o + 12);
        if (count <= 0 || count > 100000)
            return;
        var glyphs = new ushort[count];
        for (var i = 0; i < count; i++) glyphs[i] = U16(o + 16 + i * 2);
        var positions = o + 16 + count * 2;
        var text = new StringBuilder();
        var resolved = Text.FontResolver.Default.Resolve(font.Family, (font.Style & 1) != 0, (font.Style & 2) != 0);
        IReadOnlyDictionary<ushort, char>? map = (options & 1) != 0 ? null : resolved.Data.GlyphToChar();
        foreach (var g in glyphs) text.Append(map == null ? (char)g : map.TryGetValue(g, out var c) ? c : ' ');
        var xs = new StringBuilder();
        var ys = new StringBuilder();
        var realized = (options & 4) == 0;   // RealizedAdvance absent: every glyph has a position
        for (var i = 0; i < count; i++)
        {
            if (!realized && i > 0) break;
            if (i > 0) { xs.Append(' '); ys.Append(' '); }
            xs.Append(SvgWriter.Num(F32(positions + i * 8)));
            ys.Append(SvgWriter.Num(F32(positions + i * 8 + 4)));
        }
        var transform = matrixPresent ? Matrix(positions + count * 8).Then(Total) : Total;
        var paint = (flags & 0x8000) != 0 ? $" fill=\"{Rgb(brush)}\"{Opacity(brush, "fill")}" : objects.GetValueOrDefault((int)brush) is Brush b ? BrushPaint(b) : " fill=\"#000\"";
        var (size, attributes) = FontAttributes(font);
        svg.Add($"<text x=\"{xs}\" y=\"{ys}\" transform=\"{transform.Svg()}\" font-size=\"{SvgWriter.Num(size)}\"{attributes}{paint} xml:space=\"preserve\">{SvgWriter.Escape(text.ToString())}</text>", clip);
    }
}
