using System.IO.Compression;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Color mapping (catalog: presentation.colorMap, chart.part): the master maps the background to the dark theme colors
/// and swaps accent1 and accent2 (patched after saving); a slide overrides the mapping back; one chart keeps its own
/// light mapping (c:clrMapOvr), the other follows the slide.
/// </summary>
[SupportedOSPlatform("windows")]
static class ColorMapDeck
{
    const int Rectangle = 1, ColumnClustered = 51;

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        // Theme colors through the master's mapping: accents, text and background colors, a hyperlink.
        var swatches = Slide(presentation, "Mapped colors");
        string[] names = ["accent1", "accent2", "accent3", "accent4", "accent5", "accent6"];
        for (var i = 0; i < names.Length; i++)
        {
            var shape = swatches.Shapes.AddShape(Rectangle, 40 + i * 150, 140, 130, 90);
            shape.Fill.ForeColor.ObjectThemeColor = 5 + i;   // msoThemeColorAccent1 …
            shape.Line.Visible = PowerPointSession.MsoFalse;
            shape.TextFrame.TextRange.Text = names[i];
        }
        string[] texts = ["Text 1", "Background 1", "Text 2", "Background 2"];
        int[] themeColors = [13, 14, 15, 16];   // msoThemeColorText1, Background1, Text2, Background2
        for (var i = 0; i < texts.Length; i++)
        {
            var shape = swatches.Shapes.AddShape(Rectangle, 40 + i * 225, 280, 200, 80);
            shape.Fill.ForeColor.ObjectThemeColor = themeColors[(i + 1) % 4];
            shape.Line.Visible = PowerPointSession.MsoFalse;
            var range = shape.TextFrame.TextRange;
            range.Text = texts[i];
            range.Font.Color.ObjectThemeColor = themeColors[i];
        }
        var link = swatches.Shapes.AddTextbox(1, 40, 400, 400, 40);
        link.TextFrame.TextRange.Text = "A hyperlink";
        link.TextFrame.TextRange.ActionSettings[1].Hyperlink.Address = "https://example.com/";

        // Charts: the first keeps its own light mapping (patched), the second follows the slide.
        var charts = Slide(presentation, "Chart color maps");
        Chart(charts, 40, 120);
        Chart(charts, 500, 120);

        // A slide that overrides the master's mapping back to the light colors (patched).
        var light = Slide(presentation, "Slide override");
        for (var i = 0; i < 3; i++)
        {
            var shape = light.Shapes.AddShape(Rectangle, 40 + i * 300, 160, 260, 120);
            shape.Fill.ForeColor.ObjectThemeColor = 5 + i;
            shape.Line.Visible = PowerPointSession.MsoFalse;
            shape.TextFrame.TextRange.Text = names[i];
        }

