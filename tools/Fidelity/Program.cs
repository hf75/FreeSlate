using System.Globalization;
using Fidelity;
using Fidelity.Compare;
using Fidelity.PowerPoint;
using Fidelity.Word;

// PowerPoint shows chart numbers in the system's regional format, which the references were made with; the tool
// itself reads and writes invariantly.
Repo.Regional = CultureInfo.CurrentCulture;
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var command = args.FirstOrDefault() ?? "run";
var rest = args.Skip(1).Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
var force = args.Contains("--force");
var decks = Repo.Select(rest);

switch (command)
{
    case "generate" when OperatingSystem.IsWindows():
    {
        // Word documents are named "docx/generated/<name>"; "docx" stands for all of them.
        var wordNames = rest.Where(n => n.TrimEnd('/') is "docx" or "docx/generated" || n.StartsWith(Repo.DocumentPrefix, StringComparison.Ordinal)).ToList();
        if (wordNames.Count > 0)
        {
            const string Generated = Repo.DocumentPrefix + "generated/";
            if (wordNames.FirstOrDefault(n => n.TrimEnd('/') is not ("docx" or "docx/generated") && !n.StartsWith(Generated, StringComparison.Ordinal)) is { } other)
            {
                Console.Error.WriteLine($"Only generated documents can be generated: {other}");
                return 1;
            }
            DocumentGenerator.Run(Path.Combine(Repo.DocumentCorpus, "generated"),
                wordNames.Where(n => n.StartsWith(Generated, StringComparison.Ordinal)).Select(n => n[Generated.Length..]).ToList());
        }
        var deckNames = rest.Except(wordNames).ToList();
        if (deckNames.Count > 0 || wordNames.Count == 0)
            CorpusGenerator.Run(Path.Combine(Repo.Corpus, "generated"), deckNames);
        break;
    }
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
    case "transitions-reference" when OperatingSystem.IsWindows():
        Transitions.Reference(decks, force);
        break;
    case "harvest-cmyk" when OperatingSystem.IsWindows():
        CmykHarvest.Run();
        break;
    case "fetch":
        ExternalDecks.Fetch();
        break;
    case "generate" or "reference" or "calibrate" or "animate-reference" or "transitions-reference" or "harvest-cmyk":
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
    {
        // --slide=N checks one slide's diagram; --tree prints its layout tree before text fitting.
        var files = rest.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        int? slideOnly = args.FirstOrDefault(a => a.StartsWith("--slide=", StringComparison.Ordinal)) is { } s ? int.Parse(s["--slide=".Length..]) : null;
        if (slideOnly is { } one && args.Contains("--tree"))
        {
            SmartArtCheck.Tree(files[0], one);
            break;
        }
        if (slideOnly == null)
            SmartArtCheck.Run(files);
        SmartArtCheck.Geometry(files, args.Contains("--verbose"), slideOnly);
        break;
    }
    case "fonts":
        EmbeddedFonts.Extract(decks);
        break;
    case "jxr":
        JpegXrCheck.Run(rest.FirstOrDefault() ?? ".");
        break;
    case "animate":
        var animated = Animations.Run(decks);
        foreach (var r in animated)
            Console.WriteLine($"  {r.Deck,-32} {r.Score * 100,6:0.0} %   SSIM {r.Ssim:0.000}   ({r.Slides.Count} frames)");
        break;
    case "transitions":
        // --slides=3,7 compares only those transitions (while working on them).
        var only = args.FirstOrDefault(a => a.StartsWith("--slides=", StringComparison.Ordinal)) is { } list
            ? list["--slides=".Length..].Split(',').Select(int.Parse).ToHashSet()
            : null;
        foreach (var r in Transitions.Run(decks, only))
            Console.WriteLine($"  {r.Deck,-32} {r.Score * 100,6:0.0} %   SSIM {r.Ssim:0.000}   ({r.Slides.Count} frames)");
        break;
    default:
        Console.WriteLine("""
            Fidelity: compares our HTML rendering with PowerPoint, slide by slide, and with Word, page by page.

              run [deck…]          render and compare (default); a folder stands for its decks ("generated")
              render [deck…]       convert with the engine and take screenshots with a headless browser
              compare [deck…]      compare screenshots with the references, write artifacts/fidelity/report.html
              generate [deck…]     create the generated test decks with PowerPoint, the generated documents
                                   ("docx/generated/<name>", "docx" for all) with Word (Windows)
              reference [deck…]    render reference images with PowerPoint, of Word documents ("docx/…") with Word
                                   (Windows); --force re-renders
              animate [deck…]      compare the player's animation frames with PowerPoint's videos
              animate-reference [deck…]  sample PowerPoint's videos of animated slides (Windows, ffmpeg); --force
              transitions [deck…]  compare the player's slide transitions with PowerPoint's videos
              transitions-reference [deck…]  sample PowerPoint's videos of the transitions (Windows, ffmpeg); --force
              fonts [deck…]        write the decks' embedded fonts as TrueType files to artifacts/fidelity/fonts
              jxr <dir>            decode every .jxr in a directory and compare it with the .png next to it
              harvest-patterns     read the preset pattern tiles from PowerPoint's rendering of the fills deck
              harvest-cmyk         measure PowerPoint's colors of profile-less CMYK pictures (Windows)
              fetch                download other projects' test decks (spec/downloads/testdecks, decks "external/…")
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
