using System.Text;
using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>
/// Blocks marked as tracked changes against what they were, as Word's Compare marks a revised document against the original (see
/// WordDocument.MarkChanges): the blocks alike stay as they are; paragraphs are paired by their words where they are much alike, the
/// others deleted (their runs in a w:del, their marks deleted) and inserted (in a w:ins, their marks inserted), the deleted before the
/// inserted; in a pair the words that differ are deleted and inserted word by word, runs alike in text but not in look given a
/// w:rPrChange with the properties they had, the paragraph's own properties a w:pPrChange where they differ; tables paired in order,
/// their rows by their text, rows that came or went inserted or deleted (w:trPr), the cells of a pair compared as blocks. The changes
/// the blocks held already stay as they were: an insertion of another author taken away is deleted within it (as Word marks it, a
/// w:del in the w:ins), the author's own goes, a deletion stays; one written anew keeps its author.
/// </summary>
static partial class TextEdits
{
    /// <summary>A piece of a paragraph's content as compared: a word, spaces or a sign of a run's text; an element of a run (a tab, a drawing, a field's character); a link, field or control whole; a mark of no width (a bookmark).</summary>
    sealed record Atom(string Key, XElement Content, XElement? RunProperties, IReadOnlyList<XElement> Changes, bool Mark)
    {
        /// <summary>Whether the text reads with it once the changes are accepted (it is not deleted or moved away).</summary>
        public bool Present => !Changes.Any(c => c.Name.LocalName is "del" or "moveFrom");
    }

    /// <summary>
    /// The stretch's blocks marked as tracked changes by <paramref name="tracking"/>'s author against <paramref name="was"/>, the markup of
    /// the blocks they replaced (see the summary above). Returns the stretch's new text.
    /// </summary>
    public static string Compare(Stretch stretch, IReadOnlyList<string> was, Tracking tracking)
    {
        var holder = stretch.Xml.Root!.Elements().First();
        var w = holder.Name.Namespace;
        var old = stretch.Parsed(string.Concat(was)).Root!.Elements().First().Elements().ToList();
        var now = holder.Elements().ToList();
        var merged = Blocks(old, now, tracking, w);
        // The blocks merged in place of those now: each now standing for what goes in its place (the last for what follows it).
        var written = new List<(XElement, IEnumerable<XElement>)>();
        var groups = new List<List<XElement>>();
        var group = new List<XElement>();
        foreach (var (element, from) in merged)
        {
            group.Add(element);
            if (from != null)
            {
                groups.Add(group);
                group = [];
            }
        }
        if (group.Count > 0)
        {
            if (groups.Count > 0)
                groups[^1].AddRange(group);
            else
                groups.Add(group);
        }
        var anchors = merged.Where(m => m.From != null).Select(m => m.From!).ToList();
        for (var k = 0; k < anchors.Count; k++)
        {
            var anchor = anchors[k];
            var elements = groups[k];
            // Alike as it was: it stays as it reads.
            if (elements is [var only] && ReferenceEquals(only, anchor))
                continue;
            foreach (var element in elements.Where(e => !ReferenceEquals(e, anchor)))
                anchor.AddBeforeSelf(element);
            written.Add((anchor, elements));
        }
        return stretch.Written(written);
    }

    /// <summary>
    /// Old blocks and new ones merged: the elements in their order, each with the new block it stands for or follows (null for one of the
    /// old's that goes before the next new one).
    /// </summary>
    static List<(XElement Element, XElement? From)> Blocks(List<XElement> old, List<XElement> now, Tracking tracking, XNamespace w)
    {
        var result = new List<(XElement, XElement?)>();
        var (oldKeys, nowKeys) = (old.Select(Key).ToList(), now.Select(Key).ToList());
        var pairs = Common(oldKeys, nowKeys);
        var (i, j) = (0, 0);
        foreach (var (a, b) in pairs.Append((old.Count, now.Count)))
        {
            Gap(old.GetRange(i, a - i), now.GetRange(j, b - j), tracking, w, result);
            if (a < old.Count && b < now.Count)
                result.Add((now[b], now[b]));
            (i, j) = (a + 1, b + 1);
        }
        return result;
    }

