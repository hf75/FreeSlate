using System.Globalization;
using System.Security;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using FreeSlate.Skia;
using SkiaSharp;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// What blocks of the notation (see Notation) are in a Word document: WordprocessingML of its own kind, with the parts a picture
/// brings, where the marks of footnotes stand, and what a section of columns needs besides (see Made). The styles are the tone's (see
/// Writing.Tones): levels and meanings by their ids, tables in its table look.
/// </summary>
static class Markup
{
    /// <summary>
    /// The place the blocks go to: the width of the text there (points), the page setup of its section, and a new table there of so many
    /// columns and rows in the tone's look as Word's Insert Table makes it (see WordDocument.TableMarkup).
    /// </summary>
    public sealed record Place(double TextWidth, SectionFormatting Section, Func<int, int, string> EmptyTable);

    /// <summary>
    /// A block made: its markup (blocks, or nothing where it makes none), the parts it brings, the footnotes whose marks it holds (the
    /// paragraph by its place among the paragraphs it brings, the character offset there, the label), and what is to be done with it
    /// once it is in: a drawing in its first paragraph to float beside the text on a side (left, right), the section after it to go on
    /// on the same page (a section of columns ended).
    /// </summary>
    public sealed record Made(string Blocks, IReadOnlyList<NewPart> Parts, IReadOnlyList<(int Paragraph, int Offset, string Label)> Notes,
        string? Float = null, bool GoOn = false, string? Problem = null);

    const double EmuPerPoint = 12700;

    /// <summary>The namespaces markup given as it is may name by their prefixes, as the document takes them (see WordDocument.InsertMarkup).</summary>
    static readonly (string Prefix, string Uri)[] Namespaces =
    [
        ("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main"),
        ("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"),
        ("m", "http://schemas.openxmlformats.org/officeDocument/2006/math"),
        ("wp", "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"),
        ("a", "http://schemas.openxmlformats.org/drawingml/2006/main"),
        ("pic", "http://schemas.openxmlformats.org/drawingml/2006/picture"),
        ("c", "http://schemas.openxmlformats.org/drawingml/2006/chart"),
        ("wps", "http://schemas.microsoft.com/office/word/2010/wordprocessingShape"),
        ("wpg", "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup"),
        ("wpc", "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas"),
        ("wp14", "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing"),
        ("w14", "http://schemas.microsoft.com/office/word/2010/wordml"),
        ("w15", "http://schemas.microsoft.com/office/word/2012/wordml"),
        ("a14", "http://schemas.microsoft.com/office/drawing/2010/main"),
        ("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006"),
        ("v", "urn:schemas-microsoft-com:vml"),
        ("o", "urn:schemas-microsoft-com:office:office"),
    ];

    /// <summary>A block made at a place (see Made).</summary>
    public static Made Of(Notation.Block block, Place place) => block switch
    {
        Notation.Paragraph paragraph => Paragraph(paragraph.Style, paragraph.Pieces, null),
        Notation.Table table => Table(table, place),
        Notation.Fenced { Kind: "svg" } svg => Svg(svg, place),
        Notation.Fenced { Kind: "wordart" } art => WordArt(art, place),
        // Sections of columns are the writing's to make, which knows where they begin (see Writing.Put, SectionEnd).
        Notation.Columns or Notation.ColumnsEnd => new("", [], []),
        Notation.PageBreak => new("<w:p><w:r><w:br w:type=\"page\"/></w:r></w:p>", [], []),
        Notation.Rule => new("<w:p><w:pPr><w:pBdr><w:bottom w:val=\"single\" w:sz=\"6\" w:space=\"1\" w:color=\"auto\"/></w:pBdr></w:pPr></w:p>", [], []),
        Notation.Raw raw => Checked(raw.Markup) is { } problem ? new("", [], [], Problem: problem) : new(raw.Markup, [], []),
        _ => new("", [], []),
    };

