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

    /// <summary>Time PowerPoint gets for opening a deck and for exporting each slide (see <see cref="PowerPointSession.StepLimit"/>).</summary>
    static readonly TimeSpan StepLimit = TimeSpan.FromSeconds(90);

    [SupportedOSPlatform("windows")]
    public static void Render(IEnumerable<string> decks, bool force)
    {
        PowerPointSession? session = null;
        var failed = 0;
        try
        {
            foreach (var deck in decks)
            {
                var source = Repo.DeckFile(deck);
                if (!File.Exists(source))
                {
                    Console.WriteLine($"  missing     {deck}");
                    continue;
                }
                var hash = Hash(source);
                if (!force && Load(deck) is { } existing && existing.Sha256 == hash)
                {
                    Console.WriteLine($"  up to date  {deck}");
                    continue;
                }
                var password = Repo.DeckPassword(deck);
                if (Unreadable(source, password) is { } reason)
                {
                    Console.WriteLine($"  skipped     {deck} ({reason})");
                    continue;
                }

                session ??= new PowerPointSession { StepLimit = StepLimit };
                var dir = Dir(deck);
                if (Directory.Exists(dir))
                {
                    foreach (var old in Directory.GetFiles(dir, "slide-*.png"))
                        File.Delete(old);
                }

                try
                {
                    var files = session.ExportSlides(source, dir, password: password);
                    if (files.Count == 0)
                    {
                        Console.WriteLine($"  skipped     {deck} (no slides)");
                        continue;
                    }
                    using var first = SkiaSharp.SKBitmap.Decode(files[0]);
                    var info = new ReferenceInfo(
                        Path.GetRelativePath(Repo.Root, source).Replace('\\', '/'), hash, session.Version, first.Width, first.Height, files.Count);
                    File.WriteAllText(Path.Combine(dir, "meta.json"), JsonSerializer.Serialize(info, JsonOptions) + "\n");
                    Console.WriteLine($"  rendered    {deck} ({files.Count} slides, {first.Width}×{first.Height})");
                }
                catch (Exception e)
                {
                    // A deck PowerPoint cannot open or export (the watchdog may have ended PowerPoint): no reference. COM
                    // failures arrive as whatever exception their HRESULT maps to (a corrupt file's 0x80070570 as
                    // BadImageFormatException), so every exception counts as this deck's failure.
                    failed++;
                    Console.WriteLine($"  failed      {deck}: {e.Message.Split('\n')[0].Trim()}");
                    foreach (var partial in Directory.GetFiles(dir, "slide-*.png").Append(Path.Combine(dir, "meta.json")))
                        File.Delete(partial);
                    if (session.Broken)
                    {
                        session.Dispose();
                        session = null;
                    }
                }
            }
        }
        finally
        {
            session?.Dispose();
        }
        if (failed > 0)
            Console.WriteLine($"  {failed} decks PowerPoint could not render");
    }

    /// <summary>
    /// Why PowerPoint is not given a deck, or null: a damaged package or an encrypted deck without its password would
    /// stop it with a repair or password prompt.
    /// </summary>
    static string? Unreadable(string path, string? password)
    {
        var head = new byte[4];
        using (var stream = File.OpenRead(path))
        {
            if (stream.Read(head, 0, 4) < 4)
                return "too short";
        }
        // Encrypted decks are compound files (D0 CF 11 E0).
        if (head is [0xD0, 0xCF, 0x11, 0xE0])
            return password == null ? "encrypted, no password" : null;
        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(path);
            return zip.GetEntry("[Content_Types].xml") == null ? "no [Content_Types].xml" : null;
        }
        catch (InvalidDataException)
        {
            return "damaged package";
        }
    }
}
