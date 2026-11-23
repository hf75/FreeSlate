using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// The objects of a document as Claude reads them (see Reading) and keeps them as it writes (see Writing): each drawing, field, equation
/// and the like, which takes a character of a paragraph's text, by a number of its own in a token ("[picture 3]"); each footnote and
/// endnote by a label as Markdown writes one ("[^2]"), its text given with the document. An object is known by its markup as copying it
/// keeps it, its ids aside (see Key), and so found again where a write put it in anew; the markup of one a write takes away is kept from
/// before (see Keep), so that it goes in again where its token is written; one whose token is written elsewhere moves there.
/// </summary>
sealed partial class Objects(IEditableDocument document)
{
    /// <summary>The kinds a token names (see Kind), as the notation reads them back.</summary>
    public const string Kinds = "picture|graphic|wordart|frame|chart|smartart|text box|group|shape|drawing|field|equation|check box|object";

    /// <summary>An object: its number, its kind, its key (null where it cannot be copied, and so not kept), its markup kept, where it stood last.</summary>
    sealed class Entry(int id, string kind, string? key)
    {
        public int Id { get; } = id;
        public string Kind { get; } = kind;
        public string? Key { get; } = key;
        public string? Kept { get; set; }
        public (int Paragraph, int Offset)? At { get; set; }

        /// <summary>Whether the write going on took it away (see Keep): its token then puts in what was kept, whatever else reads alike.</summary>
        public bool TakenAway { get; set; }

        /// <summary>How the paragraph it stood alone in was aligned (left, center, right) when it was kept; null where it stood among text.</summary>
        public string? Align { get; set; }
    }

    /// <summary>A footnote or endnote as Claude reads it: its label, whether it is an endnote, its text in the notation.</summary>
    public sealed record Note(string Label, bool Endnote, string Text);

    readonly List<Entry> entries = [];
    readonly Dictionary<string, Note> notes = new(StringComparer.Ordinal);

    /// <summary>The notes' labels by what they are in the document (f or e and their id), and how many of each kind were labelled.</summary>
    readonly Dictionary<string, string> labels = new(StringComparer.Ordinal);
    int footnotes, endnotes;

    /// <summary>The objects known, by their numbers.</summary>
    public IEnumerable<int> Known => entries.Select(e => e.Id);

    public IEnumerable<Note> Notes => notes.Values;

    /// <summary>The number of the object whose character stands at a place (known from there on).</summary>
    public int IdAt(int paragraph, int offset) => EntryAt(paragraph, offset).Id;

    /// <summary>The kind of an object by its number; null where there is none.</summary>
    public string? KindOf(int id) => entries.FirstOrDefault(e => e.Id == id)?.Kind;

    /// <summary>The token of the character at a place, which is an object's or a note's: "[picture 3]", "[^2]".</summary>
    public string Token(int paragraph, int offset)
    {
        if (document is WordDocument word && word.NoteAt(paragraph, offset) is { } note)
            return "[^" + Label(note.Id, note.Endnote, note.Paragraphs) + "]";
        var entry = EntryAt(paragraph, offset);
        return $"[{entry.Kind} {entry.Id.ToString(CultureInfo.InvariantCulture)}]";
    }

    /// <summary>A note's label: the one given it before, else the next of its kind (1, 2 … for footnotes, e1, e2 … for endnotes), its text noted.</summary>
    string Label(string id, bool endnote, IReadOnlyList<int> paragraphs)
    {
        var key = (endnote ? "e" : "f") + id;
        if (labels.TryGetValue(key, out var label))
            return label;
        label = endnote ? "e" + (++endnotes).ToString(CultureInfo.InvariantCulture) : (++footnotes).ToString(CultureInfo.InvariantCulture);
        labels[key] = label;
        notes[label] = new Note(label, endnote, NoteText(paragraphs));
        return label;
    }

