namespace FreeSlate;

/// <summary>
/// Character formatting to give text (catalog: run.basic, run.decoration), as Word's and PowerPoint's buttons give it:
/// each property given is set as direct formatting of the text's runs (w:rPr in a Word document, a:rPr on a slide), the
/// others stay as they are.
/// </summary>
public sealed record TextFormatting
{
    /// <summary>
    /// Bold and italic (w:b, w:i, with their complex-script twins w:bCs, w:iCs, which Word's buttons set too; a:rPr b,
    /// i).
    /// </summary>
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }

    /// <summary>
    /// The underline by Word's names (w:u): single, double, thick, dotted, dash, wave, words and others, or none; on a
    /// slide as DrawingML's (a:rPr u: sng, dbl, heavy, dotted, dash, wavy, words …), whose names it takes too.
    /// </summary>
    public string? Underline { get; init; }

    /// <summary>The size in points (w:sz and w:szCs, in half points; a:rPr sz, in hundredths).</summary>
    public double? Size { get; init; }

    /// <summary>
    /// The font: in a Word document for all scripts (w:rFonts ascii, hAnsi, eastAsia and cs, the theme's fonts taken
    /// away); on a slide for Latin text (a:latin), and for East Asian and complex-script text where a run has such (a:ea,
    /// a:cs).
    /// </summary>
    public string? Font { get; init; }

    /// <summary>The color as RRGGBB, or auto (w:color, a theme color taken away; a:rPr's fill, auto: none of its own).</summary>
    public string? Color { get; init; }

    /// <summary>
    /// The highlight by Word's names (w:highlight): yellow, green, cyan, magenta, blue, red, darkBlue and others, or none;
    /// on a slide in the color of that name, or as RRGGBB (a:highlight).
    /// </summary>
    public string? Highlight { get; init; }

    /// <summary>
    /// The character style by its id (w:rStyle), "" for none: what the text means, where the style says how it looks
    /// (see IEditableDocument.DefineStyles). Slides have no character styles; there it changes nothing.
    /// </summary>
    public string? Style { get; init; }
}

/// <summary>
/// Paragraph formatting to give a paragraph (catalog: paragraph.basic, paragraph.spacing): each property given is set
/// as the paragraph's direct formatting (w:pPr in a Word document, a:pPr on a slide), the others stay as they are.
/// </summary>
public sealed record ParagraphFormatting
{
    /// <summary>The paragraph style, by its id (w:pStyle), "" for the default one (none of its own); slides have none.</summary>
    public string? Style { get; init; }

    /// <summary>The alignment (w:jc; a:pPr algn): left, center, right, both or distribute.</summary>
    public string? Alignment { get; init; }

    /// <summary>
    /// The indents in points (w:ind; a:pPr marL, marR, indent): left, right, and the first line's from the left
    /// (negative: hanging).
    /// </summary>
    public double? Left { get; init; }
    public double? Right { get; init; }
    public double? FirstLine { get; init; }

    /// <summary>
    /// The space before and after in points, and the line spacing in lines (w:spacing, the rule auto; a:spcBef and
    /// a:spcAft in points, a:lnSpc in percent).
    /// </summary>
    public double? Before { get; init; }
    public double? After { get; init; }
    public double? Lines { get; init; }

    /// <summary>The lines at least so high, in points (w:spacing, the rule atLeast); Word documents only.</summary>
    public double? LinesAtLeast { get; init; }
}

/// <summary>
/// Page setup to give a section of a Word document (catalog: section.page, section.columns), as Word's Page Setup gives
/// it: each property given is set in the section's w:sectPr, the others stay as they are; lengths in points.
/// </summary>
public sealed record SectionFormatting
{
    /// <summary>
    /// The page's size (w:pgSz w:w, w:h) and its orientation (w:orient): portrait or landscape, the width and the height
    /// swapped where they do not fit it, as Word turns the page.
    /// </summary>
    public double? PageWidth { get; init; }
    public double? PageHeight { get; init; }
    public string? Orientation { get; init; }

