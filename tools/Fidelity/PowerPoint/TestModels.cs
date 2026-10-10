using System.Text;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace Fidelity.PowerPoint;

/// <summary>
/// Animated glTF test models (GLB) for the models deck, generated so that the corpus needs no external model files.
/// PowerPoint plays only skeletal animations (a model whose animations move plain nodes or morph targets gets none,
/// measured), so every model is skinned: a tilted box turning about its axis (three materials, a texture), a ball
/// bouncing beside a pole (cubic spline translation, scale), a bending arm (two joints), a textured blob turning while
/// its morph targets change and a cone with two animations (scenes).
/// </summary>
public static class TestModels
{
    public sealed record Files(string Turntable, string Bounce, string Arm, string Blob, string Scenes);

    public static Files Write(string directory)
    {
        Directory.CreateDirectory(directory);
        var files = new Files(Path.Combine(directory, "turntable.glb"), Path.Combine(directory, "bounce.glb"), Path.Combine(directory, "arm.glb"),
            Path.Combine(directory, "blob.glb"), Path.Combine(directory, "scenes.glb"));
        File.WriteAllBytes(files.Turntable, Turntable());
        File.WriteAllBytes(files.Bounce, Bounce());
        File.WriteAllBytes(files.Arm, Arm());
        File.WriteAllBytes(files.Blob, Blob());
        File.WriteAllBytes(files.Scenes, Scenes());
        return files;
    }

    /// <summary>A box tilted towards the viewer that turns once about its axis in 2 s (linear quaternion keys).</summary>
    static byte[] Turntable()
    {
        var g = new Gltf();
        var red = g.Material([0.8f, 0.1f, 0.1f, 1], 0, 0.5f);
        var green = g.Material([0.1f, 0.6f, 0.2f, 1], 0, 0.8f);
        var checker = g.Material([1, 1, 1, 1], 0, 0.6f, g.Texture(Checker()));
        var spin = g.Node(new JsonObject { ["name"] = "Spin" });
        var tilt = g.Node(new JsonObject { ["children"] = new JsonArray(spin), ["rotation"] = Floats(Quaternion(1, 0, 0, 25)), ["name"] = "Tilt" });
        var skin = g.Skin([tilt, spin], g.Accessor([.. Identity, .. Identity], "MAT4"));
        var mesh = g.Mesh(
            g.Bound(Box([(0, 0, 1), (0, 0, -1)]), red, 1),
            g.Bound(Box([(1, 0, 0), (-1, 0, 0)]), green, 1),
            g.Bound(Box([(0, 1, 0), (0, -1, 0)]), checker, 1));
        var box = g.Node(new JsonObject { ["mesh"] = mesh, ["skin"] = skin, ["name"] = "Box" });
        float[] times = [0, 0.5f, 1, 1.5f, 2];
        g.Animation("Spin", (spin, "rotation", "LINEAR", times, times.SelectMany(t => Quaternion(0, 1, 0, t * 180)).ToArray()));
        return g.Glb(g.Node(new JsonObject { ["children"] = new JsonArray(tilt, box), ["name"] = "Turntable" }));
    }

