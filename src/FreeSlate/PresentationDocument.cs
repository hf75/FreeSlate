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
/// A shape at the top of a slide's tree as the presentation reads now: its id on the slide (p:cNvPr/@id), its name, its
/// kind (sp, pic, cxnSp, graphicFrame, grpSp, contentPart) and its place in points, its own or its placeholder's on the
/// layout or master (null where it has none).
/// </summary>
public sealed record DeckShape(uint Id, string Name, string Kind, (double X, double Y, double Width, double Height)? Place);

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
/// or taken away, text and paragraphs given direct formatting (TextFormatting, ParagraphFormatting), as PowerPoint makes
/// these changes (see Pptx.SlideEdits); shapes placed, taken away and text boxes added (see Pptx.SlideShapes); slides
/// duplicated, added from layouts, moved and taken away (see Pptx.SlideList); each change undone and done again; its
/// slides as scenes with the places of their text (see PlacesItem); and the presentation saved as PPTX: what a change
/// touched written anew in the XML it was read from, every other byte of the package as it was. A change is made in the
/// XML of its parts, parsed afresh; the parts are read again and the slides drawn anew, the unit of reading and drawing
/// anew.
/// </summary>
public sealed partial class PresentationDocument : IEditableDocument
{
    readonly OpcPackage package;
    readonly PresentationContext context;
    readonly SvgPaint paint = new();

    /// <summary>The slides in the order the presentation lists them now.</summary>
    readonly List<Slide> slides = [];

    /// <summary>Every slide the document has held, by its part: a slide taken away and brought back is the one it was.</summary>
    readonly Dictionary<string, Slide> known = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the slide show leaves each slide out, by its index.</summary>
    readonly List<bool> hidden = [];

    /// <summary>The paragraphs by id, each with its slide, the index of its body there and its index in the body.</summary>
    readonly Dictionary<int, (Slide Slide, int Body, int Index)> paragraphs = [];

    /// <summary>The ids of the paragraphs (a:p) of the slides as they read now, which the slides' scenes give their places.</summary>
    readonly Dictionary<XElement, int> ids = [];

    int lastId;

    /// <summary>The versions of the presentation, one for each change (see Version), and the one it reads as now.</summary>
    readonly List<Version> versions = [];
    int version;

    /// <summary>
    /// A version of the presentation: the bytes of each part changed since it was opened (null: taken away), and the ids
    /// of the paragraphs of the bodies of each slide changed (every other part reads as it was opened, every other slide
    /// with the ids it was read with then).
    /// </summary>
    sealed record Version(IReadOnlyDictionary<string, byte[]?> Parts, IReadOnlyDictionary<string, List<List<int>>> Ids);

    /// <summary>The bytes the parts had when the presentation was opened (null: none), once asked for.</summary>
    readonly Dictionary<string, byte[]?> originals = new(StringComparer.OrdinalIgnoreCase);

    byte[]? Original(string part) => originals.TryGetValue(part, out var bytes) ? bytes : originals[part] = package.Original(part);

    /// <summary>
    /// A slide as the document holds it: its id in the presentation (p:sldId/@id), its part's bytes as it reads now, its
    /// text bodies with their paragraphs' ids as opened (for a slide the presentation had then) and now, and its scene
    /// once drawn.
    /// </summary>
    sealed class Slide(string part)
    {
        public uint Id { get; set; }
        public string Part { get; } = part;
        public byte[] Bytes { get; set; } = [];
        public List<List<int>>? Opened { get; set; }
        public List<(uint Shape, (int Row, int Column)? Cell, XElement Element, List<int> Paragraphs)> Bodies { get; } = [];
        public PageScene? Scene { get; set; }
    }

    PresentationDocument(OpcPackage package, FontLocator? fonts, CultureInfo culture, bool embeddedFontsFirst)
    {
        this.package = package;
        context = new PresentationContext(package, fonts, embeddedFontsFirst, lines: true) { Culture = culture, Paragraphs = ids };
        versions.Add(new Version(new Dictionary<string, byte[]?>(), new Dictionary<string, List<List<int>>>()));
        Order();
        foreach (var slide in slides)
            slide.Opened = Lists(slide);
    }

