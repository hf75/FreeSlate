using System.Runtime.CompilerServices;
using SkiaSharp;

namespace FreeSlate.Skia;

/// <summary>
/// Raster pictures as a screen shows them (see SkiaScene.Draw's pictureScale): a picture of a data URI decoded once at
/// about the size it is drawn at, in eighths of its own size (a JPEG's decoder scales by eighths itself, other formats
/// are decoded whole and scaled down), oriented as its EXIF data says (as Skia's own decoding does), and kept for the
/// pages drawn again (after an edit, or scrolled back to). A picture is known by its URI, the string itself: a document
/// gives each picture's URI once and its pages share it. The pictures drawn last stay while they take no more than
/// <see cref="Budget"/> bytes together; a recording that draws one holds it on after it left (Skia counts references),
/// so a viewer keeps the pictures of the pages it keeps recorded, at the size they are shown at instead of their own.
/// </summary>
static class ShownPictures
{
    /// <summary>The bytes the pictures kept take at most together (their pixels, four bytes each).</summary>
    const long Budget = 64L << 20;

    /// <summary>A picture: its own size (oriented) and the sizes decoded, by eighths of it, with their places among those drawn last.</summary>
    sealed class Picture(int width, int height)
    {
        public readonly int Width = width, Height = height;
        public readonly SKImage?[] Sizes = new SKImage?[9];
        public readonly LinkedListNode<(Picture Picture, int Eighths)>?[] Uses = new LinkedListNode<(Picture, int)>?[9];
    }

    /// <summary>The pictures by their URIs, as long as those live.</summary>
    static readonly ConditionalWeakTable<string, Picture> pictures = [];

    /// <summary>The sizes decoded, the one drawn last first, and the bytes they take.</summary>
    static readonly LinkedList<(Picture Picture, int Eighths)> recent = new();
    static long bytes;

    /// <summary>
    /// The raster picture of a data URI decoded for drawing it <paramref name="width"/> by <paramref name="height"/>
    /// pixels: at the eighths of its own size that come next above that, at most its own; null for SVG pictures, other
    /// URIs and pictures Skia does not decode (drawn as before).
    /// </summary>
    public static SKImage? Image(string uri, double width, double height)
    {
        if (!uri.StartsWith("data:image/", StringComparison.Ordinal) || uri.StartsWith("data:image/svg+xml", StringComparison.Ordinal)
            || !(width > 0 && height > 0) || uri.IndexOf(',') is not (> 0 and var comma))
            return null;
        lock (recent)
        {
            if (pictures.TryGetValue(uri, out var picture) && Eighths(picture, width, height) is var wanted && picture.Sizes[wanted] is { } kept)
            {
                Use(picture, wanted);
                return kept;
            }
            byte[] data;
            try
            {
                data = Convert.FromBase64String(uri[(comma + 1)..]);
            }
            catch (FormatException)
            {
                return null;
            }
            using var encoded = SKData.CreateCopy(data);
            using var codec = SKCodec.Create(encoded);
            if (codec == null)
                return null;
            if (picture == null)
            {
                var swapped = codec.EncodedOrigin >= SKEncodedOrigin.LeftTop;
                picture = swapped ? new Picture(codec.Info.Height, codec.Info.Width) : new Picture(codec.Info.Width, codec.Info.Height);
                pictures.AddOrUpdate(uri, picture);
            }
            var eighths = Eighths(picture, width, height);
            if (Decode(codec, eighths) is not { } image)
                return null;
            picture.Sizes[eighths] = image;
            bytes += image.Info.BytesSize64;
            Use(picture, eighths);
            // The sizes drawn longest ago go while all take more than the budget (not the one just decoded).
            while (bytes > Budget && recent.Last is { } last && last != picture.Uses[eighths])
                Drop(last.Value.Picture, last.Value.Eighths);
            return image;
        }
    }

    /// <summary>The eighths of a picture's own size that drawing it at a size takes: the next above along either axis, 1 to 8.</summary>
    static int Eighths(Picture picture, double width, double height) =>
        Math.Clamp((int)Math.Ceiling(8 * Math.Max(width / picture.Width, height / picture.Height) - 1e-6), 1, 8);

    static void Use(Picture picture, int eighths)
    {
        if (picture.Uses[eighths] is { } node)
            recent.Remove(node);
        picture.Uses[eighths] = recent.AddFirst((picture, eighths));
    }

    /// <summary>
    /// Lets go of a size of a picture, not disposed: the recordings that draw it keep it, and a drawing on another thread
    /// may be about to draw it; the garbage collector frees it with the last of them.
    /// </summary>
    static void Drop(Picture picture, int eighths)
    {
        if (picture.Uses[eighths] is { } node)
            recent.Remove(node);
        picture.Uses[eighths] = null;
        if (picture.Sizes[eighths] is { } image)
            bytes -= image.Info.BytesSize64;
        picture.Sizes[eighths] = null;
    }

    /// <summary>
    /// A picture decoded at so many eighths of its own size: by the decoder's own scaling where it has that (JPEG, WebP),
    /// else whole and then scaled down (mipmapped, as a picture drawn smaller is); oriented; in the platform's color type,
    /// opaque where the picture is.
    /// </summary>
    static SKImage? Decode(SKCodec codec, int eighths)
    {
        var info = codec.Info;
        var (width, height) = ((int)Math.Ceiling(info.Width * eighths / 8.0), (int)Math.Ceiling(info.Height * eighths / 8.0));
        var size = eighths < 8 ? codec.GetScaledDimensions(eighths / 8f) : info.Size;
        var alpha = info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul;
        var decoded = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, alpha, info.ColorSpace);
        var bitmap = new SKBitmap();
        if (!bitmap.TryAllocPixels(decoded) || codec.GetPixels(decoded, bitmap.GetPixels()) is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            return null;
        }
        if (size.Width > width * 1.25 && size.Height > height * 1.25)
        {
            var smaller = new SKBitmap();
            if (smaller.TryAllocPixels(decoded.WithSize(width, height)) && bitmap.ScalePixels(smaller, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)))
                (bitmap, smaller) = (smaller, bitmap);
            smaller.Dispose();
        }
        if (codec.EncodedOrigin > SKEncodedOrigin.TopLeft)
        {
            var oriented = Oriented(bitmap, codec.EncodedOrigin);
            bitmap.Dispose();
            bitmap = oriented;
        }
        bitmap.SetImmutable();
        var image = SKImage.FromBitmap(bitmap);
        bitmap.Dispose();
        return image;
    }

    /// <summary>A bitmap turned and mirrored from how its file stores it to how it is shown (EXIF orientation, as SkEncodedOriginToMatrix).</summary>
    static SKBitmap Oriented(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        var (w, h) = ((float)bitmap.Width, (float)bitmap.Height);
        var swapped = origin >= SKEncodedOrigin.LeftTop;
        var result = new SKBitmap(bitmap.Info.WithSize(swapped ? bitmap.Height : bitmap.Width, swapped ? bitmap.Width : bitmap.Height));
        using var canvas = new SKCanvas(result);
        canvas.SetMatrix(origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity,
        });
        canvas.DrawBitmap(bitmap, 0, 0);
        return result;
    }
}