    /// <summary>
    /// The end of a section of columns, given the columns it has: a paragraph that holds its page setup, of the section it ends, as small as
    /// Word draws a paragraph (a line of a point), so that it takes no room. Given no columns, the section before the columns ends so,
    /// as it was set up.
    /// </summary>
    public static string SectionEnd(SectionFormatting section, (int Count, bool Line)? columns)
    {
        static string Twips(double? points) => ((int)Math.Round((points ?? 0) * 20)).ToString(CultureInfo.InvariantCulture);
        var markup = new StringBuilder("<w:p><w:pPr><w:spacing w:before=\"0\" w:after=\"0\" w:line=\"20\" w:lineRule=\"exact\"/><w:rPr><w:sz w:val=\"2\"/><w:szCs w:val=\"2\"/></w:rPr><w:sectPr>");
        var start = columns != null ? "continuous" : section.Start ?? "nextPage";
        markup.Append("<w:type w:val=\"").Append(start).Append("\"/>");
        markup.Append("<w:pgSz w:w=\"").Append(Twips(section.PageWidth ?? 595.3)).Append("\" w:h=\"").Append(Twips(section.PageHeight ?? 841.9)).Append('"');
        if (section.Orientation == "landscape")
            markup.Append(" w:orient=\"landscape\"");
        markup.Append("/>");
        markup.Append("<w:pgMar w:top=\"").Append(Twips(section.Top ?? 72)).Append("\" w:right=\"").Append(Twips(section.Right ?? 72))
            .Append("\" w:bottom=\"").Append(Twips(section.Bottom ?? 72)).Append("\" w:left=\"").Append(Twips(section.Left ?? 72))
            .Append("\" w:header=\"").Append(Twips(section.Header ?? 35.4)).Append("\" w:footer=\"").Append(Twips(section.Footer ?? 35.4))
            .Append("\" w:gutter=\"").Append(Twips(section.Gutter ?? 0)).Append("\"/>");
        var (count, line) = columns ?? (section.Columns ?? 1, false);
        markup.Append("<w:cols w:space=\"").Append(Twips(section.ColumnSpace ?? 36)).Append('"');
        if (count > 1)
            markup.Append(" w:num=\"").Append(count.ToString(CultureInfo.InvariantCulture)).Append('"');
        if (line)
            markup.Append(" w:sep=\"1\"");
        markup.Append("/>");
        if (section.VerticalAlign is { } align && align != "top")
            markup.Append("<w:vAlign w:val=\"").Append(align).Append("\"/>");
        if (section.TitlePage == true)
            markup.Append("<w:titlePg/>");
        return markup.Append("</w:sectPr></w:pPr></w:p>").ToString();
    }

    /// <summary>A paragraph of a style, its pieces as runs; with an alignment where one is given (a cell's column, a picture's line).</summary>
    static Made Paragraph(string? style, IReadOnlyList<Notation.Piece> pieces, string? align, string? before = null)
    {
        var markup = new StringBuilder("<w:p>");
        if (style != null || align != null)
        {
            markup.Append("<w:pPr>");
            if (style != null)
                markup.Append("<w:pStyle w:val=\"").Append(Escape(style)).Append("\"/>");
            if (align != null)
                markup.Append("<w:jc w:val=\"").Append(align == "justify" ? "both" : align).Append("\"/>");
            markup.Append("</w:pPr>");
        }
        markup.Append(before);
        var notes = new List<(int, int, string)>();
        var offset = before != null ? 1 : 0;
        markup.Append(Runs(pieces, notes, 0, ref offset));
        return new(markup.Append("</w:p>").ToString(), [], notes);
    }

    /// <summary>The runs of a paragraph's pieces of text, to go in among other text, and where in them footnotes' marks stand (offset, label).</summary>
    public static (string Runs, IReadOnlyList<(int Offset, string Label)> Notes) RunsOf(IReadOnlyList<Notation.Piece> pieces)
    {
        var notes = new List<(int, int, string)>();
        var offset = 0;
        var runs = Runs(pieces, notes, 0, ref offset);
        return (runs, notes.Select(n => (n.Item2, n.Item3)).ToList());
    }

    /// <summary>The runs of pieces of text, counting the characters they make from an offset, the footnotes' marks noted where they stand.</summary>
    static string Runs(IReadOnlyList<Notation.Piece> pieces, List<(int, int, string)> notes, int paragraph, ref int offset)
    {
        var markup = new StringBuilder();
        foreach (var piece in pieces)
        {
            switch (piece)
            {
                case Notation.Run run:
                    foreach (var (text, tab) in Tabbed(Clean(run.Text)))
                    {
                        markup.Append("<w:r>");
                        if (run.Style != null)
                            markup.Append("<w:rPr><w:rStyle w:val=\"").Append(Escape(run.Style)).Append("\"/></w:rPr>");
                        markup.Append(tab ? "<w:tab/>" : $"<w:t xml:space=\"preserve\">{Escape(text)}</w:t>");
                        markup.Append("</w:r>");
                        offset += tab ? 1 : text.Length;
                    }
                    break;
                case Notation.Break:
                    markup.Append("<w:r><w:br/></w:r>");
                    offset++;
                    break;
                case Notation.NoteMark note:
                    notes.Add((paragraph, offset, note.Label));
                    break;
                case Notation.RawRun raw when Checked(raw.Markup) == null:
                    markup.Append(raw.Markup);
                    offset += TextLength(raw.Markup);
                    break;
            }
        }
        return markup.ToString();
    }

