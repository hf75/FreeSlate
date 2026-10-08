using System.IO.Compression;
using System.Runtime.Versioning;
using System.Text;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// The SmartArt data model (catalog: smartart.data): diagrams with a fill, outline and shadow of their own (dgm:bg,
/// dgm:whole), with a gradient and a dashed outline, and a diagram whose shapes are animated one by one: the
/// background, then a node's shapes, then a node with its connector flying in, then the rest. The object model
/// cannot build diagrams by shape, so the patch writes the timing the way PowerPoint does (p:graphicEl/a:dgm with
/// the shapes' model ids); the animation states are checked against PowerPoint's video of the deck.
/// </summary>
[SupportedOSPlatform("windows")]
static class SmartArtDeck
{
    const string Process = "urn:microsoft.com/office/officeart/2005/8/layout/process1";
    const string Hierarchy = "urn:microsoft.com/office/officeart/2005/8/layout/hierarchy1";
    const string Cycle = "urn:microsoft.com/office/officeart/2005/8/layout/cycle2";

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var layouts = ((dynamic)presentation).Application.SmartArtLayouts;
        dynamic Add(dynamic slide, string layout, float left, float top, float width, float height, string label)
        {
            var diagram = slide.Shapes.AddSmartArt(layouts.Item(layout), left, top, width, height);
            var nodes = diagram.SmartArt.AllNodes;
            for (var i = 1; i <= nodes.Count; i++)
                nodes[i].TextFrame2.TextRange.Text = $"{label} {i}";
            return diagram;
        }

        // A fill, outline and shadow of the whole diagram.
        var formatted = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var process = Add(formatted, Process, 40, 60, 600, 300, "Step");
        process.Fill.Visible = PowerPointSession.MsoTrue;
        process.Fill.ForeColor.RGB = CorpusGenerator.Rgb(255, 204, 153);
        process.Line.Visible = PowerPointSession.MsoTrue;
        process.Line.ForeColor.RGB = CorpusGenerator.Rgb(192, 0, 0);
        process.Line.Weight = 4f;
        process.Shadow.Visible = PowerPointSession.MsoTrue;
        process.Shadow.OffsetX = 8f;
        process.Shadow.OffsetY = 8f;

        // A gradient and a dashed outline next to a diagram without formatting.
        var gradient = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var hierarchy = Add(gradient, Hierarchy, 30, 60, 440, 380, "Item");
        hierarchy.Fill.TwoColorGradient(1, 1);   // msoGradientHorizontal
        hierarchy.Fill.ForeColor.RGB = CorpusGenerator.Rgb(221, 235, 247);
        hierarchy.Fill.BackColor.RGB = CorpusGenerator.Rgb(155, 194, 230);
        hierarchy.Line.Visible = PowerPointSession.MsoTrue;
        hierarchy.Line.DashStyle = 4;   // msoLineDash
        hierarchy.Line.Weight = 2f;
        Add(gradient, Cycle, 500, 60, 420, 380, "Phase");

