using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Editing;
using FreeSlate.Opc;

namespace FreeSlate;

/// <summary>
/// A comment of a Word document (catalog: review.comments): its id, author, initials and time, the comment it answers
/// (w15:commentEx paraIdParent) and whether it is resolved (w15:done); where its range begins and ends in the text (the
/// paragraph and the place in it of w:commentRangeStart and w:commentRangeEnd, null where the document has none); and
/// the ids of its paragraphs, which the document changes as the body's.
/// </summary>
public sealed record WordComment(string Id, string Author, string Initials, DateTime? Date, string? Parent, bool Done, TextPlace? Start, TextPlace? End,
    IReadOnlyList<int> Paragraphs);

/// <summary>
/// The comments of a Word document: read as stories of their own in either view (ModelReader.CommentStories), their
/// paragraphs found by id and keeping their ids through readings anew (ModelReader.Keep). A change is made in a comment's
/// paragraphs as in the body's (see TextEdits), in the XML of the comment in its part (see ChangeComment): each change a
/// version of its own with the part's bytes, which undoing and doing again put back.
/// </summary>
public sealed partial class WordDocument
{
    static readonly XNamespace W14 = "http://schemas.microsoft.com/office/word/2010/wordml", W15 = "http://schemas.microsoft.com/office/word/2012/wordml";

    /// <summary>
    /// The comments as read last (see ReadComments): the blocks of each by id, their paragraphs (of tables and text boxes
    /// too) by id with the block of their comment they stand in, and the comment each such block is of; read for the
    /// reader, the comments part and its bytes as a version has them (null as the package was opened).
    /// </summary>
    Dictionary<string, List<Block>> commentsRead = [];
    readonly Dictionary<int, (ParagraphBlock Paragraph, Block Top)> commentParagraphs = [];
    readonly Dictionary<Block, string> commentOf = new(ReferenceEqualityComparer.Instance);
    (ModelReader? Reader, string? Part, byte[]? Bytes) commentsIndexed;

    /// <summary>The comments part, where the package has it.</summary>
    string? CommentsPart() =>
        package.FirstRelationship(context.MainPart, "comments") is { External: false } related && package.Exists(related.Target) ? related.Target : null;

    /// <summary>The comments read where they were not for the document as it reads now (see commentsIndexed).</summary>
    void KnowComments()
    {
        var part = CommentsPart();
        var bytes = part != null ? partBytes.GetValueOrDefault(part) : null;
        if (!ReferenceEquals(commentsIndexed.Reader, reader) || commentsIndexed.Part != part || !ReferenceEquals(commentsIndexed.Bytes, bytes))
            ReadComments();
    }

    /// <summary>Reads the comments anew (see ModelReader.CommentStories), each comment's paragraphs taking the ids they had (see ModelReader.Keep).</summary>
    void ReadComments()
    {
        var read = reader.CommentStories();
        var part = CommentsPart();
        foreach (var (id, blocks) in read)
        {
            // As it read from these bytes before (undoing, doing again; see readFrom), else as it reads now.
            if (((part != null ? Recalled(part, ("comment", id)) : null) ?? commentsRead.GetValueOrDefault(id)) is { } before)
                ModelReader.Keep(before, blocks);
        }
        commentsRead = read;
        commentParagraphs.Clear();
        commentOf.Clear();
        foreach (var (id, blocks) in read)
        {
            foreach (var block in blocks)
            {
                commentOf[block] = id;
                Collect(commentParagraphs, block, block);
            }
        }
        commentsIndexed = (reader, part, part != null ? partBytes.GetValueOrDefault(part) : null);
    }

