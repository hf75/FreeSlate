using System.Xml.Linq;

namespace FreeSlate.Pptx;

/// <summary>
/// PowerPoint's 74 built-in table styles (catalog: table.styles), the only ones it draws: the definitions a file
/// carries in ppt/tableStyles.xml are not used for drawing, not even under a built-in style's id.
/// </summary>
static class BuiltInTableStyles
{
    /// <summary>No Style, Table Grid: drawn for tables without a built-in style.</summary>
    public const string TableGrid = "{5940675A-B579-460E-94D1-54222C63F5DA}";

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
