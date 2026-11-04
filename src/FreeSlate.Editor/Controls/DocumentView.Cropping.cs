using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using FreeSlate.Editor.Writing;

namespace FreeSlate.Editor.Controls;

/// <summary>
/// The picture selected cropped by hand, as Word's Crop has it (see BeginCrop): the whole picture shows faint round the part it shows,
/// over the page; marks at the corners and the middles of the sides of its frame crop it there (a corner two sides at once, with Shift in
/// the frame's proportions), a press on the picture moves it under its frame. Letting go crops it so, a step of its own (see
/// EditSession.CropPicture: a floating picture stays where it is, its frame going with the sides cropped). Enter, Escape or a press
/// elsewhere ends it, as another selection does; Escape while a mark is dragged takes the drag back. A picture turned or mirrored is
/// cropped as it shows, its marks turned with it; one tilted in space shows flat while it is cropped, as Office shows it, and is tilted
/// again after, the crops and that one step.
/// </summary>
public sealed partial class DocumentView
{
    /// <summary>How long a crop mark's arms are at most, and how thick they are, in pixels.</summary>
    const double MarkLength = 12, MarkWeight = 3;

    /// <summary>How much of the picture shows where it is cut away.</summary>
    const double Faint = 0.38;

    /// <summary>Whether the picture selected is being cropped by hand.</summary>
    bool cropping;

    /// <summary>
    /// The picture being cropped as it shows whole (its bytes and its look, which it is made again for where they change), for the part
    /// cut away; stale after an edit, to be checked against the picture then.
    /// </summary>
    (byte[] Bytes, string? Look, Bitmap? Whole)? cropWhole;
    bool cropStale;

    /// <summary>
    /// A press while cropping: where, the mark pressed (none: the picture moves under its frame), the frame and the whole picture then in the
    /// view's coordinates (unturned, see CropTurn) and the way back from the view to them; once the pointer has moved, where they go.
    /// </summary>
    sealed class CropDrag(Point start, IPointer pointer, (int Across, int Down)? mark, Rect frame, Rect whole, Matrix back)
    {
        public Point Start { get; } = start;
        public IPointer Pointer { get; } = pointer;
        public (int Across, int Down)? Mark { get; } = mark;
        public Rect StartFrame { get; } = frame;
        public Rect StartWhole { get; } = whole;
        public Matrix Back { get; } = back;
        public Rect Frame { get; set; } = frame;
        public Rect Whole { get; set; } = whole;
        public bool Moving { get; set; }
    }

    CropDrag? cropDrag;

    /// <summary>
    /// A tilted picture being cropped: its tilt, which it shows without while it is (a step of its own), how many steps there were before
    /// that one, and the document's changes after it (see Retilt).
    /// </summary>
    (PictureTilt Tilt, int Steps, int Changes)? cropTilt;

    /// <summary>Whether the picture selected is being cropped by hand (see BeginCrop).</summary>
    public bool Cropping => cropping && CropFrames() != null;

    /// <summary>The picture selected is cropped by hand from here (see the class); false where the selection holds no picture.</summary>
    public bool BeginCrop()
    {
        if (CropFrames() == null)
            return false;
        EndTilt();
        if (session is { } editing && editing.PictureShown?.Tilt is { IsFlat: false } tilt)
        {
            var steps = editing.Steps;
            Edit(() => editing.TiltPicture(PictureTilt.Flat));
            cropTilt = editing.Steps > steps ? (tilt, steps, editing.Document.Changes) : null;
        }
        (cropping, cropStale) = (true, true);
        Cursor = Grab;
        InvalidateVisual();
        return true;
    }

    /// <summary>Cropping by hand ends; a crop being dragged goes back.</summary>
    public void EndCrop()
    {
        if (!cropping && cropDrag == null)
            return;
        var drag = cropDrag;
        (cropping, cropDrag, cropWhole) = (false, null, null);
        drag?.Pointer.Capture(null);
        Retilt();
        Cursor = Text;
        InvalidateVisual();
    }

