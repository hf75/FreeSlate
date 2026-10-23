using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Opc;

namespace FreeSlate.Pptx;

/// <summary>
/// Changes to the shapes of a slide made in the XML they were read from (catalog: package.opc), as PowerPoint makes them
/// (measured through its object model, PowerPoint 365): a shape placed by its transform (a:xfrm of p:spPr or p:grpSpPr,
/// p:xfrm of a graphic frame; a placeholder that takes its place from its layout gets one of its own, a group keeps its
/// children's coordinates and so moves and scales them); a shape taken away with what animates it (its effects, the
/// sequences its click starts, its builds, groups of effects left empty) and the connections of connectors to it; a text
/// box made as PowerPoint's (a rectangle without fill whose height fits its text).
/// </summary>
static class SlideShapes
{
    static readonly XNamespace Mc = MarkupCompatibility.Mc;

    /// <summary>
    /// Gives a shape a place in EMU, keeping its turn and flips (those of the transform it took from its layout too);
    /// returns the element whose XML changed and was there before (the transform, else the shape's properties).
    /// </summary>
    public static XElement Place(XElement shape, long x, long y, long width, long height, XElement? inherited)
    {
        XElement? xfrm;
        XElement holder;
        switch (shape.Name.LocalName)
        {
            case "sp" or "pic" or "cxnSp":
                holder = shape.Element(Ns.P + "spPr") ?? throw new NotSupportedException("The shape has no shape properties.");
                xfrm = holder.Element(Ns.A + "xfrm");
                break;
            case "grpSp":
                holder = shape.Element(Ns.P + "grpSpPr") ?? throw new NotSupportedException("The group has no group properties.");
                xfrm = holder.Element(Ns.A + "xfrm") ?? throw new NotSupportedException("The group has no transform.");
                if (xfrm.Element(Ns.A + "chOff") == null || xfrm.Element(Ns.A + "chExt") == null)
                    throw new NotSupportedException("The group's transform has no child coordinates.");
                break;
            case "graphicFrame":
                holder = shape;
                xfrm = shape.Element(Ns.P + "xfrm") ?? throw new NotSupportedException("The graphic frame has no transform.");
                break;
            default:
                throw new NotSupportedException($"A {shape.Name.LocalName} cannot be placed.");
        }
        var made = xfrm == null;
        if (xfrm == null)
        {
            xfrm = new XElement(Ns.A + "xfrm", inherited?.Attributes().Where(a => a.Name.LocalName is "rot" or "flipH" or "flipV"));
            holder.AddFirst(xfrm);
        }
        Set(xfrm, "off", "x", x, "y", y);
        Set(xfrm, "ext", "cx", width, "cy", height);
        return made ? holder : xfrm;
    }

