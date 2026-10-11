using System.Collections.Concurrent;
using System.IO.Compression;

namespace FreeSlate.Text;

/// <summary>
/// Hyphenation by Liang's patterns (catalog: paragraph.hyphenation): where a word may break, from the patterns and
/// exceptions of its language (the hyph-utf8 patterns of TeX: German in the reformed spelling, American and British
/// English; see Resources/NOTICE.md), keeping as many letters before and after the break as the patterns ask.
/// </summary>
public sealed class Hyphenator
{
    readonly Dictionary<string, byte[]> patterns = new(StringComparer.Ordinal);
    readonly Dictionary<string, int[]> exceptions = new(StringComparer.Ordinal);
    readonly int maxPattern;

    /// <summary>The fewest letters before and after a break.</summary>
    public int Left { get; }
    public int Right { get; }

    static readonly ConcurrentDictionary<string, Hyphenator?> loaded = new(StringComparer.Ordinal);

    Hyphenator(string data)
    {
        var lines = data.Split('\n');
        var mins = lines[0].Split(' ');
        (Left, Right) = (int.Parse(mins[0]), int.Parse(mins[1]));
        foreach (var pattern in lines.Length > 1 ? lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries) : [])
        {
            var letters = new System.Text.StringBuilder();
            var values = new List<byte> { 0 };
            foreach (var c in pattern)
            {
                if (c is >= '0' and <= '9')
                    values[^1] = (byte)(c - '0');
                else
                {
                    letters.Append(c);
                    values.Add(0);
                }
            }
            patterns[letters.ToString()] = [.. values];
            maxPattern = Math.Max(maxPattern, letters.Length);
        }
        foreach (var exception in lines.Length > 2 ? lines[2].Split(' ', StringSplitOptions.RemoveEmptyEntries) : [])
        {
            var points = new List<int>();
            var count = 0;
            foreach (var c in exception)
            {
                if (c == '-')
                    points.Add(count);
                else
                    count++;
            }
            exceptions[exception.Replace("-", "")] = [.. points];
        }
    }

    /// <summary>The hyphenator of a language (a BCP 47 tag such as de-DE, en-US), or null where there are no patterns for it.</summary>
    public static Hyphenator? For(string? language)
    {
        if (string.IsNullOrEmpty(language))
            return null;
        var tag = language.ToLowerInvariant();
        var resource = tag.StartsWith("de", StringComparison.Ordinal) ? "hyph-de.gz"
            : tag is "en-us" or "en" ? "hyph-en-us.gz"
            : tag.StartsWith("en", StringComparison.Ordinal) ? "hyph-en-gb.gz"
            : null;
        if (resource == null)
            return null;
        return loaded.GetOrAdd(resource, name =>
        {
            using var stream = typeof(Hyphenator).Assembly.GetManifestResourceStream(name);
            if (stream == null)
                return null;
            using var gzip = new GZipStream(stream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, System.Text.Encoding.UTF8);
            return new Hyphenator(reader.ReadToEnd());
        });
    }

    /// <summary>The places in a word (letters only) a hyphen may stand before, in order.</summary>
    public List<int> Points(string word)
    {
        var result = new List<int>();
        if (word.Length < Left + Right)
            return result;
        var lower = word.ToLowerInvariant();
        if (exceptions.TryGetValue(lower, out var known))
        {
            result.AddRange(known.Where(p => p >= Left && p <= word.Length - Right));
            return result;
        }
        var dotted = "." + lower + ".";
        var values = new byte[dotted.Length + 1];
        for (var start = 0; start < dotted.Length; start++)
        {
            for (var length = 1; length <= maxPattern && start + length <= dotted.Length; length++)
            {
                if (!patterns.TryGetValue(dotted.Substring(start, length), out var digits))
                    continue;
                for (var k = 0; k < digits.Length; k++)
                    values[start + k] = Math.Max(values[start + k], digits[k]);
            }
        }
        // values[k] stands before dotted[k]: before the word's letter k - 1.
        for (var p = Left; p <= word.Length - Right; p++)
        {
            if (values[p + 1] % 2 == 1)
                result.Add(p);
        }
        return result;
    }
}