    /// <summary>
    /// The tilted picture cropped tilted again (see cropTilt): shown flat and nothing more, as though that never was; cropped, the
    /// flattening, the crops and the tilt one step.
    /// </summary>
    void Retilt()
    {
        if (cropTilt is not var (tilt, steps, changes) || session is not { } editing)
            return;
        cropTilt = null;
        if (editing.Document.Changes == changes)
            Edit(() => editing.TakeBack());
        else
        {
            Edit(() =>
            {
                editing.TiltPicture(tilt);
                editing.JoinSince(steps);
            });
        }
    }

    /// <summary>
    /// The frame of the picture selected (its box, its own size about its middle where a turn swaps the box in the line; a picture with its
    /// caption, its picture's own) and the whole picture round it as it would show uncropped, in the view's coordinates before the picture
    /// is turned and mirrored (see CropTurn), and the part it shows; null where the selection holds no picture.
    /// </summary>
    (Rect Frame, Rect Whole, PictureCrop Crop)? CropFrames()
    {
        if (session is not { } editing || editing.PictureShown is not { Crop: { Wide: > 0, High: > 0 } crop } shown || SelectedDrawing() is not var (page, at)
            || PictureBox(page, at) is not { Width: > 0, Height: > 0 } box)
            return null;
        var pt = factor * PxPerPt;
        var frame = shown is { Rotation: not (null or 0), Width: double w, Height: double h } ? FrameAbout(box.Center, new Size(w * pt, h * pt)) : box;
        var (wide, high) = (frame.Width / crop.Wide, frame.Height / crop.High);
        return (frame, new Rect(frame.X - crop.Left * wide, frame.Y - crop.Top * high, wide, high), crop);
    }

    /// <summary>The picture selected mirrored and turned as it shows, about the middle of its frame: from its frame (see CropFrames) to the view.</summary>
    Matrix CropTurn(Rect frame)
    {
        var shown = session?.PictureShown;
        var middle = frame.Center;
        return Matrix.CreateTranslation(-middle.X, -middle.Y) * Matrix.CreateScale(shown?.FlipHorizontal == true ? -1 : 1, shown?.FlipVertical == true ? -1 : 1)
            * Matrix.CreateRotation((shown?.Rotation ?? 0) * Math.PI / 180) * Matrix.CreateTranslation(middle.X, middle.Y);
    }

    /// <summary>The box of the picture of the drawing among a page's characters (a picture with its caption, its picture's own) in the view's coordinates.</summary>
    Rect? PictureBox(int page, int at)
    {
        if (session?.Document is not WordDocument word || page >= rows.Count || pictures.Spots(Page(page)) is not { } spots || at >= spots.Count)
            return null;
        var places = pictures.Places(Page(page));
        var (block, offset) = places.PlaceAt(spots[at], after: false);
        var own = word.PictureAt(block, offset);
        if (own == (block, offset))
            return BoxOf(page, at);
        return places.Exact(own.Paragraph, own.Offset) is { } spot ? BoxOf(page, pictures.Index(Page(page), spot)) : null;
    }

    /// <summary>The crop mark of a frame a point of the view is on, or null.</summary>
    static (int Across, int Down)? CropMarkAt(Point point, Rect frame)
    {
        foreach (var mark in Handles)
        {
            if (((Vector)(point - HandlePoint(frame, mark))).Length <= HandleReach + 2)
                return mark;
        }
        return null;
    }

    /// <summary>A press of the main button while cropping: on a mark it crops there, on the picture it moves it; false elsewhere.</summary>
    bool CropPress(Point point, IPointer pointer)
    {
        if (CropFrames() is not var (frame, whole, _))
            return false;
        var back = CropTurn(frame).Invert();
        var local = point.Transform(back);
        var mark = CropMarkAt(local, frame);
        if (mark == null && !whole.Contains(local))
            return false;
        cropDrag = new CropDrag(point, pointer, mark, frame, whole, back);
        pointer.Capture(this);
        return true;
    }

