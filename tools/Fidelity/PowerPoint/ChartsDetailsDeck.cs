using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Chart details (catalog: chart.barColumn, chart.axes, chart.labels, chart.pie, chart.part, chart.titleLegend):
/// negative bars with inverted fills, display units, label and tick mark intervals, label offsets and alignment, a
/// date axis, data labels with legend keys, leader lines and text of their own, pie of pie and bar of pie, rounded
/// corners and a manual plot area layout.
/// </summary>
[SupportedOSPlatform("windows")]
static class ChartsDetailsDeck
{
    // XlChartType
    const int ColumnClustered = 51, Line = 4, Pie = 5, PieOfPie = 68, BarOfPie = 71, Bubble = 15, BarClustered = 57;
    // XlAxisType, XlDisplayUnit
    const int Category = 1, Value = 2, Thousands = -4, Millions = -6;

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        object negative = Slide(presentation, "Negative bars and display units");
        var inverted = Add(negative, ColumnClustered, 20, 110, 460, 410, c => SetData((object)c, ["A", "B", "C", "D", "E"],
            [("Result", [12, -7, 5, -11, 9]), ("Plan", [-4, 6, -9, 3, 7])]));
        inverted.SeriesCollection(1).InvertIfNegative = true;
        inverted.SeriesCollection(1).Format.Fill.ForeColor.RGB = CorpusGenerator.Rgb(68, 114, 196);
        inverted.SeriesCollection(2).InvertIfNegative = true;
        var units = Add(negative, ColumnClustered, 490, 110, 460, 410, c => SetData((object)c, ["2021", "2022", "2023", "2024"],
            [("Revenue", [1250000, 2400000, 3100000, 4750000]), ("Costs", [900000, 1700000, 2600000, 3200000])]));
        var axis = units.Axes(Value);
        axis.DisplayUnit = Millions;
        axis.HasDisplayUnitLabel = true;

        object intervals = Slide(presentation, "Label intervals, offsets and a date axis");
        string[] months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
        var skipped = Add(intervals, Line, 20, 110, 460, 410, c => SetData((object)c, months,
            [("Sales", [3, 4, 4.5, 5, 6, 5.5, 7, 8, 7.5, 9, 10, 11])]));
        var categories = skipped.Axes(Category);
        categories.TickLabelSpacing = 3;
        categories.TickMarkSpacing = 2;
        categories.TickLabels.Offset = 400;
        var thousands = skipped.Axes(Value);
        thousands.DisplayUnit = Thousands;
        string[] dates = ["44927", "44958", "44986", "45017", "45047", "45078"];   // the first of January to June 2023
        var dated = Add(intervals, ColumnClustered, 490, 110, 460, 410, c => SetData((object)c, dates, [("Orders", [20, 26, 23, 31, 35, 29])]));
        var dateAxis = dated.Axes(Category);
        dateAxis.CategoryType = 3;   // xlTimeScale
        dateAxis.BaseUnit = 1;   // xlMonths
        dateAxis.TickLabels.NumberFormat = "mmm yy";

        object pies = Slide(presentation, "Pie of pie and bar of pie");
        Add(pies, PieOfPie, 20, 110, 460, 410, c => SetData((object)c, ["North", "South", "East", "West", "Other A", "Other B"],
            [("Share", [35, 25, 20, 10, 6, 4])]));
        var barOfPie = Add(pies, BarOfPie, 490, 110, 460, 410, c => SetData((object)c, ["North", "South", "East", "West", "Other A", "Other B"],
            [("Share", [35, 25, 20, 10, 6, 4])]));
        barOfPie.ChartGroups(1).SplitValue = 3;
        barOfPie.ChartGroups(1).SecondPlotSize = 60;

