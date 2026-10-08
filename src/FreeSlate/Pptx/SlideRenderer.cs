using System.Text;
using System.Xml.Linq;
using FreeSlate.Drawing;
using FreeSlate.Drawing.Geometry;
using FreeSlate.Html;

namespace FreeSlate.Pptx;

/// <summary>
/// Renders one slide: background, the master's and layout's non-placeholder shapes, then the slide's shapes.
/// Every shape becomes an absolutely positioned div with one CSS matrix, an SVG for geometry and paint and an
/// HTML layer for text.
/// </summary>
public sealed class SlideRenderer(SlideContext slide, SvgPaint paint)
{
    readonly TextRenderer text = new(slide, FreeSlate.Text.FontResolver.Default);
    TableRenderer Tables => tables ??= new TableRenderer(slide, paint, text);
    TableRenderer? tables;

    /// <summary>Maps child coordinates of a group to slide coordinates (EMU); Transform holds rotations and flips.</summary>
    readonly record struct Placement(Matrix Transform, double OffX, double OffY, double ScaleX, double ScaleY, double ChildX, double ChildY, Fill? GroupFill)
    {
        public static readonly Placement Root = new(Matrix.Identity, 0, 0, 1, 1, 0, 0, null);

        public (double X, double Y, double W, double H) Map(double x, double y, double w, double h) =>
            (OffX + (x - ChildX) * ScaleX, OffY + (y - ChildY) * ScaleY, w * ScaleX, h * ScaleY);
    }

    /// <summary>The slide as a section element; <paramref name="attributes"/> and <paramref name="appendix"/> (notes) are added as given.</summary>
    public string Render(string attributes = "", string appendix = "")
    {
        var width = Units.ToPx(slide.Presentation.SlideWidth);
        var height = Units.ToPx(slide.Presentation.SlideHeight);
        var html = new StringBuilder();
        html.Append($"<section class=\"slide\" id=\"slide-{slide.Number}\"{attributes} style=\"width:{Units.Css(width)}px;height:{Units.Css(height)}px\">");
        html.Append(Background(width, height));

        var showMaster = slide.Slide.Root.Bool("showMasterSp") ?? true;
        if (showMaster && (slide.Layout?.Root.Bool("showMasterSp") ?? true) && slide.Master != null)
            RenderTree(Tree(slide.Master), slide.MasterPart!, html, Placement.Root, isSlide: false);
        if (showMaster && slide.Layout != null)
            RenderTree(Tree(slide.Layout), slide.LayoutPart!, html, Placement.Root, isSlide: false);
        RenderTree(Tree(slide.Slide), slide.SlidePart, html, Placement.Root, isSlide: true);

        html.Append(appendix).Append("</section>");
        return html.ToString();
    }

    static XElement? Tree(XDocument document) => document.Root?.Element(Ns.P + "cSld")?.Element(Ns.P + "spTree");

    void RenderTree(XElement? tree, string part, StringBuilder html, Placement placement, bool isSlide)
    {
        foreach (var element in tree?.Elements() ?? [])
        {
            if (IsHidden(element))
                continue;   // catalog: shape.accessibility, hidden="1" shapes are not drawn
            switch (element.Name.LocalName)
            {
                case "sp" or "cxnSp" or "pic":
                    // Placeholders of layouts and masters are prompts and never drawn on slides.
                    if (isSlide || Placeholders.Ph(element) == null)
                        RenderShape(element, part, html, placement, isSlide);
                    break;
                case "grpSp":
                    RenderGroup(element, part, html, placement, isSlide);
                    break;
                case "graphicFrame":
                    if (isSlide || Placeholders.Ph(element) == null)
                        RenderGraphicFrame(element, part, html, placement);
                    break;
                // contentPart (ink): not rendered yet.
            }
        }
    }

    /// <summary>The shape and, for slide placeholders, the layout and master placeholders it inherits from.</summary>
    List<PartElement> Chain(XElement shape, string part, bool isSlide)
    {
        var chain = new List<PartElement> { new(shape, part) };
        if (!isSlide || Placeholders.Ph(shape) is not { } ph)
            return chain;
        if (Placeholders.InLayout(slide.Layout, ph) is { } layoutShape)
        {
            chain.Add(new PartElement(layoutShape, slide.LayoutPart!));
            ph = Placeholders.Ph(layoutShape)!;
        }
        if (Placeholders.InMaster(slide.Master, ph) is { } masterShape)
            chain.Add(new PartElement(masterShape, slide.MasterPart!));
        return chain;
    }

