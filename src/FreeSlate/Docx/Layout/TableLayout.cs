using FreeSlate.Drawing;

namespace FreeSlate.Docx.Layout;

/// <summary>A filled rectangle on a page: a border line or shading, in points from the page's top left.</summary>
public sealed record FillRect(double X, double Y, double Width, double Height, RgbColor Color);

/// <summary>A stroked line on a page (dotted and dashed borders, diagonal cell borders) with an SVG dash pattern.</summary>
public sealed record Stroke(double X1, double Y1, double X2, double Y2, double Width, RgbColor Color, string? Dash = null)
{
    public Stroke Offset(double dx, double dy) => this with { X1 = X1 + dx, Y1 = Y1 + dy, X2 = X2 + dx, Y2 = Y2 + dy };
}

/// <summary>A vertical border stripe through a row: its left edge, width, color and dash pattern.</summary>
public sealed record Stripe(double X, double Width, RgbColor Color, string? Dash = null);

/// <summary>
/// A floating drawing anchored in a cell (catalog: drawing.anchor) with the frames it is placed in: the top of its
/// paragraph and of its line, the cell's text area (positions relative to the column, the margin or the character), the
/// cell inside its lines (relative to the page and the page's margins across) and the cell's content (relative to the
/// page and the margins down); null frames are the page's, for drawings outside tables.
/// </summary>
/// <remarks>
/// Measured with docx/local/cell-anchor: Word places these frames in the cell whether layoutInCell is set or not.
/// </remarks>
public sealed record CellAnchor(AnchorInline Anchor, double ParagraphTop, double LineTop)
{
    public (double Left, double Width)? Text { get; init; }
    public (double Left, double Width)? Cell { get; init; }
    public (double Top, double Height)? Content { get; init; }

    /// <summary>The cell between the inner edges of its side lines, which drawings that text goes around stay within.</summary>
    public (double Left, double Right)? Inside { get; init; }

    /// <summary>Where the drawing is anchored in its line, for positions relative to the character.</summary>
    public double? CharacterX { get; init; }

    /// <summary>How far down a drawing laid out in the cell that text goes around reaches (TableLayout.Reach), else null.</summary>
    public double? Reach { get; init; }

    /// <summary>
    /// Whether the drawing is placed from the top of its row's part on the page, not from its paragraph (ParagraphTop
    /// 0, see Rows): that top stays where the cell's content moves in the row or goes on in another part.
    /// </summary>
    public bool FromRowTop { get; init; }

    /// <summary>The anchor with its cell's content <paramref name="dy"/> points further down in its row's part.</summary>
    public CellAnchor Moved(double dy) => FromRowTop ? Offset(0, dy) with { ParagraphTop = ParagraphTop } : Offset(0, dy);

    public CellAnchor Offset(double dx, double dy) => this with
    {
        ParagraphTop = ParagraphTop + dy,
        LineTop = LineTop + dy,
        Reach = Reach + dy,
        CharacterX = CharacterX + dx,
        Text = Text is { } t ? (t.Left + dx, t.Width) : null,
        Cell = Cell is { } c ? (c.Left + dx, c.Width) : null,
        Content = Content is { } v ? (v.Top + dy, v.Height) : null,
        Inside = Inside is { } i ? (i.Left + dx, i.Right + dx) : null,
    };
}

/// <summary>
/// A cut through a cell's content where its row breaks, down from the top of the content as first laid out: the top of
/// the first line that goes on, where the part after the cut begins, the room above the cut, and the nested table (its
/// block, from 1) the cut goes through, 0 where it goes between lines of the cell's own text.
/// </summary>
public readonly record struct CellCut(int Table, double Top, double At, double Room);

/// <summary>
/// The content of a cell that holds nested tables as laid out again for the cuts made through it (see RowBox.Split):
/// the parts between the cuts, each with where it begins down the content as first laid out, for the content top they
/// were built for; the cuts made before the part that holds it and where that part begins.
/// </summary>
public sealed record CellCuts(Func<IReadOnlyList<CellCut>, List<(CellPart Part, double Origin)>?> Parts, double ContentTop)
{
    public IReadOnlyList<CellCut> Made { get; init; } = [];
    public double Origin { get; init; }
}

/// <summary>The content of one cell in a row: lines, nested tables' fills and strokes, relative to the row's top.</summary>
public sealed class CellPart
{
    public List<PlacedLine> Lines { get; } = [];
    public List<FillRect> Fills { get; } = [];
    public List<Stroke> Strokes { get; } = [];
    public List<CellAnchor> Anchors { get; } = [];

    /// <summary>Where the cell's content starts below the row's top (its top line and margin, without alignment).</summary>
    public double ContentTop { get; init; }

    /// <summary>The cell's bottom margin.</summary>
    public double BottomMargin { get; init; }

    /// <summary>The diagonal lines of the cell: its left and right edge and the lines (tl2br, tr2bl).</summary>
    public (double Left, double Right, BorderLine? Down, BorderLine? Up) Diagonals { get; init; }

    /// <summary>The cell's vertical alignment (w:vAlign) and how far it lowered the content.</summary>
    public string? Align { get; init; }
    public double Lowered { get; init; }

    /// <summary>Lines of a cell merged down from a row above, which go on with this row (RowBox.Overflow).</summary>
    public bool Carried { get; init; }

    /// <summary>The rows the cell spans from this one down (w:vMerge), 1 where it ends here.</summary>
    public int Span { get; init; } = 1;

    /// <summary>
    /// For a cell merged down over rows whose layout their height gives (turned text, or text aligned to their middle or
    /// bottom): the rows it spans and the part laid out again for another height of them, as where a page or column
    /// break cuts the cell (see Paginator.PlaceTable); null for other cells.
    /// </summary>
    public (int Rows, Func<double, CellPart> Part)? Along { get; init; }

    /// <summary>For a cell that holds nested tables: its content laid out again for cuts through them (RowBox.Split).</summary>
    public CellCuts? Cuts { get; init; }

    /// <summary>The part as it stands, its content no longer lowered by an alignment.</summary>
    public CellPart Settled()
    {
        var settled = new CellPart { ContentTop = ContentTop, BottomMargin = BottomMargin, Diagonals = Diagonals, Carried = Carried, Span = Span };
        settled.Lines.AddRange(Lines);
        settled.Fills.AddRange(Fills);
        settled.Strokes.AddRange(Strokes);
        settled.Anchors.AddRange(Anchors);
        return settled;
    }

    /// <summary>
    /// The part with its content <paramref name="dy"/> points further down, below a thicker top line (and with other
    /// cuts to lay it out again for, where given).
    /// </summary>
    public CellPart Lower(double dy, CellCuts? cuts = null)
    {
        var moved = new CellPart
        {
            ContentTop = ContentTop + dy, BottomMargin = BottomMargin, Diagonals = Diagonals, Align = Align, Lowered = Lowered, Carried = Carried, Span = Span,
            Along = Along is { } along ? (along.Rows, height => along.Part(height).Lower(dy)) : null, Cuts = cuts ?? Cuts,
        };
        moved.Lines.AddRange(Lines.Select(l => l with { Top = l.Top + dy }));
        moved.Fills.AddRange(Fills.Select(f => f with { Y = f.Y + dy }));
        moved.Strokes.AddRange(Strokes.Select(s => s.Offset(0, dy)));
        moved.Anchors.AddRange(Anchors.Select(a => a.Moved(dy)));
        return moved;
    }

    /// <summary>
    /// The part cut before the line at <paramref name="restTop"/> (null: all of it stays), the lines from it on, and
    /// the fills, strokes and drawings below the cut, moved up by <paramref name="offset"/> into the part after the cut;
    /// <paramref name="carry"/> is the room above the nested cell's content that line stands in (see RowBox.Split).
    /// </summary>
    public (CellPart Head, CellPart Tail) Partition(double? restTop, double carry, double offset, bool cut, CellCuts? cuts = null)
    {
        // Content cut in two goes on at the top of the continuation, whatever the cell's alignment (measured with
        // docx/local/vs-7 to vs-10: the second of two centered or bottom-aligned lines; docx/external/poi/bug65649);
        // content that stands whole in one part aligns there (vs-1 to vs-6).
        var headPart = new CellPart { ContentTop = ContentTop, BottomMargin = BottomMargin, Diagonals = Diagonals, Align = Align, Lowered = Lowered, Carried = Carried, Span = Span };
        var tailPart = new CellPart { ContentTop = ContentTop, BottomMargin = BottomMargin, Diagonals = Diagonals, Align = cut ? null : Align, Carried = Carried, Span = Span, Cuts = cuts };
        foreach (var line in Lines)
        {
            if (restTop == null || line.Top < restTop)
                headPart.Lines.Add(line);
            else
                tailPart.Lines.Add(line with { Top = line.Top - offset });
        }
        // The lines and fills of a nested row that goes on in the continuation go with it, from its top (measured
        // with docx/external/libreoffice/core-text/floattable-bad-fly-pos: nested rows of 306 and 363 pt each at the
        // top of the next part, their lines with them; core-layout/floattable-in-inlinetable).
        var cutTop = restTop - carry ?? 0;
        // A nested table's line or shading reaching past the cut goes on on the other side of it, cut there
        // (LibreOffice's core-text/floattable-anchor-height: the middle and the inner table's sides on every page they
        // go on to, not one line through them past the first page's end).
        foreach (var fill in Fills)
        {
            if (restTop != null && fill.Y < cutTop - 0.01 && fill.Y + fill.Height > cutTop + 0.01)
            {
                headPart.Fills.Add(fill with { Height = cutTop - fill.Y });
                tailPart.Fills.Add(fill with { Y = cutTop - offset, Height = fill.Y + fill.Height - cutTop });
            }
            else if (restTop == null || fill.Y < cutTop - 0.01)
                headPart.Fills.Add(fill);
            else
            {
                tailPart.Fills.Add(fill with { Y = fill.Y - offset });
                // The line at the top of the nested row the cut is at closes the rows above it too, as a table's
                // own line does where it breaks (LibreOffice's extras-layout/tdf170381-split-float-table-in-normal-table:
                // the inner line under the page's last inner row and over the next page's first).
                if (carry > 0 && fill.Y < cutTop + 0.01 && fill.Height <= carry + 0.01)
                    headPart.Fills.Add(fill);
            }
        }
        foreach (var stroke in Strokes)
        {
            var (from, to) = (Math.Min(stroke.Y1, stroke.Y2), Math.Max(stroke.Y1, stroke.Y2));
            if (restTop != null && Math.Abs(stroke.X1 - stroke.X2) < 0.01 && from < cutTop - 0.01 && to > cutTop + 0.01)
            {
                headPart.Strokes.Add(stroke with { Y1 = from, Y2 = cutTop });
                tailPart.Strokes.Add(stroke with { Y1 = cutTop - offset, Y2 = to - offset });
            }
            else if (restTop == null || from < cutTop - 0.01)
                headPart.Strokes.Add(stroke);
            else
                tailPart.Strokes.Add(stroke.Offset(0, -offset));
        }
        foreach (var anchor in Anchors)
        {
            if (restTop == null || anchor.LineTop < restTop)
                headPart.Anchors.Add(anchor);
            else
                tailPart.Anchors.Add(anchor.Moved(-offset) with { Content = anchor.Content is { } v ? (ContentTop, v.Height) : null });
        }
        return (headPart, tailPart);
    }

    /// <summary>
    /// The part before and the part after a cut through a table nested in the cell, its content laid out again with
    /// the cuts made through it so far and this one (see Cuts), at the part's own content top; null where that fails.
    /// </summary>
    public (CellPart Head, CellPart Tail)? Relaid(CellCut cut)
    {
        if (Cuts is not { } cuts || cuts.Parts([.. cuts.Made, cut]) is not { Count: > 1 } parts)
            return null;
        var dy = ContentTop - cuts.ContentTop;
        return (parts[^2].Part.Lower(dy), parts[^1].Part.Lower(dy, cuts with { Made = [.. cuts.Made, cut], Origin = parts[^1].Origin }));
    }

    /// <summary>The part moved down by <paramref name="dy"/> points.</summary>
    public CellPart Shifted(double dy)
    {
        var moved = new CellPart { ContentTop = ContentTop, BottomMargin = BottomMargin, Diagonals = Diagonals, Align = Align, Lowered = Lowered + dy, Carried = Carried, Span = Span, Cuts = Cuts };
        moved.Lines.AddRange(Lines.Select(l => l with { Top = l.Top + dy }));
        moved.Fills.AddRange(Fills.Select(f => f with { Y = f.Y + dy }));
        moved.Strokes.AddRange(Strokes.Select(s => s.Offset(0, dy)));
        moved.Anchors.AddRange(Anchors.Select(a => a.Moved(dy)));
        return moved;
    }
}

/// <summary>
/// A laid-out table row: its height and, relative to the row's top left (the table's origin), its cells' content,
/// the shading and vertical lines that run through it, the lines at its top and the lines below it where the table
/// ends or breaks after it.
/// </summary>
public sealed class RowBox
{
    public double Height { get; set; }
    public bool Header { get; init; }
    public bool CantSplit { get; init; }

    /// <summary>Whether the row has an exact height (w:hRule="exact").</summary>
    public bool Exact { get; init; }

    /// <summary>The row's at-least height without its top line (w:trHeight and the row's top and bottom margins), 0
    /// without one.</summary>
    public double Least { get; init; }

    /// <summary>
    /// Whether a line above a cut needs room for its cell's bottom margin too, as before Word 2013 (measured with
    /// docx/local/rowmar-11, -12, -14: a line ending 5 pt above the cut in a row with a 10 pt bottom margin goes on to
    /// the next page; in mode 15 it stays).
    /// </summary>
    public bool MarginBelowCut { get; init; }

    /// <summary>
    /// Whether a paragraph whose drawing that text goes around would reach past a cut goes on with it, as from Word 2013
    /// on (see Split).
    /// </summary>
    public bool AnchorsReach { get; init; }

    /// <summary>The height of the lines at the row's top, above its content.</summary>
    public double TopWidth { get; init; }

    /// <summary>
    /// The lines at the row's top where it begins a part of its table on a page or in a column (the table's top line
    /// instead of the one it shares with the row above), and their height; null where they are the row's own.
    /// </summary>
    public List<FillRect>? PageTopBorders { get; set; }
    public List<Stroke>? PageTopStrokes { get; set; }
    public double PageTopWidth { get; set; }

    /// <summary>
    /// The row where it begins a part of its table on a page or in a column, no header rows above it: the table's top
    /// line above it, its content as much lower as that is thicker (measured with docx/local/cb-1 to cb-7: a 3 pt outer
    /// top line over the continued row of a table whose inside lines are 1 pt or none, after a cut in a row and
    /// between rows, of floating tables too, in modes 14 and 15; docx/external/libreoffice/core-unocore/
    /// floattable-split).
    /// </summary>
    public RowBox AtPageTop()
    {
        if (PageTopBorders == null || Math.Abs(TopWidth - PageTopWidth) < 0.001 && TopBorders.SequenceEqual(PageTopBorders))
            return this;
        var delta = PageTopWidth - TopWidth;
        var copy = new RowBox
        {
            Height = Height + delta, Header = Header, CantSplit = CantSplit, Exact = Exact, Least = Least, MarginBelowCut = MarginBelowCut, AnchorsReach = AnchorsReach, TopWidth = PageTopWidth, BottomBorderWidth = BottomBorderWidth,
            PageTopBorders = PageTopBorders, PageTopStrokes = PageTopStrokes, PageTopWidth = PageTopWidth,
        };
        copy.Shading.AddRange(Shading);
        copy.Verticals.AddRange(Verticals);
        copy.Frames.AddRange(Frames);
        copy.TopBorders.AddRange(PageTopBorders);
        copy.TopStrokes.AddRange(PageTopStrokes ?? []);
        copy.BottomBorders.AddRange(BottomBorders);
        copy.BottomStrokes.AddRange(BottomStrokes);
        foreach (var cell in Cells)
            copy.Cells.Add(Math.Abs(delta) < 0.001 ? cell : cell.Lower(delta));
        return copy;
    }

    public List<CellPart> Cells { get; } = [];

    /// <summary>Cell shading: left edge, width and color, from the row's top through its height.</summary>
    public List<(double X, double Width, RgbColor Color)> Shading { get; } = [];
    public List<Stripe> Verticals { get; } = [];
    public List<FillRect> TopBorders { get; } = [];
    public List<Stroke> TopStrokes { get; } = [];

    /// <summary>The lines below the row where the table ends or breaks after it, drawn at the row's bottom.</summary>
    public List<FillRect> BottomBorders { get; } = [];
    public List<Stroke> BottomStrokes { get; } = [];
    public double BottomBorderWidth { get; set; }

    /// <summary>Lines and fills of rows laid out with cell spacing: each cell's own frame, through the row.</summary>
    public List<FillRect> Frames { get; } = [];

    /// <summary>All lines of the row's cells.</summary>
    public IEnumerable<PlacedLine> Lines => Cells.SelectMany(c => c.Lines);

