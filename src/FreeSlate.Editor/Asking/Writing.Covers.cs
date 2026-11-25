using FreeSlate.Editing;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// A cover page in a write (see Covers): "{cover …}" makes one at the document's start, wherever it is written; the cover stands first, so
/// that a write after or before one of its paragraphs goes after it, and one in place of paragraphs from its first on takes it away whole
/// (Word's Remove Current Cover Page) and the rest from the paragraph after it.
/// </summary>
sealed partial class Writing
{
    /// <summary>A cover made at the document's start, a step of its own; its paragraphs counted as written, before the others.</summary>
    void PutCover(WordDocument word, Notation.Cover cover) =>
        Step(() =>
        {
            var ids = Covers.Make(word, cover.Settings, problems);
            written.InsertRange(0, ids);
            return ids.Count > 0 ? new TextPlace(ids[^1], 0) : null;
        });

    /// <summary>
    /// The place's empty paragraph, left the last of the body by a write that made a cover and wrote nothing there, joined with the
    /// paragraph before it, which keeps its own mark (see WordDocument.Join).
    /// </summary>
    void ClearCoverPlace()
    {
        if (word == null || placeholder is not { } made || !document.Contains(made) || document.Text(made).Length > 0 || document.After(made) != null
            || written.Count == 0 || written.Any(p => document.Contains(p) && Covers.Around(word, p) == null)
            || document.Before(made) is not { } previous || word.TableOf(previous) != null || Covers.Around(word, previous) != null)
            return;
        try
        {
            if (word.SectionEnd(previous) == previous)
                return;
        }
        catch (NotSupportedException)
        {
            return;
        }
        Step(() => new TextPlace(document.Join(previous, backward: true), 0), quiet: true);
    }

    /// <summary>
    /// Paragraphs replaced from a cover's first on: the cover taken away whole first; where they were the cover alone, the place made before
    /// the paragraph after it (returns whether that could be done), else <paramref name="first"/> the paragraph after the cover, from which
    /// the rest is replaced as any paragraphs are (returns null). Null too where the paragraphs do not begin with a cover.
    /// </summary>
    bool? Uncovered(ref int first, int last)
    {
        if (word == null || Covers.Around(word, first) is not var (cover, coverLast) || cover != first)
            return null;
        var alone = false;
        for (int? paragraph = cover; paragraph is { } at; paragraph = at == coverLast ? null : document.After(at))
            alone |= at == last;
        if (document.After(coverLast) is not { } after)
        {
            refused = "The cover is all the document holds: write after it instead.";
            return false;
        }
        var covered = cover;
        if (!Step(() =>
            {
                word.RemoveGalleryBlock(covered, "Cover Pages");
                return new TextPlace(after, 0);
            }))
            return false;
        if (alone)
            return Step(() => (placeholder = anchor = document.InsertBlocks(after, "<w:p/>", before: true)[0]) is { } made ? new TextPlace(made, 0) : null);
        first = after;
        return null;
    }
}
