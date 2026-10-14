using System.Text;
using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>A level of a numbering definition (w:lvl, catalog: numbering.definitions).</summary>
public sealed class NumberingLevel
{
    public int Start = 1;
    public string Format = "decimal";

    /// <summary>The level text with %1 … %9 for the numbers of the levels; null shows nothing.</summary>
    public string? Text;

    public string Justification = "left";

    /// <summary>What follows the number: tab, space or nothing.</summary>
    public string Suffix = "tab";

    /// <summary>w:lvlRestart: the level (1-based) after which this one starts over, 0 for never, null for any higher level.</summary>
    public int? Restart;

    /// <summary>w:isLgl: all levels' numbers in decimal.</summary>
    public bool Legal;

    public string? ParagraphStyle;
    public XElement? ParagraphProperties, RunProperties;
    public string? PictureBullet;

    public static NumberingLevel Read(XElement lvl)
    {
        var level = new NumberingLevel
        {
            // A level without w:start counts from 0 (measured with docx/local/ns-*: "0.", "1.", "2." in modes 12 to 15,
            // a level below "0.0"; docx/external/libreoffice/extras-ooxmlexport/tdf132754).
            Start = (int)(lvl.Child("start").Number("val") ?? 0),
            Format = lvl.Child("numFmt").Val() ?? "decimal",
            Text = lvl.Child("lvlText").Val(),
            Justification = lvl.Child("lvlJc").Val() ?? "left",
            Suffix = lvl.Child("suff").Val() ?? "tab",
            Restart = lvl.Child("lvlRestart").Number("val") is { } restart ? (int)restart : null,
            Legal = lvl.Child("isLgl").OnOff() == true,
            ParagraphStyle = lvl.Child("pStyle").Val(),
            ParagraphProperties = lvl.Child("pPr"),
            RunProperties = lvl.Child("rPr"),
            PictureBullet = lvl.Child("lvlPicBulletId").Val(),
        };
        // A level text without w:val shows nothing; an empty one too.
        if (lvl.Child("lvlText") is { } text && text.Attr("val") == null)
            level.Text = null;
        // w:numFmt w:val="custom" names its format in w:format (Word 2010): zero-padded numbers as wide as the
        // first one (measured with docx/external/powertools/LIR011-en-US-00001: 3499 → 03499).
        if (level.Format == "custom" && lvl.Child("numFmt").Attr("format") is { } custom)
        {
            var width = custom.TakeWhile(char.IsAsciiDigit).Count();
            level.Format = !custom.StartsWith('0') ? "decimal" : width <= 2 ? "decimalZero" : "decimalZero" + width;
        }
        return level;
    }
}

/// <summary>A paragraph's number: its text, the formatting it is drawn with, what follows it and how it aligns.</summary>
public sealed record ListLabel(string Text, RunFormat Format, string Suffix, string Justification, NumberingLevel Level)
{
    /// <summary>The number of the label's own level in its format, without the text around it (a chapter's number).</summary>
    public string Own { get; init; } = "";

    /// <summary>The abstract numbering definition the label counts in, whatever numbering instance names it.</summary>
    public string? List { get; init; }
}

/// <summary>
/// The numbering part (catalog: numbering.definitions): abstract numbering definitions with their nine levels,
/// numbering instances that refer to them with level and start overrides, and numbering styles that link to them.
/// </summary>
public sealed class NumberingDefinitions
{
    readonly Dictionary<string, XElement> abstracts = new(StringComparer.Ordinal);
    readonly Dictionary<string, XElement> instances = new(StringComparer.Ordinal);
    readonly StyleSheet styles;

    /// <summary>The pictures of picture bullets (w:numPicBullet) by id: a w:pict or w:drawing.</summary>
    readonly Dictionary<string, XElement> pictures = new(StringComparer.Ordinal);

    /// <summary>The numbering part, which the pictures' relationships belong to.</summary>
    public string? Part { get; init; }

    /// <summary>The picture (w:pict or w:drawing) of a picture bullet id, or null.</summary>
    public XElement? PictureBullet(string id) => pictures.GetValueOrDefault(id);

    public NumberingDefinitions(XDocument? document, StyleSheet styles)
    {
        this.styles = styles;
        var root = document?.Root;
        if (root == null)
            return;
        foreach (var bullet in root.Elements(Wml.W + "numPicBullet"))
        {
            if (bullet.Attr("numPicBulletId") is { } id && bullet.Elements().FirstOrDefault() is { } picture)
                pictures.TryAdd(id, picture);
        }
        foreach (var a in root.Elements(Wml.W + "abstractNum"))
        {
            if (a.Attr("abstractNumId") is { } id)
                abstracts.TryAdd(id, a);
        }
        foreach (var n in root.Elements(Wml.W + "num"))
        {
            if (n.Attr("numId") is { } id)
                instances.TryAdd(id, n);
        }
    }

