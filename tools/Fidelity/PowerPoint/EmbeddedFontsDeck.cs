using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Embedded fonts (catalog: text.embeddedFonts): text in Georgia, Comic Sans MS and Consolas in all styles and at
/// sizes drawn as glyph outlines and by the browser, saved with the fonts embedded. PowerPoint embeds every style of
/// a family it uses, each with the characters of the presentation (subset, MicroType Express compressed). The
/// renderer uses the embedded fonts as on a computer without these families.
/// </summary>
[SupportedOSPlatform("windows")]
static class EmbeddedFontsDeck
{
    const string Pangram = "The quick brown fox jumps over the lazy dog 0123456789";
    const string Accents = "Größe Ärger Übermut œuvre façade «déjà vu» € – “quotes” …";

    static dynamic Box(dynamic slide, float left, float top, float width, float height, string text, string font, float size)
    {
        var box = slide.Shapes.AddTextbox(1, left, top, width, height);
        box.TextFrame2.WordWrap = PowerPointSession.MsoTrue;
        box.TextFrame2.AutoSize = 0;
        var range = box.TextFrame2.TextRange;
        range.Text = text;
        range.Font.Name = font;
        range.Font.Size = size;
        return box;
    }

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        // Georgia in its four styles, large (glyph outlines) and small (browser text).
        var georgia = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        (bool Bold, bool Italic)[] styles = [(false, false), (true, false), (false, true), (true, true)];
        for (var i = 0; i < styles.Length; i++)
        {
            var large = Box(georgia, 30, 20 + i * 90, 900, 60, "Georgia AVATAR Typography", "Georgia", 36);
            large.TextFrame2.TextRange.Font.Bold = styles[i].Bold ? PowerPointSession.MsoTrue : PowerPointSession.MsoFalse;
            large.TextFrame2.TextRange.Font.Italic = styles[i].Italic ? PowerPointSession.MsoTrue : PowerPointSession.MsoFalse;
            var small = Box(georgia, 30, 70 + i * 90, 900, 30, Pangram + " " + Accents, "Georgia", 12);
            small.TextFrame2.TextRange.Font.Bold = styles[i].Bold ? PowerPointSession.MsoTrue : PowerPointSession.MsoFalse;
            small.TextFrame2.TextRange.Font.Italic = styles[i].Italic ? PowerPointSession.MsoTrue : PowerPointSession.MsoFalse;
        }
        Box(georgia, 30, 390, 900, 120, Accents + " " + Pangram, "Georgia", 24);

        // Comic Sans MS and Consolas: a paragraph that wraps, mixed sizes and small text.
        var mixed = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        Box(mixed, 30, 20, 440, 250, Pangram + ". " + Accents + " Wrapping across several lines of the box.", "Comic Sans MS", 24);
        var bold = Box(mixed, 490, 20, 440, 120, "Comic Sans MS bold " + Accents, "Comic Sans MS", 20);
        bold.TextFrame2.TextRange.Font.Bold = PowerPointSession.MsoTrue;
        Box(mixed, 490, 150, 440, 120, "Comic Sans MS small: " + Pangram, "Comic Sans MS", 10);
        Box(mixed, 30, 290, 900, 110, "Consolas: if (x != 0) { return a[i] * 2; } // " + Accents, "Consolas", 28);
        Box(mixed, 30, 410, 900, 100, "Consolas 11 pt: " + Pangram + " " + Accents, "Consolas", 11);
    }
}
