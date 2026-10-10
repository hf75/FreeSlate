using System.Text;
using FreeSlate.Drawing;
using FreeSlate.Drawing.Geometry;

namespace FreeSlate.Html;

/// <summary>
/// Draws arrowheads (a:headEnd at the start of an open path, a:tailEnd at its end) and pulls the line back under
/// them, as PowerPoint does (catalog: line.ends). Sizes are multiples of the line width (at least 2 pt): sm 2, med 3, lg 5.
/// </summary>
public static class LineEnds
{
    static double Factor(string size) => size switch { "sm" => 2, "lg" => 5, _ => 3 };

    /// <summary>Adds the decorations of <paramref name="outline"/> to the first open, stroked path and returns their SVG.</summary>
    public static string Apply(Outline? outline, List<EvaluatedPath> paths, string fillAttributes, string strokeAttributes)
    {
        if (outline is not { Visible: true } || (outline.Head is not { Type: not "none" } && outline.Tail is not { Type: not "none" }))
            return "";
        var path = paths.FirstOrDefault(p => p.Stroke && p.Segments.Count > 1 && p.Segments.All(s => s.Command != 'Z'));
        if (path == null)
            return "";

        var lineWidth = Math.Max(Units.ToPx(outline.WidthEmu), 0.5);
        var svg = new StringBuilder();
        var segments = path.Segments;

        if (outline.Head is { Type: not "none" } head && segments[0].End is { } start && Reference(segments, 1, first: true) is { } next)
        {
            var direction = Normalize(start.X - next.X, start.Y - next.Y);
            svg.Append(Shape(head, start, direction, lineWidth, fillAttributes, strokeAttributes, out var pullback));
            if (direction != default && pullback > 0)
                Move(segments[0], -direction.X * pullback, -direction.Y * pullback);
        }

        var last = segments.Count - 1;
        if (outline.Tail is { Type: not "none" } tail && segments[last].End is { } end && Reference(segments, last, first: false) is { } previous)
        {
            var direction = Normalize(end.X - previous.X, end.Y - previous.Y);
            svg.Append(Shape(tail, end, direction, lineWidth, fillAttributes, strokeAttributes, out var pullback));
            if (direction != default && pullback > 0)
                Move(segments[last], -direction.X * pullback, -direction.Y * pullback);
        }
        return svg.ToString();
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

    static void Move(PathSegment segment, double dx, double dy)
    {
        segment.Values[^2] += dx;
        segment.Values[^1] += dy;
    }

    static (double X, double Y) Normalize(double x, double y)
    {
        var length = Math.Sqrt(x * x + y * y);
        return length < 1e-9 ? default : (x / length, y / length);
    }

    static string Shape(LineEnd end, (double X, double Y) tip, (double X, double Y) u, double lineWidth, string fill, string stroke, out double pullback)
    {
        pullback = 0;
        if (u == default)
            return "";
        // Heads of lines thinner than 2 pt keep the size of a 2 pt line's (measured: medium heads 6 pt wide on lines
        // of 0.25 to 2 pt, 3 widths wide beyond).
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
                return $"<path d=\"M{P(0, 0)}L{P(-length, width / 2)}L{P(-length, -width / 2)}Z\"{fill}/>";
            case "stealth":
                pullback = length * 0.5;
                return $"<path d=\"M{P(0, 0)}L{P(-length, width / 2)}L{P(-length * 0.5, 0)}L{P(-length, -width / 2)}Z\"{fill}/>";
            case "diamond":
                return $"<path d=\"M{P(length / 2, 0)}L{P(0, width / 2)}L{P(-length / 2, 0)}L{P(0, -width / 2)}Z\"{fill}/>";
            case "oval":
                var angle = Math.Atan2(u.Y, u.X) * 180 / Math.PI;
                return $"<ellipse cx=\"{Units.Css(tip.X)}\" cy=\"{Units.Css(tip.Y)}\" rx=\"{Units.Css(length / 2)}\" ry=\"{Units.Css(width / 2)}\" " +
                       $"transform=\"rotate({Units.Css(angle)} {Units.Css(tip.X)} {Units.Css(tip.Y)})\"{fill}/>";
            case "arrow":
                pullback = lineWidth / 2;
                return $"<path d=\"M{P(-length, width / 2)}L{P(0, 0)}L{P(-length, -width / 2)}\" fill=\"none\"{stroke}/>";
            default:
                return "";
        }
    }
}
