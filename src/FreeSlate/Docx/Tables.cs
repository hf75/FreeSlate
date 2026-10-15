using System.Xml.Linq;
using FreeSlate.Drawing;

namespace FreeSlate.Docx;

/// <summary>A border line (w:top, w:insideV …, catalog: table.borders): its style, width in points, color and space.</summary>
public sealed record BorderLine(string Style, double Width, RgbColor Color, double Space)
{
    /// <summary>Whether the border casts a shadow (w:shadow; page borders draw it).</summary>
    public bool Shadow { get; init; }

    /// <summary>nil and none draw nothing; nil also suppresses the neighbour's border in a conflict.</summary>
    public bool Visible => Style is not ("nil" or "none") && Width > 0;

    /// <summary>Whether the border's color is automatic (w:color auto or none).</summary>
    public bool AutoColor { get; init; }

    /// <summary>
    /// The color of one side of the border: an automatic outset or inset border in Word's two shades of gray, light
    /// above and left of a raised (outset) table and dark below and right of it, a cell's the other way round
    /// (measured with HTML tables Word imports, docx/local/altchunk, altchunk-tables: F0F0F0 and A0A0A0); one of a
    /// color of its own in that color on its light sides and half of it, each channel halved down, on its dark sides
    /// (docx/external/tika/testInstrLink: EDEBE9 and 767574; poi/bug59058: C0C0C0 and 606060).
    /// </summary>
    public RgbColor ColorOf(string side, bool cell)
    {
        if (Style is not ("outset" or "inset"))
            return Color;
        var raised = (Style == "outset") != cell;
        var light = side is "top" or "left" ? raised : !raised;
        if (!AutoColor)
            return light ? Color : new RgbColor(Half(Color.R), Half(Color.G), Half(Color.B));
        return RgbColor.FromHex(light ? "F0F0F0" : "A0A0A0");
        static double Half(double channel) => Math.Floor(Math.Round(channel * 255) / 2) / 255;
    }

    /// <summary>A border element, or null where it is absent; "none" and "nil" are kept (they override).</summary>
    public static BorderLine? Read(XElement? e)
    {
        if (e == null)
            return null;
        var style = e.Val() ?? "none";
        // w:sz is in eighths of a point (2 … 96), 0 as thin as 1 (docx/local/bsz-para, bsz-cell: paragraph and cell
        // lines of sizes 0 and 1 both 0.12 pt; docx/external/libreoffice/extras-ooxmlexport/tdf57589_hashColor); art
        // borders measure in points, not used by tables.
        var width = Math.Max(1, e.Number("sz") ?? 4) / 8;
        if (style is "nil" or "none")
            width = 0;
        var given = Wml.HexColor(e.Attr("color"));
        // Named colors as for shading (docx/external/libreoffice/extras-ooxmlexport/tdf132898_extraBorder: darkblue).
        var named = Shading.Value(given);
        var color = named ?? RgbColor.Black;
        return new BorderLine(style, width, color, e.Number("space") ?? 0)
        {
            Shadow = e.Attr("shadow") is "1" or "true" or "on",
            AutoColor = named == null,
        };
    }

    /// <summary>
    /// The total width the border takes: double, triple and thin-and-thick lines are wider than one stroke, by their
    /// lines and gaps (see TableLayout.Stripes).
    /// </summary>
    public double Total => Style switch
    {
        _ when !Visible => 0,
        "double" => Width * 3,
        "triple" => Width * 5,
        "thinThickSmallGap" or "thickThinSmallGap" => Width + 1.5,
        "thinThickMediumGap" or "thickThinMediumGap" => Width * 2,
        "thinThickLargeGap" or "thickThinLargeGap" => Width + 2.25,
        "thinThickThinSmallGap" => Width + 3,
        "thinThickThinMediumGap" => Width * 3,
        "thinThickThinLargeGap" => Width * 2 + 3,
        // The thin lines take half the width each, 0.75 pt at least, though they print on whole dots (Thin; measured
        // with docx/local/b3d-emb: 6 pt of room for a line of 3 pt, its strokes 5.88 pt).
        "threeDEmboss" or "threeDEngrave" => Width + 2 * Math.Max(0.75, Width / 2),
        _ => Width,
    };

