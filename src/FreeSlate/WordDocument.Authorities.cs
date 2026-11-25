using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FreeSlate;

/// <summary>
/// A citation marked for a table of authorities (Word's References > Mark Citation, a TA field): its short citation, by which later
/// mentions are found; its long citation and its category (1 Cases … 7 Constitutional Provisions, 8 to 16 numbered) where it is the
/// authority's own mark, null for a later mention ("TA \s Brown").
/// </summary>
public sealed record AuthorityMark(string Short, string? Long = null, int? Category = null);

/// <summary>
/// Tables of authorities as Word keeps them (catalog: fields.toa): TA fields mark citations, which show nothing (the model leaves them out,
/// as XE fields, see IndexEntriesOf); a TOA field lists a category's authorities with the pages they are cited on, its result paragraphs of
/// the body. Measured in Word 365 by COM (TablesOfAuthorities.MarkCitation and Add): Word's mark is a complex field without a result,
/// " TA \l "Brown v. Board of Education, 347 U.S. 483 (1954)" \s "Brown" \c 1 " for the first, " TA \s "Brown" " for a later one.
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>The citations a paragraph marks for a table of authorities (TA fields, simple and complex), in order; read from its XML.</summary>
    public IReadOnlyList<AuthorityMark> AuthoritiesOf(int paragraph)
    {
        var marks = new List<AuthorityMark>();
        foreach (var instruction in FieldsBegunIn(paragraph, results: false))
        {
            if (Authority(instruction) is { } mark)
                marks.Add(mark);
        }
        return marks;
    }

    /// <summary>A TA field's citation from its instruction (\l the long citation, \s the short, \c the category); null for another field.</summary>
    public static AuthorityMark? Authority(string instruction)
    {
        var words = FieldWords(instruction);
        if (words.Count == 0 || !words[0].Equals("TA", StringComparison.OrdinalIgnoreCase))
            return null;
        string? longText = null, shortText = null;
        int? category = null;
        for (var k = 1; k < words.Count; k++)
        {
            string? Next() => k + 1 < words.Count ? words[++k] : null;
            switch (words[k].ToLowerInvariant())
            {
                case "\\l":
                    longText = Next();
                    break;
                case "\\s":
                    shortText = Next();
                    break;
                case "\\c":
                    category = int.TryParse(Next(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var c) ? c : null;
                    break;
                case "\\r":
                    Next();
                    break;
            }
        }
        // Word finds later mentions by the short citation; a long one alone is its own short one.
        if ((shortText ?? longText) is not { Length: > 0 } key)
            return null;
        return new AuthorityMark(key, longText, longText != null ? category ?? 1 : null);
    }

    /// <summary>
    /// A field instruction's words as Word reads them: quoted arguments one word without their quotes, a quote within them escaped by a
    /// backslash (\"), as Word's Mark Citation writes one.
    /// </summary>
    public static List<string> FieldWords(string instruction)
    {
        var words = new List<string>();
        var k = 0;
        while (k < instruction.Length)
        {
            while (k < instruction.Length && char.IsWhiteSpace(instruction[k]))
                k++;
            if (k >= instruction.Length)
                break;
            var word = new StringBuilder();
            if (instruction[k] == '"')
            {
                k++;
                while (k < instruction.Length && instruction[k] != '"')
                {
                    if (instruction[k] == '\\' && k + 1 < instruction.Length && instruction[k + 1] is '"' or '\\')
                        k++;
                    word.Append(instruction[k++]);
                }
                k++;
            }
            else
            {
                while (k < instruction.Length && !char.IsWhiteSpace(instruction[k]))
                    word.Append(instruction[k++]);
            }
            words.Add(word.ToString());
        }
        return words;
    }

    /// <summary>
    /// The instructions of the fields that begin in a paragraph, in order, read from its XML: simple fields' and complex fields' (each at its
    /// own depth, up to its separator or end); with <paramref name="results"/> false only those without a result (TA, XE, TC), else all.
    /// </summary>
    public IReadOnlyList<string> FieldsBegunIn(int paragraph, bool results = true)
    {
        var p = Find(paragraph).Paragraph.Element;
        var w = p.Name.Namespace;
        var found = new List<string>();
        // Complex fields: the instruction of each at its own depth, up to its separator or end.
        var open = new Stack<(StringBuilder? Text, int At)>();
        foreach (var e in p.Descendants())
        {
            if (e.Name == w + "fldSimple")
            {
                if (results)
                    found.Add((string?)e.Attribute(w + "instr") ?? "");
            }
            else if (e.Name == w + "fldChar")
            {
                switch ((string?)e.Attribute(w + "fldCharType"))
                {
                    case "begin":
                        open.Push((new StringBuilder(), found.Count));
                        found.Add("");
                        break;
                    case "separate" when open.Count > 0:
                        var (instruction, at) = open.Pop();
                        if (instruction != null)
                            found[at] = (results ? "" : "\0") + instruction;
                        open.Push((null, at));
                        break;
                    case "end" when open.Count > 0:
                        var (ended, place) = open.Pop();
                        if (ended != null)
                            found[place] = ended.ToString();
                        break;
                }
            }
            else if (e.Name == w + "instrText" && open.Count > 0 && open.Peek().Text is { } building)
                building.Append(e.Value);
        }
        // A field still open at the paragraph's end (its result goes on in the next) has its instruction whole all the same.
        foreach (var (text, at) in open)
        {
            if (text != null)
                found[at] = (results ? "" : "\0") + text;
        }
        return [.. found.Where(f => f.Length > 0 && f[0] != '\0')];
    }
}
