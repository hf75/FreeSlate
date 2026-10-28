using System.Xml.Linq;

namespace FreeSlate.Drawing;

/// <summary>
/// The camera of a 3D scene (a:scene3d/a:camera, catalog: effect.scene3d) with PowerPoint's X, Y and Z rotation in
/// degrees (X = −lon, Y = lat, Z = −rev of a:rot): the shape is turned about its center by X (about the vertical
/// axis), then Y (about the horizontal axis), then Z (in the picture plane), and seen from the front, in perspective
/// for the perspective presets. Fitted to corner positions PowerPoint renders for 30 cameras (errors below 0.4 pt):
/// the eye stands 907.5 pt / 2 / tan(fov / 2) in front of the shape's center, and points in the shape's plane keep
/// their size. The presets' angles and fields of view come from PowerPoint (ThreeD.SetPresetCamera, then
/// RotationX/Y/Z and FieldOfView).
/// </summary>
public sealed record Camera(double X, double Y, double Z, double Fov, bool Perspective, bool Oblique)
{
    /// <summary>
    /// For oblique cameras: where a point one pixel behind the front plane appears, relative to the point (measured on
    /// extruded squares: legacy oblique presets shift by half the depth, oblique ones by 0.3, both along their
    /// direction; legacy perspective presets about 0.133 per axis).
    /// </summary>
    public (double X, double Y) Shear { get; init; }

    /// <summary>
    /// How far the shape stands in front of its plane (a:sp3d/@z, pixels): it moves along its own z axis before the
    /// camera turns it, so perspective cameras enlarge it about its center and turned ones shift it (measured).
    /// </summary>
    public double Offset { get; init; }

    /// <summary>
    /// The center of the scene relative to the shape's center (pixels, in the plane): the scene of a SmartArt diagram
    /// turns all its shapes about the diagram's center, and perspective cameras look at that center (catalog:
    /// smartart.styles).
    /// </summary>
    public (double X, double Y) Pivot { get; init; }

    /// <summary>The camera's zoom (a:camera/@zoom): the picture scaled about the scene's center.</summary>
    public double Zoom { get; init; } = 1;

    const double ViewSize = 907.5;   // points

    static readonly Dictionary<string, (double X, double Y, double Z, double Fov)> Presets = new()
    {
        ["orthographicFront"] = (0, 0, 0, 45),
        ["isometricTopUp"] = (45.2809, -35.39627, -60.16242, 45),
        ["isometricTopDown"] = (-45.28088, -35.39627, 60.16245, 45),
        ["isometricBottomUp"] = (-45.28088, 35.39627, -60.16242, 45),
        ["isometricBottomDown"] = (45.2809, 35.39627, 60.16245, 45),
        ["isometricLeftUp"] = (-45, -35.00002, 0, 45),
        ["isometricLeftDown"] = (-45, 35, 0, 45),
        ["isometricRightUp"] = (45.00003, 35, 0, 45),
        ["isometricRightDown"] = (45.00003, -35.00002, 0, 45),
        ["isometricOffAxis1Left"] = (-64, 18, 0, 45),
        ["isometricOffAxis1Right"] = (26.00003, 18, 0, 45),
        ["isometricOffAxis1Top"] = (53.45425, -58.73812, -57.6425, 45),
        ["isometricOffAxis2Left"] = (-26, 18, 0, 45),
        ["isometricOffAxis2Right"] = (64.00003, 18, 0, 45),
        ["isometricOffAxis2Top"] = (-53.45424, -58.73812, 57.6425, 45),
        ["isometricOffAxis3Left"] = (-64, -18.00002, 0, 45),
        ["isometricOffAxis3Right"] = (26.00003, -18.00002, 0, 45),
        ["isometricOffAxis3Bottom"] = (53.45425, 58.73808, 57.64255, 45),
        ["isometricOffAxis4Left"] = (-26, -18.00002, 0, 45),
        ["isometricOffAxis4Right"] = (64.00003, -18.00002, 0, 45),
        ["isometricOffAxis4Bottom"] = (-53.45424, 58.73808, -57.64252, 45),
        ["perspectiveFront"] = (0, 0, 0, 45),
        ["perspectiveLeft"] = (-20, 0, 0, 45),
        ["perspectiveRight"] = (20.00002, 0, 0, 45),
        ["perspectiveAbove"] = (0, -20.00002, 0, 45),
        ["perspectiveBelow"] = (0, 20, 0, 45),
        ["perspectiveAboveLeftFacing"] = (-14.33758, 39.30647, 18.87617, 45),
        ["perspectiveAboveRightFacing"] = (14.33762, 39.30647, -18.87615, 45),
        ["perspectiveContrastingLeftFacing"] = (-43.93887, 10.39642, 3.553517, 45),
        ["perspectiveContrastingRightFacing"] = (43.9389, 10.39642, -3.553517, 45),
        ["perspectiveHeroicLeftFacing"] = (-14.29302, -11.01627, -2.626817, 45),
        ["perspectiveHeroicRightFacing"] = (14.29307, -11.01627, 2.626767, 45),
        ["perspectiveHeroicExtremeLeftFacing"] = (-34.46068, 8.12245, 2.908583, 80),
        ["perspectiveHeroicExtremeRightFacing"] = (34.46073, 8.12245, -2.9086, 80),
        ["perspectiveRelaxed"] = (0, -50.43993, 0, 45),
        ["perspectiveRelaxedModerately"] = (0, -35.15602, 0, 45),
    };

