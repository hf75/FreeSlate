using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate.Editor.Writing;

/// <summary>
/// Where a picture stands among the text, as the gesture ring offers it (see EditorView.Join): its name's key among the texts, its
/// icon (path data on a box of 20 × 16: the picture a frame, the text lines), and how the drawing then stands (see DrawingFormatting).
/// A picture keeps where it stands in the direction a placement does not set.
/// </summary>
public sealed record Placement(string NameKey, string Icon, DrawingFormatting Format)
{
    /// <summary>In the line, one of its characters: a frame on the middle one of three lines.</summary>
    public static readonly Placement Inline = new("PictureInline",
        Drawn(Bar(0, 1, 20), Bar(0, 10.2, 5), Frame(6.5, 4.4, 7, 7), Bar(15, 10.2, 5), Bar(0, 13.8, 20)),
        new() { Wrap = "inline" });

    /// <summary>At the column's left, the text beside it on the right: a frame left of short lines, two lines below it.</summary>
    public static readonly Placement Left = new("PictureLeft",
        Drawn(Frame(0, 1, 8, 8.2), Bar(10, 1, 10), Bar(10, 4.5, 10), Bar(10, 8, 10), Bar(0, 11.4, 20), Bar(0, 14.8, 12)),
        new() { Wrap = "square", HorizontalFrom = "column", HorizontalAlign = "left" });

    /// <summary>At the column's right, the text beside it on the left.</summary>
    public static readonly Placement Right = new("PictureRight",
        Drawn(Frame(12, 1, 8, 8.2), Bar(0, 1, 10), Bar(0, 4.5, 10), Bar(0, 8, 10), Bar(0, 11.4, 20), Bar(0, 14.8, 12)),
        new() { Wrap = "square", HorizontalFrom = "column", HorizontalAlign = "right" });

    /// <summary>On its own, in the middle of the column, the text above and below it: a frame between two lines.</summary>
    public static readonly Placement Own = new("PictureOwn",
        Drawn(Bar(0, 0.4, 20), Frame(5, 3.4, 10, 9.2), Bar(0, 14.4, 20)),
        new() { Wrap = "topAndBottom", HorizontalFrom = "column", HorizontalAlign = "center" });

    /// <summary>Behind the text, which goes over it: four lines, the middle two across a frame.</summary>
    public static readonly Placement Behind = new("PictureBehind",
        Drawn(Bar(0, 0.4, 20), Bar(0, 5, 20), Frame(4.5, 3.2, 11, 9.4), Bar(0, 9.6, 20), Bar(0, 14.2, 20)),
        new() { Wrap = "behind" });

    /// <summary>In front of the text, which goes under it: four lines, the middle two hidden by a frame and going on beside it.</summary>
    public static readonly Placement Front = new("PictureFront",
        Drawn(Bar(0, 0.4, 20), Bar(0, 5, 3.3), Bar(16.7, 5, 3.3), Frame(4.5, 3.2, 11, 9.4), Bar(0, 9.6, 3.3), Bar(16.7, 9.6, 3.3), Bar(0, 14.2, 20)),
        new() { Wrap = "front" });

    /// <summary>The icon of what is done with the picture itself (its caption, its description, its size): a picture, a hill and the sun in a frame.</summary>
    public static readonly string PictureIcon = Drawn(Frame(0, 1, 20, 14), "M3.2,12.6 L8,7.4 L11.1,10.6 L13.5,8.3 L16.8,12.6 Z", Circle(14.6, 5.4, 1.7));

    /// <summary>The icon of what the clipboard holds: a clipboard, its clip at the top, a picture on it.</summary>
    public static readonly string ClipboardIcon = Drawn(Frame(3, 1.6, 14, 14.4), Block(7, 0, 6, 3.2),
        "M5.6,13.6 L9,9.6 L11.2,12 L12.6,10.6 L14.4,13.6 Z");

    /// <summary>The icon of a table: a frame of three columns and three rows.</summary>
    public static readonly string TableIcon = Drawn(Frame(0, 1, 20, 14), Bar(0, 5.4, 20), Bar(0, 9.6, 20), Block(6.3, 1, Weight, 14), Block(12.5, 1, Weight, 14));

