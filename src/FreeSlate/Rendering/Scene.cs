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

    /// <summary>The outlines the glyph ids of the scene's items name (in HTML the document's definitions).</summary>
    public GlyphCache? Glyphs { get; init; }

    /// <summary>
    /// Whether outlines repeat their dash patterns in their exact width, as Word does, rather than in whole pixels, as
    /// PowerPoint does (see SvgPaint.DashUnit).
    /// </summary>
    public bool ExactDashes { get; init; }

    public List<SceneItem> Items { get; } = [];
}

/// <summary>Something drawn on a page.</summary>
public abstract record SceneItem;

/// <summary>A scene item that holds items of its own (a box's, a group's, a canvas's), drawn within it.</summary>
public interface IHoldsItems
{
    List<SceneItem> Items { get; }
}

/// <summary>What is drawn as the HTML or SVG it was written as: the parts not broken down into items yet.</summary>
public sealed record MarkupItem(string Html) : SceneItem;

/// <summary>The page's own drawing surface, the page's size, with what is drawn on it.</summary>
public sealed record CanvasItem(double Width, double Height, List<SceneItem> Items) : SceneItem, IHoldsItems;

/// <summary>What stands on a page of vertical text, turned a quarter clockwise about the page's top right corner.</summary>
public sealed record TurnedItem(double Width, List<SceneItem> Items) : SceneItem, IHoldsItems;

/// <summary>A filled rectangle.</summary>
public sealed record RectItem(double X, double Y, double Width, double Height, RgbColor Color) : SceneItem;

/// <summary>A black rule as Word's device draws it: a line between columns, a bar tab, a note separator.</summary>
public sealed record RuleItem(double X, double Y, double Width, double Height) : SceneItem;

/// <summary>Items turned clockwise by an angle (degrees) about a point: a line of turned text.</summary>
public sealed record RotatedItem(double Angle, double PivotX, double PivotY, List<SceneItem> Items) : SceneItem, IHoldsItems;

/// <summary>Items that link to a target (a hyperlink's text).</summary>
public sealed record LinkItem(string Target, List<SceneItem> Items) : SceneItem, IHoldsItems;

/// <summary>A check box form field's square from its top left, crossed when checked, in 0.48 pt black lines.</summary>
public sealed record CheckBoxItem(double X, double Y, double Side, double Bottom, bool Checked) : SceneItem;

/// <summary>Items cut at a rectangle in points (a drawing in a table cell, at the cell's inside).</summary>
public sealed record ClipItem(double X, double Y, double Width, double Height, List<SceneItem> Items) : SceneItem, IHoldsItems;

/// <summary>
/// A bitmap stretched over a rectangle in points: its pixels' red, green, blue and alpha bytes, row by row (the shading
/// and borders of a grouped text box, as Word's PDF holds them).
/// </summary>
public sealed record BitmapItem(double X, double Y, double Width, double Height, int PixelWidth, int PixelHeight, byte[] Rgba) : SceneItem;

/// <summary>
/// A box of the shared DrawingML renderer (slides and the drawings of Word documents), in CSS pixels: of its size where
/// it has one, placed by its transform where it has one, holding its items; in HTML a div of class sp with the box's own
/// attributes (the player's shape id, accessibility). A shape's box holds its canvas (SvgItem), then what stands over
/// it (a picture's media, text laid out flat).
/// </summary>
public sealed record BoxItem((double Width, double Height)? Size, Matrix? Transform, string Attributes, List<SceneItem> Items) : SceneItem, IHoldsItems;

/// <summary>A drawing surface in CSS pixels, Width by Height, with what is drawn on it (a shape's geometry, a chart, a table).</summary>
public sealed record SvgItem(double Width, double Height, List<SceneItem> Items) : SceneItem, IHoldsItems;

