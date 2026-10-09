using System.Collections.Concurrent;
using FreeSlate.Fonts;

namespace FreeSlate.Text;

/// <summary>A font chosen for a run: the file to measure with and whether the browser has to synthesize the style.</summary>
public sealed record ResolvedFont(FontData Data, bool SyntheticBold, bool SyntheticItalic);

/// <summary>
/// Maps a typeface and style to a font file. Missing families fall back to Calibri like in PowerPoint, whatever
/// their panose or pitch family say (measured: serif, monospaced, script and unknown fonts all become Calibri), else
/// to its metric twin Carlito or a common sans serif; the fallback is written into the CSS, so the browser draws
/// with the font the layout was measured with.
/// </summary>
public sealed class FontResolver(FontLocator locator)
{
    static readonly string[] Fallbacks = ["Calibri", "Carlito", "Arial", "Liberation Sans", "DejaVu Sans"];

    readonly ConcurrentDictionary<string, FontData> loaded = new(StringComparer.OrdinalIgnoreCase);
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
        var faces = locator.Faces(key.Item1).ToList();
        if (faces.Count == 0)
            return null;
        var exact = faces.FirstOrDefault(f => f.Bold == key.Item2 && f.Italic == key.Item3);
        var face = exact ?? faces.FirstOrDefault(f => !f.Bold && !f.Italic) ?? faces[0];
        var data = loaded.GetOrAdd($"{face.Path}#{face.Index}", _ => new FontData(face.Path, face.Family, face.Index));
        return new ResolvedFont(data, key.Item2 && !face.Bold, key.Item3 && !face.Italic);
    });
}
