using System.Xml.Linq;
using FreeSlate.Drawing;

namespace FreeSlate.Docx.Layout;

/// <summary>The page border of a page: its lines as filled rectangles and strokes, in front of the text or behind it.</summary>
public sealed record PageBorderBox(List<FillRect> Fills, List<Stroke> Strokes, bool Front);

/// <summary>
/// Page borders (w:pgBorders, catalog: section.pageBorders) as Word prints them (measured with
/// docx/generated/page-borders, docx/local/pgb-a, pgb-b and pgb-c):
/// <list type="bullet">
/// <item>From the page's edge a side's outer edge stands its space from the edge; from the text its inner edge stands
/// its space outside the margins, around a header and a footer the page has unless w:bordersDoNotSurroundHeader and
/// w:bordersDoNotSurroundFooter say otherwise (an empty header counts). A side left out has no space and no width:
/// the other sides reach to the page's edge or the margin.</item>
/// <item>Word's 600 dpi device floors every line and gap to 0.12 pt (sz 4 prints 0.48 pt, 2.25 pt lines 2.16 pt).
/// Double and triple lines have gaps as wide as their lines; thin-thick lines a thin line and a gap of 0.75 pt
/// (small gap), half the width (medium) or 1.5 pt and 0.75 pt lines with a gap of the width (large), in the same
/// order on every side from the page's top and left. Gaps are painted white. Solid lines meet as nested frames.</item>
/// <item>Dotted and dashed lines run between the corners, which are filled squares, in dashes of 1 and 3 widths
/// with gaps of 1 width; dashes with small gaps are 4 widths long and leave the corners empty.</item>
/// <item>Embossed and engraved lines have a 0.75 pt line lighter by a sixth of the lightness, the line at the width
/// darker by an eighth and a 0.75 pt line darker by a sixth; inset and outset lines are plain.</item>
/// <item>A shadow is a black band as wide as the side's lines below the bottom and right of the frame, shifted by
/// the lines' width; from the page's edge the frame moves in by its lines' width so that the shadow keeps the
/// space.</item>
/// <item>Waves and stroked dash-dots are pictures in Word; they are drawn as lines of their fixed widths here.</item>
/// </list>
/// </summary>
public static class PageBorders
{
    /// <summary>A side of the border: its line and the stripes across it with their kinds.</summary>
    sealed record Side(BorderLine Line, List<(double Width, Kind Kind)> Stripes)
    {
        public double Total => Stripes.Sum(s => s.Width);
    }

    enum Kind { Line, Gap, Light, Mid, Dark }

    /// <summary>The page's border, or null where its section has none on this page.</summary>
    public static PageBorderBox? Layout(PageBox page, XElement? settings)
    {
        var borders = page.Section.Element.Child("pgBorders");
        if (borders == null)
            return null;
        var display = borders.Attr("display") ?? "allPages";
        if (display == "firstPage" && !page.FirstOfSection || display == "notFirstPage" && page.FirstOfSection)
            return null;
        var (top, left, bottom, right) = (Read(borders.Child("top")), Read(borders.Child("left")), Read(borders.Child("bottom")), Read(borders.Child("right")));
        if (top == null && left == null && bottom == null && right == null)
            return null;

        static double Total(Side? s) => s?.Total ?? 0;
        static double Space(Side? s) => s?.Line.Space ?? 0;
        static double Shadow(Side? s) => s is { Line.Shadow: true } ? s.Total : 0;
        // The frame's outer edges.
        double l, t, r, b;
        if (borders.Attr("offsetFrom") == "page")
        {
            l = Space(left) + Shadow(left);
            t = Space(top) + Shadow(top);
            r = page.Width - Space(right) - Shadow(right);
            b = page.Height - Space(bottom) - Shadow(bottom);
        }
        else
        {
            var section = page.Section;
            var header = page.Header != null && settings.Child("bordersDoNotSurroundHeader").OnOff() != true;
            var footer = page.Footer != null && settings.Child("bordersDoNotSurroundFooter").OnOff() != true;
            var textTop = header ? Math.Min(section.Top, section.Header) : section.Top;
            var textBottom = page.Height - (footer ? Math.Min(section.Bottom, section.Footer) : section.Bottom);
            l = page.TextLeft - Space(left) - Total(left);
            r = page.TextLeft + page.TextWidth + Space(right) + Total(right);
            t = textTop - Space(top) - Total(top);
            b = textBottom + Space(bottom) + Total(bottom);
        }

        var fills = new List<FillRect>();
        var strokes = new List<Stroke>();
        // Shadows first, under the lines.
        if (right is { Line.Shadow: true })
            fills.Add(new FillRect(r, t + Total(top), right.Total, b + Total(bottom) - t - Total(top), RgbColor.Black));
        if (bottom is { Line.Shadow: true })
            fills.Add(new FillRect(l + Total(left), b, r + Total(right) - l - Total(left), bottom.Total, RgbColor.Black));
        if (top != null)
            Draw(top, true, false, t, l, r, left, right, fills, strokes);
        if (bottom != null)
            Draw(bottom, true, true, b, l, r, left, right, fills, strokes);
        if (left != null)
            Draw(left, false, false, l, t, b, top, bottom, fills, strokes);
        if (right != null)
            Draw(right, false, true, r, t, b, top, bottom, fills, strokes);
        return new PageBorderBox(fills, strokes, borders.Attr("zOrder") != "back");
    }

