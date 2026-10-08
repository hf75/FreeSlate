using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace FreeSlate.Pptx;

/// <summary>A named group of slides (catalog: presentation.sections).</summary>
public sealed record SectionInfo(string Name, List<int> Slides);

/// <summary>An alternative slide sequence (catalog: presentation.customShows).</summary>
public sealed record CustomShowInfo(int Id, string Name, List<int> Slides);

/// <summary>Slide show settings from presProps.xml (catalog: presentation.showSettings, animation.laser).</summary>
public sealed record ShowSettings(
    bool Loop, bool ShowNarration, bool ShowAnimation, bool UseTimings, string Mode,
    int? FirstSlide, int? LastSlide, int? CustomShow, string? PenColor, string? LaserColor = null);

/// <summary>Document properties from docProps/core.xml and app.xml (catalog: package.properties).</summary>
public sealed record DocumentProperties(
    string? Title, string? Subject, string? Creator, string? Keywords, string? Description, string? Category,
    string? Language, string? Created, string? Modified, string? Company);

/// <summary>
/// Presentation-level information that is not drawn on slides: properties, sections, custom shows, show settings,
/// hidden slides and speaker notes.
/// </summary>
public sealed class DeckInfo
{
    static readonly XNamespace P14 = "http://schemas.microsoft.com/office/powerpoint/2010/main";
    static readonly XNamespace Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    static readonly XNamespace DcTerms = "http://purl.org/dc/terms/";
    static readonly XNamespace Ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";

    public DocumentProperties Properties { get; }
    public List<SectionInfo> Sections { get; } = [];
    public List<CustomShowInfo> CustomShows { get; } = [];
    public ShowSettings Show { get; }

    /// <summary>Slide ids (p:sldId/@id) in slide order; sections and custom shows refer to them.</summary>
    public List<uint> SlideIds { get; } = [];

    readonly PresentationContext context;

    public DeckInfo(PresentationContext context)
    {
        this.context = context;
        var package = context.Package;
        var root = context.Presentation.Root!;

        foreach (var id in root.Element(Ns.P + "sldIdLst")?.Elements(Ns.P + "sldId") ?? [])
        {
            if (id.Attribute(Ns.R + "id") is { } rid && package.Relationship(context.PresentationPart, rid.Value) is { External: false } rel && package.Exists(rel.Target))
                SlideIds.Add(uint.TryParse(id.Str("id"), out var value) ? value : 0);
        }
        int SlideNumber(XElement sldId) => SlideIds.IndexOf(uint.TryParse(sldId.Str("id"), out var v) ? v : 0) + 1;

        foreach (var section in root.Descendants(P14 + "section"))
        {
            var slides = section.Element(P14 + "sldIdLst")?.Elements(P14 + "sldId").Select(SlideNumber).Where(n => n > 0).ToList() ?? [];
            Sections.Add(new SectionInfo(section.Str("name") ?? "", slides));
        }

        foreach (var show in root.Element(Ns.P + "custShowLst")?.Elements(Ns.P + "custShow") ?? [])
        {
            var slides = new List<int>();
            foreach (var sld in show.Element(Ns.P + "sldLst")?.Elements(Ns.P + "sld") ?? [])
            {
                var target = sld.Attribute(Ns.R + "id") is { } rid ? package.Relationship(context.PresentationPart, rid.Value)?.Target : null;
                var index = target != null ? context.SlideParts.IndexOf(target) : -1;
                if (index >= 0) slides.Add(index + 1);
            }
            CustomShows.Add(new CustomShowInfo(show.Int("id") ?? 0, show.Str("name") ?? "", slides));
        }

        Show = ReadShowSettings(context);
        Properties = ReadProperties(context);
    }

