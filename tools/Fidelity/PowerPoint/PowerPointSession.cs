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

    public dynamic Open(string path) =>
        app.Presentations.Open(Path.GetFullPath(path), MsoTrue, MsoFalse, MsoFalse);

    public dynamic Create() => app.Presentations.Add(MsoFalse);

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
    public List<string> ExportSlides(string pptxPath, string outputDir, int? width = null)
    {
        Directory.CreateDirectory(outputDir);
        var presentation = Open(pptxPath);
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
