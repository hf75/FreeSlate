using System.Collections.Concurrent;
using System.Diagnostics;
using FreeSlate;
using Fidelity.Compare;
using Fidelity.Rendering;

namespace Fidelity;

public sealed record SlideResult(int Number, SlideScore Score);

public sealed record DeckResult(string Deck, List<SlideResult> Slides)
{
    public double Score => Slides.Count == 0 ? 0 : Slides.Average(s => s.Score.Score);
    public double Ssim => Slides.Count == 0 ? 0 : Slides.Average(s => s.Score.Ssim);
}

/// <summary>Renders decks with our engine, screenshots every slide and compares it with the PowerPoint reference.</summary>
public static class Pipeline
{
    const int Parallelism = 4;

    public static string OutputDir(string deck) => Path.Combine(Repo.Artifacts, deck);

    public static void Render(IEnumerable<string> decks)
    {
        var browser = Browser.Find();
        var profiles = new ConcurrentBag<string>(Enumerable.Range(0, Parallelism).Select(i => Path.Combine(Repo.Artifacts, ".browser", $"profile-{i}")));
        Console.WriteLine($"Browser: {browser.Executable}");

        foreach (var deck in decks)
        {
            if (Repo.IsDocument(deck))
            {
                // catalog: engine.fidelity (docx). Word documents have references, not yet a renderer.
                Console.WriteLine($"  skipped   {deck}: no DOCX renderer yet");
                continue;
            }
            var reference = References.Load(deck);
            var (width, height) = reference is { } r ? (r.Width, r.Height) : (1280, 720);
            var dir = OutputDir(deck);
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "*.png"))
                File.Delete(old);

            var watch = Stopwatch.StartNew();
            var htmlPath = Path.Combine(dir, "index.html");
            try
            {
                // Fonts a deck embeds are used even where they are installed, as on a computer without them.
                PptxConverter.ToFile(Repo.DeckFile(deck), htmlPath, password: Repo.DeckPassword(deck), embeddedFontsFirst: true, culture: Repo.Regional);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                // Damaged and encrypted decks among the downloaded ones; the others go on.
                Console.WriteLine($"  failed    {deck}: {e.GetType().Name}: {e.Message.Split('\n')[0].Trim()}");
                continue;
            }
            var convertTime = watch.ElapsedMilliseconds;
            var slides = File.ReadAllText(htmlPath).Split("<section class=\"slide\"").Length - 1;
            var url = new Uri(htmlPath).AbsoluteUri;

            Parallel.For(1, slides + 1, new ParallelOptions { MaxDegreeOfParallelism = Parallelism }, i =>
            {
                profiles.TryTake(out var profile);
                try
                {
                    browser.Screenshot($"{url}#slide-{i}", Path.Combine(dir, $"slide-{i:000}.png"), width, height, profile!);
                }
                finally
                {
                    profiles.Add(profile!);
                }
            });
            Console.WriteLine($"  rendered  {deck}: {slides} slides (convert {convertTime} ms, screenshots {watch.ElapsedMilliseconds - convertTime} ms)");
        }
    }

    public static List<DeckResult> Compare(IEnumerable<string> decks)
    {
        var results = new List<DeckResult>();
        foreach (var deck in decks)
        {
            if (References.Load(deck) is not { } reference)
            {
                Console.WriteLine($"  no reference for {deck}; run `reference` on Windows first");
                continue;
            }
            if (Repo.IsDocument(deck) && !File.Exists(Path.Combine(OutputDir(deck), References.ImageName(deck, 1))))
            {
                Console.WriteLine($"  no rendering of {deck}");
                continue;
            }
            var slides = new ConcurrentBag<SlideResult>();
            Parallel.For(1, reference.Count + 1, i =>
            {
                var name = References.ImageName(deck, i);
                var ours = Path.Combine(OutputDir(deck), name);
                if (!File.Exists(ours))
                    return;
                var score = ImageCompare.Compare(
                    Path.Combine(References.Dir(deck), name), ours, Path.Combine(OutputDir(deck), $"diff-{i:000}.png"));
                slides.Add(new SlideResult(i, score));
            });
            results.Add(new DeckResult(deck, slides.OrderBy(s => s.Number).ToList()));
        }
        return results;
    }
}
