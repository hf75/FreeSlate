using FreeSlate.Text;

namespace FreeSlate.Docx.Layout;

/// <summary>A piece of a line with one style: its text, where it starts (points from the column's left edge) and its advances.</summary>
public sealed class Piece
{
    public required string Text { get; init; }
    public required RunStyle Style { get; init; }
    public double X { get; set; }

    /// <summary>The advance of every UTF-16 unit in points (a cluster's advance on its first unit, spacing and scale included).</summary>
    public required double[] Advances { get; init; }

    /// <summary>Extra width justification gives each of the first <see cref="JustifiedSpaces"/> spaces of the piece.</summary>
    public double SpaceExtra { get; set; }
    public int JustifiedSpaces { get; set; }

    public bool IsTab { get; init; }

    /// <summary>The tab's leader (dot, hyphen, underscore, heavy, middleDot) or null.</summary>
    public string? Leader { get; init; }

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

    /// <summary>The break that ends the line: none, a line break, a page break or a column break.</summary>
    public string? EndBreak { get; set; }

    public bool First { get; init; }
    public bool Last { get; set; }
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
    /// <summary>Lays out a paragraph in a column <paramref name="width"/> points wide.</summary>
    public List<Line> Layout(ParagraphBlock paragraph, double width)
    {
        var format = paragraph.Format;
        var (text, charStyles) = Flatten(paragraph);
        var advances = Advances(text, charStyles);
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
        while (true)
        {
            var startX = left + (first ? firstLine : 0);
            var limit = width - right;
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
                    x = TabStop(format, x, text, i, charStyles, advances, left, first ? firstLine : 0, out _);
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

            var line = BuildLine(text, charStyles, advances, lineStart, end, startX, format, left, first ? firstLine : 0, mark);
            line.EndBreak = endBreak;
            lines.Add(line);
            first = false;
            lineStart = end;
            if (lineStart >= text.Length)
                break;
        }
        if (lines.Count == 0)
            lines.Add(BuildLine(text, charStyles, advances, 0, 0, left + firstLine, format, left, firstLine, mark));
        // A paragraph that ends with a break has an empty last line.
        if (lines[^1].EndBreak is "line" && lineStart >= text.Length)
            lines.Add(BuildLine(text, charStyles, advances, text.Length, text.Length, left, format, left, 0, mark));
        lines[^1].Last = true;

        foreach (var line in lines)
            Align(line, format, width - right, left + (line.First ? firstLine : 0));
        Space(lines, format);
        return lines;
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
    (string Text, List<RunStyle> Styles) Flatten(ParagraphBlock paragraph)
    {
        var text = new System.Text.StringBuilder();
        var list = new List<RunStyle>();
        foreach (var inline in paragraph.Content)
        {
            var format = inline.Format;
            switch (inline)
            {
                case TextInline t:
                {
                    if (format.Vanish == true)
                        continue;   // hidden text is not printed (Word's PDF export leaves it out)
                    var value = format.Caps == true || format.SmallCaps == true ? t.Text.ToUpperInvariant() : t.Text;
                    foreach (var rune in value.EnumerateRunes())
                    {
                        var style = styles.Style(format, RunStyles.SlotOf(rune.Value, format));
                        var s = rune.ToString();
                        text.Append(s);
                        for (var k = 0; k < s.Length; k++)
                            list.Add(style);
                    }
                    break;
                }
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
        return (text.ToString(), list);
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
    double TabStop(ParagraphFormat format, double x, string text, int tab, List<RunStyle> charStyles, double[] advances, double left, double firstLine, out TabStop? stop)
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
                "center" => t.Position - width / 2,
                "right" or "end" => t.Position - width,
                "decimal" => DecimalStart(text, tab + 1, end, advances, t.Position, width),
                _ => t.Position,
            };
            return Math.Max(x, start);
        }
        var interval = context.DefaultTabStop > 0 ? context.DefaultTabStop : 36;
        return (Math.Floor(x / interval + 1e-6) + 1) * interval;
    }

    static double DecimalStart(string text, int start, int end, double[] advances, double position, double width)
    {
        var point = text.IndexOf('.', start, end - start);
        if (point < 0)
            return position - width;
        var before = 0.0;
        for (var k = start; k < point; k++)
            before += advances[k];
        return position - before;
    }

    Line BuildLine(string text, List<RunStyle> charStyles, double[] advances, int start, int end, double startX,
        ParagraphFormat format, double left, double firstLine, RunStyle mark)
    {
        var line = new Line { First = start == 0 };
        var x = startX;
        var pieceStart = start;
        for (var i = start; i <= end; i++)
        {
            var boundary = i == end || text[i] is '\t' or '\n' or '\f' or '\v' || i > pieceStart && !ReferenceEquals(charStyles[i], charStyles[pieceStart]);
            if (!boundary)
                continue;
            if (i > pieceStart && text[pieceStart] is not ('\t' or '\n' or '\f' or '\v'))
            {
                var piece = new Piece
                {
                    Text = text[pieceStart..i],
                    Style = charStyles[pieceStart],
                    X = x,
                    Advances = advances[pieceStart..i],
                };
                line.Pieces.Add(piece);
                x += piece.Width;
            }
            if (i < end && text[i] == '\t')
            {
                var to = TabStop(format, x, text, i, charStyles, advances, left, firstLine, out var stop);
                line.Pieces.Add(new Piece
                {
                    Text = "\t",
                    Style = charStyles[i],
                    X = x,
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
        double above = 0, below = 0;
        var any = false;
        foreach (var piece in line.Pieces.Where(p => !p.IsTab))
        {
            var (a, b) = piece.Style.Metrics;
            above = Math.Max(above, a + piece.Style.Raise);
            below = Math.Max(below, b - piece.Style.Raise);
            any = true;
        }
        if (!any)
        {
            var style = end > start && start < charStyles.Count ? charStyles[start] : mark;
            (above, below) = style.Metrics;
        }
        line.Above = above;
        line.Below = below;
        return line;
    }

    /// <summary>
    /// Line heights and baselines by the paragraph's line spacing (measured with 14 fonts, docx/local/lm-*): auto is a
    /// multiple of the natural height with the baseline where single spacing puts it (the extra goes below); exact is
    /// the height given with the baseline at 80 % of it whatever the font; at least grows the line, the extra going
    /// above the text.
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
                    break;
                case "atLeast":
                    line.Height = Math.Max(value / 20, natural);
                    line.Baseline = line.Height - line.Below;
                    break;
                default:
                    line.Height = natural * value / 240;
                    line.Baseline = line.Above;
                    break;
            }
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
