using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using FreeSlate.Editing;
using FreeSlate.Editor.Controls;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// Tracked changes as Claude reads, writes and reviews them, as Word's Review tab does (see WordDocument.Revisions):
/// - read in the text as CriticMarkup writes them, each with its change's id after its closing marks: "{++inserted++12}",
///   "{--deleted--13}", "{==formatted==14}"; a paragraph's mark "¶" in them (an inserted paragraph "{++text¶++12}", a deleted mark
///   that joined two paragraphs "{--¶--13}", a paragraph's formatting changed "{==¶==14}"); after the document each under "Changes:"
///   with its kind, author, time and paragraphs, what a formatting change did, a deleted row's text;
/// - written back as read, each keeps its change: its author, time and kind (a move a move), a deletion its XML as it was (see Changed,
///   Marks); one written without an id is a new change of "Claude";
/// - a write with "track" (or in a document whose Track Changes is on) is marked as Claude's changes against what it replaced, as Word's
///   Compare marks a revision (WordDocument.MarkChanges, see Writing);
/// - the review tool accepts or rejects them, all, an author's or by their ids, and turns Word's Track Changes on or off.
/// </summary>
static partial class Reviewing
{
    public const string Author = "Claude";

    static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>The namespace of a paragraph's mark a change holds (see Changed), which Marks makes the paragraph's.</summary>
    static readonly XNamespace Fs = "urn:freeslate:review";

    /// <summary>A change of the document as read, for writing it back: its kind, author, time, text and the XML of its elements (see WordDocument.RevisionMarkup).</summary>
    sealed record Known(RevisionKind Kind, string Author, DateTime? Date, string Text, IReadOnlyList<XElement> Markup);

    /// <summary>The changes each document had as its writes began (their ids stay with them), and the document a write now writes into.</summary>
    static readonly ConditionalWeakTable<WordDocument, Dictionary<int, Known>> known = new();
    static WordDocument? writing;

    /// <summary>The ids the write gave its changes so far: a change written in pieces (or twice) takes its id once, the others ids of their own.</summary>
    static readonly HashSet<int> used = [];

    /// <summary>A write begins in a document: the changes it has known by their ids (see Changed), those known before kept too.</summary>
    public static void Remember(WordDocument word)
    {
        writing = word;
        used.Clear();
        var changes = known.GetOrCreateValue(word);
        foreach (var revision in word.Revisions())
        {
            var markup = word.RevisionMarkup(revision.Id) ?? [];
            changes[revision.Id] = new Known(revision.Kind, revision.Author, revision.Date, revision.Text,
                [.. markup.Select(m => XElement.Parse(m, LoadOptions.PreserveWhitespace))]);
        }
    }

