using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// A paragraph's own formatting as the notation says it, "{align=right after=12pt}" before its text (see Notation, Prompt): what Word's
/// Paragraph dialog, its Borders and Shading and its Tabs give a paragraph besides its style: its alignment, its indents (left, right, the
/// first line's or a hanging one), the space before and after it, its line spacing (a multiple of single, exactly or at least so high),
/// keeping it with the next and its lines together, a page break before it, its outline level, lines round it and a shading behind it, its
/// tab stops. Written as Word writes it (measured in Word 365 by COM, each given a paragraph by the Paragraph object's properties, its
/// WordprocessingML read, scratchpad gaps/C/charprobe.ps1: w:jc both for justified, w:ind in twips with w:hanging, w:spacing w:line 360
/// auto for 1.5 lines, 280 exact for exactly 14 pt, a box of single lines half a point wide one point from the text above and below and four
/// beside it, a shading clear on a fill, w:outlineLvl 1 for level 2, tab stops with their kind and leader), and read back from a
/// paragraph's w:pPr (see Of), so that what Claude reads and writes back stays as it was.
/// </summary>
public sealed partial record ParagraphLook
{
    /// <summary>left, center, right, justify or distribute.</summary>
    public string? Align { get; init; }

    /// <summary>The indents in points: from the left, from the right, the first line's (below 0 hanging).</summary>
    public double? Left { get; init; }
    public double? Right { get; init; }
    public double? First { get; init; }

    /// <summary>The space before and after in points, or Word's automatic spacing (HTML's, w:beforeAutospacing) where so marked.</summary>
    public double? Before { get; init; }
    public double? After { get; init; }
    public bool? BeforeAuto { get; init; }
    public bool? AfterAuto { get; init; }

    /// <summary>The line spacing: a multiple of single lines, or exactly or at least so many points.</summary>
    public double? Lines { get; init; }
    public double? Exactly { get; init; }
    public double? AtLeast { get; init; }

    public bool? KeepNext { get; init; }
    public bool? KeepLines { get; init; }
    public bool? PageBreak { get; init; }

    /// <summary>The outline level, 1 to 9, or 10 for body text (w:outlineLvl 0 to 9).</summary>
    public int? Level { get; init; }

    /// <summary>The lines round it (w:pBdr), by side: top, left, bottom, right, between (paragraphs alike), bar.</summary>
    public IReadOnlyList<Line>? Borders { get; init; }

    /// <summary>The shading behind it (w:shd's fill, RRGGBB).</summary>
    public string? Fill { get; init; }

    /// <summary>Its tab stops.</summary>
    public IReadOnlyList<Stop>? Tabs { get; init; }

    /// <summary>A line on a side: its kind by Word's name (single, double, thick, dotted, dashed …), width in points, colour (RRGGBB) or automatic.</summary>
    public sealed record Line(string Side, string Kind, double Width, string? Color);

    /// <summary>A tab stop: where (points from the indent's start), its kind (left, center, right, decimal, bar, clear), its leader (dot, hyphen, underscore, heavy, middleDot).</summary>
    public sealed record Stop(double Position, string Kind, string? Leader);

    public bool Empty => Align == null && Left == null && Right == null && First == null && Before == null && After == null && BeforeAuto == null && AfterAuto == null
        && Lines == null && Exactly == null && AtLeast == null && KeepNext == null && KeepLines == null && PageBreak == null && Level == null && Borders == null
        && Fill == null && Tabs == null;

    static readonly string[] Sides = ["top", "left", "bottom", "right", "between", "bar"];

    /// <summary>Word's line borders by name (ST_Border's that are no art).</summary>
    static readonly string[] Kinds =
    [
        "single", "thick", "double", "dotted", "dashed", "dotDash", "dotDotDash", "triple", "thinThickSmallGap", "thickThinSmallGap", "thinThickThinSmallGap",
        "thinThickMediumGap", "thickThinMediumGap", "thinThickThinMediumGap", "thinThickLargeGap", "thickThinLargeGap", "thinThickThinLargeGap", "wave",
        "doubleWave", "dashSmallGap", "dashDotStroked", "threeDEmboss", "threeDEngrave", "outset", "inset", "none",
    ];

    /// <summary>The words a paragraph's text begins with where they are its settings ("{align=right after=12pt} text"), and the text after them.</summary>
    [GeneratedRegex(@"^\{([^{}|\n]*)\}\s*")]
    private static partial Regex Head();

