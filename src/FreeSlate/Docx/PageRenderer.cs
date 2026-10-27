using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Docx.Layout;
using FreeSlate.Rendering;
using static FreeSlate.Docx.WordDevice;

namespace FreeSlate.Docx;

/// <summary>
/// Draws laid-out pages as SVG (catalog: engine.output): glyphs as outlines in font units placed with &lt;use&gt;,
/// a transparent text layer on top for selection and search, decorations as rectangles.
/// </summary>
public sealed partial class PageRenderer(DocumentContext context, Drawings? drawings = null, TableLayout? blocks = null)
{
    const double PxPerPt = 96.0 / 72.0;

    /// <summary>
    /// Text below this size in pixels goes to the browser as text, larger text is drawn as glyph outlines. Word's
    /// reference is its PDF with the fonts in it, which Windows' renderer draws grid-fitted like the browser draws
    /// text: printed through the same renderer, our text then matched Word's pixel for pixel where its glyphs stand at
    /// Word's positions (docx/generated/text-basic), outlines did not. FS_DOCX_HINTED sets another size for measuring.
    /// </summary>
    static readonly double HintedBelow = double.TryParse(Environment.GetEnvironmentVariable("FS_DOCX_HINTED"), System.Globalization.CultureInfo.InvariantCulture, out var h) ? h : double.MaxValue;

    static string Px(double points) => Units.Css(points * PxPerPt);

    /// <summary>
    /// The settings text boxes and balloons are laid out under: those the pagination ended with (Paginator.Settings),
    /// which a header's or footer's drawings and a box's own direction, color and grid change for themselves (see
    /// BoxScope).
    /// </summary>
    public TextSettings Settings { get; set; } = new();

    /// <summary>
    /// What a drawing's text boxes are laid out under: the settings, and the page whose header or footer the drawing
    /// stands in, for its fields (PageFields.On; null in the body).
    /// </summary>
    readonly record struct BoxScope(TextSettings Settings, PageBox? StoryPage);

    /// <summary>The paragraph layout and run styles of the document (for ruby, two lines in one and emphasis marks).</summary>
    ParagraphLayout? layout => blocks?.Paragraphs;

    /// <summary>The layout of the drawings' text boxes, where the renderer lays out text.</summary>
    readonly TextBoxLayout? boxes = blocks != null ? new TextBoxLayout(context, blocks, drawings) : null;
    RunStyles? styles => layout?.Styles;

    /// <summary>Gradients and filters of the page's text effects, drawn at its end.</summary>
    readonly StringBuilder defs = new();

    /// <param name="paper">The named @page of the page's size, so that printing keeps every page at its size.</param>
    public string Render(PageBox page, string paper) => HtmlScene.Page(Scene(page, paper));

    /// <summary>The page as it is drawn (see PageScene), from back to front.</summary>
    /// <param name="paper">The named @page of the page's size, so that printing keeps every page at its size.</param>
    /// <summary>
    /// A page's scene as Scene sizes it, not drawn (PageScene.Drawn): without the markup pane, which grows with the
    /// page's comments.
    /// </summary>
    public PageScene Sketch(PageBox page, string paper) =>
        context.Markup && MarkupPane ? throw new InvalidOperationException("A page with the markup pane is sized as it is drawn.")
            : new PageScene { Number = page.Number, Paper = paper, Width = page.PaperWidth, Height = page.PaperHeight, Glyphs = context.Glyphs, ExactDashes = true, Drawn = false };

    public PageScene Scene(PageBox page, string paper)
    {
        defs.Clear();
        PageScene scene;
        // With the markup pane the page is as wide as the pane reaches; printed, it is made smaller to fit the paper's
        // width, standing in the middle of its height (as Word prints with markup).
        if (context.Markup && MarkupPane)
        {
            var composed = MarkupWidth(page) * PxPerPt;
            var (paperWidth, paperHeight) = MarkupPaper(page.PaperWidth, page.PaperHeight);
            var scale = Math.Min(paperWidth * PxPerPt / composed, paperHeight / page.PaperHeight);
            scene = new PageScene
            {
                Number = page.Number, Paper = paper, Width = MarkupWidth(page), Height = page.PaperHeight,
                Sheet = (page.PaperWidth, scale, (paperHeight * PxPerPt - page.PaperHeight * PxPerPt * scale) / 2), Glyphs = context.Glyphs,
                ExactDashes = true,
            };
        }
        else
            scene = new PageScene { Number = page.Number, Paper = paper, Width = page.PaperWidth, Height = page.PaperHeight, Glyphs = context.Glyphs, ExactDashes = true };
        paperSize = (page.PaperWidth, page.PaperHeight);
        var items = scene.Items;
        // A page of vertical text is laid out turned a quarter: its body is turned back onto the page, its drawings
        // stand upright where their middles turn to, its header, footer and border stand upright.
        var turned = page.Upright != null;
        var drawn = !turned ? page.Drawings : page.Drawings.Select(d => Upright(d, page.PaperWidth)).ToList();
        if (page.Upright is { } upright)
            drawn.AddRange(upright.Drawings);
        LayBoxes(page, drawn);
        // The page background, drawings behind the text, the text, then inline drawings and the drawings in front.
        var front = new List<PlacedDrawing>();
        // Linked text boxes take their story in the chain's order (wps:txbx, then wps:linkedTxbx by seq), whatever order
        // their layers draw them in: the boxes of a chain on the page are made in that order first (docx/external/
        // libreoffice/extras-layout/tdf152983-1-min: a footer's linked box behind the text shows the picture its
        // story's first box, in front of the text, has no room for).
        var made = new Dictionary<PlacedDrawing, List<SceneItem>>(ReferenceEqualityComparer.Instance);
        if (drawings != null && blocks != null)
        {
            foreach (var drawing in ChainOrder(drawn))
                made[drawing] = Drawing(drawing, page);
        }
        if (drawings != null)
        {
            items.AddRange(drawings.Background(page.PaperWidth, page.PaperHeight));
            foreach (var drawing in drawn.Where(d => d.Behind).OrderBy(d => d.Order))
                items.AddRange(made.Remove(drawing, out var behind) ? behind : Drawing(drawing, page));
        }
        var canvas = new CanvasItem(scene.Width, scene.Height, []);
        var canvasAt = items.Count;
        items.Add(canvas);
        var selection = new List<SceneItem>();
        // A page border behind the text, table shading and borders below the text, a page border in front of it.
        if (page.Border is { Front: false } back)
            Fills(back, canvas.Items);
        var body = canvas.Items;
        if (turned)
        {
            var turn = new TurnedItem(scene.Width, []);
            canvas.Items.Add(turn);
            body = turn.Items;
        }
        // Lines between columns, 0.75 pt wide (measured: 0.72 to 0.84 pt as Word's 600 dpi device rounds them).
        foreach (var (x, top, bottom) in page.ColumnLines)
            body.Add(new RuleItem(x - 0.375, top, 0.75, bottom - top));
        Fills(page.Shading.Concat(page.Borders), page.Strokes, body);
        var svg = new StringBuilder();
        var commentAnchors = CommentRanges(page, svg);
        AddMarkup(body, svg.ToString());
        svg.Clear();
        var inline = new List<PlacedDrawing>();
        // The inline drawings of headers and footers lie behind the body's text, as Word draws their layer first
        // (scratch/triage/2026-10-14_agents/misc: docx/external/openxml-sdk/complex0, 2010: a header's picture
        // 546.75 pt wide over the body's lines).
        var storyInline = new List<PlacedDrawing>();
        Lines(page.Lines.Where(l => l.Story), body, selection, storyInline, onPage: true);
        Lines(page.Lines.Where(l => !l.Story), body, selection, inline, onPage: true);
        if (drawings != null && storyInline.Count > 0)
            items.InsertRange(canvasAt, (!turned ? storyInline : storyInline.Select(d => Upright(d, page.PaperWidth))).SelectMany(d => Drawing(d, page)).ToList());
        front.AddRange(!turned ? inline : inline.Select(d => Upright(d, page.PaperWidth)));
        // Line numbers, right-aligned at their place (catalog: section.lineNumbers).
        foreach (var (number, style, right, baseline) in page.LineNumbers)
        {
            context.UsedFonts.Add(style.Font.Data.Family);
            body.Add(new LabelItem(number, right, baseline, style.Font.Data, style.Size, style.Color, style.Bold, style.Italic));
        }
        Markup(page, commentAnchors, svg, selection, inline);
        body.Add(new MarkupItem(svg.ToString()));
        if (turned)
        {
            body.AddRange(selection);
            selection.Clear();
            Fills(page.Upright!.Shading.Concat(page.Upright.Borders), page.Upright.Strokes, canvas.Items);
            Lines(page.Upright.Lines, canvas.Items, selection, front);
        }
        if (page.Border is { Front: true } border)
            Fills(border, canvas.Items);
        canvas.Items.AddRange(selection);
        if (drawings != null)
        {
            foreach (var drawing in front.Concat(drawn.Where(d => !d.Behind).OrderBy(d => d.Order)))
                items.AddRange(made.Remove(drawing, out var inFront) ? inFront : Drawing(drawing, page));
        }
        foreach (var (text, x, y, w, h) in page.Alternatives)
            items.Add(new AltTextItem(text, x, y, w, h));
        foreach (var (name, x, y) in page.Bookmarks)
            items.Add(new BookmarkItem(name, x, y));
        if (defs.Length > 0)
            items.Add(new WrappedItem("<svg width=\"0\" height=\"0\" style=\"position:absolute\">", [new DefsItem(defs.ToString())], "</svg>"));
        return scene;
    }