    /// <summary>A numbering instance: the abstract definition it uses (numbering style links followed), its levels and overrides.</summary>
    public sealed class Instance
    {
        public required string Id { get; init; }
        public required string AbstractId { get; init; }
        public required NumberingLevel?[] Levels { get; init; }

        /// <summary>w:lvlOverride/w:startOverride per level.</summary>
        public required int?[] StartOverrides { get; init; }
    }

    readonly Dictionary<string, Instance?> resolved = new(StringComparer.Ordinal);

    /// <summary>The prefix of the numbering ids of LISTNUM's own lists (DefaultList).</summary>
    const string ListNumPrefix = "\u0001LISTNUM:";

    /// <summary>
    /// The numbering id of the list a LISTNUM field of a name counts in that no abstract definition has (NumberDefault,
    /// OutlineDefault, LegalDefault): Word's default list, 1) a) i) (1) (a) (i) 1. a. i., counted per name (measured
    /// with docx/local/lnum-*: OutlineDefault and LegalDefault count in these too; libreoffice/extras-ooxmlexport/
    /// tdf89731: "OutlineDefault \l 3" prints i), ii), iii)).
    /// </summary>
    public static string DefaultList(string name) => ListNumPrefix + name;

    /// <summary>The numbering id of a list LISTNUM names: an instance of the abstract definition of that w:name, or null.</summary>
    public string? Named(string name)
    {
        var abstractId = abstracts.FirstOrDefault(a => string.Equals(a.Value.Child("name").Val(), name, StringComparison.OrdinalIgnoreCase)).Key;
        return abstractId == null ? null : instances.FirstOrDefault(n => n.Value.Child("abstractNumId").Val() == abstractId).Key;
    }

    public Instance? Find(string? numId)
    {
        if (numId == null || numId == "0")
            return null;
        if (resolved.TryGetValue(numId, out var cached))
            return cached;
        if (numId.StartsWith(ListNumPrefix, StringComparison.Ordinal))
        {
            var formats = new[] { ("decimal", "%1)"), ("lowerLetter", "%2)"), ("lowerRoman", "%3)"), ("decimal", "(%4)"), ("lowerLetter", "(%5)"),
                ("lowerRoman", "(%6)"), ("decimal", "%7."), ("lowerLetter", "%8."), ("lowerRoman", "%9.") };
            return resolved[numId] = new Instance
            {
                Id = numId, AbstractId = numId, StartOverrides = new int?[9],
                Levels = formats.Select(f => (NumberingLevel?)new NumberingLevel { Format = f.Item1, Text = f.Item2 }).ToArray(),
            };
        }
        Instance? instance = null;
        if (instances.TryGetValue(numId, out var num) && num.Child("abstractNumId").Val() is { } abstractId
            && Abstract(abstractId, 0) is var (id, element))
        {
            var levels = new NumberingLevel?[9];
            foreach (var lvl in element.Elements(Wml.W + "lvl"))
            {
                if (lvl.Number("ilvl") is { } i and >= 0 and < 9)
                    levels[(int)i] = NumberingLevel.Read(lvl);
            }
            var starts = new int?[9];
            foreach (var o in num.Elements(Wml.W + "lvlOverride"))
            {
                if (o.Number("ilvl") is not { } i || i is < 0 or >= 9)
                    continue;
                if (o.Child("lvl") is { } replacement)
                    levels[(int)i] = NumberingLevel.Read(replacement);
                if (o.Child("startOverride").Number("val") is { } start)
                    starts[(int)i] = (int)start;
            }
            instance = new Instance { Id = numId, AbstractId = id, Levels = levels, StartOverrides = starts };
        }
        return resolved[numId] = instance;
    }

    /// <summary>
    /// The abstract definition with the levels: one with w:numStyleLink takes them from the numbering style's own
    /// numbering instance.
    /// </summary>
    (string Id, XElement Element)? Abstract(string id, int depth)
    {
        if (depth > 8 || !abstracts.TryGetValue(id, out var element))
            return null;
        if (element.Child("numStyleLink").Val() is { } styleId
            && styles.Find(styleId)?.ParagraphProperties.Child("numPr").Child("numId").Val() is { } linkedNum
            && instances.TryGetValue(linkedNum, out var num) && num.Child("abstractNumId").Val() is { } linkedAbstract
            && linkedAbstract != id)
            return Abstract(linkedAbstract, depth + 1);
        return (id, element);
    }
}

