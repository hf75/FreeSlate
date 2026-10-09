using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Text;

namespace FreeSlate.Pptx.SmartArt;

/// <summary>
/// Fits the text of a laid-out SmartArt diagram (catalog: smartart.layout): a node whose text does not fit its box
/// (inside its geometry's text rectangle and its margins, without breaking words) applies its rules in order, its own
/// before those of its ancestors: a font size rule lowers the font size (whole points) to the rule's value, a size rule
/// lets the node grow in sixteenths of its height to the rule's limit (a factor of its size, or INF: as far as its
/// parent has room). Nodes of the same name then take the smallest font size, nodes made equal by an equ constraint
/// the largest height. The values found are pinned and the diagram laid out again (by the engine) until nothing
/// changes.
/// </summary>
static class Fitting
{
    const double DefaultFontSize = 65;

    /// <summary>One round of fitting; whether it pinned anything new.</summary>
    public static bool Fit(LayoutNode root, Func<XElement, double, LaidOutText> measure, Pins pins)
    {
        var nodes = root.Descendants().Prepend(root).Where(n => n.Text.Count > 0 && n.AlgorithmType == "tx").ToList();
        var changed = false;
        foreach (var node in nodes)
        {
            var font = Font(node);
            changed |= FitNode(node, measure, pins);
            // A smaller font changes the text of the nodes that take their size from it: they are checked again in the
            // next round, laid out with it, before they grow.
            if (Font(node) < font - 1e-6)
                break;
        }
        foreach (var group in nodes.GroupBy(n => n.Name))
        {
            var smallest = group.Min(Font);
            foreach (var node in group)
            {
                if (Font(node) > smallest + 1e-6)
                {
                    PinFont(node, smallest, pins);
                    changed = true;
                }
            }
        }
        changed |= Equalize(root, pins);
        return changed;
    }

    public static double Font(LayoutNode node) => node.Values.TryGetValue("primFontSz", out var v) && double.IsFinite(v) ? v : DefaultFontSize;

    public static double SecondaryFont(LayoutNode node) =>
        node.Values.TryGetValue("secFontSz", out var s) && double.IsFinite(s) ? s : Font(node) * 0.78;

    /// <summary>Sets a node's font size for a trial: margins given in font sizes follow it.</summary>
    static void SetFont(LayoutNode node, double size)
    {
        var old = Font(node);
        node.Values["primFontSz"] = size;
        foreach (var side in new[] { "lMarg", "rMarg", "tMarg", "bMarg" })
        {
            if (MarginFromFont(node, side) is { } factor)
                node.Values[side] = factor * size;
        }
        if (node.Values.TryGetValue("secFontSz", out var secondary) && old > 0)
            node.Values["secFontSz"] = secondary * size / old;
    }

    static void PinFont(LayoutNode node, double size, Pins pins)
    {
        var old = Font(node);
        SetFont(node, size);
        pins.Set(node, "primFontSz", size);
        if (node.Values.TryGetValue("secFontSz", out var secondary) && old > 0)
            pins.Set(node, "secFontSz", secondary);
    }

