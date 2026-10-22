using System.Text;
using FreeSlate.Text;

namespace FreeSlate.Html;

/// <summary>
/// The glyph outlines used by a document (catalog: engine.textLayout): one SVG path per font and glyph in font
/// units, defined once and placed with &lt;use&gt;.
/// </summary>
public sealed class GlyphCache
{
    readonly Dictionary<(string Font, int Face, string Instance, uint Glyph), string?> ids = new();
    readonly Dictionary<string, string> outlines = [];
    readonly StringBuilder defs = new();

    /// <summary>The id of the glyph's path, or null for glyphs without contours (spaces).</summary>
    public string? Id(FontData font, uint glyph)
    {
        if (ids.TryGetValue((font.Path, font.Index, font.Instance, glyph), out var id))
            return id;
        var outline = font.Outline(glyph);
        id = outline.Length == 0 ? null : "_g" + ids.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ids[(font.Path, font.Index, font.Instance, glyph)] = id;
        if (id != null)
        {
            defs.Append($"<path id=\"{id}\" d=\"{outline}\"/>");
            outlines[id] = outline;
        }
        return id;
    }

    /// <summary>The outline (SVG path data in font units) of the glyph an id names, or null for an id not given out.</summary>
    public string? Outline(string id) => outlines.GetValueOrDefault(id);

    /// <summary>An invisible SVG holding the definitions, or "" when no glyph was used.</summary>
    public string Svg() => defs.Length == 0 ? "" : $"<svg class=\"glyphs\" width=\"0\" height=\"0\" aria-hidden=\"true\"><defs>{defs}</defs></svg>\n";
}
