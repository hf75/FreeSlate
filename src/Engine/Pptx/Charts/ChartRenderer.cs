using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Engine.Drawing;
using Engine.Html;
using Engine.Text;

namespace Engine.Pptx.Charts;

/// <summary>
/// Renders a DrawingML chart (c:chartSpace) as SVG from the values cached in the chart part (catalog: chart.*).
/// The automatic layout reproduces PowerPoint's, measured with a calibration deck whose plot areas, legends and
/// titles were colored and compared with the positions PowerPoint reports through COM. Lengths are points in the
/// frame's coordinates (s = font size, L = line height of the element's font); the SVG is written in pixels.
/// </summary>
public sealed class ChartRenderer(SlideContext slide, SvgPaint paint, TextRenderer text, string chartPart)
{
    const double PxPerPt = 96.0 / 72;

    // Measured automatic layout constants.
    const double EdgeGap = 6;                 // title and legend from the chart edge
    const double PlotGap = 11;                // plot area from the chart edge, the title and the legend
    const double LabelBleed = 4.5;            // axis labels may reach this far past the plot area's outer edge
    const double ValueLabelGap = 0.922;       // × s between vertical axis labels and the plot area
    const double CategoryLabelGap = 0.624;    // × s between the axis and horizontal labels
    const double LabelCenterShift = 0.054;    // × s: labels sit this much higher than their centered line box
    const double LegendRowPadding = 5.85;     // added to L per legend row
    const double LegendKey = 0.549;           // × s, square legend keys
    const double LegendTextOffset = 0.791;    // × s from a square key to its text
    const double LegendLineKeyText = 1.825;   // × s from a line key to its text
    const double LegendSpacing = 0.757;       // × s + 9.85 % of the text width between horizontal entries
    const double LegendTextExtra = 0.0985;
    const double LegendLeft = 0.78, LegendRight = 0.415;          // × s (+ text extra), horizontal legends
    const double VerticalLegendLeft = 0.454, VerticalLegendRight = 0.348;
    const double TitlePaddingX = 3, TitlePaddingY = 1.5;

    static readonly string[] MarkerCycle = ["diamond", "square", "triangle", "x", "star", "circle", "plus", "dot", "dash"];

    readonly StringBuilder defs = new();
    ChartModel model = null!;
    XElement? chartStyle;
    XElement? colorStyle;
    CultureInfo culture = CultureInfo.InvariantCulture;
    double width, height;
    double originX, originY;

    sealed record Box(double X, double Y, double W, double H)
    {
        public double Right => X + W;
        public double Bottom => Y + H;
    }

    sealed class TextBlock
    {
        public required LaidOutText Layout { get; init; }
        public required TextStyle Style { get; init; }
        public double Width => Layout.Lines.Count == 0 ? 0 : Layout.Lines.Max(l => l.Width);
    }

    sealed class TitleLayout
    {
        public required TextBlock Text { get; init; }
        public required Box Box { get; set; }
        public bool Overlay { get; init; }
        public XElement? SpPr { get; init; }
    }

    sealed record LegendEntry(string Text, Fill Fill, Outline? Outline, bool LineKey, XElement? Marker, int MarkerIndex);

    sealed class LegendLayout
    {
        public required string Position { get; init; }
        public required TextStyle Style { get; init; }
        public required List<LegendEntry> Entries { get; init; }
        public required Box Box { get; set; }
        public bool Overlay { get; init; }
        public XElement? SpPr { get; init; }
        public List<(LegendEntry Entry, double X, double RowCenter)> Placed { get; } = [];
    }

    sealed class AxisLayout
    {
        public required ChartAxis Axis { get; init; }
        public required TextStyle Style { get; init; }
        public required List<ChartGroup> Groups { get; init; }
        public bool Visible => !Axis.Deleted;
        public bool HasLabels => Visible && Axis.TickLabelPosition != "none";
        public AxisScale Scale { get; set; }
        public List<string> Labels { get; set; } = [];
        public double MaxLabelWidth { get; set; }
        public int Categories { get; set; }
        public bool Between { get; set; } = true;
        public double Start { get; set; }
        public double End { get; set; }

        public double Map(double value) => Start + Scale.Fraction(value) * (End - Start);

        /// <summary>Category boundary <paramref name="i"/> (0 … n) along the axis.</summary>
        public double Boundary(double i) => Start + i * (End - Start) / Math.Max(1, Categories);

        /// <summary>Where the data point of category <paramref name="i"/> sits.</summary>
        public double Center(int i) => Between
            ? Boundary(i + 0.5)
            : Start + (Categories > 1 ? i * (End - Start) / (Categories - 1) : (End - Start) / 2);
    }

    /// <summary>SVG content (frame-local pixels) for a chart frame of the given size in points placed at the given slide pixel origin.</summary>
    public string Render(double widthPt, double heightPt, double originXPx, double originYPx)
    {
        if (!slide.Presentation.Package.Exists(chartPart) || ChartModel.Read(slide.Presentation.Load(chartPart)) is not { } read)
            return "";
        model = read;
        width = widthPt;
        height = heightPt;
        originX = originXPx;
        originY = originYPx;
        culture = Culture(ChartModel.Val(model.ChartSpace, "lang"));
        chartStyle = Related("chartStyle");
        colorStyle = Related("chartColorStyle");

        var svg = new StringBuilder();
        svg.Append(Area(model.ChartSpace.Element(Ns.C + "spPr"), new Box(0, 0, width, height), snap: false));

        var title = BuildTitle();
        var legend = BuildLegend(title);

        // Boundaries the plot area keeps its distance from: the chart edges, the title and the legend.
        double top = 0, bottom = height, left = 0, right = width;
        if (title is { Overlay: false }) top = title.Box.Bottom;
        if (legend is { Overlay: false })
        {
            switch (legend.Position)
            {
                case "b": bottom = legend.Box.Y; break;
                case "t": top = legend.Box.Bottom; break;
                case "l": left = legend.Box.Right; break;
                case "r": right = legend.Box.X; break;
                case "tr": right = legend.Box.X; top = legend.Box.Bottom - (PlotGap - 5); break;
            }
        }
        var outer = new Box(left + PlotGap, top + PlotGap, Math.Max(0, right - left - 2 * PlotGap), Math.Max(0, bottom - top - 2 * PlotGap));

        var body = new StringBuilder();
        var pies = model.Groups.Where(g => g.IsPie).ToList();
        var axisGroups = model.Groups.Where(g => !g.IsPie).ToList();
        if (axisGroups.Count > 0)
            body.Append(AxisChart(axisGroups, outer, legend, title));
        foreach (var pie in pies)
            body.Append(Pie(pie, PlotBox(outer, square: true)));

        svg.Append(body);
        if (legend != null)
            svg.Append(DrawLegend(legend));
        if (title != null)
            svg.Append(DrawTitle(title));
        return (defs.Length > 0 ? $"<defs>{defs}</defs>" : "") + svg;
    }

    // ---------------------------------------------------------------- parts, styles, text

    XElement? Related(string kind)
    {
        var package = slide.Presentation.Package;
        var target = package.FirstRelationship(chartPart, kind)?.Target;
        return target != null && package.Exists(target) ? slide.Presentation.Load(target).Root : null;
    }

