using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Text;

namespace FreeSlate.Pptx.SmartArt;

/// <summary>
/// Lays out a SmartArt diagram without a saved drawing (catalog: smartart.layout): the layout tree is instantiated
/// for the data model, its constraints are evaluated once at the diagram's size, and the algorithms place every
/// node's children top-down, scaling each child's subtree uniformly to the box they give it (proportions set by
/// constraints are kept; font sizes and text margins are not lengths and stay). Lengths are in millimeters.
/// </summary>
public static class Engine
{
    public const double PtPerMm = 72 / 25.4;

    /// <summary>The value names that are lengths and scale with their node.</summary>
    static readonly HashSet<string> Lengths =
    [
        "w", "h", "l", "t", "r", "b", "ctrX", "ctrY", "lOff", "tOff", "rOff", "bOff", "wOff", "hOff", "ctrXOff", "ctrYOff",
        "sp", "sibSp", "secSibSp", "begPad", "endPad", "connDist", "diam", "bendDist", "alignOff", "stemThick", "hArH", "wArH",
        "begMarg", "endMarg",
    ];

    /// <summary>
    /// Lays out the diagram in a frame of <paramref name="width"/> × <paramref name="height"/> points; with
    /// <paramref name="measure"/> (a text body laid out in a width, points) the text is fitted by the rules.
    /// </summary>
    public static LayoutNode Layout(XElement layoutDef, DataModel model, double width, double height, Func<XElement, double, LaidOutText>? measure = null)
    {
        var tree = new LayoutTree(layoutDef, model);
        var root = tree.Root;
        var (w, h) = (width / PtPerMm, height / PtPerMm);
        root.Values["w"] = w;
        root.Values["h"] = h;
        Constraints.Evaluate(root);
        Place(root, 0, 0, w, h);
        if (measure != null)
            Fitting.Fit(root, measure, () => Place(root, 0, 0, w, h));
        return root;
    }

    /// <summary>Multiplies the lengths of a node and its subtree by <paramref name="factor"/>.</summary>
    static void Scale(LayoutNode node, double factor)
    {
        if (Math.Abs(factor - 1) < 1e-12)
            return;
        foreach (var n in node.Descendants().Prepend(node))
        {
            foreach (var key in n.Values.Keys.ToList())
            {
                if (Lengths.Contains(key) || key.StartsWith("user", StringComparison.Ordinal))
                    n.Values[key] *= factor;
            }
        }
    }

    static double Value(LayoutNode node, string type, double fallback = 0) =>
        Constraints.Get(node, type) is { } v && !double.IsNaN(v) && !double.IsInfinity(v) ? v : fallback;

    /// <summary>Gives a node its box and lets its algorithm place its children.</summary>
    static void Place(LayoutNode node, double x, double y, double w, double h)
    {
        (node.X, node.Y, node.W, node.H) = (x, y, w, h);
        switch (node.AlgorithmType)
        {
            case "lin":
                Linear(node);
                break;
            case "snake":
                Snake(node);
                break;
            case "sp" or "tx":
                foreach (var child in node.Children)
                    Composite1(node, child);
                break;
            default:
                Composite(node);
                break;
        }
        // Connectors are placed between the nodes their parent placed.
        foreach (var child in node.Children.Where(c => c.AlgorithmType == "conn"))
            Connector(node, child);
    }

    /// <summary>
    /// The composite algorithm: children where their constraints put them in the node (l, t, r, b, ctrX, ctrY, w, h),
    /// after fitting the node to its aspect ratio (ar) and scaling the children with it.
    /// </summary>
    static void Composite(LayoutNode node)
    {
        var (x, y, w, h) = (node.X, node.Y, node.W, node.H);
        var ar = Number(node.Param("ar"));
        if (ar is > 0)
        {
            // The composite keeps its aspect ratio (width : height) within its box, centered.
            if (w / h > ar)
            {
                var fitted = h * ar.Value;
                x += (w - fitted) / 2;
                w = fitted;
            }
            else
            {
                var fitted = w / ar.Value;
                y += (h - fitted) / 2;
                h = fitted;
            }
            (node.X, node.Y, node.W, node.H) = (x, y, w, h);
        }
        // The children's values are relative to the node's own size: scale them to the box.
        var own = Value(node, "w", w);
        var factor = own > 0 ? w / own : 1;
        foreach (var child in node.Children)
            Scale(child, factor);
        node.Values["w"] = w;
        node.Values["h"] = h;
        foreach (var child in node.Children.Where(c => c.AlgorithmType != "conn"))
            Composite1(node, child);
    }

