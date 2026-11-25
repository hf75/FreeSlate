using System.Globalization;
using System.Security;
using System.Text;
using FreeSlate.Editor.Writing;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// A cover page as the notation writes it and Word keeps one (Insert > Cover Page): "{cover style=band title="…" subtitle="…" author="…"
/// date="2026-11-25" abstract="…"}" on a line of its own makes one at the document's start, in place of the cover it has: a building block
/// of Word's gallery Cover Pages (a block content control, w:docPartObj with w:docPartGallery Cover Pages and w:docPartUnique, as Word's
/// designs stand), its paragraph holding shapes in the tone's colours behind the text and text boxes placed on the page, each field a
/// content control named as Word names its covers' (Title, Subtitle, Author, Date, Abstract) and bound to the document's properties as
/// Word binds them (the title, subject and author, the cover page properties' date and abstract), which take its values, and a page
/// break after them. The
/// designs are FreeSlate's own: band (a broad band of the tone's colour across the top, the title on it), bar (a bar down the left side,
/// the title beside it), frame (a double frame round the page, everything centred, a small diamond under the title), plain (a short bar
/// over a large title at the top, a hairline over the author at the bottom). Read back as that line, with the ids of the cover's first
/// and last paragraph; a cover of another's design reads as its paragraphs.
/// </summary>
static class Covers
{
    /// <summary>The gallery Word's covers stand in.</summary>
    const string Gallery = "Cover Pages";

    /// <summary>The tag the cover's content control carries, naming its design.</summary>
    const string TagPrefix = "FreeSlate cover ";

    public static readonly string[] Styles = ["band", "bar", "frame", "plain"];

    /// <summary>The fields a cover has, in order: the notation's names and the names (w:alias) of their content controls, Word's.</summary>
    static readonly (string Key, string Alias)[] Fields = [("title", "Title"), ("subtitle", "Subtitle"), ("abstract", "Abstract"), ("author", "Author"), ("date", "Date")];

    /// <summary>The tones' colours a cover takes: their links' (the accent) and their titles' (the text), else a calm blue and black.</summary>
    static (string Accent, string Text, string Dark) Colors(IEditableDocument document) => (Tones.Of(document)?.Key) switch
    {
        "Magazine" => ("1D4ED8", "1C1A17", Tones.All[1].LineColors().Dark),
        "Notebook" => ("2563EB", "1F1F1F", Tones.All[2].LineColors().Dark),
        "Plain" => ("1E40AF", "111111", Tones.All[0].LineColors().Dark),
        _ => ("1F4E79", "111111", "3A3936"),
    };

    /// <summary>The cover a paragraph of the body stands in, of FreeSlate's designs: its first and last paragraph and its line; else null.</summary>
    public static (int First, int Last, string Line)? At(WordDocument word, int paragraph)
    {
        if (word.GalleryBlock(paragraph, Gallery) is not var (first, last, tag, fields) || tag is null || !tag.StartsWith(TagPrefix, StringComparison.Ordinal))
            return null;
        var line = new StringBuilder("{cover style=").Append(tag[TagPrefix.Length..]);
        foreach (var (key, alias) in Fields)
        {
            if (fields.GetValueOrDefault(alias) is { Length: > 0 } value)
                line.Append(' ').Append(key).Append("=\"").Append(value.Replace("\"", "''", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal)).Append('"');
        }
        return (first, last, line.Append('}').ToString());
    }

    /// <summary>The cover (of any design, Word's too) a paragraph of the body stands in: its first and last paragraph, or null.</summary>
    public static (int First, int Last)? Around(WordDocument word, int paragraph) =>
        word.GalleryBlock(paragraph, Gallery) is var (first, last, _, _) ? (first, last) : null;

    /// <summary>
    /// A cover made at the document's start in place of the one it has, its design and fields as given (see the class); returns its
    /// paragraphs' ids, none where it could not be made (said among the problems).
    /// </summary>
    public static IReadOnlyList<int> Make(WordDocument word, IReadOnlyDictionary<string, string> settings, List<string> problems)
    {
        var style = settings.GetValueOrDefault("style")?.Trim().ToLowerInvariant() ?? "band";
        if (!Styles.Contains(style))
        {
            problems.Add($"style={style} is no cover design ({string.Join(", ", Styles)}); band was taken.");
            style = "band";
        }
        if (word.Beginning is { } start && Around(word, start) is var (first, _))
            word.RemoveGalleryBlock(first, Gallery);
        if (word.Beginning is not { } beginning || word.TableOf(beginning) != null)
        {
            problems.Add("The cover goes before the document's first paragraph; the document begins with a table, so it was not made.");
            return [];
        }
        // Its fields bound to the document's properties (see Field), which take their values, as typing into a cover's field in Word sets
        // them: the title, subject and author; the cover page properties' date and abstract.
        string? Given(string key) => settings.GetValueOrDefault(key) is { Length: > 0 } value ? value.Trim() : null;
        var properties = new DocumentProperties { Title = Given("title"), Subject = Given("subtitle"), Author = Given("author") };
        if (properties.Title != null || properties.Subject != null || properties.Author != null)
            word.SetProperties(properties);
        var published = Given("date") is { } given ? Date(given) : null;
        if (published != null || Given("abstract") != null)
            word.SetCoverPageProperties(published, Given("abstract"));
        var section = word.SectionOf(beginning);
        var markup = Markup(style, settings, section, Colors(word), word.LanguageOf(beginning));
        return word.InsertBlocks(beginning, markup, before: true);
    }

