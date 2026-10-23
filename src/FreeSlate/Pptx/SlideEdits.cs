using System.Xml.Linq;
using FreeSlate.Opc;

namespace FreeSlate.Pptx;

/// <summary>
/// Changes to slide paragraphs (a:p) made in the XML they were read from (catalog: package.opc), as PowerPoint makes
/// them (measured through its object model, PowerPoint 365): places are offsets in a paragraph's text (SlideText). New text
/// goes into the run of the character before it (into the first run at the paragraph's start), or in place of the first
/// character it replaces; after a field or a line break it is a run of its own in their format, in an empty paragraph a
/// run in the format of its mark (a:endParaRPr). A paragraph that loses all its text keeps the format of its last run in
/// its mark. Splitting copies the paragraph's properties (a:pPr) to the second half, which takes the mark; the first half
/// gets a mark of its own where it does not end with a run (empty, the format of the character after; after a field or
/// a line break, theirs). Joining keeps the first paragraph's properties and takes the second's mark; an empty first
/// paragraph goes away. Text in an alternative of markup compatibility (an equation) cannot be changed so: that throws
/// NotSupportedException, and nothing is changed.
/// </summary>
static class SlideEdits
{
    /// <summary>Run properties that say how PowerPoint last checked or showed a run, not its format, which copies leave out.</summary>
    static readonly HashSet<string> Transient = ["dirty", "err", "smtClean", "smtId"];

    /// <summary>The children of a paragraph that take places in its text, each with where it begins in it.</summary>
    static List<(XElement Element, int Start)> Pieces(XElement paragraph)
    {
        var pieces = new List<(XElement, int)>();
        var at = 0;
        foreach (var element in paragraph.Elements())
        {
            if (element.Name == Ns.A + "r" || SlideText.Length(element) > 0)
            {
                pieces.Add((element, at));
                at += SlideText.Length(element);
            }
        }
        return pieces;
    }

    /// <summary>Checks that a paragraph can be changed in its XML: not in an alternative of markup compatibility, none within it.</summary>
    public static void Check(XElement paragraph)
    {
        if (paragraph.Ancestors().Concat(paragraph.Descendants()).Any(e => e.Name.Namespace == MarkupCompatibility.Mc))
            throw new NotSupportedException("The paragraph stands in an alternative of markup compatibility, or holds one.");
    }

    /// <summary>
    /// The text of a paragraph from <paramref name="offset"/>, so many characters, replaced by <paramref name="text"/> (a
    /// line break as "\v"); see the class.
    /// </summary>
    public static void Replace(XElement paragraph, int offset, int length, string text)
    {
        Check(paragraph);
        var all = SlideText.Of(paragraph);
        if (offset < 0 || length < 0 || offset + length > all.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), "The places lie outside the paragraph's text.");
        if (text.IndexOfAny(['\r', '\n', '￼']) >= 0)
            throw new ArgumentException("New text holds no paragraph marks (see Split) and no objects.", nameof(text));
        Between(all, offset);
        Between(all, offset + length);

        // What is taken away: text cut from its runs (a run left empty goes), other pieces whole.
        var pieces = Pieces(paragraph);
        XElement? first = null;          // the piece of the first character replaced
        XElement? lastText = null;       // the last run, field or break taken away whole, for the paragraph's mark
        foreach (var (element, start) in pieces)
        {
            var size = SlideText.Length(element);
            var (from, to) = (Math.Max(offset, start), Math.Min(offset + length, start + size));
            if (from >= to)
                continue;
            first ??= element;
            if (element.Name == Ns.A + "r")
            {
                var value = Text(element);
                SetText(element, value[..(from - start)] + value[(to - start)..]);
                if (Text(element).Length == 0 && element != first)
                {
                    lastText = element;
                    element.Remove();
                }
            }
            else
            {
                lastText = element;
                if (element != first)
                    element.Remove();
            }
        }

