using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// 3D effects (catalog: effect.scene3d, effect.shape3d): camera rotations of flat shapes with text, extrusions under
/// isometric and perspective cameras, the top bevel types, materials and light rigs.
/// </summary>
[SupportedOSPlatform("windows")]
static class ThreeDDeck
{
    const int Rectangle = 1, Oval = 9, Star5 = 92, RoundedRectangle = 5, Hexagon = 10;

    // MsoPresetCamera
    const int OrthographicFront = 19, IsometricTopUp = 20, IsometricLeftDown = 25, IsometricOffAxis1Right = 29,
        PerspectiveFront = 48, PerspectiveRelaxed = 61, PerspectiveHeroicExtremeLeftFacing = 59, ObliqueTopLeft = 40;

    public static void Build(dynamic deck)
    {
        object presentation = deck;

        // Camera rotations of flat shapes: X, Y, Z in PowerPoint's terms, orthographic and perspective.
        var cameras = Slide(presentation);
        (int Camera, float X, float Y, float Z)[] turns =
        [
            (OrthographicFront, 30, 0, 0), (OrthographicFront, 0, 40, 0), (OrthographicFront, 20, 30, 25),
            (PerspectiveFront, 40, 0, 0), (PerspectiveFront, 0, 50, 0), (PerspectiveFront, 320, 20, 340),
            (PerspectiveRelaxed, 0, 0, 0), (PerspectiveHeroicExtremeLeftFacing, 0, 0, 0),
        ];
        for (var i = 0; i < turns.Length; i++)
        {
            var shape = Box(cameras, Rectangle, 40 + (i % 4) * 230, 50 + (i / 4) * 250, 180, 130, $"Camera {i + 1}");
            shape.ThreeD.SetPresetCamera(turns[i].Camera);
            if (turns[i].Camera is OrthographicFront or PerspectiveFront)
            {
                shape.ThreeD.RotationX = turns[i].X;
                shape.ThreeD.RotationY = turns[i].Y;
                shape.ThreeD.RotationZ = turns[i].Z;
            }
        }

        // Extrusions under different cameras.
        var extrusions = Slide(presentation);
        (int Shape, int Camera, float Depth)[] solids =
        [
            (Rectangle, IsometricTopUp, 30), (Oval, IsometricLeftDown, 40), (Star5, IsometricOffAxis1Right, 25),
            (Hexagon, PerspectiveRelaxed, 30), (Rectangle, ObliqueTopLeft, 30), (RoundedRectangle, PerspectiveFront, 40),
        ];
        for (var i = 0; i < solids.Length; i++)
        {
            var shape = Box(extrusions, solids[i].Shape, 70 + (i % 3) * 300, 70 + (i / 3) * 250, 170, 120, $"Depth {solids[i].Depth}");
            shape.ThreeD.SetPresetCamera(solids[i].Camera);
            shape.ThreeD.Depth = solids[i].Depth;
            if (i == 5)
            {
                shape.ThreeD.RotationX = 30;
                shape.ThreeD.RotationY = 20;
            }
        }

        // The top bevel types (MsoBevelType 2 … 13), front view.
        var bevels = Slide(presentation);
        for (var type = 2; type <= 13; type++)
        {
            var i = type - 2;
            var shape = Box(bevels, Rectangle, 30 + (i % 6) * 155, 60 + (i / 6) * 240, 135, 160, $"Bevel {type}");
            shape.ThreeD.BevelTopType = type;
            shape.ThreeD.BevelTopInset = 12f;
            shape.ThreeD.BevelTopDepth = 8f;
        }

        // Bevels on curved shapes and materials (MsoPresetMaterial).
        var materials = Slide(presentation);
        int[] presets = [2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
        for (var i = 0; i < presets.Length; i++)
        {
            var shape = Box(materials, i % 2 == 0 ? Oval : RoundedRectangle, 30 + (i % 5) * 185, 60 + (i / 5) * 240, 160, 160, $"Material {presets[i]}");
            shape.ThreeD.BevelTopType = 3;   // circle
            shape.ThreeD.BevelTopInset = 15f;
            shape.ThreeD.BevelTopDepth = 10f;
            try { shape.ThreeD.PresetMaterial = presets[i]; } catch (Exception) { }
        }

        // Light rigs (MsoLightRigType) on a beveled, tilted shape.
        var lights = Slide(presentation);
        for (var rig = 1; rig <= 15; rig++)
        {
            var i = rig - 1;
            var shape = Box(lights, RoundedRectangle, 25 + (i % 5) * 187, 40 + (i / 5) * 165, 150, 120, $"Light {rig}");
            shape.ThreeD.BevelTopType = 3;
            shape.ThreeD.BevelTopInset = 12f;
            shape.ThreeD.BevelTopDepth = 8f;
            shape.ThreeD.SetPresetCamera(PerspectiveFront);
            shape.ThreeD.RotationX = 20;
            shape.ThreeD.RotationY = 15;
            try { shape.ThreeD.PresetLighting = rig; } catch (Exception) { }
        }
    }

    static dynamic Slide(object deck) => CorpusGenerator.AddSlide((dynamic)deck, CorpusGenerator.LayoutBlank);

    static dynamic Box(object target, int type, float left, float top, float width, float height, string text)
    {
        dynamic slide = target;
        var shape = slide.Shapes.AddShape(type, left, top, width, height);
        shape.Fill.ForeColor.RGB = CorpusGenerator.Rgb(68, 114, 196);
        shape.Line.Visible = PowerPointSession.MsoFalse;
        var range = shape.TextFrame.TextRange;
        range.Text = text;
        range.Font.Size = 16;
        range.Font.Color.RGB = CorpusGenerator.Rgb(255, 255, 255);
        return shape;
    }
}
