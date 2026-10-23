using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Fonts;
using FreeSlate.Html;
using FreeSlate.Opc;
using FreeSlate.Pptx;
using FreeSlate.Rendering;

namespace FreeSlate;

/// <summary>
/// A slide of a presentation document: its id in the presentation (p:sldId/@id, which it keeps when slides move), its
/// part, whether the slide show leaves it out, and its text bodies in the order of its XML.
/// </summary>
public sealed record DeckSlide(uint Id, string Part, bool Hidden, IReadOnlyList<DeckText> Bodies);

/// <summary>
/// A text body of a slide, of a shape (p:txBody) or of a table's cell (a:txBody; Cell its row and column): its shape by
/// the id it has on the slide (p:cNvPr/@id), its paragraphs by their ids, which the places of the slide's scene name (see
/// PlacesItem).
/// </summary>
public sealed record DeckText(uint Shape, (int Row, int Column)? Cell, IReadOnlyList<int> Paragraphs);

/// <summary>
/// A PowerPoint presentation opened to change it and to save it (catalog: package.opc): its slides by their ids, the text
/// bodies of their shapes and table cells with their paragraphs by ids unique in the document, the text of a paragraph
/// (Pptx.SlideText), and its slides as scenes with the places of their text (see PlacesItem). A slide is drawn from its
/// part as the presentation reads now; the unit of reading and drawing anew is the slide.
/// </summary>
public sealed class PresentationDocument : IDisposable
{
    readonly OpcPackage package;
    readonly PresentationContext context;
    readonly SvgPaint paint = new();
    readonly List<Slide> slides = [];

    /// <summary>The paragraphs by id, each with its slide, the index of its body there and its index in the body.</summary>
    readonly Dictionary<int, (Slide Slide, int Body, int Index)> paragraphs = [];

    /// <summary>The ids of the paragraphs (a:p) of the slides as they read now, which the slides' scenes give their places.</summary>
    readonly Dictionary<XElement, int> ids = [];

    int lastId;

    /// <summary>A slide as the document holds it: its text bodies as read, their paragraphs' ids, and its scene once drawn.</summary>
    sealed class Slide(uint id, string part)
    {
        public uint Id { get; } = id;
        public string Part { get; } = part;
        public List<(uint Shape, (int Row, int Column)? Cell, XElement Element, List<int> Paragraphs)> Bodies { get; } = [];
        public PageScene? Scene { get; set; }
    }

