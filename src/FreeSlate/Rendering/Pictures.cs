using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace FreeSlate.Rendering;

/// <summary>
/// Pictures by a short token of their own (fsimg:n) in place of a data URI in each string that names them (in .NET a
/// data URI takes 2.7 times its picture's bytes): their media type and their bytes, kept once, as long as what they were
/// kept for is (see Keep: the package they come from) or what took them over (see Taken: scenes that outlive it). HTML
/// gets the data URI a token stands for (see Resolved), drawing gets the bytes (see Get).
/// </summary>
public static class Pictures
{
    /// <summary>What a token begins with.</summary>
    public const string Prefix = "fsimg:";

    sealed record Picture(string Type, byte[] Bytes);

    /// <summary>The pictures by their numbers, as long as they are kept.</summary>
    static readonly Dictionary<long, WeakReference<Picture>> pictures = [];

    /// <summary>The pictures each owner keeps.</summary>
    static readonly ConditionalWeakTable<object, List<Picture>> owned = [];

    static long made;

    /// <summary>A token for a picture of a media type with its bytes, kept as long as <paramref name="owner"/> is.</summary>
    public static string Keep(object owner, string type, byte[] bytes)
    {
        var picture = new Picture(type, bytes);
        lock (pictures)
        {
            var number = ++made;
            pictures[number] = new WeakReference<Picture>(picture);
            owned.GetOrCreateValue(owner).Add(picture);
            // The numbers of pictures no longer kept go now and then.
            if (number % 1024 == 0)
            {
                foreach (var gone in pictures.Where(p => !p.Value.TryGetTarget(out _)).Select(p => p.Key).ToList())
                    pictures.Remove(gone);
            }
            return Prefix + number.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>What keeps an owner's pictures (a package about to be closed), for what is to outlive it, or null.</summary>
    public static object? Taken(object owner)
    {
        lock (pictures)
            return owned.TryGetValue(owner, out var list) ? list : null;
    }

    /// <summary>The picture a token stands for, or null (another URI, a picture no longer kept).</summary>
    static Picture? Of(string uri)
    {
        if (!uri.StartsWith(Prefix, StringComparison.Ordinal)
            || !long.TryParse(uri.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            return null;
        lock (pictures)
            return pictures.TryGetValue(number, out var reference) && reference.TryGetTarget(out var picture) ? picture : null;
    }

    /// <summary>
    /// The media type and bytes of a token's picture or of a data URI's (base64); null for other URIs and for a token
    /// whose picture is no longer kept.
    /// </summary>
    public static (string Type, byte[] Bytes)? Get(string? uri)
    {
        if (uri == null)
            return null;
        if (Of(uri) is { } picture)
            return (picture.Type, picture.Bytes);
        if (!uri.StartsWith("data:", StringComparison.Ordinal) || uri.IndexOf(',') is not (> 0 and var comma)
            || !uri.AsSpan(0, comma).EndsWith(";base64", StringComparison.Ordinal))
            return null;
        try
        {
            return (uri[5..(comma - 7)], Convert.FromBase64String(uri[(comma + 1)..]));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>The media type of a picture's URI (a token's or a base64 data URI's), or null.</summary>
    public static string? Type(string? uri) =>
        uri == null ? null : Of(uri) is { } picture ? picture.Type
            : uri.StartsWith("data:", StringComparison.Ordinal) && uri.IndexOf(";base64,", StringComparison.Ordinal) is var at and > 5 ? uri[5..at] : null;

    /// <summary>The data URI a token stands for; any other URI as it is.</summary>
    public static string DataUri(string uri) =>
        Of(uri) is { } picture ? $"data:{picture.Type};base64,{Convert.ToBase64String(picture.Bytes)}" : uri;

    /// <summary>HTML or SVG with each href="fsimg:n" written as the data URI it stands for (see DataUri).</summary>
    public static string Resolved(string html)
    {
        const string marker = "href=\"" + Prefix;
        var at = html.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
            return html;
        var resolved = new StringBuilder(html.Length);
        var from = 0;
        while (at >= 0)
        {
            var start = at + 6;
            var end = html.IndexOf('"', start);
            if (end < 0)
                break;
            resolved.Append(html, from, start - from).Append(DataUri(html[start..end]));
            from = end;
            at = html.IndexOf(marker, end, StringComparison.Ordinal);
        }
        return resolved.Append(html, from, html.Length - from).ToString();
    }
}
