using System.Globalization;
using System.Text;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// Sections as Claude reads them (see Notation.SectionBreak, Arranging.Page): a section's end as a line of its own after the paragraph that
/// ends it ("\sectionbreak continuous", the kind the next section begins with), but where the columns' marks or a table's pages across
/// say it; columns of widths of their own and spaced otherwise in their mark; each section after a break told after the document as "Page
/// from ¶12: …", for the page tool to set up by a paragraph of it.
/// </summary>
static partial class Reading
{
    /// <summary>
    /// The line that says a section ends with a paragraph of the body: "\sectionbreak" and the kind the next section begins with (next,
    /// continuous, even, odd); empty where the notation says it otherwise (an empty paragraph between text in so many columns and text in
    /// so many others on the same page set up alike, which the columns' marks say; one next to a table on pages across, which its settings
    /// say); null where the paragraph ends no section, or nothing follows it.
    /// </summary>
    public static string? BreakLine(WordDocument word, int paragraph)
    {
        if (!Ends(word, paragraph) || word.Next(paragraph) is not { } next)
            return null;
        if (BesideAcross(word, paragraph))
            return "";
        SectionFormatting ending, starting;
        try
        {
            (ending, starting) = (word.SectionOf(paragraph), word.SectionOf(next));
        }
        catch (NotSupportedException)
        {
            return null;
        }
        if (word.Text(paragraph).Length == 0 && starting.Start == "continuous" && Alike(ending, starting) && Math.Max(1, ending.Columns ?? 1) != Math.Max(1, starting.Columns ?? 1))
            return "";
        return "\\sectionbreak " + Notation.SectionWord(starting.Start);
    }

    /// <summary>Whether a paragraph of the body stands right before or after a table on pages across (see WordDocument.TableAcross), whose section's ends it says.</summary>
    public static bool BesideAcross(WordDocument word, int paragraph) =>
        word.Next(paragraph) is { } next && word.TableOf(next) is { } following && word.TableAcross(following)
        || word.Previous(paragraph) is { } previous && word.TableOf(previous) is { } preceding && word.TableAcross(preceding);

    /// <summary>The mark text in columns begins with: "::: columns 2", widths of their own as shares ("widths=2:1"), the space between them where it is not Word's, a line between them.</summary>
    static string ColumnsMark(WordDocument word, int paragraph, int count)
    {
        var mark = new StringBuilder("::: columns ").Append(count.ToString(CultureInfo.InvariantCulture));
        SectionFormatting section;
        try
        {
            section = word.SectionOf(paragraph);
        }
        catch (NotSupportedException)
        {
            return mark.ToString();
        }
        if (section.ColumnWidths is { Count: > 1 } widths && widths.Min() is > 0 and var least)
        {
            // As whole shares where a few of the narrowest give them all (3:2), else to a tenth of it.
            var times = Enumerable.Range(1, 10).FirstOrDefault(k => widths.All(w => Math.Abs(w / least * k - Math.Round(w / least * k)) < 0.04), 0);
            mark.Append(" widths=").Append(string.Join(":", widths.Select(w => times > 0
                ? Math.Round(w / least * times).ToString(CultureInfo.InvariantCulture) : Math.Round(w / least, 1).ToString("0.#", CultureInfo.InvariantCulture))));
        }
        // Word's own space between columns: 1.25 cm (708 twips) in its templates, half an inch elsewhere.
        if (section.ColumnSpace is { } space && Math.Abs(space - 35.4) > 0.3 && Math.Abs(space - 36) > 0.3)
            mark.Append(" space=").Append(Lengths.Cm(space));
        if (section.ColumnLine == true)
            mark.Append(" line");
        return mark.ToString();
    }

    /// <summary>
    /// The sections after the first as Claude reads them after "Page:", each that begins after a break the notation says (see BreakLine) on a
    /// line of its own: "Page from ¶12: A4 landscape, margins …", its header and footer where it has its own, its page border, its page
    /// numbers', its notes' numbering where they differ from the section's before.
    /// </summary>
    public static string SectionsOf(WordDocument word, Objects objects)
    {
        var said = new StringBuilder();
        int? before = null;
        for (int? paragraph = word.AfterEnvelope; paragraph is { } p; paragraph = word.Next(p))
        {
            if (word.TableOf(p) == null)
                before ??= p;
            if (BreakLine(word, p) is not { Length: > 0 } || word.Next(p) is not { } first || before is not { } previousFirst)
                continue;
            said.Append("Page from ¶").Append(first.ToString(CultureInfo.InvariantCulture)).Append(": ").Append(SectionSaid(word, objects, first, previousFirst)).Append('\n');
            before = first;
        }
        return said.ToString();
    }

