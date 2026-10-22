using System.Collections.Concurrent;
using System.Xml.Linq;
using FreeSlate.Rendering;
using FreeSlate.Text;

namespace FreeSlate.Equations;

/// <summary>A laid-out equation standing in a line of text (catalog: text.math).</summary>
public sealed class Equation(MathItem item) : IInlineObject
{
    public double Width => item.Width;
    public double Ascent => item.Ascent;
    public double Descent => item.Descent;

    /// <summary>The equation's scene items in points with its origin at (x, y) on the baseline, and the characters it shows.</summary>
    /// <param name="asText">Whether glyphs of plain characters are drawn as text in the math font (MathCanvas).</param>
    public (List<SceneItem> Items, string Text) Draw(double x, double y, Html.GlyphCache glyphs, bool asText = false)
    {
        var canvas = new MathCanvas(glyphs) { AsText = asText };
        item.Draw(canvas, x, y);
        return (canvas.Items, canvas.Text);
    }

    public static readonly XNamespace A14 = "http://schemas.microsoft.com/office/drawing/2010/main";

    static readonly string[] MathFamilies = ["Cambria Math", "STIX Two Math", "Latin Modern Math", "XITS Math", "DejaVu Math TeX Gyre", "Libertinus Math"];
    static readonly ConcurrentDictionary<FontResolver, (FontData Font, MathTable Table)?> MathFonts = new();

    /// <summary>The math font PowerPoint uses (Cambria Math), else another installed font with a MATH table, or null.</summary>
    public static (FontData Font, MathTable Table)? MathFont(FontResolver fonts) => MathFonts.GetOrAdd(fonts, resolver =>
    {
        foreach (var family in MathFamilies)
        {
            var resolved = resolver.Resolve(family, false, false);
            if (resolved.Data.Family.Equals(family, StringComparison.OrdinalIgnoreCase) && MathTable.Of(resolved.Data) is { } table)
                return (resolved.Data, table);
        }
        return null;
    });

    /// <summary>Whether equations can be drawn here (a math font is available).</summary>
    public static bool Available => MathFont(FontResolver.Default) != null;

    /// <summary>
    /// The equations of an a14:m element: those of an m:oMathPara in display style, one per line, else one inline
    /// m:oMath; and the justification of the display paragraph (m:jc: l, ctr or r), null inline.
    /// </summary>
    public static (List<Equation> Equations, string? Align) Read(XElement a14m, FontResolver fonts, Func<XElement?, MathRunStyle> runStyle)
    {
        if (MathFont(fonts) is not var (font, table))
            return ([], null);
        var layout = new MathLayout(font, table, runStyle);
        var para = a14m.Element(MathLayout.M + "oMathPara");
        if (para != null)
        {
            var jc = (string?)para.Element(MathLayout.M + "oMathParaPr")?.Element(MathLayout.M + "jc")?.Attribute(MathLayout.M + "val");
            var align = jc switch { "left" => "l", "right" => "r", _ => "ctr" };
            return (para.Elements(MathLayout.M + "oMath").Select(m => new Equation(layout.Equation(m, display: true))).ToList(), align);
        }
        return (a14m.Elements(MathLayout.M + "oMath").Select(m => new Equation(layout.Equation(m, display: false))).ToList(), null);
    }
}
