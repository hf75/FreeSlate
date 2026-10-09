using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Drawing;
using FreeSlate.Html;
using FreeSlate.Text;

namespace FreeSlate.Pptx.Charts;

/// <summary>
/// Region maps of Office 2016 charts (layoutId regionMap, catalog: chart.ex) drawn from the geography cached in the
/// chart (see <see cref="GeoCache"/>). The cache keeps the regions' shapes but not which region a data point names, so
/// the regions are drawn the way PowerPoint draws them from the file alone (measured: its pictures of such charts show
/// every region in gray): gray regions with white borders, a legend of the series' color scale and the map's credits.
/// </summary>
public sealed partial class ChartRenderer
{
    // Robinson's projection by its 5° table of parallel lengths and distances from the equator, 1.5 % wider than
    // defined (measured: PowerPoint's world maps are, at every chart size).
    static readonly double[] RobinsonLength = [1.0000, 0.9986, 0.9954, 0.9900, 0.9822, 0.9730, 0.9600, 0.9427, 0.9216, 0.8962, 0.8679, 0.8350, 0.7986, 0.7597, 0.7186, 0.6732, 0.6213, 0.5722, 0.5322];
    static readonly double[] RobinsonDepth = [0.0000, 0.0620, 0.1240, 0.1860, 0.2480, 0.3100, 0.3720, 0.4340, 0.4958, 0.5571, 0.6176, 0.6769, 0.7346, 0.7903, 0.8435, 0.8936, 0.9394, 0.9761, 1.0000];
    const double RobinsonWidth = 0.8487 * 1.015;

    static (double X, double Y) Robinson(double latitude, double longitude)
    {
        var a = Math.Min(Math.Abs(latitude), 90) / 5;
        var i = Math.Min((int)a, 17);
        var t = a - i;
        var length = RobinsonLength[i] + (RobinsonLength[i + 1] - RobinsonLength[i]) * t;
        var depth = RobinsonDepth[i] + (RobinsonDepth[i + 1] - RobinsonDepth[i]) * t;
        return (RobinsonWidth * length * longitude * Math.PI / 180, 1.3523 * depth * Math.Sign(latitude));
    }

    static (double X, double Y) Mercator(double latitude, double longitude)
    {
        var phi = Math.Clamp(latitude, -85, 85) * Math.PI / 180;
        return (longitude * Math.PI / 180, Math.Log(Math.Tan(Math.PI / 4 + phi / 2)));
    }

    // Measured on world maps from 300 to 900 pt wide: the legend 5.7 pt inside the chart's right margin, the map fitted
    // into the room left of it less 10 pt, 3.2 % of that room's width kept free on the left; the color bar 6.75 by
    // 63.75 pt, a font size below the legend's name, its labels 4.5 pt beside it; credits in 6 pt.
    const double MapLegendInset = 5.7, MapLegendGap = 10, MapLeftShare = 0.032, ScaleBarWidth = 6.75, ScaleBarHeight = 63.75,
        ScaleLabelGap = 4.5, CreditSize = 6;

    void FrameRegionMap(ChartExModel chart, ExData data)
    {
        var series = chart.Series[0];
        var geography = series.LayoutProperties?.Element(Cx + "geography");
        var cache = GeoCache.Read(geography);
        var values = data.Values.Where(v => v != null).Select(v => v!.Value).ToList();
        // German Office writes its name of the General format.
        var format = data.Formats.Values.FirstOrDefault() is { } f && f != "Standard" ? f : "General";
        exPlot = outer => RegionMap(series, geography, cache, values, format, outer);
    }

