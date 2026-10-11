using Fidelity.PowerPoint;

namespace Fidelity.Word;

/// <summary>Generated documents of drawings: pictures inline and floating, shapes and text boxes.</summary>
public static partial class DocumentGenerator
{
    // WdWrapType, WdRelativeHorizontalPosition, WdRelativeVerticalPosition, MsoAutoShapeType
    const int WrapSquare = 0, WrapNone = 3, WrapTopBottom = 4, WrapBehind = 5;
    const int RelativeToPage = 1;
    const int ShapeRectangle = 1, ShapeOval = 9, ShapeRightArrow = 33, ShapeRoundedRectangle = 5;

    /// <summary>Inline pictures in the text, a floating picture, shapes and a text box placed on the page.</summary>
    static void Drawings(Writer w)
    {
        var images = TestImages.Write(Path.Combine(Path.GetTempPath(), "fidelity-images"));
        w.Paragraph("Inline pictures", StyleHeading1);
        var inline = w.Paragraph("Text before the picture ");
        var picture = w.Document.InlineShapes.AddPicture(images.Png, false, true, End(inline));
        picture.LockAspectRatio = 0;
        picture.Width = 120;
        picture.Height = 90;
        End(w.Document.Paragraphs.Last).InsertAfter(" and text after it on the same line.");
        var alone = w.Paragraph("");
        var jpeg = w.Document.InlineShapes.AddPicture(images.Jpeg, false, true, alone.Range);
        jpeg.LockAspectRatio = 0;
        jpeg.Width = 240;
        jpeg.Height = 160;
        var svgLine = w.Paragraph("A small vector picture ");
        var svg = w.Document.InlineShapes.AddPicture(images.Svg, false, true, End(svgLine));
        svg.LockAspectRatio = 0;
        svg.Width = 36;
        svg.Height = 36;
        End(w.Document.Paragraphs.Last).InsertAfter(" in a line of text.");

        w.Paragraph("Shapes on the page", StyleHeading1);
        var anchor = w.Paragraph(Prose.Decisions);
        var rectangle = w.Document.Shapes.AddShape(ShapeRectangle, 72, 560, 120, 70, anchor.Range);
        Place(rectangle, 72, 560, WrapNone);
        rectangle.Fill.ForeColor.RGB = Rgb(68, 114, 196);
        var oval = w.Document.Shapes.AddShape(ShapeOval, 220, 560, 100, 70, anchor.Range);
        Place(oval, 220, 560, WrapNone);
        var arrow = w.Document.Shapes.AddShape(ShapeRightArrow, 350, 570, 110, 50, anchor.Range);
        Place(arrow, 350, 570, WrapNone);
        arrow.Fill.ForeColor.RGB = Rgb(237, 125, 49);
        var rounded = w.Document.Shapes.AddShape(ShapeRoundedRectangle, 72, 660, 160, 60, anchor.Range);
        Place(rounded, 72, 660, WrapBehind);
        rounded.Fill.ForeColor.RGB = Rgb(255, 230, 153);
        rounded.Line.Visible = 0;
        var floating = w.Document.Shapes.AddPicture(images.Png, false, true, 300, 650, 160, 120, anchor.Range);
        Place(floating, 300, 650, WrapNone);
    }

    /// <summary>Text boxes and shapes with text: a text box with two paragraphs, a shape with centered text, a text box anchored at the bottom.</summary>
    static void TextBoxes(Writer w)
    {
        // MsoTextOrientation, MsoVerticalAnchor
        const int Horizontal = 1, AnchorBottom = 4;
        w.Paragraph("Text boxes", StyleHeading1);
        var anchor = w.Paragraph(Prose.Decisions);
        var box = w.Document.Shapes.AddTextbox(Horizontal, 72, 200, 200, 120, anchor.Range);
        Place(box, 72, 200, WrapNone);
        box.TextFrame.TextRange.Text = "A text box with a first paragraph of some length that wraps.\rAnd a second paragraph.";
        var shape = w.Document.Shapes.AddShape(ShapeRoundedRectangle, 320, 200, 180, 100, anchor.Range);
        Place(shape, 320, 200, WrapNone);
        shape.TextFrame.TextRange.Text = "Text in a shape";
        var bottom = w.Document.Shapes.AddTextbox(Horizontal, 72, 360, 200, 100, anchor.Range);
        Place(bottom, 72, 360, WrapNone);
        bottom.TextFrame.TextRange.Text = "Anchored at the bottom";
        bottom.TextFrame.VerticalAnchor = AnchorBottom;
    }

    /// <summary>Footnotes in the text over several pages, a long one, and an endnote at the end.</summary>
    static void Notes(Writer w)
    {
        var missing = System.Reflection.Missing.Value;
        w.Paragraph("Footnotes and endnotes", StyleHeading1);
        for (var i = 0; i < 7; i++)
        {
            var paragraph = w.Paragraph(Prose.Decisions);
            int start = paragraph.Range.Start;
            var at = w.Document.Range(start + 29, start + 29);
            w.Document.Footnotes.Add(at, missing, i == 2 ? "A long footnote. " + Prose.Constraints : $"Footnote number {i + 1}.");
            w.Paragraph(Prose.Constraints);
        }
        var last = w.Paragraph("An endnote follows here.");
        w.Document.Endnotes.Add(End(last), missing, "The text of the endnote.");
    }

