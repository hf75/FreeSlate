using System.Globalization;
using System.Security;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Docx.Layout;

namespace FreeSlate;

/// <summary>
/// The document's properties as Word's File > Info shows and sets them (catalog: package.properties): its core properties
/// (docProps/core.xml: title, subject, author, keywords, comments, category, status) and the extended ones Word keeps beside them
/// (docProps/app.xml: company, manager); a value null where the document has none.
/// </summary>
public sealed record DocumentProperties
{
    public string? Title { get; init; }
    public string? Subject { get; init; }
    public string? Author { get; init; }
    public string? Keywords { get; init; }
    public string? Comments { get; init; }
    public string? Category { get; init; }
    public string? Status { get; init; }
    public string? Company { get; init; }
    public string? Manager { get; init; }

    /// <summary>When the document was made (dcterms:created), in UTC.</summary>
    public DateTime? Created { get; init; }
}

/// <summary>
/// Document properties (see DocumentProperties) and the fields that show them and other facts of the document Word keeps as saved till
/// they are updated (TITLE, AUTHOR, NUMWORDS …, see RefreshPropertyFields).
/// </summary>
public sealed partial class WordDocument
{
    static readonly XNamespace CoreNs = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    static readonly XNamespace DcTerms = "http://purl.org/dc/terms/";
    static readonly XNamespace AppNs = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";

    /// <summary>The package's part of a relationship of its own ("/" its source) of a type, where it has one.</summary>
    string? PackagePart(string type) =>
        package.FirstRelationship("/", type) is { External: false } related && package.Exists(related.Target) ? related.Target : null;

    /// <summary>The document's properties as its core and extended properties hold them now (see DocumentProperties).</summary>
    public DocumentProperties Properties
    {
        get
        {
            Read();
            XElement? Root(string type) => PackagePart(type) is { } part ? context.Load(part).Root : null;
            var core = Root("core-properties");
            var app = Root("extended-properties");
            static string? Of(XElement? root, XNamespace ns, string name) => root?.Element(ns + name)?.Value is { Length: > 0 } value ? value : null;
            return new DocumentProperties
            {
                Title = Of(core, Dc, "title"),
                Subject = Of(core, Dc, "subject"),
                Author = Of(core, Dc, "creator"),
                Keywords = Of(core, CoreNs, "keywords"),
                Comments = Of(core, Dc, "description"),
                Category = Of(core, CoreNs, "category"),
                Status = Of(core, CoreNs, "contentStatus"),
                Company = Of(app, AppNs, "Company"),
                Manager = Of(app, AppNs, "Manager"),
                Created = Of(core, DcTerms, "created") is { } created && DateTime.TryParse(created, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var when) ? when : null,
            };
        }
    }