    /// <summary>
    /// The document's comments in the order of the comments part (see WordComment): their ranges as the body has them
    /// now, the paragraphs by the ids the document gives them.
    /// </summary>
    public IReadOnlyList<WordComment> Comments()
    {
        Read();
        KnowComments();
        if (CommentsPart() is not { } part || context.Load(part).Root is not { } root)
            return [];
        // Replies and resolved comments (w15:commentsEx), by the w14:paraId of a comment's last paragraph.
        var extended = new Dictionary<string, (string? Parent, bool Done)>(StringComparer.OrdinalIgnoreCase);
        if (package.FirstRelationship(context.MainPart, "commentsExtended") is { External: false } ex && package.Exists(ex.Target))
        {
            foreach (var e in context.Load(ex.Target).Root?.Elements(W15 + "commentEx") ?? [])
            {
                if ((string?)e.Attribute(W15 + "paraId") is { } paraId)
                    extended[paraId] = ((string?)e.Attribute(W15 + "paraIdParent"), (string?)e.Attribute(W15 + "done") is "1" or "true");
            }
        }
        var elements = root.Elements(Wml.W + "comment").ToList();
        var byParaId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in elements)
        {
            if (ParaId(element) is { } paraId && element.Attr("id") is { } id)
                byParaId.TryAdd(paraId, id);
        }
        var anchors = Anchors();
        var comments = new List<WordComment>();
        foreach (var element in elements)
        {
            if (element.Attr("id") is not { } id)
                continue;
            var (parent, done) = ParaId(element) is { } own && extended.TryGetValue(own, out var x) ? x : (null, false);
            var date = DateTime.TryParse(element.Attr("date"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at)
                ? at : (DateTime?)null;
            var paragraphsOf = commentsRead.GetValueOrDefault(id) is { } blocks ? blocks.SelectMany(Flat).Select(p => p.Id).ToList() : [];
            comments.Add(new WordComment(id, element.Attr("author") ?? "", element.Attr("initials") ?? "", date,
                parent != null && byParaId.TryGetValue(parent, out var parentId) ? parentId : null, done,
                anchors.TryGetValue((id, true), out var from) ? from : null, anchors.TryGetValue((id, false), out var to) ? to : null, paragraphsOf));
        }
        return comments;
    }

    /// <summary>The w14:paraId of a comment's last paragraph, which commentsExtended knows it by.</summary>
    static string? ParaId(XElement comment) => (string?)comment.Elements(Wml.W + "p").LastOrDefault()?.Attribute(W14 + "paraId");

    /// <summary>
    /// Where the comments' ranges begin and end in the body as it reads now (by id and whether it is the beginning): the
    /// paragraph each w:commentRangeStart or w:commentRangeEnd stands in (or the next one, for one between paragraphs) and
    /// the place in its text before the content after it.
    /// </summary>
    Dictionary<(string Id, bool Start), TextPlace> Anchors()
    {
        var anchors = new Dictionary<(string Id, bool Start), TextPlace>();
        if (body == null)
            return anchors;
        var marks = body.Descendants().Where(e => e.Name == Wml.W + "commentRangeStart" || e.Name == Wml.W + "commentRangeEnd").ToList();
        if (marks.Count == 0)
            return anchors;
        var byElement = new Dictionary<XElement, ParagraphBlock>(ReferenceEqualityComparer.Instance);
        foreach (var (paragraph, _) in paragraphs.Values)
        {
            foreach (var element in paragraph.Parts ?? [paragraph.Element])
                byElement.TryAdd(element, paragraph);
        }
        foreach (var mark in marks)
        {
            if (mark.Attr("id") is not { } id)
                continue;
            var start = mark.Name.LocalName == "commentRangeStart";
            // In a paragraph, else before the next one.
            var holder = mark.Ancestors(Wml.W + "p").FirstOrDefault() ?? mark.ElementsAfterSelf().SelectMany(e => e.DescendantsAndSelf(Wml.W + "p")).FirstOrDefault();
            // A paragraph without an id stands where the document does not change it (a shape's text box).
            if (holder == null || !byElement.TryGetValue(holder, out var paragraph) || paragraph.Id == 0)
                continue;
            var offset = paragraph.TextOffset;
            if (mark.Ancestors(Wml.W + "p").Any())
            {
                foreach (var inline in paragraph.Content)
                {
                    if (inline.Xml is { } xml && xml != mark && XNode.CompareDocumentOrder(xml, mark) > 0)
                        break;
                    offset += ParagraphText.Length(inline);
                }
            }
            anchors.TryAdd((id, start), new TextPlace(paragraph.Id, offset));
        }
        return anchors;
    }

