using System.Text;
using FreeSlate.Drawing;
using FreeSlate.Drawing.Geometry;

namespace FreeSlate.Html;

/// <summary>
/// Where PowerPoint lays dash patterns along a path (catalog: line.dash). Measured with generated/lines-dash and probe
/// decks with one long dash and a long space on 24 presets and on freeforms: on a closed figure the pattern starts
/// where its closing segment starts (the figure's last point) and runs through the closing segment first, so a
/// rectangle starts at its bottom left corner and runs up; open figures start at their first point.
/// </summary>
public static class Dashes
{
    /// <summary>The dash pattern of an outline in line widths (dash, space, …), or null for a solid line.</summary>
    public static double[]? Pattern(Outline outline) => outline.Dash == "custom" ? outline.CustomDash?.ToArray() : Preset(outline.Dash);

    public static bool IsDashed(Outline? outline) => outline is { Visible: true } && Pattern(outline) is { Length: > 0 };

    /// <summary>Preset dash patterns in multiples of the line width (ST_PresetLineDashVal).</summary>
    public static double[]? Preset(string preset) => preset switch
    {
        "dot" => [1, 3],
        "dash" => [4, 3],
        "lgDash" => [8, 3],
        "dashDot" => [4, 3, 1, 3],
        "lgDashDot" => [8, 3, 1, 3],
        "lgDashDotDot" => [8, 3, 1, 3, 1, 3],
        "sysDash" => [3, 1],
        "sysDot" => [1, 1],
        "sysDashDot" => [3, 1, 1, 1],
        "sysDashDotDot" => [3, 1, 1, 1, 1, 1],
        _ => null,
    };

    /// <summary>The pattern's unit: the line's width in whole pixels (measured: a 4 pt line repeats a 3:1 pattern every 20 px).</summary>
    public static double Unit(double widthPx) => Math.Max(1, Math.Round(widthPx));

    /// <summary>Whether every figure of the path is closed.</summary>
    public static bool Closed(EvaluatedPath path) => Figures(path.Segments).All(f => f.Closed);

    /// <summary>The path with every closed figure starting at its last point, so that SVG starts the pattern there.</summary>
    public static EvaluatedPath StartAtClosingSegment(EvaluatedPath path)
    {
        var segments = new List<PathSegment>();
        foreach (var (figure, closed) in Figures(path.Segments))
        {
            if (!closed || figure[0].Command != 'M' || figure.Count < 3 || figure[^2].End is not { } last)
            {
                segments.AddRange(figure);
                continue;
            }
            var first = (figure[0].Values[0], figure[0].Values[1]);
            segments.Add(new PathSegment('M', [last.X, last.Y]));
            if (Math.Abs(last.X - first.Item1) > 1e-6 || Math.Abs(last.Y - first.Item2) > 1e-6)
                segments.Add(new PathSegment('L', [first.Item1, first.Item2]));
            segments.AddRange(figure.Skip(1));
        }
        return path with { Segments = segments };
    }

    /// <summary>
    /// The path as Word lays a dash pattern along it (measured by agent BK on Word's PDFs: a rectangle that is not
    /// turned, which Word writes as a PDF rectangle, from its bottom left corner along its bottom; LibreOffice's
    /// extras-ooxmlexport/fdo73247, a rectangle turned 33 degrees, from its first point, the top left corner, to the
    /// right; LineStyle_DashType 98.0 -> 99.5 %, textbox_picturefill 91.4 -> 96.5 %): with <paramref name="upright"/>
    /// a closed figure of four corners whose sides run across and down from its bottom left corner along its bottom,
    /// every other figure from its first point, as its geometry gives it.
    /// </summary>
    public static EvaluatedPath WordStart(EvaluatedPath path, bool upright)
    {
        if (!upright)
            return path;
        var segments = new List<PathSegment>();
        foreach (var (figure, closed) in Figures(path.Segments))
        {
            if (closed && Rectangle(figure) is var (left, top, right, bottom))
            {
                segments.Add(new PathSegment('M', [left, bottom]));
                segments.Add(new PathSegment('L', [right, bottom]));
                segments.Add(new PathSegment('L', [right, top]));
                segments.Add(new PathSegment('L', [left, top]));
                segments.Add(new PathSegment('Z', []));
            }
            else
                segments.AddRange(figure);
        }
        return path with { Segments = segments };
    }