/// <summary>The shapes of a group (DrawingML grpSp), in HTML a div of class grp with the group's own attributes (in Word its effects' filter).</summary>
public sealed record GroupBoxItem(string Attributes, List<SceneItem> Items) : SceneItem, IHoldsItems
{
    /// <summary>The effects the filter among its attributes draws (Word's group effects), or null.</summary>
    public EffectModel? Effects { get; init; }
}

/// <summary>
/// A path of a shape's geometry, its SVG path data in CSS pixels, filled and outlined as DrawingML gives it (no fill,
/// no outline where it has none); in HTML a path element with the attributes of its paint, whose gradients and
/// patterns are the shape's definitions.
/// </summary>
public sealed record ShapePathItem(string Data, Fill Fill, Outline? Outline, string Attributes) : SceneItem
{
    /// <summary>Attributes HTML writes before the path data (the id a copy refers to, a transform).</summary>
    public string Lead { get; init; } = "";

    /// <summary>The transform the lead attributes hold, in CSS pixels.</summary>
    public Matrix? Transform { get; init; }

    /// <summary>The effects the filter among its attributes draws (warped text's).</summary>
    public EffectModel? Effects { get; init; }

    /// <summary>
    /// Whether the outline's dash pattern runs on closed figures, as its attributes stroke it (SvgPaint.Stroke): round
    /// caps then start half a width back.
    /// </summary>
    public bool DashesClosed { get; init; }
}

/// <summary>
/// An ellipse of a shape (an oval line end), its center and radii in CSS pixels, turned by Angle degrees about its
/// center and filled as DrawingML gives it; in HTML an ellipse element with the attributes of its paint.
/// </summary>
public sealed record ShapeEllipseItem(double X, double Y, double RadiusX, double RadiusY, double Angle, Fill Fill, string Attributes) : SceneItem;

/// <summary>
/// The definitions (gradients, patterns, filters, clip paths) that the HTML of the items around it names by their ids;
/// other backends draw from the items themselves.
/// </summary>
public sealed record DefsItem(string Markup) : SceneItem;

/// <summary>
/// A rectangle of a drawing in CSS pixels (a highlight, an underline), filled and outlined as DrawingML gives it; in HTML
/// a rect element with the attributes of its paint (none where it takes its fill from a group around it).
/// </summary>
public sealed record ShapeRectItem(double X, double Y, double Width, double Height, Fill Fill, Outline? Outline, string Attributes) : SceneItem
{
    /// <summary>Whether it stands at the origin, which HTML then leaves out (a slide table's background).</summary>
    public bool AtOrigin { get; init; }
}

/// <summary>
/// A slide's background in CSS pixels: its fill over Width by Height; in HTML an SVG of class bg with the fill's
/// definitions and paint attributes.
/// </summary>
public sealed record BackgroundItem(double Width, double Height, Fill Fill, string Defs, string Paint) : SceneItem;

/// <summary>A picture stretched over a rectangle in CSS pixels (a picture in text); Source is a URI.</summary>
public sealed record ImageItem(string Source, double X, double Y, double Width, double Height) : SceneItem;

/// <summary>
/// A picture filling a path of a shape, stretched over a rectangle in CSS pixels (its crop and fill rectangle
/// applied) and cut at the path (SVG path data); in HTML an image clipped by the shape's definition named Clip, with
/// the attribute of its filter (recoloring, its effects) where it has one. Source is the picture as a URI.
/// </summary>
public sealed record PictureItem(string Source, double X, double Y, double Width, double Height, string ClipPath, string Clip, string Filter) : SceneItem;

/// <summary>
/// Office's sign over a picture of Width × Height CSS pixels that it cannot show: Word's with its Message (on white in a
/// 0.14 pt black frame, the broken-picture icon and the message in 1.2 pt Tahoma), PowerPoint's without one (a grey
/// frame, the icon and "The picture can't be displayed." in 11-pixel Tahoma); in HTML a div over the picture's box.
/// </summary>
public sealed record MissingPictureItem(double Width, double Height, string? Message) : SceneItem;

