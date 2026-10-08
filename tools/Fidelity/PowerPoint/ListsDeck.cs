using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Bullets and numbering (catalog: text.bullets): all 41 numbering schemes with a start value, symbol bullets in
/// Wingdings and Symbol, bullet color and size, a picture bullet and nested levels.
/// </summary>
[SupportedOSPlatform("windows")]
static class ListsDeck
{
    const int MsoTrue = PowerPointSession.MsoTrue;
    const int Unnumbered = 1, Numbered = 2;

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var images = TestImages.Write(Path.Combine(Path.GetTempPath(), "fidelity-images"));

        // Numbering schemes, three items each, in four columns over three slides.
        for (var page = 0; page < 3; page++)
        {
            var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
            for (var column = 0; column < 4; column++)
            {
                for (var row = 0; row < 4; row++)
                {
                    var style = page * 16 + column * 4 + row;
                    if (style > 40) break;
                    var box = slide.Shapes.AddTextbox(1, 20 + column * 235, 20 + row * 128, 225, 120);
                    var range = box.TextFrame.TextRange;
                    range.Text = $"Scheme {style}\rSecond\rThird";
                    range.Font.Name = "Aptos";
                    range.Font.Size = 18;
                    range.ParagraphFormat.Bullet.Type = Numbered;
                    range.ParagraphFormat.Bullet.Style = style;
                    range.ParagraphFormat.Bullet.StartValue = style == 0 ? 1 : 3;
                }
            }
        }

        var symbols = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        (string Font, int Character, int Color, float Size)[] bullets =
        [
            ("Wingdings", 0xD8, CorpusGenerator.Rgb(200, 0, 0), 1f), ("Wingdings", 0xFC, CorpusGenerator.Rgb(0, 140, 0), 1.2f),
            ("Symbol", 0xB7, CorpusGenerator.Rgb(0, 0, 0), 1f), ("Arial", 0x2013, CorpusGenerator.Rgb(0, 90, 200), 0.8f),
            ("Wingdings", 0xA7, CorpusGenerator.Rgb(120, 0, 160), 1.5f), ("Segoe UI Symbol", 0x2605, CorpusGenerator.Rgb(230, 160, 0), 1f),
        ];
        for (var i = 0; i < bullets.Length; i++)
        {
            var box = symbols.Shapes.AddTextbox(1, 20 + (i % 3) * 310, 30 + (i / 3) * 240, 300, 200);
            var range = box.TextFrame.TextRange;
            range.Text = "First item\rSecond item\rThird item";
            range.Font.Name = "Aptos";
            range.Font.Size = 22;
            var bullet = range.ParagraphFormat.Bullet;
            bullet.Type = Unnumbered;
            bullet.Font.Name = bullets[i].Font;
            bullet.Character = bullets[i].Character;
            bullet.Font.Color.RGB = bullets[i].Color;
            bullet.RelativeSize = bullets[i].Size;
        }

        var levels = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutText);
        levels.Shapes.Placeholders[1].TextFrame.TextRange.Text = "Levels and a picture bullet";
        var body = levels.Shapes.Placeholders[2].TextFrame.TextRange;
        body.Text = "Level one\rLevel two\rLevel three\rLevel four\rLevel five\rBack to one";
        for (var i = 2; i <= 5; i++)
            body.Paragraphs(i).IndentLevel = i;
        body.Paragraphs(6).ParagraphFormat.Bullet.Picture(images.Png);
    }
}