    /// <summary>
    /// The thin lines beside an embossed or engraved line at the width: half of it, 0.75 pt at least, on Word's
    /// 0.12 pt dots (measured with docx/local/b3d-emb, b3d-thin: 1.44 pt beside 3 pt, 0.72 pt beside 1 pt).
    /// </summary>
    public double Thin => Math.Floor(Math.Max(0.75, Width / 2) / 0.12 + 1e-6) * 0.12;

    /// <summary>
    /// The shades of an embossed or engraved line top to bottom and left to right, on every side: embossed light, the
    /// line, dark; engraved the other way round (docx/local/b3d-emb, b3d-eng: light outside at the top and left, inside
    /// at the bottom and right). Black and automatic lines are C0C0C0, 606060 and black; others keep their color outside and
    /// take 70 and 80 of Word's 240 steps of lightness off it for the others, towards white less (measured with
    /// docx/local/b3d-emb, b3d-eng, b3d-mix, b3d-red, b3d-colors: 33CCCC 165555 114444, FF0000 6B0000 550000, C0C0C0
    /// 757575 6B6B6B, FFFFFF A0A0A0 808080; known deviation: 804020 lightens to C46131 first).
    /// </summary>
    public RgbColor[] Shades()
    {
        RgbColor[] shades;
        if (AutoColor || Color.R + Color.G + Color.B < 0.01)
            shades = [RgbColor.FromHex("C0C0C0"), RgbColor.FromHex("606060"), RgbColor.Black];
        else
        {
            var (h, s, l) = Color.ToHsl();
            var steps = l * 240;
            // Down by 70 and 80 steps up to a lightness of 180.7 (C0C0C0), then towards white's 150.6 and 120.5.
            double Off(double by, double atWhite) => steps <= 180.7 ? Math.Max(0, steps - by)
                : 180.7 - by + (steps - 180.7) / (240 - 180.7) * (atWhite - (180.7 - by));
            shades = [Color, RgbColor.FromHsl(h, s, Off(70, 150.6) / 240), RgbColor.FromHsl(h, s, Off(80, 120.5) / 240)];
        }
        return Style == "threeDEngrave" ? [shades[2], shades[1], shades[0]] : shades;
    }

    /// <summary>
    /// Word's weight in border conflicts (MS-OI29500 17.4.66): eighths of a point times the style's number, dotted
    /// and dashed lines 1.
    /// </summary>
    public double Weight => Style switch
    {
        "dotted" or "dashed" => 1,
        _ => Width * 8 * Number,
    };

    int Number => Style switch
    {
        "single" => 1, "thick" => 2, "double" => 3, "dotDash" => 8, "dotDotDash" => 9, "triple" => 10,
        "thinThickSmallGap" => 11, "thickThinSmallGap" => 12, "thinThickThinSmallGap" => 13, "thinThickMediumGap" => 14,
        "thickThinMediumGap" => 15, "thinThickThinMediumGap" => 16, "thinThickLargeGap" => 17, "thickThinLargeGap" => 18,
        "thinThickThinLargeGap" => 19, "wave" => 20, "doubleWave" => 21, "dashSmallGap" => 22, "dashDotStroked" => 23,
        "threeDEmboss" => 24, "threeDEngrave" => 25, "outset" => 26, "inset" => 27,
        _ => 1,
    };

    static readonly string[] Precedence =
    [
        "single", "thick", "double", "dotted", "dashed", "dotDash", "dotDotDash", "triple", "thinThickSmallGap",
        "thickThinSmallGap", "thinThickThinSmallGap", "thinThickMediumGap", "thickThinMediumGap", "thinThickThinMediumGap",
        "thinThickLargeGap", "thickThinLargeGap", "thinThickThinLargeGap", "wave", "doubleWave", "dashSmallGap",
        "dashDotStroked", "threeDEmboss", "threeDEngrave", "outset", "inset",
    ];

