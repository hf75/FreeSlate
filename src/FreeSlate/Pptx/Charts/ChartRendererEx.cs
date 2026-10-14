using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Drawing;
using FreeSlate.Html;
using FreeSlate.Text;

namespace FreeSlate.Pptx.Charts;

/// <summary>
/// Office 2016 charts (cx:chartSpace, catalog: chart.ex) drawn natively where a file has no fallback picture of them:
/// the chart is drawn as a classic chart frame (title, legend, axes, gridlines) built from it, histograms and Pareto
/// charts with their bars and line, and the plot of waterfalls, funnels, box and whisker charts, treemaps and
/// sunbursts laid out here on top of the frame.
/// </summary>
public sealed partial class ChartRenderer
{
    ChartExModel? ex;

    /// <summary>The chartex plot drawn over the frame's gridlines with the frame's axes, or over its whole plot area.</summary>
    Func<List<AxisLayout>, Box, string>? exAxisPlot;
    Func<Box, string>? exPlot;

    // ---------------------------------------------------------------- layout

    // Office 2016 charts are laid out tighter than classic ones (measured with PowerPoint on charts from 130 to 450 pt
    // wide in 11.97 pt Aptos, s the font size, L the line height): the title's line 5.15 pt from the top, the plot area
    // 6 pt from the sides and the bottom, 4.25 pt below the title's line, 9.2 pt below the legend's last line; axis
    // labels 6.9 pt from the plot area, value labels reaching 1.5 pt and category labels 1 pt into the margin.
    const double ExTitleTop = 5.15, ExMargin = 6, ExTitleGap = 4.25, ExLegendGap = 8.3, ExLegendPlotGap = 9.2, ExLabelGap = 6.9,
        ExValueBleed = 1.5, ExCategoryBleed = 1;

    // Legends: rows a line plus 4.14 pt apart, square keys 0.5 s wide 0.375 s before their text, entries 0.66 s apart,
    // at most 80 % of the chart wide before they wrap into columns.
    const double ExLegendRowExtra = 4.14, ExKey = 0.5, ExKeyGap = 0.375, ExEntryGap = 0.66, ExLegendShare = 0.8;

    /// <summary>How category labels along the bottom fit: in a row, staggered in two rows, or turned (degrees).</summary>
    enum ExLabelMode { Straight, Staggered, Rotated }

    /// <summary>
    /// The way category labels of the given widest width fit their slots (measured on labels 1 to 2.3 slots wide: in a
    /// row while they fit, staggered up to 1.3 slots, turned by −30° up to 1.65 slots, by −60° up to 2.15 slots, else
    /// upright at −90°).
    /// </summary>
    static (ExLabelMode Mode, double Rotation) ExLabels(double widest, double slot) => (widest / Math.Max(1e-9, slot)) switch
    {
        <= 1 => (ExLabelMode.Straight, 0),
        <= 1.3 => (ExLabelMode.Staggered, 0),
        <= 1.65 => (ExLabelMode.Rotated, -30),
        <= 2.15 => (ExLabelMode.Rotated, -60),
        _ => (ExLabelMode.Rotated, -90),
    };

    /// <summary>The extent of category labels below the axis: a line, two staggered lines, or the turned label's height.</summary>
    static double ExLabelExtent(AxisLayout layout, ExLabelMode mode)
    {
        var lineHeight = LineHeight(layout.Style);
        var a = Math.Abs(layout.Rotation) * Math.PI / 180;
        return mode switch
        {
            ExLabelMode.Straight => lineHeight,
            ExLabelMode.Staggered => 2 * lineHeight,
            _ => layout.MaxLabelWidth * Math.Sin(a) + lineHeight * Math.Cos(a),
        };
    }

