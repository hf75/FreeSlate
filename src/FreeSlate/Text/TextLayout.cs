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
    Outline? Line = null,
    bool? RightToLeft = null,
    string? Language = null)
{
    /// <summary>The underline's own color (a:uLn's fill, else a:uFill); null follows the text.</summary>
    public RgbColor? UnderlineColor { get; init; }

    /// <summary>The underline's own width in points (a:uLn/@w); null takes the font's.</summary>
    public double? UnderlineWidth { get; init; }

    /// <summary>The underline's outline color (a:uLn's fill) and fill color (a:uFill); null: the text's color.</summary>
    public RgbColor? UnderlineLineColor { get; init; }
    public RgbColor? UnderlineFillColor { get; init; }

    /// <summary>The underline's own dash (a:uLn/a:prstDash); null follows the underline style.</summary>
    public string? UnderlineDash { get; init; }

    /// <summary>Every glyph stretched to stand from the baseline to the font's ascent (a:rPr normalizeH).</summary>
    public bool NormalizeHeight { get; init; }

    /// <summary>Digits stand upright in vertical text (a:rPr kumimoji).</summary>
    public bool UprightDigits { get; init; }

    /// <summary>The action run when the pointer enters the text during the show (a:hlinkMouseOver).</summary>
    public TextLink? HoverLink { get; init; }

    /// <summary>An object standing in the line in place of the run's one character (an equation, catalog: text.math).</summary>
    public IInlineObject? Inline { get; init; }

    /// <summary>
    /// A line break written as a character of a run's text rather than as a:br: it sizes the line it ends like the
    /// line's other characters (see TextLayout.BuildLine).
    /// </summary>
    public bool BreakInText { get; init; }

    /// <summary>
    /// Whether the run's glyphs advance as the font gives them, as Word lays out the text of its WordArt (measured with
    /// docx/local/wfw-noline: a title of 487.33 pt of advances stays on one line in boxes of 487.56 pt in textPlain and
    /// textWave1, which on PowerPoint's master units it overran), rather than each on PowerPoint's master units.
    /// </summary>
    public bool Unrounded { get; init; }

    /// <summary>A length of the run's layout (an advance, kerning, letter spacing): on PowerPoint's master units, or as it is where the run is Unrounded.</summary>
    public double Rounded(double points) => Unrounded ? points : FontData.MasterUnits(points);

    /// <summary>The advances of <paramref name="text"/>'s UTF-16 code units in points (FontData.LayoutAdvances, or as the font gives them where the run is Unrounded).</summary>
    public double[] Advances(string text, bool kerning)
    {
        if (!Unrounded)
            return Font.Data.LayoutAdvances(text, Size, kerning, LetterSpacing);
        var advances = Font.Data.Advances(text, kerning);
        for (var i = 0; i < advances.Length; i++)
            advances[i] = char.IsLowSurrogate(text[i]) ? 0 : advances[i] * Size + LetterSpacing;
        return advances;
    }

    /// <summary>The width of <paramref name="text"/> in points (FontData.LayoutWidth, or as the font gives it where the run is Unrounded).</summary>
    public double Width(string text, bool kerning, bool spacing = true) => text.Length == 0 ? 0
        : !Unrounded ? Font.Data.LayoutWidth(text, Size, kerning, spacing ? LetterSpacing : 0)
        : Font.Data.Width(text, kerning) * Size + (spacing ? LetterSpacing * text.Count(c => !char.IsLowSurrogate(c)) : 0);
}

/// <summary>An object laid out inside a line of text: its width and how far it reaches above and below the baseline (points).</summary>
public interface IInlineObject
{
    double Width { get; }
    double Ascent { get; }
    double Descent { get; }
}

/// <summary>A hyperlink of a run: a URL or slide anchor, a slide show action and a tooltip.</summary>
/// <summary>A hyperlink; Zoom: "first,last,return,duration" of a zoom object's slides (catalog: interactivity.zoom).</summary>
public sealed record TextLink(string? Href, string? Action, string? Tooltip, string? Zoom = null);

/// <summary>A piece of text with one style; Text "\n" is a line break (a:br).</summary>
public sealed record TextRun(string Text, TextStyle Style)
{
    /// <summary>
    /// The offset of its first character in its paragraph's own text (Pptx.SlideText), which its places carry (see
    /// PlacesItem); -1 where it has none.
    /// </summary>
    public int Source { get; init; } = -1;

    /// <summary>Whether all its characters stand for the one character at Source: a field's text, an equation.</summary>
    public bool Atomic { get; init; }
}

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
    /// <summary>
    /// Picture bullets are 0.7 of the text's size high, whatever its font, and sit on the baseline (measured with
    /// PowerPoint's PDF: LibreOffice's tdf114913 in Calibri, Arial and Times New Roman at 14 to 44 pt, generated/lists
    /// in Aptos; a picture wider than the indent pushes the text on by its width, 0.7 of the size times its proportions).
    /// </summary>
    public double PictureHeight => Style.Size * 0.7;

    /// <summary>
    /// What follows Text in fonts or sizes of its own: a number in another script draws its punctuation in the text's
    /// font ("三" in MS Gothic, then "." in Aptos) or its Thai characters larger (see TextRenderer.NumberPieces).
    /// </summary>
    public IReadOnlyList<(string Text, TextStyle Style)> After { get; init; } = [];

    /// <summary>The widths of the pieces After in points.</summary>
    public IEnumerable<double> AfterWidths => After.Select(p => p.Style.Font.Data.LayoutWidth(p.Text, p.Style.Size, p.Style.Kerning));
}

