using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>
/// Legacy VML drawings of Word documents (w:pict, catalog: vml.shapes, vml.watermarks, vml.ole) converted to Word
/// drawings: a wp:anchor or wp:inline with DrawingML content (a picture, a shape with its text box or WordArt text),
/// so that they are placed and drawn like DrawingML drawings. The style attribute gives size and place
/// (position:absolute, margin-left, mso-position-horizontal-relative …), fillcolor, strokecolor, v:fill, v:stroke,
/// v:imagedata, v:textbox and v:textpath the look.
/// </summary>
public static class Vml
{
    public static readonly XNamespace V = "urn:schemas-microsoft-com:vml";
    public static readonly XNamespace O = "urn:schemas-microsoft-com:office:office";
    public static readonly XNamespace W10 = "urn:schemas-microsoft-com:office:word";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    const double EmuPerPoint = 12700;

    /// <summary>
    /// Marks an inline shape that hangs from the top of its line instead of standing on the baseline: Word puts an
    /// inline VML shape at the top of its line, of its paragraph's space before on the first line, pictures on the
    /// baseline (measured with docx/local/vml-inline6, vml-inline7: 40 pt shapes beside 60 pt ones, a 6 pt shape in
    /// text, 48 pt text, space before, exact, at least and double spacing).
    /// </summary>
    public static readonly XName Hangs = XNamespace.Get("urn:freeslate:vml") + "hangs";

    /// <summary>Marks an inline drawing converted from VML, whose effect extents are the engine's own.</summary>
    public static readonly XName Converted = XNamespace.Get("urn:freeslate:vml") + "converted";

    /// <summary>Marks any drawing converted from VML, inline or floating.</summary>
    public static readonly XName Source = XNamespace.Get("urn:freeslate:vml") + "source";

    /// <summary>The VML shape a converted floating drawing comes from, which its stacking order counts with (DocumentContext.StackingOrder).</summary>
    public sealed record Origin(XElement Shape);

    /// <summary>
    /// Marks a horizontal rule (o:hr): its share of the column's width, 0 for the style's width; RuleAlign its
    /// alignment (left, center, right), RuleBevel a shaded one. Word draws a rule across o:hrpct of the width of the
    /// column it stands in (all of it without, whatever width the style gives; the style's width at 0), in a cell or
    /// a text box too, aligned by o:hralign in its paragraph's text area, on a line of its own; that line is as high
    /// as the paragraph mark's, the rule's bottom 2.52 pt above its foot, before Word 2013 where the mark is not on it
    /// only 2.52 pt higher and lower than the rule, whatever the spacing. A rule of a share widens no autofit column,
    /// a paragraph of nothing else makes one an inch wide. Unless o:hrnoshade it is framed as Internet Explorer's
    /// rules are, 0.24 pt edges of #A0A0A0 above and left and #E3E3E3 below and right (measured with docx/local/hr-*,
    /// hrh-*, hrc-*, hrt-*, hrtm-*: rules of 0.75 to 6 pt in Times New Roman 10 to 24 and Calibri 11 at 1.08 and 1.15
    /// lines, at 50 and 100 % and 0 with 200 pt, alone, before and after text, in autofit cells, in modes 12, 14 and
    /// 15; poi/bug57031, openxml-sdk/May_12_04, tika/testInstrLink, libreoffice/extras-ooxmlexport/
    /// tdf97648_relativeWidth: columns of 216 and 283.5 pt).
    /// </summary>
    public static readonly XName Rule = XNamespace.Get("urn:freeslate:vml") + "rule";

    public static readonly XName RuleAlign = XNamespace.Get("urn:freeslate:vml") + "ruleAlign";

    public static readonly XName RuleBevel = XNamespace.Get("urn:freeslate:vml") + "ruleBevel";

    /// <summary>
    /// Marks a turned shape with what alignment counts of it beyond its box, left, above, right and below (pt, less
    /// than none where it falls short): its box, width and height swapped from 45 to 135 degrees (and from 225 to
    /// 315), about its center, together with its turned outline, curves' control points and WordArt's glyphs included
    /// (measured with docx/local/vrb-*: a rectangle of 300 by 100 pt turned 30 degrees at the margins' top by its turned
    /// corners, 45 degrees by its swapped box 300 pt high, an oval by its Béziers' control points; vwa-rc*: WordArt
    /// "Airplane" centered on the margins turned 10 to 350 degrees, at 315 by its glyphs' height and its box's width).
    /// Offsets place the box itself (vwa-rot, vwa-rcabs).
    /// </summary>
    public static readonly XName TurnedBox = XNamespace.Get("urn:freeslate:vml") + "turned";

    /// <summary>A turned shape's sides beyond its box for alignment (see TurnedBox), or null.</summary>
    public static (double L, double T, double R, double B)? TurnedSides(XElement anchor) =>
        ((string?)anchor.Attribute(TurnedBox))?.Split(' ').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray() is [var l, var t, var r, var b] ? (l, t, r, b) : null;

    /// <summary>
    /// Marks a shape that is not turned with how far its line reaches beyond its box, left, above, right and below (pt),
    /// as Word counts it where it places the shape (Paginator rounds it to pixels): every corner mitered, whatever its
    /// join, where the miter is no longer than twice the stroke's miter limit (8) times half the line, and half the
    /// line further across or down where the miter reaches past half the line that way; a longer one half the line
    /// out from the corner (vli-3 to vli-5: an apex 1.5 pt high for a 2 pt line); a line thinner than 0.75 pt
    /// as one of 0.75 pt (measured with docx/local/vlb-*, vlc-*: rectangles with lines of 0.75 to 6 pt half the line
    /// out, round joins as mitered ones, triangles with apexes of 10 to 90 degrees and lines of 1 to 8 pt their miter
    /// and half the line up; vlg-*: a diamond's corners likewise, a triangle's base corners only across; vli-*:
    /// miters of 13 and 15 times half the line count, 17 to 25 not, 17 with miterlimit 20; LibreOffice's
    /// extras-ooxmlimport/n820504: a plaque's freeform outline with corners up to 59 times).
    /// </summary>
    public static readonly XName LineReachBox = XNamespace.Get("urn:freeslate:vml") + "lineReach";

    /// <summary>A converted line's VML miter limit (v:stroke miterlimit), where it has one (see LineReachBox).</summary>
    static readonly XName MiterLimit = XNamespace.Get("urn:freeslate:vml") + "miterLimit";

    /// <summary>A shape's line beyond its box (see LineReachBox), or null.</summary>
    public static (double L, double T, double R, double B)? LineReach(XElement anchor) =>
        ((string?)anchor.Attribute(LineReachBox))?.Split(' ').Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray() is [var l, var t, var r, var b] ? (l, t, r, b) : null;

    /// <summary>The shape types of a part by their ids, the first of each id (Types).</summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XDocument, Dictionary<string, XElement>> PartTypes = new();

    /// <summary>What was worked out of a part as a whole (its shape types, its chains of text boxes) forgotten (see DocumentContext.Changed).</summary>
    internal static void Forget(XDocument part)
    {
        PartTypes.Remove(part);
        Chains.Remove(part);
    }

    /// <summary>
    /// The shape types a w:pict or w:object can refer to: its own, then the others of its part, as Word resolves them
    /// across the document (docx/external/poi/WordWithAttachments: a picture frame type defined in the first w:object
    /// and used by later ones; libreoffice/core-objectpositioning/vml-vertical-alignment: triangles whose type the
    /// first w:pict defines).
    /// </summary>
    static readonly XElement PictureFrame = new(V + "shapetype", new XAttribute("id", "_x0000_t75"), new XAttribute("coordsize", "21600,21600"),
        new XAttribute(O + "spt", "75"), new XAttribute("filled", "f"), new XAttribute("stroked", "f"));

    static Dictionary<string, XElement> Types(XElement pict)
    {
        static Dictionary<string, XElement> Of(IEnumerable<XElement> types) => types.Where(t => t.Attribute("id") != null)
            .GroupBy(t => (string)t.Attribute("id")!).ToDictionary(g => g.Key, g => g.First());
        var own = Of(pict.Descendants(V + "shapetype"));
        if (pict.Document is { } document)
        {
            foreach (var (id, type) in PartTypes.GetValue(document, d => Of(d.Descendants(V + "shapetype"))))
                own.TryAdd(id, type);
        }
        // The picture frame (t75) is a type Word knows without its definition: no fill, no line (docx/external/
        // libreoffice/extras-ooxmlexport/fdo76316: shapes of type t75 without a shapetype and without a picture print
        // nothing).
        own.TryAdd("_x0000_t75", PictureFrame);
        return own;
    }

    /// <summary>The drawings of a w:pict or w:object: one per top-level VML shape.</summary>
    public static IEnumerable<XElement> ToDrawings(XElement pict)
    {
        var types = Types(pict);
        foreach (var shape in pict.Elements())
        {
            if (shape.Name.Namespace != V || shape.Name.LocalName == "shapetype")
                continue;
            if (Drawing(shape, types) is { } drawing)
                yield return drawing;
        }
    }

