using System.IO.Compression;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>Changes the markup of named shapes after PowerPoint saved a deck: properties PowerPoint's object model cannot set.</summary>
static class ShapePatch
{
    public static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    public static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    /// <summary>Applies <paramref name="changes"/> (by shape name, p:cNvPr/@name) to the shapes of every slide.</summary>
    public static void Apply(string path, IReadOnlyDictionary<string, Action<XElement>> changes)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.Ordinal) && e.FullName.EndsWith(".xml", StringComparison.Ordinal)).ToList())
        {
            XDocument slide;
            using (var stream = entry.Open())
                slide = XDocument.Load(stream);
            var changed = false;
            foreach (var shape in slide.Descendants().Where(e => e.Name.LocalName is "sp" or "graphicFrame" or "pic" or "cxnSp").ToList())
            {
                var name = shape.Descendants(P + "cNvPr").FirstOrDefault()?.Attribute("name")?.Value ?? "";
                if (!changes.TryGetValue(name, out var change))
                    continue;
                change(shape);
                changed = true;
            }
            if (!changed)
                continue;
            entry.Delete();
            using var output = zip.CreateEntry(entry.FullName).Open();
            slide.Save(output);
        }
    }

    /// <summary>The shape's text body properties.</summary>
    public static XElement BodyPr(XElement shape) => shape.Element(P + "txBody")!.Element(A + "bodyPr")!;

    /// <summary>Sets an attribute on every paragraph's a:pPr (created where missing).</summary>
    public static void Paragraphs(XElement shape, string attribute, string value)
    {
        foreach (var paragraph in shape.Descendants(A + "p"))
        {
            var pPr = paragraph.Element(A + "pPr");
            if (pPr == null)
            {
                pPr = new XElement(A + "pPr");
                paragraph.AddFirst(pPr);
            }
            pPr.SetAttributeValue(attribute, value);
        }
    }
}