    /// <summary>
    /// The border shown where two cells' borders meet (MS-OI29500 17.4.66): nil shows none, none the other; the
    /// heavier weight wins, then the style earlier in Word's list, then the darker color.
    /// </summary>
    public static BorderLine? Resolve(BorderLine? a, BorderLine? b)
    {
        if (a == null || b == null)
            return a ?? b;
        if (a.Style == "nil" || b.Style == "nil")
            return null;
        if (!a.Visible)
            return b.Visible ? b : null;
        if (!b.Visible)
            return a;
        if (Math.Abs(a.Weight - b.Weight) > 1e-9)
            return a.Weight > b.Weight ? a : b;
        var pa = Array.IndexOf(Precedence, a.Style);
        var pb = Array.IndexOf(Precedence, b.Style);
        if (pa != pb)
            return pa >= 0 && (pb < 0 || pa < pb) ? a : b;
        static double Brightness(RgbColor c) => c.R + c.B + 2 * c.G;
        static double Second(RgbColor c) => c.B + 2 * c.G;
        if (Math.Abs(Brightness(a.Color) - Brightness(b.Color)) > 1e-9)
            return Brightness(a.Color) < Brightness(b.Color) ? a : b;
        if (Math.Abs(Second(a.Color) - Second(b.Color)) > 1e-9)
            return Second(a.Color) < Second(b.Color) ? a : b;
        return a.Color.G <= b.Color.G ? a : b;
    }
}

/// <summary>The borders of a table, a row exception or a cell: four sides, the inside lines and the diagonals.</summary>
public sealed class BorderSet
{
    public BorderLine? Top, Left, Bottom, Right, InsideH, InsideV, TopLeftToBottomRight, TopRightToBottomLeft;

    public static BorderSet? Read(XElement? e)
    {
        if (e == null)
            return null;
        return new BorderSet
        {
            Top = BorderLine.Read(e.Child("top")),
            Left = BorderLine.Read(e.Child("left") ?? e.Child("start")),
            Bottom = BorderLine.Read(e.Child("bottom")),
            Right = BorderLine.Read(e.Child("right") ?? e.Child("end")),
            InsideH = BorderLine.Read(e.Child("insideH")),
            InsideV = BorderLine.Read(e.Child("insideV")),
            TopLeftToBottomRight = BorderLine.Read(e.Child("tl2br")),
            TopRightToBottomLeft = BorderLine.Read(e.Child("tr2bl")),
        };
    }

    /// <summary>The sides <paramref name="top"/> states laid over these.</summary>
    public BorderSet Overlay(BorderSet? top)
    {
        if (top == null)
            return this;
        return new BorderSet
        {
            Top = top.Top ?? Top,
            Left = top.Left ?? Left,
            Bottom = top.Bottom ?? Bottom,
            Right = top.Right ?? Right,
            InsideH = top.InsideH ?? InsideH,
            InsideV = top.InsideV ?? InsideV,
            TopLeftToBottomRight = top.TopLeftToBottomRight ?? TopLeftToBottomRight,
            TopRightToBottomLeft = top.TopRightToBottomLeft ?? TopRightToBottomLeft,
        };
    }
}

/// <summary>Cell margins in points; null sides are not stated.</summary>
public sealed record CellMargins(double? Top, double? Left, double? Bottom, double? Right)
{
    public static CellMargins? Read(XElement? e)
    {
        if (e == null)
            return null;
        static double? Side(XElement? s) => s?.Attr("type") switch
        {
            null when s == null => null,
            "nil" => 0,
            "pct" => null,
            _ => s!.Twips("w"),
        };
        return new CellMargins(Side(e.Child("top")), Side(e.Child("left") ?? e.Child("start")), Side(e.Child("bottom")), Side(e.Child("right") ?? e.Child("end")));
    }

    public CellMargins Overlay(CellMargins? top) =>
        top == null ? this : new CellMargins(top.Top ?? Top, top.Left ?? Left, top.Bottom ?? Bottom, top.Right ?? Right);
}

