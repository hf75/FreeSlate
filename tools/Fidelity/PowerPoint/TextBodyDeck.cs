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

        // Font alignment of runs of different sizes and arch transforms with and without fromWordArt (patched).
        var align = Slide(presentation);
        string[] modes = ["t", "ctr", "b", "base"];
        for (var i = 0; i < modes.Length; i++)
        {
            var mixed = Box(align, 30 + i * 230, 30, 210, 110, "Big small Big");
            mixed.Name = "fontAlgn-" + modes[i];
            mixed.TextFrame.TextRange.Characters(5, 5).Font.Size = 10;
            mixed.TextFrame.TextRange.Characters(1, 3).Font.Size = 36;
        }
        foreach (var (name, left) in new[] { ("arch-wordart", 30f), ("arch", 500f) })
        {
            var arch = Box(align, left, 260, 400, 110, "WordArt text");
            arch.Name = name;
            arch.TextFrame.TextRange.Font.Size = 28;
            arch.TextFrame.TextRange.ParagraphFormat.Alignment = 1;
        }

        // Overflowing text: ellipsis and clip drop the lines that do not fit; horizontal clipping; the space before
        // the first and after the last paragraph (all patched).
        var overflowing = Slide(presentation);
        foreach (var (name, left, top) in new[] { ("ellipsis", 30f, 30f), ("clip", 340f, 30f), ("overflow", 650f, 30f) })
        {
            var box = Box(overflowing, left, top, 280, 90, Words(40));
            box.Name = name;
        }
        var clipped = Box(overflowing, 30, 230, 200, 70, "NoWrapLongWordThatIsWider");
        clipped.Name = "horzClip";
        clipped.TextFrame.WordWrap = MsoFalse;
        foreach (var (name, left, anchor) in new[] { ("spcFirstLast-t", 340f, 1), ("spcFirstLast-b", 650f, 4), ("spcDefault-b", 650f, 4) })
        {
            var box = Box(overflowing, left, name == "spcDefault-b" ? 380f : 230f, 280, 140, "First\rSecond");
            box.Name = name;
            box.TextFrame.VerticalAnchor = anchor;
            box.TextFrame.TextRange.Paragraphs(1).ParagraphFormat.SpaceBefore = 30f;
            box.TextFrame.TextRange.Paragraphs(2).ParagraphFormat.SpaceAfter = 30f;
        }
    }

    /// <summary>The properties of the last two slides PowerPoint's object model cannot set.</summary>
    public static void Patch(string path)
    {
        var changes = new Dictionary<string, Action<System.Xml.Linq.XElement>>
        {
            ["ellipsis"] = shape => ShapePatch.BodyPr(shape).SetAttributeValue("vertOverflow", "ellipsis"),
            ["clip"] = shape => ShapePatch.BodyPr(shape).SetAttributeValue("vertOverflow", "clip"),
            ["horzClip"] = shape => ShapePatch.BodyPr(shape).SetAttributeValue("horzOverflow", "clip"),
            ["spcFirstLast-t"] = shape => ShapePatch.BodyPr(shape).SetAttributeValue("spcFirstLastPara", "1"),
            ["spcFirstLast-b"] = shape => ShapePatch.BodyPr(shape).SetAttributeValue("spcFirstLastPara", "1"),
        };
        foreach (var mode in new[] { "t", "ctr", "b", "base" })
            changes["fontAlgn-" + mode] = shape => ShapePatch.Paragraphs(shape, "fontAlgn", mode);
        foreach (var name in new[] { "arch-wordart", "arch" })
        {
            changes[name] = shape =>
            {
                var bodyPr = ShapePatch.BodyPr(shape);
                bodyPr.AddFirst(new System.Xml.Linq.XElement(ShapePatch.A + "prstTxWarp", new System.Xml.Linq.XAttribute("prst", "textArchUp"),
                    new System.Xml.Linq.XElement(ShapePatch.A + "avLst")));
                if (name == "arch-wordart")
                    bodyPr.SetAttributeValue("fromWordArt", "1");
            };
        }
        ShapePatch.Apply(path, changes);
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
