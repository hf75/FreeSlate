using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Docx.Layout;

namespace FreeSlate;

/// <summary>
/// Pictures with their captions, figures (catalog: drawing.textBoxes): a text box where the picture stood (in the line or floating,
/// wrapped and placed as it was), as wide as the picture and as high as what it holds (a:spAutoFit, the height saved kept right as the
/// caption changes, see FitFigure): the picture in a paragraph of its own, then the caption's paragraphs, of Word's caption style. Word
/// moves, wraps and deletes the two as one drawing, whose name and description are the picture's.
/// </summary>
public sealed partial class WordDocument
{
    const string PictureUri = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    const string ShapeUri = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";

    /// <summary>How high a new caption is taken to be until the layout has it (see FitFigure), in points.</summary>
    const double CaptionEstimate = 16;

    /// <summary>
    /// A figure as its paragraph holds it: its text box (wp:inline or wp:anchor), its picture (wp:inline) in the first of the box's
    /// paragraphs, the caption's paragraphs after it, and all of them.
    /// </summary>
    sealed record Figure(XElement Box, XElement Picture, ParagraphBlock Holder, IReadOnlyList<ParagraphBlock> Caption, List<Block> Text);

    /// <summary>
    /// The figure whose character stands at a place of a paragraph's text (see the class): a text box whose first paragraph holds a
    /// picture alone and the others its caption; null where none stands there.
    /// </summary>
    Figure? FigureAt(int paragraph, int offset)
    {
        if (Found(paragraph) is not var (p, _))
            return null;
        var at = p.TextOffset;
        foreach (var inline in p.Content)
        {
            var length = ParagraphText.Length(inline);
            if (at == offset && length > 0)
            {
                var (own, text) = inline switch { DrawingInline d => (d.Drawing, d.Text), AnchorInline a => (a.Anchor, a.Text), _ => (null, null) };
                return own != null && text is [ParagraphBlock holder, _, ..] && text.All(b => b is ParagraphBlock) && GraphicUri(own) == ShapeUri
                    && ParagraphText.Of(holder) == "￼" && holder.Content.OfType<DrawingInline>().FirstOrDefault() is { } picture
                    && GraphicUri(picture.Drawing) == PictureUri
                    ? new Figure(own, picture.Drawing, holder, [.. text.Skip(1).Cast<ParagraphBlock>()], text) : null;
            }
            at += length;
        }
        return null;
    }

    /// <summary>What a drawing's graphic is (a:graphicData uri): a picture, a shape (a text box), a chart …</summary>
    static string? GraphicUri(XElement drawing) => (string?)drawing.Element(A + "graphic")?.Element(A + "graphicData")?.Attribute("uri");

    /// <summary>The first paragraph of the caption of the picture at a place of a paragraph's text (see AddCaption), or null where it has none.</summary>
    public int? CaptionOf(int paragraph, int offset) => FigureAt(paragraph, offset)?.Caption[0].Id;

    /// <summary>
    /// The figure a paragraph stands in (see AddCaption): the place of its character, and whether the paragraph is the one that holds its
    /// picture rather than one of its caption's; null for a paragraph of none.
    /// </summary>
    public (int Paragraph, int Offset, bool Picture)? FigureOf(int paragraph) =>
        DrawingAround(paragraph) is var (p, o) && FigureAt(p, o) is { } figure ? (p, o, figure.Holder.Id == paragraph) : null;

