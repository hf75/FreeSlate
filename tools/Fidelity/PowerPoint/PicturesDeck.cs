using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// The pictures deck: picture color effects (grayscale, black and white, washout, brightness and contrast,
/// transparent color, transparency), artistic effects and an OLE object shown as an icon.
/// </summary>
[SupportedOSPlatform("windows")]
static class PicturesDeck
{
    const int MsoTrue = PowerPointSession.MsoTrue, MsoFalse = PowerPointSession.MsoFalse;
    // MsoPictureColorType
    const int Grayscale = 2, BlackAndWhite = 3, Watermark = 4;
    // MsoPictureEffectType
    const int Blur = 2, ChalkSketch = 6, LineDrawing = 12, PaintBrush = 15, PencilSketch = 19, Photocopy = 20;

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var images = TestImages.Write(Path.Combine(Path.GetTempPath(), "fidelity-images"));

        var colors = Slide(presentation, "Picture color effects");
        float[] lefts = [40, 340, 640];
        float[] tops = [130, 330];
        dynamic Add(dynamic slide, int index) =>
            slide.Shapes.AddPicture(images.Jpeg, MsoFalse, MsoTrue, lefts[index % 3], tops[index / 3], 270, 180);
        Add(colors, 0);
        Add(colors, 1).PictureFormat.ColorType = Grayscale;
        Add(colors, 2).PictureFormat.ColorType = BlackAndWhite;
        Add(colors, 3).PictureFormat.ColorType = Watermark;
        var corrected = Add(colors, 4);
        corrected.PictureFormat.Brightness = 0.65f;
        corrected.PictureFormat.Contrast = 0.7f;
        var keyed = Add(colors, 5);
        keyed.PictureFormat.TransparentBackground = MsoTrue;
        keyed.PictureFormat.TransparencyColor = CorpusGenerator.Rgb(255, 255, 255);

        var artistic = Slide(presentation, "Artistic effects");
        var effects = new[] { Blur, PencilSketch, LineDrawing, ChalkSketch, PaintBrush, Photocopy };
        for (var i = 0; i < effects.Length; i++)
        {
            var picture = Add(artistic, i);
            try { picture.Fill.PictureEffects.Insert(effects[i]); } catch (Exception) { /* not available */ }
        }

        var objects = Slide(presentation, "OLE object and transparency");
        var text = Path.Combine(Path.GetTempPath(), "fidelity-images", "embedded.txt");
        File.WriteAllText(text, "An embedded file.");
        dynamic p = presentation;
        var slideObjects = objects;
        slideObjects.Shapes.AddOLEObject(80, 160, -1, -1, "", text, MsoTrue);
        var transparent = slideObjects.Shapes.AddPicture(images.Png, MsoFalse, MsoTrue, 400, 150, 400, 300);
        try { transparent.Fill.Transparency = 0.5f; } catch (Exception) { }
        _ = p;
    }

    static dynamic Slide(object deck, string title)
    {
        var slide = CorpusGenerator.AddSlide((dynamic)deck, CorpusGenerator.LayoutTitleOnly);
        slide.Shapes.Placeholders[1].TextFrame.TextRange.Text = title;
        return slide;
    }
}