public sealed class ParagraphInput
{
    public required List<TextRun> Runs { get; init; }

    /// <summary>Style of the paragraph mark; gives empty paragraphs their height.</summary>
    public required TextStyle EndStyle { get; init; }

    /// <summary>
    /// The paragraph mark as it weighs in where its last line's baseline sits (see LineMetrics): its own formatting
    /// (a:endParaRPr), else the last run's in its Latin font; null without runs.
    /// </summary>
    public TextStyle? MarkStyle { get; init; }

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

    /// <summary>
    /// The paragraph's tab stops (a:tabLst): positions in points from the text area's left edge with their alignment
    /// (l, ctr, r, dec), in ascending order; past the last one the default stops continue.
    /// </summary>
    public IReadOnlyList<(double Position, string Align)> TabStops { get; init; } = [];

    /// <summary>How runs of different sizes line up (a:pPr/@fontAlgn): base (and auto), t, ctr or b.</summary>
    public string FontAlign { get; init; } = "auto";

    /// <summary>A right-to-left paragraph (a:pPr/@rtl): its margins, indent and bullet start on the right.</summary>
    public bool RightToLeft { get; init; }

    /// <summary>The East Asian line breaking rules (a:pPr/@eaLnBrk, on by default).</summary>
    public bool Kinsoku { get; init; } = true;

    /// <summary>Latin words may break anywhere to fill a line (a:pPr/@latinLnBrk).</summary>
    public bool LatinBreak { get; init; }

    /// <summary>Commas and full stops may hang past the end of a line (a:pPr/@hangingPunct, with the kinsoku rules).</summary>
    public bool HangingPunctuation { get; init; }

    /// <summary>The paragraph's id in its document, which the places of its lines carry (see PlacesItem); null for none.</summary>
    public int? Id { get; init; }

    /// <summary>The offset of the paragraph's mark in its own text (see TextRun.Source): the text's length.</summary>
    public int Mark { get; init; }
}

/// <summary>A run placed on a line: X from the left edge of the text area, in points.</summary>
public sealed record PlacedRun(string Text, TextStyle Style, double X, double Width)
{
    /// <summary>The picture of a picture bullet and its height in points.</summary>
    public string? Picture { get; init; }

    /// <summary>An East Asian character that stays upright in vertical text.</summary>
    public bool Upright { get; init; }

    public double PictureHeight { get; init; }

    /// <summary>How far the run is raised above the line's baseline (points): fontAlgn t, ctr and b.</summary>
    public double Lift { get; init; }

    /// <summary>Text at an odd bidi level, shaped and drawn right to left.</summary>
    public bool RightToLeft { get; init; }

    /// <summary>Where the run's text starts in its paragraph's text; -1 for bullets.</summary>
    public int Start { get; init; } = -1;

    /// <summary>Whether its last character is an ellipsis the layout added (bodyPr vertOverflow ellipsis).</summary>
    public bool Ellipsis { get; init; }
}

public sealed class LaidOutLine
{
    /// <summary>Index of the paragraph the line belongs to (animations address paragraphs).</summary>
    public int Paragraph { get; set; }

    /// <summary>The text of the paragraph the line belongs to (caps applied), which the runs' starts index.</summary>
    public string ParagraphText { get; set; } = "";

    /// <summary>Baseline from the top of the text area, in points.</summary>
    public double Baseline { get; set; }
    public double Ascent { get; init; }
    public double Descent { get; init; }

    /// <summary>The descent the line would have at single spacing, which its glyphs reach below its baseline (see LaidOutText.Bound).</summary>
    public double SingleDescent { get; init; }
    public double Width { get; init; }

    /// <summary>The paragraph's alignment (l, ctr, r, just …).</summary>
    public string Align { get; init; } = "l";

    /// <summary>Where the line's text would start without its alignment, from the left of the text area (points).</summary>
    public double Start { get; init; }

    public List<PlacedRun> Runs { get; } = [];

    /// <summary>The paragraph's id that its places carry (see ParagraphInput.Id), null for none.</summary>
    public int? Id { get; set; }

    /// <summary>
    /// For every character of ParagraphText the offset in its paragraph's own text that it shows (see TextRun.Source), -1
    /// for one that shows none.
    /// </summary>
    public int[] Sources { get; set; } = [];

    /// <summary>Whether the line is its paragraph's last, which ends with the paragraph's mark (at Mark in its own text).</summary>
    public bool Last { get; set; }

    public int Mark { get; set; }

    /// <summary>The index in ParagraphText of the line break (a:br) that ends the line, -1 where none does.</summary>
    public int Break { get; set; } = -1;

    /// <summary>
    /// The line's tabs, which draw nothing: each one's index in ParagraphText and where it reaches from and to, from the
    /// left of the text area (points), in reading order.
    /// </summary>
    public List<(int Index, double From, double To)> Tabs { get; } = [];

    /// <summary>
    /// Where the line's text begins in reading order with its alignment, from the left of the text area (points): where
    /// the cursor of an empty line stands.
    /// </summary>
    public double Origin { get; set; }

    /// <summary>Moves what the line places across by <paramref name="dx"/> points: its runs, its tabs and its origin.</summary>
    public void Shift(double dx)
    {
        var runs = Runs.Select(r => r with { X = r.X + dx }).ToList();
        Runs.Clear();
        Runs.AddRange(runs);
        for (var i = 0; i < Tabs.Count; i++)
            Tabs[i] = (Tabs[i].Index, Tabs[i].From + dx, Tabs[i].To + dx);
        Origin += dx;
    }

