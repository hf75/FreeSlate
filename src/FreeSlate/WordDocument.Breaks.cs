using System.Xml.Linq;
using FreeSlate.Docx;

namespace FreeSlate;

/// <summary>
/// Section breaks as Word's Layout > Breaks puts them (catalog: section.page), measured in Word 365 by COM (scratchpad
/// gaps/C/sectprobe.ps1, Range.InsertBreak of each kind into a document with headers and footers): a break put in at a paragraph's
/// start makes an empty paragraph before it whose w:sectPr is a copy of the section's properties, the references of its headers and
/// footers with them; the part after the break keeps the section's properties but those references (its headers and footers linked to
/// the part before), beginning as the break's kind says (its w:type: continuous, evenPage, oddPage, none for a new page); a break put in
/// after a paragraph's text makes that paragraph end the section. Word gives the part after a break the section's first page of its own
/// (w:titlePg) and its first page's number again; here, as for a table's pages across (see TurnTablePages), only the part before keeps
/// what the section has of its own, the part after it going on with it. And the document's mirrored margins (Page Setup's Multiple pages).
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>How a section begins (w:type), the break before it: on the next page, on the same one, on the next even or odd page, in the next column.</summary>
    public static readonly IReadOnlyList<string> SectionStarts = ["nextPage", "continuous", "evenPage", "oddPage", "nextColumn"];

    /// <summary>
    /// Ends a section at a paragraph of the body as Word's section breaks do (see the class): before the paragraph, in an empty paragraph
    /// made there, or (<paramref name="at"/>) with the paragraph itself. The section ending there takes <paramref name="properties"/> where
    /// they are given (a w:sectPr's markup, as a write keeps one it takes away, see SectionMarkup), else a copy of the section's with what it
    /// has of its own (where the paragraph ends a section already, that section keeps its own); the section after the break begins as
    /// <paramref name="kind"/> says (see SectionStarts). One change; returns the paragraph that ends the section.
    /// </summary>
    /// <exception cref="ArgumentException">The kind is none of SectionStarts.</exception>
    /// <exception cref="NotSupportedException">The paragraph is not one of the body itself (it is in a table, a note, a header or footer), or changes are tracked; nothing changes.</exception>
    public int BreakSection(int paragraph, string kind = "nextPage", string? properties = null, bool at = false)
    {
        if (!SectionStarts.Contains(kind))
            throw new ArgumentException("A section begins as one of " + string.Join(", ", SectionStarts) + ".", nameof(kind));
        Read();
        if (!paragraphs.TryGetValue(paragraph, out var found) || !ReferenceEquals(found.Paragraph, found.Top))
            throw new NotSupportedException("A section ends at a paragraph of the body itself, not in a table, a note, a header or footer.");
        if (Author != null)
            throw new NotSupportedException("A section break is not made as a tracked change.");
        var start = version;
        try
        {
            var endsOne = ReferenceEquals(SectionEndOf(paragraph), found.Paragraph);
            int ending;
            int? after;
            if (at && endsOne)
            {
                // The section ends there already: it takes the properties given, the one after it begins as the kind says.
                if (properties != null)
                {
                    var made = Elements(properties, context.Document.Root!);
                    Strictly(context.MainPart, made);
                    var p = Find(paragraph).Paragraph;
                    Change(p, p, (stretch, _) => TextEdits.EndSection(stretch, p, made[0]));
                }
                (ending, after) = (paragraph, Next(paragraph));
            }
            else if (at && Next(paragraph) is { } next)
            {
                // The paragraph itself ends the section: its properties hold the section's.
                var section = Section(paragraph);
                var made = Elements(properties ?? SectionCopy(section), context.Document.Root!);
                Strictly(context.MainPart, made);
                var p = Find(paragraph).Paragraph;
                Change(p, p, (stretch, _) => TextEdits.EndSection(stretch, p, made[0]));
                (ending, after) = (paragraph, next);
            }
            else
            {
                // An empty paragraph before the paragraph (after the last of the body) ends the section it stands in.
                var markup = properties != null ? $"<w:p><w:pPr>{properties}</w:pPr></w:p>" : Ending(Section(paragraph), own: true, small: false);
                ending = InsertBlocks(paragraph, markup, before: !at)[0];
                after = Next(ending);
            }
            if (after is not { } following)
            {
                // The section after the break holds a paragraph, as Word's last section does.
                following = InsertBlocks(ending, "<w:p/>")[0];
            }
            // The part after the break goes on with what the part before has of its own, where the section's properties were copied.
            if (properties == null && !(at && endsOne))
                Own(following, null);
            Format(following, new SectionFormatting { Start = kind });
            Combine(version - start);
            return ending;
        }
        catch
        {
            Back(start);
            throw;
        }
    }

    /// <summary>The section properties a paragraph of the body stands under (the w:sectPr of the section it is in, the body's own for the last).</summary>
    XElement Section(int paragraph) => SectionEndOf(paragraph)?.EndsSection?.Element ?? BodySection() ?? new XElement(Wml.W + "sectPr");

    /// <summary>
    /// A copy of a section's properties to end another section with, as Word copies them for a break (its change tracked left out),
    /// written with the document's own prefixes (w:, r: …), which InsertBlocks declares round it.
    /// </summary>
    static string SectionCopy(XElement section)
    {
        var w = section.Name.Namespace;
        var copy = new XElement(section);
        copy.Elements(w + "sectPrChange").Remove();
        var declared = (section.Document?.Root?.Attributes() ?? []).Where(a => a.IsNamespaceDeclaration).ToList();
        if (declared.All(a => a.Value != w.NamespaceName))
            declared.Add(new XAttribute(XNamespace.Xmlns + "w", w.NamespaceName));
        if (declared.All(a => a.Value != Ns.R.NamespaceName))
            declared.Add(new XAttribute(XNamespace.Xmlns + "r", Ns.R.NamespaceName));
        foreach (var declaration in declared)
            copy.SetAttributeValue(declaration.Name, declaration.Value);
        var text = copy.ToString(SaveOptions.DisableFormatting);
        foreach (var declaration in copy.Attributes().Where(a => a.IsNamespaceDeclaration).ToList())
            text = text.Replace(" " + declaration, "", StringComparison.Ordinal);
        return text;
    }

    /// <summary>
    /// The properties of the section a paragraph of the body stands in as written (its w:sectPr with the namespaces it names declared), to
    /// end a section with again (see BreakSection) where a change takes away the paragraph that holds them.
    /// </summary>
    /// <exception cref="NotSupportedException">The paragraph is not in the body.</exception>
    public string SectionMarkup(int paragraph) => SectionCopy(Section(paragraph));

    /// <summary>
    /// Whether the margins of facing pages mirror each other, the left margin inside and the right outside (w:mirrorMargins in the
    /// settings, Page Setup's Multiple pages: Mirror margins, measured in Word 365 by COM: a setting of the document, whichever section it
    /// is given in). Set, a change of its own: the settings part given it, the pages laid out anew.
    /// </summary>
    /// <exception cref="NotSupportedException">The document has no settings part; nothing changes.</exception>
    public bool MirrorMargins
    {
        get
        {
            Read();
            return context.Settings.Child("mirrorMargins").OnOff() == true;
        }
        set
        {
            if (MirrorMargins == value)
                return;
            ChangeParts(new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase) { [SettingsPart() ?? throw new NotSupportedException("The document has no settings part.")] = Flagged("mirrorMargins", value) });
            SettingsChanged();
        }
    }
}
