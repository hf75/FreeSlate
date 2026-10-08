using FreeSlate.Drawing;

namespace FreeSlate.Text;

/// <summary>Resolved character formatting of a run. Sizes and spacing in points.</summary>
public sealed record TextStyle(
    ResolvedFont Font,
    double Size,
    RgbColor Color,
    bool Bold = false,
    bool Italic = false,
    bool Kerning = true,
    double LetterSpacing = 0,
    string? Underline = null,
    string? Strike = null,
    double BaselineShift = 0,
    RgbColor? Highlight = null,
    bool AllCaps = false,
    System.Xml.Linq.XElement? Effects = null,
    TextLink? Link = null,
    Fill? Paint = null,
    Outline? Line = null);

/// <summary>A hyperlink of a run: a URL or slide anchor, a slide show action and a tooltip.</summary>
public sealed record TextLink(string? Href, string? Action, string? Tooltip);

/// <summary>A piece of text with one style; Text "\n" is a line break (a:br).</summary>
public sealed record TextRun(string Text, TextStyle Style);

/// <summary>Line spacing: a multiple of single spacing (1.0 = 100 %) or an exact height in points.</summary>
public readonly record struct LineSpacing(bool Exact, double Value)
{
    public static readonly LineSpacing Single = new(false, 1);
}

/// <summary>Space before/after a paragraph: points, or a multiple of single spacing (1.2 × font size).</summary>
public readonly record struct ParagraphSpace(bool Percent, double Value)
{
    public static readonly ParagraphSpace None = new(false, 0);

    public double Points(double fontSize) => Percent ? Value * 1.2 * fontSize : Value;
}

/// <summary>A bullet: text in its own style, or a picture (data URI) with its width-to-height ratio.</summary>
public sealed record Bullet(string Text, TextStyle Style, string? Picture = null, double PictureAspect = 1)
{
    /// <summary>Picture bullets are as high as the capitals of the text (measured) and sit on the baseline.</summary>
    public double PictureHeight => Style.Size * Style.Font.Data.CapHeight;
}

public sealed class ParagraphInput
{
    public required List<TextRun> Runs { get; init; }

    /// <summary>Style of the paragraph mark; gives empty paragraphs their height.</summary>
    public required TextStyle EndStyle { get; init; }

    /// <summary>l, ctr, r, just, justLow, dist, thaiDist.</summary>
    public string Align { get; init; } = "l";

    public double MarginLeft { get; init; }
    public double MarginRight { get; init; }
    public double Indent { get; init; }
    public LineSpacing Spacing { get; init; } = LineSpacing.Single;
    public ParagraphSpace SpaceBefore { get; init; } = ParagraphSpace.None;
    public ParagraphSpace SpaceAfter { get; init; } = ParagraphSpace.None;
    public Bullet? Bullet { get; init; }
    public double DefaultTabSize { get; init; } = 72;
}

/// <summary>A run placed on a line: X from the left edge of the text area, in points.</summary>
public sealed record PlacedRun(string Text, TextStyle Style, double X, double Width)
{
    /// <summary>The picture of a picture bullet and its height in points.</summary>
    public string? Picture { get; init; }

    /// <summary>An East Asian character that stays upright in vertical text.</summary>
    public bool Upright { get; init; }

    public double PictureHeight { get; init; }
}

public sealed class LaidOutLine
{
    /// <summary>Index of the paragraph the line belongs to (animations address paragraphs).</summary>
    public int Paragraph { get; set; }

    /// <summary>Baseline from the top of the text area, in points.</summary>
    public double Baseline { get; set; }
    public double Ascent { get; init; }
    public double Descent { get; init; }
    public double Width { get; init; }

    /// <summary>The paragraph's alignment (l, ctr, r, just …).</summary>
    public string Align { get; init; } = "l";

    public List<PlacedRun> Runs { get; } = [];
}

public sealed class LaidOutText
{
    public List<LaidOutLine> Lines { get; } = [];

    /// <summary>Height of the text block from the top of the first line to the bottom of the last, in points.</summary>
    public double Height { get; set; }
}

/// <summary>
/// Lays out paragraphs like PowerPoint: own line breaking with shaped widths and PowerPoint's vertical line
/// model, measured with the text-metrics calibration deck (catalog: engine.textLayout):
/// <list type="bullet">
/// <item>Single line height S = 1.2 × font size; multiples scale it; exact spacing is rounded to whole points.</item>
/// <item>At single spacing the baseline sits at S × ascent ratio of the font. Above single spacing it sits at
/// H × min(0.75, ascent ratio); below, at max(S × ratio − (S − H), H × min(0.75, ratio)).</item>
/// <item>Each line takes the largest ascent and descent of its runs; bullets do not count.</item>
/// <item>Space before is ignored for the first paragraph; percentages refer to 1.2 × font size.</item>
/// <item>Widths add up glyph advances rounded to PowerPoint's master units (1/8 point), see
/// <see cref="FontData.LayoutAdvances"/>.</item>
/// </list>
/// </summary>
public static class TextLayout
{
    public static LaidOutText Layout(IReadOnlyList<ParagraphInput> paragraphs, double width, bool wrap)
    {
        var result = new LaidOutText();
        double cursor = 0;
        double pendingAfter = 0;

        for (var p = 0; p < paragraphs.Count; p++)
        {
            var paragraph = paragraphs[p];
            var firstSize = paragraph.Runs.FirstOrDefault(r => r.Text != "\n")?.Style.Size ?? paragraph.EndStyle.Size;
            if (p > 0)
                cursor += pendingAfter + paragraph.SpaceBefore.Points(firstSize);
            pendingAfter = paragraph.SpaceAfter.Points(firstSize);

            foreach (var line in BreakLines(paragraph, width, wrap))
            {
                line.Paragraph = p;
                line.Baseline = cursor + line.Ascent;
                cursor = line.Baseline + line.Descent;
                result.Lines.Add(line);
            }
        }
        result.Height = cursor;
        return result;
    }

