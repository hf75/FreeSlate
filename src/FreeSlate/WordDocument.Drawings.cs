using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Rendering;

namespace FreeSlate;

/// <summary>
/// The drawings of a Word document as Word's Layout Options and its Position gallery change them (catalog: drawing.inline,
/// drawing.anchor): in the line or floating, how the text goes round them, where they stand, how large they are (see
/// DrawingFormatting). A drawing is the character its w:drawing takes in its paragraph's text.
/// </summary>
public sealed partial class WordDocument
{
    const double EmuPerPoint = 12700;

    /// <summary>The DrawingML namespace (a:graphic, a:ext).</summary>
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>
    /// How the drawing at a place of a paragraph's text stands among the text (see DrawingFormatting), every property given that it has
    /// (Wrap also "tight" or "through" for those Word's wrap points go round); null where no drawing stands there.
    /// </summary>
    public DrawingFormatting? DrawingOf(int paragraph, int offset) => DrawingAt(Find(paragraph).Paragraph, offset) is { } own ? Read(own) : null;

    /// <summary>
    /// Gives the drawing at a place of a paragraph's text how it stands among the text (see DrawingFormatting), one change: in the line
    /// or floating, the text round it, its place, its size. A drawing made floating with no place given stays where it stood, across from
    /// its column and down from its paragraph (it moves with the text, as Word's default); one floating keeps its place where none is
    /// given.
    /// </summary>
    /// <exception cref="NotSupportedException">No drawing stands at the place, or it is not where it was read; nothing changes.</exception>
    public void Format(int paragraph, int offset, DrawingFormatting format)
    {
        var (p, top) = Find(paragraph);
        if (DrawingAt(p, offset) is not { } own)
            throw new NotSupportedException("No drawing stands at that place.");
        var drawn = Drawn(own, format, () => WhereItStands(paragraph, offset));
        Change(top, top, (stretch, _) => TextEdits.Redraw(stretch, p, offset, drawn));
    }

    /// <summary>The wp:inline or wp:anchor of the drawing whose character stands at a place of a paragraph's text, or null.</summary>
    static XElement? DrawingAt(ParagraphBlock paragraph, int offset)
    {
        var at = paragraph.TextOffset;
        foreach (var inline in paragraph.Content)
        {
            var length = ParagraphText.Length(inline);
            if (at == offset && length > 0)
                return inline switch { DrawingInline drawing => drawing.Drawing, AnchorInline anchor => anchor.Anchor, _ => null };
            at += length;
        }
        return null;
    }

