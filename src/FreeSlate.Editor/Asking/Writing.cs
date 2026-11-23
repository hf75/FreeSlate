using System.Globalization;
using System.Security;
using System.Text.Json;
using FreeSlate.Editing;
using FreeSlate.Editor.Controls;
using FreeSlate.Editor.Writing;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// A write of Claude's going into the document as its input streams in (see Talk, Prompt): where it goes is decided as soon as that is
/// read; the blocks it has finished go in there one after the other, the block it is writing shows after them as plain text, taken back
/// as it grows; and once its input is whole, the rest goes in as it reads then (see Finish). Each change is a step of the session, which
/// the talk joins into one or takes back as the user decides. Blocks go in before an empty paragraph made for the place (taken away at
/// the end), or before the end of a section of columns while the text is in columns; text written to words or to the cursor goes in
/// among the text there. The objects of the text a write takes away are kept first (see Objects.Keep), to go in again where their
/// tokens are written; an object whose token is written while it stands elsewhere moves here (taken away there at the end).
/// </summary>
sealed partial class Writing(EditSession editing, DocumentView sheets, Focus focus, Objects objects)
{
    readonly IEditableDocument document = editing.Document;
    readonly WordDocument? word = editing.Document as WordDocument;

    /// <summary>How many steps the session had done before the write: what it did is all after them.</summary>
    readonly int before = editing.Steps;

    /// <summary>Whether the place was read and the write began there, and as what; where it could not, why not.</summary>
    bool begun;
    (string Place, int? Paragraph, int? Through)? began;
    string? refused;

    /// <summary>The paragraph blocks go in before; the empty one made for the place, taken away at the end; the ends of the sections of columns open, the place before each.</summary>
    int? anchor, placeholder;
    readonly Stack<int> columns = new();
    readonly HashSet<int> sectionEnds = [];

    /// <summary>Where text goes in among the text (words selected, the cursor) while it does; where it went in, and how much.</summary>
    (int Paragraph, int Offset)? among, amongWritten;
    int amongLength;

    /// <summary>How many of the blocks read went in; a drawing to float beside the paragraph after it, waiting for that paragraph.</summary>
    int done;
    Markup.Made? floating;

    /// <summary>Whether a step shows the block being written, and its text then.</summary>
    bool tailShown;
    Notation.Tail? tail;

    readonly List<int> written = [];
    readonly List<(int Paragraph, int Offset, string Label)> notes = [];
    readonly List<string> problems = [];

    /// <summary>
    /// The header, the footer and the title page the document had as the write took text away that ends a section (whose page setup holds
    /// them): given again at the end where that took them with it (see End).
    /// </summary>
    (string Header, string Footer, bool Title)? stories;

    /// <summary>The objects whose tokens went in while they stood elsewhere, and where: taken away there at the end (they moved).</summary>
    readonly List<(int Id, (int Paragraph, int Offset) From)> moved = [];