    /// <summary>A text's stretches between tabs, and the tabs.</summary>
    static IEnumerable<(string Text, bool Tab)> Tabbed(string text)
    {
        var parts = text.Split('\t');
        for (var k = 0; k < parts.Length; k++)
        {
            if (k > 0)
                yield return ("", true);
            if (parts[k].Length > 0)
                yield return (parts[k], false);
        }
    }

    /// <summary>About how many characters markup given among the text makes: its text, and one for each drawing, equation or field.</summary>
    static int TextLength(string markup)
    {
        try
        {
            var element = Parsed(markup);
            XNamespace w = Namespaces[0].Uri;
            XNamespace m = Namespaces[2].Uri;
            return element.Descendants(w + "t").Sum(t => t.Value.Length) + element.DescendantsAndSelf(w + "drawing").Count()
                + element.DescendantsAndSelf(m + "oMath").Count(o => o.Ancestors(m + "oMath").FirstOrDefault() == null);
        }
        catch (XmlException)
        {
            return 0;
        }
    }

    /// <summary>A table in the tone's look, its columns of even width over the text, the first row its head; its cells' text aligned as the columns say.</summary>
    static Made Table(Notation.Table table, Place place)
    {
        var columns = table.Aligns.Count;
        var rows = table.Rows.Count;
        var empty = place.EmptyTable(columns, rows);
        const string Cell = "<w:p><w:pPr><w:pStyle w:val=\"TableText\"/></w:pPr></w:p>";
        var parts = empty.Split(Cell);
        if (parts.Length != columns * rows + 1)
            return new("", [], [], Problem: "The table could not be made.");
        var markup = new StringBuilder(parts[0]);
        var notes = new List<(int, int, string)>();
        for (var k = 0; k < columns * rows; k++)
        {
            var (row, column) = (k / columns, k % columns);
            var align = table.Aligns[column];
            markup.Append("<w:p><w:pPr><w:pStyle w:val=\"TableText\"/>");
            if (align is "center" or "right")
                markup.Append("<w:jc w:val=\"").Append(align).Append("\"/>");
            markup.Append("</w:pPr>");
            var offset = 0;
            markup.Append(Runs(table.Rows[row][column], notes, k, ref offset));
            markup.Append("</w:p>").Append(parts[k + 1]);
        }
        return new(markup.ToString(), [], notes);
    }

