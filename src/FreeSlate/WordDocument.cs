using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Docx.Layout;
using FreeSlate.Fonts;
using FreeSlate.Opc;
using FreeSlate.Rendering;

namespace FreeSlate;

/// <summary>
/// A Word document opened to change it and to save it (catalog: package.opc): its paragraphs by the ids the reader gives
/// them (Block.Id, which the places of its pages name, see PlacesItem) and keep through changes; text replaced at places
/// of a paragraph's text (Docx.ParagraphText), a paragraph split in two, joined with the next or taken away, text and
/// paragraphs given direct formatting (Docx.TextFormatting, Docx.ParagraphFormatting), as tracked changes where an
/// author is set (Docx.Tracking); each change undone and done again; its pages as it reads now (with its markup or as
/// all its tracked changes accepted); and the document saved as DOCX: the stretches of its body it
/// changed written anew in the XML they were read from, every other byte of the package as it was (see Docx.TextEdits).
/// A change is made in the XML the blocks it touches were read from, a stretch of the main part's text: parsed afresh,
/// changed, made ready as the part was when it was loaded and read anew in place of those blocks (ModelReader.Reread),
/// the numbers of the lists after it worked out anew; its pages are laid out again from the last checkpoint before it
/// (Paginator.Relayout). Where reading cannot begin afresh around a change (a field reaching across paragraphs, a
/// paragraph whose mark is hidden or deleted, a note reference, a section break that comes or goes), the document is read
/// again whole. Changes reach the body's paragraphs, in tables and text boxes too.
/// </summary>
public sealed class WordDocument : IDisposable
{
    readonly OpcPackage package;
    readonly FontLocator? fonts;
    readonly CultureInfo culture;
    readonly DateTime? printed;
    readonly bool markup;
    DocumentContext context = null!;
    ModelReader reader = null!;

    /// <summary>The body's blocks as the document reads now, and its paragraphs (of tables and text boxes too) by id, each with the block of the body it stands in.</summary>
    List<Block> blocks = [];
    readonly Dictionary<int, (ParagraphBlock Paragraph, Block Top)> paragraphs = [];

    /// <summary>
    /// The main part's bytes as the document was read from them whole last, their text, its loaded body, and the start
    /// tags of its root and body with end tags for them, which stretches of the body are parsed within (see Stretch).
    /// </summary>
    byte[] whole = null!;
    PartText wholeText = null!;
    XElement? body;
    string head = "", tail = "";

    /// <summary>The stretches of the body changed since it was read whole, in order (see Island).</summary>
    IReadOnlyList<Island> islands = [];

    /// <summary>The stretches of the body read anew since (see Piece), in order, and the piece each child of the body read anew was read from.</summary>
    readonly List<Piece> pieces = [];
    readonly Dictionary<XElement, Piece> pieceOf = new(ReferenceEqualityComparer.Instance);

    /// <summary>The versions of the document, one for each change (see Version), and the one it reads as now.</summary>
    readonly List<Version> versions = [];
    int version;

    /// <summary>How often the document was read whole, when it was opened and where a change could not be read anew.</summary>
    int wholeReads;

    /// <summary>
    /// The layout of the body as it read when its pages were laid out last: the blocks, the paginator (which lays them out
    /// again from its checkpoints), the pages drawn, the papers they name and the budget of 3D text each had and took
    /// (see DocumentContext.Text3DBudget); null before the first or after the document was read whole.
    /// </summary>
    List<Block>? laid;
    ParagraphLayout? layout;
    Paginator? paginator;
    PageRenderer? renderer;
    List<PageScene>? scenes;
    List<(int Paper, double Budget, double Cost)> drawnAs = [];
    (int Pages, string Sections) counts;
    bool? storiesRead;

    /// <summary>The budget of 3D text the document's pages are drawn with, from the first (see DocumentContext.Text3DBudget).</summary>
    double budget;

    /// <summary>A stretch of the main part's text as read whole (Start to End) and the text that stands there now.</summary>
    sealed record Island(int Start, int End, string Text);

    /// <summary>
    /// A stretch of the main part's text as read whole (Start to End) that the body read anew from Text: the line info of
    /// its children is where they stand in Read, the text within the start and end tags around it.
    /// </summary>
    sealed class Piece(int start, int end, string text, PartText read)
    {
        public int Start { get; } = start;
        public int End { get; } = end;
        public string Text { get; } = text;
        public PartText Read { get; } = read;
    }