    static CultureInfo Culture(string? lang)
    {
        try { return lang != null ? CultureInfo.GetCultureInfo(lang) : CultureInfo.InvariantCulture; }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
    }

    static XElement? DefRPr(XElement? txPr) => txPr?.Element(Ns.A + "p")?.Element(Ns.A + "pPr")?.Element(Ns.A + "defRPr");

    /// <summary>The chart style part's entry for an element (cs:title, cs:legend …) as a defRPr layer.</summary>
    XElement? StyleLayer(string element)
    {
        var entry = chartStyle?.Element(Ns.Cs + element);
        if (entry == null)
            return null;
        var layer = new XElement(Ns.A + "defRPr", entry.Element(Ns.Cs + "defRPr")?.Attributes());
        if (entry.Element(Ns.Cs + "fontRef") is { } fontRef)
        {
            if (fontRef.Elements().FirstOrDefault() is { } color)
                layer.Add(new XElement(Ns.A + "solidFill", color));
            if (fontRef.Str("idx") is "minor" or "major")
                layer.Add(new XElement(Ns.A + "latin", new XAttribute("typeface", fontRef.Str("idx") == "major" ? "+mj-lt" : "+mn-lt")));
        }
        return layer;
    }

    /// <summary>Text style of a chart element: its own txPr, the chart's txPr, the style part, then 10 pt defaults.</summary>
    TextStyle Style(XElement? txPr, string styleElement, double defaultSize = 10) =>
        text.ChartStyle([DefRPr(txPr), DefRPr(model.ChartSpace.Element(Ns.C + "txPr")), StyleLayer(styleElement),
            new XElement(Ns.A + "defRPr", new XAttribute("sz", (int)(defaultSize * 100)))]);

    static double Width(string value, TextStyle style) =>
        style.Font.Data.Width(value, style.Kerning) * style.Size + style.LetterSpacing * value.Length;

    static double LineHeight(TextStyle style) => (style.Font.Data.Ascent + style.Font.Data.Descent) * style.Size;

    static double Ascent(TextStyle style) => style.Font.Data.Ascent * style.Size;

    static double Descent(TextStyle style) => style.Font.Data.Descent * style.Size;

    /// <summary>Baseline of a single-line label vertically centered on <paramref name="center"/>.</summary>
    static double CenteredBaseline(TextStyle style, double center) =>
        center + (Ascent(style) - Descent(style)) / 2 - LabelCenterShift * style.Size;

    // ---------------------------------------------------------------- pixel output

    static string Px(double points) => Units.Css(points * PxPerPt);

    /// <summary>Rounds an x coordinate (points) to the device pixel grid of the slide; returns pixels.</summary>
    double SnapX(double points) => Math.Round(originX + points * PxPerPt) - originX;

    double SnapY(double points) => Math.Round(originY + points * PxPerPt) - originY;

    /// <summary>
    /// PowerPoint draws chart lines with their vertices on the pixel grid: lines of odd pixel width centered in a
    /// pixel, even widths on pixel edges (a 3 px line at y = 472.07 covers rows 471 to 473). Returns points.
    /// </summary>
    (double X, double Y) SnapPoint((double X, double Y) p, Outline? outline)
    {
        var odd = (int)Math.Max(1, Math.Round(Units.ToPx(outline?.WidthEmu ?? 9525))) % 2 == 1;
        double Snap(double v, double origin)
        {
            var device = origin + v * PxPerPt;
            return ((odd ? Math.Floor(device) + 0.5 : Math.Round(device)) - origin) / PxPerPt;
        }
        return (Snap(p.X, originX), Snap(p.Y, originY));
    }

    string Rect(Box box, Fill fill, Outline? outline, bool snap)
    {
        double x0, y0, x1, y1;
        if (snap)
        {
            (x0, x1) = (SnapX(box.X), SnapX(box.Right));
            (y0, y1) = (SnapY(box.Y), SnapY(box.Bottom));
        }
        else
        {
            (x0, y0, x1, y1) = (box.X * PxPerPt, box.Y * PxPerPt, box.Right * PxPerPt, box.Bottom * PxPerPt);
        }
        var w = x1 - x0;
        var h = y1 - y0;
        if (w <= 0 || h <= 0)
            return "";
        var fillAttributes = paint.Fill(fill, w, h, defs);
        var stroke = paint.Stroke(outline, w, h, defs);
        return $"<rect x=\"{Units.Css(x0)}\" y=\"{Units.Css(y0)}\" width=\"{Units.Css(w)}\" height=\"{Units.Css(h)}\"{fillAttributes}{stroke}/>";
    }

    /// <summary>Fill and outline of an element's c:spPr as a rectangle (chart area, plot area, legend, title).</summary>
    string Area(XElement? spPr, Box box, bool snap)
    {
        var fill = FillReader.FindFill(spPr) is { } f ? FillReader.Read(f, slide.Colors) : Fill.None;
        var outline = spPr?.Element(Ns.A + "ln") is { } ln ? OutlineReader.Merge([(ln, slide.Colors)]) : null;
        if (fill is NoFill && outline is not { Visible: true })
            return "";
        return Rect(box, fill, outline, snap);
    }

    /// <summary>A horizontal or vertical line of the plot area, snapped like PowerPoint draws thin lines.</summary>
    string GridLine(Outline? outline, double x1, double y1, double x2, double y2)
    {
        if (outline is not { Visible: true } || outline.Fill is not SolidFill solid)
            return outline is { Visible: true } ? $"<path d=\"M{Px(x1)} {Px(y1)}L{Px(x2)} {Px(y2)}\" fill=\"none\"{paint.Stroke(outline, 0, 0, defs)}/>" : "";
        var thickness = Math.Max(1, Math.Round(Units.ToPx(outline.WidthEmu)));
        var color = solid.Color.A < 0.999 ? $" fill=\"{solid.Color.ToHex()}\" fill-opacity=\"{Units.Css4(solid.Color.A)}\"" : $" fill=\"{solid.Color.ToHex()}\"";
        if (Math.Abs(y1 - y2) < 1e-9)
        {
            var y = Math.Round(originY + y1 * PxPerPt - thickness / 2) - originY;
            return $"<rect x=\"{Px(Math.Min(x1, x2))}\" y=\"{Units.Css(y)}\" width=\"{Px(Math.Abs(x2 - x1))}\" height=\"{Units.Css(thickness)}\"{color}/>";
        }
        var x = Math.Round(originX + x1 * PxPerPt - thickness / 2) - originX;
        return $"<rect x=\"{Units.Css(x)}\" y=\"{Px(Math.Min(y1, y2))}\" width=\"{Units.Css(thickness)}\" height=\"{Px(Math.Abs(y2 - y1))}\"{color}/>";
    }

    // ---------------------------------------------------------------- title

