using System.Globalization;
using System.Text;
using FreeSlate.Drawing;

namespace FreeSlate.Pptx.Charts;

/// <summary>
/// Formats numbers with spreadsheet number format codes (ECMA-376 Part 1, §18.8.31): sections, literals, digit
/// placeholders, grouping, scaling, percent, scientific notation and basic date codes (catalog: chart.numberFormat).
/// </summary>
public static class NumberFormat
{
    public static string Format(double value, string? code, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.InvariantCulture;
        if (double.IsNaN(value) || double.IsInfinity(value))
            return "";

        // Built-in formats 14 and 22 show the short date of the user's locale ("m/d/yyyy" is only their name).
        if (code is "m/d/yyyy" or "m/d/yyyy h:mm" && value is > -657435 and < 2958466)
        {
            var date = DateTime.FromOADate(value);
            var pattern = culture.DateTimeFormat.ShortDatePattern + (code.EndsWith("h:mm", StringComparison.Ordinal) ? " " + culture.DateTimeFormat.ShortTimePattern : "");
            return date.ToString(pattern, culture);
        }
        // Built-in format 17 is localized too: German "MMM JJ" (measured: "Okt 12" for mmm\-yy on a German system).
        if (code is "mmm\\-yy" or "mmm-yy" && culture.TwoLetterISOLanguageName == "de")
            code = "mmm yy";
        var (section, minus) = Pick(ref value, Sections(string.IsNullOrWhiteSpace(code) ? "General" : code));
        section = StripBrackets(section);
        string text;
        // A German or French Office writes General by its own name into the data of its charts (measured: LibreOffice's
        // forum-mso-de-138303, formatCode="Standard" in a waterfall's values).
        if (section.Trim() is var name && (name.Equals("General", StringComparison.OrdinalIgnoreCase) || name.Equals("Standard", StringComparison.OrdinalIgnoreCase))
            || section.Length == 0)
            text = General(value, culture);
        else if (IsDate(section))
            text = Date(value, section, culture);
        else
            text = Number(value, section, culture);
        return minus ? "-" + text : text;
    }

    /// <summary>
    /// The section that formats a value and whether its text takes a minus; the value becomes the one the section shows.
    /// Sections: positive;negative;zero;text. Only a single section prefixes negative numbers with a minus. Sections
    /// with conditions ([&lt;&gt;0], [&gt;=100]) take the numbers that meet them, the next section the others, the minus kept
    /// (measured with docx/local/csx-val: [&lt;&gt;0]General;\ "2011-12 $/t" labels 0 "2011-12 $/t"; LibreOffice's
    /// extras-layout/tdf138773).
    /// </summary>
    static (string Section, bool Minus) Pick(ref double value, List<string> sections)
    {
        if (Condition(sections[0]) is { } first)
        {
            var second = sections.Count >= 2 ? Condition(sections[1]) : null;
            var section = first(value) ? sections[0]
                : sections.Count >= 2 && (second == null || second(value)) ? sections[1]
                : sections.Count >= 3 ? sections[2] : sections[^1];
            var minus = value < 0;
            value = Math.Abs(value);
            return (section, minus);
        }
        if (value < 0 && sections.Count >= 2)
        {
            value = -value;
            return (sections[1], false);
        }
        if (value == 0 && sections.Count >= 3)
            return (sections[2], false);
        var negative = value < 0;
        value = Math.Abs(value);
        return (sections[0], negative);
    }

    /// <summary>
    /// The color the format code gives a value: [Black], [Blue], [Cyan], [Green], [Magenta], [Red], [White], [Yellow] or
    /// [Color1] to [Color56] in the section that formats it (measured: LibreOffice's chart2/tdf137691_dataTable, value
    /// axis labels of "$"#,##0_);[Red]\("$"#,##0\) red below zero); null where it names none.
    /// </summary>
    public static RgbColor? Color(double value, string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || !code.Contains('['))
            return null;
        var (section, _) = Pick(ref value, Sections(code));
        var quoted = false;
        for (var i = 0; i < section.Length; i++)
        {
            if (section[i] == '"')
                quoted = !quoted;
            if (section[i] != '[' || quoted)
                continue;
            var end = section.IndexOf(']', i);
            if (end < 0)
                break;
            var token = section[(i + 1)..end].ToLowerInvariant();
            var hex = token switch
            {
                "black" => "000000", "blue" => "0000FF", "cyan" => "00FFFF", "green" => "00FF00",
                "magenta" => "FF00FF", "red" => "FF0000", "white" => "FFFFFF", "yellow" => "FFFF00",
                _ when token.StartsWith("color", StringComparison.Ordinal) && int.TryParse(token[5..], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                       && n is >= 1 and <= 56 => Palette[n - 1],
                _ => null,
            };
            if (hex != null)
                return RgbColor.FromHex(hex);
            i = end;
        }
        return null;
    }