    /// <summary>The room a run border takes beside its text: its space and its lines (catalog: run.decoration).</summary>
    public static double Room(BorderLine line) => SideOf(line) is { } side ? Device(line.Space) + side.Total : 0;

    /// <summary>
    /// A frame of one line whose outer edges are a box's (run borders, catalog: run.decoration): its sides drawn as a
    /// page border's are, the gaps of double lines left open (measured with docx/local/ulsizes).
    /// </summary>
    public static PageBorderBox? Frame(BorderLine line, double left, double top, double right, double bottom)
    {
        if (SideOf(line) is not { } side)
            return null;
        var fills = new List<FillRect>();
        var strokes = new List<Stroke>();
        Draw(side, true, false, top, left, right, side, side, fills, strokes, false);
        Draw(side, true, true, bottom, left, right, side, side, fills, strokes, false);
        Draw(side, false, false, left, top, bottom, side, side, fills, strokes, false);
        Draw(side, false, true, right, top, bottom, side, side, fills, strokes, false);
        return new PageBorderBox(fills, strokes, true);
    }

    /// <summary>A side's stripes from its outer edge inwards: the bottom's and the right's are the last first.</summary>
    static IEnumerable<(double Width, Kind Kind)> Outward(Side? side, bool far) =>
        side == null ? [] : far ? Enumerable.Reverse(side.Stripes) : side.Stripes;

    /// <summary>
    /// A side whose outer edge is at <paramref name="edge"/> (the bottom's and the right's far from the page's top and
    /// left), between <paramref name="from"/> and <paramref name="to"/>, the outer edges of the sides it meets: a
    /// stripe reaches into a corner as far as the sides it meets have stripes further out than its own.
    /// </summary>
    static void Draw(Side side, bool horizontal, bool far, double edge, double from, double to, Side? start, Side? end, List<FillRect> fills, List<Stroke> strokes, bool gaps = true)
    {
        var line = side.Line;
        var w = side.Total;
        void Rect(double along, double length, double across, double thickness, RgbColor color) =>
            fills.Add(horizontal ? new FillRect(along, across, length, thickness, color) : new FillRect(across, along, thickness, length, color));
        void Line(double a, double b, double across, double thickness, RgbColor color, string? dash = null) =>
            strokes.Add(horizontal ? new Stroke(a, across, b, across, thickness, color, dash) : new Stroke(across, a, across, b, thickness, color, dash));

        var top = far ? edge - w : edge;
        var (inner0, inner1) = (from + (start?.Total ?? 0), to - (end?.Total ?? 0));
        if (line.Style == "dashDotStroked")
        {
            // A rope of short strokes in Word: a dash-dot line half as wide as its band.
            Line(inner0, inner1, top + w / 2, w / 2, line.Color, Pattern(1.5 * w, w / 2, w / 2, w / 2));
            return;
        }
        if (Dash(line.Style, w) is { } dash)
        {
            // Dotted and dashed lines between the corners, filled corners (empty for dashes with small gaps).
            Line(inner0, inner1, top + w / 2, w, line.Color, dash);
            if (line.Style != "dashSmallGap")
            {
                if (start != null)
                    Rect(from, inner0 - from, top, w, line.Color);
                if (end != null)
                    Rect(inner1, to - inner1, top, w, line.Color);
            }
            return;
        }
        if (line.Style is "wave" or "doubleWave")
        {
            // Waves of 6 pt periods across the band, 0.5 pt thin.
            (double Center, double Height)[] waves = line.Style == "wave" ? [(top + w / 2, w - 0.5)] : [(top + w * 0.3, w * 0.4), (top + w * 0.7, w * 0.4)];
            foreach (var (center, height) in waves)
            {
                double Across(double along) => center + height / 2 * Math.Sin((along - inner0) / 6 * 2 * Math.PI);
                var steps = Math.Max(1, (int)Math.Ceiling((inner1 - inner0) / 0.75));
                var step = (inner1 - inner0) / steps;
                for (var i = 0; i < steps; i++)
                {
                    var (a, b) = (inner0 + i * step, inner0 + (i + 1) * step);
                    strokes.Add(horizontal ? new Stroke(a, Across(a), b, Across(b), 0.5, line.Color) : new Stroke(Across(a), a, Across(b), b, 0.5, line.Color));
                }
            }
            return;
        }
        // Solid stripes from the outer edge in, each reaching into the corners past the stripes further out of the
        // sides it meets.
        var (outerStart, outerEnd) = (Outward(start, false).ToList(), Outward(end, true).ToList());
        var depth = 0.0;
        var k = 0;
        foreach (var (thickness, kind) in Outward(side, far))
        {
            if (kind == Kind.Gap && !gaps)
            {
                depth += thickness;
                k++;
                continue;
            }
            var color = kind switch
            {
                Kind.Gap => RgbColor.White,
                Kind.Light => Lighter(line.Color, 1.0 / 6),
                Kind.Mid => Lighter(line.Color, -1.0 / 8),
                Kind.Dark => Lighter(line.Color, -1.0 / 6),
                _ => line.Color,
            };
            var a = from + outerStart.Take(k).Sum(s => s.Width);
            var b = to - outerEnd.Take(k).Sum(s => s.Width);
            Rect(a, b - a, far ? edge - depth - thickness : edge + depth, thickness, color);
            depth += thickness;
            k++;
        }
    }