    /// <summary>One child of a composite: its box from its values, relative to the parent's box.</summary>
    static void Composite1(LayoutNode parent, LayoutNode child)
    {
        var (cw, ch) = (Value(child, "w", parent.W), Value(child, "h", parent.H));
        double cx = Constraints.Get(child, "l") ?? (Constraints.Get(child, "ctrX") is { } cX ? cX - cw / 2 : Constraints.Get(child, "r") is { } r ? r - cw : 0);
        double cy = Constraints.Get(child, "t") ?? (Constraints.Get(child, "ctrY") is { } cY ? cY - ch / 2 : Constraints.Get(child, "b") is { } b ? b - ch : 0);
        Place(child, parent.X + cx, parent.Y + cy, cw, ch);
    }

    static double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    static bool IsTransition(LayoutNode node) => node.Point.IsTransition;

    /// <summary>
    /// The linear algorithm: the children one after another (linDir), each at its size, all scaled uniformly to fit the
    /// node, centered across and along it.
    /// </summary>
    static void Linear(LayoutNode node)
    {
        var children = node.Children.Where(c => c.AlgorithmType != "conn" || c.Values.ContainsKey("w")).ToList();
        if (children.Count == 0)
            return;
        var direction = node.Param("linDir") ?? "fromL";
        var horizontal = direction is "fromL" or "fromR";
        var gap = Value(node, "sp");
        var sizes = children.Select(c => (W: Value(c, "w"), H: Value(c, "h"))).ToList();
        var along = sizes.Sum(s => horizontal ? s.W : s.H) + gap * (children.Count - 1);
        var across = sizes.Max(s => horizontal ? s.H : s.W);
        var (boxAlong, boxAcross) = horizontal ? (node.W, node.H) : (node.H, node.W);
        var factor = Math.Min(along > 0 ? boxAlong / along : 1, across > 0 ? boxAcross / across : 1);
        var position = (boxAlong - along * factor) / 2;
        var order = direction is "fromR" or "fromB" ? Enumerable.Range(0, children.Count).Reverse() : Enumerable.Range(0, children.Count);
        foreach (var i in order)
        {
            var child = children[i];
            Scale(child, factor);
            var (cw, ch) = (sizes[i].W * factor, sizes[i].H * factor);
            var size = horizontal ? cw : ch;
            var cross = ((horizontal ? node.H : node.W) - (horizontal ? ch : cw)) / 2;
            if (horizontal)
                Place(child, node.X + position, node.Y + cross, cw, ch);
            else
                Place(child, node.X + cross, node.Y + position, cw, ch);
            position += size + gap * factor;
        }
    }

    /// <summary>
    /// The snake algorithm: the nodes in rows (or columns) of equal cells, as many per row as make them largest (more
    /// per row when equal), the spacing sp between them, each row centered, the grid centered in the node.
    /// </summary>
    static void Snake(LayoutNode node)
    {
        var cells = node.Children.Where(c => !IsTransition(c)).ToList();
        if (cells.Count == 0)
            return;
        var (cw, ch) = (cells.Max(c => Value(c, "w")), cells.Max(c => Value(c, "h")));
        if (cw <= 0 || ch <= 0)
            return;
        var gap = Value(node, "sp");
        var columns = node.Param("flowDir") == "col";
        var n = cells.Count;
        var best = (Factor: 0.0, Count: 1);
        for (var count = 1; count <= n; count++)
        {
            var lines = (n + count - 1) / count;
            var (gw, gh) = columns ? (lines * cw + (lines - 1) * gap, count * ch + (count - 1) * gap) : (count * cw + (count - 1) * gap, lines * ch + (lines - 1) * gap);
            var factor = Math.Min(node.W / gw, node.H / gh);
            if (factor > best.Factor * (1 + 1e-9))
                best = (factor, count);
        }
        var k = best.Factor;
        // As many cells per line as fit at that size.
        var perLine = columns
            ? Math.Max(1, (int)Math.Floor((node.H + gap * k) / ((ch + gap) * k) + 1e-9))
            : Math.Max(1, (int)Math.Floor((node.W + gap * k) / ((cw + gap) * k) + 1e-9));
        perLine = Math.Min(perLine, n);
        var lineCount = (n + perLine - 1) / perLine;
        var (scw, sch, sgap) = (cw * k, ch * k, gap * k);
        var gridAcross = lineCount * (columns ? scw : sch) + (lineCount - 1) * sgap;
        var start = ((columns ? node.W : node.H) - gridAcross) / 2;
        var grow = node.Param("grDir") ?? "tL";
        var reverse = node.Param("contDir") == "revDir";
        for (var i = 0; i < n; i++)
        {
            var (line, index) = (i / perLine, i % perLine);
            var inLine = Math.Min(perLine, n - line * perLine);
            if (reverse && line % 2 == 1)
                index = inLine - 1 - index;
            var lineLength = inLine * (columns ? sch : scw) + (inLine - 1) * sgap;
            var offset = ((columns ? node.H : node.W) - lineLength) / 2 + index * ((columns ? sch : scw) + sgap);
            var across = start + line * ((columns ? scw : sch) + sgap);
            double x, y;
            if (columns)
                (x, y) = (across, offset);
            else
                (x, y) = (offset, across);
            if (grow is "tR" or "bR")
                x = node.W - x - scw;
            if (grow is "bL" or "bR")
                y = node.H - y - sch;
            var cell = cells[i];
            Scale(cell, k);
            var (w, h) = (Value(cell, "w") , Value(cell, "h"));
            Place(cell, node.X + x + (scw - w) / 2, node.Y + y + (sch - h) / 2, w, h);
        }
    }

