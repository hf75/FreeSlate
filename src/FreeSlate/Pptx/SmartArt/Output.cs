using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Drawing.Geometry;

namespace FreeSlate.Pptx.SmartArt;

/// <summary>
/// The shapes of a laid-out SmartArt diagram (catalog: smartart.layout, smartart.styles), written the way PowerPoint
/// writes a diagram's drawing (measured from the drawings of all 159 layouts):
/// <list type="bullet">
/// <item>every layout node with a shape type draws a shape (type none draws nothing), in tree order, a node's shape
/// behind its children's; zOrderOff moves a node among its siblings;</item>
/// <item>text is laid out by the text algorithm's nodes: a shape showing the same data points as a text node takes
/// that node's text and text rectangle, and a text node without geometry (hideGeom) whose text a shape takes draws
/// nothing itself (a connector's text, a Venn circle's text); one without such a shape draws its text alone;</item>
/// <item>a shape turned by its shape's rot by a quarter fills its box turned (width and height swapped); text stays
/// upright unless the text algorithm says otherwise (autoTxRot);</item>
/// <item>colors, lines and effects come from the color and style definitions by the shape's style label (the saved
/// presentation point's, the layout node's, the label of the nearest ancestor showing the same data point, else by
/// the point's type and depth: node1 to node4, parChTrans1D/2D by depth, sibTrans1D1/2D1, asst), each label's
/// colors spread over its shapes; the style references take the colors, and hidden geometry has no fill and line.</item>
/// </list>
/// </summary>
public static class Output
{
    static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace D = DataModel.Dgm;
    const double EmuPerMm = 36000;

    /// <summary>Adjust values of presets that are angles (degrees in the layout, 60000ths of a degree in the shape).</summary>
    static readonly HashSet<(string Preset, int Index)> Angles =
    [
        ("arc", 1), ("arc", 2), ("blockArc", 1), ("blockArc", 2), ("chord", 1), ("chord", 2), ("pie", 1), ("pie", 2),
        ("circularArrow", 2), ("circularArrow", 3), ("circularArrow", 4), ("leftCircularArrow", 2), ("leftCircularArrow", 3),
        ("leftCircularArrow", 4), ("leftRightCircularArrow", 2), ("leftRightCircularArrow", 3), ("leftRightCircularArrow", 4),
        ("mathNotEqual", 2),
    ];

    /// <summary>A shape's geometry: preset (or null for a line), adjust values, box (mm), rotation and flips.</summary>
    sealed record Geometry(string? Preset, List<XElement> Adjust, double X, double Y, double W, double H, double Rotation, bool Line, bool HeadArrow, bool TailArrow);

    /// <summary>The shapes of the diagram as a PresentationML shape tree, in EMU relative to the diagram's frame.</summary>
    public static XElement Shapes(LayoutNode root, Styling styling, XElement? listStyle)
    {
        var all = root.Descendants().Prepend(root).ToList();
        var byText = all.Where(n => n.Text.Count > 0).GroupBy(TextKey).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var textNodes = all.ToDictionary(n => n, n => TextLayout(n, byText));
        // Text nodes without geometry whose text a shape takes draw nothing themselves.
        var merged = new HashSet<LayoutNode>();
        foreach (var node in all)
        {
            if (Visible(node) && textNodes[node] is { } textNode && textNode != node && Hidden(textNode))
                merged.Add(textNode);
        }
        var labels = all.ToDictionary(n => n, Label);
        var indices = StyleIndices(all, labels);

        var tree = new XElement(P + "spTree",
            new XElement(P + "nvGrpSpPr", new XElement(P + "cNvPr", new XAttribute("id", 0), new XAttribute("name", "")), new XElement(P + "cNvGrpSpPr"), new XElement(P + "nvPr")),
            new XElement(P + "grpSpPr"));
        var number = 0;
        foreach (var node in Ordered(root))
        {
            if (merged.Contains(node))
                continue;
            if (!Visible(node) && !(Hidden(node) && node.Text.Count > 0))
                continue;
            if (Shape(node, textNodes[node], labels[node], indices.GetValueOrDefault(node, (0, 1)), styling, listStyle, ++number) is { } shape)
                tree.Add(shape);
        }
        return tree;
    }