    /// <summary>[Color1] to [Color56]: the spreadsheet's default palette (ECMA-376 Part 1, §18.8.27, indexed colors 8–63).</summary>
    static readonly string[] Palette =
    [
        "000000", "FFFFFF", "FF0000", "00FF00", "0000FF", "FFFF00", "FF00FF", "00FFFF", "800000", "008000", "000080", "808000",
        "800080", "008080", "C0C0C0", "808080", "9999FF", "993366", "FFFFCC", "CCFFFF", "660066", "FF8080", "0066CC", "CCCCFF",
        "000080", "FF00FF", "FFFF00", "00FFFF", "800080", "800000", "008080", "0000FF", "00CCFF", "CCFFFF", "CCFFCC", "FFFF99",
        "99CCFF", "FF99CC", "CC99FF", "FFCC99", "3366FF", "33CCCC", "99CC00", "FFCC00", "FF9900", "FF6600", "666699", "969696",
        "003366", "339966", "003300", "333300", "993300", "993366", "333399", "333333",
    ];

    /// <summary>Excel's General format: up to ten significant digits, scientific notation for very large or small values.</summary>
    public static string General(double value, CultureInfo culture)
    {
        if (value == 0)
            return "0";
        var abs = Math.Abs(value);
        if (abs >= 1e11 || abs < 1e-9)
            return value.ToString("0.#####E+00", culture);
        var magnitude = (int)Math.Floor(Math.Log10(abs));
        var decimals = Math.Clamp(9 - magnitude, 0, 15);
        return Math.Round(value, decimals).ToString("0." + new string('#', Math.Max(1, decimals)), culture);
    }