    static ShowSettings ReadShowSettings(PresentationContext context)
    {
        var part = context.Package.FirstRelationship(context.PresentationPart, "presProps")?.Target;
        var showPr = part != null && context.Package.Exists(part) ? context.Load(part).Root?.Element(Ns.P + "showPr") : null;
        var range = showPr?.Element(Ns.P + "sldRg");
        var mode = showPr?.Element(Ns.P + "kiosk") != null ? "kiosk" : showPr?.Element(Ns.P + "browse") != null ? "browse" : "present";
        // Pen and laser pointer colors stand outside any slide: no theme or color map applies.
        var colors = new Drawing.ColorContext(Drawing.Theme.Load(null), new Dictionary<string, string>());
        string? Color(XElement? parent) => colors.ResolveChild(parent) is { } color ? "#" + color.ToHex().TrimStart('#') : null;
        return new ShowSettings(
            showPr.Bool("loop") ?? false,
            // Without show settings PowerPoint plays narrations (measured); showPr without the attribute does not.
            showPr == null || (showPr.Bool("showNarration") ?? false),
            showPr.Bool("showAnimation") ?? true,
            showPr.Bool("useTimings") ?? true,
            mode,
            range.Int("st"), range.Int("end"),
            showPr?.Element(Ns.P + "custShow").Int("id"),
            Color(showPr?.Element(Ns.P + "penClr")),
            Color(showPr?.Element(Ns.P + "extLst")?.Descendants(P14 + "laserClr").FirstOrDefault()));
    }

    static DocumentProperties ReadProperties(PresentationContext context)
    {
        var package = context.Package;
        XElement? Load(string type)
        {
            var part = package.FirstRelationship("/", type)?.Target;
            return part != null && package.Exists(part) ? context.Load(part).Root : null;
        }
        var core = Load("core-properties");
        var app = Load("extended-properties");
        string? Text(XElement? parent, XName name) => parent?.Element(name)?.Value is { Length: > 0 } v ? v.Trim() : null;
        return new DocumentProperties(
            Text(core, Dc + "title"), Text(core, Dc + "subject"), Text(core, Dc + "creator"), Text(core, Cp + "keywords"),
            Text(core, Dc + "description"), Text(core, Cp + "category"), Text(core, Dc + "language"),
            Text(core, DcTerms + "created"), Text(core, DcTerms + "modified"), Text(app, Ep + "Company"));
    }

    /// <summary>p:sld/@show="0" (catalog: presentation.hiddenSlides).</summary>
    public bool IsHidden(string slidePart) => context.Load(slidePart).Root.Bool("show") == false;

    public string? SectionOf(int slideNumber) => Sections.FirstOrDefault(s => s.Slides.Contains(slideNumber))?.Name;

    /// <summary>The speaker notes of a slide as HTML paragraphs (the body placeholder of its notes slide), or null.</summary>
    public string? NotesHtml(string slidePart)
    {
        var package = context.Package;
        var part = package.FirstRelationship(slidePart, "notesSlide")?.Target;
        if (part == null || !package.Exists(part))
            return null;
        var body = context.Load(part).Root?.Element(Ns.P + "cSld")?.Element(Ns.P + "spTree")?.Descendants(Ns.P + "sp")
            .FirstOrDefault(sp => Placeholders.Ph(sp) is { } ph && Placeholders.Type(ph) == "body");
        var paragraphs = body?.Element(Ns.P + "txBody")?.Elements(Ns.A + "p").ToList();
        if (paragraphs == null || paragraphs.All(p => string.Concat(p.Descendants(Ns.A + "t").Select(t => t.Value)).Trim().Length == 0))
            return null;
        return ParagraphsHtml(body!.Element(Ns.P + "txBody"), part, package);
    }

