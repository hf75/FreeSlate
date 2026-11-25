using System.Globalization;
using System.Security;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Opc;

namespace FreeSlate;

/// <summary>
/// A content control in a paragraph's text as Word's Developer tab inserts them (catalog: sdt.text, sdt.checkbox …): the stretch of the
/// paragraph's text it shows (from Start to End), its kind (checkbox, text, richtext, dropdown, combobox, date), whether it shows its
/// placeholder (w:showingPlcHdr), the building block that placeholder is (w:placeholder/w:docPart) and its text, a check box's state, a
/// list's items (their display texts and values), a date picker's format, language (w:lid) and date (w:fullDate); and whether it is a
/// block control holding the paragraph alone.
/// </summary>
public sealed record WordContentControl(int Start, int End, string Kind)
{
    public bool Showing { get; init; }
    public string? PlaceholderPart { get; init; }
    public string? Placeholder { get; init; }
    public bool Checked { get; init; }
    public IReadOnlyList<(string Text, string Value)> Items { get; init; } = [];
    public string? DateFormat { get; init; }
    public string? Language { get; init; }
    public DateTime? Date { get; init; }
    public bool Block { get; init; }
}

/// <summary>
/// A legacy form field in a paragraph's text (Word's Developer > Legacy Forms, catalog: field.forms): the stretch it shows, its kind
/// (text for FORMTEXT, checkbox for FORMCHECKBOX, dropdown for FORMDROPDOWN), its name (w:ffData/w:name, which a bookmark round it
/// repeats), a check box's state, a drop-down's entries and the one it shows.
/// </summary>
public sealed record FormField(int Start, int End, string Kind, string Name)
{
    public bool Checked { get; init; }
    public IReadOnlyList<string> Entries { get; init; } = [];
    public int Selected { get; init; }
}

