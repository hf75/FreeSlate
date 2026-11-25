using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// Grouped shapes and linked text boxes as the notation writes them and Word keeps them. A ```group block holds ```shape and ```box blocks,
/// each at its place in the group (x and y from the group's top left, its width and height), made Word's group (wpg:wgp, Word's Group):
/// its shapes in the group's own space (a:chOff, a:chExt), each with a name and id of its own (wps:cNvPr) as Word gives the shapes of a
/// group, the group as large as its shapes reach, scaled to its width or height where those are given and to the text's width at most. A
/// box with link=next has its text go on in the next box written (in the group, or in the write), as Word's Create Link does: the first
/// box holds the story (wps:txbx id), each box after it names the story and its place in the chain (wps:linkedTxbx id, seq), and all of
/// them keep the height they are given (the text flowing on instead of the box growing). Read back as the blocks they are written as, a
/// group with its shapes inside a fence of four backticks.
/// </summary>
static partial class Groups
{
    const double EmuPerPoint = 12700;

    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    static readonly XNamespace Wps = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";
    static readonly XNamespace Wpg = "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup";

    /// <summary>Whether a box's settings send its text on to the next box (link=next).</summary>
    public static bool LinksOn(IReadOnlyDictionary<string, string> settings) =>
        settings.GetValueOrDefault("link")?.Trim().ToLowerInvariant() is "next" or "on" or "yes" or "true";

    /// <summary>The height a linked box keeps: its own (height=), else five centimetres.</summary>
    static double LinkedHeight(IReadOnlyDictionary<string, string> settings, double textWidth) =>
        Markup.Length(settings.GetValueOrDefault("height"), textWidth) ?? 5 / 2.54 * 72;

    /// <summary>
    /// A box of a chain of linked boxes made at a place (see the class): <paramref name="seq"/> 0 the first, which holds the story's text,
    /// the others its continuation without text of their own; the box keeps its height. Returns what Markup made of it with its
    /// shape so linked, or the problem.
    /// </summary>
    public static Markup.Made Linked(Notation.Fenced box, Markup.Place place, int story, int seq, List<string> problems)
    {
        var continues = seq > 0;
        if (continues && box.Body.Trim().Length > 0)
            problems.Add("A box that goes on with the text of the box before it holds no text of its own; its text was left out.");
        // A box going on with a story is made as a box of a paragraph of its own, then given the story in place of its text.
        var made = Markup.Of(continues ? box with { Body = "." } : box, place);
        if (made.Problem != null || made.Blocks.Length == 0)
            return made;
        var root = Markup.Parsed(made.Blocks);
        if (root.Descendants(Wps + "wsp").FirstOrDefault() is not { } shape)
            return made;
        Link(shape, story, seq, LinkedHeight(box.Settings, place.TextWidth));
        if (root.Descendants(Wp + "inline").FirstOrDefault()?.Element(Wp + "extent") is { } extent)
            extent.SetAttributeValue("cy", (long)Math.Round(LinkedHeight(box.Settings, place.TextWidth) * EmuPerPoint));
        return made with { Blocks = Serialized(root), Fit = false };
    }

    /// <summary>A shape (wps:wsp) made a box of a chain: its story named or gone on with, as high as given, fitted no more to its text.</summary>
    static void Link(XElement shape, int story, int seq, double height)
    {
        if (shape.Element(Wps + "spPr")?.Element(A + "xfrm")?.Element(A + "ext") is { } ext)
            ext.SetAttributeValue("cy", (long)Math.Round(height * EmuPerPoint));
        if (shape.Element(Wps + "bodyPr") is { } body)
        {
            body.Elements(A + "spAutoFit").Remove();
            if (body.Element(A + "noAutofit") == null)
                body.Add(new XElement(A + "noAutofit"));
        }
        if (shape.Element(Wps + "txbx") is not { } text)
            return;
        if (seq == 0)
            text.SetAttributeValue("id", story);
        else
            text.ReplaceWith(new XElement(Wps + "linkedTxbx", new XAttribute("id", story), new XAttribute("seq", seq)));
    }

    /// <summary>
    /// Elements of a markup read by Markup.Parsed written back as markup, without the declarations of the usual prefixes the writing
    /// gives them (the document takes those without, see WordDocument.InsertMarkup).
    /// </summary>
    static string Serialized(XElement root) =>
        Declarations().Replace(string.Concat(root.Elements().Select(e => e.ToString(SaveOptions.DisableFormatting))), "");

    [System.Text.RegularExpressions.GeneratedRegex(@" xmlns:(?:w|r|m|wp|a|pic|c|wps|wpg|wpc|wp14|w14|w15|a14|mc|v|o)=""[^""]*""")]
    private static partial System.Text.RegularExpressions.Regex Declarations();

