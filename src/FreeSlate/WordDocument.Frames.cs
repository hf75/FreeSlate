using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Docx.Layout;
using FreeSlate.Opc;
using FreeSlate.Rendering;

namespace FreeSlate;

/// <summary>
/// Tables in frames (catalog: drawing.textBoxes): a table of the body moved into a text box where it stood, so that it stands as a
/// picture does (in front of the text or behind it, turned, tilted in space, placed anywhere), and back into the text. The frame is a
/// text box without line, fill or insets, as wide as the table and as high as it and the paragraph a text box keeps after a table
/// (1 pt high, see Ending), fitted to what it holds (a:spAutoFit, the height saved kept right by FitFrame, as Word shows a text box in
/// the line with the height saved); anchored in the paragraph after the table, across from the page where the table stood, down from
/// that paragraph's top, the text above and below it as the table had it (wrap top and bottom) unless given otherwise.
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>A frame as its paragraph holds it: its text box (wp:inline or wp:anchor), its table and its text (the table, a paragraph).</summary>
    sealed record FrameModel(XElement Box, TableBlock Table, List<Block> Text);

    /// <summary>
    /// The frame whose character stands at a place of a paragraph's text (see the class): a text box that holds a table and one empty
    /// paragraph after it; null where none stands there.
    /// </summary>
    FrameModel? FrameAt(int paragraph, int offset)
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
                return own != null && text is [TableBlock table, ParagraphBlock { Shows: false }] && GraphicUri(own) == ShapeUri ? new FrameModel(own, table, text) : null;
            }
            at += length;
        }
        return null;
    }

    /// <summary>Whether a frame (see the class) stands at a place of a paragraph's text.</summary>
    public bool IsFrame(int paragraph, int offset) => FrameAt(paragraph, offset) != null;

    /// <summary>The table of the frame at a place (see the class): its block's id; null where no frame stands there.</summary>
    public int? FramedTable(int paragraph, int offset) => FrameAt(paragraph, offset)?.Table.Id;

    /// <summary>The frame a paragraph of its table stands in (see the class): the place of its character; null for a paragraph of none.</summary>
    public (int Paragraph, int Offset)? FrameOf(int paragraph) =>
        DrawingAround(paragraph) is var (p, o) && FrameAt(p, o) != null ? (p, o) : null;

    /// <summary>
    /// Moves a table of the body into a frame where it stood (see the class), one change; the frame then stands as given (see
    /// DrawingFormatting: in front of the text or behind it, turned, tilted …), floating with the text above and below it where nothing
    /// else is given. The frame stands in a paragraph of its own before the one that followed the table (see Holder), where the frames
    /// it goes on in on the next pages join it (see FitFrame). Returns the place of the frame's character.
    /// </summary>
    /// <exception cref="NotSupportedException">The table stands in a table, no paragraph follows it, or changes are tracked; nothing changes.</exception>
    public (int Paragraph, int Offset) FrameTable(int table, DrawingFormatting? format = null)
    {
        Read();
        var (model, top) = TableById(table);
        if (!ReferenceEquals(model, top))
            throw new NotSupportedException("A table in a table stays in its cell.");
        if (Author != null)
            throw new NotSupportedException("A table is not framed as a tracked change.");
        var index = IndexOf(top);
        if (index + 1 >= blocks.Count || blocks[index + 1] is not ParagraphBlock after)
            throw new NotSupportedException("No paragraph follows the table.");
        // Where the table stands on its first page: the left of its grid's first line and its width over the lines, points.
        var placed = PlacedRows(table);
        var start = version;
        try
        {
            var (left, width) = placed is [var first, ..] ? (first.Lines.Min() - FrameSlack / 2, first.Lines.Max() - first.Lines.Min() + FrameSlack)
                : ((double?)null, TableById(table).Table.Table.Grid.Sum() + FrameSlack);
            var holder = InsertBlocks(after.Id, Holder(FrameMarkup(model, width, Math.Max(12, placed.Sum(r => r.Bottom - r.Top))), pageBreak: false), before: true)[0];
            RemoveTable(table);
            // Floating where the table stood, the text above and below it, unless given otherwise; as high as its table, or going on in
            // frames on the pages after (see FitFrame).
            var floating = new DrawingFormatting { Wrap = "topAndBottom", VerticalFrom = "paragraph", VerticalOffset = 0 };
            if (left is { } x)
                floating = floating with { HorizontalFrom = "page", HorizontalOffset = x };
            Format(holder, 0, Merged(floating, format));
            FitFrame(holder, 0);
            Combine(version - start);
            return (holder, 0);
        }
        catch
        {
            Back(start);
            throw;
        }
    }

    /// <summary>
    /// Moves the table of the frame at a place back into the text where the frame stands, and takes the frame away, with the frames it goes
    /// on in and the paragraphs of their own that hold them (see Holder); one change. Returns the table's id.
    /// </summary>
    /// <exception cref="NotSupportedException">No frame stands at the place, or changes are tracked; nothing changes.</exception>
    public int UnframeTable(int paragraph, int offset)
    {
        if (FrameAt(paragraph, offset) is not { } frame)
            throw new NotSupportedException("No frame stands at that place.");
        if (Author != null)
            throw new NotSupportedException("A table is not unframed as a tracked change.");
        if (DrawingAround(paragraph) != null)
            throw new NotSupportedException("A frame in a text box stays there.");
        var start = version;
        try
        {
            var markup = PartText.Xml(frame.Table.Element);
            Unchain(paragraph, offset);
            var own = IsHolder(Find(paragraph).Paragraph);
            if (!own)
                Delete(paragraph, offset, 1);
            var made = InsertBlocks(paragraph, markup, before: true);
            if (own)
                RemoveParagraphs([paragraph]);
            Combine(version - start);
            return made.Count > 0 && TableOf(made[0]) is { } table ? table : throw new InvalidOperationException("The table did not come back into the text.");
        }
        catch
        {
            Back(start);
            throw;
        }
    }

    /// <summary>
    /// The frame at a place as high as what it holds at its width (see Paginator.TextHeight), a change where the height saved is not, as
    /// Word shows a frame in the line, and for a document opened elsewhere. A frame in a paragraph of its own (see Holder) with the text
    /// above and below it, straight on and turned in quarters only, that reaches past the page's text goes on in frames on the pages after
    /// (linked text boxes, see TextBoxLayout.Lay), each in a paragraph of its own that begins a page, as much as there is room for: down
    /// to the text's bottom from the first frame's top and from the text's top on the pages after, across the text's width where the frames
    /// are turned a quarter; the first and those between in front of the text, which nothing goes round on their pages, the last with the
    /// text above and below it, the text after the table below it. The frames are made anew where their number changes.
    /// </summary>
    public void FitFrame(int paragraph, int offset)
    {
        if (FrameAt(paragraph, offset) is not { } frame)
            return;
        Scenes();
        if (paginator == null || shown.Count == 0)
            return;
        var (width, height) = Drawings.Extent(frame.Box);
        var now = Read(frame.Box);
        var chain = ChainOf(paragraph, offset);
        var turn = now.Rotation ?? 0;
        var quarter = Math.Abs(Math.IEEERemainder(turn, 90)) < 0.01;
        var across = Math.Abs(Math.IEEERemainder(turn - 90, 180)) < 0.01;
        var (page, placed) = PlacedFrame(frame.Box);
        var flows = now.Wrap == "topAndBottom" || chain.Count > 0 && now.Wrap == "front";
        var parts = flows && quarter && now.Tilt is not { IsFlat: false } && placed != null && page != null && IsHolder(Find(paragraph).Paragraph)
            ? paginator.TextParts(frame.Text, width, across ? page.TextWidth : page.BodyBottom - placed.Y, across ? page.TextWidth : page.BodyBottom - page.BodyTop, page)
            : [paginator.TextHeight(frame.Text, width, page ?? shown[0])];
        // The same number of frames: their heights as the parts' where they differ.
        if (parts.Count == chain.Count + 1)
        {
            if (Math.Abs(parts[0] - height) >= 0.05)
                Format(paragraph, offset, new DrawingFormatting { Width = width, Height = parts[0] });
            for (var k = 0; k < chain.Count; k++)
            {
                if (Math.Abs(parts[k + 1] - Drawings.Extent(chain[k].Box).Height) >= 0.05)
                    Format(chain[k].Paragraph, 0, new DrawingFormatting { Width = width, Height = parts[k + 1] });
            }
            return;
        }
        Unchain(paragraph, offset);
        if (parts.Count == 1)
        {
            Format(paragraph, offset, new DrawingFormatting { Width = width, Height = parts[0], Wrap = chain.Count > 0 ? "topAndBottom" : null });
            return;
        }
        // The first frame in front of the text, as high as its part, fitted no more to all it holds, its story named for the others.
        var story = NewDrawingId();
        Format(paragraph, offset, new DrawingFormatting { Width = width, Height = parts[0], Wrap = "front" });
        Linked(paragraph, offset, story);
        // The others each at the top of a page of their own, turned as the first, their boxes where their turn shows them as the first's
        // shows it across (its left) and at the text's top.
        var x = placed!.X;
        var made = InsertBlocks(paragraph, string.Concat(parts.Skip(1).Select((part, k) => Holder(LinkedMarkup(width, part, story, k + 1, turn), pageBreak: true))));
        for (var k = 0; k < made.Count; k++)
        {
            var part = parts[k + 1];
            var shift = across ? (part - parts[0]) / 2 : 0;
            Format(made[k], 0, new DrawingFormatting
            {
                Wrap = k == made.Count - 1 ? "topAndBottom" : "front",
                HorizontalFrom = "page",
                HorizontalOffset = x + shift,
                VerticalFrom = "margin",
                VerticalOffset = across ? (width - part) / 2 : 0,
                Rotation = turn,
            });
        }
    }

    /// <summary>The page a frame stands on and its place there (see the class), as the layout last put it; nulls before it is laid out.</summary>
    (PageBox? Page, PlacedDrawing? Placed) PlacedFrame(XElement box)
    {
        foreach (var page in shown)
        {
            if (page.Drawings.FirstOrDefault(d => ReferenceEquals(d.Drawing, box)) is { } placed)
                return (page, placed);
        }
        return (shown.Count > 0 ? shown[0] : null, null);
    }

    /// <summary>
    /// The frames a frame goes on in (see FitFrame): their paragraphs (each holding one alone, see Holder) right after the frame's own and
    /// their boxes, in their order; none where it goes on in none.
    /// </summary>
    List<(int Paragraph, XElement Box)> ChainOf(int paragraph, int offset)
    {
        var chain = new List<(int, XElement)>();
        if (FrameAt(paragraph, offset) is not { } frame
            || (string?)frame.Box.Descendants(Drawings.Wps + "txbx").FirstOrDefault()?.Attribute("id") is not { } story)
            return chain;
        var (own, _) = Find(paragraph);
        for (var k = IndexOf(own) + 1; k < blocks.Count; k++)
        {
            if (blocks[k] is not ParagraphBlock next || !IsHolder(next)
                || next.Content.OfType<AnchorInline>().FirstOrDefault() is not { } linked
                || (string?)linked.Anchor.Descendants(Drawings.Wps + "linkedTxbx").FirstOrDefault()?.Attribute("id") != story)
                break;
            chain.Add((next.Id, linked.Anchor));
        }
        return chain;
    }

    /// <summary>The frames a frame goes on in taken away with their paragraphs (see ChainOf), and the frame's story no longer named.</summary>
    void Unchain(int paragraph, int offset)
    {
        var chain = ChainOf(paragraph, offset);
        if (chain.Count > 0)
            RemoveParagraphs([.. chain.Select(c => c.Paragraph)]);
        Linked(paragraph, offset, null);
    }

    /// <summary>
    /// The frame at a place made the first of linked frames, its story named (wps:txbx id) and no longer fitted to all of it (no
    /// a:spAutoFit); or (<paramref name="story"/> null) back to a frame alone, fitted to its table.
    /// </summary>
    void Linked(int paragraph, int offset, int? story)
    {
        if (FrameAt(paragraph, offset) is not { } frame || frame.Box.Descendants(Drawings.Wps + "txbx").FirstOrDefault() is not { } text
            || frame.Box.Descendants(Drawings.Wps + "bodyPr").FirstOrDefault() is not { } body)
            return;
        var named = (string?)text.Attribute("id");
        if (named == story?.ToString(System.Globalization.CultureInfo.InvariantCulture) && (body.Element(A + "spAutoFit") == null) == (story != null))
            return;
        var box = new XElement(frame.Box);
        box.Descendants(Drawings.Wps + "txbx").First().SetAttributeValue("id", story);
        var fit = box.Descendants(Drawings.Wps + "bodyPr").First();
        fit.Elements(A + "spAutoFit").Remove();
        if (story == null)
            fit.Add(new XElement(A + "spAutoFit"));
        var (p, top) = Find(paragraph);
        Change(top, top, (stretch, _) => TextEdits.Redraw(stretch, p, offset, box));
    }

    /// <summary>Paragraphs of the body taken away whole, one change (those that hold frames, see Holder).</summary>
    void RemoveParagraphs(IReadOnlyList<int> ids)
    {
        var gone = ids.Select(id => Find(id).Paragraph).OrderBy(p => IndexOf(p)).ToList();
        Change(gone[0], gone[^1], (stretch, _) => stretch.Written(gone.Select(p => (stretch.Find(p.Element)
            ?? throw new NotSupportedException("The paragraph is not where it was read."), Enumerable.Empty<XElement>()))),
            () => [.. blocks.Where(b => !gone.Any(g => ReferenceEquals(g, b)))]);
    }

    /// <summary>
    /// A paragraph of its own that holds a frame (see the class): no more than that run, 1 pt high, no space round it; beginning a page
    /// where it holds a frame a table goes on in.
    /// </summary>
    static string Holder(string run, bool pageBreak) =>
        "<w:p><w:pPr>" + (pageBreak ? "<w:pageBreakBefore/>" : "")
        + "<w:spacing w:before=\"0\" w:after=\"0\" w:line=\"20\" w:lineRule=\"exact\"/><w:rPr><w:sz w:val=\"2\"/><w:szCs w:val=\"2\"/></w:rPr></w:pPr>"
        + run + "</w:p>";

    /// <summary>Whether a paragraph is one of its own that holds a frame (see Holder): drawings alone, 1 pt high.</summary>
    static bool IsHolder(ParagraphBlock paragraph)
    {
        var w = paragraph.Element.Name.Namespace;
        var spacing = paragraph.Element.Element(w + "pPr")?.Element(w + "spacing");
        return (string?)spacing?.Attribute(w + "line") == "20" && (string?)spacing?.Attribute(w + "lineRule") == "exact"
            && paragraph.Content.Any(i => i is AnchorInline or DrawingInline)
            && paragraph.Content.All(i => i is AnchorInline or DrawingInline or BookmarkInline or BookmarkEndInline);
    }

    /// <summary>
    /// A run with a frame in the line that goes on with a story of linked frames (wps:linkedTxbx, see FitFrame): as wide as given, as
    /// high as its part, turned as given, without line, fill or insets.
    /// </summary>
    string LinkedMarkup(double width, double height, int story, int seq, double turn)
    {
        var (cx, cy) = (Emu(width), Emu(height));
        var rot = Math.Abs(turn) > 0.001 ? $" rot=\"{(long)Math.Round(turn * 60000)}\"" : "";
        return "<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\">"
            + $"<wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/><wp:docPr id=\"{NewDrawingId()}\" name=\"Table frame {seq + 1}\"/><wp:cNvGraphicFramePr/>"
            + $"<a:graphic><a:graphicData uri=\"{ShapeUri}\"><wps:wsp><wps:cNvSpPr txBox=\"1\"/>"
            + $"<wps:spPr><a:xfrm{rot}><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/><a:ln><a:noFill/></a:ln></wps:spPr>"
            + $"<wps:linkedTxbx id=\"{story}\" seq=\"{seq}\"/>"
            + "<wps:bodyPr rot=\"0\" vert=\"horz\" wrap=\"square\" lIns=\"0\" tIns=\"0\" rIns=\"0\" bIns=\"0\" anchor=\"t\" anchorCtr=\"0\"/>"
            + "</wps:wsp></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
    }

    /// <summary>How much wider a frame is than its table's grid over the lines' middles: room for the halves of its outer lines.</summary>
    const double FrameSlack = 3;

    /// <summary>The rows of a table of the body the pages place, in their order (see TableRowItem); empty where none is laid out yet.</summary>
    List<TableRowItem> PlacedRows(int table)
    {
        var rows = new List<TableRowItem>();
        var count = Scenes().Count;
        for (var k = 0; k < count && rows.Count == 0; k++)
            rows.AddRange(Scene(k).Items.OfType<TableRowItem>().Where(r => r.Table == table && !r.Repeated));
        return rows;
    }

    /// <summary>A format with another's properties over it where that gives them.</summary>
    static DrawingFormatting Merged(DrawingFormatting under, DrawingFormatting? over) => over == null ? under : under with
    {
        Wrap = over.Wrap ?? under.Wrap,
        Side = over.Side ?? under.Side,
        HorizontalFrom = over.HorizontalFrom ?? under.HorizontalFrom,
        HorizontalAlign = over.HorizontalAlign ?? under.HorizontalAlign,
        HorizontalOffset = over.HorizontalAlign != null ? null : over.HorizontalOffset ?? under.HorizontalOffset,
        VerticalFrom = over.VerticalFrom ?? under.VerticalFrom,
        VerticalAlign = over.VerticalAlign ?? under.VerticalAlign,
        VerticalOffset = over.VerticalAlign != null ? null : over.VerticalOffset ?? under.VerticalOffset,
        Rotation = over.Rotation ?? under.Rotation,
        Tilt = over.Tilt ?? under.Tilt,
    };

    /// <summary>
    /// A run with a frame (see the class) in the line holding a copy of a table, as wide as given and as high as a first guess; the table
    /// without an indent, alignment or floating place of its own (the frame stands for them), after it a paragraph 1 pt high.
    /// </summary>
    string FrameMarkup(TableBlock table, double width, double height)
    {
        var w = table.Element.Name.Namespace;
        var copy = new XElement(table.Element);
        if (copy.Element(w + "tblPr") is { } properties)
            properties.Elements().Where(e => e.Name == w + "tblInd" || e.Name == w + "jc" || e.Name == w + "tblpPr" || e.Name == w + "tblOverlap").Remove();
        var ending = new XElement(w + "p", new XElement(w + "pPr",
            new XElement(w + "spacing", new XAttribute(w + "before", 0), new XAttribute(w + "after", 0), new XAttribute(w + "line", 20), new XAttribute(w + "lineRule", "exact")),
            new XElement(w + "rPr", new XElement(w + "sz", new XAttribute(w + "val", 2)), new XElement(w + "szCs", new XAttribute(w + "val", 2)))));
        var (cx, cy) = (Emu(width), Emu(height));
        return "<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\">"
            + $"<wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/><wp:docPr id=\"{NewDrawingId()}\" name=\"Table frame\"/><wp:cNvGraphicFramePr/>"
            + $"<a:graphic><a:graphicData uri=\"{ShapeUri}\"><wps:wsp><wps:cNvSpPr txBox=\"1\"/>"
            + $"<wps:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/><a:ln><a:noFill/></a:ln></wps:spPr>"
            + "<wps:txbx><w:txbxContent>" + Written(copy, table.Element) + Written(ending, table.Element) + "</w:txbxContent></wps:txbx>"
            + "<wps:bodyPr rot=\"0\" vert=\"horz\" wrap=\"square\" lIns=\"0\" tIns=\"0\" rIns=\"0\" bIns=\"0\" anchor=\"t\" anchorCtr=\"0\"><a:spAutoFit/></wps:bodyPr>"
            + "</wps:wsp></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
    }

    /// <summary>
    /// An element made apart from a part's XML written with the prefixes the part's root declares (and w:, r: where it does not), the
    /// declarations left out: markup for InsertMarkup and InsertBlocks, which declare them round it.
    /// </summary>
    static string Written(XElement made, XElement within)
    {
        var copy = new XElement(made);
        var declared = (within.Document?.Root?.Attributes() ?? []).Where(a => a.IsNamespaceDeclaration).ToList();
        if (declared.All(a => a.Value != copy.Name.NamespaceName))
            declared.Add(new XAttribute(XNamespace.Xmlns + "w", copy.Name.NamespaceName));
        if (declared.All(a => a.Value != Ns.R.NamespaceName))
            declared.Add(new XAttribute(XNamespace.Xmlns + "r", Ns.R.NamespaceName));
        foreach (var declaration in declared)
            copy.SetAttributeValue(declaration.Name, declaration.Value);
        var text = copy.ToString(SaveOptions.DisableFormatting);
        foreach (var declaration in copy.Attributes().Where(a => a.IsNamespaceDeclaration).ToList())
            text = text.Replace(" " + declaration, "");
        return text;
    }
}
