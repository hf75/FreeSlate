namespace FreeSlate.Drawing;

/// <summary>
/// Lighting of 3D shapes (catalog: effect.scene3d): a color becomes color · diffuse + white · specular. The lights stay
/// with the scene while the camera turns about the shape, so a face's diffuse light depends only on its normal in the
/// scene. The default rig (threePt from the top) with the default material (warmMatte) was fitted to PowerPoint's
/// rendering of tilted faces, extruded cubes and cylinders and tilted bevels seen from many sides, split into diffuse
/// and specular parts with gray and blue copies: a key light from the upper left front, a fill light from the lower
/// right front and a back light from the lower left behind, Lambert diffuse and Phong specular with one shininess
/// (errors: diffuse 0.013, specular 0.027). Normals use x right, y down and z towards the front.
/// </summary>
public static class Lighting
{
    const double Ambient = 0.0037, Shininess = 8.655;

    static readonly (double X, double Y, double Z, double Diffuse, double Specular)[] Lights =
    [
        (-0.2681, -0.6501, 0.7109, 1.1372, 0.3447),
        (0.2511, 0.8491, 0.4647, 0.4926, 0.1481),
        (-0.2851, 0.5801, -0.7630, 0.9689, 0.2837),
    ];

    /// <summary>The diffuse factor and the specular amount of a face with normal <paramref name="n"/> seen from <paramref name="v"/>.</summary>
    public static (double Diffuse, double Specular) Shade((double X, double Y, double Z) n, (double X, double Y, double Z) v)
    {
        n = Normalize(n);
        v = Normalize(v);
        double diffuse = Ambient, specular = 0;
        foreach (var (x, y, z, d, s) in Lights)
        {
            var nl = n.X * x + n.Y * y + n.Z * z;
            if (nl <= 0)
                continue;
            diffuse += d * nl;
            // Phong: the light reflected about the normal, against the direction to the eye.
            var (rx, ry, rz) = (2 * nl * n.X - x, 2 * nl * n.Y - y, 2 * nl * n.Z - z);
            specular += s * Math.Pow(Math.Max(0, rx * v.X + ry * v.Y + rz * v.Z), Shininess);
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