    /// <summary>Blocks between those alike: paragraphs paired where much alike, tables in order, the rest deleted before the rest inserted.</summary>
    static void Gap(List<XElement> old, List<XElement> now, Tracking tracking, XNamespace w, List<(XElement, XElement?)> result)
    {
        if (old.Count == 0 && now.Count == 0)
            return;
        // Paired by likeness in order (a table only with a table), as an alignment of the most likeness.
        var (n, m) = (old.Count, now.Count);
        var score = new double[n + 1, m + 1];
        var like = new double[n, m];
        for (var a = 0; a < n; a++)
        {
            for (var b = 0; b < m; b++)
                like[a, b] = Likeness(old[a], now[b], w);
        }
        for (var a = 1; a <= n; a++)
        {
            for (var b = 1; b <= m; b++)
            {
                score[a, b] = Math.Max(score[a - 1, b], score[a, b - 1]);
                if (like[a - 1, b - 1] >= 0.5)
                    score[a, b] = Math.Max(score[a, b], score[a - 1, b - 1] + like[a - 1, b - 1]);
            }
        }
        var steps = new List<(int Old, int Now)>();
        for (var (a, b) = (n, m); a > 0 || b > 0;)
        {
            if (a > 0 && b > 0 && like[a - 1, b - 1] >= 0.5 && Math.Abs(score[a, b] - (score[a - 1, b - 1] + like[a - 1, b - 1])) < 1e-9)
            {
                steps.Add((a - 1, b - 1));
                (a, b) = (a - 1, b - 1);
            }
            else if (b > 0 && (a == 0 || score[a, b - 1] >= score[a - 1, b]))
            {
                steps.Add((-1, b - 1));
                b--;
            }
            else
            {
                steps.Add((a - 1, -1));
                a--;
            }
        }
        steps.Reverse();
        // Within each run of steps between pairs, the deleted first, then the inserted.
        var pending = new List<(int Old, int Now)>();
        // What is no block (a section's properties, a bookmark between paragraphs) is not marked: the old one goes, the new one stays as it is.
        static bool Block(XElement e) => e.Name.LocalName is "p" or "tbl" or "sdt" or "customXml";
        void Flush()
        {
            // Paragraphs in place of as many others are those changed, however little alike (a cell's "5" written "7"): in order.
            var (gone, come) = (pending.Where(s => s.Old >= 0).Select(s => s.Old).ToList(), pending.Where(s => s.Now >= 0).Select(s => s.Now).ToList());
            var paired = 0;
            while (paired < gone.Count && paired < come.Count && old[gone[paired]].Name == w + "p" && now[come[paired]].Name == w + "p")
                paired++;
            for (var k = 0; k < paired; k++)
                result.Add((Pair(old[gone[k]], now[come[k]], tracking, w), now[come[k]]));
            foreach (var a in gone.Skip(paired).Where(a => Block(old[a])))
                result.Add((DeletedBlock(new XElement(old[a]), tracking, w), null));
            foreach (var b in come.Skip(paired))
                result.Add((Block(now[b]) ? InsertedBlock(new XElement(now[b]), tracking, w) : now[b], now[b]));
            pending.Clear();
        }
        foreach (var step in steps)
        {
            if (step.Old >= 0 && step.Now >= 0)
            {
                Flush();
                result.Add((Pair(old[step.Old], now[step.Now], tracking, w), now[step.Now]));
            }
            else
                pending.Add(step);
        }
        Flush();
    }

