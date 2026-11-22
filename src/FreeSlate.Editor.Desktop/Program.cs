using Avalonia;

namespace FreeSlate.Editor.Desktop;

static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Claude at the gesture ring, where a key for Anthropic's API is set.
        Asking.Claude.Link = ClaudeLink.FromEnvironment();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
