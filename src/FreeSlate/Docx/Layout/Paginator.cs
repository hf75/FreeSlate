using System.Xml.Linq;
using FreeSlate.Drawing;

namespace FreeSlate.Docx.Layout;

/// <summary>
/// A line placed on a page: its left edge (the column's) and its top, in points from the page's top left; a line of
/// rotated text (table cells, catalog: table.cells) turns by <paramref name="Angle"/> degrees clockwise about a point
/// given relative to its left edge and top.
/// </summary>
public sealed record PlacedLine(Line Line, double Left, double Top, double Angle = 0, double PivotX = 0, double PivotY = 0)
{
    /// <summary>Where inline VML shapes hang from (see Vml.Hangs): a paragraph's first line's top with its space before.</summary>
    public double? HangTop { get; init; }

    /// <summary>
    /// Where the line's inline shapes and groups are drawn instead of their place in it: in a frame of a document in
    /// an older compatibility mode, the frame's left edge, as far below the page's top as the frame stands below its
    /// paragraph's place in the text (see Paginator.PlaceFrame); pictures stay where they stand.
    /// </summary>
    public (double X, double Y)? InlineOrigin { get; init; }

    /// <summary>A line of the body that line numbering counts (not in tables, notes or suppressed paragraphs).</summary>
    public bool Numbered { get; init; }

    /// <summary>A line of a header or footer, whose inline drawings lie behind the body's text (PageRenderer).</summary>
    public bool Story { get; init; }

    /// <summary>The left inset of the text box the line stands in, which decides where its edge's half dots go (PageRenderer.OnDots).</summary>
    public double? BoxInset { get; init; }

    /// <summary>How far the line was moved across to start on a dot of Word's device (PageRenderer.OnDots).</summary>
    public double DeviceShift { get; init; }

    /// <summary>
    /// The box of the turned frame (w:framePr with w:textDirection tbRl or btLr) the line stands in, its top from the
    /// line's, from which PageRenderer puts the line on Word's device as a turned text box's (TurnedOnDots).
    /// </summary>
    public (double Left, double Width, double TopOffset, double Height)? TurnedFrame { get; init; }

    /// <summary>The space after the line's paragraph where the line ends it, in table cells (RowBox.Split).</summary>
    public double After { get; init; }

    /// <summary>Whether the line is an empty paragraph's that only ends its section (w:sectPr), which aligning the page's text leaves out.</summary>
    public bool SectionMark { get; init; }

    /// <summary>Whether the line's paragraph has widow control, in table cells (RowBox.Split).</summary>
    public bool Widow { get; init; }

    /// <summary>
    /// The room above a nested table's cell content the line stands in (the row's line and the cell's top margin, of
    /// every table it is nested in), which the cell's continuation keeps where its row is cut at the line (RowBox.Split).
    /// </summary>
    public double Carry { get; init; }

    /// <summary>
    /// The bottom margins of the nested tables' cells the line stands in and the lines below their rows, which a cut at
    /// the line leaves room for (RowBox.Split).
    /// </summary>
    public double CarryBelow { get; init; }

    /// <summary>
    /// The block of the cell's content (from 1) that is the nested table the line stands in, 0 for a line of the cell's
    /// own text (RowBox.Split).
    /// </summary>
    public int Nest { get; init; }

    /// <summary>
    /// What the space before a paragraph's first line has more than the space after the paragraph above it, in table
    /// cells, which the line keeps at the top of a split row's continuation (RowBox.Split).
    /// </summary>
    public double Lead { get; init; }

    /// <summary>Whether the line ends a paragraph kept with the next one, in table cells from Word 2013 on (RowBox.Split).</summary>
    public bool Keep { get; init; }

    /// <summary>
    /// How far below the line's top the part of a nested table it stands in reaches that has to go with it where a row
    /// splits: its row's at-least height, the whole row where it may not split (RowBox.Split).
    /// </summary>
    public double UnitHeight { get; init; }

    /// <summary>Whether the line is a turned text box's that stands on Word's device (see PageRenderer.TurnedOnDots).</summary>
    public bool DeviceTurned { get; init; }
}

/// <summary>A page of the laid-out document.</summary>
public sealed class PageBox
{
    public int Number { get; init; }
    public required SectionProperties Section { get; init; }

    /// <summary>The footnotes that begin on the page, in the order of their references.</summary>
    public List<string> Footnotes { get; } = [];

    /// <summary>The body's paragraphs that begin on the page, in their order (for STYLEREF and PAGEREF).</summary>
    public List<ParagraphBlock> Paragraphs { get; } = [];

    /// <summary>
    /// Empty paragraphs ending a section that the page began with before the next section took it (see Place), which
    /// a header's STYLEREF met on the way (FieldEvaluator).
    /// </summary>
    public List<ParagraphBlock> Passed { get; } = [];

    /// <summary>The number of the last chapter heading on the page or before it (section.pageNumbers), or "".</summary>
    public string Chapter { get; set; } = "";

    /// <summary>What PAGE fields put before the page number: the chapter number and its separator, or "".</summary>
    public string ChapterPrefix => Section.ChapterLevel != null && Chapter.Length > 0 ? Chapter + Section.ChapterSeparator : "";

    /// <summary>The height of the footnotes of a page of several columns, which their text must leave free.</summary>
    public double NoteBand { get; set; }
    public List<PlacedLine> Lines { get; } = [];

    /// <summary>The page's number as PAGE fields show it (w:pgNumType restarts it; catalog: section.pageNumbers).</summary>
    public int DisplayNumber { get; init; }

    /// <summary>The index of the page's section and whether the page is the section's first.</summary>
    public int SectionIndex { get; init; }
    public bool FirstOfSection { get; init; }

    /// <summary>The header and footer parts of the page, or null (catalog: headerFooter.parts).</summary>
    public string? Header { get; init; }
    public string? Footer { get; init; }

    /// <summary>Where the body begins and ends: at the margins, or below a header and above a footer that reach past them.</summary>
    public double BodyTop { get; set; }
    public double BodyBottom { get; set; }

    /// <summary>The left edge and the width of the text area (mirrored margins and the gutter placed).</summary>
    public double TextLeft { get; init; }
    public double TextWidth { get; init; }

    /// <summary>A page left blank so that the next section starts on an odd or even page.</summary>
    public bool Blank { get; init; }

    /// <summary>The space after the paragraph that ends on this page, which vertical alignment counts.</summary>
    public double TrailingSpace { get; set; }

    /// <summary>Lines between columns (w:cols/@sep): x, top and bottom in points.</summary>
    public List<(double X, double Top, double Bottom)> ColumnLines { get; } = [];

    /// <summary>Table cell shading, table borders and dotted, dashed or diagonal lines, drawn in this order below the text.</summary>
    public List<FillRect> Shading { get; } = [];
    public List<FillRect> Borders { get; } = [];
    public List<Stroke> Strokes { get; } = [];

    /// <summary>Floating drawings anchored on the page (catalog: drawing.anchor).</summary>
    public List<PlacedDrawing> Drawings { get; } = [];

    /// <summary>The areas floating tables keep free of text on the page (catalog: table.floating).</summary>
    public List<Paginator.Exclusion> FloatingTables { get; } = [];

    /// <summary>The page's border (catalog: section.pageBorders), or null.</summary>
    public PageBorderBox? Border { get; set; }

    /// <summary>Line numbers (catalog: section.lineNumbers): the number, its style, where it ends and its baseline.</summary>
    public List<(string Text, RunStyle Style, double Right, double Baseline)> LineNumbers { get; } = [];

    /// <summary>Bookmarks that start on the page: their names and where their paragraph begins (catalog: field.bookmarks).</summary>
    public List<(string Name, double X, double Y)> Bookmarks { get; } = [];

    /// <summary>
    /// Alternative texts of tables on the page (w:tblCaption, w:tblDescription, catalog: table.accessibility) with the
    /// part of the table the page holds.
    /// </summary>
    public List<(string Text, double X, double Y, double Width, double Height)> Alternatives { get; } = [];

    public double Width => Section.PageWidth;
    public double Height => Section.PageHeight;

    /// <summary>
    /// A page of vertical text is laid out turned a quarter (its <see cref="Section"/> turned): this is the page as
    /// printed, with what stands upright on it (header, footer and page border); null on other pages.
    /// </summary>
    public PageBox? Upright { get; set; }

    /// <summary>The size of the page as printed.</summary>
    public double PaperWidth => (Section.Upright ?? Section).PageWidth;
    public double PaperHeight => (Section.Upright ?? Section).PageHeight;

    /// <summary>
    /// The page as it stands with lists of its own, their items shared (placed lines, fills and drawings are not
    /// replaced in place while the flow goes on), for a checkpoint (Paginator.Checkpoint).
    /// </summary>
    internal PageBox Copy()
    {
        var copy = new PageBox
        {
            Number = Number, Section = Section, DisplayNumber = DisplayNumber, SectionIndex = SectionIndex, FirstOfSection = FirstOfSection,
            Header = Header, Footer = Footer, TextLeft = TextLeft, TextWidth = TextWidth, Blank = Blank,
            Chapter = Chapter, NoteBand = NoteBand, BodyTop = BodyTop, BodyBottom = BodyBottom, TrailingSpace = TrailingSpace,
            Border = Border, Upright = Upright?.Copy(),
        };
        copy.Footnotes.AddRange(Footnotes);
        copy.Paragraphs.AddRange(Paragraphs);
        copy.Passed.AddRange(Passed);
        copy.Lines.AddRange(Lines);
        copy.ColumnLines.AddRange(ColumnLines);
        copy.Shading.AddRange(Shading);
        copy.Borders.AddRange(Borders);
        copy.Strokes.AddRange(Strokes);
        copy.Drawings.AddRange(Drawings);
        copy.FloatingTables.AddRange(FloatingTables);
        copy.LineNumbers.AddRange(LineNumbers);
        copy.Bookmarks.AddRange(Bookmarks);
        copy.Alternatives.AddRange(Alternatives);
        return copy;
    }
}

/// <summary>
/// Breaks the body into pages (catalog: engine.pagination, section.page, section.columns, paragraph.pagination):
/// sections start on a new page, an even or odd page or continue on the same page; lines fill the columns of the
/// text area; paragraphs keep their spacing (the larger of the space after the one and before the next, measured
/// with docx/generated/text-basic, as ISO/IEC 29500 17.3.1.33 says), keep with the next paragraph, keep their lines
/// together and avoid widows and orphans. At the top of a page the space before depends on how the page began
/// (measured with docx/local/sp-pagetop): all of it on the first page, after a section break what the larger space
/// has more than the space after the section's last paragraph, none after a page or column break, a page break
/// before or a natural break.
/// </summary>
/// <param name="reader">Reads the header and footer parts; without it pages have none.</param>
public sealed class Paginator(DocumentContext context, ParagraphLayout layout, ModelReader? reader = null)
{
    readonly List<PageBox> pages = [];
    readonly TableLayout tables = new(context, layout);

    // The current position: page, column, the top of the next line and whether the column is still empty.
    PageBox? page;
    SectionProperties section = new();
    int sectionIndex;
    int column;
    double y;
    bool columnEmpty = true;

    /// <summary>Where the current section's columns begin on this page (below an earlier section of the page).</summary>
    double columnTop;

    /// <summary>The lines of the current section on this page with the space before each, for balancing its columns.</summary>
    readonly List<FlowLine> flow = [];

    /// <summary>
    /// A line of the current section on this page: its place in the page's lines, the space before it where it does
    /// not begin a column (and where it does, for the first), its paragraph and its place in it.
    /// </summary>
    sealed record FlowLine(int Index, double Gap, double FirstGap, int Paragraph, int Line, int Lines, bool Widows, bool KeepNext, bool KeepLines)
    {
        /// <summary>The space after the line's paragraph when the line is its last.</summary>
        public double After { get; set; }

        /// <summary>The line's paragraph, as laid out.</summary>
        public ParagraphBlock? Block { get; init; }
    }

    /// <summary>Whether the section before ended with balanced columns, whose height holds their last space after.</summary>
    bool balanced;

    /// <summary>
    /// The space after the paragraph that holds the section break above: the empty one that holds it alone, else the
    /// last paragraph of text (see SpaceAbove).
    /// </summary>
    double sectionBreakAfter;

    /// <summary>Whether lines or rows of the current section stand on a page already (a continuous section's first
    /// page is the one it begins on).</summary>
    bool sectionShown = true;

    /// <summary>Whether the current page was begun by a page break and holds nothing yet: a section on a new page that
    /// would begin after it begins on it instead (StartSection).</summary>
    bool breakPage;

    /// <summary>The paragraphs a page passed that the next section took over, for its first page (PageBox.Passed).</summary>
    readonly List<ParagraphBlock> passedOver = [];

    int paragraphNumber;
    /// <summary>How the current column began, which decides the space before its first paragraph.</summary>
    enum Top { Document, Section, Break, Continuous, Leading }

    Top top = Top.Document;
    double pendingAfter;      // space after the previous paragraph
    Block? documentEnd;       // the body's last block, where no endnotes follow it
    double pendingOwnAfter;   // the same before contextual spacing dropped it
    ParagraphBlock? previous;

    /// <summary>
    /// The paragraph contextual spacing compares the next one with: the previous paragraph, or after a table the last
    /// paragraph of its last cell (measured with docx/local/c2-html: a contextually spaced paragraph after a table
    /// whose last paragraph has its style loses its space before).
    /// </summary>
    ParagraphBlock? stylePrevious;

