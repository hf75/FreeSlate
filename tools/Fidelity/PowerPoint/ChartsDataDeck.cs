using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Chart data and the rest of the chart settings (catalog: chart.part, chart.data, chart.axes, chart.formatting,
/// chart.threeD, chart.labels, chart.titleLegend): blank cells shown as gaps, zero or spanned, a hidden row and a
/// filtered series, multi-level categories with and without their levels, date axes counting days and months, pictures
/// stretched and stacked in columns, labels of values above the axis maximum, thick 3D walls, category labels aligned
/// left and right, and a plot area placed by its outer edges.
/// </summary>
[SupportedOSPlatform("windows")]
static class ChartsDataDeck
{
    // XlChartType
    const int ColumnClustered = 51, LineMarkers = 65, Column3DClustered = 54;
    // XlAxisType, XlDisplayBlanksAs, XlTimeUnit
    const int Category = 1, Value = 2, NotPlotted = 1, Zero = 2, Interpolated = 3, Days = 0, Months = 1;

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        object blanks = Slide(presentation, "Blank cells: gap, zero, span");
        foreach (var (left, mode) in new[] { (20f, NotPlotted), (330f, Zero), (640f, Interpolated) })
        {
            var chart = Add(blanks, LineMarkers, left, 110, 300, 410, c => SetData((object)c, ["A", "B", "C", "D", "E", "F"],
                [("Values", [3, 5, null, 4, null, 6])]));
            chart.DisplayBlanksAs = mode;
        }

        object hidden = Slide(presentation, "A hidden row and a filtered series");
        Add(hidden, ColumnClustered, 20, 110, 460, 410, c =>
        {
            SetData((object)c, ["North", "South", "East", "West"], [("2023", [4, 6, 5, 7]), ("2024", [5, 7, 6, 8])]);
            dynamic chart = c;
            chart.ChartData.Workbook.Worksheets[1].Rows[3].Hidden = true;
        });
        var filtered = Add(hidden, ColumnClustered, 490, 110, 460, 410, c => SetData((object)c, ["North", "South", "East", "West"],
            [("2023", [4, 6, 5, 7]), ("2024", [5, 7, 6, 8]), ("2025", [6, 8, 7, 9])]));
        filtered.FullSeriesCollection(2).IsFiltered = true;

        object levels = Slide(presentation, "Multi-level categories");
        Add(levels, ColumnClustered, 20, 110, 460, 410, c => SetLevels((object)c));
        var flat = Add(levels, ColumnClustered, 490, 110, 460, 410, c => SetLevels((object)c));
        flat.Axes(Category).TickLabels.MultiLevel = false;

        object dates = Slide(presentation, "Date axes in days and months");
        // 1, 4, 5, 12 and 19 January and 2 February 2024; then the 1st and 15th of months.
        var byDay = Add(dates, LineMarkers, 20, 110, 460, 410, c => SetData((object)c, ["45292", "45295", "45296", "45303", "45310", "45324"],
            [("Visits", [12, 18, 15, 22, 19, 25])]));
        var dayAxis = byDay.Axes(Category);
        dayAxis.CategoryType = 3;   // xlTimeScale
        dayAxis.BaseUnit = Days;
        dayAxis.MajorUnit = 7;
        dayAxis.MajorUnitScale = Days;
        dayAxis.TickLabels.NumberFormat = "d. mmm";
        var byMonth = Add(dates, ColumnClustered, 490, 110, 460, 410, c => SetData((object)c, ["45292", "45306", "45337", "45366", "45383", "45444"],
            [("Orders", [5, 8, 6, 9, 7, 10])]));
        var monthAxis = byMonth.Axes(Category);
        monthAxis.CategoryType = 3;
        monthAxis.BaseUnit = Months;
        monthAxis.TickLabels.NumberFormat = "mmm yy";

        object pictures = Slide(presentation, "Pictures stretched, stacked and scaled");
        var images = TestImages.Write(Path.Combine(Path.GetTempPath(), "fidelity-images"));
        foreach (var left in new[] { 20f, 330f, 640f })
        {
            var chart = Add(pictures, ColumnClustered, left, 110, 300, 410, c => SetData((object)c, ["A", "B", "C"], [("Units", [2, 4, 3])]));
            chart.SeriesCollection(1).Format.Fill.UserPicture(images.Png);
        }

        object limits = Slide(presentation, "Labels above the maximum, thick walls");
        var capped = Add(limits, ColumnClustered, 20, 110, 460, 410, c => SetData((object)c, ["A", "B", "C", "D"], [("Score", [3, 8, 5, 11])]));
        capped.Axes(Value).MaximumScale = 6;
        capped.SeriesCollection(1).HasDataLabels = true;
        Add(limits, Column3DClustered, 490, 110, 460, 410);

        object align = Slide(presentation, "Label alignment and an outer plot layout");
        string[] regions = ["North\nregion", "South", "East\nregion", "West"];
        Add(align, ColumnClustered, 20, 110, 300, 410, c => SetData((object)c, regions, [("Sales", [4, 6, 5, 7])]));
        Add(align, ColumnClustered, 330, 110, 300, 410, c => SetData((object)c, regions, [("Sales", [4, 6, 5, 7])]));
        Add(align, ColumnClustered, 640, 110, 300, 410);
    }

    /// <summary>
    /// What the object model does not set: stacked and scaled pictures (c:pictureOptions), wall thickness
    /// (c:thickness), category label alignment (c:lblAlgn) and a plot area placed by its outer edges (layoutTarget
    /// outer, w and h as factors of the chart).
    /// </summary>
    public static void Patch(string path)
    {
        using var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Update);
        XNamespace c = "http://schemas.openxmlformats.org/drawingml/2006/chart";
        XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
        XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XDocument Load(string name)
        {
            using var stream = zip.GetEntry(name)!.Open();
            return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }
        void Save(string name, XDocument document)
        {
            zip.GetEntry(name)!.Delete();
            using var stream = zip.CreateEntry(name).Open();
            document.Save(stream, SaveOptions.DisableFormatting);
        }
        List<string> Charts(int slide)
        {
            var rels = Load($"ppt/slides/_rels/slide{slide}.xml.rels");
            return Load($"ppt/slides/slide{slide}.xml").Descendants(c + "chart")
                .Select(frame => (string)frame.Attribute(r + "id")!)
                .Select(id => "ppt/" + rels.Root!.Elements(rel + "Relationship").First(e => (string?)e.Attribute("Id") == id).Attribute("Target")!.Value.Replace("../", ""))
                .ToList();
        }
        void Edit(string part, Action<XDocument> edit)
        {
            var document = Load(part);
            edit(document);
            Save(part, document);
        }
        static XElement Val(XName name, string value) => new(name, new XAttribute("val", value));

        // Pictures: stretched (as saved), stacked, stacked one per unit; c:pictureOptions follows c:invertIfNegative.
        var pictures = Charts(5);
        foreach (var (part, options) in new[]
        {
            (pictures[1], new XElement(c + "pictureOptions", Val(c + "pictureFormat", "stack"))),
            (pictures[2], new XElement(c + "pictureOptions", Val(c + "pictureFormat", "stackScale"), Val(c + "pictureStackUnit", "1"))),
        })
        {
            Edit(part, document =>
            {
                var series = document.Descendants(c + "ser").First();
                series.Element(c + "pictureOptions")?.Remove();
                var after = series.Element(c + "invertIfNegative") ?? series.Element(c + "spPr") ?? series.Element(c + "tx")!;
                after.AddAfterSelf(options);
            });
        }

        // Walls and floor 10 % thick: c:thickness comes first in c:floor, c:sideWall and c:backWall.
        Edit(Charts(6)[1], document =>
        {
            foreach (var wall in document.Descendants().Where(e => e.Name == c + "floor" || e.Name == c + "sideWall" || e.Name == c + "backWall"))
            {
                wall.Element(c + "thickness")?.Remove();
                wall.AddFirst(Val(c + "thickness", "10"));
            }
        });

        // Category labels aligned left and right (c:lblAlgn follows c:auto in c:catAx).
        var aligned = Charts(7);
        foreach (var (part, alignment) in new[] { (aligned[0], "l"), (aligned[1], "r") })
        {
            Edit(part, document =>
            {
                var axis = document.Descendants(c + "catAx").First();
                axis.Element(c + "lblAlgn")?.Remove();
                (axis.Element(c + "auto") ?? axis.Element(c + "crosses")!).AddAfterSelf(Val(c + "lblAlgn", alignment));
            });
        }

        // The third chart's plot area by its outer edges, its size as factors of the chart.
        Edit(aligned[2], document =>
        {
            var plotArea = document.Descendants(c + "plotArea").First();
            plotArea.Element(c + "layout")?.Remove();
            plotArea.AddFirst(new XElement(c + "layout", new XElement(c + "manualLayout",
                Val(c + "layoutTarget", "outer"), Val(c + "xMode", "edge"), Val(c + "yMode", "edge"),
                Val(c + "wMode", "factor"), Val(c + "hMode", "factor"),
                Val(c + "x", "0.25"), Val(c + "y", "0.2"), Val(c + "w", "0.6"), Val(c + "h", "0.5"))));
        });
    }

    static dynamic Slide(object deck, string title)
    {
        dynamic presentation = deck;
        var slide = CorpusGenerator.AddSlide(presentation, CorpusGenerator.LayoutTitleOnly);
        slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = title;
        return slide;
    }

    static dynamic Add(object target, int type, float left, float top, float width, float height, Action<dynamic>? data = null)
    {
        dynamic slide = target;
        var chart = slide.Shapes.AddChart2(-1, type, left, top, width, height).Chart;
        data?.Invoke(chart);
        try { chart.ChartData.Workbook.Close(); } catch (Exception) { /* nothing to close */ }
        return chart;
    }

    /// <summary>Replaces the chart's sample data (first column categories, then one column per series; null leaves a cell blank).</summary>
    static void SetData(object target, string[] categories, (string Name, double?[] Values)[] series)
    {
        dynamic chart = target;
        var sheet = chart.ChartData.Workbook.Worksheets[1];
        sheet.Range["A1:Z100"].ClearContents();
        for (var i = 0; i < categories.Length; i++)
            sheet.Cells[i + 2, 1].Value2 = double.TryParse(categories[i], out var number) ? number : (object)categories[i];
        for (var k = 0; k < series.Length; k++)
        {
            sheet.Cells[1, k + 2].Value2 = series[k].Name;
            for (var i = 0; i < series[k].Values.Length; i++)
            {
                if (series[k].Values[i] is { } value)
                    sheet.Cells[i + 2, k + 2].Value2 = value;
            }
        }
        var range = $"$A$1:${(char)('A' + series.Length)}${categories.Length + 1}";
        if ((int)sheet.ListObjects.Count > 0)
            sheet.ListObjects[1].Resize(sheet.Range[range]);
        chart.SetSourceData($"='{sheet.Name}'!{range}", 2);   // xlColumns
    }

    /// <summary>Two category levels (years over quarters) and one series.</summary>
    static void SetLevels(object target)
    {
        dynamic chart = target;
        var sheet = chart.ChartData.Workbook.Worksheets[1];
        sheet.Range["A1:Z100"].ClearContents();
        double[] values = [3, 4, 5, 4, 5, 6, 7, 6];
        for (var i = 0; i < 8; i++)
        {
            if (i % 4 == 0)
                sheet.Cells[i + 2, 1].Value2 = i == 0 ? "2023" : "2024";
            sheet.Cells[i + 2, 2].Value2 = $"Q{i % 4 + 1}";
            sheet.Cells[i + 2, 3].Value2 = values[i];
        }
        sheet.Cells[1, 3].Value2 = "Sales";
        const string range = "$A$1:$C$9";
        if ((int)sheet.ListObjects.Count > 0)
            sheet.ListObjects[1].Resize(sheet.Range[range]);
        chart.SetSourceData($"='{sheet.Name}'!{range}", 2);
    }
}