/// <summary>
/// Forms as Word's Developer tab and its Restrict Editing make them: content controls and legacy form fields as the paragraphs' text
/// holds them (the model reads both as the text they show), the building blocks of the content controls' placeholders in the glossary
/// document, the style their placeholders show in; and the document's protection.
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>
    /// The content controls of a paragraph's text in order (see WordContentControl): the outermost control of a kind of the Developer tab's
    /// round each stretch, or the block control that holds the paragraph alone; not those bound to data (w:dataBinding), nor the
    /// controls of citations, bibliographies, building blocks, equations, pictures and groups, nor those that show nothing.
    /// </summary>
    public IReadOnlyList<WordContentControl> ControlsOf(int paragraph)
    {
        var p = Find(paragraph).Paragraph;
        var text = ParagraphText.Of(p);
        // A block control holding the paragraph alone: the paragraph's whole text is its (Word's Developer tab makes one of a paragraph
        // selected whole).
        if ((p.Parts?[0] ?? p.Element).Parent is { Name.LocalName: "sdtContent" } content && content.Parent is { Name.LocalName: "sdt" } block
            && ControlKind(block) is not null && content.Elements().Count(e => e.Name.LocalName is "p" or "tbl" or "sdt") == 1)
            return text.Length == 0 ? [] : [Control(block, 0, text.Length, text, PlaceholderTexts()) with { Block = true }];
        var found = new List<(XElement Control, int Start, int End)>();
        var at = p.TextOffset;
        foreach (var inline in p.Content)
        {
            var (s, e) = (at, at + ParagraphText.Length(inline));
            at = e;
            if (e == s || InlineControl(inline) is not { } control)
                continue;
            if (found.Count > 0 && found[^1].Control == control && found[^1].End == s)
                found[^1] = (control, found[^1].Start, e);
            else
                found.Add((control, s, e));
        }
        if (found.Count == 0)
            return [];
        var placeholders = PlaceholderTexts();
        return [.. found.Select(f => Control(f.Control, f.Start, f.End, text[Math.Clamp(f.Start, 0, text.Length)..Math.Clamp(f.End, 0, text.Length)], placeholders))];
    }

    /// <summary>
    /// The outermost content control of a kind of the Developer tab's an inline stands in within its paragraph: the one showing its
    /// placeholder that the inline stands for (its text may be an earlier control's, see Inline.Placeholder), else the controls between
    /// the inline's element and its paragraph; null where there is none.
    /// </summary>
    static XElement? InlineControl(Inline inline)
    {
        static bool Inner(XElement sdt) => sdt.Ancestors().Any(a => a.Name.LocalName == "p");
        var start = inline.Placeholder is { } shown && Inner(shown) ? shown : inline.Xml;
        XElement? outermost = null;
        for (var element = start; element != null && element.Name.LocalName != "p"; element = element.Parent)
        {
            if (element.Name.LocalName == "sdt" && ControlKind(element) != null)
                outermost = element;
        }
        return outermost;
    }

    /// <summary>
    /// The kind of a content control as the Developer tab names its kinds: checkbox (w14:checkbox), text (w:text), richtext (w:richText or
    /// no kind of its own), dropdown (w:dropDownList), combobox (w:comboBox), date (w:date); null for one bound to data, a citation's, a
    /// bibliography's, a building block's, an equation's, a picture's, a group's or a repeating section's.
    /// </summary>
    static string? ControlKind(XElement sdt)
    {
        if (sdt.Child("sdtPr") is not { } properties)
            return "richtext";
        if (properties.Child("dataBinding") != null)
            return null;
        foreach (var e in properties.Elements())
        {
            switch (e.Name.LocalName)
            {
                case "checkbox" when e.Name.Namespace == Wml.W14:
                    return "checkbox";
                case "text" when e.Name.Namespace == Wml.W:
                    return "text";
                case "richText" when e.Name.Namespace == Wml.W:
                    return "richtext";
                case "dropDownList" when e.Name.Namespace == Wml.W:
                    return "dropdown";
                case "comboBox" when e.Name.Namespace == Wml.W:
                    return "combobox";
                case "date" when e.Name.Namespace == Wml.W:
                    return "date";
                case "citation" or "bibliography" or "docPartObj" or "docPartList" or "equation" or "picture" or "group" or "repeatingSection"
                    or "repeatingSectionItem" or "dataBinding":
                    return null;
            }
        }
        return "richtext";
    }

    /// <summary>A content control's facts (see WordContentControl), its placeholder's text the glossary's, else the text it shows where it shows it.</summary>
    static WordContentControl Control(XElement sdt, int start, int end, string shown, IReadOnlyDictionary<string, string> placeholders)
    {
        var properties = sdt.Child("sdtPr");
        var part = properties.Child("placeholder").Child("docPart").Val();
        var showing = properties.Child("showingPlcHdr").OnOff() == true;
        var list = properties.Child("dropDownList") ?? properties.Child("comboBox");
        var items = list?.Elements(Wml.W + "listItem").Select(i => (Text: i.Attr("displayText") ?? i.Attr("value") ?? "", Value: i.Attr("value") ?? i.Attr("displayText") ?? ""))
            .Where(i => i.Text.Length > 0 || i.Value.Length > 0).ToList() ?? [];
        var date = properties.Child("date");
        DateTime? full = date.Attr("fullDate") is { Length: > 0 } given && DateTime.TryParse(given, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var when) ? when : null;
        var box = properties?.Element(Wml.W14 + "checkbox");
        var isChecked = (string?)box?.Element(Wml.W14 + "checked")?.Attribute(Wml.W14 + "val") is "1" or "true" or "on";
        var placeholder = part != null && placeholders.TryGetValue(part, out var known) ? known : showing ? shown : null;
        return new WordContentControl(start, end, ControlKind(sdt) ?? "richtext")
        {
            Showing = showing,
            PlaceholderPart = part,
            Placeholder = placeholder,
            Checked = isChecked,
            Items = items,
            DateFormat = date.Child("dateFormat").Val(),
            Language = date.Child("lid").Val(),
            Date = full,
        };
    }

    /// <summary>
    /// The legacy form fields of a paragraph's text in order (see FormField): FORMTEXT, FORMCHECKBOX and FORMDROPDOWN fields whose
    /// beginning holds their form data (w:ffData), each with the stretch it shows.
    /// </summary>
    public IReadOnlyList<FormField> FormFieldsOf(int paragraph)
    {
        var fields = new List<FormField>();
        foreach (var (start, end, element) in TextEdits.Stretches(Find(paragraph).Paragraph))
        {
            if (element.Name.LocalName != "fldChar" || element.Child("ffData") is not { } data || InstructionOf(element) is not { } instruction)
                continue;
            var kind = FieldInline.TypeOf(instruction) switch
            {
                "FORMTEXT" => "text",
                "FORMCHECKBOX" => "checkbox",
                "FORMDROPDOWN" => "dropdown",
                _ => null,
            };
            if (kind == null)
                continue;
            var box = data.Child("checkBox");
            var list = data.Child("ddList");
            fields.Add(new FormField(start, end, kind, data.Child("name").Val() ?? "")
            {
                Checked = box?.Child("checked") is { } state ? state.OnOff() ?? true : box.Child("default").Val() is "1" or "true" or "on",
                Entries = list?.Elements(Wml.W + "listEntry").Select(e => e.Val() ?? "").ToList() ?? [],
                Selected = (int)(list.Child("result").Number("val") ?? list.Child("default").Number("val") ?? 0),
            });
        }
        return fields;
    }

    /// <summary>
    /// The fields of a paragraph's text that end in it where they begin, in order: the stretch each shows (its result, or the one character
    /// of a field the layout computes) and its instruction.
    /// </summary>
    public IReadOnlyList<(int Start, int End, string Instruction)> FieldsOf(int paragraph) =>
        [.. TextEdits.Stretches(Find(paragraph).Paragraph).Select(s => (s.Start, s.End, Instruction: InstructionOf(s.Element)))
            .Where(s => s.Instruction != null).Select(s => (s.Start, s.End, s.Instruction!.Trim()))];

    const string GlossaryRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/glossaryDocument";
    const string GlossaryType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.glossary+xml";

    /// <summary>The glossary document related from the main part (Word's building blocks of the document), where the package has one.</summary>
    string? GlossaryPart() =>
        package.FirstRelationship(context.MainPart, "glossaryDocument") is { External: false } related && package.Exists(related.Target) ? related.Target : null;

    /// <summary>The texts of the glossary's building blocks (w:docPart) by their names, their paragraphs' text one after the other.</summary>
    IReadOnlyDictionary<string, string> PlaceholderTexts()
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (GlossaryPart() is not { } glossary || context.Load(glossary).Root is not { } root)
            return texts;
        foreach (var part in root.Descendants(Wml.W + "docPart"))
        {
            if (part.Child("docPartPr").Child("name").Val() is { Length: > 0 } name)
                texts.TryAdd(name, string.Join("\n", part.Child("docPartBody")?.Elements(Wml.W + "p").Select(p => string.Concat(p.Descendants(Wml.W + "t").Select(t => t.Value))) ?? []));
        }
        return texts;
    }

    /// <summary>The name Word gives the style its content controls' placeholders show in, whatever the id a language gives it.</summary>
    public const string PlaceholderStyleName = "Placeholder Text";

    /// <summary>The id of the style placeholders show in (see PlaceholderStyleName): the document's, else the one SettlePlaceholders defines.</summary>
    public string PlaceholderStyle => StyleIdNamed(PlaceholderStyleName) ?? "PlaceholderText";

    /// <summary>
    /// The building blocks of the placeholders the body's content controls name (w:placeholder/w:docPart) made in the glossary document
    /// where it lacks them, as Word's Developer tab keeps them (measured with Word 365 by COM, ContentControls.Add and SetPlaceholderText:
    /// each a w:docPart of the gallery "placeholder" in the category "General", of the type bbPlcHdr and the behaviour content, with a
    /// GUID of its own, its text in a paragraph in the style Placeholder Text): the text given for its name, else the text a control
    /// showing it shows. The glossary document made where the package has none, related from the main part as Word relates it; the style
    /// Placeholder Text defined where the document lacks it, as Word 365 defines it (grey, 666666, a priority of 99, semi-hidden). Returns
    /// whether a change was made (one or two: the style, the glossary).
    /// </summary>
    public bool SettlePlaceholders(IReadOnlyDictionary<string, string> texts)
    {
        Read();
        var named = new Dictionary<string, string?>(StringComparer.Ordinal);
        void Note(XElement sdt)
        {
            if (sdt.Child("sdtPr") is not { } properties || properties.Child("placeholder").Child("docPart").Val() is not { Length: > 0 } name)
                return;
            var shown = properties.Child("showingPlcHdr").OnOff() == true && sdt.Child("sdtContent") is { } content
                ? string.Join("\n", content.DescendantsAndSelf().Where(e => e.Name.LocalName == "p").Select(p => string.Concat(p.Descendants(Wml.W + "t").Select(t => t.Value)))
                    .DefaultIfEmpty(string.Concat(content.Descendants(Wml.W + "t").Select(t => t.Value))))
                : null;
            if (!named.TryGetValue(name, out var had) || had == null)
                named[name] = texts.TryGetValue(name, out var given) ? given : shown;
        }
        var seen = new HashSet<XElement>();
        foreach (var p in BodyParagraphs())
        {
            var element = p.Parts?[0] ?? p.Element;
            foreach (var sdt in element.Ancestors().Concat(element.Descendants()).Where(e => e.Name.LocalName == "sdt"))
            {
                if (seen.Add(sdt))
                    Note(sdt);
            }
            foreach (var inline in p.Content)
            {
                if (inline.Placeholder is { } control && seen.Add(control))
                    Note(control);
            }
        }
        var existing = PlaceholderTexts();
        var missing = named.Where(n => !existing.ContainsKey(n.Key) && n.Value != null).ToList();
        if (missing.Count == 0)
            return false;
        var style = PlaceholderStyle;
        var changed = 0;
        if (StyleIdNamed(PlaceholderStyleName) == null)
        {
            var basedOn = StyleIdNamed("Default Paragraph Font") is { } font ? $"<w:basedOn w:val=\"{SecurityElement.Escape(font)}\"/>" : "";
            DefineStyles($"<w:styles xmlns:w=\"{Wml.W.NamespaceName}\"><w:style w:type=\"character\" w:styleId=\"{SecurityElement.Escape(style)}\"><w:name w:val=\"{PlaceholderStyleName}\"/>"
                + $"{basedOn}<w:uiPriority w:val=\"99\"/><w:semiHidden/><w:rPr><w:color w:val=\"666666\"/></w:rPr></w:style></w:styles>");
            changed++;
        }
        var parts = new StringBuilder();
        foreach (var (name, text) in missing)
        {
            parts.Append("<w:docPart><w:docPartPr><w:name w:val=\"").Append(SecurityElement.Escape(name)).Append("\"/><w:category><w:name w:val=\"General\"/>")
                .Append("<w:gallery w:val=\"placeholder\"/></w:category><w:types><w:type w:val=\"bbPlcHdr\"/></w:types><w:behaviors><w:behavior w:val=\"content\"/></w:behaviors>")
                .Append("<w:guid w:val=\"{").Append(Guid.NewGuid().ToString().ToUpperInvariant()).Append("}\"/></w:docPartPr><w:docPartBody>");
            foreach (var line in text!.Split('\n'))
                parts.Append("<w:p><w:r><w:rPr><w:rStyle w:val=\"").Append(SecurityElement.Escape(style)).Append("\"/></w:rPr><w:t xml:space=\"preserve\">")
                    .Append(SecurityElement.Escape(line)).Append("</w:t></w:r></w:p>");
            parts.Append("</w:docPartBody></w:docPart>");
        }
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        if (GlossaryPart() is { } glossary && context.Load(glossary).Root is { } root)
        {
            // The new building blocks after the glossary's own, its text else as it was.
            var text = new PartText(package.ReadBytes(glossary));
            var prefix = root.GetPrefixOfNamespace(Wml.W) is { Length: > 0 } own ? own : null;
            var made = prefix is null or "w" ? parts.ToString() : parts.ToString().Replace("<w:", "<" + prefix + ":", StringComparison.Ordinal).Replace("</w:", "</" + prefix + ":", StringComparison.Ordinal)
                .Replace(" w:", " " + prefix + ":", StringComparison.Ordinal);
            if (root.Element(Wml.W + "docParts") is { } list && text.Start(list) is var start and >= 0)
            {
                var close = text.Text.LastIndexOf('<', text.End(start) - 1);
                changes[glossary] = text.Text[text.TagEnd(start) - 2] == '/'
                    ? text.Spliced([(start, text.End(start), $"<{(prefix != null ? prefix + ":" : "")}docParts>{made}</{(prefix != null ? prefix + ":" : "")}docParts>")])
                    : text.Spliced([(close, close, made)]);
            }
            else
            {
                var close = text.Text.LastIndexOf('<');
                changes[glossary] = text.Spliced([(close, close, $"<{(prefix != null ? prefix + ":" : "")}docParts>{made}</{(prefix != null ? prefix + ":" : "")}docParts>")]);
            }
        }
        else
        {
            var bytes = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n"
                + $"<w:glossaryDocument xmlns:w=\"{Wml.W.NamespaceName}\"><w:docParts>{parts}</w:docParts></w:glossaryDocument>");
            Added("", [new NewPart("glossary", "/word/glossary/document.xml", GlossaryType, GlossaryRelationship, bytes)], changes, context.MainPart);
        }
        ChangeParts(changes);
        changed++;
        Combine(changed);
        return true;
    }

    /// <summary>The body's paragraphs in reading order, those of its tables' cells and text boxes among them.</summary>
    IEnumerable<ParagraphBlock> BodyParagraphs()
    {
        Read();
        IEnumerable<ParagraphBlock> Of(IEnumerable<Block> list)
        {
            foreach (var block in list)
            {
                if (block is ParagraphBlock paragraph)
                {
                    yield return paragraph;
                    foreach (var inline in paragraph.Content)
                    {
                        var (text, children) = inline switch
                        {
                            DrawingInline d => (d.Text, d.ChildText),
                            AnchorInline a => (a.Text, a.ChildText),
                            _ => (null, null),
                        };
                        foreach (var inner in Of(text ?? []).Concat((children ?? []).SelectMany(c => Of(c.Text))))
                            yield return inner;
                    }
                }
                else
                {
                    foreach (var inner in ModelReader.Inner(block))
                    {
                        foreach (var nested in Of(inner))
                            yield return nested;
                    }
                }
            }
        }
        return Of(blocks).ToList();
    }

    /// <summary>
    /// How the document may be edited, as Word's Restrict Editing enforces it (w:documentProtection with w:enforcement in the settings):
    /// forms (filling in forms), comments, trackedChanges or readOnly; null where nothing is enforced. Set (null or "none" to take it
    /// away), a change of its own, as Word's Protect writes it without a password (measured with Word 365 by COM, Document.Protect of
    /// each kind: w:edit and w:enforcement="1" alone); tracked changes switched on with it as Word's Protect switches them on
    /// (w:trackRevisions), and left on where it goes.
    /// </summary>
    /// <exception cref="ArgumentException">The kind is none of those.</exception>
    /// <exception cref="NotSupportedException">The document has no settings part; nothing changes.</exception>
    public string? Protection
    {
        get
        {
            Read();
            if (SettingsPart() is not { } part || context.Load(part).Root?.Element(Wml.W + "documentProtection") is not { } protection
                || protection.Attr("enforcement") is not ("1" or "true" or "on"))
                return null;
            return protection.Attr("edit") is { } edit and not "none" ? edit : null;
        }
        set
        {
            var kind = value is null or "none" ? null : value;
            if (kind is not (null or "forms" or "comments" or "trackedChanges" or "readOnly"))
                throw new ArgumentException("Editing is restricted to forms, comments, trackedChanges or readOnly, or none.", nameof(value));
            if (Protection == kind)
                return;
            var part = SettingsPart() ?? throw new NotSupportedException("The document has no settings part.");
            var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase)
            {
                [part] = Setting("documentProtection", kind == null ? null : prefix => $"<{prefix}documentProtection {prefix}edit=\"{kind}\" {prefix}enforcement=\"1\"/>"),
            };
            var start = Changes;
            ChangeParts(changes);
            if (kind == "trackedChanges")
                TrackRevisions = true;
            Combine(Changes - start);
        }
    }

    /// <summary>
    /// The settings part's bytes with one of its elements (by its local name in w:) given anew, made by <paramref name="made"/> with the
    /// prefix the part writes w: with, or taken away (null); in CT_Settings' order, every other character as it was.
    /// </summary>
    byte[] Setting(string name, Func<string, string>? made)
    {
        var part = SettingsPart() ?? throw new NotSupportedException("The document has no settings part.");
        var text = new PartText(package.ReadBytes(part));
        var root = context.Load(part).Root!;
        var w = root.Name.Namespace;
        var prefix = root.GetPrefixOfNamespace(w) is { Length: > 0 } p ? p + ":" : "";
        var had = root.Elements(w + name).Select(e => text.Start(e)).Where(at => at >= 0).Select(at => (at, text.End(at))).ToList();
        if (made == null)
            return text.Spliced(had.Select(h => (h.Item1, h.Item2, "")));
        var markup = made(prefix);
        if (had.Count > 0)
            return text.Spliced(had.Select((h, k) => (h.Item1, h.Item2, k == 0 ? markup : "")));
        var rank = Array.IndexOf(SettingsOrder, name);
        return root.Elements().FirstOrDefault(e => e.Name.Namespace != w || Array.IndexOf(SettingsOrder, e.Name.LocalName) > rank) is { } after
            ? text.Spliced([(text.Start(after), text.Start(after), markup)])
            : text.Spliced([(text.Text.LastIndexOf('<'), text.Text.LastIndexOf('<'), markup)]);
    }
}
