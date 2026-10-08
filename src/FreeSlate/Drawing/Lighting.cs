namespace FreeSlate.Drawing;

/// <summary>
/// Lighting of 3D shapes (catalog: effect.scene3d): a color becomes color · diffuse + white · specular. The lights stay
/// with the scene while the camera turns about the shape, so a face's diffuse light depends only on its normal in the
/// scene. Fitted to PowerPoint's rendering of the default rig (threePt from the top) and material (warmMatte): tilted
/// faces, extruded cubes and cylinders seen from many sides, split into diffuse and specular parts with gray and
/// blue copies (diffuse error 0.006, specular 0.021). Normals use x right, y down and z towards the front.
/// </summary>
public static class Lighting
{
    const double Ambient = 0.0031;

    static readonly (double X, double Y, double Z, double Strength)[] DiffuseLights =
    [
        (0.1791, 0.6542, 0.7348, 0.6687),
        (-0.3070, -0.7446, -0.5928, 0.9911),
        (-0.3377, 0.6663, 0.6648, 0.8203),
    ];

    static readonly (double X, double Y, double Z, double Strength, double Power)[] SpecularLights =
    [
        (0.2518, 0.8170, 0.5188, 0.1618, 23.57),
        (-0.2534, -0.6315, 0.7328, 0.3330, 23.66),
        (-0.3952, 0.7234, -0.5661, 0.1672, 17.80),
    ];

    /// <summary>The diffuse factor and the specular amount of a face with normal <paramref name="n"/> seen from <paramref name="v"/>.</summary>
    public static (double Diffuse, double Specular) Shade((double X, double Y, double Z) n, (double X, double Y, double Z) v)
    {
        n = Normalize(n);
        v = Normalize(v);
        var diffuse = Ambient;
        foreach (var (x, y, z, strength) in DiffuseLights)
            diffuse += strength * Math.Max(0, n.X * x + n.Y * y + n.Z * z);
        var specular = 0.0;
        foreach (var (x, y, z, strength, power) in SpecularLights)
        {
            if (n.X * x + n.Y * y + n.Z * z <= 0)
                continue;
            var h = Normalize((x + v.X, y + v.Y, z + v.Z));
            specular += strength * Math.Pow(Math.Max(0, n.X * h.X + n.Y * h.Y + n.Z * h.Z), power);
        }
        return (diffuse, specular);
    }

    /// <summary>A color lit by a shade.</summary>
    public static RgbColor Apply(RgbColor color, (double Diffuse, double Specular) shade) => new(
        Math.Clamp(color.R * shade.Diffuse + shade.Specular, 0, 1),
        Math.Clamp(color.G * shade.Diffuse + shade.Specular, 0, 1),
        Math.Clamp(color.B * shade.Diffuse + shade.Specular, 0, 1),
        color.A);

    static (double X, double Y, double Z) Normalize((double X, double Y, double Z) v)
    {
        var length = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        return length < 1e-12 ? (0, 0, 1) : (v.X / length, v.Y / length, v.Z / length);
    }
}
