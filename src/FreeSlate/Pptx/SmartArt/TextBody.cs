using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate.Pptx.SmartArt;

/// <summary>
/// The text of a SmartArt shape the way PowerPoint writes it into its drawing (measured from saved drawings): the
/// node's own text at the primary font size, centered unless parTxLTRAlign says otherwise, with 90 % line spacing and
/// 35 % after; the text of its descendants as bullets (•) 22.5 pt in, at the secondary font size, space after from
/// lnSpAfChP (15 % by default), unless the node shows only its descendants' text and starts no bullets at a deeper
/// level (then that text is its top level); margins in points as insets (0.56 em when not constrained); tabs every
/// 3.5 em.
/// </summary>
public static class TextBody
{
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    const double EmuPerPt = 12700;

    /// <summary>The primary font size of a node (points): its value, else the default.</summary>
    public static double PrimaryFont(LayoutNode node) =>
        node.Values.TryGetValue("primFontSz", out var v) && double.IsFinite(v) ? v : DefaultFont;

    /// <summary>
    /// The primary font size of a text node no constraint gives one (points; measured: RadialCluster's middle node, 36 in
    /// a box with room for more; TitlePictureLineup's texts, equal by a constraint without a value, at 36).
    /// </summary>
    public const double DefaultFont = 36;

