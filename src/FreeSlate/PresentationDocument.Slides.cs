using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Opc;
using FreeSlate.Pptx;

namespace FreeSlate;

/// <summary>
/// The slides of a presentation document added, duplicated, taken away and moved, as PowerPoint does it (measured through
/// its object model, PowerPoint 365; see Pptx.SlideList): in the presentation part's list of slides (p:sldIdLst), its
/// sections and custom shows, the relationships to the slides and the package's content types. A slide's id
/// (p:sldId/@id) stays with it; a new slide takes one more than the greatest. Each is a version as text changes are.
/// </summary>
public sealed partial class PresentationDocument
{
    /// <summary>
    /// The relationships a slide's copy makes copies of the parts of (its notes, charts with their workbooks and styles,
    /// diagrams, embedded objects; theirs too), not sharing them with the slide; the others (layout, pictures, media,
    /// links) it shares, but for comments, which it leaves.
    /// </summary>
    static readonly HashSet<string> Owned =
    [
        "notesSlide", "chart", "chartEx", "chartUserShapes", "diagramData", "diagramLayout", "diagramQuickStyle", "diagramColors", "diagramDrawing",
        "package", "oleObject", "chartStyle", "chartColorStyle",
    ];

    /// <summary>The layouts slides can be made from (see AddSlide), master by master: each one's part and name.</summary>
    public IReadOnlyList<(string Part, string Name)> Layouts =>
        [.. (context.Presentation.Root?.Element(Ns.P + "sldMasterIdLst")?.Elements(Ns.P + "sldMasterId") ?? [])
            .Select(m => m.Attribute(Ns.R + "id")?.Value is { } rid ? package.Relationship(context.PresentationPart, rid) : null)
            .Where(r => r is { External: false } && package.Exists(r.Target))
            .SelectMany(master => (context.Load(master!.Target).Root?.Element(Ns.P + "sldLayoutIdLst")?.Elements(Ns.P + "sldLayoutId") ?? [])
                .Select(l => l.Attribute(Ns.R + "id")?.Value is { } rid ? package.Relationship(master.Target, rid) : null)
                .Where(r => r is { External: false } && package.Exists(r.Target))
                .Select(r => (r!.Target, context.Load(r.Target).Root?.Element(Ns.P + "cSld").Str("name") ?? "")))];

    /// <summary>Moves a slide to another place in the list (the index it gets), into the section of the slide before it there.</summary>
    public void MoveSlide(uint slide, int index)
    {
        var s = SlideOf(slide);
        if (index < 0 || index >= slides.Count)
            throw new ArgumentOutOfRangeException(nameof(index), "The place lies outside the list of slides.");
        var from = slides.IndexOf(s);
        if (from == index)
            return;
        var order = slides.Select(x => x.Id).ToList();
        order.RemoveAt(from);
        order.Insert(index, slide);
        var deck = Presentation();
        var list = deck.Xml.Root!.Element(Ns.P + "sldIdLst")!;
        var changes = new List<(int Start, int End, string Text)>();
        var range = deck.Range(list);
        var entry = Entry(list, slide);
        entry.Remove();
        if (index + 1 < order.Count)
            Entry(list, order[index + 1]).AddBeforeSelf(entry);
        else
            Entry(list, order[index - 1]).AddAfterSelf(entry);
        changes.Add((range.Start, range.End, PartText.Xml(list)));
        if (deck.Xml.Descendants(SlideList.P14 + "sectionLst").FirstOrDefault() is { } sections)
        {
            var sectionsRange = deck.Range(sections);
            if (Section(sections, slide) is { } own)
            {
                own.Remove();
                Into(sections, index > 0 ? order[index - 1] : null, own);
                changes.Add((sectionsRange.Start, sectionsRange.End, PartText.Xml(sections)));
            }
        }
        Commit(new Dictionary<string, byte[]?> { [context.PresentationPart] = deck.Text.Spliced(changes) }, new Dictionary<string, List<List<int>>>());
    }