    /// <summary>A section as Claude reads it (see SectionsOf): what it is set up as, and what it has otherwise than the section before.</summary>
    static string SectionSaid(WordDocument word, Objects objects, int first, int previous)
    {
        SectionFormatting section, was;
        try
        {
            (section, was) = (word.SectionOf(first), word.SectionOf(previous));
        }
        catch (NotSupportedException)
        {
            return "";
        }
        static string Cm(double? points) => Lengths.Cm(Math.Abs(points ?? 0));
        var (width, height) = (section.PageWidth ?? 595.3, section.PageHeight ?? 841.9);
        var paper = Arranging.Papers.FirstOrDefault(p => Math.Abs(p.Value.Width - Math.Min(width, height)) < 3 && Math.Abs(p.Value.Height - Math.Max(width, height)) < 3).Key
            ?? $"{Cm(Math.Min(width, height))} x {Cm(Math.Max(width, height))}";
        var said = new StringBuilder(paper).Append(width > height ? " landscape" : " portrait");
        said.Append(", margins ").Append(Cm(section.Top)).Append(' ').Append(Cm(section.Right)).Append(' ').Append(Cm(section.Bottom)).Append(' ').Append(Cm(section.Left));
        if (section.Gutter is > 0 and var gutter)
            said.Append(", gutter ").Append(Cm(gutter));
        if (section.TitlePage == true)
            said.Append(", its first page with a header and footer of its own");
        foreach (var (name, footer) in new[] { ("header", false), ("footer", true) })
        {
            int? shown, before;
            try
            {
                (shown, before) = (word.ShownHeader(first, "default", footer), word.ShownHeader(previous, "default", footer));
            }
            catch (NotSupportedException)
            {
                continue;
            }
            if (shown == before)
                continue;
            var lines = new List<string>();
            for (var paragraph = shown; paragraph is { } p; paragraph = word.After(p))
                lines.Add(StoryLine(word, p, objects).Replace("\t", "\\t", StringComparison.Ordinal));
            said.Append(", its own ").Append(name).Append(": \"").Append(string.Join(" / ", lines)).Append('"');
        }
        if (section.Border is { Kind: not "none" } border)
            said.Append(", border: ").Append(border.Kind).Append(border.Color is { } color ? " #" + color : "")
                .Append(' ').Append(border.Width.ToString("0.##", CultureInfo.InvariantCulture)).Append("pt round its pages");
        else if (was.Border is { Kind: not "none" })
            said.Append(", no border round its pages");
        if (section.PageNumberStart is >= 0 || section.PageNumberFormat != was.PageNumberFormat)
            said.Append(", page numbers ").Append(NumberFormat(section.PageNumberFormat)).Append(section.PageNumberStart is >= 0 ? " from " + section.PageNumberStart.Value.ToString(CultureInfo.InvariantCulture) : "");
        if (section.Footnotes is { } notes && notes != was.Footnotes)
            said.Append(", footnotes ").Append(NotesSaid(notes));
        return said.ToString();
    }

    /// <summary>
    /// What "Page:" says of the first section besides (see PageOf): a gutter, margins mirrored on facing pages, the text of the first page's
    /// and the even pages' own headers and footers, the notes' numbering where it is not decimal and on through the document.
    /// </summary>
    static string PageMore(WordDocument word, Objects objects, int first, SectionFormatting section)
    {
        var said = new StringBuilder();
        if (section.Gutter is > 0 and var gutter)
            said.Append(", gutter ").Append(Lengths.Cm(gutter));
        if (word.MirrorMargins)
            said.Append(", margins mirrored (left inside, right outside)");
        var kinds = new List<(string Kind, string Name)>();
        if (section.TitlePage == true)
            kinds.Add(("first", "first page's"));
        if (word.EvenAndOddHeaders)
            kinds.Add(("even", "even pages'"));
        foreach (var (kind, name) in kinds)
        {
            var texts = new List<string>();
            foreach (var (story, footer) in new[] { ("header", false), ("footer", true) })
            {
                int? shown;
                try
                {
                    shown = word.ShownHeader(first, kind, footer);
                }
                catch (NotSupportedException)
                {
                    continue;
                }
                var lines = new List<string>();
                for (var paragraph = shown; paragraph is { } p; paragraph = word.After(p))
                    lines.Add(StoryLine(word, p, objects).Replace("\t", "\\t", StringComparison.Ordinal));
                if (string.Concat(lines).Trim().Length > 0)
                    texts.Add(", " + name + " " + story + ": \"" + string.Join(" / ", lines) + "\"");
            }
            said.Append(texts.Count > 0 ? string.Concat(texts) : kind == "first" ? ", the first page without header and footer" : ", even pages without header and footer");
        }
        if (section.Footnotes is { } footnotes && footnotes != new NoteNumbering())
            said.Append(", footnotes ").Append(NotesSaid(footnotes));
        if (section.Endnotes is { } endnotes && endnotes != new NoteNumbering("lowerRoman"))
            said.Append(", endnotes ").Append(NotesSaid(endnotes));
        return said.ToString();
    }

    /// <summary>A page numbers' format as the page tool names it: 1, i, I, a, A.</summary>
    static string NumberFormat(string? format) => format switch
    {
        "lowerRoman" => "i",
        "upperRoman" => "I",
        "lowerLetter" => "a",
        "upperLetter" => "A",
        _ => "1",
    };

    /// <summary>Notes' numbering as the page tool names it (see Arranging's Notes): "a each page", "1 from 5", "* each section".</summary>
    public static string NotesSaid(NoteNumbering notes) =>
        (notes.Format == "chicago" ? "*" : NumberFormat(notes.Format)) + (notes.Start != 1 ? " from " + notes.Start.ToString(CultureInfo.InvariantCulture) : "")
        + notes.Restart switch { "eachPage" => " each page", "eachSect" => " each section", _ => "" };
}
