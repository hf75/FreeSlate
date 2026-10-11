using System.Xml.Linq;

namespace FreeSlate.Pptx;

/// <summary>A shape together with the part it belongs to (relationship ids are resolved per part).</summary>
public sealed record PartElement(XElement Element, string Part);

/// <summary>Placeholder matching between slide, layout and master (catalog: presentation.placeholders).</summary>
public static class Placeholders
{
    public static XElement? Ph(XElement shape) =>
        shape.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("nv", StringComparison.Ordinal))
            ?.Element(Ns.P + "nvPr")?.Element(Ns.P + "ph");

    /// <summary>The placeholder type; a p:ph without type is an object placeholder ("obj").</summary>
    public static string Type(XElement ph) => ph.Str("type") ?? "obj";

    /// <summary>The master placeholder type a layout placeholder inherits from.</summary>
    public static string MasterType(string type) => type switch
    {
        "title" or "ctrTitle" => "title",
        "dt" or "ftr" or "sldNum" or "hdr" => type,
        _ => "body",
    };

    /// <summary>Which master text style applies: title, body or other.</summary>
    public static string TextStyle(string? type) => type switch
    {
        null => "other",
        "title" or "ctrTitle" => "title",
        "dt" or "ftr" or "sldNum" or "hdr" => "other",
        _ => "body",
    };

    static IEnumerable<XElement> Candidates(XDocument? document) =>
        document?.Root?.Element(Ns.P + "cSld")?.Element(Ns.P + "spTree")?.Descendants().Where(e => Ph(e) != null) ?? [];

    /// <summary>
    /// Finds the layout placeholder for a slide placeholder: PowerPoint goes by idx alone, 0 when it is missing,
    /// whatever the types (measured with the object model on a Title and Content layout: p:ph without idx, with
    /// type body, dt, ftr, sldNum, pic or subTitle, takes the title's place and title style; type title with idx 1
    /// takes the content placeholder's place, size and bullets; an idx the layout lacks matches nothing). The idx
    /// 4294967295 (-1) stands for none: such placeholders match by type, body that of an object placeholder.
    /// </summary>
    public static XElement? InLayout(XDocument? layout, XElement ph)
    {
        var candidates = Candidates(layout).ToList();
        if (Index(ph) is not { } idx)
            return candidates.FirstOrDefault(c => Type(Ph(c)!) == Type(ph)) ?? candidates.FirstOrDefault(c => SameType(Type(Ph(c)!), Type(ph)));
        // Among several of the same idx the one of the same type, else the first (LibreOffice's bullet-indent: p:ph
        // without type and idx takes the first of a layout's title and body, both without idx: the title).
        var byIdx = candidates.Where(c => Index(Ph(c)!) == idx).ToList();
        return byIdx.FirstOrDefault(c => Type(Ph(c)!) == Type(ph)) ?? byIdx.FirstOrDefault();
    }

    /// <summary>A placeholder's idx: 0 when it is missing, null for 4294967295, which stands for none.</summary>
    static long? Index(XElement ph) => ph.Attribute("idx")?.Value is not { } value ? 0
        : long.TryParse(value, out var idx) && idx != 4294967295 ? idx : null;

    /// <summary>
    /// Whether a placeholder has the idx 4294967295, which stands for none: without a layout placeholder of its type
    /// it inherits from the master's (measured: title and body on a blank layout take the master's places, while
    /// a title without idx or with idx 20 takes no place there).
    /// </summary>
    public static bool Unindexed(XElement ph) => Index(ph) == null;

    /// <summary>Finds the master placeholder for a layout (or slide) placeholder by type.</summary>
    public static XElement? InMaster(XDocument? master, XElement ph)
    {
        var type = MasterType(Type(ph));
        return Candidates(master).FirstOrDefault(c => MasterType(Type(Ph(c)!)) == type);
    }

    static bool SameType(string a, string b) =>
        a == b || (a is "title" or "ctrTitle" && b is "title" or "ctrTitle") || (a is "body" or "obj" && b is "body" or "obj");
}
