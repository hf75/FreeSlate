using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Drawing;
using FreeSlate.Html;
using FreeSlate.Text;

namespace FreeSlate.Pptx.Charts;

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
    const double LegendKey = 0.45;            // × L, square legend keys
    const double LegendLineKey = 0.8214;      // × L, the key box of line series
    const double LegendKeyLeft = 0.275;       // × L from the entry to a square key
    const double LegendText = 0.923;          // × L from the entry to the text after a square key
    const double LegendLineCenter = 0.875, LegendLineHalf = 0.654, LegendLineText = 1.664;   // × L, line keys
    const double VerticalLegendPadding = 1.5, VerticalLegendExtra = 3;
    const double TitlePaddingX = 3, TitlePaddingY = 1.5;
    const double TickLength = 3.75;           // 5 px
    const double LabelOffset = 3;             // data labels from the end of a bar or a point
    const double PieLabelMargin = 1.785;      // × s: pies shrink by this on every side when they show data labels

    static readonly string[] MarkerCycle = ["diamond", "square", "triangle", "x", "star", "circle", "plus", "dot", "dash"];

    readonly StringBuilder defs = new();
    readonly StringBuilder dataLabels = new();
    ChartModel model = null!;
    XElement? chartStyle;
    XElement? colorStyle;
    CultureInfo culture = CultureInfo.InvariantCulture;
    ColorContext colors = null!;
    double width, height;
    double originX, originY;

    /// <summary>The diameter of the largest bubble at a bubble scale of 100 %, in points.</summary>
    double bubbleBase;

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

    /// <summary>
    /// A legend entry. Series with a visible line get a wide line key; line-type series without a line (markers only,
    /// stock series) keep the square key's width and show just their marker there (measured).
    /// </summary>
    sealed record LegendEntry(string Text, Fill Fill, Outline? Outline, bool LineKey, XElement? Marker, int MarkerIndex, bool MarkerOnly = false, bool Circle = false);

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
        public bool HasLabels => Visible && Axis.TickLabelPosition != "none" && !InTable;
        public AxisScale Scale { get; set; }
        public List<string> Labels { get; set; } = [];
        public double MaxLabelWidth { get; set; }
        public int Categories { get; set; }
        public bool Between { get; set; } = true;
        public double Start { get; set; }
        public double End { get; set; }

        /// <summary>Label rotation in degrees; negative values rise to the right.</summary>
        public double Rotation { get; set; }

        /// <summary>The labels sit next to an axis line inside the plot area (negative values).</summary>
        public bool Inside { get; set; }

        /// <summary>The axis has a title; its labels keep 1.5 pt from it instead of reaching into the margin.</summary>
        public bool Titled { get; set; }

        /// <summary>A data table below the plot area shows the category labels instead of the axis.</summary>
        public bool InTable { get; set; }

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
        culture = ChartModel.Culture(ChartModel.Val(model.ChartSpace, "lang"));
        chartStyle = Related("chartStyle");
        colorStyle = Related("chartColorStyle");
        colors = Related("themeOverride") is { } themeOverride ? slide.Colors.WithTheme(slide.Theme.WithOverride(themeOverride)) : slide.Colors;

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
                case "tr":
                    // Next to a corner legend the plot area starts 5 pt below it and ends 17 pt above the bottom.
                    right = legend.Box.X;
                    top = legend.Box.Bottom - 6;
                    bottom = height - 6;
                    break;
            }
        }
        var outer = new Box(left + PlotGap, top + PlotGap, Math.Max(0, right - left - 2 * PlotGap), Math.Max(0, bottom - top - 2 * PlotGap));

        var body = new StringBuilder();
        var pies = model.Groups.Where(g => g.IsPie).ToList();
        var radars = model.Groups.Where(g => g.IsRadar).ToList();
        var axisGroups = model.Groups.Where(g => !g.IsPie && !g.IsRadar).ToList();
        if (axisGroups.Count > 0)
            body.Append(AxisChart(axisGroups, outer, legend, title));
        if (radars.Count > 0)
            body.Append(Radar(radars, outer));
        foreach (var pie in pies)
            body.Append(Pie(pie, PlotBox(outer, square: true)));

        svg.Append(body);
        svg.Append(dataLabels);
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
        style.Font.Data.LayoutWidth(value, style.Size, style.Kerning, style.LetterSpacing);

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
        var fill = FillReader.FindFill(spPr) is { } f ? FillReader.Read(f, colors) : Fill.None;
        var outline = spPr?.Element(Ns.A + "ln") is { } ln ? OutlineReader.Merge([(ln, colors)]) : null;
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

    // ---------------------------------------------------------------- data labels

    /// <summary>What a data label shows and where (c:dLbl of the point, else c:dLbls of the series, else of the group).</summary>
    sealed record LabelSpec(bool Value, bool Category, bool Series, bool Percent, string Separator, string? Position,
        string? FormatCode, TextStyle Style, XElement? SpPr, (double L, double T, double R, double B) Insets);

    LabelSpec? DataLabel(ChartGroup group, ChartSeries series, int point)
    {
        var seriesLabels = series.Element.Element(Ns.C + "dLbls");
        var pointLabel = seriesLabels?.Elements(Ns.C + "dLbl").FirstOrDefault(d => d.Element(Ns.C + "idx").Int("val") == point);
        var layers = new[] { pointLabel, seriesLabels, group.Element.Element(Ns.C + "dLbls") }.Where(l => l != null).Cast<XElement>().ToList();
        if (layers.Count == 0 || ChartModel.Flag(layers[0], "delete", false))
            return null;
        bool Show(string name) => layers.FirstOrDefault(l => l.Element(Ns.C + name) != null) is { } owner && ChartModel.Flag(owner, name, false);
        var spec = (Value: Show("showVal"), Category: Show("showCatName"), Series: Show("showSerName"), Percent: Show("showPercent"));
        if (!spec.Value && !spec.Category && !spec.Series && !spec.Percent)
            return null;

        XElement? Child(string name) => layers.Select(l => l.Element(Ns.C + name)).FirstOrDefault(e => e != null);
        var txPr = Child("txPr");
        var body = txPr?.Element(Ns.A + "bodyPr");
        double Inset(string name, long fallback) => (body.Long(name) ?? fallback) / Units.EmuPerPoint;
        var numFmt = Child("numFmt");
        var separator = Child("separator")?.Value ?? (group.IsPie ? "\n" : ", ");
        return new LabelSpec(spec.Value, spec.Category, spec.Series, spec.Percent, separator, Child("dLblPos").Str("val"),
            numFmt?.Bool("sourceLinked") == true ? null : numFmt.Str("formatCode"), Style(txPr, "dataLabel"), Child("spPr"),
            (Inset("lIns", 38100), Inset("tIns", 19050), Inset("rIns", 38100), Inset("bIns", 19050)));
    }

    string LabelText(LabelSpec spec, ChartSeries series, int point, double value, double total)
    {
        var parts = new List<string>();
        if (spec.Series) parts.Add(series.Name);
        if (spec.Category) parts.Add(series.Categories.ElementAtOrDefault(point) ?? (point + 1).ToString(culture));
        if (spec.Value) parts.Add(NumberFormat.Format(value, spec.FormatCode ?? series.FormatCode, culture));
        if (spec.Percent) parts.Add(NumberFormat.Format(total == 0 ? 0 : Math.Abs(value) / total, spec.FormatCode is { } f && f.Contains('%') ? f : "0%", culture));
        return string.Join(spec.Separator, parts);
    }

    /// <summary>The size of a data label's box: its lines plus the text insets.</summary>
    static (double W, double H) LabelSize(LabelSpec spec, string value)
    {
        var lines = value.Split('\n');
        return (lines.Max(l => Width(l, spec.Style)) + spec.Insets.L + spec.Insets.R,
            lines.Length * LineHeight(spec.Style) + spec.Insets.T + spec.Insets.B);
    }

    /// <summary>Draws a data label whose box has its top-left corner at (x, y); lines are centered.</summary>
    void DrawLabel(LabelSpec spec, string value, double x, double y)
    {
        var (w, h) = LabelSize(spec, value);
        dataLabels.Append(Area(spec.SpPr, new Box(x, y, w, h), snap: false));
        var lineHeight = LineHeight(spec.Style);
        var baseline = y + spec.Insets.T + Ascent(spec.Style);
        foreach (var line in value.Split('\n'))
        {
            var lw = Width(line, spec.Style);
            dataLabels.Append(text.Label(line, spec.Style, x + spec.Insets.L + (w - spec.Insets.L - spec.Insets.R - lw) / 2, baseline));
            baseline += lineHeight;
        }
    }

    /// <summary>Places a label at a point: centered, or 3 pt off it to one side (r, l, t, b).</summary>
    void LabelAtPoint(LabelSpec spec, string value, double x, double y, string position)
    {
        var (w, h) = LabelSize(spec, value);
        var (lx, ly) = position switch
        {
            "l" => (x - LabelOffset - w, y - h / 2),
            "t" => (x - w / 2, y - LabelOffset - h),
            "b" => (x - w / 2, y + LabelOffset),
            "ctr" => (x - w / 2, y - h / 2),
            _ => (x + LabelOffset, y - h / 2),
        };
        DrawLabel(spec, value, lx, ly);
    }

    // ---------------------------------------------------------------- axis titles

    sealed class AxisTitle
    {
        public required TextBlock Text { get; init; }
        public required double Rotation { get; init; }
        public required Box Box { get; set; }
        public XElement? SpPr { get; init; }
    }

    AxisTitle? BuildAxisTitle(ChartAxis axis)
    {
        if (axis.Deleted || axis.Title is not { } element)
            return null;
        var layers = new List<XElement?> { DefRPr(element.Element(Ns.C + "txPr")), DefRPr(model.ChartSpace.Element(Ns.C + "txPr")), StyleLayer("axisTitle"),
            new XElement(Ns.A + "defRPr", new XAttribute("sz", "1000"), new XAttribute("b", "1")) };
        var rich = element.Element(Ns.C + "tx")?.Element(Ns.C + "rich")
                   ?? new XElement(Ns.C + "rich", new XElement(Ns.A + "p", new XElement(Ns.A + "r", new XElement(Ns.A + "t",
                       culture.TwoLetterISOLanguageName == "de" ? "Achsentitel" : "Axis Title"))));
        var layout = text.ChartRich(rich, layers, width * 0.8, wrap: true);
        var style = layout.Lines.SelectMany(l => l.Runs).FirstOrDefault()?.Style ?? text.ChartStyle(layers);
        var rot = rich.Element(Ns.A + "bodyPr").Long("rot") ?? element.Element(Ns.C + "txPr")?.Element(Ns.A + "bodyPr").Long("rot");
        var rotation = rot is { } r && r != -60000000 ? r / 60000.0 : axis.Horizontal ? 0 : -90;
        var block = new TextBlock { Layout = layout, Style = style };
        var along = block.Width + 2 * TitlePaddingX;
        var across = Math.Max(1, layout.Lines.Count) * LineHeight(style) + 2 * TitlePaddingY;
        var vertical = Math.Abs(Math.Abs(rotation) - 90) < 1;
        return new AxisTitle
        {
            Text = block,
            Rotation = vertical ? rotation : 0,
            Box = vertical ? new Box(0, 0, across, along) : new Box(0, 0, along, across),
            SpPr = element.Element(Ns.C + "spPr"),
        };
    }

    string DrawAxisTitle(AxisTitle title)
    {
        var svg = new StringBuilder(Area(title.SpPr, title.Box, snap: false));
        var style = title.Text.Style;
        var lineHeight = LineHeight(style);
        var (cx, cy) = (title.Box.X + title.Box.W / 2, title.Box.Y + title.Box.H / 2);
        var lines = title.Text.Layout.Lines;
        var blockHeight = Math.Max(1, lines.Count) * lineHeight;
        svg.Append($"<g transform=\"translate({Px(cx)} {Px(cy)})" + (title.Rotation != 0 ? $" rotate({Units.Css(title.Rotation)})" : "") + "\">");
        var baseline = -blockHeight / 2 + Ascent(style);
        foreach (var line in lines)
        {
            svg.Append(text.EmitLine(line, -line.Width / 2, baseline));
            baseline += lineHeight;
        }
        svg.Append("</g>");
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
                    var outline = SeriesOutline(group, series);
                    var lineKey = group.HasLines && outline is { Visible: true } && (!group.IsScatter || ScatterLines(group, series));
                    entries.Add(new LegendEntry(series.Name, PointFill(group, series, -1), outline, lineKey,
                        group.HasLines ? Marker(group, series) : null, series.Index, MarkerOnly: group.HasLines && !lineKey, Circle: group.IsBubble));
                }
            }
            index += group.Series.Count;
        }
        if (entries.Count == 0)
            return null;

        var position = ChartModel.Val(element, "legendPos") ?? "r";
        var lineHeight = LineHeight(style);
        var rowHeight = lineHeight + LegendRowPadding;
        var layout = new LegendLayout
        {
            Position = position,
            Style = style,
            Entries = entries,
            Box = new Box(0, 0, 0, 0),
            Overlay = ChartModel.Flag(element, "overlay", false),
            SpPr = element.Element(Ns.C + "spPr"),
        };

        // An entry is [key/2][key][key/2][text]; square keys are 0.45 L wide, line keys 0.82 L.
        double Key(LegendEntry e) => (e.LineKey ? LegendLineKey : LegendKey) * lineHeight;
        var widths = entries.Select(e => 2 * Key(e) + Width(e.Text, style)).ToList();

        Box box;
        if (position is "t" or "b")
        {
            // One row 1.2 times as wide as the entries, which share the extra space in equal gaps; a row that
            // does not fit is squeezed to the maximum width, and wraps into balanced rows when even that fails.
            var sum = widths.Sum();
            var maxWidth = Math.Max(1, width - 30);
            int columns;
            double w, gap;
            if (sum * 1.2 <= maxWidth || sum <= maxWidth)
            {
                columns = entries.Count;
                w = Math.Min(sum * 1.2, maxWidth);
                gap = (w - sum) / (entries.Count + 1);
            }
            else
            {
                var column = widths.Max();
                var maxColumns = Math.Max(1, (int)Math.Floor(maxWidth / column));
                var rowCount = (int)Math.Ceiling(entries.Count / (double)maxColumns);
                columns = (int)Math.Ceiling(entries.Count / (double)rowCount);
                w = columns * column;
                gap = 0;
                widths = widths.Select(_ => column).ToList();
            }
            var rows = (int)Math.Ceiling(entries.Count / (double)columns);
            for (var i = 0; i < entries.Count; i++)
            {
                var (row, col) = Math.DivRem(i, columns);
                var x = gap + Enumerable.Range(row * columns, col).Sum(j => widths[j] + gap);
                layout.Placed.Add((entries[i], x, rowHeight * (row + 0.5)));
            }
            var h = rows * rowHeight - (rows - 1) * 1.5;   // measured: wrapped legends are 1.5 pt shorter per extra row
            var y = position == "b" ? height - EdgeGap - h : (title is { Overlay: false } ? title.Box.Bottom : 0) + EdgeGap;
            box = new Box((width - w) / 2, y, w, h);
        }
        else
        {
            var w = widths.Max() + 2 * VerticalLegendPadding + VerticalLegendExtra;
            var h = rowHeight * entries.Count;
            for (var i = 0; i < entries.Count; i++)
                layout.Placed.Add((entries[i], VerticalLegendPadding, rowHeight * (i + 0.5)));
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
        var style = legend.Style;
        var lineHeight = LineHeight(style);
        foreach (var (entry, x, rowCenter) in legend.Placed)
        {
            var left = legend.Box.X + x;
            var center = legend.Box.Y + rowCenter;
            var baseline = CenteredBaseline(style, center);
            if (entry.LineKey)
            {
                var keyCenter = left + LegendLineCenter * lineHeight;
                var half = LegendLineHalf * lineHeight;
                if (entry.Outline is { Visible: true } line)
                {
                    var (a, b) = (SnapPoint((keyCenter - half, center), line), SnapPoint((keyCenter + half, center), line));
                    svg.Append($"<path d=\"M{Px(a.X)} {Px(a.Y)}L{Px(b.X)} {Px(b.Y)}\" fill=\"none\"{paint.Stroke(line, 0, 0, defs)}/>");
                }
                if (entry.Marker != null)
                    svg.Append(DrawMarker(entry.Marker, entry.MarkerIndex, keyCenter, center, entry.Fill, entry.Outline));
                svg.Append(text.Label(entry.Text, style, left + LegendLineText * lineHeight, baseline));
            }
            else if (entry.MarkerOnly)
            {
                if (entry.Marker != null)
                    svg.Append(DrawMarker(entry.Marker, entry.MarkerIndex, left + (LegendKeyLeft + LegendKey / 2) * lineHeight, center, entry.Fill, entry.Outline));
                svg.Append(text.Label(entry.Text, style, left + LegendText * lineHeight, baseline));
            }
            else
            {
                var key = LegendKey * lineHeight;
                var outline = entry.Outline is { Visible: true } o ? o with { WidthEmu = Math.Min(o.WidthEmu, 19050) } : null;
                if (entry.Circle)
                {
                    // Bubble series show a round key.
                    var (kx, r) = (left + LegendKeyLeft * lineHeight + key / 2, key / 2);
                    svg.Append($"<circle cx=\"{Px(kx)}\" cy=\"{Px(center)}\" r=\"{Px(r)}\"{paint.Fill(entry.Fill, key, key, defs)}{paint.Stroke(outline, key, key, defs)}/>");
                }
                else
                {
                    svg.Append(Rect(new Box(left + LegendKeyLeft * lineHeight, center - key / 2, key, key), entry.Fill, outline, snap: false));
                }
                svg.Append(text.Label(entry.Text, style, left + LegendText * lineHeight, baseline));
            }
        }
        return svg.ToString();
    }

    // ---------------------------------------------------------------- series colors

    /// <summary>The automatic color of series or point <paramref name="index"/>: the chart's color style, else the accents.</summary>
    RgbColor AutoColor(int index)
    {
        var palette = colorStyle?.Elements().Where(e => e.Name.Namespace == Ns.A).ToList() ?? [];
        var variations = colorStyle?.Elements(Ns.Cs + "variation").ToList() ?? [];
        if (palette.Count == 0)
            palette = Enumerable.Range(1, 6).Select(i => new XElement(Ns.A + "schemeClr", new XAttribute("val", $"accent{i}"))).ToList();
        var color = new XElement(palette[index % palette.Count]);
        var cycle = index / palette.Count;
        if (variations.Count > 0)
            color.Add(variations[cycle % variations.Count].Elements());
        return colors.Resolve(color);
    }

    Fill PointFill(ChartGroup group, ChartSeries series, int point)
    {
        if (point >= 0 && series.Points.TryGetValue(point, out var dPt) && FillReader.FindFill(dPt.Element(Ns.C + "spPr")) is { } own)
            return FillReader.Read(own, colors);
        if (point >= 0 && group.VaryColors && group.Series.Count == 1)
            return new SolidFill(AutoColor(point));
        if (FillReader.FindFill(series.SpPr) is { } fill)
            return FillReader.Read(fill, colors);
        if (group.IsLine || group.IsScatter)
            return new SolidFill(AutoColor(series.Index));
        return new SolidFill(AutoColor(series.Index));
    }

    Outline? PointOutline(ChartSeries series, int point)
    {
        if (point >= 0 && series.Points.TryGetValue(point, out var dPt) && dPt.Element(Ns.C + "spPr")?.Element(Ns.A + "ln") is { } own)
            return OutlineReader.Merge([(own, colors)]);
        return series.SpPr?.Element(Ns.A + "ln") is { } ln ? OutlineReader.Merge([(ln, colors)]) : null;
    }

    /// <summary>The line of a line or scatter series: its own a:ln, else an automatic 2.25 pt line.</summary>
    Outline? SeriesOutline(ChartGroup group, ChartSeries series)
    {
        var own = series.SpPr?.Element(Ns.A + "ln");
        if (!group.HasLines)
            return own != null ? OutlineReader.Merge([(own, colors)]) : null;
        var automatic = new XElement(Ns.A + "ln", new XAttribute("w", "28575"), new XAttribute("cap", "rnd"),
            new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", AutoColor(series.Index).ToHex().TrimStart('#')))),
            new XElement(Ns.A + "round"));
        var layers = new List<(XElement, ColorContext)> { (automatic, colors) };
        if (own != null) layers.Add((own, colors));
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

        // Axis titles take their room from the outer plot area, at the edge of their axis.
        var titles = new List<(AxisLayout Axis, AxisTitle Title)>();
        foreach (var layout in axes)
        {
            if (BuildAxisTitle(layout.Axis) is not { } axisTitle)
                continue;
            layout.Titled = true;
            var b = axisTitle.Box;
            switch (layout.Axis.Position)
            {
                case "l": axisTitle.Box = b with { X = outer.X }; outer = outer with { X = outer.X + b.W, W = Math.Max(1, outer.W - b.W) }; break;
                case "r": axisTitle.Box = b with { X = outer.Right - b.W }; outer = outer with { W = Math.Max(1, outer.W - b.W) }; break;
                case "t": axisTitle.Box = b with { Y = outer.Y }; outer = outer with { Y = outer.Y + b.H, H = Math.Max(1, outer.H - b.H) }; break;
                default: axisTitle.Box = b with { Y = outer.Bottom - b.H }; outer = outer with { H = Math.Max(1, outer.H - b.H) }; break;
            }
            titles.Add((layout, axisTitle));
        }

        var table = BuildDataTable(groups, axes);
        var inner = ManualPlot();
        var plot = inner ?? outer;
        // Measured: the largest bubble is 0.2307 m + 2.65 pt wide, m being the smaller side of the plot area
        // before the axis labels take their room.
        bubbleBase = 0.2307 * Math.Min(plot.W, plot.H) + 2.65;
        for (var pass = 0; pass < 3; pass++)
        {
            foreach (var layout in axes.Where(a => a.Axis.IsValue))
                ScaleValueAxis(layout, layout.Axis.Horizontal ? plot.W : plot.H);
            if (inner != null)
                break;
            var next = InnerPlot(outer, axes, plot, table);
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
            svg.Append(AxisLabels(layout, axes, plot));
        if (table != null)
            svg.Append(DrawDataTable(table, plot));
        foreach (var (layout, axisTitle) in titles)
        {
            var b = axisTitle.Box;
            axisTitle.Box = layout.Axis.Horizontal ? b with { X = plot.X + (plot.W - b.W) / 2 } : b with { Y = plot.Y + (plot.H - b.H) / 2 };
            svg.Append(DrawAxisTitle(axisTitle));
        }
        return svg.ToString();
    }

    // ---------------------------------------------------------------- data table

    const double DataTableBleed = 6;   // the table reaches this far past the plot area's left and bottom edges

    /// <summary>A data table below the plot area (catalog: chart.dataTable).</summary>
    sealed class DataTableLayout
    {
        public required XElement Element { get; init; }
        public required TextStyle Style { get; init; }
        public required AxisLayout Category { get; init; }
        public required List<(ChartGroup Group, ChartSeries Series)> Rows { get; init; }
        public required double RowHeight { get; init; }
        public required double NameWidth { get; init; }
        public required bool Keys { get; init; }

        /// <summary>The header row with the categories and one row per series.</summary>
        public double Height => RowHeight * (Rows.Count + 1);
    }

    /// <summary>
    /// The data table of c:dTable, when the categories run along the bottom. Measured: rows are L + 0.0925 s + 2.4 pt
    /// high; the name column holds the widest series name plus 1.0975 s + 2.89 pt (key and padding).
    /// </summary>
    DataTableLayout? BuildDataTable(List<ChartGroup> groups, List<AxisLayout> axes)
    {
        if (model.PlotArea?.Element(Ns.C + "dTable") is not { } element
            || axes.FirstOrDefault(a => !a.Axis.IsValue && a.Axis.Position == "b" && a.Visible) is not { } category)
            return null;
        var style = Style(element.Element(Ns.C + "txPr"), "dataTable");
        var rows = groups.Where(g => g.AxisIds.Contains(category.Axis.Id) && !g.IsScatter).SelectMany(g => g.Series.Select(s => (g, s))).ToList();
        var keys = ChartModel.Flag(element, "showKeys", false);
        var lineHeight = LineHeight(style);
        var names = rows.Count == 0 ? 0 : rows.Max(r => Width(r.s.Name, style));
        category.InTable = true;
        return new DataTableLayout
        {
            Element = element,
            Style = style,
            Category = category,
            Rows = rows,
            RowHeight = lineHeight + 0.0925 * style.Size + 2.4,
            NameWidth = names + (keys ? 1.0975 * style.Size + 2.89 : 0.26 * lineHeight + 2),
            Keys = keys,
        };
    }

    string DrawDataTable(DataTableLayout table, Box plot)
    {
        var svg = new StringBuilder();
        var style = table.Style;
        var lineHeight = LineHeight(style);
        var category = table.Category;
        var (top, rowsTop, bottom) = (plot.Bottom, plot.Bottom + table.RowHeight, plot.Bottom + table.Height);
        var (left, right) = (plot.X - table.NameWidth, plot.Right);
        var line = table.Element.Element(Ns.C + "spPr")?.Element(Ns.A + "ln") is { } ln
            ? OutlineReader.Merge([(ln, colors)])
            : new Outline(9525, new SolidFill(RgbColor.FromHex("D9D9D9")), "solid", "flat", "round", 8, "sng");
        if (line is { Visible: true })
        {
            if (ChartModel.Flag(table.Element, "showOutline", false))
            {
                svg.Append(GridLine(line, plot.X, top, right, top));
                svg.Append(GridLine(line, right, top, right, bottom));
                svg.Append(GridLine(line, left, bottom, right, bottom));
                svg.Append(GridLine(line, left, rowsTop, left, bottom));
                svg.Append(GridLine(line, left, rowsTop, plot.X, rowsTop));
                svg.Append(GridLine(line, plot.X, top, plot.X, rowsTop));
            }
            if (ChartModel.Flag(table.Element, "showHorzBorder", false))
            {
                for (var r = 1; r <= table.Rows.Count; r++)
                    svg.Append(GridLine(line, left, top + r * table.RowHeight, right, top + r * table.RowHeight));
            }
            if (ChartModel.Flag(table.Element, "showVertBorder", false))
            {
                for (var i = 0; i < category.Categories; i++)
                    svg.Append(GridLine(line, category.Boundary(i), top, category.Boundary(i), bottom));
            }
        }

        // Category names in the header row, then a row per series: key and name, values centered under their category.
        double RowCenter(int row) => top + (row + 0.5) * table.RowHeight;
        for (var i = 0; i < category.Categories && i < category.Labels.Count; i++)
        {
            var label = category.Labels[i];
            var center = (category.Boundary(i) + category.Boundary(i + 1)) / 2;
            svg.Append(text.Label(label, style, center - Width(label, style) / 2, CenteredBaseline(style, RowCenter(0))));
        }
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var (group, series) = table.Rows[r];
            var center = RowCenter(r + 1);
            if (table.Keys)
            {
                var key = LegendKey * lineHeight;
                var fill = group.HasLines ? SeriesOutline(group, series)?.Fill ?? PointFill(group, series, -1) : PointFill(group, series, -1);
                svg.Append(Rect(new Box(left + 0.26 * lineHeight, center - key / 2, key, key), fill, null, snap: false));
            }
            svg.Append(text.Label(series.Name, style, left + (table.Keys ? 0.958 : 0.26) * lineHeight, CenteredBaseline(style, center)));
            for (var i = 0; i < category.Categories; i++)
            {
                if (series.Values.ElementAtOrDefault(i) is not { } v)
                    continue;
                var value = NumberFormat.Format(v, series.FormatCode, culture);
                var middle = (category.Boundary(i) + category.Boundary(i + 1)) / 2;
                svg.Append(text.Label(value, style, middle - Width(value, style) / 2, CenteredBaseline(style, center)));
            }
        }
        return svg.ToString();
    }

    /// <summary>The plot area inside the axis labels (PowerPoint's inner plot area) for an automatic layout.</summary>
    static Box InnerPlot(Box outer, List<AxisLayout> axes, Box previous, DataTableLayout? table)
    {
        double left = outer.X, top = outer.Y, right = outer.Right, bottom = outer.Bottom;
        var edges = new HashSet<string>();
        if (table != null)
        {
            // The data table reaches 6 pt past the bottom of the plot area; its name column ends at the plot's left.
            edges.Add("b");
            bottom = outer.Bottom + DataTableBleed - table.Height;
            left = outer.X + DataTableBleed + table.NameWidth;
        }
        foreach (var layout in axes.Where(a => a.HasLabels))
        {
            var s = layout.Style.Size;
            layout.Rotation = Rotation(layout, layout.Axis.Horizontal ? previous.W : previous.H);
            layout.Inside = LabelsInside(layout, axes);
            if (layout.Inside)
                continue;
            edges.Add(layout.Axis.Position);
            var extent = LabelExtent(layout);
            var bleed = layout.Titled ? -TitlePaddingY : LabelBleed;
            switch (layout.Axis.Position)
            {
                case "l":
                    left = Math.Max(left, outer.X - bleed + extent + ValueLabelGap * s);
                    break;
                case "r":
                    right = Math.Min(right, outer.Right + bleed - extent - ValueLabelGap * s);
                    break;
                case "b":
                    bottom = Math.Min(bottom, outer.Bottom + bleed - extent - CategoryLabelGap * s);
                    break;
                case "t":
                    top = Math.Max(top, outer.Y - bleed + extent + CategoryLabelGap * s);
                    break;
            }
        }

        // Value labels centered on the outermost ticks may reach past the outer edge: half a line minus 6 pt
        // vertically, half a label horizontally.
        foreach (var layout in axes.Where(a => a.HasLabels && a.Axis.IsValue && a.Labels.Count > 0))
        {
            var half = LineHeight(layout.Style) / 2;
            if (layout.Axis.Horizontal)
            {
                if (!edges.Contains("r")) right = Math.Min(right, outer.Right - Width(layout.Labels[^1], layout.Style) / 2);
            }
            else
            {
                if (!edges.Contains("t")) top = Math.Max(top, outer.Y - 6 + half);
                if (!edges.Contains("b")) bottom = Math.Min(bottom, outer.Bottom + 6 - half);
            }
        }

        // Horizontal labels centered on the plot edges (midCat categories, dates) stay within the outer edges.
        foreach (var layout in axes.Where(a => a.HasLabels && !a.Inside && a.Rotation == 0 && a.Axis.Horizontal && !a.Axis.IsValue && !a.Between && a.Labels.Count > 0))
        {
            left = Math.Max(left, outer.X + Width(layout.Labels[0], layout.Style) / 2);
            right = Math.Min(right, outer.Right - Width(layout.Labels[^1], layout.Style) / 2);
        }

        // Labels rotated upwards reach left of the first category; they stay within 9 pt of the outer edge.
        foreach (var layout in axes.Where(a => a.HasLabels && !a.Inside && a.Rotation < 0 && a.Axis.Horizontal && a.Labels.Count > 0))
        {
            var angle = -layout.Rotation * Math.PI / 180;
            var reach = Width(layout.Labels[0], layout.Style) * Math.Cos(angle) + LineHeight(layout.Style) / 2 * Math.Sin(angle);
            var limit = outer.X - 9 + reach;
            var n = Math.Max(1, layout.Categories);
            var needed = layout.Between ? (limit - right / (2 * n)) / (1 - 1.0 / (2 * n)) : limit;
            left = Math.Max(left, needed);
        }
        return new Box(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    /// <summary>Label rotation in degrees (negative rises to the right): the text body's own, else −45° when category labels do not fit.</summary>
    static double Rotation(AxisLayout layout, double length)
    {
        var rot = layout.Axis.TxPr?.Element(Ns.A + "bodyPr").Long("rot");
        if (rot is { } r && r != -60000000)
            return r / 60000.0;
        if (layout.Axis.IsValue || !layout.Axis.Horizontal || layout.Categories == 0)
            return 0;
        return layout.MaxLabelWidth > length / layout.Categories ? -45 : 0;
    }

    /// <summary>The labels' extent away from the axis.</summary>
    static double LabelExtent(AxisLayout layout)
    {
        var lineHeight = LineHeight(layout.Style);
        var horizontal = layout.Axis.Horizontal;
        if (layout.Rotation == 0)
            return horizontal ? lineHeight : layout.MaxLabelWidth;
        var a = Math.Abs(layout.Rotation) * Math.PI / 180;
        return horizontal
            ? layout.MaxLabelWidth * Math.Sin(a) + lineHeight * Math.Cos(a)
            : layout.MaxLabelWidth * Math.Cos(a) + lineHeight * Math.Sin(a);
    }

    /// <summary>Labels "next to" an axis that crosses inside the plot area are drawn there and take no room at the edge.</summary>
    static bool LabelsInside(AxisLayout layout, List<AxisLayout> axes)
    {
        if (layout.Axis.TickLabelPosition != "nextTo" || layout.Axis.IsValue)
            return false;
        var cross = axes.FirstOrDefault(a => a.Axis.Id == layout.Axis.CrossAxisId && a.Axis.IsValue);
        if (cross == null || cross.Scale.Max <= cross.Scale.Min)
            return false;
        var value = CrossValue(cross, layout.Axis);
        var atStart = cross.Axis.Reversed ? value >= cross.Scale.Max : value <= cross.Scale.Min;
        return layout.Axis.Position is "b" or "l" ? !atStart && value < cross.Scale.Max : false;
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
        // At most ten intervals, each at least 1.1 line heights tall (vertical axes) or one label wide plus a gap.
        var extent = layout.Axis.Horizontal
            ? Math.Max(Width(NumberFormat.Format(max, Format(layout), culture), style), Width(NumberFormat.Format(min, Format(layout), culture), style)) + style.Size
            : LineHeight(style) * 1.1;
        var maxIntervals = Math.Clamp((int)Math.Floor(length / Math.Max(1, extent)), 1, 10);
        layout.Scale = AxisScaling.Compute(min, max, layout.Axis, maxIntervals, percent);

        // Bubbles have to fit: the data range grows by each bubble's radius in axis units until the scale settles.
        var bubbles = layout.Groups.Where(g => g.IsBubble).ToList();
        for (var pass = 0; pass < 4 && bubbles.Count > 0; pass++)
        {
            var unitsPerPoint = (layout.Scale.Max - layout.Scale.Min) / Math.Max(1, length);
            double lo = min, hi = max;
            foreach (var group in bubbles)
            {
                foreach (var s in group.Series)
                {
                    for (var i = 0; i < s.Values.Count; i++)
                    {
                        var position = layout.Axis.Horizontal ? s.XValues.Count > 0 ? s.XValues.ElementAtOrDefault(i) : i + 1 : s.Values[i];
                        if (position is not { } v || s.Values[i] == null)
                            continue;
                        var r = BubbleDiameter(group, BubbleSize(s, i)) / 2 * unitsPerPoint;
                        (lo, hi) = (Math.Min(lo, v - r), Math.Max(hi, v + r));
                    }
                }
            }
            var next = AxisScaling.Compute(lo, hi, layout.Axis, maxIntervals, percent);
            if (next == layout.Scale)
                break;
            layout.Scale = next;
        }
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
            var outline = GridOutline(element);
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

    /// <summary>The line of c:majorGridlines or c:minorGridlines: its own, else 0.75 pt light gray.</summary>
    Outline GridOutline(XElement gridlines) =>
        gridlines.Element(Ns.C + "spPr")?.Element(Ns.A + "ln") is { } ln
            ? OutlineReader.Merge([(ln, colors)])
            : new Outline(9525, new SolidFill(RgbColor.FromHex("D9D9D9")), "solid", "flat", "round", 8, "sng");

    /// <summary>The line of an axis: its own, else 0.75 pt gray.</summary>
    Outline AxisOutline(ChartAxis axis) =>
        axis.SpPr?.Element(Ns.A + "ln") is { } ln
            ? OutlineReader.Merge([(ln, colors)])
            : new Outline(9525, new SolidFill(RgbColor.FromHex("BFBFBF")), "solid", "flat", "round", 8, "sng");

    string AxisLine(AxisLayout layout, List<AxisLayout> axes, Box plot)
    {
        var outline = AxisOutline(layout.Axis);
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
        var svg = new StringBuilder(layout.Axis.Horizontal
            ? GridLine(outline, plot.X, position, plot.Right, position)
            : GridLine(outline, position, plot.Y, position, plot.Bottom));

        // Tick marks: out, in or cross, 5 px long, at the major ticks (category boundaries, or the points of midCat axes).
        foreach (var (mark, minor) in new[] { (layout.Axis.MajorTickMark, false), (layout.Axis.MinorTickMark, true) })
        {
            if (mark is not ("out" or "in" or "cross") || minor && layout.Axis.IsValue == false)
                continue;
            var ticks = layout.Axis.IsValue
                ? (minor ? layout.Scale.MinorTicks() : layout.Scale.Ticks()).Select(layout.Map)
                : layout.Between
                    ? Enumerable.Range(0, layout.Categories + 1).Select(i => layout.Boundary(i))
                    : Enumerable.Range(0, layout.Categories).Select(layout.Center);
            var outward = layout.Axis.Position is "b" or "r" ? 1 : -1;
            var length = TickLength * (minor ? 0.5 : 1);
            var (from, to) = mark switch { "out" => (0.0, length), "in" => (-length, 0.0), _ => (-length, length) };
            foreach (var t in ticks)
            {
                svg.Append(layout.Axis.Horizontal
                    ? GridLine(outline, t, position + outward * from, t, position + outward * to)
                    : GridLine(outline, position + outward * from, t, position + outward * to, t));
            }
        }
        return svg.ToString();
    }

    string AxisLabels(AxisLayout layout, List<AxisLayout> axes, Box plot)
    {
        var svg = new StringBuilder();
        var style = layout.Style;
        var s = style.Size;
        var lineHeight = LineHeight(style);
        var positions = layout.Axis.IsValue
            ? layout.Scale.Ticks().Select(layout.Map).ToList()
            : Enumerable.Range(0, layout.Categories).Select(layout.Center).ToList();

        // The line the labels hang from: the plot edge, or the axis line for labels inside the plot area.
        double left = plot.X, right = plot.Right, top = plot.Y, bottom = plot.Bottom;
        if (layout.Inside && axes.FirstOrDefault(a => a.Axis.Id == layout.Axis.CrossAxisId) is { } cross)
        {
            var position = cross.Map(CrossValue(cross, layout.Axis));
            (left, bottom) = (position, position);
        }

        var angle = Math.Abs(layout.Rotation) * Math.PI / 180;
        for (var i = 0; i < layout.Labels.Count && i < positions.Count; i++)
        {
            var label = layout.Labels[i];
            var w = Width(label, style);
            var p = positions[i];
            if (layout.Rotation != 0 && layout.Axis.Position is "b")
            {
                // Rotated labels: the end (rising) or start (falling) of the text's middle line at the category,
                // with the rotated box keeping the usual gap below the axis.
                var anchorY = bottom + CategoryLabelGap * s + lineHeight / 2 * Math.Cos(angle);
                svg.Append($"<g transform=\"translate({Px(p)} {Px(anchorY)}) rotate({Units.Css(layout.Rotation)})\">");
                svg.Append(text.Label(label, style, layout.Rotation < 0 ? -w : 0, CenteredBaseline(style, 0)));
                svg.Append("</g>");
                continue;
            }
            switch (layout.Axis.Position)
            {
                case "l":
                    svg.Append(text.Label(label, style, left - ValueLabelGap * s - w, CenteredBaseline(style, p)));
                    break;
                case "r":
                    svg.Append(text.Label(label, style, right + ValueLabelGap * s, CenteredBaseline(style, p)));
                    break;
                case "b":
                    svg.Append(text.Label(label, style, p - w / 2, bottom + CategoryLabelGap * s + Ascent(style)));
                    break;
                case "t":
                    svg.Append(text.Label(label, style, p - w / 2, top - CategoryLabelGap * s - Descent(style)));
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

                if (DataLabel(group, series, i) is { } spec && series.Values[i] is { } raw)
                {
                    var label = LabelText(spec, series, i, raw, total);
                    var (w, h) = LabelSize(spec, label);
                    var position = spec.Position ?? (group.Stacked ? "ctr" : "outEnd");
                    var across = (c0 + c1) / 2;
                    var grows = Math.Sign(v1 - v0) is var sign && sign != 0 ? sign : group.Horizontal ? 1 : -1;
                    var extent = group.Horizontal ? w : h;
                    // Start of the label box along the value direction (smaller coordinate).
                    var along = position switch
                    {
                        "ctr" => (v0 + v1) / 2 - extent / 2,
                        "inEnd" => grows > 0 ? v1 - LabelOffset - extent : v1 + LabelOffset,
                        "inBase" => grows > 0 ? v0 + LabelOffset : v0 - LabelOffset - extent,
                        _ => grows > 0 ? v1 + LabelOffset : v1 - LabelOffset - extent,
                    };
                    if (group.Horizontal)
                        DrawLabel(spec, label, along, across - h / 2);
                    else
                        DrawLabel(spec, label, across - w / 2, along);
                }
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
        var all = Enumerable.Range(0, group.Series.Count).Select(k => SeriesPoints(group, k, category, value, new double[category.Categories])).ToList();
        svg.Append(StockElements(group, category, value, all));
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
            for (var i = 0; i < points.Count; i++)
            {
                if (points[i] is { } p && series.Values.ElementAtOrDefault(i) is { } raw && DataLabel(group, series, i) is { } spec)
                    LabelAtPoint(spec, LabelText(spec, series, i, raw, 0), p.X, p.Y, spec.Position ?? "r");
            }
        }
        return svg.ToString();
    }

    /// <summary>
    /// Drop lines (each category's highest point down to the axis), high-low lines (its lowest to its highest
    /// point) and up/down bars (first series to last, as wide as a category divided by 1 + gap width) of line and
    /// stock charts (catalog: chart.line, chart.stock); the bars cover the lines and the series are drawn on top.
    /// </summary>
    string StockElements(ChartGroup group, AxisLayout category, AxisLayout value, List<List<(double X, double Y)?>> points)
    {
        var svg = new StringBuilder();
        var horizontal = category.Axis.Horizontal;
        double Along((double X, double Y) p) => horizontal ? p.Y : p.X;
        string Segment(Outline outline, double c, double v0, double v1) => horizontal
            ? Polyline([(c, v0), (c, v1)], outline, closed: false)
            : Polyline([(v0, c), (v1, c)], outline, closed: false);
        Outline LineOf(XElement element) => element.Element(Ns.C + "spPr")?.Element(Ns.A + "ln") is { } ln
            ? OutlineReader.Merge([(ln, colors)])
            : new Outline(9525, new SolidFill(RgbColor.Black), "solid", "flat", "round", 8, "sng");

        for (var i = 0; i < category.Categories; i++)
        {
            var present = points.Select(p => p.ElementAtOrDefault(i)).Where(p => p.HasValue).Select(p => Along(p!.Value)).ToList();
            if (present.Count == 0)
                continue;
            var c = category.Center(i);
            // Screen coordinates grow downwards: the highest value has the smallest y (largest x for bars).
            var (low, high) = horizontal ? (present.Max(), present.Min()) : (present.Min(), present.Max());
            if (group.Element.Element(Ns.C + "dropLines") is { } dropLines)
                svg.Append(Segment(LineOf(dropLines), c, value.Map(CrossValue(value, category.Axis)), high));
            if (group.Element.Element(Ns.C + "hiLowLines") is { } hiLowLines && present.Count > 1)
                svg.Append(Segment(LineOf(hiLowLines), c, low, high));
        }

        if (group.Element.Element(Ns.C + "upDownBars") is { } bars && points.Count > 1)
        {
            var gap = (ChartModel.Number(bars, "gapWidth") ?? 150) / 100;
            var width = Math.Abs(category.End - category.Start) / Math.Max(1, category.Categories) / (1 + gap);
            for (var i = 0; i < category.Categories; i++)
            {
                if (group.Series[0].Values.ElementAtOrDefault(i) is not { } open || group.Series[^1].Values.ElementAtOrDefault(i) is not { } close
                    || points[0].ElementAtOrDefault(i) is not { } from || points[^1].ElementAtOrDefault(i) is not { } to)
                    continue;
                var part = bars.Element(Ns.C + (close >= open ? "upBars" : "downBars"));
                var spPr = part?.Element(Ns.C + "spPr");
                var fill = FillReader.FindFill(spPr) is { } f ? FillReader.Read(f, colors) : new SolidFill(close >= open ? RgbColor.White : RgbColor.Black);
                var outline = spPr?.Element(Ns.A + "ln") is { } ln ? OutlineReader.Merge([(ln, colors)])
                    : new Outline(9525, new SolidFill(RgbColor.Black), "solid", "flat", "round", 8, "sng");
                var c = category.Center(i);
                var (v0, v1) = (Math.Min(Along(from), Along(to)), Math.Max(Along(from), Along(to)));
                var box = horizontal ? new Box(c - width / 2, v0, width, v1 - v0) : new Box(v0, c - width / 2, v1 - v0, width);
                svg.Append(Rect(box, fill, outline, snap: true));
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

    /// <summary>The size of a bubble: c:bubbleSize, else 1.</summary>
    static double BubbleSize(ChartSeries series, int point) =>
        series.BubbleSizes.Count == 0 ? 1 : series.BubbleSizes.ElementAtOrDefault(point) ?? 0;

    /// <summary>
    /// A bubble's diameter in points (catalog: chart.scatterBubble): the group's largest bubble is as wide as
    /// <see cref="bubbleBase"/> times c:bubbleScale; the others scale by area (c:sizeRepresents="area") or width.
    /// Negative sizes are not drawn.
    /// </summary>
    double BubbleDiameter(ChartGroup group, double size)
    {
        var largest = group.Series.SelectMany(s => Enumerable.Range(0, s.Values.Count).Select(i => BubbleSize(s, i))).DefaultIfEmpty(0).Max();
        if (largest <= 0 || size <= 0)
            return 0;
        var full = bubbleBase * (ChartModel.Number(group.Element, "bubbleScale") ?? 100) / 100;
        return ChartModel.Val(group.Element, "sizeRepresents") == "w" ? full * size / largest : full * Math.Sqrt(size / largest);
    }

    string Bubbles(ChartGroup group, AxisLayout xAxis, AxisLayout yAxis)
    {
        var svg = new StringBuilder();
        foreach (var series in group.Series)
        {
            for (var i = 0; i < series.Values.Count; i++)
            {
                var x = series.XValues.Count > 0 ? series.XValues.ElementAtOrDefault(i) : i + 1;
                var d = BubbleDiameter(group, BubbleSize(series, i));
                if (x is not { } xv || series.Values[i] is not { } yv || d <= 0)
                    continue;
                var (cx, cy, r) = (xAxis.Map(xv), yAxis.Map(yv), d / 2);
                svg.Append($"<circle cx=\"{Px(cx)}\" cy=\"{Px(cy)}\" r=\"{Px(r)}\"{paint.Fill(PointFill(group, series, i), d, d, defs)}{paint.Stroke(PointOutline(series, i), d, d, defs)}/>");
                if (DataLabel(group, series, i) is { } spec)
                    LabelAtPoint(spec, LabelText(spec, series, i, yv, 0), cx, cy, spec.Position ?? "ctr");
            }
        }
        return svg.ToString();
    }

    string Scatter(ChartGroup group, AxisLayout xAxis, AxisLayout yAxis)
    {
        if (group.IsBubble)
            return Bubbles(group, xAxis, yAxis);
        var svg = new StringBuilder();
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
            if (ScatterLines(group, series) && outline is { Visible: true })
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

    /// <summary>Whether a scatter series draws lines: its scatter style asks for them or its a:ln has a fill.</summary>
    static bool ScatterLines(ChartGroup group, ChartSeries series)
    {
        var ln = series.SpPr?.Element(Ns.A + "ln");
        var style = ChartModel.Val(group.Element, "scatterStyle") ?? "marker";
        return ln?.Element(Ns.A + "noFill") == null && style is not ("marker" or "none") || ln?.Element(Ns.A + "solidFill") != null;
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
        if (group.IsBubble)
            return null;
        var own = series.Element.Element(Ns.C + "marker");
        var symbol = ChartModel.Val(own, "symbol");
        if (symbol == "none")
            return null;
        if (own != null && symbol != null)
            return own;
        var groupMarkers = group.IsScatter
            ? (ChartModel.Val(group.Element, "scatterStyle") ?? "marker") is "marker" or "lineMarker" or "smoothMarker"
            : group.IsRadar
                ? ChartModel.Val(group.Element, "radarStyle") == "marker"
                : ChartModel.Flag(group.Element, "marker", false);
        return groupMarkers ? own ?? new XElement(Ns.C + "marker") : null;
    }

    string DrawMarker(XElement marker, int seriesIndex, double x, double y, Fill seriesFill, Outline? seriesLine)
    {
        var symbol = ChartModel.Val(marker, "symbol") ?? MarkerCycle[seriesIndex % MarkerCycle.Length];
        if (symbol == "auto") symbol = MarkerCycle[seriesIndex % MarkerCycle.Length];
        var size = Math.Clamp(ChartModel.Number(marker, "size") ?? 5, 2, 72);
        var spPr = marker.Element(Ns.C + "spPr");
        var fill = FillReader.FindFill(spPr) is { } f ? FillReader.Read(f, colors) : seriesFill is NoFill ? new SolidFill(seriesLine?.Fill is SolidFill sf ? sf.Color : RgbColor.Black) : seriesFill;
        var line = spPr?.Element(Ns.A + "ln") is { } ln
            ? OutlineReader.Merge([(ln, colors)])
            : seriesLine is { Visible: true } ? seriesLine with { WidthEmu = 9525, Dash = "solid" } : null;
        // A dot is a small circle half the marker size (measured).
        var r = symbol == "dot" ? size / 4 : size / 2;
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

    // ---------------------------------------------------------------- radar

    /// <summary>
    /// Radar charts (catalog: chart.radar): one spoke per category clockwise from 12 o'clock, the value gridlines as
    /// polygons, the value axis line as all spokes and its labels along the first one (the category axis line and
    /// gridlines are not drawn). The radar is centered in the plot area; its radius leaves room for the category
    /// labels, measured: the widest label plus 0.3056 s − 3.005 pt on either side and a line plus 0.3127 s − 5.248 pt
    /// above and below (s = label size), whatever the labels' positions.
    /// </summary>
    string Radar(List<ChartGroup> groups, Box outer)
    {
        var first = groups[0];
        var valueAxis = model.Axes.FirstOrDefault(a => a.IsValue && first.AxisIds.Contains(a.Id));
        var categoryAxis = model.Axes.FirstOrDefault(a => !a.IsValue && first.AxisIds.Contains(a.Id));
        var all = groups.SelectMany(g => g.Series).ToList();
        var n = all.Count == 0 ? 0 : all.Max(s => Math.Max(s.Values.Count, s.Categories.Count));
        if (valueAxis == null || n == 0)
            return "";
        var names = all.Select(s => s.Categories).FirstOrDefault(c => c.Count > 0) ?? [];
        var labels = Enumerable.Range(0, n).Select(i => i < names.Count ? names[i] : (i + 1).ToString(culture)).ToList();
        var categoryStyle = Style(categoryAxis?.TxPr, "categoryAxis");
        var value = new AxisLayout { Axis = valueAxis, Groups = groups, Style = Style(valueAxis.TxPr, "valueAxis") };
        var showCategories = categoryAxis is { Deleted: false, TickLabelPosition: not "none" };

        double cx, cy, radius;
        if (ManualPlot() is { } manual)
        {
            (cx, cy, radius) = (manual.X + manual.W / 2, manual.Y + manual.H / 2, Math.Max(1, Math.Min(manual.W, manual.H) / 2));
        }
        else
        {
            double marginX = 0, marginY = 0;
            if (showCategories)
            {
                var s = categoryStyle.Size;
                marginX = labels.Max(l => Width(l, categoryStyle)) + 0.3056 * s - 3.005;
                marginY = LineHeight(categoryStyle) + 0.3127 * s - 5.248;
            }
            // Value labels centered on the outermost polygon reach half a line minus 6 pt past the plot area.
            if (value.HasLabels)
                marginY = Math.Max(marginY, LineHeight(value.Style) / 2 - 6);
            (cx, cy) = (outer.X + outer.W / 2, outer.Y + outer.H / 2);
            radius = Math.Max(1, Math.Min(outer.W / 2 - marginX, outer.H / 2 - marginY));
        }
        ScaleValueAxis(value, radius);
        (value.Start, value.End) = valueAxis.Reversed ? (radius, 0.0) : (0.0, radius);
        var direction = categoryAxis?.Reversed == true ? -1 : 1;
        double Angle(int i) => direction * 2 * Math.PI * i / n;
        (double X, double Y) At(int i, double r) => (cx + r * Math.Sin(Angle(i)), cy - r * Math.Cos(Angle(i)));
        double Radius(double v) => value.Map(Math.Clamp(v, value.Scale.Min, value.Scale.Max));

        var svg = new StringBuilder();
        svg.Append(Area(model.PlotArea?.Element(Ns.C + "spPr"), new Box(cx - radius, cy - radius, 2 * radius, 2 * radius), snap: false));
        foreach (var (element, minor) in new[] { (valueAxis.MinorGridlines, true), (valueAxis.MajorGridlines, false) })
        {
            if (element == null)
                continue;
            var outline = GridOutline(element);
            foreach (var tick in minor ? value.Scale.MinorTicks() : value.Scale.Ticks())
            {
                var r = value.Map(tick);
                if (r > 0.01)
                    svg.Append(Polyline(Enumerable.Range(0, n).Select(i => At(i, r)).ToList(), outline, closed: true));
            }
        }

        foreach (var group in groups)
        {
            foreach (var series in group.Series)
            {
                var outline = SeriesOutline(group, series);
                if (group.IsFilledRadar)
                {
                    // Filled radars draw blank values at the center.
                    var corners = Enumerable.Range(0, n).Select(i => At(i, Radius(series.Values.ElementAtOrDefault(i) ?? value.Scale.Min))).ToList();
                    var d = new StringBuilder();
                    for (var i = 0; i < corners.Count; i++)
                        d.Append(i == 0 ? 'M' : 'L').Append(Px(corners[i].X)).Append(' ').Append(Px(corners[i].Y));
                    svg.Append($"<path d=\"{d}Z\"{paint.Fill(PointFill(group, series, -1), 0, 0, defs)}{paint.Stroke(outline, 0, 0, defs)}/>");
                    continue;
                }
                var points = Enumerable.Range(0, n).Select(i => series.Values.ElementAtOrDefault(i) is { } v ? At(i, Radius(v)) : ((double X, double Y)?)null).ToList();
                if (outline is { Visible: true })
                {
                    if (points.All(p => p.HasValue))
                    {
                        svg.Append(Polyline(points.Select(p => p!.Value).ToList(), outline, closed: true));
                    }
                    else
                    {
                        // Lines stop at blank values; the last point connects to the first.
                        foreach (var run in Runs(points.Append(points[0]).ToList()))
                            svg.Append(Polyline(run, outline, closed: false));
                    }
                }
                if (Marker(group, series) is { } marker)
                {
                    for (var i = 0; i < points.Count; i++)
                    {
                        if (points[i] is { } p)
                            svg.Append(DrawMarker(marker, series.Index, p.X, p.Y, PointFill(group, series, i), outline));
                    }
                }
                for (var i = 0; i < points.Count; i++)
                {
                    if (points[i] is { } p && series.Values.ElementAtOrDefault(i) is { } raw && DataLabel(group, series, i) is { } spec)
                        LabelAtPoint(spec, LabelText(spec, series, i, raw, 0), p.X, p.Y, spec.Position ?? "t");
                }
            }
        }

        if (!valueAxis.Deleted && AxisOutline(valueAxis) is { Visible: true } spokes)
        {
            for (var i = 0; i < n; i++)
                svg.Append(Polyline([(cx, cy), At(i, radius)], spokes, closed: false));
        }

        // Value labels right-aligned left of the first spoke (measured gap: 1.14 s − 1.25 pt), centered on the ticks.
        if (value.HasLabels)
        {
            var style = value.Style;
            var gap = 1.14 * style.Size - 1.25;
            var ticks = value.Scale.Ticks().ToList();
            for (var i = 0; i < ticks.Count && i < value.Labels.Count; i++)
            {
                var label = value.Labels[i];
                var (x, y) = At(0, value.Map(ticks[i]));
                svg.Append(text.Label(label, style, x - gap - Width(label, style), CenteredBaseline(style, y)));
            }
        }

        // Category labels 6 pt beyond their spoke: left- or right-aligned and vertically centered at the sides,
        // centered above the top and below the bottom spoke.
        if (showCategories)
        {
            const double Gap = 6, VerticalGap = 6.4;
            var style = categoryStyle;
            var shift = LabelCenterShift * style.Size;
            for (var i = 0; i < n; i++)
            {
                var label = labels[i];
                var w = Width(label, style);
                var (sin, cos) = (Math.Sin(Angle(i)), Math.Cos(Angle(i)));
                var (px, py) = At(i, radius + Gap);
                if (Math.Abs(sin) < 1e-6)
                {
                    var (_, vy) = At(i, radius);
                    var baseline = cos > 0 ? vy - VerticalGap - Descent(style) - shift : vy + VerticalGap + Ascent(style) - shift;
                    svg.Append(text.Label(label, style, px - w / 2, baseline));
                }
                else
                {
                    svg.Append(text.Label(label, style, sin > 0 ? px : px - w, CenteredBaseline(style, py)));
                }
            }
        }
        return svg.ToString();
    }

    /// <summary>A polyline (or polygon) with its vertices on the pixel grid like PowerPoint's chart lines.</summary>
    string Polyline(List<(double X, double Y)> points, Outline outline, bool closed)
    {
        if (points.Count < 2 || outline is not { Visible: true })
            return "";
        var d = new StringBuilder();
        for (var i = 0; i < points.Count; i++)
        {
            var p = SnapPoint(points[i], outline);
            d.Append(i == 0 ? 'M' : 'L').Append(Px(p.X)).Append(' ').Append(Px(p.Y));
        }
        if (closed)
            d.Append('Z');
        return $"<path d=\"{d}\" fill=\"none\"{paint.Stroke(outline, 0, 0, defs)}/>";
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
        // With data labels PowerPoint keeps a margin around the pie for them.
        var labelled = rings.SelectMany(r => Enumerable.Range(0, r.Values.Count).Select(i => DataLabel(group, r, i))).FirstOrDefault(l => l != null);
        var margin = labelled != null && ManualPlot() == null ? PieLabelMargin * labelled.Style.Size : 0;
        var radius = Math.Max(1, Math.Min(plot.W, plot.H) / 2 - margin) / (1 + maxExplosion);
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
                if (DataLabel(group, series, i) is { } spec)
                {
                    // Doughnut labels sit in the middle of the ring; pie labels at half the radius (ctr), near the
                    // edge inside (inEnd, bestFit) or outside it (outEnd).
                    var label = LabelText(spec, series, i, series.Values[i] ?? 0, total);
                    var (w, h) = LabelSize(spec, label);
                    var (dx, dy) = (Math.Sin(mid), -Math.Cos(mid));
                    var distance = doughnut ? (outer + inner) / 2 : (spec.Position ?? "bestFit") switch
                    {
                        "ctr" => outer * 0.5,
                        "outEnd" => outer + LabelOffset + Math.Abs(dx) * w / 2 + Math.Abs(dy) * h / 2,
                        _ => outer * 0.8,
                    };
                    DrawLabel(spec, label, cx + ox + dx * distance - w / 2, cy + oy + dy * distance - h / 2);
                }
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
