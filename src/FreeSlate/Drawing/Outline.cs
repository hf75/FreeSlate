using System.Xml.Linq;

namespace FreeSlate.Drawing;

/// <summary>A line end decoration (a:headEnd, a:tailEnd): type none, triangle, stealth, diamond, oval or arrow; sizes sm, med, lg.</summary>
public sealed record LineEnd(string Type, string Width, string Length);

/// <summary>A resolved outline (a:ln, ECMA-376 Part 1, §20.1.2.2.24).</summary>
public sealed record Outline(double WidthEmu, Fill Fill, string Dash, string Cap, string Join, double MiterLimit, string Compound,
    LineEnd? Head = null, LineEnd? Tail = null)
{
    public bool Visible => Fill is not NoFill;

    /// <summary>
    /// A custom dash pattern (a:custDash, Dash "custom"): dash and space lengths in multiples of the line width, from
    /// the a:ds pairs (d and sp in thousandths of a percent of the width).
    /// </summary>
    public IReadOnlyList<double>? CustomDash { get; init; }
}

public static class OutlineReader
{
    /// <summary>
    /// Merges outline definitions: <paramref name="layers"/> are ordered from lowest priority (the theme line style
    /// referenced by lnRef) to highest (the shape's own a:ln); attributes and child groups of later layers win.
    /// </summary>
    public static Outline? Merge(IEnumerable<(XElement Line, ColorContext Colors)> layers, Func<string, string?>? images = null)
    {
        double? width = null;
        Fill? fill = null;
        string? dash = null, cap = null, join = null, compound = null;
        List<double>? custom = null;
        LineEnd? head = null, tail = null;
        double miter = 8;
        var any = false;

        foreach (var (line, colors) in layers)
        {
            any = true;
            if (line.Long("w") is { } w) width = w;
            if (line.Str("cap") is { } c) cap = c;
            if (line.Str("cmpd") is { } cm) compound = cm;
            if (FillReader.FindFill(line) is { } f) fill = FillReader.Read(f, colors, images);
            if (line.Element(Ns.A + "prstDash") is { } pd) dash = pd.Str("val") ?? "solid";
            else if (line.Element(Ns.A + "custDash") is { } cd)
            {
                custom = cd.Elements(Ns.A + "ds").SelectMany(ds => new[] { (ds.Long("d") ?? 0) / 100000.0, (ds.Long("sp") ?? 0) / 100000.0 }).ToList();
                dash = custom.Count > 0 && custom.Any(v => v > 0) ? "custom" : "solid";
            }
            if (line.Element(Ns.A + "headEnd") is { } h) head = new LineEnd(h.Str("type") ?? "none", h.Str("w") ?? "med", h.Str("len") ?? "med");
            if (line.Element(Ns.A + "tailEnd") is { } t) tail = new LineEnd(t.Str("type") ?? "none", t.Str("w") ?? "med", t.Str("len") ?? "med");
            if (line.Element(Ns.A + "round") != null) join = "round";
            else if (line.Element(Ns.A + "bevel") != null) join = "bevel";
            else if (line.Element(Ns.A + "miter") is { } m)
            {
                join = "miter";
                if (m.Long("lim") is { } lim) miter = lim / 100000.0;
            }
        }

        if (!any)
            return null;
        return new Outline(width ?? 9525, fill ?? Fill.None, dash ?? "solid", cap ?? "flat", join ?? "round", miter, compound ?? "sng", head, tail)
        {
            CustomDash = dash == "custom" ? custom : null,
        };
    }
}
