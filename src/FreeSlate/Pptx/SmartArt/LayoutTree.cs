using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate.Pptx.SmartArt;

/// <summary>
/// A layout node of a SmartArt layout definition instantiated for a data point (a presentation point): its algorithm,
/// shape, text, constraints, rules and variables as chosen for that point, its child instances, and the geometry the
/// layout gives it.
/// </summary>
public sealed class LayoutNode(XElement definition, DataPoint point, LayoutNode? parent)
{
    public XElement Definition { get; } = definition;
    public string Name { get; } = (string?)definition.Attribute("name") ?? "";
    public DataPoint Point { get; } = point;
    public LayoutNode? Parent { get; } = parent;
    public List<LayoutNode> Children { get; } = [];

    public XElement? Algorithm { get; set; }
    public XElement? Shape { get; set; }
    public XElement? PresOf { get; set; }

    /// <summary>The data points whose text the node shows (presOf).</summary>
    public List<DataPoint> Text { get; } = [];

    public List<XElement> Constraints { get; } = [];
    public List<XElement> Rules { get; } = [];

    /// <summary>The layout variables set on this node (varLst), overridden by the saved presentation point's.</summary>
    public Dictionary<string, string> Variables { get; } = new(StringComparer.Ordinal);

    public string? StyleLabel { get; } = (string?)definition.Attribute("styleLbl");

    /// <summary>The presentation point PowerPoint saved for this node, if any.</summary>
    public DataPoint? Saved { get; set; }

    /// <summary>The algorithm's type: composite unless given.</summary>
    public string AlgorithmType => (string?)Algorithm?.Attribute("type") ?? "composite";

    /// <summary>An algorithm parameter (dgm:param), or null.</summary>
    public string? Param(string type) =>
        (string?)Algorithm?.Elements(DataModel.Dgm + "param").FirstOrDefault(p => (string?)p.Attribute("type") == type)?.Attribute("val");

    /// <summary>A variable of this node or the nearest ancestor that sets it.</summary>
    public string? Variable(string name)
    {
        for (var node = this; node != null; node = node.Parent)
        {
            if (node.Variables.TryGetValue(name, out var value))
                return value;
        }
        return null;
    }

    // The geometry the layout gives the node, in points relative to the diagram.
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public double Rotation { get; set; }

    /// <summary>The factor the parent's algorithm scaled the node's constraint size by (lin and snake shrink to fit).</summary>
    public double Scale { get; set; } = 1;

    /// <summary>Notes of the layout for diagnostics (text fitting).</summary>
    public string Note { get; set; } = "";

    /// <summary>The value types the node's ancestors' constraints set for it, which win over its own.</summary>
    public HashSet<string> FromAncestors { get; } = new(StringComparer.Ordinal);

    /// <summary>Constraint values resolved for this node (w, h, primFontSz, lMarg …).</summary>
    public Dictionary<string, double> Values { get; } = new(StringComparer.Ordinal);

    public IEnumerable<LayoutNode> Descendants()
    {
        foreach (var child in Children)
        {
            yield return child;
            foreach (var d in child.Descendants())
                yield return d;
        }
    }

    public override string ToString() => $"{Name} ({Point})";
}

/// <summary>
/// Instantiates a layout definition (dgm:layoutDef, catalog: smartart.layout) for a data model: from the root layout
/// node with the document point, every element is processed for the current data point: layoutNode creates a child
/// instance, forEach repeats its content for the points it selects along its axis, choose takes the first if whose
/// condition holds (else otherwise), and alg, shape, presOf, constrLst, ruleLst and varLst set the current instance's.
/// </summary>
public sealed class LayoutTree
{
    static readonly XNamespace D = DataModel.Dgm;

    readonly DataModel model;
    readonly Dictionary<string, XElement> namedForEach = new(StringComparer.Ordinal);

    public LayoutNode Root { get; }

    public LayoutTree(XElement layoutDef, DataModel model)
    {
        this.model = model;
        foreach (var forEach in layoutDef.Descendants(D + "forEach"))
        {
            if ((string?)forEach.Attribute("name") is { Length: > 0 } name)
                namedForEach.TryAdd(name, forEach);
        }
        var root = layoutDef.Element(D + "layoutNode") ?? new XElement(D + "layoutNode");
        Root = new LayoutNode(root, model.Document, null);
        Attach(Root);
        Process(root.Elements(), Root, model.Document);
    }

