using FreeSlate.Docx;
using FreeSlate.Docx.Layout;
using FreeSlate.Opc;

namespace Fidelity.Word;

/// <summary>
/// Compares our layout of a document with Word's line by line: page, baseline, start and end of every line of Word's
/// PDF export against the line with the same text in our layout. Line breaks that differ show as unmatched lines.
/// </summary>
public static class WordLines
{
    public static void Compare(IEnumerable<string> documents, bool all)
    {
        foreach (var document in documents)
        {
            // Word writes trailing spaces as glyphs; the line ends at its last visible glyph. Lines of columns side by
            // side are told apart by the gap between them.
            var word = new PdfText(References.DocumentPdf(document)).Lines()
                .SelectMany(SplitAtGaps)
                .Select(l => (Page: l[0].Page, Y: l[0].Y, X0: l[0].X, X1: l.Where(g => g.Text.Trim().Length > 0).Select(g => g.X + g.Advance).DefaultIfEmpty(l[0].X).Last(),
                    Text: Normalize(string.Concat(l.Select(g => g.Text)))))
                .ToList();
            var ours = Ours(Repo.DeckFile(document), Repo.DeckPassword(document));
            if (Environment.GetEnvironmentVariable("FS_OURS") is { } only)
            {
                foreach (var o in ours.Where(o => only == "" || o.Page.ToString() == only))
                    Console.WriteLine($"  ours p{o.Page} y={o.Y,8:0.00} x={o.X0,8:0.00}..{o.X1,8:0.00}  {Clip(o.Text)}");
            }
            Console.WriteLine($"{document}: Word {word.Count} lines, ours {ours.Count}");
            var used = new HashSet<int>();
            double sumDy = 0, sumDx = 0;
            var matched = 0;
            foreach (var w in word)
            {
                // The same text may stand more than once (columns, repeated paragraphs): the nearest unused line.
                var index = -1;
                var best = double.MaxValue;
                for (var k = 0; k < ours.Count; k++)
                {
                    if (used.Contains(k) || Key(ours[k].Text) != Key(w.Text))
                        continue;
                    var distance = (ours[k].Page != w.Page ? 10000 : 0) + Math.Abs(ours[k].Y - w.Y) + Math.Abs(ours[k].X0 - w.X0);
                    if (distance < best)
                    {
                        best = distance;
                        index = k;
                    }
                }
                if (index < 0)
                {
                    Console.WriteLine($"  p{w.Page} y={w.Y,8:0.00} only Word: {Clip(w.Text)}");
                    continue;
                }
                used.Add(index);
                var o = ours[index];
                var dy = o.Y - w.Y;
                var dx0 = o.X0 - w.X0;
                var dx1 = o.X1 - w.X1;
                matched++;
                sumDy += Math.Abs(dy);
                sumDx += Math.Abs(dx0);
                if (all || o.Page != w.Page || Math.Abs(dy) > 0.15 || Math.Abs(dx0) > 0.15 || Math.Abs(dx1) > 0.4)
                    Console.WriteLine($"  p{w.Page}{(o.Page != w.Page ? $"→{o.Page}" : "")} y={w.Y,8:0.00} dy {dy,6:+0.00;-0.00} dx0 {dx0,6:+0.00;-0.00} dx1 {dx1,6:+0.00;-0.00}  {Clip(w.Text)}");
            }
            for (var i = 0; i < ours.Count; i++)
            {
                if (!used.Contains(i))
                    Console.WriteLine($"  p{ours[i].Page} y={ours[i].Y,8:0.00} only ours: {Clip(ours[i].Text)}");
            }
            if (matched > 0)
                Console.WriteLine($"  matched {matched}, mean |dy| {sumDy / matched:0.000}, mean |dx0| {sumDx / matched:0.000}");
        }
    }

    /// <summary>A line of glyphs split where more than 24 pt lie between one glyph's end and the next glyph.</summary>
    static IEnumerable<List<PdfGlyph>> SplitAtGaps(List<PdfGlyph> line)
    {
        var current = new List<PdfGlyph>();
        foreach (var g in line)
        {
            if (current.Count > 0 && g.X - (current[^1].X + current[^1].Advance) > 24)
            {
                yield return current;
                current = [];
            }
            current.Add(g);
        }
        if (current.Count > 0)
            yield return current;
    }

