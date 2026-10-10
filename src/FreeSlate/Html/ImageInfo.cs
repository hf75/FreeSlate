using System.Buffers.Binary;

namespace FreeSlate.Html;

/// <summary>Reads the pixel size and resolution of PNG, JPEG, GIF, BMP, TIFF and JPEG XR files.</summary>
public static class ImageInfo
{
    /// <summary>
    /// The size in CSS pixels (96 dpi) taking the image's own resolution into account, or <paramref name="dpi"/> when
    /// positive (blipFill dpi); (0, 0) when unknown.
    /// </summary>
    public static (double W, double H) Size(byte[] data, double dpi = 0)
    {
        var (w, h, dpiX, dpiY) = Read(data);
        if (w <= 0 || h <= 0)
            return default;
        if (dpi > 0)
            (dpiX, dpiY) = (dpi, dpi);
        return (w * 96 / (dpiX > 0 ? dpiX : 96), h * 96 / (dpiY > 0 ? dpiY : 96));
    }

    public static (int Width, int Height, double DpiX, double DpiY) Read(byte[] data)
    {
        if (data.Length > 24 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return Png(data);
        if (data.Length > 4 && data[0] == 0xFF && data[1] == 0xD8)
            return Jpeg(data);
        if (data.Length > 10 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
            return (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6)), BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8)), 96, 96);
        if (data.Length > 54 && data[0] == 0x42 && data[1] == 0x4D)
        {
            var w = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(18));
            var h = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(22)));
            var ppmX = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(38));
            var ppmY = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(42));
            return (w, h, ppmX > 0 ? ppmX * 0.0254 : 96, ppmY > 0 ? ppmY * 0.0254 : 96);
        }
        if (Tiff.IsTiff(data))
            return Tiff.Info(data);
        if (JpegXr.IsJpegXr(data))
            return JpegXr.Info(data);
        return default;
    }

    static (int, int, double, double) Png(byte[] data)
    {
        var w = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16));
        var h = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20));
        double dpiX = 96, dpiY = 96;
        var p = 8;
        while (p + 12 <= data.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(p));
            var type = System.Text.Encoding.ASCII.GetString(data, p + 4, 4);
            if (type == "pHYs" && length >= 9 && data[p + 16] == 1)
            {
                dpiX = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(p + 8)) * 0.0254;
                dpiY = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(p + 12)) * 0.0254;
            }
            if (type is "IDAT" or "IEND")
                break;
            p += 12 + Math.Max(0, length);
        }
        return (w, h, dpiX, dpiY);
    }

    static (int, int, double, double) Jpeg(byte[] data)
    {
        double dpiX = 96, dpiY = 96;
        var p = 2;
        while (p + 9 < data.Length)
        {
            if (data[p] != 0xFF) { p++; continue; }
            var marker = data[p + 1];
            var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p + 2));
            if (marker == 0xE0 && p + 16 < data.Length && data[p + 4] == 'J' && data[p + 5] == 'F')
            {
                var units = data[p + 11];
                var x = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p + 12));
                var y = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p + 14));
                if (units == 1 && x > 0 && y > 0) (dpiX, dpiY) = (x, y);
                else if (units == 2 && x > 0 && y > 0) (dpiX, dpiY) = (x * 2.54, y * 2.54);
            }
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                var h = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p + 5));
                var w = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(p + 7));
                return (w, h, dpiX, dpiY);
            }
            p += 2 + length;
        }
        return default;
    }
}
