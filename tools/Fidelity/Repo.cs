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

    /// <summary>All decks of the corpus as paths relative to <see cref="Corpus"/> without extension, e.g. "generated/fills".</summary>
    public static List<string> Decks() =>
        Directory.Exists(Corpus)
            ? Directory.GetFiles(Corpus, "*.pptx", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("~$", StringComparison.Ordinal))
                .Select(f => Path.ChangeExtension(Path.GetRelativePath(Corpus, f), null).Replace('\\', '/'))
                .Order(StringComparer.Ordinal)
                .ToList()
            : [];

    public static string DeckFile(string deck) => Path.Combine(Corpus, deck + ".pptx");

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
