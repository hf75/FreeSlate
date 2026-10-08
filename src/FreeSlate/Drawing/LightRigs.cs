namespace FreeSlate.Drawing;

/// <summary>
/// Light rig presets (a:lightRig/@rig, catalog: effect.scene3d) and materials (a:sp3d/@prstMaterial), fitted to
/// PowerPoint's rendering (see <see cref="Lighting"/>): every rig with warmMatte, every material with threePt.
/// Directions point from the shape towards the light, with x right, y down and z towards the front; strengths are
/// one number for white light or one per channel for colored light.
/// </summary>
public static class LightRigs
{
    static readonly Dictionary<string, Lighting> Rigs = new()
    {
        ["threePt"] = Lighting.Default,
        ["balanced"] = Rig(0.1263, 8.6939,
            L(-0.4104, 0.523, 0.747, 1.0511, 0.3157),
            L(0.3435, -0.9383, 0.0401, 1.0007, 0.2979),
            L(0.7614, 0.1303, -0.635, 0.4624, 0.1266),
            L(0.7978, -0.2274, -0.5585, 0.0463, 0.0233)),
        ["soft"] = Rig(0.4948, 9.0984,
            L(-0.0662, -0.2467, -0.9668, 0.6497, 0.0023),
            L(-0.2039, -0.2376, 0.9497, 0.3055, 0.0038),
            L(0.2492, -0.7006, 0.6686, 0.2998, 0.241),
            L(0.9817, -0.1452, -0.1229, 0.222, 0)),
        ["harsh"] = Rig(0.2893, 8.6437,
            L(-0.743, -0.5856, 0.3241, 0.8404, 0.2603),
            L(-0.688, 0.657, 0.3084, 0.736, 0.1616),
            L(-0.6517, 0.6898, 0.3154, 0.1332, 0.1006),
            L(-0.6938, -0.585, 0.4201, 0.0251, 0)),
        ["flood"] = Rig(0.1295, 8.8843,
            L(-0.7606, 0.5746, 0.3021, 1.0881, 0.3392),
            L(-0.9723, -0.1897, 0.1368, 0.605, 0.1319),
            L(0.1462, -0.8982, 0.4146, 0.55, 0.1632),
            L(-0.9455, -0.2788, 0.1681, 0.5106, 0.2127)),
        ["contrasting"] = Rig(0.989, 8.1095,
            L(-1, 0.0037, 0.0016, 0.0046, 0.2963),
            L(1, 0.0006, 0.001, 0.0043, 0.2974)),
        ["morning"] = Rig((0.0307, 0.0279, 0.0239), 10.283,
            L(-0.6768, 0.6681, 0.3093, (1.4931, 1.4352, 1.3602), (0.1592, 0.1604, 0.1546)),
            L(0.9751, -0.113, -0.1909, (0.5077, 0.4577, 0.4171), (0.1945, 0.1787, 0.1535)),
            L(-0.466, -0.204, 0.8609, (0.4504, 0.4459, 0.3838), (0, 0, 0)),
            L(0.9465, -0.2867, -0.148, (0.38, 0.3667, 0.293), (0.1146, 0.1074, 0.0916))),
        ["sunrise"] = Rig(0.0005, 19.2143,
            L(0.9679, -0.1803, -0.1751, (1.5066, 1.3106, 0.9906), (0, 0, 0)),
            L(-0.66, 0.6752, 0.3295, (1.3586, 1.2031, 1.0575), (0.1894, 0.1858, 0.1426)),
            L(-0.4498, -0.2706, 0.8511, (0.4734, 0.4697, 0.3825), (0, 0, 0)),
            L(-0.7684, 0.6378, -0.0528, (0.1179, 0.1256, 0.0972), (0.1802, 0.1418, 0.1059))),
        ["sunset"] = Rig((0.0199, 0.0092, 0.0008), 15.3415,
            L(-0.6838, 0.6659, 0.2983, (1.3567, 0.6985, 0.6153), (0.2046, 0.05, 0.0479)),
            L(0.8856, -0.425, -0.1873, (0.7461, 0.6833, 0.3996), (0, 0, 0)),
            L(-0.3458, 0.1742, 0.922, (0.4886, 0.5163, 0.3813), (0, 0.0001, 0.0002)),
            L(-0.0033, 0.0336, -0.9994, (0.0624, 0.0763, 0.0556), (0.2025, 0.2015, 0.2031))),
        ["chilly"] = Rig((0.1114, 0.1122, 0.1149), 5.5373,
            L(-0.648, 0.6688, 0.3645, (1.3275, 1.3271, 1.4541), (0.0548, 0.0562, 0.0481)),
            L(0.8024, -0.5791, -0.1445, (0.407, 0.4431, 0.4735), (0.0873, 0.098, 0.1043)),
            L(-0.1435, -0.3535, 0.9244, (0.441, 0.4394, 0.4387), (0, 0, 0)),
            L(-0.0104, -0.0064, -0.9999, (0.2387, 0.2586, 0.2979), (0.106, 0.1053, 0.0988))),
        ["freezing"] = Rig((0.0088, 0.0075, 0.0096), 8.7555,
            L(-0.6773, 0.6715, 0.3007, (1.1549, 1.1793, 1.5338), (0.1323, 0.143, 0.149)),
            L(0.9716, -0.1478, -0.1849, (0.6484, 0.7759, 0.9501), (0.1927, 0.2398, 0.2761)),
            L(-0.4379, -0.2627, 0.8598, (0.3707, 0.4618, 0.4713), (0, 0, 0)),
            L(0.9584, -0.2437, -0.1488, (0.308, 0.3991, 0.3999), (0.1014, 0.1231, 0.1415))),
        ["flat"] = Rig(0.9923, 8.4914,
            L(0.8568, -0.1298, -0.499, (0.0032, 0.0018, 0.0004), (0.2906, 0.3538, 0.4023)),
            L(-0.1645, -0.9539, 0.2509, 0.0025, 0.2499),
            L(-0.732, 0.6582, 0.176, 0.0022, 1.2551),
            L(0.8574, 0.1072, -0.5033, (0.0003, 0, 0.002), (0.3643, 0.452, 0.5127))),
        ["twoPt"] = Rig(0.2472, 8.9117,
            L(-0.4279, 0.5435, 0.7222, 0.6761, 0.202),
            L(0.2449, -0.8924, 0.3791, 0.3044, 0.0831),
            L(-0.0158, -0.0119, -0.9998, 0.2204, 0),
            L(-0.3241, 0.4653, 0.8237, 0.1656, 0.0583)),
        ["glow"] = Rig(0.996, 8.894,
            L(-1, -0.0008, 0, 1.0059, 0.3111),
            L(-0.0013, -0.0068, -1, 0.8878, 0.0047),
            L(1, -0.0016, 0.0023, 0.6923, 0.211)),
        ["brightRoom"] = Rig(0.9142, 8.5672,
            L(-0.9894, 0.1269, -0.0704, 1.298, 0.007),
            L(0.9488, 0.287, -0.132, 0.8296, 0),
            L(0.1133, -0.9933, -0.0247, 0.556, 0),
            L(-0.1877, 0.8247, 0.5335, 0.1286, 0.2994)),
        ["legacyFlat1"] = Rig(0.5936, 14.3768,
            L(0.0034, -0.0008, 1, 0.6015, 0.2531),
            L(0.0219, 0.0186, 0.9996, 0.2635, 0.0665),
            L(-0.5084, -0.6547, 0.5595, 0.0117, 0.0001)),
        ["legacyFlat2"] = Rig(0.4838, 15.4338,
            L(0.9015, 0.3838, -0.1998, 0.3224, 0),
            L(-0.3164, -0.9464, -0.0654, 0.2818, 0.0241),
            L(-0.6966, -0.6962, 0.1733, 0.2286, 0.2389),
            L(0.3431, -0.2937, 0.8922, 0.2242, 0.0003)),
        ["legacyFlat3"] = Rig(0.5911, 7.4103,
            L(-0.9785, -0.0052, 0.2063, 0.2932, 0.1557),
            L(0.9943, 0.0822, -0.0677, 0.1748, 0),
            L(0.9366, -0.1601, -0.3118, 0.1163, 0),
            L(-0.0126, 0.0376, 0.9992, 0.1109, 0)),
        ["legacyFlat4"] = Rig(0.4267, 7.6768,
            L(-0.659, 0.7313, 0.1759, 0.435, 0.15),
            L(0.8395, -0.5361, 0.0887, 0.3605, 0),
            L(0.5857, 0.8093, -0.0449, 0.2155, 0),
            L(-0.0225, 0.4085, 0.9125, 0.1731, 0.001)),
        ["legacyNormal1"] = Rig(0.3306, 7.384,
            L(0.0002, -0.0014, 1, 0.6243, 0.1732),
            L(-0.0719, 0.0908, 0.9933, 0.2055, 0.0092),
            L(0.3599, -0.8463, 0.3927, 0.0337, 0),
            L(0.5265, 0.5725, 0.6285, 0.0124, 0)),
        ["legacyNormal2"] = Rig(0.2818, 15.0055,
            L(-0.5573, -0.824, 0.1017, 0.4758, 0.1134),
            L(0.9286, 0.3365, -0.1566, 0.19, 0),
            L(-0.7661, -0.6154, 0.1854, 0.1252, 0.1917),
            L(0.3644, -0.2909, 0.8846, 0.1184, 0)),
        ["legacyNormal3"] = Rig(0.3037, 7.4201,
            L(-0.9788, -0.0176, 0.2041, 0.5004, 0.1791),
            L(0.8479, -0.526, -0.066, 0.1246, 0),
            L(0.9079, 0.4192, -0.0065, 0.1217, 0),
            L(-0.3201, 0.2228, 0.9208, 0.0847, 0.0044)),
        ["legacyNormal4"] = Rig(0.2357, 7.4005,
            L(-0.6766, 0.7236, 0.1363, 0.5046, 0.1787),
            L(0.8383, -0.5445, 0.0275, 0.2364, 0),
            L(-0.3419, 0.5328, 0.7741, 0.1518, 0.0013),
            L(0.6233, 0.7777, 0.0818, 0.1362, 0)),
        ["legacyHarsh1"] = Rig(0.0481, 7.383,
            L(0.0002, -0.0018, 1, 0.7958, 0.1773),
            L(-0.0183, 0.0549, 0.9983, 0.2226, 0.033),
            L(0.5171, -0.8072, 0.2846, 0.0156, 0)),
        ["legacyHarsh2"] = Rig(0.0582, 14.315,
            L(-0.7159, -0.6932, 0.0826, 0.6128, 0.2246),
            L(0.9789, 0.0113, 0.2042, 0.2134, 0),
            L(-0.6369, -0.7004, 0.3222, 0.1893, 0.1036)),
        ["legacyHarsh3"] = Rig(0.0578, 14.2849,
            L(-0.9644, 0.0089, 0.2642, 0.6384, 0.2457),
            L(0.9832, -0.0183, 0.1817, 0.2178, 0),
            L(-0.9966, -0.0168, -0.0807, 0.1657, 0.0802),
            L(-0.1135, -0.0671, -0.9913, 0.0476, 0.1021)),
        ["legacyHarsh4"] = Rig(0.0519, 7.5107,
            L(-0.7048, 0.6951, 0.142, 0.7972, 0.2164),
            L(-0.0072, 0.0109, -0.9999, 0.7674, 0.0457),
            L(0.0493, -0.0113, -0.9987, 0.5305, 0.0182),
            L(0.9524, 0.0074, 0.3046, 0.2064, 0)),
    };

