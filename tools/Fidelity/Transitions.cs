using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using Fidelity.Compare;
using Fidelity.PowerPoint;
using Fidelity.Rendering;

namespace Fidelity;

/// <summary>A transition's reference frames: the frame of its video it begins on, its duration (s) and the frames sampled.</summary>
public sealed record TransitionSlide(int Slide, int Start, double Duration, List<int> Samples);

/// <summary>Metadata of a deck's transition references.</summary>
public sealed record TransitionInfo(string Sha256, string PowerPoint, int Width, int Height, int Fps, List<TransitionSlide> Slides);

/// <summary>
/// Slide transitions compared with PowerPoint (catalog: transition.*): the references are frames of PowerPoint's
/// video of every transition (the slide and the one before it alone, each shown a second), sampled while the
/// transition runs; the transition begins a frame after the first slide's second. The player draws the
/// same moments (?transition=ms#slide-N: ms after the transition into slide N began), at the references' size.
/// </summary>
public static class Transitions
{
    const int Height = 360, Fps = 30, SlideSeconds = 1;

    /// <summary>Moments sampled within a transition, as fractions of its duration.</summary>
    static readonly double[] Moments = [0.1, 0.23, 0.37, 0.5, 0.63, 0.77, 0.9];

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Dir(string deck) => Path.Combine(References.Dir(deck), "trans");

    /// <summary>A reference frame (JPEG: the video's own noise makes PNG frames large) or one of ours (PNG).</summary>
    static string FrameName(int slide, int frame, bool reference = false) => $"slide-{slide:000}-t{frame:0000}.{(reference ? "jpg" : "png")}";

    public static TransitionInfo? Load(string deck)
    {
        var file = Path.Combine(Dir(deck), "meta.json");
        return File.Exists(file) ? JsonSerializer.Deserialize<TransitionInfo>(File.ReadAllText(file), JsonOptions) : null;
    }

