using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Text;

namespace FreeSlate.Pptx.SmartArt;

/// <summary>
/// Lays out a SmartArt diagram without a saved drawing (catalog: smartart.layout). The layout tree is instantiated for
/// the data model and its constraints evaluated at the diagram's size; then every node, given its box by its parent's
/// algorithm, evaluates its constraint list again, so that its children's values follow its actual size (lengths that
/// follow font sizes stay), and its algorithm places them: composite where their values put them, lin one after the
/// other, snake in rows, connectors between their siblings. A composite takes the room of its children's bounding box
/// (measured: process3's composite, 1000 times as high as wide, is as high as its shapes); lin and snake shrink their
/// children uniformly when they do not fit and align them by the room they actually take. The text is then fitted by
/// the rules and the diagram laid out again with the fitted values until nothing changes. Lengths in millimeters.
/// </summary>
public static class Engine
{
    public const double PtPerMm = 72 / 25.4;

    /// <summary>
    /// Lays out the diagram in a frame of <paramref name="width"/> × <paramref name="height"/> points; with
    /// <paramref name="measure"/> (a text body laid out in a width, points) the text is fitted by the rules.
    /// </summary>
    public static LayoutNode Layout(XElement layoutDef, DataModel model, double width, double height, Func<XElement, double, LaidOutText>? measure = null)
    {
        var root = new LayoutTree(layoutDef, model).Root;
        var (w, h) = (width / PtPerMm, height / PtPerMm);
        var pins = new Pins();
        Run(root, w, h, pins);
        if (measure != null)
        {
            for (var round = 0; round < 8 && Fitting.Fit(root, measure, pins); round++)
                Run(root, w, h, pins);
        }
        return root;
    }

    /// <summary>Lays the tree out from scratch with the values the fitting fixed.</summary>
    static void Run(LayoutNode root, double w, double h, Pins pins)
    {
        foreach (var node in root.Descendants().Prepend(root))
        {
            node.Values.Clear();
            node.Rotation = 0;
            node.Scale = 1;
            (node.X, node.Y, node.W, node.H) = (0, 0, 0, 0);
        }
        root.Values["w"] = w;
        root.Values["h"] = h;
        Constraints.Evaluate(root, pins);
        Place(root, 0, 0, w, h, pins);
    }

    static double Value(LayoutNode node, string type, double fallback = 0) =>
        Constraints.Get(node, type) is { } v && double.IsFinite(v) ? v : fallback;

    static double? Finite(LayoutNode node, string type) =>
        Constraints.Get(node, type) is { } v && double.IsFinite(v) ? v : null;

    static double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    static bool IsTransition(LayoutNode node) => node.Point.IsTransition;

    /// <summary>Gives a node its box: its list is evaluated again for that size, then its algorithm places its children.</summary>
    static void Place(LayoutNode node, double x, double y, double w, double h, Pins pins)
    {
        // A composite keeps its aspect ratio (width : height) within its box, centered.
        if (node.AlgorithmType == "composite" && Number(node.Param("ar")) is > 0 and var ar && w > 0 && h > 0)
        {
            if (w / h > ar)
            {
                var fitted = h * ar;
                x += (w - fitted) / 2;
                w = fitted;
            }
            else
            {
                var fitted = w / ar;
                y += (h - fitted) / 2;
                h = fitted;
            }
        }
        (node.X, node.Y, node.W, node.H) = (x, y, w, h);
        node.Values["w"] = w;
        node.Values["h"] = h;
        Constraints.EvaluateList(node, pins);
        switch (node.AlgorithmType)
        {
            case "lin":
                Linear(node, pins);
                break;
            case "snake":
                Snake(node, pins);
                break;
            default:
                Composite(node, pins);
                break;
        }
        // Connectors are placed between the nodes their parent placed.
        foreach (var child in node.Children.Where(c => c.AlgorithmType == "conn"))
            Connector(node, child, pins);
    }

    /// <summary>A child's box in its parent's (l, t, r, b, ctrX, ctrY, w, h; the parent's size where none is given).</summary>
    static (double X, double Y, double W, double H) ChildRect(LayoutNode child, double parentW, double parentH)
    {
        var (cw, ch) = (Value(child, "w", parentW), Value(child, "h", parentH));
        var cx = Finite(child, "l") ?? (Finite(child, "ctrX") is { } cX ? cX - cw / 2 : Finite(child, "r") is { } r ? r - cw : 0);
        var cy = Finite(child, "t") ?? (Finite(child, "ctrY") is { } cY ? cY - ch / 2 : Finite(child, "b") is { } b ? b - ch : 0);
        return (cx, cy, cw, ch);
    }

    /// <summary>The composite algorithm: the children where their values put them in the node's box.</summary>
    static void Composite(LayoutNode node, Pins pins)
    {
        foreach (var child in node.Children.Where(c => c.AlgorithmType != "conn"))
        {
            var (cx, cy, cw, ch) = ChildRect(child, node.W, node.H);
            Place(child, node.X + cx, node.Y + cy, cw, ch, pins);
        }
    }

