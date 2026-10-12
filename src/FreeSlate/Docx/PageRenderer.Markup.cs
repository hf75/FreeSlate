using System.Text;
using System.Xml.Linq;
using FreeSlate.Docx.Layout;
using FreeSlate.Drawing;

namespace FreeSlate.Docx;

/// <summary>
/// Word's markup view as it prints with markup (catalog: review.revisions, review.comments; measured with
/// docx/local/markup-a and docx/generated/revisions, Word 365): comment ranges shaded in their author's lighter color
/// between thin brackets; a change bar half an inch left of the text beside every line with a tracked change; beside
/// the page a gray pane where balloons show the comments and the formatting changes, each joined to its place in the
/// text by a dotted line, stacked in the order of their places, each as high as its text.
/// </summary>
public sealed partial class PageRenderer
{
    /// <summary>The document's comments in the markup view, by id.</summary>
    public Dictionary<string, Comment> Comments { get; init; } = [];

    /// <summary>Whether the pages have the markup pane beside them: the document has balloons (comments or formatting changes).</summary>
    public bool MarkupPane { get; init; }

    /// <summary>The pane's distance from the text's right edge and its width (0.1 in and 3.6 in).</summary>
    const double PaneGap = 7.2, PaneWidth = 259.2;

    /// <summary>A balloon's distance from the pane's left edge, its width and the space between two balloons.</summary>
    const double BalloonLeft = 23.76, BalloonWidth = 232.97, BalloonGap = 1.15;

    /// <summary>Where the dotted line reaches a balloon below its top; the text's insets in the balloon.</summary>
    const double BalloonAttach = 6, BalloonInsetX = 4.54, BalloonInsetTop = 2.3, BalloonInsetBottom = 1.81;

    /// <summary>Change bars: half an inch left of the text, 0.75 pt wide.</summary>
    const double BarOffset = 36, BarWidth = 0.75;

    static readonly RgbColor PaneColor = RgbColor.FromHex("F2F2F2");

    /// <summary>The comments whose ranges were open at the end of the page rendered last, by id.</summary>
    readonly Dictionary<string, double> openComments = [];

    /// <summary>
    /// The paper Word fits a page with its markup pane on when it prints with markup (measured with
    /// docx/local/markup-e and markup-f: 20 page sizes): a standard paper the page's size matches within 10 pt (A5,
    /// ISO and JIS B5, A4, Letter, Legal), else A4 where the page fits on it, else Letter; the page and pane are made
    /// smaller to fit its width and height and stand at its top left, in the middle of its height.
    /// </summary>
    public static (double Width, double Height) MarkupPaper(double width, double height)
    {
        (double W, double H)[] standard = [(419.53, 595.28), (498.9, 708.66), (515.91, 728.5), (595.28, 841.89), (612, 792), (612, 1008)];
        foreach (var paper in standard)
        {
            if (Math.Abs(paper.W - width) <= 10 && Math.Abs(paper.H - height) <= 10)
                return paper;
        }
        return width <= 595.28 + 0.5 && height <= 841.89 + 0.5 ? standard[3] : standard[4];
    }

    /// <summary>Where the markup pane begins on a page.</summary>
    public static double PaneLeft(PageBox page) => page.TextLeft + page.TextWidth + PaneGap;

    /// <summary>The width of a page with its markup pane: up to the pane's right edge, at least the page's.</summary>
    public static double MarkupWidth(PageBox page) => Math.Max(page.PaperWidth, PaneLeft(page) + PaneWidth);

    /// <summary>A place in the text a balloon belongs to: its author, its text, where it is, its line's top and its marker.</summary>
    sealed record Anchor(int Author, List<Block> Content, bool Shaded, double X, double Y, double LineTop, string Marker)
    {
        /// <summary>Whether the balloon is a resolved comment's.</summary>
        public bool Done { get; init; }
    }

