using System.Collections.Concurrent;
using Engine.Fonts;

namespace Engine.Text;

/// <summary>A font chosen for a run: the file to measure with and whether the browser has to synthesize the style.</summary>
public sealed record ResolvedFont(FontData Data, bool SyntheticBold, bool SyntheticItalic);

/// <summary>
/// Maps a typeface and style to a font file. Missing families fall back to a metric-neutral sans serif that is
/// also written into the CSS, so the browser draws with the font the layout was measured with.
/// </summary>
public sealed class FontResolver(FontLocator locator)
{
    static readonly string[] Fallbacks = ["Arial", "Liberation Sans", "DejaVu Sans"];

    readonly ConcurrentDictionary<string, FontData> loaded = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<(string, bool, bool), ResolvedFont?> cache = new();

    public static FontResolver Default { get; } = new(FontLocator.Default);

    public ResolvedFont Resolve(string family, bool bold, bool italic) =>
        Find(family, bold, italic) ?? Fallbacks.Select(f => Find(f, bold, italic)).FirstOrDefault(f => f != null)
        ?? throw new InvalidOperationException("No usable font found (not even a fallback).");

    ResolvedFont? Find(string family, bool bold, bool italic) => cache.GetOrAdd((family, bold, italic), key =>
    {
        var faces = locator.Faces(key.Item1).ToList();
        if (faces.Count == 0)
            return null;
        var exact = faces.FirstOrDefault(f => f.Bold == key.Item2 && f.Italic == key.Item3);
        var face = exact ?? faces.FirstOrDefault(f => !f.Bold && !f.Italic) ?? faces[0];
        var data = loaded.GetOrAdd(face.Path, path => new FontData(path, face.Family));
        return new ResolvedFont(data, key.Item2 && !face.Bold, key.Item3 && !face.Italic);
    });
}