    /// <summary>
    /// A group (see the class) at a place: its shapes and boxes, each at x and y of its own, made one drawing in a line of its own or
    /// floating beside the paragraph after it (see Markup.Placed).
    /// </summary>
    public static Markup.Made Of(Notation.Fenced group, Markup.Place place)
    {
        var inner = Notation.Parse(group.Body, final: true);
        var shapes = new List<(XElement Shape, double X, double Y, double Width, double Height)>();
        var problems = new List<string>();
        int? story = null;
        var seq = 0;
        var id = Random.Shared.Next(10_000, 900_000);
        foreach (var block in inner.Blocks)
        {
            if (block is not Notation.Fenced { Kind: "shape" or "box" } child)
                return new("", [], [], Problem: "A group holds ```shape and ```box blocks, each with its place in the group (x=, y=, width=, height=).");
            var settings = new Dictionary<string, string>(child.Settings, StringComparer.OrdinalIgnoreCase);
            var (x, y) = (Markup.Length(settings.GetValueOrDefault("x"), place.TextWidth) ?? 0, Markup.Length(settings.GetValueOrDefault("y"), place.TextWidth) ?? 0);
            foreach (var key in new[] { "x", "y", "align", "caption", "alt" })
                settings.Remove(key);
            // The shape's own run, floating, so that Markup gives it alone (see Markup.Placed).
            settings["float"] = "left";
            var links = child.Kind == "box" && LinksOn(settings);
            var continues = story != null && child.Kind == "box";
            var made = continues ? Linked(child with { Settings = settings }, place, story!.Value, seq, problems)
                : Markup.Of(child with { Settings = settings }, place);
            if (made.Problem != null)
                return new("", [], [], Problem: made.Problem);
            if (Markup.Parsed(made.Blocks).Descendants(Wps + "wsp").FirstOrDefault() is not { } shape)
                continue;
            var given = Markup.Length(settings.GetValueOrDefault("height"), place.TextWidth);
            if (!continues && links)
            {
                story = place.NewStory?.Invoke() ?? Random.Shared.Next(1_000, 90_000);
                Link(shape, story.Value, 0, given ?? LinkedHeight(settings, place.TextWidth));
                seq = 1;
            }
            else if (continues)
            {
                (story, seq) = links ? (story, seq + 1) : (null, 0);
            }
            else if (given is { } height)
                SetHeight(shape, height);
            // A box or a shape growing with its text keeps its height in a group (Word fits the group's shapes no more).
            if (shape.Element(Wps + "bodyPr") is { } body && body.Element(A + "spAutoFit") != null)
            {
                body.Elements(A + "spAutoFit").Remove();
                body.Add(new XElement(A + "noAutofit"));
            }
            var ext = shape.Element(Wps + "spPr")?.Element(A + "xfrm")?.Element(A + "ext");
            var (width, height2) = (((long?)ext?.Attribute("cx") ?? 0) / EmuPerPoint, ((long?)ext?.Attribute("cy") ?? 0) / EmuPerPoint);
            if (shape.Element(Wps + "spPr")?.Element(A + "xfrm")?.Element(A + "off") is { } off)
            {
                off.SetAttributeValue("x", (long)Math.Round(x * EmuPerPoint));
                off.SetAttributeValue("y", (long)Math.Round(y * EmuPerPoint));
            }
            // The shapes of a group have a name and id of their own, as Word gives them (wps:cNvPr before the shape's properties).
            shape.AddFirst(new XElement(Wps + "cNvPr", new XAttribute("id", id++), new XAttribute("name", (child.Kind == "box" ? "Text Box " : "Shape ") + (shapes.Count + 1).ToString(CultureInfo.InvariantCulture))));
            shapes.Add((shape, x, y, width, height2));
        }
        if (shapes.Count == 0)
            return new("", [], [], Problem: "A group holds its shapes: ```shape and ```box blocks inside ````group.");
        // The group's own space: as far as its shapes reach; its extent that, or its width or height given (scaled alike), no wider than the text.
        var (reachX, reachY) = (shapes.Max(s => s.X + s.Width), shapes.Max(s => s.Y + s.Height));
        reachX = Math.Max(reachX, 1);
        reachY = Math.Max(reachY, 1);
        var wanted = Markup.Length(group.Settings.GetValueOrDefault("width"), place.TextWidth);
        var high = Markup.Length(group.Settings.GetValueOrDefault("height"), place.TextWidth);
        var (sx, sy) = (wanted is { } gw ? gw / reachX : high is { } gh0 ? gh0 / reachY : 1, high is { } gh ? gh / reachY : wanted is { } gw2 ? gw2 / reachX : 1);
        if (reachX * sx > place.TextWidth)
            (sx, sy) = (place.TextWidth / reachX, sy * place.TextWidth / (reachX * sx));
        var (cx, cy) = ((long)Math.Round(reachX * sx * EmuPerPoint), (long)Math.Round(reachY * sy * EmuPerPoint));
        var (chx, chy) = ((long)Math.Round(reachX * EmuPerPoint), (long)Math.Round(reachY * EmuPerPoint));
        var wgp = new XElement(Wpg + "wgp", new XElement(Wpg + "cNvGrpSpPr"),
            new XElement(Wpg + "grpSpPr", new XElement(A + "xfrm", new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(A + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy)),
                new XElement(A + "chOff", new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(A + "chExt", new XAttribute("cx", chx), new XAttribute("cy", chy)))),
            shapes.Select(s => s.Shape));
        // Written within the usual prefixes' declarations, which it then goes without (see Serialized).
        var holder = Markup.Parsed("");
        holder.Add(wgp);
        var alt = System.Security.SecurityElement.Escape(group.Settings.GetValueOrDefault("alt") ?? group.Settings.GetValueOrDefault("description") ?? "");
        var run = $"<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>"
            + $"<wp:docPr id=\"1\" name=\"Group\" descr=\"{alt}\"/><wp:cNvGraphicFramePr/>"
            + "<a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingGroup\">"
            + Serialized(holder) + "</a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
        return Markup.Placed(run, [], group.Settings, place);
    }