    /// <summary>Sets a child of a transform (off, ext), made in its place where it has none.</summary>
    static void Set(XElement xfrm, string name, string first, long a, string second, long b)
    {
        var ns = xfrm.Name.Namespace == Ns.P ? Ns.A : xfrm.Name.Namespace;
        var child = xfrm.Element(ns + name);
        if (child == null)
        {
            child = new XElement(ns + name);
            if (name == "off")
                xfrm.AddFirst(child);
            else if (xfrm.Element(ns + "off") is { } off)
                off.AddAfterSelf(child);
            else
                xfrm.AddFirst(child);
        }
        child.SetAttributeValue(first, a.ToString(CultureInfo.InvariantCulture));
        child.SetAttributeValue(second, b.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The shapes that stand for a shape: itself, or in an alternative of markup compatibility the shape of each of its
    /// choices and its fallback (each the one shape at the top of it).
    /// </summary>
    public static List<XElement> Alternatives(XElement shape)
    {
        if (shape.Parent is not { } branch || branch.Name.Namespace != Mc)
        {
            if (shape.Ancestors().Any(e => e.Name.Namespace == Mc))
                throw new NotSupportedException("The shape stands deep in an alternative of markup compatibility.");
            return [shape];
        }
        if (branch.Parent is not { } alternatives || alternatives.Name != Mc + "AlternateContent" || alternatives.Ancestors().Any(e => e.Name.Namespace == Mc))
            throw new NotSupportedException("The shape stands in an alternative of markup compatibility that holds others.");
        var shapes = alternatives.Elements().Select(b => b.Elements().Where(e => e.Name.Namespace == Ns.P).ToList()).ToList();
        if (shapes.Any(s => s.Count != 1))
            throw new NotSupportedException("An alternative of markup compatibility the shape stands in holds no shape or several.");
        return [.. shapes.Select(s => s[0])];
    }

    /// <summary>
    /// What goes with a shape taken away: the element that stands for it (an alternative of markup compatibility goes
    /// whole) and the ids of the shapes in it (a group's too).
    /// </summary>
    public static (XElement Element, HashSet<string> Ids) Removed(XElement shape)
    {
        var element = shape.Parent is { } parent && parent.Name.Namespace == Mc && parent.Parent?.Name == Mc + "AlternateContent" ? parent.Parent : shape;
        var ids = element.Descendants(Ns.P + "cNvPr").Select(c => c.Attribute("id")?.Value).OfType<string>().ToHashSet();
        return (element, ids);
    }

    /// <summary>
    /// Takes away what animates shapes in a slide's timing (p:timing): their builds, the effects that animate them, the
    /// interactive sequences their clicks start, their media nodes, and the groups of effects that are left empty; the
    /// timing goes whole where no animation is left. Returns whether the timing changed, and whether it is to go.
    /// </summary>
    public static (bool Changed, bool Gone) Unanimate(XElement timing, IReadOnlySet<string> ids)
    {
        var changed = false;
        foreach (var build in (timing.Element(Ns.P + "bldLst")?.Elements() ?? []).Where(e => ids.Contains(e.Attribute("spid")?.Value ?? "")).ToList())
        {
            build.Remove();
            changed = true;
        }
        foreach (var target in timing.Descendants(Ns.P + "spTgt").Where(t => ids.Contains(t.Attribute("spid")?.Value ?? "")).ToList())
        {
            if (!target.Ancestors().Contains(timing))
                continue;   // gone with an effect before
            if (target.Ancestors().FirstOrDefault(Animates) is not { } node)
                continue;
            var list = node.Parent;
            node.Remove();
            changed = true;
            // A group of effects left without any goes too, up to the sequence it stands in.
            while (list is { } l && l.Name == Ns.P + "childTnLst" && !l.Elements().Any() && l.Parent?.Parent is { } container && container.Name == Ns.P + "par"
                   && l.Parent.Attribute("nodeType")?.Value is not ("tmRoot" or "mainSeq"))
            {
                list = container.Parent;
                container.Remove();
            }
        }
        if (timing.Element(Ns.P + "bldLst") is { } builds && !builds.Elements().Any())
            builds.Remove();
        var gone = changed && !timing.Descendants().Any(e => e.Name == Ns.P + "spTgt" || e.Name == Ns.P + "inkTgt" || e.Name == Ns.P + "sndTgt")
                   && !timing.Descendants(Ns.P + "cTn").Any(c => c.Attribute("presetClass") != null);
        return (changed, gone);
    }

    /// <summary>A time node that animates a target as a whole: an effect, an interactive sequence, a media node.</summary>
    static bool Animates(XElement node) =>
        node.Name == Ns.P + "par" && node.Element(Ns.P + "cTn") is { } effect
                                  && (effect.Attribute("presetClass") != null || effect.Attribute("nodeType")?.Value is "clickEffect" or "withEffect" or "afterEffect")
        || node.Name == Ns.P + "seq" && node.Element(Ns.P + "cTn")?.Attribute("nodeType")?.Value == "interactiveSeq"
        || node.Name == Ns.P + "audio" || node.Name == Ns.P + "video";

    /// <summary>The connections of connectors to shapes taken away (a:stCxn, a:endCxn), each with the element that holds it.</summary>
    public static List<XElement> Connections(XElement tree, IReadOnlySet<string> ids, XElement removed) =>
        [.. tree.Descendants().Where(e => (e.Name == Ns.A + "stCxn" || e.Name == Ns.A + "endCxn") && ids.Contains(e.Attribute("id")?.Value ?? "")
                                          && !e.Ancestors().Contains(removed))];

    /// <summary>
    /// A text box as PowerPoint makes one (Insert, Text Box): a rectangle without fill at a place in EMU whose height fits
    /// its text (spAutoFit), its paragraphs the text's lines ("\n"), a line break "\v".
    /// </summary>
    public static XElement TextBox(uint id, long x, long y, long width, long height, string text) =>
        new(Ns.P + "sp",
            new XElement(Ns.P + "nvSpPr",
                new XElement(Ns.P + "cNvPr", new XAttribute("id", id), new XAttribute("name", $"TextBox {id - 1}")),
                new XElement(Ns.P + "cNvSpPr", new XAttribute("txBox", "1")),
                new XElement(Ns.P + "nvPr")),
            new XElement(Ns.P + "spPr",
                new XElement(Ns.A + "xfrm",
                    new XElement(Ns.A + "off", new XAttribute("x", x), new XAttribute("y", y)),
                    new XElement(Ns.A + "ext", new XAttribute("cx", width), new XAttribute("cy", height))),
                new XElement(Ns.A + "prstGeom", new XAttribute("prst", "rect"), new XElement(Ns.A + "avLst")),
                new XElement(Ns.A + "noFill")),
            new XElement(Ns.P + "txBody",
                new XElement(Ns.A + "bodyPr", new XAttribute("wrap", "square"), new XAttribute("rtlCol", "0"), new XElement(Ns.A + "spAutoFit")),
                new XElement(Ns.A + "lstStyle"),
                text.Split('\n').Select(line => new XElement(Ns.A + "p", Line(line)))));

    /// <summary>A line of a text box's text as runs, its line breaks ("\v") between them.</summary>
    static IEnumerable<XElement> Line(string line)
    {
        var pieces = line.Split('\v');
        for (var i = 0; i < pieces.Length; i++)
        {
            if (i > 0)
                yield return new XElement(Ns.A + "br");
            if (pieces[i].Length > 0)
                yield return new XElement(Ns.A + "r", new XElement(Ns.A + "t", pieces[i]));
        }
    }
}