    static string? ShapeType(LayoutNode node) => (string?)node.Shape?.Attribute("type");

    static bool HideGeometry(LayoutNode node) => (string?)node.Shape?.Attribute("hideGeom") is "1" or "true";

    /// <summary>A node drawing a shape of its own: a shape type, geometry not hidden.</summary>
    static bool Visible(LayoutNode node) => ShapeType(node) is { } type && type != "none" && !HideGeometry(node);

    /// <summary>A node with a shape type whose geometry is hidden: it shows only text.</summary>
    static bool Hidden(LayoutNode node) => ShapeType(node) is { } type && type != "none" && HideGeometry(node);

    static string TextKey(LayoutNode node) => string.Join("|", node.Text.Select(p => p.Id));

    /// <summary>The node whose text and font sizes a node's shape shows (a text node of the same data), or null.</summary>
    public static LayoutNode? TextSource(LayoutNode root, LayoutNode node)
    {
        var all = root.Descendants().Prepend(root).ToList();
        var byText = all.Where(n => n.Text.Count > 0).GroupBy(TextKey).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        return TextLayout(node, byText);
    }

    /// <summary>
    /// The node laying out a node's text: the node itself if it runs the text algorithm, else a text node showing the
    /// same data points (its own descendants first, then its siblings, then any), else the node itself.
    /// </summary>
    static LayoutNode? TextLayout(LayoutNode node, Dictionary<string, List<LayoutNode>> byText)
    {
        if (node.Text.Count == 0)
            return null;
        if (node.AlgorithmType == "tx")
            return node;
        if (!byText.TryGetValue(TextKey(node), out var same))
            return node;
        var candidates = same.Where(n => n != node && n.AlgorithmType == "tx").ToList();
        return candidates.FirstOrDefault(n => IsAncestor(node, n))
               ?? candidates.FirstOrDefault(n => n.Parent == node.Parent)
               ?? candidates.FirstOrDefault()
               ?? node;
    }

    static bool IsAncestor(LayoutNode ancestor, LayoutNode node)
    {
        for (var p = node.Parent; p != null; p = p.Parent)
        {
            if (p == ancestor)
                return true;
        }
        return false;
    }

