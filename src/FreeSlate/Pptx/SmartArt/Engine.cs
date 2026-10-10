using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Text;

namespace FreeSlate.Pptx.SmartArt;

/// <summary>
/// Lays out a SmartArt diagram without a saved drawing (catalog: smartart.layout). The layout tree is instantiated for
/// the data model and its constraints evaluated at the diagram's size; then every node, given its box by its parent's
/// algorithm, evaluates its constraint list again, so that its children's values follow its actual size (lengths that
/// follow font sizes stay), and its algorithm places them: composite where their values put them, lin one after the
/// other, snake in rows, cycle around a circle, hierarchies as trees, pyramids in levels, connectors between the nodes
/// they join. A composite takes the room of its children's bounding box (measured: process3's composite, 1000 times as
/// high as wide, is as high as its shapes); lin and snake apply their rules on their children's size when they do not
/// fit (lin by halving the step, snake in 32nds of the way), shrink them uniformly when they still do not, and align
/// them by the room they actually take; a composite root's drawing is centered in the frame. The text is then fitted by
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
        Reconnect(root, pins);
        // A composite root's drawing, where it fits the frame with the root's own connectors, is centered in it
        // (measured: AccentedPicture's picture and descriptions, pyramid2, funnel1; gear1's arrows reach beyond the
        // frame, its gears stay; the roots that align their children themselves, lin and snake, keep it where they put
        // it, IncreasingCircleProcess's at the top).
        if (root.AlgorithmType == "composite" && Bounds(root) is var drawn
            && Union(drawn, root.Children.Where(c => c.AlgorithmType == "conn" && c.W + c.H > 0).Select(c => (c.X, c.Y, c.W, c.H))) is var all
            && all.W <= w + 1e-6 && all.H <= h + 1e-6)
            Translate(root, (w - drawn.W) / 2 - drawn.X, (h - drawn.H) / 2 - drawn.Y);
        return root;
    }

    /// <summary>
    /// Connectors that join a node outside their parent's subtree are placed again once every node stands where its
    /// ancestors moved it (measured: SubStepProcess's lines from a step's circle, beside the column of its sub-steps,
    /// to the middle of the rows' sides).
    /// </summary>
    static void Reconnect(LayoutNode root, Pins pins)
    {
        static bool Within(LayoutNode node, LayoutNode ancestor)
        {
            for (var n = node; n != null; n = n.Parent)
                if (n == ancestor)
                    return true;
            return false;
        }
        foreach (var connector in root.Descendants().Where(n => n.AlgorithmType == "conn").ToList())
        {
            if (connector.Parent is not { AlgorithmType: not ("hierRoot" or "hierChild") } parent)
                continue;
            var (start, end) = Ends(connector);
            if (start != null && end != null && (!Within(start, parent) || !Within(end, parent)))
                Connector(connector, pins);
        }
    }

    /// <summary>The pins of the layout running on this thread, for extents that arrange a subtree (hierarchies).</summary>
    [ThreadStatic] static Pins? running;

    /// <summary>Lays the tree out from scratch with the values the fitting fixed.</summary>
    static void Run(LayoutNode root, double w, double h, Pins pins)
    {
        running = pins;
        foreach (var node in root.Descendants().Prepend(root))
        {
            node.Values.Clear();
            node.Rotation = 0;
            node.Scale = 1;
            node.Placed = false;
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
        var (fw, fh) = Fitted(node, w, h);
        (x, y, w, h) = (x + (w - fw) / 2, y + (h - fh) / 2, fw, fh);
        (node.X, node.Y, node.W, node.H) = (x, y, w, h);
        node.Placed = true;
        node.Values["w"] = w;
        node.Values["h"] = h;
        Constraints.EvaluateList(node, pins);
        foreach (var child in node.Children)
            Constraints.EvaluateOwn(child, pins);
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
            case "hierRoot" or "hierChild":
                Hierarchy(node, pins);
                break;
            case "pyra":
                Pyramid(node, pins);
                break;
            default:
                Composite(node, pins);
                break;
        }
        // Connectors are placed between the nodes their parent placed (a hierarchy places its own).
        if (node.AlgorithmType is not ("hierRoot" or "hierChild"))
        {
            var connectors = node.Children.Where(c => c.AlgorithmType == "conn").ToList();
            foreach (var child in connectors)
                Connector(child, pins);
            // Connectors an equ constraint without a value makes equal in their distance take the shortest one's
            // (measured: bProcess2's triangles, all as long as between its first circle and the next allows).
            foreach (var constraint in node.Constraints.Where(c => (string?)c.Attribute("type") == "connDist" && (string?)c.Attribute("op") == "equ"
                         && c.Attribute("val") == null && c.Attribute("refType") == null))
            {
                var group = Constraints.Targets(node, (string?)constraint.Attribute("for"), (string?)constraint.Attribute("forName"), (string?)constraint.Attribute("ptType"))
                    .Where(connectors.Contains).Where(c => c.Values.ContainsKey("connDist")).ToList();
                if (group.Count < 2)
                    continue;
                var shortest = group.Min(c => c.Values["connDist"]);
                foreach (var connector in group)
                {
                    connector.Values["connDistEqu"] = shortest;
                    Connector(connector, pins);
                }
            }
        }
    }

    /// <summary>
    /// A child's box in its parent's (l, t, r, b, ctrX, ctrY, w, h; where no size is given, as far as the parent's box
    /// reaches from the given edge, else the parent's size; measured: PictureLineup's captions, from half their
    /// composite's height down to its bottom), each value moved by its offset (wOff, bOff …, measured: chevron2's
    /// descriptions as wide as the row less its title, wOff −1 title width, and ending half a title width above its
    /// bottom, bOff).
    /// </summary>
    static (double X, double Y, double W, double H) ChildRect(LayoutNode child, double parentW, double parentH)
    {
        double? Raw(string type) =>
            child.Values.TryGetValue(type, out var v) && double.IsFinite(v)
                ? v + (child.Values.TryGetValue(type + "Off", out var o) && double.IsFinite(o) ? o : 0)
                : null;
        var (l, r, ctrX, w) = (Raw("l"), Raw("r"), Raw("ctrX"), Raw("w"));
        var (t, b, ctrY, h) = (Raw("t"), Raw("b"), Raw("ctrY"), Raw("h"));
        var cw = w ?? (l is { } left ? (r is { } right ? right - left : parentW > 0 ? parentW - left : 0) : r is { } r0 && parentW > 0 ? r0 : parentW);
        var ch = h ?? (t is { } top ? (b is { } bottom ? bottom - top : parentH > 0 ? parentH - top : 0) : b is { } b0 && parentH > 0 ? b0 : parentH);
        var cx = l ?? (ctrX is { } cX ? cX - cw / 2 : r is { } rr ? rr - cw : 0);
        var cy = t ?? (ctrY is { } cY ? cY - ch / 2 : b is { } bb ? bb - ch : 0);
        return (cx, cy, cw, ch);
    }

    /// <summary>The composite algorithm: the children where their values put them in the node's box.</summary>
    static void Composite(LayoutNode node, Pins pins)
    {
        var first = true;
        foreach (var child in node.Children.Where(c => c.AlgorithmType != "conn"))
        {
            // Values that refer to the size of a node placed meanwhile follow its laid-out size (see
            // Constraints.Referenced): the list is evaluated again before each further child.
            if (!first)
            {
                Constraints.EvaluateList(node, pins);
                Constraints.EvaluateOwn(child, pins);
            }
            first = false;
            // A child of the root taller than the diagram is as high as it, a square one keeping its shape, and the values
            // referring to it follow (measured: target3's outer circle, 0.6 of the width, no taller than the diagram).
            if (node.Parent == null && node.H > 0
                && child.Values.TryGetValue("h", out var tall) && double.IsFinite(tall) && tall > node.H + 1e-9)
            {
                var square = child.Values.TryGetValue("w", out var wide) && Math.Abs(wide - tall) < 1e-6;
                pins.Set(child, "h", node.H);
                if (square)
                    pins.Set(child, "w", node.H);
                Constraints.EvaluateList(node, pins);
                Constraints.EvaluateOwn(child, pins);
            }
            var (cx, cy, cw, ch) = KeepRatioBounds(node, child, ChildRect(child, node.W, node.H));
            Place(child, node.X + cx, node.Y + cy, cw, ch, pins);
        }
    }

    /// <summary>
    /// A child that takes its composite's box keeps the bounds the composite sets between the child's width and height
    /// by giving up room across, centered: w ≥ 2 h leaves a box as wide as the composite and half as high (measured:
    /// CircularPictureCallout's callouts, 840 × 420 in the middle of an 840 × 460 diagram).
    /// </summary>
    static (double X, double Y, double W, double H) KeepRatioBounds(LayoutNode node, LayoutNode child, (double X, double Y, double W, double H) rect)
    {
        if (child.Values.ContainsKey("w") || child.Values.ContainsKey("h"))
            return rect;
        foreach (var constraint in node.Constraints)
        {
            var op = (string?)constraint.Attribute("op");
            var (type, refType) = ((string?)constraint.Attribute("type"), (string?)constraint.Attribute("refType"));
            if (op is not ("gte" or "lte") || (string?)constraint.Attribute("for") != "ch" || (string?)constraint.Attribute("refFor") != "ch"
                || (string?)constraint.Attribute("forName") != (string?)constraint.Attribute("refForName")
                || !(type == "w" && refType == "h" || type == "h" && refType == "w")
                || !Constraints.Targets(node, "ch", (string?)constraint.Attribute("forName"), (string?)constraint.Attribute("ptType")).Contains(child))
                continue;
            var fact = Number((string?)constraint.Attribute("fact")) ?? 1;
            if (fact <= 0)
                continue;
            var (value, reference) = type == "w" ? (rect.W, rect.H) : (rect.H, rect.W);
            if (op == "gte" && value < fact * reference - 1e-9)
            {
                // The value cannot outgrow the box: what it refers to shrinks.
                var shrunk = value / fact;
                rect = type == "w" ? rect with { Y = rect.Y + (rect.H - shrunk) / 2, H = shrunk } : rect with { X = rect.X + (rect.W - shrunk) / 2, W = shrunk };
            }
            else if (op == "lte" && value > fact * reference + 1e-9)
            {
                var limited = fact * reference;
                rect = type == "w" ? rect with { X = rect.X + (rect.W - limited) / 2, W = limited } : rect with { Y = rect.Y + (rect.H - limited) / 2, H = limited };
            }
        }
        return rect;
    }

    /// <summary>
    /// The room a node takes in its parent's layout, relative to its own origin, from its current values: its size; for
    /// a composite the bounding box of its children; for a lin node without a finite size its children one after the
    /// other.
    /// </summary>
    static (double X, double Y, double W, double H) Extent(LayoutNode node)
    {
        // A hierarchy takes the room of its tree arranged in the units of its constraints (measured: hierarchy5's tree
        // in a lin node with fallback 2D, scaled to fit it).
        if (IsHierarchy(node) && running is { } pins)
        {
            var box = Arrange(node, pins);
            return (box.X - node.X, box.Y - node.Y, box.W, box.H);
        }
        var (w, h) = (Finite(node, "w"), Finite(node, "h"));
        var children = node.Children.Where(c => c.AlgorithmType != "conn").ToList();
        if (node.AlgorithmType is "composite" && children.Count > 0)
        {
            var (pw, ph) = (w ?? 0, h ?? 0);
            // A composite kept to its aspect ratio takes the room of its children in the box it keeps (measured:
            // TitlePictureLineup's pictures, as wide as half their height, in a row as wide as the diagram).
            if (Fitted(node, pw, ph) is var (aw, ah) && (aw != pw || ah != ph))
            {
                (node.Values["w"], node.Values["h"]) = (aw, ah);
                Constraints.EvaluateList(node, running);
                foreach (var child in node.Children)
                {
                    Constraints.EvaluateOwn(child, running);
                    Refresh(child, running);
                }
                (pw, ph) = (aw, ah);
            }
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var child in children)
            {
                var (cx, cy, cw, ch) = KeepRatioBounds(node, child, ChildRect(child, pw, ph));
                var e = child.Children.Count > 0 ? Extent(child) : (0, 0, cw, ch);
                // A lin child without a size of its own takes the composite's box and shrinks its children into it
                // (measured: lProcess2's inner lists, as high as their composites, their items shrunk to fit).
                if (child.AlgorithmType == "lin")
                {
                    if (Finite(child, "w") == null && !Grows(child, "w"))
                        (e.X, e.W) = (0, cw);
                    if (Finite(child, "h") == null && !Grows(child, "h"))
                        (e.Y, e.H) = (0, ch);
                }
                minX = Math.Min(minX, cx + e.X);
                minY = Math.Min(minY, cy + e.Y);
                maxX = Math.Max(maxX, cx + e.X + e.W);
                maxY = Math.Max(maxY, cy + e.Y + e.H);
            }
            if (pw != (w ?? 0) || ph != (h ?? 0))
                (node.Values["w"], node.Values["h"]) = (w ?? 0, h ?? 0);
            return (minX, minY, maxX - minX, maxY - minY);
        }
        var (growsW, growsH) = (Grows(node, "w"), Grows(node, "h"));
        // A lin node all of whose children stay narrower across its own line takes the room of the widest, centered
        // (measured: SubStepProcess' column of sub-steps, 0.77 of its width, between the step circles; hProcess3's
        // columns keep their box with a spacer as wide as them).
        if (node.AlgorithmType == "lin" && children.Count > 0 && w is { } boxW && h is { } boxH && !growsW && !growsH)
        {
            var vertical = (node.Param("linDir") ?? "fromL") is "fromT" or "fromB";
            var across = children.Max(c => ChildRect(c, boxW, boxH) is var r ? vertical ? r.W : r.H : 0);
            var full = vertical ? boxW : boxH;
            if (across > 0 && across < full - 1e-6)
                return vertical ? ((boxW - across) / 2, 0, across, boxH) : (0, (boxH - across) / 2, boxW, across);
        }
        if (node.AlgorithmType == "lin" && children.Count > 0 && (w == null || h == null || growsW || growsH))
        {
            var horizontal = (node.Param("linDir") ?? "fromL") is "fromL" or "fromR";
            // The children the line places, connectors given a size among them (measured: lProcess1's arrows between
            // its boxes take room in its columns).
            children = node.Children.Where(c => c.AlgorithmType != "conn" || Constraints.Get(c, "w") != null).ToList();
            var extents = children.Select(Extent).ToList();
            var along = Reach(children, extents.Select(e => horizontal ? e.W : e.H).ToList(), Value(node, "sp"));
            var across = extents.Max(e => horizontal ? e.H : e.W);
            var (ew, eh) = horizontal ? (along, across) : (across, along);
            return (0, 0, w is { } fw ? growsW ? Math.Max(fw, ew) : fw : ew, h is { } fh ? growsH ? Math.Max(fh, eh) : fh : eh);
        }
        // A leaf's own size, its offsets added (ChildRect).
        var (_, _, ow, oh) = ChildRect(node, 0, 0);
        return (0, 0, ow, oh);
    }

    /// <summary>A placed lin node's room: its box, narrowed across its line to its children where all are narrower (see Extent).</summary>
    static (double X, double Y, double W, double H) LinRoom(LayoutNode node)
    {
        var box = (node.X, node.Y, node.W, node.H);
        if (Grows(node, "w") || Grows(node, "h"))
            return box;
        var kids = node.Children.Where(c => c.AlgorithmType != "conn" && c.Placed).ToList();
        if (kids.Count == 0)
            return box;
        if ((node.Param("linDir") ?? "fromL") is "fromT" or "fromB")
        {
            var (low, high) = (kids.Min(k => Math.Min(k.X, k.X + k.W)), kids.Max(k => Math.Max(k.X, k.X + k.W)));
            return high - low > 0 && high - low < node.W - 1e-6 && low >= node.X - 1e-6 && high <= node.X + node.W + 1e-6 ? (low, node.Y, high - low, node.H) : box;
        }
        var (top, bottom) = (kids.Min(k => Math.Min(k.Y, k.Y + k.H)), kids.Max(k => Math.Max(k.Y, k.Y + k.H)));
        return bottom - top > 0 && bottom - top < node.H - 1e-6 && top >= node.Y - 1e-6 && bottom <= node.Y + node.H + 1e-6 ? (node.X, top, node.W, bottom - top) : box;
    }

    /// <summary>
    /// Whether a lin node grows with its content in a dimension (w, h): a rule lets it (type w val INF, measured:
    /// hierarchy4's rows of boxes as wide as the boxes under them), or a lin child of it grows so.
    /// </summary>
    static bool Grows(LayoutNode node, string type) =>
        node.AlgorithmType == "lin"
        && (node.Rules.Any(r => (string?)r.Attribute("type") == type && (string?)r.Attribute("val") == "INF")
            || node.Children.Any(c => c.AlgorithmType == "lin" && Grows(c, type)));

    /// <summary>
    /// The room a placed subtree takes: the boxes of its nodes that draw (a shape type) and of its leaves, else its own
    /// box.
    /// </summary>
    static (double X, double Y, double W, double H) Bounds(LayoutNode node)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var n in node.Descendants().Prepend(node))
        {
            // Leaves count drawn or not (measured: AlternatingCircleProcess's anchors, invisible and as high as their
            // items, keep the items' circles level).
            if ((string?)n.Shape?.Attribute("type") is null or "none" && (n == node || n.Children.Count > 0) || InConnector(n, node))
                continue;
            minX = Math.Min(minX, Math.Min(n.X, n.X + n.W));
            minY = Math.Min(minY, Math.Min(n.Y, n.Y + n.H));
            maxX = Math.Max(maxX, Math.Max(n.X, n.X + n.W));
            maxY = Math.Max(maxY, Math.Max(n.Y, n.Y + n.H));
        }
        return minX == double.MaxValue ? (node.X, node.Y, node.W, node.H) : (minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Whether a node is a connector or inside one, below <paramref name="top"/>: its text takes no room of its own
    /// (measured: hierarchy5's connector texts, left where a previous layout put them).
    /// </summary>
    static bool InConnector(LayoutNode node, LayoutNode top)
    {
        for (var n = node; n != null; n = n.Parent)
        {
            if (n.AlgorithmType == "conn")
                return true;
            if (n == top)
                break;
        }
        return false;
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
        (w, h) = Fitted(child, w, h);
        if (child.AlgorithmType == "composite" && h > 100 * Math.Max(w, 1e-6))
            h = extent.Y + extent.H;
        Place(child, -extent.X * factor, -extent.Y * factor, w * factor, h * factor, pins);
    }

    /// <summary>
    /// Places a child at its own size, its room's corner at the origin, and then scales the laid-out subtree by
    /// <paramref name="factor"/> (measured: hList9's columns, laid out by the unscaled width of the circles beside them,
    /// then scaled with the whole row to fit).
    /// </summary>
    static void PlaceThenScale(LayoutNode child, (double X, double Y, double W, double H) extent, double factor, Pins pins)
    {
        var w = Finite(child, "w") ?? extent.X + extent.W;
        var h = Finite(child, "h") ?? extent.Y + extent.H;
        (w, h) = Fitted(child, w, h);
        if (child.AlgorithmType == "composite" && h > 100 * Math.Max(w, 1e-6))
            h = extent.Y + extent.H;
        Place(child, -extent.X, -extent.Y, w, h, pins);
        ScaleSubtree(child, factor, 0, 0);
        // Values that follow the nodes' sizes (margins of a fraction of the width) follow the scaled sizes (measured:
        // venn3's circles, their margins 0.156 of their width as drawn).
        foreach (var n in child.Descendants().Prepend(child))
        {
            (n.Values["w"], n.Values["h"]) = (n.W, n.H);
            Constraints.EvaluateOwnValues(n, pins);
        }
    }

    /// <summary>Scales a laid-out subtree about a point: positions, sizes, elbows and the nodes' scale.</summary>
    static void ScaleSubtree(LayoutNode node, double factor, double ox, double oy)
    {
        foreach (var n in node.Descendants().Prepend(node))
        {
            n.X = ox + (n.X - ox) * factor;
            n.Y = oy + (n.Y - oy) * factor;
            n.W *= factor;
            n.H *= factor;
            n.Scale *= factor;
            for (var i = 1; i <= 4; i++)
            {
                foreach (var key in new[] { $"elbowX{i}", $"elbowY{i}" })
                {
                    if (n.Values.TryGetValue(key, out var v))
                        n.Values[key] = v * factor;
                }
            }
        }
    }

    /// <summary>
    /// A child's size across a line once it has its length along it: what its own constraints make of the new length
    /// (h = 0.6 w follows), or null where they give the across size no relation to it.
    /// </summary>
    static double? Derived(LayoutNode child, string across, string along, double length)
    {
        foreach (var constraint in child.Constraints)
        {
            if ((string?)constraint.Attribute("for") is not (null or "" or "self") || (string?)constraint.Attribute("type") != across
                || (string?)constraint.Attribute("refType") != along || constraint.Attribute("refFor") != null)
                continue;
            var fact = double.TryParse((string?)constraint.Attribute("fact"), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && double.IsFinite(f) ? f : 1;
            return length * fact;
        }
        return null;
    }

    /// <summary>
    /// A child's value of a type that its parent's list takes from a sibling already placed, from the sibling as placed
    /// (its laid-out width or height times the factor), or null.
    /// </summary>
    static double? SiblingReference(LayoutNode node, LayoutNode child, string type)
    {
        foreach (var constraint in node.Constraints)
        {
            if ((string?)constraint.Attribute("type") != type || (string?)constraint.Attribute("refFor") != "ch"
                || (string?)constraint.Attribute("refType") is not ("w" or "h") and var refType
                || !Constraints.Targets(node, (string?)constraint.Attribute("for"), (string?)constraint.Attribute("forName"), (string?)constraint.Attribute("ptType")).Contains(child))
                continue;
            var reference = Constraints.Targets(node, "ch", (string?)constraint.Attribute("refForName"), (string?)constraint.Attribute("refPtType")).FirstOrDefault(r => r.Placed && r != child);
            if (reference == null)
                continue;
            var fact = Number((string?)constraint.Attribute("fact")) is { } f && double.IsFinite(f) ? f : 1;
            return ((string?)constraint.Attribute("refType") == "w" ? reference.W : reference.H) * fact;
        }
        return null;
    }

    /// <summary>A node's own lte bound on its size across a line by its length along it (h &lt;= 1.2 w), for a length.</summary>
    static double? UpperAcross(LayoutNode child, string across, string along, double length)
    {
        foreach (var constraint in child.Constraints)
        {
            if ((string?)constraint.Attribute("for") is not (null or "" or "self") || (string?)constraint.Attribute("op") != "lte"
                || (string?)constraint.Attribute("type") != across || (string?)constraint.Attribute("refType") != along || constraint.Attribute("refFor") != null)
                continue;
            var fact = Number((string?)constraint.Attribute("fact")) is { } f && double.IsFinite(f) ? f : 1;
            return length * fact;
        }
        return null;
    }

    static double Align(string? alignment, double room, double size) => alignment switch
    {
        "l" or "t" => 0,
        "r" or "b" => room - size,
        _ => (room - size) / 2,
    };

    /// <summary>
    /// The linear algorithm: the children one after another (linDir) by the room they take; where they do not fit the
    /// node, its rules on their length apply first, then they shrink uniformly. The row is aligned in the node along
    /// (horzAlign on a horizontal line; centered) and across (vertAlign; as wide as its widest child), each child across
    /// the row (nodeVertAlign; centered). Negative room (a negative height) overlaps the neighbors.
    /// </summary>
    static void Linear(LayoutNode node, Pins pins)
    {
        var children = node.Children.Where(c => c.AlgorithmType != "conn" || Constraints.Get(c, "w") != null).ToList();
        if (children.Count == 0)
            return;
        var direction = node.Param("linDir") ?? "fromL";
        var horizontal = direction is "fromL" or "fromR";
        var (boxAlong, boxAcross) = horizontal ? (node.W, node.H) : (node.H, node.W);
        var ruled = new Dictionary<LayoutNode, double?>();
        // The children's subtrees evaluated for the sizes this node's box gives them before they are measured (measured:
        // SubStepProcess' rows, as high as their texts at the width the column has once the line is scaled).
        foreach (var child in children)
            Refresh(child, pins);
        var extents = ApplyRules(node, children, horizontal, boxAlong, pins, ruled);
        // sp after the rules: it may follow a value they changed (TextCardSideLineWideImage's, 0.12 of the cards' height).
        var gap = Value(node, "sp");
        var along = Reach(children, extents.Select(e => horizontal ? e.W : e.H).ToList(), gap);
        double factor;
        // A tree alone on the line fits and centers itself in the node (measured: hierarchy5's tree, centered in a line
        // aligned to the left).
        if (children.Count == 1 && IsHierarchy(children[0]))
        {
            Place(children[0], node.X, node.Y, node.W, node.H, pins);
            foreach (var (child, before) in ruled)
            {
                if (before is { } value)
                    pins.Set(child, horizontal ? "w" : "h", value);
                else
                    pins.Remove(child, horizontal ? "w" : "h");
            }
            return;
        }
        if (node.Param("fallback") == "2D")
        {
            var across = extents.Max(e => horizontal ? e.H : e.W);
            factor = Math.Min(along > 0 ? boxAlong / along : 1, across > 0 ? boxAcross / across : 1);
            if (!double.IsFinite(factor) || factor <= 0)
                factor = 1;
            for (var i = 0; i < children.Count; i++)
                PlaceThenScale(children[i], extents[i], factor, pins);
        }
        else
        {
            // The children shrink along the line to fit it; across they keep what their constraints make of their new
            // length (a height that follows the width follows it), all shrinking further only where they stick out
            // (measured: VerticalActionList's rows keep the full width while their heights shrink).
            var shrink = along > boxAlong && along > 0 ? boxAlong / along : 1;
            var sizes = new List<(double Along, double Across)>();
            for (var i = 0; i < children.Count; i++)
            {
                var (alongType, acrossType) = horizontal ? ("w", "h") : ("h", "w");
                var length = (horizontal ? extents[i].W : extents[i].H) * shrink;
                var cross = horizontal ? extents[i].H : extents[i].W;
                // A composite takes the room of its children, whose lengths follow it where they relate to it.
                if (shrink < 1 && children[i].AlgorithmType != "composite" && Derived(children[i], acrossType, alongType, length) is { } derived)
                    cross = derived;
                // A composite's own bound across by its length holds for its new length (measured: NumberedTitleCard-
                // List's cards, h <= 1.2 w, as high as 1.2 times the width the row leaves them).
                if (children[i].AlgorithmType == "composite" && UpperAcross(children[i], acrossType, alongType, length) is { } cap)
                    cross = Math.Min(cross, cap);
                sizes.Add((length, cross));
            }
            // A composite without a size across, its content following its length, takes as much less room across as
            // along (measured: hProcess10's pictures and texts, w 0.86 of their composite, leaving the arrows between
            // them their full share).
            var widest = sizes.Select((z, i) => shrink < 1 && children[i].AlgorithmType == "composite"
                && Finite(children[i], horizontal ? "h" : "w") == null ? z.Across * shrink : z.Across).Max();
            var more = widest > boxAcross && widest > 0 ? boxAcross / widest : 1;
            // Children sticking out across only lose what sticks out (measured: hierarchy4's column, three rows high in a
            // one-row-high diagram, keeps its full width while its rows shrink to fit it), unless their size across
            // follows their length (h = 0.6 w): those shrink with the row's widest.
            factor = shrink * (children.Any(c => c.AlgorithmType is "lin" or "snake") ? 1 : more);
            for (var i = 0; i < children.Count; i++)
            {
                var (alongType, acrossType) = horizontal ? ("w", "h") : ("h", "w");
                var clamp = children[i].AlgorithmType is "lin" or "snake" || Derived(children[i], acrossType, alongType, sizes[i].Along) == null;
                var (length, cross) = clamp
                    ? (sizes[i].Along, Math.Min(sizes[i].Across, boxAcross))
                    : (sizes[i].Along * more, sizes[i].Across * more);
                var e = extents[i];
                var (w, h) = horizontal ? (length, cross) : (cross, length);
                var (sx, sy) = (e.W > 0 ? w / e.W : factor, e.H > 0 ? h / e.H : factor);
                children[i].Scale = horizontal ? sx : sy;
                var (ow, oh) = (Finite(children[i], "w") ?? e.X + e.W, Finite(children[i], "h") ?? e.Y + e.H);
                (ow, oh) = Fitted(children[i], ow, oh);
                // A size across taken from a sibling placed before follows the sibling as placed (measured: hProcess7's
                // spacers as high as the cards their own bound lowered).
                if (SiblingReference(node, children[i], horizontal ? "h" : "w") is { } placedAcross)
                {
                    if (horizontal && sy > 0)
                        oh = placedAcross / sy;
                    else if (!horizontal && sx > 0)
                        ow = placedAcross / sx;
                }
                if (Grows(children[i], "w"))
                    ow = Math.Max(ow, e.X + e.W);
                if (Grows(children[i], "h"))
                    oh = Math.Max(oh, e.Y + e.H);
                if (children[i].AlgorithmType == "composite" && oh > 100 * Math.Max(ow, 1e-6))
                    oh = e.Y + e.H;
                // A composite shrunk along the line takes its content's room along it (measured: vList3's rows, their
                // pictures as high as wide, shrunk with the row and their composites as high as them).
                if (children[i].AlgorithmType == "composite" && (horizontal ? sx : sy) < 1 - 1e-9)
                {
                    if (horizontal)
                        ow = Math.Min(ow, e.W);
                    else
                        oh = Math.Min(oh, e.H);
                }
                Place(children[i], -e.X * sx, -e.Y * sy, ow * sx, oh * sy, pins);
            }
        }

        // The rules' values hold for this layout only (a value pinned before stays).
        foreach (var (child, before) in ruled)
        {
            if (before is { } value)
                pins.Set(child, horizontal ? "w" : "h", value);
            else
                pins.Remove(child, horizontal ? "w" : "h");
        }

        // A lin child takes the room of its box (measured: hProcess3's columns, their text below a spacer that draws
        // nothing) or, where all its children are narrower across its line, of them (see Extent), any other the room
        // its drawn shapes take.
        var bounds = children.Select(c => c.AlgorithmType == "lin" ? LinRoom(c) : Bounds(c)).ToList();
        var total = Reach(children, bounds.Select(b => horizontal ? b.W : b.H).ToList(), gap * factor);
        var order = (direction is "fromR" or "fromB" ? Enumerable.Range(0, children.Count).Reverse() : Enumerable.Range(0, children.Count)).ToList();
        double After(int k) => k + 1 < order.Count && Spaced(children[order[k]], children[order[k + 1]]) ? gap * factor : 0;
        // The row's room along: from where it starts, or the farthest back a child of negative length reaches, to where
        // it ends (measured: hProcess3's arrow, drawn back over the row from its end, starts the row at the node's left).
        var (low, reach) = (0.0, 0.0);
        for (var k = 0; k < order.Count; k++)
        {
            var size = horizontal ? bounds[order[k]].W : bounds[order[k]].H;
            low = Math.Min(low, reach + Math.Min(0, size));
            reach += size + After(k);
        }
        total -= low;
        var start = Align(horizontal ? node.Param("horzAlign") : node.Param("vertAlign"), boxAlong, total) - low;
        var crossAlignment = horizontal ? node.Param("nodeVertAlign") : node.Param("nodeHorzAlign");
        // The row across: as wide as its widest child, aligned in the node (vertAlign on a horizontal line), each child
        // aligned in it (nodeVertAlign).
        var row = bounds.Max(b => horizontal ? b.H : b.W);
        var rowOffset = Align(horizontal ? node.Param("vertAlign") : node.Param("horzAlign"), boxAcross, row);
        var position = start;
        for (var k = 0; k < order.Count; k++)
        {
            var i = order[k];
            var b = bounds[i];
            var (size, cross) = horizontal ? (b.W, b.H) : (b.H, b.W);
            var offset = rowOffset + Align(crossAlignment, row, cross);
            if (horizontal)
                Translate(children[i], node.X + position - b.X, node.Y + offset - b.Y);
            else
                Translate(children[i], node.X + offset - b.X, node.Y + position - b.Y);
            position += size + After(k);
        }
    }

    /// <summary>
    /// Whether sp separates two neighbors on a line: not where a transition's node (a spacer) is one of them (measured:
    /// TextCardSideLineWideImage's cards, 0.12 of their height apart by spacers and sp alike, apart by that once).
    /// </summary>
    static bool Spaced(LayoutNode a, LayoutNode b) => !IsTransition(a) && !IsTransition(b);

    /// <summary>
    /// How far a row of children reaches along its line from its start: the farthest end, where a child of negative
    /// length steps back over the ones before it (measured: hList9's rows, their circles stepping back under the
    /// descriptions, fitted by the farthest description).
    /// </summary>
    static double Reach(List<LayoutNode> children, List<double> sizes, double gap)
    {
        double position = 0, far = 0;
        for (var i = 0; i < sizes.Count; i++)
        {
            far = Math.Max(far, Math.Max(position, position + sizes[i]));
            position += sizes[i] + (i + 1 < sizes.Count && Spaced(children[i], children[i + 1]) ? gap : 0);
        }
        return far;
    }


    /// <summary>A composite's size kept to its aspect ratio (ar, width : height) within a size; any other node's as given.</summary>
    static (double W, double H) Fitted(LayoutNode node, double w, double h)
    {
        if (node.AlgorithmType == "composite" && Number(node.Param("ar")) is > 0 and var ar && w > 0 && h > 0)
            return w / h > ar ? (h * ar, h) : (w, w / ar);
        return (w, h);
    }

    static bool Flag(string name) => Environment.GetEnvironmentVariable(name) == "1";

    /// <summary>Evaluates the lists under a node again for its values (as its layout would, without placing it).</summary>
    static void Refresh(LayoutNode node, Pins? pins)
    {
        if (node.Children.Count == 0)
            return;
        Constraints.EvaluateList(node, pins);
        foreach (var child in node.Children)
        {
            Constraints.EvaluateOwn(child, pins);
            Refresh(child, pins);
        }
    }

    /// <summary>How close to the room the rules bring the children's length: a point (mm).</summary>
    const double CloseEnough = 1 / PtPerMm;

    /// <summary>
    /// Applies a lin node's rules on its children's length along the line (w on a horizontal line) while they do not
    /// fit it, in order: each lowers its targets' value toward its limit (val, or fact times what the value refers to,
    /// see <see cref="Constraints.Reference(LayoutNode, LayoutNode, string)"/>) by halving the step, up to twelve
    /// times, until the children fit with less than a point to spare, keeping the largest value that fits; the values
    /// that follow it are evaluated again (measured: hProcess3's columns, w toward 0, end at
    /// 987/4096 of their width with 0.6 pt to spare, process3's at 279/1024 with 0.83 pt). The values are pinned for the
    /// children's layout (<paramref name="ruled"/>); the children's extents with them.
    /// </summary>
    static List<(double X, double Y, double W, double H)> ApplyRules(LayoutNode node, List<LayoutNode> children, bool horizontal, double box, Pins pins, Dictionary<LayoutNode, double?> ruled)
    {
        var type = horizontal ? "w" : "h";
        var extents = children.Select(Extent).ToList();
        double Left() => box - Reach(children, extents.Select(e => horizontal ? e.W : e.H).ToList(), Value(node, "sp"));
        bool Fits() => Left() >= -1e-9;
        foreach (var rule in node.Rules)
        {
            if (Fits())
                break;
            // A font size rule lowers its targets' size in whole points to the largest that lets the children fit, the
            // values that follow it evaluated again (measured: list1's titles at 44 pt, their rows as high as it makes
            // them); the size stays pinned for the text's own fitting.
            if ((string?)rule.Attribute("type") == "primFontSz")
            {
                var texts = Constraints.Targets(node, (string?)rule.Attribute("for"), (string?)rule.Attribute("forName"), (string?)rule.Attribute("ptType"))
                    .Where(t => t.AlgorithmType == "tx").ToList();
                if (texts.Count == 0)
                    continue;
                var largest = (int)Math.Floor(texts.Max(TextBody.PrimaryFont) + 1e-9);
                var smallest = (int)Math.Ceiling(Number((string?)rule.Attribute("val")) is { } lowest && double.IsFinite(lowest) ? lowest
                    : Number((string?)rule.Attribute("fact")) is { } part && double.IsFinite(part) ? part * largest : 5);
                if (smallest >= largest)
                    continue;
                void Size(int size)
                {
                    foreach (var text in texts)
                    {
                        pins.Set(text, "primFontSz", size);
                        text.Values["primFontSz"] = size;
                    }
                    Constraints.EvaluateList(node, pins);
                    foreach (var child in node.Children)
                    {
                        Constraints.EvaluateOwn(child, pins);
                        Refresh(child, pins);
                    }
                    extents = children.Select(Extent).ToList();
                }
                var (low, high) = (smallest, largest);
                Size(low);
                if (!Fits())
                    continue;
                while (low < high)
                {
                    var middle = (low + high + 1) / 2;
                    Size(middle);
                    if (Fits())
                        low = middle;
                    else
                        high = middle - 1;
                }
                Size(low);
                continue;
            }
            if ((string?)rule.Attribute("type") != type)
                continue;
            var targets = Constraints.Targets(node, (string?)rule.Attribute("for"), (string?)rule.Attribute("forName"), (string?)rule.Attribute("ptType"))
                .Where(children.Contains).ToList();
            var starts = targets.Select(t => Finite(t, type)).ToList();
            if (targets.Count == 0 || starts.Any(s => s == null))
                continue;
            var limits = starts.Select((s, i) => Number((string?)rule.Attribute("val")) is { } v && double.IsFinite(v) ? v
                : Number((string?)rule.Attribute("fact")) is { } f && double.IsFinite(f) ? f * (Constraints.Reference(node, targets[i], type) ?? s!.Value) : double.NaN).ToList();
            if (limits.Any(l => !double.IsFinite(l)) || limits.Select((l, i) => l >= starts[i]!.Value).All(b => b))
                continue;
            void Set(double share)
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    var value = limits[i] + (starts[i]!.Value - limits[i]) * share;
                    if (!ruled.ContainsKey(targets[i]))
                        ruled[targets[i]] = pins.TryGet(targets[i], type, out var before) ? before : null;
                    pins.Set(targets[i], type, value);
                    targets[i].Values[type] = value;
                }
                Constraints.EvaluateList(node, pins);
                foreach (var child in node.Children)
                {
                    Constraints.EvaluateOwn(child, pins);
                    Refresh(child, pins);
                }
                extents = children.Select(Extent).ToList();
            }
            Set(0);
            if (!Fits())
                continue;
            var (fits, fails, spare) = (0.0, 1.0, Left());
            for (var step = 0; step < 12 && spare >= CloseEnough; step++)
            {
                var middle = (fits + fails) / 2;
                Set(middle);
                if (Fits())
                    (fits, spare) = (middle, Left());
                else
                    fails = middle;
            }
            Set(fits);
        }
        return extents;
    }

    /// <summary>
    /// The snake algorithm: the nodes in rows (or columns, flowDir col) of equal cells, as many per row as make them
    /// largest (more per row when equal), shrunk to fit but never grown, after the node's rules where they do not fit;
    /// within a row the cells keep the room of the spacers between them (the transitions' nodes, measured with bList2),
    /// else sp, and the rows sp apart; short rows centered with off ctr, else from the row's start and each row alignOff
    /// further (<see cref="SnakeLines"/>), the grid aligned in the node, growing from the corner grDir names, every other
    /// row reversed with contDir revDir.
    /// </summary>
    static void Snake(LayoutNode node, Pins pins)
    {
        var cells = node.Children.Where(c => !IsTransition(c) && c.AlgorithmType != "conn").ToList();
        if (cells.Count == 0)
            return;
        var columns = node.Param("flowDir") == "col";
        var n = cells.Count;
        List<(double X, double Y, double W, double H)> extents = [];
        double cw = 0, ch = 0, gap = 0, lineGap = 0;
        var best = (Factor: 0.0, Count: 1);
        // The cells' size and spacing from their current values, and the arrangement that makes them largest.
        bool Measure()
        {
            extents = cells.Select(Extent).ToList();
            (cw, ch) = (extents.Max(e => e.X + e.W) - extents.Min(e => e.X), extents.Max(e => e.Y + e.H) - extents.Min(e => e.Y));
            if (cw <= 0 || ch <= 0)
                return false;
            var spacers = node.Children.Where(c => SnakeSpacer(c, columns)).Select(Extent).ToList();
            lineGap = Value(node, "sp");
            gap = spacers.Count > 0 ? spacers.Max(e => columns ? e.H : e.W) : lineGap;
            best = (0.0, 1);
            var fixedCount = SnakeBreak(node, n);
            for (var count = fixedCount > 0 ? Math.Min(fixedCount, n) : 1; count <= (fixedCount > 0 ? Math.Min(fixedCount, n) : n); count++)
            {
                var (_, gw, gh) = SnakeArrange(node, extents, count, gap, lineGap, columns);
                var factor = Math.Min(node.W / gw, node.H / gh);
                if (factor > best.Factor * (1 + 1e-9))
                    best = (factor, count);
            }
            return true;
        }
        if (!Measure())
            return;
        var ruled = new Dictionary<(LayoutNode, string), double?>();
        if (best.Factor < 1 - 1e-9)
            SnakeRules(node, pins, ruled, () => Measure() && best.Factor >= 1 - 1e-9);
        // The cells shrink to fit, never grow (measured: the TextCard layouts' cards keep their size in a larger node).
        best.Factor = Math.Min(best.Factor, 1);
        var k = best.Factor;
        // Connectors between the cells shrink with them.
        foreach (var connector in node.Children.Where(c => c.AlgorithmType == "conn"))
            connector.Scale = k;
        SnakeGrid(node, cells, extents, k, best.Count, columns, cw, ch, gap, lineGap, pins);
        foreach (var ((child, type), before) in ruled)
        {
            if (before is { } value)
                pins.Set(child, type, value);
            else
                pins.Remove(child, type);
        }
    }

    /// <summary>
    /// Places a snake's cells scaled by <paramref name="k"/> in a grid: as many per line as the breakpoint or the room
    /// allow, the spacing evaluated again for the placed cells (measured: PictureAccentBlocks' rows 0.113 of the placed
    /// cells' width apart), the grid aligned in the node (horzAlign, vertAlign; centered) and each line in the grid
    /// (horzAlign), the lines growing from the corner grDir names.
    /// </summary>
    static void SnakeGrid(LayoutNode node, List<LayoutNode> cells, List<(double X, double Y, double W, double H)> extents, double k, int count,
        bool columns, double cw, double ch, double gap, double lineGap, Pins pins)
    {
        var n = cells.Count;
        var fixedCount = SnakeBreak(node, n);
        var perLine = fixedCount > 0 ? fixedCount
            : columns
                ? Math.Max(count, (int)Math.Floor((node.H + gap * k) / ((ch + gap) * k) + 1e-6))
                : Math.Max(count, (int)Math.Floor((node.W + gap * k) / ((cw + gap) * k) + 1e-6));
        perLine = Math.Min(perLine, n);
        for (var i = 0; i < n; i++)
            PlaceScaled(cells[i], extents[i], k, pins);
        var (sgap, slineGap) = (gap * k, lineGap * k);
        // The spacing follows the placed cells where it refers to them (PictureAccentBlocks' sp, 0.113 of a cell's width:
        // of its width as placed, kept to its aspect ratio), else it shrinks with them (bProcess4's, 0.33 of the node's).
        if (PlacedReference(node, "sp") is { } placed)
        {
            slineGap = placed;
            if (!node.Children.Any(c => SnakeSpacer(c, columns)))
                sgap = slineGap;
        }
        var scaled = extents.Select(e => (e.X * k, e.Y * k, e.W * k, e.H * k)).ToList();
        var (boxes, w, h) = SnakeArrange(node, scaled, perLine, sgap, slineGap, columns);
        var (gx, gy) = (Align(node.Param("horzAlign"), node.W, w), Align(node.Param("vertAlign"), node.H, h));
        for (var i = 0; i < n; i++)
        {
            var (cell, e) = (cells[i], scaled[i]);
            var b = Bounds(cell);
            Translate(cell, node.X + gx + boxes[i].X + e.Item1 + (e.Item3 - b.W) / 2 - b.X, node.Y + gy + boxes[i].Y + e.Item2 + (e.Item4 - b.H) / 2 - b.Y);
        }
    }

    /// <summary>
    /// The cells per line a snake's breakpoint sets: bkpt fixed, bkPtFixedVal (measured: PictureAccentBlocks' two per
    /// row); bkpt bal, the square root of their count rounded up (bProcess4's two in a column for two to four nodes, three
    /// for five to nine); else none, as many as make them largest.
    /// </summary>
    static int SnakeBreak(LayoutNode node, int n) => node.Param("bkpt") switch
    {
        "fixed" => Number(node.Param("bkPtFixedVal")) is >= 1 and var f ? (int)f : 2,
        "bal" => (int)Math.Ceiling(Math.Sqrt(n) - 1e-9),
        _ => 0,
    };

    /// <summary>
    /// Whether a snake's child spaces its cells along a line: a transition's node, a connector only given a size along
    /// the line (measured: bProcess4's arrows, a quarter of a cell high, apart its cells in a column).
    /// </summary>
    static bool SnakeSpacer(LayoutNode child, bool columns) =>
        IsTransition(child) && (child.AlgorithmType != "conn" || Constraints.Get(child, columns ? "h" : "w") != null);

    /// <summary>
    /// Arranges a snake's cells, given their extents (their content's offset from their box and its size), in lines of
    /// <paramref name="perLine"/>: each in a slot as large as all cells' content, in place in it by its box, the lines
    /// started as <see cref="SnakeLines"/> starts them, growing from the corner grDir names. Gives each cell's box offset
    /// from the corner of the room their content takes, and that room (measured: StepUpProcess' last step, without its
    /// arrow, a step lower at its top, the steps centered without it).
    /// </summary>
    static (List<(double X, double Y)> Boxes, double W, double H) SnakeArrange(LayoutNode node, List<(double X, double Y, double W, double H)> extents,
        int perLine, double gap, double lineGap, bool columns)
    {
        var n = extents.Count;
        var (ux, uy) = (extents.Min(e => e.X), extents.Min(e => e.Y));
        var (cw, ch) = (extents.Max(e => e.X + e.W) - ux, extents.Max(e => e.Y + e.H) - uy);
        var (cellAlong, cellAcross) = columns ? (ch, cw) : (cw, ch);
        var (starts, gridAlong) = SnakeLines(node, n, perLine, cellAlong, gap, columns);
        var gridAcross = starts.Count * cellAcross + (starts.Count - 1) * lineGap;
        var (gridW, gridH) = columns ? (gridAcross, gridAlong) : (gridAlong, gridAcross);
        var grow = node.Param("grDir") ?? "tL";
        var reverse = node.Param("contDir") == "revDir";
        var boxes = new List<(double X, double Y)>();
        var (minX, minY, maxX, maxY) = (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
        for (var i = 0; i < n; i++)
        {
            var (line, index) = (i / perLine, i % perLine);
            var inLine = Math.Min(perLine, n - line * perLine);
            if (reverse && line % 2 == 1)
                index = inLine - 1 - index;
            var along = starts[line] + index * (cellAlong + gap);
            var across = line * (cellAcross + lineGap);
            var (x, y) = columns ? (across, along) : (along, across);
            if (grow is "tR" or "bR")
                x = gridW - x - cw;
            if (grow is "bL" or "bR")
                y = gridH - y - ch;
            var (bx, by) = (x - ux, y - uy);
            boxes.Add((bx, by));
            var e = extents[i];
            (minX, minY) = (Math.Min(minX, bx + e.X), Math.Min(minY, by + e.Y));
            (maxX, maxY) = (Math.Max(maxX, bx + e.X + e.W), Math.Max(maxY, by + e.Y + e.H));
        }
        return (boxes.Select(b => (b.X - minX, b.Y - minY)).ToList(), maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Where each line of a snake with <paramref name="perLine"/> cells starts along the lines, and how far the lines
    /// reach: with off ctr each centered on the full lines (aligned by horzAlign, vertAlign for columns); else each from
    /// its start, alignOff of a cell and its spacing further than the one before (measured: StepUpProcess' steps 1.103
    /// of a cell's width with spacers of 0.103, PictureAccentBlocks' rows a cell further; bProcess3's and the
    /// TextCardSideLine layouts' short rows from the left), a reversed line (contDir revDir) from its far end (process5's).
    /// </summary>
    static (List<double> Starts, double Along) SnakeLines(LayoutNode node, int n, int perLine, double cell, double gap, bool columns)
    {
        var full = perLine * cell + (perLine - 1) * gap;
        var centered = node.Param("off") == "ctr";
        var step = centered ? 0 : Value(node, "alignOff") * (cell + gap);
        var reverse = node.Param("contDir") == "revDir";
        var (starts, ends) = (new List<double>(), new List<double>());
        for (var line = 0; line * perLine < n; line++)
        {
            var inLine = Math.Min(perLine, n - line * perLine);
            var length = inLine * cell + (inLine - 1) * gap;
            var start = centered
                ? Align(columns ? node.Param("vertAlign") : node.Param("horzAlign"), full, length)
                : line * step + (reverse && line % 2 == 1 ? full - length : 0);
            starts.Add(start);
            ends.Add(start + length);
        }
        var low = starts.Min();
        return (starts.Select(s => s - low).ToList(), ends.Max() - low);
    }

    /// <summary>
    /// A node's own value of a type that its constraint takes from a child, from the child as placed (its laid-out width
    /// or height times the factor), or null where the constraint refers to no placed child.
    /// </summary>
    static double? PlacedReference(LayoutNode node, string type)
    {
        foreach (var constraint in node.Constraints)
        {
            if ((string?)constraint.Attribute("type") != type || (string?)constraint.Attribute("for") is not (null or "" or "self")
                || (string?)constraint.Attribute("refFor") != "ch" || (string?)constraint.Attribute("refType") is not ("w" or "h"))
                continue;
            var reference = Constraints.Targets(node, "ch", (string?)constraint.Attribute("refForName"), (string?)constraint.Attribute("refPtType")).FirstOrDefault();
            if (reference is not { Placed: true })
                continue;
            var fact = Number((string?)constraint.Attribute("fact")) is { } f && double.IsFinite(f) ? f : 1;
            return (string?)constraint.Attribute("refType") switch
            {
                "w" => reference.W * fact,
                "h" => reference.H * fact,
                _ => null,
            };
        }
        return null;
    }

    /// <summary>
    /// Applies a snake node's rules on its children's size while they do not fit it at their own size, in order: each
    /// lowers its targets' value from where it is toward its limit (val, or fact times what the value refers to:
    /// radial5's nodes, 1.25 of the middle circle, a rule of fact 1 takes to 1 of it) in 32nds of the way,
    /// taking the first that fits (measured: MeetTheTeamCard's cards at 12/32 of the way from 50 mm to the row's width,
    /// MeetTheTeam's at 2/32; AccentedPicture's text widths grow the same way), the values that follow it evaluated
    /// again.
    /// </summary>
    static void SnakeRules(LayoutNode node, Pins pins, Dictionary<(LayoutNode, string), double?> ruled, Func<bool> fits)
    {
        foreach (var rule in node.Rules)
        {
            var type = (string?)rule.Attribute("type");
            if (type is not ("w" or "h"))
                continue;
            var targets = Constraints.Targets(node, (string?)rule.Attribute("for"), (string?)rule.Attribute("forName"), (string?)rule.Attribute("ptType"))
                .Where(node.Children.Contains).ToList();
            var starts = targets.Select(t => Finite(t, type)).ToList();
            if (targets.Count == 0 || starts.Any(v => v == null))
                continue;
            var limits = starts.Select((v, i) => Number((string?)rule.Attribute("val")) is { } l && double.IsFinite(l) ? l
                : Number((string?)rule.Attribute("fact")) is { } f && double.IsFinite(f) ? f * (Constraints.Reference(node, targets[i], type) ?? v!.Value) : double.NaN).ToList();
            if (limits.Any(l => !double.IsFinite(l)) || limits.Select((l, i) => l >= starts[i]!.Value).All(b => b))
                continue;
            for (var step = 31; step >= 0; step--)
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    var value = limits[i] + (starts[i]!.Value - limits[i]) * step / 32;
                    if (!ruled.ContainsKey((targets[i], type)))
                        ruled[(targets[i], type)] = pins.TryGet(targets[i], type, out var before) ? before : null;
                    pins.Set(targets[i], type, value);
                    targets[i].Values[type] = value;
                }
                Constraints.EvaluateList(node, pins);
                foreach (var child in node.Children)
                {
                    Constraints.EvaluateOwn(child, pins);
                    Refresh(child, pins);
                }
                if (fits())
                    return;
            }
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
        var n = ring.Count;
        var full = Math.Abs(Math.Abs(span) - 360) < 1e-9;
        var step = full ? span / n : n > 1 ? span / (n - 1) : 0;
        var alongPath = node.Param("rotPath") == "alongPath";
        static (double X, double Y) On(double degrees) => (Math.Sin(degrees * Math.PI / 180), -Math.Cos(degrees * Math.PI / 180));
        Dictionary<LayoutNode, (double X, double Y, double W, double H)> extents = [];
        var places = new Dictionary<LayoutNode, (double X, double Y, double Angle)>();
        double radius = 0, minX = 0, minY = 0, bw = 0, bh = 0, factor = 1;
        double? spread = null;
        // How far a cell's outline is from its center toward a direction (turned with it along the path).
        double Exit(LayoutNode cell, double ux, double uy, double angle)
        {
            if (alongPath && cell != center)
            {
                var (sin, cos) = (Math.Sin(-angle * Math.PI / 180), Math.Cos(-angle * Math.PI / 180));
                (ux, uy) = (ux * cos - uy * sin, ux * sin + uy * cos);
            }
            var (a, b) = (extents[cell].W / 2, extents[cell].H / 2);
            if ((string?)cell.Shape?.Attribute("type") == "ellipse")
                return a * b / Math.Max(Math.Sqrt(b * ux * b * ux + a * uy * a * uy), 1e-12);
            return Math.Min(Math.Abs(ux) > 1e-12 ? a / Math.Abs(ux) : double.MaxValue, Math.Abs(uy) > 1e-12 ? b / Math.Abs(uy) : double.MaxValue);
        }
        // The arrangement from the cells' current values, and the factor that fits it to the node.
        double Measure()
        {
            extents = cells.ToDictionary(c => c, Extent);
            // The circle as small as keeps each two neighbors' outlines sibSp apart along the line between their
            // centers (measured: cycle7's boxes, half as high as wide, 0.65 of their width apart; cycle5's with its
            // spacers; cycle2's circles), and the ring sp away from the middle one likewise.
            radius = 0;
            for (var i = 0; n > 1 && Math.Abs(step) > 1e-9 && i < (full ? n : n - 1); i++)
            {
                var j = (i + 1) % n;
                var (p, q) = (On(start + i * step), On(start + j * step));
                var chord = Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y));
                if (chord < 1e-9)
                    continue;
                var (ux, uy) = ((q.X - p.X) / chord, (q.Y - p.Y) / chord);
                var need = Exit(ring[i], ux, uy, start + i * step) + Exit(ring[j], -ux, -uy, start + j * step) + Value(node, "sibSp");
                radius = Math.Max(radius, need / chord);
            }
            if (center != null)
            {
                for (var i = 0; i < n; i++)
                {
                    var (ux, uy) = On(start + i * step);
                    radius = Math.Max(radius, Exit(center, ux, uy, 0) + Exit(ring[i], -ux, -uy, start + i * step) + Value(node, "sp"));
                }
            }
            if (spread is { } wider)
                radius = Math.Max(radius, wider);
            // The cells' centers about the circle's center.
            places.Clear();
            if (center != null)
                places[center] = (0, 0, 0);
            for (var i = 0; i < n; i++)
            {
                var angle = start + i * step;
                var radians = angle * Math.PI / 180;
                places[ring[i]] = (radius * Math.Sin(radians), -radius * Math.Cos(radians), angle);
            }
            double maxX = double.MinValue, maxY = double.MinValue;
            (minX, minY) = (double.MaxValue, double.MaxValue);
            foreach (var (cell, (px, py, _)) in places)
            {
                var e = extents[cell];
                minX = Math.Min(minX, px - e.W / 2);
                maxX = Math.Max(maxX, px + e.W / 2);
                minY = Math.Min(minY, py - e.H / 2);
                maxY = Math.Max(maxY, py + e.H / 2);
            }
            // Curved connectors run along the circle where the span runs; their arcs (0.65 of their thickness beyond
            // it, 0.3 without arrowheads) take room too (measured: cycle1's around the circle; VerticalCurvedList's
            // along a quarter of it, as far as its points).
            var arcs = node.Children.Where(c => c.AlgorithmType == "conn" && c.Param("connRout") is "curve" or "longCurve" && c.Param("dim") != "1D").ToList();
            if (arcs.Count > 0 && radius > 0)
            {
                var reach = radius + arcs.Max(c => (ArcHeads(c) ? 0.65 : 0.3) * Value(c, "h"));
                var sweep = full ? 360 : step * (n - 1);
                for (var k = 0; k <= 360; k++)
                {
                    var (ax, ay) = On(start + sweep * k / 360);
                    (minX, maxX) = (Math.Min(minX, ax * reach), Math.Max(maxX, ax * reach));
                    (minY, maxY) = (Math.Min(minY, ay * reach), Math.Max(maxY, ay * reach));
                }
            }
            (bw, bh) = (maxX - minX, maxY - minY);
            factor = Math.Min(bw > 0 ? node.W / bw : 1, bh > 0 ? node.H / bh : 1);
            if (!double.IsFinite(factor) || factor <= 0)
                factor = 1;
            return factor;
        }
        // Where it does not fit, the node's rules first (in 32nds, as a snake's; measured: radial4's middle circle,
        // w toward 0.27 of its value, at 30/32 of the way for two nodes and at the limit for one), then it shrinks.
        var ruled = new Dictionary<(LayoutNode, string), double?>();
        if (Measure() < 1 - 1e-9 && node.Rules.Count > 0)
        {
            SnakeRules(node, pins, ruled, () => Measure() >= 1 - 1e-9);
            Measure();
        }
        // A ring with room to spare spreads out until it fills the node, its shapes keeping their size (measured:
        // RadialCluster's nodes about its middle circle), at most to a diameter of its own (VerticalCurvedList's, 1.344
        // of its height, its points on a quarter of it; arrow5's, 1.1 of a node's width, too small to matter); nothing
        // grows.
        if (factor > 1 + 1e-9)
        {
            var most = Finite(node, "diam") is > 0 and var diameter ? diameter / 2 : radius + node.W + node.H;
            if ((n > 1 || n == 1 && center != null) && most > radius)
            {
                var (low, high) = (radius, most);
                spread = most;
                if (Measure() >= 1)
                    low = high;
                for (var i = 0; i < 50; i++)
                {
                    spread = (low + high) / 2;
                    if (Measure() >= 1)
                        low = spread.Value;
                    else
                        high = spread.Value;
                }
                spread = low;
                Measure();
            }
            factor = Math.Min(factor, 1);
        }
        var (ox, oy) = (node.X + (node.W - bw * factor) / 2 - minX * factor, node.Y + (node.H - bh * factor) / 2 - minY * factor);
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
        foreach (var ((child, type), before) in ruled)
        {
            if (before is { } value)
                pins.Set(child, type, value);
            else
                pins.Remove(child, type);
        }
    }

    /// <summary>
    /// The pyramid algorithm: the children as levels of equal height, the first at the top, as wide as their place in a
    /// triangle filling the node (linDir fromB: widening downwards; fromT: narrowing, the shapes turned upside down);
    /// the level shape (pyraLvlNode) a trapezoid whose sides continue the triangle's (adj = half a level's widening over
    /// its height; measured with pyramid1 and pyramid3 at 1 to 5 levels, exact).
    /// </summary>
    static void Pyramid(LayoutNode node, Pins pins)
    {
        var levels = node.Children.Where(c => !IsTransition(c) && c.AlgorithmType != "conn").ToList();
        var n = levels.Count;
        if (n == 0)
            return;
        var downwards = (node.Param("linDir") ?? "fromB") == "fromT";
        var levelName = node.Param("pyraLvlNode");
        var height = node.H / n;
        var step = node.W / n;
        for (var i = 0; i < n; i++)
        {
            var width = step * (downwards ? n - i : i + 1);
            var (x, y) = (node.X + (node.W - width) / 2, node.Y + i * height);
            var level = levels[i];
            var shape = levelName != null ? level.Descendants().FirstOrDefault(d => d.Name == levelName) : null;
            Place(level, x, y, width, height, pins);
            if (level.AlgorithmType == "composite" && shape != null)
            {
                // The composite's children take the level's box (their placeholders are the algorithm's to set).
                foreach (var child in level.Children)
                    Place(child, x, y, child == shape ? width : 0, child == shape ? height : 0, pins);
            }
            var target = shape ?? level;
            target.Values["pyraAdj"] = step / 2 / Math.Max(1e-9, Math.Min(width, height));
            if (downwards)
                target.Rotation = 180;
        }
    }

    static bool IsHierarchy(LayoutNode node) => node.AlgorithmType is "hierRoot" or "hierChild";

    /// <summary>
    /// A hierarchy (hierRoot and hierChild, catalog: smartart.layout): the tree arranged in the units of its constraints
    /// (each node's shape above its children's row, sp between the levels, sibSp between siblings, assistants on a level
    /// of their own beside the line down to the children), its connectors drawn, and the whole fitted into the top
    /// node's box, uniformly scaled and centered (measured with orgChart1 and hierarchy1: a parent centered over its
    /// first and last child, levels sp apart).
    /// </summary>
    static void Hierarchy(LayoutNode node, Pins pins)
    {
        var (bx, by, bw, bh) = (node.X, node.Y, node.W, node.H);
        var box = Arrange(node, pins);
        foreach (var connector in node.Descendants().Where(c => c.AlgorithmType == "conn"))
            Connector(connector, pins);
        box = Union(box, node.Descendants().Where(c => c.AlgorithmType == "conn" && c.W + c.H > 0).Select(c => (c.X, c.Y, c.W, c.H)));
        var factor = Math.Min(box.W > 0 ? bw / box.W : 1, box.H > 0 ? bh / box.H : 1);
        if (!double.IsFinite(factor) || factor <= 0)
            factor = 1;
        // The tree aligned in the node by its horzAlign and vertAlign (centered; measured: SquareAccentList's at the top).
        var (ox, oy) = (bx + Align(node.Param("horzAlign"), bw, box.W * factor), by + Align(node.Param("vertAlign"), bh, box.H * factor));
        foreach (var n in node.Descendants())
        {
            n.X = ox + (n.X - box.X) * factor;
            n.Y = oy + (n.Y - box.Y) * factor;
            n.W *= factor;
            n.H *= factor;
            n.Scale *= factor;
            for (var i = 1; i <= 4; i++)
            {
                foreach (var key in new[] { $"elbowX{i}", $"elbowY{i}" })
                {
                    if (n.Values.TryGetValue(key, out var v))
                        n.Values[key] = v * factor;
                }
            }
        }
        // Values that follow the shapes' sizes follow them as drawn (measured: NameandTitleOrganizationalChart's names,
        // their bottom margin 0.4 of their height as drawn).
        foreach (var n in node.Descendants().Where(n => n.AlgorithmType == "tx" && !InConnector(n, node)))
        {
            (n.Values["w"], n.Values["h"]) = (n.W, n.H);
            Constraints.EvaluateOwnValues(n, pins);
        }
        (node.X, node.Y, node.W, node.H) = (bx, by, bw, bh);
    }

    static (double X, double Y, double W, double H) Union((double X, double Y, double W, double H) a, IEnumerable<(double X, double Y, double W, double H)> boxes)
    {
        var (minX, minY, maxX, maxY) = (a.X, a.Y, a.X + a.W, a.Y + a.H);
        foreach (var b in boxes)
        {
            minX = Math.Min(minX, b.X);
            minY = Math.Min(minY, b.Y);
            maxX = Math.Max(maxX, b.X + b.W);
            maxY = Math.Max(maxY, b.Y + b.H);
        }
        return (minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>Arranges a node of a hierarchy at the origin in the units of its constraints; its room afterwards.</summary>
    static (double X, double Y, double W, double H) Arrange(LayoutNode node, Pins pins)
    {
        if (node.AlgorithmType == "hierRoot")
            return ArrangeRoot(node, pins);
        if (node.AlgorithmType == "hierChild")
            return ArrangeRow(node, pins);
        var e = Extent(node);
        var (w, h) = (Finite(node, "w") ?? e.X + e.W, Finite(node, "h") ?? e.Y + e.H);
        if (node.AlgorithmType == "composite" && h > 100 * Math.Max(w, 1e-6))
            h = e.Y + e.H;
        Place(node, 0, 0, w, h, pins);
        return Bounds(node);
    }

    /// <summary>
    /// A hierarchy root: its own shape (the first child that is not a hierarchy), below it the assistants' row (right of
    /// center: to the left of the line down) and the children's row, sp apart; the shape centered over the row's first
    /// and last shapes (hierAlign tCtr), at its left or right (tL, tR) or beside a column of children (lCtrCh, rCtrCh).
    /// </summary>
    static (double X, double Y, double W, double H) ArrangeRoot(LayoutNode node, Pins pins)
    {
        Constraints.EvaluateList(node, pins);
        (node.X, node.Y) = (0, 0);
        var own = node.Children.FirstOrDefault(c => !IsHierarchy(c) && c.AlgorithmType != "conn");
        var rows = node.Children.Where(c => c.AlgorithmType == "hierChild").ToList();
        var assistants = rows.FirstOrDefault(r => r.Children.Any(c => c.Point.Type == "asst"));
        var sp = Value(node, "sp");
        var align = node.Param("hierAlign") ?? "tCtr";
        var horizontal = align is "lCtrCh" or "rCtrCh" or "lT" or "lB" or "rT" or "rB";
        if (own != null)
            Arrange(own, pins);
        // Rows line up with the root's own box (a composite's, not the union of its shapes) and hang sp beyond its shapes
        // (measured: CirclePictureHierarchy's rows sp below the pictures, inside their composites).
        (double X, double Y, double W, double H) shape = own != null ? (own.X, own.Y, own.W, own.H) : (0, 0, 0, 0);
        var drawn = own != null ? Bounds(own) : shape;
        var (centerX, centerY) = (shape.X + shape.W / 2, shape.Y + shape.H / 2);
        var next = horizontal ? (align.StartsWith('l') ? drawn.X + drawn.W + sp : drawn.X - sp) : drawn.Y + drawn.H + sp;
        if (assistants != null && assistants.Children.Count > 0)
        {
            var a = ArrangeRow(assistants, pins);
            // Half the assistants' sibSp beside the line from the shape to its children (measured: orgChart1's left of
            // the line down; HorizontalOrganizationChart's above the line across, in a column of their own sp from the
            // shape and from the children).
            var gap = Value(assistants, "sibSp", sp);
            if (horizontal)
            {
                Translate(assistants, align.StartsWith('l') ? next - a.X : next - (a.X + a.W), centerY - gap / 2 - (a.Y + a.H));
                next = align.StartsWith('l') ? next + a.W + sp : next - a.W - sp;
            }
            else
            {
                Translate(assistants, centerX - gap / 2 - (a.X + a.W), next - a.Y);
                next += a.H + sp;
            }
        }
        foreach (var row in rows.Where(r => r != assistants))
        {
            var r = ArrangeRow(row, pins);
            if (r.W <= 0 && r.H <= 0)
                continue;
            var heads = row.Children.Where(c => c.AlgorithmType != "conn").Select(Head).Where(h => h != null).ToList();
            if (horizontal)
            {
                // Children in a column beside the shape, centered on it by their first and last shapes.
                var middle = heads.Count > 0 ? (heads[0]!.Y + heads[0]!.H / 2 + heads[^1]!.Y + heads[^1]!.H / 2) / 2 : r.Y + r.H / 2;
                var dy = align.EndsWith('T') ? shape.Y - r.Y : align.EndsWith('B') ? shape.Y + shape.H - (r.Y + r.H) : centerY - middle;
                var dx = align.StartsWith('l') ? next - r.X : next - (r.X + r.W);
                Translate(row, dx, dy);
                next = align.StartsWith('l') ? next + r.W + sp : next - r.W - sp;
            }
            else
            {
                var middle = heads.Count > 0 ? (heads[0]!.X + heads[0]!.W / 2 + heads[^1]!.X + heads[^1]!.W / 2) / 2 : r.X + r.W / 2;
                var offset = Value(node, "alignOff") * shape.W;
                var dx = align switch
                {
                    "tL" => shape.X + offset - r.X,
                    "tR" => shape.X + shape.W - offset - (r.X + r.W),
                    _ => centerX - middle,
                };
                Translate(row, dx, next - r.Y);
                next += r.H + sp;
            }
        }
        return Bounds(node);
    }

    /// <summary>The own box of a hierarchy subtree's root (its composite), over which a parent centers.</summary>
    static LayoutNode? Head(LayoutNode node)
    {
        if (node.AlgorithmType == "hierRoot")
        {
            return node.Children.FirstOrDefault(c => !IsHierarchy(c) && c.AlgorithmType != "conn");
        }
        // A composite child is its own head (measured: PictureAccentList's pictures with their texts, as wide as the
        // root above them and at its left).
        if (node.AlgorithmType == "composite")
            return node;
        return node.Descendants().Prepend(node).FirstOrDefault(Draws) ?? node;
    }

    /// <summary>
    /// A hierarchy's row of children (hierChild): the subtrees one after another (linDir) sibSp apart, aligned across
    /// by chAlign (top or left by default).
    /// </summary>
    static (double X, double Y, double W, double H) ArrangeRow(LayoutNode node, Pins pins)
    {
        Constraints.EvaluateList(node, pins);
        (node.X, node.Y) = (0, 0);
        var children = node.Children.Where(c => c.AlgorithmType != "conn").ToList();
        if (children.Count == 0)
            return (0, 0, 0, 0);
        var direction = node.Param("linDir") ?? "fromL";
        var horizontal = direction is "fromL" or "fromR";
        var gap = Value(node, "sibSp");
        var alignment = node.Param("chAlign") ?? (horizontal ? "t" : "l");
        var boxes = children.Select(c => Arrange(c, pins)).ToList();
        var across = boxes.Max(b => horizontal ? b.H : b.W);
        var order = direction is "fromR" or "fromB" ? Enumerable.Range(0, children.Count).Reverse() : Enumerable.Range(0, children.Count);
        // Each subtree as close to the ones before as their shapes allow where they lie side by side, sibSp apart
        // (measured: orgChart1's and hierarchy1's second child beside the first, over its children's row).
        var placed = new List<(double X, double Y, double W, double H)>();
        var position = 0.0;
        foreach (var i in order)
        {
            var b = boxes[i];
            var size = horizontal ? b.W : b.H;
            var cross = Align(alignment switch { "b" or "r" => "b", "ctr" or "mid" => "ctr", _ => "t" }, across, horizontal ? b.H : b.W);
            if (horizontal)
                Translate(children[i], 0, cross - b.Y);
            else
                Translate(children[i], cross - b.X, 0);
            var shapes = Shapes(children[i]);
            double? shift = null;
            foreach (var a in placed)
            {
                foreach (var s in shapes)
                {
                    var side = horizontal ? a.Y < s.Y + s.H - 1e-6 && s.Y < a.Y + a.H - 1e-6 : a.X < s.X + s.W - 1e-6 && s.X < a.X + a.W - 1e-6;
                    if (side)
                        shift = Math.Max(shift ?? double.MinValue, horizontal ? a.X + a.W + gap - s.X : a.Y + a.H + gap - s.Y);
                }
            }
            var start = horizontal ? b.X : b.Y;
            var move = placed.Count == 0 ? -start : shift ?? position - start;
            if (horizontal)
                Translate(children[i], move, 0);
            else
                Translate(children[i], 0, move);
            placed.AddRange(Shapes(children[i]));
            position = Math.Max(position, start + move + size + gap);
        }
        return Bounds(node);
    }

    /// <summary>The boxes of a placed subtree's shapes (as <see cref="Bounds"/> counts them).</summary>
    static List<(double X, double Y, double W, double H)> Shapes(LayoutNode node)
    {
        var result = new List<(double X, double Y, double W, double H)>();
        foreach (var n in node.Descendants().Prepend(node))
        {
            if ((string?)n.Shape?.Attribute("type") is null or "none" && (n == node || n.Children.Count > 0) || InConnector(n, node))
                continue;
            result.Add((Math.Min(n.X, n.X + n.W), Math.Min(n.Y, n.Y + n.H), Math.Abs(n.W), Math.Abs(n.H)));
        }
        if (result.Count == 0)
            result.Add((node.X, node.Y, node.W, node.H));
        return result;
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
            // A named end among the point's nodes, else inside them (measured: NumberedDotsVertical's connectors between
            // the number circles its items hold, made for the transitions after them), else the one node of that name the
            // diagram itself holds (cycle8's points beside its wedges).
            if (name != null)
                return candidates.FirstOrDefault(n => n.Name == name)
                       ?? candidates.SelectMany(n => n.Descendants()).FirstOrDefault(n => n.Name == name && n.AlgorithmType != "conn")
                       ?? (all.Where(n => n.Name == name).ToList() is [var only] && only.Point.Type == "doc" ? only : null)
                       ?? candidates.FirstOrDefault(Draws);
            return candidates.FirstOrDefault(n => Draws(n) && (string?)n.Shape?.Attribute("hideGeom") is not ("1" or "true"))
                   ?? candidates.FirstOrDefault(Draws);
        }
        var start = Find(from, connector.Param("srcNode"));
        var end = Find(to, connector.Param("dstNode"));
        // A name the transition's own nodes do not hold names the one node of that name in the diagram, unless that
        // is the other end (measured: gear1's arrows between two points of the same gear).
        LayoutNode? Unique(string? name) => name != null && all.Where(n => n.Name == name).ToList() is [var only] ? only : null;
        if (end != null && end.Name != connector.Param("dstNode") && Unique(connector.Param("dstNode")) is { } namedEnd && namedEnd != start)
            end = namedEnd;
        if (start != null && start.Name != connector.Param("srcNode") && Unique(connector.Param("srcNode")) is { } namedStart && namedStart != end)
            start = namedStart;
        if (start != null && end != null)
            return (start, end);
        // Outside transitions, the siblings srcNode and dstNode name (measured: VerticalCurvedList's arc from its first
        // point to its last).
        if (connector.Parent is { } holder && connector.Param("srcNode") is { } source && connector.Param("dstNode") is { } target
            && holder.Children.FirstOrDefault(c => c.Name == source) is { } named && holder.Children.FirstOrDefault(c => c.Name == target) is { } other)
            return (named, other);
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
        if (start != null && start == end && connector.Param("connRout") is "curve" or "longCurve" && connector.Parent is { AlgorithmType: "cycle" } ring)
        {
            Ring(connector, start, ring, pins);
            return;
        }
        if (start == null || end == null || start == end)
            return;
        if (connector.Param("connRout") is "curve" or "longCurve" && connector.Values.ContainsKey("arcR") && Value(connector, "arcR") > 0)
        {
            Arc(connector, start, end, pins);
            return;
        }
        // A curved connector with a diameter: along the circle of that size through its ends, the short way round
        // clockwise (measured: cycle8's arrows between the points beside its wedges).
        // A negative diameter runs the other way round, counterclockwise (measured: gear1's second arrow, drawn as a left
        // circular arrow).
        // Between the points begPts and endPts name, else the centers; a diameter of the distance between them
        // (connDist) follows it (measured: hProcess4's arrows, 1.15 of the distance, from below a title to below the
        // next).
        var (px, py) = (start.X + start.W / 2, start.Y + start.H / 2);
        var (qx, qy) = (end.X + end.W / 2, end.Y + end.H / 2);
        if (connector.Param("connRout") is "curve" or "longCurve")
        {
            static string? First(string? points) => points?.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(p => p is not ("auto" or "radial"));
            if (First(connector.Param("begPts")) is { } leaving)
                (px, py) = Point(start, leaving);
            if (First(connector.Param("endPts")) is { } arriving)
                (qx, qy) = Point(end, arriving);
            if (Finite(connector, "diam") is null or 0
                && connector.Constraints.LastOrDefault(c => (string?)c.Attribute("type") == "diam") is { } rule && (string?)rule.Attribute("refType") == "connDist")
            {
                var between = Math.Sqrt((qx - px) * (qx - px) + (qy - py) * (qy - py));
                connector.Values["connDist"] = between;
                connector.Values["diam"] = (Number((string?)rule.Attribute("fact")) ?? 1) * between / Math.Max(connector.Scale, 1e-9);
            }
        }
        if (connector.Param("connRout") is "curve" or "longCurve" && Finite(connector, "diam") is { } signed && Math.Abs(signed) > 0)
        {
            var diameter = Math.Abs(signed);
            var turning = signed < 0 ? -1 : 1;
            var radius = diameter / 2 * connector.Scale;
            var chord = Math.Sqrt((qx - px) * (qx - px) + (qy - py) * (qy - py));
            if (chord > 1e-9)
            {
                var (cux, cuy) = ((qx - px) / chord, (qy - py) / chord);
                var rise = Math.Sqrt(Math.Max(0, radius * radius - chord * chord / 4));
                var (mx, my) = ((px + qx) / 2, (py + qy) / 2);
                (double X, double Y) center = (mx - cuy * rise, my + cux * rise);
                double Sweep((double X, double Y) c) =>
                    ((Math.Atan2(qy - c.Y, qx - c.X) - Math.Atan2(py - c.Y, px - c.X)) * 180 / Math.PI * turning % 360 + 360) % 360;
                if (Sweep(center) > 180)
                    center = (mx + cuy * rise, my - cux * rise);
                (connector.Values["arcX"], connector.Values["arcY"], connector.Values["arcR"]) = (center.X, center.Y, Math.Max(radius, chord / 2));
                connector.Values["arcDir"] = turning;
                Arc(connector, start, end, pins);
                return;
            }
        }
        if (connector.Param("connRout") == "bend")
        {
            Elbow(connector, start, end, pins);
            return;
        }
        var (ax, ay) = (start.X + start.W / 2, start.Y + start.H / 2);
        var (bx, by) = (end.X + end.W / 2, end.Y + end.H / 2);
        // Radial points in a cycle: where its circle leaves the first node and reaches the second (measured: cycle2's
        // arrows between two circles, over and under the middle).
        var radial = connector.Param("begPts") == "radial" && connector.Param("endPts") == "radial"
            && connector.Values.ContainsKey("arcR") && Value(connector, "arcR") > 0;
        if (radial)
        {
            var (ox, oy, r) = (Value(connector, "arcX"), Value(connector, "arcY"), Value(connector, "arcR"));
            var way = Value(connector, "arcDir") < 0 ? -1 : 1;
            (ax, ay) = OnCircle(ox, oy, r, Leave(start, ox, oy, r, Math.Atan2(ay - oy, ax - ox) * 180 / Math.PI, way));
            (bx, by) = OnCircle(ox, oy, r, Leave(end, ox, oy, r, Math.Atan2(by - oy, bx - ox) * 180 / Math.PI, -way));
        }
        // Between the connection points begPts and endPts name, the nearest pair (measured: hierarchy5's lines from a
        // node's right middle to its children's left middles).
        static List<string> Named(string? points) =>
            points?.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).Where(p => p is not ("auto" or "radial" or "ctr")).ToList() ?? [];
        var (begins, ends) = (Named(connector.Param("begPts")), Named(connector.Param("endPts")));
        // One end named, the other automatic: that end at the middle of its node's side nearest to the named point
        // (measured: SubStepProcess's lines from a step's circle to the left or right middle of its sub-steps' rows).
        List<string> sides = ["tCtr", "midL", "midR", "bCtr"];
        if (!radial && begins.Count > 0 && ends.Count == 0 && connector.Param("endPts") is null or "auto")
            ends = sides;
        else if (!radial && ends.Count > 0 && begins.Count == 0 && connector.Param("begPts") is null or "auto")
            begins = sides;
        var named = !radial && begins.Count > 0 && ends.Count > 0;
        if (named)
        {
            var pair = begins.SelectMany(n => ends.Select(m => (P: Point(start, n), Q: Point(end, m))))
                .MinBy(pq => (pq.Q.X - pq.P.X) * (pq.Q.X - pq.P.X) + (pq.Q.Y - pq.P.Y) * (pq.Q.Y - pq.P.Y));
            ((ax, ay), (bx, by)) = (pair.P, pair.Q);
        }
        var (dx, dy) = (bx - ax, by - ay);
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1e-9)
            return;
        var (ux, uy) = (dx / length, dy / length);
        // From the nodes' outlines, or from a center the points name (begPts, endPts ctr; measured: radial4's arrows
        // from the middle circle to its nodes' centers), or from the radial or named points.
        var (ta, tb) = radial || named ? (0, 0) : (connector.Param("begPts") == "ctr" ? 0 : Edge(start, ux, uy), connector.Param("endPts") == "ctr" ? 0 : Edge(end, -ux, -uy));
        var distance = Math.Max(0, length - ta - tb);
        // The distance its pads and length refer to: its own, or the one an equ constraint gives it.
        var reference = connector.Values.TryGetValue("connDistEqu", out var equal) ? equal : distance;
        connector.Values["connDist"] = reference;
        var oneD = connector.Param("dim") == "1D";
        var (begPad, endPad) = Pads(connector, reference, oneD ? (0.0, 0.0) : (0.22 * reference, 0.25 * reference));
        var span = Math.Max(0, reference - begPad - endPad);
        // As high as given, else a line without height (dim 1D, measured: RadialCluster's lines) or 0.62 of its length.
        var thickness = connector.H > 0 ? connector.H : (Finite(connector, "h") ?? (oneD ? 0 : span * 0.62)) * connector.Scale;
        // An arrow as high as its head where the head's height is given (measured: lProcess1's arrows, hArH half their
        // connector's height).
        if (Finite(connector, "hArH") is > 0 and var head && Finite(connector, "h") is > 0 and var full)
            thickness *= head / full;
        // A connector without arrowheads draws its stem alone, stemThick of its height, else 0.6 of it unless a line
        // (measured: NumberedDotsVertical's lines between the circles, 0.3 of the connector's height; bProcess4's bars
        // 0.6; radial1's lines, dim 1D, as high as given).
        if (connector.Param("begSty") != "arr" && connector.Param("endSty") is { } endStyle && endStyle is not ("arr" or "auto")
            && Finite(connector, "h") is > 0 and var height && (Finite(connector, "stemThick") ?? (oneD ? null : 0.6 * height)) is { } stem)
            thickness *= stem / height;
        // Centered between its pads (measured: bProcess2's triangles, shorter than their gaps).
        var from = ta + (distance + begPad - endPad) / 2;
        var (cx, cy) = (ax + ux * from, ay + uy * from);
        PlaceConnector(connector, cx - span / 2, cy - thickness / 2, span, thickness, pins);
        var angle = Math.Atan2(uy, ux) * 180 / Math.PI;
        connector.Rotation = Math.Abs(angle) < 1e-9 ? 0 : (angle % 360 + 360) % 360;
    }

    /// <summary>
    /// A connector's pads (begPad, endPad) by its own constraints, which refer to the distance between its ends
    /// (connDist) or its own size (measured: lProcess1's arrows between items, padded by a quarter of their width at each
    /// end), else as given.
    /// </summary>
    static (double Begin, double End) Pads(LayoutNode connector, double distance, (double Begin, double End) given)
    {
        var (begPad, endPad) = given;
        foreach (var constraint in connector.Constraints)
        {
            var type = (string?)constraint.Attribute("type");
            if (type is not ("begPad" or "endPad"))
                continue;
            var fact = Number((string?)constraint.Attribute("fact")) ?? 1;
            var value = (string?)constraint.Attribute("refType") switch
            {
                "connDist" => distance * fact,
                "w" when Finite(connector, "w") is { } cw => cw * connector.Scale * fact,
                "h" when Finite(connector, "h") is { } ch => ch * connector.Scale * fact,
                _ => Number((string?)constraint.Attribute("val")) ?? 0,
            };
            if (type == "begPad")
                begPad = value;
            else
                endPad = value;
        }
        return (begPad, endPad);
    }

    /// <summary>A connection point of a node's box (tL, tCtr, tR, midL, ctr, midR, bL, bCtr, bR).</summary>
    static (double X, double Y) Point(LayoutNode node, string name) => name switch
    {
        "tL" => (node.X, node.Y),
        "tCtr" => (node.X + node.W / 2, node.Y),
        "tR" => (node.X + node.W, node.Y),
        "midL" => (node.X, node.Y + node.H / 2),
        "midR" => (node.X + node.W, node.Y + node.H / 2),
        "bL" => (node.X, node.Y + node.H),
        "bCtr" => (node.X + node.W / 2, node.Y + node.H),
        "bR" => (node.X + node.W, node.Y + node.H),
        _ => (node.X + node.W / 2, node.Y + node.H / 2),
    };

    /// <summary>
    /// A bent connector (connRout bend): from one of the first node's begPts points to one of the second's endPts points
    /// (the nearest pair; by default the facing sides' centers), leaving and arriving straight: across bendDist before
    /// the end when both run the same way (half the way by default; measured with hierarchy1), with a single corner when
    /// one runs down and the other across (an organization chart's assistant). A line without width gets a box of 7.2 pt.
    /// </summary>
    static void Elbow(LayoutNode connector, LayoutNode start, LayoutNode end, Pins pins)
    {
        var (sx, sy) = (start.X + start.W / 2, start.Y + start.H / 2);
        var (ex, ey) = (end.X + end.W / 2, end.Y + end.H / 2);
        var down = Math.Abs(ey - sy) >= Math.Abs(ex - sx);
        IEnumerable<string> Names(string? points, string fallback)
        {
            var names = points?.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p != "auto").ToList() ?? [];
            return names.Count > 0 ? names : [fallback];
        }
        var begins = Names(connector.Param("begPts"), down ? (ey > sy ? "bCtr" : "tCtr") : (ex > sx ? "midR" : "midL"));
        var ends = Names(connector.Param("endPts"), down ? (ey > sy ? "tCtr" : "bCtr") : (ex > sx ? "midL" : "midR"));
        var (from, to) = begins.SelectMany(b => ends.Select(e => (B: b, E: e)))
            .MinBy(pair =>
            {
                var (p, q) = (Point(start, pair.B), Point(end, pair.E));
                return Math.Abs(q.X - p.X) + Math.Abs(q.Y - p.Y);
            });
        var a = Point(start, from);
        var b = Point(end, to);
        static bool Vertical(string point) => point is "tCtr" or "bCtr" or "tL" or "tR" or "bL" or "bR";
        var points = new List<(double X, double Y)> { a };
        if (Vertical(from) && Vertical(to))
        {
            var bend = Finite(connector, "bendDist") is { } d ? b.Y - Math.Sign(b.Y - a.Y) * d : (a.Y + b.Y) / 2;
            points.Add((a.X, bend));
            points.Add((b.X, bend));
        }
        else if (!Vertical(from) && !Vertical(to))
        {
            var bend = Finite(connector, "bendDist") is { } d ? b.X - Math.Sign(b.X - a.X) * d : (a.X + b.X) / 2;
            points.Add((bend, a.Y));
            points.Add((bend, b.Y));
        }
        else
            points.Add(Vertical(from) ? (a.X, b.Y) : (b.X, a.Y));
        points.Add(b);
        var (minX, minY) = (points.Min(p => p.X), points.Min(p => p.Y));
        var (w, h) = (points.Max(p => p.X) - minX, points.Max(p => p.Y) - minY);
        const double Thin = 2.54;   // 7.2 pt
        if (w < 1e-6)
            (minX, w) = (minX - Thin / 2, Thin);
        if (h < 1e-6)
            (minY, h) = (minY - Thin / 2, Thin);
        PlaceConnector(connector, minX, minY, w, h, pins);
        connector.Rotation = 0;
        while (points.Count < 4)
            points.Insert(points.Count - 1, points[^1]);
        for (var i = 0; i < 4; i++)
        {
            connector.Values[$"elbowX{i + 1}"] = points[i].X - minX;
            connector.Values[$"elbowY{i + 1}"] = points[i].Y - minY;
        }
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
        // The pads along the arc, of the straight distance between where it leaves and reaches its nodes (measured:
        // cycle5's arrows a fifth of it short of their nodes at each end).
        var chord = 2 * r * Math.Sin(sweep * Math.PI / 360);
        connector.Values["connDist"] = chord;
        var (begPad, endPad) = Pads(connector, chord, (0, 0));
        if (r > 0 && (begPad + endPad) / r * 180 / Math.PI < sweep)
        {
            begin += way * begPad / r * 180 / Math.PI;
            sweep -= (begPad + endPad) / r * 180 / Math.PI;
        }
        var head = r > 0 ? 0.5 * thickness / r * 180 / Math.PI : 0;
        // A line's box is the circle's (dim 1D, measured: cycle5's), an arrow's reaches beyond it, a band without heads
        // half its width (measured: VerticalCurvedList's, 0.6 of its thickness wide).
        // In a cycle the band lies a quarter of its width further in (measured: radial6's ring, its outer edge 0.15 of
        // the connector's height beyond the nodes' centers).
        var reach = connector.Param("dim") == "1D" ? r : r + (ArcHeads(connector) ? 0.65 : connector.Parent?.AlgorithmType == "cycle" ? 0.15 : 0.3) * thickness;
        PlaceConnector(connector, cx - reach, cy - reach, 2 * reach, 2 * reach, pins);
        connector.Rotation = 0;
        connector.Values["arcStart"] = begin;
        connector.Values["arcSweep"] = sweep;
        connector.Values["arcHead"] = Math.Min(head, sweep);
        connector.Values["arcThickness"] = thickness;
    }

    /// <summary>
    /// A curved connector from a node back to itself in a cycle: the whole circle through the node around the cycle's
    /// middle, short of the node by 0.09° at each end (measured: radial6 with one node, its band from 0.09° to 359.91°).
    /// </summary>
    static void Ring(LayoutNode connector, LayoutNode node, LayoutNode ring, Pins pins)
    {
        var (cx, cy) = (ring.X + ring.W / 2, ring.Y + ring.H / 2);
        var (nx, ny) = (node.X + node.W / 2, node.Y + node.H / 2);
        var r = Math.Sqrt((nx - cx) * (nx - cx) + (ny - cy) * (ny - cy));
        if (r < 1e-9)
            return;
        var way = Finite(connector, "diam") is < 0 ? -1 : 1;
        const double Gap = 0.0909;
        var angle = Math.Atan2(ny - cy, nx - cx) * 180 / Math.PI;
        var thickness = Value(connector, "h") * connector.Scale;
        var reach = connector.Param("dim") == "1D" ? r : r + (ArcHeads(connector) ? 0.65 : 0.15) * thickness;
        PlaceConnector(connector, cx - reach, cy - reach, 2 * reach, 2 * reach, pins);
        connector.Rotation = 0;
        (connector.Values["arcX"], connector.Values["arcY"], connector.Values["arcR"], connector.Values["arcDir"]) = (cx, cy, r, way);
        connector.Values["arcStart"] = angle + way * Gap;
        connector.Values["arcSweep"] = 360 - 2 * Gap;
        connector.Values["arcHead"] = r > 0 ? Math.Min(0.5 * thickness / r * 180 / Math.PI, 360 - 2 * Gap) : 0;
        connector.Values["arcThickness"] = thickness;
    }

    /// <summary>Whether a curved connector draws arrowheads (begSty, endSty arr; the end's by default).</summary>
    static bool ArcHeads(LayoutNode connector) =>
        connector.Param("begSty") == "arr" || connector.Param("endSty") is null or "arr" or "auto";

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
