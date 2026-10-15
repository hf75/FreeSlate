using System.Buffers.Binary;
using System.Text;

namespace FreeSlate.Metafiles;

/// <summary>
/// Plays an Enhanced Metafile ([MS-EMF]) into SVG. EMF+ records in GDI comments ([MS-EMFPLUS]) are played by
/// <see cref="EmfPlusPlayer"/>; in dual metafiles the GDI records that follow an EMF+ drawing record duplicate it
/// and are skipped, while GDI records after state-only EMF+ records (Office writes text that way) are drawn.
/// </summary>
public sealed class EmfPlayer
{
    readonly byte[] data;
    readonly SvgWriter svg;
    GdiContext gdi = null!;
    /// <summary>The reference device's size in pixels and millimeters (header szlDevice, szlMillimeters).</summary>
    (int X, int Y) devicePx, deviceMm;
    EmfPlusPlayer? plus;

    /// <summary>
    /// A monochrome bitmap ORed onto the picture (SRCPAINT). GDI+ draws transparent images for GDI as such a mask
    /// followed by the image ANDed at the same place (SRCAND); the pair becomes one image with transparency.
    /// </summary>
    (Bitmap Mask, double X, double Y, double W, double H, (int X, int Y, int W, int H) Source)? pendingMask;

    /// <summary>
    /// An icon's monochrome mask ANDed onto the picture (SRCAND), black where the icon is opaque, waiting for the
    /// icon's image XORed at the same place (SRCINVERT); the pair becomes one image with transparency (docx/external/
    /// libreoffice/extras-ooxmlexport/tdf135653: an embedded object's icon on its red ground).
    /// </summary>
    (Bitmap Mask, double X, double Y, double W, double H, (int X, int Y, int W, int H) Source)? pendingAnd;

    public (double X, double Y, double W, double H) ViewBox { get; private set; }

    double? displayWidth;

    EmfPlayer(byte[] data, string idPrefix)
    {
        this.data = data;
        svg = new SvgWriter(idPrefix);
    }