    /// <summary>
    /// A vector picture: the SVG as Word keeps one (an SVG part, with a PNG drawn from it for programs that read no SVG), in a line of its
    /// own, centred (or as its align says), as wide as its width says (8cm, 60%, 300pt, 400px), else as its own size says, no wider than
    /// the text; its description its alt; its caption under it; float=left or right puts it beside the text that follows.
    /// </summary>
    static Made Svg(Notation.Fenced svg, Place place)
    {
        var body = svg.Body.Trim();
        if (!body.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            return new("", [], [], Problem: "An svg block holds an <svg> element.");
        var bytes = Encoding.UTF8.GetBytes(body);
        var drawing = SvgDrawing.Load(bytes);
        if (drawing == null)
            return new("", [], [], Problem: "The SVG could not be read.");
        // Its own size, CSS pixels to points; else as wide as the text, its proportions kept.
        var (ownWidth, ownHeight) = (drawing.Size.Width * 0.75, drawing.Size.Height * 0.75);
        if (ownWidth <= 0 || ownHeight <= 0)
            (ownWidth, ownHeight) = (place.TextWidth, place.TextWidth / 2);
        var width = Length(svg.Settings.GetValueOrDefault("width"), place.TextWidth) ?? Math.Min(ownWidth, place.TextWidth);
        width = Math.Clamp(width, 12, place.TextWidth);
        var height = width * ownHeight / ownWidth;
        var png = Png(drawing, width, height);
        var alt = svg.Settings.GetValueOrDefault("alt") ?? svg.Settings.GetValueOrDefault("description") ?? "";
        var (cx, cy) = (Math.Max(1, (long)Math.Round(width * EmuPerPoint)), Math.Max(1, (long)Math.Round(height * EmuPerPoint)));
        var described = Escape(alt);
        var run = $"<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>"
            + $"<wp:docPr id=\"1\" name=\"Grafik\" descr=\"{described}\"/><wp:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect=\"1\"/></wp:cNvGraphicFramePr>"
            + "<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\"><pic:pic>"
            + $"<pic:nvPicPr><pic:cNvPr id=\"0\" name=\"Grafik\" descr=\"{described}\"/><pic:cNvPicPr/></pic:nvPicPr>"
            + "<pic:blipFill><a:blip r:embed=\"{fallback}\"><a:extLst><a:ext uri=\"{96DAC541-7B7A-43D3-8B79-37D633B846F1}\">"
            + "<asvg:svgBlip xmlns:asvg=\"http://schemas.microsoft.com/office/drawing/2016/SVG/main\" r:embed=\"{vector}\"/></a:ext></a:extLst></a:blip>"
            + "<a:stretch><a:fillRect/></a:stretch></pic:blipFill>"
            + $"<pic:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></pic:spPr>"
            + "</pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
        const string Image = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";
        NewPart[] parts = [new("fallback", "/word/media/image1.png", "image/png", Image, png), new("vector", "/word/media/image1.svg", "image/svg+xml", Image, bytes)];
        return Placed(run, parts, svg.Settings, place);
    }

    /// <summary>
    /// A drawing's run as a block: in a line of its own (aligned as its settings say, centred by default) with its caption under it in the
    /// tone's caption style; or, where it floats, its run alone, to go in at the start of the paragraph after it (see Writing).
    /// </summary>
    static Made Placed(string run, IReadOnlyList<NewPart> parts, IReadOnlyDictionary<string, string> settings, Place place)
    {
        var floats = settings.GetValueOrDefault("float")?.ToLowerInvariant() is "left" or "right" ? settings["float"].ToLowerInvariant() : null;
        if (floats != null)
            return new(run, parts, [], Float: floats);
        var align = settings.GetValueOrDefault("align")?.ToLowerInvariant() is "left" or "right" ? settings["align"].ToLowerInvariant() : "center";
        var markup = $"<w:p><w:pPr><w:keepNext/><w:jc w:val=\"{align}\"/></w:pPr>{run}</w:p>";
        if (settings.GetValueOrDefault("caption") is { Length: > 0 } caption)
            markup += Paragraph("Caption", Notation.Inline(caption, null), align == "center" ? "center" : null).Blocks;
        return new(markup, parts, []);
    }

    /// <summary>A PNG of an SVG drawn at about 144 dots an inch for its size in points, white where it is not drawn over.</summary>
    static byte[] Png(SvgDrawing drawing, double width, double height)
    {
        var (w, h) = (Math.Clamp((int)Math.Round(width * 2), 1, 4000), Math.Clamp((int)Math.Round(height * 2), 1, 4000));
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(w / Math.Max(1, drawing.Size.Width), h / Math.Max(1, drawing.Size.Height));
        canvas.DrawPicture(drawing.Picture);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>A length in points: given in cm, mm, in, pt or px (CSS pixels), or as a share of the text's width (60%); null where none is given or it reads as none.</summary>
    static double? Length(string? given, double whole)
    {
        if (string.IsNullOrWhiteSpace(given))
            return null;
        var text = given.Trim().ToLowerInvariant();
        var unit = new string([.. text.SkipWhile(c => char.IsDigit(c) || c is '.' or ',' or '-')]).Trim();
        var number = text[..(text.Length - unit.Length)].Trim().Replace(',', '.');
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value <= 0)
            return null;
        return unit switch
        {
            "cm" => value * 72 / 2.54,
            "mm" => value * 72 / 25.4,
            "in" => value * 72,
            "pt" => value,
            "px" or "" => value * 0.75,
            "%" => whole * value / 100,
            _ => null,
        };
    }

    /// <summary>
    /// WordArt: a text box in a line of its own (or floating, see Placed), as wide as the text unless its width says so, its text in its
    /// size, font and colours, bent as its shape says (Word's text warps: arch, circle, wave, inflate …, or their own names, textArchUp …),
    /// with a soft shadow unless shadow=none; a fill of one colour or a gradient of two or three ("#1F3A93,#7A3FB0"), an outline of a
    /// colour, a glow of one.
    /// </summary>
    static Made WordArt(Notation.Fenced art, Place place)
    {
        var lines = art.Body.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0)
            return new("", [], [], Problem: "A wordart block holds its text.");
        var settings = art.Settings;
        var size = double.TryParse(settings.GetValueOrDefault("size")?.Replace("pt", "", StringComparison.OrdinalIgnoreCase), NumberStyles.Float, CultureInfo.InvariantCulture, out var given)
            ? Math.Clamp(given, 8, 200) : 40;
        var shape = Warp(settings.GetValueOrDefault("shape") ?? settings.GetValueOrDefault("warp") ?? "none");
        var width = Math.Min(Length(settings.GetValueOrDefault("width"), place.TextWidth) ?? place.TextWidth, place.TextWidth);
        var height = Length(settings.GetValueOrDefault("height"), place.TextWidth) ?? size * lines.Count * (shape == "textNoShape" ? 1.45 : 2.1);
        var (cx, cy) = ((long)Math.Round(width * EmuPerPoint), (long)Math.Round(height * EmuPerPoint));
        var properties = new StringBuilder();
        if (settings.GetValueOrDefault("font") is { Length: > 0 } font)
        {
            var family = Escape(font);
            properties.Append($"<w:rFonts w:ascii=\"{family}\" w:hAnsi=\"{family}\" w:eastAsia=\"{family}\" w:cs=\"{family}\"/>");
        }
        if (settings.GetValueOrDefault("bold")?.ToLowerInvariant() is not ("no" or "false" or "0"))
            properties.Append("<w:b/><w:bCs/>");
        if (settings.GetValueOrDefault("italic")?.ToLowerInvariant() is "yes" or "true" or "1")
            properties.Append("<w:i/><w:iCs/>");
        var colors = (settings.GetValueOrDefault("fill") ?? settings.GetValueOrDefault("color") ?? "#1F3A93").Split(',', ';', ' ')
            .Select(Hex).Where(c => c != null).Cast<string>().Take(3).ToList();
        if (colors.Count == 0)
            colors.Add("1F3A93");
        properties.Append("<w:color w:val=\"").Append(colors[0]).Append("\"/>");
        var halfPoints = ((int)Math.Round(size * 2)).ToString(CultureInfo.InvariantCulture);
        properties.Append($"<w:sz w:val=\"{halfPoints}\"/><w:szCs w:val=\"{halfPoints}\"/>");
        if (Hex(settings.GetValueOrDefault("glow")) is { } glow)
            properties.Append($"<w14:glow w14:rad=\"101600\"><w14:srgbClr w14:val=\"{glow}\"><w14:alpha w14:val=\"60000\"/></w14:srgbClr></w14:glow>");
        if (settings.GetValueOrDefault("shadow")?.ToLowerInvariant() is not ("no" or "none" or "false" or "0"))
            properties.Append("<w14:shadow w14:blurRad=\"50800\" w14:dist=\"25400\" w14:dir=\"5400000\" w14:sx=\"100000\" w14:sy=\"100000\" w14:kx=\"0\" w14:ky=\"0\" w14:algn=\"tl\">"
                + "<w14:srgbClr w14:val=\"000000\"><w14:alpha w14:val=\"70000\"/></w14:srgbClr></w14:shadow>");
        if (Hex(settings.GetValueOrDefault("outline")) is { } outline)
            properties.Append($"<w14:textOutline w14:w=\"12700\" w14:cap=\"flat\" w14:cmpd=\"sng\" w14:algn=\"ctr\"><w14:solidFill><w14:srgbClr w14:val=\"{outline}\"/></w14:solidFill>"
                + "<w14:prstDash w14:val=\"solid\"/><w14:round/></w14:textOutline>");
        if (colors.Count == 1)
            properties.Append($"<w14:textFill><w14:solidFill><w14:srgbClr w14:val=\"{colors[0]}\"/></w14:solidFill></w14:textFill>");
        else
        {
            properties.Append("<w14:textFill><w14:gradFill><w14:gsLst>");
            for (var k = 0; k < colors.Count; k++)
                properties.Append($"<w14:gs w14:pos=\"{k * 100000 / (colors.Count - 1)}\"><w14:srgbClr w14:val=\"{colors[k]}\"/></w14:gs>");
            properties.Append("</w14:gsLst><w14:lin w14:ang=\"0\" w14:scaled=\"0\"/></w14:gradFill></w14:textFill>");
        }
        var props = properties.ToString();
        var text = new StringBuilder();
        foreach (var line in lines)
            text.Append($"<w:p><w:pPr><w:jc w:val=\"center\"/><w:spacing w:before=\"0\" w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/><w:rPr>{props}</w:rPr></w:pPr>")
                .Append($"<w:r><w:rPr>{props}</w:rPr><w:t xml:space=\"preserve\">{Escape(Clean(line))}</w:t></w:r></w:p>");
        var run = $"<w:r><w:drawing><wp:inline distT=\"0\" distB=\"0\" distL=\"0\" distR=\"0\"><wp:extent cx=\"{cx}\" cy=\"{cy}\"/><wp:effectExtent l=\"0\" t=\"0\" r=\"0\" b=\"0\"/>"
            + "<wp:docPr id=\"1\" name=\"WordArt\"/><wp:cNvGraphicFramePr/>"
            + "<a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"><wps:wsp><wps:cNvSpPr txBox=\"1\"/>"
            + $"<wps:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"{cx}\" cy=\"{cy}\"/></a:xfrm><a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/><a:ln><a:noFill/></a:ln></wps:spPr>"
            + $"<wps:txbx><w:txbxContent>{text}</w:txbxContent></wps:txbx>"
            + "<wps:bodyPr rot=\"0\" vert=\"horz\" wrap=\"square\" lIns=\"0\" tIns=\"0\" rIns=\"0\" bIns=\"0\" anchor=\"ctr\" anchorCtr=\"0\">"
            + $"<a:prstTxWarp prst=\"{shape}\"><a:avLst/></a:prstTxWarp><a:noAutofit/></wps:bodyPr></wps:wsp></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
        return Placed(run, [], settings, place);
    }

