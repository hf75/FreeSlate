using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// Charts as the notation writes them (see Prompt): a fenced block ```chart type=column title="…" holding a GitHub table, the categories (or
/// the x values) down its first column and a series in each other one, its head naming them. As Word keeps one: a chart part whose series
/// refer to the cells of a workbook embedded with it, their values cached, so that Word shows the chart and edits its data in Excel (and the
/// document draws it as Word does, see Docx.Drawings): DrawingML's c:chartSpace for Word's classic charts (columns, bars, lines, areas, pies
/// and doughnuts, pies of pies, scatter, bubbles, radar, stock, surfaces; in 3D, stacked, combined with lines and a second axis), Office
/// 2016's cx:chartSpace for the others (waterfall, funnel, treemap, sunburst, histogram, Pareto, box and whisker), behind markup
/// compatibility as Word writes them. In a line of its own or floating beside the paragraph after it, as wide as the text unless its width
/// says so. Read back as the block it was written as (see Block).
/// </summary>
static class Charts
{
    const double EmuPerPoint = 12700;
    const string C = "http://schemas.openxmlformats.org/drawingml/2006/chart";
    const string Cx = "http://schemas.microsoft.com/office/drawing/2014/chartex";

    /// <summary>The series' colours: calm ones that go with the tones, in turn.</summary>
    static readonly string[] Colors = ["2F6DB5", "E07B39", "4C9A5F", "8C5BB8", "C8484B", "2A9DA8", "C9A227", "6B7280"];