    /// <summary>The camera of a:scene3d, or null without one.</summary>
    public static Camera? Read(XElement? scene3d)
    {
        var camera = scene3d?.Element(Ns.A + "camera");
        if (camera == null)
            return null;
        var preset = camera.Str("prst") ?? "orthographicFront";
        var perspective = preset.StartsWith("perspective", StringComparison.Ordinal) || preset.StartsWith("legacyPerspective", StringComparison.Ordinal);
        var oblique = preset.StartsWith("oblique", StringComparison.Ordinal) || preset.StartsWith("legacyOblique", StringComparison.Ordinal);
        var (x, y, z, fov) = Presets.TryGetValue(preset, out var p) ? p : (0, 0, 0, preset.StartsWith("legacyPerspective", StringComparison.Ordinal) ? 65 : 45);
        var legacy = preset.StartsWith("legacy", StringComparison.Ordinal);
        // An explicit rotation replaces the preset's (60000ths of a degree; lon and rev turn the other way). Oblique
        // cameras turn the scene before they shift it (measured: LibreOffice's Scene3d_legacyObliqueBottomRight and
        // Scene3d_lightRig_legacyCamera; local/oblique-rot, obliqueTopRight turned by lat, lon and rev alone and together).
        if (camera.Element(Ns.A + "rot") is { } rot)
        {
            x = -(rot.Long("lon") ?? 0) / 60000.0;
            y = (rot.Long("lat") ?? 0) / 60000.0;
            z = -(rot.Long("rev") ?? 0) / 60000.0;
        }
        if (camera.Long("fov") is { } f)
            fov = f / 60000.0;
        var zoom = (camera.Long("zoom") ?? 100000) / 100000.0;
        var shear = (0.0, 0.0);
        if (oblique || preset.StartsWith("legacyPerspective", StringComparison.Ordinal))
        {
            var dx = preset.Contains("Left", StringComparison.Ordinal) ? -1 : preset.Contains("Right", StringComparison.Ordinal) ? 1 : 0;
            var dy = preset.Contains("Top", StringComparison.Ordinal) ? -1 : preset.Contains("Bottom", StringComparison.Ordinal) ? 1 : 0;
            var length = preset.StartsWith("legacyOblique", StringComparison.Ordinal) ? 0.5 : 0.3;
            shear = preset.StartsWith("legacyPerspective", StringComparison.Ordinal)
                ? (dx * 0.1333, dy * 0.1333)
                : dx != 0 && dy != 0 ? (dx * length / Math.Sqrt(2), dy * length / Math.Sqrt(2)) : (dx * length, dy * length);
            // Legacy perspective presets are drawn as their oblique shift; the shift replaces the perspective.
            perspective = false;
            oblique = true;
        }
        return new Camera(x, y, z, Math.Clamp(fov, 1, 179), perspective, oblique) { Shear = shear, Zoom = zoom > 0 ? zoom : 1, Legacy = legacy };
    }

