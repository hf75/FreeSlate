using System.Globalization;
using System.Security;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using FreeSlate.Opc;

namespace FreeSlate.Docx;

/// <summary>
/// What a section of a Word document holds besides its text: other versions of its text and notes for planning it. They
/// stand in the paragraph that begins the section, first after its properties, in an alternative of markup compatibility
/// (ISO/IEC 29500-3) that only this engine can choose, with nothing as the fallback:
/// <code>
/// &lt;mc:AlternateContent xmlns:mc="…" xmlns:fs="urn:freeslate:plan"&gt;&lt;mc:Choice Requires="fs"&gt;&lt;fs:plan&gt;
///   &lt;fs:version id="v1" name="First draft" date="…"&gt;&lt;![CDATA[the version's blocks]]&gt;&lt;/fs:version&gt;
///   &lt;fs:version id="v2" name="Second draft" date="…" current="1"/&gt;
///   &lt;fs:note id="n1" author="…" date="…"&gt;A thought on the section&lt;/fs:note&gt;
/// &lt;/fs:plan&gt;&lt;/mc:Choice&gt;&lt;mc:Fallback/&gt;&lt;/mc:AlternateContent&gt;
/// </code>
/// Word opens such a document as if the alternative were not there and writes nothing of it back when it saves, what only
/// the versions refer to (a note, a comment, a picture, a link) going with them; so does the engine's reader (see
/// MarkupCompatibility), which only marks the paragraph (measured in Word 365 by COM, also with mc:Ignorable and with parts
/// no relationship names; an undeclared namespace makes Word refuse the file, mc:PreserveElements it does not heed; in
/// LibreOffice's Word import a mc:Choice it does not know is dropped while it reads the content of mc:Ignorable's elements,
/// read in its sources). A version's text so never reaches what renumbers ids, and shares those of the document: it is the
/// text of the blocks as it stood, moved, every character as it was, the document's styles, lists, relationships, notes and
/// comments still its own. As CDATA, text and no elements to anything that walks the XML (see Stretch.Xml), but to what
/// reads the part's text.
/// </summary>
sealed class SectionPlan
{
    const string Opening = "<mc:AlternateContent xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" xmlns:fs=\"urn:freeslate:plan\"><mc:Choice Requires=\"fs\"><fs:plan>";
    const string Closing = "</fs:plan></mc:Choice><mc:Fallback/></mc:AlternateContent>";

    /// <summary>
    /// Where the next such alternative begins in a part's text from a place on, up to another or to its end, or -1: found by
    /// its namespace in the start tag of an alternative, which the text of a run cannot hold; one within a version kept in
    /// another is passed where the search goes on from the other's end (see PartText.End).
    /// </summary>
    public static int Next(string text, int from, int until = int.MaxValue)
    {
        const string name = "AlternateContent ";
        until = Math.Min(until, text.Length);
        for (var at = text.IndexOf(MarkupCompatibility.PlanUri, from, until - from, StringComparison.Ordinal); at >= 0; at = text.IndexOf(MarkupCompatibility.PlanUri, at + 1, until - at - 1, StringComparison.Ordinal))
        {
            var start = text.LastIndexOf('<', at);
            if (start >= from && text.IndexOf('>', start, at - start) < 0 && text.AsSpan(start, at - start).IndexOf(name, StringComparison.Ordinal) > 0)
                return start;
        }
        return -1;
    }

    /// <summary>A version: the text of its blocks, null for the one in place (its blocks stand in the body).</summary>
    public sealed record Version(string Id, string Name, DateTime? Date, string? Text);

    public sealed record Note(string Id, string Author, DateTime? Date, string Text);

    public List<Version> Versions { get; } = [];
    public List<Note> Notes { get; } = [];

    /// <summary>Whether an element is such an alternative.</summary>
    public static bool Is(XElement element) =>
        element.Name == MarkupCompatibility.Mc + "AlternateContent" && element.Elements(MarkupCompatibility.Mc + "Choice").Elements(MarkupCompatibility.Plan + "plan").Any();

    /// <summary>The alternatives that stand in a paragraph's parts (one, but where paragraphs that held one were joined).</summary>
    public static List<XElement> In(IEnumerable<XElement> parts) =>
        [.. parts.SelectMany(p => p.Elements(MarkupCompatibility.Mc + "AlternateContent")).Where(Is)];

    /// <summary>The plan the alternatives of a paragraph hold, those read with line info from <paramref name="text"/>; of several (see In) all their versions and notes, the first's in place.</summary>
    public static SectionPlan Read(PartText text, IReadOnlyList<XElement> alternatives)
    {
        var plan = new SectionPlan();
        foreach (var e in alternatives.SelectMany(a => a.Elements(MarkupCompatibility.Mc + "Choice").Elements(MarkupCompatibility.Plan + "plan").Elements()))
        {
            var date = (string?)e.Attribute("date") is { } written && DateTime.TryParse(written, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var at)
                ? at : (DateTime?)null;
            if (e.Name == MarkupCompatibility.Plan + "version")
            {
                var current = (string?)e.Attribute("current") == "1";
                if (current && plan.Versions.Any(v => v.Text == null))
                    continue;
                plan.Versions.Add(new Version(plan.Free((string?)e.Attribute("id"), "v", plan.Versions.Select(v => v.Id)), (string?)e.Attribute("name") ?? "", date, current ? null : Parked(text, e)));
            }
            else if (e.Name == MarkupCompatibility.Plan + "note")
                plan.Notes.Add(new Note(plan.Free((string?)e.Attribute("id"), "n", plan.Notes.Select(n => n.Id)), (string?)e.Attribute("author") ?? "", date, e.Value));
        }
        return plan;
    }

