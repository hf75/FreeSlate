using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Charts built step by step (catalog: animation.build, animation.targets; a:bldChart, p:graphicEl with a:chart): a
/// column chart wiped in by series, by category and by the elements of either, a line chart by series and by
/// category, and a pie by category.
/// </summary>
[SupportedOSPlatform("windows")]
static class ChartAnimationsDeck
{
    // XlChartType
    const int ColumnClustered = 51, LineMarkers = 65, Pie = 5;
    // MsoAnimEffect, MsoAnimTriggerType, MsoAnimateByLevel, MsoAnimDirection
    const int EffectWipe = 22, EffectFade = 10, OnClick = 1, LevelNone = 0, DirectionBottom = 11, DirectionLeft = 4;
    const int ByCategory = 8, ByCategoryElements = 9, BySeries = 10, BySeriesElements = 11;

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        string[] categories = ["North", "South", "East", "West"];
        (string, double?[])[] series = [("2023", [4, 6, 5, 7]), ("2024", [5, 7, 6, 8]), ("2025", [6, 5, 7, 9])];

        foreach (var (title, level) in new[] { ("Columns by series", BySeries), ("Columns by category", ByCategory),
                     ("Columns by series elements", BySeriesElements), ("Columns by category elements", ByCategoryElements) })
        {
            var slide = Slide(presentation, title);
            var chart = Add(slide, ColumnClustered, c => SetData((object)c, categories, series));
            Animate(slide, chart, EffectWipe, DirectionBottom, level);
        }
        foreach (var (title, level) in new[] { ("Lines by series", BySeries), ("Lines by category", ByCategory) })
        {
            var slide = Slide(presentation, title);
            var chart = Add(slide, LineMarkers, c => SetData((object)c, categories, series));
            Animate(slide, chart, EffectWipe, DirectionLeft, level);
        }
        var pie = Slide(presentation, "Pie by category");
        var pieChart = Add(pie, Pie, c => SetData((object)c, categories, [("Share", [4, 6, 5, 7])]));
        Animate(pie, pieChart, EffectFade, 0, ByCategory);
    }

    /// <summary>A click effect on the chart's frame, built at the given level (a step for the background, then one per part).</summary>
    static void Animate(dynamic slide, dynamic frame, int effectType, int direction, int level)
    {
        var sequence = slide.TimeLine.MainSequence;
        var effect = sequence.AddEffect(frame, effectType, LevelNone, OnClick);
        if (direction != 0)
            effect.EffectParameters.Direction = direction;
        effect.Timing.Duration = 0.5f;
        sequence.ConvertToBuildLevel(effect, level);
    }

    static dynamic Slide(object deck, string title)
    {
        dynamic presentation = deck;
        var slide = CorpusGenerator.AddSlide(presentation, CorpusGenerator.LayoutTitleOnly);
        slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = title;
        return slide;
    }

    /// <summary>A chart on the slide; returns its frame (the shape the animation targets).</summary>
    static dynamic Add(object target, int type, Action<dynamic> data)
    {
        dynamic slide = target;
        var frame = slide.Shapes.AddChart2(-1, type, 80f, 110f, 800f, 410f);
        var chart = frame.Chart;
        data(chart);
        try { chart.ChartData.Workbook.Close(); } catch (Exception) { /* nothing to close */ }
        return frame;
    }

    /// <summary>Replaces the chart's sample data (first column categories, then one column per series).</summary>
    static void SetData(object target, string[] categories, (string Name, double?[] Values)[] series)
    {
        dynamic chart = target;
        var sheet = chart.ChartData.Workbook.Worksheets[1];
        sheet.Range["A1:Z100"].ClearContents();
        for (var i = 0; i < categories.Length; i++)
            sheet.Cells[i + 2, 1].Value2 = categories[i];
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
}