    /// <summary>
    /// The icons of a new table's size stepped: a table of columns with a plus or a minus right of it, one of rows with a plus or a minus
    /// left of it (the rows on the left of the ring, the columns on its right).
    /// </summary>
    public static readonly string ColumnMoreIcon = Drawn(Frame(0, 1, 11.6, 14), Block(3.5, 1, Weight, 14), Block(6.9, 1, Weight, 14), Bar(13.6, 7.4, 6.4), Block(16.2, 4.8, Weight, 6.4));
    public static readonly string ColumnFewerIcon = Drawn(Frame(0, 1, 11.6, 14), Block(3.5, 1, Weight, 14), Block(6.9, 1, Weight, 14), Bar(13.6, 7.4, 6.4));
    public static readonly string RowMoreIcon = Drawn(Bar(0, 7.4, 6.4), Block(2.6, 4.8, Weight, 6.4), Frame(8.4, 1, 11.6, 14), Bar(8.4, 5.4, 11.6), Bar(8.4, 9.4, 11.6));
    public static readonly string RowFewerIcon = Drawn(Bar(0, 7.4, 6.4), Frame(8.4, 1, 11.6, 14), Bar(8.4, 5.4, 11.6), Bar(8.4, 9.4, 11.6));

    /// <summary>The icon of a table's row: the table, its middle row filled.</summary>
    public static readonly string RowIcon = Drawn(Frame(0, 1, 20, 14), Block(0, 5.4, 20, 4.4), Block(6.3, 1, Weight, 14), Block(12.5, 1, Weight, 14));

    /// <summary>The icon of a table's column: the table, its middle column filled.</summary>
    public static readonly string ColumnIcon = Drawn(Frame(0, 1, 20, 14), Bar(0, 5.4, 20), Bar(0, 9.6, 20), Block(6.3, 1, 7.4, 14));

    /// <summary>The icon of shaping a table by hand: a pen, its tip down left.</summary>
    public static readonly string PenIcon = Drawn(Outline((1.4, 15.4), (3.4, 9.8), (13.2, 0), (17.2, 4), (7, 13.8)), "M1.4,15.4 L3.4,9.8 L7,13.8 Z", Stroke(10.6, 2.6, 14.6, 6.6));

    /// <summary>The icons of a row put in above or below: the new row filled, the table's rows a frame beside it.</summary>
    public static readonly string RowAboveIcon = Drawn(Block(0, 0, 20, 4.2), Frame(0, 6, 20, 10), Bar(0, 10.4, 20));
    public static readonly string RowBelowIcon = Drawn(Frame(0, 0, 20, 10), Bar(0, 4.4, 20), Block(0, 11.8, 20, 4.2));

    /// <summary>The icons of a column put in left or right: the new column filled, the table's columns a frame beside it.</summary>
    public static readonly string ColumnLeftIcon = Drawn(Block(0, 1, 4.6, 14), Frame(6.6, 1, 13.4, 14), Block(12.7, 1, Weight, 14));
    public static readonly string ColumnRightIcon = Drawn(Frame(0, 1, 13.4, 14), Block(6.1, 1, Weight, 14), Block(15.4, 1, 4.6, 14));

    /// <summary>The icon of a row's cells merged: a row of one cell between two rows of three.</summary>
    public static readonly string RowMergeIcon = Drawn(Frame(0, 1, 20, 14), Bar(0, 5.4, 20), Bar(0, 9.6, 20), Block(6.3, 1, Weight, 4.4), Block(12.5, 1, Weight, 4.4),
        Block(6.3, 10.8, Weight, 4.2), Block(12.5, 10.8, Weight, 4.2));

    /// <summary>The icon of rows turned into columns: a frame cut by its diagonal, a row's mark above it and a column's below.</summary>
    public static readonly string TransposeIcon = Drawn(Frame(0, 0, 20, 16), Stroke(0.6, 0.6, 19.4, 15.4), Bar(9, 3.6, 7.6), Block(3.4, 7.6, Weight, 6.2));

    /// <summary>The icon of columns made even: three columns of one width, a short line across each at the top.</summary>
    public static readonly string DistributeIcon = Drawn(Frame(0, 3, 20, 13), Block(6.3, 3, Weight, 13), Block(12.5, 3, Weight, 13), Bar(1.4, 0, 3.4), Bar(8.2, 0, 3.4), Bar(15.2, 0, 3.4));

