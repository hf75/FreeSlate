using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Drawing.Geometry;
using FreeSlate.Html;

namespace FreeSlate.Pptx;

/// <summary>
/// WordArt transforms (a:prstTxWarp, catalog: text.warp): glyph outlines bent along the guide paths of the
/// preset (ECMA-376 presetTextWarpDefinitions). Paths come in pairs that form envelopes: the text's ink box is
/// stretched between the upper and the lower path, measured in arc length (measured in PowerPoint: the glyphs
/// fill the envelope from their tops to their baselines). Presets with an odd number of paths (arch, circle,
/// button) carry one line per path: the baseline follows the path, glyph heights stay.
/// </summary>
public static class TextWarp
{
    static readonly Lazy<Dictionary<string, GeometryDefinition>> Presets = new(() =>
    {
        using var stream = typeof(TextWarp).Assembly.GetManifestResourceStream("presetTextWarpDefinitions.xml")
                           ?? throw new InvalidOperationException("Missing resource presetTextWarpDefinitions.xml");
        return XDocument.Load(stream).Root!.Elements().ToDictionary(e => e.Name.LocalName, GeometryDefinition.Parse, StringComparer.Ordinal);
    });

    /// <summary>The preset's definition, or null for textNoShape and unknown presets.</summary>
    public static GeometryDefinition? Definition(string preset) =>
        preset == "textNoShape" ? null : Presets.Value.GetValueOrDefault(preset);

    /// <summary>A glyph outline as closed contours in pixels, with the line it belongs to and that line's baseline.</summary>
    public sealed record Glyph(List<List<(double X, double Y)>> Contours, int Line, double Baseline);

    /// <summary>Warps the glyphs into the preset's paths laid into <paramref name="box"/> (pixels).</summary>
    public static List<List<List<(double X, double Y)>>> Apply(GeometryDefinition definition, IEnumerable<Guide> adjustments,
        (double L, double T, double W, double H) box, IReadOnlyList<Glyph> glyphs)
    {
        var geometry = GeometryEvaluator.Evaluate(definition, box.W * Units.EmuPerPixel, box.H * Units.EmuPerPixel, adjustments);
        var paths = geometry.Paths
            .Select(p => new Polyline(CompoundLines.Flatten(p.Segments).SelectMany(f => f.Points).Select(pt => (pt.X + box.L, pt.Y + box.T)).ToList()))
            .Where(p => p.Length > 0)
            .ToList();
        var result = glyphs.Select(g => g.Contours.Select(c => c.ToList()).ToList()).ToList();
        if (paths.Count == 0 || glyphs.Count == 0)
            return result;
        var lines = glyphs.Max(g => g.Line) + 1;

        if (paths.Count % 2 == 0)
        {
            // Envelopes: consecutive pairs of paths; lines are shared out among them in order.
            var envelopes = paths.Count / 2;
            for (var e = 0; e < envelopes; e++)
            {
                var members = Enumerable.Range(0, glyphs.Count).Where(i => Math.Min(envelopes - 1, glyphs[i].Line * envelopes / Math.Max(1, lines)) == e).ToList();
                if (members.Count == 0) continue;
                var (minX, minY, maxX, maxY) = Bounds(members.SelectMany(i => glyphs[i].Contours));
                var (top, bottom) = (paths[2 * e], paths[2 * e + 1]);
                foreach (var i in members)
                {
                    foreach (var contour in result[i])
                    {
                        for (var k = 0; k < contour.Count; k++)
                        {
                            var u = maxX > minX ? (contour[k].X - minX) / (maxX - minX) : 0.5;
                            var v = maxY > minY ? (contour[k].Y - minY) / (maxY - minY) : 0.5;
                            var (a, _) = top.At(u);
                            var (b, _) = bottom.At(u);
                            contour[k] = (a.X + (b.X - a.X) * v, a.Y + (b.Y - a.Y) * v);
                        }
                    }
                }
            }
            return result;
        }

        // One line per path, the middle path for a single line of a three-path preset.
        for (var line = 0; line < lines; line++)
        {
            var members = Enumerable.Range(0, glyphs.Count).Where(i => glyphs[i].Line == line).ToList();
            if (members.Count == 0) continue;
            var path = paths[lines == 1 ? paths.Count / 2 : Math.Min(line, paths.Count - 1)];
            var (minX, _, maxX, _) = Bounds(members.SelectMany(i => glyphs[i].Contours));
            var baseline = glyphs[members[0]].Baseline;
            foreach (var i in members)
            {
                foreach (var contour in result[i])
                {
                    for (var k = 0; k < contour.Count; k++)
                    {
                        var u = maxX > minX ? (contour[k].X - minX) / (maxX - minX) : 0.5;
                        var height = baseline - contour[k].Y;
                        var (p, tangent) = path.At(u);
                        // The normal to the left of the direction points up for left-to-right paths.
                        contour[k] = (p.X + tangent.Y * height, p.Y - tangent.X * height);
                    }
                }
            }
        }
        return result;
    }