    /// <summary>
    /// Gives the picture at a place of a paragraph's text a caption, one change: the picture and below it an empty paragraph of Word's
    /// caption style (defined as Word has it where the document has none) in a figure that stands where the picture stood, as it stood
    /// (see the class). Returns the caption's paragraph.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// No picture stands at the place, or it stands in a text box (Word holds none in another); nothing changes.
    /// </exception>
    public int AddCaption(int paragraph, int offset)
    {
        if (DrawingAt(Find(paragraph).Paragraph, offset) is not { } picture || GraphicUri(picture) != PictureUri)
            throw new NotSupportedException("No picture stands at that place.");
        if (DrawingAround(paragraph) != null)
            throw new NotSupportedException("A picture in a text box takes no caption: Word holds no text box in another.");
        var start = version;
        try
        {
            var style = context.Styles.IdByName("caption", "paragraph");
            if (style == null)
            {
                DefineStyles(CaptionStyle(context.Styles.DefaultParagraphStyle));
                style = "Caption";
            }
            var (p, top) = Find(paragraph);
            var figure = Figured(DrawingAt(p, offset)!, style);
            Change(top, top, (stretch, _) => TextEdits.Redraw(stretch, p, offset, figure));
            var caption = CaptionOf(paragraph, offset) ?? throw new NotSupportedException("The figure does not read as one.");
            FitFigure(paragraph, offset);
            Combine(version - start);
            return caption;
        }
        catch
        {
            Back(start);
            throw;
        }
    }

    /// <summary>
    /// Word's caption style as a document without one takes it (Word's own look: small, italic, in the theme's second text color),
    /// without the space after it that would leave a figure empty below its caption; a caption's next paragraph is of it too.
    /// </summary>
    static string CaptionStyle(string? normal) =>
        "<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"
        + "<w:style w:type=\"paragraph\" w:styleId=\"Caption\"><w:name w:val=\"caption\"/>"
        + (normal != null ? $"<w:basedOn w:val=\"{System.Security.SecurityElement.Escape(normal)}\"/>" : "")
        + "<w:next w:val=\"Caption\"/><w:uiPriority w:val=\"35\"/><w:unhideWhenUsed/><w:qFormat/>"
        + "<w:pPr><w:spacing w:before=\"80\" w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr>"
        + "<w:rPr><w:i/><w:iCs/><w:color w:val=\"44546A\" w:themeColor=\"text2\"/><w:sz w:val=\"18\"/><w:szCs w:val=\"18\"/></w:rPr>"
        + "</w:style></w:styles>";