    /// <summary>The bounds of a figure of straight sides running across and down through four corners, or null.</summary>
    static (double Left, double Top, double Right, double Bottom)? Rectangle(List<PathSegment> figure)
    {
        var points = new List<(double X, double Y)>();
        foreach (var segment in figure)
        {
            if (segment.Command == 'Z')
                continue;
            if (segment.Command is not ('M' or 'L') || segment.End is not { } end)
                return null;
            if (points.Count == 0 || Math.Abs(points[^1].X - end.X) > 1e-6 || Math.Abs(points[^1].Y - end.Y) > 1e-6)
                points.Add(end);
        }
        if (points.Count == 5 && Math.Abs(points[0].X - points[4].X) <= 1e-6 && Math.Abs(points[0].Y - points[4].Y) <= 1e-6)
            points.RemoveAt(4);
        if (points.Count != 4)
            return null;
        for (var i = 0; i < 4; i++)
        {
            var (a, b) = (points[i], points[(i + 1) % 4]);
            if (Math.Abs(a.X - b.X) > 1e-6 && Math.Abs(a.Y - b.Y) > 1e-6)
                return null;
        }
        var (left, right) = (points.Min(p => p.X), points.Max(p => p.X));
        var (top, bottom) = (points.Min(p => p.Y), points.Max(p => p.Y));
        return right - left > 1e-6 && bottom - top > 1e-6 ? (left, top, right, bottom) : null;
    }

    /// <summary>
    /// The square caps of a dashed line: PowerPoint ends its dashes flat and gives only the ends of an open figure a
    /// cap, its start always (the pattern starts with a dash) and its end where the end lies within a dash (measured
    /// with generated/lines-dash: the dashes keep their full length, the first one reaches half a width before the
    /// line). <paramref name="paint"/> holds the stroke's paint attributes for a solid line with flat caps, <paramref
    /// name="unit"/> the pattern's unit in pixels.
    /// </summary>
    public static string SquareEnds(EvaluatedPath path, Outline outline, double widthPx, string paint, double unit)
    {
        var pattern = Pattern(outline);
        if (pattern is not { Length: > 0 })
            return "";
        var svg = new StringBuilder();
        foreach (var (points, closed) in CompoundLines.Flatten(path.Segments))
        {
            if (closed || points.Count < 2)
                continue;
            var length = 0.0;
            for (var i = 1; i < points.Count; i++)
                length += Distance(points[i - 1], points[i]);
            if (outline.Head is null or { Type: "none" } && Direction(points, 0, 1) is { } back)
                svg.Append(Cap(points[0], back, widthPx, paint));
            if (outline.Tail is null or { Type: "none" } && InDash(pattern, unit, length) && Direction(points, points.Count - 1, -1) is { } ahead)
                svg.Append(Cap(points[^1], ahead, widthPx, paint));
        }
        return svg.ToString();
    }

    /// <summary>Whether a position along the line (pixels) lies within a dash of the pattern.</summary>
    static bool InDash(double[] pattern, double unit, double position)
    {
        var period = pattern.Sum() * unit;
        if (period <= 0)
            return true;
        var at = position % period;
        var start = 0.0;
        for (var i = 0; i < pattern.Length; i++)
        {
            var end = start + pattern[i] * unit;
            if (at < end)
                return i % 2 == 0;
            start = end;
        }
        return false;
    }

    /// <summary>The unit vector from a figure's end point away from the figure (the way its cap reaches out).</summary>
    static (double X, double Y)? Direction(List<(double X, double Y)> points, int from, int step)
    {
        for (var i = from + step; i >= 0 && i < points.Count; i += step)
        {
            var (dx, dy) = (points[from].X - points[i].X, points[from].Y - points[i].Y);
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d > 1e-6)
                return (dx / d, dy / d);
        }
        return null;
    }

    static string Cap((double X, double Y) at, (double X, double Y) direction, double widthPx, string paint)
    {
        var (x, y) = (at.X + direction.X * widthPx / 2, at.Y + direction.Y * widthPx / 2);
        return $"<path d=\"M{Units.Css(at.X)} {Units.Css(at.Y)}L{Units.Css(x)} {Units.Css(y)}\" fill=\"none\"{paint}/>";
    }

    static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>The figures of a path: from a moveto to the next one, closed when they end with Z.</summary>
    static IEnumerable<(List<PathSegment> Segments, bool Closed)> Figures(List<PathSegment> segments)
    {
        var figure = new List<PathSegment>();
        foreach (var segment in segments)
        {
            if (segment.Command == 'M' && figure.Count > 0)
            {
                yield return (figure, figure[^1].Command == 'Z');
                figure = [];
            }
            figure.Add(segment);
            if (segment.Command == 'Z')
            {
                yield return (figure, true);
                figure = [];
            }
        }
        if (figure.Count > 0)
            yield return (figure, figure[^1].Command == 'Z');
    }
}
