using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FreeSlate.Pptx;

/// <summary>
/// Legacy VML drawings (catalog: legacy.vml). PowerPoint 2007 saved the pictures of OLE objects and ActiveX controls
/// only in a VML drawing part of the slide: p:oleObj and p:control name their VML shape (spid), whose v:imagedata
/// holds the picture; later versions add a DrawingML picture (p:pic) as well. PowerPoint draws the VML picture when
/// there is no other (measured), the control at the place the VML shape's style gives. Organization charts and
/// diagrams of PowerPoint 2002 and 2003 stay VML behind com:legacyDrawing (see <see cref="Shape"/>).
/// </summary>
public static partial class LegacyDrawing
{
    static readonly XNamespace V = "urn:schemas-microsoft-com:vml";
    static readonly XNamespace O = "urn:schemas-microsoft-com:office:office";
    static readonly XNamespace P = "urn:schemas-microsoft-com:office:powerpoint";

    /// <summary>The picture of a legacy shape: the VML part, the image's relationship id in it, its crop (fractions) and box.</summary>
    public sealed record Picture(string Part, string ImageId, (double L, double T, double R, double B) Crop, (double X, double Y, double W, double H)? Box);

    /// <summary>The picture of VML shape <paramref name="spid"/> in the VML drawings of <paramref name="slidePart"/>, or null.</summary>
    public static Picture? Find(PresentationContext presentation, string slidePart, string? spid)
    {
        if (string.IsNullOrEmpty(spid))
            return null;
        var package = presentation.Package;
        foreach (var rel in package.Relationships(slidePart).Where(r => r.Is("vmlDrawing") && !r.External && package.Exists(r.Target)))
        {
            var drawing = Load(presentation, rel.Target);
            var shape = drawing?.Descendants().FirstOrDefault(e => e.Name.Namespace == V
                && ((string?)e.Attribute("id") == spid || (string?)e.Attribute(O + "spid") == spid));
            if (shape?.Element(V + "imagedata") is not { } image)
                continue;
            var id = (string?)image.Attribute(O + "relid") ?? (string?)image.Attribute(Ns.R + "id");
            if (id == null || package.Relationship(rel.Target, id) is not { External: false })
                continue;
            var crop = (Fraction(image, "cropleft"), Fraction(image, "croptop"), Fraction(image, "cropright"), Fraction(image, "cropbottom"));
            return new Picture(rel.Target, id, crop, Box((string?)shape.Attribute("style")));
        }
        return null;
    }

    /// <summary>
    /// VML shape <paramref name="spid"/> of the VML drawings of <paramref name="slidePart"/> (the organization charts
    /// and diagrams of PowerPoint 2002 and 2003 behind com:legacyDrawing) with the part that holds it, in a container
    /// for the VML converter (Vml.ToDrawings), or null. Shapes whose text lies in a part of its own (p:textdata, see
    /// <see cref="DiagramText"/>) carry it as a DrawingML text body.
    /// </summary>
    public static (string Part, XElement Container)? Shape(PresentationContext presentation, string slidePart, string? spid)
    {
        if (string.IsNullOrEmpty(spid))
            return null;
        var package = presentation.Package;
        foreach (var rel in package.Relationships(slidePart).Where(r => r.Is("vmlDrawing") && !r.External && package.Exists(r.Target)))
        {
            if (Load(presentation, rel.Target)?.Descendants().FirstOrDefault(e => e.Name.Namespace == V && e.Name.LocalName != "shapetype"
                    && ((string?)e.Attribute("id") == spid || (string?)e.Attribute(O + "spid") == spid)) is not { } found)
                continue;
            var shape = new XElement(found);
            foreach (var node in shape.DescendantsAndSelf().Where(e => e.Element(P + "textdata") != null).ToList())
            {
                if ((string?)node.Element(P + "textdata")!.Attribute("id") is { } id && package.Relationship(rel.Target, id) is { External: false } text
                    && package.Exists(text.Target) && DiagramText(package.ReadBytes(text.Target), node) is { } body)
                    node.Add(body);
            }
            // PowerPoint draws the connectors of legacy drawings as the outline of their line stroked with the line, so
            // twice as wide; the ends it draws half the width longer lie under the boxes they join (measured: poi/
            // PPTWithAttachments's organization chart, its PDF an outline 2.25 pt wide stroked with 2.25 pt, in its
            // slides 4.5 pt wide, 2 pt for lines of 1 pt; the boxes' lines as given).
            foreach (var connector in shape.DescendantsAndSelf().Where(e => (string?)e.Attribute(O + "connectortype") is { } kind && kind != "none"))
            {
                var type = ((string?)connector.Attribute("type"))?.TrimStart('#') is { } reference
                    ? found.Document?.Descendants(V + "shapetype").FirstOrDefault(t => (string?)t.Attribute("id") == reference) : null;
                var weight = Docx.Vml.Length((string?)connector.Attribute("strokeweight") ?? (string?)type?.Attribute("strokeweight")) ?? 0.75;
                connector.SetAttributeValue("strokeweight", (2 * weight).ToString("R", CultureInfo.InvariantCulture) + "pt");
            }
            return (rel.Target, new XElement(V + "container", shape));
        }
        return null;
    }