    const double Emu = 12700;

    static string E(double points) => ((long)Math.Round(points * Emu)).ToString(CultureInfo.InvariantCulture);

    static string Escaped(string text) => SecurityElement.Escape(text) ?? "";

    /// <summary>A shape behind the text, placed on the page (from its top left corner), filled or outlined.</summary>
    static string Shape(string preset, double x, double y, double width, double height, string? fill, string? line = null, double lineWidth = 0)
    {
        var filled = fill != null ? $"<a:solidFill><a:srgbClr val=\"{fill}\"/></a:solidFill>" : "<a:noFill/>";
        var outline = line != null ? $"<a:ln w=\"{E(lineWidth)}\"><a:solidFill><a:srgbClr val=\"{line}\"/></a:solidFill></a:ln>" : "<a:ln><a:noFill/></a:ln>";
        return Anchored(x, y, width, height, behind: true, "Cover shape",
            $"<wps:wsp><wps:cNvSpPr/><wps:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{E(width)}\" cy=\"{E(height)}\"/></a:xfrm><a:prstGeom prst=\"{preset}\"><a:avLst/></a:prstGeom>{filled}{outline}</wps:spPr>"
            + "<wps:bodyPr/></wps:wsp>");
    }

    /// <summary>A drawing placed on the page, wrapped by nothing, behind the text or in front of it.</summary>
    static string Anchored(double x, double y, double width, double height, bool behind, string name, string shape) =>
        $"<w:r><w:rPr><w:noProof/></w:rPr><w:drawing><wp:anchor distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\" simplePos=\"0\" relativeHeight=\"{(behind ? 251650048 : 251660288)}\" behindDoc=\"{(behind ? 1 : 0)}\" locked=\"0\" layoutInCell=\"1\" allowOverlap=\"1\">"
        + $"<wp:simplePos x=\"0\" y=\"0\"/><wp:positionH relativeFrom=\"page\"><wp:posOffset>{E(x)}</wp:posOffset></wp:positionH><wp:positionV relativeFrom=\"page\"><wp:posOffset>{E(y)}</wp:posOffset></wp:positionV>"
        + $"<wp:extent cx=\"{E(width)}\" cy=\"{E(height)}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/><wp:wrapNone/><wp:docPr id=\"1\" name=\"{name}\"/><wp:cNvGraphicFramePr/>"
        + $"<a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\">{shape}</a:graphicData></a:graphic></wp:anchor></w:drawing></w:r>";

