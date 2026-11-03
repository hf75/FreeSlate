using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml.Linq;
using FreeSlate.Fonts;

namespace FreeSlate;

public sealed partial class WordDocument
{
    /// <summary>A new document: one empty paragraph on a page of A4 with the usual margins, set in the given font at the given size in points.</summary>
    /// <param name="styles">Style definitions it begins with, as DefineStyles gives them, but as it was opened: no change to undo.</param>
    /// <param name="numbering">The numbering definitions of the lists those styles name (see DefineStyles), in a numbering part of its own.</param>
    /// <param name="hyphenate">
    /// Whether words break with a hyphen at the end of a line as Word's automatic hyphenation breaks them (w:autoHyphenation): where the
    /// text's language has patterns and the paragraph's style does not suppress it.
    /// </param>
    public static WordDocument New(string font, double size = 11, FontLocator? fonts = null, CultureInfo? culture = null, string? styles = null,
        string? numbering = null, bool hyphenate = false)
    {
        culture ??= CultureInfo.CurrentCulture;
        using var package = new MemoryStream();
        using (var zip = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, xml) in NewParts(SecurityElement.Escape(font), (int)Math.Round(size * 2), culture.Name, numbering != null, hyphenate))
            {
                using var part = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                if (name == "word/styles.xml" && styles != null)
                {
                    var defined = XDocument.Parse(xml);
                    Define(defined.Root!, XElement.Parse(styles));
                    part.Write(Encoding.UTF8.GetString(Bytes(new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), defined.Root))));
                    continue;
                }
                if (name == "word/numbering.xml" && numbering != null)
                {
                    var lists = XDocument.Parse(xml);
                    Number(lists.Root!, XElement.Parse(numbering));
                    part.Write(Encoding.UTF8.GetString(Bytes(new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), lists.Root))));
                    continue;
                }
                part.Write("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
                part.Write(xml);
            }
        return Open(package.ToArray(), fonts: fonts, culture: culture);
    }

    /// <summary>
    /// The parts of a new document: only what Word and this engine need of it, with a numbering part where it has <paramref name="lists"/>,
    /// and automatic hyphenation where it is to <paramref name="hyphenate"/>.
    /// </summary>
    static IEnumerable<(string Name, string Xml)> NewParts(string font, int halfPoints, string language, bool lists, bool hyphenate)
    {
        const string word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        const string relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml";
        var numberingType = lists ? $"""<Override PartName="/word/numbering.xml" ContentType="{contentType}.numbering+xml"/>""" : "";
        yield return ("[Content_Types].xml", $"""
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="{contentType}.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="{contentType}.styles+xml"/><Override PartName="/word/settings.xml" ContentType="{contentType}.settings+xml"/>{numberingType}</Types>
            """);
        yield return ("_rels/.rels", $"""
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="{relationships}/officeDocument" Target="word/document.xml"/></Relationships>
            """);
        var numberingRelationship = lists ? $"""<Relationship Id="rId3" Type="{relationships}/numbering" Target="numbering.xml"/>""" : "";
        yield return ("word/_rels/document.xml.rels", $"""
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="{relationships}/styles" Target="styles.xml"/><Relationship Id="rId2" Type="{relationships}/settings" Target="settings.xml"/>{numberingRelationship}</Relationships>
            """);
        yield return ("word/document.xml", $"""
            <w:document xmlns:w="{word}"><w:body><w:p/><w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1417" w:right="1417" w:bottom="1134" w:left="1417" w:header="708" w:footer="708" w:gutter="0"/></w:sectPr></w:body></w:document>
            """);
        var lang = language.Length > 0 ? $"""<w:lang w:val="{language}"/>""" : "";
        yield return ("word/styles.xml", $"""
            <w:styles xmlns:w="{word}"><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="{font}" w:hAnsi="{font}" w:eastAsia="{font}" w:cs="{font}"/><w:sz w:val="{halfPoints}"/><w:szCs w:val="{halfPoints}"/>{lang}</w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after="160" w:line="300" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults><w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:qFormat/></w:style></w:styles>
            """);
        var hyphenation = hyphenate ? "<w:autoHyphenation/>" : "";
        yield return ("word/settings.xml", $"""
            <w:settings xmlns:w="{word}">{hyphenation}<w:compat><w:compatSetting w:name="compatibilityMode" w:uri="http://schemas.microsoft.com/office/word" w:val="15"/></w:compat></w:settings>
            """);
        if (lists)
            yield return ("word/numbering.xml", $"""<w:numbering xmlns:w="{word}"/>""");
    }
}