    /// <summary>A note's text in the notation: its paragraphs run on, the note's own mark at its start left out.</summary>
    string NoteText(IReadOnlyList<int> paragraphs)
    {
        var text = new StringBuilder();
        foreach (var paragraph in paragraphs)
        {
            var own = document.Text(paragraph);
            var from = 0;
            if (paragraph == paragraphs[0])
            {
                while (from < own.Length && (own[from] == '￼' || char.IsWhiteSpace(own[from])))
                    from++;
            }
            var line = Reading.Line(document, paragraph, from, own.Length, this).Trim();
            if (line.Length > 0)
                text.Append(text.Length > 0 ? " " : "").Append(line);
        }
        return text.ToString();
    }

    /// <summary>
    /// The formula whose character stands at a place as LaTeX (see Formulas), and whether it stands in a paragraph of its own (an
    /// m:oMathPara); null where none stands there, or it holds what LaTeX here cannot say (it then reads as an object).
    /// </summary>
    public (string Latex, bool Display)? Formula(int paragraph, int offset)
    {
        if (Copied(paragraph, offset) is not { } markup || Element(markup) is not { } element)
            return null;
        var math = element.Descendants().FirstOrDefault(e => e.Name.LocalName is "oMathPara" or "oMath" && e.Name.NamespaceName.EndsWith("/math", StringComparison.Ordinal));
        return math != null && Formulas.Latex(math) is { Length: > 0 } latex ? (latex, math.Name.LocalName == "oMathPara") : null;
    }

    /// <summary>A note's text by its label, and whether it is an endnote; null where no note has the label.</summary>
    public Note? NoteOf(string label) => notes.GetValueOrDefault(label);

    /// <summary>A note written (see Writing): its label then stands for it with this text, as it is in the document now.</summary>
    public void Noted(string label, string text, bool endnote) => notes[label] = new Note(label, endnote, text);

    /// <summary>The object whose character stands at a place: the one known by its key (where it is not known to stand elsewhere), else a new one.</summary>
    Entry EntryAt(int paragraph, int offset)
    {
        var markup = Copied(paragraph, offset);
        var key = markup != null ? Key(markup) : null;
        var entry = key != null
            ? entries.FirstOrDefault(e => e.Key == key && (e.At == (paragraph, offset) || e.At is not { } at || KeyAt(at) != key))
            : entries.FirstOrDefault(e => e.Key == null && e.At == (paragraph, offset));
        if (entry == null)
        {
            entry = new Entry(entries.Count + 1, Kind(paragraph, offset, markup), key);
            entries.Add(entry);
        }
        entry.At = (paragraph, offset);
        return entry;
    }

    /// <summary>
    /// The markup of the objects in a stretch of a paragraph's text kept (all of it by default), and its notes labelled: a write is about
    /// to take it away, and what of it the write gives again goes in as it was.
    /// </summary>
    public void Keep(int paragraph, int from = 0, int to = int.MaxValue)
    {
        var text = document.Text(paragraph);
        // An object alone in its paragraph: how that stands, to stand so again where its token alone makes a paragraph (see Markup).
        string? alone = null;
        if (text == "￼" && from == 0)
            alone = Alignment(paragraph);
        for (var offset = from; offset < Math.Min(to, text.Length); offset++)
        {
            if (text[offset] != '￼')
                continue;
            if (document is WordDocument word && word.NoteAt(paragraph, offset) is { } note)
            {
                Label(note.Id, note.Endnote, note.Paragraphs);
                continue;
            }
            var entry = EntryAt(paragraph, offset);
            if (Copied(paragraph, offset) is { } markup)
                (entry.Kept, entry.TakenAway, entry.Align) = (markup, true, alone);
        }
    }

    /// <summary>How the paragraph an object kept stood alone in was aligned; null where it stood among text, or it is not known.</summary>
    public string? AlignOf(int id) => entries.FirstOrDefault(e => e.Id == id)?.Align;