    /// <summary>The icons of a table's looks: the tone's own (its inside lines broken), lines between rows, a book's rules, a frame with its head's line.</summary>
    public static readonly string LookToneIcon = Drawn(Frame(0, 1, 20, 14), Bar(1.6, 5.4, 3.6), Bar(8.2, 5.4, 3.6), Bar(14.8, 5.4, 3.6), Bar(1.6, 9.6, 3.6), Bar(8.2, 9.6, 3.6), Bar(14.8, 9.6, 3.6));
    public static readonly string LookRowsIcon = Drawn(Bar(0, 1, 20), Block(0, 4.8, 20, 1.6), Bar(0, 9.6, 20), Bar(0, 14.2, 20));
    public static readonly string LookBookIcon = Drawn(Block(0, 0.6, 20, 2.2), Bar(0, 5.6, 20), Block(0, 13.2, 20, 2.2));
    public static readonly string LookFrameIcon = Drawn(Frame(0, 1, 20, 14), Bar(0, 5.4, 20));

    /// <summary>The icon of a table's look: a brush's tip over a table's frame.</summary>
    public static readonly string LookIcon = Drawn(Frame(0, 6, 20, 10), Bar(0, 10.6, 20), Outline((9, 6.6), (12.6, 0.4), (16.2, 2.4), (12.6, 8.6)));

    /// <summary>The icons of a head row and of none: the table, its first row filled, or empty and the frame broken above.</summary>
    public static readonly string HeadIcon = Drawn(Frame(0, 1, 20, 14), Block(0, 1, 20, 4.8), Bar(0, 9.6, 20), Block(6.3, 5, Weight, 10), Block(12.5, 5, Weight, 10));
    public static readonly string HeadRemoveIcon = Drawn(Frame(0, 5.4, 20, 9.6), Bar(0, 9.6, 20), Block(6.3, 5.4, Weight, 9.6), Block(12.5, 5.4, Weight, 9.6),
        Bar(0, 1, 4), Bar(8, 1, 4), Bar(16, 1, 4));

    /// <summary>The icons of a cell's colour (the table, its middle cell filled) and of none (a cell crossed out).</summary>
    public static readonly string FillIcon = Drawn(Frame(0, 1, 20, 14), Bar(0, 5.4, 20), Bar(0, 9.6, 20), Block(6.3, 1, Weight, 14), Block(12.5, 1, Weight, 14), Block(6.3, 5.4, 7.4, 5.4));
    public static readonly string FillNoneIcon = Drawn(Frame(1, 1, 18, 14), Stroke(2.4, 13.6, 17.6, 2.4));

    /// <summary>The icon of what goes into a cell: a cell with a plus in it.</summary>
    public static readonly string CellInsertIcon = Drawn(Frame(0, 1, 20, 14), Block(9.4, 4.4, Weight, 7.2), Block(6.4, 7.4, 7.2, Weight));

    /// <summary>The icon of a cell's lines: a cell, its left and bottom sides strong.</summary>
    public static readonly string CellLinesIcon = Drawn(Frame(0, 1, 20, 14), Block(0, 1, 2.6, 14), Block(0, 12.4, 20, 2.6));

    /// <summary>The icons of a cell split, into two or three side by side, or one below the other.</summary>
    public static readonly string SplitColumns2Icon = Drawn(Frame(0, 1, 20, 14), Block(9.4, 1, Weight, 14));
    public static readonly string SplitColumns3Icon = Drawn(Frame(0, 1, 20, 14), Block(6.3, 1, Weight, 14), Block(12.5, 1, Weight, 14));
    public static readonly string SplitRows2Icon = Drawn(Frame(0, 1, 20, 14), Bar(0, 7.4, 20));
    public static readonly string SplitRows3Icon = Drawn(Frame(0, 1, 20, 14), Bar(0, 5.4, 20), Bar(0, 9.6, 20));

    /// <summary>
    /// The icons of where text stands in a cell: a cell, two lines of text at its top, in its middle or at its bottom, and at its left, in
    /// its middle or at its right (see EditSession.PlaceText).
    /// </summary>
    public static string CellPlaceIcon(string vertical, string horizontal) => CellPlaceIcon(vertical, horizontal, "across");

    /// <summary>
    /// The icons of where text stands in a cell as the page shows it (see CellPlaceIcon), the text running across, up the page (btLr: its lines
    /// upright, the first at the left, ending together at the bottom where they begin there) or down it (tbRl: the first at the right).
    /// </summary>
    public static string CellPlaceIcon(string vertical, string horizontal, string way)
    {
        if (way is not ("btLr" or "tbRl"))
        {
            var x = horizontal switch { "left" => 2.8, "center" => 6, _ => 9.2 };
            var y = vertical switch { "top" => 3, "center" => 5.8, _ => 8.6 };
            var shorter = horizontal switch { "left" => x, "center" => x + 1.4, _ => x + 2.8 };
            return Drawn(Frame(0, 0, 20, 16), Bar(x, y, 8), Bar(shorter, y + 3.2, 5.2));
        }
        var left = horizontal switch { "left" => 3.0, "center" => 7.8, _ => 12.6 };
        var (top, shortTop) = vertical switch { "top" => (2.6, 2.6), "center" => (4.0, 5.4), _ => (5.4, 8.2) };
        var (first, second) = way == "btLr" ? (left, left + 3.2) : (left + 3.2, left);
        return Drawn(Frame(0, 0, 20, 16), Block(first, top, Weight, 8), Block(second, shortTop, Weight, 5.2));
    }

