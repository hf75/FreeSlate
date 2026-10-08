using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Outline;

namespace SpecTools.Pdf;

/// <summary>A PDF bookmark, flattened in document order. <see cref="Parent"/> is the index of the parent entry.</summary>
public sealed record OutlineEntry(int Index, int Level, string Title, int? Page, int? Parent);

public static partial class Outline
{
    public static List<OutlineEntry> Read(PdfDocument pdf)
    {
        var entries = new List<OutlineEntry>();
        if (!pdf.TryGetBookmarks(out var bookmarks))
            return entries;

        void Walk(BookmarkNode node, int? parent)
        {
            var index = entries.Count;
            var page = node is DocumentBookmarkNode d ? d.PageNumber : (int?)null;
            entries.Add(new OutlineEntry(index, node.Level, Whitespace().Replace(node.Title, " ").Trim(), page, parent));
            foreach (var child in node.Children)
                Walk(child, index);
        }

        foreach (var root in bookmarks.Roots)
            Walk(root, null);
        return entries;
    }

    public static List<OutlineEntry> Read(string pdfPath)
    {
        using var pdf = PdfDocument.Open(pdfPath);
        return Read(pdf);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