    /// <summary>
    /// A legacy camera (legacyOblique…, legacyPerspective…): its light rig stays with the viewer while the scene turns
    /// about the horizontal and vertical axes and turns along in the picture plane, where the other cameras' rigs turn
    /// with the scene (measured: the front of a shape turned by 300° about the vertical axis dark under a legacy
    /// camera, lit under orthographicFront; a legacy oblique scene turned by 10° and 20° in the plane lit as PowerPoint).
    /// </summary>
    public bool Legacy { get; init; }

    /// <summary>
    /// An oblique camera that is not a legacy one turns the projected picture by the shape's rotation, its depths' shift
    /// along, where the other cameras turn the shape in its plane before they project it (measured with
    /// local/oblique-rot: an extruded box under obliqueTopRight turned by 60° shows its faces lit as unturned).
    /// </summary>
    public bool TurnsPicture => Oblique && !Legacy;

    /// <summary>The camera leaves a flat shape as it is.</summary>
    public bool Flat => !Oblique && Normalized(X) == 0 && Normalized(Y) == 0 && Normalized(Z) == 0 && (!Perspective || Math.Abs(Offset) < 1e-9)
                        && Math.Abs(Zoom - 1) < 1e-9;

    static double Normalized(double degrees) => Math.Abs(Math.IEEERemainder(degrees, 360)) < 1e-9 ? 0 : degrees;

    /// <summary>Distance of the eye from the shape's center in pixels (perspective cameras).</summary>
    public double Distance => ViewSize * 96 / 72 / 2 / Math.Tan(Fov * Math.PI / 360);