/// <summary>A width with its type (w:tblW, w:tcW, w:wBefore): dxa (twips, here points), pct (fiftieths of a percent), auto or nil.</summary>
public readonly record struct TableWidth(double Value, string Type)
{
    public static TableWidth? Read(XElement? e)
    {
        if (e == null)
            return null;
        var type = e.Attr("type") ?? "dxa";
        var raw = e.Attr("w");
        double value;
        if (type == "pct" && raw != null && raw.EndsWith('%'))
            value = double.Parse(raw[..^1], System.Globalization.CultureInfo.InvariantCulture) * 50;
        else
            value = e.Number("w") ?? 0;
        return new TableWidth(type == "dxa" ? value / 20 : value, type);
    }

    /// <summary>The width in points against a reference width (for percentages), or null for auto.</summary>
    public double? Resolve(double reference) => Type switch
    {
        "dxa" => Value,
        "pct" => reference * Value / 5000,
        _ => null,
    };
}

/// <summary>Cell shading (w:shd): the fill, a pattern in its color over it.</summary>
public static class Shading
{
    /// <summary>The color a shading paints, or null where it paints nothing.</summary>
    public static RgbColor? Color(XElement? shd) => shd == null ? null : Color(shd.Val(), shd.Attr("color"), shd.Attr("fill"));

    /// <summary>
    /// A color of a w:shd: six hex digits, also after a "#" (Wml.HexColor), or a color's name, which Word reads too
    /// (docx/external/powertools/HC032-Named-Color: w:fill="orange" fills FFA500); anything else (auto) is none.
    /// </summary>
    public static RgbColor? Value(string? value) => Hex(Wml.HexColor(value));

    static RgbColor? Hex(string? value) =>
        value is { Length: 6 } && int.TryParse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _)
            ? RgbColor.FromHex(value)
            : value is { Length: > 0 } and not "auto" ? FreeSlate.Drawing.PresetColors.Find(value) : null;

    /// <summary>The color a w:shd's pattern, pattern color and fill show as.</summary>
    public static RgbColor? Color(string? val, string? patternColor, string? fillColor)
    {
        var pattern = val ?? "clear";
        var fill = Value(fillColor);
        var color = Value(patternColor) ?? RgbColor.Black;
        if (pattern is "nil")
            return null;
        if (pattern is "clear")
            return fill;
        if (pattern is "solid")
            return color;
        // Percentage patterns mix the pattern color over the fill; stripes and crosses as much as their 8 × 8 tiles cover
        // (measured with docx/local/shp-patterns: stripes and the horizontal cross 32 of 64 pixels, the diagonal cross
        // 48, thin stripes 16, thin crosses 28; docx/external/libreoffice/extras-ooxmlexport/style-pane-format-filter,
        // theme-preservation: thinHorzStripe a quarter).
        var share = pattern.StartsWith("pct", StringComparison.Ordinal) && double.TryParse(pattern[3..], out var pct) ? pct / 100
            : pattern switch
            {
                "diagCross" => 0.75,
                "thinHorzCross" or "thinDiagCross" => 28 / 64.0,
                "thinHorzStripe" or "thinVertStripe" or "thinReverseDiagStripe" or "thinDiagStripe" => 0.25,
                _ => 0.5,
            };
        return RgbColor.Mix(fill ?? RgbColor.White, color, share);
    }
}

/// <summary>Table properties (w:tblPr) resolved through the table style (catalog: table.width, table.styles).</summary>
public sealed class TableFormat
{
    public string? Style;

    /// <summary>
    /// Whether a table style applies (the table's own or the document's default): cells without margins of their own
    /// have none then, as the style and the table give none (measured with docx/local/c2-tblpos: a table whose style
    /// is not based on the default style has its text at its lines); without any style, 0.5 pt (docx/local/tblnostyle-*).
    /// </summary>
    public bool Styled;
    public TableWidth? Width;
    public string? Justification;
    public double? Indent;
    public double? CellSpacing;
    public CellMargins Margins = new(null, null, null, null);
    public BorderSet Borders = new();
    public string? Layout;
    public XElement? Shading;
    public int RowBandSize = 1, ColumnBandSize = 1;
    public bool FirstRow = true, LastRow, FirstColumn = true, LastColumn, NoHorizontalBand, NoVerticalBand = true;