/// <summary>
/// List counters of one story (the body, a header, a footnote …; catalog: numbering.counting): numbering instances of
/// the same abstract definition count together; an instance with a start override starts over where it is first
/// used; a level starts over after a higher level (or the one w:lvlRestart names).
/// </summary>
public sealed class ListCounter(NumberingDefinitions definitions)
{
    sealed class Counters
    {
        public readonly int[] Values = new int[9];
        public readonly bool[] Used = new bool[9];
    }

    readonly Dictionary<string, Counters> lists = new(StringComparer.Ordinal);
    readonly HashSet<string> started = new(StringComparer.Ordinal);

    /// <summary>The numbering instance of the story's last numbered paragraph, which a LISTNUM without a name counts in.</summary>
    public string? CurrentList { get; set; }

    /// <summary>The level of the story's last LISTNUM field and the paragraph it stands in.</summary>
    public (int Level, System.Xml.Linq.XElement? Paragraph) ListNum { get; set; }

    /// <summary>
    /// The number of the next paragraph of a numbering instance at a level, with the number of the level alone, or null
    /// where it has none.
    /// </summary>
    /// <param name="language">The language of the paragraph, for numbers in words.</param>
    /// <param name="start">The number to show instead of the next one, from which the level counts on (LISTNUM \s).</param>
    public (string Text, NumberingLevel Level, string Own)? Next(string? numId, int ilvl, string? language = null, int? start = null)
    {
        if (definitions.Find(numId) is not { } instance)
            return null;
        // A level outside the nine a list has takes no number and no level's indents (docx/external/libreoffice/
        // extras-ooxmlexport/tdf61309: ilvl 12 at the margin, no "1.").
        if (ilvl is < 0 or > 8)
            return null;
        if (instance.Levels[ilvl] is not { } level)
            return null;
        if (!lists.TryGetValue(instance.AbstractId, out var counters))
            lists[instance.AbstractId] = counters = new Counters();
        if (started.Add(instance.Id))
        {
            for (var i = 0; i < 9; i++)
            {
                if (instance.StartOverrides[i] is { } overridden)
                {
                    counters.Values[i] = overridden - 1;
                    counters.Used[i] = true;
                }
            }
        }
        if (!counters.Used[ilvl])
            counters.Values[ilvl] = level.Start - 1;
        if (start != null)
            counters.Values[ilvl] = start.Value - 1;
        counters.Values[ilvl]++;
        counters.Used[ilvl] = true;
        for (var d = ilvl + 1; d < 9; d++)
        {
            var restart = instance.Levels[d]?.Restart;
            if (restart == null || restart > 0 && ilvl < restart)
                counters.Used[d] = false;
        }
        var own = level.Format is "none" or "bullet" ? "" : Number(counters.Values[ilvl], level.Legal ? "decimal" : level.Format, language);
        return (Text(instance, counters, ilvl, level, language), level, own);
    }

    static string Text(NumberingDefinitions.Instance instance, Counters counters, int ilvl, NumberingLevel level, string? language)
    {
        if (level.Text == null || level.Format == "none" && !level.Text.Contains('%'))
            return level.Text ?? "";
        if (level.Format == "bullet")
            return level.Text;
        var sb = new StringBuilder();
        var text = level.Text;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '%' && i + 1 < text.Length && text[i + 1] is >= '1' and <= '9')
            {
                var k = text[i + 1] - '1';
                var referenced = instance.Levels[k];
                // A higher level not used yet shows its start and counts from there (measured with
                // docx/local/num-rules: level 3 after level 1 shows 7.1.1., the next level 2 then 7.2.).
                if (!counters.Used[k] && k < ilvl)
                {
                    counters.Values[k] = referenced?.Start ?? 1;
                    counters.Used[k] = true;
                }
                var value = counters.Used[k] ? counters.Values[k] : referenced?.Start ?? 1;
                var format = level.Legal ? "decimal" : referenced?.Format ?? "decimal";
                if (format != "none")
                    sb.Append(Number(value, format == "bullet" ? "decimal" : format, language));
                i++;
                continue;
            }
            sb.Append(text[i]);
        }
        return sb.ToString();
    }

    /// <summary>A list number in a format: letters and roman numbers have none for 0 (measured with docx/local/ns-upperLetter,
    /// ns-lowerRoman: ".", "A.", "B." and ".", "i.", "ii." where the level counts from 0).</summary>
    static string Number(int value, string format, string? language) =>
        value <= 0 && format is "upperLetter" or "lowerLetter" or "upperRoman" or "lowerRoman" ? "" : NumberText.Format(value, format, language);
}
