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
    /// the attributes without namespace.
    /// </summary>
    public static XElement DrawingML(XElement w14)
    {
        var name = w14.Name.LocalName switch { "shadow" => "outerShdw", "textOutline" => "ln", var local => local };
        var result = new XElement(Ns.A + name);
        foreach (var attribute in w14.Attributes().Where(a => a.Name.Namespace == Wml.W14))
            result.SetAttributeValue(attribute.Name.LocalName, attribute.Value);
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