    /// <summary>The input as it streams in: the write begins where its place is read, and its content goes in as far as it is written.</summary>
    public void Grew(string json)
    {
        if (refused != null)
            return;
        var members = PartialJson.Members(json);
        if (!begun)
        {
            if (!members.TryGetValue("place", out var place) || !place.Whole)
                return;
            int? paragraph = members.TryGetValue("paragraph", out var p) && p.Whole && int.TryParse(p.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
            int? through = members.TryGetValue("through", out var t) && t.Whole && int.TryParse(t.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var last) ? last : null;
            // A place by a paragraph waits for its id (which the schema has come before the content); one in place of paragraphs waits for the
            // content to begin, by when the last of them is given where it is given at all (see Finish, where it came later).
            if (place.Value != "focus" && paragraph == null || place.Value == "replace" && through == null && !members.ContainsKey("content"))
                return;
            began = (place.Value, paragraph, through);
            if (!Begin(place.Value, paragraph, through))
                return;
        }
        if (members.TryGetValue("content", out var content))
            Update(content.Value, final: false);
    }

    /// <summary>
    /// The input whole: the write as it reads, the place made good (the empty paragraph made for it taken away), its footnotes put in, what
    /// it wrote selected; what is told Claude, and whether the write failed.
    /// </summary>
    public (string Text, bool Error) Finish(string json)
    {
        string? place = null, content = null;
        int? paragraph = null, through = null;
        try
        {
            using var input = JsonDocument.Parse(json);
            var root = input.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return ("The input of write is a JSON object.", true);
            if (root.TryGetProperty("place", out var p) && p.ValueKind == JsonValueKind.String)
                place = p.GetString();
            if (root.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                content = c.GetString();
            if (root.TryGetProperty("paragraph", out var n) && n.ValueKind == JsonValueKind.Number && n.TryGetInt32(out var id))
                paragraph = id;
            if (root.TryGetProperty("through", out var u) && u.ValueKind == JsonValueKind.Number && u.TryGetInt32(out var last))
                through = last;
        }
        catch (JsonException)
        {
            TakeBackAll();
            return (JsonSerializer.Serialize(new Dictionary<string, string> { ["INVALID_JSON"] = json }, PartialJsonContext.Default.DictionaryStringString), true);
        }
        if (place is not ("focus" or "after" or "before" or "replace") || content == null)
        {
            TakeBackAll();
            return ("write needs place (focus, after, before or replace) and content; after, before and replace a paragraph id too.", true);
        }
        // Begun at another place than the input names whole (its last paragraph came after the content): begun again there.
        if (begun && began is var (beganPlace, beganParagraph, beganThrough)
            && (beganPlace != place || beganParagraph != paragraph || (beganThrough ?? beganParagraph) != (through ?? paragraph)))
        {
            TakeBackAll();
            Reset();
        }
        if (!begun && !Begin(place, paragraph, through))
            return (refused ?? "The place could not be written to.", true);
        if (refused != null)
            return (refused, true);
        Update(content, final: true);
        End();
        return (Told(), false);
    }

    /// <summary>The write as before it began, its place not read yet.</summary>
    void Reset()
    {
        (begun, began, refused, anchor, placeholder, among, amongWritten, amongLength, done, floating, tailShown, tail) =
            (false, null, null, null, null, null, null, 0, 0, null, false, null);
        columns.Clear();
        sectionEnds.Clear();
        written.Clear();
        notes.Clear();
        problems.Clear();
        moved.Clear();
        keptTables.Clear();
        parking.Clear();
        tableNotes.Clear();
        stories = null;
    }

    /// <summary>Takes back what the write did so far (input that could not be read): its steps are the session's last.</summary>
    void TakeBackAll()
    {
        tailShown = false;
        sheets.Edit(() =>
        {
            while (editing.Steps > before && editing.TakeBack())
            {
            }
        });
        written.Clear();
    }

    /// <summary>
    /// Makes the place: blocks after or before a paragraph (a table's whole, where it is its first or last paragraph), in place of
    /// paragraphs, or at the focus (see Focus): in place of the empty paragraph, the paragraphs or the table selected, after a picture; text
    /// among the text in place of the words selected or at the cursor.
    /// </summary>
    bool Begin(string place, int? paragraph, int? through)
    {
        begun = true;
        objects.Written();
        if (place != "focus" && (paragraph is not { } id || !document.Contains(id)))
        {
            refused = $"There is no paragraph {(paragraph is { } given ? "¶" + given.ToString(CultureInfo.InvariantCulture) : "given")} to write {place}.";
            return false;
        }
        switch (place)
        {
            case "after":
                return Step(() => (placeholder = anchor = After(paragraph!.Value)) is { } made ? new TextPlace(made, 0) : null);
            case "before":
                return Step(() => (placeholder = anchor = Before(paragraph!.Value)) is { } made ? new TextPlace(made, 0) : null);
            case "replace":
                return Replace(paragraph!.Value, through ?? paragraph!.Value);
        }
        switch (focus.Kind)
        {
            case Focus.Kinds.Empty:
                placeholder = anchor = focus.First;
                return true;
            case Focus.Kinds.Paragraphs or Focus.Kinds.Table:
                return Replace(focus.First, focus.Last);
            case Focus.Kinds.Picture:
                return Step(() => (placeholder = anchor = After(focus.First)) is { } made ? new TextPlace(made, 0) : null);
            case Focus.Kinds.Words:
                among = (focus.First, focus.Offset);
                objects.Keep(focus.First, focus.Offset, focus.Offset + focus.Length);
                return Step(() =>
                {
                    document.Replace(focus.First, focus.Offset, focus.Length, "");
                    return new TextPlace(focus.First, focus.Offset);
                });
            default:
                among = (focus.First, focus.Offset);
                return true;
        }
    }

    /// <summary>Whether a paragraph from one to another (in reading order) ends a section.</summary>
    bool EndsSection(int first, int last)
    {
        for (int? paragraph = first; paragraph is { } at; paragraph = document.Next(at))
        {
            try
            {
                if (word!.SectionEnd(at) == at)
                    return true;
            }
            catch (NotSupportedException)
            {
            }
            if (at == last)
                break;
        }
        return false;
    }

    /// <summary>Whether a table stands right before a paragraph of the body (its last cell's paragraph the one before it in reading order).</summary>
    bool AfterTable(int paragraph) => document.Before(paragraph) == null && document.Previous(paragraph) is { } previous && word?.TableOf(previous) != null;

    /// <summary>Whether a table stands right after a paragraph of the body.</summary>
    bool BeforeTable(int paragraph) => document.After(paragraph) == null && document.Next(paragraph) is { } next && word?.TableOf(next) != null;

    /// <summary>Whether a paragraph is the first, or the last, of a table of the body.</summary>
    bool OpensTable(int paragraph) => word?.TableOf(paragraph) is { } table && word.CellsOf(table) is { } cells && cells.Cells[0].First == paragraph;

    bool ClosesTable(int paragraph) => word?.TableOf(paragraph) is { } table && word.CellsOf(table) is { } cells && cells.Cells[^1].Last == paragraph;

    /// <summary>An empty paragraph made after one: after its table where it ends one, else right after it (in its cell, where it stands in one).</summary>
    int After(int paragraph)
    {
        if (ClosesTable(paragraph) && document.Next(paragraph) is { } next)
        {
            if (word!.TableOf(next) != null)
                throw new NotSupportedException("This table meets the table after it: nothing can go between them. Write before the paragraph after the second table, or replace both tables together.");
            return document.InsertBlocks(next, "<w:p/>", before: true)[0];
        }
        return document.InsertBlocks(paragraph, "<w:p/>")[0];
    }

    /// <summary>An empty paragraph made before one: before its table where it begins one (after the paragraph before the table), else right before it.</summary>
    int Before(int paragraph)
    {
        if (OpensTable(paragraph) && document.Previous(paragraph) is { } previous)
        {
            if (word!.TableOf(previous) != null)
                throw new NotSupportedException("This table meets the table before it: nothing can go between them. Write after the paragraph before the first table, or replace both tables together.");
            return document.InsertBlocks(previous, "<w:p/>")[0];
        }
        return document.InsertBlocks(paragraph, "<w:p/>", before: true)[0];
    }

    /// <summary>
    /// Paragraphs taken away for what is written, from one to another (whole tables where they are a table's first and last): an empty
    /// paragraph made after them, where the blocks go in, everything from the first up to it taken away; their objects kept first (see
    /// Objects.Keep), their tables kept after it to be written again in place (see KeepTables).
    /// </summary>
    bool Replace(int first, int last)
    {
        if (!document.Contains(last))
        {
            refused = $"There is no paragraph ¶{last.ToString(CultureInfo.InvariantCulture)} to replace through.";
            return false;
        }
        objects.Keep(first, last);
        if (word != null && EndsSection(first, last))
            stories = Arranging.Stories(word, objects);
        var tables = TablesIn(first, last);
        // The first paragraph of the text that is in none of those tables: what stands together once they are kept aside.
        int? plain = null;
        if (tables.Count > 0)
        {
            var inTables = tables.SelectMany(t => word!.CellsOf(t)!.Cells.SelectMany(c => c.Paragraphs)).ToHashSet();
            for (int? paragraph = first; paragraph is { } at && plain == null; paragraph = at == last ? null : document.Next(at))
            {
                if (!inTables.Contains(at))
                    plain = at;
            }
        }
        return Step(() =>
        {
            var made = After(last);
            if (tables.Count > 0 && word!.TableOf(made) == null)
            {
                KeepTables(tables, made);
                if (plain is { } start)
                {
                    document.RemoveBetween(start, made);
                    document.Remove(start);
                }
            }
            else if (OpensTable(first))
            {
                if (document.Previous(first) is not { } previous || word!.TableOf(previous) != null)
                    throw new NotSupportedException("A table right at the start of the document, or right after another table, cannot be replaced; write after it and remove it by replacing it together with the paragraph before.");
                document.RemoveBetween(previous, made);
            }
            else
            {
                document.RemoveBetween(first, made);
                document.Remove(first);
            }
            placeholder = anchor = made;
            return new TextPlace(made, 0);
        });
    }

    /// <summary>
    /// A change as a step of the session, the pages following it; false where the document refused it, why kept (where the place could
    /// not be made, as the write's refusal; else among its problems, unless <paramref name="quiet"/>).
    /// </summary>
    bool Step(Func<TextPlace?> change, bool quiet = false)
    {
        Exception? refusal = null;
        sheets.Edit(() =>
        {
            try
            {
                editing.Handle(change);
            }
            catch (Exception e) when (e is NotSupportedException or ArgumentException or InvalidOperationException or System.Xml.XmlException or KeyNotFoundException)
            {
                refusal = e;
            }
        });
        if (refusal == null)
            return true;
        if (anchor == null && among == null)
            refused = refusal.Message;
        else if (!quiet)
            problems.Add(refusal.Message);
        return false;
    }

    /// <summary>The content as far as it is written: the blocks finished put in, the one being written shown after them.</summary>
    void Update(string content, bool final)
    {
        var parsed = Notation.Parse(content, final);
        if (among != null && !Among(parsed, final))
            return;
        HideTail();
        while (done < parsed.Blocks.Count)
        {
            var block = parsed.Blocks[done];
            // A drawing to float beside the text waits for the paragraph after it, and goes into it.
            if (block is Notation.Fenced { Kind: "svg" or "wordart" or "box" or "chart" } fenced && fenced.Settings.GetValueOrDefault("float")?.ToLowerInvariant() is "left" or "right")
            {
                if (done + 1 >= parsed.Blocks.Count && !final)
                    break;
                if (done + 1 < parsed.Blocks.Count && parsed.Blocks[done + 1] is Notation.Paragraph next)
                {
                    floating = Markup.Of(block, PlaceAt());
                    done++;
                    Put(next);
                    done++;
                    continue;
                }
                // Nothing to float beside: in a line of its own.
                var settings = new Dictionary<string, string>(fenced.Settings, StringComparer.OrdinalIgnoreCase);
                settings.Remove("float");
                Put(fenced with { Settings = settings });
                done++;
                continue;
            }
            Put(block);
            done++;
        }
        if (!final)
            ShowTail(parsed.Tail);
        foreach (var problem in parsed.Problems.Where(p => final && !problems.Contains(p)))
            problems.Add(problem);
        if (final)
            Remember(parsed.Notes);
        Frame();
    }

    /// <summary>The footnotes' texts, by their labels, for the marks put in.</summary>
    IReadOnlyDictionary<string, string> noteTexts = new Dictionary<string, string>();

    void Remember(IReadOnlyDictionary<string, string> texts) => noteTexts = texts;

    /// <summary>
    /// Text among the text (words selected, the cursor): the paragraph being written shown at the place, the first one finished put in
    /// there where it is words' (and at the cursor where it is all there is); blocks otherwise after the paragraph, as blocks go (see
    /// Update). Whether blocks are to go in now.
    /// </summary>
    bool Among(Notation.Parsed parsed, bool final)
    {
        var (paragraph, offset) = among!.Value;
        amongWritten = among;
        var words = focus.Kind == Focus.Kinds.Words;
        var one = parsed.Blocks.Count == 1 && parsed.Blocks[0] is Notation.Paragraph { Style: null } && (final || words);
        if (parsed.Blocks.Count == 0 && !final)
        {
            HideTail();
            if (parsed.Tail is { Text.Length: > 0 } shown)
            {
                var text = Safe(shown.Text);
                if (Step(() =>
                {
                    document.Replace(paragraph, offset, 0, text);
                    return new TextPlace(paragraph, offset + text.Length);
                }))
                {
                    tailShown = true;
                    amongLength = text.Length;
                }
            }
            Frame();
            return false;
        }
        HideTail();
        amongLength = 0;
        if (parsed.Blocks.Count > 0 && parsed.Blocks[0] is Notation.Paragraph { Style: null } first && (one || words))
        {
            // The first paragraph among the text at the place, the rest after its paragraph.
            var made = Markup.RunsOf(first.Pieces, Kept);
            if (made.Runs.Length > 0)
            {
                Step(() =>
                {
                    var length = document.Text(paragraph).Length;
                    document.InsertMarkup(paragraph, offset, made.Runs);
                    amongLength = document.Text(paragraph).Length - length;
                    return new TextPlace(paragraph, offset + amongLength);
                });
            }
            notes.AddRange(made.Notes.Select(n => (paragraph, offset + n.Offset, n.Label)));
            done = 1;
        }
        // Blocks from here on after the paragraph.
        var at = paragraph;
        among = null;
        if (done < parsed.Blocks.Count || !final)
            Step(() => (placeholder = anchor = After(at)) is { } after ? new TextPlace(after, 0) : null);
        return true;
    }

    /// <summary>The measures of the place blocks go to now (see Markup.Place).</summary>
    Markup.Place PlaceAt()
    {
        var at = anchor!.Value;
        if (word == null)
            return new(450, new SectionFormatting(), (_, _) => "", Kept);
        SectionFormatting section;
        try
        {
            section = word.SectionOf(at);
        }
        catch (NotSupportedException)
        {
            section = new SectionFormatting();
        }
        return new(word.TextWidth(at), section, (columns, rows) => word.TableMarkup(at, columns, rows, Tables.Table.Style, Tables.Text.Style), Kept)
        {
            Lines = (Tones.Of(document) ?? Tones.All[0]).LineColors(),
            KeptAlign = objects.AlignOf,
        };
    }

    /// <summary>
    /// The markup an object's token goes in as (see Objects.Markup): as kept where the write took it away; as it stands where it stands
    /// elsewhere, to be taken away there at the end (it moves here). Null, and said to Claude, where there is no such object.
    /// </summary>
    string? Kept(int id)
    {
        if (objects.Markup(id) is not var (markup, from))
        {
            problems.Add($"There is no object {id.ToString(CultureInfo.InvariantCulture)} to keep there; its token was left out.");
            return null;
        }
        if (from is { } at && !moved.Any(m => m.Id == id))
            moved.Add((id, at));
        return markup;
    }

    /// <summary>A block put in before the anchor, a step of its own; text in columns begun or ended.</summary>
    void Put(Notation.Block block)
    {
        if (anchor is not { } at)
            return;
        switch (block)
        {
            case Notation.Columns open when word != null:
            {
                // The section before ends here as it was set up, the columns' section after it, the text after them going on on the
                // page; what is in columns goes in before the end of their section.
                var section = PlaceAt().Section;
                var markup = Markup.SectionEnd(section, null) + Markup.SectionEnd(section, (open.Count, open.Line));
                Step(() =>
                {
                    var made = document.InsertBlocks(at, markup, before: true);
                    word.Format(at, new SectionFormatting { Start = "continuous" });
                    sectionEnds.UnionWith(made);
                    columns.Push(at);
                    anchor = made[^1];
                    return new TextPlace(at, 0);
                });
                return;
            }
            case Notation.ColumnsEnd:
                if (columns.Count > 0)
                    anchor = columns.Pop();
                return;
            case Notation.Table kept when word != null && keptTables.Count > 0:
                Rework(Kept(kept), kept);
                return;
        }
        var made = Markup.Of(block, PlaceAt());
        if (made.Problem != null)
        {
            problems.Add(made.Problem);
            return;
        }
        if (made.Blocks.Length == 0)
            return;
        var blocks = made.Blocks;
        var parts = made.Parts.ToList();
        var shift = 0;
        var floats = floating;
        floating = null;
        if (floats != null)
        {
            // The floating drawing's run at the start of the paragraph's runs, after its properties.
            var properties = blocks.StartsWith("<w:p><w:pPr>", StringComparison.Ordinal) ? blocks.IndexOf("</w:pPr>", StringComparison.Ordinal) + "</w:pPr>".Length : "<w:p>".Length;
            blocks = blocks[..properties] + floats.Blocks + blocks[properties..];
            parts.AddRange(floats.Parts);
            shift = 1;
        }
        Step(() =>
        {
            // A table never meets a table before it (Word joins tables that meet): an empty paragraph between them, as Word leaves one.
            if (block is Notation.Table && AfterTable(at))
                document.InsertBlocks(at, "<w:p/>", before: true);
            var ids = document.InsertBlocks(at, blocks, before: true, parts.Count > 0 ? parts : null);
            if (floats != null && ids.Count > 0)
            {
                document.Format(ids[0], 0, new DrawingFormatting { Wrap = "square", HorizontalFrom = "column", HorizontalAlign = floats.Float });
            }
            // A box as high as its text, in a line of its own or floating at the start of the paragraph after it.
            if ((made.Fit || floats?.Fit == true) && word != null && ids.Count > 0)
                word.FitTextBox(ids[0], 0);
            // A new table given what GitHub's tables do not say: its merged cells, its cells' and its own looks; its paragraphs as they
            // then are (merged cells' go), its notes by their cells.
            if (block is Notation.Table table && word != null && ids.Count > 0 && word.TableOf(ids[0]) is { } newTable)
            {
                var across = Math.Max(1, table.Aligns.Count);
                tableNotes.AddRange(made.Notes.Select(n => (newTable, n.Paragraph / across, n.Paragraph % across, n.Offset, n.Label)));
                Merge(newTable, table, split: false);
                Dress(newTable, table);
                var cells = word.CellsOf(newTable)!;
                written.AddRange(cells.Cells.SelectMany(c => c.Paragraphs));
                return new TextPlace(cells.Cells[^1].Last, document.Text(cells.Cells[^1].Last).Length);
            }
            written.AddRange(ids);
            // A numbered point in the list of the points before it, or beginning one: at its number, or going on in the list before
            // whose last point shows the number before it (see Lists).
            if (block is Notation.Paragraph { Style: "ListNumber" or "ListNumber2" } point)
                Lists.Settle(document, ids, point.Number ?? 1, ids.Count > 0 ? Lists.Continued(document, ids[0], point.Number) : null);
            notes.AddRange(made.Notes.Where(n => n.Paragraph < ids.Count).Select(n => (ids[n.Paragraph], n.Offset + (n.Paragraph == 0 ? shift : 0), n.Label)));
            return ids.Count > 0 ? new TextPlace(ids[^1], document.Text(ids[^1]).Length) : null;
        });
    }

    /// <summary>The block being written shown after those put in: a paragraph of its style with its text so far, a step of its own.</summary>
    void ShowTail(Notation.Tail? now)
    {
        tail = now;
        if (anchor is not { } at || now is not { Text.Length: > 0 } shown)
            return;
        // A numbered point counting on in the list before it as it will once it is put in.
        var list = shown.Style is "ListNumber" or "ListNumber2" && Lists.Before(document, at) is { } before
            ? $"<w:numPr><w:ilvl w:val=\"{(shown.Style == "ListNumber" ? 0 : 1)}\"/><w:numId w:val=\"{before}\"/></w:numPr>" : "";
        var style = shown.Style is { } s ? $"<w:pPr><w:pStyle w:val=\"{SecurityElement.Escape(s)}\"/>{list}</w:pPr>" : "";
        var markup = $"<w:p>{style}<w:r><w:t xml:space=\"preserve\">{SecurityElement.Escape(Safe(shown.Text))}</w:t></w:r></w:p>";
        tailShown = Step(() => document.InsertBlocks(at, markup, before: true) is [var id, ..] ? new TextPlace(id, document.Text(id).Length) : null);
    }

    void HideTail()
    {
        if (!tailShown)
            return;
        tailShown = false;
        sheets.Edit(() => editing.TakeBack());
    }

    /// <summary>Text that XML holds: no control characters, no half of a pair of surrogates at its end (a stream cut there).</summary>
    static string Safe(string text)
    {
        var kept = new string([.. text.Where(c => c == '\t' || c >= ' ')]);
        return kept.Length > 0 && char.IsHighSurrogate(kept[^1]) ? kept[..^1] : kept;
    }

    /// <summary>
    /// The write made good: the empty paragraph made for the place taken away (where it is the last of its body or cell, joined with the
    /// paragraph before it), the footnotes put in at their marks.
    /// </summary>
    void End()
    {
        // The tables kept that were not written again taken away first, so that the place is seen as it then stands.
        EndTables();
        // The empty paragraph made for the place stays where it stands between two tables, which would join without it.
        if (placeholder is { } made && document.Contains(made) && !(AfterTable(made) && BeforeTable(made)))
        {
            var removed = Step(() =>
            {
                document.Remove(made);
                return written.Count > 0 && document.Contains(written[^1]) ? new TextPlace(written[^1], document.Text(written[^1]).Length) : null;
            }, quiet: true);
            if (!removed && document.Before(made) is { } previous && !sectionEnds.Contains(previous) && written.Contains(previous))
                Step(() => new TextPlace(document.Join(previous, backward: true), document.Text(previous).Length), quiet: true);
        }
        // The numbered points written one list with the points they now meet, that place's paragraph gone.
        if (written.Where(document.Contains).Where(p => Levels.Of(document.ParagraphStyle(p)).Numbered).ToList() is { Count: > 0 } numbered)
            Step(() =>
            {
                Lists.Settle(document, numbered);
                return null;
            }, quiet: true);
        // The notes in tables' cells at their cells' first paragraphs as they are now (a table changed is read anew).
        foreach (var (table, row, column, offset, label) in tableNotes)
        {
            if (word?.CellsOf(table)?.Cells.FirstOrDefault(c => c.Top == row && c.Left == column) is { } cell)
                notes.Add((cell.First, offset, label));
        }
        // The notes at their marks, from the last mark of a paragraph back, so that the marks before stay where they are: with the text the
        // write gives them, else with the text of the note of that label (one the write took away goes in again so).
        foreach (var (paragraph, offset, label) in notes.OrderByDescending(n => n.Offset))
        {
            var known = objects.NoteOf(label);
            if ((noteTexts.TryGetValue(label, out var given) ? given : known?.Text) is not { } text)
            {
                problems.Add($"The footnote [^{label}] has no text: give it as [^{label}]: text.");
                continue;
            }
            var endnote = known?.Endnote == true;
            var runs = Markup.RunsOf(Notation.Inline(text, null), Kept).Runs;
            if (Step(() =>
            {
                document.InsertNote(paragraph, Math.Min(offset, document.Text(paragraph).Length), runs, endnote);
                return null;
            }))
                objects.Noted(label, text, endnote);
        }
        objects.Written();
        // The header, footer and title page that went with a section's end taken away, given again.
        if (stories is var (header, footer, title) && word?.Beginning is { } beginning && Arranging.Stories(word, objects) is var (header2, footer2, title2))
        {
            if (header.Length > 0 && header2.Length == 0)
                Step(() =>
                {
                    Arranging.Story(word, beginning, footer: false, header);
                    return null;
                }, quiet: true);
            if (footer.Length > 0 && footer2.Length == 0)
                Step(() =>
                {
                    Arranging.Story(word, beginning, footer: true, footer);
                    return null;
                }, quiet: true);
            if (title && !title2)
                Step(() =>
                {
                    word.Format(beginning, new SectionFormatting { TitlePage = true });
                    return null;
                }, quiet: true);
        }
        // The objects that moved here taken away where they stood, the last of a paragraph first.
        foreach (var (id, (paragraph, offset)) in moved.OrderByDescending(m => m.From.Offset))
        {
            if (!objects.Stands(id, (paragraph, offset)))
            {
                problems.Add($"Object {id.ToString(CultureInfo.InvariantCulture)} could not be taken away where it stood before; it stands there too.");
                continue;
            }
            Step(() =>
            {
                document.Replace(paragraph, offset, 1, "");
                return null;
            }, quiet: true);
        }
        Frame();
    }

    /// <summary>What was written selected, so that the ring frames it (the session hides the selection while Claude writes, see Talk).</summary>
    void Frame()
    {
        var alive = written.Where(document.Contains).ToList();
        sheets.Edit(() =>
        {
            if (alive.Count == 0 && amongWritten is var (paragraph, offset) && amongLength > 0)
            {
                editing.Place(new TextPlace(paragraph, offset));
                editing.Place(new TextPlace(paragraph, offset + amongLength), extend: true);
            }
            else if (alive.Count > 0)
            {
                editing.Place(new TextPlace(alive[0], 0));
                var last = tailShown && anchor is { } at && document.Before(at) is { } shown ? shown : alive[^1];
                editing.Place(new TextPlace(last, document.Text(last).Length), extend: true);
            }
        });
    }

    /// <summary>What is told Claude once the write is in: what it wrote, where, and what could not be read.</summary>
    string Told()
    {
        var alive = written.Where(document.Contains).ToList();
        var told = new System.Text.StringBuilder();
        if (alive.Count == 0 && amongWritten is var (paragraph, _) && amongLength > 0)
            told.Append(CultureInfo.InvariantCulture, $"Written into ¶{paragraph}.");
        else if (alive.Count == 0)
            told.Append("Nothing was written.");
        else
        {
            told.Append("Written: ");
            told.Append(alive.Count == 1 ? $"¶{alive[0]}" : $"¶{alive[0]} to ¶{alive[^1]} ({alive.Count} paragraphs, table cells counted one by one)");
            told.Append('.');
            if (document.PageOf(alive[0], 0) is { } from && document.PageOf(alive[^1], 0) is { } to)
                told.Append(CultureInfo.InvariantCulture, $" On page {from + 1}{(to > from ? $" to {to + 1}" : "")} of {document.Scenes().Count}.");
        }
        if (problems.Count > 0)
            told.Append(" Problems: ").Append(string.Join(" ", problems.Distinct()));
        return told.ToString();
    }
}

/// <summary>JSON written without reflection (NativeAOT).</summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, string>))]
sealed partial class PartialJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
