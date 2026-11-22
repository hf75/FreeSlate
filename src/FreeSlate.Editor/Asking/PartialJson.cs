using System.Globalization;
using System.Text;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// The members of a flat JSON object as far as it is written (a tool's input streaming in): each string, number or literal read so far,
/// with whether it is whole; a string cut off gives what it holds up to there (an escape cut in half left out).
/// </summary>
static class PartialJson
{
    public static Dictionary<string, (string Value, bool Whole)> Members(string json)
    {
        var members = new Dictionary<string, (string, bool)>(StringComparer.Ordinal);
        var k = json.IndexOf('{');
        if (k < 0)
            return members;
        k++;
        while (k < json.Length)
        {
            k = Skip(json, k);
            if (k >= json.Length || json[k] == '}')
                break;
            if (json[k] == ',')
            {
                k++;
                continue;
            }
            if (json[k] != '"')
                break;
            var (name, nameWhole, afterName) = String(json, k);
            if (!nameWhole)
                break;
            k = Skip(json, afterName);
            if (k >= json.Length || json[k] != ':')
                break;
            k = Skip(json, k + 1);
            if (k >= json.Length)
                break;
            if (json[k] == '"')
            {
                var (value, whole, after) = String(json, k);
                members[name] = (value, whole);
                if (!whole)
                    break;
                k = after;
            }
            else if (json[k] is '{' or '[')
            {
                // Nested values are not read here; on past them, as far as they close.
                var depth = 0;
                var start = k;
                var inString = false;
                for (; k < json.Length; k++)
                {
                    var c = json[k];
                    if (inString)
                    {
                        if (c == '\\')
                            k++;
                        else if (c == '"')
                            inString = false;
                    }
                    else if (c == '"')
                        inString = true;
                    else if (c is '{' or '[')
                        depth++;
                    else if (c is '}' or ']' && --depth == 0)
                    {
                        k++;
                        break;
                    }
                }
                members[name] = (json[start..Math.Min(k, json.Length)], depth == 0);
            }
            else
            {
                var start = k;
                while (k < json.Length && json[k] is not (',' or '}' or ' ' or '\n' or '\r' or '\t'))
                    k++;
                members[name] = (json[start..k], k < json.Length);
            }
        }
        return members;
    }

    static int Skip(string json, int k)
    {
        while (k < json.Length && char.IsWhiteSpace(json[k]))
            k++;
        return k;
    }

    /// <summary>A JSON string from its opening quote: its text, whether it closed, and the place after it.</summary>
    static (string Text, bool Whole, int After) String(string json, int k)
    {
        var text = new StringBuilder();
        for (var c = k + 1; c < json.Length; c++)
        {
            var ch = json[c];
            if (ch == '"')
                return (text.ToString(), true, c + 1);
            if (ch != '\\')
            {
                text.Append(ch);
                continue;
            }
            if (c + 1 >= json.Length)
                break;
            var escaped = json[++c];
            switch (escaped)
            {
                case 'n': text.Append('\n'); break;
                case 't': text.Append('\t'); break;
                case 'r': text.Append('\r'); break;
                case 'b': text.Append('\b'); break;
                case 'f': text.Append('\f'); break;
                case 'u':
                    if (c + 4 >= json.Length)
                        return (text.ToString(), false, json.Length);
                    if (int.TryParse(json.AsSpan(c + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                        text.Append((char)code);
                    c += 4;
                    break;
                default: text.Append(escaped); break;
            }
        }
        return (text.ToString(), false, json.Length);
    }
}
