using System.Globalization;
using System.Xml.Linq;
using FreeSlate.Opc;
using FreeSlate.Pptx.SmartArt;

namespace Fidelity;

/// <summary>
/// Checks the SmartArt layout engine against PowerPoint: for every diagram of a deck the layout tree built from the
/// data model and layout definition is compared with the presentation points and the drawing PowerPoint saved.
/// </summary>
public static class SmartArtCheck
{
    static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace Dsp = "http://schemas.microsoft.com/office/drawing/2008/diagram";

    public sealed record Diagram(int Slide, string Layout, DataModel Model, XElement LayoutDef, XElement? Drawing, (double W, double H) Size, XElement? Style, XElement? Colors);

    /// <summary>The diagrams of a deck with their parts.</summary>
    public static IEnumerable<Diagram> Diagrams(string path)
    {
        using var package = OpcPackage.Open(path);
        var presentation = package.FirstRelationship("/", "officeDocument")!.Target;
        var slideNumber = 0;
        var slideIds = package.ReadXml(presentation).Root!.Element(P + "sldIdLst")?.Elements(P + "sldId") ?? [];
        var result = new List<Diagram>();
        foreach (var slideId in slideIds)
        {
            slideNumber++;
            var slidePart = package.Relationship(presentation, (string)slideId.Attribute(R + "id")!)!.Target;
            var slide = package.ReadXml(slidePart);
            foreach (var frame in slide.Descendants(P + "graphicFrame"))
            {
                var relIds = frame.Descendants().FirstOrDefault(e => e.Name.LocalName == "relIds");
                if (relIds == null)
                    continue;
                string? Part(string attribute) => (string?)relIds.Attribute(R + attribute) is { } id ? package.Relationship(slidePart, id)?.Target : null;
                var data = package.ReadXml(Part("dm")!).Root!;
                var layout = package.ReadXml(Part("lo")!).Root!;
                var style = Part("qs") is { } qs ? package.ReadXml(qs).Root : null;
                var colors = Part("cs") is { } cs ? package.ReadXml(cs).Root : null;
                var drawingId = data.Descendants(Dsp + "dataModelExt").FirstOrDefault()?.Attribute("relId")?.Value;
                var drawing = drawingId != null && package.Relationship(slidePart, drawingId)?.Target is { } drawingPart ? package.ReadXml(drawingPart).Root : null;
                var ext = frame.Element(P + "xfrm")!.Element(A + "ext")!;
                var size = ((long)ext.Attribute("cx")! / 12700.0, (long)ext.Attribute("cy")! / 12700.0);
                result.Add(new Diagram(slideNumber, (string?)layout.Attribute("uniqueId") ?? "", new DataModel(data), layout, drawing, size, style, colors));
            }
        }
        return result;
    }

    public static void Run(IEnumerable<string> files)
    {
        foreach (var file in files)
        {
            int good = 0, total = 0;
            foreach (var diagram in Diagrams(file))
            {
                var tree = new LayoutTree(diagram.LayoutDef, diagram.Model);
                var built = tree.Root.Descendants().Prepend(tree.Root).Select(n => (n.Point.Id, n.Name)).ToHashSet();
                var saved = diagram.Model.Presentations.Keys.ToHashSet();
                var missing = saved.Except(built).ToList();
                var extra = built.Except(saved).ToList();
                total++;
                if (missing.Count == 0 && extra.Count == 0)
                    good++;
                else
                    Console.WriteLine($"  slide {diagram.Slide,3} {Short(diagram.Layout),-28} saved {saved.Count,3} built {built.Count,3} missing {missing.Count,3} extra {extra.Count,3}"
                                      + $"  e.g. missing {string.Join(", ", missing.Take(3).Select(m => m.Name))} extra {string.Join(", ", extra.Take(3).Select(m => m.Name))}");
            }
            Console.WriteLine($"{Path.GetFileName(file)}: {good} of {total} layout trees match PowerPoint's presentation points");
        }
    }

    static string Short(string uid) => uid[(uid.LastIndexOf('/') + 1)..];
}