    /// <summary>How a drawing's element has it stand (see DrawingOf).</summary>
    static DrawingFormatting Read(XElement own)
    {
        var (width, height) = Drawings.Extent(own);
        if (own.Name.LocalName == "inline")
            return new DrawingFormatting { Width = width, Height = height, Wrap = "inline" };
        var wp = own.Name.Namespace;
        var wrap = own.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("wrap", StringComparison.Ordinal));
        var across = own.Element(wp + "positionH");
        var down = own.Element(wp + "positionV");
        return new DrawingFormatting
        {
            Width = width,
            Height = height,
            Wrap = wrap?.Name.LocalName switch
            {
                "wrapSquare" => "square",
                "wrapTight" => "tight",
                "wrapThrough" => "through",
                "wrapTopAndBottom" => "topAndBottom",
                _ => (string?)own.Attribute("behindDoc") is "1" or "true" ? "behind" : "front",
            },
            Side = (string?)wrap?.Attribute("wrapText"),
            HorizontalFrom = (string?)across?.Attribute("relativeFrom"),
            HorizontalAlign = across?.Element(wp + "align")?.Value,
            HorizontalOffset = Points(across?.Element(wp + "posOffset")?.Value),
            VerticalFrom = (string?)down?.Attribute("relativeFrom"),
            VerticalAlign = down?.Element(wp + "align")?.Value,
            VerticalOffset = Points(down?.Element(wp + "posOffset")?.Value),
        };
    }

    static double? Points(string? emu) => long.TryParse(emu, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value / EmuPerPoint : null;

    static string Emu(double points) => ((long)Math.Round(points * EmuPerPoint)).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A drawing's element made anew as a format has it stand (see Format): its own children kept (its picture, its name, its effect
    /// extents), in the order the schema has them; <paramref name="where"/> tells where it stands when it is to float and stood in the line.
    /// </summary>
    XElement Drawn(XElement own, DrawingFormatting format, Func<(double X, double Y)?> where)
    {
        var now = Read(own);
        var wp = own.Name.Namespace;
        var wrap = format.Wrap ?? now.Wrap!;
        var (width, height) = (format.Width, format.Height, now.Width ?? 0, now.Height ?? 0) switch
        {
            ({ } w, { } h, _, _) => (w, h),
            ({ } w, null, var w0, var h0) => (w, w0 > 0 ? h0 * w / w0 : h0),
            (null, { } h, var w0, var h0) => (h0 > 0 ? w0 * h / h0 : w0, h),
            (null, null, var w0, var h0) => (w0, h0),
        };
        XElement? Kept(string name) => own.Element(wp + name) is { } e ? new XElement(e) : null;
        var extent = new XElement(wp + "extent", new XAttribute("cx", Emu(width)), new XAttribute("cy", Emu(height)));
        var effect = Kept("effectExtent") ?? new XElement(wp + "effectExtent", new XAttribute("l", 0), new XAttribute("t", 0), new XAttribute("r", 0), new XAttribute("b", 0));
        var graphic = own.Elements().FirstOrDefault(e => e.Name.LocalName == "graphic") is { } g ? new XElement(g) : null;
        // A picture's own size goes with the drawing's (pic:spPr/a:xfrm/a:ext).
        if (graphic?.Elements().FirstOrDefault()?.Elements().FirstOrDefault()?.Elements().FirstOrDefault(e => e.Name.LocalName == "spPr")
                ?.Element(A + "xfrm")?.Element(A + "ext") is { } ext)
        {
            ext.SetAttributeValue("cx", Emu(width));
            ext.SetAttributeValue("cy", Emu(height));
        }
        if (wrap == "inline")
            return new XElement(wp + "inline", new XAttribute("distT", 0), new XAttribute("distB", 0), new XAttribute("distL", 0), new XAttribute("distR", 0),
                extent, effect, Kept("docPr"), Kept("cNvGraphicFramePr"), graphic);
        var floating = own.Name.LocalName == "anchor";
        // Where it stands, each way: as given, else where it is (floating), else where it stood in the line.
        var stood = new Lazy<(double X, double Y)?>(() => floating ? null : where());
        XElement Position(string name, string? from, string? align, double? offset, string defaultFrom, double? stoodAt)
        {
            if (from == null && align == null && offset == null && floating && own.Element(wp + name) is { } kept)
                return new XElement(kept);
            var position = new XElement(wp + name, new XAttribute("relativeFrom", from ?? defaultFrom));
            position.Add(align != null ? new XElement(wp + "align", align) : new XElement(wp + "posOffset", Emu(offset ?? stoodAt ?? 0)));
            return position;
        }
        var across = Position("positionH", format.HorizontalFrom, format.HorizontalAlign, format.HorizontalOffset, "column",
            format.HorizontalFrom == null && format.HorizontalAlign == null && format.HorizontalOffset == null ? stood.Value?.X : null);
        var down = Position("positionV", format.VerticalFrom, format.VerticalAlign, format.VerticalOffset, "paragraph",
            format.VerticalFrom == null && format.VerticalAlign == null && format.VerticalOffset == null ? stood.Value?.Y : null);
        var wrapping = wrap switch
        {
            "square" => new XElement(wp + "wrapSquare", new XAttribute("wrapText", format.Side ?? (now.Wrap == "square" ? now.Side : null) ?? "bothSides")),
            "topAndBottom" => new XElement(wp + "wrapTopAndBottom"),
            "behind" or "front" => new XElement(wp + "wrapNone"),
            _ => own.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("wrap", StringComparison.Ordinal)) is { } other
                ? new XElement(other) : new XElement(wp + "wrapSquare", new XAttribute("wrapText", "bothSides")),
        };
        string Attribute(string name, string made) => floating && own.Attribute(name) is { } had ? had.Value : made;
        return new XElement(wp + "anchor",
            new XAttribute("distT", Attribute("distT", "0")), new XAttribute("distB", Attribute("distB", "0")),
            new XAttribute("distL", Attribute("distL", "114300")), new XAttribute("distR", Attribute("distR", "114300")),
            new XAttribute("simplePos", "0"), new XAttribute("relativeHeight", Attribute("relativeHeight", NextHeight())),
            new XAttribute("behindDoc", wrap == "behind" ? "1" : "0"), new XAttribute("locked", Attribute("locked", "0")),
            new XAttribute("layoutInCell", Attribute("layoutInCell", "1")), new XAttribute("allowOverlap", Attribute("allowOverlap", "1")),
            new XElement(wp + "simplePos", new XAttribute("x", 0), new XAttribute("y", 0)),
            across, down, extent, effect, wrapping, Kept("docPr"), Kept("cNvGraphicFramePr"), graphic,
            own.Elements().Where(e => e.Name.LocalName is "sizeRelH" or "sizeRelV").Select(e => new XElement(e)));
    }

    /// <summary>The stacking order a drawing made floating takes, above those of the main part (wp:anchor relativeHeight), as Word stacks a new one.</summary>
    string NextHeight() =>
        (islands.Select(i => i.Text).Prepend(wholeText.Text).SelectMany(text => StackingOrder().Matches(text))
            .Select(m => long.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var order) ? order : 0)
            .DefaultIfEmpty(251658239).Max() + 1).ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"relativeHeight=""(\d{1,12})""")]
    private static partial Regex StackingOrder();

    /// <summary>
    /// Where the drawing at a place stands as the pages laid out so far show it (see PlacesItem.Boxes): its box's left from the left of
    /// its section's column it stands over, its top from its paragraph's first line on that page, in points; null where it shows nowhere.
    /// </summary>
    (double X, double Y)? WhereItStands(int paragraph, int offset)
    {
        // Each page drawn as it reads now (the pages known may be sketches still, without their places).
        for (var page = 0; page < Scenes().Count; page++)
        {
            var lines = Places(Scene(page).Items).Where(l => l.Block == paragraph && !l.Floating).ToList();
            foreach (var line in lines)
            {
                if (Array.IndexOf(line.Offsets, offset) is var index and >= 0 && line.Boxes.TryGetValue(index, out var box))
                {
                    var section = SectionOf(paragraph);
                    var columns = Math.Max(1, section.Columns ?? 1);
                    var (left, space) = (section.Left ?? 72, section.ColumnSpace ?? 36);
                    var width = ((section.PageWidth ?? 612) - left - (section.Right ?? 72) - (section.Gutter ?? 0) - (columns - 1) * space) / columns;
                    var column = Math.Clamp((int)Math.Floor((box.Left - left) / (width + space)), 0, columns - 1);
                    return (box.Left - (left + column * (width + space)), box.Top - lines.Min(l => l.Top));
                }
            }
        }
        return null;
    }

    /// <summary>The places items among a scene's items and in the items they hold.</summary>
    static IEnumerable<PlacesItem> Places(IEnumerable<SceneItem> items)
    {
        foreach (var item in items)
        {
            if (item is PlacesItem places)
                yield return places;
            else if (item is IHoldsItems holder)
            {
                foreach (var inner in Places(holder.Items))
                    yield return inner;
            }
        }
    }
}
