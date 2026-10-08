using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// The metafiles deck: PowerPoint shapes exported as EMF (EMF+ dual) and WMF through PowerPoint's own picture
/// export, and metafiles recorded with GDI+ (dual and GDI-only) with gradients, hatches, curves, text and bitmaps.
/// </summary>
[SupportedOSPlatform("windows")]
static class MetafilesDeck
{
    const int MsoTrue = PowerPointSession.MsoTrue, MsoFalse = PowerPointSession.MsoFalse;
    const int PpShapeFormatWmf = 4, PpShapeFormatEmf = 5;
    const int RoundedRectangle = 5, Star5 = 92, Oval = 9, Cloud = 179, Horizontal = 1;

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var directory = Path.Combine(Path.GetTempPath(), "fidelity-metafiles");
        Directory.CreateDirectory(directory);

        var emfSlide = Slide(presentation, "EMF exported by PowerPoint");
        var wmfSlide = Slide(presentation, "WMF exported by PowerPoint");
        var scratch = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);

        var shapes = new List<dynamic>();
        var gradient = scratch.Shapes.AddShape(RoundedRectangle, 0, 0, 260, 150);
        gradient.Fill.TwoColorGradient(1, 1);
        gradient.Fill.ForeColor.RGB = CorpusGenerator.Rgb(0, 112, 192);
        gradient.Fill.BackColor.RGB = CorpusGenerator.Rgb(160, 224, 255);
        gradient.Line.Weight = 3;
        gradient.TextFrame.TextRange.Text = "Gradient and text";
        shapes.Add(gradient);

        var star = scratch.Shapes.AddShape(Star5, 0, 0, 150, 150);
        star.Fill.Patterned(5);
        star.Fill.ForeColor.RGB = CorpusGenerator.Rgb(0, 128, 0);
        star.Fill.BackColor.RGB = CorpusGenerator.Rgb(255, 255, 255);
        shapes.Add(star);

        var ellipse = scratch.Shapes.AddShape(Oval, 0, 0, 220, 140);
        ellipse.Fill.Visible = MsoFalse;
        ellipse.Line.Weight = 4;
        ellipse.Line.DashStyle = 4;   // msoLineDash
        ellipse.Line.ForeColor.RGB = CorpusGenerator.Rgb(192, 0, 0);
        ellipse.TextFrame.TextRange.Text = "Dashed";
        ellipse.TextFrame.TextRange.Font.Color.RGB = CorpusGenerator.Rgb(192, 0, 0);
        shapes.Add(ellipse);

        var line = scratch.Shapes.AddLine(0, 0, 220, 90);
        line.Line.Weight = 3;
        line.Line.EndArrowheadStyle = 2;   // msoArrowheadTriangle
        line.Line.BeginArrowheadStyle = 6; // msoArrowheadOval
        shapes.Add(line);

        var cloud = scratch.Shapes.AddShape(Cloud, 0, 0, 200, 130);
        cloud.Shadow.Visible = MsoTrue;
        shapes.Add(cloud);

        var box = scratch.Shapes.AddTextbox(Horizontal, 0, 0, 260, 80);
        box.TextFrame.TextRange.Text = "Serif text, bold and italic";
        box.TextFrame.TextRange.Font.Name = "Georgia";
        box.TextFrame.TextRange.Font.Size = 24;
        box.TextFrame.TextRange.Characters(1, 5).Font.Bold = MsoTrue;
        box.TextFrame.TextRange.Characters(7, 4).Font.Italic = MsoTrue;
        shapes.Add(box);

        float[] lefts = [40, 340, 640];
        float[] tops = [130, 330];
        for (var i = 0; i < shapes.Count; i++)
        {
            var emf = Path.Combine(directory, $"shape{i}.emf");
            var wmf = Path.Combine(directory, $"shape{i}.wmf");
            shapes[i].Export(emf, PpShapeFormatEmf);
            shapes[i].Export(wmf, PpShapeFormatWmf);
            var (left, top) = (lefts[i % 3], tops[i / 3]);
            emfSlide.Shapes.AddPicture(emf, MsoFalse, MsoTrue, left, top);
            wmfSlide.Shapes.AddPicture(wmf, MsoFalse, MsoTrue, left, top);
        }
        scratch.Delete();

        var gdiplus = Slide(presentation, "Recorded with GDI+: dual and GDI only");
        var images = TestImages.Write(Path.Combine(Path.GetTempPath(), "fidelity-images"));
        var dual = Path.Combine(directory, "gdiplus-dual.emf");
        var gdiOnly = Path.Combine(directory, "gdiplus-gdi.emf");
        Record(dual, EmfType.EmfPlusDual, images.Png);
        Record(gdiOnly, EmfType.EmfOnly, images.Png);
        gdiplus.Shapes.AddPicture(dual, MsoFalse, MsoTrue, 40, 130, 420, 315);
        gdiplus.Shapes.AddPicture(gdiOnly, MsoFalse, MsoTrue, 500, 130, 420, 315);
    }

    static dynamic Slide(object deck, string title)
    {
        var slide = CorpusGenerator.AddSlide((dynamic)deck, CorpusGenerator.LayoutTitleOnly);
        slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = title;
        return slide;
    }

    /// <summary>A 400 × 300 metafile with the GDI+ features Office content uses.</summary>
    static void Record(string file, EmfType type, string pngPath)
    {
        using var reference = new System.Drawing.Bitmap(1, 1);
        using var referenceGraphics = Graphics.FromImage(reference);
        var hdc = referenceGraphics.GetHdc();
        using (var metafile = new Metafile(file, hdc, new RectangleF(0, 0, 400, 300), MetafileFrameUnit.Pixel, type))
        {
            referenceGraphics.ReleaseHdc(hdc);
            using var g = Graphics.FromImage(metafile);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            using (var brush = new LinearGradientBrush(new Rectangle(10, 10, 180, 100), Color.FromArgb(0, 70, 140), Color.FromArgb(255, 200, 0), 30f))
                g.FillRectangle(brush, 10, 10, 180, 100);
            using (var hatch = new HatchBrush(HatchStyle.DiagonalCross, Color.DarkGreen, Color.Honeydew))
                g.FillEllipse(hatch, 210, 10, 180, 100);
            using (var pen = new Pen(Color.Firebrick, 5) { DashStyle = DashStyle.Dash, StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawBezier(pen, 20, 160, 80, 110, 140, 210, 190, 140);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(220, 120, 160, 90);
                using var radial = new PathGradientBrush(path) { CenterColor = Color.White, SurroundColors = [Color.MediumPurple] };
                g.FillPath(radial, path);
            }
            using (var font = new Font("Arial", 20, FontStyle.Bold, GraphicsUnit.Pixel))
                g.DrawString("GDI+ text", font, Brushes.Black, 20, 225);
            using (var font = new Font("Times New Roman", 18, FontStyle.Italic, GraphicsUnit.Pixel))
                g.DrawString("Italic serif", font, Brushes.DarkBlue, 20, 260);
            using (var image = Image.FromFile(pngPath))
                g.DrawImage(image, new Rectangle(230, 220, 160, 75));
            g.DrawCurve(Pens.Black, [new PointF(200, 290), new PointF(215, 230), new PointF(230, 280)]);
        }
    }
}
