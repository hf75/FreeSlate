using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// 3D text (catalog: text.threeD): text turned with a 3D-rotated shape and, patched with a:flatTx, kept flat; text
/// with a scene of its own: extruded with a bevel under a legacy perspective, turned only, beveled in a flat shape and
/// extruded in a color of its own under a perspective camera.
/// </summary>
[SupportedOSPlatform("windows")]
static class Text3DDeck
{
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        dynamic Box(float left, float top, string name, string text)
        {
            var shape = ((dynamic)slide).Shapes.AddShape(1, left, top, 200, 140);
            shape.Name = name;
            shape.TextFrame.TextRange.Text = text;
            shape.TextFrame.TextRange.Font.Size = 28;
            shape.TextFrame.TextRange.Font.Bold = PowerPointSession.MsoTrue;
            return shape;
        }
        dynamic Words(float left, float top, string name, string text, int color)
        {
            var box = ((dynamic)slide).Shapes.AddTextbox(1, left, top, 420, 110);
            box.Name = name;
            box.TextFrame.TextRange.Text = text;
            box.TextFrame.TextRange.Font.Size = 60;
            box.TextFrame.TextRange.Font.Bold = PowerPointSession.MsoTrue;
            box.TextFrame2.TextRange.Font.Fill.ForeColor.RGB = color;
            return box;
        }

        var turned = Box(30, 40, "rotated", "Rotated");
        turned.ThreeD.RotationY = 40f;
        var flat = Box(260, 40, "flat", "Flat text");
        flat.ThreeD.RotationY = 40f;

        var extruded = Words(500, 20, "extruded", "Extruded", CorpusGenerator.Rgb(192, 96, 32));
        var depth = extruded.TextFrame2.ThreeD;
        depth.Depth = 12f;
        depth.BevelTopType = 3;   // msoBevelCircle
        depth.BevelTopDepth = 4f;
        depth.BevelTopInset = 4f;
        depth.SetPresetCamera(18);   // legacy perspective bottom right

        var words = Words(500, 170, "turned", "Turned", CorpusGenerator.Rgb(32, 32, 32));
        words.TextFrame2.ThreeD.RotationY = 40f;
        words.TextFrame2.ThreeD.RotationX = 10f;

        var bevel = Box(30, 300, "bevel", "Bevel");
        bevel.TextFrame2.ThreeD.BevelTopType = 3;
        bevel.TextFrame2.ThreeD.BevelTopDepth = 3f;
        bevel.TextFrame2.ThreeD.BevelTopInset = 3f;

        var colored = Words(330, 340, "colored", "Depth", CorpusGenerator.Rgb(0, 112, 192));
        var colorDepth = colored.TextFrame2.ThreeD;
        colorDepth.Depth = 24f;
        colorDepth.ExtrusionColor.RGB = CorpusGenerator.Rgb(255, 192, 0);
        colorDepth.RotationX = 20f;
        colorDepth.RotationY = 25f;
    }

    /// <summary>"Keep text flat" has no object model property: a:flatTx is added to the text body.</summary>
    public static void Patch(string path) =>
        ShapePatch.Apply(path, new Dictionary<string, Action<XElement>>
        {
            ["flat"] = shape =>
            {
                var bodyPr = ShapePatch.BodyPr(shape);
                // flatTx follows the text warp, autofit, scene3d and sp3d of the body properties.
                bodyPr.Add(new XElement(A + "flatTx"));
            },
        });
}
