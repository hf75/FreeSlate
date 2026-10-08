using System.Runtime.Versioning;
using System.Xml.Linq;

namespace Fidelity.PowerPoint;

/// <summary>
/// Custom dash patterns (catalog: line.dash): a:custDash with one or more dash-space pairs (in thousandths of a
/// percent of the line width) on straight lines, rectangles and ellipses, at several widths and with flat, round and
/// square caps. The object model only sets preset dashes, so the patterns are patched in.
/// </summary>
[SupportedOSPlatform("windows")]
static class DashDeck
{
    /// <summary>The patterns: (dash, space) pairs in 1/1000 % of the line width.</summary>
    static readonly (int D, int Sp)[][] Patterns =
    [
        [(100000, 100000)],
        [(300000, 100000)],
        [(800000, 300000), (100000, 300000)],
        [(50000, 200000)],
        [(400000, 200000), (100000, 200000), (100000, 200000)],
    ];

    static readonly string[] Caps = ["flat", "rnd", "sq"];
    static readonly float[] Widths = [1.5f, 4, 8];

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var lines = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        for (var p = 0; p < Patterns.Length; p++)
        {
            for (var w = 0; w < Widths.Length; w++)
            {
                var line = lines.Shapes.AddLine(30 + w * 310, 40 + p * 95, 300 + w * 310, 40 + p * 95);
                line.Line.Weight = Widths[w];
                line.Line.ForeColor.RGB = CorpusGenerator.Rgb(31, 78, 121);
                line.Name = $"line {p} {w}";
            }
        }

        var shapes = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        for (var p = 0; p < Patterns.Length; p++)
        {
            for (var c = 0; c < Caps.Length; c++)
            {
                var kind = c == 1 ? 9 : 1;   // ellipse for the round caps, rectangles otherwise
                var shape = shapes.Shapes.AddShape(kind, 30 + c * 310, 15 + p * 105, 270, 85);
                shape.Fill.Visible = PowerPointSession.MsoFalse;
                shape.Line.Weight = 5f;
                shape.Line.ForeColor.RGB = CorpusGenerator.Rgb(192, 0, 0);
                shape.Name = $"shape {p} {c}";
            }
        }
    }

    static readonly XNamespace A = ShapePatch.A;

    static XElement Dash((int D, int Sp)[] pattern) =>
        new(A + "custDash", pattern.Select(p => new XElement(A + "ds", new XAttribute("d", p.D), new XAttribute("sp", p.Sp))));

    /// <summary>Replaces the preset dash of every line and shape with its pattern, and sets the caps.</summary>
    public static void Patch(string path)
    {
        var changes = new Dictionary<string, Action<XElement>>();
        for (var p = 0; p < Patterns.Length; p++)
        {
            for (var i = 0; i < 3; i++)
            {
                var (pattern, cap) = (Patterns[p], Caps[i]);
                Action<XElement> change = shape =>
                {
                    var ln = shape.Descendants(A + "ln").First();
                    ln.Element(A + "prstDash")?.Remove();
                    ln.Element(A + "custDash")?.Remove();
                    ln.SetAttributeValue("cap", cap);
                    // The dash comes after the fill in a:ln.
                    var fill = ln.Elements().FirstOrDefault(e => e.Name.LocalName.EndsWith("Fill", StringComparison.Ordinal));
                    if (fill != null)
                        fill.AddAfterSelf(Dash(pattern));
                    else
                        ln.AddFirst(Dash(pattern));
                };
                changes[$"line {p} {i}"] = change;
                changes[$"shape {p} {i}"] = change;
            }
        }
        ShapePatch.Apply(path, changes);
    }
}