    static XElement? Drawing(XElement shape, Dictionary<string, XElement> types)
    {
        var style = Style((string?)shape.Attribute("style"));
        if (style.GetValueOrDefault("visibility") == "hidden")
            return null;
        var width = Length(style.GetValueOrDefault("width")) ?? 0;
        var height = Length(style.GetValueOrDefault("height")) ?? 0;
        var rule = (string?)shape.Attribute(O + "hr") is "t" or "true" or "1" && style.GetValueOrDefault("position") != "absolute";
        if (rule)
        {
            width = Math.Max(width, 1);
            height = height > 0 ? height : 1.5;
        }
        if (shape.Name == V + "line")
        {
            var (x1, y1) = Point((string?)shape.Attribute("from"));
            var (x2, y2) = Point((string?)shape.Attribute("to"));
            width = Math.Abs(x2 - x1);
            height = Math.Abs(y2 - y1);
            style["margin-left"] = $"{Math.Min(x1, x2).ToString(CultureInfo.InvariantCulture)}pt";
            style["margin-top"] = $"{Math.Min(y1, y2).ToString(CultureInfo.InvariantCulture)}pt";
            style.TryAdd("position", "absolute");
        }
        // A polyline (v:polyline) is its points' box with the path through them, closed where it ends where it began
        // (docx/external/libreoffice/tdf112450_vml_polyline: an open line of three corners and two closed polygons).
        if (shape.Name == V + "polyline" && Points((string?)shape.Attribute("points")) is { Count: >= 2 } points)
        {
            var (minX, minY) = (points.Min(p => p.X), points.Min(p => p.Y));
            width = Math.Max(points.Max(p => p.X) - minX, 0.01);
            height = Math.Max(points.Max(p => p.Y) - minY, 0.01);
            style["margin-left"] = $"{minX.ToString(CultureInfo.InvariantCulture)}pt";
            style["margin-top"] = $"{minY.ToString(CultureInfo.InvariantCulture)}pt";
            style.TryAdd("position", "absolute");
            static long Unit(double points) => (long)Math.Round(points * 100);
            var closed = points.Count > 2 && Math.Abs(points[^1].X - points[0].X) < 0.01 && Math.Abs(points[^1].Y - points[0].Y) < 0.01;
            var corners = (closed ? points.Take(points.Count - 1) : points).Select(p => $"{Unit(p.X - minX)},{Unit(p.Y - minY)}").ToList();
            shape = new XElement(shape);
            shape.SetAttributeValue("path", "m" + corners[0] + "l" + string.Join(",", corners.Skip(1)) + (closed ? "xe" : "e"));
            shape.SetAttributeValue("coordsize", $"{Math.Max(1, Unit(width))},{Math.Max(1, Unit(height))}");
            shape.SetAttributeValue("coordorigin", "0,0");
        }
        // Only v:shape takes a type (AG_Type); other elements' type attributes count for nothing (docx/external/
        // libreoffice/extras-ooxmlexport/n705956-2: a v:rect of a type with a red fill prints white).
        var type = shape.Name == V + "shape" && (string?)shape.Attribute("type") is { } reference && types.TryGetValue(reference.TrimStart('#'), out var t) ? t : null;
        var signature = SignatureLine(shape, width, height);
        var content = signature ?? (shape.Name == V + "group" ? Group(shape, types, width, height, top: true) : Content(shape, type, width, height));
        if (content == null)
            return null;
        var room = shape.Name == V + "group" || signature != null ? (0, 0) : Room(shape, type, content, width, height);
        var overhang = shape.Name == V + "group" && style.GetValueOrDefault("position") != "absolute" ? Overhang(content, width, height) : default;
        // Inline WordArt along a path takes the room of its glyphs instead (see WordArtRoom).
        if (shape.Name != V + "group" && style.GetValueOrDefault("position") != "absolute" && WordArtRoom(content, width, height) is { } glyphRoom)
            (room, overhang) = ((0, 0), glyphRoom);
        // Not where the shape is sized in percent, which takes its line out of the share in ways not measured through
        // (docx/local/vlj-*: a rectangle 40 % of the margins wide 2 px narrower with a 0.75 pt line, centered, 5 px with
        // a 3 pt one placed by an offset; extras-ooxmlexport/docxopenhyperlinkbox: a text box 1.5 px narrower).
        var reach = style.GetValueOrDefault("position") == "absolute" && RelativeSize(style, "width", true) == null && RelativeSize(style, "height", false) == null
            ? Reach(content, width, height) : null;
        var picture = content.Name.Namespace == Drawings.Pic || signature != null;
        if (shape.Name != V + "group" && Bordered(content, shape, width, height) is { } framed)
            (content, width, height) = framed;
        else if (shape.Name != V + "group" && DoubleShadow(content, shape, type, width, height) is { } doubled)
            content = doubled;
        var uri = content.Name.Namespace == Drawings.Pic ? "http://schemas.openxmlformats.org/drawingml/2006/picture"
            : content.Name.Namespace == Drawings.Wpg ? "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup"
            : "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";
        var graphic = new XElement(Ns.A + "graphic", new XElement(Ns.A + "graphicData", new XAttribute("uri", uri), content));
        var id = new string(((string?)shape.Attribute("id") ?? "").Where(char.IsDigit).ToArray());
        var docPr = new XElement(Drawings.Wp + "docPr", new XAttribute("id", id.Length is > 0 and < 10 ? id : "1"), new XAttribute("name", (string?)shape.Attribute("id") ?? ""));
        var extent = new XElement(Drawings.Wp + "extent", new XAttribute("cx", Emu(width)), new XAttribute("cy", Emu(height)));
        if (style.GetValueOrDefault("position") != "absolute")
            return new XElement(Drawings.Wp + "inline", picture || rule ? null : new XAttribute(Hangs, 1), new XAttribute(Converted, 1), new XAttribute(Source, 1),
                rule ? new XAttribute(Rule, double.TryParse((string?)shape.Attribute(O + "hrpct"), NumberStyles.Float, CultureInfo.InvariantCulture, out var pct) && pct >= 0 ? pct / 1000 : 1) : null,
                rule ? new XAttribute(RuleAlign, (string?)shape.Attribute(O + "hralign") ?? "left") : null,
                rule && !picture && (string?)shape.Attribute(O + "hrnoshade") is not ("t" or "true" or "1") ? new XAttribute(RuleBevel, 1) : null,
                extent, new XElement(Drawings.Wp + "effectExtent", new XAttribute("l", Emu(overhang.L)), new XAttribute("t", Emu(overhang.T)),
                    new XAttribute("r", Emu(Math.Max(room.Right, overhang.R))), new XAttribute("b", Emu(Math.Max(room.Bottom, overhang.B)))), docPr, graphic);

        var z = long.TryParse(style.GetValueOrDefault("z-index"), out var zi) ? zi : 0;
        // A shadow widens the shape's extent on its side as DrawingML's effect extent does, which alignment counts
        // (measured with docx/local/sa-v*: a shape centered at the page's top with a shadow 80 pt left and 36 pt up
        // stands 40 pt right of the middle and 36 pt down; docx/external/libreoffice/extras-ooxmlexport/testOuterShdw).
        var (el, et, er, eb) = (0.0, 0.0, 0.0, 0.0);
        if (shape.Name != V + "group" && ShadowOf(shape, type) is { } cast)
        {
            foreach (var (sx, sy) in (string?)cast.Attribute("type") == "double"
                         ? new[] { ShadowOffset((string?)cast.Attribute("offset"), (2, 2)), ShadowOffset((string?)cast.Attribute("offset2"), (-2, -2)) }
                         : new[] { ShadowOffset((string?)cast.Attribute("offset"), (2, 2)) })
            {
                (el, er) = (Math.Max(el, -sx), Math.Max(er, sx));
                (et, eb) = (Math.Max(et, -sy), Math.Max(eb, sy));
            }
        }
        // The distances wrapped text keeps (mso-wrap-distance-*), 9 pt left and right and none above and below unless
        // given (docx/external/libreoffice/extras-ooxmlexport/testOuterShdw: 10.8 pt below a box at the page's top).
        long Distance(string side, long fallback) => Length(style.GetValueOrDefault("mso-wrap-distance-" + side)) is { } d ? Emu(d) : fallback;
        var anchor = new XElement(Drawings.Wp + "anchor", new XAttribute(Source, 1),
            new XAttribute("distT", Distance("top", 0)), new XAttribute("distB", Distance("bottom", 0)),
            new XAttribute("distL", Distance("left", 114300)), new XAttribute("distR", Distance("right", 114300)),
            new XAttribute("simplePos", 0), new XAttribute("relativeHeight", Math.Abs(z)), new XAttribute("behindDoc", z < 0 ? 1 : 0),
            // o:allowincell f lays the shape out outside its cell, as layoutInCell 0 (docx/external/libreoffice/
            // extras-ooxmlexport/tdf129888vml: a page-relative line in a cell in mode 12).
            new XAttribute("locked", 0), new XAttribute("layoutInCell", (string?)shape.Attribute(O + "allowincell") is "f" or "false" ? 0 : 1),
            new XAttribute("allowOverlap", 1),
            new XElement(Drawings.Wp + "simplePos", new XAttribute("x", 0), new XAttribute("y", 0)),
            Position("positionH", style.GetValueOrDefault("mso-position-horizontal-relative"), style.GetValueOrDefault("mso-position-horizontal"),
                Length(style.GetValueOrDefault("margin-left")) ?? Length(style.GetValueOrDefault("left")) ?? 0, horizontal: true),
            Position("positionV", style.GetValueOrDefault("mso-position-vertical-relative"), style.GetValueOrDefault("mso-position-vertical"),
                Length(style.GetValueOrDefault("margin-top")) ?? Length(style.GetValueOrDefault("top")) ?? 0, horizontal: false),
            extent,
            new XElement(Drawings.Wp + "effectExtent", new XAttribute("l", Emu(el)), new XAttribute("t", Emu(et)), new XAttribute("r", Emu(er)), new XAttribute("b", Emu(eb))),
            Wrap(shape.Element(W10 + "wrap")),
            docPr, graphic,
            RelativeSize(style, "width", true), RelativeSize(style, "height", false));
        anchor.AddAnnotation(new Origin(shape));
        if (TurnedSides(content, style, width, height) is { } turned)
            anchor.SetAttributeValue(TurnedBox, string.Join(" ", new[] { turned.L, turned.T, turned.R, turned.B }.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
        else if (reach is { } line)
            anchor.SetAttributeValue(LineReachBox, string.Join(" ", new[] { line.L, line.T, line.R, line.B }.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));
        return anchor;
    }

    /// <summary>
    /// A size relative to the page or its margins (mso-width-percent, mso-height-percent in tenths of a percent, of the
    /// page unless mso-width-relative or mso-height-relative say otherwise) as DrawingML's wp14:sizeRelH or
    /// wp14:sizeRelV, which the layout resolves; null without one (measured with docx/local/vrr-pct-page,
    /// vrr-pct-margin, vrr-pct-center: 50 % and 30 % of the page and of the margins, 92 % and 94 % of the page centred;
    /// docx/external/libreoffice/extras-ooxmlimport/n779627b); groups too, their coordinates mapped onto the box
    /// (docx/local/vgp-pct, vgp-pctw, vgp-pcth, vgp-n751: rectangles where coordsize puts them in 80 % of the page's width
    /// and half its height, in modes 11, 12 and 15; PageRenderer.Stretched for groups whose text boxes fit their text).
    /// </summary>
    static XElement? RelativeSize(Dictionary<string, string> style, string dimension, bool horizontal)
    {
        if (!double.TryParse(style.GetValueOrDefault($"mso-{dimension}-percent"), NumberStyles.Float, CultureInfo.InvariantCulture, out var tenths) || tenths <= 0)
            return null;
        var from = style.GetValueOrDefault($"mso-{dimension}-relative") switch
        {
            "margin" => "margin",
            "left-margin-area" => "leftMargin",
            "right-margin-area" => "rightMargin",
            "top-margin-area" => "topMargin",
            "bottom-margin-area" => "bottomMargin",
            "inner-margin-area" => "insideMargin",
            "outer-margin-area" => "outsideMargin",
            _ => "page",
        };
        return new XElement(Drawings.Wp14 + (horizontal ? "sizeRelH" : "sizeRelV"), new XAttribute("relativeFrom", from),
            new XElement(Drawings.Wp14 + (horizontal ? "pctWidth" : "pctHeight"), (long)Math.Round(tenths * 100)));
    }

    /// <summary>What alignment counts of a turned shape beyond its box (see <see cref="TurnedBox"/>); null unless turned.</summary>
    static (double L, double T, double R, double B)? TurnedSides(XElement content, Dictionary<string, string> style, double width, double height)
    {
        var text = style.GetValueOrDefault("rotation")?.Trim();
        if (text == null || !double.TryParse(text.TrimEnd('f', 'd', 'e', 'g'), NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees))
            return null;
        if (text.EndsWith("fd", StringComparison.OrdinalIgnoreCase))
            degrees /= 65536;
        var turn = (degrees % 360 + 360) % 360;
        if (turn < 1e-6 || turn > 360 - 1e-6)
            return null;
        // The drawing's own turn and flips (DrawingML's, the turn the other way under one flip; see Xfrm).
        var xfrm = content.Elements().FirstOrDefault(e => e.Name.LocalName is "spPr" or "grpSpPr")?.Element(Ns.A + "xfrm");
        var rotation = ((double?)xfrm?.Attribute("rot") ?? 0) / 60000 * Math.PI / 180;
        var (flipH, flipV) = ((string?)xfrm?.Attribute("flipH") is "1" or "true", (string?)xfrm?.Attribute("flipV") is "1" or "true");
        IEnumerable<(double X, double Y)>? outline = null;
        var flips = true;
        if (content.Element(Drawings.Wps + "txbxWordArt") is { } art)
        {
            // WordArt by its glyphs, which its flips do not mirror.
            if ((string?)art.Attribute("warp") == "textPlain"
                && Drawings.PlainWordArt(art.Value, (string?)art.Attribute("style"), art.Attribute("trim") != null, width, height) is { } plain)
                (outline, flips) = (plain.Outline, false);
        }
        else if (content.Element(Drawings.Wps + "spPr") is { } spPr)
            outline = OutlinePoints(spPr, width, height);
        outline ??= [(0, 0), (width, 0), (width, height), (0, height)];
        var swapped = turn % 180 >= 45 && turn % 180 < 135;
        var (minX, minY) = swapped ? (-height / 2, -width / 2) : (-width / 2, -height / 2);
        var (maxX, maxY) = (-minX, -minY);
        var (cos, sin) = (Math.Cos(rotation), Math.Sin(rotation));
        foreach (var (x, y) in outline)
        {
            var (ux, uy) = (x - width / 2, y - height / 2);
            if (flips)
                (ux, uy) = (flipH ? -ux : ux, flipV ? -uy : uy);
            var (tx, ty) = (ux * cos - uy * sin, ux * sin + uy * cos);
            (minX, maxX, minY, maxY) = (Math.Min(minX, tx), Math.Max(maxX, tx), Math.Min(minY, ty), Math.Max(maxY, ty));
        }
        return (-width / 2 - minX, -height / 2 - minY, maxX - width / 2, maxY - height / 2);
    }

    /// <summary>
    /// The points of a drawing's geometry (a:prstGeom or a:custGeom in its spPr) in a box of <paramref name="width"/>
    /// by <paramref name="height"/> (pt from its top left corner): its lines' ends and its curves' control points, arcs
    /// as Béziers of up to a quarter turn; null without a geometry.
    /// </summary>
    static List<(double X, double Y)>? OutlinePoints(XElement spPr, double width, double height)
    {
        FreeSlate.Drawing.Geometry.GeometryDefinition? definition = null;
        List<FreeSlate.Drawing.Geometry.Guide>? adjustments = null;
        if (spPr.Element(Ns.A + "prstGeom") is { } preset && (string?)preset.Attribute("prst") is { } name)
        {
            definition = FreeSlate.Drawing.Geometry.PresetGeometries.Get(name);
            adjustments = preset.Element(Ns.A + "avLst")?.Elements(Ns.A + "gd")
                .Select(gd => new FreeSlate.Drawing.Geometry.Guide((string?)gd.Attribute("name") ?? "", (string?)gd.Attribute("fmla") ?? "val 0")).ToList();
        }
        else if (spPr.Element(Ns.A + "custGeom") is { } custom)
            definition = FreeSlate.Drawing.Geometry.GeometryDefinition.Parse(custom);
        if (definition == null)
            return null;
        var points = new List<(double X, double Y)>();
        foreach (var path in FreeSlate.Drawing.Geometry.GeometryEvaluator.Evaluate(definition, width * 12700, height * 12700, adjustments).Paths)
        {
            var (x, y, startX, startY) = (0.0, 0.0, 0.0, 0.0);
            foreach (var segment in path.Segments)
            {
                var v = segment.Values;
                switch (segment.Command)
                {
                    case 'M':
                        (x, y, startX, startY) = (v[0], v[1], v[0], v[1]);
                        points.Add((x, y));
                        break;
                    case 'L':
                        (x, y) = (v[0], v[1]);
                        points.Add((x, y));
                        break;
                    case 'Q' or 'C':
                        for (var i = 0; i + 1 < v.Length; i += 2)
                            points.Add((v[i], v[i + 1]));
                        (x, y) = (v[^2], v[^1]);
                        break;
                    case 'A':
                        points.AddRange(ArcPoints(x, y, v));
                        (x, y) = (v[5], v[6]);
                        break;
                    case 'Z':
                        (x, y) = (startX, startY);
                        break;
                }
            }
        }
        // Pixels to points.
        return points.Select(p => (p.X * 0.75, p.Y * 0.75)).ToList();
    }

    /// <summary>
    /// An SVG arc's Béziers' control and end points from (<paramref name="x0"/>, <paramref name="y0"/>): radii, turn,
    /// large-arc and sweep flags and end point, in pieces of up to a quarter turn.
    /// </summary>
    static IEnumerable<(double X, double Y)> ArcPoints(double x0, double y0, double[] arc)
    {
        var (rx, ry, large, sweep, x1, y1) = (arc[0], arc[1], arc[3] != 0, arc[4] != 0, arc[5], arc[6]);
        var (hx, hy) = ((x0 - x1) / 2, (y0 - y1) / 2);
        if (rx <= 0 || ry <= 0 || hx == 0 && hy == 0)
        {
            yield return (x1, y1);
            yield break;
        }
        var lambda = hx * hx / (rx * rx) + hy * hy / (ry * ry);
        if (lambda > 1)
            (rx, ry) = (rx * Math.Sqrt(lambda), ry * Math.Sqrt(lambda));
        var numerator = rx * rx * ry * ry - rx * rx * hy * hy - ry * ry * hx * hx;
        var denominator = rx * rx * hy * hy + ry * ry * hx * hx;
        var root = (large == sweep ? -1 : 1) * (denominator > 0 ? Math.Sqrt(Math.Max(0, numerator / denominator)) : 0);
        var (cx, cy) = (root * rx * hy / ry + (x0 + x1) / 2, -root * ry * hx / rx + (y0 + y1) / 2);
        var start = Math.Atan2((y0 - cy) / ry, (x0 - cx) / rx);
        var turn = Math.Atan2((y1 - cy) / ry, (x1 - cx) / rx) - start;
        if (sweep && turn < 0)
            turn += 2 * Math.PI;
        else if (!sweep && turn > 0)
            turn -= 2 * Math.PI;
        var count = Math.Max(1, (int)Math.Ceiling(Math.Abs(turn) / (Math.PI / 2) - 1e-9));
        var step = turn / count;
        var k = 4.0 / 3 * Math.Tan(step / 4);
        for (var j = 0; j < count; j++)
        {
            var (a0, a1) = (start + j * step, start + (j + 1) * step);
            yield return (cx + rx * (Math.Cos(a0) - k * Math.Sin(a0)), cy + ry * (Math.Sin(a0) + k * Math.Cos(a0)));
            yield return (cx + rx * (Math.Cos(a1) + k * Math.Sin(a1)), cy + ry * (Math.Sin(a1) - k * Math.Cos(a1)));
            yield return (cx + rx * Math.Cos(a1), cy + ry * Math.Sin(a1));
        }
    }

    /// <summary>
    /// A signature line (o:signatureline, catalog: vml.ole): Word draws it itself, whatever picture it stores, scaled
    /// from 256 × 128 pixels: on white an X in 32 px Arial 10 px in, its baseline at 68, a line 2 px thick at 75
    /// across, the suggested signer, the signer's title and e-mail in 11 px Segoe UI 10 px in, their baselines 91, 107
    /// and 123 (measured with docx/local/ole-probe, ole-sigsizes: 192 × 96, 288 × 144 and 120 × 60 pt, with and
    /// without the title and the signer); a group of its rectangles and text boxes, null for other shapes.
    /// </summary>
    static XElement? SignatureLine(XElement shape, double width, double height)
    {
        if (shape.Element(O + "signatureline") is not { } line || (string?)line.Attribute("issignatureline") is not ("t" or "true" or "1")
            || width <= 0 || height <= 0)
            return null;
        var (sx, sy) = (width / 256, height / 128);
        var w = Wml.W;
        static XElement Xfrm(double x, double y, double cx, double cy) => new(Ns.A + "xfrm",
            new XElement(Ns.A + "off", new XAttribute("x", Emu(x)), new XAttribute("y", Emu(y))),
            new XElement(Ns.A + "ext", new XAttribute("cx", Emu(Math.Max(0, cx))), new XAttribute("cy", Emu(Math.Max(0, cy)))));
        XElement Rect(double x, double y, double cx, double cy, string color) => new(Drawings.Wps + "wsp", new XElement(Drawings.Wps + "cNvSpPr"),
            new XElement(Drawings.Wps + "spPr", Xfrm(x, y, cx, cy), new XElement(Ns.A + "prstGeom", new XAttribute("prst", "rect"), new XElement(Ns.A + "avLst")),
                new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", color))), new XElement(Ns.A + "ln", new XElement(Ns.A + "noFill"))),
            new XElement(Drawings.Wps + "bodyPr"));
        // A line of text as a text box whose exact line puts the baseline where Word's is; the sizes stay exact (the
        // converted text is ours, not Word's half points).
        XElement Text(string text, string font, double size, double x, double baseline)
        {
            var halfPoints = Math.Round(Math.Max(1, size) * 2, 3).ToString(CultureInfo.InvariantCulture);
            var lineTwips = Math.Round(Math.Max(1, size) * 1.25 * 20, 2);
            var top = baseline - 0.8 * lineTwips / 20.0;
            var run = new XElement(w + "r",
                new XElement(w + "rPr", new XElement(w + "rFonts", new XAttribute(w + "ascii", font), new XAttribute(w + "hAnsi", font), new XAttribute(w + "cs", font), new XAttribute(w + "eastAsia", font)),
                    new XElement(w + "color", new XAttribute(w + "val", "000000")), new XElement(w + "sz", new XAttribute(w + "val", halfPoints)), new XElement(w + "szCs", new XAttribute(w + "val", halfPoints))),
                new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text));
            var paragraph = new XElement(w + "p",
                new XElement(w + "pPr", new XElement(w + "spacing", new XAttribute(w + "before", 0), new XAttribute(w + "after", 0), new XAttribute(w + "line", lineTwips.ToString(CultureInfo.InvariantCulture)), new XAttribute(w + "lineRule", "exact")),
                    new XElement(w + "ind", new XAttribute(w + "left", 0), new XAttribute(w + "right", 0), new XAttribute(w + "firstLine", 0)), new XElement(w + "jc", new XAttribute(w + "val", "left"))),
                run);
            return new XElement(Drawings.Wps + "wsp", new XElement(Drawings.Wps + "cNvSpPr", new XAttribute("txBox", 1)),
                new XElement(Drawings.Wps + "spPr", Xfrm(x, top, width - x, lineTwips / 20.0), new XElement(Ns.A + "prstGeom", new XAttribute("prst", "rect"), new XElement(Ns.A + "avLst")),
                    new XElement(Ns.A + "noFill"), new XElement(Ns.A + "ln", new XElement(Ns.A + "noFill"))),
                new XElement(Drawings.Wps + "txbx", new XElement(w + "txbxContent", paragraph)),
                new XElement(Drawings.Wps + "bodyPr", new XAttribute("lIns", 0), new XAttribute("tIns", 0), new XAttribute("rIns", 0), new XAttribute("bIns", 0),
                    new XAttribute("anchor", "t"), new XAttribute("wrap", "none")));
        }
        var group = new XElement(Drawings.Wpg + "wgp", new XElement(Drawings.Wpg + "cNvGrpSpPr"),
            new XElement(Drawings.Wpg + "grpSpPr", new XElement(Ns.A + "xfrm",
                new XElement(Ns.A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(Ns.A + "ext", new XAttribute("cx", Emu(width)), new XAttribute("cy", Emu(height))),
                new XElement(Ns.A + "chOff", new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(Ns.A + "chExt", new XAttribute("cx", Emu(width)), new XAttribute("cy", Emu(height))))),
            Rect(0, 0, width, height, "FFFFFF"), Rect(0, 75 * sy, width, 2 * sy, "000000"), Text("X", "Arial", 32 * sy, 10 * sx, 68 * sy));
        var baseline = 91.0;
        foreach (var name in new[] { "suggestedsigner", "suggestedsigner2", "suggestedsigneremail" })
        {
            if (((string?)line.Attribute(O + name) ?? (string?)line.Attribute(name)) is { Length: > 0 } value)
                group.Add(Text(value, "Segoe UI", 11 * sy, 10 * sx, baseline * sy));
            baseline += 16;
        }
        return group;
    }

    /// <summary>
    /// The room an inline shape's outline and shadow take right of and below it (catalog: vml.shapes): Word lays out
    /// an outlined shape in whole pixels of 0.75 pt, its width rounded up, the outline's half on either side and the
    /// shadow's offset, its height rounded up, the outline and the shadow's offset, the shape at the top left of that
    /// box (measured with docx/local/vml-inline3, vml-inline4: outlines of 0.25 to 12 pt around 60 × 40, 61 × 41 and
    /// 60.5 × 40.5 pt; docx/local/vml-fx: shadows 4 pt, -6 pt and doubled 3 and 6 pt off); a shadow alone takes its
    /// offset (docx/local/vml-inline5: within 0.3 pt); none without either.
    /// </summary>
    static (double Right, double Bottom) Room(XElement shape, XElement? type, XElement content, double width, double height)
    {
        var line = content.Element(Drawings.Wps + "spPr")?.Element(Ns.A + "ln") ?? content.Element(Drawings.Pic + "spPr")?.Element(Ns.A + "ln");
        var outline = line != null && line.Element(Ns.A + "noFill") == null
            && double.TryParse((string?)line.Attribute("w"), NumberStyles.Float, CultureInfo.InvariantCulture, out var emu) ? Math.Max(0, emu / EmuPerPoint) : 0;
        var (dx, dy) = (0.0, 0.0);
        const double Pixel = 0.75;
        static double Up(double v) => Math.Ceiling(v - 1e-6);
        if (ShadowOf(shape, type) is { } shadow)
        {
            var (x, y) = ShadowOffset((string?)shadow.Attribute("offset"), (2, 2));
            (dx, dy) = (Math.Abs(x), Math.Abs(y));
            if ((string?)shadow.Attribute("type") == "double")
            {
                var (x2, y2) = ShadowOffset((string?)shadow.Attribute("offset2"), (-2, -2));
                (dx, dy) = (Math.Max(dx, Math.Abs(x2)), Math.Max(dy, Math.Abs(y2)));
            }
            // A perspective shadow takes the room of its box and the shape's together, after the shape and on whole
            // pixels across, what it reaches out on the left too (measured with docx/local/vps-*: a 100 by 20 pt shape
            // whose shadow is skewed by 0.7071 and halved about a bottom corner, 2 pt off, puts the text after it
            // 114.75 pt on, the shadow 12.14 pt left of the shape; skewed the other way 116.25 pt; LibreOffice's
            // extras-ooxmlexport/fdo78300).
            if (Perspective(shadow) is { } skew)
            {
                var (ox, oy) = ((0.5 + skew.OriginX) * width, (0.5 + skew.OriginY) * height);
                var corners = new[] { (0.0, 0.0), (width, 0.0), (0.0, height), (width, height) }
                    .Select(c => (X: ox + skew.Xx * (c.Item1 - ox) + skew.Xy * (c.Item2 - oy) + x, Y: oy + skew.Yx * (c.Item1 - ox) + skew.Yy * (c.Item2 - oy) + y))
                    .ToList();
                var across = Math.Max(width, corners.Max(c => c.X)) - Math.Min(0, corners.Min(c => c.X));
                var down = Math.Max(height, corners.Max(c => c.Y)) - Math.Min(0, corners.Min(c => c.Y));
                (dx, dy) = (Up(across / Pixel) * Pixel - width, down - height);
            }
        }
        if (outline <= 0)
            return (dx, dy);
        return ((Up(width / Pixel) + 2 * Up(outline / Pixel / 2) + Up(dx / Pixel)) * Pixel - width,
            (Up(height / Pixel) + Up(outline / Pixel) + Up(dy / Pixel)) * Pixel - height);
    }

    /// <summary>A shape's shadow (v:shadow) when it is on, else null.</summary>
    static XElement? ShadowOf(XElement shape, XElement? type) =>
        (shape.Element(V + "shadow") ?? type?.Element(V + "shadow")) is { } shadow && (string?)shadow.Attribute("on") is "t" or "true" or "1" or "on" ? shadow : null;

    /// <summary>
    /// A shadow's offset ("4pt,4pt") in points, one value across only, down the fallback's (docx/local/vsh-one: "4pt"
    /// 4 pt right and 2 pt down; docx/external/libreoffice/extras-ooxmlexport/n705956-2).
    /// </summary>
    static (double X, double Y) ShadowOffset(string? offset, (double X, double Y) fallback) =>
        offset == null ? fallback : offset.Contains(',') ? Point(offset) : (Point(offset + ",0").X, fallback.Y);

    /// <summary>
    /// A double shadow (v:shadow type="double", catalog: vml.shapes): Word draws the shape, fill and outline, in
    /// color2 at offset2 behind the first shadow (measured with docx/local/vml-fx: #c00000 6 pt off, then the gray one 3
    /// pt off, then the shape); a group of that copy and the shape, null for other shadows.
    /// </summary>
    static XElement? DoubleShadow(XElement content, XElement shape, XElement? type, double width, double height)
    {
        if (ShadowOf(shape, type) is not { } shadow || (string?)shadow.Attribute("type") != "double" || content.Name != Drawings.Wps + "wsp"
            || content.Element(Drawings.Wps + "spPr") is not { } properties)
            return null;
        var (x, y) = ShadowOffset((string?)shadow.Attribute("offset2"), (-2, -2));
        var color = Color((string?)shadow.Attribute("color2")) ?? "CBCBCB";
        XElement Solid() => new(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", color)));
        var copy = new XElement(Drawings.Wps + "spPr");
        foreach (var child in properties.Elements())
        {
            var name = child.Name.LocalName;
            if (name == "xfrm")
            {
                var xfrm = new XElement(child);
                xfrm.Element(Ns.A + "off")?.ReplaceWith(new XElement(Ns.A + "off", new XAttribute("x", Emu(x)), new XAttribute("y", Emu(y))));
                copy.Add(xfrm);
            }
            else if (name is "prstGeom" or "custGeom")
                copy.Add(new XElement(child));
            else if (name is "solidFill" or "gradFill" or "blipFill" or "pattFill")
                copy.Add(Solid());
            else if (name == "noFill")
                copy.Add(new XElement(child));
            else if (name == "ln")
            {
                var ln = new XElement(Ns.A + "ln", child.Attributes());
                ln.Add(child.Element(Ns.A + "noFill") != null ? new XElement(Ns.A + "noFill") : Solid());
                copy.Add(ln);
            }
        }
        var group = new XElement(Drawings.Wpg + "wgp", new XElement(Drawings.Wpg + "cNvGrpSpPr"),
            new XElement(Drawings.Wpg + "grpSpPr", new XElement(Ns.A + "xfrm",
                new XElement(Ns.A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(Ns.A + "ext", new XAttribute("cx", Emu(width)), new XAttribute("cy", Emu(height))),
                new XElement(Ns.A + "chOff", new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(Ns.A + "chExt", new XAttribute("cx", Emu(width)), new XAttribute("cy", Emu(height))))));
        group.Add(new XElement(Drawings.Wps + "wsp", new XElement(Drawings.Wps + "cNvSpPr"), copy, new XElement(Drawings.Wps + "bodyPr")));
        group.Add(new XElement(content));
        return group;
    }

    /// <summary>
    /// How far the geometry of a converted group's shapes (a wpg:wgp of <paramref name="width"/> by
    /// <paramref name="height"/> pt) reaches beyond its box on each side, in points: Word sets an inline group in by
    /// what reaches out on the left and gives it room for it (Word's PDF of LibreOffice's extras-ooxmlexport/fdo73215,
    /// found by agent BK: a canvas whose elbow connector loops out on the left stands 9.75 pt right of the indent, the
    /// connector's line from 0.25 pt right of it; Word's connector, one unit of 0.0525 pt wide taken as a twip, loops
    /// out 9 pt, ours by its geometry 9.92 pt; above, right and below alike, not measured).
    /// </summary>
    static (double L, double T, double R, double B) Overhang(XElement group, double width, double height)
    {
        var (minX, minY, maxX, maxY) = (0.0, 0.0, width, height);
        foreach (var (_, figures) in Outlines(group, width, height))
        {
            foreach (var (x, y) in figures.SelectMany(f => f.Points))
                (minX, minY, maxX, maxY) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y));
        }
        return (Math.Max(0, -minX), Math.Max(0, -minY), Math.Max(0, maxX - width), Math.Max(0, maxY - height));
    }

    /// <summary>
    /// How far the lines of a converted shape or group (a wps:wsp, pic:pic or wpg:wgp of <paramref name="width"/> by
    /// <paramref name="height"/> pt) reach beyond its box on each side as Word counts them (see LineReachBox), in
    /// points; null where none of its shapes has a line.
    /// </summary>
    static (double L, double T, double R, double B)? Reach(XElement content, double width, double height)
    {
        var (minX, minY, maxX, maxY) = (0.0, 0.0, width, height);
        void Add(double x, double y) => (minX, minY, maxX, maxY) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y));
        var any = false;
        foreach (var (properties, figures) in Outlines(content, width, height))
        {
            if (properties.Element(Ns.A + "ln") is not { } ln || ln.Element(Ns.A + "noFill") != null)
                continue;
            var half = Math.Max(0.75, ((double?)ln.Attribute("w") ?? 9525) / 12700) / 2;
            var limit = 2 * ((double?)ln.Attribute(MiterLimit) ?? 8);
            foreach (var (outline, closed, stroked) in figures)
            {
                if (stroked && LineCorners(outline, closed, half, limit, Add))
                    any = true;
            }
        }
        return any ? (Math.Max(0, -minX), Math.Max(0, -minY), Math.Max(0, maxX - width), Math.Max(0, maxY - height)) : null;
    }

    /// <summary>
    /// The points a line of half width <paramref name="half"/> along an outline reaches as Word counts them (see
    /// LineReachBox), each given to <paramref name="add"/>; false where the outline has fewer than two points.
    /// </summary>
    static bool LineCorners(IEnumerable<(double X, double Y)> outline, bool closed, double half, double limit, Action<double, double> add)
    {
        static (double X, double Y) Unit((double X, double Y) from, (double X, double Y) to)
        {
            var length = Math.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Y - from.Y) * (to.Y - from.Y));
            return ((to.X - from.X) / length, (to.Y - from.Y) / length);
        }
        // The corners where the outline turns, repeated points left out.
        var points = new List<(double X, double Y)>();
        foreach (var p in outline)
        {
            if (points.Count == 0 || Math.Abs(p.X - points[^1].X) > 1e-9 || Math.Abs(p.Y - points[^1].Y) > 1e-9)
                points.Add(p);
        }
        if (closed && points.Count > 1 && Math.Abs(points[0].X - points[^1].X) <= 1e-9 && Math.Abs(points[0].Y - points[^1].Y) <= 1e-9)
            points.RemoveAt(points.Count - 1);
        var n = points.Count;
        if (n < 2)
            return false;
        for (var i = 0; i < n; i++)
        {
            var b = points[i];
            var (hasBefore, hasAfter) = (closed || i > 0, closed || i < n - 1);
            var u1 = hasBefore ? Unit(points[(i - 1 + n) % n], b) : Unit(b, points[(i + 1) % n]);
            var u2 = hasAfter ? Unit(b, points[(i + 1) % n]) : u1;
            // The line's sides at the corner, then its miter where it turns.
            foreach (var (ux, uy) in new[] { u1, u2 })
            {
                add(b.X - uy * half, b.Y + ux * half);
                add(b.X + uy * half, b.Y - ux * half);
            }
            var cross = u1.X * u2.Y - u1.Y * u2.X;
            var dot = u1.X * u2.X + u1.Y * u2.Y;
            if (!hasBefore || !hasAfter || Math.Abs(cross) < 1e-12 || dot <= -1 + 1e-12)
                continue;
            var k = (cross > 0 ? -1 : 1) * half / (1 + dot);
            var (dx, dy) = (k * (-u1.Y - u2.Y), k * (u1.X + u2.X));
            var miter = Math.Sqrt(dx * dx + dy * dy);
            if (miter > limit * half)
                add(b.X + dx / miter * half, b.Y + dy / miter * half);
            else
                add(b.X + dx + (Math.Abs(dx) > half * 1.001 ? Math.Sign(dx) * half : 0), b.Y + dy + (Math.Abs(dy) > half * 1.001 ? Math.Sign(dy) * half : 0));
        }
        return true;
    }

    /// <summary>
    /// The room inline WordArt along a path (an arch) takes beyond its shape, left, above, right and below (pt), as Word
    /// lays it out: its glyphs' ink, across with an outline's half (at least 0.75 pt's) out from its corners as
    /// LineReachBox counts them, up and down with the whole outline, rounded out to whole pixels of 0.75 pt (measured
    /// with docx/local/vwa-archin, vwb-ab-in, vwc-stroke-in, vwd-*: "WORD-ART" and "HOH" along arches of 286.45 by
    /// 134.8, 200 by 200 and 400 by 100 pt, unstroked and outlined 0.25 to 6 pt, at the line's start: the text after it
    /// at the room's right, the next line below the shape, the glyphs' tops the line's top within 0.17 pt); null
    /// for other shapes.
    /// </summary>
    static (double L, double T, double R, double B)? WordArtRoom(XElement content, double width, double height)
    {
        if (content.Element(Drawings.Wps + "txbxWordArt") is not { } art || Drawings.PathWordArt(art, width, height) is not { } glyphs)
            return null;
        var ln = content.Element(Drawings.Wps + "spPr")?.Element(Ns.A + "ln");
        var line = ln != null && ln.Element(Ns.A + "noFill") == null ? Math.Max(0.75, ((double?)ln.Attribute("w") ?? 9525) / 12700) : 0;
        var (minX, minY, maxX, maxY) = (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
        foreach (var (x, y) in glyphs.SelectMany(g => g))
            (minX, minY, maxX, maxY) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y));
        if (minX > maxX)
            return null;
        if (line > 0)
        {
            var limit = 2 * ((double?)ln!.Attribute(MiterLimit) ?? 8);
            foreach (var contour in glyphs)
                LineCorners(contour, true, line / 2, limit, (x, _) => (minX, maxX) = (Math.Min(minX, x), Math.Max(maxX, x)));
            (minY, maxY) = (minY - line, maxY + line);
        }
        const double Pixel = 0.75;
        static double Out(double v) => v > 0 ? Math.Ceiling(v / Pixel - 1e-6) * Pixel : 0;
        return (Out(-minX), Out(-minY), Out(maxX - width), Out(maxY - height));
    }