    /// <summary>A copy of the line at another baseline, its runs moved across by <paramref name="dx"/> points (see Shift).</summary>
    public LaidOutLine Moved(double dx, double baseline)
    {
        var moved = new LaidOutLine
        {
            Paragraph = Paragraph, ParagraphText = ParagraphText, Baseline = baseline, Ascent = Ascent, Descent = Descent, SingleDescent = SingleDescent, Width = Width, Align = Align,
            Start = Start, Id = Id, Sources = Sources, Last = Last, Mark = Mark, Break = Break, Origin = Origin,
        };
        moved.Runs.AddRange(Runs);
        moved.Tabs.AddRange(Tabs);
        moved.Shift(dx);
        return moved;
    }
}

public sealed class LaidOutText
{
    public List<LaidOutLine> Lines { get; } = [];

    /// <summary>Height of the text block from the top of the first line to the bottom of the last, in points.</summary>
    public double Height { get; set; }

    /// <summary>
    /// The text block's height as PowerPoint measures it to fit text to its shape (TextRange.BoundHeight): to the last
    /// line's baseline and the descent it has at single spacing (measured with PowerPoint 365's object model: Aptos,
    /// Calibri, Arial and Times New Roman at 12, 24 and 48 pt, at 80, 100 and 150 % line spacing, all to 0.001 pt),
    /// where Height takes the descent of its spacing.
    /// </summary>
    public double Bound { get; set; }
}

/// <summary>
/// Lays out paragraphs like PowerPoint: own line breaking with shaped widths and PowerPoint's vertical line
/// model, measured with the text-metrics calibration deck (catalog: engine.textLayout):
/// <list type="bullet">
/// <item>Single line height S = 1.2 × font size; multiples scale it; exact spacing is rounded to whole points.</item>
/// <item>At single spacing the baseline sits at S × ascent ratio of the font. Above single spacing it sits at
/// H × min(0.75, ascent ratio); below, at max(S × ratio − (S − H), H × min(0.75, ratio)).</item>
/// <item>A line is as high as its largest font's line; its baseline divides that height as the largest ascent and
/// descent of its runs divide theirs (see <see cref="LineMetrics"/>); bullets do not count, an empty line is as high as
/// its break.</item>
/// <item>Space before is ignored for the first paragraph, space after for the last, unless the body asks for them
/// (bodyPr spcFirstLastPara); percentages refer to 1.2 × font size.</item>
/// <item>Widths add up glyph advances rounded to PowerPoint's master units (1/8 point), see
/// <see cref="FontData.LayoutAdvances"/>.</item>
/// </list>
/// </summary>
public static class TextLayout
{
    /// <param name="trailingSpaces">Whether the spaces that end a line take room in it, which they do in text that does
    /// not wrap (see BreakLines).</param>
    public static LaidOutText Layout(IReadOnlyList<ParagraphInput> paragraphs, double width, bool wrap, bool spaceFirstLast = false,
        bool trailingSpaces = false)
    {
        var result = new LaidOutText();
        double cursor = 0;
        double pendingAfter = 0;

        for (var p = 0; p < paragraphs.Count; p++)
        {
            var paragraph = paragraphs[p];
            var firstSize = paragraph.Runs.FirstOrDefault(r => r.Text != "\n")?.Style.Size ?? paragraph.EndStyle.Size;
            if (p > 0 || spaceFirstLast)
                cursor += pendingAfter + paragraph.SpaceBefore.Points(firstSize);
            pendingAfter = paragraph.SpaceAfter.Points(firstSize);

            foreach (var line in BreakLines(paragraph, width, wrap, trailingSpaces))
            {
                line.Paragraph = p;
                line.Id = paragraph.Id;
                line.Baseline = cursor + line.Ascent;
                cursor = line.Baseline + line.Descent;
                result.Lines.Add(line);
            }
        }
        result.Height = cursor + (spaceFirstLast ? pendingAfter : 0);
        result.Bound = result.Lines.Count > 0 ? result.Height - result.Lines[^1].Descent + result.Lines[^1].SingleDescent : result.Height;
        return result;
    }

    /// <summary>Ascent and descent of a run per PowerPoint's line model.</summary>
    public static (double Ascent, double Descent) Metrics(TextStyle style, LineSpacing spacing) =>
        Metrics(style.Size, style.Font.Data.AscentRatio, spacing);

