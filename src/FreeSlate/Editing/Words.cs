using System.Globalization;

namespace FreeSlate.Editing;

/// <summary>
/// Words of a paragraph's text as Word's and PowerPoint's character formatting finds them where no text is selected
/// (measured in Word 365 and PowerPoint 365 by COM, CommandBars.ExecuteMso "Bold"): letters, digits and marks, with
/// apostrophes between them (don't), in PowerPoint hyphens between them too (e-mail; in Word only "mail"); spaces, other
/// punctuation (periods, commas, underscores) and objects part words. And Word's units of a word, which a double click
/// selects and Ctrl with the arrows goes by (see Unit).
/// </summary>
public static class Words
{
    /// <summary>
    /// The word a place of a text stands inside, with a character of it on either side: its offset and length; null at a
    /// word's edge, between words and in an empty text. <paramref name="hyphens"/>: whether hyphens join words.
    /// </summary>
    public static (int Offset, int Length)? Around(string text, int offset, bool hyphens = false)
    {
        if (offset <= 0 || offset >= text.Length || !Of(text, offset - 1, hyphens) || !Of(text, offset, hyphens))
            return null;
        var (from, to) = (offset, offset);
        while (from > 0 && Of(text, from - 1, hyphens))
            from--;
        while (to < text.Length && Of(text, to, hyphens))
            to++;
        return (from, to - from);
    }

    /// <summary>
    /// Word's unit of a word around a character of a text, which a double click selects and Ctrl with the arrows goes by
    /// (measured in Word 365 by COM: Range.Expand wdWord, WordBasic.WordRight and WordLeft): a run of letters and digits
    /// (with apostrophes between them, not hyphens: "don't", "e", "-", "mail") or of other signs ("://", "...") with the
    /// spaces after it (spaces, no-break and narrow no-break spaces); a tab, a line break, another space (an en space) or an
    /// object alone; spaces at the text's start or after one of those a unit of their own. Its start and end.
    /// </summary>
    public static (int Start, int End) Unit(string text, int index)
    {
        var kind = KindAt(text, index);
        if (kind == Kind.Alone)
            return (index, index + 1);
        var start = index;
        if (kind == Kind.Space)
        {
            while (start > 0 && KindAt(text, start - 1) == Kind.Space)
                start--;
            if (start == 0 || KindAt(text, start - 1) == Kind.Alone)
            {
                var spaces = index;
                while (spaces < text.Length && KindAt(text, spaces) == Kind.Space)
                    spaces++;
                return (start, spaces);
            }
            kind = KindAt(text, --start);
        }
        while (start > 0 && KindAt(text, start - 1) == kind)
            start--;
        var end = start;
        while (end < text.Length && KindAt(text, end) == kind)
            end++;
        while (end < text.Length && KindAt(text, end) == Kind.Space)
            end++;
        return (start, end);
    }

    /// <summary>Where Ctrl+Right goes from a place of a text: the end of the unit after it (see Unit); null at the text's end.</summary>
    public static int? Next(string text, int offset) => offset < text.Length ? Unit(text, offset).End : null;

    /// <summary>Where Ctrl+Left goes from a place of a text: the start of the unit before it (see Unit); null at the text's start.</summary>
    public static int? Previous(string text, int offset) => offset > 0 ? Unit(text, Math.Min(offset, text.Length) - 1).Start : null;

    enum Kind { Letter, Sign, Space, Alone }

    /// <summary>What a character is to a unit of a word (see Unit): either half of a surrogate pair as the pair.</summary>
    static Kind KindAt(string text, int i)
    {
        if (text[i] is ' ' or ' ' or ' ')
            return Kind.Space;
        if (Of(text, i, hyphens: false))
            return Kind.Letter;
        if (char.IsLowSurrogate(text[i]) && i > 0 && char.IsHighSurrogate(text[i - 1]))
            i--;
        return char.IsControl(text[i]) || text[i] == '￼'
            || char.GetUnicodeCategory(text, i) is UnicodeCategory.SpaceSeparator or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
            ? Kind.Alone : Kind.Sign;
    }

    /// <summary>Whether the character at an index is of a word: a letter, a digit or a mark, or an apostrophe (a hyphen) between two of them.</summary>
    static bool Of(string text, int i, bool hyphens) =>
        Letter(text, i) || (text[i] is '\'' or '’' || hyphens && text[i] == '-') && i > 0 && i + 1 < text.Length && Letter(text, i - 1) && Letter(text, i + 1);

    /// <summary>Whether the character at an index (either half of a surrogate pair) is a letter, a digit or a mark.</summary>
    static bool Letter(string text, int i)
    {
        if (char.IsLowSurrogate(text[i]) && i > 0 && char.IsHighSurrogate(text[i - 1]))
            i--;
        return char.IsLetterOrDigit(text, i)
            || char.GetUnicodeCategory(text, i) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
    }
}