    /// <summary>The icon of where text stands in cells: the text in the middle of a cell.</summary>
    public static readonly string CellPlaceFieldIcon = CellPlaceIcon("center", "center");

    /// <summary>
    /// The icons of the ways text runs in a cell: across (two lines, the first ending in a head to the right), up the page (two upright lines,
    /// the first at the left with a head up) and down it (the first at the right with a head down).
    /// </summary>
    public static string CellWayIcon(string way) => way switch
    {
        "btLr" => Drawn(Block(7, 5.2, Weight, 9.6), Block(11, 9, Weight, 5.8), "M4.4,5.6 L7.6,1 L10.8,5.6 Z"),
        "tbRl" => Drawn(Block(11.8, 1.2, Weight, 9.6), Block(7.8, 1.2, Weight, 5.8), "M9.2,10.4 L12.4,15 L15.6,10.4 Z"),
        _ => Drawn(Bar(1.2, 6.4, 11.6), Bar(1.2, 10.4, 7.6), "M13.2,3.8 L17.8,7 L13.2,10.2 Z"),
    };

    /// <summary>The icon of cells merged: a cell, two heads pointing to its middle.</summary>
    public static readonly string MergeIcon = Drawn(Frame(0, 1, 20, 14), "M3.4,4.8 L8.2,8 L3.4,11.2 Z", "M16.6,4.8 L16.6,11.2 L11.8,8 Z");

    /// <summary>The icons of where a table stands between the text's edges: the edges, the table between them at the left, in the middle, at the right, or across.</summary>
    public static readonly string AlignLeftIcon = Aligned(2.4, 9);
    public static readonly string AlignCenterIcon = Aligned(5.5, 9);
    public static readonly string AlignRightIcon = Aligned(8.6, 9);
    public static readonly string WidenIcon = Aligned(2.4, 15.2);

    /// <summary>The icons of a table floating beside the text: the table at the right or at the left, lines of text beside it.</summary>
    public static readonly string FloatRightIcon = Drawn(Frame(10, 2, 10, 12), Bar(10, 7.4, 10), Bar(0, 2.6, 7.6), Bar(0, 5.8, 7.6), Bar(0, 9, 7.6), Bar(0, 12.2, 7.6));
    public static readonly string FloatLeftIcon = Drawn(Frame(0, 2, 10, 12), Bar(0, 7.4, 10), Bar(12.4, 2.6, 7.6), Bar(12.4, 5.8, 7.6), Bar(12.4, 9, 7.6), Bar(12.4, 12.2, 7.6));

    static string Aligned(double x, double width) => Drawn(Block(0, 0, Weight, 16), Block(18.8, 0, Weight, 16), Frame(x, 4, width, 8), Bar(x, 7.4, width));

    /// <summary>
    /// The icons of a table's lines: taken away (a line crossed out), fine, strong, double, dotted, dashed, and a stretch of a line alone
    /// (a line, two ticks across it round its middle).
    /// </summary>
    public static readonly string LineHideIcon = Drawn(Bar(0, 7.4, 20), Stroke(6.4, 14, 13.6, 2));
    public static readonly string LineFineIcon = Drawn(Bar(0, 7.4, 20));
    public static readonly string LineStrongIcon = Drawn(Block(0, 6.4, 20, 3.2));
    public static readonly string LineDoubleIcon = Drawn(Bar(0, 5.6, 20), Bar(0, 9.2, 20));
    public static readonly string LineDottedIcon = Drawn(Circle(1.2, 8, 1.2), Circle(5.6, 8, 1.2), Circle(10, 8, 1.2), Circle(14.4, 8, 1.2), Circle(18.8, 8, 1.2));
    public static readonly string LineDashedIcon = Drawn(Bar(0, 7.4, 4.4), Bar(7.8, 7.4, 4.4), Bar(15.6, 7.4, 4.4));
    public static readonly string LinePieceIcon = Drawn(Bar(0, 7.4, 20), Block(5.6, 3.8, Weight, 8.4), Block(13.2, 3.8, Weight, 8.4));

