using System.Diagnostics;

namespace Fidelity.Rendering;

/// <summary>Takes screenshots of HTML pages with a locally installed Chromium browser in headless mode.</summary>
public sealed class Browser(string executable)
{
    public string Executable { get; } = executable;

    /// <summary>FIDELITY_BROWSER, else Chrome, else Edge.</summary>
    public static Browser Find()
    {
        string?[] candidates =
        [
            Environment.GetEnvironmentVariable("FIDELITY_BROWSER"),
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            "/usr/bin/google-chrome",
            "/usr/bin/chromium",
            "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
        ];
        var found = candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c) && File.Exists(c))
                    ?? throw new InvalidOperationException("No Chromium-based browser found; set FIDELITY_BROWSER.");
        return new Browser(found);
    }

    /// <summary>Renders the page at exactly width × height CSS pixels (device scale 1).</summary>
    public void Screenshot(string url, string pngPath, int width, int height, string profileDirectory)
    {
        Directory.CreateDirectory(profileDirectory);
        if (File.Exists(pngPath))
            File.Delete(pngPath);

        var info = new ProcessStartInfo(Executable)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (var arg in new[]
                 {
                     "--headless=new", "--disable-gpu", "--hide-scrollbars", "--no-first-run", "--no-default-browser-check",
                     "--disable-extensions", "--force-device-scale-factor=1",
                     // PowerPoint exports with grayscale anti-aliasing; LCD (ClearType) text would add colored fringes.
                     "--disable-lcd-text",
                     $"--user-data-dir={profileDirectory}", $"--window-size={width},{height}",
                     "--virtual-time-budget=5000", $"--screenshot={pngPath}", url,
                 })
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEndAsync();
        process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Browser did not finish: {url}");
        }
        if (!File.Exists(pngPath))
            throw new InvalidOperationException($"Browser produced no screenshot for {url}");
    }

    /// <summary>Prints the page to PDF as the browser prints it (its @page rules decide the paper), without header and footer.</summary>
    public void PrintToPdf(string url, string pdfPath, string profileDirectory)
    {
        Directory.CreateDirectory(profileDirectory);
        if (File.Exists(pdfPath))
            File.Delete(pdfPath);
        var info = new ProcessStartInfo(Executable)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (var arg in new[]
                 {
                     "--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check", "--disable-extensions",
                     "--no-pdf-header-footer", $"--user-data-dir={profileDirectory}", "--virtual-time-budget=5000",
                     $"--print-to-pdf={pdfPath}", url,
                 })
        {
            info.ArgumentList.Add(arg);
        }
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEndAsync();
        process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Browser did not finish printing: {url}");
        }
        if (!File.Exists(pdfPath))
            throw new InvalidOperationException($"Browser produced no PDF for {url}");
    }
}
