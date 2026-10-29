using FreeSlate.Drawing;
using FreeSlate.Drawing.Geometry;
using FreeSlate.Rendering;

namespace FreeSlate.Html;

/// <summary>
/// Draws arrowheads (a:headEnd at the start of an open path, a:tailEnd at its end) and pulls the line back under
/// them, as PowerPoint does (catalog: line.ends). Sizes are multiples of the width the line is drawn with (at least
/// 2 pt): sm 2, med 3, lg 5; the open arrow's are its own (OpenWidth, OpenLength).
/// </summary>
public static class LineEnds
{
    static double Factor(string size) => size switch { "sm" => 2, "lg" => 5, _ => 3 };

    /// <summary>
    /// The open arrow (type arrow) in widths: its wing ends lie 2.5, 3.5 or 5 apart and 2, 3 or 4.5 behind its bend
    /// (measured with LibreOffice's sd-data/tdf100491 on lines of 0 to 5.25 pt, generated/lines at 2.25 and 6 pt,
    /// poi/customGeo at 3.25 pt and poi/aascu.org_workarea_downloadasset.aspx_id=5864 at 2.75 and 4 pt: each size
    /// within 2 %).
    /// </summary>
    static double OpenWidth(string size) => size switch { "sm" => 2.5, "lg" => 5, _ => 3.5 };

    static double OpenLength(string size) => size switch { "sm" => 2, "lg" => 4.5, _ => 3 };

    /// <summary>
    /// The pen of an open arrow: the line's, solid, with round ends and a sharp bend whatever the line's caps and
    /// joins (measured: flat-capped lines with mitered joins and with none given alike).
    /// </summary>
    public static Outline OpenPen(Outline outline) => outline with { Dash = "solid", Cap = "rnd", Join = "miter", MiterLimit = 10 };

    /// <summary>
    /// Adds the decorations of <paramref name="outline"/> to the first open, stroked path and returns them as shape items
    /// (filled with the outline's fill, an open arrow stroked with OpenPen), with the given paint attributes in HTML.
    /// </summary>
    /// <param name="drawnWidth">The width the line is drawn with where the renderer puts it on whole pixels as PowerPoint
    /// does (SlideRenderer.PixelWidth): the heads are multiples of it (measured: the medium triangles of 2.5 pt lines,
    /// 3.33 px, are 9 px; the large one of a 2 pt line, 2.67 px, 15 px; the medium ovals of 2.83 pt lines 12 px).</param>
    public static List<SceneItem> Apply(Outline? outline, List<EvaluatedPath> paths, string fillAttributes, string strokeAttributes,
        double? drawnWidth = null)
    {
        if (outline is not { Visible: true } || (outline.Head is not { Type: not "none" } && outline.Tail is not { Type: not "none" }))
            return [];
        var path = paths.FirstOrDefault(p => p.Stroke && p.Segments.Count > 1 && p.Segments.All(s => s.Command != 'Z'));
        if (path == null)
            return [];

        var lineWidth = drawnWidth ?? Math.Max(Units.ToPx(outline.WidthEmu), 0.5);
        var items = new List<SceneItem>();
        var segments = path.Segments;

        // Legs of no length at the ends (a short one rounded onto its neighbour's pixel, see SlideRenderer.OnPixels) give
        // no direction: an end's tangent comes from the leg next to it that has a length.
        var (firstLeg, last) = (1, segments.Count - 1);
        while (firstLeg < last && segments[firstLeg].Command == 'L' && segments[firstLeg].End == segments[0].End)
            firstLeg++;
        while (last > firstLeg && segments[last].Command == 'L' && segments[last].End == segments[last - 1].End)
            last--;

        // An arc at an end gives its tangent there and is pulled back along itself, keeping its curve (LibreOffice's
        // tdf113187_arcTo_withoutReferences: the arrows at the ends of arcs point along them).
        if (outline.Head is { Type: not "none" } head && segments[0].End is { } start && Reference(segments, firstLeg, first: true) is { } next)
        {
            var arc = segments[firstLeg].Command == 'A' ? Ellipse(start, segments[firstLeg]) : null;
            var direction = arc is { } e && Tangent(e, start) is var (tx, ty) ? Normalize(-e.Turn * tx, -e.Turn * ty) : Normalize(start.X - next.X, start.Y - next.Y);
            if (Shape(head, start, direction, lineWidth, outline, fillAttributes, strokeAttributes, out var pullback) is { } shape)
                items.Add(shape);
            if (direction != default && pullback > 0)
            {
                var moved = arc is { } along ? Along(along, start, pullback) : (start.X - direction.X * pullback, start.Y - direction.Y * pullback);
                for (var i = 0; i < firstLeg; i++)
                    Place(segments[i], moved);
            }
        }

        if (outline.Tail is { Type: not "none" } tail && segments[last].End is { } end && Reference(segments, last, first: false) is { } previous)
        {
            var arc = segments[last].Command == 'A' ? Ellipse(previous, segments[last]) : null;
            var direction = arc is { } e && Tangent(e, end) is var (tx, ty) ? Normalize(e.Turn * tx, e.Turn * ty) : Normalize(end.X - previous.X, end.Y - previous.Y);
            if (Shape(tail, end, direction, lineWidth, outline, fillAttributes, strokeAttributes, out var pullback) is { } shape)
                items.Add(shape);
            if (direction != default && pullback > 0)
            {
                var moved = arc is { } along ? Along(along, end, -pullback) : (end.X - direction.X * pullback, end.Y - direction.Y * pullback);
                for (var i = last; i < segments.Count; i++)
                    Place(segments[i], moved);
            }
        }
        return items;
    }