    TitleLayout? BuildTitle()
    {
        var element = model.Title;
        var single = model.AllSeries.Count() == 1 ? model.AllSeries.First() : null;
        if (element == null && (model.AutoTitleDeleted || single == null || string.IsNullOrEmpty(single.Name)))
            return null;

        var txPr = element?.Element(Ns.C + "txPr");
        var layers = new List<XElement?> { DefRPr(txPr), DefRPr(model.ChartSpace.Element(Ns.C + "txPr")), StyleLayer("title"),
            new XElement(Ns.A + "defRPr", new XAttribute("sz", "1400"), new XAttribute("b", "1")) };
        var maxWidth = width * 0.8;
        LaidOutText layout;
        TextStyle style;
        if (element?.Element(Ns.C + "tx")?.Element(Ns.C + "rich") is { } rich)
        {
            layout = text.ChartRich(rich, layers, maxWidth, wrap: true);
            style = layout.Lines.SelectMany(l => l.Runs).FirstOrDefault()?.Style ?? text.ChartStyle(layers);
        }
        else
        {
            var value = element?.Element(Ns.C + "tx")?.Element(Ns.C + "strRef")?.Descendants(Ns.C + "v").FirstOrDefault()?.Value
                        ?? (element == null || model.AllSeries.Count() == 1 ? single?.Name : null)
                        ?? (culture.TwoLetterISOLanguageName == "de" ? "Diagrammtitel" : "Chart Title");
            style = text.ChartStyle(layers);
            var rich2 = new XElement(Ns.C + "rich", new XElement(Ns.A + "p", new XElement(Ns.A + "r", new XElement(Ns.A + "t", value))));
            layout = text.ChartRich(rich2, layers, maxWidth, wrap: true);
        }

        var block = new TextBlock { Layout = layout, Style = style };
        var lineHeight = LineHeight(style);
        var w = block.Width + 2 * TitlePaddingX;
        var h = Math.Max(1, layout.Lines.Count) * lineHeight + 2 * TitlePaddingY;
        var box = new Box((width - w) / 2, EdgeGap, w, h);
        if (ManualPosition(element, w, h) is { } manual)
            box = manual;
        return new TitleLayout
        {
            Text = block,
            Box = box,
            Overlay = ChartModel.Flag(element, "overlay", false) || ManualPosition(element, w, h) != null,
            SpPr = element?.Element(Ns.C + "spPr"),
        };
    }

    string DrawTitle(TitleLayout title)
    {
        var svg = new StringBuilder(Area(title.SpPr, title.Box, snap: false));
        var lineHeight = LineHeight(title.Text.Style);
        var baseline = title.Box.Y + TitlePaddingY + Ascent(title.Text.Style);
        foreach (var line in title.Text.Layout.Lines)
        {
            svg.Append(text.EmitLine(line, title.Box.X + (title.Box.W - line.Width) / 2, baseline));
            baseline += lineHeight;
        }
        return svg.ToString();
    }

    /// <summary>A manually positioned element (c:layout/c:manualLayout with edge positions).</summary>
    Box? ManualPosition(XElement? element, double w, double h)
    {
        var manual = element?.Element(Ns.C + "layout")?.Element(Ns.C + "manualLayout");
        if (manual == null || ChartModel.Number(manual, "x") is not { } x || ChartModel.Number(manual, "y") is not { } y)
            return null;
        var mw = ChartModel.Number(manual, "w") is { } fw ? fw * width : w;
        var mh = ChartModel.Number(manual, "h") is { } fh ? fh * height : h;
        return new Box(x * width, y * height, mw, mh);
    }

    // ---------------------------------------------------------------- legend

    LegendLayout? BuildLegend(TitleLayout? title)
    {
        var element = model.Legend;
        if (element == null)
            return null;
        var style = Style(element.Element(Ns.C + "txPr"), "legend");
        var deleted = element.Elements(Ns.C + "legendEntry")
            .Where(e => ChartModel.Flag(e, "delete", false))
            .Select(e => e.Element(Ns.C + "idx").Int("val") ?? -1).ToHashSet();

        var entries = new List<LegendEntry>();
        var index = 0;
        foreach (var group in model.Groups)
        {
            if (group.VaryColors && group.Series.Count == 1 && (group.IsPie || group.IsBar))
            {
                var series = group.Series[0];
                for (var i = 0; i < series.Values.Count; i++, index++)
                {
                    if (!deleted.Contains(index))
                        entries.Add(new LegendEntry(series.Categories.ElementAtOrDefault(i) ?? (i + 1).ToString(culture), PointFill(group, series, i), PointOutline(series, i), false, null, 0));
                }
                continue;
            }
            var list = group.Series.Select((s, k) => (s, k)).ToList();
            if (group.IsBar && group.Horizontal && !group.Stacked)
                list.Reverse();   // clustered bars: the legend lists the series in their visual order
            foreach (var (series, k) in list)
            {
                if (!deleted.Contains(series.Index))
                {
                    var lineKey = group.IsLine || group.IsScatter;
                    entries.Add(new LegendEntry(series.Name, PointFill(group, series, -1), SeriesOutline(group, series), lineKey, lineKey ? Marker(group, series) : null, series.Index));
                }
            }
            index += group.Series.Count;
        }
        if (entries.Count == 0)
            return null;

        var position = ChartModel.Val(element, "legendPos") ?? "r";
        var s = style.Size;
        var rowHeight = LineHeight(style) + LegendRowPadding;
        var layout = new LegendLayout
        {
            Position = position,
            Style = style,
            Entries = entries,
            Box = new Box(0, 0, 0, 0),
            Overlay = ChartModel.Flag(element, "overlay", false),
            SpPr = element.Element(Ns.C + "spPr"),
        };
        double TextOffset(LegendEntry e) => (e.LineKey ? LegendLineKeyText : LegendTextOffset) * s;

        Box box;
        if (position is "t" or "b")
        {
            // One row; entries are spaced by a constant plus a share of their text width.
            var widths = entries.Select(e => Width(e.Text, style)).ToList();
            double Extra(int i) => LegendTextExtra * widths[i];
            var total = LegendLeft * s + Extra(0) + LegendRight * s + Extra(entries.Count - 1);
            for (var i = 0; i < entries.Count; i++)
                total += TextOffset(entries[i]) + widths[i] + (i > 0 ? LegendSpacing * s + Extra(i - 1) : 0);
            var maxWidth = width - 30;
            var squeeze = total > maxWidth ? (total - maxWidth) : 0;
            var spacingCount = entries.Count + 1;
            var w = total - squeeze;
            var x = LegendLeft * s + Extra(0) - squeeze / spacingCount;
            for (var i = 0; i < entries.Count; i++)
            {
                if (i > 0) x += LegendSpacing * s + Extra(i - 1) - squeeze / spacingCount;
                layout.Placed.Add((entries[i], x, rowHeight / 2));
                x += TextOffset(entries[i]) + widths[i];
            }
            var y = position == "b" ? height - EdgeGap - rowHeight : (title is { Overlay: false } ? title.Box.Bottom : 0) + EdgeGap;
            box = new Box((width - w) / 2, y, w, rowHeight);
        }
        else
        {
            var w = VerticalLegendLeft * s + entries.Max(e => TextOffset(e) + Width(e.Text, style)) + VerticalLegendRight * s;
            var h = rowHeight * entries.Count;
            for (var i = 0; i < entries.Count; i++)
                layout.Placed.Add((entries[i], VerticalLegendLeft * s, rowHeight * (i + 0.5)));
            var areaTop = (title is { Overlay: false } ? title.Box.Bottom : 0) + EdgeGap;
            var areaBottom = height - EdgeGap;
            var x = position == "l" ? EdgeGap : width - EdgeGap - w;
            var y = position == "tr" ? areaTop : areaTop + (areaBottom - areaTop - h) / 2;
            box = new Box(x, y, w, h);
        }
        layout.Box = ManualPosition(element, box.W, box.H) ?? box;
        return layout;
    }

