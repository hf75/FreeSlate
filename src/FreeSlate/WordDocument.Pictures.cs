using System.Xml.Linq;
using FreeSlate.Docx;

namespace FreeSlate;

/// <summary>
/// Pictures as Word's Picture Format changes them beyond how they show (see DrawingFormatting, catalog: picture.basic): cropped at the
/// scale they have, a floating one's box moving with the sides cropped so that the picture stays where it stands (Word's Crop); another
/// picture in one's frame (Word's Change Picture).
/// </summary>
public sealed partial class WordDocument
{
    const string ImageRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";

    /// <summary>
    /// Where the picture of the drawing at a place of a paragraph's text stands: a picture with its caption's in the first paragraph of its
    /// figure (see AddCaption), others there.
    /// </summary>
    public (int Paragraph, int Offset) PictureAt(int paragraph, int offset) =>
        FigureAt(paragraph, offset) is { } figure ? (figure.Holder.Id, figure.Holder.TextOffset) : (paragraph, offset);

    /// <summary>
    /// Crops the picture at a place of a paragraph's text to a part of it (see PictureCrop), one change, as Word crops one: at the scale it
    /// has, its size the part's; floating, its box moves with the sides cropped at its left and top so that the picture stays where it
    /// stands on the page (where it stands at an offset; aligned, it stays aligned), unless <paramref name="boxStays"/>: then the box
    /// stays where it is and the picture moves in it (a part as large as before, elsewhere). A picture with its caption is cropped so,
    /// the figure as wide as it and as high as it then holds (see AddCaption).
    /// </summary>
    /// <exception cref="NotSupportedException">No picture stands at the place; nothing changes.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The part shows nothing of the picture; nothing changes.</exception>
    public void CropPicture(int paragraph, int offset, PictureCrop crop, bool boxStays = false)
    {
        var figure = FigureAt(paragraph, offset);
        var own = figure?.Picture ?? DrawingAt(Find(paragraph).Paragraph, offset);
        if (own == null || !IsPicture(own))
            throw new NotSupportedException("No picture stands at that place.");
        if (crop.Wide < 0.001 || crop.High < 0.001)
            throw new ArgumentOutOfRangeException(nameof(crop), "The part shows nothing of the picture.");
        var now = Read(own);
        var was = now.Crop ?? PictureCrop.None;
        // The whole picture as large as the part of it is shown.
        var (wide, high) = (now.Width!.Value / Math.Max(0.001, was.Wide), now.Height!.Value / Math.Max(0.001, was.High));
        var (width, height) = (Math.Max(1, wide * crop.Wide), Math.Max(1, high * crop.High));
        var stood = DrawingOf(paragraph, offset)!;
        var start = version;
        try
        {
            if (figure != null)
                Sized(figure, paragraph, offset, width, height, crop);
            else
                Format(paragraph, offset, new DrawingFormatting { Crop = crop, Width = width, Height = height });
            // The box with the sides cropped at its left and top, where it stands at an offset there.
            var (dx, dy) = ((crop.Left - was.Left) * wide, (crop.Top - was.Top) * high);
            var across = !boxStays && stood.Wrap != "inline" && stood.HorizontalAlign == null && stood.HorizontalOffset != null && Math.Abs(dx) > 0.001;
            var down = !boxStays && stood.Wrap != "inline" && stood.VerticalAlign == null && stood.VerticalOffset != null && Math.Abs(dy) > 0.001;
            if (across || down)
            {
                Format(paragraph, offset, new DrawingFormatting
                {
                    HorizontalFrom = across ? stood.HorizontalFrom : null,
                    HorizontalOffset = across ? stood.HorizontalOffset + dx : null,
                    VerticalFrom = down ? stood.VerticalFrom : null,
                    VerticalOffset = down ? stood.VerticalOffset + dy : null,
                });
            }
            Combine(version - start);
        }
        catch
        {
            Back(start);
            throw;
        }
    }

    /// <summary>
    /// The part of a picture of the given proportions (width to height) that fills a frame of a size, at its middle: as much of the
    /// picture as the frame's proportions let it show.
    /// </summary>
    public static PictureCrop Filling(double frameWidth, double frameHeight, double pictureWidth, double pictureHeight)
    {
        if (frameWidth <= 0 || frameHeight <= 0 || pictureWidth <= 0 || pictureHeight <= 0)
            return PictureCrop.None;
        var (frame, picture) = (frameWidth / frameHeight, pictureWidth / pictureHeight);
        if (Math.Abs(frame - picture) < 1e-6)
            return PictureCrop.None;
        if (picture > frame)
        {
            var cut = (1 - frame / picture) / 2;
            return new PictureCrop(cut, 0, cut, 0);
        }
        var off = (1 - picture / frame) / 2;
        return new PictureCrop(0, off, 0, off);
    }

    /// <summary>
    /// Shows another picture in the frame of the picture at a place of a paragraph's text, one change, as Word's Change Picture does: the
    /// frame as it is (its size, its place, how the text goes round it, its caption, its name and description, how it shows), the new
    /// picture filling it (see Filling) by its pixels' proportions. The bytes are a picture of the content type given, kept as they are in a
    /// part of their own (its name with the extension given, ".png" say).
    /// </summary>
    /// <exception cref="NotSupportedException">No picture stands at the place; nothing changes.</exception>
    public void ReplacePicture(int paragraph, int offset, byte[] bytes, string contentType, string extension)
    {
        var (p, top) = Find(paragraph);
        var figure = FigureAt(paragraph, offset);
        if (DrawingAt(p, offset) is not { } own || (figure?.Picture ?? own) is not { } picture || !IsPicture(picture))
            throw new NotSupportedException("No picture stands at that place.");
        var drawn = new XElement(own);
        if (Pic(drawn.Element(A + "graphic")) is not { } pic || pic.Element(pic.Name.Namespace + "blipFill")?.Element(A + "blip") is not { } blip)
            throw new NotSupportedException("No picture stands at that place.");
        var changes = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        var id = Added("{picture}", [new NewPart("picture", "/word/media/image1" + extension, contentType, ImageRelationship, bytes)], changes, PartOf(top));
        // The new picture's part in place of the old one's; a link and the extensions naming the old one's parts (its SVG, its layer before
        // its effects) go with it.
        foreach (var named in blip.Attributes().Where(a => a.Name.LocalName is "embed" or "link").ToList())
            named.Remove();
        blip.SetAttributeValue(Wml.R + "embed", id);
        blip.Element(A + "extLst")?.Elements().Where(e => e.DescendantsAndSelf().Attributes().Any(a => a.Name.LocalName is "embed" or "link")).Remove();
        if (blip.Element(A + "extLst") is { HasElements: false } empty)
            empty.Remove();
        var (width, height) = Drawings.Extent(picture);
        var (pixelsWide, pixelsHigh) = Html.ImageInfo.Size(bytes);
        Show(pic, new DrawingFormatting { Crop = Filling(width, height, pixelsWide, pixelsHigh) }, cropping: true);
        Change(top, top, (stretch, _) => TextEdits.Redraw(stretch, p, offset, drawn), changes: changes);
    }
}