    /// <summary>
    /// The text boxes of the chains of linked text boxes that have their first box on the page, in each chain's order:
    /// the box of the story (wps:txbx, place 0), then those that continue it (wps:linkedTxbx by seq).
    /// </summary>
    static IEnumerable<PlacedDrawing> ChainOrder(List<PlacedDrawing> drawn)
    {
        static (string Id, int Seq)? Place(PlacedDrawing drawing)
        {
            var shape = drawing.Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Drawings.Wps + "wsp");
            if ((string?)shape?.Element(Drawings.Wps + "txbx")?.Attribute("id") is { } id)
                return (id, 0);
            if (shape?.Element(Drawings.Wps + "linkedTxbx") is { } linked && (string?)linked.Attribute("id") is { } story)
                return (story, int.TryParse((string?)linked.Attribute("seq"), out var seq) ? seq : 0);
            return null;
        }
        return drawn.Select(d => (Drawing: d, Place: Place(d))).Where(c => c.Place != null)
            .GroupBy(c => (c.Drawing.Part, c.Place!.Value.Id))
            .Where(g => g.Count() > 1 && g.Any(c => c.Place!.Value.Seq == 0))
            .SelectMany(g => g.OrderBy(c => c.Place!.Value.Seq).Select(c => c.Drawing));
    }

    /// <summary>A drawing laid out on a page of vertical text (turned), upright on the page where its middle turns to.</summary>
    static PlacedDrawing Upright(PlacedDrawing drawing, double paperWidth) => drawing with
    {
        X = paperWidth - drawing.Y - drawing.Height / 2 - drawing.Width / 2,
        Y = drawing.X + drawing.Width / 2 - drawing.Height / 2,
        Clip = null,
    };

    /// <summary>A page border's lines, then its art borders' shapes (in points).</summary>
    static void Fills(PageBorderBox border, List<SceneItem> items)
    {
        Fills(border.Fills, border.Strokes, items);
        if (border.Art.Count > 0)
            items.Add(new PathsItem(border.Art.Select(shape => new PathItem(shape.Path, shape.Fill, shape.Stroke, shape.StrokeWidth)).ToList()));
    }

    /// <summary>Fills, then strokes.</summary>
    static void Fills(IEnumerable<FillRect> fills, IEnumerable<Stroke> strokes, List<SceneItem> items)
    {
        foreach (var fill in fills)
            items.Add(new RectItem(fill.X, fill.Y, fill.Width, fill.Height, fill.Color));
        foreach (var s in strokes)
        {
            if (s.Dash is { } pattern && Dashes(s, pattern) is { Count: > 0 } dashes)
                items.Add(new DashesItem(dashes, s.Width, s.Color));
            else
                items.Add(new LineItem(s.X1, s.Y1, s.X2, s.Y2, s.Width, s.Color));
        }
    }

    /// <summary>Fills, then strokes, as SVG (where the content around them is not broken down into items yet).</summary>
    static void Fills(IEnumerable<FillRect> fills, IEnumerable<Stroke> strokes, StringBuilder svg)
    {
        var items = new List<SceneItem>();
        Fills(fills, strokes, items);
        HtmlScene.Write(items, svg);
    }

    /// <summary>
    /// A dashed line's dashes, each a path and a stroke of its own: Word's PDF strokes them with a dash pattern, which
    /// the rasterizer widens to a pixel as any stroke, where the browser's PDF fills every dash as a shape of its own,
    /// lighter (Word's PDF of docx/external/libreoffice/extras-ooxmlexport/fdo64350: a dashed page border of 0.48 pt,
    /// dashes of 1.44 pt and gaps of 0.48 pt); as the subpaths of one path they lose the ink the rasterizer gives the
    /// pixels at their ends (found by agent BG: fdo64350 and page-borders-export-case-2 86.3 -> 100 %).
    /// </summary>
    static List<(double X1, double Y1, double X2, double Y2)> Dashes(Stroke s, string pattern)
    {
        var lengths = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        var length = Math.Sqrt((s.X2 - s.X1) * (s.X2 - s.X1) + (s.Y2 - s.Y1) * (s.Y2 - s.Y1));
        var dashes = new List<(double X1, double Y1, double X2, double Y2)>();
        if (lengths.Length == 0 || lengths.Sum() <= 0 || length <= 0)
            return dashes;
        var (dx, dy) = ((s.X2 - s.X1) / length, (s.Y2 - s.Y1) / length);
        var (at, k) = (0.0, 0);
        while (at < length && dashes.Count < 100_000)
        {
            var end = Math.Min(length, at + lengths[k % lengths.Length]);
            if (k % 2 == 0 && end > at)
                dashes.Add((s.X1 + dx * at, s.Y1 + dy * at, s.X1 + dx * end, s.Y1 + dy * end));
            at = end;
            k++;
        }
        return dashes;
    }

    /// <summary>
    /// A drawing and, over it, its text box's content (catalog: drawing.textBoxes): laid out in the shape's rectangle
    /// less the insets of wps:bodyPr (0.1 in left and right, 0.05 in top and bottom by default), at the top, the
    /// middle or the bottom.
    /// </summary>
    /// <summary>A shaded horizontal rule's frame (Vml.Rule): edges of 0.24 pt, dark above and left, light below and right.</summary>
    static string Bevel(PlacedDrawing rule)
    {
        var (w, h, edge) = (Px(rule.Width), Px(rule.Height), Px(0.24));
        var (right, bottom) = (Px(rule.Width - 0.24), Px(rule.Height - 0.24));
        return $"<div class=\"sp\" style=\"width:{w}px;height:{h}px;transform:matrix(1,0,0,1,{Px(rule.X)},{Px(rule.Y)})\">" +
            $"<svg width=\"{w}\" height=\"{h}\" viewBox=\"0 0 {w} {h}\" overflow=\"visible\">" +
            $"<rect width=\"{edge}\" height=\"{h}\" fill=\"#A0A0A0\"/><rect x=\"{right}\" width=\"{edge}\" height=\"{h}\" fill=\"#E3E3E3\"/>" +
            $"<rect width=\"{w}\" height=\"{edge}\" fill=\"#A0A0A0\"/><rect y=\"{bottom}\" width=\"{w}\" height=\"{edge}\" fill=\"#E3E3E3\"/></svg></div>";
    }

    /// <summary>
    /// A drawing as it is drawn on the page: its shape, then its text boxes (see ShapeOf); <paramref name="outer"/> is
    /// the scope of the text box the drawing stands in, if it does.
    /// </summary>
    List<SceneItem> Drawing(PlacedDrawing drawing, PageBox page, BoxScope? outer = null)
    {
        var scope = outer ?? new BoxScope(Settings, null);
        // The text boxes of headers and footers keep off the body's grid, as their stories do (measured with docx/local/
        // gbx-*: a footer box's line at its top inset and ascent, at its left inset, under pitches of 18 pt of type
        // lines and snapToChars, where the body's box has its lines on the pitch and centered in the grid's cells;
        // docx/external/libreoffice/extras-ooxmlexport/tdf91261).
        if (layout is null || drawing.Part != page.Header && drawing.Part != page.Footer)
            return ShapeOf(drawing, page, scope);
        // Their fields are a header's or footer's, which Word updates when it prints (see FieldEvaluator.Value).
        return ShapeOf(drawing, page, new BoxScope(scope.Settings with { LinePitch = null, CharacterExtra = 0, GridCell = null, SnapCell = null }, page));
    }

    List<SceneItem> ShapeOf(PlacedDrawing drawing, PageBox page, BoxScope scope)
    {
        var shape = drawing.Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Drawings.Wps + "wsp");
        var items = new List<SceneItem>();
        // Its text boxes as laid out before the page was drawn (see LayBoxes).
        var laidOut = Laid(drawing);
        // WordArt: a text box whose body has a text warp bends its text as PowerPoint does, so the slide renderer
        // draws it, in its runs' fonts, fills, outlines and effects, instead of the text box's layout; the warp fills
        // the shape less the body's insets and half its own outline (measured with docx/local/wordartbox: an H in
        // textPlain fills a shape with insets of 0, 7.2 and 3.6 pt or 20 pt exactly to them, whatever the text's outline;
        // wal-*: 1 and 3 pt in from a shape outline of 2 and 6 pt, with or without fill, its theme line's alike;
        // PowerPoint's warp ignores them).
        if (blocks != null && shape != null && drawing.Text is { Count: > 0 } words && Warped(drawing.Body) is { } warped
            && WordArt(words, warped) is { } art)
        {
            var (l, t, r, b) = WarpInsets(warped, shape, context.Theme);
            items.AddRange(drawings!.Render(drawing with { Text = null }));
            items.AddRange(drawings.Render(drawings.WordArtText(art, drawing, l, t, r, b)));
            return items;
        }
        // Fitted to its text before the page was drawn (see LayBoxes), an inline drawing laid out at the page's corner
        // fitted where it stands (see Inline); here where the order went astray.
        if (laidOut?.Fitted is { } fitted)
            drawing = ReferenceEquals(laidOut.Placed, drawing) ? fitted : fitted with { X = drawing.X + fitted.X, Y = drawing.Y + fitted.Y, DeviceShift = drawing.DeviceShift };
        else
        {
            if (blocks != null && drawing.Text is { Count: > 0 } content && drawing.Body is { } bodyPr)
                drawing = boxes!.Fitted(drawing, content, bodyPr, shape, page, scope.Settings);
            if (blocks != null && drawing.ChildText is { Count: > 0 })
                drawing = boxes!.FittedChildren(drawing, page, scope.Settings);
        }
        var drawn = new List<SceneItem>();
        drawn.AddRange(drawings!.Render(drawing));
        if (drawing.Drawing.Attribute(Vml.RuleBevel) != null)
            AddMarkup(drawn, Bevel(drawing));
        if (drawing.Clip is { } clip)
            items.Add(new ClipItem(clip.X, clip.Y, clip.Width, clip.Height, drawn));
        else
            items.AddRange(drawn);
        if (blocks == null)
            return items;
        // A turned shape turns its text with it, unless the text stands upright or the shape is flipped (docx/external/
        // libreoffice/extras-ooxmlexport/TextFrameRotation: a VML text box turned by −90° reads upwards;
        // core-exportdata-ooxml-pass/sdt-in-shape-with-textbox: one turned by 180° and flipped, which mirrors it top
        // to bottom, keeps its page number upright).
        var turn = Turn(shape, drawing.Body);
        if (drawing.Text is { Count: > 0 } || shape?.Element(Drawings.Wps + "linkedTxbx") != null)
            TextBox(laidOut != null ? laidOut.Text : boxes!.Lay(drawing.Text, drawing.Body, shape, drawing.Width, drawing.Height, page, scope.Settings, scope.StoryPage),
                drawing.X, drawing.Y, drawing.Width, drawing.Height, turn, page, scope, items, deviceShift: drawing.DeviceShift);
        // The text boxes of the shapes in a group or canvas, in their shapes' places (and turns).
        // Groups of DrawingML (wpg), not those of VML (docx/external/libreoffice/extras-ooxmlexport/tdf164065: a v:group's
        // text box's table borders lines in Word's PDF).
        var grouped = drawing.Drawing.Attribute(Vml.Source) == null
            && drawing.Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Drawings.Wpg + "wgp") != null;
        var laidChild = 0;
        foreach (var (child, childText) in drawing.ChildText ?? [])
        {
            if (Drawings.ChildRect(drawing.Drawing, child, drawing.X, drawing.Y, drawing.Width, drawing.Height) is not { } rect
                || childText.Count == 0 && child.Element(Drawings.Wps + "linkedTxbx") == null)
                continue;
            // WordArt in a group bends its text in its shape's place.
            if (Warped(child.Element(Drawings.Wps + "bodyPr")) is { } childWarp && WordArt(childText, childWarp) is { } childArt)
            {
                var (l, t, r, b) = WarpInsets(childWarp, child, context.Theme);
                var box = new PlacedDrawing(new XElement(Drawings.Wp + "anchor", new XElement(Ns.A + "graphic", new XElement(Ns.A + "graphicData", new XElement(child)))),
                    drawing.Part, rect.X, rect.Y, rect.W, rect.H) { Behind = drawing.Behind, Order = drawing.Order };
                items.AddRange(drawings.Render(drawings.WordArtText(childArt, box, l, t, r, b)));
                continue;
            }
            TextBox(laidOut != null ? laidOut.Children[laidChild++]
                    : boxes!.Lay(childText, child.Element(Drawings.Wps + "bodyPr"), child, rect.W, rect.H, page, scope.Settings, scope.StoryPage),
                rect.X, rect.Y, rect.W, rect.H, rect.Rotation, page, scope, items, grouped);
        }
        return items;
    }

    /// <summary>
    /// How far a shape turns its text with it: by its turn, unless the text stands upright or the shape is flipped
    /// (see ShapeOf).
    /// </summary>
    static double Turn(XElement? shape, XElement? body)
    {
        var xfrm = shape?.Element(Drawings.Wps + "spPr")?.Element(Ns.A + "xfrm");
        return (long?)xfrm?.Attribute("rot") is { } rot && (string?)body?.Attribute("upright") is not ("1" or "true")
               && (string?)xfrm!.Attribute("flipH") is not ("1" or "true") && (string?)xfrm.Attribute("flipV") is not ("1" or "true")
            ? rot / 60000.0 : 0;
    }

    /// <summary>
    /// A drawing's text boxes as LayBoxes laid them out before the page was drawn: the drawing fitted to its text (not
    /// WordArt, which the slide renderer draws), its own box's text and its group's boxes' texts in their order.
    /// </summary>
    sealed class LaidDrawing(PlacedDrawing placed)
    {
        /// <summary>The drawing as it was laid out: the page's own, an inline one at the page's corner.</summary>
        public PlacedDrawing Placed { get; } = placed;
        public PlacedDrawing? Fitted { get; set; }
        public BoxText? Text { get; set; }
        public List<BoxText?> Children { get; } = [];
    }

    /// <summary>The drawings of the page in hand with their text boxes laid out, in the order ShapeOf takes them.</summary>
    readonly Queue<LaidDrawing> laid = new();

    /// <summary>
    /// The text boxes of a page laid out before it is drawn (catalog: drawing.textBoxes): its drawings fitted to their
    /// text and their boxes' text set (TextBoxLayout), in the order the page's drawing takes them, which linked boxes
    /// take their story in: the drawings of chains first, those behind the text, the inline drawings of headers and
    /// footers, those of the body's and of upright lines, then the drawings in front (see Scene); of each drawing its
    /// own box, the drawings in that box's lines, then its group's boxes (see ShapeOf and TextBox).
    /// </summary>
    /// <param name="drawn">The page's drawings as the page draws them (upright where its text is vertical).</param>
    void LayBoxes(PageBox page, List<PlacedDrawing> drawn)
    {
        laid.Clear();
        if (drawings == null || blocks == null)
            return;
        var made = new HashSet<PlacedDrawing>(ReferenceEqualityComparer.Instance);
        foreach (var drawing in ChainOrder(drawn))
        {
            made.Add(drawing);
            LayDrawing(drawing, page, null);
        }
        foreach (var drawing in drawn.Where(d => d.Behind && !made.Contains(d)).OrderBy(d => d.Order))
            LayDrawing(drawing, page, null);
        foreach (var drawing in Inline(page.Lines.Where(l => l.Story)))
            LayDrawing(drawing, page, null);
        var front = Inline(page.Lines.Where(l => !l.Story));
        if (page.Upright != null)
            front = front.Concat(Inline(page.Upright.Lines));
        foreach (var drawing in front.Concat(drawn.Where(d => !d.Behind && !made.Contains(d)).OrderBy(d => d.Order)))
            LayDrawing(drawing, page, null);
    }

    /// <summary>A drawing's text boxes laid out as ShapeOf draws them (see LayBoxes), under the scope of the box it stands in, if it does.</summary>
    void LayDrawing(PlacedDrawing drawing, PageBox page, BoxScope? outer)
    {
        var shape = drawing.Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Drawings.Wps + "wsp");
        if (!Boxed(drawing, shape))
            return;
        var scope = outer ?? new BoxScope(Settings, null);
        if (layout is not null && (drawing.Part == page.Header || drawing.Part == page.Footer))
            scope = new BoxScope(scope.Settings with { LinePitch = null, CharacterExtra = 0, GridCell = null, SnapCell = null }, page);
        var record = new LaidDrawing(drawing);
        laid.Enqueue(record);
        if (shape != null && drawing.Text is { Count: > 0 } words && Warped(drawing.Body) is { } warped && WordArt(words, warped) != null)
            return;
        if (drawing.Text is { Count: > 0 } content && drawing.Body is { } bodyPr)
            drawing = boxes!.Fitted(drawing, content, bodyPr, shape, page, scope.Settings);
        if (drawing.ChildText is { Count: > 0 })
            drawing = boxes!.FittedChildren(drawing, page, scope.Settings);
        record.Fitted = drawing;
        if (drawing.Text is { Count: > 0 } || shape?.Element(Drawings.Wps + "linkedTxbx") != null)
            record.Text = LayBox(drawing.Text, drawing.Body, shape, drawing.Width, drawing.Height, Turn(shape, drawing.Body), page, scope);
        foreach (var (child, childText) in drawing.ChildText ?? [])
        {
            if (Drawings.ChildRect(drawing.Drawing, child, drawing.X, drawing.Y, drawing.Width, drawing.Height) is not { } rect
                || childText.Count == 0 && child.Element(Drawings.Wps + "linkedTxbx") == null
                || Warped(child.Element(Drawings.Wps + "bodyPr")) is { } childWarp && WordArt(childText, childWarp) != null)
                continue;
            record.Children.Add(LayBox(childText, child.Element(Drawings.Wps + "bodyPr"), child, rect.W, rect.H, rect.Rotation, page, scope));
        }
    }

    /// <summary>
    /// Whether a drawing has text boxes to lay out: text of its own, a linked box's place in a story, or its group's
    /// boxes. Others (3D text among them, which the lines make as they are drawn) have nothing to lay out.
    /// </summary>
    static bool Boxed(PlacedDrawing drawing, XElement? shape) =>
        drawing.Text is { Count: > 0 } || drawing.ChildText is { Count: > 0 } || shape?.Element(Drawings.Wps + "linkedTxbx") != null;

    /// <summary>A box's text laid out, then the drawings in its lines, which an unturned box of horizontal text draws (see TextBox).</summary>
    BoxText? LayBox(List<Block>? text, XElement? body, XElement? shape, double w, double h, double rotation, PageBox page, BoxScope scope)
    {
        var box = boxes!.Lay(text, body, shape, w, h, page, scope.Settings, scope.StoryPage);
        if (box is { Vertical: null } && rotation == 0)
        {
            foreach (var drawing in Inline(box.Lines))
                LayDrawing(drawing, page, scope);
        }
        return box;
    }

    /// <summary>
    /// The inline drawings of lines as Lines draws them, at their sizes (where they stand does not matter to their
    /// boxes' text).
    /// </summary>
    IEnumerable<PlacedDrawing> Inline(IEnumerable<PlacedLine> lines)
    {
        foreach (var line in lines)
        {
            foreach (var piece in line.Line.Pieces)
            {
                if (piece.IsTab || piece.Separator || piece.Ruby != null || piece.Combined != null && layout != null || piece.CheckBox != null
                    || piece.Equation != null || piece.Drawing is not { } inline)
                    continue;
                var (w, h) = Drawings.Extent(inline.Drawing);
                if (piece.Rule is { } rule)
                    w = rule.Width;
                yield return new PlacedDrawing(inline.Drawing, inline.Part, 0, 0, w, h) { Text = inline.Text, ChildText = inline.ChildText };
            }
        }
    }

    /// <summary>
    /// The drawing's text boxes as LayBoxes laid them out, the next in its order; null where the drawing is not that one
    /// (its boxes then laid out as it is drawn, those of the rest of the page too).
    /// </summary>
    LaidDrawing? Laid(PlacedDrawing drawing)
    {
        if (blocks == null || !Boxed(drawing, drawing.Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Drawings.Wps + "wsp")))
            return null;
        if (laid.TryDequeue(out var next) && ReferenceEquals(next.Placed.Drawing, drawing.Drawing))
            return next;
        laid.Clear();
        return null;
    }

    /// <summary>
    /// A WordArt text box's text as a DrawingML text body for the slide renderer (wps:txbx3D, which the drawing's
    /// conversion makes the shape's text body): the box's body properties with its warp, a paragraph per paragraph,
    /// aligned as it is, a run per run in its font, size, bold and italic, underline, fill, outline and effects.
    /// Null where there is no text.
    /// </summary>
    /// <summary>A text box's body if it has a text warp (WordArt), else null.</summary>
    static XElement? Warped(XElement? body) =>
        (string?)body?.Element(Ns.A + "prstTxWarp")?.Attribute("prst") is { } warp && warp != "textNoShape" ? body : null;

    /// <summary>
    /// The insets of a WordArt text box's body (Word's defaults 7.2 and 3.6 pt) and half the shape's own outline (its
    /// a:ln or its theme line's width), in points.
    /// </summary>
    static (double L, double T, double R, double B) WarpInsets(XElement body, XElement? shape, Drawing.Theme? theme)
    {
        double Inset(string name, double fallback) => double.TryParse((string?)body.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v / 12700 : fallback;
        static double? Width(XElement? ln) =>
            double.TryParse((string?)ln?.Attribute("w"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lw) ? lw / 12700 : null;
        var themed = shape?.Element(Drawings.Wps + "style")?.Element(Ns.A + "lnRef") is { } lnRef
            && int.TryParse((string?)lnRef.Attribute("idx"), out var idx) && idx > 0 ? theme?.LineStyle(idx) : null;
        var outline = (Width(shape?.Element(Drawings.Wps + "spPr")?.Element(Ns.A + "ln")) ?? Width(themed) ?? 0) / 2;
        return (Inset("lIns", 7.2) + outline, Inset("tIns", 3.6) + outline, Inset("rIns", 7.2) + outline, Inset("bIns", 3.6) + outline);
    }

    XElement? WordArt(List<Block> text, XElement body)
    {
        var paragraphs = new List<XElement>();
        // Text converted from old WordArt keeps the slide renderer's lines, which its warps were measured with.
        var converted = (string?)body.Attribute("fromWordArt") is "1" or "true";
        foreach (var paragraph in text.OfType<ParagraphBlock>())
        {
            var align = paragraph.Format.Justification switch { "center" => "ctr", "right" or "end" => "r", "both" or "distribute" => "just", _ => "l" };
            var pPr = new XElement(Ns.A + "pPr", new XAttribute("algn", align));
            var p = new XElement(Ns.A + "p", pPr);
            var runs = new List<RunStyle>();
            foreach (var run in paragraph.Content.OfType<TextInline>())
            {
                if (run.Text.Length > 0 && styles!.Style(run.Format, WordSlot.Ascii) is { Hidden: false } style)
                {
                    p.Add(new XElement(Ns.A + "r", WordArtRun(style), new XElement(Ns.A + "t", run.Text)));
                    runs.Add(style);
                }
            }
            if (runs.Count > 0 && !converted)
                pPr.Add(WordArtSpacing(paragraph.Format, runs));
            paragraphs.Add(p);
        }
        if (!paragraphs.Any(p => p.Element(Ns.A + "r") != null))
            return null;
        return new XElement(Drawings.Wps + "txbx3D", new XElement(Ns.A + "bodyPr", body.Attributes(), body.Elements().Select(e => new XElement(e))),
            new XElement(Ns.A + "lstStyle"), paragraphs);
    }

    /// <summary>
    /// A WordArt paragraph's spacing as Word lays its lines out before it bends them: its own line height for the
    /// paragraph's line spacing, given as the share of the slide renderer's single line (1.2 times the largest size),
    /// and its space before and after (measured with docx/local/wap-1: two paragraphs of Arial 20 pt in textPlain
    /// stand about 22.9 pt apart single, 1.15 and 1.5 times that at auto 276 and 360, 30 pt at exactly and at least
    /// 30 pt, and 10 pt more with 10 pt after the first or before the second; docx/external/libreoffice/
    /// tdf125885_WordArt: Courier New Bold 14 pt at 1.15 lines in textDoubleWave1).
    /// </summary>
    static IEnumerable<XElement> WordArtSpacing(ParagraphFormat format, List<RunStyle> runs)
    {
        var natural = runs.Max(r => r.Metrics.Above) + runs.Max(r => r.Metrics.Below);
        var value = format.Line ?? 240;
        var height = (format.LineRule ?? "auto") switch
        {
            "exact" => value / 20,
            "atLeast" => Math.Max(value / 20, natural),
            _ => natural * value / 240,
        };
        yield return new XElement(Ns.A + "lnSpc", new XElement(Ns.A + "spcPct",
            new XAttribute("val", (long)Math.Round(height / (1.2 * runs.Max(r => r.Size)) * 100000))));
        if (format.Before is { } before && before > 0)
            yield return new XElement(Ns.A + "spcBef", new XElement(Ns.A + "spcPts", new XAttribute("val", (long)Math.Round(before * 100))));
        if (format.After is { } after && after > 0)
            yield return new XElement(Ns.A + "spcAft", new XElement(Ns.A + "spcPts", new XAttribute("val", (long)Math.Round(after * 100))));
    }

    static XElement WordArtRun(RunStyle style)
    {
        // Kerned only from the run's own w:kern size on (docx/external/libreoffice/extras-ooxmlexport/fdo80897: WordArt
        // in Calibri Bold set without kerning pairs).
        var rPr = new XElement(Ns.A + "rPr", new XAttribute("lang", "en-US"), new XAttribute("sz", (long)Math.Round(style.Size * 100)),
            new XAttribute("b", style.Bold ? 1 : 0), new XAttribute("i", style.Italic ? 1 : 0),
            new XAttribute("kern", style.KernFrom is { } from ? (long)Math.Round(from * 100) : 400000));
        if (style.Underline is { } underline && underline != "none")
            rPr.Add(new XAttribute("u", underline == "double" ? "dbl" : "sng"));
        if (style.Strike || style.DoubleStrike)
            rPr.Add(new XAttribute("strike", style.DoubleStrike ? "dblStrike" : "sngStrike"));
        if (style.Outline is { } outline)
            rPr.Add(new XElement(outline));
        rPr.Add(style.TextFill is { } fill ? new XElement(fill)
            : new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", style.Color.ToHex().TrimStart('#')))));
        if (style.Effects is { } effects)
            rPr.Add(new XElement(effects));
        var family = style.Font.Data.Family;
        rPr.Add(new XElement(Ns.A + "latin", new XAttribute("typeface", family)), new XElement(Ns.A + "ea", new XAttribute("typeface", family)),
            new XElement(Ns.A + "cs", new XAttribute("typeface", family)));
        return rPr;
    }

    /// <summary>
    /// A shape's text box drawn: its content as TextBoxLayout laid it out in the shape's rectangle less its insets (see
    /// LayBoxes), at the top, middle or bottom, turned with the shape (catalog: drawing.textBoxes, measured with
    /// docx/local/textboxes).
    /// Vertical text turns its lines a quarter: vert runs them down, the first at the right; vert270 runs them up,
    /// the first at the left; mongolianVert runs them down, the first at the left; the insets stay with the shape's
    /// sides.
    /// </summary>
    /// <param name="grouped">Whether the box is a shape's in a group (wpg), whose shading and borders Word's PDF holds as a bitmap.</param>
    /// <param name="deviceShift">How far an inline box moved with its line onto Word's device (PlacedDrawing.DeviceShift).</param>
    void TextBox(BoxText? laid, double x, double y, double w, double h, double rotation, PageBox page, BoxScope scope, List<SceneItem> items,
        bool grouped = false, double deviceShift = 0)
    {
        if (laid == null)
            return;
        var (lines, fills, strokes, offset, vert) = (laid.Lines, laid.Fills, laid.Strokes, laid.Offset, laid.Vertical);
        var (l, t, r, b) = laid.Insets;
        var canvas = new CanvasItem(page.PaperWidth, page.PaperHeight, []);
        var drawn = canvas.Items;
        if (rotation != 0)
        {
            var rotated = new RotatedItem(rotation, x + w / 2, y + h / 2, []);
            canvas.Items.Add(rotated);
            drawn = rotated.Items;
        }
        var selection = new List<SceneItem>();
        // Pictures and shapes in the box's lines, drawn over it (an unturned box's).
        var inner = new List<PlacedDrawing>();
        if (vert == null)
        {
            var (left, top) = (x + l, y + t + offset);
            var placed = fills.Select(f => f with { X = left + f.X, Y = top + f.Y }).ToList();
            // The shading and borders of a box in a group: Word's PDF holds them as a bitmap of 200 dpi with an alpha
            // channel over the box's width between its top and bottom insets, the text over it (measured with
            // docx/local/grr-a to grr-g: tables' borders and cells' shading, paragraphs' borders and shading, a box of
            // 216 by 288 pt with insets of 3.6 pt a bitmap of 599 by 779 pixels from 3.6 pt below its top, also without a
            // fill; a box alone none; docx/external/libreoffice/extras-ooxmlexport/tdf66039: thin borders gray and
            // soft in the reference's raster).
            if (grouped && rotation == 0 && placed.Count > 0 && h - t - b > 0)
            {
                drawn.Add(FillBitmap(placed, x, y + t, w, h - t - b));
                placed.Clear();
            }
            Fills(placed, strokes.Select(s => s.Offset(left, top)), drawn);
            // An unturned box's lines stand on Word's device as the body's do (docx/external/libreoffice/
            // extras-ooxmlexport/tdf103573: Word's text box glyphs on whole dots with the PDF writer's adjustments), an
            // inline box's counted from where it stood before its line moved onto the device, edge and inset rounded
            // together (docx/local/tbi-1417: a box at 1417 twips, 590.4 dots, on 590 has its text with unfilled lines of
            // 1, 1.5 and 2 pt on 595, 597 and 599, not 594, 596 and 598).
            Lines(lines.Select(p => p with { Left = left - deviceShift + p.Left, Top = top + p.Top, BoxInset = l }), drawn, selection, rotation == 0 ? inner : [], onPage: rotation == 0);
        }
        else
        {
            // Each line turned about its own top left corner, set where its turn puts it.
            var turnedLines = lines.Select(p => vert switch
            {
                "vert270" => p with { Left = x + l + offset + p.Top, Top = y + h - b - p.Left, Angle = -90 },
                "mongolianVert" or "wordArtVert" => p with { Line = p.Line.Flipped(), Left = x + l + offset + p.Top + p.Line.Height, Top = y + t + p.Left, Angle = 90 },
                _ => p with { Left = x + w - r - offset - p.Top, Top = y + t + p.Left, Angle = 90 },
            });
            // An unturned box's turned lines stand on Word's device too (see TurnedOnDots).
            var device = rotation == 0 && vert is "vert" or "vert270" && DevicePlaced;
            Lines(device ? turnedLines.Select(p => TurnedOnDots(p, x, w, y, h, t, b, context.Profile.TurnedLinesAsWord2010)) : turnedLines, drawn, selection, [],
                onPage: device);
        }
        drawn.AddRange(selection);
        items.Add(canvas);
        foreach (var drawing in inner)
            items.AddRange(Drawing(drawing, page, scope));
    }

    /// <summary>
    /// Fills drawn in their order into one bitmap of 200 dpi with an alpha channel, stretched over the rectangle (in
    /// points) as Word's PDF holds the shading and borders of a grouped text box (see <see cref="TextBox"/>): each pixel
    /// covered as far as the fill's area covers it.
    /// </summary>
    static BitmapItem FillBitmap(List<FillRect> fills, double x, double y, double w, double h)
    {
        var (width, height) = (Math.Max(1, (int)Math.Round(w * 200 / 72)), Math.Max(1, (int)Math.Round(h * 200 / 72)));
        var (sx, sy) = (width / w, height / h);
        // Premultiplied color and alpha.
        var color = new float[width * height * 4];
        foreach (var fill in fills)
        {
            var (x0, x1) = ((fill.X - x) * sx, (fill.X + fill.Width - x) * sx);
            var (y0, y1) = ((fill.Y - y) * sy, (fill.Y + fill.Height - y) * sy);
            for (var py = Math.Max(0, (int)Math.Floor(y0)); py < Math.Min(height, (int)Math.Ceiling(y1)); py++)
            {
                var rowCover = Math.Min(y1, py + 1) - Math.Max(y0, py);
                for (var px = Math.Max(0, (int)Math.Floor(x0)); px < Math.Min(width, (int)Math.Ceiling(x1)); px++)
                {
                    var cover = (float)((Math.Min(x1, px + 1) - Math.Max(x0, px)) * rowCover * fill.Color.A);
                    if (cover <= 0)
                        continue;
                    var i = (py * width + px) * 4;
                    color[i] = (float)fill.Color.R * cover + color[i] * (1 - cover);
                    color[i + 1] = (float)fill.Color.G * cover + color[i + 1] * (1 - cover);
                    color[i + 2] = (float)fill.Color.B * cover + color[i + 2] * (1 - cover);
                    color[i + 3] = cover + color[i + 3] * (1 - cover);
                }
            }
        }
        var rgba = new byte[color.Length];
        for (var i = 0; i < color.Length; i += 4)
        {
            var alpha = color[i + 3];
            if (alpha <= 0)
                continue;
            for (var c = 0; c < 3; c++)
                rgba[i + c] = (byte)Math.Round(Math.Clamp(color[i + c] / alpha, 0, 1) * 255);
            rgba[i + 3] = (byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255);
        }
        return new BitmapItem(x, y, w, h, width, height, rgba);
    }

    /// <summary>
    /// A turned text box's line (vert, vert270) on Word's device. The box's room stands on whole dots: from Word 2013 on
    /// its top on the dot before it and its height in whole dots from there; before, the edge a turned line starts from
    /// on a dot (the top on the nearest, the bottom for text up on the one before it), the room in whole dots on from
    /// there, and lines down the box stack from its right edge on the dot before it. A line starts at its end of that room,
    /// the insets in whole dots, or its indent from there, aligned lines as on the page (see OnDots) by their width on
    /// the device; its baseline stands on the nearest dot from the room's edge, halves to even; its glyphs follow on
    /// the device and the PDF's growth moves both (measured with docx/local/vtb-cambria, vtb-calibri in mode 15: boxes
    /// at 72.00, 72.05 and 72.07 pt, 385.40 to 385.43 pt high, insets of 3.6 and 7.2 pt, left, centered and right
    /// aligned lines in Cambria 18 and Calibri 11, every start and baseline on Word's dot, right aligned Cambria "Right"
    /// 343 dots wide on the device where its advances make 342; vtc-a14, vtc-f14, vtc-g14 in mode 14: a box 385.4 pt
    /// high at 72 pt counting text up from 3811 dots, text down from 600, a box 57.09 pt wide from 1075 leftwards;
    /// docx/external/libreoffice/extras-ooxmlimport/btlr-frame-vml, extras-ooxmlexport/tbrl-frame-vml, tdf103573,
    /// layout-flow-alt-alone, found by agent BF; extras-ooxmlexport/vml-text-vertical-adjust and extras-ooxmlimport/
    /// groupshape-sdt: a box's top at 221.67 dots counting text down from 222).
    /// </summary>
    static PlacedLine TurnedOnDots(PlacedLine placed, double boxLeft, double boxWidth, double boxTop, double boxHeight, double insetTop,
        double insetBottom, bool legacy)
    {
        if (placed.Line.Pieces.Count == 0)
            return placed;
        var down = placed.Angle > 0;
        var height = Math.Round(boxHeight / Dot, MidpointRounding.AwayFromZero);
        var top = !legacy ? FloorDots(boxTop) : down ? Math.Round(boxTop / Dot, MidpointRounding.AwayFromZero) : FloorDots(boxTop + boxHeight) - height;
        var bottom = top + height;
        // Before Word 2013 lines down the box stack from its right edge on the dot before it, against where the layout
        // put them.
        var edge = down && legacy ? FloorDots(boxLeft + boxWidth) * Dot - (boxLeft + boxWidth) : 0;
        // The room along the line in dots from where the line starts: the top for text down, the bottom for text up.
        var length = height - Math.Round(insetTop / Dot) - Math.Round(insetBottom / Dot);
        var from = down ? top + Math.Round(insetTop / Dot) : bottom - Math.Round(insetBottom / Dot);
        var first = placed.Line.Pieces.Min(p => p.X);
        // Where the first glyph stands along the line from the room's start, in points.
        var along = TwipsOnDots(placed.Line.Pieces[0].X);
        if (placed.Line.Alignment is { } aligned)
        {
            var (left, right) = (TwipsOnDots(aligned.Left), length * Dot - TwipsOnDots(Math.Max(0, length * Dot - aligned.Right)));
            var content = DeviceWidth(placed) ?? Math.Round(aligned.Width / Dot, MidpointRounding.AwayFromZero) * Dot;
            along = aligned.Center ? left + Math.Floor(Math.Round((right - left - content) / Dot) / 2) * Dot : right - content;
        }
        var start = from * Dot + (down ? along : -along);
        var baseline = (down ? placed.Left - placed.Line.Baseline : placed.Left + placed.Line.Baseline) + edge;
        return placed with
        {
            Top = down ? start - first : start + first,
            Left = placed.Left + TwipsOnDots(baseline) - baseline + edge,
            DeviceTurned = true,
        };
    }

    /// <summary>
    /// The width of a line's content on Word's device, from its first glyph to the end of its last one but spaces, in
    /// whole dots, where every piece of it is plain text the device places; else null.
    /// </summary>
    static double? DeviceWidth(PlacedLine placed)
    {
        var pieces = placed.Line.Pieces;
        if (pieces.Count == 0 || pieces.Any(p => p.Text.Length > 0 && PlainGlyphs(p) == null))
            return null;
        var first = pieces.Min(p => p.X);
        if (DevicePositions(placed with { Left = -first }, turned: true) is not { } device)
            return null;
        var (start, end) = (double.MaxValue, double.MinValue);
        foreach (var piece in pieces)
        {
            if (!device.TryGetValue(piece, out var positions))
                continue;
            start = Math.Min(start, piece.X + positions[0]);
            for (var k = piece.Text.Length - 1; k >= 0; k--)
            {
                if (!char.IsWhiteSpace(piece.Text[k]))
                {
                    end = Math.Max(end, piece.X + positions[k + 1]);
                    break;
                }
            }
        }
        return end > start ? Math.Round((end - start) / Dot) * Dot : null;
    }

    /// <param name="onPage">Whether the lines stand on the page itself, where Word starts them on whole dots.</param>
    /// <summary>Lines as SVG (in balloons, which are not broken down into scene items yet).</summary>
    void Lines(IEnumerable<PlacedLine> lines, StringBuilder svg, List<SceneItem> selection, List<PlacedDrawing> front, bool onPage = false)
    {
        var items = new List<SceneItem>();
        Lines(lines, items, selection, front, onPage);
        HtmlScene.Write(items, svg);
    }

    /// <summary>What is drawn as HTML or SVG (see MarkupItem), where it is not empty.</summary>
    static void AddMarkup(List<SceneItem> items, string html)
    {
        if (html.Length > 0)
            items.Add(new MarkupItem(html));
    }

    /// <summary>An item, where there is one.</summary>
    static void AddItem(List<SceneItem> items, SceneItem? item)
    {
        if (item != null)
            items.Add(item);
    }

    /// <summary>The gray of the brackets of editable ranges.</summary>
    static readonly Drawing.RgbColor BracketGray = new(127 / 255.0, 127 / 255.0, 127 / 255.0);

    void Lines(IEnumerable<PlacedLine> lines, List<SceneItem> scene, List<SceneItem> selection, List<PlacedDrawing> front, bool onPage = false)
    {
        foreach (var line in lines)
        {
            var grows = onPage && line.Angle == 0;
            // A turned frame's lines stand on Word's device as a turned text box's (measured with LibreOffice's
            // extras-ooxmlexport/tdf154703_framePrTextDirection: a tbRl frame's line from the right edge's dot before it,
            // its baseline on dot 1692; fdo76979: a btLr header frame's 12 pt line at 528.22 pt).
            var placed = grows ? OnDots(line, paperSize.Width)
                : onPage && line.TurnedFrame is { } frame && DevicePlaced
                    ? TurnedOnDots(line, frame.Left, frame.Width, line.Top + frame.TopOffset, frame.Height, 0, 0, context.Profile.TurnedLinesAsWord2010)
                    : line;
            var turnedBox = onPage && placed.DeviceTurned;
            // A turned line's glyphs on the device along it from its first one, which TurnedOnDots put on a dot: for text
            // up the distance counts from there the other way.
            var down = placed.Angle > 0;
            var first = placed.Line.Pieces.Count > 0 ? placed.Line.Pieces.Min(p => p.X) : 0;
            var device = grows && DevicePlaced ? DevicePositions(placed)
                : turnedBox ? DevicePositions(placed with { Left = down ? placed.Top : placed.Top - 2 * first }, turned: true) : null;
            // Bar tabs: lines 0.75 pt wide from the line's top through its height (one device pixel in Word's raster).
            foreach (var bar in placed.Line.Bars ?? [])
                scene.Add(new RuleItem(placed.Left + bar - 0.375, placed.Top, 0.75, placed.Line.Height + placed.Line.BarExtension));
            var baseline = placed.Top + placed.Line.Baseline;
            if (grows)
                baseline += Grow(baseline, paperSize.Height);
            // A turned line's baseline grows across the page (its x there), its glyphs down or up it (their y).
            if (turnedBox)
                baseline += down ? -Grow(placed.Left - placed.Line.Baseline, paperSize.Width) : Grow(placed.Left + placed.Line.Baseline, paperSize.Width);
            var items = scene;
            if (placed.Angle != 0)
            {
                var rotated = new RotatedItem(placed.Angle, placed.Left + placed.PivotX, placed.Top + placed.PivotY, []);
                scene.Add(rotated);
                items = rotated.Items;
            }
            // Editable ranges (w:permStart, w:permEnd): gray brackets 0.24 pt thin through the line (measured with
            // docx/local/misc-nothing).
            foreach (var (bracketX, start) in placed.Line.Brackets ?? [])
            {
                var (bx, top, bottom) = (placed.Left + bracketX, placed.Top, placed.Top + placed.Line.Height);
                var tick = start ? bx - 0.24 : bx - 0.36;
                items.Add(new RectItem(start ? bx - 0.24 : bx, top + 0.38, 0.24, bottom - top - 0.6, BracketGray));
                items.Add(new RectItem(tick, top + 0.14, 0.6, 0.24, BracketGray));
                items.Add(new RectItem(tick, bottom - 0.22, 0.6, 0.24, BracketGray));
            }
            // Run borders behind the text; underlines and strikethrough end with the line's content.
            RunBorders(placed.Line, placed.Left, baseline, items);
            var contentEnd = placed.Left + ContentEnd(placed.Line, device);
            var places = placed.Line.Paragraph != null && placed.Line.Sources != null ? new Places(placed.Line.Sources) : null;
            foreach (var piece in placed.Line.Pieces)
            {
                var x = placed.Left + piece.X;
                // A run of text moves by where its first glyph stands on the device.
                if (grows)
                    x += Grow(x + (device != null && device.TryGetValue(piece, out var pieceDevice) ? pieceDevice[0] : 0), paperSize.Width);
                else if (turnedBox)
                {
                    var along = piece.X + (device != null && device.TryGetValue(piece, out var turnedDevice) ? turnedDevice[0] : 0);
                    x += down ? Grow(placed.Top + along, paperSize.Height) : -Grow(placed.Top - along, paperSize.Height);
                }
                places?.Add(piece, x, Positions(piece, device));
                // Highlight and shading lie behind the text over the line's natural height, as far as the line's
                // content goes: under tabs, not under the spaces that end the line (measured with docx/local/hlspace:
                // a paragraph of highlighted or shaded spaces shows none, "Word" and four spaces only the word, a
                // tab after it all of it; docx/external/powertools/RA001-Tracked-Revisions-01), with what raised and
                // lowered text adds to it (docx/external/libreoffice/extras-ooxmlexport/tdf140572_docDefault_superscript:
                // text raised 9 pt shaded from 9 pt above the ascent).
                if ((piece.Style.Highlight ?? piece.Style.Shading) is { } back && Math.Min(x + piece.Width, contentEnd) - x is var shown && shown > 0)
                    items.Add(new RectItem(x, baseline - placed.Line.Above - placed.Line.RaisedAbove, shown,
                        placed.Line.Above + placed.Line.Below + placed.Line.RaisedAbove + placed.Line.LoweredBelow, back));
                if (piece.IsTab)
                {
                    if (piece.Leader != null)
                        AddItem(items, LeaderText(piece, placed.Left + piece.X, x - placed.Left - piece.X, baseline - piece.Style.Raise - piece.Shift));
                    continue;
                }
                // A note separator: its font's strikeout stroke, 144 pt long, on whole device dots as strikethrough
                // (measured with docx/generated/notes in Aptos 12 pt, docx/local/fbn-* in Times New Roman 10 pt and
                // docx/external/libreoffice/extras-ooxmlexport/tdf90810short in Calibri 11 pt: its top 4.3, 2.5 and
                // 2.8 pt above the baseline, 0.6, 0.5 and 0.8 pt thick).
                if (piece.Separator)
                {
                    var data = piece.Style.Font.Data;
                    var strikeTop = Dots(data.StrikeoutPosition * piece.Style.Size);
                    var strikeThickness = Math.Max(1, Dots(data.StrikeoutThickness * piece.Style.Size));
                    items.Add(new RuleItem(x, baseline - strikeTop * Dot, piece.Width, strikeThickness * Dot));
                    continue;
                }
                // Ruby: the base text and above it the phonetic text (catalog: run.ruby).
                if (piece.Ruby is { } ruby)
                {
                    Ruby(ruby, x, baseline - piece.Style.Raise, piece.Width, items);
                    continue;
                }
                // Two lines in one: the halves of the text in half size above one another, between brackets.
                if (piece.Combined is { } combinedRun && layout != null)
                {
                    var (firstLine, secondLine, half, _, open, close) = layout.Combination(combinedRun);
                    var full = piece.Style;
                    var at = x;
                    if (open.Length > 0)
                    {
                        AddItem(items, TextAt(open, full, at, baseline));
                        at += ParagraphLayout.Measure(open, full).Sum();
                    }
                    var lineWidth = Math.Max(ParagraphLayout.Measure(firstLine, half).Sum(), ParagraphLayout.Measure(secondLine, half).Sum());
                    // Measured: the first half 6.12 pt above the baseline, the second 1.8 pt below (12 pt text).
                    AddItem(items, TextAt(firstLine, half, at, baseline - 0.51 * full.Size));
                    AddItem(items, TextAt(secondLine, half, at, baseline + 0.15 * full.Size));
                    at += lineWidth;
                    if (close.Length > 0)
                        AddItem(items, TextAt(close, full, at, baseline));
                    continue;
                }
                // A check box form field: a square 2.28 pt smaller than its cell, 1.08 pt in from its left and bottom,
                // crossed when checked, in 0.48 pt lines (measured with docx/local/form-boxes).
                if (piece.CheckBox is { } box)
                {
                    var (_, boxBelow) = WordFonts.LineMetrics(piece.Style.Font.Data, box.Size ?? piece.Style.Size, piece.Style.LineHeights);
                    var side = piece.Width - 2.28;
                    var bottom = baseline + boxBelow - 1.08;
                    var (bx, top) = (x + 1.08, bottom - side);
                    items.Add(new CheckBoxItem(bx, top, side, bottom, box.Checked));
                    continue;
                }
                // An equation: its glyphs and rules, its characters transparent on top for selection.
                if (piece.Equation is { } equation)
                {
                    var (drawn, mathText) = equation.Draw(x, baseline - piece.Style.Raise - piece.Shift, context.Glyphs, asText: true);
                    items.AddRange(drawn);
                    if (mathText.Length > 0)
                        selection.Add(new HiddenSpanItem(mathText, x, baseline - piece.Style.Raise - piece.Shift, piece.Style.Size, equation.Width));
                    continue;
                }
                // An inline drawing's box stands on the baseline, an inline VML shape's hangs from the line's top; its
                // extent lies inside the effect extents. Word 2007 and older (compatibility modes 11 and 12, and
                // documents without a mode) draw a shape's or a group's extent as far again right and down, the line
                // keeping its room (measured with docx/local/effx-l12, effx-t12, effx-l11, effx-grp12, pe-wpsnone: 9 pt
                // of effect extent on the left or on top put a shape 18 pt in, in mode 14 and later 9 pt; pe-12: a
                // picture 9 pt). A frame in an older mode draws them at its corner (PlacedLine.InlineOrigin).
                if (piece.Drawing is { } inline)
                {
                    var (dx, dy) = Drawings.EffectOffset(inline.Drawing);
                    var kind = inline.Drawing.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Elements().FirstOrDefault()?.Name;
                    var shape = kind == Drawings.Wps + "wsp" || kind == Drawings.Wpg + "wgp";
                    if (context.Profile.ShapesShiftedByEffects && shape && inline.Drawing.Attribute(Vml.Converted) == null)
                        (dx, dy) = (2 * dx, 2 * dy);
                    var (w, h) = Drawings.Extent(inline.Drawing);
                    var top = inline.Drawing.Attribute(Vml.Hangs) != null ? placed.HangTop ?? placed.Top : baseline - inline.Box.Height - piece.Lift;
                    if (piece.Rule is { } rule)
                        (w, top) = (rule.Width, baseline + rule.Bottom - h);
                    var (ix, iy) = (shape || inline.Drawing.Attribute(Vml.Hangs) != null) && placed.InlineOrigin is { } origin ? origin : (x, top);
                    // A drawing whose turn swaps its box's sides turns about that box's middle (ri-*: Drawings.Box).
                    if (Drawings.InlineSwapped(inline.Drawing))
                        (ix, iy) = (ix + (h - w) / 2, iy + (w - h) / 2);
                    front.Add(new PlacedDrawing(inline.Drawing, inline.Part, ix + dx, iy + dy, w, h)
                    {
                        // Its text box's text counts from where it stood before its line moved onto the device and the
                        // PDF's growth moved it (TextBox).
                        Text = inline.Text, ChildText = inline.ChildText,
                        DeviceShift = placed.InlineOrigin == null ? placed.DeviceShift + x - (placed.Left + piece.X) : 0,
                    });
                    continue;
                }
                // Text set across the line: a little line of its own, turned a quarter against the line.
                if (piece.Across is { } across && layout != null)
                {
                    var (acrossStyle, length, _, ascent, middle) = layout.Across(across, placed.Line.Vertical);
                    var run = new Piece { Text = across.Text, Style = acrossStyle, Advances = ParagraphLayout.Measure(across.Text, acrossStyle) };
                    var (originX, originY) = (x + ascent, baseline - piece.Style.Raise - piece.Shift - middle + length / 2);
                    var acrossText = new AcrossItem(originX, originY, []);
                    AddItem(acrossText.Items, acrossStyle.Size * PxPerPt < HintedBelow || acrossStyle.Font.Data.IsColor ? BrowserText(run, 0, 0) : Glyphs(run, 0, 0));
                    items.Add(acrossText);
                    continue;
                }
                // East Asian characters turned against the line (see UprightText).
                var turned = placed.Line.TurnEastAsian && piece.Text.Any(ParagraphLayout.EastAsian);
                // Outlined letters (w:outline) too, as Word's PDF draws them: paths, whose thin strokes the rasterizer
                // widens to a pixel, unlike a stroke of text (found by agent BG in Word's PDF of docx/external/libreoffice/
                // extras-ooxmlimport/tdf154370: 34 glyph paths, white, stroked 0.14 pt, 85.0 -> 95.7 %).
                var browser = !turned && !(piece.Style.Hollow && !piece.Style.Font.Data.IsColor)
                    && (piece.Style.Size * PxPerPt < HintedBelow || piece.Style.Font.Data.IsColor);
                // Hyperlinks wrap their text (catalog: field.hyperlinks); bookmarks are anchors in the page.
                var drawnText = items;
                if (piece.Link != null)
                {
                    var link = new LinkItem(piece.Link, []);
                    items.Add(link);
                    drawnText = link.Items;
                }
                // Emboss, imprint and shadow lay copies of the text behind it; emboss and imprint move the text.
                var textBaseline = baseline - piece.Style.Raise - piece.Shift;
                var (copies, moveX, moveY) = Relief(piece, x, textBaseline);
                AddItem(drawnText, copies);
                // 3D text is drawn as PowerPoint draws it, above the page; the text stays selectable.
                var cost = piece.Text.Length * Math.Max(1, piece.Style.Size / 12);
                if (piece.Style.Solid != null && drawings != null && !string.IsNullOrWhiteSpace(piece.Text) && context.Text3DBudget >= cost)
                {
                    context.Text3DBudget -= cost;
                    front.Add(drawings.Text3D(piece.Text, piece.Style, x, textBaseline, piece.Width));
                    AddItem(selection, SelectionText(piece, x, textBaseline, device));
                }
                else if (browser)
                    AddWithEffects(drawnText, piece, BrowserText(piece, x + moveX, textBaseline + moveY, device), x + moveX, textBaseline + moveY);
                else
                {
                    AddItem(drawnText, (turned ? UprightText(piece, x + moveX, textBaseline + moveY, device) : null)
                        ?? (SceneItem?)Glyphs(piece, x + moveX, textBaseline + moveY, turned, device));
                    AddItem(selection, SelectionText(piece, x + moveX, textBaseline + moveY, device));
                }
                Strikes(piece, x, textBaseline, contentEnd, drawnText);
                if (piece.Style.Emphasis is { } emphasis)
                    EmphasisMarks(piece, emphasis, x, baseline - piece.Style.Raise - piece.Shift, device, drawnText);
            }
            // w:ulTrailSpace underlines the spaces that end the line too (docx/external/libreoffice/extras-cjk/
            // UnderlineTrailingSpace: 70 and 80 underlined spaces drawn in mode 15).
            Underlines(placed.Line, placed.Left, baseline, context.Profile.UnderlinesTrailingSpaces ? double.MaxValue : contentEnd, device, items);
            if (places != null)
                items.Add(places.Item(placed.Line, baseline - placed.Line.Baseline, placed.Left));
        }
    }

    /// <summary>The places of a line's characters as its pieces are drawn, gathered into its PlacesItem.</summary>
    sealed class Places(SourceMap sources)
    {
        readonly List<int> offsets = [];
        readonly List<double> starts = [], ends = [];
        readonly List<byte> formats = [];

        /// <summary>A piece's characters at their positions from x (see Positions), right-to-left text from the piece's right end.</summary>
        public void Add(Piece piece, double x, double[] positions)
        {
            if (piece.Start < 0)
                return;
            var width = positions[^1];
            var format = (byte)((piece.Style.Bold ? PlacesItem.Bold : 0) | (piece.Style.Italic ? PlacesItem.Italic : 0)
                                | (piece.Style.Underline is { } underline && underline != "none" ? PlacesItem.Underlined : 0));
            for (var i = 0; i < piece.Text.Length; i++)
            {
                if (piece.Start + i >= sources.Count || piece.AddedHyphen && i == piece.Text.Length - 1 || sources[piece.Start + i] < 0)
                    continue;
                var (start, end) = piece.RightToLeft ? (width - positions[i], width - positions[i + 1]) : (positions[i], positions[i + 1]);
                offsets.Add(sources[piece.Start + i]);
                starts.Add(x + start);
                ends.Add(x + end);
                formats.Add(format);
            }
        }

        /// <summary>The line's places from its top; its paragraph's last line ends with the mark, after the last character or at the line's start.</summary>
        public PlacesItem Item(Line line, double top, double left)
        {
            if (line.Last)
            {
                var at = ends.Count > 0 ? ends[^1] : left;
                offsets.Add(ParagraphText.End(line.Paragraph!));
                starts.Add(at);
                ends.Add(at);
                formats.Add(formats.Count > 0 ? formats[^1] : (byte)0);
            }
            return new PlacesItem(line.Paragraph!.Id, top, top + line.Height, [.. offsets], [.. starts], [.. ends]) { Formats = [.. formats] };
        }
    }

    /// <summary>
    /// The x position (points from the piece's start) of every UTF-16 unit, justification included: where Word's PDF
    /// puts them on the page (<paramref name="device"/>, the places of its line's pieces there; see DevicePositions),
    /// otherwise by their advances.
    /// </summary>
    static double[] Positions(Piece piece, IReadOnlyDictionary<Piece, double[]>? device) =>
        device != null && device.TryGetValue(piece, out var placed) ? placed : NominalPositions(piece);

    /// <summary>Whether glyphs stand where Word's device puts them; FS_DOCX_GLYPHS=nominal keeps their advances, for measuring.</summary>
    static readonly bool DevicePlaced = Environment.GetEnvironmentVariable("FS_DOCX_GLYPHS") != "nominal";

    /// <summary>
    /// Where Word's PDF puts the glyphs of a line on the page, by piece, as positions from the piece's place (see
    /// <see cref="DeviceGlyphs"/>). The line goes on the device in stretches, each from the nearest whole dot, its error
    /// counted from there: at the line's start (on a dot already), after a tab or anything but plain text and where a
    /// piece does not go on where the one before ended; plain text is one glyph per character at the font's advances
    /// (kerned or not, spaced or not), without scaling or synthetic bold, left to right (measured with docx/local/adv-*, 92
    /// to 98 % of the characters within half a dot of Word's PDF, 46 to 59 % before; 103 documents of the corpus,
    /// 67,985 characters: 88.5 %, 72.5 % before; the error counted from the line's exact start instead: 84.0 %). Null
    /// for lines without plain text.
    /// </summary>
    /// <param name="turned">Whether the line is a turned text box's (TurnedOnDots), set on the device as any line.</param>
    static Dictionary<Piece, double[]>? DevicePositions(PlacedLine placed, bool turned = false)
    {
        var line = placed.Line;
        if (line.Vertical && !turned || line.TurnEastAsian || line.Pieces.Count == 0)
            return null;
        var glyphs = new List<(int Natural, double Nominal, double Width)>();
        var stretches = new List<(int Index, int Start, double Error)>();
        var pieces = new List<(Piece Piece, int Index, bool Joined)>();
        var (open, end) = (false, 0.0);
        Piece? previous = null, previousPiece = null;
        foreach (var piece in line.Pieces)
        {
            if (PlainGlyphs(piece) is not { } ids)
            {
                (open, previous) = (false, null);
                continue;
            }
            if (ids.Length == 0)
                continue;
            var data = piece.Style.Font.Data;
            var pixels = PixelSize(piece.Style.Size);
            var nominal = NominalPositions(piece);
            // Word writes runs one after the other as one run of its PDF where they are alike (SameRun).
            pieces.Add((piece, glyphs.Count, previous != null && piece.Link == previous.Link && SameRun(piece.Style, previous.Style)));
            // A note's mark of another size or height than the text beside it starts a stretch of its own, and so does
            // that text; other runs of another size or height go on from where the run before ended on the device
            // (found with agents BG and BH in Word's PDFs: 67 documents of the corpus, 81.9 -> 82.9 % of 389,256
            // characters within half a dot; LibreOffice's extras-ooxmlexport/sdt-2-run, 22 pt after five spaces of
            // 11 pt, 82.2 -> 100 %; extras-ooxmlimport/n782061, "bar" raised 1 pt after "foo", 87.6 -> 100 %;
            // extras-ooxmlexport/fdo79062, the text after an endnote's own superscript mark on the dot nearest to its
            // place; fdo77725, the text after a mark's superscript space not).
            var resync = previousPiece is { } last && (last.Style.NoteMark || piece.Style.NoteMark)
                && (Math.Abs(last.Style.Size - piece.Style.Size) > 1e-6 || Math.Abs(last.Style.Raise - piece.Style.Raise) > 1e-6);
            previousPiece = piece;
            previous = piece;
            // A stretch starts on the dot nearest to its place, halves down (measured with docx/local/gxt-*: text after
            // tab stops of 1440 + 7·k twips at the stop's dot, 1322.5 dots at 1322, 1357.5 at 1357; 58 of 60 exact
            // and the halves so).
            if (!open || resync || Math.Abs(piece.X - end) > 0.01)
            {
                var at = (placed.Left + piece.X) / Dot;
                var start = (int)Math.Ceiling(at - 0.5 - 1e-6);
                stretches.Add((glyphs.Count, start, start - at));
                open = true;
            }
            var spaces = 0;
            for (var i = 0; i < piece.Text.Length; i++)
            {
                var design = data.GlyphAdvance(ids[i]);
                var exact = piece.Advances[i] / Dot;
                // Kerning counts in the natural width as part of the glyph's design width (docx/generated/*, Aptos with
                // w:kern: 77.8 % of the characters within half a dot of Word's PDF; the kerning rounded on its own 64.3 %).
                var spacing = piece.Style.Spacing;
                var kerning = (int)Math.Round((piece.Advances[i] - spacing) * data.UnitsPerEm / piece.Style.Size - design);
                // Character spacing (w:spacing) adds its whole dots (measured with docx/local/gxs-a, gxs-k: 5, 10, 20, -5
                // and -10 twips in Times New Roman, Calibri and Arial, kerned and not, 2, 4, 8, -2 and -4 dots more than
                // the natural width, a dot more or less where the error says).
                var natural = DeviceGlyphs.Natural(design + kerning, data.UnitsPerEm, pixels) + (int)Math.Round(spacing / Dot, MidpointRounding.AwayFromZero);
                // A justified space is as much wider on the device as the line's spaces are, in whole dots; the error
                // decides which of them take a dot more or less (Word's PDF of docx/external/poi/bug57031: spaces of 25
                // dots 33 or 34, 31 or 32 wide, a dot more on one or two of 10 to 18).
                if (piece.SpaceExtra != 0 && piece.Widened(i, ref spaces))
                {
                    natural += (int)Math.Round(piece.SpaceExtra / Dot);
                    exact += piece.SpaceExtra / Dot;
                }
                glyphs.Add((natural, exact, InPoints(Math.Round(1000.0 * design / data.UnitsPerEm) / 1000 * pixels)));
            }
            end = piece.X + nominal[^1];
        }
        if (pieces.Count == 0)
            return null;
        // The device's places of every glyph and of what follows each.
        var places = new int[glyphs.Count];
        var ends = new int[glyphs.Count];
        for (var s = 0; s < stretches.Count; s++)
        {
            var (from, to) = (stretches[s].Index, s + 1 < stretches.Count ? stretches[s + 1].Index : glyphs.Count);
            var natural = new int[to - from];
            var nominal = new double[to - from];
            for (var k = from; k < to; k++)
                (natural[k - from], nominal[k - from]) = (glyphs[k].Natural, glyphs[k].Nominal);
            var stretch = DeviceGlyphs.Place(natural, nominal, stretches[s].Error);
            for (var k = from; k < to; k++)
                (places[k], ends[k]) = (stretches[s].Start + stretch[k - from], stretches[s].Start + stretch[k - from + 1]);
        }
        // Each run of Word's PDF has its first glyph where the device put it, the others as its writer encodes the
        // device's advances.
        var result = new Dictionary<Piece, double[]>();
        for (var p = 0; p < pieces.Count;)
        {
            var q = p + 1;
            while (q < pieces.Count && pieces[q].Joined)
                q++;
            var (from, to) = (pieces[p].Index, pieces[q - 1].Index + pieces[q - 1].Piece.Text.Length);
            var advances = new int[to - from - 1];
            var widths = new double[to - from - 1];
            for (var k = from; k < to - 1; k++)
                (advances[k - from], widths[k - from]) = (places[k + 1] - places[k], glyphs[k].Width);
            var encoded = DeviceGlyphs.Encode(advances, widths, InPoints(PixelSize(pieces[p].Piece.Style.Size)));
            for (var r = p; r < q; r++)
            {
                var (piece, index, _) = pieces[r];
                var count = piece.Text.Length;
                var positions = new double[count + 1];
                for (var k = 0; k < count; k++)
                    positions[k] = (places[from] + encoded[index - from + k]) * Dot - (placed.Left + piece.X);
                positions[count] = positions[count - 1] + (ends[index + count - 1] - places[index + count - 1]) * Dot;
                result[piece] = positions;
            }
            p = q;
        }
        return result;
    }

    /// <summary>
    /// Whether Word holds two runs as one: the same w:rPr, w:rsidRPr and w:rsidR (of 103 documents of the corpus, 88.5 %
    /// of the characters within half a dot of Word's PDF; 88.1 % with every w:r a run of its own, 85.6 % with runs of the
    /// same w:rPr as one, 86.0 % with the same w:rPr and w:rsidRPr).
    /// </summary>
    static bool SameRun(RunStyle a, RunStyle b) =>
        a.Source is { } x && b.Source == x && a.Features.SequenceEqual(b.Features) && a with { Features = b.Features } == b;

    /// <summary>
    /// The glyphs of a piece of plain text (see <see cref="DevicePositions"/>): one per character at the font's advance,
    /// kerned or not, spaced or not; none for a piece without text, null for tabs, objects and text that is not plain.
    /// </summary>
    static uint[]? PlainGlyphs(Piece piece)
    {
        if (piece.IsTab || piece.Drawing != null || piece.Equation != null || piece.CheckBox != null || piece.Ruby != null || piece.Combined != null
            || piece.Across != null || piece.Separator || piece.Rule != null || piece.RightToLeft || piece.Lead != 0)
            return null;
        if (piece.Text.Length == 0)
            return [];
        var style = piece.Style;
        var data = style.Font.Data;
        if (data.IsColor || Math.Abs(style.Scale - 1) > 1e-6 || style.Size <= 0)
            return null;
        var glyphs = data.GlyphsWithFeatures(piece.Text, style.Features, language: style.Language);
        if (glyphs.Length != piece.Text.Length)
            return null;
        var ids = new uint[glyphs.Length];
        for (var i = 0; i < glyphs.Length; i++)
        {
            var glyph = glyphs[i];
            // Kern table pairs before Word 2010 (RunStyle.LegacyKerning) count as kerning; the last glyph's pair may be
            // with the next piece's or line's first (RunStyle.ShapesWith).
            var pair = style.LegacyKerning && i + 1 < glyphs.Length ? data.LegacyKerning(glyph.Id, glyphs[i + 1].Id) * style.Size : 0;
            var kerned = (style.LegacyKerning || style.Features.Contains("kern")) && i == glyphs.Length - 1;
            if (glyph.Cluster != i || glyph.Id == 0 || char.IsSurrogate(piece.Text[i]) || Math.Abs(glyph.OffsetX) > 1e-9
                || !kerned && Math.Abs(piece.Advances[i] - glyph.Advance * style.Size - style.Spacing - pair) > 1e-4 || style.Font.SyntheticBold)
                return null;
            ids[i] = glyph.Id;
        }
        return ids;
    }

    /// <summary>The x position (points from the piece's start) of every UTF-16 unit by their advances, justification included.</summary>
    static double[] NominalPositions(Piece piece)
    {
        var positions = new double[piece.Text.Length + 1];
        var x = 0.0;
        var spaces = 0;
        for (var i = 0; i < piece.Text.Length; i++)
        {
            positions[i] = x;
            x += piece.Advances[i];
            if (piece.Widened(i, ref spaces))
                x += piece.SpaceExtra;
        }
        positions[^1] = x;
        // A run border's room before the first character is in its advance.
        if (piece.Text.Length > 0)
            positions[0] = piece.Lead;
        return positions;
    }

    /// <param name="turnEastAsian">
    /// Whether the piece's East Asian characters are turned a quarter counterclockwise against the line, each about the
    /// middle of its em box (the font's, see <see cref="FontData.EmMiddle"/>), in their vertical forms (OpenType vert):
    /// upright in vertical lines (measured with docx/local/vertical, vertical2: Yu Mincho, MS Mincho, Meiryo).
    /// </param>
    /// <param name="device">The places of the line's pieces on Word's device (see Positions), null for a piece not on the page.</param>
    GlyphsItem? Glyphs(Piece piece, double x, double baseline, bool turnEastAsian = false, IReadOnlyDictionary<Piece, double[]>? device = null)
    {
        var style = piece.Style;
        var data = style.Font.Data;
        if (data.IsColor)
            return null;
        var glyphs = data.GlyphsWithFeatures(piece.Text, turnEastAsian ? [.. style.Features, "vert"] : style.Features, language: style.Language);
        var positions = Positions(piece, device);
        var scale = style.Size * PxPerPt / data.UnitsPerEm;
        var drawn = new List<GlyphItem>();
        // Glyphs of one cluster follow each other from the cluster's position.
        var clusterStart = -1;
        var inCluster = 0.0;
        foreach (var g in glyphs)
        {
            if (g.Cluster != clusterStart)
            {
                clusterStart = g.Cluster;
                inCluster = 0;
            }
            var gx = (positions[Math.Min(g.Cluster, piece.Text.Length)] + inCluster) * PxPerPt / scale + g.OffsetX * data.UnitsPerEm * style.Scale;
            inCluster += g.Advance * style.Size * style.Scale;
            if (context.Glyphs.Id(data, g.Id) is not { } id)
                continue;
            double? middle = turnEastAsian && g.Cluster < piece.Text.Length && ParagraphLayout.EastAsian(piece.Text[g.Cluster])
                ? (positions[g.Cluster] + Math.Max(piece.Advances[g.Cluster], g.Advance * style.Size * style.Scale) / 2) * PxPerPt / scale : null;
            drawn.Add(new GlyphItem(g.Id, id, gx, -g.OffsetY * data.UnitsPerEm, middle));
        }
        if (drawn.Count == 0)
            return null;
        return new GlyphsItem(data, style.Size, x, baseline, drawn, style.Color)
        {
            Scale = style.Scale, Hollow = style.Hollow, Shadow = style.Shadow, SyntheticBold = style.Font.SyntheticBold, SyntheticItalic = style.Font.SyntheticItalic,
        };
    }

    /// <summary>
    /// A piece of a vertical line whose East Asian characters stand upright, drawn as text: each of those characters a
    /// text of its own in its vertical form (OpenType vert), turned a quarter counterclockwise about the middle of its em
    /// box, half its advance from its place, the font's own where the line squeezed it (a squeezed mark keeps its whole
    /// em, the next character moving up); the other characters text along the line. It is not selectable: the selection
    /// layer is. Word's PDF writes such text as text, which renderers hint, where outlines are not (LibreOffice's
    /// extras-ooxmlexport/tdf142407: Yu Mincho 10.5 pt in a tbRl section, 75.3 → 95.6 %; the squeezed 。 and 、 of
    /// its lines 1.4 pt lower). Null where the text is not plain (outlined, filled, scaled, slanted), which
    /// <see cref="Glyphs"/> draws from its outlines.
    /// </summary>
    SvgGroupItem? UprightText(Piece piece, double x, double baseline, IReadOnlyDictionary<Piece, double[]>? device)
    {
        var style = piece.Style;
        var data = style.Font.Data;
        if (data.IsColor || style.Hollow || style.TextFill != null || style.Outline != null || style.Font.SyntheticItalic || Math.Abs(style.Scale - 1) > 1e-6)
            return null;
        var positions = Positions(piece, device);
        var vertical = data.GlyphsWithFeatures(piece.Text, [.. style.Features, "vert"], language: style.Language);
        context.UsedFonts.Add(data.Family);
        TextItem Text(int from, int to, IReadOnlyList<string> features) =>
            new(piece.Text[from..to], x + positions[from], [.. Enumerable.Range(from, to - from + 1).Select(i => positions[i] - positions[from])],
                baseline, data, style.Size, style.Color)
            {
                Bold = style.Bold, StrokedBold = style.Bold && style.Font.SyntheticBold, Italic = style.Italic, Features = features,
                Language = style.Language, Width = positions[to] - positions[from],
            };
        var items = new List<SceneItem>();
        var along = -1;
        for (var i = 0; i <= piece.Text.Length; i++)
        {
            var upright = i < piece.Text.Length && ParagraphLayout.EastAsian(piece.Text[i]);
            if (along >= 0 && (upright || i == piece.Text.Length))
            {
                if (!string.IsNullOrWhiteSpace(piece.Text[along..i]))
                    items.Add(Text(along, i, style.Features));
                along = -1;
            }
            if (i == piece.Text.Length)
                break;
            if (!upright)
            {
                if (along < 0)
                    along = i;
                continue;
            }
            var own = vertical.Where(g => g.Cluster == i).Sum(g => g.Advance) * style.Size;
            var middle = x + positions[i] + Math.Max(positions[i + 1] - positions[i], own) / 2;
            items.Add(new RotatedItem(-90, middle, baseline - data.EmMiddle * style.Size, [Text(i, i + 1, [.. style.Features, "vert"])]));
        }
        return items.Count == 0 ? null : new SvgGroupItem(" aria-hidden=\"true\" style=\"user-select:none\"", items);
    }

    /// <summary>The paper size of the page being drawn, over which Word's PDF moves text (see <see cref="Grow"/>).</summary>
    (double Width, double Height) paperSize;

    /// <summary>
    /// How far Word's PDF moves a run of text right of or below a place of its 600 dpi device, in points: 0.945 dots
    /// (0.04 mm) more across the paper's whole width or height, in the 0.2 dot steps of its PDF's 1/3000 in grid, the
    /// glyphs' widths as they are (measured with docx/local/lsg7-a4, lsg8-a4-*, lsg9-*: line starts 0.2 dots further
    /// right from 491.5 dots on and every 1050 dots after on A4, every 740 on A5 and 1079 on Letter; lsgy-a4: baselines
    /// alike down the page).
    /// </summary>
    static double Grow(double at, double extent) => extent > 0 ? 0.2 * Dot * Math.Round(4.725 * at / extent + 0.032) : 0;

    /// <summary>
    /// A line of the page moved to start on a whole dot of Word's device: the column's or cell's edge and the line's
    /// indent each rounded to one, halves to even (measured with docx/local/lsg-*, lsg2-*: margins of 1120 to 1160
    /// twips start where 467.5 dots goes to 468 and 472.5 to 472, indents of 0 to 24 twips add whole dots).
    /// </summary>
    static PlacedLine OnDots(PlacedLine placed, double pageWidth)
    {
        var start = placed.Line.Pieces.Count > 0 ? placed.Line.Pieces[0].X : 0;
        var shift = (placed.BoxInset is { } inset ? BoxTextOnDots(placed.Left, inset) : TwipsOnDots(placed.Left)) - placed.Left + TwipsOnDots(start) - start;
        // Centered and right aligned lines: the room's edges each on a dot (the left one as lines start, the right one
        // where it stands on the page), the content's width in whole dots, half the room left over rounded down
        // (measured with docx/local/jco-a to jco-d: Arial and Calibri 11 pt, margins of 1440 and 1134 twips, indents of
        // 7, 39 of 40 centered and 35 of 40 right aligned lines on Word's dot; docx/external/libreoffice/
        // extras-ooxmlexport/tdf106174_rtlParaAlign: margins of 750 twips on a page 8391 wide, the right edge at
        // 3183.75 dots on 3184).
        // Lines of right-to-left paragraphs stand on the device's grid mirrored about the page's width in whole dots, up
        // (measured by agent AQ in Word's PDFs: tdf106174_rtlParaAlign, a page of 3496.25 dots mirrored about 3497, the
        // right margin of 312.5 dots putting start aligned text's right end at 3185; fdo72560, fdo72560b, fdo72560c: end
        // aligned text's left end at 473 and 474): start aligned (and justified) lines end at the room's right edge
        // counted from the page's right edge and floored, end aligned lines begin at its left edge so counted, centered
        // lines take half the room left over floored from the room's right edge so counted.
        if (placed.Line.Mirrored is { } mirrored && placed.Line.Pieces.Count > 0)
        {
            var width = pageWidth / Dot;
            var axis = Math.Ceiling(width - 1e-6);
            var (roomLeft, roomRight) = ((placed.Left + mirrored.Left) / Dot, (placed.Left + mirrored.Right) / Dot);
            var (target, current) = mirrored.Justification switch
            {
                "right" or "end" => (axis - Math.Floor(width - roomLeft + 1e-6), roomLeft),
                "center" => (axis - Math.Floor(width - roomRight + 1e-6) - Math.Floor((roomRight - roomLeft - mirrored.Width / Dot) / 2 + 1e-6),
                    roomRight - (roomRight - roomLeft - mirrored.Width / Dot) / 2),
                _ => (axis - Math.Floor(width - roomRight + 1e-6), roomRight),
            };
            shift = (target - current) * Dot;
        }
        else if (placed.Line.Alignment is { } aligned)
        {
            var left = TwipsOnDots(placed.Left) + TwipsOnDots(aligned.Left);
            var right = TwipsOnDots(placed.Left + aligned.Right);
            var width = Math.Round(aligned.Width / Dot, MidpointRounding.AwayFromZero) * Dot;
            var to = aligned.Center ? left + Math.Floor(Math.Round((right - left - width) / Dot) / 2) * Dot : right - width;
            // The line's left end, also where right-to-left runs were put in visual order after the first piece.
            shift = to - (placed.Left + placed.Line.Pieces.Min(p => p.X));
        }
        return Math.Abs(shift) < 1e-9 ? placed : placed with { Left = placed.Left + shift, DeviceShift = shift };
    }

    /// <summary>Where a line's content ends (from the column's left edge): its last character but spaces, or tab.</summary>
    static double ContentEnd(Line line, IReadOnlyDictionary<Piece, double[]>? device)
    {
        var end = double.NegativeInfinity;
        foreach (var piece in line.Pieces)
        {
            if (piece.IsTab)
            {
                if (piece.Start >= 0)
                    end = Math.Max(end, piece.X + piece.Width);
                continue;
            }
            var kept = piece.Text.TrimEnd(' ').Length;
            if (kept > 0)
                end = Math.Max(end, piece.X + (kept == piece.Text.Length ? piece.Width : Positions(piece, device)[kept]));
        }
        return end;
    }

    /// <summary>
    /// Strikethrough (catalog: run.decoration) through a piece at its own baseline and size, as Word prints it
    /// (measured with docx/local/ulsizes: Arial, Times New Roman, Calibri and Courier New from 8 to 72 pt, and
    /// docx/local/ulmixed: superscripts and raised text): a line from the font's strikeout position down, as thick as
    /// its strikeout size, both on whole device dots; double strikethrough is that line moved up and down by its
    /// thickness. Spaces at the end of the line are not struck through.
    /// </summary>
    static void Strikes(Piece piece, double x, double baseline, double contentEnd, List<SceneItem> items)
    {
        var style = piece.Style;
        if (!style.Strike && !style.DoubleStrike || piece.IsTab)
            return;
        var width = Math.Min(piece.Width, contentEnd - x);
        if (width <= 0)
            return;
        var data = style.Font.Data;
        var top = Dots(data.StrikeoutPosition * style.Size);
        var thickness = Math.Max(1, Dots(data.StrikeoutThickness * style.Size));
        void Line(int from) => items.Add(new RectItem(x, baseline + from * Dot, width, thickness * Dot, style.Color));
        if (style.DoubleStrike)
        {
            Line(-top - thickness);
            Line(-top + thickness);
        }
        else
            Line(-top);
    }

    /// <summary>
    /// A font's underlines at a size, in device dots below the baseline, as Word prints them (measured with
    /// docx/local/ulsizes: Arial, Times New Roman, Calibri and Courier New from 8 to 72 pt): a single line from the
    /// font's underline position, as thick as its underline thickness; double lines half as thick (rounded up), the
    /// first from half the position (rounded up), the second ending with the single line or 8 dots below the first,
    /// whichever is lower; a thick line from the first double line's top to the second's; a wave from the first
    /// double line's top, half the double lines' band less 3 dots high (rounded up). Small sizes of Arial and Courier
    /// New stand up to 3 dots higher in Word.
    /// </summary>
    readonly record struct UnderlineMetrics(int Top, int Thickness, int DoubleTop, int DoubleThickness, int Gap, int Heavy, int Wave)
    {
        public static UnderlineMetrics Of(FreeSlate.Text.FontData data, double size)
        {
            var top = Dots(data.UnderlinePosition * size);
            var thickness = Math.Max(1, Dots(data.UnderlineThickness * size));
            var doubleTop = (int)Math.Ceiling(top / 2.0);
            var doubleThickness = (thickness + 1) / 2;
            var bottom = Math.Max(top + thickness, doubleTop + 2 * doubleThickness + 8);
            return new(top, thickness, doubleTop, doubleThickness, bottom - doubleTop - 2 * doubleThickness,
                bottom - doubleThickness - doubleTop, (int)Math.Ceiling((bottom - doubleTop - 3) / 2.0));
        }
    }

    /// <summary>The dashes and gaps of a dotted or dashed underline in points, whatever the text's size (measured).</summary>
    static double[]? DashPattern(string kind) => kind switch
    {
        "dotted" or "dottedHeavy" => [1.44, 1.44],
        "dash" or "dashedHeavy" => [3.96, 1.92],
        "dashLong" or "dashLongHeavy" => [7.92, 3.96],
        "dotDash" or "dashDotHeavy" => [3.0, 0.96, 0.96, 0.96],
        "dotDotDash" or "dashDotDotHeavy" => [2.16, 0.72, 0.72, 0.72, 0.72, 0.72],
        _ => null,
    };

    /// <summary>Whether a piece carries its underline: text and tabs, not objects or the spacers past floating objects.</summary>
    static bool Underlined(Piece piece) =>
        piece.Style.Underline != null && piece.Width > 0 && (!piece.IsTab || piece.Start >= 0) && piece.Drawing == null
        && piece.Equation == null && piece.CheckBox == null && piece.Ruby == null && piece.Combined == null && piece.Across == null && !piece.Separator;

    /// <summary>
    /// The underlines of a line (catalog: run.decoration), as Word prints them (measured with docx/local/underlines,
    /// ulsizes and ulmixed): neighboring pieces with the same underline share one, its color changing with theirs.
    /// </summary>
    void Underlines(Line line, double left, double baseline, double contentEnd, IReadOnlyDictionary<Piece, double[]>? device, List<SceneItem> items)
    {
        var pieces = line.Pieces;
        for (var i = 0; i < pieces.Count; i++)
        {
            if (!Underlined(pieces[i]))
                continue;
            var kind = pieces[i].Style.Underline!;
            var j = i;
            while (j + 1 < pieces.Count && pieces[j + 1].Style.Underline == kind && Underlined(pieces[j + 1])
                   && Math.Abs(pieces[j + 1].X - pieces[j].X - pieces[j].Width) < 0.01)
                j++;
            Underline(pieces.GetRange(i, j - i + 1), kind, left, baseline, contentEnd, device, items);
            i = j;
        }
    }

    /// <summary>
    /// One underline under neighboring pieces: at the lowest of their baselines (superscripts and raised text with
    /// others underline at the others' baseline, alone at their own), its lines the width-weighted average of the
    /// lines of the pieces on that baseline (docx/local/ulmixed: 12 and 36 pt, Arial with Times New Roman and Courier
    /// New, within a dot), to the end of the line's content. Dots and dashes have fixed lengths from the underline's
    /// start; waves are 2.4 pt long, their crests on the page's 2.4 pt grid. "words" leaves out spaces and tabs.
    /// </summary>
    void Underline(List<Piece> segment, string kind, double left, double lineBaseline, double contentEnd, IReadOnlyDictionary<Piece, double[]>? device,
        List<SceneItem> items)
    {
        double BaselineOf(Piece p) => lineBaseline - p.Style.Raise - p.Shift;
        var baseline = segment.Max(BaselineOf);
        var on = segment.Where(p => BaselineOf(p) >= baseline - 0.01).ToList();
        var metrics = on.Select(p => (Metrics: UnderlineMetrics.Of(p.Style.Font.Data, p.Style.Size), p.Width)).ToList();
        var total = metrics.Sum(m => m.Width);
        int Average(Func<UnderlineMetrics, int> value) =>
            (int)Math.Round(metrics.Sum(m => value(m.Metrics) * m.Width) / total, MidpointRounding.AwayFromZero);
        var (top, thickness) = (Average(m => m.Top), Average(m => m.Thickness));
        var (doubleTop, doubleThickness, gap) = (Average(m => m.DoubleTop), Average(m => m.DoubleThickness), Average(m => m.Gap));
        var (heavy, wave) = (Average(m => m.Heavy), Average(m => m.Wave));

        // The stretches to draw, in their colors (one where they are written the same).
        var origin = left + segment[0].X;
        var ranges = new List<(double From, double To, string Hex, FreeSlate.Drawing.RgbColor Color)>();
        void Add(double from, double to, FreeSlate.Drawing.RgbColor color)
        {
            to = Math.Min(to, contentEnd);
            if (to - from < 1e-6)
                return;
            var hex = color.ToHex();
            if (ranges.Count > 0 && ranges[^1].Hex == hex && Math.Abs(ranges[^1].To - from) < 0.01)
                ranges[^1] = (ranges[^1].From, to, hex, color);
            else
                ranges.Add((from, to, hex, color));
        }
        foreach (var piece in segment)
        {
            var color = piece.Style.UnderlineColor ?? piece.Style.Color;
            var x = left + piece.X;
            if (kind != "words")
            {
                Add(x, x + piece.Width, color);
                continue;
            }
            if (piece.IsTab)
                continue;
            var positions = Positions(piece, device);
            for (var k = 0; k < piece.Text.Length; k++)
            {
                if (char.IsWhiteSpace(piece.Text[k]))
                    continue;
                var end = k;
                while (end + 1 < piece.Text.Length && !char.IsWhiteSpace(piece.Text[end + 1]))
                    end++;
                Add(x + positions[k], x + positions[end + 1], color);
                k = end;
            }
        }

        void Rect(double from, double to, int at, int height, FreeSlate.Drawing.RgbColor color) =>
            items.Add(new RectItem(from, baseline + at * Dot, to - from, height * Dot, color));
        var heavyBand = kind is "dottedHeavy" or "dashedHeavy" or "dashLongHeavy" or "dashDotHeavy" or "dashDotDotHeavy";
        foreach (var (from, to, _, color) in ranges)
        {
            switch (kind)
            {
                case "double":
                    Rect(from, to, doubleTop, doubleThickness, color);
                    Rect(from, to, doubleTop + doubleThickness + gap, doubleThickness, color);
                    break;
                case "thick":
                    Rect(from, to, doubleTop, heavy, color);
                    break;
                case "wave" or "wavyHeavy":
                    AddItem(items, Wave(from, to, baseline + doubleTop * Dot, baseline + (doubleTop + wave) * Dot, kind == "wave" ? 0.24 : 0.96, color));
                    break;
                case "wavyDouble":
                {
                    var height = (int)Math.Ceiling((wave - 3) / 2.0);
                    AddItem(items, Wave(from, to, baseline + doubleTop * Dot, baseline + (doubleTop + height) * Dot, 0.24, color));
                    AddItem(items, Wave(from, to, baseline + (doubleTop + wave - height) * Dot, baseline + (doubleTop + wave) * Dot, 0.24, color));
                    break;
                }
                default:
                    var (at, height2) = heavyBand ? (doubleTop, heavy) : (top, thickness);
                    if (DashPattern(kind) is { } pattern)
                    {
                        // Dashes from the underline's start, cut at the stretch's ends.
                        var period = pattern.Sum();
                        for (var start = origin + Math.Floor((from - origin) / period + 1e-9) * period; start < to; start += period)
                        {
                            var dash = start;
                            for (var k = 0; k < pattern.Length; k++)
                            {
                                var (a, b) = (Math.Max(dash, from), Math.Min(dash + pattern[k], to));
                                if (k % 2 == 0 && b - a > 1e-6)
                                    Rect(a, b, at, height2, color);
                                dash += pattern[k];
                            }
                        }
                    }
                    else
                        Rect(from, to, at, height2, color);
                    break;
            }
        }
    }

    /// <summary>
    /// A wave from one x to another between two heights, as Word prints it (measured): half waves 1.2 pt long, each an
    /// S-curve with level ends, the crests (at <paramref name="top"/>) on the page's 2.4 pt grid.
    /// </summary>
    static CurveItem? Wave(double from, double to, double top, double bottom, double width, FreeSlate.Drawing.RgbColor color)
    {
        const double half = 1.2;
        // The share t of a half wave's curve where it has come u of its length (x = 1.5 t - 1.5 t² + t³, rising).
        static double At(double u)
        {
            double lo = 0, hi = 1;
            for (var k = 0; k < 40; k++)
            {
                var t = (lo + hi) / 2;
                if (1.5 * t - 1.5 * t * t + t * t * t < u) lo = t; else hi = t;
            }
            return (lo + hi) / 2;
        }
        static (double X, double Y) Lerp((double X, double Y) a, (double X, double Y) b, double t) => (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        (double X, double Y)? start = null;
        var curves = new List<((double X, double Y) First, (double X, double Y) Second, (double X, double Y) End)>();
        for (var n = (long)Math.Floor(from / half); n * half < to - 1e-9; n++)
        {
            var a = n * half;
            var (y0, y1) = n % 2 == 0 ? (top, bottom) : (bottom, top);
            (double X, double Y) p0 = (a, y0), p1 = (a + half / 2, y0), p2 = (a + half / 2, y1), p3 = (a + half, y1);
            var t1 = to < a + half ? At((to - a) / half) : 1;
            var t0 = from > a ? At((from - a) / half) : 0;
            // The curve up to t1, then its part from t0 on (de Casteljau).
            if (t1 < 1)
            {
                var (q01, q12, q23) = (Lerp(p0, p1, t1), Lerp(p1, p2, t1), Lerp(p2, p3, t1));
                var (r0, r1) = (Lerp(q01, q12, t1), Lerp(q12, q23, t1));
                (p1, p2, p3) = (q01, r0, Lerp(r0, r1, t1));
                t0 /= t1;
            }
            if (t0 > 0)
            {
                var (q01, q12, q23) = (Lerp(p0, p1, t0), Lerp(p1, p2, t0), Lerp(p2, p3, t0));
                var (r0, r1) = (Lerp(q01, q12, t0), Lerp(q12, q23, t0));
                (p0, p1, p2) = (Lerp(r0, r1, t0), r1, q23);
            }
            start ??= p0;
            curves.Add((p1, p2, p3));
        }
        return start is { } first ? new CurveItem(first, curves, width, color) : null;
    }

    /// <summary>
    /// Run borders (w:bdr, catalog: run.decoration) of a line: neighboring pieces with the same border share a frame,
    /// its lines and space outside the text from its start to its end and the font's height above and below the
    /// baseline (measured with docx/local/underlines and ulsizes: Arial 10 to 24 pt, within 0.1 pt).
    /// </summary>
    static void RunBorders(Line line, double left, double baseline, List<SceneItem> items)
    {
        var pieces = line.Pieces;
        for (var i = 0; i < pieces.Count; i++)
        {
            if (pieces[i].Style.Border is not { } border)
                continue;
            var j = i;
            while (j + 1 < pieces.Count && Equals(pieces[j + 1].Style.Border, border))
                j++;
            var box = pieces.GetRange(i, j - i + 1);
            var at = baseline - box[0].Style.Raise - box[0].Shift;
            var (above, below) = (box.Max(p => p.Style.Metrics.Above), box.Max(p => p.Style.Metrics.Below));
            var room = PageBorders.Room(border);
            if (PageBorders.Frame(border, left + box[0].X, at - above - room, left + box[^1].X + box[^1].Width, at + below + room) is { } frame)
                Fills(frame.Fills, frame.Strokes, items);
            i = j;
        }
    }

    /// <summary>
    /// Emboss, imprint and shadow (catalog: run.decoration) as Word prints them (measured with docx/local/underlines
    /// and ulsizes, 10 to 48 pt): emboss lays the text in gray (#999999) where it stands and in black 1.92 pt up and
    /// left, and moves the text itself 0.96 pt up and left; imprint does the same down and right, the gray copy over
    /// the black one; a shadow is the text in light gray (#C0C0C0) a 24th of its size (in whole dots) down and right.
    /// Returns the copies, which are not selectable, and how far the text moves.
    /// </summary>
    (DecorationItem? Copies, double Dx, double Dy) Relief(Piece piece, double x, double baseline)
    {
        var style = piece.Style;
        if (!style.Emboss && !style.Imprint && !style.Shadow || string.IsNullOrWhiteSpace(piece.Text))
            return (null, 0, 0);
        var copies = new DecorationItem([]);
        void Copy(FreeSlate.Drawing.RgbColor color, double dx, double dy) =>
            AddItem(copies.Items, BrowserText(new Piece
            {
                Text = piece.Text, Style = style with { Color = color, Hollow = false, TextFill = null, Outline = null },
                X = piece.X, Advances = piece.Advances, Lead = piece.Lead, Level = piece.Level,
                SpaceExtra = piece.SpaceExtra, JustifiedSpaces = piece.JustifiedSpaces, UnspreadSpaces = piece.UnspreadSpaces,
            }, x + dx, baseline + dy));
        var gray = FreeSlate.Drawing.RgbColor.FromHex("999999");
        double move = 0;
        if (style.Shadow)
        {
            var offset = Dots(style.Size / 24) * Dot;
            Copy(FreeSlate.Drawing.RgbColor.FromHex("C0C0C0"), offset, offset);
        }
        if (style.Emboss)
        {
            Copy(gray, 0, 0);
            Copy(FreeSlate.Drawing.RgbColor.Black, -1.92, -1.92);
            move = -0.96;
        }
        else if (style.Imprint)
        {
            Copy(FreeSlate.Drawing.RgbColor.Black, 1.92, 1.92);
            Copy(gray, 0, 0);
            move = 0.96;
        }
        return (copies, move, move);
    }

    /// <summary>Text the browser draws (and selects), every character at its position, in the run's font and color.</summary>
    /// <param name="device">The places of the line's pieces on Word's device (see Positions), null for a piece not on the page.</param>
    TextItem? BrowserText(Piece piece, double x, double baseline, IReadOnlyDictionary<Piece, double[]>? device = null)
    {
        if (string.IsNullOrWhiteSpace(piece.Text))
            return null;
        var style = piece.Style;
        var positions = Positions(piece, device);
        context.UsedFonts.Add(style.Font.Data.Family);
        return new TextItem(piece.Text, x, positions, baseline, style.Font.Data, style.Size, style.Color)
        {
            Paint = TextPaint(piece, x, baseline),
            // Bold of a font without a bold face: Word fills the glyphs and strokes them a 35th of the size wide (its PDF:
            // Lucida Sans Unicode at 12 and 14.04 pt stroked 0.343 and 0.401 pt; LibreOffice's extras-ooxmlexport/
            // style-inheritance), where the browser would embolden them its own way.
            Bold = style.Bold, StrokedBold = style.Bold && style.Font.SyntheticBold && !style.Hollow && style.TextFill == null && style.Outline == null,
            Italic = style.Italic, Features = style.Features, Language = style.Language, Scale = style.Scale,
            RightToLeft = piece.RightToLeft, Width = piece.Width, WordSpacing = piece.SpaceExtra, JoinsBefore = piece.JoinsBefore, JoinsAfter = piece.JoinsAfter,
            Hollow = style.Hollow, Shadow = style.Shadow,
        };
    }

    /// <summary>A text in one style at a place, drawn by the browser.</summary>
    TextItem? TextAt(string text, RunStyle style, double x, double baseline, double spacing = 0)
    {
        if (text.Length == 0)
            return null;
        var advances = ParagraphLayout.Measure(text, style);
        if (spacing != 0)
        {
            for (var k = 0; k < advances.Length - 1; k++)
            {
                if (!char.IsLowSurrogate(text[k]))
                    advances[k] += spacing;
            }
        }
        return BrowserText(new Piece { Text = text, Style = style, X = x, Advances = advances }, x, baseline);
    }

    /// <summary>
    /// A ruby (measured with docx/local/ruby): the phonetic text w:hpsRaise above the base's baseline; narrower than its
    /// base it is centered over it, at its left or right, or spread over it (distributeLetter: the gaps between its
    /// letters, distributeSpace: also half a gap at either end); wider, it spans the ruby's width and the base is
    /// centered under it.
    /// </summary>
    void Ruby(RubyInline ruby, double x, double baseline, double width, List<SceneItem> items)
    {
        var baseText = ruby.Base.Select(t => (Text: t.Text, Style: styles!.Style(t.Format, WordSlot.EastAsia))).ToList();
        var rubyText = ruby.Ruby.Select(t => (Text: t.Text, Style: styles!.Style(t.Format, WordSlot.EastAsia))).ToList();
        var baseWidth = baseText.Sum(t => ParagraphLayout.Measure(t.Text, t.Style).Sum());
        var rubyWidth = rubyText.Sum(t => ParagraphLayout.Measure(t.Text, t.Style).Sum());
        if (ruby.Align == "rightVertical")
        {
            // The base, and its ruby down along its right, a character a line from the base's top.
            var bx = x;
            foreach (var (text, style) in baseText)
            {
                AddItem(items, TextAt(text, style, bx, baseline));
                bx += ParagraphLayout.Measure(text, style).Sum();
            }
            // Centered on the base's middle (its win ascent and descent).
            var count = rubyText.Sum(t => t.Text.EnumerateRunes().Count());
            var baseStyle = baseText.Count > 0 ? baseText[0].Style : null;
            var middle = baseStyle != null ? baseline - (baseStyle.Font.Data.WinAscent - baseStyle.Font.Data.WinDescent) / 2 * baseStyle.Size : baseline;
            var top = middle - count * (rubyText.Count > 0 ? rubyText[0].Style.Size : 0) / 2;
            foreach (var (text, style) in rubyText)
            {
                foreach (var rune in text.EnumerateRunes())
                {
                    top += style.Size;
                    AddItem(items, TextAt(rune.ToString(), style, bx, top - style.Size * 0.12));
                }
            }
            return;
        }
        // The narrower of base and ruby spreads over the wider one's width as w:rubyAlign says, the base too
        // (measured: Word's PDF of docx/external/poi/61470: とうきょう over 東京 at
        // distributeSpace, 東 at 73.344 and 京 at 87.144 pt, half a gap at either end).
        (double Start, double Gap) Spread(int letters, double free) => free <= 0.01 || letters == 0 ? (x, 0.0) : ruby.Align switch
        {
            "left" => (x, 0.0),
            "right" or "rightVertical" => (x + free, 0.0),
            "distributeLetter" when letters > 1 => (x, free / (letters - 1)),
            "distributeSpace" => (x + free / letters / 2, free / letters),
            _ => (x + free / 2, 0.0),
        };
        void Draw(List<(string Text, RunStyle Style)> texts, double width, double y)
        {
            var (at, gap) = Spread(texts.Sum(t => t.Text.EnumerateRunes().Count()), width);
            foreach (var (text, style) in texts)
            {
                AddItem(items, TextAt(text, style, at, y, gap));
                at += ParagraphLayout.Measure(text, style).Sum() + gap * text.EnumerateRunes().Count();
            }
        }
        Draw(baseText, width - baseWidth, baseline);
        Draw(rubyText, width - rubyWidth, baseline - ruby.Raise);
    }

    /// <summary>
    /// East Asian emphasis marks over (or under) every character but spaces (w:em, catalog: run.eastAsian), measured
    /// with docx/local/ruby at 12 pt: a dot (U+FF0E) or comma (U+3001) in the run's font starting 0.17 em before the
    /// character's middle and 1.01 em above its baseline, a ring (U+02DA, Batang) 0.157 em before it and 0.29 em above,
    /// a dot below (U+FF0E, SimSun) 0.205 em before it and 0.38 em below (LibreOffice's extras-ooxmlexport/em: over
    /// Calibri 11 letters 2.53 to 8.82 pt wide as over ideographs).
    /// </summary>
    void EmphasisMarks(Piece piece, string emphasis, double x, double baseline, IReadOnlyDictionary<Piece, double[]>? device, List<SceneItem> items)
    {
        var style = piece.Style;
        var (mark, family, dx, dy) = emphasis switch
        {
            "comma" => ("\u3001", (string?)null, -0.17, -1.01),
            "circle" => ("\u02DA", "Batang", -0.157, -0.29),
            "underDot" => ("\uFF0E", "SimSun", -0.205, 0.38),
            _ => ("\uFF0E", null, -0.17, -1.01),
        };
        var markStyle = family != null && context.Fonts.TryResolveTypeface(family, false, false) is { } font ? style with { Font = font } : style;
        var positions = Positions(piece, device);
        for (var k = 0; k < piece.Text.Length; k++)
        {
            if (char.IsLowSurrogate(piece.Text[k]) || char.IsWhiteSpace(piece.Text[k]) || piece.Advances[k] <= 0)
                continue;
            var advance = k + 1 < positions.Length ? positions[k + 1] - positions[k] : piece.Advances[k];
            AddItem(items, TextAt(mark, markStyle, x + positions[k] + advance / 2 + dx * style.Size, baseline + dy * style.Size));
        }
    }

    /// <summary>
    /// The fill and outline of a piece's text (catalog: run.textEffects): w14:textFill (solid, gradient over the piece
    /// from its ascent to its descent, or none) and w14:textOutline, as SVG attributes; null for the run's color.
    /// </summary>
    string? TextPaint(Piece piece, double x, double baseline)
    {
        var style = piece.Style;
        // Outline (w:outline): the letters' outlines 0.14 pt wide in the text's color, hollow, or white over a shadow
        // (measured with docx/local/ulsizes from 10 to 48 pt).
        if (style.Hollow)
            return $" fill=\"{(style.Shadow ? "#FFFFFF" : "none")}\" stroke=\"{style.Color.ToHex()}\" stroke-width=\"{Px(0.14)}\" stroke-linejoin=\"round\"";
        if (style.TextFill == null && style.Outline == null || drawings == null)
            return null;
        var (above, below) = style.Metrics;
        var (w, h) = (piece.Width * PxPerPt, (above + below) * PxPerPt);
        var origin = (X: x * PxPerPt, Y: (baseline - above) * PxPerPt);
        var fill = style.TextFill is { } own ? FreeSlate.Drawing.FillReader.Read(own, drawings.Colors) : new FreeSlate.Drawing.SolidFill(style.Color);
        var paint = drawings.Paint.Fill(fill, w, h, defs, origin: origin, offset: origin);
        if (style.Outline is { } line && FreeSlate.Drawing.OutlineReader.Merge([(line, drawings.Colors)]) is { Visible: true } outline)
            // A gradient outline spans the piece as its fill does (docx/external/libreoffice/extras-ooxmlexport/
            // TextEffects_TextOutline: not from the page's corner).
            paint += drawings.Paint.Stroke(outline, w, h, defs, origin, offset: origin);
        return paint;
    }

    /// <summary>The copies of text that its effects lie behind, numbered for their ids.</summary>
    int effectCopies;

    /// <summary>
    /// A piece's text with its glow and shadow (the filter shared with PowerPoint, sized with the font) and its
    /// reflection: a mirrored copy below the baseline (catalog: run.textEffects).
    /// </summary>
    void AddWithEffects(List<SceneItem> items, Piece piece, SceneItem? text, double x, double baseline)
    {
        if (text == null)
            return;
        var style = piece.Style;
        if (style.Effects is not { } effects || drawings == null)
        {
            items.Add(text);
            return;
        }
        var scale = Html.SvgEffects.TextScale.Of(style.Size);
        // Shadows and glow lie behind a copy of the text, the text itself unfiltered on top: a filtered group is printed
        // as a picture, its outline lighter than Word's vector text (LibreOffice's extras-ooxmlexport/
        // tdf166325_scheme_color: a 0.5 pt text outline with a shadow). Effects on the text itself filter it.
        if (Html.SvgEffects.Filter(effects, drawings.Colors, 0, 0, new StringBuilder(), boundingBox: true, text: scale, parts: Html.SvgEffects.Parts.Shape) == null
            && Html.SvgEffects.FilterOf(effects, drawings.Colors, 0, 0, defs, boundingBox: true, text: scale, parts: Html.SvgEffects.Parts.Outer) is { } outer)
        {
            // The copy refers to the text, which so stands in the page once (to select, search and read).
            text = new FilterCopyItem($"tx{++effectCopies}", outer.Id, outer.Model, [text]);
        }
        else if (Html.SvgEffects.FilterOf(effects, drawings.Colors, 0, 0, defs, boundingBox: true, text: scale) is { } filter)
            text = new FilterItem(filter.Id, filter.Model, [text]);
        // Text reflects about its baseline, the box reaching up to the font's ascent (as PowerPoint draws it).
        if (effects.Element(Ns.A + "reflection") is { } reflection)
        {
            var distance = Units.ToPx(reflection.Long("dist") ?? 0) * scale.ReflectionDistance / 2;
            var bottom = baseline * PxPerPt + distance;
            var height = style.Size * PxPerPt * style.Font.Data.Ascent + distance;
            if (Html.SvgEffects.ReflectionOf(effects, (x * PxPerPt, bottom - height, piece.Width * PxPerPt, height), defs,
                    distanceScale: 0, blurScale: scale.ReflectionBlur) is { } reflected)
                text = new ReflectionItem(reflected, [text]);
        }
        items.Add(text);
    }

    /// <summary>
    /// A tab's leader: its character repeated where <see cref="TabLeader"/> puts it, in the tab's font and color; the
    /// grid counts from the tab's place on the page before the PDF's growth moves it (Word's PDF of docx/external/
    /// libreoffice/extras-ooxmlexport/tdf137335_whitespaceLineHeight: dots of 9 pt from 72 pt, not from 81).
    /// </summary>
    TextItem? LeaderText(Piece tab, double x, double moved, double baseline)
    {
        var positions = TabLeader.Positions(tab, x).Select(p => p + moved).ToArray();
        if (positions.Length == 0 || TabLeader.Character(tab.Leader) is not { } c)
            return null;
        var style = tab.Style;
        context.UsedFonts.Add(style.Font.Data.Family);
        // Bold of a font without a bold face stroked as the text is (docx/local/sbl-a: the dots filled, then stroked a
        // 35th of the size wide).
        return new TextItem(new string(c, positions.Length), 0, positions, baseline, style.Font.Data, style.Size, style.Color)
        {
            Bold = style.Bold || tab.Leader == "heavy", StrokedBold = style.Bold && style.Font.SyntheticBold, Italic = style.Italic,
        };
    }

    /// <summary>The piece's text, invisible, character by character where the glyphs are, for selection and search.</summary>
    HiddenTextItem? SelectionText(Piece piece, double x, double baseline, IReadOnlyDictionary<Piece, double[]>? device)
    {
        if (string.IsNullOrWhiteSpace(piece.Text))
            return null;
        var positions = Positions(piece, device);
        context.UsedFonts.Add(piece.Style.Font.Data.Family);
        return new HiddenTextItem(piece.Text, x, positions, baseline, piece.Style.Font.Data, piece.Style.Size);
    }
}