    /// <summary>
    /// The pointer moved while cropping: a mark takes its sides where it goes, within the picture and no smaller than Least (a corner with
    /// Shift keeps the frame's proportions); the picture moves under its frame as far as it covers it.
    /// </summary>
    void CropTo(Point point, KeyModifiers modifiers)
    {
        var d = cropDrag!;
        if (!d.Moving && ((Vector)(point - d.Start)).Length < DragDistance)
            return;
        d.Moving = true;
        // Along the picture's own sides, as it is turned and mirrored.
        var by = point.Transform(d.Back) - d.Start.Transform(d.Back);
        var (f, w) = (d.StartFrame, d.StartWhole);
        if (d.Mark is not { } mark)
        {
            var dx = Math.Clamp(by.X, f.Right - w.Right, f.Left - w.Left);
            var dy = Math.Clamp(by.Y, f.Bottom - w.Bottom, f.Top - w.Top);
            d.Whole = w.Translate(new Vector(dx, dy));
            InvalidateVisual();
            return;
        }
        var (left, top, right, bottom) = (f.Left, f.Top, f.Right, f.Bottom);
        if (mark.Across < 0)
            left = Math.Clamp(f.Left + by.X, w.Left, right - Least);
        else if (mark.Across > 0)
            right = Math.Clamp(f.Right + by.X, left + Least, w.Right);
        if (mark.Down < 0)
            top = Math.Clamp(f.Top + by.Y, w.Top, bottom - Least);
        else if (mark.Down > 0)
            bottom = Math.Clamp(f.Bottom + by.Y, top + Least, w.Bottom);
        if (mark.Across != 0 && mark.Down != 0 && (modifiers & KeyModifiers.Shift) != 0)
        {
            // From the opposite corner, the larger change counting, as far as the picture reaches each way.
            var most = Math.Min(mark.Across < 0 ? (f.Right - w.Left) / f.Width : (w.Right - f.Left) / f.Width,
                mark.Down < 0 ? (f.Bottom - w.Top) / f.Height : (w.Bottom - f.Top) / f.Height);
            var scale = Math.Min(most, Math.Max((right - left) / f.Width, (bottom - top) / f.Height));
            var (width, height) = (f.Width * scale, f.Height * scale);
            (left, right) = mark.Across < 0 ? (f.Right - width, f.Right) : (f.Left, f.Left + width);
            (top, bottom) = mark.Down < 0 ? (f.Bottom - height, f.Bottom) : (f.Top, f.Top + height);
        }
        d.Frame = new Rect(left, top, right - left, bottom - top);
        InvalidateVisual();
    }

    /// <summary>The press while cropping ends: let go (<paramref name="keep"/>) after moving, the picture is cropped as it shows (a step); else as it was.</summary>
    void EndCropDrag(bool keep)
    {
        if (cropDrag is not { } d)
            return;
        cropDrag = null;
        if (keep && d.Moving && session is { } editing)
        {
            var (f, w) = (d.Frame, d.Whole);
            var crop = new PictureCrop((f.Left - w.Left) / w.Width, (f.Top - w.Top) / w.Height, (w.Right - f.Right) / w.Width, (w.Bottom - f.Bottom) / w.Height);
            Edit(() => editing.CropPicture(crop, boxStays: d.Mark == null));
        }
        InvalidateVisual();
    }

    /// <summary>The cursor while cropping, no button held: a mark's way as it shows, the hand on the picture, the text's elsewhere.</summary>
    Cursor CropCursorAt(Point point)
    {
        if (CropFrames() is not var (frame, whole, _))
            return Text;
        var turn = CropTurn(frame);
        var local = point.Transform(turn.Invert());
        return CropMarkAt(local, frame) is { } mark ? CursorOf(mark, turn) : whole.Contains(local) ? Grab : Text;
    }

