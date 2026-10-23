using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Opc;

namespace FreeSlate.Pptx;

/// <summary>
/// Changes to the list of a presentation's slides and to the parts around it, made in the XML they were read from
/// (catalog: package.opc), as PowerPoint makes them (measured through its object model, PowerPoint 365): the slides
/// (p:sldIdLst) and their sections (p14:sectionLst) and custom shows (p:custShowLst) in the presentation part, the
/// relationships of parts (.rels), the content types of the package, part names, a slide made from a layout.
/// </summary>
static class SlideList
{
    public const string RelationshipsNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    const string ContentTypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";
    public const string OfficeRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public static readonly XNamespace P14 = "http://schemas.microsoft.com/office/powerpoint/2010/main";
    public const string SlideType = "application/vnd.openxmlformats-officedocument.presentationml.slide+xml";

    /// <summary>The relationships part (.rels) of a part.</summary>
    public static string RelationshipsOf(string part) =>
        part[..(part.LastIndexOf('/') + 1)] + "_rels/" + part[(part.LastIndexOf('/') + 1)..] + ".rels";

    /// <summary>The target of a relationship from one part to another, relative to the first's folder.</summary>
    public static string Relative(string from, string to)
    {
        var a = from.TrimStart('/').Split('/')[..^1];
        var b = to.TrimStart('/').Split('/');
        var common = 0;
        while (common < a.Length && common < b.Length - 1 && string.Equals(a[common], b[common], StringComparison.OrdinalIgnoreCase))
            common++;
        return string.Join('/', Enumerable.Repeat("..", a.Length - common).Concat(b[common..]));
    }

    /// <summary>
    /// A part name not taken: the name with the smallest number from 1 in place of the number its file name ends with
    /// (ppt/slides/slide7.xml: ppt/slides/slide1.xml …), none of the package's parts nor of <paramref name="taken"/>.
    /// </summary>
    public static string FreeName(OpcPackage package, string like, ISet<string> taken)
    {
        var folder = like[..(like.LastIndexOf('/') + 1)];
        var file = like[(like.LastIndexOf('/') + 1)..];
        var dot = file.LastIndexOf('.');
        var (stem, extension) = dot >= 0 ? (file[..dot], file[dot..]) : (file, "");
        stem = stem.TrimEnd("0123456789".ToCharArray());
        for (var n = 1; ; n++)
        {
            var name = $"{folder}{stem}{n.ToString(CultureInfo.InvariantCulture)}{extension}";
            if (!package.Exists(name) && !taken.Contains(name))
                return name;
        }
    }