    /// <summary>
    /// The outlines of a converted shape or group (a wps:wsp, pic:pic or wpg:wgp of <paramref name="width"/> by
    /// <paramref name="height"/> pt), by shape: its properties (spPr) and its paths' figures flattened in points of the
    /// box, closed or open, stroked or not.
    /// </summary>
    static List<(XElement Properties, List<(List<(double X, double Y)> Points, bool Closed, bool Stroked)> Figures)> Outlines(XElement content, double width, double height)
    {
        static double Number(XElement? e, string name) =>
            double.TryParse((string?)e?.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
        var shapes = new List<(XElement, List<(List<(double X, double Y)>, bool, bool)>)>();
        // A group's shapes, their points mapped to the outermost group's box by toTop.
        void Walk(IEnumerable<XElement> children, Func<double, double, (double X, double Y)> toTop)
        {
            foreach (var child in children)
            {
                var properties = child.Element(Drawings.Wps + "spPr") ?? child.Element(Drawings.Pic + "spPr") ?? child.Element(Drawings.Wpg + "grpSpPr");
                if (properties?.Element(Ns.A + "xfrm") is not { } xfrm)
                    continue;
                var (offX, offY) = (Number(xfrm.Element(Ns.A + "off"), "x"), Number(xfrm.Element(Ns.A + "off"), "y"));
                var (extX, extY) = (Number(xfrm.Element(Ns.A + "ext"), "cx"), Number(xfrm.Element(Ns.A + "ext"), "cy"));
                var (sin, cos) = Math.SinCos(Number(xfrm, "rot") / 60000 * Math.PI / 180);
                var (flipX, flipY) = ((string?)xfrm.Attribute("flipH") is "1" or "true", (string?)xfrm.Attribute("flipV") is "1" or "true");
                // A point of the child's box: flipped and turned about its middle, as DrawingML does.
                (double X, double Y) Place(double x, double y)
                {
                    var (dx, dy) = ((flipX ? extX - x : x) - extX / 2, (flipY ? extY - y : y) - extY / 2);
                    return toTop(offX + extX / 2 + dx * cos - dy * sin, offY + extY / 2 + dx * sin + dy * cos);
                }
                if (child.Name == Drawings.Wpg + "grpSp")
                {
                    var (chOffX, chOffY) = (Number(xfrm.Element(Ns.A + "chOff"), "x"), Number(xfrm.Element(Ns.A + "chOff"), "y"));
                    var (chExtX, chExtY) = (Number(xfrm.Element(Ns.A + "chExt"), "cx"), Number(xfrm.Element(Ns.A + "chExt"), "cy"));
                    if (chExtX > 0 && chExtY > 0)
                        Walk(child.Elements(), (x, y) => Place((x - chOffX) * extX / chExtX, (y - chOffY) * extY / chExtY));
                    continue;
                }
                FreeSlate.Drawing.Geometry.GeometryDefinition? definition = null;
                List<FreeSlate.Drawing.Geometry.Guide>? adjustments = null;
                if (properties.Element(Ns.A + "prstGeom") is { } preset)
                {
                    definition = FreeSlate.Drawing.Geometry.PresetGeometries.Get((string?)preset.Attribute("prst") ?? "rect");
                    adjustments = preset.Element(Ns.A + "avLst")?.Elements(Ns.A + "gd")
                        .Select(gd => new FreeSlate.Drawing.Geometry.Guide((string?)gd.Attribute("name") ?? "", (string?)gd.Attribute("fmla") ?? "val 0")).ToList();
                }
                else if (properties.Element(Ns.A + "custGeom") is { } custom)
                    definition = FreeSlate.Drawing.Geometry.GeometryDefinition.Parse(custom);
                if (definition == null)
                    continue;
                // The evaluator's pixels are its EMU / 9525: the box in 9525ths gives the points in the child's units.
                var figures = new List<(List<(double X, double Y)>, bool, bool)>();
                foreach (var path in FreeSlate.Drawing.Geometry.GeometryEvaluator.Evaluate(definition, extX * 9525, extY * 9525, adjustments).Paths)
                {
                    foreach (var (points, closed) in FreeSlate.Html.CompoundLines.Flatten(path.Segments))
                        figures.Add((points.Select(p => Place(p.X, p.Y)).ToList(), closed, path.Stroke));
                }
                shapes.Add((properties, figures));
            }
        }
        if (content.Name == Drawings.Wpg + "wgp")
        {
            if (content.Element(Drawings.Wpg + "grpSpPr")?.Element(Ns.A + "xfrm") is { } outer
                && Number(outer.Element(Ns.A + "chExt"), "cx") is > 0 and var chW && Number(outer.Element(Ns.A + "chExt"), "cy") is > 0 and var chH)
            {
                var (chX, chY) = (Number(outer.Element(Ns.A + "chOff"), "x"), Number(outer.Element(Ns.A + "chOff"), "y"));
                Walk(content.Elements(), (x, y) => ((x - chX) * width / chW, (y - chY) * height / chH));
            }
        }
        else if (content.Elements().FirstOrDefault(e => e.Name.LocalName == "spPr")?.Element(Ns.A + "xfrm") is { } own
                 && Number(own.Element(Ns.A + "ext"), "cx") is > 0 and var cx && Number(own.Element(Ns.A + "ext"), "cy") is > 0 and var cy)
        {
            var (x0, y0) = (Number(own.Element(Ns.A + "off"), "x"), Number(own.Element(Ns.A + "off"), "y"));
            Walk([content], (x, y) => ((x - x0) * width / cx, (y - y0) * height / cy));
        }
        return shapes;
    }

    /// <summary>
    /// A VML group (v:group, catalog: vml.shapes) as a DrawingML group: its children's coordinates (coordorigin,
    /// coordsize) mapped onto its box, the children's left, top, width and height (or a line's from and to) in them;
    /// groups nest.
    /// </summary>
    static XElement Group(XElement group, Dictionary<string, XElement> types, double width, double height, bool top, (long X, long Y, long W, long H)? place = null)
    {
        // A value may leave either number out, which then is the default's (docx/external/libreoffice/
        // extras-ooxmlexport/vml-text-vertical-adjust: an inner group's coordorigin "9201", its y 0; docx/local/vgp-pct-org,
        // extras-ooxmlimport/n751077: ",1440" begins at x 0).
        static (long A, long B) Pair(string? value, (long A, long B) fallback)
        {
            var parts = (value ?? "").Split(',');
            var a = fallback.A;
            if (parts.Length is < 1 or > 2 || parts[0].Trim().Length > 0 && !long.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out a))
                return fallback;
            if (parts.Length == 1 || parts[1].Trim().Length == 0)
                return (a, fallback.B);
            return long.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? (a, b) : fallback;
        }
        static long Raw(Dictionary<string, string> style, string name) =>
            double.TryParse(style.GetValueOrDefault(name)?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? (long)Math.Round(v) : 0;
        var (ox, oy) = Pair((string?)group.Attribute("coordorigin"), (0, 0));
        var (cw, ch) = Pair((string?)group.Attribute("coordsize"), (1000, 1000));
        var (x, y, w, h) = place ?? (0, 0, Emu(width), Emu(height));
        var xfrm = new XElement(Ns.A + "xfrm",
            new XElement(Ns.A + "off", new XAttribute("x", x), new XAttribute("y", y)),
            new XElement(Ns.A + "ext", new XAttribute("cx", w), new XAttribute("cy", h)),
            new XElement(Ns.A + "chOff", new XAttribute("x", ox), new XAttribute("y", oy)),
            new XElement(Ns.A + "chExt", new XAttribute("cx", cw), new XAttribute("cy", ch)));
        // A group turns and flips as a whole about its box's middle, as shapes do (docx/external/libreoffice/
        // extras-ooxmlexport/groupshape-rotation: a group turned 45°, its triangle's corners within 0.1 pt).
        foreach (var turn in Xfrm(0, 0, Style((string?)group.Attribute("style"))).Attributes())
            xfrm.SetAttributeValue(turn.Name, turn.Value);
        var element = top
            ? new XElement(Drawings.Wpg + "wgp", new XElement(Drawings.Wpg + "cNvGrpSpPr"), new XElement(Drawings.Wpg + "grpSpPr", xfrm))
            : new XElement(Drawings.Wpg + "grpSp", new XElement(Drawings.Wpg + "cNvGrpSpPr"), new XElement(Drawings.Wpg + "grpSpPr", xfrm));
        foreach (var child in group.Elements())
        {
            if (child.Name.Namespace != V || child.Name.LocalName == "shapetype")
                continue;
            var style = Style((string?)child.Attribute("style"));
            if (style.GetValueOrDefault("visibility") == "hidden")
                continue;
            var (cx, cy, cwidth, cheight) = (Raw(style, "left"), Raw(style, "top"), Raw(style, "width"), Raw(style, "height"));
            if (child.Name == V + "line")
            {
                var (from, to) = (Pair((string?)child.Attribute("from"), (0, 0)), Pair((string?)child.Attribute("to"), (0, 0)));
                (cx, cy, cwidth, cheight) = (Math.Min(from.A, to.A), Math.Min(from.B, to.B), Math.Abs(to.A - from.A), Math.Abs(to.B - from.B));
            }
            if (child.Name == V + "group")
            {
                element.Add(Group(child, types, 0, 0, top: false, (cx, cy, cwidth, cheight)));
                continue;
            }
            var type = child.Name == V + "shape" && (string?)child.Attribute("type") is { } reference && types.TryGetValue(reference.TrimStart('#'), out var t) ? t : null;
            // The child's size in points, for what is measured in points (insets, line widths) to stay in scale.
            var scale = cw > 0 ? (double)w / cw / EmuPerPoint : 1;
            if (Content(child, type, cwidth * scale, cheight * scale) is not { } content)
                continue;
            if (content.Descendants(Ns.A + "xfrm").FirstOrDefault() is { } own)
            {
                own.Element(Ns.A + "off")?.SetAttributeValue("x", cx);
                own.Element(Ns.A + "off")?.SetAttributeValue("y", cy);
                own.Element(Ns.A + "ext")?.SetAttributeValue("cx", cwidth);
                own.Element(Ns.A + "ext")?.SetAttributeValue("cy", cheight);
            }
            element.Add(content);
        }
        return element;
    }

    /// <summary>
    /// A shape with Word 97 borders (w10:bordertop, borderleft, borderbottom, borderright; catalog: vml.shapes) as a
    /// group of it and its borders: lines around the shape, which grows by them, width/8 pt each floored to Word's
    /// 0.12 pt dots; double lines line, gap and line, triple five times; dotted and dashed ones dashed (measured with
    /// docx/local/vml-border: single 1 and 3 pt, double 1.5 pt, dotted 1 pt and top and left only).
    /// </summary>
    static (XElement Group, double Width, double Height)? Bordered(XElement content, XElement shape, double width, double height)
    {
        (string Type, double Width)? Side(string name) =>
            shape.Element(W10 + "border" + name) is { } b && (string?)b.Attribute("type") is { } kind && kind != "none"
            && double.TryParse((string?)b.Attribute("width"), NumberStyles.Float, CultureInfo.InvariantCulture, out var w) && w > 0
                ? (kind, WordDevice.LineWidth(w / 8)) : null;
        var (top, left, bottom, right) = (Side("top"), Side("left"), Side("bottom"), Side("right"));
        if (top == null && left == null && bottom == null && right == null)
            return null;
        static double Thickness((string Type, double Width)? side) => side is not { } s ? 0
            : s.Type switch { "double" => 3 * s.Width, "triple" => 5 * s.Width, _ => s.Width };
        var (t, l, b, r) = (Thickness(top), Thickness(left), Thickness(bottom), Thickness(right));
        var (total, totalHeight) = (l + width + r, t + height + b);
        static XElement Xfrm(double x, double y, double w, double h) => new(Ns.A + "xfrm",
            new XElement(Ns.A + "off", new XAttribute("x", Emu(x)), new XAttribute("y", Emu(y))),
            new XElement(Ns.A + "ext", new XAttribute("cx", Emu(Math.Max(0, w))), new XAttribute("cy", Emu(Math.Max(0, h)))));
        var group = new XElement(Drawings.Wpg + "wgp", new XElement(Drawings.Wpg + "cNvGrpSpPr"),
            new XElement(Drawings.Wpg + "grpSpPr", new XElement(Ns.A + "xfrm",
                new XElement(Ns.A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(Ns.A + "ext", new XAttribute("cx", Emu(total)), new XAttribute("cy", Emu(totalHeight))),
                new XElement(Ns.A + "chOff", new XAttribute("x", 0), new XAttribute("y", 0)),
                new XElement(Ns.A + "chExt", new XAttribute("cx", Emu(total)), new XAttribute("cy", Emu(totalHeight))))));
        // The shape itself, inside its borders.
        var own = new XElement(content);
        if (own.Descendants(Ns.A + "xfrm").FirstOrDefault() is { } xfrm)
            xfrm.ReplaceWith(Xfrm(l, t, width, height));
        group.Add(own);
        XElement Bar(double x, double y, double w, double h, string? dash = null)
        {
            if (dash == null)
                return new XElement(Drawings.Wps + "wsp", new XElement(Drawings.Wps + "cNvSpPr"),
                    new XElement(Drawings.Wps + "spPr", Xfrm(x, y, w, h), new XElement(Ns.A + "prstGeom", new XAttribute("prst", "rect"), new XElement(Ns.A + "avLst")),
                        new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", "000000"))),
                        new XElement(Ns.A + "ln", new XElement(Ns.A + "noFill"))),
                    new XElement(Drawings.Wps + "bodyPr"));
            // A dashed line along the bar's middle, as wide as the bar is thin.
            var horizontal = w >= h;
            var (x1, y1, x2, y2) = horizontal ? (x, y + h / 2, x + w, y + h / 2) : (x + w / 2, y, x + w / 2, y + h);
            return new XElement(Drawings.Wps + "wsp", new XElement(Drawings.Wps + "cNvSpPr"),
                new XElement(Drawings.Wps + "spPr", Xfrm(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1)),
                    new XElement(Ns.A + "prstGeom", new XAttribute("prst", "line"), new XElement(Ns.A + "avLst")),
                    new XElement(Ns.A + "noFill"),
                    new XElement(Ns.A + "ln", new XAttribute("w", Emu(horizontal ? h : w)),
                        new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", "000000"))),
                        new XElement(Ns.A + "prstDash", new XAttribute("val", dash)))),
                new XElement(Drawings.Wps + "bodyPr"));
        }
        // Each side in rings from the outside in: a single line one, a double line two, a triple line three.
        void Draw((string Type, double Width)? side, string which)
        {
            if (side is not { } s)
                return;
            var rings = s.Type switch { "double" => 2, "triple" => 3, _ => 1 };
            var dash = s.Type switch { "dot" or "dotted" => "sysDot", "dash" or "dashed" or "dashSmallGap" => "dash", "dotDash" => "dashDot", "dotDotDash" => "sysDashDotDot", _ => null };
            for (var k = 0; k < rings; k++)
            {
                var o = 2 * k * s.Width;
                group.Add(which switch
                {
                    "top" => Bar(left != null ? o : 0, o, total - (left != null ? o : 0) - (right != null ? o : 0), s.Width, dash),
                    "bottom" => Bar(left != null ? o : 0, totalHeight - o - s.Width, total - (left != null ? o : 0) - (right != null ? o : 0), s.Width, dash),
                    "left" => Bar(o, top != null ? o : 0, s.Width, totalHeight - (top != null ? o : 0) - (bottom != null ? o : 0), dash),
                    _ => Bar(total - o - s.Width, top != null ? o : 0, s.Width, totalHeight - (top != null ? o : 0) - (bottom != null ? o : 0), dash),
                });
            }
        }
        Draw(top, "top");
        Draw(bottom, "bottom");
        Draw(left, "left");
        Draw(right, "right");
        return (group, total, totalHeight);
    }

    static XElement Position(string name, string? relative, string? align, double offset, bool horizontal)
    {
        var from = (horizontal, relative) switch
        {
            (true, "page") => "page",
            (true, "margin") => "margin",
            (true, "char") => "character",
            (true, "left-margin-area") => "leftMargin",
            (true, "right-margin-area") => "rightMargin",
            (true, "inner-margin-area") => "insideMargin",
            (true, "outer-margin-area") => "outsideMargin",
            (true, _) => "column",
            (false, "page") => "page",
            (false, "margin") => "margin",
            (false, "line") => "line",
            (false, "top-margin-area") => "topMargin",
            (false, "bottom-margin-area") => "bottomMargin",
            (false, "inner-margin-area") => "insideMargin",
            (false, "outer-margin-area") => "outsideMargin",
            _ => "paragraph",
        };
        var position = new XElement(Drawings.Wp + name, new XAttribute("relativeFrom", from));
        if (align is "left" or "right" or "center" or "inside" or "outside" or "top" or "bottom")
            position.Add(new XElement(Drawings.Wp + "align", align));
        else
            position.Add(new XElement(Drawings.Wp + "posOffset", Emu(offset)));
        return position;
    }

    static XElement Wrap(XElement? wrap) => (string?)wrap?.Attribute("type") switch
    {
        "square" => new XElement(Drawings.Wp + "wrapSquare", new XAttribute("wrapText", Side(wrap))),
        "tight" => new XElement(Drawings.Wp + "wrapTight", new XAttribute("wrapText", Side(wrap))),
        "through" => new XElement(Drawings.Wp + "wrapThrough", new XAttribute("wrapText", Side(wrap))),
        "topAndBottom" => new XElement(Drawings.Wp + "wrapTopAndBottom"),
        _ => new XElement(Drawings.Wp + "wrapNone"),
    };

    static string Side(XElement? wrap) => (string?)wrap?.Attribute("side") switch { "left" => "left", "right" => "right", "largest" => "largest", _ => "bothSides" };

    /// <summary>
    /// A picture's own color adjustments (v:imagedata grayscale, bilevel, gain and blacklevel) as DrawingML blip
    /// effects: gray, black and white, and luminance with a contrast of gain − 1 (1 − 1/gain above 1) and a
    /// brightness of twice the black level (measured: docx/external/libreoffice/watermark, a logo washed out with
    /// gain 19661f and blacklevel 22938f prints black as 205 and 89 as 232, contrast −70 %, brightness 70 %).
    /// </summary>
    static IEnumerable<XElement> PictureColor(XElement image)
    {
        static double? Number(string? value) =>
            value is not { Length: > 0 } ? null
            : value.EndsWith('f') && double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var fixedPoint) ? fixedPoint / 65536
            : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var plain) ? plain : null;
        if ((string?)image.Attribute("grayscale") is "t" or "true")
            yield return new XElement(Ns.A + "grayscl");
        if ((string?)image.Attribute("bilevel") is "t" or "true")
            yield return new XElement(Ns.A + "biLevel", new XAttribute("thresh", 50000));
        var gain = Number((string?)image.Attribute("gain")) ?? 1;
        var black = Number((string?)image.Attribute("blacklevel")) ?? 0;
        if (Math.Abs(gain - 1) < 1e-6 && Math.Abs(black) < 1e-6)
            yield break;
        var contrast = gain <= 1 ? gain - 1 : 1 - 1 / gain;
        yield return new XElement(Ns.A + "lum",
            new XAttribute("bright", (long)Math.Round(Math.Clamp(2 * black, -1, 1) * 100000)),
            new XAttribute("contrast", (long)Math.Round(Math.Clamp(contrast, -1, 1) * 100000)));
    }

    /// <summary>The chains of linked VML text boxes of a part: each box's chain (its first box's id) and place in it.</summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XDocument, Dictionary<string, (string Id, int Seq)>> Chains = new();

    /// <summary>A text box's place in its chain of linked boxes (mso-next-textbox), or null where it is in none.</summary>
    static (string Id, int Seq)? Linked(XElement shape)
    {
        if (shape.Document is not { } document || (string?)shape.Attribute("id") is not { } own)
            return null;
        var chains = Chains.GetValue(document, part =>
        {
            var next = new Dictionary<string, string>();
            foreach (var box in part.Descendants(V + "textbox"))
            {
                if (box.Parent is { } owner && (string?)owner.Attribute("id") is { } id
                    && Style((string?)box.Attribute("style")).GetValueOrDefault("mso-next-textbox") is { } target
                    && target.Trim().TrimStart('#') is { Length: > 0 } to && to != id)
                    next.TryAdd(id, to);
            }
            var followers = next.Values.ToHashSet();
            var places = new Dictionary<string, (string Id, int Seq)>();
            foreach (var head in next.Keys.Where(id => !followers.Contains(id)))
            {
                var (at, seq) = (head, 0);
                while (places.TryAdd(at, (head, seq)) && next.TryGetValue(at, out var to))
                    (at, seq) = (to, seq + 1);
            }
            return places;
        });
        return chains.TryGetValue(own, out var place) ? place : null;
    }

    /// <summary>The DrawingML content of a shape: a picture for pictures alone, else a shape.</summary>
    static XElement? Content(XElement shape, XElement? type, double width, double height)
    {
        var image = shape.Element(V + "imagedata") ?? type?.Element(V + "imagedata");
        var spt = (string?)shape.Attribute(O + "spt") ?? (string?)type?.Attribute(O + "spt");
        var filled = Flag(shape, "filled", type, true);
        // v:stroke on="f" turns the outline off as stroked="f" does (docx/external/tika/testWPSAttachment: pictures
        // of embedded objects with both, printed without a frame).
        var strokeElement = shape.Element(V + "stroke") ?? type?.Element(V + "stroke");
        var stroked = Flag(shape, "stroked", type, true) && (string?)strokeElement?.Attribute("on") is not ("f" or "false" or "0");
        var xfrm = Xfrm(width, height, Style((string?)shape.Attribute("style")));
        if (image != null && ((string?)image.Attribute(R + "id") ?? (string?)image.Attribute(O + "relid")) is { } relationship)
        {
            // A picture: a frame (t75) or any shape whose image fills it.
            var blipFill = new XElement(Drawings.Pic + "blipFill", new XElement(Ns.A + "blip", new XAttribute(R + "embed", relationship), PictureColor(image)));
            var crop = Crop(image);
            if (crop != null)
                blipFill.Add(crop);
            blipFill.Add(new XElement(Ns.A + "stretch", new XElement(Ns.A + "fillRect")));
            var spPr = new XElement(Drawings.Pic + "spPr", xfrm, new XElement(Ns.A + "prstGeom", new XAttribute("prst", "rect"), new XElement(Ns.A + "avLst")));
            // A picture filled on purpose (filled="t", its own or its type's) has its fill behind it (docx/external/
            // libreoffice/extras-ooxmlexport/tdf135653: an icon on red, its frame type unfilled).
            if (Flag(shape, "filled", type, false))
                spPr.Add(Fill(shape, type));
            if (stroked && (shape.Attribute("strokecolor") != null || shape.Element(V + "stroke") != null))
                spPr.Add(Line(shape, type));
            return new XElement(Drawings.Pic + "pic",
                new XElement(Drawings.Pic + "nvPicPr", new XElement(Drawings.Pic + "cNvPr", new XAttribute("id", 0), new XAttribute("name", "")), new XElement(Drawings.Pic + "cNvPicPr")),
                blipFill, spPr);
        }

        var properties = new XElement(Drawings.Wps + "spPr", xfrm, Geometry(shape, type, spt, width, height));
        properties.Add(filled ? Fill(shape, type) : new XElement(Ns.A + "noFill"));
        properties.Add(stroked ? Line(shape, type) : new XElement(Ns.A + "ln", new XElement(Ns.A + "noFill")));
        if (Shadow(shape, type) is { } shadow)
            properties.Add(new XElement(Ns.A + "effectLst", shadow));
        var wsp = new XElement(Drawings.Wps + "wsp", new XElement(Drawings.Wps + "cNvSpPr"), properties);
        // WordArt (v:textpath): text bent as PowerPoint draws the shape type's text warp, on shapes of a type only, with
        // the text path on (the shape's or its type's); a rectangle with one stays a rectangle with its text box
        // (docx/external/libreoffice/extras-ooxmlexport/drawinglayer-pic-pos: "The Space Race" small in a framed box).
        var on = ((string?)shape.Element(V + "textpath")?.Attribute("on") ?? (string?)type?.Element(V + "textpath")?.Attribute("on")
            ?? (type == null && int.TryParse(spt, out var kind) && kind is >= 136 and <= 175 ? "t" : null)) is "t" or "true" or "1";
        if (shape.Name == V + "shape" && on && (shape.Element(V + "textpath") ?? type?.Element(V + "textpath")) is { } textpath
            && (string?)shape.Element(V + "textpath")?.Attribute("string") is { } text)
        {
            // trim fits the ink to the shape instead of the em box (docx/local/vwa-plain: "WORD-ART" in Arial Black
            // fills its 134.8 pt from the capitals' tops to the baseline); a line break at the end adds no line
            // (vwa-inflate: three lines and a break fill textInflateTop).
            var (warp, adjustments) = TextWarp(spt, Adjustments(shape, type));
            var trim = ((string?)shape.Element(V + "textpath")?.Attribute("trim") ?? (string?)type?.Element(V + "textpath")?.Attribute("trim")) is "t" or "true";
            // The text path's style is its type's with the shape's own over it (extras-ooxmlimport/WordArt: kerned by
            // its type's v-text-kern; docx/local/vwa-arch: the kerned advance fills the arch).
            var textStyle = string.Join(";", new[] { (string?)type?.Element(V + "textpath")?.Attribute("style"), (string?)shape.Element(V + "textpath")?.Attribute("style") }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            wsp.Add(new XElement(Drawings.Wps + "txbxWordArt", text.Replace("\r\n", "\n").TrimEnd('\n'), new XAttribute("style", textStyle),
                new XAttribute("warp", warp), trim ? new XAttribute("trim", 1) : null, adjustments.Select((v, i) => new XAttribute("adj" + (i + 1), v)),
                GuidePaths(shape, type, width, height)));
        }
        else if (shape.Element(V + "textbox")?.Element(Wml.W + "txbxContent") is { } content)
        {
            // Linked text boxes (mso-next-textbox): the first box of a chain holds the story, the boxes its links lead to
            // show what does not fit the boxes before, in the links' order whatever theirs in the document; a box linked
            // to itself ends its chain, as one linked to none does (LibreOffice's extras-ooxmlexport/
            // tdf87348_linkedTextboxes: four chains of two to five boxes, their followers before them in the document).
            var link = Linked(shape);
            wsp.Add(link is { Seq: > 0 } follower
                ? new XElement(Drawings.Wps + "linkedTxbx", new XAttribute("id", follower.Id), new XAttribute("seq", follower.Seq))
                : new XElement(Drawings.Wps + "txbx", link is { } head ? new XAttribute("id", head.Id) : null, new XElement(content)));
            var inset = ((string?)shape.Element(V + "textbox")!.Attribute("inset") ?? "").Split(',');
            double Inset(int i, double fallback) => i < inset.Length && Length(inset[i].Trim()) is { } v ? v : fallback;
            var anchor = Style((string?)shape.Attribute("style")).GetValueOrDefault("v-text-anchor") switch { "middle" => "ctr", "bottom" => "b", _ => "t" };
            // A text box that fits its shape to the text (mso-fit-shape-to-text) is as high as it (testOuterShdw: a box
            // stated 42.45 pt high stands 39.8 pt high around a line of text and its paragraph's top border).
            var textStyle = Style((string?)shape.Element(V + "textbox")!.Attribute("style"));
            var fits = textStyle.GetValueOrDefault("mso-fit-shape-to-text") is "t" or "true";
            // Text that runs down the box (layout-flow) turns as DrawingML's vertical text: a quarter clockwise, up from
            // the bottom with mso-layout-flow-alt bottom-to-top, East Asian upright (docx/external/libreoffice/
            // extras-ooxmlexport/vml-text-vertical-adjust: "TEXT" down a sidebar's text box, its insets as given); in a
            // turned shape the turn alone sets it (extras-ooxmlexport/TextFrameRotation: a box turned by −90° with
            // text flowing bottom to top reads upwards, not upside down).
            var turned = Style((string?)shape.Attribute("style")).GetValueOrDefault("rotation") is { } rotation
                && double.TryParse(rotation.TrimEnd('f', 'd'), NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) && degrees != 0;
            // mso-layout-flow-alt alone turns the text too, whatever layout-flow says but an ideographic flow (measured
            // with docx/local/vlf-alt, vlf-althor, vlf-alttop: bottom-to-top reads upwards, also beside layout-flow
            // horizontal; top-to-bottom downwards, its first line at the left; extras-ooxmlexport/layout-flow-alt-alone).
            var vertical = turned ? null : (textStyle.GetValueOrDefault("layout-flow"), textStyle.GetValueOrDefault("mso-layout-flow-alt")) switch
            {
                ("vertical-ideographic", _) => "eaVert",
                (_, "bottom-to-top") => "vert270",
                ("vertical", _) => "vert",
                (_, "top-to-bottom") => "mongolianVert",
                _ => null,
            };
            // Text that does not wrap (mso-wrap-style:none) makes the shape as wide as its lines, as DrawingML's wrap none
            // (measured with docx/local/vws-vml-*; LibreOffice's extras-layout/shape-anchored-in-follow).
            var unwrapped = Style((string?)shape.Attribute("style")).GetValueOrDefault("mso-wrap-style") == "none";
            wsp.Add(new XElement(Drawings.Wps + "bodyPr", unwrapped ? new XAttribute("wrap", "none") : null,
                new XAttribute("lIns", Emu(Inset(0, 7.2))), new XAttribute("tIns", Emu(Inset(1, 3.6))),
                new XAttribute("rIns", Emu(Inset(2, 7.2))), new XAttribute("bIns", Emu(Inset(3, 3.6))), new XAttribute("anchor", anchor),
                vertical != null ? new XAttribute("vert", vertical) : null,
                fits ? new XElement(Ns.A + "spAutoFit") : null));
        }
        return wsp;
    }

    /// <summary>
    /// The DrawingML text warp of a WordArt shape type (o:spt 136 to 175) and its adjustments from the shape's (or its
    /// type's) adj: the angle of arches, circles and buttons in 60000ths instead of 65536ths of a degree, the share of
    /// the pours' ring and of the other warps' offsets in 100000ths instead of the 21600ths of the shape (their
    /// defaults agree: 3086 for textCanDown's 14286, 9931 for textCurveUp's 45977, 12000 for textSlantUp's 55555);
    /// warps whose VML defaults do not map so keep DrawingML's (docx/external/libreoffice/extras-ooxmlimport/WordArt:
    /// "WORD-ART" along an arch).
    /// </summary>
    static (string Preset, List<long> Adjustments) TextWarp(string? spt, List<double> adj)
    {
        var preset = spt switch
        {
            "137" => "textStop", "138" => "textTriangle", "139" => "textTriangleInverted", "140" => "textChevron",
            "141" => "textChevronInverted", "142" => "textRingInside", "143" => "textRingOutside", "144" => "textArchUp",
            "145" => "textArchDown", "146" => "textCircle", "147" => "textButton", "148" => "textArchUpPour",
            "149" => "textArchDownPour", "150" => "textCirclePour", "151" => "textButtonPour", "152" => "textCurveUp",
            "153" => "textCurveDown", "154" => "textCascadeUp", "155" => "textCascadeDown", "156" => "textWave1",
            "157" => "textWave2", "158" => "textDoubleWave1", "159" => "textWave4", "160" => "textInflate",
            "161" => "textDeflate", "162" => "textInflateBottom", "163" => "textDeflateBottom", "164" => "textInflateTop",
            "165" => "textDeflateTop", "166" => "textDeflateInflate", "167" => "textDeflateInflateDeflate",
            "168" => "textFadeRight", "169" => "textFadeLeft", "170" => "textFadeUp", "171" => "textFadeDown",
            "172" => "textSlantUp", "173" => "textSlantDown", "174" => "textCanUp", "175" => "textCanDown",
            _ => "textPlain",
        };
        double? Value(int i) => i < adj.Count ? adj[i] : null;
        var adjustments = new List<long>();
        switch (spt)
        {
            case "144" or "145" or "146" or "147" or "148" or "149" or "150" or "151":
                if (Value(0) is { } angle)
                {
                    adjustments.Add((long)Math.Round(((angle / 65536.0 % 360 + 360) % 360) * 60000));
                    if (spt is "148" or "149" or "150" or "151" && Value(1) is { } ring)
                        adjustments.Add((long)Math.Round(ring / 10800.0 * 100000));
                }
                break;
            case "136" or "152" or "153" or "154" or "155" or "172" or "173" or "174" or "175":
                if (Value(0) is { } offset)
                    adjustments.Add((long)Math.Round(offset / 21600.0 * 100000));
                break;
        }
        return (preset, adjustments);
    }

    /// <summary>
    /// A WordArt shape type's own guide paths for warps between pairs of them (its path, each figure from a move a
    /// guide), as the custom paths of a text warp; null for other warps, which keep DrawingML's: VML's curves are not
    /// always DrawingML's (docx/local/vwa-inflate: textInflateTop's top is a cubic from 31.9 % down at the sides that
    /// reaches the top, DrawingML's a quadratic that rises halfway).
    /// </summary>
    static XElement? GuidePaths(XElement shape, XElement? type, double width, double height)
    {
        if (((string?)shape.Attribute("path") ?? (string?)type?.Attribute("path")) is not { } path
            || Path(path, (string?)shape.Attribute("coordsize") ?? (string?)type?.Attribute("coordsize"),
                (string?)shape.Attribute("coordorigin") ?? (string?)type?.Attribute("coordorigin"), Formulas(shape, type, width, height)) is not { } geometry)
            return null;
        var guides = new List<XElement>();
        foreach (var figure in geometry.Element(Ns.A + "pathLst")!.Elements(Ns.A + "path"))
        {
            foreach (var command in figure.Elements())
            {
                if (command.Name.LocalName == "moveTo" || guides.Count == 0)
                    guides.Add(new XElement(Ns.A + "path", figure.Attribute("w"), figure.Attribute("h")));
                guides[^1].Add(new XElement(command));
            }
        }
        guides.RemoveAll(g => g.Elements().Count() < 2);
        return guides.Count >= 2 && guides.Count % 2 == 0 ? new XElement(Pptx.TextWarp.OwnPaths, new XElement(Ns.A + "pathLst", guides)) : null;
    }

    static XElement Xfrm(double width, double height, Dictionary<string, string> style)
    {
        var xfrm = new XElement(Ns.A + "xfrm",
            new XElement(Ns.A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
            new XElement(Ns.A + "ext", new XAttribute("cx", Emu(width)), new XAttribute("cy", Emu(height))));
        var flip = style.GetValueOrDefault("flip") ?? "";
        var (flipH, flipV) = (flip.Contains('x'), flip.Contains('y'));
        // VML turns a shape first and flips it after, DrawingML flips first: under one flip the turn goes the other
        // way (measured with docx/local/vmlflip: right triangles and connectors turned 30° and 90° with flip:x, flip:y
        // and both; docx/external/libreoffice/extras-ooxmlexport/n751117, an arrow pointing right).
        // A turn in fixed point (fd) counts 65536ths of a degree (docx/external/libreoffice/
        // tdf137314_vml_rotation_unit_fd: rotation:1966080fd turns 30°).
        var turn = style.GetValueOrDefault("rotation")?.Trim();
        var fixedPoint = turn?.EndsWith("fd", StringComparison.OrdinalIgnoreCase) == true;
        if (double.TryParse(turn?.TrimEnd('f', 'd', 'e', 'g'), NumberStyles.Float, CultureInfo.InvariantCulture, out var rotation) && rotation != 0)
        {
            if (fixedPoint)
                rotation /= 65536;
            xfrm.SetAttributeValue("rot", (long)Math.Round((flipH != flipV ? -rotation : rotation) * 60000));
        }
        if (flipH) xfrm.SetAttributeValue("flipH", 1);
        if (flipV) xfrm.SetAttributeValue("flipV", 1);
        return xfrm;
    }

    /// <summary>The geometry: the shape's preset (VML element or o:spt number), else its path as a custom geometry.</summary>
    static XElement Geometry(XElement shape, XElement? type, string? spt, double width, double height)
    {
        var preset = shape.Name.LocalName switch
        {
            "rect" => "rect",
            "roundrect" => "roundRect",
            "oval" => "ellipse",
            "line" => "line",
            _ => spt switch
            {
                "1" or "75" or "202" => "rect",
                "2" => "roundRect",
                "3" => "ellipse",
                "4" => "diamond",
                "5" => "triangle",
                "6" => "rtTriangle",
                "7" => "parallelogram",
                "8" => "trapezoid",
                "9" => "hexagon",
                "10" => "octagon",
                "11" => "plus",
                "12" => "star5",
                "13" => "rightArrow",
                "15" => "homePlate",
                "16" => "cube",
                "20" or "32" => "line",
                "66" => "leftArrow",
                "67" => "downArrow",
                "68" => "upArrow",
                "136" => "rect",
                _ => null,
            },
        };
        // A shape of a preset type with adjustments of its own follows its type's path and formulas with them, which
        // the preset's defaults would not (docx/external/libreoffice/extras-ooxmlimport/vml-adjustments: a triangle's
        // apex at 17639/21600 of its width).
        // So does a hexagon, whose formulas count across and down on their own where the preset counts by the shorter
        // side (Word's PDF of docx/external/libreoffice/extras-ooxmlexport/test76317: 98.9 by 74.15 pt cut in by a
        // quarter of its width, 24.7 pt, not of its height); parallelograms and down arrows Word draws as the presets
        // (extras-ooxmlexport/tdf97618_testVmlShapeTextWordWrap, extras-layout/tdf123651).
        if ((preset == null || shape.Attribute("adj") != null && preset is not ("rect" or "roundRect" or "ellipse" or "line")
             || preset == "hexagon")
            && ((string?)shape.Attribute("path") ?? (string?)type?.Attribute("path")) is { } path
            && Path(path, (string?)shape.Attribute("coordsize") ?? (string?)type?.Attribute("coordsize"),
                (string?)shape.Attribute("coordorigin") ?? (string?)type?.Attribute("coordorigin"), Formulas(shape, type, width, height)) is { } custom)
            return custom;
        var geometry = new XElement(Ns.A + "prstGeom", new XAttribute("prst", preset ?? "rect"), new XElement(Ns.A + "avLst"));
        if (preset == "roundRect" && (shape.Attribute("arcsize") != null || shape.Name.LocalName == "roundrect"))
        {
            // Word rounds the corners by arcsize times the shorter side, not half of it as the VML spec says, a
            // v:roundrect 0.2 of it without an arcsize (measured with docx/local/vrr-arc: 200 by 100 pt with 0.25,
            // 2637f, 0.5 and none have corners of 25, 4.02, 50 and 20 pt; docx/external/libreoffice/extras-ooxmlimport/
            // n779627b); DrawingML's adjustment is the same share in 1/100000.
            var arc = (string?)shape.Attribute("arcsize");
            var fraction = arc == null ? 0.2
                : arc.EndsWith('f') && double.TryParse(arc[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f / 65536
                : double.TryParse(arc.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p / (arc.EndsWith('%') ? 100 : 1) : 0.2;
            geometry.Element(Ns.A + "avLst")!.Add(new XElement(Ns.A + "gd", new XAttribute("name", "adj"), new XAttribute("fmla", $"val {(long)Math.Round(Math.Clamp(fraction, 0, 0.5) * 100000)}")));
        }
        return geometry;
    }

    /// <summary>
    /// A shape's adjustments (adj): its own values in place of its type's, one left out (",10800") keeping the type's,
    /// in 65536ths for values given as fractions (f).
    /// </summary>
    static List<double> Adjustments(XElement shape, XElement? type)
    {
        var (own, defaults) = (((string?)shape.Attribute("adj") ?? "").Split(','), ((string?)type?.Attribute("adj") ?? "").Split(','));
        static double? Parse(string[] values, int i)
        {
            if (i >= values.Length)
                return null;
            var text = values[i].Trim();
            var fraction = text.EndsWith('f');
            return double.TryParse(fraction ? text[..^1] : text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
        }
        return Enumerable.Range(0, Math.Max(own.Length, defaults.Length)).Select(i => Parse(own, i) ?? Parse(defaults, i) ?? 0).ToList();
    }

    /// <summary>
    /// The values of a shape's formulas (v:formulas, catalog: vml.shapes), each v:f's eqn in order: an operation on up to
    /// three arguments, numbers, adjustments (#n), earlier values (@n) or the shape's measures (width and height of its
    /// coordinate size, xcenter, ycenter, emuwidth …); angles count 65536ths of a degree (docx/external/libreoffice/
    /// extras-ooxmlexport/fdo74110: textInflateTop's top curve from @0 at the sides to −@1 between). Null without
    /// formulas.
    /// </summary>
    static List<double>? Formulas(XElement shape, XElement? type, double width, double height)
    {
        if ((shape.Element(V + "formulas") ?? type?.Element(V + "formulas")) is not { } formulas)
            return null;
        var adjustments = Adjustments(shape, type);
        var size = (((string?)shape.Attribute("coordsize") ?? (string?)type?.Attribute("coordsize")) ?? "21600,21600").Split(',');
        var origin = (((string?)shape.Attribute("coordorigin") ?? (string?)type?.Attribute("coordorigin")) ?? "0,0").Split(',');
        double Part(string[] parts, int i, double fallback) =>
            i < parts.Length && double.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        var (cw, ch, ox, oy) = (Part(size, 0, 21600), Part(size, 1, 21600), Part(origin, 0, 0), Part(origin, 1, 0));
        var values = new List<double>();
        double Argument(string token) => token switch
        {
            ['#', .. var n] when int.TryParse(n, out var i) => i < adjustments.Count ? adjustments[i] : 0,
            ['@', .. var n] when int.TryParse(n, out var i) => i < values.Count ? values[i] : 0,
            "width" => cw,
            "height" => ch,
            "xcenter" => ox + cw / 2,
            "ycenter" => oy + ch / 2,
            "emuwidth" => width * 12700,
            "emuheight" => height * 12700,
            "emuwidth2" => width * 12700 / 2,
            "emuheight2" => height * 12700 / 2,
            "pixelwidth" => width * 96 / 72,
            "pixelheight" => height * 96 / 72,
            "pixellinewidth" or "hasstroke" or "hasfill" or "lineDrawn" => 1,
            _ => double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0,
        };
        const double Fd = 65536 * 180 / Math.PI;  // radians to 65536ths of a degree
        foreach (var f in formulas.Elements(V + "f"))
        {
            var parts = ((string?)f.Attribute("eqn") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            double A(int i) => i < parts.Length ? Argument(parts[i]) : 0;
            var (a, b, c) = (A(1), A(2), A(3));
            values.Add(parts.Length == 0 ? 0 : parts[0] switch
            {
                "val" => a,
                "sum" => a + b - c,
                "prod" => c == 0 ? 0 : a * b / c,
                "mid" => (a + b) / 2,
                "abs" => Math.Abs(a),
                "min" => Math.Min(a, b),
                "max" => Math.Max(a, b),
                "if" => a > 0 ? b : c,
                "mod" => Math.Sqrt(a * a + b * b + c * c),
                "atan2" => Math.Atan2(b, a) * Fd,
                "sin" => a * Math.Sin(b / Fd),
                "cos" => a * Math.Cos(b / Fd),
                "tan" => a * Math.Tan(b / Fd),
                "cosatan2" => a * Math.Cos(Math.Atan2(c, b)),
                "sinatan2" => a * Math.Sin(Math.Atan2(c, b)),
                "sqrt" => Math.Sqrt(Math.Max(0, a)),
                "sumangle" => a + b * 65536 - c * 65536,
                "ellipse" => b == 0 ? 0 : c * Math.Sqrt(Math.Max(0, 1 - a / b * (a / b))),
                _ => 0,
            });
        }
        return values;
    }

    /// <summary>
    /// A VML path (m, l, c, v, t, r, x, e, nf, ns, the arcs ar, at, wa, wr, qx, qy, ae, al) as a custom geometry in its
    /// coordinate size, each figure a path of its own, its points counted from the coordinate origin (docx/external/
    /// libreoffice/dml-groupshape-polygon: a polygon of coordorigin 420,11929 drawn from m420,12666, its corner at the
    /// shape's).
    /// </summary>
    static XElement? Path(string path, string? coordsize, string? coordorigin = null, IReadOnlyList<double>? values = null)
    {
        var size = (coordsize ?? "21600,21600").Split(',');
        long cw = size.Length > 0 && long.TryParse(size[0], out var a) ? a : 21600, ch = size.Length > 1 && long.TryParse(size[1], out var b) ? b : 21600;
        var origin = (coordorigin ?? "").Split(',');
        long ox = origin.Length > 0 && long.TryParse(origin[0].Trim(), out var oa) ? oa : 0, oy = origin.Length > 1 && long.TryParse(origin[1].Trim(), out var ob) ? ob : 0;
        // Commands and their parameters, a parameter left out before, between or after commas being 0 (m,128588 moves
        // to 0 128588, r469741, draws 469741 to the right; docx/external/libreoffice/extras-ooxmlexport/
        // tdf105875_VmlShapeRotationWithFlip, whose shapes were lost without their starting point).
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var pending = false;  // a comma's parameter not seen yet
        void Number()
        {
            if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
                pending = false;
            }
        }
        foreach (var c in path)
        {
            if (char.IsLetter(c))
            {
                Number();
                if (pending)
                    tokens.Add("0");
                pending = false;
                // Commands of two letters (nf, ns, qx, ar, wa …).
                if (tokens.Count > 0 && tokens[^1].Length == 1 && char.IsLetter(tokens[^1][0]) && "nqaw".Contains(tokens[^1][0]))
                    tokens[^1] += c;
                else
                    tokens.Add(c.ToString());
                continue;
            }
            if (c == ',')
            {
                // An empty parameter: right after a command or another comma.
                if (current.Length == 0 && (pending || tokens.Count > 0 && char.IsLetter(tokens[^1][0])))
                    tokens.Add("0");
                Number();
                pending = true;
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                Number();
                continue;
            }
            if (c is '-' or '@' && current.Length > 0)
                Number();
            current.Append(c);
        }
        Number();
        if (pending)
            tokens.Add("0");

        // Each figure the path ends (e) is a path of its own, nf and ns leaving it unfilled or unstroked (docx/external/
        // libreoffice/extras-ooxmlexport/tdf104115: an arc drawn, its pie filled but not drawn).
        XElement NewPath() => new(Ns.A + "path", new XAttribute("w", cw), new XAttribute("h", ch));
        var pathElement = NewPath();
        var paths = new List<XElement> { pathElement };
        string? command = null;
        var numbers = new List<long>();
        long px = 0, py = 0;
        void Flush()
        {
            switch (command)
            {
                case "m" when numbers.Count >= 2:
                    (px, py) = (numbers[0], numbers[1]);
                    pathElement.Add(new XElement(Ns.A + "moveTo", Pt(px, py)));
                    break;
                case "t" when numbers.Count >= 2:
                    (px, py) = (px + numbers[0], py + numbers[1]);
                    pathElement.Add(new XElement(Ns.A + "moveTo", Pt(px, py)));
                    break;
                case "l":
                    Begin();
                    for (var i = 0; i + 1 < numbers.Count; i += 2)
                    {
                        (px, py) = (numbers[i], numbers[i + 1]);
                        pathElement.Add(new XElement(Ns.A + "lnTo", Pt(px, py)));
                    }
                    break;
                case "r":
                    Begin();
                    for (var i = 0; i + 1 < numbers.Count; i += 2)
                    {
                        (px, py) = (px + numbers[i], py + numbers[i + 1]);
                        pathElement.Add(new XElement(Ns.A + "lnTo", Pt(px, py)));
                    }
                    break;
                case "c":
                    Begin();
                    for (var i = 0; i + 5 < numbers.Count; i += 6)
                    {
                        pathElement.Add(new XElement(Ns.A + "cubicBezTo", Pt(numbers[i], numbers[i + 1]), Pt(numbers[i + 2], numbers[i + 3]), Pt(numbers[i + 4], numbers[i + 5])));
                        (px, py) = (numbers[i + 4], numbers[i + 5]);
                    }
                    break;
                case "v":
                    Begin();
                    for (var i = 0; i + 5 < numbers.Count; i += 6)
                    {
                        pathElement.Add(new XElement(Ns.A + "cubicBezTo", Pt(px + numbers[i], py + numbers[i + 1]), Pt(px + numbers[i + 2], py + numbers[i + 3]), Pt(px + numbers[i + 4], py + numbers[i + 5])));
                        (px, py) = (px + numbers[i + 4], py + numbers[i + 5]);
                    }
                    break;
                case "x":
                    pathElement.Add(new XElement(Ns.A + "close"));
                    break;
                case "nf":
                    pathElement.SetAttributeValue("fill", "none");
                    break;
                case "ns":
                    pathElement.SetAttributeValue("stroke", "0");
                    break;
                case "e":
                    pathElement = NewPath();
                    paths.Add(pathElement);
                    break;
                // Arcs of the ellipse in a box (x1, y1, x2, y2) from its ray through (x3, y3) to the one through (x4, y4):
                // ar and at counterclockwise, wa and wr clockwise; ar and wa begin a new figure there, at and wr draw
                // a line to it (docx/external/libreoffice/extras-ooxmlexport/cloud, tdf104115).
                case "ar" or "at" or "wa" or "wr":
                    for (var i = 0; i + 7 < numbers.Count; i += 8)
                    {
                        var (cx, cy) = ((numbers[i] + numbers[i + 2]) / 2.0, (numbers[i + 1] + numbers[i + 3]) / 2.0);
                        var (rx, ry) = (Math.Abs(numbers[i + 2] - numbers[i]) / 2.0, Math.Abs(numbers[i + 3] - numbers[i + 1]) / 2.0);
                        if (rx <= 0 || ry <= 0)
                            continue;
                        double Ray(long x, long y) => Math.Atan2((y - cy) / ry, (x - cx) / rx);
                        var (start, end) = (Ray(numbers[i + 4], numbers[i + 5]), Ray(numbers[i + 6], numbers[i + 7]));
                        var clockwise = command[0] == 'w';
                        var sweep = clockwise ? Turn(end - start) : -Turn(start - end);
                        if (Math.Abs(sweep) < 1e-9)
                            sweep = clockwise ? 2 * Math.PI : -2 * Math.PI;
                        var (sx, sy) = ((long)Math.Round(cx + rx * Math.Cos(start)), (long)Math.Round(cy + ry * Math.Sin(start)));
                        To(sx, sy, command is "at" or "wr");
                        Arc(cx, cy, rx, ry, start, sweep);
                    }
                    break;
                // Quarter ellipses to a point, setting out along x (qx) or y (qy), the next point the other way.
                case "qx" or "qy":
                {
                    var alongX = command == "qx";
                    Begin();
                    for (var i = 0; i + 1 < numbers.Count; i += 2)
                    {
                        var (x, y) = (numbers[i], numbers[i + 1]);
                        var (cx, cy) = alongX ? ((double)px, (double)y) : ((double)x, (double)py);
                        var (rx, ry) = (Math.Abs(x - px) / 1.0, Math.Abs(y - py) / 1.0);
                        if (rx <= 0 || ry <= 0)
                            pathElement.Add(new XElement(Ns.A + "lnTo", Pt(x, y)));
                        else
                        {
                            var start = Math.Atan2((py - cy) / ry, (px - cx) / rx);
                            var end = Math.Atan2((y - cy) / ry, (x - cx) / rx);
                            var sweep = Turn(end - start);
                            Arc(cx, cy, rx, ry, start, sweep > Math.PI ? sweep - 2 * Math.PI : sweep);
                        }
                        (px, py) = (x, y);
                        alongX = !alongX;
                    }
                    break;
                }
                // Ellipses about a center (x, y) with radii (w, h) from an angle through a sweep, both in 65536ths of a
                // degree: al begins a new figure, ae draws a line to its start (extras-ooxmlimport/WordArt).
                case "ae" or "al":
                    for (var i = 0; i + 5 < numbers.Count; i += 6)
                    {
                        var (cx, cy, rx, ry) = ((double)numbers[i], (double)numbers[i + 1], (double)numbers[i + 2], (double)numbers[i + 3]);
                        if (rx <= 0 || ry <= 0)
                            continue;
                        var (start, sweep) = (numbers[i + 4] / 65536.0 * Math.PI / 180, numbers[i + 5] / 65536.0 * Math.PI / 180);
                        var (sx, sy) = ((long)Math.Round(cx + rx * Math.Cos(start)), (long)Math.Round(cy + ry * Math.Sin(start)));
                        To(sx, sy, command == "ae");
                        Arc(cx, cy, rx, ry, start, sweep);
                    }
                    break;
            }
            numbers.Clear();
        }
        // A figure drawn without a move starts at the current point.
        void Begin()
        {
            if (!pathElement.HasElements)
                pathElement.Add(new XElement(Ns.A + "moveTo", Pt(px, py)));
        }
        // The start of an arc: a line to it, or a move where it begins the figure.
        void To(long x, long y, bool line) => pathElement.Add(new XElement(Ns.A + (line && pathElement.HasElements ? "lnTo" : "moveTo"), Pt(x, y)));
        // An angle taken into [0, 2π).
        static double Turn(double angle) => ((angle % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
        // An elliptical arc from the angle start (the current point) through sweep (radians, clockwise on the page
        // positive) as cubic Béziers of at most a quarter each.
        void Arc(double cx, double cy, double rx, double ry, double start, double sweep)
        {
            var count = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (Math.PI / 2) - 1e-9));
            var step = sweep / count;
            var k = 4.0 / 3 * Math.Tan(step / 4);
            for (var j = 0; j < count; j++)
            {
                var (a0, a1) = (start + j * step, start + (j + 1) * step);
                long X(double v) => (long)Math.Round(v);
                pathElement.Add(new XElement(Ns.A + "cubicBezTo",
                    Pt(X(cx + rx * (Math.Cos(a0) - k * Math.Sin(a0))), X(cy + ry * (Math.Sin(a0) + k * Math.Cos(a0)))),
                    Pt(X(cx + rx * (Math.Cos(a1) + k * Math.Sin(a1))), X(cy + ry * (Math.Sin(a1) - k * Math.Cos(a1)))),
                    Pt(X(cx + rx * Math.Cos(a1)), X(cy + ry * Math.Sin(a1)))));
                (px, py) = (X(cx + rx * Math.Cos(a1)), X(cy + ry * Math.Sin(a1)));
            }
        }
        foreach (var token in tokens)
        {
            if (char.IsLetter(token[0]))
            {
                Flush();
                command = token;
                if (token == "x")
                    Flush();
                continue;
            }
            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                numbers.Add(n);
            else if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                numbers.Add((long)Math.Round(d));
            // A formula's value (@n).
            else if (token[0] == '@' && int.TryParse(token[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var k))
                numbers.Add(values != null && k >= 0 && k < values.Count ? (long)Math.Round(values[k]) : 0);
        }
        Flush();
        paths.RemoveAll(p => !p.HasElements);
        if (paths.Count == 0)
            return null;
        return new XElement(Ns.A + "custGeom", new XElement(Ns.A + "avLst"), new XElement(Ns.A + "gdLst"), new XElement(Ns.A + "ahLst"),
            new XElement(Ns.A + "cxnLst"), new XElement(Ns.A + "rect", new XAttribute("l", 0), new XAttribute("t", 0), new XAttribute("r", "r"), new XAttribute("b", "b")),
            new XElement(Ns.A + "pathLst", paths));

        XElement Pt(long x, long y) => new(Ns.A + "pt", new XAttribute("x", x - ox), new XAttribute("y", y - oy));
    }

    /// <summary>
    /// The page background (catalog: document.background): a v:background's fill, else w:background's color, as a
    /// DrawingML fill; null without one.
    /// </summary>
    public static XElement? BackgroundFill(XElement? background, Drawing.ColorContext colors)
    {
        if (background == null)
            return null;
        if (background.Element(V + "background") is { } vml)
            return Fill(vml, null);
        var color = (string?)background.Attribute(Wml.W + "color");
        if ((string?)background.Attribute(Wml.W + "themeColor") is { } theme)
        {
            var scheme = new XElement(Ns.A + "schemeClr", new XAttribute("val", theme switch
            {
                "dark1" => "dk1", "light1" => "lt1", "dark2" => "dk2", "light2" => "lt2", "text1" => "tx1", "text2" => "tx2",
                "background1" => "bg1", "background2" => "bg2", "hyperlink" => "hlink", "followedHyperlink" => "folHlink", var other => other,
            }));
            // The theme color wins over w:color, tinted or shaded in its HSL lightness as text colors are (measured with
            // docx/local/bgt-a to bgt-c, variants of LibreOffice's WPC_CanvasBackground: background2 EEECE1 shaded 3F
            // prints 484329 with w:color 484329, FF0000 or none; this rule gives 494429).
            var tinted = DocumentContext.Tinted(colors.Resolve(scheme), (string?)background.Attribute(Wml.W + "themeTint"),
                (string?)background.Attribute(Wml.W + "themeShade"));
            return new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", tinted.ToHex().TrimStart('#'))));
        }
        return color is { Length: 6 } hex && hex != "auto" ? new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", hex))) : null;
    }

    /// <summary>
    /// A shape's fill (measured with docx/local/vml-fx, vml-angles): solid; a gradient from the fill color to color2
    /// (or along its colors list) that runs, at the focus 0 Word writes by default, from color2 at the start of its
    /// angle (0: the top, 90: the left, 45: the top left corner, the angle applying in the shape stretched to a square)
    /// to the fill color, at focus 100 % the other way, at focus 50 % from the fill color to color2 in the middle and
    /// back, at -50 % the other way round (negative angles turned round, see <see cref="Turned"/>); a radial gradient
    /// (gradientRadial) from its focus position outwards; o:opacity2 the opacity at color2; pictures stretched or
    /// tiled.
    /// </summary>
    static XElement Fill(XElement shape, XElement? type)
    {
        var fill = shape.Element(V + "fill") ?? type?.Element(V + "fill");
        var color = Color((string?)shape.Attribute("fillcolor") ?? (string?)type?.Attribute("fillcolor")) ?? "FFFFFF";
        var opacity = Fraction((string?)fill?.Attribute("opacity"));
        var kind = (string?)fill?.Attribute("type") ?? "solid";
        if (kind is "gradient" or "gradientRadial" or "gradientCenter" && GradientColors(fill, color, opacity) is { } colors)
        {
            var focus = Math.Clamp(Fraction((string?)fill?.Attribute("focus")) ?? 0, -1, 1);
            var angle = double.TryParse((string?)fill?.Attribute("angle"), NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) ? degrees : 0;
            var radial = kind is "gradientRadial" or "gradientCenter";
            if (!radial)
                (angle, focus) = Turned(angle, focus);
            var stops = radial ? Blend(FocusStops(colors, focus), Sigma(fill)) : LinearStops(colors, focus, Sigma(fill));
            var list = new XElement(Ns.A + "gsLst", stops.Select(s =>
                new XElement(Ns.A + "gs", new XAttribute("pos", (long)Math.Round(s.Pos * 100000)), Solid(s.Hex, s.Alpha).Elements().First())));
            if (radial)
            {
                var (fx, fy) = Pair((string?)fill?.Attribute("focusposition"));
                var (fw, fh) = Pair((string?)fill?.Attribute("focussize"));
                string Per(double v) => ((long)Math.Round(v * 100000)).ToString(CultureInfo.InvariantCulture);
                return new XElement(Ns.A + "gradFill", list,
                    new XElement(Ns.A + "path", new XAttribute("path", "rect"),
                        new XElement(Ns.A + "fillToRect", new XAttribute("l", Per(fx)), new XAttribute("t", Per(fy)),
                            new XAttribute("r", Per(1 - fx - fw)), new XAttribute("b", Per(1 - fy - fh)))));
            }
            return new XElement(Ns.A + "gradFill", list,
                new XElement(Ns.A + "lin", new XAttribute("ang", (long)Math.Round(((90 - angle) % 360 + 360) % 360 * 60000)), new XAttribute("scaled", 1)));
        }
        // A pattern (type pattern): its picture's bits, white in the fill color and black in color2, a point a bit,
        // the tiles from the page's corner as on page backgrounds (measured with docx/local/vml-pattern: Word's 8 × 8
        // checkerboard and brick GIFs on inline and floating rectangles).
        if (kind == "pattern" && ((string?)fill?.Attribute(R + "id") ?? (string?)fill?.Attribute(O + "relid")) is { } bitmap)
        {
            return new XElement(Ns.A + "pattFill", new XAttribute("prst", "pct50"),
                new XAttribute(FreeSlate.Drawing.FillReader.PatternPicture, bitmap), new XAttribute(FreeSlate.Drawing.FillReader.PatternCell, Units.Css4(4.0 / 3)),
                new XElement(Ns.A + "fgClr", Solid(color, opacity).Elements().First()),
                new XElement(Ns.A + "bgClr", Solid(Color((string?)fill?.Attribute("color2"), color) ?? "FFFFFF", opacity).Elements().First()));
        }
        if (kind is "frame" or "tile" && ((string?)fill?.Attribute(R + "id") ?? (string?)fill?.Attribute(O + "relid")) is { } picture)
        {
            return new XElement(Ns.A + "blipFill", new XElement(Ns.A + "blip", new XAttribute(R + "embed", picture)),
                kind == "tile" ? new XElement(Ns.A + "tile") : new XElement(Ns.A + "stretch", new XElement(Ns.A + "fillRect")));
        }
        return new XElement(Ns.A + "solidFill", Solid(color, opacity).Elements().First());
    }

    /// <summary>
    /// A gradient's colors from the fill color's end (0) to color2's (1): its colors list, else the fill color and
    /// color2, with their opacity (o:opacity2 at color2); null without color2.
    /// </summary>
    static List<(double At, string Hex, double Alpha)>? GradientColors(XElement? fill, string color, double? opacity)
    {
        if (Color((string?)fill?.Attribute("color2"), color) is not { } color2)
            return null;
        var opacity1 = opacity ?? 1;
        var opacity2 = Fraction((string?)fill?.Attribute(O + "opacity2") ?? (string?)fill?.Attribute("opacity2")) ?? opacity1;
        var colors = new List<(double At, string Hex, double Alpha)>();
        foreach (var entry in ((string?)fill?.Attribute("colors") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = entry.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && Fraction(parts[0]) is { } at && Color(parts[1], color) is { } hex)
                colors.Add((Math.Clamp(at, 0, 1), hex, opacity1 + (opacity2 - opacity1) * Math.Clamp(at, 0, 1)));
        }
        return colors.Count >= 2 ? colors : [(0, color, opacity1), (1, color2, opacity2)];
    }

    /// <summary>
    /// A gradient's stops by its focus, in order: at focus 0 from color2 to the fill color, at 100 % the other way, at
    /// a focus between from the fill color to color2 at the focus and back, at a negative one from color2 to the fill
    /// color at the focus and back.
    /// </summary>
    static List<(double Pos, string Hex, double Alpha)> FocusStops(List<(double At, string Hex, double Alpha)> colors, double focus)
    {
        var stops = new List<(double Pos, string Hex, double Alpha)>();
        if (focus >= 0)
        {
            stops.AddRange(colors.Select(c => (c.At * focus, c.Hex, c.Alpha)));
            stops.AddRange(colors.AsEnumerable().Reverse().Select(c => (1 - (1 - focus) * c.At, c.Hex, c.Alpha)));
        }
        else
        {
            var g = Math.Min(1, -focus);
            stops.AddRange(colors.AsEnumerable().Reverse().Select(c => (g * (1 - c.At), c.Hex, c.Alpha)));
            stops.AddRange(colors.Select(c => (g + (1 - g) * c.At, c.Hex, c.Alpha)));
        }
        // Only the last stop at the start and the first at the end count; the others would draw a hairline there.
        var sorted = stops.OrderBy(s => s.Pos).ToList();
        var first = sorted.FindLastIndex(s => s.Pos <= 1e-9);
        var last = sorted.FindIndex(s => s.Pos >= 1 - 1e-9);
        return first >= 0 && last >= first ? sorted.GetRange(first, last - first + 1) : sorted;
    }

    /// <summary>
    /// A linear gradient's angle and focus as Word draws it (measured with docx/local/vml-angles and the page
    /// backgrounds docx/local/bggrad-*): a negative angle (or one from 360 on) turns the gradient round, the angle plus
    /// 180 with the focus ±50 % reversed and any focus other than 0 and 100 % the rest to 100 %; Word's presets write
    /// -90, -45 and -135.
    /// </summary>
    static (double Angle, double Focus) Turned(double angle, double focus)
    {
        if (angle >= 0 && angle < 360)
            return (angle, focus);
        angle = ((angle + 180) % 360 + 360) % 360;
        if (Math.Abs(focus) < 1e-6 || Math.Abs(Math.Abs(focus) - 1) < 1e-6)
            return (angle, focus);
        return (angle, Math.Abs(Math.Abs(focus) - 0.5) < 1e-6 ? -focus : Math.Sign(focus) * (1 - Math.Abs(focus)));
    }

    /// <summary>
    /// A linear gradient's stops: blended between both ends at the focus 0, 100 and ±50 %, the focus applied twice
    /// along straight lines at any other (see <see cref="TwiceFocused"/>).
    /// </summary>
    static List<(double Pos, string Hex, double Alpha)> LinearStops(List<(double At, string Hex, double Alpha)> colors, double focus, bool sigma) =>
        Math.Abs(focus) < 1e-6 || Math.Abs(Math.Abs(focus) - 1) < 1e-6 || Math.Abs(Math.Abs(focus) - 0.5) < 1e-6
            ? Blend(FocusStops(colors, focus), sigma)
            : TwiceFocused(colors, focus);

    /// <summary>
    /// Whether a gradient blends along an S-curve in linear light: unless its method is none or linear, or it lists
    /// its colors, which blend straight in RGB whatever the method (measured with docx/local/vgc-a: red to blue
    /// halfway 182,0,189 without a list, 127,0,127 with colors="0 red;1 blue", also at focus 100 %, with method
    /// "linear sigma" and o:fill gradientUnscaled; docx/external/libreoffice/extras-ooxmlexport/tdf159158_zOrder_zIndexMax).
    /// </summary>
    static bool Sigma(XElement? fill) => (string?)fill?.Attribute("method") is not ("none" or "linear") && fill?.Attribute("colors") == null;

    /// <summary>
    /// The page background's linear VML gradient as Word prints it (catalog: document.background), in points from the
    /// page's top left corner, mirrored beyond its ends; null for other fills. Word lays the gradient over a tile of
    /// 1025 × 769 of its 0.12 pt dots, starting at the page's corner (measured with docx/local/bggrad-*, its own
    /// presets docx/local/bgpreset-*): a gradient between both colors (focus 0 or 100 %) spans one tile from the
    /// start of its angle and turns back at its end; at the angles 0 and 180 and at negative ones that are multiples
    /// of 45 (Word's presets write 0, -90, -45 and -135) it spans a tile of 512 × 384 dots. A gradient out and back
    /// (focus ±50 %) spans one tile, the half tile at those diagonals. Negative angles (and from 360 on) turn the
    /// gradient round: the angle plus 180 with the focus ±50 % reversed and any other the rest to 100 %. At any other
    /// focus Word applies the focus twice, along straight lines between the colors.
    /// </summary>
    public static (double X1, double Y1, double X2, double Y2, List<(double Pos, string Hex, double Alpha)> Stops)? BackgroundGradient(XElement background)
    {
        var fill = background.Element(V + "fill");
        if ((string?)fill?.Attribute("type") != "gradient")
            return null;
        var color = Color((string?)background.Attribute("fillcolor")) ?? "FFFFFF";
        if (GradientColors(fill, color, Fraction((string?)fill.Attribute("opacity"))) is not { } colors)
            return null;
        static bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;
        static bool Multiple(double a, double m) => Near(a / m, Math.Round(a / m));
        var angle = double.TryParse((string?)fill.Attribute("angle"), NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) ? degrees : 0;
        var turned = angle < 0 || angle >= 360;
        var half = Multiple(angle, 180) || turned && Multiple(angle, 45);
        var (effective, focus) = Turned(angle, Math.Clamp(Fraction((string?)fill.Attribute("focus")) ?? 0, -1, 1));
        var ends = Near(focus, 0) || Near(Math.Abs(focus), 1);
        var symmetric = Near(Math.Abs(focus), 0.5);
        var (w, h) = ends && half || symmetric && half && !Multiple(effective, 90) ? (512 * WordDevice.Dot, 384 * WordDevice.Dot)
            : (1025 * WordDevice.Dot, 769 * WordDevice.Dot);
        // The angle applies in the tile stretched to a square: its direction in the tile, from the start corner.
        var (dy, dx) = Math.SinCos((90 - effective) * Math.PI / 180);
        (dx, dy) = (Math.Abs(dx) < 1e-9 ? 0 : dx, Math.Abs(dy) < 1e-9 ? 0 : dy);
        var (nx, ny) = (dx / w, dy / h);
        var length = Math.Sqrt(nx * nx + ny * ny);
        (nx, ny) = (nx / length, ny / length);
        var span = Math.Abs(w * nx) + Math.Abs(h * ny);
        var (x0, y0) = (dx < 0 ? w : 0, dy < 0 ? h : 0);
        return (x0, y0, x0 + nx * span, y0 + ny * span, LinearStops(colors, focus, Sigma(fill)));
    }

    /// <summary>
    /// A gradient at a focus other than 0, ±50 and 100 % as Word prints a page background with it: the focus applied
    /// twice (fill color at 0, the focus and 1, color2 at the focus of either part; a negative focus the mirror image
    /// with the colors exchanged), along straight lines between the colors (measured with docx/local/bggrad-*-focus30,
    /// -focus70, -focusm30: within 0.5 pt).
    /// </summary>
    static List<(double Pos, string Hex, double Alpha)> TwiceFocused(List<(double At, string Hex, double Alpha)> colors, double focus)
    {
        var g = Math.Abs(focus);
        (double T, double Q)[] points = [(0, 0), (g * g, 1), (g, 0), (1 - g * (1 - g), 1), (1, 0)];
        if (focus < 0)
            points = points.Reverse().Select(p => (1 - p.T, 1 - p.Q)).ToArray();
        var stops = new List<(double Pos, string Hex, double Alpha)>();
        for (var k = 0; k + 1 < points.Length; k++)
        {
            var (a, b) = (points[k], points[k + 1]);
            var along = a.Q < b.Q ? colors : colors.AsEnumerable().Reverse().ToList();
            foreach (var c in along)
                stops.Add((a.T + (c.At - a.Q) / (b.Q - a.Q) * (b.T - a.T), c.Hex, c.Alpha));
        }
        return stops;
    }

    /// <summary>
    /// The page background's picture stretched over the page (type frame) or pattern (type pattern), with the fill
    /// color and color2 (catalog: document.background); null for other fills.
    /// </summary>
    public static (string Kind, string Id, string Color, string Color2)? BackgroundPicture(XElement background)
    {
        var fill = background.Element(V + "fill");
        if ((string?)fill?.Attribute("type") is not ({ } kind and ("frame" or "pattern"))
            || ((string?)fill.Attribute(R + "id") ?? (string?)fill.Attribute(O + "relid")) is not { } id)
            return null;
        var color = Color((string?)background.Attribute("fillcolor")) ?? "FFFFFF";
        return (kind, id, color, Color((string?)fill.Attribute("color2"), color) ?? "FFFFFF");
    }

    /// <summary>
    /// The page background's radial VML gradient as Word prints it (catalog: document.background): a tile of 1025 ×
    /// 769 of Word's 0.12 pt dots, of 512 × 384 dots for its presets from a corner (o:fill gradientCenter), repeated
    /// from the page's top left corner (measured with docx/local/bgpreset-center*, bgpreset-corner*, bggrad-a4-corner,
    /// bggrad-a4-radial); null for other fills.
    /// </summary>
    public static (double Width, double Height, XElement Fill)? BackgroundTile(XElement background)
    {
        var fill = background.Element(V + "fill");
        if ((string?)fill?.Attribute("type") is not ("gradientRadial" or "gradientCenter"))
            return null;
        // Word's "from corner" gradients (o:fill gradientCenter) take the smaller tile.
        var corner = (string?)fill.Attribute("type") == "gradientCenter" || (string?)fill.Element(O + "fill")?.Attribute("type") == "gradientCenter";
        return corner ? (512 * WordDevice.Dot, 384 * WordDevice.Dot, Fill(background, null))
            : (1025 * WordDevice.Dot, 769 * WordDevice.Dot, Fill(background, null));
    }

    /// <summary>
    /// Word blends a VML gradient's colors in linear light along an S-curve, or in plain RGB along a straight line
    /// where its method is none or linear, premultiplied by their opacity (measured with docx/local/vml-fx: orange to
    /// blue and red, green and blue within 3 of 255, a color fading out keeps its hue): the stops with the blend
    /// between them in steps.
    /// </summary>
    static List<(double Pos, string Hex, double Alpha)> Blend(List<(double Pos, string Hex, double Alpha)> stops, bool sigma)
    {
        double Linear(int c) => c / 255.0 is var v && (!sigma || v <= 0.04045) ? sigma ? v / 12.92 : v : Math.Pow((v + 0.055) / 1.055, 2.4);
        int Gamma(double v) => (int)Math.Round(255 * Math.Clamp(!sigma ? v : v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055, 0, 1));
        (double R, double G, double B) Rgb(string hex) =>
            (Linear(Convert.ToInt32(hex[..2], 16)), Linear(Convert.ToInt32(hex[2..4], 16)), Linear(Convert.ToInt32(hex[4..6], 16)));
        var result = new List<(double, string, double)>();
        for (var k = 0; k < stops.Count; k++)
        {
            result.Add(stops[k]);
            if (k + 1 >= stops.Count || stops[k + 1].Pos - stops[k].Pos < 1e-6)
                continue;
            var (a, b) = (stops[k], stops[k + 1]);
            var (ca, cb) = (Rgb(a.Hex), Rgb(b.Hex));
            const int steps = 16;
            for (var n = 1; n < steps; n++)
            {
                var t = (double)n / steps;
                var s = sigma ? t * t * (3 - 2 * t) : t;
                var alpha = a.Alpha + (b.Alpha - a.Alpha) * s;
                var (wa, wb) = (a.Alpha * (1 - s), b.Alpha * s);
                var sum = wa + wb > 1e-9 ? wa + wb : 1;
                if (wa + wb <= 1e-9)
                    (wa, wb) = (1 - s, s);
                var hex = $"{Gamma((ca.R * wa + cb.R * wb) / sum):X2}{Gamma((ca.G * wa + cb.G * wb) / sum):X2}{Gamma((ca.B * wa + cb.B * wb) / sum):X2}";
                result.Add((a.Pos + (b.Pos - a.Pos) * t, hex, alpha));
            }
        }
        return result;
    }

    static XElement Solid(string hex, double? alpha)
    {
        var srgb = new XElement(Ns.A + "srgbClr", new XAttribute("val", hex));
        if (alpha is { } a && a < 1)
            srgb.Add(new XElement(Ns.A + "alpha", new XAttribute("val", (long)Math.Round(a * 100000))));
        return new XElement(Ns.A + "x", srgb);
    }

    /// <summary>
    /// A shape's outline: color, weight, dash, arrows; double and triple lines (v:stroke linestyle) and the line's
    /// opacity (measured with docx/local/vml-fx).
    /// </summary>
    static XElement Line(XElement shape, XElement? type)
    {
        var stroke = shape.Element(V + "stroke") ?? type?.Element(V + "stroke");
        var color = Color((string?)shape.Attribute("strokecolor") ?? (string?)type?.Attribute("strokecolor")) ?? "000000";
        var weight = Length((string?)shape.Attribute("strokeweight") ?? (string?)type?.Attribute("strokeweight")) ?? 0.75;
        var srgb = new XElement(Ns.A + "srgbClr", new XAttribute("val", color));
        if (Fraction((string?)stroke?.Attribute("opacity")) is { } alpha && alpha < 1)
            srgb.Add(new XElement(Ns.A + "alpha", new XAttribute("val", (long)Math.Round(alpha * 100000))));
        var line = new XElement(Ns.A + "ln", new XAttribute("w", Emu(weight)), new XElement(Ns.A + "solidFill", srgb));
        if ((string?)stroke?.Attribute("linestyle") switch
            {
                "thinThin" => "dbl",
                "thinThick" => "thinThick",
                "thickThin" => "thickThin",
                "thickBetweenThin" => "tri",
                _ => null,
            } is { } compound)
            line.SetAttributeValue("cmpd", compound);
        var dash = (string?)stroke?.Attribute("dashstyle") switch
        {
            "dash" or "shortdash" => "dash",
            "dot" or "shortdot" => "sysDot",
            "dashdot" or "shortdashdot" => "dashDot",
            "dashdotdot" or "shortdashdotdot" => "sysDashDotDot",
            "longdash" => "lgDash",
            "longdashdot" => "lgDashDot",
            "longdashdotdot" => "lgDashDotDot",
            _ => null,
        };
        if (dash != null)
            line.Add(new XElement(Ns.A + "prstDash", new XAttribute("val", dash)));
        // The stroke's miter limit, for how far its corners reach where Word places the shape (see LineReachBox).
        if (double.TryParse((string?)stroke?.Attribute("miterlimit"), NumberStyles.Float, CultureInfo.InvariantCulture, out var limit) && limit > 0)
            line.SetAttributeValue(MiterLimit, limit.ToString("R", CultureInfo.InvariantCulture));
        foreach (var (end, name) in new[] { ("startarrow", "headEnd"), ("endarrow", "tailEnd") })
        {
            if ((string?)stroke?.Attribute(end) is { } arrow && arrow != "none")
                line.Add(new XElement(Ns.A + name, new XAttribute("type", arrow switch { "block" => "triangle", "classic" => "stealth", "open" => "arrow", var v => v })));
        }
        // A line with arrowheads is at least 1 pt wide (measured with docx/local/vla-*: lines of no weight, 0.25, 0.75
        // and 1 pt with block, classic and open heads filled 1 pt wide, 2 pt 2 pt wide, the heads 6 pt long and wide;
        // without heads 0.75 pt; in modes 11 to 15 and without compatibility settings; docx/external/libreoffice/
        // extras-ooxmlexport/n751117, found by agent BH).
        if (weight < 1 && line.Elements().Any(e => e.Name.LocalName is "headEnd" or "tailEnd"))
            line.SetAttributeValue("w", Emu(1));
        return line;
    }

    /// <summary>
    /// A shape's shadow (v:shadow on): an unblurred copy in its color and opacity at its offset (2 pt right and down
    /// by default; measured with docx/local/vml-fx). Of a double shadow the first.
    /// </summary>
    static XElement? Shadow(XElement shape, XElement? type)
    {
        if (ShadowOf(shape, type) is not { } shadow)
            return null;
        var color = Color((string?)shadow.Attribute("color")) ?? "808080";
        var (x, y) = ShadowOffset((string?)shadow.Attribute("offset"), (2, 2));
        var srgb = new XElement(Ns.A + "srgbClr", new XAttribute("val", color));
        if (Fraction((string?)shadow.Attribute("opacity")) is { } alpha && alpha < 1)
            srgb.Add(new XElement(Ns.A + "alpha", new XAttribute("val", (long)Math.Round(alpha * 100000))));
        var direction = (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
        var result = new XElement(Ns.A + "outerShdw", new XAttribute("blurRad", 0), new XAttribute("dist", Emu(Math.Sqrt(x * x + y * y))),
            new XAttribute("dir", (long)Math.Round(direction * 60000)), new XAttribute("algn", "tl"), new XAttribute("rotWithShape", 0), srgb);
        // A perspective shadow (type perspective, matrix sxx, sxy, syx, syy about origin) as DrawingML's scaled and
        // skewed shadow about the corner or side its origin names (measured with docx/local/vps-doc: matrix ",46340f,,.5"
        // about the bottom left corner draws the shape half as high, its top 14.14 pt left, 2 pt off).
        if (Perspective(shadow) is { } skew)
        {
            static long Degrees(double tangent) => (long)Math.Round(Math.Atan(tangent) * 180 / Math.PI * 60000);
            var horizontal = skew.OriginX < -0.25 ? "l" : skew.OriginX > 0.25 ? "r" : "";
            var vertical = skew.OriginY < -0.25 ? "t" : skew.OriginY > 0.25 ? "b" : "";
            result.SetAttributeValue("sx", (long)Math.Round(skew.Xx * 100000));
            result.SetAttributeValue("sy", (long)Math.Round(skew.Yy * 100000));
            result.SetAttributeValue("kx", Degrees(skew.Xy));
            result.SetAttributeValue("ky", Degrees(skew.Yx));
            result.SetAttributeValue("algn", vertical + horizontal is { Length: > 0 } corner ? corner : "ctr");
        }
        return result;
    }

    /// <summary>
    /// A perspective shadow's transform (v:shadow type perspective: matrix "sxx,sxy,syx,syy,px,py", fixed-point "f" or
    /// plain numbers, empty ones 1, 0, 0, 1; origin as fractions of the shape from its middle), or null for other
    /// shadows. The perspective terms px and py are left out.
    /// </summary>
    static (double Xx, double Xy, double Yx, double Yy, double OriginX, double OriginY)? Perspective(XElement shadow)
    {
        if ((string?)shadow.Attribute("type") != "perspective" || (string?)shadow.Attribute("matrix") is not { } matrix)
            return null;
        var parts = matrix.Split(',');
        double Part(int i, double fallback) => Fraction(parts.ElementAtOrDefault(i)?.Trim()) ?? fallback;
        var (ox, oy) = Pair((string?)shadow.Attribute("origin"));
        return (Part(0, 1), Part(1, 0), Part(2, 0), Part(3, 1), ox, oy);
    }

    /// <summary>Two VML fractions ("0.5,0.5", "50%,0"), 0 where missing.</summary>
    static (double A, double B) Pair(string? value)
    {
        var parts = (value ?? "").Split(',');
        return (Fraction(parts.ElementAtOrDefault(0)) ?? 0, Fraction(parts.ElementAtOrDefault(1)) ?? 0);
    }

    static XElement? Crop(XElement image)
    {
        var (l, t, r, b) = (Fraction((string?)image.Attribute("cropleft")), Fraction((string?)image.Attribute("croptop")),
            Fraction((string?)image.Attribute("cropright")), Fraction((string?)image.Attribute("cropbottom")));
        if (l == null && t == null && r == null && b == null)
            return null;
        string Per(double? v) => ((long)Math.Round((v ?? 0) * 100000)).ToString(CultureInfo.InvariantCulture);
        return new XElement(Ns.A + "srcRect", new XAttribute("l", Per(l)), new XAttribute("t", Per(t)), new XAttribute("r", Per(r)), new XAttribute("b", Per(b)));
    }

    static bool Flag(XElement shape, string name, XElement? type, bool fallback)
    {
        var value = (string?)shape.Attribute(name) ?? (string?)type?.Attribute(name);
        return value == null ? fallback : value is "t" or "true" or "1" or "on";
    }

    /// <summary>
    /// A VML color: #rgb, #rrggbb or a name; "fill" for the fill color (<paramref name="fill"/>), darken(n) and
    /// lighten(n) after it scale it by n/255 towards black and white (Word's one-color gradients, measured with
    /// docx/local/bgpreset-one-*).
    /// </summary>
    static string? Color(string? value, string? fill = null)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "none")
            return null;
        var tokens = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hex = tokens[0].Equals("fill", StringComparison.OrdinalIgnoreCase) ? fill : Named(tokens[0]);
        foreach (var token in tokens.Skip(1))
        {
            var open = token.IndexOf('(');
            if (hex == null || !token.EndsWith(')') || open < 0
                || !double.TryParse(token[(open + 1)..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                continue;
            Func<double, double>? scale = token[..open].ToLowerInvariant() switch
            {
                "darken" => c => c * n / 255,
                "lighten" => c => 255 - (255 - c) * n / 255,
                _ => null,
            };
            if (scale != null)
                hex = string.Concat(Enumerable.Range(0, 3).Select(k => ((int)Math.Round(Math.Clamp(scale(Convert.ToInt32(hex.Substring(2 * k, 2), 16)), 0, 255))).ToString("X2")));
        }
        return hex;
    }

    static string? Named(string value)
    {
        if (value.StartsWith('#'))
        {
            var hex = value[1..];
            if (hex.Length == 3)
                hex = string.Concat(hex.Select(c => $"{c}{c}"));
            return hex.Length == 6 ? hex.ToUpperInvariant() : null;
        }
        return value.ToLowerInvariant() switch
        {
            "black" => "000000", "white" => "FFFFFF", "red" => "FF0000", "green" => "008000", "blue" => "0000FF",
            "yellow" => "FFFF00", "silver" => "C0C0C0", "gray" or "grey" => "808080", "maroon" => "800000",
            "purple" => "800080", "fuchsia" => "FF00FF", "lime" => "00FF00", "olive" => "808000", "navy" => "000080",
            "teal" => "008080", "aqua" => "00FFFF",
            _ => null,
        };
    }

    /// <summary>A VML fraction: "0.5", "50%" or "32768f" (in 65536ths).</summary>
    static double? Fraction(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (value.EndsWith('f') && double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            return f / 65536;
        if (value.EndsWith('%') && double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var p))
            return p / 100;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    /// <summary>A length of a VML style attribute in points (width, height …), or null.</summary>
    public static double? StyleLength(string? style, string name) => Length(Style(style).GetValueOrDefault(name));

    static Dictionary<string, string> Style(string? style)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in (style ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = part.IndexOf(':');
            if (colon > 0)
                map[part[..colon].Trim()] = part[(colon + 1)..].Trim();
        }
        return map;
    }

    /// <summary>A CSS length in points: pt, in, cm, mm, pc, px (0.75 pt), em (12 pt); a bare number is pixels.</summary>
    public static double? Length(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        value = value.Trim();
        var unit = value.Length > 2 && char.IsLetter(value[^1]) && char.IsLetter(value[^2]) ? value[^2..] : "";
        var number = unit.Length > 0 ? value[..^2] : value;
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
            return null;
        return unit switch
        {
            "pt" => n,
            "in" => n * 72,
            "cm" => n * 72 / 2.54,
            "mm" => n * 72 / 25.4,
            "pc" => n * 12,
            "em" => n * 12,
            _ => n * 0.75,
        };
    }

    /// <summary>The points of a polyline's points attribute ("x1,y1,x2,y2 …", lengths with or without units).</summary>
    static List<(double X, double Y)>? Points(string? value)
    {
        var numbers = (value ?? "").Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).Select(Length).ToList();
        if (numbers.Count < 4 || numbers.Any(n => n == null))
            return null;
        return Enumerable.Range(0, numbers.Count / 2).Select(i => (numbers[2 * i]!.Value, numbers[2 * i + 1]!.Value)).ToList();
    }

    static (double X, double Y) Point(string? value)
    {
        var parts = (value ?? "0,0").Split(',');
        return (Length(parts.ElementAtOrDefault(0)) ?? 0, Length(parts.ElementAtOrDefault(1)) ?? 0);
    }

    static long Emu(double points) => (long)Math.Round(points * EmuPerPoint);
}
