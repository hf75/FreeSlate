using System.Globalization;

namespace FreeSlate.Text;

/// <summary>
/// Line break opportunities, a pragmatic subset of UAX #14: after spaces and tabs, after hyphens and dashes
/// between letters, around CJK ideographs (respecting the kinsoku characters) and after zero-width spaces.
/// </summary>
public static class LineBreaker
{
    const string NoBreakBefore = "、。，．・：；？！ー）」』】〕〉》〙〗｝］…‥ゝゞヽヾぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮヵヶ,.;:!?)]}%";
    const string NoBreakAfter = "（「『【〔〈《〘〖｛［([{";

    /// <summary>
    /// Punctuation that may hang past the end of a line (pPr hangingPunct): East Asian only (measured: a Latin line
    /// ending in "z." whose period would pass the end goes down with its word, LibreOffice's TextFittingComparisonWithMSO).
    /// </summary>
    public const string Hanging = "、。，．";

    /// <summary>
    /// The kinsoku characters of a language: those that may not begin a line, those that may not end one, and those
    /// of the first that may hang past the line's end instead.
    /// </summary>
    public sealed record Kinsoku(string NotFirst, string NotLast, string Hanging = "");

    /// <summary>breakAfter[i] is true if a line may end after text[i].</summary>
    /// <param name="kinsoku">
    /// The East Asian line breaking rules (pPr eaLnBrk): without them a line may break between any two East Asian
    /// characters, punctuation included (measured).
    /// </param>
    /// <param name="rules">
    /// The kinsoku characters, PowerPoint's where not given; with them East Asian punctuation and full-width forms
    /// break like ideographs (Word: a line may end between 。 and 「, measured with docx/local/ea-adjust).
    /// </param>
    /// <remarks>PowerPoint ignores the presentation's own kinsoku characters (p:kinsoku, measured).</remarks>
    public static bool[] Opportunities(string text, bool kinsoku = true, Kinsoku? rules = null)
    {
        var (notFirst, notLast) = rules is { } own ? (own.NotFirst, own.NotLast) : (NoBreakBefore, NoBreakAfter);
        var result = new bool[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (next == '\0')
                break;
            if (!kinsoku && (IsEastAsian(c) || IsEastAsian(next)) && next is not (' ' or '\t'))
            {
                result[i] = true;
                continue;
            }
            if (notFirst.Contains(next) || notLast.Contains(c))
                continue;
            // Word breaks neither after spaces with a no-break space among them nor before one (measured with
            // docx/local/nb-15, nb-14: "X)" and NBSP SP, SP NBSP or NBSP NBSP SP before a word too long for the line keep
            // it there); its documents come with their line breaking rules.
            if (c is ' ' or '\t' or '​' or '­')
                result[i] = next is not (' ' or '\t') && !(rules != null && (next == ' ' || c == ' ' && NoBreakSpaceBefore(text, i)));
            else if (c is '-' or '‐' or '–' && i > 0 && char.IsLetter(text[i - 1]) && char.IsLetterOrDigit(next))
                result[i] = true;
            else if (c == '—' || next == '—')
                result[i] = next is not (' ' or '\t');
            else if (IsIdeographic(c) || IsIdeographic(next) || rules != null && (IsEastAsian(c) || IsEastAsian(next)))
                result[i] = next is not (' ' or '\t');
        }
        return result;
    }

    /// <summary>Whether the run of spaces ending at <paramref name="end"/> holds a no-break space.</summary>
    static bool NoBreakSpaceBefore(string text, int end)
    {
        for (var k = end; k >= 0 && text[k] is ' ' or ' '; k--)
        {
            if (text[k] == ' ')
                return true;
        }
        return false;
    }

    /// <summary>Ideographs, kana and the East Asian punctuation and full-width forms.</summary>
    static bool IsEastAsian(char c) => IsIdeographic(c) || c is >= '\u3000' and <= '\u303F' || c is >= '\uFF00' and <= '\uFFEF';

    public static bool IsIdeographic(char c) => c switch
    {
        >= '⺀' and <= '⿟' => true,   // CJK radicals
        >= '぀' and <= 'ヿ' => true,   // Hiragana, Katakana
        >= '㐀' and <= '䶿' => true,   // CJK extension A
        >= '一' and <= '鿿' => true,   // CJK unified ideographs
        >= '가' and <= '힯' => false,  // Hangul syllables break at spaces like Latin
        >= '豈' and <= '﫿' => true,   // CJK compatibility ideographs
        >= '＀' and <= '￯' => CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.OtherLetter,
        _ => false,
    };
}