    /// <summary>The nodes in drawing order: a node before its children, children moved by their zOrderOff.</summary>
    static IEnumerable<LayoutNode> Ordered(LayoutNode node)
    {
        yield return node;
        var children = node.Children
            .Select((child, index) =>
            {
                var offset = int.TryParse((string?)child.Shape?.Attribute("zOrderOff"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var z) ? z : 0;
                return (Key: index + offset + 0.5 * Math.Sign(offset), Child: child);
            })
            .OrderBy(c => c.Key)
            .Select(c => c.Child);
        foreach (var child in children)
        {
            foreach (var n in Ordered(child))
                yield return n;
        }
    }

    /// <summary>
    /// The style label of a node: its saved presentation point's, its own, that of the nearest ancestor showing the
    /// same data point, else by the point's type and depth.
    /// </summary>
    static string Label(LayoutNode node)
    {
        if (node.Saved?.Prop("presStyleLbl") is { Length: > 0 } saved)
            return saved;
        for (var n = node; n != null && n.Point == node.Point; n = n.Parent)
        {
            if (n.StyleLabel is { Length: > 0 } label)
                return label;
        }
        var point = node.Point;
        var depth = DataModel.Depth(point);
        var oneD = Connector(node) is { } connector && connector.Param("dim") == "1D";
        return point.Type switch
        {
            "parTrans" => (oneD ? "parChTrans1D" : "parChTrans2D") + Math.Clamp(depth, 1, 4),
            "sibTrans" => oneD ? "sibTrans1D1" : "sibTrans2D1",
            "asst" => "asst" + Math.Clamp(depth - 1, 0, 4),
            "doc" => "node1",
            _ => "node" + Math.Clamp(depth, 0, 4),
        };
    }

    /// <summary>The connector a node belongs to: itself or the nearest ancestor running the connector algorithm.</summary>
    static LayoutNode? Connector(LayoutNode node)
    {
        for (var n = node; n != null; n = n.Parent)
        {
            if (n.AlgorithmType == "conn")
                return n;
            if (n.Point != node.Point)
                break;
        }
        return null;
    }

    /// <summary>
    /// Each node's index among the nodes of its style label and their count: the saved presentation point's, else
    /// counted in tree order, nodes showing the same data points sharing an index (measured).
    /// </summary>
    static Dictionary<LayoutNode, (int Index, int Count)> StyleIndices(List<LayoutNode> all, Dictionary<LayoutNode, string> labels)
    {
        var result = new Dictionary<LayoutNode, (int, int)>();
        foreach (var group in all.Where(n => n.Shape != null).GroupBy(n => labels[n]))
        {
            var keys = new List<string>();
            var keyOf = new Dictionary<LayoutNode, int>();
            foreach (var node in group)
            {
                var key = node.Text.Count > 0 ? "t:" + TextKey(node) : "n:" + keyOf.Count.ToString(CultureInfo.InvariantCulture) + ":" + node.Name;
                var index = keys.IndexOf(key);
                if (index < 0)
                {
                    keys.Add(key);
                    index = keys.Count - 1;
                }
                keyOf[node] = index;
            }
            foreach (var node in group)
            {
                if (node.Saved is { } saved && int.TryParse(saved.Prop("presStyleIdx"), out var i) && int.TryParse(saved.Prop("presStyleCnt"), out var c) && c > 0)
                    result[node] = (i, c);
                else
                    result[node] = (keyOf[node], keys.Count);
            }
        }
        return result;
    }

    static double Number(string? text, double fallback = 0) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : fallback;

    static string Emu(double mm) => ((long)Math.Round(mm * EmuPerMm)).ToString(CultureInfo.InvariantCulture);

    static string Angle(double degrees) => ((long)Math.Round(degrees * 60000)).ToString(CultureInfo.InvariantCulture);

    /// <summary>The box of the geometry a node draws (millimeters, a quarter-turned shape's box turned), or null.</summary>
    public static (double X, double Y, double W, double H)? DrawnBox(LayoutNode node) =>
        GeometryOf(node) is { } g ? (g.X, g.Y, g.W, g.H) : null;

    /// <summary>The geometry a node draws, or null for none.</summary>
    static Geometry? GeometryOf(LayoutNode node)
    {
        var type = ShapeType(node);
        if (type is null or "none")
            return null;
        var turn = Number((string?)node.Shape!.Attribute("rot"));
        var (x, y, w, h) = (node.X, node.Y, node.W, node.H);
        if (type == "conn")
            return ConnectorGeometry(node, x, y, w, h);
        // A shape turned by a quarter fills its box turned.
        var quarter = Math.Abs(Math.IEEERemainder(turn, 180)) is > 45 and <= 135;
        if (quarter)
            (x, y, w, h) = (x + w / 2 - h / 2, y + h / 2 - w / 2, h, w);
        var adjust = new List<XElement>();
        var names = PresetGeometries.Get(type)?.AdjustValues.Select(a => a.Name).ToList() ?? [];
        foreach (var adj in node.Shape.Element(D + "adjLst")?.Elements(D + "adj") ?? [])
        {
            var index = (int)Number((string?)adj.Attribute("idx"), 1);
            if (index < 1 || index > names.Count)
                continue;
            var value = Number((string?)adj.Attribute("val"));
            var formula = Angles.Contains((type, index))
                ? Angle(((value % 360) + 360) % 360)
                : ((long)Math.Round(value * 100000)).ToString(CultureInfo.InvariantCulture);
            adjust.Add(new XElement(A + "gd", new XAttribute("name", names[index - 1]), new XAttribute("fmla", "val " + formula)));
        }
        // A pyramid level's trapezoid continues the pyramid's sides (Engine.Pyramid).
        if (node.Values.TryGetValue("pyraAdj", out var pyramid) && type == "trapezoid")
            adjust = [new XElement(A + "gd", new XAttribute("name", "adj"), new XAttribute("fmla", "val " + ((long)Math.Round(pyramid * 100000)).ToString(CultureInfo.InvariantCulture)))];
        return new Geometry(type, adjust, x, y, w, h, turn + node.Rotation, false, false, false);
    }

    /// <summary>
    /// A connector's geometry (shape type conn): a block arrow for two-dimensional connectors (right arrow, left arrow
    /// or both by begSty and endSty; a rectangle without arrows), a line with arrowheads for one-dimensional ones.
    /// </summary>
    static Geometry ConnectorGeometry(LayoutNode node, double x, double y, double w, double h)
    {
        var connector = Connector(node) ?? node;
        var begin = connector.Param("begSty") == "arr";
        var end = connector.Param("endSty") is null or "arr" or "auto";
        // A cycle's curved connector: a circular arrow along the circle (Engine.Arc).
        if (connector.Values.TryGetValue("arcSweep", out var sweep) && connector.Param("dim") != "1D")
        {
            var box = Math.Max(connector.W, 1e-6);
            var thickness = connector.Values["arcThickness"];
            var start = connector.Values["arcStart"];
            var head = connector.Values["arcHead"];
            static string Ratio(double v) => ((long)Math.Round(v * 100000)).ToString(CultureInfo.InvariantCulture);
            static string Degrees(double v) => ((long)Math.Round((v % 360 + 360) % 360 * 60000)).ToString(CultureInfo.InvariantCulture);
            XElement Guide(string name, string value) => new(A + "gd", new XAttribute("name", name), new XAttribute("fmla", "val " + value));
            var arcPreset = begin && !end ? "leftCircularArrow" : end || begin ? "circularArrow" : "blockArc";
            List<XElement> arc = arcPreset == "blockArc"
                ? [Guide("adj1", Degrees(start)), Guide("adj2", Degrees(start + sweep)), Guide("adj3", Ratio(0.6 * thickness / box))]
                : [Guide("adj1", Ratio(0.6 * thickness / box)), Guide("adj2", Degrees(head)), Guide("adj3", Degrees(start + sweep - head)),
                   Guide("adj4", Degrees(start)), Guide("adj5", Ratio(0.7 * thickness / box))];
            return new Geometry(arcPreset, arc, connector.X, connector.Y, connector.W, connector.H, 0, false, false, false);
        }
        if (connector.Param("dim") == "1D")
            return new Geometry(null, [], x, y, w, h, connector.Rotation, true, begin, end);
        var preset = (begin, end) switch
        {
            (true, true) => "leftRightArrow",
            (false, true) => "rightArrow",
            (true, false) => "leftArrow",
            _ => "rect",
        };
        List<XElement> adjust = preset == "rect"
            ? []
            : [new XElement(A + "gd", new XAttribute("name", "adj1"), new XAttribute("fmla", "val 60000")),
               new XElement(A + "gd", new XAttribute("name", "adj2"), new XAttribute("fmla", "val 50000"))];
        return new Geometry(preset, adjust, x, y, w, h, connector.Rotation, false, false, false);
    }

    /// <summary>The text rectangle of a geometry (mm, relative to its box, unturned).</summary>
    static (double L, double T, double R, double B) TextRect(Geometry geometry)
    {
        if (geometry.Preset == null || PresetGeometries.Get(geometry.Preset) is not { } definition)
            return (0, 0, geometry.W, geometry.H);
        var adjust = geometry.Adjust.Select(g => new Guide((string)g.Attribute("name")!, (string)g.Attribute("fmla")!)).ToList();
        var evaluated = GeometryEvaluator.Evaluate(definition, geometry.W * EmuPerMm, geometry.H * EmuPerMm, adjust);
        var mmPerPx = Units.EmuPerPixel / EmuPerMm;
        return (evaluated.TextLeft * mmPerPx, evaluated.TextTop * mmPerPx, evaluated.TextRight * mmPerPx, evaluated.TextBottom * mmPerPx);
    }

    /// <summary>
    /// The size (mm) of the box a node lays its text out in: its geometry's text rectangle, turned with the text
    /// (width and height swapped when the text turns a quarter against its shape); the whole box without geometry.
    /// </summary>
    internal static (double W, double H) TextArea(LayoutNode node)
    {
        if (GeometryOf(node) is not { } geometry)
            return (node.W, node.H);
        var (l, t, r, b) = TextRect(geometry);
        var (width, height) = (r - l, b - t);
        if (Math.Abs(Math.IEEERemainder(TextTurn(node, geometry.Rotation) - geometry.Rotation, 180)) is > 45 and <= 135)
            (width, height) = (height, width);
        return (width, height);
    }

    /// <summary>
    /// The turn of a text node's text: upright (autoTxRot upr, the default), with the shape (none), or with the shape but
    /// never upside down (grav).
    /// </summary>
    static double TextTurn(LayoutNode textNode, double shapeTurn)
    {
        var mode = textNode.Param("autoTxRot") ?? "upr";
        var turn = ((shapeTurn % 360) + 360) % 360;
        return mode switch
        {
            "none" => turn,
            "grav" => turn is > 90 and < 270 ? turn - 180 : turn,
            _ => 0,
        };
    }

    static XElement? Shape(LayoutNode node, LayoutNode? textNode, string label, (int Index, int Count) index, Styling styling, XElement? listStyle, int number)
    {
        var geometry = GeometryOf(node);
        if (geometry == null)
            return null;
        var spPr = new XElement(P + "spPr",
            new XElement(A + "xfrm",
                Math.Abs(geometry.Rotation) > 1e-9 ? new XAttribute("rot", Angle(geometry.Rotation)) : null,
                new XElement(A + "off", new XAttribute("x", Emu(geometry.X)), new XAttribute("y", Emu(geometry.Y))),
                new XElement(A + "ext", new XAttribute("cx", Emu(Math.Max(0, geometry.W))), new XAttribute("cy", Emu(Math.Max(0, geometry.H))))));
        if (geometry.Line && (Connector(node) ?? node).Values.ContainsKey("elbowX4"))
        {
            // A bent connector: its path in its box (EMU), from the start through its corners to the end.
            var v = (Connector(node) ?? node).Values;
            static XElement Pt(double x, double y) => new(A + "pt", new XAttribute("x", Emu(x)), new XAttribute("y", Emu(y)));
            var path = new XElement(A + "path", new XAttribute("w", Emu(geometry.W)), new XAttribute("h", Emu(geometry.H)), new XAttribute("fill", "none"),
                new XElement(A + "moveTo", Pt(v["elbowX1"], v["elbowY1"])),
                new XElement(A + "lnTo", Pt(v["elbowX2"], v["elbowY2"])),
                new XElement(A + "lnTo", Pt(v["elbowX3"], v["elbowY3"])),
                new XElement(A + "lnTo", Pt(v["elbowX4"], v["elbowY4"])));
            spPr.Add(new XElement(A + "custGeom", new XElement(A + "avLst"), new XElement(A + "gdLst"), new XElement(A + "ahLst"), new XElement(A + "cxnLst"),
                new XElement(A + "rect", new XAttribute("l", 0), new XAttribute("t", 0), new XAttribute("r", 0), new XAttribute("b", 0)),
                new XElement(A + "pathLst", path)));
        }
        else if (geometry.Line)
        {
            spPr.Add(new XElement(A + "custGeom", new XElement(A + "avLst"), new XElement(A + "gdLst"), new XElement(A + "ahLst"), new XElement(A + "cxnLst"),
                new XElement(A + "rect", new XAttribute("l", "l"), new XAttribute("t", "t"), new XAttribute("r", "r"), new XAttribute("b", "b")),
                new XElement(A + "pathLst", new XElement(A + "path", new XAttribute("w", 1), new XAttribute("h", 2), new XAttribute("fill", "none"),
                    new XElement(A + "moveTo", new XElement(A + "pt", new XAttribute("x", 0), new XAttribute("y", 1))),
                    new XElement(A + "lnTo", new XElement(A + "pt", new XAttribute("x", 1), new XAttribute("y", 1)))))));
        }
        else
            spPr.Add(new XElement(A + "prstGeom", new XAttribute("prst", geometry.Preset!), new XElement(A + "avLst", geometry.Adjust)));

        var hidden = HideGeometry(node);
        if (hidden)
            spPr.Add(new XElement(A + "noFill"), new XElement(A + "ln", new XElement(A + "noFill")));
        else if (geometry.Line)
        {
            var line = new XElement(A + "ln");
            if (geometry.HeadArrow)
                line.Add(new XElement(A + "headEnd", new XAttribute("type", "arrow")));
            if (geometry.TailArrow)
                line.Add(new XElement(A + "tailEnd", new XAttribute("type", "arrow")));
            spPr.Add(new XElement(A + "noFill"), line);
        }
        // The label's 3D properties, where the style definition sets any (PowerPoint writes them only then).
        if (!hidden && styling.StyleElement(label, "scene3d") is { } scene && !DefaultScene(scene))
            spPr.Add(new XElement(A + "scene3d", scene.Elements()));
        if (!hidden && styling.StyleElement(label, "sp3d") is { } sp3d && (sp3d.HasAttributes || sp3d.HasElements))
            spPr.Add(new XElement(A + "sp3d", sp3d.Attributes(), sp3d.Elements()));
        // Formatting of the shape saved with its presentation point overrides the style.
        if (node.Saved?.Element.Element(D + "spPr") is { } own)
        {
            static bool IsFill(XElement e) => e.Name.LocalName.EndsWith("Fill", StringComparison.Ordinal);
            foreach (var element in own.Elements().Where(e => e.Name.LocalName != "xfrm"))
            {
                foreach (var replaced in spPr.Elements().Where(e => e.Name == element.Name || IsFill(element) && IsFill(e)).ToList())
                    replaced.Remove();
                spPr.Add(new XElement(element));
            }
        }

        var style = Style(label, index, styling, geometry.Line);
        var shape = new XElement(P + "sp",
            new XAttribute("modelId", node.Saved?.Id ?? "{generated-" + number.ToString(CultureInfo.InvariantCulture) + "}"),
            new XElement(P + "nvSpPr", new XElement(P + "cNvPr", new XAttribute("id", 0), new XAttribute("name", "")), new XElement(P + "cNvSpPr"), new XElement(P + "nvPr")),
            spPr, style);

        if (textNode != null)
        {
            var primary = Fitting.Font(textNode);
            var body = TextBody.Build(textNode, primary, Fitting.SecondaryFont(textNode));
            if (listStyle != null)
                Styling.ApplyListStyle(body, textNode.Text, listStyle);
            // The label's text properties of the style definition go into the body (measured: the 3d9 quick style's
            // a:sp3d extrusion in PowerPoint's drawings).
            if (styling.StyleElement(label, "txPr") is { HasElements: true } txPr)
                body.Element(A + "bodyPr")?.Add(txPr.Elements().Select(e => new XElement(e)));
            shape.Add(body);
            shape.Add(TextTransform(geometry, textNode == node ? geometry : GeometryOf(textNode) ?? geometry, textNode));
        }
        return shape;
    }

    /// <summary>
    /// The text rectangle (p:txXfrm, EMU): the text node's geometry's text rectangle turned with its shape about its
    /// center, the text turned by its own rule, written relative to the turn of the shape that shows it.
    /// </summary>
    static XElement TextTransform(Geometry shape, Geometry text, LayoutNode textNode)
    {
        var (l, t, r, b) = TextRect(text);
        var (cx, cy) = (text.X + text.W / 2, text.Y + text.H / 2);
        // The rectangle's center turns with the text node's shape about that shape's center.
        var (dx, dy) = ((l + r) / 2 - text.W / 2, (t + b) / 2 - text.H / 2);
        var angle = text.Rotation * Math.PI / 180;
        var (ox, oy) = (cx + dx * Math.Cos(angle) - dy * Math.Sin(angle), cy + dx * Math.Sin(angle) + dy * Math.Cos(angle));
        var (width, height) = (r - l, b - t);
        var turn = TextTurn(textNode, text.Rotation);
        // Text turned by a quarter relative to its rectangle runs along the rectangle's other side.
        if (Math.Abs(Math.IEEERemainder(turn - text.Rotation, 180)) is > 45 and <= 135)
            (width, height) = (height, width);
        var relative = turn - shape.Rotation;
        return new XElement(P + "txXfrm",
            Math.Abs(relative) > 1e-9 ? new XAttribute("rot", Angle(relative)) : null,
            new XElement(A + "off", new XAttribute("x", Emu(ox - width / 2)), new XAttribute("y", Emu(oy - height / 2))),
            new XElement(A + "ext", new XAttribute("cx", Emu(Math.Max(0, width))), new XAttribute("cy", Emu(Math.Max(0, height)))));
    }

    static bool DefaultScene(XElement scene)
    {
        var camera = scene.Element(A + "camera");
        var light = scene.Element(A + "lightRig");
        return (camera == null || (string?)camera.Attribute("prst") == "orthographicFront" && !camera.HasElements)
               && (light == null || (string?)light.Attribute("rig") == "threePt" && (string?)light.Attribute("dir") is null or "t" && !light.HasElements);
    }

    /// <summary>The style references of a label with the colors of its color lists for the shape's index.</summary>
    static XElement Style(string label, (int Index, int Count) index, Styling styling, bool line)
    {
        var references = styling.Style(label);
        XElement Reference(string name, string list, string fallbackIndex)
        {
            var reference = references?.Element(A + name);
            var color = styling.Color(label, list, index.Index, index.Count)
                        ?? reference?.Elements().FirstOrDefault()
                        ?? new XElement(A + "scrgbClr", new XAttribute("r", 0), new XAttribute("g", 0), new XAttribute("b", 0));
            return new XElement(A + name, new XAttribute("idx", (string?)reference?.Attribute("idx") ?? fallbackIndex), new XElement(color));
        }
        var fontReference = references?.Element(A + "fontRef");
        var fontColor = styling.Color(label, "txFillClrLst", index.Index, index.Count) ?? fontReference?.Elements().FirstOrDefault();
        var font = new XElement(A + "fontRef", new XAttribute("idx", (string?)fontReference?.Attribute("idx") ?? "minor"), fontColor != null ? new XElement(fontColor) : null);
        // A connector line takes its color from the line list (the fill list colors block arrows).
        return new XElement(P + "style",
            Reference("lnRef", "linClrLst", line ? "1" : "0"),
            Reference("fillRef", "fillClrLst", "1"),
            Reference("effectRef", "effectClrLst", "0"),
            font);
    }
}