    /// <summary>
    /// A version of the document: the main part's bytes it was read from whole, the stretches of its body changed since
    /// and its body's blocks, whose ids the blocks read for it again take.
    /// </summary>
    sealed record Version(byte[] Whole, IReadOnlyList<Island> Islands, List<Block> Blocks);

    WordDocument(OpcPackage package, FontLocator? fonts, CultureInfo culture, DateTime? printed, bool markup)
    {
        (this.package, this.fonts, this.culture, this.printed, this.markup) = (package, fonts, culture, printed, markup);
        ReadWhole();
        versions.Add(new Version(whole, islands, blocks));
    }

    /// <summary>
    /// Opens a Word document, read into memory (so that it can be saved over its file): a password-protected one with its
    /// password (it is saved without it); its fields of the date print at <paramref name="printed"/> (now when null), its
    /// numbers and dates in <paramref name="culture"/>; with <paramref name="markup"/> it reads with its tracked changes
    /// and comments as Word shows them with all markup (catalog: review.revisions), else as all of them accepted.
    /// </summary>
    /// <exception cref="PasswordException">The document needs a password, or the password is wrong.</exception>
    public static WordDocument Open(string path, string? password = null, FontLocator? fonts = null, CultureInfo? culture = null, DateTime? printed = null,
        bool markup = false) =>
        new(OpcPackage.Open(File.ReadAllBytes(path), password), fonts, culture ?? CultureInfo.CurrentCulture, printed, markup);

    /// <summary>
    /// The author whose changes are tracked changes (Word's Track Changes: see Docx.Tracking), or null: changes are made
    /// as they are; and the time they carry, now where null.
    /// </summary>
    public string? Author { get; set; }
    public DateTime? ChangedAt { get; set; }

    /// <summary>The id the next tracked change takes: one more than the greatest id the main part's elements have.</summary>
    int changeId = -1;

    /// <summary>The body's blocks as the document reads now.</summary>
    public IReadOnlyList<Block> Blocks => blocks;

    /// <summary>A paragraph of the body (of its tables and text boxes too) by its id, or null.</summary>
    public ParagraphBlock? Paragraph(int id) => paragraphs.TryGetValue(id, out var found) ? found.Paragraph : null;

    /// <summary>The text of a paragraph (see ParagraphText).</summary>
    public string Text(int paragraph) => ParagraphText.Of(Find(paragraph).Paragraph);

    /// <summary>Whether a change can be undone, and one undone done again.</summary>
    public bool CanUndo => version > 0;
    public bool CanRedo => version + 1 < versions.Count;

    /// <summary>
    /// Replaces the text of a paragraph from a place, so many characters, by other text (without breaks): it takes the
    /// format of the text before it.
    /// </summary>
    /// <exception cref="NotSupportedException">The range holds what cannot be changed so (see TextEdits); nothing changes.</exception>
    public void Replace(int paragraph, int offset, int length, string text)
    {
        var (p, top) = Find(paragraph);
        Change(top, top, (stretch, tracking) => TextEdits.Replace(stretch, p, offset, length, text, tracking));
    }

    /// <summary>Puts text into a paragraph at a place (see Replace).</summary>
    public void Insert(int paragraph, int offset, string text) => Replace(paragraph, offset, 0, text);

    /// <summary>Takes so many characters of a paragraph's text away from a place (see Replace).</summary>
    public void Delete(int paragraph, int offset, int length) => Replace(paragraph, offset, length, "");

    /// <summary>
    /// Splits a paragraph in two at a place, as Enter does; returns the id of the second, or of the paragraph the reader
    /// joins it into (the first, where its mark is hidden). The first keeps the paragraph's id.
    /// </summary>
    /// <exception cref="NotSupportedException">The place is within what cannot be split (a field, a content control); nothing changes.</exception>
    public int Split(int paragraph, int offset)
    {
        var (p, top) = Find(paragraph);
        var second = 0;
        var where = Change(top, top, (stretch, tracking) =>
        {
            var (text, at) = TextEdits.Split(stretch, p, offset, tracking);
            second = at;
            return text;
        });
        bool Second(XElement? element) => element != null && where(element) == second;
        return (paragraphs.Values.FirstOrDefault(v => Second(v.Paragraph.Element)).Paragraph
                ?? paragraphs.Values.FirstOrDefault(v => v.Paragraph.Content.Any(i => Second(i.Xml?.Ancestors(v.Paragraph.Element.Name).FirstOrDefault()))).Paragraph)?.Id
               ?? paragraph;
    }

