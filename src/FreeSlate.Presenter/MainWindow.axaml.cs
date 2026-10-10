using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FreeSlate.Opc;

namespace FreeSlate.Presenter;

/// <summary>
/// Opens a PowerPoint presentation, shows it as FreeSlate's HTML document in the platform's web view and starts its
/// slide show (FreeSlate's player) full screen.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Fits the slides to the window's width and tells the window when the page's slide show begins and ends (the
    /// player's .fs-show overlay comes and goes), also when it was started or ended in the page itself.
    /// </summary>
    const string PageScript = """
        (() => {
          if (window.freeSlatePresenter) return;
          window.freeSlatePresenter = true;
          const deck = document.querySelector('main.deck'), slide = deck && deck.querySelector('.slide');
          const fit = () => { if (slide) deck.style.zoom = Math.min(1, document.documentElement.clientWidth / (parseFloat(slide.style.width) + 32)); };
          fit();
          window.addEventListener('resize', fit);
          let showing = false;
          new MutationObserver(() => {
            const now = !!document.querySelector('.fs-show');
            if (now !== showing) { showing = now; invokeCSharpAction(now ? 'show-started' : 'show-ended'); }
          }).observe(document.body, { childList: true });
        })();
        """;

    string? locked;                       // a presentation waiting for its password
    bool showing;
    WindowState stateBeforeShow = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();
        Web.EnvironmentRequested += (_, e) =>
        {
            // WebView2 keeps its profile next to the executable unless told otherwise.
            if (e is WindowsWebView2EnvironmentRequestedEventArgs webView2 && string.IsNullOrEmpty(webView2.UserDataFolder))
                webView2.UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreeSlate", "Presenter", "WebView2");
        };
        Web.NavigationCompleted += async (_, e) =>
        {
            if (!e.IsSuccess)
                return;
            await Web.InvokeScript(PageScript);
            StartButton.IsEnabled = true;
        };
        Web.WebMessageReceived += (_, e) => Dispatcher.UIThread.Post(() =>
        {
            if (e.Body == "show-started") EnterShow();
            else if (e.Body == "show-ended") LeaveShow();
        });
        // During the show the page takes the keys; the window passes on those it gets itself.
        Activated += (_, _) =>
        {
            if (showing)
                FocusPage();
        };
        AddHandler(KeyDownEvent, OnShowKey, RoutingStrategies.Tunnel);
    }

    async void OnShowKey(object? sender, KeyEventArgs e)
    {
        if (!showing)
            return;
        var command = e.Key switch
        {
            Key.Escape => "FreeSlate.stop()",
            Key.Right or Key.Down or Key.Space or Key.PageDown or Key.Enter or Key.N => "FreeSlate.next()",
            Key.Left or Key.Up or Key.Back or Key.PageUp or Key.P => "FreeSlate.previous()",
            _ => null,
        };
        if (command == null)
            return;
        e.Handled = true;
        await Web.InvokeScript(command);
    }

    void FocusPage() => Dispatcher.UIThread.Post(() => Web.Focus(), DispatcherPriority.Background);

    async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "PowerPoint-Präsentation öffnen",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("PowerPoint-Präsentationen") { Patterns = ["*.pptx", "*.pptm", "*.ppsx", "*.ppsm", "*.potx", "*.potm"] }],
        });
        if (files is [var file] && file.TryGetLocalPath() is { } path)
            await LoadAsync(path);
    }

    async void OnUnlockClick(object? sender, RoutedEventArgs e)
    {
        if (locked != null)
            await LoadAsync(locked, PasswordBox.Text);
    }

    async void OnStartClick(object? sender, RoutedEventArgs e)
    {
        EnterShow();
        await Web.InvokeScript("FreeSlate.start()");
    }

    /// <summary>Converts the presentation with FreeSlate and shows the document.</summary>
    public async Task LoadAsync(string path, string? password = null)
    {
        var name = Path.GetFileName(path);
        StatusText.Text = $"{name} wird geladen …";
        OpenButton.IsEnabled = StartButton.IsEnabled = false;
        try
        {
            var html = await Task.Run(() => PptxConverter.ToHtml(path, password: password));
            // A file, not NavigateToString: the document carries its pictures and fonts and is often large.
            var document = Path.Combine(Path.GetTempPath(), "FreeSlate.Presenter", Path.GetFileNameWithoutExtension(path) + ".html");
            Directory.CreateDirectory(Path.GetDirectoryName(document)!);
            await File.WriteAllTextAsync(document, html);
            locked = null;
            PasswordPanel.IsVisible = false;
            Placeholder.IsVisible = false;
            Web.IsVisible = true;
            Web.Navigate(new Uri(document));
            Title = $"{name} – FreeSlate Presenter";
            StatusText.Text = name;
        }
        catch (PasswordException error)
        {
            locked = path;
            PasswordBox.Text = "";
            PasswordPanel.IsVisible = true;
            PasswordBox.Focus();
            StatusText.Text = error.WrongPassword ? "Das Passwort stimmt nicht." : $"{name} ist passwortgeschützt.";
            StartButton.IsEnabled = Web.IsVisible;
        }
        catch (Exception error)
        {
            StatusText.Text = $"{name} lässt sich nicht öffnen: {error.Message}";
            StartButton.IsEnabled = Web.IsVisible;
        }
        finally
        {
            OpenButton.IsEnabled = true;
        }
    }

    void EnterShow()
    {
        if (showing)
            return;
        showing = true;
        stateBeforeShow = WindowState;
        Toolbar.IsVisible = false;
        WindowState = WindowState.FullScreen;
        FocusPage();
    }

    void LeaveShow()
    {
        if (!showing)
            return;
        showing = false;
        Toolbar.IsVisible = true;
        WindowState = stateBeforeShow;
    }
}
