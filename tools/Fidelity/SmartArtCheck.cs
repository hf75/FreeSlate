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

    public sealed record Diagram(int Slide, string SlidePart, string Layout, DataModel Model, XElement LayoutDef, XElement? Drawing, (double W, double H) Size, XElement? Style, XElement? Colors);

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
                result.Add(new Diagram(slideNumber, slidePart, (string?)layout.Attribute("uniqueId") ?? "", new DataModel(data), layout, drawing, size, style, colors));
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

    /// <summary>
    /// Compares the engine's layout with PowerPoint's drawing: for every drawing shape the node of its presentation
    /// point; a shape is good when its box is within 1 % of the diagram's size.
    /// </summary>
    /// <summary>
    /// The layout tree of one slide's diagram laid out without text fitting: every node's algorithm, box and values,
    /// indented by depth (--slide=N --tree).
    /// </summary>
    public static void Tree(string file, int slide)
    {
        foreach (var diagram in Diagrams(file).Where(d => d.Slide == slide))
        {
            var root = Engine.Layout(diagram.LayoutDef, diagram.Model, diagram.Size.W, diagram.Size.H);
            var k = Engine.PtPerMm;
            void Print(LayoutNode node, int depth)
            {
                var values = string.Join(" ", node.Values.Where(v => v.Key is "w" or "h" or "l" or "t" or "r" or "b" or "ctrX" or "ctrY" or "sp" or "userH" or "primFontSz")
                    .Select(v => string.Create(CultureInfo.InvariantCulture, $"{v.Key}={v.Value:0.##}")));
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{new string(' ', depth * 2)}{node.Name} [{node.AlgorithmType}] ({node.X * k:0.0} {node.Y * k:0.0} {node.W * k:0.0} {node.H * k:0.0}) scale {node.Scale:0.###}  {values}"));
                foreach (var child in node.Children)
                    Print(child, depth + 1);
            }
            Print(root, 0);
        }
    }

    public static void Geometry(IEnumerable<string> files, bool verbose, int? onlySlide = null)
    {
        foreach (var file in files)
        {
            var (good, shapes) = (0, 0);
            var results = new List<(string Layout, int Good, int Count, double Mean)>();
            using var context = new FreeSlate.Pptx.PresentationContext(OpcPackage.Open(file));
            foreach (var diagram in Diagrams(file))
            {
                if (diagram.Drawing == null || onlySlide != null && diagram.Slide != onlySlide)
                    continue;
                LayoutNode root;
                try
                {
                    var text = new FreeSlate.Pptx.TextRenderer(new FreeSlate.Pptx.SlideContext(context, diagram.SlidePart, diagram.Slide), FreeSlate.Text.FontResolver.Default);
                    root = Engine.Layout(diagram.LayoutDef, diagram.Model, diagram.Size.W, diagram.Size.H, (body, width) => text.LayoutCell(body, [], width));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"  slide {diagram.Slide,3} {Short(diagram.Layout)}: {e.GetType().Name} {e.Message}");
                    continue;
                }
                var nodes = root.Descendants().Prepend(root).GroupBy(n => (n.Point.Id, n.Name)).ToDictionary(g => g.Key, g => g.First());
                var scale = Math.Max(diagram.Size.W, diagram.Size.H);
                var (diagramGood, count, sum) = (0, 0, 0.0);
                var lines = new List<string>();
                foreach (var sp in diagram.Drawing.Descendants(Dsp + "sp"))
                {
                    var id = (string?)sp.Attribute("modelId") ?? "";
                    if (!diagram.Model.Points.TryGetValue(id, out var pres) || pres.Prop("presAssocID") is not { } assoc || pres.Prop("presName") is not { } name)
                        continue;
                    var xfrm = sp.Descendants(A + "xfrm").First();
                    var off = xfrm.Element(A + "off")!;
                    var ext = xfrm.Element(A + "ext")!;
                    var (px, py, pw, ph) = ((long)off.Attribute("x")! / 12700.0, (long)off.Attribute("y")! / 12700.0, (long)ext.Attribute("cx")! / 12700.0, (long)ext.Attribute("cy")! / 12700.0);
                    count++;
                    if (!nodes.TryGetValue((assoc, name), out var node))
                    {
                        sum += 1;
                        lines.Add($"      {name,-20} missing");
                        continue;
                    }
                    var k = Engine.PtPerMm;
                    // The box the shape is drawn in (a quarter-turned shape's box turned, as PowerPoint writes it).
                    var (nx, ny, nw, nh) = Output.DrawnBox(node) ?? (node.X, node.Y, node.W, node.H);
                    var error = new[] { nx * k - px, ny * k - py, nw * k - pw, nh * k - ph }.Max(Math.Abs) / scale;
                    sum += Math.Min(error, 1);
                    if (error < 0.01)
                        diagramGood++;
                    var size = sp.Descendants(A + "rPr").FirstOrDefault()?.Attribute("sz")?.Value;
                    lines.Add(string.Create(CultureInfo.InvariantCulture, $"      {name,-20} ppt ({px,7:0.0} {py,7:0.0} {pw,7:0.0} {ph,7:0.0}) sz {size,-5} ours ({nx * k,7:0.0} {ny * k,7:0.0} {nw * k,7:0.0} {nh * k,7:0.0})  {error * 100,5:0.0} %  {node.Note}"));
                }
                if (count == 0)
                    continue;
                good += diagramGood;
                shapes += count;
                results.Add((Short(diagram.Layout), diagramGood, count, sum / count));
                if (verbose)
                {
                    Console.WriteLine($"  slide {diagram.Slide,3} {Short(diagram.Layout)}: {diagramGood}/{count}");
                    foreach (var line in lines)
                        Console.WriteLine(line);
                    // Text nodes the drawing does not show (their text taken by other shapes), for the text fitting.
                    var k = Engine.PtPerMm;
                    foreach (var node in root.Descendants().Where(n => n.Note.Length > 0 && !lines.Any(l => l.Contains($" {n.Name} "))))
                        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"      ({node.Name,-18} ours ({node.X * k,7:0.0} {node.Y * k,7:0.0} {node.W * k,7:0.0} {node.H * k,7:0.0}) {node.Note})"));
                }
            }
            foreach (var r in results.OrderByDescending(r => r.Mean))
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {r.Layout,-36} {r.Good,3}/{r.Count,-3} mean error {r.Mean * 100,5:0.0} %"));
            Console.WriteLine($"{Path.GetFileName(file)}: {good} of {shapes} shapes within 1 %, {results.Count(r => r.Good == r.Count)} of {results.Count} diagrams exact");
        }
    }
}
