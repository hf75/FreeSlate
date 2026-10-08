using System.IO.Compression;

namespace SpecTools;

/// <summary>Downloads the specification sources and unpacks the ECMA-376 archives.</summary>
public static class Fetch
{
    /// <summary>Nested archives inside the ECMA-376 parts and the folder each one is unpacked to.</summary>
    static readonly Dictionary<string, string> NestedArchives = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OfficeOpenXML-DrawingMLGeometries.zip"] = "geometries",
        ["OfficeOpenXML-XMLSchema-Strict.zip"] = "xsd-strict",
        ["OfficeOpenXML-RELAXNG-Strict.zip"] = "rnc-strict",
        ["OfficeOpenXML-SpreadsheetMLStyles.zip"] = "spreadsheet-styles",
        ["OfficeOpenXML-WordprocessingMLArtBorders.zip"] = "art-borders",
        ["OpenPackagingConventions-XMLSchema.zip"] = "xsd",
        ["OpenPackagingConventions-RELAXNG.zip"] = "rnc",
        ["OfficeOpenXML-XMLSchema-Transitional.zip"] = "xsd-transitional",
        ["OfficeOpenXML-RELAXNG-Transitional.zip"] = "rnc-transitional",
    };

    public static async Task RunAsync()
    {
        Directory.CreateDirectory(Repo.Downloads);
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; SpecTools)");

        foreach (var download in Sources.Downloads)
        {
            var target = Path.Combine(Repo.Downloads, download.FileName);
            if (File.Exists(target))
            {
                Console.WriteLine($"  present     {download.FileName}");
                continue;
            }

            Console.WriteLine($"  downloading {download.FileName}");
            var temp = target + ".part";
            await using (var source = await http.GetStreamAsync(download.Url))
            await using (var file = File.Create(temp))
                await source.CopyToAsync(file);
            File.Move(temp, target, overwrite: true);
        }

        foreach (var archive in Directory.GetFiles(Repo.Downloads, "ECMA-376-*.zip"))
        {
            var part = Path.GetFileName(archive)["ECMA-376-".Length..][..1];
            var dir = Path.Combine(Repo.Ecma, "part" + part);
            ZipFile.ExtractToDirectory(archive, dir, overwriteFiles: true);

            foreach (var nested in Directory.GetFiles(dir, "*.zip"))
            {
                if (NestedArchives.TryGetValue(Path.GetFileName(nested), out var folder))
                    ZipFile.ExtractToDirectory(nested, Path.Combine(dir, folder), overwriteFiles: true);
            }
            Console.WriteLine($"  unpacked    part {part} -> {Path.GetRelativePath(Repo.Root, dir)}");
        }
    }
}
