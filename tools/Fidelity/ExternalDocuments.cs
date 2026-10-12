using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fidelity;

/// <summary>
/// The Word test documents of other open-source projects (their importers' and libraries' regression files): real
/// documents from many producers, damaged and fuzzed files among them. `fetch docx` downloads them at pinned commits
/// into <see cref="Repo.ExternalDocuments"/>; they stay out of git (third-party files under their projects' licenses)
/// and are measured against Word like the corpus, as documents named "docx/external/&lt;source&gt;/…".
/// </summary>
public static class ExternalDocuments
{
    /// <summary>
    /// A repository's folder at a pinned commit, searched through (git trees API, so that folders of more than a
    /// thousand files come whole); every document lands in a subfolder named after the folders it was found in.
    /// </summary>
    sealed record Source(string Name, string Repository, string Commit, string License, string[] Folders);

    static readonly Source[] Sources =
    [
        new("libreoffice", "LibreOffice/core", "1746b16a564f59fcaf8c5670bb292748408da54e", "MPL-2.0", ["sw/qa", "oox/qa/unit/data"]),
        new("poi", "apache/poi", "f3688f45b0de8934fb32736ba14efc5bbca4a580", "Apache-2.0", ["test-data/document"]),
        new("tika", "apache/tika", "49e40e6ddaf44910111f6b8c44db1f768d39098b", "Apache-2.0",
            ["tika-parsers/tika-parsers-standard/tika-parsers-standard-modules/tika-parser-microsoft-module/src/test/resources/test-documents"]),
        new("openxml-sdk", "dotnet/Open-XML-SDK", "431ab05cf160248cc3885a4a766026d4f8243792", "MIT", ["test/DocumentFormat.OpenXml.Tests.Assets/assets/TestFiles"]),
        new("python-docx", "python-openxml/python-docx", "e45454602b53e8e572b179ccf1c91093ec9f4ed7", "MIT", ["tests/test_files", "features/steps/test_files"]),
        new("mammoth", "mwilliamson/mammoth.js", "791a189230977143cde64dbf9f1d24dce96ac8a1", "BSD-2-Clause", ["test/test-data"]),
        new("pandoc", "jgm/pandoc", "879e2b86463301604f590a71b9bbd7d981e8489d", "GPL-2.0-or-later", ["test/docx"]),
        new("powertools", "OpenXmlDev/Open-Xml-PowerTools", "536ca1fb4bcdce1f4c920658bd66807b970393d7", "MIT", ["TestFiles"]),
    ];

    sealed record Content(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("sha")] string Sha);

    sealed record TreeEntry(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("size")] long? Size);

    sealed record Tree(
        [property: JsonPropertyName("tree")] List<TreeEntry> Entries,
        [property: JsonPropertyName("truncated")] bool Truncated);

    /// <summary>Word documents only: no macro-enabled formats.</summary>
    static readonly string[] Extensions = [".docx", ".dotx"];

    /// <summary>Downloads the documents that are not there yet and writes each source's origin and license (sources.json).</summary>
    public static void Fetch()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("FreeSlate-Fidelity");
        // An access token raises GitHub's limit of API calls (the downloads themselves are not API calls).
        if (Environment.GetEnvironmentVariable("GITHUB_TOKEN") is { Length: > 0 } token)
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        foreach (var source in Sources)
        {
            var target = Path.Combine(Repo.ExternalDocuments, source.Name);
            var files = new List<object>();
            var downloaded = 0;
            foreach (var folder in source.Folders)
            {
                var (parent, name) = folder.LastIndexOf('/') is var slash and >= 0 ? (folder[..slash], folder[(slash + 1)..]) : ("", folder);
                var listing = http.GetFromJsonAsync<List<Content>>($"https://api.github.com/repos/{source.Repository}/contents/{parent}?ref={source.Commit}")
                    .GetAwaiter().GetResult() ?? [];
                if (listing.FirstOrDefault(e => e.Name == name && e.Type == "dir") is not { } node)
                {
                    Console.WriteLine($"  {source.Name,-12} {folder}: not found");
                    continue;
                }
                var tree = http.GetFromJsonAsync<Tree>($"https://api.github.com/repos/{source.Repository}/git/trees/{node.Sha}?recursive=1")
                    .GetAwaiter().GetResult()!;
                if (tree.Truncated)
                    Console.WriteLine($"  {source.Name,-12} {folder}: listing truncated");
                foreach (var entry in tree.Entries.Where(e => e.Type == "blob" && Extensions.Contains(Path.GetExtension(e.Path), StringComparer.OrdinalIgnoreCase)))
                {
                    // The document's subfolder: the folders it was found in, without "data" and "test_files" levels.
                    var directory = Path.GetDirectoryName(entry.Path)?.Replace('\\', '/') ?? "";
                    var into = string.Join('-', directory.Split('/', StringSplitOptions.RemoveEmptyEntries)
                        .Where(p => p is not ("data" or "test-data" or "test_files" or "TestFiles")));
                    var path = Path.Combine(target, into, Path.GetFileName(entry.Path));
                    if (!File.Exists(path) || new FileInfo(path).Length != entry.Size)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        var url = $"https://raw.githubusercontent.com/{source.Repository}/{source.Commit}/{folder}/{entry.Path}";
                        File.WriteAllBytes(path, http.GetByteArrayAsync(url).GetAwaiter().GetResult());
                        downloaded++;
                    }
                    files.Add(new { file = Path.GetRelativePath(target, path).Replace('\\', '/'), origin = $"{folder}/{entry.Path}" });
                }
            }
            Directory.CreateDirectory(target);
            var record = new { repository = source.Repository, commit = source.Commit, license = source.License, files };
            File.WriteAllText(Path.Combine(target, "sources.json"), JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            Console.WriteLine($"  {source.Name,-12} {files.Count,5} documents ({downloaded} downloaded) from {source.Repository}@{source.Commit[..10]}, {source.License}");
        }
    }
}
