using FreeSlate.Rendering;
using SkiaSharp;

namespace FreeSlate.Skia;

/// <summary>
/// The places of a page's text where its scene draws them (see PlacesItem; SkiaScene.Draw gathers them): which place a
/// point hits, where the cursor of a place stands, and what a selection between two places covers, in the units the page
/// was drawn in (CSS pixels from its top left). A place is a paragraph's id (Block.Id; a slide paragraph's in its
/// PresentationDocument) and an offset in its text (Docx.ParagraphText, Pptx.SlideText). The places of floating drawings
/// (PlacesItem.Floating) are not lines: they only tell which drawing a point is on and where its box is (see FloatingAt, Box).
/// </summary>
public sealed class TextPlaces
{
    const float PxPerPt = 96f / 72;

    readonly IReadOnlyList<(SKMatrix ToPage, PlacesItem Line)> lines, floating;

    public TextPlaces(IReadOnlyList<(SKMatrix ToPage, PlacesItem Line)> places)
    {
        lines = [.. places.Where(p => !p.Line.Floating)];
        floating = [.. places.Where(p => p.Line.Floating)];
    }

    /// <summary>Where a place stands among the page's places: its line (in drawing order) and its index there.</summary>
    public readonly record struct Spot(int Line, int Index);

    public IReadOnlyList<(SKMatrix ToPage, PlacesItem Line)> Lines => lines;

    /// <summary>A point on the page in a line's own units (points).</summary>
    static SKPoint Local(SKMatrix toPage, SKPoint point)
    {
        var p = toPage.TryInvert(out var inverse) ? inverse.MapPoint(point) : point;
        return new SKPoint(p.X / PxPerPt, p.Y / PxPerPt);
    }

    /// <summary>A point of a line (in points) on the page.</summary>
    static SKPoint OnPage(SKMatrix toPage, double x, double y) => toPage.MapPoint((float)(x * PxPerPt), (float)(y * PxPerPt));

    /// <summary>
    /// The place a point of the page hits: in the line it stands in or the nearest one (above or below it, then left or
    /// right of it; of lines over one another the one drawn last, on top), before the character it is over where it
    /// stands in its first half (in reading order), after it in its second; null on a page without places.
    /// </summary>
    public (int Block, int Offset)? Hit(SKPoint point) => HitSpot(point) is { } spot ? PlaceAt(spot, After(point, spot)) : null;

    /// <summary>
    /// The character a point of the page is over and whether the point hits after it (see Hit), of the lines <paramref name="among"/> takes
    /// where it is given; null on a page without places.
    /// </summary>
    public (Spot Spot, bool After)? HitPlace(SKPoint point, Func<PlacesItem, bool>? among = null) =>
        HitSpot(point, among) is { } spot ? (spot, After(point, spot)) : null;

    /// <summary>The page's characters in order, line after line (each a spot).</summary>
    public List<Spot> Spots()
    {
        var spots = new List<Spot>();
        for (var k = 0; k < lines.Count; k++)
        {
            for (var i = 0; i < lines[k].Line.Offsets.Length; i++)
                spots.Add(new Spot(k, i));
        }
        return spots;
    }

    /// <summary>The cursor before a character, or after it: from its line's top to its bottom, on the page.</summary>
    public (SKPoint Top, SKPoint Bottom) CaretAt(Spot spot, bool after)
    {
        var (toPage, line) = lines[spot.Line];
        var x = after ? line.Ends[spot.Index] : line.Starts[spot.Index];
        return (OnPage(toPage, x, line.Top), OnPage(toPage, x, line.Bottom));
    }

    /// <summary>
    /// The box of the drawing a spot's character is (see PlacesItem.Boxes), as its four corners on the page from its top left
    /// clockwise; null for other characters.
    /// </summary>
    public SKPoint[]? Box(Spot spot)
    {
        if (InlineBox(spot) is { } inline)
            return inline;
        var line = lines[spot.Line].Line;
        var (block, offset) = (line.Block, line.Offsets[spot.Index]);
        return floating.FirstOrDefault(f => f.Line.Block == block && f.Line.Offsets[0] == offset) is var (toPage, drawing) && drawing != null ? Corners(toPage, drawing.Boxes[0]) : null;
    }

