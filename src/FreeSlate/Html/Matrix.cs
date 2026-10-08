namespace FreeSlate.Html;

/// <summary>A 2D affine transform in CSS order: (x, y) → (a·x + c·y + e, b·x + d·y + f).</summary>
public readonly record struct Matrix(double A, double B, double C, double D, double E, double F)
{
    public static readonly Matrix Identity = new(1, 0, 0, 1, 0, 0);

    public static Matrix Translate(double x, double y) => new(1, 0, 0, 1, x, y);

    public static Matrix Scale(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);

    /// <summary>Clockwise rotation in degrees (y axis pointing down).</summary>
    public static Matrix Rotate(double degrees)
    {
        var r = degrees * Math.PI / 180;
        var (sin, cos) = Math.SinCos(r);
        return new Matrix(cos, sin, -sin, cos, 0, 0);
    }

    /// <summary>this · other: applies <paramref name="other"/> first, then this.</summary>
    public Matrix Then(Matrix other) => new(
        A * other.A + C * other.B,
        B * other.A + D * other.B,
        A * other.C + C * other.D,
        B * other.C + D * other.D,
        A * other.E + C * other.F + E,
        B * other.E + D * other.F + F);

    public bool IsIdentity => this == Identity;

    public string Css() =>
        $"matrix({Units.Css4(A)},{Units.Css4(B)},{Units.Css4(C)},{Units.Css4(D)},{Units.Css(E)},{Units.Css(F)})";
}
