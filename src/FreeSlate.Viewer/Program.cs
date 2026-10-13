using Avalonia;

namespace FreeSlate.Viewer;

static class Program
{
    /// <summary>Starts the viewer; a Word document's path as the first argument opens it right away.</summary>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
