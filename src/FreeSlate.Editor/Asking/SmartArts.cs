using System.Globalization;
using System.Security;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// SmartArt as the notation writes it (see Prompt): a fenced block ```smartart layout=cycle colors=blue holding its points as "- " lines,
/// two spaces deeper for a point's subpoints. As Word keeps one: a data model of the points (dgm:dataModel), a layout definition of
/// FreeSlate's own for the layout named (Asking/SmartArt/{layout}.xml, written for FreeSlate, none of Office's), colours and a style of
/// its own; without a drawing, so that Word lays it out from the definition as the document does (Pptx.SmartArt.Engine). Word edits it
/// as a SmartArt of its own. In a line of its own or floating beside the paragraph after it, as wide as the text unless its width says
/// so, as high as its points need. Read back as the block it was written as (see Block).
/// </summary>
static class SmartArts
{
    const double EmuPerPoint = 12700;
    const string Urn = "urn:freeslate.app/smartart/";
    static readonly XNamespace Dgm = "http://schemas.openxmlformats.org/drawingml/2006/diagram";
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>The layouts the notation names, each a definition of FreeSlate's own.</summary>
    public static readonly string[] Layouts = ["list", "process", "chevrons", "cycle", "radial", "venn", "pyramid", "blocks"];

    /// <summary>The colours of one hue: its shapes, a radial's middle (darker), arrows and lines (light).</summary>
    static readonly Dictionary<string, (string Shape, string Middle, string Light)> Hues = new(StringComparer.OrdinalIgnoreCase)
    {
        ["blue"] = ("2F6DB5", "1F4E8C", "BCCBE0"),
        ["green"] = ("4C9A5F", "336B41", "C3DCC9"),
        ["orange"] = ("E07B39", "A9541F", "F2CFB5"),
        ["violet"] = ("8C5BB8", "62408A", "D7C8E6"),
        ["rose"] = ("C8484B", "923236", "EDC6C7"),
        ["teal"] = ("2A9DA8", "1C6F77", "BEE0E3"),
        ["gray"] = ("6B7280", "3F444D", "D3D6DB"),
    };

    /// <summary>colors=colorful: the shapes in these in turn (the charts' colours, see Charts), the rest as in gray.</summary>
    static readonly string[] Colorful = ["2F6DB5", "E07B39", "4C9A5F", "8C5BB8", "C8484B", "2A9DA8"];

    /// <summary>The text on light ground (see-through circles, text under a list's bars).</summary>
    const string Ink = "1F2933";

    /// <summary>The font of the diagrams' text, as the charts have it.</summary>
    const string Font = "Inter";

    /// <summary>A point and its subpoints, its text in the notation's pieces.</summary>
    sealed record Item(IReadOnlyList<Notation.Piece> Pieces, List<Item> Children)
    {
        public string Plain => string.Concat(Pieces.OfType<Notation.Run>().Select(r => r.Text));
    }

