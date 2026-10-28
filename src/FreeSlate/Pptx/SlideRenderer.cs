using System.Text;
using System.Xml.Linq;
using FreeSlate.Drawing;
using FreeSlate.Drawing.Geometry;
using FreeSlate.Html;
using FreeSlate.Rendering;

namespace FreeSlate.Pptx;

/// <summary>
/// Renders one slide: background, the master's and layout's non-placeholder shapes, then the slide's shapes.
/// Every shape becomes an absolutely positioned div with one CSS matrix, an SVG for geometry and paint and an
/// HTML layer for text.
/// </summary>
public sealed class SlideRenderer(SlideContext slide, SvgPaint paint)
{
    readonly TextRenderer text = new(slide, slide.Presentation.Fonts) { Paint = paint, Pixels = slide.Presentation.Pixels.OfText };

    /// <summary>
    /// The morph transition this slide takes part in (into it or out of it; byObject, byWord, byChar), or null: its
    /// shapes carry their names (data-name), and by words or letters its text is drawn in units.
    /// </summary>
    public string? Morph { get; init; }

    /// <summary>
    /// Shapes (by id) whose text fits itself to them anew as the slide is drawn (see TextRenderer.Refit): those whose text
    /// was just edited; how each fitted is in Fitted.
    /// </summary>
    public IReadOnlySet<uint>? Refitting { get; init; }
    public Dictionary<uint, TextFit> Fitted { get; } = [];
    TableRenderer Tables => tables ??= new TableRenderer(slide, paint, text);
    TableRenderer? tables;

    /// <summary>
    /// Shapes whose text the slide show builds letter by letter or word by word (p:iterate type lt or wd) or animates
    /// in character ranges (p:charRg): their text is drawn character by character.
    /// </summary>
    HashSet<string> Iterated => iterated ??= slide.Slide.Root?.Element(Ns.P + "timing") is { } timing
        ? timing.Descendants(Ns.P + "cTn").Where(c => c.Element(Ns.P + "iterate")?.Attribute("type")?.Value is "lt" or "wd")
            .SelectMany(c => c.Descendants(Ns.P + "spTgt"))
            .Concat(timing.Descendants(Ns.P + "spTgt").Where(t => t.Element(Ns.P + "txEl")?.Element(Ns.P + "charRg") != null))
            .Select(t => t.Attribute("spid")?.Value).OfType<string>().ToHashSet()
        : [];
    HashSet<string>? iterated;

    /// <summary>
    /// Shapes whose fill the slide show turns on (p:set fill.on: color effects on a shape without fill): their
    /// fillable paths are marked (data-fillable) so that the player can fill them.
    /// </summary>
    HashSet<string> FillsTurnedOn => fillsTurnedOn ??= slide.Slide.Root?.Element(Ns.P + "timing") is { } timing
        ? timing.Descendants(Ns.P + "cBhvr").Where(b => b.Element(Ns.P + "attrNameLst")?.Elements(Ns.P + "attrName").Any(a => a.Value.Trim() == "fill.on") == true)
            .Select(b => b.Element(Ns.P + "tgtEl")?.Element(Ns.P + "spTgt")?.Attribute("spid")?.Value).OfType<string>().ToHashSet()
        : [];
    HashSet<string>? fillsTurnedOn;

    /// <summary>The formattings the slide show gives shapes' text (catalog: animation.behaviors), by shape id.</summary>
    Dictionary<string, List<TextOverride>> Restyled => restyled ??= TextVariants.Collect(slide.Slide.Root?.Element(Ns.P + "timing"));
    Dictionary<string, List<TextOverride>>? restyled;

    /// <summary>Maps child coordinates of a group to slide coordinates (EMU); Transform holds rotations and flips.</summary>
    readonly record struct Placement(Matrix Transform, double OffX, double OffY, double ScaleX, double ScaleY, double ChildX, double ChildY, Fill? GroupFill)
    {
        public static readonly Placement Root = new(Matrix.Identity, 0, 0, 1, 1, 0, 0, null);

        /// <summary>
        /// A child's box: its center moved with the group, its size scaled; a child turned by 45° to 135° (or 225° to
        /// 315°) lies across the group, its width scaled as the group's height and its height as the width (measured:
        /// LibreOffice's tdf141463_GroupTransform, a child turned by 80° in a group squeezed to two thirds of its height).
        /// </summary>
        public (double X, double Y, double W, double H) Map(double x, double y, double w, double h, double rotation = 0)
        {
            var turn = (rotation % 180 + 180) % 180;
            var (sw, sh) = turn is >= 45 and < 135 ? (ScaleY, ScaleX) : (ScaleX, ScaleY);
            var (cx, cy) = (OffX + (x + w / 2 - ChildX) * ScaleX, OffY + (y + h / 2 - ChildY) * ScaleY);
            return (cx - w * sw / 2, cy - h * sh / 2, w * sw, h * sh);
        }

        /// <summary>The slide transform of the element the shapes are drawn into (a SmartArt frame); null for the slide.</summary>
        public Matrix? Container { get; init; }

        /// <summary>A shape transform <paramref name="local"/> in this placement as a slide transform.</summary>
        public Matrix OnSlide(Matrix local) => Container is { } container ? container.Then(local) : local;

        /// <summary>
        /// The 3D scene the shapes stand in (a SmartArt diagram's): its a:scene3d and its center (EMU, in the shapes'
        /// coordinates); shapes without a scene of their own are turned with its camera about its center.
        /// </summary>
        public (XElement Scene3d, double X, double Y)? Scene { get; init; }
    }

    /// <summary>The slide as a section element; <paramref name="attributes"/> and <paramref name="appendix"/> (notes) are added as given.</summary>
    public string Render(string attributes = "", string appendix = "")
    {
        var (width, height, items) = Scene();
        var html = new StringBuilder();
        // On device pixels of their own (PixelGrid) the slide is scaled to them.
        var pixels = slide.Presentation.Pixels;
        var scale = pixels.IsCss ? "" : FormattableString.Invariant($";transform:scale({pixels.X:R},{pixels.Y:R});transform-origin:0 0");
        html.Append($"<section class=\"slide\" id=\"slide-{slide.Number}\"{attributes} style=\"width:{Units.Css(width)}px;height:{Units.Css(height)}px{scale}\">");
        HtmlScene.Write(items, html);
        html.Append(appendix).Append("</section>");
        return html.ToString();
    }

    /// <summary>
    /// The slide as it is drawn (catalog: engine.output): its size in CSS pixels and its items from back to front, its
    /// background first, then the master's and the layout's shapes where it shows them, then its own.
    /// </summary>
    public (double Width, double Height, List<SceneItem> Items) Scene()
    {
        var width = Units.ToPx(slide.Presentation.SlideWidth);
        var height = Units.ToPx(slide.Presentation.SlideHeight);
        var items = new List<SceneItem> { Background(width, height) };

        var showMaster = slide.Slide.Root.Bool("showMasterSp") ?? true;
        if (showMaster && (slide.Layout?.Root.Bool("showMasterSp") ?? true) && slide.Master != null)
            RenderTree(Tree(slide.Master), slide.MasterPart!, items, Placement.Root, isSlide: false);
        if (showMaster && slide.Layout != null)
            RenderTree(Tree(slide.Layout), slide.LayoutPart!, items, Placement.Root, isSlide: false);
        RenderTree(Tree(slide.Slide), slide.SlidePart, items, Placement.Root, isSlide: true);

        // ActiveX controls are drawn from their stored pictures (catalog: picture.ole).
        foreach (var control in slide.Slide.Root?.Element(Ns.P + "cSld")?.Element(Ns.P + "controls")?.Elements(Ns.P + "control") ?? [])
        {
            if (control.Element(Ns.P + "pic") is { } preview)
                RenderShape(preview, slide.SlidePart, items, Placement.Root, isSlide: true);
            // PowerPoint 2007 kept the control's picture and place in the slide's VML drawing only (catalog: legacy.vml).
            else if (LegacyDrawing.Find(slide.Presentation, slide.SlidePart, control.Str("spid")) is { Box: { } box } legacy)
                RenderShape(LegacyDrawing.Element(legacy, "0", control.Str("name") ?? "", box), legacy.Part, items, Placement.Root, isSlide: true);
        }
        return (width, height, items);
    }

    static XElement? Tree(XDocument document) => document.Root?.Element(Ns.P + "cSld")?.Element(Ns.P + "spTree");

    /// <summary>
    /// One shape, picture, group or graphic frame in PresentationML names, drawn at its own transform (EMU from the
    /// page's top left): the drawings of Word documents (catalog: drawing.inline, drawing.anchor).
    /// </summary>
    public List<SceneItem> RenderItems(XElement element, string part)
    {
        var items = new List<SceneItem>();
        RenderTree(new XElement(Ns.P + "spTree", element), part, items, Placement.Root, isSlide: true);
        return items;
    }

    /// <summary>What is drawn as HTML or SVG (see MarkupItem), where it is not empty.</summary>
    static void AddMarkup(List<SceneItem> items, string html)
    {
        if (html.Length > 0)
            items.Add(new MarkupItem(html));
    }

    /// <summary>A frame's drawing (a chart's) on a canvas of its box, at least a pixel each way.</summary>
    static BoxItem Framed(double width, double height, Matrix transform, string attributes, string markup) =>
        Framed(width, height, transform, attributes, markup.Length > 0 ? [new MarkupItem(markup)] : []);

    /// <summary>A frame's drawing as items (a table's) on a canvas of its box, at least a pixel each way.</summary>
    static BoxItem Framed(double width, double height, Matrix transform, string attributes, List<SceneItem> drawn) =>
        new((width, height), transform, attributes, [new SvgItem(Math.Max(width, 1), Math.Max(height, 1), drawn)]);

