using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FreeSlate.Editing;
using FreeSlate.Opc;
using FreeSlate.Rendering;
using FreeSlate.Skia;

namespace FreeSlate.Viewer;

/// <summary>
/// Opens a Word document and shows its pages drawn by Skia, or FreeSlate's HTML document in the platform's web view, as
/// Word prints them; on Windows with Word installed also Word's own pages, beside FreeSlate's, over them or against
/// them. A PowerPoint presentation shows its slides drawn by Skia, as PowerPoint shows them.
/// </summary>
public partial class MainWindow : Window
{
    static readonly string PageScript = Resource("viewer.js");
    static readonly string[] Extensions = [".docx", ".docm", ".dotx", ".dotm"];
    static readonly string[] SlideExtensions = [".pptx", ".pptm", ".ppsx", ".ppsm", ".potx", ".potm"];
    // The zoom box's entries as the page script takes them: a fit or a factor.
    static readonly string[] Zooms = ["'auto'", "'width'", "'page'", "0.5", "0.75", "1", "1.25", "1.5", "2"];
    // The compare box's entries.
    static readonly string[] Comparisons = ["off", "side", "blend", "diff"];
    /// <summary>Pixels of Word's page pictures per CSS pixel: sharp up to a zoom of 200 % on a normal screen.</summary>
    const double WordScale = 2;

