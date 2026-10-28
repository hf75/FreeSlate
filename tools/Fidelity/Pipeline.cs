using System.Collections.Concurrent;
using System.Diagnostics;
using FreeSlate;
using FreeSlate.Skia;
using Fidelity.Compare;
using Fidelity.Rendering;
using Fidelity.Word;

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

    /// <summary>
    /// Whether the Skia backend (FreeSlate.Skia) renders instead of the browser (--skia): its renderings and report go to
    /// artifacts/fidelity/skia.
    /// </summary>
    public static bool Skia { get; set; }

    /// <summary>Where the report and the renderings go.</summary>
    public static string ReportDir => Skia ? Path.Combine(Repo.Artifacts, "skia") : Repo.Artifacts;

    public static string OutputDir(string deck) => Path.Combine(ReportDir, deck);

    /// <summary>
    /// The pixels PowerPoint draws a deck's slides on in its own picture of them, <paramref name="width"/> by
    /// <paramref name="height"/> (the slide's size rounded half up on each side, see PowerPointSession.ExportSlides):
    /// across as the slide's, down scaled by (h / H)(W / w) (measured with probe decks of one-pixel bars at 1058.33 by
    /// 793.67, 907.17 by 604.67, 1280.5 by 720 and 1058.27 by 595.28 pixels: 131 of 132 bars as predicted), text and tables
    /// by w / W across and h / H down (one-cell tables at three such sizes: every border); null where the slide's size is
    /// whole pixels.
    /// </summary>
    static FreeSlate.Rendering.PixelGrid? PowerPointPixels(string deck, int width, int height)
    {
        using var package = FreeSlate.Opc.OpcPackage.Open(Repo.DeckFile(deck), Repo.DeckPassword(deck));
        var presentation = package.FirstRelationship("/", "officeDocument")!.Target;
        var size = package.ReadXml(presentation).Root!.Element((System.Xml.Linq.XNamespace)"http://schemas.openxmlformats.org/presentationml/2006/main" + "sldSz");
        double Pixels(string name, long fallback) => (long.TryParse(size?.Attribute(name)?.Value, out var emu) ? emu : fallback) / 9525.0;
        var (w, h) = (Pixels("cx", 12192000), Pixels("cy", 6858000));
        if (Math.Abs(w - width) < 1e-9 && Math.Abs(h - height) < 1e-9)
            return null;
        return new FreeSlate.Rendering.PixelGrid(1, height / h * (w / width), Text: width / w);
    }

    public static void Render(IEnumerable<string> decks)
    {
        if (Skia)
        {
            RenderSkia(decks);
            return;
        }
        var browser = Browser.Find();
        var profiles = new ConcurrentBag<string>(Enumerable.Range(0, Parallelism).Select(i => Path.Combine(Repo.Artifacts, ".browser", $"profile-{i}")));
        Console.WriteLine($"Browser: {browser.Executable}");

        foreach (var deck in decks)
        {
            if (Repo.IsDocument(deck))
            {
                RenderDocument(browser, profiles, deck);
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
                // Fonts a deck embeds are used even where they are installed, as on a computer without them; slides on the
                // pixels of PowerPoint's own picture of them.
                PptxConverter.ToFile(Repo.DeckFile(deck), htmlPath, password: Repo.DeckPassword(deck), embeddedFontsFirst: true, culture: Repo.Regional,
                    pixels: reference != null ? PowerPointPixels(deck, width, height) : null);
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

    /// <summary>
    /// A Word document: converted with the engine, printed to PDF by the browser and rasterized by the same PDF renderer
    /// as Word's reference (on Windows), so that both sides go through one rasterizer and the score measures the layout
    /// and the drawing rather than two renderers' antialiasing; elsewhere, or with FIDELITY_DOCX_SCREENSHOTS=1, every
    /// page is a screenshot at the size of Word's page (816 × 1056 for a letter page without reference).
    /// </summary>
    static void RenderDocument(Browser browser, ConcurrentBag<string> profiles, string deck)
    {
        var dir = OutputDir(deck);
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.GetFiles(dir, "*.png"))
            File.Delete(old);
        var watch = Stopwatch.StartNew();
        var htmlPath = Path.Combine(dir, "index.html");
        try
        {
            // Printed on paper larger than the pages (browsers round paper sizes); the rasterizer cuts Word's page sizes out.
            // Documents named markup-* are shown with their markup, as Word prints them with markup.
            // Date and time fields show when Word printed the reference, so that both sides show the same.
            DocxConverter.ToFile(Repo.DeckFile(deck), htmlPath, password: Repo.DeckPassword(deck), paperExtra: 12, culture: Repo.Regional,
                markup: Repo.Markup(deck), printed: References.DocumentPrinted(deck));
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Console.WriteLine($"  failed    {deck}: {e.GetType().Name}: {e.Message.Split('\n')[0].Trim()}");
            return;
        }
        var convertTime = watch.ElapsedMilliseconds;
        var pages = File.ReadAllText(htmlPath).Split("<section class=\"page ").Length - 1;
        var url = new Uri(htmlPath).AbsoluteUri;
        if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("FIDELITY_DOCX_SCREENSHOTS") != "1")
        {
            profiles.TryTake(out var profile);
            try
            {
                var pdf = Path.Combine(dir, "ours.pdf");
                browser.PrintToPdf(url, pdf, profile!);
                // Word's PDFs embed their fonts without gasp tables, which the rasterizer would follow in ours only.
                PdfFonts.HideGasp(pdf);
                var reference = References.DocumentPdf(deck);
                var sizes = File.Exists(reference) ? new PdfText(reference).Pages.Select(p => (p.Width, p.Height)).ToList() : null;
                PdfPages.Rasterize(pdf, dir, References.DocumentScale, sizes);
            }
            finally
            {
                profiles.Add(profile!);
            }
            Console.WriteLine($"  rendered  {deck}: {pages} pages (convert {convertTime} ms, print and rasterize {watch.ElapsedMilliseconds - convertTime} ms)");
            return;
        }
        Parallel.For(1, pages + 1, new ParallelOptions { MaxDegreeOfParallelism = Parallelism }, i =>
        {
            var referencePage = Path.Combine(References.Dir(deck), References.ImageName(deck, i));
            var (width, height) = (816, 1056);
            if (File.Exists(referencePage) && SkiaSharp.SKCodec.Create(referencePage) is { } codec)
            {
                using (codec)
                    (width, height) = (codec.Info.Width, codec.Info.Height);
            }
            profiles.TryTake(out var profile);
            try
            {
                browser.Screenshot($"{url}#page-{i}", Path.Combine(dir, References.ImageName(deck, i)), width, height, profile!);
            }
            finally
            {
                profiles.Add(profile!);
            }
        });
        Console.WriteLine($"  rendered  {deck}: {pages} pages (convert {convertTime} ms, screenshots {watch.ElapsedMilliseconds - convertTime} ms)");
    }

    /// <summary>
    /// The Skia backend's renderings: a document's pages drawn into a PDF, rasterized as RenderDocument rasterizes the
    /// browser's on Windows (elsewhere drawn straight to pictures); a deck's slides, hidden ones too, drawn straight to
    /// pictures at the size of PowerPoint's.
    /// </summary>
    static void RenderSkia(IEnumerable<string> decks)
    {
        foreach (var deck in decks)
        {
            var dir = OutputDir(deck);
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "*.png"))
                File.Delete(old);
            var watch = Stopwatch.StartNew();
            try
            {
                if (Repo.IsDocument(deck))
                {
                    // Date and time fields show when Word printed the reference, as in RenderDocument.
                    var scenes = DocxConverter.ToScenes(Repo.DeckFile(deck), password: Repo.DeckPassword(deck), culture: Repo.Regional,
                        markup: Repo.Markup(deck), printed: References.DocumentPrinted(deck));
                    if (OperatingSystem.IsWindows())
                    {
                        var pdf = Path.Combine(dir, "ours.pdf");
                        using (var file = File.Create(pdf))
                            SkiaScene.Pdf(scenes, file);
                        // Word's PDFs embed their fonts without gasp tables, as the browser's are made to (RenderDocument).
                        PdfFonts.HideGasp(pdf);
                        var reference = References.DocumentPdf(deck);
                        var sizes = File.Exists(reference) ? new PdfText(reference).Pages.Select(p => (p.Width, p.Height)).ToList() : null;
                        PdfPages.Rasterize(pdf, dir, References.DocumentScale, sizes);
                    }
                    else
                    {
                        for (var i = 0; i < scenes.Count; i++)
                            File.WriteAllBytes(Path.Combine(dir, References.ImageName(deck, i + 1)), SkiaScene.Png(scenes[i], References.DocumentScale));
                    }
                    Console.WriteLine($"  rendered  {deck}: {scenes.Count} pages with Skia ({watch.ElapsedMilliseconds} ms)");
                }
                else
                {
                    // Fonts a deck embeds are used even where they are installed, as in Render.
                    var reference = References.Load(deck);
                    var (width, height) = reference is { } r ? (r.Width, r.Height) : (1280, 720);
                    var pixels = reference != null ? PowerPointPixels(deck, width, height) : null;
                    var scenes = PptxConverter.ToScenes(Repo.DeckFile(deck), password: Repo.DeckPassword(deck), embeddedFontsFirst: true,
                        culture: Repo.Regional, hidden: true, pixels: pixels);
                    for (var i = 0; i < scenes.Count; i++)
                    {
                        // On PowerPoint's pixels each axis as they scale, else the width's scale for both.
                        var scale = pixels?.X ?? width / (scenes[i].Width * 96 / 72);
                        File.WriteAllBytes(Path.Combine(dir, References.ImageName(deck, i + 1)), SkiaScene.Png(scenes[i], scale, width, height, scaleY: pixels?.Y ?? scale));
                    }
                    Console.WriteLine($"  rendered  {deck}: {scenes.Count} slides with Skia ({watch.ElapsedMilliseconds} ms)");
                }
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                Console.WriteLine($"  failed    {deck}: {e.GetType().Name}: {e.Message.Split('\n')[0].Trim()}");
            }
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
            // A document that did not convert scores 0 on every page of Word's.
            if (Repo.IsDocument(deck) && !File.Exists(Path.Combine(OutputDir(deck), References.ImageName(deck, 1))))
            {
                Console.WriteLine($"  no rendering of {deck}");
                results.Add(new DeckResult(deck, Enumerable.Range(1, reference.Count).Select(i => new SlideResult(i, new SlideScore(0, 0, 0))).ToList()));
                continue;
            }
            var slides = new ConcurrentBag<SlideResult>();
            // A document's page that one side has and the other not scores 0: a rendering with one of Word's three
            // pages, or with pages beyond Word's, would otherwise be measured by the pages they share alone.
            var document = Repo.IsDocument(deck);
            var count = document ? Math.Max(reference.Count, Directory.GetFiles(OutputDir(deck), "page-*.png").Length) : reference.Count;
            Parallel.For(1, count + 1, i =>
            {
                var name = References.ImageName(deck, i);
                var ours = Path.Combine(OutputDir(deck), name);
                if (document && (i > reference.Count || !File.Exists(ours)))
                {
                    slides.Add(new SlideResult(i, new SlideScore(0, 0, 0)));
                    return;
                }
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
