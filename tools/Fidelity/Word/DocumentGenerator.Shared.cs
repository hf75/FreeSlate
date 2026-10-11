using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using Fidelity.PowerPoint;

namespace Fidelity.Word;

/// <summary>
/// Generated documents of the DrawingML features Word shares with PowerPoint, drawn by the shared renderer: picture
/// formats and effects, more chart types, SmartArt layouts, preset and custom shapes with groups, text effects.
/// </summary>
public static partial class DocumentGenerator
{
    /// <summary>Pictures in several formats (PNG with transparency, JPEG, GIF, BMP, TIFF, EMF, SVG) and picture effects.</summary>
    [SupportedOSPlatform("windows")]
    static void Pictures(Writer w)
    {
        var directory = Path.Combine(Path.GetTempPath(), "fidelity-word-pictures");
        var images = TestImages.Write(directory);
        var files = PictureFiles(directory);
        w.Paragraph("Picture formats", StyleHeading1);
        var anchor = w.Paragraph("Pictures of several formats, placed on the page.");
        var shapes = w.Document.Shapes;
        string[] sources = [images.Png, images.Jpeg, files.Gif, files.Bmp, files.Tiff, files.Emf, images.Svg];
        for (var i = 0; i < sources.Length; i++)
        {
            var (left, top) = (72f + i % 4 * 115, 130f + i / 4 * 105);
            var picture = shapes.AddPicture(sources[i], false, true, left, top, 105, 80, anchor.Range);
            Place(picture, left, top, WrapNone);
        }
        w.Paragraph("Picture effects", StyleHeading1);
        var effects = w.Paragraph("Recolored, adjusted, cropped and styled pictures.");
        dynamic Picture(float left, float top)
        {
            var picture = shapes.AddPicture(images.Jpeg, false, true, left, top, 120, 80, effects.Range);
            Place(picture, left, top, WrapNone);
            return picture;
        }
        Picture(72, 400).PictureFormat.ColorType = 2;            // msoPictureGrayscale
        Picture(205, 400).PictureFormat.ColorType = 4;           // msoPictureWatermark
        var adjusted = Picture(338, 400);
        adjusted.PictureFormat.Brightness = 0.7f;
        adjusted.PictureFormat.Contrast = 0.3f;
        var cropped = Picture(72, 500);
        cropped.PictureFormat.CropRight = 30f;
        cropped.PictureFormat.CropBottom = 20f;
        var soft = Picture(205, 500);
        soft.SoftEdge.Type = 3;                                  // msoSoftEdgeType3
        var framed = Picture(338, 500);
        framed.Line.Visible = -1;
        framed.Line.Weight = 6f;
        framed.Line.ForeColor.RGB = Rgb(255, 255, 255);
        framed.Shadow.Visible = -1;
        framed.Shadow.Blur = 10f;
        framed.Shadow.OffsetX = 4f;
        framed.Shadow.OffsetY = 4f;
        var artistic = Picture(72, 600);
        artistic.Fill.PictureEffects.Insert(15);                 // msoEffectPencilSketch
        var blurred = Picture(205, 600);
        blurred.Fill.PictureEffects.Insert(2);                   // msoEffectBlur
        var rotated = Picture(338, 600);
        rotated.Rotation = 15f;
        rotated.ThreeD.SetPresetCamera(48);                      // msoCameraPerspectiveFront
    }