    /// <summary>How much two blocks are alike, 0 to 1: of two paragraphs their words in common (Dice's coefficient), two tables 0.5 at least; else 0.</summary>
    static double Likeness(XElement a, XElement b, XNamespace w)
    {
        if (a.Name == w + "tbl" && b.Name == w + "tbl")
            return 0.5 + 0.5 * Dice(Words(a, w), Words(b, w));
        if (a.Name != w + "p" || b.Name != w + "p")
            return 0;
        var (x, y) = (Words(a, w), Words(b, w));
        if (x.Count == 0 && y.Count == 0)
            return 1;
        return Dice(x, y);
    }

    static double Dice(List<string> x, List<string> y)
    {
        if (x.Count + y.Count == 0)
            return 1;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var word in x)
            counts[word] = counts.GetValueOrDefault(word) + 1;
        var common = 0;
        foreach (var word in y)
        {
            if (counts.GetValueOrDefault(word) > 0)
            {
                counts[word]--;
                common++;
            }
        }
        return 2.0 * common / (x.Count + y.Count);
    }

    /// <summary>The words of a block's text as it reads with its changes accepted.</summary>
    static List<string> Words(XElement block, XNamespace w)
    {
        var text = new StringBuilder();
        foreach (var t in block.Descendants(w + "t"))
        {
            if (!t.Ancestors().Any(a => a.Name == w + "del" || a.Name == w + "moveFrom"))
                text.Append(t.Value).Append(' ');
        }
        return [.. Tokens(text.ToString()).Where(token => !string.IsNullOrWhiteSpace(token))];
    }

    /// <summary>A text cut into words (letters and digits), runs of spaces, and single signs, as Word's Compare marks changes by words.</summary>
    static List<string> Tokens(string text)
    {
        var tokens = new List<string>();
        var k = 0;
        while (k < text.Length)
        {
            var start = k;
            if (char.IsLetterOrDigit(text[k]))
            {
                while (k < text.Length && (char.IsLetterOrDigit(text[k]) || char.IsSurrogate(text[k]) || text[k] is '\'' or '’' && k + 1 < text.Length && char.IsLetter(text[k + 1])))
                    k++;
            }
            else if (char.IsWhiteSpace(text[k]))
            {
                while (k < text.Length && char.IsWhiteSpace(text[k]))
                    k++;
            }
            else
                k += char.IsSurrogatePair(text, k) ? 2 : 1;
            tokens.Add(text[start..k]);
        }
        return tokens;
    }

    /// <summary>A block's markup as compared: its XML without the ids of editing sessions and paragraphs, which say nothing of its content.</summary>
    static string Key(XElement block)
    {
        var copy = new XElement(block);
        foreach (var e in copy.DescendantsAndSelf())
        {
            foreach (var attribute in e.Attributes().ToList())
            {
                var name = attribute.Name.LocalName;
                if (name.StartsWith("rsid", StringComparison.Ordinal) || name is "paraId" or "textId" || attribute.IsNamespaceDeclaration
                    || name == "id" && e.Name.LocalName is "docPr" or "cNvPr")
                    attribute.Remove();
            }
        }
        return copy.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>The pairs of places of a longest common sequence of two lists (by their keys), in order.</summary>
    static List<(int, int)> Common(List<string> a, List<string> b)
    {
        // Alike at the start and the end first (the usual case), the rest by dynamic programming where it is not too long.
        var (start, endA, endB) = (0, a.Count, b.Count);
        while (start < endA && start < endB && a[start] == b[start])
            start++;
        while (endA > start && endB > start && a[endA - 1] == b[endB - 1])
            (endA, endB) = (endA - 1, endB - 1);
        var pairs = new List<(int, int)>();
        for (var k = 0; k < start; k++)
            pairs.Add((k, k));
        var (n, m) = (endA - start, endB - start);
        if (n > 0 && m > 0 && (long)n * m <= 25_000_000)
        {
            var lengths = new int[n + 1, m + 1];
            for (var x = n - 1; x >= 0; x--)
            {
                for (var y = m - 1; y >= 0; y--)
                    lengths[x, y] = a[start + x] == b[start + y] ? lengths[x + 1, y + 1] + 1 : Math.Max(lengths[x + 1, y], lengths[x, y + 1]);
            }
            for (var (x, y) = (0, 0); x < n && y < m;)
            {
                if (a[start + x] == b[start + y])
                {
                    pairs.Add((start + x, start + y));
                    (x, y) = (x + 1, y + 1);
                }
                else if (lengths[x + 1, y] >= lengths[x, y + 1])
                    x++;
                else
                    y++;
            }
        }
        for (var k = 0; k < a.Count - endA; k++)
            pairs.Add((endA + k, endB + k));
        return pairs;
    }

    /// <summary>A block deleted as a tracked change (see Changed).</summary>
    static XElement DeletedBlock(XElement block, Tracking tracking, XNamespace w)
    {
        Changed(block, tracking, w, insert: false);
        return block;
    }

    /// <summary>A block inserted as a tracked change (see Changed).</summary>
    static XElement InsertedBlock(XElement block, Tracking tracking, XNamespace w)
    {
        Changed(block, tracking, w, insert: true);
        return block;
    }

    /// <summary>
    /// A whole block marked inserted or deleted: a paragraph's content and mark, each of a table's rows (w:trPr) with their cells'
    /// paragraphs. What another author inserted and is deleted is deleted within that insertion; the author's own insertion deleted goes;
    /// what is deleted already stays so; what is inserted already, or written as a change of its own, stays so.
    /// </summary>
    static void Changed(XElement block, Tracking tracking, XNamespace w, bool insert)
    {
        foreach (var p in block.DescendantsAndSelf(w + "p").Where(p => !p.Ancestors(w + "txbxContent").Any()).ToList())
        {
            var atoms = new List<Atom>();
            Flatten(p, [], atoms, w);
            var content = Emit([.. atoms.Select(a => (a, insert ? Inserting(a, tracking) : Deleting(a, tracking)))], tracking, w);
            foreach (var node in p.Nodes().Where(n => n is not XElement { Name.LocalName: "pPr" }).ToList())
                node.Remove();
            p.Add(content);
            MarkChanged(p, tracking, w, insert);
        }
        foreach (var row in block.DescendantsAndSelf(w + "tr").Where(r => !r.Ancestors(w + "txbxContent").Any()))
        {
            var properties = row.Element(w + "trPr") ?? Made(row, w + "trPr", "tblPrEx");
            if (properties.Elements().Any(e => e.Name.LocalName is "ins" or "del"))
                continue;
            var change = tracking.Mark(w, insert ? "ins" : "del");
            if (properties.Element(w + "trPrChange") is { } kept)
                kept.AddBeforeSelf(change);
            else
                properties.Add(change);
        }
    }

    /// <summary>A properties element made where there is none, after an element the schema puts before it.</summary>
    static XElement Made(XElement parent, XName name, string after)
    {
        var made = new XElement(name);
        if (after.Length > 0 && parent.Element(parent.Name.Namespace + after) is { } before)
            before.AddAfterSelf(made);
        else
            parent.AddFirst(made);
        return made;
    }

    /// <summary>A paragraph's mark inserted or deleted as a tracked change where it is not deleted already or (inserting) inserted.</summary>
    static void MarkChanged(XElement p, Tracking tracking, XNamespace w, bool insert)
    {
        var properties = p.Element(w + "pPr") ?? Made(p, w + "pPr", "");
        var mark = properties.Element(w + "rPr");
        if (mark == null)
        {
            mark = new XElement(w + "rPr");
            if (properties.Element(w + "sectPr") is { } section)
                section.AddBeforeSelf(mark);
            else if (properties.Element(w + "pPrChange") is { } change)
                change.AddBeforeSelf(mark);
            else
                properties.Add(mark);
        }
        var changes = mark.Elements().Where(e => e.Name.LocalName is "ins" or "del" or "moveFrom" or "moveTo").ToList();
        if (changes.Any(c => c.Name.LocalName is "del" or "moveFrom") || insert && changes.Count > 0)
            return;
        // The author's own inserted mark deleted: inserted and deleted both, which accepting and rejecting take away alike.
        var made = tracking.Mark(w, insert ? "ins" : "del");
        if (changes.LastOrDefault() is { } last)
            last.AddAfterSelf(made);
        else
            mark.AddFirst(made);
    }

    /// <summary>
    /// A paragraph's content as atoms (see Atom), with the changes around each from the outermost: runs cut into words, the elements of
    /// runs one each, links, fields and controls whole, marks of no width.
    /// </summary>
    static void Flatten(XElement parent, IReadOnlyList<XElement> changes, List<Atom> atoms, XNamespace w)
    {
        foreach (var e in parent.Elements())
        {
            if (e.Name.Namespace == w && e.Name.LocalName is "pPr")
                continue;
            if (e.Name.Namespace == w && e.Name.LocalName == "r")
            {
                var properties = e.Element(w + "rPr");
                foreach (var c in e.Elements().Where(c => c.Name != w + "rPr"))
                {
                    if (c.Name == w + "lastRenderedPageBreak")
                        continue;
                    if (c.Name == w + "t" || c.Name == w + "delText")
                    {
                        foreach (var token in Tokens(c.Value))
                        {
                            var t = new XElement(w + "t", token);
                            if (token.Length > 0 && (char.IsWhiteSpace(token[0]) || char.IsWhiteSpace(token[^1])))
                                t.SetAttributeValue(XNamespace.Xml + "space", "preserve");
                            atoms.Add(new Atom("t" + token + State(changes), t, properties, changes, false));
                        }
                        continue;
                    }
                    var content = new XElement(c);
                    if (content.Name == w + "delInstrText")
                        content.Name = w + "instrText";
                    atoms.Add(new Atom("x" + Key(content) + State(changes), content, properties, changes, false));
                }
                continue;
            }
            if (e.Name.Namespace == w && e.Name.LocalName is "ins" or "del" or "moveFrom" or "moveTo")
            {
                Flatten(e, [.. changes, e], atoms, w);
                continue;
            }
            var mark = e.Name.Namespace == w && (IsMark(e) || e.Name.LocalName is "commentReference");
            atoms.Add(new Atom((mark ? "m" : "c") + Key(e) + State(changes), new XElement(e), null, changes, mark));
        }
    }

    /// <summary>The changes around an atom as its key tells them: each one's kind and author.</summary>
    static string State(IReadOnlyList<XElement> changes) =>
        string.Concat(changes.Select(c => "|" + c.Name.LocalName + ":" + (string?)c.Attribute(c.Name.Namespace + "author")));

    /// <summary>An atom of the old blocks the new ones lack, deleted (see Changed).</summary>
    static IReadOnlyList<XElement> Deleting(Atom atom, Tracking tracking)
    {
        if (atom.Mark)
            return atom.Changes;
        if (!atom.Present)
            return atom.Changes;
        // The author's own insertion goes (no changes: left out, see Emit), another's is deleted within it.
        if (atom.Changes.Count > 0 && atom.Changes[^1].Name.LocalName is "ins" or "moveTo" && tracking.Own(atom.Changes[^1]))
            return [LeftOut];
        return [.. atom.Changes, Deletion];
    }

    /// <summary>An atom of the new blocks the old ones lack, inserted where it reads (one deleted there was not: left out).</summary>
    static IReadOnlyList<XElement> Inserting(Atom atom, Tracking tracking)
    {
        if (atom.Mark)
            return atom.Changes;
        if (!atom.Present)
            return [LeftOut];
        return atom.Changes.Count > 0 ? atom.Changes : [Insertion];
    }

    /// <summary>Stand-ins for a change of the author's to be made (see Emit) and for an atom left out.</summary>
    static readonly XElement Insertion = new("insertion"), Deletion = new("deletion"), LeftOut = new("gone");

    /// <summary>
    /// Two paragraphs much alike merged: the new one's properties, a w:pPrChange with the old's where they differ (and the new has none);
    /// their atoms compared, those alike kept (formatted otherwise: a w:rPrChange with the old's look), the old's alone deleted, the new's
    /// alone inserted, the deleted before the inserted at each place; the new mark as it is.
    /// </summary>
    static XElement Pair(XElement old, XElement now, Tracking tracking, XNamespace w)
    {
        if (old.Name == w + "tbl" && now.Name == w + "tbl")
            return Table(old, now, tracking, w);
        var (a, b) = (new List<Atom>(), new List<Atom>());
        Flatten(old, [], a, w);
        Flatten(now, [], b, w);
        // A deletion the new one writes of text the old has as it read matches it (it marks that text deleted).
        static string Matching(Atom atom, Tracking tracking) =>
            !atom.Present && atom.Changes is [var only] && only.Name.LocalName == "del" && tracking.Own(only) ? atom.Key[..atom.Key.IndexOf('|')] : atom.Key;
        var pairs = Common([.. a.Select(x => x.Key)], [.. b.Select(x => Matching(x, tracking))]);
        var emitted = new List<(Atom, IReadOnlyList<XElement>)>();
        var (i, j) = (0, 0);
        // The look given last, from which to which, with its change: atoms one after the other formatted alike share it (one run).
        (string Had, string Look, XElement Properties)? formatted = null;
        foreach (var (x, y) in pairs.Append((a.Count, b.Count)))
        {
            for (; i < x; i++)
                emitted.Add((a[i], Deleting(a[i], tracking)));
            for (; j < y; j++)
                emitted.Add((b[j], Inserting(b[j], tracking)));
            if (i < x || j < y)
                formatted = null;
            if (x < a.Count && y < b.Count)
            {
                var (before, after) = (a[x], b[y]);
                // Alike in text: the new one's changes, its look, with the old look kept where it differs.
                var atom = after;
                var (look, had) = (Look(after.RunProperties, w), Look(before.RunProperties, w));
                if (!after.Mark && look != had && after.RunProperties?.Element(w + "rPrChange") == null)
                {
                    if (formatted is not var (sameHad, sameLook, properties) || sameHad != had || sameLook != look)
                    {
                        properties = after.RunProperties != null ? new XElement(after.RunProperties) : new XElement(w + "rPr");
                        var change = tracking.Mark(w, "rPrChange");
                        change.Add(new XElement(w + "rPr", before.RunProperties?.Elements().Where(e => e.Name.LocalName != "rPrChange").Select(e => new XElement(e))));
                        properties.Add(change);
                        formatted = (had, look, properties);
                    }
                    atom = after with { RunProperties = properties };
                }
                else
                    formatted = null;
                emitted.Add((atom, after.Changes));
                (i, j) = (x + 1, y + 1);
            }
        }
        var merged = new XElement(now.Name, now.Attributes());
        if (now.Element(w + "pPr") is { } properties0)
            merged.Add(new XElement(properties0));
        // The paragraph's own properties as they were, where they differ.
        static List<XElement> Own(XElement? pPr) => pPr?.Elements().Where(e => e.Name.LocalName is not ("rPr" or "sectPr" or "pPrChange")).ToList() ?? [];
        var (mine, theirs) = (Own(now.Element(w + "pPr")), Own(old.Element(w + "pPr")));
        if (string.Concat(mine.Select(Key)) != string.Concat(theirs.Select(Key)) && now.Element(w + "pPr")?.Element(w + "pPrChange") == null)
        {
            var properties = merged.Element(w + "pPr") ?? Made(merged, w + "pPr", "");
            var change = tracking.Mark(w, "pPrChange");
            change.Add(new XElement(w + "pPr", theirs.Select(e => new XElement(e))));
            properties.Add(change);
        }
        merged.Add(Emit(emitted, tracking, w));
        return merged;
    }

    /// <summary>A run's look as compared: its properties but a change of them, without the ids of editing sessions.</summary>
    static string Look(XElement? properties, XNamespace w) =>
        properties == null ? "" : string.Concat(properties.Elements().Where(e => e.Name.LocalName is not ("rPrChange" or "ins" or "del" or "moveFrom" or "moveTo")).Select(Key));

    /// <summary>
    /// Two tables merged: the new one's properties and grid; their rows paired by their text where alike, else in order where they have as
    /// many cells (each pair of cells compared as blocks), the old's alone deleted, the new's alone inserted.
    /// </summary>
    static XElement Table(XElement old, XElement now, Tracking tracking, XNamespace w)
    {
        var merged = new XElement(now.Name, now.Attributes(), now.Elements().Where(e => e.Name.LocalName is not "tr").Select(e => new XElement(e)));
        var (a, b) = (old.Elements(w + "tr").ToList(), now.Elements(w + "tr").ToList());
        var pairs = Common([.. a.Select(Key)], [.. b.Select(Key)]);
        var (i, j) = (0, 0);
        void Rows(int x, int y)
        {
            // Rows between those alike: paired in order where they have as many cells, the rest deleted, then inserted.
            var (gapA, gapB) = (a.GetRange(i, x - i), b.GetRange(j, y - j));
            var paired = Math.Min(gapA.Count, gapB.Count);
            for (var k = 0; k < paired; k++)
            {
                if (gapA[k].Elements(w + "tc").Count() != gapB[k].Elements(w + "tc").Count())
                {
                    paired = k;
                    break;
                }
            }
            for (var k = 0; k < paired; k++)
                merged.Add(Row(gapA[k], gapB[k], tracking, w));
            foreach (var row in gapA.Skip(paired))
                merged.Add(DeletedBlock(new XElement(row), tracking, w));
            foreach (var row in gapB.Skip(paired))
                merged.Add(InsertedBlock(new XElement(row), tracking, w));
        }
        foreach (var (x, y) in pairs.Append((a.Count, b.Count)))
        {
            Rows(x, y);
            if (x < a.Count && y < b.Count)
                merged.Add(new XElement(b[y]));
            (i, j) = (x + 1, y + 1);
        }
        return merged;
    }

    /// <summary>Two rows of as many cells merged: the new row's properties, each cell's blocks compared with the old cell's.</summary>
    static XElement Row(XElement old, XElement now, Tracking tracking, XNamespace w)
    {
        var merged = new XElement(now.Name, now.Attributes(), now.Elements().Where(e => e.Name.LocalName is not "tc").Select(e => new XElement(e)));
        foreach (var (before, after) in old.Elements(w + "tc").Zip(now.Elements(w + "tc")))
        {
            var cell = new XElement(after.Name, after.Attributes(), after.Elements(w + "tcPr").Select(e => new XElement(e)));
            var (blocksBefore, blocksAfter) = (before.Elements().Where(e => e.Name.LocalName != "tcPr").ToList(), after.Elements().Where(e => e.Name.LocalName != "tcPr").ToList());
            foreach (var (element, _) in Blocks(blocksBefore, blocksAfter, tracking, w))
                cell.Add(ReferenceEquals(element.Parent, after) ? new XElement(element) : element);
            merged.Add(cell);
        }
        return merged;
    }

    /// <summary>
    /// Atoms written as runs with the changes around them: atoms one after the other with the same changes and look in one run (words in
    /// one w:t, deleted ones in a w:delText), runs with the same changes in one w:ins or w:del, the author's new changes made (one for
    /// each stretch of them) and those the atoms had copied, a copy taking an id of its own where one change is written in pieces.
    /// </summary>
    static List<XElement> Emit(List<(Atom Atom, IReadOnlyList<XElement> Changes)> atoms, Tracking tracking, XNamespace w)
    {
        var output = new List<XElement>();
        // The changes open (outermost first), each with the element written for it.
        var open = new List<(XElement Source, XElement Written)>();
        var used = new HashSet<XElement>();
        XElement? run = null;
        string? runLook = null;
        XElement? text = null;
        XElement Container() => open.Count > 0 ? open[^1].Written : null!;
        void Put(XElement element)
        {
            if (open.Count > 0)
                open[^1].Written.Add(element);
            else
                output.Add(element);
        }
        foreach (var (atom, changes) in atoms)
        {
            if (changes is [var gone] && ReferenceEquals(gone, LeftOut))
                continue;
            // The changes as they stay open from the last atom: those in common at the start.
            var common = 0;
            while (common < open.Count && common < changes.Count && ReferenceEquals(open[common].Source, changes[common]))
                common++;
            if (common < open.Count)
            {
                open.RemoveRange(common, open.Count - common);
                (run, text) = (null, null);
            }
            for (var k = common; k < changes.Count; k++)
            {
                var source = changes[k];
                XElement written;
                if (ReferenceEquals(source, Insertion) || ReferenceEquals(source, Deletion))
                    written = tracking.Mark(w, ReferenceEquals(source, Insertion) ? "ins" : "del");
                else
                {
                    written = new XElement(source.Name, source.Attributes());
                    if (!used.Add(source))
                        written = tracking.Copy(written);
                }
                Put(written);
                open.Add((source, written));
                (run, text) = (null, null);
            }
            var deleted = changes.Any(c => ReferenceEquals(c, Deletion) || c.Name.LocalName == "del");
            if (atom.RunProperties == null && atom.Content.Name != w + "t" && !IsRunContent(atom.Content, w))
            {
                // A link, field or control whole: its runs changed within it as the atom is.
                var whole = new XElement(atom.Content);
                if (deleted)
                {
                    foreach (var t in whole.Descendants(w + "t").ToList())
                        t.Name = w + "delText";
                    foreach (var instruction in whole.Descendants(w + "instrText").ToList())
                        instruction.Name = w + "delInstrText";
                }
                Put(whole);
                (run, text) = (null, null);
                continue;
            }
            var look = atom.RunProperties?.ToString(SaveOptions.DisableFormatting) ?? "";
            if (run == null || look != runLook)
            {
                // A formatting change written in pieces: each piece an id of its own.
                var properties = atom.RunProperties != null ? new XElement(atom.RunProperties) : null;
                if (atom.RunProperties?.Element(w + "rPrChange") is { } source && !used.Add(source) && properties?.Element(w + "rPrChange") is { } piece)
                    piece.SetAttributeValue(w + "id", tracking.Copy(piece).Attribute(w + "id")!.Value);
                run = new XElement(w + "r", properties);
                runLook = look;
                text = null;
                Put(run);
            }
            if (atom.Content.Name == w + "t")
            {
                var name = deleted ? w + "delText" : w + "t";
                if (text == null || text.Name != name)
                {
                    text = new XElement(name);
                    run.Add(text);
                }
                text.Value += atom.Content.Value;
                if (text.Value.Length > 0 && (char.IsWhiteSpace(text.Value[0]) || char.IsWhiteSpace(text.Value[^1])))
                    text.SetAttributeValue(XNamespace.Xml + "space", "preserve");
                continue;
            }
            var content = new XElement(atom.Content);
            if (deleted && content.Name == w + "instrText")
                content.Name = w + "delInstrText";
            run.Add(content);
            text = null;
        }
        return output;
    }

    /// <summary>Whether an element is one of a run's (a tab, a break, a drawing, a field's character …), which goes into a run.</summary>
    static bool IsRunContent(XElement e, XNamespace w) => e.Name.Namespace == w && e.Name.LocalName is "tab" or "ptab" or "br" or "cr" or "drawing" or "pict" or "object"
        or "fldChar" or "instrText" or "delInstrText" or "sym" or "noBreakHyphen" or "softHyphen" or "footnoteReference" or "endnoteReference" or "footnoteRef"
        or "endnoteRef" or "separator" or "continuationSeparator" or "annotationRef" or "ruby" or "contentPart" or "dayShort" or "monthShort" or "yearShort"
        or "dayLong" or "monthLong" or "yearLong" or "pgNum" or "commentReference" or "t" or "delText";
}
