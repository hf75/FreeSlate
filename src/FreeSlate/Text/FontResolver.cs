using System.Collections.Concurrent;
using FreeSlate.Fonts;

namespace FreeSlate.Text;

/// <summary>
/// A font chosen for a run: the file to measure with and whether the browser has to synthesize the style;
/// <paramref name="WidenedBold"/>: bold of a PostScript font without a bold face, which PowerPoint simulates by wider
/// advances alone (<see cref="FontData.WidenedBold"/>), its glyphs drawn as they are.
/// </summary>
public sealed record ResolvedFont(FontData Data, bool SyntheticBold, bool SyntheticItalic, bool WidenedBold = false);

/// <summary>
/// Maps a typeface and style to a font file. Missing families fall back to Calibri like in PowerPoint, whatever
/// their panose or pitch family say (measured: serif, monospaced, script and unknown fonts all become Calibri), else
/// to its metric twin Carlito or a common sans serif; the fallback is written into the CSS, so the browser draws
/// with the font the layout was measured with.
/// </summary>
public sealed class FontResolver(FontLocator locator)
{
    static readonly string[] Fallbacks = ["Calibri", "Carlito", "Arial", "Liberation Sans", "DejaVu Sans"];

    /// <summary>Font files, shared by all resolvers; a presentation's embedded fonts stay with its resolver.</summary>
    static readonly ConcurrentDictionary<string, FontData> Files = new(StringComparer.OrdinalIgnoreCase);

    readonly ConcurrentDictionary<string, FontData> embedded = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<(string, bool, bool), ResolvedFont?> cache = new();

    public static FontResolver Default { get; } = new(FontLocator.Default);

    public ResolvedFont Resolve(string family, bool bold, bool italic) =>
        Find(family, bold, italic) ?? Fallbacks.Select(f => Find(f, bold, italic)).FirstOrDefault(f => f != null)
        ?? throw new InvalidOperationException("No usable font found (not even a fallback).");

    /// <summary>A typeface by its family's name or a name the family is known by (<see cref="Named"/>), or null.</summary>
    public ResolvedFont? TryResolveTypeface(string typeface, bool bold, bool italic) =>
        Find(typeface, bold, italic) ?? Named(typeface, bold, italic);

    /// <summary>A typeface by its family's own name only, or null.</summary>
    public ResolvedFont? TryResolveFamily(string family, bool bold, bool italic) => Find(family, bold, italic);

    /// <summary>The first of these families that is there, else the fallback.</summary>
    public ResolvedFont ResolveFirst(IEnumerable<string> families, bool bold, bool italic) =>
        families.Select(f => Find(f, bold, italic)).FirstOrDefault(f => f != null) ?? Resolve(families.FirstOrDefault() ?? "", bold, italic);

    /// <summary>A typeface of a presentation's text: the family, else the one PowerPoint takes for the name (<see cref="Named"/>), else the fallback.</summary>
    public ResolvedFont ResolveTypeface(string typeface, bool bold, bool italic) =>
        Find(typeface, bold, italic) ?? Named(typeface, bold, italic) ?? Resolve(typeface, bold, italic);

    /// <summary>
    /// The font for <paramref name="text"/>: the family (or the one PowerPoint takes for its name) if it is there and
    /// has every glyph, else the first of <paramref name="candidates"/> that has them, else that family (or the
    /// fallback) anyway.
    /// </summary>
    public ResolvedFont ResolveCovering(string family, bool bold, bool italic, string text, IEnumerable<string> candidates)
    {
        var found = Find(family, bold, italic) ?? Named(family, bold, italic);
        if (found != null && found.Data.Covers(text))
            return found;
        // A family that is not there at all is replaced like PowerPoint replaces it, if the substitute has the glyphs.
        if (found == null && Fallbacks.Select(f => Find(f, bold, italic)).FirstOrDefault(f => f != null) is { } substitute && substitute.Data.Covers(text))
            return substitute;
        foreach (var candidate in candidates)
        {
            if (Find(candidate, bold, italic) is { } alternative && alternative.Data.Covers(text))
                return alternative;
        }
        return found ?? Resolve(family, bold, italic);
    }

    /// <summary>Names PowerPoint takes for a family whatever follows them (measured with installed fonts).</summary>
    static readonly (string Prefix, string Family)[] NamePrefixes =
    [
        ("Arial", "Arial"), ("Helv", "Arial"), ("Times", "Times New Roman"), ("Courier", "Courier New"),
        ("Segoe", "Segoe UI"), ("Palatino", "Palatino Linotype"), ("Lucida Console", "Consolas"),
        ("Liberation Sans", "Arial"), ("Liberation Serif", "Times New Roman"), ("DejaVu Sans", "Verdana"),
    ];

