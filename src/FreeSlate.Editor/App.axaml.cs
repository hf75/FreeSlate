using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FreeSlate.Editor.Localization;

namespace FreeSlate.Editor;

/// <summary>The editor on any platform: a window on the desktop, the page in a browser.</summary>
public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        UiLanguages.Show(UiLanguages.Best(LanguageAsked(desktop?.Args) ?? Settings.Current.Language));

        if (desktop != null)
            desktop.MainWindow = new EditorWindow();
        else if (ApplicationLifetime is ISingleViewApplicationLifetime page)
            page.MainView = new EditorView();

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>The language of `--language de`: for this run only, the setting stays.</summary>
    static string? LanguageAsked(string[]? args)
    {
        var at = args == null ? -1 : Array.IndexOf(args, "--language");
        return at >= 0 && at + 1 < args!.Length ? args[at + 1] : null;
    }
}