    /// <summary>
    /// A paragraph's own settings taken from the start of its text, and the text after them; no look where the text begins with none (with
    /// words that are not all the settings of a paragraph, "{drop}", "{index …}", the text stays as it is).
    /// </summary>
    public static (ParagraphLook? Look, string Text) Take(string text)
    {
        if (Head().Match(text) is not { Success: true } head || RunLook.Settings(head.Groups[1].Value) is not { } words)
            return (null, text);
        var look = new ParagraphLook();
        foreach (var (name, value) in words)
        {
            if (look.With(name, value) is not { } given)
                return (null, text);
            look = given;
        }
        return (look, text[head.Length..]);
    }

    /// <summary>This look given a setting of the notation; null where it is no setting, or its value none it takes.</summary>
    ParagraphLook? With(string name, string? value)
    {
        bool? Flag() => value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "yes" or "true" or "on" => true,
            "no" or "false" or "off" or "none" => false,
            _ => null,
        };
        switch (name.ToLowerInvariant())
        {
            case "align" or "alignment":
                return value?.Trim().ToLowerInvariant() switch
                {
                    "left" or "start" => this with { Align = "left" },
                    "center" or "centre" or "middle" => this with { Align = "center" },
                    "right" or "end" => this with { Align = "right" },
                    "justify" or "justified" or "both" or "block" => this with { Align = "justify" },
                    "distribute" or "distributed" => this with { Align = "distribute" },
                    _ => null,
                };
            case "left" or "indent":
                return Lengths.Signed(value) is { } left ? this with { Left = left } : null;
            case "right":
                return Lengths.Signed(value) is { } right ? this with { Right = right } : null;
            case "first" or "firstline":
                return Lengths.Signed(value) is { } first ? this with { First = first } : null;
            case "hanging":
                return Lengths.Points(value) is { } hanging ? this with { First = -hanging } : null;
            case "before" or "above":
                return value?.Trim().ToLowerInvariant() == "auto" ? this with { BeforeAuto = true, Before = null }
                    : Lengths.Points(value) is { } before ? this with { Before = before, BeforeAuto = null } : null;
            case "after" or "below":
                return value?.Trim().ToLowerInvariant() == "auto" ? this with { AfterAuto = true, After = null }
                    : Lengths.Points(value) is { } after ? this with { After = after, AfterAuto = null } : null;
            case "lines" or "line" or "spacing":
            {
                var given = value?.Trim().ToLowerInvariant() ?? "";
                var (rule, amount) = given.Split(':', 2) is [var r, var a] ? (r, a) : ("", given);
                switch (rule)
                {
                    case "exactly" or "exact":
                        return Lengths.Points(amount) is > 0 and var exactly ? this with { Exactly = exactly, Lines = null, AtLeast = null } : null;
                    case "atleast" or "least" or "min":
                        return Lengths.Points(amount) is > 0 and var least ? this with { AtLeast = least, Lines = null, Exactly = null } : null;
                    case "":
                        // A number alone is a multiple of single lines; with a unit, exactly so high.
                        if (double.TryParse(amount, NumberStyles.Float, CultureInfo.InvariantCulture, out var multiple))
                            return multiple is > 0 and <= 132 ? this with { Lines = multiple, Exactly = null, AtLeast = null } : null;
                        return amount switch
                        {
                            "single" => this with { Lines = 1, Exactly = null, AtLeast = null },
                            "double" => this with { Lines = 2, Exactly = null, AtLeast = null },
                            _ => Lengths.Points(amount) is > 0 and var height ? this with { Exactly = height, Lines = null, AtLeast = null } : null,
                        };
                    default:
                        return null;
                }
            }
            case "keepnext" or "keepwithnext":
                return Flag() is { } next ? this with { KeepNext = next } : null;
            case "keeplines" or "keeptogether":
                return Flag() is { } lines ? this with { KeepLines = lines } : null;
            case "pagebreak" or "pagebreakbefore":
                return Flag() is { } page ? this with { PageBreak = page } : null;
            case "level" or "outline":
                return value?.Trim().ToLowerInvariant() is "body" or "text" ? this with { Level = 10 }
                    : int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) && level is >= 1 and <= 9 ? this with { Level = level } : null;
            case "border" or "borders":
                return Sided(value) is { } sided ? this with { Borders = sided } : null;
            case "fill" or "shade" or "shading":
                return TableWords.FillHex(value, out var known) is { } fill && known ? this with { Fill = fill } : value?.Trim().ToLowerInvariant() == "none" ? this with { Fill = "auto" } : null;
            case "tabs":
                return Stops(value) is { } stops ? this with { Tabs = stops } : null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Lines round a paragraph as the notation gives them: "box" (the four sides), "bottom", "top+bottom", each with its kind, width and
    /// colour after colons where they are not a single line half a point wide in the automatic colour ("bottom:double:1.5pt:#2F6DB5"),
    /// several split by commas; "none" for none.
    /// </summary>
    static IReadOnlyList<Line>? Sided(string? given)
    {
        if (string.IsNullOrWhiteSpace(given))
            return null;
        if (given.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
            return [];
        var lines = new List<Line>();
        foreach (var part in given.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            var tokens = part.Split(':').Select(t => t.Trim()).ToList();
            var sides = new List<string>();
            foreach (var side in tokens[0].Split('+').Select(s => s.Trim().ToLowerInvariant()))
            {
                if (side is "box" or "outside" or "all")
                    sides.AddRange(side == "all" ? ["top", "left", "bottom", "right", "between"] : ["top", "left", "bottom", "right"]);
                else if (Sides.Contains(side))
                    sides.Add(side);
                else
                    return null;
            }
            var (kind, width, color) = ("single", 0.5, (string?)null);
            var widthGiven = false;
            foreach (var token in tokens.Skip(1))
            {
                if (Kinds.FirstOrDefault(k => k.Equals(token, StringComparison.OrdinalIgnoreCase)) is { } known)
                    kind = known;
                else if (token.Equals("auto", StringComparison.OrdinalIgnoreCase))
                    color = null;
                else if (token.StartsWith('#') && token.Length == 7 && token[1..].All(Uri.IsHexDigit))
                    color = token[1..].ToUpperInvariant();
                else if (Lengths.Points(token) is > 0 and <= 12 and var points)
                    (width, widthGiven) = (points, true);
                else
                    return null;
            }
            // A thick line is a line and a half wide where no width is given, as Word's Borders' thick lines are.
            if (kind == "thick" && !widthGiven)
                width = 1.5;
            foreach (var side in sides)
            {
                lines.RemoveAll(l => l.Side == side);
                lines.Add(new Line(side, kind, width, color));
            }
        }
        return lines.Count > 0 ? lines : null;
    }

    /// <summary>Tab stops as the notation gives them: "2cm,8cm:right,15cm:decimal:dot", each a place and its kind and leader where it is not left without one.</summary>
    static IReadOnlyList<Stop>? Stops(string? given)
    {
        if (string.IsNullOrWhiteSpace(given))
            return null;
        if (given.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
            return [];
        var stops = new List<Stop>();
        foreach (var part in given.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            var tokens = part.Split(':').Select(t => t.Trim().ToLowerInvariant()).ToList();
            if (Lengths.Points(tokens[0]) is not { } position)
                return null;
            var (kind, leader) = ("left", (string?)null);
            foreach (var token in tokens.Skip(1))
            {
                if (token is "left" or "center" or "right" or "decimal" or "bar" or "clear")
                    kind = token;
                else if (token is "dot" or "dots" or "dotted")
                    leader = "dot";
                else if (token is "hyphen" or "dash" or "dashes")
                    leader = "hyphen";
                else if (token is "underscore" or "line")
                    leader = "underscore";
                else if (token is "heavy")
                    leader = "heavy";
                else if (token is "middledot")
                    leader = "middleDot";
                else if (token is not "none")
                    return null;
            }
            stops.Add(new Stop(position, kind, leader));
        }
        return stops.OrderBy(s => s.Position).ToList();
    }

    static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The look as the notation's words, in an order of their own, empty for none.</summary>
    public string Words()
    {
        var words = new List<string>();
        void Flag(string name, bool? value)
        {
            if (value is { } on)
                words.Add(on ? name : name + "=no");
        }
        if (Align is { } align)
            words.Add("align=" + align);
        if (Left is { } left)
            words.Add("left=" + Lengths.Cm(left));
        if (Right is { } right)
            words.Add("right=" + Lengths.Cm(right));
        if (First is { } first)
            words.Add(first < 0 ? "hanging=" + Lengths.Cm(-first) : "first=" + Lengths.Cm(first));
        if (BeforeAuto == true)
            words.Add("before=auto");
        else if (Before is { } before)
            words.Add("before=" + Lengths.Pt(before));
        if (AfterAuto == true)
            words.Add("after=auto");
        else if (After is { } after)
            words.Add("after=" + Lengths.Pt(after));
        if (Lines is { } lines)
            words.Add("lines=" + Number(lines));
        else if (Exactly is { } exactly)
            words.Add("lines=exactly:" + Lengths.Pt(exactly));
        else if (AtLeast is { } least)
            words.Add("lines=atleast:" + Lengths.Pt(least));
        Flag("keepnext", KeepNext);
        Flag("keeplines", KeepLines);
        Flag("pagebreak", PageBreak);
        if (Level is { } level)
            words.Add("level=" + (level == 10 ? "body" : level.ToString(CultureInfo.InvariantCulture)));
        if (Borders is { } borders)
            words.Add("border=" + BorderWords(borders));
        if (Fill is { } fill)
            words.Add("fill=" + (fill == "auto" ? "none" : TableWords.FillName(fill)));
        if (Tabs is { } tabs)
            words.Add("tabs=" + (tabs.Count == 0 ? "none" : string.Join(",", tabs.Select(t => Lengths.Cm(t.Position) + (t.Kind != "left" ? ":" + t.Kind : "") + (t.Leader is { } l ? ":" + l : "")))));
        return string.Join(" ", words);
    }

    /// <summary>Lines round a paragraph as the notation writes them (see Sided): the four sides as "box" where they are alike.</summary>
    static string BorderWords(IReadOnlyList<Line> lines)
    {
        if (lines.Count == 0)
            return "none";
        static string Look(Line line) => (line.Kind != "single" ? ":" + line.Kind : "")
            + (Math.Abs(line.Width - (line.Kind == "thick" ? 1.5 : 0.5)) > 0.01 ? ":" + Lengths.Pt(line.Width) : "") + (line.Color is { } c ? ":#" + c : "");
        var parts = new List<string>();
        var box = new[] { "top", "left", "bottom", "right" }.Select(s => lines.FirstOrDefault(l => l.Side == s)).ToList();
        var rest = lines.ToList();
        if (box.All(l => l != null) && box.Select(l => Look(l!)).Distinct().Count() == 1)
        {
            parts.Add("box" + Look(box[0]!));
            rest.RemoveAll(l => l.Side is "top" or "left" or "bottom" or "right");
        }
        foreach (var group in rest.OrderBy(l => Array.IndexOf(Sides, l.Side)).GroupBy(Look))
            parts.Add(string.Join("+", group.Select(l => l.Side)) + group.Key);
        return string.Join(",", parts);
    }

    /// <summary>
    /// The properties of a paragraph's w:pPr as Word writes them (see the class), after its style's, in CT_PPr's order: keeping, a page
    /// break before, lines, shading, tabs, spacing, indents, the alignment (that given where the look has none: a table's column's, a
    /// picture's line), the outline level.
    /// </summary>
    public static string Xml(ParagraphLook? look, string? align)
    {
        static string Twips(double points) => ((int)Math.Round(points * 20)).ToString(CultureInfo.InvariantCulture);
        var markup = new StringBuilder();
        void Toggle(string name, bool? value)
        {
            if (value is { } on)
                markup.Append(on ? $"<w:{name}/>" : $"<w:{name} w:val=\"0\"/>");
        }
        if (look != null)
        {
            Toggle("keepNext", look.KeepNext);
            Toggle("keepLines", look.KeepLines);
            Toggle("pageBreakBefore", look.PageBreak);
            if (look.Borders is { } borders)
            {
                markup.Append("<w:pBdr>");
                foreach (var side in Sides)
                {
                    if (borders.FirstOrDefault(l => l.Side == side) is not { } line)
                        continue;
                    // Eighths of a point; a point from the text above and below, four beside it, as Word's Borders put them.
                    var size = Math.Clamp((int)Math.Round(line.Width * 8), 2, 96).ToString(CultureInfo.InvariantCulture);
                    var space = side is "left" or "right" ? "4" : "1";
                    markup.Append(line.Kind == "none" ? $"<w:{side} w:val=\"nil\"/>"
                        : $"<w:{side} w:val=\"{line.Kind}\" w:sz=\"{size}\" w:space=\"{space}\" w:color=\"{line.Color ?? "auto"}\"/>");
                }
                markup.Append("</w:pBdr>");
            }
            if (look.Fill is { } fill)
                markup.Append($"<w:shd w:val=\"clear\" w:color=\"auto\" w:fill=\"{fill}\"/>");
            if (look.Tabs is { Count: > 0 } tabs)
            {
                markup.Append("<w:tabs>");
                foreach (var tab in tabs)
                    markup.Append($"<w:tab w:val=\"{tab.Kind}\"").Append(tab.Leader is { } leader ? $" w:leader=\"{leader}\"" : "").Append($" w:pos=\"{Twips(tab.Position)}\"/>");
                markup.Append("</w:tabs>");
            }
            var spacing = new StringBuilder();
            if (look.Before is { } before)
                spacing.Append($" w:before=\"{Twips(before)}\"");
            if (look.BeforeAuto == true)
                spacing.Append(" w:beforeAutospacing=\"1\"");
            if (look.After is { } after)
                spacing.Append($" w:after=\"{Twips(after)}\"");
            if (look.AfterAuto == true)
                spacing.Append(" w:afterAutospacing=\"1\"");
            if (look.Lines is { } lines)
                spacing.Append($" w:line=\"{((int)Math.Round(lines * 240)).ToString(CultureInfo.InvariantCulture)}\" w:lineRule=\"auto\"");
            else if (look.Exactly is { } exactly)
                spacing.Append($" w:line=\"{Twips(exactly)}\" w:lineRule=\"exact\"");
            else if (look.AtLeast is { } least)
                spacing.Append($" w:line=\"{Twips(least)}\" w:lineRule=\"atLeast\"");
            if (spacing.Length > 0)
                markup.Append("<w:spacing").Append(spacing).Append("/>");
            var indents = new StringBuilder();
            if (look.Left is { } left)
                indents.Append($" w:left=\"{Twips(left)}\"");
            if (look.Right is { } right)
                indents.Append($" w:right=\"{Twips(right)}\"");
            if (look.First is { } first)
                indents.Append(first < 0 ? $" w:hanging=\"{Twips(-first)}\"" : $" w:firstLine=\"{Twips(first)}\"");
            if (indents.Length > 0)
                markup.Append("<w:ind").Append(indents).Append("/>");
        }
        if ((look?.Align ?? align) is { } aligned)
            markup.Append("<w:jc w:val=\"").Append(aligned == "justify" ? "both" : aligned).Append("\"/>");
        if (look?.Level is { } level)
            markup.Append($"<w:outlineLvl w:val=\"{(level - 1).ToString(CultureInfo.InvariantCulture)}\"/>");
        return markup.ToString();
    }

    /// <summary>
    /// The look a paragraph's own properties give (the children of its w:pPr but those said otherwise: its style, its mark's look, its section,
    /// a list the notation says by its number), and those of them the notation does not say (a frame, East Asian typography, right to left,
    /// a change tracked …). Where it stands alone in its paragraph, an object's alignment and keeping with its caption go without saying,
    /// as a token alone is written so again (see Markup).
    /// </summary>
    public static (ParagraphLook Look, List<XElement> Other) Of(IEnumerable<XElement> properties, bool alone)
    {
        var look = new ParagraphLook();
        var other = new List<XElement>();
        static double Twips(string? value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var twips) ? twips / 20 : 0;
        foreach (var e in properties)
        {
            string? Val(string name = "val") => (string?)e.Attributes().FirstOrDefault(a => a.Name.LocalName == name);
            bool On() => Val() is not ("0" or "false" or "off");
            switch (e.Name.LocalName)
            {
                case "keepNext":
                    if (!alone)
                        look = look with { KeepNext = On() };
                    break;
                case "keepLines":
                    look = look with { KeepLines = On() };
                    break;
                case "pageBreakBefore":
                    look = look with { PageBreak = On() };
                    break;
                case "jc":
                {
                    var align = Val() switch
                    {
                        "left" or "start" => "left",
                        "center" => "center",
                        "right" or "end" => "right",
                        "both" => "justify",
                        "distribute" => "distribute",
                        _ => null,
                    };
                    if (align == null)
                        other.Add(e);
                    else if (!alone)
                        look = look with { Align = align };
                    break;
                }
                case "ind":
                {
                    if (e.Attributes().Any(a => a.Name.LocalName.EndsWith("Chars", StringComparison.Ordinal)))
                    {
                        other.Add(e);
                        break;
                    }
                    if ((Val("left") ?? Val("start")) is { } left)
                        look = look with { Left = Twips(left) };
                    if ((Val("right") ?? Val("end")) is { } right)
                        look = look with { Right = Twips(right) };
                    if (Val("hanging") is { } hanging)
                        look = look with { First = -Twips(hanging) };
                    else if (Val("firstLine") is { } first)
                        look = look with { First = Twips(first) };
                    break;
                }
                case "spacing":
                {
                    if (Val("beforeLines") != null || Val("afterLines") != null)
                    {
                        other.Add(e);
                        break;
                    }
                    if (Val("beforeAutospacing") is "1" or "true" or "on")
                        look = look with { BeforeAuto = true };
                    else if (Val("before") is { } before)
                        look = look with { Before = Twips(before) };
                    if (Val("afterAutospacing") is "1" or "true" or "on")
                        look = look with { AfterAuto = true };
                    else if (Val("after") is { } after)
                        look = look with { After = Twips(after) };
                    if (Val("line") is { } line)
                    {
                        look = Val("lineRule") switch
                        {
                            "exact" => look with { Exactly = Twips(line) },
                            "atLeast" => look with { AtLeast = Twips(line) },
                            _ => look with { Lines = Math.Round(Twips(line) * 20 / 240, 2) },
                        };
                    }
                    break;
                }
                case "outlineLvl":
                    if (int.TryParse(Val(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) && level is >= 0 and <= 9)
                        look = look with { Level = level + 1 };
                    break;
                case "pBdr":
                {
                    var lines = new List<Line>();
                    foreach (var side in e.Elements())
                    {
                        var name = side.Name.LocalName;
                        if (!Sides.Contains(name))
                            continue;
                        var kind = (string?)side.Attributes().FirstOrDefault(a => a.Name.LocalName == "val");
                        if (kind is null or "nil" or "none")
                        {
                            lines.Add(new Line(name, "none", 0.5, null));
                            continue;
                        }
                        if (Kinds.FirstOrDefault(k => k.Equals(kind, StringComparison.Ordinal)) is not { } known)
                        {
                            other.Add(e);
                            lines.Clear();
                            break;
                        }
                        var size = double.TryParse((string?)side.Attributes().FirstOrDefault(a => a.Name.LocalName == "sz"), NumberStyles.Float, CultureInfo.InvariantCulture, out var eighths) ? eighths / 8 : 0.5;
                        var color = (string?)side.Attributes().FirstOrDefault(a => a.Name.LocalName == "color") is { Length: 6 } hex && hex.All(Uri.IsHexDigit) ? hex.ToUpperInvariant() : null;
                        lines.Add(new Line(name, known, size, color));
                    }
                    if (lines.Count > 0)
                        look = look with { Borders = lines };
                    break;
                }
                case "shd":
                    if (Val() is "clear" or "nil" && (string?)e.Attributes().FirstOrDefault(a => a.Name.LocalName == "fill") is { Length: 6 } fill && fill.All(Uri.IsHexDigit))
                        look = look with { Fill = fill.ToUpperInvariant() };
                    else if (Val() is not ("clear" or "nil"))
                        other.Add(e);
                    break;
                case "tabs":
                {
                    var stops = new List<Stop>();
                    foreach (var tab in e.Elements().Where(t => t.Name.LocalName == "tab"))
                    {
                        string? TabVal(string name) => (string?)tab.Attributes().FirstOrDefault(a => a.Name.LocalName == name);
                        var kind = TabVal("val") switch
                        {
                            "left" or "start" => "left",
                            "right" or "end" => "right",
                            "center" or "decimal" or "bar" or "clear" => TabVal("val"),
                            _ => null,
                        };
                        if (kind == null)
                        {
                            other.Add(e);
                            stops.Clear();
                            break;
                        }
                        var leader = TabVal("leader") is { } given and not "none" ? given : null;
                        stops.Add(new Stop(Twips(TabVal("pos")), kind, leader));
                    }
                    if (stops.Count > 0)
                        look = look with { Tabs = stops };
                    break;
                }
                default:
                    other.Add(e);
                    break;
            }
        }
        return (look, other);
    }
}
