using System.Xml.Linq;

namespace FreeSlate.Pptx.SmartArt;

/// <summary>A point of a SmartArt data model (dgm:pt): a node, the document, an assistant, a transition or a presentation point.</summary>
public sealed class DataPoint(XElement element)
{
    public XElement Element { get; } = element;
    public string Id { get; } = (string?)element.Attribute("modelId") ?? "";

    /// <summary>doc, node, asst, parTrans, sibTrans or pres.</summary>
    public string Type { get; } = (string?)element.Attribute("type") ?? "node";

    /// <summary>The parent in the semantic tree (null for the document); transitions keep the node they lead to or follow.</summary>
    public DataPoint? Parent { get; internal set; }

    /// <summary>Children in order, each node with the transitions around it: parent transition, node, sibling transition.</summary>
    public List<DataPoint> Children { get; } = [];

    /// <summary>For a transition: the node it belongs to (the child of the parOf connection).</summary>
    public DataPoint? Owner { get; internal set; }

    public XElement? PropertySet => Element.Element(DataModel.Dgm + "prSet");
    public string? Prop(string name) => (string?)PropertySet?.Attribute(name);

    public bool IsNode => Type is "node" or "asst";
    public bool IsTransition => Type is "parTrans" or "sibTrans";

    public override string ToString() => $"{Type} {Id}";
}

/// <summary>
/// A SmartArt data model (dgm:dataModel, catalog: smartart.data): the points, joined into the semantic tree by parOf
/// connections in source order, each child preceded by its parent transition and followed by its sibling transition
/// (the order the layout's axes walk), and the presentation points PowerPoint saved (pres points joined by presOf and
/// presParOf connections) for reuse of their ids and properties.
/// </summary>
public sealed class DataModel
{
    public static readonly XNamespace Dgm = "http://schemas.openxmlformats.org/drawingml/2006/diagram";

    public Dictionary<string, DataPoint> Points { get; } = new(StringComparer.Ordinal);
    public DataPoint Document { get; }

    /// <summary>Saved presentation points by (associated data point id, layout node name).</summary>
    public Dictionary<(string Assoc, string Name), DataPoint> Presentations { get; } = [];

    public XElement Element { get; }

    public DataModel(XElement dataModel)
    {
        Element = dataModel;
        foreach (var pt in dataModel.Element(Dgm + "ptLst")?.Elements(Dgm + "pt") ?? [])
        {
            var point = new DataPoint(pt);
            Points.TryAdd(point.Id, point);
        }
        Document = Points.Values.FirstOrDefault(p => p.Type == "doc") ?? new DataPoint(new XElement(Dgm + "pt", new XAttribute("modelId", "doc"), new XAttribute("type", "doc")));

        var parentOf = new List<(DataPoint Parent, DataPoint Child, int Order, DataPoint? ParTrans, DataPoint? SibTrans)>();
        foreach (var cxn in dataModel.Element(Dgm + "cxnLst")?.Elements(Dgm + "cxn") ?? [])
        {
            var type = (string?)cxn.Attribute("type") ?? "parOf";
            if (type != "parOf")
                continue;
            if (!Points.TryGetValue((string?)cxn.Attribute("srcId") ?? "", out var parent) || !Points.TryGetValue((string?)cxn.Attribute("destId") ?? "", out var child))
                continue;
            Points.TryGetValue((string?)cxn.Attribute("parTransId") ?? "", out var parTrans);
            Points.TryGetValue((string?)cxn.Attribute("sibTransId") ?? "", out var sibTrans);
            parentOf.Add((parent, child, (int?)cxn.Attribute("srcOrd") ?? 0, parTrans, sibTrans));
        }
        foreach (var group in parentOf.GroupBy(c => c.Parent))
        {
            foreach (var (parent, child, _, parTrans, sibTrans) in group.OrderBy(c => c.Order))
            {
                child.Parent = parent;
                if (parTrans != null)
                {
                    parTrans.Parent = parent;
                    parTrans.Owner = child;
                    parent.Children.Add(parTrans);
                }
                parent.Children.Add(child);
                if (sibTrans != null)
                {
                    sibTrans.Parent = parent;
                    sibTrans.Owner = child;
                    parent.Children.Add(sibTrans);
                }
            }
        }
        foreach (var point in Points.Values.Where(p => p.Type == "pres"))
        {
            if (point.Prop("presAssocID") is { } assoc && point.Prop("presName") is { } name)
                Presentations.TryAdd((assoc, name), point);
        }
    }

