using Avalonia;

namespace FreeSlate.Presenter;

static class Program
{
    /// <summary>Starts the presenter; a presentation's path as the first argument opens it right away.</summary>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
}