    /// <summary>The point that gives the tangent at a path end: the first control point after the start, the last before the end.</summary>
    static (double X, double Y)? Reference(List<PathSegment> segments, int index, bool first)
    {
        var segment = segments[index];
        if (first)
        {
            return segment.Command switch
            {
                'C' or 'Q' => (segment.Values[0], segment.Values[1]),
                _ => segment.End,
            };
        }
        return segment.Command switch
        {
            'C' => (segment.Values[2], segment.Values[3]),
            'Q' => (segment.Values[0], segment.Values[1]),
            _ => index > 0 ? segments[index - 1].End : null,
        };
    }

    static void Place(PathSegment segment, (double X, double Y) point)
    {
        segment.Values[^2] = point.X;
        segment.Values[^1] = point.Y;
    }

    /// <summary>
    /// The ellipse an SVG arc (not turned) from <paramref name="from"/> lies on: its center, its radii as drawn (grown
    /// where the ends lie too far apart) and its way round (+1 with the angle, -1 against); null for an empty one.
    /// </summary>
    static (double Cx, double Cy, double Rx, double Ry, double Turn)? Ellipse((double X, double Y) from, PathSegment arc)
    {
        var v = arc.Values;
        var (rx, ry, large, sweep, x, y) = (Math.Abs(v[0]), Math.Abs(v[1]), v[3] != 0, v[4] != 0, v[5], v[6]);
        var (hx, hy) = ((from.X - x) / 2, (from.Y - y) / 2);
        if (rx < 1e-9 || ry < 1e-9 || Math.Abs(hx) + Math.Abs(hy) < 1e-9)
            return null;
        var lambda = hx * hx / (rx * rx) + hy * hy / (ry * ry);
        if (lambda > 1)
            (rx, ry) = (rx * Math.Sqrt(lambda), ry * Math.Sqrt(lambda));
        var rest = rx * rx * ry * ry - rx * rx * hy * hy - ry * ry * hx * hx;
        var k = (large != sweep ? 1 : -1) * Math.Sqrt(Math.Max(0, rest / (rx * rx * hy * hy + ry * ry * hx * hx)));
        return (k * rx * hy / ry + (from.X + x) / 2, -k * ry * hx / rx + (from.Y + y) / 2, rx, ry, sweep ? 1 : -1);
    }