    /// <summary>
    /// The slides in the order the presentation lists them now (p:sldIdLst), each the one the document knows by its part
    /// (read where it is new, with the ids of the version it reads as); slides no longer listed forget their paragraphs,
    /// and where the order changed every slide is drawn anew (their numbers).
    /// </summary>
    void Order()
    {
        var before = slides.ToList();
        slides.Clear();
        var listed = context.Presentation.Root?.Element(Ns.P + "sldIdLst")?.Elements(Ns.P + "sldId")
            .Where(s => s.Attribute(Ns.R + "id") is { } rid && package.Relationship(context.PresentationPart, rid.Value) is { External: false } rel && package.Exists(rel.Target))
            .Select(s => uint.TryParse(s.Str("id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0).ToList() ?? [];
        for (var i = 0; i < context.SlideParts.Count; i++)
        {
            var part = context.SlideParts[i];
            if (!known.TryGetValue(part, out var slide))
                known[part] = slide = new Slide(part);
            slide.Id = i < listed.Count ? listed[i] : 0;
            if (!before.Contains(slide))
            {
                slide.Bytes = package.ReadBytes(part);
                context.Changed(part);
                Read(slide, versions[version].Ids.GetValueOrDefault(part) ?? slide.Opened);
            }
            slides.Add(slide);
        }
        foreach (var gone in before.Except(slides))
            Forget(gone);
        var deck = new DeckInfo(context);
        hidden.Clear();
        hidden.AddRange(slides.Select(s => deck.IsHidden(s.Part)));
        if (!before.SequenceEqual(slides))
            slides.ForEach(s => s.Scene = null);
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

    /// <summary>The slides in their order, with their text bodies as the presentation reads now.</summary>
    public IReadOnlyList<DeckSlide> Slides =>
        [.. slides.Select((s, i) => new DeckSlide(s.Id, s.Part, hidden[i], [.. s.Bodies.Select(b => new DeckText(b.Shape, b.Cell, [.. b.Paragraphs]))]))];

    /// <summary>The text of a paragraph (see SlideText).</summary>
    public string Text(int paragraph) => SlideText.Of(Element(paragraph));

    /// <summary>Whether a paragraph of that id is one of the slides' shapes or tables, which the presentation changes.</summary>
    public bool Contains(int paragraph) => paragraphs.ContainsKey(paragraph);

    /// <summary>The word a place stands inside, as PowerPoint's formatting buttons take it, hyphens joining words (see Editing.Words).</summary>
    public (int Offset, int Length)? WordAt(int paragraph, int offset) => Editing.Words.Around(Text(paragraph), offset, hyphens: true);

    /// <summary>Whether a change can be undone, and one undone done again.</summary>
    public bool CanUndo => version > 0;
    public bool CanRedo => version + 1 < versions.Count;

    /// <summary>How many changes the presentation holds as it reads now: 0 as opened, one more for each change made, one less for each undone.</summary>
    public int Changes => version;

    /// <summary>The paragraph just before or after a paragraph in its shape's (or table cell's) text, or null at its edge.</summary>
    public int? Before(int paragraph)
    {
        var (slide, body, index) = Find(paragraph);
        return index > 0 ? slide.Bodies[body].Paragraphs[index - 1] : null;
    }

    public int? After(int paragraph)
    {
        var (slide, body, index) = Find(paragraph);
        var list = slide.Bodies[body].Paragraphs;
        return index + 1 < list.Count ? list[index + 1] : null;
    }

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

    /// <summary>
    /// Gives so many characters of a paragraph's text from a place character formatting (see TextFormatting), as
    /// PowerPoint's buttons do; a stretch that reaches the paragraph's end formats its mark too, which an empty
    /// paragraph's formatting is (see SlideEdits.Format).
    /// </summary>
    public void Format(int paragraph, int offset, int length, TextFormatting format) =>
        Change(paragraph, false, (p, _, body) =>
        {
            SlideEdits.Format(p, offset, length, format);
            return ([p], body);
        });

    /// <summary>Gives a paragraph paragraph formatting (see ParagraphFormatting; slides have no paragraph styles).</summary>
    public void Format(int paragraph, ParagraphFormatting format) =>
        Change(paragraph, false, (p, _, body) =>
        {
            SlideEdits.Format(p, format);
            return ([p], body);
        });

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

    /// <summary>Deleting a selection over paragraphs joins them, as PowerPoint does (see IEditableDocument.KeepsMarks).</summary>
    public bool KeepsMarks => false;

    /// <summary>The paragraph after or before a paragraph in reading order: in its shape's or table cell's text, as After and Before.</summary>
    public int? Next(int paragraph) => After(paragraph);
    public int? Previous(int paragraph) => Before(paragraph);

    /// <summary>Takes the paragraphs between two paragraphs of one shape's (or table cell's) text away.</summary>
    /// <exception cref="NotSupportedException">The two are not of one text body, the first before the second; nothing changes.</exception>
    public void RemoveBetween(int first, int last)
    {
        var between = new List<int>();
        for (var p = After(first); p != last; p = After(p.Value))
        {
            if (p == null)
                throw new NotSupportedException("The paragraphs are not of one text body, the first before the second.");
            between.Add(p.Value);
        }
        foreach (var p in between)
            Remove(p);
    }

    /// <summary>The shapes at the top of a slide's tree (a group as one), by the slide's id, as the presentation reads now.</summary>
    public IReadOnlyList<DeckShape> Shapes(uint slide)
    {
        var s = SlideOf(slide);
        return [.. (Tree(s)?.Elements() ?? []).Where(IsShape).Select(e => new DeckShape(ShapeId(e), Shape(e)?.Str("name") ?? "", e.Name.LocalName,
            Transform(s, e) is { } xfrm && xfrm.Element(Ns.A + "off") is { } off && xfrm.Element(Ns.A + "ext") is { } ext
                ? ((off.Long("x") ?? 0) / Units.EmuPerPoint, (off.Long("y") ?? 0) / Units.EmuPerPoint, (ext.Long("cx") ?? 0) / Units.EmuPerPoint, (ext.Long("cy") ?? 0) / Units.EmuPerPoint)
                : null))];
    }

    /// <summary>
    /// Places a shape at the top of a slide's tree in points, its position and size, keeping its turn and flips: a
    /// placeholder that takes its place from its layout gets one of its own, a group moves and scales its shapes (see
    /// SlideShapes.Place).
    /// </summary>
    /// <exception cref="NotSupportedException">The shape cannot be placed in its XML (see SlideShapes).</exception>
    public void PlaceShape(uint slide, uint shape, double x, double y, double width, double height)
    {
        if (!(width >= 0 && height >= 0 && double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(width) && double.IsFinite(height)))
            throw new ArgumentOutOfRangeException(nameof(width), "A shape is placed at a point and is at least empty.");
        var s = SlideOf(slide);
        var read = ShapeOf(s, shape);
        var inherited = Transform(s, read);
        var raw = Parse(s);
        // Each alternative of markup compatibility the shape stands in, placed alike.
        var changed = SlideShapes.Alternatives(raw.Of(read, "shape")).Select(e => SlideShapes.Place(e, Emu(x), Emu(y), Emu(width), Emu(height), inherited)).ToList();
        Commit(s, raw, changed.Select(raw.Written), Lists(s));
    }

    /// <summary>
    /// Takes a shape at the top of a slide's tree away with its text and what animates it, and connectors' connections
    /// to it (see SlideShapes).
    /// </summary>
    public void DeleteShape(uint slide, uint shape)
    {
        var s = SlideOf(slide);
        var read = ShapeOf(s, shape);
        var raw = Parse(s);
        var (element, ids) = SlideShapes.Removed(raw.Of(read, "shape"));
        var (start, end) = raw.Range(element);
        var changes = new List<(int Start, int End, string Text)> { (start, end, "") };
        var tree = raw.Xml.Root!.Element(Ns.P + "cSld")!.Element(Ns.P + "spTree")!;
        var connections = SlideShapes.Connections(tree, ids, element);
        var holders = connections.Select(c => c.Parent!).Distinct().ToList();
        var ranges = holders.ToDictionary(h => h, raw.Range);
        connections.ForEach(c => c.Remove());
        changes.AddRange(holders.Select(h => (ranges[h].Start, ranges[h].End, PartText.Xml(h))));
        foreach (var timing in raw.Xml.Root.Descendants(Ns.P + "timing").ToList())
        {
            var range = raw.Range(timing);
            var (timingChanged, gone) = SlideShapes.Unanimate(timing, ids);
            if (timingChanged)
                changes.Add((range.Start, range.End, gone ? "" : PartText.Xml(timing)));
        }
        var lists = Enumerable.Range(0, s.Bodies.Count).Where(k => !s.Bodies[k].Element.Ancestors().Contains(read)).Select(k => new List<int>(s.Bodies[k].Paragraphs)).ToList();
        Commit(s, raw, changes, lists);
    }

    /// <summary>
    /// Puts a text box on a slide, at the top of its tree, in points (see SlideShapes.TextBox: its paragraphs the
    /// text's lines, "\n", a line break "\v"), as high as its text as PowerPoint makes a box that resizes to its text
    /// (see Refit); returns its id on the slide.
    /// </summary>
    public uint AddTextBox(uint slide, double x, double y, double width, double height, string text)
    {
        if (!(width >= 0 && height >= 0 && double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(width) && double.IsFinite(height)))
            throw new ArgumentOutOfRangeException(nameof(width), "A shape is placed at a point and is at least empty.");
        if (text.IndexOfAny(['\r', '￼']) >= 0)
            throw new ArgumentException("A text box's text holds no carriage returns (its lines are paragraphs) and no objects.", nameof(text));
        var s = SlideOf(slide);
        var raw = Parse(s);
        var tree = raw.Xml.Root?.Element(Ns.P + "cSld")?.Element(Ns.P + "spTree") ?? throw new NotSupportedException("The slide has no shape tree.");
        var greatest = raw.Xml.Descendants().Where(e => e.Name.LocalName == "cNvPr")
            .Select(e => long.TryParse(e.Attribute("id")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0).DefaultIfEmpty(0).Max();
        if (greatest >= uint.MaxValue)
            throw new NotSupportedException("The slide's shapes have used up the ids.");
        var id = (uint)(greatest + 1);
        var box = SlideShapes.TextBox(id, Emu(x), Emu(y), Emu(width), Emu(height), text);
        int at;
        if (tree.Element(Ns.P + "extLst") is { } extension)
        {
            at = raw.Range(extension).Start;
            extension.AddBeforeSelf(box);
        }
        else
        {
            var last = tree.Elements().Last();
            at = raw.Range(last).End;
            last.AddAfterSelf(box);
        }
        var lists = Lists(s);
        lists.Add([.. text.Split('\n').Select(_ => ++lastId)]);
        Commit(s, raw, [(at, at, PartText.Xml(box))], lists);
        Refit(s, id);
        return id;
    }

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

    /// <summary>Slides are drawn whole after each change: none are left as they were (see IEditableDocument.LayingOut).</summary>
    public bool LayingOut => false;

    public bool LayOut(int pages = 4) => false;

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
        var raw = Parse(slide);
        var p = raw.Of(read, "paragraph");
        var next = readNext != null ? raw.Of(readNext, "paragraph") : null;
        var start = raw.Text.Start(p);
        var end = raw.Text.End(next != null ? raw.Text.Start(next) : start);
        var (written, ids) = edit(p, next, [.. body.Paragraphs]);
        var lists = Lists(slide);
        lists[bodyIndex] = ids;
        Commit(slide, raw, [(start, end, string.Concat(written.Select(PartText.Xml)))], lists);
        if (body.Cell == null)
            Refit(slide, body.Shape);
    }

    /// <summary>
    /// A shape whose text changed fitted to its text anew, as PowerPoint fits it when the text is edited: text that shrinks
    /// to fit (normAutofit) takes the scale and line spacing reduction it fits at, a shape that grows to its text
    /// (spAutoFit, with its own size, neither turned nor flipped) the height of its text and insets, its top, middle or
    /// bottom where it was as its text is anchored (measured with PowerPoint 365's object model: rectangles of centered
    /// text keep their middles). Part of the change just made, as PowerPoint
    /// makes it one with the text.
    /// </summary>
    void Refit(Slide slide, uint shape)
    {
        var index = slides.IndexOf(slide);
        var renderer = new SlideRenderer(new SlideContext(context, slide.Part, index + 1), paint) { Refitting = new HashSet<uint> { shape } };
        var (width, height, items) = renderer.Scene();
        if (!renderer.Fitted.TryGetValue(shape, out var fit))
            return;
        var raw = Parse(slide);
        var id = shape.ToString(CultureInfo.InvariantCulture);
        var sp = raw.Xml.Descendants(Ns.P + "sp").FirstOrDefault(s => s.Element(Ns.P + "nvSpPr")?.Element(Ns.P + "cNvPr")?.Attribute("id")?.Value == id);
        if (sp?.Element(Ns.P + "txBody")?.Element(Ns.A + "bodyPr") is not { } bodyPr)
            return;
        XElement? changed = null;
        if (fit.Height is { } textHeight)
        {
            if (sp.Element(Ns.P + "spPr")?.Element(Ns.A + "xfrm") is { } xfrm && (xfrm.Long("rot") ?? 0) == 0 && xfrm.Attribute("flipV")?.Value is not ("1" or "true")
                && xfrm.Element(Ns.A + "ext") is { } ext && ext.Long("cy") is { } cy && Math.Abs(cy / Units.EmuPerPoint - textHeight) > 0.05)
            {
                var made = (long)Math.Round(textHeight * Units.EmuPerPoint);
                ext.SetAttributeValue("cy", made.ToString(CultureInfo.InvariantCulture));
                if (xfrm.Element(Ns.A + "off") is { } off && off.Long("y") is { } y && fit.Anchor is "ctr" or "b")
                    off.SetAttributeValue("y", (y + (cy - made) / (fit.Anchor == "ctr" ? 2 : 1)).ToString(CultureInfo.InvariantCulture));
                changed = xfrm;
            }
        }
        else
        {
            // Stored on the shape's own body (where it inherits the autofit, a normAutofit of its own where it shrinks).
            var own = bodyPr.Element(Ns.A + "normAutofit");
            var (scale, reduction) = ((long)Math.Round(fit.Scale * 100000), (long)Math.Round(fit.Reduction * 100000));
            if (own == null && (scale != 100000 || reduction != 0))
            {
                own = new XElement(Ns.A + "normAutofit");
                if (bodyPr.Element(Ns.A + "prstTxWarp") is { } warp)
                    warp.AddAfterSelf(own);
                else
                    bodyPr.AddFirst(own);
            }
            if (own != null && ((own.Long("fontScale") ?? 100000) != scale || (own.Long("lnSpcReduction") ?? 0) != reduction))
            {
                own.SetAttributeValue("fontScale", scale != 100000 ? scale.ToString(CultureInfo.InvariantCulture) : null);
                own.SetAttributeValue("lnSpcReduction", reduction != 0 ? reduction.ToString(CultureInfo.InvariantCulture) : null);
                changed = bodyPr;
            }
        }
        if (changed == null)
        {
            // As drawn: the scene drawn to fit is the slide's.
            var scene = new PageScene { Number = index + 1, Paper = "", Width = width * 72 / 96, Height = height * 72 / 96, Glyphs = context.Glyphs };
            scene.Items.AddRange(items);
            slide.Scene = scene;
            return;
        }
        var bytes = raw.Text.Spliced([raw.Written(changed)]);
        var now = versions[version];
        versions[version] = new Version(new Dictionary<string, byte[]?>(now.Parts, StringComparer.OrdinalIgnoreCase) { [slide.Part] = bytes }, now.Ids);
        package.Replace(slide.Part, bytes);
        context.Changed(slide.Part);
        slide.Bytes = bytes;
        Read(slide, now.Ids.GetValueOrDefault(slide.Part) ?? slide.Opened);
    }

    /// <summary>
    /// A slide's part as it reads now, parsed afresh with line info (no alternatives of markup compatibility chosen):
    /// its text, its XML, and the element of it each element read from it is.
    /// </summary>
    sealed class Raw(PartText text, XDocument xml)
    {
        readonly Dictionary<int, XElement> starts = xml.Descendants().GroupBy(text.Start).ToDictionary(g => g.Key, g => g.First());

        public PartText Text => text;
        public XDocument Xml => xml;

        /// <summary>The element a read element of the part is in the XML parsed afresh.</summary>
        public XElement Of(XElement read, string what) =>
            text.Start(read) is var at and >= 0 && starts.TryGetValue(at, out var found) && found.Name == read.Name
                ? found
                : throw new NotSupportedException($"The {what} is not where it was read.");

        /// <summary>Where an element of the XML parsed afresh stands in the part's text, from its start to its end.</summary>
        public (int Start, int End) Range(XElement element) => text.Start(element) is var start and >= 0
            ? (start, text.End(start))
            : throw new InvalidOperationException("The element was made after the part was read.");

        /// <summary>The element written anew in its place.</summary>
        public (int Start, int End, string Text) Written(XElement element) => Range(element) is var (start, end) ? (start, end, PartText.Xml(element)) : default;
    }

    static Raw Parse(Slide slide)
    {
        var text = new PartText(slide.Bytes);
        var xml = XDocument.Parse(text.Text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        if (xml.Root?.Name != Ns.P + "sld")
            throw new NotSupportedException("The slide's part is not in the transitional names of PresentationML (a strict presentation).");
        return new Raw(text, xml);
    }

    /// <summary>
    /// A slide's part changed: stretches of its text replaced, read again with the ids of its bodies' paragraphs, the
    /// slide drawn anew; the change is a version.
    /// </summary>
    void Commit(Slide slide, Raw raw, IEnumerable<(int Start, int End, string Text)> changes, List<List<int>> lists) =>
        Commit(new Dictionary<string, byte[]?> { [slide.Part] = raw.Text.Spliced(changes) }, new Dictionary<string, List<List<int>>> { [slide.Part] = lists });

    /// <summary>
    /// Parts changed (new bytes, or null: taken away) with the ids of the paragraphs of the slides among them: the change is
    /// a version, which the presentation then reads as.
    /// </summary>
    void Commit(IReadOnlyDictionary<string, byte[]?> parts, IReadOnlyDictionary<string, List<List<int>>> lists)
    {
        versions.RemoveRange(version + 1, versions.Count - version - 1);
        var now = versions[version];
        var allParts = new Dictionary<string, byte[]?>(now.Parts, StringComparer.OrdinalIgnoreCase);
        foreach (var (part, bytes) in parts)
            allParts[part] = bytes;
        var allLists = new Dictionary<string, List<List<int>>>(now.Ids, StringComparer.OrdinalIgnoreCase);
        foreach (var (part, list) in lists)
            allLists[part] = list;
        versions.Add(new Version(allParts, allLists));
        Go(version + 1);
    }

    /// <summary>
    /// The presentation as it reads in another of its versions: the parts whose bytes differ given theirs (or taken away),
    /// the slides read anew in the order listed where the presentation part changed, those whose parts differ read anew
    /// with the ids of that version.
    /// </summary>
    void Go(int target)
    {
        var (from, to) = (versions[version], versions[target]);
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in from.Parts.Keys.Union(to.Parts.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var now = from.Parts.TryGetValue(part, out var a) ? a : Original(part);
            var then = to.Parts.TryGetValue(part, out var b) ? b : Original(part);
            if (ReferenceEquals(now, then))
                continue;
            if (then == null)
                package.Remove(part);
            else
                package.Replace(part, then);
            context.Changed(part);
            changed.Add(part);
        }
        version = target;
        if (changed.Contains(context.PresentationPart) || changed.Contains(SlideList.RelationshipsOf(context.PresentationPart)))
        {
            context.ReadSlides();
            Order();
        }
        foreach (var slide in slides.Where(s => changed.Contains(s.Part)))
        {
            slide.Bytes = package.ReadBytes(slide.Part);
            Read(slide, to.Ids.GetValueOrDefault(slide.Part) ?? slide.Opened);
        }
    }

    /// <summary>A slide no longer listed: its paragraphs are no longer the presentation's.</summary>
    void Forget(Slide slide)
    {
        foreach (var body in slide.Bodies)
        {
            foreach (var p in body.Element.Elements(Ns.A + "p"))
                ids.Remove(p);
            foreach (var id in body.Paragraphs)
                paragraphs.Remove(id);
        }
        slide.Bodies.Clear();
        slide.Scene = null;
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
        Forget(slide);
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
        uint.TryParse(Shape(shape).Str("id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0;

    /// <summary>A shape's non-visual properties (p:cNvPr): its id, name, description.</summary>
    static XElement? Shape(XElement shape) =>
        shape.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("nv", StringComparison.Ordinal))?.Element(Ns.P + "cNvPr");

    static bool IsShape(XElement element) => element.Name.Namespace == Ns.P && element.Name.LocalName is "sp" or "pic" or "cxnSp" or "graphicFrame" or "grpSp" or "contentPart";

    static long Emu(double points) => (long)Math.Round(points * Units.EmuPerPoint);

    Slide SlideOf(uint id) => slides.FirstOrDefault(s => s.Id == id) ?? throw new ArgumentException($"No slide has the id {id}.", nameof(id));

    XElement? Tree(Slide slide) => context.Load(slide.Part).Root?.Element(Ns.P + "cSld")?.Element(Ns.P + "spTree");

    /// <summary>The shape of an id at the top of a slide's tree, as the presentation reads now.</summary>
    XElement ShapeOf(Slide slide, uint id)
    {
        var found = (Tree(slide)?.Elements() ?? []).Where(e => IsShape(e) && ShapeId(e) == id).ToList();
        return found.Count switch
        {
            1 => found[0],
            0 => throw new ArgumentException($"The slide has no shape {id} at the top of its tree.", nameof(id)),
            _ => throw new NotSupportedException($"Several shapes of the slide have the id {id}."),
        };
    }

    /// <summary>
    /// The transform a shape takes its place from: its own, else that of the placeholder on its layout or master it
    /// inherits from (as SlideRenderer chains them); null where it has none.
    /// </summary>
    XElement? Transform(Slide slide, XElement shape)
    {
        static XElement? Own(XElement element) => element.Name.LocalName switch
        {
            "graphicFrame" => element.Element(Ns.P + "xfrm"),
            "grpSp" => element.Element(Ns.P + "grpSpPr")?.Element(Ns.A + "xfrm"),
            _ => element.Element(Ns.P + "spPr")?.Element(Ns.A + "xfrm"),
        };
        if (Own(shape) is { } own && own.Element(Ns.A + "ext") != null)
            return own;
        if (Placeholders.Ph(shape) is not { } ph)
            return null;
        var at = new SlideContext(context, slide.Part, slides.IndexOf(slide) + 1);
        if (at.Layout != null)
        {
            if (Placeholders.InLayout(at.Layout, ph) is { } layoutShape)
            {
                if (Own(layoutShape) is { } layoutOwn && layoutOwn.Element(Ns.A + "ext") != null)
                    return layoutOwn;
                ph = Placeholders.Ph(layoutShape)!;
            }
            else if (!Placeholders.Unindexed(ph))
                return null;
        }
        return Placeholders.InMaster(at.Master, ph) is { } masterShape && Own(masterShape) is { } masterOwn && masterOwn.Element(Ns.A + "ext") != null ? masterOwn : null;
    }
}
