using System.Diagnostics;
using System.Runtime.Versioning;

namespace Fidelity.Word;

/// <summary>
/// Rasterizes the pages of a PDF with Windows' own PDF renderer (Windows.Data.Pdf), through Windows PowerShell, which
/// reaches the WinRT API without a package (rasterize-pdf.ps1 next to this file).
/// </summary>
[SupportedOSPlatform("windows")]
public static class PdfPages
{
    static string Script => Path.Combine(Repo.Root, "tools", "Fidelity", "Word", "rasterize-pdf.ps1");

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
                     "-Sizes", string.Join(',', (sizes ?? []).Select(s => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{s.Width}x{s.Height}"))),
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
        return output.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Path.Combine(outputDir, line.Split(' ')[0]))
            .ToList();
    }
}
