using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fidelity.PowerPoint;
using Fidelity.Word;

namespace Fidelity;

/// <summary>
/// Metadata stored next to the reference renderings of a deck (PowerPoint's slides) or of a Word document (its pages,
/// <see cref="Word"/> and <see cref="Pages"/> set instead of <see cref="PowerPoint"/> and <see cref="Slides"/>). Width and
/// height are those of the first image; the pages of a document may differ in size.
/// </summary>
public sealed record ReferenceInfo(string Source, string Sha256, string? PowerPoint, int Width, int Height, int Slides, string? Word = null, int Pages = 0)
{
    /// <summary>The number of reference images.</summary>
    [JsonIgnore]
    public int Count => Word != null ? Pages : Slides;
}

/// <summary>Renders the reference images of the corpus with PowerPoint, and of the Word documents with Word.</summary>
public static class References
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    public static string Dir(string deck) => Path.Combine(Repo.References, deck);

    /// <summary>The file name of the reference image (and of our screenshot) of slide or page <paramref name="number"/>.</summary>
    public static string ImageName(string deck, int number) => Repo.IsDocument(deck) ? $"page-{number:000}.png" : $"slide-{number:000}.png";

    /// <summary>
    /// Pixels per CSS pixel at which Word documents are rasterized (1 = 96 dpi): Word's PDF and ours both go through
    /// Windows' PDF renderer, so that text drawn at the same positions gives the same pixels.
    /// </summary>
    public const double DocumentScale = 1;

    /// <summary>Word's PDF export of a document, kept for measuring (not committed: it embeds font subsets).</summary>
    public static string DocumentPdf(string deck) => Path.Combine(Repo.Artifacts, deck, "reference.pdf");

    /// <summary>
    /// When Word printed a document's reference (the PDF's /CreationDate, the time on Word's clock), which the
    /// document's date and time fields show there; null without a reference.
    /// </summary>
    public static DateTime? DocumentPrinted(string deck) => PrintedAt(DocumentPdf(deck));

    /// <summary>The /CreationDate of a PDF as the time on its writer's clock, or null.</summary>
    public static DateTime? PrintedAt(string pdf)
    {
        if (!File.Exists(pdf))
            return null;
        var text = System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(pdf));
        var m = System.Text.RegularExpressions.Regex.Match(text, @"/CreationDate\s*\(D:(\d{14})");
        return m.Success && DateTime.TryParseExact(m.Groups[1].Value, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var time) ? time : null;
    }

    public static ReferenceInfo? Load(string deck)
    {
        var file = Path.Combine(Dir(deck), "meta.json");
        return File.Exists(file) ? JsonSerializer.Deserialize<ReferenceInfo>(File.ReadAllText(file), JsonOptions) : null;
    }

    public static string Hash(string file) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));

    /// <summary>Time PowerPoint gets for opening a deck and for exporting each slide (see <see cref="PowerPointSession.StepLimit"/>).</summary>
    static readonly TimeSpan StepLimit = TimeSpan.FromSeconds(90);

    /// <summary>Time Word gets for opening a document and for exporting it to PDF (see <see cref="WordSession.StepLimit"/>).</summary>
    static readonly TimeSpan DocumentStepLimit = TimeSpan.FromSeconds(180);

    [SupportedOSPlatform("windows")]
    public static void Render(IEnumerable<string> decks, bool force)
    {
        PowerPointSession? session = null;
        WordSession? word = null;
        var failed = 0;
        try
        {
            foreach (var deck in decks)
            {
                var source = Repo.DeckFile(deck);
                if (!File.Exists(source))
                {
                    Console.WriteLine($"  missing     {deck}");
                    continue;
                }
                var hash = Hash(source);
                if (!force && Load(deck) is { } existing && existing.Sha256 == hash)
                {
                    Console.WriteLine($"  up to date  {deck}");
                    continue;
                }
                var password = Repo.DeckPassword(deck);
                if (Unreadable(source, password) is { } reason)
                {
                    Console.WriteLine($"  skipped     {deck} ({reason})");
                    continue;
                }

                if (Repo.IsDocument(deck))
                {
                    // Word gives the authors of tracked changes and comments their colors in the order it meets them in
                    // a session, over all its documents: a document shown with its markup gets a Word of its own. So
                    // does a document that names a font this machine lacks, since Word keeps the substitute it chose
                    // for a missing family for the rest of its session (LibreOffice's fdo69649 sets "ITC Officina Sans
                    // Bold" in its w:altName Times New Roman alone, in Calibri after other documents of its folder).
                    var own = Repo.Markup(deck) || NamesMissingFont(source);
                    if (own && word != null)
                    {
                        word.Dispose();
                        word = null;
                    }
                    word ??= new WordSession { StepLimit = DocumentStepLimit };
                    if (!RenderDocument(word, deck, source, hash, password))
                    {
                        failed++;
                        if (word.Broken)
                        {
                            word.Dispose();
                            word = null;
                        }
                    }
                    if (own && word != null)
                    {
                        word.Dispose();
                        word = null;
                    }
                    continue;
                }

                session ??= new PowerPointSession { StepLimit = StepLimit };
                var dir = Dir(deck);
                if (Directory.Exists(dir))
                {
                    foreach (var old in Directory.GetFiles(dir, "slide-*.png"))
                        File.Delete(old);
                }

                try
                {
                    var files = session.ExportSlides(source, dir, password: password);
                    if (files.Count == 0)
                    {
                        Console.WriteLine($"  skipped     {deck} (no slides)");
                        continue;
                    }
                    using var first = SkiaSharp.SKBitmap.Decode(files[0]);
                    var info = new ReferenceInfo(
                        Path.GetRelativePath(Repo.Root, source).Replace('\\', '/'), hash, session.Version, first.Width, first.Height, files.Count);
                    File.WriteAllText(Path.Combine(dir, "meta.json"), JsonSerializer.Serialize(info, JsonOptions) + "\n");
                    Console.WriteLine($"  rendered    {deck} ({files.Count} slides, {first.Width}×{first.Height})");
                }
                catch (Exception e)
                {
                    // A deck PowerPoint cannot open or export (the watchdog may have ended PowerPoint): no reference. COM
                    // failures arrive as whatever exception their HRESULT maps to (a corrupt file's 0x80070570 as
                    // BadImageFormatException), so every exception counts as this deck's failure.
                    failed++;
                    Console.WriteLine($"  failed      {deck}: {e.Message.Split('\n')[0].Trim()}");
                    foreach (var partial in Directory.GetFiles(dir, "slide-*.png").Append(Path.Combine(dir, "meta.json")))
                        File.Delete(partial);
                    if (session.Broken)
                    {
                        session.Dispose();
                        session = null;
                    }
                }
            }
        }
        finally
        {
            session?.Dispose();
            word?.Dispose();
        }
        if (failed > 0)
            Console.WriteLine($"  {failed} decks or documents Office could not render");
    }

    /// <summary>
    /// Word's reference of a document: Word exports it to PDF as it prints it, Windows' PDF renderer rasterizes the
    /// pages at 96 dpi. False when Word could not open or export it.
    /// </summary>
    [SupportedOSPlatform("windows")]
    static bool RenderDocument(WordSession word, string deck, string source, string hash, string? password)
    {
        var dir = Dir(deck);
        if (Directory.Exists(dir))
        {
            foreach (var old in Directory.GetFiles(dir, "page-*.png").Append(Path.Combine(dir, "meta.json")))
                File.Delete(old);
        }
        var pdf = DocumentPdf(deck);
        Directory.CreateDirectory(Path.GetDirectoryName(pdf)!);
        try
        {
            var pages = word.ExportPdf(source, pdf, password, Repo.Markup(deck));
            var files = RasterizeReference(pdf, dir);
            if (files.Count == 0)
            {
                Console.WriteLine($"  skipped     {deck} (no pages)");
                return true;
            }
            using var first = SkiaSharp.SKBitmap.Decode(files[0]);
            var info = new ReferenceInfo(Path.GetRelativePath(Repo.Root, source).Replace('\\', '/'), hash, PowerPoint: null,
                first.Width, first.Height, Slides: 0, Word: word.Version, Pages: files.Count);
            File.WriteAllText(Path.Combine(dir, "meta.json"), JsonSerializer.Serialize(info, JsonOptions) + "\n");
            var note = pages == files.Count ? "" : $", Word counts {pages}";
            Console.WriteLine($"  rendered    {deck} ({files.Count} pages, {first.Width}×{first.Height}{note})");
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"  failed      {deck}: {e.Message.Split('\n')[0].Trim()}");
            if (Directory.Exists(dir))
            {
                foreach (var partial in Directory.GetFiles(dir, "page-*.png").Append(Path.Combine(dir, "meta.json")).Where(File.Exists))
                    File.Delete(partial);
            }
            return false;
        }
    }

    /// <summary>
    /// Rasterizes Word's PDF of a document, its premultiplied pictures first brought to straight alpha in a copy
    /// (<see cref="PdfImages"/>); the PDF itself stays as Word wrote it.
    /// </summary>
    [SupportedOSPlatform("windows")]
    static List<string> RasterizeReference(string pdf, string dir)
    {
        if (PdfImages.Unpremultiplied(pdf) is not { } straight)
            return PdfPages.Rasterize(pdf, dir, DocumentScale);
        var copy = Path.Combine(Path.GetTempPath(), $"fidelity-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(copy, straight);
        try
        {
            return PdfPages.Rasterize(copy, dir, DocumentScale);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    /// <summary>
    /// Why PowerPoint is not given a deck, or null: a damaged package or an encrypted deck without its password would
    /// stop it with a repair or password prompt.
    /// </summary>
    static string? Unreadable(string path, string? password)
    {
        var head = new byte[4];
        using (var stream = File.OpenRead(path))
        {
            if (stream.Read(head, 0, 4) < 4)
                return "too short";
        }
        // Encrypted decks are compound files (D0 CF 11 E0).
        if (head is [0xD0, 0xCF, 0x11, 0xE0])
            return password == null ? "encrypted, no password" : null;
        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(path);
            return zip.GetEntry("[Content_Types].xml") == null ? "no [Content_Types].xml" : null;
        }
        catch (InvalidDataException)
        {
            return "damaged package";
        }
    }

    /// <summary>Whether a Word document's font table names a family this machine has no face of (cloud fonts count as had).</summary>
    static bool NamesMissingFont(string path)
    {
        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(path);
            if (zip.GetEntry("word/fontTable.xml") is not { } entry)
                return false;
            using var stream = entry.Open();
            var w = System.Xml.Linq.XNamespace.Get("http://schemas.openxmlformats.org/wordprocessingml/2006/main");
            return System.Xml.Linq.XDocument.Load(stream).Descendants(w + "font")
                .Select(font => (string?)font.Attribute(w + "name"))
                .Any(name => name is { Length: > 0 } && !FreeSlate.Fonts.FontLocator.Default.Faces(name).Any());
        }
        catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException or IOException)
        {
            return false;
        }
    }
}
