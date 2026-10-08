using System.Text.RegularExpressions;
using SpecTools.Pdf;
using UglyToad.PdfPig;

namespace SpecTools;

/// <summary>A section of a specification that describes one element, attribute or type.</summary>
public sealed record SpecSection(string Doc, string Section, string Name, string Title, int Page, string? Namespace = null, string? Kind = null);

/// <summary>A section of [MS-OI29500]: Office implementation notes for one section of ISO/IEC 29500.</summary>
public sealed record OfficeNoteSection(string Section, int Part, string IsoSection, string Title, int Page);

/// <summary>Section index built from the PDF bookmarks of ECMA-376 and the Microsoft documents.</summary>
public sealed partial class SpecIndex
{
    /// <summary>Element and simple-type sections of ECMA-376 Part 1 and Part 4.</summary>
    public List<SpecSection> Ecma { get; } = [];

    /// <summary>Element, attribute and type sections of [MS-DOCX], [MS-PPTX] and [MS-ODRAWXML].</summary>
    public List<SpecSection> Ms { get; } = [];

    /// <summary>Field definitions (ECMA-376 Part 1, §17.16.5).</summary>
    public List<SpecSection> FieldCodes { get; } = [];

    public List<OfficeNoteSection> OfficeNotes { get; } = [];

    public Dictionary<string, List<OutlineEntry>> Outlines { get; } = [];

    /// <summary>Revision of each Microsoft document, e.g. "v20260818".</summary>
    public Dictionary<string, string> MsVersions { get; } = [];

    public static SpecIndex Build()
    {
        var index = new SpecIndex();
        index.AddEcma("ECMA-376-1", Sources.EcmaPart1Pdf);
        index.AddEcma("ECMA-376-4", Sources.EcmaPart4Pdf);
        foreach (var doc in Sources.MsSchemaDocs)
            index.AddMs(doc, Sources.MsPdf(doc));
        index.AddOfficeNotes(Sources.MsPdf("MS-OI29500"));
        return index;
    }

    void AddEcma(string doc, string pdf)
    {
        var outline = Outline.Read(pdf);
        Outlines[doc] = outline;
        foreach (var entry in outline)
        {
            if (entry.Page is not int page)
                continue;
            if (NamedSection().Match(entry.Title) is { Success: true } m)
                Ecma.Add(new SpecSection(doc, m.Groups["num"].Value, m.Groups["name"].Value, m.Groups["title"].Value, page));
            else if (doc == "ECMA-376-1" && FieldSection().Match(entry.Title) is { Success: true } f)
                FieldCodes.Add(new SpecSection(doc, f.Groups["num"].Value, f.Groups["name"].Value, f.Groups["name"].Value, page));
        }
    }

    void AddMs(string doc, string pdf)
    {
        var outline = ReadMsDocument(doc, pdf);

        // Body structure: "2.6 http://ns" > "2.6.1 Elements" > "2.6.1.1 checkbox".
        // The appendix headings ("5.1 http://ns Schema") do not match NamespaceSection.
        var namespaces = new Dictionary<int, string>();
        foreach (var entry in outline)
        {
            if (NamespaceSection().Match(entry.Title) is { Success: true } ns)
            {
                namespaces[entry.Index] = ns.Groups["uri"].Value;
                continue;
            }
            if (entry.Parent is not int kindIndex || outline[kindIndex].Parent is not int nsIndex || !namespaces.TryGetValue(nsIndex, out var uri))
                continue;
            if (entry.Page is int page && ItemSection().Match(entry.Title) is { Success: true } item)
            {
                var kind = KindOf().Replace(outline[kindIndex].Title, "");
                Ms.Add(new SpecSection(doc, item.Groups["num"].Value, item.Groups["name"].Value, item.Groups["name"].Value, page, uri, kind));
            }
        }
    }

    void AddOfficeNotes(string pdf)
    {
        var outline = ReadMsDocument("MS-OI29500", pdf);
        foreach (var entry in outline)
        {
            if (entry.Page is int page && OfficeNoteTitle().Match(entry.Title) is { Success: true } m)
            {
                OfficeNotes.Add(new OfficeNoteSection(
                    m.Groups["num"].Value, int.Parse(m.Groups["part"].Value), m.Groups["iso"].Value, m.Groups["title"].Value, page));
            }
        }
    }

    /// <summary>Reads the outline and the revision ("[MS-DOCX] - v20260818" in the page footer).</summary>
    List<OutlineEntry> ReadMsDocument(string doc, string pdfPath)
    {
        using var pdf = PdfDocument.Open(pdfPath);
        var outline = Outline.Read(pdf);
        Outlines[doc] = outline;
        var version = PdfLines.Read(pdf.GetPage(Math.Min(2, pdf.NumberOfPages)))
            .Select(l => MsRevision().Match(l.Text))
            .FirstOrDefault(m => m.Success);
        if (version != null)
            MsVersions[doc] = "v" + version.Groups["v"].Value;
        return outline;
    }

    [GeneratedRegex(@"\[MS-[A-Z0-9]+\] - v(?<v>\d{8})")]
    private static partial Regex MsRevision();

    /// <summary>"17.3.1.29 rPr (Run Properties for the Paragraph Mark)"</summary>
    [GeneratedRegex(@"^(?<num>\d+(?:\.\d+)+)\s+(?<name>[A-Za-z_][\w.\-]*)\s+\((?<title>.+)\)$")]
    private static partial Regex NamedSection();

    /// <summary>"17.16.5.1 ADDRESSBLOCK"</summary>
    [GeneratedRegex(@"^(?<num>17\.16\.5\.\d+)\s+(?<name>[A-Z][A-Z0-9]*)$")]
    private static partial Regex FieldSection();

    /// <summary>"2.6 http://schemas.microsoft.com/office/word/2010/wordml"</summary>
    [GeneratedRegex(@"^(?<num>\d+(?:\.\d+)+)\s+(?<uri>(?:https?://|urn:)\S+)$")]
    private static partial Regex NamespaceSection();

    /// <summary>"2.6.1.1 checkbox"</summary>
    [GeneratedRegex(@"^(?<num>\d+(?:\.\d+)+)\s+(?<name>[A-Za-z_][\w.\-]*)$")]
    private static partial Regex ItemSection();

    /// <summary>Strips the section number from "2.6.1 Elements".</summary>
    [GeneratedRegex(@"^[\d.]+\s+")]
    private static partial Regex KindOf();

    /// <summary>"2.1.57 Part 1 Section 17.3.1.29, rPr (Run Properties for the Paragraph Mark)"</summary>
    [GeneratedRegex(@"^(?<num>\d+(?:\.\d+)+)\s+Part\s+(?<part>\d+)\s+Section\s+(?<iso>\d+(?:\.\d+)*),\s*(?<title>.+)$")]
    private static partial Regex OfficeNoteTitle();
}
