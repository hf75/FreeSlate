using System.Collections.Concurrent;
using Avalonia.Platform;
using FreeSlate.Fonts;

namespace FreeSlate.Editor.Writing;

/// <summary>
/// The fonts the editor gives the engine beside those of the system. Inter, which the interface shows and the tones set text in, from
/// Avalonia's package, so that a document looks alike where Inter is not installed; its weights other than regular and bold are
/// families of their own, as Word names them ("Inter SemiBold"); it has no italics, which the engine slants. And the shelf (see
/// Shelf): free families that come with the editor, among them the metric twins of Office's fonts, which stand in for those where
/// they are not installed (Fonts.MetricTwins), as on a page, whose program sees no fonts of the system.
/// </summary>
public static class EngineFonts
{
    /// <summary>
    /// The families on the shelf, each in four styles, in Fonts/{its name without spaces}/{that}-{Regular|Bold|Italic|BoldItalic}.ttf:
    /// the metric twins of Calibri, Cambria, Georgia, Arial, Times New Roman and Courier New, and six of Google Fonts' most used ones
    /// (static builds of Google Fonts; Caladea of Chrome OS's extra fonts, see Fonts.MetricTwins).
    /// </summary>
    public static readonly IReadOnlyList<string> Shelf =
        ["Carlito", "Caladea", "Gelasio", "Arimo", "Tinos", "Cousine", "Roboto", "Open Sans", "Lato", "Montserrat", "Lora", "Playfair Display"];

    static readonly (string Name, bool Bold, bool Italic)[] Styles = [("Regular", false, false), ("Bold", true, false), ("Italic", false, true), ("BoldItalic", true, true)];

    /// <summary>The editor's own faces: Inter's and the shelf's.</summary>
    static readonly FontFace[] Own =
    [
        Face("Inter", false, "Inter-Regular.ttf"),
        Face("Inter", true, "Inter-Bold.ttf"),
        Face("Inter SemiBold", false, "Inter-SemiBold.ttf"),
        Face("Inter Medium", false, "Inter-Medium.ttf"),
        Face("Inter Light", false, "Inter-Light.ttf"),
        .. (from family in Shelf
            from style in Styles
            let file = FileOf(family, style.Name)
            select new FontFace(family, style.Bold, style.Italic, "fonts/" + file, System: false) { Embedded = new Lazy<byte[]?>(() => Bytes(file)) }),
    ];

    public static FontLocator Locator { get; } = FontLocator.Default.With(Own);

    /// <summary>
    /// The faces of the editor's own fonts a document sets text in and the machine does not have, with their files, to embed in it (see
    /// WordDocument.EmbedFonts), so that Word shows it in them too; those whose files are not at hand left out.
    /// </summary>
    public static IReadOnlyList<EmbeddedFace> Embeddable(IEnumerable<string> families)
    {
        var wanted = families.Where(family => !FontLocator.Default.IsInstalled(family)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. Own.Where(face => wanted.Contains(face.Family)).Select(face => face.Embedded?.Value is { Length: > 0 } data ? new EmbeddedFace(face.Family, face.Bold, face.Italic, data) : null).OfType<EmbeddedFace>()];
    }

    /// <summary>
    /// How a page fetches a font file of the shelf by its path under Fonts ("Gelasio/Gelasio-Bold.ttf"), null where it is missing; null
    /// where the files lie beside the program, which reads them when it first needs them.
    /// </summary>
    public static Func<string, Task<byte[]?>>? Fetch { get; set; }

    /// <summary>
    /// Makes the shelf's fonts of these families ready before a document sets text in them, or those of their twins where the machine
    /// lacks them: a page fetches them (see Fetch), as the engine reads a font when it lays text out, which a page cannot wait for. A
    /// font that does not come stays missing, its text in another; families the machine has, and families not on the shelf, need
    /// nothing.
    /// </summary>
    public static Task Prepare(IEnumerable<string> families)
    {
        if (Fetch is not { } fetch)
            return Task.CompletedTask;
        var wanted = families
            .Where(family => !FontLocator.Default.IsInstalled(family))
            .Select(family => MetricTwins.Of(family) ?? family)
            .Where(family => Shelf.Contains(family, StringComparer.OrdinalIgnoreCase) && !FontLocator.Default.IsInstalled(family))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return Task.WhenAll(wanted.SelectMany(family => Styles.Select(style => Fetched(FileOf(family, style.Name), fetch))));
    }

    /// <summary>The fetches of the shelf's files (see Prepare), one a file, and the files fetched.</summary>
    static readonly ConcurrentDictionary<string, Task> fetching = new();
    static readonly ConcurrentDictionary<string, byte[]> fetched = new();

    static Task Fetched(string file, Func<string, Task<byte[]?>> fetch) => fetching.GetOrAdd(file, async path =>
    {
        try
        {
            if (await fetch(path) is { } bytes)
                fetched[path] = bytes;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
        }
    });

    static string FileOf(string family, string style)
    {
        var name = family.Replace(" ", "", StringComparison.Ordinal);
        return $"{name}/{name}-{style}.ttf";
    }

    /// <summary>A file of the shelf: as fetched, else beside the program; null where it is neither.</summary>
    static byte[]? Bytes(string file)
    {
        if (fetched.TryGetValue(file, out var bytes))
            return bytes;
        var path = Path.Combine(AppContext.BaseDirectory, "Fonts", file);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    static FontFace Face(string family, bool bold, string file)
    {
        var uri = new Uri("avares://Avalonia.Fonts.Inter/Assets/" + file);
        return new FontFace(family, bold, false, uri.ToString(), System: false)
        {
            Embedded = new Lazy<byte[]?>(() =>
            {
                using var source = AssetLoader.Open(uri);
                using var bytes = new MemoryStream();
                source.CopyTo(bytes);
                return bytes.ToArray();
            }),
        };
    }
}
