using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Further chart types (catalog: chart.radar, chart.stock, chart.surface, chart.analysis, chart.dataTable,
/// chart.scatterBubble, chart.ex): radar, stock, contour, trendlines and error bars, a data table, bubbles and the
/// Office 2016 chart types with PowerPoint's sample data.
/// </summary>
[SupportedOSPlatform("windows")]
static class ChartsMoreDeck
{
    // XlChartType
    const int Radar = -4151, RadarFilled = 82, StockHlc = 88, StockOhlc = 89, SurfaceTopView = 85, Scatter = -4169,
        Line = 4, ColumnClustered = 51, Bubble = 15;
    const int Treemap = 117, Histogram = 118, Waterfall = 119, Sunburst = 120, BoxWhisker = 121, Funnel = 123;

    static readonly string[] Quarters = ["Q1", "Q2", "Q3", "Q4"];

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        object radars = Slide(presentation, "Radar and filled radar");
        Add(radars, Radar, 40, 120, 430, 390);
        Add(radars, RadarFilled, 490, 120, 430, 390);

        object stocks = Slide(presentation, "Stock charts");
        string[] days = ["1.3.", "2.3.", "3.3.", "4.3.", "5.3."];
        Add(stocks, StockHlc, 40, 120, 430, 390, c => SetData((object)c, days,
            [("High", [56, 58, 57, 61, 60]), ("Low", [51, 53, 52, 55, 56]), ("Close", [55, 54, 56, 60, 57])]));
        Add(stocks, StockOhlc, 490, 120, 430, 390, c => SetData((object)c, days,
            [("Open", [52, 55, 54, 56, 60]), ("High", [56, 58, 57, 61, 60]), ("Low", [51, 53, 52, 55, 56]), ("Close", [55, 54, 56, 60, 57])]));

        object contour = Slide(presentation, "Contour");
        Add(contour, SurfaceTopView, 80, 120, 800, 400);

        object analysis = Slide(presentation, "Trendline and error bars");
        var scatter = Add(analysis, Scatter, 40, 120, 430, 390, c => SetData((object)c, ["1", "2", "3", "4", "5", "6"], [("Measured", [2.1, 3.9, 6.2, 7.8, 10.5, 11.9])]));
        var trend = scatter.SeriesCollection(1).Trendlines().Add(-4132);   // xlLinear
        trend.DisplayEquation = true;
        trend.DisplayRSquared = true;
        var line = Add(analysis, Line, 490, 120, 430, 390, c => SetData((object)c, Quarters, [("Plan", [10, 12, 11, 14])]));
        line.SeriesCollection(1).ErrorBar(1, 1, 2, 10);   // xlY, xlErrorBarIncludeBoth, xlErrorBarTypePercent, 10 %

        object table = Slide(presentation, "Data table and bubbles");
        var withTable = Add(table, ColumnClustered, 40, 120, 430, 390);
        withTable.HasDataTable = true;
        Add(table, Bubble, 490, 120, 430, 390);

        object office2016 = Slide(presentation, "Waterfall, funnel and histogram");
        Add(office2016, Waterfall, 20, 120, 300, 390);
        Add(office2016, Funnel, 330, 120, 300, 390);
        Add(office2016, Histogram, 640, 120, 300, 390);

        object hierarchies = Slide(presentation, "Treemap, sunburst and box and whisker");
        Add(hierarchies, Treemap, 20, 120, 300, 390);
        Add(hierarchies, Sunburst, 330, 120, 300, 390);
        Add(hierarchies, BoxWhisker, 640, 120, 300, 390);

        // Shapes drawn on a chart (c:userShapes): a shape with text in the chart's default size, an oval, a text
        // box with its own size and a line.
        object annotated = Slide(presentation, "Shapes on a chart");
        Add(annotated, 51, 80, 110, 700, 400, c =>
        {
            var rectangle = c.Shapes.AddShape(1, 300, 40, 150, 50);
            rectangle.TextFrame2.TextRange.Text = "In the chart";
            c.Shapes.AddShape(9, 500, 150, 80, 80);
            var note = c.Shapes.AddTextbox(1, 40, 340, 200, 30);
            note.TextFrame2.TextRange.Text = "A note";
            c.Shapes.AddLine(100, 100, 250, 200);
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
        try { chart.ChartData.Workbook.Close(); } catch (Exception) { /* chartex keeps its own data */ }
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
