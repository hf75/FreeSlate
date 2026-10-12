namespace FreeSlate.Docx;

/// <summary>
/// Numbers in words and ordinal numbers as Word writes them for the formats cardinalText, ordinalText and ordinal
/// (catalog: numbering.definitions), in the language of the text: English, German, French and Russian; other languages
/// take English. Words only for numbers below a million, as in Word (measured with docx/external/powertools/LIR001 …
/// LIR007, LIR015 and docx/local/numwords2-*: every number from 1 to 1200 and samples up to 1000004 in each language
/// and format exactly as Word writes them).
/// </summary>
static class NumberWords
{
    /// <summary>The number in words, its first letter capitalized; nothing from a million on.</summary>
    public static string Cardinal(int n, string? language)
    {
        if (n < 0 || n >= 1_000_000)
            return "";
        return Capitalized(Language(language) switch
        {
            "de" => GermanCardinal(n),
            "fr" => FrenchCardinal(n),
            "ru" => RussianCardinal(n),
            _ => EnglishCardinal(n),
        });
    }

    /// <summary>The ordinal number in words, its first letter capitalized; nothing from a million on.</summary>
    public static string OrdinalText(int n, string? language)
    {
        if (n <= 0 || n >= 1_000_000)
            return "";
        return Capitalized(Language(language) switch
        {
            "de" => GermanOrdinal(n),
            "fr" => FrenchOrdinal(n),
            "ru" => RussianOrdinal(n),
            _ => EnglishOrdinal(n),
        });
    }

    /// <summary>The ordinal number in digits: 1st, 1., 1er, 1-й.</summary>
    public static string Ordinal(int n, string? language)
    {
        var digits = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Language(language) switch
        {
            "de" => digits + ".",
            "fr" => digits + (n == 1 ? "er" : "e"),
            "ru" => digits + "-й",
            _ => digits + ((n % 100) switch
            {
                11 or 12 or 13 => "th",
                _ => (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" },
            }),
        };
    }

    static string Language(string? tag) =>
        tag is { Length: >= 2 } ? tag.Split('-')[0].ToLowerInvariant() : "en";

    static string Capitalized(string words) => words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];

    // English.