    /// <summary>
    /// Puts a comment on a stretch of the body's text (Word's New Comment), from <paramref name="start"/> to
    /// <paramref name="end"/> in reading order: its range's beginning and end there (w:commentRangeStart, w:commentRangeEnd),
    /// its reference after the end in the style for comment references, and the comment at the end of the comments part
    /// (a part of its own where the document has none): by <paramref name="author"/> (the author of tracked changes where
    /// null) at ChangedAt (now where null), its markup in a paragraph of the style for comment text after the comment's
    /// mark, as Word writes them. Returns the comment's id.
    /// </summary>
    /// <exception cref="NotSupportedException">A place is not in the body, or within what the document computes or keeps; nothing changes.</exception>
    /// <exception cref="ArgumentException">The markup is not well-formed XML, or the end comes before the start; nothing changes.</exception>
    public string AddComment(TextPlace start, TextPlace end, string markup, string? author = null, string? initials = null) =>
        Comment(start, end, markup, author, initials, null);

    /// <summary>
    /// Answers a comment (Word's Reply): a comment of its own on the same range, which commentsExtended knows as the
    /// answer to it (w15:paraIdParent); see AddComment. Returns the answer's id.
    /// </summary>
    /// <exception cref="NotSupportedException">The comment has no range in the body; nothing changes.</exception>
    public string Reply(string comment, string markup, string? author = null, string? initials = null)
    {
        var (thread, members) = Thread(Comments(), comment);
        if (thread is not { Start: { } start, End: { } end })
            throw new NotSupportedException("The comment has no range in the body.");
        return Comment(start, end, markup, author, initials, (thread, members[^1]));
    }

    /// <summary>
    /// The thread a comment stands in: the comment it answers, or the one that one answers, as far as that goes (Word
    /// answers the first of a thread, whichever answer is answered), and the thread's comments in the part's order.
    /// </summary>
    static (WordComment Root, List<WordComment> Members) Thread(IReadOnlyList<WordComment> all, string comment)
    {
        WordComment RootOf(WordComment c)
        {
            for (var seen = new HashSet<string>(); c.Parent != null && seen.Add(c.Id) && all.FirstOrDefault(x => x.Id == c.Parent) is { } up;)
                c = up;
            return c;
        }
        var root = RootOf(all.FirstOrDefault(c => c.Id == comment) ?? throw new ArgumentException($"The document has no comment {comment}.", nameof(comment)));
        return (root, [.. all.Where(c => RootOf(c).Id == root.Id)]);
    }

