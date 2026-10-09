using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate.Pptx.SmartArt;

/// <summary>
/// Values the text fitting fixes for the next layout (font sizes, grown or equalized heights), which the constraints
/// then no longer set; lengths in the units of the constraints (before the parent's algorithm scales the node).
/// </summary>
public sealed class Pins
{
    readonly Dictionary<(LayoutNode Node, string Type), double> values = [];

    /// <summary>The height a node had when its text was first fitted (points), the unit of its growth steps.</summary>
    public Dictionary<LayoutNode, double> StartHeight { get; } = [];

    public bool TryGet(LayoutNode node, string type, out double value) => values.TryGetValue((node, type), out value);

    public void Set(LayoutNode node, string type, double value) => values[(node, type)] = value;

    public void Remove(LayoutNode node, string type) => values.Remove((node, type));

    public IEnumerable<(string Type, double Value)> Of(LayoutNode node) =>
        values.Where(v => v.Key.Node == node).Select(v => (v.Key.Type, v.Value));
}

/// <summary>
/// Evaluates the constraints of a layout tree (dgm:constr, catalog: smartart.layout): every node's list in document
/// order, the nodes in tree order, each constraint setting a value of its targets (the declaring node, its children or
/// its descendants of a name or point type) to an absolute value or to a value of a reference node times a factor;
/// gte and lte only raise or lower a value already set, equ without a value makes the targets' values equal, and a
/// constraint without a value or reference gives 0 where nothing else sets the value. When a node has its box, its list
/// is evaluated again: its children's values follow its actual size (the composite's ar works that way, ECMA-376
/// §21.4.7.49). Sizes and positions are in millimeters, font sizes in points.
/// </summary>
public static class Constraints
{
    /// <summary>The value types that make up a node's own box, which its parent's algorithm gives it.</summary>
    static readonly HashSet<string> Box = ["w", "h", "l", "t", "r", "b", "ctrX", "ctrY"];

