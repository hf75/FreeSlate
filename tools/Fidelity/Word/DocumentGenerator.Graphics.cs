using Fidelity.PowerPoint;

namespace Fidelity.Word;

/// <summary>
/// Generated documents of DrawingML graphics in Word, drawn by the renderer shared with PowerPoint: fills, lines,
/// effects, pictures, connectors and groups, charts and SmartArt.
/// </summary>
public static partial class DocumentGenerator
{
    // MsoShapeType constants used here.
    const int ShapeHexagon = 10, ShapeStar5 = 92, ShapeCan = 13, ShapeHeart = 21;

    /// <summary>Fills, lines and effects of shapes, pictures with effects, a connector and a group.</summary>
    static void Graphics(Writer w)
    {
        var images = TestImages.Write(Path.Combine(Path.GetTempPath(), "fidelity-images"));
        w.Paragraph("Fills, lines and effects", StyleHeading1);
        var anchor = w.Paragraph("Shapes drawn by the shared DrawingML renderer.");
        var shapes = w.Document.Shapes;
        dynamic Box(int type, float left, float top, float width = 110, float height = 70)
        {
            var shape = shapes.AddShape(type, left, top, width, height, anchor.Range);
            Place(shape, left, top, WrapNone);
            return shape;
        }

        // Fills: gradient, pattern, picture, texture.
        var gradient = Box(ShapeRectangle, 72, 140);
        gradient.Fill.TwoColorGradient(1, 1);   // msoGradientHorizontal
        gradient.Fill.ForeColor.RGB = Rgb(68, 114, 196);
        gradient.Fill.BackColor.RGB = Rgb(255, 255, 255);
        var pattern = Box(ShapeOval, 200, 140);
        pattern.Fill.Patterned(18);   // msoPatternDiagonalBrick
        pattern.Fill.ForeColor.RGB = Rgb(192, 0, 0);
        pattern.Fill.BackColor.RGB = Rgb(255, 242, 204);
        var picture = Box(ShapeHexagon, 328, 140);
        picture.Fill.UserPicture(images.Jpeg);
        var texture = Box(ShapeRoundedRectangle, 456, 140, 90);
        texture.Fill.PresetTextured(17);   // msoTextureCanvas

        // Lines: dashes, compound lines, arrows.
        var dashed = shapes.AddLine(72, 250, 200, 250, anchor.Range);
        Place(dashed, 72, 250, WrapNone);
        dashed.Line.DashStyle = 4;   // msoLineDash
        dashed.Line.Weight = 2f;
        var dotted = shapes.AddLine(72, 275, 200, 275, anchor.Range);
        Place(dotted, 72, 275, WrapNone);
        dotted.Line.DashStyle = 3;   // msoLineRoundDot
        dotted.Line.Weight = 3f;
        var arrow = shapes.AddLine(230, 250, 360, 290, anchor.Range);
        Place(arrow, 230, 250, WrapNone);
        arrow.Line.Weight = 2f;
        arrow.Line.EndArrowheadStyle = 2;   // msoArrowheadTriangle
        arrow.Line.BeginArrowheadStyle = 6;   // msoArrowheadOval
        var compound = Box(ShapeRectangle, 390, 240, 150, 60);
        compound.Line.Weight = 6f;
        compound.Line.Style = 3;   // msoLineThickThin
        compound.Fill.Visible = 0;

        // Effects: outer and inner shadow, glow with soft edges, reflection, bevel, perspective, extrusion.
        var outer = Box(ShapeRectangle, 72, 340);
        outer.Shadow.Visible = -1;
        outer.Shadow.OffsetX = 6f;
        outer.Shadow.OffsetY = 6f;
        outer.Shadow.Blur = 8f;
        outer.Shadow.Transparency = 0.4f;
        var inner = Box(ShapeRectangle, 200, 340);
        inner.Shadow.Style = 1;   // msoShadowStyleInnerShadow
        inner.Shadow.Visible = -1;
        inner.Shadow.OffsetX = 4f;
        inner.Shadow.OffsetY = 4f;
        inner.Shadow.Blur = 6f;
        var glow = Box(ShapeOval, 328, 340);
        glow.Glow.Radius = 10f;
        glow.Glow.Color.RGB = Rgb(255, 180, 0);
        glow.SoftEdge.Radius = 4f;
        var reflection = Box(ShapeStar5, 456, 330, 80, 80);
        reflection.Reflection.Type = 1;
        var bevel = Box(ShapeRectangle, 72, 470);
        bevel.ThreeD.BevelTopType = 3;   // msoBevelCircle
        bevel.ThreeD.BevelTopInset = 8f;
        bevel.ThreeD.BevelTopDepth = 8f;
        var camera = Box(ShapeRectangle, 200, 470);
        camera.ThreeD.SetPresetCamera(48);   // msoCameraPerspectiveFront
        camera.ThreeD.RotationY = 30f;
        var extrusion = Box(ShapeCan, 340, 460, 80, 90);
        extrusion.ThreeD.SetPresetCamera(20);   // msoCameraIsometricTopUp
        extrusion.ThreeD.Depth = 20f;
        var heart = Box(ShapeHeart, 456, 470, 90, 70);
        heart.Fill.ForeColor.RGB = Rgb(237, 125, 49);

        // A cropped, outlined picture with a shadow; a connector between two shapes; a group.
        var photo = shapes.AddPicture(images.Jpeg, false, true, 72, 600, 160, 110, anchor.Range);
        Place(photo, 72, 600, WrapNone);
        photo.PictureFormat.CropLeft = 40f;
        photo.PictureFormat.CropTop = 20f;
        photo.Line.Visible = -1;
        photo.Line.Weight = 3f;
        photo.Line.ForeColor.RGB = Rgb(0, 112, 192);
        photo.Shadow.Visible = -1;
        var from = Box(ShapeRectangle, 270, 610, 80, 50);
        var to = Box(ShapeOval, 430, 680, 80, 50);
        // Word connects connectors only in canvases: an elbow connector between the two shapes' sides.
        var connector = shapes.AddConnector(2, 350, 635, 470, 680);   // msoConnectorElbow
        connector.Line.Weight = 1.5f;
        connector.Line.EndArrowheadStyle = 2;
    }

    /// <summary>Charts (column, line, pie) and SmartArt diagrams inline in the text.</summary>
    static void ChartsAndDiagrams(Writer w)
    {
        // XlChartType
        const int ColumnClustered = 51, Line = 4, Pie = 5;
        w.Paragraph("Charts", StyleHeading1);
        foreach (var type in new[] { ColumnClustered, Line, Pie })
        {
            var paragraph = w.Paragraph("");
            // Word opens the chart's data sheet itself; it is closed again, or the next chart cannot open its own.
            var chart = w.Document.InlineShapes.AddChart2(-1, type, paragraph.Range).Chart;
            try
            {
                chart.ChartData.Workbook.Close();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                chart.ChartData.Activate();
                chart.ChartData.Workbook.Close();
            }
        }
        w.Paragraph("SmartArt", StyleHeading1);
        var anchor = w.Paragraph("A diagram on the page.");
        var layouts = w.Document.Application.SmartArtLayouts;
        var diagram = w.Document.Shapes.AddSmartArt(layouts.Item(1), 72, 0, 400, 200, anchor.Range);
        Place(diagram, 72, 560, WrapTopBottom);
    }
}