    /// <summary>
    /// The document's changes as Claude reads them after the document (one line each, "Changes:"): "[change 12] inserted by Anna, 2026-11-20
    /// 10:00, ¶4–¶5", what a formatting change did, a move's other half, what the text does not show (a row deleted, a table's or section's
    /// formatting); then how many changes headers, footers, notes and comments hold; empty where there are none.
    /// </summary>
    public static string Read(WordDocument word)
    {
        IReadOnlyList<WordRevision> revisions;
        try
        {
            revisions = word.Revisions();
        }
        catch (NotSupportedException)
        {
            return "";
        }
        var text = new StringBuilder();
        foreach (var r in revisions)
        {
            // A row's text ends with its last cell's mark and its own end.
            var rows = r.Text.Contains("\r\a\r\a", StringComparison.Ordinal);
            text.Append("[change ").Append(r.Id.ToString(CultureInfo.InvariantCulture)).Append("] ").Append(r.Kind switch
            {
                RevisionKind.Insertion => rows ? "rows inserted" : "inserted",
                RevisionKind.Deletion => rows ? "rows deleted" : "deleted",
                RevisionKind.MovedFrom => "moved away",
                RevisionKind.MovedTo => "moved here",
                RevisionKind.Formatting => "formatted",
                RevisionKind.ParagraphFormatting => "paragraph formatted",
                RevisionKind.TableFormatting => "table formatted",
                _ => "page set up",
            }).Append(" by ").Append(r.Author.Length > 0 ? r.Author : "an unknown author");
            if (r.Date is { } date)
                text.Append(", ").Append(date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            if (r.Kind != RevisionKind.SectionFormatting && r.Start.Paragraph > 0)
            {
                text.Append(", ¶").Append(r.Start.Paragraph.ToString(CultureInfo.InvariantCulture));
                if (r.End.Paragraph != r.Start.Paragraph && r.End.Paragraph > 0)
                    text.Append("–¶").Append(r.End.Paragraph.ToString(CultureInfo.InvariantCulture));
            }
            if (r.Partner is { } partner)
                text.Append(r.Kind == RevisionKind.MovedFrom ? ", to [change " : ", from [change ").Append(partner.ToString(CultureInfo.InvariantCulture)).Append(']');
            if (r.Description.Length > 0)
                text.Append(": ").Append(r.Description);
            // What the text as it reads does not show: a deleted row's cells.
            if (rows && r.Kind is RevisionKind.Deletion)
                text.Append(": \"").Append(Shown(r.Text)).Append('"');
            text.Append('\n');
        }
        if (word.RevisionsElsewhere() is > 0 and var elsewhere)
            text.Append("Headers, footers, notes or comments hold ").Append(elsewhere.ToString(CultureInfo.InvariantCulture))
                .Append(elsewhere == 1 ? " more change" : " more changes").Append(" (accepting or rejecting all of them settles those too).\n");
        return text.ToString();
    }

    /// <summary>A change's text in a line: cells apart by " | ", a paragraph's mark as "¶", tabs as spaces.</summary>
    static string Shown(string text)
    {
        var shown = text.Replace("\r\a\r\a", " / ", StringComparison.Ordinal).Replace("\r\a", " | ", StringComparison.Ordinal).Replace('\r', '¶')
            .Replace('\t', ' ').Replace("\a", "", StringComparison.Ordinal).Trim(' ', '/', '|');
        return shown.Length > 160 ? shown[..160] + "…" : shown;
    }

    /// <summary>The changes of a paragraph as they stand in its text (see WordDocument.RevisionsOf); none in a document without changes or where they cannot be told.</summary>
    public static IReadOnlyList<RevisionSpan> Spans(WordDocument word, int paragraph)
    {
        try
        {
            return word.RevisionsOf(paragraph);
        }
        catch (Exception e) when (e is NotSupportedException or InvalidOperationException or ArgumentException)
        {
            return [];
        }
    }

    /// <summary>
    /// The marks of a paragraph's changes as a line of the notation is written (see Reading.Line): before each piece of its text those
    /// closing there, deletions standing there and those opening there (an insertion's "{++", a formatting change's "{=="); a piece ends
    /// where one begins or ends (Next). Marks close inside out, one closed within another opened again after.
    /// </summary>
    public sealed class Marker(IReadOnlyList<RevisionSpan> spans)
    {
        readonly List<RevisionSpan> open = [];
        readonly HashSet<RevisionSpan> done = [];

        /// <summary>The marks before the text at a place: those of changes ending, deletions there, changes beginning.</summary>
        public void At(int k, StringBuilder line)
        {
            for (var i = open.Count - 1; i >= 0; i--)
            {
                if (open[i].Offset + open[i].Length > k)
                    continue;
                // Those opened within it closed first and opened again after it.
                var within = open.Skip(i + 1).ToList();
                for (var j = open.Count - 1; j >= i; j--)
                    line.Append(Closing(open[j]));
                open.RemoveRange(i, open.Count - i);
                foreach (var again in within.Where(s => s.Offset + s.Length > k))
                {
                    line.Append(Opening(again));
                    open.Add(again);
                }
            }
            foreach (var span in spans.Where(s => s.Deleted != null && s.Offset <= k && !done.Contains(s)).ToList())
            {
                line.Append("{--").Append(Deleted(span.Deleted!))
                    .Append("--").Append(span.Revision.ToString(CultureInfo.InvariantCulture)).Append('}');
                done.Add(span);
            }
            foreach (var span in spans.Where(s => s.Deleted == null && !s.Mark && s.Length > 0 && s.Offset <= k && s.Offset + s.Length > k && !done.Contains(s)).ToList())
            {
                line.Append(Opening(span));
                open.Add(span);
                done.Add(span);
            }
        }

        /// <summary>Where the piece of text from a place is to end at the latest: where a change begins or ends after it.</summary>
        public int Next(int k, int end)
        {
            foreach (var span in spans)
            {
                if (span.Offset > k)
                    end = Math.Min(end, span.Offset);
                if (span.Deleted == null && span.Offset + span.Length > k)
                    end = Math.Min(end, span.Offset + span.Length);
            }
            return end;
        }

        /// <summary>The marks at the end of the text written: those open closed, deletions standing at the end of the paragraph's text.</summary>
        public void End(int to, bool paragraphEnd, StringBuilder line)
        {
            for (var j = open.Count - 1; j >= 0; j--)
                line.Append(Closing(open[j]));
            open.Clear();
            if (paragraphEnd)
                At(to, line);
        }

        static string Opening(RevisionSpan span) => span.Kind == RevisionKind.Formatting ? "{==" : "{++";

        static string Closing(RevisionSpan span) => (span.Kind == RevisionKind.Formatting ? "==" : "++") + span.Revision.ToString(CultureInfo.InvariantCulture) + "}";
    }

    /// <summary>
    /// A paragraph's line with the changes of its own mark after it (see Reading.Line): "{++¶++12}" where it was inserted (within the
    /// insertion that ends the line, where one does: "{++text¶++12}"), "{==¶==14}" where its paragraph's formatting changed.
    /// </summary>
    public static string WithMark(WordDocument word, int paragraph, string line)
    {
        foreach (var span in Spans(word, paragraph).Where(s => s.Mark))
        {
            var id = span.Revision.ToString(CultureInfo.InvariantCulture);
            if (span.Kind == RevisionKind.ParagraphFormatting)
                line += "{==¶==" + id + "}";
            else if (line.EndsWith("++" + id + "}", StringComparison.Ordinal))
                line = line[..^(id.Length + 3)] + "¶++" + id + "}";
            else
                line += "{++¶++" + id + "}";
        }
        return line;
    }

    /// <summary>A deletion's text as the notation writes it within its marks: a paragraph's mark "¶", a cell's end left out, the notation's marks escaped.</summary>
    public static string Deleted(string text)
    {
        var shown = new StringBuilder();
        foreach (var c in text)
        {
            shown.Append(c switch
            {
                '\r' => "¶",
                '\a' => "",
                '\v' => "<br>",
                '*' or '_' or '{' or '}' or '[' or ']' or '|' or '`' or '\\' or '<' or '$' or '¶' => "\\" + c,
                _ => c.ToString(),
            });
        }
        return shown.ToString();
    }

    /// <summary>
    /// The runs of a change's pieces (made by Markup.Runs) as tracked changes: an insertion's in a w:ins, a deletion's in a w:del (their
    /// text w:delText), a formatting change's runs given a w:rPrChange with the look they had; of the change the id names (see Remember),
    /// its author, time and kind (a move's elements with their range's marks), a deletion written as it was where its text is alike; else a
    /// new change of Claude's, now. A "¶" in it is a paragraph's mark there: an element of its own (see Marks).
    /// </summary>
    public static string Changed(Notation.Change change, string runs)
    {
        var was = change.Id is { } id && writing != null && known.TryGetValue(writing, out var changes) && changes.TryGetValue(id, out var found) ? found : null;
        var (author, date) = was != null ? (was.Author, was.Date) : (Author, DateTime.Now);
        var own = change.Id is { } given && used.Add(given);
        var attributes = new List<XAttribute>
        {
            new(W + "id", (own ? change.Id!.Value : NewId()).ToString(CultureInfo.InvariantCulture)),
            new(W + "author", author),
        };
        if (date is { } at)
            attributes.Add(new XAttribute(W + "date", at.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)));
        // A deletion as it was, where it reads as written (once: its elements keep their ids).
        if (change.Kind == '-' && own && was is { Kind: RevisionKind.Deletion or RevisionKind.MovedFrom } && Comparable(Unshown(was.Text)) == Comparable(change.Text))
            return string.Concat(was.Markup.Select(Written));
        var root = Parsed(runs);
        var kind = change.Kind switch
        {
            '+' => was?.Kind == RevisionKind.MovedTo ? "moveTo" : "ins",
            '-' => was?.Kind == RevisionKind.MovedFrom ? "moveFrom" : "del",
            _ => "rPrChange",
        };
        // The pieces between paragraphs' marks, each wrapped; a mark between them an element of its own.
        var output = new StringBuilder();
        var first = true;
        XElement Wrapper()
        {
            var made = new XElement(W + kind, attributes.Select(a => new XAttribute(a)));
            if (!first)
                made.SetAttributeValue(W + "id", NewId().ToString(CultureInfo.InvariantCulture));
            first = false;
            return made;
        }
        var segment = new List<XElement>();
        void Close()
        {
            if (segment.Count == 0)
                return;
            output.Append(Wrapped(segment, kind, Wrapper, was));
            segment.Clear();
        }
        foreach (var element in Split(root.Elements().ToList()))
        {
            if (element.Name == Fs + "mark")
            {
                Close();
                var mark = new XElement(Fs + "mark", new XAttribute("kind", change.Kind), new XAttribute("change", kind == "rPrChange" ? "pPrChange" : kind),
                    attributes.Select(a => new XAttribute(a)));
                if (!first)
                    mark.SetAttributeValue(W + "id", NewId().ToString(CultureInfo.InvariantCulture));
                first = false;
                // A paragraph's formatting change: its properties with the change (what they are and were); a deleted mark: its
                // paragraph's properties as they were.
                if (change.Kind == '=' && was?.Markup.FirstOrDefault(e => e.Name == W + "pPr" && e.Element(W + "pPrChange") != null) is { } formatted)
                    mark.Add(new XElement(formatted));
                else if (change.Kind == '-' && was?.Markup.FirstOrDefault(e => e.Name == W + "pPr"
                             && e.Element(W + "rPr")?.Elements().Any(c => c.Name.LocalName is "del" or "moveFrom") == true) is { } properties)
                    mark.Add(new XElement(properties));
                output.Append(Serialized(mark));
                continue;
            }
            segment.Add(element);
        }
        Close();
        // A move's range round what it moved.
        if (kind is "moveTo" or "moveFrom" && was != null)
        {
            var (start, end) = (was.Markup.FirstOrDefault(e => e.Name.LocalName == kind + "RangeStart"), was.Markup.FirstOrDefault(e => e.Name.LocalName == kind + "RangeEnd"));
            if (start != null && end != null)
                return Written(start) + output + Written(end);
        }
        return output.ToString();
    }