    /// <summary>
    /// Whether the table's style has a first-column format, without which column bands count the first column too
    /// (docx/external/libreoffice/extras-ooxmlexport/tdf138020_undefined_firstCol: columns 1 and 3 in the first
    /// band's green; powertools/HC007-Test-02).
    /// </summary>
    public bool FirstColumnStyled = true;
    public bool BidiVisual;
    public XElement? Floating;

    /// <summary>Whether a floating table may not overlap other floating tables (w:tblOverlap never).</summary>
    public bool NoOverlap;
    public string? Caption, Description;

    public void Apply(XElement? tblPr)
    {
        if (tblPr == null)
            return;
        Style = tblPr.Child("tblStyle").Val() ?? Style;
        Width = TableWidth.Read(tblPr.Child("tblW")) ?? Width;
        Justification = tblPr.Child("jc").Val() ?? Justification;
        if (TableWidth.Read(tblPr.Child("tblInd")) is { Type: "dxa" } indent)
            Indent = indent.Value;
        if (TableWidth.Read(tblPr.Child("tblCellSpacing")) is { Type: "dxa" } spacing)
            CellSpacing = spacing.Value;
        Margins = Margins.Overlay(CellMargins.Read(tblPr.Child("tblCellMar")));
        Borders = Borders.Overlay(BorderSet.Read(tblPr.Child("tblBorders")));
        Layout = tblPr.Child("tblLayout").Attr("type") ?? Layout;
        Shading = tblPr.Child("shd") ?? Shading;
        RowBandSize = (int?)tblPr.Child("tblStyleRowBandSize").Number("val") ?? RowBandSize;
        ColumnBandSize = (int?)tblPr.Child("tblStyleColBandSize").Number("val") ?? ColumnBandSize;
        BidiVisual = tblPr.Child("bidiVisual").OnOff() ?? BidiVisual;
        Floating = tblPr.Child("tblpPr") ?? Floating;
        NoOverlap = tblPr.Child("tblOverlap").Val() is { } overlap ? overlap == "never" : NoOverlap;
        Caption = tblPr.Child("tblCaption").Val() ?? Caption;
        Description = tblPr.Child("tblDescription").Val() ?? Description;
        if (tblPr.Child("tblLook") is { } look)
        {
            // Word 2010 writes the flags as attributes; Word 2007 as a hexadecimal bit mask in w:val. Where any flag is an
            // attribute, the mask counts for nothing and the flags left out are on (docx/external/libreoffice/
            // extras-ooxmlexport/tdf167843_tblLook_firstRow_tblHeader: val 04A0 and firstRow 0 shade the first and
            // last column and the last row, without bands).
            var mask = look.Attr("val") is { } hex && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var m) ? m : (int?)null;
            var named = new[] { "firstRow", "lastRow", "firstColumn", "lastColumn", "noHBand", "noVBand" }.Any(n => look.Attr(n) != null);
            bool Flag(string name, int bit) => look.Attr(name) is { } v ? v is "1" or "true" or "on" : named || mask is { } b && (b & bit) != 0;
            FirstRow = Flag("firstRow", 0x20);
            LastRow = Flag("lastRow", 0x40);
            FirstColumn = Flag("firstColumn", 0x80);
            LastColumn = Flag("lastColumn", 0x100);
            NoHorizontalBand = Flag("noHBand", 0x200);
            NoVerticalBand = Flag("noVBand", 0x400);
        }
    }
}

/// <summary>Row properties (w:trPr, catalog: table.rows).</summary>
public sealed class RowFormat
{
    public double? Height;
    public string HeightRule = "atLeast";
    public bool CantSplit, Header, Hidden;
    public int GridBefore, GridAfter;
    public TableWidth? WidthBefore, WidthAfter;
    public string? Justification;
    public double? CellSpacing;