    /// <summary>
    /// Ascent and descent of a line of text in <paramref name="styles"/> per PowerPoint's line model (catalog:
    /// text.spacing): in fonts of one ascent ratio the largest ascent and descent of its runs; in fonts of different
    /// ones as high as the line of its largest font, its baseline dividing that height as the largest ascent and descent
    /// of its fonts at single spacing divide theirs, the line break ending it or the paragraph's mark after it
    /// (<paramref name="terminator"/>, already at the size of the run before it) among them (measured with
    /// local/mixfonts-a to -d in PowerPoint's PDF: fonts whose ascents and descents differ, at one size and at two, three
    /// in a line, at 80, 100 and 150 % and exactly 36 pt, breaks and marks of fonts and sizes of their own, Chinese and
    /// Latin text, all within 0.1 pt; a break's or mark's own size counts for nothing).
    /// </summary>
    static (double Ascent, double Descent) LineMetrics(List<TextStyle> styles, TextStyle? terminator, LineSpacing spacing)
    {
        var fonts = terminator is { } end ? [.. styles, end] : styles;
        var ratio = fonts[0].Font.Data.AscentRatio;
        if (fonts.All(s => s.Font.Data.AscentRatio == ratio))
        {
            double ascent = 0, descent = 0;
            foreach (var style in styles)
            {
                var (a, d) = Metrics(style, spacing);
                ascent = Math.Max(ascent, a);
                descent = Math.Max(descent, d);
            }
            return (ascent, descent);
        }
        var (above, below) = (0.0, 0.0);
        foreach (var style in fonts)
        {
            var single = 1.2 * style.Size;
            above = Math.Max(above, single * style.Font.Data.AscentRatio);
            below = Math.Max(below, single * (1 - style.Font.Data.AscentRatio));
        }
        return Metrics(styles.Max(s => s.Size), above / (above + below), spacing);
    }