    /// <summary>A color lighter or darker by a share of the lightness.</summary>
    static RgbColor Lighter(RgbColor color, double by)
    {
        var (h, s, l) = color.ToHsl();
        return RgbColor.FromHsl(h, s, Math.Clamp(l + by, 0, 1));
    }

    /// <summary>The dash pattern of a stroked line style in points, or null for solid styles.</summary>
    static string? Dash(string style, double w) => style switch
    {
        "dotted" => Pattern(w, w),
        "dashed" => Pattern(3 * w, w),
        "dotDash" => Pattern(3 * w, w, w, w),
        "dotDotDash" => Pattern(3 * w, w, w, w, w, w),
        "dashSmallGap" => Pattern(4 * w, w),
        _ => null,
    };

    static string Pattern(params double[] lengths) => string.Join(' ', lengths.Select(Units.Css));

    /// <summary>A side's line and stripes, or null where it has none.</summary>
    static Side? Read(XElement? e) => BorderLine.Read(e) is { Visible: true } line ? SideOf(line) : null;

    /// <summary>A line's stripes, or null where it shows none.</summary>
    static Side? SideOf(BorderLine line)
    {
        if (!line.Visible)
            return null;
        var w = Device(line.Width);
        var (thin, half, wide) = (Device(0.75), Device(line.Width / 2), Device(1.5));
        List<(double, Kind)> stripes = line.Style switch
        {
            "double" => [(w, Kind.Line), (w, Kind.Gap), (w, Kind.Line)],
            "triple" => [(w, Kind.Line), (w, Kind.Gap), (w, Kind.Line), (w, Kind.Gap), (w, Kind.Line)],
            "thinThickSmallGap" => [(w, Kind.Line), (thin, Kind.Gap), (thin, Kind.Line)],
            "thickThinSmallGap" => [(thin, Kind.Line), (thin, Kind.Gap), (w, Kind.Line)],
            "thinThickMediumGap" => [(w, Kind.Line), (half, Kind.Gap), (half, Kind.Line)],
            "thickThinMediumGap" => [(half, Kind.Line), (half, Kind.Gap), (w, Kind.Line)],
            "thinThickLargeGap" => [(wide, Kind.Line), (w, Kind.Gap), (thin, Kind.Line)],
            "thickThinLargeGap" => [(thin, Kind.Line), (w, Kind.Gap), (wide, Kind.Line)],
            "thinThickThinSmallGap" => [(thin, Kind.Line), (thin, Kind.Gap), (w, Kind.Line), (thin, Kind.Gap), (thin, Kind.Line)],
            "thinThickThinMediumGap" => [(half, Kind.Line), (half, Kind.Gap), (w, Kind.Line), (half, Kind.Gap), (half, Kind.Line)],
            "thinThickThinLargeGap" => [(thin, Kind.Line), (w, Kind.Gap), (wide, Kind.Line), (w, Kind.Gap), (thin, Kind.Line)],
            "threeDEmboss" => [(thin, Kind.Light), (w, Kind.Mid), (thin, Kind.Dark)],
            "threeDEngrave" => [(thin, Kind.Dark), (w, Kind.Mid), (thin, Kind.Light)],
            // Pictures in Word of fixed widths.
            "wave" or "dashDotStroked" => [(3, Kind.Line)],
            "doubleWave" => [(5.16, Kind.Line)],
            _ when Art(line.Style) => [],
            _ => [(w, Kind.Line)],
        };
        return stripes.Count > 0 ? new Side(line, stripes) : null;
    }

    /// <summary>A width on Word's 600 dpi device: floored to 0.12 pt, at least one dot.</summary>
    static double Device(double width) => Math.Max(0.12, Math.Floor(width / 0.12 + 1e-6) * 0.12);

    /// <summary>Whether a border style is one of the art borders (pictures), not a line.</summary>
    static bool Art(string style) => style is not ("single" or "thick" or "double" or "dotted" or "dashed" or "dotDash"
        or "dotDotDash" or "triple" or "thinThickSmallGap" or "thickThinSmallGap" or "thinThickThinSmallGap"
        or "thinThickMediumGap" or "thickThinMediumGap" or "thinThickThinMediumGap" or "thinThickLargeGap"
        or "thickThinLargeGap" or "thinThickThinLargeGap" or "wave" or "doubleWave" or "dashSmallGap" or "dashDotStroked"
        or "threeDEmboss" or "threeDEngrave" or "outset" or "inset");
}