    static void SetHeight(XElement shape, double height)
    {
        if (shape.Element(Wps + "spPr")?.Element(A + "xfrm")?.Element(A + "ext") is { } ext)
            ext.SetAttributeValue("cy", (long)Math.Round(height * EmuPerPoint));
    }

    /// <summary>
    /// What a box's settings say of its chain, as the notation reads them back (see Objects.Box): its height, which it keeps, and link=next
    /// where a box after it goes on with its text; nothing for a box alone.
    /// </summary>
    public static IEnumerable<string> BoxSettings(WordDocument word, int paragraph, int offset)
    {
        if (word.TextBoxLink(paragraph, offset) is not var (_, _, more))
            yield break;
        if (word.DrawingOf(paragraph, offset)?.Height is { } height)
            yield return "height=" + Centimetres(height);
        if (more)
            yield return "link=next";
    }

    static string Centimetres(double points) => (points / 72 * 2.54).ToString("0.#", CultureInfo.InvariantCulture) + "cm";

    /// <summary>
    /// A group as the notation writes it (see Of), read back from its drawing's markup: its size and place, then each shape or box at its
    /// place in the group (in points of the page: its own space scaled onto the group's extent), with its text.
    /// </summary>
    public static string Block(WordDocument word, int paragraph, int offset, XElement element, Objects objects)
    {
        var settings = new List<string>();
        if (word.DrawingOf(paragraph, offset) is { } drawing)
        {
            if (drawing.Width is { } across)
                settings.Add("width=" + Centimetres(across));
            if (drawing.Height is { } down)
                settings.Add("height=" + Centimetres(down));
            if (drawing.Wrap is "square" or "tight" or "through" && drawing.HorizontalAlign is "left" or "right")
                settings.Add("float=" + drawing.HorizontalAlign);
        }
        if (element.Descendants().FirstOrDefault(e => e.Name.LocalName == "wgp") is not { } group)
            return "````group " + string.Join(" ", settings) + "\n````";
        var xfrm = group.Elements().FirstOrDefault(e => e.Name.LocalName == "grpSpPr")?.Elements().FirstOrDefault(e => e.Name.LocalName == "xfrm");
        double Value(string child, string name, double fallback) =>
            double.TryParse((string?)xfrm?.Elements().FirstOrDefault(e => e.Name.LocalName == child)?.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        var (cx, cy) = (Value("ext", "cx", 1), Value("ext", "cy", 1));
        var (ox, oy) = (Value("chOff", "x", 0), Value("chOff", "y", 0));
        var (sx, sy) = (cx / Math.Max(1, Value("chExt", "cx", cx)), cy / Math.Max(1, Value("chExt", "cy", cy)));
        var texts = word.GroupTexts(paragraph, offset);
        var shapes = group.Descendants().Where(e => e.Name.LocalName == "wsp").ToList();
        var linkedSeqs = shapes.Select(s => s.Elements().FirstOrDefault(e => e.Name.LocalName == "linkedTxbx"))
            .Where(l => l != null).Select(l => ((string?)l!.Attribute("id"), int.TryParse((string?)l.Attribute("seq"), out var q) ? q : 0)).ToList();
        var blocks = new StringBuilder();
        for (var k = 0; k < shapes.Count; k++)
        {
            var shape = shapes[k];
            // Shapes of groups within the group (wpg:grpSp) aside.
            if (shape.Parent != group)
                continue;
            var properties = shape.Elements().FirstOrDefault(e => e.Name.LocalName == "spPr");
            var own = properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "xfrm");
            double Of(string child, string name) =>
                double.TryParse((string?)own?.Elements().FirstOrDefault(e => e.Name.LocalName == child)?.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
            var place = new List<string>
            {
                "x=" + Centimetres((Of("off", "x") - ox) * sx / EmuPerPoint), "y=" + Centimetres((Of("off", "y") - oy) * sy / EmuPerPoint),
                "width=" + Centimetres(Of("ext", "cx") * sx / EmuPerPoint), "height=" + Centimetres(Of("ext", "cy") * sy / EmuPerPoint),
            };
            var preset = (string?)properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "prstGeom")?.Attribute("prst") ?? "rect";
            var textBox = shape.Elements().FirstOrDefault(e => e.Name.LocalName == "cNvSpPr")?.Attribute("txBox") is { } flag && (string)flag is "1" or "true"
                || shape.Elements().Any(e => e.Name.LocalName == "linkedTxbx");
            var box = textBox && preset is "rect" or "roundRect";
            var look = Look(properties, box, (FreeSlate.Editor.Writing.Tones.Of(word) ?? FreeSlate.Editor.Writing.Tones.All[0]).LineColors());
            if (!box)
                look.Insert(0, "type=" + preset);
            else if (preset == "roundRect")
                look.Add("shape=rounded");
            // A box whose story goes on in a box after it in the group.
            var story = (string?)shape.Elements().FirstOrDefault(e => e.Name.LocalName == "txbx")?.Attribute("id")
                        ?? (string?)shape.Elements().FirstOrDefault(e => e.Name.LocalName == "linkedTxbx")?.Attribute("id");
            var seq = int.TryParse((string?)shape.Elements().FirstOrDefault(e => e.Name.LocalName == "linkedTxbx")?.Attribute("seq"), out var s) ? s : 0;
            if (story != null && linkedSeqs.Any(l => l.Item1 == story && l.Item2 > seq))
                look.Add("link=next");
            if ((long?)own?.Attribute("rot") is { } rot && rot != 0)
                look.Add("turn=" + Math.Round(rot / 60000.0).ToString(CultureInfo.InvariantCulture));
            var (flipH, flipV) = ((string?)own?.Attribute("flipH") is "1" or "true", (string?)own?.Attribute("flipV") is "1" or "true");
            if (flipH || flipV)
                look.Add("flip=" + (flipH && flipV ? "both" : flipH ? "across" : "down"));
            var lines = k < texts.Count ? texts[k].Select(p => Reading.Paragraph(word, p, objects)).ToList() : [];
            blocks.Append("```").Append(box ? "box " : "shape ").Append(string.Join(" ", look.Concat(place))).Append('\n');
            foreach (var line in lines)
                blocks.Append(line).Append("\n\n");
            if (lines.Count > 0)
                blocks.Length -= 1;
            blocks.Append("```\n");
        }
        return "````group " + string.Join(" ", settings) + "\n" + blocks + "````";
    }