        object labels = Slide(presentation, "Leader lines and legend keys");
        var leaders = Add(labels, Pie, 20, 110, 460, 410, c => SetData((object)c, ["One", "Two", "Three", "Four", "Five", "Six"],
            [("Parts", [40, 25, 15, 10, 6, 4])]));
        leaders.SeriesCollection(1).HasDataLabels = true;
        var pieLabels = leaders.SeriesCollection(1).DataLabels();
        pieLabels.Position = 2;   // xlLabelPositionOutsideEnd
        pieLabels.ShowCategoryName = true;
        leaders.SeriesCollection(1).HasLeaderLines = true;
        for (var i = 1; i <= 3; i++)
        {
            var point = leaders.SeriesCollection(1).Points(i).DataLabel;
            point.Left += 30;
            point.Top -= 20;
        }
        var keyed = Add(labels, BarClustered, 490, 110, 460, 410);
        keyed.SeriesCollection(1).HasDataLabels = true;
        keyed.SeriesCollection(1).DataLabels().ShowLegendKey = true;
        keyed.SeriesCollection(2).HasDataLabels = true;
        keyed.SeriesCollection(2).Points(3).DataLabel.Text = "Own text";

        object frames = Slide(presentation, "Rounded corners, layouts and bubble sizes");
        Add(frames, ColumnClustered, 20, 110, 300, 410);
        Add(frames, ColumnClustered, 330, 110, 300, 410);
        var bubbles = Add(frames, Bubble, 640, 110, 300, 410);
        bubbles.SeriesCollection(1).HasDataLabels = true;
        bubbles.SeriesCollection(1).DataLabels().ShowBubbleSize = true;
        bubbles.SeriesCollection(1).DataLabels().ShowValue = false;

