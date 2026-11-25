using FreeSlate.Docx;

namespace FreeSlate;

/// <summary>
/// The direct character formatting of a paragraph's text as the document writes it (catalog: run.basic, run.decoration, run.spacing,
/// run.hidden): the properties of the runs its text was read from, which Word's Font dialog and Home tab give a run (w:rPr), to say them
/// as written (see the editor's RunLook).
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>
    /// The stretches of a paragraph's text and the properties of the runs they were read from: each run's w:rPr as written ("" for a run
    /// without), the w:rStyle among them, runs written alike next to each other joined. Text, tabs and fields (as their result's first run
    /// is written, which Word formats the result by): a drawing, a note's mark or a break takes no part in them, and ends a stretch.
    /// </summary>
    public IReadOnlyList<(int Offset, int Length, string Properties)> RunProperties(int paragraph)
    {
        var p = Find(paragraph).Paragraph;
        var stretches = new List<(int Offset, int Length, string Properties)>();
        var at = p.TextOffset;
        foreach (var inline in p.Content)
        {
            var length = ParagraphText.Length(inline);
            var source = inline switch
            {
                TextInline or TabInline => inline.Format.Source,
                FieldInline field => field.Cached.OfType<TextInline>().FirstOrDefault()?.Format.Source,
                _ => null,
            };
            if (length > 0 && source is { } written)
            {
                if (stretches.Count > 0 && stretches[^1] is var (offset, before, last) && last == written.Properties && offset + before == at)
                    stretches[^1] = (offset, before + length, last);
                else
                    stretches.Add((at, length, written.Properties));
            }
            at += length;
        }
        return stretches;
    }
}
