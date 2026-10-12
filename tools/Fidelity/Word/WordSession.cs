using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Fidelity.Word;

/// <summary>
/// Late-bound COM automation of an installed Word, without windows. Only a Word this session started (a WINWORD
/// process that was not there before) is ever quit or ended.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WordSession : IDisposable
{
    // WdSaveFormat, WdExportFormat, WdExportOptimizeFor, WdExportRange, WdExportItem, WdExportCreateBookmarks
    const int FormatDocumentDefault = 16;
    const int FormatPdf = 17, OptimizeForPrint = 0, ExportAllDocument = 0, ExportDocumentContent = 0, ExportDocumentWithMarkup = 7, NoBookmarks = 0;
    const int DoNotSaveChanges = 0;

    readonly dynamic app;
    readonly Process? process;
    readonly HashSet<int> excelBefore = Process.GetProcessesByName("EXCEL").Select(p => p.Id).ToHashSet();

    public WordSession()
    {
        var before = Process.GetProcessesByName("WINWORD").Select(p => p.Id).ToHashSet();
        var type = Type.GetTypeFromProgID("Word.Application")
                   ?? throw new InvalidOperationException("Word is not installed.");
        app = Activator.CreateInstance(type)!;
        process = Process.GetProcessesByName("WINWORD").FirstOrDefault(p => !before.Contains(p.Id));
        Version = (string)app.Build;
        // No windows, no alerts, and no macros from the documents it opens.
        const int AlertsNone = 0, MsoAutomationSecurityForceDisable = 3;
        app.Visible = false;
        app.DisplayAlerts = AlertsNone;
        try { app.AutomationSecurity = MsoAutomationSecurityForceDisable; } catch (COMException) { }
    }

    /// <summary>Word's version and build, e.g. "16.0.20430".</summary>
    public string Version { get; }

    /// <summary>
    /// Limit for each step (opening a document, exporting it): Word may wait for a click on a dialog nobody sees. Over
    /// the limit the Word this session started is ended, which fails the step, and the session is <see cref="Broken"/>.
    /// </summary>
    public TimeSpan? StepLimit { get; set; }

    /// <summary>Whether the watchdog ended Word; a broken session is to be replaced by a new one.</summary>
    public bool Broken { get; private set; }

    T Step<T>(Func<T> work)
    {
        if (StepLimit is not { } limit || process == null)
            return work();
        using var watchdog = new Timer(_ =>
        {
            Broken = true;
            try { process.Kill(); } catch (InvalidOperationException) { }
        }, null, limit, Timeout.InfiniteTimeSpan);
        return work();
    }

    /// <summary>Opens a document read-only, a protected one with its password.</summary>
    public dynamic Open(string path, string? password = null) =>
        app.Documents.Open(FileName: Path.GetFullPath(path), ConfirmConversions: false, ReadOnly: true, AddToRecentFiles: false,
            PasswordDocument: password ?? "", Visible: false, NoEncodingDialog: true);

    /// <summary>A new document based on Word's default template.</summary>
    public dynamic Create() => app.Documents.Add();

    /// <summary>The application, for what a document's object model does not reach (options, selection).</summary>
    public dynamic Application => app;

    public static void SaveAndClose(dynamic document, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (File.Exists(path))
            File.Delete(path);
        document.SaveAs2(FileName: Path.GetFullPath(path), FileFormat: FormatDocumentDefault);
        Close(document);
    }

    public static void Close(dynamic document)
    {
        document.Close(DoNotSaveChanges);
        Marshal.FinalReleaseComObject(document);
    }

    /// <summary>
    /// Exports a document to PDF the way Word prints it: the document content without markup, all pages, optimized for
    /// print, missing fonts as bitmaps. Returns the number of pages.
    /// </summary>
    /// <param name="markup">Whether Word prints the document with its markup: all markup, comments and formatting in balloons.</param>
    public int ExportPdf(string documentPath, string pdfPath, string? password = null, bool markup = false)
    {
        var document = Step(() => Open(documentPath, password));
        try
        {
            if (File.Exists(pdfPath))
                File.Delete(pdfPath);
            Step(() =>
            {
                if (markup)
                {
                    var view = document.ActiveWindow.View;
                    view.ShowRevisionsAndComments = true;
                    view.RevisionsView = 0;
                }
                document.ExportAsFixedFormat(OutputFileName: Path.GetFullPath(pdfPath), ExportFormat: FormatPdf, OpenAfterExport: false,
                    OptimizeFor: OptimizeForPrint, Range: ExportAllDocument, Item: markup ? ExportDocumentWithMarkup : ExportDocumentContent, IncludeDocProps: false,
                    KeepIRM: false, CreateBookmarks: NoBookmarks, DocStructureTags: false, BitmapMissingFonts: true, UseISO19005_1: false);
                return 0;
            });
            const int StatisticPages = 2;
            return (int)document.ComputeStatistics(StatisticPages);
        }
        finally
        {
            Close(document);
        }
    }

    public void Dispose()
    {
        try
        {
            if (process != null && !Broken)
                app.Quit(DoNotSaveChanges);
        }
        catch (COMException) when (Broken)
        {
            // Word was ended by the watchdog.
        }
        finally
        {
            Marshal.FinalReleaseComObject(app);
            if (process is { } owned && !owned.WaitForExit(TimeSpan.FromSeconds(10)))
            {
                try { owned.Kill(); } catch (InvalidOperationException) { }
            }
            // Word leaves the Excel instances of chart data sheets running: end the windowless ones started meanwhile.
            foreach (var excel in Process.GetProcessesByName("EXCEL").Where(p => !excelBefore.Contains(p.Id) && p.MainWindowHandle == IntPtr.Zero))
            {
                try { excel.Kill(); } catch (InvalidOperationException) { }
            }
        }
    }
}