    /// <summary>
    /// The margins (w:pgMar), the header's and the footer's distance from the page's edge, and the gutter; a top or bottom
    /// margin negative where it is exact (the text keeps it however high the header or footer is), as Word gives it.
    /// </summary>
    public double? Top { get; init; }
    public double? Bottom { get; init; }
    public double? Left { get; init; }
    public double? Right { get; init; }
    public double? Header { get; init; }
    public double? Footer { get; init; }
    public double? Gutter { get; init; }

    /// <summary>Columns of equal width (w:cols w:num and w:space), any widths of their own taken away.</summary>
    public int? Columns { get; init; }
    public double? ColumnSpace { get; init; }

    /// <summary>Where the section begins (w:type): nextPage, continuous, evenPage, oddPage or nextColumn.</summary>
    public string? Start { get; init; }

    /// <summary>Whether the section's first page has a header and footer of its own (w:titlePg).</summary>
    public bool? TitlePage { get; init; }

    /// <summary>How the text stands on the page between the top and the bottom margin (w:vAlign): top, center, both or bottom.</summary>
    public string? VerticalAlign { get; init; }

    /// <summary>
    /// The page numbering (w:pgNumType): the number of the section's first page, negative to go on from the section before;
    /// and the numbers' format (decimal, upperRoman, lowerRoman, upperLetter, lowerLetter …).
    /// </summary>
    public int? PageNumberStart { get; init; }
    public string? PageNumberFormat { get; init; }
}

/// <summary>
/// How a drawing stands among the text of a Word document (catalog: drawing.inline, drawing.anchor), as Word's Layout Options and its
/// Position gallery give it, and how a picture shows, as Word's Picture Format gives it (catalog: picture.basic, picture.effects): each
/// property given is set in its wp:inline or wp:anchor, the others stay as they are; lengths in points. A picture with its caption (see
/// WordDocument.AddCaption) shows as its picture does.
/// </summary>
public sealed record DrawingFormatting
{
    /// <summary>Its size (wp:extent, the picture's a:ext with it); given one alone, the other keeps the proportions.</summary>
    public double? Width { get; init; }
    public double? Height { get; init; }

    /// <summary>
    /// How it stands among the text: "inline" (in the line, one of its characters: wp:inline), and floating (wp:anchor) "square" (the
    /// text beside it, see Side), "topAndBottom" (the text above and below it, none beside), "behind" and "front" (the text over it or
    /// under it, not going round it). A drawing made floating stays where it stood, the text then going round it.
    /// </summary>
    public string? Wrap { get; init; }

    /// <summary>Where the text goes beside a drawing it wraps square round (wp:wrapSquare wrapText): bothSides, left, right or largest.</summary>
    public string? Side { get; init; }

    /// <summary>
    /// Where a floating drawing stands across (wp:positionH): from what (relativeFrom: margin, page, column, character, leftMargin,
    /// rightMargin, insideMargin, outsideMargin), and either aligned there (left, center, right, inside, outside) or at an offset from it.
    /// </summary>
    public string? HorizontalFrom { get; init; }
    public string? HorizontalAlign { get; init; }
    public double? HorizontalOffset { get; init; }

    /// <summary>
    /// Where it stands down (wp:positionV): from what (margin, page, paragraph, line, topMargin, bottomMargin, insideMargin,
    /// outsideMargin), and either aligned there (top, center, bottom, inside, outside) or at an offset from it.
    /// </summary>
    public string? VerticalFrom { get; init; }
    public string? VerticalAlign { get; init; }
    public double? VerticalOffset { get; init; }

    /// <summary>
    /// What the drawing shows, for those who cannot see it (its alternative text: wp:docPr descr, a picture's pic:cNvPr descr with
    /// it), which screen readers read; "" takes it away.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>Whether the drawing is decoration only, which screen readers pass over (Word's "Mark as decorative", adec:decorative in wp:docPr).</summary>
    public bool? Decorative { get; init; }

    /// <summary>
    /// The part of a picture it shows (a:srcRect of its pic:blipFill, Word's Crop): read as it is; given, it is set as it is, the size
    /// as given (see WordDocument.CropPicture, which keeps the picture's scale).
    /// </summary>
    public PictureCrop? Crop { get; init; }