    /// <summary>GIF, BMP, TIFF and an EMF recorded with GDI+, written next to the other test pictures.</summary>
    [SupportedOSPlatform("windows")]
    static (string Gif, string Bmp, string Tiff, string Emf) PictureFiles(string directory)
    {
        Directory.CreateDirectory(directory);
        var (gif, bmp, tiff, emf) = (Path.Combine(directory, "test.gif"), Path.Combine(directory, "test.bmp"),
            Path.Combine(directory, "test.tif"), Path.Combine(directory, "test.emf"));
        using (var bitmap = new Bitmap(300, 200))
        {
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(new Point(0, 0), new Point(300, 200), Color.SteelBlue, Color.Orange);
                g.FillEllipse(brush, 20, 20, 260, 160);
                g.DrawRectangle(Pens.Black, 5, 5, 289, 189);
            }
            bitmap.Save(gif, ImageFormat.Gif);
            bitmap.Save(bmp, ImageFormat.Bmp);
            bitmap.Save(tiff, ImageFormat.Tiff);
        }
        using (var reference = new Bitmap(1, 1))
        using (var g0 = System.Drawing.Graphics.FromImage(reference))
        {
            var hdc = g0.GetHdc();
            using (var metafile = new Metafile(emf, hdc, new RectangleF(0, 0, 300, 200), MetafileFrameUnit.Pixel, EmfType.EmfPlusDual))
            using (var g = System.Drawing.Graphics.FromImage(metafile))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillRectangle(Brushes.LightYellow, 0, 0, 300, 200);
                g.FillPie(Brushes.SeaGreen, 30, 30, 140, 140, 0, 270);
                using var pen = new Pen(Color.DarkRed, 4);
                g.DrawBezier(pen, 160, 160, 200, 20, 240, 180, 290, 40);
                using var font = new Font("Arial", 18, FontStyle.Bold);
                g.DrawString("EMF", font, Brushes.Navy, 190, 150);
            }
            g0.ReleaseHdc(hdc);
        }
        return (gif, bmp, tiff, emf);
    }

    /// <summary>
    /// More chart types, a few per document (Word leaves the data sheet of some charts open, a stock chart's among
    /// them, and then refuses further charts): area, scatter, bubble, doughnut; radar, surface, stock; bar, 3-D column,
    /// 3-D pie. Word's AddChart2 refuses or fails on the newer charts (histogram, waterfall, treemap, sunburst, box and
    /// whisker, funnel).
    /// </summary>
    static void MoreCharts(Writer w) => Charts(w, [(1, "Area"), (-4169, "Scatter"), (15, "Bubble"), (-4120, "Doughnut")]);

    static void MoreCharts2(Writer w) => Charts(w, [(-4151, "Radar"), (83, "Surface"), (88, "Stock")]);

    static void MoreCharts3(Writer w) => Charts(w, [(57, "Bar"), (54, "3-D column"), (-4100, "3-D pie")]);

    static void Charts(Writer w, (int Type, string Name)[] types)
    {
        w.Paragraph("More charts", StyleHeading1);
        foreach (var (type, name) in types)
        {
            w.Paragraph(name, StyleHeading3);
            var paragraph = w.Paragraph("");
            if (AddChart(w, type, paragraph) == null)
                paragraph.Range.Text = "(not available)";
        }
    }

    /// <summary>
    /// A chart of a type in a paragraph, its data sheet closed again (Word opens it itself, and while one is open no
    /// further chart can be added); null where Word does not insert the type.
    /// </summary>
    static dynamic? AddChart(Writer w, int type, dynamic paragraph)
    {
        dynamic chart;
        try
        {
            chart = w.Document.InlineShapes.AddChart2(-1, type, paragraph.Range).Chart;
        }
        catch (System.Runtime.InteropServices.COMException e)
        {
            Console.WriteLine($"    chart type {type}: {e.Message}");
            return null;
        }
        // Closing the workbook directly leaves the sheet open for some types; activating it first fails for others.
        try
        {
            chart.ChartData.ActivateChartDataWindow();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }
        try
        {
            chart.ChartData.Workbook.Close();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            chart.ChartData.Activate();
            chart.ChartData.Workbook.Close();
        }
        return chart;
    }

    /// <summary>SmartArt diagrams of several layout families.</summary>
    static void MoreSmartArt(Writer w)
    {
        w.Paragraph("SmartArt layouts", StyleHeading1);
        var layouts = w.Document.Application.SmartArtLayouts;
        // Layouts by their identifiers: list, process, cycle, hierarchy, relationship, matrix, pyramid.
        string[] ids =
        [
            "urn:microsoft.com/office/officeart/2005/8/layout/vList2",
            "urn:microsoft.com/office/officeart/2005/8/layout/process1",
            "urn:microsoft.com/office/officeart/2005/8/layout/cycle2",
            "urn:microsoft.com/office/officeart/2005/8/layout/orgChart1",
            "urn:microsoft.com/office/officeart/2005/8/layout/venn1",
            "urn:microsoft.com/office/officeart/2005/8/layout/matrix1",
            "urn:microsoft.com/office/officeart/2005/8/layout/pyramid1",
        ];
        foreach (var id in ids)
        {
            dynamic? layout = null;
            for (var i = 1; i <= layouts.Count; i++)
            {
                if ((string)layouts.Item(i).Id == id)
                {
                    layout = layouts.Item(i);
                    break;
                }
            }
            if (layout == null)
                continue;
            var paragraph = w.Paragraph(id[(id.LastIndexOf('/') + 1)..]);
            var diagram = w.Document.InlineShapes.AddSmartArt(layout, paragraph.Range);
            diagram.Width = 300f;
            diagram.Height = 150f;
            // Text in the nodes, of different lengths.
            string[] texts = ["Plan", "Build and measure", "Ship", "Learn from it", "Repeat"];
            var nodes = diagram.SmartArt.AllNodes;
            for (var i = 1; i <= nodes.Count; i++)
                nodes.Item(i).TextFrame2.TextRange.Text = texts[(i - 1) % texts.Length];
        }
    }

    /// <summary>Preset shapes, a freeform, a group and text effects on runs (glow, shadow, reflection, outline, fill).</summary>
    static void ShapesAndTextEffects(Writer w)
    {
        w.Paragraph("Preset shapes", StyleHeading1);
        var anchor = w.Paragraph("A sample of the preset geometries, a freeform and a group.");
        var shapes = w.Document.Shapes;
        // MsoAutoShapeType: a spread over the families of the presets.
        int[] presets = [1, 5, 7, 8, 9, 10, 12, 13, 16, 17, 21, 22, 24, 33, 34, 35, 36, 44, 52, 56, 61, 64, 65, 69, 76, 92, 94, 105, 106, 114, 116, 125, 129, 133, 138, 143, 150, 158, 166, 171];
        for (var i = 0; i < presets.Length; i++)
        {
            var (left, top) = (72f + i % 8 * 57, 130f + i / 8 * 57);
            try
            {
                var shape = shapes.AddShape(presets[i], left, top, 48, 48, anchor.Range);
                Place(shape, left, top, WrapNone);
            }
            catch (Exception e) when (e is System.Runtime.InteropServices.COMException or ArgumentException)
            {
                // A preset Word does not insert.
            }
        }
        // A freeform from line and curve segments.
        var builder = shapes.BuildFreeform(0, 72f, 440f);              // msoEditingAuto
        builder.AddNodes(0, 1, 140f, 420f);                            // msoSegmentLine, msoEditingCorner
        builder.AddNodes(1, 1, 180f, 380f, 220f, 500f, 260f, 440f);    // msoSegmentCurve
        builder.AddNodes(0, 1, 200f, 520f);
        builder.AddNodes(0, 1, 72f, 440f);
        var freeform = builder.ConvertToShape(anchor.Range);
        Place(freeform, 72, 380, WrapNone);
        // A group of a rectangle and an oval.
        // Word groups shapes only before they are placed relative to the page.
        var first = shapes.AddShape(1, 320, 400, 90, 60, anchor.Range);
        var second = shapes.AddShape(9, 380, 430, 90, 60, anchor.Range);
        second.Fill.ForeColor.RGB = Rgb(237, 125, 49);
        try
        {
            var group = shapes.Range(new object[] { first.Name, second.Name }).Group();
            Place(group, 320, 400, WrapNone);
        }
        catch (UnauthorizedAccessException)
        {
            Place(first, 320, 400, WrapNone);
            Place(second, 380, 430, WrapNone);
        }

        w.Paragraph("Text effects", StyleHeading1);
        var glow = w.Paragraph("Glowing text in a large size.");
        glow.Range.Font.Size = 24f;
        glow.Range.Font.Glow.Radius = 8f;
        glow.Range.Font.Glow.Color.RGB = Rgb(255, 192, 0);
        var shadow = w.Paragraph("Shadowed text with an outline.");
        shadow.Range.Font.Size = 24f;
        shadow.Range.Font.TextShadow.Visible = -1;
        shadow.Range.Font.TextShadow.OffsetX = 3f;
        shadow.Range.Font.TextShadow.OffsetY = 3f;
        shadow.Range.Font.TextShadow.Blur = 4f;
        shadow.Range.Font.Line.Visible = -1;
        shadow.Range.Font.Line.ForeColor.RGB = Rgb(192, 0, 0);
        shadow.Range.Font.Line.Weight = 0.75f;
        var reflection = w.Paragraph("Reflected text.");
        reflection.Range.Font.Size = 24f;
        reflection.Range.Font.Reflection.Type = 1;                     // msoReflectionType1
        var gradient = w.Paragraph("Gradient filled text.");
        gradient.Range.Font.Size = 24f;
        gradient.Range.Font.Fill.TwoColorGradient(1, 1);
        gradient.Range.Font.Fill.ForeColor.RGB = Rgb(0, 112, 192);
        gradient.Range.Font.Fill.BackColor.RGB = Rgb(0, 176, 80);
    }
}
