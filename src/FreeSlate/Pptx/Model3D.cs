using System.Net;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using FreeSlate.Drawing;

namespace FreeSlate.Pptx;

/// <summary>
/// A 3D model (am3d:model3d, catalog: picture.model3d and animation.pictures) for the player's WebGL renderer
/// (model3d.js): a canvas over PowerPoint's picture of the model with the glTF file and the slide's view of it: the
/// camera, the model's transform, the viewport and the lights, its embedded animation and poster frame, in the file's
/// units (EMU, 60000ths of a degree, ratios as numbers, colors linear).
/// </summary>
static class Model3D
{
    public static readonly XNamespace Am3d = "http://schemas.microsoft.com/office/drawing/2017/model3d";
    static readonly XNamespace A3dAnim = "http://schemas.microsoft.com/office/drawing/2018/animation/model3d";

    /// <summary>The canvas of a picture that stands for a 3D model (its fallback), or "" for other pictures.</summary>
    public static string Canvas(XElement pic, string part, SlideContext slide)
    {
        var model = pic.Element(Opc.MarkupCompatibility.Annotation + "model3d")?.Element(Am3d + "model3d");
        var package = slide.Presentation.Package;
        if (model?.Attribute(Ns.R + "embed")?.Value is not { Length: > 0 } id || package.Relationship(part, id) is not { External: false } rel
            || !package.Exists(rel.Target))
            return "";
        var ext = model.Element(Am3d + "spPr")?.Element(Ns.A + "xfrm")?.Element(Ns.A + "ext")
                  ?? pic.Element(Ns.P + "spPr")?.Element(Ns.A + "xfrm")?.Element(Ns.A + "ext");
        if (ext.Long("cx") is not > 0 || ext.Long("cy") is not > 0)
            return "";
        slide.Presentation.HasModels = true;
        var spec = Spec(model, ext.Long("cx")!.Value, ext.Long("cy")!.Value, slide.Colors);
        return $"<canvas class=\"fs-model\" data-model=\"{WebUtility.HtmlEncode(spec.ToJsonString())}\" " +
               $"data-glb=\"{Convert.ToBase64String(package.ReadBytes(rel.Target))}\"></canvas>";
    }

