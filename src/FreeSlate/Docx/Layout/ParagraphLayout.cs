using System.Text;
using FreeSlate.Text;

namespace FreeSlate.Docx.Layout;

/// <summary>A piece of a line with one style: its text, where it starts (points from the column's left edge) and its advances.</summary>
public sealed class Piece
{
    public required string Text { get; init; }
    public required RunStyle Style { get; init; }
    public double X { get; set; }

    /// <summary>Where the piece's text begins in its paragraph's text; -1 for tabs and spacers.</summary>
    public int Start { get; init; } = -1;

    /// <summary>The advance of every UTF-16 unit in points (a cluster's advance on its first unit, spacing and scale included).</summary>
    public required double[] Advances { get; init; }

    /// <summary>Extra width justification gives each of the first <see cref="JustifiedSpaces"/> spaces of the piece.</summary>
    public double SpaceExtra { get; set; }
    public int JustifiedSpaces { get; set; }

    public bool IsTab { get; init; }

    /// <summary>Part of the paragraph's list number (with a space after it).</summary>
    public bool IsLabel { get; init; }

    /// <summary>The tab's leader (dot, hyphen, underscore, heavy, middleDot) or null.</summary>
    public string? Leader { get; init; }

    /// <summary>An inline drawing the piece stands for (one object replacement character), or null.</summary>
    public DrawingInline? Drawing { get; init; }

    /// <summary>An equation the piece stands for (one object replacement character), or null.</summary>
    public Equations.Equation? Equation { get; init; }

    /// <summary>Where a display equation's line stands from the paragraph's left indent (m:jc, m:wrapIndent), or null.</summary>
    public double? DisplayX { get; init; }

    /// <summary>The hyperlink of the piece's text, or null.</summary>
    public string? Link { get; init; }

    /// <summary>A note separator's line (w:separator), drawn over the piece's width at the middle of the line.</summary>
    public bool Separator { get; init; }

    public double Width => Advances.Sum() + SpaceExtra * JustifiedSpaces;
}

/// <summary>A laid-out line of a paragraph (catalog: engine.lineLayout).</summary>
public sealed class Line
{
    public List<Piece> Pieces { get; } = [];

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

    /// <summary>Whether the line continues past a floating object in a second segment.</summary>
    public bool Jumped { get; set; }

    /// <summary>The footnotes whose references stand in the line (catalog: note.footnotes).</summary>
    public List<string>? Notes { get; set; }

    /// <summary>The floating drawings anchored in the line, which wp:positionV relativeFrom="line" places from its top.</summary>
    public List<AnchorInline>? Anchors { get; set; }

    /// <summary>Where in the line (from the column's left edge) each of its drawings is anchored, for relativeFrom="character".</summary>
    public Dictionary<AnchorInline, double>? AnchorX { get; set; }

    /// <summary>The brackets of editable ranges in the line: where (from the column's left edge) and whether they open.</summary>
    public List<(double X, bool Start)>? Brackets { get; set; }

    /// <summary>Bar tab stops (w:tab w:val="bar"): vertical lines at these positions from the column's left edge.</summary>
    public List<double>? Bars { get; set; }

    /// <summary>How far below the line its bars reach: the space after the paragraph, on its last line.</summary>
    public double BarExtension { get; set; }

    /// <summary>The break that ends the line: none, a line break, a page break or a column break.</summary>
    public string? EndBreak { get; set; }

    public bool First { get; init; }
    public bool Last { get; set; }
}

/// <summary>
/// The room a line has between floating objects (catalog: engine.floats): its segments from the column's left edge, or
/// none and how far down (from the paragraph's first line) the line must move to find room.
/// </summary>
public sealed record LineRoom(List<(double Start, double End)> Segments, double SkipTo = 0);

/// <summary>
/// Where a line would stand, for the room floating objects leave it: its top below the paragraph's first line top, its
/// estimated height and the top and bottom of its text (ascent above, descent below the baseline) from its top.
/// </summary>
public readonly record struct LineSlot(double Offset, double Height, double TextTop, double TextBottom);

