using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Calibration of PowerPoint's glyph placement on the pixel grid: single H glyphs whose box moves in steps of
/// 1/16 pixel across one pixel, horizontally and vertically, in several fonts and sizes, and long runs of H that
/// show whether glyph positions are rounded one by one or as accumulated advances.
/// </summary>
[SupportedOSPlatform("windows")]
public static class TextSnapDeck
{
    const int MsoFalse = PowerPointSession.MsoFalse;

    /// <summary>Fonts and sizes (points) of the sweep rows.</summary>
    public static readonly (string Font, float Size)[] Rows =
        [("Arial", 24), ("Aptos", 24), ("Arial", 48), ("Times New Roman", 24), ("Aptos", 12), ("Courier New", 24)];

    /// <summary>Sweep steps per pixel and the spacing of the boxes in points.</summary>
    public const int Steps = 16;
    public const float Pitch = 56, RowHeight = 84, Left = 20, Top = 16;

    /// <summary>A 1/16 pixel step in points (slides render at 4/3 pixels per point).</summary>
    public const float Step = 0.75f / Steps;

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var horizontal = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var vertical = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        for (var row = 0; row < Rows.Length; row++)
        {
            var (font, size) = Rows[row];
            for (var k = 0; k < Steps; k++)
            {
                Box(horizontal, Left + k * Pitch + k * Step, Top + row * RowHeight, font, size, "H");
                Box(vertical, Left + k * Pitch, Top + row * RowHeight + k * Step, font, size, "H");
            }
        }

        // Runs of 16 H at four start phases.
        var runs = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        (string Font, float Size)[] runFonts = [("Arial", 24), ("Aptos", 24), ("Times New Roman", 18), ("Aptos", 12)];
        for (var row = 0; row < runFonts.Length; row++)
        {
            for (var phase = 0; phase < 4; phase++)
            {
                var (font, size) = runFonts[row];
                Box(runs, Left + (phase % 2) * 470 + phase * 0.1875f, Top + row * 128 + (phase / 2) * 60, font, size, new string('H', 16));
            }
        }

        // Advance rounding (Arial H at sizes where rounding up and to nearest differ), kerned pairs, a style change
        // inside a line and spaces between words.
        var details = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        (string Font, float Size, string Text)[] lines =
        [
            ("Arial", 22, new string('H', 16)), ("Arial", 26, new string('H', 16)), ("Arial", 30, new string('H', 16)),
            ("Aptos", 24, string.Concat(Enumerable.Repeat("Fi", 8))), ("Aptos", 24, string.Concat(Enumerable.Repeat("Tu", 8))),
            ("Arial", 24, new string('H', 16)), ("Arial", 24, "HHHH HHHH HHHH"),
        ];
        for (var i = 0; i < lines.Length; i++)
        {
            var (font, size, text) = lines[i];
            Box(details, Left + 0.225f, Top + i * 74, font, size, text);
        }
        // The sixth line: the second half bold.
        var mixed = details.Shapes[6].TextFrame.TextRange.Characters(9, 8);
        mixed.Font.Bold = PowerPointSession.MsoTrue;

        // Sizes 12 to 24 pt: below some size PowerPoint draws text hinted.
        var sizes = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        string[] sizeFonts = ["Aptos", "Arial", "Times New Roman"];
        for (var column = 0; column < sizeFonts.Length; column++)
        {
            for (var step = 0; step <= 12; step++)
                Box(sizes, Left + column * 310, Top + step * 39.5f, sizeFonts[column], 12 + step, "HHHH HHHH");
        }
    }

    static void Box(dynamic slide, float left, float top, string font, float size, string text)
    {
        var box = slide.Shapes.AddTextbox(1, left, top, 400, size * 2);
        var frame = box.TextFrame;
        frame.AutoSize = 0;
        frame.WordWrap = MsoFalse;
        frame.MarginLeft = 0;
        frame.MarginRight = 0;
        frame.MarginTop = 0;
        frame.MarginBottom = 0;
        box.Left = left;
        box.Top = top;
        box.Width = text.Length > 1 ? 460 : 40;
        var range = frame.TextRange;
        range.Text = text;
        range.Font.Name = font;
        range.Font.Size = size;
        range.Font.Color.RGB = 0;
    }
}
