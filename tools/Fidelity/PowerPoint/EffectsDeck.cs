using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Shape effects: outer, inner and preset shadows, perspective shadows, glow with soft edges, and 3D bevels,
/// camera rotations and extrusion (the last are expected to differ until 3D rendering exists).
/// </summary>
[SupportedOSPlatform("windows")]
static class EffectsDeck
{
    const int MsoTrue = PowerPointSession.MsoTrue;
    const int Rectangle = 1, RoundedRectangle = 5, Oval = 9;

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        var shadows = Slide(presentation);
        var outer = Box(shadows, Rectangle, 40, 40, "Outer");
        outer.Shadow.Visible = MsoTrue;
        outer.Shadow.OffsetX = 6f;
        outer.Shadow.OffsetY = 6f;
        outer.Shadow.Blur = 8f;
        outer.Shadow.Transparency = 0.4f;
        var inner = Box(shadows, Rectangle, 270, 40, "Inner");
        inner.Shadow.Style = 1;   // msoShadowStyleInnerShadow
        inner.Shadow.Visible = MsoTrue;
        inner.Shadow.OffsetX = 4f;
        inner.Shadow.OffsetY = 4f;
        inner.Shadow.Blur = 6f;
        var perspective = Box(shadows, RoundedRectangle, 500, 40, "Perspective");
        perspective.Shadow.Type = 25;
        int[] presets = [6, 14, 17];
        for (var i = 0; i < presets.Length; i++)
            Box(shadows, i == 1 ? Oval : Rectangle, 40 + i * 230, 280, $"Type {presets[i]}").Shadow.Type = presets[i];
        var glow = Box(shadows, Oval, 730, 280, "Glow");
        glow.Glow.Radius = 10f;
        glow.Glow.Color.RGB = CorpusGenerator.Rgb(255, 180, 0);
        glow.SoftEdge.Radius = 4f;

        var threeD = Slide(presentation);
        var bevel = Box(threeD, Rectangle, 40, 40, "Bevel");
        bevel.ThreeD.BevelTopType = 3;   // msoBevelCircle
        bevel.ThreeD.BevelTopInset = 8f;
        bevel.ThreeD.BevelTopDepth = 8f;
        var slope = Box(threeD, RoundedRectangle, 270, 40, "Slope");
        slope.ThreeD.BevelTopType = 4;
        slope.ThreeD.BevelTopInset = 12f;
        slope.ThreeD.BevelTopDepth = 6f;
        var camera = Box(threeD, Rectangle, 500, 40, "Camera");
        camera.ThreeD.SetPresetCamera(48);   // msoCameraPerspectiveFront
        camera.ThreeD.RotationY = 30f;
        var extrusion = Box(threeD, Rectangle, 40, 280, "Extrusion");
        extrusion.ThreeD.SetPresetCamera(20);   // msoCameraIsometricTopUp
        extrusion.ThreeD.Depth = 30f;
        var metal = Box(threeD, Oval, 270, 280, "Metal");
        metal.ThreeD.BevelTopType = 7;   // msoBevelSoftRound
        metal.ThreeD.PresetMaterial = 3;   // msoMaterialMetal
        metal.ThreeD.PresetLighting = 13;   // msoLightRigThreePoint
    }

    static dynamic Slide(object deck) => CorpusGenerator.AddSlide((dynamic)deck, CorpusGenerator.LayoutBlank);

    static dynamic Box(dynamic slide, int type, float left, float top, string label)
    {
        var shape = slide.Shapes.AddShape(type, left, top, 180, 140);
        shape.Fill.ForeColor.RGB = CorpusGenerator.Rgb(68, 114, 196);
        shape.Line.Visible = PowerPointSession.MsoFalse;
        shape.TextFrame.TextRange.Text = label;
        shape.TextFrame.TextRange.Font.Size = 20;
        return shape;
    }
}
