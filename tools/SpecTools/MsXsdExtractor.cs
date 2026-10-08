using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SpecTools.Pdf;
using UglyToad.PdfPig;

namespace SpecTools;

/// <summary>
/// Extracts the full XML schemas of the Office extensions from "Appendix A: Full XML Schemas"
/// of [MS-DOCX], [MS-PPTX] and [MS-ODRAWXML] and stores them as .xsd files.
/// </summary>
public static partial class MsXsdExtractor
{
    /// <summary>Everything below this baseline is the page footer (page number, document id, copyright).</summary>
    const double FooterTop = 95;

    public sealed record ExtractedSchema(string Prefix, string Namespace, string File, string Document, string Version, string Section, int Page);

    public static string IndexPath => Path.Combine(Repo.MsXsd, "index.json");

    public static List<ExtractedSchema> Run(NamespaceRegistry registry)
    {
        Directory.CreateDirectory(Repo.MsXsd);
        foreach (var old in Directory.GetFiles(Repo.MsXsd, "*.xsd"))
            File.Delete(old);

        var result = new List<ExtractedSchema>();
        foreach (var docName in Sources.MsSchemaDocs)
        {
            using var pdf = PdfDocument.Open(Sources.MsPdf(docName));
            var outline = Outline.Read(pdf);
            var appendix = outline.Single(e => e.Title.EndsWith("Full XML Schemas", StringComparison.Ordinal));
            var sections = outline.Where(e => e.Parent == appendix.Index).ToList();
            var next = outline.FirstOrDefault(e => e.Index > appendix.Index && e.Level <= appendix.Level);
            var lastPage = next?.Page ?? pdf.NumberOfPages;

            var version = "";
            var lines = new List<(string Text, int Page)>();
            for (var p = appendix.Page!.Value; p <= lastPage; p++)
            {
                foreach (var line in PdfLines.Read(pdf.GetPage(p)))
                {
                    if (line.Baseline > FooterTop)
                        lines.Add((line.Text, p));
                    else if (DocVersion().Match(line.Text) is { Success: true } m)
                        version = m.Groups["v"].Value;
                }
            }

            var chunks = SplitAtHeadings(lines);
            foreach (var section in sections)
            {
                var heading = SchemaHeading().Match(section.Title);
                var number = heading.Groups["num"].Value;
                if (!chunks.TryGetValue(number, out var chunk))
                    throw new InvalidOperationException($"{docName}: section {number} not found in the appendix text.");

                var text = string.Join('\n', chunk.Lines);
                var start = text.IndexOf("<xsd:schema", StringComparison.Ordinal);
                var end = text.LastIndexOf("</xsd:schema>", StringComparison.Ordinal);
                if (start < 0 || end < start)
                {
                    Console.WriteLine($"  {docName} {number}: no schema text, skipped");
                    continue;
                }

                XDocument xsd;
                try
                {
                    xsd = XDocument.Parse(text[start..(end + "</xsd:schema>".Length)]);
                }
                catch (XmlException ex)
                {
                    throw new InvalidOperationException($"{docName} {number}: extracted schema is not well-formed: {ex.Message}", ex);
                }

                var ns = xsd.Root!.Attribute("targetNamespace")?.Value ?? "";
                if (ns != heading.Groups["uri"].Value)
                    Console.WriteLine($"  warning: {docName} {number}: targetNamespace {ns} differs from heading {heading.Groups["uri"].Value}");

                var prefix = registry.Prefix(ns);
                var fileName = prefix + ".xsd";
                if (result.Any(r => r.File == fileName))
                    fileName = $"{prefix}.{docName}.xsd";

                xsd.AddFirst(new XComment($" Extracted from [{docName}] v{version}, section {number} ({ns}) by tools/SpecTools. "));
                using (var writer = XmlWriter.Create(Path.Combine(Repo.MsXsd, fileName), new XmlWriterSettings { Indent = true }))
                    xsd.Save(writer);

                result.Add(new ExtractedSchema(prefix, ns, fileName, docName, version, number, chunk.Page));
                Console.WriteLine($"  {docName} {number,-5} {prefix,-10} {ns}");
            }
        }

        File.WriteAllText(IndexPath, JsonSerializer.Serialize(result, Json.Options));
        return result;
    }

    public static List<ExtractedSchema> LoadIndex() =>
        File.Exists(IndexPath)
            ? JsonSerializer.Deserialize<List<ExtractedSchema>>(File.ReadAllText(IndexPath), Json.Options) ?? []
            : [];

    sealed record Chunk(int Page, List<string> Lines);

    /// <summary>
    /// Splits the appendix text at headings such as "5.3 http://... Schema".
    /// Long headings wrap, leaving "Schema" alone on the following line.
    /// </summary>
    static Dictionary<string, Chunk> SplitAtHeadings(List<(string Text, int Page)> lines)
    {
        var chunks = new Dictionary<string, Chunk>();
        Chunk? current = null;
        for (var i = 0; i < lines.Count; i++)
        {
            var (text, page) = lines[i];
            var heading = SchemaHeading().Match(text);
            if (heading.Success)
            {
                current = new Chunk(page, []);
                chunks[heading.Groups["num"].Value] = current;
                if (!text.EndsWith("Schema", StringComparison.Ordinal) && i + 1 < lines.Count && lines[i + 1].Text == "Schema")
                    i++;
                continue;
            }
            current?.Lines.Add(text);
        }
        return chunks;
    }

    [GeneratedRegex(@"^(?<num>\d+\.\d+)\s+(?<uri>(?:https?://|urn:)\S+)(?:\s+Schema)?$")]
    private static partial Regex SchemaHeading();

    [GeneratedRegex(@"\[MS-[A-Z0-9]+\] - v(?<v>\d{8})")]
    private static partial Regex DocVersion();
}