    readonly DocumentServer server = new();
    readonly Settings settings = Settings.Load();
    // This run's files: the documents' HTML, copies of documents another program holds open, Word's renderings.
    readonly string work = Path.Combine(Path.GetTempPath(), "FreeSlate.Viewer", Guid.NewGuid().ToString("N"));
    readonly bool wordInstalled = OperatingSystem.IsWindows() && WordPages.Installed;
    readonly Dictionary<string, WordRendering> renderings = [];
    readonly Dictionary<WordRendering, Uri> published = [];
    readonly SemaphoreSlim wordGate = new(1, 1);
    readonly DispatcherTimer reloadTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };

    View? view;                // the document shown
    string? locked;            // a document waiting for its password
    string? failed;            // a document that could not be shown, which F5 tries again
    string? position;          // where the window was in the document before it was loaded again
    bool scripted;             // whether the web view shows the document with the page script
    int pageCount;
    double seconds;            // how long FreeSlate took for the document
    string? rendering;         // the view Word renders now
    int renders;
    int retries;
    FileSystemWatcher? watcher;

    /// <summary>
    /// The document shown, open to change it (Skia only): a Word document or a presentation, and the editing session over
    /// it; and when it was last saved from here, whose own change of the file is not a reason to load it again.
    /// </summary>
    IEditableDocument? document;
    EditSession? session;
    DateTime savedAt;

    /// <summary>A document as shown: the file, the file FreeSlate and Word read (a copy when another program holds
    /// the document open), its password, whether with markup, its HTML or its pages' scenes (as Skia draws them), its
    /// version, which Word's renderings belong to, and whether it is a presentation (its pages slides).</summary>
    sealed record View(string Path, string Source, string? Password, bool Markup, string? Html, List<PageScene>? Scenes, string Key, bool Slides = false);

    public MainWindow()
    {
        InitializeComponent();
        Web.EnvironmentRequested += (_, e) =>
        {
            e.EnableDevTools = true;
            // WebView2 keeps its profile next to the executable unless told otherwise.
            if (e is WindowsWebView2EnvironmentRequestedEventArgs webView2 && string.IsNullOrEmpty(webView2.UserDataFolder))
                webView2.UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FreeSlate", "Viewer", "WebView2");
        };
        Web.NavigationStarted += OnNavigationStarted;
        Web.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            if (e.Request is { Scheme: "http" or "https" or "mailto" } uri)
                _ = Launcher.LaunchUriAsync(uri);
        };
        Web.NavigationCompleted += async (_, e) =>
        {
            if (!e.IsSuccess)
                return;
            await Web.InvokeScript(PageScript);
            scripted = true;
            await ApplyZoom();
            if (position != null)
                await Web.InvokeScript($"freeSlateViewer.restore({position})");
            position = null;
            await ApplyComparison();
        };
        Web.WebMessageReceived += (_, e) => Dispatcher.UIThread.Post(() => OnPageMessage(e.Body));

        // Files dropped on the window outside the web view (the web view gets its own, see OnNavigationStarted).
        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, (_, e) => e.DragEffects = Dropped(e) != null ? DragDropEffects.Copy : DragDropEffects.None);
        DragDrop.AddDropHandler(this, async (_, e) =>
        {
            if (Dropped(e) is { } file)
                await LoadAsync(file);
        });
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
        reloadTimer.Tick += async (_, _) =>
        {
            reloadTimer.Stop();
            await ReloadAsync();
        };

        ZoomBox.SelectedIndex = Math.Clamp(settings.Zoom, 0, Zooms.Length - 1);
        CompareBox.SelectedIndex = wordInstalled ? Math.Clamp(settings.Compare, 0, Comparisons.Length - 1) : 0;
        if (!wordInstalled)
        {
            CompareBox.IsEnabled = false;
            ToolTip.SetTip(CompareBox, "Der Vergleich braucht ein installiertes Word (Windows).");
        }
        BlendSlider.Value = Math.Clamp(settings.Blend, 0, 100);
        MarkupToggle.IsChecked = settings.Markup;
        BrowserToggle.IsChecked = settings.Browser;
        WatchBox.IsChecked = settings.Watch;
        ScenePages.PageChanged += n =>
        {
            if (!PageBox.IsFocused)
                PageBox.Text = n.ToString(CultureInfo.CurrentCulture);
        };
        ScenePages.LinkClicked += Follow;
        ScenePages.Edited += milliseconds =>
        {
            ShowChanged();
            if (view != null)
                StatusText.Text = $"{Path.GetFileName(view.Path)} · {(view.Slides ? Slides(ScenePages.Scenes.Count) : Pages(ScenePages.Scenes.Count))} · bearbeitet in {milliseconds.ToString("0", CultureInfo.CurrentCulture)} ms";
            pageCount = ScenePages.Scenes.Count;
            PageCount.Text = $"von {pageCount}";
        };
        ScenePages.Drawn += milliseconds =>
        {
            if (view != null && StatusText.Text?.Contains(" · bearbeitet in ", StringComparison.Ordinal) == true)
                StatusText.Text += $", Bild nach {milliseconds.ToString("0", CultureInfo.CurrentCulture)} ms";
        };
        ScenePages.Refused += reason => StatusText.Text = $"Nicht möglich: {reason}";
        ScenePages.PlaceChanged += place => PlaceText.Text = place is var (block, offset, selected)
            ? $"Absatz {block.ToString(CultureInfo.CurrentCulture)}, Stelle {offset.ToString(CultureInfo.CurrentCulture)}"
              + (selected > 0 ? $" · {selected.ToString(CultureInfo.CurrentCulture)} Zeichen markiert" : "")
            : "";

        Closing += (_, _) => settings.Save();
        Closed += (_, _) =>
        {
            watcher?.Dispose();
            document?.Dispose();
            server.Dispose();
            if (OperatingSystem.IsWindows())
                WordPages.Abort();
            try { Directory.Delete(work, recursive: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        };
    }

    static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    static bool IsWordFile(string path) => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    static bool IsPresentation(string path) => SlideExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    static string? Dropped(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()).FirstOrDefault(p => p != null && (IsWordFile(p) || IsPresentation(p)));

    static string Pages(int n) => n == 1 ? "1 Seite" : $"{n} Seiten";

    static string Slides(int n) => n == 1 ? "1 Folie" : $"{n} Folien";

    /// <summary>
    /// The web view goes nowhere but to this viewer's documents: a Word file dropped on it is opened, links to the web
    /// open in the browser.
    /// </summary>
    void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        if (e.Request is not { } uri || server.Serves(uri) || uri.Scheme is "about" or "data")
            return;
        e.Cancel = true;
        if (uri.IsFile && (IsWordFile(uri.LocalPath) || IsPresentation(uri.LocalPath)))
        {
            var file = uri.LocalPath;
            Dispatcher.UIThread.Post(async () => await LoadAsync(file));
        }
        else if (uri.Scheme is "http" or "https" or "mailto")
            _ = Launcher.LaunchUriAsync(uri);
    }

    async void OnKey(object? sender, KeyEventArgs e)
    {
        var control = e.KeyModifiers == KeyModifiers.Control;
        if (e.Key == Key.F5 || (control && e.Key == Key.R))
        {
            e.Handled = true;
            await ReloadAsync();
        }
        else if (control && e.Key == Key.O)
        {
            e.Handled = true;
            await OpenAsync();
        }
        else if (control && e.Key == Key.P && PrintButton.IsEnabled)
        {
            e.Handled = true;
            await PrintAsync();
        }
        else if (control && e.Key == Key.S && session != null)
        {
            e.Handled = true;
            SaveDocument();
        }
    }

    void OnStoreClick(object? sender, RoutedEventArgs e) => SaveDocument();

    /// <summary>Saves the document changed here over its file (a password goes; a copy read in its place saves to the document itself).</summary>
    void SaveDocument()
    {
        if (session == null || view == null)
            return;
        try
        {
            savedAt = DateTime.UtcNow;
            session.Save(view.Path);
            savedAt = DateTime.UtcNow;
            ShowChanged();
            StatusText.Text = $"{Path.GetFileName(view.Path)} gespeichert";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"{Path.GetFileName(view.Path)} lässt sich nicht speichern: {e.Message}";
        }
    }

    /// <summary>Whether the document has changes not saved: in the title and on the save button.</summary>
    void ShowChanged()
    {
        if (view == null)
            return;
        StoreButton.IsEnabled = session?.Changed == true;
        Title = $"{Path.GetFileName(view.Path)}{(session?.Changed == true ? " •" : "")} – FreeSlate Viewer";
    }

    /// <summary>Lets go of the document open to change it.</summary>
    void CloseDocument()
    {
        ScenePages.Session = null;
        session = null;
        document?.Dispose();
        document = null;
        StoreButton.IsEnabled = false;
    }

    void OnPageMessage(string? message)
    {
        switch (message?.Split(' ', 2))
        {
            case ["pages", var n]:
                pageCount = int.Parse(n, CultureInfo.InvariantCulture);
                PageCount.Text = $"von {pageCount}";
                if (view != null)
                    StatusText.Text = $"{Path.GetFileName(view.Path)} · {Pages(pageCount)} · in {seconds.ToString("0.00", CultureInfo.CurrentCulture)} s umgesetzt";
                ShowWordState();
                break;
            case ["page", var n]:
                if (!PageBox.IsFocused)
                    PageBox.Text = n;
                break;
            case ["reload"]:
                _ = ReloadAsync();
                break;
            case ["open"]:
                _ = OpenAsync();
                break;
        }
    }

    async void OnOpenClick(object? sender, RoutedEventArgs e) => await OpenAsync();

    async Task OpenAsync()
    {
        var start = settings.Folder is { } folder && Directory.Exists(folder) ? await StorageProvider.TryGetFolderFromPathAsync(folder) : null;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Dokument öffnen",
            AllowMultiple = false,
            SuggestedStartLocation = start,
            FileTypeFilter =
            [
                new FilePickerFileType("Word-Dokumente und PowerPoint-Präsentationen") { Patterns = Extensions.Concat(SlideExtensions).Select(x => "*" + x).ToArray() },
                new FilePickerFileType("Word-Dokumente") { Patterns = Extensions.Select(x => "*" + x).ToArray() },
                new FilePickerFileType("PowerPoint-Präsentationen") { Patterns = SlideExtensions.Select(x => "*" + x).ToArray() },
            ],
        });
        if (files is [var file] && file.TryGetLocalPath() is { } path)
            await LoadAsync(path);
    }

    async void OnReloadClick(object? sender, RoutedEventArgs e) => await ReloadAsync();

    async void OnUnlockClick(object? sender, RoutedEventArgs e)
    {
        if (locked != null)
            await LoadAsync(locked, PasswordBox.Text);
    }

    async void OnMarkupChanged(object? sender, RoutedEventArgs e)
    {
        settings.Markup = MarkupToggle.IsChecked == true;
        await ReloadAsync();
    }

    async void OnBrowserChanged(object? sender, RoutedEventArgs e)
    {
        settings.Browser = BrowserToggle.IsChecked == true;
        await ReloadAsync();
    }

    /// <summary>A link of the pages Skia draws: a bookmark of the document scrolled to, a web address opened in the browser.</summary>
    void Follow(string target)
    {
        if (target.StartsWith('#'))
        {
            var name = target[1..];
            if (view?.Scenes is { } scenes && scenes.Select((scene, i) => (Page: i + 1, Mark: Bookmark(scene.Items, name))).FirstOrDefault(m => m.Mark != null) is { Mark: { } mark } found)
                ScenePages.GoTo(found.Page, mark.Y);
            return;
        }
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto")
            _ = Launcher.LaunchUriAsync(uri);
    }

    /// <summary>A bookmark of a page by its name, among its items and those they hold.</summary>
    static BookmarkItem? Bookmark(IEnumerable<SceneItem> items, string name)
    {
        foreach (var item in items)
        {
            if (item is BookmarkItem bookmark && bookmark.Name == name)
                return bookmark;
            foreach (var property in item.GetType().GetProperties().Where(p => p.PropertyType == typeof(List<SceneItem>)))
            {
                if (property.GetValue(item) is List<SceneItem> inner && Bookmark(inner, name) is { } found)
                    return found;
            }
        }
        return null;
    }

    async void OnZoomChanged(object? sender, SelectionChangedEventArgs e)
    {
        settings.Zoom = Math.Max(0, ZoomBox.SelectedIndex);
        await ApplyZoom();
    }

    async void OnCompareChanged(object? sender, SelectionChangedEventArgs e)
    {
        settings.Compare = Math.Max(0, CompareBox.SelectedIndex);
        BlendSlider.IsVisible = Comparisons[settings.Compare] == "blend";
        await ApplyComparison();
    }

    async void OnBlendChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        settings.Blend = BlendSlider.Value;
        ScenePages.Blend = BlendSlider.Value / 100;
        if (scripted)
            await Web.InvokeScript($"freeSlateViewer.blend({(BlendSlider.Value / 100).ToString(CultureInfo.InvariantCulture)})");
    }

    void OnWatchChanged(object? sender, RoutedEventArgs e)
    {
        settings.Watch = WatchBox.IsChecked == true;
        Watch(view?.Path);
    }

    async void OnPrintClick(object? sender, RoutedEventArgs e) => await PrintAsync();

    /// <summary>Prints the document: the web view's print dialog, or the pages Skia draws as a PDF in the PDF program.</summary>
    async Task PrintAsync()
    {
        if (view?.Scenes == null)
        {
            Web.ShowPrintUI();
            return;
        }
        Directory.CreateDirectory(work);
        var pdf = Path.Combine(work, Path.GetFileNameWithoutExtension(view.Path) + ".pdf");
        try
        {
            await WritePdfAsync(view, pdf);
            await Launcher.LaunchFileInfoAsync(new FileInfo(pdf));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"Das PDF lässt sich nicht schreiben: {error.Message}";
        }
    }

    /// <summary>
    /// The document's pages as Skia draws them, written as a PDF with the document's information (see PdfExport), for
    /// archiving as PDF/A-2b where asked.
    /// </summary>
    static Task WritePdfAsync(View shown, string target, bool archive = false) => Task.Run(() =>
    {
        using var file = File.Create(target);
        if (shown.Scenes is { } scenes)
            SkiaScene.Pdf(scenes, file, metadata: PdfExport.Metadata(shown.Source, shown.Password, archive));
        else
            PdfExport.FromDocx(shown.Source, file, password: shown.Password, markup: shown.Markup, archive: archive);
    });

    static readonly FilePickerFileType PdfType = new("PDF-Dokument") { Patterns = ["*.pdf"] };

    static readonly FilePickerFileType ArchiveType = new("PDF/A-Dokument zur Archivierung") { Patterns = ["*.pdf"] };

    async void OnPdfClick(object? sender, RoutedEventArgs e)
    {
        if (view == null)
            return;
        var chosen = await StorageProvider.SaveFilePickerWithResultAsync(new FilePickerSaveOptions
        {
            Title = "Als PDF speichern",
            SuggestedFileName = Path.GetFileNameWithoutExtension(view.Path) + ".pdf",
            DefaultExtension = "pdf",
            FileTypeChoices = [PdfType, ArchiveType],
        });
        if (chosen.File?.TryGetLocalPath() is not { } target)
            return;
        try
        {
            StatusText.Text = $"{Path.GetFileName(target)} wird geschrieben …";
            await WritePdfAsync(view, target, chosen.SelectedFileType?.Name == ArchiveType.Name);
            StatusText.Text = $"Gespeichert: {target}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"{Path.GetFileName(target)} lässt sich nicht speichern: {error.Message}";
        }
    }

    async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (view == null)
            return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Als HTML speichern",
            SuggestedFileName = Path.GetFileNameWithoutExtension(view.Path) + ".html",
            DefaultExtension = "html",
            FileTypeChoices = [new FilePickerFileType("HTML-Dokument") { Patterns = ["*.html"] }],
        });
        if (file?.TryGetLocalPath() is not { } target)
            return;
        try
        {
            var shown = view;
            var html = shown.Html ?? await Task.Run(() => shown.Slides
                ? PptxConverter.ToHtml(shown.Source, password: shown.Password)
                : DocxConverter.ToHtml(shown.Source, password: shown.Password, markup: shown.Markup));
            await File.WriteAllTextAsync(target, html);
            StatusText.Text = $"Gespeichert: {target}";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"{Path.GetFileName(target)} lässt sich nicht speichern: {error.Message}";
        }
    }

    async void OnWordPdfClick(object? sender, RoutedEventArgs e)
    {
        if (view != null && renderings.TryGetValue(view.Key, out var word))
            await Launcher.LaunchFileInfoAsync(new FileInfo(word.Pdf));
    }

    async void OnPageKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !scripted && view?.Scenes == null)
            return;
        e.Handled = true;
        if (view?.Scenes != null)
        {
            if (int.TryParse(PageBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var page))
                ScenePages.GoTo(page);
            Dispatcher.UIThread.Post(() => SkiaScroll.Focus(), DispatcherPriority.Background);
            return;
        }
        if (int.TryParse(PageBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var n))
            await Web.InvokeScript($"freeSlateViewer.goTo({n})");
        Dispatcher.UIThread.Post(() => Web.Focus(), DispatcherPriority.Background);
    }

    async Task ReloadAsync()
    {
        if (view != null)
            await LoadAsync(view.Path, view.Password, again: true);
        else if (failed != null)
            await LoadAsync(failed);
    }

    /// <summary>Converts the document with FreeSlate and shows it; loaded again, at the place the window was.</summary>
    public async Task LoadAsync(string path, string? password = null, bool again = false)
    {
        path = Path.GetFullPath(path);
        var name = Path.GetFileName(path);
        StatusText.Text = again ? $"{name} wird neu geladen …" : $"{name} wird geladen …";
        // The page script answers with the position as JSON, which goes back into it as it is.
        position = again && scripted ? await Web.InvokeScript("freeSlateViewer.position()") : null;
        if (string.IsNullOrWhiteSpace(position) || position == "null")
            position = null;
        var drawnAt = again && view?.Scenes != null ? ScenePages.Position() : default;
        OpenButton.IsEnabled = ReloadButton.IsEnabled = false;
        var markup = MarkupToggle.IsChecked == true;
        try
        {
            var clock = Stopwatch.StartNew();
            var slides = IsPresentation(path);
            var (source, key) = await Task.Run(() => Readable(path, markup));
            if (BrowserToggle.IsChecked != true || slides)
            {
                // The pages drawn by Skia from their scenes, no web view, with the places of their text; the document
                // stays open to change it, typing going to an editing session over it.
                var (opened, scenes) = await Task.Run(() =>
                {
                    IEditableDocument editable = slides ? PresentationDocument.Open(source, password) : WordDocument.Open(source, password, markup: markup);
                    return (editable, editable.Scenes().ToList());
                });
                CloseDocument();
                this.document = opened;
                session = new EditSession(opened);
                ScenePages.Session = session;
                seconds = clock.Elapsed.TotalSeconds;
                view = new View(path, source, password, markup, null, scenes, key, slides);
                locked = failed = null;
                retries = 0;
                scripted = false;
                PasswordPanel.IsVisible = Placeholder.IsVisible = ErrorPanel.IsVisible = Web.IsVisible = false;
                SkiaScroll.IsVisible = true;
                ScenePages.Scenes = scenes;
                ScenePages.Blend = BlendSlider.Value / 100;
                await ApplyZoom();
                Dispatcher.UIThread.Post(() => ScenePages.Restore(drawnAt), DispatcherPriority.Loaded);
                pageCount = scenes.Count;
                PageCount.Text = $"von {pageCount}";
                ShowChanged();
                StatusText.Text = $"{name} · {(slides ? Slides(pageCount) : Pages(pageCount))} · in {seconds.ToString("0.00", CultureInfo.CurrentCulture)} s umgesetzt (Skia)";
                PrintButton.IsEnabled = SaveButton.IsEnabled = PdfButton.IsEnabled = true;
                settings.Folder = Path.GetDirectoryName(path);
                Watch(path);
                await ApplyComparison();
                return;
            }
            var html = await Task.Run(() => DocxConverter.ToHtml(source, password: password, markup: markup));
            CloseDocument();
            seconds = clock.Elapsed.TotalSeconds;
            // A file, not NavigateToString: the document carries its pictures and fonts and is often large.
            Directory.CreateDirectory(work);
            var document = Path.Combine(work, Path.GetFileNameWithoutExtension(path) + ".html");
            await File.WriteAllTextAsync(document, html);
            view = new View(path, source, password, markup, html, null, key);
            locked = failed = null;
            retries = 0;
            scripted = false;
            pageCount = 0;
            PasswordPanel.IsVisible = Placeholder.IsVisible = ErrorPanel.IsVisible = SkiaScroll.IsVisible = false;
            ScenePages.Scenes = [];
            Web.IsVisible = true;
            Web.Navigate(server.Publish(document));
            Title = $"{name} – FreeSlate Viewer";
            StatusText.Text = name;
            PrintButton.IsEnabled = SaveButton.IsEnabled = PdfButton.IsEnabled = true;
            settings.Folder = Path.GetDirectoryName(path);
            Watch(path);
        }
        catch (PasswordException error)
        {
            locked = path;
            PasswordBox.Text = "";
            PasswordPanel.IsVisible = true;
            PasswordBox.Focus();
            StatusText.Text = error.WrongPassword ? "Das Passwort stimmt nicht." : $"{name} ist passwortgeschützt.";
        }
        catch (IOException) when (again && retries++ < 5)
        {
            // Still being written: try again in a moment.
            StatusText.Text = $"{name} wird gerade gespeichert …";
            reloadTimer.Start();
        }
        catch (Exception error)
        {
            Fail(path, error);
        }
        finally
        {
            OpenButton.IsEnabled = true;
            ReloadButton.IsEnabled = view != null || failed != null;
        }
    }

    /// <summary>
    /// The file to read and the document's version: the document itself, or a copy when another program (Word) holds
    /// it open for writing; the copy keeps the name, which fields show.
    /// </summary>
    (string Source, string Key) Readable(string path, bool markup)
    {
        var info = new FileInfo(path);
        var key = string.Create(CultureInfo.InvariantCulture, $"{path}|{info.LastWriteTimeUtc.Ticks}|{info.Length}|{markup}");
        try
        {
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) { }
            return (path, key);
        }
        catch (IOException) when (File.Exists(path))
        {
            var folder = Path.Combine(work, "copy-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(folder);
            var copy = Path.Combine(folder, Path.GetFileName(path));
            using (var from = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var to = File.Create(copy))
                from.CopyTo(to);
            return (copy, key);
        }
    }

    /// <summary>Shows why a document could not be shown, with the whole exception for a bug report.</summary>
    void Fail(string path, Exception error)
    {
        var name = Path.GetFileName(path);
        CloseDocument();
        view = null;
        locked = null;
        failed = path;
        scripted = false;
        Web.IsVisible = SkiaScroll.IsVisible = Placeholder.IsVisible = PasswordPanel.IsVisible = false;
        ScenePages.Scenes = [];
        PrintButton.IsEnabled = SaveButton.IsEnabled = PdfButton.IsEnabled = WordPdfButton.IsVisible = false;
        ErrorText.Text = error.ToString();
        ErrorPanel.IsVisible = true;
        StatusText.Text = $"{name} lässt sich nicht öffnen: {error.Message}";
        WordText.Text = PageBox.Text = "";
        PageCount.Text = "von 0";
        Title = $"{name} – FreeSlate Viewer";
    }

    /// <summary>Loads the document again when it is saved (Word saves into a new file that takes the document's name).</summary>
    void Watch(string? path)
    {
        var wanted = WatchBox.IsChecked == true ? path : null;
        if (watcher != null && wanted != null && string.Equals(Path.Combine(watcher.Path, watcher.Filter), wanted, StringComparison.OrdinalIgnoreCase))
            return;
        watcher?.Dispose();
        watcher = null;
        if (wanted == null || Path.GetDirectoryName(wanted) is not { } folder)
            return;
        watcher = new FileSystemWatcher(folder, Path.GetFileName(wanted))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };
        void Changed() => Dispatcher.UIThread.Post(() =>
        {
            // Saved from here: nothing to load. Changes not saved here are not given up for another program's.
            if (DateTime.UtcNow - savedAt < TimeSpan.FromSeconds(3))
                return;
            if (session?.Changed == true)
            {
                StatusText.Text = $"{Path.GetFileName(wanted)} wurde geändert – nicht neu geladen, weil die Änderungen hier nicht gespeichert sind";
                return;
            }
            retries = 0;
            reloadTimer.Stop();
            reloadTimer.Start();
        });
        watcher.Changed += (_, _) => Changed();
        watcher.Created += (_, _) => Changed();
        watcher.Renamed += (_, e) =>
        {
            if (string.Equals(e.FullPath, wanted, StringComparison.OrdinalIgnoreCase))
                Changed();
        };
        watcher.EnableRaisingEvents = true;
    }

    async Task ApplyZoom()
    {
        if (view?.Scenes != null)
        {
            // The page script's zoom for the pages Skia draws: a fit by name or a factor.
            var zoom = Zooms[Math.Max(0, ZoomBox.SelectedIndex)];
            ScenePages.Zoom = double.TryParse(zoom, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor) ? factor : zoom.Trim('\'');
        }
        else if (scripted)
            await Web.InvokeScript($"freeSlateViewer.zoom({Zooms[Math.Max(0, ZoomBox.SelectedIndex)]})");
    }

    /// <summary>Shows Word's pages as the compare box says, Word rendering them first when they are not there yet.</summary>
    async Task ApplyComparison()
    {
        if (!scripted && view?.Scenes == null || view == null)
            return;
        var mode = Comparisons[Math.Max(0, CompareBox.SelectedIndex)];
        if (mode == "off" || !wordInstalled || view.Slides)
        {
            if (view.Scenes != null)
                ScenePages.Compare("off", []);
            else
                await Web.InvokeScript("freeSlateViewer.compare('off', [])");
            WordPdfButton.IsVisible = false;
            ShowWordState();
            return;
        }
        if (!renderings.TryGetValue(view.Key, out var word))
        {
            await RenderWordAsync(view);
            return;
        }
        if (view.Scenes != null)
        {
            ScenePages.Compare(mode, word.Pages.Select(p => new WordPicture(Path.Combine(word.Folder, p.File), p.Width, p.Height)).ToList());
            WordPdfButton.IsVisible = true;
            ShowWordState();
            return;
        }
        if (!published.TryGetValue(word, out var root))
            published[word] = root = server.PublishFolder(word.Folder);
        var pages = word.Pages.Select(p => new { src = new Uri(root, Uri.EscapeDataString(p.File)).AbsoluteUri, w = p.Width, h = p.Height });
        await Web.InvokeScript($"freeSlateViewer.blend({(BlendSlider.Value / 100).ToString(CultureInfo.InvariantCulture)})");
        await Web.InvokeScript($"freeSlateViewer.compare({JsonSerializer.Serialize(mode)}, {JsonSerializer.Serialize(pages)})");
        WordPdfButton.IsVisible = true;
        ShowWordState();
    }

    /// <summary>Word renders the document of a view, one document at a time; the result shows if it is still the one shown.</summary>
    async Task RenderWordAsync(View wanted)
    {
        if (!OperatingSystem.IsWindows() || rendering == wanted.Key)
            return;
        await wordGate.WaitAsync();
        try
        {
            if (view?.Key != wanted.Key || renderings.ContainsKey(wanted.Key))
                return;
            rendering = wanted.Key;
            WordProgress.IsVisible = true;
            WordText.Text = "Word druckt das Dokument …";
            WordPdfButton.IsVisible = false;
            var folder = Path.Combine(work, $"word-{++renders}");
            renderings[wanted.Key] = await Task.Run(() => OperatingSystem.IsWindows()
                ? WordPages.Render(wanted.Source, wanted.Password, wanted.Markup, folder, WordScale)
                : throw new PlatformNotSupportedException());
        }
        catch (Exception error)
        {
            if (view?.Key == wanted.Key)
                WordText.Text = $"Word: {error.Message}";
            return;
        }
        finally
        {
            rendering = null;
            WordProgress.IsVisible = false;
            wordGate.Release();
        }
        if (view?.Key == wanted.Key)
            await ApplyComparison();
    }

    void ShowWordState()
    {
        if (rendering != null && rendering == view?.Key)
            return;
        if (view == null || view.Slides || Comparisons[Math.Max(0, CompareBox.SelectedIndex)] == "off" || !renderings.TryGetValue(view.Key, out var word))
        {
            WordText.Text = "";
            return;
        }
        var count = word.Pages.Count;
        WordText.Text = pageCount == 0 || count == pageCount ? $"Word {word.Version}: {Pages(count)}" : $"Word {word.Version}: {Pages(count)} (FreeSlate {pageCount})";
    }
}