    static Lighting Rig(double ambient, double shininess, params Lighting.Light[] lights) => Rig((ambient, ambient, ambient), shininess, lights);

    static Lighting Rig((double R, double G, double B) ambient, double shininess, params Lighting.Light[] lights) =>
        new() { Ambient = ambient, Shininess = shininess, Lights = lights };

    static Lighting.Light L(double x, double y, double z, double diffuse, double specular) =>
        new((x, y, z), (diffuse, diffuse, diffuse), (specular, specular, specular));

    static Lighting.Light L(double x, double y, double z, (double R, double G, double B) diffuse, (double R, double G, double B) specular) =>
        new((x, y, z), diffuse, specular);

    /// <summary>The rig of a preset name, or null when unknown.</summary>
    public static Lighting? Get(string? name) => name != null && Rigs.TryGetValue(name, out var rig) ? rig : null;

    /// <summary>
    /// A material: its lighting under threePt (key, fill and back light with their diffuse and highlight strengths) and,
    /// for the other rigs, how it scales their diffuse light and highlights and what ambient light it adds.
    /// </summary>
    sealed record Material(Lighting ThreePt, double Diffuse, double Specular, double Ambient);

    /// <summary>
    /// The materials against warmMatte (the default, fitted with the rig itself): matte has no highlights, plastics and
    /// metals stronger and tighter ones, metals partly tinted; powders, dkEdge and softEdge fade towards the silhouette,
    /// powders whiten there; flat ignores the direction of the light. legacyWireframe draws the edges only.
    /// </summary>
    static readonly Dictionary<string, Material> Materials = new()
    {
        ["legacyMatte"] = new(ThreePt(0.0029, 2.8548, (1.135, 0), (0.4911, 0), (0.9757, 0)) with { White = 0 }, 1.0005, 0, -0.0019),
        ["legacyPlastic"] = new(ThreePt(0.001, 9.3786, (1.1388, 1.3053), (0.4945, 0.4376), (0.9758, 0.1966)) with { White = 1.0642, Sheen = (0.0051, -0.0173) }, 1.0033, 2.51, -0.0038),
        ["legacyMetal"] = new(ThreePt(0.0271, 8.9858, (0.8724, 0.3235), (0.3746, 0.135), (0.7707, 0.1207)) with { White = 0, Tint = 3.4784, Edge = (0.1711, 0.1258) }, 0.7759, 0.7496, 0.0234),
        ["matte"] = new(ThreePt(0.0029, 2.8548, (1.135, 0), (0.4911, 0), (0.9757, 0)) with { White = 0 }, 1.0005, 0, -0.0019),
        ["plastic"] = new(ThreePt(0.0011, 12.6566, (1.1379, 0.5866), (0.4948, 0.2494), (0.9761, 0.4735)) with { White = 1.2086 }, 1.0032, 1.6947, -0.0037),
        ["metal"] = new(ThreePt(0.0202, 12.2255, (1.1225, 1.2136), (0.4644, 0.4671), (0.9376, 0.8456)) with { White = 1.398, Edge = (0.7319, 3.2338), Sheen = (-0.0554, 0.2718) }, 0.9707, 3.2694, 0.0155),
        ["translucentPowder"] = new(ThreePt(0.0033, 2.8347, (0.8279, 0.7164), (0.3572, 0.2764), (0.7107, 0.3559)) with { White = 0.4028, Edge = (1.1534, 0.8806), Sheen = (0.3422, 0.7711) }, 0.729, 1.7454, -0.0002),
        ["powder"] = new(ThreePt(0, 1.1066, (0.5745, 0.6946), (0.2503, 0.2288), (0.4906, 0.176)) with { White = 0.6822, Edge = (0.8994, 1.8132), Sheen = (0.2748, 0.4418) }, 0.5058, 1.4228, -0.0024),
        ["dkEdge"] = new(ThreePt(0.004, 37.7727, (1.1409, 0.8348), (0.4994, 0.3451), (0.9825, 0.6205)) with { White = 1.5311, Edge = (0.9471, 1.8995) }, 1.0085, 2.33, -0.0008),
        ["softEdge"] = new(ThreePt(0.0067, 35.9382, (1.1347, 1.0315), (0.4926, 0.3954), (0.9787, 0.6396)) with { White = 1.3239, Edge = (0.8627, 3.4521), Sheen = (-0.0502, 0.2627) }, 1.0021, 2.6744, 0.0019),
        ["clear"] = new(ThreePt(0.0308, 0.5717, (0.3092, 0.4661), (0.1516, 0.2575), (0.3253, 0.253)) with { White = 0.4257, Sheen = (0.6767, -0.5) }, 0.3023, 1.2639, 0.0293),
        ["flat"] = new(ThreePt(0.9965, 52.4429, (0, 0.867), (0.0025, 0.374), (0.0257, 0.6807)) with { White = 1.1054, Edge = (0.8745, 3.6749) }, 0.0108, 2.487, 0.9964),
        ["softmetal"] = new(ThreePt(0.0098, 8.2035, (1.1305, 0.6404), (0.4998, 0.2708), (0.966, 0.517)) with { White = 0.9108, Tint = 0.8945 }, 0.9983, 1.8483, 0.005),
    };