    /// <summary>
    /// A connector between the siblings before and after it (begPad and endPad from their facing edges), as high as its
    /// constraints make it, centered between them.
    /// </summary>
    static void Connector(LayoutNode parent, LayoutNode connector)
    {
        var index = parent.Children.IndexOf(connector);
        var before = parent.Children.Take(index).LastOrDefault(c => !IsTransition(c) && c.AlgorithmType != "conn");
        var after = parent.Children.Skip(index + 1).FirstOrDefault(c => !IsTransition(c) && c.AlgorithmType != "conn");
        if (before == null || after == null)
            return;
        var (ax, ay) = (before.X + before.W / 2, before.Y + before.H / 2);
        var (bx, by) = (after.X + after.W / 2, after.Y + after.H / 2);
        var horizontal = Math.Abs(bx - ax) >= Math.Abs(by - ay);
        double gapStart, gapEnd;
        if (horizontal)
            (gapStart, gapEnd) = bx >= ax ? (before.X + before.W, after.X) : (before.X, after.X + after.W);
        else
            (gapStart, gapEnd) = by >= ay ? (before.Y + before.H, after.Y) : (before.Y, after.Y + after.H);
        var distance = Math.Abs(gapEnd - gapStart);
        connector.Values["connDist"] = distance;
        // The pads refer to the distance: re-evaluate the connector's own constraints with it.
        foreach (var constraint in connector.Constraints)
        {
            var type = (string?)constraint.Attribute("type");
            if (type is "begPad" or "endPad" && (string?)constraint.Attribute("refType") == "connDist")
                connector.Values[type] = distance * (Number((string?)constraint.Attribute("fact")) ?? 1);
        }
        var begPad = Value(connector, "begPad");
        var endPad = Value(connector, "endPad");
        var length = Math.Max(0, distance - begPad - endPad);
        var thickness = Value(connector, "h", length * 0.62);
        var sign = (horizontal ? bx - ax : by - ay) >= 0 ? 1 : -1;
        var from = gapStart + sign * begPad;
        if (horizontal)
        {
            var left = sign > 0 ? from : from - length;
            PlaceConnector(connector, left, (ay + by) / 2 - thickness / 2, length, thickness);
            connector.Rotation = sign > 0 ? 0 : 180;
        }
        else
        {
            // Drawn as a horizontal arrow turned about its center.
            var center = (X: (ax + bx) / 2, Y: from + sign * length / 2);
            PlaceConnector(connector, center.X - length / 2, center.Y - thickness / 2, length, thickness);
            connector.Rotation = sign > 0 ? 90 : 270;
        }
    }

    /// <summary>
    /// Gives a connector its box and its children theirs in it, keeping the connector's values: they are the size of its
    /// place among its siblings, which the next layout (after fitting the text) needs again.
    /// </summary>
    static void PlaceConnector(LayoutNode connector, double x, double y, double w, double h)
    {
        (connector.X, connector.Y, connector.W, connector.H) = (x, y, w, h);
        foreach (var child in connector.Children)
            Composite1(connector, child);
    }
}
