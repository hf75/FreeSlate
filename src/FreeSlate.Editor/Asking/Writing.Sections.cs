using FreeSlate.Editing;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// Section breaks and columns as a write gives them (see Notation.SectionBreak, Notation.Columns): a break written right after a paragraph
/// it wrote ends the section with that paragraph, as Word's break put in after a paragraph's text does, right after text in columns with
/// the columns' own section, else with an empty paragraph of its own, as one put in at a paragraph's start (see WordDocument.BreakSection);
/// the section it ends takes the properties of a section's end the write took away, the first not given again, and text in columns the
/// ends of the sections the columns it took away had, so that text written back as it reads keeps its sections' page setup, headers and
/// footers; columns of widths of their own and spaced as their mark says.
/// </summary>
sealed partial class Writing
{
    /// <summary>The properties of the sections the paragraphs a write takes away ended, in order, for the breaks it writes (see Break).</summary>
    readonly Queue<string> keptSections = new();

    /// <summary>
    /// The properties of the sections that end where text in columns begins and ends, the ends the columns' marks say (see KeepSections): by
    /// the place of the columns among those the write took away (0 the first), the end before them (X) or their own (C), for the columns the
    /// write gives again (see ColumnsEnds), so that they keep the page setup they had, whatever the section after the place has.
    /// </summary>
    readonly List<(int Columns, char Role, string Markup)> keptColumns = [];

    /// <summary>How many texts in columns the write began so far.</summary>
    int columnsWritten;

    /// <summary>The paragraph the block put in last made, where it was a paragraph: a section break written right after it ends the section with it.</summary>
    int? lastParagraph;

    /// <summary>Whether the block put in last was a section break: columns written right after it begin with the section it begins.</summary>
    bool lastBreak;

    /// <summary>The end of the section of the columns that ended with the block put in last (see Notation.ColumnsEnd); a break right after them ends that section.</summary>
    int? lastColumns;

    /// <summary>The columns each section of columns the write made was given, and the section at their place then, by the paragraph that ends it.</summary>
    readonly Dictionary<int, (Notation.Columns Open, SectionFormatting Section)> columnBlocks = [];

    /// <summary>
    /// The ends of the sections text in columns takes (see Writing.Put): of the section before it (none right after a section break, where
    /// the columns' section begins as the break says) and of its own, as kept from the same columns a write took away (in 1 pt paragraphs, as
    /// Markup.SectionEnd makes them), else as Markup makes them from the section at the place.
    /// </summary>
    string ColumnsEnds(SectionFormatting section, Notation.Columns open, bool afterBreak)
    {
        var block = columnsWritten++;
        string? Kept(char role)
        {
            var index = keptColumns.FindIndex(k => k.Columns == block && k.Role == role);
            if (index < 0)
                return null;
            var markup = keptColumns[index].Markup;
            keptColumns.RemoveAt(index);
            return markup;
        }
        static string Small(string properties) =>
            "<w:p><w:pPr><w:spacing w:before=\"0\" w:after=\"0\" w:line=\"20\" w:lineRule=\"exact\"/><w:rPr><w:sz w:val=\"2\"/><w:szCs w:val=\"2\"/></w:rPr>" + properties + "</w:pPr></w:p>";
        var before = Kept('X');
        var own = Kept('C');
        return (afterBreak ? "" : before != null ? Small(before) : Markup.SectionEnd(section, null))
            + (own != null ? Small(own) : Markup.SectionEnd(section, (open.Count, open.Line)));
    }

