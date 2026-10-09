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
/// (measured: process3's composite, 1000 times as high as wide, is as high as its shapes); lin applies its rules on its
/// children's length when they do not fit, lin and snake shrink their children uniformly when they still do not, and
/// align them by the room they actually take. The text is then fitted by the rules and the diagram laid out again with
/// the fitted values until nothing changes. Lengths in millimeters.
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
            foreach (var child in node.Children.Where(c => c.AlgorithmType == "conn"))
                Connector(child, pins);
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
                    Constraints.EvaluateOwn(child, running);
                (pw, ph) = (aw, ah);
            }
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var child in children)
            {
                var (cx, cy, cw, ch) = ChildRect(child, pw, ph);
                var e = child.Children.Count > 0 ? Extent(child) : (0, 0, cw, ch);
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
        if (node.AlgorithmType == "lin" && children.Count > 0 && (w == null || h == null || growsW || growsH))
        {
            var horizontal = (node.Param("linDir") ?? "fromL") is "fromL" or "fromR";
            var extents = children.Select(Extent).ToList();
            var gap = Value(node, "sp") * Pairs(children);
            var along = extents.Sum(e => horizontal ? e.W : e.H) + gap;
            var across = extents.Max(e => horizontal ? e.H : e.W);
            var (ew, eh) = horizontal ? (along, across) : (across, along);
            return (0, 0, w is { } fw ? growsW ? Math.Max(fw, ew) : fw : ew, h is { } fh ? growsH ? Math.Max(fh, eh) : fh : eh);
        }
        // A leaf's own size, its offsets added (ChildRect).
        var (_, _, ow, oh) = ChildRect(node, 0, 0);
        return (0, 0, ow, oh);
    }

    /// <summary>
    /// Whether a lin node grows with its content in a dimension (w, h): a rule lets it (type w val INF, measured:
    /// hierarchy4's rows of boxes as wide as the boxes under them), or a lin child of it grows so.
    /// </summary>
    static bool Grows(LayoutNode node, string type) =>
        node.AlgorithmType == "lin"
        && (node.Rules.Any(r => (string?)r.Attribute("type") == type && (string?)r.Attribute("val") == "INF")
            || node.Children.Any(c => c.AlgorithmType == "lin" && Grows(c, type)));

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
        (w, h) = Fitted(child, w, h);
        if (child.AlgorithmType == "composite" && h > 100 * Math.Max(w, 1e-6))
            h = extent.Y + extent.H;
        Place(child, -extent.X * factor, -extent.Y * factor, w * factor, h * factor, pins);
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
        var pairs = Pairs(children);
        var extents = ApplyRules(node, children, horizontal, boxAlong, pairs, pins, ruled);
        // sp after the rules: it may follow a value they changed (TextCardSideLineWideImage's, 0.12 of the cards' height).
        var gap = Value(node, "sp");
        var along = extents.Sum(e => horizontal ? e.W : e.H) + gap * pairs;
        if (Environment.GetEnvironmentVariable("FS_TRACE") == "1")
            Console.Error.WriteLine($"lin {node.Name} box {boxAlong:0.##}x{boxAcross:0.##} along {along:0.##} extents {string.Join(", ", children.Select((c, i) => $"{c.Name}=({extents[i].W:0.##},{extents[i].H:0.##})"))}");
        double factor;
        if (node.Param("fallback") == "2D")
        {
            var across = extents.Max(e => horizontal ? e.H : e.W);
            factor = Math.Min(along > 0 ? boxAlong / along : 1, across > 0 ? boxAcross / across : 1);
            if (!double.IsFinite(factor) || factor <= 0)
                factor = 1;
            for (var i = 0; i < children.Count; i++)
                PlaceScaled(children[i], extents[i], factor, pins);
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
                sizes.Add((length, cross));
            }
            var widest = sizes.Max(z => z.Across);
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
                if (Grows(children[i], "w"))
                    ow = Math.Max(ow, e.X + e.W);
                if (Grows(children[i], "h"))
                    oh = Math.Max(oh, e.Y + e.H);
                if (children[i].AlgorithmType == "composite" && oh > 100 * Math.Max(ow, 1e-6))
                    oh = e.Y + e.H;
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
        // nothing), any other the room its drawn shapes take.
        var bounds = children.Select(c => c.AlgorithmType == "lin" ? (c.X, c.Y, c.W, c.H) : Bounds(c)).ToList();
        var total = bounds.Sum(b => horizontal ? b.W : b.H) + gap * factor * pairs;
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

    static int Pairs(List<LayoutNode> children) => Enumerable.Range(0, Math.Max(0, children.Count - 1)).Count(i => Spaced(children[i], children[i + 1]));

    /// <summary>A composite's size kept to its aspect ratio (ar, width : height) within a size; any other node's as given.</summary>
    static (double W, double H) Fitted(LayoutNode node, double w, double h)
    {
        if (node.AlgorithmType == "composite" && Number(node.Param("ar")) is > 0 and var ar && w > 0 && h > 0)
            return w / h > ar ? (h * ar, h) : (w, w / ar);
        return (w, h);
    }

    /// <summary>Evaluates the lists under a node again for its values (as its layout would, without placing it).</summary>
    static void Refresh(LayoutNode node, Pins pins)
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
    /// fit it, in order: each lowers its targets' value toward its limit (val, or fact times the value) by halving the
    /// step, up to twelve times, until the children fit with less than a point to spare, keeping the largest value
    /// that fits; the values that follow it are evaluated again (measured: hProcess3's columns, w toward 0, end at
    /// 987/4096 of their width with 0.6 pt to spare, process3's at 279/1024 with 0.83 pt). The values are pinned for the
    /// children's layout (<paramref name="ruled"/>); the children's extents with them.
    /// </summary>
    static List<(double X, double Y, double W, double H)> ApplyRules(LayoutNode node, List<LayoutNode> children, bool horizontal, double box, int pairs, Pins pins, Dictionary<LayoutNode, double?> ruled)
    {
        var type = horizontal ? "w" : "h";
        var extents = children.Select(Extent).ToList();
        double Left() => box - extents.Sum(e => horizontal ? e.W : e.H) - Value(node, "sp") * pairs;
        bool Fits() => Left() >= -1e-9;
        foreach (var rule in node.Rules)
        {
            if (Fits())
                break;
            if ((string?)rule.Attribute("type") != type)
                continue;
            var targets = Constraints.Targets(node, (string?)rule.Attribute("for"), (string?)rule.Attribute("forName"), (string?)rule.Attribute("ptType"))
                .Where(children.Contains).ToList();
            var starts = targets.Select(t => Finite(t, type)).ToList();
            if (targets.Count == 0 || starts.Any(s => s == null))
                continue;
            var limits = starts.Select(s => Number((string?)rule.Attribute("val")) is { } v && double.IsFinite(v) ? v
                : Number((string?)rule.Attribute("fact")) is { } f && double.IsFinite(f) ? f * s!.Value : double.NaN).ToList();
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
    /// largest (more per row when equal); within a row the cells keep the room of the spacers between them (the
    /// transitions' nodes, measured with bList2), else sp, and the rows sp apart; each row centered, the grid centered in
    /// the node, growing from the corner grDir names, every other row reversed with contDir revDir.
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
        var columns = node.Param("flowDir") == "col";
        var spacers = node.Children.Where(c => IsTransition(c) && c.AlgorithmType != "conn").Select(Extent).ToList();
        var lineGap = Value(node, "sp");
        var gap = spacers.Count > 0 ? spacers.Max(e => columns ? e.H : e.W) : lineGap;
        var n = cells.Count;
        var best = (Factor: 0.0, Count: 1);
        for (var count = 1; count <= n; count++)
        {
            var lines = (n + count - 1) / count;
            var (gw, gh) = columns
                ? (lines * cw + (lines - 1) * lineGap, count * ch + (count - 1) * gap)
                : (count * cw + (count - 1) * gap, lines * ch + (lines - 1) * lineGap);
            var factor = Math.Min(node.W / gw, node.H / gh);
            if (factor > best.Factor * (1 + 1e-9))
                best = (factor, count);
        }
        var k = best.Factor;
        // Connectors between the cells shrink with them.
        foreach (var connector in node.Children.Where(c => c.AlgorithmType == "conn"))
            connector.Scale = k;
        // As many cells per line as fit at that size.
        var perLine = columns
            ? Math.Max(best.Count, (int)Math.Floor((node.H + gap * k) / ((ch + gap) * k) + 1e-6))
            : Math.Max(best.Count, (int)Math.Floor((node.W + gap * k) / ((cw + gap) * k) + 1e-6));
        perLine = Math.Min(perLine, n);
        var lineCount = (n + perLine - 1) / perLine;
        var (scw, sch, sgap, slineGap) = (cw * k, ch * k, gap * k, lineGap * k);
        var gridAcross = lineCount * (columns ? scw : sch) + (lineCount - 1) * slineGap;
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
            var across = start + line * ((columns ? scw : sch) + slineGap);
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
        var (ox, oy) = (bx + (bw - box.W * factor) / 2, by + (bh - box.H * factor) / 2);
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
        // Rows hang from the root's own box (a composite's, not the union of its shapes).
        (double X, double Y, double W, double H) shape = own != null ? (own.X, own.Y, own.W, own.H) : (0, 0, 0, 0);
        var (centerX, centerY) = (shape.X + shape.W / 2, shape.Y + shape.H / 2);
        var next = horizontal ? (align.StartsWith('l') ? shape.X + shape.W + sp : shape.X - sp) : shape.Y + shape.H + sp;
        if (assistants != null && assistants.Children.Count > 0)
        {
            var a = ArrangeRow(assistants, pins);
            Translate(assistants, centerX - sp / 2 - (a.X + a.W), next - a.Y);
            next += a.H + sp;
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
        var position = 0.0;
        foreach (var i in order)
        {
            var b = boxes[i];
            var size = horizontal ? b.W : b.H;
            var cross = Align(alignment switch { "b" or "r" => "b", "ctr" or "mid" => "ctr", _ => "t" }, across, horizontal ? b.H : b.W);
            if (horizontal)
                Translate(children[i], position - b.X, cross - b.Y);
            else
                Translate(children[i], cross - b.X, position - b.Y);
            position += size + gap;
        }
        return Bounds(node);
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
        if (connector.Param("connRout") == "bend")
        {
            Elbow(connector, start, end, pins);
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
