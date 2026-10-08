using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Text bodies (catalog: text.body, text.vertical, text.columns, text.autofit): anchoring with and without
/// anchorCtr, insets, rotated text, the vertical orientations, columns and the autofit modes.
/// </summary>
[SupportedOSPlatform("windows")]
static class TextBodyDeck
{
    const int MsoTrue = PowerPointSession.MsoTrue, MsoFalse = PowerPointSession.MsoFalse;
    const int Rectangle = 1;

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        var anchors = Slide(presentation);
        int[] anchorValues = [1, 3, 4, 1, 3, 4];   // top, middle, bottom
        for (var i = 0; i < 6; i++)
        {
            var shape = Box(anchors, 30 + (i % 3) * 310, 30 + (i / 3) * 250, 280, 210, "Anchored text\rsecond line");
            shape.TextFrame.VerticalAnchor = anchorValues[i];
            if (i >= 3)
            {
                shape.TextFrame2.HorizontalAnchor = 2;   // msoAnchorCenter (anchorCtr)
                shape.TextFrame.TextRange.ParagraphFormat.Alignment = 1;
                shape.TextFrame.MarginLeft = 30f;
                shape.TextFrame.MarginTop = 20f;
            }
        }

        var vertical = Slide(presentation);
        int[] orientations = [2, 3, 4, 5, 6];   // upward, downward, vertical far east, vertical, horizontal rotated far east
        for (var i = 0; i < orientations.Length; i++)
        {
            var shape = Box(vertical, 30 + i * 185, 30, 165, 300, i < 2 ? "Rotated text in a box" : "縦書き text 123");
            shape.TextFrame.Orientation = orientations[i];
        }
        var columns = Slide(presentation);
        var two = Box(columns, 30, 30, 430, 230, Words(70));
        two.TextFrame2.Column.Number = 2;
        two.TextFrame2.Column.Spacing = 18f;
        var three = Box(columns, 500, 30, 430, 230, Words(90));
        three.TextFrame2.Column.Number = 3;
        three.TextFrame2.Column.Spacing = 10f;

        var fit = Slide(presentation);
        var shrink = Box(fit, 30, 30, 280, 160, Words(60));
        shrink.TextFrame2.AutoSize = 2;   // msoAutoSizeTextToFitShape (normAutofit)
        var grow = Box(fit, 340, 30, 280, 60, Words(25));
        grow.TextFrame2.AutoSize = 1;   // msoAutoSizeShapeToFitText (spAutoFit)
        var overflow = Box(fit, 650, 30, 280, 120, Words(60));
        overflow.TextFrame2.AutoSize = 0;
        var noWrap = Box(fit, 30, 330, 280, 80, "A line that does not wrap and runs past the edge of its box");
        noWrap.TextFrame.WordWrap = MsoFalse;
    }

    static string Words(int count) => string.Join(' ', Enumerable.Range(0, count).Select(i => (i % 7) switch
    {
        0 => "Lorem", 1 => "ipsum", 2 => "dolor", 3 => "sit", 4 => "amet", 5 => "consectetur", _ => "elit",
    }));

    static dynamic Slide(object deck) => CorpusGenerator.AddSlide((dynamic)deck, CorpusGenerator.LayoutBlank);

    static dynamic Box(dynamic slide, float left, float top, float width, float height, string text)
    {
        var shape = slide.Shapes.AddShape(Rectangle, left, top, width, height);
        shape.Fill.ForeColor.RGB = CorpusGenerator.Rgb(222, 235, 247);
        shape.Line.ForeColor.RGB = CorpusGenerator.Rgb(68, 114, 196);
        var range = shape.TextFrame.TextRange;
        range.Text = text;
        range.Font.Size = 18;
        range.Font.Color.RGB = 0;
        return shape;
    }
}
