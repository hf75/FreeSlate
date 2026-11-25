using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// The page tool for one section and for what Word's Page Setup, Columns, Header &amp; Footer and Footnotes give besides (see Page): the
/// section a paragraph stands in set up alone (one page across among upright ones, its own margins, columns, headers and footers or those
/// of the section before), the break it begins with, a paper of any size, a gutter, margins mirrored on facing pages, the first page's and
/// the even pages' own headers and footers, columns of widths of their own, the notes' numbering.
/// </summary>
static partial class Arranging
{
    /// <summary>The paragraph whose section the page tool sets up alone ("paragraph": 12, or "¶12"); null where none is given.</summary>
    static int? SectionParagraph(string json)
    {
        try
        {
            using var parsed = JsonDocument.Parse(json);
            if (!parsed.RootElement.TryGetProperty("paragraph", out var given))
                return null;
            if (given.ValueKind == JsonValueKind.Number && given.TryGetInt32(out var number))
                return number;
            return given.ValueKind == JsonValueKind.String && Regex.Match(given.GetString() ?? "", @"\d+") is { Success: true } digits
                && int.TryParse(digits.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var read) ? read : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A paper of a size of its own: "21cm x 14.8cm" (or ×, by), width first; null where the text is none.</summary>
    static (double Width, double Height)? Paper(string text)
    {
        var parts = Regex.Split(text.Trim(), @"\s*(?:x|×|by|\*)\s*", RegexOptions.IgnoreCase);
        return parts.Length == 2 && Lengths.Points(parts[0]) is > 36 and < 1584 and var width && Lengths.Points(parts[1]) is > 36 and < 1584 and var height
            ? (width, height) : null;
    }

    /// <summary>A notes' numbering as the page tool gives it: its format (1, a, A, i, I, *), "from 3", "each page" or "each section".</summary>
    static NoteNumbering? Notes(string text, bool endnotes)
    {
        var words = text.Trim().Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return null;
        var format = words[0] switch
        {
            "1" or "arabic" or "decimal" => "decimal",
            "a" => "lowerLetter",
            "A" => "upperLetter",
            "i" => "lowerRoman",
            "I" => "upperRoman",
            "*" or "symbols" => "chicago",
            _ => null,
        };
        if (format == null)
            return null;
        var start = words.SkipWhile(w => w != "from").Skip(1).FirstOrDefault() is { } from && int.TryParse(from, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : 1;
        var rest = string.Join(" ", words).ToLowerInvariant();
        var restart = rest.Contains("page", StringComparison.Ordinal) && !endnotes ? "eachPage" : rest.Contains("section", StringComparison.Ordinal) ? "eachSect" : "continuous";
        return new NoteNumbering(format, restart, start);
    }

    /// <summary>
    /// What the page tool gives a section besides margins, paper and the like (see Page): a gutter, columns (as their mark says them: "2",
    /// "3 line", "2 widths=2:1 space=1cm", "1" for none), the notes' numbering, how it begins (the break before it).
    /// </summary>
    static SectionFormatting More(Dictionary<string, string> input, SectionFormatting format, List<string> failed)
    {
        if (input.GetValueOrDefault("gutter") is { } gutter)
        {
            if (Lengths.Points(gutter) is >= 0 and < 300 and var points)
                format = format with { Gutter = points };
            else
                failed.Add($"gutter {gutter} (a length: 1cm)");
        }
        if (input.GetValueOrDefault("columns")?.Trim() is { Length: > 0 } columns)
        {
            var parsed = Notation.Parse("::: columns " + columns + "\n:::", final: true);
            if (parsed.Blocks.FirstOrDefault() is Notation.Columns open && parsed.Problems.Count == 0)
                format = format with
                {
                    Columns = open.Count, ColumnLine = open.Line, ColumnSpace = open.Space ?? format.ColumnSpace,
                    // Widths as shares, made widths of the text once the section's width is known (see Columned).
                    ColumnWidths = open.Widths ?? [],
                };
            else
                failed.Add($"columns {columns} (2, 3 line, 2 widths=2:1 space=1cm, or 1)");
        }
        foreach (var (name, endnotes) in new[] { ("footnotes", false), ("endnotes", true) })
        {
            if (input.GetValueOrDefault(name) is not { } given)
                continue;
            if (Notes(given, endnotes) is { } numbering)
                format = endnotes ? format with { Endnotes = numbering } : format with { Footnotes = numbering };
            else
                failed.Add($"{name} {given} (1, a, A, i, I or *, then \"from 3\", \"each page\" or \"each section\")");
        }
        if (input.GetValueOrDefault("start") is { } start)
        {
            if (Notation.SectionKind(start.Trim()) is { } kind)
                format = format with { Start = kind };
            else
                failed.Add($"start {start} (next, continuous, even or odd)");
        }
        return format;
    }

    /// <summary>
    /// A section format whose columns' widths are given as shares (see More) with the widths they make of the section's text, less the
    /// spaces between them; where it changes the width of a section's text (paper, orientation, margins, gutter), the section's columns of
    /// widths of their own keep their shares of it, as Word's Page Setup keeps them.
    /// </summary>
    static SectionFormatting Widened(SectionFormatting format, SectionFormatting section)
    {
        var shares = format.ColumnWidths is { Count: > 1 } given ? given
            : format.ColumnWidths == null && section.ColumnWidths is { Count: > 1 } own && format.Columns is null or > 1 ? own : null;
        if (shares == null)
            return format;
        var space = format.ColumnSpace ?? section.ColumnSpace ?? 36;
        // The page turned without a size given lies as the section's turned (see WordDocument.Format).
        var width = format.PageWidth ?? (format.Orientation is { } orientation && (orientation == "landscape") != (section.PageWidth > section.PageHeight)
            ? section.PageHeight : section.PageWidth) ?? 595.3;
        var across = width - Math.Abs(format.Left ?? section.Left ?? 72) - Math.Abs(format.Right ?? section.Right ?? 72) - (format.Gutter ?? section.Gutter ?? 0);
        var room = Math.Max(36, across - space * (shares.Count - 1));
        return format with { ColumnWidths = [.. shares.Select(s => room * s / shares.Sum())], ColumnSpace = space };
    }

    /// <summary>
    /// The page tool's headers and footers besides the default ones and its document's settings (see Page): a section's own linked to the
    /// section before again ("link": header, footer or both), the first page's own text (first_header, first_footer), the even pages' own
    /// (even_pages own or same, even_header, even_footer), margins mirrored on facing pages (mirror on or off).
    /// </summary>
    static void Headers(WordDocument word, int target, bool alone, Dictionary<string, string> input, Action<string, Action> done, List<string> failed)
    {
        if (input.GetValueOrDefault("link")?.Trim().ToLowerInvariant() is { } link)
        {
            if (!alone)
                failed.Add("link (give the paragraph of the section whose header or footer is to be that of the section before)");
            else if (link is "header" or "footer" or "both")
                done("link", () =>
                {
                    foreach (var footer in link == "both" ? [false, true] : new[] { link == "footer" })
                    {
                        foreach (var kind in new[] { "default", "first", "even" })
                            word.LinkToPrevious(target, kind, footer, link: true);
                    }
                });
            else
                failed.Add($"link {link} (header, footer or both)");
        }
        if (input.GetValueOrDefault("mirror")?.Trim().ToLowerInvariant() is { } mirror)
        {
            if (mirror is "on" or "yes" or "true" or "off" or "no" or "false" or "none")
                done("mirror", () => word.MirrorMargins = mirror is "on" or "yes" or "true");
            else
                failed.Add($"mirror {mirror} (on or off)");
        }
        if (input.GetValueOrDefault("even_pages")?.Trim().ToLowerInvariant() is { } even)
        {
            if (even is "own" or "same")
                done("even pages", () => word.EvenAndOddHeaders = even == "own");
            else
                failed.Add($"even_pages {even} (own or same)");
        }
        foreach (var (name, kind, footer) in new[] { ("first_header", "first", false), ("first_footer", "first", true), ("even_header", "even", false), ("even_footer", "even", true) })
        {
            if (input.GetValueOrDefault(name) is not { } text)
                continue;
            done(name.Replace('_', ' '), () =>
            {
                if (kind == "first")
                    word.Format(target, new SectionFormatting { TitlePage = true });
                else
                    word.EvenAndOddHeaders = true;
                Own(word, target, alone, kind, footer);
                Story(word, target, footer, text, kind);
            });
        }
    }

    /// <summary>A section's own header (with <paramref name="footer"/> footer) of a kind, where it is set up alone: one it shows of a section before made its own first.</summary>
    static void Own(WordDocument word, int target, bool alone, string kind, bool footer)
    {
        if (!alone)
            return;
        try
        {
            word.LinkToPrevious(target, kind, footer, link: false);
        }
        catch (NotSupportedException)
        {
            // The first section has none before it: its own already.
        }
    }
}
