using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Underlines (catalog: text.underline): all underline styles in the text color and in a color of their own (uFill),
/// underline lines of their own width (uLn, patched: the object model cannot set them), underlines that follow the
/// text (uLnTx, uFillTx) across runs of different colors and sizes, and word underlines.
/// </summary>
[SupportedOSPlatform("windows")]
static class UnderlineDeck
{
    // MsoTextUnderlineType, single line to wavy double
    static readonly (int Style, string Name)[] Styles =
    [
        (2, "single"), (3, "double"), (4, "heavy"), (5, "dotted"), (6, "dotted heavy"), (7, "dash"), (8, "dash heavy"),
        (9, "long dash"), (10, "long dash heavy"), (11, "dot dash"), (12, "dot dash heavy"), (13, "dot dot dash"),
        (14, "dot dot dash heavy"), (15, "wavy"), (16, "wavy heavy"), (17, "wavy double"), (1, "words only here"),
    ];

    static dynamic Box(dynamic slide, float left, float top, string text, float size = 26)
    {
        var box = slide.Shapes.AddTextbox(1, left, top, 440, size * 1.6f);
        var range = box.TextFrame2.TextRange;
        range.Text = text;
        range.Font.Name = "Arial";
        range.Font.Size = size;
        return box;
    }

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        foreach (var colored in new[] { false, true })
        {
            var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
            for (var i = 0; i < Styles.Length; i++)
            {
                var (style, name) = Styles[i];
                var box = Box(slide, 20 + (i / 9) * 470, 10 + (i % 9) * 58, $"Underline {name}");
                var font = box.TextFrame2.TextRange.Font;
                font.UnderlineStyle = style;
                if (colored)
                {
                    font.Fill.ForeColor.RGB = CorpusGenerator.Rgb(31, 78, 121);
                    font.UnderlineColor.RGB = CorpusGenerator.Rgb(192, 0, 0);
                }
            }
        }

        // Lines of their own (patched), following the text, mixed sizes and colors.
        var own = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        string[] names = ["uLn thin", "uLn thick", "uLn dashed", "uLnTx uFillTx", "mixed runs"];
        for (var i = 0; i < names.Length; i++)
        {
            var box = Box(own, 20, 20 + i * 100, $"Underlined {names[i]} text", 32);
            box.Name = names[i];
            box.TextFrame2.TextRange.Font.UnderlineStyle = 2;
        }
        var mixed = own.Shapes.Item("mixed runs").TextFrame2.TextRange;
        mixed.Characters(1, 10).Font.Size = 20;
        mixed.Characters(12, 5).Font.Fill.ForeColor.RGB = CorpusGenerator.Rgb(0, 128, 0);
        mixed.Characters(18, 4).Font.Size = 44;
    }

    static readonly XNamespace A = ShapePatch.A;

    static XElement Fill(string hex) => new(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", hex)));

    /// <summary>Adds underline elements to a run's properties where the schema puts them (before the fonts).</summary>
    static void Underline(XElement rPr, params XElement[] elements)
    {
        var fonts = rPr.Elements().FirstOrDefault(e => e.Name.LocalName is "latin" or "ea" or "cs" or "sym" or "hlinkClick" or "hlinkMouseOver" or "rtl" or "extLst");
        if (fonts != null)
            fonts.AddBeforeSelf(elements);
        else
            rPr.Add(elements);
    }

    /// <summary>Gives the runs of the third slide's boxes underline lines and fills of their own.</summary>
    public static void Patch(string path)
    {
        void Runs(XElement shape, Action<XElement> change)
        {
            foreach (var rPr in shape.Descendants(A + "rPr"))
                change(rPr);
        }
        ShapePatch.Apply(path, new Dictionary<string, Action<XElement>>
        {
            ["uLn thin"] = shape => Runs(shape, r => Underline(r, new XElement(A + "uLn", new XAttribute("w", 6350), Fill("C00000")))),
            ["uLn thick"] = shape => Runs(shape, r => Underline(r, new XElement(A + "uLn", new XAttribute("w", 50800), Fill("2E75B6")),
                new XElement(A + "uFill", Fill("FFC000")))),
            ["uLn dashed"] = shape => Runs(shape, r => Underline(r, new XElement(A + "uLn", new XAttribute("w", 25400), Fill("7030A0"),
                new XElement(A + "prstDash", new XAttribute("val", "dash"))))),
            ["uLnTx uFillTx"] = shape => Runs(shape, r =>
            {
                r.AddFirst(Fill("00B050"));
                Underline(r, new XElement(A + "uLnTx"), new XElement(A + "uFillTx"));
            }),
        });
    }
}