    /// <summary>
    /// The comment ranges of the page's lines (drawn behind the text): from a range's start to its end, over the lines
    /// between, each line's part as high as the line; a bracket at the start and the end. Returns where the ranges end
    /// (their balloons' places).
    /// </summary>
    List<Anchor> CommentRanges(PageBox page, StringBuilder svg)
    {
        var anchors = new List<Anchor>();
        if (!context.Markup)
            return anchors;
        foreach (var placed in page.Lines)
        {
            if (placed.Angle != 0)
                continue;
            var line = placed.Line;
            var (top, height) = (placed.Top, line.Height);
            var lineStart = placed.Left + (line.Pieces.Count > 0 ? line.Pieces[0].X : 0);
            var lineEnd = placed.Left + Math.Max(0, ContentEnd(line));
            var open = openComments.Keys.ToDictionary(id => id, _ => (Start: lineStart, Bracket: false));
            foreach (var (x, id, start) in (line.Comments ?? []).OrderBy(m => m.X))
            {
                var at = placed.Left + x;
                if (start)
                {
                    open[id] = (at, true);
                    openComments[id] = at;
                    continue;
                }
                var (from, bracket) = open.TryGetValue(id, out var o) ? o : (lineStart, false);
                Comments.TryGetValue(id, out var comment);
                Shade(from, at, top, height, comment, bracket, true, svg);
                open.Remove(id);
                openComments.Remove(id);
                if (comment != null)
                    anchors.Add(new Anchor(comment.Author, Balloon(comment), true, at, top + height, top, "none") { Done = comment.Done });
            }
            foreach (var (id, (from, bracket)) in open)
                Shade(from, lineEnd, top, height, Comments.GetValueOrDefault(id), bracket, false, svg);
        }
        return anchors;
    }

    /// <summary>
    /// A comment range's part of a line, with brackets where the range begins and ends; a reply's range has only its
    /// brackets, a resolved comment's a paler shade (measured with docx/local/markup-d).
    /// </summary>
    static void Shade(double from, double to, double top, double height, Comment? comment, bool opens, bool closes, StringBuilder svg)
    {
        var author = comment?.Author ?? 0;
        if (to > from && comment?.Reply != true)
            svg.Append($"<rect x=\"{Px(from)}\" y=\"{Px(top)}\" width=\"{Px(to - from)}\" height=\"{Px(height)}\" fill=\"{(comment?.Done == true ? Review.Resolved(author) : Review.Shade(author)).ToHex()}\"/>");
        var color = Review.Color(author).ToHex();
        if (opens)
            svg.Append($"<rect x=\"{Px(from - 0.19)}\" y=\"{Px(top)}\" width=\"{Px(0.19)}\" height=\"{Px(height)}\" fill=\"{color}\"/>");
        if (closes)
            svg.Append($"<rect x=\"{Px(to)}\" y=\"{Px(top)}\" width=\"{Px(0.19)}\" height=\"{Px(height)}\" fill=\"{color}\"/>");
    }