    public void Apply(XElement? trPr)
    {
        if (trPr == null)
            return;
        if (trPr.Child("trHeight") is { } height)
        {
            Height = height.Twips("val") ?? Height;
            HeightRule = height.Attr("hRule") ?? "atLeast";
        }
        CantSplit = trPr.Child("cantSplit").OnOff() ?? CantSplit;
        Header = trPr.Child("tblHeader").OnOff() ?? Header;
        Hidden = trPr.Child("hidden").OnOff() ?? Hidden;
        GridBefore = (int?)trPr.Child("gridBefore").Number("val") ?? GridBefore;
        GridAfter = (int?)trPr.Child("gridAfter").Number("val") ?? GridAfter;
        WidthBefore = TableWidth.Read(trPr.Child("wBefore")) ?? WidthBefore;
        WidthAfter = TableWidth.Read(trPr.Child("wAfter")) ?? WidthAfter;
        Justification = trPr.Child("jc").Val() ?? Justification;
        if (TableWidth.Read(trPr.Child("tblCellSpacing")) is { Type: "dxa" } spacing)
            CellSpacing = spacing.Value;
    }
}

/// <summary>Cell properties (w:tcPr, catalog: table.cells).</summary>
public sealed class CellFormat
{
    public TableWidth? Width;
    public int GridSpan = 1;
    public string? VerticalMerge;
    public string? HorizontalMerge;
    public BorderSet Borders = new();
    public XElement? Shading;
    public CellMargins Margins = new(null, null, null, null);
    public string VerticalAlign = "top";
    public string? TextDirection;
    public bool NoWrap, FitText, HideMark;

    /// <summary>Whether the cell's own w:tcPr shades it, which then replaces its row's and its table's shading.</summary>
    public bool OwnShading;

    public void Apply(XElement? tcPr)
    {
        if (tcPr == null)
            return;
        Width = TableWidth.Read(tcPr.Child("tcW")) ?? Width;
        GridSpan = Math.Max(1, (int?)tcPr.Child("gridSpan").Number("val") ?? GridSpan);
        if (tcPr.Child("vMerge") is { } vMerge)
            VerticalMerge = vMerge.Val() ?? "continue";
        if (tcPr.Child("hMerge") is { } hMerge)
            HorizontalMerge = hMerge.Val() ?? "continue";
        Borders = Borders.Overlay(BorderSet.Read(tcPr.Child("tcBorders")));
        Shading = tcPr.Child("shd") ?? Shading;
        Margins = Margins.Overlay(CellMargins.Read(tcPr.Child("tcMar")));
        VerticalAlign = tcPr.Child("vAlign").Val() ?? VerticalAlign;
        TextDirection = tcPr.Child("textDirection").Val() ?? TextDirection;
        NoWrap = tcPr.Child("noWrap").OnOff() ?? NoWrap;
        FitText = tcPr.Child("tcFitText").OnOff() ?? FitText;
        HideMark = tcPr.Child("hideMark").OnOff() ?? HideMark;
    }
}

/// <summary>A cell: its grid column, its formatting, the conditional formats that apply and its content.</summary>
public sealed class CellModel
{
    public int Column { get; set; }
    public CellFormat Format { get; } = new();
    public List<Block> Blocks { get; } = [];

    /// <summary>The table style's conditional formats of the cell, in the order they apply.</summary>
    public List<string> Conditions { get; } = [];

    /// <summary>Borders the cell itself or a conditional format states (they win over the table's own).</summary>
    public BorderSet CellBorders { get; set; } = new();

    /// <summary>Where a cell continues the vertical merge of the cell above it.</summary>
    public bool MergedAbove => Format.VerticalMerge == "continue";
}

public sealed class RowModel
{
    public RowFormat Format { get; } = new();
    public List<CellModel> Cells { get; } = [];

    /// <summary>Table property exceptions of the row (w:tblPrEx): borders and margins it states instead of the table.</summary>
    public BorderSet? BorderExceptions;
    public CellMargins? MarginExceptions;
    public XElement? ShadingException;
}

/// <summary>A table read with its grid and resolved formatting (catalog: table.structure).</summary>
public sealed class TableModel
{
    public TableFormat Format { get; } = new();
    public List<double> Grid { get; } = [];
    public List<RowModel> Rows { get; } = [];

    /// <summary>The table's tracked formatting change (w:tblPrChange) in the markup view, or null.</summary>
    public FormatChange? Change { get; set; }
}
