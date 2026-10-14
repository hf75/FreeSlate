using System.Globalization;
using System.Text;
using FreeSlate.Drawing;
using FreeSlate.Text;

namespace FreeSlate.Equations;

/// <summary>
/// A laid-out piece of an equation in points: how far it reaches right of its origin, above its baseline (ascent)
/// and below it (descent), and the italic correction a script after it adds.
/// </summary>
public abstract class MathItem
{
    public double Width { get; set; }
    public double Ascent { get; set; }
    public double Descent { get; set; }
    public double Italic { get; set; }

    /// <summary>Appends the item's SVG with its origin at (x, y) points (y the baseline, down positive).</summary>
    public abstract void Draw(MathCanvas canvas, double x, double y);
}

/// <summary>One glyph of a font at a size, in a color.</summary>
public sealed class GlyphItem : MathItem
{
    public required FontData Font { get; init; }
    public required uint Glyph { get; init; }
    public required double Size { get; init; }
    public required RgbColor Color { get; init; }

    /// <summary>The character the glyph shows (for the selectable text), or null.</summary>
    public string? Text { get; init; }

    /// <summary>The character the glyph is the font's glyph of (its math style), and the script level whose form it has (ssty).</summary>
    public (int CodePoint, int Level)? Character { get; init; }

    public override void Draw(MathCanvas canvas, double x, double y) => canvas.Glyph(this, x, y);
}

/// <summary>A filled rectangle from the origin: Width wide, from Ascent above the baseline to Descent below.</summary>
public sealed class RuleItem : MathItem
{
    public required RgbColor Color { get; init; }

    public override void Draw(MathCanvas canvas, double x, double y) => canvas.Rule(x, y - Ascent, Width, Ascent + Descent, Color);
}

/// <summary>Items placed relative to the list's origin (Y down positive).</summary>
public sealed class ListItem : MathItem
{
    public List<(MathItem Item, double X, double Y)> Children { get; } = [];

    /// <summary>An outline drawn around the list (m:borderBox), as rule items placed with the children.</summary>
    public override void Draw(MathCanvas canvas, double x, double y)
    {
        foreach (var (item, dx, dy) in Children)
            item.Draw(canvas, x + dx, y + dy);
    }

    /// <summary>Adds an item and grows the list's box around it.</summary>
    public void Add(MathItem item, double x, double y)
    {
        Children.Add((item, x, y));
        Width = Math.Max(Width, x + item.Width);
        Ascent = Math.Max(Ascent, item.Ascent - y);
        Descent = Math.Max(Descent, item.Descent + y);
    }
}

/// <summary>An empty item taking room (spaces, phantoms).</summary>
public sealed class SpaceItem : MathItem
{
    public override void Draw(MathCanvas canvas, double x, double y) { }
}

/// <summary>Collects an equation's SVG: glyphs as uses of the document's glyph outlines, rules as rectangles.</summary>
public sealed class MathCanvas(Html.GlyphCache glyphs, double pxPerPt)
{
    readonly StringBuilder svg = new();
    readonly StringBuilder text = new();

    /// <summary>
    /// Whether glyphs that stand for their character are drawn as text in their font, the rest (stretched and large
    /// variants) as outlines: Word's PDFs hold the math as text, which the rasterizer fits to the pixel grid, an
    /// outline only smoothed (scratch/triage/2026-10-14_agents/math-embedded: powertools/RP/RP013's "=" bars one
    /// black row in Word, two gray ones as outlines; as text 82.8 → 90.8 %).
    /// </summary>
    public bool AsText { get; init; }

    public void Glyph(GlyphItem glyph, double x, double y)
    {
        if (AsText && glyph.Character is var (codepoint, level) && glyph.Glyph != 0 && glyph.Font.GlyphFor(codepoint) is var plain && plain != 0
            && (plain == glyph.Glyph || level > 0) && char.ConvertFromUtf32(codepoint) is var character)
        {
            var family = glyph.Font.Family.Replace("'", "\\'");
            svg.Append($"<text x=\"{N(x * pxPerPt)}\" y=\"{N(y * pxPerPt)}\" font-family=\"'{System.Net.WebUtility.HtmlEncode(family)}'\" font-size=\"{N4(glyph.Size * pxPerPt)}\" fill=\"{glyph.Color.ToHex()}\"");
            if (glyph.Color.A < 0.999)
                svg.Append($" fill-opacity=\"{N4(glyph.Color.A)}\"");
            // A script form is the font's ssty alternate of the level (Chrome applies the feature as the layout did).
            var features = plain != glyph.Glyph ? $";font-feature-settings:'ssty' {level}" : "";
            svg.Append($" style=\"font-kerning:none{features}\">{System.Net.WebUtility.HtmlEncode(character)}</text>");
            text.Append(glyph.Text ?? character);
            return;
        }
        if (glyphs.Id(glyph.Font, glyph.Glyph) is not { } id)
            return;
        var scale = glyph.Size * pxPerPt / glyph.Font.UnitsPerEm;
        svg.Append($"<use href=\"#{id}\" transform=\"translate({N(x * pxPerPt)} {N(y * pxPerPt)}) scale({N4(scale)})\" fill=\"{glyph.Color.ToHex()}\"");
        if (glyph.Color.A < 0.999)
            svg.Append($" fill-opacity=\"{N4(glyph.Color.A)}\"");
        svg.Append("/>");
        if (glyph.Text != null)
            text.Append(glyph.Text);
    }

    public void Rule(double x, double y, double width, double height, RgbColor color)
    {
        if (width <= 0 || height <= 0)
            return;
        svg.Append($"<rect x=\"{N(x * pxPerPt)}\" y=\"{N(y * pxPerPt)}\" width=\"{N(width * pxPerPt)}\" height=\"{N(height * pxPerPt)}\" fill=\"{color.ToHex()}\"");
        if (color.A < 0.999)
            svg.Append($" fill-opacity=\"{N4(color.A)}\"");
        svg.Append("/>");
    }

    public string Svg => svg.ToString();

    /// <summary>The characters drawn, in order (for the equation's selectable text).</summary>
    public string Text => text.ToString();

    static string N(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);
    static string N4(double v) => Math.Round(v, 5).ToString("0.#####", CultureInfo.InvariantCulture);
}
