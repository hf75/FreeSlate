using Avalonia;

namespace FreeSlate.Viewer;

static class Program
{
    /// <summary>Starts the viewer; a Word document's path as the first argument opens it right away.</summary>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <remarks>
    /// The GPU's cache gets room for the textures of the pictures in view (Avalonia's own 28 MB hold three photos of a
    /// page's width, see SkiaScene.Draw's pictureScale): with four pages in view at 50 % they were uploaded anew every frame.
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace()
            .With(new SkiaOptions { MaxGpuResourceSizeBytes = 64L << 20 });
}