    /// <summary>The factor of a margin constraint on the primary font size, if the node's margin is one.</summary>
    static double? MarginFromFont(LayoutNode node, string side)
    {
        foreach (var constraint in node.Constraints)
        {
            if ((string?)constraint.Attribute("type") == side && (string?)constraint.Attribute("refType") == "primFontSz"
                && (string?)constraint.Attribute("for") is null or "self")
                return double.TryParse((string?)constraint.Attribute("fact"), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 1;
        }
        return null;
    }

    /// <summary>Whether the node's text fits its box at its current font size, and the height it needs (points).</summary>
    static (bool Fits, double Needed) Check(LayoutNode node, Func<XElement, double, LaidOutText> measure)
    {
        var primary = Font(node);
        var body = TextBody.Build(node, primary, SecondaryFont(node));
        var (l, r) = (TextBody.Margin(node, "lMarg", primary), TextBody.Margin(node, "rMarg", primary));
        var (t, b) = (TextBody.Margin(node, "tMarg", primary), TextBody.Margin(node, "bMarg", primary));
        // The text lies in the geometry's text rectangle (a rounded rectangle's inside its corners), within the margins.
        var (areaWidth, _) = Output.TextArea(node);
        var width = areaWidth * Engine.PtPerMm - l - r;
        if (width <= 0)
            return (false, double.PositiveInfinity);
        var layout = measure(body, width);
        var overflow = layout.Lines.Any(line => line.Width > width + 0.01) || BrokenWord(layout);
        var needed = TextHeight(layout, body) + t + b;
        node.Note = string.Create(CultureInfo.InvariantCulture, $"font {primary:0.#} text {layout.Height:0.##} lines {layout.Lines.Count} margins {t:0.#}/{b:0.#} width {width:0.#}{(overflow ? " overflow" : "")}");
        return (!overflow && needed <= node.H * Engine.PtPerMm + 0.01, needed);
    }

    /// <summary>The height of a line in SmartArt's text measure, in em (measured with Aptos: one line 1.3264 to 1.3346).</summary>
    const double LineHeight = 1.33;

    /// <summary>
    /// The height SmartArt gives the text (points), not the drawn lines' height: the first line one line height L, every
    /// further line 0.9 L, and after a paragraph its space after (spcAft, % of L) before the next (measured with 1 to 4
    /// paragraphs at 42, 56 and 65 pt, all within 0.1 %); L of each line's largest font size.
    /// </summary>
    static double TextHeight(LaidOutText layout, XElement body)
    {
        var paragraphs = body.Elements(A + "p").ToList();
        double total = 0;
        var previous = -1;
        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];
            var size = line.Runs.Count > 0 ? line.Runs.Max(r => r.Style.Size) : 0;
            var height = LineHeight * size;
            if (i == 0)
                total += height;
            else
            {
                total += 0.9 * height;
                if (line.Paragraph != previous && previous >= 0 && previous < paragraphs.Count)
                    total += SpaceAfter(paragraphs[previous]) * LineHeight * PreviousSize(layout, i);
            }
            previous = line.Paragraph;
        }
        return total;
    }

    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    static double SpaceAfter(XElement paragraph) =>
        (paragraph.Element(A + "pPr")?.Element(A + "spcAft")?.Element(A + "spcPct")?.Attribute("val") is { } v ? (long)v : 0) / 100000.0;

    static double PreviousSize(LaidOutText layout, int index)
    {
        var line = layout.Lines[index - 1];
        return line.Runs.Count > 0 ? line.Runs.Max(r => r.Style.Size) : 0;
    }

    /// <summary>A word broken across lines: a line ending without a space continued by one in the same paragraph.</summary>
    static bool BrokenWord(LaidOutText layout)
    {
        for (var i = 0; i + 1 < layout.Lines.Count; i++)
        {
            var (line, next) = (layout.Lines[i], layout.Lines[i + 1]);
            if (line.Paragraph != next.Paragraph || line.Runs.Count == 0 || next.Runs.Count == 0)
                continue;
            var end = line.Runs[^1].Text;
            var start = next.Runs[0].Text;
            if (end.Length > 0 && start.Length > 0 && !char.IsWhiteSpace(end[^1]) && end[^1] != '-' && !char.IsWhiteSpace(start[0]))
                return true;
        }
        return false;
    }

    /// <summary>The rules applying to a node: its own, then those of its ancestors that target it.</summary>
    static List<XElement> Rules(LayoutNode node)
    {
        var result = node.Rules.Where(r => (string?)r.Attribute("for") is null or "self").ToList();
        for (var ancestor = node.Parent; ancestor != null; ancestor = ancestor.Parent)
        {
            foreach (var rule in ancestor.Rules)
            {
                if (Constraints.Targets(ancestor, (string?)rule.Attribute("for"), (string?)rule.Attribute("forName"), (string?)rule.Attribute("ptType")).Contains(node))
                    result.Add(rule);
            }
        }
        return result;
    }

    static double? Number(XElement rule, string attribute)
    {
        var text = (string?)rule.Attribute(attribute);
        if (text == "INF")
            return double.PositiveInfinity;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && !double.IsNaN(v) ? v : null;
    }

    /// <summary>The room a placed subtree takes (its drawing nodes' boxes).</summary>
    static (double X, double Y, double W, double H) Taken(LayoutNode node)
    {
        double minY = double.MaxValue, maxY = double.MinValue, minX = double.MaxValue, maxX = double.MinValue;
        foreach (var n in node.Descendants().Prepend(node))
        {
            if ((string?)n.Shape?.Attribute("type") is null or "none")
                continue;
            minX = Math.Min(minX, n.X);
            minY = Math.Min(minY, n.Y);
            maxX = Math.Max(maxX, n.X + n.W);
            maxY = Math.Max(maxY, n.Y + n.H);
        }
        return minY == double.MaxValue ? (node.X, node.Y, node.W, node.H) : (minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// How high a node may grow without its diagram having to shrink (mm): by the room left in the nearest lin or snake
    /// ancestor along a vertical line (or across a horizontal one), else in the diagram.
    /// </summary>
    static double Room(LayoutNode node)
    {
        var child = node;
        for (var ancestor = node.Parent; ancestor != null; child = ancestor, ancestor = ancestor.Parent)
        {
            if (ancestor.AlgorithmType is not ("lin" or "snake"))
                continue;
            var vertical = ancestor.AlgorithmType == "lin" && (ancestor.Param("linDir") ?? "fromL") is "fromT" or "fromB";
            if (vertical)
            {
                var used = ancestor.Children.Where(c => c.AlgorithmType != "conn").Sum(c => Taken(c).H);
                return node.H + Math.Max(0, ancestor.H - used);
            }
            return node.H + Math.Max(0, ancestor.H - Taken(child).H);
        }
        var root = node;
        while (root.Parent != null)
            root = root.Parent;
        return node.H + Math.Max(0, root.H - Taken(root).H);
    }

    static bool FitNode(LayoutNode node, Func<XElement, double, LaidOutText> measure, Pins pins)
    {
        if (Check(node, measure).Fits)
            return false;
        var changed = false;
        var startFont = Font(node);
        if (!pins.StartHeight.TryGetValue(node, out var startHeight))
            pins.StartHeight[node] = startHeight = node.H;
        foreach (var rule in Rules(node))
        {
            var type = (string?)rule.Attribute("type");
            if (type is "primFontSz" or "secFontSz")
            {
                var limit = Number(rule, "val") ?? (Number(rule, "fact") is { } f ? f * startFont : 5);
                // The largest whole size down to the limit that fits (binary search: smaller sizes fit as well).
                var (low, high) = ((int)Math.Ceiling(limit), (int)Math.Floor(Font(node)));
                if (high < low)
                    continue;
                SetFont(node, low);
                if (!Check(node, measure).Fits)
                {
                    PinFont(node, low, pins);
                    changed = true;
                    continue;
                }
                while (low < high)
                {
                    var middle = (low + high + 1) / 2;
                    SetFont(node, middle);
                    if (Check(node, measure).Fits)
                        low = middle;
                    else
                        high = middle - 1;
                }
                PinFont(node, low, pins);
                return true;
            }
            if (type is "h")
            {
                // The node grows in steps of a sixteenth of its height until the text fits (measured: 21/16, 39/16 …), as
                // far as the rule and its parent's room allow.
                var limit = Number(rule, "val") is { } v ? v : Number(rule, "fact") is { } f ? f * startHeight : startHeight;
                limit = Math.Min(limit, Room(node));
                if (Constraints.UpperBound(node, "h") is { } upper)
                    limit = Math.Min(limit, Math.Max(upper * node.Scale, node.H));
                var wanted = Check(node, measure).Needed / Engine.PtPerMm;
                if (startHeight > 0)
                    wanted = Math.Ceiling(wanted / startHeight * 16 - 1e-9) / 16 * startHeight;
                var height = wanted;
                if (wanted > limit + 1e-6)
                    height = startHeight > 0 ? Math.Floor(limit / startHeight * 16 + 1e-9) / 16 * startHeight : limit;
                if (height > node.H + 1e-6)
                {
                    pins.Set(node, "h", height / Math.Max(node.Scale, 1e-9));
                    node.H = height;
                    node.Values["h"] = height;
                    changed = true;
                }
                if (wanted <= limit + 1e-6)
                    return true;
            }
        }
        return changed;
    }

    /// <summary>Makes sizes equal (the largest) where an equ constraint without a value asks for it.</summary>
    static bool Equalize(LayoutNode root, Pins pins)
    {
        var changed = false;
        foreach (var node in root.Descendants().Prepend(root))
        {
            foreach (var constraint in node.Constraints)
            {
                if ((string?)constraint.Attribute("op") != "equ" || constraint.Attribute("val") != null || constraint.Attribute("refType") != null)
                    continue;
                var type = (string?)constraint.Attribute("type");
                if (type is not ("h" or "w"))
                    continue;
                var targets = Constraints.Targets(node, (string?)constraint.Attribute("for"), (string?)constraint.Attribute("forName"), (string?)constraint.Attribute("ptType"));
                if (targets.Count < 2)
                    continue;
                double Size(LayoutNode n) => type == "h" ? n.H : n.W;
                var largest = targets.Max(Size);
                foreach (var target in targets)
                {
                    if (Size(target) < largest - 1e-6)
                    {
                        pins.Set(target, type, largest / Math.Max(target.Scale, 1e-9));
                        changed = true;
                    }
                }
            }
        }
        return changed;
    }
}