    /// <param name="footnotes">The document's footnotes by id, with "separator" and "continuationSeparator".</param>
    /// <param name="endnotes">The document's endnotes in the order of their references, with the sections that refer to them.</param>
    /// <remarks>
    /// Floating objects with text wrapping (catalog: engine.floats) stand on the page of their anchor and push aside
    /// every line of that page, also of paragraphs above the anchor (measured with docx/generated/wrapping): the
    /// document is paginated again with the objects of each page known from the run before, until the pages keep
    /// their objects (at most four runs).
    /// </remarks>
    public List<PageBox> Paginate(List<Block> blocks, SectionProperties lastSection, Dictionary<string, List<Block>>? footnotes = null,
        List<(int Section, List<Block> Blocks)>? endnotes = null, List<Block>? endnoteSeparator = null)
    {
        input = (blocks, lastSection, footnotes, endnotes, endnoteSeparator);
        List<PageBox> Run()
        {
            // What the runs before settled for the next one.
            var settled = new Settled();
            List<PageBox> result = [];
            // The pages each object stood on run by run (see Settled.ForcedBreaks).
            var history = new Dictionary<XElement, List<int>>(ReferenceEqualityComparer.Instance);
            // Where each drawing of the body that text goes around stood, run by run (see Settled.Pins), and in the
            // last run that placed it.
            var placements = new Dictionary<XElement, List<(int Page, double X, double Y)>>(ReferenceEqualityComparer.Instance);
            var lastStood = new Dictionary<XElement, (int Run, int Page, double X, double Y)>(ReferenceEqualityComparer.Instance);
            for (var run = 0; run < 5; run++)
            {
                var paginator = new Paginator(context, layout, reader)
                {
                    settled = settled, EndnoteContinuation = EndnoteContinuation, settings = settings, checkpoints = KeepCheckpoints ? [] : null,
                };
                result = paginator.PaginateOnce(blocks, lastSection, footnotes, endnotes, endnoteSeparator);
                settings = paginator.settings;
                checkpoints = paginator.checkpoints;
                laid = paginator.pages;
                // Text frames on the page too push aside the lines above them (measured with docx/external/libreoffice/
                // extras-ooxmlexport/special_styles: the paragraphs before an envelope address framed at the page's
                // bottom stand beside it); those standing with the text move the lines they reach themselves (Lower).
                var found = result.ToDictionary(p => p.Number, p => p.Drawings.Select(ExclusionOf).OfType<Exclusion>()
                    .Concat(p.FloatingTables.Where(e => e.Frame && !e.WithText || e.Banded != null)).ToList());
                // Footnotes of later columns of a page take room from its earlier columns too: the page is laid out
                // again with the room they took.
                var foundBands = result.Where(p => p.NoteBand > 0).ToDictionary(p => p.Number, p => p.NoteBand);
                var bandsKept = foundBands.Count == settled.NoteBands.Count
                                && foundBands.All(b => settled.NoteBands.TryGetValue(b.Key, out var old) && Math.Abs(old - b.Value) < 0.01);
                // A drawing above its paragraph that this run found over lines before it stays where it stood before
                // they went below it, the lines going below it in the next run: where the run before put it on the same
                // page, else where it stands now, until its paragraph stands on another page; the first run knows no
                // objects of the pages yet (see reaching).
                var reaching = new List<(XElement Source, (int Page, double X, double Y) Stands)>();
                foreach (var (source, now) in run > 0 ? paginator.reaching : [])
                {
                    if (settled.Pins.TryGetValue(source, out var pinned) && pinned.Page == now.Page)
                        continue;
                    reaching.Add((source, lastStood.TryGetValue(source, out var before) && before.Run == run - 1 && before.Page == now.Page
                        ? (before.Page, before.X, before.Y) : now));
                }
                foreach (var placedPage in result)
                {
                    foreach (var drawing in placedPage.Drawings.Where(d => d.Part == context.MainPart && ExclusionOf(d) != null))
                        lastStood[drawing.Drawing] = (run, placedPage.Number, drawing.X, drawing.Y);
                }
                if ((found.Values.All(e => e.Count == 0) || Same(settled.Floats, found)) && bandsKept && reaching.Count == 0)
                    break;
                foreach (var (source, stands) in reaching)
                    settled.Pins[source] = stands;
                // A drawing that goes back and forth on its page, pushing the lines above its paragraph down and so its
                // paragraph and itself, stays where it stood first, from where its paragraph stood without it: Word moves
                // the lines below it, not the drawing (measured with docx/local/np-c14, np-c15: a rectangle 42.55 pt
                // above its paragraph over the line before stays where that paragraph first stood, the line below it, the
                // paragraph after the line; LibreOffice's extras-layout/fdo43573-2-min). Before Word 2013 the paragraph's
                // other drawings that went back and forth with it stay too, from Word 2013 on they stand from the
                // paragraph where it ends up (np-a14: a rectangle 42.55 pt below the paragraph at 128.0, np-a15 at 209.03).
                foreach (var placedPage in result)
                {
                    foreach (var drawing in placedPage.Drawings.Where(d => d.Part == context.MainPart && ExclusionOf(d) != null))
                    {
                        if (!placements.TryGetValue(drawing.Drawing, out var stood))
                            placements[drawing.Drawing] = stood = [];
                        stood.Add((placedPage.Number, drawing.X, drawing.Y));
                    }
                }
                foreach (var (source, stood) in placements)
                {
                    if (stood.Count < 3 || settled.Pins.ContainsKey(source))
                        continue;
                    var (a, b, c) = (stood[^3], stood[^2], stood[^1]);
                    if (a.Page == b.Page && b.Page == c.Page && Math.Abs(a.Y - c.Y) < 0.01 && Math.Abs(b.Y - c.Y) > 0.01
                        && (!context.Profile.DrawingsFollowMovedParagraph || Above(source)))
                        settled.Pins[source] = stood.First(s => s.Page == c.Page);
                }
                // An object that goes back and forth between two pages pushes its own paragraph off the first of them
                // when it wraps text there: Word puts the paragraph on the second and leaves the first without the object
                // (docx/external/openxml-sdk/Document: a cover page's text box placed from the page, anchored after a
                // chart, on the page after the chart; Complex01, Strict01 page 4).
                // The first run places the objects knowing none of those before them, so the history begins with the
                // second (LibreOffice's extras-layout/fdo43573-2-min: a picture below its paragraph at the bottom of page
                // 12's left column stands higher on page 12 in the first run, on page 13 in the second and where Word
                // has it in the third; the paragraph stays in the column).
                foreach (var (page, exclusions) in run > 0 ? found : [])
                {
                    foreach (var source in exclusions.Select(e => e.Source).OfType<XElement>())
                    {
                        if (!history.TryGetValue(source, out var pages))
                            history[source] = pages = [];
                        pages.Add(page);
                    }
                }
                // Every object of the paragraph leaves the first page, not only the first one found going back and forth
                // (Document: two text boxes of the cover, one over the chart, one over a table above it).
                foreach (var (source, pages) in history)
                {
                    if (pages.Count < 3 || pages[^1] != pages[^3] || Math.Abs(pages[^1] - pages[^2]) != 1
                        || AnchorParagraph(blocks, source) is not { } anchorParagraph)
                        continue;
                    settled.ForcedBreaks.Add(anchorParagraph.Id);
                    var earlier = Math.Min(pages[^1], pages[^2]);
                    if (found.TryGetValue(earlier, out var stale))
                        stale.RemoveAll(e => ReferenceEquals(e.Source, source));
                }
                settled.Floats = found;
                settled.NoteBands = foundBands;
            }
            return result;
        }
        // Fields Word computes when it prints: those that depend on the pages take them from a layout before.
        layout.Evaluator ??= new FieldEvaluator(context, blocks);
        var pages = Run();
        for (var pass = 0; pass < 2 && layout.Evaluator.Update(pages); pass++)
            pages = Run();
        layout.Evaluator.Update(pages);
        // Footnotes numbered anew on every page (w:numRestart eachPage) take their numbers from the pages they land on;
        // the pages are laid out again with them until they stay (measured with docx/local/fnpages-b).
        for (var pass = 0; pass < 3 && footnotes is { Count: > 0 }; pass++)
        {
            var marks = new Dictionary<(bool Endnote, string Id), string>();
            foreach (var page in pages)
            {
                var (own, settings) = (page.Section.Element.Child("footnotePr"), context.Settings.Child("footnotePr"));
                if ((own.Child("numRestart").Val() ?? settings.Child("numRestart").Val()) != "eachPage")
                    continue;
                var format = own.Child("numFmt").Val() ?? settings.Child("numFmt").Val() ?? "decimal";
                var k = 0;
                foreach (var id in page.Footnotes)
                    marks[(false, id)] = NumberText.Format(++k, format);
            }
            if (marks.Count == 0 || layout.NoteMarks is { } before && before.Count == marks.Count && before.All(m => marks.GetValueOrDefault(m.Key) == m.Value))
                break;
            layout.NoteMarks = marks;
            pages = Run();
        }
        NumberLines(pages);
        return pages;

        // The body paragraph an object is anchored in, or null.
        static ParagraphBlock? AnchorParagraph(List<Block> blocks, XElement source) =>
            blocks.OfType<ParagraphBlock>().FirstOrDefault(p => p.Content.OfType<AnchorInline>().Any(a => ReferenceEquals(a.Anchor, source)));

        static bool Same(Dictionary<int, List<Exclusion>> a, Dictionary<int, List<Exclusion>> b) =>
            a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var other) && other.Count == kv.Value.Count
                && other.Zip(kv.Value).All(p => Math.Abs(p.First.Top - p.Second.Top) < 0.01 && Math.Abs(p.First.Left - p.Second.Left) < 0.01));
    }

    /// <summary>
    /// Line numbers (w:lnNumType, catalog: section.lineNumbers) as Word prints them (measured with
    /// docx/local/line-numbers): every body line of a numbered section counts, empty ones too, except the lines of
    /// paragraphs with w:suppressLineNumbers, tables, notes, headers and footers; the first line has w:start + 1, the
    /// count starting again on every page (newPage, the default), in every section (newSection) or never
    /// (continuous); every w:countBy-th number shows, right-aligned w:distance (0.25 in by default) left of its
    /// column, on the line's baseline, in the Line Number style (not the paragraph's formatting).
    /// </summary>
    void NumberLines(List<PageBox> pages)
    {
        var count = 0;
        var section = -1;
        var styleId = context.Styles.Find("LineNumber") is { Type: "character" } ? "LineNumber" : null;
        var style = layout.Styles.Style(context.Styles.Run(context.Styles.DefaultParagraphStyle, styleId, null), WordSlot.Ascii);
        foreach (var page in pages)
        {
            if (page.Section.Element.Child("lnNumType") is not { } numbering || (numbering.Number("countBy") ?? 0) < 1)
            {
                section = -1;
                continue;
            }
            var start = (int)(numbering.Number("start") ?? 0);
            var every = (int)(numbering.Number("countBy") ?? 1);
            var distance = numbering.Twips("distance") ?? 18;
            var restart = numbering.Attr("restart") ?? "newPage";
            if (section < 0 || restart == "newPage" || restart == "newSection" && page.SectionIndex != section)
                count = start;
            section = page.SectionIndex;
            foreach (var placed in page.Lines.Where(l => l.Numbered))
            {
                count++;
                if (count % every == 0)
                    page.LineNumbers.Add((count.ToString(System.Globalization.CultureInfo.InvariantCulture), style, placed.Left - distance, placed.Top + placed.Line.Baseline));
            }
        }
    }

    /// <summary>
    /// A floating table (w:tblpPr, catalog: table.floating), measured with docx/local/float-tables: it takes no room in
    /// the text; its outer left edge stands w:tblpX from its column (horzAnchor text), the margin or the page, or it
    /// aligns left, right, centered, inside or outside (w:tblpXSpec); its top stands w:tblpY below the top of the
    /// paragraph after it (vertAnchor text), the top margin or the page, or aligns (w:tblpYSpec); text wraps around it
    /// on both sides, kept w:leftFromText, w:rightFromText, w:topFromText and w:bottomFromText away.
    /// </summary>
    /// <summary>The parts of floating tables that wait for the next page, the text going on beside them (see PlaceFloatingTable).</summary>
    readonly Queue<Action> floatingRests = new();

    void PlaceFloatingTable(List<Block> blocks, int index, XElement position, TableBox box, double left, double width)
    {
        double Twips(string name) => position.Twips(name) ?? 0;
        var height = box.Rows.Sum(r => r.Height) + (box.Rows.Count > 0 ? box.Rows[^1].BottomBorderWidth : 0);
        var tableWidth = box.Width;
        var (frameLeft, frameWidth) = position.Attr("horzAnchor") switch
        {
            "page" => (0.0, page!.Width),
            "margin" => (page!.TextLeft, page.TextWidth),
            _ => (left, width),
        };
        // The top of the paragraph after the table, above its space before (measured with docx/local/floatanchor-*: a
        // table at tblpY 0 stands right below the space after the paragraph before it, whatever the next one has
        // before; docx/external/libreoffice/core-text/floattable-heading-split-hyphen).
        var textTop = y + (columnEmpty ? 0 : pendingAfter);
        var anchor = VerticalAnchor(position, "vertAnchor");
        var (frameTop, frameHeight) = anchor switch
        {
            "page" => (0.0, page!.Height),
            "margin" => (page!.BodyTop, page.BodyBottom - page.BodyTop),
            _ => (textTop, 0.0),
        };
        var odd = page!.DisplayNumber % 2 == 1;
        // A table in a frame (TableReader) after paragraphs framed alike goes on below them (see PlaceFrame).
        var tableFrame = ((TableBlock)blocks[index]).Table is { Format.Framed: true } framedTable
            && framedTable.Rows.FirstOrDefault()?.Cells.FirstOrDefault()?.Blocks.FirstOrDefault() is ParagraphBlock { Format.Frame: { } cellFrame }
            ? cellFrame : null;
        // Older modes put the first cell's text at the left edge and the last cell's at the right one, the table out
        // by the inset (measured with docx/local/ftx-14-*, -15-*: left 6.0 pt left of the margin, right 6.0 pt past it
        // with cell margins of 115 twips, 5.64 with 108, centered alike; poi/Bug54771b).
        var (inset, rightInset) = context.Profile.FloatingTablesByText ? box.TextInsets : (0, 0);
        var x = position.Attr("tblpXSpec") switch
        {
            "left" => frameLeft - inset,
            "right" => frameLeft + frameWidth - tableWidth + rightInset,
            "center" => frameLeft + (frameWidth - tableWidth) / 2,
            "inside" => odd ? frameLeft - inset : frameLeft + frameWidth - tableWidth + rightInset,
            "outside" => odd ? frameLeft + frameWidth - tableWidth + rightInset : frameLeft - inset,
            // Older modes put the first cell's text at the position, as they do at an indent (measured with
            // docx/external/libreoffice/core-layout/floattable-compat14, -compat14-body, -compat14-nosplit in modes 14
            // and 12, extras-ooxmlexport/fdo68607: the text at the margin, the table 5.4 pt left of it), and the
            // table's own indent further right, which Word 2013 and later ignore (docx/local/fti-12-*, fti-14-*: tblInd
            // 108 and 720 move it 5.4 and 36 pt, from the margin, the page and the text alike, none or 0 not at all;
            // fti-15-*; docx/external/libreoffice/extras-ooxmlimport/tdf97038); a table in a frame keeps it from then on too
            // (docx/local/frsb-15: tblInd 360 puts the left line 18 pt right of the margin; frsb-12 the first cell's text;
            // extras-ooxmlexport/tdf164474).
            _ => frameLeft + Twips("tblpX") + (context.Profile.FloatingTablesByText ? (((TableBlock)blocks[index]).Table.Format.Indent ?? 0) - inset
                : ((TableBlock)blocks[index]).Table.Format is { Framed: true, Indent: { } framedIndent } ? framedIndent : 0),
        };
        // From the page, Word puts the table on its device's dot left of the position (PowerTools' DB-HeadersFooters-Src/
        // Letterhead: tblpX 1135 twips, 472.92 dots, at 472, its cells' text a dot left of where rounding put it;
        // LibreOffice's writerfilter-dmapper/floattable-header-overlap: 1362 twips, 567.5 dots, at 567).
        if (position.Attr("horzAnchor") == "page" && position.Attr("tblpXSpec") == null)
            x = WordDevice.Floored(x);
        // A table placed above the page's top stands at it (measured with docx/local/hft-*: tblpY -2000 from the
        // margin, the page or without an anchor, in the body and in a header, at the page's top edge).
        var top = Math.Max(0, position.Attr("tblpYSpec") switch
        {
            "top" or "inside" => frameTop,
            "bottom" or "outside" => frameTop + frameHeight - height,
            "center" => frameTop + (frameHeight - height) / 2,
            _ => frameTop + Twips("tblpY"),
        });
        // A table anchored to the page or the margin whose rows of its first page would reach past the page's bottom
        // moves up to end there with them: as many rows as fit between the paragraph after it and the bottom margin
        // (measured with docx/local/fpb-*: rows of 100 pt at tblpY 300 pt move up to 192 pt, six of them on the page,
        // at 500 pt three to 492 pt, one at 750 pt to 692 pt, rows of 283.5 pt at 640.85 pt two to 225 pt; at 100 and
        // at 450 pt they stay; anchored to the margin alike; in modes 12, 14 and 15, where the rows then break at the
        // bottom margin; docx/external/libreoffice/extras-ooxmlexport/tdf99140: text down to 368 pt leaves one row,
        // ending at the page's bottom, found by agent BG).
        if (anchor is "page" or "margin")
        {
            var (rows, part) = (0, 0.0);
            foreach (var row in box.Rows)
            {
                if (textTop + part + row.Height + row.BottomBorderWidth > Bottom + 0.01)
                    break;
                part += row.Height;
                rows++;
            }
            if (rows > 0)
            {
                var end = part + box.Rows[rows - 1].BottomBorderWidth;
                if (top + end > page!.Height + 0.01)
                    top = page.Height - end;
            }
        }
        if (tableFrame != null && Stacks(tableFrame, index))
            top = frameStack!.Value.Bottom;
        // A table that may not overlap others (w:tblOverlap never) moves with its distances off a floating table before
        // it on the page that it would overlap: right of it where it fits in its horizontal anchor's span, else below
        // it, its distance left then counting from its place (measured with docx/local/tc-5: beside a table at the
        // margin; td-*: below a right-aligned one, as far right as its own leftFromText; docx/external/libreoffice/
        // core-objectpositioning/floattable-tbl-overlap).
        if (((TableBlock)blocks[index]).Table.Format.NoOverlap)
        {
            var below = false;
            for (var tries = 0; tries < 8; tries++)
            {
                var (l, t, r, b) = (x - Twips("leftFromText"), top - Twips("topFromText"), x + tableWidth + Twips("rightFromText"), top + height + Twips("bottomFromText"));
                if (page.FloatingTables.FirstOrDefault(e => !e.Frame && !e.Story && e.Left < r - 0.01 && e.Right > l + 0.01 && e.Top < b - 0.01 && e.Bottom > t + 0.01)
                    is not { } other)
                    break;
                if (other.Right + (r - l) <= frameLeft + frameWidth + 0.01)
                    x = other.Right + Twips("leftFromText");
                else
                {
                    top = other.Bottom + Twips("topFromText");
                    if (!below)
                        x += Twips("leftFromText");
                    below = true;
                }
            }
        }
        // A table standing with the text over the lines before it, or over their paragraph's space after, moves them
        // below itself and keeps its place, the text going on below them (LibreOffice's core-text/
        // floattable-negative-vert-offset: tblpY -269 puts the line before the table below it, its paragraph after
        // that; floattable-anchor-next-page: 1.2 pt of overlap suffice; writerfilter-dmapper/
        // floattable-outer-nonsplit-inner: an empty paragraph whose 8 pt space after reaches the next table goes below
        // it, and below each table after); its distance from the text counts for nothing there, nor does an overlap of
        // less than a point (writerfilter-dmapper/floattable-redline: tblpY -17 twips leaves the page's first, empty
        // line where it is; extras-layout/tdf167540: tblpY 1 twip, 43 twips from the text).
        // The lines before it reach into its distance above it too, their paragraph's space after counting before Word
        // 2013, less than a point of it counting for nothing: they go beside the table where it leaves them room, the
        // table staying where it stood for them, else below it (measured with docx/local/ftb-*: three lines before a table
        // 150 pt wide at tblpY 1 with topFromText 300, 567 and 1134 twips have their last one, two and three lines beside
        // it, with 43 twips the last one by its paragraph's 8 pt after before Word 2013; a table 450 pt wide takes them
        // below itself alike; LibreOffice's extras-ooxmlexport/table-floating-margins).
        Block? banded = null;
        if (anchor == "text")
        {
            var block = blocks[index];
            var reach = y + (context.Profile.SpaceAfterMeetsFloats ? pendingAfter : 0) - (top - Twips("topFromText"));
            if (settled.Floats.TryGetValue(page!.Number, out var prior) && prior.FirstOrDefault(e => e.Banded?.Id == block.Id) is { } kept)
            {
                top = kept.Top + Twips("topFromText");
                banded = block;
            }
            else if (!columnEmpty && y + pendingAfter - top > 1)
                Lower(Math.Min(top, y - 0.02), top + height + Twips("bottomFromText"), left, width);
            else if (!columnEmpty && reach > 1
                     && Math.Max(x - Twips("leftFromText") - left, left + width - (x + tableWidth + Twips("rightFromText"))) >= ParagraphLayout.MinimumSegment)
                banded = block;
            else if (!columnEmpty && reach > 1)
                Lower(Math.Min(top - Twips("topFromText"), y - 0.02), top + height + Twips("bottomFromText"), left, width);
        }
        // The rows at the table's place (their own offset is the table's indent, which the position replaces). A row
        // that does not fit below the rows before it on the page goes on at the top of the next page's body, where
        // the table stands at the same place across, its header rows repeated; the line below the last row of a page
        // closes the part there, and the paragraph after the table begins at the top of its last page (measured with
        // docx/external/libreoffice/core-layout/floattable, floattable-3pages, floattable-compat14 and fdo68607).
        var rowTop = top;
        var partTop = top;
        // Where the part on the page began among the page's lines, fills, strokes and drawings, which move with it where
        // it moves off a drawing of the footer (see Close).
        var partFrom = PartFrom();
        (int Lines, int Shading, int Borders, int Strokes, int Drawings) PartFrom() =>
            (page!.Lines.Count, page.Shading.Count, page.Borders.Count, page.Strokes.Count, page.Drawings.Count);
        var onPage = 0;
        RowBox? last = null;
        var headers = box.Rows.TakeWhile(r => r.Header).ToList();
        void Draw(RowBox row)
        {
            // Drawings anchored in the cells stand as in the rows of other tables (docx/external/libreoffice/
            // extras-ooxmlexport/fdo68607: a text box with the title above the table).
            var anchors = new List<CellAnchor>();
            var count = page!.Lines.Count;
            TableLayout.Draw(row, row.Height, x - box.Left, rowTop, page.Lines, page.Shading, page.Borders, page.Strokes, anchors);
            // The table's lines stay where it stands when a table after it moves the text below itself (see Lower).
            for (var k = count; k < page.Lines.Count; k++)
                frameLines.Add(page.Lines[k]);
            // Before Word 2013 a drawing of layoutInCell 0 placed from its paragraph in a cell stands as far below the
            // page's top as that paragraph stands below the top of the paragraph the table belongs to (measured with
            // docx/local/nie-a to nie-d: 100 pt below a cell's paragraph 20 pt below that top puts it 120 pt below the
            // page's top, whatever the table's vertAnchor; placed from its line or the page as said, nie-e, nie-f;
            // docx/external/libreoffice/extras-ooxmlexport/missing-path, extras-uiwriter/tdf78727).
            foreach (var anchor in anchors)
                page.Drawings.Add(PlaceAnchor(anchor.Anchor, page, anchor.Cell == null && context.Profile.HonorsLayoutInCell ? anchor.ParagraphTop - textTop : anchor.ParagraphTop,
                    anchor.LineTop, x - box.Left, page.TextWidth, anchor, anchor.CharacterX));
            rowTop += row.Height;
            last = row;
            onPage++;
        }
        void Close(RowBox row)
        {
            // From Word 2013 on a table that may not overlap others moves up off a drawing of the header or footer that
            // text goes around and that its part on the page reaches below the part's top, the part then ending at the
            // drawing's top, where it still begins on the page; else it stays (measured with docx/local/fto-*: a table
            // at tblpY 1 below the text's top with rows of exactly 55 pt, five on each page of a body of 275.5 pt, below a
            // footer's rectangle at 281.25 pt behind the text, in front of it or wrapped square, 66 pt higher on every
            // page, one at 240 pt where it would leave the page staying, not wrapped, without tblOverlap never or in mode
            // 14 staying; LibreOffice's extras-layout/floattable-footer-object).
            var (partLeft, partRight) = (x - Twips("leftFromText"), x + tableWidth + Twips("rightFromText"));
            var partBottom = rowTop + row.BottomBorderWidth + Twips("bottomFromText");
            if (((TableBlock)blocks[index]).Table.Format.NoOverlap && context.Profile.FloatingTablesClearStoryDrawings
                && page!.FloatingTables.Where(e => e.Story && e.Source != null && e.Top > partTop - Twips("topFromText") + 0.01 && e.Top < partBottom - 0.01
                    && e.Left < partRight - 0.01 && e.Right > partLeft + 0.01).Select(e => partBottom - e.Top).DefaultIfEmpty(0).Max() is var lift and > 0.01
                && partTop - Twips("topFromText") - lift > -0.01)
            {
                for (var k = partFrom.Lines; k < page.Lines.Count; k++)
                {
                    var moved = page.Lines[k] with { Top = page.Lines[k].Top - lift };
                    if (frameLines.Remove(page.Lines[k]))
                        frameLines.Add(moved);
                    page.Lines[k] = moved;
                }
                for (var k = partFrom.Shading; k < page.Shading.Count; k++)
                    page.Shading[k] = page.Shading[k] with { Y = page.Shading[k].Y - lift };
                for (var k = partFrom.Borders; k < page.Borders.Count; k++)
                    page.Borders[k] = page.Borders[k] with { Y = page.Borders[k].Y - lift };
                for (var k = partFrom.Strokes; k < page.Strokes.Count; k++)
                    page.Strokes[k] = page.Strokes[k].Offset(0, -lift);
                for (var k = partFrom.Drawings; k < page.Drawings.Count; k++)
                    page.Drawings[k] = page.Drawings[k] with { Y = page.Drawings[k].Y - lift };
                rowTop -= lift;
                partTop -= lift;
            }
            foreach (var fill in row.BottomBorders)
                page!.Borders.Add(fill with { X = x - box.Left + fill.X, Y = rowTop + fill.Y });
            page!.Strokes.AddRange(row.BottomStrokes.Select(s => s.Offset(x - box.Left, rowTop)));
            page.FloatingTables.Add(new Exclusion(x - Twips("leftFromText"), partTop - Twips("topFromText"),
                x + tableWidth + Twips("rightFromText"), rowTop + row.BottomBorderWidth + Twips("bottomFromText"), false, "bothSides")
                {
                    DistanceLeft = Twips("leftFromText"), DistanceRight = Twips("rightFromText"), Anchor = index + 1 < blocks.Count ? blocks[index + 1] : null,
                    Banded = Math.Abs(partTop - top) < 0.01 ? banded : null,
                });
        }
        var split = false;
        // Before Word 2013 a table anchored to the page or the margin breaks on its first page where it would at the
        // paragraph after it, as many lines down, and so may reach past the bottom margin or stop above it (measured
        // with docx/local/floatsplit-14p-*: as many lines as anchored to the text; dummyv0, -v2, -v4: variants of
        // docx/external/libreoffice/core-doc/floating-table-dummy-text; floattable-compat14); Word 2013 breaks it at
        // the bottom margin (floattable-2cols, floattable-footer).
        // From Word 2013 on one standing above the text's top breaks that much above the bottom margin (measured with
        // docx/local/fm-a15, fm-e15: 60 rows at tblpY -345 twips from the margin, 50 on the first page, 51 at the text's
        // top; docx/external/libreoffice/extras-layout/tdf109053).
        var shift = anchor is not ("page" or "margin") ? 0 : context.Profile.PageTablesBreakAsText ? textTop - top : Math.Max(0, textTop - top);
        double Limit() => Bottom - (split ? 0 : shift);
        // From Word 2013 on a table anchored to the page or the margin whose first row does not fit above the bottom
        // margin stays where it stands on a page without text before it, its row reaching past the margin, and goes
        // to the next page at the same place from one with text, its next rows going on at the top of the page after
        // (measured with docx/local/flb-spec, flb-below, flb-cross: rows of 20 pt at the page's foot, at 740 and at
        // 705 pt with the bottom margin at 720 pt, after a paragraph; docx/external/powertools/DB-HeadersFooters-Src/
        // Footer: the table first on its page at the page's foot).
        var unbroken = !context.Profile.PageTablesBreakAsText && anchor is "page" or "margin";
        var emptyPage = columnEmpty && page!.Lines.Count == 0;
        // w:doNotBreakWrappedTables keeps the table whole where it stands (docx/external/libreoffice/
        // writerfilter-dmapper/do-not-break-wrapped-tables).
        var breaks = !context.Profile.KeepsWrappedTablesWhole;
        // A table anchored to the text in a section of columns does not break: where it does not fit below its place it
        // goes to the next page whole with its paragraph (LibreOffice's core-layout/floattable-multi-col: three rows in
        // two columns, the third reaching past the bottom margin, on page 2 at its place from "anchor").
        var movesWhole = section.Columns > 1 && anchor == "text" && breaks && top + height > Limit() + 0.01 && !(columnEmpty && page!.Lines.Count == 0);
        // From Word 2013 on, where the document allows text after a floating table's break, the text after the table
        // goes on beside and below its first part, each further part waiting for the next page the text or a break
        // begins, at its top, the text beside it there, its header rows repeated; parts the text does not reach take
        // pages of their own (measured with docx/local/atf-*: a table of two rows on a page with a body of 166 pt,
        // the paragraph after it beside and below its first row on page 1 and beside its second at the top of page 2;
        // six paragraphs after it, a page break after it, a table placed from the page, a header row and three rows
        // alike; the document ending after one line, a page of its own for the second row; in mode 14 and without the
        // setting the text begins beside the last part; LibreOffice's writerfilter-dmapper/floattable-wrap-on-all-pages).
        var waits = context.Profile.TextAfterFloatingTableBreak;
        var waited = false;
        // The rows from the one at from on, the first of them held, the rest of a row the page before broke, at the
        // top of the page.
        void Rows(int from, RowBox? held)
        {
            for (var r = from; r < box.Rows.Count; r++)
            {
                var row = r == from && held != null ? Continue(held) : box.Rows[r];
                // A row that does not fit splits at a line of each cell as in a table in the text, in Word 2013 and before
                // alike (also one that may not split where no row of the page is before it), else moves on; a row that
                // cannot split at the top of a page overflows (measured with docx/local/floatsplit-*: anchored to the text
                // or the page, its first row of lines or of one long paragraph; docx/external/libreoffice/core-layout/
                // floattable-rowsplit, floattable-table-row-keep).
                while (breaks && (rowTop + row.Height + row.BottomBorderWidth > Limit() + 0.01 || movesWhole))
                {
                    if (!movesWhole && Limit() - rowTop > row.TopWidth + 1 && row.Split(Limit() - rowTop - row.BottomBorderWidth, onPage == 0) is { } parts)
                    {
                        Draw(parts.Head);
                        row = parts.Tail;
                    }
                    // A row of at least height that such a table puts below the margin on a page without text before it
                    // keeps one line of each cell there and goes on at the next page (docx/external/powertools/
                    // DB-HeadersFooters-Src/Footer: a row of 45.35 pt at the page's foot cut after its first line, 9.24 pt,
                    // its text at its top); rows of exact height reach past the margin whole (flb-*).
                    else if (unbroken && emptyPage && onPage == 0 && !row.Exact && !row.CantSplit && !row.Header && Least(row) is { } least)
                    {
                        Draw(least.Head);
                        row = least.Tail;
                    }
                    // A row of at least its height taller than a page's body ends at the bottom margin at the top of a page,
                    // the rest of its height gone, the next row on the next page (found by agent BE in Word's PDF of
                    // docx/external/libreoffice/core-layout/floattable-in-text-section: rows of at least 283.5 pt on a body
                    // of 166 pt, mode 15, each on a page of its own, cut at the margin).
                    else if (onPage == 0 && (split || rowTop <= page!.BodyTop + 0.01) && !row.Exact && row.Height + row.BottomBorderWidth > Bottom - page.BodyTop + 0.01
                        && Limit() - rowTop > row.TopWidth + 1 && row.Split(Limit() - rowTop - row.BottomBorderWidth, force: true, dropRest: true) is { } cut)
                    {
                        row = cut.Head;
                        break;
                    }
                    else if (onPage == 0 && (split || rowTop <= page!.BodyTop + 0.01 || unbroken && emptyPage))
                        break;
                    // A table anchored to the text that begins on the next page goes there with its paragraph and stands
                    // where it would at it (measured with docx/local/fn-1, fn-2, fn-4: tblpY -289 and 289 twips put it as
                    // far above and below the next page's top margin, in modes 14 and 15; docx/external/libreoffice/
                    // core-layout/floattable-next-leaf-in-section).
                    var withText = last == null && anchor == "text";
                    var samePlace = last == null && unbroken;
                    if (last != null)
                        Close(last);
                    if (waits && last != null)
                    {
                        var (at, rest) = (r, row);
                        floatingRests.Enqueue(() =>
                        {
                            rowTop = partTop = page!.BodyTop;
                            partFrom = PartFrom();
                            Rows(at, rest);
                        });
                        waited = true;
                        return;
                    }
                    NewPage(section);
                    rowTop = partTop = samePlace ? top : page!.BodyTop + (withText ? top - textTop : 0);
                    partFrom = PartFrom();
                    row = Continue(row);
                }
                Draw(row);
            }
            if (last != null)
                Close(last);
            else
                page!.FloatingTables.Add(new Exclusion(x - Twips("leftFromText"), top - Twips("topFromText"),
                    x + tableWidth + Twips("rightFromText"), top + height + Twips("bottomFromText"), false, "bothSides")
                    {
                        DistanceLeft = Twips("leftFromText"), DistanceRight = Twips("rightFromText"), Anchor = index + 1 < blocks.Count ? blocks[index + 1] : null,
                        Banded = banded,
                    });
        }
        // A part going on at the top of a page: its header rows first.
        RowBox Continue(RowBox row)
        {
            onPage = 0;
            split = true;
            movesWhole = false;
            if (!row.Header)
            {
                foreach (var header in headers)
                    Draw(header);
                onPage = 0;
            }
            return row.Header || headers.Count == 0 ? row.AtPageTop() : row;
        }
        Rows(0, null);
        frameStack = tableFrame != null && !split && !waited ? (tableFrame, page!, top + height, index + 1) : null;
        if (split)
        {
            y = page!.BodyTop;
            columnEmpty = true;
            this.top = Top.Break;
        }
        // The paragraph after the table adds all its space before to the space after the paragraph before the table
        // (docx/local/floatanchor-fb: 12 pt after and 24 pt before make 36).
        pendingOwnAfter = 0;
    }

    /// <summary>
    /// A row cut after the first line of each of its cells, or, where none has more than one, after its content, the
    /// rest of its height gone (DB-HeadersFooters-Src/Footer: one line in each cell, the row 9.24 pt high on the page
    /// and nothing on the next); null where it has no lines.
    /// </summary>
    static (RowBox Head, RowBox Tail)? Least(RowBox row)
    {
        var withLines = row.Cells.Count(c => c.Lines.Count > 0);
        if (withLines == 0)
            return null;
        for (var available = row.TopWidth + 1; available < row.Height; available += 0.5)
        {
            if (row.Split(available, force: true) is { } parts && parts.Head.Cells.Count(c => c.Lines.Count > 0) >= withLines)
                return parts;
        }
        var content = row.Cells.Where(c => c.Lines.Count > 0)
            .Max(c => c.Lines.Max(l => l.Top - c.Lowered + l.Line.Height + l.After) + c.BottomMargin);
        return row.Split(content + 0.01, force: true, dropRest: true);
    }

    /// <summary>
    /// A text frame or drop cap (w:framePr, catalog: paragraph.frames): consecutive paragraphs with the same frame
    /// properties, measured with docx/local/frames. The frame takes no room in the text: it is w:w wide (else as wide
    /// as its widest content) and stands w:x from, or aligned (w:xAlign) in, its column (hAnchor text), the margin or
    /// the page, w:y below the top of the paragraph after it (vAnchor text), the margin or the page; text wraps around
    /// it, kept w:hSpace and w:vSpace away (not beside it with wrap notBeside, over it with wrap none). A drop cap is a
    /// frame of its letter at the start of the paragraph after it, a margin drop cap left of the column.
    /// </summary>
    /// <returns>The index of the first block after the frame.</returns>
    /// <summary>
    /// The frame (w:framePr) the blocks placed last stand in: its properties, page and the bottom of what it holds so
    /// far, and the block that may go on in it (see PlaceFrame, PlaceFloatingTable).
    /// </summary>
    (XElement Properties, PageBox Page, double Bottom, int Next)? frameStack;

    /// <summary>Whether a block of frame properties <paramref name="frame"/> at <paramref name="index"/> goes on in the frame placed last.</summary>
    bool Stacks(XElement frame, int index) =>
        frameStack is { } stack && stack.Next == index && ReferenceEquals(stack.Page, page) && XNode.DeepEquals(stack.Properties, frame);

    int PlaceFrame(List<Block> blocks, int index)
    {
        var first = (ParagraphBlock)blocks[index];
        var properties = first.Format.Frame!;
        var group = new List<ParagraphBlock> { first };
        var end = index + 1;
        while (end < blocks.Count && blocks[end] is ParagraphBlock { EndsSection: null } p && p.Format.Frame is { } other && XNode.DeepEquals(other, properties))
        {
            group.Add(p);
            end++;
        }
        // A frame whose first paragraph breaks the page before it (w:pageBreakBefore, its own or its style's) stands on
        // the next page, unless this one is still empty, as the text does (docx/external/libreoffice/writerfilter-dmapper/
        // framepr-pagebreak-style: a framed heading of a style with a page break before on page 2).
        if (first.Format.PageBreakBefore == true && !(columnEmpty && column == 0 && page!.Lines.Count == 0))
        {
            NewPage(section);
            this.top = Top.Break;
        }
        var (left, width) = Column(column);
        double Twips(string name) => properties.Twips(name) ?? 0;
        // A frame without a width of its own (w:w) is as wide as its paragraph, an empty one as its mark; one of several
        // paragraphs is as wide as its column (docx/external/libreoffice/extras-layout/tdf124423: two empty paragraphs
        // framed across the column, an empty paragraph 6.4 pt wide, Calibri 11's ¶; tdf135035: 127.6 pt in a column of
        // three; poi/checkboxes).
        double OwnWidth(ParagraphBlock p) => layout.Widths(p, settings, pictures: true).Max is var max && max > 0 ? max
            : layout.Styles.Mark(p.MarkFormat) is var mark ? mark.Font.Data.Width("¶", false) * mark.Size : 0;
        // A frame of one paragraph without a width is as wide as its text, at most its column (docx/external/
        // libreoffice/extras-layout/tdf135035: a frame in a column 127.6 pt wide in four lines).
        var frameWidth = properties.Twips("w") is { } w && w > 0 ? w : group.Count > 1 ? width : Math.Min(OwnWidth(first), width);
        // A frame of a given height whose paragraph runs its text down (tbRl) or up (btLr) lays it out along that
        // height, turned a quarter: its first line at the right and running down from the top, or at the left and
        // running up from the bottom, as text boxes turn theirs (docx/external/libreoffice/writerfilter-dmapper/
        // frame-direction: frames 44.35 pt wide and 820 pt high).
        var turn = first.Format.TextDirection is "tbRl" or "btLr" && properties.Twips("h") is { } along && along > 0 ? first.Format.TextDirection : null;
        var laid = Turned(turn != null, () => group.Select(p => layout.Layout(p, turn != null ? properties.Twips("h")!.Value : frameWidth, settings)).ToList());
        var content = 0.0;
        for (var k = 0; k < group.Count; k++)
            content += (k > 0 ? Between(group[k - 1], group[k]) : 0) + laid[k].Sum(l => l.Height);
        // The paragraphs' borders frame the content: their top and bottom lines with their space take room. Paragraphs
        // of the same borders (or, without, shading) share a box, each such group its own, the last one reaching down
        // to the frame's height (docx/external/libreoffice/extras-ooxmlexport/tdf154703_framePr: a bordered and shaded
        // paragraph, a shaded one and one of other borders in a frame of exact height, three boxes).
        var boxes = new List<(int From, int To, Frame? Box)>();
        for (var k = 0; k < group.Count; k++)
        {
            var own = Frame.Read(group[k].Format);
            if (boxes.Count > 0 && (own == null ? boxes[^1].Box == null : own.Same(boxes[^1].Box)))
                boxes[^1] = boxes[^1] with { To = k };
            else
                boxes.Add((k, k, own));
        }
        static double Opening(Frame? box) => box?.Top is { Visible: true } bt ? bt.Space + bt.Total : 0;
        static double Closing(Frame? box) => box?.Bottom is { Visible: true } bb ? bb.Space + bb.Total : 0;
        var frameBorders = boxes[0].Box;
        var borderTop = Opening(frameBorders);
        content += boxes.Sum(box => Opening(box.Box) + Closing(box.Box));
        var dropCap = properties.Attr("dropCap");
        // The first paragraph's space before stands in the frame, above its lines and borders (measured with
        // docx/local/fb-*: 6 and 12 pt before put the frame's text and the text below it as much lower, after 12 pt
        // after the paragraph above too; docx/external/libreoffice/core-ooxml-pass/fdo78904_fileHang).
        var spaceTop = dropCap is "drop" or "margin" ? 0 : SpaceBefore(first, null, false);
        // The last paragraph's space after stands in the frame too, below its lines and borders (measured with
        // docx/local/fk-*: 16 pt after a line framed notBeside take the text below as much lower, the text beside a
        // frame wrapped around goes on as far; docx/external/libreoffice/extras-ooxmlexport/tdf105035_framePrB).
        var spaceBottom = dropCap is "drop" or "margin" ? 0 : OwnAfter(group[^1]);
        content += spaceTop + spaceBottom;
        var height = properties.Twips("h") is { } h && h > 0 ? properties.Attr("hRule") == "exact" ? h : Math.Max(h, content) : content;
        // The paragraph the frame belongs to: the first after it.
        var next = end < blocks.Count ? blocks[end] as ParagraphBlock : null;
        var textTop = y + (columnEmpty ? 0 : Gap(next != null ? SpaceBefore(next) : 0));
        var (frameLeft, frameSpan) = properties.Attr("hAnchor") switch
        {
            "page" when dropCap is not "margin" => (0.0, page!.Width),
            "margin" or "page" => (page!.TextLeft, page.TextWidth),
            _ => (left, width),
        };
        var (frameTop, frameHeight) = VerticalAnchor(properties) switch
        {
            "page" => (0.0, page!.Height),
            "margin" => (page!.BodyTop, page.BodyBottom - page.BodyTop),
            _ => (textTop, 0.0),
        };
        var odd = page!.DisplayNumber % 2 == 1;
        var hSpace = Twips("hSpace");
        var x = dropCap == "margin" ? left - frameWidth - hSpace
            : dropCap == "drop" ? left
            // A w:x, 0 too, outweighs a w:xAlign beside it (measured with docx/local/fxa-a, -b, -f: x 720 and xAlign
            // center in either order 36 pt from the column, x 0 at the margin, in modes 14 and 15; docx/external/
            // libreoffice/extras-ooxmlexport/tdf112287B).
            : (properties.Attr("x") != null ? null : properties.Attr("xAlign")) switch
            {
                "left" => frameLeft,
                "right" => frameLeft + frameSpan - frameWidth,
                "center" => frameLeft + (frameSpan - frameWidth) / 2,
                "inside" => odd ? frameLeft : frameLeft + frameSpan - frameWidth,
                "outside" => odd ? frameLeft + frameSpan - frameWidth : frameLeft,
                _ => frameLeft + Twips("x"),
            };
        // A frame standing with the text is placed by w:y alone, its w:yAlign unseen (docx/external/libreoffice/
        // extras-ooxmlexport/tdf157572_insidiousCombination: a frame of yAlign bottom and vAnchor text below its
        // paragraph's top, not above it); so is any frame with a w:y (measured with docx/local/fya-14, fya-15: yAlign
        // center and w:y 10 stand 0.5 pt below the margin or the page, without w:y on the margin's middle;
        // tdf157572_defaultVAnchor).
        var top = dropCap is "drop" or "margin" ? textTop : (VerticalAnchor(properties) == "text" || properties.Attr("y") != null ? null : properties.Attr("yAlign")) switch
        {
            "top" or "inside" => frameTop,
            "bottom" or "outside" => frameTop + frameHeight - height,
            "center" => frameTop + (frameHeight - height) / 2,
            _ => frameTop + Twips("y"),
        };
        // Paragraphs framed as a table before them go on below it in that frame, and a table framed as paragraphs
        // before it below them (measured with LibreOffice's extras-ooxmlexport/fdo60957-2: "F", a table and "H" framed
        // alike, 5103 by 1565 twips, one below the other: F's baseline 120.5 pt, the table from 122.66 to 133.46, H's
        // baseline 142.82; ours all at the frame's top before).
        var stacked = Stacks(properties, index);
        if (stacked)
            top = frameStack!.Value.Bottom;
        // Lines above a frame standing with the text that reach into it or its w:vSpace go below it, the frame keeping
        // its place, and the text goes on below them (measured with docx/local/fi-2, fi-3, fj-2: the paragraph before
        // two frames centered with vSpace 187 and notBeside goes below both, the second standing below the first after
        // that paragraph; docx/external/libreoffice/extras-ooxmlexport/tdf105035_framePrB).
        var vSpace = Twips("vSpace");
        var withText = VerticalAnchor(properties) == "text";
        if (withText && properties.Attr("wrap") == "notBeside" && dropCap is null or "none" && !stacked)
            Lower(top - vSpace, top + height + vSpace, left, width);
        // The frame's lines, its paragraphs' borders around the whole frame. In compatibility modes before 15 Word
        // draws the inline shapes of a frame at its left edge and as far down from the page's top as the frame stands
        // below where its paragraph would stand in the text, wherever they stand in its lines, the text where it
        // belongs (see PlacedLine.InlineOrigin; measured with docx/local/framepg-*, frin-*, frio-*: in modes 11, 12 and
        // 14 a rectangle after "Framed " in a frame at page y 49.8 pt, the first paragraph below a top margin of 27 pt,
        // is drawn at the frame's left edge and 22.75 pt, after a line of 13.8 pt at 8.95 pt, with a top margin of
        // 72 pt at -22.25 pt, two rectangles both there, in a frame 49.8 pt below its paragraph at 49.8 pt, in a frame
        // without a position at the page's top; pictures where they stand; docx/external/libreoffice/
        // extras-ooxmlexport/FDO78590, Tdf147485; fdo78904: a picture).
        (double, double)? origin = context.Profile.FrameShapesAtFrameEdge && dropCap is null or "none" ? (x, top - textTop) : null;
        var lineTop = top + spaceTop + borderTop;
        var boxTops = new double[boxes.Count];
        var boxBottoms = new double[boxes.Count];
        boxTops[0] = top + spaceTop;
        // A turned frame's paragraphs: where each begins across the frame and where its first glyph stands along it.
        var parts = new List<(double Start, double Glyph)>();
        for (var k = 0; k < group.Count; k++)
        {
            var opening = boxes.FindIndex(box => box.From == k);
            if (k > 0 && opening > 0)
            {
                // A new box: the one before closes below its last line, this one opens below the space between.
                boxBottoms[opening - 1] = lineTop + Closing(boxes[opening - 1].Box);
                lineTop = boxBottoms[opening - 1] + Between(group[k - 1], group[k]);
                boxTops[opening] = lineTop;
                lineTop += Opening(boxes[opening].Box);
            }
            else if (k > 0)
                lineTop += Between(group[k - 1], group[k]);
            // The paragraph's floating drawings, placed from it in the frame as in the text (docx/external/libreoffice/
            // extras-ooxmlexport/tdf90789: a page-relative VML rectangle in a framed paragraph).
            if (turn == null)
            {
                foreach (var anchor in group[k].Content.OfType<AnchorInline>())
                    page.Drawings.Add(PlaceAnchor(anchor, page, lineTop, lineTop, x, frameWidth));
            }
            parts.Add((lineTop - top, laid[k].Count > 0 ? GlyphStart(laid[k][0], group[k].Format, properties.Twips("h") ?? 0) : 0));
            foreach (var line in laid[k])
            {
                var placed = turn switch
                {
                    "tbRl" => new PlacedLine(line, x + frameWidth - (lineTop - top), top) { Angle = 90, TurnedFrame = (x, frameWidth, 0, height) },
                    "btLr" => new PlacedLine(line, x + (lineTop - top), top + height) { Angle = -90, TurnedFrame = (x, frameWidth, -height, height) },
                    _ => new PlacedLine(line, x, lineTop) { InlineOrigin = origin },
                };
                page.Lines.Add(placed);
                frameLines.Add(placed);
                lineTop += line.Height;
            }
        }
        // A frame's borders go all the way down, the last paragraph's space after inside them, to its height where it is
        // higher than its content (docx/external/libreoffice/extras-ooxmlexport/tdf131420, tdf133702: frames of 91.25
        // and 104.45 pt framed from their top to their bottom; extras-layout/tdf124423: two empty paragraphs and their
        // 8 pt after each framed 48.4 pt high; frame-wrap-auto).
        var given = properties.Twips("h") is { } fh && fh > 0 ? fh : 0;
        boxBottoms[^1] = top + (turn != null ? frameWidth : Math.Max(given, height));
        for (var i = 0; i < boxes.Count; i++)
        {
            if (turn != null && boxes[i].Box is { } turnedBox)
                DrawTurned(turnedBox, page.Shading, page.Borders, turn, x, frameWidth, top, height, boxTops[i] - top, boxBottoms[i] - top,
                    parts.GetRange(boxes[i].From, boxes[i].To - boxes[i].From + 1));
            else
                boxes[i].Box?.Draw(page, x, frameWidth, group[boxes[i].From].Format, boxTops[i], boxBottoms[i], top: true, bottom: true, between: null);
        }
        // The frame keeps free its side borders too (they stand outside its width, as paragraph borders do).
        var outerLeft = frameBorders?.Left is { Visible: true } fl ? 1.4 + fl.Space + fl.Total : 0;
        var outerRight = frameBorders?.Right is { Visible: true } fr ? 1.55 + fr.Space + fr.Total : 0;
        if (properties.Attr("wrap") != "none" && !stacked)
            page.FloatingTables.Add(new Exclusion(x - outerLeft - hSpace, top - vSpace, x + frameWidth + outerRight + hSpace, top + height + vSpace,
                properties.Attr("wrap") == "notBeside", "bothSides")
            {
                Frame = true, WithText = withText, DropCap = dropCap == "drop", NarrowestRoom = dropCap is null or "none" ? FrameRoom : 0,
            });
        frameStack = dropCap is null or "none" ? (properties, page!, top + content, end) : null;
        return end;
    }

    /// <summary>
    /// A turned frame's paragraph borders and shading, laid out along its lines and turned with them (measured with
    /// LibreOffice's extras-ooxmlexport/fdo76979: a btLr header frame 42.55 by 85.05 pt framed across its width and 3.13
    /// pt past both its ends, as far as side borders stand out; each paragraph's shading across its own lines, from the
    /// border at the lines' end to 2.52 pt before its first glyph, the mark of an empty centered paragraph in the middle).
    /// </summary>
    /// <param name="length">The frame's height, its lines' length.</param>
    /// <param name="boxTop">The box's top across the frame, from its edge where its lines begin; <paramref name="boxBottom"/> alike.</param>
    /// <param name="parts">Each paragraph's start across the frame and its first glyph along its lines.</param>
    static void DrawTurned(Frame frame, List<FillRect> shadings, List<FillRect> borders, string turn, double x, double frameWidth, double frameTop,
        double length, double boxTop, double boxBottom, List<(double Start, double Glyph)> parts)
    {
        var (localShading, localBorders) = (new List<FillRect>(), new List<FillRect>());
        frame.Draw(localShading, localBorders, 0, length, boxTop, boxBottom, top: true, bottom: true, between: null);
        var shaded = new List<FillRect>();
        foreach (var fill in localShading)
        {
            for (var k = 0; k < parts.Count; k++)
            {
                var from = k == 0 ? fill.Y : Math.Max(fill.Y, parts[k].Start);
                var to = k + 1 < parts.Count ? Math.Min(fill.Y + fill.Height, parts[k + 1].Start) : fill.Y + fill.Height;
                var start = Math.Max(fill.X, parts[k].Glyph - 2.52);
                if (to > from + 0.01 && fill.X + fill.Width > start + 0.01)
                    shaded.Add(fill with { X = start, Y = from, Width = fill.X + fill.Width - start, Height = to - from });
            }
        }
        // Down (tbRl) the lines stack from the right edge and run down from the top; up (btLr) from the left edge and up
        // from the bottom.
        FillRect Turn(FillRect r) => turn == "tbRl"
            ? r with { X = x + frameWidth - (r.Y + r.Height), Y = frameTop + r.X, Width = r.Height, Height = r.Width }
            : r with { X = x + r.Y, Y = frameTop + length - (r.X + r.Width), Width = r.Height, Height = r.Width };
        shadings.AddRange(shaded.Select(Turn));
        borders.AddRange(localBorders.Select(Turn));
    }

    /// <summary>Where a line's first glyph stands along it: its first piece, else its mark where its paragraph aligns it.</summary>
    static double GlyphStart(Line line, ParagraphFormat format, double length) =>
        line.Pieces.Count > 0 ? line.Pieces.Min(p => p.X)
        : format.Justification switch
        {
            "center" => ((format.Left ?? 0) + length - (format.Right ?? 0)) / 2,
            "right" or "end" => length - (format.Right ?? 0),
            _ => format.Left ?? 0,
        };

    /// <summary>
    /// The lines of a turned frame laid out as vertical lines, as a turned text box's are (thicker; see PageRenderer.TextBox,
    /// measured with docx/local/vertical6; LibreOffice's extras-ooxmlexport/tdf154703_framePrTextDirection: a tbRl
    /// frame's 12 pt line with its baseline 12/64 pt deeper, on Word's dot 1692).
    /// </summary>
    T Turned<T>(bool turned, Func<T> lay)
    {
        var vertical = settings.Vertical;
        settings = settings with { Vertical = vertical || turned };
        try
        {
            return lay();
        }
        finally
        {
            settings = settings with { Vertical = vertical };
        }
    }

    /// <summary>
    /// Moves the last lines of the column that reach below <paramref name="from"/> down to <paramref name="to"/>, and
    /// the text after them, where nothing but their lines stands among them and they still fit in the column.
    /// </summary>
    void Lower(double from, double to, double left, double width)
    {
        var moving = new List<int>();
        for (var k = page!.Lines.Count - 1; k >= 0; k--)
        {
            var line = page.Lines[k];
            if (frameLines.Contains(line))
                continue;
            if (line.Top + line.Line.Height <= from + 0.01 || line.Top < columnTop - 0.01 || line.Left <= left - width / 2 || line.Left >= left + width)
                break;
            moving.Add(k);
        }
        if (moving.Count == 0)
            return;
        var first = moving.Min(k => page.Lines[k].Top);
        var shift = to - first;
        bool Among(double top, double bottom) => bottom > first + 0.01 && top < y - 0.01;
        if (shift <= 0 || y + shift > Bottom + 0.01
            || page.Shading.Concat(page.Borders).Any(f => Among(f.Y, f.Y + f.Height))
            || page.Strokes.Any(s => Among(Math.Min(s.Y1, s.Y2), Math.Max(s.Y1, s.Y2)))
            || page.Drawings.Any(d => Among(d.Y, d.Y + d.Height)))
            return;
        foreach (var k in moving)
            page.Lines[k] = page.Lines[k] with { Top = page.Lines[k].Top + shift };
        y += shift;
    }

    /// <summary>
    /// The top of the first line of the current column that an object's area overlaps (which the object pushes below
    /// it), or null where it overlaps none.
    /// </summary>
    double? Pushed(Exclusion e)
    {
        var (left, width) = Column(column);
        var tops = page!.Lines.Where(l => l.Top >= columnTop - 0.01 && l.Left > left - width / 2 && l.Left < left + width
            && l.Top < e.Bottom - 0.01 && l.Top + l.Line.Height > e.Top + 0.01).Select(l => l.Top).ToList();
        return tops.Count > 0 ? tops.Min() : null;
    }

    /// <summary>The lines of text frames placed in the body, which the text below them does not move (see Lower).</summary>
    readonly HashSet<PlacedLine> frameLines = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// What a text frame's w:y and w:yAlign count from (a floating table's w:tblpY and w:tblpYSpec with
    /// <paramref name="anchor"/> vertAnchor): w:vAnchor, the margin without it; a frame without a vertical position
    /// (w:y 0 and no w:yAlign or inline) stands at its paragraph before mode 15 whatever its anchor (measured with
    /// docx/local/fd-1, fd-3, fd-5: w:y 144 pt below the top margin, yAlign bottom on the bottom margin; ff-*, fg-*:
    /// without w:y on the top margin or the page's top in mode 15, at its paragraph in modes 12 and 14 also with
    /// vAnchor margin or page and y 0, yAlign inline as no position; fh-*: the same in headers; ta-*, tb-*: a table
    /// alike; docx/external/libreoffice/extras-ooxmlexport/special_styles, core-ooxml-pass/fdo81491,
    /// core-layout/floattable-deleted-anchor).
    /// </summary>
    string VerticalAnchor(XElement properties, string anchor = "vAnchor") =>
        context.Profile.UnplacedFramesAtParagraph && (properties.Twips(anchor == "vAnchor" ? "y" : "tblpY") ?? 0) == 0
            && (properties.Attr(anchor == "vAnchor" ? "yAlign" : "tblpYSpec") is null or "inline"
                // A frame's w:y 0 is no position whatever w:yAlign beside it says (measured with docx/local/fxa-c,
                // -d: y 0 and yAlign bottom at the paragraph in mode 14, with vAnchor margin too, on the top margin
                // in mode 15; tdf112287B).
                || anchor == "vAnchor" && properties.Attr("y") != null)
            ? "text"
            : properties.Attr(anchor) ?? "margin";

    /// <summary>The endnotes' continuation separator (w:endnote type continuationSeparator).</summary>
    public List<Block>? EndnoteContinuation { get; init; }

    /// <summary>The separator that heads each page the endnotes go on to while they are laid out.</summary>
    List<Block>? continuedEndnotes;

    /// <summary>
    /// What the runs before settled for the run in hand (see Paginate); a run's own holds stay its own (heldInTable,
    /// frameLines).
    /// </summary>
    sealed class Settled
    {
        /// <summary>The floating objects of each page from the run before, which this run's lines go around.</summary>
        public Dictionary<int, List<Exclusion>> Floats { get; set; } = [];

        /// <summary>The height the footnotes of each page with columns took in the run before.</summary>
        public Dictionary<int, double> NoteBands { get; set; } = [];

        /// <summary>
        /// Paragraphs that begin a new page (by Block.Id): the anchors of objects that push their own paragraph off the
        /// page they wrap text on, which Word leaves on the next page, the page before without them.
        /// </summary>
        public HashSet<int> ForcedBreaks { get; } = [];

        /// <summary>Drawings held where they stood first on a page, the runs before going back and forth (see Placed).</summary>
        public Dictionary<XElement, (int Page, double X, double Y)> Pins { get; } = new(ReferenceEqualityComparer.Instance);

        /// <summary>A copy, which the runs after do not change (see Checkpoint).</summary>
        public Settled Copy()
        {
            var copy = new Settled { Floats = Floats.ToDictionary(f => f.Key, f => f.Value.ToList()), NoteBands = new(NoteBands) };
            copy.ForcedBreaks.UnionWith(ForcedBreaks);
            foreach (var (source, pin) in Pins)
                copy.Pins[source] = pin;
            return copy;
        }
    }

    /// <summary>What the runs before settled; nothing before the first.</summary>
    Settled settled = new();

    /// <summary>
    /// The settings its paragraphs are laid out under (the section's document grid, the frame they stand in), each run
    /// going on from those the run before ended with.
    /// </summary>
    TextSettings settings = new();

    /// <summary>The settings the pagination ended with, which the text boxes drawn later are laid out under (PageRenderer).</summary>
    public TextSettings Settings => settings;

    /// <summary>
    /// Whether the runs take checkpoints to resume from (see Checkpoint); off unless asked for, as each checkpoint
    /// copies the pages before it.
    /// </summary>
    public bool KeepCheckpoints { get; init; }

    /// <summary>The checkpoints of the run whose pages Paginate returned, one on each page it could take one on, in order.</summary>
    public IReadOnlyList<Checkpoint> Checkpoints => checkpoints ?? [];

    /// <summary>The checkpoints of the run in hand (of its last run, for the paginator that runs them), or null where none are taken.</summary>
    List<Checkpoint>? checkpoints;

    /// <summary>The number of the page the last checkpoint went on on.</summary>
    int checkpointPage;

    /// <summary>What the runs before settled and what the fields knew of the last layout, as the run's checkpoints keep them.</summary>
    Settled? settledCopy;
    FieldEvaluator.Snapshot? fieldsCopy;

    /// <summary>The pages of the run whose pages Paginate returned as they were laid out, before they were finished (see PaginateRun).</summary>
    List<PageBox>? laid;

    /// <summary>What Paginate was given, which a run resumed from a checkpoint lays out on.</summary>
    (List<Block> Blocks, SectionProperties LastSection, Dictionary<string, List<Block>>? Footnotes, List<(int Section, List<Block> Blocks)>? Endnotes,
        List<Block>? EndnoteSeparator)? input;

    /// <summary>
    /// A point a run's flow can be taken up again from (see Resume): the first block boundary on a page, with what the
    /// run carries from block to block (the page in hand, where the next line goes, the space and the notes pending,
    /// what the runs before settled and what the fields knew). The pages before are done by then: Resume takes them
    /// from the run's pages. A checkpoint holds copies, so it can be taken up any number of times. None is taken where
    /// parts of floating tables wait for the next page (closures, see floatingRests) or the flow refers to an earlier
    /// page.
    /// </summary>
    public sealed class Checkpoint
    {
        internal Checkpoint(int page, int section, int block, int id, Paginator state, FieldEvaluator.Snapshot? fields) =>
            (Page, Section, Block, Id, State, Fields) = (page, section, block, id, state, fields);

        /// <summary>The number of the page the flow goes on on.</summary>
        public int Page { get; }

        /// <summary>The section and the block in it that the flow goes on with, and that block's id (Block.Id).</summary>
        public int Section { get; }
        public int Block { get; }
        public int Id { get; }

        /// <summary>The run as it stood, in a paginator of its own that does not run.</summary>
        internal Paginator State { get; }

        /// <summary>What the fields knew (see FieldEvaluator.Snapshot), or null without fields.</summary>
        internal FieldEvaluator.Snapshot? Fields { get; }
    }

    /// <summary>
    /// The pages of the run a checkpoint was taken in, laid out on from the checkpoint (as Paginate's last run, with
    /// line numbers): the pages before it as the checkpoint kept them, the rest laid out again, of the blocks Paginate
    /// was given or of <paramref name="blocks"/>, the same up to the checkpoint's (by position and id). With the same
    /// blocks they are the pages Paginate returned (the invariant that checks a resumed layout).
    /// </summary>
    /// <exception cref="ArgumentException">The checkpoint's block is not where <paramref name="blocks"/> have it.</exception>
    public List<PageBox> Resume(Checkpoint from, List<Block>? blocks = null)
    {
        var (given, lastSection, footnotes, endnotes, endnoteSeparator) = input ?? throw new InvalidOperationException("Resume needs a pagination.");
        if (laid is not { } done || done.Count < from.Page - 1)
            throw new InvalidOperationException("Resume needs the pages the checkpoint was taken on.");
        var run = new Paginator(context, layout, reader) { EndnoteContinuation = EndnoteContinuation };
        run.Take(from.State, done.Take(from.Page - 1));
        if (from.Fields != null)
            layout.Evaluator?.Restore(from.Fields);
        var resumed = run.PaginateOnce(blocks ?? given, lastSection, footnotes, endnotes, endnoteSeparator, from);
        NumberLines(resumed);
        return resumed;
    }

    /// <summary>
    /// Takes a checkpoint before block <paramref name="block"/> of section <paramref name="at"/>, whose id is
    /// <paramref name="id"/>: of the pages a copy of the one in hand; none where the flow refers to an earlier page (a
    /// stack of frames or notes kept there).
    /// </summary>
    void Keep(int at, int block, int id)
    {
        if (!ReferenceEquals(page, pages[^1]) || frameStack is { } stack && !ReferenceEquals(stack.Page, page)
            || keptNotes is { } kept && !ReferenceEquals(kept.Page, page))
            return;
        checkpointPage = page!.Number;
        var state = new Paginator(context, layout, reader) { EndnoteContinuation = EndnoteContinuation };
        state.Take(this, []);
        state.settled = settledCopy ??= settled.Copy();
        if (layout.Evaluator != null)
            fieldsCopy = layout.Evaluator.Take(fieldsCopy);
        checkpoints!.Add(new Checkpoint(page.Number, at, block, id, state, fieldsCopy));
    }

    /// <summary>
    /// Takes over another run's flow as it stands: as its pages copies of <paramref name="earlier"/> and of the page in
    /// hand (what refers to that page refers to its copy), its collections copied and the lines of its flow (their
    /// space after changes), the rest as it is; the closure over the column goes over this run's. The paragraphs,
    /// tables and notes a run keeps laid out are not taken: they are laid out again the same.
    /// </summary>
    void Take(Paginator source, IEnumerable<PageBox> earlier)
    {
        if (source.floatingRests.Count > 0 || source.mergedDown.Count > 0)
            throw new InvalidOperationException("A flow with parts of tables in hand cannot be taken over.");
        var current = source.page?.Copy();
        PageBox Copied(PageBox p) => ReferenceEquals(p, source.page) ? current! : throw new InvalidOperationException("The flow refers to a page before the one in hand.");
        pages.Clear();
        pages.AddRange(earlier.Select(p => p.Copy()));
        if (current != null)
            pages.Add(current);
        page = current;
        tables.PageArea = source.tables.PageArea;
        (section, sectionIndex, column, y, columnEmpty, columnTop) = (source.section, source.sectionIndex, source.column, source.y, source.columnEmpty, source.columnTop);
        flow.Clear();
        flow.AddRange(source.flow.Select(f => f with { }));
        (balanced, sectionBreakAfter, sectionShown, breakPage) = (source.balanced, source.sectionBreakAfter, source.sectionShown, source.breakPage);
        passedOver.Clear();
        passedOver.AddRange(source.passedOver);
        (paragraphNumber, top, pendingAfter, documentEnd, pendingOwnAfter) = (source.paragraphNumber, source.top, source.pendingAfter, source.documentEnd, source.pendingOwnAfter);
        (previous, stylePrevious) = (source.previous, source.stylePrevious);
        frameStack = source.frameStack is { } stack ? stack with { Page = Copied(stack.Page) } : null;
        frameLines.Clear();
        frameLines.UnionWith(source.frameLines);
        continuedEndnotes = source.continuedEndnotes;
        settled = source.settled;
        settings = source.settings.ColumnWidth != null ? source.settings with { ColumnWidth = () => Column(column).Width } : source.settings;
        Copy(reaching, source.reaching);
        Copy(heldInTable, source.heldInTable);
        (endnotesFrom, nextContinuous, chapter, columnsRightToLeft, footnotes) = (source.endnotesFrom, source.nextContinuous, source.chapter, source.columnsRightToLeft, source.footnotes);
        Copy(storyOwners, source.storyOwners);
        pageNotes.Clear();
        pageNotes.AddRange(source.pageNotes);
        notesHeight = source.notesHeight;
        (splitNote, carried, carriedHere, bodyEnd) = (source.splitNote, source.carried, source.carriedHere, source.bodyEnd);
        keptNotes = source.keptNotes is { } kept ? kept with { Page = Copied(kept.Page) } : null;
        unitNotes.Clear();
        unitNotes.AddRange(source.unitNotes);
        notesYield = source.notesYield;
        deferredNotes.Clear();
        deferredNotes.AddRange(source.deferredNotes);
        (tableKept, previousTable) = (source.tableKept, source.previousTable);

        static void Copy<TKey, TValue>(Dictionary<TKey, TValue> to, Dictionary<TKey, TValue> from) where TKey : notnull
        {
            to.Clear();
            foreach (var (key, value) in from)
                to[key] = value;
        }
    }

    /// <summary>Whether a drawing is placed above its paragraph or line, a negative offset from it.</summary>
    static bool Above(XElement anchor) =>
        anchor.Element(Docx.Drawings.Wp + "positionV") is { } position && (string?)position.Attribute("relativeFrom") is "paragraph" or "line"
        && double.TryParse(position.Element(Docx.Drawings.Wp + "posOffset")?.Value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var offset) && offset < 0;

    /// <summary>
    /// The drawings of this run placed above their paragraphs, wrapped top and bottom, over lines before them on the
    /// page, where they stand: Word leaves them there and takes the lines below them (measured with docx/local/npo-*,
    /// npe-*: a rectangle 0.5 to 5 pt above its paragraph over the line before, of text or empty, stays where the
    /// paragraph stood, the line below it, in modes 14 and 15; LibreOffice's extras-layout/fdo43573-2-min: a picture
    /// 0.91 pt above an empty paragraph, the empty one before it below the picture). Settled as pins (Settled.Pins).
    /// </summary>
    readonly Dictionary<XElement, (int Page, double X, double Y)> reaching = new(ReferenceEqualityComparer.Instance);

    /// <summary>A table's own drawings held where its first layout put them, the table below them (see PlaceTable).</summary>
    readonly Dictionary<XElement, (int Page, double X, double Y)> heldInTable = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// The area a floating object keeps free of text (catalog: drawing.wrap): its box with the effect extents and the
    /// distances around it; square wrapping keeps the box, top and bottom the whole width, tight and through wrapping
    /// the wrap polygon's width beside each line's text with the distances left and right of it.
    /// </summary>
    public sealed record Exclusion(double Left, double Top, double Right, double Bottom, bool TopAndBottom, string Side)
    {
        /// <summary>The wrap polygon on the page (tight and through wrapping), or null for the box.</summary>
        public (double X, double Y)[]? Polygon { get; init; }

        /// <summary>The drawing's element, which finds the same object again in the next run.</summary>
        public XElement? Source { get; init; }

        public double DistanceLeft { get; init; }

        /// <summary>A wrap polygon's distances from the text above and below it (distT, distB), see Room.</summary>
        public double DistanceTop { get; init; }
        public double DistanceBottom { get; init; }
        public double DistanceRight { get; init; }

        /// <summary>Whether the area is a text frame's (w:framePr).</summary>
        public bool Frame { get; init; }

        /// <summary>Whether the area is a drop cap's (w:framePr w:dropCap drop), whose room moves the first line beside it (LineRoom.DropCap).</summary>
        public bool DropCap { get; init; }

        /// <summary>Whether the area is a floating table's of the header or footer.</summary>
        public bool Story { get; init; }

        /// <summary>The block a floating table stands with (the one after it), whose space after counts against it before Word 2013.</summary>
        public Block? Anchor { get; init; }

        /// <summary>Whether the area is a text frame's standing with the text (w:vAnchor text), not on the page.</summary>
        public bool WithText { get; init; }

        /// <summary>The narrowest room beside the area that takes text (see FrameRoom), or 0 for any.</summary>
        public double NarrowestRoom { get; init; }

        /// <summary>The floating table whose area it is, where the lines before it go around it (see PlaceFloatingTable), else null.</summary>
        public Block? Banded { get; init; }

        /// <summary>
        /// Whether text goes right of the area where its wrapText "left" leaves no room left of it (before Word 2013;
        /// measured with docx/local/wsd-14, wsd2-14: a rectangle at the column's left edge has the lines beside it on
        /// its right, from Word 2013 on below it, wsd-15; one wrapped "right" at the right edge has none on its left).
        /// </summary>
        public bool OtherSide { get; init; }
    }

    Exclusion? ExclusionOf(PlacedDrawing drawing)
    {
        var element = drawing.Drawing;
        var box = new DrawingBox(element);
        if (!box.Wraps)
            return null;
        var (el, et, er, eb) = box.Effect;
        var (dl, dt, dr, db) = box.Distance;
        var side = box.Side;
        var otherSide = side == "left" && context.Profile.WrapsLeftOnOtherSide;
        // Tight and through wrapping follow the wrap polygon, in 21600ths of the drawing's extent (measured with
        // docx/generated/wrap-types: text beside an oval and a triangle starts the right distance from the polygon).
        if (box.Polygon is { } points)
        {
            var onPage = points.Select(p => (drawing.X + p.X * drawing.Width, drawing.Y + p.Y * drawing.Height)).ToArray();
            // The polygon turns with its shape, about the extent's centre (found by agent BF in Word's PDF of
            // docx/external/libreoffice/writerfilter-dmapper/tdf143208_wrapTight: a moon turned by 90 degrees has the
            // text along its turned outline, 80.5 -> 99.9 %).
            var xfrm = element.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Elements().FirstOrDefault()?
                .Elements().FirstOrDefault(e => e.Name.LocalName is "spPr" or "grpSpPr")?.Element(Ns.A + "xfrm");
            if (double.TryParse((string?)xfrm?.Attribute("rot"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rot) && rot % 21600000 != 0)
            {
                var (cos, sin) = (Math.Cos(rot / 60000 * Math.PI / 180), Math.Sin(rot / 60000 * Math.PI / 180));
                var (cx, cy) = (drawing.X + drawing.Width / 2, drawing.Y + drawing.Height / 2);
                onPage = onPage.Select(p => (cx + cos * (p.Item1 - cx) - sin * (p.Item2 - cy), cy + sin * (p.Item1 - cx) + cos * (p.Item2 - cy))).ToArray();
            }
            return new Exclusion(onPage.Min(p => p.Item1) - dl, onPage.Min(p => p.Item2) - dt, onPage.Max(p => p.Item1) + dr, onPage.Max(p => p.Item2) + db, false, side)
            {
                DistanceTop = dt,
                DistanceBottom = db,
                Polygon = onPage,
                DistanceLeft = dl,
                DistanceRight = dr,
                Source = element,
                OtherSide = otherSide,
            };
        }
        return new Exclusion(drawing.X - el - dl, drawing.Y - et - dt, drawing.X + drawing.Width + er + dr, drawing.Y + drawing.Height + eb + db,
            box.TopAndBottom, side) { Source = element, OtherSide = otherSide, DistanceLeft = el + dl, DistanceRight = er + dr };
    }

    /// <summary>Whether blocks of the text's flow stand before the one at <paramref name="index"/>: all but frames, also
    /// tables in frames (floating tables of w:tblpPr count: docx/external/libreoffice/core-layout/
    /// floattable-next-leaf-in-section, the empty paragraph after one anchored to the text, ending the section, takes
    /// no line).</summary>
    static bool FlowBefore(List<Block> blocks, int index) =>
        blocks.Take(index).Any(b => b is ParagraphBlock { Format.Frame: null } || b is TableBlock { Table.Format.Framed: false });

    /// <summary>A paragraph's drawings anchored before its first page or column break.</summary>
    static List<AnchorInline> FirstPartAnchors(ParagraphBlock paragraph)
    {
        var anchors = new List<AnchorInline>();
        foreach (var inline in paragraph.Content)
        {
            if (inline is BreakInline { Type: "page" or "column" })
                break;
            if (inline is AnchorInline anchor)
                anchors.Add(anchor);
        }
        return anchors;
    }

    /// <summary>The height of a placed drawing with its effect extents above and below it.</summary>
    static double ExtentHeight(PlacedDrawing drawing)
    {
        var (_, top, _, bottom) = new DrawingBox(drawing.Drawing).Effect;
        return drawing.Height + top + bottom;
    }

    /// <summary>The leftmost and rightmost points of a polygon between two heights, or null where it does not reach.</summary>
    static (double Min, double Max)? Span((double X, double Y)[] polygon, double top, double bottom)
    {
        double? min = null, max = null;
        void Take(double x)
        {
            min = min is { } m ? Math.Min(m, x) : x;
            max = max is { } n ? Math.Max(n, x) : x;
        }
        for (var i = 0; i < polygon.Length; i++)
        {
            var (a, b) = (polygon[i], polygon[(i + 1) % polygon.Length]);
            if (a.Y >= top && a.Y <= bottom)
                Take(a.X);
            // Where the edge crosses the band's top and bottom.
            foreach (var y in (double[])[top, bottom])
            {
                if ((a.Y - y) * (b.Y - y) < 0)
                    Take(a.X + (b.X - a.X) * (y - a.Y) / (b.Y - a.Y));
            }
        }
        return min is { } lo && max is { } hi ? (lo, hi) : null;
    }

    /// <summary>
    /// The floating objects of the current page this run knows: those placed so far where they stand now, the others
    /// where the run before placed them.
    /// </summary>
    List<Exclusion> PageExclusions()
    {
        var placed = page!.Drawings.Select(ExclusionOf).OfType<Exclusion>().Concat(page.FloatingTables).ToList();
        var list = settled.Floats.TryGetValue(page.Number, out var before)
            ? before.Where(e => !placed.Any(p => ReferenceEquals(p.Source, e.Source) && e.Source != null)).ToList()
            : [];
        foreach (var exclusion in placed)
        {
            if (!list.Any(e => Math.Abs(e.Left - exclusion.Left) < 0.01 && Math.Abs(e.Top - exclusion.Top) < 0.01))
                list.Add(exclusion);
        }
        return list;
    }

    /// <summary>
    /// The room a line has beside the floating objects: the column less every object its box overlaps, keeping the
    /// sides the object's wrapText allows; where none is left, how far down the line must go.
    /// </summary>
    internal static LineRoom Room(List<Exclusion> exclusions, double paragraphTop, LineSlot slot, double columnLeft, double columnWidth)
    {
        var top = paragraphTop + slot.Offset;
        var height = slot.Height;
        var segments = new List<(double Start, double End)> { (0, columnWidth) };
        var skipTo = 0.0;
        // A line beside objects without room left tries again below the first of them to end (measured with
        // docx/local/td-1: a paragraph below a table on the right and beside one moved below it stands right of the
        // second as soon as the first ends), beside one wrapped along its polygon a line further down (ub-1 to ub-5,
        // ub-7: below a shape wrapped through or tight the first line stands on the paragraph's line pitch, square
        // right at its bottom, ub-6).
        var nextBottom = double.MaxValue;
        var (beside, tight) = (false, true);
        double? dropCap = null;
        foreach (var e in exclusions)
        {
            // Objects beside the column leave it alone, also those kept free above and below (measured with
            // docx/external/libreoffice/extras-layout/fdo43573-2-min: an image wrapped top and bottom in the left column
            // of a page, the right column's text beside it); an empty frame at its edge does not
            // (extras-ooxmlexport/fdo78904).
            if (top >= e.Bottom - 0.01 || top + height <= e.Top + 0.01 || e.Right < columnLeft - 0.01 || e.Left > columnLeft + columnWidth + 0.01)
                continue;
            nextBottom = Math.Min(nextBottom, e.Polygon != null ? slot.Offset + slot.Height : e.Bottom - paragraphTop);
            var (l, r) = (e.Left - columnLeft, e.Right - columnLeft);
            // A wrap polygon keeps free what of it lies beside the line's text.
            if (e.Polygon != null)
            {
                // Its distances above and below widen the line's band (found by agents AG and CD in Word's PDFs: LibreOffice's
                // extras-ooxmlexport/tdf143219ContourWrap_glow_rotate, wrapped through 8.95 pt above and 9 pt below, 19 line
                // starts within 0.2 pt).
                if (Span(e.Polygon, top + slot.TextTop - e.DistanceBottom, top + slot.TextBottom + e.DistanceTop) is not { } span)
                    continue;
                (l, r) = (span.Min - e.DistanceLeft - columnLeft, span.Max + e.DistanceRight - columnLeft);
            }
            beside = true;
            tight &= e.Polygon != null;
            if (e.DropCap)
                dropCap = Math.Max(dropCap ?? 0, r);
            // Objects kept free above and below, and after a break with w:clear those of the side it clears (an
            // object on the left half of the column is on the left), take the whole line: it moves below them.
            var center = (e.Left + e.Right) / 2 - columnLeft;
            if (e.TopAndBottom || slot.Clear == "all" || slot.Clear == "left" && center < columnWidth / 2 || slot.Clear == "right" && center >= columnWidth / 2)
            {
                segments.Clear();
                skipTo = Math.Max(skipTo, e.Bottom - paragraphTop);
                continue;
            }
            var next = new List<(double Start, double End)>();
            foreach (var (start, end) in segments)
            {
                var before = (start, Math.Min(end, l));
                var after = (Math.Max(start, r), end);
                var keepBefore = before.Item2 > before.start;
                var keepAfter = after.end > after.Item1;
                // The room beside a frame counts as if it stood a twip further left (see FrameRoom).
                if (e.NarrowestRoom > 0)
                {
                    keepBefore &= before.Item2 - before.start - 0.05 >= e.NarrowestRoom - 0.001;
                    keepAfter &= after.end - after.Item1 + 0.05 >= e.NarrowestRoom - 0.001;
                }
                switch (e.Side)
                {
                    case "left": keepAfter = e.OtherSide && !keepBefore; break;
                    case "right": keepBefore = false; break;
                    case "largest":
                        if (keepBefore && keepAfter)
                        {
                            if (before.Item2 - before.start >= after.end - after.Item1) keepAfter = false;
                            else keepBefore = false;
                        }
                        break;
                }
                if (r <= start || l >= end)
                {
                    next.Add((start, end));
                    continue;
                }
                if (keepBefore) next.Add(before);
                if (keepAfter) next.Add(after);
            }
            segments = next;
        }
        // Where the line tries again when its room is too narrow: below the first object beside it to end.
        if (skipTo == 0 && nextBottom < double.MaxValue)
            skipTo = nextBottom;
        return new LineRoom(segments, skipTo) { Tight = beside && tight, DropCap = dropCap };
    }

    /// <summary>
    /// The narrowest room beside a text frame that takes text: before Word 2013 an inch, counted as if the frame stood a
    /// twip further left (measured with docx/local/frw-*, frx-*, fry-*: 1441 twips between the left margin and a frame's
    /// hSpace take text, 1440 not, 1439 between its hSpace and the right margin, 1438 not, whatever the frame's width,
    /// its hSpace and the margins, in modes 12 and 14; hfs-12-*: beside the frames of headers too), from then on any
    /// room its line's first word fits in (hfs-15-*: "The body" in 62.65 pt left of a frame; see
    /// ParagraphLayout.MinimumSegment).
    /// </summary>
    double FrameRoom => context.Profile.FrameRoomFromInch ? 72 : 0;

    /// <param name="from">The checkpoint the run goes on from (see Resume), or null for the run from the beginning.</param>
    List<PageBox> PaginateOnce(List<Block> blocks, SectionProperties lastSection, Dictionary<string, List<Block>>? footnotes = null,
        List<(int Section, List<Block> Blocks)>? endnotes = null, List<Block>? endnoteSeparator = null, Checkpoint? from = null)
    {
        // The paragraphs this run lays out ask it whether their page breaks go unseen (a story's run in between asks
        // its own).
        var outer = settings.BreakUnseen;
        settings = settings with { BreakUnseen = BreakUnseen };
        try
        {
            return PaginateRun(blocks, lastSection, footnotes, endnotes, endnoteSeparator, from);
        }
        finally
        {
            settings = settings with { BreakUnseen = outer };
        }
    }

    List<PageBox> PaginateRun(List<Block> blocks, SectionProperties lastSection, Dictionary<string, List<Block>>? footnotes,
        List<(int Section, List<Block> Blocks)>? endnotes, List<Block>? endnoteSeparator, Checkpoint? from)
    {
        this.footnotes = footnotes;
        // The pages of this run so far, which the fields of a header look back into (STYLEREF).
        if (layout.Evaluator != null)
            layout.Evaluator.Current = pages;
        // Each section's properties are given at its end.
        var sections = new List<(List<Block> Blocks, SectionProperties Properties)>();
        var current = new List<Block>();
        foreach (var block in blocks)
        {
            current.Add(block);
            if (block is ParagraphBlock { EndsSection: { } ends })
            {
                sections.Add((current, ends));
                current = [];
            }
        }
        sections.Add((current, lastSection));
        foreach (var (_, own) in sections)
        {
            foreach (var part in own.Headers.Values.Concat(own.Footers.Values))
                storyOwners.TryAdd(part, own);
        }
        // A section without a header or footer of a type has the previous section's (ISO/IEC 29500 17.10.5).
        for (var s = 1; s < sections.Count; s++)
        {
            foreach (var (type, part) in sections[s - 1].Properties.Headers)
                sections[s].Properties.Headers.TryAdd(type, part);
            foreach (var (type, part) in sections[s - 1].Properties.Footers)
                sections[s].Properties.Footers.TryAdd(type, part);
        }

        for (var s = from?.Section ?? 0; s < sections.Count; s++)
        {
            var (sectionBlocks, properties) = sections[s];
            sectionIndex = s;
            // Vertical text is laid out on the page turned a quarter (catalog: paragraph.bidi).
            if (properties.Vertical)
                properties = properties.Turned();
            // A run resumed from a checkpoint goes on in the section the checkpoint was taken in.
            if (from == null || s > from.Section)
                StartSection(properties, s == 0);
            nextContinuous = s + 1 < sections.Count && sections[s + 1].Properties.Start == "continuous";
            // Endnotes follow the document's last paragraph, after their separator, or each section's where the
            // settings put them at the end of sections (w:endnotePr/w:pos sectEnd), unless the section has none
            // (w:noEndnote; catalog: note.endnotes): after the text of the paragraph that ends the section, an empty
            // one taking no line before them (measured with docx/local/fnpages-c and -e).
            var atSectionEnds = context.Settings.Child("endnotePr").Child("pos").Val() == "sectEnd" && properties.Element.Child("noEndnote").OnOff() != true;
            var due = endnotes?.Where(n => n.Section >= endnotesFrom && (s == sections.Count - 1 || atSectionEnds && n.Section <= s)).ToList();
            var emptyEnd = due is { Count: > 0 } && sectionBlocks.Count > 0 && sectionBlocks[^1] is ParagraphBlock { EndsSection: not null } end
                && end.Content.All(c => c is BookmarkInline or PermissionInline);
            documentEnd = s == sections.Count - 1 && due is not { Count: > 0 } && sectionBlocks.Count > 0 ? sectionBlocks[^1] : null;
            if (from != null && s == from.Section && (from.Block >= sectionBlocks.Count || sectionBlocks[from.Block].Id != from.Id))
                throw new ArgumentException("The checkpoint's block is not where the blocks have it.", nameof(from));
            for (var i = from != null && s == from.Section ? from.Block : 0; i < sectionBlocks.Count - (emptyEnd ? 1 : 0); i++)
            {
                // A checkpoint on each page, before the first block placed there that no parts of floating tables wait for.
                if (checkpoints != null && page!.Number != checkpointPage && floatingRests.Count == 0)
                    Keep(s, i, sectionBlocks[i].Id);
                if (sectionBlocks[i] is TableBlock)
                    PlaceTable(sectionBlocks, i);
                else if (sectionBlocks[i] is ParagraphBlock { Format.Frame: not null, EndsSection: null })
                    i = PlaceFrame(sectionBlocks, i) - 1;
                else
                    Place(sectionBlocks, i);
            }
            if (due is { Count: > 0 })
            {
                endnotesFrom = s + 1;
                // Footnotes beneath the text come right after it, before the endnotes, which follow below them
                // (docx/external/libreoffice/extras-ooxmlexport/fdo79062: three footnotes, then the endnotes' separator);
                // from Word 2013 on below the endnotes (measured with docx/local/fbt-14, fbt-15: the footnote above the
                // endnote in mode 14, below it in mode 15).
                if (context.Profile.FootnotesAboveEndnotes && page?.Section.Element.Child("footnotePr").Child("pos").Val() == "beneathText"
                    && (pageNotes.Count > 0 || carriedHere != null))
                {
                    y = PlaceNotes();
                    (pendingAfter, pendingOwnAfter) = (0, 0);
                }
                var trailing = new List<Block>();
                trailing.AddRange(endnoteSeparator ?? []);
                foreach (var note in due)
                    trailing.AddRange(note.Blocks);
                var pitch = settings.LinePitch;
                for (var i = 0; i < trailing.Count; i++)
                {
                    // The separator's paragraphs keep off the line grid (see Note).
                    settings = settings with { LinePitch = i < (endnoteSeparator?.Count ?? 0) ? null : pitch };
                    continuedEndnotes = i < (endnoteSeparator?.Count ?? 0) ? null : EndnoteContinuation;
                    if (trailing[i] is TableBlock)
                        PlaceTable(trailing, i);
                    else
                        Place(trailing, i);
                }
                continuedEndnotes = null;
                settings = settings with { LinePitch = pitch };
            }
            if (emptyEnd)
                Place(sectionBlocks, sectionBlocks.Count - 1);
            // A continuous section break balances the columns above it, before Word 2013 unless w:noColumnBalance
            // (measured with docx/local/colsect-h, -i: mode 12 leaves the lines in the first column, mode 15 balances).
            if (s + 1 < sections.Count && sections[s + 1].Properties.Start == "continuous" && section.Columns > 1
                && context.Profile.BalancesColumns)
                Balance();
            CloseColumns();
        }
        if (pages.Count == 0)
            NewPage(lastSection, firstOfSection: true);
        // The parts of floating tables the text did not reach take pages of their own (see PlaceFloatingTable).
        for (var extra = 0; extra < 100 && floatingRests.Count > 0; extra++)
            NewPage(section);
        PlaceNotes();
        // Notes that go on past the last page of text fill pages of their own.
        for (var extra = 0; extra < 20 && (carried != null || deferredNotes.Count > 0); extra++)
        {
            NewPage(section);
            PlaceNotes();
        }
        // The pages as laid out stay as they are, for a run resumed from a checkpoint to go on from; the pages it returns
        // are copies, aligned, mirrored and with their headers and footers, whose fields look back on them (STYLEREF).
        var finished = pages.Select(p => p.Copy()).ToList();
        if (layout.Evaluator != null)
            layout.Evaluator.Current = finished;
        foreach (var p in finished)
        {
            AlignVertically(p);
            if (p.Section is { Upright: not null, LinesFromLeft: true })
                Mirror(p);
        }
        if (reader != null)
        {
            // NUMPAGES counts the blank pages, SECTIONPAGES does not (measured with docx/local/hf-parity).
            var sectionPages = finished.Where(p => !p.Blank).GroupBy(p => p.SectionIndex).ToDictionary(g => g.Key, g => g.Count());
            foreach (var p in finished)
            {
                var fields = new PageFields(p.DisplayNumber, p.Section.PageNumberFormat, finished.Count, sectionPages.GetValueOrDefault(p.SectionIndex)) { Chapter = p.ChapterPrefix, On = p };
                // Headers and footers of pages of vertical text stand upright (measured with docx/local/vertical2).
                var on = p.Upright ?? p;
                if (p.Header != null)
                    Story(p.Header, on, fields, header: true).AddTo(on);
                if (p.Footer != null)
                    Story(p.Footer, on, fields, header: false).AddTo(on);
            }
        }
        return finished;
    }

    /// <summary>The first section whose endnotes are still to come.</summary>
    int endnotesFrom;

    /// <summary>Whether the section after the one being placed begins on the same page (continuous).</summary>
    bool nextContinuous;

    /// <summary>
    /// Whether a paragraph at <paramref name="index"/> of its section is an empty one holding the section break after
    /// other text of its section, which takes no room before a continuous section (see Place).
    /// </summary>
    bool TakesNoRoom(ParagraphBlock paragraph, int index) =>
        paragraph is { EndsSection: not null, Format.Frame: null } && index > 0 && nextContinuous && paragraph.Label is not { Own.Length: > 0 }
        && paragraph.Content.All(c => c is BookmarkInline or BookmarkEndInline or PermissionInline);

    /// <summary>
    /// Whether a paragraph is an empty one holding the break before a section on a new page, which stays where it is
    /// at a page's end, no page of its own (see Place).
    /// </summary>
    bool MarkBeforePage(ParagraphBlock paragraph) =>
        paragraph is { EndsSection: not null, Format.Frame: null } && !nextContinuous && paragraph.Label is not { Own.Length: > 0 }
        && paragraph.Content.All(c => c is BookmarkInline or BookmarkEndInline or PermissionInline);

    /// <summary>
    /// Whether line <paramref name="k"/> of a paragraph that ends a section is its last and holds nothing but the
    /// paragraph's mark, after a line break: it stays at a page's end, past the bottom too, and the lines before it need
    /// no room for it (measured with docx/local/sbe-br, sbe-brc: "Line 29" and a line break end a full page whether
    /// the next section begins on a new page or goes on; sbe-brp: a paragraph without the break goes on whole with
    /// widow control; sbe-brt: one with "tail" after the break too; docx/external/libreoffice/extras-ooxmlexport/
    /// tdf126287).
    /// </summary>
    static bool MarkLine(ParagraphBlock paragraph, List<Line> lines, int k) =>
        paragraph is { EndsSection: not null, Format.Frame: null } && k > 0 && k == lines.Count - 1
        && lines[k].Pieces.All(p => p.Drawing == null && !p.IsLabel && string.IsNullOrWhiteSpace(p.Text));

    /// <summary>The number of the last chapter heading placed (section.pageNumbers).</summary>
    string chapter = "";

    void StartSection(SectionProperties properties, bool first)
    {
        var start = first ? "nextPage" : properties.Start;
        // A section whose text runs another way begins a new page.
        if (page != null && (page.Section.Upright != null) != (properties.Upright != null))
            start = "nextPage";
        // A section on the next column with another number of columns than the section before begins a new page
        // (measured with docx/local/ncs-13-12, -14, -15: three columns after one begin page 2, the next three in its
        // second column; ncs-22-15; docx/external/libreoffice/extras-ooxmlexport/tdf135343_columnSectionBreak_c14v2;
        // _c14, _c15: three after three on a page that two columns began go on in its columns).
        if (start == "nextColumn" && page != null && section.Columns != properties.Columns)
            start = "nextPage";
        // So does a continuous section on paper of another size (docx/external/libreoffice/core-doc/
        // header-footer-delete: a Letter section after an A4 one on page 3).
        if (start is "continuous" or "nextColumn" && page != null
            && (Math.Abs(page.Section.PageWidth - properties.PageWidth) > 0.5 || Math.Abs(page.Section.PageHeight - properties.PageHeight) > 0.5))
            start = "nextPage";
        // A continuous section on a page a page break began that holds nothing yet takes that page as its own, its
        // headers too (LibreOffice's core-unocore/tdf149555: a page break ending section 1, HEADER 2 on page 2).
        if (start == "continuous" && !first && breakPage && page is { Lines.Count: 0 } && pageNotes.Count == 0 && pages.Count > 1
            && ReferenceEquals(pages[^1], page))
            start = "nextPage";
        section = properties;
        if (start != "nextColumn")
            columnsRightToLeft = properties.Element?.Child("bidi").OnOff() == true;
        tables.PageArea = (properties.PageWidth - properties.Left - properties.Right - properties.Gutter, properties.PageHeight - properties.Top - properties.Bottom,
            properties.PageWidth, properties.PageHeight);
        settings = settings with
        {
            Vertical = properties.Upright != null,
            TurnEastAsian = properties.TurnEastAsian,
            LinePitch = GridPitch(properties),
            CharacterExtra = properties.Element.Child("docGrid") is { } charGrid && charGrid.Attr("type") is "linesAndChars" or "snapToChars"
                ? (charGrid.Number("charSpace") ?? 0) / 4096 : 0,
            CharacterExtraEastAsian = properties.Element.Child("docGrid")?.Attr("type") == "snapToChars",
            GridCell = context.Profile.CharacterGridCells && properties.Element.Child("docGrid") is { } cellGrid && cellGrid.Attr("type") is "linesAndChars" or "snapToChars"
                ? (context.Styles.Run(null, null, null).Size ?? 10) + (cellGrid.Number("charSpace") ?? 0) / 4096 : null,
            SnapCell = properties.Element.Child("docGrid") is { } snapGrid && snapGrid.Attr("type") == "snapToChars"
                ? (context.Styles.Run(null, null, null).Size ?? 10) + (snapGrid.Number("charSpace") ?? 0) / 4096 : null,
        };
        if (page == null || start != "continuous" && start != "nextColumn")
        {
            // A page a page break began that holds nothing yet is the section's first page (measured with
            // docx/local/pbs-*: a page break right before a section break on a new page leaves no empty page, also
            // where the section is landscape or the break ends a paragraph with text, one of two page breaks does).
            if (!first && breakPage && page is { Lines.Count: 0 } && pageNotes.Count == 0 && pages.Count > 1 && ReferenceEquals(pages[^1], page))
            {
                passedOver.AddRange(page.Passed);
                pages.RemoveAt(pages.Count - 1);
                page = pages[^1];
            }
            // Odd page numbers are right-hand pages: an odd or even page section leaves a blank page where the next
            // number has the wrong parity, and with different odd and even headers or mirrored margins so does any
            // section whose first number has the parity of the page before (measured with docx/local/hf-oddpage,
            // hf-restart-*: page numbers decide, not the sheets).
            if (!first && pages.Count > 0)
            {
                var previousNumber = pages[^1].DisplayNumber;
                var next = properties.PageNumberStart ?? previousNumber + 1;
                var sides = context.Settings.Child("evenAndOddHeaders").OnOff() == true || context.Settings.Child("mirrorMargins").OnOff() == true;
                if (start == "oddPage" && next % 2 == 0 || start == "evenPage" && next % 2 == 1 || sides && next % 2 == previousNumber % 2)
                    NewPage(pages[^1].Section, blank: true);
            }
            NewPage(properties, firstOfSection: true);
            sectionShown = true;
            top = first ? Top.Document : Top.Section;
            return;
        }
        // A section on the next column goes on in the columns of the one before, at the top of the column after the
        // current one, on the next page after the last (measured with docx/local/ncs-22-15: "Next" level with "Two" in
        // the second column, "Again" at the top of page 2; ncs-13-*: the second three-column section in the second
        // column).
        if (start == "nextColumn")
        {
            sectionShown = false;
            top = Top.Continuous;
            flow.Clear();
            NextColumn(natural: false);
            return;
        }
        // A continuous section goes on below what the page holds already and the space after its last paragraph,
        // where all its columns begin; its first paragraph adds what its space before has more (measured with
        // docx/local/colsp-*: a second column begins level with the first below 12 pt after, without 24 pt before;
        // balanced columns above hold that space already; at the top of a page nothing, docx/local/pbs-4).
        if (!balanced && !columnEmpty)
            y += pendingAfter;
        columnTop = y;
        column = 0;
        columnEmpty = true;
        sectionShown = false;
        top = Top.Continuous;
        flow.Clear();
    }

    /// <param name="blank">A page left blank before a section: it has a number but no header or footer.</param>
    void NewPage(SectionProperties properties, bool firstOfSection = false, bool blank = false)
    {
        breakPage = false;
        CloseColumns();
        PlaceNotes();
        // A document whose first section begins on an even page begins at page 2, an even page, with no blank page
        // before it (docx/external/libreoffice/core-header_footer/tdf135216_evenOddFooter: four pages numbered 2 to 5,
        // the first with the even footer; extras-ooxmlexport/A019_min: margins mirrored for an even page).
        var number = firstOfSection && properties.PageNumberStart is { } restart ? restart
            : pages.Count == 0 ? properties.Start == "evenPage" ? 2 : 1 : pages[^1].DisplayNumber + 1;
        // The first page of a section with a title page has the first header, also the page a continuous section
        // begins at the top of (measured with docx/local/hfc-0t: not where lines of the section stand on the page
        // before, hfc-2t); with different odd and even pages (w:evenAndOddHeaders) even pages have the even one; a
        // type the sections do not give is an empty header.
        var type = (firstOfSection || !sectionShown) && properties.TitlePage ? "first"
            : number % 2 == 0 && context.Settings.Child("evenAndOddHeaders").OnOff() == true ? "even" : "default";
        // Mirrored margins swap the left and right margins of even pages; the gutter is on the inside, at the top
        // (w:gutterAtTop) or on the right (w:rtlGutter).
        var (left, width) = TextArea(properties, number);
        var gutterAtTop = context.Settings.Child("gutterAtTop").OnOff() == true;
        page = new PageBox
        {
            Number = pages.Count + 1,
            Section = properties,
            DisplayNumber = number,
            SectionIndex = sectionIndex,
            FirstOfSection = firstOfSection,
            Blank = blank,
            Chapter = chapter,
            Header = reader != null && !blank ? properties.Headers.GetValueOrDefault(type) : null,
            Footer = reader != null && !blank ? properties.Footers.GetValueOrDefault(type) : null,
            TextLeft = left,
            TextWidth = Math.Max(1, width),
        };
        if (!blank)
        {
            page.Passed.AddRange(passedOver);
            passedOver.Clear();
        }
        settings = settings with { ColumnWidth = () => Column(column).Width };
        // A header that reaches below the top margin pushes the body down, a footer above the bottom margin pushes
        // its end up, each with the space before and after its paragraphs, unless the margin is negative (fixed;
        // measured with docx/local/hf-tall, hf-short).
        // Two pages per sheet (w:printTwoOnOne) swap the top and bottom margins of odd pages (measured with
        // docx/local/print-twoonone: their text starts at the bottom margin's distance; book fold keeps them).
        var swapped = number % 2 == 1 && context.Settings.Child("printTwoOnOne").OnOff() == true;
        page.BodyTop = (swapped ? properties.Bottom : properties.Top) + (gutterAtTop ? properties.Gutter : 0);
        page.BodyBottom = properties.PageHeight - (swapped ? properties.Top : properties.Bottom);
        if (properties.Upright is { } upright)
        {
            var (uprightLeft, uprightWidth) = TextArea(upright, number);
            page.Upright = new PageBox
            {
                Number = page.Number, Section = upright, DisplayNumber = number, SectionIndex = sectionIndex, FirstOfSection = firstOfSection,
                Blank = blank, Chapter = chapter, Header = page.Header, Footer = page.Footer, TextLeft = uprightLeft, TextWidth = uprightWidth,
                BodyTop = upright.Top, BodyBottom = upright.PageHeight - upright.Bottom,
            };
        }
        else
        {
            if (page.Header != null && !properties.TopFixed)
            {
                // The header's fields that look at the page's paragraphs (STYLEREF) see this page.
                if (layout.Evaluator != null)
                    layout.Evaluator.Laying = page;
                var headerBox = Story(page.Header, page, null, header: true);
                page.BodyTop = Math.Max(page.BodyTop, headerBox.Bottom);
                // What a header or footer keeps free above or below the body's text leaves it alone, also the lines
                // there that go on on the next page (docx/external/libreoffice/core-ooxml-pass/fdo78904_fileHang: a
                // footer's page number framed below the body leaves the line breaks of the paragraph at its end).
                page.FloatingTables.AddRange(headerBox.Floats.Where(f => f.Bottom > page.BodyTop + 0.01));
                page.FloatingTables.AddRange(StoryObstacles(headerBox));
                if (layout.Evaluator != null)
                    layout.Evaluator.Laying = null;
            }
            if (page.Footer != null && !properties.BottomFixed)
            {
                var footerBox = Story(page.Footer, page, null, header: false);
                page.BodyBottom = Math.Min(page.BodyBottom, footerBox.Top);
                page.FloatingTables.AddRange(footerBox.Floats.Where(f => f.Top < page.BodyBottom - 0.01));
                page.FloatingTables.AddRange(StoryObstacles(footerBox));
            }
        }
        page.Border = PageBorders.Layout(page.Upright ?? page, context.Settings);
        pages.Add(page);
        column = 0;
        y = page.BodyTop;
        columnTop = y;
        columnEmpty = true;
        flow.Clear();
        if (!blank)
            ContinueNotes();
        // Endnotes that go on to a page have their continuation separator at its top (found by agent BE in Word's PDFs
        // of docx/external/libreoffice/core-layout/endnote-cont-separator and inline-endnote-section-delete: a rule
        // from the left indent to the right margin above the first line of each such page, a line of the separator's
        // mark high).
        if (!blank && continuedEndnotes is { Count: > 0 } continuation)
        {
            var pitch = settings.LinePitch;
            settings = settings with { LinePitch = null };
            var (lines, fills, strokes, height) = tables.Blocks(continuation, page.TextWidth, settings, null);
            settings = settings with { LinePitch = pitch };
            foreach (var line in lines)
                page.Lines.Add(line with { Left = page.TextLeft + line.Left, Top = y + line.Top });
            foreach (var fill in fills)
                page.Borders.Add(fill with { X = page.TextLeft + fill.X, Y = y + fill.Y });
            page.Strokes.AddRange(strokes.Select(s => s.Offset(page.TextLeft, y)));
            y += height;
        }
        if (properties.Columns > 1 && settled.NoteBands.TryGetValue(page.Number, out var band))
            notesHeight = Math.Max(notesHeight, band);
        // The next parts of floating tables the text went on beside stand at the top of the new page (see
        // PlaceFloatingTable).
        for (var waiting = blank ? 0 : floatingRests.Count; waiting > 0; waiting--)
            floatingRests.Dequeue()();
    }

    /// <summary>
    /// A header or footer laid out on a page of a section (catalog: headerFooter.parts): across the text area, a
    /// header from w:header below the page's top edge, a footer ending w:footer above its bottom edge; where its
    /// content begins and ends.
    /// </summary>
    /// <summary>
    /// The text area across a page of a section: mirrored margins swap the left and right margins of even pages; the
    /// gutter is on the inside, at the top (w:gutterAtTop) or on the right (w:rtlGutter).
    /// </summary>
    (double Left, double Width) TextArea(SectionProperties properties, int number)
    {
        var even = number % 2 == 0 && context.Settings.Child("mirrorMargins").OnOff() == true;
        var gutterAtTop = context.Settings.Child("gutterAtTop").OnOff() == true;
        var rightGutter = properties.Element.Child("rtlGutter").OnOff() == true;
        var gutterLeft = !gutterAtTop && (even ? rightGutter : !rightGutter);
        var left = (even ? properties.Right : properties.Left) + (gutterLeft ? properties.Gutter : 0);
        var width = properties.PageWidth - properties.Left - properties.Right - (gutterAtTop ? 0 : properties.Gutter);
        return (left, Math.Max(1, width));
    }

    /// <summary>
    /// A page of vertical text whose lines stack from the left (tbRlV, tbLrV): laid out from the right like the others,
    /// its lines, fills and strokes are mirrored across the turned page, each line keeping its own way up.
    /// </summary>
    static void Mirror(PageBox p)
    {
        var across = p.Height;
        for (var i = 0; i < p.Lines.Count; i++)
            p.Lines[i] = p.Lines[i] with { Top = across - p.Lines[i].Top - p.Lines[i].Line.Height, Line = p.Lines[i].Line.Flipped() };
        foreach (var fills in new[] { p.Shading, p.Borders })
        {
            for (var i = 0; i < fills.Count; i++)
                fills[i] = fills[i] with { Y = across - fills[i].Y - fills[i].Height };
        }
        for (var i = 0; i < p.Strokes.Count; i++)
            p.Strokes[i] = p.Strokes[i] with { Y1 = across - p.Strokes[i].Y1, Y2 = across - p.Strokes[i].Y2 };
        for (var i = 0; i < p.ColumnLines.Count; i++)
            p.ColumnLines[i] = (p.ColumnLines[i].X, across - p.ColumnLines[i].Bottom, across - p.ColumnLines[i].Top);
    }

    /// <summary>The line pitch of a section's line grid (w:docGrid), or null where it has none.</summary>
    static double? GridPitch(SectionProperties properties) =>
        properties.Element.Child("docGrid") is { } grid && grid.Attr("type") is "lines" or "linesAndChars" or "snapToChars" && grid.Twips("linePitch") is { } pitch && pitch > 0
            ? pitch : null;

    StoryBox Story(string part, PageBox on, PageFields? fields, bool header)
    {
        // Headers and footers do not snap to the body's grid, and their text runs across the page.
        var body = settings;
        settings = body with { LinePitch = null, CharacterExtra = 0, Vertical = false, TurnEastAsian = false, GridCell = null, SnapCell = null };
        try
        {
            return StoryOf(part, on, fields, header);
        }
        finally
        {
            settings = settings with
            {
                LinePitch = body.LinePitch, CharacterExtra = body.CharacterExtra, Vertical = body.Vertical, TurnEastAsian = body.TurnEastAsian,
                GridCell = body.GridCell, SnapCell = body.SnapCell,
            };
        }
    }

    /// <summary>The section whose header and footer references name each story first.</summary>
    readonly Dictionary<string, SectionProperties> storyOwners = new(StringComparer.Ordinal);

    /// <param name="marginInStory">Where the bottom margin begins in the footer (0: at its top), for its own drawings placed
    /// from it that push its lines; null: they push none.</param>
    StoryBox StoryOf(string part, PageBox on, PageFields? fields, bool header, double? marginInStory = null)
    {
        var s = on.Section;
        // A footer's own drawings placed from the bottom margin push its lines where the footer reaches above the
        // margin, whose top is then the footer's own (see PlaceAnchor): laid out so first, kept where the footer
        // then does reach above it (measured with docx/local/fbq-6-square: a rule 20 pt below the top of a footer of six
        // lines over a margin of 56.7 pt, its text beside it; LibreOffice's extras-layout/tdf164905: the footer's text
        // below a rule wrapped square across its width 20.25 pt down, the footer 64.8 pt high, the body ending above it).
        if (!header && marginInStory == null
            && reader!.Story(part).OfType<ParagraphBlock>().Any(p => p.Content.OfType<AnchorInline>().Any(MarginPushes))
            && StoryOf(part, on, fields, header, 0) is { } reaching && reaching.Top < s.PageHeight - s.Bottom - 0.01)
            return reaching;
        var box = new StoryBox();
        var left = on.TextLeft;
        var width = on.TextWidth;
        // Tables of a header or footer a section has from one before it are as wide as in that one's text, from the
        // margin of the page's (measured with docx/local/ifw-*: a table of 100 % in a footer of a section 468 pt wide,
        // floating or not, 468 pt wide from the left margin of the next section, 252 pt wide, where its own footer of
        // the same content has it 252 pt wide; paragraphs, tabs and alignment follow the page's margins;
        // docx/external/libreoffice/extras-uiwriter/tdf157131, found by agent BG).
        var tableWidth = storyOwners.GetValueOrDefault(part) is { } owner && !ReferenceEquals(owner, s) ? Math.Max(1, TextArea(owner, on.DisplayNumber).Width) : width;
        var anchors = new List<CellAnchor>();
        // The drawings of floating tables standing on the page, already placed from its top.
        var fixedAnchors = new List<CellAnchor>();
        var y = 0.0;
        double after = 0;
        var first = true;
        var blocks = reader!.Story(part);
        // Paragraph borders and shading as in the body: paragraphs with the same frame share one box.
        var boxTop = 0.0;
        Frame? previousFrame = null;
        // Lines of text frames placed on the page itself (vAnchor margin or page), not moving with the story.
        var placed = new List<PlacedLine>();
        // Floating tables placed on the page itself, and those that move with the story, with the room they keep free.
        var fixedBox = new StoryBox();
        var floats = new List<(Exclusion Area, bool Moves, int Anchor)>();
        for (var b = 0; b < blocks.Count; b++)
        {
            var block = blocks[b];
            // A text frame (w:framePr) takes no room in the story, as in the body (see PlaceFrame): it stands aligned in
            // or at w:x from the column, the margin or the page, w:y below the top of the paragraph after it, the
            // margin or the page (measured with docx/external/libreoffice/extras-ooxmlexport/tdf168017: a footer's
            // page number framed centered 1 twip below the next paragraph's top stands on its line).
            if (block is ParagraphBlock { Format.Frame: { } properties } framed && properties.Attr("dropCap") is null or "none")
            {
                var group = new List<ParagraphBlock> { framed };
                var end = b + 1;
                while (end < blocks.Count && blocks[end] is ParagraphBlock { Format.Frame: { } other } p && XNode.DeepEquals(other, properties))
                {
                    group.Add(p);
                    end++;
                }
                double Twips(string name) => properties.Twips(name) ?? 0;
                // As wide as its paragraph, an empty one as its mark, or as its column with several (see PlaceFrame).
                var frameWidth = properties.Twips("w") is { } w && w > 0 ? w : group.Count > 1 ? width
                    : layout.Widths(framed, settings, fields, pictures: true).Max is var max && max > 0 ? Math.Min(max, width)
                    : layout.Styles.Mark(framed.MarkFormat) is var mark ? mark.Font.Data.Width("¶", false) * mark.Size : 0;
                // Turned text, the paragraphs' borders and shading and the first paragraph's space before as in the body
                // (see PlaceFrame; docx/external/libreoffice/extras-ooxmlexport/fdo76979: a header's frame of btLr text
                // 42.55 by 85 pt, bordered and shaded black, beside the body).
                var turn = framed.Format.TextDirection is "tbRl" or "btLr" && properties.Twips("h") is { } along && along > 0 ? framed.Format.TextDirection : null;
                var laid = Turned(turn != null, () => group.Select(p => layout.Layout(p, turn != null ? properties.Twips("h")!.Value : frameWidth, settings, fields)).ToList());
                var content = 0.0;
                for (var k = 0; k < group.Count; k++)
                    content += (k > 0 ? Between(group[k - 1], group[k]) : 0) + laid[k].Sum(l => l.Height);
                var frameBorders = Frame.Read(framed.Format);
                var borderTop = frameBorders?.Top is { Visible: true } bt ? bt.Space + bt.Total : 0;
                var spaceTop = framed.Format.Before ?? 0;
                content += spaceTop + borderTop + (frameBorders?.Bottom is { Visible: true } bb ? bb.Space + bb.Total : 0);
                var frameHeight = properties.Twips("h") is { } h && h > 0 ? properties.Attr("hRule") == "exact" ? h : Math.Max(h, content) : content;
                var next = end < blocks.Count ? blocks[end] as ParagraphBlock : null;
                var textTop = y + (first ? next?.Format.Before ?? 0 : context.Between(after, next?.Format.Before ?? 0));
                var (spanLeft, span) = properties.Attr("hAnchor") switch
                {
                    "page" => (0.0, on.Width),
                    "margin" => (on.TextLeft, on.TextWidth),
                    _ => (left, width),
                };
                var x = (properties.Attr("x") != null ? null : properties.Attr("xAlign")) switch
                {
                    "left" or "inside" => spanLeft,
                    "right" or "outside" => spanLeft + span - frameWidth,
                    "center" => spanLeft + (span - frameWidth) / 2,
                    _ => spanLeft + Twips("x"),
                };
                var onPage = VerticalAnchor(properties) is "page" or "margin";
                var (areaTop, areaHeight) = VerticalAnchor(properties) switch
                {
                    "page" => (0.0, s.PageHeight),
                    "margin" => (s.Top, s.PageHeight - s.Top - s.Bottom),
                    _ => (textTop, 0.0),
                };
                var frameTop = ((onPage || VerticalAnchor(properties) != "text") && properties.Attr("y") == null ? properties.Attr("yAlign") : null) switch
                {
                    "top" or "inside" => areaTop,
                    "bottom" or "outside" => areaTop + areaHeight - frameHeight,
                    "center" => areaTop + (areaHeight - frameHeight) / 2,
                    _ => areaTop + Twips("y"),
                };
                var lineTop = frameTop + spaceTop + borderTop;
                var parts = new List<(double Start, double Glyph)>();
                for (var k = 0; k < group.Count; k++)
                {
                    if (k > 0)
                        lineTop += Between(group[k - 1], group[k]);
                    parts.Add((lineTop - frameTop, laid[k].Count > 0 ? GlyphStart(laid[k][0], group[k].Format, frameHeight) : 0));
                    foreach (var line in laid[k])
                    {
                        (onPage ? placed : box.Lines).Add(turn switch
                        {
                            "tbRl" => new PlacedLine(line, x + frameWidth - (lineTop - frameTop), frameTop) { Angle = 90, TurnedFrame = (x, frameWidth, 0, frameHeight) },
                            "btLr" => new PlacedLine(line, x + (lineTop - frameTop), frameTop + frameHeight)
                            {
                                Angle = -90, TurnedFrame = (x, frameWidth, -frameHeight, frameHeight),
                            },
                            _ => new PlacedLine(line, x, lineTop),
                        });
                        lineTop += line.Height;
                    }
                }
                var frameBox = onPage ? fixedBox : box;
                var given = properties.Twips("h") is { } fh && fh > 0 ? fh : 0;
                if (turn != null && frameBorders != null)
                    DrawTurned(frameBorders, frameBox.Shading, frameBox.Borders, turn, x, frameWidth, frameTop, frameHeight, spaceTop, frameWidth, parts);
                else
                    frameBorders?.Draw(frameBox.Shading, frameBox.Borders, x, frameWidth, frameTop + spaceTop, frameTop + Math.Max(given, frameHeight),
                        top: true, bottom: true, between: null);
                // The body's text goes around a frame standing on the page, as around a floating table there (measured
                // with docx/local/fh-1: a header's frame on the top margin in mode 15 puts the body's first line beside it),
                // and around one standing with the story's text, as far as it reaches below the story, which ends where
                // its text does, the story's own text going over it (measured with docx/local/hfr-*, hfs-*: the body's
                // first four lines at the top margin beside a header's frame 72 pt high from 51.3 pt down, right of it in
                // modes 12 and 14, on both sides in mode 15, as beside the body's own frames, one 20 pt high above the
                // margin leaving them; openxml-sdk/complex0: a table below such a frame, the header's picture and
                // rule over it).
                if (properties.Attr("wrap") != "none")
                    floats.Add((new Exclusion(x - Twips("hSpace"), frameTop - Twips("vSpace"), x + frameWidth + Twips("hSpace"),
                        frameTop + frameHeight + Twips("vSpace"), properties.Attr("wrap") == "notBeside", "bothSides")
                    {
                        Story = true, NarrowestRoom = FrameRoom,
                    }, !onPage, -1));
                b = end - 1;
                continue;
            }
            // A floating table stands as in the body (see PlaceFloatingTable), taking no room in the story; the body text
            // goes around it (measured with docx/external/libreoffice/extras-ooxmlexport/tdf162746: a header's table of
            // the text's width 62.4 pt below the page's top edge pushes the body below it).
            if (block is TableBlock { Table.Format.Floating: { } position } floating)
            {
                double Twips(string name) => position.Twips(name) ?? 0;
                var laid = tables.Layout(floating.Table, tableWidth, settings, fields);
                var height = laid.Rows.Sum(r => r.Height) + (laid.Rows.Count > 0 ? laid.Rows[^1].BottomBorderWidth : 0);
                var (spanLeft, span) = position.Attr("horzAnchor") switch
                {
                    "page" => (0.0, on.Width),
                    "margin" => (on.TextLeft, on.TextWidth),
                    _ => (left, width),
                };
                var x = position.Attr("tblpXSpec") switch
                {
                    "left" or "inside" => spanLeft,
                    "right" or "outside" => spanLeft + span - laid.Width,
                    "center" => spanLeft + (span - laid.Width) / 2,
                    _ => spanLeft + Twips("tblpX") + (context.Profile.FloatingTablesByText ? (floating.Table.Format.Indent ?? 0) - laid.TextInsets.Left : 0),
                };
                var onPage = VerticalAnchor(position, "vertAnchor") is "page" or "margin";
                var (areaTop, areaHeight) = VerticalAnchor(position, "vertAnchor") switch
                {
                    "page" => (0.0, s.PageHeight),
                    "margin" => (s.Top, s.PageHeight - s.Top - s.Bottom),
                    _ => (y + (first ? 0 : after), 0.0),
                };
                // Placed on the page above its top it stands at it, as in the body (PlaceFloatingTable); one placed from
                // the story's text stands where it is, above its paragraph too (docx/external/libreoffice/
                // extras-ooxmlexport/tdf149089: a header's table 1.35 pt above the header's first paragraph).
                var tableTop = position.Attr("tblpYSpec") switch
                {
                    "top" or "inside" => areaTop,
                    "bottom" or "outside" => areaTop + areaHeight - height,
                    "center" => areaTop + (areaHeight - height) / 2,
                    _ => areaTop + Twips("tblpY"),
                };
                if (onPage)
                    tableTop = Math.Max(0, tableTop);
                var target = onPage ? fixedBox : box;
                var rowTop = tableTop;
                foreach (var row in laid.Rows)
                {
                    TableLayout.Draw(row, row.Height, x - laid.Left, rowTop, target.Lines, target.Shading, target.Borders, target.Strokes, onPage ? fixedAnchors : anchors);
                    rowTop += row.Height;
                }
                if (laid.Rows.Count > 0)
                {
                    var last = laid.Rows[^1];
                    target.Borders.AddRange(last.BottomBorders.Select(f => f with { X = x - laid.Left + f.X, Y = rowTop + f.Y }));
                    target.Strokes.AddRange(last.BottomStrokes.Select(st => st.Offset(x - laid.Left, rowTop)));
                }
                TableLayout.Frame(laid, x - laid.Left, tableTop, tableTop + height, target.Borders);
                floats.Add((new Exclusion(x - Twips("leftFromText"), tableTop - Twips("topFromText"), x + laid.Width + Twips("rightFromText"),
                    tableTop + height + Twips("bottomFromText"), false, "bothSides") { Story = true }, !onPage, b + 1));
                continue;
            }
            if (block is ParagraphBlock paragraph)
            {
                // The paragraph begins below the space after the one before; its own space before lies inside it.
                var paragraphTop = y + (first ? 0 : after);
                // HTML auto spacing counts here as in the body: 14 pt, none before the first paragraph (docx/external/
                // libreoffice/extras-ooxmlexport/fdo80997: a header of auto spacing 14 pt above the body, a footer's
                // text 14 pt above the page's bottom).
                var storyBefore = paragraph.Format.BeforeAuto == true && !context.Profile.AddsSpacing ? first ? 0 : 14 : paragraph.Format.Before ?? 0;
                y += first ? storyBefore : context.Between(after, storyBefore);
                var frame = Frame.Read(paragraph.Format);
                var continues = frame != null && frame.Same(previousFrame);
                var nextContinues = frame != null && b + 1 < blocks.Count && blocks[b + 1] is ParagraphBlock following && frame.Same(Frame.Read(following.Format));
                double? betweenAt = null;
                if (frame != null && !continues)
                {
                    y += frame.TopShadow;
                    boxTop = y;
                    y += frame.Top is { Visible: true } t ? t.Space + t.Total : 0;
                }
                else if (frame?.Between is { Visible: true } between)
                {
                    betweenAt = y + between.Space;
                    y += 2 * between.Space + between.Total;
                }
                var lineTops = new Dictionary<AnchorInline, (double Top, double X)>(ReferenceEqualityComparer.Instance);
                // The paragraph a floating table is anchored to goes below it where the table begins above it, the
                // story's other text over it (measured with docx/external/libreoffice/extras-ooxmlexport/tdf149089: a
                // header's bordered paragraph below a table 1.35 pt above it; poi/PageSpecificHeadFoot: a footer's
                // text over a table 1 twip below its top; core-header_footer/tdf79639).
                var linesTop = y;
                var around = floats.Where(f => f.Moves && f.Anchor == b && f.Area.Bottom > linesTop && f.Area.Top < paragraphTop - 0.01).Select(f => f.Area).ToList();
                // A header's text goes around its floating tables placed from the page or the margin, which stand on
                // the page as its own text does, from the header's top (measured with docx/external/libreoffice/
                // writerfilter-dmapper/floattable-header-overlap: the ninth empty paragraph below a table 343 pt down,
                // the body below the header then).
                if (header)
                    around.AddRange(floats.Where(f => !f.Moves && f.Area.Bottom - s.Header > linesTop)
                        .Select(f => f.Area with { Top = f.Area.Top - s.Header, Bottom = f.Area.Bottom - s.Header }));
                // The paragraph's own floating drawings placed from it push its lines as in the body (docx/external/
                // libreoffice/extras-layout/footer-nowrap-object: a footer's line below its own picture top and bottom,
                // which reaches 7.5 pt into the paragraph, the footer as much higher; Cool15691_headerTextWrapsAroundImage:
                // a header's text beside its picture wrapped square).
                foreach (var own in paragraph.Content.OfType<AnchorInline>())
                {
                    if ((string?)own.Anchor.Element(Docx.Drawings.Wp + "positionV")?.Attribute("relativeFrom") == "paragraph"
                        && ExclusionOf(PlaceAnchor(own, on, paragraphTop, linesTop, left, width)) is { } area && area.Bottom > linesTop + 0.01)
                        around.Add(area);
                    else if (marginInStory is { } storyMargin && MarginPushes(own)
                        && ExclusionOf(PlaceAnchor(own, on, paragraphTop, linesTop, left, width, marginTop: storyMargin)) is { } margined && margined.Bottom > linesTop + 0.01)
                        around.Add(margined);
                }
                var laid = around.Count > 0
                    ? layout.Layout(paragraph, width, settings, fields, room: slot => Room(around, linesTop, slot, left, width))
                    : layout.Layout(paragraph, width, settings, fields);
                foreach (var line in laid)
                {
                    y += line.Skip;
                    foreach (var anchor in line.Anchors ?? [])
                        lineTops[anchor] = (y, left + (line.AnchorX?.GetValueOrDefault(anchor) ?? 0));
                    box.Lines.Add(new PlacedLine(line, left, y));
                    y += line.Height;
                }
                if (frame != null)
                {
                    if (!nextContinues)
                        y += frame.Bottom is { Visible: true } bottomLine ? bottomLine.Space + bottomLine.Total : 0;
                    frame.Draw(box.Shading, box.Borders, left, width, boxTop, y, top: !continues, bottom: !nextContinues, between: betweenAt);
                    if (!nextContinues)
                        y += frame.BottomShadow;
                }
                previousFrame = frame;
                foreach (var anchor in paragraph.Content.OfType<AnchorInline>())
                {
                    var (lineTop, x) = lineTops.GetValueOrDefault(anchor, (paragraphTop, left));
                    anchors.Add(new CellAnchor(anchor, paragraphTop, lineTop) { CharacterX = x });
                }
                after = OwnAfter(paragraph);
            }
            else if (block is TableBlock table)
            {
                y += first ? 0 : after;
                var laid = tables.Layout(table.Table, tableWidth, settings, fields);
                var tableTop = y;
                foreach (var row in laid.Rows)
                {
                    TableLayout.Draw(row, row.Height, left, y, box.Lines, box.Shading, box.Borders, box.Strokes, anchors);
                    y += row.Height;
                }
                if (laid.Rows.Count > 0)
                {
                    var last = laid.Rows[^1];
                    box.Borders.AddRange(last.BottomBorders.Select(f => f with { X = left + f.X, Y = y + f.Y }));
                    box.Strokes.AddRange(last.BottomStrokes.Select(st => st.Offset(left, y)));
                    y += last.BottomBorderWidth;
                }
                TableLayout.Frame(laid, left, tableTop, y, box.Borders);
                after = 0;
                previousFrame = null;
            }
            first = false;
        }
        y += after;
        var top = header ? s.Header : s.PageHeight - s.Footer - y;
        box.Move(top);
        box.Lines.AddRange(placed);
        box.Lines.AddRange(fixedBox.Lines);
        box.Shading.AddRange(fixedBox.Shading);
        box.Borders.AddRange(fixedBox.Borders);
        box.Strokes.AddRange(fixedBox.Strokes);
        box.Floats.AddRange(floats.Select(f => f.Moves ? f.Area with { Top = f.Area.Top + top, Bottom = f.Area.Bottom + top } : f.Area));
        box.Top = top;
        box.Bottom = top + y;
        // Floating tables anchored to the story's text reach as far as the story does (measured with docx/external/
        // libreoffice/extras-ooxmlexport/tdf149089: the body begins below a header's floating table 1.35 pt above its
        // paragraph), its frames not (see above).
        foreach (var (area, _, _) in floats.Where(f => f.Moves && f.Anchor >= 0))
        {
            box.Top = Math.Min(box.Top, area.Top + top);
            box.Bottom = Math.Max(box.Bottom, area.Bottom + top);
        }
        // Floating drawings of headers and footers (watermarks among them) stand on the page like the body's.
        // Those of floating tables on the page stand where their tables do, not again below the story's top
        // (docx/external/libreoffice/extras-ooxmlexport/tdf105688: a picture in a header table placed from the page).
        // A footer that reaches above the bottom margin moves the margin's top up to its own for its drawings (measured
        // with docx/local/fbq-6-*: a rule 20 pt below the bottom margin's top stands 20 pt below the top of a footer of six
        // lines over a margin of 56.7 pt, of one line 20 pt below the margin's; LibreOffice's extras-layout/tdf164905).
        double? marginTop = header ? null : Math.Min(s.PageHeight - s.Bottom, box.Top);
        foreach (var anchor in anchors.Select(a => a.Offset(0, top)).Concat(fixedAnchors))
            box.Drawings.Add(PlaceAnchor(anchor.Anchor, on, anchor.ParagraphTop, anchor.LineTop, left, width, anchor, anchor.CharacterX, box.Drawings, marginTop));
        return box;
    }

    /// <summary>Whether a drawing is placed from the bottom margin and text goes around it.</summary>
    static bool MarginPushes(AnchorInline anchor) =>
        (string?)anchor.Anchor.Element(Docx.Drawings.Wp + "positionV")?.Attribute("relativeFrom") == "bottomMargin"
        && new DrawingBox(anchor.Anchor).Wraps;

    /// <summary>A header or footer laid out: its lines, table fills and strokes, and where its content begins and ends.</summary>
    /// <summary>
    /// The drawings of a header or footer that text goes around, which keep the body's text off them as the body's own
    /// do (by agent AE in Word's PDFs: LibreOffice's core-layout/table-fly-overlap, a header picture wrapped square
    /// from 34.25 to 164.75 pt with the body's table below it; core-objectpositioning/floattable-footer-object).
    /// </summary>
    IEnumerable<Exclusion> StoryObstacles(StoryBox story) =>
        story.Drawings.Select(ExclusionOf).OfType<Exclusion>().Select(e => e with { Story = true });

    sealed class StoryBox
    {
        public List<PlacedLine> Lines { get; } = [];
        public List<FillRect> Shading { get; } = [];
        public List<FillRect> Borders { get; } = [];
        public List<Stroke> Strokes { get; } = [];
        public List<PlacedDrawing> Drawings { get; } = [];

        /// <summary>What the story's floating tables keep free of the body's text, on the page.</summary>
        public List<Exclusion> Floats { get; } = [];
        public double Top, Bottom;

        public void Move(double dy)
        {
            for (var i = 0; i < Lines.Count; i++)
                Lines[i] = Lines[i] with { Top = Lines[i].Top + dy };
            for (var i = 0; i < Shading.Count; i++)
                Shading[i] = Shading[i] with { Y = Shading[i].Y + dy };
            for (var i = 0; i < Borders.Count; i++)
                Borders[i] = Borders[i] with { Y = Borders[i].Y + dy };
            for (var i = 0; i < Strokes.Count; i++)
                Strokes[i] = Strokes[i].Offset(0, dy);
        }

        public void AddTo(PageBox page)
        {
            page.Lines.AddRange(Lines.Select(l => l with { Story = true }));
            page.Shading.AddRange(Shading);
            page.Borders.AddRange(Borders);
            page.Strokes.AddRange(Strokes);
            page.Drawings.AddRange(Drawings);
        }
    }

    (double Left, double Width) Column(int index)
    {
        var s = section;
        // A continuous section that begins on the page has its own left and right margins (measured with
        // docx/local/csm-1, csm-cols: its text 144 pt from the page's left edge below a section's at 72 pt, its two
        // columns to its 36 pt right margin; docx/external/libreoffice/extras-ooxmlexport/fdo52208).
        var (left, textWidth) = ReferenceEquals(s, page!.Section) || s.Vertical ? (page.TextLeft, page.TextWidth) : TextArea(s, page.DisplayNumber);
        var (areaLeft, columnLeft, columnWidth) = (left, left, 0.0);
        if (s.ColumnWidths is { Count: > 0 } widths)
        {
            for (var k = 0; k < index && k < widths.Count; k++)
                columnLeft += widths[k].Width + widths[k].Space;
            columnWidth = widths[Math.Min(index, widths.Count - 1)].Width;
        }
        else
        {
            columnWidth = (textWidth - s.ColumnSpace * (s.Columns - 1)) / s.Columns;
            columnLeft = left + index * (columnWidth + s.ColumnSpace);
        }
        // The columns of a right-to-left section (w:bidi) run from the right (LibreOffice's extras-ooxmlexport/
        // tdf135343_columnSectionBreak_c12v3: "RTL 1" centered in the right-hand first column); a section on the next
        // column keeps the direction of the section whose columns it goes on in (_c14: its w:bidi changes nothing).
        return columnsRightToLeft && s.Columns > 1 ? (areaLeft + textWidth - (columnLeft - areaLeft) - columnWidth, columnWidth) : (columnLeft, columnWidth);
    }

    /// <summary>Whether the current columns run from the right: the w:bidi of the section that set them up.</summary>
    bool columnsRightToLeft;

    /// <summary>Where the body text ends: above the footnotes the page holds, or where a note continued past it filled it.</summary>
    double Bottom => Math.Min(page!.BodyBottom - notesHeight, bodyEnd ?? double.MaxValue);


    Dictionary<string, List<Block>>? footnotes;

    /// <summary>The footnotes referenced on the current page and the height they take (with the separator).</summary>
    readonly List<string> pageNotes = [];
    double notesHeight;

    /// <summary>
    /// A note split at the bottom of the current page: its id and the part shown here; what continues of it (and of
    /// the notes after it on its line) on the next page; the part of such a continuation the current page shows; and
    /// where the body text had to end once the note filled the page.
    /// </summary>
    (string Id, NoteBox Shown)? splitNote;
    NoteBox? carried, carriedHere;
    double? bodyEnd;

    /// <summary>
    /// Lines no page break may come between (a unit: the lines of a paragraph that keeps them together, the first two
    /// and the last two of one under widow control, the last line of a paragraph kept with the next and that one's
    /// first) make room only for the notes the page held when the unit began, set here while it is placed: the notes
    /// referred to in it take no room from it (measured with docx/local/fnkeep-*: a keep chain fills the page as
    /// without them, a widow-controlled pair of lines stays above its note, which shows what fits below the text and
    /// goes on to the next page).
    /// </summary>
    (PageBox Page, double Height)? keptNotes;

    /// <summary>The notes the lines of the unit in hand referred to, in order.</summary>
    readonly List<string> unitNotes = [];

    /// <summary>Whether kept lines ran into the current page's notes, which then yield (YieldNotes).</summary>
    bool notesYield;

    /// <summary>Notes kept lines left no room for, whole, which go first on the next page.</summary>
    readonly List<string> deferredNotes = [];

    /// <summary>A footnote (or a separator) laid out in a width: its lines and fills from its top, and its height.</summary>
    sealed record NoteBox(List<PlacedLine> Lines, List<FillRect> Fills, List<Stroke> Strokes, double Height)
    {
        /// <summary>
        /// The note cut where <paramref name="room"/> points end, between lines: the lines that end within them, and the
        /// rest moved up to begin at the top; no part shown where not even the first line fits, no rest where all fit.
        /// </summary>
        public (NoteBox? Shown, NoteBox? Remainder) Cut(double room)
        {
            if (Height <= room + 0.01)
                return (this, null);
            var cut = Lines.Select(l => l.Top).Where(t => t > 0.01 && Lines.Where(l => l.Top < t - 0.01).All(l => l.Top + l.Line.Height <= room + 0.01))
                .DefaultIfEmpty(0).Max();
            if (cut <= 0.01)
                return (null, this);
            var shown = Lines.Where(l => l.Top < cut - 0.01).ToList();
            return (new NoteBox(shown, Fills.Where(f => f.Y < cut).ToList(), Strokes.Where(s => s.Y1 < cut).ToList(), shown.Max(l => l.Top + l.Line.Height)),
                new NoteBox(Lines.Where(l => l.Top >= cut - 0.01).Select(l => l with { Top = l.Top - cut }).ToList(),
                    Fills.Where(f => f.Y >= cut).Select(f => f with { Y = f.Y - cut }).ToList(),
                    Strokes.Where(s => s.Y1 >= cut).Select(s => s.Offset(0, -cut)).ToList(), Height - cut));
        }
    }

    readonly Dictionary<(string, double), NoteBox> noteBoxes = [];

    /// <summary>
    /// The columns footnotes stand in (measured with docx/local/fnpages-h): a page of several columns has its notes in
    /// columns too, under one separator at the left.
    /// </summary>
    List<(double Left, double Width)> NoteColumns()
    {
        var s = page!.Section;
        if (s.Columns <= 1)
            return [(page.TextLeft, page.TextWidth)];
        var columns = new List<(double Left, double Width)>();
        var left = page.TextLeft;
        for (var k = 0; k < s.Columns; k++)
        {
            var (width, space) = s.ColumnWidths is { Count: > 0 } widths ? widths[Math.Min(k, widths.Count - 1)]
                : ((page.TextWidth - s.ColumnSpace * (s.Columns - 1)) / s.Columns, s.ColumnSpace);
            columns.Add((left, width));
            left += width + space;
        }
        return columns;
    }

    /// <summary>
    /// The columns the notes of a page fall into, whole, in order: each column takes notes until those so far reach its
    /// share of their height (seven notes of two lines in two columns: four and three).
    /// </summary>
    List<List<string>> NoteSpread(IEnumerable<string> ids, int columns)
    {
        var list = ids.ToList();
        var total = list.Sum(id => Note(id).Height);
        var spread = Enumerable.Range(0, columns).Select(_ => new List<string>()).ToList();
        var (c, sum) = (0, 0.0);
        foreach (var id in list)
        {
            spread[c].Add(id);
            sum += Note(id).Height;
            if (c < columns - 1 && sum >= total * (c + 1) / columns - 0.01)
                c++;
        }
        return spread;
    }

    /// <summary>The height the notes of a page of columns take: their tallest column and the separator.</summary>
    double Band(IEnumerable<string> ids)
    {
        var columns = NoteColumns().Count;
        var spread = NoteSpread(ids, columns);
        return spread.Any(c => c.Count > 0) ? SeparatorHeight + spread.Max(c => c.Sum(id => Note(id).Height)) : 0;
    }

    /// <summary>A footnote laid out in the current page's text width (or its first column's).</summary>
    NoteBox Note(string id)
    {
        var width = NoteColumns()[0].Width;
        if (noteBoxes.TryGetValue((id, width), out var box))
            return box;
        var blocks = footnotes != null && footnotes.TryGetValue(id, out var found) ? found : null;
        // Separators keep off the section's line grid (LibreOffice's extras-ooxmlexport/tdf143399: lines of 15.6 pt,
        // the separator's paragraph 12.07 pt high in Word).
        var pitch = settings.LinePitch;
        if (id is "separator" or "continuationSeparator")
            settings = settings with { LinePitch = null };
        var (lines, fills, strokes, height) = blocks != null ? tables.Blocks(blocks, width, settings, null) : ([], [], [], 0);
        settings = settings with { LinePitch = pitch };
        // A note's last paragraph keeps its HTML auto space after below it, the separator's too (measured with
        // docx/local/fau-both, fau-after: notes 14 pt apart, the last 14 pt above the bottom margin, the first 14 pt
        // below the separator's paragraph; fau-note: the separator's own paragraph without it; docx/external/
        // libreoffice/extras-ooxmlexport/tdf104354_firstParaInSection).
        if (blocks?.LastOrDefault() is ParagraphBlock { Format.AfterAuto: true } && !context.Profile.AddsSpacing && lines.Count > 0)
            height += 14;
        return noteBoxes[(id, width)] = new NoteBox(lines, fills, strokes, height);
    }

    /// <summary>
    /// The separator above the first footnote of a page; before Word 2013 also the room of the continuation notice
    /// below the last one where the settings list it, whether a note goes on or not (measured with docx/local/fnd-l,
    /// fnd-n: an empty notice of 12 pt Courier New listed in w:footnotePr puts the separator 13.68 pt higher in modes 14
    /// and 12, fnd-g not in mode 15, fnd-m not where only the separators are listed; docx/external/libreoffice/
    /// extras-ooxmlexport/tdf141660).
    /// </summary>
    double SeparatorHeight => footnotes == null ? 0
        : (footnotes.ContainsKey("separator") ? Note("separator").Height : 0)
          + (context.Profile.CountsContinuationNotice && footnotes.ContainsKey("continuationNotice") ? Note("continuationNotice").Height : 0);

    /// <summary>
    /// The height footnotes need below the text (catalog: note.footnotes): their content in the text width, and the
    /// separator above the first footnote of a page.
    /// </summary>
    double NotesHeight(List<string>? ids)
    {
        if (ids == null || footnotes == null || page == null)
            return 0;
        if (page.Section.Columns > 1)
        {
            var fresh = ids.Where(id => footnotes.ContainsKey(id) && !pageNotes.Contains(id)).Distinct().ToList();
            return fresh.Count == 0 ? 0 : Math.Max(0, Band(pageNotes.Concat(fresh)) - notesHeight);
        }
        var height = ids.Where(footnotes.ContainsKey).Sum(id => Note(id).Height);
        if (height > 0 && pageNotes.Count == 0 && carriedHere == null)
            height += SeparatorHeight;
        return height;
    }

    /// <summary>Takes the notes of a line or row onto the current page.</summary>
    void AddNotes(IEnumerable<string>? ids)
    {
        if (ids == null || footnotes == null)
            return;
        var added = ids.Where(id => footnotes.ContainsKey(id) && !pageNotes.Contains(id)).Distinct().ToList();
        notesHeight += NotesHeight(added);
        pageNotes.AddRange(added);
    }

    /// <summary>The height of the current page's notes again, after notes left it.</summary>
    void RecountNotes()
    {
        var kept = pageNotes.ToList();
        pageNotes.Clear();
        notesHeight = carriedHere != null ? SeparatorHeight + carriedHere.Height : 0;
        notesHeight += NotesHeight(kept);
        pageNotes.AddRange(kept);
        if (splitNote is { } split)
            notesHeight -= Note(split.Id).Height - split.Shown.Height;
    }

    /// <summary>
    /// A line whose notes do not fit below it with the text above (measured with docx/local/fnpages-a and -d): it
    /// stays on the page where its notes but the last fit and the last shows a line at least; that note shows as many
    /// lines as the page holds, the rest continues on the next page under the separator before that page's own
    /// notes, and the page takes no more text.
    /// </summary>
    bool SplitNotes(List<string> ids, double room)
    {
        if (footnotes == null || page!.Section.Columns > 1)
            return false;
        ids = ids.Where(id => footnotes.ContainsKey(id) && !pageNotes.Contains(id)).Distinct().ToList();
        if (ids.Count == 0)
            return false;
        room -= pageNotes.Count == 0 && carriedHere == null ? SeparatorHeight : 0;
        var whole = ids.Take(ids.Count - 1).Sum(id => Note(id).Height);
        if (whole > room + 0.01)
            return false;
        var (shown, rest) = Note(ids[^1]).Cut(room - whole);
        if (shown == null || rest == null)
            return false;
        AddNotes(ids);
        splitNote = (ids[^1], shown);
        notesHeight -= Note(ids[^1]).Height - shown.Height;
        carried = rest;
        return true;
    }

    /// <summary>The current page's footnotes at its bottom, above the bottom margin, under their separator.</summary>
    /// <returns>Where the notes end on the page.</returns>
    double PlaceNotes()
    {
        if (notesYield)
            YieldNotes();
        notesYield = false;
        if (page == null || pageNotes.Count == 0 && carriedHere == null || footnotes == null)
        {
            pageNotes.Clear();
            notesHeight = 0;
            (splitNote, carriedHere, bodyEnd) = (null, null, null);
            return y;
        }
        // A section that puts its footnotes beneath the text (its own w:footnotePr/w:pos; the settings' alone leave
        // them at the page's bottom) has them follow its last line (measured with docx/local/fnpages-f and -g) and that
        // line's paragraph's space after (fbt2-14, fbt2-15: 12 pt after put the separator 12 pt lower; fbt3-15).
        var at = page.Section.Element.Child("footnotePr").Child("pos").Val() == "beneathText"
            ? Math.Min(y + (columnEmpty ? 0 : pendingAfter), page.BodyBottom - notesHeight) : page.BodyBottom - notesHeight;
        var x = page.TextLeft;
        void Put(NoteBox box)
        {
            foreach (var line in box.Lines)
                page.Lines.Add(line with { Left = x + line.Left, Top = at + line.Top });
            foreach (var fill in box.Fills)
                page.Borders.Add(fill with { X = x + fill.X, Y = at + fill.Y });
            page.Strokes.AddRange(box.Strokes.Select(s => s.Offset(x, at)));
            at += box.Height;
        }
        if (footnotes.ContainsKey("separator"))
            Put(Note("separator"));
        if (carriedHere != null)
            Put(carriedHere);
        page.Footnotes.AddRange(pageNotes);
        var columns = NoteColumns();
        if (columns.Count > 1)
        {
            // Notes in columns, each column's from the top of the band.
            page.NoteBand = Band(pageNotes);
            var bandTop = at;
            var spread = NoteSpread(pageNotes, columns.Count);
            for (var c = 0; c < columns.Count; c++)
            {
                (x, at) = (columns[c].Left, bandTop);
                foreach (var id in spread[c])
                    Put(Note(id));
            }
        }
        else
        {
            foreach (var id in pageNotes)
                Put(splitNote is { } split && split.Id == id ? split.Shown : Note(id));
        }
        pageNotes.Clear();
        notesHeight = 0;
        (splitNote, carriedHere, bodyEnd) = (null, null, null);
        return at;
    }

    /// <summary>
    /// The part of a note continued from the page before that a new page shows (Word's continuation separator prints as
    /// its separator, measured with docx/local/fnpages-a and -d), leaving the text some room on pages it would fill.
    /// </summary>
    void ContinueNotes()
    {
        if (page == null)
            return;
        if (carried != null)
        {
            var room = page.BodyBottom - page.BodyTop - SeparatorHeight - 36;
            var (shown, rest) = carried.Cut(Math.Max(room, carried.Lines.Count > 0 ? carried.Lines[0].Line.Height : 0));
            (carriedHere, carried) = (shown ?? carried, shown == null ? null : rest);
            notesHeight = SeparatorHeight + carriedHere.Height;
        }
        // Notes kept lines left no room for follow, before the page's own.
        AddNotes(deferredNotes.ToList());
        deferredNotes.Clear();
    }

    /// <summary>
    /// Whether notes referred to in a unit of kept lines ending at <paramref name="bottom"/> show below it, under the
    /// notes the page held before it (<paramref name="before"/>): all but the last whole and the last with a line at
    /// least, as a line's notes (SplitNotes); a unit whose notes do not show goes to the next page unless it began
    /// its page (measured with docx/local/fnkeep-y4: a widow-controlled pair of lines at the bottom of the page moves
    /// on with its note; fnkeep-q2: a keep chain below other text too).
    /// </summary>
    bool NotesShow(double bottom, double before, List<string> ids)
    {
        if (ids.Count == 0 || footnotes == null || page == null)
            return true;
        var room = page.BodyBottom - bottom - before - (before <= 0.01 ? SeparatorHeight : 0);
        for (var i = 0; i + 1 < ids.Count; i++)
            room -= Note(ids[i]).Height;
        if (room < -0.01)
            return false;
        var last = Note(ids[^1]);
        return last.Height <= room + 0.01 || last.Cut(room).Shown != null;
    }

    /// <summary>
    /// The notes of a page whose kept lines left them too little room below the text (keptNotes): those that fit stay,
    /// the first that does not shows the lines that fit, if any, and continues on the next page, where the notes after
    /// it go whole (measured with docx/local/fnkeep-*: the next page shows them under the separator, before its own).
    /// </summary>
    void YieldNotes()
    {
        if (page == null || footnotes == null || splitNote != null)
            return;
        var room = page.BodyBottom - y - (carriedHere != null ? SeparatorHeight + carriedHere.Height : pageNotes.Count > 0 ? SeparatorHeight : 0);
        var stays = new List<string>();
        for (var i = 0; i < pageNotes.Count; i++)
        {
            var note = Note(pageNotes[i]);
            if (note.Height <= room + 0.01)
            {
                stays.Add(pageNotes[i]);
                room -= note.Height;
                continue;
            }
            var (shown, rest) = note.Cut(room);
            if (shown != null && rest != null)
            {
                stays.Add(pageNotes[i]);
                splitNote = (pageNotes[i], shown);
                carried = rest;
            }
            else
                deferredNotes.Add(pageNotes[i]);
            deferredNotes.AddRange(pageNotes.Skip(i + 1));
            break;
        }
        pageNotes.Clear();
        pageNotes.AddRange(stays);
        RecountNotes();
    }

    void NextColumn(bool natural)
    {
        if (column + 1 < section.Columns)
        {
            column++;
            y = columnTop;
        }
        else
            NewPage(section);
        columnEmpty = true;
        top = Top.Break;
    }

    /// <summary>
    /// Places a table row by row (catalog: table.rows): a row that does not fit goes to the next column or page,
    /// where the table's header rows are repeated first; where the table breaks, the line below the last row of the
    /// page closes it. The paragraph before keeps its space after above the table.
    /// </summary>
    /// <remarks>
    /// Measured with docx/local/tblhdr-*: header rows that leave no room for the first row below them (all of it where
    /// it may not split, else its first line) go with it to the next column or page, unless the table starts there.
    /// They repeat only where the row that goes on fits below them in the same way; where they do not, the rows stand
    /// at the top without them, and Word still counts them: what follows the table goes to the next page. A row that
    /// may not split moves on whole and splits only at the top of a page that cannot hold it.
    /// </remarks>
    /// <summary>
    /// A table does not stand beside the floating objects and frames placed before it: it begins below those its part
    /// on the page would overlap, from Word 2013 on whatever their wrapping, before only those kept free above and
    /// below and those that leave it less than half its width right of them (measured with docx/local/fx-1 to fx-11 and ft-1 to ft-5: below a
    /// 40 pt shape wrapped square or top and bottom, also one beside a 70-row table's last rows on the page, and below a
    /// frame not beside text; a shape right of the table, one without wrapping and in mode 14 one wrapped square leave
    /// it, 340 pt beside it; docx/external/libreoffice/core-ooxml-pass/fdo78904_fileHang; extras-ooxmlexport/tdf162746:
    /// in mode 12 below a header's floating table that leaves 5 pt; tfh-*-14: below a shape wrapped square at the column's
    /// right, in the header or the body, that leaves 50, 65 or 85 % of a full-width table left of it; core-layout/
    /// table-fly-overlap, found by agent BI; core-layout/table-fly-overlap-spacing: a picture whose own box ends right
    /// of a narrow table, only its distance over it, leaves it). From Word 2013 on also below the objects anchored
    /// after it on the page, where the run before placed them (docx/local/tbo-*: a shape anchored in the paragraph
    /// after a table or later, over its last or first row, wrapped square or top and bottom; one without wrapping
    /// leaves it; openxml-sdk/Document: a cover page's text box over the last row of a table above a chart, the cover
    /// then on the next page). Known deviation: in mode 14 Word moves only the row such an object overlaps (tbo-14),
    /// ours leaves the table.
    /// </summary>
    /// <returns>
    /// How far the table moves across into the room beside a floating table of the text that it fits beside and would
    /// stand across, aligned there as in its column (measured with docx/local/ib-1, ib-2: a 90 pt table beside a
    /// 350 pt one at its right distance, centered in the room beside it; docx/external/libreoffice/extras-uiwriter/
    /// tdf134227).
    /// </returns>
    double BelowFloats(TableBox box, double left, XElement element, string? justification)
    {
        var (from, to) = (left + box.Left, left + box.Left + box.Width);
        var height = box.Rows.Sum(r => r.Height);
        var (columnLeft, columnWidth) = Column(column);
        // A floating table of the text the table fits beside, or whose own box it does not reach, leaves it there
        // (measured with docx/local/ib-1 to ib-7: a table of 90 pt beside one of 350 pt, of 468 pt below it;
        // docx/external/libreoffice/writerfilter-dmapper/floattable-then-table, extras-layout/tdf167526,
        // floattable-center-inline-overlap; extras-ooxmlexport/tdf92157: a table into the distance of a floating table
        // in the right margin stays).
        bool Beside(Exclusion e) => !e.Frame && !e.Story && (Math.Max(e.Left - columnLeft, columnLeft + columnWidth - e.Right) >= box.Width - 0.01
            || e.Left + e.DistanceLeft >= to - 0.01 || e.Right - e.DistanceRight <= from + 0.01);
        var placed = page!.Drawings.Select(ExclusionOf).OfType<Exclusion>().ToList();
        var later = context.Profile.TablesBelowAllFloats && settled.Floats.TryGetValue(page.Number, out var before)
            ? before.Where(e => e.Source is { } source && !e.Frame && !placed.Any(p => ReferenceEquals(p.Source, source))
                && !source.Ancestors().Any(a => ReferenceEquals(a, element)))
            : [];
        var floats = placed.Concat(later).Concat(page.FloatingTables.Where(e => !Beside(e)))
            .Where(e => (context.Profile.TablesBelowAllFloats || e.TopAndBottom
                         || to - e.Right < box.Width / 2 && e.Left + e.DistanceLeft < to - 0.01 && e.Right - e.DistanceRight > from + 0.01)
                        && e.Left < to - 0.01 && e.Right > from + 0.01).ToList();
        for (var moved = true; moved;)
        {
            moved = false;
            foreach (var e in floats)
            {
                if (e.Top < Math.Min(y + height, Bottom) - 0.01 && e.Bottom > y + 0.01)
                {
                    y = e.Bottom;
                    moved = true;
                }
            }
        }
        foreach (var e in page.FloatingTables.Where(Beside))
        {
            if (e.Frame || e.Story || e.Top >= Math.Min(y + height, Bottom) - 0.01 || e.Bottom <= y + 0.01
                || e.Left + e.DistanceLeft >= to - 0.01 || e.Right - e.DistanceRight <= from + 0.01)
                continue;
            var (roomLeft, roomRight) = e.Left - columnLeft >= columnLeft + columnWidth - e.Right
                ? (columnLeft, e.Left) : (e.Right, columnLeft + columnWidth);
            if (roomRight - roomLeft < box.Width - 0.01)
                continue;
            var target = justification switch
            {
                "center" => roomLeft + (roomRight - roomLeft - box.Width) / 2,
                "right" or "end" => roomRight - box.Width,
                _ => roomLeft,
            };
            return target - from;
        }
        return 0;
    }

    void PlaceTable(List<Block> blocks, int index)
    {
        var table = (TableBlock)blocks[index];
        var (left, width) = Column(column);
        // A table right after another one continues it (TableLayout.Layout's joined).
        var joined = index > 0 && blocks[index - 1] is TableBlock { Table.Format.Floating: null } prior && table.Table.Format.Floating == null
                     && previousTable is { } p && ReferenceEquals(p.Block, prior) ? p.Box.BottomEdges : null;
        var box = LaidTable(table, width, stylePrevious, keep: false, joined);
        if (table.Table.Format.Floating == null)
            previousTable = (table, box);
        // A floating table that ends the document, no paragraph after it to anchor it, stands in the text (LibreOffice's
        // extras-ooxmlimport/table_width: at the body's top, not at its tblpY 513 pt).
        if (table.Table.Format.Floating is { } floating && box.Rows.Count > 0 && !ReferenceEquals(table, documentEnd))
        {
            PlaceFloatingTable(blocks, index, floating, box, left, width);
            return;
        }
        if (!columnEmpty)
            y += pendingAfter;
        // A table whose first cell's first paragraph breaks the page before it (w:pageBreakBefore, its own or its
        // style's) begins on a new page, unless the page is still empty (docx/external/libreoffice/extras-ooxmlexport/
        // tdf89377_tableWithBreakBeforeParaStyle: two tables on pages of their own).
        if (table.Table.Rows.FirstOrDefault()?.Cells.FirstOrDefault()?.Blocks.FirstOrDefault() is ParagraphBlock { Format.PageBreakBefore: true }
            && !(columnEmpty && column == 0 && page!.Lines.Count == 0))
        {
            NewPage(section);
            top = Top.Break;
            (left, width) = Column(column);
        }
        // A table whose first row is kept with the next one begins a keep chain, unless it continues one (measured
        // with docx/local/tk1, tk4, tk7, tk9: such rows go to the next page with the paragraphs kept with them).
        var chained = previous != null ? previous.Format.KeepNext == true : tableKept;
        if (!columnEmpty && !chained && RowKept(table.Table, 0) && ChainBreaks(blocks, index, null, width, y))
        {
            NextColumn(natural: true);
            (left, width) = Column(column);
        }
        // A table whose cells' paragraphs have frame properties stands in that frame (docx/local/lb-1 to lb-4: Word
        // sets such a table at the margin's outside top, beside the frame before it, without them below it).
        if (table.Table.Rows.FirstOrDefault()?.Cells.FirstOrDefault()?.Blocks.FirstOrDefault() is not ParagraphBlock { Format.Frame: not null })
            left += BelowFloats(box, left, table.Element, table.Table.Format.Justification);
        var breakAfter = BelowOwnDrawings(box, left, index + 1 < blocks.Count);
        tableKept = RowKept(table.Table, box.Rows.Count - 1);
        var headers = box.Rows.TakeWhile(r => r.Header).ToList();
        var headersHeight = headers.Sum(h => h.Height);
        // Whether the header rows were left off this column or page.
        var omitted = false;
        RowBox? last = null;
        var tableTop = y;
        // The table's alternative text, for the part of it on each page.
        var alternative = string.Join(": ", new[] { table.Table.Format.Caption, table.Table.Format.Description }.Where(t => !string.IsNullOrWhiteSpace(t)));
        void Describe()
        {
            if (alternative.Length > 0 && y > tableTop)
                page!.Alternatives.Add((alternative, left + box.Left, tableTop, box.Width, y - tableTop));
        }
        // Where the table breaks, the line below the page's last row closes it, and a frame (cell spacing) ends.
        void Break()
        {
            if (last != null)
                Below(last, left);
            Describe();
            // A table that moves whole leaves no frame behind (docx/external/poi/bib-chernigovka.netdo.ru_download_docs_17459: a
            // zero-height frame line at page 1's bottom, the cell-spacing table on page 2).
            if (y > tableTop + 0.01)
                TableLayout.Frame(box, left, tableTop, y, page!.Borders);
            NextColumn(natural: true);
            (left, width) = Column(column);
            // The part that goes on begins below the floating objects and frames there that it would overlap, as the
            // table does where it begins (openxml-sdk/complex0: rows going on below a header's frame on page 4).
            BelowFloats(box, left, table.Element, table.Table.Format.Justification);
            tableTop = y;
            // A part that goes on below rows of the page before begins below its frame's top line and the spacing; a
            // table that moves whole has them in its first row.
            if (last != null)
                y += box.ContinuationTop;
        }
        // Footnotes of a row's cells need room below it as a paragraph's do.
        List<string> NotesOf(RowBox row) => row.Lines.SelectMany(l => l.Line.Notes ?? []).ToList();
        // Whether a row, all of it or its split, fits below a position in this column (as the loop below places it):
        // with the line that closes the table below it where the table breaks (measured with docx/local/tblbottom-*).
        bool Fits(RowBox row, double top) => top + row.Height + row.BottomBorderWidth <= Bottom - NotesHeight(NotesOf(row)) + 0.01;
        (RowBox Head, RowBox Tail)? Split(RowBox row, double top, bool force = false) =>
            Bottom - top > row.TopWidth + 1 ? row.Split(Bottom - top - row.BottomBorderWidth, force) : null;
        bool Takes(RowBox row, double top) => Fits(row, top) || Split(row, top) != null;
        // A table of header rows only moves where they do not all fit, whether or not they fit a page (docx/external/
        // libreoffice/extras-ooxmlexport/tdf138020_all_rows_tblHeader: eight rows after a line go to the next page and
        // break there). Before Word 2013 header rows stand where they fit as other rows do, repeat after a break below
        // all of them and not after one between them (measured with docx/local/hk-*: in modes 11, 12 and 14 a header
        // row with room for no row below it stays and repeats on the next page; of two the first stays alone and the
        // second goes on with the table; docx/external/poi/bug57031).
        if (headers.Count > 0 && !columnEmpty && context.Profile.HeaderRowsGoOn
            && (headers.Count < box.Rows.Count ? !Takes(box.Rows[headers.Count], y + headersHeight) : !Fits(headers[^1], y + headersHeight - headers[^1].Height)))
        {
            NextColumn(natural: true);
            (left, width) = Column(column);
            tableTop = y;
        }
        mergedDown.Clear();
        for (var r = 0; r < box.Rows.Count; r++)
        {
            var row = box.Rows[r];
            // Lines of cells merged down from the row before go on with this one (RowBox.Overflow).
            if (mergedDown.Count > 0)
            {
                row = row.With(mergedDown);
                mergedDown.Clear();
            }
            var broke = false;
            // A row kept with the next one that begins a keep chain inside the table goes to the next column with it
            // where the chain does not fit here (measured with docx/local/tkr-e, tkr-g; docx/external/libreoffice/
            // extras-ooxmlexport/tdf104061_tableSectionColumns: a heading row before a row of 173 pt at least).
            var moves = r > 0 && last != null && !columnEmpty && RowKept(table.Table, r) && !RowKept(table.Table, r - 1)
                        && ChainBreaks(blocks, index, null, width, y, fromRow: r);
            while (moves || !Fits(row, y))
            {
                // A row that does not fit splits at a line of each cell (unless it may not split, measured with
                // docx/local/tbl-autofit), else moves on; one that may not split splits at the top of a column it
                // does not fit either, and one that cannot split at all overflows.
                if (!moves && Split(row, y, force: columnEmpty) is { } parts)
                {
                    PlaceRow(parts.Head, left);
                    AddNotes(NotesOf(parts.Head));
                    last = parts.Head;
                    row = parts.Tail;
                }
                else if (columnEmpty)
                    break;
                Break();
                broke = true;
                omitted = !row.Header && headers.Count > 0 && !Takes(row, y + headersHeight);
                if (!row.Header && !omitted)
                {
                    foreach (var header in headers)
                    {
                        PlaceRow(header, left);
                        last = header;
                    }
                }
                if (row.Header || omitted || headers.Count == 0)
                    row = row.AtPageTop();
                moves = false;
            }
            if (broke)
                row = row.Continued();
            // A cell merged down over rows that the page or column breaks is laid out along its part before the break:
            // its turned text along that part from its end (btLr) or its top (tbRl), its text aligned to the middle or
            // the bottom of that part, the rest of it going on as before (measured with docx/local/vmt-*: a cell merged
            // over rows of 102, 150.45 and 121.5 pt at least, the third on the next page, btLr and tbRl text in two lines
            // along the first two from their bottom or top, centered text from their top as it does not fit, in modes 11
            // and 15; LibreOffice's extras-layout/tdf127118).
            foreach (var cell in row.Cells.ToList())
            {
                if (cell.Along is not { } along)
                    continue;
                var (reach, k) = (y, r);
                for (var end = Math.Min(box.Rows.Count, r + along.Rows); k < end && Fits(k == r ? row : box.Rows[k], reach); k++)
                    reach += (k == r ? row : box.Rows[k]).Height;
                if (k > r && k < Math.Min(box.Rows.Count, r + along.Rows))
                    row = row.With(cell, along.Part(reach - y - row.TopWidth));
            }
            PlaceRow(row, left);
            AddNotes(NotesOf(row));
            last = row;
        }
        // Lines merged down past the last row stand below it.
        if (mergedDown.Count > 0)
            TableLayout.Draw(new RowBox().With(mergedDown), 0, left, y, page!.Lines, page.Shading, page.Borders, page.Strokes);
        mergedDown.Clear();
        // A table that another table follows directly continues in it: Word joins them, one line between (measured
        // with docx/local/tbl-geom); a floating table after it or before it joins nothing (docx/external/libreoffice/
        // extras-layout/floattable-not-wrapped-by-table: the bottom line before a floating table drawn).
        if (last != null && !(table.Table.Format.Floating == null && index + 1 < blocks.Count && blocks[index + 1] is TableBlock { Table.Format.Floating: null }))
        {
            Below(last, left);
            y += last.BottomBorderWidth;
        }
        Describe();
        TableLayout.Frame(box, left, tableTop, y, page!.Borders);
        if (omitted)
            y += headersHeight;
        pendingAfter = 0;
        pendingOwnAfter = 0;
        page!.TrailingSpace = 0;
        previous = null;
        stylePrevious = LastParagraph(table.Table);
        paragraphNumber++;
        if (breakAfter)
            NextColumn(natural: true);
    }

    /// <summary>
    /// Before Word 2013 a table's own drawings of layoutInCell 0 that text goes around, placed from their paragraph,
    /// stand where the table's first layout puts them, and the table goes below those over it; where it then does not
    /// fit, it stays and what follows it goes on in the next column or page (measured with docx/local/nlm-12-0-*,
    /// nlm-14-0-*: a rectangle of 100 x 50 pt wrapped through in the last row's cell, aligned left in the column 0.05 pt
    /// below its paragraph, at the column's left at that row's top, the table below it; layoutInCell 1 and mode 15 not,
    /// nlm-12-1-*, nlm-15-*; cig-noin14, cig-noin12, ccl-out14: shapes placed from the column across; nlc-*: copies of
    /// LibreOffice's extras-ooxmlexport/tdf162541_notLayoutInCell_paraLeft, a picture wrapped through, square or top and
    /// bottom of 50 or 103.5 pt, the table staying at the page's top, the paragraphs after it on page 2 whatever the room
    /// left; unwrapped or without the picture they stay). True where what follows goes on so.
    /// </summary>
    bool BelowOwnDrawings(TableBox box, double left, bool followed)
    {
        if (!context.Profile.HonorsLayoutInCell || box.Rows.Count == 0)
            return false;
        var (lines, fills, borders, strokes) = (new List<PlacedLine>(), new List<FillRect>(), new List<FillRect>(), new List<Stroke>());
        var anchors = new List<CellAnchor>();
        var rowTop = y;
        foreach (var row in box.Rows)
        {
            TableLayout.Draw(row, row.Height, left, rowTop, lines, fills, borders, strokes, anchors);
            rowTop += row.Height;
        }
        var height = rowTop - y;
        if (y + height > Bottom + 0.01)
            return false;
        var over = new List<(XElement Source, PlacedDrawing Placed, double Bottom)>();
        foreach (var anchor in anchors)
        {
            var element = anchor.Anchor.Anchor;
            if ((string?)element.Attribute("layoutInCell") is not ("0" or "false")
                || (string?)element.Element(Docx.Drawings.Wp + "positionV")?.Attribute("relativeFrom") != "paragraph")
                continue;
            var placed = PlaceAnchor(anchor.Anchor, page!, anchor.ParagraphTop, anchor.LineTop, left, page!.TextWidth, anchor, anchor.CharacterX);
            if (ExclusionOf(placed) is { } area && area.Left < left + box.Left + box.Width - 0.01 && area.Right > left + box.Left + 0.01
                && area.Top < y + height - 0.01 && area.Bottom > y + 0.01)
                over.Add((element, placed, area.Bottom));
        }
        if (over.Count == 0)
            return false;
        var below = over.Max(o => o.Bottom);
        if (below + height > Bottom + 0.01)
            return followed;
        foreach (var (source, placed, _) in over)
            heldInTable[source] = (page!.Number, placed.X, placed.Y);
        y = below;
        return false;
    }

    /// <summary>
    /// Where a floating drawing stands (catalog: drawing.anchor): its offset from, or its alignment in, the frame its
    /// wp:positionH and wp:positionV name (page, margin, column, the margins on either side, the paragraph, the
    /// line), or a percentage of it (wp14:pctPosHOffset); wp:simplePos gives the page position directly.
    /// </summary>
    /// <remarks>
    /// The paragraph's top lies above its own space before, below the space after the paragraph before it (where
    /// the larger of the two separates them, what the space before has more); the line's top is that of the line
    /// holding the anchor (measured with docx/local/anchor-rules, all within 0.05 pt).
    /// </remarks>
    /// <summary>
    /// An anchored drawing placed (Anchored), a text box that resizes to its text (a:spAutoFit) or does not wrap fitted
    /// to it as it is drawn, so that text goes around the box Word shows, not its saved or relative height (powertools/
    /// DB-GlossaryDocuments/HeaderContent-built, DB010-Template: boxes saved 69.85 and 110.55 pt high, 52.9 and 33.4
    /// pt with their text, one 20 % of the page high; openxml-sdk/Complex01: the object after a box saved 110.6 pt high
    /// stands 3.6 pt below its 30.4 pt).
    /// </summary>
    /// <param name="marginTop">Where the bottom margin begins for the drawings of a footer that reaches above it (see StoryOf), else null.</param>
    PlacedDrawing PlaceAnchor(AnchorInline anchor, PageBox page, double paragraphTop, double lineTop, double columnLeft, double columnWidth,
        CellAnchor? cell = null, double? character = null, IReadOnlyList<PlacedDrawing>? before = null, double? marginTop = null) =>
        Clear(Placed(anchor, page, paragraphTop, lineTop, columnLeft, columnWidth, cell, character, marginTop), before ?? page.Drawings, page.Section.PageWidth,
            context.Profile.OverlapStepsDown ? (page.BodyBottom, page.Section.PageHeight) : null);

    /// <summary>
    /// A floating drawing that may not overlap the drawings placed before it, or that one of them may not overlap
    /// (wp:anchor allowOverlap 0), moves right of the one it overlaps where it fits across the page, else below it
    /// (measured with docx/local/aov-*: a 200 pt rectangle over half of one before it right of that one, wrapped or not,
    /// placed from the page or its paragraph, whichever of the two forbids it, in either order; aov2-wide: of 400 pt
    /// below it; aov2-marg: right of it past the margin up to the page's edge; docx/external/libreoffice/extras-layout/
    /// tdf124600, extras-ooxmlexport/tdf124600b: a header's second text box below the first, one behind the text and
    /// wrapped square). Drawings behind the text without wrapping take no part (tdf120760_ZOrderInHeader: a page's
    /// background picture behind a header's band stays).
    /// </summary>
    /// <param name="legacy">
    /// Before Word 2013 the body's bottom and the page's height, where a drawing that moves with its paragraph or line
    /// first goes its own height down: still beside the one it overlaps and then past the body's bottom it stands there
    /// right of that one, else below that one where it fits on the page, else right of it as low as the page lets it
    /// (measured with docx/local/aoy-*-14, -12, aoz-*: in mode 14 a rectangle 112.82 pt wide and 60 to 120 pt high,
    /// centred on the column 14.2 pt below its paragraph, over one 63.21 x 94.39 pt centred on the margin: of 64 and 68
    /// pt below it, of 72 to 92 pt right of it and its own height down, of 96 to 110 pt below it, of 120 pt right of it
    /// at the page's bottom; with that one 70 to 130 pt high, 30 pt down, the bottom margin 2 cm less, in a paragraph of
    /// its own or the first of the two alike; placed from the page right only, aoy-page-14; LibreOffice's
    /// extras-ooxmlexport/tdf134114_allowOverlap; known deviation: without text beside the one it overlaps Word leaves
    /// it its own height down, aoz-short).
    /// </param>
    static PlacedDrawing Clear(PlacedDrawing drawing, IReadOnlyList<PlacedDrawing> before, double pageWidth, (double BodyBottom, double PageHeight)? legacy = null)
    {
        static bool Forbids(PlacedDrawing d) => (string?)d.Drawing.Attribute("allowOverlap") is "0" or "false";
        static bool Background(PlacedDrawing d) => d.Behind && new DrawingBox(d.Drawing).Wrap?.Name.LocalName == "wrapNone";
        if (Background(drawing))
            return drawing;
        var withText = legacy is { } &&
            (string?)drawing.Drawing.Element(Docx.Drawings.Wp + "positionV")?.Attribute("relativeFrom") is "paragraph" or "line";
        var planned = drawing.Y;
        for (var round = 0; round < 8; round++)
        {
            var hit = before.FirstOrDefault(p => !ReferenceEquals(p.Drawing, drawing.Drawing) && !Background(p) && (Forbids(p) || Forbids(drawing))
                && p.X < drawing.X + drawing.Width - 0.01 && drawing.X < p.X + p.Width - 0.01 && p.Y < drawing.Y + drawing.Height - 0.01 && drawing.Y < p.Y + p.Height - 0.01);
            if (hit == null)
                break;
            var right = hit.X + hit.Width + drawing.Width <= pageWidth + 0.01;
            if (withText && legacy is var (bodyBottom, pageHeight))
            {
                var (own, below) = (planned + drawing.Height, hit.Y + hit.Height);
                drawing = own < below - 0.01 && own + drawing.Height > bodyBottom + 0.01 && right ? drawing with { X = hit.X + hit.Width, Y = own }
                    : below + drawing.Height <= pageHeight + 0.01 ? drawing with { Y = below }
                    : drawing with { X = right ? hit.X + hit.Width : drawing.X, Y = Math.Min(own, pageHeight - drawing.Height) };
                continue;
            }
            drawing = right ? drawing with { X = hit.X + hit.Width } : drawing with { Y = hit.Y + hit.Height };
        }
        return drawing;
    }

    PlacedDrawing Placed(AnchorInline anchor, PageBox page, double paragraphTop, double lineTop, double columnLeft, double columnWidth,
        CellAnchor? cell, double? character, double? marginTop)
    {
        var placed = Unpinned(anchor, page, paragraphTop, lineTop, columnLeft, columnWidth, cell, character, marginTop);
        if (heldInTable.TryGetValue(anchor.Anchor, out var held) && held.Page == page.Number)
            return placed with { X = held.X, Y = held.Y };
        return settled.Pins.TryGetValue(anchor.Anchor, out var pin) && pin.Page == page.Number ? placed with { X = pin.X, Y = pin.Y } : placed;
    }

    PlacedDrawing Unpinned(AnchorInline anchor, PageBox page, double paragraphTop, double lineTop, double columnLeft, double columnWidth,
        CellAnchor? cell, double? character, double? marginTop)
    {
        var placed = Anchored(anchor, page, paragraphTop, lineTop, columnLeft, columnWidth, cell, character, context.Profile.AnchorsAsWord2010, marginTop);
        placed = placed with { Order = context.StackingOrder(anchor.Anchor) };
        // From Word 2013 on the shapes of a drawing in a cell that text goes around are cut at the cell's inside, between
        // the inner edges of its side lines and from its content's top to its bottom, its text box's text not (measured
        // with docx/local/cdr-pager, ccl-mar, ccl-thick, ccl-col: a 387 pt box in a 200 pt cell ends at it, margins of
        // 10 pt above and 15 pt below cut it there, those across not; ccl-14 not cut, ccl-none and ccl-behind unwrapped
        // not; docx/external/powertools/WC/WC048-Text-Box-in-Cell).
        if (context.Profile.CellsCutWrappedDrawings && cell?.Inside is { } inside && cell.Content is { } content
            && new DrawingBox(anchor.Anchor).Wraps)
            placed = placed with { Clip = (inside.Left, content.Top, Math.Max(0, inside.Right - inside.Left), content.Height) };
        if (anchor.Text is not { Count: > 0 } text || placed.Body is not { } body)
            return Held(placed);
        var shape = placed.Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Docx.Drawings.Wps + "wsp");
        var fitted = TextBoxLayout.FitToText(placed, text, body, shape, page, tables, settings, context.Theme);
        // A box that grows or shrinks to its text is aligned as it then is (LibreOffice's core-doc/
        // text-box-word-wrap: a header box without wrapping saved 34.95 pt wide, 103.1 pt with its text, ends at the
        // page's right edge).
        var grown = fitted.Width - placed.Width;
        if (Math.Abs(grown) > 0.01 && anchor.Anchor.Element(Docx.Drawings.Wp + "positionH")?.Element(Docx.Drawings.Wp + "align")?.Value.Trim() is { } align)
        {
            var odd = page.DisplayNumber % 2 == 1;
            var move = align switch
            {
                "right" => grown,
                "center" => grown / 2,
                "inside" => odd ? 0 : grown,
                "outside" => odd ? grown : 0,
                _ => 0,
            };
            fitted = fitted with { X = fitted.X - move };
        }
        return Held(fitted);

        // From Word 2013 on a drawing of the text that text goes around, placed from its paragraph or line, stays
        // between the margins with its effect extents, as high as it is drawn: past the bottom margin it moves up to end
        // there, above the top margin down to begin there, which wins (measured with docx/local/wrm-15, wrm-behind: 20
        // pt above the first paragraph at the margin, wrapped square or behind the text tight; wrm-14 above it in mode
        // 14, wrm-none unwrapped above it; eff-15: 20 pt above to 1 pt below a paragraph at the margin with 1.5 pt of
        // effect extent above it 1.5 pt below the margin, with 6 pt 6 pt below, its distance from the text not counted;
        // 60 pt high 20 to 30 pt below a paragraph lower down, with 6 pt of effect extent above and 3 pt below, ending at
        // the bottom margin; efd-15, efe-15, efi-15: unwrapped or placed from the margin or the page they stay, in mode
        // 14 too, efd-14; docx/external/libreoffice/extras-ooxmlimport/tdf60351, extras-ooxmlexport/
        // tdf160049_anchorMargin15).
        PlacedDrawing Held(PlacedDrawing drawing)
        {
            var box = new DrawingBox(anchor.Anchor);
            if (!context.Profile.KeepsDrawingsInMargins || cell != null || anchor.Part != context.MainPart
                || (string?)anchor.Anchor.Element(Docx.Drawings.Wp + "positionV")?.Attribute("relativeFrom") is not ("paragraph" or "line")
                || !box.Wraps)
                return drawing;
            var (_, above, _, below) = DrawingBox.Outward(box.Effect);
            var s = page.Section;
            var lowest = s.PageHeight - s.Bottom - below - drawing.Height;
            return drawing with { Y = Math.Max(Math.Min(drawing.Y, lowest), s.Top + above), HeldUp = drawing.Y > lowest + 0.01 };
        }
    }

    static PlacedDrawing Anchored(AnchorInline anchor, PageBox page, double paragraphTop, double lineTop, double columnLeft, double columnWidth,
        CellAnchor? cell = null, double? character = null, bool legacy = false, double? marginTop = null)
    {
        var element = anchor.Anchor;
        var alignedAcross = false;
        var box = new DrawingBox(element);
        var (w, h) = box.Extent;
        // The effect extents count outward only, for sizes relative to the page or the margins and for alignment.
        var effect = DrawingBox.Outward(box.Effect);
        var s = page.Section;
        // Sizes relative to the page or its margins (wp14:sizeRelH, wp14:sizeRelV; 0 % keeps the extent): shapes take
        // them as they are, pictures only both together, scaled evenly to fit them (measured with
        // docx/local/size-rel-shape and docx/local/size-rel), each less the effect extents across it (measured with
        // docx/local/relsz-*: 40 % of a 451.3 pt margin with 1.8 pt of effect extent right is 178.7 pt wide, with 1 pt
        // all round 178.5; 20 % of 697.9 pt high with 1 pt above and below 137.6; a picture fits into what is left;
        // scratch: powertools/WC/WC065-Textbox, DB-GlossaryDocuments/FooterContent-built).
        var oddPage = page.DisplayNumber % 2 == 1;
        var rightMargin = s.PageWidth - page.TextLeft - page.TextWidth;
        // Groups and canvases sized relative to the page go by their effect extents as shapes do and draw their
        // content as much larger as their own extent (a:xfrm a:ext) is than the anchor's (measured with
        // docx/local/gr-*: a group 50 % of the page wide with 1.2 pt of effect extent right 296.45 pt wide, centered as
        // 297.65 pt, in modes 14 and 15; docx/external/libreoffice/extras-ooxmlimport/groupshape-relsize: 90.9 % of
        // the page high, 10.55 pt of effect extent below, an own extent of 730.1 pt over an anchor's of 719.9 pt drawn
        // 765.4 pt high, centered as its share; core-ooxml-pass/fdo69616, extras-layout/tdf69648).
        var grouped = element.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Elements().FirstOrDefault()?.Name.LocalName is "wgp" or "wpc";
        double? Relative(string name, string percent, bool horizontal)
        {
            if (element.Element(Docx.Drawings.Wp14 + name) is not { } relative
                || !double.TryParse(relative.Element(Docx.Drawings.Wp14 + percent)?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var share)
                || share <= 0)
                return null;
            var frame = ((string?)relative.Attribute("relativeFrom"), horizontal) switch
            {
                ("page", true) => s.PageWidth,
                ("page", false) => s.PageHeight,
                ("leftMargin", _) => page.TextLeft,
                ("rightMargin", _) => rightMargin,
                ("topMargin", _) => s.Top,
                ("bottomMargin", _) => s.Bottom,
                // Across, Word takes the width of the margin on the other side than the one the drawing stands in
                // (measured with docx/local/srm-in, srm-out: margins of 127.6 and 72 pt, the inside on odd pages 72 pt
                // wide at the left, on even ones 127.6 pt at the right, the outside the other way round; docx/external/
                // libreoffice/extras-ooxmlexport/tdf133861_RelativeAnchorWidthFromInsideOutsideMargin, mirrored).
                ("insideMargin", true) => oddPage ? rightMargin : page.TextLeft,
                ("outsideMargin", true) => oddPage ? page.TextLeft : rightMargin,
                ("insideMargin", false) => oddPage ? s.Top : s.Bottom,
                ("outsideMargin", false) => oddPage ? s.Bottom : s.Top,
                (_, true) => page.TextWidth,
                (_, false) => s.PageHeight - s.Top - s.Bottom,
            };
            return Math.Max(0, frame * share / 100000 - (horizontal ? effect.Left + effect.Right : effect.Top + effect.Bottom));
        }
        var (relativeWidth, relativeHeight) = (Relative("sizeRelH", "pctWidth", true), Relative("sizeRelV", "pctHeight", false));
        if (element.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Docx.Drawings.Pic + "pic") != null)
        {
            if (relativeWidth is { } rw && relativeHeight is { } rh && w > 0 && h > 0)
            {
                var scale = Math.Min(rw / w, rh / h);
                (w, h) = (w * scale, h * scale);
            }
        }
        else
            (w, h) = (relativeWidth ?? w, relativeHeight ?? h);
        double x, y;
        if ((string?)element.Attribute("simplePos") is "1" or "true" && element.Element(Docx.Drawings.Wp + "simplePos") is { } simple)
        {
            x = DrawingBox.Points((string?)simple.Attribute("x"));
            y = DrawingBox.Points((string?)simple.Attribute("y"));
        }
        else
        {
            var odd = page.DisplayNumber % 2 == 1;
            var positionH = element.Element(Docx.Drawings.Wp + "positionH");
            var fromH = (string?)positionH?.Attribute("relativeFrom");
            var (x0, xw) = fromH switch
            {
                "character" when character is { } at => (at, 0.0),
                // In a table cell: its text area, or the cell inside its lines for the page and the page margins.
                "page" or "leftMargin" or "rightMargin" or "insideMargin" or "outsideMargin" when cell?.Cell is { } inCell => inCell,
                _ when cell?.Text is { } text => text,
                "page" => (0.0, s.PageWidth),
                "column" or "character" => (columnLeft, columnWidth),
                "leftMargin" => (0.0, page.TextLeft),
                "rightMargin" => (page.TextLeft + page.TextWidth, s.PageWidth - page.TextLeft - page.TextWidth),
                "insideMargin" => odd ? (0.0, page.TextLeft) : (page.TextLeft + page.TextWidth, s.PageWidth - page.TextLeft - page.TextWidth),
                "outsideMargin" => odd ? (page.TextLeft + page.TextWidth, s.PageWidth - page.TextLeft - page.TextWidth) : (0.0, page.TextLeft),
                _ => (page.TextLeft, page.TextWidth),
            };
            // Before Word 2013 an alignment in the column counts from the paragraph's indents, centering not, and an
            // aligned drawing placed down by an offset stands its effect extent lower (measured by agent AG in Word's
            // PDFs of LibreOffice's extras-ooxmlexport/tdf160049_anchorMargin14, mode 14: left aligned at a left indent
            // of 1701 twips, right aligned at a right indent of 1134, centered on the column, each 1.5 pt of effect
            // extent lower; offsets across not; tdf160049_anchorMargin2, tdf160077_layoutInCell).
            alignedAcross = legacy && positionH?.Element(Docx.Drawings.Wp + "align") != null;
            if (alignedAcross && fromH == "column")
                (x0, xw) = positionH!.Element(Docx.Drawings.Wp + "align")!.Value.Trim() switch
                {
                    "left" => (x0 + anchor.Indents.Left, xw - anchor.Indents.Left),
                    "right" => (x0, xw - anchor.Indents.Right),
                    _ => (x0, xw),
                };
            // Alignment places the shape with its effect extent (measured with docx/local/sa-d1, sa-d2: 80 pt left and
            // 36 pt above take a shape centered at the page's top 40 pt right and 36 pt down, with or without an effect;
            // sb-*, sc-*: each side counts where its alignment meets it, across the page and the margins), shapes sized
            // relative to the page or the margins too (relsz-3, -4, -5, -7: centered and right aligned with their
            // effect extents, the whole as wide as the share; docx/external/libreoffice/extras-ooxmlimport/
            // groupshape-relsize, powertools/DB-GlossaryDocuments/FooterContent-built).
            // A turned VML shape by its swapped box or its turned outline (see Vml.TurnedBox), with its effect extents.
            var turned = Docx.Vml.TurnedSides(element);
            // Another VML shape with its line (Vml.LineReachBox), in whole pixels from its box's corner: its left and
            // top pixel the first its line touches, its right and bottom the first past it (measured with
            // docx/local/vlh-*: 75 pt squares with lines of 0.75 and 3 pt 1 and 2 px from the page's right and bottom
            // edges; vlg-*: a 100 x 50 pt rectangle with a 3 pt line 2.67 and 2.33 pt; LibreOffice's
            // extras-ooxmlimport/n820504: a plaque centered on the page 0.375 pt right and 1.5 pt down of its box's
            // place, 89.8 → 99.8 %; core-objectpositioning/vml-vertical-alignment).
            static double Pixels(double points, bool up) => (up ? Math.Ceiling(points / 0.75 - 0.01) : Math.Floor(points / 0.75 + 0.01)) * 0.75;
            var line = Docx.Vml.LineReach(element);
            var (lineL, lineR) = line is { } across && positionH?.Element(Docx.Drawings.Wp + "align") != null
                ? (Pixels(across.L, up: true), Pixels(w + across.R, up: true) - w) : (0, 0);
            x = Position(positionH, x0, xw, w, odd, horizontal: true, (turned?.L ?? 0) + effect.Left + lineL, (turned?.R ?? 0) + effect.Right + lineR);
            // Before Word 2013 a drawing aligned left or right on the page stands in the margin beside the text, its
            // right edge at the left margin or its left edge at the right margin, as far as the page lets it, whatever
            // its effect extent (measured with docx/local/pgr-14, pgr2-12-*, pgr2-14-*: rectangles of 30, 61.5 and 100 pt,
            // right margins of 70.85 and 36 pt, effect extents of 0 to 3 pt and distances of 0 and 9 pt; on the page
            // from Word 2013 on, pgr-15, pgr2-15-*; docx/external/libreoffice/extras-ooxmlexport/tdf113183, found by
            // agent BG).
            if (legacy && fromH == "page" && cell?.Text == null
                && positionH?.Element(Docx.Drawings.Wp + "align")?.Value.Trim() is "left" or "right")
                x = positionH.Element(Docx.Drawings.Wp + "align")!.Value.Trim() == "left"
                    ? Math.Max(0, page.TextLeft - w) : Math.Min(page.TextLeft + page.TextWidth, s.PageWidth - w);
            var positionV = element.Element(Docx.Drawings.Wp + "positionV");
            var (y0, yh) = ((string?)positionV?.Attribute("relativeFrom")) switch
            {
                not ("paragraph" or "line") when cell?.Content is { } content => content,
                "page" => (0.0, s.PageHeight),
                "paragraph" => (paragraphTop, 0.0),
                "line" => (lineTop, 0.0),
                "topMargin" => (0.0, s.Top),
                "bottomMargin" => marginTop is { } footerMargin ? (footerMargin, s.PageHeight - footerMargin) : (s.PageHeight - s.Bottom, s.Bottom),
                "insideMargin" => odd ? (0.0, s.Top) : (s.PageHeight - s.Bottom, s.Bottom),
                "outsideMargin" => odd ? (s.PageHeight - s.Bottom, s.Bottom) : (0.0, s.Top),
                _ => (s.Top, s.PageHeight - s.Top - s.Bottom),
            };
            var (lineT, lineB) = line is { } down && positionV?.Element(Docx.Drawings.Wp + "align") != null
                ? (Pixels(down.T, up: true), Pixels(h + down.B, up: true) - h) : (0, 0);
            y = Position(positionV, y0, yh, h, odd, horizontal: false, (turned?.T ?? 0) + effect.Top + lineT, (turned?.B ?? 0) + effect.Bottom + lineB);
            // Before Word 2013 such a shape aligned one way and placed by an offset the other stands that way as far
            // past its offset as the offset is past the first pixel its line touches, counted from where the offset
            // counts from (measured with docx/local/vld-12, vld-14, vld-none: a triangle with a 3 pt line top aligned
            // 100 pt right of the page 104.75 pt right, not aligned at 100 pt, in mode 15 at 100 pt; vle-*: 100 to
            // 101.25 pt in steps of 0.25 pt, rectangles and triangles; vlj-12: 100 pt down from the page 101 pt down with
            // a 0.75 pt line, 101.75 pt with a 3 pt line, shapes in percent of the margins; n820504's plaque centered
            // across 36 pt down 39.75 pt down; extras-ooxmlexport/docxopenhyperlinkbox: a text box in percent 0 pt below
            // its paragraph's top at 70.85 pt 0.75 pt below it).
            if (legacy && line is { } reach)
            {
                if (positionH?.Element(Docx.Drawings.Wp + "posOffset") != null && positionV?.Element(Docx.Drawings.Wp + "align") != null)
                    x = x0 + 2 * (x - x0) - Pixels(x - x0 - reach.L, up: false);
                if (positionV?.Element(Docx.Drawings.Wp + "posOffset") != null && positionH?.Element(Docx.Drawings.Wp + "align") != null)
                    y = y0 + 2 * (y - y0) - Pixels(y - y0 - reach.T, up: false);
            }
            // In a cell a drawing that text goes around, aligned or placed by a percentage on the page or its margins,
            // stands at the cell's inside left and its content's top, the alignment and the percentage unseen (measured
            // with docx/local/cdr-pagec, cdr-pagecf: centered on the page at the cell's left line; cdr-pager: right of
            // the page and 2.5 % down at the cell's top left; docx/external/powertools/WC/WC048-Text-Box-in-Cell); one
            // without wrapping keeps its place (docx/external/libreoffice/extras-ooxmlimport/tdf97038: a picture behind
            // the text right and at the top of the cell's room).
            var wrapsInCell = cell != null && box.Wraps;
            if (wrapsInCell && cell?.Cell != null && fromH is "page" or "leftMargin" or "rightMargin" or "insideMargin" or "outsideMargin"
                && positionH?.Element(Docx.Drawings.Wp + "posOffset") == null)
                x = x0 + effect.Left;
            if (wrapsInCell && cell?.Content != null && (string?)positionV?.Attribute("relativeFrom") is not ("paragraph" or "line")
                && positionV?.Element(Docx.Drawings.Wp + "posOffset") == null)
                y = y0 + effect.Top;
            // There it also stays within the cell: across between the inner edges of its side lines, ending at the right
            // one or else beginning at the left one, and down not above its content's top (measured with
            // docx/local/ccl-col, ccl-left, ccl-up in modes 15 and 14: 150 pt boxes 120 pt right of and 50 pt left of the
            // column in a 200 pt cell end at its right and begin at its left, one 30 pt above its paragraph stands at the
            // cell's top; ccl-none, ccl-behind: without wrapping it keeps its place).
            if (wrapsInCell && cell?.Inside is { } inside)
                x = Math.Max(inside.Left, Math.Min(x, inside.Right - w));
            if (wrapsInCell && cell?.Content is { } within)
                y = Math.Max(y, within.Top + effect.Top);
            // A drawing that the text wraps around and that stands down from its paragraph or line stays on the page
            // across: past the right edge it ends there, past the left edge it begins there, wider than the page at its
            // left edge (measured with docx/local/clp-*: a 200 pt rectangle 450 pt right of the column or 500 pt right of
            // the page, wrapped square or top and bottom, ends at the page's edge, in modes 14 and 15; one 150 pt left of
            // the column begins at the page's edge, one of 700 pt there too; without wrapping or placed down from the
            // margin or the page it stays past the edge; docx/external/libreoffice/extras-ooxmlexport/tdf170908_delText).
            if (cell == null && (string?)positionV?.Attribute("relativeFrom") is null or "paragraph" or "line" && box.Wraps)
            {
                x = Math.Max(0, Math.Min(x, s.PageWidth - w));
                // Before Word 2013 it stays on the page down too: above the page's top it begins there, past its bottom
                // it ends there (measured with docx/local/vc-*: a 200 pt square 1000 pt above or 700 pt below its
                // paragraph, DrawingML or VML, wrapped square, at the page's top or ending at its bottom in modes 12 and
                // 14, unwrapped off the page; from Word 2013 the margins hold it; docx/external/libreoffice/
                // extras-ooxmlexport/tdf78902).
                if (legacy)
                    y = Math.Max(0, Math.Min(y, s.PageHeight - h));
            }
        }
        if (grouped && (relativeWidth != null || relativeHeight != null)
            && element.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Elements().FirstOrDefault()?.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "grpSpPr")?.Element(Ns.A + "xfrm")?.Element(Ns.A + "ext") is { } own
            && Docx.Drawings.Extent(element) is var (anchorWidth, anchorHeight) && anchorWidth > 0 && anchorHeight > 0)
        {
            if (relativeWidth != null)
                w *= DrawingBox.Points((string?)own.Attribute("cx")) / anchorWidth;
            if (relativeHeight != null)
                h *= DrawingBox.Points((string?)own.Attribute("cy")) / anchorHeight;
        }
        return new PlacedDrawing(element, anchor.Part, x, y, w, h)
        {
            Behind = (string?)element.Attribute("behindDoc") is "1" or "true",
            Order = long.TryParse((string?)element.Attribute("relativeHeight"), out var order) ? order : 0,
            Text = anchor.Text,
            ChildText = anchor.ChildText,
        };

        double Position(XElement? position, double origin, double size, double extent, bool odd, bool horizontal, double before, double after)
        {
            // An offset places the extent, but the effect extent where the drawing is sized relative to the page or
            // margins in that direction (measured with docx/local/relsz-2, -6, -9 to -11: 1 pt of effect extent all
            // round puts a shape 40 % of the margin wide at offset 0 1 pt right, 20 % high too 1 pt down; a shape of its
            // own size stands at the offset).
            if (position?.Element(Docx.Drawings.Wp + "posOffset")?.Value is { } offset)
                return origin + DrawingBox.Points(offset) + ((horizontal ? relativeWidth : relativeHeight) != null || !horizontal && alignedAcross ? before : 0);
            // A percentage places the extent with its effect extent before it, sized relative or not (measured with
            // docx/local/pcp-*: 20 % of the page across and 10 % down with 10 pt of effect extent left and top, from the
            // page and the margin, 10 pt right and down; docx/external/libreoffice/extras-ooxmlexport/
            // content-control-shape).
            if (position?.Element(Docx.Drawings.Wp14 + (horizontal ? "pctPosHOffset" : "pctPosVOffset"))?.Value is { } percent)
                return origin + size * DrawingBox.Points(percent) * 12700 / 100000 + before;
            var whole = before + extent + after;
            // Inside is the left or the top of the area on odd pages, the right or the bottom on even ones, whichever
            // margin the area is (measured with docx/local/vio-*: relative to the top and the bottom margin, the
            // margin and the page; libreoffice/core-objectpositioning/inside-outside-vert-align).
            return before + position?.Element(Docx.Drawings.Wp + "align")?.Value switch
            {
                "right" or "bottom" => origin + size - whole,
                "center" => origin + (size - whole) / 2,
                "inside" => odd ? origin : origin + size - whole,
                "outside" => odd ? origin + size - whole : origin,
                _ => origin,
            };
        }
    }

    /// <summary>A paragraph's borders and shading (w:pBdr, w:shd).</summary>
    internal sealed class Frame
    {
        public BorderLine? Top, Left, Bottom, Right, Between;
        public RgbColor? Fill;
        XElement? borders, shading;
        double indentLeft, indentRight;

        public static Frame? Read(ParagraphFormat format)
        {
            var fill = Shading.Color(format.Shading);
            if (format.Borders == null && fill == null)
                return null;
            var b = format.Borders;
            var frame = new Frame
            {
                Top = BorderLine.Read(b.Child("top")),
                Left = BorderLine.Read(b.Child("left") ?? b.Child("start")),
                Bottom = BorderLine.Read(b.Child("bottom")),
                Right = BorderLine.Read(b.Child("right") ?? b.Child("end")),
                Between = BorderLine.Read(b.Child("between")),
                Fill = fill,
                borders = b,
                shading = format.Shading,
                // From a hanging first line's start (measured with docx/local/pbh-*: hanging 360 to 1080 twips with and
                // without a number, borders and shading alone, in modes 14 and 15; paragraphs of the same left indent
                // but other first lines in boxes of their own).
                indentLeft = Math.Min(format.Left ?? 0, (format.Left ?? 0) + (format.FirstLine ?? 0)),
                indentRight = format.Right ?? 0,
            };
            return frame.Top is { Visible: true } || frame.Left is { Visible: true } || frame.Bottom is { Visible: true }
                || frame.Right is { Visible: true } || frame.Between is { Visible: true } || fill != null ? frame : null;
        }

        /// <summary>
        /// The room a shadowed top line keeps above the box, its width; a shadowed bottom line's band below it (measured
        /// with docx/local/psh-*: 3 and 1 pt lines on all sides, the top or the bottom alone, around one or two
        /// paragraphs; docx/external/libreoffice/extras-ooxmlexport/para-shadow).
        /// </summary>
        public double TopShadow => Top is { Visible: true, Shadow: true } t ? t.Total : 0;

        public double BottomShadow => Bottom is { Visible: true, Shadow: true } b ? b.Total : 0;

        /// <summary>Whether the frame has a line to show.</summary>
        bool Lined => Top is { Visible: true } || Left is { Visible: true } || Bottom is { Visible: true } || Right is { Visible: true }
                      || Between is { Visible: true };

        /// <summary>The frame of an HTML division at its indents (the margins of it and the divisions around it).</summary>
        public static Frame Division(HtmlDiv div, double indentLeft, double indentRight) => new()
        {
            Top = div.Top, Left = div.LeftLine, Bottom = div.Bottom, Right = div.RightLine,
            indentLeft = indentLeft, indentRight = indentRight,
        };

        /// <summary>
        /// Whether another paragraph's frame continues this one: the same borders and indents, whatever their shading
        /// (measured with docx/local/bordergroup-b1, -b3, -b4: bordered paragraphs with different fills or none share
        /// their lines, the between line too; docx/external/libreoffice/extras-ooxmlexport/
        /// tdf153964_topMarginAfterBreak15); paragraphs without borders, by their shading.
        /// </summary>
        public bool Same(Frame? other) =>
            other != null && XNode.DeepEquals(borders, other.borders) && (Lined || XNode.DeepEquals(shading, other.shading))
            && Math.Abs(indentLeft - other.indentLeft) < 0.01 && Math.Abs(indentRight - other.indentRight) < 0.01;

        /// <summary>
        /// The box from <paramref name="boxTop"/> to <paramref name="boxBottom"/>: shading, then the sides, the top or
        /// between line and the bottom line. The shading reaches 1.4 pt left and 1.55 pt right of the text, the side
        /// lines stand their space outside it (measured with docx/local/pbdr).
        /// </summary>
        public void Draw(PageBox page, double columnLeft, double columnWidth, ParagraphFormat format, double boxTop, double boxBottom, bool top, bool bottom, double? between,
            IReadOnlyList<(double Top, double Bottom, List<(double Start, double End)> Segments)>? cuts = null) =>
            Draw(page.Shading, page.Borders, columnLeft, columnWidth, boxTop, boxBottom, top, bottom, between, cuts);

        /// <summary>The shading alone, as a paragraph without borders has it: 1.4 pt left and 1.55 pt right of the text.</summary>
        public void DrawBare(PageBox page, double columnLeft, double columnWidth, double top, double bottom)
        {
            if (Fill is { } fill && bottom > top + 0.01)
                page.Shading.Add(new FillRect(columnLeft + indentLeft - 1.4, top, columnWidth - indentLeft - indentRight + 2.95, bottom - top, fill));
        }

        /// <param name="cuts">
        /// The lines that floating objects cut, top to bottom, whose shading covers only their segments, from 1.4 pt
        /// before a cut start to 1.55 pt after a cut end (measured with docx/local/psl-square, psl-behind: a yellow
        /// paragraph beside a rectangle wrapped square, or behind the text tight, shaded left and right of it line by
        /// line; docx/external/libreoffice/extras-ooxmlimport/tdf60351).
        /// </param>
        public void Draw(List<FillRect> shadings, List<FillRect> lines, double columnLeft, double columnWidth, double boxTop, double boxBottom, bool top, bool bottom, double? between,
            IReadOnlyList<(double Top, double Bottom, List<(double Start, double End)> Segments)>? cuts = null)
        {
            var innerLeft = columnLeft + indentLeft - 1.4 - (Left is { Visible: true } il ? il.Space : 0);
            var innerRight = columnLeft + columnWidth - indentRight + 1.55 + (Right is { Visible: true } ir ? ir.Space : 0);
            var left = innerLeft - (Left is { Visible: true } l ? l.Total : 0);
            var right = innerRight + (Right is { Visible: true } r ? r.Total : 0);
            if (Fill is { } fill)
            {
                // Between the top and the bottom line, which leave their strokes' gaps unshaded (measured with
                // docx/local/bsp2-other: a double line's 3 pt between its strokes white).
                var from = boxTop + (top && Top is { Visible: true } shadedTop ? shadedTop.Total : 0);
                var to = boxBottom - (bottom && Bottom is { Visible: true } shadedBottom ? shadedBottom.Total : 0);
                foreach (var (cutTop, cutBottom, segments) in cuts ?? [])
                {
                    if (cutTop > from + 0.01)
                        shadings.Add(new FillRect(innerLeft, from, innerRight - innerLeft, cutTop - from, fill));
                    foreach (var (start, end) in segments)
                    {
                        var segmentLeft = start <= indentLeft + 0.01 ? innerLeft : columnLeft + start - 1.4;
                        var segmentRight = end >= columnWidth - indentRight - 0.01 ? innerRight : columnLeft + end + 1.55;
                        if (segmentRight > segmentLeft)
                            shadings.Add(new FillRect(segmentLeft, cutTop, segmentRight - segmentLeft, cutBottom - cutTop, fill));
                    }
                    from = Math.Max(from, cutBottom);
                }
                if (to > from + 0.01)
                    shadings.Add(new FillRect(innerLeft, from, innerRight - innerLeft, to - from, fill));
            }
            // Double and thin-and-thick lines in their strokes, as tables draw them (docx/external/poi/FancyFoot: a
            // thin-thick top line, 3 pt above 0.75 pt).
            // Embossed and engraved lines in their shades (docx/local/b3d-*).
            void Across(BorderLine line, double y)
            {
                foreach (var (offset, thickness, color) in TableLayout.Painted(line))
                    lines.Add(new FillRect(left, y + offset, right - left, thickness, color));
            }
            void Down(BorderLine line, double x)
            {
                foreach (var (offset, thickness, color) in TableLayout.Painted(line))
                    lines.Add(new FillRect(x + offset, boxTop, thickness, boxBottom - boxTop, color));
            }
            // Shadows under the lines, black bands of a line's width as page borders have them: right of a shadowed
            // right line from below the top line down through the bottom line's width, below a shadowed bottom line from
            // inside the left line to the right shadow's edge (docx/local/psh-all, psh-right, psh-bottom).
            var topWidth = top && Top is { Visible: true } shadowTop ? shadowTop.Total : 0;
            if (Right is { Visible: true, Shadow: true } shadowRight)
                lines.Add(new FillRect(right, boxTop + topWidth, shadowRight.Total, boxBottom + (bottom ? BottomShadow : 0) - boxTop - topWidth, RgbColor.Black));
            if (bottom && Bottom is { Visible: true, Shadow: true } shadowBottom)
            {
                var from = left + (Left is { Visible: true } shadowLeft ? shadowLeft.Total : 0);
                lines.Add(new FillRect(from, boxBottom, right + (Right is { Visible: true } r2 ? r2.Total : 0) - from, shadowBottom.Total, RgbColor.Black));
            }
            if (Left is { Visible: true } leftLine)
                Down(leftLine, left);
            if (Right is { Visible: true } rightLine)
                Down(rightLine, right - rightLine.Total);
            if (top && Top is { Visible: true } topLine)
                Across(topLine, boxTop);
            if (between is { } betweenY && Between is { Visible: true } betweenLine)
                Across(betweenLine, betweenY);
            if (bottom && Bottom is { Visible: true } bottomLine)
                Across(bottomLine, boxBottom - bottomLine.Total);
        }
    }

    /// <summary>
    /// How far a paragraph with mirrored indents (w:mirrorIndents) moves on an even page: its left indent becomes the
    /// inside one, its right the outside, the two trading places where page numbers are even; its lines break as they
    /// would (measured with docx/local/mirror).
    /// </summary>
    double Mirror(ParagraphFormat format) =>
        format.MirrorIndents == true && page!.DisplayNumber % 2 == 0 ? (format.Right ?? 0) - (format.Left ?? 0) : 0;

    void PlaceRow(RowBox row, double left)
    {
        // Lines of cells merged down that end below a table row go on with the next (RowBox.Overflow).
        if (!row.Header)
        {
            var (own, below) = row.Overflow();
            if (below.Count > 0)
            {
                row = own;
                mergedDown.AddRange(below);
            }
        }
        var anchors = new List<CellAnchor>();
        TableLayout.Draw(row, row.Height, left, y, page!.Lines, page.Shading, page.Borders, page.Strokes, anchors);
        foreach (var anchor in anchors)
            page.Drawings.Add(PlaceAnchor(anchor.Anchor, page, anchor.ParagraphTop, anchor.LineTop, left, page.TextWidth, anchor, anchor.CharacterX));
        y += row.Height;
        columnEmpty = false;
        sectionShown = true;
        breakPage = false;
    }

    /// <summary>Lines of cells merged down below the table row placed last, for the next (RowBox.Overflow).</summary>
    readonly List<CellPart> mergedDown = [];

    /// <summary>The lines below a row at the current position.</summary>
    void Below(RowBox row, double left)
    {
        foreach (var fill in row.BottomBorders)
            page!.Borders.Add(fill with { X = left + fill.X, Y = y + fill.Y });
        page!.Strokes.AddRange(row.BottomStrokes.Select(s => s.Offset(left, y)));
    }

    /// <summary>
    /// A part of a paragraph in one column or page as its drawings stand from it: the page, the part's top (for the
    /// paragraph's first part its own top where its first line stands as planned, else that line's) and the column.
    /// </summary>
    readonly record struct ParagraphPart(PageBox Page, double Top, double Left, double Width);

    /// <summary>
    /// What a floating drawing anchored in a paragraph stands from (see PlaceAnchor): the part of the paragraph its
    /// anchor's line is in, that line's top before floating objects pushed it down, the anchor's place across, how far
    /// those objects pushed the line down and where its room begins after them, and whether the line holds nothing
    /// (for drawings text does not go around, see LayoutProfile.PushedLinesMoveDrawings).
    /// </summary>
    readonly record struct AnchorFrame(ParagraphPart Part, double LineTop, double Character, double Push, double? RoomStart, bool EmptyLine);

    void Place(List<Block> blocks, int index)
    {
        var paragraph = (ParagraphBlock)blocks[index];
        // An empty paragraph that holds a section break after other text of its section takes no room before a
        // continuous section, no line and no space (measured with docx/local/colsect-*: sections of one or two columns,
        // in balanced columns too, in modes 12 and 15); alone in its section, before a section on a new page (whose
        // first paragraph its space after still shortens) or with text it is a line as any other
        // (docx/external/libreoffice/extras-ooxmlexport/tdf64372_continuousBreaks, tdf170003_bottomSpacing), except at
        // the top of a page a page break began, which the section after it takes (docx/local/pbs-1).
        // Text before it means text of the flow: a table in a frame alone before it leaves it a line, and an empty one in
        // a frame takes none (measured with docx/local/frsb-12, -14, -15: after a table in a frame an empty paragraph
        // ending the section takes a line, framed as the table's paragraphs none; docx/external/libreoffice/
        // extras-ooxmlexport/tdf164474).
        if (paragraph is { EndsSection: not null } && (paragraph.Format.Frame == null || index > 0)
            && (FlowBefore(blocks, index) && nextContinuous || paragraph.Format.Frame != null || breakPage && !nextContinuous && page!.Lines.Count == 0)
            && paragraph.Label is not { Own.Length: > 0 } && paragraph.Content.All(c => c is BookmarkInline or BookmarkEndInline or PermissionInline))
        {
            foreach (var bookmark in paragraph.Content.OfType<BookmarkInline>())
                page!.Bookmarks.Add((bookmark.Name, Column(column).Left, y));
            // At the top of a page its space after is the section's last, which the next paragraph's space before
            // has to exceed (measured with docx/local/sbb-a, sbb-b, sbb-c: after a page break, 200 pt after leave
            // nothing of 150 pt before, none all of it, in modes 14 and 15; docx/external/libreoffice/
            // extras-ooxmlexport/tdf170119_bottomSpacing).
            // Before a section on a new page its space after still shortens the next paragraph's space before and its
            // style still counts for contextual spacing (measured with docx/local/secb-b, secb-e in modes 12, 14 and 15:
            // a heading with 24 pt before 18 pt down below 6 pt after, at the top after one of its style with contextual
            // spacing; docx/external/libreoffice/extras-ooxmlexport/tdf170602_exportContext).
            sectionBreakAfter = SpaceAfter(paragraph, index + 1 < blocks.Count ? blocks[index + 1] : null);
            if (columnEmpty && nextContinuous || !nextContinuous)
            {
                pendingAfter = SpaceAfter(paragraph, index + 1 < blocks.Count ? blocks[index + 1] : null);
                pendingOwnAfter = OwnAfter(paragraph);
            }
            if (!nextContinuous)
            {
                stylePrevious = paragraph;
                page!.Passed.Add(paragraph);
            }
            return;
        }
        // A page or column break that begins a paragraph comes before it where the rest of the paragraph goes on in
        // the next page or column (LeadingBreak): its number, its space before and its text go there (measured with
        // docx/external/libreoffice/extras-ooxmlexport/wDateValueFormat: "1 Status" after a page break at the start of
        // its heading stands at the top of the next page, 12 pt down).
        var led = false;
        if (LeadingBreak(paragraph) is { } leading && !(leading.Type == "page" && BreakUnseen(paragraph)))
        {
            // The line of the break keeps the paragraph's shading where it stands, without its borders, below the space
            // after the paragraph before and without its own space before (measured with docx/local/bsp-lead-15, -14:
            // 12.7 pt of shading 8 pt below the box before, which closes; docx/external/libreoffice/extras-ooxmlexport/
            // tdf153964_topMarginAfterBreak14, -15).
            if (Frame.Read(paragraph.Format) is { Fill: not null } bareFrame)
            {
                var (bareLeft, bareWidth) = Column(column);
                var bareTop = y + (columnEmpty ? 0 : pendingAfter);
                bareFrame.DrawBare(page!, bareLeft + Mirror(paragraph.Format), bareWidth, bareTop, bareTop + layout.Layout(paragraph, bareWidth, settings)[0].Height);
            }
            led = true;
            if (leading.Type == "page")
                NewPage(section);
            else
                NextColumn(natural: false);
            top = Top.Leading;
            paragraph = paragraph with { Content = paragraph.Content.Where(i => !ReferenceEquals(i, leading)).ToList() };
        }
        var next = index + 1 < blocks.Count ? blocks[index + 1] as ParagraphBlock : null;
        var format = paragraph.Format;
        var (left, width) = Column(column);
        var lines = layout.Layout(paragraph, width, settings);

        var brokeBefore = false;
        if (format.PageBreakBefore == true && !(columnEmpty && column == 0 && page!.Lines.Count == 0))
        {
            NewPage(section);
            top = Top.Break;
            brokeBefore = true;
            (left, width) = Column(column);
        }
        // A paragraph pushed off the page its drawing goes back and forth on (see Paginate) begins the next column, a
        // new page in a section of one column (LibreOffice's extras-layout/fdo43573-2-min: two columns, the second of
        // the page filled as in Word, not left empty).
        else if (settled.ForcedBreaks.Contains(blocks[index].Id) && (section.Columns > 1 ? !columnEmpty : !(columnEmpty && column == 0 && page!.Lines.Count == 0)))
        {
            NextColumn(natural: true);
            (left, width) = Column(column);
        }

        // A boxed or shaded paragraph whose first line holds only a page or column break draws that line's shading
        // alone, without its borders and its space before (measured with docx/local/bsp-only-15, -14: 20.7 pt of
        // shading, the line and the space after, 8 pt below the box before, which closes).
        var bare = lines.Count > 0 && BreakOnly(lines[0]) && Frame.Read(format) != null;

        // Space between this paragraph and the previous one.
        var before = bare ? 0 : SpaceBefore(paragraph);
        var spacing = SpaceAbove(before, brokeBefore);
        var gap = spacing.Here;
        balanced = false;

        // Keep with next and keep lines together: move the paragraph to the next column when its keep chain has to
        // break here (ChainBreaks), when it keeps its lines together and they do not fit, also more than a column of
        // them (measured with docx/local/kc7, kc9), or when its first lines do not fit but would in an empty column. A
        // paragraph the one before it is kept with continues that one's chain, which was decided at its start: at the
        // top of a column a chain taller than the rest of it breaks where it must (docx/local/kc1, kc4), not at the top
        // of the columns of a section that begins below other text of the page, where it moves on as elsewhere, column
        // by column (docx/local/kcs-2-*, kcs-3-*: a heading kept with a paragraph, two lines from the page's end, takes
        // a section of two or three columns to the next page).
        while (!columnEmpty || columnTop > page!.BodyTop + 0.01)
        {
            var chained = previous != null ? previous.Format.KeepNext == true : tableKept;
            // A paragraph that goes on with a unit of kept lines needs room only for the notes before the unit.
            var bottom = chained && keptNotes is { } open && open.Page == page ? Math.Min(page!.BodyBottom - open.Height, bodyEnd ?? double.MaxValue) : Bottom;
            bool moves;
            var counted = MarkLine(paragraph, lines, lines.Count - 1) ? lines.Take(lines.Count - 1).ToList() : lines;
            if (format.KeepNext == true && index + 1 < blocks.Count && blocks[index + 1] is ParagraphBlock or TableBlock && !chained)
                moves = ChainBreaks(blocks, index, lines, width, y + gap);
            else if (format.KeepLines == true)
                moves = y + gap + Needed(counted) > bottom + 0.01;
            else
            {
                var needed = gap + FirstLines(counted, format);
                moves = y + needed > bottom + 0.01 && needed - gap <= Bottom - page!.BodyTop + 0.01;
            }
            if (!moves || MarkBeforePage(paragraph))
                break;
            NextColumn(natural: true);
            var laid = width;
            (left, width) = Column(column);
            // In a column of another width (w:cols with w:equalWidth 0) the paragraph breaks its lines anew before it
            // is measured there (measured with docx/local/cc-*: openxml-sdk/complex0's heading, two lines in a 264 pt
            // column, needs four in the 132 pt one beside it and goes to the next page; in one 216 pt wide, cc-equal,
            // or shortened to a word, cc-short, it stays in that column).
            if (Math.Abs(width - laid) > 0.01)
                lines = layout.Layout(paragraph, width, settings);
            gap = 0;
        }

        // Paragraph borders and shading (catalog: paragraph.borders): paragraphs with the same borders form one box,
        // the top line above the first, the bottom line below the last, the between line between them; a line and
        // its space take room in the paragraph.
        // A paragraph goes on with the box of the one before it in another column or page too, without a top line
        // there; one that begins with a page or column break opens a box of its own after it, and the box before it
        // closes (measured with docx/local/bsp-end-15, -14, bsp-lead-*, bsp-only-*, bsp2-flow, bsp2-flowc).
        var frame = Frame.Read(format);
        var continues = frame != null && !led && previous != null && (!columnEmpty || top == Top.Break) && frame.Same(Frame.Read(previous.Format));
        var nextContinues = frame != null && next != null && !StartsWithBreak(next) && frame.Same(Frame.Read(next.Format));
        // The box of a paragraph that goes on with the one before begins below that one's space after, which lies in
        // the box before (bsp2-gap: 8 pt after "A" in its shading, the 22 pt more before "B" in B's); at the top of a
        // column the space before it dropped lies above the column (bsp-end, bsp2-flow: "B" at the top margin, its
        // shading 22 pt above it).
        var boxTop = !continues ? y : columnEmpty ? y - Math.Max(0, spacing.Between - pendingAfter - gap) : y + pendingAfter;
        // The paragraph's top for drawings placed relative to it: its own space before lies inside it.
        var ownBefore = columnEmpty ? gap : Math.Max(0, gap - pendingAfter);
        // HTML divisions with borders frame their paragraphs like paragraph borders, at the division's indents; a
        // division's top line and space come before its first paragraph, its bottom line after its last (measured with
        // docx/local/html-divs).
        var divisions = Divisions(format.DivId);
        var previousDivisions = previous != null ? context.Divs.Chain(previous.Format.DivId).Select(d => d.Id).ToHashSet() : [];
        var nextDivisions = next != null ? context.Divs.Chain(next.Format.DivId).Select(d => d.Id).ToHashSet() : [];
        var divisionTops = new double[divisions.Count];
        for (var d = 0; d < divisions.Count; d++)
            divisionTops[d] = y;
        y += gap;
        for (var d = 0; d < divisions.Count; d++)
        {
            if (!columnEmpty && previousDivisions.Contains(divisions[d].Div.Id))
                continue;
            divisionTops[d] = y;
            y += divisions[d].Frame.Top is { Visible: true } dt ? dt.Space + dt.Total : 0;
        }
        var paragraphTop = (Page: page!, Top: y - ownBefore);
        var betweenAt = y;
        double? betweenLine = null;
        if (frame != null)
        {
            if (bare)
                boxTop = y;
            else if (!continues)
            {
                y += frame.TopShadow;
                boxTop = y;
                y += frame.Top is { Visible: true } t ? t.Space + t.Total : 0;
            }
            else if (frame.Between is { Visible: true } between && !columnEmpty)
            {
                // The between line has its space above and below it (measured with docx/local/pbdr).
                betweenAt = y + between.Space;
                betweenLine = betweenAt;
                y += 2 * between.Space + between.Total;
            }
        }
        // An object kept free above and below that reaches down to its paragraph's place pushes the lines already in
        // the column that it overlaps below it, the page laid out again with it; where the paragraph would then no
        // longer begin in the column, it takes the paragraph to the next, where the object then stands (measured with
        // docx/local/uc-1 to uc-5, ud-*: lines before an anchor paragraph go below a rectangle at the top margin of 100
        // or 650 pt, before one of 690 pt they stay and the anchor goes to the next page with it; docx/generated/
        // wrapping, docx/local/compat11-wrapping: a rectangle ending above the anchor, the rest of the page stays
        // empty; tall-t: one ending above the anchor, which stays, a picture line before it going below it;
        // docx/external/libreoffice/extras-layout/fdo43573-2-min).
        var wasEmpty = columnEmpty;
        // Drawings anchored after a page or column break of the paragraph stand with its later part (see the placing
        // below), not beside its first lines (found by agent BE in Word's PDF of docx/external/libreoffice/
        // extras-ooxmlexport/tdf153613_anchoredAfterPgBreak6: a picture wrapped square after "x" and a page break
        // only on the second page, "x" at the first page's top).
        var firstPart = FirstPartAnchors(paragraph);
        // How far below the first line's top the line of an anchor ends, the paragraph's space after below its last.
        double LineBottom(AnchorInline anchor)
        {
            var at = Math.Max(0, lines.FindIndex(l => l.Anchors?.Any(a => ReferenceEquals(a, anchor)) == true));
            return lines.Take(at + 1).Sum(l => l.Height) + (at >= lines.Count - 1 ? SpaceAfter(paragraph, index + 1 < blocks.Count ? blocks[index + 1] : null) : 0);
        }
        if (!columnEmpty && firstPart.Any(anchor =>
                ExclusionOf(PlaceAnchor(anchor, page!, paragraphTop.Top, y, left, width)) is { TopAndBottom: true } e && e.Top < y - 0.01 && e.Bottom > page!.BodyTop
                && Pushed(e) is { } top && e.Bottom + y - top + (lines.Count > 0 ? lines[0].FitHeight : 0) > Bottom + 0.01))
        {
            NextColumn(natural: true);
            (left, width) = Column(column);
        }
        // An object that wraps text and moves with its paragraph, moved up by the bottom margin (see Unpinned), takes the
        // paragraph to the next page where it then reaches with its effect extent above the bottom of its line, the
        // paragraph's space after below a last line, from Word 2013 (measured with docx/local/af-over, af-over-tb,
        // af-huge: a text box 330 or 700 pt high, square or top and bottom, at the top of a paragraph 500 pt down a
        // Letter page goes to the next page with it; af-fit, 180 pt, stays; af-over-none in front of the text,
        // af-over-page and af-over-margin placed from the page or the margin stay; af-over-14 in Word 2010 stays; efg-15,
        // efh-15: 50 pt below a line from 125.35 to 136.85 pt rectangles of 60 to 71 pt stay, ending at the margin, of
        // 71.5 pt and more go, below a line of 20 pt text one of 60 pt; efi-15: one of 68 pt with 3 pt of effect extent
        // above it goes, with 4 pt of distance from the text stays, 12 pt of space after take one of 65 pt along, a line
        // of 1.5 lines not; docx/external/libreoffice/extras-ooxmlexport/tdf123243, tdf115719).
        else if (!columnEmpty && context.Profile.HeldDrawingsTakeParagraph && firstPart.Any(anchor =>
                     PlaceAnchor(anchor, page!, paragraphTop.Top, y, left, width) is { HeldUp: true } placed && ExclusionOf(placed) != null
                     && placed.Y - Math.Max(0, new DrawingBox(placed.Drawing).Effect.Top) < y + LineBottom(anchor) - 0.01))
        {
            NextColumn(natural: true);
            (left, width) = Column(column);
        }
        // Moved on before its first line, the paragraph begins at the top of the new column, where from Word 2013 on the
        // drawings placed from it stand (measured with docx/local/af-over-tb: a text box wrapped top and bottom at the
        // next page's top, the paragraph's line below it); before, they stand as far down as they did (docx/external/
        // libreoffice/extras-layout/fdo43573-2-min, mode 14: a picture 2.7 pt below an empty paragraph moved to the right
        // column stands where it stood in the left; so do drawings that go back and forth, np-a14, np-a15). So does its
        // box.
        if (!wasEmpty && columnEmpty && context.Profile.DrawingsFollowMovedParagraph)
            paragraphTop = (page!, y);
        if (frame != null && !bare && !wasEmpty && columnEmpty)
        {
            boxTop = continues ? y - spacing.Excess : y + frame.TopShadow;
            if (!continues)
                y = boxTop + (frame.Top is { Visible: true } opening ? opening.Space + opening.Total : 0);
            betweenLine = null;
        }
        // Lines beside floating objects of the page are laid out again in the room the objects leave; the paragraph's
        // own objects, placed after its lines, count where they will stand (not where the run before put them).
        var exclusions = PageExclusions();
        // From Word 2013 on an object of the paragraph taller than the page's body, its effect extents with it, leaves
        // the paragraph's first line where it would stand without it and takes the lines after it below it, whatever its
        // wrapping (measured with docx/local/tlw-15: rectangles of 201 pt, and of 190 pt with a bottom effect extent of
        // 12 pt, on a body of 200 pt, wrapped square or top and bottom: the first line over them, the second on the next
        // page; 199 pt, and 190 pt 12 pt apart from the text, wrap; tlw-14, tlw-12 wrap; bhy-*: docx/external/
        // libreoffice/extras-ooxmlexport/tdf160049_anchorMargin15, a picture of 117.75 pt and 2.25 pt of effect extents
        // on a body of 118 pt, wrapped through on its left), before only where it is wrapped through or tight
        // (docx/local/bwr-14-*: a square of 117.75 pt with those effect extents wrapped through on its left leaves the
        // line over it, behind the text or in front of it, wrapped square it does not, nor one of 100 pt;
        // tdf160049_anchorMargin14).
        var tall = new List<Exclusion>();
        var owned = new Dictionary<AnchorInline, Exclusion>(ReferenceEqualityComparer.Instance);
        // Each where it then stands, clear of the paragraph's drawings before it (see Clear; docx/local/aox-*, aoy-*:
        // the text beside a rectangle that may not overlap where it moved, not where it was placed).
        void Own()
        {
            var placedOwn = new List<PlacedDrawing>();
            foreach (var anchor in firstPart)
            {
                var placed = PlaceAnchor(anchor, page!, paragraphTop.Top, y, left, width, before: [.. page!.Drawings, .. placedOwn]);
                placedOwn.Add(placed);
                if (ExclusionOf(placed) is { } own)
                {
                    owned[anchor] = own;
                    if (own.TopAndBottom && Above(anchor.Anchor) && page!.Lines.Any(l => !l.Story && l.Left > left - 0.01 && l.Left < left + width - 0.01
                            && l.Top < own.Bottom - 0.01 && l.Top + l.Line.Height > own.Top + 0.01))
                        reaching.TryAdd(anchor.Anchor, (page.Number, placed.X, placed.Y));
                    if ((context.Profile.TallDrawingsTakeLines || new DrawingBox(placed.Drawing).Tight)
                        && ExtentHeight(placed) > page!.BodyBottom - page.BodyTop + 0.01)
                    {
                        own = own with { TopAndBottom = true };
                        tall.Add(own);
                    }
                    exclusions.RemoveAll(e => e.Source != null && ReferenceEquals(e.Source, own.Source));
                    exclusions.Add(own);
                }
            }
        }
        Own();
        // Before Word 2013 a paragraph's space after counts against the floating table it stands with (the one before
        // it): where it reaches the table, the paragraph goes below it (measured with docx/local/fla-16-*-14: an empty
        // paragraph and "Anchor", 13.8 pt high with 8 pt after, above a table 16 pt below their top go below it; without
        // space after, or with the table 24 pt down, they stay; from Word 2013 on, fla-*-15, they stay; LibreOffice's
        // core-text/floattable-overlap and floattable-wrap-empty-para-legacy, found by agents BK and BM; uitest/
        // tdf124675: a later paragraph of two lines keeps its first above the table). So does it against any drawing
        // wrapped top and bottom (docx/local/saw-14-*: "Second" and an empty paragraph with 10 pt after, 2 pt above a
        // rectangle anchored in the paragraph before, go below it, 12 pt above it they stay; in mode 15 they stay,
        // saw-15-*; docx/external/libreoffice/extras-ooxmlexport/shape-theme-preservation).
        var raisedAfter = 0.0;
        if (context.Profile.SpaceAfterMeetsFloats && SpaceAfter(paragraph, index + 1 < blocks.Count ? blocks[index + 1] : null) is var spaceAfter and > 0.01)
        {
            for (var i = 0; i < exclusions.Count; i++)
            {
                if (!exclusions[i].Frame && !exclusions[i].Story
                    && (exclusions[i].Anchor?.Id == paragraph.Id || exclusions[i].Source != null && exclusions[i].TopAndBottom))
                {
                    exclusions[i] = exclusions[i] with { Top = exclusions[i].Top - spaceAfter };
                    raisedAfter = spaceAfter;
                }
                // So does the space after of the paragraph right before a table whose distance above it the lines before it
                // go around, for its last line (docx/local/ftb-12-narrow: 8 pt after the last line reaching 43 twips above
                // the table put that line beside it, the line before it not).
                else if (exclusions[i].Banded is { } table && index + 1 < blocks.Count && blocks[index + 1].Id == table.Id
                         && y + lines.Sum(l => l.Height) is var linesBottom && exclusions[i].Top >= linesBottom - 0.01
                         && exclusions[i].Top < linesBottom + spaceAfter - 1)
                    exclusions[i] = exclusions[i] with { Top = linesBottom - 0.1 };
            }
        }
        // Before Word 2013 a paragraph whose floating table (the one standing with it) reaches beside its first line has
        // that line below the page frames over the column's edges it meets, wrapped around or not (measured with
        // docx/local/fwa-g-14, fwa-l-14, fwa-n-14, fwa-p-14, fwa-r-14: a frame 109.75 pt wide over the right or the left
        // margin, its text 8.5 pt high 13.25 pt below the paragraph's top, and a table 28.8 pt below it: the empty or
        // "Text" 36 pt line below the frame; fwa-a-14: without the table, fwa-q-14: the table below the line, fwa-k-14,
        // fwa-h-14: a frame inside the column, fwa-g-15: in mode 15, beside it; LibreOffice's extras-ooxmlexport/
        // tdf164500_framePrBeforeTable2, found by agent CC).
        if (context.Profile.FramesLowerTableParagraphs && lines.Count > 0
            && exclusions.Any(e => !e.Frame && !e.Story && e.Anchor?.Id == paragraph.Id && e.Top < y + lines[0].Height - 0.01))
        {
            for (var i = 0; i < exclusions.Count; i++)
            {
                var e = exclusions[i];
                if (e is { Frame: true, WithText: false, DropCap: false, TopAndBottom: false } && (e.Left < left - 0.01 || e.Right > left + width + 0.01)
                    && e.Top < y + lines[0].Height - 0.01 && e.Bottom > y + 0.01)
                    exclusions[i] = e with { TopAndBottom = true };
            }
        }
        // Whether the lines were broken beside floating objects, which another column or page may not have.
        var laidAround = false;
        // Whether the box begins below a first line pushed below objects (see LayAround).
        var pushedBox = false;
        void LayAround()
        {
            if (exclusions.Count == 0)
                return;
            var linesTop = y;
            var bottom = linesTop + lines.Sum(l => l.Height);
            if (exclusions.Any(e => e.Top < Math.Max(bottom, linesTop + 1) && e.Bottom > linesTop))
            {
                var (columnLeft, columnWidth) = (left, width);
                // A paragraph whose box begins with it keeps its top border too above a first line pushed below
                // objects, the box beginning below them (docx/external/libreoffice/core-ooxml-pass/tdf108272: a
                // bordered heading below a header's floating table, its top line at the table's bottom).
                var opening = frame != null && !bare && !continues && frame.Top is { Visible: true } line ? line.Space + line.Total : 0;
                // At a column's top, where the paragraph's space before was dropped, a first line pushed below its own
                // objects has it below them, no longer beginning the column (measured with docx/local/psk-15, psk-14: 12
                // pt below a rectangle wrapped top and bottom at the page's top after a page break before, and after the
                // page before filled; efd-15: wrapped top and bottom after a page break before); below other objects not
                // (docx/external/libreoffice/core-objectpositioning/do-not-capture-draw-objs-on-page-draw-wrap-none: a
                // page's first line below another paragraph's shape at the top margin; writerfilter-dmapper/
                // framepr-pagebreak-style, extras-layout/tdf170846_1, _2: below frames).
                var dropped = SpaceAbove(before, brokeBefore).Dropped && lines.Count > 0
                              && owned.Values.Any(e => e.Top < linesTop + lines[0].Height - 0.01 && e.Bottom > linesTop + 0.01);
                settings = settings with { PushedSpace = (dropped ? spacing.Between : ownBefore) + opening };
                try
                {
                    double? firstOffset = null;
                    var untall = tall.Count > 0 ? exclusions.Where(e => !tall.Contains(e)).ToList() : exclusions;
                    lines = layout.Layout(paragraph, width, settings, room: slot =>
                    {
                        firstOffset ??= slot.Offset;
                        return Room(slot.Offset <= firstOffset.Value + 0.01 ? untall : exclusions, linesTop, slot, columnLeft, columnWidth);
                    });
                    laidAround = true;
                }
                finally
                {
                    settings = settings with { PushedSpace = 0 };
                }
                // So does its shading, drawn line by line, which leaves the room the line skipped white (measured with
                // docx/local/psk-15, psk-14: a yellow paragraph below a rectangle wrapped top and bottom, shaded from
                // its line's top, one going on with the shading of the paragraph before at a page's top too;
                // bwr-15-*-sql-100); a box going on from the paragraph above keeps its part above the line.
                pushedBox = frame != null && !bare && (!continues || columnEmpty) && lines.Count > 0 && lines[0].Skip > 0.01;
                if (pushedBox)
                    boxTop = linesTop + lines[0].Skip - opening;
            }
        }
        LayAround();
        // An object anchored in a line that goes on in another column or page stands with the paragraph's part there,
        // the lines before it not beside it (LibreOffice's extras-layout/shape-anchored-in-follow: a rectangle anchored
        // late in a paragraph that fills three columns stands on the next page, the first column's lines go straight
        // on below the 81 pt it is placed down from the paragraph's top).
        if (laidAround && owned.Count > 0)
        {
            var (top, fit) = (y, 0);
            while (fit < lines.Count && top + lines[fit].Height <= Bottom + 0.01)
                top += lines[fit++].Height;
            var later = lines.Skip(fit).SelectMany(l => l.Anchors ?? []).Where(owned.ContainsKey).ToList();
            if (later.Count > 0)
            {
                foreach (var anchor in later)
                {
                    exclusions.Remove(owned[anchor]);
                    tall.Remove(owned[anchor]);
                }
                lines = layout.Layout(paragraph, width, settings);
                laidAround = false;
                LayAround();
            }
        }
        var firstLineTop = (Page: page!, Top: y);
        var widow = format.WidowControl ?? true;
        // The rest of a paragraph that goes on in a column of another width (w:cols with w:equalWidth 0) breaks its
        // lines anew there (measured with docx/local/uc-7, uc-8: 400 words from a 132 pt column on in a 264 pt one).
        var laidWidth = width;
        // In another column or page the rest goes on beside the floating objects there (measured with docx/external/
        // libreoffice/extras-layout/fdo43573-2-min: the last line of a paragraph begun on the page before goes below an
        // image at the top of the column); true where the lines are laid out again so.
        var laidAt = (Page: page!, Column: column);
        bool Relayout(int k)
        {
            if (k >= lines.Count)
                return false;
            var fresh = !ReferenceEquals(laidAt.Page, page) || laidAt.Column != column;
            laidAt = (page!, column);
            var (columnLeft, columnWidth, linesTop) = (left, width, y);
            var around = fresh
                ? PageExclusions().Where(e => e.Left < columnLeft + columnWidth - 0.01 && e.Right > columnLeft + 0.01 && e.Bottom > linesTop + 0.01).ToList()
                : [];
            // Lines broken beside objects of the page before break anew in a column without them (measured with
            // docx/external/libreoffice/extras-ooxmlexport/tdf154703_framePrWrapSpacing: the lines a frame narrowed at
            // the bottom of page 1 go on across the whole width of page 2).
            if (Math.Abs(width - laidWidth) < 0.01 && around.Count == 0 && !(fresh && laidAround))
                return false;
            laidAround = around.Count > 0;
            lines = [.. lines.Take(k), .. around.Count > 0
                ? layout.Layout(paragraph, width, settings, room: slot => Room(around, linesTop, slot, columnLeft, columnWidth), from: lines[k].TextStart)
                : layout.Layout(paragraph, width, settings, from: lines[k].TextStart)];
            laidWidth = width;
            return around.Count > 0;
        }
        // The lines from line k on with that line broken before the word it would hyphenate, beside the page's objects.
        List<Line> Unhyphenated(int k)
        {
            var (columnLeft, columnWidth, linesTop) = (left, width, y);
            var around = PageExclusions().Where(e => e.Left < columnLeft + columnWidth - 0.01 && e.Right > columnLeft + 0.01 && e.Bottom > linesTop + 0.01).ToList();
            return around.Count > 0
                ? layout.Layout(paragraph, width, settings, room: slot => Room(around, linesTop, slot, columnLeft, columnWidth), from: lines[k].TextStart, unhyphenated: true)
                : layout.Layout(paragraph, width, settings, from: lines[k].TextStart, unhyphenated: true);
        }
        // Lines no page break may come before (Bound) or after (Binds), which form units with their neighbours
        // (keptNotes), on pages of one column: a paragraph's lines where it keeps them together, its second and last
        // under widow control, its first after a paragraph kept with it, its last where it is kept with the next.
        var keepsNext = format.KeepNext == true && index + 1 < blocks.Count && blocks[index + 1] is ParagraphBlock or TableBlock;
        var chainedHere = previous != null ? previous.Format.KeepNext == true : tableKept;
        var units = section.Columns == 1 && footnotes != null;
        bool Bound(int k) => units && (k > 0
            ? lines[k - 1].EndBreak is not ("page" or "column") && (format.KeepLines == true || widow && (k == 1 || k == lines.Count - 1))
            : chainedHere);
        bool Binds(int k) => k + 1 < lines.Count ? Bound(k + 1) : units && keepsNext;
        if (!chainedHere || keptNotes is not { } going || going.Page != page)
            keptNotes = null;
        // Where the unit in hand began in the paragraph (-1: before it), and whether it began its page.
        var unitStart = keptNotes != null ? -1 : 0;
        var unitAtTop = false;
        // Lines of this paragraph in the current column, and whether it began the column there.
        var here = 0;
        var hereAtTop = columnEmpty;
        // What the paragraph's drawings stand from: its first part with how far floating objects pushed its first line
        // down, its part in the column or page being filled, and each anchor's frame (see AnchorFrame).
        (ParagraphPart Part, double Push)? paragraphStart = null;
        ParagraphPart? currentPart = null;
        var anchorFrames = new Dictionary<AnchorInline, AnchorFrame>(ReferenceEqualityComparer.Instance);
        var boxPage = page!;
        // The lines on this page that floating objects cut, for the paragraph's shading (Frame.Draw).
        var cuts = new List<(double Top, double Bottom, List<(double Start, double End)> Segments)>();
        var cutsPage = page!;
        // The box's part in each column or page: from the box's top in the first, the column's top in the others, to
        // the bottom of the part's last line; the parts before the last have no bottom line, those after the first no
        // top line (measured with docx/local/bsp-nat-15, -14, bsp-col-15, bsp-pbr-*, bsp-cbr-*: a paragraph broken by
        // the page's end, a column's or a page or column break, its shading and side lines in both parts).
        var (partTop, partFirst, partPage, partLeft, partWidth) = (boxTop, true, page!, left, width);
        // At least spacing's extra above a first line goes above the box too, its top line the extra lower (measured
        // with docx/local/abx-*: a top border of space 6 over 8.5 pt Verdana at least 12 and 24 pt 1.68 and 13.68 pt
        // lower, its text where it was; exact and auto lines keep it at the top; docx/external/powertools/
        // DB-HeadersFooters-Dest/Fax (content control)).
        if (frame != null && !bare && !continues && format.LineRule == "atLeast" && lines.Count > 0)
            partTop += Math.Max(0, lines[0].Height - lines[0].Above - lines[0].Below - lines[0].RaisedAbove - lines[0].LoweredBelow);
        var partBottoms = new List<double>();
        var boxDone = false;
        void ClosePart()
        {
            if (frame != null && !bare && partBottoms.Count > 0)
            {
                frame.Draw(partPage, partLeft + Mirror(format), partWidth, format, partTop, partBottoms[^1], top: partFirst && !continues, bottom: false,
                    between: partFirst ? betweenLine : null, cuts: cutsPage == partPage ? cuts : null);
                partFirst = false;
            }
            partBottoms.Clear();
        }
        // After a part with no line the box begins in the new column as it would have in the first.
        void OpenPart()
        {
            (partTop, partPage, partLeft, partWidth) = (y, page!, left, width);
            (cuts, cutsPage) = ([], page!);
            if (frame == null || bare || !partFirst)
                return;
            betweenLine = null;
            if (continues)
                partTop = y - spacing.Excess;
            else
            {
                partTop = y + frame.TopShadow;
                y = partTop + (frame.Top is { Visible: true } opening ? opening.Space + opening.Total : 0);
            }
        }
        // The last part, with the bottom line where the box closes; where the next paragraph goes on with the box, it
        // reaches the paragraph's space after (bsp2-gap, bsp2-flow: 8 pt below the last line, past the bottom margin).
        void FinishBox()
        {
            boxDone = true;
            if (frame == null)
                return;
            var reach = nextContinues ? SpaceAfter(paragraph, index + 1 < blocks.Count ? blocks[index + 1] : null) : 0;
            if (bare)
            {
                frame.DrawBare(page!, left + Mirror(format), width, partTop, y + reach);
                return;
            }
            if (!nextContinues)
                y += frame.Bottom is { Visible: true } b ? b.Space + b.Total : 0;
            frame.Draw(page!, left + Mirror(format), width, format, partTop, y + reach, top: partFirst && !continues, bottom: !nextContinues,
                between: partFirst ? betweenLine : null, cuts: cutsPage == page ? cuts : null);
            if (!nextContinues)
                y += frame.BottomShadow;
        }
        // Whether the paragraph went on in another column or page, where the objects that pushed its lines down here
        // are not in their way.
        var moved = false;
        for (var k = 0; k < lines.Count; k++)
        {
            if (Relayout(k))
                moved = false;
            var line = lines[k];
            // A line with footnote references needs room for their notes too; a note too long for the rest of the page
            // continues on the next. A kept line needs room only for the notes before its keep unit.
            if (!Bound(k) && !Binds(k))
                keptNotes = null;
            else if (!Bound(k) || keptNotes is not { } unit || unit.Page != page)
            {
                keptNotes = (page!, notesHeight);
                unitNotes.Clear();
                (unitStart, unitAtTop) = (k, columnEmpty);
            }
            var keptLine = keptNotes != null;
            var notesNeeded = NotesHeight(line.Notes);
            var limit = keptNotes is { } unitBefore ? Math.Min(page!.BodyBottom - unitBefore.Height, bodyEnd ?? double.MaxValue) : Bottom - notesNeeded;
            var split = false;
            // A column's last line breaks anew without its automatic hyphen (LayoutProfile.HyphenBreaksAnew).
            if (line.AutoHyphenated && k + 1 < lines.Count && context.Profile.HyphenBreaksAnew && y + (moved ? 0 : line.Skip) is var lineTop
                && lineTop + line.FitHeight <= limit + 0.01 && lineTop + line.Height + lines[k + 1].FitHeight > limit + 0.01)
            {
                lines = [.. lines.Take(k), .. Unhyphenated(k)];
                line = lines[k];
            }
            // A line that ends with a hyphen does not end a column: it goes on with the line after it (measured with
            // docx/local/hyph-d).
            var fitHeight = line.Hyphenated && k + 1 < lines.Count && context.Profile.HyphenMoves ? line.Height + lines[k + 1].FitHeight : line.FitHeight;
            // Above footnotes a paragraph's last line needs its whole height and the paragraph's space after, from Word
            // 2013 on (measured with docx/local/fbn-*, fbm-*, fbo-*: a line of 24.15 pt with 10 pt after fits where
            // 34.15 pt are left above the notes, not 33 pt, without notes where 16.1 pt are; mode 14 as without;
            // docx/external/libreoffice/extras-ooxmlexport/tdf90810short).
            if (context.Profile.NotesNeedSpaceAfter && k == lines.Count - 1 && (notesHeight > 0.01 || notesNeeded > 0.01))
                fitHeight = Math.Max(fitHeight, line.Height + SpaceAfter(paragraph, index + 1 < blocks.Count ? blocks[index + 1] : null));
            // Before Word 2013 a last line that the paragraph's space after took below a floating table needs room for
            // that space too (measured with docx/local/fas-160-14: a line fitting below the table only without its 8 pt
            // after goes to the next page, fas-0-14 without space after stays; LibreOffice's core-text/
            // floattable-wrap-empty-para-legacy).
            if (raisedAfter > 0 && k == lines.Count - 1 && line.Skip > 0.01)
                fitHeight = Math.Max(fitHeight, line.Height + raisedAfter);
            // A line a floating object pushes down has to fit below it, else it goes to the next column or page, with
            // the paragraph's own objects (measured with docx/local/anchorpush-*: a paragraph whose full-width object
            // leaves it no room on the page moves to the next with it, and the lines above keep their places).
            var skip = moved ? 0 : line.Skip;
            if (!keptLine && y + skip + line.FitHeight > limit + 0.01 && !columnEmpty && notesNeeded > 0 && y + skip + line.FitHeight <= Bottom + 0.01
                && SplitNotes(line.Notes!, Bottom - y - skip - line.FitHeight))
                split = true;
            // A line of nothing but a page or column break stays where it is, past the bottom too: it moves nothing on
            // (measured with docx/external/libreoffice/extras-ooxmlexport/FDO77715: headings that begin with a column
            // break, after a full page, stand at the top of the next page, no empty page between). A line that a
            // floating table on the page pushes past the bottom moves on, though no text stands above it
            // (docx/external/libreoffice/extras-layout/floattable-footer-object-nofit: "After the table." below the
            // last part of a table that fills its page). An empty paragraph that holds the break before a section on a
            // new page stays too, no page of its own (measured with docx/local/sbe-15, sbe-14, sbe-after: after a full
            // page, in modes 14 and 15, also below space after; sbe-text: one with text moves; docx/external/
            // libreoffice/extras-ooxmlexport/section_break_after_table, tdf136589_paraHadField).
            else if (y + skip + fitHeight > limit + 0.01 && (!columnEmpty || skip > 0.01 && page!.FloatingTables.Count > 0) && !BreakOnly(line)
                     && !MarkBeforePage(paragraph) && !MarkLine(paragraph, lines, k))
            {
                // Widow and orphan control: no single first line at the bottom, no single last line at the top
                // (a paragraph that began the column stays, there is no better place for it).
                if (widow && lines.Count > 1)
                {
                    var back = 0;
                    if (k == 1)
                        back = 1;
                    else if (lines.Count - k == 1 && k >= 2)
                        back = k >= 3 ? 1 : k;
                    if (back > 0 && back <= here && !(hereAtTop && back == here))
                    {
                        page!.Lines.RemoveRange(page.Lines.Count - back, back);
                        flow.RemoveAll(f => f.Index >= page.Lines.Count);
                        // The notes of the lines that move go with them.
                        for (var m = k - back; m < k; m++)
                        {
                            foreach (var id in lines[m].Notes ?? [])
                            {
                                pageNotes.Remove(id);
                                if (splitNote?.Id == id)
                                    (splitNote, carried, bodyEnd) = (null, null, null);
                            }
                        }
                        RecountNotes();
                        k -= back;
                        line = lines[k];
                        partBottoms.RemoveRange(Math.Max(0, partBottoms.Count - back), Math.Min(back, partBottoms.Count));
                    }
                }
                ClosePart();
                NextColumn(natural: true);
                (left, width) = Column(column);
                OpenPart();
                bool relaid;
                // A paragraph whose first line its own objects push past the column's end goes on in the next with
                // them: they stand from its top there, the line below them with its space before (measured with
                // docx/local/psk-15, psk-14: a rectangle wrapped top and bottom 40 pt high at the next page's top, the
                // line 12 pt below it).
                if (k == 0 && owned.Count > 0)
                {
                    paragraphTop = (page!, y);
                    firstLineTop = (page!, y);
                    exclusions = PageExclusions();
                    tall.Clear();
                    owned.Clear();
                    Own();
                    lines = layout.Layout(paragraph, width, settings);
                    laidAround = false;
                    LayAround();
                    if (pushedBox)
                        partTop = boxTop;
                    (laidAt, laidWidth, relaid) = ((page!, column), width, laidAround);
                }
                else
                    relaid = Relayout(k);
                line = lines[k];
                here = 0;
                hereAtTop = true;
                moved = !relaid;
                skip = relaid ? line.Skip : 0;
                // The unit goes on below the notes the new page begins with.
                keptNotes = Bound(k) || Binds(k) ? (page!, notesHeight) : null;
                unitNotes.Clear();
                (unitStart, unitAtTop) = (k, true);
            }
            y += skip;
            // Where the first line stands as planned the paragraph begins above it; moved to another column or page,
            // it begins with the line.
            var planned = page == paragraphTop.Page && page == firstLineTop.Page && Math.Abs(y - skip - firstLineTop.Top) < 0.01;
            paragraphStart ??= (new ParagraphPart(page!, planned ? paragraphTop.Top : y, left, width), planned ? skip : 0);
            if (currentPart is not { } part || !ReferenceEquals(part.Page, page) || Math.Abs(part.Left - left) > 0.01)
                currentPart = ReferenceEquals(paragraphStart.Value.Part.Page, page) && Math.Abs(paragraphStart.Value.Part.Left - left) < 0.01
                    ? paragraphStart.Value.Part : new ParagraphPart(page!, y, left, width);
            // A drawing placed from its line stands where the line stood before floating objects pushed it down, as the
            // room around it was measured (docx/external/libreoffice/writerfilter-dmapper/textbox-textline-top,
            // textbox-textline: text boxes wider than the column at their line's top, the text below them).
            foreach (var anchor in line.Anchors ?? [])
                anchorFrames[anchor] = new AnchorFrame(currentPart!.Value, y - skip, left + (line.AnchorX?.GetValueOrDefault(anchor) ?? 0), skip, line.Start,
                    line.Pieces.Count == 0);
            if (page!.Lines.Count == 0 && column == 0 && TopTrim(line, format) is var trim and > 0)
                line = line.Trimmed(trim);
            flow.Add(new FlowLine(page!.Lines.Count, k == 0 ? spacing.Between : 0, k == 0 && here == 0 ? gap : 0, paragraphNumber, k, lines.Count,
                widow, format.KeepNext == true, format.KeepLines == true) { Block = paragraph });
            if (cutsPage != page)
                (cuts, cutsPage) = ([], page!);
            // The room a later line skipped below objects stays unshaded (docx/local/psk-15, psk-14: the third of nine
            // lines below a rectangle wrapped top and bottom, white between the second and it), the first line's too
            // where the box began above it.
            if (skip > 0.01 && (k > 0 || !pushedBox))
                cuts.Add((y - skip, y, []));
            if (line.Cut is { } cut)
                cuts.Add((y, y + line.Height, cut));
            page!.Lines.Add(new PlacedLine(line, left + Mirror(format), y)
            {
                Numbered = format.SuppressLineNumbers != true,
                HangTop = k == 0 && paragraphStart?.Part.Page == page ? Math.Min(y, paragraphStart.Value.Part.Top) : null,
                SectionMark = paragraph.EndsSection != null && !paragraph.Shows,
            });
            if (k == 0)
                page.Paragraphs.Add(paragraph);
            // A chapter heading numbers its page and the pages after it (measured with docx/local/chapter: a page with
            // the end of one chapter and the start of the next has the next's number).
            if (k == 0 && paragraph.Label is { Own.Length: > 0 } chapterLabel && section.ChapterLevel is { } chapterLevel && format.OutlineLevel == chapterLevel - 1)
                page.Chapter = chapter = chapterLabel.Own;
            here++;
            y += line.Height;
            partBottoms.Add(y);
            columnEmpty = false;
            sectionShown = true;
            breakPage = false;
            if (split)
                bodyEnd = y;
            else
            {
                var had = pageNotes.Count;
                AddNotes(line.Notes);
                if (keptNotes != null)
                {
                    unitNotes.AddRange(pageNotes.Skip(had));
                    notesYield |= y - line.Overhang > page!.BodyBottom - notesHeight + 0.01;
                }
            }
            // A unit whose notes do not show below it goes to the next page with them, unless it began the page.
            if (keptNotes is { } ending && !Binds(k) && unitStart >= 0 && !unitAtTop && line.EndBreak is not ("page" or "column")
                && !NotesShow(y - line.Overhang, ending.Height, unitNotes))
            {
                var count = k - unitStart + 1;
                page!.Lines.RemoveRange(page.Lines.Count - count, count);
                flow.RemoveAll(f => f.Index >= page.Lines.Count);
                foreach (var id in unitNotes)
                    pageNotes.Remove(id);
                RecountNotes();
                notesYield = false;
                if (unitStart == 0)
                    page.Paragraphs.Remove(paragraph);
                partBottoms.RemoveRange(Math.Max(0, partBottoms.Count - count), Math.Min(count, partBottoms.Count));
                ClosePart();
                NextColumn(natural: true);
                (left, width) = Column(column);
                OpenPart();
                here = 0;
                hereAtTop = true;
                moved = true;
                keptNotes = null;
                unitNotes.Clear();
                k = unitStart - 1;
                continue;
            }
            // A break that ends the paragraph's last line ends its box there: nothing of it goes on in the next column
            // or page (bsp-end-15, -14, bsp2-other: the box before the page break with the space after "A" in it, the
            // next paragraph at the top of the next page).
            // A page break that ends the document begins no page (measured with docx/local/trail-text-15, -14,
            // trail-only-*: "A" and a page break in the last paragraph, or a last paragraph of only a page break, one
            // page; trail-two-15: of two page breaks after "A" the second begins none; docx/external/libreoffice/
            // extras-ooxmlexport/tdf153964_topMarginAfterBreak14).
            var paged = line.EndBreak is "page" && !BreakUnseen(paragraph) && !(k == lines.Count - 1 && ReferenceEquals(blocks[index], documentEnd));
            var breaks = paged || line.EndBreak is "column";
            if (breaks && k == lines.Count - 1)
                FinishBox();
            else if (breaks)
                ClosePart();
            if (paged)
            {
                NewPage(section);
                breakPage = true;
                top = Top.Break;
                (left, width) = Column(column);
                here = 0;
                hereAtTop = true;
            }
            else if (line.EndBreak is "column")
            {
                NextColumn(natural: false);
                (left, width) = Column(column);
                here = 0;
                hereAtTop = true;
            }
            if (breaks && !boxDone)
                OpenPart();
        }
        if (!boxDone)
            FinishBox();
        for (var d = divisions.Count - 1; d >= 0; d--)
        {
            var (div, divisionFrame) = divisions[d];
            var opens = !previousDivisions.Contains(div.Id);
            var closes = !nextDivisions.Contains(div.Id);
            if (closes)
                y += divisionFrame.Bottom is { Visible: true } db ? db.Space + db.Total : 0;
            var divisionTop = boxPage != page ? page!.BodyTop : divisionTops[d];
            divisionFrame.Draw(page!, left, width, format, divisionTop, y, top: opens || boxPage != page, bottom: closes, between: null);
        }
        foreach (var bookmark in paragraph.Content.OfType<BookmarkInline>())
        {
            var (bookmarkPage, bookmarkTop, bookmarkLeft, _) = paragraphStart?.Part ?? new ParagraphPart(page!, y, left, width);
            bookmarkPage.Bookmarks.Add((bookmark.Name, bookmarkLeft, bookmarkTop));
        }
        // Floating drawings anchored in the paragraph, placed relative to its page, column, paragraph or line.
        foreach (var anchor in paragraph.Content.OfType<AnchorInline>())
        {
            // An object anchored in a part of the paragraph in a later column or page stands relative to that part
            // (measured with docx/external/libreoffice/extras-layout/shape-anchored-in-follow: a rectangle anchored at
            // the end of a paragraph going on in the second column of the next page stands 92 pt right of that column
            // and 81 pt below the paragraph's top there).
            var inLine = anchorFrames.TryGetValue(anchor, out var at);
            var (anchorPage, anchorTop, columnLeft, columnWidth) = inLine ? at.Part : paragraphStart?.Part ?? new ParagraphPart(page!, y, left, width);
            var (lineTop, character) = inLine ? (at.LineTop, at.Character) : (anchorTop, columnLeft);
            // Before Word 2013 a drawing that text does not go around stands from the paragraph's first line and its own
            // line where the floating objects text goes around pushed them: down, and right for the column and the
            // character (measured with docx/local/np-*: in modes 12 and 14 an unwrapped rectangle 28.35 pt from the
            // column and the paragraph, or the character and the line, stands 28.35 pt right of and below its line
            // pushed below one wrapped rectangle and right of another, from the margin where it was; one wrapped square
            // stays where the line was planned, as in mode 15; docx/external/libreoffice/extras-uiwriter/tdf98987). Only
            // the paragraph's own wrapped drawings so move it, and the indent is no push (extras-layout/tdf123651: an
            // unwrapped arrow of an indented paragraph whose lines other floats push stays where it was planned).
            static bool Wrapped(AnchorInline a) => new DrawingBox(a.Anchor).Wraps;
            if (context.Profile.PushedLinesMoveDrawings && inLine && !Wrapped(anchor)
                && paragraph.Content.OfType<AnchorInline>().Any(Wrapped))
            {
                // Down only where one of them covers the paragraph's top, which then moves (docx/local/np2-a, -c: a
                // drawing kept free above and below 8.3 pt under the top leaves the unwrapped one where it was, one from
                // 20 pt above takes it below; np2-b: one wrapped square beside the line takes it right all the same).
                var plannedTop = anchorTop;
                var covered = paragraph.Content.OfType<AnchorInline>().Where(Wrapped)
                    .Select(a => ExclusionOf(PlaceAnchor(a, anchorPage, plannedTop, at.LineTop, columnLeft, columnWidth)))
                    .Any(e => e is { } area && area.Top <= plannedTop + 0.01 && area.Bottom > plannedTop + 0.01);
                if (covered && paragraphStart is { } first && first.Part.Page == anchorPage)
                    anchorTop += first.Push;
                if (covered)
                    lineTop += at.Push;
                var indent = Math.Max(0, (format.Left ?? 0) + (lines.FirstOrDefault()?.First == true ? format.FirstLine ?? 0 : 0));
                if (at.RoomStart is { } start && start > indent + 0.01)
                {
                    if (at.EmptyLine)
                        character = Math.Max(character, columnLeft + start);
                    (columnLeft, columnWidth) = (columnLeft + start - indent, Math.Max(0, columnWidth - start + indent));
                }
            }
            anchorPage.Drawings.Add(PlaceAnchor(anchor, anchorPage, anchorTop, lineTop, columnLeft, columnWidth, character: character));
        }
        pendingAfter = SpaceAfter(paragraph, index + 1 < blocks.Count ? blocks[index + 1] : null);
        pendingOwnAfter = OwnAfter(paragraph);
        if (paragraph.EndsSection != null)
            sectionBreakAfter = pendingAfter;
        // Bar tabs reach down through the space after the paragraph (measured with docx/generated/tabs).
        lines[^1].BarExtension = pendingAfter;
        if (flow.Count > 0 && flow[^1].Paragraph == paragraphNumber)
            flow[^1].After = pendingAfter;
        page!.TrailingSpace = pendingAfter;
        previous = paragraph;
        stylePrevious = paragraph;
        paragraphNumber++;
    }

    /// <summary>
    /// Balances the columns of the current section on this page (catalog: section.columns): the least column height in
    /// whole lines of the section's least line height that holds all its lines, each column filled as far as that
    /// height lets it by the rules of page breaks (widows and orphans, keep with next, keep lines together), the first
    /// column with the space before its first paragraph, a column's last line with its text but not the extra space of
    /// multiple line spacing below it, the space after its paragraph needing no room; the next section begins below the
    /// longest column. Measured with docx/local/cb-*: a line with 60 pt after and ten lines go 3 / 8, ten lines and a
    /// line with 60 pt after 6 / 5, four lines of 24 pt and eight of 12 pt 4 / 8, a line with 60 pt after and 14 lines in
    /// three columns 1 / 7 / 7 as a single first line would be an orphan; docx/local/cbx-*, cbs-*: ten lines of 13.6 pt
    /// below X before go 5 / 5 up to X = 13.6 pt, which counts as shown (less the space after above a new page);
    /// docx/local/cbp-*: four paragraphs of four lines of 13.6 pt with A after, X before the first, move two lines of the
    /// second into the second column where the first column would end below the least multiple of 13.6 pt that holds
    /// the second with them (at X = 27.2, 36.8, 46.4, 42.4, 52.0, 48.0 and 57.6 pt for A = 0 to 24 pt); docx/local/cbq-*:
    /// lines of 15.75 pt at 1.16 lines, their text 2.15 pt less, multiples of 15.75 pt; docx/local/cbr*-*: of 13.6 pt
    /// where one line of 27.2 pt begins or ends the section, where the lines that move or most lines are 20.4 or
    /// 27.2 pt; docx/generated/page-setup: a heading and 60 lines go 31 / 30 where moving two lines would make the
    /// longest column 0.3 pt shorter, not where 4 pt (docx/local/ps-h*); docx/local/colsp-*, cols-balance.
    /// </summary>
    void Balance()
    {
        if (page == null || flow.Count == 0)
            return;
        if (Enumerable.Range(1, Math.Max(0, section.Columns - 1)).Any(c => Math.Abs(Column(c).Width - Column(0).Width) > 0.01) && flow.All(f => f.Block != null))
        {
            BalanceUnequal();
            return;
        }
        var items = flow;
        var heights = items.Select(f => page.Lines[f.Index].Line.Height).ToList();
        var fits = items.Select(f => page.Lines[f.Index].Line.FitHeight).ToList();
        var columns = section.Columns;

        // Where a column may end before item b, moved back by the page break rules; -1 when the column began there.
        int BreakBefore(int b, int columnStart)
        {
            var moved = true;
            while (moved && b > columnStart)
            {
                moved = false;
                var item = items[b];
                if (item.Widows && item.Lines > 1)
                {
                    // A single last line goes with the line before it; a single first line goes with its paragraph.
                    if (item.Line == item.Lines - 1 && item.Line >= 1)
                    {
                        b -= item.Line >= 2 ? 1 : item.Line;
                        moved = true;
                        continue;
                    }
                    if (item.Line == 1)
                    {
                        b -= 1;
                        moved = true;
                        continue;
                    }
                }
                if (item.KeepLines && item.Line > 0)
                {
                    b -= item.Line;
                    moved = true;
                    continue;
                }
                // The paragraph before keeps with this one.
                if (item.Line == 0 && b > 0 && items[b - 1].KeepNext)
                {
                    b -= items[b - 1].Line + 1;
                    moved = true;
                }
            }
            return b > columnStart ? b : -1;
        }

        // The columns filled up to a height, or null where they do not hold all lines.
        List<(int Column, double Top)>? Place(double limit)
        {
            var placement = new List<(int Column, double Top)>();
            var c = 0;
            var used = 0.0;
            var columnStart = 0;
            var bottom = Math.Min(limit, Bottom - columnTop);
            for (var i = 0; i < items.Count; i++)
            {
                var g = i == 0 ? items[0].FirstGap : used > 0 ? items[i].Gap : 0;
                // Word tells heights 0.006 pt apart (docx/local/cbx-272: 13.6 pt and five lines of 13.594 pt exceed six).
                if (used > 0 && used + g + fits[i] > bottom + 0.001)
                {
                    var b = BreakBefore(i, columnStart);
                    // Where the rules of page breaks cannot hold in the column, a greater height is tried (measured
                    // with docx/local/kcs-3-4: the last two lines of a paragraph stand together in the first of three
                    // columns, not one in each); the page's height breaks them as it must, and before Word 2013 so does
                    // any height where the section's columns begin at the page's top (see BalanceBreaksAtPageTop).
                    if (b < 0 && bottom < Bottom - columnTop - 0.001 && !BalanceBreaksHere())
                        return null;
                    if (b < 0)
                        b = i;
                    c++;
                    if (c >= columns)
                        return null;
                    placement.RemoveRange(b, placement.Count - b);
                    i = b;
                    columnStart = b;
                    used = 0;
                    g = 0;
                }
                placement.Add((c, used + g));
                used += g + heights[i];
            }
            return placement;
        }

        // Whole lines of the least line height from a height no balance goes below, up to the least that holds all
        // lines or the page's.
        var unit = heights.Min();
        List<(int Column, double Top)>? result = null;
        if (unit > 0.01)
        {
            var room = Bottom - columnTop;
            for (var k = Math.Max(1, (int)Math.Floor(Math.Max(fits.Max(), fits.Sum() / columns) / unit)); result == null && (k - 1) * unit <= room; k++)
                result = Place(k * unit);
        }
        if (result == null)
            return;
        // Before Word 2013 the space after the paragraph that ends the section is shared out among the columns, where the
        // paragraph has text (docx/external/libreoffice/extras-ooxmlimport/tdf103931: an empty one alone in its section
        // keeps it whole).
        var shared = context.Profile.SharesSectionEndSpace && items[^1].Block is { EndsSection: not null } && !page.Lines[items[^1].Index].SectionMark;
        var bottom = columnTop;
        for (var i = 0; i < items.Count; i++)
        {
            var (c, offset) = result[i];
            var old = page.Lines[items[i].Index];
            page.Lines[items[i].Index] = old with { Left = Column(c).Left, Top = columnTop + offset };
            // A column ends below its last line and the space after its paragraph (measured with page-setup: the
            // next section's heading stands its space after and its own space before below the longest column).
            var columnEnds = i + 1 == items.Count || result[i + 1].Column != c;
            bottom = Math.Max(bottom, columnTop + offset + old.Line.Height + (columnEnds && !(shared && i + 1 == items.Count) ? items[i].After : 0));
        }
        // Before Word 2013 the next section begins below the longest column, where the paragraph that ends the section
        // ends it without its space after, or, where that is lower, a column's share of all the section's lines and
        // spaces and that space after (measured with docx/local/cbl-12,
        // cbl-14: 15 lines of 15.44 pt in three columns end 77.2, 80.53 and 87.2 pt down with 0, 10 and 30 pt after, 18
        // lines 95.97 with 10 pt, 16 lines at their longest column of six whatever the space after; cbl-15 below the
        // longest column and the space after; docx/external/libreoffice/extras-ooxmlexport/tdf121670_columnsInSectionsOnly).
        if (shared)
        {
            var total = items[0].FirstGap + heights.Sum() + items.Skip(1).Sum(f => f.Gap) + items[^1].After;
            bottom = Math.Max(bottom, columnTop + total / columns);
        }
        y = bottom;
        column = 0;
        balanced = true;
    }

    /// <summary>
    /// Whether balancing breaks a paragraph where it must, at any height, where the rules of page breaks find no place
    /// for a column's end, as they do at the top of a page: before Word 2013 where the section's columns begin at the
    /// page's top (measured with docx/local/hbs-*, mode 12: a heading or text of two lines in a section of two
    /// columns holding nothing else, at the page's top, has one line in each column, widow control on or off, the
    /// heading kept with the next or not, the columns equally wide or not; below other text, hbs-below, and in mode 15,
    /// hbs-m15, both stay in the first column; a paragraph of four lines keeps its last two together, hbs-four; one of
    /// two whose rest would take two lines in the narrow column stays whole, hbs-three; openxml-sdk/complex0: the
    /// heading "Format Columns: Line Between" with "Between" at the top of the narrow column of page 3).
    /// </summary>
    bool BalanceBreaksHere() => context.Profile.BalanceBreaksAtPageTop && columnTop <= page!.BodyTop + 0.01;

    /// <summary>
    /// Balances columns of different widths (w:cols with w:equalWidth 0) as Balance does, a paragraph that goes on in
    /// another column breaking its rest anew at that column's width (measured with docx/local/uc-1 to uc-6: 90 words
    /// in columns of 132 and 264 pt, either first; docx/external/openxml-sdk/complex0).
    /// </summary>
    void BalanceUnequal()
    {
        var columns = section.Columns;
        // The section's paragraphs on the page, each from where its first line here begins.
        var groups = new List<(ParagraphBlock Block, int From, double Gap, double After, int Before, bool Widows, bool KeepNext, bool KeepLines)>();
        for (var i = 0; i < flow.Count; i++)
        {
            var f = flow[i];
            if (i == 0 || f.Paragraph != flow[i - 1].Paragraph)
                groups.Add((f.Block!, page!.Lines[f.Index].Line.TextStart, f.Gap, 0, f.Line, f.Widows, f.KeepNext, f.KeepLines));
            if (i + 1 == flow.Count || flow[i + 1].Paragraph != f.Paragraph)
                groups[^1] = groups[^1] with { After = f.After };
        }
        var firstGap = flow[0].FirstGap;

        // Where a column must end before the line after the placed ones, by the rules of page breaks (as in Balance);
        // -1 where the column began there.
        int BreakBefore(List<(int Group, Line Line, int Column, double Top, int Number, int Count)> placed, (int Group, int Number, int Count) next, int columnStart)
        {
            (int Group, int Number, int Count) At(int i) => i < placed.Count ? (placed[i].Group, placed[i].Number, placed[i].Count) : next;
            var b = placed.Count;
            var moved = true;
            while (moved && b > columnStart)
            {
                moved = false;
                var (group, number, count) = At(b);
                if (groups[group].Widows && count > 1)
                {
                    if (number == count - 1 && number >= 1)
                    {
                        b -= number >= 2 ? 1 : number;
                        moved = true;
                        continue;
                    }
                    if (number == 1)
                    {
                        b -= 1;
                        moved = true;
                        continue;
                    }
                }
                if (groups[group].KeepLines && number > 0)
                {
                    b -= number;
                    moved = true;
                    continue;
                }
                if (number == 0 && b > 0 && groups[At(b - 1).Group].KeepNext)
                {
                    b -= At(b - 1).Number + 1;
                    moved = true;
                }
            }
            return b > columnStart ? b : -1;
        }

        // The columns filled up to a height, each paragraph's rest laid out anew in the column it goes on in; null
        // where they do not hold it all.
        List<(int Group, Line Line, int Column, double Top, int Number, int Count)>? Place(double limit)
        {
            var placed = new List<(int Group, Line Line, int Column, double Top, int Number, int Count)>();
            var (c, used, columnStart) = (0, 0.0, 0);
            var bottom = Math.Min(limit, Bottom - columnTop);
            var (g, from) = (0, groups[0].From);
            for (var steps = 0; g < groups.Count; steps++)
            {
                if (steps > 10000)
                    return null;
                var group = groups[g];
                var lines = layout.Layout(group.Block, Column(c).Width, settings, from: from);
                var number = group.Before + placed.Count(p => p.Group == g);
                var count = number + lines.Count;
                var broke = false;
                for (var j = 0; j < lines.Count; j++)
                {
                    var line = lines[j];
                    var gap = placed.Count == 0 ? firstGap : j == 0 && from == group.From && used > 0 ? group.Gap : 0;
                    if (used > 0 && used + gap + line.FitHeight > bottom + 0.001)
                    {
                        var b = BreakBefore(placed, (g, number + j, count), columnStart);
                        if (b < 0 && bottom < Bottom - columnTop - 0.001 && !BalanceBreaksHere())
                            return null;
                        if (b < 0)
                            b = placed.Count;
                        if (++c >= columns)
                            return null;
                        if (b < placed.Count)
                        {
                            (g, from) = (placed[b].Group, placed[b].Line.TextStart);
                            placed.RemoveRange(b, placed.Count - b);
                        }
                        else
                            from = line.TextStart;
                        columnStart = placed.Count;
                        used = 0;
                        broke = true;
                        break;
                    }
                    placed.Add((g, line, c, used + gap, number + j, count));
                    used += gap + line.Height;
                }
                if (broke)
                    continue;
                if (++g < groups.Count)
                    from = groups[g].From;
            }
            return placed;
        }

        // Whole lines of the least line height from a height no balance goes below (the lines laid out in the widest
        // column, shared out) up to the least that holds all lines or the page's.
        var unit = flow.Min(f => page!.Lines[f.Index].Line.Height);
        if (unit <= 0.01)
            return;
        var widest = Enumerable.Range(0, columns).Max(c => Column(c).Width);
        var least = groups.Sum(group => layout.Layout(group.Block, widest, settings, from: group.From).Sum(l => l.Height)) / columns;
        var room = Bottom - columnTop;
        List<(int Group, Line Line, int Column, double Top, int Number, int Count)>? result = null;
        for (var k = Math.Max(1, (int)Math.Floor(Math.Max(least, flow.Max(f => page!.Lines[f.Index].Line.FitHeight)) / unit)); result == null && (k - 1) * unit <= room; k++)
            result = Place(k * unit);
        if (result == null)
            return;
        // The balanced lines take the place of the section's lines on the page.
        var indices = flow.Select(f => f.Index).ToHashSet();
        var at = flow[0].Index;
        var others = page!.Lines.Where((_, i) => !indices.Contains(i) && i >= at).ToList();
        page.Lines.RemoveRange(at, page.Lines.Count - at);
        var bottomEdge = columnTop;
        var rebuilt = new List<FlowLine>();
        for (var i = 0; i < result.Count; i++)
        {
            var (group, line, c, top, number, count) = result[i];
            var block = groups[group].Block;
            var last = number == count - 1;
            rebuilt.Add(new FlowLine(page.Lines.Count, 0, 0, group, number, count, groups[group].Widows, groups[group].KeepNext, groups[group].KeepLines)
            {
                Block = block,
                After = last ? groups[group].After : 0,
            });
            page.Lines.Add(new PlacedLine(line, Column(c).Left + Mirror(block.Format), columnTop + top)
            {
                Numbered = block.Format.SuppressLineNumbers != true,
                HangTop = number == 0 ? columnTop + top : null,
            });
            var columnEnds = i + 1 == result.Count || result[i + 1].Column != c;
            bottomEdge = Math.Max(bottomEdge, columnTop + top + line.Height + (columnEnds && last ? groups[group].After : 0));
        }
        page.Lines.AddRange(others);
        flow.Clear();
        flow.AddRange(rebuilt);
        y = bottomEdge;
        column = 0;
        balanced = true;
    }

    /// <summary>
    /// The lines between the columns of the current section on this page (w:cols/@sep): in the middle of each space
    /// between columns, from the columns' top to the bottom of the longest column (measured with page-setup, page 5).
    /// </summary>
    void CloseColumns()
    {
        if (page == null || flow.Count == 0 || !section.ColumnSeparator || section.Columns < 2)
            return;
        var bottom = flow.Max(f => page.Lines[f.Index].Top + page.Lines[f.Index].Line.Height);
        for (var c = 0; c + 1 < section.Columns; c++)
        {
            var (left, width) = Column(c);
            var next = Column(c + 1).Left;
            page.ColumnLines.Add(((left + width + next) / 2, columnTop, bottom));
        }
        flow.Clear();
    }

    /// <summary>The height a paragraph needs at least: its first two lines with widow control, else its first.</summary>
    /// <summary>
    /// The height the first lines of a paragraph need together: two with widow control, one without; a first line
    /// that ends in a page or column break stands alone, the lines after it go on in the next column anyway (measured
    /// with docx/external/libreoffice/extras-ooxmlexport/FDO77715: headings that begin with a column break near the
    /// bottom of a page leave no empty page).
    /// </summary>
    static double FirstLines(List<Line> lines, ParagraphFormat format) => Needed(lines.Take(FirstLineCount(lines, format)).ToList());

    /// <summary>How many first lines of a paragraph go together: two under widow control, else one.</summary>
    static int FirstLineCount(List<Line> lines, ParagraphFormat format) =>
        (format.WidowControl ?? true) && lines.Count > 1 && lines[0].EndBreak is not ("page" or "column") ? 2 : 1;

    /// <summary>The height lines need on a page: all of them, the last without what may hang past the bottom.</summary>
    static double Needed(List<Line> lines) => lines.Count == 0 ? 0 : lines.Sum(l => l.Height) - lines[^1].Overhang;

    /// <summary>
    /// Whether the paragraph at <paramref name="index"/>, kept with the next one and beginning at <paramref name="top"/>
    /// below other content of the column, moves to the next column with its keep chain: laid on from here, the chain's
    /// paragraphs (those kept with the next one and the first one that is not) reach one that does not fit and cannot
    /// break here (a line or two, too few lines for widow control, lines kept together) and must begin the next column;
    /// the paragraphs kept with it go along back to this one unless one of them can break and give its last lines (as
    /// Word pushes paragraphs back, measured with docx/local/kc2 … kc12: chains taller than a column move too, a
    /// paragraph that can break keeps its start here). Lays out no more than a column of paragraphs, so long chains
    /// (docx/external/powertools/LIR*: 3500 list items kept with each other) cost little. A chain that begins inside
    /// the table at <paramref name="index"/> begins at its row <paramref name="fromRow"/>.
    /// </summary>
    bool ChainBreaks(List<Block> blocks, int index, List<Line>? lines, double width, double top, int fromRow = 0)
    {
        var y = top;
        // Whether a paragraph of the chain placed so far could break and give its last lines to the next column.
        var gives = false;
        // The notes the chain refers to, which must show below a chain that cannot break (NotesShow).
        var notes = new List<string>();
        for (var k = index; k < blocks.Count; k++)
        {
            if (blocks[k] is TableBlock table)
            {
                if (table.Table.Format.Floating != null)
                    return false;
                if (k > index && blocks[k - 1] is ParagraphBlock before)
                    y += SpaceAfter(before, table);
                // Rows kept with the next one do not break for a chain: each must fit, and carries the chain on,
                // through the table to the block after it where all are; the first row that is not kept needs only to
                // begin here, all of it where it may not split (docx/local/tk10, tk11, tk13: a heading goes along with
                // a first row that does not fit below it; tkr-a, tkr-e, tkr-g, tkr-h: a kept row stays above the start
                // of a row that splits, goes on with one that may not split or is kept itself).
                var box = LaidTable(table, width, k > index ? blocks[k - 1] as ParagraphBlock ?? LastParagraph(blocks, k - 1) : stylePrevious, keep: true);
                for (var r = k == index ? fromRow : 0; r < box.Rows.Count; r++)
                {
                    var row = box.Rows[r];
                    var kept = RowKept(table.Table, r);
                    if (y + row.Height + row.BottomBorderWidth > Bottom + 0.01)
                        return kept || Bottom - y <= row.TopWidth + 1 || row.Split(Bottom - y - row.BottomBorderWidth) == null ? !gives : false;
                    y += row.Height;
                    if (!kept)
                        return false;
                }
                continue;
            }
            if (blocks[k] is not ParagraphBlock paragraph)
                return false;
            if (k > index)
            {
                if (paragraph.Format.PageBreakBefore == true)
                    return false;
                // The chain ends at an empty paragraph that holds a continuous section break and takes no room
                // (measured with docx/local/keepsect-k1: a heading kept with it stays at the bottom of the page, the
                // next section's text goes to the next one; with "x" in it or without the break the heading goes too).
                if (TakesNoRoom(paragraph, k))
                    return !gives && section.Columns == 1 && !NotesShow(y, notesHeight, notes);
                y += blocks[k - 1] switch
                {
                    ParagraphBlock before => Between(before, paragraph),
                    TableBlock before => context.Between(0, SpaceBefore(paragraph, LastParagraph(before.Table), false, afterTable: true), 0),
                    _ => 0,
                };
            }
            var own = k == index && lines != null ? lines : KeptLines(paragraph, width);
            // A paragraph that ends with a page or column break ends the chain: what follows begins anew anyway
            // (docx/external/libreoffice/extras-ooxmlexport/testPageref: a page break kept with the next paragraph,
            // through its style, made an empty page).
            var last = paragraph.Format.KeepNext != true || k + 1 >= blocks.Count || blocks[k + 1] is not (ParagraphBlock or TableBlock)
                       || own.Count > 0 && own[^1].EndBreak is "page" or "column";
            var room = Bottom - y;
            // The last paragraph needs its first lines here, all of them where it keeps them together.
            var needed = last && paragraph.Format.KeepLines != true ? FirstLines(own, paragraph.Format) : Needed(own);
            if (needed <= room + 0.01)
            {
                var counted = last && paragraph.Format.KeepLines != true ? own.Take(FirstLineCount(own, paragraph.Format)) : own;
                foreach (var id in counted.SelectMany(l => l.Notes ?? []))
                {
                    if (footnotes != null && footnotes.ContainsKey(id) && !pageNotes.Contains(id) && !notes.Contains(id))
                        notes.Add(id);
                }
                if (last)
                    return !gives && section.Columns == 1 && !NotesShow(y + needed, notesHeight, notes);
                gives |= Breaks(own, paragraph.Format, double.MaxValue);
                y += own.Sum(l => l.Height);
                continue;
            }
            if (!last && Breaks(own, paragraph.Format, room))
                return false;
            return !gives;
        }
        return false;
    }

    /// <summary>
    /// Whether a table's row (of those that show) is kept with the next row or block: its first cell's first
    /// paragraph is kept with the next one (measured with docx/local/tk4, tk6, tk8, tk9: the other cells and
    /// paragraphs do not count).
    /// </summary>
    static bool RowKept(TableModel table, int row) =>
        table.Rows.Where(r => !r.Format.Hidden).ElementAtOrDefault(row)?.Cells.FirstOrDefault()?.Blocks.FirstOrDefault() is ParagraphBlock { Format.KeepNext: true };

    /// <summary>Whether the last table placed ends with a row kept with the next block.</summary>
    bool tableKept;

    /// <summary>The last paragraph of the block before a position, if it is a table.</summary>
    static ParagraphBlock? LastParagraph(List<Block> blocks, int index) =>
        index >= 0 && blocks[index] is TableBlock table ? LastParagraph(table.Table) : null;

    /// <summary>
    /// A table laid out at a width below a paragraph (whose style counts for its spacing); one the keep chain
    /// look-ahead laid out (<paramref name="keep"/>) is kept for the placement, which takes it.
    /// </summary>
    TableBox LaidTable(TableBlock table, double width, ParagraphBlock? prior, bool keep, IReadOnlyList<(double From, double To, BorderLine? Line)>? joined = null)
    {
        var before = prior != null ? context.Styles.ParagraphStyleOf(prior.Format.Style) : TableLayout.NoParagraph;
        if (laidTables.Remove(table, out var laid) && joined == null && laid.Width == width && laid.Before == before)
        {
            if (keep)
                laidTables[table] = laid;
            return laid.Box;
        }
        var box = tables.Layout(table.Table, width, settings, before: before, joined: joined);
        if (keep && joined == null)
            laidTables[table] = (width, before, box);
        return box;
    }

    /// <summary>The last table of the flow placed and its layout, which a table right after it continues.</summary>
    (TableBlock Block, TableBox Box)? previousTable;

    /// <summary>Tables the keep chain look-ahead laid out, with the width and the paragraph style before them.</summary>
    readonly Dictionary<TableBlock, (double Width, string? Before, TableBox Box)> laidTables = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Whether a paragraph's lines can break between columns with <paramref name="room"/> points here: not kept
    /// together, and as many lines as widow and orphan control leave at least on each side (two, else one).
    /// </summary>
    static bool Breaks(List<Line> lines, ParagraphFormat format, double room)
    {
        if (format.KeepLines == true)
            return false;
        var least = format.WidowControl ?? true ? 2 : 1;
        if (lines.Count < 2 * least)
            return false;
        var (fit, height) = (0, 0.0);
        foreach (var line in lines)
        {
            if (height + line.FitHeight > room + 0.01)
                break;
            height += line.Height;
            fit++;
        }
        return fit >= least;
    }

    /// <summary>
    /// The page or column break a paragraph begins with (after marks that take no room) where the rest of the
    /// paragraph goes on in the next page or column: always after a column break, also one that ends the page, whose
    /// paragraph mark goes along and takes a line there with floating objects anchored to it; after a page break
    /// where text or an inline drawing follows, or where w:splitPgBreakAndParaMark takes the mark along, which Word
    /// 2013 and later ignore (compatibility mode 15); null otherwise (measured with docx/local/breakmark-* in modes 11,
    /// 14 and 15, breakmark4-*; docx/external/libreoffice/extras-ooxmlexport/tdf153613_anchoredAfterPgBreak, -2, -3: a picture
    /// anchored after a page break stays on the page before unless text follows or the option is on;
    /// tdf153592_columnBreaks). The start of a content control is a mark that does take its place: the break then
    /// ends the paragraph's first line like any other (ControlStartsBefore).
    /// </summary>
    /// <summary>
    /// Whether a page break in the paragraph goes unseen: before Word 2013 in the paragraph that ends a section where
    /// the next section goes on on the same page (measured with docx/local/pb14*, pb12*: "A", a page break holding a
    /// continuous section break and "B" stand on one page, in mode 15 on two; of a page break holding one and a page
    /// break after it only the second begins a page; docx/external/libreoffice/extras-uiwriter/tdf131963), unless
    /// w:splitPgBreakAndParaMark moves the paragraph's mark and its section break after the page break (docx/local/
    /// pbsp14*, pbsp12*; extras-ooxmlexport/tdf97648_relativeWidth).
    /// </summary>
    bool BreakUnseen(ParagraphBlock paragraph) =>
        context.Profile.ContinuousSectionsHidePageBreaks && paragraph.EndsSection != null && nextContinuous;

    BreakInline? LeadingBreak(ParagraphBlock paragraph)
    {
        var k = 0;
        while (k < paragraph.Content.Count && paragraph.Content[k] is BookmarkInline or BookmarkEndInline or PermissionInline or CommentInline
               or TextInline { Text.Length: 0 })
            k++;
        if (k >= paragraph.Content.Count || paragraph.Content[k] is not BreakInline { Type: "page" or "column" } leading)
            return null;
        if (ControlStartsBefore(paragraph.Element))
            return null;
        if (leading.Type == "column")
            return leading;
        var rest = paragraph.Content.Skip(k + 1).ToList();
        if (rest.Any(i => i is TextInline { Text.Length: > 0 } or DrawingInline))
            return leading;
        return context.Profile.SplitPageBreak ? leading : null;
    }

    /// <summary>
    /// Whether a content control starts in a paragraph before its first page or column break: a block control whose
    /// first paragraph it is, or a control in the paragraph around or before the break (measured with docx/local/
    /// leadbreak-j, -m, -n, -o, -p, -q: there the space before and the number stay with the break on the page
    /// before, the text goes on at the top of the next page; in a control's second paragraph they go with the text,
    /// as docx/external/libreoffice/extras-ooxmlexport/tdf154478's TOC heading shows).
    /// </summary>
    static bool ControlStartsBefore(XElement element)
    {
        var w = Docx.Wml.W;
        for (var a = element.Parent; a != null && a.Name != w + "body" && a.Name != w + "tc" && a.Name != w + "txbxContent"; a = a.Parent)
            if (a.Name == w + "sdtContent" && a.Descendants(w + "p").FirstOrDefault() == element)
                return true;
        var first = element.Descendants(w + "br").FirstOrDefault(b => b.Attr("type") is "page" or "column");
        if (first == null)
            return false;
        return first.Ancestors(w + "sdt").Any(c => c.Ancestors().Contains(element))
               || element.Descendants(w + "sdt").Any(c => c.IsBefore(first));
    }

    /// <summary>Whether a paragraph's first line ends with a page or column break before anything that takes room.</summary>
    static bool StartsWithBreak(ParagraphBlock paragraph)
    {
        var first = paragraph.Content.FirstOrDefault(i => i is not (BookmarkInline or BookmarkEndInline or PermissionInline or CommentInline
            or TextInline { Text.Length: 0 } or AnchorInline));
        return first is BreakInline { Type: "page" or "column" } && !ControlStartsBefore(paragraph.Element);
    }

    /// <summary>Whether a line holds nothing but a page or column break (and marks that take no room).</summary>
    static bool BreakOnly(Line line) =>
        line.EndBreak is "page" or "column" && line.Pieces.All(p => p.Drawing == null && !p.IsLabel && string.IsNullOrWhiteSpace(p.Text));

    /// <summary>The lines of a paragraph in a keep chain, laid out once for the width they were laid out at.</summary>
    List<Line> KeptLines(ParagraphBlock paragraph, double width)
    {
        if (!keptLines.TryGetValue(paragraph, out var kept) || kept.Width != width)
            keptLines[paragraph] = kept = (width, layout.Layout(paragraph, width, settings));
        return kept.Lines;
    }

    /// <summary>The lines of paragraphs in keep chains and the width they were laid out at.</summary>
    readonly Dictionary<ParagraphBlock, (double Width, List<Line> Lines)> keptLines = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// How much less room the first line on a page has above its text (catalog: settings.compatibility): where the
    /// document says w:suppressTopSpacing, an exact line has its baseline 9.6 pt below its top and an at-least line
    /// loses its height less 9.6 pt, whatever its text (measured with docx/local/c5-top and c6-top in modes 15 and
    /// 12: Arial from 8 to 40 pt, Times New Roman and Calibri at 15 to 40 pt high lines, the text of large fonts above
    /// the page's margin then); before Word 2013, where it says w:suppressSpacingAtTopOfPage, an at-least line or one
    /// on the document grid has its natural ascent above the baseline (measured with docx/local/c4-misc12, c5-top12
    /// and c6-top12).
    /// </summary>
    double TopTrim(Line line, ParagraphFormat format)
    {
        var rule = format.LineRule ?? "auto";
        if (context.Profile.SuppressTopSpacing && rule is "exact")
            return line.Baseline - 9.6;
        if (context.Profile.SuppressTopSpacing && rule is "atLeast")
            return (format.Line ?? 240) / 20 - 9.6;
        if (context.Profile.SuppressSpacingAtTop && (rule is "atLeast" || rule is "auto" && settings.LinePitch != null && format.SnapToGrid != false))
            return line.Baseline - line.Above - line.RaisedAbove;
        return 0;
    }

    /// <summary>
    /// The space above a paragraph, in points (catalog: paragraph.spacing): Between, all of the space between it and the
    /// paragraph above (see <see cref="Gap"/>), which it has where it goes on below that one, also in balanced columns;
    /// Excess, what its space before has more than the space after above; Here, the space above it where it begins (see
    /// <see cref="SpaceAbove"/>); Dropped, whether a break at the column's top dropped its space before.
    /// </summary>
    readonly record struct Spacing(double Between, double Excess, double Here, bool Dropped);

    /// <summary>
    /// The space above the paragraph being placed, with <paramref name="before"/> its own space before: within a
    /// column all of the space between it and the paragraph above, at a column's top by how the column began (see
    /// <see cref="Top"/>; <paramref name="brokeBefore"/>: whether its own page break before began the page).
    /// </summary>
    Spacing SpaceAbove(double before, bool brokeBefore)
    {
        var between = Gap(before);
        var excess = Math.Max(0, between - pendingAfter);
        if (!columnEmpty)
            return new(between, excess, between, false);
        return top switch
        {
            Top.Document => new(between, excess, before, false),
            Top.Section => new(between, excess, excess, false),
            // A continuous section begins below the space after the last paragraph of text above (balanced columns
            // hold it in their height, before Word 2013 sharing it out where its paragraph holds the break, see
            // Balance); its first paragraph adds what its space before has more than the space after of the
            // paragraph that holds the section break, the empty one holding it alone or that one of text, from the
            // longest column whatever column that paragraph ends in (measured with docx/local/hbt-*: one-line
            // paragraphs of 3, 0 and 12 pt after in two balanced columns or one column, an empty paragraph of no space
            // after holding the break, then one of 12, 12 and 3 pt before beginning a section of one, two or three
            // columns, 15, 12 and 15 pt apart, in modes 12 and 15, 16 pt for 8 and 8; hbt-ba, -bb: 0 and 3 pt after,
            // the empty paragraph 12 and 6 pt after, 6 and 4 pt before, 0 and 3 pt apart; hbt-bt: the break in the
            // paragraph of 3 pt after, 12 pt before, 12 pt apart; colsp-e: 12, 12 and 6, 12 apart; hbu-*: the last
            // paragraph above in the shorter of two columns, 12 and 30 pt before in full below the longer, in modes 14
            // and 15; docx/generated/page-setup: a heading of 8 pt before at the top of three columns, the break in
            // the paragraph of 8 pt after above; openxml-sdk/complex0: Heading3 15 pt below the heading balanced above
            // it; LibreOffice's extras-ooxmlexport/tdf90697_complexBreaksHeaders: an empty line of 3.1 pt before below
            // columns ended by column breaks).
            Top.Continuous => new(between, excess, context.Profile.AddsSpacing ? before : Math.Max(0, before - sectionBreakAfter), false),
            // After a break that begins the paragraph, what its space before has more than the space after the
            // paragraph before it (measured with docx/external/libreoffice/extras-ooxmlexport/
            // tdf153964_topMarginAfterBreak15: 60 pt before after an empty paragraph with 8 pt after leave 52 pt;
            // docx/local/breakmark2-*: all of it after paragraphs without space after).
            Top.Leading => new(between, excess, excess, false),
            // Before Word 2013 a page break before keeps what the space before has more than the space after the
            // paragraph before it (measured with docx/local/sptm-11, -12, -14: 18 pt after none; sbk-*: 14 pt, also
            // beforeAutospacing, after 0, 6 and 20 pt keep 14, 8 and 0 in modes 11, 12 and 14; -15 none;
            // python-docx/sty-known-styles; LibreOffice's extras-ooxmlexport/fdo81492).
            Top.Break when brokeBefore && context.Profile.PageBreakKeepsSpaceBefore => new(between, excess, excess, false),
            // Otherwise a break drops it at the column's top.
            _ => new(between, excess, 0, true),
        };
    }

    /// <summary>The space between the previous paragraph and one with the given space before (see <see cref="DocumentContext.Between"/>).</summary>
    double Gap(double before) => context.Between(pendingAfter, before, pendingOwnAfter);

    /// <summary>The space between two paragraphs that follow each other (see <see cref="DocumentContext.Between"/>).</summary>
    double Between(ParagraphBlock first, ParagraphBlock second) =>
        context.Between(SpaceAfter(first, second), SpaceBefore(second, first, false), OwnAfter(first));

    /// <summary>The space before the paragraph being placed, after the previous one.</summary>
    double SpaceBefore(ParagraphBlock paragraph) => SpaceBefore(paragraph, stylePrevious, paragraphNumber == 0, afterTable: previous == null && stylePrevious != null);

    /// <summary>The last paragraph of a table: its last cell's, in a nested table the nested table's.</summary>
    static ParagraphBlock? LastParagraph(TableModel table) =>
        table.Rows.LastOrDefault()?.Cells.LastOrDefault()?.Blocks.LastOrDefault() switch
        {
            ParagraphBlock paragraph => paragraph,
            TableBlock nested => LastParagraph(nested.Table),
            _ => null,
        };

    /// <summary>The space before a paragraph; contextual spacing drops it after a paragraph of the same style.</summary>
    /// <param name="prior">The paragraph before it, if one.</param>
    /// <param name="first">Whether it begins the document.</param>
    /// <param name="afterTable">Whether a table stands before it, <paramref name="prior"/> its last paragraph.</param>
    double SpaceBefore(ParagraphBlock paragraph, ParagraphBlock? prior, bool first, bool afterTable = false)
    {
        if (paragraph.Format.ContextualSpacing == true && prior != null && SameStyle(prior, paragraph))
            return 0;
        // HTML auto spacing (w:beforeAutospacing) is 14 pt, none before the document's first paragraph (measured with
        // docx/local/html-divs, altchunk and c2-html, 14 pt after a table too); where the document says
        // w:doNotUseHTMLParagraphAutoSpacing, the paragraph's own space before (measured with docx/local/c2-html). An
        // item after an item of its list has none, not after a table that ends with one (Word's PDF of
        // docx/external/libreoffice/extras-ooxmlexport/tdf132807: the item after a table 14 pt below it).
        if (paragraph.Format.BeforeAuto == true && !context.Profile.AddsSpacing)
            return first || !afterTable && prior != null && TableLayout.ListPair(prior, paragraph) ? 0 : 14;
        return paragraph.Format.Before ?? 0;
    }

    /// <summary>The bordered HTML divisions around a paragraph, the outermost first, with their frames at their indents.</summary>
    List<(HtmlDiv Div, Frame Frame)> Divisions(string? id)
    {
        var result = new List<(HtmlDiv, Frame)>();
        var (left, right) = (0.0, 0.0);
        foreach (var div in context.Divs.Chain(id))
        {
            (left, right) = (left + div.Left, right + div.Right);
            if (div.Bordered)
                result.Add((div, Frame.Division(div, left, right)));
        }
        return result;
    }

    /// <summary>The space after a paragraph; contextual spacing drops it before a paragraph of the same style.</summary>
    double SpaceAfter(ParagraphBlock paragraph, ParagraphBlock? next) =>
        paragraph.Format.ContextualSpacing == true && next != null && SameStyle(paragraph, next) ? 0
        : paragraph.Format.AfterAuto == true && !context.Profile.AddsSpacing && next != null && TableLayout.ListPair(paragraph, next) ? 0
        : OwnAfter(paragraph);

    /// <summary>
    /// The space after a paragraph before the block that follows it: a table's first paragraph counts for contextual
    /// spacing (measured with docx/local/c5-cells), not as an item of the same list, the last item before a table
    /// keeping its auto space after (docx/local/acl-list, acl-other: 14 pt above the table whatever its cells hold).
    /// </summary>
    double SpaceAfter(ParagraphBlock paragraph, Block? next) => next is TableBlock t
        ? paragraph.Format.ContextualSpacing == true && TableLayout.FirstParagraph(t.Table) is { } cellFirst && SameStyle(paragraph, cellFirst) ? 0 : OwnAfter(paragraph)
        : SpaceAfter(paragraph, next as ParagraphBlock);

    /// <summary>A paragraph's space after, contextual spacing aside: HTML auto spacing (w:afterAutospacing) 14 pt.</summary>
    double OwnAfter(ParagraphBlock paragraph) =>
        paragraph.Format.AfterAuto == true && !context.Profile.AddsSpacing ? 14 : paragraph.Format.After ?? 0;

    bool SameStyle(ParagraphBlock a, ParagraphBlock b) =>
        context.Styles.ParagraphStyleOf(a.Format.Style) == context.Styles.ParagraphStyleOf(b.Format.Style);

    /// <summary>
    /// Vertical alignment of the page's text (w:vAlign): centered or at the bottom, the text counted from the top
    /// margin (with the space before its first paragraph) to the space after its last paragraph (measured with
    /// page-setup, page 4; docx/local/va-1 to va-5 in modes 12 to 15); justified (both), the room left spread evenly
    /// between the paragraphs (va-6: three paragraphs at the top, in the middle and at the bottom;
    /// docx/external/libreoffice/extras-ooxmlexport/tdf36117_verticalAdjustment). Centered or at the bottom, the page's
    /// floating objects count down to their bottoms and move with the text, none moving up (measured with docx/local/
    /// vao-11, vao-14, vao-15: a page frame centred on the page, a text box at the page's top wrapped square, rectangles
    /// in front of the text 400 and 600 pt down the page or wrapped square below their paragraph move with the three
    /// lines, the room below the lowest of them shared out; a page-sized rectangle behind the text leaves none; tdf36117).
    /// </summary>
    static void AlignVertically(PageBox page)
    {
        var align = page.Section.VerticalAlign;
        if (align is not ("center" or "bottom" or "both") || page.Lines.Count == 0)
            return;
        var top = page.BodyTop;
        var bottom = page.BodyBottom;
        // An empty paragraph that only ends the section counts nothing (measured with docx/local/vb-1 to vb-3: one
        // after a centered line moves it 5.8 pt down, at the bottom 11.6 pt; an empty paragraph of the text counts).
        if (!page.Lines.Any(l => !l.SectionMark))
            return;
        var text = page.Lines.Where(l => !l.SectionMark).ToList();
        var marked = text.Count < page.Lines.Count && page.Lines[^1].SectionMark;
        var used = text.Max(l => l.Top + l.Line.Height) + (marked ? 0 : page.TrailingSpace) - top;
        if (align != "both")
        {
            foreach (var drawing in page.Drawings)
                used = Math.Max(used, drawing.Y + drawing.Height - top);
            foreach (var area in page.FloatingTables.Where(e => !e.Story))
                used = Math.Max(used, area.Bottom - top);
        }
        var room = Math.Max(0, bottom - top - used);
        // Where each paragraph of the text begins (not those of tables or notes) and how far it moves down.
        var starts = align == "both" ? page.Lines.Where(l => l.Numbered && l.Line.First && !l.SectionMark).Select(l => l.Top).Distinct().Order().ToList() : [];
        if (align == "both" && starts.Count < 2)
            return;
        double Shift(double y)
        {
            if (align != "both")
                return align == "center" ? room / 2 : room;
            var k = starts.FindLastIndex(s => s <= y + 0.01);
            return Math.Max(0, k) * room / (starts.Count - 1);
        }
        for (var i = 0; i < page.Lines.Count; i++)
            page.Lines[i] = page.Lines[i] with { Top = page.Lines[i].Top + Shift(page.Lines[i].Top) };
        foreach (var fills in new[] { page.Shading, page.Borders })
        {
            for (var i = 0; i < fills.Count; i++)
                fills[i] = fills[i] with { Y = fills[i].Y + Shift(fills[i].Y) };
        }
        for (var i = 0; i < page.Strokes.Count; i++)
            page.Strokes[i] = page.Strokes[i].Offset(0, Shift(Math.Min(page.Strokes[i].Y1, page.Strokes[i].Y2)));
        if (align == "both" || room <= 0)
            return;
        var moved = Shift(0);
        for (var i = 0; i < page.Drawings.Count; i++)
        {
            var drawing = page.Drawings[i];
            page.Drawings[i] = drawing with { Y = drawing.Y + moved, Clip = drawing.Clip is { } clip ? clip with { Y = clip.Y + moved } : null };
        }
        for (var i = 0; i < page.FloatingTables.Count; i++)
        {
            var area = page.FloatingTables[i];
            if (!area.Story)
                page.FloatingTables[i] = area with { Top = area.Top + moved, Bottom = area.Bottom + moved, Polygon = area.Polygon?.Select(q => (q.X, q.Y + moved)).ToArray() };
        }
    }
}
