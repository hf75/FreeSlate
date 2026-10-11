using SpecTools.Pdf;
using UglyToad.PdfPig;

namespace SpecTools;

/// <summary>
/// Prints the text of one section of a specification, for reading what it says while measuring Office:
/// `text ECMA-376-1 17.3.1.33`, `text MS-OI29500 2.1.60`, or `text MS-OI29500 17.3.1.33` for the Office notes on an
/// ISO/IEC 29500 section.
/// </summary>
public static class SpecText
{
    public static int Print(string doc, string section)
    {
        var path = doc switch
        {
            "ECMA-376-1" => Sources.EcmaPart1Pdf,
            "ECMA-376-4" => Sources.EcmaPart4Pdf,
            _ => Sources.MsPdf(doc),
        };
        using var pdf = PdfDocument.Open(path);
        var outline = Outline.Read(pdf);
        var matches = outline.Where(e => e.Page != null && (e.Title.StartsWith(section + " ", StringComparison.Ordinal)
                                                           || doc == "MS-OI29500" && e.Title.Contains($"Part 1 Section {section},", StringComparison.Ordinal)))
            .ToList();
        if (matches.Count == 0)
        {
            Console.Error.WriteLine($"No section {section} in {doc}.");
            return 1;
        }
        foreach (var entry in matches)
        {
            // The text runs to the next bookmark in document order.
            var next = outline.Skip(entry.Index + 1).FirstOrDefault(e => e.Page != null);
            var lastPage = Math.Min(pdf.NumberOfPages, next?.Page ?? entry.Page!.Value + 3);
            var started = false;
            for (var page = entry.Page!.Value; page <= lastPage; page++)
            {
                foreach (var line in PdfLines.Read(pdf.GetPage(page)))
                {
                    var text = line.Text.Trim();
                    if (!started)
                    {
                        if (!text.StartsWith(entry.Title.Split(' ')[0] + " ", StringComparison.Ordinal))
                            continue;
                        started = true;
                    }
                    else if (next != null && page == next.Page && text.StartsWith(next.Title.Split(' ')[0] + " ", StringComparison.Ordinal))
                    {
                        page = lastPage;
                        break;
                    }
                    Console.WriteLine(text);
                }
            }
            Console.WriteLine();
        }
        return 0;
    }
}
