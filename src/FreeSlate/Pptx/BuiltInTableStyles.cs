using System.Xml.Linq;

namespace FreeSlate.Pptx;

/// <summary>
/// PowerPoint's 74 built-in table styles (catalog: table.styles), for tables that name one in a:tableStyleId without
/// their presentation defining it (files from other producers; PowerPoint writes the styles it uses).
/// </summary>
static class BuiltInTableStyles
{
    static readonly Lazy<Dictionary<string, XElement>> Styles = new(() =>
    {
        using var stream = typeof(BuiltInTableStyles).Assembly.GetManifestResourceStream("builtinTableStyles.xml")
                           ?? throw new InvalidOperationException("builtinTableStyles.xml is missing.");
        var document = XDocument.Load(stream);
        return document.Root!.Elements(Ns.A + "tblStyle")
            .Where(s => s.Attribute("styleId") != null)
            .ToDictionary(s => s.Attribute("styleId")!.Value.ToUpperInvariant(), s => s);
    });

    /// <summary>The built-in style with the given id (braces and case as in a:tableStyleId), or null.</summary>
    public static XElement? Find(string id) => Styles.Value.GetValueOrDefault(id.Trim().ToUpperInvariant());
}
