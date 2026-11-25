using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Editing;
using FreeSlate.Opc;

namespace FreeSlate;

/// <summary>The kind of a tracked change, as Word's Revisions name it (WdRevisionType).</summary>
public enum RevisionKind
{
    /// <summary>Text, paragraphs or a table's rows inserted (w:ins; wdRevisionInsert).</summary>
    Insertion,

    /// <summary>Text, paragraphs' marks or a table's rows deleted (w:del; wdRevisionDelete).</summary>
    Deletion,

    /// <summary>Text moved away from here (w:moveFrom; wdRevisionMovedFrom).</summary>
    MovedFrom,

    /// <summary>Text moved here (w:moveTo; wdRevisionMovedTo).</summary>
    MovedTo,

    /// <summary>Characters formatted (w:rPrChange; wdRevisionProperty).</summary>
    Formatting,

    /// <summary>Paragraphs formatted (w:pPrChange; wdRevisionParagraphProperty).</summary>
    ParagraphFormatting,

    /// <summary>A table, its rows or cells formatted (w:tblPrChange, w:trPrChange, w:tcPrChange …; wdRevisionTableProperty).</summary>
    TableFormatting,

    /// <summary>A section's page set up otherwise (w:sectPrChange; wdRevisionSectionProperty).</summary>
    SectionFormatting,
}

/// <summary>
/// A tracked change of a Word document's body as Word's Review tab lists it (catalog: review.revisions): its id (the w:id of its first
/// element, by which it is accepted or rejected), kind, author and time; where it begins and ends in the text as it reads with the
/// changes accepted (a deletion where it stands); its text as Word's Range.Text gives it ("\r" a paragraph's mark, "\a" after a cell's
/// or row's end, "\t" a tab, U+FFFC an object), a formatting change's description in English as Word's balloons word it; and for a move
/// the id of its other half (Partner), which Word accepts and rejects with it.
/// </summary>
public sealed record WordRevision(int Id, RevisionKind Kind, string Author, DateTime? Date, TextPlace Start, TextPlace End, string Text, string Description)
{
    public int? Partner { get; init; }
}

/// <summary>
/// A tracked change as it stands in one paragraph's text as it reads with the changes accepted (see WordDocument.RevisionsOf): the
/// change's id and kind; for an insertion, a move here or a formatting change the stretch of the text it covers (Offset, Length); for a
/// deletion or a move away the place it stands and the text deleted there (Deleted, "\r" for a paragraph's mark, which joined the
/// paragraphs around it); for the paragraph's own mark (Mark) its insertion or its paragraph's formatting change, at the text's end.
/// </summary>
public sealed record RevisionSpan(int Revision, RevisionKind Kind, int Offset, int Length, string? Deleted = null, bool Mark = false);