/// <summary>Items drawn transformed (in CSS pixels): a scaled, skewed or flipped shadow's copy of its shape, turned text.</summary>
public sealed record TransformItem(Matrix Transform, List<SceneItem> Items) : SceneItem, IHoldsItems
{
    /// <summary>The transform as HTML writes it, where that is not its matrix (an SVG translate or rotate).</summary>
    public string? Css { get; init; }

    /// <summary>Items turned clockwise by degrees about (x, y), written as an SVG rotate.</summary>
    public static TransformItem Rotation(double degrees, double x, double y, List<SceneItem> items) =>
        new(Matrix.Translate(x, y).Then(Matrix.Rotate(degrees)).Then(Matrix.Translate(-x, -y)), items)
        {
            Css = $"rotate({Units.Css(degrees)} {Units.Css(x)} {Units.Css(y)})",
        };
}

/// <summary>
/// Items of an SVG group with attributes not broken down yet (a clip to the front face inside a bevel, a 3D face's
/// light, the hooks by which animations build a shape's background apart from its text and text by paragraph, word or
/// letter, an isolated blend, the fill a run's text and decorations share).
/// </summary>
public sealed record SvgGroupItem(string Attributes, List<SceneItem> Items) : SceneItem, IHoldsItems
{
    /// <summary>The transform its attributes hold (a block of text flipped back), for other backends.</summary>
    public Matrix? Transform { get; init; }

    /// <summary>The effects the filter among its attributes draws (a run of text's), for other backends.</summary>
    public EffectModel? Effects { get; init; }

    /// <summary>
    /// The color matrix the filter among its attributes applies (a 3D face's light): 4 rows of 5, the last column added
    /// in fractions of full intensity, for other backends.
    /// </summary>
    public double[]? ColorMatrix { get; init; }
}

/// <summary>Items inside markup not broken down yet (a shape's hyperlink, the hidden SVG that holds definitions).</summary>
public sealed record WrappedItem(string Before, List<SceneItem> Items, string After) : SceneItem, IHoldsItems;

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

    /// <summary>Whether the letters are outlined in the color (w:outline), filled white over a shadow, else hollow.</summary>
    public bool Hollow { get; init; }

    public bool Shadow { get; init; }
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

/// <summary>
/// Characters drawn invisibly in points, stretched over Width from X on the baseline Y at the font size Size, for
/// selecting and searching them (an equation's).
/// </summary>
public sealed record HiddenSpanItem(string Text, double X, double Y, double Size, double Width) : SceneItem;

/// <summary>
/// A glyph of an equation in points: the glyph Glyph of Font at Size, its origin at (X, Y) on its baseline, in its color.
/// HTML sets it as text in its font where it stands for a character (Character; a script form is the font's ssty
/// alternate of ScriptLevel), else as the outline the page's definitions hold under Id.
/// </summary>
public sealed record MathGlyphItem(FontData Font, uint Glyph, double Size, double X, double Y, RgbColor Color) : SceneItem
{
    public string? Character { get; init; }
    public int ScriptLevel { get; init; }
    public string? Id { get; init; }
}

/// <summary>
/// A run of glyphs drawn from their outlines in CSS pixels (slide text): from its origin (X, Y) on the baseline the glyphs
/// at their places in font units, Scale pixels a unit, filled in Color, or with Paint over PaintBox (a gradient, a pattern
/// or a picture spanning the line), outlined where it has an outline. In HTML a group of uses of the glyph outlines the
/// page's definitions hold, with the attributes of its fill (none where a group around it has them) and of its outline
/// in font units; a painted run is a rectangle of its paint masked by the glyphs (the mask Mask among the definitions).
/// </summary>
public sealed record GlyphRunItem(FontData Font, double X, double Y, double Scale, List<RunGlyph> Glyphs, RgbColor Color) : SceneItem
{
    public string FillAttributes { get; init; } = "";
    public Outline? Outline { get; init; }
    public string StrokeAttributes { get; init; } = "";
    public Fill? Paint { get; init; }
    public (double X, double Y, double Width, double Height) PaintBox { get; init; }
    public string PaintAttributes { get; init; } = "";
    public string? Mask { get; init; }
}