    /// <summary>
    /// The text of a node of a legacy diagram (p:textdata) as a DrawingML text body for the slide renderer
    /// (wps:txbx3D, which the VML converter passes on), or null: PowerPoint 97's records of it (MS-PPT TextCharsAtom
    /// or TextBytesAtom, StyleTextPropAtom), in the shape's text box (v:textbox inset, v-text-anchor,
    /// mso-wrap-style). Paragraphs keep their alignment, runs their bold, italic, underline, size, raise and RGB
    /// color; scheme colors and fonts (references into a font table the package does not have) are the defaults'
    /// (poi/PPTWithAttachments: "Тўхталиб, двадцать пять, zeitgeber" italic in 8 pt, centered across the top box).
    /// </summary>
    static XElement? DiagramText(byte[] data, XElement shape)
    {
        string? text = null;
        byte[]? styles = null;
        void Records(int start, int end)
        {
            for (var at = start; at + 8 <= end;)
            {
                var type = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at + 2));
                var length = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at + 4));
                var body = at + 8;
                if (length > (uint)(end - body))
                    return;
                if ((data[at] & 0x0F) == 0x0F)
                    Records(body, body + (int)length);
                else if (type == 0x0FA0)
                    text ??= Encoding.Unicode.GetString(data, body, (int)length & ~1);
                else if (type == 0x0FA8)
                    text ??= Encoding.Latin1.GetString(data, body, (int)length);
                else if (type == 0x0FA1)
                    styles ??= data[body..(body + (int)length)];
                at = body + (int)length;
            }
        }
        Records(0, data.Length);
        if (string.IsNullOrEmpty(text))
            return null;
        var (paragraphs, runs) = styles != null ? Styles(styles, text.Length + 1) : ([], []);
        static T? At<T>(List<(int Count, T Value)> list, int index) where T : class
        {
            foreach (var (count, value) in list)
            {
                if (index < count)
                    return value;
                index -= count;
            }
            return list.Count > 0 ? list[^1].Value : null;
        }
        static XElement Properties(string name, CharacterStyle? style) => new(Ns.A + name, new XAttribute("lang", "en-US"),
            style?.Size is { } size ? new XAttribute("sz", (long)Math.Round(size * 100)) : null,
            style?.Bold is { } bold ? new XAttribute("b", bold ? 1 : 0) : null,
            style?.Italic is { } italic ? new XAttribute("i", italic ? 1 : 0) : null,
            style?.Underline is { } underline ? new XAttribute("u", underline ? "sng" : "none") : null,
            style?.Raise is { } raise ? new XAttribute("baseline", raise * 1000) : null,
            style?.Color is { } color ? new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", color))) : null);

        var properties = Style((string?)shape.Attribute("style"));
        var inset = ((string?)shape.Element(V + "textbox")?.Attribute("inset") ?? "").Split(',');
        long Inset(int i, double fallback) => (long)Math.Round((i < inset.Length && Docx.Vml.Length(inset[i]) is { } v ? v : fallback) * 12700);
        var anchor = properties.GetValueOrDefault("v-text-anchor") ?? "top";
        var result = new XElement(Docx.Drawings.Wps + "txbx3D",
            new XElement(Ns.A + "bodyPr", properties.GetValueOrDefault("mso-wrap-style") == "none" ? new XAttribute("wrap", "none") : null,
                new XAttribute("lIns", Inset(0, 7.2)), new XAttribute("tIns", Inset(1, 3.6)), new XAttribute("rIns", Inset(2, 7.2)), new XAttribute("bIns", Inset(3, 3.6)),
                new XAttribute("anchor", anchor.StartsWith("middle", StringComparison.Ordinal) ? "ctr" : anchor.StartsWith("bottom", StringComparison.Ordinal) ? "b" : "t"),
                anchor.Contains("center", StringComparison.Ordinal) ? new XAttribute("anchorCtr", 1) : null));
        // A paragraph per carriage return, a line break per vertical tab, a run per stretch of one character style.
        var position = 0;
        foreach (var line in text.Split('\r'))
        {
            var paragraph = new XElement(Ns.A + "p");
            if (At(paragraphs, position) is { Length: > 0 } align)
                paragraph.Add(new XElement(Ns.A + "pPr", new XAttribute("algn", align)));
            for (var at = 0; at < line.Length;)
            {
                var run = At(runs, position + at);
                if (line[at] == '\v')
                {
                    paragraph.Add(new XElement(Ns.A + "br", Properties("rPr", run)));
                    at++;
                    continue;
                }
                var end = at + 1;
                while (end < line.Length && line[end] != '\v' && ReferenceEquals(At(runs, position + end), run))
                    end++;
                paragraph.Add(new XElement(Ns.A + "r", Properties("rPr", run), new XElement(Ns.A + "t", line[at..end])));
                at = end;
            }
            paragraph.Add(Properties("endParaRPr", At(runs, position + line.Length)));
            result.Add(paragraph);
            position += line.Length + 1;
        }
        return result;
    }

    /// <summary>A run's character properties in PowerPoint 97's records, null where the run leaves them to the defaults.</summary>
    sealed record CharacterStyle(bool? Bold, bool? Italic, bool? Underline, double? Size, int? Raise, string? Color);

    /// <summary>
    /// The paragraph alignments and character styles of a StyleTextPropAtom (MS-PPT TextPFRun, TextCFRun), each with
    /// the count of characters it covers, for text of <paramref name="length"/> characters (its paragraph mark
    /// included); fields not read are skipped by their masks.
    /// </summary>
    static (List<(int Count, string Value)> Paragraphs, List<(int Count, CharacterStyle Value)> Runs) Styles(byte[] data, int length)
    {
        var (paragraphs, runs) = (new List<(int, string)>(), new List<(int, CharacterStyle)>());
        var at = 0;
        int U16() { var v = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at)); at += 2; return v; }
        uint U32() { var v = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at)); at += 4; return v; }
        static bool Has(uint masks, int bit) => (masks & 1u << bit) != 0;
        try
        {
            for (var covered = 0; covered < length && at < data.Length;)
            {
                var count = (int)U32();
                at += 2;
                var masks = U32();
                // Bullet flags, character, font, size and color.
                at += ((masks & 0xF) != 0 ? 2 : 0) + (Has(masks, 7) ? 2 : 0) + (Has(masks, 4) ? 2 : 0) + (Has(masks, 6) ? 2 : 0) + (Has(masks, 5) ? 4 : 0);
                var align = Has(masks, 11) ? U16() switch { 0 => "l", 1 => "ctr", 2 => "r", 3 => "just", 4 => "dist", 5 => "thaiDist", 6 => "justLow", _ => "" } : "";
                // Line spacing, space before and after, left margin, indent, default tab size, tab stops, font
                // alignment, wrap flags, text direction.
                at += (Has(masks, 12) ? 2 : 0) + (Has(masks, 13) ? 2 : 0) + (Has(masks, 14) ? 2 : 0) + (Has(masks, 8) ? 2 : 0) + (Has(masks, 10) ? 2 : 0) + (Has(masks, 15) ? 2 : 0);
                if (Has(masks, 20))
                    at += 4 * U16();
                at += (Has(masks, 16) ? 2 : 0) + ((masks & 0x7u << 17) != 0 ? 2 : 0) + (Has(masks, 21) ? 2 : 0);
                paragraphs.Add((count, align));
                covered += count;
            }
            for (var covered = 0; covered < length && at < data.Length;)
            {
                var count = (int)U32();
                var masks = U32();
                // The style bits (bold, italic, underline, shadow, emboss …) exist where any of their masks does.
                var style = (masks & 0x3EB7) != 0 ? U16() : 0;
                bool? Flag(int bit) => Has(masks, bit) ? (style & 1 << bit) != 0 : null;
                // Font, East Asian, ANSI and symbol font references.
                at += (Has(masks, 16) ? 2 : 0) + (Has(masks, 21) ? 2 : 0) + (Has(masks, 22) ? 2 : 0) + (Has(masks, 23) ? 2 : 0);
                double? size = Has(masks, 17) ? U16() : null;
                string? color = null;
                if (Has(masks, 18))
                {
                    // A color index 0xFE gives red, green and blue; the others name a color of the scheme.
                    if (data[at + 3] == 0xFE)
                        color = $"{data[at]:X2}{data[at + 1]:X2}{data[at + 2]:X2}";
                    at += 4;
                }
                int? raise = Has(masks, 19) ? (short)U16() : null;
                at += (Has(masks, 20) ? 4 : 0) + (Has(masks, 24) ? 2 : 0) + (Has(masks, 25) ? 2 : 0) + (Has(masks, 26) ? 4 : 0);
                runs.Add((count, new CharacterStyle(Flag(0), Flag(1), Flag(2), size, raise, color)));
                covered += count;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            // A record cut short: the runs read so far.
        }
        return (paragraphs, runs);
    }

    /// <summary>A VML part; older ones are HTML-like (unclosed br elements), which are closed before parsing.</summary>
    static XDocument? Load(PresentationContext presentation, string part)
    {
        try
        {
            return presentation.Load(part);
        }
        catch (System.Xml.XmlException)
        {
            var text = System.Text.Encoding.UTF8.GetString(presentation.Package.ReadBytes(part));
            try { return XDocument.Parse(UnclosedBreak().Replace(text, "<br/>")); }
            catch (System.Xml.XmlException) { return null; }
        }
    }

    [GeneratedRegex(@"<br\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex UnclosedBreak();

    /// <summary>A crop value: a fraction, or 1/65536ths with the suffix f.</summary>
    static double Fraction(XElement image, string attribute)
    {
        var value = (string?)image.Attribute(attribute);
        if (string.IsNullOrEmpty(value))
            return 0;
        return value.EndsWith('f')
            ? double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f / 65536 : 0
            : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    /// <summary>The properties of a VML shape's style, by lowercase name.</summary>
    static Dictionary<string, string> Style(string? style) => (style ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Split(':', 2)).Where(p => p.Length == 2)
        .GroupBy(p => p[0].Trim().ToLowerInvariant(), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last()[1].Trim(), StringComparer.Ordinal);

    /// <summary>The box of a VML shape's style (position: absolute; left, top, width, height) in EMU, or null.</summary>
    static (double X, double Y, double W, double H)? Box(string? style)
    {
        if (style == null)
            return null;
        var properties = Style(style);
        double? Length(string name) => properties.TryGetValue(name, out var v) ? Emu(v) : null;
        return Length("left") is { } x && Length("top") is { } y && Length("width") is { } w && Length("height") is { } h ? (x, y, w, h) : null;
    }

    /// <summary>A CSS length (pt, in, cm, mm, px; points without unit) in EMU.</summary>
    static double? Emu(string value)
    {
        var match = Regex.Match(value, @"^(-?[0-9.]+)\s*([a-z]*)$", RegexOptions.IgnoreCase);
        if (!match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return null;
        return match.Groups[2].Value.ToLowerInvariant() switch
        {
            "in" => number * 914400,
            "cm" => number * 360000,
            "mm" => number * 36000,
            "px" => number * 9525,
            "emu" => number,
            _ => number * 12700,
        };
    }

    /// <summary>A DrawingML picture showing <paramref name="picture"/> in the box <paramref name="xfrm"/> (EMU), for the renderer.</summary>
    public static XElement Element(Picture picture, string id, string name, (double X, double Y, double W, double H) xfrm)
    {
        var (l, t, r, b) = picture.Crop;
        static string Per(double fraction) => ((long)Math.Round(fraction * 100000)).ToString(CultureInfo.InvariantCulture);
        return new XElement(Ns.P + "pic",
            new XElement(Ns.P + "nvPicPr", new XElement(Ns.P + "cNvPr", new XAttribute("id", id), new XAttribute("name", name)),
                new XElement(Ns.P + "cNvPicPr"), new XElement(Ns.P + "nvPr")),
            new XElement(Ns.P + "blipFill",
                new XElement(Ns.A + "blip", new XAttribute(Ns.R + "embed", picture.ImageId)),
                new XElement(Ns.A + "srcRect", new XAttribute("l", Per(l)), new XAttribute("t", Per(t)), new XAttribute("r", Per(r)), new XAttribute("b", Per(b))),
                new XElement(Ns.A + "stretch", new XElement(Ns.A + "fillRect"))),
            new XElement(Ns.P + "spPr",
                new XElement(Ns.A + "xfrm",
                    new XElement(Ns.A + "off", new XAttribute("x", (long)Math.Round(xfrm.X)), new XAttribute("y", (long)Math.Round(xfrm.Y))),
                    new XElement(Ns.A + "ext", new XAttribute("cx", (long)Math.Round(xfrm.W)), new XAttribute("cy", (long)Math.Round(xfrm.H)))),
                new XElement(Ns.A + "prstGeom", new XAttribute("prst", "rect"), new XElement(Ns.A + "avLst"))));
    }
}