    /// <summary>
    /// The paragraphs (a:p) of a text body as simple HTML: indent levels, line breaks, bold, italic, underline and
    /// external links (resolved through the relationships of <paramref name="part"/>).
    /// </summary>
    public static string ParagraphsHtml(XElement? body, string? part, Opc.OpcPackage package)
    {
        var html = new StringBuilder();
        foreach (var p in body?.Elements(Ns.A + "p") ?? [])
        {
            var level = p.Element(Ns.A + "pPr").Int("lvl") ?? 0;
            html.Append(level > 0 ? $"<p style=\"margin-left:{level * 1.5}em\">" : "<p>");
            foreach (var run in p.Elements())
            {
                var value = run.Name.LocalName switch
                {
                    "r" or "fld" => run.Element(Ns.A + "t")?.Value ?? "",
                    "br" => "\n",
                    _ => null,
                };
                if (value == null)
                    continue;
                if (value == "\n")
                {
                    html.Append("<br>");
                    continue;
                }
                var rPr = run.Element(Ns.A + "rPr");
                var text = WebUtility.HtmlEncode(value);
                if (rPr.Bool("b") == true) text = $"<b>{text}</b>";
                if (rPr.Bool("i") == true) text = $"<i>{text}</i>";
                if (rPr.Str("u") is { } u && u != "none") text = $"<u>{text}</u>";
                if (part != null && rPr?.Element(Ns.A + "hlinkClick")?.Attribute(Ns.R + "id") is { } link && package.Relationship(part, link.Value) is { External: true } target)
                    text = $"<a href=\"{WebUtility.HtmlEncode(target.Target)}\">{text}</a>";
                html.Append(text);
            }
            html.Append("</p>");
        }
        return html.ToString();
    }

    /// <summary>The presentation-level data for the player as JSON.</summary>
    public string Json(IReadOnlyList<SlideData> slides)
    {
        var sb = new StringBuilder("{");
        sb.Append($"\"slides\":[{string.Join(",", slides.Select(s => s.Json()))}],");
        sb.Append($"\"sections\":[{string.Join(",", Sections.Select(s => $"{{\"name\":{Quote(s.Name)},\"slides\":[{string.Join(",", s.Slides)}]}}"))}],");
        sb.Append($"\"customShows\":[{string.Join(",", CustomShows.Select(c => $"{{\"id\":{c.Id},\"name\":{Quote(c.Name)},\"slides\":[{string.Join(",", c.Slides)}]}}"))}],");
        var show = Show;
        sb.Append($"\"show\":{{\"loop\":{Bool(show.Loop)},\"narration\":{Bool(show.ShowNarration)},\"animation\":{Bool(show.ShowAnimation)},\"timings\":{Bool(show.UseTimings)},\"mode\":{Quote(show.Mode)}");
        if (show.FirstSlide is { } first) sb.Append($",\"first\":{first}");
        if (show.LastSlide is { } last) sb.Append($",\"last\":{last}");
        if (show.CustomShow is { } custom) sb.Append($",\"customShow\":{custom}");
        if (show.PenColor is { } pen) sb.Append($",\"pen\":{Quote(pen)}");
        if (show.LaserColor is { } laser) sb.Append($",\"laser\":{Quote(laser)}");
        sb.Append("}}");
        return sb.ToString();
    }

    static string Bool(bool value) => value ? "true" : "false";

    /// <summary>A JSON string literal; "&lt;/" is escaped so the JSON can sit in a script element.</summary>
    public static string Quote(string value)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '<': sb.Append("\\u003c"); break;
                case '>': sb.Append("\\u003e"); break;
                case '&': sb.Append("\\u0026"); break;
                default:
                    if (c < 0x20) sb.Append($"\\u{(int)c:x4}");
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}

/// <summary>Per-slide data for the player; transitions and animations add their parts.</summary>
public sealed class SlideData
{
    public required int Number { get; init; }
    public bool Hidden { get; init; }
    public string? Section { get; init; }

    /// <summary>Additional JSON members ("name":value) contributed by transitions, animations and links.</summary>
    public List<string> Members { get; } = [];

    public string Json()
    {
        var parts = new List<string> { $"\"number\":{Number.ToString(CultureInfo.InvariantCulture)}" };
        if (Hidden) parts.Add("\"hidden\":true");
        if (Section != null) parts.Add($"\"section\":{DeckInfo.Quote(Section)}");
        parts.AddRange(Members);
        return "{" + string.Join(",", parts) + "}";
    }
}