    static Lighting ThreePt(double ambient, double shininess, (double Diffuse, double Specular) key, (double Diffuse, double Specular) fill,
        (double Diffuse, double Specular) back)
    {
        var lights = Lighting.Default.Lights;
        return Rig(ambient, shininess,
            L(lights[0].Direction.X, lights[0].Direction.Y, lights[0].Direction.Z, key.Diffuse, key.Specular),
            L(lights[1].Direction.X, lights[1].Direction.Y, lights[1].Direction.Z, fill.Diffuse, fill.Specular),
            L(lights[2].Direction.X, lights[2].Direction.Y, lights[2].Direction.Z, back.Diffuse, back.Specular));
    }

    /// <summary>The rig as a material reflects it (warmMatte, the default, leaves it unchanged).</summary>
    public static Lighting WithMaterial(Lighting rig, string? material)
    {
        if (material == null || !Materials.TryGetValue(material, out var m))
            return rig;
        if (ReferenceEquals(rig, Lighting.Default))
            return m.ThreePt;
        static (double R, double G, double B) Scale((double R, double G, double B) c, double f) => (c.R * f, c.G * f, c.B * f);
        return m.ThreePt with
        {
            Ambient = (rig.Ambient.R * m.Diffuse + m.Ambient, rig.Ambient.G * m.Diffuse + m.Ambient, rig.Ambient.B * m.Diffuse + m.Ambient),
            Lights = [.. rig.Lights.Select(l => l with { Diffuse = Scale(l.Diffuse, m.Diffuse), Specular = Scale(l.Specular, m.Specular) })],
        };
    }
}
