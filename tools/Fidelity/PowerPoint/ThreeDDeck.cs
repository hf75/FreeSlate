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

        // Bevels on pictures (their texture stays on the bevel) and gradients.
        var textured = Slide(presentation);
        var images = TestImages.Write(Path.Combine(Path.GetTempPath(), "fidelity-images"));
        int[] pictureBevels = [3, 7, 4];
        for (var i = 0; i < pictureBevels.Length; i++)
        {
            var picture = ((dynamic)textured).Shapes.AddPicture(i == 1 ? images.Png : images.Jpeg, PowerPointSession.MsoFalse, PowerPointSession.MsoTrue, 40 + i * 300, 40, 260, 180);
            picture.ThreeD.BevelTopType = pictureBevels[i];
            picture.ThreeD.BevelTopInset = 14f;
            picture.ThreeD.BevelTopDepth = 10f;
        }
        for (var i = 0; i < 3; i++)
        {
            var shape = Box(textured, i == 1 ? Oval : Rectangle, 40 + i * 300, 290, 260, 180, $"Gradient {i + 1}");
            shape.Fill.TwoColorGradient(i + 1, 1);   // horizontal, vertical, diagonal up
            shape.Fill.ForeColor.RGB = CorpusGenerator.Rgb(68, 114, 196);
            shape.Fill.BackColor.RGB = CorpusGenerator.Rgb(255, 192, 0);
            shape.ThreeD.BevelTopType = 3;
            shape.ThreeD.BevelTopInset = 16f;
            shape.ThreeD.BevelTopDepth = 10f;
        }

        // Light angles: the rig turned about the view axis.
        var angles = Slide(presentation);
        float[] lightAngles = [0, 45, 90, 135, 180, 225, 270, 315];
        for (var i = 0; i < lightAngles.Length; i++)
        {
            var shape = Box(angles, Oval, 40 + (i % 4) * 230, 50 + (i / 4) * 240, 190, 190, $"Angle {lightAngles[i]}");
            shape.ThreeD.BevelTopType = 6;   // angle
            shape.ThreeD.BevelTopInset = 40f;
            shape.ThreeD.BevelTopDepth = 40f;
            shape.ThreeD.LightAngle = lightAngles[i];
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

        // Bottom bevels and contours: the contour is a tube around the body's rims and edges.
        var contours = Slide(presentation);
        var yellow = CorpusGenerator.Rgb(255, 192, 0);
        dynamic Contoured(int type, int column, int row, float width)
        {
            var shape = Box(contours, type, 40 + column * 235, 60 + row * 250, 160, 110, "");
            shape.ThreeD.ContourWidth = width;
            shape.ThreeD.ContourColor.RGB = yellow;
            return shape;
        }
        var bottom = Box(contours, Rectangle, 40, 60, 160, 110, "");
        bottom.ThreeD.SetPresetCamera(IsometricLeftDown);
        bottom.ThreeD.Depth = 20;
        bottom.ThreeD.BevelBottomType = 3;
        bottom.ThreeD.BevelBottomInset = 12f;
        bottom.ThreeD.BevelBottomDepth = 12f;
        var both = Box(contours, Rectangle, 275, 60, 160, 110, "");
        both.ThreeD.SetPresetCamera(IsometricLeftDown);
        both.ThreeD.Depth = 20;
        both.ThreeD.BevelTopType = 3;
        both.ThreeD.BevelTopInset = 12f;
        both.ThreeD.BevelTopDepth = 12f;
        both.ThreeD.BevelBottomType = 6;
        both.ThreeD.BevelBottomInset = 12f;
        both.ThreeD.BevelBottomDepth = 12f;
        Contoured(Rectangle, 2, 0, 8);
        var beveledOval = Contoured(Oval, 3, 0, 6);
        beveledOval.ThreeD.BevelTopType = 3;
        beveledOval.ThreeD.BevelTopInset = 15f;
        beveledOval.ThreeD.BevelTopDepth = 10f;
        var box = Contoured(Rectangle, 0, 1, 6);
        box.ThreeD.SetPresetCamera(IsometricLeftDown);
        box.ThreeD.Depth = 30;
        var puck = Contoured(Oval, 1, 1, 6);
        puck.ThreeD.SetPresetCamera(IsometricLeftDown);
        puck.ThreeD.Depth = 40;
        var angled = Contoured(Rectangle, 2, 1, 6);
        angled.ThreeD.BevelTopType = 6;
        angled.ThreeD.BevelTopInset = 20f;
        angled.ThreeD.BevelTopDepth = 10f;
        var star = Contoured(Star5, 3, 1, 5);
        star.ThreeD.SetPresetCamera(OrthographicFront);
        star.ThreeD.RotationX = 40;

        // Lines on 3D bodies (the line colors the sides, the bevels' outer band and the back) and offsets along z.
        var lined = Slide(presentation);
        dynamic Lined(int type, int column, int row)
        {
            var shape = Box(lined, type, 40 + column * 235, 60 + row * 250, 160, 110, "");
            shape.Line.Visible = PowerPointSession.MsoTrue;
            shape.Line.Weight = 8;
            shape.Line.ForeColor.RGB = CorpusGenerator.Rgb(220, 0, 0);
            return shape;
        }
        var walls = Lined(Rectangle, 0, 0);
        walls.ThreeD.SetPresetCamera(IsometricLeftDown);
        walls.ThreeD.Depth = 40;
        var colored = Lined(Rectangle, 1, 0);
        colored.ThreeD.SetPresetCamera(IsometricLeftDown);
        colored.ThreeD.Depth = 40;
        colored.ThreeD.ExtrusionColor.RGB = CorpusGenerator.Rgb(0, 176, 0);
        var beveledLine = Lined(Rectangle, 2, 0);
        beveledLine.ThreeD.BevelTopType = 3;
        beveledLine.ThreeD.BevelTopInset = 15f;
        beveledLine.ThreeD.BevelTopDepth = 10f;
        var deep = Lined(Rectangle, 3, 0);
        deep.ThreeD.SetPresetCamera(IsometricLeftDown);
        deep.ThreeD.Depth = 30;
        deep.ThreeD.BevelTopType = 3;
        deep.ThreeD.BevelTopInset = 15f;
        deep.ThreeD.BevelTopDepth = 10f;
        var nearer = Box(lined, Rectangle, 40, 310, 160, 110, "z 120");
        nearer.ThreeD.SetPresetCamera(PerspectiveFront);
        nearer.ThreeD.Z = 120;
        var farther = Box(lined, Rectangle, 275, 310, 160, 110, "z -150");
        farther.ThreeD.SetPresetCamera(PerspectiveFront);
        farther.ThreeD.Z = -150;
        var shifted = Box(lined, Rectangle, 510, 260, 160, 110, "z 100");
        shifted.ThreeD.SetPresetCamera(OrthographicFront);
        shifted.ThreeD.RotationY = 40;
        shifted.ThreeD.Z = 100;
        var behind = Lined(Rectangle, 3, 1);
        behind.ThreeD.SetPresetCamera(OrthographicFront);
        behind.ThreeD.RotationX = 150;
        behind.ThreeD.RotationY = 20;
        behind.ThreeD.Depth = 30;
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