    /// <summary>The icons of lines of cells selected: four cells, the lines named strong, the others dotted.</summary>
    public static readonly string CellsTopIcon = Cells(CellLines.Top);
    public static readonly string CellsOutsideIcon = Cells(CellLines.Outside);
    public static readonly string CellsRightIcon = Cells(CellLines.Right);
    public static readonly string CellsAllIcon = Cells(CellLines.All);
    public static readonly string CellsBottomIcon = Cells(CellLines.Bottom);
    public static readonly string CellsAcrossIcon = Cells(CellLines.Across);
    public static readonly string CellsLeftIcon = Cells(CellLines.Left);
    public static readonly string CellsUprightIcon = Cells(CellLines.Upright);

    static string Cells(CellLines strong)
    {
        var parts = new List<string>();
        foreach (var (side, across, at) in new[] { (CellLines.Top, true, 0.9), (CellLines.Across, true, 8.0), (CellLines.Bottom, true, 15.1),
            (CellLines.Left, false, 2.9), (CellLines.Upright, false, 10.0), (CellLines.Right, false, 17.1) })
        {
            if (strong.HasFlag(side))
                parts.Add(across ? Block(2, at - 0.9, 16, 1.8) : Block(at - 0.9, 0, 1.8, 16));
            else
            {
                for (var k = 0; k <= 5; k++)
                    parts.Add(across ? Block(2.4 + k * 3, at - 0.5, 1, 1) : Block(at - 0.5, 0.4 + k * 3, 1, 1));
            }
        }
        return Drawn([.. parts]);
    }

    /// <summary>The icon of a caption: a picture with two lines of text below it.</summary>
    public static readonly string CaptionIcon = Drawn(Frame(3, 0, 14, 9.8), Hills, Bar(3, 12.2, 14), Bar(3, 14.8, 9));

    /// <summary>The icon of taking a caption away: the picture with a cross where its lines were.</summary>
    public static readonly string CaptionRemoveIcon = Drawn(Frame(3, 0, 14, 9.8), Hills, Stroke(7.6, 11.6, 12.4, 16.2), Stroke(12.4, 11.6, 7.6, 16.2));

    /// <summary>The hills of a small picture (the caption's), as the picture's own icon has them.</summary>
    const string Hills = "M5.3,8 L8.6,4.6 L10.8,6.7 L12.4,5.1 L14.7,8 Z";

    /// <summary>The icon of a description, what screen readers read: a speech bubble with lines in it.</summary>
    public static readonly string DescriptionIcon = Drawn(Frame(1, 1, 18, 11.2), "M5,11.4 L5,15.6 L9.6,11.4 Z", Bar(4.2, 4.3, 11.6), Bar(4.2, 7.5, 7.6));

    /// <summary>The icon of the size a picture took when it was put in: a frame with the corners of its room around it.</summary>
    public static readonly string NaturalSizeIcon = Drawn(Frame(4.4, 3.6, 11.2, 8.8),
        Bar(0, 0, 4), Block(0, 0, Weight, 4), Bar(16, 0, 4), Block(20 - Weight, 0, Weight, 4),
        Bar(0, 16 - Weight, 4), Block(0, 12, Weight, 4), Bar(16, 16 - Weight, 4), Block(20 - Weight, 12, Weight, 4));

    /// <summary>The icon of cropping a picture: the two angles of a cropping frame, one round the other.</summary>
    public static readonly string CropIcon = Drawn(Block(5, 0, Weight, 12.2), Bar(5, 11, 13), Bar(2, 4, 12.6), Block(13.4, 4, Weight, 12));

    /// <summary>The icon of a picture's colors: a circle, its right half filled.</summary>
    public static readonly string RecolorIcon = Drawn(Ring(10, 8, 7.2), "M10,2 A6,6 0 0 1 10,14 Z");

    /// <summary>The icon of another picture in a picture's frame: two arrows turning round each other.</summary>
    public static readonly string ReplaceIcon = Drawn(Turn(10, 8, 5.4, 200, 320), Turn(10, 8, 5.4, 20, 140));

    /// <summary>The icon of a hairline round a picture: a picture in a frame of its own.</summary>
    public static readonly string FrameIcon = Drawn(Frame(0, 0, 20, 16), Frame(2.8, 2.8, 14.4, 10.4), FramedHills);

