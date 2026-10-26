using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.InteropServices;
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
public sealed class WordDocument : IEditableDocument
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
    /// The body's blocks as a version keeps them (see Version): the same as blocks, in a list that shares what a change
    /// left as it was with the list before it, so that each version costs what its change read anew, not all the blocks.
    /// </summary>
    ImmutableList<Block> kept = [];

    /// <summary>The blocks of the body the last change read anew (all of them where it read the document whole).</summary>
    List<Block> fresh = [];

    /// <summary>
    /// The main part's bytes as the document was read from them whole last, their text, its loaded body, and the start
    /// tags of its root and body with end tags for them, which stretches of the body are parsed within (see Stretch).
    /// </summary>
    byte[] whole = null!;
    PartText wholeText = null!;
    XElement? body;
    string head = "", tail = "";

    /// <summary>The stretches of the body changed since it was read whole, in order (see Island); the versions share what they have alike.</summary>
    ImmutableList<Island> islands = [];

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
    /// again from its checkpoints), the pages it gave, their scenes, the papers they name and the budget of 3D text each
    /// had and took (see DocumentContext.Text3DBudget); null before the first or after the document was read whole.
    /// </summary>
    List<Block>? laid;
    ParagraphLayout? layout;
    Paginator? paginator;
    PageRenderer? renderer;
    List<PageBox> shown = [];
    List<PageScene>? scenes;
    List<(int Paper, double Budget, double Cost)> drawnAs = [];

    /// <summary>
    /// How many pages drawn when they were asked for stay drawn (see Scene): those nearest the page asked for last, the
    /// others back to their sketches, to be drawn again when they are asked for again; so a long document scrolled
    /// through or written out keeps the drawings of so many pages, not of all of them.
    /// </summary>
    const int PagesKeptDrawn = 32;

    /// <summary>How many of the scenes are drawn (see PagesKeptDrawn).</summary>
    int drawnScenes;

    /// <summary>The budget of 3D text the document's pages are drawn with, from the first (see DocumentContext.Text3DBudget).</summary>
    double budget;

    /// <summary>A stretch of the main part's text as read whole (Start to End) and the text that stands there now.</summary>
    sealed record Island(int Start, int End, string Text)
    {
        /// <summary>The first of the islands (in order) a test holds for that holds for all after it too; their count where it holds for none.</summary>
        public static int First(ImmutableList<Island> islands, Func<Island, bool> holds)
        {
            var (low, high) = (0, islands.Count);
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (holds(islands[middle]))
                    high = middle;
                else
                    low = middle + 1;
            }
            return low;
        }

        /// <summary>The stretches two versions changed otherwise: those of either the other has not, alike in place and text.</summary>
        public static List<Island> Differing(ImmutableList<Island> one, ImmutableList<Island> other)
        {
            var differ = new List<Island>();
            for (var (i, j) = (0, 0); i < one.Count || j < other.Count;)
            {
                var (a, b) = (i < one.Count ? one[i] : null, j < other.Count ? other[j] : null);
                if (a != null && b != null && a.Start == b.Start)
                {
                    if (!a.Equals(b))
                        differ.AddRange([a, b]);
                    (i, j) = (i + 1, j + 1);
                }
                else if (a != null && (b == null || a.Start < b.Start))
                {
                    differ.Add(a);
                    i++;
                }
                else
                {
                    differ.Add(b!);
                    j++;
                }
            }
            return differ;
        }
    }

    /// <summary>
    /// A stretch of the main part's text as read whole (Start to End) that the body read anew from Text: the children of
    /// the body read from it, one after the other, whose line info is where they stand in Read, the text within the start
    /// and end tags around it.
    /// </summary>
    sealed class Piece(int start, int end, string text, PartText read, IReadOnlyList<XElement> children)
    {
        public int Start { get; } = start;
        public int End { get; } = end;
        public string Text { get; } = text;
        public PartText Read { get; } = read;
        public IReadOnlyList<XElement> Children { get; } = children;
    }

    /// <summary>
    /// A version of the document: the main part's bytes it was read from whole, the stretches of its body changed since
    /// and its body's blocks (see kept), whose ids the blocks read for it again take.
    /// </summary>
    sealed record Version(byte[] Whole, ImmutableList<Island> Islands, ImmutableList<Block> Blocks);

    WordDocument(OpcPackage package, FontLocator? fonts, CultureInfo culture, DateTime? printed, bool markup)
    {
        (this.package, this.fonts, this.culture, this.printed, this.markup) = (package, fonts, culture, printed, markup);
        ReadWhole(portions: true);
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
        Open(File.ReadAllBytes(path), password, fonts, culture, printed, markup);

    /// <summary>Opens a Word document from its bytes, as Open opens a file.</summary>
    /// <exception cref="PasswordException">The document needs a password, or the password is wrong.</exception>
    public static WordDocument Open(byte[] bytes, string? password = null, FontLocator? fonts = null, CultureInfo? culture = null, DateTime? printed = null,
        bool markup = false) =>
        new(OpcPackage.Open(bytes, password), fonts, culture ?? CultureInfo.CurrentCulture, printed, markup);

    /// <summary>
    /// The author whose changes are tracked changes (Word's Track Changes: see Docx.Tracking), or null: changes are made
    /// as they are; and the time they carry, now where null.
    /// </summary>
    public string? Author { get; set; }
    public DateTime? ChangedAt { get; set; }

    /// <summary>The id the next tracked change takes: one more than the greatest id the main part's elements have.</summary>
    int changeId = -1;

    /// <summary>The body's blocks as the document reads now.</summary>
    public IReadOnlyList<Block> Blocks
    {
        get
        {
            Read();
            return blocks;
        }
    }

    /// <summary>A paragraph of the body (of its tables and text boxes too) by its id, or null.</summary>
    public ParagraphBlock? Paragraph(int id)
    {
        Read();
        return paragraphs.TryGetValue(id, out var found) ? found.Paragraph : null;
    }

    /// <summary>The text of a paragraph (see ParagraphText).</summary>
    public string Text(int paragraph) => ParagraphText.Of(Find(paragraph).Paragraph);

    /// <summary>Whether a paragraph of that id is the body's (in tables and text boxes too), which the document changes.</summary>
    public bool Contains(int paragraph)
    {
        Read();
        return paragraphs.ContainsKey(paragraph);
    }

    /// <summary>The word a place stands inside, as Word's formatting buttons take it (see Editing.Words).</summary>
    public (int Offset, int Length)? WordAt(int paragraph, int offset)
    {
        var p = Find(paragraph).Paragraph;
        var text = ParagraphText.Of(p);
        return Editing.Words.Around(text, offset - p.TextOffset) is var (at, length) ? (at + p.TextOffset, length) : null;
    }

    /// <summary>Whether a change can be undone, and one undone done again.</summary>
    public bool CanUndo => version > 0;
    public bool CanRedo => version + 1 < versions.Count;

    /// <summary>How many changes the document holds as it reads now: 0 as opened, one more for each change made, one less for each undone.</summary>
    public int Changes => version;

    /// <summary>
    /// The paragraph just before or after a paragraph in its body, table cell or text box, or null where none is (at its
    /// edge, or where a table stands between).
    /// </summary>
    public int? Before(int paragraph) => Beside(paragraph, -1);
    public int? After(int paragraph) => Beside(paragraph, 1);

    int? Beside(int paragraph, int step)
    {
        var (p, top) = Find(paragraph);
        return ListOf(top, p, walking: true) is var (list, index) && index + step >= 0 && index + step < list.Count && list[index + step] is ParagraphBlock beside
            ? beside.Id
            : null;
    }

    /// <summary>
    /// Replaces the text of a paragraph from a place, so many characters, by other text (without breaks): it takes the
    /// format of the text before it.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The range holds what cannot be changed so (see TextEdits), or the paragraph would not read so afterwards (the place
    /// is in what the document computes, such as a table of contents, or keeps, such as an object); nothing changes.
    /// </exception>
    public void Replace(int paragraph, int offset, int length, string text)
    {
        var (p, top) = Find(paragraph);
        var expected = markup ? null : ParagraphText.Of(p) is var now && offset - p.TextOffset is var at and >= 0 && at + length <= now.Length
            ? now.Remove(at, length).Insert(at, text) : null;
        Change(top, top, (stretch, tracking) => TextEdits.Replace(stretch, p, offset, length, text, tracking));
        if (expected != null && !Reads(paragraph, expected))
            throw Refused("The paragraph would not read so: the place is in what the document computes or keeps.");
    }

    /// <summary>The paragraphs the last change read anew (see fresh), of tables and text boxes too.</summary>
    IEnumerable<ParagraphBlock> Fresh()
    {
        IEnumerable<ParagraphBlock> Within(Block block) =>
            (block is ParagraphBlock paragraph ? [paragraph] : Enumerable.Empty<ParagraphBlock>()).Concat(ModelReader.Inner(block).SelectMany(list => list).SelectMany(Within));
        return fresh.SelectMany(Within);
    }

    /// <summary>Whether a paragraph of the body reads so (as all its tracked changes accepted).</summary>
    bool Reads(int paragraph, string text) => paragraphs.TryGetValue(paragraph, out var found) && ParagraphText.Of(found.Paragraph) == text;

    /// <summary>The change just made taken back and forgotten, as if it had not been made: a refusal of it.</summary>
    NotSupportedException Refused(string why)
    {
        Go(version - 1);
        versions.RemoveRange(version + 1, versions.Count - version - 1);
        return new NotSupportedException(why);
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
        var before = markup ? null : ParagraphText.Of(p);
        var second = 0;
        var where = Change(top, top, (stretch, tracking) =>
        {
            var (text, at) = TextEdits.Split(stretch, p, offset, tracking);
            second = at;
            return text;
        });
        bool Second(XElement? element) => element != null && where(element) == second;
        // The second half may be a part of a paragraph the reader joins (a mark deleted or hidden).
        var id = Fresh().FirstOrDefault(p => (p.Parts ?? [p.Element]).Any(Second))?.Id ?? paragraph;
        // Two paragraphs that read as the halves, the second where the place was.
        var at = offset - p.TextOffset;
        if (before != null && at >= 0 && at <= before.Length && !(Reads(id, before[at..]) && Before(id) is { } first && Reads(first, before[..at])))
            throw Refused("The paragraph would not split so: the place is in what the document computes or keeps.");
        return id;
    }

    /// <summary>
    /// Joins a paragraph with the paragraph after it in its body, cell or text box, as Delete at its end does (see
    /// TextEdits.Join); returns the id of the joined paragraph, which stands where the first did.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// No paragraph follows it there, or the two cannot be joined so, or the joined paragraph would not read as the two
    /// one after the other; nothing changes.
    /// </exception>
    public int Join(int paragraph)
    {
        var (p, top) = Find(paragraph);
        var (list, index) = ListOf(top, p) ?? throw new ArgumentException($"The document's body has no paragraph {paragraph}.", nameof(paragraph));
        if (index + 1 >= list.Count || list[index + 1] is not ParagraphBlock next)
            throw new NotSupportedException("No paragraph follows the paragraph in its body, cell or text box.");
        var both = markup ? null : ParagraphText.Of(p) + ParagraphText.Of(next);
        var (first, start) = (true, -1);
        var where = Change(top, ReferenceEquals(list, blocks) ? next : top, (stretch, tracking) =>
        {
            start = stretch.Find(First(p)) is { } element ? stretch.Text.Start(element) - stretch.Head : -1;
            var (text, stays) = TextEdits.Join(stretch, p, next, tracking);
            first = stays;
            return text;
        });
        // The joined paragraph begins where the first began (its id goes by its text and place, see ModelReader.Keep).
        var id = Fresh().FirstOrDefault(p => start >= 0 && where(First(p)) == start)?.Id ?? (first ? paragraph : next.Id);
        if (both != null && !Reads(id, both))
            throw Refused("The paragraphs would not join so: one holds what the document computes or keeps.");
        return id;
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
        Read();
        if (!CanUndo)
            return false;
        Go(version - 1);
        return true;
    }

    /// <summary>Does the last change undone again; false where there is none.</summary>
    public bool Redo()
    {
        Read();
        if (!CanRedo)
            return false;
        Go(version + 1);
        return true;
    }

    /// <summary>
    /// How many pages after the one a change begins on are laid out with it before the rest is left as it was, to be laid
    /// out by LayOut (see LayingOut).
    /// </summary>
    public const int PagesAtOnce = 3;

    /// <summary>
    /// The pages of the document as it reads now, laid out and drawn as DocxConverter does (see PageScene): at first so
    /// many pages (PagesAtOnce), the rest laid out by LayOut (see LayingOut); after a change laid out again from the last
    /// checkpoint before it, the pages the paginator gives as it gave them kept as they were drawn (see
    /// Paginator.Relayout); after a change that moves what follows on, so many pages, the rest as they were until LayOut
    /// lays them out.
    /// </summary>
    public IReadOnlyList<PageScene> Scenes()
    {
        if (reading)
            return Early();
        if (early)
            return scenes!;
        if (scenes != null && ReferenceEquals(laid, blocks))
            return scenes;
        List<PageBox> pages;
        if (paginator == null || layout == null || renderer == null)
        {
            layout = new ParagraphLayout(context, new RunStyles(context));
            // Notes are read after the body: their numbers come from the order of their references.
            var footnotes = reader.Notes(endnotes: false);
            var endnoteParts = reader.Notes(endnotes: true);
            var endnotes = endnoteParts.Where(n => n.Key is not ("separator" or "continuationSeparator" or "continuationNotice")).OrderBy(n => reader.EndnoteNumber(n.Key))
                .Where(n => reader.EndnoteNumber(n.Key) > 0).Select(n => (reader.EndnoteSection(n.Key), n.Value)).ToList();
            paginator = new Paginator(context, layout, reader) { EndnoteContinuation = endnoteParts.GetValueOrDefault("continuationSeparator"), KeepCheckpoints = true };
            pages = paginator.Paginate(blocks, reader.LastSection(), footnotes, endnotes, endnoteParts.GetValueOrDefault("separator"), PagesAtOnce);
            renderer = new PageRenderer(context, new Drawings(context, culture), new TableLayout(context, layout));
            scenes = null;
        }
        else
            pages = paginator.Relayout(blocks, PagesAtOnce).Pages;
        Draw(pages);
        laid = blocks;
        return scenes!;
    }

    /// <summary>
    /// Whether pages after those the last change laid out still stand as they did before it, or are not laid out yet where
    /// the document's layout grows from its first pages (see LayOut).
    /// </summary>
    public bool LayingOut => reading || early || paginator?.Paused == true || !ReferenceEquals(laid, blocks);

    /// <summary>
    /// Lays out about so many more of the pages the last change left as they were (see LayingOut), and draws those that
    /// come out otherwise; whether more are left. Scenes gives the pages as they then stand.
    /// </summary>
    public bool LayOut(int pages = PagesAtOnce)
    {
        // A long body is read bit by bit first, then laid out from its first page (see reading).
        if (reading)
        {
            var many = pages == int.MaxValue ? int.MaxValue : childrenAtOnce;
            if (context.Loading)
            {
                if (!context.LoadBody(many))
                    blocks = reader.BeginBody();
            }
            else if (!reader.ReadBody(many))
                Done();
            return LayingOut;
        }
        if (early)
        {
            Leave();
            Scenes();
            return LayingOut;
        }
        if (scenes == null || !ReferenceEquals(laid, blocks))
            Scenes();
        else if (paginator?.Continue(pages) is { } relaid)
            Draw(relaid.Pages);
        return LayingOut;
    }

    /// <summary>
    /// The scenes of the pages laid out: a page the paginator gives as it gave it before is kept as it was drawn, where the
    /// pages before it took the same budget of 3D text; the others drawn, or left to be drawn by Scene (see drawLater).
    /// </summary>
    void Draw(List<PageBox> pages)
    {
        renderer!.Settings = paginator!.Settings;
        var sizes = new List<(double Width, double Height)>();
        var drawn = new List<PageScene>(pages.Count);
        var how = new List<(int Paper, double Budget, double Cost)>(pages.Count);
        // The pages take the budget of 3D text in their order, as a pass over all of them gives it: a page drawn before is
        // kept where it had the same.
        context.Text3DBudget = budget;
        for (var k = 0; k < pages.Count; k++)
        {
            var page = pages[k];
            var (width, height) = (page.PaperWidth, page.PaperHeight);
            var paper = 0;
            while (paper < sizes.Count && !(Math.Abs(sizes[paper].Width - width) < 0.01 && Math.Abs(sizes[paper].Height - height) < 0.01))
                paper++;
            if (paper == sizes.Count)
                sizes.Add((width, height));
            var left = context.Text3DBudget;
            if (scenes != null && k < scenes.Count && k < shown.Count && ReferenceEquals(page, shown[k]) && drawnAs[k].Paper == paper && drawnAs[k].Budget == left)
            {
                drawn.Add(scenes[k]);
                context.Text3DBudget -= drawnAs[k].Cost;
            }
            else
                drawn.Add(drawLater ? renderer.Sketch(page, $"paper{paper}") : renderer.Scene(page, $"paper{paper}"));
            how.Add((paper, left, left - context.Text3DBudget));
        }
        (scenes, drawnAs, shown) = (drawn, how, pages);
        drawnScenes = drawn.Count(scene => scene.Drawn);
    }

    /// <summary>
    /// Whether pages are drawn when they are asked for (see Scene) rather than as they are laid out: unless the document
    /// shows its markup, whose pane widens a page as far as its comments reach, or a part holds 3D text (w14:props3d),
    /// which takes a budget the pages share in their order (DocumentContext.Text3DBudget).
    /// </summary>
    bool drawLater;

    /// <summary>The page the layout puts a bookmark on (0-based), of those laid out so far; null where none has it.</summary>
    public int? PageOf(string bookmark)
    {
        Scenes();
        var page = shown.FindIndex(p => p.Bookmarks.Any(b => b.Name == bookmark));
        return page >= 0 ? page : null;
    }

    public PageScene Scene(int page)
    {
        var all = Scenes();
        if (all[page] is { Drawn: false } sketch)
        {
            renderer!.Settings = paginator!.Settings;
            context.Text3DBudget = budget;
            scenes![page] = renderer.Scene(shown[page], sketch.Paper);
            if (++drawnScenes > PagesKeptDrawn)
            {
                // The drawn page farthest from this one goes back to its sketch.
                var far = -1;
                for (var k = 0; k < scenes.Count; k++)
                {
                    if (k != page && scenes[k].Drawn && (far < 0 || Math.Abs(k - page) > Math.Abs(far - page)))
                        far = k;
                }
                if (far >= 0)
                {
                    scenes[far] = renderer.Sketch(shown[far], scenes[far].Paper);
                    drawnScenes--;
                }
            }
        }
        return scenes![page];
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

    (ParagraphBlock Paragraph, Block Top) Find(int id)
    {
        Read();
        return paragraphs.TryGetValue(id, out var found) ? found : throw new ArgumentException($"The document's body has no paragraph {id}.", nameof(id));
    }

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
        // The stretches changed before but those within this one, which takes their place where its text differs.
        var (first, after) = (Island.First(islands, i => i.End > start), Island.First(islands, i => i.Start >= end));
        var changed = islands.RemoveRange(first, after - first);
        if (text != wholeText.Text[start..end])
            changed = changed.Insert(first, new Island(start, end, text));
        var where = Apply(start, end, text, old, null, changed, null);
        versions.RemoveRange(version + 1, versions.Count - version - 1);
        versions.Add(new Version(whole, islands, kept));
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
        if (Island.Differing(islands, to.Islands) is { Count: > 0 } differ)
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
        versions[target] = new Version(whole, islands, kept);
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
    Func<XElement, int> Apply(int start, int end, string text, List<XElement> old, XElement? before, ImmutableList<Island> changed, IReadOnlyList<Block>? like)
    {
        var was = blocks;
        var read = new PartText(head + text + tail);
        var xml = XDocument.Parse(read.Text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        var children = xml.Root?.Elements().FirstOrDefault() is { } parent ? context.Prepare(parent) : [];
        if (head.Length > 0 && reader.Reread(blocks, old, before, children, like) is { } result)
        {
            var piece = new Piece(start, end, text, read, children);
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
            versions[v] = versions[v] with { Whole = whole, Islands = ImmutableList.CreateRange(rebased) };
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
        if (head.Length == 0 || Top(First(blocks[first])) is not { } a || Top(Last(blocks[last])) is not { } b || RangeOf(a) is not { } from
            || RangeOf(b) is not { } to)
            throw new NotSupportedException("The paragraph is not where it was read.");
        var (start, end) = (from.Start, to.End);
        // The children of the pieces they were read from, whole.
        if (pieceOf.TryGetValue(a, out var firstPiece))
            a = firstPiece.Children[0];
        if (pieceOf.TryGetValue(b, out var lastPiece))
            b = lastPiece.Children[^1];
        var old = new List<XElement>();
        for (var e = a; e != null; e = Next(e))
        {
            old.Add(e);
            if (e == b)
                break;
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

    /// <summary>The first and the last element a block was read from: a paragraph's parts where the reader joined several (see ParagraphBlock.Parts).</summary>
    static XElement First(Block block) => block switch
    {
        ParagraphBlock paragraph => paragraph.Parts?[0] ?? paragraph.Element,
        TableBlock table => table.Element,
        _ => throw new ArgumentOutOfRangeException(nameof(block)),
    };

    static XElement Last(Block block) => block switch
    {
        ParagraphBlock paragraph => paragraph.Parts?[^1] ?? paragraph.Element,
        TableBlock table => table.Element,
        _ => throw new ArgumentOutOfRangeException(nameof(block)),
    };

    /// <summary>
    /// Where each block of the body stands in blocks (see IndexOf), and the blocks it was worked out for: worked out where
    /// the paragraphs are walked one after the other (a selection over many of them), not for each change.
    /// </summary>
    Dictionary<Block, int>? places;
    List<Block>? placed;

    int IndexOf(Block block, bool walking = false)
    {
        if (walking && !(ReferenceEquals(placed, blocks) && places!.Count == blocks.Count))
        {
            places = new Dictionary<Block, int>(blocks.Count, ReferenceEqualityComparer.Instance);
            for (var k = 0; k < blocks.Count; k++)
                places[blocks[k]] = k;
            placed = blocks;
        }
        if (ReferenceEquals(placed, blocks) && places!.Count == blocks.Count)
            return places.TryGetValue(block, out var at) ? at : throw new InvalidOperationException("The block is not in the body.");
        for (var i = 0; i < blocks.Count; i++)
        {
            if (ReferenceEquals(blocks[i], block))
                return i;
        }
        throw new InvalidOperationException("The block is not in the body.");
    }

    /// <summary>The list of blocks a paragraph stands in within a block of the body (the body's, a cell's, a text box's) and its place there.</summary>
    (List<Block> List, int Index)? ListOf(Block top, ParagraphBlock paragraph, bool walking = false)
    {
        if (ReferenceEquals(top, paragraph))
            return (blocks, IndexOf(top, walking));
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
    /// Whether the body is being read bit by bit (see LayOut): a long body when the document opens, from its children
    /// read so far laid out as the first pages (see Early); the rest read before anything needs it whole (see Read).
    /// </summary>
    bool reading;

    /// <summary>How many children of a long body LayOut reads at once, and the first pages are laid out from while it reads.</summary>
    static int childrenAtOnce = 1000;

    /// <summary>
    /// The rest of the body read where it is read bit by bit (see reading): its XML, then its blocks; the first pages laid
    /// out meanwhile given up (see early).
    /// </summary>
    void Read()
    {
        if (reading)
        {
            if (context.Loading)
            {
                context.LoadBody(int.MaxValue);
                blocks = reader.BeginBody();
            }
            while (reader.ReadBody(int.MaxValue))
            {
            }
            Done();
        }
        Leave();
    }

    /// <summary>
    /// Whether the pages are still the first ones laid out while the body was read (see Early): until LayOut lays the
    /// body out from its first page, the slice after the one that read its end.
    /// </summary>
    bool early;

    /// <summary>The first pages laid out while the body was read given up: the next Scenes lays the body out from its first page.</summary>
    void Leave()
    {
        if (!early)
            return;
        early = false;
        (laid, layout, paginator, renderer) = (null, null, null, null);
    }

    /// <summary>
    /// The body read to its end: its paragraphs found by id, its pages to be laid out anew from the first (the first ones
    /// were laid out from what was read first, see early).
    /// </summary>
    void Done()
    {
        reading = false;
        early = scenes != null;
        if (!early)
            (laid, layout, paginator, renderer) = (null, null, null, null);
        Index();
        // The version the document opened as.
        if (versions.Count == 0)
            versions.Add(new Version(whole, islands, kept));
    }

    /// <summary>
    /// The first pages while the body is being read (see reading): laid out from the first children of the body by a
    /// reader of their own, which numbers their blocks as the body's reader does, so many pages (PagesAtOnce), without
    /// notes (whose numbers follow all of the body).
    /// </summary>
    IReadOnlyList<PageScene> Early()
    {
        if (scenes != null)
            return scenes;
        var own = new ModelReader(context);
        var first = own.BeginBody(childrenAtOnce);
        own.ReadBody(int.MaxValue);
        layout = new ParagraphLayout(context, new RunStyles(context));
        paginator = new Paginator(context, layout, own);
        renderer = new PageRenderer(context, new Drawings(context, culture), new TableLayout(context, layout));
        Draw(paginator.Paginate(first, own.ReadingSection(), pages: PagesAtOnce));
        return scenes!;
    }

    /// <summary>
    /// Reads the document whole as its package has it now: its body's blocks, numbered after the ids given before, and its
    /// paragraphs by id; a long body bit by bit where asked (see reading).
    /// </summary>
    void ReadWhole(bool portions = false)
    {
        var last = reader?.LastId ?? 0;
        wholeReads++;
        // The main part's bytes kept by the package, so that the context reads its XML from them: unpacked once.
        if (package.FirstRelationship("/", "officeDocument")?.Target is { } main && package.Exists(main))
            package.ReadBytes(main, keep: true);
        context = new DocumentContext(package, fonts, bodyBitByBit: portions) { Culture = culture, Printed = printed, Markup = markup };
        budget = context.Text3DBudget;
        reader = new ModelReader(context) { LastId = last };
        whole = package.ReadBytes(context.MainPart);
        wholeText = context.MainText;
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
        (laid, layout, paginator, renderer, scenes, shown) = (null, null, null, null, null, []);
        drawLater = !markup && !package.PartNames.Any(part => part.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            && (part == context.MainPart ? whole : package.ReadBytes(part)).AsSpan().IndexOf("props3d"u8) >= 0);
        // A long body is read bit by bit (see reading): its XML, then its blocks; a short one at once.
        reading = true;
        if (!context.LoadBody(2 * childrenAtOnce))
        {
            blocks = reader.BeginBody();
            if (!portions || reader.BodyChildren <= 2 * childrenAtOnce)
                Read();
        }
    }

    /// <summary>The paragraphs of the body by id, each with the block of the body it stands in, and the blocks as the versions keep them.</summary>
    void Index()
    {
        paragraphs.Clear();
        foreach (var block in blocks)
            Collect(block, block);
        fresh = blocks;
        (kept, places, placed) = (ImmutableList.CreateRange(blocks), null, null);
    }

    /// <summary>
    /// After some blocks of the body were read anew (see Differing): the paragraphs by id, those of the blocks gone
    /// forgotten (by the layout too, which is not asked to lay them out again), those of the new ones known; and the
    /// blocks as the versions keep them, the new ones in place of those gone.
    /// </summary>
    void Reindex(List<Block> before)
    {
        var was = CollectionsMarshal.AsSpan(before);
        var now = CollectionsMarshal.AsSpan(blocks);
        var differing = Differing(was, now);
        var gone = new HashSet<ParagraphBlock>(ReferenceEqualityComparer.Instance);
        foreach (var (at, count, _, _) in differing)
        {
            foreach (var block in was.Slice(at, count))
                Forget(block, gone);
        }
        // The stretches from the last: the places of those before it stay as they were.
        var list = kept;
        foreach (var (at, count, from, length) in differing)
        {
            if (count == length)
            {
                for (var k = 0; k < count; k++)
                    list = list.SetItem(at + k, now[from + k]);
            }
            else
                list = list.RemoveRange(at, count).InsertRange(at, now.Slice(from, length).ToArray());
        }
        var read = new List<Block>();
        for (var k = differing.Count - 1; k >= 0; k--)
        {
            foreach (var block in now.Slice(differing[k].From, differing[k].Length))
            {
                Collect(block, block);
                read.Add(block);
            }
        }
        (kept, fresh, places, placed) = (list, read, null, null);
        layout?.Forget(gone);
    }

    /// <summary>
    /// Where the body's blocks after a change differ from those before it: stretches of blocks gone (their place and count
    /// before) with the blocks read in their place (their place and count now), from the last to the first. The blocks
    /// stand as they stood, the same objects, where the change did not read them anew: before the stretch it changed, and
    /// after it but for blocks read anew one for one (paragraphs whose numbers it changed), so many places on as the blocks
    /// it read outnumber those gone.
    /// </summary>
    static List<(int At, int Count, int From, int Length)> Differing(ReadOnlySpan<Block> was, ReadOnlySpan<Block> now)
    {
        var head = 0;
        while (head < was.Length && head < now.Length && ReferenceEquals(was[head], now[head]))
            head++;
        var differing = new List<(int At, int Count, int From, int Length)>();
        // From the end, the blocks before and after side by side, until those of one reach the head.
        var (i, j) = (was.Length, now.Length);
        while (i > head && j > head)
        {
            if (ReferenceEquals(was[i - 1], now[j - 1]))
            {
                (i, j) = (i - 1, j - 1);
                continue;
            }
            var (endI, endJ) = (i, j);
            while (i > head && j > head && !ReferenceEquals(was[i - 1], now[j - 1]))
                (i, j) = (i - 1, j - 1);
            differing.Add((i, endI - i, j, endJ - j));
        }
        // What the one has more than the other goes with the stretch that begins there (the change's own).
        if (i > head || j > head)
        {
            if (differing.Count > 0 && differing[^1].At == i && differing[^1].From == j)
                differing[^1] = (head, differing[^1].At + differing[^1].Count - head, head, differing[^1].From + differing[^1].Length - head);
            else
                differing.Add((head, i - head, head, j - head));
        }
        return differing;
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

    /// <summary>The paragraphs of a block gone (itself, those of its tables and text boxes) forgotten by id, and added to those gone.</summary>
    void Forget(Block block, HashSet<ParagraphBlock> gone)
    {
        if (block is ParagraphBlock paragraph)
        {
            gone.Add(paragraph);
            if (paragraphs.TryGetValue(paragraph.Id, out var known) && ReferenceEquals(known.Paragraph, paragraph))
                paragraphs.Remove(paragraph.Id);
        }
        foreach (var list in ModelReader.Inner(block))
        {
            foreach (var inner in list)
                Forget(inner, gone);
        }
    }
}
