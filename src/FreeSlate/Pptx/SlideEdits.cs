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
    /// line break as "\v", a page or column break, which slides lack, as a line break too); see the class.
    /// </summary>
    public static void Replace(XElement paragraph, int offset, int length, string text)
    {
        Check(paragraph);
        text = text.Replace('\f', '\v').Replace('\u000E', '\v');
        if (text.Any(c => char.IsControl(c) && c is not ('\t' or '\v' or '\r' or '\n')))
            throw new ArgumentException("New text holds control characters other than tabs and line breaks.", nameof(text));
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

    /// <summary>
    /// Gives text character formatting as PowerPoint's buttons do: the runs cut where the stretch begins and ends, its
    /// runs, fields and line breaks formatted (a:rPr), and the paragraph's mark where the stretch reaches the paragraph's
    /// end (an empty paragraph's mark made where it has none); runs cut in vain joined again.
    /// </summary>
    public static void Format(XElement paragraph, int offset, int length, TextFormatting format)
    {
        Check(paragraph);
        var all = SlideText.Of(paragraph);
        if (offset < 0 || length < 0 || offset + length > all.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), "The places lie outside the paragraph's text.");
        Between(all, offset);
        Between(all, offset + length);
        var cuts = new[] { Cut(paragraph, offset), Cut(paragraph, offset + length) };
        foreach (var (element, start) in Pieces(paragraph))
        {
            var size = SlideText.Length(element);
            if (size > 0 && start >= offset && start + size <= offset + length && element.Name.Namespace == Ns.A)
                Apply(Properties(element), format, element.Name == Ns.A + "r" ? Text(element) : "");
        }
        if (offset + length == all.Length)
        {
            var mark = paragraph.Element(Ns.A + "endParaRPr");
            if (mark == null && all.Length == 0)
                paragraph.Add(mark = new XElement(Ns.A + "endParaRPr"));
            if (mark != null)
                Apply(mark, format, "");
        }
        foreach (var cut in cuts)
        {
            if (cut is { Parent: not null } && cut.ElementsAfterSelf().FirstOrDefault() is { } next && next.Name == Ns.A + "r"
                && XNode.DeepEquals(cut.Element(Ns.A + "rPr"), next.Element(Ns.A + "rPr")))
            {
                SetText(cut, Text(cut) + Text(next));
                next.Remove();
            }
        }
    }

    /// <summary>
    /// Gives a paragraph paragraph formatting (a:pPr): its alignment, indents (marL, marR, indent in EMU), space before
    /// and after (in points) and line spacing (in percent), each in the place the schema gives it.
    /// </summary>
    public static void Format(XElement paragraph, ParagraphFormatting format)
    {
        Check(paragraph);
        if (format.Style != null)
            throw new NotSupportedException("Slide paragraphs have no styles.");
        var pPr = paragraph.Element(Ns.A + "pPr");
        if (pPr == null)
            paragraph.AddFirst(pPr = new XElement(Ns.A + "pPr"));
        if (format.Alignment is { } alignment)
            pPr.SetAttributeValue("algn", alignment switch
            {
                "left" or "start" or "l" => "l",
                "center" or "ctr" => "ctr",
                "right" or "end" or "r" => "r",
                "both" or "just" => "just",
                "distribute" or "dist" => "dist",
                "justLow" or "thaiDist" => alignment,
                _ => throw new ArgumentException($"No alignment is called {alignment}.", nameof(format)),
            });
        if (format.Left is { } left)
            pPr.SetAttributeValue("marL", Emu(left, 0, nameof(format.Left)));
        if (format.Right is { } right)
            pPr.SetAttributeValue("marR", Emu(right, 0, nameof(format.Right)));
        if (format.FirstLine is { } firstLine)
            pPr.SetAttributeValue("indent", Emu(firstLine, -4032, nameof(format.FirstLine)));
        if (format.Lines is { } lines)
            Place(pPr, new XElement(Ns.A + "lnSpc", new XElement(Ns.A + "spcPct", new XAttribute("val", Whole(lines * 100000, 0, 13200000, nameof(format.Lines))))), ParagraphOrder);
        if (format.Before is { } before)
            Place(pPr, new XElement(Ns.A + "spcBef", new XElement(Ns.A + "spcPts", new XAttribute("val", Whole(before * 100, 0, 158400, nameof(format.Before))))), ParagraphOrder);
        if (format.After is { } after)
            Place(pPr, new XElement(Ns.A + "spcAft", new XElement(Ns.A + "spcPts", new XAttribute("val", Whole(after * 100, 0, 158400, nameof(format.After))))), ParagraphOrder);
    }

    /// <summary>The order of a:rPr's children (CT_TextCharacterProperties).</summary>
    static readonly string[] RunOrder =
    [
        "ln", "noFill", "solidFill", "gradFill", "blipFill", "pattFill", "grpFill", "effectLst", "effectDag", "highlight", "uLnTx", "uLn",
        "uFillTx", "uFill", "latin", "ea", "cs", "sym", "hlinkClick", "hlinkMouseOver", "rtl", "extLst",
    ];

    /// <summary>The order of a:pPr's children (CT_TextParagraphProperties).</summary>
    static readonly string[] ParagraphOrder =
    [
        "lnSpc", "spcBef", "spcAft", "buClrTx", "buClr", "buSzTx", "buSzPct", "buSzPts", "buFontTx", "buFont", "buNone", "buAutoNum",
        "buChar", "buBlip", "tabLst", "defRPr", "extLst",
    ];

    static readonly string[] Fills = ["noFill", "solidFill", "gradFill", "blipFill", "pattFill", "grpFill"];

    /// <summary>Word's highlight colors by name (w:highlight), as RRGGBB.</summary>
    static readonly Dictionary<string, string> Highlights = new(StringComparer.Ordinal)
    {
        ["yellow"] = "FFFF00", ["green"] = "00FF00", ["cyan"] = "00FFFF", ["magenta"] = "FF00FF", ["blue"] = "0000FF", ["red"] = "FF0000",
        ["darkBlue"] = "000080", ["darkCyan"] = "008080", ["darkGreen"] = "008000", ["darkMagenta"] = "800080", ["darkRed"] = "800000",
        ["darkYellow"] = "808000", ["darkGray"] = "808080", ["lightGray"] = "C0C0C0", ["black"] = "000000", ["white"] = "FFFFFF",
    };

    /// <summary>Word's underlines by name (w:u) as DrawingML's (a:rPr u).</summary>
    static readonly Dictionary<string, string> Underlines = new(StringComparer.Ordinal)
    {
        ["none"] = "none", ["single"] = "sng", ["double"] = "dbl", ["thick"] = "heavy", ["dotted"] = "dotted", ["dottedHeavy"] = "dottedHeavy",
        ["dash"] = "dash", ["dashedHeavy"] = "dashHeavy", ["dashLong"] = "dashLong", ["dashLongHeavy"] = "dashLongHeavy", ["dotDash"] = "dotDash",
        ["dashDotHeavy"] = "dotDashHeavy", ["dotDotDash"] = "dotDotDash", ["dashDotDotHeavy"] = "dotDotDashHeavy", ["wave"] = "wavy",
        ["wavyHeavy"] = "wavyHeavy", ["wavyDouble"] = "wavyDbl", ["words"] = "words",
    };

    static readonly HashSet<string> DrawingUnderlines =
        ["none", "words", "sng", "dbl", "heavy", "dotted", "dottedHeavy", "dash", "dashHeavy", "dashLong", "dashLongHeavy", "dotDash",
         "dotDashHeavy", "dotDotDash", "dotDotDashHeavy", "wavy", "wavyHeavy", "wavyDbl"];

    /// <summary>Character formatting set in a run's, field's, break's or mark's properties (see TextFormatting).</summary>
    static void Apply(XElement properties, TextFormatting format, string text)
    {
        if (format.Bold is { } bold)
            properties.SetAttributeValue("b", bold ? "1" : "0");
        if (format.Italic is { } italic)
            properties.SetAttributeValue("i", italic ? "1" : "0");
        if (format.Underline is { } underline)
            properties.SetAttributeValue("u", Underlines.GetValueOrDefault(underline)
                ?? (DrawingUnderlines.Contains(underline) ? underline : throw new ArgumentException($"No underline is called {underline}.", nameof(format))));
        if (format.Size is { } size)
            properties.SetAttributeValue("sz", Whole(size * 100, 100, 400000, nameof(format.Size)));
        if (format.Font is { } font)
        {
            var slots = FreeSlate.Text.Scripts.Split(text).Select(p => p.Slot).ToHashSet();
            Place(properties, new XElement(Ns.A + "latin", new XAttribute("typeface", font)), RunOrder);
            if (slots.Contains(FreeSlate.Text.FontSlot.EastAsian))
                Place(properties, new XElement(Ns.A + "ea", new XAttribute("typeface", font)), RunOrder);
            if (slots.Contains(FreeSlate.Text.FontSlot.Complex))
                Place(properties, new XElement(Ns.A + "cs", new XAttribute("typeface", font)), RunOrder);
        }
        if (format.Color is { } color)
        {
            properties.Elements().Where(e => Fills.Contains(e.Name.LocalName)).Remove();
            if (color != "auto")
                Place(properties, new XElement(Ns.A + "solidFill", new XElement(Ns.A + "srgbClr", new XAttribute("val", Rgb(color)))), RunOrder);
        }
        if (format.Highlight is { } highlight)
        {
            properties.Element(Ns.A + "highlight")?.Remove();
            if (highlight != "none")
                Place(properties, new XElement(Ns.A + "highlight", new XElement(Ns.A + "srgbClr", new XAttribute("val", Highlights.GetValueOrDefault(highlight) ?? Rgb(highlight)))), RunOrder);
        }
    }

    /// <summary>A color as RRGGBB, upper case.</summary>
    static string Rgb(string color) =>
        color.Length == 6 && color.All(char.IsAsciiHexDigit) ? color.ToUpperInvariant() : throw new ArgumentException($"{color} is no color (RRGGBB).", nameof(color));

    /// <summary>A length in points as EMU, at least <paramref name="least"/> points and at most 4 032 (the schema's 51 206 400 EMU).</summary>
    static string Emu(double points, double least, string name) => Whole(points * 12700, least * 12700, 51206400, name);

    static string Whole(double value, double least, double most, string name) =>
        value >= least - 1e-9 && value <= most + 1e-9
            ? ((long)Math.Round(value)).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : throw new ArgumentOutOfRangeException(name, "The value lies outside what the schema allows.");

    /// <summary>A child put in its place among an element's children by the schema's order, in place of one of its name.</summary>
    static void Place(XElement parent, XElement child, string[] order)
    {
        var rank = Array.IndexOf(order, child.Name.LocalName);
        if (parent.Elements(child.Name).FirstOrDefault() is { } old)
        {
            old.ReplaceWith(child);
            return;
        }
        if (parent.Elements().FirstOrDefault(e => e.Name.Namespace == Ns.A && Array.IndexOf(order, e.Name.LocalName) > rank) is { } after)
            after.AddBeforeSelf(child);
        else
            parent.Add(child);
    }

    /// <summary>The properties of a run, field or break (a:rPr), made where it has none.</summary>
    static XElement Properties(XElement element)
    {
        if (element.Element(Ns.A + "rPr") is { } properties)
            return properties;
        properties = new XElement(Ns.A + "rPr");
        element.AddFirst(properties);
        return properties;
    }

    /// <summary>
    /// Cuts the run a place falls inside in two, both in its format; returns the run that ends at the place (the first
    /// half, or the run there already), null where none does.
    /// </summary>
    static XElement? Cut(XElement paragraph, int offset)
    {
        foreach (var (element, start) in Pieces(paragraph))
        {
            var size = SlideText.Length(element);
            if (element.Name != Ns.A + "r" || size == 0)
                continue;
            if (start + size == offset)
                return element;
            if (start < offset && offset < start + size)
            {
                var value = Text(element);
                SetText(element, value[..(offset - start)]);
                var rest = NewRun(element.Element(Ns.A + "rPr"), copy: true);
                SetText(rest, value[(offset - start)..]);
                element.AddAfterSelf(rest);
                return element;
            }
        }
        return null;
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