    /// <summary>The box of the drawing in the line a spot's character is, as Box gives it; null for other characters, a floating drawing's anchor too.</summary>
    public SKPoint[]? InlineBox(Spot spot)
    {
        var (toPage, line) = lines[spot.Line];
        return line.Boxes.TryGetValue(spot.Index, out var box) ? Corners(toPage, box) : null;
    }

    static SKPoint[] Corners(SKMatrix toPage, (double Left, double Top, double Right, double Bottom) box) =>
        [OnPage(toPage, box.Left, box.Top), OnPage(toPage, box.Right, box.Top), OnPage(toPage, box.Right, box.Bottom), OnPage(toPage, box.Left, box.Bottom)];

    /// <summary>
    /// The floating drawing a point of the page is on, the topmost of those behind the text or of the others (<paramref name="behind"/>):
    /// its anchor's place; null where it is on none.
    /// </summary>
    public (int Block, int Offset)? FloatingAt(SKPoint point, bool behind)
    {
        for (var k = floating.Count - 1; k >= 0; k--)
        {
            var (toPage, drawing) = floating[k];
            if (drawing.Behind != behind)
                continue;
            var corners = Corners(toPage, drawing.Boxes[0]);
            if (point.X >= corners.Min(c => c.X) && point.X <= corners.Max(c => c.X) && point.Y >= corners.Min(c => c.Y) && point.Y <= corners.Max(c => c.Y))
                return (drawing.Block, drawing.Offsets[0]);
        }
        return null;
    }

    /// <summary>Whether a point of the page is over a character of a line (within its width and its line's height).</summary>
    public bool Over(SKPoint point)
    {
        if (HitSpot(point) is not { } spot)
            return false;
        var (toPage, line) = lines[spot.Line];
        var (left, right) = (Math.Min(line.Starts[spot.Index], line.Ends[spot.Index]), Math.Max(line.Starts[spot.Index], line.Ends[spot.Index]));
        var (a, b) = (OnPage(toPage, left, line.Top), OnPage(toPage, right, line.Bottom));
        return point.X >= Math.Min(a.X, b.X) && point.X <= Math.Max(a.X, b.X) && point.Y >= Math.Min(a.Y, b.Y) && point.Y <= Math.Max(a.Y, b.Y);
    }

    /// <summary>The character a point of the page is over (see Hit), of the lines <paramref name="among"/> takes where it is given.</summary>
    public Spot? HitSpot(SKPoint point, Func<PlacesItem, bool>? among = null)
    {
        Spot? best = null;
        var (bestY, bestX) = (double.MaxValue, double.MaxValue);
        for (var k = 0; k < lines.Count; k++)
        {
            var (toPage, line) = lines[k];
            if (line.Offsets.Length == 0 || among != null && !among(line))
                continue;
            var local = Local(toPage, point);
            var dy = local.Y < line.Top ? line.Top - local.Y : local.Y > line.Bottom ? local.Y - line.Bottom : 0;
            for (var i = 0; i < line.Offsets.Length; i++)
            {
                var (left, right) = (Math.Min(line.Starts[i], line.Ends[i]), Math.Max(line.Starts[i], line.Ends[i]));
                var dx = local.X < left ? left - local.X : local.X > right ? local.X - right : 0;
                if (dy < bestY - 1e-6 || Math.Abs(dy - bestY) <= 1e-6 && (dx < bestX || dx == 0 && bestX == 0 && best?.Line != k))
                    (best, bestY, bestX) = (new Spot(k, i), dy, dx);
            }
        }
        return best;
    }

