using System.Text;
using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Text;

namespace FreeSlate.Docx.Layout;

/// <summary>A piece of a line with one style: its text, where it starts (points from the column's left edge) and its advances.</summary>
public sealed class Piece
{
    public required string Text { get; init; }
    public required RunStyle Style { get; init; }

    /// <summary>Whether the piece's first letter joins the letter before it, its last the one after (Arabic words over runs).</summary>
    public bool JoinsBefore { get; init; }
    public bool JoinsAfter { get; init; }
    public double X { get; set; }

    /// <summary>Where the piece's text begins in its paragraph's text; -1 for tabs and spacers.</summary>
    public int Start { get; init; } = -1;

    /// <summary>Room before the piece's first character, in its first advance: a run border's line and space.</summary>
    public double Lead { get; init; }

    /// <summary>The advance of every UTF-16 unit in points (a cluster's advance on its first unit, spacing and scale included).</summary>
    public required double[] Advances { get; init; }

    /// <summary>Extra width justification gives each of the first <see cref="JustifiedSpaces"/> spaces of the piece.</summary>
    public double SpaceExtra { get; set; }
    public int JustifiedSpaces { get; set; }

    /// <summary>The spaces at the piece's start that justification leaves as they are (those that begin a line).</summary>
    public int UnspreadSpaces { get; set; }

    /// <summary>
    /// Whether justification widens the character at <paramref name="index"/>: a space past the unspread ones, among
    /// the justified ones; <paramref name="spaces"/> counts the piece's spaces met so far.
    /// </summary>
    public bool Widened(int index, ref int spaces)
    {
        if (Text[index] != ' ')
            return false;
        var k = spaces++;
        return k >= UnspreadSpaces && k < UnspreadSpaces + JustifiedSpaces;
    }

    public bool IsTab { get; init; }

    /// <summary>Part of the paragraph's list number (with a space after it).</summary>
    public bool IsLabel { get; init; }

    /// <summary>The tab's leader (dot, hyphen, underscore, heavy, middleDot) or null.</summary>
    public string? Leader { get; init; }

    /// <summary>An inline drawing the piece stands for (one object replacement character), or null.</summary>
    public DrawingInline? Drawing { get; init; }

    /// <summary>
    /// The width a horizontal rule is drawn with and how far its bottom stands below the baseline (Vml.Rule), where
    /// the piece is one; it takes no room in the line.
    /// </summary>
    public (double Width, double Bottom)? Rule { get; set; }

    /// <summary>An equation the piece stands for (one object replacement character), or null.</summary>
    public Equations.Equation? Equation { get; init; }

    /// <summary>A legacy check box form field the piece stands for, or null.</summary>
    public CheckBoxInline? CheckBox { get; init; }

    /// <summary>The ruby the piece stands for (catalog: run.ruby), or null.</summary>
    public RubyInline? Ruby { get; init; }

    /// <summary>Text set as two lines in one (w:eastAsianLayout w:combine, catalog: run.eastAsian), or null.</summary>
    public TextInline? Combined { get; init; }

    /// <summary>Text set across the line (w:eastAsianLayout w:vert, catalog: run.eastAsian), or null.</summary>
    public TextInline? Across { get; init; }

    /// <summary>How far the piece stands above the line's baseline by the paragraph's w:textAlignment, in points.</summary>
    public double Shift { get; set; }

    /// <summary>How far an inline drawing stands raised (lowered where negative) by its run's w:position, in points.</summary>
    public double Lift { get; set; }

    /// <summary>Where a display equation's line stands from the paragraph's left indent (m:jc, m:wrapIndent), or null.</summary>
    public double? DisplayX { get; init; }

    /// <summary>The hyperlink of the piece's text, or null.</summary>
    public string? Link { get; init; }

    /// <summary>A note separator's line (w:separator), drawn over the piece's width at the middle of the line.</summary>
    public bool Separator { get; init; }

    /// <summary>The piece's bidirectional embedding level (catalog: run.complexScript): odd levels read right to left.</summary>
    public byte Level { get; init; }

    /// <summary>Whether the piece reads right to left: the browser draws it whole from its right end.</summary>
    public bool RightToLeft => Level % 2 == 1;

    public double Width => Advances.Sum() + SpaceExtra * JustifiedSpaces;
}

/// <summary>A laid-out line of a paragraph (catalog: engine.lineLayout).</summary>
public sealed class Line
{
    public List<Piece> Pieces { get; } = [];

    /// <summary>Whether the line ends with the hyphen of a word broken at its end (automatic or soft hyphenation).</summary>
    public bool Hyphenated { get; set; }

    /// <summary>Whether that hyphen is automatic hyphenation's (w:autoHyphenation), not a soft hyphen.</summary>
    public bool AutoHyphenated { get; set; }

    /// <summary>The width of punctuation that hangs past the line's end (paragraph.eastAsian), which alignment leaves out.</summary>
    public double Hanging { get; set; }

    /// <summary>Whether the line took its last character by squeezing its East Asian punctuation (see ParagraphLayout.CompressesPunctuation).</summary>
    public bool PunctuationSqueezed { get; set; }

    /// <summary>
    /// Whether the hanging punctuation is a closing mark's half, punctuation compression's (see
    /// ParagraphLayout.CompressesPunctuation): justification stretches the gap before it too.
    /// </summary>
    public bool HangsHalf { get; set; }

    /// <summary>A copy of the line with less room above its text, its height and baseline <paramref name="by"/> less.</summary>
    public Line Trimmed(double by)
    {
        var copy = (Line)MemberwiseClone();
        copy.Height -= by;
        copy.Baseline -= by;
        return copy;
    }

    /// <summary>
    /// A copy of the line for vertical text whose lines stack from the left (mongolianVert, tbLrV): what lies below its
    /// text (the extra of multiple spacing) lies above it, on the side the next line follows (measured with
    /// docx/local/textboxes: Arial 11 pt at 1.16 lines in a mongolianVert text box).
    /// </summary>
    public Line Flipped()
    {
        var copy = (Line)MemberwiseClone();
        copy.Baseline = Height - Baseline + Above - Below;
        return copy;
    }

    /// <summary>The line's height in points, its baseline from its top and the natural parts above and below the baseline.</summary>
    public double Height { get; set; }
    public double Baseline { get; set; }
    public double Above { get; set; }
    public double Below { get; set; }

    /// <summary>How far raised and lowered text (w:position, super- and subscript) reaches beyond Above and Below.</summary>
    public double RaisedAbove { get; set; }
    public double LoweredBelow { get; set; }

    /// <summary>The largest single line height of the line's fonts, which multiple line spacing multiplies.</summary>
    public double LargestNatural { get; set; }

    /// <summary>How far ruby reaches above Above, which multiple line spacing multiplies too.</summary>
    public double RubyAbove { get; set; }

    /// <summary>
    /// The part of the height below the text that may hang past the bottom of the page: the extra space of multiple
    /// line spacing (measured with docx/generated/headers-footers: a line of Aptos 12 pt at 1.16 lines whose text
    /// ends 0.4 pt above the bottom margin stays on the page, its full height reaching 1.9 pt past it).
    /// </summary>
    public double Overhang { get; set; }

    /// <summary>The height the line needs on the page.</summary>
    public double FitHeight => Height - Overhang;

    /// <summary>Space above the line where floating objects left no room beside it (catalog: engine.floats).</summary>
    public double Skip { get; set; }

    /// <summary>Where the line's room around floating objects begins and ends (from the column's left edge), or null.</summary>
    public double? Start { get; set; }
    public double? Limit { get; set; }

    /// <summary>
    /// The segments of a line that floating objects cut (from the column's left edge), where its paragraph's shading
    /// stands; a segment that begins at the line's natural start begins at the paragraph's indent. Null for lines of
    /// their whole width.
    /// </summary>
    public List<(double Start, double End)>? Cut { get; set; }

    /// <summary>
    /// Whether justification leaves the line's spacing as it is: a line a tab stop past the right indent took further
    /// that ends in a line break (catalog: paragraph.tabs).
    /// </summary>
    public bool Natural { get; set; }

    /// <summary>Whether the line continues past a floating object in a second segment.</summary>
    public bool Jumped { get; set; }

    /// <summary>
    /// How a centered or right aligned line was moved (from the column's left edge): the room's left and right edge
    /// and the width of its content, by which Word places it on its device (PageRenderer.OnDots); null for others.
    /// </summary>
    public (bool Center, double Left, double Right, double Width)? Alignment { get; set; }

    /// <summary>
    /// For a line of a right-to-left paragraph: its alignment (w:jc), the edges of its room as it shows (from the
    /// column's left edge) and its content's width, by which Word places it from the page's right edge
    /// (PageRenderer.OnDots); null for others.
    /// </summary>
    public (string Justification, double Left, double Right, double Width)? Mirrored { get; set; }

    /// <summary>The line's room as laid out before mirroring (w:jc, its start and end edge, its content's width).</summary>
    internal (string Justification, double Start, double End, double Width)? Room { get; set; }

    /// <summary>The footnotes whose references stand in the line (catalog: note.footnotes).</summary>
    public List<string>? Notes { get; set; }

    /// <summary>The floating drawings anchored in the line, which wp:positionV relativeFrom="line" places from its top.</summary>
    public List<AnchorInline>? Anchors { get; set; }

    /// <summary>Where in the line (from the column's left edge) each of its drawings is anchored, for relativeFrom="character".</summary>
    public Dictionary<AnchorInline, double>? AnchorX { get; set; }

    /// <summary>The brackets of editable ranges in the line: where (from the column's left edge) and whether they open.</summary>
    public List<(double X, bool Start)>? Brackets { get; set; }

    /// <summary>Where comments' ranges begin and end in the line, in the markup view (catalog: review.comments).</summary>
    public List<(double X, string Id, bool Start)>? Comments { get; set; }

    /// <summary>
    /// In the markup view (catalog: review.revisions): whether the line has a change bar beside it whatever its text
    /// (lines of a paragraph or table whose formatting changed, the last line of a paragraph whose mark changed), and
    /// the formatting change of its paragraph or table whose balloon it anchors.
    /// </summary>
    public bool Changed { get; set; }
    public FormatChange? BlockChange { get; set; }

    /// <summary>The formatting changes of hidden text in the line and where it would stand (markup view).</summary>
    public List<(double X, FormatChange Change)>? HiddenChanges { get; set; }

    /// <summary>Bar tab stops (w:tab w:val="bar"): vertical lines at these positions from the column's left edge.</summary>
    public List<double>? Bars { get; set; }

    /// <summary>How far below the line its bars reach: the space after the paragraph, on its last line.</summary>
    public double BarExtension { get; set; }

    /// <summary>The break that ends the line: none, a line break, a page break or a column break.</summary>
    public string? EndBreak { get; set; }

    /// <summary>Whether the line runs down the page (vertical text): its height is across the page.</summary>
    public bool Vertical { get; init; }

    /// <summary>
    /// Whether the line's East Asian characters are turned a quarter against it (counterclockwise): upright in vertical
    /// text (w:textDirection tbRlV and tbLrV, and tbRl in sections), lying in lrTbV.
    /// </summary>
    public bool TurnEastAsian { get; init; }

    public bool First { get; init; }
    public bool Last { get; set; }

    /// <summary>The paragraph the line is of (set by <see cref="ParagraphLayout.Layout"/>).</summary>
    public ParagraphBlock? Paragraph { get; set; }

    /// <summary>Where the line's text begins in its paragraph's text.</summary>
    public int TextStart { get; init; }
}

/// <summary>
/// The room a line has between floating objects (catalog: engine.floats): its segments from the column's left edge, and
/// how far down (from the paragraph's first line) the line moves where they cannot take it.
/// </summary>
public sealed record LineRoom(List<(double Start, double End)> Segments, double SkipTo = 0)
{
    /// <summary>Whether only objects wrapped tightly or through (along their wrap polygons) stand beside the line.</summary>
    public bool Tight { get; init; }

    /// <summary>The right edge of the room a drop cap beside the line takes, from the column's left, or null.</summary>
    public double? DropCap { get; init; }
}

/// <summary>
/// Where a line would stand, for the room floating objects leave it: its top below the paragraph's first line top, its
/// estimated height and the top and bottom of its text (ascent above, descent below the baseline) from its top; after a
/// text wrapping break with w:clear the side ("left", "right", "all") whose objects the line has to pass below.
/// </summary>
public readonly record struct LineSlot(double Offset, double Height, double TextTop, double TextBottom, string? Clear = null);

/// <summary>
/// The values of the page fields where a paragraph stands (catalog: field.evaluation, section.pageNumbers): the page's
/// number and its section's number format, the document's pages and the section's.
/// </summary>
public sealed record PageFields(int Page, string PageFormat, int Pages, int SectionPages)
{
    /// <summary>The page whose header or footer is laid out, for the fields that look at its paragraphs (STYLEREF).</summary>
    public PageBox? On { get; init; }

    /// <summary>The chapter number and its separator before page numbers (w:pgNumType w:chapStyle), or "".</summary>
    public string Chapter { get; init; } = "";

    /// <summary>The field's result here, or null for fields the layout does not compute.</summary>
    public string? Value(FieldInline field)
    {
        var format = NumberText.FieldFormat(field.NumberFormat);
        return field.Type switch
        {
            "PAGE" => Chapter + NumberText.Format(Page, format ?? PageFormat),
            "NUMPAGES" => NumberText.Format(Pages, format),
            "SECTIONPAGES" => NumberText.Format(SectionPages, format),
            _ => null,
        };
    }
}

/// <summary>
/// Breaks a paragraph into lines the way Word does (catalog: paragraph.basic, paragraph.spacing, paragraph.tabs,
/// engine.lineLayout). Widths come from the fonts shaped with Word's features at the nominal size (measured: glyph
/// positions in Word's PDF match within 0.1 pt per line without kerning, 0.3 pt with it); a line ends at the last
/// break opportunity (UAX #14) whose text fits, trailing spaces hanging past the end; a word longer than a whole line
/// breaks between characters.
/// </summary>
public sealed class ParagraphLayout(DocumentContext context, RunStyles styles)
{
    /// <summary>The run styles the layout measures with.</summary>
    public RunStyles Styles => styles;

    /// <summary>Whether a character trails a line's content at its end.</summary>
    bool Trails(char c) => c == ' ' || context.Profile.IdeographicTrails && c == (char)0x3000;

    /// <summary>
    /// The line pitch of the section's document grid (w:docGrid type lines, linesAndChars or snapToChars), which lines
    /// of paragraphs that snap to the grid take whole units of; null without a line grid (catalog: section.docGrid).
    /// </summary>
    public double? LinePitch { get; set; }

    /// <summary>
    /// What the section's character grid (w:docGrid type linesAndChars or snapToChars, w:charSpace in 4096ths of a point)
    /// adds to every character, 0 without one (measured with docx/local/docgrid: about w:charSpace / 4096 for East
    /// Asian and Latin characters alike).
    /// </summary>
    public double CharacterExtra { get; set; }

    /// <summary>
    /// Whether the character grid adds to East Asian characters only (w:docGrid type snapToChars; measured with
    /// docx/local/cgr-chars, cgr-both: Latin text at its natural width there, Japanese one point wider a character;
    /// docx/external/libreoffice/extras-ooxmlexport/tdf91261).
    /// </summary>
    public bool CharacterExtraEastAsian { get; set; }

    /// <summary>Whether the paragraphs being laid out stand in a table cell.</summary>
    public bool InCell { get; set; }

    /// <summary>
    /// Before Word 2013, the character grid's pitch (the Normal style's size and w:charSpace / 4096), whose whole
    /// multiples the lines of paragraphs that snap to the grid span, aligned and justified there; null otherwise
    /// (measured with docx/local/cgw14-*, cgw12-dist: a 415.6 pt column holds 17 pitches of 24 pt, a distributed line
    /// spreads over 408 pt, right aligned text ends there, centered text is centered in it, with runs of 10.5 pt too;
    /// cgw-*, cgw15c-dist: Word 2013 and later use the whole column; docx/external/libreoffice/extras-ooxmlexport/
    /// tdf107359-char-pitch).
    /// </summary>
    public double? GridCell { get; set; }

    /// <summary>
    /// The cell of a character grid of snapToChars (the Normal style's size and w:charSpace / 4096), null otherwise. The
    /// cells fill the width they are laid out in, as many whole ones as fit stretched to it, East Asian characters one
    /// each; a paragraph's last line of Latin text stands centered in the whole cells it needs from its start, however
    /// the paragraph is aligned and whether or not it snaps to the grid (measured with docx/local/cglc-snap0: lines of
    /// Calibri 11 left and right aligned and centered, on cells of 11 pt, each centered in 9, 8, 6 and 2 cells from the
    /// margin; cgl-*: cells of 20.265 pt before Word 2013, whose lines shrink to whole cells, 20.51 pt across 451.3 pt
    /// from Word 2013 on, of 12 pt and 12.2 pt; gbx-snap-*: in a text box 185.6 pt wide cells of 20.62 pt in both;
    /// full lines and lines with Japanese at their start; docx/external/libreoffice/extras-ooxmlexport/tdf91261: cells
    /// of 21.265 pt, w:snapToGrid off alike, found by agent BG).
    /// </summary>
    public double? SnapCell { get; set; }

    /// <summary>
    /// Whether the paragraphs being laid out run down the page (vertical text in sections and table cells): their
    /// lines are thicker (see <see cref="Metrics"/>) and text set across them takes a horizontal line's height.
    /// </summary>
    public bool Vertical { get; set; }

    /// <summary>Whether the lines' East Asian characters are turned a quarter against them (see <see cref="Line.TurnEastAsian"/>).</summary>
    public bool TurnEastAsian { get; set; }

    /// <summary>Whether lines are left where they begin, unaligned: for measuring their natural lengths (rotated cells).</summary>
    public bool Unaligned { get; set; }

    /// <summary>
    /// Lines laid out <c>Laid</c> wide so as not to wrap (text boxes that do not wrap) align in the width
    /// <c>Area</c> instead; those of other widths (table cells in the box) in their own.
    /// </summary>
    public (double Laid, double Area)? AlignIn { get; set; }

    /// <summary>The color of automatic text with no shading behind it: a text box's from its shape's style (fontRef).</summary>
    public FreeSlate.Drawing.RgbColor? TextColor { get; set; }

    /// <summary>The width of the column the text stands in, of which horizontal rules take a share (Vml.Rule).</summary>
    public Func<double>? ColumnWidth { get; set; }

    /// <summary>The paragraph's own space before, which its first line keeps below floating objects that push it down.</summary>
    public double PushedSpace { get; set; }

    /// <summary>Whether a page break in a paragraph goes unseen (Paginator.BreakUnseen), its line then the mark's.</summary>
    public Func<ParagraphBlock, bool>? BreakUnseen { get; set; }

    /// <summary>Whether the paragraph being laid out has its page breaks unseen.</summary>
    bool unseenBreak;

    /// <summary>
    /// A style's parts of a line above and below the baseline: in vertical lines 1/32 of the size more (1.3 times that
    /// for East Asian fonts, as their lines are), half above and half below (measured with docx/local/vertical3,
    /// vertical4, vertical5: Yu Mincho, MS Mincho, Meiryo, Yu Gothic and Arial at 8 to 28 pt, single and 1.5 lines, in
    /// sections and table cells; exact and at least spacing keep their heights).
    /// </summary>
    static (double Above, double Below) Metrics(RunStyle style, bool vertical)
    {
        var (above, below) = style.Metrics;
        if (!vertical)
            return (above, below);
        var extra = (style.Font.Data.EastAsian ? 1.3 : 1) * style.Size / 32;
        return (above + extra / 2, below + extra / 2);
    }

    /// <summary>
    /// Text set across the line (w:eastAsianLayout w:vert, catalog: run.eastAsian; measured with docx/local/vertical,
    /// vertical2 and vertical3): a little line of its own, turned a quarter against the line (counterclockwise), in the
    /// style of its first character; along the line it takes the height of a line across it (a vertical line's in
    /// horizontal text, a horizontal one's in vertical text), its baseline that line's ascent from where it begins;
    /// across the line it is centered on the middle of its font's win ascent and descent, reaching beyond the line's
    /// text as far as it is long (w:vertCompress changes nothing in Word's printout).
    /// </summary>
    public (RunStyle Style, double Length, double Slot, double Ascent, double Middle) Across(TextInline run, bool verticalLine)
    {
        var style = styles.Character(run.Format, char.ConvertToUtf32(run.Text, 0));
        var (above, below) = Metrics(style, !verticalLine);
        return (style, Measure(run.Text, style).Sum(), above + below, above, (style.Font.Data.WinAscent - style.Font.Data.WinDescent) / 2 * style.Size);
    }

    /// <summary>The position of the character at a place in the paragraph's text within its line.</summary>
    static double CharacterX(Line line, int index)
    {
        var end = line.Pieces.Count > 0 ? line.Pieces[0].X : 0;
        foreach (var piece in line.Pieces)
        {
            if (piece.Start >= 0 && index < piece.Start)
                return piece.X;
            if (piece.Start >= 0 && index < piece.Start + piece.Text.Length)
            {
                var x = piece.X;
                var spaces = 0;
                for (var k = 0; k < index - piece.Start; k++)
                {
                    x += piece.Advances[k];
                    if (piece.Widened(k, ref spaces))
                        x += piece.SpaceExtra;
                }
                return x;
            }
            end = piece.X + piece.Width;
        }
        return end;
    }

    /// <summary>
    /// Segments beside floating objects narrower than this take no text, also none of an empty line, and the first of
    /// a line only where its first word fits, the line going down otherwise (measured with docx/local/tf-*, tk-*: 17.3
    /// pt beside a table or a shape stay empty, 19.3 pt take "a an"; tq-*: empty lines go below a shape leaving 16 pt;
    /// te-*, tg-*, th-*, tl-*: a word of 40.6 pt goes below a table or a shape leaving 39.3 pt, beside one leaving
    /// 43.3, one of 27.8 pt beside a shape leaving 27.8; within 0.5 pt beside tables, ti-*, tj-*). Before Word 2013
    /// segments beside objects wrapped along their polygons take text from 9 pt (tv-*, tw-*: 10 pt take empty lines
    /// and 12.75 "a", 8 pt none, not so in mode 15, tr-3; docx/external/libreoffice/core-text/fly-minimal-wrap).
    /// </summary>
    internal const double MinimumSegment = 18, MinimumTightSegment = 9;

    /// <summary>How far into the column a line taken past the right indent by a tab reaches before Word 2013 (22 in).</summary>
    const double MaximumLine = 1584;

    /// <summary>The inline drawings of the paragraph being laid out, by their place in its text.</summary>
    readonly Dictionary<int, DrawingInline> objects = [];

    /// <summary>The fields Word computes when it prints, or null to show the saved results.</summary>
    public FieldEvaluator? Evaluator { get; set; }

    /// <summary>
    /// Marks of notes that differ from the ones read, by kind (endnote or not) and id: footnotes numbered anew on every
    /// page, which take their numbers from the pages they land on (catalog: note.footnotes).
    /// </summary>
    public Dictionary<(bool Endnote, string Id), string>? NoteMarks { get; set; }

    /// <summary>The check box form fields of the paragraph being laid out, by their place in its text.</summary>
    readonly Dictionary<int, CheckBoxInline> boxes = [];

    /// <summary>The automatic spaces between East Asian and Latin text of the paragraph being laid out, by the character they follow.</summary>
    readonly Dictionary<int, double> autoSpaced = [];

    /// <summary>
    /// What justification adds after a piece's character at a boundary between East Asian and Latin text: the
    /// automatic space twice, less the one the text already has (measured with docx/local/cjk-justify-lines and
    /// cjk-justify-western: a distributed line of a document without East Asian default font has twice the space there,
    /// one with such a font once more).
    /// </summary>
    double Doubled(Piece piece, int index) =>
        piece.Start >= 0 && autoSpaced.TryGetValue(piece.Start + index, out var space) ? styles.EastAsianDefaults ? space : 2 * space : 0;