    /// <summary>
    /// Gives the document properties, a change of its own: those given (not null) set, an empty one taken away, the others as they
    /// were. The core properties' part (docProps/core.xml) and the extended ones' (docProps/app.xml) made where the package has none,
    /// related from the package as Word relates them, with nothing in them but what is given (no author, no dates Word would add, but
    /// the time it was made, dcterms:created); in the order Word writes them (measured with Word 365 by COM, BuiltInDocumentProperties:
    /// title, subject, creator, keywords, description, lastModifiedBy, revision, created, modified, category, contentStatus; Manager and
    /// Company in app.xml). Returns whether a change was made.
    /// </summary>
    public bool SetProperties(DocumentProperties given)
    {
        Read();
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var added = new List<NewPart>();
        // The core properties, in Word's order.
        (XNamespace Ns, string Name, string? Value)[] core =
        [
            (Dc, "title", given.Title), (Dc, "subject", given.Subject), (Dc, "creator", given.Author), (CoreNs, "keywords", given.Keywords),
            (Dc, "description", given.Comments), (CoreNs, "lastModifiedBy", null), (CoreNs, "revision", null), (DcTerms, "created", null),
            (DcTerms, "modified", null), (CoreNs, "category", given.Category), (CoreNs, "contentStatus", given.Status),
        ];
        if (core.Any(c => c.Value != null) || given.Created != null && PackagePart("core-properties") == null)
        {
            var part = PackagePart("core-properties");
            var document = part != null ? Xml(package.ReadBytes(part))
                : new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(CoreNs + "coreProperties",
                    new XAttribute(XNamespace.Xmlns + "cp", CoreNs.NamespaceName), new XAttribute(XNamespace.Xmlns + "dc", Dc.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "dcterms", DcTerms.NamespaceName), new XAttribute(XNamespace.Xmlns + "dcmitype", "http://purl.org/dc/dcmitype/"),
                    new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                    new XElement(DcTerms + "created", new XAttribute(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance"), "dcterms:W3CDTF"),
                        Minute(given.Created ?? DateTime.UtcNow).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))));
            var root = document.Root!;
            var changed = part == null;
            for (var k = 0; k < core.Length; k++)
            {
                var (ns, name, value) = core[k];
                if (value == null)
                    continue;
                var element = root.Element(ns + name);
                if (value.Length == 0)
                {
                    if (element != null)
                    {
                        element.Remove();
                        changed = true;
                    }
                    continue;
                }
                if (element != null)
                {
                    if (element.Value != value)
                    {
                        element.Value = value;
                        changed = true;
                    }
                    continue;
                }
                // After the last of those Word writes before it, else first.
                var made = new XElement(ns + name, value);
                if (core.Take(k).Select(c => root.Element(c.Ns + c.Name)).LastOrDefault(e => e != null) is { } before)
                    before.AddAfterSelf(made);
                else
                    root.AddFirst(made);
                changed = true;
            }
            if (changed)
            {
                if (part != null)
                    changes[part] = Bytes(document);
                else
                    added.Add(new NewPart("core", "/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml",
                        "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", Bytes(document)));
            }
        }
        // The time it was made given where the core properties have none (as Word keeps it, to the minute).
        if (given.Created is { } begun && changes.Count == 0 && added.Count == 0 && PackagePart("core-properties") is { } corePart
            && Xml(package.ReadBytes(corePart)) is { Root: { } coreRoot } coreDocument && coreRoot.Element(DcTerms + "created") == null)
        {
            var created = new XElement(DcTerms + "created", new XAttribute(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance"), "dcterms:W3CDTF"),
                Minute(begun).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            if ((coreRoot.Element(CoreNs + "revision") ?? coreRoot.Element(CoreNs + "lastModifiedBy") ?? coreRoot.Element(Dc + "description")) is { } previous)
                previous.AddAfterSelf(created);
            else
                coreRoot.Add(created);
            changes[corePart] = Bytes(coreDocument);
        }
        (string Name, string? Value)[] app = [("Manager", given.Manager), ("Company", given.Company)];
        if (app.Any(a => a.Value != null))
        {
            var part = PackagePart("extended-properties");
            var document = part != null ? Xml(package.ReadBytes(part))
                : new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(AppNs + "Properties"));
            var root = document.Root!;
            var changed = false;
            foreach (var (name, value) in app)
            {
                if (value == null)
                    continue;
                var element = root.Element(AppNs + name);
                if (value.Length == 0)
                {
                    if (element != null)
                    {
                        element.Remove();
                        changed = true;
                    }
                }
                else if (element == null)
                {
                    // Manager before Company, both before what Word writes after them (LinksUpToDate …), as Word orders them.
                    var made = new XElement(AppNs + name, value);
                    if (name == "Company" && root.Element(AppNs + "Manager") is { } manager)
                        manager.AddAfterSelf(made);
                    else if (root.Elements().FirstOrDefault(e => e.Name.LocalName is "LinksUpToDate" or "CharactersWithSpaces" or "SharedDoc" or "HyperlinkBase"
                                 or "HyperlinksChanged" or "AppVersion" or "DocSecurity" && name == "Manager" || e.Name.LocalName == "Company" && name == "Manager") is { } after)
                        after.AddBeforeSelf(made);
                    else
                        root.Add(made);
                    changed = true;
                }
                else if (element.Value != value)
                {
                    element.Value = value;
                    changed = true;
                }
            }
            if (changed)
            {
                if (part != null)
                    changes[part] = Bytes(document);
                else
                    added.Add(new NewPart("app", "/docProps/app.xml", "application/vnd.openxmlformats-officedocument.extended-properties+xml",
                        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties", Bytes(document)));
            }
        }
        if (changes.Count == 0 && added.Count == 0)
            return false;
        if (added.Count > 0)
            Added("", added, changes, "/");
        ChangeParts(changes);
        // Headers and footers show document properties as they are when Word prints (see FieldEvaluator): laid out anew.
        (paginator, laid, layout) = (null, null, null);
        return true;
    }

    /// <summary>A time to the minute, as Word keeps the time a document was made (measured: dcterms:created of a new document at :00).</summary>
    static DateTime Minute(DateTime time) => new(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, time.Kind);

    /// <summary>The fields whose saved results RefreshPropertyFields sets, by their types.</summary>
    static readonly HashSet<string> PropertyFields = new(StringComparer.Ordinal)
    {
        "TITLE", "SUBJECT", "AUTHOR", "KEYWORDS", "COMMENTS", "FILENAME", "NUMWORDS", "NUMCHARS", "CREATEDATE", "DOCPROPERTY", "INFO", "MERGEFIELD",
        "SAVEDATE", "PRINTDATE", "TIME", "SECTION", "SECTIONPAGES",
    };

    /// <summary>
    /// The saved results of the body's fields that show the document's properties and facts set as Word shows them once it updates them
    /// (measured with Word 365 by COM, Fields.Add of each and Fields.Update: TITLE, SUBJECT, AUTHOR, KEYWORDS, COMMENTS, DOCPROPERTY and
    /// INFO the properties' values; FILENAME the file's name, "Document1" in English and "Dokument1" in German before it has one;
    /// NUMWORDS and NUMCHARS the body's words and characters without spaces, as Word counts them; CREATEDATE the time it was made in the
    /// short date and long time of the field's language; MERGEFIELD its name in chevrons, «FirstName», as Word shows it without data;
    /// and those the layout computes as it lays the pages out, SAVEDATE, PRINTDATE, TIME, SECTION and SECTIONPAGES, as the pages show
    /// them). Word shows a field's saved result till it updates it (when it prints only those the layout computes too), so that Word then
    /// shows what the editor does. One change for all, none where all are so; returns whether one was made.
    /// </summary>
    public bool RefreshPropertyFields(string? fileName = null)
    {
        var properties = Properties;
        var made = 0;
        // The fields whose results the model reads as text first (the counts last, of the text as the others then show it), then those
        // it computes, found anew after that change.
        bool Texts(bool counting)
        {
            var (words, characters) = counting ? Counts() : (0, 0);
            var stale = TextFields().Where(f => FieldInline.TypeOf(f.Instruction) is "NUMWORDS" or "NUMCHARS" == counting)
                .Select(f => (f.Field, f.Top, f.Shown, Text: PropertyText(f.Instruction, properties, fileName, words, characters)))
                .Where(f => f.Text != null && f.Text != f.Shown).ToList();
            if (stale.Count == 0)
                return false;
            Change(stale[0].Top, stale[^1].Top, (stretch, _) => TextEdits.Results(stretch, [.. stale.Select(f => (f.Field, f.Text!))]));
            made++;
            return true;
        }
        Texts(counting: false);
        var fields = BodyFields(field => PropertyFields.Contains(field.Type));
        if (fields.Count > 0)
        {
            Scenes();
            var stale = new List<(Inline Field, Block Top, string Text)>();
            foreach (var (field, top) in fields)
            {
                var shown = string.Concat(field.Cached.OfType<TextInline>().Select(t => t.Text));
                // A document property the body keeps as saved (see FieldEvaluator) as Word's update shows it; the others as the pages do.
                var text = field.Type == "DOCPROPERTY" ? PropertyText(field.Instruction, properties, fileName, 0, 0) : layout?.Evaluator?.Value(field, null);
                if (text != null && !FieldEvaluator.IsError(text) && text != shown)
                    stale.Add((field, top, text));
            }
            if (stale.Count > 0)
            {
                Change(stale[0].Top, stale[^1].Top, (stretch, _) => TextEdits.Results(stretch, [.. stale.Select(f => (f.Field, f.Text))]));
                made++;
            }
        }
        Texts(counting: true);
        Combine(made);
        return made > 0;
    }

    /// <summary>
    /// The body's fields whose results the model reads as text (it computes none of them), of the kinds RefreshPropertyFields sets: each
    /// field's element (its beginning, or the simple field), its instruction, the text it shows and the block of the body it stands in.
    /// </summary>
    List<(XElement Field, string Instruction, string Shown, Block Top)> TextFields()
    {
        Read();
        var found = new List<(XElement, string, string, Block)>();
        void Blocks(IEnumerable<Block> list, Block? top)
        {
            foreach (var block in list)
            {
                if (block is ParagraphBlock paragraph)
                {
                    var text = ParagraphText.Of(paragraph);
                    foreach (var (start, end, element) in TextEdits.Stretches(paragraph))
                    {
                        if (InstructionOf(element) is { } instruction && FieldInline.TypeOf(instruction) is var type && PropertyFields.Contains(type)
                            && !FieldInline.IsPageField(instruction))
                        {
                            var (from, to) = (Math.Clamp(start - paragraph.TextOffset, 0, text.Length), Math.Clamp(end - paragraph.TextOffset, 0, text.Length));
                            found.Add((element, instruction.Trim(), text[from..Math.Max(from, to)], top ?? block));
                        }
                    }
                }
                else
                {
                    foreach (var inner in ModelReader.Inner(block))
                        Blocks(inner, top ?? block);
                }
            }
        }
        Blocks(blocks, null);
        return found;
    }

    /// <summary>
    /// What a field showing a property or a fact of the document shows once Word updates it (see RefreshPropertyFields); null for one this
    /// does not know (a DOCPROPERTY of a custom property, a FILENAME with switches of a path).
    /// </summary>
    string? PropertyText(string instruction, DocumentProperties properties, string? fileName, int words, int characters)
    {
        var parts = FieldWords(instruction);
        var type = parts.Count > 0 ? parts[0].ToUpperInvariant() : "";
        string? Named(string name) => name.ToUpperInvariant() switch
        {
            "TITLE" => properties.Title ?? "",
            "SUBJECT" => properties.Subject ?? "",
            "AUTHOR" => properties.Author ?? "",
            "KEYWORDS" => properties.Keywords ?? "",
            "COMMENTS" => properties.Comments ?? "",
            "CATEGORY" => properties.Category ?? "",
            "COMPANY" => properties.Company ?? "",
            "MANAGER" => properties.Manager ?? "",
            _ => null,
        };
        var german = context.Culture.TwoLetterISOLanguageName == "de";
        switch (type)
        {
            case "TITLE" or "SUBJECT" or "AUTHOR" or "KEYWORDS" or "COMMENTS":
                return Named(type);
            case "INFO" when parts.Count > 1:
                return Named(parts[1]);
            case "DOCPROPERTY" when parts.Count > 1:
                return Named(parts[1]) is { Length: > 0 } value ? value
                    : Named(parts[1]) != null ? german ? "Fehler! Unbekannter Name für Dokument-Eigenschaft." : "Error! Unknown document property name." : null;
            case "FILENAME":
                return parts.Skip(1).Any(p => p.StartsWith('\\') && p.Equals("\\p", StringComparison.OrdinalIgnoreCase)) && fileName == null ? null
                    : fileName is { } name ? Path.GetFileName(name) : german ? "Dokument1" : "Document1";
            case "NUMWORDS":
                return words.ToString(CultureInfo.InvariantCulture);
            case "NUMCHARS":
                return characters.ToString(CultureInfo.InvariantCulture);
            case "CREATEDATE":
            {
                var culture = context.Culture;
                var when = Minute(properties.Created ?? DateTime.UtcNow).ToLocalTime();
                var at = parts.IndexOf("\\@");
                var picture = at >= 0 && at + 1 < parts.Count ? parts[at + 1] : culture.DateTimeFormat.ShortDatePattern + " " + culture.DateTimeFormat.LongTimePattern;
                try
                {
                    return when.ToString(picture.Replace("/", "\\/", StringComparison.Ordinal).Replace(":", "\\:", StringComparison.Ordinal), culture);
                }
                catch (FormatException)
                {
                    return when.ToString(culture);
                }
            }
            case "MERGEFIELD" when parts.Count > 1:
                return "«" + parts[1] + "»";
            default:
                return null;
        }
    }

    /// <summary>
    /// The body's words and its characters without spaces as Word's statistics count them (ComputeStatistics, NUMWORDS and NUMCHARS;
    /// measured with Word 365 by COM, Range.ComputeStatistics of each paragraph of a worksheet): its paragraphs' and tables' text as it
    /// shows, a numbered paragraph's number ("1.") among it, the results of the fields the layout computes too; a word each stretch
    /// between spaces (a check box's ☐ and a comma alone too), the characters all but spaces; objects count as neither.
    /// </summary>
    public (int Words, int Characters) Counts()
    {
        Read();
        Scenes();
        var (words, characters) = (0, 0);
        void Blocks(IEnumerable<Block> list)
        {
            foreach (var block in list)
            {
                if (block is ParagraphBlock paragraph)
                {
                    var text = new StringBuilder();
                    if (paragraph.Label is { Text: { } label } && label.Any(char.IsLetterOrDigit))
                        text.Append(label).Append(' ');
                    foreach (var inline in paragraph.Content)
                    {
                        text.Append(inline switch
                        {
                            TextInline t => t.Text,
                            FieldInline field => layout?.Evaluator?.Value(field, null) ?? string.Concat(field.Cached.OfType<TextInline>().Select(t => t.Text)),
                            _ => ParagraphText.Length(inline) > 0 ? " " : "",
                        });
                    }
                    var inWord = false;
                    foreach (var c in text.ToString())
                    {
                        if (char.IsWhiteSpace(c) || c is '\v' or '\f' or '\u000E')
                        {
                            if (inWord)
                                words++;
                            inWord = false;
                            continue;
                        }
                        characters++;
                        inWord = true;
                    }
                    if (inWord)
                        words++;
                }
                else
                {
                    foreach (var inner in ModelReader.Inner(block))
                        Blocks(inner);
                }
            }
        }
        Blocks(blocks);
        return (words, characters);
    }
}
