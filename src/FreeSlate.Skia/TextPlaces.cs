using FreeSlate.Rendering;
using SkiaSharp;

namespace FreeSlate.Skia;

/// <summary>
/// The places of a page's text where its scene draws them (see PlacesItem; SkiaScene.Draw gathers them): which place a
/// point hits, where the cursor of a place stands, and what a selection between two places covers, in the units the page
/// was drawn in (CSS pixels from its top left). A place is a paragraph's id (Block.Id; a slide paragraph's in its
/// PresentationDocument) and an offset in its text (Docx.ParagraphText, Pptx.SlideText).
/// </summary>
public sealed class TextPlaces(IReadOnlyList<(SKMatrix ToPage, PlacesItem Line)> lines)
{
    const float PxPerPt = 96f / 72;

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

    /// <summary>The character a point of the page is over and whether the point hits after it (see Hit); null on a page without places.</summary>
    public (Spot Spot, bool After)? HitPlace(SKPoint point) => HitSpot(point) is { } spot ? (spot, After(point, spot)) : null;

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

    /// <summary>The character a point of the page is over (see Hit), and whether it hits after it.</summary>
    public Spot? HitSpot(SKPoint point)
    {
        Spot? best = null;
        var (bestY, bestX) = (double.MaxValue, double.MaxValue);
        for (var k = 0; k < lines.Count; k++)
        {
            var (toPage, line) = lines[k];
            if (line.Offsets.Length == 0)
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