    /// <summary>The paragraphs of a node: (text, level), levels counted from the node's own point.</summary>
    public static List<(XElement Paragraph, int Level)> Paragraphs(LayoutNode node)
    {
        var result = new List<(XElement, int)>();
        if (node.Text.Count == 0)
            return result;
        var baseDepth = DataModel.Depth(node.Point);
        var firstBullet = int.TryParse(node.Param("stBulletLvl"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var b) ? b : 1;
        // A node showing only its descendants' text starts it at the top level, unbulleted at the primary size, unless
        // its algorithm starts bullets (stBulletLvl 1 and more) at that text's depth or above (measured: MeetTheTeam's
        // roles, VerticalAccentList's and VerticalActionList's children and TitledPictureBlocks', stBulletLvl 2, at the
        // top level; vList2's children, stBulletLvl 1, bulleted).
        var shallowest = node.Text.Count > 0 ? node.Text.Min(p => Math.Max(0, DataModel.Depth(p) - baseDepth)) : 0;
        var unbulleted = node.Text.All(p => p != node.Point)
            && !(node.Param("stBulletLvl") is { } start && int.TryParse(start, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level0) && level0 >= 1 && shallowest >= level0);
        foreach (var point in node.Text)
        {
            var level = Math.Max(0, DataModel.Depth(point) - baseDepth);
            if (unbulleted)
                level -= shallowest;
            // A node showing only the text of its descendants starts them at the first bullet level.
            else if (node.Text.All(p => p != node.Point) && level > 0 && firstBullet <= 1)
                level = Math.Max(1, level);
            var body = point.Element.Element(DataModel.Dgm + "t");
            var paragraphs = body?.Elements(A + "p").ToList() ?? [];
            if (paragraphs.Count == 0)
                paragraphs.Add(new XElement(A + "p"));
            foreach (var p in paragraphs)
                result.Add((p, level));
        }
        return result;
    }

    /// <summary>
    /// The size a node's text gives itself (points): the largest of its runs' sizes when every run has one, else null.
    /// Such text keeps its size, PowerPoint fits it no further (measured: LibreOffice's fill-color-list, titles of 18 pt
    /// in hList1 boxes made for 65).
    /// </summary>
    public static double? ExplicitSize(LayoutNode node)
    {
        var runs = Paragraphs(node).SelectMany(p => p.Paragraph.Elements().Where(e => e.Name.LocalName is "r" or "fld")).ToList();
        if (runs.Count == 0)
            return null;
        var sizes = runs.Select(r => (long?)r.Element(A + "rPr")?.Attribute("sz")).ToList();
        return sizes.All(s => s is > 0) ? sizes.Max()!.Value / 100.0 : null;
    }

    /// <summary>The margin of a side in points: the constraint's value, else 0.56 × the primary font size.</summary>
    public static double Margin(LayoutNode node, string side, double primary) =>
        node.Values.TryGetValue(side, out var value) && double.IsFinite(value) ? value : 0.56 * primary;

    /// <summary>The text body of a node at the given font sizes (points).</summary>
    public static XElement Build(LayoutNode node, double primary, double secondary)
    {
        var paragraphs = Paragraphs(node);
        var hasChildren = paragraphs.Any(p => p.Level > 0);
        var anchor = hasChildren && paragraphs.All(p => p.Level > 0)
            ? Anchor(node.Param("txAnchorVertCh") ?? "t")
            : Anchor(node.Param("txAnchorVert") ?? "mid");
        static string Emu(double points) => ((long)Math.Round(points * EmuPerPt)).ToString(CultureInfo.InvariantCulture);
        var bodyPr = new XElement(A + "bodyPr",
            new XAttribute("spcFirstLastPara", "0"), new XAttribute("vert", "horz"), new XAttribute("wrap", "square"),
            new XAttribute("lIns", Emu(Margin(node, "lMarg", primary))), new XAttribute("tIns", Emu(Margin(node, "tMarg", primary))),
            new XAttribute("rIns", Emu(Margin(node, "rMarg", primary))), new XAttribute("bIns", Emu(Margin(node, "bMarg", primary))),
            new XAttribute("numCol", "1"), new XAttribute("spcCol", "1270"), new XAttribute("anchor", anchor), new XAttribute("anchorCtr", "0"),
            new XElement(A + "noAutofit"));
        var body = new XElement(A + "txBody", bodyPr, new XElement(A + "lstStyle"));
        var parentAlign = Align(node.Param("parTxLTRAlign") ?? (hasChildren ? "l" : "ctr"));
        var childAlign = Align(node.Param("shpTxLTRAlignCh") ?? "l");
        var after = double.TryParse(node.Param("lnSpAfChP"), NumberStyles.Float, CultureInfo.InvariantCulture, out var a) ? a : 15;
        var afterParent = double.TryParse(node.Param("lnSpAfParP"), NumberStyles.Float, CultureInfo.InvariantCulture, out var ap) ? ap : 35;
        foreach (var (source, level) in paragraphs)
        {
            var size = level == 0 ? primary : secondary;
            // Bulleted text hangs by a step of 4.5 pt per size class (measured on PowerPoint's drawings: one step below
            // 12 pt, two below 16, three below 20, four below 28, five from 28 on), each level indented by it.
            var hang = 4.5 * (size < 12 ? 1 : size < 16 ? 2 : size < 20 ? 3 : size < 28 ? 4 : 5);
            var pPr = new XElement(A + "pPr",
                new XAttribute("marL", level == 0 ? "0" : Emu(hang * level)), new XAttribute("lvl", level),
                new XAttribute("indent", level == 0 ? "0" : Emu(-hang)), new XAttribute("algn", level == 0 ? parentAlign : childAlign),
                new XAttribute("defTabSz", Emu(3.5 * size)),
                new XElement(A + "lnSpc", new XElement(A + "spcPct", new XAttribute("val", "90000"))),
                new XElement(A + "spcBef", new XElement(A + "spcPct", new XAttribute("val", "0"))),
                new XElement(A + "spcAft", new XElement(A + "spcPct", new XAttribute("val", ((long)((level == 0 ? afterParent : after) * 1000)).ToString(CultureInfo.InvariantCulture)))),
                level == 0 ? new XElement(A + "buNone") : new XElement(A + "buChar", new XAttribute("char", "•")));
            var paragraph = new XElement(A + "p", pPr);
            foreach (var run in source.Elements().Where(e => e.Name.LocalName is "r" or "fld" or "br"))
            {
                var copy = new XElement(run);
                if (copy.Element(A + "rPr") is not { } rPr)
                {
                    rPr = new XElement(A + "rPr");
                    copy.AddFirst(rPr);
                }
                // A run's own size stays (see ExplicitSize).
                if (rPr.Attribute("sz") == null)
                    rPr.SetAttributeValue("sz", ((long)Math.Round(size * 100)).ToString(CultureInfo.InvariantCulture));
                rPr.SetAttributeValue("kern", "1200");
                paragraph.Add(copy);
            }
            body.Add(paragraph);
        }
        return body;
    }

    static string Anchor(string value) => value switch { "t" => "t", "b" => "b", _ => "ctr" };

    static string Align(string value) => value switch { "l" => "l", "r" => "r", "just" => "just", _ => "ctr" };
}
