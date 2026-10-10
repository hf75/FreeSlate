using static FreeSlate.Html.JxrFormat;

namespace FreeSlate.Html;

/// <summary>
/// The sample reconstruction of a JPEG XR image plane (ITU-T T.832 9.9): two levels of the inverse core transform,
/// each followed by the overlap filter, with the geometry of the specification's tile-by-tile pseudocode reduced
/// to regions: hard tiles are filtered one by one, soft tiles as one image.
/// </summary>
static class JxrTransform
{
    static readonly int[] InvPermute = [0, 8, 4, 13, 2, 15, 3, 14, 1, 12, 5, 9, 7, 11, 6, 10];

    public static void Reconstruct(JxrPlane plane, JxrCodestream image, int[][] dclp)
    {
        Span<int> c = stackalloc int[16];
        for (var i = 0; i < plane.Components; i++)
        {
            var samples = plane.Samples[i];
            var stride = plane.SampleWidth[i];
            var subsampled = i > 0 && plane.Format is Yuv420 or Yuv422;
            var mbWidth = subsampled ? 8 : 16;
            var mbHeight = i > 0 && plane.Format == Yuv420 ? 8 : 16;
            var (across, down) = (mbWidth / 4, mbHeight / 4);
            var d = dclp[i];
            for (var mby = 0; mby < image.MbHeight; mby++)
                for (var mbx = 0; mbx < image.MbWidth; mbx++)
                {
                    var at = (mby * image.MbWidth + mbx) * 16;
                    var count = across * down;
                    if (!subsampled)
                    {
                        for (var k = 0; k < 16; k++)
                            c[InvPermute[k]] = d[at + k];
                        Ict4x4(c);
                        c.CopyTo(d.AsSpan(at, 16));
                    }
                    else
                    {
                        if (down == 4)
                            T2pt(ref d[at], ref d[at + 4]);
                        T2x2h(ref d[at], ref d[at + 1], ref d[at + 2], ref d[at + 3], 0);
                        (d[at + 1], d[at + 2]) = (d[at + 2], d[at + 1]);
                        if (down == 4)
                        {
                            T2x2h(ref d[at + 4], ref d[at + 6], ref d[at + 5], ref d[at + 7], 0);
                            (d[at + 5], d[at + 6]) = (d[at + 6], d[at + 5]);
                        }
                    }
                    if (i > 0 && plane.Scaled)
                        for (var k = 0; k < count; k++)
                            d[at + k] *= 2;
                    for (var k = 0; k < count; k++)
                        samples[(mby * mbHeight + k / across * 4) * stride + mbx * mbWidth + k % across * 4] = d[at + k];
                }
            if (image.Overlap == 2)
                foreach (var (x0, x1, y0, y1) in Regions(image, across, down))
                    Overlap(samples, 4, 4 * stride, x0, x1, y0, y1, subsampled);
            var height = plane.SampleHeight[i];
            for (var y = 0; y < height; y += 4)
                for (var x = 0; x < stride; x += 4)
                {
                    var o = y * stride + x;
                    for (var k = 0; k < 16; k++)
                        c[InvPermute[k]] = samples[o + (k >> 2) * stride + (k & 3)];
                    Ict4x4(c);
                    for (var k = 0; k < 16; k++)
                        samples[o + (k >> 2) * stride + (k & 3)] = c[k];
                }
            if (image.Overlap != 0)
                foreach (var (x0, x1, y0, y1) in Regions(image, mbWidth, mbHeight))
                    Overlap(samples, 1, stride, x0, x1, y0, y1, false);
        }
    }

    /// <summary>The areas filtered on their own, in units of a macroblock's width and height.</summary>
    static IEnumerable<(int X0, int X1, int Y0, int Y1)> Regions(JxrCodestream image, int unitWidth, int unitHeight)
    {
        if (!image.HardTiling)
        {
            yield return (0, image.MbWidth * unitWidth, 0, image.MbHeight * unitHeight);
            yield break;
        }
        for (var ty = 0; ty < image.TilesY; ty++)
            for (var tx = 0; tx < image.TilesX; tx++)
                yield return (image.TileLeft[tx] * unitWidth, image.TileLeft[tx + 1] * unitWidth,
                    image.TileTop[ty] * unitHeight, image.TileTop[ty + 1] * unitHeight);
    }