    /// <summary>A ball bouncing on a plate in 1.5 s: cubic spline translation, squashed (scale) where it lands.</summary>
    static byte[] Bounce()
    {
        var g = new Gltf();
        var cube = Box([(0, 0, 1), (0, 0, -1), (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0)]);
        var plate = g.Node(new JsonObject
        {
            ["mesh"] = g.Mesh(g.Primitive(cube, g.Material([0.5f, 0.5f, 0.55f, 1], 0, 0.9f))),
            ["scale"] = new JsonArray(1.6f, 0.08f, 1.0f), ["translation"] = new JsonArray(0f, -0.08f, 0f), ["name"] = "Plate",
        });
        // A pole beside the ball reaches as high as it jumps, so that the bounds of the model hold the whole bounce.
        var pole = g.Node(new JsonObject
        {
            ["mesh"] = g.Mesh(g.Primitive(cube, g.Material([0.35f, 0.25f, 0.2f, 1], 0, 0.7f))),
            ["scale"] = new JsonArray(0.08f, 2.4f, 0.08f), ["translation"] = new JsonArray(1.0f, 1.2f, 0f), ["name"] = "Pole",
        });
        var sphere = Sphere(48, 32);
        var ballGeometry = sphere with { Positions = sphere.Positions.Select(v => v * 0.4f).ToArray() };
        var joint = g.Node(new JsonObject { ["translation"] = new JsonArray(0f, 0.4f, 0f), ["name"] = "Center" });
        var skin = g.Skin([joint], g.Accessor(Identity, "MAT4"));
        var ball = g.Node(new JsonObject { ["mesh"] = g.Mesh(g.Bound(ballGeometry, g.Material([0.95f, 0.45f, 0.05f, 1], 0, 0.35f), 0)), ["skin"] = skin, ["name"] = "Ball" });
        // Cubic spline keys: in-tangent, value, out-tangent per key (glTF 2.0, 3.11).
        float[] times = [0, 0.75f, 1.5f];
        float[] translation =
        [
            0, 0, 0, 0, 0.4f, 0, 0, 4.4f, 0,
            0, 0, 0, 0, 2.0f, 0, 0, 0, 0,
            0, -4.4f, 0, 0, 0.4f, 0, 0, 0, 0,
        ];
        float[] squashTimes = [0, 0.1f, 1.4f, 1.5f];
        float[] squash = [1.2f, 0.8f, 1.2f, 1, 1, 1, 1, 1, 1, 1.2f, 0.8f, 1.2f];
        g.Animation("Bounce", (joint, "translation", "CUBICSPLINE", times, translation), (joint, "scale", "LINEAR", squashTimes, squash));
        return g.Glb(g.Node(new JsonObject { ["children"] = new JsonArray(plate, pole, joint, ball), ["name"] = "Bounce" }));
    }

    /// <summary>A two-bone arm (a cylinder skinned to two joints) that bends and swings in 2 s.</summary>
    static byte[] Arm()
    {
        var g = new Gltf();
        const int Rings = 24, Sides = 24;
        var positions = new List<float>();
        var normals = new List<float>();
        var joints = new List<ushort>();
        var weights = new List<float>();
        for (var r = 0; r <= Rings; r++)
        {
            var y = 2f * r / Rings;
            // Below 0.8 the lower joint, above 1.2 the upper, blended between.
            var upper = Math.Clamp((y - 0.8f) / 0.4f, 0, 1);
            for (var s = 0; s <= Sides; s++)
            {
                var a = 2 * Math.PI * s / Sides;
                positions.AddRange([0.25f * (float)Math.Cos(a), y, 0.25f * (float)Math.Sin(a)]);
                normals.AddRange([(float)Math.Cos(a), 0, (float)Math.Sin(a)]);
                joints.AddRange([0, 1, 0, 0]);
                weights.AddRange([1 - upper, upper, 0, 0]);
            }
        }
        var indices = new List<uint>();
        for (var r = 0; r < Rings; r++)
            for (var s = 0; s < Sides; s++)
            {
                uint a = (uint)(r * (Sides + 1) + s), b = (uint)((r + 1) * (Sides + 1) + s);
                indices.AddRange([a, b, a + 1, a + 1, b, b + 1]);
            }
        var primitive = g.Primitive(new Geometry(positions.ToArray(), normals.ToArray(), null, indices.ToArray()), g.Material([0.15f, 0.35f, 0.85f, 1], 0, 0.6f));
        primitive["attributes"]!["JOINTS_0"] = g.Accessor(joints.ToArray(), "VEC4");
        primitive["attributes"]!["WEIGHTS_0"] = g.Accessor(weights.ToArray(), "VEC4");
        var upperJoint = g.Node(new JsonObject { ["translation"] = new JsonArray(0f, 1f, 0f), ["name"] = "Upper" });
        var lowerJoint = g.Node(new JsonObject { ["children"] = new JsonArray(upperJoint), ["name"] = "Lower" });
        // Inverse bind matrices: the joints rest at y = 0 and y = 1.
        float[] inverse = [.. Identity, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, -1, 0, 1];
        var skin = g.Skin([lowerJoint, upperJoint], g.Accessor(inverse, "MAT4"));
        var body = g.Node(new JsonObject { ["mesh"] = g.Mesh(primitive), ["skin"] = skin, ["name"] = "Arm" });
        float[] times = [0, 0.5f, 1, 1.5f, 2];
        g.Animation("Wave",
            (upperJoint, "rotation", "LINEAR", times, new[] { 0f, 50, 0, -50, 0 }.SelectMany(d => Quaternion(0, 0, 1, d)).ToArray()),
            (lowerJoint, "rotation", "LINEAR", times, new[] { 0f, 15, 30, 15, 0 }.SelectMany(d => Quaternion(0, 1, 0, d)).ToArray()));
        return g.Glb(g.Node(new JsonObject { ["children"] = new JsonArray(lowerJoint, body), ["name"] = "Rig" }));
    }

