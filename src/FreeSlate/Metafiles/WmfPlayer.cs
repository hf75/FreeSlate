using System.Buffers.Binary;
using System.Text;

namespace FreeSlate.Metafiles;

/// <summary>
/// Plays a Windows Metafile ([MS-WMF]) into SVG. The placeable header's bounding box becomes the view box;
/// the window origin and extent map logical coordinates onto it. A WMF that carries an embedded EMF in escape
/// records is drawn from the EMF.
/// </summary>
public sealed class WmfPlayer
{
    static readonly Encoding Ansi;

    static WmfPlayer()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Ansi = Encoding.GetEncoding(1252);
    }

    readonly byte[] data;
    readonly SvgWriter svg;
    readonly List<object?> slots = [];
    GdiContext gdi = null!;

    WmfPlayer(byte[] data, string idPrefix)
    {
        this.data = data;
        svg = new SvgWriter(idPrefix);
    }

    public static bool IsWmf(byte[] data) =>
        data.Length >= 18 && (BinaryPrimitives.ReadUInt32LittleEndian(data) == 0x9AC6CDD7
                              || (BinaryPrimitives.ReadUInt16LittleEndian(data) is 1 or 2 && BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2)) == 9));

    public static string? ToSvg(byte[] data, string idPrefix = "w", double? displayWidth = null)
    {
        if (!IsWmf(data))
            return null;
        return new WmfPlayer(data, idPrefix) { displayWidth = displayWidth }.Play();
    }

    double? displayWidth;

    short I16(int o) => o + 2 <= data.Length ? BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(o)) : (short)0;
    ushort U16(int o) => o + 2 <= data.Length ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(o)) : (ushort)0;
    uint U32(int o) => o + 4 <= data.Length ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(o)) : 0;

    ReadOnlySpan<byte> Span(int offset, int length) =>
        offset >= 0 && length > 0 && offset + length <= data.Length ? data.AsSpan(offset, length) : ReadOnlySpan<byte>.Empty;

    string? Play()
    {
        var start = 0;
        double x = 0, y = 0, w = 0, h = 0;
        var inch = 1440.0;
        if (U32(0) == 0x9AC6CDD7)
        {
            var (left, top, right, bottom) = (I16(6), I16(8), I16(10), I16(12));
            inch = Math.Max(1, (int)U16(14));
            (x, y, w, h) = (Math.Min(left, right), Math.Min(top, bottom), Math.Abs(right - left), Math.Abs(bottom - top));
            start = 22;
        }
        var headerWords = U16(start + 2);
        var offset = start + headerWords * 2;

        // An embedded EMF (META_ESCAPE_ENHANCED_METAFILE) is a better picture than the WMF records.
        if (EmbeddedEmf(offset) is { } emf && EmfPlayer.ToSvg(emf, svg.NewId() + "e") is { } emfSvg)
            return emfSvg;

        // Without a placeable header, the first window origin and extent define the picture.
        if (w <= 0 || h <= 0)
        {
            var (org, ext) = FindWindow(offset);
            (x, y, w, h) = (org.X, org.Y, Math.Abs(ext.X), Math.Abs(ext.Y));
            if (w <= 0 || h <= 0) (w, h) = (1000, 1000);
        }

        gdi = new GdiContext(svg, inch / 25.4) { UnitsPerPixel = displayWidth is > 0 ? w / displayWidth.Value : inch / 96 };
        var s = gdi.State;
        s.MapMode = 8;
        s.WindowOrg = (x, y);
        s.WindowExt = (w, h);
        s.ViewportOrg = (x, y);
        s.ViewportExt = (w, h);
        s.Font = new GdiFont { Height = -12, Face = "Arial" };

        while (offset + 6 <= data.Length)
        {
            var words = U32(offset);
            var function = U16(offset + 4);
            if (words < 3 || offset + words * 2 > data.Length)
                break;
            if (function == 0)
                break;
            try
            {
                Record(function, offset + 6, (int)words * 2 - 6);
            }
            catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException or OverflowException)
            {
            }
            offset += (int)words * 2;
        }
        return svg.Finish(x, y, w, h);
    }

    ((double X, double Y), (double X, double Y)) FindWindow(int offset)
    {
        (double, double) org = (0, 0), ext = (0, 0);
        while (offset + 6 <= data.Length)
        {
            var words = U32(offset);
            var function = U16(offset + 4);
            if (words < 3 || function == 0) break;
            if (function == 0x020B) org = (I16(offset + 8), I16(offset + 6));
            if (function == 0x020C && ext == (0, 0)) ext = (I16(offset + 8), I16(offset + 6));
            offset += (int)words * 2;
        }
        return (org, ext);
    }

    byte[]? EmbeddedEmf(int offset)
    {
        using var emf = new MemoryStream();
        var found = false;
        while (offset + 6 <= data.Length)
        {
            var words = U32(offset);
            var function = U16(offset + 4);
            if (words < 3 || function == 0) break;
            var p = offset + 6;
            // META_ESCAPE, MFCOMMENT (0x000F), identifier "WMFC", comment type 1 (enhanced metafile).
            if (function == 0x0626 && U16(p) == 0x000F && U32(p + 4) == 0x43464D57 && U32(p + 8) == 1)
            {
                var size = (int)U32(p + 30);   // CommentRecordCount at +22, CurrentRecordSize at +26, RemainingBytes at +30?
                var current = (int)U32(p + 26);
                var chunk = Span(p + 38, current);
                emf.Write(chunk);
                found = true;
                _ = size;
            }
            offset += (int)words * 2;
        }
        return found && emf.Length > 88 ? emf.ToArray() : null;
    }

    int AddObject(object item)
    {
        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = item;
                return i;
            }
        }
        slots.Add(item);
        return slots.Count - 1;
    }

    List<(double X, double Y)> Points(int o, int count)
    {
        var points = new List<(double X, double Y)>(Math.Max(0, Math.Min(count, 100000)));
        for (var i = 0; i < count && o + i * 4 + 4 <= data.Length; i++)
            points.Add((I16(o + i * 4), I16(o + i * 4 + 2)));
        return points;
    }

    void Record(int function, int p, int size)
    {
        var s = gdi.State;
        switch (function)
        {
            case 0x0201: s.BkColor = U32(p); break;
            case 0x0234:   // SELECTPALETTE
                if (U16(p) < slots.Count && slots[U16(p)] is GdiPalette palette)
                    gdi.Select(palette);
                break;
            case 0x0035: break;   // REALIZEPALETTE: the selected palette is used as it is
            case 0x0037 or 0x0436:   // SETPALENTRIES, ANIMATEPALETTE: entries of the selected palette
                if (s.Palette != GdiPalette.Default)
                {
                    var (first, count) = ((int)U16(p), (int)U16(p + 2));
                    var merged = s.Palette.Entries.ToList();
                    while (merged.Count < first + count)
                        merged.Add(0);
                    var entries = PaletteEntries(p + 4, count);
                    for (var i = 0; i < entries.Length; i++)
                        merged[first + i] = entries[i];
                    s.Palette.Entries = [.. merged];
                }
                break;
            case 0x0139:   // RESIZEPALETTE
                if (s.Palette != GdiPalette.Default)
                {
                    var entries = s.Palette.Entries;
                    Array.Resize(ref entries, Math.Clamp((int)U16(p), 0, 1024));
                    s.Palette.Entries = entries;
                }
                break;
            case 0x0102: s.BkMode = U16(p); break;
            case 0x0104: s.Rop2 = U16(p); break;
            case 0x0106: s.PolyFillMode = U16(p); break;
            case 0x0108: s.TextCharExtra = I16(p); break;
            case 0x0209: s.TextColor = U32(p); break;
            case 0x020B: s.WindowOrg = (I16(p + 2), I16(p)); break;
            case 0x020C: s.WindowExt = (I16(p + 2), I16(p)); break;
            case 0x020D: s.ViewportOrg = (I16(p + 2), I16(p)); break;
            case 0x020E: s.ViewportExt = (I16(p + 2), I16(p)); break;
            case 0x020F: s.WindowOrg = (s.WindowOrg.X + I16(p + 2), s.WindowOrg.Y + I16(p)); break;
            case 0x0211: s.ViewportOrg = (s.ViewportOrg.X + I16(p + 2), s.ViewportOrg.Y + I16(p)); break;
            case 0x0410:
                s.WindowExt = (s.WindowExt.X * I16(p + 6) / Math.Max(1.0, I16(p + 4)), s.WindowExt.Y * I16(p + 2) / Math.Max(1.0, I16(p)));
                break;
            case 0x0412:
                s.ViewportExt = (s.ViewportExt.X * I16(p + 6) / Math.Max(1.0, I16(p + 4)), s.ViewportExt.Y * I16(p + 2) / Math.Max(1.0, I16(p)));
                break;
            case 0x0213: gdi.LineTo(I16(p + 2), I16(p)); break;
            case 0x0214: gdi.MoveTo(I16(p + 2), I16(p)); break;
            case 0x0220: gdi.OffsetClip(I16(p + 2), I16(p)); break;   // OFFSETCLIPRGN
            case 0x0415: gdi.ExcludeClipRect(I16(p + 6), I16(p + 4), I16(p + 2), I16(p)); break;
            case 0x0416: gdi.IntersectClipRect(I16(p + 6), I16(p + 4), I16(p + 2), I16(p)); break;
            case 0x0817 or 0x081A or 0x0830:
                gdi.Arc(I16(p + 14), I16(p + 12), I16(p + 10), I16(p + 8), (I16(p + 6), I16(p + 4)), (I16(p + 2), I16(p)),
                    function switch { 0x081A => "pie", 0x0830 => "chord", _ => "arc" });
                break;
            case 0x0418: gdi.Ellipse(I16(p + 6), I16(p + 4), I16(p + 2), I16(p)); break;
            case 0x041B: gdi.Rectangle(I16(p + 6), I16(p + 4), I16(p + 2), I16(p)); break;
            case 0x061C: gdi.RoundRect(I16(p + 10), I16(p + 8), I16(p + 6), I16(p + 4), I16(p + 2), I16(p)); break;
            case 0x061D:   // PATBLT
            {
                var rop = U32(p);
                var (h, w, y, x) = ((double)I16(p + 4), (double)I16(p + 6), (double)I16(p + 8), (double)I16(p + 10));
                var color = rop switch { 0x00000042 => "#000000", 0x00FF0062 => "#ffffff", _ => null };
                if (rop == 0x00F00021) gdi.Draw(gdi.RectPath(x, y, x + w, y + h), fill: true, stroke: false);
                else if (color != null) svg.Add($"<path d=\"{gdi.RectPath(x, y, x + w, y + h)}\" fill=\"{color}\"/>", s.Clip);
                break;
            }
            case 0x001E: gdi.Save(); break;
            case 0x0127: gdi.Restore(I16(p)); break;
            case 0x041F:
            {
                var (px, py) = gdi.ToDevice(I16(p + 6), I16(p + 4));
                svg.Add($"<rect x=\"{SvgWriter.Num(px)}\" y=\"{SvgWriter.Num(py)}\" width=\"1\" height=\"1\" fill=\"{SvgWriter.Color(gdi.Resolve(U32(p)))}\"/>", s.Clip);
                break;
            }
            case 0x0521:   // TEXTOUT
            {
                var length = I16(p);
                var text = Ansi.GetString(Span(p + 2, length));
                var padded = (length + 1) & ~1;
                gdi.Text(I16(p + 2 + padded + 2), I16(p + 2 + padded), text, null, null, null);
                break;
            }
            case 0x0A32:   // EXTTEXTOUT
            {
                var (y, x) = (I16(p), I16(p + 2));
                var length = I16(p + 4);
                var options = U16(p + 6);
                var q = p + 8;
                (double L, double T, double R, double B)? rect = null;
                if ((options & 0x06) != 0 && size >= 16 + length)
                {
                    rect = (I16(q), I16(q + 2), I16(q + 4), I16(q + 6));
                    q += 8;
                }
                var text = Ansi.GetString(Span(q, length));
                q += (length + 1) & ~1;
                double[]? dx = null;
                if (q + length * 2 <= p + size)
                    dx = Enumerable.Range(0, length).Select(i => (double)I16(q + i * 2)).ToArray();
                gdi.Text(x, y, text, dx, (options & 0x02) != 0 ? rect : null, (options & 0x04) != 0 ? rect : null);
                break;
            }
            case 0x0324 or 0x0325:   // POLYGON, POLYLINE
            {
                var points = Points(p + 2, I16(p));
                gdi.Polyline(points, closed: function == 0x0324, fill: function == 0x0324);
                break;
            }
            case 0x0538:   // POLYPOLYGON
            {
                var count = U16(p);
                var counts = Enumerable.Range(0, count).Select(i => (int)U16(p + 2 + i * 2)).ToList();
                var q = p + 2 + count * 2;
                var polygons = new List<IReadOnlyList<(double X, double Y)>>();
                foreach (var n in counts)
                {
                    polygons.Add(Points(q, n));
                    q += n * 4;
                }
                gdi.PolyPolygon(polygons, closed: true, fill: true);
                break;
            }
            case 0x012D:   // SELECTOBJECT
            {
                var index = U16(p);
                if (index < slots.Count && slots[index] is { } item)
                {
                    if (item is Region region) gdi.SelectClip(RegionPath(region), 5);
                    else gdi.Select(item);
                }
                break;
            }
            case 0x01F0:   // DELETEOBJECT
            {
                var index = U16(p);
                if (index < slots.Count) slots[index] = null;
                break;
            }
            case 0x012E: s.TextAlign = U16(p); break;
            case 0x012C:   // SELECTCLIPREGION
            {
                var index = U16(p);
                gdi.SelectClip(index < slots.Count && slots[index] is Region region ? RegionPath(region) : null, 5);
                break;
            }
            case 0x0228:   // FILLREGION
                if (U16(p) < slots.Count && slots[U16(p)] is Region fill && U16(p + 2) < slots.Count && slots[U16(p + 2)] is GdiBrush brush)
                    svg.Add($"<path d=\"{RegionPath(fill)}\"{gdi.FillPaint(brush)}/>", s.Clip);
                break;
            case 0x012B:   // PAINTREGION
                if (U16(p) < slots.Count && slots[U16(p)] is Region paint)
                    svg.Add($"<path d=\"{RegionPath(paint)}\"{gdi.FillPaint(s.Brush)}/>", s.Clip);
                break;
            case 0x02FA:   // CREATEPENINDIRECT
                AddObject(new GdiPen { Style = U16(p), Width = I16(p + 2), Color = U32(p + 6) });
                break;
            case 0x02FB:   // CREATEFONTINDIRECT
            {
                var face = Ansi.GetString(Span(p + 18, Math.Min(32, size - 18))).Split('\0')[0];
                AddObject(new GdiFont
                {
                    Height = I16(p), Width = I16(p + 2), Escapement = I16(p + 4), Weight = I16(p + 8),
                    Italic = data[p + 10] != 0, Underline = data[p + 11] != 0, StrikeOut = data[p + 12] != 0, CharSet = data[p + 13],
                    Face = face.Length > 0 ? face : "Arial",
                });
                break;
            }
            case 0x02FC:   // CREATEBRUSHINDIRECT
                AddObject(new GdiBrush { Style = U16(p), Color = U32(p + 2), Hatch = U16(p + 6) });
                break;
            case 0x0142:   // DIBCREATEPATTERNBRUSH
            {
                // A monochrome pattern (BS_PATTERN) is drawn in the text and background colors, as GDI paints monochrome
                // brushes (measured: an ActiveX toggle button's pink 50 % dither of white and its background color).
                var monochrome = U16(p) == 3 ? (gdi.Resolve(s.TextColor), gdi.Resolve(s.BkColor)) : ((uint, uint)?)null;
                var bitmap = DibAt(p + 4, size - 4, U16(p + 2), monochrome);
                AddObject(new GdiBrush { Style = bitmap != null ? 3 : 0, Pattern = bitmap, Color = 0x808080 });
                break;
            }
            case 0x01F9: AddObject(new GdiBrush { Color = 0x808080 }); break;   // CREATEPATTERNBRUSH (old bitmaps)
            case 0x00F7:   // CREATEPALETTE: version, entry count, entries (red, green, blue, flags)
                AddObject(new GdiPalette { Entries = PaletteEntries(p + 4, U16(p + 2)) });
                break;
            case 0x06FF: AddObject(ReadRegion(p)); break;                       // CREATEREGION
            case 0x0940:   // DIBBITBLT
            {
                var rop = U32(p);
                var hasBitmap = size > 22;
                if (!hasBitmap) break;
                var (sy, sx, h, w, y, x) = (I16(p + 4), I16(p + 6), I16(p + 8), I16(p + 10), I16(p + 12), I16(p + 14));
                DrawDib(rop, p + 16, size - 16, x, y, w, h, (sx, sy, w, h));
                break;
            }
            case 0x0B41:   // DIBSTRETCHBLT
            {
                var rop = U32(p);
                var hasBitmap = size > 26;
                if (!hasBitmap) break;
                var (sh, sw, sy, sx) = (I16(p + 4), I16(p + 6), I16(p + 8), I16(p + 10));
                var (dh, dw, dy, dx) = (I16(p + 12), I16(p + 14), I16(p + 16), I16(p + 18));
                DrawDib(rop, p + 20, size - 20, dx, dy, dw, dh, (sx, sy, sw, sh));
                break;
            }
            case 0x0F43:   // STRETCHDIB
            {
                var rop = U32(p);
                var (sh, sw, sy, sx) = (I16(p + 6), I16(p + 8), I16(p + 10), I16(p + 12));
                var (dh, dw, dy, dx) = (I16(p + 14), I16(p + 16), I16(p + 18), I16(p + 20));
                DrawDib(rop, p + 22, size - 22, dx, dy, dw, dh, (sx, sy, sw, sh), U16(p + 4));
                break;
            }
            case 0x0D33:   // SETDIBTODEV
            {
                var (h, w, y, x) = (I16(p + 10), I16(p + 12), I16(p + 14), I16(p + 16));
                DrawDib(0x00CC0020, p + 18, size - 18, x, y, w, h, null, U16(p));
                break;
            }
        }
    }

    /// <summary>Palette entries (red, green, blue, flags) as COLORREFs.</summary>
    uint[] PaletteEntries(int offset, int count)
    {
        var entries = new uint[Math.Clamp(count, 0, 1024)];
        for (var i = 0; i < entries.Length; i++)
            entries[i] = (uint)(data[offset + i * 4] | (data[offset + i * 4 + 1] << 8) | (data[offset + i * 4 + 2] << 16));
        return entries;
    }

    /// <summary>A DIB; with <paramref name="usage"/> DIB_PAL_COLORS (1) its color table indexes the selected palette.</summary>
    Bitmap? DibAt(int p, int length, int usage = 0, (uint Zero, uint One)? monochrome = null)
    {
        if (length < 12)
            return null;
        var headerSize = (int)U32(p);
        var bpp = headerSize == 12 ? U16(p + 10) : U16(p + 14);
        var compression = headerSize == 12 ? 0 : (int)U32(p + 16);
        var colorsUsed = headerSize == 12 ? 0 : (int)U32(p + 32);
        var paletteSize = bpp <= 8 ? (colorsUsed > 0 ? colorsUsed : 1 << bpp) * (usage == 1 ? 2 : headerSize == 12 ? 3 : 4) : compression == 3 && headerSize == 40 ? 12 : 0;
        var bitsOffset = headerSize + paletteSize;
        if (bitsOffset > length)
            return null;
        return Dib.Decode(Span(p, bitsOffset), Span(p + bitsOffset, length - bitsOffset), monochrome: monochrome,
            logicalPalette: usage == 1 ? gdi.State.Palette.Entries : null);
    }

    void DrawDib(uint rop, int p, int length, double x, double y, double w, double h, (int X, int Y, int W, int H)? source, int usage = 0)
    {
        if (rop is 0x008800C6 or 0x00AA0029 or 0x00550009)
            return;
        var bitmap = DibAt(p, length, usage);
        if (bitmap == null)
            return;
        if (rop == 0x00EE0086) bitmap = bitmap.WithTransparentColor(0);
        gdi.Image(bitmap, x, y, w, h, source);
    }

    sealed record Region(List<(int L, int T, int R, int B)> Rects);

    Region ReadRegion(int p)
    {
        // Region header (10 words), then scans: count, top, bottom, pairs of left/right, count again.
        var rects = new List<(int, int, int, int)>();
        var scans = I16(p + 14);
        var q = p + 20;
        for (var i = 0; i < scans && q + 6 <= data.Length; i++)
        {
            var count = U16(q);
            var top = I16(q + 2);
            var bottom = I16(q + 4);
            for (var k = 0; k + 1 < count; k += 2)
                rects.Add((I16(q + 6 + k * 2), top, I16(q + 8 + k * 2), bottom));
            q += 6 + count * 2 + 2;
        }
        return new Region(rects);
    }

    string RegionPath(Region region)
    {
        var sb = new StringBuilder();
        foreach (var (l, t, r, b) in region.Rects)
            sb.Append(gdi.RectPath(l, t, r, b));
        return sb.ToString();
    }
}