    string DrawLegend(LegendLayout legend)
    {
        var svg = new StringBuilder(Area(legend.SpPr, legend.Box, snap: false));
        var s = legend.Style.Size;
        foreach (var (entry, x, rowCenter) in legend.Placed)
        {
            var left = legend.Box.X + x;
            var center = legend.Box.Y + rowCenter;
            if (entry.LineKey)
            {
                var (x1, x2) = (left + 0.063 * s, left + 1.66 * s);
                if (entry.Outline is { Visible: true } line)
                {
                    var (a, b) = (SnapPoint((x1, center), line), SnapPoint((x2, center), line));
                    svg.Append($"<path d=\"M{Px(a.X)} {Px(a.Y)}L{Px(b.X)} {Px(b.Y)}\" fill=\"none\"{paint.Stroke(line, 0, 0, defs)}/>");
                }
                if (entry.Marker != null)
                    svg.Append(DrawMarker(entry.Marker, entry.MarkerIndex, (x1 + x2) / 2, center, entry.Fill, entry.Outline));
                svg.Append(text.Label(entry.Text, legend.Style, left + LegendLineKeyText * s, CenteredBaseline(legend.Style, center)));
            }
            else
            {
                var key = LegendKey * s;
                svg.Append(Rect(new Box(left, center - key / 2, key, key), entry.Fill, entry.Outline is { Visible: true } o ? o with { WidthEmu = Math.Min(o.WidthEmu, 19050) } : null, snap: false));
                svg.Append(text.Label(entry.Text, legend.Style, left + LegendTextOffset * s, CenteredBaseline(legend.Style, center)));
            }
        }
        return svg.ToString();
    }

    // ---------------------------------------------------------------- series colors

    /// <summary>The automatic color of series or point <paramref name="index"/>: the chart's color style, else the accents.</summary>
    RgbColor AutoColor(int index)
    {
        var colors = colorStyle?.Elements().Where(e => e.Name.Namespace == Ns.A).ToList() ?? [];
        var variations = colorStyle?.Elements(Ns.Cs + "variation").ToList() ?? [];
        if (colors.Count == 0)
            colors = Enumerable.Range(1, 6).Select(i => new XElement(Ns.A + "schemeClr", new XAttribute("val", $"accent{i}"))).ToList();
        var color = new XElement(colors[index % colors.Count]);
        var cycle = index / colors.Count;
        if (variations.Count > 0)
            color.Add(variations[cycle % variations.Count].Elements());
        return slide.Colors.Resolve(color);
    }

    Fill PointFill(ChartGroup group, ChartSeries series, int point)
    {
        if (point >= 0 && series.Points.TryGetValue(point, out var dPt) && FillReader.FindFill(dPt.Element(Ns.C + "spPr")) is { } own)
            return FillReader.Read(own, slide.Colors);
        if (point >= 0 && group.VaryColors && group.Series.Count == 1)
            return new SolidFill(AutoColor(point));
        if (FillReader.FindFill(series.SpPr) is { } fill)
            return FillReader.Read(fill, slide.Colors);
        if (group.IsLine || group.IsScatter)
            return new SolidFill(AutoColor(series.Index));
        return new SolidFill(AutoColor(series.Index));
    }

    Outline? PointOutline(ChartSeries series, int point)
    {
        if (point >= 0 && series.Points.TryGetValue(point, out var dPt) && dPt.Element(Ns.C + "spPr")?.Element(Ns.A + "ln") is { } own)
            return OutlineReader.Merge([(own, slide.Colors)]);
        return series.SpPr?.Element(Ns.A + "ln") is { } ln ? OutlineReader.Merge([(ln, slide.Colors)]) : null;
    }

    /// <summary>The line of a line or scatter series: its own a:ln, else an automatic 2.25 pt line.</summary>
    Outline? SeriesOutline(ChartGroup group, ChartSeries series)
    {
        var own = series.SpPr?.Element(Ns.A + "ln");
        if (!group.IsLine && !group.IsScatter)
            return own != null ? OutlineReader.Merge([(own, slide.Colors)]) : null;
        var automatic = new XElement(Ns.A + "ln", new XAttribute("w", "28575"), new XAttribute("cap", "rnd"),
            new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", AutoColor(series.Index).ToHex().TrimStart('#')))),
            new XElement(Ns.A + "round"));
        var layers = new List<(XElement, ColorContext)> { (automatic, slide.Colors) };
        if (own != null) layers.Add((own, slide.Colors));
        return OutlineReader.Merge(layers);
    }

    // ---------------------------------------------------------------- axis charts

    Box PlotBox(Box outer, bool square)
    {
        if (ManualPlot() is { } manual)
            return manual;
        if (!square)
            return outer;
        var size = Math.Min(outer.W, outer.H);
        return new Box(outer.X + (outer.W - size) / 2, outer.Y + (outer.H - size) / 2, size, size);
    }

    /// <summary>A manual plot area layout targeting the inner plot area.</summary>
    Box? ManualPlot()
    {
        var manual = model.PlotArea?.Element(Ns.C + "layout")?.Element(Ns.C + "manualLayout");
        if (manual == null || ChartModel.Number(manual, "x") is not { } x || ChartModel.Number(manual, "y") is not { } y
            || ChartModel.Number(manual, "w") is not { } w || ChartModel.Number(manual, "h") is not { } h)
            return null;
        return new Box(x * width, y * height, w * width, h * height);
    }

