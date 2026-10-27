using System.Diagnostics;
using System.Runtime.Versioning;

namespace Fidelity.Word;

/// <summary>
/// Rasterizes the pages of a PDF with Windows' own PDF renderer (Windows.Data.Pdf), through Windows PowerShell, which
/// reaches the WinRT API without a package (rasterize-pdf.ps1 next to this file); then repaints with MuPDF what that
/// renderer draws wrong, where Python can import PyMuPDF (repaint.py next to this file): content under soft masks, Type
/// 3 text filled with a pattern, pictures thinner than a pixel.
/// </summary>
[SupportedOSPlatform("windows")]
public static class PdfPages
{
    static string Script => Path.Combine(Repo.Root, "tools", "Fidelity", "Word", "rasterize-pdf.ps1");

    static string RepaintScript => Path.Combine(Repo.Root, "tools", "Fidelity", "Word", "repaint.py");

    static string UnshearScript => Path.Combine(Repo.Root, "tools", "Fidelity", "Word", "unshear.py");

    /// <summary>
    /// Whether pages are repainted (repaint.py): Python on the path imports PyMuPDF and Pillow, and FIDELITY_NO_MUPDF
    /// is not 1. Checked once; without it the pages stay as Windows draws them, which a note says.
    /// </summary>
    static readonly Lazy<bool> Repaints = new(() =>
    {
        if (Environment.GetEnvironmentVariable("FIDELITY_NO_MUPDF") == "1")
            return false;
        try
        {
            using var process = Python("-I", "-c", "import pymupdf, PIL");
            if (process.WaitForExit(60_000) && process.ExitCode == 0)
                return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        Console.WriteLine("  note: no Python with PyMuPDF, pages stay as Windows' PDF renderer draws them (docs/fidelity.md)");
        return false;
    });

    static Process Python(params string[] args)
    {
        var info = new ProcessStartInfo("python")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            info.ArgumentList.Add(arg);
        return Process.Start(info)!;
    }

    static string Sizes(IReadOnlyList<(double Width, double Height)>? sizes) =>
        string.Join(',', (sizes ?? []).Select(s => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{s.Width}x{s.Height}")));

    /// <summary>
    /// Writes page-001.png, page-002.png … at 96 dpi times <paramref name="scale"/>; returns the files in page order.
    /// With <paramref name="sizes"/> (points) page i is cut to that size from its top left.
    /// </summary>
    public static List<string> Rasterize(string pdf, string outputDir, double scale = 1, IReadOnlyList<(double Width, double Height)>? sizes = null)
    {
        Directory.CreateDirectory(outputDir);
        var info = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in new[]
                 {
                     "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Script,
                     "-Pdf", Path.GetFullPath(pdf), "-Out", Path.GetFullPath(outputDir),
                     "-Scale", scale.ToString(System.Globalization.CultureInfo.InvariantCulture),
                     "-Sizes", Sizes(sizes),
                 })
        {
            info.ArgumentList.Add(arg);
        }
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Rasterizing {pdf} took too long.");
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Rasterizing {pdf} failed: {error.Result.Trim().Split('\n')[0]}");
        var files = output.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Path.Combine(outputDir, line.Split(' ')[0]))
            .ToList();
        if (Repaints.Value)
            Repaint(pdf, outputDir, scale, sizes);
        return files;
    }

    /// <summary>
    /// A copy of Word's PDF whose pictures stand without the tiny shear Word gives them, which makes Windows draw their
    /// text unhinted now and then (unshear.py), in the temporary folder; null where it has none or without PyMuPDF.
    /// </summary>
    public static string? Unsheared(string pdf)
    {
        if (!Repaints.Value)
            return null;
        var copy = Path.Combine(Path.GetTempPath(), $"fidelity-{Guid.NewGuid():N}.pdf");
        using var process = Python("-I", UnshearScript, "--pdf", Path.GetFullPath(pdf), "--out", copy);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Unshearing {pdf} took too long.");
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Unshearing {pdf} failed: {error.Result.Trim().Split('\n').LastOrDefault()}");
        _ = output.Result;
        return File.Exists(copy) ? copy : null;
    }

    /// <summary>Repaints the pages Windows drew where it draws wrong, with MuPDF's pixels (repaint.py).</summary>
    static void Repaint(string pdf, string outputDir, double scale, IReadOnlyList<(double Width, double Height)>? sizes)
    {
        using var process = Python("-I", RepaintScript, "--pdf", Path.GetFullPath(pdf), "--out", Path.GetFullPath(outputDir),
            "--scale", scale.ToString(System.Globalization.CultureInfo.InvariantCulture), "--sizes", Sizes(sizes));
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(300_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Repainting {pdf} took too long.");
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Repainting {pdf} failed: {error.Result.Trim().Split('\n').LastOrDefault()}");
        _ = output.Result;
    }
}
