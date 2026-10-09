using System.Globalization;
using Fidelity;
using Fidelity.Compare;
using Fidelity.PowerPoint;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var command = args.FirstOrDefault() ?? "run";
var rest = args.Skip(1).Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
var force = args.Contains("--force");
var decks = rest.Count > 0 ? rest : Repo.Decks();

switch (command)
{
    case "generate" when OperatingSystem.IsWindows():
        CorpusGenerator.Run(Path.Combine(Repo.Corpus, "generated"), rest);
        break;
    case "reference" when OperatingSystem.IsWindows():
        References.Render(decks, force);
        break;
    case "calibrate" when OperatingSystem.IsWindows():
        if (!args.Contains("--measure-only"))
            Calibration.Render();
        Console.WriteLine($"{Calibration.Measure().Count} boxes measured: {Path.Combine(Calibration.Dir, "measurements.json")}");
        break;
    case "animate-reference" when OperatingSystem.IsWindows():
        Animations.Reference(decks, force);
        break;
    case "harvest-cmyk" when OperatingSystem.IsWindows():
        CmykHarvest.Run();
        break;
    case "generate" or "reference" or "calibrate" or "animate-reference" or "harvest-cmyk":
        Console.Error.WriteLine($"'{command}' needs PowerPoint and therefore Windows.");
        return 1;
    case "harvest-patterns":
        PatternHarvest.Run();
        break;
    case "render":
        Pipeline.Render(decks);
        break;
    case "compare":
        Summarize(Pipeline.Compare(decks));
        break;
    case "run":
        Pipeline.Render(decks);
        Summarize(Pipeline.Compare(decks));
        break;
    case "smartart":
        SmartArtCheck.Run(rest);
        SmartArtCheck.Geometry(rest, args.Contains("--verbose"));
        break;
    case "animate":
        var animated = Animations.Run(decks);
        foreach (var r in animated)
            Console.WriteLine($"  {r.Deck,-32} {r.Score * 100,6:0.0} %   SSIM {r.Ssim:0.000}   ({r.Slides.Count} frames)");
        break;
    default:
        Console.WriteLine("""
            Fidelity: compares our HTML rendering with PowerPoint, slide by slide.

              run [deck…]          render and compare (default)
              render [deck…]       convert with the engine and take screenshots with a headless browser
              compare [deck…]      compare screenshots with the references, write artifacts/fidelity/report.html
              generate [deck…]     create the generated test decks with PowerPoint (Windows)
              reference [deck…]    render reference images with PowerPoint (Windows); --force re-renders
              animate [deck…]      compare the player's animation frames with PowerPoint's videos
              animate-reference [deck…]  sample PowerPoint's videos of animated slides (Windows, ffmpeg); --force
              harvest-patterns     read the preset pattern tiles from PowerPoint's rendering of the fills deck
              harvest-cmyk         measure PowerPoint's colors of profile-less CMYK pictures (Windows)
            """);
        break;
}
return 0;

static void Summarize(List<DeckResult> results)
{
    foreach (var r in results)
        Console.WriteLine($"  {r.Deck,-32} {r.Score * 100,6:0.0} %   SSIM {r.Ssim:0.000}   ({r.Slides.Count} slides)");
    if (results.Count > 0)
        Console.WriteLine($"  {"overall",-32} {results.SelectMany(r => r.Slides).Average(s => s.Score.Score) * 100,6:0.0} %");
    Console.WriteLine($"Report: {Report.Write(results)}");
}
