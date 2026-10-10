using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// Late-bound COM automation of an installed PowerPoint. Presentations are opened without windows;
/// PowerPoint is only closed again if this session started it.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PowerPointSession : IDisposable
{
    public const int MsoTrue = -1;
    public const int MsoFalse = 0;
    const int PpSaveAsOpenXmlPresentation = 24;

    readonly dynamic app;
    readonly bool startedBySession;
    readonly Process? process;

    public PowerPointSession()
    {
        startedBySession = Process.GetProcessesByName("POWERPNT").Length == 0;
        var type = Type.GetTypeFromProgID("PowerPoint.Application")
                   ?? throw new InvalidOperationException("PowerPoint is not installed.");
        app = Activator.CreateInstance(type)!;
        Version = $"{app.Version}.{app.Build}";
        if (startedBySession)
        {
            process = Process.GetProcessesByName("POWERPNT").FirstOrDefault();
            // No alerts, and no macros from the decks it opens.
            const int PpAlertsNone = 1, MsoAutomationSecurityForceDisable = 3;
            try
            {
                app.DisplayAlerts = PpAlertsNone;
                app.AutomationSecurity = MsoAutomationSecurityForceDisable;
            }
            catch (COMException) { }
        }
    }

    /// <summary>
    /// Limit for each step of a slide export (opening the deck, exporting a slide): PowerPoint may wait for a click on a
    /// dialog nobody sees (a damaged file's repair prompt). Over the limit a PowerPoint this session started is ended,
    /// which fails the step, and the session is <see cref="Broken"/>; null waits as long as it takes.
    /// </summary>
    public TimeSpan? StepLimit { get; set; }

    /// <summary>Whether the watchdog ended PowerPoint; a broken session is to be replaced by a new one.</summary>
    public bool Broken { get; private set; }

    T Step<T>(Func<T> work)
    {
        if (StepLimit is not { } limit || process == null)
            return work();
        using var watchdog = new Timer(_ =>
        {
            Broken = true;
            try { process.Kill(); } catch (InvalidOperationException) { }
        }, null, limit, Timeout.InfiniteTimeSpan);
        return work();
    }

    /// <summary>PowerPoint version and build, e.g. "16.0.20430".</summary>
    public string Version { get; }

    /// <summary>Opens a presentation read-only; a protected one with its password ("path::password::").</summary>
    public dynamic Open(string path, string? password = null) =>
        app.Presentations.Open(Path.GetFullPath(path) + (password != null ? $"::{password}::" : ""), MsoTrue, MsoFalse, MsoFalse);

    /// <summary>A new presentation; some galleries (SmartArt, charts) need a document window, which is then minimized.</summary>
    public dynamic Create(bool window = false)
    {
        var presentation = app.Presentations.Add(window ? MsoTrue : MsoFalse);
        if (window)
        {
            const int PpWindowMinimized = 2;
            try { app.WindowState = PpWindowMinimized; } catch (COMException) { }
        }
        return presentation;
    }

    /// <param name="embedFonts">Embed the fonts the presentation uses (PowerPoint embeds the characters used, in all styles).</param>
    public static void SaveAndClose(dynamic presentation, string path, bool embedFonts = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (File.Exists(path))
            File.Delete(path);
        if (embedFonts)
            presentation.SaveAs(Path.GetFullPath(path), PpSaveAsOpenXmlPresentation, MsoTrue);
        else
            presentation.SaveAs(Path.GetFullPath(path), PpSaveAsOpenXmlPresentation);
        Close(presentation);
    }

    public static void Close(dynamic presentation)
    {
        presentation.Close();
        Marshal.FinalReleaseComObject(presentation);
    }

    /// <summary>Exports every slide as PNG; the size follows the slide size at 96 dpi unless a width is given.</summary>
    public List<string> ExportSlides(string pptxPath, string outputDir, int? width = null, string? password = null)
    {
        Directory.CreateDirectory(outputDir);
        var presentation = Step(() => Open(pptxPath, password));
        try
        {
            double slideWidth = presentation.PageSetup.SlideWidth;   // points
            double slideHeight = presentation.PageSetup.SlideHeight;
            var pixelWidth = width ?? (int)Math.Round(slideWidth * 96 / 72);
            var pixelHeight = (int)Math.Round(pixelWidth * slideHeight / slideWidth);

            var files = new List<string>();
            int count = presentation.Slides.Count;
            for (var i = 1; i <= count; i++)
            {
                var file = Path.Combine(outputDir, $"slide-{i:000}.png");
                var slide = presentation.Slides[i];
                // A statement lambda: a dynamic call of the void Export has no value to return.
                Step(() => { slide.Export(file, "PNG", pixelWidth, pixelHeight); return 0; });
                files.Add(file);
            }
            return files;
        }
        finally
        {
            Close(presentation);
        }
    }

    /// <summary>The numbers of the slides whose main animation sequence has effects.</summary>
    public List<int> AnimatedSlides(string pptxPath, string? password = null)
    {
        var presentation = Open(pptxPath, password);
        try
        {
            var result = new List<int>();
            int count = presentation.Slides.Count;
            for (var i = 1; i <= count; i++)
            {
                if ((int)presentation.Slides[i].TimeLine.MainSequence.Count > 0)
                    result.Add(i);
            }
            return result;
        }
        finally
        {
            Close(presentation);
        }
    }

    /// <summary>
    /// PowerPoint's video of one slide (the others deleted, its transition removed): the main sequence's steps play
    /// one after another from the slide's start. Waits until the video is written.
    /// </summary>
    public void ExportSlideVideo(string pptxPath, int slide, string mp4Path, int height, int fps, string? password = null)
    {
        const int PpMediaTaskStatusInProgress = 1, PpMediaTaskStatusQueued = 2, PpMediaTaskStatusDone = 3, PpEffectNone = 0;
        var presentation = Open(pptxPath, password);
        try
        {
            int count = presentation.Slides.Count;
            for (var i = count; i >= 1; i--)
            {
                if (i != slide)
                    presentation.Slides[i].Delete();
            }
            presentation.Slides[1].SlideShowTransition.EntryEffect = PpEffectNone;
            presentation.Slides[1].SlideShowTransition.Duration = 0f;
            if (File.Exists(mp4Path))
                File.Delete(mp4Path);
            presentation.CreateVideo(Path.GetFullPath(mp4Path), false, 1, height, fps, 100);
            var watch = Stopwatch.StartNew();
            int status;
            do
            {
                Thread.Sleep(200);
                status = presentation.CreateVideoStatus;
            } while (status is PpMediaTaskStatusInProgress or PpMediaTaskStatusQueued && watch.Elapsed < TimeSpan.FromMinutes(5));
            if (status != PpMediaTaskStatusDone)
                throw new InvalidOperationException($"PowerPoint did not export the video of slide {slide} (status {status}).");
        }
        finally
        {
            presentation.Saved = MsoTrue;
            Close(presentation);
        }
    }

    /// <summary>The slides (1-based) that are entered with a transition, with the transition's duration (s).</summary>
    public List<(int Slide, double Duration)> TransitionSlides(string pptxPath, string? password = null)
    {
        const int PpEffectNone = 0;
        var presentation = Open(pptxPath, password);
        try
        {
            var result = new List<(int, double)>();
            int count = presentation.Slides.Count;
            for (var i = 2; i <= count; i++)
            {
                var transition = presentation.Slides[i].SlideShowTransition;
                if ((int)transition.EntryEffect != PpEffectNone)
                    result.Add((i, (double)(float)transition.Duration));
            }
            return result;
        }
        finally
        {
            Close(presentation);
        }
    }

    /// <summary>
    /// PowerPoint's video of the transition into a slide: that slide and the one before it alone, without animations,
    /// each shown <paramref name="seconds"/> (the transition begins after the first one's time). Waits until the
    /// video is written.
    /// </summary>
    public void ExportTransitionVideo(string pptxPath, int slide, string mp4Path, int height, int fps, double seconds, string? password = null)
    {
        const int PpMediaTaskStatusInProgress = 1, PpMediaTaskStatusQueued = 2, PpMediaTaskStatusDone = 3, PpEffectNone = 0;
        var presentation = Open(pptxPath, password);
        try
        {
            int count = presentation.Slides.Count;
            for (var i = count; i >= 1; i--)
            {
                if (i != slide && i != slide - 1)
                    presentation.Slides[i].Delete();
            }
            presentation.Slides[1].SlideShowTransition.EntryEffect = PpEffectNone;
            for (var i = 1; i <= 2; i++)
            {
                var sequence = presentation.Slides[i].TimeLine.MainSequence;
                for (int j = sequence.Count; j >= 1; j--)
                    sequence[j].Delete();
            }
            if (File.Exists(mp4Path))
                File.Delete(mp4Path);
            presentation.CreateVideo(Path.GetFullPath(mp4Path), false, (int)Math.Round(seconds), height, fps, 100);
            var watch = Stopwatch.StartNew();
            int status;
            do
            {
                Thread.Sleep(200);
                status = presentation.CreateVideoStatus;
            } while (status is PpMediaTaskStatusInProgress or PpMediaTaskStatusQueued && watch.Elapsed < TimeSpan.FromMinutes(5));
            if (status != PpMediaTaskStatusDone)
                throw new InvalidOperationException($"PowerPoint did not export the video of the transition into slide {slide} (status {status}).");
        }
        finally
        {
            presentation.Saved = MsoTrue;
            Close(presentation);
        }
    }

    public void Dispose()
    {
        try
        {
            if (startedBySession && !Broken && (int)app.Presentations.Count == 0)
                app.Quit();
        }
        catch (COMException) when (Broken)
        {
            // PowerPoint was ended by the watchdog.
        }
        finally
        {
            Marshal.FinalReleaseComObject(app);
            // A PowerPoint this session started does not always end on Quit after damaged decks (seen with no
            // presentation open and no window); it is ended after a while.
            if (process is { } owned && !owned.WaitForExit(TimeSpan.FromSeconds(10)))
            {
                try { owned.Kill(); } catch (InvalidOperationException) { }
            }
        }
    }
}