    /// <summary>
    /// The properties of the sections that end with paragraphs from one to another (in reading order) kept before they are taken away: those
    /// at a break the notation says on a line of its own (see Reading.BreakLine) for the breaks the write gives, those the columns' marks
    /// say by the columns they begin or end, counted as the notation counts them (a mark at each change of the columns' number to more
    /// than one); not those of a table's pages across, which the table keeps.
    /// </summary>
    void KeepSections(int first, int last)
    {
        if (word == null)
            return;
        int Count(int paragraph) => Math.Max(1, word.SectionOf(paragraph).Columns ?? 1);
        // As the notation counts them from the text before: columns the place begins within are none it begins.
        var (columns, blocks) = (1, 0);
        try
        {
            if (document.Previous(first) is { } previous && word.TableOf(previous) == null)
                columns = Count(previous);
        }
        catch (NotSupportedException)
        {
        }
        for (int? paragraph = first; paragraph is { } at; paragraph = document.Next(at))
        {
            try
            {
                if (word.TableOf(at) == null && Count(at) is var count && count != columns)
                {
                    if (count > 1)
                        blocks++;
                    columns = count;
                }
                if (Reading.BreakLine(word, at) is { } line && (line.Length > 0 || !Reading.BesideAcross(word, at)))
                {
                    if (line.Length > 0)
                        keptSections.Enqueue(word.SectionMarkup(at));
                    else if (document.Next(at) is { } next)
                    {
                        // The end of text in columns (its section of more than one), else the end before columns that begin after it.
                        var (own, after) = (Count(at), Count(next));
                        if (own > 1)
                            keptColumns.Add((blocks - 1, 'C', word.SectionMarkup(at)));
                        else if (after > 1)
                            keptColumns.Add((blocks, 'X', word.SectionMarkup(at)));
                    }
                }
            }
            catch (NotSupportedException)
            {
            }
            if (at == last)
                break;
        }
    }

    /// <summary>
    /// A section break before the place (see the class): right after text in columns (<paramref name="columnsEnd"/>) with their section, right
    /// after the paragraph <paramref name="after"/> with it, else in an empty paragraph of its own; a step of its own.
    /// </summary>
    void Break(Notation.SectionBreak block, int at, int? after, int? columnsEnd)
    {
        Step(() =>
        {
            var kept = keptSections.Count > 0 ? keptSections.Dequeue() : null;
            if (columnsEnd is { } end && document.Contains(end) && columnBlocks.TryGetValue(end, out var columns))
            {
                // The columns' section ends at the break: as it was kept, given the columns as their mark says them.
                word!.BreakSection(end, block.Kind, kept, at: true);
                if (kept != null)
                    Columned(end, columns.Open, columns.Section);
                return new TextPlace(at, 0);
            }
            var attach = after is { } paragraph && document.Contains(paragraph) && document.Before(at) == paragraph && !sectionEnds.Contains(paragraph);
            var ending = word!.BreakSection(attach ? after!.Value : at, block.Kind, kept, at: attach);
            sectionEnds.Add(ending);
            if (!written.Contains(ending))
                written.Add(ending);
            return new TextPlace(at, 0);
        });
    }

    /// <summary>
    /// The section of columns ending with a paragraph given the columns, widths and space its mark says (see Notation.Columns): widths as
    /// shares of the text's width less the spaces between the columns.
    /// </summary>
    void Columned(int ending, Notation.Columns open, SectionFormatting section)
    {
        if (word == null)
            return;
        // So many columns, a line between them or not, as the mark says (columns kept from before may have had others).
        var space = open.Space ?? (open.Widths != null ? section.ColumnSpace ?? 36 : null);
        IReadOnlyList<double> widths = [];
        if (open.Widths is { Count: > 1 } shares)
        {
            var across = (section.PageWidth ?? 595.3) - Math.Abs(section.Left ?? 72) - Math.Abs(section.Right ?? 72) - (section.Gutter ?? 0);
            var room = Math.Max(36, across - space!.Value * (shares.Count - 1));
            widths = [.. shares.Select(s => room * s / shares.Sum())];
        }
        word.Format(ending, new SectionFormatting { Columns = open.Count, ColumnLine = open.Line, ColumnSpace = space, ColumnWidths = widths });
    }
}