    static readonly string[] EnglishOnes = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
        "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];
    static readonly string[] EnglishTens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];

    /// <summary>"one hundred twenty-three", "three thousand five hundred".</summary>
    static string EnglishCardinal(int n)
    {
        if (n < 20)
            return EnglishOnes[n];
        if (n < 100)
            return EnglishTens[n / 10] + (n % 10 == 0 ? "" : "-" + EnglishOnes[n % 10]);
        if (n < 1000)
            return EnglishOnes[n / 100] + " hundred" + (n % 100 == 0 ? "" : " " + EnglishCardinal(n % 100));
        return EnglishCardinal(n / 1000) + " thousand" + (n % 1000 == 0 ? "" : " " + EnglishCardinal(n % 1000));
    }

    static string EnglishOrdinal(int n)
    {
        var words = EnglishCardinal(n);
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

    // German: written as one word; "ein" before "und", "hundert" and "tausend", "eins" at the end; a hundred alone is
    // "hundert", after thousands "einhundert"; ordinals end in -te below twenty (erste, dritte, siebente, achte) and
    // -ste from twenty on and after hundert and tausend.

    static readonly string[] GermanOnes = ["null", "eins", "zwei", "drei", "vier", "fünf", "sechs", "sieben", "acht", "neun",
        "zehn", "elf", "zwölf", "dreizehn", "vierzehn", "fünfzehn", "sechzehn", "siebzehn", "achtzehn", "neunzehn"];
    static readonly string[] GermanTens = ["", "", "zwanzig", "dreißig", "vierzig", "fünfzig", "sechzig", "siebzig", "achtzig", "neunzig"];

    static string GermanBelow100(int n, bool last)
    {
        if (n < 20)
            return n == 1 && !last ? "ein" : GermanOnes[n];
        var (tens, ones) = (n / 10, n % 10);
        return ones == 0 ? GermanTens[tens] : (ones == 1 ? "ein" : GermanOnes[ones]) + "und" + GermanTens[tens];
    }

    static string GermanBelow1000(int n, bool last, bool afterThousands)
    {
        var (hundreds, rest) = (n / 100, n % 100);
        var text = hundreds == 0 ? "" : (hundreds == 1 ? afterThousands ? "ein" : "" : GermanOnes[hundreds]) + "hundert";
        return rest == 0 ? text : text + GermanBelow100(rest, last);
    }

    static string GermanCardinal(int n)
    {
        if (n == 0)
            return GermanOnes[0];
        var (thousands, rest) = (n / 1000, n % 1000);
        var text = thousands == 0 ? "" : (thousands == 1 ? "ein" : GermanBelow1000(thousands, false, false)) + "tausend";
        return rest == 0 ? text : text + GermanBelow1000(rest, true, thousands > 0);
    }

    static string GermanOrdinal(int n)
    {
        var words = GermanCardinal(n);
        var below100 = n % 100;
        if (below100 == 0 || below100 >= 20)
            return words + "ste";
        foreach (var (cardinal, ordinal) in GermanIrregular)
        {
            if (words.EndsWith(cardinal, StringComparison.Ordinal))
                return words[..^cardinal.Length] + ordinal;
        }
        return words + "te";
    }

    static readonly (string Cardinal, string Ordinal)[] GermanIrregular =
        [("eins", "erste"), ("drei", "dritte"), ("sieben", "siebente"), ("acht", "achte")];

    // French (the spelling before 1990): hyphens below a hundred, "et un" and "et onze", quatre-vingts and cents
    // plural only at the end; ordinals premier, cinquième, neuvième, -ième dropping a final e.

    static readonly string[] FrenchOnes = ["zéro", "un", "deux", "trois", "quatre", "cinq", "six", "sept", "huit", "neuf", "dix",
        "onze", "douze", "treize", "quatorze", "quinze", "seize"];
    static readonly string[] FrenchTens = ["", "", "vingt", "trente", "quarante", "cinquante", "soixante"];

    static string FrenchBelow100(int n)
    {
        if (n <= 16)
            return FrenchOnes[n];
        if (n < 20)
            return "dix-" + FrenchOnes[n - 10];
        if (n < 70)
        {
            var (tens, ones) = (n / 10, n % 10);
            return ones switch { 0 => FrenchTens[tens], 1 => FrenchTens[tens] + " et un", _ => FrenchTens[tens] + "-" + FrenchOnes[ones] };
        }
        if (n < 80)
            return n == 71 ? "soixante et onze" : "soixante-" + FrenchBelow100(n - 60);
        return n == 80 ? "quatre-vingts" : "quatre-vingt-" + FrenchBelow100(n - 80);
    }

    static string FrenchBelow1000(int n)
    {
        var (hundreds, rest) = (n / 100, n % 100);
        var parts = new List<string>();
        if (hundreds == 1)
            parts.Add("cent");
        else if (hundreds > 1)
            parts.Add(FrenchOnes[hundreds] + (rest == 0 ? " cents" : " cent"));
        if (rest > 0)
            parts.Add(FrenchBelow100(rest));
        return string.Join(' ', parts);
    }

    static string FrenchCardinal(int n)
    {
        if (n == 0)
            return FrenchOnes[0];
        var (thousands, rest) = (n / 1000, n % 1000);
        var parts = new List<string>();
        if (thousands == 1)
            parts.Add("mille");
        else if (thousands > 1)
            parts.Add(FrenchBelow1000(thousands).Replace("quatre-vingts", "quatre-vingt").Replace(" cents", " cent") + " mille");
        if (rest > 0)
            parts.Add(FrenchBelow1000(rest));
        return string.Join(' ', parts);
    }

    static string FrenchOrdinal(int n)
    {
        if (n == 1)
            return "premier";
        var words = FrenchCardinal(n);
        if (words.EndsWith("cinq", StringComparison.Ordinal))
            return words + "uième";
        if (words.EndsWith("neuf", StringComparison.Ordinal))
            return words[..^1] + "vième";
        if (words.EndsWith('s') && (words.EndsWith("quatre-vingts", StringComparison.Ordinal) || words.EndsWith("cents", StringComparison.Ordinal)))
            return words[..^1] + "ième";
        return (words.EndsWith('e') ? words[..^1] : words) + "ième";
    }

    // Russian: one and two thousand feminine (одна, две тысячи), тысяча/тысячи/тысяч by the last digits, "одна
    // тысяча" from 1001 on; ordinals turn the last word, round thousands are one compound word in the genitive
    // (двухтысячный, двадцатиоднотысячный, стотысячный), ё written е.

    static readonly string[] RussianOnes = ["", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять", "десять",
        "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать",
        "девятнадцать"];
    static readonly string[] RussianTens = ["", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто"];
    static readonly string[] RussianHundreds = ["", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот"];
    static readonly string[] RussianOrdinalOnes = ["", "первый", "второй", "третий", "четвертый", "пятый", "шестой", "седьмой", "восьмой",
        "девятый", "десятый", "одиннадцатый", "двенадцатый", "тринадцатый", "четырнадцатый", "пятнадцатый", "шестнадцатый",
        "семнадцатый", "восемнадцатый", "девятнадцатый"];
    static readonly string[] RussianOrdinalTens = ["", "", "двадцатый", "тридцатый", "сороковой", "пятидесятый", "шестидесятый", "семидесятый",
        "восьмидесятый", "девяностый"];
    static readonly string[] RussianOrdinalHundreds = ["", "сотый", "двухсотый", "трехсотый", "четырехсотый", "пятисотый", "шестисотый",
        "семисотый", "восьмисотый", "девятисотый"];
    static readonly string[] RussianGenitiveOnes = ["", "одно", "двух", "трех", "четырех", "пяти", "шести", "семи", "восьми", "девяти",
        "десяти", "одиннадцати", "двенадцати", "тринадцати", "четырнадцати", "пятнадцати", "шестнадцати", "семнадцати",
        "восемнадцати", "девятнадцати"];
    static readonly string[] RussianGenitiveTens = ["", "", "двадцати", "тридцати", "сорока", "пятидесяти", "шестидесяти", "семидесяти",
        "восьмидесяти", "девяноста"];
    static readonly string[] RussianGenitiveHundreds = ["", "сто", "двухсот", "трехсот", "четырехсот", "пятисот", "шестисот", "семисот",
        "восьмисот", "девятисот"];

    static List<string> RussianBelow1000(int n)
    {
        var (hundreds, rest) = (n / 100, n % 100);
        var parts = new List<string>();
        if (hundreds > 0)
            parts.Add(RussianHundreds[hundreds]);
        if (rest is > 0 and < 20)
            parts.Add(RussianOnes[rest]);
        else if (rest >= 20)
        {
            parts.Add(RussianTens[rest / 10]);
            if (rest % 10 > 0)
                parts.Add(RussianOnes[rest % 10]);
        }
        return parts;
    }

    static List<string> RussianThousands(int thousands)
    {
        var parts = RussianBelow1000(thousands);
        if (parts.Count > 0 && parts[^1] == "один")
            parts[^1] = "одна";
        else if (parts.Count > 0 && parts[^1] == "два")
            parts[^1] = "две";
        parts.Add(thousands % 100 is >= 11 and <= 14 || thousands % 10 is 0 or >= 5 ? "тысяч" : thousands % 10 == 1 ? "тысяча" : "тысячи");
        return parts;
    }

    static string RussianCardinal(int n)
    {
        if (n == 0)
            return "ноль";
        var parts = new List<string>();
        if (n >= 1000)
            parts.AddRange(RussianThousands(n / 1000));
        parts.AddRange(RussianBelow1000(n % 1000));
        return string.Join(' ', parts);
    }

    static string RussianOrdinal(int n)
    {
        var (thousands, rest) = (n / 1000, n % 1000);
        if (thousands > 0 && rest == 0)
            return (thousands == 1 ? "" : RussianGenitive(thousands)) + "тысячный";
        var parts = thousands > 0 ? RussianThousands(thousands) : [];
        var (hundreds, below100) = (rest / 100, rest % 100);
        if (below100 == 0)
            parts.Add(RussianOrdinalHundreds[hundreds]);
        else
        {
            if (hundreds > 0)
                parts.Add(RussianHundreds[hundreds]);
            if (below100 < 20)
                parts.Add(RussianOrdinalOnes[below100]);
            else if (below100 % 10 == 0)
                parts.Add(RussianOrdinalTens[below100 / 10]);
            else
            {
                parts.Add(RussianTens[below100 / 10]);
                parts.Add(RussianOrdinalOnes[below100 % 10]);
            }
        }
        return string.Join(' ', parts);
    }

    /// <summary>The first part of the compound ordinal of round thousands: двух-, двадцатиодно-, сто-.</summary>
    static string RussianGenitive(int n)
    {
        var (hundreds, rest) = (n / 100, n % 100);
        var text = RussianGenitiveHundreds[hundreds];
        return rest < 20 ? text + RussianGenitiveOnes[rest] : text + RussianGenitiveTens[rest / 10] + RussianGenitiveOnes[rest % 10];
    }
}
