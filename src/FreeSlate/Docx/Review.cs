using FreeSlate.Drawing;

namespace FreeSlate.Docx;

/// <summary>
/// A tracked change a run belongs to in the markup view (catalog: review.revisions): its kind (ins, del, moveFrom,
/// moveTo) and its author's place among the document's authors.
/// </summary>
public sealed record RevisionMark(string Kind, int Author);

/// <summary>A tracked formatting change (w:rPrChange, w:pPrChange, w:tblPrChange): what it says in its balloon and its author.</summary>
public sealed record FormatChange(string Text, int Author);

/// <summary>
/// A comment (catalog: review.comments): its id, author, label ("[initials and number]", a reply's with "R" and the
/// number of the comment it answers) and content; whether it answers another and whether it is resolved
/// (w15:commentEx paraIdParent, done).
/// </summary>
public sealed record Comment(string Id, int Author, string Label, List<Block> Content)
{
    public bool Reply { get; init; }
    public bool Done { get; init; }
}

/// <summary>Where a comment's range begins or ends in the text (w:commentRangeStart, w:commentRangeEnd).</summary>
public sealed record CommentInline(string Id, bool Start, RunFormat Format) : Inline(Format);

/// <summary>
/// Word's markup view as it prints with markup (measured with docx/local/markup-a and docx/generated/revisions, Word
/// 365): every author has a color of Word's palette in the order the authors appear, which marks their insertions
/// (underlined) and deletions (struck through), their comments' ranges and balloons (in a lighter shade) and their
/// formatting changes' balloons; moved text is dark green, struck through twice where it was and underlined twice
/// where it is.
/// </summary>
public static class Review
{
    /// <summary>Word's author colors, in order (measured with docx/local/markup-a: eight authors).</summary>
    static readonly string[] Palette = ["D13438", "0078D4", "5C2E91", "498205", "CC3595", "7160E8", "038387", "6D5700"];

    /// <summary>The color of moved text.</summary>
    public static readonly RgbColor Moved = RgbColor.FromHex("2C6234");

    public static RgbColor Color(int author) => RgbColor.FromHex(Palette[author % Palette.Length]);

    /// <summary>
    /// The lighter shade of comment ranges and comment balloons: the author's color at lightness 0.9176, its hue and
    /// saturation kept (measured: D13438 to F8DCDD, 0078D4 to D5EDFF, 5C2E91 to EADFF4).
    /// </summary>
    public static RgbColor Shade(int author) => Lighter(author, 0.9176);

    /// <summary>
    /// A resolved comment's colors (measured with docx/local/markup-d): its range and balloon at lightness 0.98, the
    /// balloon's outline and the dotted line at 0.875, its text light gray.
    /// </summary>
    public static RgbColor Resolved(int author) => Lighter(author, 0.98);

    public static RgbColor ResolvedLine(int author) => Lighter(author, 0.875);

    public static readonly RgbColor ResolvedText = RgbColor.FromHex("BFBFBF");

    static RgbColor Lighter(int author, double lightness)
    {
        var (h, s, _) = Color(author).ToHsl();
        return RgbColor.FromHsl(h, s, lightness);
    }
}