        // Point labels with text of their own whose runs are formatted (c:dLbl/c:tx/c:rich with run properties).
        object formatted = Slide(presentation, "Formatted data label text");
        var columns = Add(formatted, ColumnClustered, 20, 110, 460, 410);
        columns.SeriesCollection(1).HasDataLabels = true;
        var peak = columns.SeriesCollection(1).Points(4).DataLabel;
        peak.Text = "Peak 4.5";
        var peakText = peak.Format.TextFrame2.TextRange.Characters(1, 4);
        peakText.Font.Bold = -1;
        peakText.Font.Size = 16;
        peakText.Font.Fill.ForeColor.RGB = CorpusGenerator.Rgb(192, 0, 0);
        var low = columns.SeriesCollection(1).Points(2).DataLabel;
        low.Text = "low";
        low.Format.TextFrame2.TextRange.Characters(1, 3).Font.Italic = -1;
        var bars = Add(formatted, BarClustered, 490, 110, 460, 410);
        bars.SeriesCollection(2).HasDataLabels = true;
        var mixed = bars.SeriesCollection(2).Points(1).DataLabel;
        mixed.Text = "Big and small";
        mixed.Format.TextFrame2.TextRange.Characters(1, 3).Font.Size = 20;
        mixed.Format.TextFrame2.TextRange.Characters(9, 5).Font.Size = 8;
    }

    /// <summary>
    /// Rounds the corners of the fifth slide's first chart (c:roundedCorners) on a framed chart area and places the
    /// second one's plot area by hand (c:manualLayout); the object model sets neither (nor, there, the frame's line).
    /// </summary>
    public static void Patch(string path)
    {
        using var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Update);
        System.Xml.Linq.XDocument Load(string name)
        {
            using var stream = zip.GetEntry(name)!.Open();
            return System.Xml.Linq.XDocument.Load(stream, System.Xml.Linq.LoadOptions.PreserveWhitespace);
        }
        System.Xml.Linq.XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
        System.Xml.Linq.XNamespace c = "http://schemas.openxmlformats.org/drawingml/2006/chart";
        var slide = Load("ppt/slides/slide5.xml");
        var rels = Load("ppt/slides/_rels/slide5.xml.rels");
        string PartOf(System.Xml.Linq.XElement frame)
        {
            var id = (string)frame.Attribute(System.Xml.Linq.XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"))!;
            var target = rels.Root!.Elements(rel + "Relationship").First(r => (string?)r.Attribute("Id") == id).Attribute("Target")!.Value;
            return "ppt/" + target.Replace("../", "");
        }
        void Save(string name, System.Xml.Linq.XDocument document)
        {
            zip.GetEntry(name)!.Delete();
            using var stream = zip.CreateEntry(name).Open();
            document.Save(stream, System.Xml.Linq.SaveOptions.DisableFormatting);
        }
        var charts = slide.Descendants(c + "chart").ToList();

        // The second chart's plot area at a manual place: its inner rectangle (layoutTarget inner) as fractions.
        var manualPart = PartOf(charts[1]);
        var manual = Load(manualPart);
        var plotArea = manual.Descendants(c + "plotArea").First();
        plotArea.Element(c + "layout")?.Remove();
        plotArea.AddFirst(new System.Xml.Linq.XElement(c + "layout", new System.Xml.Linq.XElement(c + "manualLayout",
            new System.Xml.Linq.XElement(c + "layoutTarget", new System.Xml.Linq.XAttribute("val", "inner")),
            new System.Xml.Linq.XElement(c + "xMode", new System.Xml.Linq.XAttribute("val", "edge")),
            new System.Xml.Linq.XElement(c + "yMode", new System.Xml.Linq.XAttribute("val", "edge")),
            new System.Xml.Linq.XElement(c + "x", new System.Xml.Linq.XAttribute("val", "0.3")),
            new System.Xml.Linq.XElement(c + "y", new System.Xml.Linq.XAttribute("val", "0.2")),
            new System.Xml.Linq.XElement(c + "w", new System.Xml.Linq.XAttribute("val", "0.5")),
            new System.Xml.Linq.XElement(c + "h", new System.Xml.Linq.XAttribute("val", "0.45")))));
        Save(manualPart, manual);

        var part = PartOf(charts[0]);
        var chart = Load(part);
        var space = chart.Root!;
        space.Element(c + "roundedCorners")?.Remove();
        // roundedCorners follows date1904 and lang in c:chartSpace.
        var after = space.Elements().LastOrDefault(e => e.Name.LocalName is "date1904" or "lang");
        var rounded = new System.Xml.Linq.XElement(c + "roundedCorners", new System.Xml.Linq.XAttribute("val", "1"));
        if (after != null)
            after.AddAfterSelf(rounded);
        else
            space.AddFirst(rounded);
        // The frame the corners show on: a light fill and a 3 pt red line (c:spPr follows c:chart).
        System.Xml.Linq.XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        space.Element(c + "spPr")?.Remove();
        space.Element(c + "chart")!.AddAfterSelf(new System.Xml.Linq.XElement(c + "spPr",
            new System.Xml.Linq.XElement(a + "solidFill", new System.Xml.Linq.XElement(a + "srgbClr", new System.Xml.Linq.XAttribute("val", "F2F2F2"))),
            new System.Xml.Linq.XElement(a + "ln", new System.Xml.Linq.XAttribute("w", "38100"),
                new System.Xml.Linq.XElement(a + "solidFill", new System.Xml.Linq.XElement(a + "srgbClr", new System.Xml.Linq.XAttribute("val", "C00000"))))));
        Save(part, chart);
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

    /// <summary>Replaces the chart's sample data (first column categories, then one column per series).</summary>
    static void SetData(object target, string[] categories, (string Name, double[] Values)[] series)
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
                sheet.Cells[i + 2, k + 2].Value2 = series[k].Values[i];
        }
        var range = $"$A$1:${(char)('A' + series.Length)}${categories.Length + 1}";
        if ((int)sheet.ListObjects.Count > 0)
            sheet.ListObjects[1].Resize(sheet.Range[range]);
        chart.SetSourceData($"='{sheet.Name}'!{range}", 2);   // xlColumns
    }
}