    static readonly string[] StyleWords = ["Bold", "Italic", "Oblique", "Regular", "Light", "Semibold", "Medium", "Black", "Narrow", "Condensed"];

    /// <summary>
    /// The family PowerPoint draws for a typeface that is no family's name, or null (then it draws Calibri), measured
    /// with installed fonts. In this order: a style word run on is dropped (TahomaBold, VerdanaBold, Lucida ConsoleBold:
    /// the family, regular); a name beginning with Arial, Helv, Times, Courier, Segoe, Palatino or Lucida Console takes
    /// a family whatever follows (ArialMT, Arial-BoldMT, ArialNarrow, Helvetica Neue, TimesNewRomanPSMT, SegoeUI,
    /// Segoe UI Bold, Lucida ConsoleX: Arial, Times New Roman, Segoe UI, Consolas, in the run's style), as do the free
    /// families Liberation Sans, Liberation Serif and DejaVu Sans (Arial, Times New Roman, Verdana; Liberation Mono,
    /// DejaVu Serif, Arimo, Tinos, Carlito and Caladea stay unmatched); a trailing
    /// " Bold" or " Italic" sets the style (Cambria Bold); what follows a hyphen is dropped (Georgia-Bold, Tahoma-Foo:
    /// the family, regular). Other names stay unmatched (TahomaX, Tahoma Foo, Verdanaa, CambriaMath, Consola, XArial).
    /// </summary>
    ResolvedFont? Named(string typeface, bool bold, bool italic)
    {
        foreach (var word in StyleWords)
        {
            if (typeface.Length > word.Length && typeface.EndsWith(word, StringComparison.OrdinalIgnoreCase)
                && Find(typeface[..^word.Length], bold, italic) is { } runOn)
                return runOn;
        }
        foreach (var (prefix, family) in NamePrefixes)
        {
            if (typeface.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return Find(family, bold, italic);
        }
        var words = typeface.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var count = words.Length;
        var (styledBold, styledItalic) = (bold, italic);
        while (count > 1 && (words[count - 1].Equals("Bold", StringComparison.OrdinalIgnoreCase) || words[count - 1].Equals("Italic", StringComparison.OrdinalIgnoreCase)))
        {
            styledBold |= words[count - 1].Equals("Bold", StringComparison.OrdinalIgnoreCase);
            styledItalic |= words[count - 1].Equals("Italic", StringComparison.OrdinalIgnoreCase);
            count--;
        }
        if (count < words.Length && Find(string.Join(' ', words[..count]), styledBold, styledItalic) is { } styled)
            return styled;
        if (typeface.IndexOf('-') is > 0 and var hyphen && Find(typeface[..hyphen], bold, italic) is { } beforeHyphen)
            return beforeHyphen;
        return null;
    }

    ResolvedFont? Find(string family, bool bold, bool italic) => cache.GetOrAdd((family, bold, italic), key =>
    {
        // Embedded fonts that cannot be decoded are skipped.
        var faces = locator.Faces(key.Item1).Where(f => f.Embedded == null || f.Embedded.Value != null).ToList();
        if (faces.Count == 0)
            return null;
        var exact = faces.FirstOrDefault(f => f.Bold == key.Item2 && f.Italic == key.Item3);
        var face = exact ?? faces.FirstOrDefault(f => !f.Bold && !f.Italic) ?? faces[0];
        var data = face.Embedded?.Value is { } bytes
            ? embedded.GetOrAdd(face.Path, _ => new FontData(bytes, face.Path, face.Family))
            : Files.GetOrAdd($"{face.Path}#{face.Index}", _ => new FontData(face.Path, face.Family, face.Index));
        // Bold of a variable font is its weight axis at 700 (the Bold instance), also where the presentation embeds the
        // same file as its bold face: PowerPoint shows it so (seen with an embedded Wix Madefor Display).
        if (key.Item2 && data.WeightAxis is { Default: < 600, Max: >= 600 })
        {
            var weighted = (face.Embedded != null ? embedded : Files).GetOrAdd($"{face.Path}#{face.Index}#wght700", _ => data.Weighted(700));
            return new ResolvedFont(weighted, false, key.Item3 && !face.Italic);
        }
        if (key.Item2 && !face.Bold && data.IsPostScript)
        {
            var widened = (face.Embedded != null ? embedded : Files).GetOrAdd($"{face.Path}#{face.Index}#widened", _ => data.Widened(FontData.WidenedBold));
            return new ResolvedFont(widened, false, key.Item3 && !face.Italic, WidenedBold: true);
        }
        return new ResolvedFont(data, key.Item2 && !face.Bold, key.Item3 && !face.Italic);
    });
}
