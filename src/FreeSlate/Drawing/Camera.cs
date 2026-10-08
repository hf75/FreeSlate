using System.Xml.Linq;

namespace FreeSlate.Drawing;

/// <summary>
/// The camera of a 3D scene (a:scene3d/a:camera, catalog: effect.scene3d): the shape is turned about its center by
/// lon (about the vertical axis), then lat (about the horizontal axis), then rev (in the picture plane), and seen
/// from the front, in perspective for the perspective presets. Fitted to corner positions PowerPoint renders for
/// 30 cameras (errors below 0.4 pt): the eye stands 907.5 pt / 2 / tan(fov / 2) in front of the shape's center, and
/// points in the shape's plane keep their size. The presets' angles and fields of view come from PowerPoint
/// (ThreeD.SetPresetCamera, then RotationX/Y/Z and FieldOfView).
/// </summary>
public sealed record Camera(double Lat, double Lon, double Rev, double Fov, bool Perspective, bool Oblique)
{
    const double ViewSize = 907.5;   // points

    static readonly Dictionary<string, (double Lon, double Lat, double Rev, double Fov)> Presets = new()
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
        var (lon, lat, rev, fov) = Presets.TryGetValue(preset, out var p) ? p : (0, 0, 0, preset.StartsWith("legacyPerspective", StringComparison.Ordinal) ? 65 : 45);
        if (camera.Element(Ns.A + "rot") is { } rot)
        {
            // An explicit rotation replaces the preset's (60000ths of a degree).
            lat = (rot.Long("lat") ?? 0) / 60000.0;
            lon = (rot.Long("lon") ?? 0) / 60000.0;
            rev = (rot.Long("rev") ?? 0) / 60000.0;
        }
        if (camera.Long("fov") is { } f)
            fov = f / 60000.0;
        return new Camera(lat, lon, rev, Math.Clamp(fov, 1, 179), perspective, oblique);
    }

    /// <summary>The camera leaves a flat shape as it is.</summary>
    public bool Flat => Normalized(Lat) == 0 && Normalized(Lon) == 0 && Normalized(Rev) == 0;

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
        var (lat, lon, rev) = (-Lat * Math.PI / 180, Lon * Math.PI / 180, Rev * Math.PI / 180);
        double[] x = [1, 0, 0, 0, Math.Cos(lat), -Math.Sin(lat), 0, Math.Sin(lat), Math.Cos(lat)];
        double[] y = [Math.Cos(lon), 0, Math.Sin(lon), 0, 1, 0, -Math.Sin(lon), 0, Math.Cos(lon)];
        double[] z = [Math.Cos(rev), -Math.Sin(rev), 0, Math.Sin(rev), Math.Cos(rev), 0, 0, 0, 1];
        return Multiply(z, Multiply(x, y));
    }

    /// <summary>A point relative to the shape's center (pixels, z towards the viewer) on the picture plane.</summary>
    public (double X, double Y) Project(double x, double y, double z)
    {
        var r = Rotation();
        var (px, py, pz) = (r[0] * x + r[1] * y + r[2] * z, r[3] * x + r[4] * y + r[5] * z, r[6] * x + r[7] * y + r[8] * z);
        if (!Perspective)
            return (px, py);
        var f = Distance / (Distance - pz);
        return (px * f, py * f);
    }

    /// <summary>
    /// The CSS matrix3d (column major) that projects the plane z = 0 like <see cref="Project"/>, for points
    /// relative to the shape's center.
    /// </summary>
    public double[] Matrix3D()
    {
        var r = Rotation();
        // Rows of the 4×4 matrix: x' = r0 x + r1 y, y' = r3 x + r4 y, z' = 0, w = 1 - (r6 x + r7 y) / d.
        var w = Perspective ? 1 / Distance : 0;
        return
        [
            r[0], r[3], 0, -r[6] * w,
            r[1], r[4], 0, -r[7] * w,
            0, 0, 1, 0,
            0, 0, 0, 1,
        ];
    }
}
