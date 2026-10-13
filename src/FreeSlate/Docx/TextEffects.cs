using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>
/// Word's text effects (catalog: run.textEffects): the w14 glow, shadow, reflection, outline and fill of runs as their
/// DrawingML counterparts, which the effect renderer shared with PowerPoint draws, and the room they take in the line.
/// </summary>
public static class TextEffects
{
    /// <summary>
    /// A w14 element as DrawingML: the same names in the a namespace (w14:shadow is a:outerShdw, w14:textOutline a:ln),
    /// the attributes without namespace; a color's w14:alpha is its transparency, a:alpha its opacity (measured with
    /// docx/local/w14alpha: fills of w14:alpha 20000, 50000 and 80000 print at 80, 50 and 20 % opacity;
    /// docx/external/libreoffice/tdf152896_WordArt_color_darken).
    /// </summary>
    public static XElement DrawingML(XElement w14)
    {
        var name = w14.Name.LocalName switch { "shadow" => "outerShdw", "textOutline" => "ln", var local => local };
        var result = new XElement(Ns.A + name);
        foreach (var attribute in w14.Attributes().Where(a => a.Name.Namespace == Wml.W14))
            result.SetAttributeValue(attribute.Name.LocalName, attribute.Value);
        if (name == "alpha" && double.TryParse((string?)result.Attribute("val"), NumberStyles.Float, CultureInfo.InvariantCulture, out var transparency))
            result.SetAttributeValue("val", (100000 - transparency).ToString(CultureInfo.InvariantCulture));
        foreach (var child in w14.Elements().Where(e => e.Name.Namespace == Wml.W14))
            result.Add(DrawingML(child));
        return result;
    }

    /// <summary>The run's glow, shadow and reflection as an a:effectLst, or null.</summary>
    public static XElement? EffectList(RunFormat format)
    {
        var list = new XElement(Ns.A + "effectLst");
        foreach (var effect in new[] { format.Glow, format.TextShadow, format.Reflection })
        {
            if (effect != null)
                list.Add(DrawingML(effect));
        }
        return list.HasElements ? list : null;
    }

    /// <summary>
    /// The run's 3D format as the scene and body PowerPoint's 3D text takes (w14:scene3d as a:scene3d, w14:props3d as
    /// a:sp3d; catalog: run.textEffects): null unless it has a body (a bevel, depth or contour). Without a scene of its
    /// own the text faces the viewer under three lights from the top.
    /// </summary>
    public static (XElement Scene, XElement Shape)? Solid(RunFormat format)
    {
        if (format.Props3D is not { } props)
            return null;
        double Emu(string name) => double.TryParse((string?)props.Attribute(Wml.W14 + name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
        var body = props.Element(Wml.W14 + "bevelT") != null || props.Element(Wml.W14 + "bevelB") != null || Emu("extrusionH") > 0 || Emu("contourW") > 0;
        if (!body)
            return null;
        var shape = DrawingML(props);
        shape.Name = Ns.A + "sp3d";
        // Text faces the viewer whatever camera the scene names (measured: a perspective camera draws as the front
        // view); the light rig stays.
        var light = format.Scene3D?.Element(Wml.W14 + "lightRig") is { } rig ? DrawingML(rig)
            : new XElement(Ns.A + "lightRig", new XAttribute("rig", "threePt"), new XAttribute("dir", "t"));
        var scene = new XElement(Ns.A + "scene3d", new XElement(Ns.A + "camera", new XAttribute("prst", "orthographicFront")), light);
        return (scene, shape);
    }

    /// <summary>The run's outline (w14:textOutline) as an a:ln, or null.</summary>
    public static XElement? Outline(RunFormat format) => format.TextOutline is { } outline ? DrawingML(outline) : null;

    /// <summary>The run's fill (w14:textFill: a solid, gradient or no fill) as its DrawingML element, or null.</summary>
    public static XElement? Fill(RunFormat format) =>
        format.TextFill?.Elements().FirstOrDefault(e => e.Name.Namespace == Wml.W14) is { } fill ? DrawingML(fill) : null;

    /// <summary>
    /// How far the effects reach above and below the text, which the line makes room for outside its spacing
    /// (measured with docx/local/text-effects*, Arial at 12 to 96 pt, within 0.15 pt): a glow 0.778 of its radius, a
    /// shadow 1.046 of its blur plus or minus its vertical offset, both at the outer scale of text effects
    /// ((size / 78)^0.695, as PowerPoint sizes them); a reflection below the text the size times 0.944 of its end
    /// position less 0.21, and 0.0566 of the size for every point of its distance.
    /// </summary>
    public static (double Above, double Below) Reach(XElement? effects, double size)
    {
        if (effects == null)
            return (0, 0);
        var scale = Html.SvgEffects.TextScale.Of(size).Outer;
        double Emu(XElement e, string name) => double.TryParse((string?)e.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v / 12700 : 0;
        double above = 0, below = 0;
        if (effects.Element(Ns.A + "glow") is { } glow)
        {
            var reach = 0.778 * Emu(glow, "rad") * scale;
            (above, below) = (Math.Max(above, reach), Math.Max(below, reach));
        }
        if (effects.Element(Ns.A + "outerShdw") is { } shadow)
        {
            var blur = Emu(shadow, "blurRad");
            var direction = (double.TryParse((string?)shadow.Attribute("dir"), NumberStyles.Float, CultureInfo.InvariantCulture, out var dir) ? dir : 0) / 60000 * Math.PI / 180;
            var down = Emu(shadow, "dist") * Math.Sin(direction);
            above = Math.Max(above, 1.046 * scale * (blur - down));
            below = Math.Max(below, 1.046 * scale * (blur + down));
        }
        if (effects.Element(Ns.A + "reflection") is { } reflection)
        {
            var end = (double.TryParse((string?)reflection.Attribute("endPos"), NumberStyles.Float, CultureInfo.InvariantCulture, out var e) ? e : 100000) / 100000;
            below = Math.Max(below, Math.Max(0, size * (0.944 * end - 0.21)) + 0.0566 * size * Emu(reflection, "dist"));
        }
        return (Math.Max(0, above), Math.Max(0, below));
    }
}