    /// <summary>colors=… of one hue: its shades, darkest first, for the series in turn (as SmartArt's hues, see SmartArts).</summary>
    static readonly Dictionary<string, string[]> Hues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["blue"] = ["1F4E8C", "2F6DB5", "6E98CF", "A9C2E4", "D4E1F2"],
        ["green"] = ["336B41", "4C9A5F", "7DB88D", "B0D5B9", "D9EBDE"],
        ["orange"] = ["A9541F", "E07B39", "EBA272", "F3C7A8", "F9E3D3"],
        ["violet"] = ["62408A", "8C5BB8", "AF8DCE", "D0BDE3", "E9DFF2"],
        ["rose"] = ["923236", "C8484B", "D87D7F", "E8B0B1", "F4D9DA"],
        ["teal"] = ["1C6F77", "2A9DA8", "62BAC2", "9DD5DA", "CFEBED"],
        ["gray"] = ["3F444D", "6B7280", "989EA9", "C3C7CE", "E2E4E8"],
    };

    /// <summary>The classic chart types (c:chartSpace) and Office 2016's (cx:chartSpace) as the notation names them.</summary>
    public static readonly string[] Classic = ["column", "bar", "line", "area", "pie", "doughnut", "pie-of-pie", "bar-of-pie", "scatter", "bubble", "radar", "stock", "surface"];
    public static readonly string[] Modern = ["waterfall", "funnel", "treemap", "sunburst", "histogram", "pareto", "boxwhisker"];

    /// <summary>A type as the notation may name it, by the name the notation reads back.</summary>
    static string Kind(string? given) => given?.Trim().ToLowerInvariant().Replace(' ', '-').Replace('_', '-') switch
    {
        null or "" => "column",
        "columns" or "col" => "column",
        "bars" => "bar",
        "lines" => "line",
        "areas" => "area",
        "donut" => "doughnut",
        "pieofpie" or "pie-pie" => "pie-of-pie",
        "barofpie" or "pie-bar" => "bar-of-pie",
        "xy" or "scatter-plot" or "scatterplot" => "scatter",
        "bubbles" => "bubble",
        "spider" or "web" => "radar",
        "ohlc" or "hlc" or "candlestick" or "stocks" => "stock",
        "contour" or "surface-3d" => "surface",
        "box" or "boxplot" or "box-plot" or "box-whisker" or "box-and-whisker" => "boxwhisker",
        "tree-map" => "treemap",
        "sun-burst" => "sunburst",
        "histograms" => "histogram",
        var kind => kind,
    };

    /// <summary>A chart made at a place (see Markup.Made): its drawing's run in a line of its own (or floating), its chart part and workbook.</summary>
    public static Markup.Made Of(Notation.Fenced chart, Markup.Place place)
    {
        var settings = chart.Settings;
        var type = Kind(settings.GetValueOrDefault("type"));
        if (!Classic.Contains(type) && !Modern.Contains(type))
            return new("", [], [], Problem: $"type={type} is no chart type ({string.Join(", ", Classic.Concat(Modern))}).");
        if (Notation.Parse(chart.Body, final: true).Blocks.OfType<Notation.Table>().FirstOrDefault() is not { } table || table.Texts.Count < 2 || table.Aligns.Count < 1)
            return new("", [], [], Problem: "A chart holds a table: | category | series … | then |---|---| and a row of values for each category.");
        var head = table.Texts[0].ToList();
        var rows = table.Texts.Skip(1).Select(r => Enumerable.Range(0, head.Count).Select(k => k < r.Count ? r[k] : "").ToList()).ToList();
        var language = CultureInfo.CurrentCulture.Name is { Length: > 0 } name ? name : "en-US";
        var modern = Modern.Contains(type);
        var (part, problem) = modern ? ChartEx(type, settings, head, rows) : ChartPart(type, settings, head, rows, language);
        if (problem != null)
            return new("", [], [], Problem: problem);
        var width = Math.Clamp(Markup.Length(settings.GetValueOrDefault("width"), place.TextWidth) ?? place.TextWidth, 72, place.TextWidth);
        var round = type is "pie" or "doughnut" or "sunburst" || type == "radar";
        var height = Markup.Length(settings.GetValueOrDefault("height"), place.TextWidth) ?? width * (round ? 0.62 : type is "pie-of-pie" or "bar-of-pie" ? 0.5 : 0.58);
        var (cx, cy) = ((long)Math.Round(width * EmuPerPoint), (long)Math.Round(height * EmuPerPoint));
        var alt = settings.GetValueOrDefault("alt") ?? settings.GetValueOrDefault("title") ?? "";
        var inline = $"<wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>"
            + $"<wp:docPr id=\"1\" name=\"Diagramm\" descr=\"{Escape(alt)}\"/><wp:cNvGraphicFramePr/>"
            + (modern
                ? "<a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/drawing/2014/chartex\"><cx:chart xmlns:cx=\"" + Cx + "\" r:id=\"{chart}\"/></a:graphicData></a:graphic>"
                : "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"><c:chart r:id=\"{chart}\"/></a:graphicData></a:graphic>")
            + "</wp:inline>";
        // Office 2016's charts stand behind markup compatibility, as Word writes them (Word 2013 and older show nothing there).
        var run = modern
            ? "<w:r><mc:AlternateContent xmlns:cx1=\"http://schemas.microsoft.com/office/drawing/2015/9/8/chartex\"><mc:Choice Requires=\"cx1\"><w:drawing>" + inline + "</w:drawing></mc:Choice></mc:AlternateContent></w:r>"
            : "<w:r><w:drawing>" + inline + "</w:drawing></w:r>";
        var workbook = Workbook(head, rows);
        NewPart[] parts = modern
            ?
            [
                new("chart", "/word/charts/chartEx1.xml", "application/vnd.ms-office.chartex+xml", "http://schemas.microsoft.com/office/2014/relationships/chartEx", Encoding.UTF8.GetBytes(part!)),
                new("workbook", "/word/embeddings/Microsoft_Excel_Worksheet1.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/package", workbook, From: "chart"),
                // Word opens an Office 2016 chart only with a chart style and a colour style beside it (measured); ours are as small as the
                // schema lets them be.
                new("style", "/word/charts/style1.xml", "application/vnd.ms-office.chartstyle+xml", "http://schemas.microsoft.com/office/2011/relationships/chartStyle",
                    Encoding.UTF8.GetBytes(ChartStyle()), From: "chart"),
                new("colors", "/word/charts/colors1.xml", "application/vnd.ms-office.chartcolorstyle+xml", "http://schemas.microsoft.com/office/2011/relationships/chartColorStyle",
                    Encoding.UTF8.GetBytes(ColorStyle()), From: "chart"),
            ]
            :
            [
                new("chart", "/word/charts/chart1.xml", "application/vnd.openxmlformats-officedocument.drawingml.chart+xml",
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart", Encoding.UTF8.GetBytes(part!)),
                new("workbook", "/word/embeddings/Microsoft_Excel_Worksheet1.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/package", workbook, From: "chart"),
            ];
        return Markup.Placed(run, parts, settings, place);
    }

    /// <summary>A number as a table's cell gives it: a decimal comma or point, thousands' separators, a unit or a percent sign aside; null for none.</summary>
    static double? Number(string text)
    {
        var kept = new string([.. text.Where(c => char.IsDigit(c) || c is ',' or '.' or '-' or '−')]).Replace('−', '-');
        if (kept.Length == 0 || !kept.Any(char.IsDigit))
            return null;
        if (kept.Contains(',') && kept.Contains('.'))
            kept = kept.LastIndexOf(',') > kept.LastIndexOf('.') ? kept.Replace(".", "", StringComparison.Ordinal).Replace(',', '.') : kept.Replace(",", "", StringComparison.Ordinal);
        else if (kept.Count(c => c == ',') == 1)
            kept = kept.Replace(',', '.');
        else if (kept.Count(c => c == '.') > 1)
            kept = kept.Replace(".", "", StringComparison.Ordinal);
        return double.TryParse(kept, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    static string Escape(string text) => SecurityElement.Escape(text) ?? "";

    static string Column(int index) => index < 26 ? ((char)('A' + index)).ToString() : ((char)('A' + index / 26 - 1)).ToString() + (char)('A' + index % 26);

    static string Value(double? value) => value?.ToString("R", CultureInfo.InvariantCulture) ?? "";

    static bool Yes(string? setting) => setting?.Trim().ToLowerInvariant() is "yes" or "true" or "on" or "1";

    /// <summary>Names a setting lists (comma-separated), as the table's head has them.</summary>
    static HashSet<string> Names(string? setting) =>
        new((setting ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);

    /// <summary>The colour of the n-th of so many series: the charts' colours in turn, or the shades of the hue colors=… names.</summary>
    static string Color(IReadOnlyDictionary<string, string> settings, int index, int count)
    {
        if (settings.GetValueOrDefault("colors") is { } named && Hues.TryGetValue(named.Trim(), out var shades))
        {
            if (count <= 1)
                return shades[1];
            var at = Math.Min(shades.Length - 1, (int)Math.Round(index * (shades.Length - 2.0) / Math.Max(1, count - 1)));
            return shades[at];
        }
        return Colors[index % Colors.Length];
    }

    /// <summary>A series as the table gives it: the column it stands in, its name, its values; a bubble's sizes beside them.</summary>
    sealed record Series(int Column, string Name, List<double?> Values, int SizeColumn = -1, List<double?>? Sizes = null);

    /// <summary>The value axis' settings: its least and greatest value, logarithmic, its numbers' format, its gridlines.</summary>
    sealed record Scale(double? Min, double? Max, bool Log, string? Format, string Gridlines);

    /// <summary>
    /// The classic chart part: the chart groups of its type (a combination's lines and second axis among them), their series referring to
    /// the workbook's cells with their values cached, the axes, the title, legend and data table. Null with a problem where the table does
    /// not give what the type needs.
    /// </summary>
    static (string? Part, string? Problem) ChartPart(string type, IReadOnlyDictionary<string, string> settings, List<string> head, List<List<string>> rows, string language)
    {
        var n = rows.Count;
        var threeD = Yes(settings.GetValueOrDefault("3d"));
        var stacked = settings.GetValueOrDefault("stacked")?.Trim().ToLowerInvariant() switch { "yes" or "true" => "stacked", "percent" or "100" or "100%" => "percentStacked", _ => null };
        var labels = settings.GetValueOrDefault("labels")?.Trim().ToLowerInvariant() is "yes" or "true" or "values" or "percent";
        var format = settings.GetValueOrDefault("format") is { Length: > 0 } f ? f : null;
        var scale = new Scale(Number(settings.GetValueOrDefault("min") ?? ""), Number(settings.GetValueOrDefault("max") ?? ""), Yes(settings.GetValueOrDefault("log")),
            format ?? (stacked == "percentStacked" ? "0%" : null), settings.GetValueOrDefault("gridlines")?.Trim().ToLowerInvariant() ?? "major");
        var trends = Trends(settings.GetValueOrDefault("trend"));
        var errors = Yes(settings.GetValueOrDefault("errors"));
        var categories = rows.Select(r => r[0]).ToList();
        List<Series> series;
        switch (type)
        {
            case "bubble":
            {
                series = [];
                for (var c = 1; c + 1 < head.Count; c += 2)
                    series.Add(new(c, head[c], rows.Select(r => Number(r[c])).ToList(), c + 1, rows.Select(r => Number(r[c + 1])).ToList()));
                if (series.Count == 0)
                    return (null, "A bubble chart's table holds x, then for each series its y and its size: | x | Sales | Sales size |.");
                break;
            }
            default:
                series = Enumerable.Range(1, head.Count - 1).Select(c => new Series(c, head[c], rows.Select(r => Number(r[c])).ToList())).ToList();
                if (series.Count == 0)
                    return (null, "A chart's table holds the categories in its first column and a series in each other one.");
                break;
        }
        if (series.All(s => s.Values.All(v => v == null)))
            return (null, "The chart's table holds no numbers below its head.");
        if (type is "scatter" or "bubble" && categories.Any(c => c.Length > 0 && Number(c) == null))
            return (null, $"A {type} chart's first column holds the x values, numbers.");

        // Series parts shared by the chart types.
        string Tx(Series s) => $"<c:tx><c:strRef><c:f>Sheet1!${Column(s.Column)}$1</c:f><c:strCache><c:ptCount val=\"1\"/><c:pt idx=\"0\"><c:v>{Escape(s.Name)}</c:v></c:pt></c:strCache></c:strRef></c:tx>";
        string Cat() => $"<c:cat><c:strRef><c:f>Sheet1!$A$2:$A${n + 1}</c:f><c:strCache><c:ptCount val=\"{n}\"/>"
            + string.Concat(categories.Select((c, k) => $"<c:pt idx=\"{k}\"><c:v>{Escape(c)}</c:v></c:pt>")) + "</c:strCache></c:strRef></c:cat>";
        string Numbers(string element, int column, List<double?> values) => $"<{element}><c:numRef><c:f>Sheet1!${Column(column)}$2:${Column(column)}${n + 1}</c:f><c:numCache><c:formatCode>General</c:formatCode><c:ptCount val=\"{n}\"/>"
            + string.Concat(values.Select((v, k) => v is { } value ? $"<c:pt idx=\"{k}\"><c:v>{Value(value)}</c:v></c:pt>" : "")) + $"</c:numCache></c:numRef></{element}>";
        string XValues() => Numbers("c:xVal", 0, categories.Select(Number).ToList());
        string Fill(string color, int? alpha = null) => $"<c:spPr><a:solidFill><a:srgbClr val=\"{color}\">{(alpha is { } a ? $"<a:alpha val=\"{a}\"/>" : "")}</a:srgbClr></a:solidFill><a:ln><a:noFill/></a:ln></c:spPr>";
        string Stroke(string color, bool visible = true) => visible
            ? $"<c:spPr><a:ln w=\"28575\" cap=\"rnd\"><a:solidFill><a:srgbClr val=\"{color}\"/></a:solidFill><a:round/></a:ln></c:spPr>"
            : "<c:spPr><a:ln w=\"28575\" cap=\"rnd\"><a:noFill/><a:round/></a:ln></c:spPr>";
        string Marker(string color, bool shown, int size = 5) => shown
            ? $"<c:marker><c:symbol val=\"circle\"/><c:size val=\"{size}\"/><c:spPr><a:solidFill><a:srgbClr val=\"{color}\"/></a:solidFill><a:ln w=\"9525\"><a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill></a:ln></c:spPr></c:marker>"
            : "<c:marker><c:symbol val=\"none\"/></c:marker>";
        string Labels(bool percent = false) => "<c:dLbls>" + (format != null && !percent ? $"<c:numFmt formatCode=\"{Escape(format)}\" sourceLinked=\"0\"/>" : "")
            + "<c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr><c:showLegendKey val=\"0\"/>"
            + $"<c:showVal val=\"{(percent ? 0 : 1)}\"/><c:showCatName val=\"0\"/><c:showSerName val=\"0\"/><c:showPercent val=\"{(percent ? 1 : 0)}\"/><c:showBubbleSize val=\"0\"/></c:dLbls>";
        string Trend(Series s, string color) => trends.TryGetValue(s.Name, out var kind) || trends.TryGetValue("", out kind)
            ? $"<c:trendline><c:spPr><a:ln w=\"19050\" cap=\"rnd\"><a:solidFill><a:srgbClr val=\"{color}\"/></a:solidFill><a:prstDash val=\"sysDash\"/></a:ln></c:spPr><c:trendlineType val=\"{kind}\"/>"
              + (kind == "poly" ? "<c:order val=\"2\"/>" : kind == "movingAvg" ? "<c:period val=\"2\"/>" : "") + "<c:dispRSqr val=\"0\"/><c:dispEq val=\"0\"/></c:trendline>"
            : "";
        string Errors(bool xy) => errors
            ? "<c:errBars>" + (xy ? "<c:errDir val=\"y\"/>" : "") + "<c:errBarType val=\"both\"/><c:errValType val=\"stdErr\"/><c:noEndCap val=\"0\"/>"
              + "<c:spPr><a:ln w=\"9525\"><a:solidFill><a:srgbClr val=\"404040\"/></a:solidFill></a:ln></c:spPr></c:errBars>"
            : "";
        string Ids(params int[] ids) => string.Concat(ids.Select(id => $"<c:axId val=\"{500000000 + id}\"/>"));

        var groups = new StringBuilder();
        var axes = new StringBuilder();
        var view3D = "";
        var count = series.Count;
        var lineNames = Names(settings.GetValueOrDefault("line"));
        var rightNames = Names(settings.GetValueOrDefault("right"));
        var legendDefault = count > 1;
        switch (type)
        {
            case "column" or "bar" or "line" or "area" when threeD:
            {
                var colors = series.Select((_, k) => Color(settings, k, count)).ToList();
                var body = new StringBuilder();
                for (var k = 0; k < count; k++)
                {
                    var s = series[k];
                    body.Append($"<c:ser><c:idx val=\"{k}\"/><c:order val=\"{k}\"/>").Append(Tx(s));
                    body.Append(type == "line" ? Stroke(colors[k]).Replace("<c:spPr>", "<c:spPr><a:solidFill><a:srgbClr val=\"" + colors[k] + "\"/></a:solidFill>") : Fill(colors[k]));
                    if (type is "column" or "bar")
                        body.Append("<c:invertIfNegative val=\"0\"/>");
                    if (labels)
                        body.Append(Labels());
                    body.Append(Cat()).Append(Numbers("c:val", s.Column, s.Values));
                    if (type == "line")
                        body.Append("<c:smooth val=\"0\"/>");
                    body.Append("</c:ser>");
                }
                var grouping = stacked ?? (type is "column" or "bar" ? "clustered" : "standard");
                var depth = type is "line" || type == "area" && stacked == null || type is "column" && stacked == null && Yes(settings.GetValueOrDefault("depth"));
                if (type is "column" or "bar")
                {
                    var shape = settings.GetValueOrDefault("shape")?.Trim().ToLowerInvariant() switch { "cylinder" => "cylinder", "cone" => "cone", "pyramid" => "pyramid", _ => "box" };
                    groups.Append($"<c:bar3DChart><c:barDir val=\"{(type == "bar" ? "bar" : "col")}\"/><c:grouping val=\"{(depth ? "standard" : grouping)}\"/><c:varyColors val=\"0\"/>")
                        .Append(body).Append("<c:gapWidth val=\"150\"/>").Append(stacked != null ? "<c:overlap val=\"100\"/>" : "")
                        .Append($"<c:shape val=\"{shape}\"/>").Append(depth ? Ids(1, 2, 3) : Ids(1, 2) + "<c:axId val=\"0\"/>").Append("</c:bar3DChart>");
                }
                else if (type == "line")
                    groups.Append("<c:line3DChart><c:grouping val=\"standard\"/><c:varyColors val=\"0\"/>").Append(body).Append("<c:gapDepth val=\"150\"/>").Append(Ids(1, 2, 3)).Append("</c:line3DChart>");
                else
                    groups.Append($"<c:area3DChart><c:grouping val=\"{grouping}\"/><c:varyColors val=\"0\"/>").Append(body).Append("<c:gapDepth val=\"150\"/>")
                        .Append(depth ? Ids(1, 2, 3) : Ids(1, 2) + "<c:axId val=\"0\"/>").Append("</c:area3DChart>");
                view3D = $"<c:view3D><c:rotX val=\"15\"/><c:rotY val=\"20\"/><c:depthPercent val=\"100\"/><c:rAngAx val=\"{(depth ? 0 : 1)}\"/>{(depth ? "<c:perspective val=\"30\"/>" : "")}</c:view3D>"
                    + "<c:floor><c:thickness val=\"0\"/></c:floor><c:sideWall><c:thickness val=\"0\"/><c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr></c:sideWall>"
                    + "<c:backWall><c:thickness val=\"0\"/><c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr></c:backWall>";
                var bar = type == "bar";
                axes.Append(CategoryAxis(1, 2, bar ? "l" : "b", settings.GetValueOrDefault("x"), language, reverse: bar))
                    .Append(ValueAxis(2, 1, bar ? "b" : "l", settings.GetValueOrDefault("y"), language, scale, crossesMax: bar));
                if (depth)
                    axes.Append($"<c:serAx><c:axId val=\"500000003\"/><c:scaling><c:orientation val=\"minMax\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"b\"/><c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/><c:crossAx val=\"500000002\"/><c:crosses val=\"autoZero\"/></c:serAx>");
                break;
            }
            case "column" or "bar" or "line" or "area":
            {
                // The series by the group they fall in: the type's, lines (line=…), each on the main axes or the second (right=…).
                var bar = type == "bar";
                var parts = new List<(string Kind, bool Right, List<int> Members)>();
                for (var k = 0; k < count; k++)
                {
                    var kind = lineNames.Contains(series[k].Name) && type != "line" ? "line" : type;
                    var right = rightNames.Contains(series[k].Name);
                    if (parts.FirstOrDefault(p => p.Kind == kind && p.Right == right) is { Members: { } members })
                        members.Add(k);
                    else
                        parts.Add((kind, right, [k]));
                }
                legendDefault = count > 1;
                foreach (var (kind, right, members) in parts.OrderBy(p => p.Right).ThenBy(p => p.Kind == "line" ? 1 : 0))
                {
                    var body = new StringBuilder();
                    foreach (var k in members)
                    {
                        var s = series[k];
                        var color = Color(settings, k, count);
                        body.Append($"<c:ser><c:idx val=\"{k}\"/><c:order val=\"{k}\"/>").Append(Tx(s));
                        if (kind == "line")
                        {
                            body.Append(Stroke(color)).Append(Marker(color, !(settings.GetValueOrDefault("markers")?.Trim().ToLowerInvariant() is "no" or "none" or "false")));
                            if (labels)
                                body.Append(Labels());
                            body.Append(Trend(s, color)).Append(Errors(false)).Append(Cat()).Append(Numbers("c:val", s.Column, s.Values))
                                .Append($"<c:smooth val=\"{(Yes(settings.GetValueOrDefault("smooth")) ? 1 : 0)}\"/>");
                        }
                        else
                        {
                            body.Append(Fill(color, kind == "area" && stacked == null && count > 1 ? 80000 : null));
                            if (kind is "column" or "bar")
                                body.Append("<c:invertIfNegative val=\"0\"/>");
                            if (labels)
                                body.Append(Labels());
                            body.Append(Trend(s, color)).Append(Errors(false)).Append(Cat()).Append(Numbers("c:val", s.Column, s.Values));
                        }
                        body.Append("</c:ser>");
                    }
                    var ids = right ? Ids(3, 4) : Ids(1, 2);
                    var grouping = kind == "line" && type != "line" ? "standard" : stacked;
                    switch (kind)
                    {
                        case "column" or "bar":
                            groups.Append($"<c:barChart><c:barDir val=\"{(kind == "bar" ? "bar" : "col")}\"/><c:grouping val=\"{grouping ?? "clustered"}\"/><c:varyColors val=\"0\"/>")
                                .Append(body).Append("<c:gapWidth val=\"80\"/>").Append(grouping != null ? "<c:overlap val=\"100\"/>" : "").Append(ids).Append("</c:barChart>");
                            break;
                        case "line":
                            groups.Append($"<c:lineChart><c:grouping val=\"{grouping ?? "standard"}\"/><c:varyColors val=\"0\"/>").Append(body).Append("<c:marker val=\"1\"/>").Append(ids).Append("</c:lineChart>");
                            break;
                        default:
                            groups.Append($"<c:areaChart><c:grouping val=\"{grouping ?? "standard"}\"/><c:varyColors val=\"0\"/>").Append(body).Append(ids).Append("</c:areaChart>");
                            break;
                    }
                }
                axes.Append(CategoryAxis(1, 2, bar ? "l" : "b", settings.GetValueOrDefault("x"), language, reverse: bar))
                    .Append(ValueAxis(2, 1, bar ? "b" : "l", settings.GetValueOrDefault("y"), language, scale, crossesMax: bar));
                if (parts.Any(p => p.Right))
                {
                    axes.Append($"<c:catAx><c:axId val=\"500000003\"/><c:scaling><c:orientation val=\"minMax\"/></c:scaling><c:delete val=\"1\"/><c:axPos val=\"{(bar ? "l" : "b")}\"/><c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/><c:crossAx val=\"500000004\"/><c:crosses val=\"autoZero\"/><c:auto val=\"1\"/><c:lblAlgn val=\"ctr\"/><c:lblOffset val=\"100\"/><c:noMultiLvlLbl val=\"0\"/></c:catAx>")
                        .Append(ValueAxis(4, 3, bar ? "t" : "r", settings.GetValueOrDefault("y2"), language, new Scale(null, null, false, null, "none"), crossesMax: true));
                }
                break;
            }
            case "pie" or "doughnut" or "pie-of-pie" or "bar-of-pie":
            {
                var s = series[0];
                var body = new StringBuilder($"<c:ser><c:idx val=\"0\"/><c:order val=\"0\"/>").Append(Tx(s));
                body.Append("<c:spPr><a:ln w=\"12700\"><a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill></a:ln></c:spPr>");
                if (type == "pie" && Yes(settings.GetValueOrDefault("explode")))
                    body.Append("<c:explosion val=\"12\"/>");
                for (var k = 0; k < n; k++)
                    body.Append($"<c:dPt><c:idx val=\"{k}\"/><c:bubble3D val=\"0\"/><c:spPr><a:solidFill><a:srgbClr val=\"{Color(settings, k, n)}\"/></a:solidFill><a:ln w=\"12700\"><a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill></a:ln></c:spPr></c:dPt>");
                var showValues = settings.GetValueOrDefault("labels")?.Trim().ToLowerInvariant() is "yes" or "true" or "values";
                if (settings.GetValueOrDefault("labels")?.Trim().ToLowerInvariant() is not ("no" or "none" or "false"))
                    body.Append(Labels(percent: !showValues));
                body.Append(Cat()).Append(Numbers("c:val", s.Column, s.Values)).Append("</c:ser>");
                if (type is "pie-of-pie" or "bar-of-pie")
                {
                    var split = (int)Math.Clamp(Number(settings.GetValueOrDefault("split") ?? "") ?? Math.Max(1, n / 3), 1, Math.Max(1, n - 1));
                    groups.Append($"<c:ofPieChart><c:ofPieType val=\"{(type == "bar-of-pie" ? "bar" : "pie")}\"/><c:varyColors val=\"1\"/>").Append(body)
                        .Append($"<c:gapWidth val=\"100\"/><c:splitType val=\"pos\"/><c:splitPos val=\"{split}\"/><c:secondPieSize val=\"75\"/><c:serLines><c:spPr><a:ln w=\"9525\"><a:solidFill><a:srgbClr val=\"A6A6A6\"/></a:solidFill></a:ln></c:spPr></c:serLines></c:ofPieChart>");
                }
                else if (type == "pie" && threeD)
                {
                    groups.Append("<c:pie3DChart><c:varyColors val=\"1\"/>").Append(body).Append("</c:pie3DChart>");
                    view3D = "<c:view3D><c:rotX val=\"30\"/><c:rotY val=\"0\"/><c:rAngAx val=\"0\"/></c:view3D>";
                }
                else if (type == "pie")
                    groups.Append("<c:pieChart><c:varyColors val=\"1\"/>").Append(body).Append("<c:firstSliceAng val=\"0\"/></c:pieChart>");
                else
                    groups.Append("<c:doughnutChart><c:varyColors val=\"1\"/>").Append(body).Append("<c:firstSliceAng val=\"0\"/><c:holeSize val=\"55\"/></c:doughnutChart>");
                legendDefault = true;
                break;
            }
            case "scatter":
            {
                var lines = settings.GetValueOrDefault("lines")?.Trim().ToLowerInvariant() switch { "yes" or "true" or "straight" => "straight", "smooth" => "smooth", _ => "none" };
                var markers = !(settings.GetValueOrDefault("markers")?.Trim().ToLowerInvariant() is "no" or "none" or "false") || lines == "none";
                var style = lines == "none" ? "lineMarker" : lines == "smooth" ? (markers ? "smoothMarker" : "smooth") : markers ? "lineMarker" : "line";
                var body = new StringBuilder();
                for (var k = 0; k < count; k++)
                {
                    var s = series[k];
                    var color = Color(settings, k, count);
                    body.Append($"<c:ser><c:idx val=\"{k}\"/><c:order val=\"{k}\"/>").Append(Tx(s)).Append(Stroke(color, lines != "none")).Append(Marker(color, markers, lines == "none" ? 7 : 5));
                    if (labels)
                        body.Append(Labels());
                    body.Append(Trend(s, color)).Append(Errors(true)).Append(XValues()).Append(Numbers("c:yVal", s.Column, s.Values))
                        .Append($"<c:smooth val=\"{(lines == "smooth" ? 1 : 0)}\"/></c:ser>");
                }
                groups.Append($"<c:scatterChart><c:scatterStyle val=\"{style}\"/><c:varyColors val=\"0\"/>").Append(body).Append(Ids(1, 2)).Append("</c:scatterChart>");
                axes.Append(ValueAxis(1, 2, "b", settings.GetValueOrDefault("x"), language, new Scale(null, null, false, null, "none"), crossBetween: "midCat"))
                    .Append(ValueAxis(2, 1, "l", settings.GetValueOrDefault("y"), language, scale, crossBetween: "midCat"));
                break;
            }
            case "bubble":
            {
                var body = new StringBuilder();
                for (var k = 0; k < count; k++)
                {
                    var s = series[k];
                    var color = Color(settings, k, count);
                    body.Append($"<c:ser><c:idx val=\"{k}\"/><c:order val=\"{k}\"/>").Append(Tx(s)).Append(Fill(color, 75000)).Append("<c:invertIfNegative val=\"0\"/>");
                    if (labels)
                        body.Append(Labels());
                    body.Append(Trend(s, color)).Append(Errors(true)).Append(XValues()).Append(Numbers("c:yVal", s.Column, s.Values))
                        .Append(Numbers("c:bubbleSize", s.SizeColumn, s.Sizes!)).Append("<c:bubble3D val=\"0\"/></c:ser>");
                }
                groups.Append("<c:bubbleChart><c:varyColors val=\"0\"/>").Append(body).Append("<c:bubbleScale val=\"100\"/><c:showNegBubbles val=\"0\"/>").Append(Ids(1, 2)).Append("</c:bubbleChart>");
                axes.Append(ValueAxis(1, 2, "b", settings.GetValueOrDefault("x"), language, new Scale(null, null, false, null, "none"), crossBetween: "midCat"))
                    .Append(ValueAxis(2, 1, "l", settings.GetValueOrDefault("y"), language, scale, crossBetween: "midCat"));
                break;
            }
            case "radar":
            {
                var filled = Yes(settings.GetValueOrDefault("filled"));
                var markers = !(settings.GetValueOrDefault("markers")?.Trim().ToLowerInvariant() is "no" or "none" or "false");
                var body = new StringBuilder();
                for (var k = 0; k < count; k++)
                {
                    var s = series[k];
                    var color = Color(settings, k, count);
                    body.Append($"<c:ser><c:idx val=\"{k}\"/><c:order val=\"{k}\"/>").Append(Tx(s));
                    body.Append(filled ? Fill(color, 55000) : Stroke(color));
                    body.Append(filled ? "" : Marker(color, markers));
                    if (labels)
                        body.Append(Labels());
                    body.Append(Cat()).Append(Numbers("c:val", s.Column, s.Values)).Append("</c:ser>");
                }
                groups.Append($"<c:radarChart><c:radarStyle val=\"{(filled ? "filled" : "marker")}\"/><c:varyColors val=\"0\"/>").Append(body).Append(Ids(1, 2)).Append("</c:radarChart>");
                axes.Append(CategoryAxis(1, 2, "b", null, language, gridlines: true))
                    .Append(ValueAxis(2, 1, "l", null, language, scale with { Gridlines = scale.Gridlines == "none" ? "none" : "major" }));
                break;
            }
            case "stock":
            {
                int Find(string word) => head.FindIndex(1, h => h.Contains(word, StringComparison.OrdinalIgnoreCase));
                var (open, high, low, close) = (Find("open"), Find("high"), Find("low"), Find("close"));
                if (high < 0 || low < 0 || close < 0)
                {
                    // By position: high, low, close, or open, high, low, close.
                    (open, high, low, close) = head.Count >= 5 ? (1, 2, 3, 4) : head.Count == 4 ? (-1, 1, 2, 3) : (-1, -1, -1, -1);
                    if (high < 0)
                        return (null, "A stock chart's table holds the dates, then High, Low and Close (or Open, High, Low and Close).");
                }
                var order = (open >= 0 ? new[] { open, high, low, close } : [high, low, close]).Select(c => series.First(s => s.Column == c)).ToList();
                var body = new StringBuilder();
                for (var k = 0; k < order.Count; k++)
                {
                    var s = order[k];
                    var isClose = s.Column == close && open < 0;
                    body.Append($"<c:ser><c:idx val=\"{k}\"/><c:order val=\"{k}\"/>").Append(Tx(s))
                        .Append("<c:spPr><a:ln w=\"19050\"><a:noFill/></a:ln></c:spPr>")
                        .Append(isClose ? "<c:marker><c:symbol val=\"dash\"/><c:size val=\"7\"/><c:spPr><a:solidFill><a:srgbClr val=\"2F6DB5\"/></a:solidFill><a:ln><a:noFill/></a:ln></c:spPr></c:marker>" : "<c:marker><c:symbol val=\"none\"/></c:marker>")
                        .Append(Cat()).Append(Numbers("c:val", s.Column, s.Values)).Append("<c:smooth val=\"0\"/></c:ser>");
                }
                groups.Append("<c:stockChart>").Append(body)
                    .Append("<c:hiLowLines><c:spPr><a:ln w=\"9525\"><a:solidFill><a:srgbClr val=\"404040\"/></a:solidFill></a:ln></c:spPr></c:hiLowLines>")
                    .Append(open >= 0 ? "<c:upDownBars><c:gapWidth val=\"150\"/><c:upBars><c:spPr><a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill><a:ln w=\"9525\"><a:solidFill><a:srgbClr val=\"404040\"/></a:solidFill></a:ln></c:spPr></c:upBars><c:downBars><c:spPr><a:solidFill><a:srgbClr val=\"404040\"/></a:solidFill><a:ln w=\"9525\"><a:solidFill><a:srgbClr val=\"404040\"/></a:solidFill></a:ln></c:spPr></c:downBars></c:upDownBars>" : "")
                    .Append(Ids(1, 2)).Append("</c:stockChart>");
                axes.Append(CategoryAxis(1, 2, "b", settings.GetValueOrDefault("x"), language))
                    .Append(ValueAxis(2, 1, "l", settings.GetValueOrDefault("y"), language, scale));
                legendDefault = false;
                break;
            }
            case "surface":
            {
                var wireframe = Yes(settings.GetValueOrDefault("wireframe"));
                var top = Kind(settings.GetValueOrDefault("type")) == "surface" && settings.GetValueOrDefault("type")?.Trim().ToLowerInvariant() == "contour";
                var body = new StringBuilder();
                for (var k = 0; k < count; k++)
                {
                    var s = series[k];
                    body.Append($"<c:ser><c:idx val=\"{k}\"/><c:order val=\"{k}\"/>").Append(Tx(s)).Append(Cat()).Append(Numbers("c:val", s.Column, s.Values)).Append("</c:ser>");
                }
                var bands = new StringBuilder("<c:bandFmts>");
                for (var b = 0; b < 6; b++)
                    bands.Append($"<c:bandFmt><c:idx val=\"{b}\"/><c:spPr><a:solidFill><a:srgbClr val=\"{Colors[b % Colors.Length]}\"/></a:solidFill></c:spPr></c:bandFmt>");
                bands.Append("</c:bandFmts>");
                groups.Append(top ? "<c:surfaceChart>" : "<c:surface3DChart>").Append($"<c:wireframe val=\"{(wireframe ? 1 : 0)}\"/>").Append(body).Append(bands)
                    .Append(Ids(1, 2, 3)).Append(top ? "</c:surfaceChart>" : "</c:surface3DChart>");
                view3D = top
                    ? "<c:view3D><c:rotX val=\"90\"/><c:rotY val=\"0\"/><c:rAngAx val=\"0\"/><c:perspective val=\"0\"/></c:view3D>"
                    : "<c:view3D><c:rotX val=\"15\"/><c:rotY val=\"20\"/><c:rAngAx val=\"0\"/><c:perspective val=\"30\"/></c:view3D>";
                axes.Append(CategoryAxis(1, 2, "b", settings.GetValueOrDefault("x"), language))
                    .Append(ValueAxis(2, 1, "l", settings.GetValueOrDefault("y"), language, scale))
                    .Append("<c:serAx><c:axId val=\"500000003\"/><c:scaling><c:orientation val=\"minMax\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"b\"/><c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/><c:crossAx val=\"500000002\"/><c:crosses val=\"autoZero\"/></c:serAx>");
                legendDefault = true;
                break;
            }
        }
        var table = Yes(settings.GetValueOrDefault("table")) && axes.Length > 0
            ? "<c:dTable><c:showHorzBorder val=\"1\"/><c:showVertBorder val=\"1\"/><c:showOutline val=\"1\"/><c:showKeys val=\"1\"/></c:dTable>"
            : "";
        var title = settings.GetValueOrDefault("title");
        var titled = title is { Length: > 0 }
            ? $"<c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"1200\" b=\"1\"/></a:pPr><a:r><a:rPr lang=\"{language}\" sz=\"1200\" b=\"1\"/><a:t>{Escape(title)}</a:t></a:r></a:p></c:rich></c:tx><c:overlay val=\"0\"/></c:title><c:autoTitleDeleted val=\"0\"/>"
            : "<c:autoTitleDeleted val=\"1\"/>";
        var legend = Legend(settings.GetValueOrDefault("legend"), legendDefault);
        var legendMarkup = legend != null ? $"<c:legend><c:legendPos val=\"{legend}\"/><c:overlay val=\"0\"/></c:legend>" : "";
        return ("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<c:chartSpace xmlns:c=\"" + C + "\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
            + $"<c:date1904 val=\"0\"/><c:lang val=\"{language}\"/><c:roundedCorners val=\"0\"/>"
            + $"<c:chart>{titled}{view3D}<c:plotArea><c:layout/>{groups}{axes}{table}</c:plotArea>{legendMarkup}<c:plotVisOnly val=\"1\"/><c:dispBlanksAs val=\"gap\"/></c:chart>"
            + "<c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr>"
            + $"<c:txPr><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"900\"><a:solidFill><a:srgbClr val=\"404040\"/></a:solidFill><a:latin typeface=\"Inter\"/></a:defRPr></a:pPr><a:endParaRPr lang=\"{language}\"/></a:p></c:txPr>"
            + "<c:externalData r:id=\"{workbook}\"><c:autoUpdate val=\"0\"/></c:externalData>"
            + "</c:chartSpace>", null);
    }

    /// <summary>Where the legend stands (legend=…), by default where the chart has several series or slices; null for none.</summary>
    static string? Legend(string? setting, bool byDefault) => setting?.Trim().ToLowerInvariant() switch
    {
        "none" or "no" => null,
        "right" => "r",
        "top" => "t",
        "left" => "l",
        "bottom" => "b",
        _ => byDefault ? "b" : null,
    };

    /// <summary>trend=…: a kind for every series ("" key) or kinds by series ("Sales:linear, Cost:poly"), as DrawingML names them.</summary>
    static Dictionary<string, string> Trends(string? setting)
    {
        var trends = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static string? Of(string kind) => kind.Trim().ToLowerInvariant() switch
        {
            "linear" or "yes" or "true" => "linear",
            "exponential" or "exp" => "exp",
            "logarithmic" or "log" => "log",
            "polynomial" or "poly" => "poly",
            "power" => "power",
            "average" or "moving" or "moving-average" or "movingavg" => "movingAvg",
            _ => null,
        };
        foreach (var item in (setting ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cut = item.LastIndexOf(':');
            if (cut > 0 && Of(item[(cut + 1)..]) is { } kind)
                trends[item[..cut].Trim()] = kind;
            else if (Of(item) is { } every)
                trends[""] = every;
        }
        return trends;
    }

    static string AxisTitle(string? title, string language) => title is { Length: > 0 }
        ? $"<c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"900\" b=\"0\"/></a:pPr><a:r><a:rPr lang=\"{language}\" sz=\"900\" b=\"0\"/><a:t>{Escape(title)}</a:t></a:r></a:p></c:rich></c:tx><c:overlay val=\"0\"/></c:title>"
        : "";

    const string AxisLine = "<c:spPr><a:ln w=\"6350\"><a:solidFill><a:srgbClr val=\"BFBFBF\"/></a:solidFill></a:ln></c:spPr>";
    const string GridLine = "<c:spPr><a:ln w=\"6350\"><a:solidFill><a:srgbClr val=\"E3E1DC\"/></a:solidFill></a:ln></c:spPr>";

    /// <summary>A category axis: its id, the axis it crosses, where it stands, its title; a bar chart's from the top down (reverse).</summary>
    static string CategoryAxis(int id, int cross, string position, string? title, string language, bool reverse = false, bool gridlines = false) =>
        $"<c:catAx><c:axId val=\"{500000000 + id}\"/><c:scaling><c:orientation val=\"{(reverse ? "maxMin" : "minMax")}\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"{position}\"/>"
        + (gridlines ? $"<c:majorGridlines>{GridLine}</c:majorGridlines>" : "") + AxisTitle(title, language)
        + $"<c:numFmt formatCode=\"General\" sourceLinked=\"1\"/><c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/>{AxisLine}"
        + $"<c:crossAx val=\"{500000000 + cross}\"/><c:crosses val=\"autoZero\"/><c:auto val=\"1\"/><c:lblAlgn val=\"ctr\"/><c:lblOffset val=\"100\"/><c:noMultiLvlLbl val=\"0\"/></c:catAx>";

    /// <summary>A value axis: its id, the axis it crosses, where it stands, its title and scale; crossing at the other axis' greatest value (crossesMax) for a bar chart's and a second axis.</summary>
    static string ValueAxis(int id, int cross, string position, string? title, string language, Scale scale, bool crossesMax = false, string crossBetween = "between")
    {
        var scaling = new StringBuilder("<c:scaling>");
        if (scale.Log)
            scaling.Append("<c:logBase val=\"10\"/>");
        scaling.Append("<c:orientation val=\"minMax\"/>");
        if (scale.Max is { } max)
            scaling.Append($"<c:max val=\"{Value(max)}\"/>");
        if (scale.Min is { } min)
            scaling.Append($"<c:min val=\"{Value(min)}\"/>");
        scaling.Append("</c:scaling>");
        var grid = (scale.Gridlines is "major" or "both" or "yes" ? $"<c:majorGridlines>{GridLine}</c:majorGridlines>" : "")
                   + (scale.Gridlines is "minor" or "both" ? $"<c:minorGridlines>{GridLine}</c:minorGridlines>" : "");
        var format = scale.Format is { } f ? $"<c:numFmt formatCode=\"{Escape(f)}\" sourceLinked=\"0\"/>" : "<c:numFmt formatCode=\"General\" sourceLinked=\"1\"/>";
        return $"<c:valAx><c:axId val=\"{500000000 + id}\"/>{scaling}<c:delete val=\"0\"/><c:axPos val=\"{position}\"/>{grid}{AxisTitle(title, language)}{format}"
            + "<c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/><c:spPr><a:ln><a:noFill/></a:ln></c:spPr>"
            + $"<c:crossAx val=\"{500000000 + cross}\"/><c:crosses val=\"{(crossesMax ? "max" : "autoZero")}\"/><c:crossBetween val=\"{crossBetween}\"/></c:valAx>";
    }

    /// <summary>
    /// Office 2016's chart part (cx:chartSpace): its data (categories, levels of categories for a treemap's and a sunburst's, values or
    /// sizes) referring to the workbook's cells, the series of its layout with what it lays out by (a waterfall's totals, a histogram's
    /// bins, a Pareto's line on a percentage axis, a box and whisker's statistics), its axes, title and legend.
    /// </summary>
    static (string? Part, string? Problem) ChartEx(string type, IReadOnlyDictionary<string, string> settings, List<string> head, List<List<string>> rows)
    {
        var n = rows.Count;
        var data = new StringBuilder();
        var series = new StringBuilder();
        var axes = new StringBuilder();
        var legend = settings.GetValueOrDefault("legend")?.Trim().ToLowerInvariant() is { } given && given is not ("none" or "no") ? given switch { "top" => "t", "left" => "l", "right" => "r", _ => "b" } : null;
        string Str(int column) => $"<cx:strDim type=\"cat\"><cx:f>Sheet1!${Column(column)}$2:${Column(column)}${n + 1}</cx:f><cx:lvl ptCount=\"{n}\">"
            + string.Concat(rows.Select((r, k) => $"<cx:pt idx=\"{k}\">{Escape(r[column])}</cx:pt>")) + "</cx:lvl></cx:strDim>";
        string Num(int column, string kind = "val") => $"<cx:numDim type=\"{kind}\"><cx:f>Sheet1!${Column(column)}$2:${Column(column)}${n + 1}</cx:f><cx:lvl ptCount=\"{n}\" formatCode=\"General\">"
            + string.Concat(rows.Select((r, k) => Number(r[column]) is { } v ? $"<cx:pt idx=\"{k}\">{Value(v)}</cx:pt>" : "")) + "</cx:lvl></cx:numDim>";
        string Name(int column) => $"<cx:tx><cx:txData><cx:f>Sheet1!${Column(column)}$1</cx:f><cx:v>{Escape(head[column])}</cx:v></cx:txData></cx:tx>";
        string Id() => "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}";
        // A column of numbers: most of its cells numbers, a number's letters (a unit) fewer than its digits ("5a" is a class, not 5).
        static bool IsNumber(string cell) => Number(cell) != null && cell.Count(char.IsLetter) < cell.Count(char.IsDigit);
        bool Numeric(int column) => rows.Count(r => IsNumber(r[column])) >= Math.Max(1, rows.Count(r => r[column].Trim().Length > 0) * 0.8);
        var labelled = head.Count > 1 && !Numeric(0);
        var valueColumn = Enumerable.Range(0, head.Count).Reverse().FirstOrDefault(Numeric, -1);
        if (valueColumn < 0)
            return (null, "The chart's table holds no numbers below its head.");
        const string ValueAxisMarkup = "<cx:axis id=\"1\"><cx:valScaling/><cx:majorGridlines/><cx:tickLabels/></cx:axis>";
        // Labels on the shapes themselves (a treemap's, a sunburst's, a funnel's) in white.
        const string WhiteLabels = "<cx:txPr><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"900\"><a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill></a:defRPr></a:pPr><a:endParaRPr lang=\"en-US\"/></a:p></cx:txPr>";
        switch (type)
        {
            case "waterfall":
            {
                if (!labelled)
                    return (null, "A waterfall's table holds the steps in its first column and their values in the second; totals=… names the steps that are totals.");
                data.Append("<cx:data id=\"0\">").Append(Str(0)).Append(Num(valueColumn)).Append("</cx:data>");
                var totals = Names(settings.GetValueOrDefault("totals") ?? settings.GetValueOrDefault("total"));
                var indexes = rows.Select((r, k) => (r, k)).Where(p => totals.Contains(p.r[0].Trim())).Select(p => p.k).ToList();
                series.Append($"<cx:series layoutId=\"waterfall\" uniqueId=\"{Id()}\">").Append(Name(valueColumn))
                    .Append("<cx:dataLabels pos=\"outEnd\"><cx:visibility seriesName=\"0\" categoryName=\"0\" value=\"1\"/></cx:dataLabels><cx:dataId val=\"0\"/>")
                    .Append(indexes.Count > 0 ? "<cx:layoutPr><cx:subtotals>" + string.Concat(indexes.Select(k => $"<cx:idx val=\"{k}\"/>")) + "</cx:subtotals></cx:layoutPr>" : "")
                    .Append("</cx:series>");
                axes.Append("<cx:axis id=\"0\"><cx:catScaling gapWidth=\"0.5\"/><cx:tickLabels/></cx:axis>").Append(ValueAxisMarkup);
                break;
            }
            case "funnel":
            {
                if (!labelled)
                    return (null, "A funnel's table holds the stages in its first column and their values in the second.");
                data.Append("<cx:data id=\"0\">").Append(Str(0)).Append(Num(valueColumn)).Append("</cx:data>");
                series.Append($"<cx:series layoutId=\"funnel\" uniqueId=\"{Id()}\">").Append(Name(valueColumn))
                    .Append($"<cx:dataLabels pos=\"ctr\">{WhiteLabels}<cx:visibility seriesName=\"0\" categoryName=\"0\" value=\"1\"/></cx:dataLabels><cx:dataId val=\"0\"/></cx:series>");
                axes.Append("<cx:axis id=\"1\"><cx:catScaling gapWidth=\"0.06\"/><cx:tickLabels/></cx:axis>");
                break;
            }
            case "treemap" or "sunburst":
            {
                // The levels of the categories, the leaves first (as Word keeps them), then the values as sizes.
                var levels = Enumerable.Range(0, valueColumn).Where(c => !Numeric(c)).ToList();
                if (levels.Count == 0)
                    return (null, $"A {type}'s table holds its levels of categories in its first columns, the largest first, and the values in the last.");
                data.Append("<cx:data id=\"0\"><cx:strDim type=\"cat\">");
                foreach (var level in Enumerable.Reverse(levels))
                    data.Append($"<cx:lvl ptCount=\"{n}\">").Append(string.Concat(rows.Select((r, k) => $"<cx:pt idx=\"{k}\">{Escape(r[level])}</cx:pt>"))).Append("</cx:lvl>");
                data.Append("</cx:strDim>").Append(Num(valueColumn, "size")).Append("</cx:data>");
                series.Append($"<cx:series layoutId=\"{type}\" uniqueId=\"{Id()}\">").Append(Name(valueColumn))
                    .Append(type == "treemap"
                        ? $"<cx:dataLabels pos=\"inEnd\">{WhiteLabels}<cx:visibility seriesName=\"0\" categoryName=\"1\" value=\"0\"/></cx:dataLabels><cx:dataId val=\"0\"/><cx:layoutPr><cx:parentLabelLayout val=\"overlapping\"/></cx:layoutPr>"
                        : $"<cx:dataLabels pos=\"ctr\">{WhiteLabels}<cx:visibility seriesName=\"0\" categoryName=\"1\" value=\"0\"/></cx:dataLabels><cx:dataId val=\"0\"/>")
                    .Append("</cx:series>");
                legend ??= settings.ContainsKey("legend") ? null : type == "treemap" ? "t" : null;
                break;
            }
            case "histogram" or "pareto":
            {
                var bins = Number(settings.GetValueOrDefault("bins") ?? "") is { } b && b >= 1 ? $"<cx:binCount val=\"{(int)b}\"/>"
                    : Number(settings.GetValueOrDefault("binwidth") ?? settings.GetValueOrDefault("bin") ?? "") is { } w && w > 0 ? $"<cx:binSize val=\"{Value(w)}\"/>" : "";
                // A Pareto of categories counts them (their values summed); of numbers it bins them as a histogram does.
                var counted = type == "pareto" && labelled;
                data.Append("<cx:data id=\"0\">").Append(counted ? Str(0) : "").Append(Num(valueColumn)).Append("</cx:data>");
                series.Append($"<cx:series layoutId=\"clusteredColumn\" uniqueId=\"{Id()}\">").Append(Name(valueColumn)).Append("<cx:dataId val=\"0\"/>")
                    .Append(counted ? "<cx:layoutPr><cx:aggregation/></cx:layoutPr>" : $"<cx:layoutPr><cx:binning intervalClosed=\"r\">{bins}</cx:binning></cx:layoutPr>")
                    .Append(type == "pareto" ? "<cx:axisId val=\"1\"/>" : "").Append("</cx:series>");
                if (type == "pareto")
                    series.Append($"<cx:series layoutId=\"paretoLine\" ownerIdx=\"0\" uniqueId=\"{Id()}\"><cx:axisId val=\"2\"/></cx:series>");
                axes.Append("<cx:axis id=\"0\"><cx:catScaling gapWidth=\"0\"/><cx:tickLabels/></cx:axis>").Append(ValueAxisMarkup);
                if (type == "pareto")
                    axes.Append("<cx:axis id=\"2\"><cx:valScaling max=\"1\" min=\"0\"/><cx:units unit=\"percentage\"/><cx:tickLabels/></cx:axis>");
                break;
            }
            default:
            {
                // Box and whisker: each numeric column a series of its own data, the rows its observations, grouped by the first column's categories.
                var columns = Enumerable.Range(labelled ? 1 : 0, head.Count - (labelled ? 1 : 0)).Where(Numeric).ToList();
                for (var k = 0; k < columns.Count; k++)
                {
                    data.Append($"<cx:data id=\"{k}\">").Append(labelled ? Str(0) : "").Append(Num(columns[k])).Append("</cx:data>");
                    // Whiskers and box outlines in the series' colour (the style's white outline would hide the whiskers).
                    series.Append($"<cx:series layoutId=\"boxWhisker\" uniqueId=\"{Id()}\">").Append(Name(columns[k]))
                        .Append($"<cx:spPr><a:ln w=\"12700\"><a:solidFill><a:srgbClr val=\"{Colors[k % Colors.Length]}\"/></a:solidFill></a:ln></cx:spPr>")
                        .Append($"<cx:dataId val=\"{k}\"/>")
                        .Append("<cx:layoutPr><cx:visibility meanLine=\"0\" meanMarker=\"1\" nonoutliers=\"0\" outliers=\"1\"/><cx:statistics quartileMethod=\"exclusive\"/></cx:layoutPr></cx:series>");
                }
                axes.Append("<cx:axis id=\"0\"><cx:catScaling gapWidth=\"1\"/><cx:tickLabels/></cx:axis>").Append(ValueAxisMarkup);
                legend ??= settings.ContainsKey("legend") || columns.Count < 2 ? null : "b";
                break;
            }
        }
        var title = settings.GetValueOrDefault("title") is { Length: > 0 } t
            ? $"<cx:title pos=\"t\" align=\"ctr\" overlay=\"0\"><cx:tx><cx:txData><cx:v>{Escape(t)}</cx:v></cx:txData></cx:tx></cx:title>"
            : "";
        return ("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<cx:chartSpace xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:cx=\"" + Cx + "\">"
            + "<cx:chartData><cx:externalData r:id=\"{workbook}\" cx:autoUpdate=\"0\"/>" + data + "</cx:chartData>"
            + $"<cx:chart>{title}<cx:plotArea><cx:plotAreaRegion>{series}</cx:plotAreaRegion>{axes}</cx:plotArea>"
            + (legend != null ? $"<cx:legend pos=\"{legend}\" align=\"ctr\" overlay=\"0\"/>" : "") + "</cx:chart>"
            + "<cx:txPr><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"900\"><a:solidFill><a:srgbClr val=\"404040\"/></a:solidFill><a:latin typeface=\"Inter\"/></a:defRPr></a:pPr><a:endParaRPr lang=\"en-US\"/></a:p></cx:txPr>"
            + "</cx:chartSpace>", null);
    }

    const string ChartStyleNamespace = "http://schemas.microsoft.com/office/drawing/2012/chartStyle";

    /// <summary>
    /// A chart style of our own (cs:chartStyle) for an Office 2016 chart, which Word wants beside it (measured: without one Word does not open
    /// the document): every entry the schema names, the data points filled in their colour of the colour style (phClr, styleClr auto),
    /// lines and markers alike, axes, gridlines and text in calm greys, the chart's area clear.
    /// </summary>
    static string ChartStyle()
    {
        const string Gray = "<a:schemeClr val=\"tx1\"><a:lumMod val=\"65000\"/><a:lumOff val=\"35000\"/></a:schemeClr>";
        const string Light = "<a:schemeClr val=\"tx1\"><a:lumMod val=\"15000\"/><a:lumOff val=\"85000\"/></a:schemeClr>";
        const string Auto = "<cs:styleClr val=\"auto\"/>";
        static string Entry(string name, string? font = null, string? shape = null, string? text = null, bool auto = false) =>
            $"<cs:{name}><cs:lnRef idx=\"0\">{(auto ? Auto : "")}</cs:lnRef><cs:fillRef idx=\"0\">{(auto ? Auto : "")}</cs:fillRef><cs:effectRef idx=\"0\"/>"
            + $"<cs:fontRef idx=\"minor\">{font ?? "<a:schemeClr val=\"tx1\"/>"}</cs:fontRef>{(shape != null ? $"<cs:spPr>{shape}</cs:spPr>" : "")}{text ?? ""}</cs:{name}>";
        const string Fill = "<a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill>";
        const string Clear = "<a:noFill/><a:ln><a:noFill/></a:ln>";
        var thin = $"<a:ln w=\"9525\" cap=\"flat\" cmpd=\"sng\" algn=\"ctr\"><a:solidFill>{Light}</a:solidFill><a:round/></a:ln>";
        var style = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><cs:chartStyle xmlns:cs=\"" + ChartStyleNamespace
            + "\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" id=\"410\">");
        style.Append(Entry("axisTitle", Gray, text: "<cs:defRPr sz=\"900\"/>"))
            .Append(Entry("categoryAxis", Gray, thin, "<cs:defRPr sz=\"900\"/>"))
            .Append(Entry("chartArea", shape: Clear))
            .Append(Entry("dataLabel", Gray, text: "<cs:defRPr sz=\"900\"/>"))
            .Append(Entry("dataLabelCallout", Gray, text: "<cs:defRPr sz=\"900\"/>"))
            .Append(Entry("dataPoint", shape: Fill + "<a:ln w=\"9525\"><a:solidFill><a:schemeClr val=\"bg1\"/></a:solidFill></a:ln>", auto: true))
            .Append(Entry("dataPoint3D", shape: Fill, auto: true))
            .Append(Entry("dataPointLine", shape: "<a:ln w=\"28575\" cap=\"rnd\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:round/></a:ln>", auto: true))
            .Append(Entry("dataPointMarker", shape: Fill, auto: true))
            .Append("<cs:dataPointMarkerLayout symbol=\"circle\" size=\"5\"/>")
            .Append(Entry("dataPointWireframe", shape: "<a:ln w=\"9525\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill></a:ln>", auto: true))
            .Append(Entry("dataTable", Gray, thin, "<cs:defRPr sz=\"900\"/>"))
            .Append(Entry("downBar", shape: "<a:solidFill><a:schemeClr val=\"dk1\"><a:lumMod val=\"75000\"/><a:lumOff val=\"25000\"/></a:schemeClr></a:solidFill>"))
            .Append(Entry("dropLine", shape: thin))
            .Append(Entry("errorBar", shape: thin))
            .Append(Entry("floor", shape: Clear))
            .Append(Entry("gridlineMajor", shape: thin))
            .Append(Entry("gridlineMinor", shape: thin))
            .Append(Entry("hiLoLine", shape: thin))
            .Append(Entry("leaderLine", shape: thin))
            .Append(Entry("legend", Gray, text: "<cs:defRPr sz=\"900\"/>"))
            .Append(Entry("plotArea", shape: Clear))
            .Append(Entry("plotArea3D", shape: Clear))
            .Append(Entry("seriesAxis", Gray, thin, "<cs:defRPr sz=\"900\"/>"))
            .Append(Entry("seriesLine", shape: thin))
            .Append(Entry("title", Gray, text: "<cs:defRPr sz=\"1200\" b=\"1\"/>"))
            .Append(Entry("trendline", shape: "<a:ln w=\"19050\" cap=\"rnd\"><a:solidFill><a:schemeClr val=\"phClr\"/></a:solidFill><a:prstDash val=\"sysDash\"/></a:ln>", auto: true))
            .Append(Entry("trendlineLabel", Gray, text: "<cs:defRPr sz=\"900\"/>"))
            .Append(Entry("upBar", shape: "<a:solidFill><a:schemeClr val=\"lt1\"/></a:solidFill>"))
            .Append(Entry("valueAxis", Gray, text: "<cs:defRPr sz=\"900\"/>"))
            .Append(Entry("wall", shape: Clear));
        return style.Append("</cs:chartStyle>").ToString();
    }

    /// <summary>A colour style of our own (cs:colorStyle) for an Office 2016 chart: the charts' colours in turn, as the classic ones have them.</summary>
    static string ColorStyle() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><cs:colorStyle xmlns:cs=\"" + ChartStyleNamespace + "\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" meth=\"cycle\" id=\"10\">"
        + string.Concat(Colors.Take(6).Select(color => $"<a:srgbClr val=\"{color}\"/>")) + "<cs:variation/></cs:colorStyle>";

    /// <summary>The workbook the chart's data refer to: a sheet of the table as it was given, its numbers as numbers.</summary>
    static byte[] Workbook(List<string> head, List<List<string>> rows)
    {
        const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheet = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"{Main}\"><sheetData>");
        string Text(string cell, string value) => $"<c r=\"{cell}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(value)}</t></is></c>";
        sheet.Append("<row r=\"1\">");
        for (var c = 0; c < head.Count; c++)
            sheet.Append(Text($"{Column(c)}1", head[c]));
        sheet.Append("</row>");
        for (var k = 0; k < rows.Count; k++)
        {
            var row = k + 2;
            sheet.Append($"<row r=\"{row}\">");
            for (var c = 0; c < head.Count; c++)
            {
                var cell = rows[k][c];
                if (cell.Trim().Length == 0)
                    continue;
                // A first column of words stays words; a number elsewhere is a number.
                sheet.Append(Number(cell) is { } value && (c > 0 || !cell.Any(char.IsLetter)) ? $"<c r=\"{Column(c)}{row}\"><v>{Value(value)}</v></c>" : Text($"{Column(c)}{row}", cell));
            }
            sheet.Append("</row>");
        }
        sheet.Append("</sheetData></worksheet>");
        var files = new (string Name, string Content)[]
        {
            ("[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>"),
            ("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>"),
            ("xl/workbook.xml", $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"{Main}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Sheet1\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>"),
            ("xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>"),
            ("xl/worksheets/sheet1.xml", sheet.ToString()),
        };
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                using var entry = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                entry.Write(content);
            }
        }
        return stream.ToArray();
    }

    /// <summary>
    /// A chart part read back as the notation writes the chart (see Of): its type and what the notation says of it (stacking, 3D, lines and a
    /// second axis of a combination, scale, trendlines, a data table, a waterfall's totals, a histogram's bins …), its table from the values
    /// cached for its series; null where it holds no chart this reads.
    /// </summary>
    public static string? Block(byte[] part, DrawingFormatting? drawing)
    {
        XElement root;
        try
        {
            root = XElement.Parse(Encoding.UTF8.GetString(part).TrimStart('﻿'));
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
        var settings = new List<string>();
        string? table;
        if (root.Name.NamespaceName == Cx)
        {
            if (ModernBlock(root, settings) is not { } read)
                return null;
            table = read;
        }
        else if (ClassicBlock(root, settings) is { } read)
            table = read;
        else
            return null;
        if (drawing?.Width is { } width)
            settings.Add("width=" + (width / 72 * 2.54).ToString("0.#", CultureInfo.InvariantCulture) + "cm");
        if (drawing?.Height is { } height)
            settings.Add("height=" + (height / 72 * 2.54).ToString("0.#", CultureInfo.InvariantCulture) + "cm");
        return "```chart " + string.Join(" ", settings) + "\n" + table + "```";
    }

    static string Quote(string text) => "\"" + text.Replace("\"", "''", StringComparison.Ordinal) + "\"";

    static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);

    static string Rows(List<string> head, List<List<string>> rows)
    {
        var table = new StringBuilder("|");
        foreach (var name in head)
            table.Append(' ').Append(Cell(name)).Append(" |");
        table.Append('\n').Append(string.Concat(Enumerable.Repeat("|---", head.Count))).Append("|\n");
        foreach (var row in rows)
        {
            table.Append('|');
            foreach (var cell in row)
                table.Append(' ').Append(Cell(cell)).Append(" |");
            table.Append('\n');
        }
        return table.ToString();
    }

    /// <summary>A classic chart's settings and table (see Block), or null where its plot holds none of the types this writes.</summary>
    static string? ClassicBlock(XElement root, List<string> settings)
    {
        XNamespace c = C;
        var chart = root.Element(c + "chart");
        var plot = chart?.Element(c + "plotArea");
        var groups = plot?.Elements().Where(e => e.Name.LocalName.EndsWith("Chart", StringComparison.Ordinal)).ToList() ?? [];
        if (groups.Count == 0)
            return null;
        string? Val(XElement? e, string name) => (string?)e?.Element(c + name)?.Attribute("val");
        static List<string> Points(XElement? holder)
        {
            var cache = holder?.Descendants().FirstOrDefault(e => e.Name.LocalName is "strCache" or "numCache" or "strLit" or "numLit");
            var count = (int?)cache?.Elements().FirstOrDefault(e => e.Name.LocalName == "ptCount")?.Attribute("val") ?? 0;
            var points = new string[count];
            foreach (var point in cache?.Elements().Where(e => e.Name.LocalName == "pt") ?? [])
            {
                if ((int?)point.Attribute("idx") is { } index && index >= 0 && index < count)
                    points[index] = point.Elements().FirstOrDefault(e => e.Name.LocalName == "v")?.Value ?? "";
            }
            return [.. points.Select(p => p ?? "")];
        }
        string NameOf(XElement series) => string.Concat(series.Element(c + "tx")?.Descendants().Where(e => e.Name.LocalName == "v").Select(v => v.Value) ?? []);
        var first = groups[0];
        var kind = first.Name.LocalName;
        var threeD = kind.Contains("3D", StringComparison.Ordinal);
        var type = kind switch
        {
            "barChart" or "bar3DChart" => Val(first, "barDir") == "bar" ? "bar" : "column",
            "lineChart" or "line3DChart" => "line",
            "areaChart" or "area3DChart" => "area",
            "pieChart" or "pie3DChart" => "pie",
            "doughnutChart" => "doughnut",
            "ofPieChart" => Val(first, "ofPieType") == "bar" ? "bar-of-pie" : "pie-of-pie",
            "scatterChart" => "scatter",
            "bubbleChart" => "bubble",
            "radarChart" => "radar",
            "stockChart" => "stock",
            "surfaceChart" or "surface3DChart" => "surface",
            _ => null,
        };
        if (type == null)
            return null;
        // A stock chart Word makes with volume holds a bar chart first: the stock group says the type.
        if (groups.Any(g => g.Name.LocalName == "stockChart"))
            type = "stock";
        settings.Add("type=" + (kind == "surfaceChart" ? "contour" : type));
        var title = string.Concat(chart!.Element(c + "title")?.Descendants().Where(e => e.Name.LocalName == "t").Select(t => t.Value) ?? []);
        if (title.Length > 0)
            settings.Add("title=" + Quote(title));
        if (threeD && type is not "surface")
            settings.Add("3d=yes");
        if (Val(first, "grouping") is "stacked" or "percentStacked" && type is "column" or "bar" or "line" or "area")
            settings.Add("stacked=" + (Val(first, "grouping") == "stacked" ? "yes" : "percent"));
        if (Val(first, "shape") is { } shape && shape != "box")
            settings.Add("shape=" + shape);
        if (type == "pie" && first.Descendants(c + "explosion").Any(e => (int?)e.Attribute("val") > 0))
            settings.Add("explode=yes");
        if (type is "pie-of-pie" or "bar-of-pie" && Val(first, "splitPos") is { } split)
            settings.Add("split=" + split);
        if (type == "radar" && Val(first, "radarStyle") == "filled")
            settings.Add("filled=yes");
        if (type == "surface" && Val(first, "wireframe") == "1")
            settings.Add("wireframe=yes");
        if (type == "scatter")
        {
            var style = Val(first, "scatterStyle");
            var lined = first.Elements(c + "ser").Any(s => s.Element(c + "spPr")?.Element(XName.Get("ln", "http://schemas.openxmlformats.org/drawingml/2006/main")) is { } ln && !ln.Elements().Any(e => e.Name.LocalName == "noFill"));
            if (lined)
                settings.Add("lines=" + (style is "smooth" or "smoothMarker" ? "smooth" : "yes"));
        }
        if (type == "line" && first.Elements(c + "ser").Any(s => Val(s, "smooth") == "1"))
            settings.Add("smooth=yes");
        if (type is "line" or "scatter" or "radar" && first.Elements(c + "ser").Any()
            && first.Elements(c + "ser").All(s => (string?)s.Element(c + "marker")?.Element(c + "symbol")?.Attribute("val") == "none")
            && !(type == "radar" && Val(first, "radarStyle") == "filled"))
            settings.Add("markers=no");
        // A combination: series of another group drawn as lines, series on the second axis (an axis standing at the right or the top).
        var allSeries = groups.SelectMany(g => g.Elements(c + "ser").Select(s => (Group: g, Series: s))).OrderBy(p => (int?)p.Series.Element(c + "order")?.Attribute("val") ?? 0).ToList();
        var axisPositions = plot!.Elements().Where(e => e.Name.LocalName is "valAx" or "catAx" or "dateAx").ToDictionary(e => Val(e, "axId") ?? "", e => Val(e, "axPos") ?? "", StringComparer.Ordinal);
        bool OnRight(XElement group) => group.Elements(c + "axId").Select(a => (string?)a.Attribute("val") ?? "").Any(id => axisPositions.GetValueOrDefault(id) is "r" or "t") && type is "column" or "line" or "area" or "bar";
        var lines = allSeries.Where(p => p.Group != first && p.Group.Name.LocalName == "lineChart" && type is "column" or "bar" or "area").Select(p => NameOf(p.Series)).ToList();
        if (lines.Count > 0)
            settings.Add("line=" + Quote(string.Join(",", lines)));
        var right = allSeries.Where(p => OnRight(p.Group)).Select(p => NameOf(p.Series)).ToList();
        if (right.Count > 0)
            settings.Add("right=" + Quote(string.Join(",", right)));
        // The colours: the shades of one hue where the series (a pie's slices) have them in turn.
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        var round = type is "pie" or "doughnut" or "pie-of-pie" or "bar-of-pie";
        var painted = (round ? first.Descendants(c + "dPt").Select(d => d.Element(c + "spPr")) : allSeries.Select(p => p.Series.Element(c + "spPr")))
            .Select(sp => (string?)sp?.Element(a + "solidFill")?.Element(a + "srgbClr")?.Attribute("val") ?? (string?)sp?.Element(a + "ln")?.Element(a + "solidFill")?.Element(a + "srgbClr")?.Attribute("val"))
            .ToList();
        var standard = painted.Select((p, k) => string.Equals(p, Colors[k % Colors.Length], StringComparison.OrdinalIgnoreCase)).All(same => same);
        if (painted.Count > 0 && painted.All(p => p != null) && !standard)
        {
            foreach (var (hue, shades) in Hues)
            {
                string Shade(int k) => painted.Count <= 1 ? shades[1] : shades[Math.Min(shades.Length - 1, (int)Math.Round(k * (shades.Length - 2.0) / Math.Max(1, painted.Count - 1)))];
                if (painted.Select((p, k) => string.Equals(p, Shade(k), StringComparison.OrdinalIgnoreCase)).All(same => same))
                {
                    settings.Add("colors=" + hue);
                    break;
                }
            }
        }
        if (first.Descendants(c + "dLbls").Any(d => Val(d, "showVal") == "1") && type is not ("pie" or "doughnut" or "pie-of-pie" or "bar-of-pie"))
            settings.Add("labels=yes");
        if (type is "pie" or "doughnut" or "pie-of-pie" or "bar-of-pie" && first.Descendants(c + "dLbls").Any(d => Val(d, "showVal") == "1"))
            settings.Add("labels=values");
        var trendKinds = allSeries.SelectMany(p => p.Series.Elements(c + "trendline").Select(t => (Name: NameOf(p.Series), Kind: Val(t, "trendlineType") ?? "linear"))).ToList();
        if (trendKinds.Count > 0)
        {
            static string Named(string kind) => kind switch { "exp" => "exponential", "log" => "logarithmic", "poly" => "polynomial", "movingAvg" => "average", var k => k };
            settings.Add(trendKinds.Count == allSeries.Count && trendKinds.Select(t => t.Kind).Distinct().Count() == 1
                ? "trend=" + Named(trendKinds[0].Kind)
                : "trend=" + Quote(string.Join(",", trendKinds.Select(t => t.Name + ":" + Named(t.Kind)))));
        }
        if (allSeries.Any(p => p.Series.Element(c + "errBars") != null))
            settings.Add("errors=yes");
        if (plot.Element(c + "dTable") != null)
            settings.Add("table=yes");
        // The value axis' scale (the main one: the left one, the bottom one of a bar chart).
        var valueAxis = plot.Elements(c + "valAx").FirstOrDefault(a => Val(a, "axPos") is "l" or "b" && !(type is "scatter" or "bubble" && Val(a, "axPos") == "b"));
        if (valueAxis?.Element(c + "scaling") is { } scaling)
        {
            if (Val(scaling, "min") is { } min)
                settings.Add("min=" + min);
            if (Val(scaling, "max") is { } max)
                settings.Add("max=" + max);
            if (scaling.Element(c + "logBase") != null)
                settings.Add("log=yes");
        }
        if (valueAxis?.Element(c + "numFmt") is { } numFmt && (string?)numFmt.Attribute("sourceLinked") == "0" && (string?)numFmt.Attribute("formatCode") is { } code && code != "General"
            && !(code == "0%" && Val(first, "grouping") == "percentStacked"))
            settings.Add("format=" + Quote(code));
        if (valueAxis != null && type is not ("radar" or "pie" or "doughnut"))
        {
            var (major, minor) = (valueAxis.Element(c + "majorGridlines") != null, valueAxis.Element(c + "minorGridlines") != null);
            if (!major && !minor)
                settings.Add("gridlines=none");
            else if (minor)
                settings.Add("gridlines=" + (major ? "both" : "minor"));
        }
        string? AxisTitle(XElement? axis) => axis?.Element(c + "title") is { } t ? string.Concat(t.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value)) : null;
        var categoryAxis = type is "scatter" or "bubble" ? plot.Elements(c + "valAx").FirstOrDefault(a => Val(a, "axPos") == "b") : plot.Elements().FirstOrDefault(e => e.Name.LocalName is "catAx" or "dateAx");
        if (AxisTitle(categoryAxis) is { Length: > 0 } x)
            settings.Add("x=" + Quote(x));
        if (AxisTitle(valueAxis) is { Length: > 0 } y)
            settings.Add("y=" + Quote(y));
        if (AxisTitle(plot.Elements(c + "valAx").FirstOrDefault(a => Val(a, "axPos") is "r" or "t")) is { Length: > 0 } y2)
            settings.Add("y2=" + Quote(y2));
        var legend = chart.Element(c + "legend");
        var position = Val(legend, "legendPos");
        var byDefault = type is "pie" or "doughnut" or "pie-of-pie" or "bar-of-pie" or "surface" || allSeries.Count > 1 && type is not "stock";
        if (legend == null && byDefault)
            settings.Add("legend=none");
        else if (legend != null && (position is not (null or "b") || !byDefault))
            settings.Add("legend=" + (position switch { "r" => "right", "t" => "top", "l" => "left", _ => "bottom" }));

        // The table: the categories (or x values) down the first column, then each series' values (a bubble's sizes beside them).
        var ordered = allSeries.Select(p => p.Series).ToList();
        if (ordered.Count == 0)
            return null;
        var xy = type is "scatter" or "bubble";
        var head = new List<string> { xy ? "x" : "" };
        var columns = new List<List<string>>();
        var categories = Points(ordered[0].Element(c + (xy ? "xVal" : "cat")));
        foreach (var series in ordered)
        {
            head.Add(NameOf(series));
            columns.Add(Points(series.Element(c + (xy ? "yVal" : "val"))));
            if (type == "bubble")
            {
                head.Add(NameOf(series) + " size");
                columns.Add(Points(series.Element(c + "bubbleSize")));
            }
        }
        var count = Math.Max(categories.Count, columns.Count == 0 ? 0 : columns.Max(v => v.Count));
        var rows = Enumerable.Range(0, count).Select(k => new[] { k < categories.Count ? categories[k] : "" }.Concat(columns.Select(v => k < v.Count ? v[k] : "")).ToList()).ToList();
        return Rows(head, rows);
    }

    /// <summary>An Office 2016 chart's settings and table (see Block), or null where it holds none of the types this writes.</summary>
    static string? ModernBlock(XElement root, List<string> settings)
    {
        XNamespace cx = Cx;
        var chart = root.Element(cx + "chart");
        var all = chart?.Descendants(cx + "series").ToList() ?? [];
        if (all.Count == 0)
            return null;
        var layout = (string?)all[0].Attribute("layoutId");
        var type = layout switch
        {
            "waterfall" => "waterfall",
            "funnel" => "funnel",
            "treemap" => "treemap",
            "sunburst" => "sunburst",
            "boxWhisker" => "boxwhisker",
            "clusteredColumn" => all.Any(s => (string?)s.Attribute("layoutId") == "paretoLine") ? "pareto" : "histogram",
            _ => null,
        };
        if (type == null)
            return null;
        settings.Add("type=" + type);
        var title = string.Concat(chart!.Element(cx + "title")?.Descendants().Where(e => e.Name.LocalName is "v" or "t").Select(e => e.Value) ?? []);
        if (title.Length > 0)
            settings.Add("title=" + Quote(title));
        var data = root.Element(cx + "chartData")?.Elements(cx + "data").ToDictionary(d => (string?)d.Attribute("id") ?? "0", d => d, StringComparer.Ordinal) ?? [];
        static List<string> Level(XElement? lvl)
        {
            var count = (int?)lvl?.Attribute("ptCount") ?? 0;
            var points = new string[count];
            foreach (var pt in lvl?.Elements().Where(e => e.Name.LocalName == "pt") ?? [])
            {
                if ((int?)pt.Attribute("idx") is { } index && index >= 0 && index < count)
                    points[index] = pt.Value;
            }
            return [.. points.Select(p => p ?? "")];
        }
        string NameOf(XElement series) => string.Concat(series.Element(cx + "tx")?.Descendants(cx + "v").Select(v => v.Value) ?? []) is { Length: > 0 } name ? name : "Values";
        XElement? DataOf(XElement series) => data.GetValueOrDefault((string?)series.Element(cx + "dataId")?.Attribute("val") ?? "0");
        var head = new List<string>();
        var columns = new List<List<string>>();
        var main = DataOf(all[0]);
        // Categories, the largest level first (Word keeps the leaves first).
        var levels = main?.Element(cx + "strDim")?.Elements(cx + "lvl").Reverse().ToList() ?? [];
        for (var k = 0; k < levels.Count; k++)
        {
            head.Add(levels.Count == 1 ? "" : "Level " + (k + 1).ToString(CultureInfo.InvariantCulture));
            columns.Add(Level(levels[k]));
        }
        foreach (var series in all.Where(s => (string?)s.Attribute("layoutId") != "paretoLine"))
        {
            head.Add(NameOf(series));
            columns.Add(Level(DataOf(series)?.Element(cx + "numDim")?.Element(cx + "lvl")));
        }
        if (type == "waterfall" && all[0].Descendants(cx + "subtotals").FirstOrDefault() is { } subtotals && columns.Count > 1)
        {
            var names = subtotals.Elements(cx + "idx").Select(i => (int?)i.Attribute("val") ?? -1).Where(i => i >= 0 && i < columns[0].Count).Select(i => columns[0][i]).ToList();
            if (names.Count > 0)
                settings.Add("totals=" + Quote(string.Join(",", names)));
        }
        if (all[0].Descendants(cx + "binning").FirstOrDefault() is { } binning)
        {
            if ((string?)binning.Element(cx + "binCount")?.Attribute("val") is { } bins)
                settings.Add("bins=" + bins);
            else if ((string?)binning.Element(cx + "binSize")?.Attribute("val") is { } size)
                settings.Add("binwidth=" + size);
        }
        // The legend where it is not where the type has it by default (a treemap's at the top, several boxes' at the bottom, others none).
        var byDefault = type == "treemap" ? "t" : type == "boxwhisker" && all.Count > 1 ? "b" : null;
        var shown = (string?)chart.Element(cx + "legend")?.Attribute("pos") is { } pos ? pos : chart.Element(cx + "legend") != null ? "b" : null;
        if (shown != byDefault)
            settings.Add("legend=" + (shown switch { "r" => "right", "t" => "top", "l" => "left", "b" => "bottom", _ => "none" }));
        var count = columns.Count == 0 ? 0 : columns.Max(v => v.Count);
        var rows = Enumerable.Range(0, count).Select(k => columns.Select(v => k < v.Count ? v[k] : "").ToList()).ToList();
        return Rows(head, rows);
    }
}
