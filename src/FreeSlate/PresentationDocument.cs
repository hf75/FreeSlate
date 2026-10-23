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
/// bodies of their shapes and table cells with their paragraphs by ids unique in the document, which they keep through
/// changes; text replaced at places of a paragraph's text (Pptx.SlideText), a paragraph split in two, joined with the next
/// or taken away, as PowerPoint makes these changes (see Pptx.SlideEdits); each change undone and done again; its slides
/// as scenes with the places of their text (see PlacesItem); and the presentation saved as PPTX: the paragraphs a change
/// touched written anew in the XML they were read from, every other byte of the package as it was. A change is made in
/// the XML of its slide's part, parsed afresh; the part is read again and the slide drawn anew, the unit of reading and
/// drawing anew.
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

    /// <summary>The versions of the presentation, one for each change (see Version), and the one it reads as now.</summary>
    readonly List<Version> versions = [];
    int version;

    /// <summary>
    /// A version of the presentation: the bytes of each slide part changed since it was opened, and the ids of the
    /// paragraphs of their bodies (every other slide reads as it was opened).
    /// </summary>
    sealed record Version(IReadOnlyDictionary<string, byte[]> Parts, IReadOnlyDictionary<string, List<List<int>>> Ids);

    /// <summary>
    /// A slide as the document holds it: its part's bytes as opened and as it reads now, its text bodies with their
    /// paragraphs' ids as opened and now, and its scene once drawn.
    /// </summary>
    sealed class Slide(uint id, string part, byte[] bytes)
    {
        public uint Id { get; } = id;
        public string Part { get; } = part;
        public byte[] Original { get; } = bytes;
        public byte[] Bytes { get; set; } = bytes;
        public List<List<int>> Opened { get; set; } = [];
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
            var slide = new Slide(i < slideIds.Count ? slideIds[i] : 0, context.SlideParts[i], package.ReadBytes(context.SlideParts[i]));
            Hidden.Add(deck.IsHidden(slide.Part));
            slides.Add(slide);
            Read(slide, null);
            slide.Opened = Lists(slide);
        }
        versions.Add(new Version(new Dictionary<string, byte[]>(), new Dictionary<string, List<List<int>>>()));
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

    /// <summary>Whether a change can be undone, and one undone done again.</summary>
    public bool CanUndo => version > 0;
    public bool CanRedo => version + 1 < versions.Count;

    /// <summary>
    /// Replaces so many characters of a paragraph's text from a place with new text (a line break as "\v"), as typing
    /// over them does (see SlideEdits).
    /// </summary>
    /// <exception cref="NotSupportedException">The paragraph cannot be changed in its XML (see SlideEdits).</exception>
    public void Replace(int paragraph, int offset, int length, string text) =>
        Change(paragraph, false, (p, _, body) =>
        {
            SlideEdits.Replace(p, offset, length, text);
            return ([p], body);
        });

    /// <summary>Puts text into a paragraph at a place (see Replace).</summary>
    public void Insert(int paragraph, int offset, string text) => Replace(paragraph, offset, 0, text);

    /// <summary>Takes so many characters of a paragraph's text away from a place (see Replace).</summary>
    public void Delete(int paragraph, int offset, int length) => Replace(paragraph, offset, length, "");

    /// <summary>Splits a paragraph in two at a place, as Enter does; returns the id of the second, which follows it.</summary>
    public int Split(int paragraph, int offset)
    {
        var made = 0;
        Change(paragraph, false, (p, _, body) =>
        {
            var second = SlideEdits.Split(p, offset);
            made = ++lastId;
            body.Insert(body.IndexOf(paragraph) + 1, made);
            return ([p, second], body);
        });
        return made;
    }

    /// <summary>
    /// Joins a paragraph with the one after it in its text body, as Delete at its end does; returns the id of the paragraph
    /// that stays: the first, or the second where the first is empty (see SlideEdits.Join).
    /// </summary>
    public int Join(int paragraph)
    {
        var stays = paragraph;
        Change(paragraph, true, (p, next, body) =>
        {
            var index = body.IndexOf(paragraph);
            var kept = SlideEdits.Join(p, next!);
            stays = kept == p ? paragraph : body[index + 1];
            body.RemoveAt(kept == p ? index + 1 : index);
            return ([kept], body);
        });
        return stays;
    }

    /// <summary>Takes a paragraph away with all its text; the only paragraph of a text body stays.</summary>
    public void Remove(int paragraph) =>
        Change(paragraph, false, (p, _, body) =>
        {
            if (body.Count == 1)
                throw new NotSupportedException("The paragraph is the only one of its text body.");
            SlideEdits.Check(p);
            body.Remove(paragraph);
            return ([], body);
        });

    /// <summary>Undoes the last change not undone; false where there is none.</summary>
    public bool Undo()
    {
        if (!CanUndo)
            return false;
        Go(version - 1);
        return true;
    }

    /// <summary>Does the last change undone again; false where there is none.</summary>
    public bool Redo()
    {
        if (!CanRedo)
            return false;
        Go(version + 1);
        return true;
    }

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

    /// <summary>Writes the presentation as PPTX (see OpcPackage.Save).</summary>
    public void Save(Stream output) => package.Save(output);

    /// <summary>Saves the presentation as a PPTX file, over the one it was opened from too.</summary>
    public void Save(string target)
    {
        target = Path.GetFullPath(target);
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $"~{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        using (var stream = File.Create(temporary))
            Save(stream);
        File.Move(temporary, target, overwrite: true);
    }

    public void Dispose() => context.Dispose();

    /// <summary>
    /// Changes a paragraph in the XML of its slide's part as it reads now, parsed afresh (no alternatives of markup
    /// compatibility chosen): <paramref name="edit"/> changes it there (with the paragraph after it in its body where
    /// <paramref name="withNext"/>) and gives the paragraphs written in their place and the ids of the body's paragraphs
    /// after. The part is read again with them and its slide drawn anew; the change is a version.
    /// </summary>
    void Change(int paragraph, bool withNext, Func<XElement, XElement?, List<int>, (IReadOnlyList<XElement> Written, List<int> Body)> edit)
    {
        var (slide, bodyIndex, index) = Find(paragraph);
        var body = slide.Bodies[bodyIndex];
        var read = body.Element.Elements(Ns.A + "p").ElementAt(index);
        var readNext = withNext
            ? body.Element.Elements(Ns.A + "p").ElementAtOrDefault(index + 1) ?? throw new NotSupportedException("The paragraph is the last of its text body.")
            : null;
        var text = new PartText(slide.Bytes);
        var raw = XDocument.Parse(text.Text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        if (raw.Root?.Name != Ns.P + "sld")
            throw new NotSupportedException("The slide's part is not in the transitional names of PresentationML (a strict presentation).");
        var starts = new Dictionary<int, XElement>();
        foreach (var element in raw.Descendants())
            starts.TryAdd(text.Start(element), element);
        XElement Raw(XElement element) =>
            text.Start(element) is var at and >= 0 && starts.TryGetValue(at, out var found) && found.Name == element.Name
                ? found
                : throw new NotSupportedException("The paragraph is not where it was read.");
        var p = Raw(read);
        var next = readNext != null ? Raw(readNext) : null;
        var start = text.Start(p);
        var end = text.End(next != null ? text.Start(next) : start);
        var (written, ids) = edit(p, next, [.. body.Paragraphs]);
        var lists = Lists(slide);
        lists[bodyIndex] = ids;
        Apply(slide, text.Spliced([(start, end, string.Concat(written.Select(PartText.Xml)))]), lists);
        versions.RemoveRange(version + 1, versions.Count - version - 1);
        var now = versions[version];
        versions.Add(new Version(new Dictionary<string, byte[]>(now.Parts) { [slide.Part] = slide.Bytes },
            new Dictionary<string, List<List<int>>>(now.Ids) { [slide.Part] = lists }));
        version++;
    }

    /// <summary>The presentation as it reads in another of its versions: the slides whose parts differ read anew.</summary>
    void Go(int target)
    {
        var to = versions[target];
        foreach (var slide in slides)
        {
            var bytes = to.Parts.GetValueOrDefault(slide.Part) ?? slide.Original;
            if (!ReferenceEquals(bytes, slide.Bytes))
                Apply(slide, bytes, to.Ids.GetValueOrDefault(slide.Part) ?? slide.Opened);
        }
        version = target;
    }

    /// <summary>A slide's part given new bytes, read anew with the ids of its bodies' paragraphs.</summary>
    void Apply(Slide slide, byte[] bytes, List<List<int>> lists)
    {
        slide.Bytes = bytes;
        package.Replace(slide.Part, bytes);
        context.Changed(slide.Part);
        Read(slide, lists);
    }

    /// <summary>The ids of a slide's bodies' paragraphs as it reads now, a list for each body.</summary>
    static List<List<int>> Lists(Slide slide) => [.. slide.Bodies.Select(b => new List<int>(b.Paragraphs))];

    (Slide Slide, int Body, int Index) Find(int paragraph) =>
        paragraphs.TryGetValue(paragraph, out var found) ? found : throw new ArgumentException($"No paragraph has the id {paragraph}.", nameof(paragraph));

    /// <summary>The paragraph of an id as the presentation reads now.</summary>
    XElement Element(int paragraph)
    {
        var (slide, body, index) = Find(paragraph);
        return slide.Bodies[body].Element.Elements(Ns.A + "p").ElementAt(index);
    }

    /// <summary>
    /// A slide's text bodies read from its part as it is now, their paragraphs with the ids of <paramref name="lists"/> (a
    /// list for each body; new ids for a body whose count of paragraphs differs, and where none are given).
    /// </summary>
    void Read(Slide slide, List<List<int>>? lists)
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
            var given = lists?.ElementAtOrDefault(slide.Bodies.Count);
            var count = element.Elements(Ns.A + "p").Count();
            var numbers = given != null && given.Count == count ? new List<int>(given) : [.. Enumerable.Range(0, count).Select(_ => ++lastId)];
            var k = 0;
            foreach (var p in element.Elements(Ns.A + "p"))
            {
                ids[p] = numbers[k];
                paragraphs[numbers[k]] = (slide, slide.Bodies.Count, k);
                k++;
            }
            slide.Bodies.Add((shape, cell, element, numbers));
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
