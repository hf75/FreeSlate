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
            case "cycle":
                Cycle(node, pins);
                break;
            default:
                Composite(node, pins);
                break;
        }
        // Connectors are placed between the nodes their parent placed.
        foreach (var child in node.Children.Where(c => c.AlgorithmType == "conn"))
            Connector(child, pins);
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
    /// The cycle algorithm: the children around a circle, from stAng (degrees clockwise from the top) over spanAng in
    /// equal steps (the last at the end of the span unless it is a full circle), the first in the middle with ctrShpMap
    /// fNode; the circle as small as keeps neighbors sibSp apart (chord = size + sibSp) and the ring sp away from the
    /// middle one; everything then scaled uniformly to fit the node and centered (measured with cycle2 and radial1 at 1
    /// to 7 nodes, within 0.3 pt). With rotPath alongPath the shapes turn with the circle.
    /// </summary>
    static void Cycle(LayoutNode node, Pins pins)
    {
        var cells = node.Children.Where(c => !IsTransition(c) && c.AlgorithmType != "conn").ToList();
        if (cells.Count == 0)
            return;
        var start = Number(node.Param("stAng")) ?? 0;
        var span = Number(node.Param("spanAng")) ?? 360;
        var center = node.Param("ctrShpMap") == "fNode" && cells.Count > 1 ? cells[0] : null;
        var ring = center != null ? cells.Skip(1).ToList() : cells;
        var extents = cells.ToDictionary(c => c, Extent);
        var size = ring.Max(c => Math.Max(extents[c].W, extents[c].H));
        var n = ring.Count;
        var step = Math.Abs(Math.Abs(span) - 360) < 1e-9 ? span / n : n > 1 ? span / (n - 1) : 0;
        var radius = n > 1 && Math.Abs(step) > 1e-9 ? (size + Value(node, "sibSp")) / (2 * Math.Sin(Math.Abs(step) * Math.PI / 360)) : 0;
        if (center != null)
        {
            var middle = Math.Max(extents[center].W, extents[center].H);
            radius = Math.Max(radius, (middle + size) / 2 + Value(node, "sp"));
        }
        // The cells' centers about the circle's center, then the arrangement fitted to the node.
        var places = new Dictionary<LayoutNode, (double X, double Y, double Angle)>();
        if (center != null)
            places[center] = (0, 0, 0);
        for (var i = 0; i < n; i++)
        {
            var angle = start + i * step;
            var radians = angle * Math.PI / 180;
            places[ring[i]] = (radius * Math.Sin(radians), -radius * Math.Cos(radians), angle);
        }
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (cell, (px, py, _)) in places)
        {
            var e = extents[cell];
            minX = Math.Min(minX, px - e.W / 2);
            maxX = Math.Max(maxX, px + e.W / 2);
            minY = Math.Min(minY, py - e.H / 2);
            maxY = Math.Max(maxY, py + e.H / 2);
        }
        // Curved connectors run along the circle; their arcs' circle (0.65 of their thickness beyond it) takes room too.
        var arcs = node.Children.Where(c => c.AlgorithmType == "conn" && c.Param("connRout") is "curve" or "longCurve").ToList();
        if (arcs.Count > 0 && radius > 0)
        {
            var reach = radius + 0.65 * arcs.Max(c => Value(c, "h"));
            (minX, maxX, minY, maxY) = (Math.Min(minX, -reach), Math.Max(maxX, reach), Math.Min(minY, -reach), Math.Max(maxY, reach));
        }
        var (bw, bh) = (maxX - minX, maxY - minY);
        var factor = Math.Min(bw > 0 ? node.W / bw : 1, bh > 0 ? node.H / bh : 1);
        if (!double.IsFinite(factor) || factor <= 0)
            factor = 1;
        var (ox, oy) = (node.X + (node.W - bw * factor) / 2 - minX * factor, node.Y + (node.H - bh * factor) / 2 - minY * factor);
        var alongPath = node.Param("rotPath") == "alongPath";
        foreach (var (cell, (px, py, angle)) in places)
        {
            PlaceScaled(cell, extents[cell], factor, pins);
            var b = Bounds(cell);
            Translate(cell, ox + px * factor - (b.X + b.W / 2), oy + py * factor - (b.Y + b.H / 2));
            if (alongPath && cell != center)
                cell.Rotation = angle;
        }
        foreach (var connector in node.Children.Where(c => c.AlgorithmType == "conn"))
        {
            connector.Scale = factor;
            connector.Values["arcX"] = ox;
            connector.Values["arcY"] = oy;
            connector.Values["arcR"] = radius * factor;
            connector.Values["arcDir"] = Math.Sign(span == 0 ? 1 : span);
        }
    }

    /// <summary>
    /// The layout nodes a connector joins: those showing the transition's nodes (a sibling transition from its node to
    /// the next, the first after the last; a parent transition from the parent to its node), by name where srcNode and
    /// dstNode give one, else the first that draws a shape.
    /// </summary>
    static (LayoutNode? From, LayoutNode? To) Ends(LayoutNode connector)
    {
        var point = connector.Point;
        DataPoint? from = null, to = null;
        if (point.Type == "sibTrans" && point.Owner is { } owner)
        {
            from = owner;
            var siblings = owner.Parent?.Children.Where(c => c.IsNode).ToList() ?? [];
            var index = siblings.IndexOf(owner);
            to = index >= 0 && index + 1 < siblings.Count ? siblings[index + 1] : siblings.FirstOrDefault();
        }
        else if (point.Type == "parTrans" && point.Owner is { } child)
            (from, to) = (child.Parent, child);
        var root = connector;
        while (root.Parent != null)
            root = root.Parent;
        var all = root.Descendants().Prepend(root).Where(n => n.AlgorithmType != "conn").ToList();
        LayoutNode? Find(DataPoint? p, string? name)
        {
            if (p == null)
                return null;
            var candidates = all.Where(n => n.Point == p).ToList();
            if (name != null)
                return candidates.FirstOrDefault(n => n.Name == name) ?? candidates.FirstOrDefault(Draws);
            return candidates.FirstOrDefault(n => Draws(n) && (string?)n.Shape?.Attribute("hideGeom") is not ("1" or "true"))
                   ?? candidates.FirstOrDefault(Draws);
        }
        var start = Find(from, connector.Param("srcNode"));
        var end = Find(to, connector.Param("dstNode"));
        if (start != null && end != null)
            return (start, end);
        // Without transitions: the siblings before and after the connector.
        var parent = connector.Parent;
        if (parent == null)
            return (null, null);
        var at = parent.Children.IndexOf(connector);
        return (parent.Children.Take(at).LastOrDefault(c => !IsTransition(c) && c.AlgorithmType != "conn"),
                parent.Children.Skip(at + 1).FirstOrDefault(c => !IsTransition(c) && c.AlgorithmType != "conn"));
    }

    static bool Draws(LayoutNode node) => (string?)node.Shape?.Attribute("type") is { } type && type != "none";

    /// <summary>How far a node's outline lies from its center in direction (ux, uy): an ellipse's or its box's edge.</summary>
    static double Edge(LayoutNode node, double ux, double uy)
    {
        var (a, b) = (Math.Abs(node.W) / 2, Math.Abs(node.H) / 2);
        if (a <= 0 || b <= 0)
            return 0;
        if ((string?)node.Shape?.Attribute("type") is "ellipse" or "donut" or "flowChartConnector" or "pie" or "chord" or "blockArc")
            return 1 / Math.Sqrt(ux * ux / (a * a) + uy * uy / (b * b));
        return Math.Min(Math.Abs(ux) > 1e-12 ? a / Math.Abs(ux) : double.MaxValue, Math.Abs(uy) > 1e-12 ? b / Math.Abs(uy) : double.MaxValue);
    }

    /// <summary>
    /// A connector between the nodes it joins, along the line between their centers: from the first's outline to the
    /// second's (connDist), begPad and endPad short of them (2D: 0.22 and 0.25 of the distance unless constrained,
    /// measured with cycle2; 1D: none), as thick as its place among its siblings or its constraints make it, turned
    /// along the line.
    /// </summary>
    static void Connector(LayoutNode connector, Pins pins)
    {
        var (start, end) = Ends(connector);
        if (start == null || end == null || start == end)
            return;
        if (connector.Param("connRout") is "curve" or "longCurve" && connector.Values.ContainsKey("arcR") && Value(connector, "arcR") > 0)
        {
            Arc(connector, start, end, pins);
            return;
        }
        var (ax, ay) = (start.X + start.W / 2, start.Y + start.H / 2);
        var (bx, by) = (end.X + end.W / 2, end.Y + end.H / 2);
        var (dx, dy) = (bx - ax, by - ay);
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-9)
            return;
        var (ux, uy) = (dx / length, dy / length);
        var (ta, tb) = (Edge(start, ux, uy), Edge(end, -ux, -uy));
        var distance = Math.Max(0, length - ta - tb);
        connector.Values["connDist"] = distance;
        var oneD = connector.Param("dim") == "1D";
        var (begPad, endPad) = oneD ? (0.0, 0.0) : (0.22 * distance, 0.25 * distance);
        // The pads refer to the distance: evaluate the connector's own constraints with it.
        foreach (var constraint in connector.Constraints)
        {
            var type = (string?)constraint.Attribute("type");
            if (type is "begPad" or "endPad")
            {
                var value = (string?)constraint.Attribute("refType") == "connDist"
                    ? distance * (Number((string?)constraint.Attribute("fact")) ?? 1)
                    : Number((string?)constraint.Attribute("val")) ?? 0;
                if (type == "begPad")
                    begPad = value;
                else
                    endPad = value;
            }
        }
        var span = Math.Max(0, distance - begPad - endPad);
        var thickness = connector.H > 0 ? connector.H : (Finite(connector, "h") ?? span * 0.62) * connector.Scale;
        var from = ta + begPad + span / 2;
        var (cx, cy) = (ax + ux * from, ay + uy * from);
        PlaceConnector(connector, cx - span / 2, cy - thickness / 2, span, thickness, pins);
        var angle = Math.Atan2(uy, ux) * 180 / Math.PI;
        connector.Rotation = Math.Abs(angle) < 1e-9 ? 0 : (angle % 360 + 360) % 360;
    }

    /// <summary>The point of a circle at a DrawingML angle (degrees clockwise from the x axis).</summary>
    static (double X, double Y) OnCircle(double cx, double cy, double r, double degrees) =>
        (cx + r * Math.Cos(degrees * Math.PI / 180), cy + r * Math.Sin(degrees * Math.PI / 180));

    static bool Inside(LayoutNode node, (double X, double Y) p)
    {
        var (a, b) = (node.W / 2, node.H / 2);
        var (dx, dy) = (p.X - (node.X + a), p.Y - (node.Y + b));
        if (a <= 0 || b <= 0)
            return false;
        if ((string?)node.Shape?.Attribute("type") is "ellipse" or "donut" or "flowChartConnector")
            return dx * dx / (a * a) + dy * dy / (b * b) <= 1;
        return Math.Abs(dx) <= a && Math.Abs(dy) <= b;
    }

    /// <summary>Where the circle leaves a node going round from its center's angle (degrees, the given way round).</summary>
    static double Leave(LayoutNode node, double cx, double cy, double r, double from, int way)
    {
        var (low, high) = (0.0, 180.0);
        if (Inside(node, OnCircle(cx, cy, r, from + way * high)))
            return from;
        for (var i = 0; i < 40; i++)
        {
            var middle = (low + high) / 2;
            if (Inside(node, OnCircle(cx, cy, r, from + way * middle)))
                low = middle;
            else
                high = middle;
        }
        return from + way * low;
    }

    /// <summary>
    /// A curved connector of a cycle (connRout curve): an arc along the cycle's circle from where it leaves its first
    /// node to where it reaches the next (begPad, endPad), drawn as a circular arrow 0.6 of its thickness wide with a
    /// head 1.4 of it wide and 0.5 of it long, its box the circle 0.65 of its thickness beyond the cycle's (measured with
    /// cycle1 at 2 to 7 nodes).
    /// </summary>
    static void Arc(LayoutNode connector, LayoutNode start, LayoutNode end, Pins pins)
    {
        var (cx, cy, r) = (Value(connector, "arcX"), Value(connector, "arcY"), Value(connector, "arcR"));
        var way = Value(connector, "arcDir") < 0 ? -1 : 1;
        double AngleOf(LayoutNode n) => Math.Atan2(n.Y + n.H / 2 - cy, n.X + n.W / 2 - cx) * 180 / Math.PI;
        var thickness = Value(connector, "h") * connector.Scale;
        var begin = Leave(start, cx, cy, r, AngleOf(start), way);
        var finish = Leave(end, cx, cy, r, AngleOf(end), -way);
        var sweep = ((finish - begin) * way % 360 + 360) % 360;
        var head = r > 0 ? 0.5 * thickness / r * 180 / Math.PI : 0;
        var reach = r + 0.65 * thickness;
        PlaceConnector(connector, cx - reach, cy - reach, 2 * reach, 2 * reach, pins);
        connector.Rotation = 0;
        connector.Values["arcStart"] = begin;
        connector.Values["arcSweep"] = sweep;
        connector.Values["arcHead"] = Math.Min(head, sweep);
        connector.Values["arcThickness"] = thickness;
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