    /// <summary>
    /// Takes a slide away: from the list, its section and the custom shows, with its part, its notes and their
    /// relationships; links of other slides to it go (the text they are on stays).
    /// </summary>
    public void DeleteSlide(uint slide)
    {
        var s = SlideOf(slide);
        var deck = Presentation();
        var rid = Entry(deck.Xml.Root!.Element(Ns.P + "sldIdLst")!, slide).Attribute(Ns.R + "id")!.Value;
        var changes = new List<(int Start, int End, string Text)>();
        var list = deck.Xml.Root.Element(Ns.P + "sldIdLst")!;
        var entry = Entry(list, slide);
        var entryRange = deck.Range(entry);
        changes.Add((entryRange.Start, entryRange.End, ""));
        if (deck.Xml.Descendants(SlideList.P14 + "sectionLst").FirstOrDefault() is { } sections && Section(sections, slide) is { } own)
        {
            var ownRange = deck.Range(own);
            changes.Add((ownRange.Start, ownRange.End, ""));
        }
        foreach (var shown in deck.Xml.Descendants(Ns.P + "custShow").SelectMany(c => c.Descendants(Ns.P + "sld")).Where(e => e.Attribute(Ns.R + "id")?.Value == rid).ToList())
        {
            var shownRange = deck.Range(shown);
            changes.Add((shownRange.Start, shownRange.End, ""));
        }
        var parts = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase)
        {
            [context.PresentationPart] = deck.Text.Spliced(changes),
        };
        var relationships = SlideList.RelationshipsOf(context.PresentationPart);
        parts[relationships] = SlideList.Relationships(package.ReadBytes(relationships), [], new HashSet<string> { rid });
        // The slide's part, its notes and their relationships go.
        var gone = new List<string> { s.Part };
        gone.AddRange(package.Relationships(s.Part).Where(r => r.Is("notesSlide") && !r.External && package.Exists(r.Target)).Select(r => r.Target));
        foreach (var part in gone.ToList())
        {
            gone.Add(SlideList.RelationshipsOf(part));
        }
        foreach (var part in gone.Where(package.Exists))
            parts[part] = null;
        parts["/[Content_Types].xml"] = SlideList.ContentTypes(package.ReadBytes("/[Content_Types].xml"), [], gone.ToHashSet(StringComparer.OrdinalIgnoreCase));
        // Links of other slides to it go, with their relationships.
        var lists = new Dictionary<string, List<List<int>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var other in slides.Where(o => o != s))
        {
            var links = package.Relationships(other.Part).Where(r => !r.External && r.Is("slide") && string.Equals(r.Target, s.Part, StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Id).ToHashSet();
            if (links.Count == 0)
                continue;
            var raw = Parse(other);
            var linked = raw.Xml.Descendants().Where(e => (e.Name == Ns.A + "hlinkClick" || e.Name == Ns.A + "hlinkMouseOver") && links.Contains(e.Attribute(Ns.R + "id")?.Value ?? "")).ToList();
            parts[other.Part] = raw.Text.Spliced(linked.Select(e => raw.Range(e) is var (start, end) ? (start, end, "") : default));
            var otherRelationships = SlideList.RelationshipsOf(other.Part);
            parts[otherRelationships] = SlideList.Relationships(package.ReadBytes(otherRelationships), [], links);
            lists[other.Part] = Lists(other);
        }
        Commit(parts, lists);
    }

    /// <summary>
    /// Copies a slide as PowerPoint's Duplicate Slide does: after it in the list and in its section, with copies of what it
    /// owns (its notes, charts, diagrams, embedded objects; see Owned), sharing the rest; returns the copy's id.
    /// </summary>
    public uint DuplicateSlide(uint slide)
    {
        var s = SlideOf(slide);
        var parts = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var types = new List<(string Part, string Type)>();
        var copy = Copy(s.Part, null, parts, types);
        var lists = new Dictionary<string, List<List<int>>>(StringComparer.OrdinalIgnoreCase)
        {
            [copy] = [.. s.Bodies.Select(b => b.Paragraphs.Select(_ => ++lastId).ToList())],
        };
        var id = List(copy, slides.IndexOf(s) + 1, parts, types);
        Commit(parts, lists);
        return id;
    }

    /// <summary>
    /// Makes a slide from a layout (see Layouts; its part) as PowerPoint does (see SlideList.FromLayout) and puts it at a
    /// place in the list (the index it gets), into the section of the slide before it; returns its id.
    /// </summary>
    public uint AddSlide(int index, string layout)
    {
        if (index < 0 || index > slides.Count)
            throw new ArgumentOutOfRangeException(nameof(index), "The place lies outside the list of slides.");
        if (!Layouts.Any(l => string.Equals(l.Part, layout, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"The presentation has no layout {layout}.", nameof(layout));
        var parts = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var types = new List<(string Part, string Type)>();
        var name = SlideList.FreeName(package, "/ppt/slides/slide1.xml", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var made = SlideList.FromLayout(context.Load(layout));
        parts[name] = SlideList.Document(made);
        parts[SlideList.RelationshipsOf(name)] = SlideList.Relationships(null,
            [("rId1", SlideList.OfficeRelationships + "/slideLayout", SlideList.Relative(name, layout))], new HashSet<string>());
        types.Add((name, SlideList.SlideType));
        var lists = new Dictionary<string, List<List<int>>>(StringComparer.OrdinalIgnoreCase)
        {
            [name] = [.. made.Descendants(Ns.P + "txBody").Select(_ => new List<int> { ++lastId })],
        };
        var id = List(name, index, parts, types);
        Commit(parts, lists);
        return id;
    }

    /// <summary>
    /// A new slide's part put into the list at a place (the index it gets) and into the section of the slide before it,
    /// with its relationship from the presentation and the content types of the new parts; returns its id.
    /// </summary>
    uint List(string part, int index, Dictionary<string, byte[]?> parts, List<(string Part, string Type)> types)
    {
        var deck = Presentation();
        var list = deck.Xml.Root!.Element(Ns.P + "sldIdLst");
        var relationships = SlideList.RelationshipsOf(context.PresentationPart);
        var rid = SlideList.NextRelationshipId(package.Relationships(context.PresentationPart).Select(r => r.Id));
        parts[relationships] = SlideList.Relationships(package.ReadBytes(relationships),
            [(rid, SlideList.OfficeRelationships + "/slide", SlideList.Relative(context.PresentationPart, part))], new HashSet<string>());
        var greatest = deck.Xml.Root.Element(Ns.P + "sldIdLst")?.Elements(Ns.P + "sldId").Select(e => e.Long("id") ?? 0).DefaultIfEmpty(255).Max() ?? 255;
        var id = (uint)Math.Max(256, greatest + 1);
        var entry = new XElement(Ns.P + "sldId", new XAttribute("id", id), new XAttribute(Ns.R + "id", rid));
        var changes = new List<(int Start, int End, string Text)>();
        if (list == null)
        {
            // A presentation without slides gets a list of them where the schema puts it: before the slide size.
            var size = deck.Xml.Root.Element(Ns.P + "sldSz") ?? throw new NotSupportedException("The presentation has no slide size to put a list of slides before.");
            var at = deck.Range(size).Start;
            size.AddBeforeSelf(list = new XElement(Ns.P + "sldIdLst", entry));
            changes.Add((at, at, PartText.Xml(list)));
        }
        else
        {
            var range = deck.Range(list);
            if (index < slides.Count)
                Entry(list, slides[index].Id).AddBeforeSelf(entry);
            else if (slides.Count > 0)
                Entry(list, slides[^1].Id).AddAfterSelf(entry);
            else
                list.Add(entry);
            changes.Add((range.Start, range.End, PartText.Xml(list)));
        }
        if (deck.Xml.Descendants(SlideList.P14 + "sectionLst").FirstOrDefault() is { } sections && sections.Elements(SlideList.P14 + "section").Any())
        {
            var sectionsRange = deck.Range(sections);
            Into(sections, index > 0 ? slides[index - 1].Id : null, new XElement(SlideList.P14 + "sldId", new XAttribute("id", id)));
            changes.Add((sectionsRange.Start, sectionsRange.End, PartText.Xml(sections)));
        }
        parts[context.PresentationPart] = deck.Text.Spliced(changes);
        parts["/[Content_Types].xml"] = SlideList.ContentTypes(package.ReadBytes("/[Content_Types].xml"),
            types.Where(t => package.ContentType(t.Part) != t.Type), new HashSet<string>());
        return id;
    }

    /// <summary>
    /// A copy of a part (a slide's, or what it owns) under a free name of its folder, with copies of the parts it owns
    /// (see Owned) and its relationships to them; a copy of notes leads to the slide copy (<paramref name="slide"/>).
    /// Returns the copy's name.
    /// </summary>
    string Copy(string part, string? slide, Dictionary<string, byte[]?> parts, List<(string Part, string Type)> types)
    {
        var name = SlideList.FreeName(package, part, parts.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase));
        var type = package.ContentType(part);
        var bytes = package.ReadBytes(part);
        // A slide's copy is a slide of its own to co-authoring: its creation id anew.
        if (type == SlideList.SlideType)
            bytes = System.Text.Encoding.UTF8.GetBytes(System.Text.RegularExpressions.Regex.Replace(System.Text.Encoding.UTF8.GetString(bytes),
                @"(<p14:creationId\b[^>]*\bval="")\d+("")", m => m.Groups[1].Value + Random.Shared.NextInt64(1, uint.MaxValue).ToString(CultureInfo.InvariantCulture) + m.Groups[2].Value));
        parts[name] = bytes;
        types.Add((name, type));
        var relationships = SlideList.RelationshipsOf(part);
        if (!package.Exists(relationships))
            return name;
        XNamespace ns = SlideList.RelationshipsNs;
        var xml = XDocument.Load(new MemoryStream(package.ReadBytes(relationships)));
        var targets = package.Relationships(part).ToDictionary(r => r.Id);
        foreach (var relationship in xml.Root!.Elements(ns + "Relationship").ToList())
        {
            if (!targets.TryGetValue((string?)relationship.Attribute("Id") ?? "", out var r) || r.External)
                continue;
            var kind = r.Type[(r.Type.LastIndexOf('/') + 1)..];
            if (kind.StartsWith("comment", StringComparison.OrdinalIgnoreCase) || kind == "authors")
                relationship.Remove();
            else if (kind == "slide" && slide != null)
                relationship.SetAttributeValue("Target", SlideList.Relative(name, slide));
            else if (Owned.Contains(kind) && package.Exists(r.Target))
                relationship.SetAttributeValue("Target", SlideList.Relative(name, Copy(r.Target, slide ?? (type == SlideList.SlideType ? name : null), parts, types)));
        }
        parts[SlideList.RelationshipsOf(name)] = SlideList.Document(xml.Root);
        return name;
    }

    /// <summary>The presentation part as it reads now, parsed afresh with line info.</summary>
    Raw Presentation()
    {
        var text = new PartText(package.ReadBytes(context.PresentationPart));
        var xml = XDocument.Parse(text.Text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        if (xml.Root?.Name != Ns.P + "presentation")
            throw new NotSupportedException("The presentation part is not in the transitional names of PresentationML (a strict presentation).");
        return new Raw(text, xml);
    }

    /// <summary>A slide's entry in the list of slides (p:sldId) by its id.</summary>
    static XElement Entry(XElement list, uint id) =>
        list.Elements(Ns.P + "sldId").FirstOrDefault(e => e.Long("id") == id) ?? throw new InvalidOperationException($"The list of slides has no slide {id}.");

    /// <summary>A slide's entry in its section (p14:sldId), null where it is in none.</summary>
    static XElement? Section(XElement sections, uint id) =>
        sections.Descendants(SlideList.P14 + "sldId").FirstOrDefault(e => e.Long("id") == id);

    /// <summary>A slide's section entry put after that of the slide before it (null: at the start of the first section).</summary>
    static void Into(XElement sections, uint? after, XElement entry)
    {
        if (after is { } before && Section(sections, before) is { } previous)
        {
            previous.AddAfterSelf(entry);
            return;
        }
        var first = sections.Elements(SlideList.P14 + "section").First();
        var list = first.Element(SlideList.P14 + "sldIdLst");
        if (list == null)
            first.AddFirst(list = new XElement(SlideList.P14 + "sldIdLst"));
        list.AddFirst(entry);
    }
}
