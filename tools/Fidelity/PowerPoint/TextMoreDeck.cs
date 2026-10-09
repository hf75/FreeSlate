using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Remaining text properties (catalog: text.fonts, text.body, text.runs): symbols inserted from symbol fonts (a:sym with
/// characters at U+F000 to U+F0FF), line spacing in compatibility mode (a:bodyPr compatLnSpc) and runs with normalized
/// heights and horizontal numbers in vertical text (a:rPr normalizeH, kumimoji).
/// </summary>
[SupportedOSPlatform("windows")]
static class TextMoreDeck
{
    static dynamic Box(dynamic slide, float left, float top, float width, float height, string text, float size = 24)
    {
        var box = slide.Shapes.AddTextbox(1, left, top, width, height);
        box.TextFrame2.WordWrap = PowerPointSession.MsoTrue;
        box.TextFrame2.AutoSize = 0;
        box.Line.Visible = PowerPointSession.MsoTrue;
        box.Line.ForeColor.RGB = CorpusGenerator.Rgb(191, 191, 191);
        var range = box.TextFrame2.TextRange;
        range.Text = text;
        range.Font.Name = "Calibri";
        range.Font.Size = size;
        return box;
    }

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        // Symbols: Insert Symbol stores the font's character at U+F000 + code with the font as a:sym.
        var symbols = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        (string Font, int[] Codes)[] sets =
        [
            ("Wingdings", [0xF0E0, 0xF0FC, 0xF04A, 0xF0A8, 0xF06C]),
            ("Wingdings 2", [0xF050, 0xF0CE, 0xF097]),
            ("Wingdings 3", [0xF0C6, 0xF070]),
            ("Symbol", [0xF061, 0xF062, 0xF053, 0xF0A5]),
            ("Webdings", [0xF040, 0xF0FB]),
        ];
        for (var i = 0; i < sets.Length; i++)
        {
            var box = Box(symbols, 30, 20 + i * 100, 900, 80, sets[i].Font + ": ", 32);
            var range = box.TextFrame2.TextRange;
            foreach (var code in sets[i].Codes)
            {
                range.InsertAfter(" ");
                range.Characters(range.Length + 1, 0).InsertSymbol(sets[i].Font, code, PowerPointSession.MsoTrue);
            }
            range.InsertAfter(" end");
        }

        // Line spacing 150 % with and without compatibility spacing (patched).
        var spacing = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        foreach (var left in new[] { 30f, 490f })
        {
            var box = Box(spacing, left, 30, 440, 450, "First line of the text\rSecond line\rThird line with more words to wrap across the box", 28);
            box.TextFrame2.TextRange.ParagraphFormat.SpaceWithin = 1.5f;
            box.TextFrame2.TextRange.ParagraphFormat.SpaceAfter = 12f;
        }

        // Normalized heights and numbers in vertical text (patched).
        var runs = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        Box(runs, 30, 30, 440, 120, "Normal height Ag 123", 32);
        Box(runs, 490, 30, 440, 120, "Normalized Ag 123", 32);
        foreach (var left in new[] { 30f, 490f })
        {
            var vertical = Box(runs, left, 180, 200, 330, "縦書き 2024年 AB", 32);
            vertical.TextFrame2.Orientation = 5;   // msoTextOrientationVerticalFarEast
        }
    }

    /// <summary>compatLnSpc on the second slide's right box; normalizeH and kumimoji on the third slide's right boxes.</summary>
    public static void Patch(string path)
    {
        using var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Update);
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main";
        void Edit(string name, Action<XDocument> edit)
        {
            XDocument document;
            using (var stream = zip.GetEntry(name)!.Open())
                document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            edit(document);
            zip.GetEntry(name)!.Delete();
            using var output = zip.CreateEntry(name).Open();
            document.Save(output, SaveOptions.DisableFormatting);
        }
        Edit("ppt/slides/slide2.xml", document =>
        {
            var shapes = document.Descendants(p + "sp").ToList();
            shapes[1].Descendants(a + "bodyPr").First().SetAttributeValue("compatLnSpc", "1");
        });
        Edit("ppt/slides/slide3.xml", document =>
        {
            var shapes = document.Descendants(p + "sp").ToList();
            foreach (var rPr in shapes[1].Descendants(a + "rPr"))
                rPr.SetAttributeValue("normalizeH", "1");
            foreach (var rPr in shapes[3].Descendants(a + "rPr"))
                rPr.SetAttributeValue("kumimoji", "1");
        });
    }
}