    void Attach(LayoutNode node)
    {
        if (model.Presentations.TryGetValue((node.Point.Id, node.Name), out var saved))
            node.Saved = saved;
    }

    void Process(IEnumerable<XElement> items, LayoutNode owner, DataPoint point)
    {
        foreach (var item in items)
        {
            switch (item.Name.LocalName)
            {
                case "alg":
                    owner.Algorithm ??= item;
                    break;
                case "shape":
                    owner.Shape ??= item;
                    break;
                case "presOf":
                    if (owner.PresOf == null)
                    {
                        owner.PresOf = item;
                        owner.Text.AddRange(Select(item, point));
                    }
                    break;
                case "constrLst":
                    owner.Constraints.AddRange(item.Elements(D + "constr"));
                    break;
                case "ruleLst":
                    owner.Rules.AddRange(item.Elements(D + "rule"));
                    break;
                case "varLst":
                    foreach (var variable in item.Elements())
                        owner.Variables[variable.Name.LocalName] = (string?)variable.Attribute("val") ?? Default(variable.Name.LocalName);
                    // The variables the user set are saved with the presentation point.
                    if (owner.Saved?.PropertySet?.Element(D + "presLayoutVars") is { } saved)
                    {
                        foreach (var variable in saved.Elements())
                            owner.Variables[variable.Name.LocalName] = (string?)variable.Attribute("val") ?? Default(variable.Name.LocalName);
                    }
                    break;
                case "layoutNode":
                {
                    var child = new LayoutNode(item, point, owner);
                    Attach(child);
                    owner.Children.Add(child);
                    Process(item.Elements(), child, point);
                    break;
                }
                case "forEach":
                {
                    // A reference uses the named forEach whole: its selection and its content.
                    var body = (string?)item.Attribute("ref") is { Length: > 0 } reference && namedForEach.TryGetValue(reference, out var referenced) ? referenced : item;
                    foreach (var selected in Select(body, point))
                        Process(body.Elements(), owner, selected);
                    break;
                }
                case "choose":
                {
                    var branch = item.Elements(D + "if").FirstOrDefault(c => Holds(c, owner, point)) ?? item.Element(D + "else");
                    if (branch != null)
                        Process(branch.Elements(), owner, point);
                    break;
                }
            }
        }
    }

    static string Default(string variable) => variable switch
    {
        "dir" => "norm",
        "hierBranch" => "std",
        "animOne" => "one",
        "animLvl" => "none",
        "resizeHandles" => "rel",
        "chMax" or "chPref" => "-1",
        _ => "",
    };

    static List<string> List(XElement element, string attribute, string fallback) =>
        ((string?)element.Attribute(attribute) ?? fallback).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

    /// <summary>
    /// The points a forEach, presOf or condition selects from <paramref name="point"/>: along each axis in turn, of the
    /// point types, from st (1-based, negative from the end) cnt points (0 for all) every step-th; sibling
    /// transitions after the last node are left out unless hideLastTrans is false.
    /// </summary>
    public IEnumerable<DataPoint> Select(XElement element, DataPoint point) => SelectFrom(element, point).Select(p => p.Point);

    /// <summary>The selected points with the point the last axis started from.</summary>
    List<(DataPoint Base, DataPoint Point)> SelectFrom(XElement element, DataPoint point)
    {
        var axes = List(element, "axis", "none");
        var types = List(element, "ptType", "all");
        var starts = List(element, "st", "1");
        var counts = List(element, "cnt", "0");
        var steps = List(element, "step", "1");
        var hide = List(element, "hideLastTrans", "true");
        List<(DataPoint Base, DataPoint Point)> current = [(point, point)];
        for (var i = 0; i < axes.Count; i++)
        {
            var type = types.Count > i ? types[i] : types[^1];
            var start = int.Parse(starts.Count > i ? starts[i] : "1", CultureInfo.InvariantCulture);
            var count = int.Parse(counts.Count > i ? counts[i] : "0", CultureInfo.InvariantCulture);
            var step = int.Parse(steps.Count > i ? steps[i] : "1", CultureInfo.InvariantCulture);
            var hideLast = (hide.Count > i ? hide[i] : hide[^1]) is not ("false" or "0");
            current = current.SelectMany(p =>
            {
                var selected = model.Axis(p.Point, axes[i]).Where(q => DataModel.Matches(q, type)).ToList();
                // Only a walk over siblings leaves the last transition out; a point selects itself.
                if (hideLast && axes[i] != "self")
                    selected.RemoveAll(q => q.Type == "sibTrans" && IsLastTransition(q));
                return Range(selected, start, count, step).Select(q => (p.Point, q));
            }).ToList();
        }
        return current;
    }

