using System.Globalization;
using System.Text;

namespace FreeSlate.Text;

/// <summary>
/// The 41 automatic numbering schemes of a:buAutoNum (ST_TextAutonumberScheme, catalog: text.bullets), as
/// PowerPoint draws them (read from renderings of every scheme): Latin letters repeat after z (aa, bb …),
/// East Asian schemes count in Chinese numerals, the circled schemes use the circled digit characters, the
/// Bidi schemes Arabic letters in hijā'ī (arabic1) or abjad (arabic2) order and Hebrew letters followed by
/// a hyphen-minus, Thai letters skip the obsolete ฃ ฅ ฆ, and the Hindi schemes count in Devanagari vowels,
/// consonants or digits.
/// </summary>
public static class AutoNumbering
{
    const string ArabicHijai = "ابتثجحخدذرزسشصضطظعغفقكلمنهوي";
    const string ArabicAbjad = "أبجدهوزحطيكلمنسعفصقرشتثخذضظغ";
    const string Hebrew = "אבגדהוזחטיכלמנסעפצקרשת";
    const string ThaiLetters = "กขคงจฉชซฌญฎฏฐฑฒณดตถทธนบปผฝพฟภมยรลวศษสหฬอฮ";
    static readonly string[] HindiVowels = ["अ", "आ", "इ", "ई", "उ", "ऊ", "ऋ", "ए", "ऐ", "ओ", "औ", "अं", "अः"];
    const string HindiConsonants = "कखगघङचछजझञटठडढणतथदधनपफबभमयरलवशषसह";

    /// <summary>The bullet text for item <paramref name="n"/> (1-based) of a scheme.</summary>
    public static string Format(string scheme, int n)
    {
        n = Math.Max(1, n);
        return scheme switch
        {
            "alphaLcParenBoth" => $"({Alpha(n).ToLowerInvariant()})",
            "alphaUcParenBoth" => $"({Alpha(n)})",
            "alphaLcParenR" => $"{Alpha(n).ToLowerInvariant()})",
            "alphaUcParenR" => $"{Alpha(n)})",
            "alphaLcPeriod" => $"{Alpha(n).ToLowerInvariant()}.",
            "alphaUcPeriod" => $"{Alpha(n)}.",
            "arabicParenBoth" => $"({Arabic(n)})",
            "arabicParenR" => $"{Arabic(n)})",
            "arabicPeriod" => $"{Arabic(n)}.",
            "arabicPlain" => Arabic(n),
            "romanLcParenBoth" => $"({Roman(n).ToLowerInvariant()})",
            "romanUcParenBoth" => $"({Roman(n)})",
            "romanLcParenR" => $"{Roman(n).ToLowerInvariant()})",
            "romanUcParenR" => $"{Roman(n)})",
            "romanLcPeriod" => $"{Roman(n).ToLowerInvariant()}.",
            "romanUcPeriod" => $"{Roman(n)}.",
            "circleNumDbPlain" or "circleNumWdWhitePlain" => CircledWhite(n),
            "circleNumWdBlackPlain" => CircledBlack(n),
            "arabicDbPlain" => FullWidth(Arabic(n)),
            "arabicDbPeriod" => FullWidth(Arabic(n)) + "．",
            "ea1ChsPlain" or "ea1ChtPlain" or "ea1JpnKorPlain" => Chinese(n),
            "ea1ChsPeriod" or "ea1ChtPeriod" or "ea1JpnKorPeriod" => Chinese(n) + ".",
            "ea1JpnChsDbPeriod" => Chinese(n) + "．",
            "arabic1Minus" => Letters(ArabicHijai, n) + "-",
            "arabic2Minus" => Letters(ArabicAbjad, n) + "-",
            "hebrew2Minus" => Letters(Hebrew, n) + "-",
            "thaiAlphaPeriod" => Letters(ThaiLetters, n) + ".",
            "thaiAlphaParenR" => Letters(ThaiLetters, n) + ")",
            "thaiAlphaParenBoth" => $"({Letters(ThaiLetters, n)})",
            "thaiNumPeriod" => Digits(n, '๐') + ".",
            "thaiNumParenR" => Digits(n, '๐') + ")",
            "thaiNumParenBoth" => $"({Digits(n, '๐')})",
            "hindiAlphaPeriod" => HindiVowels[(n - 1) % HindiVowels.Length] + ".",
            "hindiAlpha1Period" => Letters(HindiConsonants, n) + ".",
            "hindiNumPeriod" => Digits(n, '०') + ".",
            "hindiNumParenR" => Digits(n, '०') + ")",
            _ => $"{Arabic(n)}.",
        };
    }

    static string Arabic(int n) => n.ToString(CultureInfo.InvariantCulture);

    /// <summary>Letters a … z, then aa … zz and so on (the letter repeats).</summary>
    static string Alpha(int n) => new((char)('A' + (n - 1) % 26), (n - 1) / 26 + 1);

    static string Roman(int n)
    {
        (int Value, string Symbol)[] table = [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        var sb = new StringBuilder();
        foreach (var (value, symbol) in table)
        {
            while (n >= value) { sb.Append(symbol); n -= value; }
        }
        return sb.ToString();
    }

    /// <summary>A letter of an alphabet, repeated like the Latin letters after the last one.</summary>
    static string Letters(string alphabet, int n)
    {
        var elements = StringInfo.GetTextElementEnumerator(alphabet);
        var letters = new List<string>();
        while (elements.MoveNext()) letters.Add((string)elements.Current);
        return string.Concat(Enumerable.Repeat(letters[(n - 1) % letters.Count], (n - 1) / letters.Count + 1));
    }

    static string Digits(int n, char zero) => string.Concat(Arabic(n).Select(c => (char)(zero + (c - '0'))));

    static string FullWidth(string digits) => string.Concat(digits.Select(c => (char)('０' + (c - '0'))));

    /// <summary>① … ⑳, ㉑ … ㉟, ㊱ … ㊿; beyond 50 the plain number.</summary>
    static string CircledWhite(int n) => n switch
    {
        <= 20 => ((char)(0x2460 + n - 1)).ToString(),
        <= 35 => ((char)(0x3251 + n - 21)).ToString(),
        <= 50 => ((char)(0x32B1 + n - 36)).ToString(),
        _ => Arabic(n),
    };

    /// <summary>❶ … ❿, ⓫ … ⓴; beyond 20 the plain number.</summary>
    static string CircledBlack(int n) => n switch
    {
        <= 10 => ((char)(0x2776 + n - 1)).ToString(),
        <= 20 => ((char)(0x24EB + n - 11)).ToString(),
        _ => Arabic(n),
    };

    /// <summary>Chinese numerals: 一 … 十, 十一, 二十 …, 百 and 千 for larger numbers.</summary>
    static string Chinese(int n)
    {
        const string digits = "〇一二三四五六七八九";
        if (n < 10) return digits[n].ToString();
        if (n < 100) return (n / 10 == 1 ? "" : digits[n / 10].ToString()) + "十" + (n % 10 == 0 ? "" : digits[n % 10].ToString());
        if (n < 1000)
        {
            var rest = n % 100;
            return digits[n / 100] + "百" + (rest == 0 ? "" : rest < 10 ? "〇" + digits[rest] : rest < 20 ? "一" + Chinese(rest) : Chinese(rest));
        }
        return Arabic(n);
    }
}