    /// <summary>Ascent and descent of a line as high as text of <paramref name="size"/> whose baseline divides it at <paramref name="ratio"/>.</summary>
    static (double Ascent, double Descent) Metrics(double size, double ratio, LineSpacing spacing)
    {
        var single = 1.2 * size;
        var height = spacing.Exact ? Math.Round(spacing.Value) : single * Math.Max(0.01, spacing.Value);
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

    /// <param name="trailingSpaces">
    /// Whether the spaces ending a line count in its width and so in its alignment. Lines that wrap hang them past
    /// their end, the paragraph's last line too (measured: LibreOffice's oox/tdf139618_ThemeColorTransparency, "Happy "
    /// centered in 96 pt as "Happy"); text that does not wrap keeps them, before a line break too (measured: centered
    /// lines ending in a space half a space further left than without it, in POI's
    /// aascu.org_workarea_downloadasset.aspx_id=5864 "Why Such Urgency " at 44 pt 9 px, the title of slide 7 at 20 pt 4
    /// px, Open XML SDK's o09_Performance_typical "Shaded " and "Workgroup " before a break in Arial bold 18 and 20 pt
    /// 3.5 px, POI's alterman_security "FPKI Medium &amp; " 3.3 px).
    /// </param>
    static List<LaidOutLine> BreakLines(ParagraphInput paragraph, double width, bool wrap, bool trailingSpaces = false)
    {
        // Flatten the runs into one string with a style per character (caps applied, which keeps the length) and the
        // offset each character shows in its paragraph's own text.
        var chars = new System.Text.StringBuilder();
        var styles = new List<TextStyle>();
        var sources = new List<int>();
        foreach (var run in paragraph.Runs)
        {
            var text = run.Style.AllCaps ? run.Text.ToUpperInvariant() : run.Text;
            chars.Append(text);
            styles.AddRange(Enumerable.Repeat(run.Style, text.Length));
            for (var i = 0; i < text.Length; i++)
                sources.Add(run.Source < 0 ? -1 : run.Atomic ? run.Source : run.Source + i);
        }
        var all = chars.ToString();
        var offsets = sources.ToArray();
        var breaks = LineBreaker.Opportunities(all, paragraph.Kinsoku);
        // Bidi levels of the paragraph (catalog: text.rtl); neutrals the rules leave open follow their run's language.
        var levels = Bidi.Levels(all, paragraph.RightToLeft, i => i < styles.Count ? styles[i].RightToLeft : null);

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
            s.Width = Measure(all, styles, s.Start, s.End) + KerningAcross(all, styles, s.End);
            var trailing = s.End;
            while (trailing > s.Start && all[trailing - 1] == ' ') trailing--;
            s.TrailingWidth = s.Width - Measure(all, styles, s.Start, trailing);
        }

        var lines = new List<LaidOutLine>();
        var bulletWidth = paragraph.Bullet is { } b
            ? b.Picture != null ? b.PictureHeight * b.PictureAspect : b.Style.Width(b.Text, b.Style.Kerning, spacing: false)
            : 0;
        var limit = width - paragraph.MarginRight;
        // A hanging indent reaching left of the text area moves the whole paragraph right: the first line starts
        // at the edge (measured: marL 0 with indent -27 pt puts the bullet at 0 and the text at 27 pt).
        var marginLeft = EffectiveMargin(paragraph);
        var firstStart = marginLeft + paragraph.Indent;
        // A number in two fonts or sizes is as wide as its pieces together (Bullet.After).
        bulletWidth += paragraph.Bullet?.AfterWidths.Sum() ?? 0;
        var textStart = paragraph.Bullet != null ? Math.Max(Math.Max(marginLeft, firstStart), BulletAt(paragraph) + bulletWidth) : firstStart;

        var current = new List<(int Start, int End, double X)>();
        // The tabs on the current line, where each reaches from and to before the line is aligned.
        var lineTabs = new List<(int Index, double From, double To)>();
        var x = textStart;
        var lineStart = textStart;
        var lastContentEnd = textStart;
        var first = true;
        var endsWithBreak = false;

        void Finish(bool lastOfParagraph, int lineBreak = -1)
        {
            var line = BuildLine(paragraph, all, styles, levels, current, lineStart, lastContentEnd, width, first, lastOfParagraph, bulletWidth, lineTabs);
            line.ParagraphText = all;
            line.Sources = offsets;
            line.Last = lastOfParagraph;
            line.Mark = paragraph.Mark;
            line.Break = lineBreak;
            lines.Add(line);
            current = [];
            lineTabs = [];
            first = false;
            x = lineStart = lastContentEnd = marginLeft;
        }

        // A tab from the position it stands at to its stop.
        void Tab(Segment tab)
        {
            var from = x;
            x = TabStop(paragraph, all, styles, tab.End, x);
            lineTabs.Add((tab.Start, from, x));
        }

        // A word longer than the room left, broken between characters. Its trailing spaces hang at the end of its last
        // piece, as they do after any word (measured: "き 2024" in mongolianVert columns one character long takes no
        // column for the space). A line holds at least one character, unless a tab stands before them (tab: its index).
        void BreakBetweenCharacters(Segment segment, int? tab)
        {
            var start = segment.Start;
            var wordEnd = segment.End;
            while (wordEnd > start && all[wordEnd - 1] == ' ') wordEnd--;
            while (start < segment.End)
            {
                var end = start;
                while (end < wordEnd && x + Measure(all, styles, start, end + 1) <= limit + 0.01) end++;
                if (end == start && tab == null) end = start + 1;
                if (end >= wordEnd) end = segment.End;
                if (end > start)
                {
                    current.Add((start, end, x));
                    lastContentEnd = x + Measure(all, styles, start, Math.Min(end, wordEnd));
                    x += Measure(all, styles, start, end);
                }
                else
                    current.Add((tab!.Value, tab.Value, x));   // a line of only the tab keeps the tab's style
                tab = null;
                if (end < segment.End) Finish(false);
                start = end;
            }
        }

        // The tabs since the line's last word: a tab binds to the word after it.
        var tabs = new List<Segment>();
        foreach (var segment in segments)
        {
            endsWithBreak = false;
            if (segment.IsBreak)
            {
                current.Add((segment.Start, segment.Start, x));   // keeps the break's style for empty lines
                Finish(false, segment.Start);
                endsWithBreak = true;
                tabs.Clear();
                continue;
            }
            if (segment.IsTab)
            {
                tabs.Add(segment);
                Tab(segment);
                continue;
            }

            var content = segment.Width - segment.TrailingWidth;
            // Hanging punctuation stays on the line even past its end (measured: 、 and 。 hang, the character before
            // them is not pushed to the next line).
            var hanging = paragraph.HangingPunctuation && paragraph.Kinsoku ? HangingWidth(all, styles, segment) : 0;
            // A word that does not fit after a tab takes the tab to the next line, and a tab that starts a line keeps
            // what fits of the word after its stop, possibly nothing (measured with stops near the end of a 385 pt
            // line: "Text⇥after" puts the tab on a line of its own and "after" on the next, a stop 35 pt earlier
            // keeps "afte" after it; a stop past the line's end leaves the tab alone).
            if (wrap && tabs.Count > 0 && x + content - hanging > limit + 0.01)
            {
                if (current.Count > 0)
                {
                    lineTabs.RemoveRange(lineTabs.Count - tabs.Count, tabs.Count);
                    Finish(false);
                    foreach (var tab in tabs)
                        Tab(tab);
                }
                if (x + content - hanging > limit + 0.01)
                {
                    BreakBetweenCharacters(segment, tabs[^1].Start);
                    tabs.Clear();
                    continue;
                }
            }
            tabs.Clear();
            // latinLnBrk: a Latin word that does not fit is broken to fill the line (measured).
            var breakWord = paragraph.LatinBreak && current.Count > 0 && x + content > limit + 0.01 && IsLatinWord(all, segment);
            if (wrap && current.Count > 0 && x + content - hanging > limit + 0.01 && !breakWord)
                Finish(false);

            if (wrap && x + content - hanging > limit + 0.01 && (current.Count == 0 || breakWord) && segment.End - segment.Start > 1)
            {
                BreakBetweenCharacters(segment, null);
                continue;
            }

            current.Add((segment.Start, segment.End, x));
            lastContentEnd = x + (trailingSpaces ? segment.Width : content);
            x += segment.Width;
        }
        // A last line of nothing but tabs keeps the first tab's style, as a line of only a tab does where a word moves on
        // (measured: poi/ae.ac.uaeu.faculty_nafaachbili_GeomLec1, slide 4: a paragraph of one tab in 28 pt between lines
        // of 28 pt takes their pitch, not that of its mark's 18 pt).
        if (current.Count == 0 && tabs.Count > 0)
            current.Add((tabs[0].Start, tabs[0].Start, x));
        if (current.Count > 0 || lines.Count == 0 || endsWithBreak)
            Finish(true);
        return lines;
    }