    PresentationDocument(OpcPackage package, FontLocator? fonts, CultureInfo culture, bool embeddedFontsFirst)
    {
        this.package = package;
        context = new PresentationContext(package, fonts, embeddedFontsFirst, lines: true) { Culture = culture, Paragraphs = ids };
        var deck = new DeckInfo(context);
        var slideIds = context.Presentation.Root?.Element(Ns.P + "sldIdLst")?.Elements(Ns.P + "sldId")
            .Where(s => s.Attribute(Ns.R + "id") is { } rid && package.Relationship(context.PresentationPart, rid.Value) is { External: false } rel && package.Exists(rel.Target))
            .Select(s => uint.TryParse(s.Str("id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0).ToList() ?? [];
        for (var i = 0; i < context.SlideParts.Count; i++)
        {
            var slide = new Slide(i < slideIds.Count ? slideIds[i] : 0, context.SlideParts[i]);
            Hidden.Add(deck.IsHidden(slide.Part));
            slides.Add(slide);
            Read(slide);
        }
    }

    /// <summary>
    /// Opens a presentation from its file, which it reads into memory (the file can be written over while it is open); a
    /// password-protected one needs its password.
    /// </summary>
    /// <param name="culture">The regional format of numbers and dates in charts, as for PptxConverter.ToHtml.</param>
    /// <exception cref="PasswordException">The presentation needs a password, or the password is wrong.</exception>
    public static PresentationDocument Open(string path, string? password = null, FontLocator? fonts = null, CultureInfo? culture = null,
        bool embeddedFontsFirst = false) =>
        new(OpcPackage.Open(File.ReadAllBytes(path), password), fonts, culture ?? CultureInfo.CurrentCulture, embeddedFontsFirst);

    /// <summary>Whether the slide show leaves each slide out, by its index.</summary>
    List<bool> Hidden { get; } = [];

    /// <summary>The slides in their order, with their text bodies as the presentation reads now.</summary>
    public IReadOnlyList<DeckSlide> Slides =>
        [.. slides.Select((s, i) => new DeckSlide(s.Id, s.Part, Hidden[i], [.. s.Bodies.Select(b => new DeckText(b.Shape, b.Cell, [.. b.Paragraphs]))]))];

    /// <summary>The text of a paragraph (see SlideText).</summary>
    public string Text(int paragraph) => SlideText.Of(Element(paragraph));

    /// <summary>A slide as it is drawn now (see PptxConverter.ToScenes), by its index: its places name its paragraphs by id.</summary>
    public PageScene Scene(int index)
    {
        var slide = slides[index];
        if (slide.Scene == null)
        {
            var (width, height, items) = new SlideRenderer(new SlideContext(context, slide.Part, index + 1), paint).Scene();
            var scene = new PageScene { Number = index + 1, Paper = "", Width = width * 72 / 96, Height = height * 72 / 96, Glyphs = context.Glyphs };
            scene.Items.AddRange(items);
            slide.Scene = scene;
        }
        return slide.Scene;
    }

    /// <summary>The slides as they are drawn now, hidden ones too.</summary>
    public IReadOnlyList<PageScene> Scenes() => [.. Enumerable.Range(0, slides.Count).Select(Scene)];

    public void Dispose() => context.Dispose();

    /// <summary>The paragraph of an id as the presentation reads now.</summary>
    XElement Element(int paragraph)
    {
        if (!paragraphs.TryGetValue(paragraph, out var found))
            throw new ArgumentException($"No paragraph has the id {paragraph}.", nameof(paragraph));
        return found.Slide.Bodies[found.Body].Element.Elements(Ns.A + "p").ElementAt(found.Index);
    }

    /// <summary>A slide's text bodies read from its part as it is now, its paragraphs given new ids.</summary>
    void Read(Slide slide)
    {
        foreach (var body in slide.Bodies)
        {
            foreach (var p in body.Element.Elements(Ns.A + "p"))
                ids.Remove(p);
            foreach (var id in body.Paragraphs)
                paragraphs.Remove(id);
        }
        slide.Bodies.Clear();
        var tree = context.Load(slide.Part).Root?.Element(Ns.P + "cSld")?.Element(Ns.P + "spTree");
        foreach (var (shape, cell, element) in Bodies(tree))
        {
            var body = (shape, cell, element, new List<int>());
            foreach (var p in element.Elements(Ns.A + "p"))
            {
                var id = ++lastId;
                body.Item4.Add(id);
                ids[p] = id;
                paragraphs[id] = (slide, slide.Bodies.Count, body.Item4.Count - 1);
            }
            slide.Bodies.Add(body);
        }
        slide.Scene = null;
    }

    /// <summary>
    /// The text bodies of a slide's shapes in the order of its XML: of shapes, of the shapes of groups, of the cells of
    /// tables, each with its shape's id (and its cell's row and column).
    /// </summary>
    static IEnumerable<(uint Shape, (int Row, int Column)? Cell, XElement Body)> Bodies(XElement? tree)
    {
        foreach (var element in tree?.Elements() ?? [])
        {
            switch (element.Name.LocalName)
            {
                case "sp" when element.Element(Ns.P + "txBody") is { } body:
                    yield return (ShapeId(element), null, body);
                    break;
                case "grpSp":
                    foreach (var inner in Bodies(element))
                        yield return inner;
                    break;
                case "graphicFrame" when element.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Ns.A + "tbl") is { } table:
                    var row = 0;
                    foreach (var tr in table.Elements(Ns.A + "tr"))
                    {
                        var column = 0;
                        foreach (var tc in tr.Elements(Ns.A + "tc"))
                        {
                            if (tc.Element(Ns.A + "txBody") is { } cell)
                                yield return (ShapeId(element), (row, column), cell);
                            column++;
                        }
                        row++;
                    }
                    break;
            }
        }
    }

    /// <summary>A shape's id on its slide (p:cNvPr/@id), 0 where it has none.</summary>
    static uint ShapeId(XElement shape) =>
        uint.TryParse(shape.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("nv", StringComparison.Ordinal))?.Element(Ns.P + "cNvPr").Str("id"),
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0;
}
