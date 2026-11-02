using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;

[assembly: SupportedOSPlatform("browser")]

namespace FreeSlate.Editor.Browser;

/// <summary>The editor as a page: the settings lie in the page's storage, as a page has no lasting files.</summary>
static partial class Program
{
    const string SettingsKey = "FreeSlate.Editor.Settings";

    [JSImport("globalThis.localStorage.getItem")]
    private static partial string? GetItem(string key);

    [JSImport("globalThis.localStorage.setItem")]
    private static partial void SetItem(string key, string value);

    static Task Main()
    {
        Settings.Read = () => GetItem(SettingsKey);
        Settings.Write = text => SetItem(SettingsKey, text);
        return AppBuilder.Configure<App>().WithInterFont().StartBrowserAppAsync("out");
    }
}