    public static JsonObject Spec(XElement model, long width, long height, ColorContext colors)
    {
        var spec = new JsonObject { ["frame"] = new JsonObject { ["w"] = width, ["h"] = height } };
        var camera = model.Element(Am3d + "camera");
        var perspective = camera?.Element(Am3d + "perspective");
        var orthographic = camera?.Element(Am3d + "orthographic");
        spec["camera"] = new JsonObject
        {
            ["pos"] = Vector(camera?.Element(Am3d + "pos"), "x", "y", "z", [0, 0, 36000000]),
            ["up"] = Vector(camera?.Element(Am3d + "up"), "dx", "dy", "dz", [0, 1, 0]),
            ["lookAt"] = Vector(camera?.Element(Am3d + "lookAt"), "x", "y", "z", [0, 0, 0]),
            ["fov"] = orthographic == null ? perspective.Long("fov") ?? 2700000 : null,
            ["ortho"] = orthographic != null ? Ratio(orthographic.Element(Am3d + "sz")) ?? 1 : null,
        };
        var trans = model.Element(Am3d + "trans");
        var transform = new JsonObject();
        if (Ratio(trans?.Element(Am3d + "meterPerModelUnit")) is { } meters)
            transform["mpu"] = meters;
        if (trans?.Element(Am3d + "preTrans") is { } pre)
            transform["pre"] = Vector(pre, "dx", "dy", "dz", [0, 0, 0]);
        if (trans?.Element(Am3d + "scale") is { } scale)
            transform["scale"] = new JsonArray(Ratio(scale.Element(Am3d + "sx")) ?? 1, Ratio(scale.Element(Am3d + "sy")) ?? 1, Ratio(scale.Element(Am3d + "sz")) ?? 1);
        if (trans?.Element(Am3d + "rot") is { } rot)
            transform["rot"] = Vector(rot, "ax", "ay", "az", [0, 0, 0]);
        if (trans?.Element(Am3d + "postTrans") is { } post)
            transform["post"] = Vector(post, "dx", "dy", "dz", [0, 0, 0]);
        spec["trans"] = transform;
        // An object viewport has its size; a window viewport is the frame, its field of view across the height.
        spec["viewport"] = model.Element(Am3d + "objViewport").Long("viewportSz") is { } viewport ? viewport : height;
        if (model.Element(Am3d + "ambientLight") is { } ambient)
            spec["ambient"] = new JsonObject
            {
                ["color"] = Color(ambient, colors), ["lux"] = Ratio(ambient.Element(Am3d + "illuminance")) ?? 0, ["enabled"] = ambient.Bool("enabled") != false,
            };
        var lights = new JsonArray();
        foreach (var light in model.Elements())
        {
            var on = light.Bool("enabled") != false;
            switch (light.Name.LocalName)
            {
                case "ptLight":
                    lights.Add(new JsonObject
                    {
                        ["type"] = "point", ["color"] = Color(light, colors), ["cd"] = Ratio(light.Element(Am3d + "intensity")) ?? 0,
                        ["pos"] = Vector(light.Element(Am3d + "pos"), "x", "y", "z", [0, 0, 0]), ["enabled"] = on,
                    });
                    break;
                case "spotLight":
                    lights.Add(new JsonObject
                    {
                        ["type"] = "spot", ["color"] = Color(light, colors), ["cd"] = Ratio(light.Element(Am3d + "intensity")) ?? 0,
                        ["pos"] = Vector(light.Element(Am3d + "pos"), "x", "y", "z", [0, 0, 0]),
                        ["lookAt"] = Vector(light.Element(Am3d + "lookAt"), "x", "y", "z", [0, 0, 0]), ["angle"] = light.Long("spotAng") ?? 2700000, ["enabled"] = on,
                    });
                    break;
                case "dirLight":
                    lights.Add(new JsonObject
                    {
                        ["type"] = "dir", ["color"] = Color(light, colors), ["lux"] = Ratio(light.Element(Am3d + "illuminance")) ?? 0,
                        ["pos"] = Vector(light.Element(Am3d + "pos"), "x", "y", "z", [0, 0, 0]),
                        ["lookAt"] = Vector(light.Element(Am3d + "lookAt"), "x", "y", "z", [0, 0, 0]), ["enabled"] = on,
                    });
                    break;
            }
        }
        spec["lights"] = lights;
        var extensions = model.Element(Am3d + "extLst")?.Elements(Ns.A + "ext").SelectMany(e => e.Elements()).ToList() ?? [];
        if (extensions.FirstOrDefault(e => e.Name == A3dAnim + "embedAnim") is { } embedded && embedded.Element(A3dAnim + "animPr") is { } properties)
            spec["anim"] = new JsonObject
            {
                ["id"] = embedded.Long("animId") ?? 0, ["length"] = properties.Long("length") ?? 0, ["count"] = properties.Str("count") ?? "1",
                ["auto"] = properties.Bool("auto") != false,
            };
        if (extensions.FirstOrDefault(e => e.Name == A3dAnim + "posterFrame") is { } poster)
            spec["poster"] = new JsonObject { ["id"] = poster.Long("animId") ?? 0, ["frame"] = (poster.Long("frame") ?? 0) / 100000.0 };
        return spec;
    }

    static JsonArray Vector(XElement? element, string x, string y, string z, long[] fallback) =>
        new(element.Long(x) ?? (element == null ? fallback[0] : 0), element.Long(y) ?? (element == null ? fallback[1] : 0), element.Long(z) ?? (element == null ? fallback[2] : 0));

    static double? Ratio(XElement? element) =>
        element.Long("n") is { } n && element.Long("d") is { } d && d != 0 ? (double)n / d : null;

    /// <summary>A light's color (am3d:clr), linear like PowerPoint's scRGB.</summary>
    static JsonArray Color(XElement light, ColorContext colors)
    {
        var color = (colors.ResolveChild(light.Element(Am3d + "clr")) ?? RgbColor.White).ToLinear();
        return new JsonArray(Math.Round(color.R, 6), Math.Round(color.G, 6), Math.Round(color.B, 6));
    }
}
