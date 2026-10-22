using System.Xml.Linq;
using FreeSlate.Drawing;
using FreeSlate.Html;
using FreeSlate.Text;

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

/// <summary>A black rule as Word's device draws it: a line between columns, a bar tab, a note separator.</summary>
public sealed record RuleItem(double X, double Y, double Width, double Height) : SceneItem;

/// <summary>Items turned clockwise by an angle (degrees) about a point: a line of turned text.</summary>
public sealed record RotatedItem(double Angle, double PivotX, double PivotY, List<SceneItem> Items) : SceneItem;

/// <summary>Items that link to a target (a hyperlink's text).</summary>
public sealed record LinkItem(string Target, List<SceneItem> Items) : SceneItem;

/// <summary>A check box form field's square from its top left, crossed when checked, in 0.48 pt black lines.</summary>
public sealed record CheckBoxItem(double X, double Y, double Side, double Bottom, bool Checked) : SceneItem;

/// <summary>Items cut at a rectangle in points (a drawing in a table cell, at the cell's inside).</summary>
public sealed record ClipItem(double X, double Y, double Width, double Height, List<SceneItem> Items) : SceneItem;

/// <summary>
/// A bitmap stretched over a rectangle in points: its pixels' red, green, blue and alpha bytes, row by row (the shading
/// and borders of a grouped text box, as Word's PDF holds them).
/// </summary>
public sealed record BitmapItem(double X, double Y, double Width, double Height, int PixelWidth, int PixelHeight, byte[] Rgba) : SceneItem;

/// <summary>A solid straight line.</summary>
public sealed record LineItem(double X1, double Y1, double X2, double Y2, double Width, RgbColor Color) : SceneItem;

/// <summary>The dashes of a dashed line, each stroked as a line of its own (see PageRenderer.Dashes).</summary>
public sealed record DashesItem(List<(double X1, double Y1, double X2, double Y2)> Dashes, double Width, RgbColor Color) : SceneItem;

/// <summary>A path filled and stroked, its data in points (an art border's shape).</summary>
public sealed record PathItem(string Data, RgbColor? Fill, RgbColor? Stroke, double StrokeWidth);

/// <summary>Paths in points drawn together (the shapes of an art border).</summary>
public sealed record PathsItem(List<PathItem> Paths) : SceneItem;

/// <summary>
/// Cubic curves in points stroked in a color: from the start through each curve's two control points to its end, the
/// next curve going on from there (a wavy underline).
/// </summary>
public sealed record CurveItem((double X, double Y) Start, List<((double X, double Y) First, (double X, double Y) Second, (double X, double Y) End)> Curves,
    double Width, RgbColor Color) : SceneItem;

/// <summary>The alternative text of a region of the page (a picture's description), for assistive technology.</summary>
public sealed record AltTextItem(string Text, double X, double Y, double Width, double Height) : SceneItem;

/// <summary>A bookmark's place on the page, which links go to.</summary>
public sealed record BookmarkItem(string Name, double X, double Y) : SceneItem;

/// <summary>
/// A run of text set in its font by the backend (in HTML by the browser, which shapes it), its characters on the
/// baseline Y at their places from X (Positions: each UTF-16 unit's, a piece's end after them), in its color unless it
/// has a paint of its own; right-to-left text is set whole from its right end (X + Width), its spaces wider by
/// WordSpacing.
/// </summary>
public sealed record TextItem(string Text, double X, double[] Positions, double Y, FontData Font, double Size, RgbColor Color) : SceneItem
{
    /// <summary>
    /// The paint where it is not the color, as SVG attributes each after a space: outlined letters, gradient fills and
    /// text outlines (not broken down yet).
    /// </summary>
    public string? Paint { get; init; }

    /// <summary>Whether the text is bold, from a bold face or as the backend makes letters bold.</summary>
    public bool Bold { get; init; }

    /// <summary>Bold as Word draws it in a font without a bold face: the letters stroked in the color a 35th of the size wide.</summary>
    public bool StrokedBold { get; init; }