/// <summary>
/// The values of the page fields where a paragraph stands (catalog: field.evaluation, section.pageNumbers): the page's
/// number and its section's number format, the document's pages and the section's.
/// </summary>
public sealed record PageFields(int Page, string PageFormat, int Pages, int SectionPages)
{
    /// <summary>The field's result here, or null for fields the layout does not compute.</summary>
    public string? Value(FieldInline field)
    {
        var format = NumberText.FieldFormat(field.NumberFormat);
        return field.Type switch
        {
            "PAGE" => NumberText.Format(Page, format ?? PageFormat),
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
                    if (piece.Text[k] == ' ' && spaces++ < piece.JustifiedSpaces)
                        x += piece.SpaceExtra;
                }
                return x;
            }
            end = piece.X + piece.Width;
        }
        return end;
    }

    /// <summary>Segments beside floating objects narrower than this take no text.</summary>
    const double MinimumSegment = 18;

    /// <summary>The inline drawings of the paragraph being laid out, by their place in its text.</summary>
    readonly Dictionary<int, DrawingInline> objects = [];

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
                ? new Equations.MathRunStyle(s.Size, s.Color)
                : default,
            Wml.W + "rPr")
        {
            DelimitersByHeight = true,
            IntegralLimits = MathSetting("intLim") ?? "subSup",
            NaryLimits = MathSetting("naryLim") ?? "undOvr",
        };
        var items = math.Display ? layout.EquationLines(math.Element, width, WrapIndent) : [layout.Equation(math.Element, false)];
        var lines = items.Select(item => new Equations.Equation(item)).ToList();
        laidEquations.AddOrUpdate(math, Tuple.Create(width, lines));
        return lines;
    }

    /// <summary>How far the continuation lines of a broken display equation are indented (m:wrapIndent, 1 inch by default).</summary>
    double WrapIndent => double.TryParse(MathSetting("wrapIndent"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var twips) ? twips / 20 : 72;

    /// <summary>The floating drawings of the paragraph being laid out, by their place in its text.</summary>
    readonly List<(int Index, AnchorInline Anchor)> anchored = [];

    /// <summary>The starts and ends of editable ranges of the paragraph being laid out, by their place in its text.</summary>
    readonly List<(int Index, bool Start)> permissions = [];

    /// <summary>The footnote references of the paragraph being laid out: where each stands and its note.</summary>
    readonly List<(int Index, string Id)> notes = [];

    /// <summary>Note separators of the paragraph being laid out (true for the continuation separator), by their place.</summary>
    readonly Dictionary<int, bool> separators = [];

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
    public List<Line> Layout(ParagraphBlock paragraph, double width, PageFields? fields = null, Func<LineSlot, LineRoom>? room = null)
    {
        var format = paragraph.Format;
        displayWidth = width - (format.Left ?? 0) - (format.Right ?? 0);
        var (text, charStyles, labelLength) = Flatten(paragraph, fields);
        var advances = Advances(text, charStyles);
        foreach (var (index, drawing) in objects)
            advances[index] = drawing.Box.Width;
        foreach (var (index, (equation, _)) in equations)
            advances[index] = equation.Width;
        foreach (var (index, continuation) in separators)
            advances[index] = continuation ? width - (format.Left ?? 0) - (format.Right ?? 0) : Math.Min(separatorWidth, width);
        // A number aligned right or centered ends or centers at the first line's indent (w:lvlJc).
        var labelShift = 0.0;
        var labelEnd = paragraph.Label?.Suffix == "space" ? labelLength + 1 : labelLength;
        if (paragraph.Label is { Justification: "right" or "end" or "center" } label && labelLength > 0)
        {
            var labelWidth = advances.Take(labelLength).Sum();
            labelShift = label.Justification == "center" ? labelWidth / 2 : labelWidth;
        }
        var breaks = LineBreaker.Opportunities(text, format.Kinsoku ?? true);
        var mark = styles.Style(paragraph.MarkFormat, WordSlot.Ascii);

        var left = format.Left ?? 0;
        var right = format.Right ?? 0;
        var firstLine = format.FirstLine ?? 0;
        var lines = new List<Line>();
        // Justified lines may squeeze the spaces between their words by a fifth to take one more word (measured with
        // Courier New, docx/local/jc-compress: ten words fit a column 12 pt narrower than they are, not 13 pt; left
        // aligned lines take no word that does not fit, docx/local/jc-left).
        var squeeze = Justified(format) ? 0.2 : 0;

        // Characters of the current line: [start, end) and where it starts.
        var lineStart = 0;
        var first = true;
        var offset = 0.0;            // the line's top below the paragraph's first line top
        var estimate = (mark.Metrics.Above + mark.Metrics.Below) * (format.LineRule is null or "auto" ? (format.Line ?? 240) / 240 : 1);
        var textBand = (Top: 0.0, Bottom: mark.Metrics.Above + mark.Metrics.Below);
        while (true)
        {
            var startX = left + (first ? firstLine - labelShift : 0);
            var limit = width - right;
            // Floating objects beside the line: its segments within the indents; none moves the line down.
            var segments = new List<(double Start, double End)>();
            var skip = 0.0;
            if (room != null)
            {
                for (var attempt = 0; attempt < 20; attempt++)
                {
                    var r = room(new LineSlot(offset + skip, estimate, textBand.Top, textBand.Bottom));
                    segments = r.Segments.Select(s => (Math.Max(s.Start, startX), Math.Min(s.End, limit))).Where(s => s.Item2 - s.Item1 >= MinimumSegment).ToList();
                    if (segments.Count > 0 || r.SkipTo <= offset + skip + 0.01)
                        break;
                    skip = r.SkipTo - offset;
                }
                if (segments.Count > 0)
                {
                    startX = segments[0].Start;
                    limit = segments[0].End;
                }
            }
            var segment = 0;
            var jumps = new Dictionary<int, double>();
            var x = startX;
            var spaces = 0.0;          // width of the spaces on the line so far
            var end = lineStart;
            var lastFit = -1;          // end of the text that fits, at a break opportunity
            var endBreak = (string?)null;
            var i = lineStart;
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
                if (c == '\t')
                {
                    var to = TabStop(format, x, text, i, charStyles, advances, left, first ? firstLine : 0, limit, out _);
                    if (to > limit + 0.005)
                    {
                        // A tab whose stop lies past the right margin starts the next line; at a line's start it fills
                        // the line and its text follows on the next (measured with docx/local/tab-break).
                        if (HasContent(text, lineStart, i))
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
                while (contentEnd > i && text[contentEnd - 1] == ' ')
                {
                    contentEnd--;
                    contentX -= advances[contentEnd];
                }
                var innerSpaces = spaces;
                for (var k = i; k < contentEnd; k++)
                {
                    if (text[k] == ' ')
                        innerSpaces += advances[k];
                }
                var fits = contentX - squeeze * innerSpaces <= limit + 0.005;
                // A word that does not fit the segment beside a floating object continues in the next segment.
                if (!fits && segment + 1 < segments.Count)
                {
                    segment++;
                    jumps[i] = segments[segment].Start;
                    x = segments[segment].Start;
                    limit = segments[segment].End;
                    continue;
                }
                if (!fits && end > lineStart && HasContent(text, lineStart, end))
                    break;   // the word goes to the next line
                if (!fits)
                {
                    // A word longer than the line: as many characters as fit, at least one.
                    var k = i;
                    var kx = x;
                    while (k < contentEnd && (k == i || kx + advances[k] <= limit + 0.005))
                    {
                        kx += advances[k];
                        k++;
                    }
                    end = k;
                    lastFit = k;
                    x = kx;
                    break;
                }
                x = w;
                for (var k = i; k < wordEnd; k++)
                {
                    if (text[k] == ' ')
                        spaces += advances[k];
                }
                end = wordEnd;
                lastFit = wordEnd;
                i = wordEnd;
            }
            if (end == lineStart && i >= text.Length)
                end = text.Length;
            if (lastFit < 0)
                lastFit = end;

            var line = BuildLine(text, charStyles, advances, lineStart, end, startX, format, left, first ? firstLine : 0, mark, labelEnd, limit, jumps);
            line.EndBreak = endBreak;
            line.Skip = skip;
            if (anchored.Where(a => a.Index >= lineStart && (a.Index < end || end >= text.Length)).Select(a => a.Anchor).ToList() is { Count: > 0 } inLine)
                line.Anchors = inLine;
            if (room != null)
            {
                // The line's height decides where the next one stands beside the objects.
                Space([line], format);
                offset += skip + line.Height;
                estimate = line.Height;
                textBand = (line.Baseline - line.Above, line.Baseline + line.Below);
                (line.Start, line.Limit) = (startX, segments.Count > 1 ? segments[^1].End : limit);
            }
            lines.Add(line);
            first = false;
            lineStart = end;
            if (lineStart >= text.Length)
                break;
        }
        if (lines.Count == 0)
            lines.Add(BuildLine(text, charStyles, advances, 0, 0, left + firstLine - labelShift, format, left, firstLine, mark, labelEnd, width - right));
        // A paragraph that ends with a break has an empty last line.
        if (lines[^1].EndBreak is "line" && lineStart >= text.Length)
            lines.Add(BuildLine(text, charStyles, advances, text.Length, text.Length, left, format, left, 0, mark, 0, width - right));
        lines[^1].Last = true;

        foreach (var line in lines)
        {
            // Lines broken around floating objects align in their room; lines in several segments stay as they are.
            if (line.Jumped)
                continue;
            // A display equation's line stands where its justification puts it, from the left indent.
            if (line.Pieces.FirstOrDefault(p => p.DisplayX != null) is { DisplayX: { } displayX } displayed)
            {
                displayed.X = left + displayX;
                continue;
            }
            Align(line, format, line.Limit ?? width - right, line.Start ?? left + (line.First ? firstLine - labelShift : 0));
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
        Space(lines, format);
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
    public (double Min, double Max) Widths(ParagraphBlock paragraph, PageFields? fields = null)
    {
        var format = paragraph.Format;
        displayWidth = double.MaxValue;
        var (text, charStyles, _) = Flatten(paragraph, fields);
        var advances = Advances(text, charStyles);
        foreach (var (index, (equation, _)) in equations)
            advances[index] = equation.Width;
        var breaks = LineBreaker.Opportunities(text, format.Kinsoku ?? true);
        var left = format.Left ?? 0;
        var right = format.Right ?? 0;
        var firstLine = format.FirstLine ?? 0;
        double min = 0, max = 0, word = 0, trailing = 0;
        var line = left + firstLine;
        var interval = context.DefaultTabStop > 0 ? context.DefaultTabStop : 36;
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
                line = stop?.Position ?? (Math.Floor(line / interval + 1e-6) + 1) * interval;
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
        return (min + left + Math.Max(0, firstLine) + right, max + right);
    }

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
    (string Text, List<RunStyle> Styles, int LabelLength) Flatten(ParagraphBlock paragraph, PageFields? fields)
    {
        var text = new System.Text.StringBuilder();
        var list = new List<RunStyle>();
        objects.Clear();
        equations.Clear();
        anchored.Clear();
        permissions.Clear();
        var breakAfterDisplay = false;
        links.Clear();
        notes.Clear();
        separators.Clear();

        void AddText(string value, RunFormat format, string? link = null)
        {
            if (link != null)
                links[text.Length] = (link, value.Length);
            if (format.Vanish == true)
                return;   // hidden text is not printed (Word's PDF export leaves it out)
            // All caps shows capitals; small caps shows lowercase letters as smaller capitals.
            var small = format.SmallCaps == true && format.Caps != true;
            foreach (var rune in value.EnumerateRunes())
            {
                var lower = small && Rune.IsLower(rune);
                var shown = format.Caps == true || lower ? Rune.ToUpperInvariant(rune) : rune;
                var style = styles.Style(format, RunStyles.SlotOf(shown.Value, format), lower);
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
            if (breakAfterDisplay && inline is not (BookmarkInline or AnchorInline or MathInline { Display: true }))
            {
                text.Append('\n');
                list.Add(styles.Style(format, WordSlot.Ascii));
                breakAfterDisplay = false;
            }
            switch (inline)
            {
                case MathInline math:
                {
                    var lines = EquationLines(math, displayWidth);
                    if (!math.Display)
                    {
                        foreach (var equation in lines)
                        {
                            equations[text.Length] = (equation, null);
                            text.Append('\uFFFC');
                            list.Add(styles.Style(format, WordSlot.Ascii));
                        }
                        break;
                    }
                    // A display equation's lines, placed by its justification: centered as a group (the lines left
                    // aligned in it, the continuations indented; measured: Word centers the widest line), each
                    // centered, at the left or right; m:wrapRight puts the continuations at the right.
                    var indent = WrapIndent;
                    var group = lines.Count > 0 ? lines.Select((l, k) => l.Width + (k > 0 ? indent : 0)).Max() : 0;
                    var right = MathSetting("wrapRight") is "1" or "on" or "true" || MathSettings?.Element(Equations.MathLayout.M + "wrapRight") is { } wrapRight && wrapRight.Attribute(Equations.MathLayout.M + "val") == null;
                    var available = displayWidth == double.MaxValue ? group : displayWidth;
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
                        equations[text.Length] = (lines[k], Math.Max(0, x));
                        text.Append('\uFFFC');
                        list.Add(styles.Style(format, WordSlot.Ascii));
                    }
                    breakAfterDisplay = true;
                    break;
                }
                case TextInline t:
                    AddText(t.Text, format, t.Link);
                    break;
                case FieldInline field when fields?.Value(field) is { } value:
                    AddText(value, format);
                    break;
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
                case DrawingInline drawing:
                    objects[text.Length] = drawing;
                    text.Append('\uFFFC');
                    list.Add(styles.Style(format, WordSlot.Ascii));
                    break;
                case NoteReferenceInline note:
                    if (!note.Endnote)
                        notes.Add((text.Length, note.Id));
                    AddText(note.Mark, format);
                    break;
                case SeparatorInline separator:
                    separators[text.Length] = separator.Continuation;
                    text.Append('\uFFFC');
                    list.Add(styles.Style(format, WordSlot.Ascii));
                    break;
                case TabInline:
                    text.Append('\t');
                    list.Add(styles.Style(format, WordSlot.Ascii));
                    break;
                case BreakInline b:
                    text.Append(b.Type switch { "page" => '\f', "column" => '\v', _ => '\n' });
                    list.Add(styles.Style(format, WordSlot.Ascii));
                    break;
            }
        }

        // The list number comes first, followed by a tab, a space or nothing (w:suff).
        var labelLength = 0;
        if (paragraph.Label is { } label)
        {
            AddText(label.Text, label.Format);
            labelLength = text.Length;
            if (label.Suffix == "tab")
            {
                text.Append('\t');
                list.Add(styles.Style(label.Format, WordSlot.Ascii));
            }
            else if (label.Suffix == "space")
                AddText(" ", label.Format);
        }
        foreach (var inline in paragraph.Content)
            Add(inline);
        return (text.ToString(), list, labelLength);
    }

    /// <summary>The advance of every character in points, shaped run by run with Word's features.</summary>
    static double[] Advances(string text, List<RunStyle> charStyles)
    {
        var advances = new double[text.Length];
        var start = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i < text.Length && ReferenceEquals(charStyles[i], charStyles[start]) && text[i] is not ('\t' or '\n' or '\f' or '\v')
                && text[start] is not ('\t' or '\n' or '\f' or '\v'))
                continue;
            if (text[start] is not ('\t' or '\n' or '\f' or '\v'))
            {
                var style = charStyles[start];
                var piece = text[start..i];
                var glyphs = style.Font.Data.GlyphsWithFeatures(piece, style.Features, language: style.Language);
                var firstOfCluster = new HashSet<int>();
                foreach (var g in glyphs)
                {
                    if (g.Cluster < piece.Length)
                    {
                        advances[start + g.Cluster] += g.Advance * style.Size * style.Scale;
                        firstOfCluster.Add(g.Cluster);
                    }
                }
                foreach (var cluster in firstOfCluster)
                    advances[start + cluster] += style.Spacing;
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
    /// <param name="limit">The right indent: centered, right and decimal stops past it align at it (measured with docx/local/tab-break).</param>
    double TabStop(ParagraphFormat format, double x, string text, int tab, List<RunStyle> charStyles, double[] advances, double left, double firstLine, double limit, out TabStop? stop)
    {
        stop = null;
        if (firstLine < 0 && x < left - 0.005)
        {
            // The left indent is a stop for the hanging first line, unless a custom stop comes before it.
            var custom = format.Tabs?.FirstOrDefault(t => t.Position > x + 0.005 && t.Align != "bar");
            if (custom == null || custom.Position > left)
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
            var start = t.Align switch
            {
                "center" or "right" or "end" or "decimal" when t.Position > limit + 0.005 => limit - width,
                "center" => t.Position - width / 2,
                "right" or "end" => t.Position - width,
                "decimal" => DecimalStart(text, tab + 1, end, advances, t.Position, width, context.DecimalSymbol),
                _ => t.Position,
            };
            return Math.Max(x, start);
        }
        var interval = context.DefaultTabStop > 0 ? context.DefaultTabStop : 36;
        return (Math.Floor(x / interval + 1e-6) + 1) * interval;
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
        var line = new Line { First = start == 0 };
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
                || i > pieceStart && i < end && links.Count > 0 && LinkAt(i) != LinkAt(pieceStart);
            if (!boundary)
                continue;
            if (i > pieceStart && text[pieceStart] is not ('\t' or '\n' or '\f' or '\v'))
            {
                var piece = new Piece
                {
                    Text = text[pieceStart..i],
                    Style = charStyles[pieceStart],
                    X = x,
                    Start = pieceStart,
                    Advances = advances[pieceStart..i],
                    IsLabel = pieceStart < labelEnd,
                    Drawing = text[pieceStart] == '\uFFFC' && objects.TryGetValue(pieceStart, out var drawing) ? drawing : null,
                    Equation = text[pieceStart] == '\uFFFC' && equations.TryGetValue(pieceStart, out var math) ? math.Equation : null,
                    DisplayX = text[pieceStart] == '\uFFFC' && equations.TryGetValue(pieceStart, out var display) ? display.DisplayX : null,
                    Separator = text[pieceStart] == '\uFFFC' && separators.ContainsKey(pieceStart),
                    Link = links.Count > 0 ? LinkAt(pieceStart) : null,
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
        double above = 0, below = 0, raised = 0, lowered = 0, largest = 0;
        var any = false;
        var drawings = false;
        foreach (var piece in line.Pieces.Where(p => !p.IsTab))
        {
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
            // An inline drawing stands on the baseline: its box is all ascent; the line spacing's extra comes from
            // the fonts (measured with docx/generated/drawings: a 90 pt picture beside 12 pt Aptos at 1.16 lines makes
            // the line 90 + 3.38 + 2.32 pt high, a picture alone 160.5 + 2.32 pt).
            if (piece.Drawing is { } drawing)
            {
                above = Math.Max(above, drawing.Box.Height);
                raised = Math.Max(raised, drawing.Box.Height);
                drawings = true;
                continue;
            }
            var (a, b) = piece.Style.Metrics;
            above = Math.Max(above, a);
            // Text effects reach beyond the text; the line makes room for them outside its spacing.
            var (reachAbove, reachBelow) = TextEffects.Reach(piece.Style.Effects, piece.Style.Size);
            raised = Math.Max(raised, a + piece.Style.Raise + reachAbove);
            any = true;
            // A list number's ascent counts, its descent not (measured with docx/local/num-rules: a 16 pt number
            // before 12 pt Arial makes the line 17.52 pt high, its ascent and the text's descent).
            if (piece.IsLabel)
                continue;
            below = Math.Max(below, b);
            largest = Math.Max(largest, a + b);
            lowered = Math.Max(lowered, b - piece.Style.Raise + reachBelow);
        }
        if (drawings && !any)
        {
            // Drawings without text: no descent, the paragraph mark's font gives the spacing's extra.
            var (a, b) = mark.Metrics;
            largest = a + b;
            any = true;
        }
        else if (any && below == 0 && largest == 0)
        {
            // Only a number on the line: the paragraph mark gives the rest.
            var (a, b) = mark.Metrics;
            below = b;
            largest = a + b;
            lowered = Math.Max(lowered, b);
        }
        if (!any)
        {
            var style = end > start && start < charStyles.Count ? charStyles[start] : mark;
            (above, below) = style.Metrics;
            largest = above + below;
            (raised, lowered) = (above, below);
        }
        line.Above = above;
        line.Below = below;
        line.LargestNatural = largest;
        line.RaisedAbove = Math.Max(0, raised - above);
        line.LoweredBelow = Math.Max(0, lowered - below);
        return line;
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
    static void Space(List<Line> lines, ParagraphFormat format)
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
                    line.Baseline = 0.8 * line.Height;
                    continue;
                case "atLeast":
                    line.Height = Math.Max(value / 20, natural);
                    line.Baseline = line.Height - line.Below;
                    break;
                default:
                    line.Height = natural + line.LargestNatural * (value / 240 - 1);
                    line.Baseline = line.Above;
                    line.Overhang = Math.Max(0, line.Height - natural);
                    break;
            }
            line.Height += line.RaisedAbove + line.LoweredBelow;
            line.Baseline += line.RaisedAbove;
        }
    }

    static bool Justified(ParagraphFormat format) =>
        format.Justification is "both" or "distribute" or "lowKashida" or "mediumKashida" or "highKashida" or "thaiDistribute";

    /// <summary>Alignment (catalog: paragraph.basic): left, centered, right, justified (not the last line) and distributed.</summary>
    static void Align(Line line, ParagraphFormat format, double limit, double startX)
    {
        var content = line.Pieces.Where(p => !p.IsTab).ToList();
        if (content.Count == 0)
            return;
        // The line's end without trailing spaces.
        var last = content[^1];
        var trailing = 0.0;
        for (var k = last.Text.Length - 1; k >= 0 && last.Text[k] == ' '; k--)
            trailing += last.Advances[k];
        var end = last.X + last.Width - trailing;
        var free = limit - end;
        var jc = format.Justification ?? "left";
        var bidi = format.Bidi == true;
        if (bidi && jc is "left" or "start") jc = "right";
        else if (bidi && jc is "right" or "end") jc = "left";
        // Tabs fix what comes after them; alignment moves only the text after the last tab.
        var lastTab = line.Pieces.FindLastIndex(p => p.IsTab);
        var movable = line.Pieces.Skip(lastTab + 1).ToList();
        double shift = jc switch
        {
            "center" => free / 2,
            "right" or "end" => free,
            _ => 0,
        };
        if (shift > 0 && lastTab < 0)
        {
            foreach (var piece in movable)
                piece.X += shift;
            return;
        }
        var justified = jc is "both" or "lowKashida" or "mediumKashida" or "highKashida" or "distribute" or "thaiDistribute";
        // The last line of a justified paragraph keeps its natural spacing, unless it took a word by squeezing.
        var justify = justified && (!line.Last || jc is "distribute" or "thaiDistribute" || free < 0);
        if (!justify || Math.Abs(free) < 1e-9)
            return;
        // The spaces between words share the free width (trailing spaces excluded).
        var spaces = 0;
        foreach (var piece in movable)
            spaces += piece.Text.Count(c => c == ' ');
        spaces -= last.Text.Length - last.Text.TrimEnd(' ').Length;
        if (spaces <= 0)
            return;
        var extra = free / spaces;
        var x = movable.Count > 0 ? movable[0].X : startX;
        var remaining = spaces;
        foreach (var piece in movable)
        {
            piece.X = x;
            var own = piece.Text.Count(c => c == ' ');
            if (ReferenceEquals(piece, last))
                own -= last.Text.Length - last.Text.TrimEnd(' ').Length;
            own = Math.Min(Math.Max(0, own), remaining);
            remaining -= own;
            piece.SpaceExtra = extra;
            piece.JustifiedSpaces = own;
            x += piece.Width;
        }
    }
}