    /// <summary>An id as written where no other has it, else the next free one.</summary>
    string Free(string? wished, string prefix, IEnumerable<string> taken)
    {
        var used = taken.ToHashSet(StringComparer.Ordinal);
        if (wished != null && !used.Contains(wished))
            return wished;
        return NextId(prefix, used);
    }

    static string NextId(string prefix, IReadOnlySet<string> used)
    {
        for (var k = 1; ; k++)
        {
            var id = prefix + k.ToString(CultureInfo.InvariantCulture);
            if (!used.Contains(id))
                return id;
        }
    }

    /// <summary>The id the next version takes.</summary>
    public string NewVersionId() => NextId("v", Versions.Select(v => v.Id).ToHashSet(StringComparer.Ordinal));

    /// <summary>The id the next note takes.</summary>
    public string NewNoteId() => NextId("n", Notes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal));

    /// <summary>The text a version element parks, every character as it stands in the part's text.</summary>
    static string Parked(PartText text, XElement version)
    {
        var start = text.Start(version);
        var (open, end) = (text.TagEnd(start), text.End(start));
        if (open == end)
            return "";
        var inner = text.Text[open..text.Text.LastIndexOf("</", end - 1, StringComparison.Ordinal)];
        return inner.StartsWith("<![CDATA[", StringComparison.Ordinal) ? inner[9..^3].Replace("]]]]><![CDATA[>", "]]>", StringComparison.Ordinal) : inner;
    }

    /// <summary>The version in place; one of that name, at that time, where the plan names none yet.</summary>
    public Version InPlace(string name, DateTime? date)
    {
        if (Versions.FirstOrDefault(v => v.Text == null) is { } current)
            return current;
        var made = new Version(NewVersionId(), name, date, null);
        Versions.Add(made);
        return made;
    }

    /// <summary>A version given its name, its date or its text, in its place in the list.</summary>
    public void Replace(Version version, Version with) => Versions[Versions.IndexOf(version)] = with;

    /// <summary>
    /// The alternative as written, with what the plan holds; nothing where it holds nothing but the version in place, which
    /// then is simply the section's text.
    /// </summary>
    public string Written()
    {
        if (Notes.Count == 0 && Versions.All(v => v.Text == null))
            return "";
        var s = new StringBuilder(Opening);
        foreach (var version in Versions)
        {
            s.Append("<fs:version id=\"").Append(Attribute(version.Id)).Append("\" name=\"").Append(Attribute(version.Name)).Append('"').Append(Dated(version.Date));
            if (version.Text == null)
                s.Append(" current=\"1\"/>");
            else
                s.Append("><![CDATA[").Append(version.Text.Replace("]]>", "]]]]><![CDATA[>", StringComparison.Ordinal)).Append("]]></fs:version>");
        }
        foreach (var note in Notes)
        {
            s.Append("<fs:note id=\"").Append(Attribute(note.Id)).Append("\" author=\"").Append(Attribute(note.Author)).Append('"').Append(Dated(note.Date)).Append('>')
                .Append(Attribute(note.Text)).Append("</fs:note>");
        }
        return s.Append(Closing).ToString();
    }

    static string Dated(DateTime? date) => date is { } at ? $" date=\"{at.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}\"" : "";

    /// <summary>A text for an attribute's value or an element's content: what XML cannot hold left out, escaped.</summary>
    static string Attribute(string text)
    {
        var s = new StringBuilder(text.Length);
        for (var k = 0; k < text.Length; k++)
        {
            if (k + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[k + 1], text[k]))
                s.Append(text[k]).Append(text[++k]);
            else if (XmlConvert.IsXmlChar(text[k]))
                s.Append(text[k]);
        }
        return SecurityElement.Escape(s.ToString().ReplaceLineEndings("\n"));
    }

    /// <summary>
    /// The changes of a stretch's text (see Stretch.Spliced) that give a paragraph's parts the alternative
    /// <paramref name="written"/>: in place of the first it holds, the others gone; else first after its properties.
    /// </summary>
    public static List<(int Start, int End, string Text)> Splices(PartText text, List<XElement> parts, string written)
    {
        var splices = new List<(int Start, int End, string Text)>();
        var alternatives = In(parts);
        for (var k = 0; k < alternatives.Count; k++)
        {
            var at = text.Start(alternatives[k]);
            splices.Add((at, text.End(at), k == 0 ? written : ""));
        }
        if (alternatives.Count > 0 || written.Length == 0)
            return splices;
        var p = parts[0];
        var start = text.Start(p);
        var open = text.TagEnd(start);
        // A paragraph written empty gets an end tag.
        if (text.Text[open - 2] == '/')
        {
            var name = text.Text[(start + 1)..text.Text.IndexOfAny([' ', '\t', '\r', '\n', '/', '>'], start + 1)];
            splices.Add((start, open, $"{text.Text[start..(open - 2)].TrimEnd()}>{written}</{name}>"));
        }
        else
        {
            var place = p.Element(p.Name.Namespace + "pPr") is { } properties ? text.End(text.Start(properties)) : open;
            splices.Add((place, place, written));
        }
        return splices;
    }
}