    /// <summary>
    /// The markup beside the page's text: change bars, then the pane with the balloons of the formatting changes and
    /// comments in the order of their places, joined to them by dotted lines.
    /// </summary>
    void Markup(PageBox page, List<Anchor> comments, StringBuilder svg, StringBuilder selection, List<PlacedDrawing> front)
    {
        if (!context.Markup)
            return;
        // Change bars beside the lines with tracked changes, one bar over lines that follow each other, the space
        // between them too (measured with docx/generated/revisions: two moved paragraphs, a table's rows).
        var bars = new List<(double Top, double Bottom)>();
        var barOpen = false;
        var anchors = new List<(int Line, Anchor Anchor)>();
        for (var k = 0; k < page.Lines.Count; k++)
        {
            var placed = page.Lines[k];
            if (placed.Angle != 0)
                continue;
            var line = placed.Line;
            var changed = line.Changed || line.HiddenChanges != null || line.Pieces.Any(p => p.Style.Revision != null || p.Style.Change != null);
            if (changed && barOpen)
                bars[^1] = (Math.Min(bars[^1].Top, placed.Top), Math.Max(bars[^1].Bottom, placed.Top + line.Height));
            else if (changed)
                bars.Add((placed.Top, placed.Top + line.Height));
            barOpen = changed;
            var bottom = placed.Top + line.Height;
            FormatChange? previous = null;
            foreach (var piece in line.Pieces)
            {
                if (piece.Style.Change is { } change && !ReferenceEquals(change, previous))
                    anchors.Add((k, new Anchor(change.Author, Balloon(context.Describe.FormattedRun, change.Text), false, placed.Left + piece.X, bottom, placed.Top, "run")));
                previous = piece.Style.Change;
            }
            foreach (var (x, hidden) in line.HiddenChanges ?? [])
                anchors.Add((k, new Anchor(hidden.Author, Balloon(context.Describe.FormattedRun, hidden.Text), false, placed.Left + x, bottom, placed.Top, "run")));
            if (line.BlockChange is { } block)
                anchors.Add((k, new Anchor(block.Author, block.Text.Length > 0 ? Balloon(context.Describe.FormattedParagraph, block.Text) : Balloon(context.Describe.FormattedTable, ""), false,
                    page.TextLeft + page.TextWidth, placed.Top + line.Height / 2, placed.Top, "block")));
        }
        foreach (var comment in comments)
            anchors.Add((page.Lines.FindIndex(p => Math.Abs(p.Top + p.Line.Height - comment.Y) < 0.01 && p.Top == comment.LineTop), comment));
        foreach (var (top, bottom) in bars)
            svg.Append($"<rect x=\"{Px(page.TextLeft - BarOffset)}\" y=\"{Px(top)}\" width=\"{Px(BarWidth)}\" height=\"{Px(bottom - top)}\" fill=\"#000\"/>");
        if (!MarkupPane)
            return;
        var pane = PaneLeft(page);
        svg.Append($"<rect x=\"{Px(pane)}\" y=\"0\" width=\"{Px(MarkupWidth(page) - pane)}\" height=\"{Px(page.PaperHeight)}\" fill=\"{PaneColor.ToHex()}\"/>");
        var fields = new PageFields(page.DisplayNumber, page.Section.PageNumberFormat, 0, 0) { Chapter = page.ChapterPrefix };
        var y = double.NegativeInfinity;
        foreach (var (_, anchor) in anchors.OrderBy(a => a.Line).ThenBy(a => a.Anchor.X))
        {
            var (lines, fills, strokes, height) = blocks!.Blocks(anchor.Content, BalloonWidth - 2 * BalloonInsetX, fields);
            var balloonTop = Math.Max(anchor.LineTop, y + BalloonGap);
            var balloonHeight = BalloonInsetTop + height + BalloonInsetBottom;
            var left = pane + BalloonLeft;
            var color = (anchor.Done ? Review.ResolvedLine(anchor.Author) : Review.Color(anchor.Author)).ToHex();
            // The dotted line along the text's line to the pane and on to the balloon (its dots as fine as Word's
            // device's, which print as a thin line).
            var (reach, attach) = (pane - 2.6, balloonTop + BalloonAttach);
            svg.Append($"<path d=\"M{Px(anchor.X)} {Px(anchor.Y)}H{Px(reach)}L{Px(left)} {Px(attach)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{Px(0.19)}\"/>");
            if (anchor.Marker == "run")
                svg.Append($"<path d=\"M{Px(anchor.X - 1.45)} {Px(anchor.Y)}L{Px(anchor.X + 1.45)} {Px(anchor.Y)}L{Px(anchor.X)} {Px(anchor.Y - 3)}Z\" fill=\"{color}\"/>");
            else if (anchor.Marker == "block")
                svg.Append($"<path d=\"M{Px(anchor.X + 1.5)} {Px(anchor.Y)}L{Px(anchor.X + 4.5)} {Px(anchor.Y - 1.45)}L{Px(anchor.X + 4.5)} {Px(anchor.Y + 1.45)}Z\" fill=\"{color}\"/>");
            var fill = anchor.Done ? Review.Resolved(anchor.Author).ToHex() : anchor.Shaded ? Review.Shade(anchor.Author).ToHex() : "#FFFFFF";
            svg.Append($"<rect x=\"{Px(left)}\" y=\"{Px(balloonTop)}\" width=\"{Px(BalloonWidth)}\" height=\"{Px(balloonHeight)}\" rx=\"{Px(2)}\" fill=\"{fill}\" stroke=\"{color}\" stroke-width=\"{Px(0.48)}\"/>");
            var (textLeft, textTop) = (left + BalloonInsetX, balloonTop + BalloonInsetTop);
            Fills(fills.Select(f => f with { X = textLeft + f.X, Y = textTop + f.Y }), strokes.Select(s => s.Offset(textLeft, textTop)), svg);
            Lines(lines.Select(p => p with { Left = textLeft + p.Left, Top = textTop + p.Top }), svg, selection, front);
            y = balloonTop + balloonHeight;
        }
    }

