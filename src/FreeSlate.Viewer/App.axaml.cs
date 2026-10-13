using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace FreeSlate.Viewer;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            if (desktop.Args is [var path, ..])
                window.Opened += async (_, _) => await window.LoadAsync(path);
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
