using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using FreeSlate.Fonts;

namespace FreeSlate;

public sealed partial class WordDocument
{
    /// <summary>A new document: one empty paragraph on a page of A4 with the usual margins, set in the given font at the given size in points.</summary>
    public static WordDocument New(string font, double size = 11, FontLocator? fonts = null, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        using var package = new MemoryStream();
        using (var zip = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, xml) in NewParts(SecurityElement.Escape(font), (int)Math.Round(size * 2), culture.Name))
            {
                using var part = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                part.Write("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
                part.Write(xml);
            }
        return Open(package.ToArray(), fonts: fonts, culture: culture);
    }

    /// <summary>The parts of a new document: only what Word and this engine need of it.</summary>
    static IEnumerable<(string Name, string Xml)> NewParts(string font, int halfPoints, string language)
    {
        const string word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        const string relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml";
        yield return ("[Content_Types].xml", $"""
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="{contentType}.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="{contentType}.styles+xml"/><Override PartName="/word/settings.xml" ContentType="{contentType}.settings+xml"/></Types>
            """);
        yield return ("_rels/.rels", $"""
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="{relationships}/officeDocument" Target="word/document.xml"/></Relationships>
            """);
        yield return ("word/_rels/document.xml.rels", $"""
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="{relationships}/styles" Target="styles.xml"/><Relationship Id="rId2" Type="{relationships}/settings" Target="settings.xml"/></Relationships>
            """);
        yield return ("word/document.xml", $"""
            <w:document xmlns:w="{word}"><w:body><w:p/><w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1417" w:right="1417" w:bottom="1134" w:left="1417" w:header="708" w:footer="708" w:gutter="0"/></w:sectPr></w:body></w:document>
            """);
        var lang = language.Length > 0 ? $"""<w:lang w:val="{language}"/>""" : "";
        yield return ("word/styles.xml", $"""
            <w:styles xmlns:w="{word}"><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="{font}" w:hAnsi="{font}" w:eastAsia="{font}" w:cs="{font}"/><w:sz w:val="{halfPoints}"/><w:szCs w:val="{halfPoints}"/>{lang}</w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after="160" w:line="300" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults><w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:qFormat/></w:style></w:styles>
            """);
        yield return ("word/settings.xml", $"""
            <w:settings xmlns:w="{word}"><w:compat><w:compatSetting w:name="compatibilityMode" w:uri="http://schemas.microsoft.com/office/word" w:val="15"/></w:compat></w:settings>
            """);
    }
}