    static XElement? SpPr(XElement shape) => shape.Element(Ns.P + "spPr");

    void RenderShape(XElement shape, string part, StringBuilder html, Placement placement, bool isSlide)
    {
        var chain = Chain(shape, part, isSlide);
        var xfrm = chain.Select(s => SpPr(s.Element)?.Element(Ns.A + "xfrm")).FirstOrDefault(x => x?.Element(Ns.A + "ext") != null);
        if (xfrm == null)
            return;

        var (x, y, w, h, rotation, flipH, flipV) = ReadTransform(xfrm);
        var (bx, by, bw, bh) = placement.Map(x, y, w, h);
        var matrix = placement.Transform.Then(LocalMatrix(bx, by, bw, bh, rotation, flipH, flipV));

        var (definition, adjustments) = Geometry(chain);
        var geometry = GeometryEvaluator.Evaluate(definition, bw, bh, adjustments);
        if (shape.Element(Ns.P + "txXfrm") is { } txXfrm && txXfrm.Element(Ns.A + "ext") != null)
        {
            // SmartArt shapes carry their own text rectangle, in the same coordinates as the shape.
            var (tx, ty, tw, th, _, _, _) = ReadTransform(txXfrm);
            var (tbx, tby, tbw, tbh) = placement.Map(tx, ty, tw, th);
            geometry = geometry with
            {
                TextLeft = Units.ToPx(tbx - bx),
                TextTop = Units.ToPx(tby - by),
                TextRight = Units.ToPx(tbx - bx + tbw),
                TextBottom = Units.ToPx(tby - by + tbh),
            };
        }
        var style = chain.Select(s => s.Element.Element(Ns.P + "style")).FirstOrDefault(s => s != null);

        var fill = shape.Name.LocalName == "pic" ? PictureFill(shape, part, Units.ToPx(bw)) : ResolveFill(chain, style, placement.GroupFill);
        var outline = ResolveOutline(chain, style);
        var (pw, ph) = (Units.ToPx(bw), Units.ToPx(bh));

        var defs = new StringBuilder();
        var paths = new StringBuilder();
        var ends = outline is { Visible: true }
            ? LineEnds.Apply(outline, geometry.Paths, paint.Fill(outline.Fill, pw, ph, defs), paint.Stroke(outline with { Dash = "solid" }, pw, ph, defs))
            : "";
        foreach (var path in geometry.Paths)
        {
            var pathFill = path.Fill == "none" ? Fill.None : Shade(fill, path.Fill);
            var origin = (Units.ToPx(matrix.E), Units.ToPx(matrix.F));
            paths.Append($"<path d=\"{path.Data}\"{paint.Fill(pathFill, pw, ph, defs, rotation, origin)}{(path.Stroke ? paint.Stroke(outline, pw, ph, defs) : " stroke=\"none\"")}/>");
        }

