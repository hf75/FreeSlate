using System.Globalization;
using System.Text;

namespace FreeSlate.Drawing.Geometry;

/// <summary>One SVG path command with its numbers in pixels (A: rx ry rotation large-arc sweep x y).</summary>
public sealed record PathSegment(char Command, double[] Values)
{
    /// <summary>The point the segment ends at (none for Z).</summary>
    public (double X, double Y)? End => Command == 'Z' || Values.Length < 2 ? null : (Values[^2], Values[^1]);
}

/// <summary>A path of an evaluated geometry in pixels.</summary>
public sealed record EvaluatedPath(List<PathSegment> Segments, string Fill, bool Stroke, bool ExtrusionOk = true)
{
    public string Data => Serialize(Segments);

    public static string Serialize(IEnumerable<PathSegment> segments)
    {
        var sb = new StringBuilder();
        foreach (var segment in segments)
        {
            sb.Append(segment.Command);
            for (var i = 0; i < segment.Values.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(segment.Command == 'A' && i is 2 or 3 or 4
                    ? ((int)segment.Values[i]).ToString(CultureInfo.InvariantCulture)
                    : Units.Css(segment.Values[i]));
            }
        }
        return sb.ToString();
    }
}

/// <summary>Paths and text rectangle of a geometry evaluated for one shape size (pixels, shape-local).</summary>
public sealed record EvaluatedGeometry(List<EvaluatedPath> Paths, double TextLeft, double TextTop, double TextRight, double TextBottom);

/// <summary>
/// Evaluates the DrawingML shape formula language (ECMA-376 Part 1, §20.1.9.11): guides are computed in EMU
/// in document order, paths are converted to SVG path data in pixels.
/// </summary>
public static class GeometryEvaluator
{
    static readonly int[] Divisors = [2, 3, 4, 5, 6, 8, 10, 12, 16, 32];

    public static EvaluatedGeometry Evaluate(GeometryDefinition definition, double widthEmu, double heightEmu, IEnumerable<Guide>? adjustments = null)
    {
        var vars = BuiltIns(widthEmu, heightEmu);
        var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in adjustments ?? [])
            overrides[a.Name] = a.Formula;

        foreach (var av in definition.AdjustValues)
            vars[av.Name] = Eval(overrides.GetValueOrDefault(av.Name, av.Formula), vars);
        foreach (var (name, formula) in overrides)
            vars.TryAdd(name, Eval(formula, vars));
        foreach (var gd in definition.Guides)
            vars[gd.Name] = Eval(gd.Formula, vars);

        var paths = definition.Paths.Select(p => new EvaluatedPath(Path(p, widthEmu, heightEmu, vars), p.Fill, p.Stroke, p.ExtrusionOk)).ToList();

