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

        // Perspective shadows (scaled, skewed and flipped about their alignment point) and shadows of turned shapes
        // that keep their direction on the slide or turn with the shape (patched, see Patch).
        foreach (var (prefix, rotations) in new[] { ("perspective", new[] { 0f, 0, 0, 0, 0, 0, 0, 0 }), ("turned", new[] { 30f, 90, 30, 90, 30, 0, 200, 30 }) })
        {
            var slide = Slide(presentation);
            for (var i = 0; i < 8; i++)
            {
                var shape = slide.Shapes.AddShape(Rectangle, 60 + (i % 4) * 230, 80 + (i / 4) * 260, 130, 100);
                shape.Fill.ForeColor.RGB = CorpusGenerator.Rgb(68, 114, 196);
                shape.Line.Visible = PowerPointSession.MsoFalse;
                shape.Shadow.Visible = PowerPointSession.MsoTrue;
                shape.Rotation = rotations[i];
                shape.Name = $"{prefix}-{i}";
                if (prefix == "turned" && i == 5)
                    shape.Flip(0);   // msoFlipHorizontal
            }
        }
    }

    /// <summary>The outer shadows of the last two slides: transforms PowerPoint's object model cannot set.</summary>
    public static void Patch(string path)
    {
        string[] perspective =
        [
            "dist=\"0\" sx=\"100000\" sy=\"-50000\" algn=\"b\"",
            "dist=\"12700\" dir=\"8100000\" sy=\"-23000\" kx=\"800400\" algn=\"br\"",
            "dist=\"0\" sx=\"50000\" sy=\"50000\" algn=\"tl\"",
            "dist=\"0\" kx=\"1800000\" algn=\"b\"",
            "dist=\"0\" ky=\"1200000\" algn=\"l\"",
            "dist=\"0\" sx=\"150000\" sy=\"150000\" algn=\"ctr\"",
            "blurRad=\"76200\" dist=\"12700\" dir=\"8100000\" sy=\"-23000\" kx=\"800400\" algn=\"br\"",
            "blurRad=\"76200\" dist=\"317500\" dir=\"5400000\" sx=\"90000\" sy=\"-19000\"",
        ];
        string[] turned =
        [
            "dist=\"190500\" dir=\"2700000\" rotWithShape=\"0\"",
            "dist=\"190500\" dir=\"2700000\" rotWithShape=\"0\"",
            "dist=\"190500\" dir=\"2700000\" rotWithShape=\"1\"",
            "dist=\"190500\" dir=\"2700000\" rotWithShape=\"1\"",
            "dist=\"12700\" dir=\"8100000\" sy=\"-23000\" kx=\"800400\" algn=\"br\" rotWithShape=\"0\"",
            "dist=\"190500\" dir=\"2700000\" rotWithShape=\"0\"",
            "dist=\"190500\" dir=\"2700000\" rotWithShape=\"0\"",
            "dist=\"12700\" dir=\"8100000\" sy=\"-23000\" kx=\"800400\" algn=\"br\" rotWithShape=\"1\"",
        ];
        var changes = new Dictionary<string, Action<System.Xml.Linq.XElement>>();
        foreach (var (prefix, list) in new[] { ("perspective", perspective), ("turned", turned) })
        {
            for (var i = 0; i < list.Length; i++)
            {
                var attributes = list[i];
                changes[$"{prefix}-{i}"] = shape =>
                {
                    var spPr = shape.Element(ShapePatch.P + "spPr")!;
                    spPr.Element(ShapePatch.A + "effectLst")?.Remove();
                    var effects = System.Xml.Linq.XElement.Parse(
                        $"<a:effectLst xmlns:a=\"{ShapePatch.A}\"><a:outerShdw {attributes}><a:srgbClr val=\"000000\"><a:alpha val=\"50000\"/></a:srgbClr></a:outerShdw></a:effectLst>");
                    // a:effectLst follows the fill and the line in spPr.
                    var line = spPr.Element(ShapePatch.A + "ln");
                    if (line != null) line.AddAfterSelf(effects);
                    else spPr.Add(effects);
                };
            }
        }
        ShapePatch.Apply(path, changes);
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
