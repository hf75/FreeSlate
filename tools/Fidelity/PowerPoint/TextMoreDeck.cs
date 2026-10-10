using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Remaining text properties (catalog: text.fonts, text.body, text.runs): symbols inserted from symbol fonts (a:sym with
/// characters at U+F000 to U+F0FF), line spacing in compatibility mode (a:bodyPr compatLnSpc), runs with normalized
/// heights and horizontal numbers in vertical text (a:rPr normalizeH, kumimoji), line breaks of their own sizes,
/// autofit's font scale (a:normAutofit fontScale), GPOS kerning of variable fonts, typefaces that are no family's name
/// and several lines on single-path WordArt transforms.
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

        // Line breaks (a:br) of their own sizes: after text they leave the line as it is, alone they size it. The
        // first box is built like a title seen in a user's deck: 90 % spacing, centered and overflowing.
        var breaks = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        (string Text, float Size)[][] parts =
        [
            [("Opening Lecture", 29), ("\v", 53), ("THE TITLE", 60), ("\v", 26), ("\v", 26), ("Subject: ", 26),
                ("Shared Workshops", 24), ("\v", 24), ("\v", 24), ("March 2024", 18)],
            [("abc", 20), ("\v", 60), ("def", 20), ("\v", 10), ("ghi", 40), ("\v", 10), ("jkl", 20)],
            [("abc", 20), ("\v", 20), ("\v", 60), ("\v", 10), ("def", 20), ("\v", 40), ("\v", 20), ("ghi", 20)],
        ];
        (float Left, float Top, float Width, float Height)[] frames = [(30, 180, 430, 165), (500, 20, 200, 500), (730, 20, 200, 500)];
        for (var i = 0; i < parts.Length; i++)
        {
            var box = Box(breaks, frames[i].Left, frames[i].Top, frames[i].Width, frames[i].Height, string.Concat(parts[i].Select(part => part.Text)));
            box.Height = frames[i].Height;   // AddTextbox fitted the box to its empty text
            var range = box.TextFrame2.TextRange;
            var start = 1;
            foreach (var (text, size) in parts[i])
            {
                range.Characters(start, text.Length).Font.Size = size;
                start += text.Length;
            }
            if (i == 0)
            {
                box.TextFrame2.VerticalAnchor = 3;   // msoAnchorMiddle
                range.ParagraphFormat.SpaceWithin = 0.9f;
            }
        }

        // Autofit's font scale on text of several sizes: unscaled, then 90 %, 77.5 % and 62.5 % (patched).
        var autofit = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        int[] sizes = [29, 26, 25, 24, 18, 15, 11];
        for (var i = 0; i < 4; i++)
        {
            var box = Box(autofit, 20 + i * 235, 20, 225, 500, string.Join("\r", sizes.Select(size => $"Shrunk {size} pt")));
            box.Height = 500f;
            var range = box.TextFrame2.TextRange;
            for (var line = 0; line < sizes.Length; line++)
                range.Paragraphs(line + 1).Font.Size = sizes[line];
        }

        // Kerning of fonts without a 'kern' table: from GPOS for variable fonts (Bahnschrift, also bold at its weight
        // axis), not for static TrueType fonts with GPOS pairs (Gabriola).
        var gpos = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        (string Font, bool Bold)[] kerned = [("Bahnschrift", false), ("Bahnschrift", true), ("Gabriola", false)];
        for (var i = 0; i < kerned.Length; i++)
        {
            var box = Box(gpos, 30, 30 + i * 120, 900, 100, "AVAWAYATAV To Wa Yo LT Te Vo P. F, r. y.", 44);
            var range = box.TextFrame2.TextRange;
            range.Font.Name = kerned[i].Font;
            range.Font.Bold = kerned[i].Bold ? PowerPointSession.MsoTrue : PowerPointSession.MsoFalse;
            range.Font.Kerning = 12f;
        }

        // Typefaces that are no family's name: PowerPoint finds a family for some (PostScript names, style words,
        // hyphens, a few name beginnings) and draws the others in Calibri.
        var names = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        string[] typefaces = ["SegoeUI", "ArialMT", "TimesNewRomanPSMT", "Georgia-Bold", "TahomaBold", "Cambria Bold", "CambriaMath", "TahomaX"];
        for (var i = 0; i < typefaces.Length; i++)
        {
            var box = Box(names, 30 + i / 4 * 460, 30 + i % 4 * 120, 440, 100, typefaces[i] + " Diskussion", 28);
            box.TextFrame2.TextRange.Font.Name = typefaces[i];
        }

        // Several lines on a single-path WordArt transform (patched: circle, arch up, arch down) and a long line on a
        // circle, which is not wrapped.
        var warps = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        string[] warpTexts = ["Line one text\rLine two\rThird line here", "Line one text\rLine two\rThird line here",
            "Line one text\rLine two\rThird line here", string.Join(' ', Enumerable.Repeat("TTT", 18))];
        for (var i = 0; i < warpTexts.Length; i++)
        {
            var box = Box(warps, 40 + i * 225, 150, 200, 200, warpTexts[i], 18);
            box.Height = 200f;
        }
    }

    /// <summary>
    /// compatLnSpc on the second slide's right box; normalizeH and kumimoji on the third slide's right boxes; the font
    /// scales of the fifth slide's boxes; the WordArt transforms of the eighth slide's boxes.
    /// </summary>
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
        Edit("ppt/slides/slide5.xml", document =>
        {
            var shapes = document.Descendants(p + "sp").ToList();
            string[] scales = ["90000", "77500", "62500"];
            for (var i = 0; i < scales.Length; i++)
            {
                var body = shapes[i + 1].Descendants(a + "bodyPr").First();
                var fit = new XElement(a + "normAutofit", new XAttribute("fontScale", scales[i]));
                if (body.Elements().FirstOrDefault(e => e.Name.LocalName is "noAutofit" or "normAutofit" or "spAutoFit") is { } old)
                    old.ReplaceWith(fit);
                else
                    body.AddFirst(fit);
            }
        });
        Edit("ppt/slides/slide8.xml", document =>
        {
            var shapes = document.Descendants(p + "sp").ToList();
            string[] presets = ["textCircle", "textArchUp", "textArchDown", "textCircle"];
            for (var i = 0; i < presets.Length; i++)
                shapes[i].Descendants(a + "bodyPr").First().AddFirst(new XElement(a + "prstTxWarp", new XAttribute("prst", presets[i]), new XElement(a + "avLst")));
        });
    }
}