    /// <summary>The shape a picture shows in (a:prstGeom of its pic:spPr, Word's Crop to Shape): "rect", "ellipse", "roundRect" …</summary>
    public string? Shape { get; init; }

    /// <summary>
    /// How a picture's colors show (Word's Recolor, an effect of its a:blip): "none", "grayscale" (a:grayscl), "blackWhite" (a:biLevel at
    /// half), "washout" (a:lum, as bright as Word washes a picture out to stand behind text); read "other" for another recoloring.
    /// </summary>
    public string? Recolor { get; init; }

    /// <summary>
    /// The line round a picture (a:ln of its pic:spPr, Word's Picture Border): its width in points, 0 for none, and its color as RRGGBB;
    /// the effect extents round a picture by itself as Word gives them for it.
    /// </summary>
    public double? Outline { get; init; }
    public string? OutlineColor { get; init; }

    /// <summary>
    /// How far a picture is turned in its plane, clockwise in degrees from 0 up to 360 (a:xfrm rot of its pic:spPr, Word's Rotate and its
    /// handle for turning); the effect extents round a picture by itself as Word gives them for it.
    /// </summary>
    public double? Rotation { get; init; }

    /// <summary>Whether a picture is mirrored across, left for right, and down, top for bottom (a:xfrm flipH, flipV, Word's Flip Horizontal and Vertical).</summary>
    public bool? FlipHorizontal { get; init; }
    public bool? FlipVertical { get; init; }

    /// <summary>
    /// How a picture is tilted in space (a:scene3d of its pic:spPr, Word's 3-D Rotation, see PictureTilt); PictureTilt.Flat for not at
    /// all. The effect extents round a picture by itself as Word gives them for it.
    /// </summary>
    public PictureTilt? Tilt { get; init; }
}

/// <summary>
/// A picture tilted in space as Word's 3-D Rotation tilts it (a:scene3d/a:camera, catalog: effect.scene3d): the camera, one of
/// DrawingML's presets ("orthographicFront" straight on, "perspectiveFront" the same in perspective, "isometricLeftDown" and its kin
/// askew), and where turns are given (degrees, Word's X, Y and Z Rotation: X about the vertical axis, Y about the horizontal one, Z in
/// the picture's plane; a:rot −lon, lat and −rev) they stand in place of the preset's; Fov, a perspective's field of view (degrees).
/// </summary>
public readonly record struct PictureTilt(string Camera, double? X = null, double? Y = null, double? Z = null, double? Fov = null)
{
    /// <summary>Not tilted: no scene of its own.</summary>
    public static readonly PictureTilt Flat = new("orthographicFront");

    /// <summary>Whether the tilt leaves the picture as it is: straight on, not turned.</summary>
    public bool IsFlat => Camera == "orthographicFront" && Straight(X) && Straight(Y) && Straight(Z);

    /// <summary>The turns it gives the picture (degrees, as X, Y and Z): those given, else its preset's (see Drawing.Camera).</summary>
    public (double X, double Y, double Z) Angles
    {
        get
        {
            var (x, y, z) = Drawing.Camera.PresetAngles(Camera);
            return (X ?? x, Y ?? y, Z ?? z);
        }
    }

    static bool Straight(double? degrees) => Math.Abs(Math.IEEERemainder(degrees ?? 0, 360)) < 1e-9;
}

/// <summary>
/// The part of a picture a drawing shows (a:srcRect l, t, r, b): how much of the picture is cut away at each side, as shares of its whole
/// width (left, right) and height (top, bottom); below 0 the picture leaves room there.
/// </summary>
public readonly record struct PictureCrop(double Left, double Top, double Right, double Bottom)
{
    /// <summary>The whole picture.</summary>
    public static readonly PictureCrop None = new(0, 0, 0, 0);

    /// <summary>The shares of the picture's width and height the part shows.</summary>
    public double Wide => 1 - Left - Right;
    public double High => 1 - Top - Bottom;
}
