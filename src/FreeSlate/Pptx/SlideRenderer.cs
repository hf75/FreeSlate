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
    readonly TextRenderer text = new(slide, FreeSlate.Text.FontResolver.Default) { Paint = paint };
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

        // ActiveX controls are drawn from their stored pictures (catalog: picture.ole).
        foreach (var control in slide.Slide.Root?.Element(Ns.P + "cSld")?.Element(Ns.P + "controls")?.Elements(Ns.P + "control") ?? [])
        {
            if (control.Element(Ns.P + "pic") is { } preview)
                RenderShape(preview, slide.SlidePart, html, Placement.Root, isSlide: true);
        }

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
        // A shape filled with the slide's background shows the part of the background behind it.
        if (shape.Name.LocalName == "sp" && shape.Bool("useBgFill") == true)
        {
            var toShape = ToPx(matrix).Invert();
            fill = new SlideFill(SlideBackground(), Units.ToPx(slide.Presentation.SlideWidth), Units.ToPx(slide.Presentation.SlideHeight),
                (toShape.A, toShape.B, toShape.C, toShape.D, toShape.E, toShape.F));
        }
        var outline = ResolveOutline(chain, style);
        var (pw, ph) = (Units.ToPx(bw), Units.ToPx(bh));

        var defs = new StringBuilder();
        var paths = new StringBuilder();
        var origin = (Units.ToPx(matrix.E), Units.ToPx(matrix.F));
        var ends = outline is { Visible: true }
            ? LineEnds.Apply(outline, geometry.Paths, paint.Fill(outline.Fill, pw, ph, defs, rotation, origin), paint.Stroke(outline with { Dash = "solid" }, pw, ph, defs, origin))
            : "";
        // A fill overlay blends a second fill over the shape's fill, not its outline (catalog: effect.blur).
        var effects = Effects(chain, style);
        var overlay = effects?.Element(Ns.A + "fillOverlay") is { } fillOverlay && FillReader.FindFill(fillOverlay) is { } overlayFill
            ? (Fill: FillReader.Read(overlayFill, slide.Colors, slide.Images(part), slide.ImageSizes(part)), Blend: fillOverlay.Str("blend") ?? "over")
            : ((Fill Fill, string Blend)?)null;
        // Picture borders lie outside the picture (measured: a 6 pt border ends where the picture begins).
        var outsetBorder = shape.Name.LocalName == "pic" && outline is { Visible: true } && ReferenceEquals(definition, PresetGeometries.Get("rect"));
        // A picture can have a fill of its own behind it, visible through its transparent parts.
        var background = shape.Name.LocalName == "pic" ? ResolveFill(chain, style, placement.GroupFill) : Fill.None;
        var axisAligned = IsAxisAligned(ToPx(matrix));
        foreach (var path in geometry.Paths)
        {
            var pathFill = path.Fill == "none" ? Fill.None : Shade(fill, path.Fill);
            // PowerPoint draws axis-aligned rectangles and lines without antialiasing: every edge goes to the
            // nearest pixel, which is what crispEdges does (measured on fills and outlines at fractional positions).
            var crisp = axisAligned && IsRectilinear(path) ? " shape-rendering=\"crispEdges\"" : "";
            if (background is not NoFill && path.Fill != "none")
                paths.Append($"<path d=\"{path.Data}\"{paint.Fill(background, pw, ph, defs, rotation, origin)} stroke=\"none\"{crisp}/>");
            var compound = path.Stroke && outline is { Visible: true } && CompoundLines.Parts(outline.Compound) != null;
            var stroke = path.Stroke && !outsetBorder && !compound ? paint.Stroke(outline, pw, ph, defs, origin) : " stroke=\"none\"";
            if (overlay is var (overlayPaint, blend) && pathFill is not NoFill)
            {
                var mode = blend switch { "mult" => "multiply", "screen" => "screen", "darken" => "darken", "lighten" => "lighten", _ => "normal" };
                paths.Append($"<g style=\"isolation:isolate\"><path d=\"{path.Data}\"{paint.Fill(pathFill, pw, ph, defs, rotation, origin)} stroke=\"none\"{crisp}/>");
                paths.Append($"<path d=\"{path.Data}\"{paint.Fill(overlayPaint, pw, ph, defs, rotation, origin)} stroke=\"none\" style=\"mix-blend-mode:{mode}\"{crisp}/></g>");
                if (stroke != " stroke=\"none\"")
                    paths.Append($"<path d=\"{path.Data}\" fill=\"none\"{stroke}{crisp}/>");
            }
            else
            {
                paths.Append($"<path d=\"{path.Data}\"{paint.Fill(pathFill, pw, ph, defs, rotation, origin)}{stroke}{crisp}/>");
            }
            if (compound)
                paths.Append(CompoundLines.Strokes(path, outline!, width => paint.Stroke(outline! with { WidthEmu = width * Units.EmuPerPixel, Compound = "sng" }, pw, ph, defs, origin)));
        }
        if (outsetBorder)
        {
            var half = Units.ToPx(outline!.WidthEmu) / 2;
            var crisp = axisAligned ? " shape-rendering=\"crispEdges\"" : "";
            paths.Append($"<path d=\"M{Units.Css(-half)} {Units.Css(-half)}H{Units.Css(pw + half)}V{Units.Css(ph + half)}H{Units.Css(-half)}Z\" fill=\"none\"{paint.Stroke(outline with { Join = "miter" }, pw, ph, defs, origin)}{crisp}/>");
        }

        // A 3D camera (catalog: effect.scene3d) turns the drawn shape about its center: the outer div keeps the
        // shape's unturned box (the player animates it) and holds the extruded body, an inner div projects the
        // front face with the camera.
        var scene3d = Scene3D(chain, style);
        var camera = Camera.Read(scene3d);
        var shape3d = camera != null ? Shape3D(chain, style) : null;
        if (camera != null && shape3d.Long("z") is { } offset)
            camera = camera with { Offset = Units.ToPx(offset) };
        var lighting = Lighting.For(scene3d, shape3d);
        var (topWidth, topHeight, topProfile) = Bevel3D(shape3d?.Element(Ns.A + "bevelT"), pw, ph);
        var (bottomWidth, bottomHeight, bottomProfile) = Bevel3D(shape3d?.Element(Ns.A + "bevelB"), pw, ph);
        var beveled = topWidth > 0;
        var depth = Units.ToPx(shape3d.Long("extrusionH") ?? 0);
        var contourWidth = Units.ToPx(shape3d.Long("contourW") ?? 0);
        // The contour does not reach in front of the shape's plane: unless the top bevel is as high as the contour's
        // radius, the body moves back by the difference (measured).
        if (camera != null && contourWidth / 2 > topHeight)
            camera = camera with { Offset = camera.Offset - (contourWidth / 2 - topHeight) };
        // The shape's line is part of the 3D body (measured): the body reaches to the line's outer edge, and the line
        // colors the sides (unless the extrusion has a color of its own), the outer band of the bevels and the back.
        var lineWidth = outline is { Visible: true } ? Units.ToPx(outline.WidthEmu) : 0;
        var lineColor = lineWidth > 0 ? Fill.MainColor(outline!.Fill) : null;
        List<List<(double X, double Y)>>? solidOutlines = null;
        var texturedBevel = false;
        var bevelShading = "";
        // The legacyWireframe material draws the body's edges instead of its faces (the text stays).
        var wireframe = shape3d?.Str("prstMaterial") == "legacyWireframe";
        var solidBody = camera is { Flat: false } || camera != null && (beveled || contourWidth > 0.01 || wireframe);
        if (solidBody)
        {
            var box = ToPx(placement.Transform.Then(Matrix.Translate(bx, by)));
            html.Append($"<div class=\"sp\"{Accessibility(shape, isSlide, part)} style=\"width:{Units.Css(pw)}px;height:{Units.Css(ph)}px;transform:{box.Css()}\">");
            var faceColor = Fill.MainColor(fill) ?? RgbColor.FromHex("808080");
            var extrusionColor = shape3d?.Element(Ns.A + "extrusionClr") is { } extrusionClr ? slide.Colors.ResolveChild(extrusionClr) : null;
            var solid = new Solid(camera!, lighting, pw, ph, rotation, flipH, flipV);
            solidOutlines = Solid.Outlines(geometry, lineWidth / 2);
            // The extrusion runs from the top bevel's outer edge backwards; the bottom bevel closes it at the back.
            var (front, back) = (-topHeight, -topHeight - depth);
            if (wireframe)
                solid.Wireframe(solidOutlines, front, camera!.Flat ? front : back);
            else if (!camera!.Flat)
            {
                if (depth > 0.01)
                    solid.Extrusion(solidOutlines, front, back, extrusionColor ?? lineColor ?? faceColor);
                if (bottomWidth > 0)
                    solid.Bevel(solidOutlines, bottomWidth, bottomHeight, bottomProfile, faceColor, back, bottom: true, lineColor, lineWidth);
                if (depth > 0.01 || beveled || bottomWidth > 0)
                {
                    var backOutlines = bottomWidth > 0 ? solidOutlines.Select(o => Solid.Inset(o, bottomWidth)).ToList() : solidOutlines;
                    solid.Back(backOutlines, back - bottomHeight, faceColor, lineWidth > bottomWidth ? lineColor : null, lineWidth - bottomWidth);
                }
            }
            // Pictures and gradients keep their texture on the bevel: it is shaded over the unclipped front face.
            texturedBevel = beveled && fill is not (SolidFill or NoFill);
            if (beveled && !wireframe)
                solid.Bevel(solidOutlines, topWidth, topHeight, topProfile, texturedBevel ? null : faceColor, line: lineColor, lineWidth: lineWidth);
            if (contourWidth > 0.01 && !wireframe)
                solid.Contour(solidOutlines, front, back, contourWidth, Solid.Rise(topWidth, topHeight, topProfile), Solid.Rise(bottomWidth, bottomHeight, bottomProfile));
            // A 3D body casts its shadow and glow from its back, the outline at the back seen by the camera (measured).
            var (backData, backBounds) = solid.Projected(solidOutlines, back);
            var outerDefs = new StringBuilder();
            if (backData.Length > 0 && SvgEffects.Filter(effects, slide.Colors, pw, ph, outerDefs, parts: SvgEffects.Parts.Outer, region: backBounds) is { } outerFilter)
                html.Append($"<svg class=\"sp\" width=\"{Units.Css(Math.Max(pw, 1))}\" height=\"{Units.Css(Math.Max(ph, 1))}\" style=\"overflow:visible\"><defs>{outerDefs}</defs><path d=\"{backData}\" fill=\"#000\" filter=\"url(#{outerFilter})\"/></svg>");
            var solidMarkup = solid.Markup();
            if (solidMarkup.Length > 0)
                html.Append($"<svg class=\"sp\" width=\"{Units.Css(Math.Max(pw, 1))}\" height=\"{Units.Css(Math.Max(ph, 1))}\" style=\"overflow:visible\">{solidMarkup}</svg>");
            var contourColor = shape3d?.Element(Ns.A + "contourClr") is { } contourClr ? slide.Colors.ResolveChild(contourClr) : null;
            bevelShading = solid.OverlayMarkup(contourColor ?? RgbColor.Black);
            // A solid body hides its front face from behind: the back face shows there.
            var hidden = (depth > 0.01 || beveled || bottomWidth > 0) && !wireframe ? ";backface-visibility:hidden" : "";
            html.Append($"<div class=\"sp\" style=\"width:{Units.Css(pw)}px;height:{Units.Css(ph)}px;transform:{Projection(camera, pw, ph, rotation, flipH, flipV)}{hidden}\">");
        }
        else
        {
            html.Append($"<div class=\"sp\"{Accessibility(shape, isSlide, part)} style=\"width:{Units.Css(pw)}px;height:{Units.Css(ph)}px;transform:{ToPx(matrix).Css()}\">");
        }
        // A zero-sized SVG is not painted at all, so lines (height or width 0) get a 1 px viewport.
        var (sw, sh) = (Math.Max(pw, 1), Math.Max(ph, 1));
        html.Append($"<svg width=\"{Units.Css(sw)}\" height=\"{Units.Css(sh)}\" viewBox=\"0 0 {Units.Css(sw)} {Units.Css(sh)}\">");
        // A hyperlink or action on the whole shape (catalog: interactivity.hyperlinks).
        var shapeLink = Links.Resolve(NonVisual(shape)?.Element(Ns.A + "hlinkClick"), part, slide.Presentation);
        if (shapeLink != null)
            html.Append(Links.Open(shapeLink));
        // Effects: the shape's effect list, else the theme effect style referenced by effectRef. Without a visible
        // fill or outline the text casts the effect.
        var effectFilter = SvgEffects.Filter(effects, slide.Colors, pw, ph, defs, stroke: outline is { Visible: true } ? Units.ToPx(outline.WidthEmu) : 0,
            parts: solidBody ? SvgEffects.Parts.Shape : SvgEffects.Parts.All, frame: matrix);
        var opaque = fill is not NoFill || outline is { Visible: true };
        var body = new StringBuilder();
        var shapeMarkup = wireframe && solidBody ? "" : paths.ToString() + ends;
        if (beveled && !texturedBevel && solidOutlines != null && shapeMarkup.Length > 0)
        {
            // The fill shows on the front face inside the bevel; the line lies on the bevel.
            var clip = $"bf{++litFilters}";
            defs.Append($"<clipPath id=\"{clip}\"><path d=\"{Solid.PathData(solidOutlines.Select(o => Solid.Inset(o, topWidth)))}\"/></clipPath>");
            shapeMarkup = $"<g clip-path=\"url(#{clip})\">{shapeMarkup}</g>";
        }
        // The shape's own drawing is a group of its own when it has text: animations can build the background and
        // the text separately (catalog: animation.build).
        var hasText = shape.Name.LocalName == "sp" && shape.Element(Ns.P + "txBody")?.Descendants(Ns.A + "t").Any() == true;
        if (hasText && shapeMarkup.Length > 0)
            shapeMarkup = $"<g data-bg=\"\">{shapeMarkup}</g>";
        // A scaled, skewed or flipped shadow is a transformed copy of the shape behind it.
        if (!solidBody && opaque && shapeMarkup.Length > 0 && SvgEffects.Shadow(effects, slide.Colors, pw, ph, defs, matrix) is { } shadowCopy)
            body.Append($"<g filter=\"url(#{shadowCopy.Filter})\"><g transform=\"{shadowCopy.Transform.Css()}\">{shapeMarkup}</g></g>");
        body.Append(effectFilter != null && opaque ? $"<g filter=\"url(#{effectFilter})\">{shapeMarkup}</g>" : shapeMarkup);
        if (shape.Name.LocalName == "sp")
        {
            var placeholder = Placeholders.Ph(shape);
            // Upright text (bodyPr upright) does not turn with its shape: it is laid out about the shape's center,
            // with width and height swapped when the shape stands on its side (45° to 135°, 225° to 315°; measured).
            var upright = chain.Select(s => s.Element.Element(Ns.P + "txBody")?.Element(Ns.A + "bodyPr")?.Attribute("upright")?.Value)
                .FirstOrDefault(v => v != null) is "1" or "true";
            var turn = ((rotation % 360) + 360) % 360;
            var textGeometry = geometry;
            if (upright && turn is >= 45 and < 135 or >= 225 and < 315)
            {
                var (cx, cy) = ((geometry.TextLeft + geometry.TextRight) / 2, (geometry.TextTop + geometry.TextBottom) / 2);
                var (halfWidth, halfHeight) = ((geometry.TextBottom - geometry.TextTop) / 2, (geometry.TextRight - geometry.TextLeft) / 2);
                textGeometry = geometry with { TextLeft = cx - halfWidth, TextRight = cx + halfWidth, TextTop = cy - halfHeight, TextBottom = cy + halfHeight };
            }
            var textMarkup = text.Render(new TextChain
            {
                Shapes = chain,
                StyleKind = placeholder == null ? "other" : Placeholders.TextStyle(Placeholders.Type(placeholder)),
                FontRef = style?.Element(Ns.A + "fontRef"),
            }, textGeometry, pw, flipH ^ flipV);
            if (upright && turn != 0 && textMarkup.Length > 0)
                textMarkup = $"<g transform=\"rotate({Units.Css(-turn)} {Units.Css(pw / 2)} {Units.Css(ph / 2)})\">{textMarkup}</g>";
            if (shapeLink != null)
                textMarkup = UnlinkText(textMarkup);   // links cannot nest
            body.Append(effectFilter != null && !opaque && textMarkup.Length > 0 ? $"<g filter=\"url(#{effectFilter})\">{textMarkup}</g>" : textMarkup);
        }
        var content = SvgEffects.Reflection(effects, body.ToString(), (0, 0, pw, ph), defs, matrix);
        if (camera != null && content.Length > 0)
        {
            // The front face is lit like the rest of the 3D shape (its normal faces the front).
            var (diffuse, specular) = lighting.Shade((0, 0, 1), camera.View((0, 0, 0)));
            var lit = $"l{++litFilters}";
            defs.Append($"<filter id=\"{lit}\" color-interpolation-filters=\"sRGB\"><feColorMatrix type=\"matrix\" values=\"" +
                        $"{Units.Css4(diffuse.R)} 0 0 0 {Units.Css4(specular.R)} 0 {Units.Css4(diffuse.G)} 0 0 {Units.Css4(specular.G)} 0 0 {Units.Css4(diffuse.B)} 0 {Units.Css4(specular.B)} 0 0 0 1 0\"/></filter>");
            content = $"<g filter=\"url(#{lit})\">{content}</g>";
        }
        if (defs.Length > 0)
            html.Append("<defs>").Append(defs).Append("</defs>");
        html.Append(content);
        if (shapeLink != null)
            html.Append("</a>");
        html.Append("</svg>");
        if (shape.Name.LocalName == "pic")
            html.Append(Media(shape, part));
        if (solidBody)
        {
            html.Append("</div>");
            if (bevelShading.Length > 0)
                html.Append($"<svg class=\"sp\" width=\"{Units.Css(Math.Max(pw, 1))}\" height=\"{Units.Css(Math.Max(ph, 1))}\" style=\"overflow:visible\">{bevelShading}</svg>");
            html.Append("</div>");
        }
        else
        {
            html.Append("</div>");
        }
    }

    /// <summary>
    /// The CSS transform of a shape's drawing under a 3D camera, in the shape's own pixels: the shape's rotation and
    /// flips in its plane, then the camera's turn and projection about its center.
    /// </summary>
    static string Projection(Camera camera, double w, double h, double rotation, bool flipH, bool flipV)
    {
        var plane = Matrix.Rotate(rotation).Then(Matrix.Scale(flipH ? -1 : 1, flipV ? -1 : 1)).Then(Matrix.Translate(-w / 2, -h / 2));
        // Column-major 4×4 of the plane's 2D transform.
        double[] local = [plane.A, plane.B, 0, 0, plane.C, plane.D, 0, 0, 0, 0, 1, 0, plane.E, plane.F, 0, 1];
        var projection = camera.Matrix3D();
        double[] center = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, w / 2, h / 2, 0, 1];
        var m = Multiply(center, Multiply(projection, local));
        return "matrix3d(" + string.Join(",", m.Select(v => Units.Css4(v))) + ")";

        static double[] Multiply(double[] a, double[] b)
        {
            // Column-major: (a·b)[c * 4 + r] = Σ a[k * 4 + r] · b[c * 4 + k].
            var result = new double[16];
            for (var c = 0; c < 4; c++)
                for (var r = 0; r < 4; r++)
                    for (var k = 0; k < 4; k++)
                        result[c * 4 + r] += a[k * 4 + r] * b[c * 4 + k];
            return result;
        }
    }

    static int litFilters;

    /// <summary>A bevel's width, height and profile in pixels: at most half the shape's smaller side wide (measured).</summary>
    static (double Width, double Height, double[] Profile) Bevel3D(XElement? bevel, double w, double h)
    {
        var profile = BevelProfiles.Get(bevel?.Str("prst"));
        if (bevel == null)
            return (0, 0, profile);
        var width = Math.Min(Units.ToPx(bevel.Long("w") ?? 76200), Math.Min(w, h) / 2);
        var height = Units.ToPx(bevel.Long("h") ?? 76200);
        return width > 0.01 && height > 0.01 ? (width, height, profile) : (0, 0, profile);
    }

    /// <summary>The 3D shape properties: its own a:sp3d, else the ones of the theme effect style it refers to.</summary>
    XElement? Shape3D(List<PartElement> chain, XElement? style)
    {
        foreach (var s in chain)
        {
            if (SpPr(s.Element)?.Element(Ns.A + "sp3d") is { } own)
                return own;
        }
        var index = style?.Element(Ns.A + "effectRef").Int("idx") ?? 0;
        return index >= 1 && index <= slide.Theme.EffectStyles.Count ? slide.Theme.EffectStyles[index - 1].Element(Ns.A + "sp3d") : null;
    }

    /// <summary>The 3D scene of a shape: its own a:scene3d, else the one of the theme effect style it refers to.</summary>
    XElement? Scene3D(List<PartElement> chain, XElement? style)
    {
        foreach (var s in chain)
        {
            if (SpPr(s.Element)?.Element(Ns.A + "scene3d") is { } own)
                return own;
        }
        var index = style?.Element(Ns.A + "effectRef").Int("idx") ?? 0;
        return index >= 1 && index <= slide.Theme.EffectStyles.Count ? slide.Theme.EffectStyles[index - 1].Element(Ns.A + "scene3d") : null;
    }

    static readonly XNamespace P14 = "http://schemas.microsoft.com/office/powerpoint/2010/main";

    /// <summary>
    /// The audio or video of a picture (catalog: picture.media): an HTML media element over the poster picture,
    /// hidden until the player plays it. The p14:media part is preferred; trimming and fades travel as data
    /// attributes.
    /// </summary>
    string Media(XElement pic, string part)
    {
        var nvPr = pic.Element(Ns.P + "nvPicPr")?.Element(Ns.P + "nvPr");
        if (nvPr == null)
            return "";
        var file = nvPr.Elements().FirstOrDefault(e => e.Name.LocalName is "videoFile" or "audioFile" or "wavAudioFile" or "quickTimeFile");
        var media = nvPr.Descendants(P14 + "media").FirstOrDefault();
        if (file == null && media == null)
            return "";
        var video = file?.Name.LocalName is "videoFile" or "quickTimeFile";
        var package = slide.Presentation.Package;
        string? source = null;
        foreach (var id in new[] { media?.Attribute(Ns.R + "embed")?.Value, file?.Attribute(Ns.R + "link")?.Value, file?.Attribute(Ns.R + "embed")?.Value })
        {
            if (string.IsNullOrEmpty(id) || package.Relationship(part, id) is not { } rel)
                continue;
            if (rel.External)
            {
                if (Uri.TryCreate(rel.Target, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                    source = rel.Target;
            }
            else if (package.Exists(rel.Target))
            {
                var type = package.ContentType(rel.Target);
                if (string.IsNullOrEmpty(type) || type == "application/octet-stream")
                    type = video ? "video/mp4" : "audio/mpeg";
                source = $"data:{type};base64,{Convert.ToBase64String(package.ReadBytes(rel.Target))}";
            }
            if (source != null)
                break;
        }
        if (source == null)
            return "";
        var attributes = new StringBuilder();
        if (media?.Element(P14 + "trim") is { } trim)
        {
            if (trim.Str("st") is { } st) attributes.Append($" data-trim-start=\"{System.Net.WebUtility.HtmlEncode(st)}\"");
            if (trim.Str("end") is { } end) attributes.Append($" data-trim-end=\"{System.Net.WebUtility.HtmlEncode(end)}\"");
        }
        if (media?.Element(P14 + "fade") is { } fade)
        {
            if (fade.Str("in") is { } fadeIn) attributes.Append($" data-fade-in=\"{System.Net.WebUtility.HtmlEncode(fadeIn)}\"");
            if (fade.Str("out") is { } fadeOut) attributes.Append($" data-fade-out=\"{System.Net.WebUtility.HtmlEncode(fadeOut)}\"");
        }
        var tag = video ? "video" : "audio";
        return $"<{tag} class=\"fs-media\" src=\"{System.Net.WebUtility.HtmlEncode(source)}\" preload=\"metadata\" playsinline{attributes}></{tag}>";
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

        // OLE objects (catalog: picture.ole): PowerPoint stores a picture of the object, usually an EMF.
        if (data.Element(Ns.P + "oleObj") is { } ole)
        {
            if (ole.Element(Ns.P + "pic") is { } preview)
                RenderShape(WithFrameTransform(preview, xfrm), part, html, placement, isSlide: true);
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
            html.Append($"<div class=\"sp\"{Accessibility(frame, true, part)} style=\"width:{Units.Css(pw)}px;height:{Units.Css(ph)}px;transform:{ToPx(matrix).Css()}\">");
            html.Append($"<svg width=\"{Units.Css(Math.Max(pw, 1))}\" height=\"{Units.Css(Math.Max(ph, 1))}\" viewBox=\"0 0 {Units.Css(Math.Max(pw, 1))} {Units.Css(Math.Max(ph, 1))}\">{svg}</svg></div>");
            return;
        }

        if (data.Element(Ns.A + "tbl") is { } table)
        {
            var (x, y, w, h, rotation, flipH, flipV) = ReadTransform(xfrm);
            var (bx, by, bw, bh) = placement.Map(x, y, w, h);
            var (svg, width, height) = Tables.Render(table);
            var matrix = placement.Transform.Then(LocalMatrix(bx, by, Math.Max(bw, width * Units.EmuPerPixel), Math.Max(bh, height * Units.EmuPerPixel), rotation, flipH, flipV));
            html.Append($"<div class=\"sp\"{Accessibility(frame, true, part)} style=\"width:{Units.Css(width)}px;height:{Units.Css(height)}px;transform:{ToPx(matrix).Css()}\">");
            html.Append($"<svg width=\"{Units.Css(Math.Max(width, 1))}\" height=\"{Units.Css(Math.Max(height, 1))}\" viewBox=\"0 0 {Units.Css(Math.Max(width, 1))} {Units.Css(Math.Max(height, 1))}\">{svg}</svg></div>");
        }
    }

    /// <summary>The transform keeps horizontal and vertical lines horizontal and vertical (rotations by multiples of 90°).</summary>
    static bool IsAxisAligned(Matrix m) =>
        (Math.Abs(m.B) < 1e-9 && Math.Abs(m.C) < 1e-9) || (Math.Abs(m.A) < 1e-9 && Math.Abs(m.D) < 1e-9);

    /// <summary>The path consists of horizontal and vertical straight segments only.</summary>
    static bool IsRectilinear(EvaluatedPath path)
    {
        (double X, double Y)? last = null;
        foreach (var segment in path.Segments)
        {
            switch (segment.Command)
            {
                case 'M':
                    last = (segment.Values[0], segment.Values[1]);
                    break;
                case 'L':
                    var point = (X: segment.Values[0], Y: segment.Values[1]);
                    if (last is { } p && Math.Abs(p.X - point.X) > 1e-6 && Math.Abs(p.Y - point.Y) > 1e-6)
                        return false;
                    last = point;
                    break;
                case 'Z':
                    break;
                default:
                    return false;
            }
        }
        return path.Segments.Count > 1;
    }

    /// <summary>A picture that takes the frame's position when it has none of its own (OLE and control previews).</summary>
    static XElement WithFrameTransform(XElement pic, XElement frameXfrm)
    {
        if (pic.Element(Ns.P + "spPr")?.Element(Ns.A + "xfrm")?.Element(Ns.A + "ext") != null)
            return pic;
        var copy = new XElement(pic);
        var spPr = copy.Element(Ns.P + "spPr");
        if (spPr == null)
        {
            spPr = new XElement(Ns.P + "spPr");
            copy.Add(spPr);
        }
        spPr.Element(Ns.A + "xfrm")?.Remove();
        spPr.AddFirst(new XElement(Ns.A + "xfrm", frameXfrm.Attributes(), frameXfrm.Elements()));
        return copy;
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
        html.Append($"<div class=\"grp\"{Accessibility(group, isSlide, part)}>");
        RenderTree(group, part, html, child, isSlide);
        html.Append("</div>");
    }

    static readonly XNamespace Adec = "http://schemas.microsoft.com/office/drawing/2017/decorative";

    /// <summary>Text markup without its run links (inside a linked shape).</summary>
    static string UnlinkText(string markup) =>
        System.Text.RegularExpressions.Regex.Replace(markup, "<a(?: [^>]*)?>(?:<title>[^<]*</title>)?|</a>", "");

    static XElement? NonVisual(XElement shape) =>
        shape.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("nv", StringComparison.Ordinal))?.Elements().FirstOrDefault(e => e.Name.LocalName == "cNvPr");

    static bool IsHidden(XElement shape) => NonVisual(shape).Bool("hidden") == true;

    /// <summary>
    /// Shape id for the player (slide shapes only; ids repeat across master and layout) and accessibility
    /// attributes: alt text, title, decorative shapes hidden from assistive technology (catalog: shape.accessibility).
    /// </summary>
    string Accessibility(XElement shape, bool isSlide, string part)
    {
        var c = NonVisual(shape);
        if (c == null)
            return "";
        var sb = new StringBuilder();
        if (isSlide && c.Str("id") is { } id)
            sb.Append($" data-id=\"{System.Net.WebUtility.HtmlEncode(id)}\"");
        // Mouse-over actions (a:hlinkHover, catalog: interactivity.hyperlinks): the player runs them when the pointer
        // enters the shape during the show.
        if (Links.Resolve(c.Element(Ns.A + "hlinkHover"), part, slide.Presentation) is { } hover)
        {
            if (hover.Action != null) sb.Append($" data-hover-action=\"{System.Net.WebUtility.HtmlEncode(hover.Action)}\"");
            if (hover.Href != null) sb.Append($" data-hover-href=\"{System.Net.WebUtility.HtmlEncode(hover.Href)}\"");
        }
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
                var fill = FillReader.Read(f, slide.Colors, slide.Images(s.Part), slide.ImageSizes(s.Part));
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
        if (image == null || blipFill == null)
            return Fill.None;
        // The blip's color effects and tiling come with the fill; the picture itself is the SVG or the bitmap.
        return FillReader.Read(blipFill, slide.Colors, _ => image, slide.ImageSizes(part)) is PictureFill fill ? fill : Fill.None;
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
        var fill = SlideBackground();
        var defs = new StringBuilder();
        var paintAttributes = paint.Fill(fill, width, height, defs);
        return $"<svg class=\"bg\" width=\"{Units.Css(width)}\" height=\"{Units.Css(height)}\">" +
               (defs.Length > 0 ? $"<defs>{defs}</defs>" : "") +
               $"<rect width=\"{Units.Css(width)}\" height=\"{Units.Css(height)}\"{paintAttributes}/></svg>";
    }

    /// <summary>The slide's background fill: its own, else the layout's, else the master's; white without any.</summary>
    Fill SlideBackground()
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
        return fill is NoFill ? new SolidFill(RgbColor.White) : fill;
    }
}
