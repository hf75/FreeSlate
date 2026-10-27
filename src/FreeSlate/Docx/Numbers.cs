using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>
/// A number the reader asks for while it reads a story, in the order it asks (catalog: numbering.counting,
/// note.footnotes): a numbered paragraph's list number, a LISTNUM or AUTONUM field's text, a note reference's mark.
/// Replayed in that order on fresh counters (see Numbers.Replay), the steps give the same answers again, so that a
/// story's numbers can be worked out anew from its steps after some of its blocks changed.
/// </summary>
public abstract record NumberStep;

/// <summary>A numbered paragraph's number: its numbering instance, its level, its language (for numbers in words).</summary>
public sealed record ListStep(string? List, int Level, string? Language) : NumberStep;

/// <summary>
/// A LISTNUM field: the name of its list, its level (\l, from 0) and its start (\s) where given, and the paragraph it
/// stands in (a second in the same paragraph counts a level deeper).
/// </summary>
public sealed record ListNumStep(string? Name, int? Level, int? Start, object? Paragraph) : NumberStep;

/// <summary>
/// A paragraph numbering field (AUTONUM, AUTONUMLGL, AUTONUMOUT, BIDIOUTLINE): its type, the outline level of its
/// paragraph (1 to 9, 0 for body text) and the paragraph (fields of one paragraph show one number).
/// </summary>
public sealed record AutoNumStep(string Type, int Level, object? Paragraph) : NumberStep;

/// <summary>
/// A note reference: footnote or endnote, its note's id (null for a deleted reference, which counts without a mark of its
/// own) and the section it stands in (its index and properties, which say how its notes are numbered).
/// </summary>
public sealed record NoteStep(bool Endnote, string? Id, int Section, XElement? SectionProperties) : NumberStep;

/// <summary>
/// The numbers of footnotes and endnotes in the order of their first references (catalog: note.footnotes,
/// note.endnotes), as Word numbers them (measured with docx/local/fnpages-c): in the format, from the start and with the
/// restarts of the section of the reference (w:footnotePr, w:endnotePr), else of the document's settings; decimal
/// footnotes and lower roman endnotes by default, counting on through the sections.
/// </summary>
public sealed class NoteNumbers(XElement? settings)
{
    readonly int[] counts = new int[2], sectionsSeen = [-1, -1];

    /// <summary>Each note's number: the order of its first reference.</summary>
    public Dictionary<string, int> Footnotes { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> Endnotes { get; } = new(StringComparer.Ordinal);

    /// <summary>The marks of the notes, by kind and id.</summary>
    public Dictionary<(bool Endnote, string Id), string> Marks { get; } = [];

    /// <summary>The index of the section that refers to an endnote.</summary>
    public Dictionary<string, int> EndnoteSections { get; } = new(StringComparer.Ordinal);

    /// <summary>The mark a reference shows: its note's, taken where the note is first referred to; null for a deleted reference.</summary>
    public string? Reference(NoteStep step)
    {
        if (step.Id is not { } id)
        {
            Next(step);
            return null;
        }
        var numbers = step.Endnote ? Endnotes : Footnotes;
        if (!numbers.ContainsKey(id))
        {
            numbers[id] = numbers.Count + 1;
            Marks[(step.Endnote, id)] = Next(step);
            if (step.Endnote)
                EndnoteSections[id] = step.Section;
        }
        return Marks[(step.Endnote, id)];
    }

    /// <summary>The mark of the next note of a kind.</summary>
    string Next(NoteStep step)
    {
        var name = step.Endnote ? "endnotePr" : "footnotePr";
        var (own, document) = (step.SectionProperties.Child(name), settings.Child(name));
        string? Value(string property) => own.Child(property).Val() ?? document.Child(property).Val();
        var kind = step.Endnote ? 1 : 0;
        if (Value("numRestart") == "eachSect" && sectionsSeen[kind] != step.Section)
            counts[kind] = 0;
        sectionsSeen[kind] = step.Section;
        var start = int.TryParse(Value("numStart"), out var first) ? first : 1;
        return NumberText.Format(++counts[kind] + start - 1, Value("numFmt") ?? (step.Endnote ? "lowerRoman" : "decimal"));
    }
}

/// <summary>The answers to a story's number steps (see NumberStep), worked out on fresh counters.</summary>
public static class Numbers
{
    /// <summary>
    /// What a step is answered by a story's list counter and the document's note numbers: a list number's text, level and
    /// number of its own (or null), a field's text, a note reference's mark.
    /// </summary>
    public static object? Answer(NumberStep step, ListCounter counter, NoteNumbers notes)
    {
        switch (step)
        {
            case ListStep list:
                var next = counter.Next(list.List, list.Level, list.Language);
                // A LISTNUM without a list's name counts in the list of the story's last numbered paragraph.
                if (next != null)
                    counter.CurrentList = list.List;
                return next;
            case ListNumStep listNum:
                return counter.ListNumber(listNum.Name, listNum.Level, listNum.Start, listNum.Paragraph);
            case AutoNumStep autoNum:
                return counter.AutoNumber(autoNum.Type, autoNum.Level, autoNum.Paragraph);
            case NoteStep note:
                return notes.Reference(note);
            default:
                throw new ArgumentOutOfRangeException(nameof(step));
        }
    }

    /// <summary>
    /// The answers to a story's steps in their order, on a fresh list counter and the given note numbers; where steps
    /// come <paramref name="before"/> them (whose answers are known), those are counted first without their text.
    /// </summary>
    public static List<object?> Replay(IEnumerable<NumberStep> steps, NumberingDefinitions definitions, NoteNumbers notes,
        IEnumerable<NumberStep>? before = null)
    {
        var counter = new ListCounter(definitions);
        foreach (var step in before ?? [])
        {
            // A list number only moves the counters on; the rarer steps are answered as they would be.
            if (step is ListStep list)
            {
                if (counter.Count(list.List, list.Level))
                    counter.CurrentList = list.List;
            }
            else
                Answer(step, counter, notes);
        }
        return [.. steps.Select(step => Answer(step, counter, notes))];
    }
}