    /// <summary>The icon of taking the hairline round a picture away: the picture, its frame broken where it was.</summary>
    public static readonly string FrameRemoveIcon = Drawn(Frame(2.8, 2.8, 14.4, 10.4), FramedHills,
        Bar(0, 0, 3), Bar(8.5, 0, 3), Bar(17, 0, 3), Bar(0, 16 - Weight, 3), Bar(8.5, 16 - Weight, 3), Bar(17, 16 - Weight, 3),
        Block(0, 0, Weight, 3), Block(0, 6.5, Weight, 3), Block(0, 13, Weight, 3), Block(20 - Weight, 0, Weight, 3), Block(20 - Weight, 6.5, Weight, 3), Block(20 - Weight, 13, Weight, 3));

    /// <summary>The hills of the picture in a frame of its own (see FrameIcon).</summary>
    const string FramedHills = "M5,11.4 L8.2,8 L10.4,10.2 L12,8.6 L15,11.4 Z";

    /// <summary>The icon of a picture shown in a circle: a ring.</summary>
    public static readonly string CircleIcon = Drawn(Ring(10, 8, 7.4));

    /// <summary>The icon of a picture shown in its rectangle again: a frame.</summary>
    public static readonly string RectangleIcon = Drawn(Frame(1, 2, 18, 12));

    /// <summary>The icon of turning a picture: a small picture inside an arrow going round it clockwise.</summary>
    public static readonly string TurnIcon = Drawn(Frame(6.4, 5.4, 7.2, 5.2), Turn(10, 8, 6.4, 150, 410));

    /// <summary>The icon of a picture standing straight again: a picture standing level on a line.</summary>
    public static readonly string StraightIcon = Drawn(Frame(3, 0.4, 14, 11), "M5.4,9.6 L8.6,6.2 L10.8,8.4 L12.4,6.8 L14.6,9.6 Z", Bar(0, 14.6, 20));

    /// <summary>The icon of a quarter turn clockwise: a picture, and over its top an arrow going right and down.</summary>
    public static readonly string TurnRightIcon = Drawn(Frame(1, 6, 10.4, 9.6), Turn(10, 9.6, 7.4, 225, 345));

    /// <summary>The icon of a quarter turn the other way: a picture, and over its top an arrow going left and down.</summary>
    public static readonly string TurnLeftIcon = Drawn(Frame(8.6, 6, 10.4, 9.6), Turn(10, 9.6, 7.4, 315, 195, back: true));

    /// <summary>The icon of mirroring across: a triangle and its outline on either side of a broken upright line.</summary>
    public static readonly string FlipAcrossIcon = Drawn("M8.4,1.6 L8.4,14.4 L1,14.4 Z", Outline((11.6, 1.6), (19, 14.4), (11.6, 14.4)),
        Block(9.4, 0, Weight, 2.4), Block(9.4, 4.5, Weight, 2.4), Block(9.4, 9, Weight, 2.4), Block(9.4, 13.5, Weight, 2.4));

    /// <summary>The icon of mirroring down: a triangle and its outline on either side of a broken level line.</summary>
    public static readonly string FlipDownIcon = Drawn("M2,6.8 L18,6.8 L2,0.6 Z", Outline((2, 9.2), (2, 15.4), (18, 9.2)),
        Bar(0, 7.4, 3), Bar(5.6, 7.4, 3), Bar(11.4, 7.4, 3), Bar(17, 7.4, 3));

    /// <summary>
    /// The icon of a picture tilted by one of Word's 3-D Rotation presets (see PictureTilt): a picture's frame as the engine's camera shows
    /// it, fitted into the icon's box.
    /// </summary>
    public static string TiltIcon(string camera)
    {
        XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
        var seen = FreeSlate.Drawing.Camera.Read(new XElement(a + "scene3d", new XElement(a + "camera", new XAttribute("prst", camera))));
        // A picture of 240 by 168 pixels, about the size of one in a column, for its perspective as it shows there.
        (double X, double Y)[] corners = [(-120, -84), (120, -84), (120, 84), (-120, 84)];
        var shown = corners.Select(c => seen?.Project(c.X, c.Y, 0) ?? c).ToArray();
        var (left, right, top, bottom) = (shown.Min(p => p.X), shown.Max(p => p.X), shown.Min(p => p.Y), shown.Max(p => p.Y));
        var scale = Math.Min(20 / (right - left), 16 / (bottom - top));
        var (dx, dy) = (10 - (left + right) / 2 * scale, 8 - (top + bottom) / 2 * scale);
        return Drawn(Outline([.. shown.Select(p => (p.X * scale + dx, p.Y * scale + dy))]));
    }

