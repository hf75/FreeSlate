using System.IO.Compression;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Vertical text (catalog: text.vertical): rotated text (vert, vert270) with every anchor and alignment, East Asian
/// vertical text (eaVert, mongolianVert) with upright CJK and rotated Latin, stacked WordArt text (wordArtVert) and
/// insets on vertical text bodies.
/// </summary>
[SupportedOSPlatform("windows")]
static class TextVerticalDeck
{
    const int Rectangle = 1;

    // MsoTextOrientation: upward = vert270, downward = vert, vertical far east = eaVert, vertical = mongolianVert,
    // horizontal rotated far east = wordArtVert.
    const int Upward = 2, Downward = 3, VerticalFarEast = 4, Vertical = 5, HorizontalRotatedFarEast = 6;

    // MsoVerticalAnchor and PpParagraphAlignment
    static readonly int[] Anchors = [1, 3, 4];
    static readonly int[] Alignments = [1, 2, 3];

    const string Latin = "Vertical text that wraps onto a second line\rSecond paragraph";
    const string Mixed = "縦書きのテキスト ABC 123 と日本語の文章\r二段落目 xyz";

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        foreach (var orientation in new[] { Downward, Upward })
        {
            var slide = Slide(presentation);
            for (var a = 0; a < Anchors.Length; a++)
            {
                for (var j = 0; j < Alignments.Length; j++)
                {
                    var box = Box(slide, 30 + (a * 3 + j) * 100, 40, 90, 300, Latin, 14);
                    box.TextFrame.Orientation = orientation;
                    box.TextFrame.VerticalAnchor = Anchors[a];
                    box.TextFrame.TextRange.ParagraphFormat.Alignment = Alignments[j];
                }
            }
            var inset = Box(slide, 30, 380, 300, 120, "Insets on rotated text", 16);
            inset.TextFrame.Orientation = orientation;
            inset.TextFrame.MarginLeft = 30f;
            inset.TextFrame.MarginTop = 10f;
            inset.TextFrame.MarginRight = 5f;
            inset.TextFrame.MarginBottom = 20f;
            var noWrap = Box(slide, 360, 380, 300, 120, "A long line that does not wrap at all", 16);
            noWrap.TextFrame.Orientation = orientation;
            noWrap.TextFrame.WordWrap = PowerPointSession.MsoFalse;
        }

        var eastAsian = Slide(presentation);
        int[] orientations = [VerticalFarEast, Vertical];
        for (var o = 0; o < orientations.Length; o++)
        {
            for (var a = 0; a < Anchors.Length; a++)
            {
                var box = Box(eastAsian, 30 + (o * 3 + a) * 150, 40, 130, 300, Mixed, 18);
                box.TextFrame.Orientation = orientations[o];
                box.TextFrame.VerticalAnchor = Anchors[a];
            }
        }
        var single = Box(eastAsian, 30, 380, 400, 130, "日本語 Latin 2024", 24);
        single.TextFrame.Orientation = VerticalFarEast;

        var stacked = Slide(presentation);
        string[] texts = ["STACKED", "Stacked letters wrap", "縦書き text 123"];
        for (var i = 0; i < texts.Length; i++)
        {
            var box = Box(stacked, 30 + i * 200, 40, 170, 300, texts[i], 20);
            box.TextFrame.Orientation = HorizontalRotatedFarEast;
            box.TextFrame.VerticalAnchor = Anchors[i];
        }
        var wide = Box(stacked, 630, 40, 300, 300, "WordArt vertical", 28);
        wide.TextFrame.Orientation = HorizontalRotatedFarEast;
        wide.TextFrame.TextRange.ParagraphFormat.Alignment = 2;

        // Text that stays upright in rotated shapes (bodyPr upright, "do not rotate text").
        var upright = Slide(presentation);
        float[] rotations = [30, 90, 135, 200];
        for (var i = 0; i < rotations.Length; i++)
        {
            var box = Box(upright, 40 + i * 230, 60, 200, 120, "Upright text in a rotated shape", 16);
            box.Rotation = rotations[i];
            box.Name = $"upright-{i}";
        }
        var turned = Box(upright, 120, 300, 260, 160, "Upright vertical text", 18);
        turned.Rotation = 40;
        turned.TextFrame.Orientation = Downward;
        turned.Name = "upright-vert";
        var plain = Box(upright, 560, 300, 260, 160, "Rotated with its shape", 18);
        plain.Rotation = 40;
    }

    /// <summary>Sets bodyPr upright on the shapes named upright-… (PowerPoint has no COM property for it).</summary>
    public static void Patch(string path)
    {
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main";
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)).ToList())
        {
            XDocument slide;
            using (var stream = entry.Open())
                slide = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            var changed = false;
            foreach (var shape in slide.Descendants(p + "sp"))
            {
                var name = shape.Descendants(p + "cNvPr").FirstOrDefault()?.Attribute("name")?.Value ?? "";
                if (name.StartsWith("upright-", StringComparison.Ordinal) && shape.Element(p + "txBody")?.Element(a + "bodyPr") is { } bodyPr)
                {
                    bodyPr.SetAttributeValue("upright", "1");
                    changed = true;
                }
            }
            if (!changed)
                continue;
            entry.Delete();
            using var output = zip.CreateEntry(entry.FullName).Open();
            slide.Save(output, SaveOptions.DisableFormatting);
        }
    }

    static dynamic Slide(object deck) => CorpusGenerator.AddSlide((dynamic)deck, CorpusGenerator.LayoutBlank);

    static dynamic Box(object target, float left, float top, float width, float height, string text, float size)
    {
        dynamic slide = target;
        var shape = slide.Shapes.AddShape(Rectangle, left, top, width, height);
        shape.Fill.ForeColor.RGB = CorpusGenerator.Rgb(222, 235, 247);
        shape.Line.ForeColor.RGB = CorpusGenerator.Rgb(68, 114, 196);
        var range = shape.TextFrame.TextRange;
        range.Text = text;
        range.Font.Size = size;
        range.Font.Color.RGB = 0;
        return shape;
    }
}
