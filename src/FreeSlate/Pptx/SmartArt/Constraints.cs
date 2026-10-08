using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate.Pptx.SmartArt;

/// <summary>
/// Evaluates the constraints of a layout tree (dgm:constr, catalog: smartart.layout): every node's list in document
/// order, the nodes in tree order, each constraint setting a value of its targets (the declaring node, its children or
/// its descendants of a name or point type) to an absolute value or to a value of a reference node times a factor;
/// gte and lte only raise or lower a value already set, and equ without a value makes the targets' values equal.
/// Sizes and positions are in millimeters, font sizes in points.
/// </summary>
public static class Constraints
{
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
    public static void Evaluate(LayoutNode root)
    {
        var nodes = root.Descendants().Prepend(root).ToList();
        for (var pass = 0; pass < 8; pass++)
        {
            var before = nodes.Select(n => n.Values.Count + n.Values.Values.Sum(v => double.IsFinite(v) ? v : 0)).ToList();
            foreach (var node in nodes)
            {
                foreach (var constraint in node.Constraints)
                    Apply(node, constraint);
            }
            var after = nodes.Select(n => n.Values.Count + n.Values.Values.Sum(v => double.IsFinite(v) ? v : 0)).ToList();
            if (before.SequenceEqual(after))
                break;
        }
    }

    static void Apply(LayoutNode owner, XElement constraint)
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

        foreach (var target in targets)
        {
            double? value;
            if (!string.IsNullOrEmpty(refType) && refType != "none")
            {
                var references = Targets(owner, (string?)constraint.Attribute("refFor"), (string?)constraint.Attribute("refForName"), (string?)constraint.Attribute("refPtType"));
                // A reference among the targets' peers refers to the same data point where there is one (the node's own
                // value for each of several instances), else to the first.
                var reference = references.FirstOrDefault(r => r.Point == target.Point) ?? references.FirstOrDefault();
                value = reference != null && Get(reference, refType) is { } referenced ? referenced * fact : null;
            }
            else
            {
                value = Number(constraint, "val") ?? (op == "equ" && constraint.Attribute("val") == null ? null : 0);
            }
            if (value is not { } v || double.IsNaN(v))
                continue;
            switch (op)
            {
                case "gte":
                    if (target.Values.TryGetValue(type, out var low))
                        target.Values[type] = Math.Max(low, v);
                    else
                        target.Values[type] = v;
                    break;
                case "lte":
                    if (target.Values.TryGetValue(type, out var high))
                        target.Values[type] = Math.Min(high, v);
                    else
                        target.Values[type] = v;
                    break;
                default:
                    target.Values[type] = v;
                    break;
            }
        }
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