    /// <summary>The rotation as a 3×3 matrix (row major) for x right, y down and z towards the viewer.</summary>
    public double[] Rotation()
    {
        static double[] Multiply(double[] a, double[] b)
        {
            var m = new double[9];
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 3; c++)
                    m[r * 3 + c] = a[r * 3] * b[c] + a[r * 3 + 1] * b[3 + c] + a[r * 3 + 2] * b[6 + c];
            return m;
        }
        // About the vertical axis by X, then the horizontal axis by −Y, then the view axis by Z.
        var (ax, ay, az) = (-Y * Math.PI / 180, X * Math.PI / 180, Z * Math.PI / 180);
        double[] rx = [1, 0, 0, 0, Math.Cos(ax), -Math.Sin(ax), 0, Math.Sin(ax), Math.Cos(ax)];
        double[] ry = [Math.Cos(ay), 0, Math.Sin(ay), 0, 1, 0, -Math.Sin(ay), 0, Math.Cos(ay)];
        double[] rz = [Math.Cos(az), -Math.Sin(az), 0, Math.Sin(az), Math.Cos(az), 0, 0, 0, 1];
        return Multiply(rz, Multiply(rx, ry));
    }

    /// <summary>A point or direction of the scene turned into the view (x right, y down, z towards the eye).</summary>
    public (double X, double Y, double Z) Turn((double X, double Y, double Z) p)
    {
        var r = Rotation();
        var q = (X: r[0] * p.X + r[1] * p.Y + r[2] * p.Z, Y: r[3] * p.X + r[4] * p.Y + r[5] * p.Z, Z: r[6] * p.X + r[7] * p.Y + r[8] * p.Z);
        // Oblique cameras turn the scene by their rotation (a:rot) too, then shift it by depth.
        return Oblique ? (q.X - q.Z * Shear.X, q.Y - q.Z * Shear.Y, q.Z) : q;
    }

    /// <summary>
    /// A point of the scene (relative to the shape's center) turned into the view about the scene's center, standing
    /// at the shape's offset; the result is relative to the scene's center.
    /// </summary>
    public (double X, double Y, double Z) Place((double X, double Y, double Z) p) => Turn((p.X - Pivot.X, p.Y - Pivot.Y, p.Z + Offset));

    /// <summary>How much a turned point is enlarged on the picture plane (perspective cameras and zoom).</summary>
    public double Scale((double X, double Y, double Z) q) => (Perspective ? Distance / Math.Max(1e-6, Distance - q.Z) : 1) * Zoom;

    /// <summary>A turned point (relative to the scene's center) on the picture plane, relative to the shape's center.</summary>
    public (double X, double Y) Flatten((double X, double Y, double Z) q)
    {
        var f = Scale(q);
        return (Pivot.X + q.X * f, Pivot.Y + q.Y * f);
    }

    /// <summary>A point relative to the shape's center (pixels, z towards the viewer) on the picture plane.</summary>
    public (double X, double Y) Project(double x, double y, double z) => Flatten(Place((x, y, z)));

    /// <summary>
    /// The direction towards the eye in the scene seen from a scene point: straight ahead for parallel projections,
    /// from the point to the eye for perspective ones.
    /// </summary>
    public (double X, double Y, double Z) View((double X, double Y, double Z) scenePoint)
    {
        var r = Rotation();
        if (Oblique)
        {
            // Oblique projections look along the shear, turned back into the scene with the transpose.
            var length = Math.Sqrt(Shear.X * Shear.X + Shear.Y * Shear.Y + 1);
            var (vx, vy, vz) = (Shear.X / length, Shear.Y / length, 1 / length);
            return (r[0] * vx + r[3] * vy + r[6] * vz, r[1] * vx + r[4] * vy + r[7] * vz, r[2] * vx + r[5] * vy + r[8] * vz);
        }
        // The eye sits at (0, 0, d) in view coordinates; turned back into the scene with the transpose.
        var (ex, ey, ez) = Perspective ? (0.0, 0.0, Distance) : (0.0, 0.0, 1.0);
        var eye = (X: r[0] * ex + r[3] * ey + r[6] * ez, Y: r[1] * ex + r[4] * ey + r[7] * ez, Z: r[2] * ex + r[5] * ey + r[8] * ez);
        return Perspective ? (eye.X + Pivot.X - scenePoint.X, eye.Y + Pivot.Y - scenePoint.Y, eye.Z - scenePoint.Z - Offset) : eye;
    }

    /// <summary>
    /// The CSS matrix3d (column major) that projects the plane z = 0 like <see cref="Project"/>, for points
    /// relative to the shape's center.
    /// </summary>
    public double[] Matrix3D()
    {
        var o = Offset;
        double[] about;
        if (Oblique)
        {
            // x' = turned x − Sx · turned z (the offset included), likewise y; z' = turned z without the offset.
            var r = Rotation();
            var (sx, sy) = Shear;
            about =
            [
                r[0] - sx * r[6], r[3] - sy * r[6], r[6], 0,
                r[1] - sx * r[7], r[4] - sy * r[7], r[7], 0,
                r[2] - sx * r[8], r[5] - sy * r[8], r[8], 0,
                (r[2] - sx * r[8]) * o, (r[5] - sy * r[8]) * o, 0, 1,
            ];
        }
        else
        {
            var r = Rotation();
            // Rows of the 4×4 matrix: x' = r0 x + r1 y + r2 (z + o), y' = r3 x + r4 y + r5 (z + o), z' = r8 z (the
            // plane keeps z = 0; the turned z axis tells the browser which side faces the eye),
            // w = 1 - (r6 x + r7 y + r8 (z + o)) / d.
            var w = Perspective ? 1 / Distance : 0;
            about =
            [
                r[0], r[3], r[6], -r[6] * w,
                r[1], r[4], r[7], -r[7] * w,
                r[2], r[5], r[8], -r[8] * w,
                r[2] * o, r[5] * o, 0, 1 - r[8] * o * w,
            ];
        }
        if (Pivot == (0, 0) && Zoom == 1)
            return about;
        // About the scene's center, zoomed: T(pivot) · zoom · M · T(−pivot).
        double[] from = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, -Pivot.X, -Pivot.Y, 0, 1];
        double[] to = [Zoom, 0, 0, 0, 0, Zoom, 0, 0, 0, 0, 1, 0, Pivot.X, Pivot.Y, 0, 1];
        return Multiply(to, Multiply(about, from));
    }

    /// <summary>The product a · b of column-major 4×4 matrices.</summary>
    public static double[] Multiply(double[] a, double[] b)
    {
        var result = new double[16];
        for (var c = 0; c < 4; c++)
            for (var r = 0; r < 4; r++)
                for (var k = 0; k < 4; k++)
                    result[c * 4 + r] += a[k * 4 + r] * b[c * 4 + k];
        return result;
    }
}
