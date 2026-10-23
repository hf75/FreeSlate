using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FreeSlate.Opc;

/// <summary>
/// A part's XML as the bytes it was written in (catalog: package.opc), for writing changed elements back into it and
/// leaving everything else as it was, byte for byte: where an element read with line info (LoadOptions.SetLineInfo)
/// stands in its text, where that element ends, and an element written as XML with the prefixes of its scope.
/// </summary>
public sealed class PartText
{
    readonly byte[] bytes;
    readonly Encoding encoding;
    readonly int preamble;
    readonly List<int> lines = [0];

    public PartText(byte[] bytes)
    {
        this.bytes = bytes;
        (encoding, preamble) = Detect(bytes);
        Text = encoding.GetString(bytes, preamble, bytes.Length - preamble);
        // Lines as XML readers count them: a CR LF is one break, a CR or an LF alone another.
        for (var i = 0; i < Text.Length; i++)
        {
            if (Text[i] == '\r' && i + 1 < Text.Length && Text[i + 1] == '\n')
                i++;
            if (Text[i] is '\r' or '\n')
                lines.Add(i + 1);
        }
    }

    /// <summary>XML as text of its own (a stretch of a part's, see WordDocument), its bytes in UTF-8.</summary>
    public PartText(string text) : this(new UTF8Encoding(false).GetBytes(text))
    {
    }

    /// <summary>The part's text, without its byte order mark.</summary>
    public string Text { get; }

    /// <summary>
    /// Where an element read with line info starts in the text (its "&lt;"), or -1 for one without: made or copied after
    /// reading. The line position is its name's first character.
    /// </summary>
    public int Start(XElement element) =>
        element is IXmlLineInfo info && info.HasLineInfo() && info.LineNumber <= lines.Count ? lines[info.LineNumber - 1] + info.LinePosition - 2 : -1;

    /// <summary>Where the element that starts at <paramref name="start"/> ends: after the "&gt;" of its end tag, or of its own tag when it is empty.</summary>
    public int End(int start)
    {
        var text = Text;
        var depth = 0;
        var i = start;
        while (i >= 0 && i < text.Length)
        {
            if (text[i] != '<')
            {
                i = text.IndexOf('<', i);
                continue;
            }
            if (string.CompareOrdinal(text, i, "<!--", 0, 4) == 0)
                i = After(text.IndexOf("-->", i + 4, StringComparison.Ordinal), 3);
            else if (string.CompareOrdinal(text, i, "<![CDATA[", 0, 9) == 0)
                i = After(text.IndexOf("]]>", i + 9, StringComparison.Ordinal), 3);
            else if (i + 1 < text.Length && text[i + 1] == '?')
                i = After(text.IndexOf("?>", i + 2, StringComparison.Ordinal), 2);
            else if (i + 1 < text.Length && text[i + 1] == '/')
            {
                i = After(text.IndexOf('>', i), 1);
                if (--depth == 0)
                    return i;
            }
            else
            {
                var j = TagClose(text, i);
                if (j >= text.Length)
                    break;
                var empty = text[j - 1] == '/';
                i = j + 1;
                if (!empty)
                    depth++;
                else if (depth == 0)
                    return i;
            }
        }
        throw new InvalidDataException("The part's XML ends within an element.");

        static int After(int found, int length) => found < 0 ? -1 : found + length;
    }

    /// <summary>Where the start tag that begins at <paramref name="start"/> ends: after its "&gt;".</summary>
    public int TagEnd(int start) =>
        TagClose(Text, start) is var close && close < Text.Length ? close + 1 : throw new InvalidDataException("The part's XML ends within a tag.");

