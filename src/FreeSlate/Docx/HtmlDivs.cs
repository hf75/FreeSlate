using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>A division of a document that came from HTML (w:div): its margins in points, its borders and its parent.</summary>
public sealed record HtmlDiv(string Id, double Left, double Right, BorderLine? Top, BorderLine? LeftLine, BorderLine? Bottom, BorderLine? RightLine, string? Parent)
{
    /// <summary>Whether the division draws a border.</summary>
    public bool Bordered => Top is { Visible: true } || LeftLine is { Visible: true } || Bottom is { Visible: true } || RightLine is { Visible: true };
}

/// <summary>
/// The HTML divisions of the web settings (w:divs, catalog: paragraph.htmlDivs) that paragraphs refer to with
/// w:divId: nested through w:divsChild; their margins add to the indents of their paragraphs, their borders frame
/// them (measured with docx/local/html-divs, a page Word converted from HTML).
/// </summary>
public sealed class HtmlDivs
{
    readonly Dictionary<string, HtmlDiv> divs = new(StringComparer.Ordinal);

    public HtmlDivs(XDocument? webSettings)
    {
        Read(webSettings?.Root?.Child("divs"), null);
    }

    void Read(XElement? container, string? parent)
    {
        foreach (var div in container?.Elements(Wml.W + "div") ?? [])
        {
            if (div.Attr("id") is not { } id)
                continue;
            var borders = div.Child("divBdr");
            divs.TryAdd(id, new HtmlDiv(id, div.Child("marLeft").Twips("val") ?? 0, div.Child("marRight").Twips("val") ?? 0,
                BorderLine.Read(borders.Child("top")), BorderLine.Read(borders.Child("left")),
                BorderLine.Read(borders.Child("bottom")), BorderLine.Read(borders.Child("right")), parent));
            Read(div.Child("divsChild"), id);
        }
    }

    /// <summary>A division and the divisions around it, the outermost first; empty for none.</summary>
    public List<HtmlDiv> Chain(string? id)
    {
        var chain = new List<HtmlDiv>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var div = id != null ? divs.GetValueOrDefault(id) : null; div != null && seen.Add(div.Id); div = div.Parent != null ? divs.GetValueOrDefault(div.Parent) : null)
            chain.Insert(0, div);
        return chain;
    }
}
