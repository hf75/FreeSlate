using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Fidelity.PowerPoint;

namespace Fidelity;

/// <summary>Metadata stored next to the reference renderings of a deck.</summary>
public sealed record ReferenceInfo(string Source, string Sha256, string PowerPoint, int Width, int Height, int Slides);

/// <summary>Renders the reference images of the corpus with PowerPoint.</summary>
public static class References
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Dir(string deck) => Path.Combine(Repo.References, deck);

    public static ReferenceInfo? Load(string deck)
    {
        var file = Path.Combine(Dir(deck), "meta.json");
        return File.Exists(file) ? JsonSerializer.Deserialize<ReferenceInfo>(File.ReadAllText(file), JsonOptions) : null;
    }

    public static string Hash(string file) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));

    [SupportedOSPlatform("windows")]
    public static void Render(IEnumerable<string> decks, bool force)
    {
        PowerPointSession? session = null;
        try
        {
            foreach (var deck in decks)
            {
                var source = Repo.DeckFile(deck);
                var hash = Hash(source);
                if (!force && Load(deck) is { } existing && existing.Sha256 == hash)
                {
                    Console.WriteLine($"  up to date  {deck}");
                    continue;
                }

                session ??= new PowerPointSession();
                var dir = Dir(deck);
                if (Directory.Exists(dir))
                {
                    foreach (var old in Directory.GetFiles(dir, "slide-*.png"))
                        File.Delete(old);
                }

                var files = session.ExportSlides(source, dir);
                using var first = SkiaSharp.SKBitmap.Decode(files[0]);
                var info = new ReferenceInfo(
                    Path.GetRelativePath(Repo.Root, source).Replace('\\', '/'), hash, session.Version, first.Width, first.Height, files.Count);
                File.WriteAllText(Path.Combine(dir, "meta.json"), JsonSerializer.Serialize(info, JsonOptions) + "\n");
                Console.WriteLine($"  rendered    {deck} ({files.Count} slides, {first.Width}×{first.Height})");
            }
        }
        finally
        {
            session?.Dispose();
        }
    }
}