    static List<string> Sections(string code)
    {
        var sections = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        var bracket = false;
        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            if (c == '"' && !bracket) quoted = !quoted;
            else if (c == '[' && !quoted) bracket = true;
            else if (c == ']' && !quoted) bracket = false;
            else if (c == '\\' && !quoted && i + 1 < code.Length) { current.Append(c).Append(code[++i]); continue; }
            else if (c == ';' && !quoted && !bracket) { sections.Add(current.ToString()); current.Clear(); continue; }
            current.Append(c);
        }
        sections.Add(current.ToString());
        return sections;
    }

    /// <summary>A section's condition ([&lt;&gt;0], [&gt;=100]) among its leading brackets, else null.</summary>
    static Func<double, bool>? Condition(string section)
    {
        var i = 0;
        while (i < section.Length && char.IsWhiteSpace(section[i]))
            i++;
        while (i < section.Length && section[i] == '[' && section.IndexOf(']', i) is var end and > 0)
        {
            var token = section[(i + 1)..end].Trim();
            var op = token.StartsWith("<=") || token.StartsWith(">=") || token.StartsWith("<>") ? token[..2]
                : token.Length > 0 && token[0] is '<' or '>' or '=' ? token[..1] : null;
            if (op != null && double.TryParse(token[op.Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out var bound))
                return op switch
                {
                    "<=" => v => v <= bound,
                    ">=" => v => v >= bound,
                    "<>" => v => v != bound,
                    "<" => v => v < bound,
                    ">" => v => v > bound,
                    _ => v => v == bound,
                };
            i = end + 1;
        }
        return null;
    }

    /// <summary>Removes [colors] and [conditions]; [$currency-locale] becomes the quoted currency text.</summary>
    static string StripBrackets(string section)
    {
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < section.Length; i++)
        {
            var c = section[i];
            if (c == '"') quoted = !quoted;
            if (c == '[' && !quoted)
            {
                var end = section.IndexOf(']', i);
                if (end < 0) break;
                var token = section[(i + 1)..end];
                if (token.StartsWith('$'))
                    sb.Append('"').Append(token[1..].Split('-')[0]).Append('"');
                else if (token.Length > 0 && char.ToLowerInvariant(token[0]) is 'h' or 'm' or 's')
                {
                    sb.Append(token);   // elapsed time; treated like the plain code
                }
                i = end;
                continue;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Whether a format code shows dates or times (its first section has date or time codes).</summary>
    public static bool IsDateFormat(string? code) =>
        !string.IsNullOrWhiteSpace(code) && (code is "m/d/yyyy" or "m/d/yyyy h:mm" || IsDate(StripBrackets(Sections(code)[0])));

    static bool IsDate(string section)
    {
        var quoted = false;
        var hasDigits = false;
        var hasDate = false;
        for (var i = 0; i < section.Length; i++)
        {
            var c = section[i];
            if (c == '"') { quoted = !quoted; continue; }
            if (quoted) continue;
            if (c == '\\') { i++; continue; }
            switch (char.ToLowerInvariant(c))
            {
                case '0' or '#' or '?': hasDigits = true; break;
                case 'y' or 'd' or 'h' or 's' or 'm': hasDate = true; break;
                case 'e' when i + 1 < section.Length && section[i + 1] is '+' or '-': hasDigits = true; break;
            }
        }
        return hasDate && !hasDigits;
    }

    static string Number(double value, string section, CultureInfo culture)
    {
        // Literals before and after one numeric pattern; digits after the pattern has ended are kept as text.
        var before = new StringBuilder();
        var after = new StringBuilder();
        var pattern = new StringBuilder();
        var quoted = false;
        var percent = 0;
        StringBuilder Literal() => pattern.Length == 0 ? before : after;
        for (var i = 0; i < section.Length; i++)
        {
            var c = section[i];
            if (c == '"') { quoted = !quoted; continue; }
            if (quoted) { Literal().Append(c); continue; }
            switch (c)
            {
                case '\\' when i + 1 < section.Length:
                    Literal().Append(section[++i]);
                    break;
                case '_' when i + 1 < section.Length:
                    i++;
                    Literal().Append(' ');
                    break;
                case '*' when i + 1 < section.Length:
                    i++;
                    break;
                case '%':
                    percent++;
                    Literal().Append('%');
                    break;
                case '0' or '#' or '?' or '.' or ',' when after.Length == 0:
                    pattern.Append(c);
                    break;
                case 'E' or 'e' when i + 1 < section.Length && section[i + 1] is '+' or '-' && pattern.Length > 0 && after.Length == 0:
                    pattern.Append('E').Append(section[++i]);
                    while (i + 1 < section.Length && section[i + 1] is '0' or '#' or '?')
                        pattern.Append(section[++i]);
                    break;
                case '@':
                    break;
                default:
                    Literal().Append(c);
                    break;
            }
        }

        value *= Math.Pow(100, percent);
        var p = pattern.ToString();
        var number = p.Length == 0 ? "" : p.Contains('E') ? Scientific(value, p, culture) : Fixed(value, p, culture);
        return before + number + after;
    }

    static string Fixed(double value, string pattern, CultureInfo culture)
    {
        // Trailing commas scale by 1000 each; commas between digit placeholders turn on grouping.
        var scale = 0;
        while (pattern.EndsWith(',')) { scale++; pattern = pattern[..^1]; }
        var dot = pattern.IndexOf('.');
        var integer = dot < 0 ? pattern : pattern[..dot];
        var fraction = dot < 0 ? "" : pattern[(dot + 1)..].Replace(",", "");
        while (integer.EndsWith(',')) { scale++; integer = integer[..^1]; }
        var grouping = integer.Contains(',');
        integer = integer.Replace(",", "");
        value /= Math.Pow(1000, scale);

        var decimals = fraction.Length;
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        var digits = rounded.ToString("F" + decimals, CultureInfo.InvariantCulture);
        var parts = digits.Split('.');
        var intDigits = parts[0];
        var fracDigits = parts.Length > 1 ? parts[1] : "";

        var minInt = integer.Count(c => c == '0');
        if (intDigits == "0" && minInt == 0)
            intDigits = integer.Contains('?') ? " " : "";
        while (intDigits.Length < minInt) intDigits = "0" + intDigits;
        if (grouping && intDigits.Trim().Length > 0)
        {
            var group = culture.NumberFormat.NumberGroupSeparator;
            var sb = new StringBuilder();
            for (var i = 0; i < intDigits.Length; i++)
            {
                if (i > 0 && (intDigits.Length - i) % 3 == 0) sb.Append(group);
                sb.Append(intDigits[i]);
            }
            intDigits = sb.ToString();
        }

        // Optional fraction digits (#, ?) drop trailing zeros; ? pads with spaces.
        var chars = fracDigits.ToCharArray();
        for (var i = chars.Length - 1; i >= 0 && fraction[i] != '0' && chars[i] == '0'; i--)
            chars[i] = fraction[i] == '?' ? ' ' : '\0';
        var frac = new string(chars.Where(c => c != '\0').ToArray());

        // Like Excel, a pattern with only optional fraction digits keeps the separator ("0.#" shows 1 as "1.").
        return fraction.Length == 0 ? intDigits : intDigits + culture.NumberFormat.NumberDecimalSeparator + frac;
    }

    static string Scientific(double value, string pattern, CultureInfo culture)
    {
        var e = pattern.IndexOf('E');
        var mantissa = pattern[..e];
        var sign = pattern[e + 1];
        var expDigits = Math.Max(1, pattern.Length - e - 2);
        var exponent = value == 0 ? 0 : (int)Math.Floor(Math.Log10(Math.Abs(value)));
        var intPlaces = Math.Max(1, (mantissa.Split('.')[0]).Count(c => c is '0' or '#' or '?'));
        if (intPlaces > 1)
            exponent = (int)Math.Floor((double)exponent / intPlaces) * intPlaces;
        var m = value / Math.Pow(10, exponent);
        var text = Fixed(m, mantissa, culture);
        var exp = Math.Abs(exponent).ToString(new string('0', expDigits), CultureInfo.InvariantCulture);
        return text + "E" + (exponent < 0 ? "-" : sign == '+' ? "+" : "") + exp;
    }

    /// <summary>
    /// A month's abbreviation (mmm) as Office writes it: Windows' own abbreviations, which differ from .NET's for
    /// German (Mrz, Jun, Sep; measured with a German Office: "Mrz 23").
    /// </summary>
    static string AbbreviatedMonth(int month, CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
        "de" => GermanMonths[month - 1],
        _ => culture.DateTimeFormat.GetAbbreviatedMonthName(month).TrimEnd('.'),
    };

    static readonly string[] GermanMonths = ["Jan", "Feb", "Mrz", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez"];

    static string Date(double serial, string section, CultureInfo culture)
    {
        DateTime date;
        try { date = DateTime.FromOADate(serial); }
        catch (ArgumentException) { return General(serial, culture); }

        var sb = new StringBuilder();
        var quoted = false;
        var hasAmPm = section.Contains("AM/PM", StringComparison.OrdinalIgnoreCase) || section.Contains("A/P", StringComparison.OrdinalIgnoreCase);
        var lastWasHour = false;
        for (var i = 0; i < section.Length; i++)
        {
            var c = section[i];
            if (c == '"') { quoted = !quoted; continue; }
            if (quoted) { sb.Append(c); continue; }
            if (c == '\\' && i + 1 < section.Length) { sb.Append(section[++i]); continue; }
            var lower = char.ToLowerInvariant(c);
            var run = 1;
            while (i + run < section.Length && char.ToLowerInvariant(section[i + run]) == lower) run++;
            switch (lower)
            {
                case 'y':
                    sb.Append(run <= 2 ? (date.Year % 100).ToString("00", culture) : date.Year.ToString(culture));
                    break;
                case 'm':
                    // Minutes after an hour or before seconds, months otherwise.
                    var rest = section[(i + run)..].TrimStart(':', ' ');
                    if (lastWasHour || rest.StartsWith('s') || rest.StartsWith('S'))
                        sb.Append(run >= 2 ? date.Minute.ToString("00", culture) : date.Minute.ToString(culture));
                    else
                        sb.Append(run switch
                        {
                            1 => date.Month.ToString(culture),
                            2 => date.Month.ToString("00", culture),
                            3 => AbbreviatedMonth(date.Month, culture),
                            5 => culture.DateTimeFormat.GetMonthName(date.Month)[..1],
                            _ => culture.DateTimeFormat.GetMonthName(date.Month),
                        });
                    break;
                case 'd':
                    sb.Append(run switch
                    {
                        1 => date.Day.ToString(culture),
                        2 => date.Day.ToString("00", culture),
                        3 => culture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek),
                        _ => culture.DateTimeFormat.GetDayName(date.DayOfWeek),
                    });
                    break;
                case 'h':
                    var hour = hasAmPm ? (date.Hour % 12 == 0 ? 12 : date.Hour % 12) : date.Hour;
                    sb.Append(run >= 2 ? hour.ToString("00", culture) : hour.ToString(culture));
                    break;
                case 's':
                    sb.Append(run >= 2 ? date.Second.ToString("00", culture) : date.Second.ToString(culture));
                    break;
                case 'a' when section.Length >= i + 5 && section.Substring(i, 5).Equals("AM/PM", StringComparison.OrdinalIgnoreCase):
                    sb.Append(date.Hour < 12 ? "AM" : "PM");
                    run = 5;
                    break;
                case 'a' when section.Length >= i + 3 && section.Substring(i, 3).Equals("A/P", StringComparison.OrdinalIgnoreCase):
                    sb.Append(date.Hour < 12 ? "A" : "P");
                    run = 3;
                    break;
                default:
                    sb.Append(section, i, run);
                    break;
            }
            if (lower != ':' && lower != ' ')
                lastWasHour = lower == 'h';
            i += run - 1;
        }
        return sb.ToString();
    }
}
