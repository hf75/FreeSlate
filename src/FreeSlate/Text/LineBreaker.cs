using System.Globalization;

namespace FreeSlate.Text;

/// <summary>
/// Line break opportunities, a pragmatic subset of UAX #14: after spaces and tabs, after hyphens and dashes
/// between letters, around CJK ideographs (respecting the common kinsoku characters) and after zero-width spaces.
/// </summary>
public static class LineBreaker
{
    const string NoBreakBefore = "、。，．・：；？！ー）」』】〕〉》〙〗｝］…‥ゝゞヽヾぁぃぅぇぉっゃゅょゎァィゥェォッャュョヮヵヶ,.;:!?)]}%";
    const string NoBreakAfter = "（「『【〔〈《〘〖｛［([{";

    /// <summary>breakAfter[i] is true if a line may end after text[i].</summary>
    public static bool[] Opportunities(string text)
    {
        var result = new bool[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (next == '\0')
                break;
            if (NoBreakBefore.Contains(next) || NoBreakAfter.Contains(c))
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