    /// <param name="tabs">The line's tabs, where each reaches from and to before the line is aligned (see LaidOutLine.Tabs).</param>
    static LaidOutLine BuildLine(ParagraphInput paragraph, string all, List<TextStyle> styles, byte[] levels,
        List<(int Start, int End, double X)> pieces, double lineStart, double contentEnd, double width,
        bool first, bool last, double bulletWidth, List<(int Index, double From, double To)> tabs)
    {
        // Vertical metrics (see LineMetrics). The line break ending it sizes the line only when it is otherwise empty
        // (measured: a 60 pt break after 20 pt text leaves the line 24 pt high, a 60 pt break alone makes it 72 pt),
        // unless it is a character of a run's text, which counts as the line's others do (measured: LibreOffice's
        // n828390_2, "Linux" at 20 pt ended by a 24 pt run of a newline); an empty last line takes the paragraph mark's.
        var characters = pieces.Where(p => p.End > p.Start).SelectMany(p => Enumerable.Range(p.Start, p.End - p.Start))
            .Where(i => i < styles.Count).ToList();
        var lineStyles = characters.Select(i => styles[i]).Distinct().ToList();
        double ascent, descent, singleDescent;
        if (lineStyles.Count == 0)
        {
            lineStyles = pieces.Count > 0 && pieces[0].Start < styles.Count ? [styles[pieces[0].Start]] : [paragraph.EndStyle];
            (ascent, descent) = Metrics(lineStyles[0], paragraph.Spacing);
            (_, singleDescent) = Metrics(lineStyles[0], LineSpacing.Single);
        }
        else
        {
            var terminator = pieces[^1] is var (breakAt, breakEnd, _) && breakEnd == breakAt && breakAt < all.Length && all[breakAt] == '\n'
                ? styles[breakAt]
                : last ? paragraph.MarkStyle : null;
            if (terminator is { BreakInText: true } character)
                (lineStyles, terminator) = ([.. lineStyles, character], null);
            var ending = terminator is { } end ? end with { Size = styles[characters[^1]].Size } : null;
            (ascent, descent) = LineMetrics(lineStyles, ending, paragraph.Spacing);
            (_, singleDescent) = LineMetrics(lineStyles, ending, LineSpacing.Single);
        }
        // An equation in the line makes room for itself.
        foreach (var style in lineStyles)
        {
            if (style.Inline is { } inline)
            {
                ascent = Math.Max(ascent, inline.Ascent);
                descent = Math.Max(descent, inline.Descent);
            }
        }

        if (paragraph.RightToLeft || pieces.Any(p => Enumerable.Range(p.Start, p.End - p.Start).Any(i => i < levels.Length && levels[i] > 0)))
            return BuildBidiLine(paragraph, all, styles, levels, pieces, lineStart, width, first, last, bulletWidth, ascent, descent, singleDescent);

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

        var line = new LaidOutLine
        {
            Ascent = ascent, Descent = descent, SingleDescent = singleDescent, Width = lineWidth, Align = paragraph.Align, Start = lineStart, Origin = lineStart + offset,
        };
        if (first && paragraph.Bullet is { } bullet)
            line.Runs.AddRange(BulletRuns(bullet, BulletAt(paragraph) + offset, bulletWidth, false));

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
                line.Runs.Add(new PlacedRun(text, styles[runStart], runX, w) { Lift = Lift(styles[runStart], paragraph, ascent, descent), Start = runStart });
                runX += w;
                runStart = i;
            }
            shift += spaceExtra * all[start..end].Count(c => c == ' ');
        }
        // A tab moves with the line's alignment and with the room justification gave the spaces before it.
        foreach (var (index, from, to) in tabs)
        {
            var widened = spaceExtra * pieces.Where(p => p.End <= index).Sum(p => all[p.Start..p.End].Count(c => c == ' '));
            line.Tabs.Add((index, from + offset + widened, to + offset + widened));
        }
        return line;
    }

    /// <summary>
    /// A line with right-to-left text (catalog: text.rtl): the pieces split where the style or the bidi level
    /// changes, put in visual order (L2) and placed from the left. A right-to-left paragraph starts on the right: its
    /// left margin, indent and bullet are measured from the right edge, alignment r is its start and l its end
    /// (measured); tabs count as their width only.
    /// </summary>
    static LaidOutLine BuildBidiLine(ParagraphInput paragraph, string all, List<TextStyle> styles, byte[] levels,
        List<(int Start, int End, double X)> pieces, double lineStart, double width, bool first, bool last, double bulletWidth,
        double ascent, double descent, double singleDescent)
    {
        var justify = (paragraph.Align is "just" or "justLow" && !last) || paragraph.Align is "dist" or "thaiDist";
        // Logical items without the line's trailing spaces.
        var lastVisible = -1;
        foreach (var (start, end, _) in pieces)
        {
            for (var i = start; i < end; i++)
            {
                if (all[i] is not (' ' or '\n'))
                    lastVisible = Math.Max(lastVisible, i);
            }
        }
        var items = new List<(int Start, int End)>();
        foreach (var (start, end, _) in pieces)
        {
            var stop = Math.Min(end, lastVisible + 1);
            var runStart = start;
            for (var i = start + 1; i <= stop; i++)
            {
                var split = i == stop || !ReferenceEquals(styles[i], styles[runStart]) || levels[i] != levels[runStart]
                            || justify && (all[i] == ' ' || all[i - 1] == ' ');
                if (!split)
                    continue;
                if (i > runStart)
                    items.Add((runStart, i));
                runStart = i;
            }
        }
        var widths = items.Select(item => Measure(all, styles, item.Start, item.End)).ToList();
        var lineWidth = widths.Sum();
        var available = width - paragraph.MarginRight - lineStart;
        var spaces = justify ? items.Count(item => all[item.Start] == ' ') : 0;
        var spaceExtra = spaces > 0 && available > lineWidth ? (available - lineWidth) / spaces : 0;
        var used = lineWidth + spaceExtra * spaces;
        double x;
        if (paragraph.RightToLeft)
        {
            var (left, right) = (paragraph.MarginRight, width - lineStart);
            x = paragraph.Align switch
            {
                "l" => left,
                "ctr" => left + (right - left - used) / 2,
                _ when spaceExtra > 0 => left,
                _ => right - used,
            };
        }
        else
        {
            x = lineStart + paragraph.Align switch { "ctr" => (available - used) / 2, "r" => available - used, _ => 0 };
        }

        var line = new LaidOutLine
        {
            Ascent = ascent, Descent = descent, SingleDescent = singleDescent, Width = used, Align = paragraph.Align, Start = lineStart, Origin = paragraph.RightToLeft ? x + used : x,
        };
        if (first && paragraph.Bullet is { } bullet)
        {
            var bulletAt = BulletAt(paragraph);
            var bulletX = paragraph.RightToLeft ? width - bulletAt - bulletWidth : bulletAt + (x - lineStart);
            line.Runs.AddRange(BulletRuns(bullet, bulletX, bulletWidth, paragraph.RightToLeft));
        }
        foreach (var k in Bidi.Reorder(items.Select(item => levels[item.Start]).ToList()))
        {
            var (start, end) = items[k];
            line.Runs.Add(new PlacedRun(all[start..end], styles[start], x, widths[k])
            {
                Lift = Lift(styles[start], paragraph, ascent, descent),
                RightToLeft = levels[start] % 2 == 1,
                Start = start,
            });
            x += widths[k] + (justify && all[start] == ' ' ? spaceExtra : 0);
        }
        return line;
    }

    /// <summary>
    /// The runs of a bullet <paramref name="width"/> wide from x (points): its text, then the pieces after it in fonts of
    /// their own (Bullet.After). A bullet of a right-to-left paragraph reads right to left too ("1." shows as ".1",
    /// measured): its pieces in reverse order, each in visual order.
    /// </summary>
    static IEnumerable<PlacedRun> BulletRuns(Bullet bullet, double x, double width, bool rightToLeft)
    {
        if (bullet.After.Count == 0)
        {
            yield return new PlacedRun(rightToLeft ? Visual(bullet.Text) : bullet.Text, bullet.Style, x, width)
            {
                Picture = bullet.Picture,
                PictureHeight = bullet.Picture != null ? bullet.PictureHeight : 0,
            };
            yield break;
        }
        var after = bullet.After.Zip(bullet.AfterWidths, (p, w) => (p.Text, p.Style, Width: w)).ToList();
        List<(string Text, TextStyle Style, double Width)> pieces = [(bullet.Text, bullet.Style, width - after.Sum(p => p.Width)), .. after];
        if (rightToLeft)
            pieces.Reverse();
        foreach (var (text, style, w) in pieces)
        {
            yield return new PlacedRun(rightToLeft ? Visual(text) : text, style, x, w);
            x += w;
        }
    }

    /// <summary>A short right-to-left text (a bullet) in visual order, its brackets mirrored.</summary>
    static string Visual(string text)
    {
        var levels = Bidi.Levels(text, true);
        var order = Bidi.Reorder(levels);
        return string.Concat(order.Select(i => levels[i] % 2 == 1 ? Mirror(text[i]) : text[i]));
    }

    static char Mirror(char c) => c switch
    {
        '(' => ')', ')' => '(', '[' => ']', ']' => '[', '{' => '}', '}' => '{', '<' => '>', '>' => '<',
        '\u00AB' => '\u00BB', '\u00BB' => '\u00AB', '\u2039' => '\u203A', '\u203A' => '\u2039', _ => c,
    };

    /// <summary>
    /// How far fontAlgn raises a run above the baseline: t lines up the tops of the runs with the line's, b their
    /// bottoms, ctr their middles (measured); base and auto keep the baselines.
    /// </summary>
    static double Lift(TextStyle style, ParagraphInput paragraph, double lineAscent, double lineDescent)
    {
        if (paragraph.FontAlign is not ("t" or "ctr" or "b"))
            return 0;
        var (ascent, descent) = Metrics(style, paragraph.Spacing);
        return paragraph.FontAlign switch
        {
            "t" => lineAscent - ascent,
            "b" => descent - lineDescent,
            _ => (lineAscent - lineDescent - ascent + descent) / 2,
        };
    }

    /// <summary>
    /// The line cut to fit <paramref name="limit"/> points with an ellipsis: whole words come off its end until the
    /// text and the ellipsis fit (bodyPr vertOverflow ellipsis on the last line that fits).
    /// </summary>
    public static void Ellipsize(LaidOutLine line, double limit)
    {
        var runs = line.Runs.Where(r => r.Picture == null).ToList();
        if (runs.Count == 0)
            return;
        var last = runs[^1];
        var text = string.Concat(runs.Select(r => r.Text)).TrimEnd();
        double Width(string value) => last.Style.Width(value, last.Style.Kerning);
        var start = runs[0].X;
        var cut = text;
        while (cut.Length > 0 && start + Width(cut + "\u2026") > limit + 0.01)
        {
            var space = cut.TrimEnd().LastIndexOf(' ');
            cut = space > 0 ? cut[..space].TrimEnd() : cut[..^1];
        }
        foreach (var run in runs)
            line.Runs.Remove(run);
        // The shorter line keeps its paragraph's alignment.
        var shorter = line.Width - Width(cut + "\u2026");
        start += line.Align switch { "ctr" => shorter / 2, "r" => shorter, _ => 0 };
        line.Runs.Add(new PlacedRun(cut + "\u2026", last.Style, start, Width(cut + "\u2026")) { Start = runs[0].Start, Ellipsis = true });
        line.Tabs.Clear();
        line.Break = -1;
        line.Origin = start;
    }

    /// <summary>
    /// anchorCtr: the smallest box around the text is centered in the text area <paramref name="width"/> points wide,
    /// the paragraphs keep their alignment within it (measured on text boxes and table cells); a box wider than the
    /// area reaches out on both sides (measured: LibreOffice's oox/tdf151008_eaVertAnchor, columns of vertical text
    /// longer than their box, centered on it).
    /// </summary>
    public static void CenterBlock(LaidOutText layout, double width)
    {
        if (layout.Lines.Count == 0)
            return;
        var free = (width - layout.Lines.Max(l => l.Start + l.Width)) / 2;
        if (Math.Abs(free) <= 0.005)
            return;
        foreach (var line in layout.Lines)
        {
            var shift = line.Align switch { "ctr" => 0, "r" => -free, _ => free };
            if (shift == 0)
                continue;
            line.Shift(shift);
        }
    }

    /// <summary>The width of the hanging punctuation at the end of a segment (before its trailing spaces).</summary>
    static double HangingWidth(string all, List<TextStyle> styles, Segment segment)
    {
        var end = segment.End;
        while (end > segment.Start && all[end - 1] == ' ') end--;
        var start = end;
        while (start > segment.Start && LineBreaker.Hanging.Contains(all[start - 1])) start--;
        return start < end ? Measure(all, styles, start, end) : 0;
    }

    /// <summary>Whether a segment is a Latin word (letters and digits of non-East Asian scripts).</summary>
    static bool IsLatinWord(string all, Segment segment)
    {
        var any = false;
        for (var i = segment.Start; i < segment.End; i++)
        {
            var c = all[i];
            if (c == ' ')
                continue;
            if (!char.IsLetterOrDigit(c) || LineBreaker.IsIdeographic(c) || c > '\u024F')
                return false;
            any = true;
        }
        return any;
    }

    /// <summary>The left margin, moved right so that the first line does not start left of the text area.</summary>
    static double EffectiveMargin(ParagraphInput paragraph) => Math.Max(paragraph.MarginLeft, -paragraph.Indent);

    /// <summary>
    /// Where a bullet stands: at the first line's start for a hanging indent, at the margin for a first line
    /// indented further, whose text then starts at the indent (measured with the object model: marL 0 and indent
    /// 27 pt start the text at 27 pt, marL 36 and indent 0 right after the bullet; LibreOffice's bullet-indent).
    /// </summary>
    static double BulletAt(ParagraphInput paragraph) => EffectiveMargin(paragraph) + Math.Min(0, paragraph.Indent);

    static int CountInnerSpaces(string all, List<(int Start, int End, double X)> pieces)
    {
        if (pieces.Count == 0) return 0;
        var text = string.Concat(pieces.Select(p => all[p.Start..p.End])).TrimEnd(' ');
        return text.Count(c => c == ' ');
    }

    /// <summary>Width in points of all[start..end], measured run by run.</summary>
    /// <summary>
    /// Where the text after a tab starts (catalog: text.tabs): at the next custom stop past the current position,
    /// left-aligned there, centered on it, ending at it (r) or with its decimal point at it (dec; text without one ends
    /// at the stop), never before the current position; past the last custom stop at the next default stop
    /// (multiples of defTabSz from the text area's left edge; measured with generated/text-tabs). A hanging first line
    /// stops at the paragraph's margin first.
    /// </summary>
    static double TabStop(ParagraphInput paragraph, string all, List<TextStyle> styles, int after, double x)
    {
        // A hanging first line has a stop at the paragraph's margin (measured: marL 60 pt, indent -40 pt).
        var margin = EffectiveMargin(paragraph);
        if (paragraph.Indent < 0 && x < margin - 1e-6)
            return margin;
        foreach (var (position, align) in paragraph.TabStops)
        {
            if (position <= x + 1e-6)
                continue;
            if (align == "l")
                return position;
            // The text the stop aligns: up to the next tab or line break.
            var end = after;
            while (end < all.Length && all[end] is not ('\t' or '\n'))
                end++;
            var width = Measure(all, styles, after, end);
            var start = align switch
            {
                "ctr" => position - width / 2,
                "dec" => all.IndexOf('.', after, end - after) is var point and >= 0 ? position - Measure(all, styles, after, point) : position - width,
                _ => position - width,
            };
            return Math.Max(x, start);
        }
        var tab = paragraph.DefaultTabSize <= 0 ? 72 : paragraph.DefaultTabSize;
        return (Math.Floor(x / tab + 1e-9) + 1) * tab;
    }

    /// <summary>
    /// The kerning between the characters before and at <paramref name="at"/> when both belong to one format run:
    /// PowerPoint kerns across break opportunities too (measured: the pair "-J" of "IT-Jobs").
    /// </summary>
    static double KerningAcross(string all, List<TextStyle> styles, int at)
    {
        if (at <= 0 || at >= all.Length || !ReferenceEquals(styles[at - 1], styles[at]) || !styles[at].Kerning || styles[at].Inline != null
            || char.IsSurrogate(all[at - 1]) || char.IsSurrogate(all[at]) || all[at - 1] is '\n' or '\t' || all[at] is '\n' or '\t')
            return 0;
        var style = styles[at];
        return style.Advances(all.Substring(at - 1, 2), true)[0] - style.Advances(all.Substring(at - 1, 1), true)[0];
    }

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
            width += style.Inline is { } inline ? inline.Width * text.Length : style.Width(text, style.Kerning);
            runStart = i;
        }
        return width;
    }
}