    /// <summary>A textured sphere turning on a joint while the weights of its two morph targets (stretched, bulging) change, in 2 s.</summary>
    static byte[] Blob()
    {
        var g = new Gltf();
        var sphere = Sphere(48, 32);
        var stretch = new float[sphere.Positions.Length];
        var bulge = new float[sphere.Positions.Length];
        for (var i = 0; i < sphere.Positions.Length; i += 3)
        {
            stretch[i + 1] = sphere.Positions[i + 1] * 0.6f;
            bulge[i] = sphere.Positions[i] * 0.5f * (1 - Math.Abs(sphere.Positions[i + 1]));
            bulge[i + 2] = sphere.Positions[i + 2] * 0.5f * (1 - Math.Abs(sphere.Positions[i + 1]));
        }
        var joint = g.Node(new JsonObject { ["name"] = "Root" });
        var skin = g.Skin([joint], g.Accessor(Identity, "MAT4"));
        var primitive = g.Bound(sphere, g.Material([0.2f, 0.7f, 0.35f, 1], 0, 0.5f), 0, g.Texture(Checker()));
        primitive["targets"] = new JsonArray(new JsonObject { ["POSITION"] = g.Accessor(stretch, "VEC3", minMax: true) },
            new JsonObject { ["POSITION"] = g.Accessor(bulge, "VEC3", minMax: true) });
        var mesh = g.Mesh(primitive);
        g.Meshes[mesh]!["weights"] = new JsonArray(0f, 0f);
        var blob = g.Node(new JsonObject { ["mesh"] = mesh, ["skin"] = skin, ["name"] = "Blob" });
        float[] times = [0, 0.7f, 1.4f, 2];
        g.Animation("Morph",
            (joint, "rotation", "LINEAR", times, new[] { 0f, 30, 60, 90 }.SelectMany(d => Quaternion(0, 1, 0, d)).ToArray()),
            (blob, "weights", "LINEAR", times, [0, 0, 1, 0, 0, 1, 0, 0]));
        return g.Glb(g.Node(new JsonObject { ["children"] = new JsonArray(joint, blob), ["name"] = "Rig" }));
    }

    /// <summary>A cone on a joint with two animations: Nod (about x, 1.2 s) and Shake (about y, 1.6 s).</summary>
    static byte[] Scenes()
    {
        var g = new Gltf();
        var joint = g.Node(new JsonObject { ["name"] = "Head" });
        var skin = g.Skin([joint], g.Accessor(Identity, "MAT4"));
        var cone = g.Node(new JsonObject { ["mesh"] = g.Mesh(g.Bound(Cone(32), g.Material([0.85f, 0.75f, 0.2f, 1], 0.6f, 0.4f), 0)), ["skin"] = skin, ["name"] = "Cone" });
        float[] nod = [0, 0.3f, 0.6f, 0.9f, 1.2f], shake = [0, 0.4f, 0.8f, 1.2f, 1.6f];
        g.Animation("Nod", (joint, "rotation", "LINEAR", nod, new[] { 0f, 30, 0, -30, 0 }.SelectMany(d => Quaternion(1, 0, 0, d)).ToArray()));
        g.Animation("Shake", (joint, "rotation", "LINEAR", shake, new[] { 0f, 40, 0, -40, 0 }.SelectMany(d => Quaternion(0, 1, 0, d)).ToArray()));
        return g.Glb(g.Node(new JsonObject { ["children"] = new JsonArray(joint, cone), ["name"] = "Rig" }));
    }

    static readonly float[] Identity = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

    // ------------------------------------------------------------------------------------------------ geometry

    sealed record Geometry(float[] Positions, float[] Normals, float[]? Uvs, uint[] Indices);

    /// <summary>The faces of a cube (-0.5 … 0.5) with the given outward normals, with texture coordinates.</summary>
    static Geometry Box((int X, int Y, int Z)[] faces)
    {
        var positions = new List<float>();
        var normals = new List<float>();
        var uvs = new List<float>();
        var indices = new List<uint>();
        foreach (var (x, y, z) in faces)
        {
            float[] n = [x, y, z];
            // Two axes across the face, so that u × v points along the normal.
            float[] u = x != 0 ? [0, 0, -x] : y != 0 ? [1, 0, 0] : [z, 0, 0];
            float[] v = [n[1] * u[2] - n[2] * u[1], n[2] * u[0] - n[0] * u[2], n[0] * u[1] - n[1] * u[0]];
            var start = (uint)(positions.Count / 3);
            foreach (var (su, sv) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                for (var k = 0; k < 3; k++)
                    positions.Add(0.5f * (n[k] + su * u[k] + sv * v[k]));
                normals.AddRange(n);
                uvs.AddRange([(su + 1) / 2f, (1 - sv) / 2f]);
            }
            indices.AddRange([start, start + 1, start + 2, start, start + 2, start + 3]);
        }
        return new Geometry(positions.ToArray(), normals.ToArray(), uvs.ToArray(), indices.ToArray());
    }