    /// <summary>
    /// The row cut at <paramref name="available"/> points (catalog: table.rows): each cell keeps the lines that end
    /// above the cut, a paragraph's last line with the paragraph's space after, the rest continue at the top of the
    /// continuation, below its top line and margin, which is at least the row's at-least height again. Null where no
    /// cell has a line above the cut or none below it, and, unless <paramref name="force"/>d (a row taller than its
    /// page), for rows Word does not split (measured with docx/local/tblsplit-*, rowsplit-*, rowsplit2-*): those that
    /// may not split, header rows, rows of exact height, rows whose at-least height does not fit above the cut and
    /// rows with a cell whose first line does not (a picture line with space after it that fits alone moves on too).
    /// </summary>
    /// <param name="dropRest">Whether a row whose lines all stand above the cut may end there, the rest of its height gone.</param>
    public (RowBox Head, RowBox Tail)? Split(double available, bool force = false, bool dropRest = false)
    {
        if (!force && (CantSplit || Header || Exact || TopWidth + Least > available + 0.01)
            || Frames.Count > 0 || Cells.Any(c => c.Lines.Any(l => l.Angle != 0)))
            return null;
        var head = Copy(available);
        var tail = Copy(0);
        var any = false;
        var continues = false;
        var tailHeight = TopWidth;
        foreach (var cell in Cells)
        {
            // Where the lines would stand without the cell's vertical alignment, which each part applies again.
            // A row of a nested table goes on whole where its at-least height does not fit (measured with docx/external/
            // libreoffice/writerfilter-dmapper/floattable-nested: an inner row of 85 pt with a line of text moves to the
            // next page, as rows of the table itself do).
            // A line of a nested table needs room for the bottom margins of the nested cells it stands in and the lines
            // below their rows, in every mode (measured with docx/local/nft-*-c*, -m*: a line of a nested cell whose
            // 3 pt bottom line or 10 pt bottom margin would reach past the page's bottom goes on to the next page in
            // modes 11 and 15, without them it stays; LibreOffice's writerfilter-dmapper/floattable-hidemark: a line
            // of a floating table's cell with a 6.75 pt bottom margin, its bottom 1.6 pt above the page's, goes on in
            // mode 12; core-text/floattable-anchor-height: the last line of page 3 in three tables goes on).
            var margin = MarginBelowCut ? cell.BottomMargin : 0;
            var rest = cell.Lines.Where(l => l.Top - cell.Lowered + Math.Max(l.Line.Height + l.After, l.UnitHeight) + margin + l.CarryBelow > available + 0.01)
                .MinBy(l => l.Top);
            // Widow control keeps two lines of a paragraph on each side of the cut, else the paragraph goes on whole,
            // from Word 2013 on (measured with docx/local/rs3-*: three lines with two above the cut, or one, go on, four
            // split two and two; before, rows split at any line).
            if (rest != null && rest.Widow && !rest.Line.First)
            {
                var k = cell.Lines.FindIndex(l => ReferenceEquals(l, rest));
                var first = k;
                while (first > 0 && !cell.Lines[first].Line.First)
                    first--;
                var last = k;
                while (last + 1 < cell.Lines.Count && !cell.Lines[last].Line.Last)
                    last++;
                var (above, below) = (k - first, last - k + 1);
                if (below < 2 && above >= 3)
                    k--;
                else if (above < 2 || below < 2)
                    k = first;
                rest = cell.Lines[k];
            }
            // A paragraph kept with the next one goes on with the next one's first line from Word 2013 on, where the cell
            // has lines before it (measured with docx/local/spg2-15, spg3-15: kept with a paragraph that does not fit, the
            // last line on the page goes on, framed or not; spg2-12, spg3-12 and -14 stay).
            if (rest != null && rest.Line.First)
            {
                var k = cell.Lines.FindIndex(l => ReferenceEquals(l, rest));
                var kept = k;
                while (kept > 0 && cell.Lines[kept].Line.First && cell.Lines[kept - 1].Keep)
                {
                    kept--;
                    while (kept > 0 && !cell.Lines[kept].Line.First)
                        kept--;
                }
                if (kept > 0)
                    rest = cell.Lines[kept];
            }
            // From Word 2013 on a paragraph whose drawing that text goes around, laid out in the cell, would reach past the
            // cut goes on with it (measured with docx/local/tpq-tbl-15-*: a 92 pt picture 1 to 50 pt past the page's
            // bottom takes its paragraph to the next page, as in the body, tpq-body-15-*; before, tpq-tbl-14-*, the
            // picture stays; LibreOffice's extras-layout/tdf172156).
            if (AnchorsReach)
            {
                foreach (var anchor in cell.Anchors)
                {
                    if (anchor.Reach is not { } reach || reach - cell.Lowered + margin <= available + 0.01)
                        continue;
                    var k = cell.Lines.FindIndex(l => l.Line.First && l.Top >= anchor.ParagraphTop - 0.01);
                    if (k >= 0 && (rest == null || cell.Lines[k].Top < rest.Top))
                        rest = cell.Lines[k];
                }
            }
            if (!force && rest != null && ReferenceEquals(rest, cell.Lines[0]))
                return null;
            var cut = rest != null && !ReferenceEquals(rest, cell.Lines[0]);
            // A nested row cut in two goes on below its line and its cell's margin again (measured with docx/local/sr-3,
            // sr-5: 6.15 pt lower than the line of a row of the table itself, in modes 11 and 15; docx/external/
            // libreoffice/core-text/floattable-anchor-height).
            // A paragraph that begins the continuation keeps what its space before has more than the space after the
            // paragraph above it, which ends the part before the cut (measured with docx/local/spg-14, spg-15, spgf-14,
            // spgf-15: 12 pt before after 0, 6 and 18 pt after keep 12, 6 and 0 pt, in inline and floating tables;
            // docx/external/libreoffice/core-layout/floattable-deleted-anchor).
            var offset = rest != null ? rest.Top - cell.ContentTop - rest.Carry - rest.Lead : 0;
            CellPart headPart, tailPart;
            // A cut through a table nested in the cell breaks it as a table: the cell's content is laid out again for the
            // cut (see TableLayout.Content), the nested row it goes through cut as a row of the table itself; one through a
            // floating table also where the cell's own text after it is what goes on first. A cut between lines of the
            // cell's own text keeps the lines as they stand, the cell's later cuts laying the content out again with it.
            var spec = rest == null || cell.Cuts is not { } cuts ? (CellCut?)null
                : new CellCut(!cut ? 0 : rest.Nest > 0 ? rest.Nest
                        : cell.Lines.Where(l => l.Nest > 0 && l.Top >= rest.Top && cell.Lines.Any(o => o.Nest == l.Nest && o.Top < rest.Top)).Select(l => l.Nest).FirstOrDefault(),
                    cuts.Origin + rest.Top - cell.ContentTop - cell.Lowered, cuts.Origin + offset - cell.Lowered, cuts.Origin + available - cell.ContentTop - margin);
            if (spec is { Table: > 0 } through && cell.Relaid(through) is { } relaid)
                (headPart, tailPart) = relaid;
            else
                (headPart, tailPart) = cell.Partition(rest?.Top, rest?.Carry ?? 0, offset, cut,
                    cell.Cuts is { } kept && spec is { } plain ? cut ? kept with { Made = [.. kept.Made, plain with { Table = 0 }], Origin = plain.At } : kept with { Origin = plain.At } : null);
            any |= headPart.Lines.Count > 0;
            continues |= tailPart.Lines.Count > 0;
            // The continuation holds its nested rows whole, as high as their fills reach, so that it splits again where
            // its page cannot hold them (floattable-bad-fly-pos: four pages).
            // It grows down to the drawings that went on with it that text goes around, as the row does (tpq-tbl-15-*:
            // the picture's bottom 1.9 pt above the line after the table), and to the bottom margins of the nested cells
            // whose lines went on (LibreOffice's writerfilter-dmapper/floattable-hidemark: the paragraph after the
            // table 6.75 pt lower).
            if (tailPart.Lines.Count > 0)
                tailHeight = Math.Max(tailHeight, Math.Max(tailPart.Lines.Max(l => l.Top + l.Line.Height + l.After + l.CarryBelow),
                    Math.Max(tailPart.Fills.Count > 0 ? tailPart.Fills.Max(f => f.Y + f.Height) : 0,
                        tailPart.Anchors.Max(a => a.Reach) ?? 0)) + cell.BottomMargin);
            head.Cells.Add(headPart);
            tail.Cells.Add(tailPart);
        }
        if (!any || !continues && !dropRest)
            return null;
        tail.Height = continues ? Math.Max(tailHeight, TopWidth + Least) : 0;
        // The part before the cut ends below its cells' content, a paragraph's last line with its space after, and
        // their bottom margin where the page has room for it, not at the page's end (spg-*, spgf-*: the line below a
        // cell's last line on the page with 0, 6 and 18 pt after is that far below it, 25 pt above the page's end;
        // docx/external/libreoffice/extras-ooxmlexport/tdf170381), below a nested table's line the bottom margins of
        // its cells (docx/local/nft-*-m*: a nested cell's 10 pt margin below its last line on the page, the row's
        // line below it).
        if (continues && head.Cells.All(c => c.Anchors.Count == 0))
        {
            var bottom = head.Cells.Max(c => Math.Max(c.Lines.Count == 0 ? 0 : c.Lines.Max(l => l.Top + l.Line.Height + l.After + l.CarryBelow) - c.Lowered,
                c.Fills.Count == 0 ? 0 : c.Fills.Max(f => f.Y + f.Height)) + c.BottomMargin);
            head.Height = Math.Min(available, Math.Max(bottom, TopWidth + Least));
        }
        Realign(head.Cells, null);
        Realign(tail.Cells, tail.Height);
        return (head, tail);
    }

    /// <summary>
    /// Cells aligned to the middle or the bottom align again in a part of a split row: within the part's height, or
    /// for the part before the cut down to the bottom of its tallest content (measured with docx/local/rowsplit-28c).
    /// </summary>
    static void Realign(List<CellPart> parts, double? height)
    {
        double? Bottom(CellPart part) => part.Lines.Count == 0 ? null : part.Lines.Max(l => l.Top + l.Line.Height + l.After) - part.Lowered + part.BottomMargin;
        var bottoms = parts.Select(Bottom).ToList();
        if (height == null && bottoms.All(b => b == null))
            return;
        var room = height ?? bottoms.Max(b => b ?? 0);
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Align is not ("center" or "bottom") || bottoms[i] is not { } bottom)
                continue;
            var free = Math.Max(0, room - bottom);
            var lowered = parts[i].Align == "center" ? free / 2 : free;
            if (Math.Abs(lowered - parts[i].Lowered) > 0.001)
                parts[i] = parts[i].Shifted(lowered - parts[i].Lowered);
        }
    }

    /// <summary>
    /// The row without the lines of cells merged down into the rows below that end below it, as laid out from the
    /// cell's top whatever its alignment, and those lines (with their fills and strokes, and the rest of a paragraph
    /// that widow control would not let split there) as parts of their own, from the next row's top: they go on with
    /// it, to the next page where the table breaks before it, there at the top of its content (measured with
    /// docx/local/vmbw0-*, vmbw1-*; docx/external/poi/bug65649: the second line of a merged price goes on beside the
    /// unit row below it, a merged heading centered on two rows stays with the first; docx/external/libreoffice/
    /// extras-layout/tdf152298: the second line of a number aligned to the bottom of four rows goes on at the top of
    /// the next page, its empty first line stays).
    /// </summary>
    public (RowBox Own, List<CellPart> Below) Overflow()
    {
        var below = new List<CellPart>();
        bool Down(CellPart cell, double top, double height) => cell.Span > 1 && top - cell.Lowered + height > Height + 0.01;
        if (!Cells.Any(c => c.Lines.Any(l => Down(c, l.Top, l.Line.Height))))
            return (this, below);
        var own = Copy(Height);
        foreach (var cell in Cells)
        {
            var lines = cell.Lines;
            var cut = lines.FindIndex(l => Down(cell, l.Top, l.Line.Height));
            if (cut < 0)
            {
                own.Cells.Add(cell);
                continue;
            }
            // Widow control: a paragraph keeps two lines on each side, else goes on whole.
            if (lines[cut].Widow && !lines[cut].Line.First)
            {
                var first = cut;
                while (first > 0 && !lines[first].Line.First)
                    first--;
                var last = cut;
                while (last + 1 < lines.Count && !lines[last].Line.Last)
                    last++;
                var (above, after) = (cut - first, last - cut + 1);
                if (after < 2 && above >= 3)
                    cut--;
                else if (above < 2 || after < 2)
                    cut = first;
            }
            var limit = lines[cut].Top;
            var stay = new CellPart { ContentTop = cell.ContentTop, BottomMargin = cell.BottomMargin, Diagonals = cell.Diagonals, Align = cell.Align, Lowered = cell.Lowered, Carried = cell.Carried };
            var go = new CellPart { ContentTop = cell.ContentTop, BottomMargin = cell.BottomMargin, Lowered = cell.Lowered, Carried = true, Span = cell.Span - 1 };
            for (var k = 0; k < lines.Count; k++)
                (k >= cut ? go : stay).Lines.Add(lines[k] with { Top = lines[k].Top - (k >= cut ? Height : 0) });
            foreach (var fill in cell.Fills)
            {
                if (fill.Y >= limit - 0.01)
                    go.Fills.Add(fill with { Y = fill.Y - Height });
                else
                    stay.Fills.Add(fill);
            }
            foreach (var stroke in cell.Strokes)
            {
                if (stroke.Y1 >= limit - 0.01)
                    go.Strokes.Add(stroke.Offset(0, -Height));
                else
                    stay.Strokes.Add(stroke);
            }
            stay.Anchors.AddRange(cell.Anchors);
            own.Cells.Add(stay);
            below.Add(go);
        }
        return (own, below);
    }

    /// <summary>The row with one of its cell parts in place of another.</summary>
    public RowBox With(CellPart old, CellPart part)
    {
        var copy = Copy(Height);
        copy.Cells.AddRange(Cells.Select(c => ReferenceEquals(c, old) ? part : c));
        return copy;
    }

    /// <summary>The row with more cell parts.</summary>
    public RowBox With(IEnumerable<CellPart> parts)
    {
        var copy = Copy(Height);
        copy.Cells.AddRange(Cells);
        copy.Cells.AddRange(parts);
        return copy;
    }

    /// <summary>
    /// The row where it begins a page or column after the rows above it went on the page before: lines merged down
    /// from them (Overflow) stand at the top of its content, as far apart as before.
    /// </summary>
    public RowBox Continued()
    {
        double? Raise(CellPart cell) => cell.Carried && cell.Lines.Count > 0 && cell.Lines.Min(l => l.Top) is var top && Math.Abs(top - cell.ContentTop) > 0.01
            ? cell.ContentTop - top : null;
        if (!Cells.Any(c => Raise(c) != null))
            return this;
        var copy = Copy(Height);
        foreach (var cell in Cells)
            copy.Cells.Add(Raise(cell) is { } dy ? cell.Shifted(dy).Settled() : cell);
        // The last row of the merge grows to hold what goes on in it (docx/local/vmbw1-*: three lines of a merged cell
        // the row below it on the page before was to share take a row of two lines' height and a third).
        // A row of exact height keeps it (docx/external/libreoffice/extras-layout/tdf128959: a merged cell going on in a
        // 17 pt row on the next page, its line cut off).
        foreach (var cell in copy.Cells)
        {
            if (!Exact && cell.Carried && cell.Span <= 1 && cell.Lines.Count > 0)
                copy.Height = Math.Max(copy.Height, cell.Lines.Max(l => l.Top + l.Line.Height + l.After) + cell.BottomMargin);
        }
        return copy;
    }

    RowBox Copy(double height)
    {
        var copy = new RowBox
        {
            Height = height, Header = Header, CantSplit = CantSplit, Exact = Exact, Least = Least, MarginBelowCut = MarginBelowCut, AnchorsReach = AnchorsReach, TopWidth = TopWidth, BottomBorderWidth = BottomBorderWidth,
            PageTopBorders = PageTopBorders, PageTopStrokes = PageTopStrokes, PageTopWidth = PageTopWidth,
        };
        copy.Shading.AddRange(Shading);
        copy.Verticals.AddRange(Verticals);
        // The cells' own frames of a table with cell spacing (docx/external/libreoffice/extras-ooxmlexport/tdf123104:
        // shaded cells merged down, whose rows go on without them).
        copy.Frames.AddRange(Frames);
        copy.TopBorders.AddRange(TopBorders);
        copy.TopStrokes.AddRange(TopStrokes);
        copy.BottomBorders.AddRange(BottomBorders);
        copy.BottomStrokes.AddRange(BottomStrokes);
        return copy;
    }
}

/// <summary>A laid-out table: its left edge from the column's, its width and its rows.</summary>
public sealed class TableBox
{
    public double Left { get; init; }
    public double Width { get; init; }
    public List<RowBox> Rows { get; } = [];

    /// <summary>
    /// The frame of a table with cell spacing (its own four borders), which the paginator draws around the table's
    /// rows on every page; its top is in the first row's height, its bottom below the last row.
    /// </summary>
    public (BorderLine? Top, BorderLine? Left, BorderLine? Bottom, BorderLine? Right) Frame { get; init; }

    /// <summary>
    /// How far the table's left edge stands left of its indent: in older compatibility modes the first cell's text
    /// stands at the indent (TableLayout), and at a floating table's position (Paginator.PlaceFloatingTable).
    /// </summary>
    public double Inset { get; init; }

    /// <summary>
    /// How far the first cell's text and the last cell's stand inside the table's left and right edges: its outer
    /// lines' halves and the cells' margins (older modes place floating tables by them, Paginator.PlaceFloatingTable).
    /// </summary>
    public (double Left, double Right) TextInsets { get; init; }

    /// <summary>
    /// The room above the first row of a part of a table with cell spacing that goes on on another page: its frame's
    /// top line and the cell spacing (measured with docx/local/sb-*: 1.5 pt lines and 3 pt spacing 4.5 pt, no lines
    /// 2.9 pt, 0.75 pt lines and spacing 1.5 pt; docx/external/poi/bib-chernigovka.netdo.ru_download_docs_17459).
    /// </summary>
    public double ContinuationTop { get; init; }

    /// <summary>
    /// The lines below the last row's cells as a table right after this one meets them (TableLayout.Layout's joined):
    /// each cell's own bottom line or the table's inside line, from and to where in the column (none for tables with
    /// cell spacing).
    /// </summary>
    public List<(double From, double To, BorderLine? Line)> BottomEdges { get; } = [];
}

/// <summary>
/// Lays out tables the way Word 2013 and later do (catalog: engine.tableLayout, table.width, table.borders,
/// table.rows, table.cells), measured with docx/local/tbl-geom and tbl-features: the grid starts at the indent from
/// the column's left edge (older compatibility modes put the first cell's text there) and moves right by half the
/// left border, every vertical line centered on its grid line; text starts the larger of the margin and half the
/// line after it, below the whole top line and the top margin; a row is its top line and its content, at least or
/// exactly its height (at least without the line, exactly with it); a table's bottom line lies below its last row.
/// Double lines take three times their width (line, gap, line), triple five times. With cell spacing every cell has
/// its own frame, the cells twice the spacing apart and from the table's frame.
/// </summary>
public sealed class TableLayout(DocumentContext context, ParagraphLayout paragraphs)
{
    /// <summary>
    /// The left and right cell margin of tables without any table style (see <see cref="TableFormat.Styled"/>): 0.5 pt,
    /// measured with docx/local/tblnostyle-12, -14 and -15 for lines of 0 to 6 pt.
    /// </summary>
    const double DefaultMargin = 0.5;

    /// <summary>The paragraph layout the cells' text is laid out with.</summary>
    public ParagraphLayout Paragraphs => paragraphs;

    /// <summary>Whether a table nested in a cell is being laid out.</summary>
    bool inCell;

    /// <summary>Whether a table cell's blocks are being laid out (not a text box's in it).</summary>
    bool cellBlocks;

    /// <summary>
    /// Whether the table being laid out has its first cell's text at its indent and its percentage width with its
    /// cells' margins, as before Word 2013 the tables of the text do; nested in cells they stand as from Word 2013 on
    /// (measured with docx/local/nz-11 to nz-14b: in modes 11 to 14 a nested table's left line at the outer cell's text,
    /// 100 % as wide as that text, tblInd from there; docx/external/libreoffice/core-text/floattable-anchor-height); not
    /// so the tables of text boxes (core-text/fly-minimal-wrap: a table in a text box in mode 14 has its text at the
    /// box's text).
    /// </summary>
    bool Legacy => context.Profile.TablesIndentedByText && !inCell;

    /// <summary>Stands for no paragraph where contextual spacing compares paragraph styles.</summary>
    public const string NoParagraph = "\0";

    /// <summary>The paragraph style a paragraph has, as contextual spacing compares them.</summary>
    string? StyleOf(ParagraphBlock paragraph) => context.Styles.ParagraphStyleOf(paragraph.Format.Style);

    /// <summary>The first paragraph of a table: its first cell's, in a nested table the nested table's.</summary>
    public static ParagraphBlock? FirstParagraph(TableModel table) =>
        table.Rows.FirstOrDefault()?.Cells.FirstOrDefault()?.Blocks.FirstOrDefault() switch
        {
            ParagraphBlock paragraph => paragraph,
            TableBlock nested => FirstParagraph(nested.Table),
            _ => null,
        };

    /// <summary>
    /// The styles of the paragraphs before and after every cell's content in the document's order, which contextual
    /// spacing compares the cell's first and last paragraphs with (measured with docx/local/c5-cells): a cell's first
    /// paragraph follows the last paragraph of the cell before it in its row, the first cell's the end of the row
    /// before (whose mark has the default paragraph style) or, in the first row, the paragraph before the table; a
    /// cell's last paragraph precedes the next cell's first, the last cell's the end of its row.
    /// </summary>
    Dictionary<CellModel, (string? Before, string? After)> Neighbors(List<RowModel> rows, string? before)
    {
        var result = new Dictionary<CellModel, (string?, string?)>(ReferenceEqualityComparer.Instance);
        var rowEnd = context.Styles.ParagraphStyleOf(null);
        string? Last(CellModel cell) => cell.Blocks.LastOrDefault() switch
        {
            ParagraphBlock p => StyleOf(p),
            TableBlock t => t.Table.Rows.Count > 0 ? rowEnd : NoParagraph,
            _ => NoParagraph,
        };
        string? First(CellModel cell) => cell.Blocks.FirstOrDefault() switch
        {
            ParagraphBlock p => StyleOf(p),
            TableBlock t => FirstParagraph(t.Table) is { } p ? StyleOf(p) : NoParagraph,
            _ => NoParagraph,
        };
        for (var r = 0; r < rows.Count; r++)
        {
            var cells = rows[r].Cells;
            for (var c = 0; c < cells.Count; c++)
                result[cells[c]] = (c > 0 ? Last(cells[c - 1]) : r > 0 ? rowEnd : before, c + 1 < cells.Count ? First(cells[c + 1]) : rowEnd);
        }
        return result;
    }

    /// <param name="before">The style of the paragraph before the table (<see cref="NoParagraph"/> for none), which contextual spacing compares its first paragraph with.</param>
    /// <param name="joined">The bottom lines of a table right before this one (<see cref="TableBox.BottomEdges"/>), which it continues.</param>
    public TableBox Layout(TableModel table, double available, PageFields? fields = null, string? before = NoParagraph,
        IReadOnlyList<(double From, double To, BorderLine? Line)>? joined = null)
    {
        // Lines in table cells snap to the line grid only where the document says w:adjustLineHeightInTable (measured
        // with docx/local/ygc-12, -15: 11 pt Yu Gothic lines 18.4 pt apart in a cell under an 18 pt grid, two pitches
        // in the body; ygc-12-adj, -15-adj: two pitches in the cell too; docx/external/libreoffice/extras-uiwriter/
        // tdf128375).
        var pitch = paragraphs.LinePitch;
        if (!context.Profile.AdjustsLineHeightInTables)
            paragraphs.LinePitch = null;
        TableBox box;
        try
        {
            box = Laid(table, available, fields, before, joined);
        }
        finally
        {
            paragraphs.LinePitch = pitch;
        }
        // The markup view marks every line of a table whose formatting changed; the balloon hangs from its first line.
        if (table.Change is { } change)
        {
            var lines = box.Rows.SelectMany(r => r.Cells).SelectMany(c => c.Lines).ToList();
            foreach (var line in lines)
                line.Line.Changed = true;
            if (lines.Count > 0)
                lines[0].Line.BlockChange = change;
        }
        return box;
    }

