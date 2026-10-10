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

    /// <summary>
    /// The font for <paramref name="text"/>: the family if it is installed and has every glyph, else the first of
    /// <paramref name="candidates"/> that has them, else the family (or its fallback) anyway.
    /// </summary>
    public ResolvedFont ResolveCovering(string family, bool bold, bool italic, string text, IEnumerable<string> candidates)
    {
        var found = Find(family, bold, italic);
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
        return Resolve(family, bold, italic);
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