    public bool Italic { get; init; }

    /// <summary>The OpenType features the text turns on and off (off with a minus), which shaping applies.</summary>
    public IReadOnlyList<string> Features { get; init; } = [];

    /// <summary>The text's language, which shaping may follow.</summary>
    public string? Language { get; init; }

    /// <summary>How much wider than the font draws them the letters stand (w:w), about X.</summary>
    public double Scale { get; init; } = 1;

    public bool RightToLeft { get; init; }

    /// <summary>The run's width from X.</summary>
    public double Width { get; init; }

    public double WordSpacing { get; init; }

    /// <summary>Whether right-to-left text joins the letters before and after it (a word over several runs).</summary>
    public bool JoinsBefore { get; init; }

    public bool JoinsAfter { get; init; }
}

/// <summary>
/// Glyphs drawn from their outlines, in a font at a size from the point X on the baseline Y: filled in the color or
/// outlined in it (Hollow; filled white over a shadow), with a synthetic bold's stroke and a synthetic italic's slant,
/// each glyph wider by Scale about its own origin.
/// </summary>
public sealed record GlyphsItem(FontData Font, double Size, double X, double Y, List<GlyphItem> Glyphs, RgbColor Color) : SceneItem
{
    public double Scale { get; init; } = 1;

    public bool Hollow { get; init; }

    public bool Shadow { get; init; }

    public bool SyntheticBold { get; init; }

    public bool SyntheticItalic { get; init; }
}

/// <summary>
/// A glyph of a GlyphsItem: its id in the font, the name of its outline in the document (in HTML its path's id, see
/// GlyphCache), its place from the run's origin in font units, and for an East Asian character turned upright in a
/// vertical line the x of its middle, which it turns a quarter counterclockwise about at the font's em middle.
/// </summary>
public readonly record struct GlyphItem(uint Id, string Outline, double X, double Y, double? TurnX = null);

/// <summary>
/// Text drawn invisibly over the glyphs drawn as outlines for it, its characters at theirs (see TextItem), for
/// selecting, searching and reading it.
/// </summary>
public sealed record HiddenTextItem(string Text, double X, double[] Positions, double Y, FontData Font, double Size) : SceneItem;

/// <summary>A short text in its font and color ending at the point X on the baseline Y: a line number.</summary>
public sealed record LabelItem(string Text, double X, double Y, FontData Font, double Size, RgbColor Color, bool Bold, bool Italic) : SceneItem;

/// <summary>Items drawn from a point, turned a quarter counterclockwise about it: text set across a vertical line.</summary>
public sealed record AcrossItem(double X, double Y, List<SceneItem> Items) : SceneItem;

/// <summary>Items drawn but neither selected nor read as text: the copies of a text that emboss, imprint and shadow lay behind it.</summary>
public sealed record DecorationItem(List<SceneItem> Items) : SceneItem;

/// <summary>
/// Items drawn with the effects of a DrawingML effect list on them (blur, soft edges, inner shadows, glow and shadow), in
/// HTML through the filter of the page's definitions named Filter.
/// </summary>
public sealed record FilterItem(string Filter, XElement Effects, List<SceneItem> Items) : SceneItem;

/// <summary>
/// Items drawn first through the filter of a DrawingML effect list's outer effects (glow and shadow, see FilterItem), then
/// as they are over it: in HTML a copy of the group named Id, which so stands in the page once (text's glow and shadow).
/// </summary>
public sealed record FilterCopyItem(string Id, string Filter, XElement Effects, List<SceneItem> Items) : SceneItem;

/// <summary>
/// Items with their reflection (a:reflection): a copy of them turned, scaled and skewed by Transform (in pixels), faded
/// and, where Blurred, blurred by the page's definitions numbered Id (see SvgEffects.ReflectionOf).
/// </summary>
public sealed record ReflectionItem(int Id, Matrix Transform, bool Blurred, List<SceneItem> Items) : SceneItem;