    /// <summary>One more than the greatest number of the relationship ids "rId…" among a part's relationships.</summary>
    public static string NextRelationshipId(IEnumerable<string> ids) =>
        "rId" + (ids.Select(id => id.StartsWith("rId", StringComparison.Ordinal) && int.TryParse(id[3..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty(0).Max() + 1).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A relationships part with relationships added (each its id, type and target) and those of some ids taken away; a
    /// new part where there was none. Only what changes is written anew.
    /// </summary>
    public static byte[] Relationships(byte[]? bytes, IEnumerable<(string Id, string Type, string Target)> added, ISet<string> removed)
    {
        XNamespace ns = RelationshipsNs;
        var made = added.Select(r => new XElement(ns + "Relationship", new XAttribute("Id", r.Id), new XAttribute("Type", r.Type), new XAttribute("Target", r.Target))).ToList();
        if (bytes == null)
            return Document(new XElement(ns + "Relationships", made));
        return Edited(bytes, root => root.Elements(ns + "Relationship").Where(r => removed.Contains((string?)r.Attribute("Id") ?? "")), made);
    }

    /// <summary>The content types with overrides added for new parts (each its name and type) and those of parts taken away removed.</summary>
    public static byte[] ContentTypes(byte[] bytes, IEnumerable<(string Part, string Type)> added, ISet<string> removed)
    {
        XNamespace ns = ContentTypesNs;
        var made = added.Select(o => new XElement(ns + "Override", new XAttribute("PartName", o.Part), new XAttribute("ContentType", o.Type))).ToList();
        return Edited(bytes, root => root.Elements(ns + "Override").Where(o => removed.Contains((string?)o.Attribute("PartName") ?? "")), made);
    }

    /// <summary>
    /// A part's XML with some of its root's children taken away and new ones put after its last, every other byte as it
    /// was (see PartText).
    /// </summary>
    static byte[] Edited(byte[] bytes, Func<XElement, IEnumerable<XElement>> taken, List<XElement> added)
    {
        var text = new PartText(bytes);
        var xml = XDocument.Parse(text.Text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        var root = xml.Root!;
        var changes = taken(root).ToList().Select(e => (text.Start(e), text.End(text.Start(e)), "")).ToList();
        if (added.Count > 0)
        {
            if (root.Elements().LastOrDefault() is { } last)
            {
                var end = text.End(text.Start(last));
                last.AddAfterSelf(added);
                changes.Add((end, end, string.Concat(added.Select(PartText.Xml))));
            }
            else
            {
                var start = text.Start(root);
                root.Add(added);
                changes.Add((start, text.End(start), PartText.Xml(root)));
            }
        }
        return text.Spliced(changes);
    }

    /// <summary>A new part's XML as PowerPoint writes it: a declaration, then the element.</summary>
    public static byte[] Document(XElement root) =>
        new UTF8Encoding(false).GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n" + root.ToString(SaveOptions.DisableFormatting));

    /// <summary>
    /// A new slide as PowerPoint makes one from a layout: the layout's placeholders (not its date, footer, slide number
    /// and header), each a shape of the placeholder's type and index with an empty text and its name, without a place of
    /// its own.
    /// </summary>
    public static XElement FromLayout(XDocument layout)
    {
        var shapes = new List<XElement>();
        var id = 2;
        foreach (var shape in layout.Root?.Element(Ns.P + "cSld")?.Element(Ns.P + "spTree")?.Elements() ?? [])
        {
            if (Placeholders.Ph(shape) is not { } ph || Placeholders.Type(ph) is "dt" or "ftr" or "sldNum" or "hdr")
                continue;
            var name = shape.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("nv", StringComparison.Ordinal))?.Element(Ns.P + "cNvPr").Str("name") ?? "";
            shapes.Add(new XElement(Ns.P + "sp",
                new XElement(Ns.P + "nvSpPr",
                    new XElement(Ns.P + "cNvPr", new XAttribute("id", id++), new XAttribute("name", name)),
                    new XElement(Ns.P + "cNvSpPr", new XElement(Ns.A + "spLocks", new XAttribute("noGrp", "1"))),
                    new XElement(Ns.P + "nvPr", new XElement(Ns.P + "ph", ph.Attributes().Where(a => a.Name.LocalName is "type" or "orient" or "sz" or "idx")))),
                new XElement(Ns.P + "spPr"),
                new XElement(Ns.P + "txBody", new XElement(Ns.A + "bodyPr"), new XElement(Ns.A + "lstStyle"), new XElement(Ns.A + "p", new XElement(Ns.A + "endParaRPr")))));
        }
        return new XElement(Ns.P + "sld",
            new XAttribute(XNamespace.Xmlns + "a", Ns.A.NamespaceName), new XAttribute(XNamespace.Xmlns + "r", Ns.R.NamespaceName), new XAttribute(XNamespace.Xmlns + "p", Ns.P.NamespaceName),
            new XElement(Ns.P + "cSld",
                new XElement(Ns.P + "spTree",
                    new XElement(Ns.P + "nvGrpSpPr", new XElement(Ns.P + "cNvPr", new XAttribute("id", 1), new XAttribute("name", "")), new XElement(Ns.P + "cNvGrpSpPr"), new XElement(Ns.P + "nvPr")),
                    new XElement(Ns.P + "grpSpPr", new XElement(Ns.A + "xfrm",
                        new XElement(Ns.A + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(Ns.A + "ext", new XAttribute("cx", 0), new XAttribute("cy", 0)),
                        new XElement(Ns.A + "chOff", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(Ns.A + "chExt", new XAttribute("cx", 0), new XAttribute("cy", 0)))),
                    shapes)),
            new XElement(Ns.P + "clrMapOvr", new XElement(Ns.A + "masterClrMapping")));
    }
}
