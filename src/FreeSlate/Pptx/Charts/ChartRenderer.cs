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
public sealed partial class ChartRenderer(SlideContext slide, SvgPaint paint, TextRenderer text, string chartPart)
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
    const double LegendKeyLeft = 0.275;       // × L from the entry to a square key
    const double LegendText = 0.923;          // × L from the entry to the text after a square key
    // Line keys keep their size at every font size (measured with legends of 7 to 24 pt): a 12 pt key box, the line
    // centered 12.79 pt from the entry's edge and 19.1 pt long, the text at 24.31 pt (points).
    const double LegendLineKey = 12, LegendLineCenter = 12.79, LegendLineHalf = 9.56, LegendLineText = 24.31;
    // × the legend's font size: the largest marker and line width in keys (measured with 7 to 14 pt legends).
    const double LegendMarkerCap = 0.5, LegendLineCap = 0.375;
    // Legends with a dashed line (a series' or trendline's) give every entry a longer key, also of a fixed size (points).
    const double LongKeyLineFrom = 3.05, LongKeyLineTo = 29.31, LongKeyMarker = 17.15, LongKeyText = 31.5;
    const double VerticalLegendPadding = 1.5, VerticalLegendExtra = 3;
    const double TitlePaddingX = 3, TitlePaddingY = 1.5;
    const double TickLength = 3.75;           // 5 px
    const double LabelOffset = 3;             // data labels from the end of a bar or a point
    const double PieLabelMargin = 1.785;      // × s: pies shrink by this on every side when they show data labels
    const double LabelMaxShare = 0.2;         // data labels wrap at this share of the chart's width
    const double LabelKeyText = 1.108;        // × L from a data label's inset to its text after a legend key
    const double PieOutsideGapX = 0.0509, PieOutsideGapY = 0.04;   // × r, outside pie labels off the edge
    const double PieOutsideTurn = 3.05;       // outside pie labels straddle the ray within atan(1 / 3.05) of the vertical
    const double PieInEndMargin = 0.037;      // × r, inEnd pie labels inside the edge
    const double PieInsideMargin = 0.009;     // × r, best fit pie labels' outer corner inside the edge
    const double LeaderShoulder = 5.6;        // the level end of a pie label's leader line
    const double BubbleLabelGap = 5.5;        // labels beside a bubble from its edge
    const double CornerRadius = 9.8;          // rounded chart area corners

    static readonly string[] MarkerCycle = ["diamond", "square", "triangle", "x", "star", "circle", "plus", "dot", "dash"];

    readonly StringBuilder defs = new();
    readonly StringBuilder dataLabels = new();
    ChartModel model = null!;

    /// <summary>Every series' place in plot order (a:chart seriesIdx of chart builds).</summary>
    Dictionary<ChartSeries, int> plotOrder = [];

    /// <summary>The chart group of every series.</summary>
    Dictionary<ChartSeries, ChartGroup> groupOf = [];
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

        /// <summary>The box the plot and the legend keep below where it is not the title's own: Word's automatic one.</summary>
        public Box? Room { get; init; }

        /// <summary>Where the room the title takes ends.</summary>
        public double Bottom => (Room ?? Box).Bottom;
    }

    /// <summary>
    /// A legend entry. Series with a visible line get a wide line key; line-type series without a line (markers only,
    /// stock series) keep the square key's width and show just their marker there (measured).
    /// </summary>
    sealed record LegendEntry(string Text, Fill Fill, Outline? Outline, bool LineKey, XElement? Marker, int MarkerIndex, bool MarkerOnly = false, bool Circle = false)
    {
        /// <summary>The text's lines where a legend of a given size wraps it; the key stands at the first.</summary>
        public IReadOnlyList<string>? Lines { get; init; }
    }

    sealed class LegendLayout
    {
        public required string Position { get; init; }
        public required TextStyle Style { get; init; }
        public required List<LegendEntry> Entries { get; init; }
        public required Box Box { get; set; }
        public bool Overlay { get; init; }
        public XElement? SpPr { get; init; }
        public List<(LegendEntry Entry, double X, double RowCenter)> Placed { get; } = [];

        /// <summary>Legends with a dashed line key (a series' or a trendline's) give every entry a longer key (31.5 pt, measured).</summary>
        public bool LongKeys { get; init; }

        /// <summary>The legend of an Office 2016 chart, laid out and drawn with its own metrics (see ExLegend).</summary>
        public bool Ex { get; init; }
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

        /// <summary>The category labels before they wrap at a vertical axis (see WrapVerticalLabels).</summary>
        public List<string> Names { get; set; } = [];

        /// <summary>Labels cut short with an ellipsis after their last line.</summary>
        public HashSet<int> Cut { get; } = [];
        public double MaxLabelWidth { get; set; }
        public int Categories { get; set; }

        /// <summary>The groups of multi-level categories' outer levels, innermost first: first and end category, name.</summary>
        public List<List<(int Start, int End, string Name)>> Levels { get; set; } = [];

        /// <summary>The alignment of category labels in their slot (c:lblAlgn: ctr, l, r).</summary>
        public string Align { get; set; } = "ctr";

        /// <summary>Every how many categories a date axis labels (its major unit), 0 where it labels as many as fit.</summary>
        public int? DateStep { get; set; }

        /// <summary>A date axis's labels wrapped to fit their slots, all of them shown (see FitLabels).</summary>
        public bool DatesWrapped { get; set; }

        /// <summary>The most lines a label has.</summary>
        public int LabelLines => Labels.Count == 0 ? 1 : Labels.Max(l => l.Count(ch => ch == '\n') + 1);
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

        /// <summary>How the labels of an Office 2016 chart's category axis fit (see ExLabels).</summary>
        public ExLabelMode ExMode { get; set; }

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
        var previous = text.ColorOverride;
        try
        {
            return Draw(widthPt, heightPt, originXPx, originYPx);
        }
        finally
        {
            text.ColorOverride = previous;
        }
    }

    string Draw(double widthPt, double heightPt, double originXPx, double originYPx)
    {
        if (!slide.Presentation.Package.Exists(chartPart))
            return "";
        var document = slide.Presentation.Load(chartPart);
        // Office 2016 charts are drawn in a classic chart frame (catalog: chart.ex).
        if (ChartExModel.Read(document) is { } chartEx)
            document = ClassicFrame(ex = chartEx);
        if (ChartModel.Read(document, slide.Presentation.Culture) is not { } read)
            return "";
        model = read;
        plotOrder = model.Groups.SelectMany(g => g.Series).OrderBy(s => s.Order).Select((s, i) => (Series: s, Place: i))
            .ToDictionary(p => p.Series, p => p.Place);
        groupOf = model.Groups.SelectMany(g => g.Series.Select(s => (Series: s, Group: g))).ToDictionary(p => p.Series, p => p.Group);
        width = widthPt;
        height = heightPt;
        originX = originXPx;
        originY = originYPx;
        culture = slide.Presentation.Culture;
        chartStyle = Related("chartStyle");
        colorStyle = Related("chartColorStyle");
        // What a chart does not format itself looks like its Office 2007 style, also where the chart has style and color
        // parts, which PowerPoint does not draw by (measured: the same look and colors with and without them; catalog:
        // chart.formatting). Office 2016 charts draw by their style part.
        legacy = ex == null ? Legacy(model.ChartSpace, slide.Presentation.AppVersion) : null;
        colors = Related("themeOverride") is { } themeOverride ? slide.Colors.WithTheme(slide.Theme.WithOverride(themeOverride)) : slide.Colors;
        // A chart can keep its own color mapping (measured: PowerPoint honors c:clrMapOvr against the slide's).
        if (model.ChartSpace.Element(Ns.C + "clrMapOvr") is { } mapping)
            colors = colors.WithColorMap(mapping.Attributes().Where(a => !a.IsNamespaceDeclaration).Select(a => KeyValuePair.Create(a.Name.LocalName, a.Value)));
        text.ColorOverride = colors;

        var svg = new StringBuilder();
        // The chart area's corners are rounded unless c:roundedCorners says otherwise (catalog: chart.part; measured:
        // PowerPoint rounds them where the element is missing too, by 9.8 pt at every chart size).
        var rounded = ChartModel.Flag(model.ChartSpace, "roundedCorners", true);
        (Fill? areaFill, List<(XElement, ColorContext)>? areaLine) = legacy != null ? LegacyChartArea() : (null, null);
        svg.Append(Area(model.ChartSpace.Element(Ns.C + "spPr"), new Box(0, 0, width, height), snap: false, rounded ? CornerRadius : 0, areaFill, areaLine));

        var title = BuildTitle();
        var legend = BuildLegend(title);

        // Boundaries the plot area keeps its distance from: the chart edges, the title and the legend.
        double top = 0, bottom = height, left = 0, right = width;
        if (title is { Overlay: false }) top = title.Bottom;
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
        var outer = ex != null
            ? ExOuterPlot(title, legend)
            : new Box(left + PlotGap, top + PlotGap, Math.Max(0, right - left - 2 * PlotGap), Math.Max(0, bottom - top - 2 * PlotGap));

        var body = new StringBuilder();
        var pies = model.Groups.Where(g => g.IsPie).ToList();
        var radars = model.Groups.Where(g => g.IsRadar).ToList();
        var axisGroups = model.Groups.Where(g => !g.IsPie && !g.IsRadar && !g.IsSurface).ToList();
        var surfaces = model.Groups.Where(g => g.IsSurface).ToList();
        if (Chart3D(model.Groups, outer) is { } threeD)
        {
            body.Append(threeD);
            (axisGroups, radars, pies, surfaces) = ([], [], [], []);
            // The legend of a 3D surface lists the bands of its value axis, known only now.
            if (surfaceAxis != null && legend != null)
                legend = BuildLegend(title);
        }
        if (axisGroups.Count > 0)
            body.Append(AxisChart(axisGroups, outer, legend, title));
        if (radars.Count > 0)
            body.Append(Radar(radars, outer));
        foreach (var surface in surfaces)
            body.Append(Surface(surface, outer));
        // A pie of pie or bar of pie lays its two plots out across the whole plot area.
        if (exPlot != null)
            body.Append(exPlot(PlotBox(outer, square: ex?.Layout == "sunburst")));
        else
        {
            foreach (var pie in pies)
                body.Append(Pie(pie, PlotBox(outer, square: pie.Type != "ofPieChart")));
        }

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

    /// <summary>
    /// The layers of a chart element's text: its own, the chart's txPr, the defaults (the Office 2007 style's); Office 2016
    /// charts put their style part's before the chart's (measured: a chart-wide size does not change their text).
    /// </summary>
    List<XElement?> Layers(XElement? own, string styleElement, XElement defaults)
    {
        var chart = DefRPr(model.ChartSpace.Element(Ns.C + "txPr"));
        return ex != null ? [own, StyleLayer(styleElement), chart, defaults] : [own, chart, defaults];
    }

    /// <summary>Text style of a chart element: its own txPr, the chart's txPr, the style part, then 10 pt defaults.</summary>
    TextStyle Style(XElement? txPr, string styleElement, double defaultSize = 10) =>
        text.ChartStyle(Layers(DefRPr(txPr), styleElement, legacy != null
            ? LegacyText(styleElement, defaultSize)
            : new XElement(Ns.A + "defRPr", new XAttribute("sz", (int)(defaultSize * 100)))));

    static double Width(string value, TextStyle style) =>
        style.Font.Data.LayoutWidth(value, style.Size, style.Kerning, style.LetterSpacing);

    static double LineHeight(TextStyle style) => (style.Font.Data.Ascent + style.Font.Data.Descent) * style.Size;

    /// <summary>A line of rich label text as tall as its largest run.</summary>
    static double RichLineHeight(LaidOutLine line) => line.Runs.Count > 0 ? line.Runs.Max(r => LineHeight(r.Style)) : 0;

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

    /// <param name="nudge">Pixels added after snapping: ½ puts a 1 px outline on whole pixels.</param>
    string Rect(Box box, Fill fill, Outline? outline, bool snap, double radius = 0, double nudge = 0)
    {
        double x0, y0, x1, y1;
        if (snap)
        {
            (x0, x1) = (SnapX(box.X) + nudge, SnapX(box.Right) + nudge);
            (y0, y1) = (SnapY(box.Y) + nudge, SnapY(box.Bottom) + nudge);
        }
        else
        {
            (x0, y0, x1, y1) = (box.X * PxPerPt, box.Y * PxPerPt, box.Right * PxPerPt, box.Bottom * PxPerPt);
        }
        var w = x1 - x0;
        var h = y1 - y0;
        if (w <= 0 || h <= 0)
            return "";
        var fillAttributes = fill is GradientFill ? paint.Fill(fill, w, h, defs, offset: (x0, y0)) : paint.Fill(fill, w, h, defs);
        var stroke = paint.Stroke(outline, w, h, defs);
        var corners = radius > 0 ? $" rx=\"{Units.Css(Math.Min(radius * PxPerPt, Math.Min(w, h) / 2))}\"" : "";
        // Picture patterns start at the shape's own origin: the rectangle is drawn there and moved into place.
        if (fill is PictureFill)
            return $"<rect transform=\"translate({Units.Css(x0)} {Units.Css(y0)})\" width=\"{Units.Css(w)}\" height=\"{Units.Css(h)}\"{corners}{fillAttributes}{stroke}/>";
        return $"<rect x=\"{Units.Css(x0)}\" y=\"{Units.Css(y0)}\" width=\"{Units.Css(w)}\" height=\"{Units.Css(h)}\"{corners}{fillAttributes}{stroke}/>";
    }

    /// <summary>
    /// Fill attributes of a shape within the given bounds (points): a gradient spans the shape's own box, as PowerPoint
    /// shades every bar, slice, area, bubble and marker by itself.
    /// </summary>
    string FillOver(Fill fill, (double X, double Y, double W, double H) bounds) =>
        fill is GradientFill
            ? paint.Fill(fill, bounds.W * PxPerPt, bounds.H * PxPerPt, defs, offset: (bounds.X * PxPerPt, bounds.Y * PxPerPt))
            : paint.Fill(fill, bounds.W * PxPerPt, bounds.H * PxPerPt, defs);

    static (double X, double Y, double W, double H) Bounds(IEnumerable<(double X, double Y)> points)
    {
        double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
        foreach (var (x, y) in points)
        {
            (x0, y0, x1, y1) = (Math.Min(x0, x), Math.Min(y0, y), Math.Max(x1, x), Math.Max(y1, y));
        }
        return double.IsInfinity(x0) ? default : (x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>The box of a pie or doughnut slice (see <see cref="Slice"/>): its corners and the circle's extremes it spans.</summary>
    static (double X, double Y, double W, double H) SliceBounds(double cx, double cy, double r, double inner, double start, double sweep)
    {
        if (sweep >= 359.999)
            return (cx - r, cy - r, 2 * r, 2 * r);
        (double X, double Y) Point(double radius, double degrees) =>
            (cx + Math.Sin(degrees * Math.PI / 180) * radius, cy - Math.Cos(degrees * Math.PI / 180) * radius);
        var points = new List<(double X, double Y)> { Point(r, start), Point(r, start + sweep), inner > 0 ? Point(inner, start) : (cx, cy) };
        if (inner > 0)
            points.Add(Point(inner, start + sweep));
        for (var quadrant = Math.Ceiling(start / 90) * 90; quadrant < start + sweep; quadrant += 90)
            points.Add(Point(r, quadrant));
        return Bounds(points);
    }

    /// <summary>A fill of the chart part: picture fills find their image among the chart's own relationships.</summary>
    Fill ReadFill(XElement fill) => FillReader.Read(fill, colors, slide.Images(chartPart), slide.ImageSizes(chartPart));

    /// <summary>
    /// Fill and outline of an element's c:spPr as a rectangle (chart area, plot area, legend, title), over a default fill
    /// and line (the style's).
    /// </summary>
    string Area(XElement? spPr, Box box, bool snap, double radius = 0, Fill? defaultFill = null, List<(XElement, ColorContext)>? defaultLine = null)
    {
        var fill = FillReader.FindFill(spPr) is { } f ? ReadFill(f) : defaultFill ?? Fill.None;
        var layers = new List<(XElement, ColorContext)>(defaultLine ?? []);
        if (spPr?.Element(Ns.A + "ln") is { } ln)
            layers.Add((ln, colors));
        var outline = layers.Count > 0 ? OutlineReader.Merge(layers) : null;
        if (fill is NoFill && outline is not { Visible: true })
            return "";
        return Rect(box, fill, outline, snap, radius);
    }

    /// <summary>A horizontal or vertical line of the plot area, snapped like PowerPoint draws thin lines.</summary>
    string GridLine(Outline? outline, double x1, double y1, double x2, double y2)
    {
        if (outline is not { Visible: true } || outline.Fill is not SolidFill solid)
            return outline is { Visible: true } ? $"<path d=\"M{Px(x1)} {Px(y1)}L{Px(x2)} {Px(y2)}\" fill=\"none\"{paint.Stroke(outline, 0, 0, defs)}/>" : "";
        var thickness = Math.Max(1, Math.Round(Units.ToPx(outline.WidthEmu)));
        if (outline.Dash != "solid")
        {
            // Dashed lines keep their dashes: a stroke along the snapped center line, as wide as the snapped line, from
            // a whole pixel, so that the dashes (whole multiples of the width) end on pixel edges like PowerPoint's.
            var line = outline with { WidthEmu = thickness * 9525 };
            if (Math.Abs(y1 - y2) < 1e-9)
            {
                var center = Math.Round(originY + y1 * PxPerPt - thickness / 2) - originY + thickness / 2;
                return $"<path d=\"M{Units.Css(SnapX(x1))} {Units.Css(center)}H{Px(x2)}\" fill=\"none\"{paint.Stroke(line, 0, 0, defs)}/>";
            }
            var middle = Math.Round(originX + x1 * PxPerPt - thickness / 2) - originX + thickness / 2;
            return $"<path d=\"M{Units.Css(middle)} {Units.Css(SnapY(y1))}V{Px(y2)}\" fill=\"none\"{paint.Stroke(line, 0, 0, defs)}/>";
        }
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
        var layers = Layers(DefRPr(txPr), "title", legacy != null
            ? LegacyText("title", 18)
            : new XElement(Ns.A + "defRPr", new XAttribute("sz", "1400"), new XAttribute("b", "1")));
        if (legacy != null && element == null && chartStyle != null)
        {
            // A title PowerPoint makes up from the series' name (no c:title) takes the style part's look (measured:
            // generated/charts, 18.62 pt regular in tx1 at 65 %), unlike a c:title without formatting of its own.
            layers.Insert(2, StyleLayer("title"));
        }
        else if (legacy != null && DefRPr(model.ChartSpace.Element(Ns.C + "txPr")).Int("sz") is { } chartSize)
        {
            // Office 2007 titles are 1.2 times the chart's text size where it has one (measured: 16.8 pt with 14 pt).
            layers.Insert(1, new XElement(Ns.A + "defRPr", new XAttribute("sz", ((int)Math.Round(chartSize * 1.2)).ToString(CultureInfo.InvariantCulture))));
        }
        // Office 2016 charts give their title the chart's width less its margins (measured: 115 pt in a 130 pt chart).
        var maxWidth = ex != null ? width - 2 * ExMargin : width * 0.8;
        LaidOutText layout;
        TextStyle style;
        if (element?.Element(Ns.C + "tx")?.Element(Ns.C + "rich") is { } rich)
        {
            if (legacy != null)
                rich = LegacyTitleText(rich);
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
        var box = new Box((width - w) / 2, ex != null ? ExTitleTop - TitlePaddingY : EdgeGap, w, h);
        var manual = ManualPosition(element, w, h);
        // Word draws a manually placed title where its layout says but lays the plot and the legend out below the
        // automatic title unless it overlays them (scratch/triage/2026-10-14_agents/charts: libreoffice/
        // extras-ooxmlexport/tdf134235 plot top 11 pt below the automatic box, powertools/CU007, CU012).
        var word = manual != null && slide.Presentation.Word;
        return new TitleLayout
        {
            Text = block,
            Box = manual ?? box,
            Overlay = ChartModel.Flag(element, "overlay", false) || manual != null && !word,
            SpPr = element?.Element(Ns.C + "spPr"),
            Room = word ? box : null,
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

    /// <summary>
    /// What a data label shows and where (c:dLbl of the point, else c:dLbls of the series, else of the group): its parts,
    /// separator, position and text, the widest its lines may be, the legend key's fill (c:showLegendKey), the point
    /// label's manual position (c:layout/c:manualLayout) and, for pies, its leader line (c:showLeaderLines); a point
    /// label's own text (c:tx) replaces the parts, a cell range's text (c15:datalabelsRange) leads them.
    /// </summary>
    sealed record LabelSpec(bool Value, bool Category, bool Series, bool Percent, bool BubbleSize, string Separator, string? Position,
        string? FormatCode, TextStyle Style, XElement? SpPr, (double L, double T, double R, double B) Insets, double MaxWidth,
        Fill? Key, ManualOffset? Manual, Outline? Leader, string? Text = null, string? RangeText = null, int Ser = -1, int Pt = -1,
        LaidOutText? Rich = null);

    /// <summary>A label's manual position: offsets from its automatic place in fractions of the chart, or edge positions.</summary>
    sealed record ManualOffset(double? X, double? Y, bool EdgeX, bool EdgeY);

    LabelSpec? DataLabel(ChartGroup group, ChartSeries series, int point)
    {
        var seriesLabels = series.Element.Element(Ns.C + "dLbls");
        var pointLabel = seriesLabels?.Elements(Ns.C + "dLbl").FirstOrDefault(d => d.Element(Ns.C + "idx").Int("val") == point);
        var layers = new[] { pointLabel, seriesLabels, group.Element.Element(Ns.C + "dLbls") }.Where(l => l != null).Cast<XElement>().ToList();
        if (layers.Count == 0 || ChartModel.Flag(layers[0], "delete", false))
            return null;
        bool Show(string name) => layers.FirstOrDefault(l => l.Element(Ns.C + name) != null) is { } owner && ChartModel.Flag(owner, name, false);
        var spec = (Value: Show("showVal"), Category: Show("showCatName"), Series: Show("showSerName"), Percent: Show("showPercent"),
            Bubble: group.IsBubble && Show("showBubbleSize"));
        XElement? Child(string name) => layers.Select(l => l.Element(Ns.C + name)).FirstOrDefault(e => e != null);
        var numFmt = Child("numFmt");
        var formatCode = numFmt?.Bool("sourceLinked") == true ? null : numFmt.Str("formatCode");
        // Text from a range of cells (c15:datalabelsRange, shown by c15:showDataLabelsRange; the cache holds every
        // point's text).
        var rangeShown = layers.Select(l => l.Element(Ns.C + "extLst")?.Descendants().FirstOrDefault(e => e.Name.LocalName == "showDataLabelsRange"))
            .FirstOrDefault(e => e != null) is { } range && range.Attribute("val")?.Value is not ("0" or "false");
        var rangeCache = series.Element.Element(Ns.C + "extLst")?.Descendants().FirstOrDefault(e => e.Name.LocalName == "dlblRangeCache")?
            .Elements(Ns.C + "pt").FirstOrDefault(pt => pt.Attribute("idx")?.Value == point.ToString(CultureInfo.InvariantCulture))?.Element(Ns.C + "v")?.Value;
        var rangeText = rangeShown ? rangeCache : null;
        // The point label's own text: rich text whose fields show the point's current values, or a cell's text.
        string? own = null;
        XElement? rich = null;
        if (pointLabel?.Element(Ns.C + "tx") is { } tx)
        {
            if ((rich = tx.Element(Ns.C + "rich")) != null)
            {
                rich = FieldsFilled(rich, group, series, point, formatCode, rangeCache);
                own = string.Join("\n", rich.Elements(Ns.A + "p").Select(p => string.Concat(p.Elements().Select(e => e.Name.LocalName switch
                {
                    "r" or "fld" => e.Element(Ns.A + "t")?.Value ?? "",
                    "br" => "\n",
                    _ => "",
                }))));
            }
            else if (tx.Element(Ns.C + "strRef")?.Element(Ns.C + "strCache") is { } cache)
                own = string.Concat(cache.Elements(Ns.C + "pt").Select(pt => pt.Element(Ns.C + "v")?.Value));
        }
        if (own == null && rangeText == null && !spec.Value && !spec.Category && !spec.Series && !spec.Percent && !spec.Bubble)
            return null;

        var txPr = rich?.Descendants(Ns.A + "defRPr").Any() == true ? rich : Child("txPr");
        var body = txPr?.Element(Ns.A + "bodyPr");
        double Inset(string name, long fallback) => (body.Long(name) ?? fallback) / Units.EmuPerPoint;
        // The default separator (measured with a German Office): a line break between a percentage and a name shown
        // without the value, else the list separator of the chart's language and a space ("One; 40").
        var separator = Child("separator")?.Value ?? (spec.Percent && !spec.Value ? "\n" : culture.TextInfo.ListSeparator + " ");
        var style = Style(txPr, "dataLabel");
        var insets = (Inset("lIns", 38100), Inset("tIns", 19050), Inset("rIns", 38100), Inset("bIns", 19050));
        var key = Show("showLegendKey") ? KeyFill(group, series, point) : null;
        // Labels wrap at a fifth of the chart's width, box and key included (measured: 86 pt of text in a 460 pt chart), or
        // at the width a label was given (c15:layout, in fractions of the chart; LibreOffice's tdf146487).
        var share = pointLabel?.Element(Ns.C + "extLst")?.Descendants().FirstOrDefault(e => e.Name.LocalName == "layout")?
            .Element(Ns.C + "manualLayout") is { } sized && ChartModel.Number(sized, "w") is { } w and > 0 ? w : LabelMaxShare;
        var maxWidth = Math.Max(1, share * width - insets.Item1 - insets.Item3 - (key != null ? LabelKeyText * LineHeight(style) : 0));
        ManualOffset? manual = null;
        if (pointLabel?.Element(Ns.C + "layout")?.Element(Ns.C + "manualLayout") is { } layout)
            manual = new ManualOffset(ChartModel.Number(layout, "x"), ChartModel.Number(layout, "y"),
                ChartModel.Val(layout, "xMode") == "edge", ChartModel.Val(layout, "yMode") == "edge");
        Outline? leader = null;
        if (group.IsPie && Show("showLeaderLines"))
        {
            var line = layers.Select(l => l.Element(Ns.C + "leaderLines")).FirstOrDefault(e => e != null)?.Element(Ns.C + "spPr")?.Element(Ns.A + "ln");
            leader = line != null
                ? OutlineReader.Merge([(line, colors)])
                : new Outline(9525, new SolidFill(colors.Resolve(new XElement(Ns.A + "schemeClr", new XAttribute("val", "tx1"),
                    new XElement(Ns.A + "lumMod", new XAttribute("val", "35000")), new XElement(Ns.A + "lumOff", new XAttribute("val", "65000"))))),
                    "solid", "flat", "round", 8, "sng");
        }
        // A point label's own rich text with formatted runs keeps their formatting, laid out like a title.
        LaidOutText? richLayout = null;
        if (rich != null && rich.Descendants(Ns.A + "rPr").Any(r => r.HasAttributes || r.HasElements))
            richLayout = text.ChartRich(rich, Layers(DefRPr(Child("txPr")), "dataLabel", new XElement(Ns.A + "defRPr", new XAttribute("sz", "1000"))), maxWidth, wrap: true);
        return new LabelSpec(spec.Value, spec.Category, spec.Series, spec.Percent, spec.Bubble, separator, Child("dLblPos").Str("val"),
            formatCode, style, Child("spPr"), insets, maxWidth,
            key, manual, leader, own, string.IsNullOrEmpty(rangeText) ? null : rangeText, plotOrder.GetValueOrDefault(series), point, richLayout);
    }

    /// <summary>
    /// A data label's own rich text with its fields (a:fld of type VALUE, YVALUE, XVALUE, CATEGORYNAME, SERIESNAME,
    /// PERCENTAGE, BUBBLESIZE, CELLRANGE) showing the point's current values like the label's parts: PowerPoint recomputes
    /// them, the saved text may be a placeholder like "[PERCENTAGE]" (measured: LibreOffice's tdf125444, tdf146756).
    /// </summary>
    XElement FieldsFilled(XElement rich, ChartGroup group, ChartSeries series, int point, string? formatCode, string? rangeText)
    {
        var fields = rich.Descendants(Ns.A + "fld").ToList();
        if (fields.Count == 0)
            return rich;
        var copy = new XElement(rich);
        var value = point >= 0 ? series.Values.ElementAtOrDefault(point) : null;
        var total = series.Values.Sum(v => Math.Abs(v ?? 0));
        foreach (var field in copy.Descendants(Ns.A + "fld"))
        {
            string? current = field.Str("type") switch
            {
                "VALUE" or "YVALUE" => value is { } v ? NumberFormat.Format(v, formatCode ?? series.FormatCode, culture) : null,
                "XVALUE" => group.IsScatter && series.XValues.ElementAtOrDefault(point) is { } x
                    ? NumberFormat.Format(x, "General", culture)
                    : series.Categories.ElementAtOrDefault(point),
                "CATEGORYNAME" => series.Categories.ElementAtOrDefault(point) ?? (point + 1).ToString(culture),
                "SERIESNAME" => series.Name,
                "PERCENTAGE" => value is { } p
                    ? NumberFormat.Format(total == 0 ? 0 : Math.Abs(p) / total, formatCode is { } f && f.Contains('%') ? f : "0%", culture)
                    : null,
                "BUBBLESIZE" => group.IsBubble ? NumberFormat.Format(BubbleSize(series, point), formatCode ?? "General", culture) : null,
                "CELLRANGE" => rangeText,
                _ => null,
            };
            if (current != null && field.Element(Ns.A + "t") is { } t)
                t.Value = current;
        }
        return copy;
    }

    /// <summary>The fill of a data label's legend key: the point's fill, the line's color for series drawn as lines.</summary>
    Fill KeyFill(ChartGroup group, ChartSeries series, int point) =>
        group.HasLines && SeriesOutline(group, series) is { Visible: true, Fill: { } line } ? line : PointFill(group, series, point);

    /// <summary>
    /// The text of a data label in pieces a line may break after: its parts (series name, category, value, percentage,
    /// bubble size) with the separator leading every part after the first, split after spaces; a line break in the
    /// separator is a piece of its own (measured: "Ffffffffffff; 10" breaks into "Ffffffffffff" and "; 10").
    /// </summary>
    List<string> LabelPieces(LabelSpec spec, ChartSeries series, int point, double value, double total)
    {
        var parts = new List<string>();
        if (spec.Text != null) parts.Add(spec.Text);
        else if (spec.RangeText != null) parts.Add(spec.RangeText);
        if (spec.Text == null && spec.Series) parts.Add(series.Name);
        if (spec.Text == null)
        {
            if (spec.Category) parts.Add(series.Categories.ElementAtOrDefault(point) ?? (point + 1).ToString(culture));
            if (spec.Value) parts.Add(NumberFormat.Format(value, spec.FormatCode ?? series.FormatCode, culture));
            if (spec.Percent) parts.Add(NumberFormat.Format(total == 0 ? 0 : Math.Abs(value) / total, spec.FormatCode is { } f && f.Contains('%') ? f : "0%", culture));
            if (spec.BubbleSize) parts.Add(NumberFormat.Format(BubbleSize(series, point), spec.FormatCode ?? "General", culture));
        }
        var pieces = new List<string>();
        for (var i = 0; i < parts.Count; i++)
        {
            var text = i == 0 ? parts[i] : spec.Separator + parts[i];
            var start = 0;
            for (var k = 0; k < text.Length; k++)
            {
                if (text[k] == '\n')
                {
                    if (k > start) pieces.Add(text[start..k]);
                    pieces.Add("\n");
                    start = k + 1;
                }
                else if (text[k] == ' ' && (k + 1 == text.Length || text[k + 1] != ' '))
                {
                    pieces.Add(text[start..(k + 1)]);
                    start = k + 1;
                }
            }
            if (start < text.Length)
                pieces.Add(text[start..]);
        }
        return pieces;
    }

    string LabelText(LabelSpec spec, ChartSeries series, int point, double value, double total) =>
        WrapLabel(LabelPieces(spec, series, point, value, total), spec.Style, spec.MaxWidth);

    /// <summary>
    /// Breaks label pieces into lines no wider than <paramref name="max"/>, filling each line as far as it goes; a piece
    /// wider than a line breaks between characters (measured: "Wwwwwwwwww; 1" reads "Wwwwwwwww" and "w; 1"). Lines drop
    /// their trailing spaces.
    /// </summary>
    static string WrapLabel(IReadOnlyList<string> pieces, TextStyle style, double max)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var piece in pieces)
        {
            if (piece == "\n")
            {
                lines.Add(line.TrimEnd());
                line = "";
                continue;
            }
            if (line.Length > 0 && Width((line + piece).TrimEnd(), style) > max)
            {
                lines.Add(line.TrimEnd());
                line = "";
            }
            line += piece;
            while (line.TrimEnd().Length > 1 && Width(line.TrimEnd(), style) > max)
            {
                var n = 1;
                while (n + 1 < line.Length && Width(line[..(n + 1)], style) <= max)
                    n++;
                lines.Add(line[..n]);
                line = line[n..];
            }
        }
        lines.Add(line.TrimEnd());
        return string.Join("\n", lines);
    }

    /// <summary>The size of a data label's box: its lines, the legend key before them and the text insets.</summary>
    (double W, double H) LabelSize(LabelSpec spec, string value)
    {
        var key = spec.Key != null ? LabelKeyText * LineHeight(spec.Style) : 0;
        if (spec.Rich is { } rich)
            return ((rich.Lines.Count > 0 ? rich.Lines.Max(l => l.Width) : 0) + key + spec.Insets.L + spec.Insets.R,
                rich.Lines.Sum(RichLineHeight) + spec.Insets.T + spec.Insets.B);
        var lines = value.Split('\n');
        return (lines.Max(l => Width(l, spec.Style)) + key + spec.Insets.L + spec.Insets.R,
            lines.Length * LineHeight(spec.Style) + spec.Insets.T + spec.Insets.B);
    }

    /// <summary>
    /// Draws a data label whose box has its top-left corner at (x, y): the legend key, a square of the point's fill
    /// centered in height (measured: as in legends, the text 1.108 line heights after the inset), then the centered lines.
    /// </summary>
    void DrawLabel(LabelSpec spec, string value, double x, double y, int start = -1)
    {
        start = start >= 0 ? start : dataLabels.Length;
        var (w, h) = LabelSize(spec, value);
        dataLabels.Append(Area(spec.SpPr, new Box(x, y, w, h), snap: false));
        var lineHeight = LineHeight(spec.Style);
        var keyWidth = 0.0;
        if (spec.Key is { } fill)
        {
            var size = LegendKey * lineHeight;
            dataLabels.Append(Rect(new Box(x + spec.Insets.L + LegendKeyLeft * lineHeight, y + h / 2 - size / 2, size, size), fill, null, snap: false));
            keyWidth = LabelKeyText * lineHeight;
        }
        var baseline = y + spec.Insets.T + Ascent(spec.Style);
        var left = x + spec.Insets.L + keyWidth;
        var room = w - spec.Insets.L - spec.Insets.R - keyWidth;
        if (spec.Rich is { } rich)
        {
            // Formatted runs: each line as tall as its largest run, centered.
            var top = y + spec.Insets.T;
            foreach (var line in rich.Lines)
            {
                var ascent = line.Runs.Count > 0 ? line.Runs.Max(r => Ascent(r.Style)) : Ascent(spec.Style);
                dataLabels.Append(text.EmitLine(line, left + (room - line.Width) / 2, top + ascent));
                top += RichLineHeight(line);
            }
        }
        else
        {
            foreach (var line in value.Split('\n'))
            {
                var lw = Width(line, spec.Style);
                dataLabels.Append(text.Label(line, spec.Style, left + (room - lw) / 2, baseline));
                baseline += lineHeight;
            }
        }
        // The label (and a pie label's leader line) builds with its point.
        if (spec.Ser >= 0 && dataLabels.Length > start)
        {
            dataLabels.Insert(start, PartStart(spec.Ser, spec.Pt));
            dataLabels.Append("</g>");
        }
    }

    /// <summary>
    /// Places a label at a point: centered, or <paramref name="offset"/> (3 pt) off it to one side (r, l, t, b); a moved
    /// label (c:manualLayout) is offset from there by fractions of the chart, or placed at its edge position.
    /// </summary>
    void LabelAtPoint(LabelSpec spec, string value, double x, double y, string position, double offset = LabelOffset)
    {
        var (w, h) = LabelSize(spec, value);
        var (lx, ly) = position switch
        {
            "l" => (x - offset - w, y - h / 2),
            "t" => (x - w / 2, y - offset - h),
            "b" => (x - w / 2, y + offset),
            "ctr" => (x - w / 2, y - h / 2),
            _ => (x + offset, y - h / 2),
        };
        var box = Moved(spec, new Box(lx, ly, w, h));
        DrawLabel(spec, value, box.X, box.Y);
    }

    /// <summary>
    /// Whether a value lies beyond its axis and so shows no data label (c:showDLblsOverMax 0, as PowerPoint writes it;
    /// measured: 8 and 11 on an axis up to 6 go without labels).
    /// </summary>
    bool Beyond(AxisLayout axis, double value) =>
        model.Chart.Element(Ns.C + "showDLblsOverMax") != null && !ChartModel.Flag(model.Chart, "showDLblsOverMax", true)
        && (value > axis.Scale.Max + 1e-9 * Math.Abs(axis.Scale.Max) || value < axis.Scale.Min - 1e-9 * Math.Abs(axis.Scale.Min));

    /// <summary>A label box moved by its manual position (c:manualLayout: offsets in fractions of the chart, or edges).</summary>
    Box Moved(LabelSpec spec, Box box)
    {
        if (spec.Manual is not { } manual)
            return box;
        var x = manual.X is { } mx ? manual.EdgeX ? mx * width : box.X + mx * width : box.X;
        var y = manual.Y is { } my ? manual.EdgeY ? my * height : box.Y + my * height : box.Y;
        return box with { X = x, Y = y };
    }

    /// <summary>
    /// Places a pie slice's data label (catalog: chart.labels; measured with PowerPoint's label boxes for pies turned in
    /// 5° steps and labels of several widths, local/pieangles, local/piebestfit). On the slice's middle ray: ctr at half
    /// the radius; inEnd where the ray leaves the label 3.7 % of the radius inside the edge; bestFit inside with the
    /// outermost corner just inside the edge, where the label's text fits into the slice (else broken into two lines,
    /// else outside). Outside (outEnd, bestFit that does not fit, moved labels before their offset) the label's inner
    /// edges keep 4 % of the radius vertically and 5.1 % horizontally off the edge's point, the label straddling the ray
    /// near the axes (horizontally within 18° of the vertical by (1 − 3.05 tan φ) / 2 of its width, vertically within
    /// 45° of the horizontal by (1 − cot φ) / 2 of its height). Labels stay inside the chart; a moved label clear of the
    /// pie gets a leader line from the slice's edge, ending in a short level stroke beside it or straight above or below.
    /// </summary>
    void PieLabel(LabelSpec spec, ChartSeries series, int point, double value, double total, double cx, double cy, double r, double start, double sweep)
    {
        var pieces = LabelPieces(spec, series, point, value, total);
        var label = WrapLabel(pieces, spec.Style, spec.MaxWidth);
        var mid = (start + sweep / 2) * Math.PI / 180;
        var (ux, uy) = (Math.Sin(mid), -Math.Cos(mid));
        var (ax, ay) = (Math.Abs(ux), Math.Abs(uy));

        Box OnRay(string text, double distance)
        {
            var (w, h) = LabelSize(spec, text);
            return new Box(cx + ux * distance - w / 2, cy + uy * distance - h / 2, w, h);
        }
        Box Outside(string text)
        {
            var (w, h) = LabelSize(spec, text);
            var tan = ay < 1e-9 ? double.MaxValue : ax / ay;
            var shareX = Math.Max(-0.01, (1 - PieOutsideTurn * tan) / 2);
            var shareY = ax < 1e-9 ? 0 : Math.Max(0, (1 - ay / ax) / 2);
            var innerX = (1 + PieOutsideGapX) * r * ax - w * shareX;
            var innerY = (1 + PieOutsideGapY) * r * ay - h * shareY;
            return new Box(cx + Math.Sign(ux) * (innerX + w / 2) - w / 2, cy + Math.Sign(uy) * (innerY + h / 2) - h / 2, w, h);
        }
        Box? Inside(string text)
        {
            // The corner farthest out on a circle just inside the edge.
            var (w, h) = LabelSize(spec, text);
            var (kx, ky) = (ux < 0 ? -w / 2 : w / 2, uy < 0 ? -h / 2 : h / 2);
            var along = ux * kx + uy * ky;
            var reach = r * (1 - PieInsideMargin);
            var root = along * along - (kx * kx + ky * ky) + reach * reach;
            if (root < 0)
                return null;
            var box = sweep < 180 ? Balanced(text, w, h, reach) : OnRay(text, -along + Math.Sqrt(root));
            return InSlice(new Box(box.X + spec.Insets.L, box.Y + spec.Insets.T, box.W - spec.Insets.L - spec.Insets.R,
                box.H - spec.Insets.T - spec.Insets.B), cx, cy, start, sweep) ? box : null;
        }
        // Off the ray, sideways until the label's lines keep the same distance from both of the slice's radii, and
        // as far out as its corners stay within reach (measured: wide labels of slices turned in 5° steps move
        // towards the horizontal by up to 34 px, matched within 2 px).
        Box Balanced(string text, double w, double h, double reach)
        {
            var lines = text.Split('\n').Select(l => Width(l, spec.Style)).ToArray();
            var lineHeight = (h - spec.Insets.T - spec.Insets.B) / Math.Max(1, lines.Length);
            var (px, py) = (Math.Cos(mid), Math.Sin(mid));
            var (a0, a1) = (start * Math.PI / 180, (start + sweep) * Math.PI / 180);
            // The lines' boxes (insets around the block) for the label's center at (x, y) relative to the pie's center.
            IEnumerable<(double X, double Y)> Corners(double x, double y)
            {
                var top = y - h / 2;
                for (var i = 0; i < lines.Length; i++)
                {
                    var (y0, y1) = (top + (i == 0 ? 0 : spec.Insets.T) + i * lineHeight, top + spec.Insets.T + (i + 1) * lineHeight + (i == lines.Length - 1 ? spec.Insets.B : 0));
                    var (x0, x1) = (x - lines[i] / 2 - spec.Insets.L, x + lines[i] / 2 + spec.Insets.R);
                    yield return (x0, y0);
                    yield return (x1, y0);
                    yield return (x0, y1);
                    yield return (x1, y1);
                }
            }
            double Outmost(double perp)
            {
                var (lo, hi) = (0.0, r);
                for (var i = 0; i < 40; i++)
                {
                    var m = (lo + hi) / 2;
                    var (x, y) = (ux * m + px * perp, uy * m + py * perp);
                    var far = Math.Sqrt(Math.Pow(Math.Abs(x) + w / 2, 2) + Math.Pow(Math.Abs(y) + h / 2, 2));
                    if (far <= reach) lo = m; else hi = m;
                }
                return lo;
            }
            var (low, high) = (-r, r);
            for (var i = 0; i < 40; i++)
            {
                var perp = (low + high) / 2;
                var along = Outmost(perp);
                var corners = Corners(ux * along + px * perp, uy * along + py * perp).ToList();
                // Distances into the slice from the start radius (clockwise side) and from the end radius.
                var fromStart = corners.Min(c => c.X * Math.Cos(a0) + c.Y * Math.Sin(a0));
                var fromEnd = corners.Min(c => -(c.X * Math.Cos(a1) + c.Y * Math.Sin(a1)));
                if (fromStart > fromEnd) high = perp; else low = perp;
            }
            var shift = (low + high) / 2;
            var reachAlong = Outmost(shift);
            return new Box(cx + ux * reachAlong + px * shift - w / 2, cy + uy * reachAlong + py * shift - h / 2, w, h);
        }

        Box box;
        var position = spec.Position ?? "bestFit";
        if (position == "ctr")
            box = OnRay(label, r / 2);
        else if (position == "inEnd")
        {
            var (w, h) = LabelSize(spec, label);
            var leave = Math.Min(ax > 1e-9 ? w / 2 / ax : double.MaxValue, ay > 1e-9 ? h / 2 / ay : double.MaxValue);
            box = OnRay(label, r * (1 - PieInEndMargin) - leave);
        }
        else if (position == "bestFit" && spec.Manual == null)
        {
            var inside = Inside(label);
            if (inside == null && !label.Contains('\n'))
            {
                // A one-line label that does not fit tries two lines, breaking as late as possible.
                for (var k = pieces.Count - 1; k > 0 && inside == null; k--)
                {
                    var broken = string.Concat(pieces.Take(k)).TrimEnd() + "\n" + string.Concat(pieces.Skip(k)).TrimEnd();
                    if ((inside = Inside(broken)) != null)
                        label = broken;
                }
            }
            box = inside ?? Outside(label);
        }
        else
            box = Outside(label);

        var moved = Moved(spec, box);
        moved = moved with { X = Math.Clamp(moved.X, 0, Math.Max(0, width - moved.W)), Y = Math.Clamp(moved.Y, 0, Math.Max(0, height - moved.H)) };
        var labelStart = dataLabels.Length;
        if (spec.Manual != null && spec.Leader is { Visible: true } leader && Distance(moved, cx, cy) > r)
            dataLabels.Append(LeaderLine(leader, moved, cx + ux * r, cy + uy * r, moved.Y + spec.Insets.T + Ascent(spec.Style)));
        DrawLabel(spec, label, moved.X, moved.Y, labelStart);
    }

    /// <summary>Whether every corner of a box lies within a pie slice's angles (from the top, clockwise), give or take 1°.</summary>
    static bool InSlice(Box box, double cx, double cy, double start, double sweep)
    {
        if (sweep >= 359.99)
            return true;
        foreach (var (x, y) in new[] { (box.X, box.Y), (box.Right, box.Y), (box.X, box.Bottom), (box.Right, box.Bottom) })
        {
            var angle = Math.Atan2(x - cx, cy - y) * 180 / Math.PI;
            var into = ((angle - start) % 360 + 360) % 360;
            if (into > sweep + 1 && into < 359)
                return false;
        }
        return true;
    }

    /// <summary>The distance from a point to the nearest point of a box (0 inside it).</summary>
    static double Distance(Box box, double x, double y) =>
        Math.Sqrt(Math.Pow(Math.Max(0, Math.Max(box.X - x, x - box.Right)), 2) + Math.Pow(Math.Max(0, Math.Max(box.Y - y, y - box.Bottom)), 2));

    /// <summary>
    /// A pie label's leader line from the slice's edge point: straight up or down to a label above or below it, else to
    /// a level stroke of 5.6 pt ending at the label's near side at its first baseline (measured).
    /// </summary>
    string LeaderLine(Outline outline, Box box, double px, double py, double attach)
    {
        var points = new List<(double X, double Y)> { (px, py) };
        if (px >= box.X && px <= box.Right)
            points.Add((px, box.Y > py ? box.Y : box.Bottom));
        else
        {
            var (end, shoulder) = box.X > px ? (box.X, box.X - LeaderShoulder) : (box.Right, box.Right + LeaderShoulder);
            if (box.X > px ? shoulder > px : shoulder < px)
                points.Add((shoulder, attach));
            points.Add((end, attach));
        }
        var d = string.Concat(points.Select((p, i) => $"{(i == 0 ? 'M' : 'L')}{Px(p.X)} {Px(p.Y)}"));
        return $"<path d=\"{d}\" fill=\"none\"{paint.Stroke(outline, 0, 0, defs)}/>";
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
        var layers = Layers(DefRPr(element.Element(Ns.C + "txPr")), "axisTitle", new XElement(Ns.A + "defRPr", new XAttribute("sz", "1000"), new XAttribute("b", "1")));
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
            if (group.IsSurface)
            {
                // Surface charts list their value bands; a wireframe's keys are hollow squares in the bands' lines
                // (measured with generated/charts-3d).
                var scale = SurfaceScale(group);
                var bands = Bands(scale);
                var wireframe = ChartModel.Flag(group.Element, "wireframe", false);
                for (var b = 0; b < bands.Count; b++, index++)
                {
                    if (!deleted.Contains(index))
                        entries.Add(wireframe && BandLine(group, b) is { } line
                            ? new LegendEntry(BandName(group, bands[b]), Fill.None, line, false, null, 0)
                            : new LegendEntry(BandName(group, bands[b]), BandFill(group, b), null, false, null, 0));
                }
                continue;
            }
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
            // Clustered bars, and stacked columns and areas beside a legend at the side, list their series in their
            // visual order (measured: LibreOffice's stacked-non-stacked-mix-y-axis, Series 3, 2, 1 at the right).
            var side = (ChartModel.Val(element, "legendPos") ?? "r") is "r" or "l";
            if (group.IsBar && group.Horizontal && !group.Stacked || side && group.Stacked && (group.IsBar && !group.Horizontal || group.IsArea))
                list.Reverse();
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
        // Trendlines follow the series.
        foreach (var group in model.Groups)
        {
            foreach (var series in group.Series)
            {
                foreach (var trendline in series.Element.Elements(Ns.C + "trendline"))
                {
                    if (!deleted.Contains(index++))
                        entries.Add(new LegendEntry(TrendName(trendline, series), Fill.None, TrendOutline(trendline, group, series), true, null, series.Index));
                }
            }
        }
        if (entries.Count == 0)
            return null;
        if (ex != null)
            return ExLegend(element, style, entries, title);

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
            LongKeys = entries.Any(e => e.LineKey && e.Outline is { Visible: true, Dash: not "solid" }),
        };

        // An entry is [key/2][key][key/2][text]; square keys are 0.45 L wide, line keys 12 pt.
        double Key(LegendEntry e) => layout.LongKeys ? LongKeyText / 2 : e.LineKey ? LegendLineKey : LegendKey * lineHeight;
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
            var y = position == "b" ? height - EdgeGap - h : (title is { Overlay: false } ? title.Bottom : 0) + EdgeGap;
            box = new Box((width - w) / 2, y, w, h);
        }
        else
        {
            var w = widths.Max() + 2 * VerticalLegendPadding + VerticalLegendExtra;
            var h = rowHeight * entries.Count;
            for (var i = 0; i < entries.Count; i++)
                layout.Placed.Add((entries[i], VerticalLegendPadding, rowHeight * (i + 0.5)));
            var areaTop = (title is { Overlay: false } ? title.Bottom : 0) + EdgeGap;
            var areaBottom = height - EdgeGap;
            var x = position == "l" ? EdgeGap : width - EdgeGap - w;
            var y = position == "tr" ? areaTop : areaTop + (areaBottom - areaTop - h) / 2;
            box = new Box(x, y, w, h);
        }
        layout.Box = ManualPosition(element, box.W, box.H) ?? box;
        if (position is not ("t" or "b") && element.Element(Ns.C + "layout")?.Element(Ns.C + "manualLayout") is { } sized
            && (ChartModel.Number(sized, "w") != null || ChartModel.Number(sized, "h") != null))
            FitLegend(layout, lineHeight, Key);
        return layout;
    }

    /// <summary>
    /// The entries of a legend at the side given a size of its own (c:manualLayout w, h): the text wraps at the legend's
    /// width less 4 pt and the key's room; as many entries show as slots of the tallest entry's height the legend's
    /// height begins, k = min(n, ceil(height / tallest)), each in an equal slot of height / k, the tallest centered in
    /// it and the others at the same top; entries are centered across (measured through COM with legends 90 to 280 pt
    /// tall and 90 to 170 pt wide; LibreOffice's tdf122765 shows 7 of 9 entries).
    /// </summary>
    void FitLegend(LegendLayout layout, double lineHeight, Func<LegendEntry, double> key)
    {
        var entries = layout.Entries;
        if (entries.Count == 0)
            return;
        var (w, h) = (layout.Box.W, layout.Box.H);
        var wrapped = entries.Select(e => WrapWords(e.Text, layout.Style, Math.Max(1, w - 4 - 2 * key(e)))).ToList();
        var tallest = wrapped.Max(lines => Math.Max(1, lines.Count)) * lineHeight;
        var shown = Math.Clamp((int)Math.Ceiling(h / tallest - 1e-9), 1, entries.Count);
        var slot = h / shown;
        var entryWidth = Math.Min(w - 4, Enumerable.Range(0, shown).Max(i => 2 * key(entries[i]) + wrapped[i].DefaultIfEmpty("").Max(line => Width(line, layout.Style))));
        layout.Placed.Clear();
        for (var i = 0; i < shown; i++)
            layout.Placed.Add((entries[i] with { Lines = wrapped[i] }, (w - entryWidth) / 2, i * slot + (slot - tallest) / 2 + lineHeight / 2));
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
            if (legend.Ex)
            {
                svg.Append(ExLegendEntry(entry, style, left, center));
                continue;
            }
            var baseline = CenteredBaseline(style, center);
            // Key geometry in points from the entry's left edge: line keys, square keys (in line heights), or the
            // longer keys of legends with a dashed line.
            double lineFrom = LegendLineCenter - LegendLineHalf, lineTo = LegendLineCenter + LegendLineHalf, markerAt, squareAt, textAt;
            if (legend.LongKeys)
                (lineFrom, lineTo, markerAt, squareAt, textAt) = (LongKeyLineFrom, LongKeyLineTo,
                    entry.LineKey ? (LongKeyLineFrom + LongKeyLineTo) / 2 : LongKeyMarker, LongKeyText / 2 - LegendKey / 2 * lineHeight, LongKeyText);
            else if (entry.LineKey)
                (markerAt, squareAt, textAt) = (LegendLineCenter, LegendKeyLeft * lineHeight, LegendLineText);
            else
                (markerAt, squareAt, textAt) = ((LegendKeyLeft + LegendKey / 2) * lineHeight, LegendKeyLeft * lineHeight, LegendText * lineHeight);
            // Keys draw lines and markers no larger than the legend's font allows.
            var keyLine = entry.Outline is { } entryLine ? entryLine with { WidthEmu = Math.Min(entryLine.WidthEmu, LegendLineCap * style.Size * 12700) } : null;
            if (entry.LineKey)
            {
                if (keyLine is { Visible: true } line)
                {
                    var (a, b) = (SnapPoint((left + lineFrom, center), line), SnapPoint((left + lineTo, center), line));
                    svg.Append($"<path d=\"M{Px(a.X)} {Px(a.Y)}L{Px(b.X)} {Px(b.Y)}\" fill=\"none\"{paint.Stroke(line, 0, 0, defs)}/>");
                }
                if (entry.Marker != null)
                    svg.Append(DrawMarker(entry.Marker, entry.MarkerIndex, left + markerAt, center, entry.Fill, entry.Outline, style.Size));
            }
            else if (entry.MarkerOnly)
            {
                if (entry.Marker != null)
                    svg.Append(DrawMarker(entry.Marker, entry.MarkerIndex, left + markerAt, center, entry.Fill, entry.Outline, style.Size));
            }
            else
            {
                var key = LegendKey * lineHeight;
                var outline = entry.Outline is { Visible: true } o ? o with { WidthEmu = Math.Min(o.WidthEmu, 19050) } : null;
                if (entry.Circle)
                {
                    // Bubble series show a round key.
                    var (kx, r) = (left + squareAt + key / 2, key / 2);
                    svg.Append($"<circle cx=\"{Px(kx)}\" cy=\"{Px(center)}\" r=\"{Px(r)}\"{FillOver(entry.Fill, (kx - r, center - r, key, key))}{paint.Stroke(outline, key, key, defs)}/>");
                }
                else
                {
                    // Hollow keys (wireframe surfaces) draw their outline on whole pixels like PowerPoint's.
                    var hollow = entry.Fill is NoFill;
                    svg.Append(Rect(new Box(left + squareAt, center - key / 2, key, key), entry.Fill, outline, snap: hollow, nudge: hollow ? 0.5 : 0));
                }
            }
            if (entry.Lines is { Count: > 1 } lines)
            {
                for (var k = 0; k < lines.Count; k++)
                    svg.Append(text.Label(lines[k], style, left + textAt, baseline + k * lineHeight));
            }
            else
                svg.Append(text.Label(entry.Text, style, left + textAt, baseline));
        }
        return svg.ToString();
    }

    // ---------------------------------------------------------------- series colors

    /// <summary>
    /// The automatic color of series or point <paramref name="index"/> of <paramref name="count"/> (default: the chart's
    /// series): the colors of the chart's Office 2007 style; in Office 2016 charts their color style part's, else the
    /// accents.
    /// </summary>
    RgbColor AutoColor(int index, int? count = null)
    {
        if (legacy != null)
            return LegacyColor(index, count ?? AllSeriesCount);
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
            return ReadFill(own);
        // A fill of the series' own colors its points without one, even where the colors vary by point (measured:
        // LibreOffice's tdf150176 and tdf146756, the first slice in the series' bg2).
        if (FillReader.FindFill(series.SpPr) is { } fill)
            return ReadFill(fill);
        var color = AutoPointColor(group, series, point);
        return legacy != null ? LegacyFill(color) : new SolidFill(color);
    }

    /// <summary>The automatic color of a data point (−1: of the series): by point where the group varies colors, else the series'.</summary>
    RgbColor AutoPointColor(ChartGroup group, ChartSeries series, int point) =>
        point >= 0 && group.VaryColors && group.Series.Count == 1 ? AutoColor(point, series.Values.Count) : AutoColor(series.Index);

    /// <summary>
    /// How a picture fills a point (c:pictureOptions of the point, else of the series): stretched (the default),
    /// stacked at its proportions, or stacked one per c:pictureStackUnit.
    /// </summary>
    static (string Format, double Unit) PictureOptions(ChartSeries series, int point)
    {
        var options = (series.Points.TryGetValue(point, out var dPt) ? dPt.Element(Ns.C + "pictureOptions") : null)
                      ?? series.Element.Element(Ns.C + "pictureOptions");
        return (ChartModel.Val(options, "pictureFormat") ?? "stretch", Math.Max(1e-9, ChartModel.Number(options, "pictureStackUnit") ?? 1));
    }

    /// <summary>A bar filled with copies of its picture, a tile long each along the value direction from the base.</summary>
    string StackedPicture(Box box, PictureFill picture, Outline? outline, bool horizontal, double basePosition, double tile)
    {
        var id = paint.NewId("stack");
        var (x0, y0, w, h) = (box.X * PxPerPt, box.Y * PxPerPt, box.W * PxPerPt, box.H * PxPerPt);
        var t = tile * PxPerPt;
        var origin = basePosition * PxPerPt;
        var (px, py, pw, ph) = horizontal ? (origin, y0, t, h) : (x0, origin, w, t);
        defs.Append($"<pattern id=\"{id}\" patternUnits=\"userSpaceOnUse\" x=\"{Units.Css(px)}\" y=\"{Units.Css(py)}\" width=\"{Units.Css(pw)}\" height=\"{Units.Css(ph)}\">" +
                    $"<image href=\"{picture.Image}\" width=\"{Units.Css(pw)}\" height=\"{Units.Css(ph)}\" preserveAspectRatio=\"none\"/></pattern>");
        return $"<rect x=\"{Units.Css(x0)}\" y=\"{Units.Css(y0)}\" width=\"{Units.Css(w)}\" height=\"{Units.Css(h)}\" fill=\"url(#{id})\"{paint.Stroke(outline, w, h, defs)}/>";
    }

    /// <summary>
    /// The fill and line of a negative bar of a series that inverts them (c:invertIfNegative, catalog: chart.barColumn):
    /// the fill of c14:invertSolidFillFmt, else white, and a black hairline where the series draws no line (measured:
    /// white bars with black outlines); null where the point has a fill of its own or the series does not invert.
    /// </summary>
    (Fill Fill, Outline? Line)? Inverted(ChartSeries series, int point)
    {
        if (series.Element.Element(Ns.C + "invertIfNegative")?.Attribute("val")?.Value is not ("1" or "true"))
            return null;
        if (series.Points.TryGetValue(point, out var dPt) && FillReader.FindFill(dPt.Element(Ns.C + "spPr")) != null)
            return null;
        var format = series.Element.Descendants().FirstOrDefault(e => e.Name.LocalName == "invertSolidFillFmt")?.Elements().FirstOrDefault();
        var fill = FillReader.FindFill(format) is { } own ? ReadFill(own) : new SolidFill(RgbColor.White);
        var line = PointOutline(series, point) is { Visible: true } visible
            ? visible
            : new Outline(9525, new SolidFill(RgbColor.Black), "solid", "flat", "miter", 8, "sng");
        return (fill, line);
    }

    /// <summary>
    /// The edge of a bar, area, slice or bubble: the point's own a:ln, else the series', over the automatic edge of the
    /// Office 2007 style of a chart without style part.
    /// </summary>
    Outline? PointOutline(ChartSeries series, int point)
    {
        var layers = legacy != null && groupOf.TryGetValue(series, out var group) ? LegacyEdge(group, series, point) : [];
        if (point >= 0 && series.Points.TryGetValue(point, out var dPt) && dPt.Element(Ns.C + "spPr")?.Element(Ns.A + "ln") is { } own)
            layers.Add((own, colors));
        else if (series.SpPr?.Element(Ns.A + "ln") is { } ln)
            layers.Add((ln, colors));
        return layers.Count > 0 ? OutlineReader.Merge(layers) : null;
    }

    /// <summary>
    /// The line of a line or scatter series: its own a:ln over an automatic line (2.25 pt; in charts without style
    /// part the Office 2007 style's); the edge of other series.
    /// </summary>
    Outline? SeriesOutline(ChartGroup group, ChartSeries series)
    {
        var own = series.SpPr?.Element(Ns.A + "ln");
        if (!group.HasLines)
            return PointOutline(series, -1);
        var automatic = legacy != null
            ? LegacySeriesLine(AutoColor(series.Index))
            : new XElement(Ns.A + "ln", new XAttribute("w", "28575"), new XAttribute("cap", "rnd"),
                new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", AutoColor(series.Index).ToHex().TrimStart('#')))),
                new XElement(Ns.A + "round"));
        var layers = new List<(XElement, ColorContext)> { (automatic, colors) };
        if (own != null) layers.Add((own, colors));
        return OutlineReader.Merge(layers);
    }

    // ---------------------------------------------------------------- axis charts

    Box PlotBox(Box outer, bool square)
    {
        if (PlotLayout() is { } manual)
            return manual.Box;
        if (!square)
            return outer;
        var size = Math.Min(outer.W, outer.H);
        return new Box(outer.X + (outer.W - size) / 2, outer.Y + (outer.H - size) / 2, size, size);
    }

    /// <summary>A manual plot area layout targeting the inner plot area.</summary>
    Box? ManualPlot() => PlotLayout() is { Inner: true } layout ? layout.Box : null;

    /// <summary>A manual layout of the plot area with its axis labels (layoutTarget outer, the default).</summary>
    Box? ManualOuterPlot() => PlotLayout() is { Inner: false } layout ? layout.Box : null;

    /// <summary>
    /// The plot area's manual layout (c:manualLayout, catalog: chart.titleLegend) in points, and whether it places the
    /// inner plot area (layoutTarget inner) or the plot area with its axis labels (outer, the default): x and y are edge
    /// positions, w and h sizes (wMode, hMode factor) or the far edges (edge), all in fractions of the chart.
    /// </summary>
    (Box Box, bool Inner)? PlotLayout()
    {
        var manual = model.PlotArea?.Element(Ns.C + "layout")?.Element(Ns.C + "manualLayout");
        if (manual == null || ChartModel.Number(manual, "x") is not { } x || ChartModel.Number(manual, "y") is not { } y
            || ChartModel.Number(manual, "w") is not { } w || ChartModel.Number(manual, "h") is not { } h)
            return null;
        if (ChartModel.Val(manual, "wMode") == "edge") w -= x;
        if (ChartModel.Val(manual, "hMode") == "edge") h -= y;
        return (new Box(x * width, y * height, Math.Max(0, w) * width, Math.Max(0, h) * height), ChartModel.Val(manual, "layoutTarget") == "inner");
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
            var withNames = series.FirstOrDefault(s => s.Categories.Count > 0);
            var names = withNames?.Categories ?? [];
            // Numeric categories (dates on a date axis) take the axis's number format, else their own (catalog:
            // chart.axes; measured: serial dates with mmm yy read "Jan 23").
            var categoryFormat = layout.Axis.FormatCode is { } own && own != "General" && !layout.Axis.SourceLinked ? own
                : withNames?.CategoryFormatCode is { } cached && cached != "General" ? cached : null;
            layout.Labels = Enumerable.Range(0, layout.Categories)
                .Select(i => i < names.Count
                    ? categoryFormat != null && double.TryParse(names[i], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                        ? NumberFormat.Format(number, categoryFormat, culture)
                        : names[i]
                    : (i + 1).ToString(culture)).ToList();
            // Multi-level categories (catalog: chart.axes): the outer levels get rows of their own, every name
            // centered over its group; with c:noMultiLvlLbl the names join the label of their group's first category
            // as further lines (measured: "Q1" over "2023").
            var outerLevels = withNames?.OuterCategories ?? [];
            if (outerLevels.Count > 0 && ChartModel.Flag(layout.Axis.Element, "noMultiLvlLbl", false))
                layout.Labels = layout.Labels.Select((label, i) => string.Join("\n",
                    new[] { label }.Concat(outerLevels.Select(level => level.ElementAtOrDefault(i) ?? "").Where(n => n.Length > 0)))).ToList();
            else if (outerLevels.Count > 0)
                layout.Levels = outerLevels.Select(level => CategoryGroups(level, layout.Categories)).ToList();
            layout.Align = ChartModel.Val(layout.Axis.Element, "lblAlgn") ?? "ctr";
            // A date axis labels every major unit (c:majorUnit in c:majorTimeUnit), else as many dates as fit.
            if (layout.Groups.Select(g => g.TimeUnit).FirstOrDefault(u => u != null) is { } baseUnit)
            {
                var major = ChartModel.Number(layout.Axis.Element, "majorUnit");
                var majorUnit = ChartModel.Val(layout.Axis.Element, "majorTimeUnit") ?? baseUnit;
                var perMajor = (baseUnit, majorUnit) switch
                {
                    ("days", "months") => 30.4375,
                    ("days", "years") => 365.25,
                    ("months", "years") => 12,
                    _ => 1,
                };
                layout.DateStep = major is { } m && m > 0 ? Math.Max(1, (int)Math.Round(m * perMajor)) : 0;
            }
            layout.MaxLabelWidth = layout.Labels.Count == 0 ? 0 : layout.Labels.Max(l => l.Split('\n').Max(line => Width(line, layout.Style)));
            layout.Names = layout.Labels;
        }

        // A plot area placed by hand with its labels replaces the automatic one (measured: the labels fit inside it).
        if (ManualOuterPlot() is { } placed)
            outer = placed;

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
            // A horizontal value axis divides the plot area's width before the labels beside it take their room, also
            // where the plot area is placed by hand (measured: bar charts with category labels of a third or half the
            // chart's width; LibreOffice's tdf48041).
            foreach (var layout in axes.Where(a => a.Axis.IsValue))
                ScaleValueAxis(layout, layout.Axis.Horizontal ? plot.W : plot.H, layout.Axis.Horizontal && ex == null ? outer.W : null);
            if (inner != null)
            {
                // A plot area placed by hand still turns category labels that do not fit (measured: −45° in a
                // 150 pt wide plot area with four categories); labels beside a vertical axis wrap at half the chart's
                // width less the value labels' overhangs, and all labels stay inside the chart.
                foreach (var layout in axes.Where(a => a.HasLabels))
                {
                    FitLabels(layout, layout.Axis.Horizontal ? plot.W : plot.H);
                    layout.Inside = LabelsInside(layout, axes);
                    if (!layout.Inside && !layout.Axis.IsValue && !layout.Axis.Horizontal && layout.Rotation == 0 && layout.Levels.Count == 0)
                        WrapVerticalLabels(layout, axes, width, plot.H);
                }
                plot = LabelsInChart(plot, axes);
                break;
            }
            var next = ex != null
                ? ExInnerPlot(outer, axes, legend is { Position: "t", Overlay: false })
                : InnerPlot(outer, axes, plot, table, ManualOuterPlot() != null);
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
        svg.Append(Area(model.PlotArea?.Element(Ns.C + "spPr"), plot, snap: false, defaultFill: legacy != null ? LegacyPlotArea() : null));

        foreach (var layout in axes.Where(a => a.Visible || a.Axis.MajorGridlines != null))
            svg.Append(Gridlines(layout, plot));

        foreach (var group in groups)
        {
            var categoryAxis = axes.FirstOrDefault(a => !a.Axis.IsValue && group.AxisIds.Contains(a.Axis.Id));
            var valueAxis = axes.FirstOrDefault(a => a.Axis.IsValue && group.AxisIds.Contains(a.Axis.Id) && a != categoryAxis);
            if (valueAxis == null)
                continue;
            SeriesFrame frame;
            if (group.IsScatter)
            {
                var xAxis = axes.FirstOrDefault(a => a.Axis.IsValue && group.AxisIds.Contains(a.Axis.Id) && a.Axis.Horizontal);
                var yAxis = axes.FirstOrDefault(a => a.Axis.IsValue && group.AxisIds.Contains(a.Axis.Id) && !a.Axis.Horizontal);
                if (xAxis == null || yAxis == null)
                    continue;
                frame = ScatterFrame(xAxis, yAxis);
                svg.Append(string.Concat(group.Series.Select(s => Part(s, -1, ErrorBarLines(group, s, frame)))));
                svg.Append(Scatter(group, xAxis, yAxis));
            }
            else
            {
                if (categoryAxis == null)
                    continue;
                frame = CategoryFrame(categoryAxis, valueAxis);
                // Error bars lie under lines and markers but over bars and areas.
                var errorBars = string.Concat(group.Series.Select(s => Part(s, -1, ErrorBarLines(group, s, frame))));
                if (group.IsLine) svg.Append(errorBars);
                if (group.IsBar) svg.Append(Bars(group, categoryAxis, valueAxis));
                else if (group.IsArea) svg.Append(Areas(group, categoryAxis, valueAxis));
                else if (group.IsLine) svg.Append(Lines(group, categoryAxis, valueAxis));
                if (!group.IsLine) svg.Append(errorBars);
            }
            foreach (var series in group.Series)
                svg.Append(Part(series, -1, Trendlines(group, series, frame, plot)));
        }
        // The plot of an Office 2016 chart, laid out on its frame's axes.
        if (exAxisPlot != null)
            svg.Append(exAxisPlot(axes, plot));

        foreach (var layout in axes.Where(a => a.Visible))
            svg.Append(AxisLine(layout, axes, plot));
        foreach (var layout in axes.Where(a => a.HasLabels))
            svg.Append(ex != null ? ExAxisLabels(layout, plot) : AxisLabels(layout, axes, plot));
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
        /// <summary>The height of a row of one line.</summary>
        public required double RowHeight { get; init; }
        public required double LineHeight { get; init; }
        /// <summary>Every series name broken into the lines of the name column.</summary>
        public required List<List<string>> Names { get; init; }
        public required double NameWidth { get; init; }
        public required bool Keys { get; init; }
        /// <summary>The category names of the header row broken into the width of their columns (see FitDataTable).</summary>
        public List<List<string>> Header { get; set; } = [];

        public double HeaderHeight => RowHeight + (Header.Count == 0 ? 0 : Header.Max(h => h.Count) - 1) * LineHeight;

        public double SeriesHeight(int row) => RowHeight + (Names[row].Count - 1) * LineHeight;

        /// <summary>The header row and one row per series, each taller by a line for every further line of its names.</summary>
        public double Height => HeaderHeight + Enumerable.Range(0, Rows.Count).Sum(SeriesHeight);
    }

    /// <summary>The padding between a data table cell's border and its text, also the gap after the names without keys.</summary>
    const double DataTablePadding = 3.91;

    /// <summary>
    /// The data table of c:dTable, when the categories run along the bottom. Measured: rows are L + 0.0925 s + 2.4 pt
    /// high; the name column holds the widest series name plus 1.0975 s + 2.89 pt (key and padding), at most a third
    /// of the chart's width: longer names break between words, every further line making their row a line taller
    /// (local/dtable, dtable2: chart widths of 400 to 900 pt).
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
        var padding = keys ? 1.0975 * style.Size + 2.89 : DataTablePadding;
        var names = rows.Select(r => WrapWords(r.s.Name, style, width / 3 - padding)).ToList();
        var widest = names.Count == 0 ? 0 : names.Max(lines => lines.Max(l => Width(l, style)));
        category.InTable = true;
        return new DataTableLayout
        {
            Element = element,
            Style = style,
            Category = category,
            Rows = rows,
            RowHeight = lineHeight + 0.0925 * style.Size + 2.4,
            LineHeight = lineHeight,
            Names = names,
            NameWidth = widest + padding,
            Keys = keys,
        };
    }

    /// <summary>
    /// Breaks the header row's category names into the width of their columns, the plot area's width over the
    /// categories less 2.7 pt on each side (measured: a 57.4 pt name stays on one line in a 63.1 pt column and
    /// breaks in a 62.4 pt one, so the padding lies between 2.53 and 2.87 pt).
    /// </summary>
    void FitDataTable(DataTableLayout table, double plotWidth)
    {
        var column = plotWidth / Math.Max(1, table.Category.Categories) - 2 * 2.7;
        table.Header = table.Category.Labels.Take(table.Category.Categories).Select(l => WrapWords(l, table.Style, column)).ToList();
    }

    /// <summary>Text broken between words into lines of at most the given width (a longer word keeps a line of its own).</summary>
    List<string> WrapWords(string text, TextStyle style, double limit)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && Width(candidate, style) > limit)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                    line = candidate;
            }
            lines.Add(line);
        }
        return lines;
    }

    string DrawDataTable(DataTableLayout table, Box plot)
    {
        if (table.Header.Count == 0)
            FitDataTable(table, plot.W);
        var svg = new StringBuilder();
        var style = table.Style;
        var lineHeight = table.LineHeight;
        var category = table.Category;
        // Row edges: the header row, then the series rows.
        var edges = new List<double> { plot.Bottom, plot.Bottom + table.HeaderHeight };
        for (var r = 0; r < table.Rows.Count; r++)
            edges.Add(edges[^1] + table.SeriesHeight(r));
        var (top, rowsTop, bottom) = (edges[0], edges[1], edges[^1]);
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
                    svg.Append(GridLine(line, left, edges[r], right, edges[r]));
            }
            if (ChartModel.Flag(table.Element, "showVertBorder", false))
            {
                for (var i = 0; i < category.Categories; i++)
                    svg.Append(GridLine(line, category.Boundary(i), top, category.Boundary(i), bottom));
            }
        }

        // The baseline of line k of n lines centered in a row.
        double Baseline(int row, int k, int n) => CenteredBaseline(style, (edges[row] + edges[row + 1]) / 2 + (k - (n - 1) / 2.0) * lineHeight);
        // Category names in the header row (each line centered in its column), then a row per series: key and name
        // (further lines of a long name centered under its widest line), values centered in their cells.
        for (var i = 0; i < category.Categories && i < table.Header.Count; i++)
        {
            var center = (category.Boundary(i) + category.Boundary(i + 1)) / 2;
            var lines = table.Header[i];
            for (var k = 0; k < lines.Count; k++)
                svg.Append(text.Label(lines[k], style, center - Width(lines[k], style) / 2, Baseline(0, k, lines.Count)));
        }
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var (group, series) = table.Rows[r];
            var names = table.Names[r];
            var firstLine = (edges[r + 1] + edges[r + 2]) / 2 - (names.Count - 1) / 2.0 * lineHeight;
            if (table.Keys)
            {
                var key = LegendKey * lineHeight;
                var fill = group.HasLines ? SeriesOutline(group, series)?.Fill ?? PointFill(group, series, -1) : PointFill(group, series, -1);
                svg.Append(Rect(new Box(left + 0.26 * lineHeight, firstLine - key / 2, key, key), fill, null, snap: false));
            }
            var nameLeft = left + (table.Keys ? 0.958 : 0.26) * lineHeight;
            var widest = names.Max(l => Width(l, style));
            for (var k = 0; k < names.Count; k++)
                svg.Append(text.Label(names[k], style, nameLeft + (widest - Width(names[k], style)) / 2, Baseline(r + 1, k, names.Count)));
            for (var i = 0; i < category.Categories; i++)
            {
                if (series.Values.ElementAtOrDefault(i) is not { } v)
                    continue;
                var value = NumberFormat.Format(v, series.FormatCode, culture);
                var middle = (category.Boundary(i) + category.Boundary(i + 1)) / 2;
                svg.Append(text.Label(value, style, middle - Width(value, style) / 2, Baseline(r + 1, 0, 1)));
            }
        }
        return svg.ToString();
    }

    /// <summary>The plot area inside the axis labels (PowerPoint's inner plot area) for an automatic layout.</summary>
    /// <param name="manual">
    /// The outer box is a manual layout (layoutTarget outer): the labels then fit inside it instead of reaching past
    /// it (measured: a 180 x 205 pt outer plot area with labels turned by −45° and value labels from 0 to 6).
    /// </param>
    Box InnerPlot(Box outer, List<AxisLayout> axes, Box previous, DataTableLayout? table, bool manual = false)
    {
        double left = outer.X, top = outer.Y, right = outer.Right, bottom = outer.Bottom;
        var edges = new HashSet<string>();
        if (table != null)
        {
            // The data table reaches 6 pt past the bottom of the plot area; its name column ends at the plot's left.
            FitDataTable(table, previous.W);
            edges.Add("b");
            bottom = outer.Bottom + DataTableBleed - table.Height;
            left = outer.X + DataTableBleed + table.NameWidth;
        }
        foreach (var layout in axes.Where(a => a.HasLabels))
        {
            var s = layout.Style.Size;
            FitLabels(layout, layout.Axis.Horizontal ? previous.W : previous.H);
            layout.Inside = LabelsInside(layout, axes);
            if (layout.Inside)
                continue;
            if (!layout.Axis.IsValue && !layout.Axis.Horizontal && layout.Rotation == 0 && layout.Levels.Count == 0)
                WrapVerticalLabels(layout, axes, outer.W, previous.H);
            edges.Add(layout.Axis.Position);
            var extent = LabelExtent(layout);
            var bleed = layout.Titled ? -TitlePaddingY : manual ? 0 : LabelBleed;
            // A category axis's labels keep their distance by c:lblOffset (see AxisLabelOffset); a value axis with a
            // display unit label gives it a column of its own beside its labels.
            var gapScale = AxisLabelOffset(layout);
            if (UnitLabel(layout.Axis) is { } unit)
                extent += LineHeight(unit.Style) + UnitLabelColumn;
            switch (layout.Axis.Position)
            {
                case "l":
                    left = Math.Max(left, outer.X - bleed + extent + ValueLabelGap * s);
                    break;
                case "r":
                    right = Math.Min(right, outer.Right + bleed - extent - ValueLabelGap * s);
                    break;
                case "b":
                    bottom = Math.Min(bottom, outer.Bottom + bleed - extent - CategoryLabelGap * s * gapScale);
                    break;
                case "t":
                    top = Math.Max(top, outer.Y - bleed + extent + CategoryLabelGap * s * gapScale);
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
                var reach = manual ? 0 : 6;
                if (!edges.Contains("t")) top = Math.Max(top, outer.Y - reach + half);
                if (!edges.Contains("b")) bottom = Math.Min(bottom, outer.Bottom + reach - half);
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
            var limit = outer.X - (manual ? 0 : 9) + reach;
            var n = Math.Max(1, layout.Categories);
            var needed = layout.Between ? (limit - right / (2 * n)) / (1 - 1.0 / (2 * n)) : limit;
            left = Math.Max(left, needed);
        }
        return new Box(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
    }

    /// <summary>
    /// A plot area placed by hand keeps its axis labels inside the chart: it moves left as far as the value label
    /// centered on its right edge comes closer than 6 pt to the chart's edge, then gives up width on the left to the
    /// labels beside a left axis, which start 0.5 pt inside the chart (measured: LibreOffice's tdf48041 and variants
    /// of its layout).
    /// </summary>
    Box LabelsInChart(Box plot, List<AxisLayout> axes)
    {
        foreach (var layout in axes.Where(a => a.HasLabels && !a.Inside && a.Axis.IsValue && a.Axis.Horizontal && a.Labels.Count > 0))
        {
            var last = layout.Axis.Reversed ? layout.Labels[0] : layout.Labels[^1];
            var reach = plot.Right + Width(last, layout.Style) / 2 + 6 - width;
            if (reach > 0)
                plot = plot with { X = plot.X - reach };
        }
        foreach (var layout in axes.Where(a => a.HasLabels && !a.Inside && a.Axis.Position == "l"))
        {
            var need = 0.5 + LabelExtent(layout) + ValueLabelGap * layout.Style.Size;
            if (plot.X < need)
                plot = new Box(need, plot.Y, Math.Max(1, plot.Right - need), plot.H);
        }
        return plot;
    }

    /// <summary>
    /// The labels' turn (see <see cref="Rotation"/>); category labels that do not fit their slots on a horizontal axis wrap
    /// at the slot's width where every word fits it, and turn by 45° where a word alone does not (measured: two- to
    /// six-word labels wrap, also in a chart 200 pt short; one word wider than its slot turns all labels; as in 3D).
    /// </summary>
    void FitLabels(AxisLayout layout, double length)
    {
        if (layout.Axis.IsValue || !layout.Axis.Horizontal || layout.Names.Count == 0)
        {
            layout.Rotation = Rotation(layout, length);
            return;
        }
        var style = layout.Style;
        layout.Labels = layout.Names;
        layout.MaxLabelWidth = layout.Names.Max(l => l.Split('\n').Max(line => Width(line, style)));
        layout.Rotation = Rotation(layout, length);
        layout.DatesWrapped = false;
        if (layout.DateStep == 0 && layout.Levels.Count == 0 && layout.MaxLabelWidth > length / Math.Max(1, layout.Categories))
        {
            // A date axis that labels as many dates as fit shows them all where their words fit their slots, wrapped
            // (measured: "Okt 12" … "Jan 14" under 16 months, Nov 12 on two lines; LibreOffice's bnc889755).
            var dateSlot = length / Math.Max(1, layout.Categories);
            var dates = layout.Names.Select(name => name.Split('\n').SelectMany(line => WrapWords(line, style, dateSlot)).ToList()).ToList();
            if (dates.All(lines => lines.All(line => Width(line, style) <= dateSlot)))
            {
                layout.Labels = dates.Select(lines => string.Join("\n", lines)).ToList();
                layout.MaxLabelWidth = dates.Max(lines => lines.Max(line => Width(line, style)));
                layout.DatesWrapped = true;
            }
            return;
        }
        if (layout.Rotation != -45 || layout.DateStep != null || layout.Levels.Count > 0
            || layout.Axis.TxPr?.Element(Ns.A + "bodyPr").Long("rot") is not (null or -60000000))
            return;
        var slot = length / Math.Max(1, layout.Categories);
        var wrapped = layout.Names.Select(name => name.Split('\n').SelectMany(line => WrapWords(line, style, slot)).ToList()).ToList();
        if (wrapped.Any(lines => lines.Any(line => Width(line, style) > slot)))
            return;
        layout.Labels = wrapped.Select(lines => string.Join("\n", lines)).ToList();
        layout.MaxLabelWidth = wrapped.Max(lines => lines.Max(line => Width(line, style)));
        layout.Rotation = 0;
    }

    /// <summary>
    /// Category labels beside a vertical axis wrap at half the outer plot area's width less the overhangs of the value
    /// labels centered on the plot's left and right edges, and keep only the lines that fit their category's height, the
    /// last one ending in an ellipsis (measured: bar charts 200 to 950 pt wide with 9 to 24 pt labels, value labels of
    /// several widths or none).
    /// </summary>
    void WrapVerticalLabels(AxisLayout layout, List<AxisLayout> axes, double outerWidth, double length)
    {
        var value = axes.FirstOrDefault(a => a.Axis.Id == layout.Axis.CrossAxisId && a.Axis.IsValue && a.Axis.Horizontal && a.HasLabels && a.Labels.Count > 0);
        var overhangs = value == null ? 0 : (Width(value.Labels[0], value.Style) + Width(value.Labels[^1], value.Style)) / 2;
        var limit = Math.Max(1, (outerWidth - overhangs) / 2);
        var style = layout.Style;
        var room = Math.Max(1, (int)Math.Floor(length / Math.Max(1, layout.Categories) / LineHeight(style) + 1e-9));
        const string ellipsis = "\u2026";
        layout.Cut.Clear();
        layout.Labels = layout.Names.Select((name, i) =>
        {
            var lines = WrapWords(name, style, limit);
            if (lines.Count <= room)
                return string.Join("\n", lines);
            layout.Cut.Add(i);
            // The last line that fits takes the words that fit with the ellipsis after them.
            var words = string.Join(" ", lines.Skip(room - 1)).Split(' ');
            var last = words[0];
            foreach (var word in words.Skip(1))
            {
                if (Width(last + " " + word + ellipsis, style) > limit)
                    break;
                last += " " + word;
            }
            return string.Join("\n", lines.Take(room - 1).Append(last + ellipsis));
        }).ToList();
        layout.MaxLabelWidth = layout.Labels.Count == 0 ? 0 : layout.Labels.Max(l => l.Split('\n').Max(line => Width(line, style)));
    }

    /// <summary>
    /// The factor on the default gap between a category axis and its labels (c:lblOffset, percent): half of the gap
    /// stays, the other half follows the percentage (measured: offsets of 0, 200, 400 and 1000 put 12 pt labels 0.5,
    /// 1.5, 2.5 and 5.5 default gaps below the axis).
    /// </summary>
    static double AxisLabelOffset(AxisLayout layout) =>
        layout.Axis.IsValue ? 1 : (1 + (ChartModel.Number(layout.Axis.Element, "lblOffset") ?? 100) / 100) / 2;

    /// <summary>
    /// Label rotation in degrees (negative rises to the right): the text body's own, else −45° when category labels
    /// do not fit. Flat charts thin out the labels of date axes instead; 3D charts label every date and turn them when
    /// a label and a font size do not fit, the room flat charts keep between dates (charts-3d slide 18).
    /// </summary>
    static double Rotation(AxisLayout layout, double length, bool threeD = false)
    {
        var rot = layout.Axis.TxPr?.Element(Ns.A + "bodyPr").Long("rot");
        if (rot is { } r && r != -60000000)
            return r / 60000.0;
        if (layout.Axis.IsValue || !layout.Axis.Horizontal || layout.Categories == 0 || layout.DateStep != null && !threeD)
            return 0;
        var dates = layout.DateStep != null || threeD && layout.Groups.Any(g => g.TimeUnit != null);
        var needed = layout.MaxLabelWidth + (dates ? layout.Style.Size : 0);
        return needed > length / layout.Categories ? -45 : 0;
    }

    /// <summary>The groups of an outer category level: from each named category to the next one (or the end).</summary>
    static List<(int Start, int End, string Name)> CategoryGroups(List<string?> level, int categories)
    {
        var starts = Enumerable.Range(0, Math.Min(level.Count, categories)).Where(i => level[i] != null).ToList();
        return starts.Select((start, k) => (start, k + 1 < starts.Count ? starts[k + 1] : categories, level[start]!)).ToList();
    }

    /// <summary>The labels' extent away from the axis.</summary>
    static double LabelExtent(AxisLayout layout)
    {
        var lineHeight = LineHeight(layout.Style);
        var horizontal = layout.Axis.Horizontal;
        var lines = layout.Axis.IsValue ? 1 : layout.LabelLines;
        // Outer category levels add a row each, the label gap apart (measured: 7.5 pt between 12 pt rows).
        var levels = layout.Levels.Count * (lineHeight + CategoryLabelGap * layout.Style.Size);
        if (layout.Rotation == 0)
            return horizontal ? lines * lineHeight + levels : layout.MaxLabelWidth;
        var a = Math.Abs(layout.Rotation) * Math.PI / 180;
        return horizontal
            ? layout.MaxLabelWidth * Math.Sin(a) + lines * lineHeight * Math.Cos(a) + levels
            : layout.MaxLabelWidth * Math.Cos(a) + lines * lineHeight * Math.Sin(a);
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

    /// <param name="length">The axis's length in the plot area.</param>
    /// <param name="divided">The length the axis divides into intervals where that is not its own.</param>
    /// <param name="interval">The least length of an interval where it is not the axis's own rule.</param>
    void ScaleValueAxis(AxisLayout layout, double length, double? divided = null, double? interval = null)
    {
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        // A 100 % axis only where every group on it stacks to 100 %; mixed with others, the fractions are plotted as
        // values (measured: LibreOffice's stacked-non-stacked-mix-y-axis, a 100 % area at 1 beside columns up to 7).
        var percent = layout.Groups.Count > 0 && layout.Groups.All(g => g.PercentStacked);
        foreach (var group in layout.Groups)
        {
            foreach (var (lo, hi) in Ranges(group, layout.Axis))
            {
                min = Math.Min(min, lo);
                max = Math.Max(max, hi);
            }
        }
        if (double.IsInfinity(min)) (min, max) = (0, 1);

        var style = layout.Style;
        // At most ten intervals, each at least 1.1 line heights tall (vertical axes) or four font sizes wide, however
        // wide the labels are (horizontal axes; measured: bar and scatter charts 150 to 900 pt wide with 9 and 12 pt
        // labels, which overlap where they are wider). Office 2016 charts keep one label wide plus three font sizes.
        var extent = interval is { } least ? least
            : !layout.Axis.Horizontal ? LineHeight(style) * 1.1
            : ex == null ? 4 * style.Size
            : Math.Max(Width(NumberFormat.Format(max, Format(layout), culture), style), Width(NumberFormat.Format(min, Format(layout), culture), style)) + 3 * style.Size;
        var maxIntervals = Math.Clamp((int)Math.Floor((divided ?? length) / Math.Max(1, extent)), 1, 10);
        // Histograms of Office 2016 charts end at the first major unit past their largest bin (measured: 24 on 0 … 25),
        // radar charts at the first one from their largest value (measured: 7.9 on 0 … 8, 9.6 on 0 … 10).
        var headroom = ex?.Layout != "clusteredColumn" && !layout.Groups.Any(g => g.IsRadar);
        layout.Scale = AxisScaling.Compute(min, max, layout.Axis, maxIntervals, percent, headroom);

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
        var unit = DisplayUnit(layout.Axis);
        layout.Labels = layout.Scale.Ticks().Select(v => NumberFormat.Format(v / unit, Format(layout), culture)).ToList();
        layout.MaxLabelWidth = layout.Labels.Count == 0 ? 0 : layout.Labels.Max(l => Width(l, style));
    }

    /// <summary>
    /// A value axis's display unit label (c:dispUnitsLbl): its own text (c:tx) or the unit's name in the chart's
    /// language, in its text properties; null where the axis shows none.
    /// </summary>
    (string Text, TextStyle Style)? UnitLabel(ChartAxis axis)
    {
        if (!axis.IsValue || axis.Element.Element(Ns.C + "dispUnits")?.Element(Ns.C + "dispUnitsLbl") is not { } label)
            return null;
        var own = string.Concat(label.Element(Ns.C + "tx")?.Descendants(Ns.A + "t").Select(t => t.Value) ?? []);
        var text = own.Length > 0 ? own : DisplayUnitText(axis);
        return text.Length == 0 ? null : (text, Style(label.Element(Ns.C + "txPr"), "valueAxis", 10));
    }

    // A display unit label's line stands 3.6 pt beside the value labels, its end 2.5 pt below the top of the plot
    // area, in a column 9.5 pt wider than its line (measured: "Millionen" in 13.3 pt beside labels from 0 to 5).
    const double UnitLabelGap = 3.6, UnitLabelEnd = 2.5, UnitLabelColumn = 9.5;

    /// <summary>
    /// The display unit label of a value axis on the left or right: turned a quarter (reading upwards) beside the
    /// labels, its end at the top of the plot area (see <see cref="UnitLabelGap"/>).
    /// </summary>
    string UnitLabelMarkup(AxisLayout layout, double edge)
    {
        if (UnitLabel(layout.Axis) is not { } unit || layout.Axis.Position is not ("l" or "r"))
            return "";
        var (label, style) = unit;
        var w = Width(label, style);
        var s = layout.Style.Size;
        var lineHeight = LineHeight(style);
        var x = layout.Axis.Position == "l"
            ? edge - ValueLabelGap * s - layout.MaxLabelWidth - UnitLabelGap - lineHeight / 2
            : edge + ValueLabelGap * s + layout.MaxLabelWidth + UnitLabelGap + lineHeight / 2;
        var top = layout.Scale.Ticks().Select(layout.Map).DefaultIfEmpty(0).Min() + UnitLabelEnd;
        return $"<g transform=\"translate({Px(x)} {Px(top + w)}) rotate(-90)\">{text.Label(label, style, 0, CenteredBaseline(style, 0))}</g>";
    }

    static readonly Dictionary<string, double> DisplayUnits = new()
    {
        ["hundreds"] = 1e2, ["thousands"] = 1e3, ["tenThousands"] = 1e4, ["hundredThousands"] = 1e5, ["millions"] = 1e6,
        ["tenMillions"] = 1e7, ["hundredMillions"] = 1e8, ["billions"] = 1e9, ["trillions"] = 1e12,
    };

    /// <summary>The value an axis's labels are divided by (c:dispUnits: a built-in or custom unit), else 1.</summary>
    static double DisplayUnit(ChartAxis axis)
    {
        var units = axis.Element.Element(Ns.C + "dispUnits");
        if (units?.Element(Ns.C + "custUnit")?.Attribute("val")?.Value is { } custom
            && double.TryParse(custom, System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0)
            return value;
        return units?.Element(Ns.C + "builtInUnit")?.Attribute("val")?.Value is { } name && DisplayUnits.TryGetValue(name, out var builtIn) ? builtIn : 1;
    }

    /// <summary>
    /// The default text of a display unit label in the chart's language (Office writes none: PowerPoint shows its own
    /// words, measured with a German Office: "Millionen", "x 10000").
    /// </summary>
    string DisplayUnitText(ChartAxis axis)
    {
        var units = axis.Element.Element(Ns.C + "dispUnits");
        var name = units?.Element(Ns.C + "builtInUnit")?.Attribute("val")?.Value;
        if (name == null)
            return units?.Element(Ns.C + "custUnit")?.Attribute("val")?.Value is { } custom ? "x " + custom : "";
        var german = culture.TwoLetterISOLanguageName == "de";
        return name switch
        {
            "hundreds" => german ? "Hunderte" : "Hundreds",
            "thousands" => german ? "Tausende" : "Thousands",
            "millions" => german ? "Millionen" : "Millions",
            "billions" => german ? "Milliarden" : "Billions",
            "trillions" => german ? "Billionen" : "Trillions",
            _ => "x " + DisplayUnits[name].ToString("0", CultureInfo.InvariantCulture),
        };
    }

    string Format(AxisLayout layout)
    {
        if (layout.Groups.Count > 0 && layout.Groups.All(g => g.PercentStacked) && (layout.Axis.SourceLinked || layout.Axis.FormatCode == "General"))
            return "0%";
        if (layout.Axis.SourceLinked)
            return layout.Groups.SelectMany(g => g.Series).FirstOrDefault()?.FormatCode ?? layout.Axis.FormatCode;
        return layout.Axis.FormatCode;
    }

    /// <summary>The value ranges a group contributes to an axis (stacked groups contribute their sums).</summary>
    static IEnumerable<(double Min, double Max)> Ranges(ChartGroup group, ChartAxis axis)
    {
        // Error bars count towards the range.
        IEnumerable<(double, double)> Errors(ChartSeries s, List<double?> values, bool alongX)
        {
            if (ErrorBars(group, s, alongX) is not { } bars)
                yield break;
            for (var i = 0; i < values.Count; i++)
            {
                if (ErrorRange(bars, values, i) is var (low, high))
                    yield return (low, high);
            }
        }
        if (group.IsScatter)
        {
            foreach (var s in group.Series)
            {
                var all = axis.Horizontal ? (s.XValues.Count > 0 ? s.XValues : Enumerable.Range(1, s.Values.Count).Select(i => (double?)i).ToList()) : s.Values;
                var values = all.Where(v => v.HasValue).Select(v => v!.Value).ToList();
                if (values.Count > 0) yield return (values.Min(), values.Max());
                foreach (var range in Errors(s, all, axis.Horizontal))
                    yield return range;
            }
            yield break;
        }
        if (!group.Stacked)
        {
            foreach (var s in group.Series)
            {
                var values = s.Values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
                if (values.Count > 0) yield return (values.Min(), values.Max());
                foreach (var range in Errors(s, s.Values, false))
                    yield return range;
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

    /// <summary>
    /// The line of c:majorGridlines or c:minorGridlines: its own, else 0.75 pt light gray (charts without style part:
    /// the Office 2007 style's, under their own).
    /// </summary>
    Outline GridOutline(XElement gridlines) =>
        LineOver(gridlines.Element(Ns.C + "spPr")?.Element(Ns.A + "ln"), new Outline(9525, new SolidFill(RgbColor.FromHex("D9D9D9")), "solid", "flat", "round", 8, "sng"));

    /// <summary>The line of an axis: its own, else 0.75 pt gray (charts without style part: as gridlines).</summary>
    Outline AxisOutline(ChartAxis axis) =>
        LineOver(axis.SpPr?.Element(Ns.A + "ln"), new Outline(9525, new SolidFill(RgbColor.FromHex("BFBFBF")), "solid", "flat", "round", 8, "sng"));

    /// <summary>An axis or grid line: its own a:ln (in charts without style part over the style's line), else the default.</summary>
    Outline LineOver(XElement? own, Outline automatic)
    {
        if (legacy != null)
            return OutlineReader.Merge(own != null ? [LegacyAxisLine(), (own, colors)] : [LegacyAxisLine()])!;
        return own != null ? OutlineReader.Merge([(own, colors)])! : automatic;
    }

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
            // A category axis may mark only every n-th category (c:tickMarkSkip).
            var markSkip = layout.Axis.IsValue ? 1 : Math.Max(1, (int)(ChartModel.Number(layout.Axis.Element, "tickMarkSkip") ?? 1));
            var ticks = layout.Axis.IsValue
                ? (minor ? layout.Scale.MinorTicks() : layout.Scale.Ticks()).Select(layout.Map)
                : layout.Between
                    ? Enumerable.Range(0, layout.Categories + 1).Where(i => i % markSkip == 0).Select(i => layout.Boundary(i))
                    : Enumerable.Range(0, layout.Categories).Where(i => i % markSkip == 0).Select(layout.Center);
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
        // A category axis may label only every n-th category (c:tickLblSkip), and its labels keep their distance by
        // c:lblOffset.
        var skip = layout.Axis.IsValue ? 1 : Math.Max(1, (int)(ChartModel.Number(layout.Axis.Element, "tickLblSkip") ?? 1));
        if (layout.DateStep is { } step)
        {
            // Date axes without a major unit label as many dates as fit, a label and a font size apart.
            var slot = Math.Abs(layout.End - layout.Start) / Math.Max(1, layout.Categories);
            skip = step > 0 ? step : layout.DatesWrapped ? 1 : Math.Max(1, (int)Math.Ceiling((layout.MaxLabelWidth + style.Size) / Math.Max(1e-9, slot)));
        }
        var gapScale = AxisLabelOffset(layout);
        for (var i = 0; i < layout.Labels.Count && i < positions.Count; i++)
        {
            if (i % skip != 0)
                continue;
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
                case "l" or "r" when label.Contains('\n'):
                {
                    // Wrapped labels: a block as wide as its longest line beside the axis, the lines centered in it
                    // and the block centered on the category; a label cut short centers its last line without the
                    // ellipsis (measured).
                    var lines = label.Split('\n');
                    var block = lines.Max(line => Width(line, style));
                    var blockLeft = layout.Axis.Position == "l" ? left - ValueLabelGap * s - block : right + ValueLabelGap * s;
                    var baseline = CenteredBaseline(style, p) - (lines.Length - 1) * lineHeight / 2;
                    for (var k = 0; k < lines.Length; k++)
                    {
                        var centered = k == lines.Length - 1 && layout.Cut.Contains(i) ? lines[k][..^1] : lines[k];
                        svg.Append(text.Label(lines[k], style, blockLeft + (block - Width(centered, style)) / 2, baseline));
                        baseline += lineHeight;
                    }
                    break;
                }
                case "l":
                    svg.Append(text.Label(label, style, left - ValueLabelGap * s - w, CenteredBaseline(style, p)));
                    if (i == 0)
                        svg.Append(UnitLabelMarkup(layout, left));
                    break;
                case "r":
                    svg.Append(text.Label(label, style, right + ValueLabelGap * s, CenteredBaseline(style, p)));
                    if (i == 0)
                        svg.Append(UnitLabelMarkup(layout, right));
                    break;
                case "b":
                case "t":
                {
                    // Lines centered, or flush with the category's slot edge (c:lblAlgn l, r; measured).
                    var lines = label.Split('\n');
                    var (slot0, slot1) = Slot(layout, i);
                    var baseline = layout.Axis.Position == "b"
                        ? bottom + CategoryLabelGap * s * gapScale + Ascent(style)
                        : top - CategoryLabelGap * s * gapScale - Descent(style) - (lines.Length - 1) * lineHeight;
                    foreach (var line in lines)
                    {
                        var lw = Width(line, style);
                        var x = layout.Axis.IsValue ? p - lw / 2 : layout.Align switch
                        {
                            "l" => slot0,
                            "r" => slot1 - lw,
                            _ => p - lw / 2,
                        };
                        svg.Append(text.Label(line, style, x, baseline));
                        baseline += lineHeight;
                    }
                    break;
                }
            }
        }
        if (layout.Levels.Count > 0 && layout.Axis.Position is "b" or "t" && layout.Rotation == 0)
            svg.Append(CategoryLevels(layout, layout.Axis.Position == "b" ? bottom : top, gapScale));
        return svg.ToString();
    }

    /// <summary>A category's slot along the axis: between its boundaries, or around its point on midCat axes.</summary>
    static (double From, double To) Slot(AxisLayout layout, int i)
    {
        double a, b;
        if (layout.Between)
            (a, b) = (layout.Boundary(i), layout.Boundary(i + 1));
        else
        {
            var half = Math.Abs(layout.End - layout.Start) / Math.Max(1, layout.Categories - 1) / 2;
            (a, b) = (layout.Center(i) - half, layout.Center(i) + half);
        }
        return (Math.Min(a, b), Math.Max(a, b));
    }

    /// <summary>
    /// The outer levels of multi-level categories: a row per level after the categories' own labels, the label gap
    /// apart, each name centered over its group, and lines along the group edges from the axis through the level's
    /// row (measured: 2023 and 2024 under Q1 to Q4 with lines at both ends and between them).
    /// </summary>
    string CategoryLevels(AxisLayout layout, double axis, double gapScale)
    {
        var svg = new StringBuilder();
        var style = layout.Style;
        var lineHeight = LineHeight(style);
        var gap = CategoryLabelGap * style.Size;
        var down = layout.Axis.Position == "b" ? 1 : -1;
        var outline = AxisOutline(layout.Axis);
        var edge = gap * gapScale + layout.LabelLines * lineHeight;   // from the axis to the end of the categories' row
        foreach (var level in layout.Levels)
        {
            edge += gap;
            var baseline = down > 0 ? axis + edge + Ascent(style) : axis - edge - Descent(style);
            foreach (var (start, end, name) in level)
            {
                var (a, b) = (layout.Boundary(start), layout.Boundary(end));
                svg.Append(text.Label(name, style, (a + b) / 2 - Width(name, style) / 2, baseline));
            }
            edge += lineHeight;
            if (outline is { Visible: true })
            {
                foreach (var boundary in level.Select(g => g.Start).Append(layout.Categories).Distinct())
                {
                    var x = layout.Boundary(boundary);
                    svg.Append(GridLine(outline, x, axis, x, axis + down * edge));
                }
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
                var (barFill, barOutline) = (PointFill(group, series, i), PointOutline(series, i));
                if (v < 0 && Inverted(series, i) is { } inverted)
                    (barFill, barOutline) = inverted;
                if (barFill is PictureFill picture && PictureOptions(series, i) is { Format: "stack" or "stackScale" } options)
                {
                    // Stacked pictures repeat from the bar's base: as wide as the bar at their own proportions, or one
                    // per c:pictureStackUnit of the value axis (catalog: chart.formatting).
                    var across = group.Horizontal ? box.H : box.W;
                    var tile = options.Format == "stackScale"
                        ? Math.Abs(value.Map(options.Unit) - value.Map(0))
                        : picture.NaturalSize.W > 0 ? across * picture.NaturalSize.H / picture.NaturalSize.W : across;
                    if (group.Horizontal && picture.NaturalSize.H > 0 && options.Format == "stack")
                        tile = across * picture.NaturalSize.W / picture.NaturalSize.H;
                    svg.Append(ShapePart(series, i, StackedPicture(box, picture, barOutline, group.Horizontal, v0, Math.Max(0.5, tile))));
                }
                else
                    svg.Append(ShapePart(series, i, Rect(box, barFill, barOutline, snap: true)));

                if (DataLabel(group, series, i) is { } spec && series.Values[i] is { } raw && !Beyond(value, to))
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
                    var placed = Moved(spec, group.Horizontal ? new Box(along, across - h / 2, w, h) : new Box(across - w / 2, along, w, h));
                    DrawLabel(spec, label, placed.X, placed.Y);
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
            if (v == null && BlanksAs == "zero" && !series.Fillers.Contains(i))
                v = 0;
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
                svg.Append(SeriesLine(series, points, outline, smooth));
            if (Marker(group, series) is { } marker)
            {
                for (var i = 0; i < points.Count; i++)
                {
                    if (points[i] is { } p)
                        svg.Append(ShapePart(series, i, DrawMarker(marker, series.Index, p.X, p.Y, PointFill(group, series, i), outline)));
                }
            }
            for (var i = 0; i < points.Count; i++)
            {
                if (points[i] is { } p && series.Values.ElementAtOrDefault(i) is { } raw && !Beyond(value, raw) && DataLabel(group, series, i) is { } spec)
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
                var fill = FillReader.FindFill(spPr) is { } f ? ReadFill(f) : new SolidFill(close >= open ? RgbColor.White : RgbColor.Black);
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
            // Dates without data (fillers) take the line between their neighbors' points.
            for (var i = 0; i < points.Count; i++)
            {
                if (points[i] != null || !series.Fillers.Contains(i))
                    continue;
                var before = Enumerable.Range(0, i).Reverse().FirstOrDefault(j => points[j] != null && !series.Fillers.Contains(j), -1);
                var after = Enumerable.Range(i + 1, points.Count - i - 1).FirstOrDefault(j => points[j] != null && !series.Fillers.Contains(j), -1);
                if (before < 0 || after < 0)
                    continue;
                var t = (double)(i - before) / (after - before);
                var (a, b) = (points[before]!.Value, points[after]!.Value);
                points[i] = (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            }
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
            var area = Bounds(top.Concat(top.Select((_, i) => horizontal ? (category.Center(i), baseline[i]) : (baseline[i], category.Center(i)))));
            svg.Append(ShapePart(series, -1, $"<path d=\"{d}\"{FillOver(PointFill(group, series, -1), area)}{paint.Stroke(PointOutline(series, -1), 0, 0, defs)}/>"));
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
                svg.Append(ShapePart(series, i, $"<circle cx=\"{Px(cx)}\" cy=\"{Px(cy)}\" r=\"{Px(r)}\"{FillOver(PointFill(group, series, i), (cx - r, cy - r, 2 * r, 2 * r))}{paint.Stroke(PointOutline(series, i), d, d, defs)}/>"));
                if (DataLabel(group, series, i) is { } spec)
                    LabelAtPoint(spec, LabelText(spec, series, i, yv, 0), cx, cy, spec.Position ?? "r", r + BubbleLabelGap);
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
                var y = series.Values[i] ?? (BlanksAs == "zero" ? 0 : null);
                if (x is not { } xv || y is not { } yv)
                {
                    points.Add(null);
                    continue;
                }
                points.Add((xAxis.Map(xv), yAxis.Map(yv)));
            }
            var outline = SeriesOutline(group, series);
            if (ScatterLines(group, series) && outline is { Visible: true })
                svg.Append(SeriesLine(series, points, outline, ChartModel.Flag(series.Element, "smooth", false)));
            if (Marker(group, series) is { } marker)
            {
                for (var i = 0; i < points.Count; i++)
                {
                    if (points[i] is { } p)
                        svg.Append(ShapePart(series, i, DrawMarker(marker, series.Index, p.X, p.Y, PointFill(group, series, -1), outline)));
                }
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

    /// <summary>
    /// How blank cells show (c:dispBlanksAs, catalog: chart.part): as gaps where the element is missing, as zero where
    /// it says so or names nothing, or spanned by the line (measured: spanned blanks get no marker).
    /// </summary>
    string BlanksAs => model.Chart.Element(Ns.C + "dispBlanksAs") is { } blanks ? blanks.Attribute("val")?.Value ?? "zero" : "gap";

    /// <summary>A line's points with blanks dropped where the line spans them.</summary>
    // ---------------------------------------------------------------- build steps

    /// <summary>
    /// A point's markup (bar, slice, marker, label, line segment) or, with point -1, a whole series' (area, error bars,
    /// trendline), tagged for the slide show's chart builds (catalog: animation.build): data-ser is the series' place
    /// in plot order (a:chart seriesIdx), data-pt its category (categoryIdx). Untagged parts are the chart's
    /// background (bldStep gridLegend).
    /// </summary>
    string Part(ChartSeries series, int point, string markup) =>
        markup.Length == 0 ? "" : PartStart(plotOrder.GetValueOrDefault(series), point) + markup + "</g>";

    /// <summary>Wraps what <paramref name="svg"/> gained since <paramref name="start"/> as a part (see <see cref="Part"/>).</summary>
    void WrapPart(StringBuilder svg, int start, ChartSeries series, int point)
    {
        if (svg.Length == start)
            return;
        svg.Insert(start, PartStart(plotOrder.GetValueOrDefault(series), point));
        svg.Append("</g>");
    }

    static string PartStart(int series, int point) =>
        point >= 0 ? $"<g data-ser=\"{series}\" data-pt=\"{point}\">" : $"<g data-ser=\"{series}\">";

    /// <summary>
    /// A series' line in segments, each with the category it ends at (a category build draws the line up to the
    /// category; measured on PowerPoint's video). Lines a segment would join differently (other caps or joins than
    /// round, dashes) stay whole, with the series.
    /// </summary>
    string SeriesLine(ChartSeries series, List<(double X, double Y)?> points, Outline outline, bool smooth)
    {
        var stroke = paint.Stroke(outline, 0, 0, defs);
        var indexed = points.Select((p, i) => p is { } q ? (Index: i, q.X, q.Y) : ((int Index, double X, double Y)?)null)
            .Where((_, i) => !series.Fillers.Contains(i)).ToList();
        if (BlanksAs == "span")
            indexed = indexed.Where(p => p != null).ToList();
        var svg = new StringBuilder();
        var whole = outline.Dash != "solid" || outline.Cap != "rnd" || outline.Join != "round";
        foreach (var run in Runs(indexed))
        {
            var snapped = run.Select(p => SnapPoint((p.X, p.Y), outline)).ToList();
            if (whole)
            {
                svg.Append(Part(series, -1, $"<path d=\"{PathData(snapped, smooth)}\" fill=\"none\"{stroke}/>"));
                continue;
            }
            for (var j = 1; j < run.Count; j++)
                svg.Append(Part(series, run[j].Index, $"<path d=\"{SegmentData(snapped, j, smooth)}\" fill=\"none\"{stroke}/>"));
        }
        return svg.ToString();
    }

    static IEnumerable<List<T>> Runs<T>(List<T?> points) where T : struct
    {
        var run = new List<T>();
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

    /// <summary>
    /// Path data through the points; smoothed lines use Catmull-Rom splines converted to Béziers, the ends continued
    /// by mirroring their neighbors (measured: the last segment of a smoothed scatter line leaves its point a third
    /// of the way back to the previous one).
    /// </summary>
    static string PathData(List<(double X, double Y)> points, bool smooth)
    {
        var d = new StringBuilder($"M{Px(points[0].X)} {Px(points[0].Y)}");
        for (var i = 1; i < points.Count; i++)
            d.Append(Segment(points, i, smooth));
        return d.ToString();
    }

    /// <summary>The segment from point i − 1 to point i on its own.</summary>
    static string SegmentData(List<(double X, double Y)> points, int i, bool smooth) =>
        $"M{Px(points[i - 1].X)} {Px(points[i - 1].Y)}" + Segment(points, i, smooth);

    static string Segment(List<(double X, double Y)> points, int i, bool smooth)
    {
        if (!smooth || points.Count < 3)
            return $"L{Px(points[i].X)} {Px(points[i].Y)}";
        var p1 = points[i - 1];
        var p2 = points[i];
        (double X, double Y) p0 = i >= 2 ? points[i - 2] : (2 * p1.X - p2.X, 2 * p1.Y - p2.Y);
        (double X, double Y) p3 = i + 1 < points.Count ? points[i + 1] : (2 * p2.X - p1.X, 2 * p2.Y - p1.Y);
        const double t = 1.0 / 6;
        var c1 = (p1.X + (p2.X - p0.X) * t, p1.Y + (p2.Y - p0.Y) * t);
        var c2 = (p2.X - (p3.X - p1.X) * t, p2.Y - (p3.Y - p1.Y) * t);
        return $"C{Px(c1.Item1)} {Px(c1.Item2)} {Px(c2.Item1)} {Px(c2.Item2)} {Px(p2.X)} {Px(p2.Y)}";
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
                // A line chart without the flag shows markers (measured: LibreOffice's tdf105517); stock charts have none.
                : ChartModel.Flag(group.Element, "marker", group.Type == "lineChart");
        return groupMarkers ? own ?? new XElement(Ns.C + "marker") : null;
    }

    /// <param name="keySize">The font size of the legend whose key the marker is: keys cap its size and edge (see LegendMarkerCap).</param>
    string DrawMarker(XElement marker, int seriesIndex, double x, double y, Fill seriesFill, Outline? seriesLine, double? keySize = null)
    {
        var symbol = ChartModel.Val(marker, "symbol") ?? MarkerCycle[seriesIndex % MarkerCycle.Length];
        if (symbol == "auto") symbol = MarkerCycle[seriesIndex % MarkerCycle.Length];
        var size = Math.Clamp(ChartModel.Number(marker, "size") ?? (legacy != null ? LegacyMarkerSize(seriesLine) : 5), 2, 72);
        if (keySize is { } key)
            size = Math.Min(size, LegendMarkerCap * key);
        var spPr = marker.Element(Ns.C + "spPr");
        var fill = FillReader.FindFill(spPr) is { } f ? ReadFill(f) : seriesFill is NoFill ? new SolidFill(seriesLine?.Fill is SolidFill sf ? sf.Color : RgbColor.Black) : seriesFill;
        var layers = new List<(XElement, ColorContext)>();
        if (legacy != null)
            layers.Add(LegacyMarkerEdge(seriesLine is { Visible: true, Fill: SolidFill lineFill } ? lineFill.Color : AutoColor(seriesIndex)));
        if (spPr?.Element(Ns.A + "ln") is { } ln)
            layers.Add((ln, colors));
        var line = layers.Count > 0
            ? OutlineReader.Merge(layers)
            : seriesLine is { Visible: true } ? seriesLine with { WidthEmu = 9525, Dash = "solid" } : null;
        if (keySize is { } font && line != null)
            line = line with { WidthEmu = Math.Min(line.WidthEmu, LegendLineCap * font * 12700) };
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
            : $"<path d=\"{d}\"{FillOver(fill, (x - r, y - r, 2 * r, 2 * r))}{paint.Stroke(line, 0, 0, defs)}/>";
    }

    // ---------------------------------------------------------------- trendlines and error bars

    /// <summary>Maps a series' data coordinates (x: the category number from 1, or the scatter x value) to the plot.</summary>
    sealed record SeriesFrame(Func<double, double, (double X, double Y)> Point, bool ValueVertical);

    static SeriesFrame CategoryFrame(AxisLayout category, AxisLayout value)
    {
        var step = category.Categories > 1 ? category.Center(1) - category.Center(0) : 0;
        return new SeriesFrame((x, y) =>
        {
            var c = category.Center(0) + (x - 1) * step;
            var v = value.Map(y);
            return category.Axis.Horizontal ? (c, v) : (v, c);
        }, category.Axis.Horizontal);
    }

    static SeriesFrame ScatterFrame(AxisLayout xAxis, AxisLayout yAxis) => new((x, y) => (xAxis.Map(x), yAxis.Map(y)), true);

    /// <summary>The points of a series with values: x is the category number (from 1) or the scatter x value.</summary>
    static List<(int Index, double X, double Y)> DataPoints(ChartGroup group, ChartSeries series)
    {
        var points = new List<(int, double, double)>();
        for (var i = 0; i < series.Values.Count; i++)
        {
            var x = group.IsScatter && series.XValues.Count > 0 ? series.XValues.ElementAtOrDefault(i) : i + 1;
            if (x is { } xv && series.Values[i] is { } yv)
                points.Add((i, xv, yv));
        }
        return points;
    }

    /// <summary>
    /// A fitted trendline (catalog: chart.analysis): a function of x, or the points of a moving average; the
    /// equation as text runs (superscript runs flagged) and R².
    /// </summary>
    sealed record Trend(Func<double, double?>? At, List<(double X, double Y)>? Points, List<(string Text, bool Superscript)> Equation, double? RSquared);

    /// <summary>Least squares w = a·u + b, or w = a·u + b₀ with a fixed intercept; R² in the fitted space.</summary>
    static (double A, double B, double R2) LeastSquares(List<(double U, double W)> p, double? fixedB)
    {
        if (fixedB is { } b0)
        {
            var suu = p.Sum(q => q.U * q.U);
            var a0 = suu == 0 ? 0 : p.Sum(q => q.U * (q.W - b0)) / suu;
            var residual = p.Sum(q => Math.Pow(q.W - (a0 * q.U + b0), 2));
            var total = p.Sum(q => Math.Pow(q.W - b0, 2));
            return (a0, b0, total == 0 ? 1 : 1 - residual / total);
        }
        var mu = p.Average(q => q.U);
        var mw = p.Average(q => q.W);
        var su = p.Sum(q => (q.U - mu) * (q.U - mu));
        var a = su == 0 ? 0 : p.Sum(q => (q.U - mu) * (q.W - mw)) / su;
        var b = mw - a * mu;
        var res = p.Sum(q => Math.Pow(q.W - (a * q.U + b), 2));
        var tot = p.Sum(q => Math.Pow(q.W - mw, 2));
        return (a, b, tot == 0 ? 1 : 1 - res / tot);
    }

    /// <summary>Polynomial least squares of the given order through normal equations (c[0] fixed when given).</summary>
    static double[]? Polynomial(List<(int Index, double X, double Y)> data, int order, double? intercept)
    {
        var first = intercept != null ? 1 : 0;
        var n = order + 1 - first;
        var m = new double[n, n + 1];
        foreach (var (_, x, y) in data)
        {
            var target = y - (intercept ?? 0);
            for (var r = 0; r < n; r++)
            {
                for (var c = 0; c < n; c++)
                    m[r, c] += Math.Pow(x, r + first + c + first);
                m[r, n] += Math.Pow(x, r + first) * target;
            }
        }
        for (var col = 0; col < n; col++)
        {
            var pivot = Enumerable.Range(col, n - col).MaxBy(r => Math.Abs(m[r, col]));
            if (Math.Abs(m[pivot, col]) < 1e-300)
                return null;
            for (var c = 0; c <= n; c++) (m[col, c], m[pivot, c]) = (m[pivot, c], m[col, c]);
            for (var r = 0; r < n; r++)
            {
                if (r == col) continue;
                var f = m[r, col] / m[col, col];
                for (var c = col; c <= n; c++) m[r, c] -= f * m[col, c];
            }
        }
        var coefficients = new double[order + 1];
        coefficients[0] = intercept ?? 0;
        for (var r = 0; r < n; r++)
            coefficients[r + first] = m[r, n] / m[r, r];
        return coefficients;
    }

    Trend? Fit(XElement trendline, List<(int Index, double X, double Y)> data, Func<double, string> number)
    {
        var type = ChartModel.Val(trendline, "trendlineType") ?? "linear";
        var intercept = ChartModel.Number(trendline, "intercept");
        if (data.Count < 2)
            return null;
        string Signed(double v) => v < 0 ? " - " + number(-v) : " + " + number(v);
        switch (type)
        {
            case "movingAvg":
            {
                var period = Math.Max(2, (int)(ChartModel.Number(trendline, "period") ?? 2));
                var points = new List<(double X, double Y)>();
                for (var i = period - 1; i < data.Count; i++)
                    points.Add((data[i].X, data.Skip(i - period + 1).Take(period).Average(p => p.Y)));
                return points.Count < 2 ? null : new Trend(null, points, [], null);
            }
            case "poly":
            {
                var order = Math.Clamp((int)(ChartModel.Number(trendline, "order") ?? 2), 2, 6);
                if (Polynomial(data, order, intercept) is not { } c)
                    return null;
                double Value(double x) => Enumerable.Range(0, c.Length).Sum(k => c[k] * Math.Pow(x, k));
                var mean = data.Average(p => p.Y);
                var res = data.Sum(p => Math.Pow(p.Y - Value(p.X), 2));
                var tot = data.Sum(p => Math.Pow(p.Y - (intercept ?? mean), 2));
                var terms = new List<(string, bool)> { ("y = ", false) };
                for (var k = order; k >= 0; k--)
                {
                    if (k < order && c[k] == 0 && k == 0 && intercept == 0)
                        continue;
                    terms.Add((k == order ? number(c[k]) : Signed(c[k]), false));
                    if (k >= 1) terms.Add(("x", false));
                    if (k >= 2) terms.Add((k.ToString(CultureInfo.InvariantCulture), true));
                }
                return new Trend(x => Value(x), null, terms, tot == 0 ? 1 : 1 - res / tot);
            }
            case "exp":
            {
                var p = data.Where(d => d.Y > 0).Select(d => (d.X, Math.Log(d.Y))).ToList();
                if (p.Count < 2) return null;
                var (b, lnC, r2) = LeastSquares(p, intercept is > 0 ? Math.Log(intercept.Value) : null);
                var c = Math.Exp(lnC);
                return new Trend(x => c * Math.Exp(b * x), null, [("y = " + number(c) + "e", false), (number(b) + "x", true)], r2);
            }
            case "log":
            {
                var p = data.Where(d => d.X > 0).Select(d => (Math.Log(d.X), d.Y)).ToList();
                if (p.Count < 2) return null;
                var (a, b, r2) = LeastSquares(p, null);
                return new Trend(x => x > 0 ? a * Math.Log(x) + b : null, null, [("y = " + number(a) + "ln(x)" + Signed(b), false)], r2);
            }
            case "power":
            {
                var p = data.Where(d => d.X > 0 && d.Y > 0).Select(d => (Math.Log(d.X), Math.Log(d.Y))).ToList();
                if (p.Count < 2) return null;
                var (b, lnC, r2) = LeastSquares(p, null);
                var c = Math.Exp(lnC);
                return new Trend(x => x > 0 ? c * Math.Pow(x, b) : null, null, [("y = " + number(c) + "x", false), (number(b), true)], r2);
            }
            default:
            {
                var (a, b, r2) = LeastSquares(data.Select(d => (d.X, d.Y)).ToList(), intercept);
                var text = "y = " + number(a) + "x" + (intercept == 0 ? "" : Signed(b));
                return new Trend(x => a * x + b, null, [(text, false)], r2);
            }
        }
    }

    /// <summary>The legend name of a trendline: c:name, else the type and the series name like Office.</summary>
    string TrendName(XElement trendline, ChartSeries series)
    {
        if (trendline.Element(Ns.C + "name")?.Value is { Length: > 0 } name)
            return name;
        var german = culture.TwoLetterISOLanguageName == "de";
        var type = (ChartModel.Val(trendline, "trendlineType") ?? "linear") switch
        {
            "exp" => german ? "Exponentiell" : "Expon.",
            "log" => german ? "Logarithmisch" : "Log.",
            "poly" => german ? "Polynomisch" : "Poly.",
            "power" => german ? "Potenziell" : "Power",
            "movingAvg" => german
                ? $"Gleitender Durchschnitt mit {ChartModel.Number(trendline, "period") ?? 2} Perioden"
                : $"{ChartModel.Number(trendline, "period") ?? 2} per. Mov. Avg.",
            _ => "Linear",
        };
        return $"{type} ({series.Name})";
    }

    /// <summary>The line of a trendline: its own a:ln over the series color, 1.5 pt, round dots.</summary>
    Outline TrendOutline(XElement trendline, ChartGroup group, ChartSeries series)
    {
        var color = SeriesOutline(group, series) is { Visible: true, Fill: SolidFill line } ? line.Color
            : PointFill(group, series, -1) is SolidFill fill ? fill.Color : AutoColor(series.Index);
        var automatic = new XElement(Ns.A + "ln", new XAttribute("w", "19050"), new XAttribute("cap", "rnd"),
            new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", color.ToHex().TrimStart('#')))),
            new XElement(Ns.A + "prstDash", new XAttribute("val", "sysDot")));
        var layers = new List<(XElement, ColorContext)> { (automatic, colors) };
        if (trendline.Element(Ns.C + "spPr")?.Element(Ns.A + "ln") is { } own)
            layers.Add((own, colors));
        return OutlineReader.Merge(layers);
    }

    /// <summary>
    /// The trendlines of a series, clipped to the plot area, with their equation and R² labels: the label block is
    /// vertically centered on the trendline's end, its right edge 8.8 pt left of it (measured), unless moved.
    /// </summary>
    string Trendlines(ChartGroup group, ChartSeries series, SeriesFrame frame, Box plot)
    {
        var svg = new StringBuilder();
        var data = DataPoints(group, series);
        foreach (var trendline in series.Element.Elements(Ns.C + "trendline"))
        {
            var label = trendline.Element(Ns.C + "trendlineLbl");
            var numFmt = label?.Element(Ns.C + "numFmt").Str("formatCode") ?? "General";
            string Number(double v) => NumberFormat.Format(numFmt == "General" ? Math.Round(v, 4) : v, numFmt, culture);
            if (Fit(trendline, data, Number) is not { } trend)
                continue;
            var outline = TrendOutline(trendline, group, series);
            List<(double X, double Y)> curve;
            if (trend.Points != null)
            {
                curve = trend.Points.Select(p => frame.Point(p.X, p.Y)).ToList();
            }
            else
            {
                var from = data.Min(p => p.X) - (ChartModel.Number(trendline, "backward") ?? 0);
                var to = data.Max(p => p.X) + (ChartModel.Number(trendline, "forward") ?? 0);
                var linear = (ChartModel.Val(trendline, "trendlineType") ?? "linear") == "linear";
                var steps = linear ? 1 : 100;
                curve = [];
                for (var k = 0; k <= steps; k++)
                {
                    var x = from + (to - from) * k / steps;
                    if (trend.At!(x) is { } y && double.IsFinite(y))
                        curve.Add(frame.Point(x, y));
                }
            }
            if (curve.Count < 2)
                continue;
            var clip = paint.NewId("c");
            defs.Append($"<clipPath id=\"{clip}\"><rect x=\"{Px(plot.X)}\" y=\"{Px(plot.Y)}\" width=\"{Px(plot.W)}\" height=\"{Px(plot.H)}\"/></clipPath>");
            svg.Append($"<g clip-path=\"url(#{clip})\">{Polyline(curve, outline, closed: false)}</g>");

            var showEquation = ChartModel.Flag(trendline, "dispEq", false) && trend.Equation.Count > 0;
            var showR2 = ChartModel.Flag(trendline, "dispRSqr", false) && trend.RSquared != null;
            if (!showEquation && !showR2)
                continue;
            var paragraphs = new List<XElement>();
            XElement Run(string value, bool superscript) => new(Ns.A + "r",
                superscript ? new XElement(Ns.A + "rPr", new XAttribute("baseline", "30000")) : null, new XElement(Ns.A + "t", value));
            if (showEquation)
                paragraphs.Add(new XElement(Ns.A + "p", trend.Equation.Select(t => Run(t.Text, t.Superscript))));
            if (showR2)
                paragraphs.Add(new XElement(Ns.A + "p", Run("R", false), Run("2", true), Run(" = " + Number(trend.RSquared!.Value), false)));
            var layers = Layers(DefRPr(label?.Element(Ns.C + "txPr")), "trendlineLabel", new XElement(Ns.A + "defRPr", new XAttribute("sz", "1000")));
            var layout = text.ChartRich(new XElement(Ns.C + "rich", paragraphs), layers, width, wrap: false);
            var style = layout.Lines.SelectMany(l => l.Runs).FirstOrDefault()?.Style ?? text.ChartStyle(layers);
            var lineHeight = LineHeight(style);
            var w = layout.Lines.Count == 0 ? 0 : layout.Lines.Max(l => l.Width);
            var h = layout.Lines.Count * lineHeight;
            var end = curve[^1];
            var (left, top) = (end.X - 8.8 - w, end.Y - h / 2);
            if (label?.Element(Ns.C + "layout")?.Element(Ns.C + "manualLayout") is { } manual)
            {
                var edge = ChartModel.Val(manual, "xMode") == "edge";
                if (ChartModel.Number(manual, "x") is { } mx) left = edge ? mx * width : left + mx * width;
                if (ChartModel.Number(manual, "y") is { } my) top = edge ? my * height : top + my * height;
            }
            dataLabels.Append(Area(label?.Element(Ns.C + "spPr"), new Box(left, top, w, h), snap: false));
            var baseline = top + Ascent(style);
            foreach (var line in layout.Lines)
            {
                dataLabels.Append(text.EmitLine(line, left + (w - line.Width) / 2, baseline));
                baseline += lineHeight;
            }
        }
        return svg.ToString();
    }

    /// <summary>The error bars of a series in a direction: x or y for scatter charts, else the value direction.</summary>
    static XElement? ErrorBars(ChartGroup group, ChartSeries series, bool alongX) =>
        series.Element.Elements(Ns.C + "errBars").FirstOrDefault(e => !group.IsScatter || (ChartModel.Val(e, "errDir") ?? "y") == (alongX ? "x" : "y"));

    /// <summary>
    /// The extent of point i's error bar in value units (catalog: chart.analysis): a fixed value, a percentage of
    /// the value, the standard error, custom values, or n standard deviations around the mean of all values (as
    /// Office draws them).
    /// </summary>
    static (double Low, double High)? ErrorRange(XElement errBars, List<double?> values, int i)
    {
        if (values.ElementAtOrDefault(i) is not { } v)
            return null;
        var kind = ChartModel.Val(errBars, "errValType") ?? "fixedVal";
        var amount = ChartModel.Number(errBars, "val") ?? (kind == "percentage" ? 5 : 1);
        var all = values.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        double Deviation()
        {
            if (all.Count < 2) return 0;
            var mean = all.Average();
            return Math.Sqrt(all.Sum(x => (x - mean) * (x - mean)) / (all.Count - 1));
        }
        double center = v, plus, minus;
        switch (kind)
        {
            case "percentage": plus = minus = Math.Abs(v) * amount / 100; break;
            case "stdDev": center = all.Average(); plus = minus = amount * Deviation(); break;
            case "stdErr": plus = minus = Deviation() / Math.Sqrt(all.Count); break;
            case "cust":
                plus = ChartModel.Numbers(errBars.Element(Ns.C + "plus")).Values.ElementAtOrDefault(i) ?? 0;
                minus = ChartModel.Numbers(errBars.Element(Ns.C + "minus")).Values.ElementAtOrDefault(i) ?? 0;
                break;
            default: plus = minus = amount; break;
        }
        var type = ChartModel.Val(errBars, "errBarType") ?? "both";
        return (type is "both" or "minus" ? center - minus : center, type is "both" or "plus" ? center + plus : center);
    }

    /// <summary>Error bars with 4.5 pt end caps (measured), drawn under the series lines and markers.</summary>
    string ErrorBarLines(ChartGroup group, ChartSeries series, SeriesFrame frame)
    {
        var svg = new StringBuilder();
        foreach (var alongX in group.IsScatter ? new[] { false, true } : [false])
        {
            if (ErrorBars(group, series, alongX) is not { } bars)
                continue;
            var outline = bars.Element(Ns.C + "spPr")?.Element(Ns.A + "ln") is { } ln ? OutlineReader.Merge([(ln, colors)])
                : new Outline(9525, new SolidFill(RgbColor.Black), "solid", "flat", "round", 8, "sng");
            var caps = !ChartModel.Flag(bars, "noEndCap", false);
            var type = ChartModel.Val(bars, "errBarType") ?? "both";
            var xValues = group.IsScatter && series.XValues.Count > 0 ? series.XValues : null;
            foreach (var (i, x, y) in DataPoints(group, series))
            {
                if (ErrorRange(bars, alongX ? xValues ?? [] : series.Values, i) is not var (low, high))
                    continue;
                var (a, b) = alongX ? (frame.Point(low, y), frame.Point(high, y)) : (frame.Point(x, low), frame.Point(x, high));
                svg.Append(Polyline([a, b], outline, closed: false));
                if (!caps)
                    continue;
                // The bar runs along the value direction; its caps across it.
                var vertical = Math.Abs(b.X - a.X) < Math.Abs(b.Y - a.Y) || a == b && frame.ValueVertical != alongX;
                foreach (var (end, show) in new[] { (a, type is "both" or "minus"), (b, type is "both" or "plus") })
                {
                    if (show)
                        svg.Append(vertical
                            ? Polyline([(end.X - 2.25, end.Y), (end.X + 2.25, end.Y)], outline, closed: false)
                            : Polyline([(end.X, end.Y - 2.25), (end.X, end.Y + 2.25)], outline, closed: false));
                }
            }
        }
        return svg.ToString();
    }

    // ---------------------------------------------------------------- surface (contour)

    /// <summary>The value axis of a 3D surface chart once laid out: its steps are the bands (measured: Word's surface).</summary>
    AxisScale? surfaceAxis;

    /// <summary>The value axis of a surface chart's bands: zero-based like value axes, but without headroom (measured).</summary>
    AxisScale SurfaceScale(ChartGroup group)
    {
        if (group.Type == "surface3DChart" && surfaceAxis is { } laidOut)
            return laidOut;
        var axis = model.Axes.FirstOrDefault(a => a.IsValue && group.AxisIds.Contains(a.Id));
        var values = group.Series.SelectMany(s => s.Values).Select(v => v ?? 0).ToList();
        var (dataMin, dataMax) = values.Count == 0 ? (0.0, 1.0) : (values.Min(), values.Max());
        var lo = axis?.Min ?? (dataMin >= 0 && dataMax - dataMin > dataMax / 6 ? 0 : dataMin);
        var hi = axis?.Max ?? dataMax;
        // 3D surfaces divide their (taller) value axis finer, like the axis of a 3D chart.
        var unit = axis?.MajorUnit is { } fixedUnit and > 0 ? fixedUnit : AxisScaling.NiceUnit(Math.Max(hi - lo, 1e-9), group.Type == "surface3DChart" ? 10 : 6);
        var min = axis?.Min ?? Math.Floor(AxisScale.Clean(lo / unit)) * unit;
        var max = axis?.Max ?? Math.Ceiling(AxisScale.Clean(hi / unit)) * unit;
        if (max <= min) max = min + unit;
        return new AxisScale(AxisScale.Clean(min), AxisScale.Clean(max), unit, unit / 5);
    }

    static List<(double Low, double High)> Bands(AxisScale scale)
    {
        var ticks = scale.Ticks().ToList();
        return ticks.Zip(ticks.Skip(1)).ToList();
    }

    string BandName(ChartGroup group, (double Low, double High) band)
    {
        var axis = model.Axes.FirstOrDefault(a => a.IsValue && group.AxisIds.Contains(a.Id));
        var format = axis is { SourceLinked: false } ? axis.FormatCode : group.Series.FirstOrDefault()?.FormatCode ?? "General";
        return NumberFormat.Format(band.Low, format, culture) + "-" + NumberFormat.Format(band.High, format, culture);
    }

    /// <summary>The fill of band <paramref name="index"/>: its c:bandFmt, else the automatic series colors.</summary>
    Fill BandFill(ChartGroup group, int index)
    {
        var bandFmt = group.Element.Element(Ns.C + "bandFmts")?.Elements(Ns.C + "bandFmt").FirstOrDefault(b => b.Element(Ns.C + "idx").Int("val") == index);
        return FillReader.FindFill(bandFmt?.Element(Ns.C + "spPr")) is { } fill ? ReadFill(fill) : new SolidFill(AutoColor(index));
    }

    /// <summary>
    /// Surface charts as contour charts (catalog: chart.surface): the grid of categories (left to right) and series
    /// (bottom to top) is cut into triangles along each cell's diagonal with the larger sum (measured), and every
    /// triangle into the value bands by linear interpolation. The series axis is labelled on the right.
    /// </summary>
    string Surface(ChartGroup group, Box outer)
    {
        var series = group.Series;
        var columns = series.Count == 0 ? 0 : series.Max(s => Math.Max(s.Values.Count, s.Categories.Count));
        var rows = series.Count;
        if (columns == 0 || rows == 0)
            return "";
        var categoryAxis = model.Axes.FirstOrDefault(a => a.Kind is "catAx" or "dateAx" && group.AxisIds.Contains(a.Id));
        var seriesAxis = model.Axes.FirstOrDefault(a => a.Kind == "serAx" && group.AxisIds.Contains(a.Id));
        var categoryStyle = Style(categoryAxis?.TxPr, "categoryAxis");
        var seriesStyle = Style(seriesAxis?.TxPr, "seriesAxis");
        var names = series.Select(s => s.Categories).FirstOrDefault(c => c.Count > 0) ?? [];
        var categoryLabels = Enumerable.Range(0, columns).Select(i => i < names.Count ? names[i] : (i + 1).ToString(culture)).ToList();
        var showCategories = categoryAxis is { Deleted: false, TickLabelPosition: not "none" };
        var showSeries = seriesAxis is { Deleted: false, TickLabelPosition: not "none" };

        // Measured layout: the first category label overhangs the left edge, the series labels stand 0.624 s right
        // of the surface and the category labels 0.624 s below it, both reaching 4.5 pt past the plot area. The
        // surface is a top view of a 3D box: categories by series in equal steps, 97.3 % of the inner plot area.
        var inner = PlotLayout()?.Box ?? new Func<Box>(() =>
        {
            double left = outer.X, right = outer.Right, bottom = outer.Bottom;
            if (showCategories)
            {
                left += Math.Max(6.6, Width(categoryLabels[0], categoryStyle) / 2 - 1.6);
                bottom -= LineHeight(categoryStyle) + CategoryLabelGap * categoryStyle.Size - LabelBleed;
            }
            if (showSeries)
                right -= series.Max(s => Width(s.Name, seriesStyle)) + CategoryLabelGap * seriesStyle.Size - LabelBleed;
            return new Box(left, outer.Y, Math.Max(1, right - left), Math.Max(1, bottom - outer.Y));
        })();
        var unit = Math.Min(inner.W / columns, inner.H / rows) * 0.973;
        var plot = new Box(inner.X + (inner.W - unit * columns) / 2, inner.Y + (inner.H - unit * rows) / 2, unit * columns, unit * rows);
        double X(int i) => columns > 1 ? plot.X + i * plot.W / (columns - 1) : plot.X + plot.W / 2;
        double Y(int k) => rows > 1 ? plot.Bottom - k * plot.H / (rows - 1) : plot.Y + plot.H / 2;
        double Value(int k, int i) => series[k].Values.ElementAtOrDefault(i) ?? 0;

        var scale = SurfaceScale(group);
        var bands = Bands(scale);
        var fills = Enumerable.Range(0, bands.Count).Select(b => BandFill(group, b)).ToList();
        var svg = new StringBuilder();

        // Facets are lit like PowerPoint's 3D surface (fitted to its rendering): 0.52 + 0.595 n·l for light from
        // the upper right at 31.1° elevation, and a specular highlight. The box seen from the top is as high as
        // 0.6 × its base scaled by the inner plot area's height over its width (measured with tilted planes in
        // square, wide and tall charts: the same slope shades darker the taller the plot area).
        var height = 0.6 * plot.H * (inner.H / Math.Max(1e-9, inner.W)) / Math.Max(1e-9, scale.Max - scale.Min);
        var elevation = 31.1 * Math.PI / 180;
        var light = (X: Math.Cos(elevation) * Math.Sqrt(0.5), Y: Math.Cos(elevation) * Math.Sqrt(0.5), Z: Math.Sin(elevation));
        var halfLength = Math.Sqrt(light.X * light.X + light.Y * light.Y + (light.Z + 1) * (light.Z + 1));
        var half = (X: light.X / halfLength, Y: light.Y / halfLength, Z: (light.Z + 1) / halfLength);
        void Triangle((double X, double Y, double V) a, (double X, double Y, double V) b, (double X, double Y, double V) c)
        {
            // The value's gradient over the facet (y up) gives its normal.
            double ux = b.X - a.X, uy = a.Y - b.Y, wx = c.X - a.X, wy = a.Y - c.Y;
            var det = ux * wy - wx * uy;
            if (Math.Abs(det) < 1e-12)
                return;
            var gx = ((b.V - a.V) * wy - (c.V - a.V) * uy) / det;
            var gy = ((c.V - a.V) * ux - (b.V - a.V) * wx) / det;
            var (nx, ny) = (-height * gx, -height * gy);
            var length = Math.Sqrt(nx * nx + ny * ny + 1);
            // Facets turned away from the light keep darkening (measured: 0.4 at n·l = −0.18).
            var diffuse = Math.Max(0, 0.52 + 0.595 * (nx * light.X + ny * light.Y + light.Z) / length);
            var specular = Math.Min(0.15, 0.278 * Math.Pow(Math.Max(0, (nx * half.X + ny * half.Y + half.Z) / length), 26.8));
            List<(double X, double Y, double V)> triangle = [a, b, c];
            for (var band = 0; band < bands.Count; band++)
            {
                var (low, high) = bands[band];
                // The lowest and highest bands also take values beyond the scale.
                var part = band == 0 ? triangle : Clip(triangle, low, above: true);
                if (band < bands.Count - 1) part = Clip(part, high, above: false);
                if (part.Count < 3)
                    continue;
                var d = new StringBuilder();
                for (var p = 0; p < part.Count; p++)
                    d.Append(p == 0 ? 'M' : 'L').Append(Px(part[p].X)).Append(' ').Append(Px(part[p].Y));
                if (fills[band] is SolidFill solid)
                {
                    static double Channel(double v, double diffuse, double specular) => Math.Clamp(v * diffuse + specular, 0, 1);
                    var lit = new RgbColor(Channel(solid.Color.R, diffuse, specular), Channel(solid.Color.G, diffuse, specular), Channel(solid.Color.B, diffuse, specular), solid.Color.A);
                    // A hairline in the facet's color closes the seams between neighbouring pieces.
                    svg.Append($"<path d=\"{d}Z\"{paint.Fill(new SolidFill(lit), 0, 0, defs)} stroke=\"{lit.ToHex()}\" stroke-width=\"0.5\" stroke-linejoin=\"round\"/>");
                }
                else
                {
                    svg.Append($"<path d=\"{d}Z\"{paint.Fill(fills[band], plot.W * PxPerPt, plot.H * PxPerPt, defs)}/>");
                }
            }
        }
        for (var k = 0; k + 1 < rows; k++)
        {
            for (var i = 0; i + 1 < columns; i++)
            {
                var bl = (X(i), Y(k), Value(k, i));
                var br = (X(i + 1), Y(k), Value(k, i + 1));
                var tl = (X(i), Y(k + 1), Value(k + 1, i));
                var tr = (X(i + 1), Y(k + 1), Value(k + 1, i + 1));
                if (bl.Item3 + tr.Item3 >= tl.Item3 + br.Item3)
                {
                    Triangle(bl, br, tr);
                    Triangle(bl, tr, tl);
                }
                else
                {
                    Triangle(tl, bl, br);
                    Triangle(tl, br, tr);
                }
            }
        }

        // Axis lines with their tick marks along the bottom (categories) and the right edge (series), then labels.
        if (categoryAxis is { Deleted: false } && AxisOutline(categoryAxis) is { Visible: true } categoryLine)
        {
            svg.Append(GridLine(categoryLine, plot.X, plot.Bottom, plot.Right, plot.Bottom));
            if (categoryAxis.MajorTickMark is "out" or "cross")
                for (var i = 0; i < columns; i++)
                    svg.Append(GridLine(categoryLine, X(i), plot.Bottom, X(i), plot.Bottom + TickLength));
        }
        if (seriesAxis is { Deleted: false } && AxisOutline(seriesAxis) is { Visible: true } seriesLine)
        {
            svg.Append(GridLine(seriesLine, plot.Right, plot.Y, plot.Right, plot.Bottom));
            if (seriesAxis.MajorTickMark is "out" or "cross")
                for (var k = 0; k < rows; k++)
                    svg.Append(GridLine(seriesLine, plot.Right, Y(k), plot.Right + TickLength, Y(k)));
        }
        if (showCategories)
        {
            for (var i = 0; i < columns; i++)
            {
                var w = Width(categoryLabels[i], categoryStyle);
                svg.Append(text.Label(categoryLabels[i], categoryStyle, X(i) - w / 2, plot.Bottom + CategoryLabelGap * categoryStyle.Size + Ascent(categoryStyle)));
            }
        }
        if (showSeries)
        {
            for (var k = 0; k < rows; k++)
                svg.Append(text.Label(series[k].Name, seriesStyle, plot.Right + CategoryLabelGap * seriesStyle.Size, CenteredBaseline(seriesStyle, Y(k))));
        }
        return svg.ToString();
    }

    /// <summary>Clips a polygon with values at its corners to where the (linearly interpolated) value is above or below a level.</summary>
    static List<(double X, double Y, double V)> Clip(List<(double X, double Y, double V)> polygon, double level, bool above)
    {
        var result = new List<(double X, double Y, double V)>();
        for (var i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];
            var currentIn = above ? current.V >= level : current.V <= level;
            var nextIn = above ? next.V >= level : next.V <= level;
            if (currentIn)
                result.Add(current);
            if (currentIn != nextIn && next.V != current.V)
            {
                var f = (level - current.V) / (next.V - current.V);
                result.Add((current.X + f * (next.X - current.X), current.Y + f * (next.Y - current.Y), level));
            }
        }
        return result;
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
        if (PlotLayout()?.Box is { } manual)
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
        // A radar divides its radius into intervals at least 1.2 font sizes long (measured: PowerPoint steps a radius of
        // 109 pt with 18 pt labels by 2 up to 10, Word one of 82 pt with 9 pt labels by 5 up to 35).
        ScaleValueAxis(value, radius, interval: 1.2 * value.Style.Size);
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
                    svg.Append(ShapePart(series, -1, $"<path d=\"{d}Z\"{FillOver(PointFill(group, series, -1), Bounds(corners))}{paint.Stroke(outline, 0, 0, defs)}/>"));
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
                            svg.Append(ShapePart(series, i, DrawMarker(marker, series.Index, p.X, p.Y, PointFill(group, series, i), outline)));
                    }
                }
                for (var i = 0; i < points.Count; i++)
                {
                    if (points[i] is { } p && series.Values.ElementAtOrDefault(i) is { } raw && DataLabel(group, series, i) is { } spec)
                        LabelAtPoint(spec, LabelText(spec, series, i, raw, 0), p.X, p.Y, spec.Position ?? "t");
                }
            }
        }

        // Charts without style part draw spokes only where the axis has a line of its own (measured).
        if (!valueAxis.Deleted && (legacy == null || valueAxis.SpPr?.Element(Ns.A + "ln") != null) && AxisOutline(valueAxis) is { Visible: true } spokes)
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
        if (group.Type == "ofPieChart")
            return OfPie(group, plot);
        var svg = new StringBuilder();
        var doughnut = group.Type == "doughnutChart";
        var rings = doughnut ? group.Series : group.Series.Take(1).ToList();
        if (rings.Count == 0)
            return "";
        var maxExplosion = rings.SelectMany(s => s.Points.Values.Select(p => ChartModel.Number(p, "explosion") ?? 0)
            .Append(ChartModel.Number(s.Element, "explosion") ?? 0)).DefaultIfEmpty(0).Max() / 100;
        // With data labels that may go outside (outEnd, bestFit) PowerPoint keeps a margin around the pie for them;
        // inside labels (inEnd, ctr) and doughnut labels leave it its full size (measured with local/pielabels). The
        // series' labels decide, not single points' (measured: centered labels with some at bestFit, LibreOffice's
        // tdf125444 and percentage-number-formats).
        var labelled = rings.Select(r => DataLabel(group, r, -1))
            .FirstOrDefault(l => l != null && (doughnut || (l.Position ?? "bestFit") is "bestFit" or "outEnd"));
        var margin = labelled != null && PlotLayout() == null ? PieLabelMargin * labelled.Style.Size : 0;
        // A plot area placed by hand around the pie itself (layoutTarget inner) holds the pie with its series' explosion,
        // single exploded slices reach beyond it (measured: LibreOffice's tdf146756_bestFit, one slice exploded by 17 %,
        // and tdf146487, all by 8 %).
        var seriesExplosion = rings.Select(s => ChartModel.Number(s.Element, "explosion") ?? 0).DefaultIfEmpty(0).Max() / 100;
        var radius = Math.Max(1, Math.Min(plot.W, plot.H) / 2 - margin) / (1 + (PlotLayout() is { Inner: true } ? seriesExplosion : maxExplosion));
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
                svg.Append(ShapePart(series, i, $"<path d=\"{Slice(cx + ox, cy + oy, outer, inner, angle, sweep)}\"{FillOver(PointFill(group, series, i), SliceBounds(cx + ox, cy + oy, outer, inner, angle, sweep))}{paint.Stroke(PointOutline(series, i), 0, 0, defs)}/>"));
                if (DataLabel(group, series, i) is { } spec)
                {
                    if (doughnut)
                    {
                        // Doughnut labels sit in the middle of the ring.
                        var label = LabelText(spec, series, i, series.Values[i] ?? 0, total);
                        var (w, h) = LabelSize(spec, label);
                        var (dx, dy) = (Math.Sin(mid), -Math.Cos(mid));
                        var placed = Moved(spec, new Box(cx + ox + dx * (outer + inner) / 2 - w / 2, cy + oy + dy * (outer + inner) / 2 - h / 2, w, h));
                        DrawLabel(spec, label, placed.X, placed.Y);
                    }
                    else
                        PieLabel(spec, series, i, series.Values[i] ?? 0, total, cx + ox, cy + oy, outer, angle, sweep);
                }
                angle += sweep;
            }
        }
        return svg.ToString();
    }

    /// <summary>
    /// Pie of pie and bar of pie (c:ofPieChart, catalog: chart.pie): the points the split puts aside (splitType pos:
    /// the last splitPos, 2 by default; val and percent: those below splitPos; cust: c:secondPiePt) are drawn as a second
    /// pie or a stacked bar beside the main pie, which shows them as one slice (formatted by the extra data point)
    /// centered towards it; the second plot is secondPieSize % of the main pie high, a bar half as wide as high, the gap
    /// between them gapWidth % of a constant share of the main pie, the second pie's slices start (clockwise) where the
    /// combined slice ends, the bar's first point on top; series lines join them (measured with generated/charts-details).
    /// </summary>
    string OfPie(ChartGroup group, Box plot)
    {
        var svg = new StringBuilder();
        if (group.Series.FirstOrDefault() is not { } series)
            return "";
        var values = series.Values.Select(v => Math.Abs(v ?? 0)).ToList();
        var n = values.Count;
        var bar = (group.Element.Element(Ns.C + "ofPieType")?.Attribute("val")?.Value ?? "pie") == "bar";
        var splitType = group.Element.Element(Ns.C + "splitType")?.Attribute("val")?.Value ?? "auto";
        var splitPos = ChartModel.Number(group.Element, "splitPos");
        var total = values.Sum();
        var second = new HashSet<int>();
        switch (splitType)
        {
            case "cust":
                foreach (var point in group.Element.Element(Ns.C + "custSplit")?.Elements(Ns.C + "secondPiePt") ?? [])
                {
                    if (int.TryParse(point.Attribute("val")?.Value, out var index) && index >= 0 && index < n)
                        second.Add(index);
                }
                break;
            case "val":
                for (var i = 0; i < n; i++)
                    if (values[i] < (splitPos ?? 0)) second.Add(i);
                break;
            case "percent":
                for (var i = 0; i < n; i++)
                    if (total > 0 && values[i] / total * 100 < (splitPos ?? 0)) second.Add(i);
                break;
            default:
                var count = (int)Math.Clamp(splitPos ?? 2, 0, n);
                for (var i = n - count; i < n; i++)
                    second.Add(i);
                break;
        }
        var other = second.Sum(i => values[i]);
        if (total <= 0)
            return "";

        // Sizes relative to the main pie's diameter, fitted to the plot area and centered.
        var size = Math.Clamp(ChartModel.Number(group.Element, "secondPieSize") ?? 75, 5, 200) / 100;
        var gapWidth = (ChartModel.Number(group.Element, "gapWidth") ?? 100) / 100;
        var (secondWidth, gap) = bar ? (size / 2, 0.259 * gapWidth) : (size, 0.514 * gapWidth);
        var diameter = Math.Max(1, Math.Min(plot.W / (1 + gap + secondWidth), plot.H));
        var left = plot.X + (plot.W - diameter * (1 + gap + secondWidth)) / 2;
        var cy = plot.Y + plot.H / 2;
        var (cx, r) = (left + diameter / 2, diameter / 2);

        // The main pie: the combined slice centered towards the second plot (90°, clockwise from the top).
        var otherSweep = other / total * 360;
        var angle = 90 + otherSweep / 2;
        for (var i = 0; i < n; i++)
        {
            if (second.Contains(i) || values[i] <= 0)
                continue;
            var sweep = values[i] / total * 360;
            svg.Append(ShapePart(series, i, $"<path d=\"{Slice(cx, cy, r, 0, angle, sweep)}\"{FillOver(PointFill(group, series, i), SliceBounds(cx, cy, r, 0, angle, sweep))}{paint.Stroke(PointOutline(series, i), 0, 0, defs)}/>"));
            angle += sweep;
        }
        if (other > 0)
            svg.Append(ShapePart(series, n, $"<path d=\"{Slice(cx, cy, r, 0, 90 - otherSweep / 2, otherSweep)}\"{FillOver(PointFill(group, series, n), SliceBounds(cx, cy, r, 0, 90 - otherSweep / 2, otherSweep))}{paint.Stroke(PointOutline(series, n), 0, 0, defs)}/>"));

        // The second plot.
        var secondLeft = left + diameter * (1 + gap);
        var height = size * diameter;
        (double X, double Y) top, bottom;
        if (bar)
        {
            var w = secondWidth * diameter;
            var y = cy - height / 2;
            foreach (var i in second.OrderBy(i => i))
            {
                var h = other > 0 ? values[i] / other * height : 0;
                svg.Append(ShapePart(series, i, Rect(new Box(secondLeft, y, w, h), PointFill(group, series, i), PointOutline(series, i), snap: true)));
                y += h;
            }
            (top, bottom) = ((secondLeft, cy - height / 2), (secondLeft, cy + height / 2));
        }
        else
        {
            var (cx2, r2) = (secondLeft + height / 2, height / 2);
            var a = 90 + otherSweep / 2;
            foreach (var i in second.OrderBy(i => i))
            {
                var sweep = other > 0 ? values[i] / other * 360 : 0;
                svg.Append(ShapePart(series, i, $"<path d=\"{Slice(cx2, cy, r2, 0, a, sweep)}\"{FillOver(PointFill(group, series, i), SliceBounds(cx2, cy, r2, 0, a, sweep))}{paint.Stroke(PointOutline(series, i), 0, 0, defs)}/>"));
                a += sweep;
            }
            (top, bottom) = ((cx2, cy - r2), (cx2, cy + r2));
        }

        // Series lines from the combined slice's edges to the second plot's top and bottom.
        if (other > 0 && group.Element.Element(Ns.C + "serLines") is { } lines)
        {
            var outline = lines.Element(Ns.C + "spPr")?.Element(Ns.A + "ln") is { } ln
                ? OutlineReader.Merge([(ln, colors)])
                : new Outline(9525, new SolidFill(RgbColor.Black), "solid", "flat", "round", 8, "sng");
            if (outline is { Visible: true })
            {
                (double X, double Y) Edge(double degrees) => (cx + Math.Sin(degrees * Math.PI / 180) * r, cy - Math.Cos(degrees * Math.PI / 180) * r);
                var (e0, e1) = (Edge(90 - otherSweep / 2), Edge(90 + otherSweep / 2));
                svg.Append($"<path d=\"M{Px(e0.X)} {Px(e0.Y)}L{Px(top.X)} {Px(top.Y)}M{Px(e1.X)} {Px(e1.Y)}L{Px(bottom.X)} {Px(bottom.Y)}\" fill=\"none\"{paint.Stroke(outline, 0, 0, defs)}/>");
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