    string RegionMap(ExSeries series, XElement? geography, GeoCache? cache, List<double> values, string format, Box outer)
    {
        var svg = new StringBuilder();
        var style = Style(ex?.Legend?.Element(Cx + "txPr") is { } lt ? new XElement(Ns.C + "txPr", lt.Elements()) : null, "legend");
        var lineHeight = LineHeight(style);

        // The legend: the series' name over its color scale, the largest value at the top.
        var right = width - ExMargin;
        if (ex?.Legend != null && values.Count > 0)
        {
            var (min, max) = (values.Min(), values.Max());
            var (low, high) = (NumberFormat.Format(min, format, culture), NumberFormat.Format(max, format, culture));
            var name = series.Name ?? "";
            var legendWidth = Math.Max(Width(name, style), ScaleBarWidth + ScaleLabelGap + Math.Max(Width(low, style), Width(high, style)));
            var x = width - ExMargin - MapLegendInset - legendWidth;
            var lineTop = outer.Y - ExTitleGap + ExLegendGap;
            svg.Append(text.Label(name, style, x, lineTop + Ascent(style)));
            var barTop = lineTop + lineHeight + style.Size;
            var barX = x + 3;
            var id = paint.NewId("scale");
            var (top, bottom) = ScaleColors();
            // The scale runs between its colors in linear light (measured: its middle is lighter than an sRGB mix).
            defs.Append($"<linearGradient id=\"{id}\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\">");
            for (var i = 0; i <= 8; i++)
            {
                var t = i / 8.0;
                var color = RgbColor.Mix(top.ToLinear(), bottom.ToLinear(), t).FromLinear();
                defs.Append($"<stop offset=\"{Units.Css4(t)}\" stop-color=\"{color.ToHex()}\"/>");
            }
            defs.Append("</linearGradient>");
            svg.Append($"<rect x=\"{Px(barX)}\" y=\"{Px(barTop)}\" width=\"{Px(ScaleBarWidth)}\" height=\"{Px(ScaleBarHeight)}\" fill=\"url(#{id})\"/>");
            var labelX = barX + ScaleBarWidth + ScaleLabelGap;
            svg.Append(text.Label(high, style, labelX, ExCenteredBaseline(style, barTop)));
            svg.Append(text.Label(low, style, labelX, ExCenteredBaseline(style, barTop + ScaleBarHeight)));
            right = x - MapLegendGap;
        }

        var box = new Box(ExMargin + MapLeftShare * (right - ExMargin), outer.Y, Math.Max(1, right - ExMargin - MapLeftShare * (right - ExMargin)), outer.H);
        if (cache is { Regions.Count: > 0 })
            svg.Append(Regions(cache, geography, box));

        // Credits at the bottom right: the map's provider, the sources below it where they fit.
        var credit = style with { Size = CreditSize };
        var baseline = height - ExMargin - Descent(credit);
        var sources = cache is { Copyrights.Count: > 0 } ? "© " + string.Join(", ", cache.Copyrights) : "";
        if (sources.Length > 0 && Width(sources, credit) <= right - ExMargin)
        {
            svg.Append(text.Label(sources, credit, right - Width(sources, credit), baseline));
            baseline -= LineHeight(credit);
        }
        if ((string?)geography?.Attribute("attribution") is { Length: > 0 } attribution)
            svg.Append(text.Label(attribution, credit, right - Width(attribution, credit), baseline));
        return svg.ToString();
    }

    /// <summary>A length in tenths of a pixel as SVG number ("-12.3", ".5").</summary>
    static string Tenths(long tenths)
    {
        var text = (Math.Abs(tenths) / 10).ToString(CultureInfo.InvariantCulture);
        var digit = Math.Abs(tenths) % 10;
        var number = digit == 0 ? text : text == "0" ? "." + digit : text + "." + digit;
        return tenths < 0 ? "-" + number : number;
    }

    /// <summary>
    /// The colors of a region map's scale, measured with the default theme: the lightest accent 1 at 20 % luminance
    /// plus 80 % at the bottom, accent 1 with 86.8 % of its lightness and 77.2 % of its saturation at the top.
    /// </summary>
    (RgbColor Top, RgbColor Bottom) ScaleColors()
    {
        var accent = colors.Resolve(new XElement(Ns.A + "schemeClr", new XAttribute("val", "accent1")));
        var (h, s, l) = accent.ToHsl();
        var bottom = colors.Resolve(new XElement(Ns.A + "schemeClr", new XAttribute("val", "accent1"),
            new XElement(Ns.A + "lumMod", new XAttribute("val", "20000")), new XElement(Ns.A + "lumOff", new XAttribute("val", "80000"))));
        return (RgbColor.FromHsl(h, s * 0.772, l * 0.868), bottom);
    }

