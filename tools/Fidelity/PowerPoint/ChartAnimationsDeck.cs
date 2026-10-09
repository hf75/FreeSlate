using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Charts built step by step (catalog: animation.build, animation.targets; a:bldChart, p:graphicEl with a:chart): a
/// column chart wiped in by series, by category and by the elements of either, a line chart by series and by
/// category, and a pie by category. PowerPoint's object model refuses chart builds of modern charts, so every slide
/// gets a plain click effect on its chart and the patch splits it into the build's steps; PowerPoint then saves the
/// deck once more.
/// </summary>
[SupportedOSPlatform("windows")]
static class ChartAnimationsDeck
{
    // XlChartType
    const int ColumnClustered = 51, LineMarkers = 65, Pie = 5;
    // MsoAnimEffect, MsoAnimTriggerType, MsoAnimateByLevel, MsoAnimDirection
    const int EffectWipe = 22, EffectFade = 10, OnClick = 1, LevelNone = 0, DirectionBottom = 11, DirectionLeft = 4;

    const int Series = 3, Categories = 4;

    /// <summary>The build of every slide's chart (ST_AnimationChartBuildType) and its series count.</summary>
    static readonly (string Build, int Series)[] Builds =
    [
        ("series", Series), ("category", Series), ("seriesEl", Series), ("categoryEl", Series),
        ("series", Series), ("category", Series), ("category", 1),
    ];

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        string[] categories = ["North", "South", "East", "West"];
        (string, double?[])[] series = [("2023", [4, 6, 5, 7]), ("2024", [5, 7, 6, 8]), ("2025", [6, 5, 7, 9])];

        foreach (var title in new[] { "Columns by series", "Columns by category", "Columns by series elements", "Columns by category elements" })
        {
            object slide = Slide(presentation, title);
            Animate(slide, Add(slide, ColumnClustered, c => SetData((object)c, categories, series)), EffectWipe, DirectionBottom);
        }
        foreach (var title in new[] { "Lines by series", "Lines by category" })
        {
            object slide = Slide(presentation, title);
            Animate(slide, Add(slide, LineMarkers, c => SetData((object)c, categories, series)), EffectWipe, DirectionLeft);
        }
        object pie = Slide(presentation, "Pie by category");
        Animate(pie, Add(pie, Pie, c => SetData((object)c, categories, [("Share", [4, 6, 5, 7])])), EffectFade, 0);
    }

    /// <summary>A click effect on the chart's frame; the patch builds it step by step.</summary>
    static void Animate(object target, dynamic frame, int effectType, int direction)
    {
        dynamic slide = target;
        var effect = slide.TimeLine.MainSequence.AddEffect(frame, effectType, LevelNone, OnClick);
        if (direction != 0)
            effect.EffectParameters.Direction = direction;
        effect.Timing.Duration = 0.5f;
    }

    /// <summary>
    /// Splits each slide's click effect into the steps of its build: the background (gridLegend) first, then the
    /// series, the categories or their points, each on a click of its own; the build list names the build.
    /// </summary>
    public static void Patch(string path)
    {
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main";
        using var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Update);
        for (var i = 0; i < Builds.Length; i++)
        {
            var (build, seriesCount) = Builds[i];
            var name = $"ppt/slides/slide{i + 1}.xml";
            XDocument document;
            using (var stream = zip.GetEntry(name)!.Open())
                document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            var main = document.Descendants(p + "cTn").First(c => (string?)c.Attribute("nodeType") == "mainSeq");
            var clicks = main.Element(p + "childTnLst")!;
            var template = clicks.Elements(p + "par").First();
            var steps = new List<(int Series, int Category, string Step)> { (-3, -3, "gridLegend") };
            var categoryCount = Categories;
            steps.AddRange(build switch
            {
                "series" => Enumerable.Range(0, seriesCount).Select(s => (s, -4, "series")),
                "category" => Enumerable.Range(0, categoryCount).Select(c => (-4, c, "category")),
                "seriesEl" => Enumerable.Range(0, seriesCount).SelectMany(s => Enumerable.Range(0, categoryCount).Select(c => (s, c, "ptInSeries"))),
                _ => Enumerable.Range(0, categoryCount).SelectMany(c => Enumerable.Range(0, seriesCount).Select(s => (s, c, "ptInCategory"))),
            });
            template.Remove();
            foreach (var (seriesIdx, categoryIdx, step) in steps)
            {
                var click = new XElement(template);
                foreach (var shape in click.Descendants(p + "spTgt"))
                {
                    shape.RemoveNodes();
                    shape.Add(new XElement(p + "graphicEl", new XElement(a + "chart",
                        new XAttribute("seriesIdx", seriesIdx), new XAttribute("categoryIdx", categoryIdx), new XAttribute("bldStep", step))));
                }
                clicks.Add(click);
            }
            var id = 1;
            foreach (var node in document.Descendants(p + "cTn"))
                node.SetAttributeValue("id", id++);
            foreach (var graphic in document.Descendants(p + "bldGraphic"))
            {
                graphic.RemoveNodes();
                graphic.Add(new XElement(p + "bldSub", new XElement(a + "bldChart", new XAttribute("bld", build))));
            }
            zip.GetEntry(name)!.Delete();
            using var output = zip.CreateEntry(name).Open();
            document.Save(output, SaveOptions.DisableFormatting);
        }
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