    /// <summary>A sibling transition after the last node of its parent.</summary>
    static bool IsLastTransition(DataPoint transition) =>
        transition.Owner is { } owner && transition.Parent is { } parent && parent.Children.LastOrDefault(c => c.IsNode) == owner;

    static IEnumerable<DataPoint> Range(List<DataPoint> points, int start, int count, int step)
    {
        if (points.Count == 0)
            return [];
        var index = start > 0 ? start - 1 : points.Count + start;
        if (step == 0)
            step = 1;
        var result = new List<DataPoint>();
        for (; index >= 0 && index < points.Count; index += step)
        {
            result.Add(points[index]);
            if (count > 0 && result.Count == count)
                break;
        }
        return result;
    }

    /// <summary>Whether an if element's condition holds for <paramref name="point"/> in <paramref name="owner"/>.</summary>
    bool Holds(XElement condition, LayoutNode owner, DataPoint point)
    {
        var func = (string?)condition.Attribute("func") ?? "";
        var op = (string?)condition.Attribute("op") ?? "equ";
        var expected = (string?)condition.Attribute("val") ?? "";
        string actual;
        switch (func)
        {
            case "cnt":
                actual = Select(condition, point).Count().ToString(CultureInfo.InvariantCulture);
                break;
            case "pos" or "revPos" or "posEven" or "posOdd":
            {
                var (position, total) = Position(point);
                var value = func switch
                {
                    "pos" => position,
                    "revPos" => total - position + 1,
                    "posEven" => position % 2 == 0 ? 1 : 0,
                    _ => position % 2 == 1 ? 1 : 0,
                };
                actual = value.ToString(CultureInfo.InvariantCulture);
                break;
            }
            case "depth":
                actual = DataModel.Depth(point).ToString(CultureInfo.InvariantCulture);
                break;
            case "maxDepth":
            {
                // The deepest of the selected points, counted from the point the last axis started from (the root for
                // "root des", the current point for "des").
                var selected = SelectFrom(condition, point).Where(p => p.Point.IsNode).ToList();
                var depth = selected.Count == 0 ? 0 : selected.Max(p => DataModel.Depth(p.Point) - DataModel.Depth(p.Base));
                actual = depth.ToString(CultureInfo.InvariantCulture);
                break;
            }
            case "var":
                actual = owner.Variable((string?)condition.Attribute("arg") ?? "") ?? Default((string?)condition.Attribute("arg") ?? "");
                break;
            default:
                return false;
        }
        return Compare(actual, op, expected);
    }

    /// <summary>The 1-based position of a point among its parent's children of the same kind, and their count.</summary>
    static (int Position, int Total) Position(DataPoint point)
    {
        if (point.Parent is not { } parent)
            return (1, 1);
        var peers = parent.Children.Where(c => point.IsNode ? c.IsNode : c.Type == point.Type).ToList();
        return (peers.IndexOf(point) + 1, peers.Count);
    }

    static bool Compare(string actual, string op, string expected)
    {
        var numeric = double.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var a)
                      & double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var b);
        if (!numeric)
        {
            // Booleans and names compare as text (true/1, false/0).
            static string Normal(string v) => v switch { "1" => "true", "0" => "false", _ => v };
            var equal = string.Equals(Normal(actual), Normal(expected), StringComparison.OrdinalIgnoreCase);
            return op switch { "equ" => equal, "neq" => !equal, _ => false };
        }
        return op switch
        {
            "equ" => Math.Abs(a - b) < 1e-9,
            "neq" => Math.Abs(a - b) >= 1e-9,
            "gt" => a > b,
            "lt" => a < b,
            "gte" => a >= b,
            "lte" => a <= b,
            _ => false,
        };
    }
}