    bool After(SKPoint point, Spot spot)
    {
        var (toPage, line) = lines[spot.Line];
        var local = Local(toPage, point);
        var (start, end) = (line.Starts[spot.Index], line.Ends[spot.Index]);
        return start <= end ? local.X > (start + end) / 2 : local.X < (start + end) / 2;
    }

    /// <summary>The place before a character, or after it: the next offset (a field's result or a note's mark after all of it).</summary>
    public (int Block, int Offset) PlaceAt(Spot spot, bool after)
    {
        var line = lines[spot.Line].Line;
        var offset = line.Offsets[spot.Index];
        return (line.Block, after && line.Starts[spot.Index] != line.Ends[spot.Index] ? offset + 1 : offset);
    }

    /// <summary>
    /// Where a place stands: the character at its offset (the first that shows it), else the last character before it
    /// on the line that goes on after it, as a spot and whether the place is after that character; null where the page does
    /// not show the place.
    /// </summary>
    public (Spot Spot, bool After)? Find(int block, int offset)
    {
        (Spot, bool)? before = null;
        for (var k = 0; k < lines.Count; k++)
        {
            var line = lines[k].Line;
            if (line.Block != block)
                continue;
            for (var i = 0; i < line.Offsets.Length; i++)
            {
                if (line.Offsets[i] == offset)
                    return (new Spot(k, i), false);
                if (line.Offsets[i] < offset && (before == null || line.Offsets[i] >= Offset(before.Value.Item1)))
                    before = (new Spot(k, i), true);
            }
        }
        return before;
    }

    int Offset(Spot spot) => lines[spot.Line].Line.Offsets[spot.Index];

    /// <summary>The formatting a spot's character shows (PlacesItem.Formats: bold, italic, underlined); 0 where its line gives none.</summary>
    public byte FormatAt(Spot spot) => lines[spot.Line].Line.Formats is { } formats && spot.Index < formats.Length ? formats[spot.Index] : (byte)0;

    /// <summary>Whether a page shows a place: a character at its offset.</summary>
    public Spot? Exact(int block, int offset)
    {
        for (var k = 0; k < lines.Count; k++)
        {
            var line = lines[k].Line;
            if (line.Block != block)
                continue;
            var i = Array.IndexOf(line.Offsets, offset);
            if (i >= 0)
                return new Spot(k, i);
        }
        return null;
    }

    /// <summary>The cursor of a place: from its line's top to its bottom at the place's x, on the page; null where the page does not show it.</summary>
    public (SKPoint Top, SKPoint Bottom)? Caret(int block, int offset) => Find(block, offset) is var (spot, after) ? CaretAt(spot, after) : null;

    /// <summary>
    /// What a selection from one character to another covers (both included, in the order the page holds its places):
    /// for each line the stretch of its characters from its top to its bottom, as four corners on the page.
    /// </summary>
    public List<SKPoint[]> Selection(Spot from, Spot to)
    {
        if ((to.Line, to.Index).CompareTo((from.Line, from.Index)) < 0)
            (from, to) = (to, from);
        var areas = new List<SKPoint[]>();
        for (var k = from.Line; k <= to.Line && k < lines.Count; k++)
        {
            var (toPage, line) = lines[k];
            var (first, last) = (k == from.Line ? from.Index : 0, k == to.Line ? to.Index : line.Offsets.Length - 1);
            if (first > last)
                continue;
            var (left, right) = (double.MaxValue, double.MinValue);
            for (var i = first; i <= last; i++)
            {
                left = Math.Min(left, Math.Min(line.Starts[i], line.Ends[i]));
                right = Math.Max(right, Math.Max(line.Starts[i], line.Ends[i]));
            }
            if (right <= left)
                continue;
            areas.Add([OnPage(toPage, left, line.Top), OnPage(toPage, right, line.Top), OnPage(toPage, right, line.Bottom), OnPage(toPage, left, line.Bottom)]);
        }
        return areas;
    }
}