    /// <summary>The icon of a picture slanted (see TiltIcon): a frame leaning up to the right, as Word's isometric presets show a picture.</summary>
    public static readonly string AskewIcon = TiltIcon("isometricRightUp");

    /// <summary>The icon of tilting a picture by hand: a picture turned away, its right side nearer.</summary>
    public static readonly string TiltByHandIcon = TiltIcon("perspectiveContrastingLeftFacing");

    /// <summary>The hairline the gesture ring puts round a picture (see DrawingFormatting.Outline): half a point, in a quarter's grey.</summary>
    public const double FrameWidth = 0.5;
    public const string FrameColor = "BFBFBF";

    /// <summary>The icon of the places on the page: a page with nine dots.</summary>
    public static readonly string PlacesIcon = Drawn(Frame(4, 0, 12, 16),
        Block(6, 2.6, 1.6, 1.6), Block(9.2, 2.6, 1.6, 1.6), Block(12.4, 2.6, 1.6, 1.6),
        Block(6, 7.2, 1.6, 1.6), Block(9.2, 7.2, 1.6, 1.6), Block(12.4, 7.2, 1.6, 1.6),
        Block(6, 11.8, 1.6, 1.6), Block(9.2, 11.8, 1.6, 1.6), Block(12.4, 11.8, 1.6, 1.6));

    /// <summary>
    /// The eight places round the page's margins, from the top clockwise as the ring's directions point (top, top right, right …), the
    /// text beside them as Word's Position gallery has it: a page with a block where the picture goes.
    /// </summary>
    public static readonly IReadOnlyList<Placement> Places =
    [
        At("PlaceTop", "center", "top"),
        At("PlaceTopRight", "right", "top"),
        At("PlaceRight", "right", "center"),
        At("PlaceBottomRight", "right", "bottom"),
        At("PlaceBottom", "center", "bottom"),
        At("PlaceBottomLeft", "left", "bottom"),
        At("PlaceLeft", "left", "center"),
        At("PlaceTopLeft", "left", "top"),
    ];

    static Placement At(string nameKey, string across, string down)
    {
        var x = across switch { "left" => 2.2, "center" => 4.2, _ => 6.2 };
        var y = down switch { "top" => 2.2, "center" => 6.2, _ => 10.2 };
        return new(nameKey, Drawn(Frame(0, 0, 12, 16), Block(x, y, 3.6, 3.6)),
            new() { Wrap = "square", HorizontalFrom = "margin", HorizontalAlign = across, VerticalFrom = "margin", VerticalAlign = down });
    }

    /// <summary>The weight of an icon's lines and frames.</summary>
    const double Weight = 1.2;

    /// <summary>An icon of its parts, filled where any of them is (the inside of a frame is not, unless a part covers it).</summary>
    static string Drawn(params string[] parts) => "F1 " + string.Join(" ", parts);

    /// <summary>A line of text, as wide as given.</summary>
    static string Bar(double x, double y, double width) => Block(x, y, width, Weight);

    /// <summary>A stroke from one point to another, as thick as a line of text (all of them turning the same way, so that they cross filled).</summary>
    static string Stroke(double x1, double y1, double x2, double y2)
    {
        var (dx, dy) = (x2 - x1, y2 - y1);
        var length = Math.Sqrt(dx * dx + dy * dy);
        var (nx, ny) = (-dy / length * Weight / 2, dx / length * Weight / 2);
        return $"M{N(x1 + nx)},{N(y1 + ny)} L{N(x2 + nx)},{N(y2 + ny)} L{N(x2 - nx)},{N(y2 - ny)} L{N(x1 - nx)},{N(y1 - ny)} Z";
    }

    /// <summary>A filled box, clockwise.</summary>
    static string Block(double x, double y, double width, double height) => $"M{N(x)},{N(y)} H{N(x + width)} V{N(y + height)} H{N(x)} Z";

    /// <summary>A frame: a box less its inside, which goes the other way round.</summary>
    static string Frame(double x, double y, double width, double height) =>
        Block(x, y, width, height) + $" M{N(x + Weight)},{N(y + Weight)} V{N(y + height - Weight)} H{N(x + width - Weight)} V{N(y + Weight)} Z";