    /// <summary>
    /// A picture's drawing made a figure with an empty caption of a style (see the class): the box where the picture stood as it stood
    /// (its wrapping, its place, its name and description), the picture as it was (its size, its effects and the room they and its line
    /// take round it in its line, its name and description, an id of its own) in a line of its own in it, the box as wide as the picture
    /// and that room, the caption indented by it (Word shows of a text box only what lies within its text).
    /// </summary>
    XElement Figured(XElement own, string style)
    {
        var wp = own.Name.Namespace;
        var w = Wml.W;
        var wps = Drawings.Wps;
        var (width, height) = Drawings.Extent(own);
        var (l, t, r, b) = Room(own);
        var (cx, cy) = (Emu(width + l + r), Emu(height + t + b + CaptionEstimate));
        XElement None() => new(wp + "effectExtent", new XAttribute("l", 0), new XAttribute("t", 0), new XAttribute("r", 0), new XAttribute("b", 0));
        var docPr = own.Element(wp + "docPr") is { } named ? new XElement(named) : new XElement(wp + "docPr", new XAttribute("name", "Picture"));
        docPr.SetAttributeValue("id", NewDrawingId());
        var picture = new XElement(wp + "inline", new XAttribute("distT", 0), new XAttribute("distB", 0), new XAttribute("distL", 0), new XAttribute("distR", 0),
            new XElement(wp + "extent", new XAttribute("cx", Emu(width)), new XAttribute("cy", Emu(height))),
            own.Element(wp + "effectExtent") is { } effect ? new XElement(effect) : None(),
            docPr,
            own.Element(wp + "cNvGraphicFramePr") is { } locks ? new XElement(locks) : new XElement(wp + "cNvGraphicFramePr"),
            new XElement(own.Element(A + "graphic")!));
        // The picture's line as tight as Word sets one: no space round it, no indent, from the box's left.
        var holder = new XElement(w + "p",
            new XElement(w + "pPr",
                new XElement(w + "spacing", new XAttribute(w + "before", 0), new XAttribute(w + "after", 0), new XAttribute(w + "line", 240), new XAttribute(w + "lineRule", "auto")),
                new XElement(w + "ind", new XAttribute(w + "left", 0), new XAttribute(w + "right", 0), new XAttribute(w + "firstLine", 0)),
                new XElement(w + "jc", new XAttribute(w + "val", "left"))),
            new XElement(w + "r", new XElement(w + "drawing", picture)));
        var caption = new XElement(w + "p", new XElement(w + "pPr", new XElement(w + "pStyle", new XAttribute(w + "val", style)),
            l > 0 || r > 0 ? new XElement(w + "ind", new XAttribute(w + "left", Twips(l)), new XAttribute(w + "right", Twips(r))) : null));
        var box = new XElement(own);
        box.Element(wp + "extent")?.ReplaceWith(new XElement(wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)));
        box.Element(wp + "effectExtent")?.ReplaceWith(None());
        box.Element(wp + "cNvGraphicFramePr")?.ReplaceWith(new XElement(wp + "cNvGraphicFramePr"));
        // The namespaces the box's graphic takes, declared on it where the part's root does not (see PartText).
        box.Element(A + "graphic")!.ReplaceWith(new XElement(A + "graphic",
            new XAttribute(XNamespace.Xmlns + "a", A), new XAttribute(XNamespace.Xmlns + "wps", wps), new XAttribute(XNamespace.Xmlns + "pic", Drawings.Pic),
            new XAttribute(XNamespace.Xmlns + "r", Wml.R),
            new XElement(A + "graphicData", new XAttribute("uri", ShapeUri),
                new XElement(wps + "wsp",
                    new XElement(wps + "cNvSpPr", new XAttribute("txBox", 1)),
                    new XElement(wps + "spPr",
                        new XElement(A + "xfrm", new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(A + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))),
                        new XElement(A + "prstGeom", new XAttribute("prst", "rect"), new XElement(A + "avLst")),
                        new XElement(A + "noFill"),
                        new XElement(A + "ln", new XElement(A + "noFill"))),
                    new XElement(wps + "txbx", new XElement(w + "txbxContent", holder, caption)),
                    new XElement(wps + "bodyPr", new XAttribute("rot", 0), new XAttribute("vert", "horz"), new XAttribute("wrap", "square"),
                        new XAttribute("lIns", 0), new XAttribute("tIns", 0), new XAttribute("rIns", 0), new XAttribute("bIns", 0),
                        new XAttribute("anchor", "t"), new XAttribute("anchorCtr", 0), new XElement(A + "spAutoFit"))))));
        return box;
    }

    /// <summary>
    /// Takes the caption of the picture at a place of a paragraph's text away, one change: the picture alone stands where its figure
    /// stood, as it stood (in the line or floating, wrapped and placed so), with the figure's name and description.
    /// </summary>
    /// <exception cref="NotSupportedException">No picture with a caption stands at the place; nothing changes.</exception>
    public void RemoveCaption(int paragraph, int offset)
    {
        if (FigureAt(paragraph, offset) is not { } figure)
            throw new NotSupportedException("No picture with a caption stands at that place.");
        var (box, picture) = (figure.Box, figure.Picture);
        var wp = box.Name.Namespace;
        var alone = new XElement(box);
        alone.Element(wp + "extent")?.ReplaceWith(new XElement(picture.Element(wp + "extent")!));
        if (picture.Element(wp + "effectExtent") is { } effect)
            alone.Element(wp + "effectExtent")?.ReplaceWith(new XElement(effect));
        if (picture.Element(wp + "cNvGraphicFramePr") is { } locks)
            alone.Element(wp + "cNvGraphicFramePr")?.ReplaceWith(new XElement(locks));
        alone.Element(A + "graphic")!.ReplaceWith(new XElement(picture.Element(A + "graphic")!));
        var (p, top) = Find(paragraph);
        Change(top, top, (stretch, _) => TextEdits.Redraw(stretch, p, offset, alone));
    }

    /// <summary>
    /// The drawing whose text box holds a paragraph (see IEditableDocument.DrawingAround): the paragraph and the place of its character, the
    /// nearest around it; null for a paragraph of none.
    /// </summary>
    public (int Paragraph, int Offset)? DrawingAround(int paragraph)
    {
        if (Found(paragraph) is not var (own, top) || ReferenceEquals(own, top))
            return null;
        // Through the text of the drawings and the cells of the tables the paragraph's block holds, the drawing nearest around it.
        (int, int)? Search(Block block, (int, int)? around)
        {
            if (ReferenceEquals(block, own))
                return around;
            if (block is ParagraphBlock host)
            {
                var at = host.TextOffset;
                foreach (var inline in host.Content)
                {
                    var (text, children) = inline switch { DrawingInline d => (d.Text, d.ChildText), AnchorInline a => (a.Text, a.ChildText), _ => (null, null) };
                    foreach (var list in (children ?? []).Select(c => c.Text).Prepend(text ?? []))
                    {
                        foreach (var inner in list)
                        {
                            if (Search(inner, (host.Id, at)) is { } found)
                                return found;
                        }
                    }
                    at += ParagraphText.Length(inline);
                }
                return null;
            }
            foreach (var list in ModelReader.Inner(block))
            {
                foreach (var inner in list)
                {
                    if (Search(inner, around) is { } found)
                        return found;
                }
            }
            return null;
        }
        return Search(top, null);
    }

    /// <summary>
    /// The figure at a place of a paragraph's text as high as what it holds (see the class), a change where the height it is saved with is
    /// another: Word fits it so as it lays it out, but a figure in the line takes the height saved until then, here too.
    /// </summary>
    public void FitFigure(int paragraph, int offset)
    {
        if (FigureAt(paragraph, offset) is not { } figure)
            return;
        var (width, height) = Drawings.Extent(figure.Box);
        if (Fitted(figure, width) is { } fitted && Math.Abs(fitted - height) >= 0.05)
            Format(paragraph, offset, new DrawingFormatting { Width = width, Height = fitted });
    }

    /// <summary>The figures of the body as high as what they hold (see FitFigure), after a change of how their captions look: a change for each that is not.</summary>
    void FitFigures()
    {
        foreach (var (paragraph, _) in paragraphs.Values.ToList())
        {
            var at = paragraph.TextOffset;
            foreach (var inline in paragraph.Content)
            {
                if (inline is DrawingInline { Text.Count: > 1 } or AnchorInline { Text.Count: > 1 })
                    FitFigure(paragraph.Id, at);
                at += ParagraphText.Length(inline);
            }
        }
    }

    /// <summary>How high a figure is as wide as given, holding its text (see Paginator.TextHeight) between its insets; null before the document is laid out.</summary>
    double? Fitted(Figure figure, double width)
    {
        if (paginator == null || shown.Count == 0)
            Scenes();
        if (paginator == null || shown.Count == 0)
            return null;
        var shape = figure.Box.Element(A + "graphic")?.Element(A + "graphicData")?.Element(Drawings.Wps + "wsp");
        var (l, t, r, b) = TextBoxLayout.Insets(shape?.Element(Drawings.Wps + "bodyPr"), shape, width, Drawings.Extent(figure.Box).Height, context.Theme);
        return paginator.TextHeight(figure.Text, width - l - r, shown[0]) + t + b;
    }

    /// <summary>
    /// Gives the picture at a place of a paragraph's text a size, in points, one change: a figure's picture its own (see AddCaption), the
    /// figure as wide as it and as high as it then holds; another drawing that size. Its place stays.
    /// </summary>
    /// <exception cref="NotSupportedException">No drawing stands at the place; nothing changes.</exception>
    public void SizePicture(int paragraph, int offset, double width, double height)
    {
        var start = version;
        try
        {
            if (FigureAt(paragraph, offset) is { } figure)
                Sized(figure, paragraph, offset, width, height);
            else
                Format(paragraph, offset, new DrawingFormatting { Width = width, Height = height });
            Combine(version - start);
        }
        catch
        {
            Back(start);
            throw;
        }
    }

    /// <summary>
    /// A figure sized by its handles (see SizeDrawing): its picture as wide as given less the room round it (see Room) and as high as
    /// given less the caption; where both change (a corner's, which keeps the figure's proportions) as wide so, its own proportions kept. A
    /// change or a few.
    /// </summary>
    void SizedByHandles(Figure figure, int paragraph, int offset, double width, double height)
    {
        var (boxWidth, boxHeight) = Drawings.Extent(figure.Box);
        var (wide, high) = Drawings.Extent(figure.Picture);
        var (l, _, r, _) = Room(figure.Picture);
        var (across, down) = (Math.Abs(width - boxWidth) > 0.01, Math.Abs(height - boxHeight) > 0.01);
        var picture = Math.Max(1, width - l - r);
        var (w, h) = (across, down) switch
        {
            (true, true) when wide > 0 => (picture, picture * high / wide),
            (true, _) => (picture, high),
            (_, true) => (wide, Math.Max(1, height - Math.Max(0, boxHeight - high))),
            _ => (wide, high),
        };
        Sized(figure, paragraph, offset, w, h);
    }

    /// <summary>
    /// A figure's picture given a size (points), and the part of it shown where given (see CropPicture), the figure as wide as it and the
    /// room round it and as high as it then holds (see Refit): a change or two.
    /// </summary>
    void Sized(Figure figure, int paragraph, int offset, double width, double height, PictureCrop? crop = null)
    {
        Format(figure.Holder.Id, figure.Holder.TextOffset, new DrawingFormatting { Width = width, Height = height, Crop = crop });
        Refit(paragraph, offset, indent: false);
    }

    /// <summary>
    /// The figure at a place as wide as its picture and the room round it in its line (see Room), and as high as it then holds; with
    /// <paramref name="indent"/>, its caption's paragraphs indented by that room at both sides, so that they stand under the picture. A
    /// change or a few.
    /// </summary>
    void Refit(int paragraph, int offset, bool indent)
    {
        if (FigureAt(paragraph, offset) is not { } figure)
            return;
        var (l, _, r, _) = Room(figure.Picture);
        if (indent)
        {
            foreach (var caption in figure.Caption)
                Format(caption.Id, new ParagraphFormatting { Left = l, Right = r });
        }
        if (FigureAt(paragraph, offset) is not { } now)
            return;
        var width = Drawings.Extent(now.Picture).Width + l + r;
        var fitted = Fitted(now, width) ?? Drawings.Extent(now.Box).Height;
        Format(paragraph, offset, new DrawingFormatting { Width = width, Height = fitted });
    }

    /// <summary>The room round a drawing in its line (its wp:effectExtent: the left, the top, the right and the bottom), in points.</summary>
    static (double Left, double Top, double Right, double Bottom) Room(XElement drawing)
    {
        var effect = drawing.Element(drawing.Name.Namespace + "effectExtent");
        double Side(string name) => Points((string?)effect?.Attribute(name)) ?? 0;
        return (Side("l"), Side("t"), Side("r"), Side("b"));
    }

    static string Twips(double points) => ((long)Math.Round(points * 20)).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The wp:inline of a figure's picture in the graphic of the figure's box (see the class), as its XML has it: the only drawing of the
    /// first of two paragraphs or more, a picture; null for other graphics.
    /// </summary>
    static XElement? FigurePicture(XElement? graphic) =>
        graphic?.Element(A + "graphicData") is { } data && (string?)data.Attribute("uri") == ShapeUri
        && data.Element(Drawings.Wps + "wsp")?.Element(Drawings.Wps + "txbx")?.Elements().FirstOrDefault(e => e.Name.LocalName == "txbxContent") is { } content
        && content.Elements().Where(e => e.Name.LocalName == "p").ToList() is [var first, _, ..]
        && first.Descendants().Where(e => e.Name.LocalName == "drawing").ToList() is [var drawing]
        && drawing.Elements().FirstOrDefault() is { Name.LocalName: "inline" } inline && GraphicUri(inline) == PictureUri ? inline : null;

}