    /// <summary>
    /// An element of a change as the document had it, as markup (its ids as they were: the write took the element away); a deleted mark's
    /// paragraph properties a mark of its own between the pieces (see Marks), with the change of that mark.
    /// </summary>
    static string Written(XElement element)
    {
        var copy = new XElement(element);
        if (copy.Name != W + "pPr")
            return Serialized(copy);
        var marker = copy.Element(W + "rPr")?.Elements().FirstOrDefault(e => e.Name.LocalName is "del" or "moveFrom");
        return Serialized(new XElement(Fs + "mark", new XAttribute("kind", "-"), new XAttribute("change", marker?.Name.LocalName ?? "del"),
            marker?.Attributes().Where(a => !a.IsNamespaceDeclaration).Select(a => new XAttribute(a)), copy));
    }

    /// <summary>Runs whose text holds "¶" cut there, a mark element (Fs:mark) in its place.</summary>
    static List<XElement> Split(List<XElement> elements)
    {
        var result = new List<XElement>();
        foreach (var element in elements)
        {
            if (element.Name != W + "r" || !element.Elements(W + "t").Any(t => t.Value.Contains('¶')))
            {
                result.Add(element);
                continue;
            }
            var properties = element.Element(W + "rPr");
            foreach (var t in element.Elements(W + "t"))
            {
                var parts = t.Value.Split('¶');
                for (var k = 0; k < parts.Length; k++)
                {
                    if (k > 0)
                        result.Add(new XElement(Fs + "mark"));
                    if (parts[k].Length > 0)
                        result.Add(new XElement(W + "r", properties != null ? new XElement(properties) : null,
                            new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), parts[k])));
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Pieces of a change wrapped: runs (and what holds runs: a link's field, a control) in a w:ins or w:del of their own, the runs within
    /// a link's field wrapped there; a deletion's text w:delText; a formatting change a w:rPrChange in each run with the look it had.
    /// </summary>
    static string Wrapped(List<XElement> elements, string kind, Func<XElement> wrapper, Known? was)
    {
        if (kind == "rPrChange")
        {
            // The look the change gave the runs, with the look they had in it; without it there is no change to keep: the runs as written.
            if (was?.Markup.FirstOrDefault(e => e.Name == W + "rPr" && e.Element(W + "rPrChange") != null) is { } look)
            {
                foreach (var run in elements.SelectMany(e => e.DescendantsAndSelf(W + "r")))
                {
                    var given = new XElement(look);
                    var change = given.Element(W + "rPrChange")!;
                    change.ReplaceAttributes(wrapper().Attributes(), change.Attributes().Where(a => a.Name.Namespace != W));
                    run.Element(W + "rPr")?.Remove();
                    run.AddFirst(given);
                }
            }
            return string.Concat(elements.Select(Serialized));
        }
        // A field of its own element (a link's, a page number's) as the runs of a field, as Word marks one inserted or deleted.
        elements = [.. elements.SelectMany(e => e.Name == W + "fldSimple" ? Complex(e) : [e])];
        var deleting = kind is "del" or "moveFrom";
        if (deleting)
        {
            foreach (var e in elements.SelectMany(e => e.DescendantsAndSelf()).ToList())
            {
                if (e.Name == W + "t")
                    e.Name = W + "delText";
                else if (e.Name == W + "instrText")
                    e.Name = W + "delInstrText";
            }
        }
        var output = new StringBuilder();
        var runs = new List<XElement>();
        void Flush()
        {
            if (runs.Count == 0)
                return;
            var around = wrapper();
            around.Add(runs);
            output.Append(Serialized(around));
            runs.Clear();
        }
        foreach (var element in elements)
        {
            // Runs and what an insertion holds besides: another author's deletion within it stays within it.
            if (element.Name.LocalName is "r" or "sdt" or "smartTag" or "customXml" || !deleting && element.Name.LocalName is "del" or "moveFrom")
            {
                runs.Add(element);
                continue;
            }
            Flush();
            // A link's field or the like: the runs within it wrapped.
            foreach (var run in element.Elements(W + "r").ToList())
            {
                var around = wrapper();
                run.ReplaceWith(around);
                around.Add(run);
            }
            output.Append(Serialized(element));
        }
        Flush();
        return output.ToString();
    }

    /// <summary>A simple field (w:fldSimple) as the runs of a complex one: its beginning, its instruction, its result's runs, its end, in its result's look.</summary>
    static IEnumerable<XElement> Complex(XElement field)
    {
        var look = field.Descendants(W + "rPr").FirstOrDefault();
        XElement Run(params object[] content) => new(W + "r", look != null ? new XElement(look) : null, content);
        yield return Run(new XElement(W + "fldChar", new XAttribute(W + "fldCharType", "begin")));
        yield return Run(new XElement(W + "instrText", new XAttribute(XNamespace.Xml + "space", "preserve"), (string?)field.Attribute(W + "instr") ?? ""));
        yield return Run(new XElement(W + "fldChar", new XAttribute(W + "fldCharType", "separate")));
        foreach (var result in field.Elements())
            yield return new XElement(result);
        yield return Run(new XElement(W + "fldChar", new XAttribute(W + "fldCharType", "end")));
    }

    /// <summary>The usual prefixes of markup given (see Markup.Namespaces) and the marks' own, which elements are parsed and written within.</summary>
    static readonly (string Prefix, string Uri)[] Prefixes =
    [
        ("w", W.NamespaceName), ("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"),
        ("m", "http://schemas.openxmlformats.org/officeDocument/2006/math"), ("wp", "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"),
        ("a", "http://schemas.openxmlformats.org/drawingml/2006/main"), ("pic", "http://schemas.openxmlformats.org/drawingml/2006/picture"),
        ("c", "http://schemas.openxmlformats.org/drawingml/2006/chart"), ("wps", "http://schemas.microsoft.com/office/word/2010/wordprocessingShape"),
        ("wpg", "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup"), ("wpc", "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas"),
        ("wp14", "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing"), ("w14", "http://schemas.microsoft.com/office/word/2010/wordml"),
        ("w15", "http://schemas.microsoft.com/office/word/2012/wordml"), ("a14", "http://schemas.microsoft.com/office/drawing/2010/main"),
        ("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006"), ("v", "urn:schemas-microsoft-com:vml"), ("o", "urn:schemas-microsoft-com:office:office"),
        ("fs", Fs.NamespaceName),
    ];

    /// <summary>Markup of the usual prefixes parsed: the elements it holds, under a root that declares them.</summary>
    static XElement Parsed(string markup) =>
        XElement.Parse("<root " + string.Join(" ", Prefixes.Select(p => $"xmlns:{p.Prefix}=\"{p.Uri}\"")) + ">" + markup + "</root>", LoadOptions.PreserveWhitespace);

    /// <summary>An element as markup of the usual prefixes, without declaring them (Markup's blocks are parsed within them, see WordDocument.InsertBlocks).</summary>
    static string Serialized(XElement element)
    {
        var root = Parsed("");
        var copy = new XElement(element);
        foreach (var e in copy.DescendantsAndSelf())
            e.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
        root.Add(copy);
        return Declared().Replace(copy.ToString(SaveOptions.DisableFormatting), "");
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\s+xmlns:(?:w|r|m|wp|a|pic|c|wps|wpg|wpc|wp14|w14|w15|a14|mc|v|o|fs)=""[^""]*""")]
    private static partial System.Text.RegularExpressions.Regex Declared();

    /// <summary>A change's text as Word gives it (see WordRevision.Text) as the notation writes it (marks "¶", cells' ends left out).</summary>
    static string Unshown(string text) => text.Replace("\a", "", StringComparison.Ordinal).Replace('\r', '¶');

    /// <summary>A text as compared with another: spaces at its ends and runs of them alike.</summary>
    static string Comparable(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>A new change's id: a number too great to meet one of the document's (as new bookmarks take, see Markup.BookmarkId).</summary>
    static int NewId() => Random.Shared.Next(100_000_000, 999_999_999);

    /// <summary>Whether markup holds a paragraph's mark a change made (see Changed), which Marks makes the paragraph's.</summary>
    public static bool HasMarks(string markup) => markup.Contains("<fs:mark", StringComparison.Ordinal) || markup.Contains(Fs.NamespaceName, StringComparison.Ordinal);

    /// <summary>
    /// Blocks whose paragraphs hold marks of changes (see Changed) made as Word keeps them: an inserted or deleted mark in the middle cuts
    /// the paragraph there, the part before taking the mark's change in its w:pPr's w:rPr (a deleted mark the properties its paragraph had,
    /// where they are known); one at the end is the paragraph's own; a formatting change's mark a w:pPrChange of the paragraph's
    /// properties with those it had.
    /// </summary>
    public static string Marks(string blocks)
    {
        if (!HasMarks(blocks))
            return blocks;
        var root = Parsed(blocks);
        foreach (var p in root.Descendants(W + "p").Where(p => p.Descendants(Fs + "mark").Any()).ToList())
        {
            var marks = p.Elements(Fs + "mark").ToList();
            foreach (var stray in p.Descendants(Fs + "mark").Where(m => m.Parent != p).ToList())
                stray.Remove();
            foreach (var mark in marks)
            {
                var properties = p.Element(W + "pPr");
                var kind = (string?)mark.Attribute("kind");
                var name = (string?)mark.Attribute("change") ?? (kind == "+" ? "ins" : kind == "-" ? "del" : "pPrChange");
                var change = new XElement(W + name, mark.Attributes().Where(a => a.Name.Namespace == W));
                var last = !mark.ElementsAfterSelf().Any(e => e.Name.LocalName is "r" or "ins" or "del" or "moveTo" or "moveFrom" or "fldSimple" or "hyperlink" or "sdt");
                if (kind == "=")
                {
                    if (mark.Element(W + "pPr") is { } formatted && formatted.Element(W + "pPrChange") is { } kept)
                    {
                        // The properties the change gave, with those it kept; the paragraph's level, its mark and its section as written.
                        var merged = new XElement(W + "pPr");
                        if (properties?.Element(W + "pStyle") is { } style)
                            merged.Add(new XElement(style));
                        merged.Add(formatted.Elements().Where(e => e.Name.LocalName is not ("pStyle" or "rPr" or "sectPr" or "pPrChange")).Select(e => new XElement(e)));
                        foreach (var written in new[] { "rPr", "sectPr" })
                        {
                            if (properties?.Element(W + written) is { } same)
                                merged.Add(new XElement(same));
                        }
                        var restored = new XElement(kept);
                        restored.ReplaceAttributes(change.Attributes(), kept.Attributes().Where(a => a.Name.Namespace != W && !a.IsNamespaceDeclaration));
                        merged.Add(restored);
                        if (properties != null)
                            properties.ReplaceWith(merged);
                        else
                            p.AddFirst(merged);
                    }
                    // Without what the properties were there is no change to keep.
                    mark.Remove();
                    continue;
                }
                if (last)
                {
                    // The paragraph's own mark.
                    MarkRun(properties ?? Prepended(p)).AddFirst(change);
                    mark.Remove();
                    continue;
                }
                // Cut there: the part before a paragraph of its own whose mark is the change's.
                var part = new XElement(W + "p", p.Attributes().Where(a => !a.IsNamespaceDeclaration));
                // The properties the paragraph had where they are known (its section's end and formatting change too), else the next part's
                // but those (a section ends with its last paragraph).
                var own = mark.Element(W + "pPr") is { } given ? new XElement(given) : properties != null ? new XElement(properties) : new XElement(W + "pPr");
                if (mark.Element(W + "pPr") == null)
                {
                    own.Elements(W + "sectPr").Remove();
                    own.Elements(W + "pPrChange").Remove();
                }
                var markRun = MarkRun(own);
                markRun.Elements().Where(e => e.Name.LocalName is "ins" or "del" or "moveFrom" or "moveTo").Remove();
                markRun.AddFirst(change);
                part.Add(own);
                foreach (var node in mark.NodesBeforeSelf().Where(n => n is not XElement { Name.LocalName: "pPr" }).ToList())
                {
                    node.Remove();
                    part.Add(node);
                }
                mark.Remove();
                p.AddBeforeSelf(part);
            }
        }
        return string.Concat(root.Nodes().Select(n => n is XElement e ? Serialized(e) : n.ToString(SaveOptions.DisableFormatting)));
    }

    /// <summary>A paragraph's w:pPr, made first where it has none.</summary>
    static XElement Prepended(XElement p)
    {
        var made = new XElement(W + "pPr");
        p.AddFirst(made);
        return made;
    }

    /// <summary>The run properties of a paragraph's mark (w:pPr/w:rPr), made in the schema's place where there are none.</summary>
    static XElement MarkRun(XElement properties)
    {
        if (properties.Element(W + "rPr") is { } found)
            return found;
        var made = new XElement(W + "rPr");
        if (properties.Elements().FirstOrDefault(e => e.Name.LocalName is "sectPr" or "pPrChange") is { } after)
            after.AddBeforeSelf(made);
        else
            properties.Add(made);
        return made;
    }

    /// <summary>Runs among the text with no paragraph's mark of a change in them (text written into a paragraph cuts none).</summary>
    public static string Unmarked(string runs)
    {
        if (!HasMarks(runs))
            return runs;
        return System.Text.RegularExpressions.Regex.Replace(runs, @"<fs:mark\b[^>]*?(?:/>|>.*?</fs:mark>)|<mark xmlns=""urn:freeslate:review""[^>]*?(?:/>|>.*?</mark>)", "",
            System.Text.RegularExpressions.RegexOptions.Singleline);
    }

    /// <summary>
    /// The review tool: tracked changes accepted or rejected (all of them, an author's, or by their ids) and Word's Track Changes turned on
    /// or off, each a step of the session; what it did, as the document's changes then read, or why it did nothing.
    /// </summary>
    public static (string Text, bool Error) Review(EditSession editing, DocumentView sheets, string json)
    {
        if (editing.Document is not WordDocument word)
            return ("This document has no tracked changes.", true);
        JsonElement input;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            input = parsed.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ("The input of review is a JSON object.", true);
        }
        string? Text(string name) => input.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        List<int>? ids = null;
        if (input.TryGetProperty("changes", out var given) && given.ValueKind == JsonValueKind.Array)
            ids = [.. given.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out _)).Select(v => v.GetInt32())];
        var (action, author, track) = (Text("action"), Text("author") is { Length: > 0 } a ? a : null, Text("track"));
        if (action is not ("accept" or "reject") && track is not ("on" or "off"))
            return ("Give action (accept or reject), with the changes' ids or an author or neither (all of them), or track (on or off).", true);
        if (ids is { Count: 0 })
            return ("changes lists no ids; leave it out for all of them.", true);
        string? refused = null;
        var told = new StringBuilder();
        void Step(Func<TextPlace?> change)
        {
            sheets.Edit(() =>
            {
                try
                {
                    editing.Handle(change);
                }
                catch (Exception e) when (e is NotSupportedException or ArgumentException or InvalidOperationException)
                {
                    refused = e.Message;
                }
            });
        }
        if (action is "accept" or "reject")
        {
            var listed = word.Revisions();
            var unknown = ids?.Where(id => listed.All(r => r.Id != id)).ToList() ?? [];
            if (unknown.Count > 0)
                return ($"The document has no change {string.Join(", ", unknown)}; its changes are {(listed.Count == 0 ? "none" : string.Join(", ", listed.Select(r => r.Id)))}.", true);
            var settled = 0;
            Step(() =>
            {
                settled = action == "accept" ? word.AcceptRevisions(ids, author) : word.RejectRevisions(ids, author);
                return null;
            });
            told.Append(action == "accept" ? "Accepted " : "Rejected ").Append(settled.ToString(CultureInfo.InvariantCulture))
                .Append(settled == 1 ? " change" : " changes").Append(author != null ? " of " + author : "").Append(". ");
        }
        if (track is "on" or "off")
        {
            Step(() =>
            {
                word.TrackRevisions = track == "on";
                return null;
            });
            told.Append("Track Changes is ").Append(track).Append(". ");
        }
        if (refused != null)
            return ($"Not done: {refused}", true);
        var now = Read(word);
        return (told.Append(now.Length > 0 ? "Changes now:\n" + now : "The document has no tracked changes now.").ToString(), false);
    }
}
