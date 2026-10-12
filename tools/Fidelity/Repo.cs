namespace Fidelity;

/// <summary>Well-known folders of the repository.</summary>
public static class Repo
{
    public static string Root { get; } = FindRoot();

    /// <summary>Test decks; generated decks live in a subfolder "generated".</summary>
    public static string Corpus => Path.Combine(Root, "tests", "corpus", "pptx");

    /// <summary>Test documents of Word, named "docx/…" (<see cref="DocumentPrefix"/>); generated ones in "generated".</summary>
    public static string DocumentCorpus => Path.Combine(Root, "tests", "corpus", "docx");

    /// <summary>Reference renderings by PowerPoint and Word, committed so that the comparison also runs without Office.</summary>
    public static string References => Path.Combine(Root, "tests", "fidelity", "reference");

    /// <summary>Our renderings, screenshots and the report (not committed).</summary>
    public static string Artifacts => Path.Combine(Root, "artifacts", "fidelity");

    /// <summary>The system's regional format, in which PowerPoint shows chart numbers and the references show them.</summary>
    public static System.Globalization.CultureInfo Regional { get; set; } = System.Globalization.CultureInfo.CurrentCulture;

    /// <summary>Test decks of other projects, downloaded by `fetch` (not committed, see <see cref="ExternalDecks"/>).</summary>
    public static string External => Path.Combine(Root, "spec", "downloads", "testdecks");

    /// <summary>Word documents of other projects, downloaded by `fetch docx` (not committed, see <see cref="Fidelity.ExternalDocuments"/>).</summary>
    public static string ExternalDocuments => Path.Combine(Root, "spec", "downloads", "testdocs");

    /// <summary>PowerPoint's Open XML formats: presentation, macro-enabled presentation, template and show.</summary>
    public static readonly string[] Extensions = [".pptx", ".pptm", ".potx", ".potm", ".ppsx", ".ppsm"];

    /// <summary>Word's Open XML formats: document, macro-enabled document and their templates.</summary>
    public static readonly string[] DocumentExtensions = [".docx", ".docm", ".dotx", ".dotm"];

    const string ExternalPrefix = "external/";

    /// <summary>Prefix of the names of other projects' Word documents, e.g. "docx/external/poi/sample".</summary>
    const string ExternalDocumentPrefix = "docx/external/";

    /// <summary>Prefix of the names of Word documents in <see cref="DocumentCorpus"/>, e.g. "docx/generated/paragraphs".</summary>
    public const string DocumentPrefix = "docx/";

    /// <summary>Whether a deck name stands for a Word document (pages) rather than a presentation (slides).</summary>
    public static bool IsDocument(string deck) =>
        deck.StartsWith(DocumentPrefix, StringComparison.Ordinal) ||
        DocumentExtensions.Contains(Path.GetExtension(DeckFile(deck)), StringComparer.OrdinalIgnoreCase);

    /// <summary>All decks of the corpus as paths relative to <see cref="Corpus"/> without extension, e.g. "generated/fills".</summary>
    public static List<string> Decks() =>
        Directory.Exists(Corpus)
            ? Directory.GetFiles(Corpus, "*.pptx", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.Ordinal))
                .Select(f => Path.ChangeExtension(Path.GetRelativePath(Corpus, f), null).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];

    /// <summary>All Word documents of the corpus, named "docx/…" without extension, e.g. "docx/generated/paragraphs".</summary>
    public static List<string> Documents() =>
        Directory.Exists(DocumentCorpus)
            ? Directory.GetFiles(DocumentCorpus, "*", SearchOption.AllDirectories)
                .Where(f => DocumentExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase)
                            && !Path.GetFileName(f).StartsWith("~$", StringComparison.Ordinal))
                .Select(f => DocumentPrefix + Path.ChangeExtension(Path.GetRelativePath(DocumentCorpus, f), null).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];

    /// <summary>The downloaded decks of other projects, named "external/&lt;source&gt;/…" without extension.</summary>
    public static List<string> ExternalDecks() =>
        Directory.Exists(External)
            ? Directory.GetFiles(External, "*", SearchOption.AllDirectories)
                .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Select(f => ExternalPrefix + Path.ChangeExtension(Path.GetRelativePath(External, f), null).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];

    /// <summary>The downloaded Word documents of other projects, named "docx/external/&lt;source&gt;/…" without extension.</summary>
    public static List<string> ExternalDocumentNames() =>
        Directory.Exists(ExternalDocuments)
            ? Directory.GetFiles(ExternalDocuments, "*", SearchOption.AllDirectories)
                .Where(f => DocumentExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Select(f => ExternalDocumentPrefix + Path.ChangeExtension(Path.GetRelativePath(ExternalDocuments, f), null).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];

    /// <summary>
    /// The decks the command line names: a deck, or a folder standing for every deck in it ("generated", "external/poi",
    /// "docx"); without names the presentations of the corpus (Word documents and downloaded decks only when named;
    /// other projects' Word documents only as "docx/external…").
    /// </summary>
    public static List<string> Select(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
            return Decks();
        var all = Decks().Concat(Documents()).Concat(ExternalDecks()).ToList();
        if (names.Any(n => n.StartsWith(ExternalDocumentPrefix.TrimEnd('/'), StringComparison.Ordinal)))
            all.AddRange(ExternalDocumentNames());
        var result = new List<string>();
        foreach (var name in names)
        {
            var folder = name.TrimEnd('/') + "/";
            var inFolder = all.Where(d => d.StartsWith(folder, StringComparison.Ordinal)).ToList();
            // A name that is no deck and no folder stays, so that commands report it.
            result.AddRange(all.Contains(name) || inFolder.Count == 0 ? [name] : inFolder);
        }
        return result.Distinct().ToList();
    }

    /// <summary>Whether a document is shown with its markup (comments, tracked changes): documents named markup-*.</summary>
    public static bool Markup(string deck) => deck.StartsWith(DocumentPrefix, StringComparison.Ordinal) && Path.GetFileName(deck).StartsWith("markup-", StringComparison.Ordinal);

    public static string DeckFile(string deck)
    {
        if (deck.StartsWith(ExternalDocumentPrefix, StringComparison.Ordinal))
        {
            var external = Path.Combine(ExternalDocuments, deck[ExternalDocumentPrefix.Length..]);
            return DocumentExtensions.Select(e => external + e).FirstOrDefault(File.Exists) ?? external + ".docx";
        }
        if (deck.StartsWith(DocumentPrefix, StringComparison.Ordinal))
        {
            var document = Path.Combine(DocumentCorpus, deck[DocumentPrefix.Length..]);
            return DocumentExtensions.Select(e => document + e).FirstOrDefault(File.Exists) ?? document + ".docx";
        }
        if (!deck.StartsWith(ExternalPrefix, StringComparison.Ordinal))
            return Path.Combine(Corpus, deck + ".pptx");
        var stem = Path.Combine(External, deck[ExternalPrefix.Length..]);
        return Extensions.Concat(DocumentExtensions).Select(e => stem + e).FirstOrDefault(File.Exists) ?? stem + ".pptx";
    }

    /// <summary>The password of a protected deck or document: the contents of its .password file next to it, or null.</summary>
    public static string? DeckPassword(string deck)
    {
        var file = Path.ChangeExtension(DeckFile(deck), ".password");
        return File.Exists(file) ? File.ReadAllText(file).Trim() : null;
    }

    static string FindRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "tools", "Fidelity", "Fidelity.csproj")))
                    return dir.FullName;
            }
        }
        throw new InvalidOperationException("Repository root not found (looked for tools/Fidelity/Fidelity.csproj).");
    }
}