    /// <summary>
    /// Joins a paragraph with the paragraph after it in its body, cell or text box, as Delete at its end does (see
    /// TextEdits.Join); returns the id of the joined paragraph: the first's, or the second's where the first was empty.
    /// </summary>
    /// <exception cref="NotSupportedException">No paragraph follows it there, or the two cannot be joined so; nothing changes.</exception>
    public int Join(int paragraph)
    {
        var (p, top) = Find(paragraph);
        var (list, index) = ListOf(top, p) ?? throw new ArgumentException($"The document's body has no paragraph {paragraph}.", nameof(paragraph));
        if (index + 1 >= list.Count || list[index + 1] is not ParagraphBlock next)
            throw new NotSupportedException("No paragraph follows the paragraph in its body, cell or text box.");
        var first = true;
        Change(top, ReferenceEquals(list, blocks) ? next : top, (stretch, tracking) =>
        {
            var (text, stays) = TextEdits.Join(stretch, p, next, tracking);
            first = stays;
            return text;
        });
        return first ? paragraph : next.Id;
    }

    /// <summary>
    /// Gives a stretch of a paragraph's text character formatting (see TextFormatting): its runs split where it begins
    /// and ends, those within it formatted.
    /// </summary>
    /// <exception cref="NotSupportedException">The stretch holds what cannot be formatted so (an equation, a ruby, what the reader made); nothing changes.</exception>
    public void Format(int paragraph, int offset, int length, TextFormatting format)
    {
        var (p, top) = Find(paragraph);
        Change(top, top, (stretch, tracking) => TextEdits.Format(stretch, p, offset, length, format, tracking));
    }

    /// <summary>Gives a paragraph paragraph formatting (see ParagraphFormatting).</summary>
    /// <exception cref="NotSupportedException">The paragraph holds content of another (see TextEdits); nothing changes.</exception>
    public void Format(int paragraph, ParagraphFormatting format)
    {
        var (p, top) = Find(paragraph);
        Change(top, top, (stretch, tracking) => TextEdits.Format(stretch, p, format, tracking));
    }