    /// <summary>A paragraph's alignment as its own properties give it (left, center, right; justified text reads as left).</summary>
    string Alignment(int paragraph)
    {
        var copied = (document.After(paragraph) is { } next ? document.Copy(paragraph, 0, next, 0) : null)
            ?? document.Copy(paragraph, 0, paragraph, document.Text(paragraph).Length);
        if (copied is not [var first, ..] || Element(first) is not { } element)
            return "left";
        var jc = element.Descendants().FirstOrDefault(e => e.Name.LocalName == "pPr")?.Elements().FirstOrDefault(e => e.Name.LocalName == "jc");
        return ((string?)jc?.Attributes().FirstOrDefault(a => a.Name.LocalName == "val"))?.ToLowerInvariant() switch
        {
            "center" => "center",
            "right" or "end" => "right",
            _ => "left",
        };
    }

    /// <summary>A write begun or ended: the objects it took away are those of any other write again (see Entry.TakenAway).</summary>
    public void Written()
    {
        foreach (var entry in entries)
            entry.TakenAway = false;
    }

    /// <summary>The objects of paragraphs kept (see Keep): those from one to another in reading order, their tables' cells too.</summary>
    public void Keep(int first, int last)
    {
        for (int? paragraph = first; paragraph is { } at; paragraph = document.Next(at))
        {
            Keep(at);
            if (at == last)
                break;
        }
    }

    /// <summary>
    /// The markup an object's token goes in as (see Writing): where it stands in the document, as it is there, and that place (where it is
    /// to be taken away once it went in at its token: it moved); where a write took it away, as it was kept; null where there is no such
    /// object, or it cannot be copied.
    /// </summary>
    public (string Markup, (int Paragraph, int Offset)? From)? Markup(int id)
    {
        if (entries.FirstOrDefault(e => e.Id == id) is not { Key: not null } entry)
            return null;
        // Taken away by the write going on: as it was, though another of the same markup stands elsewhere (a picture pasted twice).
        if (entry.TakenAway && entry.Kept is { } taken)
            return (taken, null);
        if (Find(entry) is { } at && Copied(at.Paragraph, at.Offset) is { } now)
            return (now, at);
        return entry.Kept is { } kept ? (kept, null) : null;
    }

    /// <summary>Where an object stands now; null where it is not in the document (taken away), or it cannot be found again.</summary>
    public (int Paragraph, int Offset)? Find(int id) => entries.FirstOrDefault(e => e.Id == id) is { } entry ? Find(entry) : null;

    (int Paragraph, int Offset)? Find(Entry entry)
    {
        if (entry.Key == null)
            return entry.At is { } place && document.Contains(place.Paragraph) && place.Offset < document.Text(place.Paragraph).Length
                && document.Text(place.Paragraph)[place.Offset] == '￼' ? place : null;
        if (entry.At is { } hint && KeyAt(hint) == entry.Key)
            return hint;
        for (int? paragraph = document.Beginning; paragraph is { } p; paragraph = document.Next(p))
        {
            var text = document.Text(p);
            for (var offset = text.IndexOf('￼'); offset >= 0; offset = text.IndexOf('￼', offset + 1))
            {
                if (KeyAt((p, offset)) == entry.Key)
                {
                    entry.At = (p, offset);
                    return entry.At;
                }
            }
        }
        return null;
    }

    /// <summary>Whether the object at a place is still the one an object's number names (its key there), as a change it made is checked.</summary>
    public bool Stands(int id, (int Paragraph, int Offset) at) => entries.FirstOrDefault(e => e.Id == id) is { Key: { } key } && KeyAt(at) == key;

    /// <summary>The object at a place known anew after a change to it (see Talk's picture): its number then names it as it is now.</summary>
    public void Changed(int id, (int Paragraph, int Offset) at)
    {
        var index = entries.FindIndex(e => e.Id == id);
        if (index < 0 || Copied(at.Paragraph, at.Offset) is not { } markup)
            return;
        var was = entries[index];
        entries[index] = new Entry(id, was.Kind, Key(markup)) { At = at, Kept = was.Kept };
    }

