using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FreeSlate.Viewer;

/// <summary>Word's rendering of a document: its PDF and its pages as pictures in <see cref="Folder"/>.</summary>
sealed record WordRendering(string Version, string Pdf, string Folder, IReadOnlyList<WordPage> Pages);

/// <summary>A page picture of Word's and the page's size in CSS pixels (96 dpi).</summary>
sealed record WordPage(string File, double Width, double Height);

/// <summary>
/// Renders a document with an installed Word for comparing (Windows): Word exports it to PDF as it prints it, through
/// late-bound COM and without windows, and Windows' own PDF renderer (Windows.Data.Pdf, reached through Windows
/// PowerShell) rasterizes the pages, as the fidelity tool renders its references. Only a Word this class started is
/// ever quit or ended.
/// </summary>
[SupportedOSPlatform("windows")]
static class WordPages
{
    // WdExportFormat, WdExportOptimizeFor, WdExportRange, WdExportItem, WdExportCreateBookmarks, WdSaveOptions
    const int FormatPdf = 17, OptimizeForPrint = 0, ExportAllDocument = 0, ExportDocumentContent = 0, ExportDocumentWithMarkup = 7, NoBookmarks = 0;
    const int DoNotSaveChanges = 0;

    /// <summary>Time Word gets for opening and exporting a document; it may wait for a click on a dialog nobody sees.</summary>
    static readonly TimeSpan Limit = TimeSpan.FromMinutes(3);

    static Process? running;

    public static bool Installed => Type.GetTypeFromProgID("Word.Application") != null;

    /// <summary>Ends the Word of a rendering in progress (when the viewer closes).</summary>
    public static void Abort()
    {
        try { running?.Kill(); } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
    }

    /// <summary>
    /// Word's PDF of a document (with its markup as Word prints it with markup) and its pages rasterized at
    /// <paramref name="scale"/> pixels per CSS pixel, all in <paramref name="folder"/>.
    /// </summary>
    public static WordRendering Render(string docx, string? password, bool markup, string folder, double scale)
    {
        Directory.CreateDirectory(folder);
        var pdf = Path.Combine(folder, "word.pdf");
        var version = ExportPdf(docx, pdf, password, markup);
        return new WordRendering(version, pdf, folder, Rasterize(pdf, folder, scale));
    }

    static string ExportPdf(string docx, string pdf, string? password, bool markup)
    {
        var before = Process.GetProcessesByName("WINWORD").Select(p => p.Id).ToHashSet();
        var type = Type.GetTypeFromProgID("Word.Application") ?? throw new InvalidOperationException("Word ist nicht installiert.");
        dynamic app = Activator.CreateInstance(type)!;
        var process = Process.GetProcessesByName("WINWORD").FirstOrDefault(p => !before.Contains(p.Id));
        running = process;
        var ended = false;
        using var watchdog = new Timer(_ =>
        {
            ended = true;
            Abort();
        }, null, Limit, Timeout.InfiniteTimeSpan);
        try
        {
            // No windows, no alerts, and no macros from the document; a Word that was running before (someone else's)
            // keeps its settings.
            const int AlertsNone = 0, MsoAutomationSecurityForceDisable = 3;
            if (process != null)
            {
                app.Visible = false;
                app.DisplayAlerts = AlertsNone;
                try { app.AutomationSecurity = MsoAutomationSecurityForceDisable; } catch (COMException) { }
            }
            var version = (string)app.Build;
            var document = app.Documents.Open(FileName: Path.GetFullPath(docx), ConfirmConversions: false, ReadOnly: true, AddToRecentFiles: false,
                PasswordDocument: password ?? "", Visible: false, NoEncodingDialog: true);
            try
            {
                if (markup)
                {
                    var view = document.ActiveWindow.View;
                    view.ShowRevisionsAndComments = true;
                    view.RevisionsView = 0;
                }
                document.ExportAsFixedFormat(OutputFileName: Path.GetFullPath(pdf), ExportFormat: FormatPdf, OpenAfterExport: false,
                    OptimizeFor: OptimizeForPrint, Range: ExportAllDocument, Item: markup ? ExportDocumentWithMarkup : ExportDocumentContent, IncludeDocProps: false,
                    KeepIRM: false, CreateBookmarks: NoBookmarks, DocStructureTags: false, BitmapMissingFonts: true, UseISO19005_1: false);
            }
            finally
            {
                try
                {
                    if (!ended)
                        document.Close(DoNotSaveChanges);
                    Marshal.FinalReleaseComObject(document);
                }
                catch (Exception e) when (e is COMException or InvalidComObjectException) { }
            }
            return version;
        }
        catch (Exception e) when (ended && e is not TimeoutException)
        {
            throw new TimeoutException($"Word hat nach {Limit.TotalMinutes:0} Minuten nicht geantwortet.");
        }
        finally
        {
            try
            {
                // A Word that was running before is someone else's: it keeps running.
                if (process != null && !ended)
                    app.Quit(DoNotSaveChanges);
                Marshal.FinalReleaseComObject(app);
            }
            catch (Exception e) when (e is COMException or InvalidComObjectException) { }
            if (process != null && !process.WaitForExit(TimeSpan.FromSeconds(10)))
                Abort();
            running = null;
        }
    }

    static List<WordPage> Rasterize(string pdf, string folder, double scale)
    {
        var script = Path.Combine(folder, "rasterize-pdf.ps1");
        using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("rasterize-pdf.ps1")!)
        using (var file = File.Create(script))
            resource.CopyTo(file);
        var info = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, "-Pdf", pdf, "-Out", folder,
                     "-Scale", scale.ToString(CultureInfo.InvariantCulture) })
            info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromMinutes(3)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Das Rastern von Words PDF hat zu lange gedauert.");
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Words PDF lässt sich nicht rastern: {error.Result.Trim().Split('\n')[0]}");
        // page-001.png 1588x2246 793.701x1122.52: the file, its pixels, the page's size in DIPs (CSS pixels)
        var pages = new List<WordPage>();
        foreach (var line in output.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(' ');
            if (parts.Length < 3 || parts[2].Split('x') is not [var w, var h])
                continue;
            pages.Add(new WordPage(parts[0], double.Parse(w, CultureInfo.InvariantCulture), double.Parse(h, CultureInfo.InvariantCulture)));
        }
        return pages;
    }
}