    string AxisChart(List<ChartGroup> groups, Box outer, LegendLayout? legend, TitleLayout? title)
    {
        // Axes used by the groups, with their labels and scales.
        var axes = new List<AxisLayout>();
        foreach (var axis in model.Axes)
        {
            var users = groups.Where(g => g.AxisIds.Contains(axis.Id)).ToList();
            if (users.Count == 0)
                continue;
            axes.Add(new AxisLayout
            {
                Axis = axis,
                Groups = users,
                Style = Style(axis.TxPr, axis.IsValue ? "valueAxis" : "categoryAxis"),
            });
        }
        foreach (var layout in axes.Where(a => !a.Axis.IsValue))
        {
            var crossing = axes.FirstOrDefault(a => a.Axis.Id == layout.Axis.CrossAxisId);
            layout.Between = crossing?.Axis.CrossBetween != "midCat";
            var series = layout.Groups.SelectMany(g => g.Series).ToList();
            layout.Categories = series.Count == 0 ? 0 : series.Max(s => Math.Max(s.Values.Count, s.Categories.Count));
            var names = series.Select(s => s.Categories).FirstOrDefault(c => c.Count > 0) ?? [];
            layout.Labels = Enumerable.Range(0, layout.Categories)
                .Select(i => i < names.Count ? names[i] : (i + 1).ToString(culture)).ToList();
            layout.MaxLabelWidth = layout.Labels.Count == 0 ? 0 : layout.Labels.Max(l => Width(l, layout.Style));
        }

        var inner = ManualPlot();
        var plot = inner ?? outer;
        for (var pass = 0; pass < 3; pass++)
        {
            foreach (var layout in axes.Where(a => a.Axis.IsValue))
                ScaleValueAxis(layout, layout.Axis.Horizontal ? plot.W : plot.H);
            if (inner != null)
                break;
            var next = InnerPlot(outer, axes);
            if (Math.Abs(next.W - plot.W) < 0.01 && Math.Abs(next.H - plot.H) < 0.01)
            {
                plot = next;
                break;
            }
            plot = next;
        }

        foreach (var layout in axes)
        {
            var horizontal = layout.Axis.Horizontal;
            (layout.Start, layout.End) = horizontal ? (plot.X, plot.Right) : (plot.Bottom, plot.Y);
            if (layout.Axis.Reversed)
                (layout.Start, layout.End) = (layout.End, layout.Start);
        }

        var svg = new StringBuilder();
        svg.Append(Area(model.PlotArea?.Element(Ns.C + "spPr"), plot, snap: false));

        foreach (var layout in axes.Where(a => a.Visible || a.Axis.MajorGridlines != null))
            svg.Append(Gridlines(layout, plot));

        foreach (var group in groups)
        {
            var categoryAxis = axes.FirstOrDefault(a => !a.Axis.IsValue && group.AxisIds.Contains(a.Axis.Id));
            var valueAxis = axes.FirstOrDefault(a => a.Axis.IsValue && group.AxisIds.Contains(a.Axis.Id) && a != categoryAxis);
            if (valueAxis == null)
                continue;
            if (group.IsScatter)
            {
                var xAxis = axes.FirstOrDefault(a => a.Axis.IsValue && group.AxisIds.Contains(a.Axis.Id) && a.Axis.Horizontal);
                var yAxis = axes.FirstOrDefault(a => a.Axis.IsValue && group.AxisIds.Contains(a.Axis.Id) && !a.Axis.Horizontal);
                if (xAxis != null && yAxis != null)
                    svg.Append(Scatter(group, xAxis, yAxis));
                continue;
            }
            if (categoryAxis == null)
                continue;
            if (group.IsBar) svg.Append(Bars(group, categoryAxis, valueAxis));
            else if (group.IsArea) svg.Append(Areas(group, categoryAxis, valueAxis));
            else if (group.IsLine) svg.Append(Lines(group, categoryAxis, valueAxis));
        }

        foreach (var layout in axes.Where(a => a.Visible))
            svg.Append(AxisLine(layout, axes, plot));
        foreach (var layout in axes.Where(a => a.HasLabels))
            svg.Append(AxisLabels(layout, plot));
        return svg.ToString();
    }

