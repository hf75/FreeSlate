using System.Globalization;
using System.Text;

namespace FreeSlate.Docx;

/// <summary>
/// Numbers as text in Word's number formats (ST_NumberFormat, ISO/IEC 29500 17.18.59; catalog: numbering.definitions,
/// section.pageNumbers, field.evaluation): decimal, roman, letters (repeated after z: aa, bb …), ordinals, English
/// number words, full-width and enclosed digits, Chinese, Japanese and Korean counting, Hebrew, Arabic, Thai, Hindi and
/// Russian letters.
/// </summary>
public static class NumberText
{
    const string ArabicAlpha = "أبتثجحخدذرزسشصضطظعغفقكلمنهوي";
    const string ArabicAbjad = "أبجدهوزحطيكلمنسعفصقرشتثخذضظغ";
    const string Hebrew2 = "אבגדהוזחטיכלמנסעפצקרשת";
    const string ThaiLetters = "กขคงจฉชซฌญฎฏฐฑฒณดตถทธนบปผฝพฟภมยรลวศษสหฬอฮ";
    const string RussianLetters = "абвгдежзиклмнопрстуфхцчшщэюя";
    const string Aiueo = "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン";
    const string Iroha = "イロハニホヘトチリヌルヲワカヨタレソツネナラムウヰノオクヤマケフコエテアサキユメミシヱヒモセス";
    const string Ganada = "가나다라마바사아자차카타파하";
    const string Chosung = "ㄱㄴㄷㄹㅁㅂㅅㅇㅈㅊㅋㅌㅍㅎ";
    static readonly string[] HindiVowels = ["अ", "आ", "इ", "ई", "उ", "ऊ", "ऋ", "ए", "ऐ", "ओ", "औ", "अं", "अः"];
    const string HindiConsonants = "कखगघङचछजझञटठडढणतथदधनपफबभमयरलवशषसह";

    /// <summary>Number <paramref name="n"/> in a number format; unknown formats are decimal.</summary>
    public static string Format(int n, string? format) => format switch
    {
        null or "decimal" => Decimal(n),
        "upperRoman" => Roman(n),
        "lowerRoman" => Roman(n).ToLowerInvariant(),
        "upperLetter" => Repeated("ABCDEFGHIJKLMNOPQRSTUVWXYZ", n),
        "lowerLetter" => Repeated("abcdefghijklmnopqrstuvwxyz", n),
        "ordinal" => Decimal(n) + OrdinalSuffix(n),
        "cardinalText" => Capitalized(Words(n)),
        "ordinalText" => Capitalized(OrdinalWords(n)),
        "hex" => n.ToString("X", CultureInfo.InvariantCulture),
        "chicago" => Chicago(n),
        "decimalZero" => n is >= 0 and < 10 ? "0" + Decimal(n) : Decimal(n),
        "decimalFullWidth" or "decimalFullWidth2" => FullWidth(Decimal(n)),
        "decimalHalfWidth" => Decimal(n),
        "decimalEnclosedCircle" or "decimalEnclosedCircleChinese" => Enclosed(n, 0x2460, 20) ?? Decimal(n),
        "decimalEnclosedFullstop" => Enclosed(n, 0x2488, 20) ?? Decimal(n),
        "decimalEnclosedParen" => Enclosed(n, 0x2474, 20) ?? Decimal(n),
        "ideographEnclosedCircle" => Enclosed(n, 0x3220, 10) ?? Decimal(n),
        "numberInDash" => $"- {Decimal(n)} -",
        "none" => "",
        "bullet" => "•",
        "aiueo" => Cycled(Aiueo, n),
        "aiueoFullWidth" => Cycled(Aiueo, n),
        "iroha" or "irohaFullWidth" => Cycled(Iroha, n),
        "ganada" => Cycled(Ganada, n),
        "chosung" => Cycled(Chosung, n),
        "ideographDigital" or "japaneseDigitalTenThousand" or "taiwaneseDigital" or "koreanDigital2" => DigitByDigit(n, "〇一二三四五六七八九"),
        "koreanDigital" => DigitByDigit(n, "영일이삼사오육칠팔구"),
        "japaneseCounting" or "chineseCounting" or "taiwaneseCounting" or "chineseCountingThousand"
            or "taiwaneseCountingThousand" or "japaneseLegal" or "ideographLegalTraditional" or "chineseLegalSimplified"
            or "koreanCounting" or "koreanLegal" => Chinese(n),
        "ideographTraditional" => Cycled("甲乙丙丁戊己庚辛壬癸", n),
        "ideographZodiac" => Cycled("子丑寅卯辰巳午未申酉戌亥", n),
        "ideographZodiacTraditional" => Decimal(n),
        "hebrew1" => Hebrew1(n),
        "hebrew2" => Repeated(Hebrew2, n),
        "arabicAlpha" => Repeated(ArabicAlpha, n),
        "arabicAbjad" => Repeated(ArabicAbjad, n),
        "thaiLetters" => Repeated(ThaiLetters, n),
        "thaiNumbers" => Digits(n, '๐'),
        "hindiVowels" => HindiVowels[(n - 1 + HindiVowels.Length * 1000) % HindiVowels.Length],
        "hindiConsonants" => Repeated(HindiConsonants, n),
        "hindiNumbers" => Digits(n, '०'),
        "russianLower" => Repeated(RussianLetters, n),
        "russianUpper" => Repeated(RussianLetters.ToUpperInvariant(), n),
        "vietnameseCounting" or "hindiCounting" or "thaiCounting" or "bahtText" or "dollarText" => Decimal(n),
        _ => Decimal(n),
    };

