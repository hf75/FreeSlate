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
