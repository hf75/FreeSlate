using System.Globalization;
using System.Text;

namespace FreeSlate.Metafiles;

/// <summary>A 2D affine transform in GDI order: x' = M11·x + M21·y + Dx, y' = M12·x + M22·y + Dy.</summary>
public readonly record struct Xform(double M11, double M12, double M21, double M22, double Dx, double Dy)
{
    public static readonly Xform Identity = new(1, 0, 0, 1, 0, 0);

    public (double X, double Y) Apply(double x, double y) => (M11 * x + M21 * y + Dx, M12 * x + M22 * y + Dy);

    /// <summary>Applies only the linear part (for vectors and sizes).</summary>
    public (double X, double Y) ApplyVector(double x, double y) => (M11 * x + M21 * y, M12 * x + M22 * y);

    /// <summary>This transform first, then <paramref name="next"/>.</summary>
    public Xform Then(Xform next) => new(
        M11 * next.M11 + M12 * next.M21, M11 * next.M12 + M12 * next.M22,
        M21 * next.M11 + M22 * next.M21, M21 * next.M12 + M22 * next.M22,
        Dx * next.M11 + Dy * next.M21 + next.Dx, Dx * next.M12 + Dy * next.M22 + next.Dy);

    public static Xform Translate(double x, double y) => new(1, 0, 0, 1, x, y);

    public static Xform Scale(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);

    /// <summary>Rotation in degrees, clockwise on a y-down device.</summary>
    public static Xform Rotate(double degrees)
    {
        var (sin, cos) = Math.SinCos(degrees * Math.PI / 180);
        return new Xform(cos, sin, -sin, cos, 0, 0);
    }

    public double Determinant => M11 * M22 - M12 * M21;

    /// <summary>The average scale (for line widths).</summary>
    public double AverageScale => Math.Sqrt(Math.Abs(Determinant));

    public Xform? Inverse()
    {
        var det = Determinant;
        if (Math.Abs(det) < 1e-12)
            return null;
        var (a, b, c, d) = (M22 / det, -M12 / det, -M21 / det, M11 / det);
        return new Xform(a, b, c, d, -(Dx * a + Dy * c), -(Dx * b + Dy * d));
    }

    /// <summary>
    /// The transform as SVG, its scale and rotation with six significant digits: logical units of a few hundredths of a
    /// pixel lose a percent of the picture at three decimals (docx/external/tika/testWORD_embedded_pdf: 0.028356 → 0.028).
    /// </summary>
    public string Svg() => FormattableString.Invariant($"matrix({Fine(M11)} {Fine(M12)} {Fine(M21)} {Fine(M22)} {N(Dx)} {N(Dy)})");

    static string N(double v) => SvgWriter.Num(v);

    static string Fine(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "0" : v.ToString("G6", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>A clipping region in device coordinates; regions nest by intersection.</summary>
public sealed class Clip
{
    public Clip? Parent { get; init; }

    /// <summary>Path data in device coordinates.</summary>
    public required string Path { get; init; }

    public bool EvenOdd { get; init; }

    /// <summary>An exclusion clip (far rectangle plus excluded areas, even-odd); consecutive exclusions merge into it.</summary>
    public bool Exclusion { get; init; }

    /// <summary>How far the path is moved (device units; OffsetClipRgn).</summary>
    public (double X, double Y) Shift { get; init; }

    internal string? Id { get; set; }

    /// <summary>The clip has been written; later changes need a new clip.</summary>
    public bool IsWritten => Id != null;
}

/// <summary>Collects SVG elements and definitions for one metafile.</summary>
public sealed class SvgWriter(string idPrefix)
{
    readonly StringBuilder body = new();
    readonly StringBuilder defs = new();
    int nextId;
    Clip? openClip;
    bool clipOpen;

    public string NewId() => $"{idPrefix}{++nextId}";

    public void Def(string markup) => defs.Append(markup);

    /// <summary>Appends an element, inside a group clipped by <paramref name="clip"/>.</summary>
    public void Add(string element, Clip? clip)
    {
        if (!clipOpen || !ReferenceEquals(clip, openClip))
        {
            if (clipOpen) body.Append("</g>");
            clipOpen = clip != null;
            openClip = clip;
            if (clip != null)
                body.Append($"<g clip-path=\"url(#{ClipId(clip)})\">");
        }
        body.Append(element);
    }

    string ClipId(Clip clip)
    {
        if (clip.Id != null)
            return clip.Id;
        var parent = clip.Parent != null ? $" clip-path=\"url(#{ClipId(clip.Parent)})\"" : "";
        clip.Id = NewId();
        var shift = clip.Shift == default ? "" : $" transform=\"translate({Num(clip.Shift.X)} {Num(clip.Shift.Y)})\"";
        defs.Append($"<clipPath id=\"{clip.Id}\" clipPathUnits=\"userSpaceOnUse\"{parent}><path d=\"{clip.Path}\"{(clip.EvenOdd ? " clip-rule=\"evenodd\"" : "")}{shift}/></clipPath>");
        return clip.Id;
    }

    /// <summary>The finished document; the view box is in device units.</summary>
    public string Finish(double x, double y, double width, double height)
    {
        if (clipOpen) body.Append("</g>");
        clipOpen = false;
        var sb = new StringBuilder();
        sb.Append(FormattableString.Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"{Num(x)} {Num(y)} {Num(Math.Max(width, 1e-3))} {Num(Math.Max(height, 1e-3))}\" preserveAspectRatio=\"none\" overflow=\"hidden\">"));
        if (defs.Length > 0) sb.Append("<defs>").Append(defs).Append("</defs>");
        sb.Append(body).Append("</svg>");
        return sb.ToString();
    }

    public static string Num(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
        return Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static string Color(uint colorRef) => $"#{colorRef & 0xFF:x2}{(colorRef >> 8) & 0xFF:x2}{(colorRef >> 16) & 0xFF:x2}";

    /// <summary>
    /// An 8 × 8 hatch tile as a pixelated image: the pixels <paramref name="set"/> names in the foreground color, the
    /// others in the background (transparent when null). GDI and GDI+ set whole device pixels (measured: crisp
    /// diagonal crosses in PowerPoint's rendering), which vector squares blur once the picture is scaled.
    /// </summary>
    public static string HatchTile(Func<int, int, bool> set, (byte R, byte G, byte B, byte A) foreground, (byte R, byte G, byte B, byte A)? background)
    {
        var rgba = new byte[8 * 8 * 4];
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var (r, g, b, a) = set(x, y) ? foreground : background ?? (0, 0, 0, 0);
                var i = (y * 8 + x) * 4;
                (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = (r, g, b, a);
            }
        }
        var uri = "data:image/png;base64," + Convert.ToBase64String(Png.Encode(8, 8, rgba));
        return $"<image width=\"8\" height=\"8\" href=\"{uri}\" style=\"image-rendering:pixelated\"/>";
    }

    public static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '&': sb.Append("&amp;"); break;
                case '"': sb.Append("&quot;"); break;
                default:
                    if (c >= 0x20 || c == '\t') sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}

/// <summary>Builds path data in device coordinates.</summary>
public sealed class PathBuilder
{
    readonly StringBuilder d = new();

    public bool IsEmpty => d.Length == 0;

    public void MoveTo((double X, double Y) p) => d.Append('M').Append(SvgWriter.Num(p.X)).Append(' ').Append(SvgWriter.Num(p.Y));

    public void LineTo((double X, double Y) p) => d.Append('L').Append(SvgWriter.Num(p.X)).Append(' ').Append(SvgWriter.Num(p.Y));

    public void CurveTo((double X, double Y) c1, (double X, double Y) c2, (double X, double Y) p) =>
        d.Append('C').Append(SvgWriter.Num(c1.X)).Append(' ').Append(SvgWriter.Num(c1.Y)).Append(' ')
            .Append(SvgWriter.Num(c2.X)).Append(' ').Append(SvgWriter.Num(c2.Y)).Append(' ')
            .Append(SvgWriter.Num(p.X)).Append(' ').Append(SvgWriter.Num(p.Y));

    public void Close() => d.Append('Z');

    public void Append(string data) => d.Append(data);

    public override string ToString() => d.ToString();

    /// <summary>An ellipse arc (center, radii in logical units) through a transform, as Bézier segments.</summary>
    public static IEnumerable<((double X, double Y) C1, (double X, double Y) C2, (double X, double Y) P)> ArcBeziers(
        double cx, double cy, double rx, double ry, double startRadians, double sweepRadians)
    {
        var segments = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweepRadians) / (Math.PI / 2) - 1e-9));
        var step = sweepRadians / segments;
        var k = 4.0 / 3 * Math.Tan(step / 4);
        var a = startRadians;
        for (var i = 0; i < segments; i++)
        {
            var (s0, c0) = Math.SinCos(a);
            var (s1, c1) = Math.SinCos(a + step);
            yield return (
                (cx + rx * (c0 - k * s0), cy + ry * (s0 + k * c0)),
                (cx + rx * (c1 + k * s1), cy + ry * (s1 - k * c1)),
                (cx + rx * c1, cy + ry * s1));
            a += step;
        }
    }
}
