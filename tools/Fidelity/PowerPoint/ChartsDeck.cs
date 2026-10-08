using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// The charts deck: chart types, data labels, axis titles, legend positions, fonts, number formats and data
/// shapes (negative values, long and many categories, small charts) as PowerPoint builds them by default.
/// </summary>
[SupportedOSPlatform("windows")]
static class Charts
{
    // XlChartType
    const int ColumnClustered = 51, ColumnStacked = 52, ColumnStacked100 = 53, BarClustered = 57, Line = 4, LineMarkers = 65,
        Pie = 5, Doughnut = -4120, Area = 1, AreaStacked = 76, Scatter = -4169;
    // XlLegendPosition, XlDataLabelPosition
    const int LegendBottom = -4107, LegendTop = -4160, LegendLeft = -4131, LegendRight = -4152, LegendCorner = 2;
    const int LabelCenter = -4108, LabelBestFit = 5;

    static readonly string[] Quarters = ["Q1", "Q2", "Q3", "Q4"];

    public static void Build(dynamic deck)
    {
        // Static binding: lambdas and collection expressions cannot be arguments of dynamically bound calls.
        object presentation = deck;
        Chart(presentation, "Data labels", ColumnClustered, c => Labels(c));
        Chart(presentation, "Stacked, centered labels", ColumnStacked, c => Labels(c, LabelCenter));
        Chart(presentation, "100 % stacked, legend top", ColumnStacked100, c => c.Legend.Position = LegendTop);
        Chart(presentation, "Clustered bar, legend right", BarClustered, c => c.Legend.Position = LegendRight);
        Chart(presentation, "Line with markers", LineMarkers);

        object areas = Slide(presentation, "Area and stacked area");
        Add(areas, Area, 40, 120, 430, 390);
        Add(areas, AreaStacked, 490, 120, 430, 390);

        Chart(presentation, "Doughnut, percentages", Doughnut, c =>
        {
            var labels = Labels(c);
            labels.ShowValue = false;
            labels.ShowPercentage = true;
        });
        Chart(presentation, "Pie, category and percentage", Pie, c =>
        {
            var labels = Labels(c, LabelBestFit);
            labels.ShowValue = false;
            labels.ShowCategoryName = true;
            labels.ShowPercentage = true;
        });
        Chart(presentation, "Scatter", Scatter, null, c => SetData((object)c, ["1", "2", "3", "5", "8"], [("Measured", [2.1, 3.9, 6.2, 9.8, 16.5])]));
        Chart(presentation, "Titles", ColumnClustered, c =>
        {
            c.HasTitle = true;
            c.ChartTitle.Text = "Revenue by quarter";
            c.Axes(1).HasTitle = true;
            c.Axes(1).AxisTitle.Text = "Quarter";
            c.Axes(2).HasTitle = true;
            c.Axes(2).AxisTitle.Text = "Million €";
        }, c => SetData((object)c, Quarters, [("North", [12, 15, 14, 18]), ("South", [9, 11, 13, 12])]));
        Chart(presentation, "Calibri", ColumnClustered, c => c.ChartArea.Format.TextFrame2.TextRange.Font.Name = "Calibri");
        Chart(presentation, "Arial", ColumnClustered, c => c.ChartArea.Format.TextFrame2.TextRange.Font.Name = "Arial");
        Chart(presentation, "Thousands", ColumnClustered, c => c.Axes(2).TickLabels.NumberFormat = "#,##0",
            c => SetData((object)c, Quarters, [("2024", [1250, 2380, 1975, 3120]), ("2025", [1430, 2210, 2490, 3560])]));
        Chart(presentation, "Negative values", ColumnClustered, null,
            c => SetData((object)c, Quarters, [("Result", [3.5, -1.2, 2.4, -2.8]), ("Plan", [2, 1, 1.5, 0.5])]));
        Chart(presentation, "Long categories", ColumnClustered, null,
            c => SetData((object)c, ["North America and Canada", "Europe, Middle East and Africa", "Asia Pacific", "Latin America"], [("Share", [42, 31, 18, 9])]));
        Chart(presentation, "Twelve months", Line, null,
            c => SetData((object)c, ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
                [("Visitors", [120, 135, 160, 158, 190, 240, 310, 298, 220, 180, 150, 170])]));

        object small = Slide(presentation, "Small charts, legend left and corner");
        var left = Add(small, ColumnClustered, 40, 140, 280, 200);
        left.Legend.Position = LegendLeft;
        var corner = Add(small, Line, 340, 140, 280, 200);
        corner.Legend.Position = LegendCorner;
        Add(small, Pie, 640, 140, 280, 200);
    }

    internal static dynamic Slide(object deck, string title)
    {
        dynamic presentation = deck;
        var slide = CorpusGenerator.AddSlide(presentation, CorpusGenerator.LayoutTitleOnly);
        slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = title;
        return slide;
    }

    /// <summary>One chart per slide, below the title.</summary>
    internal static void Chart(object presentation, string title, int type, Action<dynamic>? format = null, Action<dynamic>? data = null)
    {
        var chart = Add((object)Slide(presentation, title), type, 80, 120, 800, 400, data);
        format?.Invoke(chart);
    }

    internal static dynamic Add(object target, int type, float left, float top, float width, float height, Action<dynamic>? data = null)
    {
        dynamic slide = target;
        var chart = slide.Shapes.AddChart2(-1, type, left, top, width, height).Chart;
        data?.Invoke(chart);
        chart.ChartData.Workbook.Close();
        return chart;
    }

    /// <summary>Data labels for every series; returns the labels of the last series for further settings.</summary>
    static dynamic Labels(dynamic chart, int? position = null)
    {
        dynamic? labels = null;
        int count = chart.SeriesCollection().Count;
        for (var i = 1; i <= count; i++)
        {
            var series = chart.SeriesCollection(i);
            series.HasDataLabels = true;
            labels = series.DataLabels();
            if (position is { } p)
                labels.Position = p;
        }
        return labels!;
    }

    /// <summary>Replaces the chart's sample data (first column categories or x values, then one column per series).</summary>
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