        html.Append($"<div class=\"sp\"{Accessibility(shape, isSlide)} style=\"width:{Units.Css(pw)}px;height:{Units.Css(ph)}px;transform:{ToPx(matrix).Css()}\">");
        // A zero-sized SVG is not painted at all, so lines (height or width 0) get a 1 px viewport.
        var (sw, sh) = (Math.Max(pw, 1), Math.Max(ph, 1));
        html.Append($"<svg width=\"{Units.Css(sw)}\" height=\"{Units.Css(sh)}\" viewBox=\"0 0 {Units.Css(sw)} {Units.Css(sh)}\">");
        // Effects: the shape's effect list, else the theme effect style referenced by effectRef. Without a visible
        // fill or outline the text casts the effect.
        var effectFilter = SvgEffects.Filter(Effects(chain, style), slide.Colors, pw, ph, defs);
        var opaque = fill is not NoFill || outline is { Visible: true };
        if (defs.Length > 0)
            html.Append("<defs>").Append(defs).Append("</defs>");
        var shapeMarkup = paths.ToString() + ends;
        html.Append(effectFilter != null && opaque ? $"<g filter=\"url(#{effectFilter})\">{shapeMarkup}</g>" : shapeMarkup);
        if (shape.Name.LocalName == "sp")
        {
            var placeholder = Placeholders.Ph(shape);
            var textMarkup = text.Render(new TextChain
            {
                Shapes = chain,
                StyleKind = placeholder == null ? "other" : Placeholders.TextStyle(Placeholders.Type(placeholder)),
                FontRef = style?.Element(Ns.A + "fontRef"),
            }, geometry, pw, flipH ^ flipV);
            html.Append(effectFilter != null && !opaque && textMarkup.Length > 0 ? $"<g filter=\"url(#{effectFilter})\">{textMarkup}</g>" : textMarkup);
        }
        html.Append("</svg>");
        html.Append("</div>");
    }

    /// <summary>Graphic frames: SmartArt, charts and tables (OLE objects follow).</summary>
    void RenderGraphicFrame(XElement frame, string part, StringBuilder html, Placement placement)
    {
        var xfrm = frame.Element(Ns.P + "xfrm");
        var data = frame.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData");
        if (xfrm?.Element(Ns.A + "ext") == null || data == null)
            return;

        if (data.Elements().FirstOrDefault(e => e.Name.LocalName == "relIds") is { } relIds)
        {
            RenderDiagram(relIds, frame, part, html, placement);
            return;
        }

        if (data.Element(Ns.C + "chart")?.Attribute(Ns.R + "id") is { } chartId
            && slide.Presentation.Package.Relationship(part, chartId.Value) is { External: false } chartRel)
        {
            var (x, y, w, h, _, _, _) = ReadTransform(xfrm);
            var (bx, by, bw, bh) = placement.Map(x, y, w, h);
            var matrix = placement.Transform.Then(LocalMatrix(bx, by, bw, bh, 0, false, false));
            var (pw, ph) = (Units.ToPx(bw), Units.ToPx(bh));
            var svg = new Charts.ChartRenderer(slide, paint, text, chartRel.Target).Render(bw / Units.EmuPerPoint, bh / Units.EmuPerPoint, Units.ToPx(matrix.E), Units.ToPx(matrix.F));
            html.Append($"<div class=\"sp\"{Accessibility(frame, true)} style=\"width:{Units.Css(pw)}px;height:{Units.Css(ph)}px;transform:{ToPx(matrix).Css()}\">");
            html.Append($"<svg width=\"{Units.Css(Math.Max(pw, 1))}\" height=\"{Units.Css(Math.Max(ph, 1))}\" viewBox=\"0 0 {Units.Css(Math.Max(pw, 1))} {Units.Css(Math.Max(ph, 1))}\">{svg}</svg></div>");
            return;
        }

        if (data.Element(Ns.A + "tbl") is { } table)
        {
            var (x, y, w, h, rotation, flipH, flipV) = ReadTransform(xfrm);
            var (bx, by, bw, bh) = placement.Map(x, y, w, h);
            var (svg, width, height) = Tables.Render(table);
            var matrix = placement.Transform.Then(LocalMatrix(bx, by, Math.Max(bw, width * Units.EmuPerPixel), Math.Max(bh, height * Units.EmuPerPixel), rotation, flipH, flipV));
            html.Append($"<div class=\"sp\"{Accessibility(frame, true)} style=\"width:{Units.Css(width)}px;height:{Units.Css(height)}px;transform:{ToPx(matrix).Css()}\">");
            html.Append($"<svg width=\"{Units.Css(Math.Max(width, 1))}\" height=\"{Units.Css(Math.Max(height, 1))}\" viewBox=\"0 0 {Units.Css(Math.Max(width, 1))} {Units.Css(Math.Max(height, 1))}\">{svg}</svg></div>");
        }
    }

    static readonly XNamespace Dsp = "http://schemas.microsoft.com/office/drawing/2008/diagram";

    /// <summary>
    /// SmartArt (catalog: smartart.drawing): PowerPoint saves the laid-out shapes in a drawing part, linked from
    /// the data model's dsp:dataModelExt. Its shapes are drawn like slide shapes in the frame's coordinates.
    /// </summary>
    void RenderDiagram(XElement relIds, XElement frame, string part, StringBuilder html, Placement placement)
    {
        var package = slide.Presentation.Package;
        var dataPart = relIds.Attribute(Ns.R + "dm") is { } dm ? package.Relationship(part, dm.Value)?.Target : null;
        if (dataPart == null || !package.Exists(dataPart))
            return;
        var drawingId = slide.Presentation.Load(dataPart).Descendants(Dsp + "dataModelExt").FirstOrDefault()?.Str("relId");
        var drawingPart = drawingId != null ? package.Relationship(part, drawingId)?.Target : null;
        if (drawingPart == null || !package.Exists(drawingPart))
            return;

        var tree = slide.Presentation.Load(drawingPart).Root?.Element(Dsp + "spTree");
        if (tree == null)
            return;
        var xfrm = frame.Element(Ns.P + "xfrm");
        var (x, y, _, _, _, _, _) = ReadTransform(xfrm!);
        var (bx, by, _, _) = placement.Map(x, y, 0, 0);
        RenderTree(ToPresentationMl(tree), drawingPart, html, placement with { OffX = bx, OffY = by, ScaleX = 1, ScaleY = 1, ChildX = 0, ChildY = 0 }, isSlide: false);
    }

    /// <summary>The diagram drawing uses its own namespace for sp, spPr, style and txBody; map it to PresentationML.</summary>
    static XElement ToPresentationMl(XElement element) =>
        new(element.Name.Namespace == Dsp ? Ns.P + element.Name.LocalName : element.Name,
            element.Attributes(),
            element.Nodes().Select(n => n is XElement e ? ToPresentationMl(e) : n));

    void RenderGroup(XElement group, string part, StringBuilder html, Placement placement, bool isSlide)
    {
        var spPr = group.Element(Ns.P + "grpSpPr");
        var xfrm = spPr?.Element(Ns.A + "xfrm");
        if (xfrm?.Element(Ns.A + "ext") == null)
        {
            RenderTree(group, part, html, placement, isSlide);
            return;
        }

        var (x, y, w, h, rotation, flipH, flipV) = ReadTransform(xfrm);
        var (bx, by, bw, bh) = placement.Map(x, y, w, h);
        var chOff = xfrm.Element(Ns.A + "chOff");
        var chExt = xfrm.Element(Ns.A + "chExt");
        var chW = chExt.Long("cx") ?? w;
        var chH = chExt.Long("cy") ?? h;

        var (cx, cy) = (bx + bw / 2, by + bh / 2);
        var rotFlip = Matrix.Translate(cx, cy).Then(Matrix.Rotate(rotation)).Then(Matrix.Scale(flipH ? -1 : 1, flipV ? -1 : 1)).Then(Matrix.Translate(-cx, -cy));

        var groupFill = FillReader.FindFill(spPr) is { } f ? FillReader.Read(f, slide.Colors, slide.Images(part)) : null;
        if (groupFill is GroupFill)
            groupFill = placement.GroupFill;

        var child = new Placement(
            placement.Transform.Then(rotFlip),
            bx, by,
            chW != 0 ? bw / chW : 1,
            chH != 0 ? bh / chH : 1,
            chOff.Long("x") ?? x,
            chOff.Long("y") ?? y,
            groupFill ?? placement.GroupFill);
        html.Append($"<div class=\"grp\"{Accessibility(group, isSlide)}>");
        RenderTree(group, part, html, child, isSlide);
        html.Append("</div>");
    }

    static readonly XNamespace Adec = "http://schemas.microsoft.com/office/drawing/2017/decorative";

    static XElement? NonVisual(XElement shape) =>
        shape.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("nv", StringComparison.Ordinal))?.Elements().FirstOrDefault(e => e.Name.LocalName == "cNvPr");

    static bool IsHidden(XElement shape) => NonVisual(shape).Bool("hidden") == true;

    /// <summary>
    /// Shape id for the player (slide shapes only; ids repeat across master and layout) and accessibility
    /// attributes: alt text, title, decorative shapes hidden from assistive technology (catalog: shape.accessibility).
    /// </summary>
    static string Accessibility(XElement shape, bool isSlide)
    {
        var c = NonVisual(shape);
        if (c == null)
            return "";
        var sb = new StringBuilder();
        if (isSlide && c.Str("id") is { } id)
            sb.Append($" data-id=\"{System.Net.WebUtility.HtmlEncode(id)}\"");
        if (c.Descendants(Adec + "decorative").Any(d => d.Bool("val") != false))
            return sb.Append(" aria-hidden=\"true\"").ToString();
        if (c.Str("descr") is { Length: > 0 } descr)
            sb.Append($" role=\"img\" aria-label=\"{System.Net.WebUtility.HtmlEncode(descr)}\"");
        if (c.Str("title") is { Length: > 0 } title)
            sb.Append($" title=\"{System.Net.WebUtility.HtmlEncode(title)}\"");
        return sb.ToString();
    }

    static (double X, double Y, double W, double H, double Rotation, bool FlipH, bool FlipV) ReadTransform(XElement xfrm)
    {
        var off = xfrm.Element(Ns.A + "off");
        var ext = xfrm.Element(Ns.A + "ext");
        return (off.Long("x") ?? 0, off.Long("y") ?? 0, ext.Long("cx") ?? 0, ext.Long("cy") ?? 0,
            (xfrm.Long("rot") ?? 0) / 60000.0, xfrm.Bool("flipH") ?? false, xfrm.Bool("flipV") ?? false);
    }

    /// <summary>Maps the shape box (0..w, 0..h) into place: flip and rotate around the center, then translate.</summary>
    static Matrix LocalMatrix(double x, double y, double w, double h, double rotation, bool flipH, bool flipV) =>
        Matrix.Translate(x + w / 2, y + h / 2)
            .Then(Matrix.Rotate(rotation))
            .Then(Matrix.Scale(flipH ? -1 : 1, flipV ? -1 : 1))
            .Then(Matrix.Translate(-w / 2, -h / 2));

    /// <summary>The matrices work in EMU; the div's local coordinates are pixels, so only the translation changes.</summary>
    static Matrix ToPx(Matrix m) => m with { E = Units.ToPx(m.E), F = Units.ToPx(m.F) };

    static (GeometryDefinition Definition, IEnumerable<Guide> Adjustments) Geometry(List<PartElement> chain)
    {
        foreach (var s in chain)
        {
            var spPr = SpPr(s.Element);
            if (spPr?.Element(Ns.A + "prstGeom") is { } preset)
            {
                var definition = PresetGeometries.Get(preset.Str("prst") ?? "rect") ?? PresetGeometries.Get("rect")!;
                var adjustments = preset.Element(Ns.A + "avLst")?.Elements(Ns.A + "gd")
                    .Select(g => new Guide(g.Str("name") ?? "", g.Str("fmla") ?? "val 0")) ?? [];
                return (definition, adjustments.ToList());
            }
            if (spPr?.Element(Ns.A + "custGeom") is { } custom)
                return (GeometryDefinition.Parse(custom), []);
        }
        return (PresetGeometries.Get("rect")!, []);
    }

    XElement? Effects(List<PartElement> chain, XElement? style)
    {
        foreach (var s in chain)
        {
            if (SpPr(s.Element)?.Element(Ns.A + "effectLst") is { } own)
                return own;
        }
        var index = style?.Element(Ns.A + "effectRef").Int("idx") ?? 0;
        return index >= 1 && index <= slide.Theme.EffectStyles.Count ? slide.Theme.EffectStyles[index - 1].Element(Ns.A + "effectLst") : null;
    }

    Fill ResolveFill(List<PartElement> chain, XElement? style, Fill? groupFill)
    {
        foreach (var s in chain)
        {
            if (FillReader.FindFill(SpPr(s.Element)) is { } f)
            {
                var fill = FillReader.Read(f, slide.Colors, slide.Images(s.Part));
                return fill is GroupFill ? groupFill ?? Fill.None : fill;
            }
        }
        if (style?.Element(Ns.A + "fillRef") is { } fillRef && (fillRef.Int("idx") ?? 0) > 0
            && slide.Theme.FillStyle(fillRef.Int("idx")!.Value) is { } themed)
        {
            return FillReader.Read(themed, slide.Colors.WithPlaceholder(slide.Colors.ResolveChild(fillRef)));
        }
        return Fill.None;
    }

    Outline? ResolveOutline(List<PartElement> chain, XElement? style)
    {
        var layers = new List<(XElement, ColorContext)>();
        if (style?.Element(Ns.A + "lnRef") is { } lnRef && (lnRef.Int("idx") ?? 0) > 0
            && slide.Theme.LineStyle(lnRef.Int("idx")!.Value) is { } themed)
        {
            layers.Add((themed, slide.Colors.WithPlaceholder(slide.Colors.ResolveChild(lnRef))));
        }
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            if (SpPr(chain[i].Element)?.Element(Ns.A + "ln") is { } ln)
                layers.Add((ln, slide.Colors));
        }
        return OutlineReader.Merge(layers);
    }

    Fill PictureFill(XElement pic, string part, double displayWidth)
    {
        var blipFill = pic.Element(Ns.P + "blipFill");
        var blip = blipFill?.Element(Ns.A + "blip");
        var svg = blip?.Descendants(Ns.Asvg + "svgBlip").FirstOrDefault()?.Attribute(Ns.R + "embed")?.Value;
        var embed = blip?.Attribute(Ns.R + "embed")?.Value;
        var image = (svg != null ? slide.Presentation.Image(part, svg) : null) ?? (embed != null ? slide.Presentation.Image(part, embed, displayWidth) : null);
        if (image == null)
            return Fill.None;
        var stretch = blipFill!.Element(Ns.A + "stretch");
        return new PictureFill(image, FillReader.Rect(blipFill.Element(Ns.A + "srcRect")), FillReader.Rect(stretch?.Element(Ns.A + "fillRect")), blipFill.Element(Ns.A + "tile") != null);
    }

    /// <summary>Path fill modes of preset geometries that shade the fill (e.g. the top of a cylinder).</summary>
    static Fill Shade(Fill fill, string mode)
    {
        if (fill is not SolidFill solid || mode == "norm")
            return fill;
        var c = solid.Color;
        return new SolidFill(mode switch
        {
            "darken" => new RgbColor(c.R * 0.6, c.G * 0.6, c.B * 0.6, c.A),
            "darkenLess" => new RgbColor(c.R * 0.8, c.G * 0.8, c.B * 0.8, c.A),
            "lighten" => RgbColor.Mix(c, RgbColor.White.WithAlpha(c.A), 0.4),
            "lightenLess" => RgbColor.Mix(c, RgbColor.White.WithAlpha(c.A), 0.2),
            _ => c,
        });
    }

    string Background(double width, double height)
    {
        var fill = Fill.None as Fill;
        foreach (var (document, part) in new[] { (slide.Slide, slide.SlidePart), (slide.Layout, slide.LayoutPart), (slide.Master, slide.MasterPart) })
        {
            var bg = document?.Root?.Element(Ns.P + "cSld")?.Element(Ns.P + "bg");
            if (bg == null)
                continue;
            if (bg.Element(Ns.P + "bgPr") is { } bgPr && FillReader.FindFill(bgPr) is { } f)
                fill = FillReader.Read(f, slide.Colors, slide.Images(part!));
            else if (bg.Element(Ns.P + "bgRef") is { } bgRef && slide.Theme.FillStyle(bgRef.Int("idx") ?? 0) is { } themed)
                fill = FillReader.Read(themed, slide.Colors.WithPlaceholder(slide.Colors.ResolveChild(bgRef)));
            break;
        }
        if (fill is NoFill)
            fill = new SolidFill(RgbColor.White);

        var defs = new StringBuilder();
        var paintAttributes = paint.Fill(fill, width, height, defs);
        return $"<svg class=\"bg\" width=\"{Units.Css(width)}\" height=\"{Units.Css(height)}\">" +
               (defs.Length > 0 ? $"<defs>{defs}</defs>" : "") +
               $"<rect width=\"{Units.Css(width)}\" height=\"{Units.Css(height)}\"{paintAttributes}/></svg>";
    }
}
