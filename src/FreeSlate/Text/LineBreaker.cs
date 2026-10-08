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

    /// <summary>Punctuation that may hang past the end of a line (pPr hangingPunct).</summary>
    public const string Hanging = "、。，．,.";

    /// <summary>breakAfter[i] is true if a line may end after text[i].</summary>
    /// <param name="kinsoku">
    /// The East Asian line breaking rules (pPr eaLnBrk): without them a line may break between any two East Asian
    /// characters, punctuation included (measured).
    /// </param>
    /// <param name="custom">Characters that may not start and not end a line (p:kinsoku), replacing the defaults.</param>
    public static bool[] Opportunities(string text, bool kinsoku = true, (string NotFirst, string NotLast)? custom = null)
    {
        var (notFirst, notLast) = custom ?? (NoBreakBefore, NoBreakAfter);
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
            if (c is ' ' or '\t' or '​' or '­')
                result[i] = next is not (' ' or '\t');
            else if (c is '-' or '‐' or '–' && i > 0 && char.IsLetter(text[i - 1]) && char.IsLetterOrDigit(next))
                result[i] = true;
            else if (c == '—' || next == '—')
                result[i] = next is not (' ' or '\t');
            else if (IsIdeographic(c) || IsIdeographic(next))
                result[i] = next is not (' ' or '\t');
        }
        return result;
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
