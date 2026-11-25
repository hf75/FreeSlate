using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>Sections ended at a paragraph (catalog: section.page), as Word's section breaks end them (see WordDocument.BreakSection).</summary>
static partial class TextEdits
{
    /// <summary>
    /// A paragraph made the end of a section: the section's properties (a w:sectPr) put last in its w:pPr, before a change of them tracked,
    /// in place of any it held; a paragraph the reader joined from several ends it with its last part, where Word keeps its mark. Returns
    /// the stretch's new text.
    /// </summary>
    /// <exception cref="NotSupportedException">The paragraph is not in the stretch; nothing changes.</exception>
    public static string EndSection(Stretch stretch, ParagraphBlock paragraph, XElement section)
    {
        var p = Parts(stretch, paragraph)[^1];
        var w = p.Name.Namespace;
        var properties = Properties(p, "pPr");
        properties.Elements(w + "sectPr").Remove();
        var made = new XElement(section);
        if (properties.Element(w + "pPrChange") is { } change)
            change.AddBeforeSelf(made);
        else
            properties.Add(made);
        return stretch.Written([(p, [p])]);
    }
}
