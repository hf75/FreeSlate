using System.Text;

namespace FreeSlate.Docx;

/// <summary>
/// The HTML of a web archive (MHTML, RFC 2557: a MIME message of type message/rfc822, catalog: document.altChunk):
/// its first text/html part, depth first through multipart bodies, decoded from quoted-printable or base64 in the
/// part's charset. The archive's other parts (pictures) are not read.
/// </summary>
static class Mhtml
{
    /// <summary>The archive's HTML, or null where it has none.</summary>
    public static string? Html(byte[] data)
    {
        var (headers, body) = Split(Encoding.Latin1.GetString(data));
        return Part(headers, body);
    }

    static string? Part(Dictionary<string, string> headers, string body)
    {
        var type = headers.GetValueOrDefault("content-type") ?? "text/plain";
        if (type.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            if (Parameter(type, "boundary") is not { Length: > 0 } boundary)
                return null;
            foreach (var part in Parts(body, boundary))
            {
                var (partHeaders, partBody) = Split(part);
                if (Part(partHeaders, partBody) is { } html)
                    return html;
            }
            return null;
        }
        if (!type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            return null;
        var bytes = Decode(body, headers.GetValueOrDefault("content-transfer-encoding"));
        return Text(bytes, Parameter(type, "charset") ?? "utf-8");
    }

    /// <summary>A message or part: its headers (unfolded, names in lower case) and its body.</summary>
    static (Dictionary<string, string> Headers, string Body) Split(string message)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = message.Split('\n');
        var k = 0;
        string? name = null;
        for (; k < lines.Length; k++)
        {
            var line = lines[k].TrimEnd('\r');
            if (line.Length == 0)
            {
                k++;
                break;
            }
            if (line[0] is ' ' or '\t' && name != null)
                headers[name] += " " + line.Trim();
            else if (line.IndexOf(':') is var colon and > 0)
            {
                name = line[..colon].Trim().ToLowerInvariant();
                headers[name] = line[(colon + 1)..].Trim();
            }
        }
        return (headers, string.Join('\n', lines.Skip(k)));
    }

    /// <summary>The parts of a multipart body between its boundary lines.</summary>
    static IEnumerable<string> Parts(string body, string boundary)
    {
        var delimiter = "--" + boundary;
        StringBuilder? part = null;
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith(delimiter, StringComparison.Ordinal))
            {
                if (part != null)
                    yield return part.ToString();
                if (line.AsSpan(delimiter.Length).TrimEnd().SequenceEqual("--"))
                    yield break;
                part = new StringBuilder();
                continue;
            }
            part?.Append(line).Append('\n');
        }
        if (part != null)
            yield return part.ToString();
    }

    /// <summary>A parameter of a header value (boundary="…", charset=…), unquoted.</summary>
    static string? Parameter(string value, string name)
    {
        foreach (var piece in value.Split(';').Skip(1))
        {
            var at = piece.IndexOf('=');
            if (at > 0 && piece[..at].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                return piece[(at + 1)..].Trim().Trim('"');
        }
        return null;
    }

    static byte[] Decode(string body, string? encoding)
    {
        switch (encoding?.Trim().ToLowerInvariant())
        {
            case "base64":
                try
                {
                    return Convert.FromBase64String(new string(body.Where(c => !char.IsWhiteSpace(c)).ToArray()));
                }
                catch (FormatException)
                {
                    return [];
                }
            case "quoted-printable":
            {
                var bytes = new List<byte>(body.Length);
                for (var i = 0; i < body.Length; i++)
                {
                    var c = body[i];
                    if (c == '=' && i + 1 < body.Length && body[i + 1] is '\r' or '\n')
                    {
                        // A soft line break.
                        i += body[i + 1] == '\r' && i + 2 < body.Length && body[i + 2] == '\n' ? 2 : 1;
                        continue;
                    }
                    if (c == '=' && i + 2 < body.Length && Uri.IsHexDigit(body[i + 1]) && Uri.IsHexDigit(body[i + 2]))
                    {
                        bytes.Add((byte)Convert.ToInt32(body.Substring(i + 1, 2), 16));
                        i += 2;
                        continue;
                    }
                    bytes.Add((byte)c);
                }
                return bytes.ToArray();
            }
            default:
                return Encoding.Latin1.GetBytes(body);
        }
    }

    static string Text(byte[] bytes, string charset)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            return Encoding.GetEncoding(charset).GetString(bytes);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