    /// <summary>The number format a field's \* switch names (roman, ROMAN, alphabetic, Arabic, Ordinal …), or null.</summary>
    public static string? FieldFormat(string? name) => name switch
    {
        null => null,
        "roman" => "lowerRoman",
        "Roman" or "ROMAN" => "upperRoman",
        "alphabetic" => "lowerLetter",
        "Alphabetic" or "ALPHABETIC" => "upperLetter",
        _ when name.Equals("Arabic", StringComparison.OrdinalIgnoreCase) => "decimal",
        _ when name.Equals("ArabicDash", StringComparison.OrdinalIgnoreCase) => "numberInDash",
        _ when name.Equals("Ordinal", StringComparison.OrdinalIgnoreCase) => "ordinal",
        _ when name.Equals("CardText", StringComparison.OrdinalIgnoreCase) => "cardinalText",
        _ when name.Equals("OrdText", StringComparison.OrdinalIgnoreCase) => "ordinalText",
        _ when name.Equals("Hex", StringComparison.OrdinalIgnoreCase) => "hex",
        _ when name.Equals("CircleNum", StringComparison.OrdinalIgnoreCase) => "decimalEnclosedCircle",
        _ => null,
    };

    static string Decimal(int n) => n.ToString(CultureInfo.InvariantCulture);