    /// <summary>A filled circle.</summary>
    static string Circle(double x, double y, double radius) =>
        $"M{N(x - radius)},{N(y)} A{N(radius)},{N(radius)} 0 1 0 {N(x + radius)},{N(y)} A{N(radius)},{N(radius)} 0 1 0 {N(x - radius)},{N(y)} Z";

    /// <summary>A ring as thick as a line of text, its outside as large as given: a circle less its inside, which goes the other way round.</summary>
    static string Ring(double x, double y, double radius)
    {
        var inner = radius - Weight;
        return Circle(x, y, radius)
            + $" M{N(x - inner)},{N(y)} A{N(inner)},{N(inner)} 0 1 1 {N(x + inner)},{N(y)} A{N(inner)},{N(inner)} 0 1 1 {N(x - inner)},{N(y)} Z";
    }

    /// <summary>
    /// An arc as thick as a line of text round a centre, clockwise from one angle to another (degrees, 0 to the right, as the y axis goes
    /// down; <paramref name="back"/>, the other way round, to below from), and at its end a head pointing on the way it turns.
    /// </summary>
    static string Turn(double x, double y, double radius, double from, double to, bool back = false)
    {
        (double X, double Y) At(double r, double degrees) => (x + r * Math.Cos(degrees * Math.PI / 180), y + r * Math.Sin(degrees * Math.PI / 180));
        var (outer, inner) = (radius + Weight / 2, radius - Weight / 2);
        var large = Math.Abs(to - from) > 180 ? 1 : 0;
        var (on, against) = back ? (0, 1) : (1, 0);
        var (a, b, c, d) = (At(outer, from), At(outer, to), At(inner, to), At(inner, from));
        // The head: across the arc's end, twice as wide as the arc, its tip ahead along the circle.
        var (wide, tip) = (At(radius + 2.2, to), At(radius, to + (back ? -1 : 1) * 3.2 / radius * 180 / Math.PI));
        var narrow = At(radius - 2.2, to);
        // The head goes round the same way as the arc, so that where they meet they fill as one.
        var (first, last) = back ? (narrow, wide) : (wide, narrow);
        return $"M{N(a.X)},{N(a.Y)} A{N(outer)},{N(outer)} 0 {large} {on} {N(b.X)},{N(b.Y)} L{N(c.X)},{N(c.Y)} A{N(inner)},{N(inner)} 0 {large} {against} {N(d.X)},{N(d.Y)} Z"
            + $" M{N(first.X)},{N(first.Y)} L{N(tip.X)},{N(tip.Y)} L{N(last.X)},{N(last.Y)} Z";
    }

    /// <summary>
    /// A frame of four or three straight sides through corners going clockwise (as the y axis goes down), its inside a line's weight in,
    /// which goes the other way round.
    /// </summary>
    static string Outline(params (double X, double Y)[] corners)
    {
        var n = corners.Length;
        // Each side moved in by the weight; the inside's corners where neighbouring sides so moved meet.
        (double X, double Y) Inward(int k)
        {
            var (p, q) = (corners[k], corners[(k + 1) % n]);
            var length = Math.Sqrt((q.X - p.X) * (q.X - p.X) + (q.Y - p.Y) * (q.Y - p.Y));
            return (-(q.Y - p.Y) / length * Weight, (q.X - p.X) / length * Weight);
        }
        var inside = new (double X, double Y)[n];
        for (var k = 0; k < n; k++)
        {
            var (before, after) = ((k + n - 1) % n, k);
            var (u, v) = (Inward(before), Inward(after));
            var (p1, d1) = ((corners[before].X + u.X, corners[before].Y + u.Y), (corners[k].X - corners[before].X, corners[k].Y - corners[before].Y));
            var (p2, d2) = ((corners[k].X + v.X, corners[k].Y + v.Y), (corners[(k + 1) % n].X - corners[k].X, corners[(k + 1) % n].Y - corners[k].Y));
            var cross = d1.Item1 * d2.Item2 - d1.Item2 * d2.Item1;
            var t = Math.Abs(cross) < 1e-9 ? 1 : ((p2.Item1 - p1.Item1) * d2.Item2 - (p2.Item2 - p1.Item2) * d2.Item1) / cross;
            inside[k] = (p1.Item1 + d1.Item1 * t, p1.Item2 + d1.Item2 * t);
        }
        return $"M{string.Join(" L", corners.Select(c => $"{N(c.X)},{N(c.Y)}"))} Z M{string.Join(" L", inside.Reverse().Select(c => $"{N(c.X)},{N(c.Y)}"))} Z";
    }

    static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