    /// <summary>
    /// The overlap post filter over a region of 4×4 blocks (9.9.3.2, 9.9.6), or of the 2×2 blocks of subsampled
    /// chroma lowpass coefficients (9.9.3.3, 9.9.3.4) whose corners are filtered as residuals of their neighbours.
    /// </summary>
    static void Overlap(int[] s, int xStep, int yStep, int x0, int x1, int y0, int y1, bool small)
    {
        int At(int x, int y) => y * yStep + x * xStep;
        if (small)
        {
            s[At(x0, y0)] -= s[At(x0 + 1, y0)];
            s[At(x1 - 1, y0)] -= s[At(x1 - 2, y0)];
            s[At(x0, y1 - 1)] -= s[At(x0 + 1, y1 - 1)];
            s[At(x1 - 1, y1 - 1)] -= s[At(x1 - 2, y1 - 1)];
            for (var y = y0 + 1; y < y1 - 1; y += 2)
                for (var x = x0 + 1; x < x1 - 1; x += 2)
                    Filter2x2(ref s[At(x, y)], ref s[At(x + 1, y)], ref s[At(x, y + 1)], ref s[At(x + 1, y + 1)]);
            for (var y = y0 + 1; y < y1 - 1; y += 2)
            {
                Filter2(ref s[At(x0, y)], ref s[At(x0, y + 1)]);
                Filter2(ref s[At(x1 - 1, y)], ref s[At(x1 - 1, y + 1)]);
            }
            for (var x = x0 + 1; x < x1 - 1; x += 2)
            {
                Filter2(ref s[At(x, y0)], ref s[At(x + 1, y0)]);
                Filter2(ref s[At(x, y1 - 1)], ref s[At(x + 1, y1 - 1)]);
            }
            s[At(x0, y0)] += s[At(x0 + 1, y0)];
            s[At(x1 - 1, y0)] += s[At(x1 - 2, y0)];
            s[At(x0, y1 - 1)] += s[At(x0 + 1, y1 - 1)];
            s[At(x1 - 1, y1 - 1)] += s[At(x1 - 2, y1 - 1)];
            return;
        }
        Span<int> c = stackalloc int[16];
        for (var y = y0 + 2; y < y1 - 2; y += 4)
            for (var x = x0 + 2; x < x1 - 2; x += 4)
            {
                for (var k = 0; k < 16; k++)
                    c[k] = s[At(x + (k & 3), y + (k >> 2))];
                Filter4x4(c);
                for (var k = 0; k < 16; k++)
                    s[At(x + (k & 3), y + (k >> 2))] = c[k];
            }
        for (var y = y0 + 2; y < y1 - 2; y += 4)
            foreach (var x in (ReadOnlySpan<int>)[x0, x0 + 1, x1 - 2, x1 - 1])
                Filter4(ref s[At(x, y)], ref s[At(x, y + 1)], ref s[At(x, y + 2)], ref s[At(x, y + 3)]);
        for (var x = x0 + 2; x < x1 - 2; x += 4)
            foreach (var y in (ReadOnlySpan<int>)[y0, y0 + 1, y1 - 2, y1 - 1])
                Filter4(ref s[At(x, y)], ref s[At(x + 1, y)], ref s[At(x + 2, y)], ref s[At(x + 3, y)]);
        foreach (var (x, y) in (ReadOnlySpan<(int, int)>)[(x0, y0), (x1 - 2, y0), (x0, y1 - 2), (x1 - 2, y1 - 2)])
            Filter4(ref s[At(x, y)], ref s[At(x + 1, y)], ref s[At(x, y + 1)], ref s[At(x + 1, y + 1)]);
    }

    /// <summary>ICT4x4( ) (9.9.7.1) after InvPermute( ), which the caller folds into gathering the input.</summary>
    static void Ict4x4(Span<int> c)
    {
        T2x2h(ref c[0], ref c[1], ref c[4], ref c[5], 1);
        InvTodd(ref c[2], ref c[3], ref c[6], ref c[7]);
        InvTodd(ref c[8], ref c[12], ref c[9], ref c[13]);
        InvToddOdd(ref c[10], ref c[11], ref c[14], ref c[15]);
        T2x2h(ref c[0], ref c[3], ref c[12], ref c[15], 0);
        T2x2h(ref c[5], ref c[6], ref c[9], ref c[10], 0);
        T2x2h(ref c[1], ref c[2], ref c[13], ref c[14], 0);
        T2x2h(ref c[4], ref c[7], ref c[8], ref c[11], 0);
    }

    static void T2x2h(ref int a, ref int b, ref int c, ref int d, int round)
    {
        a += d;
        b -= c;
        var t1 = (a - b + round) >> 1;
        var t2 = c;
        c = t1 - d;
        d = t1 - t2;
        a -= d;
        b += c;
    }

    static void InvTodd(ref int a, ref int b, ref int c, ref int d)
    {
        b += d;
        a -= c;
        d -= b >> 1;
        c += (a + 1) >> 1;
        a -= (3 * b + 4) >> 3;
        b += (3 * a + 4) >> 3;
        c -= (3 * d + 4) >> 3;
        d += (3 * c + 4) >> 3;
        c -= (b + 1) >> 1;
        d = ((a + 1) >> 1) - d;
        b += c;
        a -= d;
    }