    public static string Roman(int n)
    {
        if (n <= 0)
            return Decimal(n);
        (int Value, string Symbol)[] table = [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        var sb = new StringBuilder();
        foreach (var (value, symbol) in table)
        {
            while (n >= value)
            {
                sb.Append(symbol);
                n -= value;
            }
        }
        return sb.ToString();
    }

    /// <summary>A letter of an alphabet, repeated after the last one (27 → AA, 28 → BB).</summary>
    static string Repeated(string alphabet, int n)
    {
        if (n <= 0)
            return Decimal(n);
        var letters = Elements(alphabet);
        return string.Concat(Enumerable.Repeat(letters[(n - 1) % letters.Count], (n - 1) / letters.Count + 1));
    }

    /// <summary>A letter of a syllabary, starting over after the last one.</summary>
    static string Cycled(string alphabet, int n)
    {
        if (n <= 0)
            return Decimal(n);
        var letters = Elements(alphabet);
        return letters[(n - 1) % letters.Count];
    }

    static List<string> Elements(string text)
    {
        var list = new List<string>();
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
            list.Add((string)elements.Current);
        return list;
    }

    static string Digits(int n, char zero) => string.Concat(Decimal(n).Select(c => c is >= '0' and <= '9' ? (char)(zero + (c - '0')) : c));

    static string FullWidth(string digits) => string.Concat(digits.Select(c => c is >= '0' and <= '9' ? (char)('０' + (c - '0')) : c));

    static string DigitByDigit(int n, string digits) => string.Concat(Decimal(n).Select(c => c is >= '0' and <= '9' ? digits[c - '0'] : c));

    static string? Enclosed(int n, int first, int count) => n >= 1 && n <= count ? char.ConvertFromUtf32(first + n - 1) : null;

    /// <summary>*, †, ‡, §, then doubled, tripled … (the Chicago Manual of Style's note marks).</summary>
    static string Chicago(int n)
    {
        if (n <= 0)
            return Decimal(n);
        const string marks = "*†‡§";
        return new string(marks[(n - 1) % 4], (n - 1) / 4 + 1);
    }

    static string OrdinalSuffix(int n) => (n % 100) switch
    {
        11 or 12 or 13 => "th",
        _ => (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" },
    };

    static readonly string[] Ones = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];
    static readonly string[] Tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>The number in English words ("one hundred twenty-three").</summary>
    static string Words(int n)
    {
        if (n < 0)
            return "minus " + Words(-n);
        if (n < 20)
            return Ones[n];
        if (n < 100)
            return Tens[n / 10] + (n % 10 == 0 ? "" : "-" + Ones[n % 10]);
        if (n < 1000)
            return Ones[n / 100] + " hundred" + (n % 100 == 0 ? "" : " " + Words(n % 100));
        if (n < 1_000_000)
            return Words(n / 1000) + " thousand" + (n % 1000 == 0 ? "" : " " + Words(n % 1000));
        return Words(n / 1_000_000) + " million" + (n % 1_000_000 == 0 ? "" : " " + Words(n % 1_000_000));
    }

    static string OrdinalWords(int n)
    {
        var words = Words(n);
        var cut = Math.Max(words.LastIndexOf(' '), words.LastIndexOf('-')) + 1;
        var last = words[cut..];
        var ordinal = last switch
        {
            "one" => "first",
            "two" => "second",
            "three" => "third",
            "five" => "fifth",
            "eight" => "eighth",
            "nine" => "ninth",
            "twelve" => "twelfth",
            _ when last.EndsWith('y') => last[..^1] + "ieth",
            _ => last + "th",
        };
        return words[..cut] + ordinal;
    }

    static string Capitalized(string words) => words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];

    /// <summary>Hebrew numerals: letters add up (א = 1 … ת = 400), 15 and 16 written ט״ו and ט״ז.</summary>
    static string Hebrew1(int n)
    {
        if (n <= 0)
            return Decimal(n);
        (int Value, char Letter)[] table = [(400, 'ת'), (300, 'ש'), (200, 'ר'), (100, 'ק'), (90, 'צ'), (80, 'פ'), (70, 'ע'), (60, 'ס'), (50, 'נ'), (40, 'מ'), (30, 'ל'), (20, 'כ'), (10, 'י'), (9, 'ט'), (8, 'ח'), (7, 'ז'), (6, 'ו'), (5, 'ה'), (4, 'ד'), (3, 'ג'), (2, 'ב'), (1, 'א')];
        var sb = new StringBuilder();
        var rest = n;
        foreach (var (value, letter) in table)
        {
            if (value < 100 && rest is 15 or 16)
            {
                sb.Append(rest == 15 ? "טו" : "טז");
                break;
            }
            while (rest >= value)
            {
                sb.Append(letter);
                rest -= value;
            }
        }
        return sb.ToString();
    }

    /// <summary>Chinese numerals: 一 … 十, 十一, 二十 …, 百 and 千 for larger numbers.</summary>
    static string Chinese(int n)
    {
        const string digits = "〇一二三四五六七八九";
        if (n < 0)
            return Decimal(n);
        if (n < 10)
            return digits[n].ToString();
        if (n < 100)
            return (n / 10 == 1 ? "" : digits[n / 10].ToString()) + "十" + (n % 10 == 0 ? "" : digits[n % 10].ToString());
        if (n < 1000)
        {
            var rest = n % 100;
            return digits[n / 100] + "百" + (rest == 0 ? "" : rest < 10 ? "〇" + digits[rest] : rest < 20 ? "一" + Chinese(rest) : Chinese(rest));
        }
        if (n < 10000)
        {
            var rest = n % 1000;
            return digits[n / 1000] + "千" + (rest == 0 ? "" : rest < 100 ? "〇" + Chinese(rest) : Chinese(rest));
        }
        return Decimal(n);
    }
}