    /// <summary>
    /// The plot area of an Office 2016 chart with its axis labels (see <see cref="ExMargin"/>): below the title or the
    /// legend, the chart's margin elsewhere.
    /// </summary>
    Box ExOuterPlot(TitleLayout? title, LegendLayout? legend)
    {
        double top = ExMargin, bottom = height - ExMargin, left = ExMargin, right = width - ExMargin;
        // Pareto charts, with their percentages on a second axis, keep 7.5 pt more on both sides (measured).
        if (ex?.Series.Any(s => s.Layout == "paretoLine") == true)
            (left, right) = (left + 7.5, right - 7.5);
        if (title is { Overlay: false })
            top = title.Bottom - TitlePaddingY + ExTitleGap;
        if (legend is { Overlay: false })
        {
            switch (legend.Position)
            {
                case "t": top = legend.Box.Bottom - ExLegendRowExtra / 2 + ExLegendPlotGap; break;
                case "b": bottom = legend.Box.Y + ExLegendRowExtra / 2 - ExLegendPlotGap; break;
                case "l": left = legend.Box.Right + ExLegendPlotGap; break;
                default: right = legend.Box.X - ExLegendPlotGap; break;
            }
        }
        return new Box(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    /// <summary>
    /// The inner plot area of an Office 2016 chart: value labels beside it (and half a line above it, 1.43 pt less
    /// below a title alone), category labels on the left (funnels) or along the bottom in the way they fit, the first
    /// turned label kept 4.8 pt from the chart's left edge and the last staggered one 5.5 pt from its right edge
    /// (measured).
    /// </summary>
    Box ExInnerPlot(Box outer, List<AxisLayout> axes, bool legendAbove)
    {
        double left = outer.X, top = outer.Y, right = outer.Right, bottom = outer.Bottom;
        foreach (var layout in axes.Where(a => a.HasLabels && !a.Axis.Horizontal))
        {
            var extent = layout.MaxLabelWidth + ExLabelGap - ExValueBleed;
            if (layout.Axis.Position == "r")
                right = Math.Min(right, outer.Right - extent);
            else
                left = Math.Max(left, outer.X + extent);
            if (layout.Axis.IsValue)
                top = Math.Max(top, outer.Y + LineHeight(layout.Style) / 2 - (legendAbove ? 0 : 1.43));
        }
        foreach (var layout in axes.Where(a => a.HasLabels && a.Axis.Horizontal && !a.Axis.IsValue))
        {
            // The room taken for one way stays when the narrower slots then call for another (measured: labels
            // 1.62 slots wide turned by −30° keep the plot area's left edge that leaves them when they turn by −60°).
            var n = Math.Max(1, layout.Categories);
            var mode = ExLabelMode.Straight;
            for (var pass = 0; pass < 3; pass++)
            {
                var slot = (right - left) / n;
                (mode, layout.Rotation) = ExLabels(layout.MaxLabelWidth, slot);
                if (mode == ExLabelMode.Rotated && layout.Rotation > -90)
                {
                    // The first label's upper left corner stays 4.8 pt inside the chart: cx1 − w cos a − L/2 sin a ≥ 4.8.
                    var a = -layout.Rotation * Math.PI / 180;
                    var reach = Width(layout.Labels.FirstOrDefault() ?? "", layout.Style) * Math.Cos(a) + LineHeight(layout.Style) / 2 * Math.Sin(a);
                    left = Math.Max(left, (4.8 + reach - right / (2 * n)) / (1 - 1.0 / (2 * n)));
                }
                else if (mode == ExLabelMode.Staggered)
                {
                    // The last label, centered on its category, ends 5.5 pt before the chart's right edge.
                    var last = Width(layout.Labels.LastOrDefault() ?? "", layout.Style);
                    right = Math.Min(right, (width - 5.5 - last / 2 - left / (2 * n)) / (1 - 1.0 / (2 * n)));
                }
            }
            layout.ExMode = mode;
            bottom = Math.Min(bottom, outer.Bottom + ExCategoryBleed - ExLabelExtent(layout, mode) - ExLabelGap);
        }
        return new Box(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    /// <summary>A label's style on a fill: white text on dark fills, its own on light ones.</summary>
    static TextStyle Contrasting(TextStyle style, Fill fill) =>
        Fill.MainColor(fill) is { } color && 0.299 * color.R + 0.587 * color.G + 0.114 * color.B < 0.5 ? style with { Color = RgbColor.White } : style;

    /// <summary>The baseline of a label whose capitals are centered on <paramref name="center"/> (measured on value labels).</summary>
    static double ExCenteredBaseline(TextStyle style, double center) => center + 0.3285 * style.Size;

    /// <summary>
    /// Axis labels of an Office 2016 chart: value labels and category labels on the left right-aligned 6.9 pt from the
    /// plot area with their capitals centered on their tick or category; category labels below it centered on their
    /// category (staggered ones alternating, the first in the lower row), turned ones ending at their category with the
    /// middle of their line, their highest corner 6.9 pt below the plot area.
    /// </summary>
    string ExAxisLabels(AxisLayout layout, Box plot)
    {
        var svg = new StringBuilder();
        var style = layout.Style;
        var lineHeight = LineHeight(style);
        var positions = layout.Axis.IsValue
            ? layout.Scale.Ticks().Select(layout.Map).ToList()
            : Enumerable.Range(0, layout.Categories).Select(layout.Center).ToList();
        for (var i = 0; i < layout.Labels.Count && i < positions.Count; i++)
        {
            var label = layout.Labels[i];
            var w = Width(label, style);
            var p = positions[i];
            if (!layout.Axis.Horizontal)
            {
                var x = layout.Axis.Position == "r" ? plot.Right + ExLabelGap : plot.X - ExLabelGap - w;
                svg.Append(text.Label(label, style, x, ExCenteredBaseline(style, p)));
                continue;
            }
            var top = plot.Bottom + ExLabelGap;
            switch (layout.ExMode)
            {
                case ExLabelMode.Rotated:
                {
                    var anchorY = top + lineHeight / 2 * Math.Cos(-layout.Rotation * Math.PI / 180);
                    svg.Append($"<g transform=\"translate({Px(p)} {Px(anchorY)}) rotate({Units.Css(layout.Rotation)})\">");
                    svg.Append(text.Label(label, style, -w, (Ascent(style) - Descent(style)) / 2));
                    svg.Append("</g>");
                    break;
                }
                case ExLabelMode.Staggered:
                    svg.Append(text.Label(label, style, p - w / 2, top + (i % 2 == 0 ? lineHeight : 0) + Ascent(style)));
                    break;
                default:
                    svg.Append(text.Label(label, style, p - w / 2, top + Ascent(style)));
                    break;
            }
        }
        return svg.ToString();
    }

    /// <summary>
    /// The legend of an Office 2016 chart (see <see cref="ExLegendRowExtra"/>): its entries packed in a row centered
    /// below the title, in columns as wide as the widest entry where the row would be too wide, or stacked beside the
    /// plot area.
    /// </summary>
    LegendLayout ExLegend(XElement element, TextStyle style, List<LegendEntry> entries, TitleLayout? title)
    {
        var s = style.Size;
        var lineHeight = LineHeight(style);
        var pitch = lineHeight + ExLegendRowExtra;
        var gap = ExEntryGap * s;
        var widths = entries.Select(e => (ExKey + ExKeyGap) * s + Width(e.Text, style)).ToList();
        var position = ChartModel.Val(element, "legendPos") ?? "r";
        var layout = new LegendLayout
        {
            Position = position,
            Style = style,
            Entries = entries,
            Box = new Box(0, 0, 0, 0),
            Overlay = ChartModel.Flag(element, "overlay", false),
            SpPr = element.Element(Ns.C + "spPr"),
            Ex = true,
        };
        var titleLine = title is { Overlay: false } ? title.Bottom - TitlePaddingY + ExLegendGap : ExMargin;
        Box box;
        if (position is "t" or "b")
        {
            var rowWidth = widths.Sum() + gap * (entries.Count - 1);
            var columns = entries.Count;
            var columnWidth = 0.0;
            if (rowWidth > width * ExLegendShare && entries.Count > 1)
            {
                columnWidth = widths.Max() + gap;
                var fit = Math.Max(1, (int)Math.Floor((width * ExLegendShare + gap) / columnWidth));
                var rowCount = (int)Math.Ceiling(entries.Count / (double)fit);
                columns = (int)Math.Ceiling(entries.Count / (double)rowCount);
                rowWidth = columns * columnWidth - gap;
            }
            var rows = (int)Math.Ceiling(entries.Count / (double)columns);
            for (var i = 0; i < entries.Count; i++)
            {
                var (row, col) = Math.DivRem(i, columns);
                var x = columnWidth > 0 ? col * columnWidth : Enumerable.Range(row * columns, col).Sum(j => widths[j] + gap);
                layout.Placed.Add((entries[i], x, pitch * (row + 0.5)));
            }
            var h = rows * pitch;
            var y = position == "b" ? height - ExMargin - h : titleLine - ExLegendRowExtra / 2;
            box = new Box((width - rowWidth) / 2, y, rowWidth, h);
        }
        else
        {
            var w = widths.Max();
            var h = pitch * entries.Count;
            for (var i = 0; i < entries.Count; i++)
                layout.Placed.Add((entries[i], 0, pitch * (i + 0.5)));
            var areaTop = titleLine - ExLegendRowExtra / 2;
            var x = position == "l" ? ExMargin : width - ExMargin - w;
            box = new Box(x, areaTop + Math.Max(0, (height - ExMargin - areaTop - h) / 2), w, h);
        }
        layout.Box = ManualPosition(element, box.W, box.H) ?? box;
        return layout;
    }

    /// <summary>An entry of an Office 2016 chart's legend: its square key 0.66 s below the top of its line, then its text.</summary>
    string ExLegendEntry(LegendEntry entry, TextStyle style, double left, double rowCenter)
    {
        var s = style.Size;
        var lineTop = rowCenter - LineHeight(style) / 2;
        var key = ExKey * s;
        var outline = entry.Outline is { Visible: true } o ? o with { WidthEmu = Math.Min(o.WidthEmu, 19050) } : null;
        return Rect(new Box(left, lineTop + 0.66 * s - key / 2, key, key), entry.Fill, outline, snap: false)
               + text.Label(entry.Text, style, left + (ExKey + ExKeyGap) * s, lineTop + Ascent(style));
    }

    static XElement C(string name, params object?[] content) => new(Ns.C + name, content);
    static XElement V(string name, object value) => new(Ns.C + name, new XAttribute("val", value));
    static readonly XNamespace Cx = ChartExModel.Cx;

    static XElement Invisible() => new(Ns.C + "spPr", new XElement(Ns.A + "noFill"), new XElement(Ns.A + "ln", new XElement(Ns.A + "noFill")));

    /// <summary>The language of the presentation's text (its default text style), for the words Office puts in charts.</summary>
    string PresentationLanguage() =>
        slide.Presentation.DefaultTextStyle?.Descendants(Ns.A + "defRPr").Select(d => d.Str("lang")).FirstOrDefault(l => l != null) ?? "en-US";

    /// <summary>
    /// An element's own shape properties (cx:spPr) as c:spPr, else the style part's for it (cs:categoryAxis …), else
    /// none drawn: chartex parts leave their formatting to the style part.
    /// </summary>
    XElement Shape(XElement? owner, string styleElement) =>
        owner?.Element(Cx + "spPr") is { } own ? new XElement(Ns.C + "spPr", own.Elements())
        : chartStyle?.Element(Ns.Cs + styleElement)?.Element(Ns.Cs + "spPr") is { } styled ? new XElement(Ns.C + "spPr", styled.Elements())
        : Invisible();

    /// <summary>The text properties of an Office 2016 chart's title: its own, regular where they do not say (classic titles are bold).</summary>
    static XElement RegularText(XElement? txPr)
    {
        var copy = new XElement(Ns.C + "txPr", txPr?.Elements());
        if (copy.Element(Ns.A + "p") == null)
            copy.Add(new XElement(Ns.A + "bodyPr"), new XElement(Ns.A + "p"));
        var paragraph = copy.Element(Ns.A + "p")!;
        var pPr = paragraph.Element(Ns.A + "pPr");
        if (pPr == null)
            paragraph.AddFirst(pPr = new XElement(Ns.A + "pPr"));
        var defRPr = pPr.Element(Ns.A + "defRPr");
        if (defRPr == null)
            pPr.Add(defRPr = new XElement(Ns.A + "defRPr"));
        if (defRPr.Attribute("b") == null)
            defRPr.SetAttributeValue("b", "0");
        return copy;
    }

    /// <summary>A classic series of strings and numbers.</summary>
    static XElement Series(int index, string name, IReadOnlyList<string> categories, IReadOnlyList<double?> values, string format, params object?[] extra)
    {
        var count = Math.Max(categories.Count, values.Count);
        return C("ser", V("idx", index), V("order", index),
            C("tx", C("strRef", C("strCache", V("ptCount", 1), C("pt", new XAttribute("idx", 0), C("v", name))))),
            extra,
            C("cat", C("strRef", C("strCache", V("ptCount", count),
                categories.Select((c, i) => C("pt", new XAttribute("idx", i), C("v", c)))))),
            C("val", C("numRef", C("numCache", C("formatCode", format), V("ptCount", count),
                values.Select((v, i) => (v, i)).Where(p => p.v != null)
                    .Select(p => C("pt", new XAttribute("idx", p.i), C("v", p.v!.Value.ToString("R", CultureInfo.InvariantCulture))))))));
    }

    /// <summary>The classic counterpart of a chartex axis: deleted when hidden, its scaling, gridlines, labels, format and text.</summary>
    XElement Axis(string kind, int id, int cross, string position, XElement? exAxis, bool deleted = false, bool reversed = false,
        string? crossBetween = null, string crosses = "autoZero")
    {
        var scaling = exAxis?.Element(Cx + "valScaling");
        var content = new List<object?>
        {
            V("axId", id),
            C("scaling", V("orientation", reversed ? "maxMin" : "minMax"),
                double.TryParse((string?)scaling?.Attribute("max"), NumberStyles.Float, CultureInfo.InvariantCulture, out var max) ? V("max", max) : null,
                double.TryParse((string?)scaling?.Attribute("min"), NumberStyles.Float, CultureInfo.InvariantCulture, out var min) ? V("min", min) : null),
            V("delete", deleted || (string?)exAxis?.Attribute("hidden") is "1" or "true" ? 1 : 0),
            V("axPos", position),
        };
        if (exAxis?.Element(Cx + "majorGridlines") is { } major)
            content.Add(C("majorGridlines", Shape(major, "gridlineMajor")));
        if (exAxis?.Element(Cx + "minorGridlines") is { } minor)
            content.Add(C("minorGridlines", Shape(minor, "gridlineMinor")));
        if (exAxis?.Element(Cx + "numFmt") is { } numFmt)
            content.Add(new XElement(Ns.C + "numFmt", new XAttribute("formatCode", (string?)numFmt.Attribute("formatCode") ?? "General"),
                new XAttribute("sourceLinked", (string?)numFmt.Attribute("sourceLinked") is "1" or "true" ? 1 : 0)));
        else
            content.Add(new XElement(Ns.C + "numFmt", new XAttribute("formatCode", "General"), new XAttribute("sourceLinked", 1)));
        content.Add(V("majorTickMark", (string?)exAxis?.Element(Cx + "majorTickMarks")?.Attribute("type") ?? "none"));
        content.Add(V("minorTickMark", (string?)exAxis?.Element(Cx + "minorTickMarks")?.Attribute("type") ?? "none"));
        content.Add(V("tickLblPos", exAxis?.Element(Cx + "tickLabels") != null ? "nextTo" : "none"));
        // Value axes draw a line too, the category axis's where the style part gives them none (measured: D9D9D9
        // along the plot area's left edge).
        var styleElement = kind == "valAx" && chartStyle?.Element(Ns.Cs + "valueAxis")?.Element(Ns.Cs + "spPr") != null ? "valueAxis" : "categoryAxis";
        content.Add(Shape(exAxis, styleElement));
        if (exAxis?.Element(Cx + "txPr") is { } txPr)
            content.Add(new XElement(Ns.C + "txPr", txPr.Elements()));
        content.Add(V("crossAx", cross));
        content.Add(V("crosses", crosses));
        if (kind == "valAx")
        {
            content.Add(V("crossBetween", crossBetween ?? "between"));
            if (double.TryParse((string?)scaling?.Attribute("majorUnit"), NumberStyles.Float, CultureInfo.InvariantCulture, out var unit))
                content.Add(V("majorUnit", unit));
        }
        else
        {
            content.Add(V("auto", 1));
            content.Add(V("lblAlgn", "ctr"));
            content.Add(V("lblOffset", 100));
        }
        return new XElement(Ns.C + kind, content);
    }

    /// <summary>
    /// The classic chart a chartex chart is framed by: its title (Office's "Chart Title" where it has no text), legend
    /// and axes, with the series the frame needs for its legend entries and value range; the chartex plot itself is
    /// set up in <see cref="exAxisPlot"/> or <see cref="exPlot"/>.
    /// </summary>
    XDocument ClassicFrame(ChartExModel chart)
    {
        var language = PresentationLanguage();
        var german = language.StartsWith("de", StringComparison.OrdinalIgnoreCase);
        culture = slide.Presentation.Culture;
        chartStyle = Related("chartStyle");
        var plotArea = new XElement(Ns.C + "plotArea", C("layout"));
        var deletedEntries = new List<int>();
        var data = chart.Series.Count > 0 && chart.Data.TryGetValue(chart.Series[0].DataId, out var first) ? first : new ExData();

        switch (chart.Layout)
        {
            case "waterfall":
                FrameWaterfall(chart, data, plotArea, deletedEntries, german);
                break;
            case "funnel":
                FrameFunnel(chart, data, plotArea);
                break;
            case "clusteredColumn":
                FrameHistogram(chart, data, plotArea, german);
                break;
            case "boxWhisker":
                FrameBoxWhisker(chart, plotArea, deletedEntries);
                break;
            case "treemap" or "sunburst":
                FrameHierarchy(chart, data, plotArea);
                break;
            case "regionMap":
                FrameRegionMap(chart, data);
                break;
            default:
                // Unknown layouts: title and legend only.
                break;
        }

        var space = new XElement(Ns.C + "chartSpace", new XAttribute(XNamespace.Xmlns + "c", Ns.C.NamespaceName), new XAttribute(XNamespace.Xmlns + "a", Ns.A.NamespaceName),
            V("lang", language), V("roundedCorners", 0));
        var classic = C("chart");
        if (chart.Title is { } title)
        {
            // Without text of its own the title reads "Chart Title" in the presentation's language.
            var text = title.Element(Cx + "tx")?.Element(Cx + "txData")?.Element(Cx + "v")?.Value;
            var rich = title.Element(Cx + "tx")?.Element(Cx + "rich");
            classic.Add(C("title",
                rich != null ? C("tx", new XElement(Ns.C + "rich", rich.Elements()))
                : C("tx", C("rich", new XElement(Ns.A + "bodyPr"), new XElement(Ns.A + "p", new XElement(Ns.A + "r",
                    new XElement(Ns.A + "t", text ?? (german ? "Diagrammtitel" : "Chart Title")))))),
                V("overlay", (string?)title.Attribute("overlay") is "1" or "true" ? 1 : 0),
                title.Element(Cx + "spPr") is { } titleSpPr ? new XElement(Ns.C + "spPr", titleSpPr.Elements()) : null,
                RegularText(title.Element(Cx + "txPr"))));
            classic.Add(V("autoTitleDeleted", 0));
        }
        else
            classic.Add(V("autoTitleDeleted", 1));
        classic.Add(plotArea);
        // Region maps draw their color scale themselves (see RegionMap).
        if (chart.Legend is { } legend && chart.Layout != "regionMap")
        {
            classic.Add(C("legend", V("legendPos", (string?)legend.Attribute("pos") ?? "r"),
                deletedEntries.Select(i => C("legendEntry", V("idx", i), V("delete", 1))),
                V("overlay", (string?)legend.Attribute("overlay") is "1" or "true" ? 1 : 0),
                legend.Element(Cx + "spPr") is { } legendSpPr ? new XElement(Ns.C + "spPr", legendSpPr.Elements()) : null,
                legend.Element(Cx + "txPr") is { } legendTxPr ? new XElement(Ns.C + "txPr", legendTxPr.Elements()) : null));
        }
        classic.Add(V("plotVisOnly", 1), V("dispBlanksAs", "gap"));
        space.Add(classic);
        // The chart area has neither fill nor line of the style part's (measured: no line around PowerPoint's).
        space.Add(chart.Space.Element(Cx + "spPr") is { } spaceSpPr ? new XElement(Ns.C + "spPr", spaceSpPr.Elements()) : Invisible());
        if (chart.Space.Element(Cx + "txPr") is { } spaceTxPr)
            space.Add(new XElement(Ns.C + "txPr", spaceTxPr.Elements()));
        return new XDocument(space);
    }

    // ---------------------------------------------------------------- waterfall

    /// <summary>
    /// A waterfall: every point a bar from the running total, rising (increase) or falling (decrease), the points
    /// listed as subtotals bars from zero to their value (total), connected by lines at the running total; the legend
    /// names the three kinds in the presentation's language (measured: German Office "Anstieg", "Abfall", "Summe").
    /// </summary>
    void FrameWaterfall(ChartExModel chart, ExData data, XElement plotArea, List<int> deletedEntries, bool german)
    {
        var series = chart.Series[0];
        var subtotals = series.LayoutProperties?.Element(Cx + "subtotals")?.Elements(Cx + "idx").Select(e => (int?)e.Attribute("val") ?? -1).ToHashSet() ?? [];
        var values = data.Values;
        var n = data.Count;
        var categories = Enumerable.Range(0, n).Select(data.Category).ToList();
        var bars = new List<(int Index, double From, double To, int Kind)>();
        var running = 0.0;
        for (var i = 0; i < n; i++)
        {
            if (values.ElementAtOrDefault(i) is not { } v)
                continue;
            if (subtotals.Contains(i))
            {
                bars.Add((i, 0, v, 2));
                running = v;
            }
            else
            {
                bars.Add((i, running, running + v, v >= 0 ? 0 : 1));
                running += v;
            }
        }
        var names = german ? new[] { "Anstieg", "Abfall", "Summe" } : new[] { "Increase", "Decrease", "Total" };
        var gap = double.TryParse((string?)chart.Axis(0)?.Element(Cx + "catScaling")?.Attribute("gapWidth"), NumberStyles.Float, CultureInfo.InvariantCulture, out var g) ? g : 0.5;
        // The legend's three kinds as empty bars, the value range as invisible lines through the bars' ends.
        var empty = Enumerable.Repeat<double?>(null, n).ToList();
        plotArea.Add(C("barChart", V("barDir", "col"), V("grouping", "clustered"), V("varyColors", 0),
            names.Select((name, k) => Series(k, name, categories, empty, data.Format)),
            V("gapWidth", (int)Math.Round(gap * 100)), V("axId", 1), V("axId", 2)));
        var low = Enumerable.Range(0, n).Select(i => bars.Where(b => b.Index == i).Select(b => (double?)Math.Min(b.From, b.To)).FirstOrDefault()).ToList();
        var high = Enumerable.Range(0, n).Select(i => bars.Where(b => b.Index == i).Select(b => (double?)Math.Max(b.From, b.To)).FirstOrDefault()).ToList();
        plotArea.Add(C("lineChart", V("grouping", "standard"), V("varyColors", 0),
            Series(3, "", categories, low, data.Format, Invisible(), C("marker", V("symbol", "none"))),
            Series(4, "", categories, high, data.Format, Invisible(), C("marker", V("symbol", "none"))),
            V("marker", 0), V("axId", 1), V("axId", 2)));
        deletedEntries.AddRange([3, 4]);
        plotArea.Add(Axis("catAx", 1, 2, "b", chart.Axis(0)));
        plotArea.Add(Axis("valAx", 2, 1, "l", chart.Axis(1)));

        exAxisPlot = (axes, plot) =>
        {
            var category = axes.First(a => !a.Axis.IsValue);
            var value = axes.First(a => a.Axis.IsValue);
            var group = model.Groups.First(gr => gr.IsBar);
            var svg = new StringBuilder();
            var width = Math.Abs(category.Boundary(1) - category.Boundary(0));
            var barWidth = width / (1 + gap);
            var labels = series.Element.Element(Cx + "dataLabels");
            var showValues = labels != null && (string?)labels.Element(Cx + "visibility")?.Attribute("value") is not ("0" or "false");
            var labelStyle = Style(labels?.Element(Cx + "txPr") is { } lt ? new XElement(Ns.C + "txPr", lt.Elements()) : null, "dataLabel");
            // Connector lines at each bar's end over to the next bar: the style part's series lines (measured: D9D9D9).
            var connector = chartStyle?.Element(Ns.Cs + "seriesLine")?.Element(Ns.Cs + "spPr")?.Element(Ns.A + "ln") is { } seriesLine
                ? OutlineReader.Merge([(seriesLine, colors)])
                : new Outline(9525, new SolidFill(RgbColor.FromHex("D9D9D9")), "solid", "flat", "round", 8, "sng");
            var connectors = (string?)series.LayoutProperties?.Element(Cx + "visibility")?.Attribute("connectorLines") is not ("0" or "false");
            for (var k = 0; k < bars.Count; k++)
            {
                var (index, from, to, kind) = bars[k];
                var center = category.Center(index);
                var (y0, y1) = (value.Map(from), value.Map(to));
                var box = new Box(center - barWidth / 2, Math.Min(y0, y1), barWidth, Math.Abs(y1 - y0));
                svg.Append(Rect(box, PointFill(group, group.Series[kind], -1), PointOutline(group.Series[kind], -1), snap: true));
                if (connectors && k + 1 < bars.Count)
                {
                    var next = bars[k + 1];
                    var level = value.Map(to);
                    svg.Append(GridLine(connector, center + barWidth / 2, level, category.Center(next.Index) - barWidth / 2, level));
                }
                if (showValues && values.ElementAtOrDefault(index) is { } raw)
                {
                    var label = NumberFormat.Format(raw, data.Format, culture);
                    var (w, h) = (Width(label, labelStyle), LineHeight(labelStyle));
                    // Above rising bars and totals, below falling ones, their lines touching the bar (measured).
                    var y = kind == 1 ? Math.Max(y0, y1) : Math.Min(y0, y1) - h;
                    svg.Append(text.Label(label, labelStyle, center - w / 2, y + Ascent(labelStyle)));
                }
            }
            return svg.ToString();
        };
    }

    // ---------------------------------------------------------------- funnel

    /// <summary>
    /// A funnel: the categories from the top down, each a bar centered across the plot area, as long against the
    /// largest value as its value, the value inside; category labels left of the plot area.
    /// </summary>
    void FrameFunnel(ChartExModel chart, ExData data, XElement plotArea)
    {
        var series = chart.Series[0];
        var n = data.Count;
        var categories = Enumerable.Range(0, n).Select(data.Category).ToList();
        var values = data.Values;
        var gap = double.TryParse((string?)chart.Axis(1)?.Element(Cx + "catScaling")?.Attribute("gapWidth") ?? (string?)chart.Axis(0)?.Element(Cx + "catScaling")?.Attribute("gapWidth"),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var g) ? g : 0.06;
        plotArea.Add(C("barChart", V("barDir", "bar"), V("grouping", "clustered"), V("varyColors", 0),
            Series(0, series.Name ?? "", categories, Enumerable.Repeat<double?>(null, n).ToList(), data.Format),
            V("gapWidth", (int)Math.Round(gap * 100)), V("axId", 1), V("axId", 2)));
        // The category axis carries the funnel's names (axis id 1 in chartex), the value axis is not shown.
        var categoryAxis = chart.Axis(1) ?? chart.Axis(0);
        plotArea.Add(Axis("catAx", 1, 2, "l", categoryAxis, reversed: true));
        plotArea.Add(Axis("valAx", 2, 1, "b", null, deleted: true));

        exAxisPlot = (axes, plot) =>
        {
            var category = axes.First(a => !a.Axis.IsValue);
            var group = model.Groups.First(gr => gr.IsBar);
            var svg = new StringBuilder();
            var largest = values.Where(v => v != null).Select(v => Math.Abs(v!.Value)).DefaultIfEmpty(0).Max();
            var slot = Math.Abs(category.Boundary(1) - category.Boundary(0));
            var thickness = slot / (1 + gap);
            var labels = series.Element.Element(Cx + "dataLabels");
            var showValues = labels != null && (string?)labels.Element(Cx + "visibility")?.Attribute("value") is not ("0" or "false");
            var labelStyle = Style(labels?.Element(Cx + "txPr") is { } lt ? new XElement(Ns.C + "txPr", lt.Elements()) : null, "dataLabel");
            var middle = plot.X + plot.W / 2;
            for (var i = 0; i < n; i++)
            {
                if (values.ElementAtOrDefault(i) is not { } v || largest <= 0)
                    continue;
                var length = Math.Abs(v) / largest * plot.W;
                var center = category.Center(i);
                var fill = PointFill(group, group.Series[0], i);
                svg.Append(Rect(new Box(middle - length / 2, center - thickness / 2, length, thickness), fill, PointOutline(group.Series[0], i), snap: true));
                // Values inside their bars, in white on dark fills (measured: white on the theme's blue), left out
                // where the bar is shorter than the value.
                var label = NumberFormat.Format(v, data.Format, culture);
                var labelWidth = Width(label, labelStyle);
                if (showValues && labelWidth <= length)
                    svg.Append(text.Label(label, Contrasting(labelStyle, fill), middle - labelWidth / 2, ExCenteredBaseline(labelStyle, center)));
            }
            return svg.ToString();
        };
    }

    // ---------------------------------------------------------------- histograms and Pareto charts

    /// <summary>
    /// A histogram (clusteredColumn with binning) or a column chart of categories, with the Pareto line of a
    /// paretoLine series (the running share of the sorted bars on a second axis from 0 to 100 %). Numbers bin into
    /// intervals of c:binSize, or c:binCount ones, else of the width of Scott's normal reference rule, from the
    /// smallest value (or the underflow bin's limit) up, closed on the right (intervalClosed r) or left; their
    /// labels read [a, b], (a, b] or ≤ a and &gt; b for the underflow and overflow bins.
    /// </summary>
    void FrameHistogram(ChartExModel chart, ExData data, XElement plotArea, bool german)
    {
        var series = chart.Series.First(s => s.Layout == "clusteredColumn");
        var binning = series.LayoutProperties?.Element(Cx + "binning");
        var aggregation = series.LayoutProperties?.Element(Cx + "aggregation") != null;
        List<string> categories;
        List<double?> counts;
        var format = data.Format;
        if (binning != null)
            (categories, counts) = Bins(data.Values.Where(v => v != null).Select(v => v!.Value).ToList(), binning, data.Format);
        else if (aggregation || data.Levels.Count > 0)
        {
            // Categories with their values summed (aggregation) or as they are.
            var sums = new List<(string Name, double Sum)>();
            for (var i = 0; i < data.Count; i++)
            {
                var name = data.Category(i);
                var v = data.Values.ElementAtOrDefault(i) ?? 0;
                var existing = sums.FindIndex(s => s.Name == name);
                if (aggregation && existing >= 0)
                    sums[existing] = (name, sums[existing].Sum + v);
                else
                    sums.Add((name, v));
            }
            categories = sums.Select(s => s.Name).ToList();
            counts = sums.Select(s => (double?)s.Sum).ToList();
        }
        else
        {
            categories = Enumerable.Range(0, data.Count).Select(data.Category).ToList();
            counts = data.Values.ToList();
            format = data.Format;
        }
        var pareto = chart.Series.FirstOrDefault(s => s.Layout == "paretoLine");
        if (pareto != null)
        {
            // Pareto charts sort their bars, the largest first.
            var order = Enumerable.Range(0, categories.Count).OrderByDescending(i => counts[i] ?? double.MinValue).ToList();
            categories = order.Select(i => categories[i]).ToList();
            counts = order.Select(i => counts[i]).ToList();
        }
        var gap = double.TryParse((string?)chart.Axis(0)?.Element(Cx + "catScaling")?.Attribute("gapWidth"), NumberStyles.Float, CultureInfo.InvariantCulture, out var g) ? g : 0;
        var labels = series.Element.Element(Cx + "dataLabels");
        XElement? dLbls = null;
        if (labels != null)
        {
            var visibility = labels.Element(Cx + "visibility");
            dLbls = C("dLbls",
                labels.Element(Cx + "spPr") is { } lsp ? new XElement(Ns.C + "spPr", lsp.Elements()) : null,
                labels.Element(Cx + "txPr") is { } ltx ? new XElement(Ns.C + "txPr", ltx.Elements()) : null,
                V("dLblPos", (string?)labels.Attribute("pos") ?? "outEnd"),
                V("showLegendKey", 0), V("showVal", (string?)visibility?.Attribute("value") is "1" or "true" ? 1 : 0),
                V("showCatName", (string?)visibility?.Attribute("categoryName") is "1" or "true" ? 1 : 0),
                V("showSerName", (string?)visibility?.Attribute("seriesName") is "1" or "true" ? 1 : 0),
                V("showPercent", 0), V("showBubbleSize", 0));
        }
        plotArea.Add(C("barChart", V("barDir", "col"), V("grouping", "clustered"), V("varyColors", 0),
            Series(0, series.Name ?? (german ? "Reihe 1" : "Series 1"), categories, counts, binning != null ? "General" : format,
                // Bars outlined in the background's color (measured: a white pixel between bins).
                series.Element.Element(Cx + "spPr") is { } sp ? new XElement(Ns.C + "spPr", sp.Elements())
                    : new XElement(Ns.C + "spPr", new XElement(Ns.A + "ln", new XAttribute("w", 9525),
                        new XElement(Ns.A + "solidFill", new XElement(Ns.A + "schemeClr", new XAttribute("val", "bg1"))))), dLbls),
            V("gapWidth", (int)Math.Round(gap * 100)), V("axId", 1), V("axId", 2)));
        plotArea.Add(Axis("catAx", 1, 2, "b", chart.Axis(0)));
        plotArea.Add(Axis("valAx", 2, 1, "l", chart.Axis(1)));
        if (pareto != null)
        {
            var total = counts.Sum(c => Math.Abs(c ?? 0));
            var running = 0.0;
            var shares = counts.Select(c => (double?)((running += Math.Abs(c ?? 0)) / (total == 0 ? 1 : total))).ToList();
            plotArea.Add(C("lineChart", V("grouping", "standard"), V("varyColors", 0),
                Series(1, pareto.Name ?? (german ? "Reihe 2" : "Series 2"), categories, shares, "0%",
                    pareto.Element.Element(Cx + "spPr") is { } psp ? new XElement(Ns.C + "spPr", psp.Elements()) : null, C("marker", V("symbol", "none"))),
                V("marker", 0), V("axId", 3), V("axId", 4)));
            plotArea.Add(Axis("catAx", 3, 4, "b", null, deleted: true));
            var percent = Axis("valAx", 4, 3, "r", chart.Axis(2), crosses: "max");
            percent.Element(Ns.C + "scaling")!.Add(V("max", 1), V("min", 0));
            percent.Element(Ns.C + "numFmt")?.SetAttributeValue("formatCode", "0%");
            percent.Element(Ns.C + "numFmt")?.SetAttributeValue("sourceLinked", 0);
            plotArea.Add(percent);
        }
    }

    /// <summary>Histogram bins of numbers: their labels and counts.</summary>
    (List<string> Labels, List<double?> Counts) Bins(List<double> values, XElement binning, string format)
    {
        if (values.Count == 0)
            return ([], []);
        var closedRight = (string?)binning.Attribute("intervalClosed") != "l";
        double? Limit(string name) => (string?)binning.Attribute(name) is { } s && s != "auto" && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
        var underflow = Limit("underflow");
        var overflow = Limit("overflow");
        var inner = values.Where(v => (underflow == null || (closedRight ? v > underflow : v >= underflow)) && (overflow == null || (closedRight ? v <= overflow : v < overflow))).ToList();
        var min = underflow ?? (inner.Count > 0 ? inner.Min() : values.Min());
        var max = overflow ?? (inner.Count > 0 ? inner.Max() : values.Max());
        double width;
        if (double.TryParse((string?)binning.Element(Cx + "binSize")?.Attribute("val"), NumberStyles.Float, CultureInfo.InvariantCulture, out var size) && size > 0)
            width = size;
        else if (int.TryParse((string?)binning.Element(Cx + "binCount")?.Attribute("val"), out var count) && count > 0)
            width = (max - min) / count;
        else
        {
            // Scott's normal reference rule: 3.5 sample standard deviations over the cube root of the count, to two
            // significant digits (measured: 4.047 gives bins 4 wide, 3.3214 3.3, 2204.8 2200, 0.27623 0.28, 17.505 18).
            var mean = values.Average();
            var deviation = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / Math.Max(1, values.Count - 1));
            width = 3.5 * deviation / Math.Cbrt(values.Count);
            if (width > 0)
            {
                var magnitude = Math.Pow(10, Math.Floor(Math.Log10(width)) - 1);
                width = Math.Round(width / magnitude, MidpointRounding.AwayFromZero) * magnitude;
            }
        }
        if (width <= 0 || double.IsNaN(width))
            width = Math.Max(1, max - min);
        var binCount = Math.Max(1, (int)Math.Ceiling((max - min) / width - 1e-9));
        string F(double v) => NumberFormat.Format(v, format, culture);
        var labels = new List<string>();
        var counts = new List<double?>();
        if (underflow != null)
        {
            labels.Add((closedRight ? "≤" : "<") + F(underflow.Value));
            counts.Add(values.Count(v => closedRight ? v <= underflow : v < underflow));
        }
        for (var b = 0; b < binCount; b++)
        {
            var (lo, hi) = (AxisScale.Clean(min + b * width), AxisScale.Clean(min + (b + 1) * width));
            var first = b == 0 && underflow == null;
            var last = b == binCount - 1 && overflow == null;
            var open = closedRight ? (first ? "[" : "(") : "[";
            var close = closedRight ? "]" : (last ? "]" : ")");
            labels.Add($"{open}{F(lo)}, {F(hi)}{close}");
            counts.Add(inner.Count(v => closedRight
                ? (first ? v >= lo : v > lo) && (v <= hi || last && v <= max)
                : v >= lo && (v < hi || last && v <= hi)));
        }
        if (overflow != null)
        {
            labels.Add((closedRight ? ">" : "≥") + F(overflow.Value));
            counts.Add(values.Count(v => closedRight ? v > overflow : v >= overflow));
        }
        return (labels, counts);
    }

    // ---------------------------------------------------------------- box and whisker

    /// <summary>
    /// Box and whisker charts: per category (points sharing a name) and series a box from the first to the third
    /// quartile (exclusive or inclusive method) with the median across, whiskers to the extreme values within 1.5
    /// interquartile ranges, outliers as dots, the mean as a cross (and mean lines, inner points where shown), the box
    /// in the series' color and its lines that color at 80 % (measured).
    /// </summary>
    void FrameBoxWhisker(ChartExModel chart, XElement plotArea, List<int> deletedEntries)
    {
        var names = new List<string>();
        foreach (var s in chart.Series)
        {
            if (!chart.Data.TryGetValue(s.DataId, out var d))
                continue;
            for (var i = 0; i < d.Count; i++)
            {
                var name = d.Category(i);
                if (!names.Contains(name))
                    names.Add(name);
            }
        }
        var groups = chart.Series.Select(s => chart.Data.TryGetValue(s.DataId, out var d)
            ? names.Select(name => Enumerable.Range(0, d.Count).Where(i => d.Category(i) == name && d.Values.ElementAtOrDefault(i) != null)
                .Select(i => d.Values[i]!.Value).OrderBy(v => v).ToList()).ToList()
            : names.Select(_ => new List<double>()).ToList()).ToList();
        var empty = Enumerable.Repeat<double?>(null, names.Count).ToList();
        var format = chart.Series.Select(s => chart.Data.TryGetValue(s.DataId, out var d) ? d.Format : null).FirstOrDefault(f => f != null) ?? "General";
        var gap = double.TryParse((string?)chart.Axis(0)?.Element(Cx + "catScaling")?.Attribute("gapWidth"), NumberStyles.Float, CultureInfo.InvariantCulture, out var g) ? g : 1;
        plotArea.Add(C("barChart", V("barDir", "col"), V("grouping", "clustered"), V("varyColors", 0),
            chart.Series.Select((s, k) => Series(k, s.Name ?? $"{k + 1}", names, empty, format)),
            V("gapWidth", (int)Math.Round(gap * 100)), V("axId", 1), V("axId", 2)));
        var all = groups.SelectMany(gr => gr.SelectMany(v => v)).ToList();
        var low = names.Select((_, i) => groups.SelectMany(gr => gr[i]).Select(v => (double?)v).DefaultIfEmpty(null).Min()).ToList();
        var high = names.Select((_, i) => groups.SelectMany(gr => gr[i]).Select(v => (double?)v).DefaultIfEmpty(null).Max()).ToList();
        var count = chart.Series.Count;
        plotArea.Add(C("lineChart", V("grouping", "standard"), V("varyColors", 0),
            Series(count, "", names, low, format, Invisible(), C("marker", V("symbol", "none"))),
            Series(count + 1, "", names, high, format, Invisible(), C("marker", V("symbol", "none"))),
            V("marker", 0), V("axId", 1), V("axId", 2)));
        deletedEntries.AddRange([count, count + 1]);
        plotArea.Add(Axis("catAx", 1, 2, "b", chart.Axis(0)));
        plotArea.Add(Axis("valAx", 2, 1, "l", chart.Axis(1)));

        exAxisPlot = (axes, plot) =>
        {
            var category = axes.First(a => !a.Axis.IsValue);
            var value = axes.First(a => a.Axis.IsValue);
            var group = model.Groups.First(gr => gr.IsBar);
            var svg = new StringBuilder();
            var slot = Math.Abs(category.Boundary(1) - category.Boundary(0)) / (count + gap);
            for (var k = 0; k < count; k++)
            {
                var layout = chart.Series[k].LayoutProperties;
                var visibility = layout?.Element(Cx + "visibility");
                bool Shown(string name, bool fallback) => (string?)visibility?.Attribute(name) is { } v ? v is "1" or "true" : fallback;
                var inclusive = (string?)layout?.Element(Cx + "statistics")?.Attribute("quartileMethod") == "inclusive";
                var fill = PointFill(group, group.Series[k], -1);
                var color = Fill.MainColor(fill) ?? RgbColor.Black;
                var dark = new RgbColor(color.R * 0.8, color.G * 0.8, color.B * 0.8, color.A);
                var line = new Outline(9525, new SolidFill(dark), "solid", "flat", "miter", 8, "sng");
                var means = new List<(double X, double Y)?>();
                for (var i = 0; i < names.Count; i++)
                {
                    var values = groups[k][i];
                    if (values.Count == 0)
                    {
                        means.Add(null);
                        continue;
                    }
                    var q1 = Quartile(values, 0.25, inclusive);
                    var median = Quartile(values, 0.5, inclusive);
                    var q3 = Quartile(values, 0.75, inclusive);
                    var iqr = q3 - q1;
                    var inside = values.Where(v => v >= q1 - 1.5 * iqr && v <= q3 + 1.5 * iqr).ToList();
                    var (lowWhisker, highWhisker) = inside.Count > 0 ? (inside.Min(), inside.Max()) : (q1, q3);
                    var left = category.Boundary(i) + (category.Boundary(i + 1) - category.Boundary(i)) * gap / (count + gap) / 2 + slot * k;
                    var center = left + slot / 2;
                    var (y1, y3) = (value.Map(q1), value.Map(q3));
                    var capHalf = slot / 8;
                    // Whiskers with their caps, then the box over them, the median across it.
                    svg.Append(GridLine(line, center, value.Map(highWhisker), center, Math.Min(y1, y3)));
                    svg.Append(GridLine(line, center, Math.Max(y1, y3), center, value.Map(lowWhisker)));
                    svg.Append(GridLine(line, center - capHalf, value.Map(highWhisker), center + capHalf, value.Map(highWhisker)));
                    svg.Append(GridLine(line, center - capHalf, value.Map(lowWhisker), center + capHalf, value.Map(lowWhisker)));
                    svg.Append(Rect(new Box(left, Math.Min(y1, y3), slot, Math.Abs(y3 - y1)), fill, line, snap: true));
                    svg.Append(GridLine(line, left, value.Map(median), left + slot, value.Map(median)));
                    var mean = values.Average();
                    means.Add((center, value.Map(mean)));
                    if (Shown("meanMarker", true))
                    {
                        var r = Math.Min(slot / 6, 4.5);
                        var (mx, my) = (center, value.Map(mean));
                        svg.Append($"<path d=\"M{Px(mx - r)} {Px(my - r)}L{Px(mx + r)} {Px(my + r)}M{Px(mx - r)} {Px(my + r)}L{Px(mx + r)} {Px(my - r)}\" fill=\"none\"{paint.Stroke(line, 0, 0, defs)}/>");
                    }
                    var dot = 2.6;
                    if (Shown("outliers", true))
                    {
                        foreach (var o in values.Where(v => v < q1 - 1.5 * iqr || v > q3 + 1.5 * iqr))
                            svg.Append($"<circle cx=\"{Px(center)}\" cy=\"{Px(value.Map(o))}\" r=\"{Px(dot / 2 + 0.5)}\"{paint.Fill(fill, 0, 0, defs)}/>");
                    }
                    if (Shown("nonoutliers", false))
                    {
                        foreach (var o in inside)
                            svg.Append($"<circle cx=\"{Px(center)}\" cy=\"{Px(value.Map(o))}\" r=\"{Px(dot / 2 + 0.5)}\" fill=\"none\"{paint.Stroke(line, 0, 0, defs)}/>");
                    }
                }
                if (Shown("meanLine", false))
                {
                    var points = means.Where(m => m != null).Select(m => m!.Value).ToList();
                    if (points.Count > 1)
                        svg.Append($"<path d=\"{string.Concat(points.Select((p, j) => (j == 0 ? "M" : "L") + Px(p.X) + " " + Px(p.Y)))}\" fill=\"none\"{paint.Stroke(line, 0, 0, defs)}/>");
                }
            }
            return svg.ToString();
        };
    }

    /// <summary>A quartile like Excel's QUARTILE.EXC (between n + 1 ranks) or QUARTILE.INC (between n − 1 ranks).</summary>
    static double Quartile(List<double> sorted, double q, bool inclusive)
    {
        var n = sorted.Count;
        if (n == 1)
            return sorted[0];
        var rank = inclusive ? (n - 1) * q : (n + 1) * q - 1;
        rank = Math.Clamp(rank, 0, n - 1);
        var lower = (int)Math.Floor(rank);
        var upper = Math.Min(n - 1, lower + 1);
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (rank - lower);
    }

    // ---------------------------------------------------------------- treemaps and sunbursts

    /// <summary>A node of a treemap's or sunburst's hierarchy.</summary>
    sealed class HierarchyNode
    {
        public required string Name { get; init; }
        public int Depth { get; init; }
        public int Point { get; set; } = -1;
        public double Size { get; set; }
        public int Branch { get; set; }
        public List<HierarchyNode> Children { get; } = [];
    }

    /// <summary>The hierarchy of the category levels (the top level first), sizes summed upwards; empty names skip a level.</summary>
    static HierarchyNode Hierarchy(ExData data)
    {
        var root = new HierarchyNode { Name = "", Depth = 0 };
        var values = data.Values;
        var levels = data.Levels.AsEnumerable().Reverse().ToList();
        for (var i = 0; i < data.Count; i++)
        {
            var node = root;
            var path = levels.Select(l => i < l.Count ? l[i] : null).Where(name => !string.IsNullOrEmpty(name)).ToList();
            for (var d = 0; d < path.Count; d++)
            {
                var child = node.Children.FirstOrDefault(c => c.Name == path[d] && c.Point < 0 && d < path.Count - 1);
                if (child == null || d == path.Count - 1)
                {
                    child = new HierarchyNode { Name = path[d]!, Depth = d + 1 };
                    node.Children.Add(child);
                }
                node = child;
            }
            node.Point = i;
            node.Size = Math.Max(0, values.ElementAtOrDefault(i) ?? 0);
        }
        double Sum(HierarchyNode n) => n.Children.Count == 0 ? n.Size : n.Size = n.Children.Sum(Sum);
        Sum(root);
        for (var b = 0; b < root.Children.Count; b++)
        {
            void Mark(HierarchyNode n) { n.Branch = b; n.Children.ForEach(Mark); }
            Mark(root.Children[b]);
        }
        return root;
    }

    /// <summary>A treemap or sunburst: no axes, a legend of the top-level branches (pie-style entries), the plot drawn here.</summary>
    void FrameHierarchy(ChartExModel chart, ExData data, XElement plotArea)
    {
        var root = Hierarchy(data);
        var branches = root.Children.Select(c => c.Name).ToList();
        plotArea.Add(C("pieChart", V("varyColors", 1),
            Series(0, chart.Series[0].Name ?? "", branches, branches.Select(_ => (double?)1).ToList(), "General"),
            V("firstSliceAng", 0)));
        var series = chart.Series[0];
        var labels = series.Element.Element(Cx + "dataLabels");
        var showNames = labels != null && (string?)labels.Element(Cx + "visibility")?.Attribute("categoryName") is not ("0" or "false");
        var sunburst = chart.Layout == "sunburst";
        exPlot = plot =>
        {
            var labelStyle = Style(labels?.Element(Cx + "txPr") is { } lt ? new XElement(Ns.C + "txPr", lt.Elements()) : null, "dataLabel");
            var group = model.Groups.First();
            Fill BranchFill(HierarchyNode node) => PointFill(group, group.Series[0], node.Branch);
            var border = chartStyle?.Element(Ns.Cs + "dataPoint")?.Element(Ns.Cs + "spPr")?.Element(Ns.A + "ln") is { } styleLine
                ? OutlineReader.Merge([(ResolvePlaceholder(styleLine), colors)])
                : new Outline(19050, new SolidFill(RgbColor.White), "solid", "flat", "miter", 8, "sng");
            return sunburst
                ? Sunburst(root, plot, BranchFill, border, showNames, labelStyle)
                : Treemap(root, plot, BranchFill, border, showNames, labelStyle, (string?)series.LayoutProperties?.Element(Cx + "parentLabelLayout")?.Attribute("val") ?? "overlapping");
        };
    }

    /// <summary>The style part's placeholder color (phClr) as white: the outlines of treemaps and sunbursts.</summary>
    static XElement ResolvePlaceholder(XElement line)
    {
        var copy = new XElement(line);
        foreach (var placeholder in copy.Descendants(Ns.A + "schemeClr").Where(c => c.Str("val") == "phClr"))
            placeholder.SetAttributeValue("val", "bg1");
        return copy;
    }

    /// <summary>
    /// A squarified treemap (Bruls, Huizing and van Wijk): the children of every node, the largest first, in rows
    /// along the shorter side of the space left; leaves filled by their top-level branch with their names at the
    /// bottom left, the branches' names at their top left (parentLabelLayout overlapping) or in a gray banner above
    /// their children (banner, a line and 3.6 pt insets high); names 3.6 pt inside, wrapped (words longer than a line
    /// broken between characters) and left out where they do not fit (measured).
    /// </summary>
    string Treemap(HierarchyNode root, Box plot, Func<HierarchyNode, Fill> fill, Outline border, bool names, TextStyle style, string parentLabels)
    {
        const double inset = 3.6;
        var svg = new StringBuilder();
        var labels = new StringBuilder();
        var lineHeight = LineHeight(style);
        void Layout(HierarchyNode node, Box box)
        {
            if (node.Children.Count == 0)
            {
                svg.Append(Rect(box, fill(node), border, snap: true));
                if (names)
                    Name(node.Name, box, bottom: true);
                return;
            }
            // Only the top-level parents are named (measured): over their first child, or in a banner above them.
            if (node.Depth == 1 && names && parentLabels != "none")
            {
                if (parentLabels == "banner")
                {
                    var banner = Math.Min(box.H, lineHeight + 2 * inset);
                    var gray = new SolidFill(colors.Resolve(new XElement(Ns.A + "schemeClr", new XAttribute("val", "bg1"), new XElement(Ns.A + "lumMod", new XAttribute("val", "65000")))));
                    svg.Append(Rect(box with { H = banner }, gray, border, snap: true));
                    Name(node.Name, box with { H = banner }, bottom: false);
                    box = box with { Y = box.Y + banner, H = Math.Max(0, box.H - banner) };
                }
                else
                {
                    Name(node.Name, box, bottom: false);
                }
            }
            var children = node.Children.Where(c => c.Size > 0).OrderByDescending(c => c.Size).ToList();
            foreach (var (child, childBox) in Squarify(children, box))
                Layout(child, childBox);
        }
        void Name(string name, Box box, bool bottom)
        {
            var lines = WrapCharacters(name, style, box.W - 2 * inset);
            if (lines.Count == 0 || lines.Count * lineHeight > box.H - 2 * inset + 0.5)
                return;
            var y = bottom ? box.Bottom - inset - lines.Count * lineHeight : box.Y + inset;
            foreach (var line in lines)
            {
                labels.Append(text.Label(line, style, box.X + inset, y + Ascent(style)));
                y += lineHeight;
            }
        }
        Layout(root, plot);
        return svg.Append(labels).ToString();
    }

    /// <summary>
    /// A label's words in lines at most <paramref name="max"/> wide; a word longer than a line is broken between
    /// characters (measured: "Verzweigung 1" in a 34.5 pt ring reads "Verzw", "eigung", "1"); none where not even a
    /// character fits.
    /// </summary>
    static List<string> WrapCharacters(string label, TextStyle style, double max)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in label.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (Width(candidate, style) <= max)
            {
                line = candidate;
                continue;
            }
            if (line.Length > 0)
                lines.Add(line);
            var rest = word;
            while (rest.Length > 1 && Width(rest, style) > max)
            {
                var take = 1;
                while (take < rest.Length && Width(rest[..(take + 1)], style) <= max)
                    take++;
                if (Width(rest[..take], style) > max)
                    return [];
                lines.Add(rest[..take]);
                rest = rest[take..];
            }
            if (Width(rest, style) > max)
                return [];
            line = rest;
        }
        if (line.Length > 0)
            lines.Add(line);
        return lines;
    }

    /// <summary>Squarified rows: children placed along the shorter side as long as the worst aspect ratio improves.</summary>
    static List<(HierarchyNode Node, Box Box)> Squarify(List<HierarchyNode> children, Box box)
    {
        var result = new List<(HierarchyNode, Box)>();
        var total = children.Sum(c => c.Size);
        if (total <= 0)
            return result;
        var scale = box.W * box.H / total;
        var remaining = box;
        var queue = new Queue<HierarchyNode>(children);
        var row = new List<HierarchyNode>();
        double Worst(List<HierarchyNode> nodes, double side)
        {
            var area = nodes.Sum(n => n.Size * scale);
            if (area <= 0 || side <= 0)
                return double.MaxValue;
            var thickness = area / side;
            return nodes.Max(n => Math.Max(n.Size * scale / thickness / thickness, thickness * thickness / (n.Size * scale)));
        }
        void Place()
        {
            var area = row.Sum(n => n.Size * scale);
            if (remaining.W >= remaining.H)
            {
                // A column at the left of the remaining space.
                var w = remaining.H > 0 ? area / remaining.H : 0;
                var y = remaining.Y;
                foreach (var n in row)
                {
                    var h = w > 0 ? n.Size * scale / w : 0;
                    result.Add((n, new Box(remaining.X, y, w, h)));
                    y += h;
                }
                remaining = new Box(remaining.X + w, remaining.Y, Math.Max(0, remaining.W - w), remaining.H);
            }
            else
            {
                var h = remaining.W > 0 ? area / remaining.W : 0;
                var x = remaining.X;
                foreach (var n in row)
                {
                    var w = h > 0 ? n.Size * scale / h : 0;
                    result.Add((n, new Box(x, remaining.Y, w, h)));
                    x += w;
                }
                remaining = new Box(remaining.X, remaining.Y + h, remaining.W, Math.Max(0, remaining.H - h));
            }
            row.Clear();
        }
        while (queue.Count > 0)
        {
            var next = queue.Peek();
            var side = Math.Min(remaining.W, remaining.H);
            if (row.Count == 0 || Worst([.. row, next], side) <= Worst(row, side))
            {
                row.Add(queue.Dequeue());
                continue;
            }
            Place();
        }
        if (row.Count > 0)
            Place();
        return result;
    }

    /// <summary>
    /// A sunburst: rings from the hole (as wide as a ring) outwards, the top-level branches innermost, every node an
    /// arc of its parent's angle in the share of its size, the largest first clockwise from the top, in the ring of its
    /// level (measured: leaves of shallower branches leave the outer rings empty). Names run along the ray through the
    /// middle of their arc, never upside down, wrapped to the ring's width less 1.5 pt and left out where their lines
    /// with 3.6 pt insets are longer than the arc through their middle (measured: "Blatt" and "10" in a 34 pt arc are).
    /// </summary>
    string Sunburst(HierarchyNode root, Box plot, Func<HierarchyNode, Fill> fill, Outline border, bool names, TextStyle style)
    {
        var depth = 0;
        void Measure(HierarchyNode n) { depth = Math.Max(depth, n.Depth); n.Children.ForEach(Measure); }
        Measure(root);
        if (depth == 0)
            return "";
        var radius = Math.Min(plot.W, plot.H) / 2;
        var (cx, cy) = (plot.X + plot.W / 2, plot.Y + plot.H / 2);
        var ring = radius / (depth + 1);
        var lineHeight = LineHeight(style);
        var svg = new StringBuilder();
        var labels = new StringBuilder();
        void Draw(HierarchyNode node, double start, double sweep)
        {
            var total = node.Children.Sum(c => c.Size);
            var angle = start;
            foreach (var child in node.Children.Where(c => c.Size > 0).OrderByDescending(c => c.Size))
            {
                if (total <= 0)
                    break;
                var share = sweep * child.Size / total;
                var (inner, outer) = (ring * child.Depth, ring * (child.Depth + 1));
                svg.Append($"<path d=\"{Slice(cx, cy, outer, inner, angle, share)}\"{paint.Fill(fill(child), 0, 0, defs)}{paint.Stroke(border, 0, 0, defs)}/>");
                var lines = names ? WrapCharacters(child.Name, style, ring - 1.5) : [];
                var middleRadius = (inner + outer) / 2;
                if (lines.Count > 0 && lines.Count * lineHeight + 2 * 3.6 <= middleRadius * share * Math.PI / 180)
                {
                    var degrees = angle + share / 2;
                    var middle = degrees * Math.PI / 180;
                    var (lx, ly) = (cx + Math.Sin(middle) * middleRadius, cy - Math.Cos(middle) * middleRadius);
                    var rotation = degrees <= 180 ? degrees - 90 : degrees - 270;
                    labels.Append($"<g transform=\"translate({Px(lx)} {Px(ly)}) rotate({Units.Css(rotation)})\">");
                    var top = -lines.Count * lineHeight / 2;
                    foreach (var line in lines)
                    {
                        labels.Append(text.Label(line, style, -Width(line, style) / 2, top + Ascent(style)));
                        top += lineHeight;
                    }
                    labels.Append("</g>");
                }
                Draw(child, angle, share);
                angle += share;
            }
        }
        Draw(root, 0, 360);
        return svg.Append(labels).ToString();
    }
}