    /// <summary>A SmartArt made at a place (see Markup.Made): its drawing's run in a line of its own (or floating) and its four parts.</summary>
    public static Markup.Made Of(Notation.Fenced block, Markup.Place place)
    {
        var settings = block.Settings;
        var layout = (settings.GetValueOrDefault("layout") ?? settings.GetValueOrDefault("type") ?? "list").Trim().ToLowerInvariant() switch
        {
            "steps" or "flow" => "process",
            "circle" or "loop" => "cycle",
            "hub" or "center" or "centre" => "radial",
            "overlap" or "overlapping" => "venn",
            "grid" or "tiles" => "blocks",
            var named => named,
        };
        if (!Layouts.Contains(layout))
            return new("", [], [], Problem: $"layout={layout} is none of FreeSlate's SmartArt layouts ({string.Join(", ", Layouts)}).");
        var colors = (settings.GetValueOrDefault("colors") ?? settings.GetValueOrDefault("color") ?? "blue").Trim().ToLowerInvariant();
        if (colors != "colorful" && !Hues.ContainsKey(colors))
            return new("", [], [], Problem: $"colors={colors} is none of {string.Join(", ", Hues.Keys)} or colorful.");
        var items = Items(block.Body);
        if (items.Count == 0)
            return new("", [], [], Problem: "A smartart block holds its points as lines \"- point\", two spaces deeper for subpoints (\"  - subpoint\").");
        if (layout == "radial")
        {
            // The middle is the first point, what stands round it its subpoints: points after the first go round it too.
            var middle = items[0];
            middle.Children.AddRange(items.Skip(1));
            items = [middle];
            if (middle.Children.Count == 0)
                return new("", [], [], Problem: "A radial smartart holds the middle as its first point and what stands round it as its subpoints (or as the points after it).");
        }
        if (layout == "venn" && items.Count > 4)
            return new("", [], [], Problem: "A venn smartart holds one to four points, a circle for each.");
        var width = Math.Clamp(Markup.Length(settings.GetValueOrDefault("width"), place.TextWidth) ?? place.TextWidth, 72, place.TextWidth);
        var height = Markup.Length(settings.GetValueOrDefault("height"), place.TextWidth) ?? Height(layout, items, width);
        var (cx, cy) = ((long)Math.Round(width * EmuPerPoint), (long)Math.Round(height * EmuPerPoint));
        var alt = settings.GetValueOrDefault("alt") ?? settings.GetValueOrDefault("description") ?? string.Join(", ", items.Select(i => i.Plain));
        var run = $"<w:r><w:rPr><w:noProof/></w:rPr><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/>"
            + $"<wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/><wp:docPr id=\"1\" name=\"SmartArt\" descr=\"{Escape(alt)}\"/><wp:cNvGraphicFramePr/>"
            + "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/diagram\">"
            + "<dgm:relIds xmlns:dgm=\"http://schemas.openxmlformats.org/drawingml/2006/diagram\" r:dm=\"{data}\" r:lo=\"{layout}\" r:qs=\"{style}\" r:cs=\"{colors}\"/>"
            + "</a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
        const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
        const string Types = "application/vnd.openxmlformats-officedocument.drawingml.";
        NewPart[] parts =
        [
            new("data", "/word/diagrams/data1.xml", Types + "diagramData+xml", Relationships + "diagramData", Bytes(DataModel(layout, colors, items))),
            new("layout", "/word/diagrams/layout1.xml", Types + "diagramLayout+xml", Relationships + "diagramLayout", Bytes(Definition(layout))),
            new("style", "/word/diagrams/quickStyle1.xml", Types + "diagramStyle+xml", Relationships + "diagramQuickStyle", Bytes(Style())),
            new("colors", "/word/diagrams/colors1.xml", Types + "diagramColors+xml", Relationships + "diagramColors", Bytes(Colors(colors))),
        ];
        return Markup.Placed(run, parts, settings, place);
    }

    static string Escape(string text) => SecurityElement.Escape(text) ?? "";