/// <summary>
/// The tracked changes of a Word document as Word's Review tab shows, accepts and rejects them (catalog: review.revisions), and its
/// Track Changes setting. Revisions lists the body's as Word's Document.Revisions does (measured in Word 365 by COM with probes of every
/// kind, scratchpad gaps/A/makeprobe.py and revs.ps1): neighbouring elements of a kind and an author are one revision, whatever their
/// times (an insertion running on over an inserted paragraph mark, a deletion within it its own), a paragraph's mark one character of
/// it, an inserted or deleted table row its cells' text with their marks and the row's end; adjacent formatting changes of runs of one
/// author one, as are adjacent paragraphs' formatting changes; a table's formatting changes (table, grid, rows, cells) one for each of
/// its authors; each section's own; ordered by where they begin. Accepting and rejecting go by the XML of the parts (see
/// TextEdits.Settle), each a change of its own to undo.
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>A character or more of the body as Word counts them for its revisions, in reading order, with the changes it belongs to.</summary>
    sealed class Entry
    {
        public string Text = "";

        /// <summary>The paragraph of the text as it reads accepted it stands in (null till known: a deleted row's), the place there and how many characters it takes.</summary>
        public int? Paragraph;
        public int Offset, Length;

        /// <summary>The changes it belongs to: an insertion or move here, a deletion or move away, a formatting change, its paragraph's formatting change (marks only).</summary>
        public XElement? Insertion, Deletion, Formatting, ParagraphChange;

        /// <summary>Whether it is a paragraph's mark (Own: the last part's, the paragraph's as the text reads), or the end of a table's row (Row).</summary>
        public bool Mark, Own, Row;

        /// <summary>The revisions it is in, by their places in the list (-1 for none).</summary>
        public int InsertionOf = -1, DeletionOf = -1, FormattingOf = -1, ParagraphOf = -1;

        /// <summary>For a paragraph's mark: where its paragraph's entries begin.</summary>
        public int ParagraphStart;
    }

    /// <summary>A revision as listed, with the elements it is made of and the entries it covers.</summary>
    sealed class Revised(RevisionKind kind, string author, DateTime? date)
    {
        public RevisionKind Kind = kind;
        public string Author = author;
        public DateTime? Date = date;
        public readonly List<XElement> Elements = [];
        public int First = -1, Last = -1;
        public string? Move;
        public string Description = "";
        public int Id;
        public int? Partner;
        public string? Text;
    }

    /// <summary>The body's revisions as read last, and what for (the blocks and the changed stretches of that version).</summary>
    (List<Block>? Blocks, ImmutableList<Island>? Islands, List<Entry> Entries, List<Revised> Revisions, Dictionary<string, List<XElement>> Moves)? reviewed;

    /// <summary>The entries of the revisions read last by the paragraphs they stand in (see RevisionsOf).</summary>
    (List<Entry>? Of, Dictionary<int, List<Entry>>? Index) entriesIndexed;

    /// <summary>The body's tracked changes as Word lists them (see the class's summary).</summary>
    public IReadOnlyList<WordRevision> Revisions()
    {
        var (entries, revisions, _) = Reviewed();
        return [.. revisions.Select(r => Public(r, entries))];
    }

    /// <summary>The tracked changes of a paragraph of the body (of a table's cell too) as they stand in its text (see RevisionSpan), in order.</summary>
    public IReadOnlyList<RevisionSpan> RevisionsOf(int paragraph)
    {
        var (entries, revisions, _) = Reviewed();
        var spans = new List<RevisionSpan>();
        void Add(RevisionSpan span)
        {
            // Pieces of one change meeting are one.
            if (spans.Count > 0 && spans[^1] is var last && last.Revision == span.Revision && last.Mark == span.Mark)
            {
                if (span.Deleted != null && last.Deleted != null && last.Offset == span.Offset)
                {
                    spans[^1] = last with { Deleted = last.Deleted + span.Deleted };
                    return;
                }
                if (span.Deleted == null && last.Deleted == null && last.Offset + last.Length == span.Offset)
                {
                    spans[^1] = last with { Length = last.Length + span.Length };
                    return;
                }
            }
            spans.Add(span);
        }
        // The entries by their paragraphs, once for the entries read (a reading asks for each paragraph).
        if (!ReferenceEquals(entriesIndexed.Of, entries))
        {
            var index = new Dictionary<int, List<Entry>>();
            foreach (var e in entries)
            {
                if (e.Paragraph is { } id)
                {
                    if (!index.TryGetValue(id, out var list))
                        index[id] = list = [];
                    list.Add(e);
                }
            }
            entriesIndexed = (entries, index);
        }
        foreach (var entry in entriesIndexed.Index!.GetValueOrDefault(paragraph) ?? [])
        {
            if (entry.Mark && entry.Own)
            {
                if (entry.InsertionOf >= 0)
                    Add(new RevisionSpan(revisions[entry.InsertionOf].Id, revisions[entry.InsertionOf].Kind, entry.Offset, 0, Mark: true));
                if (entry.ParagraphOf >= 0)
                    Add(new RevisionSpan(revisions[entry.ParagraphOf].Id, RevisionKind.ParagraphFormatting, entry.Offset, 0, Mark: true));
                continue;
            }
            if (entry.Row)
                continue;
            if (entry.DeletionOf >= 0 && entry.Length == 0)
            {
                Add(new RevisionSpan(revisions[entry.DeletionOf].Id, revisions[entry.DeletionOf].Kind, entry.Offset, 0, entry.Text));
                continue;
            }
            if (entry.Length == 0)
                continue;
            if (entry.InsertionOf >= 0)
                Add(new RevisionSpan(revisions[entry.InsertionOf].Id, revisions[entry.InsertionOf].Kind, entry.Offset, entry.Length));
            if (entry.FormattingOf >= 0)
                Add(new RevisionSpan(revisions[entry.FormattingOf].Id, RevisionKind.Formatting, entry.Offset, entry.Length));
        }
        // A change's own pieces apart from those of others: insertions and formatting by their places, the deletions where they stand.
        return [.. spans.OrderBy(s => s.Offset).ThenBy(s => s.Deleted != null ? 0 : s.Mark ? 2 : 1)];
    }

    /// <summary>
    /// The XML of a revision's elements as the document holds them (each written as copying writes markup, with the prefixes of the
    /// main part): its insertions and deletions with what they hold, the w:rPrChange, w:pPrChange of a formatting change, a paragraph's
    /// w:pPr for a deleted mark (the properties of the paragraph it joins); with a move's range marks. Null where there is none so.
    /// </summary>
    public IReadOnlyList<string>? RevisionMarkup(int id)
    {
        var (_, revisions, moves) = Reviewed();
        if (revisions.FirstOrDefault(r => r.Id == id) is not { } revision)
            return null;
        var written = new List<string>();
        foreach (var element in revision.Elements)
        {
            // A mark's change stands for its paragraph's properties.
            var shown = element.Parent is { Name.LocalName: "rPr" } run && run.Parent is { Name.LocalName: "pPr" } properties && element.Name.LocalName is "ins" or "del" or "moveFrom" or "moveTo"
                ? properties : element;
            written.Add(shown.ToString(SaveOptions.DisableFormatting));
        }
        if (revision.Move is { } name && moves.TryGetValue(name, out var marks))
            written.AddRange(marks.Select(m => m.ToString(SaveOptions.DisableFormatting)));
        return written;
    }

    WordRevision Public(Revised r, List<Entry> entries)
    {
        var (first, last) = (entries[r.First], entries[r.Last]);
        var start = new TextPlace(first.Paragraph ?? 0, first.Offset);
        var end = r.Kind is RevisionKind.Deletion or RevisionKind.MovedFrom ? start : new TextPlace(last.Paragraph ?? 0, last.Offset + last.Length);
        return new WordRevision(r.Id, r.Kind, r.Author, r.Date, start, end, r.Text ?? "", r.Description) { Partner = r.Partner };
    }

    /// <summary>The body's revisions read where they were not for the document as it reads now (see reviewed).</summary>
    (List<Entry> Entries, List<Revised> Revisions, Dictionary<string, List<XElement>> Moves) Reviewed()
    {
        Read();
        if (reviewed is var (b, i, e, r, m) && ReferenceEquals(b, blocks) && ReferenceEquals(i, islands))
            return (e, r, m);
        var walk = new RevisionWalk(this);
        if (body != null)
            walk.Blocks(body);
        var (entries, revisions, moves) = walk.Done();
        reviewed = (blocks, islands, entries, revisions, moves);
        return (entries, revisions, moves);
    }

    /// <summary>
    /// The walk over the body that finds its revisions (see Reviewed): its elements in reading order as entries (each character or more
    /// of the text, each paragraph's mark, each row's end), with the changes around them; text boxes left out (Word counts them a story
    /// of their own). Then the revisions made of them (see Done).
    /// </summary>
    sealed class RevisionWalk
    {
        static readonly XNamespace W = Wml.W, M = "http://schemas.openxmlformats.org/officeDocument/2006/math";
        readonly WordDocument document;
        readonly Dictionary<XElement, ParagraphBlock> byElement = new(ReferenceEqualityComparer.Instance);
        readonly List<Entry> entries = [];
        readonly List<(XElement Table, int First, int Last, List<XElement> Changes)> tables = [];
        readonly List<(XElement Change, int First, int Last)> sections = [];
        readonly Dictionary<string, List<XElement>> moves = new(StringComparer.Ordinal);
        readonly Dictionary<XElement, string> moveNames = new(ReferenceEqualityComparer.Instance);
        readonly List<(string Id, string Name)> openFrom = [], openTo = [];
        ParagraphBlock? current;
        Dictionary<XElement, (int Start, int Length, string Text)> offsets = new(ReferenceEqualityComparer.Instance);
        int at, sectionStart;

        public RevisionWalk(WordDocument document)
        {
            this.document = document;
            foreach (var (paragraph, _) in document.paragraphs.Values)
            {
                foreach (var element in paragraph.Parts ?? [paragraph.Element])
                    byElement.TryAdd(element, paragraph);
            }
        }

        public void Blocks(XElement container)
        {
            foreach (var e in container.Elements())
            {
                if (e.Name.Namespace != W)
                    continue;
                switch (e.Name.LocalName)
                {
                    case "p":
                        Paragraph(e);
                        break;
                    case "tbl":
                        Table(e);
                        break;
                    case "sdt" when e.Element(W + "sdtContent") is { } content:
                        Blocks(content);
                        break;
                    case "customXml":
                        Blocks(e);
                        break;
                    case "moveFromRangeStart" or "moveToRangeStart" or "moveFromRangeEnd" or "moveToRangeEnd":
                        Range(e);
                        break;
                    case "sectPr":
                        Section(e);
                        break;
                }
            }
        }

        void Paragraph(XElement p)
        {
            var model = byElement.GetValueOrDefault(p);
            if (!ReferenceEquals(model, current) || model == null)
            {
                current = model;
                offsets = Offsets(model);
                at = model?.TextOffset ?? 0;
            }
            var start = entries.Count;
            Inline(p, null, null);
            var properties = p.Element(W + "pPr");
            var mark = properties?.Element(W + "rPr");
            static bool Inserting(XElement e) => e.Name.LocalName is "ins" or "moveTo";
            static bool Deleting(XElement e) => e.Name.LocalName is "del" or "moveFrom";
            var entry = new Entry
            {
                Text = "\r" + (LastInCell(p) ? "\a" : ""), Paragraph = model?.Id, Offset = at, Mark = true, Own = model != null && ReferenceEquals(model.Element, p),
                Insertion = mark?.Elements().LastOrDefault(e => e.Name.Namespace == W && Inserting(e)),
                Deletion = mark?.Elements().LastOrDefault(e => e.Name.Namespace == W && Deleting(e)),
                Formatting = mark?.Element(W + "rPrChange"), ParagraphChange = properties?.Element(W + "pPrChange"), ParagraphStart = start,
            };
            Named(entry.Insertion);
            Named(entry.Deletion);
            entries.Add(entry);
            if (properties?.Element(W + "sectPr") is { } section)
                Section(section);
        }

        /// <summary>Whether a paragraph is the last block of its table cell, whose mark ends the cell.</summary>
        static bool LastInCell(XElement p) =>
            p.Parent is { Name.LocalName: "tc" } && !p.ElementsAfterSelf().Any(e => e.Name.LocalName is "p" or "tbl" or "sdt");

        void Inline(XElement parent, XElement? insertion, XElement? deletion)
        {
            foreach (var e in parent.Elements())
            {
                if (e.Name.Namespace == M)
                {
                    Math(e, insertion, deletion);
                    continue;
                }
                if (e.Name.Namespace != W)
                    continue;
                switch (e.Name.LocalName)
                {
                    case "r":
                        Run(e, insertion, deletion);
                        break;
                    case "ins" or "moveTo":
                        Named(e);
                        Inline(e, e, deletion);
                        break;
                    case "del" or "moveFrom":
                        Named(e);
                        Inline(e, insertion, e);
                        break;
                    case "hyperlink" or "smartTag" or "customXml" or "fldSimple" or "bdo" or "dir":
                        Inline(e, insertion, deletion);
                        break;
                    case "sdt" when e.Element(W + "sdtContent") is { } content:
                        Inline(content, insertion, deletion);
                        break;
                    case "moveFromRangeStart" or "moveToRangeStart" or "moveFromRangeEnd" or "moveToRangeEnd":
                        Range(e);
                        break;
                }
            }
        }

        /// <summary>An equation's runs: text and changes in their w:rPr (a math run inserted or deleted), as Word marks them.</summary>
        void Math(XElement element, XElement? insertion, XElement? deletion)
        {
            foreach (var r in element.DescendantsAndSelf(M + "r"))
            {
                var properties = r.Element(W + "rPr");
                var ins = properties?.Elements().FirstOrDefault(e => e.Name == W + "ins" || e.Name == W + "moveTo") ?? insertion;
                var del = properties?.Elements().FirstOrDefault(e => e.Name == W + "del" || e.Name == W + "moveFrom") ?? deletion;
                var text = string.Concat(r.Elements(M + "t").Select(t => t.Value));
                if (text.Length > 0)
                    entries.Add(new Entry { Text = text, Paragraph = current?.Id, Offset = at, Insertion = ins, Deletion = del });
            }
        }

        void Run(XElement r, XElement? insertion, XElement? deletion)
        {
            var formatting = r.Element(W + "rPr")?.Element(W + "rPrChange");
            foreach (var c in r.Elements())
            {
                if (c.Name.Namespace != W)
                    continue;
                string? text = c.Name.LocalName switch
                {
                    "t" or "delText" => c.Value,
                    "tab" or "ptab" => "\t",
                    "br" => c.Attr("type") switch { "page" => "\f", "column" => "\u000E", _ => "\v" },
                    "cr" => "\v",
                    "instrText" or "delInstrText" or "fldChar" => "",
                    "noBreakHyphen" => "‑",
                    "softHyphen" => "­",
                    "sym" when c.Attr("char") is { } code && int.TryParse(code, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) => ((char)value).ToString(),
                    "drawing" or "pict" or "object" or "footnoteReference" or "endnoteReference" or "ruby" or "contentPart" => "￼",
                    _ => null,
                };
                if (text == null)
                    continue;
                var entry = new Entry { Text = text, Paragraph = current?.Id, Offset = at, Insertion = insertion, Deletion = deletion, Formatting = formatting };
                if (deletion == null && offsets.TryGetValue(c, out var mapped))
                {
                    (entry.Offset, entry.Length, entry.Text) = mapped;
                    at = mapped.Start + mapped.Length;
                }
                entries.Add(entry);
            }
        }

        /// <summary>Where the inlines of a paragraph begin in its text, by the elements they were read from.</summary>
        static Dictionary<XElement, (int Start, int Length, string Text)> Offsets(ParagraphBlock? paragraph)
        {
            var offsets = new Dictionary<XElement, (int, int, string)>(ReferenceEqualityComparer.Instance);
            if (paragraph == null)
                return offsets;
            var at = paragraph.TextOffset;
            foreach (var inline in paragraph.Content)
            {
                var length = ParagraphText.Length(inline);
                if (inline.Xml is { } xml && length > 0)
                {
                    var text = inline switch
                    {
                        TextInline t => t.Text,
                        TabInline => "\t",
                        BreakInline b => b.Type switch { "page" => "\f", "column" => "\u000E", _ => "\v" },
                        _ => "￼",
                    };
                    offsets[xml] = offsets.TryGetValue(xml, out var had) ? (had.Item1, had.Item2 + length, had.Item3 + text) : (at, length, text);
                }
                at += length;
            }
            return offsets;
        }

        void Table(XElement table)
        {
            var first = entries.Count;
            var changes = new List<XElement>();
            void Changed(XElement? properties, string name)
            {
                if (properties?.Element(W + name) is { } change)
                    changes.Add(change);
            }
            Changed(table.Element(W + "tblPr"), "tblPrChange");
            Changed(table.Element(W + "tblGrid"), "tblGridChange");
            foreach (var row in Rows(table))
            {
                var properties = row.Element(W + "trPr");
                Changed(properties, "trPrChange");
                Changed(row.Element(W + "tblPrEx"), "tblPrExChange");
                foreach (var cell in Cells(row))
                {
                    Changed(cell.Element(W + "tcPr"), "tcPrChange");
                    Blocks(cell);
                }
                entries.Add(new Entry
                {
                    Text = "\r\a", Paragraph = current?.Id, Offset = at, Mark = true, Row = true,
                    Insertion = properties?.Element(W + "ins"), Deletion = properties?.Element(W + "del"),
                });
            }
            if (changes.Count > 0 && entries.Count > first)
                tables.Add((table, first, entries.Count - 1, changes));
        }

        /// <summary>A table's rows, those in content controls and custom XML around rows too.</summary>
        internal static IEnumerable<XElement> Rows(XElement table)
        {
            foreach (var e in table.Elements())
            {
                if (e.Name == W + "tr")
                    yield return e;
                else if (e.Name == W + "sdt" && e.Element(W + "sdtContent") is { } content)
                    foreach (var row in Rows(content))
                        yield return row;
                else if (e.Name == W + "customXml")
                    foreach (var row in Rows(e))
                        yield return row;
            }
        }

        internal static IEnumerable<XElement> Cells(XElement row)
        {
            foreach (var e in row.Elements())
            {
                if (e.Name == W + "tc")
                    yield return e;
                else if (e.Name == W + "sdt" && e.Element(W + "sdtContent") is { } content)
                    foreach (var cell in Cells(content))
                        yield return cell;
                else if (e.Name == W + "customXml")
                    foreach (var cell in Cells(e))
                        yield return cell;
            }
        }

        void Section(XElement properties)
        {
            if (properties.Element(W + "sectPrChange") is { } change && entries.Count > sectionStart)
                sections.Add((change, sectionStart, entries.Count - 1));
            sectionStart = entries.Count;
        }

        /// <summary>A move's range begun or ended: the changes of a move within it are known by its name.</summary>
        void Range(XElement e)
        {
            var (from, starting) = (e.Name.LocalName.StartsWith("moveFrom", StringComparison.Ordinal), e.Name.LocalName.EndsWith("Start", StringComparison.Ordinal));
            var open = from ? openFrom : openTo;
            var id = e.Attr("id") ?? "";
            if (starting)
            {
                var name = e.Attr("name") ?? "";
                open.Add((id, name));
                if (!moves.TryGetValue(name, out var marks))
                    moves[name] = marks = [];
                marks.Add(e);
            }
            else if (open.FindLastIndex(o => o.Id == id) is var k and >= 0)
            {
                if (moves.TryGetValue(open[k].Name, out var marks))
                    marks.Add(e);
                open.RemoveAt(k);
            }
        }

        /// <summary>A move's change known by the name of the range open around it.</summary>
        void Named(XElement? change)
        {
            if (change == null || change.Name.LocalName is not ("moveFrom" or "moveTo"))
                return;
            var open = change.Name.LocalName == "moveFrom" ? openFrom : openTo;
            if (open.Count > 0)
                moveNames[change] = open[^1].Name;
        }

        /// <summary>
        /// The revisions the entries make (see the summary of WordDocument.Revisions), ordered by where they begin; the entries' places
        /// where they stand in no paragraph of the text as it reads (a deleted row's) the place of the next that does.
        /// </summary>
        public (List<Entry>, List<Revised>, Dictionary<string, List<XElement>>) Done()
        {
            Section(document.BodySection() ?? new XElement(W + "sectPr"));
            for (var k = entries.Count - 1; k >= 0; k--)
            {
                if (entries[k].Paragraph != null)
                    continue;
                var following = k + 1 < entries.Count ? entries[k + 1] : null;
                var previous = entries.Take(k).LastOrDefault(e => e.Paragraph != null);
                (entries[k].Paragraph, entries[k].Offset) = following?.Paragraph != null ? (following.Paragraph, following.Offset)
                    : previous != null ? (previous.Paragraph, previous.Offset + previous.Length) : (0, 0);
            }
            var revisions = new List<(Revised Revision, int Rank)>();
            // Changes of one kind and author one after the other: an insertion's, a deletion's, a formatting change's.
            void Track(Func<Entry, XElement?> change, Func<XElement, RevisionKind> kind, Action<Entry, int> member, int rank)
            {
                Revised? open = null;
                for (var k = 0; k < entries.Count; k++)
                {
                    var entry = entries[k];
                    var element = change(entry);
                    if (element == null)
                    {
                        open = null;
                        continue;
                    }
                    var (kindOf, author) = (kind(element), element.Attr("author") ?? "");
                    if (open == null || open.Kind != kindOf || open.Author != author)
                    {
                        open = new Revised(kindOf, author, Date(element)) { First = k };
                        revisions.Add((open, rank));
                    }
                    open.Last = k;
                    if (!open.Elements.Contains(element))
                        open.Elements.Add(element);
                    if (moveNames.TryGetValue(element, out var name))
                        open.Move ??= name;
                    member(entry, revisions.Count - 1);
                }
            }
            Track(e => e.Insertion, e => e.Name.LocalName == "moveTo" ? RevisionKind.MovedTo : RevisionKind.Insertion, (e, k) => e.InsertionOf = k, 3);
            Track(e => e.Deletion, e => e.Name.LocalName == "moveFrom" ? RevisionKind.MovedFrom : RevisionKind.Deletion, (e, k) => e.DeletionOf = k, 3);
            Track(e => e.Formatting, _ => RevisionKind.Formatting, (e, k) => e.FormattingOf = k, 4);
            // Paragraphs' formatting changes, by their marks: those of one author one after the other.
            Revised? paragraphs = null;
            for (var k = 0; k < entries.Count; k++)
            {
                var entry = entries[k];
                if (!entry.Mark || entry.Row)
                    continue;
                if (entry.ParagraphChange is not { } change)
                {
                    paragraphs = null;
                    continue;
                }
                var author = change.Attr("author") ?? "";
                if (paragraphs == null || paragraphs.Author != author)
                {
                    paragraphs = new Revised(RevisionKind.ParagraphFormatting, author, Date(change)) { First = entry.ParagraphStart };
                    revisions.Add((paragraphs, 2));
                }
                paragraphs.Last = k;
                paragraphs.Elements.Add(change);
                entry.ParagraphOf = revisions.Count - 1;
            }
            foreach (var (_, first, last, changes) in tables)
            {
                foreach (var group in changes.GroupBy(c => c.Attr("author") ?? ""))
                {
                    var table = new Revised(RevisionKind.TableFormatting, group.Key, Date(group.First())) { First = first, Last = last };
                    table.Elements.AddRange(group);
                    revisions.Add((table, 1));
                }
            }
            foreach (var (change, first, last) in sections)
            {
                var section = new Revised(RevisionKind.SectionFormatting, change.Attr("author") ?? "", Date(change)) { First = first, Last = last };
                section.Elements.Add(change);
                revisions.Add((section, 0));
            }
            var describe = new Describe(CultureInfo.InvariantCulture);
            foreach (var (r, _) in revisions)
            {
                var covered = entries.Skip(r.First).Take(r.Last - r.First + 1);
                r.Text = r.Kind switch
                {
                    RevisionKind.SectionFormatting => "",
                    RevisionKind.Insertion or RevisionKind.MovedTo => string.Concat(covered.Where(e => e.Insertion != null && r.Elements.Contains(e.Insertion)).Select(e => e.Text)),
                    RevisionKind.Deletion or RevisionKind.MovedFrom => string.Concat(covered.Where(e => e.Deletion != null && r.Elements.Contains(e.Deletion)).Select(e => e.Text)),
                    RevisionKind.Formatting => string.Concat(covered.Where(e => e.Formatting != null && r.Elements.Contains(e.Formatting)).Select(e => e.Text)),
                    _ => string.Concat(covered.Select(e => e.Text)),
                };
                r.Description = r.Kind switch
                {
                    RevisionKind.Formatting when r.Elements[0].Parent is { } properties => describe.Run(properties, r.Elements[0].Element(W + "rPr")),
                    RevisionKind.ParagraphFormatting when r.Elements[0].Parent is { } properties => ParagraphChange(describe, properties, r.Elements[0].Element(W + "pPr")),
                    RevisionKind.TableFormatting => "Formatted Table",
                    RevisionKind.SectionFormatting when r.Elements[0].Parent is { } properties => SectionChange(properties, r.Elements[0].Element(W + "sectPr")),
                    _ => "",
                };
            }
            var ordered = revisions.Select((r, k) => (r.Revision, r.Rank, k)).OrderBy(r => r.Revision.First).ThenBy(r => r.Rank).ThenBy(r => r.k).Select(r => r.Revision).ToList();
            // Each by the w:id of its first element, another where that is taken (a document with ids twice).
            var taken = new HashSet<int>();
            var next = ordered.SelectMany(r => r.Elements).Select(e => int.TryParse(e.Attr("id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
                .DefaultIfEmpty(0).Max() + 1;
            foreach (var r in ordered)
                r.Id = int.TryParse(r.Elements[0].Attr("id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && taken.Add(id) ? id : next++;
            foreach (var r in ordered.Where(r => r.Move != null))
                r.Partner = ordered.FirstOrDefault(o => o != r && o.Move == r.Move && (o.Kind == RevisionKind.MovedFrom) != (r.Kind == RevisionKind.MovedFrom)
                    && o.Kind is RevisionKind.MovedFrom or RevisionKind.MovedTo)?.Id;
            // The entries' revisions by their places in the ordered list.
            var place = ordered.Select((r, k) => (r, k)).ToDictionary(x => x.r, x => x.k);
            var listed = revisions.Select(r => place[r.Revision]).ToList();
            foreach (var entry in entries)
            {
                entry.InsertionOf = entry.InsertionOf >= 0 ? listed[entry.InsertionOf] : -1;
                entry.DeletionOf = entry.DeletionOf >= 0 ? listed[entry.DeletionOf] : -1;
                entry.FormattingOf = entry.FormattingOf >= 0 ? listed[entry.FormattingOf] : -1;
                entry.ParagraphOf = entry.ParagraphOf >= 0 ? listed[entry.ParagraphOf] : -1;
            }
            return (entries, ordered, moves);
        }

        static DateTime? Date(XElement change) =>
            DateTime.TryParse(change.Attr("date"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date) ? date : null;

        /// <summary>A paragraph's formatting change as Word's balloons word it, its style's name where that is what changed.</summary>
        static string ParagraphChange(Describe describe, XElement now, XElement? before)
        {
            var said = describe.Paragraph(now, before);
            var (style, was) = (now.Element(W + "pStyle")?.Attr("val"), before?.Element(W + "pStyle")?.Attr("val"));
            if (style != was)
                said = (said.Length > 0 ? said + ", " : "") + "Style: " + (style ?? "Normal");
            return said;
        }

        /// <summary>A section's change in words: its paper, its margins, its columns as they are now, where they changed.</summary>
        static string SectionChange(XElement now, XElement? before)
        {
            static string Cm(string? twips) => double.TryParse(twips, NumberStyles.Float, CultureInfo.InvariantCulture, out var t)
                ? (t / 1440 * 2.54).ToString("0.##", CultureInfo.InvariantCulture) + " cm" : "?";
            var parts = new List<string>();
            var (size, sizeBefore) = (now.Element(W + "pgSz"), before?.Element(W + "pgSz"));
            if (size?.Attr("orient") != sizeBefore?.Attr("orient"))
                parts.Add(size?.Attr("orient") == "landscape" ? "Landscape" : "Portrait");
            if (size?.Attr("w") != sizeBefore?.Attr("w") || size?.Attr("h") != sizeBefore?.Attr("h"))
                parts.Add($"Page size: {Cm(size?.Attr("w"))} × {Cm(size?.Attr("h"))}");
            var (margins, marginsBefore) = (now.Element(W + "pgMar"), before?.Element(W + "pgMar"));
            foreach (var (side, name) in new[] { ("top", "Top"), ("bottom", "Bottom"), ("left", "Left"), ("right", "Right") })
            {
                if (margins?.Attr(side) != marginsBefore?.Attr(side))
                    parts.Add($"{name}: {Cm(margins?.Attr(side))}");
            }
            if (now.Element(W + "cols")?.Attr("num") != before?.Element(W + "cols")?.Attr("num"))
                parts.Add($"Columns: {now.Element(W + "cols")?.Attr("num") ?? "1"}");
            return parts.Count > 0 ? string.Join(", ", parts) : "Section formatted";
        }
    }

    /// <summary>
    /// Accepts tracked changes as Word's Accept does (see TextEdits.Settle): all of them in all of the document's stories (its body,
    /// headers and footers, notes and comments) where neither ids nor an author are given, as Word's Accept All Changes (measured:
    /// Document.AcceptAllRevisions reaches every story, Revisions.AcceptAll of the document the body alone); all of an author's
    /// (Word's Accept All Changes by that reviewer); or those of the body's revisions with the ids given (see Revisions), a move with its
    /// other half, as Word accepts one. One change to undo; returns how many revisions it settled (of the body's as listed, the others'
    /// elements counted as one each).
    /// </summary>
    public int AcceptRevisions(IReadOnlyCollection<int>? ids = null, string? author = null) => Settle(true, ids, author);

    /// <summary>Rejects tracked changes as Word's Reject does; see AcceptRevisions.</summary>
    public int RejectRevisions(IReadOnlyCollection<int>? ids = null, string? author = null) => Settle(false, ids, author);

    int Settle(bool accept, IReadOnlyCollection<int>? ids, string? author)
    {
        var (_, revisions, moves) = Reviewed();
        if (ids != null)
        {
            // The body's revisions named, with their moves' other halves, by the elements they are made of; the blocks from the one before the
            // first of them to the one after the last (paragraphs join the next).
            var chosen = revisions.Where(r => ids.Contains(r.Id) && (author == null || r.Author == author)).ToList();
            foreach (var r in chosen.ToList())
            {
                if (r.Partner is { } partner && revisions.FirstOrDefault(o => o.Id == partner) is { } other && !chosen.Contains(other))
                    chosen.Add(other);
            }
            if (chosen.Count == 0)
                return 0;
            var elements = chosen.SelectMany(r => r.Elements).Concat(chosen.Where(r => r.Move != null).SelectMany(r => moves.GetValueOrDefault(r.Move!) ?? [])).Distinct().ToList();
            var (first, last, section) = (int.MaxValue, -1, false);
            foreach (var element in elements)
            {
                if (Top(element) is not { } top)
                    continue;
                if (top.Name.LocalName == "sectPr")
                {
                    section = true;
                    (first, last) = (Math.Min(first, blocks.Count - 1), blocks.Count - 1);
                    continue;
                }
                var k = BlockAt(top);
                (first, last) = (Math.Min(first, k), Math.Max(last, k));
            }
            if (last < 0)
                return 0;
            (first, last) = (Math.Max(0, first - 1), Math.Min(blocks.Count - 1, last + 1));
            Change(blocks[first], blocks[last], (stretch, _) => TextEdits.Settle(stretch, [.. elements.Select(e => stretch.Find(e)).OfType<XElement>()], accept),
                lastSection: section || last == blocks.Count - 1 && BodySection() != null);
            return chosen.Count;
        }
        // All of them, or all of an author's: in the body and in the other stories' parts.
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var settled = revisions.Count(r => author == null || r.Author == author);
        var stories = new List<string>();
        foreach (var part in StoryParts())
        {
            var text = new PartText(package.ReadBytes(part));
            var xml = XDocument.Parse(text.Text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            if (xml.Root is not { } root)
                continue;
            var found = TextEdits.Changes(root, author);
            if (found.Count == 0)
                continue;
            settled += found.Count;
            // Each child of the part's root (a note, a comment, a header's block) the changes touched written anew, one they took away as nothing.
            var children = root.Elements().ToList();
            var touched = TextEdits.Settle(found, accept);
            var splices = new List<(int Start, int End, string Text)>();
            foreach (var child in children)
            {
                if (text.Start(child) is not (>= 0 and var start))
                    continue;
                if (child.Parent != root)
                    splices.Add((start, text.End(start), ""));
                else if (touched.Any(t => t == child || t.Ancestors().Contains(child)))
                    splices.Add((start, text.End(start), PartText.Xml(child)));
            }
            changes[part] = text.Spliced(splices);
            if (package.Relationships(context.MainPart).Any(r => !r.External && string.Equals(r.Target, part, StringComparison.OrdinalIgnoreCase) && (r.Is("header") || r.Is("footer"))))
                stories.Add(part);
        }
        var inBody = body != null && TextEdits.Changes(body, author).Count > 0;
        if (!inBody && changes.Count == 0)
            return 0;
        if (inBody && blocks.Count > 0)
            Change(blocks[0], blocks[^1], (stretch, _) => TextEdits.Settle(stretch, TextEdits.Changes(stretch.Xml.Root!, author), accept), changes: changes,
                lastSection: BodySection() != null);
        else
            ChangeParts(changes);
        if (stories.Count > 0)
            Restory(stories);
        return settled;
    }

    /// <summary>
    /// The body's children as the main part has them now, each as its markup (its text as read, every character as it stands), in
    /// order: what MarkChanges compares the body with after changes.
    /// </summary>
    public IReadOnlyList<string> BodyMarkup()
    {
        Read();
        return body == null ? [] : [.. body.Elements().Select(Source)];
    }

    /// <summary>A child of the body as its text stands in what it was read from (a piece's slice, else the main part as read whole).</summary>
    string Source(XElement child)
    {
        var text = sliceOf.TryGetValue(child, out var slice) ? slice.Read : wholeText;
        var start = text.Start(child);
        return start < 0 ? child.ToString(SaveOptions.DisableFormatting) : text.Text[start..text.End(start)];
    }

    /// <summary>
    /// The changes made since the body read as <paramref name="was"/> (its markup as BodyMarkup gave it then) made tracked changes of
    /// Author, as Word's Compare marks a revised document against the original (see TextEdits.Compare): the children alike at the
    /// start and the end stay; the blocks between, with one on either side, are compared with those they replaced, one change to undo.
    /// Returns the ids of the paragraphs of those blocks as they then are (none where nothing changed).
    /// </summary>
    /// <exception cref="InvalidOperationException">No author is set (see Author).</exception>
    public IReadOnlyList<int> MarkChanges(IReadOnlyList<string> was)
    {
        if (Author == null)
            throw new InvalidOperationException("Changes are marked as an author's: set Author.");
        Read();
        var now = BodyMarkup();
        var start = 0;
        while (start < now.Count && start < was.Count && now[start] == was[start])
            start++;
        var (endNow, endWas) = (now.Count, was.Count);
        while (endNow > start && endWas > start && now[endNow - 1] == was[endWas - 1])
            (endNow, endWas) = (endNow - 1, endWas - 1);
        if (start == endNow && start == endWas || body == null || blocks.Count == 0)
            return [];
        // The blocks the children between stand in, with one on either side (a place to put what went where nothing came).
        var children = body.Elements().ToList();
        var first = BlockAt(children[Math.Min(start, children.Count - 1)]);
        var last = endNow > start ? BlockAt(children[endNow - 1]) : first;
        (first, last) = (Math.Max(0, first - 1), Math.Min(blocks.Count - 1, last + 1));
        // The children the stretch of those blocks covers, and the old ones in their place (those alike before and after are the same).
        var (from, to) = (children.IndexOf(Top(First(blocks[first]))!), children.IndexOf(Top(Last(blocks[last]))!));
        var section = last == blocks.Count - 1 && BodySection() is { } sectPr && children.IndexOf(sectPr) is var s and >= 0 && s < endNow;
        if (section)
            to = children.IndexOf(BodySection()!);
        if (from < 0 || to < from || from > start || to < endNow - 1)
            throw new NotSupportedException("The changed blocks are not where they were read.");
        var old = was.Skip(from).Take(to - from + 1 + (was.Count - now.Count)).ToList();
        var count = blocks.Count;
        Change(blocks[first], blocks[last], (stretch, tracking) => TextEdits.Compare(stretch, old, tracking!), lastSection: section);
        var made = blocks.Count - (count - (last - first + 1));
        return [.. blocks.Skip(first).Take(Math.Max(0, made)).SelectMany(Flat).Select(p => p.Id)];
    }

    /// <summary>The place among the body's blocks of the block a child of the body was read from (the next one's for a child no block was read from).</summary>
    int BlockAt(XElement top)
    {
        for (var k = 0; k < blocks.Count; k++)
        {
            if (Top(First(blocks[k])) is { } from && Top(Last(blocks[k])) is { } to
                && (from == top || to == top || XNode.CompareDocumentOrder(from, top) <= 0 && XNode.CompareDocumentOrder(top, to) <= 0))
                return k;
            if (Top(First(blocks[k])) is { } after && after.Parent == top.Parent && XNode.CompareDocumentOrder(top, after) < 0)
                return k;
        }
        return blocks.Count - 1;
    }

    /// <summary>The parts of the stories besides the body: headers, footers, footnotes, endnotes, comments.</summary>
    IEnumerable<string> StoryParts() =>
        package.Relationships(context.MainPart).Where(r => !r.External && (r.Is("header") || r.Is("footer") || r.Is("footnotes") || r.Is("endnotes") || r.Is("comments"))
            && package.Exists(r.Target)).Select(r => r.Target).Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether changes are tracked as Word's Track Changes tracks them (w:trackRevisions in the document's settings): Word then marks
    /// every edit as a tracked change of its user. Set, a change of its own: the settings part given it or without it.
    /// </summary>
    /// <exception cref="NotSupportedException">The document has no settings part; nothing changes.</exception>
    public bool TrackRevisions
    {
        get
        {
            Read();
            return SettingsPart() is { } part && context.Load(part).Root?.Element(Wml.W + "trackRevisions") is { } track && track.OnOff() != false;
        }
        set
        {
            if (TrackRevisions == value)
                return;
            var part = SettingsPart() ?? throw new NotSupportedException("The document has no settings part.");
            var text = new PartText(package.ReadBytes(part));
            var root = context.Load(part).Root!;
            var w = root.Name.Namespace;
            // Any there are (one saying off) out, one in its place by the settings' order where it is to be on.
            var splices = root.Elements(w + "trackRevisions").Select(e => text.Start(e)).Where(s => s >= 0).Select(s => (Start: s, End: text.End(s), Text: "")).ToList();
            if (value)
            {
                var rank = Array.IndexOf(SettingsOrder, "trackRevisions");
                var prefix = root.GetPrefixOfNamespace(w) is { Length: > 0 } p ? p + ":" : "";
                var at = root.Elements().FirstOrDefault(e => e.Name != w + "trackRevisions" && (e.Name.Namespace != w || Array.IndexOf(SettingsOrder, e.Name.LocalName) > rank)) is { } after
                    ? text.Start(after) : text.Text.LastIndexOf('<');
                splices.Add((at, at, $"<{prefix}trackRevisions/>"));
            }
            ChangeParts(new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase) { [part] = text.Spliced(splices.OrderBy(s => s.Start).ThenBy(s => s.End)) });
        }
    }
}