    /// <summary>A date as a cover's field gives it (2026-11-25); null where it is none.</summary>
    static DateTime? Date(string value) =>
        DateTime.TryParseExact(value.Trim(), ["yyyy-MM-dd", "yyyy-M-d"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    /// <summary>A content control's binding to a core property of the document (dc:title …), as Word writes it.</summary>
    static string CoreBinding(string name) =>
        "<w:dataBinding w:prefixMappings=\"xmlns:ns0='http://purl.org/dc/elements/1.1/' xmlns:ns1='http://schemas.openxmlformats.org/package/2006/metadata/core-properties' \""
        + $" w:xpath=\"/ns1:coreProperties[1]/ns0:{name}[1]\" w:storeItemID=\"{{6C3C8BC8-F283-45AE-878A-BAB7291924A1}}\"/>";

    /// <summary>A content control's binding to one of the cover page properties (PublishDate, Abstract), as Word writes it.</summary>
    static string CoverBinding(string name) =>
        $"<w:dataBinding w:prefixMappings=\"xmlns:ns0='{WordDocument.CoverPageNamespace}' \" w:xpath=\"/ns0:CoverPageProperties[1]/ns0:{name}[1]\" w:storeItemID=\"{WordDocument.CoverPageItemId}\"/>";

    /// <summary>
    /// A text box placed on the page holding a field of the cover in a content control named as Word names it, bound to the document's
    /// property Word binds it to (see Make), its text in a size and colour of the design's, bold or not, aligned as given, at the box's top
    /// or bottom; nothing where the field is not given.
    /// </summary>
    static string Field(IReadOnlyDictionary<string, string> settings, string key, double x, double y, double width, double height, double size,
        string color, bool bold, string align = "left", string anchor = "t", string? style = null, int lcid = 1033)
    {
        if (settings.GetValueOrDefault(key) is not { Length: > 0 } value)
            return "";
        var alias = Fields.First(f => f.Key == key).Alias;
        var half = ((int)Math.Round(size * 2)).ToString(CultureInfo.InvariantCulture);
        var properties = (bold ? "<w:b/><w:bCs/>" : "") + $"<w:color w:val=\"{color}\"/><w:sz w:val=\"{half}\"/><w:szCs w:val=\"{half}\"/>";
        var kind = "<w:text/>";
        var shown = value;
        // Bound to the document's properties as Word binds its covers' fields (see Make; measured in Word 365 by COM, a cover of its gallery:
        // Title, Subtitle and Author to the core properties' title, subject and creator, Date to the cover page properties' PublishDate).
        var binding = key switch
        {
            "title" => CoreBinding("title"),
            "subtitle" => CoreBinding("subject"),
            "author" => CoreBinding("creator"),
            "abstract" => CoverBinding("Abstract"),
            _ => "",
        };
        if (key == "abstract")
            kind = "<w:text w:multiLine=\"1\"/>";
        if (key == "date" && Date(value) is { } date)
        {
            var german = (lcid & 0x3FF) == 0x07;
            var (format, culture) = german ? ("d. MMMM yyyy", "de-DE") : ("MMMM d, yyyy", "en-US");
            kind = $"<w:date w:fullDate=\"{date:yyyy-MM-dd}T00:00:00Z\"><w:dateFormat w:val=\"{format}\"/><w:lid w:val=\"{culture}\"/><w:storeMappedDataAs w:val=\"dateTime\"/><w:calendar w:val=\"gregorian\"/></w:date>";
            shown = date.ToString(format, CultureInfo.GetCultureInfo(culture));
            binding = CoverBinding("PublishDate");
        }
        var id = Random.Shared.Next(100_000_000, 999_999_999).ToString(CultureInfo.InvariantCulture);
        var runs = string.Join("<w:r><w:rPr>" + properties + "</w:rPr><w:br/></w:r>", shown.Split('\n').Select(line =>
            $"<w:r><w:rPr>{properties}</w:rPr><w:t xml:space=\"preserve\">{Escaped(line.Trim())}</w:t></w:r>"));
        var paragraph = $"<w:p><w:pPr>{(style != null ? $"<w:pStyle w:val=\"{style}\"/>" : "")}<w:spacing w:before=\"0\" w:after=\"0\"/><w:jc w:val=\"{align}\"/><w:rPr>{properties}</w:rPr></w:pPr>"
            + $"<w:sdt><w:sdtPr><w:rPr>{properties}</w:rPr><w:alias w:val=\"{alias}\"/><w:tag w:val=\"{alias}\"/><w:id w:val=\"{id}\"/>{binding}{kind}</w:sdtPr><w:sdtContent>{runs}</w:sdtContent></w:sdt></w:p>";
        return Anchored(x, y, width, height, behind: false, "Cover " + alias,
            "<wps:wsp><wps:cNvSpPr txBox=\"1\"/><wps:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/>" + $"<a:ext cx=\"{E(width)}\" cy=\"{E(height)}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/><a:ln><a:noFill/></a:ln></wps:spPr>"
            + $"<wps:txbx><w:txbxContent>{paragraph}</w:txbxContent></wps:txbx>"
            + $"<wps:bodyPr rot=\"0\" vert=\"horz\" wrap=\"square\" lIns=\"0\" tIns=\"0\" rIns=\"0\" bIns=\"0\" anchor=\"{anchor}\" anchorCtr=\"0\"><a:noAutofit/></wps:bodyPr></wps:wsp>");
    }