    /// <summary>The "&gt;" a start tag runs to, outside quoted values (the text's length where there is none).</summary>
    static int TagClose(string text, int start)
    {
        var quote = '\0';
        var j = start + 1;
        for (; j < text.Length; j++)
        {
            var c = text[j];
            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';
            }
            else if (c is '"' or '\'')
                quote = c;
            else if (c == '>')
                break;
        }
        return j;
    }

    /// <summary>The text with the given stretches of it (from Start to End, not overlapping) replaced by new text.</summary>
    public string Replaced(IEnumerable<(int Start, int End, string Text)> changes)
    {
        var result = new StringBuilder(Text.Length + 1024);
        var at = 0;
        foreach (var (start, end, text) in changes.OrderBy(c => c.Start))
        {
            if (start < at || end < start)
                throw new ArgumentException("The changes overlap.", nameof(changes));
            result.Append(Text, at, start - at).Append(text);
            at = end;
        }
        return result.Append(Text, at, Text.Length - at).ToString();
    }

    /// <summary>
    /// The part's bytes with the given stretches of its text (from Start to End, not overlapping) replaced by new text:
    /// the bytes around them as they were, the new text in the part's encoding.
    /// </summary>
    public byte[] Spliced(IEnumerable<(int Start, int End, string Text)> changes)
    {
        var result = new MemoryStream(bytes.Length + 4096);
        var (at, atByte) = (0, preamble);
        result.Write(bytes, 0, preamble);
        foreach (var (start, end, text) in changes.OrderBy(c => c.Start))
        {
            if (start < at || end < start)
                throw new ArgumentException("The changes overlap.", nameof(changes));
            var startByte = atByte + encoding.GetByteCount(Text.AsSpan(at, start - at));
            result.Write(bytes, atByte, startByte - atByte);
            result.Write(encoding.GetBytes(text));
            (at, atByte) = (end, startByte + encoding.GetByteCount(Text.AsSpan(start, end - start)));
        }
        result.Write(bytes, atByte, bytes.Length - atByte);
        return result.ToArray();
    }

    /// <summary>
    /// An element as XML in its document's scope: its names with the prefixes its ancestors declare (no declarations are
    /// added), an element read as empty written empty, text escaped so that it reads back the same (carriage returns
    /// too).
    /// </summary>
    public static string Xml(XElement element)
    {
        var s = new StringBuilder();
        Write(s, element);
        return s.ToString();
    }

    static void Write(StringBuilder s, XElement element)
    {
        var name = Qualified(element, element.Name, attribute: false);
        s.Append('<').Append(name);
        foreach (var attribute in element.Attributes())
        {
            s.Append(' ');
            if (attribute.IsNamespaceDeclaration)
                s.Append(attribute.Name.Namespace == XNamespace.None ? "xmlns" : "xmlns:" + attribute.Name.LocalName);
            else
                s.Append(Qualified(element, attribute.Name, attribute: true));
            s.Append("=\"");
            Escape(s, attribute.Value, attribute: true);
            s.Append('"');
        }
        if (element.IsEmpty)
        {
            s.Append("/>");
            return;
        }
        s.Append('>');
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XElement child:
                    Write(s, child);
                    break;
                case XCData data:
                    s.Append("<![CDATA[").Append(data.Value).Append("]]>");
                    break;
                case XText text:
                    Escape(s, text.Value, attribute: false);
                    break;
                default:
                    s.Append(node.ToString(SaveOptions.DisableFormatting));
                    break;
            }
        }
        s.Append("</").Append(name).Append('>');
    }

    static string Qualified(XElement scope, XName name, bool attribute)
    {
        if (name.Namespace == XNamespace.None)
            return name.LocalName;
        if (name.Namespace == XNamespace.Xml)
            return "xml:" + name.LocalName;
        if (!attribute && scope.GetDefaultNamespace() == name.Namespace)
            return name.LocalName;
        return (scope.GetPrefixOfNamespace(name.Namespace) ?? throw new InvalidOperationException($"No prefix in scope for {name.Namespace}.")) + ":" + name.LocalName;
    }

    static void Escape(StringBuilder s, string value, bool attribute)
    {
        foreach (var c in value)
        {
            s.Append(c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '\r' => "&#xD;",
                '"' when attribute => "&quot;",
                '\t' when attribute => "&#x9;",
                '\n' when attribute => "&#xA;",
                _ => null,
            } ?? c.ToString());
        }
    }

    /// <summary>
    /// The encoding of a part's bytes: by its byte order mark, else by its XML declaration (UTF-8 where it names none or
    /// one .NET does not know), and the length of the mark.
    /// </summary>
    static (Encoding Encoding, int Preamble) Detect(byte[] bytes)
    {
        if (bytes is [0xEF, 0xBB, 0xBF, ..])
            return (new UTF8Encoding(false, true), 3);
        if (bytes is [0xFF, 0xFE, ..])
            return (new UnicodeEncoding(false, false, true), 2);
        if (bytes is [0xFE, 0xFF, ..])
            return (new UnicodeEncoding(true, false, true), 2);
        var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 200));
        if (head.StartsWith("<?xml", StringComparison.Ordinal)
            && System.Text.RegularExpressions.Regex.Match(head[..Math.Max(head.IndexOf("?>", StringComparison.Ordinal), 0)], "encoding\\s*=\\s*[\"']([^\"']+)") is { Success: true } named
            && !named.Groups[1].Value.StartsWith("utf-8", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return (Encoding.GetEncoding(named.Groups[1].Value), 0);
            }
            catch (ArgumentException)
            {
            }
        }
        return (new UTF8Encoding(false, true), 0);
    }
}