/// <summary>
/// A glyph of a GlyphRunItem: its outline's id among the page's definitions, the font's glyph and its place in font units
/// from the run's origin; Stretch the factor of equalized character height (its ink from the baseline to the font's
/// ascent, the place then where its outline's origin lands).
/// </summary>
public readonly record struct RunGlyph(string Id, uint Glyph, double X, double Y, double? Stretch = null);

/// <summary>
/// Slide text set by the backend in CSS pixels: its characters on the baseline Y at their places (Positions, one per
/// UTF-16 unit; right-to-left text whole from its right end, Positions[0] + Width), in Font at Size, in its color, with a
/// plain underline or strikethrough where it has one; Hidden where outlines draw it (GlyphRunItem) and it stays for
/// selecting and searching only. In HTML an SVG text element, its x attribute Placement, its paint, decoration, style and
/// filter Attributes.
/// </summary>
public sealed record SvgTextItem(string Text, double[] Positions, double Y, FontData Font, double Size, RgbColor Color) : SceneItem
{
    public bool Hidden { get; init; }
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public bool RightToLeft { get; init; }
    public double Width { get; init; }
    public bool Kerning { get; init; } = true;
    public bool Underline { get; init; }
    public bool Strike { get; init; }
    public EffectModel? Effects { get; init; }
    public string Placement { get; init; } = "";
    public string Attributes { get; init; } = "";
}

/// <summary>A short text in its font and color ending at the point X on the baseline Y: a line number.</summary>
public sealed record LabelItem(string Text, double X, double Y, FontData Font, double Size, RgbColor Color, bool Bold, bool Italic) : SceneItem;

/// <summary>Items drawn from a point, turned a quarter counterclockwise about it: text set across a vertical line.</summary>
public sealed record AcrossItem(double X, double Y, List<SceneItem> Items) : SceneItem, IHoldsItems;

/// <summary>Items drawn but neither selected nor read as text: the copies of a text that emboss, imprint and shadow lay behind it.</summary>
public sealed record DecorationItem(List<SceneItem> Items) : SceneItem, IHoldsItems;

/// <summary>
/// Items drawn with the effects of a DrawingML effect list (blur, soft edges, inner shadows, glow and shadow, as Effects
/// gives them; a shadow copy's, see SvgEffects.Shadow, only its shadow), in HTML through the filter of the page's
/// definitions named Filter.
/// </summary>
public sealed record FilterItem(string Filter, EffectModel Effects, List<SceneItem> Items) : SceneItem, IHoldsItems;

/// <summary>
/// Items drawn first with the outer effects of a DrawingML effect list (glow and shadow, see FilterItem), then as they
/// are over them: in HTML a copy of the group named Id through the filter Filter, which so stands in the page once
/// (text's glow and shadow).
/// </summary>
public sealed record FilterCopyItem(string Id, string Filter, EffectModel Effects, List<SceneItem> Items) : SceneItem, IHoldsItems
{
    /// <summary>Whether the copy refers to the one item itself, which carries the id (see ShapePathItem.Lead), not to a group.</summary>
    public bool Bare { get; init; }
}

/// <summary>
/// Items with their reflection (a:reflection): a copy of them turned, scaled and skewed by the reflection's transform (in
/// pixels), faded and blurred, in HTML by the page's definitions numbered with the reflection (see SvgEffects.ReflectionOf).
/// </summary>
public sealed record ReflectionItem(ReflectionModel Reflection, List<SceneItem> Items) : SceneItem, IHoldsItems;
