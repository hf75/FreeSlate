namespace FreeSlate.Drawing;

/// <summary>
/// A smooth curve along a polyline's points as cubic Bézier pieces (catalog: picture.ink, fitToCurve): the uniform
/// cubic B-spline with the points as control points and its ends held at the first and last point. Like PowerPoint's
/// fitted curve it approximates the points rather than passing through them, cuts corners inwards and never leaves
/// their convex hull (no loops or overshoots between sparse points); along dense ink it stays well within a pixel of
/// the points. Turns sharper than 150° (the pen reversing) stay corners.
/// </summary>
public static class CurveFit
{
    public readonly record struct Cubic((double X, double Y) A, (double X, double Y) B, (double X, double Y) C, (double X, double Y) D);

    /// <summary>The pieces, end to end from the first point to the last (none for fewer than two distinct points).</summary>
    public static List<Cubic> Fit(IReadOnlyList<(double X, double Y)> points)
    {
        var p = new List<(double X, double Y)>();
        foreach (var point in points)
        {
            if (p.Count == 0 || Distance(p[^1], point) > 1e-6)
                p.Add(point);
        }
        var result = new List<Cubic>();
        if (p.Count < 2)
            return result;
        // Corners where the pen reverses split the curve into runs drawn on their own.
        var start = 0;
        for (var i = 1; i < p.Count; i++)
        {
            if (i < p.Count - 1 && Dot(Unit(Sub(p[i], p[i - 1])), Unit(Sub(p[i + 1], p[i]))) > Math.Cos(150 * Math.PI / 180))
                continue;
            Spline(p.GetRange(start, i - start + 1), result);
            start = i;
        }
        return result;
    }

    static void Spline(List<(double X, double Y)> p, List<Cubic> result)
    {
        // The end points tripled hold the curve's ends; each span of four control points is one Bezier piece.
        var q = new List<(double X, double Y)>(p.Count + 4) { p[0], p[0] };
        q.AddRange(p);
        q.Add(p[^1]);
        q.Add(p[^1]);
        for (var i = 0; i + 3 < q.Count; i++)
        {
            var (q0, q1, q2, q3) = (q[i], q[i + 1], q[i + 2], q[i + 3]);
            var a = Mul(Add(Add(q0, Mul(q1, 4)), q2), 1.0 / 6);
            var d = Mul(Add(Add(q1, Mul(q2, 4)), q3), 1.0 / 6);
            if (Distance(a, d) < 1e-9)
                continue;
            result.Add(new Cubic(a, Mul(Add(Mul(q1, 2), q2), 1.0 / 3), Mul(Add(q1, Mul(q2, 2)), 1.0 / 3), d));
        }
    }

    static (double X, double Y) Add((double X, double Y) a, (double X, double Y) b) => (a.X + b.X, a.Y + b.Y);
    static (double X, double Y) Sub((double X, double Y) a, (double X, double Y) b) => (a.X - b.X, a.Y - b.Y);
    static (double X, double Y) Mul((double X, double Y) a, double f) => (a.X * f, a.Y * f);
    static double Dot((double X, double Y) a, (double X, double Y) b) => a.X * b.X + a.Y * b.Y;
    static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    static (double X, double Y) Unit((double X, double Y) v)
    {
        var length = Math.Sqrt(v.X * v.X + v.Y * v.Y);
        return length < 1e-12 ? (0, 0) : (v.X / length, v.Y / length);
    }
}