    static Geometry Sphere(int segments, int rings)
    {
        var positions = new List<float>();
        var uvs = new List<float>();
        var indices = new List<uint>();
        for (var r = 0; r <= rings; r++)
        {
            var theta = Math.PI * r / rings;
            for (var s = 0; s <= segments; s++)
            {
                var phi = 2 * Math.PI * s / segments;
                positions.AddRange([(float)(Math.Sin(theta) * Math.Sin(phi)), (float)Math.Cos(theta), (float)(Math.Sin(theta) * Math.Cos(phi))]);
                uvs.AddRange([(float)s / segments, (float)r / rings]);
            }
        }
        for (var r = 0; r < rings; r++)
            for (var s = 0; s < segments; s++)
            {
                uint a = (uint)(r * (segments + 1) + s), b = (uint)((r + 1) * (segments + 1) + s);
                indices.AddRange([a, b, a + 1, a + 1, b, b + 1]);
            }
        return new Geometry(positions.ToArray(), positions.ToArray(), uvs.ToArray(), indices.ToArray());
    }

    /// <summary>A cone (base radius 0.5 at y = -0.5, tip at y = 0.7) with its base.</summary>
    static Geometry Cone(int sides)
    {
        var positions = new List<float>();
        var normals = new List<float>();
        var indices = new List<uint>();
        var slope = 0.5 / 1.2;
        for (var s = 0; s < sides; s++)
        {
            double a0 = 2 * Math.PI * s / sides, a1 = 2 * Math.PI * (s + 1) / sides, am = (a0 + a1) / 2;
            var start = (uint)(positions.Count / 3);
            positions.AddRange([0, 0.7f, 0, 0.5f * (float)Math.Sin(a1), -0.5f, 0.5f * (float)Math.Cos(a1), 0.5f * (float)Math.Sin(a0), -0.5f, 0.5f * (float)Math.Cos(a0)]);
            var length = Math.Sqrt(1 + slope * slope);
            foreach (var a in new[] { am, a1, a0 })
                normals.AddRange([(float)(Math.Sin(a) / length), (float)(slope / length), (float)(Math.Cos(a) / length)]);
            indices.AddRange([start, start + 2, start + 1]);
            start = (uint)(positions.Count / 3);
            positions.AddRange([0, -0.5f, 0, 0.5f * (float)Math.Sin(a0), -0.5f, 0.5f * (float)Math.Cos(a0), 0.5f * (float)Math.Sin(a1), -0.5f, 0.5f * (float)Math.Cos(a1)]);
            normals.AddRange([0, -1, 0, 0, -1, 0, 0, -1, 0]);
            indices.AddRange([start, start + 2, start + 1]);
        }
        // Counter-clockwise seen from outside.
        var ordered = new List<uint>();
        for (var i = 0; i < indices.Count; i += 3)
        {
            var (a, b, c) = (indices[i], indices[i + 1], indices[i + 2]);
            float[] p(uint k) => [positions[(int)k * 3], positions[(int)k * 3 + 1], positions[(int)k * 3 + 2]];
            var (pa, pb, pc) = (p(a), p(b), p(c));
            float[] e1 = [pb[0] - pa[0], pb[1] - pa[1], pb[2] - pa[2]], e2 = [pc[0] - pa[0], pc[1] - pa[1], pc[2] - pa[2]];
            float[] cross = [e1[1] * e2[2] - e1[2] * e2[1], e1[2] * e2[0] - e1[0] * e2[2], e1[0] * e2[1] - e1[1] * e2[0]];
            var n = (int)a * 3;
            ordered.AddRange(cross[0] * normals[n] + cross[1] * normals[n + 1] + cross[2] * normals[n + 2] < 0 ? [a, c, b] : [a, b, c]);
        }
        return new Geometry(positions.ToArray(), normals.ToArray(), null, ordered.ToArray());
    }