    /// <summary>Word's text warp for a shape's name: its own name (textArchUp …) or a short one (arch, circle, wave …); none for straight text.</summary>
    static string Warp(string name) => name.Trim().ToLowerInvariant() switch
    {
        "arch" or "arch-up" or "archup" => "textArchUp",
        "arch-down" or "archdown" => "textArchDown",
        "circle" => "textCircle",
        "button" => "textButton",
        "wave" or "wave1" => "textWave1",
        "wave2" => "textWave2",
        "double-wave" or "doublewave" => "textDoubleWave1",
        "inflate" => "textInflate",
        "deflate" => "textDeflate",
        "inflate-top" => "textInflateTop",
        "inflate-bottom" => "textInflateBottom",
        "deflate-top" => "textDeflateTop",
        "deflate-bottom" => "textDeflateBottom",
        "slant-up" => "textSlantUp",
        "slant-down" => "textSlantDown",
        "triangle" => "textTriangle",
        "triangle-down" => "textTriangleInverted",
        "chevron" => "textChevron",
        "chevron-down" => "textChevronInverted",
        "can" or "can-up" => "textCanUp",
        "can-down" => "textCanDown",
        "fade-right" => "textFadeRight",
        "fade-left" => "textFadeLeft",
        "fade-up" => "textFadeUp",
        "fade-down" => "textFadeDown",
        "cascade-up" => "textCascadeUp",
        "cascade-down" => "textCascadeDown",
        "ring" => "textRingInside",
        "stop" => "textStop",
        "" or "none" or "plain" or "straight" => "textNoShape",
        var other when other.StartsWith("text", StringComparison.Ordinal) && other.Length > 4 => "text" + char.ToUpperInvariant(name.Trim()[4]) + name.Trim()[5..],
        _ => "textNoShape",
    };

