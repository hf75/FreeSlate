using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using FreeSlate.Editing;
using FreeSlate.Editor.Writing;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// Direct formatting as Claude reads it (see RunLook, ParagraphLook): a stretch of text whose runs have a look of their own in the marks
/// that say it (==marked==, ~~struck~~, ^superscript^, ~subscript~ where the look is only theirs, else "{font=Georgia size=14|text}"), a
/// stretch whose runs hold what the notation does not say as the runs' WordprocessingML; a paragraph's own formatting as its settings
/// before its text ("{align=right after=12pt} ").
/// </summary>
static partial class Reading
{
    /// <summary>
    /// The stretches of a paragraph's text whose runs have a look of their own (see WordDocument.RunProperties): its look where the notation
    /// says it, and whether they hold what it does not say. A language the paragraph is in anyway goes without saying.
    /// </summary>
    static List<(int Offset, int Length, RunLook? Look, bool Other)> Looks(WordDocument word, int paragraph)
    {
        var looks = new List<(int Offset, int Length, RunLook? Look, bool Other)>();
        int? language = null;
        foreach (var (offset, length, properties) in word.RunProperties(paragraph))
        {
            if (properties.Length == 0)
                continue;
            var (look, other) = RunLook.Of(properties);
            if (look.Language is { } tag)
            {
                language ??= word.LanguageOf(paragraph);
                if (Lcid(tag) == language)
                    look = look with { Language = null };
            }
            if (!other && look.Empty)
                continue;
            // Runs that read alike next to each other (two runs of one look written apart) as one stretch.
            if (looks.Count > 0 && looks[^1] is var (before, beforeLength, beforeLook, beforeOther) && before + beforeLength == offset && !other && !beforeOther
                && Equals(beforeLook, look))
                looks[^1] = (before, beforeLength + length, beforeLook, false);
            else
                looks.Add((offset, length, look.Empty ? null : look, other));
        }
        return looks;
    }

    /// <summary>A language tag's Windows language id; null where it names none.</summary>
    static int? Lcid(string tag)
    {
        try
        {
            return CultureInfo.GetCultureInfo(tag).LCID;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// A stretch of text as the notation writes it with its meaning (a character style, see Line) and its look: **…**, *…*, {Style|…} as
    /// before; a look of the compact marks' alone within them, where the text takes them (beginning and ending with no space, a script's
    /// with none); else its words before it, "{color=#C00000|…}" within a meaning's stars, "{Term color=#C00000|…}" for another style.
    /// </summary>
    static string Wrapped(string? style, RunLook? look, string shown)
    {
        // Spaces and tabs mean nothing, but a look shows on them (a form's underlined tab to write on).
        if (shown.Trim().Length == 0)
            return look == null || shown.Length == 0 ? shown : "{" + look.Words() + "|" + shown + "}";
        var meaning = style is { Length: > 0 } and not "Hyperlink" ? style : null;
        string Meant(string inner) => meaning switch
        {
            null => inner,
            "Important" => "**" + inner + "**",
            "Emphasized" => "*" + inner + "*",
            var other => "{" + other + "|" + inner + "}",
        };
        if (look == null)
            return Meant(shown);
        if (Compact(look, shown) is { } compact)
            return Meant(compact);
        return meaning is null or "Important" or "Emphasized"
            ? Meant("{" + look.Words() + "|" + shown + "}")
            : "{" + meaning + " " + look.Words() + "|" + shown + "}";
    }

    /// <summary>A stretch of text in the compact marks of its look, where its look is theirs alone and its text takes them; else null.</summary>
    static string? Compact(RunLook look, string shown)
    {
        var (open, close) = ("", "");
        var rest = look;
        void Mark(string mark)
        {
            open += mark;
            close = mark + close;
        }
        if (rest.Highlight == "yellow")
        {
            Mark("==");
            rest = rest with { Highlight = null };
        }
        if (rest.Strike == "single")
        {
            Mark("~~");
            rest = rest with { Strike = null };
        }
        var script = rest.Script is "superscript" or "subscript";
        if (script)
        {
            Mark(rest.Script == "superscript" ? "^" : "~");
            rest = rest with { Script = null };
        }
        if (!rest.Empty || open.Length == 0 || char.IsWhiteSpace(shown[0]) || char.IsWhiteSpace(shown[^1]) || shown[^1] == '\\'
            || script && shown.Any(char.IsWhiteSpace))
            return null;
        return open + shown + close;
    }

    /// <summary>
    /// A stretch of a paragraph's text as the runs it is in, as copying it keeps them (a look the notation does not say, see Line); null
    /// where it keeps none.
    /// </summary>
    static string? Raw(WordDocument word, int paragraph, int from, int to)
    {
        if (word.Copy(paragraph, from, paragraph, to) is not [var copied])
            return null;
        try
        {
            var p = Markup.Parsed(copied).Elements().FirstOrDefault();
            var runs = p == null ? "" : string.Concat(p.Elements().Where(e => e.Name.LocalName != "pPr").Select(e => e.ToString(SaveOptions.DisableFormatting)));
            return runs.Length > 0 ? Declared().Replace(runs, "") : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>The declarations of the prefixes markup given as it is may use without them (see Markup.Parsed).</summary>
    [GeneratedRegex(@" xmlns:(?:w|r|m|wp|a|pic|c|wps|wpg|wpc|wp14|w14|w15|a14|mc|v|o)=""[^""]*""")]
    private static partial Regex Declared();

    /// <summary>The paragraph properties said otherwise (its style, its mark's look, its section) or going without saying (widow control, hyphenation, line numbers, spacing between paragraphs alike).</summary>
    static readonly HashSet<string> Said = ["pStyle", "rPr", "sectPr", "widowControl", "suppressAutoHyphens", "suppressLineNumbers", "contextualSpacing"];

    /// <summary>
    /// A paragraph's own properties the notation does not say otherwise (see Said): those but a list the notation says by its number (a
    /// numbered point's own list, of the tone's at its depth); null where it cannot be copied.
    /// </summary>
    static List<XElement>? Own(WordDocument word, int paragraph)
    {
        if (Copied(word, paragraph) is not var (_, properties))
            return null;
        if (properties == null)
            return [];
        var level = Levels.Of(word.ParagraphStyle(paragraph));
        var listed = level.Numbered && word.ListDefinition(paragraph) == Lists.Definition && word.OwnList(paragraph).Level is var depth
            && (depth == null || depth == level.Depth - 1);
        return [.. properties.Elements().Where(e => !Said.Contains(e.Name.LocalName) && !(listed && e.Name.LocalName == "numPr"))];
    }

    /// <summary>
    /// A paragraph's own formatting as its settings before its text ("{align=right after=12pt} "), empty where it has none the notation says;
    /// an alignment as <paramref name="align"/> left out (a shape's text, centred where it is written without one).
    /// </summary>
    static string Settings(WordDocument word, int paragraph, string? align = null)
    {
        if (Own(word, paragraph) is not { Count: > 0 } own)
            return "";
        var look = ParagraphLook.Of(own, word.Text(paragraph) == "￼").Look;
        if (align != null && look.Align == align)
            look = look with { Align = null };
        return look.Empty ? "" : "{" + look.Words() + "} ";
    }

    /// <summary>The paragraph copied last (see Copied), with the document's version then: a paragraph read twice in a row is copied once.</summary>
    static (WordDocument Word, int Paragraph, int Changes, XElement Copy, XElement? Properties)? copiedLast;
}
