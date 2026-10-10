using System.IO.Compression;
using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// 3D models with embedded animations (catalog: picture.model3d, animation.pictures): one generated glTF model per
/// slide (<see cref="TestModels"/>), inserted by PowerPoint, which adds the effect that plays the model's first
/// animation when the slide begins. The last slide's model has two animations; its effect is changed afterwards to
/// play the second one (the object model cannot choose a model's animation).
/// </summary>
[SupportedOSPlatform("windows")]
static class ModelsDeck
{
    public static void Build(dynamic deck)
    {
        const int MsoFalse = PowerPointSession.MsoFalse, MsoTrue = PowerPointSession.MsoTrue;
        var models = TestModels.Write(Path.Combine(Path.GetTempPath(), "fidelity-models"));
        object presentation = deck;
        foreach (var (file, title) in new[]
                 {
                     (models.Turntable, "Turntable: node rotation, materials, texture"),
                     (models.Bounce, "Bounce: cubic spline translation, scale"),
                     (models.Arm, "Arm: skin"),
                     (models.Blob, "Blob: morph targets"),
                     (models.Scenes, "Scenes: the second animation"),
                 })
        {
            var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
            var caption = slide.Shapes.AddTextbox(1, 40, 20, 880, 40);
            caption.TextFrame.TextRange.Text = title;
            caption.TextFrame.TextRange.Font.Size = 24;
            slide.Shapes.Add3DModel(file, MsoFalse, MsoTrue, 230, 80, 500, 440);
        }
    }

    static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    static readonly XNamespace A3dAnim = "http://schemas.microsoft.com/office/drawing/2018/animation/model3d";

    /// <summary>The last slide plays the model's second animation (Shake, 1.6 s): attrName embedded2, animId 1.</summary>
    public static void Patch(string path)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        var entry = zip.GetEntry("ppt/slides/slide5.xml")!;
        XDocument slide;
        using (var stream = entry.Open())
            slide = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        foreach (var name in slide.Descendants(P + "attrName").Where(n => n.Value == "embedded1"))
        {
            name.Value = "embedded2";
            name.Ancestors(P + "cBhvr").First().Element(P + "cTn")!.SetAttributeValue("dur", "1600");
        }
        foreach (var embedded in slide.Descendants(A3dAnim + "embedAnim"))
        {
            embedded.SetAttributeValue("animId", "1");
            embedded.Element(A3dAnim + "animPr")?.SetAttributeValue("length", "1600");
        }
        foreach (var poster in slide.Descendants(A3dAnim + "posterFrame"))
            poster.SetAttributeValue("animId", "1");
        entry.Delete();
        using var output = zip.CreateEntry("ppt/slides/slide5.xml").Open();
        slide.Save(output, SaveOptions.DisableFormatting);
    }
}
