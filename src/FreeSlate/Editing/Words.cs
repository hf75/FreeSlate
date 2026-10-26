using System.Globalization;

namespace FreeSlate.Editing;

/// <summary>
/// Words of a paragraph's text as Word's and PowerPoint's character formatting finds them where no text is selected
/// (measured in Word 365 and PowerPoint 365 by COM, CommandBars.ExecuteMso "Bold"): letters, digits and marks, with
/// apostrophes between them (don't), in PowerPoint hyphens between them too (e-mail; in Word only "mail"); spaces, other
/// punctuation (periods, commas, underscores) and objects part words.
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