    static double Angle((double Cx, double Cy, double Rx, double Ry, double Turn) e, (double X, double Y) p) =>
        Math.Atan2((p.Y - e.Cy) / e.Ry, (p.X - e.Cx) / e.Rx);

    /// <summary>The ellipse's derivative by its angle at <paramref name="p"/>.</summary>
    static (double X, double Y) Tangent((double Cx, double Cy, double Rx, double Ry, double Turn) e, (double X, double Y) p) =>
        Angle(e, p) is var angle ? (-e.Rx * Math.Sin(angle), e.Ry * Math.Cos(angle)) : default;

    /// <summary>The point <paramref name="distance"/> on from <paramref name="p"/> the arc's way round (back where negative).</summary>
    static (double X, double Y) Along((double Cx, double Cy, double Rx, double Ry, double Turn) e, (double X, double Y) p, double distance)
    {
        var angle = Angle(e, p);
        var speed = Math.Max(Math.Sqrt(Math.Pow(e.Rx * Math.Sin(angle), 2) + Math.Pow(e.Ry * Math.Cos(angle), 2)), 1e-9);
        angle += e.Turn * distance / speed;
        return (e.Cx + e.Rx * Math.Cos(angle), e.Cy + e.Ry * Math.Sin(angle));
    }

    static (double X, double Y) Normalize(double x, double y)
    {
        var length = Math.Sqrt(x * x + y * y);
        return length < 1e-9 ? default : (x / length, y / length);
    }

    static SceneItem? Shape(LineEnd end, (double X, double Y) tip, (double X, double Y) u, double lineWidth, Outline outline, string fill, string stroke,
        out double pullback)
    {
        pullback = 0;
        if (u == default)
            return null;
        // Heads of lines drawn thinner than 2 pt keep the size of a 2 pt line's (measured: medium heads 6 pt wide on
        // lines of 0.25 to 1.75 pt, 3 drawn widths wide beyond).
        var size = Math.Max(lineWidth, 2 * 96 / 72.0);
        var length = size * Factor(end.Length);
        var width = size * Factor(end.Width);
        var v = (X: -u.Y, Y: u.X);
        string P(double along, double across) =>
            $"{Units.Css(tip.X + u.X * along + v.X * across)} {Units.Css(tip.Y + u.Y * along + v.Y * across)}";

        switch (end.Type)
        {
            case "triangle":
                pullback = length;
                return new ShapePathItem($"M{P(0, 0)}L{P(-length, width / 2)}L{P(-length, -width / 2)}Z", outline.Fill, null, fill);
            case "stealth":
                pullback = length * 0.5;
                return new ShapePathItem($"M{P(0, 0)}L{P(-length, width / 2)}L{P(-length * 0.5, 0)}L{P(-length, -width / 2)}Z", outline.Fill, null, fill);
            case "diamond":
                return new ShapePathItem($"M{P(length / 2, 0)}L{P(0, width / 2)}L{P(-length / 2, 0)}L{P(0, -width / 2)}Z", outline.Fill, null, fill);
            case "oval":
                return new ShapeEllipseItem(tip.X, tip.Y, length / 2, width / 2, Math.Atan2(u.Y, u.X) * 180 / Math.PI, outline.Fill, fill);
            case "arrow":
            {
                // Bent the stroke's half width over the sine of its half angle behind the line's end, so that its outer
                // point lies on the end (measured: tdf100491's tips where its lines end, 0.1 px in, the bends 1 to 10 px
                // behind); the line ends at the bend, under the stroke.
                var (half, back) = (size * OpenWidth(end.Width) / 2, size * OpenLength(end.Length));
                var bend = lineWidth / 2 * Math.Sqrt(half * half + back * back) / half;
                pullback = bend;
                return new ShapePathItem($"M{P(-bend - back, half)}L{P(-bend, 0)}L{P(-bend - back, -half)}", Fill.None, OpenPen(outline),
                    $" fill=\"none\"{stroke}");
            }
            default:
                return null;
        }
    }
}
