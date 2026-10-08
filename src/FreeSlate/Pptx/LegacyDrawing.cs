using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FreeSlate.Pptx;

/// <summary>
/// Legacy VML drawings (catalog: legacy.vml). PowerPoint 2007 saved the pictures of OLE objects and ActiveX controls
/// only in a VML drawing part of the slide: p:oleObj and p:control name their VML shape (spid), whose v:imagedata
/// holds the picture; later versions add a DrawingML picture (p:pic) as well. PowerPoint draws the VML picture when
/// there is no other (measured), the control at the place the VML shape's style gives.
/// </summary>
public static partial class LegacyDrawing
{
    static readonly XNamespace V = "urn:schemas-microsoft-com:vml";
    static readonly XNamespace O = "urn:schemas-microsoft-com:office:office";

    /// <summary>The picture of a legacy shape: the VML part, the image's relationship id in it, its crop (fractions) and box.</summary>
    public sealed record Picture(string Part, string ImageId, (double L, double T, double R, double B) Crop, (double X, double Y, double W, double H)? Box);

    /// <summary>The picture of VML shape <paramref name="spid"/> in the VML drawings of <paramref name="slidePart"/>, or null.</summary>
    public static Picture? Find(PresentationContext presentation, string slidePart, string? spid)
    {
        if (string.IsNullOrEmpty(spid))
            return null;
        var package = presentation.Package;
        foreach (var rel in package.Relationships(slidePart).Where(r => r.Is("vmlDrawing") && !r.External && package.Exists(r.Target)))
        {
            var drawing = Load(presentation, rel.Target);
            var shape = drawing?.Descendants().FirstOrDefault(e => e.Name.Namespace == V
                && ((string?)e.Attribute("id") == spid || (string?)e.Attribute(O + "spid") == spid));
            if (shape?.Element(V + "imagedata") is not { } image)
                continue;
            var id = (string?)image.Attribute(O + "relid") ?? (string?)image.Attribute(Ns.R + "id");
            if (id == null || package.Relationship(rel.Target, id) is not { External: false })
                continue;
            var crop = (Fraction(image, "cropleft"), Fraction(image, "croptop"), Fraction(image, "cropright"), Fraction(image, "cropbottom"));
            return new Picture(rel.Target, id, crop, Box((string?)shape.Attribute("style")));
        }
        return null;
    }

    /// <summary>A VML part; older ones are HTML-like (unclosed br elements), which are closed before parsing.</summary>
    static XDocument? Load(PresentationContext presentation, string part)
    {
        try
        {
            return presentation.Load(part);
        }
        catch (System.Xml.XmlException)
        {
            var text = System.Text.Encoding.UTF8.GetString(presentation.Package.ReadBytes(part));
            try { return XDocument.Parse(UnclosedBreak().Replace(text, "<br/>")); }
            catch (System.Xml.XmlException) { return null; }
        }
    }

    [GeneratedRegex(@"<br\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex UnclosedBreak();

    /// <summary>A crop value: a fraction, or 1/65536ths with the suffix f.</summary>
    static double Fraction(XElement image, string attribute)
    {
        var value = (string?)image.Attribute(attribute);
        if (string.IsNullOrEmpty(value))
            return 0;
        return value.EndsWith('f')
            ? double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f / 65536 : 0
            : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    /// <summary>The box of a VML shape's style (position: absolute; left, top, width, height) in EMU, or null.</summary>
    static (double X, double Y, double W, double H)? Box(string? style)
    {
        if (style == null)
            return null;
        var properties = style.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split(':', 2)).Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim().ToLowerInvariant(), p => p[1].Trim(), StringComparer.Ordinal);
        double? Length(string name) => properties.TryGetValue(name, out var v) ? Emu(v) : null;
        return Length("left") is { } x && Length("top") is { } y && Length("width") is { } w && Length("height") is { } h ? (x, y, w, h) : null;
    }

    /// <summary>A CSS length (pt, in, cm, mm, px; points without unit) in EMU.</summary>
    static double? Emu(string value)
    {
        var match = Regex.Match(value, @"^(-?[0-9.]+)\s*([a-z]*)$", RegexOptions.IgnoreCase);
        if (!match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return null;
        return match.Groups[2].Value.ToLowerInvariant() switch
        {
            "in" => number * 914400,
            "cm" => number * 360000,
            "mm" => number * 36000,
            "px" => number * 9525,
            "emu" => number,
            _ => number * 12700,
        };
    }

    /// <summary>A DrawingML picture showing <paramref name="picture"/> in the box <paramref name="xfrm"/> (EMU), for the renderer.</summary>
    public static XElement Element(Picture picture, string id, string name, (double X, double Y, double W, double H) xfrm)
    {
        var (l, t, r, b) = picture.Crop;
        static string Per(double fraction) => ((long)Math.Round(fraction * 100000)).ToString(CultureInfo.InvariantCulture);
        return new XElement(Ns.P + "pic",
            new XElement(Ns.P + "nvPicPr", new XElement(Ns.P + "cNvPr", new XAttribute("id", id), new XAttribute("name", name)),
                new XElement(Ns.P + "cNvPicPr"), new XElement(Ns.P + "nvPr")),
            new XElement(Ns.P + "blipFill",
                new XElement(Ns.A + "blip", new XAttribute(Ns.R + "embed", picture.ImageId)),
                new XElement(Ns.A + "srcRect", new XAttribute("l", Per(l)), new XAttribute("t", Per(t)), new XAttribute("r", Per(r)), new XAttribute("b", Per(b))),
                new XElement(Ns.A + "stretch", new XElement(Ns.A + "fillRect"))),
            new XElement(Ns.P + "spPr",
                new XElement(Ns.A + "xfrm",
                    new XElement(Ns.A + "off", new XAttribute("x", (long)Math.Round(xfrm.X)), new XAttribute("y", (long)Math.Round(xfrm.Y))),
                    new XElement(Ns.A + "ext", new XAttribute("cx", (long)Math.Round(xfrm.W)), new XAttribute("cy", (long)Math.Round(xfrm.H)))),
                new XElement(Ns.A + "prstGeom", new XAttribute("prst", "rect"), new XElement(Ns.A + "avLst"))));
    }
}