    static string Clip(string text) => text.Length > 70 ? text[..70] + "…" : text;

    /// <summary>The text lines are matched by: without white space (Word writes tabs as spaces) and symbol font characters.</summary>
    static string Key(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c) && c is not (>= '\uE000' and <= '\uF8FF') and not '\u2022' and not '?'));

    static string Normalize(string text) => text.Replace(' ', ' ').TrimEnd();

    /// <summary>Our lines: page, baseline, start and end of the drawn text (trailing spaces excluded) and the text.</summary>
    static List<(int Page, double Y, double X0, double X1, string Text)> Ours(string path, string? password)
    {
        using var context = new DocumentContext(OpcPackage.Open(path, password));
        var reader = new ModelReader(context);
        var styles = new RunStyles(context);
        var layout = new ParagraphLayout(context, styles);
        var pages = new Paginator(context, layout, reader).Paginate(reader.Body(), reader.LastSection());
        var result = new List<(int, double, double, double, string)>();
        foreach (var page in pages)
        {
            foreach (var placed in page.Lines)
            {
                // Pieces of drawn text, a tab's leader as its characters; a tab without leader more than 24 pt wide
                // separates segments, as Word's lines are split.
                var segment = new List<(double X0, double X1, string Text, double Raise)>();
                void Close()
                {
                    var text = Normalize(string.Concat(segment.Select(s => s.Text)));
                    var visible = segment.Where(s => s.Text.Trim().Length > 0).ToList();
                    if (text.Length > 0 && visible.Count > 0)
                        result.Add((page.Number, placed.Top + placed.Line.Baseline - visible[0].Raise, visible[0].X0, visible[^1].X1, text));
                    segment.Clear();
                }
                foreach (var piece in placed.Line.Pieces)
                {
                    var x = placed.Left + piece.X;
                    if (piece.IsTab)
                    {
                        var leader = TabLeader.Positions(piece, x);
                        if (leader.Count > 0 && TabLeader.Character(piece.Leader) is { } c)
                            segment.Add((leader[0], leader[^1] + (leader.Count > 1 ? leader[^1] - leader[^2] : 0), new string(c, leader.Count), 0));
                        // Word draws a tab as a space: the gap starts after it.
                        else if (piece.Width - piece.Style.Font.Data.GlyphsWithFeatures(" ", piece.Style.Features)[0].Advance * piece.Style.Size > 24)
                            Close();
                        continue;
                    }
                    segment.Add((x + Leading(piece), x + Trimmed(piece), piece.Text, piece.Style.Raise));
                }
                Close();
            }
        }
        // Lines of table cells side by side on one baseline are one line in Word's PDF where less than 24 pt lie
        // between them.
        var merged = new List<(int Page, double Y, double X0, double X1, string Text)>();
        foreach (var line in result.OrderBy(l => l.Item1).ThenBy(l => Math.Round(l.Item2, 1)).ThenBy(l => l.Item3))
        {
            if (merged.Count > 0 && merged[^1].Page == line.Item1 && Math.Abs(merged[^1].Y - line.Item2) < 0.05
                && line.Item3 - merged[^1].X1 < 27 && line.Item3 > merged[^1].X0)
            {
                var m = merged[^1];
                merged[^1] = (m.Page, m.Y, m.X0, line.Item4, m.Text + " " + line.Item5);
            }
            else
                merged.Add(line);
        }
        return merged;
    }

    static double Leading(Piece piece)
    {
        var x = 0.0;
        for (var i = 0; i < piece.Text.Length && piece.Text[i] == ' '; i++)
            x += piece.Advances[i];
        return x;
    }

    static double Trimmed(Piece piece)
    {
        var end = piece.Text.TrimEnd(' ').Length;
        var x = 0.0;
        var spaces = 0;
        for (var i = 0; i < end; i++)
        {
            x += piece.Advances[i];
            if (piece.Text[i] == ' ' && spaces++ < piece.JustifiedSpaces)
                x += piece.SpaceExtra;
        }
        return x;
    }
}
