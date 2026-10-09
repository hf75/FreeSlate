using System.Diagnostics;
using System.Xml.Linq;
using FreeSlate.Fonts;
using FreeSlate.Opc;

namespace Fidelity;

/// <summary>
/// Writes the fonts embedded in decks (catalog: text.embeddedFonts) as TrueType files to artifacts/fidelity/fonts,
/// so that the decoder's output can be compared with the installed originals.
/// </summary>
public static class EmbeddedFonts
{
    static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static void Extract(IEnumerable<string> decks)
    {
        foreach (var deck in decks)
        {
            using var package = OpcPackage.Open(Repo.DeckFile(deck), Repo.DeckPassword(deck));
            var presentation = package.FirstRelationship("/", "officeDocument")!.Target;
            var root = package.ReadXml(presentation).Root!;
            var dir = Path.Combine(Repo.Artifacts, "fonts", deck);
            Directory.CreateDirectory(dir);
            Console.WriteLine(deck);
            foreach (var font in root.Descendants(P + "embeddedFont"))
            {
                var typeface = font.Element(P + "font")?.Attribute("typeface")?.Value ?? "?";
                foreach (var style in new[] { "regular", "bold", "italic", "boldItalic" })
                {
                    if (font.Element(P + style)?.Attribute(R + "id")?.Value is not { } id || package.Relationship(presentation, id) is not { } rel)
                        continue;
                    var watch = Stopwatch.StartNew();
                    try
                    {
                        var (family, ttf) = EmbeddedOpenType.Read(package.ReadBytes(rel.Target));
                        File.WriteAllBytes(Path.Combine(dir, $"{typeface}-{style}.ttf"), ttf);
                        Console.WriteLine($"  {typeface,-20} {style,-10} {family,-20} {ttf.Length,8} bytes  {watch.ElapsedMilliseconds,5} ms");
                    }
                    catch (InvalidDataException e)
                    {
                        Console.WriteLine($"  {typeface,-20} {style,-10} {e.Message}");
                    }
                }
            }
        }
    }
}
