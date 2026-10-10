namespace Fidelity;

/// <summary>Well-known folders of the repository.</summary>
public static class Repo
{
    public static string Root { get; } = FindRoot();

    /// <summary>Test documents; generated decks live in a subfolder "generated".</summary>
    public static string Corpus => Path.Combine(Root, "tests", "corpus", "pptx");

    /// <summary>Reference renderings by PowerPoint, committed so that the comparison also runs without Office.</summary>
    public static string References => Path.Combine(Root, "tests", "fidelity", "reference");

    /// <summary>Our renderings, screenshots and the report (not committed).</summary>
    public static string Artifacts => Path.Combine(Root, "artifacts", "fidelity");

    /// <summary>Test decks of other projects, downloaded by `fetch` (not committed, see <see cref="ExternalDecks"/>).</summary>
    public static string External => Path.Combine(Root, "spec", "downloads", "testdecks");

    /// <summary>PowerPoint's Open XML formats: presentation, macro-enabled presentation, template and show.</summary>
    public static readonly string[] Extensions = [".pptx", ".pptm", ".potx", ".potm", ".ppsx", ".ppsm"];

    const string ExternalPrefix = "external/";

    /// <summary>All decks of the corpus as paths relative to <see cref="Corpus"/> without extension, e.g. "generated/fills".</summary>
    public static List<string> Decks() =>
        Directory.Exists(Corpus)
            ? Directory.GetFiles(Corpus, "*.pptx", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.Ordinal))
                .Select(f => Path.ChangeExtension(Path.GetRelativePath(Corpus, f), null).Replace('\\', '/'))
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

    /// <summary>
    /// The decks the command line names: a deck, or a folder standing for every deck in it ("generated", "external/poi");
    /// without names the corpus (the downloaded decks only when named).
    /// </summary>
    public static List<string> Select(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
            return Decks();
        var all = Decks().Concat(ExternalDecks()).ToList();
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

    public static string DeckFile(string deck)
    {
        if (!deck.StartsWith(ExternalPrefix, StringComparison.Ordinal))
            return Path.Combine(Corpus, deck + ".pptx");
        var stem = Path.Combine(External, deck[ExternalPrefix.Length..]);
        return Extensions.Select(e => stem + e).FirstOrDefault(File.Exists) ?? stem + ".pptx";
    }

    /// <summary>The password of a protected deck: the contents of its .password file next to it, or null.</summary>
    public static string? DeckPassword(string deck) =>
        File.Exists(Path.Combine(Corpus, deck + ".password")) ? File.ReadAllText(Path.Combine(Corpus, deck + ".password")).Trim() : null;

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