    void RenderTree(XElement? tree, string part, List<SceneItem> items, Placement placement, bool isSlide)
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
                        RenderShape(element, part, items, placement, isSlide);
                    break;
                case "grpSp":
                    RenderGroup(element, part, items, placement, isSlide);
                    break;
                case "graphicFrame":
                    if (isSlide || Placeholders.Ph(element) == null)
                        RenderGraphicFrame(element, part, items, placement, isSlide);
                    break;
                case "contentPart":
                    RenderContentPart(element, part, items, placement, isSlide);
                    break;
            }
        }
    }

    /// <summary>
    /// The shape and, for slide placeholders, the layout and master placeholders it inherits from. A slide
    /// placeholder the layout has no placeholder for inherits from no placeholder at all, not even the master's
    /// of its type (measured: dt and sldNum with an idx the layout lacks take neither position nor the master
    /// placeholders' 12 pt, color and alignment, only the master's text style of their type), unless its idx is
    /// 4294967295 (none): then it inherits from the master's placeholder of its type.
    /// </summary>
    List<PartElement> Chain(XElement shape, string part, bool isSlide)
    {
        var chain = new List<PartElement> { new(shape, part) };
        if (!isSlide || Placeholders.Ph(shape) is not { } ph)
            return chain;
        if (slide.Layout != null)
        {
            if (Placeholders.InLayout(slide.Layout, ph) is { } layoutShape)
            {
                chain.Add(new PartElement(layoutShape, slide.LayoutPart!));
                ph = Placeholders.Ph(layoutShape)!;
            }
            else if (!Placeholders.Unindexed(ph))
                return chain;
        }
        if (Placeholders.InMaster(slide.Master, ph) is { } masterShape)
            chain.Add(new PartElement(masterShape, slide.MasterPart!));
        return chain;
    }

    static XElement? SpPr(XElement shape) => shape.Element(Ns.P + "spPr");

    void RenderShape(XElement shape, string part, List<SceneItem> items, Placement placement, bool isSlide)
    {
        // The markup of a 3D body, which stays as it is drawn (its backdrop's shadow takes the text in later).
        var html = new StringBuilder();
        var chain = Chain(shape, part, isSlide);
        var xfrm = chain.Select(s => SpPr(s.Element)?.Element(Ns.A + "xfrm")).FirstOrDefault(x => x?.Element(Ns.A + "ext") != null);
        if (xfrm == null)
            return;

        var (x, y, w, h, rotation, flipH, flipV) = ReadTransform(xfrm);
        var (bx, by, bw, bh) = placement.Map(x, y, w, h, rotation);
        var matrix = placement.Transform.Then(LocalMatrix(bx, by, bw, bh, rotation, flipH, flipV));

        var (definition, adjustments) = Geometry(chain);
        var geometry = GeometryEvaluator.Evaluate(definition, bw, bh, adjustments);
        // A sketched outline (catalog: shape.sketched) is saved as its hand-drawn paths; the text keeps the text
        // rectangle of the original geometry, which the sketch properties hold.
        if (SketchedGeometry(chain) is var (original, originalAdjustments))
        {
            var text = GeometryEvaluator.Evaluate(original, bw, bh, originalAdjustments);
            geometry = geometry with { TextLeft = text.TextLeft, TextTop = text.TextTop, TextRight = text.TextRight, TextBottom = text.TextBottom };
        }
        // The text's own turn within the shape (SmartArt text rectangles), about the text rectangle's center.
        var (textTurn, textCenter) = (0.0, (X: 0.0, Y: 0.0));
        if (shape.Element(Ns.P + "txXfrm") is { } txXfrm && txXfrm.Element(Ns.A + "ext") != null)
        {
            // SmartArt shapes carry their own text rectangle: placed like the shape (its center where it stands, in
            // the same coordinates), turned by the shape's rotation and its own, which PowerPoint uses to keep the
            // text of turned shapes upright (measured: rot 90° with a text rot of −90°).
            var (tx, ty, tw, th, turn, _, _) = ReadTransform(txXfrm);
            var (tbx, tby, tbw, tbh) = placement.Map(tx, ty, tw, th);
            var (dx, dy) = (tbx + tbw / 2 - (bx + bw / 2), tby + tbh / 2 - (by + bh / 2));
            var angle = -rotation * Math.PI / 180;
            var (lx, ly) = (dx * Math.Cos(angle) - dy * Math.Sin(angle), dx * Math.Sin(angle) + dy * Math.Cos(angle));
            if (flipH)
                lx = -lx;
            if (flipV)
                ly = -ly;
            var (cx, cy) = (Units.ToPx(bw / 2 + lx), Units.ToPx(bh / 2 + ly));
            var (halfWidth, halfHeight) = (Units.ToPx(tbw) / 2, Units.ToPx(tbh) / 2);
            geometry = geometry with { TextLeft = cx - halfWidth, TextTop = cy - halfHeight, TextRight = cx + halfWidth, TextBottom = cy + halfHeight };
            (textTurn, textCenter) = (turn, (cx, cy));
        }
        var style = chain.Select(s => s.Element.Element(Ns.P + "style")).FirstOrDefault(s => s != null);

        var fill = shape.Name.LocalName == "pic" ? PictureFill(shape, part, Units.ToPx(bw)) : ResolveFill(chain, placement.GroupFill);
        // A shape filled with the slide's background shows the part of the background behind it.
        if (shape.Name.LocalName == "sp" && shape.Bool("useBgFill") == true)
        {
            var toShape = ToPx(placement.OnSlide(matrix)).Invert();
            fill = new SlideFill(SlideBackground(), Units.ToPx(slide.Presentation.SlideWidth), Units.ToPx(slide.Presentation.SlideHeight),
                (toShape.A, toShape.B, toShape.C, toShape.D, toShape.E, toShape.F));
        }
        // Presets that cannot shade along their outline draw such a gradient as a circle gradient.
        if (fill is GradientFill { Path: "shape" } alongOutline && !definition.ShadeShape)
            fill = alongOutline with { Path = "circle" };
        var outline = ResolveOutline(chain);
        var (pw, ph) = (Units.ToPx(bw), Units.ToPx(bh));

        var defs = new StringBuilder();
        var paths = new List<SceneItem>();
        var slideMatrix = placement.OnSlide(matrix);
        var origin = (Units.ToPx(slideMatrix.E), Units.ToPx(slideMatrix.F));
        // PowerPoint's pattern tiles lie on the slide's pixels whatever the shape's turn (PatternFill.ToShape), and a
        // gradient that does not turn with the shape keeps the slide's directions through its groups' turns and flips
        // too (GradientFill.Unturn).
        if (!slide.Presentation.Word && ToPx(slideMatrix).Invert() is var (ta, tb, tc, td, te, tf))
        {
            if (fill is PatternFill pattern)
                fill = pattern with { ToShape = (ta, tb, tc, td, te, tf) };
            if (fill is GradientFill { RotateWithShape: false } unturned)
                fill = unturned with { Unturn = (ta, tb, tc, td) };
            if (outline?.Fill is PatternFill linePattern)
                outline = outline with { Fill = linePattern with { ToShape = (ta, tb, tc, td, te, tf) } };
        }
        // Picture borders lie outside the picture (measured: a 6 pt border ends where the picture begins).
        var outsetBorder = shape.Name.LocalName == "pic" && outline is { Visible: true } && ReferenceEquals(definition, PresetGeometries.Get("rect"));
        var axisAligned = IsAxisAligned(ToPx(matrix));
        // PowerPoint draws outlines in whole pixels (PixelWidth), those of a shape that is not turned on its pixels
        // (OnPixels), their line ends with them; Word's PDF keeps both.
        double? drawnWidth = outline is { Visible: true } && !slide.Presentation.Word ? PixelWidth(outline) : null;
        var device = slide.Presentation.Pixels;
        EvaluatedPath Outlined(EvaluatedPath path) =>
            path.Stroke && !outsetBorder && axisAligned && drawnWidth is { } pixels && CompoundLines.Parts(outline!.Compound) == null
                ? OnPixels(path, ToPx(slideMatrix), pixels, IsRectilinear(path) ? null : (pw, ph), device) : path;
        // Line ends pull the line back under them in the paths they are given: the ones that are drawn.
        var outlinedPaths = geometry.Paths.Select(Outlined).ToList();
        List<SceneItem> ends = outline is { Visible: true }
            ? LineEnds.Apply(outline, outlinedPaths, paint.Fill(outline.Fill, pw, ph, defs, rotation, origin),
                paint.Stroke(outline with { Dash = "solid" }, pw, ph, defs, origin, drawnWidth: drawnWidth))
            : [];
        // A fill overlay blends a second fill over the shape's fill, not its outline (catalog: effect.blur).
        var effects = Effects(chain, style);
        var dag = EffectContainer(chain, style) is { Name.LocalName: "effectDag" } effectGraph ? effectGraph : null;
        var dagText = "";
        Camera? textCamera = null;
        var flatText = false;
        var textLayer = "";
        // The places of text drawn as markup (see PlacesOf), where it was laid out flat in the shape.
        List<SceneItem> textPlaces = [];
        var backdropText = false;
        var overlay = effects?.Element(Ns.A + "fillOverlay") is { } fillOverlay && FillReader.FindFill(fillOverlay) is { } overlayFill
            ? (Fill: FillReader.Read(overlayFill, slide.Colors, slide.Images(part), slide.ImageSizes(part)), Blend: fillOverlay.Str("blend") ?? "over")
            : ((Fill Fill, string Blend)?)null;
        // A picture can have a fill of its own behind it, visible through its transparent parts.
        var background = shape.Name.LocalName == "pic" ? ResolveFill(chain, placement.GroupFill) : Fill.None;
        // PowerPoint fills all paths of a geometry before it strokes them (measured: the outline of a first path
        // lies on the fill of a second one); while no stroked path comes before a filled one, that is the same as
        // drawing path after path. Sketched shapes save their outline before their fill (catalog: shape.sketched).
        var lastFilled = geometry.Paths.FindLastIndex(p => p.Fill != "none");
        var strokesLast = outline is { Visible: true } && geometry.Paths.Take(Math.Max(0, lastFilled)).Any(p => p.Stroke);
        var strokes = new List<SceneItem>();
        var fillable = isSlide && fill is NoFill && NonVisual(shape)?.Attribute("id")?.Value is { } animatedId && FillsTurnedOn.Contains(animatedId)
            ? " data-fillable=\"\"" : "";
        // A gradient spans the bounds of the geometry's filled paths, not the shape's box, and one that follows the
        // shape (path="shape") repeats their outline (measured: the band of a block arc shows the whole gradient;
        // the shaded faces of cans, cubes and bevels show their part of it).
        var filled = geometry.Paths.Where(p => p.Fill != "none").ToList();
        var outlineData = string.Concat(filled.Select(p => p.Data));
        var paintBounds = PathBounds(new EvaluatedPath(filled.SelectMany(p => p.Segments).ToList(), "norm", false));
        string PathPaint(Fill pathFill, EvaluatedPath path)
        {
            if (pathFill is not GradientFill || paintBounds is not var (x, y, w, h))
                return paint.Fill(pathFill, pw, ph, defs, rotation, origin, outlineData);
            return paint.Fill(pathFill, w, h, defs, rotation, origin, outlineData, (x, y));
        }
        for (var index = 0; index < geometry.Paths.Count; index++)
        {
            var path = geometry.Paths[index];
            var pathFill = path.Fill == "none" ? Fill.None : Shade(fill, path.Fill);
            // PowerPoint draws axis-aligned rectangles and lines without antialiasing: every edge goes to the
            // nearest pixel, which is what crispEdges does (measured on fills and outlines at fractional positions); on
            // device pixels of their own the edges so placed are then where its samples see them (OnPixels).
            var rectilinear = axisAligned && IsRectilinear(path);
            var crisp = rectilinear && device.X == 1 && device.Y == 1 ? " shape-rendering=\"crispEdges\"" : "";
            var fillPath = rectilinear && crisp == "" ? OnPixels(path, ToPx(slideMatrix), 0, null, device) : path;
            // Subpaths that overlap leave a hole, as PowerPoint fills them (alternate; measured: LibreOffice's
            // custgeom-overlapping-contours shows the shape below through the overlap of its two rectangles).
            var rule = pathFill is not NoFill && path.Segments.Count(s => s.Command == 'M') > 1 ? " fill-rule=\"evenodd\"" : "";
            if (background is not NoFill && path.Fill != "none")
                paths.Add(new ShapePathItem(fillPath.Data, background, null, $"{paint.Fill(background, pw, ph, defs, rotation, origin)} stroke=\"none\"{crisp}"));
            var compound = path.Stroke && outline is { Visible: true } && CompoundLines.Parts(outline.Compound) != null;
            var closed = Dashes.Closed(path);
            // The fill that shares an element with the outline on PowerPoint's pixels is the same on screen (only under
            // a translucent outline does it move by up to half a pixel).
            var outlined = path.Stroke && !outsetBorder && !compound ? outlinedPaths[index] : fillPath;
            var pathWidth = drawnWidth >= 2 && !ReferenceEquals(outlined, path) && Curved(path) ? drawnWidth - CurvedLowSide : drawnWidth;
            var stroke = path.Stroke && !outsetBorder && !compound ? paint.Stroke(outline, pw, ph, defs, origin, closed, drawnWidth: pathWidth) : " stroke=\"none\"";
            if (stroke == " stroke=\"none\"")
                outlined = fillPath;
            // A dashed outline starts its pattern where PowerPoint does, in a Word document where Word does (catalog:
            // line.dash).
            var dashed = stroke != " stroke=\"none\"" && Dashes.IsDashed(outline);
            var strokeData = !dashed ? outlined.Data
                : slide.Presentation.Word ? Dashes.WordStart(path, axisAligned).Data : Dashes.StartAtClosingSegment(outlined).Data;
            var strokeTarget = strokesLast ? strokes : paths;
            if (overlay is var (overlayPaint, blend) && pathFill is not NoFill)
            {
                var mode = blend switch { "mult" => "multiply", "screen" => "screen", "darken" => "darken", "lighten" => "lighten", _ => "normal" };
                paths.Add(new SvgGroupItem(" style=\"isolation:isolate\"",
                [
                    new ShapePathItem(fillPath.Data, pathFill, null, $"{PathPaint(pathFill, path)} stroke=\"none\"{crisp}{rule}"),
                    new ShapePathItem(fillPath.Data, overlayPaint, null, $"{paint.Fill(overlayPaint, pw, ph, defs, rotation, origin)} stroke=\"none\" style=\"mix-blend-mode:{mode}\"{crisp}{rule}"),
                ]));
                if (stroke != " stroke=\"none\"")
                    strokeTarget.Add(new ShapePathItem(strokeData, Fill.None, outline, $" fill=\"none\"{stroke}{crisp}") { DashesClosed = closed });
            }
            else if (strokesLast)
            {
                if (paint.PictureImage(pathFill, pw, ph, fillPath.Data, defs, crisp) is { } image)
                    paths.Add(new PictureItem(image.Image, image.X, image.Y, image.Width, image.Height, fillPath.Data, image.Clip, image.Filter));
                else if (pathFill is not NoFill)
                    paths.Add(new ShapePathItem(fillPath.Data, pathFill, null, $"{PathPaint(pathFill, path)} stroke=\"none\"{crisp}{rule}"));
                if (stroke != " stroke=\"none\"")
                    strokes.Add(new ShapePathItem(strokeData, Fill.None, outline, $" fill=\"none\"{stroke}{crisp}") { DashesClosed = closed });
            }
            else if (paint.PictureImage(pathFill, pw, ph, fillPath.Data, defs, crisp) is { } image)
            {
                // A picture prints at its own resolution as an image clipped to the path, the outline over it.
                paths.Add(new PictureItem(image.Image, image.X, image.Y, image.Width, image.Height, fillPath.Data, image.Clip, image.Filter));
                if (stroke != " stroke=\"none\"")
                    paths.Add(new ShapePathItem(strokeData, Fill.None, outline, $" fill=\"none\"{stroke}{crisp}") { DashesClosed = closed });
            }
            else
            {
                paths.Add(new ShapePathItem(strokeData, pathFill, stroke != " stroke=\"none\"" ? outline : null,
                    $"{PathPaint(pathFill, path)}{stroke}{crisp}{rule}{(path.Fill != "none" ? fillable : "")}") { DashesClosed = closed });
            }
            if (dashed && outline!.Cap == "sq")
                strokeTarget.AddRange(Dashes.SquareEnds(path, outline, Math.Max(Units.ToPx(outline.WidthEmu), 0.5),
                    paint.Stroke(outline with { Dash = "solid", Cap = "flat" }, pw, ph, defs, origin) + crisp, paint.DashUnit(outline)));
            if (compound)
                strokeTarget.AddRange(CompoundLines.Strokes(path, outline!, part => paint.Stroke(part, pw, ph, defs, origin)));
        }
        paths.AddRange(strokes);
        if (outsetBorder)
        {
            var half = Units.ToPx(outline!.WidthEmu) / 2;
            var crisp = axisAligned ? " shape-rendering=\"crispEdges\"" : "";
            // Starting at the bottom left corner, where PowerPoint starts a dash pattern on a rectangle, running up, in
            // Word along the bottom where the rectangle is not turned (Dashes).
            var outset = slide.Presentation.Word && axisAligned
                ? $"M{Units.Css(-half)} {Units.Css(ph + half)}H{Units.Css(pw + half)}V{Units.Css(-half)}H{Units.Css(-half)}Z"
                : $"M{Units.Css(-half)} {Units.Css(ph + half)}V{Units.Css(-half)}H{Units.Css(pw + half)}V{Units.Css(ph + half)}Z";
            var mitered = outline with { Join = "miter" };
            paths.Add(new ShapePathItem(outset, Fill.None, mitered, $" fill=\"none\"{paint.Stroke(mitered, pw, ph, defs, origin, closed: true)}{crisp}") { DashesClosed = true });
        }

        // A 3D camera (catalog: effect.scene3d) turns the drawn shape about its center: the outer div keeps the
        // shape's unturned box (the player animates it) and holds the extruded body, an inner div projects the
        // front face with the camera.
        var scene3d = Scene3D(chain, style);
        var camera = Camera.Read(scene3d);
        // A diagram's 3D scene turns the shape with the diagram's camera about the diagram's center, lit by the
        // shape's own light rig if it has one (catalog: smartart.styles).
        if (placement.Scene is { } diagramScene && camera is null or { Flat: true })
        {
            var light = scene3d?.Element(Ns.A + "lightRig");
            scene3d = light == null ? diagramScene.Scene3d : new XElement(Ns.A + "scene3d", diagramScene.Scene3d.Element(Ns.A + "camera"), light);
            camera = Camera.Read(scene3d)! with { Pivot = (Units.ToPx(diagramScene.X - bx - bw / 2), Units.ToPx(diagramScene.Y - by - bh / 2)) };
        }
        var shape3d = camera != null ? Shape3D(chain, style) : null;
        if (camera != null && shape3d.Long("z") is { } offset)
            camera = camera with { Offset = Units.ToPx(offset) };
        var (topWidth, topHeight, topProfile) = Bevel3D(shape3d?.Element(Ns.A + "bevelT"), pw, ph);
        var (bottomWidth, bottomHeight, bottomProfile) = Bevel3D(shape3d?.Element(Ns.A + "bevelB"), pw, ph);
        var beveled = topWidth > 0;
        var depth = Units.ToPx(shape3d.Long("extrusionH") ?? 0);
        var contourWidth = Units.ToPx(shape3d.Long("contourW") ?? 0);
        var lighting = Lighting.For(scene3d, shape3d, flat: depth <= 0.01 && !beveled && bottomWidth <= 0 && contourWidth <= 0.01);
        if (camera is { Legacy: true })
            lighting = lighting.WithViewer(camera);
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
        // A body without fill has no faces of its own (measured, local/nofill3d): no bevels and no back, sides only in
        // the extrusion's or the line's color; its line and its text cast the shadow and glow as on a flat shape.
        var hollow = solidBody && fill is NoFill;
        if (solidBody)
        {
            var box = ToPx(placement.Transform.Then(Matrix.Translate(bx, by)));
            html.Append($"<div class=\"sp\"{Accessibility(shape, isSlide, part)} style=\"width:{Units.Css(pw)}px;height:{Units.Css(ph)}px;transform:{box.Css()}\">");
            var faceColor = Fill.MainColor(fill) ?? RgbColor.FromHex("808080");
            var extrusionColor = shape3d?.Element(Ns.A + "extrusionClr") is { } extrusionClr ? slide.Colors.ResolveChild(extrusionClr) : null;
            // Outlines that turn against the largest are holes (frames, donuts): their sides face into the hole
            // (measured: LibreOffice's Scene3d_lightRig_* frames show the walls inside).
            var solid = new Solid(camera!, lighting, pw, ph, rotation, flipH, flipV) { Holes = true };
            solidOutlines = Solid.Outlines(geometry, lineWidth / 2);
            // The extrusion runs from the top bevel's outer edge backwards; the bottom bevel closes it at the back.
            var (front, back) = (-topHeight, -topHeight - depth);
            if (wireframe)
                solid.Wireframe(solidOutlines, front, camera!.Flat ? front : back, slide.Presentation.Word ? null : lineColor, slide.Presentation.Word ? 0 : lineWidth);
            else if (!camera!.Flat)
            {
                if (depth > 0.01 && (extrusionColor ?? lineColor ?? (hollow ? null : faceColor)) is { } sideColor)
                    solid.Extrusion(solidOutlines, front, back, sideColor, open: hollow);
                if (bottomWidth > 0 && !hollow)
                    solid.Bevel(solidOutlines, bottomWidth, bottomHeight, bottomProfile, faceColor, back, bottom: true, lineColor, lineWidth);
                if ((depth > 0.01 || beveled || bottomWidth > 0) && !hollow)
                {
                    var backOutlines = bottomWidth > 0 ? solidOutlines.Select(o => Solid.Inset(o, bottomWidth)).ToList() : solidOutlines;
                    solid.Back(backOutlines, back - bottomHeight, faceColor, lineWidth > bottomWidth ? lineColor : null, lineWidth - bottomWidth);
                }
            }
            // Pictures and gradients keep their texture on the bevel: it is shaded over the unclipped front face.
            texturedBevel = beveled && fill is not (SolidFill or NoFill);
            if (beveled && !wireframe && !hollow)
                solid.Bevel(solidOutlines, topWidth, topHeight, topProfile, texturedBevel ? null : faceColor, line: lineColor, lineWidth: lineWidth);
            if (contourWidth > 0.01 && !wireframe)
                solid.Contour(solidOutlines, front, back, contourWidth, Solid.Rise(topWidth, topHeight, topProfile), Solid.Rise(bottomWidth, bottomHeight, bottomProfile));
            // A 3D body casts its shadow and glow from its back, the outline at the back seen by the camera (measured);
            // a backdrop (a:backdrop) takes them instead: the flat shape's shadow and glow lie in that plane, seen by
            // the camera, behind the body when the plane is, else over its sides (measured: a floor plane below the
            // shape shows the whole shadow there, a plane through the front face covers the extrusion).
            var planeEffects = "";
            var planeBehind = false;
            // A hollow body leaves them to the flat shape's filter below.
            if (!hollow && Backdrop(scene3d) is { } backdrop && effects != null)
            {
                // The glow lies at the anchor; the shadow moves by its distance along the scene's x and y, not the
                // plane's (measured: a floor plane's shadow only moves sideways).
                var flatOutline = string.Concat(geometry.Paths.Where(p => p.Fill != "none").Select(p => p.Data));
                var layers = new List<(XElement Effects, (double X, double Y) Shift)>();
                if (effects.Element(Ns.A + "glow") is { } planeGlow)
                    layers.Add((new XElement(Ns.A + "effectLst", planeGlow), (0, 0)));
                if (effects.Element(Ns.A + "outerShdw") is { } planeShadow)
                {
                    var (distance, direction) = (Units.ToPx(planeShadow.Long("dist") ?? 0), (planeShadow.Long("dir") ?? 0) / 60000.0 * Math.PI / 180);
                    var still = new XElement(planeShadow);
                    still.SetAttributeValue("dist", null);
                    layers.Insert(0, (new XElement(Ns.A + "effectLst", still), (distance * Math.Cos(direction), distance * Math.Sin(direction))));
                }
                foreach (var (layerEffects, shift) in layers)
                {
                    var planeDefs = new StringBuilder();
                    if (flatOutline.Length == 0 || SvgEffects.Filter(layerEffects, slide.Colors, pw, ph, planeDefs, parts: SvgEffects.Parts.Outer) is not { } planeFilter)
                        continue;
                    var moved = backdrop with { Anchor = (backdrop.Anchor.X + shift.X, backdrop.Anchor.Y + shift.Y, backdrop.Anchor.Z) };
                    // The shadow of the shape's text lies over the shape's own, darker where they overlap (measured: a
                    // floor plane shows the text in the shape's shadow); the text follows once it is laid out.
                    var textShadow = layerEffects.Element(Ns.A + "outerShdw") != null ? $"<g filter=\"url(#{planeFilter})\">{BackdropTextToken}</g>" : "";
                    backdropText |= textShadow.Length > 0;
                    planeEffects += $"<div class=\"sp\" style=\"width:{Units.Css(pw)}px;height:{Units.Css(ph)}px;transform:{OnBackdrop(camera!, pw, ph, rotation, flipH, flipV, moved)}\">" +
                                    $"<svg width=\"{Units.Css(Math.Max(pw, 1))}\" height=\"{Units.Css(Math.Max(ph, 1))}\" style=\"overflow:visible\"><defs>{planeDefs}</defs>" +
                                    $"<path d=\"{flatOutline}\" fill=\"#000\" filter=\"url(#{planeFilter})\"/>{textShadow}</svg></div>";
                }
                planeBehind = backdrop.Anchor.Z < back - 0.01;
            }
            else if (!hollow)
            {
                var (backData, backBounds) = solid.Projected(solidOutlines, back);
                var outerDefs = new StringBuilder();
                // A picture's glow and shadow follow its pixels, not its box: a copy of the picture at the body's back,
                // seen by the camera, casts them (Word's PDF of LibreOffice's extras-ooxmlexport/
                // picture-effects-preservation: a tree on a transparent background in an extruded frame seen
                // isometrically glows around the tree).
                if (fill is PictureFill && paths.Count > 0
                    && SvgEffects.Filter(effects, slide.Colors, pw, ph, outerDefs, parts: SvgEffects.Parts.Outer) is { } pictureFilter)
                    html.Append($"<div class=\"sp\" style=\"width:{Units.Css(pw)}px;height:{Units.Css(ph)}px;transform:{Projection(camera! with { Offset = camera.Offset + back }, pw, ph, rotation, flipH, flipV)}\">" +
                                $"<svg width=\"{Units.Css(Math.Max(pw, 1))}\" height=\"{Units.Css(Math.Max(ph, 1))}\" style=\"overflow:visible\"><defs>{outerDefs}</defs>" +
                                $"<g filter=\"url(#{pictureFilter})\">{HtmlScene.Markup(paths)}</g></svg></div>");
                else if (backData.Length > 0 && SvgEffects.Filter(effects, slide.Colors, pw, ph, outerDefs, parts: SvgEffects.Parts.Outer, region: backBounds) is { } outerFilter)
                    html.Append($"<svg class=\"sp\" width=\"{Units.Css(Math.Max(pw, 1))}\" height=\"{Units.Css(Math.Max(ph, 1))}\" style=\"overflow:visible\"><defs>{outerDefs}</defs><path d=\"{backData}\" fill=\"#000\" filter=\"url(#{outerFilter})\"/></svg>");
            }
            if (planeBehind)
                html.Append(planeEffects);
            var solidMarkup = solid.Markup();
            if (solidMarkup.Length > 0)
                html.Append($"<svg class=\"sp\" width=\"{Units.Css(Math.Max(pw, 1))}\" height=\"{Units.Css(Math.Max(ph, 1))}\" style=\"overflow:visible\">{solidMarkup}</svg>");
            if (!planeBehind)
                html.Append(planeEffects);
            var contourColor = shape3d?.Element(Ns.A + "contourClr") is { } contourClr ? slide.Colors.ResolveChild(contourClr) : null;
            bevelShading = solid.OverlayMarkup(contourColor ?? RgbColor.Black);
            // A solid body hides its front face from behind: the back face shows there.
            var hidden = (depth > 0.01 || beveled || bottomWidth > 0) && !wireframe ? ";backface-visibility:hidden" : "";
            html.Append($"<div class=\"sp\" style=\"width:{Units.Css(pw)}px;height:{Units.Css(ph)}px;transform:{Projection(camera, pw, ph, rotation, flipH, flipV)}{hidden}\">");
        }
        var attributes = solidBody ? "" : Accessibility(shape, isSlide, part);
        // A zero-sized SVG is not painted at all, so lines (height or width 0) get a 1 px viewport.
        var (sw, sh) = (Math.Max(pw, 1), Math.Max(ph, 1));
        // A hyperlink or action on the whole shape (catalog: interactivity.hyperlinks).
        var shapeLink = Links.Resolve(NonVisual(shape)?.Element(Ns.A + "hlinkClick"), part, slide.Presentation);
        // A zoom object's picture: its slides, whether the show returns to it, the zoom transition's duration.
        if (shapeLink != null && (string?)shape.Attribute(Opc.MarkupCompatibility.Annotation + "zoom") is { } zoom && slide.Presentation.ZoomTarget(zoom) is { } range)
        {
            var back = (string?)shape.Attribute(Opc.MarkupCompatibility.Annotation + "zoomReturn") == "0" ? 0 : 1;
            var duration = (string?)shape.Attribute(Opc.MarkupCompatibility.Annotation + "zoomDur") ?? "";
            shapeLink = shapeLink with { Zoom = $"{range.First},{range.Last},{back},{duration}" };
        }
        // Effects: the shape's effect list, else the theme effect style referenced by effectRef. Without a visible
        // fill or outline the text casts the effect.
        // The effects reach the geometry also where it leaves the shape's box (a callout's tail: LibreOffice's
        // extras-ooxmlexport/tdf41466_testVmlShapeWithTextbox, a tail 40 pt beside the box casts its shadow too).
        (double X, double Y, double W, double H)? effectRegion = null;
        if (effects != null && PathBounds(new EvaluatedPath(geometry.Paths.SelectMany(p => p.Segments).ToList(), "norm", false)) is var (gx, gy, gw, gh)
            && (gx < -0.5 || gy < -0.5 || gx + gw > pw + 0.5 || gy + gh > ph + 0.5))
        {
            var (rx, ry) = (Math.Min(0, gx), Math.Min(0, gy));
            effectRegion = (rx, ry, Math.Max(pw, gx + gw) - rx, Math.Max(ph, gy + gh) - ry);
        }
        var effectFilterOf = SvgEffects.FilterOf(effects, slide.Colors, pw, ph, defs, stroke: outline is { Visible: true } ? Units.ToPx(outline.WidthEmu) : 0,
            parts: solidBody && !hollow ? SvgEffects.Parts.Shape : SvgEffects.Parts.All, frame: matrix, region: effectRegion);
        var opaque = fill is not NoFill || outline is { Visible: true };
        var body = new List<SceneItem>();
        List<SceneItem> shapeItems = wireframe && solidBody ? [] : [.. paths];
        if (!(wireframe && solidBody))
            shapeItems.AddRange(ends);
        if (beveled && !texturedBevel && !hollow && solidOutlines != null && shapeItems.Count > 0)
        {
            // The fill shows on the front face inside the bevel; the line lies on the bevel.
            var clip = $"bf{++litFilters}";
            defs.Append($"<clipPath id=\"{clip}\"><path d=\"{Solid.PathData(solidOutlines.Select(o => Solid.Inset(o, topWidth)))}\"/></clipPath>");
            shapeItems = [new SvgGroupItem($" clip-path=\"url(#{clip})\"", shapeItems)];
        }
        // A translucent material (clear, translucentPowder) lets the background through the front face, lit with its
        // highlights added in full; the text on it stays opaque (measured). An effect graph keeps the face opaque.
        var faceOpacity = camera != null && dag == null ? lighting.OpacityAt((0, 0, 1), camera.View((0, 0, 0))) : 1;
        var frontShade = camera != null ? lighting.Shade((0, 0, 1), camera.View((0, 0, 0))) : default;
        if (faceOpacity < 1 && shapeItems.Count > 0)
            shapeItems = [new SvgGroupItem($" opacity=\"{Units.Css4(faceOpacity)}\" filter=\"url(#{LitFilter(Lighting.Through(frontShade, faceOpacity), defs)})\"", shapeItems)
                { ColorMatrix = LitMatrix(Lighting.Through(frontShade, faceOpacity)) }];
        // The shape's own drawing is a group of its own when it has text: animations can build the background and
        // the text separately (catalog: animation.build).
        var hasText = shape.Name.LocalName == "sp" && shape.Element(Ns.P + "txBody")?.Descendants(Ns.A + "t").Any() == true;
        if (hasText && shapeItems.Count > 0)
            shapeItems = [new SvgGroupItem(" data-bg=\"\"", shapeItems)];
        // A scaled, skewed or flipped shadow is a transformed copy of the shape behind it.
        if (!solidBody && opaque && shapeItems.Count > 0 && SvgEffects.Shadow(effects, slide.Colors, pw, ph, defs, matrix) is { } shadowCopy)
            body.Add(new FilterItem(shadowCopy.Filter, shadowCopy.Model, [new TransformItem(shadowCopy.Transform, shapeItems)]));
        if (effectFilterOf is { } shapeFilter && opaque)
            body.Add(new FilterItem(shapeFilter.Id, shapeFilter.Model, shapeItems));
        else
            body.AddRange(shapeItems);
        if (shape.Name.LocalName == "sp")
        {
            // The master's text style follows the layout placeholder a slide placeholder matched, else its own type
            // (measured: type dt with the title's idx is set in the title style; see Chain).
            var placeholder = isSlide && chain.Count > 1 && chain[1].Part == slide.LayoutPart
                ? Placeholders.Ph(chain[1].Element) : Placeholders.Ph(shape);
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
            // 3D text (catalog: text.threeD): a camera of the text body's own turns the text, an extrusion and bevels of its
            // own give the glyphs a body like a 3D shape's (the shape stays flat); flat text (a:flatTx) in a 3D shape
            // keeps out of the scene, facing the viewer (measured).
            var bodyPrs = chain.Select(s => s.Element.Element(Ns.P + "txBody")?.Element(Ns.A + "bodyPr")).Where(b => b != null).ToList();
            var textScene = bodyPrs.Select(b => b!.Element(Ns.A + "scene3d")).FirstOrDefault(e => e != null);
            var textShape3d = bodyPrs.Select(b => b!.Element(Ns.A + "sp3d")).FirstOrDefault(e => e != null);
            textCamera = solidBody ? null : Camera.Read(textScene);
            if (textCamera != null && textShape3d.Long("z") is { } textOffset)
                textCamera = textCamera with { Offset = Units.ToPx(textOffset) };
            var textSolid = textCamera != null && (!textCamera.Flat || textShape3d?.Element(Ns.A + "bevelT") != null || textShape3d?.Element(Ns.A + "bevelB") != null
                                                  || (textShape3d.Long("extrusionH") ?? 0) > 0 || (textShape3d.Long("contourW") ?? 0) > 0);
            if (!textSolid)
                textCamera = null;
            flatText = solidBody && bodyPrs.Any(b => b!.Element(Ns.A + "flatTx") != null);
            if (textCamera != null)
                text.Outlines = [];
            var shapeId = isSlide ? NonVisual(shape)?.Attribute("id")?.Value : null;
            var textChain = new TextChain
            {
                Shapes = chain,
                StyleKind = placeholder == null ? "other" : Placeholders.TextStyle(Placeholders.Type(placeholder)),
                Defaults = textDefaults,
                FontRef = style?.Element(Ns.A + "fontRef"),
                Units = shapeId != null && (Iterated.Contains(shapeId) || Morph is "byWord" or "byChar"),
            };
            text.ShapeSize = (pw, ph);
            // Text that stays as laid out in its box lines its underlines up with the slide's pixels; upright, turned
            // and 3D text and a Word page's drawings leave them in the box.
            text.Grid = textCamera == null && !solidBody && !(upright && turn != 0) && Math.Abs(textTurn) < 1e-9 ? Grid(slideMatrix) : null;
            var refitted = shapeId != null && uint.TryParse(shapeId, out var refit) && Refitting?.Contains(refit) == true ? refit : (uint?)null;
            text.Refit = refitted != null;
            var textItems = text.Render(textChain, textGeometry, pw, flipH ^ flipV);
            if (refitted is { } fitted && text.Fitted is { } fit)
                Fitted[fitted] = fit with { Transform = xfrm };
            (text.Refit, text.Fitted) = (false, null);
            var glyphOutlines = text.Outlines;
            text.Outlines = null;
            // Text the slide show restyles is drawn in each formatting too (not 3D text).
            if (textCamera == null && shapeId != null && textItems.Count > 0 && Restyled.TryGetValue(shapeId, out var formattings))
                textItems = [new MarkupItem(TextVariants.Merge(HtmlScene.Markup(textItems),
                    formattings.Select(f => (f.Key, HtmlScene.Markup(text.Render(textChain.With(f), textGeometry, pw, flipH ^ flipV)))).ToList(), textChain.Units)),
                    .. PlacesOf(textItems)];
            text.Grid = null;
            if (upright && turn != 0 && textItems.Count > 0)
                textItems = [TransformItem.Rotation(-turn, pw / 2, ph / 2, textItems)];
            if (Math.Abs(textTurn) > 1e-9 && textItems.Count > 0)
                textItems = [TransformItem.Rotation(textTurn, textCenter.X, textCenter.Y, textItems)];
            // On device pixels that map text apart from shapes (PixelGrid.Text), it is scaled about the slide's corner.
            if (slide.Presentation.Pixels.Text is not 1 and var scale && textItems.Count > 0 && ToPx(slideMatrix) is var toSlide && toSlide.Invert() is var fromSlide)
                textItems = [new TransformItem(fromSlide.Then(new Matrix(scale, 0, 0, scale, 0, 0)).Then(toSlide), textItems)];
            if (shapeLink != null)
                textItems = Unlinked(textItems);   // links cannot nest
            // The text's silhouette in a backdrop's shadow: its glyphs without ids, links or effects of their own.
            if (backdropText)
            {
                html.Replace(BackdropTextToken, Silhouette(HtmlScene.Markup(textItems)));
                backdropText = false;
            }
            if (textCamera != null && glyphOutlines is { Count: > 0 } || flatText || dag != null || solidBody)
                textPlaces = PlacesOf(textItems);
            if (textCamera != null && glyphOutlines is { Count: > 0 })
                textLayer = Text3D(textCamera, Lighting.For(textScene, textShape3d, flat: (textShape3d.Long("extrusionH") ?? 0) <= 0 && textShape3d?.Element(Ns.A + "bevelT") == null
                                                                                      && textShape3d?.Element(Ns.A + "bevelB") == null && (textShape3d.Long("contourW") ?? 0) <= 0),
                    textShape3d, glyphOutlines, HtmlScene.Markup(textItems), pw, ph, CastsOuter(textItems));
            else if (flatText)
                textLayer = textItems.Count > 0
                    ? $"<svg class=\"sp\" width=\"{Units.Css(Math.Max(pw, 1))}\" height=\"{Units.Css(Math.Max(ph, 1))}\" style=\"overflow:visible\"><g transform=\"rotate({Units.Css(rotation)} {Units.Css(pw / 2)} {Units.Css(ph / 2)})\">{HtmlScene.Markup(textItems)}</g></svg>"
                    : "";
            else if (dag != null)
                dagText = HtmlScene.Markup(textItems);
            else
            {
                if (faceOpacity < 1 && textItems.Count > 0)
                    textItems = [new SvgGroupItem($" filter=\"url(#{LitFilter(frontShade, defs)})\"", textItems) { ColorMatrix = LitMatrix(frontShade) }];
                if (effectFilterOf is { } textFilter && !opaque && textItems.Count > 0)
                    body.Add(new FilterItem(textFilter.Id, textFilter.Model, textItems));
                else
                    body.AddRange(textItems);
            }
        }
        if (backdropText)
            html.Replace(BackdropTextToken, "");
        var content = body;
        if (body.Count > 0 && SvgEffects.ReflectionOf(effects, (0, 0, pw, ph), defs, matrix) is { } reflected)
            content = [new ReflectionItem(reflected, body)];
        if (dag != null)
        {
            // An effect graph (catalog: effect.dag) draws the shape from its fill, its line and its text.
            var fillOnly = new StringBuilder();
            var lineOnly = new StringBuilder();
            foreach (var path in geometry.Paths)
            {
                if (path.Fill != "none" && fill is not NoFill)
                    fillOnly.Append($"<path d=\"{path.Data}\"{paint.Fill(Shade(fill, path.Fill), pw, ph, defs, rotation, origin)} stroke=\"none\"/>");
                if (path.Stroke && outline is { Visible: true })
                {
                    var data = !Dashes.IsDashed(outline) ? path.Data
                        : slide.Presentation.Word ? Dashes.WordStart(path, axisAligned).Data : Dashes.StartAtClosingSegment(path).Data;
                    lineOnly.Append($"<path d=\"{data}\" fill=\"none\"{paint.Stroke(outline, pw, ph, defs, origin, Dashes.Closed(path))}/>");
                }
            }
            lineOnly.Append(HtmlScene.Markup(ends));
            var graph = new EffectDag(slide.Colors, pw, ph, defs,
                element => paint.Fill(FillReader.Read(element, slide.Colors, slide.Images(part), slide.ImageSizes(part)), pw, ph, defs, rotation, origin), matrix);
            content = [];
            AddMarkup(content, graph.Render(dag, new EffectDag.Sources(fillOnly.ToString(), lineOnly.ToString(), HtmlScene.Markup(paths) + HtmlScene.Markup(ends) + dagText)));
        }
        // The front face is lit like the rest of the 3D shape (its normal faces the front); a translucent one has
        // lit its drawing and its text apart.
        if (camera != null && content.Count > 0 && faceOpacity >= 1)
            content = [new SvgGroupItem($" filter=\"url(#{LitFilter(frontShade, defs)})\"", content) { ColorMatrix = LitMatrix(frontShade) }];
        var drawn = new List<SceneItem>();
        if (defs.Length > 0)
            drawn.Add(new DefsItem(defs.ToString()));
        drawn.AddRange(content);
        List<SceneItem> canvas = shapeLink != null ? [new WrappedItem(Links.Open(shapeLink), drawn, "</a>")] : drawn;
        var over = new StringBuilder();
        var missing = shape.Name.LocalName == "pic" ? MissingPicture(shape, part, pw, ph) : null;
        if (shape.Name.LocalName == "pic")
            over.Append(Media(shape, part)).Append(Model3D.Canvas(shape, part, slide));
        if (solidBody)
        {
            html.Append($"<svg width=\"{Units.Css(sw)}\" height=\"{Units.Css(sh)}\" viewBox=\"0 0 {Units.Css(sw)} {Units.Css(sh)}\">");
            HtmlScene.Write(canvas, html);
            html.Append("</svg>").Append(over);
            if (missing != null)
                html.Append(HtmlScene.Markup(missing));
            html.Append("</div>");
            if (bevelShading.Length > 0)
                html.Append($"<svg class=\"sp\" width=\"{Units.Css(Math.Max(pw, 1))}\" height=\"{Units.Css(Math.Max(ph, 1))}\" style=\"overflow:visible\">{bevelShading}</svg>");
            html.Append(textLayer).Append("</div>");
            items.Add(new MarkupItem(html.ToString()));
            if (textPlaces.Count > 0)
                items.Add(new BoxItem((pw, ph), ToPx(matrix), "", textPlaces));
            return;
        }
        var shapeCanvas = new SvgItem(sw, sh, canvas);
        var shapeBox = new BoxItem((pw, ph), ToPx(matrix), attributes, [shapeCanvas]);
        AddMarkup(shapeBox.Items, over.ToString());
        if (missing != null)
            shapeBox.Items.Add(missing);
        AddMarkup(shapeBox.Items, textLayer);
        shapeBox.Items.AddRange(textPlaces);
        items.Add(shapeBox);
    }

    /// <summary>
    /// The places among text's scene items (see PlacesItem), in the transforms and groups they stand in, and nothing
    /// else: what keeps them where the text was laid out where it is drawn as markup (3D text, flat text of a 3D body,
    /// text the slide show restyles or an effect graph draws). Text being edited stands flat in its shape, as PowerPoint
    /// shows it then.
    /// </summary>
    static List<SceneItem> PlacesOf(List<SceneItem> items)
    {
        var kept = new List<SceneItem>();
        foreach (var item in items)
        {
            switch (item)
            {
                case PlacesItem places:
                    kept.Add(places);
                    break;
                case TransformItem transformed when PlacesOf(transformed.Items) is { Count: > 0 } inner:
                    kept.Add(transformed with { Items = inner });
                    break;
                case SvgGroupItem group when PlacesOf(group.Items) is { Count: > 0 } inner:
                    kept.Add(new SvgGroupItem("", inner) { Transform = group.Transform });
                    break;
                case IHoldsItems holder and not (BoxItem or RotatedItem) when PlacesOf(holder.Items) is { Count: > 0 } inner:
                    kept.AddRange(inner);   // links, filters, reflections, which move nothing
                    break;
            }
        }
        return kept;
    }

    /// <summary>
    /// The CSS transform of a shape's drawing under a 3D camera, in the shape's own pixels: the shape's rotation and
    /// flips in its plane, then the camera's turn and projection about its center.
    /// </summary>
    /// <summary>
    /// A scene's backdrop plane (a:backdrop) in the camera's scene coordinates (pixels about the shape's center, x right,
    /// y down, z towards the viewer, as DrawingML's): its anchor, the directions of a picture's right and down on it
    /// (down along its up vector) and its normal.
    /// </summary>
    sealed record BackdropPlane((double X, double Y, double Z) Anchor, (double X, double Y, double Z) Right, (double X, double Y, double Z) Down,
        (double X, double Y, double Z) Normal);

    /// <summary>
    /// The backdrop of a:scene3d, or null: the shape's picture lies on it with its x along down × normal and its y along
    /// the up vector, its corner where the plane's anchor less half the shape's size is (fitted to the corners of right
    /// triangles' glow PowerPoint renders on walls and floors of both orientations, within 3 px).
    /// </summary>
    static BackdropPlane? Backdrop(XElement? scene3d)
    {
        if (scene3d?.Element(Ns.A + "backdrop") is not { } backdrop)
            return null;
        (double X, double Y, double Z) Vector(string name, (double, double, double) fallback) =>
            backdrop.Element(Ns.A + name) is { } v ? ((v.Long("dx") ?? 0) * 1.0, (v.Long("dy") ?? 0) * 1.0, (v.Long("dz") ?? 0) * 1.0) : fallback;
        static (double X, double Y, double Z) Unit((double X, double Y, double Z) v)
        {
            var length = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            return length < 1e-9 ? (0, 0, 0) : (v.X / length, v.Y / length, v.Z / length);
        }
        static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        var normal = Unit(Vector("norm", (0, 0, 1)));
        var down = Vector("up", (0, 1, 0));
        // The up vector as far as it lies in the plane.
        var along = Dot(down, normal);
        down = Unit((down.X - along * normal.X, down.Y - along * normal.Y, down.Z - along * normal.Z));
        if (normal == (0, 0, 0) || down == (0, 0, 0))
            return null;
        var right = (down.Y * normal.Z - down.Z * normal.Y, down.Z * normal.X - down.X * normal.Z, down.X * normal.Y - down.Y * normal.X);
        var anchor = backdrop.Element(Ns.A + "anchor");
        var position = (Units.ToPx(anchor?.Long("x") ?? 0), Units.ToPx(anchor?.Long("y") ?? 0), Units.ToPx(anchor?.Long("z") ?? 0));
        return new BackdropPlane(position, right, down, normal);
    }

    /// <summary>The CSS transform that lays a shape-sized picture onto the backdrop plane and projects it like the camera.</summary>
    static string OnBackdrop(Camera camera, double w, double h, double rotation, bool flipH, bool flipV, BackdropPlane plane)
    {
        var turn = Matrix.Rotate(rotation).Then(Matrix.Scale(flipH ? -1 : 1, flipV ? -1 : 1)).Then(Matrix.Translate(-w / 2, -h / 2));
        double[] local = [turn.A, turn.B, 0, 0, turn.C, turn.D, 0, 0, 0, 0, 1, 0, turn.E, turn.F, 0, 1];
        var (r, d, n) = (plane.Right, plane.Down, plane.Normal);
        // The picture's point (x, y) of the box goes to anchor + x·right + y·down − (w/2, h/2, 0); the turned picture's
        // points are relative to the box's center.
        var a = (X: plane.Anchor.X + w / 2 * r.X + h / 2 * d.X - w / 2, Y: plane.Anchor.Y + w / 2 * r.Y + h / 2 * d.Y - h / 2, Z: plane.Anchor.Z + w / 2 * r.Z + h / 2 * d.Z);
        double[] onPlane = [r.X, r.Y, r.Z, 0, d.X, d.Y, d.Z, 0, n.X, n.Y, n.Z, 0, a.X, a.Y, a.Z, 1];
        double[] center = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, w / 2, h / 2, 0, 1];
        var m = Camera.Multiply(center, Camera.Multiply(camera.Matrix3D(), Camera.Multiply(onPlane, local)));
        return "matrix3d(" + string.Join(",", m.Select(v => Units.Css4(v))) + ")";
    }

    static string Projection(Camera camera, double w, double h, double rotation, bool flipH, bool flipV)
    {
        // An oblique camera turns its picture by the shape's rotation (Camera.TurnsPicture), the others the shape.
        var turned = camera.TurnsPicture ? rotation : 0;
        var plane = Matrix.Rotate(rotation - turned).Then(Matrix.Scale(flipH ? -1 : 1, flipV ? -1 : 1)).Then(Matrix.Translate(-w / 2, -h / 2));
        // Column-major 4×4 of the plane's 2D transform.
        double[] local = [plane.A, plane.B, 0, 0, plane.C, plane.D, 0, 0, 0, 0, 1, 0, plane.E, plane.F, 0, 1];
        var projection = camera.Matrix3D();
        double[] center = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, w / 2, h / 2, 0, 1];
        var (sin, cos) = Math.SinCos(turned * Math.PI / 180);
        double[] turn = [cos, sin, 0, 0, -sin, cos, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
        var m = Multiply(center, Multiply(turn, Multiply(projection, local)));
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

    /// <summary>A filter lighting its content with a shade (color · diffuse + specular per channel); returns its id.</summary>
    /// <summary>The color matrix of a face's light (see LitFilter): its colors scaled by the diffuse light, the specular added.</summary>
    static double[] LitMatrix(((double R, double G, double B) Diffuse, (double R, double G, double B) Specular) shade) =>
        [shade.Diffuse.R, 0, 0, 0, shade.Specular.R, 0, shade.Diffuse.G, 0, 0, shade.Specular.G, 0, 0, shade.Diffuse.B, 0, shade.Specular.B, 0, 0, 0, 1, 0];

    static string LitFilter(((double R, double G, double B) Diffuse, (double R, double G, double B) Specular) shade, StringBuilder defs)
    {
        var (diffuse, specular) = shade;
        var lit = $"l{++litFilters}";
        defs.Append($"<filter id=\"{lit}\" color-interpolation-filters=\"sRGB\"><feColorMatrix type=\"matrix\" values=\"" +
                    $"{Units.Css4(diffuse.R)} 0 0 0 {Units.Css4(specular.R)} 0 {Units.Css4(diffuse.G)} 0 0 {Units.Css4(specular.G)} 0 0 {Units.Css4(diffuse.B)} 0 {Units.Css4(specular.B)} 0 0 0 1 0\"/></filter>");
        return lit;
    }

    /// <summary>
    /// 3D text (catalog: text.threeD): the glyphs' body in the text body's own scene (a:bodyPr/a:scene3d, a:sp3d), drawn
    /// like a 3D shape's along the glyph outlines (extrusion, bevels, back, contour; the extrusion in the text's color
    /// unless it has one of its own), and the text itself projected as the front face, inside the top bevel and lit
    /// like it. The scene turns about the center of the text, not of the shape (measured). A bevel wider than half the
    /// glyph's strokes meets the opposite one in a ridge: it is cut there, with its height in proportion.
    /// </summary>
    /// <summary>Whether drawn items cast a shadow or glow of their own (effects with outer parts among them).</summary>
    static bool CastsOuter(IEnumerable<SceneItem> items) =>
        items.Any(i => i switch
        {
            FilterItem f => Outer(f.Effects),
            SvgGroupItem g => Outer(g.Effects),
            GroupBoxItem g => Outer(g.Effects),
            ShapePathItem p => Outer(p.Effects),
            SvgTextItem t => Outer(t.Effects),
            _ => false,
        } || i is IHoldsItems holder && CastsOuter(holder.Items));

    static bool Outer(EffectModel? effects) => effects is { Shadow: not null } or { Glow: not null };

    string Text3D(Camera camera, Lighting lighting, XElement? shape3d, List<(List<List<(double X, double Y)>> Contours, RgbColor Color, bool Textured)> glyphs,
        string textMarkup, double pw, double ph, bool castsOuter = false)
    {
        // The scene's center: the center of the glyphs' box; Solid and Projection turn about the middle of the box given.
        var points = glyphs.SelectMany(g => g.Contours).SelectMany(c => c).ToList();
        var (cx, cy) = points.Count > 0 ? ((points.Min(p => p.X) + points.Max(p => p.X)) / 2, (points.Min(p => p.Y) + points.Max(p => p.Y)) / 2) : (pw / 2, ph / 2);
        var (sceneWidth, sceneHeight) = (2 * cx, 2 * cy);
        var solid = new Solid(camera, lighting, sceneWidth, sceneHeight, 0, false, false) { Holes = true };
        var (topWidth, topHeight, topProfile) = Bevel3D(shape3d?.Element(Ns.A + "bevelT"), pw, ph);
        var (bottomWidth, bottomHeight, bottomProfile) = Bevel3D(shape3d?.Element(Ns.A + "bevelB"), pw, ph);
        var depth = Units.ToPx(shape3d.Long("extrusionH") ?? 0);
        var contourWidth = Units.ToPx(shape3d.Long("contourW") ?? 0);
        var extrusionColor = shape3d?.Element(Ns.A + "extrusionClr") is { } extrusionClr ? slide.Colors.ResolveChild(extrusionClr) : null;
        var (front, back) = (-topHeight, -topHeight - depth);
        var face = new List<List<(double X, double Y)>>();
        foreach (var (contours, color, textured) in glyphs)
        {
            var outlines = contours.Where(c => c.Count >= 3).ToList();
            if (outlines.Count == 0)
                continue;
            // Bevels wider than half a stroke run across it to a crest along its middle (see Solid.Bevel; found by an
            // agent in Word's picture of LibreOffice's extras-ooxmlexport/TextEffects_Props3d_Ligatures_NumForm_NumSpacing:
            // Calibri 48 with a relaxed inset bevel of 3 pt over strokes of 3 to 4 pt, no flat front face left; Word's
            // text-3d "Metal" M of Arial Black 48: crests 2.2 and 2.66 pt inside its stem's edges under a bevel of 5 pt).
            // Text filled with a gradient or a picture keeps its bevels within its strokes' mean thickness, twice its
            // area over its perimeter, their height as much lower (not measured yet: docx/external/poi/bib-chernigovka's
            // heading in an accent gradient under a circle bevel of 3 pt shows its gradient across its bold strokes).
            var middle = !textured;
            var limit = double.PositiveInfinity;
            if (textured)
            {
                var area = Math.Abs(outlines.Sum(Area));
                var perimeter = outlines.Sum(o => o.Select((p, i) => Math.Sqrt(Math.Pow(o[(i + 1) % o.Count].X - p.X, 2) + Math.Pow(o[(i + 1) % o.Count].Y - p.Y, 2))).Sum());
                limit = perimeter > 0 ? area / perimeter : 0;
            }
            var (top, bottom) = (Math.Min(topWidth, limit), Math.Min(bottomWidth, limit));
            var topRise = topWidth > 0.01 ? topHeight * top / topWidth : 0;
            var bottomRise = bottomWidth > 0.01 ? bottomHeight * bottom / bottomWidth : 0;
            if (depth > 0.01)
                solid.Extrusion(outlines, front, back, extrusionColor ?? color);
            if (bottom > 0.01)
                solid.Bevel(outlines, bottom, bottomRise, bottomProfile, color, back, bottom: true, middle: middle);
            // The counters of the glyph are holes: inset away from their middle (Solid.Holes).
            var turn = Solid.Turn(outlines);
            List<(double X, double Y)> Inset(List<(double X, double Y)> o, double distance) =>
                middle ? Solid.InsetToMiddle(o, outlines, distance, turn) : Solid.Inset(o, distance, turn);
            if (depth > 0.01 || top > 0.01 || bottom > 0.01)
                solid.Back(bottom > 0.01 ? outlines.Select(o => Inset(o, bottom)).ToList() : outlines, back - bottomRise, color, null, 0);
            if (top > 0.01)
                solid.Bevel(outlines, top, topRise, topProfile, color, middle: middle);
            if (contourWidth > 0.01)
                solid.Contour(outlines, front, back, contourWidth, Solid.Rise(top, topRise, topProfile), Solid.Rise(bottom, bottomRise, bottomProfile));
            face.AddRange(top > 0.01 ? outlines.Select(o => Inset(o, top)) : outlines);
        }
        var (sw, sh) = (Units.Css(Math.Max(pw, 1)), Units.Css(Math.Max(ph, 1)));
        var result = new StringBuilder();
        // The text's own shadow and glow fall from its whole glyphs, not from the front face the bevels leave: a copy of
        // the text lies behind the body, which covers all of it but its effects (measured: tika's testWordArt, a
        // shadow under red text with a bevel and a contour).
        if (castsOuter)
            result.Append($"<div class=\"sp\" style=\"width:{Units.Css(pw)}px;height:{Units.Css(ph)}px;transform:{Projection(camera, sceneWidth, sceneHeight, 0, false, false)}\">" +
                          $"<svg width=\"{sw}\" height=\"{sh}\" style=\"overflow:visible\">{textMarkup}</svg></div>");
        var markup = solid.Markup();
        if (markup.Length > 0)
            result.Append($"<svg class=\"sp\" width=\"{sw}\" height=\"{sh}\" style=\"overflow:visible\">{markup}</svg>");
        // The front face: the text inside its top bevel, projected by the camera.
        var clip = "";
        if (topWidth > 0.01)
        {
            var id = $"t3{++litFilters}";
            clip = $"<defs><clipPath id=\"{id}\"><path d=\"{Solid.PathData(face)}\" fill-rule=\"evenodd\"/></clipPath></defs>";
            textMarkup = $"{clip}<g clip-path=\"url(#{id})\">{textMarkup}</g>";
        }
        // The front face is lit like the rest of the body (its normal faces the front), translucent with a translucent
        // material like a shape's.
        var frontView = camera.View((0, 0, 0));
        var opacity = lighting.OpacityAt((0, 0, 1), frontView);
        var textDefs = new StringBuilder();
        var lit = LitFilter(Lighting.Through(lighting.Shade((0, 0, 1), frontView), opacity), textDefs);
        textMarkup = $"<defs>{textDefs}</defs><g filter=\"url(#{lit})\"{(opacity < 1 ? $" opacity=\"{Units.Css4(opacity)}\"" : "")}>{textMarkup}</g>";
        result.Append($"<div class=\"sp\" style=\"width:{Units.Css(pw)}px;height:{Units.Css(ph)}px;transform:{Projection(camera, sceneWidth, sceneHeight, 0, false, false)}\">" +
                      $"<svg width=\"{sw}\" height=\"{sh}\" style=\"overflow:visible\">{textMarkup}</svg></div>");
        var contourColor = shape3d?.Element(Ns.A + "contourClr") is { } contourClr ? slide.Colors.ResolveChild(contourClr) : null;
        if (solid.OverlayMarkup(contourColor ?? RgbColor.Black) is { Length: > 0 } overlay)
            result.Append($"<svg class=\"sp\" width=\"{sw}\" height=\"{sh}\" style=\"overflow:visible\">{overlay}</svg>");
        return result.ToString();

        static double Area(List<(double X, double Y)> outline)
        {
            var sum = 0.0;
            for (var i = 0; i < outline.Count; i++)
            {
                var (a, b) = (outline[i], outline[(i + 1) % outline.Count]);
                sum += a.X * b.Y - b.X * a.Y;
            }
            return sum / 2;
        }
    }

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

    /// <summary>Extensions of media files a linked video or sound may point to; other web addresses are web players.</summary>
    static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".webm", ".ogv", ".ogg", ".mkv", ".avi", ".wmv", ".mpg", ".mpeg", ".mp3", ".m4a", ".aac", ".wav", ".wma", ".flac", ".opus",
    };

    /// <summary>
    /// The sign over a picture of <paramref name="width"/> × <paramref name="height"/> pixels without its picture (an
    /// a:blip naming none, or a part the package lacks; in Word also a linked file it does not find), or null:
    /// PowerPoint's is a 1-pixel grey frame on the picture's edge with the broken-picture icon and "The picture can't be
    /// displayed." at its top left (measured: #6D6D6D, the icon 6 pixels in), worded in Office's language by
    /// PowerPoint and in the viewer's by the player; Word's see MissingPictureItem.
    /// </summary>
    MissingPictureItem? MissingPicture(XElement pic, string part, double width, double height)
    {
        var blip = pic.Element(Ns.P + "blipFill")?.Element(Ns.A + "blip");
        var id = blip?.Descendants(Ns.Asvg + "svgBlip").FirstOrDefault()?.Attribute(Ns.R + "embed")?.Value
                 ?? blip?.Attribute(Ns.R + "embed")?.Value ?? blip?.Attribute(Ns.R + "link")?.Value;
        var package = slide.Presentation.Package;
        var german = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";
        if (id != null && package.Relationship(part, id) is { } rel && (rel.External || package.Exists(rel.Target)))
        {
            // Word's sign for a linked picture it does not find, a path on a disk of another machine: on white in a
            // 0.14 pt black frame its icon and the message in 1.2 pt Tahoma, in Office's language (docx/external/
            // libreoffice/extras-ooxmlexport/FileWithInvalidImageLink: a GIF linked on F:, the message in three lines).
            if (slide.Presentation.Word && rel.External && blip?.Attribute(Ns.R + "embed") == null
                && !(Uri.TryCreate(rel.Target, UriKind.Absolute, out var linked) && linked.Scheme is "http" or "https"))
                return new MissingPictureItem(width, height, german
                    ? "Das verknüpfte Bild kann nicht angezeigt werden. Möglicherweise wurde die Datei verschoben, umbenannt oder gelöscht. Stellen Sie sicher, dass die Verknüpfung auf die korrekte Datei und den korrekten Speicherort zeigt."
                    : "The linked image cannot be displayed. The file may have been moved, renamed, or deleted. Verify that the link points to the correct file and location.");
            return null;
        }
        // Word's sign for an embedded picture whose part the package lacks, or whose a:blip names none: the same frame
        // and icon, its own message (docx/external/libreoffice/extras-ooxmlimport/tdf115883: media/image1.png missing;
        // extras-ooxmlexport/fdo83057, tdf148671: an empty a:blip).
        if (slide.Presentation.Word && (id != null && package.Relationship(part, id) is { External: false } || id == null && blip != null))
            return new MissingPictureItem(width, height, german
                ? "Das Bild kann nicht angezeigt werden. Dieser Computer verfügt möglicherweise über zu wenig Arbeitsspeicher, um das Bild zu öffnen, oder das Bild ist beschädigt. Starten Sie den Computer neu, und öffnen Sie dann erneut die Datei. Wenn weiterhin das rote x angezeigt wird, müssen Sie das Bild möglicherweise löschen und dann erneut einfügen."
                : "The image cannot be displayed. Your computer may not have enough memory to open the image, or the image may have been corrupted. Restart your computer, and then open the file again. If the red x still appears, you may have to delete the image and then insert it again.");
        // A picture of imported content (an RTF chunk's) comes as a data URI.
        if (id != null && id.StartsWith("data:", StringComparison.Ordinal))
            return null;
        // Media have content of their own.
        var nvPr = pic.Element(Ns.P + "nvPicPr")?.Element(Ns.P + "nvPr");
        if (nvPr != null && (nvPr.Elements().Any(e => e.Name.LocalName is "videoFile" or "audioFile" or "wavAudioFile" or "quickTimeFile")
                             || nvPr.Descendants(P14 + "media").Any()))
            return null;
        return new MissingPictureItem(width, height, null);
    }

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
                {
                    // An online video (Insert > Online video: a link to a web player such as YouTube's embed page)
                    // plays in a frame, loaded only when it plays.
                    if (video && !MediaExtensions.Contains(Path.GetExtension(uri.AbsolutePath)))
                        return $"<iframe class=\"fs-media\" data-src=\"{System.Net.WebUtility.HtmlEncode(rel.Target)}\" " +
                               "allow=\"autoplay; encrypted-media; picture-in-picture; fullscreen\" allowfullscreen referrerpolicy=\"strict-origin-when-cross-origin\"></iframe>";
                    source = rel.Target;
                }
            }
            else if (package.Exists(rel.Target))
            {
                // The file's own media type (a:videoFile/a:audioFile contentType), else the package's.
                var type = file?.Str("contentType") is { Length: > 0 } declared ? declared : package.ContentType(rel.Target);
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
        // Bookmarks (p14:bmkLst): named times that start animations (catalog: animation.media).
        var bookmarks = media?.Element(P14 + "bmkLst")?.Elements(P14 + "bmk")
            .Select(b => new System.Text.Json.Nodes.JsonObject { ["n"] = b.Str("name") ?? "", ["t"] = double.TryParse(b.Str("time"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : 0 })
            .ToList();
        if (bookmarks is { Count: > 0 })
            attributes.Append($" data-bookmarks=\"{System.Net.WebUtility.HtmlEncode(new System.Text.Json.Nodes.JsonArray(bookmarks.ToArray<System.Text.Json.Nodes.JsonNode?>()).ToJsonString())}\"");
        // Caption and subtitle tracks (p173:tracksInfo): WebVTT files the video shows over itself.
        var tracks = new StringBuilder();
        foreach (var track in nvPr.Descendants().Where(e => e.Name.LocalName == "track" && e.Parent?.Name.LocalName == "trackLst"))
        {
            if (track.Attribute(Ns.R + "id")?.Value is not { Length: > 0 } trackId || package.Relationship(part, trackId) is not { External: false } trackRel
                || !package.Exists(trackRel.Target))
                continue;
            var kind = track.Str("type") is "subtitles" ? "subtitles" : "captions";
            var language = track.Str("lang") is { Length: > 0 } lang ? $" srclang=\"{System.Net.WebUtility.HtmlEncode(lang)}\"" : "";
            var label = track.Str("label") is { Length: > 0 } name ? $" label=\"{System.Net.WebUtility.HtmlEncode(name)}\"" : "";
            tracks.Append($"<track kind=\"{kind}\" src=\"data:text/vtt;base64,{Convert.ToBase64String(package.ReadBytes(trackRel.Target))}\"{language}{label}{(tracks.Length == 0 ? " default" : "")}>");
        }
        var tag = video ? "video" : "audio";
        return $"<{tag} class=\"fs-media\" src=\"{System.Net.WebUtility.HtmlEncode(source)}\" preload=\"metadata\" playsinline{attributes}>{(video ? tracks.ToString() : "")}</{tag}>";
    }

    /// <summary>Graphic frames: SmartArt, charts and tables (OLE objects follow).</summary>
    void RenderGraphicFrame(XElement frame, string part, List<SceneItem> items, Placement placement, bool isSlide)
    {
        var xfrm = frame.Element(Ns.P + "xfrm");
        var data = frame.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData");
        if (xfrm?.Element(Ns.A + "ext") == null || data == null)
            return;

        if (data.Elements().FirstOrDefault(e => e.Name.LocalName == "relIds") is { } relIds)
        {
            RenderDiagram(relIds, frame, part, items, placement, isSlide);
            return;
        }

        // A locked canvas (lc:lockedCanvas, catalog: shape.group): its DrawingML shapes drawn like a picture of
        // them, the box around them stretched over the frame with their lines and text (measured: shapes, lines and
        // text grow and shrink with the frame; the canvas's own group transform is not used).
        if (data.Elements().FirstOrDefault(e => e.Name.LocalName == "lockedCanvas") is { } canvas)
        {
            var group = FromCanvas(canvas);
            // The shapes' own boxes, a turned shape's its geometry's as turned (flipped first); those in groups mapped
            // through their groups (docx/external/libreoffice/extras-ooxmlimport/fdo43641: a rectangle and a
            // connector in a group of the canvas reaching past them, the box around the two stretched over the frame,
            // not the group's; docx/local/lcv-*, lcw-*: two shapes stretched over frames of other sizes and shapes,
            // offset, in a stretching group, the canvas's own transform unused; a rectangle turned by 30, 60, 90 and
            // 308 degrees by its turned corners, an ellipse turned by 45 by its curve, a text box's text not counted).
            static List<(double X, double Y, double W, double H)> Boxes(XElement container)
            {
                var result = new List<(double X, double Y, double W, double H)>();
                foreach (var e in container.Elements())
                {
                    var x = (e.Element(Ns.P + "spPr") ?? e.Element(Ns.P + "grpSpPr"))?.Element(Ns.A + "xfrm") ?? e.Element(Ns.P + "xfrm");
                    if (x?.Element(Ns.A + "ext") == null)
                        continue;
                    var (bx, by, bw, bh, turn, flipH, flipV) = ReadTransform(x);
                    if (e.Name.LocalName != "grpSp")
                    {
                        result.Add(Math.Abs(Math.IEEERemainder(turn, 360)) < 1e-9 ? (bx, by, bw, bh) : TurnedBounds(SpPr(e), bx, by, bw, bh, turn, flipH, flipV));
                        continue;
                    }
                    var (chOff, chExt) = (x.Element(Ns.A + "chOff"), x.Element(Ns.A + "chExt"));
                    var (cw, ch) = (chExt.Long("cx") ?? 0, chExt.Long("cy") ?? 0);
                    var (ox, oy) = cw > 0 && ch > 0 ? (chOff.Long("x") ?? 0, chOff.Long("y") ?? 0) : (0L, 0L);
                    var (sx, sy) = (cw > 0 ? bw / cw : 1, ch > 0 ? bh / ch : 1);
                    result.AddRange(Boxes(e).Select(b => (bx + (b.X - ox) * sx, by + (b.Y - oy) * sy, b.W * sx, b.H * sy)));
                }
                return result;
            }
            var boxes = Boxes(group);
            if (boxes.Count == 0)
                return;
            var (cx0, cy0) = (boxes.Min(b => b.X), boxes.Min(b => b.Y));
            var (cw, ch) = (Math.Max(1, boxes.Max(b => b.X + b.W) - cx0), Math.Max(1, boxes.Max(b => b.Y + b.H) - cy0));
            var (fx, fy, fw, fh, _, _, _) = ReadTransform(xfrm);
            group.Element(Ns.P + "grpSpPr")?.Remove();
            group.AddFirst(new XElement(Ns.P + "grpSpPr", new XElement(Ns.A + "xfrm",
                new XElement(Ns.A + "off", new XAttribute("x", (long)fx), new XAttribute("y", (long)fy)),
                new XElement(Ns.A + "ext", new XAttribute("cx", (long)cw), new XAttribute("cy", (long)ch)),
                new XElement(Ns.A + "chOff", new XAttribute("x", (long)cx0), new XAttribute("y", (long)cy0)),
                new XElement(Ns.A + "chExt", new XAttribute("cx", (long)cw), new XAttribute("cy", (long)ch)))));
            var content = new List<SceneItem>();
            RenderGroup(group, part, content, placement, isSlide: false);
            var (sx, sy) = (fw / cw, fh / ch);
            var (px, py, _, _) = placement.Map(fx, fy, fw, fh);
            var origin = ToPx(placement.Transform.Then(Matrix.Translate(px, py)));
            var (ox, oy) = (origin.E, origin.F);
            items.Add(new BoxItem(null, new Matrix(sx, 0, 0, sy, ox * (1 - sx), oy * (1 - sy)), "", content));
            return;
        }

        // OLE objects (catalog: picture.ole): PowerPoint stores a picture of the object, usually an EMF; PowerPoint 2007
        // only in the slide's VML drawing (catalog: legacy.vml).
        if (data.Element(Ns.P + "oleObj") is { } ole)
        {
            if (ole.Element(Ns.P + "pic") is { } preview)
                RenderShape(WithFrameTransform(preview, xfrm), part, items, placement, isSlide: true);
            else if (LegacyDrawing.Find(slide.Presentation, part, ole.Str("spid")) is { } legacy)
            {
                var (x, y, w, h, _, _, _) = ReadTransform(xfrm);
                var frameId = NonVisual(frame)?.Str("id") ?? "0";
                RenderShape(LegacyDrawing.Element(legacy, frameId, NonVisual(frame)?.Str("name") ?? "", (x, y, w, h)), legacy.Part, items, placement, isSlide);
            }
            return;
        }

        if ((data.Element(Ns.C + "chart") ?? data.Element(Charts.ChartExModel.Cx + "chart"))?.Attribute(Ns.R + "id") is { } chartId
            && slide.Presentation.Package.Relationship(part, chartId.Value) is { External: false } chartRel)
        {
            var (x, y, w, h, _, _, _) = ReadTransform(xfrm);
            var (bx, by, bw, bh) = placement.Map(x, y, w, h);
            var matrix = placement.Transform.Then(LocalMatrix(bx, by, bw, bh, 0, false, false));
            var (pw, ph) = (Units.ToPx(bw), Units.ToPx(bh));
            var svg = new Charts.ChartRenderer(slide, paint, text, chartRel.Target).Render(bw / Units.EmuPerPoint, bh / Units.EmuPerPoint, Units.ToPx(matrix.E), Units.ToPx(matrix.F));
            items.Add(Framed(pw, ph, ToPx(matrix), Accessibility(frame, isSlide, part), svg));
            // Shapes drawn on the chart (c:userShapes, catalog: chart.userShapes), placed by their anchors.
            var package = slide.Presentation.Package;
            if (package.FirstRelationship(chartRel.Target, "chartUserShapes")?.Target is { } shapesPart && package.Exists(shapesPart))
            {
                // Their text defaults to the chart's text: its c:txPr, else 10 pt (measured).
                var chartText = slide.Presentation.Load(chartRel.Target).Root?.Element(Ns.C + "txPr")?.Descendants(Ns.A + "defRPr").FirstOrDefault();
                var defaults = new XElement(Ns.A + "defRPr", new XAttribute("sz", "1000"));
                foreach (var attribute in chartText?.Attributes() ?? [])
                    defaults.SetAttributeValue(attribute.Name, attribute.Value);
                defaults.Add(chartText?.Elements());
                textDefaults = new XElement(Ns.A + "lstStyle", Enumerable.Range(1, 9).Select(level => new XElement(Ns.A + $"lvl{level}pPr", new XElement(defaults))));
                RenderUserShapes(slide.Presentation.Load(shapesPart).Root, shapesPart, items, placement, (x, y, w, h));
                textDefaults = null;
            }
            return;
        }

        if (data.Element(Ns.A + "tbl") is { } table)
        {
            var (x, y, w, h, rotation, flipH, flipV) = ReadTransform(xfrm);
            var (bx, by, bw, bh) = placement.Map(x, y, w, h, rotation);
            text.Grid = Grid(placement.OnSlide(placement.Transform.Then(LocalMatrix(bx, by, bw, bh, rotation, flipH, flipV))));
            var (drawn, width, height) = Tables.Render(table);
            text.Grid = null;
            var matrix = placement.Transform.Then(LocalMatrix(bx, by, Math.Max(bw, width * Units.EmuPerPixel), Math.Max(bh, height * Units.EmuPerPixel), rotation, flipH, flipV));
            // On device pixels that map text and tables apart from shapes (PixelGrid.Text), scaled about the slide's corner.
            var scale = slide.Presentation.Pixels.Text;
            if (scale != 1)
                matrix = matrix with { A = matrix.A * scale, B = matrix.B * scale, C = matrix.C * scale, D = matrix.D * scale, E = matrix.E * scale, F = matrix.F * scale };
            items.Add(Framed(width, height, ToPx(matrix), Accessibility(frame, isSlide, part), drawn));
        }
    }

    /// <summary>
    /// Where a box with this transform (EMU) stands on the slide in pixels, where it is only moved (see
    /// <see cref="TextRenderer.Grid"/>); null where it is turned, scaled or flipped, or lies on a Word page.
    /// </summary>
    (double X, double Y)? Grid(Matrix m) =>
        !slide.Presentation.Word && Math.Abs(m.A - 1) < 1e-9 && Math.Abs(m.B) < 1e-9 && Math.Abs(m.C) < 1e-9 && Math.Abs(m.D - 1) < 1e-9
            ? (Units.ToPx(m.E), Units.ToPx(m.F)) : null;

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

    /// <summary>
    /// The width PowerPoint draws an outline with: whole pixels, at least one (measured with local/lw-probe at the
    /// references' 96 dpi: 0.25 to 1 pt draw 1 px, 1.25 and 1.5 pt 2 px, 2 and 2.25 pt 3 px, 3 pt 4 px; rectangles,
    /// lines, ellipses and rounded rectangles alike).
    /// </summary>
    static double PixelWidth(Outline outline) => Math.Max(1, Math.Floor(Units.ToPx(outline.WidthEmu) + 0.5));

    /// <summary>
    /// How much of its first pixel the outline of a curved path (OnPixels) two pixels wide or more leaves out on its
    /// low sides, its left and top ones: PowerPoint's antialiasing misses one sample there, 1/8 along a row and 1/4
    /// down a column (measured with rounded rectangles of 1 to 3 px at eighth-pixel offsets, any line cap: their sides
    /// cover 7/8 and 3/4 of that pixel, the pixel at the high side wholly, 1 px outlines and rectangles every pixel
    /// wholly); drawn as the outline that much thinner, moved by half of it.
    /// </summary>
    const double CurvedLowSide = 3 / 16.0;

    static bool Curved(EvaluatedPath path) => path.Segments.Any(s => s.Command is 'A' or 'C' or 'Q');

    /// <summary>
    /// The outline of a shape that is not turned on PowerPoint's pixels: every edge rounded to the nearest pixel
    /// boundary (half up) on its own, the right and bottom ones too, a stroke of <paramref name="width"/> whole pixels
    /// then covering floor(width / 2) pixels before the boundary and the rest after it (measured with local/lw-probe:
    /// a 1 px line on the row after the rounded edge, 2 px on both sides, 3 px one before and two after). A path of
    /// horizontal and vertical lines has each corner rounded so; any other path is stretched with its box (<paramref
    /// name="box"/>, its width and height), whose edges are rounded so, and only moved along a side that would round
    /// to nothing (measured: the sides of a rounded rectangle are as sharp as a rectangle's, LibreOffice's oox/theme
    /// even on its right and bottom; an ellipse sits where its box does; LibreOffice's fdo47434, connectors sloping by
    /// 0.02 px over 210 px, keep their slope). A curved path's outline of two pixels or more moves by half of what it
    /// leaves out on its low sides (CurvedLowSide). On device pixels of their own (<paramref name="device"/>, PixelGrid)
    /// PowerPoint places the outline so on the slide's pixels first and then maps it onto the device's, where its
    /// band's edges lie on the next of its antialiasing samples, a quarter's top down a column and an eighth's left along
    /// a row (measured with probe decks of rectangles at 1058.33 by 793.67 px, drawn at 1058 by 794: an outline at 60.3 px
    /// on row 60 covers 3/4 of it and 1/4 of the next, at 380.3 px half of each; LibreOffice's tdf149551_TextRot and
    /// trailing-paragraphs). A width of 0 places the edges of a fill so.
    /// </summary>
    static EvaluatedPath OnPixels(EvaluatedPath path, Matrix toSlide, double width, (double W, double H)? box = null, PixelGrid device = default)
    {
        var fromSlide = toSlide.Invert();
        var shift = width % 2 == 1 ? 0.5 : 0;
        double SnapX(double v) => device.SampledX(Math.Floor(v + 0.5) + shift, width);
        double SnapY(double v) => device.SampledY(Math.Floor(v + 0.5) + shift, width);
        (double X, double Y) Map(Matrix m, double x, double y) => (m.A * x + m.C * y + m.E, m.B * x + m.D * y + m.F);
        if (box is not var (w, h))
        {
            return path with
            {
                Segments = path.Segments.Select(segment =>
                {
                    if (segment.Command is not ('M' or 'L'))
                        return segment;
                    var (sx, sy) = Map(toSlide, segment.Values[0], segment.Values[1]);
                    var (x, y) = Map(fromSlide, SnapX(sx), SnapY(sy));
                    return new PathSegment(segment.Command, [x, y]);
                }).ToList(),
            };
        }

        // The box on the slide, its edges rounded, and the stretch that takes the path along (per axis: the shape is not
        // turned); a side that would round to nothing keeps its length and moves.
        var (x0, y0) = Map(toSlide, 0, 0);
        var (x1, y1) = Map(toSlide, w, h);
        (double Edge, double Rounded, double Scale) Axis(double a, double b, Func<double, double> snap)
        {
            var (low, high) = (Math.Min(a, b), Math.Max(a, b));
            var size = snap(high) - snap(low);
            return (low, snap(low), size > 0.5 && high - low > 1e-9 ? size / (high - low) : 1);
        }
        var (edgeX, roundedX, kx) = Axis(x0, x1, SnapX);
        var (edgeY, roundedY, ky) = Axis(y0, y1, SnapY);
        var (dx, dy) = (0.0, 0.0);
        // A straight line that slopes by less than a sample is antialiased where PowerPoint's samples see its band, as a
        // table's border is (TableRenderer.Sampled): the band moves to them as a whole (measured: LibreOffice's fdo47434,
        // connectors sloping by 0.02 px cover 3/4 of one row and 1/4 of the next).
        if (path.Segments is [{ Command: 'M' } from, { Command: 'L' } to])
        {
            var (ax, ay) = Map(toSlide, from.Values[0], from.Values[1]);
            var (bx, by) = Map(toSlide, to.Values[0], to.Values[1]);
            var (mx, my) = (roundedX + ((ax + bx) / 2 - edgeX) * kx, roundedY + ((ay + by) / 2 - edgeY) * ky);
            var (sx, sy) = (device.X is 0 ? 1 : device.X, device.Y is 0 ? 1 : device.Y);
            if (Math.Abs(by - ay) < 0.25 && Math.Abs(bx - ax) > 1 && (my - width / 2) * sy is var top)
                dy = (Math.Ceiling(top * 4 - 1e-9) / 4 - top) / sy;
            else if (Math.Abs(bx - ax) < 0.125 && Math.Abs(by - ay) > 1 && (mx - width / 2) * sx is var start)
                dx = (Math.Ceiling(start * 8 - 1e-9) / 8 - start) / sx;
        }
        else if (width >= 2 && Curved(path))
            (dx, dy) = (CurvedLowSide / 2, CurvedLowSide / 2);
        (double X, double Y) Moved(double x, double y)
        {
            var (sx, sy) = Map(toSlide, x, y);
            return Map(fromSlide, roundedX + (sx - edgeX) * kx + dx, roundedY + (sy - edgeY) * ky + dy);
        }
        // Arc radii stretch with the axis they lie along in the shape (swapped where it is turned by 90°).
        var (rx, ry) = Math.Abs(toSlide.B) < 1e-9 ? (kx, ky) : (ky, kx);
        return path with
        {
            Segments = path.Segments.Select(segment =>
            {
                var v = segment.Values;
                switch (segment.Command)
                {
                    case 'A':
                        var (ex, ey) = Moved(v[5], v[6]);
                        return new PathSegment('A', [v[0] * rx, v[1] * ry, v[2], v[3], v[4], ex, ey]);
                    case 'Z':
                        return segment;
                    default:
                        var moved = new double[v.Length];
                        for (var i = 0; i + 1 < v.Length; i += 2)
                            (moved[i], moved[i + 1]) = Moved(v[i], v[i + 1]);
                        return new PathSegment(segment.Command, moved);
                }
            }).ToList(),
        };
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
    /// the data model's dsp:dataModelExt. Its shapes are drawn like slide shapes into the frame, an element of its
    /// own that animations of the whole diagram change; each shape carries its model id, which animations of single
    /// shapes target (catalog: smartart.data). The data model's background (dgm:bg: fill and effects) lies behind
    /// the shapes, its outline (dgm:whole: line and effects) on top of them (measured).
    /// </summary>
    void RenderDiagram(XElement relIds, XElement frame, string part, List<SceneItem> items, Placement placement, bool isSlide)
    {
        var package = slide.Presentation.Package;
        var dataPart = relIds.Attribute(Ns.R + "dm") is { } dm ? package.Relationship(part, dm.Value)?.Target : null;
        if (dataPart == null || !package.Exists(dataPart))
            return;
        var dataModel = slide.Presentation.Load(dataPart).Root;
        if (dataModel == null)
            return;
        var drawingId = dataModel.Descendants(Dsp + "dataModelExt").FirstOrDefault()?.Str("relId");
        var drawingPart = drawingId != null ? package.Relationship(part, drawingId)?.Target : null;
        var tree = drawingPart != null && package.Exists(drawingPart) ? slide.Presentation.Load(drawingPart).Root?.Element(Dsp + "spTree") : null;
        // A drawing without shapes (written so by other programs) counts as none: PowerPoint lays the diagram out
        // anew (measured with LibreOffice's test decks, whose drawings are emptied).
        if (tree != null && !tree.Elements().Any(e => e.Name.LocalName is "sp" or "grpSp" or "cxnSp" or "pic" or "graphicFrame"))
            tree = null;
        XElement? DiagramPart(string attribute) =>
            relIds.Attribute(Ns.R + attribute) is { } id && package.Relationship(part, id.Value)?.Target is { } target && package.Exists(target)
                ? slide.Presentation.Load(target).Root
                : null;
        var layoutDef = DiagramPart("lo");
        // Without a saved drawing the layout engine lays the diagram out (catalog: smartart.layout).
        if (tree == null && layoutDef == null)
            return;
        var (x, y, w, h, _, _, _) = ReadTransform(frame.Element(Ns.P + "xfrm")!);
        var (bx, by, bw, bh) = placement.Map(x, y, w, h);
        var box = placement.Transform.Then(Matrix.Translate(bx, by));
        var flat = new Placement(Matrix.Identity, 0, 0, 1, 1, 0, 0, placement.GroupFill) { Container = placement.OnSlide(box) };
        var diagramBox = new BoxItem((Units.ToPx(bw), Units.ToPx(bh)), ToPx(box), Accessibility(frame, isSlide, part), []);
        items.Add(diagramBox);

        // The diagram's own background and outline: a rectangle over the frame, each an element of its own, which
        // the animation of the diagram's background targets (a:dgm bldStep="bg").
        void Whole(XElement? formatting, bool outline)
        {
            if (formatting == null || !formatting.HasElements)
                return;
            var spPr = new XElement(Ns.P + "spPr",
                new XElement(Ns.A + "xfrm", new XElement(Ns.A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
                    new XElement(Ns.A + "ext", new XAttribute("cx", (long)Math.Round(bw)), new XAttribute("cy", (long)Math.Round(bh)))),
                new XElement(Ns.A + "prstGeom", new XAttribute("prst", "rect"), new XElement(Ns.A + "avLst")),
                outline ? new XElement(Ns.A + "noFill") : formatting.Elements().Where(e => e.Name.LocalName.EndsWith("Fill", StringComparison.Ordinal)),
                outline ? formatting.Element(Ns.A + "ln") : new XElement(Ns.A + "ln", new XElement(Ns.A + "noFill")),
                formatting.Element(Ns.A + "effectLst"), formatting.Element(Ns.A + "effectDag"));
            var shape = new XElement(Ns.P + "sp",
                new XElement(Ns.P + "nvSpPr", new XElement(Ns.P + "cNvPr", new XAttribute("id", 0), new XAttribute("name", "")),
                    new XElement(Ns.P + "cNvSpPr"), new XElement(Ns.P + "nvPr")), spPr);
            var backgroundBox = new BoxItem((Units.ToPx(bw), Units.ToPx(bh)), null, " data-dgm-bg=\"\"", []);
            diagramBox.Items.Add(backgroundBox);
            RenderShape(shape, dataPart, backgroundBox.Items, flat, isSlide: false);
        }
        var dgm = dataModel.Name.Namespace;
        Whole(dataModel.Element(dgm + "bg"), outline: false);
        var styling = new SmartArt.Styling(DiagramPart("cs"), DiagramPart("qs"), slide.Colors);
        // The quick style's 3D scene holds the whole diagram (measured: 3D styles turn the diagram as one).
        var inner = styling.DiagramScene is { } diagramScene && Camera.Read(diagramScene) is { Flat: false }
            ? flat with { Scene = (diagramScene, bw / 2, bh / 2) }
            : flat;
        var model = new SmartArt.DataModel(dataModel);
        var listStyle = SmartArt.Styling.ListStyle(layoutDef);
        if (tree == null)
        {
            var root = SmartArt.Engine.Layout(layoutDef!, model, bw / Units.EmuPerPoint, bh / Units.EmuPerPoint, (body, width) => text.LayoutCell(body, [], width));
            RenderTree(SmartArt.Output.Shapes(root, styling, listStyle), dataPart, diagramBox.Items, inner, isSlide: false);
            Whole(dataModel.Element(dgm + "whole"), outline: true);
            return;
        }
        var shapes = ToPresentationMl(tree);
        // The drawing keeps the style's text color in fontRef; PowerPoint draws the text in the color the color
        // definition gives the shape's style label (txFillClrLst), when it gives one (catalog: smartart.styles).
        foreach (var sp in shapes.Descendants(Ns.P + "sp"))
        {
            if (sp.Attribute("modelId") is not { } modelId || !model.Points.TryGetValue(modelId.Value, out var pres)
                || sp.Element(Ns.P + "style")?.Element(Ns.A + "fontRef") is not { } fontRef)
                continue;
            var (index, count) = (int.TryParse(pres.Prop("presStyleIdx"), out var i) ? i : 0, int.TryParse(pres.Prop("presStyleCnt"), out var c) ? c : 1);
            if (styling.Color(pres.Prop("presStyleLbl"), "txFillClrLst", index, count) is { } textColor)
            {
                fontRef.RemoveNodes();
                fontRef.Add(textColor);
            }
        }
        // The layout's list style formats the text by the depth of its data points, which the drawing does not keep:
        // the layout tree tells which points each shape shows.
        if (layoutDef != null && listStyle != null)
        {
            var layoutTree = new SmartArt.LayoutTree(layoutDef, model);
            var bySaved = layoutTree.Root.Descendants().Prepend(layoutTree.Root).Where(n => n.Saved != null)
                .GroupBy(n => n.Saved!.Id).ToDictionary(g => g.Key, g => g.First());
            foreach (var sp in shapes.Descendants(Ns.P + "sp"))
            {
                if (sp.Attribute("modelId") is { } modelId && bySaved.TryGetValue(modelId.Value, out var node) && node.Text.Count > 0
                    && sp.Element(Ns.P + "txBody") is { } body)
                    SmartArt.Styling.ApplyListStyle(body, node.Text, listStyle);
            }
        }
        RenderTree(shapes, drawingPart!, diagramBox.Items, inner, isSlide: false);
        Whole(dataModel.Element(dgm + "whole"), outline: true);
    }

    /// <summary>The diagram drawing uses its own namespace for sp, spPr, style and txBody; map it to PresentationML.</summary>
    static XElement ToPresentationMl(XElement element) =>
        new(element.Name.Namespace == Dsp ? Ns.P + element.Name.LocalName : element.Name,
            element.Attributes(),
            element.Nodes().Select(n => n is XElement e ? ToPresentationMl(e) : n));

    /// <summary>Ink (p:contentPart with an InkML part, catalog: picture.ink) drawn into the part's box.</summary>
    void RenderContentPart(XElement element, string part, List<SceneItem> items, Placement placement, bool isSlide)
    {
        var xfrm = element.Element(P14 + "xfrm") ?? element.Element(Ns.P + "xfrm");
        if (xfrm?.Element(Ns.A + "ext") == null || element.Attribute(Ns.R + "id") is not { } id)
            return;
        var package = slide.Presentation.Package;
        if (package.Relationship(part, id.Value) is not { External: false } rel || !package.Exists(rel.Target))
            return;
        var ink = slide.Presentation.Load(rel.Target);
        if (!Ink.IsInk(ink))
            return;
        var (x, y, w, h, rotation, flipH, flipV) = ReadTransform(xfrm);
        var (bx, by, bw, bh) = placement.Map(x, y, w, h, rotation);
        var matrix = placement.Transform.Then(LocalMatrix(bx, by, bw, bh, rotation, flipH, flipV));
        var (pw, ph) = (Units.ToPx(bw), Units.ToPx(bh));
        var drawn = Ink.Render(ink, pw, ph);
        if (drawn.Count == 0)
            return;
        // The id lets animations reach the ink (p:inkTgt, or p:spTgt of the content part).
        items.Add(new BoxItem((pw, ph), ToPx(matrix), Accessibility(element, isSlide, part),
            [new WrappedItem($"<svg width=\"{Units.Css(Math.Max(pw, 1))}\" height=\"{Units.Css(Math.Max(ph, 1))}\" style=\"overflow:visible\">", drawn, "</svg>")]));
    }

    static readonly XNamespace Cdr = "http://schemas.openxmlformats.org/drawingml/2006/chartDrawing";

    /// <summary>Text defaults of the shapes being drawn (a chart's for its user shapes).</summary>
    XElement? textDefaults;

    /// <summary>
    /// The shapes of a chart's drawing part: each anchor (relSizeAnchor: corners as fractions of the chart frame,
    /// absSizeAnchor: a corner and a size in EMU) puts its shape into the frame <paramref name="frame"/> (slide EMU);
    /// the shapes are drawn like slide shapes.
    /// </summary>
    void RenderUserShapes(XElement? root, string part, List<SceneItem> items, Placement placement, (double X, double Y, double W, double H) frame)
    {
        var tree = new XElement(Ns.P + "spTree");
        foreach (var anchor in root?.Elements() ?? [])
        {
            double Fraction(XElement? corner, string axis) => double.TryParse(corner?.Element(Cdr + axis)?.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
            var from = anchor.Element(Cdr + "from");
            var (x, y) = (frame.X + Fraction(from, "x") * frame.W, frame.Y + Fraction(from, "y") * frame.H);
            double w, h;
            if (anchor.Name == Cdr + "relSizeAnchor")
            {
                var to = anchor.Element(Cdr + "to");
                (w, h) = (frame.X + Fraction(to, "x") * frame.W - x, frame.Y + Fraction(to, "y") * frame.H - y);
            }
            else if (anchor.Name == Cdr + "absSizeAnchor")
            {
                var ext = anchor.Element(Cdr + "ext");
                (w, h) = (ext.Long("cx") ?? 0, ext.Long("cy") ?? 0);
            }
            else
            {
                continue;
            }
            var shape = anchor.Elements().FirstOrDefault(e => e.Name.Namespace == Cdr && e.Name.LocalName is "sp" or "grpSp" or "pic" or "cxnSp" or "graphicFrame");
            if (shape == null)
                continue;
            var converted = FromChartDrawing(shape);
            // The anchor places the shape; its own xfrm only keeps the turn, the flips and a group's child space.
            var properties = converted.Element(Ns.P + "spPr") ?? converted.Element(Ns.P + "grpSpPr");
            var xfrm = properties?.Element(Ns.A + "xfrm") ?? converted.Element(Ns.P + "xfrm");
            if (xfrm == null && properties != null)
            {
                xfrm = new XElement(Ns.A + "xfrm");
                properties.AddFirst(xfrm);
            }
            if (xfrm == null)
                continue;
            xfrm.Element(Ns.A + "off")?.Remove();
            xfrm.Element(Ns.A + "ext")?.Remove();
            xfrm.AddFirst(new XElement(Ns.A + "off", new XAttribute("x", (long)Math.Round(x)), new XAttribute("y", (long)Math.Round(y))),
                new XElement(Ns.A + "ext", new XAttribute("cx", (long)Math.Round(w)), new XAttribute("cy", (long)Math.Round(h))));
            tree.Add(converted);
        }
        RenderTree(tree, part, items, placement, isSlide: false);
    }

    /// <summary>
    /// A locked canvas in PresentationML names: the canvas a group (p:grpSp), its DrawingML shapes (a:sp, a:pic,
    /// a:cxnSp, a:grpSp, a:graphicFrame) and their properties as p: elements, the shape text (a:txSp/a:txBody) as
    /// p:txBody (catalog: text.body; useSpRect, the shape's own rectangle for the text, is what the shapes have anyway).
    /// </summary>
    static XElement FromCanvas(XElement element)
    {
        static XName Name(XElement e) => e.Name.LocalName switch
        {
            "lockedCanvas" => Ns.P + "grpSp",
            "sp" or "pic" or "cxnSp" or "grpSp" or "graphicFrame" or "nvSpPr" or "cNvSpPr" or "nvPicPr" or "cNvPicPr" or "nvCxnSpPr" or "cNvCxnSpPr"
                or "nvGrpSpPr" or "cNvGrpSpPr" or "nvGraphicFramePr" or "cNvGraphicFramePr" or "cNvPr" or "nvPr" or "spPr" or "grpSpPr" or "style"
                or "blipFill" when e.Name.Namespace == Ns.A || e.Name.LocalName == "lockedCanvas" => Ns.P + e.Name.LocalName,
            _ => e.Name,
        };
        var converted = new XElement(Name(element), element.Attributes());
        foreach (var node in element.Nodes())
        {
            if (node is not XElement child)
            {
                converted.Add(node);
                continue;
            }
            if (child.Name == Ns.A + "txSp")
            {
                // The text of a DrawingML shape: its body becomes the shape's p:txBody. It inherits no text styles:
                // runs without a Latin font are Arial (measured).
                if (child.Element(Ns.A + "txBody") is { } body)
                {
                    var text = new XElement(Ns.P + "txBody", body.Attributes(), body.Nodes().Select(n => n is XElement e ? new XElement(e) : n));
                    foreach (var properties in text.Descendants().Where(e => e.Name == Ns.A + "rPr" || e.Name == Ns.A + "endParaRPr"))
                    {
                        if (properties.Element(Ns.A + "latin") == null)
                            properties.Add(new XElement(Ns.A + "latin", new XAttribute("typeface", "Arial")));
                    }
                    converted.Add(text);
                }
                continue;
            }
            // Graphic frames keep their graphic in a: names; their transform is p:xfrm.
            if (child.Name == Ns.A + "xfrm" && element.Name.LocalName == "graphicFrame")
            {
                converted.Add(new XElement(Ns.P + "xfrm", child.Attributes(), child.Nodes()));
                continue;
            }
            converted.Add(child.Name == Ns.A + "graphic" ? new XElement(child) : FromCanvas(child));
        }
        return converted;
    }

    /// <summary>A shape of a chart drawing (cdr:) in PresentationML (p:) names.</summary>
    static XElement FromChartDrawing(XElement element) =>
        new(element.Name.Namespace == Cdr ? Ns.P + element.Name.LocalName : element.Name,
            element.Attributes(),
            element.Nodes().Select(n => n is XElement e ? FromChartDrawing(e) : n));

    void RenderGroup(XElement group, string part, List<SceneItem> items, Placement placement, bool isSlide)
    {
        var spPr = group.Element(Ns.P + "grpSpPr");
        var xfrm = spPr?.Element(Ns.A + "xfrm");
        if (xfrm?.Element(Ns.A + "ext") == null)
        {
            RenderTree(group, part, items, placement, isSlide);
            return;
        }

        var (x, y, w, h, rotation, flipH, flipV) = ReadTransform(xfrm);
        var (bx, by, bw, bh) = placement.Map(x, y, w, h, rotation);
        var chOff = xfrm.Element(Ns.A + "chOff");
        var chExt = xfrm.Element(Ns.A + "chExt");
        var chW = chExt.Long("cx") ?? w;
        var chH = chExt.Long("cy") ?? h;

        var (cx, cy) = (bx + bw / 2, by + bh / 2);
        var rotFlip = Matrix.Translate(cx, cy).Then(Matrix.Rotate(rotation)).Then(Matrix.Scale(flipH ? -1 : 1, flipV ? -1 : 1)).Then(Matrix.Translate(-cx, -cy));

        var groupFill = FillReader.FindFill(spPr) is { } f ? FillReader.Read(f, slide.Colors, slide.Images(part), slide.ImageSizes(part)) : null;
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
        // In Word the group's own effects (its grpSpPr's a:effectLst) work on all of it: a glow grows from the union
        // of its shapes, behind them all (Word's PDF of LibreOffice's tdf156902_GlowOnGroup: a 36 pt glow around a
        // triangle with an oval inside it; docx/local/ggl-group: two rectangles 12 pt apart glow as each with the glow
        // would, but for the gap's corners). PowerPoint draws none (external/poi/60810 slide 22: a group's 18 pt glow
        // and its shadow). The filter takes the group's layer, whose origin is its parent's.
        var filter = "";
        EffectModel? model = null;
        if (slide.Presentation.Word && spPr?.Element(Ns.A + "effectLst") is { HasElements: true } effects)
        {
            var turn = ToPx(placement.Transform.Then(rotFlip));
            var corners = new[] { (X: bx, Y: by), (X: bx + bw, Y: by), (X: bx, Y: by + bh), (X: bx + bw, Y: by + bh) }
                .Select(p => (X: turn.A * Units.ToPx(p.X) + turn.C * Units.ToPx(p.Y) + turn.E, Y: turn.B * Units.ToPx(p.X) + turn.D * Units.ToPx(p.Y) + turn.F)).ToList();
            var (left, top) = (corners.Min(p => p.X), corners.Min(p => p.Y));
            var defs = new StringBuilder();
            if (SvgEffects.FilterOf(effects, slide.Colors, Units.ToPx(bw), Units.ToPx(bh), defs,
                    region: (left, top, corners.Max(p => p.X) - left, corners.Max(p => p.Y) - top)) is { } made)
            {
                items.Add(new WrappedItem("<svg width=\"0\" height=\"0\" style=\"position:absolute\" aria-hidden=\"true\">", [new DefsItem(defs.ToString())], "</svg>"));
                filter = $" style=\"filter:url(#{made.Id})\"";
                model = made.Model;
            }
        }
        var grouped = new GroupBoxItem(filter + Accessibility(group, isSlide, part), []) { Effects = model };
        items.Add(grouped);
        RenderTree(group, part, grouped.Items, child, isSlide);
    }

    static readonly XNamespace Adec = "http://schemas.microsoft.com/office/drawing/2017/decorative";

    /// <summary>Text markup without its run links (inside a linked shape).</summary>
    /// <summary>Marks where a backdrop's shadow takes the shape's text once it is laid out.</summary>
    const string BackdropTextToken = "\u0001backdrop-text\u0001";

    /// <summary>Text markup as a silhouette for a shadow: without ids (the text itself keeps them), links and filters.</summary>
    static string Silhouette(string markup) =>
        System.Text.RegularExpressions.Regex.Replace(UnlinkText(markup), " (?:id|filter)=\"[^\"]*\"", "");

    static string UnlinkText(string markup) =>
        System.Text.RegularExpressions.Regex.Replace(markup, "<a(?: [^>]*)?>(?:<title>[^<]*</title>)?|</a>", "");

    /// <summary>Items without their links (see UnlinkText): a link's items stand in its place.</summary>
    static List<SceneItem> Unlinked(List<SceneItem> items)
    {
        var result = new List<SceneItem>(items.Count);
        foreach (var item in items)
        {
            if (item is WrappedItem { After: "</a>" } link && link.Before.StartsWith("<a", StringComparison.Ordinal))
            {
                result.AddRange(Unlinked(link.Items));
                continue;
            }
            if (item is MarkupItem markup)
            {
                result.Add(new MarkupItem(UnlinkText(markup.Html)));
                continue;
            }
            if (item switch
                {
                    SvgGroupItem group => group.Items, TransformItem transformed => transformed.Items, FilterItem filtered => filtered.Items,
                    FilterCopyItem copied => copied.Items, ReflectionItem reflected => reflected.Items, RotatedItem rotated => rotated.Items,
                    WrappedItem wrapped => wrapped.Items, _ => null,
                } is { } children)
            {
                var inner = Unlinked(children);
                children.Clear();
                children.AddRange(inner);
            }
            result.Add(item);
        }
        return result;
    }

    static XElement? NonVisual(XElement shape) =>
        shape.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("nv", StringComparison.Ordinal))?.Elements().FirstOrDefault(e => e.Name.LocalName == "cNvPr");

    static bool IsHidden(XElement shape) => NonVisual(shape).Bool("hidden") == true;

    /// <summary>
    /// Shape id for the player (slide shapes only; ids repeat across master and layout) and accessibility
    /// attributes: alt text, title, decorative shapes hidden from assistive technology (catalog: shape.accessibility).
    /// </summary>
    string Accessibility(XElement shape, bool isSlide, string part)
    {
        // SmartArt shapes carry their model id, which animations of single diagram shapes target (catalog: smartart.data).
        var model = shape.Str("modelId") is { } modelId ? $" data-mid=\"{System.Net.WebUtility.HtmlEncode(modelId)}\"" : "";
        var c = NonVisual(shape);
        if (c == null)
            return model;
        var sb = new StringBuilder(model);
        if (isSlide && c.Str("id") is { } id)
            sb.Append($" data-id=\"{System.Net.WebUtility.HtmlEncode(id)}\"");
        if (isSlide && Morph != null && c.Str("name") is { Length: > 0 } name)
        {
            sb.Append($" data-name=\"{System.Net.WebUtility.HtmlEncode(name)}\"");
            // The geometry, so that morph knows when to turn one outline into another.
            var spPr = shape.Element(Ns.P + "spPr");
            var geometry = spPr?.Element(Ns.A + "prstGeom")?.Str("prst") ?? (spPr?.Element(Ns.A + "custGeom") != null ? "custom" : null);
            if (geometry != null)
                sb.Append($" data-geom=\"{System.Net.WebUtility.HtmlEncode(geometry)}\"");
        }
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
            if (Geometry(spPr) is not { } geometry)
                continue;
            // PowerPoint keeps a sketch only on a custom geometry; on a preset it draws the sketch's original
            // geometry without the sketch, and nothing if the sketch has none (catalog: shape.sketched).
            if (spPr!.Element(Ns.A + "prstGeom") != null && Sketch(spPr) is { } sketch)
                return Geometry(sketch) ?? (GeometryDefinition.Parse(new XElement(Ns.A + "custGeom")), []);
            return geometry;
        }
        return (PresetGeometries.Get("rect")!, []);
    }

    /// <summary>
    /// The box around a turned shape's geometry (its paths flattened, flipped, then turned about the box's center), its
    /// box's turned corners where it draws none; in EMU, as the box.
    /// </summary>
    static (double X, double Y, double W, double H) TurnedBounds(XElement? spPr, double x, double y, double w, double h, double turn, bool flipH, bool flipV)
    {
        var (definition, adjustments) = Geometry(spPr) ?? (PresetGeometries.Get("rect")!, Array.Empty<Guide>());
        // The evaluator takes EMU and gives pixels.
        var points = GeometryEvaluator.Evaluate(definition, w, h, adjustments).Paths
            .SelectMany(p => CompoundLines.Flatten(p.Segments)).SelectMany(f => f.Points)
            .Select(p => (p.X * Units.EmuPerPixel, p.Y * Units.EmuPerPixel)).ToList();
        if (points.Count == 0)
            points = [(0, 0), (w, 0), (w, h), (0, h)];
        var (sin, cos) = Math.SinCos(turn * Math.PI / 180);
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var (px, py) in points)
        {
            var (dx, dy) = ((flipH ? w - px : px) - w / 2, (flipV ? h - py : py) - h / 2);
            var (tx, ty) = (x + w / 2 + dx * cos - dy * sin, y + h / 2 + dx * sin + dy * cos);
            (minX, minY, maxX, maxY) = (Math.Min(minX, tx), Math.Min(minY, ty), Math.Max(maxX, tx), Math.Max(maxY, ty));
        }
        return (minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>The preset or custom geometry among the children of <paramref name="parent"/>, or null.</summary>
    static (GeometryDefinition Definition, IEnumerable<Guide> Adjustments)? Geometry(XElement? parent)
    {
        if (parent?.Element(Ns.A + "prstGeom") is { } preset)
        {
            var definition = PresetGeometries.Get(preset.Str("prst") ?? "rect") ?? PresetGeometries.Get("rect")!;
            var adjustments = preset.Element(Ns.A + "avLst")?.Elements(Ns.A + "gd")
                .Select(g => new Guide(g.Str("name") ?? "", g.Str("fmla") ?? "val 0")) ?? [];
            return (definition, adjustments.ToList());
        }
        return parent?.Element(Ns.A + "custGeom") is { } custom ? (GeometryDefinition.Parse(custom), []) : null;
    }

    /// <summary>
    /// The original geometry of a sketched shape (catalog: shape.sketched): PowerPoint saves the hand-drawn paths it
    /// derives from the original geometry and a seed as the shape's custom geometry and keeps the original in the
    /// sketch properties of the outline (ask:lineSketchStyleProps), and only on a custom geometry.
    /// </summary>
    static (GeometryDefinition Definition, IEnumerable<Guide> Adjustments)? SketchedGeometry(List<PartElement> chain)
    {
        foreach (var s in chain)
        {
            var spPr = SpPr(s.Element);
            if (spPr?.Element(Ns.A + "prstGeom") != null)
                return null;
            if (spPr?.Element(Ns.A + "custGeom") == null)
                continue;
            // A sketch of the style none, or without its original geometry, leaves the custom geometry as it is.
            var sketch = Sketch(spPr);
            var style = sketch?.Element(Ns.Ask + "type")?.Elements().FirstOrDefault()?.Name.LocalName;
            return style is null or "lineSketchNone" ? null : Geometry(sketch);
        }
        return null;
    }

    /// <summary>The sketch properties of a shape's outline (ask:lineSketchStyleProps), or null.</summary>
    static XElement? Sketch(XElement spPr) =>
        spPr.Element(Ns.A + "ln")?.Element(Ns.A + "extLst")?.Elements(Ns.A + "ext")
            .FirstOrDefault(e => e.Str("uri") == "{C807C97D-BFC1-408E-A445-0C87EB9F89A2}")?.Element(Ns.Ask + "lineSketchStyleProps");

    XElement? Effects(List<PartElement> chain, XElement? style) =>
        EffectContainer(chain, style) is { Name.LocalName: "effectLst" } list ? list : null;

    /// <summary>The shape's effects, a:effectLst or a:effectDag: its own, else the theme effect style's it refers to.</summary>
    XElement? EffectContainer(List<PartElement> chain, XElement? style)
    {
        static XElement? Of(XElement? parent) => parent?.Elements().FirstOrDefault(e => e.Name == Ns.A + "effectLst" || e.Name == Ns.A + "effectDag");
        foreach (var s in chain)
        {
            if (Of(SpPr(s.Element)) is { } own)
                return own;
        }
        var index = style?.Element(Ns.A + "effectRef").Int("idx") ?? 0;
        return index >= 1 && index <= slide.Theme.EffectStyles.Count ? Of(slide.Theme.EffectStyles[index - 1]) : null;
    }

    /// <summary>
    /// The shape's fill, level by level from the shape up through its layout and master placeholders: a level's own
    /// fill, else its style's fillRef (measured: a slide title's own style fill beats the master title's explicit
    /// noFill).
    /// </summary>
    Fill ResolveFill(List<PartElement> chain, Fill? groupFill)
    {
        for (var level = 0; level < chain.Count; level++)
        {
            if (FillReader.FindFill(SpPr(chain[level].Element)) is { } f)
            {
                // A gradient without stops takes the stops of the style's gradient, in its own direction (measured with
                // an ellipse whose gradFill has only a:lin: PowerPoint draws the theme's gradient in accent 1).
                if (f.Name.LocalName == "gradFill" && f.Element(Ns.A + "gsLst") == null
                    && chain.Skip(level).Select(s => StyleFill(s.Element)).FirstOrDefault(t => t != null) is { Style.Name.LocalName: "gradFill" } gradient
                    && gradient.Style.Element(Ns.A + "gsLst") is { } stops)
                {
                    var merged = new XElement(f);
                    merged.AddFirst(new XElement(stops));
                    return FillReader.Read(merged, gradient.Colors);
                }
                var fill = FillReader.Read(f, slide.Colors, slide.Images(chain[level].Part), slide.ImageSizes(chain[level].Part));
                return fill is GroupFill ? groupFill ?? Fill.None : fill;
            }
            if (StyleFill(chain[level].Element) is { } themed)
                return slide.ThemedFill(themed.Style, themed.Colors);
        }
        return Fill.None;
    }

    /// <summary>The fill a shape's own style refers to (p:style/a:fillRef) in the style's color, or null.</summary>
    (XElement Style, ColorContext Colors)? StyleFill(XElement shape) =>
        shape.Element(Ns.P + "style")?.Element(Ns.A + "fillRef") is { } fillRef && (fillRef.Int("idx") ?? 0) > 0
        && slide.Theme.FillStyle(fillRef.Int("idx")!.Value) is { } styleFill
            ? (styleFill, slide.Colors.WithPlaceholder(slide.Colors.ResolveChild(fillRef)))
            : null;

    /// <summary>
    /// The shape's outline: the levels' lines from the master placeholder down to the shape, each level's style line
    /// under its own line (as for fills, a slide title's style line beats the master's explicit a:ln noFill).
    /// </summary>
    Outline? ResolveOutline(List<PartElement> chain)
    {
        var layers = new List<(XElement, ColorContext)>();
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            if (chain[i].Element.Element(Ns.P + "style")?.Element(Ns.A + "lnRef") is { } lnRef && (lnRef.Int("idx") ?? 0) > 0
                && slide.Theme.LineStyle(lnRef.Int("idx")!.Value) is { } themed)
            {
                layers.Add((themed, slide.Colors.WithPlaceholder(slide.Colors.ResolveChild(lnRef))));
            }
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
        if (FillReader.Read(blipFill, slide.Colors, _ => image, slide.ImageSizes(part)) is not PictureFill fill)
            return Fill.None;
        // Word's PDF holds a cropped TIFF's own pixels (see PresentationContext.CroppedImage).
        if (slide.Presentation.Word && svg == null && embed != null && fill.Tiling == null && fill.SourceRect is var (l, t, r, b)
            && l >= 0 && t >= 0 && r >= 0 && b >= 0 && l + t + r + b > 0 && slide.Presentation.CroppedImage(part, embed, fill.SourceRect) is { } cut)
            fill = fill with { Image = cut, SourceRect = (0, 0, 0, 0) };
        return fill;
    }

    /// <summary>Path fill modes of preset geometries that shade the fill (e.g. the top of a cylinder).</summary>
    static Fill Shade(Fill fill, string mode)
    {
        if (mode == "norm")
            return fill;
        RgbColor Modify(RgbColor c) => mode switch
        {
            "darken" => new RgbColor(c.R * 0.6, c.G * 0.6, c.B * 0.6, c.A),
            "darkenLess" => new RgbColor(c.R * 0.8, c.G * 0.8, c.B * 0.8, c.A),
            "lighten" => RgbColor.Mix(c, RgbColor.White.WithAlpha(c.A), 0.4),
            "lightenLess" => RgbColor.Mix(c, RgbColor.White.WithAlpha(c.A), 0.2),
            _ => c,
        };
        // Gradients and patterns are shaded color by color (measured: the lit top of a can with a gradient).
        return fill switch
        {
            SolidFill solid => new SolidFill(Modify(solid.Color)),
            GradientFill gradient => gradient with { Stops = gradient.Stops.Select(s => s with { Color = Modify(s.Color) }).ToList() },
            PatternFill pattern => pattern with { Foreground = Modify(pattern.Foreground), Background = Modify(pattern.Background) },
            _ => fill,
        };
    }

    /// <summary>The bounds of an evaluated path (pixels, shape-local), or null for an empty one.</summary>
    static (double X, double Y, double W, double H)? PathBounds(EvaluatedPath path)
    {
        var points = CompoundLines.Flatten(path.Segments).SelectMany(f => f.Points).ToList();
        if (points.Count == 0)
            return null;
        var (minX, minY) = (points.Min(p => p.X), points.Min(p => p.Y));
        var (maxX, maxY) = (points.Max(p => p.X), points.Max(p => p.Y));
        return maxX - minX > 1e-6 && maxY - minY > 1e-6 ? (minX, minY, maxX - minX, maxY - minY) : null;
    }

    BackgroundItem Background(double width, double height)
    {
        var fill = SlideBackground();
        var defs = new StringBuilder();
        var paintAttributes = paint.Fill(fill, width, height, defs);
        return new BackgroundItem(width, height, fill, defs.ToString(), paintAttributes);
    }

    /// <summary>
    /// The slide's background fill: its own, else the layout's, else the master's; without any the color bg1 maps to
    /// (measured: a layout mapping bg1 to dk1 gives its slides a black background).
    /// </summary>
    Fill SlideBackground()
    {
        var fill = Fill.None as Fill;
        foreach (var (document, part) in new[] { (slide.Slide, slide.SlidePart), (slide.Layout, slide.LayoutPart), (slide.Master, slide.MasterPart) })
        {
            var bg = document?.Root?.Element(Ns.P + "cSld")?.Element(Ns.P + "bg");
            if (bg == null)
                continue;
            if (bg.Element(Ns.P + "bgPr") is { } bgPr && FillReader.FindFill(bgPr) is { } f)
            {
                fill = FillReader.Read(f, slide.Colors, slide.Images(part!), slide.ImageSizes(part!));
                // A background shaded to the title has its path gradient's focus on the title's box (measured:
                // LibreOffice's sd/tdf114848 slide 5, white under the master's title, D1C39F at the slide's edges).
                if (bgPr.Bool("shadeToTitle") == true && fill is GradientFill { Path: not null } gradient && TitleBox() is var (tx, ty, tw, th))
                {
                    var (sw, sh) = ((double)slide.Presentation.SlideWidth, (double)slide.Presentation.SlideHeight);
                    fill = gradient with { FocusRect = (tx / sw, ty / sh, 1 - (tx + tw) / sw, 1 - (ty + th) / sh) };
                }
            }
            else if (bg.Element(Ns.P + "bgRef") is { } bgRef && slide.Theme.FillStyle(bgRef.Int("idx") ?? 0) is { } themed)
                fill = slide.ThemedFill(themed, slide.Colors.WithPlaceholder(slide.Colors.ResolveChild(bgRef)));
            break;
        }
        return fill is NoFill ? new SolidFill(slide.Colors.Resolve(new XElement(Ns.A + "schemeClr", new XAttribute("val", "bg1")))) : fill;
    }

    /// <summary>The box of the slide's title placeholder (EMU), as the slide, its layout or its master place it.</summary>
    (double X, double Y, double W, double H)? TitleBox()
    {
        static bool IsTitle(XElement shape) => Placeholders.Ph(shape) is { } ph && Placeholders.Type(ph) is "title" or "ctrTitle";
        foreach (var (document, part, isSlide) in new[] { (slide.Slide, slide.SlidePart, true), (slide.Layout, slide.LayoutPart, false), (slide.Master, slide.MasterPart, false) })
        {
            if (document?.Root?.Element(Ns.P + "cSld")?.Element(Ns.P + "spTree")?.Elements(Ns.P + "sp").FirstOrDefault(IsTitle) is not { } title)
                continue;
            var chain = Chain(title, part!, isSlide);
            if (chain.Select(s => SpPr(s.Element)?.Element(Ns.A + "xfrm")).FirstOrDefault(x => x?.Element(Ns.A + "ext") != null) is { } xfrm)
            {
                var (x, y, w, h, _, _, _) = ReadTransform(xfrm);
                return (x, y, w, h);
            }
        }
        return null;
    }
}