    static (double MinX, double MinY, double MaxX, double MaxY) Bounds(IEnumerable<List<(double X, double Y)>> contours)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var contour in contours)
        {
            foreach (var (x, y) in contour)
            {
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
        }
        return (minX, minY, maxX, maxY);
    }

    /// <summary>Glyph outline path data (font units, y down; M, L, Q, C, Z) as contours in pixels, curves and long lines subdivided.</summary>
    public static List<List<(double X, double Y)>> Contours(string outline, double originX, double originY, double scale)
    {
        var contours = new List<List<(double X, double Y)>>();
        var tokens = System.Text.RegularExpressions.Regex.Matches(outline, "[MLQCZ]|-?\\d*\\.?\\d+(?:e[-+]?\\d+)?");
        List<(double X, double Y)>? current = null;
        var command = 'M';
        var index = 0;
        double Next() => double.Parse(tokens[index++].Value, CultureInfo.InvariantCulture);
        (double X, double Y) Point() => (originX + Next() * scale, originY + Next() * scale);
        void Line((double X, double Y) to)
        {
            var from = current![^1];
            var steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Y - from.Y) * (to.Y - from.Y)) / 3));
            for (var s = 1; s <= steps; s++)
                current.Add((from.X + (to.X - from.X) * s / steps, from.Y + (to.Y - from.Y) * s / steps));
        }
        while (index < tokens.Count)
        {
            var token = tokens[index].Value;
            if (char.IsLetter(token[0]))
            {
                command = token[0];
                index++;
                if (command == 'Z')
                {
                    if (current is { Count: > 2 }) contours.Add(current);
                    current = null;
                    continue;
                }
            }
            switch (command)
            {
                case 'M':
                    if (current is { Count: > 2 }) contours.Add(current);
                    current = [Point()];
                    command = 'L';
                    break;
                case 'L':
                    Line(Point());
                    break;
                case 'Q':
                {
                    var p0 = current![^1];
                    var (p1, p2) = (Point(), Point());
                    for (var s = 1; s <= 8; s++)
                    {
                        var t = s / 8.0;
                        var u = 1 - t;
                        current.Add((u * u * p0.X + 2 * u * t * p1.X + t * t * p2.X, u * u * p0.Y + 2 * u * t * p1.Y + t * t * p2.Y));
                    }
                    break;
                }
                case 'C':
                {
                    var p0 = current![^1];
                    var (p1, p2, p3) = (Point(), Point(), Point());
                    for (var s = 1; s <= 10; s++)
                    {
                        var t = s / 10.0;
                        var u = 1 - t;
                        current.Add((u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X,
                            u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y));
                    }
                    break;
                }
                default:
                    index++;
                    break;
            }
        }
        if (current is { Count: > 2 }) contours.Add(current);
        return contours;
    }

    /// <summary>Contours as SVG path data.</summary>
    public static string PathData(IEnumerable<List<(double X, double Y)>> contours)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var contour in contours)
        {
            sb.Append('M').Append(Units.Css(contour[0].X)).Append(' ').Append(Units.Css(contour[0].Y));
            for (var i = 1; i < contour.Count; i++)
                sb.Append('L').Append(Units.Css(contour[i].X)).Append(' ').Append(Units.Css(contour[i].Y));
            sb.Append('Z');
        }
        return sb.ToString();
    }

    /// <summary>A flattened guide path with cumulative lengths.</summary>
    sealed class Polyline
    {
        readonly List<(double X, double Y)> points;
        readonly double[] lengths;

        public Polyline(List<(double X, double Y)> points)
        {
            this.points = points;
            lengths = new double[points.Count];
            for (var i = 1; i < points.Count; i++)
                lengths[i] = lengths[i - 1] + Math.Sqrt((points[i].X - points[i - 1].X) * (points[i].X - points[i - 1].X) + (points[i].Y - points[i - 1].Y) * (points[i].Y - points[i - 1].Y));
        }

        public double Length => lengths.Length > 0 ? lengths[^1] : 0;

        /// <summary>The point at a fraction of the length and the unit tangent there.</summary>
        public ((double X, double Y) Point, (double X, double Y) Tangent) At(double fraction)
        {
            var d = Math.Clamp(fraction, 0, 1) * Length;
            var i = Array.BinarySearch(lengths, d);
            if (i < 0) i = ~i;
            i = Math.Clamp(i, 1, points.Count - 1);
            var (a, b) = (points[i - 1], points[i]);
            var span = lengths[i] - lengths[i - 1];
            var t = span > 0 ? (d - lengths[i - 1]) / span : 0;
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            return ((a.X + dx * t, a.Y + dy * t), length > 0 ? (dx / length, dy / length) : (1, 0));
        }
    }
}