    /// <summary>The cover's content control: its paragraph of shapes and fields in a design's places, a page break after them.</summary>
    static string Markup(string style, IReadOnlyDictionary<string, string> settings, SectionFormatting section, (string Accent, string Text, string Dark) colors, int lcid)
    {
        var (w, h) = (section.PageWidth ?? 595.3, section.PageHeight ?? 841.9);
        var (left, right, top) = (Math.Abs(section.Left ?? 72), Math.Abs(section.Right ?? 72), Math.Abs(section.Top ?? 72));
        var across = w - left - right;
        var (accent, text, dark) = colors;
        const string white = "FFFFFF";
        var parts = new StringBuilder();
        string F(string key, double x, double y, double width, double height, double size, string color, bool bold, string align = "left", string anchor = "t") =>
            Field(settings, key, x, y, width, height, size, color, bold, align, anchor, lcid: lcid);
        switch (style)
        {
            case "band":
                parts.Append(Shape("rect", 0, 0, w, 0.42 * h, accent)).Append(Shape("rect", 0, 0.42 * h, w, 0.012 * h, dark));
                parts.Append(F("title", left, 0.16 * h, across, 0.15 * h, 40, white, bold: true, anchor: "b"));
                parts.Append(F("subtitle", left, 0.325 * h, across, 0.08 * h, 18, white, bold: false));
                parts.Append(F("abstract", left, 0.50 * h, across, 0.26 * h, 11, text, bold: false));
                parts.Append(F("author", left, 0.82 * h, across, 0.035 * h, 13, accent, bold: true));
                parts.Append(F("date", left, 0.855 * h, across, 0.035 * h, 11, text, bold: false));
                break;
            case "bar":
            {
                var bar = 0.16 * w;
                var x = bar + 0.07 * w;
                var width = w - x - right;
                parts.Append(Shape("rect", 0, 0, bar, h, accent)).Append(Shape("rect", bar + 8, 0, 2, h, accent));
                parts.Append(F("title", x, 0.28 * h, width, 0.18 * h, 36, text, bold: true, anchor: "b"));
                parts.Append(Shape("rect", x, 0.47 * h, 0.12 * w, 4, accent));
                parts.Append(F("subtitle", x, 0.49 * h, width, 0.07 * h, 16, accent, bold: false));
                parts.Append(F("abstract", x, 0.58 * h, width, 0.22 * h, 11, text, bold: false));
                parts.Append(F("author", x, 0.85 * h, width, 0.035 * h, 13, text, bold: true));
                parts.Append(F("date", x, 0.885 * h, width, 0.035 * h, 11, accent, bold: false));
                break;
            }
            case "frame":
            {
                var inset = 0.05 * w;
                parts.Append(Shape("rect", inset, inset, w - 2 * inset, h - 2 * inset, null, accent, 2.25));
                parts.Append(Shape("rect", inset + 6, inset + 6, w - 2 * inset - 12, h - 2 * inset - 12, null, accent, 0.75));
                var (x, width) = (0.14 * w, 0.72 * w);
                parts.Append(F("title", x, 0.26 * h, width, 0.17 * h, 36, text, bold: true, "center", "b"));
                parts.Append(Shape("diamond", w / 2 - 9, 0.445 * h, 18, 18, accent));
                parts.Append(F("subtitle", x, 0.48 * h, width, 0.07 * h, 16, accent, bold: false, "center"));
                parts.Append(F("abstract", x, 0.57 * h, width, 0.2 * h, 11, text, bold: false, "center"));
                parts.Append(F("author", x, 0.80 * h, width, 0.035 * h, 13, text, bold: true, "center"));
                parts.Append(F("date", x, 0.835 * h, width, 0.035 * h, 11, accent, bold: false, "center"));
                break;
            }
            default:
            {
                var y = top + 0.08 * h;
                parts.Append(Shape("rect", left, y, 0.1 * w, 6, accent));
                parts.Append(F("title", left, y + 16, across, 0.16 * h, 40, text, bold: true));
                parts.Append(F("subtitle", left, y + 0.17 * h, across, 0.07 * h, 18, accent, bold: false));
                parts.Append(F("abstract", left, y + 0.27 * h, across, 0.25 * h, 11, text, bold: false));
                parts.Append(Shape("rect", left, 0.80 * h, across, 0.75, dark));
                parts.Append(F("author", left, 0.81 * h, across, 0.035 * h, 13, text, bold: true));
                parts.Append(F("date", left, 0.845 * h, across, 0.035 * h, 11, accent, bold: false));
                break;
            }
        }
        var id = Random.Shared.Next(100_000_000, 999_999_999).ToString(CultureInfo.InvariantCulture);
        return $"<w:sdt><w:sdtPr><w:tag w:val=\"{TagPrefix}{style}\"/><w:id w:val=\"{id}\"/><w:docPartObj><w:docPartGallery w:val=\"{Gallery}\"/><w:docPartUnique/></w:docPartObj></w:sdtPr>"
            + $"<w:sdtContent><w:p><w:pPr><w:spacing w:before=\"0\" w:after=\"0\"/></w:pPr>{parts}<w:r><w:br w:type=\"page\"/></w:r></w:p></w:sdtContent></w:sdt>";
    }
}
