using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Platform;
using FreeSlate.Editor.Writing;

[assembly: SupportedOSPlatform("browser")]

namespace FreeSlate.Editor.Browser;

/// <summary>
/// The editor as a page: the settings lie in the page's storage, as a page has no lasting files; the shelf's fonts lie beside it
/// (fonts/), fetched when a document needs them (see EngineFonts.Prepare).
/// </summary>
static partial class Program
{
    const string SettingsKey = "FreeSlate.Editor.Settings";

    [JSImport("globalThis.localStorage.getItem")]
    private static partial string? GetItem(string key);

    [JSImport("globalThis.localStorage.setItem")]
    private static partial void SetItem(string key, string value);

    /// <summary>The page's address (main.js), against which its files are fetched.</summary>
    [JSImport("page.address", "main.js")]
    private static partial string Address();

    static Task Main()
    {
        Settings.Read = () => GetItem(SettingsKey);
        Settings.Write = text => SetItem(SettingsKey, text);
        var page = new HttpClient { BaseAddress = new Uri(Address()) };
        EngineFonts.Fetch = async file =>
        {
            using var response = await page.GetAsync("fonts/" + file);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync() : null;
        };
        return AppBuilder.Configure<App>().WithInterFont().AfterSetup(_ => GiveFontsToTheEngine()).StartBrowserAppAsync("out");
    }

    /// <summary>A page has no system fonts: the engine finds the app's own where it looks for them on Linux-like systems.</summary>
    static void GiveFontsToTheEngine()
    {
        const string folder = "/usr/share/fonts";
        Directory.CreateDirectory(folder);
        foreach (var face in new[] { "Inter-Regular.ttf", "Inter-Bold.ttf" })
        {
            using var source = AssetLoader.Open(new Uri("avares://Avalonia.Fonts.Inter/Assets/" + face));
            using var target = File.Create(Path.Combine(folder, face));
            source.CopyTo(target);
        }
    }
}