    /// <summary>Text flowing around floating shapes: square at the left and right, both sides, top and bottom, tight.</summary>
    static void Wrapping(Writer w)
    {
        // WdWrapSideType
        const int BothSides = 0, Left = 1, Right = 2, Largest = 3, WrapTight = 1;
        w.Paragraph("Text around floating shapes", StyleHeading1);
        var first = w.Paragraph(Prose.Decisions + " " + Prose.Constraints);
        var square = w.Document.Shapes.AddShape(ShapeRectangle, 72, 120, 150, 100, first.Range);
        Place(square, 72, 120, WrapSquare);
        square.WrapFormat.Side = BothSides;
        w.Paragraph(Prose.Measuring);
        var second = w.Paragraph(Prose.Decisions + " " + Prose.Rules);
        var right = w.Document.Shapes.AddShape(ShapeOval, 380, 300, 140, 90, second.Range);
        Place(right, 380, 300, WrapSquare);
        right.WrapFormat.Side = BothSides;
        w.Paragraph(Prose.Constraints);
        var third = w.Paragraph(Prose.Decisions + " " + Prose.Measuring);
        var center = w.Document.Shapes.AddShape(ShapeRectangle, 240, 470, 110, 80, third.Range);
        Place(center, 240, 470, WrapSquare);
        center.WrapFormat.Side = BothSides;
        var largest = w.Paragraph(Prose.Rules + " " + Prose.Constraints);
        var both = w.Document.Shapes.AddShape(ShapeRoundedRectangle, 200, 600, 200, 60, largest.Range);
        Place(both, 200, 600, WrapTopBottom);
        w.Paragraph(Prose.Decisions);
        w.Paragraph(Prose.Measuring);
    }

    /// <summary>
    /// Tight and through wrapping along Word's wrap polygon, text on one side of a shape (left, right, the larger one)
    /// and wrap distances.
    /// </summary>
    static void WrapTypes(Writer w)
    {
        // WdWrapType, WdWrapSideType, MsoAutoShapeType
        const int WrapTight = 1, WrapThrough = 2;
        const int BothSides = 0, Left = 1, Right = 2, Largest = 3;
        const int ShapeTriangle = 7;
        dynamic Shape(int type, float left, float top, float width, float height, dynamic anchor, int wrap, int side)
        {
            var shape = w.Document.Shapes.AddShape(type, left, top, width, height, anchor.Range);
            Place(shape, left, top, wrap);
            shape.WrapFormat.Side = side;
            return shape;
        }
        w.Paragraph("Tight, through and one-sided wrapping", StyleHeading1);
        var oval = w.Paragraph(Prose.Decisions + " " + Prose.Constraints);
        Shape(ShapeOval, 220, 120, 170, 120, oval, WrapTight, BothSides);
        w.Paragraph(Prose.Measuring);
        var triangle = w.Paragraph(Prose.Rules + " " + Prose.Decisions);
        Shape(ShapeTriangle, 72, 330, 160, 140, triangle, WrapTight, Right);
        var through = w.Paragraph(Prose.Constraints + " " + Prose.Measuring);
        Shape(ShapeOval, 340, 520, 150, 110, through, WrapThrough, BothSides);
        var left = w.Paragraph(Prose.Decisions + " " + Prose.Rules);
        Shape(ShapeRectangle, 330, 680, 140, 70, left, 0, Left);

        w.Section();
        var right = w.Paragraph(Prose.Decisions + " " + Prose.Constraints);
        Shape(ShapeRectangle, 150, 100, 140, 80, right, 0, Right);
        var largest = w.Paragraph(Prose.Measuring + " " + Prose.Rules);
        Shape(ShapeRectangle, 330, 270, 120, 80, largest, 0, Largest);
        var distances = w.Paragraph(Prose.Constraints + " " + Prose.Decisions);
        var spaced = Shape(ShapeRectangle, 230, 450, 130, 80, distances, 0, BothSides);
        spaced.WrapFormat.DistanceLeft = 24f;
        spaced.WrapFormat.DistanceRight = 30f;
        spaced.WrapFormat.DistanceTop = 12f;
        spaced.WrapFormat.DistanceBottom = 18f;
        var pictured = w.Paragraph(Prose.Rules + " " + Prose.Measuring);
        var images = TestImages.Write(Path.Combine(Path.GetTempPath(), "fidelity-images"));
        var picture = w.Document.Shapes.AddPicture(images.Jpeg, false, true, 72, 620, 120, 90, pictured.Range);
        Place(picture, 72, 620, WrapTight);
        w.Paragraph(Prose.Decisions);
    }

    /// <summary>A collapsed range at the end of a paragraph, before its mark.</summary>
    static dynamic End(dynamic paragraph)
    {
        var end = paragraph.Range;
        end.MoveEnd(1, -1);   // wdCharacter
        end.Collapse(0);      // wdCollapseEnd
        return end;
    }

    /// <summary>Puts a floating shape at a place on the page with a wrap type.</summary>
    static void Place(dynamic shape, float left, float top, int wrap)
    {
        shape.WrapFormat.Type = wrap;
        shape.RelativeHorizontalPosition = RelativeToPage;
        shape.RelativeVerticalPosition = RelativeToPage;
        shape.Left = left;
        shape.Top = top;
    }
}
