using System.Xml.Linq;

namespace FreeSlate.Drawing;

/// <summary>
/// Lighting of 3D shapes (catalog: effect.scene3d): per channel, a color becomes color · diffuse + specular. The lights
/// stay with the scene while the camera turns about the shape, so a face's diffuse light depends only on its normal in
/// the scene. Every light rig and material was fitted to PowerPoint's rendering of extruded cubes and cylinders and of
/// tilted bevels seen from many sides, rendered in two base colors so that diffuse and specular separate per channel:
/// Lambert diffuse and Phong highlights with one shininess per material; materials add highlights tinted by the
/// surface (metals), darken the diffuse light towards the silhouette (dkEdge, powders) and add white there (powders).
/// Normals use x right, y down and z towards the front. The rig turns with a:lightRig/@dir (45° steps clockwise from
/// t) and a:lightRig/a:rot.
/// </summary>
public sealed record Lighting
{
    public sealed record Light((double X, double Y, double Z) Direction, (double R, double G, double B) Diffuse, (double R, double G, double B) Specular);

    public required IReadOnlyList<Light> Lights { get; init; }
    public required (double R, double G, double B) Ambient { get; init; }
    public required double Shininess { get; init; }

    /// <summary>How much of the highlights is white (added) and how much tinted by the surface color (multiplied).</summary>
    public double White { get; init; } = 1;

    /// <summary>See <see cref="White"/>.</summary>
    public double Tint { get; init; }

    /// <summary>The diffuse light fades towards the silhouette: it is multiplied by 1 − Strength · (1 − n·v)^Power.</summary>
    public (double Strength, double Power) Edge { get; init; } = (0, 1);

    /// <summary>White added to every face, and more towards the silhouette: Base + Rim · (1 − n·v).</summary>
    public (double Base, double Rim) Sheen { get; init; }

    /// <summary>threePt from the top with warmMatte: key light upper left front, fill lower right front, back light lower left behind.</summary>
    public static readonly Lighting Default = new()
    {
        Ambient = (0.0048, 0.0048, 0.0048),
        Shininess = 8.669,
        Lights =
        [
            new((-0.2686, -0.6501, 0.7108), (1.1345, 1.1345, 1.1345), (0.3424, 0.3424, 0.3424)),
            new((0.2499, 0.8483, 0.4669), (0.494, 0.494, 0.494), (0.1446, 0.1446, 0.1446)),
            new((-0.2877, 0.5765, -0.7648), (0.9721, 0.9721, 0.9721), (0.2857, 0.2857, 0.2857)),
        ],
    };

    /// <summary>The lighting of a shape from its a:scene3d light rig and a:sp3d material.</summary>
    public static Lighting For(XElement? scene3d, XElement? sp3d)
    {
        var rigElement = scene3d?.Element(Ns.A + "lightRig");
        var rig = LightRigs.Get(rigElement?.Str("rig")) ?? Default;
        rig = LightRigs.WithMaterial(rig, sp3d?.Str("prstMaterial"));
        // The rig's direction turns it clockwise in 45° steps; a:rot adds a turn about the view axis (rev) and the
        // other axes (lat, lon).
        var turn = (rigElement?.Str("dir") ?? "t") switch
        {
            "tr" => 45, "r" => 90, "br" => 135, "b" => 180, "bl" => 225, "l" => 270, "tl" => 315, _ => 0,
        };
        var rot = rigElement?.Element(Ns.A + "rot");
        var rev = turn + (rot.Long("rev") ?? 0) / 60000.0;
        var lat = (rot.Long("lat") ?? 0) / 60000.0;
        var lon = (rot.Long("lon") ?? 0) / 60000.0;
        return rev == 0 && lat == 0 && lon == 0 ? rig : rig.Turned(lat, lon, rev);
    }

    /// <summary>The rig turned: about the vertical axis by lon, the horizontal axis by lat, the view axis by rev (clockwise on screen).</summary>
    public Lighting Turned(double lat, double lon, double rev)
    {
        var (a, b, c) = (lat * Math.PI / 180, lon * Math.PI / 180, rev * Math.PI / 180);
        (double X, double Y, double Z) Turn((double X, double Y, double Z) d)
        {
            // lon about y, lat about x, then rev about z (y down: positive angles turn clockwise on screen).
            var (x1, z1) = (d.X * Math.Cos(b) + d.Z * Math.Sin(b), -d.X * Math.Sin(b) + d.Z * Math.Cos(b));
            var (y2, z2) = (d.Y * Math.Cos(a) - z1 * Math.Sin(a), d.Y * Math.Sin(a) + z1 * Math.Cos(a));
            return (x1 * Math.Cos(c) - y2 * Math.Sin(c), x1 * Math.Sin(c) + y2 * Math.Cos(c), z2);
        }
        return this with { Lights = Lights.Select(l => l with { Direction = Turn(l.Direction) }).ToList() };
    }

    /// <summary>The diffuse factors and the specular amounts (per channel) of a face with normal <paramref name="n"/> seen from <paramref name="v"/>.</summary>
    public ((double R, double G, double B) Diffuse, (double R, double G, double B) Specular) Shade((double X, double Y, double Z) n, (double X, double Y, double Z) v)
    {
        n = Normalize(n);
        v = Normalize(v);
        var diffuse = Ambient;
        (double R, double G, double B) highlight = (0, 0, 0);
        foreach (var light in Lights)
        {
            var (x, y, z) = light.Direction;
            var nl = n.X * x + n.Y * y + n.Z * z;
            if (nl <= 0)
                continue;
            diffuse = (diffuse.R + light.Diffuse.R * nl, diffuse.G + light.Diffuse.G * nl, diffuse.B + light.Diffuse.B * nl);
            // Phong: the light reflected about the normal, against the direction to the eye.
            var (rx, ry, rz) = (2 * nl * n.X - x, 2 * nl * n.Y - y, 2 * nl * n.Z - z);
            var lobe = Math.Pow(Math.Max(0, rx * v.X + ry * v.Y + rz * v.Z), Shininess);
            highlight = (highlight.R + light.Specular.R * lobe, highlight.G + light.Specular.G * lobe, highlight.B + light.Specular.B * lobe);
        }
        var facing = Math.Clamp(n.X * v.X + n.Y * v.Y + n.Z * v.Z, 0, 1);
        var edge = Edge.Strength == 0 ? 1 : Math.Max(0, 1 - Edge.Strength * Math.Pow(1 - facing, Edge.Power));
        var sheen = Sheen.Base + Sheen.Rim * (1 - facing);
        return ((diffuse.R * edge + Tint * highlight.R, diffuse.G * edge + Tint * highlight.G, diffuse.B * edge + Tint * highlight.B),
            (White * highlight.R + sheen, White * highlight.G + sheen, White * highlight.B + sheen));
    }

    /// <summary>A color lit by a shade.</summary>
    public static RgbColor Apply(RgbColor color, ((double R, double G, double B) Diffuse, (double R, double G, double B) Specular) shade) => new(
        Math.Clamp(color.R * shade.Diffuse.R + shade.Specular.R, 0, 1),
        Math.Clamp(color.G * shade.Diffuse.G + shade.Specular.G, 0, 1),
        Math.Clamp(color.B * shade.Diffuse.B + shade.Specular.B, 0, 1),
        color.A);

    static (double X, double Y, double Z) Normalize((double X, double Y, double Z) v)
    {
        var length = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        return length < 1e-12 ? (0, 0, 1) : (v.X / length, v.Y / length, v.Z / length);
    }
}
