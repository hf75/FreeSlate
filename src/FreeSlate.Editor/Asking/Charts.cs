using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// Charts as the notation writes them (see Prompt): a fenced block ```chart type=column title="…" holding a GitHub table, the categories down
/// its first column and a series in each other one, its head naming them. As Word keeps one: a chart part (DrawingML's c:chartSpace) whose
/// series refer to the cells of a workbook embedded with it, their values cached, so that Word shows the chart and edits its data in Excel
/// (and the document draws it as Word does, see Docx.Drawings); in a line of its own or floating beside the paragraph after it, as wide as
/// the text unless its width says so. Read back as the block it was written as (see Block).
/// </summary>
static class Charts
{
    const double EmuPerPoint = 12700;

    /// <summary>The series' colours: calm ones that go with the tones, in turn.</summary>
    static readonly string[] Colors = ["2F6DB5", "E07B39", "4C9A5F", "8C5BB8", "C8484B", "2A9DA8", "C9A227", "6B7280"];

    /// <summary>The chart types the notation names, as DrawingML's chart elements.</summary>
    static readonly string[] Types = ["column", "bar", "line", "area", "pie", "doughnut"];

    /// <summary>A chart made at a place (see Markup.Made): its drawing's run in a line of its own (or floating), its chart part and workbook.</summary>
    public static Markup.Made Of(Notation.Fenced chart, Markup.Place place)
    {
        var settings = chart.Settings;
        var type = settings.GetValueOrDefault("type")?.Trim().ToLowerInvariant() ?? "column";
        if (!Types.Contains(type))
            return new("", [], [], Problem: $"type={type} is no chart type (column, bar, line, area, pie or doughnut).");
        if (Notation.Parse(chart.Body, final: true).Blocks.OfType<Notation.Table>().FirstOrDefault() is not { } table || table.Texts.Count < 2 || table.Aligns.Count < 2)
            return new("", [], [], Problem: "A chart holds a table: | category | series … | then |---|---| and a row of values for each category.");
        var categories = table.Texts.Skip(1).Select(r => r[0]).ToList();
        var series = Enumerable.Range(1, table.Aligns.Count - 1).Select(c => (Name: table.Texts[0][c], Values: table.Texts.Skip(1).Select(r => Number(r[c])).ToList())).ToList();
        if (series.All(s => s.Values.All(v => v == null)))
            return new("", [], [], Problem: "The chart's table holds no numbers below its head.");
        var width = Math.Clamp(Markup.Length(settings.GetValueOrDefault("width"), place.TextWidth) ?? place.TextWidth, 72, place.TextWidth);
        var height = Markup.Length(settings.GetValueOrDefault("height"), place.TextWidth) ?? width * (type is "pie" or "doughnut" ? 0.62 : 0.58);
        var language = CultureInfo.CurrentCulture.Name;
        var part = ChartPart(type, settings, table.Texts[0][0], categories, series, language);
        var workbook = Workbook(table.Texts[0][0], categories, series);
        var (cx, cy) = ((long)Math.Round(width * EmuPerPoint), (long)Math.Round(height * EmuPerPoint));
        var alt = settings.GetValueOrDefault("alt") ?? settings.GetValueOrDefault("title") ?? "";
        var run = $"<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>"
            + $"<wp:docPr id=\"1\" name=\"Diagramm\" descr=\"{Escape(alt)}\"/><wp:cNvGraphicFramePr/>"
            + "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"><c:chart r:id=\"{chart}\"/></a:graphicData></a:graphic>"
            + "</wp:inline></w:drawing></w:r>";
        NewPart[] parts =
        [
            new("chart", "/word/charts/chart1.xml", "application/vnd.openxmlformats-officedocument.drawingml.chart+xml",
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart", Encoding.UTF8.GetBytes(part)),
            new("workbook", "/word/embeddings/Microsoft_Excel_Worksheet1.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/package", workbook, From: "chart"),
        ];
        return Markup.Placed(run, parts, settings, place);
    }

    /// <summary>A number as a table's cell gives it: a decimal comma or point, thousands' separators, a unit or a percent sign aside; null for none.</summary>
    static double? Number(string text)
    {
        var kept = new string([.. text.Where(c => char.IsDigit(c) || c is ',' or '.' or '-' or '−')]).Replace('−', '-');
        if (kept.Length == 0)
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

    /// <summary>The chart part: the chart of its type, its series referring to the workbook's cells, their values cached; its title, legend and axes.</summary>
    static string ChartPart(string type, IReadOnlyDictionary<string, string> settings, string head, List<string> categories, List<(string Name, List<double?> Values)> series, string language)
    {
        var n = categories.Count;
        var round = type is "pie" or "doughnut";
        var stacked = settings.GetValueOrDefault("stacked")?.Trim().ToLowerInvariant() switch { "yes" or "true" => "stacked", "percent" => "percentStacked", _ => null };
        var labels = settings.GetValueOrDefault("labels")?.Trim().ToLowerInvariant() is "yes" or "true" or "values";
        var chart = new StringBuilder();
        var element = type switch { "column" or "bar" => "barChart", "line" => "lineChart", "area" => "areaChart", "pie" => "pieChart", _ => "doughnutChart" };
        chart.Append('<').Append("c:").Append(element).Append('>');
        if (type is "column" or "bar")
            chart.Append($"<c:barDir val=\"{(type == "bar" ? "bar" : "col")}\"/><c:grouping val=\"{stacked ?? "clustered"}\"/>");
        else if (type is "line" or "area")
            chart.Append($"<c:grouping val=\"{stacked ?? "standard"}\"/>");
        chart.Append($"<c:varyColors val=\"{(round ? 1 : 0)}\"/>");
        var used = round ? series.Take(1).ToList() : series;
        for (var s = 0; s < used.Count; s++)
        {
            var (name, values) = used[s];
            var color = Colors[s % Colors.Length];
            var column = Column(s + 1);
            chart.Append($"<c:ser><c:idx val=\"{s}\"/><c:order val=\"{s}\"/>");
            chart.Append($"<c:tx><c:strRef><c:f>Sheet1!${column}$1</c:f><c:strCache><c:ptCount val=\"1\"/><c:pt idx=\"0\"><c:v>{Escape(name)}</c:v></c:pt></c:strCache></c:strRef></c:tx>");
            chart.Append(type == "line"
                ? $"<c:spPr><a:ln w=\"28575\" cap=\"rnd\"><a:solidFill><a:srgbClr val=\"{color}\"/></a:solidFill><a:round/></a:ln></c:spPr><c:marker><c:symbol val=\"circle\"/><c:size val=\"5\"/><c:spPr><a:solidFill><a:srgbClr val=\"{color}\"/></a:solidFill><a:ln><a:noFill/></a:ln></c:spPr></c:marker>"
                : round ? "<c:spPr><a:ln w=\"12700\"><a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill></a:ln></c:spPr>"
                : $"<c:spPr><a:solidFill><a:srgbClr val=\"{color}\"/></a:solidFill><a:ln><a:noFill/></a:ln></c:spPr>");
            if (type is "column" or "bar")
                chart.Append("<c:invertIfNegative val=\"0\"/>");
            if (round)
            {
                for (var k = 0; k < n; k++)
                    chart.Append($"<c:dPt><c:idx val=\"{k}\"/><c:bubble3D val=\"0\"/><c:spPr><a:solidFill><a:srgbClr val=\"{Colors[k % Colors.Length]}\"/></a:solidFill><a:ln w=\"12700\"><a:solidFill><a:srgbClr val=\"FFFFFF\"/></a:solidFill></a:ln></c:spPr></c:dPt>");
            }
            if (labels || round && settings.GetValueOrDefault("labels") == null)
                chart.Append($"<c:dLbls><c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr><c:showLegendKey val=\"0\"/><c:showVal val=\"{(round && !labels ? 0 : 1)}\"/><c:showCatName val=\"0\"/><c:showSerName val=\"0\"/><c:showPercent val=\"{(round && !labels ? 1 : 0)}\"/><c:showBubbleSize val=\"0\"/></c:dLbls>");
            chart.Append($"<c:cat><c:strRef><c:f>Sheet1!$A$2:$A${n + 1}</c:f><c:strCache><c:ptCount val=\"{n}\"/>");
            for (var k = 0; k < n; k++)
                chart.Append($"<c:pt idx=\"{k}\"><c:v>{Escape(categories[k])}</c:v></c:pt>");
            chart.Append("</c:strCache></c:strRef></c:cat>");
            chart.Append($"<c:val><c:numRef><c:f>Sheet1!${column}$2:${column}${n + 1}</c:f><c:numCache><c:formatCode>General</c:formatCode><c:ptCount val=\"{n}\"/>");
            for (var k = 0; k < n; k++)
            {
                if (values[k] is { } value)
                    chart.Append($"<c:pt idx=\"{k}\"><c:v>{Value(value)}</c:v></c:pt>");
            }
            chart.Append("</c:numCache></c:numRef></c:val>");
            if (type == "line")
                chart.Append("<c:smooth val=\"0\"/>");
            chart.Append("</c:ser>");
        }
        if (type is "column" or "bar")
            chart.Append($"<c:gapWidth val=\"80\"/>{(stacked != null ? "<c:overlap val=\"100\"/>" : "")}");
        if (type == "line")
            chart.Append("<c:marker val=\"1\"/>");
        if (round)
            chart.Append("<c:firstSliceAng val=\"0\"/>").Append(type == "doughnut" ? "<c:holeSize val=\"55\"/>" : "");
        else
            chart.Append("<c:axId val=\"500000001\"/><c:axId val=\"500000002\"/>");
        chart.Append("</c:").Append(element).Append('>');
        if (!round)
        {
            var (categoryAt, valueAt) = type == "bar" ? ("l", "b") : ("b", "l");
            var line = "<c:spPr><a:ln w=\"6350\"><a:solidFill><a:srgbClr val=\"BFBFBF\"/></a:solidFill></a:ln></c:spPr>";
            chart.Append($"<c:catAx><c:axId val=\"500000001\"/><c:scaling><c:orientation val=\"{(type == "bar" ? "maxMin" : "minMax")}\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"{categoryAt}\"/>")
                .Append(AxisTitle(settings.GetValueOrDefault("x"), language))
                .Append($"<c:numFmt formatCode=\"General\" sourceLinked=\"1\"/><c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/>{line}<c:crossAx val=\"500000002\"/><c:crosses val=\"autoZero\"/><c:auto val=\"1\"/><c:lblAlgn val=\"ctr\"/><c:lblOffset val=\"100\"/><c:noMultiLvlLbl val=\"0\"/></c:catAx>");
            chart.Append($"<c:valAx><c:axId val=\"500000002\"/><c:scaling><c:orientation val=\"minMax\"/></c:scaling><c:delete val=\"0\"/><c:axPos val=\"{valueAt}\"/>")
                .Append("<c:majorGridlines><c:spPr><a:ln w=\"6350\"><a:solidFill><a:srgbClr val=\"E3E1DC\"/></a:solidFill></a:ln></c:spPr></c:majorGridlines>")
                .Append(AxisTitle(settings.GetValueOrDefault("y"), language))
                .Append($"<c:numFmt formatCode=\"{(stacked == "percentStacked" ? "0%" : "General")}\" sourceLinked=\"{(stacked == "percentStacked" ? 0 : 1)}\"/><c:majorTickMark val=\"none\"/><c:minorTickMark val=\"none\"/><c:tickLblPos val=\"nextTo\"/><c:spPr><a:ln><a:noFill/></a:ln></c:spPr><c:crossAx val=\"500000001\"/><c:crosses val=\"{(type == "bar" ? "max" : "autoZero")}\"/><c:crossBetween val=\"between\"/></c:valAx>");
        }
        var title = settings.GetValueOrDefault("title");
        var titled = title is { Length: > 0 }
            ? $"<c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"1200\" b=\"1\"/></a:pPr><a:r><a:rPr lang=\"{language}\" sz=\"1200\" b=\"1\"/><a:t>{Escape(title)}</a:t></a:r></a:p></c:rich></c:tx><c:overlay val=\"0\"/></c:title><c:autoTitleDeleted val=\"0\"/>"
            : "<c:autoTitleDeleted val=\"1\"/>";
        var legend = settings.GetValueOrDefault("legend")?.Trim().ToLowerInvariant() switch
        {
            "none" or "no" => null,
            "right" => "r",
            "top" => "t",
            "left" => "l",
            "bottom" => "b",
            _ => round || series.Count > 1 ? "b" : null,
        };
        var legendMarkup = legend != null ? $"<c:legend><c:legendPos val=\"{legend}\"/><c:overlay val=\"0\"/></c:legend>" : "";
        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<c:chartSpace xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
            + $"<c:date1904 val=\"0\"/><c:lang val=\"{language}\"/><c:roundedCorners val=\"0\"/>"
            + $"<c:chart>{titled}<c:plotArea><c:layout/>{chart}</c:plotArea>{legendMarkup}<c:plotVisOnly val=\"1\"/><c:dispBlanksAs val=\"gap\"/></c:chart>"
            + "<c:spPr><a:noFill/><a:ln><a:noFill/></a:ln></c:spPr>"
            + $"<c:txPr><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"900\"><a:solidFill><a:srgbClr val=\"404040\"/></a:solidFill><a:latin typeface=\"Inter\"/></a:defRPr></a:pPr><a:endParaRPr lang=\"{language}\"/></a:p></c:txPr>"
            + "<c:externalData r:id=\"{workbook}\"><c:autoUpdate val=\"0\"/></c:externalData>"
            + "</c:chartSpace>";
    }

    static string AxisTitle(string? title, string language) => title is { Length: > 0 }
        ? $"<c:title><c:tx><c:rich><a:bodyPr/><a:lstStyle/><a:p><a:pPr><a:defRPr sz=\"900\" b=\"0\"/></a:pPr><a:r><a:rPr lang=\"{language}\" sz=\"900\" b=\"0\"/><a:t>{Escape(title)}</a:t></a:r></a:p></c:rich></c:tx><c:overlay val=\"0\"/></c:title>"
        : "";

    /// <summary>The workbook the chart's series refer to: a sheet of the table as it was given, its numbers as numbers.</summary>
    static byte[] Workbook(string head, List<string> categories, List<(string Name, List<double?> Values)> series)
    {
        const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheet = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"{Main}\"><sheetData>");
        string Text(string cell, string value) => $"<c r=\"{cell}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(value)}</t></is></c>";
        sheet.Append("<row r=\"1\">").Append(Text("A1", head));
        for (var s = 0; s < series.Count; s++)
            sheet.Append(Text($"{Column(s + 1)}1", series[s].Name));
        sheet.Append("</row>");
        for (var k = 0; k < categories.Count; k++)
        {
            var row = k + 2;
            sheet.Append($"<row r=\"{row}\">").Append(Text($"A{row}", categories[k]));
            for (var s = 0; s < series.Count; s++)
            {
                if (series[s].Values[k] is { } value)
                    sheet.Append($"<c r=\"{Column(s + 1)}{row}\"><v>{Value(value)}</v></c>");
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
    /// A chart part read back as the notation writes the chart (see Of): its type, title, stacking and legend, and its table from the values
    /// cached for its series; null where it holds no chart this reads (another kind than these).
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
        XNamespace c = "http://schemas.openxmlformats.org/drawingml/2006/chart";
        var plot = root.Descendants(c + "plotArea").FirstOrDefault();
        var kind = plot?.Elements().FirstOrDefault(e => e.Name.LocalName is "barChart" or "lineChart" or "areaChart" or "pieChart" or "doughnutChart"
            or "bar3DChart" or "line3DChart" or "pie3DChart" or "area3DChart");
        if (kind == null)
            return null;
        string? Val(XElement? e, string name) => (string?)e?.Element(c + name)?.Attribute("val");
        var type = kind.Name.LocalName switch
        {
            "barChart" or "bar3DChart" => Val(kind, "barDir") == "bar" ? "bar" : "column",
            "lineChart" or "line3DChart" => "line",
            "areaChart" or "area3DChart" => "area",
            "pieChart" or "pie3DChart" => "pie",
            _ => "doughnut",
        };
        var settings = new List<string> { "type=" + type };
        var title = string.Concat(root.Element(c + "chart")?.Element(c + "title")?.Descendants().Where(e => e.Name.LocalName == "t").Select(t => t.Value) ?? []);
        if (title.Length > 0)
            settings.Add("title=\"" + title.Replace("\"", "''", StringComparison.Ordinal) + "\"");
        if (Val(kind, "grouping") is "stacked" or "percentStacked")
            settings.Add("stacked=" + (Val(kind, "grouping") == "stacked" ? "yes" : "percent"));
        if (root.Element(c + "chart")?.Element(c + "legend") == null)
            settings.Add("legend=none");
        if (drawing?.Width is { } width)
            settings.Add("width=" + (width / 72 * 2.54).ToString("0.#", CultureInfo.InvariantCulture) + "cm");
        if (drawing?.Height is { } height)
            settings.Add("height=" + (height / 72 * 2.54).ToString("0.#", CultureInfo.InvariantCulture) + "cm");
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
        var series = kind.Elements(c + "ser").ToList();
        if (series.Count == 0)
            return null;
        var categories = Points(series[0].Element(c + "cat"));
        var table = new StringBuilder("| |");
        foreach (var one in series)
            table.Append(' ').Append(string.Concat(one.Element(c + "tx")?.Descendants().Where(e => e.Name.LocalName == "v").Select(v => v.Value) ?? []).Replace("|", "\\|", StringComparison.Ordinal)).Append(" |");
        table.Append('\n').Append(string.Concat(Enumerable.Repeat("|---", series.Count + 1))).Append("|\n");
        var values = series.Select(s => Points(s.Element(c + "val"))).ToList();
        for (var k = 0; k < categories.Count; k++)
        {
            table.Append("| ").Append(categories[k].Replace("|", "\\|", StringComparison.Ordinal)).Append(" |");
            foreach (var column in values)
                table.Append(' ').Append(k < column.Count ? column[k] : "").Append(" |");
            table.Append('\n');
        }
        return "```chart " + string.Join(" ", settings) + "\n" + table + "```";
    }
}