    /// <summary>
    /// The room a node takes in its parent's layout, relative to its own origin, from its current values: its size; for
    /// a composite the bounding box of its children; for a lin node without a finite size its children one after the
    /// other.
    /// </summary>
    static (double X, double Y, double W, double H) Extent(LayoutNode node)
    {
        var (w, h) = (Finite(node, "w"), Finite(node, "h"));
        var children = node.Children.Where(c => c.AlgorithmType != "conn").ToList();
        if (node.AlgorithmType is "composite" && children.Count > 0)
        {
            var (pw, ph) = (w ?? 0, h ?? 0);
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var child in children)
            {
                var (cx, cy, _, _) = ChildRect(child, pw, ph);
                var e = Extent(child);
                minX = Math.Min(minX, cx + e.X);
                minY = Math.Min(minY, cy + e.Y);
                maxX = Math.Max(maxX, cx + e.X + e.W);
                maxY = Math.Max(maxY, cy + e.Y + e.H);
            }
            return (minX, minY, maxX - minX, maxY - minY);
        }
        if (node.AlgorithmType == "lin" && children.Count > 0 && (w == null || h == null))
        {
            var horizontal = (node.Param("linDir") ?? "fromL") is "fromL" or "fromR";
            var extents = children.Select(Extent).ToList();
            var gap = Value(node, "sp") * (children.Count - 1);
            var along = extents.Sum(e => horizontal ? e.W : e.H) + gap;
            var across = extents.Max(e => horizontal ? e.H : e.W);
            var (ew, eh) = horizontal ? (along, across) : (across, along);
            return (0, 0, w ?? ew, h ?? eh);
        }
        return (0, 0, w ?? 0, h ?? 0);
    }

    /// <summary>The room a placed subtree takes: the boxes of its nodes that draw (a shape type), else its own box.</summary>
    static (double X, double Y, double W, double H) Bounds(LayoutNode node)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var n in node.Descendants().Prepend(node))
        {
            if ((string?)n.Shape?.Attribute("type") is null or "none" || n.AlgorithmType == "conn")
                continue;
            minX = Math.Min(minX, Math.Min(n.X, n.X + n.W));
            minY = Math.Min(minY, Math.Min(n.Y, n.Y + n.H));
            maxX = Math.Max(maxX, Math.Max(n.X, n.X + n.W));
            maxY = Math.Max(maxY, Math.Max(n.Y, n.Y + n.H));
        }
        return minX == double.MaxValue ? (node.X, node.Y, node.W, node.H) : (minX, minY, maxX - minX, maxY - minY);
    }

    static void Translate(LayoutNode node, double dx, double dy)
    {
        if (Math.Abs(dx) < 1e-12 && Math.Abs(dy) < 1e-12)
            return;
        foreach (var n in node.Descendants().Prepend(node))
        {
            n.X += dx;
            n.Y += dy;
        }
    }

    /// <summary>
    /// Places a child scaled by <paramref name="factor"/>: its own size scaled (a composite's as wide as its own width,
    /// as high as its room unless it has a sensible height of its own), its room's corner at the origin.
    /// </summary>
    static void PlaceScaled(LayoutNode child, (double X, double Y, double W, double H) extent, double factor, Pins pins)
    {
        child.Scale = factor;
        var w = Finite(child, "w") ?? extent.X + extent.W;
        var h = Finite(child, "h") ?? extent.Y + extent.H;
        if (child.AlgorithmType == "composite" && h > 100 * Math.Max(w, 1e-6))
            h = extent.Y + extent.H;
        Place(child, -extent.X * factor, -extent.Y * factor, w * factor, h * factor, pins);
    }

    static double Align(string? alignment, double room, double size) => alignment switch
    {
        "l" or "t" => 0,
        "r" or "b" => room - size,
        _ => (room - size) / 2,
    };

    /// <summary>
    /// The linear algorithm: the children one after another (linDir) by the room they take, scaled uniformly to fit
    /// the node when they do not, the row aligned in the node (horzAlign, vertAlign; centered) and each child across it
    /// (nodeHorzAlign, nodeVertAlign; centered). Negative room (a negative height) overlaps the neighbors.
    /// </summary>
    static void Linear(LayoutNode node, Pins pins)
    {
        var children = node.Children.Where(c => c.AlgorithmType != "conn" || Constraints.Get(c, "w") != null).ToList();
        if (children.Count == 0)
            return;
        var direction = node.Param("linDir") ?? "fromL";
        var horizontal = direction is "fromL" or "fromR";
        var gap = Value(node, "sp");
        var extents = children.Select(Extent).ToList();
        var along = extents.Sum(e => horizontal ? e.W : e.H) + gap * (children.Count - 1);
        var across = extents.Max(e => horizontal ? e.H : e.W);
        var (boxAlong, boxAcross) = horizontal ? (node.W, node.H) : (node.H, node.W);
        var factor = Math.Min(along > 0 ? boxAlong / along : 1, across > 0 ? boxAcross / across : 1);
        if (!double.IsFinite(factor) || factor <= 0)
            factor = 1;
        for (var i = 0; i < children.Count; i++)
            PlaceScaled(children[i], extents[i], factor, pins);

        var bounds = children.Select(Bounds).ToList();
        var total = bounds.Sum(b => horizontal ? b.W : b.H) + gap * factor * (children.Count - 1);
        var start = Align(horizontal ? node.Param("horzAlign") : node.Param("vertAlign"), boxAlong, total);
        var crossAlignment = horizontal ? node.Param("nodeVertAlign") : node.Param("nodeHorzAlign");
        var order = direction is "fromR" or "fromB" ? Enumerable.Range(0, children.Count).Reverse() : Enumerable.Range(0, children.Count);
        var position = start;
        foreach (var i in order)
        {
            var b = bounds[i];
            var (size, cross) = horizontal ? (b.W, b.H) : (b.H, b.W);
            var offset = Align(crossAlignment, boxAcross, cross);
            if (horizontal)
                Translate(children[i], node.X + position - b.X, node.Y + offset - b.Y);
            else
                Translate(children[i], node.X + offset - b.X, node.Y + position - b.Y);
            position += size + gap * factor;
        }
    }

    /// <summary>
    /// The snake algorithm: the nodes in rows (or columns) of equal cells, as many per row as make them largest (more
    /// per row when equal), the spacing sp between them, each row centered, the grid centered in the node.
    /// </summary>
    static void Snake(LayoutNode node, Pins pins)
    {
        var cells = node.Children.Where(c => !IsTransition(c) && c.AlgorithmType != "conn").ToList();
        if (cells.Count == 0)
            return;
        var extents = cells.Select(Extent).ToList();
        var (cw, ch) = (extents.Max(e => e.W), extents.Max(e => e.H));
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
            PlaceScaled(cell, extents[i], k, pins);
            var b = Bounds(cell);
            Translate(cell, node.X + x + (scw - b.W) / 2 - b.X, node.Y + y + (sch - b.H) / 2 - b.Y);
        }
    }

    /// <summary>
    /// A connector between the siblings before and after it (begPad and endPad from their facing edges), as thick as
    /// its constraints make it for its place among its siblings, centered between them.
    /// </summary>
    static void Connector(LayoutNode parent, LayoutNode connector, Pins pins)
    {
        var index = parent.Children.IndexOf(connector);
        var before = parent.Children.Take(index).LastOrDefault(c => !IsTransition(c) && c.AlgorithmType != "conn");
        var after = parent.Children.Skip(index + 1).FirstOrDefault(c => !IsTransition(c) && c.AlgorithmType != "conn");
        if (before == null || after == null)
            return;
        var (a, b) = (Bounds(before), Bounds(after));
        var (ax, ay) = (a.X + a.W / 2, a.Y + a.H / 2);
        var (bx, by) = (b.X + b.W / 2, b.Y + b.H / 2);
        var horizontal = Math.Abs(bx - ax) >= Math.Abs(by - ay);
        double gapStart, gapEnd;
        if (horizontal)
            (gapStart, gapEnd) = bx >= ax ? (a.X + a.W, b.X) : (a.X, b.X + b.W);
        else
            (gapStart, gapEnd) = by >= ay ? (a.Y + a.H, b.Y) : (a.Y, b.Y + b.H);
        var distance = Math.Abs(gapEnd - gapStart);
        connector.Values["connDist"] = distance;
        // The pads refer to the distance: evaluate the connector's own constraints with it.
        foreach (var constraint in connector.Constraints)
        {
            var type = (string?)constraint.Attribute("type");
            if (type is "begPad" or "endPad" && (string?)constraint.Attribute("refType") == "connDist")
                connector.Values[type] = distance * (Number((string?)constraint.Attribute("fact")) ?? 1);
        }
        var begPad = Value(connector, "begPad");
        var endPad = Value(connector, "endPad");
        var length = Math.Max(0, distance - begPad - endPad);
        // As thick as its place among its siblings, else as its own height or 0.62 of its length.
        var thickness = connector.H > 0 ? connector.H : Finite(connector, "h") ?? length * 0.62;
        var sign = (horizontal ? bx - ax : by - ay) >= 0 ? 1 : -1;
        var from = gapStart + sign * begPad;
        if (horizontal)
        {
            var left = sign > 0 ? from : from - length;
            PlaceConnector(connector, left, (ay + by) / 2 - thickness / 2, length, thickness, pins);
            connector.Rotation = sign > 0 ? 0 : 180;
        }
        else
        {
            // Drawn as a horizontal arrow turned about its center.
            var center = (X: (ax + bx) / 2, Y: from + sign * length / 2);
            PlaceConnector(connector, center.X - length / 2, center.Y - thickness / 2, length, thickness, pins);
            connector.Rotation = sign > 0 ? 90 : 270;
        }
    }

    /// <summary>Gives a connector its box and its children theirs in it.</summary>
    static void PlaceConnector(LayoutNode connector, double x, double y, double w, double h, Pins pins)
    {
        (connector.X, connector.Y, connector.W, connector.H) = (x, y, w, h);
        foreach (var child in connector.Children)
        {
            var (cx, cy, cw, ch) = ChildRect(child, w, h);
            Place(child, x + cx, y + cy, cw, ch, pins);
        }
    }
}
