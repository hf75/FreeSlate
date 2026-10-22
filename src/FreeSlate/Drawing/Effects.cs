using FreeSlate.Html;

namespace FreeSlate.Drawing;

/// <summary>
/// What an effect list draws (catalog: effect.*), in CSS pixels and the same for every backend: behind the drawing its
/// outer shadow and its glow, both from its silhouette, then the drawing itself where Source is set (blurred, its edges
/// softened), then its inner shadow on it. Blurs are Gaussians of the standard deviations given (see SvgEffects for the
/// measured constants).
/// </summary>
public sealed record EffectModel
{
    /// <summary>The drawing's own blur (a:blur).</summary>
    public double? Blur { get; init; }

    /// <summary>Soft edges (a:softEdge): the silhouette shrunk by Erode and blurred, as the drawing's alpha.</summary>
    public (double Erode, double Sigma)? SoftEdge { get; init; }

    /// <summary>A glow (a:glow): the silhouette, opaque wherever it is not clear, grown round by Dilate, blurred and colored.</summary>
    public (double Dilate, double Sigma, RgbColor Color)? Glow { get; init; }

    /// <summary>An outer shadow (a:outerShdw, a:prstShdw): the silhouette blurred, moved and colored.</summary>
    public (double Dx, double Dy, double Sigma, RgbColor Color)? Shadow { get; init; }

    /// <summary>An inner shadow (a:innerShdw): what lies outside the silhouette blurred, moved and colored, kept within it.</summary>
    public (double Dx, double Dy, double Sigma, RgbColor Color)? InnerShadow { get; init; }

    /// <summary>Whether the drawing itself is drawn (not for a copy that casts outer effects behind it).</summary>
    public bool Source { get; init; } = true;
}

/// <summary>
/// A reflection (a:reflection): a copy of the drawing under Transform, where Blurred blurred by Sigma, faded along the line
/// from From to To, its alpha going from FromAlpha to ToAlpha and kept beyond the ends; Id numbers its definitions in HTML.
/// </summary>
public sealed record ReflectionModel(int Id, Matrix Transform, bool Blurred, double Sigma, (double X, double Y) From, (double X, double Y) To,
    double FromAlpha, double ToAlpha);
