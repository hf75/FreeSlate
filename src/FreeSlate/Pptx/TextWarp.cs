using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Drawing.Geometry;
using FreeSlate.Html;

namespace FreeSlate.Pptx;

/// <summary>
/// WordArt transforms (a:prstTxWarp, catalog: text.warp): glyph outlines bent along the guide paths of the
/// preset (ECMA-376 presetTextWarpDefinitions). Paths come in pairs that form envelopes: the text's ink box is
/// stretched between the upper and the lower path, measured in arc length, every envelope at the same scale
/// (measured in PowerPoint: the glyphs fill the envelope from their tops to their baselines; the lines of
/// textButtonPour share one scale). Presets with an odd number of paths (arch, circle, button) share their lines out
/// among the paths: the baselines follow the paths with every glyph rigid at its size, placed along them by the
/// lines' alignment and shrunk only when longer than PowerPoint measures the path; text converted from old WordArt
/// (bodyPr fromWordArt) is scaled evenly to the path's length instead (measured with textArchUp).
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

    /// <summary>
    /// Guide paths of a warp's own in place of its preset's (an a:pathLst in it): VML WordArt's, whose shape types
    /// bring their own curves.
    /// </summary>
    public static readonly XName OwnPaths = XNamespace.Get("urn:freeslate:warp") + "paths";

    /// <summary>A warp's definition (a:prstTxWarp): its own guide paths, else its preset's.</summary>
    public static GeometryDefinition? Definition(XElement warp) =>
        warp.Element(OwnPaths) is { } own ? GeometryDefinition.Parse(own)
        : (string?)warp.Attribute("prst") is { } preset ? Definition(preset) : null;

    /// <summary>A glyph outline as closed contours in pixels, with the line it belongs to and that line's baseline.</summary>
    public sealed record Glyph(List<List<(double X, double Y)>> Contours, int Line, double Baseline);

    /// <summary>
    /// A laid-out line (pixels): its baseline, how far below a single path that baseline lies when the line is the one
    /// next to the path, and the top and bottom of its line box.
    /// </summary>
    public sealed record Line(double Baseline, double Drop, double Top, double Bottom);

    /// <summary>
    /// Presets whose lines follow paths (an odd number of them: arches, circle, button), which are not wrapped
    /// (measured: a line longer than the text box stays whole on the lower arc of textButton).
    /// </summary>
    public static bool FollowsPaths(GeometryDefinition definition) => definition.Paths.Count % 2 == 1;

    /// <summary>
    /// Warps the glyphs into the preset's paths laid into <paramref name="box"/> (pixels); <paramref name="align"/> gives
    /// each line's paragraph alignment, its free space at the start and end of the text box and its width (pixels).
    /// </summary>
    public static List<List<List<(double X, double Y)>>> Apply(GeometryDefinition definition, IEnumerable<Guide> adjustments,
        (double L, double T, double W, double H) box, IReadOnlyList<Glyph> glyphs, bool fromWordArt = false,
        Func<int, (string Align, double Before, double After, double Width)>? align = null,
        IReadOnlyList<Line>? lines = null)
    {
        var geometry = GeometryEvaluator.Evaluate(definition, box.W * Units.EmuPerPixel, box.H * Units.EmuPerPixel, adjustments);
        var follows = FollowsPaths(definition);
        var paths = geometry.Paths
            .Select(p => (follows ? Arc(p.Segments, box.L, box.T) : null)
                         ?? new Polyline(CompoundLines.Flatten(p.Segments).SelectMany(f => f.Points).Select(pt => (pt.X + box.L, pt.Y + box.T)).ToList()))
            .Where(p => p.Length > 0)
            .ToList();
        var result = glyphs.Select(g => g.Contours.Select(c => c.ToList()).ToList()).ToList();
        if (paths.Count == 0 || glyphs.Count == 0)
            return result;
        // Empty lines count when the lines are shared out (measured: a fourth, empty paragraph takes a button's lower arc).
        var lineCount = Math.Max(glyphs.Max(g => g.Line) + 1, lines?.Count ?? 0);

        if (paths.Count % 2 == 0)
        {
            // Envelopes: consecutive pairs of paths; lines are shared out among them in order. All envelopes share one
            // scale each way (measured with textButtonPour, one to four lines): along, the envelope whose ink is
            // longest for the middle of the envelope (the mean of its paths' lengths) sets the scale and is filled,
            // the others' ink taking its part of their envelope where the alignment puts it; across, the tallest ink
            // fills its envelope and the others' is centered at that scale. A single envelope is filled both ways.
            var envelopes = paths.Count / 2;
            var groups = Enumerable.Range(0, envelopes)
                .Select(e => Enumerable.Range(0, glyphs.Count).Where(i => Math.Min(envelopes - 1, glyphs[i].Line * envelopes / Math.Max(1, lineCount)) == e).ToList())
                .ToList();
            var bounds = groups.Select(g => g.Count > 0 ? Bounds(g.SelectMany(i => glyphs[i].Contours)) : default).ToList();
            // A line without ink takes its line box across, in its envelope and for the scale across (measured: an
            // empty paragraph after a line of A's, in textButtonPour's middle envelope or textDeflateInflate's lower one,
            // leaves the A's 0.56 of their envelope's height, centered; in the A's own envelope it keeps them at its top).
            var heights = bounds.Select((b, e) => groups[e].Count > 0 ? b.MaxY - b.MinY : 0.0).ToList();
            for (var l = 0; lines != null && l < lines.Count; l++)
            {
                if (glyphs.Any(g => g.Line == l))
                    continue;
                var e = Math.Min(envelopes - 1, l * envelopes / Math.Max(1, lineCount));
                var (top, bottom) = (lines[l].Top, lines[l].Bottom);
                if (groups[e].Count > 0)
                {
                    var (minX, minY, maxX, maxY) = bounds[e];
                    bounds[e] = (minX, Math.Min(minY, top), maxX, Math.Max(maxY, bottom));
                    heights[e] = bounds[e].MaxY - bounds[e].MinY;
                }
                else
                    heights[e] = Math.Max(heights[e], bottom - top);
            }
            var middles = Enumerable.Range(0, envelopes).Select(e => (paths[2 * e].Length + paths[2 * e + 1].Length) / 2).ToList();
            var along = double.MaxValue;
            var tallest = 0.0;
            for (var e = 0; e < envelopes; e++)
            {
                tallest = Math.Max(tallest, heights[e]);
                var (minX, _, maxX, _) = bounds[e];
                if (groups[e].Count > 0 && maxX > minX)
                    along = Math.Min(along, middles[e] / (maxX - minX));
            }
            for (var e = 0; e < envelopes; e++)
            {
                var members = groups[e];
                if (members.Count == 0) continue;
                var (minX, minY, maxX, maxY) = bounds[e];
                var (top, bottom) = (paths[2 * e], paths[2 * e + 1]);
                var share = along < double.MaxValue && middles[e] > 0 ? Math.Min(1, (maxX - minX) * along / middles[e]) : 1;
                var offset = (1 - share) * ((align?.Invoke(glyphs[members[0]].Line).Align ?? "l") switch { "ctr" => 0.5, "r" => 1, _ => 0 });
                var middleY = (minY + maxY) / 2;
                foreach (var i in members)
                {
                    foreach (var contour in result[i])
                    {
                        for (var k = 0; k < contour.Count; k++)
                        {
                            var u = maxX > minX ? offset + (contour[k].X - minX) / (maxX - minX) * share : 0.5;
                            var v = tallest > 0 ? 0.5 + (contour[k].Y - middleY) / tallest : 0.5;
                            var (a, _) = top.At(u);
                            var (b, _) = bottom.At(u);
                            contour[k] = (a.X + (b.X - a.X) * v, a.Y + (b.Y - a.Y) * v);
                        }
                    }
                }
            }
            return result;
        }

        // Lines on paths (an odd number: arches and the circle have one, the button three): the lines are shared out
        // among the paths in order as among envelopes (measured with textButton: one line lies on the upper arc, two on
        // the arc and the middle line, three on all three paths, four with two on the upper arc), and each path's
        // lines follow it as on a single path (measured: a line's baseline lies its drop below the middle line, 4.8 px
        // in 24 pt Aptos; left and right aligned lines reach the ends of their paths).
        var laidOut = lines ?? Enumerable.Range(0, lineCount).Select(l => glyphs.FirstOrDefault(g => g.Line == l)?.Baseline ?? 0).Select(b => new Line(b, 0, b, b)).ToList();
        for (var p = 0; p < paths.Count; p++)
        {
            var group = Enumerable.Range(0, lineCount).Where(l => Math.Min(paths.Count - 1, l * paths.Count / lineCount) == p).ToList();
            if (group.Count > 0)
                FollowPath(paths[p], box, glyphs, result, laidOut, fromWordArt, align, group);
        }
        return result;
    }

    /// <summary>
    /// The lines of <paramref name="group"/> on one path (arches, circle, a button's arcs and middle line), as measured
    /// in PowerPoint: every line follows a parallel of the path, a line pitch apart, the lines lying on the side of
    /// the path away from the box's center (the rings of a circle grow outwards with the last line innermost; under an
    /// arch down the first line lies on the path, the others below it). The line next to the path has its baseline its
    /// drop below the path (0.79 em under the top of its line box). Each line starts at its parallel's start or as
    /// aligned, insets not counted; glyphs stay rigid at their size, all lines shrunk alike when the longest is longer
    /// than the path's reach, and old WordArt (fromWordArt) scales all lines alike, so that the longest fills the path.
    /// </summary>
    static void FollowPath(Polyline path, (double L, double T, double W, double H) box, IReadOnlyList<Glyph> glyphs,
        List<List<List<(double X, double Y)>>> result, IReadOnlyList<Line> lines, bool fromWordArt,
        Func<int, (string Align, double Before, double After, double Width)>? align, IReadOnlyList<int> group)
    {
        // Which side is up for the glyphs at the path's middle: away from the box's center or towards it.
        var (middlePoint, middleTangent) = path.AtDistance(path.PathLength / 2);
        var (cx, cy) = (box.L + box.W / 2, box.T + box.H / 2);
        var outward = middleTangent.Y * (middlePoint.X - cx) - middleTangent.X * (middlePoint.Y - cy) >= 0;
        var reference = outward ? group[^1] : group[0];

        var widths = group.Select(i => Math.Max(1e-6, align?.Invoke(i).Width ?? 0)).ToList();
        // Text longer than its path as PowerPoint measures it shrinks until it fits (LibreOffice's tdf116350-texteffects:
        // an arch of 56° holds the whole line, small); old WordArt fills the path's own length (measured with circles
        // and a wide arch down).
        var scale = fromWordArt ? path.PathLength / widths.Max() : Math.Min(1, path.Reach / widths.Max());
        foreach (var line in group)
        {
            var members = Enumerable.Range(0, glyphs.Count).Where(i => glyphs[i].Line == line).ToList();
            if (members.Count == 0)
                continue;
            // The line's parallel: a line pitch per line above the path (towards the glyphs' tops); glyphs are placed
            // along it by arc length and then lowered by the drop (measured: the spacing of the glyphs is the spacing on
            // the parallel, not on the lowered baseline).
            var parallel = path.Offset((lines[reference].Baseline - lines[line].Baseline) * scale);
            var drop = lines[reference].Drop * scale;
            var (minX, _, maxX, _) = Bounds(members.SelectMany(i => glyphs[i].Contours));
            var (mode, before, _, advance) = align?.Invoke(line) ?? ("l", minX - box.L, box.L + box.W - maxX, maxX - minX);
            var width = Math.Max(1e-6, advance) * scale;
            var lineStart = box.L + before;
            // Aligned on its parallel; a line longer than that is centered on it, running on past both ends (measured:
            // a left-aligned line shrunk on an arch lies centered, the shorter line on the next parallel starts at its
            // start).
            var length = parallel.PathLength;
            var start = width > length ? (length - width) / 2 : mode switch
            {
                "ctr" => (length - width) / 2,
                "r" => length - width,
                _ => 0,
            };
            var baseline = lines[line].Baseline;
            foreach (var i in members)
            {
                var (glyphLeft, _, glyphRight, _) = Bounds(glyphs[i].Contours);
                var middle = (glyphLeft + glyphRight) / 2;
                var (p, tangent) = parallel.AtDistance(start + (middle - lineStart) * scale);
                foreach (var contour in result[i])
                {
                    for (var k = 0; k < contour.Count; k++)
                    {
                        var along = (contour[k].X - middle) * scale;
                        var height = (baseline - contour[k].Y) * scale - drop;
                        contour[k] = (p.X + tangent.X * along + tangent.Y * height, p.Y + tangent.Y * along - tangent.X * height);
                    }
                }
            }
        }
    }

    /// <summary>
    /// A path of arcs on one ellipse, continued on the ellipse half a turn beyond either end (measured: text longer than
    /// an arch runs on along its ellipse), and measured as PowerPoint fits lines to it: by its width radius times its
    /// sweep, as if it were a circle (measured with textArchUp: eight H's shrink to 177.6 px a radian on arches of 10 to
    /// 40 degrees with a width radius of 180 px, to 91 px a radian with one of 90 px). Null for other paths.
    /// </summary>
    static Polyline? Arc(List<PathSegment> segments, double offsetX, double offsetY)
    {
        if (segments.Count < 2 || segments[0].Command != 'M' || segments.Skip(1).Any(s => s.Command != 'A'))
            return null;
        var (x0, y0) = (segments[0].Values[0], segments[0].Values[1]);
        var arc = segments[1].Values;
        var (rx, ry) = (arc[0], arc[1]);
        if (rx <= 0 || ry <= 0 || segments.Skip(1).Any(s => Math.Abs(s.Values[0] - rx) > 1e-6 || Math.Abs(s.Values[1] - ry) > 1e-6))
            return null;
        // The center from the first arc's end points and flags (SVG's endpoint parameterization, no rotation).
        var (x1, y1) = (arc[5], arc[6]);
        var (hx, hy) = ((x0 - x1) / 2, (y0 - y1) / 2);
        var lambda = hx * hx / (rx * rx) + hy * hy / (ry * ry);
        if (lambda > 1)
            (rx, ry) = (rx * Math.Sqrt(lambda), ry * Math.Sqrt(lambda));
        var numerator = rx * rx * ry * ry - rx * rx * hy * hy - ry * ry * hx * hx;
        var denominator = rx * rx * hy * hy + ry * ry * hx * hx;
        var root = denominator > 0 ? Math.Sqrt(Math.Max(0, numerator / denominator)) : 0;
        var sign = (arc[3] != 0) == (arc[4] != 0) ? -1 : 1;
        var cx = sign * root * rx * hy / ry + (x0 + x1) / 2;
        var cy = sign * root * -ry * hx / rx + (y0 + y1) / 2;

        // The sweep, as the ellipse's parameter and as the visual angle, piece by piece.
        var direction = arc[4] != 0 ? 1 : -1;
        double Param(double x, double y) => Math.Atan2((y - cy) / ry, (x - cx) / rx);
        double Visual(double x, double y) => Math.Atan2(y - cy, x - cx);
        double Turn(double from, double to)
        {
            var d = (to - from) * direction;
            while (d <= 1e-9) d += 2 * Math.PI;
            while (d > 2 * Math.PI + 1e-9) d -= 2 * Math.PI;
            return d;
        }
        double sweep = 0, visual = 0;
        var (px, py) = (x0, y0);
        foreach (var segment in segments.Skip(1))
        {
            var (ex, ey) = (segment.Values[5], segment.Values[6]);
            sweep += Turn(Param(px, py), Param(ex, ey));
            visual += Turn(Visual(px, py), Visual(ex, ey));
            (px, py) = (ex, ey);
        }
        var start = Param(x0, y0);
        var step = 1.0 / Math.Max(rx, ry);
        var before = (int)Math.Ceiling(Math.PI / step);
        var within = Math.Max(1, (int)Math.Ceiling(sweep / step));
        var points = new List<(double X, double Y)>();
        for (var i = -before; i <= within + before; i++)
        {
            var t = start + direction * (i < 0 ? i * step : i <= within ? sweep * i / within : sweep + (i - within) * step);
            points.Add((cx + rx * Math.Cos(t) + offsetX, cy + ry * Math.Sin(t) + offsetY));
        }
        return new Polyline(points, before, before + within, arc[0] * visual);
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
        readonly int first, last;

        /// <summary>
        /// A polyline whose points <paramref name="first"/> to <paramref name="last"/> are the path itself, those before
        /// and after it its continuation; <paramref name="reach"/> is the path's length as PowerPoint fits lines to it.
        /// </summary>
        public Polyline(List<(double X, double Y)> points, int first = 0, int last = -1, double? reach = null)
        {
            this.points = points;
            lengths = new double[points.Count];
            for (var i = 1; i < points.Count; i++)
                lengths[i] = lengths[i - 1] + Math.Sqrt((points[i].X - points[i - 1].X) * (points[i].X - points[i - 1].X) + (points[i].Y - points[i - 1].Y) * (points[i].Y - points[i - 1].Y));
            this.first = Math.Clamp(first, 0, Math.Max(0, points.Count - 1));
            this.last = last < 0 ? Math.Max(0, points.Count - 1) : Math.Clamp(last, this.first, Math.Max(0, points.Count - 1));
            Reach = reach ?? PathLength;
        }

        public double Length => lengths.Length > 0 ? lengths[^1] : 0;

        /// <summary>The length of the path itself, without its continuation.</summary>
        public double PathLength => lengths.Length > 0 ? lengths[last] - lengths[first] : 0;

        /// <summary>The path's length as PowerPoint fits lines to it.</summary>
        public double Reach { get; }

        /// <summary>The point <paramref name="distance"/> pixels from the path's start (before it when negative) and the unit tangent there.</summary>
        public ((double X, double Y) Point, (double X, double Y) Tangent) AtDistance(double distance) =>
            At(Length > 0 ? (lengths[first] + distance) / Length : 0.5);

        /// <summary>The parallel curve <paramref name="distance"/> pixels to the left of the direction (up for text on it).</summary>
        public Polyline Offset(double distance)
        {
            if (Math.Abs(distance) < 1e-9 || points.Count < 2)
                return this;
            var shifted = new List<(double X, double Y)>(points.Count);
            for (var i = 0; i < points.Count; i++)
            {
                var (a, b) = (points[Math.Max(0, i - 1)], points[Math.Min(points.Count - 1, i + 1)]);
                var (dx, dy) = (b.X - a.X, b.Y - a.Y);
                var length = Math.Sqrt(dx * dx + dy * dy);
                var (nx, ny) = length > 0 ? (dy / length, -dx / length) : (0.0, -1.0);
                shifted.Add((points[i].X + nx * distance, points[i].Y + ny * distance));
            }
            return new Polyline(shifted, first, last, Reach);
        }

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