    /// <summary>A formatting change's balloon: its label in bold, then what changed, in Segoe UI 9 pt.</summary>
    List<Block> Balloon(string label, string text)
    {
        var content = new List<Inline> { new TextInline(text.Length > 0 ? label + " " : label, BalloonFormat(bold: true)) };
        if (text.Length > 0)
            content.Add(new TextInline(text, BalloonFormat(bold: false)));
        return [new ParagraphBlock(new XElement(Wml.W + "p"), BalloonParagraph(), content, BalloonFormat(bold: false))];
    }

    /// <summary>A comment's balloon: "Commented" and its label in bold, then its text in its own fonts at 9 pt.</summary>
    List<Block> Balloon(Comment comment)
    {
        var result = new List<Block>();
        var labelled = false;
        foreach (var block in comment.Content)
        {
            if (block is not ParagraphBlock paragraph)
                continue;
            var content = paragraph.Content.Select(Small).ToList();
            if (!labelled)
                content.Insert(0, new TextInline(context.Describe.Commented(comment.Label) + " ", Gray(BalloonFormat(bold: true))));
            labelled = true;
            result.Add(paragraph with { Format = BalloonParagraph(paragraph.Format), Content = content, MarkFormat = Sized(paragraph.MarkFormat) });
        }
        if (!labelled)
            result.AddRange(Balloon(context.Describe.Commented(comment.Label), ""));
        return result;

        Inline Small(Inline inline) => inline switch
        {
            TextInline text => text with { Format = Gray(Sized(text.Format)) },
            TabInline tab => tab with { Format = Sized(tab.Format) },
            _ => inline,
        };

        // A resolved comment's text is light gray.
        RunFormat Gray(RunFormat format)
        {
            if (!comment.Done)
                return format;
            var gray = format.Clone();
            (gray.Color, gray.ThemeColor) = (Review.ResolvedText.ToHex().TrimStart('#'), null);
            return gray;
        }
    }

    static RunFormat Sized(RunFormat format)
    {
        var sized = format.Clone();
        (sized.Size, sized.SizeComplex) = (9, 9);
        return sized;
    }

    RunFormat BalloonFormat(bool bold)
    {
        var direct = new RunFormat { Ascii = "Segoe UI", HAnsi = "Segoe UI", EastAsia = "Segoe UI", ComplexScript = "Segoe UI", Size = 9, SizeComplex = 9, Bold = bold };
        var format = context.Styles.Run(null, null, direct, null);
        (format.AsciiTheme, format.HAnsiTheme, format.EastAsiaTheme, format.ComplexScriptTheme) = (null, null, null, null);
        return format;
    }

    static ParagraphFormat BalloonParagraph(ParagraphFormat? from = null)
    {
        var format = from?.Clone() ?? new ParagraphFormat();
        (format.Before, format.After, format.Line, format.LineRule) = (0, 0, 240, "auto");
        (format.Left, format.Right, format.FirstLine, format.Justification) = (0, 0, 0, "left");
        (format.BeforeAuto, format.AfterAuto, format.BeforeLines, format.AfterLines) = (false, false, null, null);
        return format;
    }
}