    /// <summary>The plot area inside the axis labels (PowerPoint's inner plot area) for an automatic layout.</summary>
    static Box InnerPlot(Box outer, List<AxisLayout> axes)
    {
        double left = outer.X, top = outer.Y, right = outer.Right, bottom = outer.Bottom;
        foreach (var layout in axes.Where(a => a.HasLabels))
        {
            var s = layout.Style.Size;
            var lineHeight = LineHeight(layout.Style);
            switch (layout.Axis.Position)
            {
                case "l":
                    left = Math.Max(left, outer.X - LabelBleed + layout.MaxLabelWidth + ValueLabelGap * s);
                    break;
                case "r":
                    right = Math.Min(right, outer.Right + LabelBleed - layout.MaxLabelWidth - ValueLabelGap * s);
                    break;
                case "b":
                    bottom = Math.Min(bottom, outer.Bottom + LabelBleed - lineHeight - CategoryLabelGap * s);
                    break;
                case "t":
                    top = Math.Max(top, outer.Y - LabelBleed + lineHeight + CategoryLabelGap * s);
                    break;
            }
        }
        // Value labels centered on the outermost ticks may reach a little past the outer edge.
        foreach (var layout in axes.Where(a => a.HasLabels && a.Axis.IsValue && a.Labels.Count > 0))
        {
            if (layout.Axis.Horizontal)
                right = Math.Min(right, outer.Right - Width(layout.Labels[^1], layout.Style) / 2);
            else
                top = Math.Max(top, outer.Y - 6 + LineHeight(layout.Style) / 2);
        }
        return new Box(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    void ScaleValueAxis(AxisLayout layout, double length)
    {
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        var percent = false;
        foreach (var group in layout.Groups)
        {
            percent |= group.PercentStacked;
            foreach (var (lo, hi) in Ranges(group, layout.Axis))
            {
                min = Math.Min(min, lo);
                max = Math.Max(max, hi);
            }
        }
        if (double.IsInfinity(min)) (min, max) = (0, 1);

        var style = layout.Style;
        var extent = layout.Axis.Horizontal
            ? Math.Max(Width(NumberFormat.Format(max, Format(layout), culture), style), Width(NumberFormat.Format(min, Format(layout), culture), style)) + style.Size
            : LineHeight(style);
        var maxIntervals = Math.Clamp((int)Math.Floor(length / Math.Max(1, extent * 1.5)), 1, 10);
        layout.Scale = AxisScaling.Compute(min, max, layout.Axis, maxIntervals, percent);
        layout.Labels = layout.Scale.Ticks().Select(v => NumberFormat.Format(v, Format(layout), culture)).ToList();
        layout.MaxLabelWidth = layout.Labels.Count == 0 ? 0 : layout.Labels.Max(l => Width(l, style));
    }

    string Format(AxisLayout layout)
    {
        if (layout.Groups.Any(g => g.PercentStacked) && (layout.Axis.SourceLinked || layout.Axis.FormatCode == "General"))
            return "0%";
        if (layout.Axis.SourceLinked)
            return layout.Groups.SelectMany(g => g.Series).FirstOrDefault()?.FormatCode ?? layout.Axis.FormatCode;
        return layout.Axis.FormatCode;
    }

    /// <summary>The value ranges a group contributes to an axis (stacked groups contribute their sums).</summary>
    static IEnumerable<(double Min, double Max)> Ranges(ChartGroup group, ChartAxis axis)
    {
        if (group.IsScatter)
        {
            foreach (var s in group.Series)
            {
                var values = (axis.Horizontal ? (s.XValues.Count > 0 ? s.XValues : Enumerable.Range(1, s.Values.Count).Select(i => (double?)i).ToList()) : s.Values)
                    .Where(v => v.HasValue).Select(v => v!.Value).ToList();
                if (values.Count > 0) yield return (values.Min(), values.Max());
            }
            yield break;
        }
        if (!group.Stacked)
        {
            foreach (var s in group.Series)
            {
                var values = s.Values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
                if (values.Count > 0) yield return (values.Min(), values.Max());
            }
            yield break;
        }
        var count = group.Series.Count == 0 ? 0 : group.Series.Max(s => s.Values.Count);
        for (var i = 0; i < count; i++)
        {
            double pos = 0, neg = 0;
            foreach (var s in group.Series)
            {
                var v = s.Values.ElementAtOrDefault(i) ?? 0;
                if (v >= 0) pos += v; else neg += v;
            }
            if (group.PercentStacked)
            {
                var total = pos - neg;
                yield return total == 0 ? (0, 0) : (neg / total, pos / total);
            }
            else
            {
                yield return (neg, pos);
            }
        }
    }

    /// <summary>The value at which the category axis crosses a value axis (and bars start).</summary>
    static double CrossValue(AxisLayout value, ChartAxis? category)
    {
        var scale = value.Scale;
        var crosses = category?.Crosses ?? "autoZero";
        if (category?.CrossesAt is { } at)
            return Math.Clamp(at, scale.Min, scale.Max);
        return crosses switch
        {
            "min" => scale.Min,
            "max" => scale.Max,
            _ => scale.LogBase != null ? scale.Min : Math.Clamp(0, scale.Min, scale.Max),
        };
    }

    string Gridlines(AxisLayout layout, Box plot)
    {
        var svg = new StringBuilder();
        foreach (var (element, minor) in new[] { (layout.Axis.MinorGridlines, true), (layout.Axis.MajorGridlines, false) })
        {
            if (element == null)
                continue;
            var outline = element.Element(Ns.C + "spPr")?.Element(Ns.A + "ln") is { } ln
                ? OutlineReader.Merge([(ln, slide.Colors)])
                : new Outline(9525, new SolidFill(RgbColor.FromHex("D9D9D9")), "solid", "flat", "round", 8, "sng");
            IEnumerable<double> positions;
            if (layout.Axis.IsValue)
            {
                positions = (minor ? layout.Scale.MinorTicks() : layout.Scale.Ticks()).Select(layout.Map);
            }
            else
            {
                var step = minor ? 1 : 1;
                positions = Enumerable.Range(0, layout.Categories + 1).Where(i => i % step == 0)
                    .Select(i => layout.Between ? layout.Boundary(i) : layout.Center(Math.Min(i, layout.Categories - 1))).Distinct();
            }
            foreach (var p in positions)
            {
                svg.Append(layout.Axis.Horizontal
                    ? GridLine(outline, p, plot.Y, p, plot.Bottom)
                    : GridLine(outline, plot.X, p, plot.Right, p));
            }
        }
        return svg.ToString();
    }

    string AxisLine(AxisLayout layout, List<AxisLayout> axes, Box plot)
    {
        var outline = layout.Axis.SpPr?.Element(Ns.A + "ln") is { } ln
            ? OutlineReader.Merge([(ln, slide.Colors)])
            : new Outline(9525, new SolidFill(RgbColor.FromHex("BFBFBF")), "solid", "flat", "round", 8, "sng");
        if (outline is not { Visible: true })
            return "";
        // The axis line lies where it crosses the perpendicular axis.
        var cross = axes.FirstOrDefault(a => a.Axis.Id == layout.Axis.CrossAxisId);
        double position;
        if (cross is { Axis.IsValue: true })
            position = cross.Map(CrossValue(cross, layout.Axis));
        else if (cross != null)
            position = layout.Axis.Crosses == "max" ? cross.End : cross.Start;
        else
            position = layout.Axis.Horizontal ? plot.Bottom : plot.X;
        return layout.Axis.Horizontal
            ? GridLine(outline, plot.X, position, plot.Right, position)
            : GridLine(outline, position, plot.Y, position, plot.Bottom);
    }

    string AxisLabels(AxisLayout layout, Box plot)
    {
        var svg = new StringBuilder();
        var style = layout.Style;
        var s = style.Size;
        var positions = layout.Axis.IsValue
            ? layout.Scale.Ticks().Select(layout.Map).ToList()
            : Enumerable.Range(0, layout.Categories).Select(layout.Center).ToList();
        for (var i = 0; i < layout.Labels.Count && i < positions.Count; i++)
        {
            var label = layout.Labels[i];
            var w = Width(label, style);
            var p = positions[i];
            switch (layout.Axis.Position)
            {
                case "l":
                    svg.Append(text.Label(label, style, plot.X - ValueLabelGap * s - w, CenteredBaseline(style, p)));
                    break;
                case "r":
                    svg.Append(text.Label(label, style, plot.Right + ValueLabelGap * s, CenteredBaseline(style, p)));
                    break;
                case "b":
                    svg.Append(text.Label(label, style, p - w / 2, plot.Bottom + CategoryLabelGap * s + Ascent(style)));
                    break;
                case "t":
                    svg.Append(text.Label(label, style, p - w / 2, plot.Y - CategoryLabelGap * s - Descent(style)));
                    break;
            }
        }
        return svg.ToString();
    }

    // ---------------------------------------------------------------- series

    string Bars(ChartGroup group, AxisLayout category, AxisLayout value)
    {
        var svg = new StringBuilder();
        var n = group.Series.Count;
        if (n == 0 || category.Categories == 0)
            return "";
        var gap = group.GapWidth / 100;
        var overlap = group.Stacked ? 1 : Math.Clamp(group.Overlap / 100, -1, 1);
        var slots = group.Stacked ? 1 : n;
        var categoryWidth = Math.Abs(category.End - category.Start) / category.Categories;
        var barWidth = categoryWidth / (slots - (slots - 1) * overlap + gap);
        var direction = Math.Sign(category.End - category.Start);
        var cross = CrossValue(value, category.Axis);

        for (var i = 0; i < category.Categories; i++)
        {
            double positive = 0, negative = 0;
            var total = group.PercentStacked ? group.Series.Sum(s => Math.Abs(s.Values.ElementAtOrDefault(i) ?? 0)) : 1;
            for (var k = 0; k < n; k++)
            {
                var series = group.Series[k];
                if (series.Values.ElementAtOrDefault(i) is not { } v)
                    continue;
                double from, to;
                if (group.Stacked)
                {
                    if (group.PercentStacked) v = total == 0 ? 0 : v / total;
                    if (v >= 0) { from = positive; positive += v; to = positive; }
                    else { from = negative; negative += v; to = negative; }
                }
                else
                {
                    from = cross;
                    to = v;
                }
                var slot = group.Stacked ? 0 : k;
                var offset = gap * barWidth / 2 + slot * barWidth * (1 - overlap);
                var c0 = category.Boundary(i) + direction * offset;
                var c1 = c0 + direction * barWidth;
                var v0 = value.Map(Math.Clamp(from, value.Scale.Min, value.Scale.Max));
                var v1 = value.Map(Math.Clamp(to, value.Scale.Min, value.Scale.Max));
                var box = group.Horizontal
                    ? new Box(Math.Min(v0, v1), Math.Min(c0, c1), Math.Abs(v1 - v0), Math.Abs(c1 - c0))
                    : new Box(Math.Min(c0, c1), Math.Min(v0, v1), Math.Abs(c1 - c0), Math.Abs(v1 - v0));
                svg.Append(Rect(box, PointFill(group, series, i), PointOutline(series, i), snap: true));
            }
        }
        return svg.ToString();
    }

    List<(double X, double Y)?> SeriesPoints(ChartGroup group, int k, AxisLayout category, AxisLayout value, double[] stack)
    {
        var series = group.Series[k];
        var points = new List<(double X, double Y)?>();
        for (var i = 0; i < category.Categories; i++)
        {
            var v = series.Values.ElementAtOrDefault(i);
            if (group.Stacked)
            {
                var total = group.PercentStacked ? group.Series.Sum(s => Math.Abs(s.Values.ElementAtOrDefault(i) ?? 0)) : 1;
                stack[i] += (v ?? 0) / (total == 0 ? 1 : total);
                v = stack[i];
            }
            if (v is not { } value1)
            {
                points.Add(null);
                continue;
            }
            var c = category.Center(i);
            var p = value.Map(Math.Clamp(value1, value.Scale.Min, value.Scale.Max));
            points.Add(category.Axis.Horizontal ? (c, p) : (p, c));
        }
        return points;
    }

    string Lines(ChartGroup group, AxisLayout category, AxisLayout value)
    {
        var svg = new StringBuilder();
        var stack = new double[category.Categories];
        for (var k = 0; k < group.Series.Count; k++)
        {
            var series = group.Series[k];
            var points = SeriesPoints(group, k, category, value, stack);
            var outline = SeriesOutline(group, series);
            var smooth = ChartModel.Flag(series.Element, "smooth", false);
            if (outline is { Visible: true })
            {
                foreach (var run in Runs(points))
                    svg.Append($"<path d=\"{PathData(run.Select(p => SnapPoint(p, outline)).ToList(), smooth)}\" fill=\"none\"{paint.Stroke(outline, 0, 0, defs)}/>");
            }
            if (Marker(group, series) is { } marker)
            {
                for (var i = 0; i < points.Count; i++)
                {
                    if (points[i] is { } p)
                        svg.Append(DrawMarker(marker, series.Index, p.X, p.Y, PointFill(group, series, i), outline));
                }
            }
        }
        return svg.ToString();
    }

    string Areas(ChartGroup group, AxisLayout category, AxisLayout value)
    {
        var svg = new StringBuilder();
        var stack = new double[category.Categories];
        var baseline = Enumerable.Repeat(value.Map(CrossValue(value, category.Axis)), category.Categories).ToArray();
        var horizontal = category.Axis.Horizontal;
        for (var k = 0; k < group.Series.Count; k++)
        {
            var series = group.Series[k];
            var points = SeriesPoints(group, k, category, value, stack);
            var top = points.Select((p, i) => p ?? (horizontal ? (category.Center(i), baseline[i]) : (baseline[i], category.Center(i)))).ToList();
            var d = new StringBuilder();
            for (var i = 0; i < top.Count; i++)
                d.Append(i == 0 ? 'M' : 'L').Append(Px(top[i].X)).Append(' ').Append(Px(top[i].Y));
            for (var i = top.Count - 1; i >= 0; i--)
            {
                var b = horizontal ? (category.Center(i), baseline[i]) : (baseline[i], category.Center(i));
                d.Append('L').Append(Px(b.Item1)).Append(' ').Append(Px(b.Item2));
            }
            d.Append('Z');
            svg.Append($"<path d=\"{d}\"{paint.Fill(PointFill(group, series, -1), 0, 0, defs)}{paint.Stroke(PointOutline(series, -1), 0, 0, defs)}/>");
            if (group.Stacked)
            {
                for (var i = 0; i < top.Count; i++)
                    baseline[i] = horizontal ? top[i].Y : top[i].X;
            }
        }
        return svg.ToString();
    }

    string Scatter(ChartGroup group, AxisLayout xAxis, AxisLayout yAxis)
    {
        var svg = new StringBuilder();
        var style = ChartModel.Val(group.Element, "scatterStyle") ?? "marker";
        foreach (var series in group.Series)
        {
            var points = new List<(double X, double Y)?>();
            for (var i = 0; i < series.Values.Count; i++)
            {
                var x = series.XValues.Count > 0 ? series.XValues.ElementAtOrDefault(i) : i + 1;
                if (x is not { } xv || series.Values[i] is not { } yv)
                {
                    points.Add(null);
                    continue;
                }
                points.Add((xAxis.Map(xv), yAxis.Map(yv)));
            }
            var outline = SeriesOutline(group, series);
            var lines = series.SpPr?.Element(Ns.A + "ln")?.Element(Ns.A + "noFill") == null && style is not ("marker" or "none") || series.SpPr?.Element(Ns.A + "ln")?.Element(Ns.A + "solidFill") != null;
            if (lines && outline is { Visible: true })
            {
                foreach (var run in Runs(points))
                    svg.Append($"<path d=\"{PathData(run.Select(p => SnapPoint(p, outline)).ToList(), ChartModel.Flag(series.Element, "smooth", false))}\" fill=\"none\"{paint.Stroke(outline, 0, 0, defs)}/>");
            }
            if (Marker(group, series) is { } marker)
            {
                foreach (var p in points.Where(p => p.HasValue))
                    svg.Append(DrawMarker(marker, series.Index, p!.Value.X, p.Value.Y, PointFill(group, series, -1), outline));
            }
        }
        return svg.ToString();
    }

    static IEnumerable<List<(double X, double Y)>> Runs(List<(double X, double Y)?> points)
    {
        var run = new List<(double X, double Y)>();
        foreach (var p in points)
        {
            if (p is { } point)
            {
                run.Add(point);
                continue;
            }
            if (run.Count > 0) yield return run;
            run = [];
        }
        if (run.Count > 0) yield return run;
    }

    /// <summary>Path data through the points; smoothed lines use Catmull-Rom splines converted to Béziers.</summary>
    static string PathData(List<(double X, double Y)> points, bool smooth)
    {
        var d = new StringBuilder($"M{Px(points[0].X)} {Px(points[0].Y)}");
        for (var i = 1; i < points.Count; i++)
        {
            if (!smooth || points.Count < 3)
            {
                d.Append($"L{Px(points[i].X)} {Px(points[i].Y)}");
                continue;
            }
            var p0 = points[Math.Max(0, i - 2)];
            var p1 = points[i - 1];
            var p2 = points[i];
            var p3 = points[Math.Min(points.Count - 1, i + 1)];
            const double t = 1.0 / 6;
            var c1 = (p1.X + (p2.X - p0.X) * t, p1.Y + (p2.Y - p0.Y) * t);
            var c2 = (p2.X - (p3.X - p1.X) * t, p2.Y - (p3.Y - p1.Y) * t);
            d.Append($"C{Px(c1.Item1)} {Px(c1.Item2)} {Px(c2.Item1)} {Px(c2.Item2)} {Px(p2.X)} {Px(p2.Y)}");
        }
        return d.ToString();
    }

    /// <summary>The marker of a line or scatter series, or null when markers are off.</summary>
    static XElement? Marker(ChartGroup group, ChartSeries series)
    {
        var own = series.Element.Element(Ns.C + "marker");
        var symbol = ChartModel.Val(own, "symbol");
        if (symbol == "none")
            return null;
        if (own != null && symbol != null)
            return own;
        var groupMarkers = group.IsScatter
            ? (ChartModel.Val(group.Element, "scatterStyle") ?? "marker") is "marker" or "lineMarker" or "smoothMarker"
            : ChartModel.Flag(group.Element, "marker", false);
        return groupMarkers ? own ?? new XElement(Ns.C + "marker") : null;
    }

    string DrawMarker(XElement marker, int seriesIndex, double x, double y, Fill seriesFill, Outline? seriesLine)
    {
        var symbol = ChartModel.Val(marker, "symbol") ?? MarkerCycle[seriesIndex % MarkerCycle.Length];
        if (symbol == "auto") symbol = MarkerCycle[seriesIndex % MarkerCycle.Length];
        var size = Math.Clamp(ChartModel.Number(marker, "size") ?? 5, 2, 72);
        var spPr = marker.Element(Ns.C + "spPr");
        var fill = FillReader.FindFill(spPr) is { } f ? FillReader.Read(f, slide.Colors) : seriesFill is NoFill ? new SolidFill(seriesLine?.Fill is SolidFill sf ? sf.Color : RgbColor.Black) : seriesFill;
        var line = spPr?.Element(Ns.A + "ln") is { } ln
            ? OutlineReader.Merge([(ln, slide.Colors)])
            : seriesLine is { Visible: true } ? seriesLine with { WidthEmu = 9525, Dash = "solid" } : null;
        var r = size / 2;
        string d = symbol switch
        {
            "circle" or "dot" => $"M{Px(x - r)} {Px(y)}a{Px(r)} {Px(r)} 0 1 0 {Px(2 * r)} 0a{Px(r)} {Px(r)} 0 1 0 {Px(-2 * r)} 0Z",
            "square" => $"M{Px(x - r)} {Px(y - r)}h{Px(size)}v{Px(size)}h{Px(-size)}Z",
            "diamond" => $"M{Px(x)} {Px(y - r)}L{Px(x + r)} {Px(y)}L{Px(x)} {Px(y + r)}L{Px(x - r)} {Px(y)}Z",
            "triangle" => $"M{Px(x)} {Px(y - r)}L{Px(x + r)} {Px(y + r)}L{Px(x - r)} {Px(y + r)}Z",
            "x" => $"M{Px(x - r)} {Px(y - r)}L{Px(x + r)} {Px(y + r)}M{Px(x + r)} {Px(y - r)}L{Px(x - r)} {Px(y + r)}",
            "plus" => $"M{Px(x - r)} {Px(y)}L{Px(x + r)} {Px(y)}M{Px(x)} {Px(y - r)}L{Px(x)} {Px(y + r)}",
            "star" => $"M{Px(x - r)} {Px(y - r)}L{Px(x + r)} {Px(y + r)}M{Px(x + r)} {Px(y - r)}L{Px(x - r)} {Px(y + r)}M{Px(x)} {Px(y - r)}L{Px(x)} {Px(y + r)}",
            "dash" => $"M{Px(x - r)} {Px(y - size / 10)}h{Px(size)}v{Px(size / 5)}h{Px(-size)}Z",
            _ => "",
        };
        if (d.Length == 0)
            return "";
        var open = symbol is "x" or "plus" or "star";
        return open
            ? $"<path d=\"{d}\" fill=\"none\"{paint.Stroke(line ?? new Outline(9525, fill, "solid", "flat", "round", 8, "sng"), 0, 0, defs)}/>"
            : $"<path d=\"{d}\"{paint.Fill(fill, 0, 0, defs)}{paint.Stroke(line, 0, 0, defs)}/>";
    }

    // ---------------------------------------------------------------- pie and doughnut

    string Pie(ChartGroup group, Box plot)
    {
        var svg = new StringBuilder();
        var doughnut = group.Type == "doughnutChart";
        var rings = doughnut ? group.Series : group.Series.Take(1).ToList();
        if (rings.Count == 0)
            return "";
        var maxExplosion = rings.SelectMany(s => s.Points.Values.Select(p => ChartModel.Number(p, "explosion") ?? 0)
            .Append(ChartModel.Number(s.Element, "explosion") ?? 0)).DefaultIfEmpty(0).Max() / 100;
        var radius = Math.Min(plot.W, plot.H) / 2 / (1 + maxExplosion);
        var (cx, cy) = (plot.X + plot.W / 2, plot.Y + plot.H / 2);
        var hole = doughnut ? Math.Clamp(group.HoleSize, 10, 90) / 100 : 0;
        var ringWidth = (radius - radius * hole) / rings.Count;

        for (var ring = 0; ring < rings.Count; ring++)
        {
            var series = rings[ring];
            var outer = radius - ring * ringWidth;
            var inner = doughnut ? outer - ringWidth : 0;
            var total = series.Values.Sum(v => Math.Abs(v ?? 0));
            if (total <= 0)
                continue;
            var angle = group.FirstSliceAngle;
            for (var i = 0; i < series.Values.Count; i++)
            {
                var v = Math.Abs(series.Values[i] ?? 0);
                if (v <= 0)
                    continue;
                var sweep = v / total * 360;
                var explosion = (series.Points.TryGetValue(i, out var dPt) ? ChartModel.Number(dPt, "explosion") : null)
                                ?? ChartModel.Number(series.Element, "explosion") ?? 0;
                var mid = (angle + sweep / 2) * Math.PI / 180;
                var (ox, oy) = (Math.Sin(mid) * outer * explosion / 100, -Math.Cos(mid) * outer * explosion / 100);
                svg.Append($"<path d=\"{Slice(cx + ox, cy + oy, outer, inner, angle, sweep)}\"{paint.Fill(PointFill(group, series, i), 0, 0, defs)}{paint.Stroke(PointOutline(series, i), 0, 0, defs)}/>");
                angle += sweep;
            }
        }
        return svg.ToString();
    }

    static string Slice(double cx, double cy, double r, double inner, double start, double sweep)
    {
        (double X, double Y) Point(double radius, double degrees)
        {
            var a = degrees * Math.PI / 180;
            return (cx + Math.Sin(a) * radius, cy - Math.Cos(a) * radius);
        }
        if (sweep >= 359.999)
        {
            var ring = $"M{Px(cx - r)} {Px(cy)}a{Px(r)} {Px(r)} 0 1 0 {Px(2 * r)} 0a{Px(r)} {Px(r)} 0 1 0 {Px(-2 * r)} 0Z";
            return inner > 0 ? ring + $"M{Px(cx - inner)} {Px(cy)}a{Px(inner)} {Px(inner)} 0 1 1 {Px(2 * inner)} 0a{Px(inner)} {Px(inner)} 0 1 1 {Px(-2 * inner)} 0Z" : ring;
        }
        var large = sweep > 180 ? 1 : 0;
        var a0 = Point(r, start);
        var a1 = Point(r, start + sweep);
        if (inner <= 0)
            return $"M{Px(cx)} {Px(cy)}L{Px(a0.X)} {Px(a0.Y)}A{Px(r)} {Px(r)} 0 {large} 1 {Px(a1.X)} {Px(a1.Y)}Z";
        var b1 = Point(inner, start + sweep);
        var b0 = Point(inner, start);
        return $"M{Px(a0.X)} {Px(a0.Y)}A{Px(r)} {Px(r)} 0 {large} 1 {Px(a1.X)} {Px(a1.Y)}L{Px(b1.X)} {Px(b1.Y)}A{Px(inner)} {Px(inner)} 0 {large} 0 {Px(b0.X)} {Px(b0.Y)}Z";
    }
}