        BackgroundFill(presentation);
    }

    /// <summary>Shapes filled with the slide's background (p:sp/@useBgFill) over a gradient background and other shapes.</summary>
    static void BackgroundFill(object deck)
    {
        var slide = Slide(deck, "Background fill");
        slide.FollowMasterBackground = PowerPointSession.MsoFalse;
        slide.Background.Fill.TwoColorGradient(3, 1);   // diagonal up
        slide.Background.Fill.ForeColor.RGB = CorpusGenerator.Rgb(255, 230, 150);
        slide.Background.Fill.BackColor.RGB = CorpusGenerator.Rgb(60, 120, 200);
        var stripe = slide.Shapes.AddShape(Rectangle, 0, 250, 960, 120);
        stripe.Fill.ForeColor.RGB = CorpusGenerator.Rgb(200, 40, 40);
        stripe.Line.Visible = PowerPointSession.MsoFalse;
        (float Left, float Top, float Rotation)[] holes = [(80, 200, 0), (400, 220, 30), (700, 180, 0)];
        foreach (var (left, top, rotation) in holes)
        {
            var hole = slide.Shapes.AddShape(9, left, top, 180, 220);   // oval
            hole.Fill.Background();
            hole.Line.ForeColor.RGB = CorpusGenerator.Rgb(0, 0, 0);
            hole.Rotation = rotation;
        }
    }

    static dynamic Slide(object deck, string title)
    {
        dynamic presentation = deck;
        var slide = CorpusGenerator.AddSlide(presentation, CorpusGenerator.LayoutTitleOnly);
        slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = title;
        return slide;
    }

    static void Chart(object target, float left, float top)
    {
        dynamic slide = target;
        var chart = slide.Shapes.AddChart2(-1, ColumnClustered, left, top, 420, 300).Chart;
        chart.HasTitle = true;
        chart.ChartTitle.Text = "Chart";
        chart.SeriesCollection(1).HasDataLabels = true;
        try { chart.ChartData.Workbook.Close(); } catch (Exception) { }
    }

    /// <summary>Dark mapping with swapped accents on the master, the light mapping on slide 3 and the first chart.</summary>
    public static void Patch(string path)
    {
        XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main";
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        XNamespace c = "http://schemas.openxmlformats.org/drawingml/2006/chart";
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        void Update(string name, Action<XDocument> change)
        {
            var entry = zip.GetEntry(name) ?? throw new InvalidOperationException($"{name} missing");
            XDocument doc;
            using (var stream = entry.Open())
                doc = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            change(doc);
            entry.Delete();
            using var output = zip.CreateEntry(name).Open();
            doc.Save(output, SaveOptions.DisableFormatting);
        }
        Update("ppt/slideMasters/slideMaster1.xml", doc =>
        {
            var map = doc.Root!.Element(p + "clrMap")!;
            map.SetAttributeValue("bg1", "dk1");
            map.SetAttributeValue("tx1", "lt1");
            map.SetAttributeValue("bg2", "dk2");
            map.SetAttributeValue("tx2", "lt2");
            map.SetAttributeValue("accent1", "accent2");
            map.SetAttributeValue("accent2", "accent1");
        });
        Update("ppt/slides/slide3.xml", doc =>
        {
            var mapping = new XElement(a + "overrideClrMapping",
                new XAttribute("bg1", "lt1"), new XAttribute("tx1", "dk1"), new XAttribute("bg2", "lt2"), new XAttribute("tx2", "dk2"),
                new XAttribute("accent1", "accent1"), new XAttribute("accent2", "accent2"), new XAttribute("accent3", "accent3"),
                new XAttribute("accent4", "accent4"), new XAttribute("accent5", "accent5"), new XAttribute("accent6", "accent6"),
                new XAttribute("hlink", "hlink"), new XAttribute("folHlink", "folHlink"));
            doc.Root!.Element(p + "clrMapOvr")?.Remove();
            doc.Root!.Element(p + "cSld")!.AddAfterSelf(new XElement(p + "clrMapOvr", mapping));
        });
        var firstChart = zip.Entries.Select(e => e.FullName).Where(n => n.StartsWith("ppt/charts/chart", StringComparison.Ordinal) && n.EndsWith(".xml", StringComparison.Ordinal))
            .OrderBy(n => n.Length).ThenBy(n => n, StringComparer.Ordinal).First();
        Update(firstChart, doc =>
        {
            var map = new XElement(c + "clrMapOvr",
                new XAttribute("bg1", "lt1"), new XAttribute("tx1", "dk1"), new XAttribute("bg2", "lt2"), new XAttribute("tx2", "dk2"),
                new XAttribute("accent1", "accent1"), new XAttribute("accent2", "accent2"), new XAttribute("accent3", "accent3"),
                new XAttribute("accent4", "accent4"), new XAttribute("accent5", "accent5"), new XAttribute("accent6", "accent6"),
                new XAttribute("hlink", "hlink"), new XAttribute("folHlink", "folHlink"));
            doc.Root!.Element(c + "chart")!.AddBeforeSelf(map);
        });
    }
}
