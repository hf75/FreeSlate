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

    /// <summary>The drawings of a w:pict or w:object: one per top-level VML shape.</summary>
    public static IEnumerable<XElement> ToDrawings(XElement pict)
    {
        var types = pict.Descendants(V + "shapetype").Where(t => t.Attribute("id") != null)
            .GroupBy(t => (string)t.Attribute("id")!).ToDictionary(g => g.Key, g => g.First());
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
        var type = (string?)shape.Attribute("type") is { } reference && types.TryGetValue(reference.TrimStart('#'), out var t) ? t : null;
        var content = shape.Name == V + "group" ? Group(shape, types, width, height, top: true) : Content(shape, type, width, height);
        if (content == null)
            return null;
        if (shape.Name != V + "group" && Bordered(content, shape, width, height) is { } framed)
            (content, width, height) = framed;
        var uri = content.Name.Namespace == Drawings.Pic ? "http://schemas.openxmlformats.org/drawingml/2006/picture"
            : content.Name.Namespace == Drawings.Wpg ? "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup"
            : "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";
        var graphic = new XElement(Ns.A + "graphic", new XElement(Ns.A + "graphicData", new XAttribute("uri", uri), content));
        var id = new string(((string?)shape.Attribute("id") ?? "").Where(char.IsDigit).ToArray());
        var docPr = new XElement(Drawings.Wp + "docPr", new XAttribute("id", id.Length is > 0 and < 10 ? id : "1"), new XAttribute("name", (string?)shape.Attribute("id") ?? ""));
        var extent = new XElement(Drawings.Wp + "extent", new XAttribute("cx", Emu(width)), new XAttribute("cy", Emu(height)));
        if (style.GetValueOrDefault("position") != "absolute")
            return new XElement(Drawings.Wp + "inline", extent, new XElement(Drawings.Wp + "effectExtent", new XAttribute("l", 0), new XAttribute("t", 0), new XAttribute("r", 0), new XAttribute("b", 0)), docPr, graphic);

        var z = long.TryParse(style.GetValueOrDefault("z-index"), out var zi) ? zi : 0;
        var anchor = new XElement(Drawings.Wp + "anchor",
            new XAttribute("distT", 0), new XAttribute("distB", 0), new XAttribute("distL", 114300), new XAttribute("distR", 114300),
            new XAttribute("simplePos", 0), new XAttribute("relativeHeight", Math.Abs(z)), new XAttribute("behindDoc", z < 0 ? 1 : 0),
            new XAttribute("locked", 0), new XAttribute("layoutInCell", 1), new XAttribute("allowOverlap", 1),
            new XElement(Drawings.Wp + "simplePos", new XAttribute("x", 0), new XAttribute("y", 0)),
            Position("positionH", style.GetValueOrDefault("mso-position-horizontal-relative"), style.GetValueOrDefault("mso-position-horizontal"),
                Length(style.GetValueOrDefault("margin-left")) ?? Length(style.GetValueOrDefault("left")) ?? 0, horizontal: true),
            Position("positionV", style.GetValueOrDefault("mso-position-vertical-relative"), style.GetValueOrDefault("mso-position-vertical"),
                Length(style.GetValueOrDefault("margin-top")) ?? Length(style.GetValueOrDefault("top")) ?? 0, horizontal: false),
            extent,
            new XElement(Drawings.Wp + "effectExtent", new XAttribute("l", 0), new XAttribute("t", 0), new XAttribute("r", 0), new XAttribute("b", 0)),
            Wrap(shape.Element(W10 + "wrap")),
            docPr, graphic);
        return anchor;
    }

    /// <summary>
    /// A VML group (v:group, catalog: vml.shapes) as a DrawingML group: its children's coordinates (coordorigin,
    /// coordsize) mapped onto its box, the children's left, top, width and height (or a line's from and to) in them;
    /// groups nest.
    /// </summary>
    static XElement Group(XElement group, Dictionary<string, XElement> types, double width, double height, bool top, (long X, long Y, long W, long H)? place = null)
    {
        static (long A, long B) Pair(string? value, (long, long) fallback)
        {
            var parts = (value ?? "").Split(',');
            return parts.Length == 2 && long.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var a)
                   && long.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? (a, b) : fallback;
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
            var type = (string?)child.Attribute("type") is { } reference && types.TryGetValue(reference.TrimStart('#'), out var t) ? t : null;
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
        static double Dots(double points) => Math.Max(0.12, Math.Floor(points / 0.12 + 1e-6) * 0.12);
        (string Type, double Width)? Side(string name) =>
            shape.Element(W10 + "border" + name) is { } b && (string?)b.Attribute("type") is { } kind && kind != "none"
            && double.TryParse((string?)b.Attribute("width"), NumberStyles.Float, CultureInfo.InvariantCulture, out var w) && w > 0
                ? (kind, Dots(w / 8)) : null;
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

    /// <summary>The DrawingML content of a shape: a picture for pictures alone, else a shape.</summary>
    static XElement? Content(XElement shape, XElement? type, double width, double height)
    {
        var image = shape.Element(V + "imagedata") ?? type?.Element(V + "imagedata");
        var spt = (string?)shape.Attribute(O + "spt") ?? (string?)type?.Attribute(O + "spt");
        var filled = Flag(shape, "filled", type, true);
        var stroked = Flag(shape, "stroked", type, true);
        var xfrm = Xfrm(width, height, Style((string?)shape.Attribute("style")));
        if (image != null && ((string?)image.Attribute(R + "id") ?? (string?)image.Attribute(O + "relid")) is { } relationship)
        {
            // A picture: a frame (t75) or any shape whose image fills it.
            var blipFill = new XElement(Drawings.Pic + "blipFill", new XElement(Ns.A + "blip", new XAttribute(R + "embed", relationship)));
            var crop = Crop(image);
            if (crop != null)
                blipFill.Add(crop);
            blipFill.Add(new XElement(Ns.A + "stretch", new XElement(Ns.A + "fillRect")));
            var spPr = new XElement(Drawings.Pic + "spPr", xfrm, new XElement(Ns.A + "prstGeom", new XAttribute("prst", "rect"), new XElement(Ns.A + "avLst")));
            if (stroked && shape.Attribute("strokecolor") != null || shape.Element(V + "stroke") != null)
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
        // WordArt (v:textpath): text stretched to the shape, as PowerPoint draws a textPlain warp.
        if ((shape.Element(V + "textpath") ?? type?.Element(V + "textpath")) is { } textpath && (string?)shape.Element(V + "textpath")?.Attribute("string") is { } text)
        {
            wsp.Add(new XElement(Drawings.Wps + "txbxWordArt", text, new XAttribute("style", (string?)textpath.Attribute("style") ?? "")));
        }
        else if (shape.Element(V + "textbox")?.Element(Wml.W + "txbxContent") is { } content)
        {
            wsp.Add(new XElement(Drawings.Wps + "txbx", new XElement(content)));
            var inset = ((string?)shape.Element(V + "textbox")!.Attribute("inset") ?? "").Split(',');
            double Inset(int i, double fallback) => i < inset.Length && Length(inset[i].Trim()) is { } v ? v : fallback;
            var anchor = Style((string?)shape.Attribute("style")).GetValueOrDefault("v-text-anchor") switch { "middle" => "ctr", "bottom" => "b", _ => "t" };
            wsp.Add(new XElement(Drawings.Wps + "bodyPr",
                new XAttribute("lIns", Emu(Inset(0, 7.2))), new XAttribute("tIns", Emu(Inset(1, 3.6))),
                new XAttribute("rIns", Emu(Inset(2, 7.2))), new XAttribute("bIns", Emu(Inset(3, 3.6))), new XAttribute("anchor", anchor)));
        }
        return wsp;
    }

    static XElement Xfrm(double width, double height, Dictionary<string, string> style)
    {
        var xfrm = new XElement(Ns.A + "xfrm",
            new XElement(Ns.A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
            new XElement(Ns.A + "ext", new XAttribute("cx", Emu(width)), new XAttribute("cy", Emu(height))));
        if (double.TryParse(style.GetValueOrDefault("rotation")?.TrimEnd('f', 'd', 'e', 'g'), NumberStyles.Float, CultureInfo.InvariantCulture, out var rotation) && rotation != 0)
            xfrm.SetAttributeValue("rot", (long)Math.Round(rotation * 60000));
        if (style.GetValueOrDefault("flip") is { } flip)
        {
            if (flip.Contains('x')) xfrm.SetAttributeValue("flipH", 1);
            if (flip.Contains('y')) xfrm.SetAttributeValue("flipV", 1);
        }
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
        if (preset == null && ((string?)shape.Attribute("path") ?? (string?)type?.Attribute("path")) is { } path
            && Path(path, (string?)shape.Attribute("coordsize") ?? (string?)type?.Attribute("coordsize")) is { } custom)
            return custom;
        var geometry = new XElement(Ns.A + "prstGeom", new XAttribute("prst", preset ?? "rect"), new XElement(Ns.A + "avLst"));
        if (preset == "roundRect" && (string?)shape.Attribute("arcsize") is { } arc)
        {
            // VML's arcsize is a fraction of the shorter side's half; DrawingML's adjustment the same in 1/50000.
            var fraction = arc.EndsWith('f') && double.TryParse(arc[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f / 65536
                : double.TryParse(arc.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p / (arc.EndsWith('%') ? 100 : 1) : 0.1667;
            geometry.Element(Ns.A + "avLst")!.Add(new XElement(Ns.A + "gd", new XAttribute("name", "adj"), new XAttribute("fmla", $"val {(long)Math.Round(fraction * 50000)}")));
        }
        return geometry;
    }

    /// <summary>A VML path (m, l, c, x, e, r, v, t, nf, ns) as a custom geometry in its coordinate size.</summary>
    static XElement? Path(string path, string? coordsize)
    {
        var size = (coordsize ?? "21600,21600").Split(',');
        long cw = size.Length > 0 && long.TryParse(size[0], out var a) ? a : 21600, ch = size.Length > 1 && long.TryParse(size[1], out var b) ? b : 21600;
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var c in path)
        {
            if (char.IsLetter(c))
            {
                if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
                current.Append(c);
                // Commands of two letters (nf, ns, qx …).
                if (tokens.Count > 0 && tokens[^1].Length == 1 && char.IsLetter(tokens[^1][0]) && "nq".Contains(tokens[^1][0]))
                {
                    tokens[^1] += c;
                    current.Clear();
                }
                continue;
            }
            if (c is ',' or ' ')
            {
                if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
                continue;
            }
            if (current.Length > 0 && char.IsLetter(current[0]))
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
            current.Append(c);
        }
        if (current.Length > 0) tokens.Add(current.ToString());

        var pathElement = new XElement(Ns.A + "path", new XAttribute("w", cw), new XAttribute("h", ch));
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
                    for (var i = 0; i + 1 < numbers.Count; i += 2)
                    {
                        (px, py) = (numbers[i], numbers[i + 1]);
                        pathElement.Add(new XElement(Ns.A + "lnTo", Pt(px, py)));
                    }
                    break;
                case "r":
                    for (var i = 0; i + 1 < numbers.Count; i += 2)
                    {
                        (px, py) = (px + numbers[i], py + numbers[i + 1]);
                        pathElement.Add(new XElement(Ns.A + "lnTo", Pt(px, py)));
                    }
                    break;
                case "c":
                    for (var i = 0; i + 5 < numbers.Count; i += 6)
                    {
                        pathElement.Add(new XElement(Ns.A + "cubicBezTo", Pt(numbers[i], numbers[i + 1]), Pt(numbers[i + 2], numbers[i + 3]), Pt(numbers[i + 4], numbers[i + 5])));
                        (px, py) = (numbers[i + 4], numbers[i + 5]);
                    }
                    break;
                case "v":
                    for (var i = 0; i + 5 < numbers.Count; i += 6)
                    {
                        pathElement.Add(new XElement(Ns.A + "cubicBezTo", Pt(px + numbers[i], py + numbers[i + 1]), Pt(px + numbers[i + 2], py + numbers[i + 3]), Pt(px + numbers[i + 4], py + numbers[i + 5])));
                        (px, py) = (px + numbers[i + 4], py + numbers[i + 5]);
                    }
                    break;
                case "x":
                    pathElement.Add(new XElement(Ns.A + "close"));
                    break;
            }
            numbers.Clear();
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
        }
        Flush();
        if (!pathElement.HasElements)
            return null;
        return new XElement(Ns.A + "custGeom", new XElement(Ns.A + "avLst"), new XElement(Ns.A + "gdLst"), new XElement(Ns.A + "ahLst"),
            new XElement(Ns.A + "cxnLst"), new XElement(Ns.A + "rect", new XAttribute("l", 0), new XAttribute("t", 0), new XAttribute("r", "r"), new XAttribute("b", "b")),
            new XElement(Ns.A + "pathLst", pathElement));

        static XElement Pt(long x, long y) => new(Ns.A + "pt", new XAttribute("x", x), new XAttribute("y", y));
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
            if ((string?)background.Attribute(Wml.W + "themeTint") is { } tint && int.TryParse(tint, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var t))
                scheme.Add(new XElement(Ns.A + "tint", new XAttribute("val", t * 100000 / 255)));
            if ((string?)background.Attribute(Wml.W + "themeShade") is { } shade && int.TryParse(shade, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var s))
                scheme.Add(new XElement(Ns.A + "shade", new XAttribute("val", s * 100000 / 255)));
            return new XElement(Ns.A + "solidFill", scheme);
        }
        return color is { Length: 6 } hex && hex != "auto" ? new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", hex))) : null;
    }

    /// <summary>
    /// A shape's fill (measured with docx/local/vml-fx): solid; a gradient from the fill color to color2 (or along
    /// its colors list) that runs, at the focus 0 Word writes by default, from color2 at the start of its angle (0: the
    /// top, 90: the left) to the fill color, at focus 100 % the other way, at focus 50 % from the fill color to color2
    /// in the middle and back, at -50 % the other way round; a radial gradient (gradientRadial) from its focus position
    /// outwards; o:opacity2 the opacity at color2; pictures stretched or tiled.
    /// </summary>
    static XElement Fill(XElement shape, XElement? type)
    {
        var fill = shape.Element(V + "fill") ?? type?.Element(V + "fill");
        var color = Color((string?)shape.Attribute("fillcolor") ?? (string?)type?.Attribute("fillcolor")) ?? "FFFFFF";
        var opacity = Fraction((string?)fill?.Attribute("opacity"));
        var kind = (string?)fill?.Attribute("type") ?? "solid";
        if (kind is "gradient" or "gradientRadial" or "gradientCenter" && Color((string?)fill?.Attribute("color2")) is { } color2)
        {
            var opacity1 = opacity ?? 1;
            var opacity2 = Fraction((string?)fill?.Attribute(O + "opacity2") ?? (string?)fill?.Attribute("opacity2")) ?? opacity1;
            // The colors from the fill color's end (0) to color2's (1).
            var colors = new List<(double At, string Hex, double Alpha)>();
            foreach (var entry in ((string?)fill?.Attribute("colors") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = entry.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && Fraction(parts[0]) is { } at && Color(parts[1]) is { } hex)
                    colors.Add((Math.Clamp(at, 0, 1), hex, opacity1 + (opacity2 - opacity1) * Math.Clamp(at, 0, 1)));
            }
            if (colors.Count < 2)
                colors = [(0, color, opacity1), (1, color2, opacity2)];
            var focus = Fraction((string?)fill?.Attribute("focus")) ?? 0;
            var stops = new List<(double Pos, string Hex, double Alpha)>();
            if (focus >= 0)
            {
                // From the fill color at 0 to color2 at the focus, back to the fill color at 1.
                stops.AddRange(colors.Select(c => (c.At * focus, c.Hex, c.Alpha)));
                stops.AddRange(colors.AsEnumerable().Reverse().Select(c => (1 - (1 - focus) * c.At, c.Hex, c.Alpha)));
            }
            else
            {
                var g = Math.Min(1, -focus);
                stops.AddRange(colors.AsEnumerable().Reverse().Select(c => (g * (1 - c.At), c.Hex, c.Alpha)));
                stops.AddRange(colors.Select(c => (g + (1 - g) * c.At, c.Hex, c.Alpha)));
            }
            var sigma = (string?)fill?.Attribute("method") is not ("none" or "linear");
            var list = new XElement(Ns.A + "gsLst", Blend(stops.OrderBy(s => s.Pos).ToList(), sigma).Select(s =>
                new XElement(Ns.A + "gs", new XAttribute("pos", (long)Math.Round(s.Pos * 100000)), Solid(s.Hex, s.Alpha).Elements().First())));
            if (kind is "gradientRadial" or "gradientCenter")
            {
                var (fx, fy) = Pair((string?)fill?.Attribute("focusposition"));
                var (fw, fh) = Pair((string?)fill?.Attribute("focussize"));
                string Per(double v) => ((long)Math.Round(v * 100000)).ToString(CultureInfo.InvariantCulture);
                return new XElement(Ns.A + "gradFill", list,
                    new XElement(Ns.A + "path", new XAttribute("path", "rect"),
                        new XElement(Ns.A + "fillToRect", new XAttribute("l", Per(fx)), new XAttribute("t", Per(fy)),
                            new XAttribute("r", Per(1 - fx - fw)), new XAttribute("b", Per(1 - fy - fh)))));
            }
            var angle = double.TryParse((string?)fill?.Attribute("angle"), NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees) ? degrees : 0;
            return new XElement(Ns.A + "gradFill", list,
                new XElement(Ns.A + "lin", new XAttribute("ang", (long)Math.Round(((90 - angle) % 360 + 360) % 360 * 60000)), new XAttribute("scaled", 0)));
        }
        if (kind is "frame" or "tile" && ((string?)fill?.Attribute(R + "id") ?? (string?)fill?.Attribute(O + "relid")) is { } picture)
        {
            return new XElement(Ns.A + "blipFill", new XElement(Ns.A + "blip", new XAttribute(R + "embed", picture)),
                kind == "tile" ? new XElement(Ns.A + "tile") : new XElement(Ns.A + "stretch", new XElement(Ns.A + "fillRect")));
        }
        return new XElement(Ns.A + "solidFill", Solid(color, opacity).Elements().First());

        // Word blends a VML gradient's colors in linear light along an S-curve, or in plain RGB along a straight line
        // where its method is none or linear, premultiplied by their opacity (measured with docx/local/vml-fx: orange to
        // blue and red, green and blue within 3 of 255, a color fading out keeps its hue): the stops with the blend
        // between them in steps.
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
        foreach (var (end, name) in new[] { ("startarrow", "headEnd"), ("endarrow", "tailEnd") })
        {
            if ((string?)stroke?.Attribute(end) is { } arrow && arrow != "none")
                line.Add(new XElement(Ns.A + name, new XAttribute("type", arrow switch { "block" => "triangle", "classic" => "stealth", "open" => "arrow", var v => v })));
        }
        return line;
    }

    /// <summary>
    /// A shape's shadow (v:shadow on): an unblurred copy in its color and opacity at its offset (2 pt right and down
    /// by default; measured with docx/local/vml-fx). Of a double shadow the first.
    /// </summary>
    static XElement? Shadow(XElement shape, XElement? type)
    {
        var shadow = shape.Element(V + "shadow") ?? type?.Element(V + "shadow");
        if (shadow == null || !((string?)shadow.Attribute("on") is "t" or "true" or "1" or "on"))
            return null;
        var color = Color((string?)shadow.Attribute("color")) ?? "808080";
        var offset = (string?)shadow.Attribute("offset");
        var (x, y) = offset == null ? (2.0, 2.0) : Point(offset.Contains(',') ? offset : offset + "," + offset);
        var srgb = new XElement(Ns.A + "srgbClr", new XAttribute("val", color));
        if (Fraction((string?)shadow.Attribute("opacity")) is { } alpha && alpha < 1)
            srgb.Add(new XElement(Ns.A + "alpha", new XAttribute("val", (long)Math.Round(alpha * 100000))));
        var direction = (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
        return new XElement(Ns.A + "outerShdw", new XAttribute("blurRad", 0), new XAttribute("dist", Emu(Math.Sqrt(x * x + y * y))),
            new XAttribute("dir", (long)Math.Round(direction * 60000)), new XAttribute("algn", "tl"), new XAttribute("rotWithShape", 0), srgb);
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

    /// <summary>A VML color: #rgb, #rrggbb, a name, or "name [index]" as Word writes theme colors; null for none.</summary>
    static string? Color(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "none")
            return null;
        value = value.Trim().Split(' ')[0];
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

    static (double X, double Y) Point(string? value)
    {
        var parts = (value ?? "0,0").Split(',');
        return (Length(parts.ElementAtOrDefault(0)) ?? 0, Length(parts.ElementAtOrDefault(1)) ?? 0);
    }

    static long Emu(double points) => (long)Math.Round(points * EmuPerPoint);
}
