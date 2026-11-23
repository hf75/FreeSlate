using System.Xml.Linq;
using FreeSlate.Docx;
using FreeSlate.Opc;

namespace FreeSlate;

/// <summary>
/// The sections of a Word document as Page Setup changes them (catalog: section.page, section.columns): the section a
/// paragraph of the body stands in ends at the first paragraph from it on whose properties hold a w:sectPr; the last
/// section's are the body's own, after its last block, which a change of them takes with it (see StretchOf).
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>The page setup of the section a paragraph of the body stands in, every property given (see SectionFormatting).</summary>
    /// <exception cref="NotSupportedException">The paragraph is not in the body (a note, a header or footer, a comment); nothing changes.</exception>
    public SectionFormatting SectionOf(int paragraph)
    {
        var s = SectionEndOf(paragraph)?.EndsSection ?? reader.LastSection();
        return new SectionFormatting
        {
            PageWidth = s.PageWidth, PageHeight = s.PageHeight, Orientation = s.Element?.Element(Wml.W + "pgSz")?.Attribute(Wml.W + "orient")?.Value ?? "portrait",
            Top = s.TopFixed ? -s.Top : s.Top, Bottom = s.BottomFixed ? -s.Bottom : s.Bottom, Left = s.Left, Right = s.Right, Header = s.Header, Footer = s.Footer, Gutter = s.Gutter,
            Columns = s.Columns, ColumnSpace = s.ColumnSpace,
            ColumnLine = s.Element?.Element(Wml.W + "cols")?.Attribute(Wml.W + "sep")?.Value is "1" or "true" or "on",
            Start = s.Start, TitlePage = s.TitlePage, VerticalAlign = s.VerticalAlign,
            PageNumberStart = s.PageNumberStart ?? -1, PageNumberFormat = s.PageNumberFormat,
        };
    }

    /// <summary>
    /// Gives the section a paragraph of the body stands in page setup (see SectionFormatting), in the w:sectPr that ends it;
    /// the page turned (its orientation given without a size) with its width and height swapped, as Word turns it.
    /// </summary>
    /// <exception cref="NotSupportedException">The paragraph is not in the body (a note, a header or footer, a comment); nothing changes.</exception>
    public void Format(int paragraph, SectionFormatting format)
    {
        var end = SectionEndOf(paragraph);
        var now = end?.EndsSection ?? reader.LastSection();
        if (format is { Orientation: { } orientation, PageWidth: null, PageHeight: null } && (orientation == "landscape") != (now.PageWidth > now.PageHeight)
            && now.PageWidth != now.PageHeight)
            format = format with { PageWidth = now.PageHeight, PageHeight = now.PageWidth };
        if (end != null)
            Change(end, end, (stretch, tracking) => TextEdits.Format(stretch, end.EndsSection!.Element, format, tracking));
        else
            Change(blocks[^1], blocks[^1], (stretch, tracking) => TextEdits.Format(stretch, BodySection(), format, tracking), lastSection: true);
    }

    /// <summary>The paragraph of the body that ends the section a paragraph stands in (its properties hold the section's w:sectPr); null for the last section.</summary>
    /// <exception cref="NotSupportedException">The paragraph is not in the body (a note, a header or footer, a comment).</exception>
    public int? SectionEnd(int paragraph) => SectionEndOf(paragraph)?.Id;

    /// <summary>The paragraph of the body that ends the section a paragraph stands in, null for the last section.</summary>
    ParagraphBlock? SectionEndOf(int paragraph)
    {
        Read();
        if (!paragraphs.TryGetValue(paragraph, out var found))
            throw new NotSupportedException("Only the body's paragraphs stand in sections.");
        for (var k = IndexOf(found.Top); k < blocks.Count; k++)
        {
            if (blocks[k] is ParagraphBlock { EndsSection: not null } end)
                return end;
        }
        return null;
    }

    /// <summary>The body's own section properties (the last section's w:sectPr, which no block holds), or null.</summary>
    XElement? BodySection() => body?.LastNode is XElement { Name.LocalName: "sectPr" } last ? last : body?.Element(Wml.W + "sectPr");

    /// <summary>
    /// The header (with <paramref name="footer"/> the footer) of a kind (default, first or even) that the section a
    /// paragraph of the body stands in shows, as Word's Edit Header opens it: the section's own, else that of the nearest
    /// section before it with one (linked to the previous); where no section up to it has one, one made as Word makes it,
    /// in the first section (a paragraph in the Header or Footer style), with headers and footers of every kind there
    /// where the document has none at all (measured in Word 365 by COM: text put into the third section's header of a
    /// document without any gives the first section all six). Returns the id of its first paragraph, which the document
    /// changes as its others.
    /// </summary>
    /// <exception cref="NotSupportedException">The paragraph is not in the body (a note, a header or footer, a comment); nothing changes.</exception>
    public int Header(int paragraph, string kind = "default", bool footer = false)
    {
        if (ShownHeader(paragraph, kind, footer) is { } shown)
            return shown;
        var sections = Sections();
        var none = sections.All(s => s.Properties.Element?.Elements().Any(e => e.Name.LocalName is "headerReference" or "footerReference") != true);
        // Word's order: the even, the default and the first, each header with its footer after it.
        (string Kind, bool Footer)[] made = none ? [("even", false), ("default", false), ("even", true), ("default", true), ("first", false), ("first", true)]
            : [(kind, footer)];
        var parts = Referenced(0, [.. made.Select(m => (m.Kind, m.Footer, EmptyStory(m.Footer), (byte[]?)null))]);
        return FirstParagraph(parts[Array.IndexOf(made, (kind, footer))]);
    }

    /// <summary>
    /// The header (with <paramref name="footer"/> the footer) of a kind (default, first or even) that the section a
    /// paragraph of the body stands in shows: the id of the first paragraph of its own, else of the nearest section's before
    /// it with one (linked to the previous); null where no section up to it has one (see Header).
    /// </summary>
    /// <exception cref="NotSupportedException">The paragraph is not in the body (a note, a header or footer, a comment); nothing changes.</exception>
    public int? ShownHeader(int paragraph, string kind = "default", bool footer = false)
    {
        Kind(kind);
        var sections = Sections();
        for (var k = SectionIndex(paragraph, sections); k >= 0; k--)
        {
            if (Own(sections[k].Properties, kind, footer) is { } part)
                return FirstParagraph(part);
        }
        return null;
    }

    /// <summary>
    /// Links the header (with <paramref name="footer"/> the footer) of a kind of the section a paragraph of the body stands
    /// in to the sections before it, as Word's Link to Previous does: the section's own taken away, it shows theirs; or
    /// unlinks it: a copy of the one it showed (with the part's relationships) becomes its own, as Word makes it (measured
    /// in Word 365 by COM: the second of three sections unlinked has its default header alone of its own).
    /// </summary>
    /// <exception cref="NotSupportedException">The paragraph is not in the body, or in its first section, which has none before it; nothing changes.</exception>
    public void LinkToPrevious(int paragraph, string kind, bool footer, bool link)
    {
        Kind(kind);
        var sections = Sections();
        var index = SectionIndex(paragraph, sections);
        if (index == 0)
            throw new NotSupportedException("The first section has no section before it.");
        var (end, _) = sections[index];
        var own = Own(sections[index].Properties, kind, footer);
        if (link)
        {
            if (own == null)
                return;
            if (end != null)
                Change(end, end, (stretch, _) => TextEdits.RemoveReference(stretch, end.EndsSection!.Element!, kind, footer));
            else
                Change(blocks[^1], blocks[^1], (stretch, _) => TextEdits.RemoveReference(stretch, BodySection()!, kind, footer), lastSection: true);
            return;
        }
        if (own != null)
            return;
        string? shown = null;
        for (var k = index - 1; k >= 0 && shown == null; k--)
            shown = Own(sections[k].Properties, kind, footer);
        var rels = shown != null ? OpcPackage.RelationshipsPart(shown) : null;
        Referenced(index, [(kind, footer, shown != null ? package.ReadBytes(shown) : EmptyStory(footer),
            rels != null && package.Exists(rels) ? package.ReadBytes(rels) : null)]);
    }

    /// <summary>
    /// Whether even pages have headers and footers of their own (w:evenAndOddHeaders in the document's settings, Word's
    /// Different Odd and Even Pages). Set, a change of its own: the settings part given it (every other character as it
    /// was), the pages laid out anew.
    /// </summary>
    /// <exception cref="NotSupportedException">The document has no settings part; nothing changes.</exception>
    public bool EvenAndOddHeaders
    {
        get
        {
            Read();
            return context.Settings.Child("evenAndOddHeaders").OnOff() == true;
        }
        set
        {
            if (EvenAndOddHeaders == value)
                return;
            var part = SettingsPart() ?? throw new NotSupportedException("The document has no settings part.");
            var text = new PartText(package.ReadBytes(part));
            var root = context.Load(part).Root!;
            byte[] bytes;
            if (value)
            {
                // In the settings' order (CT_Settings): after those that come before it, else at the end.
                var rank = Array.IndexOf(SettingsOrder, "evenAndOddHeaders");
                var w = root.Name.Namespace;
                var prefix = root.GetPrefixOfNamespace(w) is { Length: > 0 } p ? p + ":" : "";
                var made = $"<{prefix}evenAndOddHeaders/>";
                bytes = root.Elements().FirstOrDefault(e => e.Name.Namespace != w || Array.IndexOf(SettingsOrder, e.Name.LocalName) > rank) is { } after
                    ? text.Spliced([(text.Start(after), text.Start(after), made)])
                    : text.Spliced([(text.Text.LastIndexOf('<'), text.Text.LastIndexOf('<'), made)]);
            }
            else
                bytes = text.Spliced([.. root.Elements(root.Name.Namespace + "evenAndOddHeaders").Select(e => text.Start(e)).Select(at => (at, text.End(at), ""))]);
            ChangeParts(new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase) { [part] = bytes });
            SettingsChanged();
        }
    }

    /// <summary>The settings part, where the package has one.</summary>
    string? SettingsPart() =>
        package.FirstRelationship(context.MainPart, "settings") is { External: false } related && package.Exists(related.Target) ? related.Target : null;

    /// <summary>
    /// The settings the layout reads (DocumentContext.Settings, loaded with the document) given what the settings part says
    /// now of those the document changes (EvenAndOddHeaders), after a change or going to another version (see Go): the
    /// pages laid out anew.
    /// </summary>
    void SettingsChanged()
    {
        if (SettingsPart() is not { } part || context.Settings is not { } settings)
            return;
        settings.Elements(Wml.W + "evenAndOddHeaders").Remove();
        if (context.Load(part).Root?.Element(Wml.W + "evenAndOddHeaders") is { } now)
            settings.Add(new XElement(now));
        (paginator, laid) = (null, null);
    }

    /// <summary>The order of the document's settings in w:settings (CT_Settings), as far as those after even and odd headers.</summary>
    static readonly string[] SettingsOrder =
    [
        "writeProtection", "view", "zoom", "removePersonalInformation", "removeDateAndTime", "doNotDisplayPageBoundaries", "displayBackgroundShape",
        "printPostScriptOverText", "printFractionalCharacterWidth", "printFormsData", "embedTrueTypeFonts", "embedSystemFonts", "saveSubsetFonts",
        "saveFormsData", "mirrorMargins", "alignBordersAndEdges", "bordersDoNotSurroundHeader", "bordersDoNotSurroundFooter", "gutterAtTop",
        "hideSpellingErrors", "hideGrammaticalErrors", "activeWritingStyle", "proofState", "formsDesign", "attachedTemplate", "linkStyles",
        "stylePaneFormatFilter", "stylePaneSortMethod", "documentType", "mailMerge", "revisionView", "trackRevisions", "doNotTrackMoves",
        "doNotTrackFormatting", "documentProtection", "autoFormatOverride", "styleLockTheme", "styleLockQFSet", "defaultTabStop", "autoHyphenation",
        "consecutiveHyphenLimit", "hyphenationZone", "doNotHyphenateCaps", "showEnvelope", "summaryLength", "clickAndTypeStyle", "defaultTableStyle",
        "evenAndOddHeaders", "bookFoldRevPrinting", "bookFoldPrinting", "bookFoldPrintingSheets", "drawingGridHorizontalSpacing",
        "drawingGridVerticalSpacing", "displayHorizontalDrawingGridEvery", "displayVerticalDrawingGridEvery", "doNotUseMarginsForDrawingGridOrigin",
        "drawingGridHorizontalOrigin", "drawingGridVerticalOrigin", "doNotShadeFormData", "noPunctuationKerning", "characterSpacingControl",
        "printTwoOnOne", "strictFirstAndLastChars", "noLineBreaksAfter", "noLineBreaksBefore", "savePreviewPicture", "doNotValidateAgainstSchema",
        "saveInvalidXml", "ignoreMixedContent", "alwaysShowPlaceholderText", "doNotDemarcateInvalidXml", "saveXmlDataOnly", "useXSLTWhenSaving",
        "saveThroughXslt", "showXMLTags", "alwaysMergeEmptyNamespace", "updateFields", "hdrShapeDefaults", "footnotePr", "endnotePr", "compat",
        "docVars", "rsids", "mathPr", "attachedSchema", "themeFontLang", "clrSchemeMapping", "doNotIncludeSubdocsInStats",
        "doNotAutoCompressPictures", "forceUpgrade", "captions", "readModeInkLockDown", "smartTagType", "schemaLibrary", "shapeDefaults",
        "doNotEmbedSmartTags", "decimalSymbol", "listSeparator",
    ];

    static void Kind(string kind)
    {
        if (kind is not ("default" or "first" or "even"))
            throw new ArgumentException("A header or footer is of the kind default, first or even.", nameof(kind));
    }

    /// <summary>The body's sections in order: the paragraph that ends each (null for the last) and its properties.</summary>
    List<(ParagraphBlock? End, SectionProperties Properties)> Sections()
    {
        Read();
        var sections = new List<(ParagraphBlock? End, SectionProperties Properties)>();
        foreach (var block in blocks)
        {
            if (block is ParagraphBlock { EndsSection: { } properties } end)
                sections.Add((end, properties));
        }
        sections.Add((null, reader.LastSection()));
        return sections;
    }

    /// <summary>The place among the sections (see Sections) of the section a paragraph of the body stands in.</summary>
    int SectionIndex(int paragraph, List<(ParagraphBlock? End, SectionProperties Properties)> sections) =>
        SectionEndOf(paragraph) is { } end ? sections.FindIndex(s => ReferenceEquals(s.End, end)) : sections.Count - 1;

    /// <summary>The part of a section's own header or footer of a kind (its w:headerReference or w:footerReference), or null.</summary>
    string? Own(SectionProperties section, string kind, bool footer) =>
        section.Element?.Elements(Wml.W + (footer ? "footerReference" : "headerReference")).FirstOrDefault(r => (r.Attr("type") ?? "default") == kind) is { } reference
        && reference.Attribute(Ns.R + "id")?.Value is { } id && package.Relationship(context.MainPart, id) is { External: false } related && package.Exists(related.Target)
            ? related.Target : null;

    /// <summary>
    /// Headers and footers given a section (by its place among the sections, see Sections) in one change: the parts made
    /// (their bytes, and relationships of their own for a copy), related from the main part, and the references put into
    /// the section's properties. Returns the parts' names.
    /// </summary>
    List<string> Referenced(int index, IReadOnlyList<(string Kind, bool Footer, byte[] Bytes, byte[]? Relationships)> parts)
    {
        var section = Sections()[index];
        const string Types = "application/vnd.openxmlformats-officedocument.wordprocessingml.", Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var added = parts.Select((p, k) => new NewPart("hf" + k, p.Footer ? "/word/footer1.xml" : "/word/header1.xml", Types + (p.Footer ? "footer+xml" : "header+xml"),
            Relationships + (p.Footer ? "footer" : "header"), p.Bytes, Relationships: p.Relationships)).ToList();
        var markup = Added(string.Concat(parts.Select((p, k) => $"<w:{(p.Footer ? "footer" : "header")}Reference w:type=\"{p.Kind}\" r:id=\"{{hf{k}}}\"/>")), added, changes,
            context.MainPart);
        var references = Elements(markup, context.Document.Root!);
        Strictly(context.MainPart, references);
        if (section.End is { } end)
            Change(end, end, (stretch, _) => TextEdits.AddReferences(stretch, end.EndsSection!.Element, [.. references.Select(e => new XElement(e))]), changes: changes);
        else
            Change(blocks[^1], blocks[^1], (stretch, _) => TextEdits.AddReferences(stretch, BodySection(), [.. references.Select(e => new XElement(e))]), changes: changes,
                lastSection: true);
        // The parts by the references now in the section (Added numbered them on where their names were taken).
        var now = Sections()[index].Properties;
        return [.. parts.Select(p => Own(now, p.Kind, p.Footer) ?? throw new InvalidOperationException("The header was not put in."))];
    }

    /// <summary>A header's or footer's part as Word makes one empty: one paragraph in the Header or Footer style.</summary>
    byte[] EmptyStory(bool footer)
    {
        var w = Wml.W;
        var style = context.Styles.IdByName(footer ? "footer" : "header", "paragraph");
        var root = new XElement(w + (footer ? "ftr" : "hdr"), new XAttribute(XNamespace.Xmlns + "w", w.NamespaceName), new XAttribute(XNamespace.Xmlns + "r", Ns.R.NamespaceName),
            new XElement(w + "p", style != null ? new XElement(w + "pPr", new XElement(w + "pStyle", new XAttribute(w + "val", style))) : null));
        if (IsStrictPart(context.MainPart))
            Strict.Revert(root);
        return Bytes(new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root));
    }

    /// <summary>The id of a header's or footer's first paragraph, its story read for the layout where it was not.</summary>
    int FirstParagraph(string part)
    {
        var story = reader.Story(part);
        KnowStories();
        return story.SelectMany(Flat).FirstOrDefault()?.Id ?? throw new NotSupportedException("The header or footer has no paragraph.");
    }
}