    /// <summary>Exports PowerPoint's videos of the transitions and samples them (needs ffmpeg).</summary>
    [SupportedOSPlatform("windows")]
    public static void Reference(IEnumerable<string> decks, bool force)
    {
        PowerPointSession? session = null;
        try
        {
            foreach (var deck in decks)
            {
                var source = Repo.DeckFile(deck);
                var hash = References.Hash(source);
                if (!force && Load(deck) is { } existing && existing.Sha256 == hash)
                {
                    Console.WriteLine($"  up to date  {deck}");
                    continue;
                }
                session ??= new PowerPointSession();
                var dir = Dir(deck);
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
                Directory.CreateDirectory(dir);
                var slides = new List<TransitionSlide>();
                var temporary = Path.Combine(Path.GetTempPath(), "fidelity-trans-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temporary);
                try
                {
                    foreach (var (slide, duration) in session.TransitionSlides(source, Repo.DeckPassword(deck)))
                    {
                        var video = Path.Combine(temporary, $"slide-{slide}.mp4");
                        session.ExportTransitionVideo(source, slide, video, Height, Fps, SlideSeconds, Repo.DeckPassword(deck));
                        var frames = Path.Combine(temporary, $"slide-{slide}");
                        Directory.CreateDirectory(frames);
                        Animations.Tool("ffmpeg", ["-v", "error", "-y", "-i", video, "-vf", "scale=in_color_matrix=bt709:in_range=tv:out_range=pc",
                            Path.Combine(frames, "f%04d.png")]);
                        var files = Directory.GetFiles(frames, "f*.png").Order(StringComparer.Ordinal).ToList();
                        var start = StartFrame();
                        var samples = Moments.Select(m => start + (int)Math.Round(m * duration * Fps)).Where(f => f < files.Count).Distinct().ToList();
                        foreach (var frame in samples)
                            SaveJpeg(files[frame], Path.Combine(dir, FrameName(slide, frame, reference: true)));
                        slides.Add(new TransitionSlide(slide, start, duration, samples));
                        Console.WriteLine($"    slide {slide,3}: begins at frame {start}");
                    }
                }
                finally
                {
                    Directory.Delete(temporary, recursive: true);
                }
                var (width, height) = (0, 0);
                if (slides.Count > 0)
                {
                    using var first = SkiaSharp.SKBitmap.Decode(Path.Combine(dir, FrameName(slides[0].Slide, slides[0].Samples[0], reference: true)));
                    (width, height) = (first.Width, first.Height);
                }
                var info = new TransitionInfo(hash, session.Version, width, height, Fps, slides);
                File.WriteAllText(Path.Combine(dir, "meta.json"), JsonSerializer.Serialize(info, JsonOptions) + "\n");
                Console.WriteLine($"  sampled     {deck} ({slides.Count} transitions, {slides.Sum(s => s.Samples.Count)} frames)");
            }
        }
        finally
        {
            session?.Dispose();
        }
    }

    /// <summary>
    /// The frame the transition begins on: a frame after the first slide's time (measured: Push's quadratic easing
    /// fits its edge from frame 30.9 at 30 fps with a second per slide, within 0.1 %; transitions that start slowly
    /// show their first change frames later).
    /// </summary>
    static int StartFrame() => Fps * SlideSeconds + 1;

    /// <summary>Draws the reference moments with the player and compares them; decks without transition references are skipped.</summary>
    public static List<DeckResult> Run(IEnumerable<string> decks, ISet<int>? only = null)
    {
        var browser = Browser.Find();
        const int Parallelism = 4;
        var profiles = new ConcurrentBag<string>(Enumerable.Range(0, Parallelism).Select(i => Path.Combine(Repo.Artifacts, ".browser", $"profile-{i}")));
        var results = new List<DeckResult>();
        foreach (var deck in decks)
        {
            if (Load(deck) is not { Slides.Count: > 0 } info)
                continue;
            var dir = Path.Combine(Pipeline.OutputDir(deck), "trans");
            if (Directory.Exists(dir) && only == null)
                Directory.Delete(dir, recursive: true);
            Directory.CreateDirectory(dir);
            var htmlPath = Path.Combine(dir, "index.html");
            FreeSlate.PptxConverter.ToFile(Repo.DeckFile(deck), htmlPath, password: Repo.DeckPassword(deck));
            var url = new Uri(htmlPath).AbsoluteUri;
            var samples = info.Slides.Where(s => only == null || only.Contains(s.Slide))
                .SelectMany(s => s.Samples.Select(f => (s.Slide, s.Start, Frame: f))).ToList();
            var scores = new ConcurrentBag<SlideResult>();
            Parallel.ForEach(samples, new ParallelOptions { MaxDegreeOfParallelism = Parallelism }, sample =>
            {
                profiles.TryTake(out var profile);
                try
                {
                    var name = FrameName(sample.Slide, sample.Frame);
                    var full = Path.Combine(dir, "full-" + name);
                    var ours = Path.Combine(dir, name);
                    var time = ((sample.Frame - sample.Start) * 1000.0 / info.Fps).ToString("0.###", CultureInfo.InvariantCulture);
                    // The slide's size in CSS pixels (96 dpi), scaled down to the reference's.
                    browser.Screenshot($"{url}?transition={time}&fps={info.Fps}#slide-{sample.Slide}", full, 1280, 720, profile!);
                    Scale(full, ours, info.Width, info.Height);
                    var (blurredReference, blurredOurs) = (Path.Combine(dir, "ref-" + name), Path.Combine(dir, "blur-" + name));
                    Animations.Blur(Path.Combine(Dir(deck), FrameName(sample.Slide, sample.Frame, reference: true)), blurredReference);
                    Animations.Blur(ours, blurredOurs);
                    var score = ImageCompare.Compare(blurredReference, blurredOurs, Path.Combine(dir, "diff-" + name));
                    scores.Add(new SlideResult(sample.Slide * 100000 + sample.Frame, score));
                }
                finally
                {
                    profiles.Add(profile!);
                }
            });
            var ordered = scores.OrderBy(s => s.Number).ToList();
            foreach (var slide in info.Slides)
            {
                var own = ordered.Where(s => s.Number / 100000 == slide.Slide).ToList();
                if (own.Count == 0)
                    continue;
                var worst = own.MinBy(s => s.Score.Score)!;
                Console.WriteLine($"    slide {slide.Slide,3}: {own.Average(s => s.Score.Score) * 100,5:0.0} %  (worst {worst.Score.Score * 100:0.0} % at frame {worst.Number % 100000})");
            }
            results.Add(new DeckResult(deck + " (transitions)", ordered));
        }
        return results;
    }

    static void SaveJpeg(string source, string target)
    {
        using var bitmap = SkiaSharp.SKBitmap.Decode(source);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90);
        using var file = File.Create(target);
        data.SaveTo(file);
    }

    static void Scale(string source, string target, int width, int height)
    {
        using var bitmap = SkiaSharp.SKBitmap.Decode(source);
        using var scaled = bitmap.Resize(new SkiaSharp.SKImageInfo(width, height), new SkiaSharp.SKSamplingOptions(SkiaSharp.SKCubicResampler.Mitchell));
        using var image = SkiaSharp.SKImage.FromBitmap(scaled);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var file = File.Create(target);
        data.SaveTo(file);
    }
}