    public static bool IsEmf(byte[] data) =>
        data.Length >= 88 && BinaryPrimitives.ReadUInt32LittleEndian(data) == 1 && BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(40)) == 0x464D4520;

    public static string? ToSvg(byte[] data, string idPrefix = "m", double? displayWidth = null)
    {
        if (!IsEmf(data))
            return null;
        var player = new EmfPlayer(data, idPrefix) { displayWidth = displayWidth };
        player.Play();
        if (player.gdi is not null)
            player.gdi.FlushXor();
        var (x, y, w, h) = player.ViewBox;
        return player.svg.Finish(x, y, w, h);
    }

    ReadOnlySpan<byte> Span(int offset, int length) =>
        offset >= 0 && length >= 0 && offset + length <= data.Length ? data.AsSpan(offset, length) : ReadOnlySpan<byte>.Empty;

    int I32(int offset) => offset + 4 <= data.Length ? BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset)) : 0;
    uint U32(int offset) => offset + 4 <= data.Length ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset)) : 0;
    short I16(int offset) => offset + 2 <= data.Length ? BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset)) : (short)0;
    ushort U16(int offset) => offset + 2 <= data.Length ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset)) : (ushort)0;
    float F32(int offset) => offset + 4 <= data.Length ? BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset)) : 0;

    void Play()
    {
        // Header: bounds (device units), frame (0.01 mm), reference device size in pixels and millimeters.
        var frame = (L: I32(24), T: I32(28), R: I32(32), B: I32(36));
        devicePx = (I32(72), I32(76));
        deviceMm = (I32(80), I32(84));
        // The reference device's millimeters give the ratio, not the header's micrometers where they disagree (Word
        // maps by szlMillimeters: docx/external/tika/testWORD_embedded_pdf, 474 × 296 mm against 592666 × 370416 µm,
        // prints the picture at 80 % of its shape, as the millimeters say).
        var pxPerMmX = deviceMm.X > 0 ? devicePx.X / (double)deviceMm.X : 96 / 25.4;
        var pxPerMmY = deviceMm.Y > 0 ? devicePx.Y / (double)deviceMm.Y : 96 / 25.4;
        var x = frame.L / 100.0 * pxPerMmX;
        var y = frame.T / 100.0 * pxPerMmY;
        var w = (frame.R - frame.L) / 100.0 * pxPerMmX;
        var h = (frame.B - frame.T) / 100.0 * pxPerMmY;
        if (w <= 0 || h <= 0)
        {
            var bounds = (L: I32(8), T: I32(12), R: I32(16), B: I32(20));
            (x, y, w, h) = (bounds.L, bounds.T, bounds.R - bounds.L + 1, bounds.B - bounds.T + 1);
        }
        ViewBox = (x, y, w, h);
        gdi = new GdiContext(svg, (pxPerMmX + pxPerMmY) / 2)
        {
            // Units per output pixel: from the display width, else the picture at its natural size (96 dpi).
            UnitsPerPixel = displayWidth is > 0 ? w / displayWidth.Value : (pxPerMmX + pxPerMmY) / 2 * 25.4 / 96,
            DevicePixels = true,
        };

        var offset = 0;
        while (offset + 8 <= data.Length)
        {
            var type = U32(offset);
            var size = (int)U32(offset + 4);
            if (size < 8 || offset + size > data.Length)
                break;
            if (type == 14)
                break;
            try
            {
                Record(type, offset, size);
            }
            catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException or OverflowException)
            {
                // A damaged record is skipped like GDI does.
            }
            offset += size;
        }
        FlushAnd();
    }

    static readonly HashSet<uint> DrawingRecords =
    [
        2, 3, 4, 5, 6, 7, 8, 15, 27, 41, 42, 43, 44, 45, 46, 47, 53, 54, 55, 56, 62, 63, 64, 71, 72, 73, 74, 76, 77, 78, 79, 80, 81,
        83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 96, 97, 108, 114, 116, 118,
    ];

    void Record(uint type, int o, int size)
    {
        if (type == 70)
        {
            Comment(o, size);
            return;
        }
        if (gdi.SuppressDrawing)
            return;   // duplicate GDI rendering of an EMF+ drawing record
        if (pendingMask != null && !IsMaskPartner(type, o))
            FlushMask();
        if (pendingAnd != null && !IsIconPartner(type, o))
            FlushAnd();

        var s = gdi.State;
        switch (type)
        {
            case 2 or 3 or 4 or 5 or 6:   // POLYBEZIER, POLYGON, POLYLINE, POLYBEZIERTO, POLYLINETO
                Poly(type, PointsL(o + 28, I32(o + 24)));
                break;
            case 85 or 86 or 87 or 88 or 89:
                Poly(type - 83, PointsS(o + 28, I32(o + 24)));
                break;
            case 7 or 8:   // POLYPOLYLINE, POLYPOLYGON
                PolyPoly(o, type == 8, wide: true);
                break;
            case 90 or 91:
                PolyPoly(o, type == 91, wide: false);
                break;
            case 9: s.WindowExt = (I32(o + 8), I32(o + 12)); break;
            case 10: s.WindowOrg = (I32(o + 8), I32(o + 12)); break;
            case 11: s.ViewportExt = (I32(o + 8), I32(o + 12)); break;
            case 12: s.ViewportOrg = (I32(o + 8), I32(o + 12)); break;
            case 15:   // SETPIXELV
            {
                var (px, py) = gdi.ToDevice(I32(o + 8), I32(o + 12));
                svg.Add($"<rect x=\"{SvgWriter.Num(px)}\" y=\"{SvgWriter.Num(py)}\" width=\"1\" height=\"1\" fill=\"{SvgWriter.Color(gdi.Resolve(U32(o + 16)))}\"/>", s.Clip);
                break;
            }
            case 17:   // SETMAPMODE
            {
                // A new mode sets the extents as GDI does, MM_ISOTROPIC's as MM_LOMETRIC's: the reference device's
                // tenths of a millimeter onto its pixels, upwards (docx/external/libreoffice/extras-ooxmlimport/
                // tdf133448: an isotropic window of 12471 units and no viewport, an NMR chart over the 1024 × 768 px).
                var mode = I32(o + 8);
                if (mode != s.MapMode && (mode is >= 1 and <= 7))
                {
                    var units = mode switch { 3 => 100.0, 4 => 1000 / 25.4, 5 => 10000 / 25.4, 6 => 14400 / 25.4, _ => 10.0 };
                    (s.WindowExt, s.ViewportExt) = mode == 1 ? ((1, 1), (1, 1))
                        : ((deviceMm.X * units, deviceMm.Y * units), (devicePx.X, -devicePx.Y));
                }
                s.MapMode = mode;
                break;
            }
            case 18: s.BkMode = I32(o + 8); break;
            case 19: s.PolyFillMode = I32(o + 8); break;
            case 20: s.Rop2 = I32(o + 8); break;
            case 22: s.TextAlign = I32(o + 8); break;
            case 24: s.TextColor = U32(o + 8); break;
            case 25: s.BkColor = U32(o + 8); break;
            case 27: gdi.MoveTo(I32(o + 8), I32(o + 12)); break;
            case 26: gdi.OffsetClip(I32(o + 8), I32(o + 12)); break;   // OFFSETCLIPRGN
            case 28: gdi.SetMetaRegion(); break;
            case 29: gdi.ExcludeClipRect(I32(o + 8), I32(o + 12), I32(o + 16), I32(o + 20)); break;
            case 30: gdi.IntersectClipRect(I32(o + 8), I32(o + 12), I32(o + 16), I32(o + 20)); break;
            case 31:   // SCALEVIEWPORTEXTEX
                s.ViewportExt = (s.ViewportExt.X * I32(o + 8) / Math.Max(1.0, I32(o + 12)), s.ViewportExt.Y * I32(o + 16) / Math.Max(1.0, I32(o + 20)));
                break;
            case 32:   // SCALEWINDOWEXTEX
                s.WindowExt = (s.WindowExt.X * I32(o + 8) / Math.Max(1.0, I32(o + 12)), s.WindowExt.Y * I32(o + 16) / Math.Max(1.0, I32(o + 20)));
                break;
            case 33: gdi.Save(); break;
            case 34: gdi.Restore(I32(o + 8)); break;
            case 35: s.World = Xform(o + 8); break;
            case 36:   // MODIFYWORLDTRANSFORM
            {
                var x = Xform(o + 8);
                s.World = U32(o + 32) switch
                {
                    1 => Metafiles.Xform.Identity,
                    2 => x.Then(s.World),
                    3 => s.World.Then(x),
                    4 => x,
                    _ => s.World,
                };
                break;
            }
            case 37:   // SELECTOBJECT
            {
                var handle = U32(o + 8);
                if ((handle & 0x80000000) != 0)
                    gdi.SelectStock((int)(handle & 0x7FFFFFFF));
                else if (gdi.Objects.TryGetValue((int)handle, out var item))
                    gdi.Select(item);
                break;
            }
            case 38:   // CREATEPEN
                gdi.Objects[I32(o + 8)] = new GdiPen { Style = I32(o + 12), Width = I32(o + 16), Color = U32(o + 24) };
                break;
            case 39:   // CREATEBRUSHINDIRECT
                gdi.Objects[I32(o + 8)] = new GdiBrush { Style = I32(o + 12), Color = U32(o + 16), Hatch = I32(o + 20) };
                break;
            case 40: gdi.Objects.Remove(I32(o + 8)); break;
            case 48:   // SELECTPALETTE
            {
                var handle = U32(o + 8);
                if ((handle & 0x80000000) != 0)
                    gdi.SelectStock((int)(handle & 0x7FFFFFFF));
                else if (gdi.Objects.TryGetValue((int)handle, out var palette) && palette is GdiPalette)
                    gdi.Select(palette);
                break;
            }
            case 49:   // CREATEPALETTE: version, entry count, entries (red, green, blue, flags)
                gdi.Objects[I32(o + 8)] = new GdiPalette { Entries = PaletteEntries(o + 16, U16(o + 14)) };
                break;
            case 50:   // SETPALETTEENTRIES
                if (gdi.Objects.TryGetValue(I32(o + 8), out var changed) && changed is GdiPalette target)
                {
                    var (first, count) = ((int)U32(o + 12), (int)U32(o + 16));
                    var entries = PaletteEntries(o + 20, count);
                    var merged = target.Entries.ToList();
                    while (merged.Count < first + count)
                        merged.Add(0);
                    for (var i = 0; i < count; i++)
                        merged[first + i] = entries[i];
                    target.Entries = [.. merged];
                }
                break;
            case 51:   // RESIZEPALETTE
                if (gdi.Objects.TryGetValue(I32(o + 8), out var resized) && resized is GdiPalette sized)
                {
                    var entries = sized.Entries;
                    Array.Resize(ref entries, Math.Clamp((int)U32(o + 12), 0, 1024));
                    sized.Entries = entries;
                }
                break;
            case 52: break;   // REALIZEPALETTE: the selected palette is used as it is
            case 41:   // ANGLEARC
            {
                var (cx, cy, r) = ((double)I32(o + 8), (double)I32(o + 12), (double)U32(o + 16));
                var start = F32(o + 20) * Math.PI / 180;
                var sweep = F32(o + 24) * Math.PI / 180;
                var a = (cx + r * Math.Cos(start), cy - r * Math.Sin(start));
                var b = (cx + r * Math.Cos(start + sweep), cy - r * Math.Sin(start + sweep));
                var clockwise = s.Clockwise;
                s.Clockwise = sweep < 0;
                gdi.Arc(cx - r, cy - r, cx + r, cy + r, a, b, "arc", toCurrent: true);
                s.Clockwise = clockwise;
                break;
            }
            case 42: gdi.Ellipse(I32(o + 8), I32(o + 12), I32(o + 16), I32(o + 20)); break;
            case 43: gdi.Rectangle(I32(o + 8), I32(o + 12), I32(o + 16), I32(o + 20)); break;
            case 44: gdi.RoundRect(I32(o + 8), I32(o + 12), I32(o + 16), I32(o + 20), I32(o + 24), I32(o + 28)); break;
            case 45 or 46 or 47 or 55:   // ARC, CHORD, PIE, ARCTO
                gdi.Arc(I32(o + 8), I32(o + 12), I32(o + 16), I32(o + 20), (I32(o + 24), I32(o + 28)), (I32(o + 32), I32(o + 36)),
                    type switch { 46 => "chord", 47 => "pie", _ => "arc" }, toCurrent: type == 55);
                break;
            case 54: gdi.LineTo(I32(o + 8), I32(o + 12)); break;
            case 57: s.Clockwise = I32(o + 8) == 2; break;
            case 58: s.MiterLimit = U32(o + 8); break;
            case 59: gdi.BeginPath(); break;
            case 60: gdi.EndPath(); break;
            case 61: gdi.CloseFigure(); break;
            case 62 or 63 or 64:   // FILLPATH, STROKEANDFILLPATH, STROKEPATH
                if (gdi.TakePath() is { } d)
                    gdi.Draw(d, fill: type != 64, stroke: type != 62);
                break;
            case 65 or 66: break;   // FLATTENPATH, WIDENPATH: the path stays as is
            case 67:   // SELECTCLIPPATH
                gdi.SelectClip(gdi.TakePath(), I32(o + 8));
                break;
            case 68: gdi.AbortPath(); break;
            case 71:   // FILLRGN
                if (gdi.Objects.TryGetValue(I32(o + 28), out var fillBrush) && fillBrush is GdiBrush brush)
                    svg.Add($"<path d=\"{Region(o + 32, I32(o + 24))}\"{gdi.FillPaint(brush)}/>", s.Clip);
                break;
            case 72:   // FRAMERGN
                if (gdi.Objects.TryGetValue(I32(o + 28), out var frameBrush) && frameBrush is GdiBrush frame)
                    svg.Add($"<path d=\"{Region(o + 40, I32(o + 24))}\" fill=\"none\" stroke=\"{SvgWriter.Color(gdi.Resolve(frame.Color))}\" stroke-width=\"{Math.Max(1, I32(o + 32))}\"/>", s.Clip);
                break;
            case 74:   // PAINTRGN
                svg.Add($"<path d=\"{Region(o + 28, I32(o + 24))}\"{gdi.FillPaint(s.Brush)}/>", s.Clip);
                break;
            case 75:   // EXTSELECTCLIPRGN: region in device units
            {
                var length = I32(o + 8);
                gdi.SelectClip(length > 0 ? Region(o + 16, length) : null, I32(o + 12));
                break;
            }
            case 76 or 77:   // BITBLT, STRETCHBLT
                Blt(o, size, stretch: type == 77);
                break;
            case 78:   // MASKBLT: the source where the mask is set (the foreground operation), the destination elsewhere
            {
                var source = Dib.Decode(Span(o + I32(o + 84), I32(o + 88)), Span(o + I32(o + 92), I32(o + 96)), logicalPalette: PaletteFor(U32(o + 80)));
                if (source == null)
                    break;
                var mask = MaskBits(o, o + 112);
                var (cx, cy) = (I32(o + 32), I32(o + 36));
                gdi.Image(mask != null ? Masked(source, mask) : source, I32(o + 24), I32(o + 28), cx, cy, (I32(o + 44), I32(o + 48), cx, cy));
                break;
            }
            case 79:   // PLGBLT: the source rectangle mapped onto a parallelogram, masked like MASKBLT
            {
                var source = Dib.Decode(Span(o + I32(o + 96), I32(o + 100)), Span(o + I32(o + 104), I32(o + 108)), logicalPalette: PaletteFor(U32(o + 92)));
                if (source == null)
                    break;
                var mask = MaskBits(o, o + 124);
                gdi.Image(mask != null ? Masked(source, mask) : source, (I32(o + 24), I32(o + 28)), (I32(o + 32), I32(o + 36)), (I32(o + 40), I32(o + 44)),
                    (I32(o + 48), I32(o + 52), I32(o + 56), I32(o + 60)));
                break;
            }
            case 56 or 92:   // POLYDRAW, POLYDRAW16: points with their kinds (move, line, Bézier, closing)
            {
                var count = I32(o + 24);
                var points = type == 56 ? PointsL(o + 28, count) : PointsS(o + 28, count);
                PolyDraw(points, o + 28 + count * (type == 56 ? 8 : 4));
                break;
            }
            case 80:   // SETDIBITSTODEVICE
            {
                var bitmap = Dib.Decode(Span(o + I32(o + 48), I32(o + 52)), Span(o + I32(o + 56), I32(o + 60)), logicalPalette: PaletteFor(U32(o + 64)));
                if (bitmap != null)
                    gdi.Image(bitmap, I32(o + 24), I32(o + 28), I32(o + 40), I32(o + 44), (I32(o + 32), I32(o + 36), I32(o + 40), I32(o + 44)));
                break;
            }
            case 81:   // STRETCHDIBITS
            {
                var bitmap = Dib.Decode(Span(o + I32(o + 48), I32(o + 52)), Span(o + I32(o + 56), I32(o + 60)), logicalPalette: PaletteFor(U32(o + 64)));
                if (bitmap != null)
                    Raster(U32(o + 68), bitmap, IsMonochrome(o + I32(o + 48)), I32(o + 24), I32(o + 28), I32(o + 72), I32(o + 76), (I32(o + 32), I32(o + 36), I32(o + 40), I32(o + 44)));
                break;
            }
            case 82:   // EXTCREATEFONTINDIRECTW
                gdi.Objects[I32(o + 8)] = LogFont(o + 12);
                break;
            case 83 or 84:   // EXTTEXTOUTA, EXTTEXTOUTW
                TextOut(o, wide: type == 84);
                break;
            case 93 or 94:   // CREATEMONOBRUSH, CREATEDIBPATTERNBRUSHPT
            {
                var monochrome = type == 93 ? (gdi.Resolve(s.TextColor), gdi.Resolve(s.BkColor)) : ((uint, uint)?)null;
                var bitmap = Dib.Decode(Span(o + I32(o + 16), I32(o + 20)), Span(o + I32(o + 24), I32(o + 28)), monochrome: monochrome,
                    logicalPalette: type == 94 ? PaletteFor(U32(o + 12)) : null);
                gdi.Objects[I32(o + 8)] = new GdiBrush { Style = bitmap != null ? 3 : 0, Pattern = bitmap, Color = 0x808080 };
                break;
            }
            case 95:   // EXTCREATEPEN
            {
                var style = I32(o + 28);
                var count = I32(o + 48);
                double[]? dashes = null;
                if ((style & 0x0F) == 7 && count > 0)
                    dashes = Enumerable.Range(0, Math.Min(count, 16)).Select(i => (double)U32(o + 52 + i * 4)).ToArray();
                var brushStyle = I32(o + 36);
                gdi.Objects[I32(o + 8)] = new GdiPen
                {
                    Style = style, Width = U32(o + 32), Color = U32(o + 40), Geometric = (style & 0x10000) != 0, UserDashes = dashes,
                    Brush = brushStyle == 0 ? null : new GdiBrush { Style = brushStyle, Color = U32(o + 40), Hatch = I32(o + 44) },
                };
                break;
            }
            case 96 or 97:   // POLYTEXTOUTA, POLYTEXTOUTW
            {
                var count = I32(o + 36);
                for (var i = 0; i < count && i < 10000; i++)
                    TextAt(o, o + 40 + i * 40, type == 97);
                break;
            }
            case 108:   // SMALLTEXTOUT
                SmallText(o);
                break;
            case 114 or 116:   // ALPHABLEND, TRANSPARENTBLT
                Blend(o, transparent: type == 116);
                break;
            case 118:   // GRADIENTFILL
                GradientFill(o);
                break;
        }
    }

    /// <summary>
    /// The monochrome mask of MaskBlt and PlgBlt (offBmiMask, cbBmiMask, offBitsMask, cbBitsMask at
    /// <paramref name="field"/>, offsets from the record) as white where its bits are set: masks come without a color
    /// table, the bits decide.
    /// </summary>
    Bitmap? MaskBits(int record, int field)
    {
        var (offBmi, cbBmi, offBits, cbBits) = (I32(field), I32(field + 4), I32(field + 8), I32(field + 12));
        return cbBmi < 16 || cbBits <= 0 ? null : MaskFrom(record + offBmi, record + offBits, cbBits);
    }

    Bitmap? MaskFrom(int header, int bits, int length)
    {
        var width = I32(header + 4);
        var height = I32(header + 8);
        if (width <= 0 || height == 0 || I16(header + 14) != 1)
            return null;
        var rows = Math.Abs(height);
        var stride = (width + 31) / 32 * 4;
        if (stride * rows > length || bits + stride * rows > data.Length)
            return null;
        var rgba = new byte[width * rows * 4];
        for (var y = 0; y < rows; y++)
        {
            var row = bits + (height > 0 ? rows - 1 - y : y) * stride;
            for (var x = 0; x < width; x++)
            {
                var value = (data[row + x / 8] & (0x80 >> (x % 8))) != 0 ? (byte)255 : (byte)0;
                var i = (y * width + x) * 4;
                (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = (value, value, value, 255);
            }
        }
        return new Bitmap { Width = width, Height = rows, Rgba = rgba };
    }

    /// <summary>
    /// PolyDraw: every point moves (PT_MOVETO), draws a line (PT_LINETO) or is one of three Bézier points
    /// (PT_BEZIERTO); PT_CLOSEFIGURE closes the figure after its point (in a path, else with a line to its start).
    /// </summary>
    void PolyDraw(List<(double X, double Y)> points, int types)
    {
        var start = gdi.State.Position;
        for (var i = 0; i < points.Count && types + i < data.Length; i++)
        {
            var kind = data[types + i];
            switch (kind & 0x06)
            {
                case 0x06:
                    gdi.MoveTo(points[i].X, points[i].Y);
                    start = points[i];
                    break;
                case 0x02:
                    gdi.LineTo(points[i].X, points[i].Y);
                    break;
                case 0x04 when i + 2 < points.Count && types + i + 2 < data.Length:
                    gdi.PolyBezier([points[i], points[i + 1], points[i + 2]], fromCurrent: true);
                    i += 2;
                    kind = data[types + i];
                    break;
            }
            if ((kind & 0x01) == 0)
                continue;
            if (gdi.InPath)
                gdi.CloseFigure();
            else
                gdi.LineTo(start.X, start.Y);
            gdi.State.Position = start;
        }
    }

    // ------------------------------------------------------------------ EMF+

    void Comment(int o, int size)
    {
        var dataSize = (int)U32(o + 8);
        if (dataSize < 4 || U32(o + 12) != 0x2B464D45)   // "EMF+"
            return;
        plus ??= new EmfPlusPlayer(data, gdi, svg) { UnitsPerPixel = gdi.UnitsPerPixel };
        plus.Play(o + 16, Math.Min(dataSize - 4, size - 16));
    }

    // ------------------------------------------------------------------ helpers

    Xform Xform(int o) => new(F32(o), F32(o + 4), F32(o + 8), F32(o + 12), F32(o + 16), F32(o + 20));

    List<(double X, double Y)> PointsL(int o, int count)
    {
        var points = new List<(double X, double Y)>(Math.Max(0, Math.Min(count, 100000)));
        for (var i = 0; i < count && o + i * 8 + 8 <= data.Length; i++)
            points.Add((I32(o + i * 8), I32(o + i * 8 + 4)));
        return points;
    }

    List<(double X, double Y)> PointsS(int o, int count)
    {
        var points = new List<(double X, double Y)>(Math.Max(0, Math.Min(count, 100000)));
        for (var i = 0; i < count && o + i * 4 + 4 <= data.Length; i++)
            points.Add((I16(o + i * 4), I16(o + i * 4 + 2)));
        return points;
    }

    void Poly(uint type, List<(double X, double Y)> points)
    {
        switch (type)
        {
            case 2: gdi.PolyBezier(points, fromCurrent: false); break;
            case 3: gdi.Polyline(points, closed: true, fill: true); break;
            case 4:
                if (gdi.InPath && points.Count > 0)
                {
                    gdi.MoveTo(points[0].X, points[0].Y);
                    foreach (var p in points.Skip(1)) gdi.LineTo(p.X, p.Y);
                }
                else
                {
                    gdi.Polyline(points, closed: false, fill: false);
                }
                break;
            case 5: gdi.PolyBezier(points, fromCurrent: true); break;
            case 6:
                foreach (var p in points) gdi.LineTo(p.X, p.Y);
                break;
        }
    }

    void PolyPoly(int o, bool polygon, bool wide)
    {
        var polygons = I32(o + 24);
        var counts = Enumerable.Range(0, Math.Max(0, Math.Min(polygons, 10000))).Select(i => I32(o + 32 + i * 4)).ToList();
        var offset = o + 32 + polygons * 4;
        var list = new List<IReadOnlyList<(double X, double Y)>>();
        foreach (var count in counts)
        {
            list.Add(wide ? PointsL(offset, count) : PointsS(offset, count));
            offset += count * (wide ? 8 : 4);
        }
        gdi.PolyPolygon(list, closed: polygon, fill: polygon);
    }

    /// <summary>RGNDATA (header plus rectangles, device units) as path data.</summary>
    string Region(int o, int length)
    {
        if (length < 32)
            return "";
        var count = I32(o + 8);
        var sb = new StringBuilder();
        for (var i = 0; i < count && 32 + (i + 1) * 16 <= length; i++)
        {
            var r = o + 32 + i * 16;
            sb.Append($"M{I32(r)} {I32(r + 4)}H{I32(r + 8)}V{I32(r + 12)}H{I32(r)}Z");
        }
        return sb.ToString();
    }

    GdiFont LogFont(int o)
    {
        var face = Encoding.Unicode.GetString(Span(o + 28, 64)).Split('\0')[0];
        return new GdiFont
        {
            Height = I32(o), Width = I32(o + 4), Escapement = I32(o + 8), Weight = I32(o + 16),
            Italic = data[o + 20] != 0, Underline = data[o + 21] != 0, StrikeOut = data[o + 22] != 0, CharSet = data[o + 23],
            Face = face.Length > 0 ? face : "Arial",
        };
    }

    void TextOut(int o, bool wide) => TextAt(o, o + 36, wide);

    /// <summary>An EMRTEXT structure at <paramref name="t"/> (offsets relative to the record start).</summary>
    void TextAt(int o, int t, bool wide)
    {
        var refX = I32(t);
        var refY = I32(t + 4);
        var chars = I32(t + 8);
        var offString = I32(t + 12);
        var options = U32(t + 16);
        var rect = (L: (double)I32(t + 20), T: (double)I32(t + 24), R: (double)I32(t + 28), B: (double)I32(t + 32));
        var offDx = I32(t + 36);
        if (chars <= 0 || chars > 100000)
            return;
        string text;
        if ((options & 0x10) != 0)   // ETO_GLYPH_INDEX: glyph ids, mapped back to characters through the font
            text = GlyphText(o + offString, chars);
        else
            text = wide ? Encoding.Unicode.GetString(Span(o + offString, chars * 2)) : gdi.State.Font.Encoding.GetString(Span(o + offString, chars));
        double[]? dx = null;
        if (offDx > 0)
        {
            var pdy = (options & 0x2000) != 0;
            dx = new double[text.Length];
            for (var i = 0; i < text.Length && i < chars; i++)
                dx[i] = I32(o + offDx + i * (pdy ? 8 : 4));
        }
        var valid = rect.R > rect.L && rect.B > rect.T;
        var opaque = (options & 0x02) != 0 && valid ? rect : ((double L, double T, double R, double B)?)null;
        var clip = (options & 0x04) != 0 && valid ? rect : ((double L, double T, double R, double B)?)null;
        gdi.Text(refX, refY, text, dx, opaque, clip);
    }

    string GlyphText(int o, int count)
    {
        var font = gdi.State.Font;
        var resolved = Text.FontResolver.Default.Resolve(font.Face, font.Weight >= 600, font.Italic);
        var map = resolved.Data.GlyphToChar();
        var sb = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            var glyph = (ushort)I16(o + i * 2);
            sb.Append(map.TryGetValue(glyph, out var c) ? c : ' ');
        }
        return sb.ToString();
    }

    void SmallText(int o)
    {
        var x = I32(o + 8);
        var y = I32(o + 12);
        var chars = I32(o + 16);
        var options = U32(o + 20);
        var hasRect = (options & 0x100) == 0;   // ETO_NO_RECT absent
        var textOffset = o + 36 + (hasRect ? 16 : 0);
        var text = (options & 0x200) != 0 ? gdi.State.Font.Encoding.GetString(Span(textOffset, chars)) : Encoding.Unicode.GetString(Span(textOffset, chars * 2));
        gdi.Text(x, y, text, null, null, null);
    }

    /// <summary>DIB sources are bottom-up: GDI's source y counts from the top of the image.</summary>
    static int SourceY(Bitmap bitmap, int y, int height) => y;

    void Blt(int o, int size, bool stretch)
    {
        var (x, y, cx, cy) = (I32(o + 24), I32(o + 28), I32(o + 32), I32(o + 36));
        var rop = U32(o + 40);
        var (sx, sy) = (I32(o + 44), I32(o + 48));
        var offBmi = I32(o + 84);
        var cbBmi = I32(o + 88);
        var offBits = I32(o + 92);
        var cbBits = I32(o + 96);
        var (scx, scy) = stretch ? (I32(o + 100), I32(o + 104)) : (cx, cy);
        if (cbBmi == 0)
        {
            // No source bitmap: a pattern or constant raster operation.
            var color = rop switch { 0x00000042 => "#000000", 0x00FF0062 => "#ffffff", _ => null };
            if (rop == 0x00F00021)
                gdi.Draw(gdi.RectPath(x, y, x + cx, y + cy), fill: true, stroke: false);
            else if (color != null && !gdi.SuppressDrawing)
                svg.Add($"<path d=\"{gdi.RectPath(x, y, x + cx, y + cy)}\" fill=\"{color}\"/>", gdi.State.Clip);
            return;
        }
        var bitmap = Dib.Decode(Span(o + offBmi, cbBmi), Span(o + offBits, cbBits), logicalPalette: PaletteFor(U32(o + 80)));
        if (bitmap != null)
            Raster(rop, bitmap, IsMonochrome(o + offBmi), x, y, cx, cy, (sx, sy, scx, scy));
    }

    /// <summary>The selected palette for bitmaps whose color table indexes it (DIB_PAL_COLORS).</summary>
    uint[]? PaletteFor(uint usage) => usage == 1 ? gdi.State.Palette.Entries : null;

    /// <summary>Palette entries (red, green, blue, flags) as COLORREFs.</summary>
    uint[] PaletteEntries(int offset, int count)
    {
        var entries = new uint[Math.Clamp(count, 0, 1024)];
        for (var i = 0; i < entries.Length; i++)
            entries[i] = (uint)(data[offset + i * 4] | (data[offset + i * 4 + 1] << 8) | (data[offset + i * 4 + 2] << 16));
        return entries;
    }

    bool IsMonochrome(int header) => I32(header) >= 12 && (I32(header) == 12 ? I16(header + 10) : I16(header + 14)) == 1;

    /// <summary>A bitmap drawn with a raster operation (SRCCOPY, or the mask pairs GDI+ writes for transparency).</summary>
    void Raster(uint rop, Bitmap bitmap, bool monochrome, double x, double y, double w, double h, (int X, int Y, int W, int H) source)
    {
        switch (rop)
        {
            case 0x00EE0086 when monochrome:   // SRCPAINT with a mask: wait for the image
                pendingMask = (bitmap, x, y, w, h, source);
                return;
            case 0x008800C6 when monochrome && pendingMask == null:   // SRCAND of an icon's mask: wait for its image
                FlushAnd();
                pendingAnd = (bitmap, x, y, w, h, source);
                return;
            case 0x00660046 when pendingAnd is { } and && Math.Abs(and.X - x) < 1 && Math.Abs(and.Y - y) < 1 && Math.Abs(and.W - w) < 1 && Math.Abs(and.H - h) < 1:
                // SRCINVERT: the icon's image over its mask, opaque where the mask is black.
                pendingAnd = null;
                gdi.Image(Masked(bitmap, and.Mask, blackOpaque: true), x, y, w, h, source);
                return;
            case 0x008800C6:   // SRCAND: the image of a mask pair; alone, white is transparent
                if (pendingMask is { } mask && Math.Abs(mask.X - x) < 1 && Math.Abs(mask.Y - y) < 1 && Math.Abs(mask.W - w) < 1 && Math.Abs(mask.H - h) < 1)
                {
                    pendingMask = null;
                    gdi.Image(Masked(bitmap, mask.Mask), x, y, w, h, source);
                }
                else
                {
                    gdi.Image(bitmap.WithTransparentColor(0xFFFFFF), x, y, w, h, source);
                }
                return;
            case 0x00EE0086:   // SRCPAINT: black adds nothing
                gdi.Image(bitmap.WithTransparentColor(0), x, y, w, h, source);
                return;
            case 0x00AA0029 or 0x00550009:   // NOP, DSTINVERT
                return;
            default:
                gdi.Image(bitmap, x, y, w, h, source);
                return;
        }
    }

    /// <summary>The image with alpha from a mask whose white pixels (or black ones) mark the opaque part.</summary>
    static Bitmap Masked(Bitmap image, Bitmap mask, bool blackOpaque = false)
    {
        if (image.Rgba == null || mask.Rgba == null)
            return image;
        var rgba = (byte[])image.Rgba.Clone();
        for (var y = 0; y < image.Height; y++)
        {
            var my = y * mask.Height / Math.Max(1, image.Height);
            for (var x = 0; x < image.Width; x++)
            {
                var mx = x * mask.Width / Math.Max(1, image.Width);
                var m = (my * mask.Width + mx) * 4;
                if (mask.Rgba[m] < 128 != blackOpaque)
                    rgba[(y * image.Width + x) * 4 + 3] = 0;
            }
        }
        return new Bitmap { Width = image.Width, Height = image.Height, Rgba = rgba };
    }

    bool IsMaskPartner(uint type, int o) => type switch
    {
        81 => U32(o + 68) == 0x008800C6,
        76 or 77 => U32(o + 40) == 0x008800C6,
        19 or 21 or 70 => true,   // fill mode, stretch mode and comments may sit between the pair
        _ => false,
    };

    bool IsIconPartner(uint type, int o) => type switch
    {
        81 => U32(o + 68) == 0x00660046,
        76 or 77 => U32(o + 40) == 0x00660046,
        19 or 21 or 70 => true,
        _ => false,
    };

    /// <summary>An icon's mask without its image: drawn alone, its white transparent.</summary>
    void FlushAnd()
    {
        if (pendingAnd is not { } and)
            return;
        pendingAnd = null;
        gdi.Image(and.Mask.WithTransparentColor(0xFFFFFF), and.X, and.Y, and.W, and.H, and.Source);
    }

    void FlushMask()
    {
        if (pendingMask is not { } mask)
            return;
        pendingMask = null;
        gdi.Image(mask.Mask.WithTransparentColor(0), mask.X, mask.Y, mask.W, mask.H, mask.Source);
    }

    void Blend(int o, bool transparent)
    {
        var (x, y, cx, cy) = (I32(o + 24), I32(o + 28), I32(o + 32), I32(o + 36));
        var function = U32(o + 40);
        var (sx, sy) = (I32(o + 44), I32(o + 48));
        var offBmi = I32(o + 84);
        var cbBmi = I32(o + 88);
        var offBits = I32(o + 92);
        var cbBits = I32(o + 96);
        var (scx, scy) = (I32(o + 100), I32(o + 104));
        var perPixel = !transparent && ((function >> 24) & 0xFF) == 1;   // AC_SRC_ALPHA
        var bitmap = Dib.Decode(Span(o + offBmi, cbBmi), Span(o + offBits, cbBits), useAlpha: perPixel, logicalPalette: PaletteFor(U32(o + 80)));
        if (bitmap == null)
            return;
        if (transparent)
            bitmap = bitmap.WithTransparentColor(function);
        var constant = transparent ? 255 : (function >> 16) & 0xFF;
        if (constant < 255 && bitmap.Rgba != null)
        {
            var copy = (byte[])bitmap.Rgba.Clone();
            for (var i = 3; i < copy.Length; i += 4) copy[i] = (byte)(copy[i] * constant / 255);
            bitmap = new Bitmap { Width = bitmap.Width, Height = bitmap.Height, Rgba = copy };
        }
        gdi.Image(bitmap, x, y, cx, cy, (sx, sy, scx, scy));
    }

    void GradientFill(int o)
    {
        var vertexCount = I32(o + 24);
        var meshCount = I32(o + 28);
        var mode = U32(o + 32);
        var vertices = new List<(double X, double Y, uint Color)>();
        for (var i = 0; i < vertexCount; i++)
        {
            var v = o + 36 + i * 16;
            var color = (uint)((data[v + 9]) | (data[v + 11] << 8) | (data[v + 13] << 16));
            vertices.Add((I32(v), I32(v + 4), color));
        }
        var mesh = o + 36 + vertexCount * 16;
        for (var i = 0; i < meshCount; i++)
        {
            if (mode == 2)   // GRADIENT_FILL_TRIANGLE: average color (SVG has no triangle meshes)
            {
                var (a, b, c) = (U32(mesh + i * 12), U32(mesh + i * 12 + 4), U32(mesh + i * 12 + 8));
                if (a >= vertices.Count || b >= vertices.Count || c >= vertices.Count) continue;
                var p = new PathBuilder();
                p.MoveTo(gdi.ToDevice(vertices[(int)a].X, vertices[(int)a].Y));
                p.LineTo(gdi.ToDevice(vertices[(int)b].X, vertices[(int)b].Y));
                p.LineTo(gdi.ToDevice(vertices[(int)c].X, vertices[(int)c].Y));
                p.Close();
                svg.Add($"<path d=\"{p}\" fill=\"{SvgWriter.Color(vertices[(int)a].Color)}\"/>", gdi.State.Clip);
                continue;
            }
            var (ul, lr) = (U32(mesh + i * 8), U32(mesh + i * 8 + 4));
            if (ul >= vertices.Count || lr >= vertices.Count) continue;
            var (v0, v1) = (vertices[(int)ul], vertices[(int)lr]);
            var id = svg.NewId();
            var (x0, y0) = gdi.ToDevice(v0.X, v0.Y);
            var (x1, y1) = gdi.ToDevice(v1.X, v1.Y);
            var vertical = mode == 1;
            svg.Def($"<linearGradient id=\"{id}\" gradientUnits=\"userSpaceOnUse\" x1=\"{SvgWriter.Num(x0)}\" y1=\"{SvgWriter.Num(y0)}\" x2=\"{SvgWriter.Num(vertical ? x0 : x1)}\" y2=\"{SvgWriter.Num(vertical ? y1 : y0)}\"><stop offset=\"0\" stop-color=\"{SvgWriter.Color(v0.Color)}\"/><stop offset=\"1\" stop-color=\"{SvgWriter.Color(v1.Color)}\"/></linearGradient>");
            svg.Add($"<path d=\"{gdi.RectPath(v0.X, v0.Y, v1.X, v1.Y)}\" fill=\"url(#{id})\"/>", gdi.State.Clip);
        }
    }
}