    static void InvToddOdd(ref int a, ref int b, ref int c, ref int d)
    {
        d += a;
        c -= b;
        var t1 = d >> 1;
        var t2 = c >> 1;
        a -= t1;
        b += t2;
        a -= (b * 3 + 3) >> 3;
        b += (a * 3 + 3) >> 2;
        a -= (b * 3 + 4) >> 3;
        b -= t2;
        a += t1;
        c += b;
        d -= a;
        b = -b;
        c = -c;
    }

    static void T2pt(ref int a, ref int b)
    {
        a -= (b + 1) >> 1;
        b += a;
    }

    /// <summary>OverlapPostFilter4x4( ) (9.9.8.1).</summary>
    static void Filter4x4(Span<int> c)
    {
        T2x2h(ref c[0], ref c[3], ref c[12], ref c[15], 0);
        T2x2h(ref c[1], ref c[2], ref c[13], ref c[14], 0);
        T2x2h(ref c[4], ref c[7], ref c[8], ref c[11], 0);
        T2x2h(ref c[5], ref c[6], ref c[9], ref c[10], 0);
        InvRotate(ref c[13], ref c[12]);
        InvRotate(ref c[9], ref c[8]);
        InvRotate(ref c[7], ref c[3]);
        InvRotate(ref c[6], ref c[2]);
        InvToddOddPost(ref c[10], ref c[11], ref c[14], ref c[15]);
        InvScale(ref c[0], ref c[15]);
        InvScale(ref c[1], ref c[14]);
        InvScale(ref c[4], ref c[11]);
        InvScale(ref c[5], ref c[10]);
        T2x2hPost(ref c[0], ref c[3], ref c[12], ref c[15]);
        T2x2hPost(ref c[1], ref c[2], ref c[13], ref c[14]);
        T2x2hPost(ref c[4], ref c[7], ref c[8], ref c[11]);
        T2x2hPost(ref c[5], ref c[6], ref c[9], ref c[10]);
    }

    /// <summary>OverlapPostFilter4( ) (9.9.8.2).</summary>
    static void Filter4(ref int a, ref int b, ref int c, ref int d)
    {
        a += d;
        b += c;
        d -= (a + 1) >> 1;
        c -= (b + 1) >> 1;
        InvScale(ref a, ref d);
        InvScale(ref b, ref c);
        a += (d * 3 + 4) >> 3;
        b += (c * 3 + 4) >> 3;
        d -= a >> 1;
        c -= b >> 1;
        a += d;
        b += c;
        d = -d;
        c = -c;
        InvRotate(ref c, ref d);
        d += (a + 1) >> 1;
        c += (b + 1) >> 1;
        a -= d;
        b -= c;
    }

    /// <summary>OverlapPostFilter2x2( ) (9.9.8.3).</summary>
    static void Filter2x2(ref int a, ref int b, ref int c, ref int d)
    {
        a += d;
        b += c;
        d -= (a + 1) >> 1;
        c -= (b + 1) >> 1;
        b += (a + 2) >> 2;
        a += (b + 1) >> 1;
        a += b >> 5;
        a += b >> 9;
        a += b >> 13;
        b += (a + 2) >> 2;
        d += (a + 1) >> 1;
        c += (b + 1) >> 1;
        a -= d;
        b -= c;
    }

    /// <summary>OverlapPostFilter2( ) (9.9.8.4).</summary>
    static void Filter2(ref int a, ref int b)
    {
        b += (a + 2) >> 2;
        a += (b + 1) >> 1;
        a += b >> 5;
        a += b >> 9;
        a += b >> 13;
        b += (a + 2) >> 2;
    }

    static void InvRotate(ref int a, ref int b)
    {
        a -= (b + 1) >> 1;
        b += (a + 1) >> 1;
    }

    static void InvScale(ref int a, ref int b)
    {
        a += b;
        b = (a >> 1) - b;
        a += (b * 3 + 0) >> 3;
        b += (a * 3 + 0) >> 4;
        b += a >> 7;
        b -= a >> 10;
    }

    static void T2x2hPost(ref int a, ref int b, ref int c, ref int d)
    {
        b -= c;
        a += (d * 3 + 4) >> 3;
        d -= b >> 1;
        c = ((a - b) >> 1) - c;
        (c, d) = (d, c);
        a -= d;
        b += c;
    }

    static void InvToddOddPost(ref int a, ref int b, ref int c, ref int d)
    {
        d += a;
        c -= b;
        var t1 = d >> 1;
        var t2 = c >> 1;
        a -= t1;
        b += t2;
        a -= (b * 3 + 6) >> 3;
        b += (a * 3 + 2) >> 2;
        a -= (b * 3 + 4) >> 3;
        b -= t2;
        a += t1;
        c += b;
        d -= a;
    }
}
