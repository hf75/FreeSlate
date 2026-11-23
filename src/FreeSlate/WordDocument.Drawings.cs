using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Docx.Layout;
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

    /// <summary>Word's mark of a drawing as decoration (adec:decorative, in an a:ext of wp:docPr with its own uri).</summary>
    static readonly XNamespace Adec = "http://schemas.microsoft.com/office/drawing/2017/decorative";
    const string DecorativeUri = "{C183D7F6-B498-43B3-948B-1728B52AA6E4}";

    /// <summary>
    /// The bytes of the picture the drawing at a place of a paragraph's text shows (its a:blip's r:embed in the part it stands in); null
    /// where it shows none, or its part is not in the package.
    /// </summary>
    public byte[]? PictureOf(int paragraph, int offset) => Embedded(paragraph, offset, blip => blip);

    /// <summary>
    /// The bytes of the SVG the picture of the drawing at a place of a paragraph's text shows where it has one (Office's asvg:svgBlip in
    /// its a:blip's extensions, the a:blip's own picture drawn from it for programs that read no SVG); null where it has none.
    /// </summary>
    public byte[]? VectorOf(int paragraph, int offset) =>
        Embedded(paragraph, offset, blip => blip.Descendants().FirstOrDefault(e => e.Name.LocalName == "svgBlip"));

    /// <summary>The bytes of the chart part the drawing at a place of a paragraph's text shows (its c:chart's r:id); null where it shows no chart.</summary>
    public byte[]? ChartOf(int paragraph, int offset)
    {
        var (p, _) = Find(paragraph);
        var at = p.TextOffset;
        foreach (var inline in p.Content)
        {
            var length = ParagraphText.Length(inline);
            if (at == offset && length > 0)
            {
                var (own, part) = inline switch { DrawingInline d => (d.Drawing, d.Part), AnchorInline a => (a.Anchor, a.Part), _ => (null, null) };
                var chart = own?.Descendants().FirstOrDefault(e => e.Name.LocalName == "chart" && e.Name.NamespaceName.EndsWith("/chart", StringComparison.Ordinal));
                var id = (string?)chart?.Attributes().FirstOrDefault(a => a.Name.LocalName == "id");
                return id != null && part != null && package.Relationship(part, id) is { External: false } related && package.Exists(related.Target)
                    ? package.ReadBytes(related.Target) : null;
            }
            at += length;
        }
        return null;
    }

    /// <summary>The bytes of the part an element of the first a:blip of the drawing at a place refers to by its r:embed; null where there is none.</summary>
    byte[]? Embedded(int paragraph, int offset, Func<XElement, XElement?> referring)
    {
        var (p, _) = Find(paragraph);
        var at = p.TextOffset;
        foreach (var inline in p.Content)
        {
            var length = ParagraphText.Length(inline);
            if (at == offset && length > 0)
            {
                var (own, part) = inline switch { DrawingInline d => (d.Drawing, d.Part), AnchorInline a => (a.Anchor, a.Part), _ => (null, null) };
                var blip = own?.Descendants(A + "blip").FirstOrDefault() is { } first ? referring(first) : null;
                var id = (string?)blip?.Attributes().FirstOrDefault(a => a.Name.LocalName == "embed");
                return id != null && part != null && package.Relationship(part, id) is { External: false } related && package.Exists(related.Target)
                    ? package.ReadBytes(related.Target) : null;
            }
            at += length;
        }
        return null;
    }

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
        // A line round a picture with its caption goes round the picture in its line, the figure making room for it (see Refit), which
        // Word shows (it shows of a text box only what lies within its text); so does the picture turned, mirrored or tilted, its caption
        // staying level under it. A few changes taken as one.
        if ((format.Outline != null || format.OutlineColor != null || format.Rotation != null || format.FlipHorizontal != null || format.FlipVertical != null
                || format.Tilt != null) && FigureAt(paragraph, offset) is { } figure)
        {
            var start = version;
            try
            {
                Format(figure.Holder.Id, figure.Holder.TextOffset, new DrawingFormatting
                {
                    Outline = format.Outline,
                    OutlineColor = format.OutlineColor,
                    Rotation = format.Rotation,
                    FlipHorizontal = format.FlipHorizontal,
                    FlipVertical = format.FlipVertical,
                    Tilt = format.Tilt,
                });
                Refit(paragraph, offset, indent: true);
                if (format with { Outline = null, OutlineColor = null, Rotation = null, FlipHorizontal = null, FlipVertical = null, Tilt = null } is var rest
                    && rest != new DrawingFormatting())
                    Format(paragraph, offset, rest);
                Combine(version - start);
            }
            catch
            {
                Back(start);
                throw;
            }
            return;
        }
        var (p, top) = Find(paragraph);
        if (DrawingAt(p, offset) is not { } own)
            throw new NotSupportedException("No drawing stands at that place.");
        var drawn = Drawn(own, format, () => WhereItStands(paragraph, offset));
        Change(top, top, (stretch, _) => TextEdits.Redraw(stretch, p, offset, drawn));
    }

    /// <summary>
    /// Moves the drawing whose character stands at a place of a paragraph's text to another place of the text of its story, one change,
    /// as Word moves one dragged there: in the line it goes in there; floating, its anchor goes there. Its run's formatting goes with
    /// it. Returns where its character then stands.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// No drawing stands at the place, the other is in another story or within what the document computes or keeps, or changes are
    /// tracked; nothing changes.
    /// </exception>
    public (int Paragraph, int Offset) MoveDrawing(int paragraph, int offset, int toParagraph, int toOffset)
    {
        var (p, top) = Find(paragraph);
        var (q, toTop) = Find(toParagraph);
        if (DrawingAt(p, offset) == null)
            throw new NotSupportedException("No drawing stands at that place.");
        if (toParagraph == paragraph && (toOffset == offset || toOffset == offset + 1))
            return (paragraph, offset);
        if (!ReferenceEquals(top, toTop) && !(paragraphs.ContainsKey(paragraph) && paragraphs.ContainsKey(toParagraph)))
            throw new NotSupportedException("A drawing moves within its story.");
        var (first, last) = IndexOf(top) <= IndexOf(toTop) ? (top, toTop) : (toTop, top);
        Change(first, last, (stretch, tracking) => tracking == null ? TextEdits.MoveDrawing(stretch, p, offset, q, toOffset)
            : throw new NotSupportedException("A drawing moved is not tracked."));
        return (toParagraph, toParagraph == paragraph && toOffset > offset ? toOffset - 1 : toOffset);
    }

    /// <summary>
    /// Puts the floating drawing whose character stands at a place of a paragraph's text where its box's top left stands at a point of
    /// a page, in points from the page's top left, one change, as Word puts one dragged there: across and down by offsets from what it
    /// stands from (an alignment given instead, from what it stands from, across from the column where that is neither the margin nor
    /// the column); its anchor goes to the beginning of the paragraph of the body whose line stands at the box's top, where that is
    /// another (one in a table keeps its anchor). Where the layout holds it (between the margins, on the page), it stands as near as
    /// that lets it. Returns where its character then stands.
    /// </summary>
    /// <exception cref="NotSupportedException">No floating drawing stands at the place; nothing changes.</exception>
    public (int Paragraph, int Offset) PlaceDrawing(int paragraph, int offset, int page, double x, double y, string? alignAcross = null, string? alignDown = null)
    {
        var start = version;
        try
        {
            var place = Placed(paragraph, offset, page, x, y, alignAcross, alignDown, anchorMoves: true);
            Combine(version - start);
            return place;
        }
        catch
        {
            Back(start);
            throw;
        }
    }

    /// <summary>
    /// Gives the drawing whose character stands at a place of a paragraph's text a size, in points, one change, as Word sizes one by a
    /// handle: floating, with its box's top left at a point of a page where it is given (the side it is sized from moving, the other
    /// staying, see PlaceDrawing), its anchor staying where it is. A picture with its caption (see AddCaption) is sized by its picture:
    /// as wide as given, as high as given less the caption, or, where both change (a corner's sizing), its picture's proportions kept;
    /// the figure then as high as it holds. Returns where its character then stands.
    /// </summary>
    /// <exception cref="NotSupportedException">No drawing stands at the place; nothing changes.</exception>
    public (int Paragraph, int Offset) SizeDrawing(int paragraph, int offset, double width, double height, (int Page, double X, double Y)? at = null,
        string? alignAcross = null, string? alignDown = null)
    {
        var start = version;
        try
        {
            if (FigureAt(paragraph, offset) is { } figure)
                SizedByHandles(figure, paragraph, offset, width, height);
            else
                Format(paragraph, offset, new DrawingFormatting { Width = width, Height = height });
            var place = (paragraph, offset);
            if (at is var (page, x, y) && DrawingOf(paragraph, offset) is { Wrap: not "inline" })
                place = Placed(paragraph, offset, page, x, y, alignAcross, alignDown, anchorMoves: false);
            Combine(version - start);
            return place;
        }
        catch
        {
            Back(start);
            throw;
        }
    }

    /// <summary>The changes since a version taken back and forgotten (see Refused).</summary>
    void Back(int start)
    {
        if (version <= start)
            return;
        Go(start);
        versions.RemoveRange(start + 1, versions.Count - start - 1);
    }

    /// <summary>
    /// A floating drawing put where its box's top left is to stand (see PlaceDrawing), a change or a few; where <paramref name="anchorMoves"/>,
    /// its anchor to the paragraph there, unless its box's top stays within what its paragraph and it take of the page.
    /// </summary>
    (int Paragraph, int Offset) Placed(int paragraph, int offset, int page, double x, double y, string? alignAcross, string? alignDown, bool anchorMoves)
    {
        var (p, top) = Find(paragraph);
        if (DrawingAt(p, offset) is not { Name.LocalName: "anchor" })
            throw new NotSupportedException("No floating drawing stands at that place.");
        Scenes();
        if (page < 0 || page >= shown.Count)
            throw new ArgumentOutOfRangeException(nameof(page), "The document has no such page laid out.");
        var place = (Paragraph: paragraph, Offset: offset);
        // Its anchor to the paragraph of the body at its top, where that is another; while its top stays within its own paragraph and
        // itself (which pushes the paragraph's text away where it is wide), the anchor stays.
        if (anchorMoves && paragraphs.ContainsKey(paragraph) && ReferenceEquals(p, top) && !Within(page, paragraph, offset, y)
            && ParagraphAt(page, x, y) is { } at && at != paragraph)
        {
            try
            {
                place = MoveDrawing(paragraph, offset, at, Find(at).Paragraph.TextOffset);
            }
            catch (NotSupportedException)
            {
                // It stays anchored where it was.
            }
        }
        // Offsets from what it stands from: from where it is by as much as it moves, else from where that begins; measured as laid out
        // and once more where the layout put it elsewhere (a paragraph's top lies above its space before).
        Scenes();
        var now = DrawingOf(place.Paragraph, place.Offset)!;
        var stood = place == (paragraph, offset) ? PlacedAt(place.Paragraph, place.Offset) : null;
        var across = alignAcross != null && now.HorizontalFrom is not ("margin" or "column") ? "column" : now.HorizontalFrom ?? "column";
        var down = now.VerticalFrom ?? "paragraph";
        var format = new DrawingFormatting
        {
            HorizontalFrom = across,
            HorizontalAlign = alignAcross,
            HorizontalOffset = alignAcross != null ? null
                : now.HorizontalOffset is { } h && stood is var (_, was) ? h + x - was.X : x - AcrossFrom(across, shown[page], x),
            VerticalFrom = down,
            VerticalAlign = alignDown,
            VerticalOffset = alignDown != null ? null
                : now.VerticalOffset is { } v && stood is var (_, had) ? v + y - had.Y : y - DownFrom(down, shown[page], place.Paragraph),
        };
        for (var pass = 0; pass < 2; pass++)
        {
            Format(place.Paragraph, place.Offset, format);
            if (PlacedAt(place.Paragraph, place.Offset) is not var (on, drawn) || on != page
                || Math.Abs(drawn.X - x) < 0.01 && Math.Abs(drawn.Y - y) < 0.01 || pass == 1)
                break;
            format = format with
            {
                HorizontalOffset = format.HorizontalOffset + x - drawn.X,
                VerticalOffset = format.VerticalOffset + y - drawn.Y,
            };
        }
        return place;
    }

    /// <summary>
    /// Whether a height of a page lies within what a paragraph takes of the page with the floating drawing at a place of its text: from
    /// the higher of its first line there and the drawing (whose text the drawing may push below it) to its last line's bottom.
    /// </summary>
    bool Within(int page, int paragraph, int offset, double y)
    {
        var lines = shown[page].Lines.Where(l => l.Line.Paragraph?.Id == paragraph).ToList();
        if (lines.Count == 0)
            return false;
        var (top, bottom) = (lines.Min(l => l.Top), lines.Max(l => l.Top + l.Line.Height));
        if (PlacedAt(paragraph, offset) is var (on, drawn) && on == page)
            top = Math.Min(top, drawn.Y);
        return y >= top - 0.01 && y <= bottom + 0.01;
    }

    /// <summary>
    /// The text area of a page as laid out (see IEditableDocument.TextArea): its columns across, from the left of the first, and the
    /// margins down; null for a page not laid out.
    /// </summary>
    public (IReadOnlyList<(double Left, double Right)> Columns, double Top, double Bottom)? TextArea(int page)
    {
        Scenes();
        if (page < 0 || page >= shown.Count)
            return null;
        var box = shown[page];
        return (ColumnsOf(box), box.Section.Top, box.Section.PageHeight - box.Section.Bottom);
    }

    /// <summary>The columns of a page's text, each from its left to its right in points from the page's left.</summary>
    static List<(double Left, double Right)> ColumnsOf(PageBox page)
    {
        var s = page.Section;
        var columns = new List<(double Left, double Right)>();
        var left = page.TextLeft;
        if (s.ColumnWidths is { Count: > 0 } widths)
        {
            foreach (var (width, space) in widths)
            {
                columns.Add((left, left + width));
                left += width + space;
            }
            return columns;
        }
        var count = Math.Max(1, s.Columns);
        var each = (page.TextWidth - (count - 1) * s.ColumnSpace) / count;
        for (var k = 0; k < count; k++)
            columns.Add((left + k * (each + s.ColumnSpace), left + k * (each + s.ColumnSpace) + each));
        return columns;
    }

    /// <summary>The page a floating drawing at a place of a paragraph's text is laid out on and its box there, or null where it is on none.</summary>
    (int Page, PlacedDrawing Drawing)? PlacedAt(int paragraph, int offset)
    {
        Scenes();
        if (DrawingAt(Find(paragraph).Paragraph, offset) is not { } own)
            return null;
        for (var k = 0; k < shown.Count; k++)
        {
            if (shown[k].Drawings.FirstOrDefault(d => ReferenceEquals(d.Drawing, own)) is { } drawn)
                return (k, drawn);
        }
        return null;
    }

    /// <summary>
    /// The paragraph of the body, not of a table, whose line on a page stands at a height in the column at a point across: the last
    /// beginning above it, else the first; null where the page has none.
    /// </summary>
    int? ParagraphAt(int page, double x, double y)
    {
        var box = shown[page];
        var columns = ColumnsOf(box);
        var column = columns.LastOrDefault(c => c.Left <= x + 0.01, columns[0]);
        var lines = box.Lines.Where(l => !l.Story && l.Line.Paragraph is { } own && paragraphs.TryGetValue(own.Id, out var found)
            && ReferenceEquals(found.Paragraph, found.Top)).ToList();
        var inColumn = lines.Where(l => l.Left >= column.Left - 1 && l.Left < column.Right).ToList();
        if (inColumn.Count > 0)
            lines = inColumn;
        var line = lines.Where(l => l.Top <= y + 0.01).MaxBy(l => l.Top) ?? lines.MinBy(l => l.Top);
        return line?.Line.Paragraph!.Id;
    }

    /// <summary>Where a drawing's place across counts from on a page, near enough for a first guess (see Paginator.Anchored).</summary>
    static double AcrossFrom(string from, PageBox page, double x)
    {
        var right = page.TextLeft + page.TextWidth;
        var odd = page.DisplayNumber % 2 == 1;
        return from switch
        {
            "page" or "leftMargin" => 0,
            "rightMargin" => right,
            "insideMargin" => odd ? 0 : right,
            "outsideMargin" => odd ? right : 0,
            "column" or "character" => ColumnsOf(page).Select(c => c.Left).Where(left => left <= x + 0.01).DefaultIfEmpty(page.TextLeft).Last(),
            _ => page.TextLeft,
        };
    }

    /// <summary>Where a drawing's place down counts from on a page, near enough for a first guess (see Paginator.Anchored).</summary>
    static double DownFrom(string from, PageBox page, int paragraph)
    {
        var s = page.Section;
        var bottom = s.PageHeight - s.Bottom;
        var odd = page.DisplayNumber % 2 == 1;
        return from switch
        {
            "page" or "topMargin" => 0,
            "bottomMargin" => bottom,
            "insideMargin" => odd ? 0 : bottom,
            "outsideMargin" => odd ? bottom : 0,
            "paragraph" or "line" => page.Lines.Where(l => l.Line.Paragraph?.Id == paragraph).Select(l => l.Top).DefaultIfEmpty(s.Top).Min(),
            _ => s.Top,
        };
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

    /// <summary>How a drawing's element has it stand (see DrawingOf), and a picture show.</summary>
    static DrawingFormatting Read(XElement own)
    {
        var (width, height) = Drawings.Extent(own);
        var wp = own.Name.Namespace;
        var docPr = own.Element(wp + "docPr");
        var (description, decorative) = ((string?)docPr?.Attribute("descr") ?? "", Decorative(docPr));
        var shows = Shows(Pic(own.Element(A + "graphic")) ?? FrameShape(own.Element(A + "graphic")));
        if (own.Name.LocalName == "inline")
            return shows with { Width = width, Height = height, Wrap = "inline", Description = description, Decorative = decorative };
        var wrap = own.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("wrap", StringComparison.Ordinal));
        var across = own.Element(wp + "positionH");
        var down = own.Element(wp + "positionV");
        return shows with
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
            Description = description,
            Decorative = decorative,
        };
    }

    /// <summary>
    /// The pic:pic a drawing's graphic shows: a picture's own, or the picture's of a picture with its caption (see AddCaption); null for
    /// other graphics.
    /// </summary>
    static XElement? Pic(XElement? graphic) =>
        graphic?.Element(A + "graphicData") is { } data && (string?)data.Attribute("uri") == PictureUri
            ? data.Elements().FirstOrDefault(e => e.Name.LocalName == "pic")
            : FigurePicture(graphic)?.Element(A + "graphic") is { } inner ? Pic(inner) : null;

    /// <summary>
    /// The wps:wsp of a text box a drawing's graphic shows that is a frame of a table (see WordDocument.Frames) or one its story goes on in
    /// (wps:linkedTxbx), which turns and tilts as a picture does; null for other graphics.
    /// </summary>
    static XElement? FrameShape(XElement? graphic) =>
        graphic?.Element(A + "graphicData") is { } data && (string?)data.Attribute("uri") == ShapeUri && FigurePicture(graphic) == null
        && data.Element(Drawings.Wps + "wsp") is { } shape
        && (shape.Element(Drawings.Wps + "linkedTxbx") != null
            || shape.Element(Drawings.Wps + "txbx")?.Elements().FirstOrDefault(e => e.Name.LocalName == "txbxContent") is { } content
            && content.Elements().FirstOrDefault()?.Name.LocalName == "tbl") ? shape : null;

    /// <summary>How a picture's pic:pic has it show (see DrawingFormatting.Crop, Shape, Recolor, Outline, Rotation, Tilt); nothing where there is none.</summary>
    static DrawingFormatting Shows(XElement? pic)
    {
        if (pic == null)
            return new DrawingFormatting();
        var fill = pic.Element(pic.Name.Namespace + "blipFill");
        var spPr = pic.Element(pic.Name.Namespace + "spPr");
        var line = spPr?.Element(A + "ln");
        var solid = line?.Element(A + "solidFill");
        var xfrm = spPr?.Element(A + "xfrm");
        double Share(XElement rect, string side) => (long?)rect.Attribute(side) / 100000.0 ?? 0;
        return new DrawingFormatting
        {
            Crop = fill?.Element(A + "srcRect") is { } rect ? new PictureCrop(Share(rect, "l"), Share(rect, "t"), Share(rect, "r"), Share(rect, "b")) : PictureCrop.None,
            Shape = (string?)spPr?.Element(A + "prstGeom")?.Attribute("prst"),
            Recolor = RecolorOf(fill?.Element(A + "blip")),
            // A line without a width is as wide as Office draws one by default.
            Outline = solid != null ? Points((string?)line!.Attribute("w")) ?? 0.75 : 0,
            OutlineColor = (string?)solid?.Element(A + "srgbClr")?.Attribute("val"),
            Rotation = Turned(((long?)xfrm?.Attribute("rot") ?? 0) / 60000.0),
            FlipHorizontal = (string?)xfrm?.Attribute("flipH") is "1" or "true",
            FlipVertical = (string?)xfrm?.Attribute("flipV") is "1" or "true",
            Tilt = TiltOf(spPr?.Element(A + "scene3d")),
        };
    }

    /// <summary>An angle in degrees from 0 up to 360.</summary>
    static double Turned(double degrees) => (degrees % 360 + 360) % 360;

    /// <summary>How an a:scene3d has a picture tilted (see PictureTilt): flat without one.</summary>
    static PictureTilt TiltOf(XElement? scene)
    {
        if (scene?.Element(A + "camera") is not { } camera)
            return PictureTilt.Flat;
        var rot = camera.Element(A + "rot");
        double? Degrees(string name, int sign) => rot == null ? null : Turned(sign * ((long?)rot.Attribute(name) ?? 0) / 60000.0);
        return new PictureTilt((string?)camera.Attribute("prst") ?? "orthographicFront", Degrees("lon", -1), Degrees("lat", 1), Degrees("rev", -1),
            (long?)camera.Attribute("fov") is { } fov ? fov / 60000.0 : null);
    }

    /// <summary>The effects of an a:blip that recolor its picture, as Word's Recolor sets one of them (see DrawingFormatting.Recolor).</summary>
    static readonly HashSet<string> Recolorings = ["grayscl", "biLevel", "lum", "duotone"];

    /// <summary>How an a:blip's effects recolor its picture (see DrawingFormatting.Recolor).</summary>
    static string RecolorOf(XElement? blip) =>
        blip?.Elements().Where(e => e.Name.Namespace == A && Recolorings.Contains(e.Name.LocalName)).ToList() switch
        {
            null or [] => "none",
            [{ Name.LocalName: "grayscl" }] => "grayscale",
            [{ Name.LocalName: "biLevel" } level] when (string?)level.Attribute("thresh") == "50000" => "blackWhite",
            [{ Name.LocalName: "lum" } lum] when (string?)lum.Attribute("bright") == "70000" && (string?)lum.Attribute("contrast") == "-70000" => "washout",
            _ => "other",
        };

    /// <summary>
    /// A picture's pic:pic (a copy, changed in place) showing as a format has it (see DrawingFormatting.Crop, Shape, Recolor, Outline,
    /// Rotation, Tilt): the part of it given where <paramref name="cropping"/> (a picture by itself, whose size goes with it), the shape,
    /// the recoloring in place of any other, the line round it, its turn and mirroring, its tilt, as Word's Picture Format writes them.
    /// </summary>
    static void Show(XElement pic, DrawingFormatting format, bool cropping)
    {
        var fill = pic.Element(pic.Name.Namespace + "blipFill");
        var spPr = pic.Element(pic.Name.Namespace + "spPr");
        if (spPr?.Element(A + "xfrm") is { } frame)
        {
            if (format.Rotation is { } rotation)
                frame.SetAttributeValue("rot", Angle(rotation) is var turn and not 0 ? turn : null);
            if (format.FlipHorizontal is { } across)
                frame.SetAttributeValue("flipH", across ? "1" : null);
            if (format.FlipVertical is { } down)
                frame.SetAttributeValue("flipV", down ? "1" : null);
        }
        if (format.Tilt is { } tilt && spPr != null)
        {
            spPr.Element(A + "scene3d")?.Remove();
            if (!tilt.IsFlat)
            {
                var camera = new XElement(A + "camera", new XAttribute("prst", tilt.Camera));
                if (tilt.Fov is { } fov)
                    camera.SetAttributeValue("fov", (long)Math.Round(fov * 60000));
                if (tilt.X != null || tilt.Y != null || tilt.Z != null)
                    camera.Add(new XElement(A + "rot", new XAttribute("lat", Angle(tilt.Y ?? 0)), new XAttribute("lon", Angle(-(tilt.X ?? 0))),
                        new XAttribute("rev", Angle(-(tilt.Z ?? 0)))));
                var scene = new XElement(A + "scene3d", camera, new XElement(A + "lightRig", new XAttribute("rig", "threePt"), new XAttribute("dir", "t")));
                // After the line and the effects, before an extrusion (a:sp3d) and the extensions.
                if (spPr.Elements().LastOrDefault(e => e.Name.LocalName is "xfrm" or "prstGeom" or "custGeom" or "noFill" or "solidFill" or "gradFill" or "blipFill"
                        or "pattFill" or "grpFill" or "ln" or "effectLst" or "effectDag") is { } after)
                    after.AddAfterSelf(scene);
                else
                    spPr.AddFirst(scene);
            }
        }
        if (cropping && format.Crop is { } crop && fill != null)
        {
            fill.Element(A + "srcRect")?.Remove();
            static string Share(double share) => ((long)Math.Round(share * 100000)).ToString(CultureInfo.InvariantCulture);
            var rect = new XElement(A + "srcRect");
            foreach (var (side, share) in new[] { ("l", crop.Left), ("t", crop.Top), ("r", crop.Right), ("b", crop.Bottom) })
            {
                if (Math.Round(share * 100000) != 0)
                    rect.SetAttributeValue(side, Share(share));
            }
            // After the blip, before how it fills the picture's box.
            if (rect.HasAttributes)
            {
                if (fill.Element(A + "blip") is { } blipAt)
                    blipAt.AddAfterSelf(rect);
                else
                    fill.AddFirst(rect);
            }
        }
        if (format.Shape is { } shape && spPr != null)
        {
            spPr.Elements().Where(e => e.Name == A + "prstGeom" || e.Name == A + "custGeom").Remove();
            var geometry = new XElement(A + "prstGeom", new XAttribute("prst", shape), new XElement(A + "avLst"));
            if (spPr.Element(A + "xfrm") is { } xfrm)
                xfrm.AddAfterSelf(geometry);
            else
                spPr.AddFirst(geometry);
        }
        if (format.Recolor is { } recolor && recolor != "other" && fill?.Element(A + "blip") is { } blip)
        {
            blip.Elements().Where(e => e.Name.Namespace == A && Recolorings.Contains(e.Name.LocalName)).Remove();
            XElement? effect = recolor switch
            {
                "grayscale" => new XElement(A + "grayscl"),
                "blackWhite" => new XElement(A + "biLevel", new XAttribute("thresh", 50000)),
                "washout" => new XElement(A + "lum", new XAttribute("bright", 70000), new XAttribute("contrast", -70000)),
                _ => null,
            };
            // Before the blip's extensions, as Word writes it.
            if (effect != null)
            {
                if (blip.Element(A + "extLst") is { } extensions)
                    extensions.AddBeforeSelf(effect);
                else
                    blip.Add(effect);
            }
        }
        if ((format.Outline != null || format.OutlineColor != null) && spPr != null)
        {
            var had = spPr.Element(A + "ln");
            var width = format.Outline ?? Shows(pic).Outline ?? 0;
            had?.Remove();
            if (width > 0)
            {
                var color = format.OutlineColor ?? (string?)had?.Element(A + "solidFill")?.Element(A + "srgbClr")?.Attribute("val") ?? "000000";
                var line = new XElement(A + "ln", new XAttribute("w", Emu(width)), new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", color))),
                    had?.Elements().Where(e => !e.Name.LocalName.EndsWith("Fill", StringComparison.Ordinal)).Select(e => new XElement(e)));
                // After the geometry and the fill, before the effects.
                if (spPr.Elements().LastOrDefault(e => e.Name.LocalName is "xfrm" or "prstGeom" or "custGeom" or "noFill" or "solidFill" or "gradFill" or "blipFill" or "pattFill" or "grpFill") is { } before)
                    before.AddAfterSelf(line);
                else
                    spPr.AddFirst(line);
            }
        }
    }

    /// <summary>An angle in 60000ths of a degree from 0 up to a whole turn, as DrawingML writes them.</summary>
    static long Angle(double degrees) => (long)Math.Round(Turned(degrees) * 60000) % 21600000;

    /// <summary>How far a tilted picture's effect extents reach beyond what it shows, in points: 3 pt and a little (Word's 4.5 pt round a flat camera's).</summary>
    const double TiltSeam = 3.01;

    /// <summary>
    /// The effect extents Word gives a picture by itself, of its size, with a line round it, turned or tilted (see DrawingFormatting.Outline,
    /// Rotation, Tilt), on each side: how far the picture with its line (as wide as the line all round; an ellipse as one) stands out of
    /// the box it stands in, turned in its plane and seen through its camera, a little more than 3 pt more where tilted, up to the next
    /// 1.5 pt; the box itself turned with it from 45 up to 135 degrees and from 225 up to 315. Measured in Word 365 by COM
    /// (scratch/TurnCheck): a line alone 1.5 pt at least, its width above (2.25 and 3 pt make 3 pt); the tops and bottoms of 432 turns
    /// and of nearly all 278 tilts alike. Word's sides go round what the picture shows, at times short of it, these never. None without any.
    /// </summary>
    static XElement Margins(XNamespace wp, double width, double height, XElement pic)
    {
        var shows = Shows(pic);
        var line = shows.Outline ?? 0;
        var camera = Drawing.Camera.Read(pic.Element(pic.Name.Namespace + "spPr")?.Element(A + "scene3d")) is { Flat: false } seen ? seen : null;
        var turn = shows.Rotation ?? 0;
        var (a, b) = (width / 2 + line, height / 2 + line);
        (double X, double Y)[] outline = shows.Shape == "ellipse"
            ? [.. Enumerable.Range(0, 72).Select(k => (a * Math.Cos(k * Math.PI / 36), b * Math.Sin(k * Math.PI / 36)))]
            : [(-a, -b), (a, -b), (a, b), (-a, b)];
        var points = Docx.Drawings.Shown(outline, turn, camera);
        var (boxWidth, boxHeight) = turn is >= 45 and < 135 or >= 225 and < 315 ? (height, width) : (width, height);
        var seam = camera != null ? TiltSeam : 0;
        string Side(double beyond) => beyond + seam > 1e-6 ? Emu(Math.Ceiling((beyond + seam) / 1.5 - 1e-6) * 1.5) : "0";
        return new XElement(wp + "effectExtent",
            new XAttribute("l", Side(-points.Min(p => p.X) - boxWidth / 2)), new XAttribute("t", Side(-points.Min(p => p.Y) - boxHeight / 2)),
            new XAttribute("r", Side(points.Max(p => p.X) - boxWidth / 2)), new XAttribute("b", Side(points.Max(p => p.Y) - boxHeight / 2)));
    }

    /// <summary>Whether a drawing's element is a picture by itself (not one with its caption, see AddCaption).</summary>
    static bool IsPicture(XElement own) => GraphicUri(own) == PictureUri;

    /// <summary>Whether a drawing's wp:docPr marks it as decoration (see DrawingFormatting.Decorative).</summary>
    static bool Decorative(XElement? docPr) =>
        docPr?.Elements().FirstOrDefault(e => e.Name.LocalName == "extLst")?.Elements().Where(e => (string?)e.Attribute("uri") == DecorativeUri)
            .SelectMany(e => e.Elements(Adec + "decorative")).Any(e => (string?)e.Attribute("val") is "1" or "true") == true;

    /// <summary>
    /// A drawing's wp:docPr (a copy) with the description and the mark of decoration a format gives, and the picture's own pic:cNvPr in
    /// its graphic with the description too (see DrawingFormatting.Description, Decorative); a figure's picture (see AddCaption) as the
    /// figure, its own wp:docPr too.
    /// </summary>
    static XElement? Described(XElement? docPr, XElement? graphic, DrawingFormatting format)
    {
        if (docPr == null)
            return null;
        if (FigurePicture(graphic) is { } picture)
            Described(picture.Element(picture.Name.Namespace + "docPr"), null, format);
        if (format.Description is { } text)
        {
            docPr.SetAttributeValue("descr", text.Length > 0 ? text : null);
            foreach (var own in graphic?.Descendants().Where(e => e.Name.LocalName == "cNvPr") ?? [])
                own.SetAttributeValue("descr", text.Length > 0 ? text : null);
        }
        if (format.Decorative is { } decorative && decorative != Decorative(docPr))
        {
            var list = docPr.Elements().FirstOrDefault(e => e.Name.LocalName == "extLst");
            list?.Elements().Where(e => (string?)e.Attribute("uri") == DecorativeUri).Remove();
            if (decorative)
            {
                if (list == null)
                    docPr.Add(list = new XElement(A + "extLst", new XAttribute(XNamespace.Xmlns + "a", A)));
                list.Add(new XElement(A + "ext", new XAttribute("uri", DecorativeUri),
                    new XElement(Adec + "decorative", new XAttribute(XNamespace.Xmlns + "adec", Adec), new XAttribute("val", "1"))));
            }
            else if (list != null && !list.HasElements)
                list.Remove();
        }
        return docPr;
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
        // How a picture shows (see Show), by itself or with its caption. One by itself framed, turned, tilted, cut to a shape or sized anew
        // takes Word's effect extents for that (see Margins), where it has no effects or extrusion of its own (a figure's picture so in its
        // line, see Format).
        if (Pic(graphic) is { } pic)
        {
            Show(pic, format, cropping: IsPicture(own));
            if (IsPicture(own) && (format.Outline != null || format.Rotation != null || format.Tilt != null || format.Shape != null || format.Width != null
                    || format.Height != null || format.Crop != null)
                && pic.Element(pic.Name.Namespace + "spPr")?.Elements().Any(e => e.Name.LocalName is "effectLst" or "effectDag" or "sp3d") != true)
                effect = Margins(wp, width, height, pic);
        }
        // A table's frame turns and tilts as a picture does, its effect extents as Word gives a picture's (see Margins).
        else if (FrameShape(graphic) is { } frame)
        {
            Show(frame, format with { Crop = null, Shape = null, Recolor = null, Outline = null, OutlineColor = null }, cropping: false);
            if (format.Rotation != null || format.Tilt != null || format.Width != null || format.Height != null)
                effect = Margins(wp, width, height, frame);
        }
        // A picture's own size goes with the drawing's (pic:spPr/a:xfrm/a:ext).
        if (graphic?.Elements().FirstOrDefault()?.Elements().FirstOrDefault()?.Elements().FirstOrDefault(e => e.Name.LocalName == "spPr")
                ?.Element(A + "xfrm")?.Element(A + "ext") is { } ext)
        {
            ext.SetAttributeValue("cx", Emu(width));
            ext.SetAttributeValue("cy", Emu(height));
        }
        if (wrap == "inline")
            return new XElement(wp + "inline", new XAttribute("distT", 0), new XAttribute("distB", 0), new XAttribute("distL", 0), new XAttribute("distR", 0),
                extent, effect, Described(Kept("docPr"), graphic, format), Kept("cNvGraphicFramePr"), graphic);
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
            across, down, extent, effect, wrapping, Described(Kept("docPr"), graphic, format), Kept("cNvGraphicFramePr"), graphic,
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