    /// <summary>
    /// The picture being cropped: the whole picture faint over the sheet where it is cut away, the part it would show while a mark or the
    /// picture is dragged (the page shows the part it shows until then), the edge of the whole, the frame and its marks; turned and
    /// mirrored as the picture is.
    /// </summary>
    void DrawCrop(DrawingContext context)
    {
        if (!cropping)
            return;
        if (CropFrames() is not var (startFrame, startWhole, _))
        {
            (cropping, cropTilt) = (false, null);
            return;
        }
        using var turned = context.PushTransform(CropTurn(startFrame));
        var (frame, whole) = cropDrag is { Moving: true } d ? (d.Frame, d.Whole) : (startFrame, startWhole);
        var picture = WholePicture(whole);
        using (context.PushGeometryClip(new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(whole), new RectangleGeometry(frame))))
        {
            context.FillRectangle(Sheet ?? Brushes.White, whole);
            if (picture != null)
            {
                using (context.PushOpacity(Faint))
                    context.DrawImage(picture, whole);
            }
        }
        if (cropDrag is { Moving: true } && picture != null)
        {
            using (context.PushClip(frame))
                context.DrawImage(picture, whole);
        }
        var accent = DrawingBrush ?? Brushes.Orange;
        context.DrawRectangle(null, new Pen(accent, 1, new DashStyle([3, 3], 0)), whole);
        context.DrawRectangle(null, new Pen(accent, 1), frame);
        DrawCropMarks(context, frame, accent);
    }

    /// <summary>The marks at a frame's corners (each an L round it) and at the middles of its sides (a bar along it), outside it.</summary>
    void DrawCropMarks(DrawingContext context, Rect frame, IBrush fill)
    {
        var (w, arm) = (MarkWeight, Math.Min(MarkLength, Math.Min(frame.Width, frame.Height) / 3));
        var edge = new Pen(Sheet ?? Brushes.White, 1);
        foreach (var (across, down) in Handles)
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                if (across != 0 && down != 0)
                {
                    // From the corner, the arms going in along the sides, the mark outside them.
                    var (x, y) = (across < 0 ? frame.Left : frame.Right, down < 0 ? frame.Top : frame.Bottom);
                    var (ix, iy) = (-across, -down);
                    Point At(double u, double v) => new(x + u * ix, y + v * iy);
                    g.BeginFigure(At(-w, -w), true);
                    foreach (var p in new[] { At(arm, -w), At(arm, 0), At(0, 0), At(0, arm), At(-w, arm) })
                        g.LineTo(p);
                }
                else
                {
                    var bar = across == 0
                        ? new Rect(frame.Center.X - arm, down < 0 ? frame.Top - w : frame.Bottom, 2 * arm, w)
                        : new Rect(across < 0 ? frame.Left - w : frame.Right, frame.Center.Y - arm, w, 2 * arm);
                    g.BeginFigure(bar.TopLeft, true);
                    g.LineTo(bar.TopRight);
                    g.LineTo(bar.BottomRight);
                    g.LineTo(bar.BottomLeft);
                }
                g.EndFigure(true);
            }
            context.DrawGeometry(fill, edge, geometry);
        }
    }

    /// <summary>
    /// The picture being cropped whole, in its look, at about the size it shows at on the screen (made again where its bytes or its look
    /// changed); null where it cannot be read.
    /// </summary>
    Bitmap? WholePicture(Rect whole)
    {
        if (!cropStale && cropWhole is var (_, _, kept))
            return kept;
        cropStale = false;
        if (session is not { } editing || editing.PictureBytes is not { } bytes)
            return null;
        var look = editing.PictureShown?.Recolor;
        if (cropWhole is var (had, hadLook, bitmap) && hadLook == look && had.AsSpan().SequenceEqual(bytes))
            return bitmap;
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var made = Looks.Shown(bytes, PictureCrop.None, look, (int)Math.Min(2400, Math.Ceiling(whole.Width * scaling)), (int)Math.Min(2400, Math.Ceiling(whole.Height * scaling)));
        cropWhole = (bytes, look, made);
        return made;
    }
}