    TableBox Laid(TableModel table, double available, PageFields? fields, string? before,
        IReadOnlyList<(double From, double To, BorderLine? Line)>? joined = null)
    {
        var format = table.Format;
        // A cell's own shading replaces its row's and its table's, even where it paints nothing (docx/external/
        // libreoffice/extras-ooxmlexport/table-black_fill: cells of fill auto in a table shaded black stay white).
        RgbColor? CellShading(CellModel cell, RowModel row) => cell.Format.OwnShading ? Shading.Color(cell.Format.Shading)
            : Shading.Color(cell.Format.Shading) ?? Shading.Color(row.ShadingException) ?? Shading.Color(format.Shading);
        var rows = table.Rows.Where(r => !r.Format.Hidden).ToList();
        var neighbors = Neighbors(rows, before);
        var columns = Math.Max(table.Grid.Count, rows.Count == 0 ? 0 : rows.Max(r => r.Cells.Count == 0 ? 0 : r.Cells.Max(c => c.Column + c.Format.GridSpan) + r.Format.GridAfter));
        columns = Math.Max(1, columns);

        // Every cell's place in the grid and the rows a vertical merge spans.
        var at = new CellModel?[rows.Count, columns];
        for (var r = 0; r < rows.Count; r++)
        {
            foreach (var cell in rows[r].Cells)
            {
                for (var k = cell.Column; k < Math.Min(columns, cell.Column + cell.Format.GridSpan); k++)
                    at[r, k] = cell;
            }
        }
        var origin = new Dictionary<CellModel, (int Row, CellModel Cell)>();
        var span = new Dictionary<CellModel, int>();
        for (var r = 1; r < rows.Count; r++)
        {
            foreach (var cell in rows[r].Cells)
            {
                if (cell.MergedAbove && cell.Column < columns && at[r - 1, cell.Column] is { } above)
                {
                    (int Row, CellModel Cell) start = origin.TryGetValue(above, out var o) ? o : (r - 1, above);
                    origin[cell] = start;
                    span[start.Cell] = r - start.Row + 1;
                }
            }
        }
        CellModel? Owner(CellModel? cell) => cell != null && origin.TryGetValue(cell, out var o) ? o.Cell : cell;

        // Cell spacing, even of nothing, keeps the table's frame and the cells' borders apart (measured with
        // docx/local/altchunk-tables: an HTML table with cellspacing="0" has its 1.5 pt frame and 0.75 pt cell borders).
        var spacingSet = rows.Count > 0 ? rows[0].Format.CellSpacing ?? format.CellSpacing : format.CellSpacing;
        var spacing = spacingSet ?? 0;
        if (spacingSet != null)
            return Spaced(table, rows, columns, at, available, spacing, fields, neighbors, origin, span);

        // Borders: for each row boundary (0 … rows) and grid column the horizontal line, for each row and grid line
        // the vertical one; a cell's own border wins over the table's, two cells' borders are resolved by weight.
        var horizontal = new BorderLine?[rows.Count + 1, columns];
        // Where the line at a row's top lies in the row's top margin, taking room only as far as it is wider: where
        // only the cell above brings it with its bottom border, the row's own cells and the table bringing none
        // (measured with docx/local/cellhlines, cellhlines2: rows of cells with bottom borders alone are their content
        // and margins high, with top borders or the table's inside lines a line higher; a 3 pt line above margins of
        // 2.75 pt takes 0.25 pt, a 0.5 pt one above none 0.5 pt; docx/external/libreoffice/core-layout/rtl-table).
        var inMargin = new bool[rows.Count + 1, columns];
        var vertical = new BorderLine?[rows.Count, columns + 1];
        for (var r = 0; r <= rows.Count; r++)
        {
            for (var k = 0; k < columns; k++)
            {
                var above = r > 0 ? Owner(at[r - 1, k]) : null;
                var below = r < rows.Count ? Owner(at[r, k]) : null;
                // Inside a vertical merge there is no line, nor where no cell is (rows that skip grid columns).
                if (above == below)
                    continue;
                // The table's top and bottom lines at its first row's top and its last row's bottom only; between rows the
                // inside line, also where a cell has none above or below it (w:gridBefore, w:gridAfter; measured with
                // docx/local/gaf-1: 0.5 pt inside lines below and above the cells a row leaves out, 3 pt outer lines;
                // docx/external/libreoffice/extras-ooxmlexport/tdf134609_gridAfter).
                var tableLine = r == 0 ? TableBorder(rows[0], format, b => b.Top)
                    : r == rows.Count ? TableBorder(rows[r - 1], format, b => b.Bottom)
                    : TableBorder(rows[below != null ? r : r - 1], format, b => b.InsideH);
                // Between rows each cell brings its own row's inside line, the row's exceptions over the table's
                // (measured with docx/local/rowex: a row whose w:tblPrEx makes the inside lines red and double has
                // them above and below it; docx/external/powertools/RP/RP033-Table-Prop-Ex-Change).
                var aboveLine = above != null && below != null ? TableBorder(rows[r - 1], format, b => b.InsideH) : tableLine;
                // A merge's bottom line is its last row's cell's (docx/external/libreoffice/extras-ooxmlexport/
                // tdf132898_missingBorder: a bottom border set on the last cell of a vertical merge alone).
                var last = r > 0 ? at[r - 1, k] ?? above : above;
                horizontal[r, k] = Edge(last?.CellBorders.Bottom, below?.CellBorders.Top, tableLine, above != null && below != null, aboveLine);
                inMargin[r, k] = above != null && below != null && below.CellBorders.Top == null && tableLine is not { Visible: true };
            }
        }
        for (var r = 0; r < rows.Count; r++)
        {
            for (var k = 0; k <= columns; k++)
            {
                var left = k > 0 ? at[r, k - 1] : null;
                var right = k < columns ? at[r, k] : null;
                if (left == right)
                    continue;
                var tableLine = k == 0 || left == null ? TableBorder(rows[r], format, b => b.Left)
                    : k == columns || right == null ? TableBorder(rows[r], format, b => b.Right)
                    : TableBorder(rows[r], format, b => b.InsideV);
                // Each row of a vertical merge takes its side lines from its own cell (tdf132898_extraBorder: a 5 pt right
                // border set on the merge's continuing cells drawn beside them).
                vertical[r, k] = Edge(left?.CellBorders.Right, right?.CellBorders.Left, tableLine, left != null && right != null);
            }
        }

        // Column widths: the saved grid for fixed tables, Word's autofit for the others.
        var allowance = rows.Count > 0 ? (Width(vertical[0, 0]) + Width(vertical[0, columns])) / 2 : 0;
        // Older modes let a table as wide as its column end its last cell's text at the column's right edge, the
        // cell's margin and the line's middle beyond it, as its first cell's text begins at the indent (measured with
        // docx/local/tw-12-*, tw-14-*: margins of 0.5 and 8.5 pt reach 0.5 and 8.5 pt past the right margin, in mode 15
        // none; docx/external/libreoffice/core-ooxml-pass/fdo78904_fileHang: 432 pt between the margins, 440.7 pt).
        var reach = 0.0;
        if (Legacy && format.Justification is not ("center" or "right" or "end") && rows.Count > 0 && rows[0].Cells.Count > 0)
        {
            var firstLeft = Margins(rows[0], rows[0].Cells[0], format).Left;
            reach = allowance + Margins(rows[0], rows[0].Cells[^1], format).Right
                + (format.Indent == null ? 0 : Math.Max(firstLeft, Width(vertical[0, 0]) / 2));
        }
        // A centered or right-aligned one of automatic width by both its outer cell margins, its first cell's text at
        // the left margin and its last cell's at the right one (measured with docx/local/tac-14: cells of 2 × 6000
        // twips centered or right aligned span the column and both margins of 5.4 or 10 pt, tac-15 the column;
        // docx/external/libreoffice/extras-ooxmlexport/tdf79329); one of 100 % stays the column's width
        // (extras-ooxmlexport/sdt-company-multipara).
        else if (Legacy && !format.BidiVisual && format.Justification is "center" or "right" or "end" && format.Width is not { Type: "dxa" or "pct", Value: > 0 }
            && rows.Count > 0 && rows[0].Cells.Count > 0)
            reach = allowance + Margins(rows[0], rows[0].Cells[0], format).Left + Margins(rows[0], rows[0].Cells[^1], format).Right;
        // A percentage width is of the column's width, whatever the indent (measured with docx/local/tpi-1 to tpi-6:
        // 100 % indented -15.9 or 36 pt is 450.8 pt wide, in modes 14 and 15; docx/external/poi/bug65649).
        var grid = Grid(table, rows, columns, available - (format.Justification is "center" or "right" or "end" ? 0 : format.Indent ?? 0), allowance, fields,
            widthBase: available, lines: vertical, reach: reach);
        var x = new double[grid.Length + 1];
        for (var k = 0; k < grid.Length; k++)
            x[k + 1] = x[k] + grid[k];

        // The grid moves right by half the table's left border; every vertical line is centered on its grid line, so
        // the left border's outer edge lies at the table's left edge (measured with docx/local/tbl-geom: a 3 pt
        // inside line of a table with 0.5 pt outside lines is centered 0.25 pt right of its grid line).
        var shift = rows.Count > 0 ? Width(vertical[0, 0]) / 2 : 0;
        var firstMargin = rows.Count > 0 && rows[0].Cells.Count > 0 ? Margins(rows[0], rows[0].Cells[0], format).Left : format.Styled ? 0 : DefaultMargin;
        var rightBorder = rows.Count > 0 ? Width(vertical[0, columns]) : 0;
        var tableWidth = x[^1] + shift + rightBorder / 2;
        var indent = format.Indent ?? 0;
        // Right-to-left tables (w:bidiVisual) start at the right: their first column is the rightmost, their indent
        // and alignment count from the right (measured with docx/local/tbl-features).
        var rtl = format.BidiVisual;
        var justification = format.Justification;
        if (rtl)
            justification = justification switch { "right" or "end" => "left", "center" => "center", _ => "right" };
        var lastTextInset = rightBorder / 2 + Math.Max(rows.Count > 0 && rows[0].Cells.Count > 0 ? Margins(rows[0], rows[0].Cells[^1], format).Right : 0, rightBorder / 2);
        // Older modes put the first cell's text at the indent; a table without any indent (none in its properties or
        // style) has its first line centered on the column's edge there (measured with docx/local/c2-tblpos in modes
        // 11 to 14). A right-to-left table does so from the right (docx/local/btv-12, btv-14: tblInd 84 pt with cell
        // margins of 5.4 and 10 pt puts the right line 78.6 and 74 pt from the right margin, without tblInd at it;
        // btv-15 84 pt and at it; docx/external/libreoffice/core-ooxml-pass/FDO76312).
        var startOffset = indent - (!Legacy ? 0 : format.Indent == null ? shift : shift + Math.Max(firstMargin, shift));
        double origin0 = justification switch
        {
            "center" => (available - tableWidth) / 2,
            // Older modes end the last cell's text at the column's right edge, the table out by its margin and half its
            // line (measured with docx/local/tal-12, tal-14: tables of 200, 451.3 and 500 pt with cell margins of 5.4
            // and 10 pt end their right lines that far past the margin, within 0.12 pt; tal-15 at it).
            "right" or "end" => available - tableWidth + (rtl ? -startOffset : Legacy ? lastTextInset : 0),
            // A right-to-left one aligned to its end, at the left, likewise ends its last cell's text at the column's
            // left edge (btv-12, btv-14: jc right with cell margins of 5.4 and 10 pt puts the left line that far left
            // of the left margin; btv-15 at it).
            _ => rtl ? indent - (Legacy ? lastTextInset : 0) : startOffset,
        };
        var lastMargin = rows.Count > 0 && rows[0].Cells.Count > 0 ? Margins(rows[0], rows[0].Cells[^1], format).Right : format.Styled ? 0 : DefaultMargin;
        var box = new TableBox
        {
            Left = origin0, Width = tableWidth, Inset = justification is "center" or "right" or "end" ? 0 : indent - origin0,
            TextInsets = (shift + Math.Max(firstMargin, shift), rightBorder / 2 + Math.Max(lastMargin, rightBorder / 2)),
        };
        double X(double position) => origin0 + (rtl ? tableWidth - position : position);
        (double From, double Width) Range(double from, double to) => rtl ? (origin0 + tableWidth - to, to - from) : (origin0 + from, to - from);

        // A table right after another one continues it: the line between them is an inside line, the cell bottoms
        // or the inside line of the first's last row against the cell tops or the inside line of this one's first
        // row, neither table's own bottom or top line (measured with docx/local/tjn-a to tjn-i: a 3 pt cell bottom
        // or inside line of either table shows and takes its room, a 3 pt bottom or top line of either table not;
        // docx/external/libreoffice/extras-ooxmlexport/tdf104061_tableSectionColumns). A first row that begins a page
        // has the table's own top line (RowBox.AtPageTop).
        if (joined is { Count: > 0 } && rows.Count > 0)
        {
            for (var k = 0; k < columns; k++)
            {
                if (Owner(at[0, k]) is not { } below)
                    continue;
                var middle = X((x[k] + x[k + 1]) / 2 + shift);
                var above = joined.FirstOrDefault(e => e.From <= middle + 0.01 && middle <= e.To + 0.01).Line;
                var own = (below.CellBorders.Top ?? TableBorder(rows[0], format, b => b.InsideH)) is { Visible: true } top ? top : null;
                horizontal[0, k] = BorderLine.Resolve(above, own);
            }
        }

        // Cell content and row heights. A row's cells all take its largest top and bottom margins (measured with
        // docx/local/rowmar-15, -12: beside a cell 5 pt from its top and one 10 pt from its bottom a cell without
        // margins of its own puts its text 5 pt down, and the row is 15 pt higher than its content).
        var rowEdges = rows.Select(row => row.Cells.Count == 0 ? (Top: 0.0, Bottom: 0.0)
            : (Top: row.Cells.Max(c => Margins(row, c, format).Top), Bottom: row.Cells.Max(c => Margins(row, c, format).Bottom))).ToArray();
        (double Top, double Left, double Bottom, double Right) CellMargins(int r, CellModel cell, int last = -1)
        {
            var own = Margins(rows[r], cell, format);
            return (rowEdges[r].Top, own.Left, rowEdges[last < 0 ? r : last].Bottom, own.Right);
        }
        var topWidths = new double[rows.Count + 1];
        for (var r = 0; r <= rows.Count; r++)
        {
            for (var k = 0; k < columns; k++)
            {
                var width = Width(horizontal[r, k]);
                topWidths[r] = Math.Max(topWidths[r], inMargin[r, k] ? Math.Max(0, width - rowEdges[r].Top) : width);
            }
        }
        var laid = new Dictionary<CellModel, CellContent>();
        // The content of cells that hold nested tables laid out again, as first, for cuts through those (CellPart.Cuts).
        var laidAgain = new Dictionary<CellModel, Func<IReadOnlyList<CellCut>, CellContent>>();
        var contentHeights = new double[rows.Count];
        for (var r = 0; r < rows.Count; r++)
        {
            foreach (var cell in rows[r].Cells)
            {
                if (origin.ContainsKey(cell))
                    continue;
                var c0 = Math.Min(cell.Column, columns);
                var c1 = Math.Min(cell.Column + cell.Format.GridSpan, columns);
                var margins = CellMargins(r, cell);
                var leftLine = Width(vertical[r, c0]);
                var rightLine = Width(vertical[r, c1]);
                var textLeft = x[c0] + shift + Math.Max(margins.Left, leftLine / 2);
                var textRight = x[c1] + shift - Math.Max(margins.Right, rightLine / 2);
                var outerInside = cellInside;
                cellInside = (x[c0] + shift + leftLine / 2 - textLeft, x[c1] + shift - rightLine / 2 - textLeft);
                var (cellWidth, cellEdge, cellShading, cellNeighbors) = (Math.Max(1, textRight - textLeft), textLeft - x[c0] - shift, CellShading(cell, rows[r]),
                    neighbors.GetValueOrDefault(cell, (NoParagraph, NoParagraph)));
                var content = Content(cell, cellWidth, fields, cellNeighbors, cellEdge, cellShading);
                if (!Rotated(cell) && cell.Blocks.Any(b => b is TableBlock))
                {
                    // Laid out again in the state of the layout it was first laid out in.
                    var (inside, nested, state, cellLeft) = (cellInside, inCell, (paragraphs.Vertical, paragraphs.TurnEastAsian, paragraphs.Unaligned), textLeft);
                    laidAgain[cell] = cuts =>
                    {
                        var was = (cellInside, inCell, (paragraphs.Vertical, paragraphs.TurnEastAsian, paragraphs.Unaligned));
                        (cellInside, inCell, (paragraphs.Vertical, paragraphs.TurnEastAsian, paragraphs.Unaligned)) = (inside, nested, state);
                        try
                        {
                            return Content(cell, cellWidth, fields, cellNeighbors, cellEdge, cellShading, cuts) with { Left = cellLeft, Width = cellWidth };
                        }
                        finally
                        {
                            (cellInside, inCell, (paragraphs.Vertical, paragraphs.TurnEastAsian, paragraphs.Unaligned)) = was;
                        }
                    };
                }
                cellInside = outerInside;
                laid[cell] = content with { Left = textLeft, Width = cellWidth };
                if (!span.ContainsKey(cell) && !(Rotated(cell) && TurnsInRow(rows[r])))
                    contentHeights[r] = Math.Max(contentHeights[r], margins.Top + content.Height + margins.Bottom);
            }
        }
        var heights = new double[rows.Count];
        for (var r = 0; r < rows.Count; r++)
        {
            var f = rows[r].Format;
            heights[r] = contentHeights[r];
            // An at-least height lies between the row's top and bottom margins; an exact one holds the top line and
            // margin, the bottom margin comes below it (rowmar-15: rows of 30 pt at least with margins of 5 and 3 pt
            // are 38 pt high, rows of exactly 30 pt 33 pt).
            if (f.Height is { } h && h > 0)
                heights[r] = f.HeightRule == "exact" ? Math.Max(0, h - topWidths[r]) + rowEdges[r].Bottom : Math.Max(heights[r], rowEdges[r].Top + h + rowEdges[r].Bottom);
        }
        // A vertically merged cell taller than its rows makes its last row higher.
        foreach (var (cell, rowsSpanned) in span)
        {
            var r0 = rows.FindIndex(row => row.Cells.Contains(cell));
            if (r0 < 0 || Rotated(cell) && TurnsInRow(rows[r0]))
                continue;
            var last = Math.Min(rows.Count - 1, r0 + rowsSpanned - 1);
            var margins = CellMargins(r0, cell, last);
            var needed = margins.Top + laid[cell].Height + margins.Bottom;
            var have = 0.0;
            for (var r = r0; r < r0 + rowsSpanned && r < rows.Count; r++)
                have += heights[r] + (r > r0 ? topWidths[r] : 0);
            if (needed > have && rows[last].Format.HeightRule != "exact")
                heights[last] += needed - have;
        }

        // Row boxes.
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var specified = row.Format.Height is { } rh && rh > 0 ? rh : 0;
            var rowBox = new RowBox
            {
                Header = row.Format.Header, CantSplit = row.Format.CantSplit, TopWidth = topWidths[r], Height = topWidths[r] + heights[r],
                Exact = specified > 0 && row.Format.HeightRule == "exact",
                Least = row.Format.HeightRule == "exact" || specified <= 0 ? 0 : rowEdges[r].Top + specified + rowEdges[r].Bottom,
                // A nested table's row needs it in every mode (measured with docx/local/nft-15-m*: a line of a nested
                // cell whose 10 pt bottom margin would reach past the page's bottom goes on to the next page).
                MarginBelowCut = context.Profile.CellMarginBelowCut || inCell,
                AnchorsReach = context.Profile.AnchorsReachPastCut,
            };
            // Shading of every cell the row passes through, also cells merged from above.
            for (var k = 0; k < columns; k++)
            {
                if (at[r, k] is not { } here || k > 0 && at[r, k - 1] == here)
                    continue;
                var owner = Owner(here)!;
                if (CellShading(owner, row) is { } fill)
                {
                    var c1 = Math.Min(columns, here.Column + here.Format.GridSpan);
                    var (from, width) = Range(x[k] + shift, x[c1] + shift);
                    rowBox.Shading.Add((from, width, fill));
                }
            }
            foreach (var cell in row.Cells)
            {
                if (origin.ContainsKey(cell))
                    continue;
                var c0 = Math.Min(cell.Column, columns);
                var c1 = Math.Min(cell.Column + cell.Format.GridSpan, columns);
                var rowsSpanned = span.GetValueOrDefault(cell, 1);
                var total = 0.0;
                for (var k = r; k < Math.Min(rows.Count, r + rowsSpanned); k++)
                    total += heights[k] + (k > r ? topWidths[k] : 0);
                var content = laid[cell];
                var margins = CellMargins(r, cell, Math.Min(rows.Count, r + rowsSpanned) - 1);
                // The cell's part laid out for a height of the rows it spans: all of them, or where a page or column
                // break cuts the cell, its part before the break (see CellPart.Along).
                var cellRow = r;
                var relayout = laidAgain.GetValueOrDefault(cell);
                var rowsTotal = total;
                CellPart Part(double total)
                {
                    var free = total - margins.Top - (Rotated(cell) ? content.Height : content.Reach ?? content.Height) - margins.Bottom;
                    var lowered = cell.Format.VerticalAlign switch
                    {
                        "center" => Math.Max(0, free) / 2,
                        "bottom" => Math.Max(0, free),
                        _ => 0,
                    };
                    return Build(content, total, lowered, Rotated(cell) ? null : cell.Format.VerticalAlign,
                        rowsSpanned > 1 && (Rotated(cell) || cell.Format.VerticalAlign is "center" or "bottom") ? (rowsSpanned, Part) : null,
                        relayout != null ? new CellCuts(Parts, topWidths[cellRow] + margins.Top) : null);
                }
                // The parts of the content between cuts through it, each with where it begins down the content as first
                // laid out: laid out again for the cuts through its nested tables, the others made in the parts as they
                // stand (see CellPart.Cuts); null where the content does not break so again.
                List<(CellPart Part, double Origin)>? Parts(IReadOnlyList<CellCut> cuts)
                {
                    var through = cuts.Where(c => c.Table > 0).ToList();
                    var redone = through.Count == 0 ? content : relayout!(through);
                    if (redone.Boundaries.Count != through.Count)
                        return null;
                    var top = topWidths[cellRow] + margins.Top;
                    var ends = redone.Boundaries.Append(new CellBoundary(redone.Lines.Count, redone.Fills.Count, redone.Strokes.Count, redone.Anchors.Count, 0)).ToList();
                    var from = new CellBoundary(0, 0, 0, 0, 0);
                    CellPart Piece(CellBoundary end, bool first)
                    {
                        var o = from.Origin;
                        var piece = new CellContent(redone.Lines.Take(end.Lines).Skip(from.Lines).Select(l => l with { Top = l.Top - o }).ToList(),
                            redone.Fills.Take(end.Fills).Skip(from.Fills).Select(f => f with { Y = f.Y - o }).ToList(),
                            redone.Strokes.Take(end.Strokes).Skip(from.Strokes).Select(s => s.Offset(0, -o)).ToList(), 0, redone.Left, redone.Width)
                        {
                            Anchors = redone.Anchors.Take(end.Anchors).Skip(from.Anchors).Select(a => a.Offset(0, -o)).ToList(),
                        };
                        var reach = piece.Lines.Select(l => l.Top + l.Line.Height).Concat(piece.Fills.Select(f => f.Y + f.Height)).DefaultIfEmpty(0).Max();
                        from = end;
                        return Build(piece, Math.Max(rowsTotal, top + reach + margins.Bottom), 0, first ? cell.Format.VerticalAlign : null, null, null);
                    }
                    var parts = new List<(CellPart Part, double Origin)>();
                    var (current, origin) = (Piece(ends[0], true), 0.0);
                    var next = 1;
                    foreach (var cut in cuts)
                    {
                        if (cut.Table > 0)
                        {
                            parts.Add((current, origin));
                            origin = from.Origin;
                            current = Piece(ends[next++], false);
                        }
                        else
                        {
                            var (head, tail) = current.Partition(top + cut.Top - origin, 0, cut.At - origin, cut: true);
                            parts.Add((head, origin));
                            (current, origin) = (tail, cut.At);
                        }
                    }
                    parts.Add((current, origin));
                    return parts;
                }
                CellPart Build(CellContent laidOut, double total, double lowered, string? align, (int Rows, Func<double, CellPart> Part)? along, CellCuts? cuts)
                {
                    var (textFrom, _) = Range(laidOut.Left, laidOut.Left + laidOut.Width);
                    var top = topWidths[cellRow] + margins.Top;
                    var (cellFrom, cellWidth) = Range(x[c0] + shift, x[c1] + shift);
                    var part = new CellPart
                    {
                        ContentTop = top,
                        BottomMargin = margins.Bottom,
                        Diagonals = (cellFrom, cellFrom + cellWidth, cell.CellBorders.TopLeftToBottomRight, cell.CellBorders.TopRightToBottomLeft),
                        Align = align,
                        Lowered = Rotated(cell) ? 0 : lowered,
                        Span = Rotated(cell) ? 1 : rowsSpanned,
                        Along = along,
                        Cuts = cuts,
                    };
                    if (Rotated(cell))
                    {
                        // tbRl turns clockwise about the content's top right corner, btLr counterclockwise about its bottom
                        // left (measured with docx/local/tbl-features: lines start at the cell's top and at its bottom);
                        // tbRlV and tbLrV run down like tbRl, tbLrV's lines from the left (measured with docx/local/vertical5).
                        var down = cell.Format.TextDirection is "tbRl" or "tbRlV" or "tbLrV";
                        var fromLeft = cell.Format.TextDirection == "tbLrV";
                        var pivotX = down ? textFrom + laidOut.Width : textFrom;
                        var pivotY = down ? top : topWidths[cellRow] + total - margins.Bottom;
                        var turned = RotatedLines(cell, laidOut, total - margins.Top - margins.Bottom, fields).ToList();
                        // Vertical alignment moves the lines across the cell, towards its right for btLr and its left for
                        // tbRl, by the room the cell's width leaves beside its lines and side lines, its margins not
                        // counted (measured with docx/local/rcv-*: 20, 40 and 80 pt cells of one and two lines of Calibri
                        // 11, each line 13.78 pt across, centered and bottom aligned within 0.06 pt).
                        if (!fromLeft && cell.Format.VerticalAlign is "center" or "bottom" && turned.Count > 0)
                        {
                            var room = cellWidth - Width(vertical[cellRow, c0]) - Width(vertical[cellRow, c1]) - turned.Max(l => l.Top + l.Line.Height);
                            var across = Math.Max(0, room) / (cell.Format.VerticalAlign == "center" ? 2 : 1);
                            pivotX += down ? -across : across;
                        }
                        foreach (var line in turned)
                        {
                            var lineTop = fromLeft ? laidOut.Width - line.Top - line.Line.Height : line.Top;
                            part.Lines.Add(line with { Line = fromLeft ? line.Line.Flipped() : line.Line, Left = pivotX + line.Left, Top = pivotY + lineTop, Angle = down ? 90 : -90, PivotX = -line.Left, PivotY = -lineTop });
                        }
                    }
                    else
                    {
                        foreach (var line in laidOut.Lines)
                            part.Lines.Add(line with { Left = textFrom + line.Left, Top = top + laidOut.Lowering(line, lowered) + line.Top });
                    }
                    // What the cell holds is cut at its inside: between its side lines, from below the row's top line
                    // to its bottom (measured with docx/external/libreoffice/core-layout/para-border-in-cell-clip: Word
                    // clips a paragraph's borders past its lines and below a row of exact height away).
                    var (insideLeft, insideRight) = (cellFrom + Width(vertical[cellRow, rtl ? c1 : c0]) / 2, cellFrom + cellWidth - Width(vertical[cellRow, rtl ? c0 : c1]) / 2);
                    foreach (var fill in laidOut.Fills)
                    {
                        var (fx, fy) = (textFrom + fill.X, top + laidOut.Lowering(fill, lowered) + fill.Y);
                        var (l, t, rgt, b) = (Math.Max(fx, insideLeft), Math.Max(fy, topWidths[cellRow]), Math.Min(fx + fill.Width, insideRight), Math.Min(fy + fill.Height, topWidths[cellRow] + total));
                        if (rgt > l + 0.001 && b > t + 0.001)
                            part.Fills.Add(fill with { X = l, Y = t, Width = rgt - l, Height = b - t });
                    }
                    foreach (var stroke in laidOut.Strokes)
                        part.Strokes.Add(stroke.Offset(textFrom, top + laidOut.Lowering(stroke, lowered)));
                    if (!Rotated(cell))
                    {
                        // Drawings in the cell: across from its text area or from the inner edge of its left line, down from
                        // its content's top (measured with docx/local/cell-anchor).
                        var inner = vertical[cellRow, c0] is { Visible: true } edge ? edge.Total / 2 : 0;
                        foreach (var anchor in laidOut.Anchors)
                        {
                            var moved = anchor.Offset(textFrom, top + lowered);
                            // Before Word 2013 a drawing of layoutInCell 0 placed by offsets is placed as one of the text
                            // from its paragraph or line in the cell, the page and the column for the rest (measured with
                            // docx/local/cig-noin14, cig-noin12: 150 pt right of the column's left edge, the paragraph's top;
                            // docx/external/libreoffice/extras-ooxmlexport/tdf162541_notLayoutInCell_paraLeft, tdf129888dml:
                            // a page-relative line in mode 14); one aligned across stands so in the page's frames too
                            // (docx/local/lica-12, lica-14: aligned left from the column, the margin and the page at the margin
                            // and left of it, not in the cell; tdf162541) but where it is placed from its line, then in the
                            // cell (tdf162551: aligned left of the page at the cell's text, centered on its line).
                            var placement = anchor.Anchor.Anchor;
                            if (!InCell(placement) && (placement.Element(Docx.Drawings.Wp + "positionH")?.Element(Docx.Drawings.Wp + "align") == null
                                || (string?)placement.Element(Docx.Drawings.Wp + "positionV")?.Attribute("relativeFrom") != "line"))
                            {
                                // Placed from its paragraph, it counts from the row's top, whichever paragraph of the cell it
                                // is in (measured with docx/local/lic-12, lic-14: drawings 20 pt below the first and the third
                                // paragraph of a cell under a 3 pt top line and a 10 pt margin both 20 pt below the row's top;
                                // lic-15 from their paragraphs; docx/external/libreoffice/extras-ooxmlexport/
                                // tdf135595_HFtableWrap_c12, found by agent BE). In a row going on over pages it counts from
                                // the top of the row's part on its paragraph's page (LibreOffice's core-objectpositioning/
                                // tdf154863-img-move-crash: seven pictures in a cell of four pages and more, mode 12, each its
                                // offset below the top margin of its paragraph's page).
                                if ((string?)anchor.Anchor.Anchor.Element(Docx.Drawings.Wp + "positionV")?.Attribute("relativeFrom") is null or "paragraph")
                                    moved = moved with { ParagraphTop = 0, FromRowTop = true };
                                part.Anchors.Add(moved);
                                continue;
                            }
                            part.Anchors.Add(moved with
                            {
                                Text = moved.Text ?? (textFrom, laidOut.Width),
                                Cell = moved.Cell ?? (cellFrom + inner, cellWidth - inner),
                                Content = moved.Content ?? (top, Math.Max(0, total - top - margins.Bottom)),
                                Inside = moved.Inside ?? (insideLeft, insideRight),
                            });
                        }
                    }
                    return part;
                }
                rowBox.Cells.Add(Part(total));
            }
            // Lines at the row's top, from the outer edge of the vertical line at their start to that at their end; where
            // the row begins a part of the table on a page, the table's top line (RowBox.AtPageTop).
            if (r > 0 || joined is { Count: > 0 })
            {
                (rowBox.PageTopBorders, rowBox.PageTopStrokes) = ([], []);
                // A cell merged down from the row above has its own top line there, not that of the cell it goes on
                // (measured with docx/local/vmt-*: the 1.5 pt top line of a cell merged over three rows gone at the top of
                // page 2 where its third row goes on, a 3 pt top line of the merged cell of that row drawn there, the row's
                // content below it, in modes 11 and 15; LibreOffice's extras-layout/tdf127118).
                for (var k = 0; k < columns; k++)
                {
                    var below = at[r, k];
                    if (below == null || k > 0 && at[r, k - 1] == below)
                        continue;
                    if (Edge(null, below.CellBorders.Top, TableBorder(row, format, b => b.Top), false) is { Visible: true } line)
                    {
                        var (from, to) = Span(k, r);
                        var (left, width) = Range(from, to);
                        Horizontal(line, left, width, 0, rowBox.PageTopBorders, rowBox.PageTopStrokes);
                        rowBox.PageTopWidth = Math.Max(rowBox.PageTopWidth, Width(line));
                    }
                }
            }
            for (var k = 0; k < columns; k++)
            {
                if (horizontal[r, k] is { Visible: true } line)
                {
                    var (from, to) = Span(k, r);
                    var (left, width) = Range(from, to);
                    Horizontal(line, left, width, 0, rowBox.TopBorders, rowBox.TopStrokes);
                }
                if (horizontal[r + 1, k] is { Visible: true } below)
                {
                    var (from, to) = Span(k, r);
                    var (left, width) = Range(from, to);
                    Horizontal(below, left, width, 0, rowBox.BottomBorders, rowBox.BottomStrokes);
                    rowBox.BottomBorderWidth = Math.Max(rowBox.BottomBorderWidth, below.Total);
                }
            }
            // Vertical lines through the row, centered on their grid lines.
            for (var k = 0; k <= columns; k++)
            {
                if (vertical[r, k] is { Visible: true } line)
                {
                    var center = X(x[k] + shift);
                    foreach (var (offset, thickness, color) in Painted(line))
                        rowBox.Verticals.Add(new Stripe(center - line.Total / 2 + offset, thickness, color, Dash(line)));
                }
            }
            box.Rows.Add(rowBox);
        }
        // What a table right after this one meets below its last row.
        if (rows.Count > 0)
        {
            var last = rows.Count - 1;
            for (var k = 0; k < columns; k++)
            {
                if (Owner(at[last, k]) is not { } cell || k > 0 && Owner(at[last, k - 1]) == cell)
                    continue;
                var end = k;
                while (end + 1 < columns && Owner(at[last, end + 1]) == cell)
                    end++;
                var (from, width) = Range(x[k] + shift, x[end + 1] + shift);
                var line = (cell.CellBorders.Bottom ?? TableBorder(rows[last], format, b => b.InsideH)) is { Visible: true } bottom ? bottom : null;
                box.BottomEdges.Add((from, from + width, line));
            }
        }
        return box;