    /// <summary>Takes a paragraph away with all its content; the section it ends goes on into the next one.</summary>
    /// <exception cref="NotSupportedException">It is the last of its body, cell or text box; nothing changes.</exception>
    public void Remove(int paragraph)
    {
        var (p, top) = Find(paragraph);
        Change(top, top, (stretch, tracking) => TextEdits.Remove(stretch, p, tracking));
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

    /// <summary>
    /// The pages of the document as it reads now, laid out and drawn as DocxConverter does (see PageScene): after a change
    /// laid out again from the last checkpoint before it, the pages that come out as they were kept as they were drawn.
    /// </summary>
    public IReadOnlyList<PageScene> Scenes()
    {
        if (scenes != null && ReferenceEquals(laid, blocks))
            return scenes;
        List<PageBox> pages;
        var (from, until) = (0, 0);
        if (paginator == null || layout == null || renderer == null)
        {
            layout = new ParagraphLayout(context, new RunStyles(context));
            // Notes are read after the body: their numbers come from the order of their references.
            var footnotes = reader.Notes(endnotes: false);
            var endnoteParts = reader.Notes(endnotes: true);
            var endnotes = endnoteParts.Where(n => n.Key is not ("separator" or "continuationSeparator" or "continuationNotice")).OrderBy(n => reader.EndnoteNumber(n.Key))
                .Where(n => reader.EndnoteNumber(n.Key) > 0).Select(n => (reader.EndnoteSection(n.Key), n.Value)).ToList();
            paginator = new Paginator(context, layout, reader) { EndnoteContinuation = endnoteParts.GetValueOrDefault("continuationSeparator"), KeepCheckpoints = true };
            pages = paginator.Paginate(blocks, reader.LastSection(), footnotes, endnotes, endnoteParts.GetValueOrDefault("separator"));
            renderer = new PageRenderer(context, new Drawings(context, culture), new TableLayout(context, layout));
            scenes = null;
        }
        else
        {
            var relaid = paginator.Relayout(blocks);
            (pages, from, until) = (relaid.Pages, relaid.From, relaid.Until);
        }
        renderer.Settings = paginator.Settings;
        // The pages before the one laid out again from and from the one the layout before followed on are drawn as they
        // were, unless what their headers and footers show may have changed with them: their number, a section's number
        // of pages, fields that read the body; those after it also not where line numbers count on over pages.
        var now = (pages.Count, string.Join(",", pages.Where(p => !p.Blank).GroupBy(p => p.SectionIndex).Select(g => $"{g.Key}:{g.Count()}")));
        var keep = scenes != null && from > 0 && now == counts && !StoriesRead(pages);
        var counted = pages.Any(p => p.Section.Element.Child("lnNumType") is { } numbering && (numbering.Number("countBy") ?? 0) >= 1
            && numbering.Attr("restart") is "newSection" or "continuous");
        var sizes = new List<(double Width, double Height)>();
        var drawn = new List<PageScene>(pages.Count);
        var how = new List<(int Paper, double Budget, double Cost)>(pages.Count);
        // The pages take the budget of 3D text in their order, as a pass over all of them gives it: a page drawn before is
        // kept where it had the same.
        context.Text3DBudget = budget;
        for (var k = 0; k < pages.Count; k++)
        {
            var page = pages[k];
            var paper = sizes.FindIndex(p => Math.Abs(p.Width - page.PaperWidth) < 0.01 && Math.Abs(p.Height - page.PaperHeight) < 0.01);
            if (paper < 0)
            {
                sizes.Add((page.PaperWidth, page.PaperHeight));
                paper = sizes.Count - 1;
            }
            var left = context.Text3DBudget;
            if (keep && k < scenes!.Count && drawnAs[k].Paper == paper && drawnAs[k].Budget == left && (k < from - 1 || until > 0 && k >= until - 1 && !counted))
            {
                drawn.Add(scenes[k]);
                context.Text3DBudget -= drawnAs[k].Cost;
            }
            else
                drawn.Add(renderer.Scene(page, $"paper{paper}"));
            how.Add((paper, left, left - context.Text3DBudget));
        }
        (scenes, drawnAs, counts, laid) = (drawn, how, now, blocks);
        return scenes;
    }

    /// <summary>Writes the document as DOCX (see OpcPackage.Save).</summary>
    public void Save(Stream output)
    {
        package.Replace(context.MainPart, Spliced(whole, islands));
        package.Save(output);
    }

    /// <summary>Saves the document as a DOCX file, over the one it was opened from too.</summary>
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

    public void Dispose() => package.Dispose();

    (ParagraphBlock Paragraph, Block Top) Find(int id) =>
        paragraphs.TryGetValue(id, out var found) ? found : throw new ArgumentException($"The document's body has no paragraph {id}.", nameof(id));

    /// <summary>
    /// A change made in the stretch of the body the blocks from <paramref name="from"/> to <paramref name="to"/> were
    /// read from (the children of the body they stand in, see StretchOf): <paramref name="edit"/> gives the stretch's new
    /// text, which the document then reads (see Apply), a version of its own. Returns where an element of the new text
    /// stands in it.
    /// </summary>
    Func<XElement, int> Change(Block from, Block to, Func<Stretch, Tracking?, string> edit)
    {
        var (start, end, old, stretch) = StretchOf(IndexOf(from), IndexOf(to));
        var tracking = Author != null ? new Tracking(Author, ChangedAt ?? DateTime.Now, ChangeId()) : null;
        var text = edit(stretch, tracking);
        if (tracking != null)
            changeId = tracking.NextId;
        var changed = islands.Where(i => i.End <= start || i.Start >= end).ToList();
        if (text != wholeText.Text[start..end])
            changed.Add(new Island(start, end, text));
        var where = Apply(start, end, text, old, null, [.. changed.OrderBy(i => i.Start)], null);
        versions.RemoveRange(version + 1, versions.Count - version - 1);
        versions.Add(new Version(whole, islands, blocks));
        version++;
        return where;
    }

    /// <summary>The id the next tracked change takes (see changeId).</summary>
    int ChangeId()
    {
        if (changeId < 0)
        {
            var greatest = islands.Select(i => i.Text).Prepend(wholeText.Text)
                .SelectMany(text => System.Text.RegularExpressions.Regex.Matches(text, @"(?<=[\s:]id="")\d{1,9}(?="")").Select(m => int.Parse(m.Value, CultureInfo.InvariantCulture)))
                .DefaultIfEmpty(0).Max();
            changeId = greatest + 1;
        }
        return changeId;
    }

    /// <summary>The document as it reads in another of its versions: the stretches whose text differs read anew (see Apply).</summary>
    void Go(int target)
    {
        var to = versions[target];
        if (islands.Except(to.Islands).Concat(to.Islands.Except(islands)).ToList() is { Count: > 0 } differ)
        {
            var (old, before, start, end) = Covering(differ.Min(i => i.Start), differ.Max(i => i.End));
            var text = new StringBuilder();
            var at = start;
            foreach (var island in to.Islands.Where(i => i.Start >= start && i.End <= end))
            {
                text.Append(wholeText.Text, at, island.Start - at).Append(island.Text);
                at = island.End;
            }
            text.Append(wholeText.Text, at, end - at);
            Apply(start, end, text.ToString(), old, before, to.Islands, to.Blocks);
        }
        versions[target] = new Version(whole, islands, blocks);
        version = target;
    }

    /// <summary>
    /// The stretch of the body (<paramref name="start"/> to <paramref name="end"/> of the main part's text as read whole)
    /// read as <paramref name="text"/> now, the stretches changed since the document was read whole being
    /// <paramref name="changed"/>: in place of the children <paramref name="old"/> of the body (none to put them before
    /// <paramref name="before"/>) the text is parsed within the tags around the body's children, made ready as the part
    /// was when loaded and read anew (ModelReader.Reread), the blocks read anew taking the ids of <paramref name="like"/>
    /// where given (another version of the body), else of the blocks they stand for; where the reader cannot read it so,
    /// the document is read again whole (the main part's bytes with the stretches changed put in), the versions' stretches
    /// changed taken over (see Rebase). Returns where an element read from the text stands in it, or -1.
    /// </summary>
    Func<XElement, int> Apply(int start, int end, string text, List<XElement> old, XElement? before, IReadOnlyList<Island> changed, IReadOnlyList<Block>? like)
    {
        var was = blocks;
        var read = new PartText(head + text + tail);
        var xml = XDocument.Parse(read.Text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        var children = xml.Root?.Elements().FirstOrDefault() is { } parent ? context.Prepare(parent) : [];
        if (head.Length > 0 && reader.Reread(blocks, old, before, children, like) is { } result)
        {
            var piece = new Piece(start, end, text, read);
            pieces.RemoveAll(p => p.Start >= start && p.End <= end);
            pieces.Insert(pieces.FindIndex(p => p.Start >= end) is var k and >= 0 ? k : pieces.Count, piece);
            foreach (var e in old)
                pieceOf.Remove(e);
            foreach (var child in children)
                pieceOf[child] = piece;
            (blocks, islands) = (result, changed);
            Reindex(was);
            return element => Top(element) is { } top && pieceOf.GetValueOrDefault(top) == piece && read.Start(element) is var at and >= 0 ? at - head.Length : -1;
        }
        // Read whole, with the main part's bytes as they read now: where the stretch's text went is where it stood, the
        // stretches changed before it moving it on.
        var moved = start + changed.Where(i => i.End <= start).Sum(i => i.Text.Length - (i.End - i.Start));
        var previous = wholeText;
        package.Replace(context.MainPart, Spliced(whole, changed));
        ReadWhole();
        Rebase(previous, changed);
        ModelReader.Keep(like ?? was, blocks);
        Index();
        var length = text.Length;
        return element => wholeText.Start(element) - moved is var at and >= 0 && at < length ? at : -1;
    }

    /// <summary>
    /// The versions' stretches changed as stretches of the main part's text as read whole now, which is the text it was
    /// read from whole before (<paramref name="previous"/>) with <paramref name="applied"/> put in: where a version's
    /// stretches and the applied ones meet, the stretch of both, with the version's text where it differs. Where that
    /// stretch now holds no text (a paragraph the version has and the text has not), it takes in the child of the body
    /// after it (before it, at the body's end), so that it is known where it goes.
    /// </summary>
    void Rebase(PartText previous, IReadOnlyList<Island> applied)
    {
        var children = body?.Elements().Where(e => wholeText.Start(e) >= 0).ToList() ?? [];
        for (var v = 0; v < versions.Count; v++)
        {
            var own = versions[v].Islands;
            var regions = new List<(int Start, int End)>();
            foreach (var (s, e) in own.Concat(applied).Select(i => (i.Start, i.End)).OrderBy(r => r.Start))
            {
                if (regions.Count > 0 && s <= regions[^1].End)
                    regions[^1] = (regions[^1].Start, Math.Max(regions[^1].End, e));
                else
                    regions.Add((s, e));
            }
            string Text(IReadOnlyList<Island> set, int s, int e)
            {
                var text = new StringBuilder();
                var at = s;
                foreach (var island in set.Where(i => i.Start >= s && i.End <= e))
                {
                    text.Append(previous.Text, at, island.Start - at).Append(island.Text);
                    at = island.End;
                }
                return text.Append(previous.Text, at, e - at).ToString();
            }
            var rebased = new List<Island>();
            foreach (var (s, e) in regions)
            {
                var (mine, now) = (Text(own, s, e), Text(applied, s, e));
                // Where the region begins in the text now: moved on by the applied stretches before it, which end before it.
                var start = s + applied.Where(i => i.End < s).Sum(i => i.Text.Length - (i.End - i.Start));
                if (mine != now)
                    rebased.Add(new Island(start, start + now.Length, mine));
            }
            for (var k = 0; k < rebased.Count; k++)
            {
                var (start, end, mine) = (rebased[k].Start, rebased[k].End, rebased[k].Text);
                if (start != end)
                    continue;
                if (children.FirstOrDefault(c => wholeText.Start(c) >= end) is { } after && after.Name.LocalName != "sectPr")
                {
                    // With the child after it; with the next stretch where that one begins in the child.
                    var until = wholeText.End(wholeText.Start(after));
                    if (k + 1 < rebased.Count && rebased[k + 1].Start < until)
                    {
                        var next = rebased[k + 1];
                        rebased[k] = new Island(start, next.End, mine + wholeText.Text[start..next.Start] + next.Text);
                        rebased.RemoveAt(k + 1);
                    }
                    else
                        rebased[k] = new Island(start, until, mine + wholeText.Text[start..until]);
                }
                else if (children.LastOrDefault(c => wholeText.Start(c) < start) is { } prior)
                {
                    var from = wholeText.Start(prior);
                    if (k > 0 && rebased[k - 1].End > from)
                    {
                        var last = rebased[k - 1];
                        rebased[k - 1] = new Island(last.Start, start, last.Text + wholeText.Text[last.End..start] + mine);
                        rebased.RemoveAt(k--);
                    }
                    else
                        rebased[k] = new Island(from, start, wholeText.Text[from..start] + mine);
                }
            }
            versions[v] = versions[v] with { Whole = whole, Islands = rebased };
        }
    }

    /// <summary>
    /// The stretch of the main part's text (as read whole) that the blocks <paramref name="first"/> to
    /// <paramref name="last"/> of the body were read from: the children of the body they stand in, with the others read
    /// from the same pieces and those between them; its start, its end, the children and the stretch as it reads now,
    /// parsed afresh.
    /// </summary>
    (int Start, int End, List<XElement> Old, Stretch Stretch) StretchOf(int first, int last)
    {
        if (head.Length == 0 || Top(Element(blocks[first])) is not { } a || Top(Element(blocks[last])) is not { } b || RangeOf(a) is not { } from
            || RangeOf(b) is not { } to)
            throw new NotSupportedException("The paragraph is not where it was read.");
        var (start, end) = (from.Start, to.End);
        // The children of the pieces they were read from, whole.
        while (Previous(a) is { } previous && pieceOf.TryGetValue(a, out var piece) && pieceOf.GetValueOrDefault(previous) == piece)
            a = previous;
        var old = new List<XElement>();
        for (var e = a; e != null; e = Next(e))
        {
            old.Add(e);
            if (e == b)
            {
                while (Next(e) is { } next && pieceOf.TryGetValue(b, out var piece) && pieceOf.GetValueOrDefault(next) == piece)
                    old.Add(e = next);
                break;
            }
        }
        var within = pieces.Where(p => p.Start >= start && p.End <= end).ToList();
        var text = new StringBuilder();
        var at = start;
        foreach (var piece in within)
        {
            text.Append(wholeText.Text, at, piece.Start - at).Append(piece.Text);
            at = piece.End;
        }
        text.Append(wholeText.Text, at, end - at);
        try
        {
            return (start, end, old, new Stretch(head, text.ToString(), tail, read => Where(read, start, end, within)));
        }
        catch (System.Xml.XmlException e)
        {
            // Children of the body that markup compatibility chose from an alternative stand within it in the text.
            throw new NotSupportedException("The paragraph is not where it was read: it stands in an alternative of markup compatibility.", e);
        }
    }

    /// <summary>
    /// Where an element read from the stretch <paramref name="start"/> to <paramref name="end"/> (with the pieces
    /// <paramref name="within"/> it) stands in the stretch's text within the tags around it, or -1: by its line info, in
    /// the text of the piece it was read from or of the main part as read whole, moved on by the pieces before it.
    /// </summary>
    int Where(XElement read, int start, int end, List<Piece> within)
    {
        if (Top(read) is not { } top)
            return -1;
        int Moved(int before) => within.Where(p => p.End <= before).Sum(p => p.Text.Length - (p.End - p.Start));
        if (pieceOf.TryGetValue(top, out var piece))
        {
            var local = piece.Read.Start(read) - head.Length;
            return within.Contains(piece) && local >= 0 ? head.Length + piece.Start - start + Moved(piece.Start) + local : -1;
        }
        var at = wholeText.Start(read);
        return at >= start && at < end ? head.Length + at - start + Moved(at) : -1;
    }

    /// <summary>
    /// The children of the body overlapping the stretch <paramref name="start"/> to <paramref name="end"/> of the main
    /// part's text (as read whole), with the others read from the same pieces; where there are none, the child the
    /// stretch stands before; and the stretch they cover with it.
    /// </summary>
    (List<XElement> Old, XElement? Before, int Start, int End) Covering(int start, int end)
    {
        var children = body!.Elements().ToList();
        // From the last child that begins at or before the stretch (the first of its piece): it may reach into it.
        var (from, last) = (0, -1);
        for (var i = 0; i < children.Count; i++)
        {
            var at = pieceOf.TryGetValue(children[i], out var piece) ? piece.Start : wholeText.Start(children[i]);
            if (at > start)
                break;
            if (at > last)
                (from, last) = (i, at);
        }
        var old = new List<XElement>();
        for (var i = from; i < children.Count; i++)
        {
            if (RangeOf(children[i]) is not { } range || range.End <= start)
                continue;
            if (range.Start >= end)
                return (old, old.Count == 0 ? children[i] : null, start, end);
            old.Add(children[i]);
            (start, end) = (Math.Min(start, range.Start), Math.Max(end, range.End));
        }
        return (old, null, start, end);
    }

    /// <summary>The stretch of the main part's text (as read whole) a child of the body was read from: its piece's, else its own; null for one made after.</summary>
    (int Start, int End)? RangeOf(XElement child)
    {
        if (pieceOf.TryGetValue(child, out var piece))
            return (piece.Start, piece.End);
        var start = wholeText.Start(child);
        return start < 0 ? null : (start, wholeText.End(start));
    }

    /// <summary>The element before one among its siblings, white space aside.</summary>
    static XElement? Previous(XElement element)
    {
        for (var node = element.PreviousNode; node != null; node = node.PreviousNode)
        {
            if (node is XElement previous)
                return previous;
        }
        return null;
    }

    /// <summary>The element after one among its siblings, white space aside.</summary>
    static XElement? Next(XElement element)
    {
        for (var node = element.NextNode; node != null; node = node.NextNode)
        {
            if (node is XElement next)
                return next;
        }
        return null;
    }

    /// <summary>The child of the body an element stands in (itself, or one around it), or null for one outside it.</summary>
    XElement? Top(XElement element)
    {
        var top = element;
        while (top.Parent is { } parent && parent != body)
            top = parent;
        return top.Parent == body && body != null ? top : null;
    }

    static XElement Element(Block block) => block switch
    {
        ParagraphBlock paragraph => paragraph.Element,
        TableBlock table => table.Element,
        _ => throw new ArgumentOutOfRangeException(nameof(block)),
    };

    int IndexOf(Block block)
    {
        for (var i = 0; i < blocks.Count; i++)
        {
            if (ReferenceEquals(blocks[i], block))
                return i;
        }
        throw new InvalidOperationException("The block is not in the body.");
    }

    /// <summary>The list of blocks a paragraph stands in within a block of the body (the body's, a cell's, a text box's) and its place there.</summary>
    (List<Block> List, int Index)? ListOf(Block top, ParagraphBlock paragraph)
    {
        if (ReferenceEquals(top, paragraph))
            return (blocks, IndexOf(top));
        (List<Block>, int)? Search(Block block)
        {
            foreach (var list in ModelReader.Inner(block))
            {
                for (var k = 0; k < list.Count; k++)
                {
                    if (ReferenceEquals(list[k], paragraph))
                        return (list, k);
                    if (Search(list[k]) is { } found)
                        return found;
                }
            }
            return null;
        }
        return Search(top);
    }

    /// <summary>The main part's bytes with stretches of their text changed.</summary>
    byte[] Spliced(byte[] bytes, IReadOnlyList<Island> changed) =>
        changed.Count == 0 ? bytes : (ReferenceEquals(bytes, whole) ? wholeText : new PartText(bytes)).Spliced(changed.Select(i => (i.Start, i.End, i.Text)));

    /// <summary>
    /// Whether the headers and footers of the pages hold fields other than PAGE, which may read what changed elsewhere
    /// (NUMPAGES, SECTIONPAGES, REF, STYLEREF): their pages are then drawn anew after each change.
    /// </summary>
    bool StoriesRead(List<PageBox> pages) =>
        storiesRead ??= pages.SelectMany(p => new[] { p.Header, p.Footer }).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)
            .Any(part => context.Load(part).Root is { } root && root.Descendants().Any(e =>
                (e.Name == Wml.W + "instrText" ? e.Value : e.Name == Wml.W + "fldSimple" ? (string?)e.Attribute(Wml.W + "instr") : null) is { } instruction
                && instruction.Trim() is { Length: > 0 } code && !code.StartsWith('\\') && !(code.StartsWith("PAGE", StringComparison.OrdinalIgnoreCase) && (code.Length == 4 || !char.IsLetter(code[4])))));

    /// <summary>Reads the document whole as its package has it now: its body's blocks, numbered after the ids given before, and its paragraphs by id.</summary>
    void ReadWhole()
    {
        var last = reader?.LastId ?? 0;
        wholeReads++;
        context = new DocumentContext(package, fonts) { Culture = culture, Printed = printed, Markup = markup };
        budget = context.Text3DBudget;
        reader = new ModelReader(context) { LastId = last };
        blocks = reader.Body();
        whole = package.ReadBytes(context.MainPart);
        wholeText = new PartText(whole);
        body = context.Document.Root.Child("body");
        (head, tail) = ("", "");
        if (context.Document.Root is { } root && body != null && wholeText.Start(root) is var rootStart and >= 0 && wholeText.Start(body) is var bodyStart and >= 0
            && wholeText.TagEnd(bodyStart) is var bodyEnd && wholeText.Text[bodyEnd - 2] != '/')
        {
            var text = wholeText.Text;
            string Name(int at) => text[(at + 1)..text.IndexOfAny([' ', '\t', '\r', '\n', '/', '>'], at + 1)];
            (head, tail) = (text[rootStart..wholeText.TagEnd(rootStart)] + text[bodyStart..bodyEnd], $"</{Name(bodyStart)}></{Name(rootStart)}>");
        }
        islands = [];
        pieces.Clear();
        pieceOf.Clear();
        (laid, layout, paginator, renderer, scenes, storiesRead) = (null, null, null, null, null, null);
        Index();
    }

    /// <summary>The paragraphs of the body by id, each with the block of the body it stands in.</summary>
    void Index()
    {
        paragraphs.Clear();
        foreach (var block in blocks)
            Collect(block, block);
    }

    /// <summary>The paragraphs by id after some blocks of the body were read anew: those of the blocks gone forgotten, those of the new ones known.</summary>
    void Reindex(List<Block> before)
    {
        var (was, now) = (new HashSet<Block>(before, ReferenceEqualityComparer.Instance), new HashSet<Block>(blocks, ReferenceEqualityComparer.Instance));
        foreach (var block in before.Where(b => !now.Contains(b)))
            Forget(block);
        foreach (var block in blocks.Where(b => !was.Contains(b)))
            Collect(block, block);
    }

    void Collect(Block block, Block top)
    {
        if (block is ParagraphBlock paragraph)
            paragraphs.TryAdd(paragraph.Id, (paragraph, top));
        foreach (var list in ModelReader.Inner(block))
        {
            foreach (var inner in list)
                Collect(inner, top);
        }
    }

    void Forget(Block block)
    {
        if (block is ParagraphBlock paragraph && paragraphs.TryGetValue(paragraph.Id, out var known) && ReferenceEquals(known.Paragraph, paragraph))
            paragraphs.Remove(paragraph.Id);
        foreach (var list in ModelReader.Inner(block))
        {
            foreach (var inner in list)
                Forget(inner);
        }
    }
}