    /// <summary>
    /// The cached regions fitted into the box and centered: the world in Robinson's projection, smaller areas (spanning
    /// less than half the globe) in Mercator's; outlines simplified to points at least 0.35 pt apart and shapes under
    /// 0.3 pt left out.
    /// </summary>
    string Regions(GeoCache cache, XElement? geography, Box box)
    {
        var all = cache.Regions.SelectMany(r => r.Rings).SelectMany(p => p).ToList();
        if (all.Count == 0)
            return "";
        var projection = (string?)geography?.Attribute("projectionType") switch
        {
            "mercator" => (Func<double, double, (double X, double Y)>)Mercator,
            "robinson" => Robinson,
            _ => all.Max(p => p.Lon) - all.Min(p => p.Lon) > 180 ? Robinson : Mercator,
        };
        var projected = cache.Regions.Select(r => r.Rings.Select(ring => ring.Select(p => projection(p.Lat, p.Lon)).ToList()).ToList()).ToList();
        double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
        foreach (var point in projected.SelectMany(r => r).SelectMany(p => p))
        {
            (x0, x1) = (Math.Min(x0, point.X), Math.Max(x1, point.X));
            (y0, y1) = (Math.Min(y0, point.Y), Math.Max(y1, point.Y));
        }
        var scale = Math.Min(box.W / Math.Max(1e-9, x1 - x0), box.H / Math.Max(1e-9, y1 - y0));
        var ox = box.X + box.W / 2 - (x0 + x1) / 2 * scale;
        var oy = box.Y + box.H / 2 + (y0 + y1) / 2 * scale;
        var fill = new SolidFill(colors.Resolve(new XElement(Ns.A + "schemeClr", new XAttribute("val", "bg1"), new XElement(Ns.A + "lumMod", new XAttribute("val", "88000")))));
        var border = chartStyle?.Element(Ns.Cs + "dataPoint")?.Element(Ns.Cs + "spPr")?.Element(Ns.A + "ln") is { } styleLine
            ? OutlineReader.Merge([(ResolvePlaceholder(styleLine), colors)])
            : new Outline(3175, new SolidFill(RgbColor.White), "solid", "flat", "round", 8, "sng");
        var fillAttributes = paint.Fill(fill, 0, 0, defs);
        var strokeAttributes = paint.Stroke(border, 0, 0, defs);
        var svg = new StringBuilder($"<g{fillAttributes}{strokeAttributes} fill-rule=\"evenodd\">");
        foreach (var region in projected)
        {
            var path = new StringBuilder();
            foreach (var ring in region)
            {
                var points = ring.Select(p => (X: ox + p.X * scale, Y: oy - p.Y * scale)).ToList();
                if (points.Max(p => p.X) - points.Min(p => p.X) < 0.3 && points.Max(p => p.Y) - points.Min(p => p.Y) < 0.3)
                    continue;
                var kept = new List<(double X, double Y)> { points[0] };
                for (var i = 1; i < points.Count; i++)
                {
                    var last = kept[^1];
                    if (i == points.Count - 1 || Math.Abs(points[i].X - last.X) + Math.Abs(points[i].Y - last.Y) >= 0.35)
                        kept.Add(points[i]);
                }
                if (kept.Count < 3)
                    continue;
                // Steps between positions rounded to tenths of a pixel keep the outlines short.
                var tenths = kept.Select(p => ((long)Math.Round(p.X * PxPerPt * 10), (long)Math.Round(p.Y * PxPerPt * 10))).ToList();
                path.Append('M').Append(Tenths(tenths[0].Item1)).Append(' ').Append(Tenths(tenths[0].Item2)).Append('l');
                for (var i = 1; i < tenths.Count; i++)
                {
                    var (dx, dy) = (tenths[i].Item1 - tenths[i - 1].Item1, tenths[i].Item2 - tenths[i - 1].Item2);
                    if (dx == 0 && dy == 0)
                        continue;
                    path.Append(Tenths(dx));
                    if (dy >= 0)
                        path.Append(' ');
                    path.Append(Tenths(dy));
                    path.Append(' ');
                }
                path.Append('z');
            }
            if (path.Length > 0)
                svg.Append($"<path d=\"{path}\"/>");
        }
        return svg.Append("</g>").ToString();
    }
}
