using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace FreeSlate.Pptx.Charts;

/// <summary>
/// The geography a region map keeps with it (cx:geography/cx:geoCache): base64 of raw deflated XML (cx:clear) with
/// every region's name, bounds and polygons. A polygon's rings (pcaRings: the count of outer rings, then the rings)
/// are point-compressed like the Bing Maps point compression algorithm, in the base64url alphabet with "_-" last:
/// each point one number of 5-bit groups (the last below 32) pairing the zigzagged latitude and longitude steps in
/// 1e-5 degrees diagonally.
/// </summary>
sealed class GeoCache
{
    const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-";

    /// <summary>A region: its name and polygons, each an outer ring followed by its holes (latitude, longitude).</summary>
    public sealed record Region(string Name, string Id, List<List<(double Lat, double Lon)>> Rings);

    public List<Region> Regions { get; } = [];

    /// <summary>The sources the cached geography credits (cx:copyright), in alphabetical order.</summary>
    public List<string> Copyrights { get; } = [];

    public static GeoCache? Read(XElement? geography)
    {
        var binary = geography?.Element(ChartExModel.Cx + "geoCache")?.Element(ChartExModel.Cx + "binary")?.Value;
        if (string.IsNullOrWhiteSpace(binary))
            return null;
        byte[] packed;
        try
        {
            packed = Convert.FromBase64String(binary.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
        // Office ends the stream without a final block: whatever inflates is kept.
        var inflated = new MemoryStream();
        try
        {
            using var deflate = new DeflateStream(new MemoryStream(packed), CompressionMode.Decompress);
            deflate.CopyTo(inflated);
        }
        catch (InvalidDataException)
        {
        }
        XElement root;
        try
        {
            root = XElement.Parse(Encoding.UTF8.GetString(inflated.ToArray()));
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
        var cache = new GeoCache();
        var copyrights = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var data in root.Descendants(ChartExModel.Cx + "geoData"))
        {
            var rings = new List<List<(double, double)>>();
            foreach (var polygon in data.Descendants(ChartExModel.Cx + "geoPolygon"))
            {
                var parts = ((string?)polygon.Attribute("pcaRings") ?? "").Split(',');
                for (var i = 1; i < parts.Length; i++)
                {
                    if (Decode(parts[i]) is { Count: > 2 } ring)
                        rings.Add(ring);
                }
            }
            foreach (var copyright in data.Descendants(ChartExModel.Cx + "copyright"))
            {
                if (copyright.Value.Trim() is { Length: > 0 } source)
                    copyrights.Add(source);
            }
            cache.Regions.Add(new Region((string?)data.Attribute("entityName") ?? "", (string?)data.Attribute("entityId") ?? "", rings));
        }
        cache.Copyrights.AddRange(copyrights);
        return cache;
    }

    /// <summary>One point-compressed ring; null where it does not decode.</summary>
    static List<(double Lat, double Lon)>? Decode(string value)
    {
        var points = new List<(double, double)>();
        long latitude = 0, longitude = 0;
        var index = 0;
        while (index < value.Length)
        {
            long n = 0;
            var shift = 0;
            while (true)
            {
                if (index >= value.Length || shift > 60)
                    return null;
                var b = Alphabet.IndexOf(value[index++]);
                if (b < 0)
                    return null;
                n |= (long)(b & 31) << shift;
                shift += 5;
                if (b < 32)
                    break;
            }
            var diagonal = (long)((Math.Sqrt(8.0 * n + 5) - 1) / 2);
            while (diagonal > 0 && diagonal * (diagonal + 1) / 2 > n)
                diagonal--;
            while ((diagonal + 1) * (diagonal + 2) / 2 <= n)
                diagonal++;
            n -= diagonal * (diagonal + 1) / 2;
            var dy = n;
            var dx = diagonal - dy;
            dx = (dx >> 1) ^ -(dx & 1);
            dy = (dy >> 1) ^ -(dy & 1);
            longitude += dx;
            latitude += dy;
            points.Add((latitude * 1e-5, longitude * 1e-5));
        }
        return points;
    }
}