    /// <summary>The key of the object at a place: its markup without its ids; null where none stands there, or it cannot be copied.</summary>
    string? KeyAt((int Paragraph, int Offset) at)
    {
        if (!document.Contains(at.Paragraph))
            return null;
        var text = document.Text(at.Paragraph);
        return at.Offset < text.Length && text[at.Offset] == '￼' && Copied(at.Paragraph, at.Offset) is { } markup ? Key(markup) : null;
    }

    /// <summary>What an object is known by: its markup without the ids Word and the document give drawings anew (docPr, cNvPr, wp14's).</summary>
    static string Key(string markup) => Ids().Replace(markup, "");

    [GeneratedRegex(@"\s(?:id|wp14:anchorId|wp14:editId|w14:paraId|w14:textId)=""[^""]*""")]
    private static partial Regex Ids();

    /// <summary>The runs of the character at a place as copying it keeps them (see IEditableDocument.Copy); null where it keeps none.</summary>
    string? Copied(int paragraph, int offset)
    {
        if (document.Copy(paragraph, offset, paragraph, offset + 1) is not [var copied])
            return null;
        try
        {
            var p = Asking.Markup.Parsed(copied).Elements().FirstOrDefault();
            var runs = p == null ? "" : string.Concat(p.Elements().Where(e => e.Name.LocalName != "pPr").Select(e => e.ToString(SaveOptions.DisableFormatting)));
            return runs.Length > 0 ? runs : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>What an object is, as its token names it, by its markup (see Kinds).</summary>
    string Kind(int paragraph, int offset, string? markup)
    {
        if (markup == null || Element(markup) is not { } element)
            return "object";
        bool Has(string name) => element.Descendants().Any(e => e.Name.LocalName == name);
        if (document is WordDocument word && word.IsFrame(paragraph, offset))
            return "frame";
        if (Has("svgBlip"))
            return "graphic";
        // A text box is a rectangle with text; another preset with text inside is a shape (see Markup.Shape).
        var preset = (string?)element.Descendants().FirstOrDefault(e => e.Name.LocalName == "prstGeom")?.Attribute("prst");
        if (Has("txbxContent"))
            return Has("prstTxWarp") || Has("textFill") || Has("textOutline") ? "wordart" : Has("pic") ? "picture"
                : preset is null or "rect" or "roundRect" && Has("spAutoFit") ? "text box" : "shape";
        if (Has("pic"))
            return "picture";
        if (Has("relIds"))
            return "smartart";
        if (Has("chart") || element.Descendants().Any(e => e.Name.NamespaceName.Contains("/chart", StringComparison.Ordinal)))
            return "chart";
        if (Has("wgp"))
            return "group";
        if (Has("wsp"))
            return "shape";
        if (Has("drawing"))
            return "drawing";
        if (Has("fldChar") || Has("fldSimple") || Has("instrText"))
            return "field";
        if (Has("oMath"))
            return "equation";
        if (Has("checkbox"))
            return "check box";
        return "object";
    }

    static XElement? Element(string markup)
    {
        try
        {
            return Asking.Markup.Parsed(markup);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// What Claude reads of an object besides its token (see Reading): where and how it stands, as the picture tool sets it (place, width,
    /// turn …), its description and caption; a vector graphic's SVG and WordArt's block as the notation writes them, to be written anew;
    /// a text box's text, a field's instruction, an equation's text.
    /// </summary>
    public string Details(int id)
    {
        if (entries.FirstOrDefault(e => e.Id == id) is not { } entry)
            return "";
        var line = new StringBuilder($"[{entry.Kind} {id.ToString(CultureInfo.InvariantCulture)}]");
        if (Find(entry) is not { } at)
            return line.Append(" (taken away)").ToString();
        var (paragraph, offset) = at;
        var markup = Copied(paragraph, offset);
        var element = markup != null ? Element(markup) : null;
        var word = document as WordDocument;
        if (document.DrawingOf(paragraph, offset) is { } drawing)
        {
            line.Append(" place=").Append(Place(drawing));
            if (drawing.Width is { } width)
                line.Append(" width=").Append(Centimetres(width));
            if (drawing.Height is { } height)
                line.Append(" height=").Append(Centimetres(height));
            if (drawing.Rotation is { } rotation && Math.Abs(rotation) > 0.05)
                line.Append(" turn=").Append(Math.Round(rotation).ToString(CultureInfo.InvariantCulture));
            if ((drawing.FlipHorizontal, drawing.FlipVertical) is var (across, down) && (across == true || down == true))
                line.Append(" flip=").Append(across == true && down == true ? "both" : across == true ? "across" : "down");
            if (drawing.Tilt is { IsFlat: false } tilt)
                line.Append(" tilt=").Append(TiltName(tilt));
            if (drawing.Shape == "ellipse")
                line.Append(" crop=circle");
            else if (drawing.Crop is { } crop && crop != PictureCrop.None)
                line.Append(" crop=yes");
            if (drawing.Recolor is { } recolor && recolor != "none")
                line.Append(" color=").Append(recolor);
            if (drawing.Outline is > 0)
                line.Append(" frame=yes");
            if (drawing.Decorative == true)
                line.Append(" decorative=yes");
            if (drawing.Description is { Length: > 0 } description)
                line.Append(" description=").Append(Quoted(description));
            if (word?.CaptionOf(paragraph, offset) is { } caption)
            {
                var captions = new List<string>();
                for (int? p = caption; p is { } c; p = document.After(c))
                    captions.Add(Reading.Line(document, c, 0, document.Text(c).Length, this));
                line.Append(" caption=").Append(Quoted(string.Join(" ", captions).Trim()));
            }
        }
        switch (entry.Kind)
        {
            case "graphic" when word?.VectorOf(paragraph, offset) is { } vector:
            {
                var svg = Encoding.UTF8.GetString(vector).Trim();
                line.Append(svg.Length <= 8000 ? $"\n```svg\n{svg}\n```" : $" (its SVG is {svg.Length.ToString(CultureInfo.InvariantCulture)} characters long, not shown)");
                break;
            }
            case "wordart" when element != null && WordArt(element) is { } block:
                line.Append('\n').Append(block);
                break;
            case "chart" when word?.ChartOf(paragraph, offset) is { } part && Charts.Block(part, document.DrawingOf(paragraph, offset)) is { } block:
                line.Append('\n').Append(block);
                break;
            case "smartart" when word?.DiagramOf(paragraph, offset) is { } diagram && SmartArts.Block(diagram.Data, diagram.Layout, document.DrawingOf(paragraph, offset)) is { } block:
                line.Append('\n').Append(block);
                break;
            case "text box" when element != null && word != null:
                line.Append('\n').Append(Box(word, paragraph, offset, element));
                break;
            case "shape" when element != null && word != null && element.Descendants().Any(e => e.Name.LocalName == "prstGeom"):
                line.Append('\n').Append(ShapeBlock(word, paragraph, offset, element));
                break;
            case "field" when element != null:
            {
                var instruction = (string?)element.Descendants().FirstOrDefault(e => e.Name.LocalName == "fldSimple")?.Attributes().FirstOrDefault(a => a.Name.LocalName == "instr")
                    ?? string.Concat(element.Descendants().Where(e => e.Name.LocalName == "instrText").Select(e => e.Value));
                line.Append(' ').Append(instruction.Trim());
                if (document.FieldText(paragraph, offset) is { Length: > 0 } shown)
                    line.Append(" shows ").Append(Quoted(shown));
                break;
            }
            case "equation" when element != null:
                line.Append(' ').Append(string.Concat(element.Descendants().Where(e => e.Name.LocalName == "t").Select(e => e.Value)));
                break;
            case "frame" when word?.FramedTable(paragraph, offset) is { } framed && word.CellsOf(framed) is { } cells:
            {
                // The table it holds, as the notation writes one (to read; the picture tool places, turns and tilts the frame).
                line.Append(" holds a table:");
                for (var row = 0; row < cells.Rows; row++)
                {
                    line.Append("\n|");
                    for (var column = 0; column < cells.Columns; column++)
                    {
                        var cell = cells.Cells.FirstOrDefault(c => c.Top <= row && row < c.Bottom && c.Left <= column && column < c.Right);
                        line.Append(' ').Append(cell == null ? "" : cell.Top != row ? "^" : cell.Left != column ? "<" : Reading.CellText(document, cell, this)).Append(" |");
                    }
                    if (row == 0)
                        line.Append("\n|").Append(string.Concat(Enumerable.Repeat("---|", cells.Columns)));
                }
                break;
            }
        }
        return line.ToString();
    }

    /// <summary>
    /// A text box as the notation writes a box (see Markup.Box): its fill, line, shape, width and float read back from its shape, its
    /// paragraphs in the notation within it.
    /// </summary>
    string Box(WordDocument word, int paragraph, int offset, XElement element)
    {
        var settings = new List<string>();
        var properties = element.Descendants().FirstOrDefault(e => e.Name.LocalName == "spPr");
        var fill = properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "solidFill")?.Descendants().FirstOrDefault(e => e.Name.LocalName == "srgbClr");
        if ((string?)fill?.Attribute("val") is { Length: 6 } hex)
            settings.Add("fill=" + TableWords.FillName(hex));
        var outline = properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "ln");
        var width = (long?)outline?.Attribute("w") ?? 0;
        var dash = (string?)outline?.Elements().FirstOrDefault(e => e.Name.LocalName == "prstDash")?.Attribute("val");
        settings.Add("line=" + (outline == null || outline.Elements().Any(e => e.Name.LocalName == "noFill") ? "none"
            : (string?)outline.Attribute("cmpd") == "dbl" ? "double"
            : dash is "sysDot" or "dot" or "sysDash" && dash != "sysDash" ? "dotted"
            : dash is "dash" or "sysDash" or "lgDash" ? "dashed"
            : width >= 15875 ? "strong" : "fine"));
        if ((string?)element.Descendants().FirstOrDefault(e => e.Name.LocalName == "prstGeom")?.Attribute("prst") == "roundRect")
            settings.Add("shape=rounded");
        if (document.DrawingOf(paragraph, offset) is { } drawing)
        {
            if (drawing.Width is { } across)
                settings.Add("width=" + Centimetres(across));
            if (Place(drawing) is "left" or "right")
                settings.Add("float=" + Place(drawing));
        }
        var lines = word.TextBoxParagraphs(paragraph, offset).Select(p => Reading.Paragraph(document, p, this));
        return "```box " + string.Join(" ", settings) + "\n" + string.Join("\n\n", lines) + "\n```";
    }

    /// <summary>
    /// A shape as the notation writes it (see Markup.Shape): its preset, fill, line (and the line's colour where it is not the tone's), a
    /// line's arrowheads, its shadow, size, turn, mirror and place, its text.
    /// </summary>
    string ShapeBlock(WordDocument word, int paragraph, int offset, XElement element)
    {
        var settings = new List<string>();
        var properties = element.Descendants().FirstOrDefault(e => e.Name.LocalName == "spPr");
        var preset = (string?)properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "prstGeom")?.Attribute("prst") ?? "rect";
        settings.Add("type=" + preset);
        var isLine = element.Descendants().Any(e => e.Name.LocalName == "cNvCnPr") || preset is "line" or "lineInv" or "straightConnector1";
        var fill = properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "solidFill")?.Descendants().FirstOrDefault(e => e.Name.LocalName == "srgbClr");
        if ((string?)fill?.Attribute("val") is { Length: 6 } hex)
            settings.Add("fill=" + TableWords.FillName(hex));
        else if (!isLine)
            settings.Add("fill=none");
        var outline = properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "ln");
        var width = (long?)outline?.Attribute("w") ?? 0;
        var dash = (string?)outline?.Elements().FirstOrDefault(e => e.Name.LocalName == "prstDash")?.Attribute("val");
        var none = outline == null || outline.Elements().Any(e => e.Name.LocalName == "noFill");
        settings.Add("line=" + (none ? "none"
            : (string?)outline!.Attribute("cmpd") == "dbl" ? "double"
            : dash is "sysDot" or "dot" ? "dotted"
            : dash is "dash" or "sysDash" or "lgDash" ? "dashed"
            : width >= 15875 ? "strong" : "fine"));
        if (!none && (string?)outline!.Descendants().FirstOrDefault(e => e.Name.LocalName == "srgbClr")?.Attribute("val") is { Length: 6 } lineColor && lineColor != "FFFFFF"
            && !string.Equals(lineColor, "D6D3CD", StringComparison.OrdinalIgnoreCase) && !string.Equals(lineColor, "3F3C37", StringComparison.OrdinalIgnoreCase))
            settings.Add("color=" + TableWords.FillName(lineColor));
        if (isLine)
        {
            bool End(string name) => outline?.Elements().Any(e => e.Name.LocalName == name && (string?)e.Attribute("type") is { } type && type != "none") == true;
            var (head, tail) = (End("headEnd"), End("tailEnd"));
            settings.Add("ends=" + (head && tail ? "both" : head ? "start" : tail ? "end" : "none"));
        }
        if (properties?.Descendants().Any(e => e.Name.LocalName == "outerShdw") == true)
            settings.Add("shadow=yes");
        if (document.DrawingOf(paragraph, offset) is { } drawing)
        {
            if (drawing.Width is { } across)
                settings.Add("width=" + Centimetres(across));
            if (drawing.Height is { } down && !(isLine && down < 0.5) && !element.Descendants().Any(e => e.Name.LocalName == "spAutoFit"))
                settings.Add("height=" + Centimetres(down));
            if (Place(drawing) is "left" or "right")
                settings.Add("float=" + Place(drawing));
        }
        // The shape's own turn and mirror (its spPr's xfrm).
        var xfrm = properties?.Elements().FirstOrDefault(e => e.Name.LocalName == "xfrm");
        if ((long?)xfrm?.Attribute("rot") is { } rot && rot != 0)
            settings.Add("turn=" + Math.Round(rot / 60000.0).ToString(CultureInfo.InvariantCulture));
        var (flipH, flipV) = ((string?)xfrm?.Attribute("flipH") is "1" or "true", (string?)xfrm?.Attribute("flipV") is "1" or "true");
        if (flipH || flipV)
            settings.Add("flip=" + (flipH && flipV ? "both" : flipH ? "across" : "down"));
        var lines = element.Descendants().Any(e => e.Name.LocalName == "txbxContent")
            ? word.TextBoxParagraphs(paragraph, offset).Select(p => Reading.Paragraph(document, p, this)).ToList()
            : [];
        return "```shape " + string.Join(" ", settings) + "\n" + string.Concat(lines.Select(l => l + "\n\n")).TrimEnd('\n') + (lines.Count > 0 ? "\n" : "") + "```";
    }

    /// <summary>Where a drawing stands, as the picture tool names it: inline, left, right, own, behind, front (floating otherwise).</summary>
    static string Place(DrawingFormatting drawing) => drawing.Wrap switch
    {
        "inline" => "inline",
        "topAndBottom" => "own",
        "behind" => "behind",
        "front" => "front",
        "square" or "tight" or "through" => drawing.HorizontalAlign is "left" or "right" ? drawing.HorizontalAlign : "floating",
        _ => "floating",
    };

    /// <summary>A tilt as the picture tool names it: a preset of the ring's (above, rising …), else its angles.</summary>
    public static string TiltName(PictureTilt tilt) => Tilts.FirstOrDefault(t => t.Camera == tilt.Camera && tilt.X == null && tilt.Y == null && tilt.Z == null).Name
        ?? FormattableString.Invariant($"x{Math.Round(tilt.Angles.X)},y{Math.Round(tilt.Angles.Y)}");

    /// <summary>The tilts the ring offers (see EditorView), by the names the picture tool takes.</summary>
    public static readonly (string Name, string Camera)[] Tilts =
    [
        ("above", "perspectiveAbove"), ("rising", "isometricRightUp"), ("right", "perspectiveRight"), ("falling", "isometricRightDown"),
        ("below", "perspectiveBelow"), ("lying", "perspectiveRelaxed"), ("left", "perspectiveLeft"),
    ];

    static string Centimetres(double points) => (points / 72 * 2.54).ToString("0.#", CultureInfo.InvariantCulture) + "cm";

    static string Quoted(string text) => "\"" + text.Replace("\"", "''", StringComparison.Ordinal) + "\"";

    /// <summary>WordArt as the notation writes it (see Markup.WordArt): its settings read back from its text box and its text.</summary>
    static string? WordArt(XElement element)
    {
        var content = element.Descendants().FirstOrDefault(e => e.Name.LocalName == "txbxContent");
        if (content == null)
            return null;
        var lines = content.Elements().Where(e => e.Name.LocalName == "p")
            .Select(p => string.Concat(p.Descendants().Where(t => t.Name.LocalName == "t").Select(t => t.Value)).Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0)
            return null;
        var settings = new List<string>();
        var warp = (string?)element.Descendants().FirstOrDefault(e => e.Name.LocalName == "prstTxWarp")?.Attribute("prst") ?? "textNoShape";
        settings.Add("shape=" + Asking.Markup.ShapeName(warp));
        var properties = content.Descendants().FirstOrDefault(e => e.Name.LocalName == "r")?.Elements().FirstOrDefault(e => e.Name.LocalName == "rPr");
        XElement? Property(string name) => properties?.Elements().FirstOrDefault(e => e.Name.LocalName == name);
        string? Value(XElement? e, string name = "val") => (string?)e?.Attributes().FirstOrDefault(a => a.Name.LocalName == name);
        if (Value(Property("sz")) is { } halfPoints && double.TryParse(halfPoints, NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
            settings.Add("size=" + (size / 2).ToString(CultureInfo.InvariantCulture));
        if (Value(Property("rFonts"), "ascii") is { Length: > 0 } font)
            settings.Add("font=" + (font.Contains(' ', StringComparison.Ordinal) ? Quoted(font) : font));
        static IEnumerable<string> Colors(XElement? e) => e?.Descendants().Where(c => c.Name.LocalName == "srgbClr")
            .Select(c => "#" + (string?)c.Attributes().FirstOrDefault(a => a.Name.LocalName == "val")) ?? [];
        var fill = Colors(Property("textFill")).ToList();
        if (fill.Count == 0 && Value(Property("color")) is { Length: 6 } color)
            fill.Add("#" + color);
        if (fill.Count > 0)
            settings.Add("fill=" + string.Join(",", fill));
        if (Colors(Property("textOutline")).FirstOrDefault() is { } outline)
            settings.Add("outline=" + outline);
        if (Colors(Property("glow")).FirstOrDefault() is { } glow)
            settings.Add("glow=" + glow);
        if (Property("shadow") == null)
            settings.Add("shadow=none");
        if (Property("b") == null)
            settings.Add("bold=no");
        if (Property("i") != null)
            settings.Add("italic=yes");
        return "```wordart " + string.Join(" ", settings) + "\n" + string.Join("\n", lines) + "\n```";
    }
}
