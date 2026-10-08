namespace SpecTools;

/// <summary>Well-known folders of the repository.</summary>
public static class Repo
{
    public static string Root { get; } = FindRoot();

    public static string Spec => Path.Combine(Root, "spec");
    public static string Downloads => Path.Combine(Spec, "downloads");
    public static string Ecma => Path.Combine(Spec, "ecma-376");
    public static string MsXsd => Path.Combine(Spec, "ms-xsd");
    public static string Catalog => Path.Combine(Root, "catalog");

    static string FindRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "tools", "SpecTools", "SpecTools.csproj")))
                    return dir.FullName;
            }
        }
        throw new InvalidOperationException("Repository root not found (looked for tools/SpecTools/SpecTools.csproj).");
    }
}