        // Animated one shape after another (patched).
        var animated = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        var diagram = Add(animated, Hierarchy, 80, 50, 800, 440, "Item");
        diagram.Name = "animated";
        diagram.Fill.Visible = PowerPointSession.MsoTrue;
        diagram.Fill.ForeColor.RGB = CorpusGenerator.Rgb(242, 242, 242);
    }

    static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace Dgm = "http://schemas.openxmlformats.org/drawingml/2006/diagram";
    static readonly XNamespace Dsp = "http://schemas.microsoft.com/office/drawing/2008/diagram";
    static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>Writes the timing of the animated diagram on slide 3.</summary>
    public static void Patch(string path)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        XDocument Load(string name)
        {
            using var stream = zip.GetEntry(name)!.Open();
            return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        }
        string Target(string part, string id)
        {
            var folder = part[..part.LastIndexOf('/')];
            var rels = Load($"{folder}/_rels/{part[(folder.Length + 1)..]}.rels");
            var target = rels.Root!.Elements(Rel + "Relationship").First(r => (string?)r.Attribute("Id") == id).Attribute("Target")!.Value;
            var segments = new List<string>();
            foreach (var segment in $"{folder}/{target}".Split('/'))
            {
                if (segment == "..")
                    segments.RemoveAt(segments.Count - 1);
                else if (segment is not ("." or ""))
                    segments.Add(segment);
            }
            return string.Join('/', segments);
        }

        const string slidePart = "ppt/slides/slide3.xml";
        var slide = Load(slidePart);
        var frame = slide.Descendants(P + "graphicFrame").First(f => (string?)f.Descendants(P + "cNvPr").First().Attribute("name") == "animated");
        var spid = (string)frame.Descendants(P + "cNvPr").First().Attribute("id")!;
        var dataPart = Target(slidePart, (string)frame.Descendants(Dgm + "relIds").First().Attribute(R + "dm")!);
        var data = Load(dataPart);
        var drawingPart = Target(slidePart, (string)data.Descendants(Dsp + "dataModelExt").First().Attribute("relId")!);
        var drawing = Load(drawingPart);

        // Each drawing shape belongs to the node its presentation point stands for; a connector (the presentation
        // of a parent transition) to the child node.
        var points = data.Descendants(Dgm + "pt").ToDictionary(p => (string)p.Attribute("modelId")!);
        var childOfTransition = data.Descendants(Dgm + "cxn").Where(c => c.Attribute("parTransId") != null)
            .ToDictionary(c => (string)c.Attribute("parTransId")!, c => (string)c.Attribute("destId")!);
        var nodes = data.Descendants(Dgm + "pt").Where(p => p.Attribute("type") == null || (string?)p.Attribute("type") == "node")
            .Select(p => (string)p.Attribute("modelId")!).ToList();
        string? NodeOf(string shapeId)
        {
            if (!points.TryGetValue(shapeId, out var point))
                return null;
            var owner = (string?)point.Element(Dgm + "prSet")?.Attribute("presAssocID");
            return owner != null && childOfTransition.TryGetValue(owner, out var child) ? child : owner;
        }
        var shapes = drawing.Descendants(Dsp + "sp").Select(sp => (string)sp.Attribute("modelId")!).ToList();
        var byNode = nodes.ToDictionary(n => n, n => shapes.Where(s => NodeOf(s) == n).ToList());

        var ids = 2;
        string Target3(string id, bool background = false) =>
            $"<p:spTgt spid=\"{spid}\"><p:graphicEl><a:dgm id=\"{id}\"{(background ? " bldStep=\"bg\"" : "")}/></p:graphicEl></p:spTgt>";
        string Visible(string target) =>
            $"<p:set><p:cBhvr><p:cTn id=\"{++ids}\" dur=\"1\" fill=\"hold\"><p:stCondLst><p:cond delay=\"0\"/></p:stCondLst></p:cTn><p:tgtEl>{target}</p:tgtEl>" +
            "<p:attrNameLst><p:attrName>style.visibility</p:attrName></p:attrNameLst></p:cBhvr><p:to><p:strVal val=\"visible\"/></p:to></p:set>";
        string Fade(string target) =>
            Visible(target) + $"<p:animEffect transition=\"in\" filter=\"fade\"><p:cBhvr><p:cTn id=\"{++ids}\" dur=\"500\"/><p:tgtEl>{target}</p:tgtEl></p:cBhvr></p:animEffect>";
        string Fly(string target) =>
            Visible(target) +
            $"<p:anim calcmode=\"lin\" valueType=\"num\"><p:cBhvr additive=\"base\"><p:cTn id=\"{++ids}\" dur=\"500\" fill=\"hold\"/><p:tgtEl>{target}</p:tgtEl>" +
            "<p:attrNameLst><p:attrName>ppt_x</p:attrName></p:attrNameLst></p:cBhvr><p:tavLst><p:tav tm=\"0\"><p:val><p:strVal val=\"#ppt_x\"/></p:val></p:tav>" +
            "<p:tav tm=\"100000\"><p:val><p:strVal val=\"#ppt_x\"/></p:val></p:tav></p:tavLst></p:anim>" +
            $"<p:anim calcmode=\"lin\" valueType=\"num\"><p:cBhvr additive=\"base\"><p:cTn id=\"{++ids}\" dur=\"500\" fill=\"hold\"/><p:tgtEl>{target}</p:tgtEl>" +
            "<p:attrNameLst><p:attrName>ppt_y</p:attrName></p:attrNameLst></p:cBhvr><p:tavLst><p:tav tm=\"0\"><p:val><p:strVal val=\"1+#ppt_h/2\"/></p:val></p:tav>" +
            "<p:tav tm=\"100000\"><p:val><p:strVal val=\"#ppt_y\"/></p:val></p:tav></p:tavLst></p:anim>";
        string Step(string presetClass, int presetId, int subtype, IEnumerable<string> behaviours)
        {
            var (outer, inner, effect) = (++ids, ++ids, ++ids);
            return $"<p:par><p:cTn id=\"{outer}\" fill=\"hold\"><p:stCondLst><p:cond delay=\"indefinite\"/></p:stCondLst><p:childTnLst>" +
                   $"<p:par><p:cTn id=\"{inner}\" fill=\"hold\"><p:stCondLst><p:cond delay=\"0\"/></p:stCondLst><p:childTnLst>" +
                   $"<p:par><p:cTn id=\"{effect}\" presetID=\"{presetId}\" presetClass=\"{presetClass}\" presetSubtype=\"{subtype}\" fill=\"hold\" grpId=\"0\" nodeType=\"clickEffect\">" +
                   $"<p:stCondLst><p:cond delay=\"0\"/></p:stCondLst><p:childTnLst>{string.Concat(behaviours)}</p:childTnLst></p:cTn></p:par>" +
                   "</p:childTnLst></p:cTn></p:par></p:childTnLst></p:cTn></p:par>";
        }

        var first = byNode[nodes[0]];
        var second = byNode[nodes[1]];
        var rest = shapes.Except(first).Except(second);
        var steps = new StringBuilder()
            .Append(Step("entr", 10, 0, [Fade(Target3("{00000000-0000-0000-0000-000000000000}", background: true))]))
            .Append(Step("entr", 10, 0, first.Select(id => Fade(Target3(id)))))
            .Append(Step("entr", 2, 4, second.Select(id => Fly(Target3(id)))))
            .Append(Step("entr", 1, 0, rest.Select(id => Visible(Target3(id)))));
        var timing = XElement.Parse(
            $"<p:timing xmlns:p=\"{P}\" xmlns:a=\"{A}\"><p:tnLst><p:par><p:cTn id=\"1\" dur=\"indefinite\" restart=\"never\" nodeType=\"tmRoot\"><p:childTnLst>" +
            $"<p:seq concurrent=\"1\" nextAc=\"seek\"><p:cTn id=\"2\" dur=\"indefinite\" nodeType=\"mainSeq\"><p:childTnLst>{steps}</p:childTnLst></p:cTn>" +
            "<p:prevCondLst><p:cond evt=\"onPrev\" delay=\"0\"><p:tgtEl><p:sldTgt/></p:tgtEl></p:cond></p:prevCondLst>" +
            "<p:nextCondLst><p:cond evt=\"onNext\" delay=\"0\"><p:tgtEl><p:sldTgt/></p:tgtEl></p:cond></p:nextCondLst></p:seq></p:childTnLst></p:cTn></p:par></p:tnLst>" +
            $"<p:bldLst><p:bldGraphic spid=\"{spid}\" grpId=\"0\"><p:bldSub><a:bldDgm bld=\"one\"/></p:bldSub></p:bldGraphic></p:bldLst></p:timing>");
        slide.Root!.Element(P + "timing")?.Remove();
        // p:timing follows p:transition and precedes p:extLst.
        if (slide.Root.Element(P + "extLst") is { } extensions)
            extensions.AddBeforeSelf(timing);
        else
            slide.Root.Add(timing);

        zip.GetEntry(slidePart)!.Delete();
        using var output = zip.CreateEntry(slidePart).Open();
        slide.Save(output, SaveOptions.DisableFormatting);
    }
}