        // A horizontal line's extent over grid column k: over the vertical lines at its ends.
        (double From, double To) Span(int k, int r)
        {
            var from = x[k] + shift - Width(vertical[r, k]) / 2;
            var to = x[k + 1] + shift + Width(vertical[r, k + 1]) / 2;
            return (from, to);
        }
    }

    /// <summary>
    /// A table with cell spacing (w:tblCellSpacing, measured with docx/local/tbl-features): the table's frame, twice
    /// the spacing, then the cells, each with its own borders, twice the spacing apart; text starts after a cell's
    /// whole border and its margin.
    /// </summary>
    TableBox Spaced(TableModel table, List<RowModel> rows, int columns, CellModel?[,] at, double available, double spacing, PageFields? fields,
        Dictionary<CellModel, (string? Before, string? After)> neighbors, Dictionary<CellModel, (int Row, CellModel Cell)> origin,
        Dictionary<CellModel, int> span)
    {
        var format = table.Format;
        var gap = 2 * spacing;
        BorderLine? Visible(BorderLine? b) => b is { Visible: true } ? b : null;
        var outer = (Top: Visible(format.Borders.Top), Left: Visible(format.Borders.Left), Bottom: Visible(format.Borders.Bottom), Right: Visible(format.Borders.Right));
        // A table of a width holds its gaps and half its frame within it, the cells their own borders (measured with
        // docx/local/altchunk-tables: a 300 px HTML table is 225.75 pt wide over its frame).
        var extras = gap * (columns + 1) + (Width(outer.Left) + Width(outer.Right)) / 2;
        var sized = format.Width is { Type: "dxa" or "pct" } tw && tw.Value > 0;
        // Autofit columns of cells without preferred widths hold the cells' own borders too, which every spaced cell
        // draws on both sides (HTML tables Word imports, docx/local/altchunk, altchunk-tables): a cell's own, else the
        // table's inside line or side; they take their room from what the columns may have, so a table its text fills
        // is as wide as the page (docx/external/poi/bib-chernigovka.netdo.ru_download_docs_17459). A cell's border set
        // to nil or none draws nothing and takes no room (docx/external/poi/bug57031: SAS tables whose cells clear
        // their borders inside a 0.75 pt frame have columns as wide as their text and margins).
        double[]? edges = null;
        if (!sized && format.Layout != "fixed" && rows.SelectMany(r => r.Cells).All(c => c.Format.Width is not { Type: "dxa" or "pct" } cw || cw.Value <= 0))
        {
            var inside = Visible(format.Borders.InsideV);
            edges = new double[columns];
            for (var k = 0; k < columns; k++)
            {
                var single = rows.SelectMany(r => r.Cells).Where(c => c.Column == k && c.Format.GridSpan <= 1).ToList();
                edges[k] = single.Count == 0 ? Width(k == 0 ? outer.Left : inside) + Width(k == columns - 1 ? outer.Right : inside)
                    : single.Max(cell => Width(cell.CellBorders.Left ?? (k == 0 ? outer.Left : inside))
                                         + Width(cell.CellBorders.Right ?? (k == columns - 1 ? outer.Right : inside)));
            }
        }
        var grid = Grid(table, rows, columns, available - extras - (edges?.Sum() ?? 0), sized ? (Width(outer.Left) + Width(outer.Right)) / 2 : 0, fields,
            gaps: sized ? gap * (columns + 1) : 0, widthBase: available);
        if (edges != null)
        {
            for (var k = 0; k < grid.Length && k < edges.Length; k++)
                grid[k] += edges[k];
        }
        var x = new double[grid.Length + 1];
        for (var k = 0; k < grid.Length; k++)
            x[k + 1] = x[k] + grid[k];
        var left0 = Width(outer.Left) / 2 + gap;
        // A fixed table's grid columns hold the gaps between its cells (docx/external/libreoffice/extras-ooxmlexport/
        // tdf123104: cells 1.5 pt narrower than their grid columns, the table as wide as its grid), one of a width of
        // its own takes them from that width as autofit tables do (measured with docx/local/tsl-12, -14, -15: 300 pt
        // with spacing of 2 pt between the frame's lines' middles, cells of 143.9 pt).
        var gridHoldsGaps = format.Layout == "fixed" && !sized;
        double CellX(int k) => left0 + x[k] + (gridHoldsGaps ? 0 : k * gap);
        var tableWidth = CellX(columns) - gap + gap + Width(outer.Right) / 2;
        // Before Word 2013 the first cell's text stands at the indent, the table's frame, gap, the cell's line (half of
        // it without a gap) and its margin left of it, as at tables without spacing; without an indent the frame's line
        // is centred on the column's edge (measured with docx/local/tsl-12, tsl-14: tblInd 720 and 0 with spacing 0 and
        // 40 twips and margins of 5.4 pt put the text at them, without an indent the frame's line about the margin;
        // tsl-15 at them; docx/local/hk-*s; docx/external/libreoffice/core-layout/floattable-del-empty).
        var firstCell = rows.Count > 0 && rows[0].Cells.Count > 0 ? rows[0].Cells[0] : null;
        var legacyInset = firstCell == null ? 0
            : Width(outer.Left) + gap + Width(firstCell.CellBorders.Left ?? format.Borders.Left) / 2 + Margins(rows[0], firstCell, format).Left;
        var origin0 = format.Justification switch
        {
            "center" => (available - tableWidth) / 2,
            "right" or "end" => available - tableWidth,
            _ when Legacy => format.Indent is { } legacyIndent ? legacyIndent - legacyInset : -Width(outer.Left) / 2,
            _ => format.Indent ?? 0,
        };
        var frameTop = Width(outer.Top) + gap;
        var box = new TableBox { Left = origin0, Width = tableWidth, Frame = outer, ContinuationTop = Width(outer.Top) + spacing };
        // The cells laid out row by row and the rows' heights; a cell merged down (w:vMerge) counts in the last of its
        // rows, which grows where they are lower together, and the cells it continues in draw nothing (measured with
        // docx/external/poi/bug57031: a SAS table's two-line heading merged over two header rows of one line).
        var allParts = new List<List<(CellModel Cell, CellContent Content, double Left, double Right, BorderSet Borders, (double Top, double Left, double Bottom, double Right) Margins)>>();
        var heights = new double[rows.Count];
        var cellHeights = new Dictionary<CellModel, double>();
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var height = 0.0;
            var parts = new List<(CellModel Cell, CellContent Content, double Left, double Right, BorderSet Borders, (double Top, double Left, double Bottom, double Right) Margins)>();
            foreach (var cell in row.Cells)
            {
                if (origin.ContainsKey(cell))
                    continue;
                var c0 = Math.Min(cell.Column, columns);
                var c1 = Math.Min(cell.Column + cell.Format.GridSpan, columns);
                var left = CellX(c0);
                var right = CellX(c1) - gap;
                // A cell's own borders, else the table's inside lines between cells and its sides at the edges.
                var borders = new BorderSet
                {
                    Top = cell.CellBorders.Top ?? (r == 0 ? format.Borders.Top : format.Borders.InsideH) ?? format.Borders.InsideH,
                    Bottom = cell.CellBorders.Bottom ?? (r == rows.Count - 1 ? format.Borders.Bottom : format.Borders.InsideH) ?? format.Borders.InsideH,
                    Left = cell.CellBorders.Left ?? (c0 == 0 ? format.Borders.Left : format.Borders.InsideV) ?? format.Borders.InsideV,
                    Right = cell.CellBorders.Right ?? (c1 == columns ? format.Borders.Right : format.Borders.InsideV) ?? format.Borders.InsideV,
                };
                var margins = Margins(row, cell, format);
                // Without a gap between them the cells' text stands half their side lines in, as in tables without spacing
                // (measured with docx/local/tsl-12, -14, -15: spacing 0 puts the text 0.24 pt further left than 2 pt do).
                var lineShare = gap > 0 ? 1 : 0.5;
                var textLeft = left + Width(borders.Left) * lineShare + margins.Left;
                var textRight = right - Width(borders.Right) * lineShare - margins.Right;
                var outerInside = cellInside;
                cellInside = (-margins.Left, textRight - textLeft + margins.Right);
                var content = Content(cell, Math.Max(1, textRight - textLeft), fields, neighbors.GetValueOrDefault(cell, (NoParagraph, NoParagraph)), textLeft - left,
                    Shading.Color(cell.Format.Shading));
                cellInside = outerInside;
                parts.Add((cell, content with { Left = textLeft, Width = Math.Max(1, textRight - textLeft) }, left, right, borders, margins));
                cellHeights[cell] = Width(borders.Top) + margins.Top + content.Height + margins.Bottom + Width(borders.Bottom);
                if (!span.ContainsKey(cell))
                    height = Math.Max(height, cellHeights[cell]);
            }
            // An at-least height lies between the row's largest top and bottom margins, as in tables without spacing
            // (tdf123104: a row of 49.5 pt at least with margins of 3.75 pt 57 pt high).
            if (row.Format.Height is { } h && h > 0)
                height = row.Format.HeightRule == "exact" ? h
                    : Math.Max(height, h + (row.Cells.Count == 0 ? 0 : row.Cells.Max(c => Margins(row, c, format).Top) + row.Cells.Max(c => Margins(row, c, format).Bottom)));
            heights[r] = height;
            allParts.Add(parts);
        }
        foreach (var (cell, rowsSpanned) in span)
        {
            var r0 = rows.FindIndex(row => row.Cells.Contains(cell));
            if (r0 < 0 || !cellHeights.TryGetValue(cell, out var needed))
                continue;
            var last = Math.Min(rows.Count - 1, r0 + rowsSpanned - 1);
            var have = 0.0;
            for (var k = r0; k <= last; k++)
                have += heights[k] + (k > r0 ? gap : 0);
            if (needed > have && rows[last].Format.HeightRule != "exact")
                heights[last] += needed - have;
        }
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            // Rows split where they may, as other tables' rows, unless their cells' frames (borders, shading) would
            // have to (docx/external/poi/drawing: a borderless table with cell spacing 0 holding a whole column).
            var specified = row.Format.Height is { } rh && rh > 0 ? rh : 0;
            var rowBox = new RowBox
            {
                Header = row.Format.Header, CantSplit = row.Format.CantSplit,
                Exact = specified > 0 && row.Format.HeightRule == "exact", Least = row.Format.HeightRule == "exact" ? 0 : specified,
            };
            var rowHeight = heights[r];
            rowBox.Height = (r == 0 ? frameTop : 0) + rowHeight + gap;
            var top0 = r == 0 ? frameTop : 0;
            foreach (var (cell, content, left, right, borders, margins) in allParts[r])
            {
                // A cell merged down reaches over its rows and the gaps between them.
                var rowsSpanned = span.GetValueOrDefault(cell, 1);
                var height = rowHeight;
                for (var k = r + 1; k < Math.Min(rows.Count, r + rowsSpanned); k++)
                    height += gap + heights[k];
                if (Shading.Color(cell.Format.Shading) is { } fill)
                    rowBox.Frames.Add(new FillRect(origin0 + left, top0, right - left, height, fill));
                foreach (var (line, side, rect) in new[]
                {
                    (borders.Top, "top", new FillRect(origin0 + left, top0, right - left, Width(borders.Top), RgbColor.Black)),
                    (borders.Bottom, "bottom", new FillRect(origin0 + left, top0 + height - Width(borders.Bottom), right - left, Width(borders.Bottom), RgbColor.Black)),
                    (borders.Left, "left", new FillRect(origin0 + left, top0, Width(borders.Left), height, RgbColor.Black)),
                    (borders.Right, "right", new FillRect(origin0 + right - Width(borders.Right), top0, Width(borders.Right), height, RgbColor.Black)),
                })
                {
                    if (line is { Visible: true })
                        rowBox.Frames.Add(rect with { Color = line.ColorOf(side, cell: true) });
                }
                var part = new CellPart { ContentTop = top0 + Width(borders.Top) + margins.Top, BottomMargin = margins.Bottom, Span = rowsSpanned };
                // Vertical alignment in the cell, as in tables without spacing (docx/external/poi/
                // bib-chernigovka.netdo.ru_download_docs_17459: dates centered beside two lines of text).
                var free = height - Width(borders.Top) - margins.Top - (content.Reach ?? content.Height) - margins.Bottom - Width(borders.Bottom);
                var lowered = cell.Format.VerticalAlign switch
                {
                    "center" => Math.Max(0, free) / 2,
                    "bottom" => Math.Max(0, free),
                    _ => 0,
                };
                var contentTop = part.ContentTop + lowered;
                foreach (var line in content.Lines)
                    part.Lines.Add(line with { Left = origin0 + content.Left + line.Left, Top = contentTop - lowered + content.Lowering(line, lowered) + line.Top });
                foreach (var nested in content.Fills)
                    part.Fills.Add(nested with { X = origin0 + content.Left + nested.X, Y = contentTop - lowered + content.Lowering(nested, lowered) + nested.Y });
                foreach (var stroke in content.Strokes)
                    part.Strokes.Add(stroke.Offset(origin0 + content.Left, contentTop - lowered + content.Lowering(stroke, lowered)));
                foreach (var anchor in content.Anchors)
                {
                    var moved = anchor.Offset(origin0 + content.Left, contentTop);
                    if (!InCell(anchor.Anchor.Anchor) && anchor.Anchor.Anchor.Element(Docx.Drawings.Wp + "positionH")?.Element(Docx.Drawings.Wp + "align") == null)
                    {
                        part.Anchors.Add(moved);
                        continue;
                    }
                    part.Anchors.Add(moved with
                    {
                        Text = moved.Text ?? (origin0 + content.Left, content.Width),
                        Cell = moved.Cell ?? (origin0 + left + Width(borders.Left), right - left - Width(borders.Left) - Width(borders.Right)),
                        Content = moved.Content ?? (part.ContentTop, Math.Max(0, height - part.ContentTop + top0 - margins.Bottom - Width(borders.Bottom))),
                        Inside = moved.Inside ?? (origin0 + left + Width(borders.Left), origin0 + right - Width(borders.Right)),
                    });
                }
                rowBox.Cells.Add(part);
            }
            box.Rows.Add(rowBox);
        }
        // The frame's bottom below the last row.
        if (box.Rows.Count > 0)
            box.Rows[^1].BottomBorderWidth = Width(outer.Bottom);
        return box;
    }

    /// <summary>The space a border takes across: all its strokes and gaps.</summary>
    static double Width(BorderLine? line) => line is { Visible: true } ? line.Total : 0;

    /// <summary>
    /// A border's strokes across its total width: one for single and thick lines, two for double (line, gap, line),
    /// three for triple; thin and thick lines with 0.75 pt for thin lines and small gaps (measured with
    /// docx/local/tbl-features).
    /// </summary>
    /// <summary>
    /// The strokes of a line with their colors: embossed and engraved lines in their shades top to bottom and left to
    /// right on every side (see BorderLine.Shades); others all in the line's color.
    /// </summary>
    internal static IEnumerable<(double Offset, double Width, RgbColor Color)> Painted(BorderLine line)
    {
        if (line.Style is "threeDEmboss" or "threeDEngrave")
        {
            var shades = line.Shades();
            var k = 0;
            foreach (var (offset, width) in Stripes(line))
                yield return (offset, width, shades[Math.Min(k++, 2)]);
            yield break;
        }
        foreach (var (offset, width) in Stripes(line))
            yield return (offset, width, line.Color);
    }

    internal static IEnumerable<(double Offset, double Width)> Stripes(BorderLine line)
    {
        var w = line.Width;
        switch (line.Style)
        {
            // Embossed and engraved lines: a thin line, the line at the width and a thin line (BorderLine.Thin).
            case "threeDEmboss" or "threeDEngrave":
                yield return (0, line.Thin);
                yield return (line.Thin, w);
                yield return (line.Thin + w, line.Thin);
                break;
            case "double":
                yield return (0, w);
                yield return (2 * w, w);
                break;
            case "triple":
                yield return (0, w);
                yield return (2 * w, w);
                yield return (4 * w, w);
                break;
            // Top to bottom and left to right on every side, as Word draws them (measured with docx/local/
            // tblcompound-24, -12, as page borders): thin lines and small gaps 0.75 pt, medium ones half the width,
            // large gaps the width with 1.5 pt lines beside them.
            case "thinThickSmallGap":
                yield return (0, w);
                yield return (w + 0.75, 0.75);
                break;
            case "thickThinSmallGap":
                yield return (0, 0.75);
                yield return (1.5, w);
                break;
            case "thinThickMediumGap":
                yield return (0, w);
                yield return (1.5 * w, w / 2);
                break;
            case "thickThinMediumGap":
                yield return (0, w / 2);
                yield return (w, w);
                break;
            case "thinThickLargeGap":
                yield return (0, 1.5);
                yield return (1.5 + w, 0.75);
                break;
            case "thickThinLargeGap":
                yield return (0, 0.75);
                yield return (0.75 + w, 1.5);
                break;
            case "thinThickThinSmallGap":
                yield return (0, 0.75);
                yield return (1.5, w);
                yield return (w + 2.25, 0.75);
                break;
            case "thinThickThinMediumGap":
                yield return (0, w / 2);
                yield return (w, w);
                yield return (2.5 * w, w / 2);
                break;
            case "thinThickThinLargeGap":
                yield return (0, 0.75);
                yield return (0.75 + w, 1.5);
                yield return (2.25 + 2 * w, 0.75);
                break;
            default:
                yield return (0, w);
                break;
        }
    }

    /// <summary>The SVG dash pattern of dotted and dashed borders, or null for solid ones.</summary>
    static string? Dash(BorderLine line)
    {
        var w = Math.Max(0.25, line.Width);
        return line.Style switch
        {
            "dotted" => $"{Units.Css(w)} {Units.Css(w)}",
            "dashed" or "dashSmallGap" => $"{Units.Css(3 * w)} {Units.Css(line.Style == "dashed" ? 3 * w : w)}",
            "dotDash" or "dashDotStroked" => $"{Units.Css(3 * w)} {Units.Css(w)} {Units.Css(w)} {Units.Css(w)}",
            "dotDotDash" => $"{Units.Css(3 * w)} {Units.Css(w)} {Units.Css(w)} {Units.Css(w)} {Units.Css(w)} {Units.Css(w)}",
            _ => null,
        };
    }

    /// <summary>A horizontal border as rectangles (solid lines) or strokes (dotted and dashed lines).</summary>
    static void Horizontal(BorderLine line, double left, double width, double y, List<FillRect> rects, List<Stroke> strokes)
    {
        foreach (var (offset, thickness, color) in Painted(line))
        {
            if (Dash(line) is { } dash)
                strokes.Add(new Stroke(left, y + offset + thickness / 2, left + width, y + offset + thickness / 2, thickness, color, dash));
            else
                rects.Add(new FillRect(left, y + offset, width, thickness, color));
        }
    }

    /// <summary>
    /// The line where two cells meet (measured with docx/local/nilborder-* and rowborder-k, -l): each side brings its
    /// cell's own border, else the table's, and nothing where it is nil or none, so the other side's line still shows
    /// (a row of nil sides below a bottom line keeps it, and its room); two lines are resolved by weight, style and
    /// color (MS-OI29500 17.4.66), a table's thick inside line over a cell's own thin one too. At the table's edge,
    /// where one cell meets none, its own border wins over the table's.
    /// </summary>
    static BorderLine? Edge(BorderLine? a, BorderLine? b, BorderLine? table, bool inside, BorderLine? tableOfA = null)
    {
        if (!inside)
            return a != null || b != null ? BorderLine.Resolve(a, b) : table;
        static BorderLine? Side(BorderLine? own, BorderLine? table) => (own ?? table) is { Visible: true } line ? line : null;
        return BorderLine.Resolve(Side(a, tableOfA ?? table), Side(b, table));
    }

    /// <summary>A table border of a row: the row's exceptions over the table's.</summary>
    static BorderLine? TableBorder(RowModel row, TableFormat format, Func<BorderSet, BorderLine?> side) =>
        (row.BorderExceptions != null ? side(row.BorderExceptions) : null) ?? side(format.Borders);

    /// <summary>A cell's margins: its own over the row's exceptions over the table's.</summary>
    static (double Top, double Left, double Bottom, double Right) Margins(RowModel row, CellModel cell, TableFormat format)
    {
        var m = format.Margins.Overlay(row.MarginExceptions).Overlay(cell.Format.Margins);
        var side = format.Styled ? 0 : DefaultMargin;
        return (m.Top ?? 0, m.Left ?? side, m.Bottom ?? 0, m.Right ?? side);
    }

    /// <param name="allowance">What the outer vertical lines take of the available width.</param>
    /// <param name="gaps">What cell spacing takes of a table's own width (twice the spacing beside every cell).</param>
    /// <param name="widthBase">What a percentage width is of, where not the available width.</param>
    /// <param name="lines">The vertical lines of the rows (row, grid line), whose halves count where they are wider
    /// than the cells' margins beside them; none for tables with cell spacing.</param>
    double[] Grid(TableModel table, List<RowModel> rows, int columns, double available, double allowance, PageFields? fields,
        double gaps = 0, double? widthBase = null, BorderLine?[,]? lines = null, double reach = 0)
    {
        var grid = new double[columns];
        for (var k = 0; k < columns; k++)
            grid[k] = k < table.Grid.Count ? table.Grid[k] : 0;
        // An autofit table whose grid lists fewer columns than its rows use lays each row out by its cells' widths from
        // the left, a grid line where the first row ending a cell there puts it, the last where the widest row ends,
        // the other rows' last cells stretched to it (Word's PDF of docx/external/libreoffice/extras-ooxmlexport/
        // tdf59274: a grid of 4 columns for spans of 11, rows of 7348, 3016 and 9048 twips all 9048 wide, their inner
        // lines where their cells end); one without a grid lays its columns out by their content (LibreOffice's
        // writerfilter-dmapper/floattable-then-table).
        if (table.Format.Layout != "fixed" && columns > 0 && rows.Count > 0 && table.Grid.Count is > 0 and var listed && listed < columns
            && Edges(table, columns, available) is { } edges)
            return Scaled(edges);
        if (table.Format.Layout != "fixed" && columns > 0 && rows.Count > 0)
            return Autofit(table, rows, columns, available + reach, allowance, fields, Target(table, rows, widthBase ?? available, allowance, gaps, lines), lines);
        if (grid.All(w => w > 0))
        {
            // Grid columns that no row reaches take nothing of a width of the table's own (measured with docx/local/
            // tug-1 to tug-4: rows of one cell in a grid of three fill 9072 twips, rows of two share them, a row of
            // all three keeps the grid; docx/external/libreoffice/extras-mailmerge/grabbagtest).
            var reached = rows.Count == 0 ? columns
                : rows.Max(r => (r.Cells.Count == 0 ? r.Format.GridBefore : r.Cells.Max(c => c.Column + c.Format.GridSpan)) + r.Format.GridAfter);
            if (reached > 0 && reached < columns && Target(table, rows, widthBase ?? available, allowance, gaps, lines) != null)
            {
                for (var k = reached; k < columns; k++)
                    grid[k] = 0;
            }
            return Scaled(grid);
        }
        // A grid that lists fewer columns than the rows use is not the table's: Word builds it from the cells' widths,
        // those of one column first, then what the spanning cells leave to theirs (Word's PDF of docx/external/
        // libreoffice/extras-ooxmlimport/n780645: grid columns of 2029 and 7469 twips for a row of two cells, the second
        // spanning four, and a row of five cells of 2029, 3589, 1530, 989 and 1361, which make the grid).
        if (table.Grid.Count < columns)
        {
            var built = new double[columns];
            foreach (var single in new[] { true, false })
            {
                foreach (var cell in table.Rows.SelectMany(r => r.Cells))
                {
                    if (single != cell.Format.GridSpan <= 1 || cell.Format.Width?.Resolve(available) is not { } w || w <= 0 || cell.Column >= columns)
                        continue;
                    var end = Math.Min(columns, cell.Column + cell.Format.GridSpan);
                    var open = Enumerable.Range(cell.Column, end - cell.Column).Where(k => built[k] <= 0).ToList();
                    var rest = w - Enumerable.Range(cell.Column, end - cell.Column).Sum(k => built[k]);
                    if (open.Count > 0 && rest > 0)
                        open.ForEach(k => built[k] = rest / open.Count);
                }
            }
            if (built.All(w => w > 0))
                return Scaled(built);
        }
        // From the first row's cell widths.
        foreach (var row in table.Rows)
        {
            foreach (var cell in row.Cells)
            {
                if (cell.Format.Width?.Resolve(available) is { } w && w > 0 && cell.Column < columns && grid[cell.Column] <= 0)
                {
                    for (var k = cell.Column; k < Math.Min(columns, cell.Column + cell.Format.GridSpan); k++)
                        grid[k] = w / cell.Format.GridSpan;
                }
            }
        }
        var missing = grid.Count(w => w <= 0);
        if (missing > 0)
        {
            var share = Math.Max(10, (available - grid.Where(w => w > 0).Sum()) / missing);
            for (var k = 0; k < columns; k++)
                if (grid[k] <= 0) grid[k] = share;
        }
        return Scaled(grid);

        // A fixed table with a width of its own stretches or shrinks its grid to it in proportion, whatever its cells
        // say (measured with docx/local/tblwidth-15 and -14: percentages and points, wider and narrower than the grid);
        // a nested one wider than its cell's text shrinks to it with its outer lines' halves (measured with
        // docx/local/np-5: a grid of 400 pt in a cell of 200 pt with margins of 5.4 pt from the text's left to its right
        // edge; docx/external/libreoffice/writerfilter-dmapper/floattable-nested-3tables, found by agent BE).
        double[] Scaled(double[] widths)
        {
            var sum = widths.Sum();
            if (Target(table, rows, widthBase ?? available, allowance, gaps, lines) is { } target && sum > 0)
                return widths.Select(w => w * target / sum).ToArray();
            if (inCell && sum > 0 && sum + allowance > available + 0.01 && available - allowance > 0)
                return widths.Select(w => w * (available - allowance) / sum).ToArray();
            return widths;
        }
    }

    /// <summary>
    /// Grid columns from the rows' cell widths laid end to end from the left (see Grid): each line where the first row
    /// ending a cell there puts it, the last where the widest row ends, lines no row ends a cell at shared out between
    /// their neighbours; null where a row has a cell without a width or columns before its cells.
    /// </summary>
    static double[]? Edges(TableModel table, int columns, double available)
    {
        var bounds = new double?[columns + 1];
        bounds[0] = 0;
        var end = 0.0;
        foreach (var row in table.Rows)
        {
            if (row.Format.GridBefore > 0)
                return null;
            var x = 0.0;
            foreach (var cell in row.Cells)
            {
                if (cell.Format.Width?.Resolve(available) is not { } w || w <= 0 || cell.Column + cell.Format.GridSpan > columns)
                    return null;
                x += w;
                var at = cell.Column + cell.Format.GridSpan;
                if (at < columns)
                    bounds[at] ??= x;
            }
            end = Math.Max(end, x);
        }
        bounds[columns] = end;
        var widths = new double[columns];
        var previous = 0.0;
        for (var k = 1; k <= columns; k++)
        {
            if (bounds[k] is not { } line)
            {
                var next = k + 1;
                while (bounds[next] == null)
                    next++;
                line = previous + (bounds[next]!.Value - previous) / (next - k + 1);
            }
            line = Math.Max(line, previous);
            bounds[k] = line;
            widths[k - 1] = line - previous;
            previous = line;
        }
        return widths;
    }

    /// <summary>
    /// The width a table asks for (w:tblW in points or percent of the available width) for its grid, or null. A width
    /// in points lies between the outer lines' middles, also with cell spacing (whose gaps it holds); a percentage
    /// between their outer edges, before Word 2013 (compatibility mode below 15) between the middles, of the available
    /// width and the first cell's and the last cell's margins together (measured with docx/local/tblwidth-15, -14,
    /// tblwidth2-15, -14 and compat14-tables).
    /// </summary>
    double? Target(TableModel table, List<RowModel> rows, double available, double allowance, double gaps, BorderLine?[,]? lines = null)
    {
        var format = table.Format;
        if (format.Width is not { Type: "dxa" or "pct" } w || w.Resolve(available) is not { } tw || tw <= 0)
            return null;
        var target = tw - gaps;
        if (w.Type != "pct")
            return target;
        // From Word 2013 on the percentage is of the available width less the outer lines' share, which then come on top
        // (measured with docx/local/tpw-15: 40 % and 50 % of 451.3 pt with outer lines of 0.5, 3 and 6 pt are 180.87,
        // 182.31, 184.11, 225.87, 227.07 and 228.63 pt between their outer edges; LibreOffice's core-unocore/
        // floattable-split, core-frmedt/floating-table-caption: 40 % with 3 pt lines).
        if (!Legacy)
            return (w.Resolve(available - allowance) ?? tw) - gaps;
        // Before Word 2013 the percentage is of the available width and those margins together (measured with
        // docx/local/tblwidth-14: 50 % of 451.3 pt with margins of 5.4 pt is 231.0 pt wide, not 236.4; pandoc/
        // golden/raw-blocks: 40 %), or half the outer line where that is wider (docx/local/tpw-14: 40 % and 50 % with
        // margins of 0.5 pt and outer lines of 0.5, 3 and 6 pt are 180.99, 181.71, 182.91, 226.23, 227.19 and 228.63 pt
        // between the lines' middles; docx/external/libreoffice/extras-ooxmlexport/tdf168988_grabbagDatePicker).
        if (rows.Count > 0 && rows[0].Cells.Count > 0)
        {
            var (first, last) = (rows[0].Cells[0], rows[0].Cells[^1]);
            var (leftLine, rightLine) = lines is { Length: > 0 } ? (Width(lines[0, 0]) / 2, Width(lines[0, lines.GetLength(1) - 1]) / 2) : (0, 0);
            var margins = Math.Max(Margins(rows[0], first, format).Left, leftLine) + Math.Max(Margins(rows[0], last, format).Right, rightLine);
            target = (w.Resolve(available + margins) ?? tw + margins) - gaps;
        }
        return target;
    }

    /// <summary>
    /// Word's autofit column widths (catalog: table.width, measured with docx/local/tbl-autofit in Courier New, all ten
    /// cases within 0.15 pt): every column has a minimum (its widest unbreakable content) and a maximum (its content
    /// unwrapped), each with the cells' margins; a preferred cell width fixes its column. Where the maxima fit, the
    /// columns take them (stretched in proportion to them to a table width that asks for more); where only the minima
    /// fit, every column gets its minimum and a share of the rest in proportion to its maximum less its minimum; where
    /// not even those fit, the minima shrink in proportion, the margins stay; where every column has a preferred width
    /// of its own, those give way instead: columns whose contents need more than they prefer keep their contents'
    /// minimum, the others share the rest in proportion to their preferred widths, none below its contents' minimum
    /// (measured with docx/local/tblpic, table B: beside a 250 pt picture two cells preferring 150 pt take 100 pt
    /// each; tblpic2 V1), and where the contents' minima do not fit either, those shrink (docx/local/tblpic3, table G:
    /// pictures 200, 200 and 100 pt wide in cells preferring 150 pt take 180, 180 and 91 pt). A table narrower than
    /// its contents' minima grows to them, up to the width available (docx/local/tblover-14, -15: pictures of 200 and
    /// 250 pt make a table preferring 400 pt 450 pt wide, of 300 and 250 pt 468 pt, the width between the margins); one
    /// that prefers more than is available keeps its contents' minima, however wide it becomes (700 pt of pictures in
    /// a table preferring 500 pt; docx/external/poi/bug59058). Preferred cell widths give way to the table's width.
    /// </summary>
    double[] Autofit(TableModel table, List<RowModel> rows, int columns, double available, double allowance, PageFields? fields, double? target,
        BorderLine?[,]? lines = null)
    {
        var format = table.Format;
        var space = (target ?? available - allowance);
        var (min, max, margins, preferred, natural, spans) = Ranges(table, rows, columns, space, fields, lines);
        if (target is { } wanted && Needed(natural, spans) is var needed && needed > wanted + 0.01)
            space = wanted > available - allowance + 0.01 ? needed : Math.Min(needed, available - allowance);
        var sumMin = min.Sum();
        var sumMax = max.Sum();
        var widths = new double[columns];
        if (sumMax <= space + 0.01)
        {
            if (target == null)
                return max;
            // Columns with nothing in them, not even margins, take the table's width by its grid, else in equal
            // shares (measured with docx/local/twb-*: one empty cell of a table preferring 800 pt in the body, 400 pt
            // in text boxes 200 pt wide, as wide as that and centered; libreoffice/extras-ooxmlexport/n780563).
            if (sumMax <= 0)
            {
                var grid = Enumerable.Range(0, columns).Select(k => k < table.Grid.Count ? Math.Max(0, table.Grid[k]) : 0).ToArray();
                for (var k = 0; k < columns; k++)
                    widths[k] = grid.Sum() > 0 ? space * grid[k] / grid.Sum() : space / columns;
                return widths;
            }
            // Stretched to the table's width in proportion to the maxima of the columns without a preferred width.
            var flexible = Enumerable.Range(0, columns).Where(k => preferred[k] == null).ToList();
            if (flexible.Count == 0)
                flexible = Enumerable.Range(0, columns).ToList();
            var fixedSum = Enumerable.Range(0, columns).Except(flexible).Sum(k => max[k]);
            var flexibleSum = flexible.Sum(k => max[k]);
            for (var k = 0; k < columns; k++)
                widths[k] = flexible.Contains(k) && flexibleSum > 0 ? max[k] * (space - fixedSum) / flexibleSum : max[k];
            return widths;
        }
        if (sumMin <= space + 0.01)
        {
            var range = sumMax - sumMin;
            for (var k = 0; k < columns; k++)
                widths[k] = min[k] + (range > 0 ? (space - sumMin) * (max[k] - min[k]) / range : 0);
            return widths;
        }
        // Preferred widths give way where every column has one of its own (as measured); else the minima shrink as
        // they are.
        var own = preferred.All(p => p != null);
        if (own && natural.Sum() <= space + 0.01)
        {
            var percentages = rows.All(r => r.Cells.All(c => c.MergedAbove || c.Format.Width is { Type: "pct" }));
            return Yielding(min, natural, preferred, space, percentages);
        }
        // Not even the contents' minima fit: they shrink in proportion, the margins stay.
        var shrinking = own ? natural : min;
        var content = Enumerable.Range(0, columns).Sum(k => Math.Max(0, shrinking[k] - margins[k]));
        var room = space - margins.Sum();
        for (var k = 0; k < columns; k++)
            widths[k] = margins[k] + (content > 0 ? Math.Max(0, shrinking[k] - margins[k]) * Math.Max(0, room) / content : 0);
        // Where cells over several columns preferred more than the table has, none becomes narrower than its contents
        // need: its last column takes what is missing from the columns of the other cells (docx/external/libreoffice/
        // extras-ooxmlexport/tdf123705: a form of 41 columns keeps "n/m" on one line in its last cell, 33.4 pt wide).
        if (!own)
        {
            var kept = new bool[columns];
            var raised = (double[])widths.Clone();
            var missing = 0.0;
            foreach (var (from, to, need) in spans)
            {
                var have = 0.0;
                for (var k = from; k < to; k++)
                    have += raised[k];
                if (need > have + 0.01)
                {
                    raised[to - 1] += need - have;
                    missing += need - have;
                    for (var k = from; k < to; k++)
                        kept[k] = true;
                }
            }
            // Only where the other columns can give it.
            var giving = Enumerable.Range(0, columns).Where(k => !kept[k]).Sum(k => Math.Max(0, raised[k] - margins[k]));
            if (missing > 0 && missing < giving)
            {
                for (var k = 0; k < columns; k++)
                    widths[k] = kept[k] ? raised[k] : raised[k] - Math.Max(0, raised[k] - margins[k]) * missing / giving;
            }
        }
        return widths;
    }

    /// <summary>How wide the columns' contents need the table at least: their own cells' minima, widened where a
    /// cell over several columns needs more, without preferred widths.</summary>
    static double Needed(double[] natural, List<(int From, int To, double Min)> spans)
    {
        var contents = (double[])natural.Clone();
        foreach (var (from, to, need) in spans.OrderBy(s => s.To - s.From))
        {
            var have = 0.0;
            for (var k = from; k < to; k++)
                have += contents[k];
            if (to > from && need > have)
                contents[to - 1] += need - have;
        }
        return contents.Sum();
    }

    /// <summary>
    /// Columns whose preferred widths do not fit where their contents would: those that need more than they prefer
    /// keep their contents' minimum, the others give up what is missing in proportion to how much they prefer over
    /// their contents' minimum (measured with docx/local/overpref-O1 to -O4 within 0.4 pt: three cells preferring
    /// 221 pt on 451 pt take 143.6, 157.1 and 150.3 pt beside words of 12, 48 and 30 pt;
    /// docx/external/libreoffice/core-ooxml-pass/fdo78904_fileHang; docx/external/powertools/HW002-Table07). Where the
    /// cells prefer percentages that fit and only columns whose contents need more than they prefer make the table too
    /// wide, the others give up what is missing in equal shares, none below its contents' minimum (measured with
    /// docx/local/t65649-table: four columns of 18 to 38 % give up 1.6 pt each to one of 6.5 % whose word needs 6.4 pt
    /// more; docx/external/poi/bug65649).
    /// </summary>
    static double[] Yielding(double[] min, double[] natural, double?[] prefers, double space, bool percentages)
    {
        var columns = min.Length;
        var widths = new double[columns];
        if (percentages && prefers.Sum(p => p ?? 0) <= space + 0.01)
        {
            // Those that need more than they prefer take it; the others share what that takes from the table equally.
            var giving = new List<int>();
            var taken = 0.0;
            for (var k = 0; k < columns; k++)
            {
                widths[k] = Math.Max(prefers[k] ?? 0, natural[k]);
                if (natural[k] < (prefers[k] ?? 0) - 0.01)
                    giving.Add(k);
                taken += widths[k];
            }
            var excess = taken - space;
            while (excess > 0.01 && giving.Count > 0)
            {
                var share = excess / giving.Count;
                var limited = giving.Where(k => widths[k] - share < natural[k]).ToList();
                if (limited.Count == 0)
                {
                    foreach (var k in giving)
                        widths[k] -= share;
                    break;
                }
                foreach (var k in limited)
                {
                    excess -= widths[k] - natural[k];
                    widths[k] = natural[k];
                    giving.Remove(k);
                }
            }
            return widths;
        }
        // Percentages that add up to more than the table, those before the last fitting: they take theirs, the last
        // column what is left (measured with docx/local/pw-a, -b, -c: cells of 30 and 80 %, 80 and 30 %, 50, 40 and
        // 30 % of a table as wide as the column, autofit or fixed, in modes 14 and 15, within 0.1 pt; docx/external/
        // libreoffice/extras-ooxmlexport/fdo78469). Known deviation: where those before the last do not fit either
        // (pw-d: 30, 80 and 10 %) Word narrows them otherwise.
        if (percentages && columns > 1 && prefers.All(p => p != null) && prefers.Take(columns - 1).Sum(p => p!.Value) <= space + 0.01)
        {
            var used = 0.0;
            for (var k = 0; k < columns - 1; k++)
                used += widths[k] = Math.Max(prefers[k]!.Value, natural[k]);
            widths[^1] = Math.Max(natural[^1], space - used);
            return widths;
        }
        var rest = space;
        var preferred = 0.0;
        var slack = 0.0;
        for (var k = 0; k < columns; k++)
        {
            if (natural[k] >= min[k] - 0.01)
                rest -= natural[k];
            else
            {
                preferred += min[k];
                slack += min[k] - natural[k];
            }
        }
        var missing = Math.Max(0, preferred - rest);
        for (var k = 0; k < columns; k++)
            widths[k] = natural[k] >= min[k] - 0.01 ? natural[k] : min[k] - (slack > 0 ? missing * (min[k] - natural[k]) / slack : 0);
        return widths;
    }

    /// <summary>A width in points rounded up to whole twips.</summary>
    static double Twips(double points) => Math.Ceiling(points * 20 - 1e-6) / 20;

    /// <summary>
    /// The columns' minima and maxima, the cells' margins in them, their preferred widths and the minima of their own
    /// cells' contents alone (Natural, before preferred widths fix them and cells across several columns widen them:
    /// spread evenly, a merged cell's nested table would make a narrow column keep more than Word gives it, docx/
    /// external/libreoffice/extras-ooxmlexport/tdf158349_SDTstreamStateStack).
    /// </summary>
    (double[] Min, double[] Max, double[] Margins, double?[] Preferred, double[] Natural, List<(int From, int To, double Min)> Spans) Ranges(TableModel table, List<RowModel> rows, int columns, double space, PageFields? fields,
        BorderLine?[,]? lines = null)
    {
        var format = table.Format;
        var min = new double[columns];
        var max = new double[columns];
        var margins = new double[columns];
        var preferred = new double?[columns];
        var spanning = new List<(int From, int To, double Min, double Max, double? Preferred)>();
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            // Widths before and after a row (w:wBefore, w:wAfter) do not widen autofit columns (measured with
            // docx/local/tbl-features).
            foreach (var cell in row.Cells)
            {
                var c0 = Math.Min(cell.Column, columns - 1);
                var c1 = Math.Min(columns, cell.Column + cell.Format.GridSpan);
                if (cell.MergedAbove)
                    continue;
                // Margins narrower than half the line beside them give way to it, as in the cell's layout (measured
                // with docx/local/cellborder: margins 0 or 1 pt beside 3 pt lines leave 1.5 pt, 5.4 pt stay).
                var m = Margins(row, cell, format);
                if (lines != null && r < lines.GetLength(0))
                    m = (m.Top, Math.Max(m.Left, Width(lines[r, c0]) / 2), m.Bottom, Math.Max(m.Right, Width(lines[r, c1]) / 2));
                var (cmin, cmax) = Rotated(cell) ? RotatedWidths(cell, fields) : Directed(cell, () => CellWidths(cell.Blocks, fields));
                // A cell that may not wrap (w:noWrap) needs its whole width, unless it prefers a width in points
                // (measured with docx/local/nowrap: such a cell wraps as if it could, one preferring a percentage not).
                if (cell.Format.NoWrap && cell.Format.Width is not { Type: "dxa", Value: > 0 })
                    cmin = cmax;
                // Word measures content at most 22 inches wide, its longest line (measured with docx/local/tbl-features:
                // a column beside one with a very long paragraph gets 52.8 pt, 51.2 pt without the limit), rounded up
                // to whole twips (docx/external/pandoc/golden/tables_separated_with_rawblock: Aptos 12's "a", 127.5
                // twips, makes a column of 344 twips with its margins, the next cell's text at 1761 twips, dot 734).
                cmax = Twips(Math.Min(cmax, 1584));
                cmin = Twips(cmin);
                cmin += m.Left + m.Right;
                cmax += m.Left + m.Right;
                var pref = cell.Format.Width?.Resolve(space) is { } p && p > 0 ? p : (double?)null;
                if (c1 - c0 <= 1)
                {
                    min[c0] = Math.Max(min[c0], cmin);
                    max[c0] = Math.Max(max[c0], cmax);
                    margins[c0] = Math.Max(margins[c0], m.Left + m.Right);
                    if (pref != null)
                        preferred[c0] = Math.Max(preferred[c0] ?? 0, pref.Value);
                }
                else
                    spanning.Add((c0, c1, cmin, cmax, pref));
            }
        }
        // Preferred widths fix their columns first; cells over several columns then widen them where they are too
        // narrow for them together (measured with docx/local/hmerge-g: a 200 pt cell over two 100 pt columns leaves
        // them as they are), the narrower spans first: what is missing goes to the columns nothing has given a width
        // yet (in proportion to the grid's widths), else to those without a preferred width of their own in proportion
        // to their widths, else to the last column (measured with docx/local/so-1 to so-10 and gw-1 to gw-10: a 300 pt
        // cell over a 15 pt column and two of 250 pt together makes them 17.2 and 282.8 pt, wherever it stands; over
        // columns preferring 100 and 50 pt 100 and 200 pt; cells over three and two columns of 125 pt above cells over
        // two and three of 75 and 175 pt leave the middle column 50 pt; a cell over all columns of a table of
        // percentages leaves them theirs; docx/external/libreoffice/extras-ooxmlexport/wDateValueFormat).
        var natural = (double[])min.Clone();
        for (var k = 0; k < columns; k++)
        {
            if (preferred[k] is { } p)
                min[k] = max[k] = Math.Max(p, min[k]);
        }
        foreach (var (from, to, cmin, cmax, pref) in spanning.OrderBy(s => s.To - s.From))
        {
            if (to <= from)
                continue;
            var takers = Enumerable.Range(from, to - from).Where(k => max[k] <= 0).ToList();
            if (takers.Count == 0)
                takers = Enumerable.Range(from, to - from).Where(k => preferred[k] == null).ToList();
            if (takers.Count == 0)
                takers.Add(to - 1);
            Widen(min, from, to, takers, Math.Max(cmin, pref ?? 0), table.Grid);
            Widen(max, from, to, takers, Math.Max(cmax, pref ?? 0), table.Grid);
            for (var k = from; k < to; k++)
                max[k] = Math.Max(max[k], min[k]);
        }
        for (var k = 0; k < columns; k++)
            max[k] = Math.Max(max[k], min[k]);
        return (min, max, margins, preferred, natural, spanning.Select(s => (s.From, s.To, s.Min)).ToList());

        static void Widen(double[] widths, int from, int to, List<int> takers, double want, List<double> grid)
        {
            var have = 0.0;
            for (var k = from; k < to; k++)
                have += widths[k];
            if (want <= have)
                return;
            double Share(int k) => k < grid.Count ? Math.Max(0, grid[k]) : 0;
            var basis = takers.Sum(k => widths[k]);
            var gridBasis = takers.Sum(Share);
            foreach (var k in takers)
                widths[k] += (want - have) * (basis > 0 ? widths[k] / basis : gridBasis > 0 ? Share(k) / gridBasis : 1.0 / takers.Count);
        }
    }

    /// <summary>The narrowest and widest a cell's content can be: of its paragraphs and nested tables.</summary>
    (double Min, double Max) CellWidths(List<Block> blocks, PageFields? fields)
    {
        double min = 0, max = 0;
        foreach (var block in blocks)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                {
                    // Tracked deleted text counts, shown or not (measured in Word's PDF of LibreOffice's extras-ooxmlexport/
                    // testTrackChangesDeletedTableCell: the columns where its deleted "ABCD" puts them in the markup view).
                    var (pmin, pmax) = paragraphs.Widths(paragraph, fields, pictures: true, empty: false, deleted: true);
                    min = Math.Max(min, pmin);
                    max = Math.Max(max, pmax);
                    // Floating drawings that text goes around count with their width and effect extents, not their
                    // distances (measured with docx/local/agr-anchor, -anchor14: shapes of 72 and 43.2 pt wrapped square
                    // alone in two cells share a table in proportion to them and the margins; docx/external/libreoffice/
                    // core-exportdata-ooxml-pass/textframe-in-shape).
                    foreach (var anchor in paragraph.Content.OfType<AnchorInline>())
                    {
                        var box = new DrawingBox(anchor.Anchor);
                        if (!box.Wraps || (string?)anchor.Anchor.Attribute("layoutInCell") is "0" or "false")
                            continue;
                        var wide = box.Extent.Width + box.Effect.Left + box.Effect.Right;
                        min = Math.Max(min, wide);
                        max = Math.Max(max, wide);
                    }
                    break;
                }
                case TableBlock nested:
                {
                    // A nested table needs its content and the width it prefers itself, not what its cells prefer or
                    // its grid fixes: wider ones it gives up in a cell narrower (measured with docx/local/np-1 to np-7: a
                    // nested cell of 400 pt, of three of 150 pt or of a fixed table in an outer cell preferring 200 pt
                    // leaves the outer columns theirs, a nested table preferring 400 pt widens it; docx/external/
                    // libreoffice/core-layout/floattable-nested-rowspan, tika/testWORD).
                    var rows = nested.Table.Rows.Where(r => !r.Format.Hidden).ToList();
                    var columns = Math.Max(nested.Table.Grid.Count, rows.Count == 0 ? 0 : rows.Max(r => r.Cells.Count == 0 ? 0 : r.Cells.Max(c => c.Column + c.Format.GridSpan)));
                    if (columns > 0)
                    {
                        var (_, nmax, _, _, natural, _) = Ranges(nested.Table, rows, columns, 100000, fields);
                        var own = nested.Table.Format.Width is { Type: "dxa", Value: > 0 } preferred ? preferred.Value : 0;
                        // Its outer lines take their halves beside the columns, as where it is laid out (docx/external/
                        // libreoffice/filter-ascii/tdf162180: nested columns of 24.23, 43.23 and 54.58 pt between 0.5 pt
                        // lines keep "Approved" and "Central" whole).
                        var lineRoom = (Width(nested.Table.Format.Borders.Left) + Width(nested.Table.Format.Borders.Right)) / 2;
                        min = Math.Max(min, Math.Max(natural.Sum() + lineRoom, own));
                        max = Math.Max(max, nested.Table.Format.Layout == "fixed" && nested.Table.Grid.Count > 0 ? nested.Table.Grid.Sum() : nmax.Sum());
                    }
                    break;
                }
            }
        }
        return (min, max);
    }

    /// <summary>The shading of the last paragraph of the blocks laid out last (<see cref="Blocks"/>), one of their fills, or null.</summary>
    public FillRect? LastShading { get; private set; }

    /// <summary>
    /// Blocks laid out in a width as in a table cell or a text box: lines, fills and strokes relative to their top
    /// left, and their height; <paramref name="unaligned"/> leaves the lines where they begin, for measuring them.
    /// </summary>
    /// <param name="direction">Vertical lines and upright East Asian characters, as a text box of such text has them.</param>
    public (List<PlacedLine> Lines, List<FillRect> Fills, List<Stroke> Strokes, double Height) Blocks(List<Block> blocks, double width, PageFields? fields, bool unaligned = false,
        (bool Vertical, bool Turn)? direction = null)
    {
        var (wasCell, wasInCell, wasUnaligned, wasVertical, wasTurn) = (cellBlocks, inCell, paragraphs.Unaligned, paragraphs.Vertical, paragraphs.TurnEastAsian);
        (cellBlocks, inCell) = (false, false);
        paragraphs.Unaligned |= unaligned;
        if (direction is { } turned)
            (paragraphs.Vertical, paragraphs.TurnEastAsian) = (turned.Vertical, turned.Turn);
        try
        {
            var content = Content(blocks, width, fields);
            LastShading = content.LastShading;
            return (content.Lines, content.Fills, content.Strokes, content.Height);
        }
        finally
        {
            (cellBlocks, inCell, paragraphs.Unaligned, paragraphs.Vertical, paragraphs.TurnEastAsian) = (wasCell, wasInCell, wasUnaligned, wasVertical, wasTurn);
        }
    }

    /// <summary>A cell's content laid out: lines, fills and strokes relative to its text's top left, its height, its place.</summary>
    sealed record CellContent(List<PlacedLine> Lines, List<FillRect> Fills, List<Stroke> Strokes, double Height, double Left = 0, double Width = 0, double Thickness = 0)
    {
        /// <summary>The floating drawings of the content, relative to its top left.</summary>
        public List<CellAnchor> Anchors { get; init; } = [];

        /// <summary>How far down the content and all its floating drawings reach, which vertical alignment moves together.</summary>
        public double? Reach { get; init; }

        /// <summary>
        /// The lines, fills and strokes of the content's floating tables, which the cell's vertical alignment leaves at
        /// their place (measured with docx/local/nfv-*: centered and bottom aligned text lowered by the room below the
        /// lowest of text and floating table, the floating tables themselves at the cell's top or their offset below it).
        /// </summary>
        public IReadOnlySet<object>? Floated { get; init; }

        /// <summary>How far the cell's vertical alignment moves one of the content's lines, fills or strokes.</summary>
        public double Lowering(object item, double lowered) => Floated?.Contains(item) == true ? 0 : lowered;

        /// <summary>The shading of the content's last paragraph, where it is the last block and shaded (one of the fills).</summary>
        public FillRect? LastShading { get; init; }

        /// <summary>Where the parts after the cuts through the content's nested tables begin (see Content's cuts).</summary>
        public List<CellBoundary> Boundaries { get; init; } = [];
    }

    /// <summary>
    /// Where the part after a cut through a cell's nested table begins: its first line, fill, stroke and drawing in the
    /// content's lists and how far down the content it begins.
    /// </summary>
    readonly record struct CellBoundary(int Lines, int Fills, int Strokes, int Anchors, double Origin);

    /// <summary>
    /// How far down a floating drawing in a cell reaches from the content's top, placed from its paragraph, its line or
    /// the cell's top (margins and the page stand for the cell there; see CellAnchor), its box with its effect extent and
    /// distance below (docx/external/powertools/WC/WC049-Text-Box-in-Cell: a row ending 0.9 and 3.6 pt below its text
    /// box), and whether text goes around it.
    /// </summary>
    (double Bottom, bool Wraps)? Reach(AnchorInline anchor, double paragraphTop, double lineTop, PageFields? fields)
    {
        var element = anchor.Anchor;
        if (CellTop(element, paragraphTop, lineTop) is not { } top)
            return null;
        var (_, height) = CellDrawingSize(anchor, fields);
        var box = new DrawingBox(element);
        var wraps = box.Wraps;
        return (top + height + DrawingBox.Outward(box.Effect).Bottom + (wraps ? DrawingBox.Outward(box.Distance).Bottom : 0), wraps);
    }

    /// <summary>
    /// Where a floating drawing in a cell stands down from the content's top: an offset from its paragraph, its line or
    /// the cell's top; aligned or placed by a percentage from the page or the margins one that text goes around stands
    /// at the content's top, and one placed above it moves down to it (see Paginator.Anchored; measured with
    /// docx/local/cdr-pager, ccl-up); null where it is placed otherwise.
    /// </summary>
    static double? CellTop(System.Xml.Linq.XElement element, double paragraphTop, double lineTop)
    {
        var vertical = element.Element(Docx.Drawings.Wp + "positionV");
        var from = (string?)vertical?.Attribute("relativeFrom");
        var box = new DrawingBox(element);
        var wraps = box.Wraps;
        var least = DrawingBox.Outward(box.Effect).Top;
        if (vertical?.Element(Docx.Drawings.Wp + "posOffset") is { } offset)
        {
            if (Number(offset.Value) is not { } dy)
                return null;
            var top = from switch { "paragraph" => paragraphTop, "line" => lineTop, _ => 0 } + dy;
            return wraps ? Math.Max(top, least) : top;
        }
        if (from is not ("paragraph" or "line" or "character") && vertical != null && wraps)
            return least;
        return null;
    }

    static double? Number(string? value) =>
        double.TryParse(value?.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v / 12700 : null;

    /// <summary>
    /// The section's text area and paper, of which drawings sized relative to the page or its margins take their share
    /// (set by the paginator for the section the tables stand in).
    /// </summary>
    public (double TextWidth, double TextHeight, double PageWidth, double PageHeight) PageArea { get; set; } = (451.3, 697.9, 595.3, 841.9);

    /// <summary>
    /// A floating drawing's size in a cell as Word draws it: a share of the page or its margins less the effect extents
    /// across (see Paginator.Anchored), a text box that resizes to its text as high as it and its insets (measured with
    /// docx/local/cdr-pct, cdr-auto: 40 % of a 451.3 pt margin with 1.8 pt of effect extent right 178.7 pt wide, one
    /// line of text 19.4 pt high instead of the saved 110.6 pt; docx/external/powertools/WC/WC048-Text-Box-in-Cell).
    /// </summary>
    (double Width, double Height) CellDrawingSize(AnchorInline anchor, PageFields? fields)
    {
        var element = anchor.Anchor;
        var box = new DrawingBox(element);
        var (w, h) = box.Extent;
        var data = element.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData");
        var effect = DrawingBox.Outward(box.Effect);
        double? Relative(string name, string percent, bool horizontal)
        {
            if (element.Element(Docx.Drawings.Wp14 + name) is not { } relative
                || !double.TryParse(relative.Element(Docx.Drawings.Wp14 + percent)?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var share)
                || share <= 0)
                return null;
            var frame = ((string?)relative.Attribute("relativeFrom"), horizontal) switch
            {
                ("page", true) => PageArea.PageWidth,
                ("page", false) => PageArea.PageHeight,
                (_, true) => PageArea.TextWidth,
                (_, false) => PageArea.TextHeight,
            };
            return Math.Max(0, frame * share / 100000 - (horizontal ? effect.Left + effect.Right : effect.Top + effect.Bottom));
        }
        if (data?.Element(Docx.Drawings.Pic + "pic") == null && data?.Elements().FirstOrDefault()?.Name.LocalName is not ("wgp" or "wpc"))
            (w, h) = (Relative("sizeRelH", "pctWidth", true) ?? w, Relative("sizeRelV", "pctHeight", false) ?? h);
        var shape = data?.Element(Docx.Drawings.Wps + "wsp");
        if (shape?.Element(Docx.Drawings.Wps + "bodyPr") is { } body && body.Element(Ns.A + "spAutoFit") != null && anchor.Text is { Count: > 0 } text
            && (string?)body.Attribute("vert") is null or "horz")
        {
            var (l, t, r, b) = PageRenderer.Insets(body, shape, w, h, context.Theme);
            var (_, _, _, height) = Blocks(text, Math.Max(1, w - l - r), fields);
            h = height + t + b;
        }
        return (w, h);
    }

    /// <summary>
    /// Whether a floating drawing in a cell is laid out in it: from Word 2013 on always, before where layoutInCell says
    /// so (measured with docx/local/cig-noin, cig-noin14, cig-noin12: a shape wrapped square of layoutInCell 0 grows
    /// its row in mode 15; in modes 14 and 12 it stands in the column, the table below it).
    /// </summary>
    bool InCell(System.Xml.Linq.XElement anchor) =>
        !context.Profile.HonorsLayoutInCell || (string?)anchor.Attribute("layoutInCell") is not ("0" or "false");

    /// <summary>
    /// The inside of the cell being laid out, between the inner edges of its side lines, relative to its text's left:
    /// drawings that text goes around stay within it across (see <see cref="CellExclusion"/>).
    /// </summary>
    (double Left, double Right)? cellInside;

    /// <summary>
    /// The area a floating drawing in a cell keeps text from, relative to the content's top left: its box with its
    /// effect extents and distances, placed down as in <see cref="Reach"/> and across by an offset from the text's left
    /// (the page's from the cell's edge), aligned in the text with its effect extents (measured with docx/local/cdr-pct:
    /// a box centered in the column 9.25 pt in from the text's left with 1.8 pt of effect extent right), or at the cell's
    /// edge where it is aligned or placed by a percentage from the page or the margins (cdr-pagec, cdr-pager); null for
    /// drawings text does not go around or placed otherwise.
    /// </summary>
    Paginator.Exclusion? CellExclusion(AnchorInline anchor, double paragraphTop, double width, double edge, PageFields? fields)
    {
        var element = anchor.Anchor;
        var box = new DrawingBox(element);
        if (!box.Wraps)
            return null;
        var (effect, distance) = (DrawingBox.Outward(box.Effect), DrawingBox.Outward(box.Distance));
        var vertical = element.Element(Docx.Drawings.Wp + "positionV");
        var horizontal = element.Element(Docx.Drawings.Wp + "positionH");
        if ((string?)vertical?.Attribute("relativeFrom") is "line" or "character" || CellTop(element, paragraphTop, paragraphTop) is not { } top)
            return null;
        var (w, h) = CellDrawingSize(anchor, fields);
        var from = (string?)horizontal?.Attribute("relativeFrom");
        var (before, after) = (effect.Left, effect.Right);
        double x;
        if (Number(horizontal?.Element(Docx.Drawings.Wp + "posOffset")?.Value) is { } dx && from != "character")
            x = (from == "page" ? -edge : 0) + dx;
        else if (from is "page" or "leftMargin" or "rightMargin" or "insideMargin" or "outsideMargin" && horizontal != null)
            x = -edge + before;
        else if (horizontal?.Element(Docx.Drawings.Wp + "align")?.Value is { } align && from is "column" or "margin")
            x = align switch { "right" => width - w - after, "center" => before + (width - before - w - after) / 2, _ => before };
        else
            return null;
        if (cellInside is { } inside)
            x = Math.Max(inside.Left, Math.Min(x, inside.Right - w));
        return new Paginator.Exclusion(x - before - distance.Left, top - effect.Top - distance.Top,
            x + w + after + distance.Right, top + h + effect.Bottom + distance.Bottom, box.TopAndBottom, box.Side);
    }

    /// <summary>A cell's blocks laid out in its text width: lines with tops from the content's top, and the height.</summary>
    /// <remarks>
    /// Rotated text (w:textDirection tbRl, btLr) is laid out unwrapped along the cell; its height is its longest
    /// line, its lines are turned when the row is built.
    /// </remarks>
    /// <param name="edge">How far the cell's text begins right of its left edge.</param>
    /// <param name="cuts">Cuts through the cell's nested tables to lay it out for (see CellPart.Cuts).</param>
    CellContent Content(CellModel cell, double width, PageFields? fields, (string? Before, string? After) neighbors, double edge = 0,
        RgbColor? shading = null, IReadOnlyList<CellCut>? cuts = null)
    {
        var was = cellBlocks;
        var behind = paragraphs.Background;
        cellBlocks = true;
        // The cell's shading is behind its text, else what is behind the table (catalog: run.basic, automatic color).
        paragraphs.Background = shading ?? behind;
        try
        {
            return CellBlocks(cell, width, fields, neighbors, edge, cuts);
        }
        finally
        {
            cellBlocks = was;
            paragraphs.Background = behind;
        }
    }

    CellContent CellBlocks(CellModel cell, double width, PageFields? fields, (string? Before, string? After) neighbors, double edge, IReadOnlyList<CellCut>? cuts)
    {
        // An empty cell whose end mark is hidden (w:hideMark) takes no height for it, its last paragraph; its other
        // empty paragraphs keep their lines (docx/external/libreoffice/extras-ooxmlexport/tdf123104: four empty 9 pt
        // paragraphs three lines high, five centered as four).
        if (cell.Format.HideMark && cell.Blocks.All(b => b is ParagraphBlock { Content.Count: 0 }))
        {
            if (cell.Blocks.Count <= 1)
                return new CellContent([], [], [], 0);
            var kept = cell.Blocks.Take(cell.Blocks.Count - 1).ToList();
            return Directed(cell, () => Content(kept, width, fields, neighbors.Before, neighbors.After, edge, floats: true, cuts: cuts));
        }
        if (!Rotated(cell))
        {
            var wasInCell = paragraphs.InCell;
            paragraphs.InCell = true;
            try
            {
                var content = Directed(cell, () => Content(cell.Blocks, width, fields, neighbors.Before, neighbors.After, edge, floats: true, cuts: cuts));
                // A hidden end mark after a line break takes no line of its own either (docx/external/libreoffice/
                // extras-ooxmlexport/fdo76316: cells ending in w:br, their 13.8 pt mark lines gone).
                if (cell.Format.HideMark && cell.Blocks.LastOrDefault() is ParagraphBlock { Content: [.., BreakInline { Type: "textWrapping" or null }] }
                    && content.Lines.Count > 1 && content.Lines[^1].Line.Pieces.All(p => p.Text.Length == 0 && p.Drawing == null))
                {
                    var kept = content.Lines[^2];
                    content = content with { Lines = content.Lines.Take(content.Lines.Count - 1).ToList(), Height = kept.Top + kept.Line.Height };
                }
                return content;
            }
            finally
            {
                paragraphs.InCell = wasInCell;
            }
        }
        // Measured unaligned: a centered line's length is its text's, not its place in the unwrapped width (measured
        // with docx/external/powertools/HC008-Test-03: "notes" centered in a btLr cell of a row 72 pt high keeps the
        // row 72 pt high, not 810).
        var unaligned = paragraphs.Unaligned;
        paragraphs.Unaligned = true;
        try
        {
            var content = Directed(cell, () => Content(cell.Blocks, Unwrapped, fields));
            var length = content.Lines.Count == 0 ? 0 : content.Lines.Max(l => LineLength(l.Line));
            return content with { Height = length, Thickness = content.Height };
        }
        finally
        {
            paragraphs.Unaligned = unaligned;
        }
    }

    /// <summary>
    /// A rotated cell's lines broken and aligned along the length its row gives them: its height from the row's other
    /// cells or its own height, which the rotated text does not change, its words broken where they are longer (measured
    /// with docx/local/rcw-*: "this is rotated by 270 degrees" in btLr and tbRl cells of rows at least or exactly
    /// 56.7 pt high in four lines, in rows of 20 pt at least or of one line beside a plain cell in pieces like "rota"
    /// and "ted"; docx/external/libreoffice/extras-ooxmlexport/cell-btlr, tblr-height).
    /// </summary>
    List<PlacedLine> RotatedLines(CellModel cell, CellContent content, double length, PageFields? fields) =>
        Directed(cell, () => Content(cell.Blocks, Math.Max(1, length), fields)).Lines;

    /// <summary>
    /// Whether the rotated cells of a row take its height as given, by its own height or its other cells, not the
    /// length of their text (which they take where nothing else gives the row a height).
    /// </summary>
    static bool TurnsInRow(RowModel row) => row.Format.Height is > 0 || row.Cells.Any(c => !Rotated(c));

    /// <summary>
    /// Laid out with the cell's text direction (measured with docx/local/vertical2 and vertical5): its lines vertical
    /// where it turns them (tbRl, btLr, tbRlV, tbLrV), its East Asian characters turned against them in tbRlV, tbLrV
    /// (upright) and lrTbV (lying); a cell without a direction of its own keeps the one it stands in.
    /// </summary>
    T Directed<T>(CellModel cell, Func<T> laid)
    {
        var direction = cell.Format.TextDirection;
        if (direction is null or "lrTb")
            return laid();
        var (vertical, turn) = (paragraphs.Vertical, paragraphs.TurnEastAsian);
        (paragraphs.Vertical, paragraphs.TurnEastAsian) = (Rotated(cell), direction is "tbRlV" or "tbLrV" or "lrTbV");
        try
        {
            return laid();
        }
        finally
        {
            (paragraphs.Vertical, paragraphs.TurnEastAsian) = (vertical, turn);
        }
    }

    /// <summary>
    /// Whether two paragraphs that follow each other are items of the same list, between which HTML auto spacing leaves no
    /// space (docx/external/poi/delins as Word prints it: list items 13.8 pt apart in 12 pt Times New Roman, 14 pt
    /// before the first and after the last): the same abstract definition, whatever instance numbers them (measured
    /// with docx/local/acl2-two, acl2-direct, acl2-bullet: 14 pt between items of two definitions;
    /// docx/external/libreoffice/extras-ooxmlexport/tdf133052: none between items of instances 2 and 1 of one).
    /// </summary>
    public static bool ListPair(ParagraphBlock first, ParagraphBlock second) =>
        first.Label != null && second.Label != null
        && (first.Label.List ?? first.Format.NumberingId) == (second.Label.List ?? second.Format.NumberingId);

    /// <summary>The width rotated text is laid out in: wide enough not to wrap.</summary>
    const double Unwrapped = 1584;

    static bool Rotated(CellModel cell) => cell.Format.TextDirection is "tbRl" or "btLr" or "tbRlV" or "tbLrV";

    /// <summary>The length of a line's content, from its left edge to the end of its last piece without trailing spaces.</summary>
    static double LineLength(Line line)
    {
        var end = 0.0;
        foreach (var piece in line.Pieces)
            end = Math.Max(end, piece.X + piece.Width - (piece.IsTab ? 0 : piece.Text.Length - piece.Text.TrimEnd(' ').Length > 0 ? piece.Advances.Skip(piece.Text.TrimEnd(' ').Length).Sum() : 0));
        return end;
    }

    /// <summary>A rotated cell's width: the thickness of its lines.</summary>
    (double Min, double Max) RotatedWidths(CellModel cell, PageFields? fields)
    {
        var content = Directed(cell, () => Content(cell.Blocks, Unwrapped, fields));
        return (content.Height, content.Height);
    }

    /// <param name="previous">The style of the paragraph before the blocks (<see cref="NoParagraph"/> for none).</param>
    /// <param name="next">The style of the paragraph after them.</param>
    /// <remarks>
    /// Contextual spacing works in cells as in the body, but not before Word 2013 where the document says
    /// w:allowSpaceOfSameStyleInTable (catalog: settings.compatibility, measured with docx/local/c5-cells).
    /// </remarks>
    CellContent Content(List<Block> blocks, double width, PageFields? fields, string? previous = NoParagraph, string? next = NoParagraph, double edge = 0,
        bool floats = false, IReadOnlyList<CellCut>? cuts = null)
    {
        var lines = new List<PlacedLine>();
        var fills = new List<FillRect>();
        var strokes = new List<Stroke>();
        var anchors = new List<CellAnchor>();
        var y = 0.0;
        var after = 0.0;
        var ownAfter = 0.0;
        // The space after the last paragraph in the text's flow: frames stand out of it (RowBox.Split).
        var flowAfter = 0.0;
        var first = true;
        // Floating tables in the cell, which the text after them goes around, and how far down they reach.
        var around = new List<Paginator.Exclusion>();
        // The floating tables of the cell, with their distances.
        var cellTables = new List<Paginator.Exclusion>();
        var floated = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var floatBottom = 0.0;
        var extent = 0.0;
        // The border box of the paragraphs that share their borders, from the first one's top.
        Paginator.Frame? previousFrame = null;
        var boxTop = 0.0;
        FillRect? lastShading = null;
        // With cuts through the nested tables: where each part after one begins, the next cut to make, where the part
        // being laid out begins and whether the text goes on at the top of a part after a floating table's cut.
        var boundaries = new List<CellBoundary>();
        var cutAt = 0;
        var partTop = 0.0;
        var resumed = false;
        for (var b = 0; b < blocks.Count; b++)
        {
            var block = blocks[b];
            switch (block)
            {
                // A cell's empty last paragraph after a nested table takes no room, whatever its spacing and mark
                // (measured with docx/local/nestedend-12, -14 and -15); a text box's does (docx/local/tbxtbl-a, -b: a
                // table and an empty paragraph make a box as high as a table and a line, its space after included;
                // docx/external/powertools/WC/WC050-Table-in-Text-Box).
                case ParagraphBlock last when cellBlocks && b == blocks.Count - 1 && b > 0 && blocks[b - 1] is TableBlock && !last.Shows:
                    break;
                case ParagraphBlock paragraph:
                {
                    var style = StyleOf(paragraph);
                    var spaced = paragraph.Format.ContextualSpacing != true || !context.Profile.ContextualSpacingInCells;
                    // HTML auto spacing is 14 pt between a cell's blocks, none at its top and bottom (measured with
                    // docx/local/autospace-cells-15 and -14).
                    var autoSpacing = !context.Profile.AddsSpacing;
                    var before = !spaced && previous == style || paragraph.ResumeAt != null ? 0
                        : paragraph.Format.BeforeAuto == true && autoSpacing ? (first || b > 0 && blocks[b - 1] is ParagraphBlock prior && ListPair(prior, paragraph) ? 0 : 14)
                        : paragraph.Format.Before ?? 0;
                    var following = b + 1 < blocks.Count
                        ? blocks[b + 1] switch
                        {
                            ParagraphBlock p => StyleOf(p),
                            TableBlock t => FirstParagraph(t.Table) is { } p ? StyleOf(p) : NoParagraph,
                            _ => NoParagraph,
                        }
                        : next;
                    // Drawings count from the paragraph's top above its own space before, as in the body. One that goes
                    // on at the top of a part keeps what its space before has more than the space after above (RowBox.Split).
                    var paragraphTop = y + (first || resumed ? 0 : after);
                    y += first ? before : resumed ? Math.Max(0, before - flowAfter) : context.Between(after, before, ownAfter);
                    resumed = false;
                    // Paragraph borders and shading as in the body: their top and bottom lines with their space take room
                    // (measured with docx/external/libreoffice/extras-layout/fdo48718-1: bordered paragraphs in a cell 3 pt
                    // taller each, their lines drawn).
                    var frame = Paginator.Frame.Read(paragraph.Format);
                    var continues = frame != null && frame.Same(previousFrame);
                    var nextContinues = frame != null && b + 1 < blocks.Count && blocks[b + 1] is ParagraphBlock followingParagraph
                        && frame.Same(Paginator.Frame.Read(followingParagraph.Format));
                    double? betweenAt = null;
                    if (frame != null && !continues)
                    {
                        y += frame.TopShadow;
                        boxTop = y;
                        y += frame.Top is { Visible: true } frameTop ? frameTop.Space + frameTop.Total : 0;
                    }
                    else if (frame?.Between is { Visible: true } between)
                    {
                        betweenAt = y + between.Space;
                        y += 2 * between.Space + between.Total;
                    }
                    var lineTops = new Dictionary<AnchorInline, (double Top, double X)>(ReferenceEqualityComparer.Instance);
                    var firstLine = lines.Count;
                    var linesTop = y;
                    // The paragraph's own floating drawings that text goes around push its lines and those after it, as
                    // in the body (measured with docx/local/cig-tb: a shape wrapped top and bottom takes the cell's text
                    // below it, the row growing with it; docx/external/powertools/WC/WC048-Text-Box-in-Cell: text beside
                    // a text box wrapped square).
                    if (floats)
                    {
                        foreach (var own in paragraph.Content.OfType<AnchorInline>())
                        {
                            if (InCell(own.Anchor) && CellExclusion(own, paragraphTop, width, edge, fields) is { } area)
                                around.Add(area);
                        }
                    }
                    var laid = around.Any(f => f.Bottom > linesTop)
                        ? paragraphs.Layout(paragraph, width, fields, room: slot => Paginator.Room(around, linesTop, slot, 0, width), from: paragraph.ResumeAt ?? 0)
                        : paragraphs.Layout(paragraph, width, fields, from: paragraph.ResumeAt ?? 0);
                    foreach (var line in laid)
                    {
                        y += line.Skip;
                        foreach (var anchor in line.Anchors ?? [])
                            lineTops[anchor] = (y, line.AnchorX?.GetValueOrDefault(anchor) ?? 0);
                        // Widow control holds where rows split from Word 2013 on (docx/local/rs3-*14, rs3-*12).
                        lines.Add(new PlacedLine(line, 0, y)
                        {
                            Widow = (paragraph.Format.WidowControl ?? true) && context.Profile.SplitRowsKeepLines,
                            Lead = !first && lines.Count == firstLine ? Math.Max(0, before - flowAfter) : 0,
                        });
                        y += line.Height;
                    }
                    if (frame != null)
                    {
                        if (!nextContinues)
                            y += frame.Bottom is { Visible: true } bottomLine ? bottomLine.Space + bottomLine.Total : 0;
                        var shadedAt = fills.Count;
                        frame.Draw(fills, fills, 0, width, boxTop, y, top: !continues, bottom: !nextContinues, between: betweenAt);
                        if (b == blocks.Count - 1 && frame.Fill != null && fills.Count > shadedAt)
                            lastShading = fills[shadedAt];
                        if (!nextContinues)
                            y += frame.BottomShadow;
                    }
                    previousFrame = frame;
                    foreach (var anchor in paragraph.Content.OfType<AnchorInline>())
                    {
                        var (lineTop, x) = lineTops.GetValueOrDefault(anchor, (paragraphTop, 0));
                        var reaches = floats && InCell(anchor.Anchor) ? Reach(anchor, paragraphTop, lineTop, fields) : null;
                        anchors.Add(new CellAnchor(anchor, paragraphTop, lineTop) { CharacterX = x, Reach = reaches is { Wraps: true } wrapped ? wrapped.Bottom : null });
                        // A cell's row grows down to its drawings that text goes around, whether or not they are laid
                        // out in the cell, not to those in front of or behind the text; vertical alignment moves the
                        // content with all of them (measured with docx/local/cig-*: a 40 pt shape wrapped square or top
                        // and bottom from its paragraph, its line or the margin, layoutInCell 0 too, in modes 15 and 14;
                        // not wrapped the row keeps its line; a bottom-aligned cell's content ends with an unwrapped
                        // shape's bottom; docx/external/libreoffice/extras-ooxmlexport/tdf37153_considerWrapOnObjPos).
                        if (floats && InCell(anchor.Anchor) && Reach(anchor, paragraphTop, lineTop, fields) is { } reach)
                        {
                            extent = Math.Max(extent, reach.Bottom);
                            if (reach.Wraps)
                                floatBottom = Math.Max(floatBottom, reach.Bottom);
                        }
                    }
                    // At the cell's end a list item keeps its auto space after, other paragraphs none (measured with
                    // docx/local/acl-list, acl-mixed, acl-14: numbered items 14 pt above the cell's bottom line, plain
                    // paragraphs on it; docx/external/libreoffice/extras-ooxmlexport/tdf132802).
                    ownAfter = paragraph.Format.AfterAuto == true && autoSpacing
                        ? (b == blocks.Count - 1 ? (paragraph.Label != null ? 14 : 0)
                            : blocks[b + 1] is ParagraphBlock nextParagraph && ListPair(paragraph, nextParagraph) ? 0 : 14)
                        : paragraph.Format.After ?? 0;
                    after = !spaced && following == style ? 0 : ownAfter;
                    // A frame in a cell stands out of the text's flow: the paragraph after it begins a split row's
                    // continuation with what its space before has more than the space after the paragraph above the
                    // frame (measured with docx/local/spg3-12, -14, -15: 12 pt before after a framed paragraph with no
                    // space after below one with 12 pt after keep nothing; core-ooxml-pass/fdo78904_fileHang).
                    if (paragraph.Format.Frame == null)
                        flowAfter = after;
                    if (lines.Count > firstLine)
                        lines[^1] = lines[^1] with { After = after, Keep = paragraph.Format.KeepNext == true && context.Profile.SplitRowsKeepLines };
                    previous = style;
                    first = false;
                    break;
                }
                // A floating table in a cell stands at its position in the cell's text area, at the top of the paragraph
                // after it or of the cell's content, and the text after it goes around it (measured with
                // docx/local/nf-1 to nf-7: tblpX and tblpY from the text's left and the next paragraph's top, the margin
                // as the text, the page from the cell's left edge, tblpXSpec right at the text's right; docx/external/
                // libreoffice/core-layout/floattable-del-empty). One without a vertical position (tblpY 0, no tblpYSpec
                // or inline) not anchored to the text stands in the text as other tables there, the text below it, and
                // with a position the cell's top counts without vertAnchor (measured with docx/local/nh-*, ni-* in modes
                // 14 and 15: without vertAnchor or with margin, after the cell's first paragraph or at its top; with
                // vertAnchor text the text goes beside it, nf-7, ni-1).
                case TableBlock floating when floats && floating.Table.Format.Floating is { } position
                    && !((position.Twips("tblpY") ?? 0) == 0 && position.Attr("tblpYSpec") is null or "inline" && position.Attr("vertAnchor") != "text"):
                {
                    // Laid out as a table nested in the cell, as the tables of the text there are (docx/external/
                    // libreoffice/writerfilter-dmapper/floattable-hidemark: a floating table in a cell in mode 12 keeps
                    // its cell's own margins, no shift or widening of the text's tables before Word 2013).
                    var floatInCell = inCell;
                    inCell = cellBlocks;
                    var box = Layout(floating.Table, width, fields, previous);
                    inCell = floatInCell;
                    double Twips(string name) => position.Twips(name) ?? 0;
                    var x = position.Attr("tblpXSpec") switch
                    {
                        "left" or "inside" => 0,
                        "right" or "outside" => width - box.Width,
                        "center" => (width - box.Width) / 2,
                        _ => Twips("tblpX") - (position.Attr("horzAnchor") == "page" ? edge : 0) - (context.Profile.FloatingTablesByText ? box.Inset : 0),
                    };
                    // It stays inside its cell's lines as drawings do, one wider than the cell at its left line (measured with
                    // docx/local/fic-14, fic-15: tblpX -426 at the left line, a table of 190 pt at 426 twips ending at the right
                    // one, of 250 pt at the left; docx/external/libreoffice/core-objectpositioning/floattable-overlap-cell).
                    if (cellInside is { } inside)
                        x = Math.Max(inside.Left, Math.Min(x, inside.Right - box.Width));
                    var top = Math.Max(0, ((position.Attr("vertAnchor") ?? "margin") is "page" or "margin" ? 0 : y + (first ? 0 : after)) + Twips("tblpY"));
                    // One that may not overlap others (w:tblOverlap never) goes below a floating table of the cell before it
                    // that it would overlap, where it stands across (found by agent BE in Word's PDF of docx/external/
                    // libreoffice/writerfilter-dmapper/floattable-nested-cellstart: the second of two tables at tblpY 0
                    // below the first, at the same x, in mode 12).
                    if (floating.Table.Format.NoOverlap)
                    {
                        var height = box.Rows.Sum(r => r.Height) + (box.Rows.Count > 0 ? box.Rows[^1].BottomBorderWidth : 0);
                        for (var tries = 0; tries < 8; tries++)
                        {
                            var (l, t, r, bottom) = (x - Twips("leftFromText"), top - Twips("topFromText"), x + box.Width + Twips("rightFromText"), top + height + Twips("bottomFromText"));
                            if (cellTables.FirstOrDefault(e => e.Left < r - 0.01 && e.Right > l + 0.01 && e.Top < bottom - 0.01 && e.Bottom > t + 0.01) is not { } other)
                                break;
                            top = other.Bottom + Twips("topFromText");
                        }
                    }
                    var (linesFrom, fillsFrom, strokesFrom, cutsFrom) = (lines.Count, fills.Count, strokes.Count, boundaries.Count);
                    (top, var tableTop) = Rows(box, x - box.Left, top, b);
                    Frame(box, x - box.Left, tableTop, top, fills);
                    floated.UnionWith(lines.Skip(linesFrom));
                    floated.UnionWith(fills.Skip(fillsFrom));
                    floated.UnionWith(strokes.Skip(strokesFrom));
                    around.Add(new Paginator.Exclusion(x - Twips("leftFromText"), tableTop - Twips("topFromText"), x + box.Width + Twips("rightFromText"),
                        top + Twips("bottomFromText"), false, "bothSides"));
                    cellTables.Add(around[^1]);
                    floatBottom = Math.Max(floatBottom, top);
                    // Where a cut goes through it the text after it begins beside its last part, at that part's top, the
                    // parts before without it (measured with docx/local/nft-*-f, -lf, -ft: a table of three rows over two
                    // pages, the paragraph after it, one line or five beside the table, at the top of page 2 in modes 11
                    // and 15; LibreOffice's core-text/floattable-anchor-height: at the top of page 4 of three).
                    if (boundaries.Count > cutsFrom)
                        (y, after, resumed) = (tableTop, 0, true);
                    break;
                }
                case TableBlock nested:
                {
                    previousFrame = null;
                    y += first ? 0 : after;
                    var wasInCell = inCell;
                    inCell = cellBlocks;
                    var box = Layout(nested.Table, width, fields, previous);
                    inCell = wasInCell;
                    (y, var tableTop) = Rows(box, 0, y, b);
                    Frame(box, 0, tableTop, y, fills);
                    after = 0;
                    ownAfter = 0;
                    previous = box.Rows.Count > 0 ? context.Styles.ParagraphStyleOf(null) : previous;
                    first = false;
                    break;
                }
            }
        }
        return new CellContent(lines, fills, strokes, Math.Max(y + after, floatBottom))
        {
            Anchors = anchors, Reach = Math.Max(Math.Max(y + after, floatBottom), extent), Floated = floated.Count > 0 ? floated : null,
            LastShading = lastShading, Boundaries = boundaries,
        };

        // The rows of a nested table (block b) from top down, left at left, and the line below the last: where the cuts
        // go through the table, cut as the rows of a table in the text are, the part before ending with the line below
        // its last row, the part after beginning with the table's top line, a row cut in two going on at least as high
        // as its at-least height (measured with docx/local/nft-*: the line below and above the cut, the third row of at
        // least 169.6 pt going on 169.6 pt high with three lines, in modes 11 and 15; LibreOffice's core-text/
        // floattable-anchor-height). Returns how far down the table reaches and where its last part begins.
        (double Bottom, double Top) Rows(TableBox box, double left, double top, int b)
        {
            var tableTop = top;
            RowBox? placed = null;
            for (var r = 0; r < box.Rows.Count; r++)
            {
                var row = box.Rows[r];
                while (cuts != null && cutAt < cuts.Count && cuts[cutAt].Table == b + 1 && top + row.Height + row.BottomBorderWidth > cuts[cutAt].Room + 0.01)
                {
                    var room = cuts[cutAt++].Room;
                    if (row.Split(room - top - row.BottomBorderWidth, force: top <= partTop + 0.01) is { } parts)
                    {
                        var drawn = lines.Count;
                        Draw(parts.Head, parts.Head.Height, left, top, lines, fills, fills, strokes, anchors);
                        Unit(drawn, parts.Head, top, b);
                        top += parts.Head.Height;
                        placed = parts.Head;
                        row = parts.Tail.AtPageTop();
                    }
                    else
                        row = row.AtPageTop();
                    if (placed != null)
                    {
                        Close(placed, left, top);
                        top += placed.BottomBorderWidth;
                    }
                    boundaries.Add(new CellBoundary(lines.Count, fills.Count, strokes.Count, anchors.Count, top));
                    tableTop = partTop = top;
                    placed = null;
                }
                var at = lines.Count;
                Draw(row, row.Height, left, top, lines, fills, fills, strokes, anchors);
                Unit(at, row, top, b);
                top += row.Height;
                placed = row;
            }
            if (placed != null)
            {
                Close(placed, left, top);
                top += placed.BottomBorderWidth;
            }
            return (top, tableTop);
        }

        void Close(RowBox row, double left, double top)
        {
            fills.AddRange(row.BottomBorders.Select(f => f with { X = left + f.X, Y = top + f.Y }));
            strokes.AddRange(row.BottomStrokes.Select(s => s.Offset(left, top)));
        }

        // The lines of a nested table's row go with what of the row has to stand together.
        void Unit(int from, RowBox row, double rowTop, int b)
        {
            var bottom = rowTop + (row.CantSplit ? row.Height : row.TopWidth + row.Least);
            for (var k = from; k < lines.Count; k++)
                lines[k] = lines[k] with { UnitHeight = Math.Max(lines[k].UnitHeight, bottom - lines[k].Top), Nest = b + 1 };
        }
    }

    /// <summary>
    /// Draws a row (or the part of it on a page) <paramref name="height"/> points high at (left, top): shading, the
    /// lines at its top and through it, the diagonals and its cells' content.
    /// </summary>
    public static void Draw(RowBox row, double height, double left, double top, List<PlacedLine> lines, List<FillRect> shading, List<FillRect> borders, List<Stroke> strokes,
        List<CellAnchor>? anchors = null)
    {
        foreach (var (x, width, color) in row.Shading)
            shading.Add(new FillRect(left + x, top, width, height, color));
        foreach (var fill in row.Frames)
            borders.Add(fill with { X = left + fill.X, Y = top + fill.Y });
        foreach (var fill in row.TopBorders)
            borders.Add(fill with { X = left + fill.X, Y = top + fill.Y });
        strokes.AddRange(row.TopStrokes.Select(s => s.Offset(left, top)));
        foreach (var stripe in row.Verticals)
        {
            if (stripe.Dash != null)
                strokes.Add(new Stroke(left + stripe.X + stripe.Width / 2, top, left + stripe.X + stripe.Width / 2, top + height, stripe.Width, stripe.Color, stripe.Dash));
            else
                borders.Add(new FillRect(left + stripe.X, top, stripe.Width, height, stripe.Color));
        }
        foreach (var cell in row.Cells)
        {
            var (from, to, down, up) = cell.Diagonals;
            if (down is { Visible: true })
                strokes.Add(new Stroke(left + from, top + row.TopWidth, left + to, top + height, down.Width, down.Color, Dash(down)));
            if (up is { Visible: true })
                strokes.Add(new Stroke(left + to, top + row.TopWidth, left + from, top + height, up.Width, up.Color, Dash(up)));
            // What a nested row's line carries counts what the cell's alignment lowered it by too, so that a cut at a
            // centered cell's line takes the row's top line along (LibreOffice's extras-layout/
            // tdf170381-split-float-table-in-normal-table: an empty centered row of a nested table that goes on on the
            // next page goes there whole, its top line too).
            foreach (var line in cell.Lines)
                lines.Add(line with
                {
                    Left = left + line.Left, Top = top + line.Top, Carry = cell.ContentTop + cell.Lowered + line.Carry,
                    CarryBelow = cell.BottomMargin + row.BottomBorderWidth + line.CarryBelow,
                });
            foreach (var fill in cell.Fills)
                borders.Add(fill with { X = left + fill.X, Y = top + fill.Y });
            strokes.AddRange(cell.Strokes.Select(s => s.Offset(left, top)));
            anchors?.AddRange(cell.Anchors.Select(a => a.Offset(left, top)));
        }
    }

    /// <summary>The frame of a table with cell spacing from <paramref name="top"/> to <paramref name="bottom"/>.</summary>
    public static void Frame(TableBox box, double left, double top, double bottom, List<FillRect> borders)
    {
        var (t, l, b, r) = box.Frame;
        var x = left + box.Left;
        if (t is { Visible: true })
            borders.Add(new FillRect(x, top, box.Width, t.Total, t.ColorOf("top", cell: false)));
        if (b is { Visible: true })
            borders.Add(new FillRect(x, bottom - b.Total, box.Width, b.Total, b.ColorOf("bottom", cell: false)));
        if (l is { Visible: true })
            borders.Add(new FillRect(x, top, l.Total, bottom - top, l.ColorOf("left", cell: false)));
        if (r is { Visible: true })
            borders.Add(new FillRect(x + box.Width - r.Total, top, r.Total, bottom - top, r.ColorOf("right", cell: false)));
    }
}
