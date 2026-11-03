using System.Xml.Linq;
using FreeSlate.Docx;

namespace FreeSlate;

/// <summary>
/// Copying and pasting with the source's formatting within a Word document (see IEditableDocument.Copy, Paste): a
/// selection's paragraphs as WordprocessingML, each with its properties and the runs of the selection in it, put back
/// where the paste goes as Word puts pasted paragraphs (measured in Word 365 by COM, Range.FormattedText).
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>
    /// The selection from a place to a later one in the body, a table cell or a text box (of the body) in WordprocessingML,
    /// as copying it keeps it for pasting with its formatting (see TextEdits.Copy): each paragraph it reaches into a w:p
    /// with its properties and what of its content the selection holds, the last one without its mark (where the
    /// selection ends at a paragraph's beginning, an empty w:p last); the changes accepted, without bookmarks, comments'
    /// ranges and references, proofing marks and notes' references. Null where the document keeps the selection's text
    /// alone: it is not of one body, cell or text box (a table between, an end in a table), stands in a note, header,
    /// footer or comment, or reaches into what cannot be split (a field's result, a computed text); and in a document of
    /// Strict names (whose markup goes in in transitional names, see InsertMarkup).
    /// </summary>
    public IReadOnlyList<string>? Copy(int first, int offset, int last, int lastOffset)
    {
        Read();
        if (IsStrictPart(context.MainPart) || !paragraphs.TryGetValue(first, out var from) || !paragraphs.TryGetValue(last, out var to))
            return null;
        var (p, top) = from;
        var (q, lastTop) = to;
        if (ListOf(top, p, walking: true) is not var (list, i) || list.FindIndex(b => ReferenceEquals(b, q)) is var j && j < i
            || list.Skip(i).Take(j - i + 1).Any(b => b is not ParagraphBlock))
            return null;
        try
        {
            var stretch = StretchOf(IndexOf(top), IndexOf(lastTop));
            return TextEdits.Copy(stretch, p, offset, q, lastOffset);
        }
        catch (Exception e) when (e is NotSupportedException or InvalidOperationException)
        {
            // (An element the selection's paragraphs declare namespaces on is written with the prefixes of its scope alone.)
            return null;
        }
    }

    /// <summary>
    /// Pastes paragraphs Copy gave at a place, as Word pastes them with their formatting (measured in Word 365 by COM,
    /// Range.FormattedText): one alone goes in among the runs there, its runs keeping their own formatting; of more, the
    /// first's runs end the paragraph before the place and its mark brings its properties to it, those between follow as
    /// paragraphs of their own, and the last's runs begin the paragraph after the place, which keeps its own properties.
    /// Returns where the cursor then stands: after what was pasted.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The markup cannot go there (see InsertMarkup, InsertBlocks, Split), or the paragraphs it went into are not found
    /// again; the changes made before are not taken back (an editor does so, see Editing.EditSession).
    /// </exception>
    public (int Paragraph, int Offset) Paste(int paragraph, int offset, IReadOnlyList<string> copied)
    {
        try
        {
            return Pasted(paragraph, offset, copied);
        }
        catch (ArgumentException e)
        {
            throw new NotSupportedException("The pasted paragraphs are not found again (read anew with other ids).", e);
        }
    }

    (int Paragraph, int Offset) Pasted(int paragraph, int offset, IReadOnlyList<string> copied)
    {
        if (copied.Count == 0)
            return (paragraph, offset);
        var (_, top) = Find(paragraph);
        var part = PartOf(top);
        var parsed = Elements(string.Concat(copied), RootOf(part));
        if (parsed.Count != copied.Count || parsed.Any(e => e.Name.LocalName != "p"))
            throw new ArgumentException("The copied markup is not paragraphs.", nameof(copied));
        // What it refers to by the main part's relationships (a picture, a hyperlink) goes into the main part alone.
        if (part != context.MainPart && parsed.Any(p => p.DescendantsAndSelf().Attributes().Any(a => a.Name.NamespaceName.Contains("officeDocument", StringComparison.Ordinal) && a.Name.NamespaceName.EndsWith("relationships", StringComparison.Ordinal))))
            throw new NotSupportedException("What was copied refers to the body's parts (a picture, a hyperlink): it goes into the body alone.");
        // A copied paragraph's content (its runs and what holds them), and the cursor after it by how much longer the
        // paragraph's text grew.
        static string Runs(XElement p) => string.Concat(p.Nodes().Where(n => n is not XElement { Name.LocalName: "pPr" }).Select(n => n.ToString(SaveOptions.DisableFormatting)));
        if (parsed.Count == 1)
        {
            var before = Text(paragraph).Length;
            if (Runs(parsed[0]) is { Length: > 0 } runs)
                InsertMarkup(paragraph, offset, runs);
            return (paragraph, offset + Text(paragraph).Length - before);
        }
        // The halves: split at the paragraph's beginning, the second keeps its id (see Split). The last piece goes in first,
        // while their ids are as Split gave them.
        var second = Split(paragraph, offset);
        var first = Before(second) ?? throw new NotSupportedException("The paragraph did not split there.");
        var rest = Text(second).Length;
        if (Runs(parsed[^1]) is { Length: > 0 } lastRuns)
            InsertMarkup(second, 0, lastRuns);
        var pasted = Text(second).Length - rest;
        if (Runs(parsed[0]) is { Length: > 0 } firstRuns)
            InsertMarkup(first, Text(first).Length, firstRuns);
        var (p, pTop) = Find(first);
        Change(pTop, pTop, (stretch, tracking) => TextEdits.Properties(stretch, p, parsed[0].Element(parsed[0].Name.Namespace + "pPr"), tracking));
        if (parsed.Count > 2)
            InsertBlocks(first, string.Concat(copied.Skip(1).Take(copied.Count - 2)));
        // The cursor after the last piece: in the paragraph so many after the first (read anew, it may have taken another id).
        int? at = first;
        for (var k = 1; k < parsed.Count && at != null; k++)
            at = After(at.Value);
        return (at ?? throw new NotSupportedException("The pasted paragraphs are not where they were put."), pasted);
    }
}