    /// <summary>
    /// Latin words breaking anywhere (w:wordWrap off; catalog: paragraph.eastAsian): a line may end between any two
    /// characters that are not spaces, kinsoku aside (measured with docx/local/ea-opts: Latin and mixed paragraphs).
    /// </summary>
    static void BreakWords(string text, bool[] breaks, LineBreaker.Kinsoku rules)
    {
        for (var i = 0; i + 1 < text.Length; i++)
        {
            var (c, next) = (text[i], text[i + 1]);
            if (!char.IsWhiteSpace(c) && !char.IsWhiteSpace(next) && c is not ('\uFFFC' or '\u00AD') && !char.IsLowSurrogate(next)
                && !rules.NotFirst.Contains(next) && !rules.NotLast.Contains(c))
                breaks[i] = true;
        }
    }

    const string ClosingPunctuation = "\u3001\u3002\uFF0C\uFF0E\u300D\u300F\uFF09\u3015\uFF3D\uFF5D\u3009\u300B\u3011\u3017\u3019";
    const string OpeningPunctuation = "\u300C\u300E\uFF08\u3014\uFF3B\uFF5B\u3008\u300A\u3010\u3016\u3018";

    /// <summary>
    /// Punctuation kerning in kerned runs (w:kern; catalog: paragraph.eastAsian): an East Asian comma, period or
    /// closing bracket before another one or before an opening bracket loses half its width, unless the document says
    /// w:noPunctuationKerning (measured with docx/local/ea-punctkern: 」、, 、（ and ）。 in 11 pt MS Mincho 5.5 pt
    /// closer; runs without kerning keep their widths).
    /// </summary>
    static void KernPunctuation(string text, List<RunStyle> styles, double[] advances)
    {
        for (var i = 0; i + 1 < text.Length && i < styles.Count; i++)
        {
            if (ClosingPunctuation.Contains(text[i]) && (ClosingPunctuation.Contains(text[i + 1]) || OpeningPunctuation.Contains(text[i + 1]))
                && styles[i].Features.Contains("kern"))
                advances[i] -= styles[i].Size / 2;
        }
    }

    /// <summary>The automatic space the text has after a character (see <see cref="AutoSpace"/>), 0 where none.</summary>
    double EndSpace(int index) => styles.EastAsianDefaults && autoSpaced.TryGetValue(index, out var space) ? space : 0;

    /// <summary>
    /// A line that ends between East Asian and Latin text drops the automatic space at its end (measured with
    /// docx/local/cjk-justify15: a justified line of Latin capitals and ideographs before a line beginning with a capital
    /// fills the line without it).
    /// </summary>
    void DropEndSpace(Line line, string text, int end)
    {
        var at = end - 1;
        while (at >= 0 && at < text.Length && char.IsWhiteSpace(text[at]))
            at--;
        var space = at >= 0 ? EndSpace(at) : 0;
        if (space <= 0)
            return;
        foreach (var piece in line.Pieces)
        {
            if (piece.Start >= 0 && at >= piece.Start && at < piece.Start + piece.Advances.Length)
            {
                piece.Advances[at - piece.Start] -= space;
                return;
            }
        }
    }

    /// <summary>
    /// Whether a paragraph has an East Asian language for East Asian text (w:lang w:eastAsia): its runs or its mark,
    /// where the document defaults name none or an East Asian one too (measured with docx/local/hgp-*: a comma or period
    /// after Latin words before Word 2013 hangs with zh-CN in the Normal style and no document defaults, not with en-US
    /// as the defaults' East Asian language; docx/external/libreoffice/extras-ooxmlexport/
    /// tdf89377_tableWithBreakBeforeParaStyle; extras-ooxmlimport/groupshape-child-rotation: zh-CN in the defaults, en-US
    /// in Normal, no period hangs).
    /// </summary>
    bool HasEastAsianLanguage(ParagraphBlock paragraph)
    {
        static bool Asian(string? tag) => tag != null && (tag.StartsWith("ja", StringComparison.OrdinalIgnoreCase)
            || tag.StartsWith("zh", StringComparison.OrdinalIgnoreCase) || tag.StartsWith("ko", StringComparison.OrdinalIgnoreCase));
        if (context.Styles.DefaultRun.LanguageEastAsia is { } language && !Asian(language))
            return false;
        return Asian(paragraph.MarkFormat.LanguageEastAsia) || paragraph.Content.Any(i => Asian(i.Format.LanguageEastAsia));
    }

    /// <summary>Whether a paragraph's text has East Asian characters.</summary>
    static bool HasEastAsian(string text)
    {
        foreach (var c in text)
        {
            if (c >= 0x80 && Text.Scripts.Of(c) is { Slot: Text.FontSlot.EastAsian })
                return true;
        }
        return false;
    }

    /// <summary>The East Asian language of a paragraph's text, for its kinsoku: that of its first East Asian character.</summary>
    static string? EastAsianLanguage(string text, List<RunStyle> charStyles)
    {
        for (var i = 0; i < text.Length && i < charStyles.Count; i++)
        {
            if (text[i] >= 0x80 && Text.Scripts.Of(text[i]) is { Slot: Text.FontSlot.EastAsian })
                return charStyles[i].Language;
        }
        return null;
    }

    /// <summary>Where the line being built ends with an automatic hyphen, or -1.</summary>
    int autoHyphenAt = -1;