        double Px(string arg) => Units.ToPx(Value(arg, vars));
        var rect = definition.TextRect;
        return rect is { } r
            ? new EvaluatedGeometry(paths, Px(r.L), Px(r.T), Px(r.R), Px(r.B))
            : new EvaluatedGeometry(paths, 0, 0, Units.ToPx(widthEmu), Units.ToPx(heightEmu));
    }

    static Dictionary<string, double> BuiltIns(double w, double h)
    {
        var ss = Math.Min(w, h);
        var vars = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["l"] = 0, ["t"] = 0, ["r"] = w, ["b"] = h, ["w"] = w, ["h"] = h,
            ["hc"] = w / 2, ["vc"] = h / 2, ["ss"] = ss, ["ls"] = Math.Max(w, h),
            ["cd2"] = 10800000, ["cd4"] = 5400000, ["cd8"] = 2700000,
            ["3cd4"] = 16200000, ["3cd8"] = 8100000, ["5cd8"] = 13500000, ["7cd8"] = 18900000,
        };
        foreach (var d in Divisors)
        {
            vars[$"wd{d}"] = w / d;
            vars[$"hd{d}"] = h / d;
            vars[$"ssd{d}"] = ss / d;
        }
        return vars;
    }

    static double Value(string arg, Dictionary<string, double> vars) =>
        double.TryParse(arg, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : vars.GetValueOrDefault(arg);

    static double Rad(double angle60000) => angle60000 / 60000.0 * Math.PI / 180;

    public static double Eval(string formula, Dictionary<string, double> vars)
    {
        var parts = formula.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return 0;
        double A(int i) => i < parts.Length ? Value(parts[i], vars) : 0;
        var (x, y, z) = (A(1), A(2), A(3));
        return parts[0] switch
        {
            "*/" => z == 0 ? 0 : x * y / z,
            "+-" => x + y - z,
            "+/" => z == 0 ? 0 : (x + y) / z,
            "?:" => x > 0 ? y : z,
            "abs" => Math.Abs(x),
            "at2" => Math.Atan2(y, x) * 180 / Math.PI * 60000,
            "cat2" => x * Math.Cos(Math.Atan2(z, y)),
            "cos" => x * Math.Cos(Rad(y)),
            "max" => Math.Max(x, y),
            "min" => Math.Min(x, y),
            "mod" => Math.Sqrt(x * x + y * y + z * z),
            "pin" => y < x ? x : y > z ? z : y,
            "sat2" => x * Math.Sin(Math.Atan2(z, y)),
            "sin" => x * Math.Sin(Rad(y)),
            "sqrt" => Math.Sqrt(Math.Max(0, x)),
            "tan" => x * Math.Tan(Rad(y)),
            "val" => x,
            _ => 0,
        };
    }

    static List<PathSegment> Path(PathDefinition path, double w, double h, Dictionary<string, double> vars)
    {
        var sx = path.W is { } pw ? w / pw : 1;
        var sy = path.H is { } ph ? h / ph : 1;
        var segments = new List<PathSegment>();
        double curX = 0, curY = 0, startX = 0, startY = 0;

        void Emit(char command, params double[] emu) => segments.Add(new PathSegment(command, emu.Select(Units.ToPx).ToArray()));

        foreach (var cmd in path.Commands)
        {
            double Arg(int i) => Value(cmd.Args[i], vars) * (i % 2 == 0 ? sx : sy);
            switch (cmd.Kind)
            {
                case "moveTo":
                    (curX, curY) = (Arg(0), Arg(1));
                    (startX, startY) = (curX, curY);
                    Emit('M', curX, curY);
                    break;
                case "lnTo":
                    (curX, curY) = (Arg(0), Arg(1));
                    Emit('L', curX, curY);
                    break;
                case "quadBezTo":
                    Emit('Q', Arg(0), Arg(1), Arg(2), Arg(3));
                    (curX, curY) = (Arg(2), Arg(3));
                    break;
                case "cubicBezTo":
                    Emit('C', Arg(0), Arg(1), Arg(2), Arg(3), Arg(4), Arg(5));
                    (curX, curY) = (Arg(4), Arg(5));
                    break;
                case "arcTo":
                    var wR = Value(cmd.Args[0], vars) * sx;
                    var hR = Value(cmd.Args[1], vars) * sy;
                    (curX, curY) = Arc(segments, curX, curY, wR, hR, Value(cmd.Args[2], vars), Value(cmd.Args[3], vars));
                    break;
                case "close":
                    segments.Add(new PathSegment('Z', []));
                    (curX, curY) = (startX, startY);
                    break;
            }
        }
        return segments;
    }

    /// <summary>
    /// arcTo: the current point lies on the ellipse at the visual angle stAng. Visual angles map to the ellipse
    /// parameter via t = atan2(wR·sin a, hR·cos a); sweeps are split into pieces of at most 180°.
    /// </summary>
    static (double X, double Y) Arc(List<PathSegment> segments, double x, double y, double wR, double hR, double stAng, double swAng)
    {
        double Param(double a) => Math.Atan2(wR * Math.Sin(a), hR * Math.Cos(a));
        var start = Rad(stAng);
        var sweep = Rad(swAng);
        var t0 = Param(start);
        var cx = x - wR * Math.Cos(t0);
        var cy = y - hR * Math.Sin(t0);

        if (wR <= 0 || hR <= 0)
        {
            var t = Param(start + sweep);
            var (ex, ey) = (cx + wR * Math.Cos(t), cy + hR * Math.Sin(t));
            segments.Add(new PathSegment('L', [Units.ToPx(ex), Units.ToPx(ey)]));
            return (ex, ey);
        }

        var pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / Math.PI - 1e-9));
        var (endX, endY) = (x, y);
        for (var i = 1; i <= pieces; i++)
        {
            var t = Param(start + sweep * i / pieces);
            (endX, endY) = (cx + wR * Math.Cos(t), cy + hR * Math.Sin(t));
            segments.Add(new PathSegment('A', [Units.ToPx(wR), Units.ToPx(hR), 0, 0, sweep > 0 ? 1 : 0, Units.ToPx(endX), Units.ToPx(endY)]));
        }
        return (endX, endY);
    }
}