    /// <summary>Ascent and descent of a run per PowerPoint's line model.</summary>
    public static (double Ascent, double Descent) Metrics(TextStyle style, LineSpacing spacing)
    {
        var single = 1.2 * style.Size;
        var height = spacing.Exact ? Math.Round(spacing.Value) : single * Math.Max(0.01, spacing.Value);
        var ratio = style.Font.Data.AscentRatio;
        var capped = Math.Min(0.75, ratio);
        var ascent = height <= single + 1e-9
            ? Math.Max(single * ratio - (single - height), height * capped)
            : height * capped;
        return (ascent, height - ascent);
    }

    sealed class Segment(int start, int end)
    {
        public int Start { get; } = start;
        public int End { get; } = end;
        public double Width { get; set; }
        public double TrailingWidth { get; set; }
        public bool IsBreak { get; set; }
        public bool IsTab { get; set; }
    }

    static List<LaidOutLine> BreakLines(ParagraphInput paragraph, double width, bool wrap)
    {
        // Flatten the runs into one string with a style per character (caps applied).
        var chars = new System.Text.StringBuilder();
        var styles = new List<TextStyle>();
        foreach (var run in paragraph.Runs)
        {
            var text = run.Style.AllCaps ? run.Text.ToUpperInvariant() : run.Text;
            chars.Append(text);
            styles.AddRange(Enumerable.Repeat(run.Style, text.Length));
        }
        var all = chars.ToString();
        var breaks = LineBreaker.Opportunities(all);

        // Segments end at break opportunities, line breaks and tabs.
        var segments = new List<Segment>();
        var segmentStart = 0;
        for (var i = 0; i < all.Length; i++)
        {
            if (all[i] is '\n' or '\t')
            {
                if (i > segmentStart) segments.Add(new Segment(segmentStart, i));
                segments.Add(new Segment(i, i + 1) { IsBreak = all[i] == '\n', IsTab = all[i] == '\t' });
                segmentStart = i + 1;
            }
            else if (breaks[i] || i == all.Length - 1)
            {
                segments.Add(new Segment(segmentStart, i + 1));
                segmentStart = i + 1;
            }
        }
        foreach (var s in segments.Where(s => !s.IsBreak && !s.IsTab))
        {
            s.Width = Measure(all, styles, s.Start, s.End);
            var trailing = s.End;
            while (trailing > s.Start && all[trailing - 1] == ' ') trailing--;
            s.TrailingWidth = s.Width - Measure(all, styles, s.Start, trailing);
        }

        var lines = new List<LaidOutLine>();
        var bulletWidth = paragraph.Bullet is { } b
            ? b.Picture != null ? b.PictureHeight * b.PictureAspect : b.Style.Font.Data.LayoutWidth(b.Text, b.Style.Size, b.Style.Kerning)
            : 0;
        var limit = width - paragraph.MarginRight;
        // A hanging indent reaching left of the text area moves the whole paragraph right: the first line starts
        // at the edge (measured: marL 0 with indent -27 pt puts the bullet at 0 and the text at 27 pt).
        var marginLeft = EffectiveMargin(paragraph);
        var firstStart = marginLeft + paragraph.Indent;
        var textStart = paragraph.Bullet != null ? Math.Max(marginLeft, firstStart + bulletWidth) : firstStart;

        var current = new List<(int Start, int End, double X)>();
        var x = textStart;
        var lineStart = textStart;
        var lastContentEnd = textStart;
        var first = true;
        var endsWithBreak = false;

        void Finish(bool lastOfParagraph)
        {
            lines.Add(BuildLine(paragraph, all, styles, current, lineStart, lastContentEnd, width, first, lastOfParagraph, bulletWidth));
            current = [];
            first = false;
            x = lineStart = lastContentEnd = marginLeft;
        }

        foreach (var segment in segments)
        {
            endsWithBreak = false;
            if (segment.IsBreak)
            {
                current.Add((segment.Start, segment.Start, x));   // keeps the break's style for empty lines
                Finish(false);
                endsWithBreak = true;
                continue;
            }
            if (segment.IsTab)
            {
                var tab = paragraph.DefaultTabSize <= 0 ? 72 : paragraph.DefaultTabSize;
                x = (Math.Floor(x / tab + 1e-9) + 1) * tab;
                continue;
            }

            var content = segment.Width - segment.TrailingWidth;
            if (wrap && current.Count > 0 && x + content > limit + 0.01)
                Finish(false);

            if (wrap && x + content > limit + 0.01 && current.Count == 0 && segment.End - segment.Start > 1)
            {
                // A word longer than the line: break it between characters.
                var start = segment.Start;
                while (start < segment.End)
                {
                    var end = start + 1;
                    while (end < segment.End && x + Measure(all, styles, start, end + 1) <= limit + 0.01) end++;
                    var w = Measure(all, styles, start, end);
                    current.Add((start, end, x));
                    x += w;
                    lastContentEnd = x;
                    if (end < segment.End) Finish(false);
                    start = end;
                }
                continue;
            }

            current.Add((segment.Start, segment.End, x));
            lastContentEnd = x + content;
            x += segment.Width;
        }
        if (current.Count > 0 || lines.Count == 0 || endsWithBreak)
            Finish(true);
        return lines;
    }

