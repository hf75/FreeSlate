using System.Globalization;
using System.Security;
using FreeSlate.Docx;

namespace FreeSlate;

/// <summary>
/// Drop caps as Word's Insert > Drop Cap makes them (catalog: paragraph.frames): the paragraph's first letter in a paragraph of its own
/// before it, in a frame (w:framePr w:dropCap drop, or margin to stand in the margin) of so many of the paragraph's lines, around which the
/// text wraps.
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>
    /// Whether a paragraph is a drop cap's (see the class): its lines and whether it stands in the margin; null for another paragraph.
    /// </summary>
    public (int Lines, bool Margin)? DropCapOf(int paragraph)
    {
        if (Found(paragraph) is not var (p, _) || p.Format.Frame is not { } frame || frame.Attr("dropCap") is not { } kind || kind is not ("drop" or "margin"))
            return null;
        return (int.TryParse(frame.Attr("lines"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lines) ? lines : 1, kind == "margin");
    }

    /// <summary>
    /// The first letter of a paragraph made a drop cap of so many lines (2 to 10), in the text or in the margin, as Word makes one: a
    /// paragraph before it, of its style, framed (w:framePr w:dropCap, w:lines, wrapped around, at the text), its one line exactly as
    /// high as those lines of the paragraph; the letter in the paragraph's font as large as its capitals then reach from the first
    /// line's capitals to the last line's baseline, and lowered onto that baseline from where Word's exact lines put it, at 80 % of
    /// their height (measured with Word's own: Aptos 10 to 24 pt, Calibri, Times New Roman, Georgia and Arial, single to 1.5 lines, 2 to
    /// 5 lines down: its line Word's, its size and lowering within a point, mostly half a point, of Word's). One change; returns the drop
    /// cap's paragraph.
    /// </summary>
    /// <exception cref="ArgumentException">The lines are fewer than 2 or more than 10.</exception>
    /// <exception cref="NotSupportedException">The paragraph begins with no letter, or is not laid out; nothing changes.</exception>
    public int DropCap(int paragraph, int lines = 3, bool margin = false)
    {
        if (lines is < 2 or > 10)
            throw new ArgumentException("A drop cap is 2 to 10 lines high.", nameof(lines));
        var text = Text(paragraph);
        if (text.Length == 0 || !char.IsLetterOrDigit(text, 0))
            throw new NotSupportedException("The paragraph begins with no letter.");
        var letter = char.IsSurrogatePair(text, 0) ? text[..2] : text[..1];
        // The paragraph's first line as laid out: its height (its spacing's), its baseline (where single spacing puts it), its font; the
        // pages laid out on till they hold it.
        while (PageOf(paragraph, 0) == null && LayingOut && LayOut(8))
        {
        }
        if (PageOf(paragraph, 0) is not { } page || shown[page].Lines.FirstOrDefault(l => l.Line.Paragraph?.Id == paragraph) is not { } first
            || first.Line.Pieces.FirstOrDefault(piece => piece.Text.Length > 0) is not { } piece)
            throw new NotSupportedException("The paragraph is not laid out.");
        var (pitch, baseline, size, cap) = (first.Line.Height, first.Line.Baseline, piece.Style.Size, piece.Style.Font.Data.CapHeight);
        var height = lines * pitch;
        var letterSize = (lines - 1) * pitch / cap + size;
        var lowered = (lines - 1) * pitch + baseline - 0.8 * height;
        // Word cuts its twips and half points off rather than round them (its line one twip, its letter half a point less than rounding).
        static string Number(double value) => Math.Truncate(value + 1e-9 * Math.Sign(value)).ToString(CultureInfo.InvariantCulture);
        var (p, _) = Find(paragraph);
        var style = p.Format.Style is { } id ? $"<w:pStyle w:val=\"{SecurityElement.Escape(id)}\"/>" : "";
        var font = piece.Style.Font.Data.Family is { Length: > 0 } family
            ? $"<w:rFonts w:ascii=\"{SecurityElement.Escape(family)}\" w:hAnsi=\"{SecurityElement.Escape(family)}\"/>" : "";
        var markup = $"<w:p><w:pPr>{style}<w:framePr w:dropCap=\"{(margin ? "margin" : "drop")}\" w:lines=\"{lines.ToString(CultureInfo.InvariantCulture)}\""
            + $" w:wrap=\"around\" w:vAnchor=\"text\" w:hAnchor=\"{(margin ? "page" : "text")}\"/><w:spacing w:after=\"0\" w:line=\"{Number(height * 20)}\""
            + $" w:lineRule=\"exact\"/><w:textAlignment w:val=\"baseline\"/></w:pPr><w:r><w:rPr>{font}<w:position w:val=\"{Number(-lowered * 2)}\"/>"
            + $"<w:sz w:val=\"{Number(letterSize * 2)}\"/><w:szCs w:val=\"{Number(letterSize * 2)}\"/></w:rPr><w:t>{SecurityElement.Escape(letter)}</w:t></w:r></w:p>";
        var made = InsertBlocks(paragraph, markup, before: true)[0];
        try
        {
            Delete(paragraph, 0, letter.Length);
        }
        catch
        {
            Undo();
            throw;
        }
        Combine(2);
        return made;
    }
}