    /// <summary>
    /// A shape's fill, line and its colour (where it is not the tone's, <paramref name="lines"/>), shadow as the notation names them (see
    /// Markup.Shape, Objects.ShapeBlock).
    /// </summary>
    static List<string> Look(XElement? properties, bool box, (string Light, string Dark) lines)
    {
        var settings = new List<string>();
        var fill = properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "solidFill")?.Descendants().FirstOrDefault(e => e.Name.LocalName == "srgbClr");
        if ((string?)fill?.Attribute("val") is { Length: 6 } hex)
            settings.Add("fill=" + TableWords.FillName(hex));
        else if (!box)
            settings.Add("fill=none");
        var outline = properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "ln");
        var width = (long?)outline?.Attribute("w") ?? 0;
        var dash = (string?)outline?.Elements().FirstOrDefault(e => e.Name.LocalName == "prstDash")?.Attribute("val");
        var none = outline == null || outline.Elements().Any(e => e.Name.LocalName == "noFill");
        settings.Add("line=" + (none ? "none" : (string?)outline!.Attribute("cmpd") == "dbl" ? "double" : dash is "sysDot" or "dot" ? "dotted"
            : dash is "dash" or "sysDash" or "lgDash" ? "dashed" : width >= 15875 ? "strong" : "fine"));
        if (!none && (string?)outline!.Descendants().FirstOrDefault(e => e.Name.LocalName == "srgbClr")?.Attribute("val") is { Length: 6 } color && color != "FFFFFF"
            && !string.Equals(color, lines.Light, StringComparison.OrdinalIgnoreCase) && !string.Equals(color, lines.Dark, StringComparison.OrdinalIgnoreCase))
            settings.Add("color=" + TableWords.FillName(color));
        if (properties?.Descendants().Any(e => e.Name.LocalName == "outerShdw") == true)
            settings.Add("shadow=yes");
        return settings;
    }
}