    static LaidOutLine BuildLine(ParagraphInput paragraph, string all, List<TextStyle> styles,
        List<(int Start, int End, double X)> pieces, double lineStart, double contentEnd, double width,
        bool first, bool last, double bulletWidth)
    {
        // Vertical metrics: the largest ascent and descent of the line's runs (the paragraph mark if empty).
        double ascent = 0, descent = 0;
        var lineStyles = pieces.SelectMany(p => Enumerable.Range(p.Start, Math.Max(1, p.End - p.Start)))
            .Where(i => i < styles.Count).Select(i => styles[i]).Distinct().ToList();
        if (lineStyles.Count == 0 || pieces.All(p => p.End == p.Start && (p.Start >= all.Length || all[p.Start] == '\n')))
            lineStyles = pieces.Count > 0 && pieces[0].Start < styles.Count ? [styles[pieces[0].Start]] : [paragraph.EndStyle];
        foreach (var style in lineStyles)
        {
            var (a, d) = Metrics(style, paragraph.Spacing);
            ascent = Math.Max(ascent, a);
            descent = Math.Max(descent, d);
        }

        var lineWidth = contentEnd - lineStart;
        var available = width - paragraph.MarginRight - lineStart;
        var offset = paragraph.Align switch
        {
            "ctr" => (available - lineWidth) / 2,
            "r" => available - lineWidth,
            _ => 0,
        };

        // Justified lines (not the last one of a paragraph) spread the free space over the spaces.
        var spaceExtra = 0.0;
        var justify = (paragraph.Align is "just" or "justLow" && !last) || paragraph.Align is "dist" or "thaiDist";
        if (justify && available > lineWidth && CountInnerSpaces(all, pieces) is var spaces and > 0)
            spaceExtra = (available - lineWidth) / spaces;

        var line = new LaidOutLine { Ascent = ascent, Descent = descent, Width = lineWidth, Align = paragraph.Align };
        if (first && paragraph.Bullet is { } bullet)
            line.Runs.Add(new PlacedRun(bullet.Text, bullet.Style, EffectiveMargin(paragraph) + paragraph.Indent + offset, bulletWidth)
            {
                Picture = bullet.Picture,
                PictureHeight = bullet.Picture != null ? bullet.PictureHeight : 0,
            });

        var shift = 0.0;
        foreach (var (start, end, x) in pieces)
        {
            if (end <= start)
                continue;
            // One placed run per style change inside the piece.
            var runStart = start;
            var runX = x + offset + shift;
            for (var i = start + 1; i <= end; i++)
            {
                if (i < end && ReferenceEquals(styles[i], styles[runStart]))
                    continue;
                var text = all[runStart..i];
                var w = Measure(all, styles, runStart, i);
                line.Runs.Add(new PlacedRun(text, styles[runStart], runX, w));
                runX += w;
                runStart = i;
            }
            shift += spaceExtra * all[start..end].Count(c => c == ' ');
        }
        return line;
    }

    /// <summary>The left margin, moved right so that the first line does not start left of the text area.</summary>
    static double EffectiveMargin(ParagraphInput paragraph) => Math.Max(paragraph.MarginLeft, -paragraph.Indent);

    static int CountInnerSpaces(string all, List<(int Start, int End, double X)> pieces)
    {
        if (pieces.Count == 0) return 0;
        var text = string.Concat(pieces.Select(p => all[p.Start..p.End])).TrimEnd(' ');
        return text.Count(c => c == ' ');
    }

    /// <summary>Width in points of all[start..end], measured run by run.</summary>
    static double Measure(string all, List<TextStyle> styles, int start, int end)
    {
        double width = 0;
        var runStart = start;
        for (var i = start + 1; i <= end; i++)
        {
            if (i < end && ReferenceEquals(styles[i], styles[runStart]))
                continue;
            var style = styles[runStart];
            var text = all[runStart..i];
            width += style.Font.Data.LayoutWidth(text, style.Size, style.Kerning, style.LetterSpacing);
            runStart = i;
        }
        return width;
    }
}