    /// <summary>Whether <paramref name="point"/> is of type <paramref name="ptType"/> (ST_ElementType).</summary>
    public static bool Matches(DataPoint point, string? ptType) => ptType switch
    {
        null or "" or "all" => true,
        "node" => point.IsNode,
        "nonAsst" or "norm" => point.Type == "node",
        "asst" or "nonNorm" => point.Type == "asst",
        "doc" => point.Type == "doc",
        "parTrans" => point.Type == "parTrans",
        "sibTrans" => point.Type == "sibTrans",
        "pres" => point.Type == "pres",
        _ => point.Type == ptType,
    };

    /// <summary>
    /// The points along <paramref name="axis"/> from <paramref name="point"/> (ST_AxisType), in document order (also the
    /// preceding ones: st="-1" then picks the nearest).
    /// </summary>
    public IEnumerable<DataPoint> Axis(DataPoint point, string axis)
    {
        switch (axis)
        {
            case "self":
                return [point];
            case "ch":
                return point.Children;
            case "des":
                return Descendants(point);
            case "desOrSelf":
                return Descendants(point).Prepend(point);
            case "par":
                return point.Parent is { } parent ? [parent] : [];
            case "ancst":
                return Ancestors(point);
            case "ancstOrSelf":
                return Ancestors(point).Prepend(point);
            case "followSib":
                return point.Parent is { } p1 ? p1.Children.Skip(p1.Children.IndexOf(point) + 1) : [];
            case "precedSib":
                return point.Parent is { } p2 ? p2.Children.Take(p2.Children.IndexOf(point)) : [];
            case "follow":
                return Following(point);
            case "preced":
                return Preceding(point);
            case "root":
                return [Document];
            case "none":
                return [];
            default:
                return [];
        }
    }

    static IEnumerable<DataPoint> Descendants(DataPoint point)
    {
        foreach (var child in point.Children)
        {
            yield return child;
            if (child.IsNode)
            {
                foreach (var d in Descendants(child))
                    yield return d;
            }
        }
    }

    static IEnumerable<DataPoint> Ancestors(DataPoint point)
    {
        for (var p = point.Parent; p != null; p = p.Parent)
            yield return p;
    }

    IEnumerable<DataPoint> Following(DataPoint point)
    {
        var all = Descendants(Document).ToList();
        var index = all.IndexOf(point);
        return index < 0 ? [] : all.Skip(index + 1);
    }

    IEnumerable<DataPoint> Preceding(DataPoint point)
    {
        var all = Descendants(Document).ToList();
        var index = all.IndexOf(point);
        return index < 0 ? [] : all.Take(index);
    }

    /// <summary>The depth of a point in the semantic tree: the document 0, its nodes 1, theirs 2 …</summary>
    public static int Depth(DataPoint point)
    {
        var depth = 0;
        for (var p = point.IsTransition ? point.Owner : point; p?.Parent != null; p = p.Parent)
            depth++;
        return depth;
    }

    /// <summary>The depth of the deepest node below <paramref name="point"/>, counted like <see cref="Depth"/>.</summary>
    public static int MaxDepth(DataPoint point)
    {
        var nodes = Descendants(point).Where(p => p.IsNode).ToList();
        return nodes.Count == 0 ? Depth(point) : nodes.Max(Depth);
    }
}
