using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fidelity;

/// <summary>
/// The test decks of other open-source projects (their importers' and libraries' regression files): real decks from
/// many producers, damaged and fuzzed files among them. `fetch` downloads them at pinned commits into
/// <see cref="Repo.External"/>; they stay out of git (third-party files whose licenses ask for attribution) and are
/// measured against PowerPoint like the corpus, as decks named "external/&lt;source&gt;/…".
/// </summary>
public static class ExternalDecks
{
    /// <summary>A repository's folders at a pinned commit; every folder lands in a subfolder of the source (or in the source itself).</summary>
    sealed record Source(string Name, string Repository, string Commit, string License, (string Folder, string Into)[] Folders);

    static readonly Source[] Sources =
    [
        new("libreoffice", "LibreOffice/core", "1746b16a564f59fcaf8c5670bb292748408da54e", "MPL-2.0",
        [
            ("sd/qa/unit/data/pptx", "sd"), ("sd/qa/unit/data", "sd-data"), ("sd/qa/unit/data/pptm", "sd-pptm"),
            ("sd/qa/unit/data/potx", "sd-potx"), ("sd/qa/unit/data/ppsx", "sd-ppsx"), ("sd/qa/unit/tiledrendering/data", "sd-tiled"),
            ("sd/qa/filter/eppt/data", "sd-eppt"), ("oox/qa/unit/data", "oox"), ("chart2/qa/extras/data/pptx", "chart2"),
            ("svx/qa/unit/data", "svx"),
        ]),
        new("poi", "apache/poi", "ae62bb5116b9aee19ebd5834e3a82066132c9f7f", "Apache-2.0", [("test-data/slideshow", "")]),
        new("tika", "apache/tika", "49e40e6ddaf44910111f6b8c44db1f768d39098b", "Apache-2.0",
            [("tika-parsers/tika-parsers-standard/tika-parsers-standard-modules/tika-parser-microsoft-module/src/test/resources/test-documents", "")]),
        new("openxml-sdk", "dotnet/Open-XML-SDK", "431ab05cf160248cc3885a4a766026d4f8243792", "MIT",
            [("test/DocumentFormat.OpenXml.Tests.Assets/assets/TestFiles", "")]),
        new("python-pptx", "scanny/python-pptx", "278b47b1dedd5b46ee84c286e77cdfb0bf4594be", "MIT", [("tests/test_files", "")]),
    ];

    sealed record Entry(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("download_url")] string? DownloadUrl);

    /// <summary>Downloads the decks that are not there yet and writes each source's origin and license (sources.json).</summary>
    public static void Fetch()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("FreeSlate-Fidelity");
        foreach (var source in Sources)
        {
            var target = Path.Combine(Repo.External, source.Name);
            var files = new List<object>();
            var downloaded = 0;
            foreach (var (folder, into) in source.Folders)
            {
                var listing = http.GetFromJsonAsync<List<Entry>>($"https://api.github.com/repos/{source.Repository}/contents/{folder}?ref={source.Commit}")
                    .GetAwaiter().GetResult() ?? [];
                foreach (var entry in listing.Where(e => e.Type == "file" && e.DownloadUrl != null && Repo.Extensions.Contains(Path.GetExtension(e.Name), StringComparer.OrdinalIgnoreCase)))
                {
                    var path = Path.Combine(target, into, entry.Name);
                    if (!File.Exists(path) || new FileInfo(path).Length != entry.Size)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        File.WriteAllBytes(path, http.GetByteArrayAsync(entry.DownloadUrl).GetAwaiter().GetResult());
                        downloaded++;
                    }
                    files.Add(new { file = Path.GetRelativePath(target, path).Replace('\\', '/'), origin = $"{folder}/{entry.Name}" });
                }
            }
            Directory.CreateDirectory(target);
            var record = new { repository = source.Repository, commit = source.Commit, license = source.License, files };
            File.WriteAllText(Path.Combine(target, "sources.json"), JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            Console.WriteLine($"  {source.Name,-12} {files.Count,4} decks ({downloaded} downloaded) from {source.Repository}@{source.Commit[..10]}, {source.License}");
        }
    }
}