    /// <summary>
    /// The places a hyphen may stand before in the paragraph's text (catalog: paragraph.hyphenation), or null where it
    /// is not hyphenated: with automatic hyphenation (w:autoHyphenation) and not suppressed (w:suppressAutoHyphens), the
    /// words of languages with patterns, all capitals left out where w:doNotHyphenateCaps says so.
    /// </summary>
    bool[]? Hyphenation(string text, List<RunStyle> charStyles, ParagraphFormat format)
    {
        if (context.Settings.Child("autoHyphenation").OnOff() != true || format.SuppressAutoHyphens == true)
            return null;
        var caps = context.Settings.Child("doNotHyphenateCaps").OnOff() != true;
        bool[]? result = null;
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsLetter(text[i]))
                continue;
            var end = i;
            while (end < text.Length && (char.IsLetter(text[end]) || char.GetUnicodeCategory(text[end]) == UnicodeCategory.NonSpacingMark))
                end++;
            var language = charStyles[i].Language ?? context.Culture.Name;
            var word = text[i..end];
            var numeric = end < text.Length && char.IsDigit(text[end]) || i > 0 && char.IsDigit(text[i - 1]);
            if (!numeric && (caps || word.Any(char.IsLower)) && FreeSlate.Text.Hyphenator.For(language) is { } hyphenator)
            {
                foreach (var point in hyphenator.Points(word))
                    (result ??= new bool[text.Length + 1])[i + point] = true;
            }
            i = end;
        }
        return result;
    }

    /// <summary>The room run borders take before the first character of their boxes, by its place in the text.</summary>
    readonly Dictionary<int, double> leads = [];

    /// <summary>
    /// Run borders (w:bdr, catalog: run.decoration) take room in the line: their lines and space before the first and
    /// after the last character of a box, neighboring runs with the same border sharing one box (measured with
    /// docx/local/ulsizes: the text stands the border's lines and space, both floored to Word's 0.12 pt dots, inside
    /// the frame, and the next text as far outside it).
    /// </summary>
    void BorderRoom(string text, List<RunStyle> charStyles, double[] advances)
    {
        leads.Clear();
        for (var i = 0; i < text.Length; i++)
        {
            if (charStyles[i].Border is not { } border || i > 0 && Equals(charStyles[i - 1].Border, border) && text[i - 1] is not ('\n' or '\f' or '\v'))
                continue;
            var end = i;
            while (end + 1 < text.Length && Equals(charStyles[end + 1].Border, border) && text[end + 1] is not ('\n' or '\f' or '\v'))
                end++;
            var room = PageBorders.Room(border);
            leads[i] = room;
            advances[i] += room;
            advances[end] += room;
            i = end;
        }
    }

    /// <summary>
    /// Text fit to a width (w:fitText, catalog: run.spacing), as Word prints it (measured with docx/local/fittext and
    /// fittext2): neighboring runs with the same id fit together; text wider than the width is scaled horizontally to
    /// it, narrower text spreads its characters with equal gaps between them, the last at the end (Word also narrows
    /// spread characters a little, to between (n − 1)/n of their width and all of it, which is left out here).
    /// </summary>
    void FitText(string text, List<RunStyle> charStyles, double[] advances)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (charStyles[i].FitText is not { } width)
                continue;
            var id = charStyles[i].FitTextId;
            var end = i;
            while (end + 1 < text.Length && charStyles[end + 1].FitText is { } next && Math.Abs(next - width) < 0.01
                   && charStyles[end + 1].FitTextId == id && text[end + 1] is not ('\n' or '\f' or '\v'))
                end++;
            var natural = 0.0;
            var count = 0;
            for (var k = i; k <= end; k++)
            {
                natural += advances[k];
                if (advances[k] > 0)
                    count++;
            }
            if (natural > width + 0.01)
            {
                // Too wide: the glyphs narrow to the width (one style object per style it had, so pieces stay whole).
                var scale = width / natural;
                var narrowed = new Dictionary<RunStyle, RunStyle>(ReferenceEqualityComparer.Instance);
                for (var k = i; k <= end; k++)
                {
                    if (!narrowed.TryGetValue(charStyles[k], out var style))
                        narrowed[charStyles[k]] = style = charStyles[k] with { Scale = charStyles[k].Scale * scale };
                    charStyles[k] = style;
                    advances[k] *= scale;
                }
            }
            else if (count > 0)
            {
                // Narrower: equal gaps between the characters, the last one ending at the width.
                var gaps = Math.Max(1, count - 1);
                var gap = (width - natural) / gaps;
                var last = Enumerable.Range(i, end - i + 1).Last(k => advances[k] > 0);
                for (var k = i; k <= end; k++)
                {
                    if (advances[k] > 0 && (k < last || count == 1))
                        advances[k] += gap;
                }
            }
            i = end;
        }
    }

    /// <summary>The rubies and the two-lines-in-one runs of the paragraph being laid out, by their place in its text.</summary>
    readonly Dictionary<int, RubyInline> rubies = [];
    readonly Dictionary<int, TextInline> combined = [];
    readonly Dictionary<int, TextInline> across = [];

    /// <summary>
    /// The spaces of runs whose East Asian font is one (with w:balanceSingleByteDoubleByteWidth), by their place in the
    /// text: those balance; after <see cref="Balance"/> the ones that did.
    /// </summary>
    readonly HashSet<int> balanced = [];

    /// <summary>The advance of every character of a text in one style, in points.</summary>
    public static double[] Measure(string text, RunStyle style)
    {
        var advances = new double[text.Length];
        foreach (var g in style.Font.Data.GlyphsWithFeatures(text, style.Features, language: style.Language))
        {
            if (g.Cluster < text.Length)
                advances[g.Cluster] += g.Advance * style.Size * style.Scale;
        }
        return advances;
    }

    /// <summary>The widths of a ruby's base and phonetic text in their styles.</summary>
    public (double Base, double Ruby) RubyWidths(RubyInline ruby) =>
        (ruby.Base.Sum(t => Measure(t.Text, styles.Style(t.Format, WordSlot.EastAsia)).Sum()),
         ruby.Ruby.Sum(t => Measure(t.Text, styles.Style(t.Format, WordSlot.EastAsia)).Sum()));

    /// <summary>
    /// Two lines in one (measured with docx/local/ruby): the text in half its size, its first half (rounded up) above
    /// its second, as wide as the wider line, between brackets of the full size where w:combineBrackets asks for them.
    /// </summary>
    public (string First, string Second, RunStyle Half, double Width, string Open, string Close) Combination(TextInline run)
    {
        var style = styles.Style(run.Format, WordSlot.EastAsia);
        var half = style with { Size = style.Size / 2 };
        var split = (run.Text.Length + 1) / 2;
        var (first, second) = (run.Text[..split], run.Text[split..]);
        var (open, close) = run.Format.CombineBrackets switch
        {
            "round" => ("(", ")"), "square" => ("[", "]"), "angle" => ("<", ">"), "curly" => ("{", "}"), _ => ("", ""),
        };
        var width = Math.Max(Measure(first, half).Sum(), Measure(second, half).Sum())
                    + Measure(open, style).Sum() + Measure(close, style).Sum();
        return (first, second, half, width, open, close);
    }

    /// <summary>
    /// A check box form field's metrics: a character of the run's font at the box's size, its advance the font's line
    /// height (measured with docx/local/form-boxes: Arial 8 to 36 pt and Aptos).
    /// </summary>
    (double Above, double Below) BoxMetrics(CheckBoxInline box) =>
        styles.Style(box.Format, WordSlot.Ascii) is var style ? WordFonts.LineMetrics(style.Font.Data, box.Size ?? style.Size, style.LineHeights) : default;

    /// <summary>The equations of the paragraph being laid out, by their place in its text, display lines with their place.</summary>
    readonly Dictionary<int, (Equations.Equation Equation, double? DisplayX)> equations = [];

    /// <summary>The width display equations of the paragraph being laid out may take (between its indents).</summary>
    double displayWidth = double.MaxValue;

    /// <summary>Equations laid out once for every layout of their paragraph at a width: their lines.</summary>
    readonly System.Runtime.CompilerServices.ConditionalWeakTable<MathInline, Tuple<double, List<Equations.Equation>>> laidEquations = new();

    /// <summary>The document's math settings (m:mathPr).</summary>
    System.Xml.Linq.XElement? MathSettings => context.Settings?.Element(Equations.MathLayout.M + "mathPr");

    string? MathSetting(string name) => (string?)MathSettings?.Element(Equations.MathLayout.M + name)?.Attribute(Equations.MathLayout.M + "val");

    /// <summary>
    /// The lines of an equation (catalog: math.equations, math.settings): one inline, those of a display equation
    /// broken to fit the width; in the document's math font (m:mathFont, else Cambria Math) with its limit placement
    /// (m:intLim, m:naryLim); none without a math font.
    /// </summary>
    List<Equations.Equation> EquationLines(MathInline math, double width)
    {
        if (laidEquations.TryGetValue(math, out var known) && (known.Item1 == width || !math.Display))
            return known.Item2;
        var mathFont = MathSetting("mathFont") is { Length: > 0 } family && context.Fonts.Resolve(family, false, false) is var resolved
            && resolved.Data.Family.Equals(family, StringComparison.OrdinalIgnoreCase) && Equations.MathTable.Of(resolved.Data) is { } own
            ? (resolved.Data, own)
            : Equations.Equation.MathFont(context.Fonts);
        if (mathFont is not var (font, table))
            return [];
        var layout = new Equations.MathLayout(font, table,
            rPr => styles.Style(rPr != null && math.RunFormats.TryGetValue(rPr, out var run) ? run : math.Format, WordSlot.Ascii) is var s
                ? new Equations.MathRunStyle(s.Size, s.Color, s.Font.Data)
                : default,
            Wml.W + "rPr")
        {
            Word = true,
            IntegralLimits = MathSetting("intLim") ?? "subSup",
            NaryLimits = MathSetting("naryLim") ?? "undOvr",
        };
        var items = math.Display
            ? layout.EquationLines(math.Element, width, WrapIndent, MathSetting("brkBin") ?? "before", MathSetting("brkBinSub") ?? "--")
            : [layout.Equation(math.Element, false)];
        var lines = items.Select(item => new Equations.Equation(item)).ToList();
        laidEquations.AddOrUpdate(math, Tuple.Create(width, lines));
        return lines;
    }

    /// <summary>A math setting in twips as points, 0 where not given.</summary>
    double MathTwips(string name) =>
        double.TryParse(MathSetting(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var twips) ? twips / 20 : 0;

    /// <summary>How far the continuation lines of a broken display equation are indented (m:wrapIndent, 1 inch by default).</summary>
    double WrapIndent => double.TryParse(MathSetting("wrapIndent"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var twips) ? twips / 20 : 72;

    /// <summary>The floating drawings of the paragraph being laid out, by their place in its text.</summary>
    readonly List<(int Index, AnchorInline Anchor)> anchored = [];

    /// <summary>The absolute position tabs (w:ptab) of the paragraph being laid out, by their place in its text.</summary>
    readonly Dictionary<int, (string Alignment, string RelativeTo, string Leader)> positional = [];

    /// <summary>The place in its text of the tab after the list number of the paragraph being laid out, or -1.</summary>
    int labelTab = -1;

    /// <summary>The Word 6 numbering of the paragraph's list number (NumberingLevel.Legacy), or null.</summary>
    (double Indent, double Space)? legacyLabel;

    /// <summary>Where absolute position tabs count from in the paragraph being laid out: its margins' and its indents' edges.</summary>
    (double MarginLeft, double MarginRight, double IndentLeft, double IndentRight) positionalArea;

    /// <summary>The starts and ends of editable ranges of the paragraph being laid out, by their place in its text.</summary>
    readonly List<(int Index, bool Start)> permissions = [];
    readonly List<(int Index, string Id, bool Start)> commentMarks = [];
    readonly List<(int Index, FormatChange Change)> changeMarks = [];

    /// <summary>The bidirectional levels of the paragraph being laid out per UTF-16 unit, or null for left-to-right text.</summary>
    byte[]? levels;

    /// <summary>The footnote references of the paragraph being laid out: where each stands and its note.</summary>
    readonly List<(int Index, string Id)> notes = [];

    /// <summary>Note separators of the paragraph being laid out (true for the continuation separator), by their place.</summary>
    readonly Dictionary<int, bool> separators = [];

    /// <summary>The w:clear of the text wrapping breaks of the paragraph being laid out, by their place in its text.</summary>
    readonly Dictionary<int, string> clears = [];

    /// <summary>
    /// The width of a note separator: 144 pt (2 in), the continuation separator the column's width (catalog:
    /// note.footnotes).
    /// </summary>
    double separatorWidth = 144;

    /// <summary>The hyperlinks of the paragraph being laid out: where each linked text starts, its target and length.</summary>
    readonly Dictionary<int, (string Target, int Length)> links = [];

    /// <summary>The hyperlink of the character at <paramref name="index"/>, or null.</summary>
    string? LinkAt(int index)
    {
        foreach (var (start, (target, length)) in links)
        {
            if (index >= start && index < start + length)
                return target;
        }
        return null;
    }

    /// <summary>Lays out a paragraph in a column <paramref name="width"/> points wide.</summary>
    /// <param name="fields">The page fields' values; without them, fields show the result Word saved.</param>
    /// <param name="room">
    /// The room of a line at an offset below the paragraph's first line top for an estimated height, where floating
    /// objects stand beside the paragraph; null for the whole column.
    /// </param>
    /// <param name="from">Where in the paragraph's text the lines begin (Line.TextStart): its rest in another column.</param>
    /// <param name="unhyphenated">Whether the first line breaks before a word it would hyphenate automatically.</param>
    public List<Line> Layout(ParagraphBlock paragraph, double width, PageFields? fields = null, Func<LineSlot, LineRoom>? room = null, int from = 0,
        bool unhyphenated = false)
    {
        var laid = LayoutLines(paragraph, width, fields, room, from, unhyphenated);
        foreach (var line in laid)
            line.Paragraph = paragraph;
        // The markup view marks the lines of a paragraph whose formatting changed and the last line of one whose mark
        // changed; the formatting change's balloon hangs from the first line.
        if (laid.Count > 0 && (paragraph.Change != null || paragraph.MarkRevision != null))
        {
            foreach (var line in laid)
                line.Changed |= paragraph.Change != null;
            laid[^1].Changed = true;
            laid[0].BlockChange = paragraph.Change;
        }
        return laid;
    }

    /// <summary>
    /// In vertical lines the curly quotation marks take a whole em each, their glyphs at its start (measured with
    /// docx/local/vqt-*: ‘ ’ “ ” of Corbel and Calibri 18 a full 18 pt in tbRl and btLr frames and in vert, vert270 and
    /// eaVert boxes, in modes 12 and 15, across 3.94 to 7.11 pt; the ellipsis, the middle dot and the apostrophe keep
    /// theirs; LibreOffice's writerfilter-dmapper/frame-direction: "King’s" in turned frames).
    /// </summary>
    void VerticalQuotes(string text, List<RunStyle> charStyles, double[] advances)
    {
        if (!Vertical)
            return;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '\u2018' or '\u2019' or '\u201C' or '\u201D')
                advances[i] = charStyles[i].Size + charStyles[i].Spacing;
        }
    }

    List<Line> LayoutLines(ParagraphBlock paragraph, double width, PageFields? fields, Func<LineSlot, LineRoom>? room, int from = 0,
        bool unhyphenated = false)
    {
        var format = paragraph.Format;
        if (GridCell is { } cell && cell > 0.01 && format.SnapToGrid != false)
            width = Math.Max(cell, Math.Floor(width / cell + 1e-6) * cell);
        double? snapCell = SnapCell is { } nominal && nominal > 0.01 ? width / Math.Max(1, Math.Floor(width / nominal + 1e-6)) : null;
        displayWidth = width - (format.Left ?? 0) - (format.Right ?? 0);
        unseenBreak = BreakUnseen?.Invoke(paragraph) == true;
        var (text, charStyles, labelLength) = Flatten(paragraph, fields);
        labelTab = paragraph.Label?.Suffix == "tab" ? labelLength : -1;
        legacyLabel = paragraph.Label?.Level.Legacy;
        levels = BidiLevels(text, charStyles, format.Bidi == true, labelLength);
        var advances = Advances(text, charStyles, CharacterExtra + (CharacterExtraEastAsian && snapCell != null ? snapCell.Value - SnapCell!.Value : 0),
            CharacterExtraEastAsian);
        SuffixSpace(paragraph, labelLength, advances);
        if (balanced.Count > 0)
            Balance(text, charStyles, advances, balanced);
        if (context.Settings.Child("noPunctuationKerning").OnOff() != true)
            KernPunctuation(text, charStyles, advances);
        autoSpaced.Clear();
        AutoSpace(text, charStyles, advances, format, autoSpaced, styles.EastAsianDefaults);
        BorderRoom(text, charStyles, advances);
        FitText(text, charStyles, advances);
        VerticalQuotes(text, charStyles, advances);
        // A horizontal rule of a share of the width takes the width it is drawn with (Vml.Rule).
        foreach (var (index, drawing) in objects)
            advances[index] = RuleShare(drawing) is > 0 and var share ? share * (ColumnWidth?.Invoke() ?? width - (format.Left ?? 0) - (format.Right ?? 0)) : drawing.Box.Width;
        foreach (var (index, (equation, _)) in equations)
            advances[index] = equation.Width;
        foreach (var (index, box) in boxes)
            advances[index] = BoxMetrics(box) is var (a, b) ? a + b : 0;
        foreach (var (index, ruby) in rubies)
        {
            var (baseWidth, rubyWidth) = RubyWidths(ruby);
            // Vertical ruby stands right of its base, one character wide.
            advances[index] = ruby.Align == "rightVertical"
                ? baseWidth + (ruby.Ruby.Count > 0 ? styles.Style(ruby.Ruby[0].Format, WordSlot.EastAsia).Size : 0)
                : Math.Max(baseWidth, rubyWidth);
        }
        foreach (var (index, run) in combined)
            advances[index] = Combination(run).Width;
        foreach (var (index, run) in across)
            advances[index] = Across(run, Vertical).Slot;
        foreach (var (index, continuation) in separators)
            advances[index] = continuation ? width - (format.Left ?? 0) - (format.Right ?? 0) : Math.Min(separatorWidth, width);
        // A number aligned right or centered ends or centers at the first line's indent (w:lvlJc).
        var labelShift = 0.0;
        var labelEnd = paragraph.Label?.Suffix == "space" ? labelLength + 1 : labelLength;
        var labelWhole = paragraph.Label?.Suffix is "tab" or "space" ? labelLength + 1 : labelLength;
        if (paragraph.Label is { Justification: "right" or "end" or "center" } label && labelLength > 0)
        {
            var labelWidth = advances.Take(labelLength).Sum();
            labelShift = label.Justification == "center" ? labelWidth / 2 : labelWidth;
        }
        var rules = context.Kinsoku(EastAsianLanguage(text, charStyles));
        var breaks = LineBreaker.Opportunities(text, format.Kinsoku ?? true, rules);
        BreakAtSpaces(text, breaks);
        BreakAfterHyphens(text, breaks, rules, labelWhole);
        BreakAroundObjects(text, breaks, rules, labelWhole);
        if (format.WordWrap == false)
            BreakWords(text, breaks, rules);
        // Punctuation that may not begin a line may hang past its end instead, where the paragraph lets it
        // (w:overflowPunct, on by default): before Word 2013 always, from then on in justified paragraphs (measured with
        // docx/local/kinsoku-chars12-*, kinsoku-chars14-ja-JP and kinsoku-chars15-*-both, distribute), where the paragraph
        // has East Asian text or its runs an East Asian language for it (docx/local/hangcomma-*: a comma after Latin words
        // goes down with its word, with a Japanese word in the paragraph it hangs; docx/external/poi/bug65649's Russian
        // footnotes; c3-lines12-* with ja-JP and docx/external/libreoffice/core-ooxml-pass/fdo63164-1 with zh-CN: after
        // Latin words; docx/external/powertools/DB007-Abstract, openxml-sdk/May_12_04 with en-US: not); from Word 2013 on
        // the language alone does not do (docx/local/hangcomma-h4, -h7: zh-CN and ja-JP for East Asian text, no hanging
        // comma; docx/external/libreoffice/extras-ooxmlexport/tdf163894). East Asian punctuation hangs only where the
        // document's default East Asian font is one (docx/local/hgr-*, hgd-*: a 。 after a full line of SimSun ideographs
        // in zh-CN hangs with SimSun as that font, with none it goes down with its ideograph whatever the room left;
        // extras-odfexport/tdf130314), Latin commas and periods without (fdo63164-1).
        var hanging = format.OverflowPunctuation != false && format.Kinsoku != false
            && (context.Profile.PunctuationHangsAnywhere || Justified(format))
            && (HasEastAsian(text) || context.Profile.PunctuationHangsAnywhere && HasEastAsianLanguage(paragraph)) ? rules.Hanging : "";
        if (!styles.EastAsianDefaults)
            hanging = string.Concat(hanging.Where(c => c < '\u2E80'));
        var hangAt = -1;
        // Where a closing mark hangs half its width past the line's end and whether the line squeezes its punctuation
        // (see CompressesPunctuation).
        var (halfHangAt, punctuationSqueezed) = (-1, false);
        var hyphens = Hyphenation(text, charStyles, format);
        var zone = context.Settings.Child("hyphenationZone").Twips("val") ?? 18;
        var hyphenLimit = (int)(context.Settings.Child("consecutiveHyphenLimit").Number("val") ?? 0);
        var hyphenatedLines = 0;
        // The last hyphenation place of the word [from, to) where its part before it and a hyphen fit the line; before
        // Word 2013 the part and the wider of the hyphen and the letter after the place (measured with docx/local/hys-14l,
        // hys-14j: Times New Roman 11 at 211 line widths, "inter|n" needing 1.83 pt more than "inter-"; hys-15l, hys-15j
        // the hyphen alone).
        int? Hyphenate(int from, int to, double startX, double lineLimit)
        {
            int? best = null;
            var px = startX;
            for (var k = from; k < to; k++)
            {
                var hyphen = k > from ? HyphenWidth(charStyles[k - 1]) : 0;
                if (context.Profile.HyphenTakesNextLetter)
                    hyphen = Math.Max(hyphen, advances[k]);
                if (k > from && hyphens![k] && px + hyphen <= lineLimit + 0.005)
                    best = k;
                px += advances[k];
            }
            return best;
        }
        var mark = styles.Mark(paragraph.MarkFormat);

        var left = format.Left ?? 0;
        var right = format.Right ?? 0;
        var firstLine = format.FirstLine ?? 0;
        positionalArea = (0, width, left, width - right);
        var lines = new List<Line>();
        // Justified lines may squeeze the spaces between their words to take one more word (see Squeezes); left
        // aligned lines take no word that does not fit (docx/local/jc-left); not before Word 2013 (measured with
        // docx/local/cjk-justify12: a line of Japanese words between spaces leaves the word that only fits squeezed);
        // not lines of left-to-right text with kashida justification (see KashidaShare).
        var squeezing = Justified(format) && context.Profile.SqueezesJustifiedLines && KashidaShare(format) == null;
        // So may justified lines with punctuation compression (w:characterSpacingControl compressPunctuation) by
        // squeezing their East Asian punctuation, in every mode (see CompressesPunctuation).
        var compressing = Justified(format) && context.Settings.Child("characterSpacingControl").Val() == "compressPunctuation";
        // The width of the content from a place to its next break opportunity, without the spaces there.
        double FirstWord(int from)
        {
            var k = from;
            var w = 0.0;
            while (k < text.Length && text[k] is not ('\t' or '\n' or '\f' or '\v'))
            {
                w += advances[k];
                k++;
                if (breaks[k - 1])
                    break;
            }
            while (k > from && Trails(text[k - 1]))
                w -= advances[--k];
            return w;
        }

        // Characters of the current line: [start, end) and where it starts.
        var lineStart = Math.Clamp(from, 0, text.Length);
        var first = lineStart == 0;
        var offset = 0.0;            // the line's top below the paragraph's first line top
        var estimate = (mark.Metrics.Above + mark.Metrics.Below) * (format.LineRule is null or "auto" ? (format.Line ?? 240) / 240 : 1);
        var textBand = (Top: 0.0, Bottom: mark.Metrics.Above + mark.Metrics.Below);
        string? clear = null;     // the w:clear of the break that ended the line before
        var remeasured = false;   // whether the line is laid out again with its own height (see below)
        var grown = false;        // whether the line is laid out again with the height it grew to (see below)
        var markWraps = false;    // whether the paragraph mark goes to a line of its own (see below)
        while (true)
        {
            var startX = left + (first ? firstLine - labelShift : 0);
            var limit = width - right;
            // The left indent as the hanging first line's tab stop (see the drop cap below).
            var tabLeft = left;
            var (naturalStart, naturalLimit) = (startX, limit);
            // Where the line's content has to end to fit: its limit, for kashida justification of left-to-right text
            // nearer (see KashidaShare).
            double Fit() => KashidaShare(format) is { } share ? startX + (limit - startX) * share : limit;
            // Floating objects beside the line: its segments within the indents, or it moves down where they leave it
            // none (see MinimumSegment). Before Word 2013 a line beside objects wrapped square stays where the column
            // has room enough beside them, also only outside its indents, and takes what of it they leave, a character
            // a line where they leave none, a word breaking where it does not fit (measured with docx/local/tx-*, ty-1,
            // ty-3 in modes 12 and 14: empty and text lines indented past 26.8 pt left of a floating table or a shape
            // stand at their place, one character a line, in mode 15 below it, ty-2; tz-1, ua-1, ua-3: "Parag" and
            // "raph" beside 23.3 pt, beside a shape wrapped through the line goes below, ua-2; docx/external/
            // powertools/RP/RP001-Tracked-Revisions-01-Accepted).
            var segments = new List<(double Start, double End)>();
            var skip = 0.0;
            var none = false;
            LineRoom? taken = null;
            if (room != null)
            {
                for (var attempt = 0; attempt < 400; attempt++)
                {
                    var r = taken = room(new LineSlot(offset + skip, estimate, textBand.Top, textBand.Bottom, clear));
                    // A right-to-left paragraph lays its lines out from the right: the room beside the objects mirrored,
                    // its rightmost part first (Visual mirrors the line back; measured with docx/local/rtw-14, rtw-15:
                    // Hebrew lines beside rectangles at the column's left and right edges and in its middle;
                    // docx/external/libreoffice/extras-odfexport/tdf163575).
                    if (format.Bidi == true)
                        r = r with { Segments = r.Segments.Select(s => (width - s.End, width - s.Start)).Reverse().ToList() };
                    var minimum = r.Tight && context.Profile.WrapRoomBeforeIndent ? MinimumTightSegment : MinimumSegment;
                    // Beside a drop cap the first line starts at the cap's room or its own start, whichever is further,
                    // a first-line indent past the room; before Word 2013 a first line indented or hanging moves by the
                    // room's whole width (measured with docx/local/dcn-*: a three-line cap 49.3 pt wide with hSpace before
                    // text unindented, at left 36 pt, numbered or hanging 18 pt at left 36 or 72 pt, or indented 18 pt
                    // first; mode 15 puts the hanging lines at the room, 18 pt indented 18 pt past it, modes before it
                    // every first line indented or hanging 49.3 pt right of its own start, the hanging indent's tab stop
                    // with it (dcn-b-12, c-12, g-12: the text after "1." 18 pt after it); docx/external/libreoffice/
                    // extras-ooxmlexport/tdf109308_dropCapNumbered).
                    var dropCap = first ? r.DropCap : null;
                    var legacyShift = dropCap is { } shiftBy && context.Profile.DropCapShiftsFirstLine && Math.Abs(firstLine) > 0.01 ? shiftBy : 0;
                    var indentX = dropCap is not { } capEdge ? startX
                        : !context.Profile.DropCapShiftsFirstLine ? firstLine > 0.01 ? Math.Max(capEdge, left) + firstLine : startX
                        : startX + legacyShift;
                    tabLeft = left + legacyShift;
                    if (context.Profile.WrapRoomBeforeIndent && !r.Tight)
                    {
                        var enough = r.Segments.Where(s => s.End - s.Start >= minimum).ToList();
                        segments = enough.Select(s => Indented(s, indentX, limit, width)).Where(s => s.Item2 - s.Item1 > 0.005).ToList();
                        none = segments.Count == 0 && enough.Count > 0;
                    }
                    else
                    {
                        segments = r.Segments.Select(s => Indented(s, indentX, limit, width)).Where(s => s.Item2 - s.Item1 >= minimum).ToList();
                        if (r.Segments.Count != 1 || r.Segments[0].End - r.Segments[0].Start < width - 0.01)
                        {
                            var word = FirstWord(lineStart);
                            while (segments.Count > 0 && segments[0].End - segments[0].Start < word - 0.005)
                                segments.RemoveAt(0);
                        }
                    }
                    if (segments.Count > 0 || none || r.SkipTo <= offset + skip + 0.01)
                        break;
                    // A first line pushed below objects keeps its paragraph's space before below them (measured with
                    // docx/local/fsb-*: 14 pt below a floating table anchored to the page or the text, in modes 14
                    // and 15; docx/external/powertools/RP/RP051).
                    skip = r.SkipTo - offset + (first ? PushedSpace : 0);
                }
                if (segments.Count > 0)
                {
                    startX = segments[0].Start;
                    limit = segments[0].End;
                }
                else if (none)
                    limit = startX;
            }
            // Punctuation compressed at the start of a line (w:topLinePunct): an East Asian opening bracket that begins
            // the line stands half its width further left (measured with docx/local/toplinepunct15 and toplinepunct12:
            // 「（『【〔 at 12 pt 6 pt left in modes 15 and 12, “ not).
            if (format.TopLinePunctuation == true && lineStart < text.Length && lineStart < charStyles.Count && OpeningPunctuation.Contains(text[lineStart]))
                startX -= charStyles[lineStart].Size / 2;
            var segment = 0;
            var jumps = new Dictionary<int, double>();
            var x = startX;
            var spaces = 0.0;          // width of the spaces on the line so far
            var end = lineStart;
            var lastFit = -1;          // end of the text that fits, at a break opportunity
            var endBreak = (string?)null;
            var i = lineStart;
            var extended = false;      // whether a tab stop past the right indent took the line further (see below)
            HashSet<int>? overTabs = null;    // tabs to stops past the right indent, where the line does not break
            var wordEnds = new List<int>();   // where the line's words end, its break opportunities
            if (none && lineStart < text.Length)
            {
                end = lineStart + (char.IsHighSurrogate(text[lineStart]) && lineStart + 1 < text.Length ? 2 : 1);
                lastFit = end;
                i = text.Length;
            }
            while (i < text.Length)
            {
                var c = text[i];
                if (c is '\n' or '\f' or '\v')
                {
                    end = i + 1;
                    endBreak = c switch { '\n' => "line", '\f' => "page", _ => "column" };
                    lastFit = end;
                    break;
                }
                // A horizontal rule stands on a line of its own, with the break after it (Vml.Rule).
                if (objects.TryGetValue(i, out var ruled) && RuleShare(ruled) != null)
                {
                    end = HasContent(text, lineStart, i) ? i : i + 1;
                    if (end == i + 1 && end < text.Length && text[end] is '\n' or '\f' or '\v')
                        endBreak = text[end++] switch { '\n' => "line", '\f' => "page", _ => "column" };
                    lastFit = end;
                    break;
                }
                if (c == '\t')
                {
                    // An absolute position tab whose place lies behind what the line holds before it starts the next
                    // line, where it takes the text to its place; at its place or before it nothing breaks (measured
                    // with docx/local/ptb-la, ptb-li, ptb-cp: left at the margin or the indent after "A", centered after
                    // text past the middle; ptb-l0, ptb-l0i, ptb-lr: at the line's start; docx/external/libreoffice/
                    // writerfilter-dmapper/ptab: after a space, the page number a line lower).
                    if (positional.ContainsKey(i) && i > lineStart && PositionalPlace(text, i, advances, out _) < x - 0.005)
                    {
                        end = i;
                        lastFit = i;
                        break;
                    }
                    var to = TabStop(format, x, text, i, charStyles, advances, tabLeft, first ? firstLine : 0, limit, out var stop);
                    // A custom stop at or past the right indent holds: from Word 2013 on where it is not past the
                    // column's edge, the line then reaching up to that edge; before where it is past it too, the line
                    // then reaching 22 in (measured with docx/local/tov-a to tov-i, tovc-1 to tovc-12: left, right,
                    // centered and decimal stops past the indent of paragraphs and cells, in modes 15, 14 and 12; Word's
                    // line ends 1582 pt into the column in mode 14; tv-a to tv-d: stops at the indent, text after them
                    // reaching past it; docx/external/libreoffice/extras-ooxmlexport/tdf77417). A default stop past it
                    // does not, a line taken further keeps them up to its new end; from Word 2013 on the line does not
                    // break next to tabs past the indent, a word that does not fit after them breaking inside where it
                    // has no other place ("Left\taf", "ter"); justification fills the line to its new end.
                    var ownLimit = width - right;
                    if (!extended && stop != null && !positional.ContainsKey(i) && stop.Position >= limit - 0.005
                        && segments.Count <= 1 && Math.Abs(limit - ownLimit) < 0.01
                        && (context.Profile.TabStopsPastEdge || stop.Position <= Math.Max(width, ownLimit) + 0.005))
                    {
                        limit = context.Profile.TabStopsPastEdge ? MaximumLine : Math.Max(width, ownLimit);
                        extended = true;
                        to = TabStop(format, x, text, i, charStyles, advances, tabLeft, first ? firstLine : 0, limit, out stop);
                    }
                    if (extended && !context.Profile.TabStopsPastEdge && (stop?.Position ?? to) > ownLimit + 0.005)
                        (overTabs ??= []).Add(i);
                    if (to > limit + 0.005)
                    {
                        // A tab whose stop lies past the right margin starts the next line; at a line's start it fills
                        // the line and its text follows on the next (measured with docx/local/tab-break); a space before
                        // it at the paragraph's start keeps the first line for itself (docx/local/lke-sp: " ", a tab and a
                        // picture in a box 15.2 pt wide, the tab and the picture on one line of the next linked box), one
                        // after a tab that filled a line not (dts-31680: " " between tabs to stops past the margin).
                        if (HasContent(text, lineStart, i) || first && i > lineStart)
                        {
                            end = i;
                            lastFit = i;
                            break;
                        }
                        i++;
                        end = i;
                        lastFit = i;
                        break;
                    }
                    x = to;
                    i++;
                    end = i;
                    continue;
                }
                // Content up to the next break opportunity: it fits when its end (without trailing spaces) stays inside.
                var wordEnd = i;
                var w = x;
                while (wordEnd < text.Length && text[wordEnd] is not ('\t' or '\n' or '\f' or '\v'))
                {
                    w += advances[wordEnd];
                    wordEnd++;
                    if (breaks[wordEnd - 1])
                        break;
                }
                var contentEnd = wordEnd;
                var contentX = w;
                while (contentEnd > i && Trails(text[contentEnd - 1]))
                {
                    contentEnd--;
                    contentX -= advances[contentEnd];
                }
                // A line ending at a soft hyphen shows a hyphen, which has to fit too (catalog: run.content).
                if (contentEnd > i && text[contentEnd - 1] == '\u00AD')
                    contentX += HyphenWidth(charStyles[contentEnd - 1]);
                var innerSpaces = spaces;
                for (var k = i; k < contentEnd; k++)
                {
                    if (text[k] == ' ')
                        innerSpaces += advances[k];
                }
                // Where the line would end without the word, and its spaces then, which justification stretches.
                var (shortEnd, shortSpaces) = (x, spaces);
                for (var k = i; k > lineStart && Trails(text[k - 1]); k--)
                {
                    shortEnd -= advances[k - 1];
                    shortSpaces -= balanced.Contains(k - 1) ? 0 : advances[k - 1];
                }
                bool Takes(double contentEndX) => contentEndX <= Fit() + 0.005
                    || squeezing && Squeezes(contentEndX - Fit(), innerSpaces, Fit() - shortEnd, shortSpaces);
                // The automatic space after the content's last character goes where the line ends there.
                // With punctuation compression a closing mark ending the content may hang half its width past the end:
                // where that alone is enough, rather than the spaces squeezing for it; where they may not squeeze for all
                // of it, it hangs half and they squeeze for the rest (measured with docx/local/cpv-b-c: Latin words and
                // "可从页看起，" in lines 0.5 pt apart, the comma 5.8 pt over hangs half, 6.3 to 7.8 pt over it stands
                // inside, the nine spaces squeezed by up to 24 %, 8.3 to 12.8 pt over it hangs half again).
                var endX = contentX - EndSpace(contentEnd - 1);
                var halfMark = compressing && segments.Count <= 1 && contentEnd > i ? HalfMark(text, advances, charStyles, contentEnd - 1) : 0;
                var fits = !(halfMark > 0 && endX > Fit() + 0.005 && endX - halfMark <= Fit() + 0.005) && Takes(endX);
                if (!fits && halfMark > 0 && Takes(endX - halfMark))
                    (fits, halfHangAt) = (true, contentEnd - 1);
                if (!fits && compressing && segments.Count <= 1 && contentEnd > i
                    && CompressesPunctuation(text, advances, charStyles, lineStart, i, contentEnd, contentX - EndSpace(contentEnd - 1) - Fit(),
                        Fit() - shortEnd, context.Profile.CompressionByGaps) is { } compressed)
                {
                    fits = true;
                    (halfHangAt, punctuationSqueezed) = compressed;
                }
                // A word that does not fit the segment beside a floating object continues in the next segment.
                if (!fits && segment + 1 < segments.Count)
                {
                    segment++;
                    jumps[i] = segments[segment].Start;
                    x = segments[segment].Start;
                    limit = segments[segment].End;
                    continue;
                }
                if (!fits && hanging.Length > 0 && contentEnd - 1 > i && hanging.Contains(text[contentEnd - 1])
                    && Takes(contentX - advances[contentEnd - 1]))
                {
                    // The word fits but for its last character, which hangs past the end.
                    hangAt = contentEnd - 1;
                    fits = true;
                }
                // A tab goes to the next line with the word after it, the line breaking before it (measured with
                // docx/local/lw-6 to lw-10, ly-1, ly-2, ly-4: in modes 14 and 15); one that begins the line keeps the word,
                // which breaks inside then (lx-1, ly-6, lz-5), as after nothing but the list number and its tab or space
                // (lw-2, lx-2, lx-4; docx/external/libreoffice/extras-ooxmlexport/tdf123054).
                var breakAt = end == i && end > lineStart && text[end - 1] == '\t' ? end - 1 : end;
                if (!fits && overTabs != null && (overTabs.Contains(breakAt) || overTabs.Contains(breakAt - 1)))
                    breakAt = wordEnds.LastOrDefault(e => e <= breakAt && !overTabs.Contains(e) && !overTabs.Contains(e - 1), -1);
                // Spaces alone before it do too: they stay on a line of their own (measured with docx/local/spic-*: a
                // space or two before a picture as wide as the column or a word wider than it; pandoc/image).
                if (!fits && breakAt > lineStart && breakAt > labelWhole)
                {
                    // Automatic hyphenation (measured with docx/local/hyph-a to -d and hyph-fit): the word that does not
                    // fit breaks at its last place where its part and a hyphen fit; justified lines may squeeze their
                    // spaces by 23 % for it (between 21.9 %, which Word takes, and 25.2 %, which it does not); other
                    // lines break so only where they would end further than the hyphenation zone from their end: 0.25
                    // in from Word 2013 on, whatever w:hyphenationZone says (zones of 0, 0.25 and 1 in break alike),
                    // w:hyphenationZone before.
                    if (hyphens != null && (hyphenLimit <= 0 || hyphenatedLines < hyphenLimit) && !(unhyphenated && lineStart == from))
                    {
                        var lineEnd = x;
                        for (var k = end; k > lineStart && text[k - 1] == ' '; k--)
                            lineEnd -= advances[k - 1];
                        var justified = Justified(format) && context.Profile.SqueezesJustifiedLines;
                        var threshold = context.Profile.FixedHyphenationZone ? 18 : zone;
                        if ((justified || Fit() - lineEnd > threshold + 0.005) && Hyphenate(i, contentEnd, x, Fit() + (justified ? 0.23 * spaces : 0)) is { } at)
                        {
                            end = at;
                            lastFit = at;
                            autoHyphenAt = at;
                            break;
                        }
                    }
                    end = breakAt;
                    break;   // the word goes to the next line
                }
                if (!fits && hyphens != null && Hyphenate(i, contentEnd, x, Fit()) is { } inWord)
                {
                    // A word longer than the line breaks at its last hyphenation place that fits.
                    end = inWord;
                    lastFit = inWord;
                    autoHyphenAt = inWord;
                    break;
                }
                if (!fits)
                {
                    // A word longer than the line: as many characters as fit, at least one; after a tab that begins the
                    // line none need, the word then following on the next (measured with docx/local/lt-1: a tab to 6 in,
                    // then "WWW" at 36 pt; docx/external/libreoffice/extras-ooxmlexport/tdf142404_tabOverMarginC15), but
                    // after the list number's tab (lt-3: a shape wider than the room beside a bullet stays beside it).
                    var k = i;
                    var kx = x;
                    while (k < contentEnd && (k == i && (breakAt == end || breakAt == labelTab) || kx + advances[k] <= Fit() + 0.005))
                    {
                        kx += advances[k];
                        k++;
                    }
                    // Spaces after the whole of it stay on the line (lt-11: a shape wider than the room, then a space).
                    end = k == contentEnd ? wordEnd : k;
                    lastFit = k;
                    x = kx;
                    break;
                }
                x = w;
                for (var k = i; k < wordEnd; k++)
                {
                    // Balanced spaces do not squeeze (measured with docx/local/c2-bsbdb).
                    if (text[k] == ' ' && !balanced.Contains(k))
                        spaces += advances[k];
                }
                end = wordEnd;
                lastFit = wordEnd;
                wordEnds.Add(wordEnd);
                i = wordEnd;
            }
            if (end == lineStart && i >= text.Length)
                end = text.Length;
            // Every line takes at least a character: one that starts at spaces where the indents leave no room at all
            // (docx/external/libreoffice/extras-ooxmlexport/tdf124604) would else stand still.
            if (end <= lineStart && lineStart < text.Length)
                end = lineStart + (char.IsHighSurrogate(text[lineStart]) && lineStart + 1 < text.Length ? 2 : 1);
            if (lastFit < 0)
                lastFit = end;

            var line = BuildLine(text, charStyles, advances, lineStart, end, startX, format, tabLeft, first ? firstLine : 0, mark, labelEnd, limit, jumps);
            // A line a stop past the indent took further is justified up to its new end where it wraps, not where it
            // ends in a line break (measured with docx/local/tov-g, jz-a to jz-d: "a b c" after a left stop past the
            // indent keeps its spaces before a line break in modes 14 and 15, after a stop inside the indents it fills
            // the line).
            if (extended)
                (line.Limit, line.Natural) = (limit, endBreak == "line");
            if (hangAt >= lineStart && hangAt < end && !HasContent(text, hangAt + 1, end))
                line.Hanging = advances[hangAt];
            else if (halfHangAt >= lineStart && halfHangAt < end && !HasContent(text, halfHangAt + 1, end))
                (line.Hanging, line.HangsHalf) = (advances[halfHangAt] / 2, true);
            line.PunctuationSqueezed = punctuationSqueezed;
            DropEndSpace(line, text, end);
            hangAt = -1;
            (halfHangAt, punctuationSqueezed) = (-1, false);
            var hyphenatedBefore = hyphenatedLines;
            hyphenatedLines = autoHyphenAt == end ? hyphenatedLines + 1 : 0;
            autoHyphenAt = -1;
            line.EndBreak = endBreak;
            line.Skip = skip;
            if (anchored.Where(a => a.Index >= lineStart && (a.Index < end || end >= text.Length)).Select(a => a.Anchor).ToList() is { Count: > 0 } inLine)
                line.Anchors = inLine;
            if (room != null)
            {
                // The line's height decides where the next one stands beside the objects.
                Space([line], format, Pitch(format), context.Profile.ExactFromTop);
                // A line moved below objects for the height its paragraph mark gives stands where it fits with its own
                // (docx/external/libreoffice/uitest/tdf124675: a line just above a floating table, its mark taller).
                if (skip > 0.01 && !remeasured && line.Height < estimate - 0.01
                    && room(new LineSlot(offset, line.Height, line.Baseline - line.Above, line.Baseline + line.Below, clear)) is { } own
                    && own.Segments.Count == 1 && own.Segments[0].Start <= 0.01 && own.Segments[0].End >= width - 0.01)
                {
                    estimate = line.Height;
                    textBand = (line.Baseline - line.Above, line.Baseline + line.Below);
                    hyphenatedLines = hyphenatedBefore;
                    remeasured = true;
                    continue;
                }
                // From Word 2013 on a line taller than the height it was placed for keeps clear of the objects beside
                // the rest of it too, laid out again with its own height (measured with docx/local/tall-*: a 200 pt
                // inline picture whose line top is clear moves below a shape over its lower or upper part, also one
                // wrapped top and bottom, anchored before or after it; beside one leaving it room enough it stays; in
                // mode 14 it stays over the shape; openxml-sdk/Document: a cover page's text box over a chart's lower
                // half).
                if (context.Profile.TallLinesClearObjects && !grown && line.Height > estimate + 0.01 && taken != null
                    && room(new LineSlot(offset + skip, line.Height, line.Baseline - line.Above, line.Baseline + line.Below, clear)) is { } whole
                    && !SameRoom(whole, taken))
                {
                    estimate = line.Height;
                    textBand = (line.Baseline - line.Above, line.Baseline + line.Below);
                    hyphenatedLines = hyphenatedBefore;
                    grown = true;
                    continue;
                }
                grown = false;
                remeasured = false;
                offset += skip + line.Height;
                estimate = line.Height;
                textBand = (line.Baseline - line.Above, line.Baseline + line.Below);
                (line.Start, line.Limit) = (startX, segments.Count > 1 ? segments[^1].End : limit);
                if (segments.Count > 1 || segments.Count == 1 && (segments[0].Start > naturalStart + 0.01 || segments[0].End < naturalLimit - 0.01))
                    line.Cut = segments.Select(s => (Math.Abs(s.Start - naturalStart) < 0.01 ? Math.Min(left, naturalStart) : s.Start, s.End)).ToList();
            }
            // Before Word 2013 the mark after a field's result wider than its line goes to the next line, where after a
            // drawing alone it stands past the line's end (measured with docx/local/owf-11, owf-12, owf-14: a rectangle
            // 456.3 pt wide in a 451.3 pt column as a complex or simple field's result or in a hyperlink; owf-15 and
            // wio-*: the mark beside it; Apache POI's WordWithAttachments: an OLE object, an EMBED field, 468 pt wide in
            // a column of 467.75 pt, not one 467.75 pt wide).
            markWraps = context.Profile.MarkWrapsAfterWideFields && end >= text.Length && endBreak == null && x > limit + 0.005
                && objects.TryGetValue(text.Length - 1, out var lastObject) && lastObject.InField;
            lines.Add(line);
            first = false;
            clear = endBreak == "line" && clears.TryGetValue(end - 1, out var side) ? side : null;
            lineStart = end;
            if (lineStart >= text.Length)
                break;
        }
        if (lines.Count == 0)
            lines.Add(BuildLine(text, charStyles, advances, 0, 0, left + firstLine - labelShift, format, left, firstLine, mark, labelEnd, width - right));
        // A paragraph that ends with a break has an empty last line, after a column break in the next column or page
        // (measured with docx/local/breakmark3-cx-*, breakmark4-cl-*, -c2-*: in all compatibility modes); one that
        // ends with a page break too where its mark goes to the next page.
        if ((lines[^1].EndBreak is "line" or "column" || lines[^1].EndBreak is "page" && context.Profile.SplitPageBreak) && lineStart >= text.Length
            || markWraps)
            lines.Add(BuildLine(text, charStyles, advances, text.Length, text.Length, left, format, left, 0, mark, 0, width - right));
        lines[^1].Last = true;

        var expandBreaks = context.Profile.ExpandsLineBreaks;
        // How justification may spread a line between characters (measured with docx/local/cjk-justify-lines,
        // cjk-justify-western and docx/generated/paragraph-alignment): justified lines with East Asian text where the
        // document's default East Asian font is one (elsewhere their spaces only, East Asian text not at all);
        // distributed lines between all characters where the paragraph has East Asian text or the document such a
        // font (elsewhere their spaces).
        var hasEastAsian = text.Any(EastAsian);
        var characters = format.Justification is "distribute" or "thaiDistribute"
            ? hasEastAsian || styles.EastAsianDefaults ? Spread.All : Spread.None
            : styles.EastAsianDefaults ? Spread.EastAsian : Spread.None;
        foreach (var line in lines)
        {
            // Lines broken around floating objects align in their room; a line in several segments aligns each of them
            // in its own, justified ones all but the paragraph's last (measured with docx/local/jf-*: justified lines
            // beside a frame and beside rectangles wrapped square or tight end each segment at its room's end, centred
            // and right aligned ones centre or end each; docx/external/libreoffice/extras-ooxmlexport/tdf133701).
            if (line.Jumped && !Unaligned && line.Cut is { Count: > 1 } cut)
            {
                var groups = new List<List<Piece>> { new() };
                foreach (var piece in line.Pieces)
                {
                    if (piece.IsTab && piece.Start < 0)
                        groups.Add([]);
                    else
                        groups[^1].Add(piece);
                }
                var lastGroup = groups.FindLastIndex(g => g.Count > 0);
                for (var g = 0; g <= lastGroup; g++)
                {
                    if (groups[g].Count == 0)
                        continue;
                    var x0 = groups[g][0].X;
                    var k = Math.Max(0, cut.FindLastIndex(s => s.Start <= x0 + 0.01));
                    var segmentStart = g == 0 ? line.Start ?? left + (line.First ? firstLine - labelShift : 0) : cut[k].Start;
                    var segmentEnd = k == cut.Count - 1 ? line.Limit ?? (AlignIn is { } inBox && Math.Abs(width - inBox.Laid) < 1e-6 ? inBox.Area : width) - right
                        : cut[k].End;
                    var part = new Line { First = line.First && g == 0, Last = line.Last && g == lastGroup, Natural = line.Natural,
                        EndBreak = g == lastGroup ? line.EndBreak : null, Hanging = g == lastGroup ? line.Hanging : 0 };
                    part.Pieces.AddRange(groups[g]);
                    Align(part, format, segmentEnd, segmentStart, expandBreaks, Doubled, characters, segmentEnd, context.Profile.IdeographicTrails,
                        context.Profile.DistributedSpaceShare, phased: context.Profile.PhasedDistribution);
                }
                continue;
            }
            if (line.Jumped || Unaligned)
                continue;
            // A display equation's line stands where its justification puts it, from the left indent.
            if (line.Pieces.FirstOrDefault(p => p.DisplayX != null) is { DisplayX: { } displayX } displayed)
            {
                displayed.X = left + displayX;
                continue;
            }
            var alignEnd = (AlignIn is { } box && Math.Abs(width - box.Laid) < 1e-6 ? box.Area : width) - right;
            Align(line, format, line.Limit ?? alignEnd, line.Start ?? left + (line.First ? firstLine - labelShift : 0), expandBreaks, Doubled, characters, alignEnd,
                context.Profile.IdeographicTrails, context.Profile.DistributedSpaceShare, snapCell, context.Profile.PhasedDistribution);
            // Horizontal rules take their share of the column's width, aligned in the paragraph's text area.
            foreach (var piece in line.Pieces.Where(p => p.Drawing?.Drawing.Attribute(Vml.Rule) != null))
            {
                var share = RuleShare(piece.Drawing!) ?? 1;
                var ruleWidth = share > 0 ? share * (ColumnWidth?.Invoke() ?? width - right - left) : piece.Drawing.Box.Width;
                piece.Rule = (ruleWidth, piece.Rule?.Bottom ?? 0);
                piece.X = (string?)piece.Drawing.Drawing.Attribute(Vml.RuleAlign) switch
                {
                    "center" => left + (width - right - left - ruleWidth) / 2,
                    "right" => width - right - ruleWidth,
                    _ => left,
                };
            }
        }
        // Right-to-left paragraphs and runs: the lines, laid out from the start edge, read in visual order.
        if (levels != null || format.Bidi == true)
        {
            foreach (var line in lines)
                Visual(line, width, format.Bidi == true);
        }
        // Where the editable ranges begin and end in their lines: at the character after their start, after the
        // character before their end.
        if (permissions.Count > 0)
        {
            var starts = lines.Select(l => l.Pieces.Where(p => p.Start >= 0).Select(p => p.Start).DefaultIfEmpty(int.MaxValue).Min()).ToList();
            foreach (var (index, start) in permissions)
            {
                var k = starts.FindLastIndex(s => s <= index);
                if (k < 0)
                    k = 0;
                (lines[k].Brackets ??= []).Add((CharacterX(lines[k], index), start));
            }
        }
        // Formatting changes of hidden text, where it would stand.
        if (changeMarks.Count > 0)
        {
            var starts = lines.Select(l => l.Pieces.Where(p => p.Start >= 0).Select(p => p.Start).DefaultIfEmpty(int.MaxValue).Min()).ToList();
            foreach (var (index, change) in changeMarks)
            {
                var k = Math.Max(0, starts.FindLastIndex(s => s <= index));
                (lines[k].HiddenChanges ??= []).Add((CharacterX(lines[k], index), change));
            }
        }
        // Where comments' ranges begin and end in their lines: a range begins at the character after its start and
        // ends after the character before its end.
        if (commentMarks.Count > 0)
        {
            var starts = lines.Select(l => l.Pieces.Where(p => p.Start >= 0).Select(p => p.Start).DefaultIfEmpty(int.MaxValue).Min()).ToList();
            foreach (var (index, id, start) in commentMarks)
            {
                var k = start ? starts.FindLastIndex(s => s <= index) : starts.FindLastIndex(s => s < index);
                if (k < 0)
                    k = 0;
                (lines[k].Comments ??= []).Add((CharacterX(lines[k], index), id, start));
            }
        }
        // Where the drawings are anchored in their lines, once the lines are aligned: at the start of the character
        // before the anchor (measured with docx/local/anchor-char: after a space, a letter or a tab).
        foreach (var line in lines)
        {
            foreach (var anchor in line.Anchors ?? [])
            {
                var index = anchored.First(a => ReferenceEquals(a.Anchor, anchor)).Index;
                (line.AnchorX ??= new(ReferenceEqualityComparer.Instance))[anchor] = CharacterX(line, index - 1);
            }
        }
        Space(lines, format, Pitch(format), context.Profile.ExactFromTop);
        if (format.Tabs?.Where(t => t.Align == "bar").Select(t => t.Position).ToList() is { Count: > 0 } bars)
        {
            foreach (var line in lines)
                line.Bars = bars;
        }
        return lines;
    }

    /// <summary>
    /// The narrowest and the widest a paragraph can be laid out (catalog: table.width): its longest piece between
    /// break opportunities and its longest line without wrapping, each with its indents.
    /// </summary>
    /// <param name="pictures">
    /// Whether pictures and boxes count with their width (a frame's width); table autofit leaves them out as Word does
    /// (docx/external/powertools/WC/WC023-Table-4-Row-Image-Before: a picture wider than its cell widens no column).
    /// </param>
    /// <param name="empty">Whether a paragraph without content needs its indents; in table cells it needs nothing
    /// (measured with docx/local/agr-text, agr-firsttext: an empty paragraph indented 144 pt from the left or by 14.4 pt
    /// on its first line beside "x" leaves both columns as without the indents).</param>
    public (double Min, double Max) Widths(ParagraphBlock paragraph, PageFields? fields = null, bool pictures = false, bool empty = true, bool deleted = false)
    {
        var format = paragraph.Format;
        displayWidth = double.MaxValue;
        measuringDeleted = deleted;
        var (text, charStyles, labelLength) = Flatten(paragraph, fields);
        measuringDeleted = false;
        if (!empty && text.Length == 0)
            return (0, 0);
        var advances = Advances(text, charStyles, CharacterExtra, CharacterExtraEastAsian);
        SuffixSpace(paragraph, labelLength, advances);
        if (balanced.Count > 0)
            Balance(text, charStyles, advances, balanced);
        if (context.Settings.Child("noPunctuationKerning").OnOff() != true)
            KernPunctuation(text, charStyles, advances);
        BorderRoom(text, charStyles, advances);
        FitText(text, charStyles, advances);
        VerticalQuotes(text, charStyles, advances);
        // Pictures and boxes take their width where asked, as in Layout (a frame as wide as a picture and a word would
        // else break the word: docx/external/libreoffice/core-ooxml-pass/fdo78904_fileHang).
        if (pictures)
        {
            foreach (var (index, drawing) in objects)
                advances[index] = RuleShare(drawing) > 0 ? 0 : drawing.Box.Width;
            foreach (var (index, box) in boxes)
                advances[index] = BoxMetrics(box) is var (a, b) ? a + b : 0;
        }
        foreach (var (index, (equation, _)) in equations)
            advances[index] = equation.Width;
        var rules = context.Kinsoku(EastAsianLanguage(text, charStyles));
        var breaks = LineBreaker.Opportunities(text, format.Kinsoku ?? true, rules);
        BreakAtSpaces(text, breaks);
        BreakAfterHyphens(text, breaks, rules, paragraph.Label?.Suffix is "tab" or "space" ? labelLength + 1 : labelLength);
        BreakAroundObjects(text, breaks, rules, paragraph.Label?.Suffix is "tab" or "space" ? labelLength + 1 : labelLength);
        if (format.WordWrap == false)
            BreakWords(text, breaks, rules);
        var left = format.Left ?? 0;
        var right = format.Right ?? 0;
        var firstLine = format.FirstLine ?? 0;
        double min = 0, max = 0, word = 0, trailing = 0;
        var line = left + firstLine;
        var interval = context.DefaultTabStop;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '\n' or '\f' or '\v')
            {
                max = Math.Max(max, line - trailing);
                min = Math.Max(min, word);
                (line, word, trailing) = (left, 0, 0);
                continue;
            }
            if (c == '\t')
            {
                // The next custom stop, else the next default stop.
                var stop = format.Tabs?.FirstOrDefault(t => t.Position > line + 0.005 && t.Align is not ("bar" or "clear"));
                line = stop?.Position ?? (interval > 0 ? (Math.Floor(line / interval + 1e-6) + 1) * interval : line);
                min = Math.Max(min, word);
                (word, trailing) = (0, 0);
                continue;
            }
            line += advances[i];
            if (c == ' ')
                trailing += advances[i];
            else
            {
                word += advances[i];
                trailing = 0;
            }
            if (breaks[i])
            {
                min = Math.Max(min, word);
                word = 0;
            }
        }
        max = Math.Max(max, line - trailing);
        min = Math.Max(min, word);
        // A paragraph of nothing but horizontal rules of a share of the width is an inch wide (Vml.Rule).
        if (pictures && objects.Count == text.Length && text.Length > 0 && objects.Values.All(o => RuleShare(o) > 0))
            (min, max) = (Math.Max(min, 72), Math.Max(max, 72));
        // A negative right indent does not shrink the minimum, a negative left one does (measured with docx/local/negind:
        // a word of 49.3 pt with indents of -5.65 pt on both sides or on the left only makes a column 5.65 pt narrower
        // than without; docx/external/poi/bug65649).
        return (min + left + Math.Max(0, firstLine) + Math.Max(0, right), max + right);
    }

    /// <summary>A horizontal rule's share of the section's text width (Vml.Rule), 0 for one of its style's width; null for other drawings.</summary>
    static double? RuleShare(DrawingInline drawing) =>
        (string?)drawing.Drawing.Attribute(Vml.Rule) is { } share && double.TryParse(share, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>Whether two rooms give a line the same segments.</summary>
    /// <summary>
    /// A segment of a line's room within its indents; one that reaches the column's edge goes on to an indent beyond
    /// it (docx/external/libreoffice/extras-ooxmlimport/tdf117843: a header's line indented 7.1 pt into the margin
    /// beside text boxes wrapped square starts there).
    /// </summary>
    static (double, double) Indented((double Start, double End) segment, double startX, double limit, double width) =>
        (segment.Start <= 0.01 ? startX : Math.Max(segment.Start, startX), segment.End >= width - 0.01 ? limit : Math.Min(segment.End, limit));

    static bool SameRoom(LineRoom a, LineRoom b) =>
        a.Tight == b.Tight && a.Segments.Count == b.Segments.Count
        && a.Segments.Zip(b.Segments).All(p => Math.Abs(p.First.Start - p.Second.Start) < 0.01 && Math.Abs(p.First.End - p.Second.End) < 0.01);

    static bool HasContent(string text, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (text[i] != ' ')
                return true;
        }
        return false;
    }

    /// <summary>The paragraph's text with a style per character: tabs \t, line breaks \n, page breaks \f, column breaks \v.</summary>
    /// <returns>The text, a style per character and how many characters the list number and its suffix take.</returns>
    /// <summary>Whether Flatten takes tracked deleted text too (see RunFormat.Deleted).</summary>
    bool measuringDeleted;

    /// <summary>
    /// The space after a list number (w:suff space) Word sets as wide as Arial's at the number's size, bold and italic
    /// as it is, while the number's font keeps setting the line's height (measured with docx/local/sfy-*, sfx-*: after
    /// numbers in Times New Roman, Courier New, Verdana and Cambria, East Asian numbers with any hint, theme and explicit
    /// fonts, in modes 14 and 15, Word's PDFs draw it in Arial; sfy-lab: a 14 pt Courier New number's line as high as
    /// Courier New's).
    /// </summary>
    void SuffixSpace(ParagraphBlock paragraph, int labelLength, double[] advances)
    {
        if (paragraph.Label is not { Suffix: "space" } label || labelLength >= advances.Length)
            return;
        var arial = label.Format.Clone();
        (arial.Ascii, arial.HAnsi, arial.EastAsia, arial.ComplexScript) = ("Arial", "Arial", "Arial", "Arial");
        (arial.AsciiTheme, arial.HAnsiTheme, arial.EastAsiaTheme, arial.ComplexScriptTheme) = (null, null, null, null);
        advances[labelLength] = Advances(" ", [styles.Style(arial, WordSlot.Ascii)], CharacterExtra, CharacterExtraEastAsian)[0];
    }

    (string Text, List<RunStyle> Styles, int LabelLength) Flatten(ParagraphBlock paragraph, PageFields? fields)
    {
        var text = new System.Text.StringBuilder();
        var list = new List<RunStyle>();
        levels = null;
        objects.Clear();
        boxes.Clear();
        rubies.Clear();
        combined.Clear();
        across.Clear();
        balanced.Clear();
        equations.Clear();
        anchored.Clear();
        positional.Clear();
        permissions.Clear();
        commentMarks.Clear();
        changeMarks.Clear();
        var breakAfterDisplay = false;
        links.Clear();
        notes.Clear();
        separators.Clear();
        clears.Clear();

        void AddText(string value, RunFormat format, string? link = null, bool number = false, bool noteMark = false)
        {
            // Medium and high kashida justification lengthen every Arabic word by two or three kashidas.
            if (paragraph.Format.Justification is "mediumKashida" or "highKashida")
                value = Kashidas(value, paragraph.Format.Justification == "mediumKashida" ? 2 : 3);
            if (link != null)
                links[text.Length] = (link, value.Length);
            if (format.Vanish == true && !(measuringDeleted && format.Deleted))
            {
                // Hidden text is not printed (Word's PDF export leaves it out); its formatting change keeps its balloon.
                if (format.Change is { } hiddenChange)
                    changeMarks.Add((text.Length, hiddenChange));
                return;
            }
            // A list number's spaces keep their width (docx/external/libreoffice/extras-ooxmlexport/chtoutline: "第 1 章"
            // in Cambria's spaces).
            if (context.Profile.BalanceSpaces && !number && value.Contains(' ') && styles.Style(format, WordSlot.EastAsia).Font.Data.EastAsian)
            {
                for (var k = 0; k < value.Length; k++)
                {
                    if (value[k] == ' ')
                        balanced.Add(text.Length + k);
                }
            }
            if (format.AcrossLine == true && value.Length > 0)
            {
                across[text.Length] = new TextInline(value, format);
                text.Append('\uFFFC');
                list.Add(styles.Character(format, char.ConvertToUtf32(value, 0)));
                return;
            }
            if (format.CombineLines == true && value.Length > 1)
            {
                combined[text.Length] = new TextInline(value, format);
                text.Append('\uFFFC');
                list.Add(styles.Style(format, WordSlot.EastAsia));
                return;
            }
            // All caps shows capitals; small caps shows lowercase letters as smaller capitals, the spaces between them
            // at that size too, a long s as itself (measured with docx/local/smallcaps-chars: digits, punctuation,
            // symbols, no-break spaces and tabs keep the text's size).
            var small = format.SmallCaps == true && format.Caps != true;
            RunStyle? previous = null;
            var runes = value.EnumerateRunes().ToList();
            for (var r = 0; r < runes.Count; r++)
            {
                var rune = runes[r];
                var lower = small && (Rune.IsLower(rune) || rune.Value == ' ');
                var shown = format.Caps == true || lower && rune.Value != 0x17F ? rune.Value == 0x131 ? new Rune('I') : Rune.ToUpperInvariant(rune) : rune;
                var style = styles.Character(format, shown.Value, lower, number ? paragraph.Format.Bidi == true : null);
                // A note's mark in Hebrew or Arabic letters takes the complex script font at the run's own size (Word's
                // PDF of docx/external/libreoffice/extras-ooxmlexport/tdf141231_arabicHebrewNumbering: abjad and Hebrew
                // marks of Calibri runs in the theme's Arial at 15.5 pt, not at w:szCs; text of such runs keeps Calibri,
                // docx/local/arf-15).
                if (noteMark && FreeSlate.Text.Bidi.Classify(shown.Value) is FreeSlate.Text.Bidi.Type.R or FreeSlate.Text.Bidi.Type.AL)
                    style = styles.Style(format, WordSlot.ComplexScript, lower, complex: false);
                if (noteMark)
                    style = style with { NoteMark = true };
                if (shown.Value == '\\' && context.Profile.YenBackslash && format.LanguageEastAsia?.StartsWith("ja", StringComparison.OrdinalIgnoreCase) == true
                    && !style.Font.Data.EastAsian)
                    shown = new Rune('\u00A5');
                // A character the font lacks is drawn and measured in Word's fallback font; marks, joiners and
                // variation selectors stay with the character before them.
                if (previous != null && previous != style && Rune.GetUnicodeCategory(shown) is UnicodeCategory.NonSpacingMark
                        or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
                    style = previous;
                else if (!style.Font.Data.HasGlyph(shown.Value) && styles.Fallback(style, shown.Value, r + 1 < runes.Count && runes[r + 1].Value == 0xFE0F) is { } fallback)
                    style = fallback;
                previous = style;
                var s = shown.ToString();
                text.Append(s);
                for (var k = 0; k < s.Length; k++)
                    list.Add(style);
            }
        }

        void Add(Inline inline)
        {
            var format = inline.Format;
            // Display equations take lines of their own.
            // A bookmark's end after the equation stays on its line (docx/external/libreoffice/extras-ooxmlexport/tdf130907:
            // _GoBack's end after the last display equation, no empty line).
            if (breakAfterDisplay && inline is not (BookmarkInline or BookmarkEndInline or AnchorInline or MathInline { Display: true }))
            {
                text.Append('\n');
                list.Add(styles.Style(format, WordSlot.Ascii));
                breakAfterDisplay = false;
            }
            switch (inline)
            {
                case MathInline math:
                {
                    // Display equations keep the document's math margins (m:lMargin, m:rMargin; measured with
                    // docx/local/mathset-d: they apply without m:dispDef, and the spacing m:preSp, m:postSp, m:interSp
                    // and m:intraSp changes nothing).
                    var (mathLeft, mathRight) = (MathTwips("lMargin"), MathTwips("rMargin"));
                    var lines = EquationLines(math, displayWidth == double.MaxValue ? displayWidth : Math.Max(1, displayWidth - mathLeft - mathRight));
                    // The highlight of the equation's runs lies behind all of it, as behind text (pandoc/golden/
                    // mark_math: inline and display equations yellow over their lines).
                    var mathStyle = styles.Style(format, WordSlot.Ascii);
                    if (math.RunFormats.Values.FirstOrDefault(f => f.Highlight is { } h && h != "none")?.Highlight is { } highlight)
                    {
                        var highlighted = format.Clone();
                        highlighted.Highlight = highlight;
                        mathStyle = styles.Style(highlighted, WordSlot.Ascii);
                    }
                    if (!math.Display)
                    {
                        foreach (var equation in lines)
                        {
                            equations[text.Length] = (equation, null);
                            text.Append('\uFFFC');
                            list.Add(mathStyle);
                        }
                        break;
                    }
                    // A display equation's lines, placed by its justification: centered as a group (the lines left
                    // aligned in it, the continuations indented; measured: Word centers the widest line), each
                    // centered, at the left or right; m:wrapRight puts the continuations at the right.
                    var indent = WrapIndent;
                    var group = lines.Count > 0 ? lines.Select((l, k) => l.Width + (k > 0 ? indent : 0)).Max() : 0;
                    var right = MathSetting("wrapRight") is "1" or "on" or "true" || MathSettings?.Element(Equations.MathLayout.M + "wrapRight") is { } wrapRight && wrapRight.Attribute(Equations.MathLayout.M + "val") == null;
                    var available = displayWidth == double.MaxValue ? group : Math.Max(1, displayWidth - mathLeft - mathRight);
                    for (var k = 0; k < lines.Count; k++)
                    {
                        if (text.Length > 0 && text[^1] != '\n')
                        {
                            text.Append('\n');
                            list.Add(styles.Style(format, WordSlot.Ascii));
                        }
                        var width = lines[k].Width;
                        double x = math.Justification switch
                        {
                            "left" => k > 0 ? indent : 0,
                            "right" => available - width,
                            "center" => (available - width) / 2,
                            _ => (available - group) / 2 + (k > 0 ? indent : 0),
                        };
                        if (k > 0 && right)
                            x = available - width;
                        equations[text.Length] = (lines[k], mathLeft + Math.Max(0, x));
                        text.Append('\uFFFC');
                        list.Add(mathStyle);
                    }
                    breakAfterDisplay = true;
                    break;
                }
                case TextInline t:
                    AddText(t.Text, format, t.Link);
                    break;
                case FieldInline field when (fields?.Value(field) ?? Evaluator?.Value(field, fields?.On)) is { } value:
                {
                    // Word sets its error texts in bold where the field's text would not be bold, and regular where it
                    // would (measured with docx/local/fielderrors, -14, -12 in modes 15, 14 and 12; nref-miss-15; eb-15,
                    // -14, -12: bold from a paragraph or character style or direct formatting makes the error regular,
                    // a bold paragraph and character style cancelling each other leave it bold).
                    var shown = format;
                    if (FieldEvaluator.IsError(value))
                    {
                        var errorFormat = field.ErrorFormat ?? format;
                        shown = errorFormat.Clone();
                        shown.Bold = errorFormat.Bold != true;
                    }
                    else if (Evaluator?.NoteAlignment(field) is { } alignment)
                    {
                        shown = format.Clone();
                        shown.VerticalAlign = alignment;
                    }
                    else if (Evaluator?.SymbolFormat(field, format) is { } symbol)
                        shown = symbol;
                    if (field.Unmerged is { } unmerged && Evaluator != null && fields?.On != null)
                    {
                        if (value.Length == 0)
                            Evaluator.Empty(field);
                        if (Evaluator.Emptied(field) && !FieldEvaluator.IsError(value))
                            shown = unmerged;
                    }
                    AddText(value, shown, field.Cached.OfType<TextInline>().FirstOrDefault()?.Link);
                    break;
                }
                case FieldInline field:
                    foreach (var cached in field.Cached)
                        Add(cached);
                    break;
                case AnchorInline anchor:
                    anchored.Add((text.Length, anchor));
                    break;
                case PermissionInline permission:
                    permissions.Add((text.Length, permission.Start));
                    break;
                case CommentInline comment:
                    commentMarks.Add((text.Length, comment.Id, comment.Start));
                    break;
                case DrawingInline drawing:
                    objects[text.Length] = drawing;
                    text.Append('\uFFFC');
                    list.Add(styles.Style(format, WordSlot.Ascii));
                    break;
                case CheckBoxInline box:
                    boxes[text.Length] = box;
                    text.Append('\uFFFC');
                    list.Add(styles.Style(format, WordSlot.Ascii));
                    break;
                case RubyInline ruby:
                    rubies[text.Length] = ruby;
                    text.Append('\uFFFC');
                    list.Add(styles.Style(ruby.Base.FirstOrDefault()?.Format ?? format, WordSlot.EastAsia));
                    break;
                case NoteReferenceInline note:
                    if (!note.Endnote && !note.Own)
                        notes.Add((text.Length, note.Id));
                    AddText(note.Mark.Length > 0 && NoteMarks?.GetValueOrDefault((note.Endnote, note.Id)) is { } mark ? mark : note.Mark, format, noteMark: true);
                    break;
                case SeparatorInline separator:
                    separators[text.Length] = separator.Continuation;
                    text.Append('\uFFFC');
                    list.Add(styles.Style(format, WordSlot.Ascii));
                    break;
                // Tabs and breaks of a right-to-left or complex script run take its complex script font and size
                // (docx/external/powertools/RP/RP051-Arabic: an empty paragraph of two breaks in w:rtl runs of w:szCs
                // 14, two lines 17.9 pt high).
                case TabInline tab:
                    if (tab.Positional is { } absolute)
                        positional[text.Length] = absolute;
                    text.Append('\t');
                    list.Add(styles.Character(format, '\t'));
                    break;
                case BreakInline b:
                    if (b.Type is not ("page" or "column") && b.Clear is "left" or "right" or "all")
                        clears[text.Length] = b.Clear;
                    text.Append(b.Type switch { "page" => '\f', "column" => '\v', _ => '\n' });
                    list.Add(styles.Character(format, '\n'));
                    break;
            }
        }

        // The list number comes first, followed by a tab, a space or nothing (w:suff).
        var labelLength = 0;
        if (paragraph.Label is { } label)
        {
            if (PictureBullet(label) is { } picture)
            {
                objects[text.Length] = picture;
                text.Append('\uFFFC');
                list.Add(styles.Style(label.Format, WordSlot.Ascii));
            }
            else
                AddText(label.Text, label.Format, number: true);
            labelLength = text.Length;
            if (label.Suffix == "tab")
            {
                text.Append('\t');
                list.Add(styles.Style(label.Format, WordSlot.Ascii));
            }
            else if (label.Suffix == "space")
                AddText(" ", label.Format, number: true);
        }
        // In a table cell a decimal tab stop aligns a paragraph without tabs at its decimal point, as if a tab began it
        // (docx/external/libreoffice/extras-odfexport/table_number_format_3: numbers at a stop of 1884 twips without tab
        // characters; openxml-sdk/complex0, complex2010).
        if (InCell && paragraph.Format.Tabs?.Any(t => t.Align == "decimal") == true && !paragraph.Content.Any(i => i is TabInline)
            && paragraph.Content.OfType<TextInline>().FirstOrDefault(t => t.Text.Length > 0) is { } firstText)
        {
            text.Append('\t');
            list.Add(styles.Style(firstText.Format, WordSlot.Ascii));
        }
        foreach (var inline in paragraph.Content)
            Add(inline);
        // Text of automatic color is white on dark shading, its own, its paragraph's or its cell's (measured with
        // docx/local/autocolor: where 0.30 red + 0.59 green + 0.11 blue of the shading is below 60 of 255, C00000 and
        // 006400 white, C80000, 006600 and 3C3C3C black; highlights change nothing); in a text box without shading it
        // takes the color of its shape's style (measured with docx/local/fref-colors, -colors14, -normal, -defaults:
        // fontRef lt1, red and accent2 color text without a color of its own, also of color auto, highlighted too; a
        // color of the run, its styles or the document defaults wins, paragraph or run shading gives auto's black).
        // Tracked deleted text counts where autofit tables measure their cells, as words of their own (see
        // RunFormat.Deleted).
        if (measuringDeleted)
        {
            foreach (var deleted in paragraph.Deleted ?? [])
                AddText(text.Length > 0 ? " " + deleted.Text : deleted.Text, deleted.Format);
        }
        var behind = Shading.Color(paragraph.Format.Shading) ?? Background;
        for (var k = 0; k < list.Count; k++)
        {
            if (!list[k].AutoColor)
                continue;
            if ((list[k].Shading ?? behind) is { } shade)
            {
                if ((0.30 * shade.R + 0.59 * shade.G + 0.11 * shade.B) * 255 < 60)
                    list[k] = whitened.GetOrAdd(list[k], s => s with { Color = FreeSlate.Drawing.RgbColor.White });
            }
            else if (TextColor is { } color)
                list[k] = colored.GetOrAdd((list[k], color), s => s.Item1 with { Color = s.Item2 });
        }
        return (text.ToString(), list, labelLength);
    }

    readonly System.Collections.Concurrent.ConcurrentDictionary<RunStyle, RunStyle> whitened = new();
    readonly System.Collections.Concurrent.ConcurrentDictionary<(RunStyle, FreeSlate.Drawing.RgbColor), RunStyle> colored = new();

    /// <summary>The shading behind the paragraphs being laid out where they have none of their own: their cell's.</summary>
    public FreeSlate.Drawing.RgbColor? Background { get; set; }

    /// <summary>
    /// Whether a justified line takes a word that overflows it by squeezing its spaces: where they shrink less than half
    /// as much (relative to their width) as the spaces of the line without the word would stretch, and by less than a
    /// quarter (measured with docx/local/compress3: 223 of 225 lines of random words in Times New Roman, the boundary
    /// between 0.496 and 0.501 of the stretch, the other two squeezing 25.4 % and 32 %; compress, compress2: a Russian
    /// line taken at 23.6 %, not at 25.9 %; jc-compress: ten words of Courier New in a column 12 pt narrower than they
    /// are, not 13 pt).
    /// </summary>
    /// <param name="overflow">How far the line with the word reaches past its end.</param>
    /// <param name="spaces">The width of the spaces between the words of the line with the word.</param>
    /// <param name="stretch">The room the line without the word leaves.</param>
    /// <param name="shortSpaces">The width of the spaces between the words of the line without the word.</param>
    static bool Squeezes(double overflow, double spaces, double stretch, double shortSpaces)
    {
        if (spaces <= 0)
            return false;
        var squeeze = overflow / spaces;
        return squeeze < 0.25 && (shortSpaces <= 0 || squeeze < 0.5 * stretch / shortSpaces);
    }

    /// <summary>The width of the hyphen a soft hyphen shows at a line's end, in the run's style.</summary>
    static double HyphenWidth(RunStyle style) =>
        style.Font.Data.GlyphsWithFeatures("-", style.Features, language: style.Language).Sum(g => g.Advance) * style.Size * style.Scale + style.Spacing;

    /// <summary>What kerning between a letter and the hyphen after it adds to the letter's advance (0 unkerned).</summary>
    static double HyphenKern(RunStyle style, char letter)
    {
        if (!style.Features.Contains("kern"))
            return 0;
        var data = style.Font.Data;
        var pair = data.GlyphsWithFeatures($"{letter}-", style.Features, language: style.Language).Sum(g => g.Advance);
        var alone = data.GlyphsWithFeatures(letter.ToString(), style.Features, language: style.Language).Sum(g => g.Advance)
            + data.GlyphsWithFeatures("-", style.Features, language: style.Language).Sum(g => g.Advance);
        return (pair - alone) * style.Size * style.Scale;
    }

    /// <summary>
    /// A picture bullet (w:lvlPicBulletId, catalog: numbering.pictureBullets) as an inline picture: about 2 pt less high
    /// than the number's font size (0.996 × size − 1.97 pt), as wide as its aspect ratio makes it, standing on the
    /// baseline where the number would (measured with docx/local/picture-bullets2: 8 to 48 pt make 6 to 45.85 pt
    /// pictures); null without one.
    /// </summary>
    DrawingInline? PictureBullet(ListLabel label)
    {
        if (label.Level.PictureBullet is not { } id || context.Numbering.PictureBullet(id) is not { } picture || context.Numbering.Part is not { } part)
            return null;
        XNamespace v = "urn:schemas-microsoft-com:vml";
        var embed = picture.Descendants(v + "imagedata").Select(i => (string?)i.Attribute(Wml.R + "id")).FirstOrDefault(r => r != null)
                    ?? picture.Descendants(FreeSlate.Ns.A + "blip").Select(b => (string?)b.Attribute(Wml.R + "embed")).FirstOrDefault(r => r != null);
        // A picture bullet without a picture takes its place and shows nothing (LibreOffice's
        // extras-ooxmlexport/lvlPicBulletId: a v:shape without v:imagedata, no bullet character drawn).
        if (embed == null && !picture.Descendants(v + "shape").Any())
            return null;
        // The picture's aspect ratio from its VML style or its extent.
        var aspect = 1.0;
        if (picture.Descendants(v + "shape").FirstOrDefault()?.Attribute("style")?.Value is { } style
            && Vml.StyleLength(style, "width") is { } sw && Vml.StyleLength(style, "height") is { } sh && sh > 0)
            aspect = sw / sh;
        else if (picture.Descendants(Drawings.Wp + "extent").FirstOrDefault() is { } extent
            && double.TryParse((string?)extent.Attribute("cx"), System.Globalization.CultureInfo.InvariantCulture, out var cx)
            && double.TryParse((string?)extent.Attribute("cy"), System.Globalization.CultureInfo.InvariantCulture, out var cy) && cy > 0)
            aspect = cx / cy;
        var height = Math.Max(1, 0.996 * styles.Style(label.Format, WordSlot.Ascii).Size - 1.97);
        var (w, h) = ((long)Math.Round(height * aspect * 12700), (long)Math.Round(height * 12700));
        var a = FreeSlate.Ns.A;
        XNamespace pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
        if (embed == null)
            return new DrawingInline(new XElement(Drawings.Wp + "inline",
                new XElement(Drawings.Wp + "extent", new XAttribute("cx", w), new XAttribute("cy", h)),
                new XElement(Drawings.Wp + "docPr", new XAttribute("id", 0), new XAttribute("name", "Picture bullet"))), part, label.Format);
        var drawing = new XElement(Drawings.Wp + "inline",
            new XElement(Drawings.Wp + "extent", new XAttribute("cx", w), new XAttribute("cy", h)),
            new XElement(Drawings.Wp + "docPr", new XAttribute("id", 0), new XAttribute("name", "Picture bullet")),
            new XElement(a + "graphic", new XElement(a + "graphicData", new XAttribute("uri", pic.NamespaceName),
                new XElement(pic + "pic",
                    new XElement(pic + "nvPicPr", new XElement(pic + "cNvPr", new XAttribute("id", 0), new XAttribute("name", "")), new XElement(pic + "cNvPicPr")),
                    new XElement(pic + "blipFill", new XElement(a + "blip", new XAttribute(Wml.R + "embed", embed)), new XElement(a + "stretch", new XElement(a + "fillRect"))),
                    new XElement(pic + "spPr",
                        new XElement(a + "xfrm", new XElement(a + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(a + "ext", new XAttribute("cx", w), new XAttribute("cy", h))),
                        new XElement(a + "prstGeom", new XAttribute("prst", "rect"), new XElement(a + "avLst")))))));
        return new DrawingInline(drawing, part, label.Format);
    }

    /// <summary>The advance of every character in points, shaped run by run with Word's features.</summary>
    /// <summary>
    /// The automatic space between East Asian text and Latin letters (w:autoSpaceDE) and digits (w:autoSpaceDN), both
    /// on unless the paragraph turns them off, where the document's default East Asian font is one: after the
    /// character before the boundary, a quarter of the East Asian character's size after East Asian text, half the
    /// Latin font's average character width after Latin text (measured with docx/local/ec-none, ea-autospace and
    /// ea-autospace2: Arial, Times New Roman, Calibri, Courier New and Segoe UI beside MS Mincho, Yu Mincho, SimSun and
    /// Meiryo at 11 to 22 pt, within 0.1 pt).
    /// </summary>
    /// <param name="spaced">The boundaries and their automatic spaces, whether added or not.</param>
    /// <param name="add">Whether the text gets the spaces (where the document's default East Asian font is one).</param>
    static void AutoSpace(string text, List<RunStyle> styles, double[] advances, ParagraphFormat format, Dictionary<int, double> spaced, bool add)
    {
        var (letters, digits) = (format.AutoSpaceDE != false, format.AutoSpaceDN != false);
        if (!letters && !digits)
            return;
        static bool Asian(char c) => LineBreaker.IsIdeographic(c) && char.IsLetter(c);
        static bool Western(char c, bool letters, bool digits) =>
            letters && char.IsLetter(c) && !LineBreaker.IsIdeographic(c) && c < '\u2E80' || digits && char.IsAsciiDigit(c);
        for (var i = 0; i + 1 < text.Length && i + 1 < styles.Count; i++)
        {
            var (a, b) = (text[i], text[i + 1]);
            if (Asian(a) && Western(b, letters, digits))
                spaced[i] = styles[i].Size / 4;
            else if (Western(a, letters, digits) && Asian(b))
                spaced[i] = styles[i].Size * styles[i].Font.Data.AverageWidth / 2;
            else
                continue;
            if (add)
                advances[i] += spaced[i];
        }
    }

    /// <summary>
    /// Spaces beside another space or an East Asian character half as wide as their font's size, as Word balances
    /// single and double byte characters (w:balanceSingleByteDoubleByteWidth, catalog: settings.compatibility;
    /// measured with docx/local/c2-bsbdb and c3-bsbdb: Arial, Times New Roman, Courier New and Consolas at 8 to 20 pt,
    /// spaces between Latin words left alone, and so are the spaces of runs whose East Asian font is none).
    /// </summary>
    /// <param name="spaces">The spaces that may balance; left with those that did.</param>
    static void Balance(string text, List<RunStyle> charStyles, double[] advances, HashSet<int> spaces)
    {
        bool Next(int k) => k >= 0 && k < text.Length && (text[k] == ' ' || text[k] >= 0x80 && Text.Scripts.Of(text[k]) is { Slot: Text.FontSlot.EastAsian });
        foreach (var i in spaces.ToList())
        {
            if (i >= charStyles.Count || text[i] != ' ' || !(Next(i - 1) || Next(i + 1)))
            {
                spaces.Remove(i);
                continue;
            }
            var style = charStyles[i];
            var space = style.Font.Data.GlyphsWithFeatures(" ", style.Features, language: style.Language).Sum(g => g.Advance) * style.Size * style.Scale;
            advances[i] += style.Size * style.Scale / 2 - space;
        }
    }

    /// <summary>Whether a character takes part in Arabic, Syriac or N'Ko joining (letters, tatweel and the marks on them).</summary>
    internal static bool Joining(char c) =>
        c is >= '\u0620' and <= '\u065F' or '\u0670' or >= '\u0671' and <= '\u06D3' or '\u06D5' or >= '\u06FA' and <= '\u06FC' or '\u06FF'
            or >= '\u0710' and <= '\u074F' or >= '\u0750' and <= '\u077F' or >= '\u07CA' and <= '\u07F5' or >= '\u08A0' and <= '\u08C9';

    /// <summary>Arabic letters that join only the letter before them (ArabicShaping.txt type R: alef, dal, reh, waw ...).</summary>
    static bool RightJoiningOnly(char c) => c is '\u0622' or '\u0623' or '\u0624' or '\u0625' or '\u0627' or '\u0629' or '\u062F' or '\u0630'
        or '\u0631' or '\u0632' or '\u0648' or '\u0671' or '\u0672' or '\u0673' or '\u0675' or '\u0676' or '\u0677'
        or >= '\u0688' and <= '\u0699' or '\u06C0' or >= '\u06C3' and <= '\u06CB' or '\u06CD' or '\u06CF' or '\u06D2'
        or '\u06D3' or '\u06D5' or '\u06EE' or '\u06EF';

    /// <summary>
    /// Whether the letters on either side of a run boundary (before <paramref name="boundary"/>) join: both take part in
    /// joining and the first joins the letter after it, which right-joining Arabic letters (alef, dal, reh, waw ...)
    /// and the hamza do not; marks between them are transparent (found by agent BI in Word's PDF of LibreOffice's
    /// extras-ooxmlimport/tdf87533_bidi: a run beginning after a reh in its initial form, not a medial one).
    /// </summary>
    internal static bool JoinsAcross(string text, int boundary)
    {
        static bool Mark(char c) => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark;
        if (boundary <= 0 || boundary >= text.Length || !Joining(text[boundary - 1]) || !Joining(text[boundary]))
            return false;
        var before = boundary - 1;
        while (before > 0 && Mark(text[before]) && Joining(text[before - 1]))
            before--;
        var after = boundary;
        while (after + 1 < text.Length && Mark(text[after]) && Joining(text[after + 1]))
            after++;
        var (left, right) = (text[before], text[after]);
        return !RightJoiningOnly(left) && left is not ('\u0621' or '\u0674' or '\u06DD') && right is not ('\u0621' or '\u0674' or '\u06DD');
    }

    static double[] Advances(string text, List<RunStyle> charStyles, double extra = 0, bool eastAsianOnly = false)
    {
        var advances = new double[text.Length];
        var start = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i < text.Length && charStyles[i].ShapesWith(charStyles[start]) && text[i] is not ('\t' or '\n' or '\f' or '\v')
                && text[start] is not ('\t' or '\n' or '\f' or '\v'))
                continue;
            if (text[start] is not ('\t' or '\n' or '\f' or '\v'))
            {
                var style = charStyles[start];
                var piece = text[start..i];
                // An Arabic word over several runs is shaped joined, as Word sets it (docx/external/powertools/RP/
                // RP051-Arabic: "ال" and "ملاحة" in two runs one word 25.5 pt wide, not 32.7 pt of a final lam).
                var before = JoinsAcross(text, start) ? text[(start - 1)..start] : null;
                var after = JoinsAcross(text, i) ? text[i..(i + 1)] : null;
                var glyphs = style.Font.Data.GlyphsWithFeatures(piece, style.Features, language: style.Language, before: before, after: after);
                var firstOfCluster = new HashSet<int>();
                for (var k = 0; k < glyphs.Length; k++)
                {
                    var g = glyphs[k];
                    if (g.Cluster < piece.Length)
                    {
                        advances[start + g.Cluster] += g.Advance * style.Size * style.Scale;
                        firstOfCluster.Add(g.Cluster);
                        // Kern table pairs (documents before Word 2010), of every script (docx/local/legkern-*).
                        if (style.LegacyKerning && k + 1 < glyphs.Length && glyphs[k + 1].Cluster < piece.Length)
                            advances[start + g.Cluster] += style.Font.Data.LegacyKerning(g.Id, glyphs[k + 1].Id) * style.Size * style.Scale;
                    }
                }
                foreach (var cluster in firstOfCluster)
                    advances[start + cluster] += style.Spacing + (!eastAsianOnly || EastAsian(text[start + cluster]) ? extra : 0);
            }
            start = i;
        }
        return advances;
    }

    /// <summary>
    /// Where the text after a tab at <paramref name="x"/> starts (catalog: paragraph.tabs): at the next custom stop past
    /// it, aligned left, centered, right or on the decimal point; a hanging first line stops at the left indent; past
    /// the custom stops the default stops continue (multiples of w:defaultTabStop from the margin).
    /// </summary>
    /// <summary>
    /// Where an absolute position tab takes the text after it: the left edge, the middle or the right edge of the margins
    /// or the indents, as a stop of its alignment there would (see TabStop).
    /// </summary>
    double PositionalPlace(string text, int tab, double[] advances, out TabStop stop)
    {
        var absolute = positional[tab];
        var (from, to) = absolute.RelativeTo == "indent" ? (positionalArea.IndentLeft, positionalArea.IndentRight) : (positionalArea.MarginLeft, positionalArea.MarginRight);
        var (position, align) = absolute.Alignment switch { "center" => ((from + to) / 2, "center"), "right" => (to, "right"), _ => (from, "left") };
        stop = new TabStop(position, align, absolute.Leader);
        var next = tab + 1;
        while (next < text.Length && text[next] is not ('\t' or '\n' or '\f' or '\v'))
            next++;
        var length = 0.0;
        for (var k = tab + 1; k < next; k++)
            length += advances[k];
        return align switch { "center" => position - length / 2, "right" => position - length, _ => position };
    }

    /// <param name="limit">
    /// Where the line ends: the right indent, or the edge a stop past it took the line to; centered, right and decimal
    /// stops past it align at it (measured with docx/local/tab-break).
    /// </param>
    double TabStop(ParagraphFormat format, double x, string text, int tab, List<RunStyle> charStyles, double[] advances, double left, double firstLine, double limit, out TabStop? stop)
    {
        stop = null;
        // An absolute position tab aligns what follows it at the left edge, the middle or the right edge of the
        // margins or the indents, as a stop of its alignment there would.
        if (positional.ContainsKey(tab))
        {
            var place = PositionalPlace(text, tab, advances, out var absoluteStop);
            stop = absoluteStop;
            return Math.Max(x, place);
        }
        // The tab after a list number takes the left indent as its stop also where the number ends right at it, which
        // leaves the tab no width; custom and default stops count only after its place, as for other tabs (measured
        // with docx/local/emptylabel, tabzero, tabzero2: an empty number or one right-aligned at the indent;
        // docx/external/libreoffice/extras-ooxmlexport/tdf153042_largeTab: a number tab where the first line begins,
        // past the indent, does not count).
        var label = tab == labelTab;
        // Word 6 numbering puts the text its legacy indent after the number's start, its legacy space after the number's
        // end and at the left indent, whichever is furthest (measured with docx/local/lgy-a, lgy-b: indents of 0, 336 and
        // 720 twips, spaces of 0, 120 and 288, hanging indents of 0 and 360, all 18 in modes 15 and 12 within 0.01 pt).
        if (label && legacyLabel is { } legacy)
            return Math.Max(Math.Max(left + firstLine + legacy.Indent, x + legacy.Space), left);
        if ((firstLine < 0 || label) && x < left + (label ? 0.005 : -0.005))
        {
            // The left indent is a stop for the hanging first line, unless a custom stop comes before it; with
            // w:doNotUseIndentAsNumberingTabStop before Word 2013 the number's tab takes any custom stop after it
            // first (measured with docx/local/numtabind-*: modes 14, 12 and 11, not 15).
            var custom = format.Tabs?.FirstOrDefault(t => t.Position > x + 0.005 && t.Align != "bar");
            if (custom == null || custom.Position > left && !(label && context.Profile.NumberTabSkipsIndent))
                return left;
        }
        foreach (var t in format.Tabs ?? [])
        {
            if (t.Position <= x + 0.005 || t.Align is "bar" or "clear")
                continue;
            stop = t;
            // The text the stop aligns: up to the next tab or break.
            var end = tab + 1;
            while (end < text.Length && text[end] is not ('\t' or '\n' or '\f' or '\v'))
                end++;
            var width = 0.0;
            for (var k = tab + 1; k < end; k++)
                width += advances[k];
            // Centered and decimal text that would reach past the line's end ends there (measured with docx/local/tv-c,
            // tv-d: a word of 200 pt centered 20.8 pt short of the indent, "12," and 40 zeros at a decimal stop at the
            // indent, in modes 14 and 15). After a list number every stop is a left one (measured with
            // docx/local/lbt2-15, lbt2-14: centre, right and decimal stops between the number's end and the indent put
            // the text at the stop; docx/external/libreoffice/extras-ooxmlexport/rhbz988516).
            var start = (label ? "left" : t.Align) switch
            {
                "center" or "right" or "end" or "decimal" when t.Position > limit + 0.005 => limit - width,
                "center" => Math.Min(t.Position - width / 2, limit - width),
                "right" or "end" => t.Position - width,
                "decimal" => Math.Min(DecimalStart(text, tab + 1, end, advances, t.Position, width, context.DecimalSymbol), limit - width),
                _ => t.Position,
            };
            return Math.Max(x, start);
        }
        // Without default stops a tab goes nowhere (docx/local/dts-0, dts-65536).
        var interval = context.DefaultTabStop;
        return interval > 0 ? (Math.Floor(x / interval + 1e-6) + 1) * interval : x;
    }

    /// <summary>
    /// Where text after a decimal tab starts: its decimal symbol at the stop, or its end where it has none (measured
    /// with docx/generated/tabs: with the German decimal comma, 12.5 ends at the stop).
    /// </summary>
    static double DecimalStart(string text, int start, int end, double[] advances, double position, double width, char symbol)
    {
        var point = text.IndexOf(symbol, start, end - start);
        if (point < 0)
            return position - width;
        var before = 0.0;
        for (var k = start; k < point; k++)
            before += advances[k];
        return position - before;
    }

    /// <param name="labelEnd">Where the list number (and a space after it) ends in the text.</param>
    Line BuildLine(string text, List<RunStyle> charStyles, double[] advances, int start, int end, double startX,
        ParagraphFormat format, double left, double firstLine, RunStyle mark, int labelEnd, double limit, Dictionary<int, double>? jumps = null)
    {
        var line = new Line { First = start == 0, TextStart = start, Vertical = Vertical, TurnEastAsian = TurnEastAsian };
        foreach (var (index, id) in notes)
        {
            if (index >= start && (index < end || index == end && end == text.Length))
                (line.Notes ??= []).Add(id);
        }
        var x = startX;
        var pieceStart = start;
        for (var i = start; i <= end; i++)
        {
            var boundary = i == end || text[i] is '\t' or '\n' or '\f' or '\v' || i > pieceStart && !ReferenceEquals(charStyles[i], charStyles[pieceStart])
                || jumps != null && jumps.ContainsKey(i) && i > start
                || i > pieceStart && (text[i] == '\uFFFC' || text[i - 1] == '\uFFFC')
                || i > pieceStart && i < end && links.Count > 0 && LinkAt(i) != LinkAt(pieceStart)
                || i > pieceStart && i < end && levels != null && levels[i] != levels[pieceStart];
            if (!boundary)
                continue;
            if (i > pieceStart && text[pieceStart] is not ('\t' or '\n' or '\f' or '\v'))
            {
                // A soft hyphen where the line breaks shows as a hyphen (measured with docx/local/run-content), and so
                // does a word broken by automatic hyphenation.
                var hyphen = i == end && end < text.Length && text[end - 1] == '\u00AD';
                var automatic = i == end && end == autoHyphenAt;
                var pieceAdvances = advances[pieceStart..i];
                if (hyphen)
                    pieceAdvances[^1] = HyphenWidth(charStyles[end - 1]);
                if (automatic)
                    pieceAdvances = [.. pieceAdvances, HyphenWidth(charStyles[end - 1])];
                // The hyphen kerns with the letter before it (docx/external/libreoffice/extras-ooxmlexport/
                // tdf160518_useWord2013TrackBottomHyphenation: "differ-" in kerned Times New Roman 49 pt, the hyphen
                // 0.93 pt left by the pair's 41 units).
                if ((hyphen || automatic) && pieceAdvances.Length >= 2)
                {
                    var before = hyphen ? end - 2 : end - 1;
                    if (before >= pieceStart && text[before] is var letter && !char.IsSurrogate(letter))
                        pieceAdvances[^2] += HyphenKern(charStyles[end - 1], letter);
                }
                if (hyphen || automatic)
                    line.Hyphenated = true;
                if (automatic)
                    line.AutoHyphenated = true;
                var piece = new Piece
                {
                    Text = hyphen ? text[pieceStart..(i - 1)] + "-" : automatic ? text[pieceStart..i] + "-" : text[pieceStart..i],
                    Style = charStyles[pieceStart],
                    X = x,
                    Start = pieceStart,
                    JoinsBefore = JoinsAcross(text, pieceStart),
                    JoinsAfter = i > pieceStart && JoinsAcross(text, i),
                    Advances = pieceAdvances,
                    Lead = leads.GetValueOrDefault(pieceStart),
                    IsLabel = pieceStart < labelEnd,
                    Drawing = text[pieceStart] == '\uFFFC' && objects.TryGetValue(pieceStart, out var drawing) ? drawing : null,
                    Equation = text[pieceStart] == '\uFFFC' && equations.TryGetValue(pieceStart, out var math) ? math.Equation : null,
                    CheckBox = text[pieceStart] == '\uFFFC' && boxes.TryGetValue(pieceStart, out var box) ? box : null,
                    Ruby = text[pieceStart] == '\uFFFC' && rubies.TryGetValue(pieceStart, out var ruby) ? ruby : null,
                    Combined = text[pieceStart] == '\uFFFC' && combined.TryGetValue(pieceStart, out var pair) ? pair : null,
                    Across = text[pieceStart] == '\uFFFC' && across.TryGetValue(pieceStart, out var turned) ? turned : null,
                    DisplayX = text[pieceStart] == '\uFFFC' && equations.TryGetValue(pieceStart, out var display) ? display.DisplayX : null,
                    Separator = text[pieceStart] == '\uFFFC' && separators.ContainsKey(pieceStart),
                    Link = links.Count > 0 ? LinkAt(pieceStart) : null,
                    Level = levels?[pieceStart] ?? 0,
                };
                line.Pieces.Add(piece);
                x += piece.Width;
            }
            // A jump over a floating object: an invisible spacer to the next segment.
            if (jumps != null && i < end && i > start && jumps.TryGetValue(i, out var jumpTo))
            {
                line.Pieces.Add(new Piece { Text = "\t", Style = charStyles[i], X = x, Advances = [Math.Max(0, jumpTo - x)], IsTab = true });
                line.Jumped = true;
                x = jumpTo;
                pieceStart = i;
                if (text[i] != '\t')
                    continue;
            }
            if (i < end && text[i] == '\t')
            {
                // A tab reaching past the right margin ends at it.
                var to = Math.Min(TabStop(format, x, text, i, charStyles, advances, left, firstLine, limit, out var stop), Math.Max(x, limit));
                line.Pieces.Add(new Piece
                {
                    Text = "\t",
                    Style = charStyles[i],
                    X = x,
                    Start = i,
                    Advances = [Math.Max(0, to - x)],
                    IsTab = true,
                    Leader = stop?.Leader is { } leader && leader != "none" ? leader : null,
                });
                x = to;
                pieceStart = i + 1;
                continue;
            }
            pieceStart = i < end && text[i] is '\n' or '\f' or '\v' ? i + 1 : i;
        }

        // Vertical metrics: the largest parts above and below the baseline of the line's runs (raised runs reach
        // further); an empty line takes its break's or the paragraph mark's.
        double above = 0, below = 0, raised = 0, lowered = 0, largest = 0, rubyRaised = 0;
        var any = false;
        var texts = false;       // anything but list numbers that stands like text
        var drawings = false;
        var pictures = false;
        // Spaces between drawings do not make the line taller (measured with docx/local/vml-inline2: two 40 pt shapes
        // or pictures with spaces between them make the line as high as one alone); beside text they count.
        static bool Blank(Piece p) => p.Drawing == null && p.Equation == null && p.CheckBox == null && p.Ruby == null && p.Combined == null && p.Across == null && string.IsNullOrWhiteSpace(p.Text);
        var onlyDrawings = line.Pieces.Any(p => p.Drawing != null) && line.Pieces.All(p => p.IsTab || p.Drawing != null || Blank(p));
        // Spaces never make a line taller or lower, whatever their size, where they stand or how they are formatted;
        // a line of nothing but spaces is the paragraph mark's (measured with docx/local/ts-*, tu-*, tv-*: a 45 pt space
        // before, between or after 9 pt words leaves the line 9 pt, in modes 12, 14 and 15; a 45 pt "x" or no-break
        // space does not; docx/external/libreoffice/extras-ooxmlexport/testOuterShdw). So do the en, em, four-per-em
        // and ideographic spaces, not the others (wsp-15, wsp-12: 45 pt U+2002, U+2003, U+2005 and U+3000 at a 9 pt
        // line's start, middle or end leave it 9 pt; U+00A0, U+2000, U+2001, U+2004, U+2006 to U+200A, U+202F and
        // U+205F make it 45 pt; docx/external/libreoffice/extras-layout/tdf153136).
        static bool Spaces(Piece p) => Blank(p) && p.Text.Length > 0 && p.Text.All(c => c is ' ' or '\u2002' or '\u2003' or '\u2005' or '\u3000') && !p.IsLabel;
        // Tabs neither: a paragraph's last line of nothing but tabs is the mark's too (measured with docx/local/
        // tabheight: a 24 pt tab, alone, twice or between 8 pt letters, leaves an 8 pt mark's line 8 pt, an 8 pt tab
        // alone a 24 pt mark's 24 pt; docx/external/powertools/RA001-Tracked-Revisions-01), a line before it of a tab
        // alone the tab's (tovc-10: a 10 pt tab wrapped past the right indent, the mark 12 pt).
        var spacesOnly = line.Pieces.Count > 0 && line.Pieces.All(p => p.IsTab || Spaces(p)) && (line.Pieces.Any(Spaces) || end >= text.Length);
        foreach (var piece in line.Pieces.Where(p => !p.IsTab && !(onlyDrawings && Blank(p)) && !Spaces(p)))
        {
            // Ruby stands above its base, w:hpsRaise up from the base's baseline: the line makes room for it.
            if (piece.Ruby is { } rubyText && rubyText.Ruby.Count > 0)
            {
                var rubyStyle = styles.Style(rubyText.Ruby[0].Format, WordSlot.EastAsia);
                above = Math.Max(above, piece.Style.Metrics.Above);
                if (rubyText.Align == "rightVertical")
                {
                    // Its characters run down beside the base, centered on its middle.
                    var height = rubyText.Ruby.Sum(t => t.Text.EnumerateRunes().Count()) * rubyStyle.Size;
                    var middle = (piece.Style.Font.Data.WinAscent - piece.Style.Font.Data.WinDescent) / 2 * piece.Style.Size;
                    raised = Math.Max(raised, middle + height / 2);
                    lowered = Math.Max(lowered, height / 2 - middle);
                }
                else
                {
                    raised = Math.Max(raised, rubyText.Raise + rubyStyle.Metrics.Above);
                    rubyRaised = Math.Max(rubyRaised, rubyText.Raise + rubyStyle.Metrics.Above);
                }
            }
            // A check box form field stands like a character of its size.
            if (piece.CheckBox is { } checkBox)
            {
                var (boxAbove, boxBelow) = BoxMetrics(checkBox);
                above = Math.Max(above, boxAbove);
                below = Math.Max(below, boxBelow);
                raised = Math.Max(raised, boxAbove);
                lowered = Math.Max(lowered, boxBelow);
                largest = Math.Max(largest, boxAbove + boxBelow);
                any = texts = true;
                continue;
            }
            // An equation stands on the baseline with its ascent and descent; the line spacing's extra comes from the
            // fonts, the paragraph mark's where no text is beside it (measured with docx/local/eq-shaded: a display
            // equation's line is its ascent, its descent and the mark's extra high).
            if (piece.Equation is { } equation)
            {
                above = Math.Max(above, equation.Ascent);
                below = Math.Max(below, equation.Descent);
                raised = Math.Max(raised, equation.Ascent);
                lowered = Math.Max(lowered, equation.Descent);
                drawings = true;
                continue;
            }
            // A horizontal rule stands in a line as high as the paragraph mark's, its bottom 2.52 pt above the line's
            // foot; before Word 2013 one without the mark on its line in a line only 2.52 pt higher and lower than
            // itself, whatever the spacing (Vml.Rule).
            if (piece.Drawing is { } rule && rule.Drawing.Attribute(Vml.Rule) != null)
            {
                var (markAbove, markBelow) = Metrics(mark, Vertical);
                if (context.Profile.RulesSizeTheirLines && end < text.Length)
                {
                    above = Math.Max(above, rule.Box.Height + 2.52);
                    below = Math.Max(below, 2.52);
                    raised = Math.Max(raised, rule.Box.Height + 2.52);
                    lowered = Math.Max(lowered, 2.52);
                    piece.Rule = (0, 0);
                    any = true;
                    continue;
                }
                piece.Rule = (0, markBelow - 2.52);
                above = Math.Max(above, Math.Max(markAbove, rule.Box.Height));
                below = Math.Max(below, markBelow);
                raised = Math.Max(raised, Math.Max(markAbove, rule.Box.Height));
                lowered = Math.Max(lowered, markBelow);
                largest = Math.Max(largest, markAbove + markBelow);
                any = texts = true;
                continue;
            }
            // An inline drawing stands on the baseline: its box is all ascent; the line spacing's extra comes from
            // the fonts (measured with docx/generated/drawings: a 90 pt picture beside 12 pt Aptos at 1.16 lines makes
            // the line 90 + 3.38 + 2.32 pt high, a picture alone 160.5 + 2.32 pt).
            if (piece.Drawing is { } drawing)
            {
                // Lowered or raised (w:position) it moves as text does (Word's PDFs of docx/external/libreoffice/
                // extras-layout/tdf138465min: an equation of 15.6 pt lowered 3 pt makes the line's ascent 12.6 pt;
                // extras-ooxmlimport/tdf119200: equations lowered 2, 3 and 4 pt on an exact line).
                // On a line of nothing but drawings lowering moves nothing, unless the line is exactly so high (Word's
                // PDFs of docx/local/c2mt-a, -c, -e: an equation of 13.25 pt alone in its paragraph lowered 2, 4 or 6 pt
                // stands where it does unlowered, the lines below too; c2mt-d: raised 2 pt the line grows; LibreOffice's
                // extras-odfexport/2_MathType3; on exact lines extras-ooxmlimport/tdf119200 lowers them).
                var drawingLift = context.Profile.NoSpaceRaiseLower ? 0
                    : onlyDrawings && format.LineRule != "exact" ? Math.Max(0, piece.Style.Raise) : piece.Style.Raise;
                piece.Lift = drawingLift;
                above = Math.Max(above, drawing.Box.Height + Math.Min(0, drawingLift));
                raised = Math.Max(raised, drawing.Box.Height + drawingLift);
                lowered = Math.Max(lowered, -drawingLift);
                drawings = true;
                pictures = true;
                continue;
            }
            // Text set across the line reaches as far beyond its middle as it is long; the line's spacing is its font's.
            if (piece.Across is { } turned)
            {
                var (_, length, _, _, middle) = Across(turned, Vertical);
                var (fontAbove, fontBelow) = Metrics(piece.Style, Vertical);
                above = Math.Max(above, Math.Max(fontAbove, middle + length / 2));
                below = Math.Max(below, Math.Max(fontBelow, length / 2 - middle));
                raised = Math.Max(raised, Math.Max(fontAbove, middle + length / 2));
                lowered = Math.Max(lowered, Math.Max(fontBelow, length / 2 - middle));
                largest = Math.Max(largest, fontAbove + fontBelow);
                any = texts = true;
                continue;
            }
            // Super- and subscripts stand within the line of the size they belong to; w:position moves them on.
            var (a, b) = piece.Style.ScriptBase is { } scriptBase ? WordFonts.LineMetrics(piece.Style.Font.Data, scriptBase, piece.Style.LineHeights) : Metrics(piece.Style, Vertical);
            var lift = TopLift(piece, format);
            // Lowered text takes as much less of the ascent, raised text of the descent (measured with
            // docx/local/lowr-*: "Base" in 12 pt Arial with 48 pt lowered 12 pt has its baseline 33 pt below the line's
            // top, 45 pt less 12, lowered 60 pt the 12 pt text's own; 48 pt raised 12 pt leaves the 12 pt text's
            // descent; docx/external/libreoffice/extras-ooxmlexport/tdf99602_charStyleSubscript); where characters
            // align their tops, centers or bottoms (w:textAlignment) they keep their whole ascent and descent
            // (lowr-18, lowr-19: 12 pt lowered 6 pt aligned top make the line about 19.4 pt high; tdf140668).
            var shifts = format.TextAlignment is not ("top" or "center" or "bottom");
            // A run's border and its space take room above and below its text, outside the spacing (measured with
            // docx/local/rbd-*: borders of 0.5 to 3 pt with 0 and 4 pt of space make the line taller by twice both,
            // the box reaching from the line's top; docx/external/powertools/HC033).
            var boxed = piece.Style.Border is { } runBorder ? PageBorders.Room(runBorder) : 0;
            above = Math.Max(above, a + boxed + (shifts ? Math.Min(0, lift) : 0));
            // Text effects reach beyond the text; the line makes room for them outside its spacing.
            var (reachAbove, reachBelow) = TextEffects.Reach(piece.Style.Effects, piece.Style.Size);
            // So do emphasis marks: a quarter of the text's single line height above it (dot, comma, circle) or below it
            // (underDot), outside the spacing too (measured with docx/local/emr-*: Calibri 11, 16 and 24 pt lines 3.36,
            // 4.92 and 7.32 pt lower, the line after an underDot one as much lower, at single and 1.15 spacing, in modes
            // 12 and 15; Times New Roman 11 3.12; LibreOffice's extras-ooxmlexport/em).
            if (piece.Style.Emphasis is { } emphasis)
            {
                if (emphasis == "underDot")
                    reachBelow += (a + b) / 4;
                else
                    reachAbove += (a + b) / 4;
            }
            raised = Math.Max(raised, a + boxed + lift + reachAbove);
            any = true;
            // A list number's ascent counts, its descent not (measured with docx/local/num-rules: a 16 pt number
            // before 12 pt Arial makes the line 17.52 pt high, its ascent and the text's descent).
            if (piece.IsLabel)
                continue;
            texts = true;
            below = Math.Max(below, b + boxed - (shifts ? Math.Max(0, lift) : 0));
            largest = Math.Max(largest, a + b);
            lowered = Math.Max(lowered, b + boxed - lift + reachBelow);
        }
        // Before Word 2013 a line of drawings without the paragraph mark is as high as they are, without spacing's extra;
        // where a line break ends it, it has the break's font as text has (measured with docx/local/dl-a14, dl-b14: a 470
        // by 2 pt rectangle too wide for the line between 36 pt words makes a line 2 pt high, at 1.15 lines too; dl-c14,
        // dl-d14: a 40 by 20 pt one between line breaks a line of the 36 pt font; in mode 15 both the mark's; LibreOffice's
        // extras-ooxmlexport/128197_compat14).
        if (drawings && !texts && context.Profile.DrawingLinesWithoutMark && end < text.Length)
        {
            if (end > start && text[end - 1] is '\n' or '\v' && end - 1 < charStyles.Count)
            {
                var (a, b) = Metrics(charStyles[end - 1], Vertical);
                above = Math.Max(above, a);
                raised = Math.Max(raised, a);
                below = Math.Max(below, b);
                lowered = Math.Max(lowered, b);
                largest = a + b;
            }
            any = true;
        }
        else if (drawings && !texts)
        {
            // Drawings without text: no descent, the paragraph mark's font gives the spacing's extra; pictures
            // smaller than the mark's line still take its whole height (measured with docx/local/smallpic-single and
            // -115: pictures of 2 to 12 pt beside an 11 pt Calibri mark stand 13.43 pt high, larger ones their own);
            // beside nothing but a list number too (lt-8, lt-10: a bullet and a 40 pt shape make the line 40 pt high).
            var (a, b) = Metrics(mark, Vertical);
            largest = a + b;
            if (pictures)
            {
                above = Math.Max(above, a + b);
                raised = Math.Max(raised, a + b);
            }
            any = true;
        }
        else if (any && below == 0 && largest == 0)
        {
            // Only a number on the line: the paragraph mark gives the rest, its ascent too where it is the larger
            // (docx/external/libreoffice/core-text/redline-bullet: an item whose text is all deleted keeps a Courier New
            // 10 pt bullet with the baseline the mark's Aptos 12 pt ascent below the line's top; poi/delins).
            var (a, b) = Metrics(mark, Vertical);
            above = Math.Max(above, a);
            raised = Math.Max(raised, a);
            below = b;
            largest = a + b;
            lowered = Math.Max(lowered, b);
        }
        if (!any)
        {
            // A line of nothing but a page break that goes unseen takes the mark's (measured with docx/local/ubk-12, ubk-14:
            // a break of 18 pt with an 11 pt mark at 1.5 lines makes an 11 pt line's, one of 11 pt with an 18 pt mark an
            // 18 pt one's; docx/external/libreoffice/extras-ooxmlexport/090716_Studentische_Arbeit_VWS).
            var style = end > start && start < charStyles.Count && !spacesOnly && !(unseenBreak && text[start] == '\f') ? charStyles[start] : mark;
            // A line of nothing but spaces and tabs that does not end its paragraph takes its largest one's line (Word's
            // PDF of docx/external/libreoffice/extras-ooxmlexport/tdf137335_whitespaceLineHeight: tabs of 36 and 48 pt and
            // spaces of 72 pt wrapped before the paragraph's last line make a line 72 pt high, the last one the mark's).
            if (spacesOnly && end < text.Length && end > start && start < charStyles.Count)
                style = Enumerable.Range(start, Math.Min(end, charStyles.Count) - start).Select(k => charStyles[k])
                    .MaxBy(s => { var (a, b) = Metrics(s, Vertical); return a + b; }) ?? style;
            // A raised or lowered mark of its own takes its unscaled size's line (docx/external/libreoffice/core-text/
            // empty-superscript: an empty paragraph with a superscript 10 pt mark 12.2 pt high).
            (above, below) = style.ScriptBase is { } markBase ? WordFonts.LineMetrics(style.Font.Data, markBase, style.LineHeights) : Metrics(style, Vertical);
            largest = above + below;
            // A mark raised or lowered (w:position) moves its line as such text does, a superscript or subscript one not
            // (docx/external/powertools/HC061-Hyperlink-in-Field: empty cells of a table style raising its text 3 pt as
            // high as those with text).
            var markLift = context.Profile.NoSpaceRaiseLower ? 0 : style.Raise;
            (raised, lowered) = (above + markLift, below - markLift);
            if (format.TextAlignment is not ("top" or "center" or "bottom"))
                (above, below) = (above + Math.Min(0, markLift), Math.Max(0, below - Math.Max(0, markLift)));
        }
        line.Above = above;
        line.Below = below;
        line.LargestNatural = largest;
        // Characters of different sizes align their tops, centers or bottoms instead of their baselines
        // (w:textAlignment, catalog: paragraph.eastAsian), by their fonts' win ascents and descents (measured with
        // docx/local/ec-none: 12 and 24 pt MS Mincho beside 12 pt Arial).
        if (format.TextAlignment is "top" or "center" or "bottom" && line.Pieces.Count(p => !p.IsTab) > 1)
        {
            static (double Ascent, double Descent) Win(Piece p) => (p.Style.Font.Data.WinAscent * p.Style.Size, p.Style.Font.Data.WinDescent * p.Style.Size);
            var shown = line.Pieces.Where(p => !p.IsTab && p.Drawing == null && p.Equation == null && p.CheckBox == null).ToList();
            if (shown.Count > 1)
            {
                var tallest = shown.MaxBy(p => Win(p).Ascent + Win(p).Descent)!;
                var (ascent, descent) = Win(tallest);
                foreach (var piece in shown)
                {
                    var (a, d) = Win(piece);
                    piece.Shift = format.TextAlignment switch
                    {
                        "top" => ascent - a,
                        "center" => (ascent - descent) / 2 - (a - d) / 2,
                        _ => d - descent,
                    };
                }
            }
        }
        if (format.TextAlignment == "top")
        {
            var shown = line.Pieces.Where(p => !p.IsTab && p.Drawing == null && p.Equation == null && p.CheckBox == null && p.Across == null).ToList();
            // Lowered text aligned by its top goes down only as far as it reaches past its line gap (see TopLift).
            foreach (var piece in shown)
                piece.Shift += TopLift(piece, format) - (context.Profile.NoSpaceRaiseLower ? 0 : piece.Style.Raise - piece.Style.ScriptShift);
            if (shown.Count > 0 && shown.All(p => p.Style.Font.Data == shown[0].Style.Font.Data && Math.Abs(p.Style.Size - shown[0].Style.Size) < 0.01))
            {
                var down = TopDeviceShift(shown[0].Style);
                foreach (var piece in shown)
                    piece.Shift -= down;
            }
        }
        line.RaisedAbove = Math.Max(0, raised - above);
        line.LoweredBelow = Math.Max(0, lowered - below);
        line.RubyAbove = Math.Max(0, rubyRaised - above);
        return line;
    }

    /// <summary>
    /// How far a piece is raised (w:position, less a subscript's shift): where characters align their tops
    /// (w:textAlignment top) lowered text first takes up its font's line gap, going down and making its line taller
    /// only by what it is lowered more (measured with docx/local/lowr-18, lowr-19, lowr-20: 12 pt Arial lowered 6 pt
    /// makes the line 19.4 pt high, 13.8 + 6 less Arial's 0.39 pt gap).
    /// </summary>
    double TopLift(Piece piece, ParagraphFormat format)
    {
        var lift = context.Profile.NoSpaceRaiseLower ? 0 : piece.Style.Raise - piece.Style.ScriptShift;
        if (format.TextAlignment != "top" || lift >= 0)
            return lift;
        var (above, _) = WordFonts.LineMetrics(piece.Style.Font.Data, piece.Style.Size, piece.Style.LineHeights);
        var gap = Math.Max(0, above - WordFonts.Ascent(piece.Style.Font.Data) * piece.Style.Size);
        return Math.Min(0, lift + gap);
    }

    /// <summary>
    /// How much lower a line of one font and size whose characters align their tops (w:textAlignment top) has its
    /// baseline than aligned by baselines, in points: Word sets it on its 600 dpi device the font's whole-pixel ascent as
    /// GDI realizes it (its VDMX record at the size's whole pixels per em, else its win ascent or, with
    /// USE_TYPO_METRICS, its typo ascent rounded) and the rounded line gap below the line's top, less one pixel
    /// (measured with docx/local/tat-top against tat-base: Arial 12 two pixels higher, Times New Roman 12, Calibri 11
    /// and Verdana 9 alike, Cambria 12, Georgia 14 and Aptos 12 one pixel higher, Segoe UI 10 two, Courier New 12 four,
    /// Nyala 11 six lower, here seven; East Asian fonts alike, MS Mincho 12 and Yu Gothic 11; LibreOffice's
    /// extras-ooxmlexport/tdf140668: Nyala 11 lowered 0.5 pt).
    /// </summary>
    static double TopDeviceShift(RunStyle style)
    {
        var font = style.Font.Data;
        if (font.EastAsian)
            return 0;
        var ppem = (int)Math.Round(style.Size * WordDevice.DotsPerPoint, MidpointRounding.AwayFromZero);
        var (above, _) = WordFonts.LineMetrics(font, style.Size, style.LineHeights);
        var ascent = WordFonts.Ascent(font) * style.Size * WordDevice.DotsPerPoint;
        var gap = Math.Max(0, above * WordDevice.DotsPerPoint - ascent);
        var gdiAscent = font.Typo is { Use: true } typo ? (int)Math.Round(typo.Ascender * ppem, MidpointRounding.AwayFromZero) : font.GdiMetrics(ppem).Ascent;
        var shift = Math.Round(gap, MidpointRounding.AwayFromZero) + gdiAscent - 1 - Math.Round(gap + ascent, MidpointRounding.AwayFromZero);
        return shift / WordDevice.DotsPerPoint;
    }

    /// <summary>
    /// Line heights and baselines by the paragraph's line spacing (measured with 14 fonts, docx/local/lm-*): auto adds
    /// to the natural height (the largest ascent and the largest descent of the line's fonts) the multiple's share of
    /// the largest single font's line height, below the text, the baseline where single spacing puts it (measured
    /// with docx/local/lm-mixed: Symbol and Aptos 12 pt at three lines make 44.84 pt, 15.45 + 2 × 14.70, not
    /// 3 × 15.45); exact is the height given with the baseline at 80 % of it whatever the font; at least grows the
    /// line, the extra going above the text. Raised and lowered text adds what it reaches beyond, outside the
    /// multiple (measured with docx/generated/character-formats: text 3 pt up and 3 pt down makes a 1.15 line 6 pt
    /// higher, not 6.95 pt).
    /// </summary>
    /// <summary>The line pitch a paragraph's lines snap to: the section's, unless the paragraph does not snap (w:snapToGrid).</summary>
    double? Pitch(ParagraphFormat format) => format.SnapToGrid == false ? null : LinePitch;

    /// <param name="pitch">
    /// The document grid's line pitch (measured with docx/local/docgrid: 18 and 15 pt grids, Arial 10 to 30 pt): a
    /// line of auto spacing takes as many whole pitches as its natural height needs, times its spacing factor up to
    /// single spacing and the larger of that and the factor's pitches above, the extra half above and half below the
    /// text; exact spacing keeps its height. What ruby and raised or lowered text
    /// reach beyond the line counts in its natural height there (measured with docx/local/rg-*: 11 pt MS Mincho with
    /// ruby 10 pt up, 18.7 pt high, takes two pitches of 18 pt and stands in their middle, its baseline 24.26 pt
    /// down; ruby 5 pt up one pitch; docx/external/poi/61470).
    /// </param>
    /// <param name="fromTop">
    /// Whether exact lines put their text at the top (w:noExtraLineSpacing): the baseline below the natural ascent, at
    /// the line's bottom at most.
    /// </param>
    static void Space(List<Line> lines, ParagraphFormat format, double? pitch = null, bool fromTop = false)
    {
        var rule = format.LineRule ?? "auto";
        var value = format.Line ?? 240;
        foreach (var line in lines)
        {
            var natural = line.Above + line.Below;
            switch (rule)
            {
                case "exact":
                    line.Height = value / 20;
                    line.Baseline = fromTop ? Math.Min(line.Above, line.Height) : 0.8 * line.Height;
                    continue;
                case "atLeast":
                    line.Height = Math.Max(value / 20, natural);
                    line.Baseline = line.Height - line.Below;
                    break;
                case "auto" when pitch is { } grid && grid > 0:
                {
                    var whole = natural + line.RaisedAbove + line.LoweredBelow;
                    var units = Math.Max(1, Math.Ceiling(whole / grid - 1e-6));
                    // Above single spacing the larger of the factor's pitches and the whole pitches the text needs
                    // (measured with docx/local/lgr-20, lgr-40: Arial 20 pt at 1.5, 2 and 3 times an 18 pt grid 36, 36
                    // and 54 pt high, 40 pt 54 pt at all three; docx/external/libreoffice/extras-ooxmlexport/chtoutline).
                    line.Height = value > 240 ? Math.Max(grid * value / 240, units * grid) : units * grid * value / 240;
                    var extra = line.Height - whole;
                    line.Baseline = line.Above + line.RaisedAbove + extra / 2;
                    line.Overhang = Math.Max(0, extra / 2);
                    continue;
                }
                default:
                    // From single spacing on the multiple takes in what ruby reaches above the text, the extra going
                    // below it (measured with docx/local/eql-15, eql-s: MS Mincho 20 pt with 8 pt ruby raised 19 and
                    // 23 pt is 1.15 times its single height at 1.15 lines, with EQ \s\up or w:position as high its
                    // single height and the 20 pt line's extra; docx/external/libreoffice/extras-rtfexport/tdf138779).
                    line.Height = natural + (line.LargestNatural + (value >= 240 ? line.RubyAbove : 0)) * (value / 240 - 1);
                    // Below single spacing the line loses its share above the baseline and below it alike (measured
                    // with docx/local/linemult-120, -156, -192: Arial 10 pt, Times New Roman and Traditional Arabic
                    // 20 pt at 0.5, 0.65 and 0.8 put their baselines at that share of their single ones, the line
                    // after them as far below); from single spacing on the extra goes below the text.
                    line.Baseline = value is > 0 and < 240 && natural > 0 ? line.Above * line.Height / natural : line.Above;
                    line.Overhang = Math.Max(0, line.Height - natural);
                    break;
            }
            line.Height += line.RaisedAbove + line.LoweredBelow;
            line.Baseline += line.RaisedAbove;
        }
    }

    /// <summary>
    /// The share of a line's measure that left-to-right text with kashida justification breaks on, the line then
    /// stretched over the whole of it (measured with docx/local/ksh-*: English paragraphs in Calibri and Times New Roman
    /// at 11 to 20 pt, 200 and 451 pt wide, indented, in modes 14 and 15: mediumKashida lines break on 0.9 of the measure,
    /// highKashida on 0.8, lowKashida as justified ones); null for other paragraphs.
    /// </summary>
    static double? KashidaShare(ParagraphFormat format) => format.Bidi == true ? null
        : format.Justification switch { "mediumKashida" => 0.9, "highKashida" => 0.8, _ => null };

    /// <summary>
    /// Arabic words lengthened by kashidas (U+0640) for medium and high kashida justification (w:jc mediumKashida,
    /// highKashida; catalog: paragraph.basic), as Word inserts them (measured with docx/local/kashida: two or three
    /// in every word, the lines breaking with them): at one place a word joins, chosen by the priorities of Arabic
    /// justification: after seen or sad, before a final teh marbuta, heh or dal, before a final alef, tah, lam or
    /// kaf, before a medial beh, before a final waw, ain, qaf or feh, before any other final letter; the last such
    /// place of a word where several have the same priority.
    /// </summary>
    static string Kashidas(string value, int count)
    {
        static bool Arabic(char c) => c is >= '\u0620' and <= '\u06FF' && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark;
        static bool RightJoining(char c) => c is '\u0622' or '\u0623' or '\u0624' or '\u0625' or '\u0627' or '\u0629' or '\u062F' or '\u0630'
            or '\u0631' or '\u0632' or '\u0648' or '\u0671' or '\u0672' or '\u0673' or '\u0675' or '\u0676' or '\u0677'
            or >= '\u0688' and <= '\u0699' or '\u06C0' or >= '\u06C3' and <= '\u06CB' or '\u06CD' or '\u06CF' or '\u06D2'
            or '\u06D3' or '\u06D5' or '\u06EE' or '\u06EF';
        static bool Joins(char c) => Arabic(c) && c is not ('\u0621' or '\u0674' or '\u06DD');
        static bool JoinsLeft(char c) => Joins(c) && !RightJoining(c);
        if (!value.Any(Arabic))
            return value;
        var sb = new StringBuilder(value.Length + 16);
        var start = 0;
        while (start < value.Length)
        {
            var end = start;
            while (end < value.Length && !char.IsWhiteSpace(value[end]))
                end++;
            // The letters of the word (marks stay with theirs).
            var letters = Enumerable.Range(start, end - start).Where(i => Joins(value[i])).ToList();
            var (best, at) = (int.MaxValue, -1);
            for (var k = 0; k + 1 < letters.Count; k++)
            {
                var (a, b) = (value[letters[k]], value[letters[k + 1]]);
                if (letters[k + 1] - letters[k] > 1 && Enumerable.Range(letters[k] + 1, letters[k + 1] - letters[k] - 1).Any(i => !char.IsWhiteSpace(value[i]) && CharUnicodeInfo.GetUnicodeCategory(value[i]) != UnicodeCategory.NonSpacingMark))
                    continue;
                if (!JoinsLeft(a) || a == '\u0644' && b is '\u0622' or '\u0623' or '\u0625' or '\u0627')
                    continue;
                var final = RightJoining(b) || k + 2 >= letters.Count;
                var priority = a is '\u0633' or '\u0634' or '\u0635' or '\u0636' ? 1
                    : final && b is '\u0629' or '\u0647' or '\u062F' or '\u0630' ? 2
                    : final && b is '\u0627' or '\u0622' or '\u0623' or '\u0625' or '\u0671' or '\u0637' or '\u0638' or '\u0644' or '\u0643' or '\u06A9' or '\u06AF' ? 3
                    : !final && b is '\u0628' or '\u062A' or '\u062B' or '\u0646' or '\u064A' or '\u0626' or '\u067E' or '\u06CC' ? 4
                    : final && b is '\u0648' or '\u0639' or '\u063A' or '\u0642' or '\u0641' ? 5
                    : final ? 6 : int.MaxValue;
                if (priority <= best && priority != int.MaxValue)
                    (best, at) = (priority, letters[k + 1]);
            }
            for (var i = start; i < end; i++)
            {
                if (i == at)
                    sb.Append('\u0640', count);
                sb.Append(value[i]);
            }
            while (end < value.Length && char.IsWhiteSpace(value[end]))
                sb.Append(value[end++]);
            start = end;
        }
        return sb.ToString();
    }

    /// <summary>
    /// A line may end at a space before any character but East Asian ones, whose kinsoku rules hold: Word breaks
    /// before periods, closing brackets, quotes, slashes, percent signs and the like after a space too (measured with
    /// docx/local/spacebreak: 19 characters; docx/external/libreoffice/extras-ooxmlexport/tdf95377 "indent .5/margin").
    /// </summary>
    static void BreakAtSpaces(string text, bool[] breaks)
    {
        for (var i = 0; i + 1 < text.Length; i++)
        {
            if (text[i] is not (' ' or '\t') || text[i + 1] is ' ' or '\t' or '\n' or '\f' or '\v' or ' ' || EastAsian(text[i + 1]))
                continue;
            // Spaces with a no-break space among them give no break (measured with docx/local/nb-15, nb-14: "X)" and
            // NBSP SP, SP NBSP or NBSP NBSP SP before a word too long for the line keep it there, broken by characters,
            // SP and SP SP not; docx/external/libreoffice/extras-ooxmlexport/tdf134606).
            var start = i;
            while (start > 0 && text[start - 1] is ' ' or ' ')
                start--;
            if (text[i] == ' ' && text.AsSpan(start, i - start).Contains(' '))
                continue;
            breaks[i] = true;
        }
    }

    /// <summary>
    /// A line may end after a hyphen or an en dash whatever stands before it, digits, punctuation or a space, and after
    /// the hyphen U+2010 from Word 2013 on (measured with docx/local/hyphbreak-15 and -12: "10-" | "08", "1)-" | "08",
    /// "1 -" | "08"; not after a slash, plus sign, underscore, period or comma between digits, nor a non-breaking
    /// hyphen; docx/external/poi/bug65649: "ТЕРм10-08-" | "001-02" in a narrow cell), also before Latin characters that
    /// may not begin a line (docx/local/hkn-*: "ametur-" | "%…", before ) . ; ! ] ° ? , : alike, in modes 14 and 15,
    /// with and without w:useAltKinsokuLineBreakRules; docx/external/libreoffice/extras-ooxmlexport/fdo81492).
    /// </summary>
    void BreakAfterHyphens(string text, bool[] breaks, LineBreaker.Kinsoku rules, int labelEnd)
    {
        for (var i = labelEnd; i + 1 < text.Length; i++)
        {
            // Not after a non-breaking hyphen, a hyphen with a word joiner (docx/external/powertools/RP/RP038), nor
            // before East Asian characters kinsoku keeps off a line's start.
            if ((text[i] is '-' or '\u2013' || text[i] == '\u2010' && context.Profile.BreaksAfterHyphen)
                && text[i + 1] is not (' ' or '\t' or '\n' or '\f' or '\v' or '\u2060') && !(text[i + 1] >= '\u2E80' && rules.NotFirst.Contains(text[i + 1])))
                breaks[i] = true;
        }
    }

    /// <summary>
    /// A line may end before and after an inline picture, as beside an ideograph: not before a space or a character
    /// that may not begin a line, not after one that may not end it, nor between the list number and the picture
    /// (measured with docx/local/tblpic3: P2's first line ends right before its picture; in table I a word right after
    /// a picture widens no column, nor in docx/external/powertools/WC/WC023-Table-4-Row-Image-Before).
    /// </summary>
    void BreakAroundObjects(string text, bool[] breaks, LineBreaker.Kinsoku rules, int labelEnd)
    {
        foreach (var index in objects.Keys)
        {
            if (index < labelEnd)
                continue;
            if (index > labelEnd && !rules.NotLast.Contains(text[index - 1]))
                breaks[index - 1] = true;
            if (index + 1 < text.Length && text[index + 1] is not (' ' or '\t') && !rules.NotFirst.Contains(text[index + 1]))
                breaks[index] = true;
        }
    }

    /// <summary>
    /// The side of an East Asian punctuation mark that punctuation compression squeezes: 1 the right of commas, stops,
    /// colons, semicolons, exclamation marks and closing brackets, -1 the left of opening brackets, 0 none (measured with
    /// docx/local/cpt-marks: ，。、；：！ and ）》】』〉〕」 on the right, （《【『〈〔「 on the left; ？, quotes, ・, … and ～ not).
    /// </summary>
    static int Squeezable(char c) => c switch
    {
        '\uFF0C' or '\u3002' or '\u3001' or '\uFF1B' or '\uFF1A' or '\uFF01' or '\uFF09' or '\u300B' or '\u3011' or '\u300F' or '\u3009' or '\u3015' or '\u300D' => 1,
        '\uFF08' or '\u300A' or '\u3010' or '\u300E' or '\u3008' or '\u3014' or '\u300C' => -1,
        _ => 0,
    };

    /// <summary>Half the width of a full-width closing mark that punctuation compression lets hang (see Squeezable), else 0.</summary>
    static double HalfMark(string text, double[] advances, List<RunStyle> styles, int k) =>
        Squeezable(text[k]) > 0 && k < styles.Count && advances[k] >= 0.9 * styles[k].Size ? advances[k] / 2 : 0;

    /// <summary>
    /// Whether a justified line with punctuation compression (w:characterSpacingControl compressPunctuation) takes the
    /// content from <paramref name="from"/> to <paramref name="end"/> that reaches <paramref name="overflow"/> points past
    /// its end: a closing mark ending it hangs half its width past the end, and the line's other full-width East Asian
    /// punctuation (Squeezable; not an opening bracket that begins the line) squeezes alike by the rest, each by half its
    /// width at most; from Word 2013 on each by no more than 32 times what each gap of the line without the content would
    /// stretch to fill its <paramref name="free"/> room, before by half a mark in all (measured with docx/local/cpq-*,
    /// cpr-*, cps-a, cpt-*, cpu-*: Microsoft YaHei lines of 10 to 36 characters with 1 to 5 marks 1 to 16 pt over, at 12
    /// and 10.5 pt, in modes 15 and 14: with two commas a line of 20 characters takes 9.3 pt, not 9.4, of 30 8.3, not
    /// 8.4; a stop ending a line 1 pt over hangs, the line stretching by 5 pt; left aligned lines take nothing).
    /// </summary>
    /// <returns>Where a mark hangs half (-1 for none) and whether marks squeeze; null where the line does not take it.</returns>
    static (int HalfHang, bool Squeezed)? CompressesPunctuation(string text, double[] advances, List<RunStyle> styles, int lineStart, int from,
        int end, double overflow, double free, bool modern)
    {
        bool Full(int k) => k < styles.Count && advances[k] >= 0.9 * styles[k].Size;
        var halfHang = -1;
        if (Squeezable(text[end - 1]) > 0 && Full(end - 1))
        {
            halfHang = end - 1;
            overflow -= advances[end - 1] / 2;
        }
        if (overflow <= 0.005)
            return (halfHang, false);
        var (count, capacity) = (0, double.MaxValue);
        for (var k = lineStart; k < end; k++)
        {
            var side = Squeezable(text[k]);
            if (k == halfHang || side == 0 || side < 0 && k == lineStart || !Full(k))
                continue;
            count++;
            capacity = Math.Min(capacity, advances[k] / 2);
        }
        if (count == 0 || overflow / count > capacity + 0.005)
            return null;
        // The gaps between the characters of the line without the content, which justification would stretch.
        var shortEnd = from;
        while (shortEnd > lineStart && text[shortEnd - 1] == ' ')
            shortEnd--;
        var gaps = shortEnd - lineStart - 1;
        if (modern ? gaps <= 0 || overflow / count > 32 * free / gaps + 1e-6 : overflow > capacity + 0.005)
            return null;
        return (halfHang, true);
    }

    /// <summary>
    /// Squeezes a line's East Asian punctuation alike by <paramref name="amount"/> in all (see CompressesPunctuation): a
    /// closing mark's advance, the advance before an opening one; not a last mark that hangs.
    /// </summary>
    /// <returns>Whether the line had punctuation to squeeze.</returns>
    static bool SqueezePunctuation(List<Piece> pieces, double amount, bool hangs)
    {
        var chars = new List<(Piece Piece, int Index)>();
        foreach (var piece in pieces)
        {
            if (piece.Drawing != null || piece.Equation != null || piece.CheckBox != null || piece.Ruby != null || piece.Combined != null || piece.Across != null)
            {
                chars.Add((piece, -1));
                continue;
            }
            for (var k = 0; k < piece.Text.Length; k++)
                chars.Add((piece, k));
        }
        var count = chars.Count;
        while (count > 0 && chars[count - 1].Index >= 0 && chars[count - 1].Piece.Text[chars[count - 1].Index] == ' ')
            count--;
        if (hangs && count > 0)
            count--;
        var marks = new List<(Piece Piece, int Index)>();
        for (var n = 0; n < count; n++)
        {
            var (piece, index) = chars[n];
            if (index < 0 || piece.Advances[index] < 0.9 * piece.Style.Size)
                continue;
            var side = Squeezable(piece.Text[index]);
            if (side > 0)
                marks.Add((piece, index));
            else if (side < 0 && n > 0 && chars[n - 1].Index >= 0)
                marks.Add(chars[n - 1]);
        }
        if (marks.Count == 0)
            return false;
        foreach (var (piece, index) in marks)
            piece.Advances[index] -= amount / marks.Count;
        var x = pieces[0].X;
        foreach (var piece in pieces)
        {
            piece.X = x;
            x += piece.Width;
        }
        return true;
    }

    /// <summary>Characters justification treats as East Asian: ideographs, kana, East Asian punctuation and full-width forms.</summary>
    internal static bool EastAsian(char c) => c >= '\u2E80' && (c < '\uA000' || c is >= '\uF900' and <= '\uFAFF' or >= '\uFE30' and <= '\uFE4F' or >= '\uFF00' and <= '\uFFEF');

    /// <summary>
    /// Justification of lines with East Asian text and distributed lines (measured with docx/local/cjk-justify-lines,
    /// cjk-justify15 and ea-autospace: Arial with MS Mincho at 11, 16 and 22 pt, Times New Roman with MS Gothic,
    /// Calibri with Yu Mincho): the free width goes in equal parts after every East Asian character and before every
    /// one, after every space (the letters of a Latin word stay together), and after every character of a distributed
    /// line; a space gets 0.45 of its own width more, and the automatic space between East Asian and Latin text
    /// doubles. The last character of the line and the spaces after it take none. From Word 2013 on a line with less
    /// room than those extras widens its spaces first, then the boundaries, the gaps between ideographs keeping their
    /// pitch (measured with docx/local/cjv-*, cjx-*: Microsoft YaHei lines of ideographs and "ABC def ghi" 0.5 and 2 pt
    /// wider in turn, the spaces up to 1.5 times their width, then the boundaries' 3 pt up to 6, then every gap alike;
    /// dst4-s68: a distributed Latin line's letters keep their pitch; pandoc/instrText_hyperlink: ideographs 12 pt apart,
    /// spaces 5.38 pt); before, it spreads every gap alike (docx/local/cgw14-lat).
    /// </summary>
    /// <returns>Whether the line was justified so (false where nothing can take the width).</returns>
    static bool Distribute(List<Piece> pieces, double free, bool all, bool hangs, Func<Piece, int, double>? doubled, bool ideographicTrails = false,
        double spaceShare = 0.45, bool phased = false)
    {
        var chars = new List<(Piece Piece, int Index)>();
        foreach (var piece in pieces)
        {
            if (piece.Drawing != null || piece.Equation != null || piece.CheckBox != null || piece.Ruby != null || piece.Combined != null || piece.Across != null)
            {
                chars.Add((piece, -1));
                continue;
            }
            for (var k = 0; k < piece.Text.Length; k++)
            {
                if (!char.IsLowSurrogate(piece.Text[k]))
                    chars.Add((piece, k));
            }
        }
        char At(int n) => chars[n].Index < 0 ? '\uFFFC' : chars[n].Piece.Text[chars[n].Index];
        // The line's content ends before its trailing spaces and its hanging punctuation.
        var count = chars.Count;
        while (count > 0 && (At(count - 1) == ' ' || ideographicTrails && At(count - 1) == (char)0x3000))
            count--;
        if (hangs && count > 0)
            count--;
        var gaps = new List<(int N, double Fixed)>();
        var (spaceTotal, boundaryTotal) = (0.0, 0.0);
        var spaceExtras = new Dictionary<int, double>();
        for (var n = 0; n + 1 < count; n++)
        {
            var (c, next) = (At(n), At(n + 1));
            if (!all && !(c == ' ' || EastAsian(c) || EastAsian(next)))
                continue;
            var (space, boundary) = (0.0, 0.0);
            if (c == ' ' && chars[n].Index >= 0)
                space = spaceShare * chars[n].Piece.Advances[chars[n].Index];
            if (doubled != null && chars[n].Index >= 0)
                boundary = doubled(chars[n].Piece, chars[n].Index);
            gaps.Add((n, space + boundary));
            spaceExtras[n] = space;
            (spaceTotal, boundaryTotal) = (spaceTotal + space, boundaryTotal + boundary);
        }
        if (gaps.Count == 0)
            return false;
        // The spaces' extras fill first, then the boundaries', each in its part; what is left goes to every gap alike.
        // Before Word 2013 extras the line has no room for go.
        if (phased)
        {
            var spaceScale = spaceTotal > 0 ? Math.Min(1, free / spaceTotal) : 0;
            var boundaryScale = boundaryTotal > 0 ? Math.Clamp((free - spaceTotal) / boundaryTotal, 0, 1) : 0;
            gaps = gaps.Select(g => (g.N, spaceExtras[g.N] * spaceScale + (g.Fixed - spaceExtras[g.N]) * boundaryScale)).ToList();
        }
        else if (spaceTotal + boundaryTotal > free)
            gaps = gaps.Select(g => (g.N, 0.0)).ToList();
        var fixedTotal = gaps.Sum(g => g.Fixed);
        var share = Math.Max(0, free - fixedTotal) / gaps.Count;
        foreach (var (n, extra) in gaps)
        {
            var (piece, index) = chars[n];
            var at = index < 0 ? piece.Advances.Length - 1 : index;
            if (at >= 0)
                piece.Advances[at] += share + extra;
        }
        var x = pieces[0].X;
        foreach (var piece in pieces)
        {
            piece.X = x;
            x += piece.Width;
        }
        return true;
    }

    /// <summary>Text set with kerning or ligatures, which a distributed line does not spread between its letters.</summary>
    static bool Shaped(RunStyle style) =>
        style.Features.Any(f => f is "kern" or "liga" or "clig" or "calt" or "hlig" or "dlig");

    static bool Justified(ParagraphFormat format) =>
        format.Justification is "both" or "distribute" or "lowKashida" or "mediumKashida" or "highKashida" or "thaiDistribute";

    /// <summary>Alignment (catalog: paragraph.basic): left, centered, right, justified (not the last line) and distributed.</summary>
    /// <summary>
    /// The bidirectional levels of a paragraph's text (UAX #9 through <see cref="Bidi.Levels"/>), or null where all of
    /// it reads left to right: the paragraph's direction (w:bidi) is the base, neutrals of right-to-left runs (w:rtl,
    /// w:dir) read right to left, and runs inside a w:bdo take its direction whatever their characters. In a
    /// left-to-right paragraph right-to-left runs are embeddings of their own, reversed as a whole ("abc def!" in a
    /// w:rtl run shows "!abc def", measured with docx/local/bidi).
    /// </summary>
    static byte[]? BidiLevels(string text, List<RunStyle> styles, bool rightToLeft, int labelLength = 0)
    {
        if (!rightToLeft && !styles.Any(s => s.RightToLeft || s.Override == "rtl") && !Bidi.HasRightToLeft(text))
            return null;
        // Neutrals between text of both directions read as their run does: right to left in w:rtl runs, left to right in
        // the others, also in right-to-left paragraphs (Word's PDFs of docx/external/libreoffice/extras-ooxmlexport/
        // fdo43093b: "$" of a run without w:rtl stays after its "M"; extras-ooxmlimport/tdf129659: the period after
        // "Lemberg." in such a run stays at its end); a list number's as the paragraph does (docx/local/rtlnum-e:
        // "a." before Hebrew text in a right-to-left paragraph reads ".a").
        var levels = Bidi.Levels(text, rightToLeft, i => i < labelLength ? null : i < styles.Count && styles[i].RightToLeft);
        if (!rightToLeft)
        {
            for (var i = 0; i < text.Length && i < styles.Count;)
            {
                if (!styles[i].RightToLeft || text[i] is '\t' or '\n' or '\f' or '\v')
                {
                    i++;
                    continue;
                }
                var j = i;
                while (j < text.Length && j < styles.Count && styles[j].RightToLeft && text[j] is not ('\t' or '\n' or '\f' or '\v'))
                    j++;
                Bidi.Levels(text[i..j], true).CopyTo(levels, i);
                i = j;
            }
        }
        // A left-to-right embedding (w:dir ltr) in a right-to-left paragraph reads as one, its levels two up (UAX #9 X3;
        // LibreOffice's extras-ooxmlexport/tdf119143: an Arabic paragraph from "يتكلّم" on in such an embedding, its
        // brackets around Latin text and its numbers placed as in it).
        if (rightToLeft)
        {
            for (var i = 0; i < text.Length && i < styles.Count;)
            {
                if (!styles[i].LeftToRightEmbedding || text[i] is '\t' or '\n' or '\f' or '\v')
                {
                    i++;
                    continue;
                }
                var j = i;
                while (j < text.Length && j < styles.Count && styles[j].LeftToRightEmbedding && text[j] is not ('\t' or '\n' or '\f' or '\v'))
                    j++;
                // Its neutrals resolve as UAX #9 says, whatever their runs' direction, an explicit formatting character
                // in it standing as the embedding's own direction (Word's PDF of tdf119143: the comma after a stray
                // U+202C left to right between Arabic words).
                var from = i;
                var embedded = string.Concat(text[from..j].Select(ch => ch is >= '‪' and <= '‮' or >= '⁦' and <= '⁩' ? 'a' : ch));
                var inner = Bidi.Levels(embedded, false);
                for (var k = 0; k < inner.Length; k++)
                    levels[from + k] = (byte)(inner[k] + 2);
                i = j;
            }
        }
        var baseLevel = rightToLeft ? 1 : 0;
        for (var i = 0; i < text.Length && i < styles.Count; i++)
        {
            if (styles[i].Override is "rtl")
                levels[i] = 1;
            else if (styles[i].Override is "ltr")
                levels[i] = (byte)(baseLevel == 1 ? 2 : 0);
        }
        return levels;
    }

    /// <summary>
    /// A line in visual order: between its tabs its pieces are reordered by their levels (UAX #9 L2); a right-to-left
    /// paragraph's line, laid out from the start edge, is mirrored across <paramref name="width"/>, so that its start
    /// indent, its tab stops and its alignment count from the right and its trailing spaces hang past the left margin.
    /// </summary>
    static void Visual(Line line, double width, bool rightToLeft)
    {
        if (rightToLeft)
        {
            line.Alignment = null;
            if (line.Cut is { } cut)
                line.Cut = cut.Select(s => (width - s.End, width - s.Start)).Reverse().ToList();
            if (line.Room is { } room)
                line.Mirrored = (room.Justification, width - room.End, width - room.Start, room.Width);
        }
        var pieces = line.Pieces;
        // Spaces ending the line take the paragraph's level, at the line's end (UAX #9 L1; measured with docx/local/
        // rtt-14, rtt-15: "Final " of a left-to-right run ending a line of a centred, right or left aligned right-to-left
        // paragraph stands a space further right than its run alone would put it; docx/external/libreoffice/
        // extras-ooxmlexport/FDO76312): pieces of nothing but them change level, a run that ends with them keeps its
        // text where it stands without them.
        var baseLevel = (byte)(rightToLeft ? 1 : 0);
        var trailing = new HashSet<Piece>(ReferenceEqualityComparer.Instance);
        Piece? ending = null;
        var endingWidth = 0.0;
        for (var k = pieces.Count - 1; k >= 0 && !pieces[k].IsTab; k--)
        {
            var piece = pieces[k];
            if (piece.Text.Length == 0)
                continue;
            if (piece.Text.All(c => c == ' '))
            {
                trailing.Add(piece);
                continue;
            }
            var spaces = piece.Text.Length - piece.Text.TrimEnd(' ').Length;
            if (spaces > 0 && piece.Level % 2 != baseLevel && piece.Advances.Length == piece.Text.Length)
                (ending, endingWidth) = (piece, piece.Advances.Skip(piece.Advances.Length - spaces).Sum());
            break;
        }
        for (var i = 0; i < pieces.Count;)
        {
            if (pieces[i].IsTab)
            {
                if (rightToLeft)
                    pieces[i].X = width - pieces[i].X - pieces[i].Width;
                i++;
                continue;
            }
            var j = i;
            while (j < pieces.Count && !pieces[j].IsTab)
                j++;
            var segment = pieces.GetRange(i, j - i);
            var (start, end) = (segment[0].X, segment[^1].X + segment[^1].Width);
            var x = rightToLeft ? width - end : start;
            foreach (var k in Bidi.Reorder(segment.Select(p => trailing.Contains(p) ? baseLevel : p.Level).ToList()))
            {
                segment[k].X = x;
                x += segment[k].Width;
            }
            if (ending != null && segment.Contains(ending))
                ending.X += rightToLeft ? endingWidth : -endingWidth;
            i = j;
        }
    }

    /// <param name="expandBreaks">
    /// Whether justified lines that end with a manual line break are stretched, as Word does unless the document says
    /// w:doNotExpandShiftReturn (catalog: settings.compatibility, measured with docx/local/compat15-*).
    /// </param>
    /// <summary>How justification spreads a line: over its spaces only, also around East Asian characters, or between all characters.</summary>
    enum Spread { None, EastAsian, All }

    /// <param name="doubled">What justification adds at a boundary between East Asian and Latin text after a character.</param>
    /// <param name="edge">The paragraph's own right edge, which centered and right aligned lines a tab stop took past it align by.</param>
    /// <param name="spaceShare">What a space takes beyond the other characters of a line spread between all of them, in its own width.</param>
    /// <param name="snapCell">The cell of a character grid of snapToChars the paragraph snaps to (see <see cref="SnapCell"/>).</param>
    static void Align(Line line, ParagraphFormat format, double limit, double startX, bool expandBreaks, Func<Piece, int, double>? doubled = null,
        Spread spread = Spread.None, double? edge = null, bool ideographicTrails = false, double spaceShare = 0.45, double? snapCell = null, bool phased = false)
    {
        var content = line.Pieces.Where(p => !p.IsTab).ToList();
        if (content.Count == 0)
            return;
        // The line's end without trailing spaces and hanging punctuation; spaces a tab follows do not trail
        // (docx/external/libreoffice/extras-ooxmlexport/tdf168607_tabstopZero: "10", a space and tabs of no width
        // centered with the space).
        var last = content[^1];
        var trailing = 0.0;
        var tabbed = line.Pieces.Skip(line.Pieces.IndexOf(last) + 1).Any(p => p.IsTab);
        // Trailing spaces run back over the pieces of nothing but spaces before the last, runs of other formatting
        // (measured with docx/external/libreoffice/extras-uiwriter/MsWordCompTrailingBlanks: "TEST" centered and right
        // aligned before spaces in runs of several shadings).
        var trailingSpaces = new Dictionary<Piece, int>(ReferenceEqualityComparer.Instance);
        for (var p = line.Pieces.Count - 1; !tabbed && p >= 0; p--)
        {
            var piece = line.Pieces[p];
            if (piece.IsTab || piece.Drawing != null)
                break;
            var (count, plain) = (0, 0);
            for (var k = piece.Text.Length - 1; k >= 0 && (piece.Text[k] == ' ' || ideographicTrails && piece.Text[k] == (char)0x3000); k--, count++)
            {
                trailing += piece.Advances[k];
                plain += piece.Text[k] == ' ' ? 1 : 0;
            }
            if (plain > 0)
                trailingSpaces[piece] = plain;
            if (count < piece.Text.Length || piece.Text.Length == 0)
                break;
        }
        var end = last.X + last.Width - trailing - line.Hanging;
        var free = limit - end;
        // Right-to-left paragraphs are laid out from their start edge and mirrored afterwards (Visual): left and
        // start align at the start, right and end at the end (measured with docx/local/bidi).
        var jc = format.Justification ?? "left";
        line.Room = (jc, startX, Math.Min(limit, edge ?? limit), end - line.Pieces[0].X);
        // Centered and right aligned lines are laid out as left aligned, tabs and list numbers with them, and move as
        // a whole (measured with docx/local/numcenter: "a", a tab and "b" centered with the tab to the first default
        // stop after the line's start; a number, its tab to the indent and the text, its following lines from the
        // indent; docx/external/libreoffice/extras-ooxmlexport/tdf135774_numberingShading), by the paragraph's own
        // right edge also where a stop took the line past it (docx/external/powertools/DA034-HeaderFooter: a centered
        // footer, a tab to a right stop at the margin and text reaching past it stays where the tab put it).
        // Justified lines spread only the text after their last tab.
        var lastTab = line.Pieces.FindLastIndex(p => p.IsTab);
        var movable = line.Pieces.Skip(lastTab + 1).ToList();
        var room = Math.Min(limit, edge ?? limit) - end;
        double shift = jc switch
        {
            "center" => room / 2,
            "right" or "end" => room,
            _ => 0,
        };
        // On a character grid of snapToChars a paragraph's last line of Latin text stands centered in the whole cells
        // it needs from its start, however the paragraph is aligned (see SnapCell).
        if (snapCell is { } cell && cell > 0.01 && line.Last && lastTab < 0 && end > line.Pieces[0].X && !line.Pieces.Any(p => p.Text.Any(EastAsian)))
        {
            var width = end - line.Pieces[0].X;
            shift = Math.Max(0, (Math.Ceiling(width / cell - 1e-6) * cell - width) / 2);
            foreach (var piece in line.Pieces)
                piece.X += shift;
            return;
        }
        if (shift > 0)
        {
            foreach (var piece in line.Pieces)
                piece.X += shift;
            if (content[0] == line.Pieces[0])
                line.Alignment = (jc == "center", line.Pieces[0].X - shift, Math.Min(limit, edge ?? limit), end - (line.Pieces[0].X - shift));
            return;
        }
        var justified = jc is "both" or "lowKashida" or "mediumKashida" or "highKashida" or "distribute" or "thaiDistribute";
        if (justified && line.PunctuationSqueezed && free < 0 && SqueezePunctuation(movable, -free, line.Hanging > 0))
            return;
        // The last line of a justified paragraph keeps its natural spacing, unless it took a word by squeezing.
        var justify = justified && !line.Natural && (!line.Last || jc is "distribute" or "thaiDistribute" || free < 0) && (expandBreaks || line.EndBreak != "line" || free < 0);
        // A line whose punctuation hangs past its end fills it, aligned as it may be (measured with
        // docx/local/c4-kinsoku12: lines of a left aligned paragraph in compatibility mode 12 spread their characters).
        if (line.Hanging > 0 && free > 0 && shift == 0 && Distribute(movable, free, false, !line.HangsHalf, doubled, ideographicTrails, spaceShare, phased))
            return;
        if (!justify || Math.Abs(free) < 1e-9)
            return;
        var lineSpaces = movable.Sum(p => p.Text.Count(c => c == ' ')) - trailingSpaces.Values.Sum();
        // Lines spread around East Asian text from two such characters on, with one only their spaces as Latin lines
        // (measured with docx/local/cjkr-*: Latin words with one ideograph stretch their spaces alike, with two or four
        // every gap beside them; cjky-*, cjkz-*: ideographs with spaces between them or Latin words spread every gap;
        // pandoc/nested_instrText, instrText_hyperlink, empty_field).
        // Distributed lines of Latin text spread over every character too, unless their text is kerned or takes
        // ligatures, which keeps it together as justified text where it has spaces (measured with docx/local/dst-*,
        // dst2-*, dst3-two, dst4-*: every line of the paragraph, full or not, each space 0.44 of its width more than a
        // letter in mode 15, dst6-*: 0.6 in modes 11 to 14; dst5-kern, dst5-lig and docx/generated/paragraph-alignment:
        // spaces only; docx/external/libreoffice/extras-ooxmlexport/para-adjust-distribute).
        var everyCharacter = jc is "distribute" or "thaiDistribute" && (lineSpaces <= 0 || !movable.Any(p => p.Text.Length > 0 && Shaped(p.Style)));
        // A line of nothing but inline objects spreads between them where lines spread around East Asian text (measured
        // with docx/local/ojx-ea, ojx-lat: five rectangles 13.2 pt apart with SimSun as the document's East Asian font,
        // none without; beside Latin text objects take nothing; tika/testWPSAttachment: OLE icons 85.69 pt apart).
        var objectsOnly = spread == Spread.EastAsian && movable.All(p => p.Drawing != null || p.Text.Length == 0) && movable.Count(p => p.Drawing != null) >= 2;
        if (free > 0 && (spread == Spread.All || spread == Spread.EastAsian && movable.Where(p => p.Drawing == null).Sum(p => p.Text.Count(EastAsian)) >= 2
                || everyCharacter || objectsOnly)
            && Distribute(movable, free, spread == Spread.All || everyCharacter || objectsOnly, line.Hanging > 0 && !line.HangsHalf, doubled, ideographicTrails, spaceShare, phased))
            return;
        // The spaces between words share the free width: not those that end the line, nor those it begins with
        // (measured with docx/local/jls-*: one to three spaces before a justified paragraph's first word, and one after
        // a line break, stay 25 dots wide in Times New Roman 12 pt while the line's other spaces widen, in mode 14 too;
        // docx/external/poi/bug57031).
        var leading = new Dictionary<Piece, int>(ReferenceEqualityComparer.Instance);
        for (var p = 0; lastTab < 0 && p < movable.Count; p++)
        {
            var count = 0;
            while (count < movable[p].Text.Length && movable[p].Text[count] == ' ')
                count++;
            if (count > 0)
                leading[movable[p]] = count;
            if (count < movable[p].Text.Length || movable[p].Drawing != null)
                break;
        }
        var spaces = 0;
        foreach (var piece in movable)
            spaces += piece.Text.Count(c => c == ' ') - leading.GetValueOrDefault(piece);
        spaces -= trailingSpaces.Values.Sum();
        if (spaces <= 0)
            return;
        var extra = free / spaces;
        var x = movable.Count > 0 ? movable[0].X : startX;
        var remaining = spaces;
        foreach (var piece in movable)
        {
            piece.X = x;
            var own = piece.Text.Count(c => c == ' ') - leading.GetValueOrDefault(piece);
            own -= trailingSpaces.GetValueOrDefault(piece);
            own = Math.Min(Math.Max(0, own), remaining);
            remaining -= own;
            piece.SpaceExtra = extra;
            piece.JustifiedSpaces = own;
            piece.UnspreadSpaces = leading.GetValueOrDefault(piece);
            x += piece.Width;
        }
    }
}