    /// <summary>
    /// Takes a comment away with its answers (Word's Delete Comment): their ranges and references out of the body, the
    /// comments out of their part, what commentsExtended, commentsIds and commentsExtensible know of them too.
    /// </summary>
    public void DeleteComment(string comment)
    {
        var all = Comments();
        if (all.All(c => c.Id != comment))
            throw new ArgumentException($"The document has no comment {comment}.", nameof(comment));
        // The comment and the answers to it, and to those.
        var gone = new HashSet<string>(StringComparer.Ordinal) { comment };
        for (var more = true; more;)
        {
            more = false;
            foreach (var c in all)
                more |= c.Parent != null && gone.Contains(c.Parent) && gone.Add(c.Id);
        }
        var part = CommentsPart()!;
        var text = new PartText(package.ReadBytes(part));
        var root = context.Load(part).Root!;
        var paraIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cuts = new List<(int Start, int End, string Text)>();
        foreach (var element in root.Elements(Wml.W + "comment").Where(e => e.Attr("id") is { } id && gone.Contains(id)))
        {
            foreach (var p in element.Elements(Wml.W + "p"))
            {
                if ((string?)p.Attribute(W14 + "paraId") is { } paraId)
                    paraIds.Add(paraId);
            }
            var at = text.Start(element);
            cuts.Add((at, text.End(at), ""));
        }
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase) { [part] = text.Spliced(cuts.OrderBy(c => c.Start)) };
        Unlisted("commentsExtended", W15 + "commentEx", W15 + "paraId", paraIds, changes);
        var durable = Unlisted("commentsIds", W16Cid + "commentId", W16Cid + "paraId", paraIds, changes, W16Cid + "durableId");
        Unlisted("commentsExtensible", W16Cex + "commentExtensible", W16Cex + "durableId", durable, changes);
        // The body's blocks from the first that holds a mark of them to the last; a mark between blocks before its block.
        // Marks the body has where no range is known (in a shape's text box) are not taken away alone.
        var marks = body?.Descendants().Count(e => e.Name.LocalName is "commentRangeStart" or "commentRangeEnd" && e.Attr("id") is { } id && gone.Contains(id)) ?? 0;
        var known = all.Where(c => gone.Contains(c.Id)).SelectMany(c => new[] { c.Start, c.End }).OfType<TextPlace>().ToList();
        if (known.Count < marks)
            throw new NotSupportedException("The comment's range stands where the document does not change it (a shape's text box).");
        var places = known.Select(t => Find(t.Paragraph).Top).ToList();
        if (places.Count == 0)
            ChangeParts(changes);
        else
        {
            var indices = places.Select(b => IndexOf(b)).ToList();
            var (first, last) = (Math.Max(0, indices.Min() - 1), indices.Max());
            Change(blocks[first], blocks[last], (stretch, _) => TextEdits.RemoveMarks(stretch, gone), changes: changes);
        }
    }

    /// <summary>Marks a comment resolved (Word's Resolve) or not: w15:done of its entry in commentsExtended, made where it has none.</summary>
    public void Resolve(string comment, bool done = true)
    {
        Read();
        var part = CommentsPart() ?? throw new ArgumentException($"The document has no comment {comment}.", nameof(comment));
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var extended = Extended(changes);
        var root = extended.Document.Root!;
        var splices = new List<(int Start, int End, string Text)>();
        // The whole thread, as Word resolves it.
        foreach (var member in Thread(Comments(), comment).Members)
        {
            var paraId = Identified(part, member.Id, splices);
            if (root.Elements(W15 + "commentEx").FirstOrDefault(e => string.Equals((string?)e.Attribute(W15 + "paraId"), paraId, StringComparison.OrdinalIgnoreCase)) is { } entry)
                entry.SetAttributeValue(W15 + "done", done ? "1" : "0");
            else
                root.Add(new XElement(W15 + "commentEx", new XAttribute(W15 + "paraId", paraId), new XAttribute(W15 + "done", done ? "1" : "0")));
        }
        if (splices.Count > 0)
            changes[part] = new PartText(package.ReadBytes(part)).Spliced(splices.OrderBy(splice => splice.Start));
        extended.Save(changes);
        if (extended.New is { } fresh)
            Added("", [fresh], changes, context.MainPart);
        ChangeParts(changes);
    }

    static readonly XNamespace W16Cid = "http://schemas.microsoft.com/office/word/2016/wordml/cid", W16Cex = "http://schemas.microsoft.com/office/word/2018/wordml/cex";

    /// <summary>
    /// A comment or an answer put in (see AddComment, Reply): its range in the body as one change with the parts it gives
    /// (the comments part, commentsExtended for an answer); returns its id.
    /// </summary>
    string Comment(TextPlace start, TextPlace end, string markup, string? author, string? initials, (WordComment Root, WordComment Last)? thread)
    {
        var answered = thread?.Root;
        var (p, top) = Find(start.Paragraph);
        var (q, lastTop) = Find(end.Paragraph);
        if (!paragraphs.ContainsKey(start.Paragraph) || !paragraphs.ContainsKey(end.Paragraph))
            throw new NotSupportedException("A comment's range is in the body.");
        var (from, to) = (IndexOf(top), IndexOf(lastTop));
        if (from > to || from == to && (ReferenceEquals(p, q) ? start.Offset > end.Offset : Flat(top).IndexOf(p) > Flat(top).IndexOf(q)))
            throw new ArgumentException("The range's end comes before its start.", nameof(end));
        // As the document's settings let them be (see Tracked): by "Author", with no time.
        var (who, when) = Writer(author ?? Author ?? "");
        var mark = who == "Author" && who != (author ?? Author) ? "A"
            : initials ?? string.Concat(who.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0])));
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        List<NewPart> added = [];
        Action<Stretch, XElement?>? place = null;
        // What the change splices into the comments part's text as the version has it.
        var splices = new List<(int Start, int End, string Text)>();
        // The comments part, as the changes so far have it; a new one where the document has none.
        var part = CommentsPart();
        var text = part != null ? new PartText(package.ReadBytes(part)) : null;
        var taken = new HashSet<string>(StringComparer.Ordinal);
        if (part != null)
        {
            foreach (var c in context.Load(part).Root!.Elements(Wml.W + "comment"))
                taken.Add(c.Attr("id") ?? "");
        }
        foreach (var e in body?.Descendants().Where(e => e.Name.LocalName is "commentRangeStart" or "commentRangeEnd" or "commentReference") ?? [])
            taken.Add(e.Attr("id") ?? "");
        var id = Enumerable.Range(0, taken.Count + 1).First(k => !taken.Contains(k.ToString(CultureInfo.InvariantCulture))).ToString(CultureInfo.InvariantCulture);
        var paraId = NewParaId(part);
        var date = when is { } at ? $" w:date=\"{at.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}\"" : "";
        var textStyle = context.Styles.IdByName("annotation text", "paragraph");
        var referenceStyle = context.Styles.IdByName("annotation reference", "character");
        var referenceFormat = referenceStyle != null ? $"<w:rPr><w:rStyle w:val=\"{Escaped(referenceStyle)}\"/></w:rPr>" : "";
        var comment = $"<w:comment w:id=\"{id}\" w:author=\"{Escaped(who)}\"{date} w:initials=\"{Escaped(mark)}\">"
            + $"<w:p w14:paraId=\"{paraId}\" w14:textId=\"77777777\">{(textStyle != null ? $"<w:pPr><w:pStyle w:val=\"{Escaped(textStyle)}\"/></w:pPr>" : "")}"
            + $"<w:r>{referenceFormat}<w:annotationRef/></w:r>{markup}</w:p></w:comment>";
        if (part != null && text != null)
        {
            var root = context.Load(part).Root!;
            var elements = Elements(comment, root);
            Strictly(part, elements);
            // At the end of the part's root, every other character as it was: written within the root's start tag as the
            // part's text has it, whose prefixes they take.
            var rootStart = text.Start(root);
            var open = text.TagEnd(rootStart);
            var scope = XElement.Parse(text.Text[rootStart..open].TrimEnd('>', '/', ' ', '\t', '\r', '\n') + "/>", LoadOptions.PreserveWhitespace);
            scope.Add(elements);
            var written = string.Concat(elements.Select(PartText.Xml));
            // A root written empty (pandoc's) gets an end tag.
            if (text.Text[open - 2] == '/')
            {
                var slash = text.Text.LastIndexOf('/', open - 1);
                changes[part] = text.Spliced([(slash, open, $">{written}</{Qualified(text.Text, rootStart)}>")]);
            }
            else
            {
                // Where the comment goes in its part is known once its marks stand in the stretch (see Among; the edit runs
                // before the parts are put, see Change).
                place = (stretch, firstChild) =>
                {
                    var at = Among(root, text, Preceding(stretch, id, firstChild)) ?? text.Text.LastIndexOf('<', text.End(rootStart) - 1);
                    splices.Add((at, at, written));
                    changes[part] = text.Spliced(splices.OrderBy(splice => splice.Start));
                };
            }
        }
        else
        {
            var document = XDocument.Parse("<w:comments xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:w14=\""
                + W14.NamespaceName + "\" xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" mc:Ignorable=\"w14\"/>");
            document.Declaration = new XDeclaration("1.0", "UTF-8", "yes");
            document.Root!.Add(Elements(comment, document.Root));
            if (IsStrictPart(context.MainPart))
                Strict.Revert(document.Root);
            added.Add(new NewPart("comments", "/word/comments.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.comments+xml",
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/comments", Bytes(document)));
        }
        // An answer: commentsExtended knows it by its paragraph, and the comment it answers by its own.
        if (answered != null)
        {
            var answeredId = Identified(part!, answered.Id, splices);
            var extended = Extended(changes);
            var root = extended.Document.Root!;
            if (!root.Elements(W15 + "commentEx").Any(e => string.Equals((string?)e.Attribute(W15 + "paraId"), answeredId, StringComparison.OrdinalIgnoreCase)))
                root.Add(new XElement(W15 + "commentEx", new XAttribute(W15 + "paraId", answeredId), new XAttribute(W15 + "done", answered.Done ? "1" : "0")));
            // The answer takes the thread's state, as Word gives it.
            root.Add(new XElement(W15 + "commentEx", new XAttribute(W15 + "paraId", paraId), new XAttribute(W15 + "paraIdParent", answeredId),
                new XAttribute(W15 + "done", answered.Done ? "1" : "0")));
            extended.Save(changes);
            if (extended.New is { } fresh)
                added.Add(fresh);
        }
        Added("", added, changes, context.MainPart);
        var bodyRoot = context.Document.Root!;
        var (first, last) = (Elements($"<w:commentRangeStart w:id=\"{id}\"/>", bodyRoot),
            Elements($"<w:commentRangeEnd w:id=\"{id}\"/><w:r>{referenceFormat}<w:commentReference w:id=\"{id}\"/></w:r>", bodyRoot));
        Strictly(context.MainPart, first);
        Strictly(context.MainPart, last);
        if (thread is var (_, after))
        {
            // An answer's marks beside those of the thread's last comment (a mark between blocks before its block).
            var before = blocks[Math.Max(0, IndexOf(top) - 1)];
            Change(before, lastTop, (stretch, _) =>
            {
                var edited = TextEdits.InsertBeside(stretch, after.Id, [.. first.Select(e => new XElement(e))], [.. last.Select(e => new XElement(e))]);
                place?.Invoke(stretch, Top(First(before)));
                return edited;
            }, changes: changes);
        }
        else
        {
            Change(top, lastTop, (stretch, _) =>
            {
                var edited = TextEdits.InsertMarks(stretch, p, start.Offset, [.. first.Select(e => new XElement(e))], q, end.Offset, [.. last.Select(e => new XElement(e))]);
                place?.Invoke(stretch, Top(First(top)));
                return edited;
            }, changes: changes);
        }
        return id;
    }

    /// <summary>
    /// Where a new comment goes in the comments part's text: Word keeps the comments in the order of their references
    /// (w:commentReference; it numbers them by where their ranges begin when it saves), so right after the comment whose
    /// reference stands before the new one's, before the part's first where none does; null where the part has none.
    /// </summary>
    static int? Among(XElement root, PartText text, string? preceding)
    {
        var elements = root.Elements(Wml.W + "comment").ToList();
        if (preceding != null)
            return elements.FirstOrDefault(e => e.Attr("id") == preceding) is { } element && text.Start(element) is var at and >= 0 ? text.End(at) : null;
        return elements.Count > 0 && text.Start(elements[0]) is var first and >= 0 ? first : null;
    }

    /// <summary>
    /// The comment whose reference stands right before that of a comment put into the stretch: in the stretch, else the
    /// last in the body before the first child the stretch was read from; null where none does.
    /// </summary>
    string? Preceding(Stretch stretch, string comment, XElement? firstChild)
    {
        static string? Id(XElement e) => e.Attributes().FirstOrDefault(a => a.Name.LocalName == "id")?.Value;
        var references = stretch.Xml.Descendants().Where(e => e.Name.LocalName == "commentReference").ToList();
        var own = references.FindIndex(r => Id(r) == comment);
        if (own > 0)
            return Id(references[own - 1]);
        return firstChild == null || body == null ? null
            : body.Descendants().Where(e => e.Name.LocalName == "commentReference").TakeWhile(e => XNode.CompareDocumentOrder(e, firstChild) < 0).Select(Id).LastOrDefault();
    }

    /// <summary>
    /// A w14:paraId that no paragraph of the comments part or the body has and none given before (eight hex digits below
    /// 80000000, as Word gives them), the same for the same document and changes.
    /// </summary>
    string NewParaId(string? part)
    {
        string[] texts = [part != null ? new PartText(package.ReadBytes(part)).Text : "", wholeText.Text];
        for (var seed = (uint)(texts.Sum(t => t.Length) + paraIdsGiven.Count + version); ; seed++)
        {
            var candidate = (Mixed(seed) & 0x7FFFFFFFu).ToString("X8", CultureInfo.InvariantCulture);
            if (!paraIdsGiven.Contains(candidate) && !texts.Any(t => t.Contains($"\"{candidate}\"", StringComparison.OrdinalIgnoreCase)))
            {
                paraIdsGiven.Add(candidate);
                return candidate;
            }
        }

        // The seed's bits spread over all of them, one to one (lowbias32), where HashCode differs from process to process.
        static uint Mixed(uint x)
        {
            x = (x ^ (x >> 16)) * 0x7FEB352Du;
            x = (x ^ (x >> 15)) * 0x846CA68Bu;
            return x ^ (x >> 16);
        }
    }

    /// <summary>The paragraph ids given (see NewParaId), which no other one takes.</summary>
    readonly HashSet<string> paraIdsGiven = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The w14:paraId of a comment's last paragraph, which commentsExtended knows it by: one given to it where it has none,
    /// as a splice of the comments part's text as the version has it (added to <paramref name="splices"/>).
    /// </summary>
    string Identified(string part, string comment, List<(int Start, int End, string Text)> splices)
    {
        var root = context.Load(part).Root!;
        var element = root.Elements(Wml.W + "comment").FirstOrDefault(c => c.Attr("id") == comment) ?? throw new ArgumentException($"The document has no comment {comment}.", nameof(comment));
        if (ParaId(element) is { } own)
            return own;
        var last = element.Elements(Wml.W + "p").LastOrDefault() ?? throw new NotSupportedException("The comment has no paragraph.");
        var text = new PartText(package.ReadBytes(part));
        var paraId = NewParaId(part);
        // Put into the paragraph's start tag, with the namespace where nothing around it declares it.
        var at = text.Start(last);
        var name = Qualified(text.Text, at);
        var prefix = last.GetPrefixOfNamespace(W14);
        var attribute = prefix != null ? $" {prefix}:paraId=\"{paraId}\"" : $" xmlns:w14=\"{W14.NamespaceName}\" w14:paraId=\"{paraId}\"";
        splices.Add((at + 1 + name.Length, at + 1 + name.Length, attribute));
        return paraId;
    }

    /// <summary>The commentsExtended part as the changes have it, to add to; a new one where the document has none (see New).</summary>
    sealed record ExtendedPart(string? Name, XDocument Document)
    {
        public NewPart? New { get; private set; }

        public void Save(Dictionary<string, byte[]?> changes)
        {
            if (Name != null)
                changes[Name] = Bytes(Document);
            else
                New = new NewPart("commentsExtended", "/word/commentsExtended.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.commentsExtended+xml",
                    "http://schemas.microsoft.com/office/2011/relationships/commentsExtended", Bytes(Document));
        }
    }

    ExtendedPart Extended(Dictionary<string, byte[]?> changes)
    {
        if (package.FirstRelationship(context.MainPart, "commentsExtended") is { External: false } related && package.Exists(related.Target))
            return new ExtendedPart(related.Target, Xml(changes.TryGetValue(related.Target, out var bytes) && bytes != null ? bytes : package.ReadBytes(related.Target)));
        var document = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(W15 + "commentsEx", new XAttribute(XNamespace.Xmlns + "w15", W15.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "mc", "http://schemas.openxmlformats.org/markup-compatibility/2006"),
            new XAttribute(XName.Get("Ignorable", "http://schemas.openxmlformats.org/markup-compatibility/2006"), "w15")));
        return new ExtendedPart(null, document);
    }

    /// <summary>
    /// The entries of a part related from the main part whose key is among <paramref name="keys"/> taken out (into the
    /// changes); returns the values of <paramref name="also"/> they had.
    /// </summary>
    HashSet<string> Unlisted(string relationship, XName entry, XName key, IReadOnlySet<string> keys, Dictionary<string, byte[]?> changes, XName? also = null)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (package.FirstRelationship(context.MainPart, relationship) is not { External: false } related || !package.Exists(related.Target))
            return values;
        var document = Xml(package.ReadBytes(related.Target));
        var entries = document.Root?.Elements(entry).Where(e => (string?)e.Attribute(key) is { } k && keys.Contains(k)).ToList() ?? [];
        if (entries.Count == 0)
            return values;
        foreach (var e in entries)
        {
            if (also != null && (string?)e.Attribute(also) is { } value)
                values.Add(value);
            e.Remove();
        }
        changes[related.Target] = Bytes(document);
        return values;
    }

    /// <summary>A change that gives other parts than the main part new bytes and nothing else, a version of its own (see Put).</summary>
    void ChangeParts(IReadOnlyDictionary<string, byte[]?> changes)
    {
        Read();
        Put(changes);
        versions.RemoveRange(version + 1, versions.Count - version - 1);
        versions.Add(new Version(whole, islands, kept, partBytes));
        version++;
        fresh = [];
    }

    /// <summary>
    /// A change made in the paragraphs of a comment (see Change for the body's): <paramref name="edit"/> gives the new
    /// text of the comment's children in the XML of its part (see Stretch), which takes the place of theirs, every other
    /// character of the part as it was; the part's new bytes (with those of the other parts the change gives, see Put) a
    /// version of their own, the comments read anew. Returns where an element of the new text stands in it.
    /// </summary>
    Func<XElement, int> ChangeComment(string id, Func<Stretch, Tracking?, string> edit, IReadOnlyDictionary<string, byte[]?>? changes)
    {
        if (CommentsPart() is not { } part || context.Load(part).Root is not { } root
            || root.Elements(Wml.W + "comment").FirstOrDefault(c => c.Attr("id") == id) is not { } element)
            throw new NotSupportedException("The comment is not where it was read.");
        var text = new PartText(package.ReadBytes(part));
        var (start, rootStart) = (text.Start(element), text.Start(root));
        if (start < 0 || rootStart < 0 || text.Text[text.TagEnd(start) - 2] == '/')
            throw new NotSupportedException("The comment is not where it was read.");
        // Its children, within the start tags of the part's root and the comment, which the stretch is parsed within.
        var (open, close) = (text.TagEnd(start), text.Text.LastIndexOf('<', text.End(start) - 1));
        var head = text.Text[rootStart..text.TagEnd(rootStart)] + text.Text[start..open];
        var tail = text.Text[close..text.End(start)] + $"</{Qualified(text.Text, rootStart)}>";
        var stretch = new Stretch(head, text.Text[open..close], tail,
            e => e.Document == element.Document && text.Start(e) is var at && at >= open && at < close ? at - open + head.Length : -1);
        var tracking = Tracked(() => ChangeId(part, () => text.Text));
        var inner = edit(stretch, tracking);
        if (tracking != null)
            changeId = tracking.NextId;
        var given = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, changed) in changes ?? new Dictionary<string, byte[]?>())
            given[name] = changed;
        var spliced = text.Spliced([(open, close, inner)]);
        given[part] = spliced;
        var partsBefore = partBytes;
        Put(given);
        try
        {
            ReadComments();
        }
        catch
        {
            Restore(partsBefore);
            ReadComments();
            throw;
        }
        versions.RemoveRange(version + 1, versions.Count - version - 1);
        versions.Add(new Version(whole, islands, kept, partBytes));
        version++;
        fresh = commentsRead.GetValueOrDefault(id) ?? [];
        var now = new PartText(spliced);
        var innerStart = context.Load(part).Root?.Elements(Wml.W + "comment").FirstOrDefault(c => c.Attr("id") == id) is { } reread && now.Start(reread) is var found and >= 0
            ? now.TagEnd(found) : -1;
        return e => now.Start(e) is var at && innerStart >= 0 && at >= innerStart && at < innerStart + inner.Length ? at - innerStart : -1;
    }
}
