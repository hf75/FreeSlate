using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Opc;

namespace FreeSlate;

/// <summary>
/// A version of a section's text (see WordDocument.Versions): <paramref name="Current"/> the one that stands in the
/// document; the others are kept beside it, and each can be shown in its place and edited there.
/// </summary>
public sealed record TextVersion(string Id, string Name, DateTime? Date, bool Current);

/// <summary>A note for planning a section (see WordDocument.PlanningNotes): plain text, its lines apart by line feeds.</summary>
public sealed record PlanningNote(string Id, string Author, DateTime? Date, string Text);

/// <summary>
/// A section's versions and planning notes. A section is a paragraph of the body itself, a heading as a rule, and what
/// follows it up to the next heading of its level or above (a paragraph that is no heading: up to the next heading); what it
/// keeps besides its text stands in that paragraph (see Docx.SectionPlan), travels with it, is taken away and comes back with
/// it. Word shows none of it and writes none of it back when it saves. Each change is a version of the document to undo,
/// that of a version shown too; the document's other parts stay as they are, but for what a change names.
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>The heading a paragraph of the body stands under (itself, for a heading): null before the first; headings without text are none.</summary>
    /// <exception cref="NotSupportedException">The paragraph is not in the body; nothing changes.</exception>
    public int? HeadingOf(int paragraph)
    {
        Read();
        if (!paragraphs.TryGetValue(paragraph, out var found))
            throw new NotSupportedException("Only the body's paragraphs stand in sections.");
        for (var k = IndexOf(found.Top, walking: true); k >= 0; k--)
        {
            if (blocks[k] is ParagraphBlock heading && IsHeading(heading))
                return heading.Id;
        }
        return null;
    }

    static bool IsHeading(ParagraphBlock paragraph) => paragraph.Format.OutlineLevel is < 9 && ParagraphText.Of(paragraph).Length > 0;

    /// <summary>The paragraphs of the body that hold versions or planning notes, in reading order.</summary>
    public IReadOnlyList<int> PlannedParagraphs()
    {
        Read();
        return [.. blocks.OfType<ParagraphBlock>().Where(Planned).Select(p => p.Id)];
    }

    /// <summary>The versions of the section a paragraph begins, in the order they were made; none where it has but its text.</summary>
    /// <exception cref="NotSupportedException">The paragraph does not stand in the body itself; nothing changes.</exception>
    public IReadOnlyList<TextVersion> Versions(int paragraph)
    {
        var (p, _, _) = SectionAt(paragraph);
        return [.. PlanOf(p).Versions.Select(v => new TextVersion(v.Id, v.Name, v.Date, v.Text == null))];
    }

    /// <summary>
    /// A new version of the section a paragraph begins, in place from now on, named <paramref name="name"/>; the version in
    /// place until now stays as it is, kept beside it (<paramref name="previousName"/> names it where the section has no versions
    /// yet). The new one goes on from the text in place, which a copy keeps as it is: with notes and comments of its own, not
    /// the versions a sub-section in it keeps, which stay with the text in place; with <paramref name="markup"/> (blocks, see
    /// InsertBlocks) it holds that instead. Returns its id.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The paragraph does not stand in the body itself, a field stands over the section's edge or a content control holds both
    /// its end and what follows; with markup, also where changes are tracked or the markup holds other blocks than paragraphs
    /// and tables; nothing changes.
    /// </exception>
    /// <exception cref="ArgumentException">The markup is not well-formed XML; nothing changes.</exception>
    public string AddVersion(int paragraph, string name, string? previousName = null, string? markup = null, IReadOnlyList<NewPart>? parts = null)
    {
        var (p, i, j) = SectionAt(paragraph);
        var when = Writer("").Date;
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var id = "";
        if (markup != null)
        {
            Untracked();
            var elements = Elements(Added(markup, parts, changes, context.MainPart), RootOf(context.MainPart));
            if (elements.Any(e => e.Name != Wml.W + "p" && e.Name != Wml.W + "tbl"))
                throw new NotSupportedException("Blocks put in are paragraphs and tables.");
            // A table does not end the body (see InsertBlocks).
            if (j == blocks.Count && elements.Count > 0 && elements[^1].Name == Wml.W + "tbl")
                elements.Add(new XElement(Wml.W + "p"));
            Strictly(context.MainPart, elements);
            Exchange(p, i, j, (plan, active, stretch, last) =>
            {
                var current = plan.InPlace(previousName ?? "", when);
                plan.Replace(current, current with { Text = active });
                id = plan.NewVersionId();
                plan.Versions.Add(new SectionPlan.Version(id, name, when, null));
                // Written with the prefixes of the stretch's scope, which they stand in for that.
                var copies = elements.Select(e => new XElement(e)).ToList();
                last.AddAfterSelf(copies);
                var written = string.Concat(copies.Select(PartText.Xml));
                foreach (var copy in copies)
                    copy.Remove();
                return written;
            }, changes);
            return id;
        }
        Whole(i, j);
        var section = StretchOf(i, j - 1);
        Fieldless(section, i, j);
        var (from, to) = Span(section, p, j - 1 > i ? blocks[j - 1] : null);
        var copied = Copied(section, from, to, changes);
        Change(p, p, (stretch, _) =>
        {
            var own = TextEdits.Parts(stretch, p);
            var plan = SectionPlan.Read(stretch.Text, SectionPlan.In(own));
            var current = plan.InPlace(previousName ?? "", when);
            plan.Replace(current, current with { Text = copied });
            id = plan.NewVersionId();
            plan.Versions.Add(new SectionPlan.Version(id, name, when, null));
            return stretch.Spliced(SectionPlan.Splices(stretch.Text, own, plan.Written()));
        }, changes: changes);
        return id;
    }

    /// <summary>
    /// A version of the section a paragraph begins put in place, the one in place kept beside it, every character of both as
    /// it was: the paragraphs that show are those the method returns (ids of their own); the paragraph and those after the
    /// section keep theirs. Nothing changes where the version is in place already.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The paragraph does not stand in the body itself, a field stands over the section's edge or a content control holds both
    /// its end and what follows, or changes are tracked (a version put in place is no tracked change); nothing changes.
    /// </exception>
    /// <exception cref="ArgumentException">The section has no such version; nothing changes.</exception>
    public IReadOnlyList<int> ShowVersion(int paragraph, string id)
    {
        var (p, i, j) = SectionAt(paragraph);
        var wanted = PlanOf(p).Versions.FirstOrDefault(v => v.Id == id) ?? throw new ArgumentException($"The section has no version {id}.", nameof(id));
        if (wanted.Text != null)
        {
            Untracked();
            Exchange(p, i, j, (plan, active, _, _) =>
            {
                var current = plan.InPlace("", null);
                plan.Replace(current, current with { Text = active });
                var coming = plan.Versions.First(v => v.Id == id);
                plan.Replace(coming, coming with { Text = null });
                return coming.Text!;
            }, null);
        }
        var (_, first, end) = SectionAt(paragraph);
        return [.. blocks.Skip(first + 1).Take(end - first - 1).SelectMany(b => (b is ParagraphBlock q ? [q.Id] : Enumerable.Empty<int>()).Concat(Within(b)))];
    }

    /// <summary>A version of the section a paragraph begins named anew.</summary>
    /// <exception cref="ArgumentException">The section has no such version; nothing changes.</exception>
    public void RenameVersion(int paragraph, string id, string name) => ChangePlan(paragraph, plan =>
    {
        var version = plan.Versions.FirstOrDefault(v => v.Id == id) ?? throw new ArgumentException($"The section has no version {id}.", nameof(id));
        plan.Replace(version, version with { Name = name });
    });

    /// <summary>
    /// A version of the section a paragraph begins taken away, what only it refers to with it: its notes, its comments, its
    /// pictures and links. The last one left beside the version in place, where no notes stay, leaves the section as if it
    /// never had any.
    /// </summary>
    /// <exception cref="NotSupportedException">The version is the one in place (another is shown first); nothing changes.</exception>
    /// <exception cref="ArgumentException">The section has no such version; nothing changes.</exception>
    public void RemoveVersion(int paragraph, string id)
    {
        var (p, _, _) = SectionAt(paragraph);
        var parked = PlanOf(p).Versions.FirstOrDefault(v => v.Id == id) ?? throw new ArgumentException($"The section has no version {id}.", nameof(id));
        if (parked.Text == null)
            throw new NotSupportedException("The version in place is not taken away: another is shown first.");
        // (Notes go by themselves: see NotesGone.)
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var main = MainText();
        var comments = CommentsIn(parked.Text).Where(c => Regex.Count(main, CommentMark(c)) <= Regex.Count(parked.Text, CommentMark(c))).ToHashSet(StringComparer.Ordinal);
        if (comments.Count > 0)
            CommentsTaken(comments, changes);
        Unrelated(context.MainPart, [.. package.Relationships(context.MainPart).Where(r => Sweepable(r) && parked.Text.Contains($"\"{r.Id}\"", StringComparison.Ordinal)
            && Occurrences(main, $"\"{r.Id}\"") <= Occurrences(parked.Text, $"\"{r.Id}\"")).Select(r => r.Id)], changes);
        ChangePlan(paragraph, plan => plan.Versions.RemoveAll(v => v.Id == id), changes);
    }

    /// <summary>The planning notes of the section a paragraph begins, in the order they were made.</summary>
    /// <exception cref="NotSupportedException">The paragraph does not stand in the body itself; nothing changes.</exception>
    public IReadOnlyList<PlanningNote> PlanningNotes(int paragraph)
    {
        var (p, _, _) = SectionAt(paragraph);
        return [.. PlanOf(p).Notes.Select(n => new PlanningNote(n.Id, n.Author, n.Date, n.Text))];
    }

    /// <summary>
    /// A planning note put on the section a paragraph begins: by <paramref name="author"/> (the author of tracked changes where
    /// null) at ChangedAt (now where null), as the document's settings let them be (see Tracked). Returns its id.
    /// </summary>
    public string AddPlanningNote(int paragraph, string text, string? author = null)
    {
        var (who, when) = Writer(author ?? Author ?? "");
        var id = "";
        ChangePlan(paragraph, plan =>
        {
            id = plan.NewNoteId();
            plan.Notes.Add(new SectionPlan.Note(id, who, when, text));
        });
        return id;
    }

    /// <summary>A planning note written anew, dated now.</summary>
    /// <exception cref="ArgumentException">The section has no such note; nothing changes.</exception>
    public void ChangePlanningNote(int paragraph, string id, string text)
    {
        var when = Writer("").Date;
        ChangePlan(paragraph, plan =>
        {
            var at = plan.Notes.FindIndex(n => n.Id == id);
            if (at < 0)
                throw new ArgumentException($"The section has no note {id}.", nameof(id));
            plan.Notes[at] = plan.Notes[at] with { Date = when, Text = text };
        });
    }

    /// <summary>A planning note taken away.</summary>
    /// <exception cref="ArgumentException">The section has no such note; nothing changes.</exception>
    public void RemovePlanningNote(int paragraph, string id) => ChangePlan(paragraph, plan =>
    {
        if (plan.Notes.RemoveAll(n => n.Id == id) == 0)
            throw new ArgumentException($"The section has no note {id}.", nameof(id));
    });

    /// <summary>
    /// A version of the section a paragraph begins as a document of its own, to look at it without putting it in place: the
    /// paragraph and the version's blocks, on the pages of the section it stands in, with the document's other parts as they
    /// are now. What is changed in it stays there. Its caller disposes of it.
    /// </summary>
    /// <exception cref="NotSupportedException">The paragraph does not stand in the body itself, or the document's text cannot be taken apart so; nothing changes.</exception>
    /// <exception cref="ArgumentException">The section has no such version; nothing changes.</exception>
    public WordDocument OpenVersion(int paragraph, string id)
    {
        var (p, i, j) = SectionAt(paragraph);
        var version = PlanOf(p).Versions.FirstOrDefault(v => v.Id == id);
        if (version == null)
            throw new ArgumentException($"The section has no version {id}.", nameof(id));
        if (head.Length == 0)
            throw new NotSupportedException("The document's text cannot be taken apart.");
        var own = StretchOf(i, i);
        var parts = TextEdits.Parts(own, p);
        var (start, end) = (own.Text.Start(parts[0]), own.Text.End(own.Text.Start(parts[^1])));
        var heading = new StringBuilder();
        var at = start;
        foreach (var alternative in SectionPlan.In(parts))
        {
            var from = own.Text.Start(alternative);
            heading.Append(own.Text.Text, at, from - at);
            at = own.Text.End(from);
        }
        heading.Append(own.Text.Text, at, end - at);
        var text = version.Text;
        if (text == null)
        {
            var section = StretchOf(i, j - 1);
            var (first, last) = Span(section, p, j - 1 > i ? blocks[j - 1] : null);
            text = section.Text.Text[first..last];
        }
        var main = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" + head + heading + text + SectionProperties(paragraph) + tail;
        using var stream = new MemoryStream();
        package.Save(stream, new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase) { [context.MainPart] = new UTF8Encoding(false).GetBytes(main) });
        return Open(stream.ToArray(), null, fonts, culture, printed, markup);
    }

    /// <summary>The w:sectPr of the section (of pages) a paragraph stands in as the main part's text has it, empty where it has none.</summary>
    string SectionProperties(int paragraph)
    {
        if (SectionEndOf(paragraph) is { EndsSection: { } ends } end)
        {
            var stretch = StretchOf(IndexOf(end, walking: true), IndexOf(end, walking: true));
            if (ends.Element is { } read && stretch.Find(read) is { } found && stretch.Text.Start(found) is var at)
                return stretch.Text.Text[at..stretch.Text.End(at)];
        }
        if (BodySection() is { } last && blocks.Count > 0)
        {
            var stretch = StretchOf(blocks.Count - 1, blocks.Count - 1, lastSection: true);
            if (stretch.Find(last) is { } found && stretch.Text.Start(found) is var at)
                return stretch.Text.Text[at..stretch.Text.End(at)];
        }
        return "";
    }

    /// <summary>
    /// The section a paragraph begins moved in front of another paragraph of the body (to the end of the body without one),
    /// with what it keeps besides its text: every character as it was, the paragraphs with the ids they had, one change.
    /// Nothing changes where it stands there already.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// A paragraph does not stand in the body itself, a field stands over the section's edge, a content control holds both its
    /// end and what follows, or changes are tracked (a move is no tracked change here); nothing changes.
    /// </exception>
    /// <exception cref="ArgumentException">The place is within the section; nothing changes.</exception>
    public void MoveSection(int paragraph, int? before = null)
    {
        var (p, i, j) = SectionAt(paragraph);
        var target = blocks.Count;
        if (before is { } place)
        {
            var (q, top) = Find(place);
            if (!paragraphs.ContainsKey(place) || !ReferenceEquals(q, top) || Top(First(q)) is null)
                throw new NotSupportedException("A section goes before a paragraph of the body itself.");
            target = IndexOf(top, walking: true);
            if (target > i && target < j)
                throw new ArgumentException("The place is within the section.", nameof(before));
        }
        if (target == j || target == i)
            return;
        Untracked();
        Whole(i, j);
        // The blocks that change places: the section and those it passes.
        var (lo, hi) = (Math.Min(i, target), Math.Max(j, target));
        var last = hi - 1;
        var ending = hi == blocks.Count;
        var made = 0;
        Change(blocks[lo], blocks[last], (stretch, _) =>
        {
            var text = stretch.Text;
            // The section from the start of its paragraph to the end of its last block.
            var start = text.Start(TextEdits.Parts(stretch, p)[0]);
            var end = EndOf(stretch, blocks[j - 1]);
            if (TextEdits.OpenFields(ElementsOf(stretch, i, j).SelectMany(e => e.DescendantsAndSelf())) != 0)
                throw new NotSupportedException("A field stands over the section's edge.");
            var section = text.Text[start..end];
            var stop = EndOf(stretch, blocks[last]);
            string own;
            if (target < i)
            {
                var from = StartOf(stretch, blocks[target]);
                own = stretch.Spliced([(from, from, section), (start, end, "")]);
            }
            else
                own = stretch.Spliced([(start, end, ""), (stop, stop, section)]);
            // A table does not end the body (see InsertBlocks).
            if (ending && stretch.Parsed(own).Root!.Elements().First().Elements().LastOrDefault() is { Name.LocalName: "tbl" })
            {
                own += ParagraphTag(text, start);
                made = 1;
            }
            return own;
        },
        () =>
        {
            var section = blocks.Skip(i).Take(j - i).ToList();
            var moved = target < i
                ? blocks.Take(target).Concat(section).Concat(blocks.Skip(target).Take(i - target)).Concat(blocks.Skip(j))
                : blocks.Take(i).Concat(blocks.Skip(j).Take(target - j)).Concat(section).Concat(blocks.Skip(target));
            return [.. moved, .. Enumerable.Range(0, made).Select(_ => (Block)new ModelReader.NewBlock())];
        });
    }

    /// <summary>
    /// Writes a copy of the document without any version but those in place, without any planning note: what they hold is
    /// gone from every part, and with it what only they refer to, as Word's saving takes it away: notes, comments, pictures,
    /// links (a style or a list only they used stays, as in Word, unused). The document itself stays as it is.
    /// </summary>
    public void SaveWithoutPlans(Stream output)
    {
        Read();
        var main = new PartText(Spliced(whole, islands));
        var cuts = new List<(int Start, int End, string Text)>();
        for (var at = SectionPlan.Next(main.Text, 0); at >= 0; at = SectionPlan.Next(main.Text, cuts[^1].End))
            cuts.Add((at, main.End(at), ""));
        var instead = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase) { [context.MainPart] = main.Spliced(cuts) };
        Sweep(main.Replaced(cuts), instead);
        package.Save(output, instead);
    }

    /// <summary>Saves a copy of the document without its plans as a DOCX file (see SaveWithoutPlans).</summary>
    public void SaveWithoutPlans(string target)
    {
        target = Path.GetFullPath(target);
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $"~{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        using (var stream = File.Create(temporary))
            SaveWithoutPlans(stream);
        File.Move(temporary, target, overwrite: true);
    }

    /// <summary>The main part's text as it reads now.</summary>
    string MainText() => wholeText.Replaced(islands.Select(i => (i.Start, i.End, i.Text)));

    /// <summary>Whether a paragraph of the body holds a plan (see MarkupCompatibility.PlanHeld).</summary>
    static bool Planned(ParagraphBlock paragraph) =>
        paragraph.Parts is { } parts ? parts.Any(e => e.Annotation<MarkupCompatibility.PlanHeld>() != null) : paragraph.Element.Annotation<MarkupCompatibility.PlanHeld>() != null;

    /// <summary>The section a paragraph begins: the paragraph, its place among the body's blocks and the place of the first block after the section.</summary>
    (ParagraphBlock Paragraph, int First, int End) SectionAt(int paragraph)
    {
        Read();
        if (!paragraphs.TryGetValue(paragraph, out var found) || !ReferenceEquals(found.Paragraph, found.Top) || found.Paragraph.Placeholder != null
            || Top(First(found.Paragraph)) != First(found.Paragraph))
            throw new NotSupportedException("A section begins with a paragraph of the body itself.");
        var p = found.Paragraph;
        var i = IndexOf(p, walking: true);
        var level = p.Format.OutlineLevel is { } own and < 9 ? own : 8;
        var j = i + 1;
        while (j < blocks.Count && !(blocks[j] is ParagraphBlock { Format.OutlineLevel: { } other } next && other <= level && IsHeading(next)))
            j++;
        return (p, i, j);
    }

    /// <summary>The plan a paragraph of the body holds, an empty one where it holds none.</summary>
    SectionPlan PlanOf(ParagraphBlock paragraph)
    {
        if (!Planned(paragraph))
            return new SectionPlan();
        var at = IndexOf(paragraph, walking: true);
        var stretch = StretchOf(at, at);
        return SectionPlan.Read(stretch.Text, SectionPlan.In(TextEdits.Parts(stretch, paragraph)));
    }

    /// <summary>A change of what a paragraph's plan holds, the section's text as it is: one version of the document.</summary>
    void ChangePlan(int paragraph, Action<SectionPlan> change, IReadOnlyDictionary<string, byte[]?>? changes = null)
    {
        var (p, _, _) = SectionAt(paragraph);
        Change(p, p, (stretch, _) =>
        {
            var parts = TextEdits.Parts(stretch, p);
            var plan = SectionPlan.Read(stretch.Text, SectionPlan.In(parts));
            change(plan);
            return stretch.Spliced(SectionPlan.Splices(stretch.Text, parts, plan.Written()));
        }, changes: changes);
    }

    void Untracked()
    {
        if (Author != null)
            throw new NotSupportedException("Changes are tracked: a version put in place or a section moved would not be one.");
    }

    /// <summary>
    /// The section's text exchanged in one change: <paramref name="edit"/> is given the plan, the text in place, the stretch and
    /// the last element before that text's end, changes the plan and gives the text that comes in. The paragraph and the blocks
    /// after the section keep their ids; those that come in take their own.
    /// </summary>
    void Exchange(ParagraphBlock p, int i, int j, Func<SectionPlan, string, Stretch, XElement, string> edit, IReadOnlyDictionary<string, byte[]?>? changes)
    {
        Whole(i, j);
        var last = j - 1 > i ? blocks[j - 1] : null;
        var made = 0;
        Change(p, blocks[j - 1], (stretch, _) =>
        {
            var text = stretch.Text;
            var parts = TextEdits.Parts(stretch, p);
            var (from, to) = Span(stretch, p, last);
            Fieldless(stretch, i, j);
            var plan = SectionPlan.Read(text, SectionPlan.In(parts));
            var coming = edit(plan, text.Text[from..to], stretch, parts[^1]);
            var own = stretch.Spliced([.. SectionPlan.Splices(text, parts, plan.Written()), (from, to, coming)]);
            made = BlocksIn(stretch.Parsed(own).Root!.Elements().First()) - 1;
            return own;
        },
        () => [.. blocks.Take(i + 1), .. Enumerable.Range(0, Math.Max(0, made)).Select(_ => (Block)new ModelReader.NewBlock()), .. blocks.Skip(j)],
        changes);
    }

    /// <summary>A section's text ends where a child of the body ends, not within what also holds what follows it.</summary>
    void Whole(int i, int j)
    {
        if (j - 1 > i && j < blocks.Count && Top(Last(blocks[j - 1])) == Top(First(blocks[j])))
            throw new NotSupportedException("A content control holds the section's end and what follows.");
    }

    /// <summary>No field stands over the edge of a section's text (see TextEdits.RemoveBetween), which is taken away whole when another version comes in.</summary>
    void Fieldless(Stretch stretch, int i, int j)
    {
        if (TextEdits.OpenFields(ElementsOf(stretch, i + 1, j).SelectMany(e => e.DescendantsAndSelf())) != 0)
            throw new NotSupportedException("A field stands over the section's edge.");
    }

    /// <summary>An empty paragraph as the stretch's text names one, from a paragraph's start.</summary>
    static string ParagraphTag(PartText text, int paragraph) => $"<{Qualified(text.Text, paragraph)}/>";

    /// <summary>How many blocks a container's children read as, as nearly as the XML tells: paragraphs and tables, those in what holds them.</summary>
    static int BlocksIn(XElement container) =>
        container.Elements().Sum(e => e.Name.LocalName switch
        {
            "p" or "tbl" => 1,
            "sdt" => e.Elements(e.Name.Namespace + "sdtContent").Sum(BlocksIn),
            "customXml" or "smartTag" or "ins" or "moveTo" => BlocksIn(e),
            _ => 0,
        });

    /// <summary>Where the text of a section stands in a stretch that holds it: from the end of its paragraph to the end of its last block (without any: nothing).</summary>
    (int From, int To) Span(Stretch stretch, ParagraphBlock paragraph, Block? last)
    {
        var from = stretch.Text.End(stretch.Text.Start(TextEdits.Parts(stretch, paragraph)[^1]));
        return (from, last != null ? EndOf(stretch, last) : from);
    }

    /// <summary>Where the child of the body a block's first element stands in begins in a stretch.</summary>
    int StartOf(Stretch stretch, Block block) => stretch.Text.Start(ChildOf(stretch, First(block)));

    /// <summary>Where the child of the body a block's last element stands in ends in a stretch.</summary>
    int EndOf(Stretch stretch, Block block) => stretch.Text.End(stretch.Text.Start(ChildOf(stretch, Last(block))));

    XElement ChildOf(Stretch stretch, XElement read) =>
        (Top(read) is { } top ? stretch.Find(top) : null) ?? throw new NotSupportedException("The block is not where it was read.");

    /// <summary>The children of the body in a stretch that the blocks from one place up to another were read from.</summary>
    IEnumerable<XElement> ElementsOf(Stretch stretch, int first, int end)
    {
        if (end <= first)
            return [];
        var (a, b) = (ChildOf(stretch, First(blocks[first])), ChildOf(stretch, Last(blocks[end - 1])));
        return a == b ? [a] : [a, .. a.ElementsAfterSelf().TakeWhile(e => e != b), b];
    }

    /// <summary>
    /// A stretch's text from <paramref name="from"/> to <paramref name="to"/> as a version of its own: without the plans within
    /// it (a sub-section's versions stay with the text in place), its references to notes and comments naming copies of
    /// those, which go among the changes; so a change of a note or a comment in one version does not reach the other.
    /// </summary>
    string Copied(Stretch section, int from, int to, Dictionary<string, byte[]?> changes)
    {
        var text = section.Text;
        var cuts = new List<(int Start, int End, string Text)>();
        var plans = section.Xml.Descendants().Where(SectionPlan.Is).Select(e => text.Start(e)).Where(at => at >= from && at < to).Select(at => (Start: at, End: text.End(at))).ToList();
        foreach (var (start, end) in plans)
            cuts.Add((start, end, ""));
        var marks = section.Xml.Descendants().Where(e => e.Name.LocalName is "footnoteReference" or "endnoteReference" or "commentRangeStart" or "commentRangeEnd" or "commentReference")
            .Select(e => (Element: e, At: text.Start(e))).Where(m => m.At >= from && m.At < to && !plans.Any(p => m.At >= p.Start && m.At < p.End)).ToList();
        static string? Id(XElement e) => e.Attributes().FirstOrDefault(a => a.Name.LocalName == "id")?.Value;
        IReadOnlySet<string> Named(params string[] names) => marks.Where(m => names.Contains(m.Element.Name.LocalName)).Select(m => Id(m.Element)).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var footnotes = NotesCopied(false, Named("footnoteReference"), changes);
        var endnotes = NotesCopied(true, Named("endnoteReference"), changes);
        var comments = CommentsCopied(Named("commentRangeStart", "commentRangeEnd", "commentReference"), changes);
        foreach (var (element, at) in marks)
        {
            var map = element.Name.LocalName switch { "footnoteReference" => footnotes, "endnoteReference" => endnotes, _ => comments };
            if (Id(element) is { } id && map.TryGetValue(id, out var copy))
            {
                var end = text.TagEnd(at);
                cuts.Add((at, end, TagId().Replace(text.Text[at..end], copy, 1)));
            }
        }
        var written = new StringBuilder();
        var place = from;
        foreach (var (start, end, with) in cuts.OrderBy(c => c.Start))
        {
            written.Append(text.Text, place, start - place).Append(with);
            place = end;
        }
        return written.Append(text.Text, place, to - place).ToString();
    }

    /// <summary>Copies of a kind of notes, each right after its note, among the changes; the id of the copy by the id of each.</summary>
    Dictionary<string, string> NotesCopied(bool endnote, IReadOnlySet<string> ids, Dictionary<string, byte[]?> changes)
    {
        var copies = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ids.Count == 0 || NotePart(endnote) is not { } part)
            return copies;
        var text = new PartText(package.ReadBytes(part));
        var root = XDocument.Parse(text.Text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo).Root!;
        var kind = root.Name.Namespace + (endnote ? "endnote" : "footnote");
        var next = root.Elements(kind).Select(n => int.TryParse((string?)n.Attribute(n.Name.Namespace + "id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var k) ? k : 0).DefaultIfEmpty(0).Max() + 1;
        var splices = new List<(int Start, int End, string Text)>();
        foreach (var note in root.Elements(kind).Where(n => (string?)n.Attribute(n.Name.Namespace + "id") is { } id && ids.Contains(id)))
        {
            var at = text.Start(note);
            var (open, end) = (text.TagEnd(at), text.End(at));
            var id = next++.ToString(CultureInfo.InvariantCulture);
            copies[(string)note.Attribute(note.Name.Namespace + "id")!] = id;
            splices.Add((end, end, FreshParaIds(TagId().Replace(text.Text[at..open], id, 1) + text.Text[open..end], part, null)));
        }
        if (splices.Count > 0)
            changes[part] = text.Spliced(splices);
        return copies;
    }

    /// <summary>Copies of comments, each right after its comment, what commentsExtended knows of them too, among the changes; the id of the copy by the id of each.</summary>
    Dictionary<string, string> CommentsCopied(IReadOnlySet<string> ids, Dictionary<string, byte[]?> changes)
    {
        var copies = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ids.Count == 0 || CommentsPart() is not { } part)
            return copies;
        var text = new PartText(package.ReadBytes(part));
        var root = XDocument.Parse(text.Text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo).Root!;
        var w = root.Name.Namespace;
        var taken = root.Elements(w + "comment").Select(c => (string?)c.Attribute(w + "id")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var next = 0;
        var splices = new List<(int Start, int End, string Text)>();
        var paraIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var comment in root.Elements(w + "comment").Where(c => (string?)c.Attribute(w + "id") is { } id && ids.Contains(id)))
        {
            while (taken.Contains(next.ToString(CultureInfo.InvariantCulture)))
                next++;
            var id = next.ToString(CultureInfo.InvariantCulture);
            taken.Add(id);
            var at = text.Start(comment);
            var (open, end) = (text.TagEnd(at), text.End(at));
            copies[(string)comment.Attribute(w + "id")!] = id;
            splices.Add((end, end, FreshParaIds(TagId().Replace(text.Text[at..open], id, 1) + text.Text[open..end], part, paraIds)));
        }
        if (splices.Count == 0)
            return copies;
        changes[part] = text.Spliced(splices);
        // Answers answer the copies, resolved stays resolved.
        if (package.FirstRelationship(context.MainPart, "commentsExtended") is { External: false })
        {
            var extended = Extended(changes);
            var entries = extended.Document.Root!;
            var made = false;
            foreach (var entry in entries.Elements(W15 + "commentEx").ToList())
            {
                if ((string?)entry.Attribute(W15 + "paraId") is not { } paraId || !paraIds.TryGetValue(paraId, out var fresh))
                    continue;
                var copy = new XElement(entry);
                copy.SetAttributeValue(W15 + "paraId", fresh);
                if ((string?)entry.Attribute(W15 + "paraIdParent") is { } parent && paraIds.TryGetValue(parent, out var freshParent))
                    copy.SetAttributeValue(W15 + "paraIdParent", freshParent);
                entries.Add(copy);
                made = true;
            }
            if (made)
                extended.Save(changes);
        }
        return copies;
    }

    /// <summary>The w14:paraId values of a stretch of a part's text made new ones, which <paramref name="given"/> keeps by the old ones.</summary>
    string FreshParaIds(string text, string part, Dictionary<string, string>? given) =>
        ParaIdValue().Replace(text, m =>
        {
            var fresh = NewParaId(part);
            if (given != null)
                given[m.Value] = fresh;
            return fresh;
        });

    /// <summary>A start tag's w:id value (the first attribute so named).</summary>
    [GeneratedRegex(@"(?<=[\s:]id=[""'])[^""']*(?=[""'])")]
    private static partial Regex TagId();

    [GeneratedRegex(@"(?<=[\s:]paraId=[""'])[0-9A-Fa-f]+(?=[""'])")]
    private static partial Regex ParaIdValue();

    /// <summary>The marks of comments in a text that is no XML to walk (a version's), by the comments' ids.</summary>
    [GeneratedRegex(@"<[^<>]*comment(?:Reference|RangeStart|RangeEnd)\b[^<>]*\bid=[""']([^""']*)[""']")]
    private static partial Regex CommentMarks();

    static IEnumerable<string> CommentsIn(string parked) => CommentMarks().Matches(parked).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal);

    static string CommentMark(string id) => $@"<[^<>]*comment(?:Reference|RangeStart|RangeEnd)\b[^<>]*\bid=[""']{Regex.Escape(id)}[""']";

    static int Occurrences(string text, string what)
    {
        var count = 0;
        for (var at = text.IndexOf(what, StringComparison.Ordinal); at >= 0; at = text.IndexOf(what, at + what.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    /// <summary>The comments the document's versions kept beside the text hold (see Comments): they do not count as the document's while so.</summary>
    HashSet<string> ParkedComments()
    {
        var parked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var paragraph in blocks.OfType<ParagraphBlock>().Where(Planned))
        {
            foreach (var text in PlanOf(paragraph).Versions.Select(v => v.Text).OfType<string>())
                parked.UnionWith(CommentsIn(text));
        }
        return parked;
    }

    /// <summary>Whether what a relationship names goes where nothing refers to it any more: what a part's text refers to by the relationship's id, not what comes with the part by its type alone.</summary>
    static bool Sweepable(Relationship relationship) =>
        relationship.Is("image") || relationship.Is("hyperlink") || relationship.Is("chart") || relationship.Is("oleObject") || relationship.Is("package") || relationship.Is("video")
        || relationship.Is("audio") || relationship.Is("media") || relationship.Is("header") || relationship.Is("footer") || relationship.Is("diagramData") || relationship.Is("diagramLayout")
        || relationship.Is("diagramQuickStyle") || relationship.Is("diagramColors") || relationship.Is("diagramDrawing") || relationship.Is("subDocument") || relationship.Is("aFChunk")
        || relationship.Is("vmlDrawing") || relationship.Is("control") || relationship.Is("ctrlProp");

    /// <summary>
    /// What nothing refers to in a main part's text, among the changes: notes, comments, and the targets of the relationships
    /// of the main part, the notes' and the comments' parts where nothing else names them.
    /// </summary>
    void Sweep(string main, Dictionary<string, byte[]?> changes)
    {
        var inMain = Referring.In(main);
        var comments = new HashSet<string>(inMain.Comments, StringComparer.Ordinal);
        foreach (var endnote in new[] { false, true })
        {
            if (NotePart(endnote) is not { } part)
                continue;
            var name = endnote ? "endnote" : "footnote";
            var text = new PartText(package.ReadBytes(part));
            var root = XDocument.Parse(text.Text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo).Root!;
            var w = root.Name.Namespace;
            var cuts = new List<(int Start, int End, string Text)>();
            foreach (var note in root.Elements(w + name))
            {
                if ((string?)note.Attribute(w + "type") is null or "normal" && (string?)note.Attribute(w + "id") is { } id && !(endnote ? inMain.Endnotes : inMain.Footnotes).Contains(id))
                {
                    var at = text.Start(note);
                    cuts.Add((at, text.End(at), ""));
                }
            }
            if (cuts.Count > 0)
                changes[part] = text.Spliced(cuts);
            comments.UnionWith(Referring.In(text.Replaced(cuts)).Comments);
        }
        if (CommentsPart() is { } commentsPart && context.Load(commentsPart).Root is { } commentsRoot)
        {
            var gone = commentsRoot.Elements(Wml.W + "comment").Select(c => c.Attr("id")).OfType<string>().Where(id => !comments.Contains(id)).ToHashSet(StringComparer.Ordinal);
            if (gone.Count > 0)
                CommentsTaken(gone, changes);
        }
        void Unreferred(string part, IReadOnlySet<string> values) =>
            Unrelated(part, [.. package.Relationships(part).Where(r => Sweepable(r) && !values.Contains(r.Id)).Select(r => r.Id)], changes);
        // A diagram's data names the drawing of the diagram by a relationship of the main part.
        foreach (var data in package.Relationships(context.MainPart).Where(r => r.Is("diagramData") && !r.External && inMain.Values.Contains(r.Id) && package.Exists(r.Target)).ToList())
            inMain.Values.UnionWith(Referring.In(new PartText(package.ReadBytes(data.Target)).Text).Values);
        Unreferred(context.MainPart, inMain.Values);
        foreach (var part in new[] { NotePart(false), NotePart(true), CommentsPart() }.OfType<string>())
        {
            if (Current(part, changes) is { } bytes)
                Unreferred(part, Referring.In(new PartText(bytes).Text).Values);
        }
    }

    /// <summary>What a part's XML refers to: notes and comments by their ids, and the values of its attributes (a relationship's id among them).</summary>
    sealed record Referring(HashSet<string> Footnotes, HashSet<string> Endnotes, HashSet<string> Comments, HashSet<string> Values)
    {
        public static Referring In(string xml)
        {
            var referring = new Referring(new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal));
            // As BodyStream reads: a DTD parsed but nothing resolved.
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, MaxCharactersFromEntities = 10_000_000, XmlResolver = null });
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || !reader.HasAttributes)
                    continue;
                var kind = reader.LocalName switch
                {
                    "footnoteReference" => referring.Footnotes,
                    "endnoteReference" => referring.Endnotes,
                    "commentReference" or "commentRangeStart" or "commentRangeEnd" => referring.Comments,
                    _ => null,
                };
                while (reader.MoveToNextAttribute())
                {
                    if (reader.Value.Length <= 128)
                        referring.Values.Add(reader.Value);
                    if (kind != null && reader.LocalName == "id")
                        kind.Add(reader.Value);
                }
            }
            return referring;
        }
    }

    /// <summary>
    /// Relationships of a part taken away, among the changes: and the parts they name where no other relationship does, with
    /// what those name so in their turn, out of the content types too.
    /// </summary>
    void Unrelated(string source, IReadOnlyCollection<string> ids, Dictionary<string, byte[]?> changes)
    {
        if (ids.Count == 0)
            return;
        var gone = new HashSet<(string Source, string Id)>();
        var owners = package.PartNames.Where(n => n.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)).Select(Owner).ToList();
        void Take(string from, IEnumerable<string> names)
        {
            var targets = new List<string>();
            foreach (var id in names)
            {
                gone.Add((from, id));
                if (package.Relationship(from, id) is { External: false } related)
                    targets.Add(related.Target);
            }
            foreach (var target in targets.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (owners.Any(owner => package.Relationships(owner).Any(r => !r.External && string.Equals(r.Target, target, StringComparison.OrdinalIgnoreCase) && !gone.Contains((owner, r.Id)))))
                    continue;
                if (package.Exists(target))
                    changes[target] = null;
                Take(target, package.Relationships(target).Select(r => r.Id).ToList());
                if (package.Exists(OpcPackage.RelationshipsPart(target)))
                    changes[OpcPackage.RelationshipsPart(target)] = null;
            }
        }
        Take(source, ids);
        var relsPart = OpcPackage.RelationshipsPart(source);
        var rels = Xml(Current(relsPart, changes) ?? throw new InvalidOperationException("The part has no relationships."));
        foreach (var relationship in rels.Root!.Elements(Relationships + "Relationship").Where(r => (string?)r.Attribute("Id") is { } id && ids.Contains(id)).ToList())
            relationship.Remove();
        changes[relsPart] = Bytes(rels);
        var types = Xml(Current(OpcPackage.ContentTypesPart, changes) ?? throw new NotSupportedException("The package names no content types."));
        var listed = false;
        foreach (var entry in types.Root!.Elements(ContentTypes + "Override").Where(o => (string?)o.Attribute("PartName") is { } name && changes.TryGetValue(name, out var bytes) && bytes == null).ToList())
        {
            entry.Remove();
            listed = true;
        }
        if (listed)
            changes[OpcPackage.ContentTypesPart] = Bytes(types);

        static string Owner(string rels)
        {
            var at = rels.LastIndexOf("/_rels/", StringComparison.OrdinalIgnoreCase);
            var name = rels[(at + 7)..^5];
            return at == 0 && name.Length == 0 ? "/" : rels[..(at + 1)] + name;
        }
    }
}