    static double? Number(XElement element, string attribute)
    {
        var text = (string?)element.Attribute(attribute);
        if (text == null)
            return null;
        if (text == "INF")
            return double.PositiveInfinity;
        if (text == "NaN")
            return double.NaN;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    /// <summary>The nodes a constraint or rule applies to, seen from the node declaring it.</summary>
    public static List<LayoutNode> Targets(LayoutNode owner, string? relation, string? name, string? ptType)
    {
        IEnumerable<LayoutNode> candidates = relation switch
        {
            "ch" => owner.Children,
            "des" => owner.Descendants(),
            _ => [owner],
        };
        if (!string.IsNullOrEmpty(name))
            candidates = candidates.Where(n => n.Name == name);
        if (!string.IsNullOrEmpty(ptType) && ptType != "all")
            candidates = candidates.Where(n => DataModel.Matches(n.Point, ptType));
        return candidates.ToList();
    }

    /// <summary>
    /// Evaluates all constraints of the tree under <paramref name="root"/>, again until no value changes: a constraint
    /// may refer to a value a later one sets (h from primFontSz before primFontSz is given).
    /// </summary>
    public static void Evaluate(LayoutNode root, Pins? pins = null)
    {
        var nodes = root.Descendants().Prepend(root).ToList();
        foreach (var node in nodes)
        {
            node.FromAncestors.Clear();
            for (var ancestor = node.Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                // Only constraints that give a value (not those that make values equal or give 0 where none is set).
                foreach (var constraint in ancestor.Constraints.Where(c => !Own(c) && !Bound(c)
                             && (c.Attribute("val") != null || (string?)c.Attribute("refType") is { Length: > 0 } and not "none")))
                {
                    if (Targets(ancestor, (string?)constraint.Attribute("for"), (string?)constraint.Attribute("forName"), (string?)constraint.Attribute("ptType")).Contains(node))
                        node.FromAncestors.Add((string?)constraint.Attribute("type") ?? "");
                }
            }
        }
        if (pins != null)
        {
            foreach (var node in nodes)
            {
                foreach (var (type, value) in pins.Of(node))
                    node.Values[type] = value;
            }
        }
        for (var pass = 0; pass < 8; pass++)
        {
            var before = nodes.Select(n => n.Values.Count + n.Values.Values.Sum(v => double.IsFinite(v) ? v : 0)).ToList();
            // A node's own values first, those its ancestors set for it over them (the nearest last), bounds at the end.
            foreach (var node in nodes)
            {
                foreach (var constraint in node.Constraints.Where(c => Own(c) && !Bound(c)))
                {
                    // A value an ancestor sets is the ancestor's: the node's own would only stand in for it meanwhile
                    // (bList2's composite, w = 1 of its own, takes its height from the width its parent gives).
                    if (!node.FromAncestors.Contains((string?)constraint.Attribute("type") ?? ""))
                        Apply(node, constraint, pins, null);
                }
            }
            foreach (var node in nodes)
            {
                foreach (var constraint in node.Constraints.Where(c => !Own(c) && !Bound(c)))
                    Apply(node, constraint, pins, null);
            }
            foreach (var node in nodes)
            {
                foreach (var constraint in node.Constraints.Where(Bound))
                    Apply(node, constraint, pins, null);
            }
            var after = nodes.Select(n => n.Values.Count + n.Values.Values.Sum(v => double.IsFinite(v) ? v : 0)).ToList();
            if (before.SequenceEqual(after))
                break;
        }
    }

    static bool Own(XElement constraint) => (string?)constraint.Attribute("for") is null or "" or "self";

    static bool Bound(XElement constraint) => (string?)constraint.Attribute("op") is "lte" or "gte";

    /// <summary>
    /// Evaluates a node's list again once it has its box: the values of its children and descendants follow its size,
    /// its own box stays as given, and its own values give way to those its ancestors set.
    /// </summary>
    public static void EvaluateList(LayoutNode node, Pins? pins)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var constraint in node.Constraints.Where(c => !Bound(c)))
            {
                if (Own(constraint) && node.FromAncestors.Contains((string?)constraint.Attribute("type") ?? ""))
                    continue;
                Apply(node, constraint, pins, node);
            }
            foreach (var constraint in node.Constraints.Where(Bound))
                Apply(node, constraint, pins, node);
        }
    }

    /// <summary>
    /// Applies a node's constraints on itself again once its parent's list has given it new values: those its ancestors
    /// do not set (h = 0.6 w follows a new width), then its bounds (h ≤ 0.4 w caps a height its parent's width gives;
    /// measured with process3).
    /// </summary>
    public static void EvaluateOwn(LayoutNode node, Pins? pins)
    {
        foreach (var constraint in node.Constraints.Where(c => Own(c) && !Bound(c)))
        {
            if (!node.FromAncestors.Contains((string?)constraint.Attribute("type") ?? ""))
                Apply(node, constraint, pins, null);
        }
        foreach (var constraint in node.Constraints.Where(c => Own(c) && Bound(c)))
            Apply(node, constraint, pins, null);
    }

    static void Apply(LayoutNode owner, XElement constraint, Pins? pins, LayoutNode? keep)
    {
        var type = (string?)constraint.Attribute("type") ?? "";
        if (type is "" or "none")
            return;
        var targets = Targets(owner, (string?)constraint.Attribute("for"), (string?)constraint.Attribute("forName"), (string?)constraint.Attribute("ptType"));
        if (targets.Count == 0)
            return;
        var op = (string?)constraint.Attribute("op") ?? "none";
        var fact = Number(constraint, "fact") ?? 1;
        if (double.IsNaN(fact))
            fact = 1;
        var refType = (string?)constraint.Attribute("refType");
        // A bound on a user variable holds nothing (measured: hProcess3's text, h ≤ userT, keeps the height of its font
        // size above the userT its root sets).
        if (op is "lte" or "gte" && refType?.StartsWith("user", StringComparison.Ordinal) == true)
            return;

        foreach (var target in targets)
        {
            if (target == keep && Box.Contains(type))
                continue;
            if (pins != null && pins.TryGet(target, type, out var pinned))
            {
                target.Values[type] = pinned;
                continue;
            }
            double? value;
            if (!string.IsNullOrEmpty(refType) && refType != "none")
            {
                var references = Targets(owner, (string?)constraint.Attribute("refFor"), (string?)constraint.Attribute("refForName"), (string?)constraint.Attribute("refPtType"));
                // A reference among the targets' peers refers to the same data point where there is one (the node's own
                // value for each of several instances), else to the first.
                var reference = references.FirstOrDefault(r => r.Point == target.Point) ?? references.FirstOrDefault();
                value = reference != null && Referenced(reference, refType, owner) is { } referenced ? referenced * fact : null;
            }
            else if (constraint.Attribute("val") == null)
            {
                // A constraint without a value or reference only gives a value nothing else sets: 0 (measured: process3's
                // text node keeps the height its grandparent gives it; list1's text node has no top margin).
                if (op == "equ" || target.Values.ContainsKey(type))
                    continue;
                value = 0;
            }
            else
            {
                value = Number(constraint, "val");
            }
            if (value is not { } v || double.IsNaN(v))
                continue;
            switch (op)
            {
                // A bound on a value the others derive (w from l and r) holds it only where they break it (measured:
                // VerticalCurvedList's text, w ≥ h, spans from its l to its r).
                case "gte":
                    if (target.Values.TryGetValue(type, out var low))
                        target.Values[type] = Math.Max(low, v);
                    else if (Get(target, type) is { } derivedLow)
                    {
                        if (derivedLow < v)
                            target.Values[type] = v;
                    }
                    else
                        target.Values[type] = v;
                    break;
                case "lte":
                    if (target.Values.TryGetValue(type, out var high))
                        target.Values[type] = Math.Min(high, v);
                    else if (Get(target, type) is { } derivedHigh)
                    {
                        if (derivedHigh > v)
                            target.Values[type] = v;
                    }
                    else
                        target.Values[type] = v;
                    break;
                default:
                    target.Values[type] = v;
                    break;
            }
        }
    }

    /// <summary>
    /// The smallest bound the lte constraints set on a node's value (its own and those of its ancestors that target it),
    /// or null: text fitting lets a node grow only that far (measured: process3's text node, h ≤ 0.4 w, shrinks its font).
    /// </summary>
    public static double? UpperBound(LayoutNode node, string type)
    {
        double? bound = null;
        for (var owner = node; owner != null; owner = owner.Parent)
        {
            foreach (var constraint in owner.Constraints)
            {
                if ((string?)constraint.Attribute("op") != "lte" || (string?)constraint.Attribute("type") != type)
                    continue;
                if (((string?)constraint.Attribute("refType"))?.StartsWith("user", StringComparison.Ordinal) == true)
                    continue;
                if (!Targets(owner, (string?)constraint.Attribute("for"), (string?)constraint.Attribute("forName"), (string?)constraint.Attribute("ptType")).Contains(node))
                    continue;
                var fact = Number(constraint, "fact") is { } f && !double.IsNaN(f) ? f : 1;
                double? value = null;
                if ((string?)constraint.Attribute("refType") is { Length: > 0 } refType && refType != "none")
                {
                    var references = Targets(owner, (string?)constraint.Attribute("refFor"), (string?)constraint.Attribute("refForName"), (string?)constraint.Attribute("refPtType"));
                    var reference = references.FirstOrDefault(r => r.Point == node.Point) ?? references.FirstOrDefault();
                    if (reference != null && Get(reference, refType) is { } referenced)
                        value = referenced * fact;
                }
                else if (Number(constraint, "val") is { } v && !double.IsNaN(v))
                    value = v;
                if (value is { } upper)
                    bound = bound is { } b ? Math.Min(b, upper) : upper;
            }
        }
        return bound;
    }

    /// <summary>
    /// The value a constraint refers to: the reference's laid-out width or height once the layout has placed it (other
    /// than the declaring node, whose box is being given), else its value (measured: hierarchy5's background columns
    /// 1.2 times as wide as its first level's shapes as the fitted tree draws them; process3's description below its
    /// title's height after the title's bound).
    /// </summary>
    static double? Referenced(LayoutNode reference, string type, LayoutNode owner)
    {
        if (reference.Placed && reference != owner && type is "w" or "h")
            return type == "w" ? reference.W : reference.H;
        return Get(reference, type);
    }

    /// <summary>A value of a node: set by a constraint, or derived (r = l + w, ctrX = l + w / 2 …).</summary>
    public static double? Get(LayoutNode node, string type)
    {
        if (node.Values.TryGetValue(type, out var value))
            return value;
        var v = node.Values;
        double? Has(string key) => v.TryGetValue(key, out var x) ? x : null;
        return type switch
        {
            "r" when Has("l") is { } l && Has("w") is { } w => l + w,
            "b" when Has("t") is { } t && Has("h") is { } h => t + h,
            "ctrX" when Has("l") is { } l && Has("w") is { } w => l + w / 2,
            "ctrY" when Has("t") is { } t && Has("h") is { } h => t + h / 2,
            "w" when Has("l") is { } l && Has("r") is { } r => r - l,
            "h" when Has("t") is { } t && Has("b") is { } b => b - t,
            "l" when Has("r") is { } r && Has("w") is { } w => r - w,
            "t" when Has("b") is { } b && Has("h") is { } h => b - h,
            _ => null,
        };
    }
}
