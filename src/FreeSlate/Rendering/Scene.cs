using FreeSlate.Drawing;

namespace FreeSlate.Rendering;

/// <summary>
/// A page as it is drawn, from back to front, in points from its top left (catalog: engine.output): what a backend
/// writes as HTML (HtmlScene) and later draws as an image or writes as PDF. What is not broken down into items yet is
/// held as the HTML or SVG it is drawn as (MarkupItem).
/// </summary>
public sealed class PageScene
{
    /// <summary>The page's number (its HTML id is page-N).</summary>
    public required int Number { get; init; }

    /// <summary>The named @page of the page's paper size in the HTML, which printing keeps every page at.</summary>
    public required string Paper { get; init; }

    /// <summary>The size the page is drawn at: its paper's, or with the markup view's pane as wide as that reaches.</summary>
    public required double Width { get; init; }
    public required double Height { get; init; }

    /// <summary>
    /// For the markup view's pane: the page's own paper width, how much printing scales the page with its pane to fit
    /// the paper, and how far down (in CSS pixels) it then stands; null for a page without the pane.
    /// </summary>
    public (double PaperWidth, double Scale, double Offset)? Sheet { get; init; }

    public List<SceneItem> Items { get; } = [];
}

/// <summary>Something drawn on a page.</summary>
public abstract record SceneItem;

/// <summary>What is drawn as the HTML or SVG it was written as: the parts not broken down into items yet.</summary>
public sealed record MarkupItem(string Html) : SceneItem;

/// <summary>The page's own drawing surface, the page's size, with what is drawn on it.</summary>
public sealed record CanvasItem(double Width, double Height, List<SceneItem> Items) : SceneItem;

/// <summary>What stands on a page of vertical text, turned a quarter clockwise about the page's top right corner.</summary>
public sealed record TurnedItem(double Width, List<SceneItem> Items) : SceneItem;

/// <summary>A filled rectangle.</summary>
public sealed record RectItem(double X, double Y, double Width, double Height, RgbColor Color) : SceneItem;

/// <summary>A solid straight line.</summary>
public sealed record LineItem(double X1, double Y1, double X2, double Y2, double Width, RgbColor Color) : SceneItem;

/// <summary>The dashes of a dashed line, each stroked as a line of its own (see PageRenderer.Dashes).</summary>
public sealed record DashesItem(List<(double X1, double Y1, double X2, double Y2)> Dashes, double Width, RgbColor Color) : SceneItem;

/// <summary>A path filled and stroked, its data in points (an art border's shape).</summary>
public sealed record PathItem(string Data, RgbColor? Fill, RgbColor? Stroke, double StrokeWidth);

/// <summary>Paths in points drawn together (the shapes of an art border).</summary>
public sealed record PathsItem(List<PathItem> Paths) : SceneItem;

/// <summary>The alternative text of a region of the page (a picture's description), for assistive technology.</summary>
public sealed record AltTextItem(string Text, double X, double Y, double Width, double Height) : SceneItem;

/// <summary>A bookmark's place on the page, which links go to.</summary>
public sealed record BookmarkItem(string Name, double X, double Y) : SceneItem;