    /// <summary>A rotation about an axis by degrees as a quaternion (x, y, z, w).</summary>
    static float[] Quaternion(float x, float y, float z, float degrees)
    {
        var half = degrees * Math.PI / 360;
        var s = (float)Math.Sin(half);
        return [x * s, y * s, z * s, (float)Math.Cos(half)];
    }

    static JsonArray Floats(float[] values) => new(values.Select(v => (JsonNode?)v).ToArray());

    /// <summary>A checkerboard texture (PNG) with a colored corner, so that its orientation shows.</summary>
    static byte[] Checker()
    {
        using var surface = SKSurface.Create(new SKImageInfo(64, 64));
        var canvas = surface.Canvas;
        using var light = new SKPaint { Color = new SKColor(0xF2, 0xF2, 0xF2) };
        using var dark = new SKPaint { Color = new SKColor(0x30, 0x40, 0x70) };
        using var mark = new SKPaint { Color = new SKColor(0xE0, 0x80, 0x10) };
        for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
                canvas.DrawRect(x * 8, y * 8, 8, 8, (x + y) % 2 == 0 ? light : dark);
        canvas.DrawRect(0, 0, 16, 16, mark);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // ------------------------------------------------------------------------------------------------ the file

    /// <summary>A glTF 2.0 document with one binary buffer, written as GLB.</summary>
    sealed class Gltf
    {
        readonly MemoryStream binary = new();
        readonly JsonArray bufferViews = [], accessors = [], materials = [], nodes = [], animations = [], skins = [], images = [], textures = [];
        public JsonArray Meshes { get; } = [];

        int View(byte[] data, int? target)
        {
            while (binary.Length % 4 != 0)
                binary.WriteByte(0);
            var view = new JsonObject { ["buffer"] = 0, ["byteOffset"] = binary.Length, ["byteLength"] = data.Length };
            if (target != null)
                view["target"] = target;
            binary.Write(data);
            bufferViews.Add(view);
            return bufferViews.Count - 1;
        }

        public int Accessor(float[] data, string type, bool minMax = false, int? target = 34962)
        {
            var bytes = new byte[data.Length * 4];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            var size = type switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, _ => 16 };
            var accessor = new JsonObject { ["bufferView"] = View(bytes, type == "MAT4" ? null : target), ["componentType"] = 5126, ["count"] = data.Length / size, ["type"] = type };
            if (minMax)
            {
                accessor["min"] = Floats(Enumerable.Range(0, size).Select(c => Enumerable.Range(0, data.Length / size).Min(i => data[i * size + c])).ToArray());
                accessor["max"] = Floats(Enumerable.Range(0, size).Select(c => Enumerable.Range(0, data.Length / size).Max(i => data[i * size + c])).ToArray());
            }
            accessors.Add(accessor);
            return accessors.Count - 1;
        }

        public int Accessor(ushort[] data, string type)
        {
            var bytes = new byte[data.Length * 2];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            accessors.Add(new JsonObject { ["bufferView"] = View(bytes, 34962), ["componentType"] = 5123, ["count"] = data.Length / 4, ["type"] = type });
            return accessors.Count - 1;
        }

        int Indices(uint[] data)
        {
            var bytes = new byte[data.Length * 4];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            accessors.Add(new JsonObject { ["bufferView"] = View(bytes, 34963), ["componentType"] = 5125, ["count"] = data.Length, ["type"] = "SCALAR" });
            return accessors.Count - 1;
        }

        /// <summary>Keyframe times (seconds), with their range as glTF requires.</summary>
        int Times(float[] times)
        {
            var index = Accessor(times, "SCALAR", target: null);
            accessors[index]!["min"] = new JsonArray(times.Min());
            accessors[index]!["max"] = new JsonArray(times.Max());
            return index;
        }

        public JsonObject Primitive(Geometry geometry, int material)
        {
            var attributes = new JsonObject { ["POSITION"] = Accessor(geometry.Positions, "VEC3", minMax: true), ["NORMAL"] = Accessor(geometry.Normals, "VEC3") };
            if (geometry.Uvs != null)
                attributes["TEXCOORD_0"] = Accessor(geometry.Uvs, "VEC2");
            return new JsonObject { ["attributes"] = attributes, ["indices"] = Indices(geometry.Indices), ["material"] = material };
        }

        /// <summary>A primitive whose vertices all follow one joint of the skin.</summary>
        public JsonObject Bound(Geometry geometry, int material, ushort joint, int? texture = null)
        {
            if (texture != null)
                materials[material]!["pbrMetallicRoughness"]!["baseColorTexture"] = new JsonObject { ["index"] = texture };
            var primitive = Primitive(geometry, material);
            var count = geometry.Positions.Length / 3;
            primitive["attributes"]!["JOINTS_0"] = Accessor(Enumerable.Range(0, count).SelectMany(_ => new ushort[] { joint, 0, 0, 0 }).ToArray(), "VEC4");
            primitive["attributes"]!["WEIGHTS_0"] = Accessor(Enumerable.Range(0, count).SelectMany(_ => new[] { 1f, 0, 0, 0 }).ToArray(), "VEC4");
            return primitive;
        }

        public int Mesh(params JsonObject[] primitives)
        {
            Meshes.Add(new JsonObject { ["primitives"] = new JsonArray(primitives.Select(p => (JsonNode?)p).ToArray()) });
            return Meshes.Count - 1;
        }

        public int Material(float[] color, float metallic, float roughness, int? texture = null)
        {
            var pbr = new JsonObject { ["baseColorFactor"] = Floats(color), ["metallicFactor"] = metallic, ["roughnessFactor"] = roughness };
            if (texture != null)
                pbr["baseColorTexture"] = new JsonObject { ["index"] = texture };
            materials.Add(new JsonObject { ["pbrMetallicRoughness"] = pbr });
            return materials.Count - 1;
        }

        public int Texture(byte[] png)
        {
            images.Add(new JsonObject { ["bufferView"] = View(png, null), ["mimeType"] = "image/png" });
            textures.Add(new JsonObject { ["source"] = images.Count - 1 });
            return textures.Count - 1;
        }

        public int Node(JsonObject node)
        {
            nodes.Add(node);
            return nodes.Count - 1;
        }

        public int Skin(int[] joints, int inverseBindMatrices)
        {
            skins.Add(new JsonObject { ["joints"] = new JsonArray(joints.Select(j => (JsonNode?)j).ToArray()), ["inverseBindMatrices"] = inverseBindMatrices });
            return skins.Count - 1;
        }

        public void Animation(string name, params (int Node, string Path, string Interpolation, float[] Times, float[] Values)[] channels)
        {
            var samplers = new JsonArray();
            var targets = new JsonArray();
            foreach (var (node, path, interpolation, times, values) in channels)
            {
                var size = values.Length / times.Length / (interpolation == "CUBICSPLINE" ? 3 : 1);
                samplers.Add(new JsonObject
                {
                    ["input"] = Times(times), ["interpolation"] = interpolation,
                    ["output"] = Accessor(values, size switch { 3 => "VEC3", 4 => "VEC4", _ => "SCALAR" }, target: null),
                });
                targets.Add(new JsonObject { ["sampler"] = samplers.Count - 1, ["target"] = new JsonObject { ["node"] = node, ["path"] = path } });
            }
            animations.Add(new JsonObject { ["name"] = name, ["samplers"] = samplers, ["channels"] = targets });
        }

        public byte[] Glb(int root)
        {
            while (binary.Length % 4 != 0)
                binary.WriteByte(0);
            var json = new JsonObject
            {
                ["asset"] = new JsonObject { ["version"] = "2.0", ["generator"] = "FreeSlate Fidelity test models" },
                ["scene"] = 0, ["scenes"] = new JsonArray(new JsonObject { ["nodes"] = new JsonArray(root) }),
                ["nodes"] = nodes, ["meshes"] = Meshes, ["materials"] = materials, ["accessors"] = accessors, ["bufferViews"] = bufferViews,
                ["buffers"] = new JsonArray(new JsonObject { ["byteLength"] = binary.Length }),
            };
            if (animations.Count > 0) json["animations"] = animations;
            if (skins.Count > 0) json["skins"] = skins;
            if (images.Count > 0) { json["images"] = images; json["textures"] = textures; }
            var text = Encoding.UTF8.GetBytes(json.ToJsonString());
            var padded = (text.Length + 3) / 4 * 4;
            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);
            writer.Write(0x46546C67u);   // glTF
            writer.Write(2u);
            writer.Write((uint)(12 + 8 + padded + 8 + binary.Length));
            writer.Write((uint)padded);
            writer.Write(0x4E4F534Au);   // JSON
            writer.Write(text);
            for (var i = text.Length; i < padded; i++)
                writer.Write((byte)' ');
            writer.Write((uint)binary.Length);
            writer.Write(0x004E4942u);   // BIN
            writer.Write(binary.ToArray());
            writer.Flush();
            return output.ToArray();
        }
    }
}
