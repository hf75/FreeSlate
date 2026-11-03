using Avalonia.Platform;
using FreeSlate.Fonts;

namespace FreeSlate.Editor.Writing;

/// <summary>
/// The fonts the editor gives the engine beside those of the system: Inter, which the interface shows and the tones set text in, from
/// Avalonia's package, so that a document looks alike where Inter is not installed. Its weights other than regular and bold are
/// families of their own, as Word names them ("Inter SemiBold"); it has no italics, which the engine slants.
/// </summary>
public static class EngineFonts
{
    public static FontLocator Locator { get; } = FontLocator.Default.With(
    [
        Face("Inter", false, "Inter-Regular.ttf"),
        Face("Inter", true, "Inter-Bold.ttf"),
        Face("Inter SemiBold", false, "Inter-SemiBold.ttf"),
        Face("Inter Medium", false, "Inter-Medium.ttf"),
        Face("Inter Light", false, "Inter-Light.ttf"),
    ]);

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