    static byte[] Bytes(XElement root) =>
        Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + root.ToString(SaveOptions.DisableFormatting));

    /// <summary>The points of a block's body: "- ", "* " or "+ " lines (a line without a mark is a point too), nested by their indent.</summary>
    static List<Item> Items(string body)
    {
        var top = new List<Item>();
        var open = new List<(int Indent, Item Item)>();
        foreach (var raw in body.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            if (raw.Trim().Length == 0)
                continue;
            var indent = raw.Length - raw.TrimStart(' ', '\t').Length + raw.TakeWhile(c => c is ' ' or '\t').Count(c => c == '\t') * 3;
            var text = raw.Trim();
            if (text.Length > 1 && text[0] is ('-' or '*' or '+') && text[1] == ' ')
                text = text[2..].Trim();
            if (text.Length == 0)
                continue;
            var item = new Item(Notation.Inline(text, null), []);
            while (open.Count > 0 && open[^1].Indent >= indent)
                open.RemoveAt(open.Count - 1);
            if (open.Count == 0)
                top.Add(item);
            else
                open[^1].Item.Children.Add(item);
            open.Add((indent, item));
        }
        return top;
    }

    /// <summary>
    /// The diagram's height for its width when the block gives none: as the layout lays its points out (the boxes of a row of steps
    /// at their proportions, a ring's circles, a list's bars and lines), so that the text keeps its size and nothing stands far
    /// from it.
    /// </summary>
    static double Height(string layout, List<Item> items, double width)
    {
        var n = Math.Max(1, items.Count);
        var deeper = items.Any(i => i.Children.Count > 0);
        double Clamp(double h) => Math.Clamp(h, 54, Math.Max(54, width * 0.75));
        switch (layout)
        {
            case "process":
            {
                var box = width / (n + (n - 1) * 0.26);
                return Clamp(box * 0.66 * (deeper ? 1.6 : 1) * 1.06);
            }
            case "chevrons":
                return Clamp(width / (n - 0.08 * (n - 1)) * 0.42 * (deeper ? 1.5 : 1) * 1.08);
            case "cycle":
                return Clamp(width * (n <= 2 ? 0.42 : n == 3 ? 0.52 : 0.6));
            case "radial":
                return Clamp(width * 0.62);
            case "venn":
                return Clamp(width * (n == 1 ? 0.5 : n == 2 ? 0.5 : 0.6));
            case "pyramid":
                return Clamp(Math.Clamp(n * 40.0, 140, 260));
            case "blocks":
            {
                var columns = n <= 4 ? n : n <= 6 || n == 9 ? 3 : 4;
                var rows = (n + columns - 1) / columns;
                var box = width / (columns + (columns - 1) * 0.08);
                return Clamp((rows * box * 0.62 + (rows - 1) * box * 0.08) * (deeper ? 1.15 : 1));
            }
            default:
            {
                // A bar of 15-point text for each point, two lines of a little smaller text under it for each with subpoints.
                var height = 0.0;
                foreach (var item in items)
                    height += 26 + (item.Children.Count > 0 ? 6 + item.Children.Sum(c => Lines(c, width)) * 16 : 5);
                return Clamp(height + 6);
            }
        }
    }

    /// <summary>The lines a subpoint and its own take in a list as wide as given (about 13-point text).</summary>
    static int Lines(Item item, double width) =>
        Math.Max(1, (int)Math.Ceiling(item.Plain.Length * 6.4 / Math.Max(40, width - 40))) + item.Children.Sum(c => Lines(c, width));

    /// <summary>A layout definition as FreeSlate has it (Asking/SmartArt/{layout}.xml), its comments left out.</summary>
    static XElement Definition(string layout)
    {
        using var stream = typeof(SmartArts).Assembly.GetManifestResourceStream($"FreeSlate.Editor.SmartArt.{layout}.xml")
            ?? throw new InvalidOperationException($"no layout definition {layout}");
        var root = XElement.Load(stream);
        root.DescendantNodes().OfType<XComment>().ToList().ForEach(c => c.Remove());
        return root;
    }

    /// <summary>
    /// The data model: the document's point naming the layout, style and colours, a point for each point of the block with its text,
    /// and the connections that make them a tree in order, each with the transitions Word keeps beside it.
    /// </summary>
    static XElement DataModel(string layout, string colors, List<Item> items)
    {
        var number = 0;
        string Id() => string.Create(CultureInfo.InvariantCulture, $"{{5F3A{++number:X4}-0000-4000-8000-{number:X12}}}");
        var language = CultureInfo.CurrentCulture.Name is { Length: > 0 } name ? name : "en-US";
        var points = new XElement(Dgm + "ptLst");
        var connections = new XElement(Dgm + "cxnLst");
        XElement Empty() => new(Dgm + "t", new XElement(A + "bodyPr"), new XElement(A + "lstStyle"), new XElement(A + "p", new XElement(A + "endParaRPr", new XAttribute("lang", language))));
        var document = Id();
        points.Add(new XElement(Dgm + "pt", new XAttribute("modelId", document), new XAttribute("type", "doc"),
            new XElement(Dgm + "prSet", new XAttribute("loTypeId", Urn + "layout/" + layout), new XAttribute("loCatId", Category(layout)),
                new XAttribute("qsTypeId", Urn + "style/flat"), new XAttribute("qsCatId", "simple"),
                new XAttribute("csTypeId", Urn + "colors/" + colors), new XAttribute("csCatId", colors == "colorful" ? "colorful" : "accent1"), new XAttribute("phldr", "0")),
            new XElement(Dgm + "spPr"), Empty()));
        void Add(string parent, Item item, int order)
        {
            var (node, before, after, connection) = (Id(), Id(), Id(), Id());
            points.Add(new XElement(Dgm + "pt", new XAttribute("modelId", node), new XElement(Dgm + "prSet", new XAttribute("phldrT", "[Text]")), new XElement(Dgm + "spPr"),
                new XElement(Dgm + "t", new XElement(A + "bodyPr"), new XElement(A + "lstStyle"), Paragraph(item.Pieces, language))));
            foreach (var (type, id) in new[] { ("parTrans", before), ("sibTrans", after) })
                points.Add(new XElement(Dgm + "pt", new XAttribute("modelId", id), new XAttribute("type", type), new XAttribute("cxnId", connection),
                    new XElement(Dgm + "prSet"), new XElement(Dgm + "spPr"), Empty()));
            connections.Add(new XElement(Dgm + "cxn", new XAttribute("modelId", connection), new XAttribute("srcId", parent), new XAttribute("destId", node),
                new XAttribute("srcOrd", order), new XAttribute("destOrd", 0), new XAttribute("parTransId", before), new XAttribute("sibTransId", after)));
            for (var k = 0; k < item.Children.Count; k++)
                Add(node, item.Children[k], k);
        }
        for (var k = 0; k < items.Count; k++)
            Add(document, items[k], k);
        return new XElement(Dgm + "dataModel", new XAttribute(XNamespace.Xmlns + "dgm", Dgm.NamespaceName), new XAttribute(XNamespace.Xmlns + "a", A.NamespaceName),
            points, connections, new XElement(Dgm + "bg"), new XElement(Dgm + "whole"));
    }

    static string Category(string layout) => layout switch
    {
        "process" or "chevrons" => "process",
        "cycle" => "cycle",
        "radial" or "venn" => "relationship",
        "pyramid" => "pyramid",
        _ => "list",
    };

    /// <summary>A point's text as a paragraph: its meanings as Word's emphasis (important bold, the others italic but open questions).</summary>
    static XElement Paragraph(IReadOnlyList<Notation.Piece> pieces, string language)
    {
        var paragraph = new XElement(A + "p");
        foreach (var piece in pieces)
        {
            if (piece is not Notation.Run { Text.Length: > 0 } run)
                continue;
            var rPr = new XElement(A + "rPr", new XAttribute("lang", language));
            if (run.Style == "Important")
                rPr.Add(new XAttribute("b", 1));
            else if (run.Style is "Emphasized" or "Term" or "Quotation")
                rPr.Add(new XAttribute("i", 1));
            rPr.Add(new XElement(A + "latin", new XAttribute("typeface", Font)), new XElement(A + "cs", new XAttribute("typeface", Font)));
            paragraph.Add(new XElement(A + "r", rPr, new XElement(A + "t", run.Text)));
        }
        if (!paragraph.HasElements)
            paragraph.Add(new XElement(A + "endParaRPr", new XAttribute("lang", language)));
        return paragraph;
    }

    /// <summary>
    /// The style: flat, no effects; the shapes filled, with a thin line in the colours' line colour (white, so that levels and
    /// overlaps part), lines between shapes a little stronger; text in the theme's body font where a point gives none.
    /// </summary>
    static XElement Style()
    {
        XElement Label(string name, int line, int fill) => new(Dgm + "styleLbl", new XAttribute("name", name),
            new XElement(Dgm + "scene3d", new XElement(A + "camera", new XAttribute("prst", "orthographicFront")), new XElement(A + "lightRig", new XAttribute("rig", "threePt"), new XAttribute("dir", "t"))),
            new XElement(Dgm + "sp3d"), new XElement(Dgm + "txPr"),
            new XElement(Dgm + "style",
                new XElement(A + "lnRef", new XAttribute("idx", line), Black()), new XElement(A + "fillRef", new XAttribute("idx", fill), Black()),
                new XElement(A + "effectRef", new XAttribute("idx", 0), Black()), new XElement(A + "fontRef", new XAttribute("idx", "minor"), new XElement(A + "schemeClr", new XAttribute("val", "lt1")))));
        static XElement Black() => new(A + "scrgbClr", new XAttribute("r", 0), new XAttribute("g", 0), new XAttribute("b", 0));
        return new XElement(Dgm + "styleDef", new XAttribute(XNamespace.Xmlns + "dgm", Dgm.NamespaceName), new XAttribute(XNamespace.Xmlns + "a", A.NamespaceName),
            new XAttribute("uniqueId", Urn + "style/flat"),
            new XElement(Dgm + "title", new XAttribute("val", "")), new XElement(Dgm + "desc", new XAttribute("val", "")),
            new XElement(Dgm + "catLst", new XElement(Dgm + "cat", new XAttribute("type", "simple"), new XAttribute("pri", 500))),
            new XElement(Dgm + "scene3d", new XElement(A + "camera", new XAttribute("prst", "orthographicFront")), new XElement(A + "lightRig", new XAttribute("rig", "threePt"), new XAttribute("dir", "t"))),
            Label("node0", 1, 1), Label("node1", 1, 1), Label("vennNode1", 2, 1), Label("sibTrans2D1", 0, 1), Label("sibTrans1D1", 1, 0),
            Label("parChTrans1D2", 2, 0), Label("revTx", 0, 0));
    }

    /// <summary>The colours of a hue (or colorful): fill, line and text of each style label the layouts use.</summary>
    static XElement Colors(string colors)
    {
        var (shape, middle, light) = Hues.GetValueOrDefault(colors == "colorful" ? "gray" : colors);
        var shapes = colors == "colorful" ? Colorful : [shape];
        if (colors == "colorful")
            middle = "3F444D";
        static XElement Color(string hex, int? alpha = null) => new(A + "srgbClr", new XAttribute("val", hex), alpha is { } a ? new XElement(A + "alpha", new XAttribute("val", a)) : null);
        static XElement List(string name, IEnumerable<XElement> colors) => new(Dgm + name, new XAttribute("meth", "repeat"), colors);
        XElement Label(string name, IEnumerable<XElement> fill, IEnumerable<XElement> line, XElement text) => new(Dgm + "styleLbl", new XAttribute("name", name),
            List("fillClrLst", fill), List("linClrLst", line), new XElement(Dgm + "effectClrLst"), new XElement(Dgm + "txLinClrLst"),
            List("txFillClrLst", [text]), new XElement(Dgm + "txEffectClrLst"));
        XElement[] White() => [Color("FFFFFF")];
        XElement[] Clear() => [Color("FFFFFF", 0)];
        return new XElement(Dgm + "colorsDef", new XAttribute(XNamespace.Xmlns + "dgm", Dgm.NamespaceName), new XAttribute(XNamespace.Xmlns + "a", A.NamespaceName),
            new XAttribute("uniqueId", Urn + "colors/" + colors),
            new XElement(Dgm + "title", new XAttribute("val", "")), new XElement(Dgm + "desc", new XAttribute("val", "")),
            new XElement(Dgm + "catLst", new XElement(Dgm + "cat", new XAttribute("type", colors == "colorful" ? "colorful" : "accent1"), new XAttribute("pri", 500))),
            Label("node0", [Color(middle)], White(), Color("FFFFFF")),
            Label("node1", shapes.Select(s => Color(s)), White(), Color("FFFFFF")),
            Label("vennNode1", shapes.Select(s => Color(s, 55000)), White(), Color(Ink)),
            Label("sibTrans2D1", [Color(light)], [Color(light)], Color(Ink)),
            Label("sibTrans1D1", [Color(light)], [Color(light)], Color(Ink)),
            Label("parChTrans1D2", [Color(light)], [Color(light)], Color(Ink)),
            Label("revTx", Clear(), Clear(), Color(Ink)));
    }

    /// <summary>
    /// A SmartArt read back as the notation writes it (see Of): its layout (FreeSlate's own by name; another by its kind, the nearest of
    /// FreeSlate's), its colours where they are FreeSlate's, its size, and its points from the data model in order; null where the
    /// data model does not read.
    /// </summary>
    public static string? Block(byte[] data, byte[]? layout, DrawingFormatting? drawing)
    {
        XElement model;
        XElement? definition = null;
        try
        {
            model = XElement.Parse(Encoding.UTF8.GetString(data).TrimStart('﻿'));
            if (layout != null)
                definition = XElement.Parse(Encoding.UTF8.GetString(layout).TrimStart('﻿'));
        }
        catch (XmlException)
        {
            return null;
        }
        XNamespace d = model.Name.Namespace;
        var points = model.Element(d + "ptLst")?.Elements(d + "pt").ToList() ?? [];
        var document = points.FirstOrDefault(p => (string?)p.Attribute("type") == "doc");
        if (document == null)
            return null;
        var prSet = document.Element(d + "prSet");
        var layoutId = (string?)prSet?.Attribute("loTypeId") ?? (string?)definition?.Attribute("uniqueId") ?? "";
        var name = layoutId.StartsWith(Urn + "layout/", StringComparison.Ordinal) && Layouts.Contains(layoutId[(Urn.Length + 7)..]) ? layoutId[(Urn.Length + 7)..] : Nearest(layoutId, definition);
        var settings = new List<string> { "layout=" + name };
        var colorsId = (string?)prSet?.Attribute("csTypeId") ?? "";
        if (colorsId.StartsWith(Urn + "colors/", StringComparison.Ordinal) && colorsId[(Urn.Length + 7)..] is var colors && colors != "blue")
            settings.Add("colors=" + colors);
        if (drawing?.Width is { } width)
            settings.Add("width=" + (width / 72 * 2.54).ToString("0.#", CultureInfo.InvariantCulture) + "cm");
        if (drawing?.Height is { } height)
            settings.Add("height=" + (height / 72 * 2.54).ToString("0.#", CultureInfo.InvariantCulture) + "cm");
        var byId = points.Where(p => (string?)p.Attribute("modelId") != null).GroupBy(p => (string)p.Attribute("modelId")!).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var children = (model.Element(d + "cxnLst")?.Elements(d + "cxn") ?? [])
            .Where(c => (string?)c.Attribute("type") is null or "parOf")
            .GroupBy(c => (string?)c.Attribute("srcId") ?? "", StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => (int?)c.Attribute("srcOrd") ?? 0).Select(c => (string?)c.Attribute("destId") ?? "").ToList(), StringComparer.Ordinal);
        var lines = new StringBuilder();
        void Write(string id, int depth)
        {
            if (depth > 6 || !byId.TryGetValue(id, out var point) || (string?)point.Attribute("type") is "parTrans" or "sibTrans" or "pres")
                return;
            var text = new StringBuilder();
            foreach (var run in point.Element(d + "t")?.Descendants(A + "r") ?? [])
            {
                var value = Marked(string.Concat(run.Elements(A + "t").Select(t => t.Value)));
                var rPr = run.Element(A + "rPr");
                text.Append((string?)rPr?.Attribute("b") is "1" or "true" ? $"**{value}**" : (string?)rPr?.Attribute("i") is "1" or "true" ? $"*{value}*" : value);
            }
            lines.Append(new string(' ', depth * 2)).Append("- ").Append(text.ToString().Trim()).Append('\n');
            foreach (var child in children.GetValueOrDefault(id) ?? [])
                Write(child, depth + 1);
        }
        foreach (var top in children.GetValueOrDefault((string)document.Attribute("modelId")!) ?? [])
            Write(top, 0);
        return "```smartart " + string.Join(" ", settings) + "\n" + lines + "```";
    }

    /// <summary>Text as the notation reads it: its marks' characters escaped (see Reading).</summary>
    static string Marked(string text)
    {
        var marked = new StringBuilder(text.Length);
        foreach (var c in text)
            marked.Append(c is '*' or '_' or '{' or '}' or '[' or ']' or '|' or '`' or '\\' or '<' or '$' ? "\\" + c : c.ToString());
        return marked.ToString();
    }

    /// <summary>The nearest of FreeSlate's layouts to another (Word's own): by its name, else by the kind its definition gives.</summary>
    static string Nearest(string id, XElement? definition)
    {
        var last = id[(id.LastIndexOf('/') + 1)..].ToLowerInvariant();
        foreach (var (part, layout) in new[] { ("chevron", "chevrons"), ("cycle", "cycle"), ("radial", "radial"), ("venn", "venn"), ("pyramid", "pyramid"),
                     ("process", "process"), ("arrow", "process"), ("default", "blocks"), ("matrix", "blocks"), ("list", "list") })
        {
            if (last.Contains(part, StringComparison.Ordinal))
                return layout;
        }
        var category = (string?)definition?.Descendants().FirstOrDefault(e => e.Name.LocalName == "cat")?.Attribute("type");
        return category switch
        {
            "process" => "process",
            "cycle" => "cycle",
            "relationship" => "venn",
            "pyramid" => "pyramid",
            "matrix" => "blocks",
            _ => "list",
        };
    }
}