    /// <summary>A colour as six hex digits (from #RRGGBB, RRGGBB or #RGB), or null.</summary>
    static string? Hex(string? given)
    {
        if (string.IsNullOrWhiteSpace(given))
            return null;
        var hex = given.Trim().TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(c => $"{c}{c}"));
        return hex.Length == 6 && hex.All(Uri.IsHexDigit) ? hex.ToUpperInvariant() : null;
    }

    /// <summary>Text without what XML cannot hold (control characters but the tab).</summary>
    static string Clean(string text) => string.Concat(text.Where(c => c == '\t' || c >= ' '));

    static string Escape(string text) => SecurityElement.Escape(text) ?? "";

    /// <summary>Markup given as it is read with the usual prefixes declared: null where it is well-formed, else why not.</summary>
    public static string? Checked(string markup)
    {
        try
        {
            Parsed(markup);
            return null;
        }
        catch (XmlException e)
        {
            return $"This WordprocessingML is not well-formed ({e.Message}): {(markup.Length > 120 ? markup[..120] + "…" : markup)}";
        }
    }

    static XElement Parsed(string markup)
    {
        var declarations = string.Concat(Namespaces.Select(n => $" xmlns:{n.Prefix}=\"{n.Uri}\""));
        return XElement.Parse($"<root{declarations}>{markup}</root>");
    }
}