        if (text.Length > 0)
            Insert(paragraph, offset, text, first);
        // The first piece replaced stays where new text went into it, as long as it was a run with text left.
        if (first?.Parent != null && (first.Name != Ns.A + "r" || Text(first).Length == 0))
        {
            lastText ??= first;
            first.Remove();
        }
        // A paragraph left without text keeps the format of what it lost last in its mark.
        if (lastText != null && Pieces(paragraph).Count == 0 && paragraph.Element(Ns.A + "endParaRPr") == null
            && Properties(lastText.Element(Ns.A + "rPr"), Ns.A + "endParaRPr") is { } mark)
            paragraph.Add(mark);
    }

    /// <summary>
    /// Puts text in at a place: into the run of <paramref name="replaced"/> (the piece of the first character replaced,
    /// still in the paragraph), else into the run of the character before, else as a run of its own in the format of the
    /// field or break before it, or at the paragraph's start of its first piece, or of the paragraph's mark.
    /// </summary>
    static void Insert(XElement paragraph, int offset, string text, XElement? replaced)
    {
        XElement run;
        int at;
        var pieces = Pieces(paragraph);
        var before = pieces.LastOrDefault(p => p.Start < offset);
        if (replaced is { Parent: not null } && replaced.Name == Ns.A + "r")
            (run, at) = (replaced, offset - pieces.First(p => p.Element == replaced).Start);
        else if (replaced is { Parent: not null })
        {
            // A field or break typed over: its format, in its place.
            run = NewRun(replaced.Element(Ns.A + "rPr"));
            replaced.AddBeforeSelf(run);
            at = 0;
        }
        else if (before.Element is { } previous && previous.Name == Ns.A + "r")
            (run, at) = (previous, offset - before.Start);
        else if (before.Element is { } other)
        {
            run = NewRun(other.Element(Ns.A + "rPr"));
            other.AddAfterSelf(run);
            at = 0;
        }
        else if (pieces.Count > 0 && pieces[0].Element.Name == Ns.A + "r")
            (run, at) = (pieces[0].Element, 0);
        else if (pieces.Count > 0)
        {
            run = NewRun(pieces[0].Element.Element(Ns.A + "rPr"));
            pieces[0].Element.AddBeforeSelf(run);
            at = 0;
        }
        else
        {
            run = NewRun(paragraph.Element(Ns.A + "endParaRPr"));
            if (paragraph.Element(Ns.A + "pPr") is { } pPr)
                pPr.AddAfterSelf(run);
            else
                paragraph.AddFirst(run);
            at = 0;
        }

        // Line breaks part the text into runs of the run's format with breaks between them.
        var value = Text(run);
        var (head, tail) = (value[..at], value[at..]);
        var lines = text.Split('\v');
        SetText(run, head + lines[0] + (lines.Length == 1 ? tail : ""));
        var last = run;
        for (var i = 1; i < lines.Length; i++)
        {
            var lineBreak = new XElement(Ns.A + "br", Properties(run.Element(Ns.A + "rPr"), Ns.A + "rPr"));
            var next = NewRun(run.Element(Ns.A + "rPr"));
            SetText(next, lines[i] + (i == lines.Length - 1 ? tail : ""));
            last.AddAfterSelf(lineBreak, next);
            last = next;
        }
        foreach (var made in new[] { run, last }.Distinct())
        {
            if (Text(made).Length == 0 && made != replaced)
                made.Remove();
        }
    }

    /// <summary>
    /// Splits a paragraph in two at a place, as Enter does; returns the second half, which follows it (see the class).
    /// </summary>
    public static XElement Split(XElement paragraph, int offset)
    {
        Check(paragraph);
        var all = SlideText.Of(paragraph);
        if (offset < 0 || offset > all.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), "The place lies outside the paragraph's text.");
        Between(all, offset);

        var second = new XElement(paragraph.Name);
        if (paragraph.Element(Ns.A + "pPr") is { } pPr)
            second.Add(new XElement(pPr));
        var pieces = Pieces(paragraph);
        // A run the place falls into is cut in two.
        if (pieces.FirstOrDefault(p => p.Start < offset && offset < p.Start + SlideText.Length(p.Element)) is { Element: { } cut } split)
        {
            var value = Text(cut);
            SetText(cut, value[..(offset - split.Start)]);
            var rest = NewRun(cut.Element(Ns.A + "rPr"), copy: true);
            SetText(rest, value[(offset - split.Start)..]);
            cut.AddAfterSelf(rest);
            pieces = Pieces(paragraph);
        }
        var moved = pieces.Where(p => p.Start > offset || p.Start == offset && SlideText.Length(p.Element) > 0).Select(p => p.Element).ToList();
        var kept = pieces.Select(p => p.Element).Except(moved).ToList();
        foreach (var element in moved)
        {
            element.Remove();
            second.Add(element);
        }
        var mark = paragraph.Element(Ns.A + "endParaRPr");
        mark?.Remove();
        var lastKept = kept.LastOrDefault(e => SlideText.Length(e) > 0);
        if (mark != null)
            second.Add(mark);
        else if (moved.Count == 0 && Properties(lastKept?.Element(Ns.A + "rPr"), Ns.A + "endParaRPr") is { } inherited)
            second.Add(inherited);
        // The first half's own mark: an empty one takes the format of the character after it, else of the field or
        // break it ends with.
        var firstMoved = moved.FirstOrDefault(e => SlideText.Length(e) > 0);
        if (lastKept == null)
        {
            if ((firstMoved != null ? Properties(firstMoved.Element(Ns.A + "rPr"), Ns.A + "endParaRPr") : mark != null ? new XElement(mark) : null) is { } own)
                paragraph.Add(own);
        }
        else if (lastKept.Name != Ns.A + "r" && Properties(lastKept.Element(Ns.A + "rPr"), Ns.A + "endParaRPr") is { } own)
            paragraph.Add(own);
        paragraph.AddAfterSelf(second);
        return second;
    }

    /// <summary>
    /// Joins a paragraph with the one after it, as Delete at its end does; returns the paragraph that stays: the first,
    /// with the second's content and mark, or the second where the first is empty (see the class).
    /// </summary>
    public static XElement Join(XElement paragraph, XElement next)
    {
        Check(paragraph);
        Check(next);
        if (SlideText.Of(paragraph).Length == 0)
        {
            paragraph.Remove();
            return next;
        }
        var content = next.Elements().Where(e => e.Name != Ns.A + "pPr" && e.Name != Ns.A + "endParaRPr").ToList();
        var mark = next.Element(Ns.A + "endParaRPr");
        paragraph.Element(Ns.A + "endParaRPr")?.Remove();
        foreach (var element in content)
        {
            element.Remove();
            paragraph.Add(element);
        }
        if (mark != null)
        {
            mark.Remove();
            paragraph.Add(mark);
        }
        next.Remove();
        return paragraph;
    }

    /// <summary>Checks that a place does not fall between the halves of a character (a surrogate pair).</summary>
    static void Between(string text, int offset)
    {
        if (offset > 0 && offset < text.Length && char.IsHighSurrogate(text[offset - 1]) && char.IsLowSurrogate(text[offset]))
            throw new ArgumentOutOfRangeException(nameof(offset), "The place is within a character (between the halves of a surrogate pair).");
    }

    static string Text(XElement run) => run.Element(Ns.A + "t")?.Value ?? "";

    static void SetText(XElement run, string value)
    {
        if (run.Element(Ns.A + "t") is { } t)
            t.Value = value;
        else
            run.Add(new XElement(Ns.A + "t", value));
    }

    /// <summary>A run in a format: the properties of a run, field, break or mark as its a:rPr.</summary>
    static XElement NewRun(XElement? properties, bool copy = false) =>
        new(Ns.A + "r", Properties(properties, Ns.A + "rPr", copy), new XElement(Ns.A + "t"));

    /// <summary>
    /// Properties copied under another name (a:rPr, a:endParaRPr), without what says how PowerPoint last checked or
    /// showed them (unless a whole copy is asked for: a run cut in two); null for none.
    /// </summary>
    static XElement? Properties(XElement? properties, XName name, bool copy = false) =>
        properties == null ? null
            : new XElement(name, properties.Attributes().Where(a => copy || !Transient.Contains(a.Name.LocalName)), properties.Nodes().Select(n => n switch
            {
                XElement e => (XNode)new XElement(e),
                XText t => new XText(t),
                _ => n,
            }));
}
