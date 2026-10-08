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
                     "--disable-extensions", "--force-device-scale-factor=1", "--font-render-hinting=none",
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
}
