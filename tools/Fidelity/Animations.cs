using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using Fidelity.Compare;
using Fidelity.PowerPoint;
using Fidelity.Rendering;

namespace Fidelity;

/// <summary>An animated slide's reference frames: the numbers of the video frames sampled (frame n shows the slide n / fps s after it began).</summary>
public sealed record AnimatedSlide(int Slide, int Frames, List<int> Samples);

/// <summary>Metadata of a deck's animation references.</summary>
public sealed record AnimationInfo(string Sha256, string PowerPoint, int Width, int Height, int Fps, List<AnimatedSlide> Slides);

/// <summary>
/// Animations compared with PowerPoint: the references are frames of PowerPoint's video of every slide whose main
/// sequence has steps (exported on its own, without its transition; the video plays the steps one after another),
/// every <see cref="Interval"/>th frame and the last; the player draws the same moments (?frame=ms#slide-N).
/// </summary>
public static class Animations
{
    const int Interval = 8, Height = 720, Fps = 30;
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Dir(string deck) => Path.Combine(References.Dir(deck), "anim");

    static string FrameName(int slide, int frame) => $"slide-{slide:000}-f{frame:0000}.png";

    public static AnimationInfo? Load(string deck)
    {
        var file = Path.Combine(Dir(deck), "meta.json");
        return File.Exists(file) ? JsonSerializer.Deserialize<AnimationInfo>(File.ReadAllText(file), JsonOptions) : null;
    }

    /// <summary>Exports PowerPoint's videos of the animated slides and samples them (needs ffmpeg).</summary>
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

                var slides = new List<AnimatedSlide>();
                var temporary = Path.Combine(Path.GetTempPath(), "fidelity-anim-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temporary);
                try
                {
                    foreach (var slide in session.AnimatedSlides(source, Repo.DeckPassword(deck)))
                    {
                        var video = Path.Combine(temporary, $"slide-{slide}.mp4");
                        session.ExportSlideVideo(source, slide, video, Height, Fps, Repo.DeckPassword(deck));
                        var frames = (int)Math.Round(VideoDuration(video) * Fps);
                        var samples = Enumerable.Range(0, (frames - 1) / Interval + 1).Select(i => i * Interval).ToList();
                        if (samples[^1] != frames - 1)
                            samples.Add(frames - 1);
                        foreach (var frame in samples)
                            ExtractFrame(video, frame, Path.Combine(dir, FrameName(slide, frame)));
                        slides.Add(new AnimatedSlide(slide, frames, samples));
                    }
                }
                finally
                {
                    Directory.Delete(temporary, recursive: true);
                }
                var (width, height) = (0, 0);
                if (slides.Count > 0)
                {
                    using var first = SkiaSharp.SKBitmap.Decode(Path.Combine(dir, FrameName(slides[0].Slide, 0)));
                    (width, height) = (first.Width, first.Height);
                }
                var info = new AnimationInfo(hash, session.Version, width, height, Fps, slides);
                File.WriteAllText(Path.Combine(dir, "meta.json"), JsonSerializer.Serialize(info, JsonOptions) + "\n");
                Console.WriteLine($"  sampled     {deck} ({slides.Count} animated slides, {slides.Sum(s => s.Samples.Count)} frames)");
            }
        }
        finally
        {
            session?.Dispose();
        }
    }

    /// <summary>Draws the reference moments with the player and compares them; decks without animation references are skipped.</summary>
    public static List<DeckResult> Run(IEnumerable<string> decks)
    {
        var browser = Browser.Find();
        const int Parallelism = 4;
        var profiles = new ConcurrentBag<string>(Enumerable.Range(0, Parallelism).Select(i => Path.Combine(Repo.Artifacts, ".browser", $"profile-{i}")));
        var results = new List<DeckResult>();
        foreach (var deck in decks)
        {
            if (Load(deck) is not { Slides.Count: > 0 } info)
                continue;
            var dir = Path.Combine(Pipeline.OutputDir(deck), "anim");
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
            Directory.CreateDirectory(dir);
            var htmlPath = Path.Combine(dir, "index.html");
            FreeSlate.PptxConverter.ToFile(Repo.DeckFile(deck), htmlPath, password: Repo.DeckPassword(deck));
            var url = new Uri(htmlPath).AbsoluteUri;
            var samples = info.Slides.SelectMany(s => s.Samples.Select(f => (s.Slide, Frame: f))).ToList();
            var scores = new ConcurrentBag<SlideResult>();
            Parallel.ForEach(samples, new ParallelOptions { MaxDegreeOfParallelism = Parallelism }, sample =>
            {
                profiles.TryTake(out var profile);
                try
                {
                    var name = FrameName(sample.Slide, sample.Frame);
                    var ours = Path.Combine(dir, name);
                    var time = (sample.Frame * 1000.0 / info.Fps).ToString("0.###", CultureInfo.InvariantCulture);
                    browser.Screenshot($"{url}?frame={time}&fps={info.Fps}#slide-{sample.Slide}", ours, info.Width, info.Height, profile!);
                    var score = ImageCompare.Compare(Path.Combine(Dir(deck), name), ours, Path.Combine(dir, "diff-" + name));
                    // Samples are numbered slide * 100000 + frame.
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
                var worst = own.MinBy(s => s.Score.Score)!;
                Console.WriteLine($"    slide {slide.Slide,3}: {own.Average(s => s.Score.Score) * 100,5:0.0} %  (worst {worst.Score.Score * 100:0.0} % at frame {worst.Number % 100000})");
            }
            results.Add(new DeckResult(deck + " (animations)", ordered));
        }
        return results;
    }

    static double VideoDuration(string video)
    {
        var output = Tool("ffprobe", ["-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", video]);
        return double.Parse(output.Trim(), CultureInfo.InvariantCulture);
    }

    /// <summary>Frame <paramref name="frame"/> (counted from 0) of the video.</summary>
    static void ExtractFrame(string video, int frame, string png) =>
        Tool("ffmpeg", ["-v", "error", "-y", "-i", video, "-vf", $"select=eq(n\\,{frame})", "-vsync", "vfr", "-frames:v", "1", png]);

    static string Tool(string name, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(name) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"{name} not found (ffmpeg is needed for animation references).");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{name} failed: {error.Result}");
        return output.Result;
    }
}
