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

    public PowerPointSession()
    {
        startedBySession = Process.GetProcessesByName("POWERPNT").Length == 0;
        var type = Type.GetTypeFromProgID("PowerPoint.Application")
                   ?? throw new InvalidOperationException("PowerPoint is not installed.");
        app = Activator.CreateInstance(type)!;
        Version = $"{app.Version}.{app.Build}";
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

    public static void SaveAndClose(dynamic presentation, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (File.Exists(path))
            File.Delete(path);
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
        var presentation = Open(pptxPath, password);
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
                presentation.Slides[i].Export(file, "PNG", pixelWidth, pixelHeight);
                files.Add(file);
            }
            return files;
        }
        finally
        {
            Close(presentation);
        }
    }

    public void Dispose()
    {
        try
        {
            if (startedBySession && (int)app.Presentations.Count == 0)
                app.Quit();
        }
        finally
        {
            Marshal.FinalReleaseComObject(app);
        }
    }
}
