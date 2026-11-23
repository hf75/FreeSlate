using FreeSlate.Editor.Writing;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// The words the notation names a table's looks with (see Prompt, Reading, Writing.Tables), as the ring offers them: the table's looks (the
/// tone's own, a grid, lines between rows, a book's rules, a box), the cells' fills, the lines of their sides, where their text stands and
/// which way it runs.
/// </summary>
static class TableWords
{
    /// <summary>The looks by their names: the tone's own table style and the kinds beside it (see Tables.Kinds).</summary>
    static readonly (string Name, string Style)[] Looks =
        [("tone", "Table"), ("grid", "TableLines"), ("rows", "TableRows"), ("book", "TableBook"), ("box", "TableFrame")];

    /// <summary>A look's name by its table style; null for a style of none of the looks.</summary>
    public static string? LookName(string? style) => Looks.FirstOrDefault(l => l.Style == style).Name;

    /// <summary>A look's table style by its name; null where it names none.</summary>
    public static string? LookStyle(string? name) => Looks.FirstOrDefault(l => l.Name.Equals(name?.Trim(), StringComparison.OrdinalIgnoreCase)).Style;

    /// <summary>A fill by its name (gray, yellow, orange, green, blue, rose, violet, see Tables.Fills), else as #RRGGBB.</summary>
    public static string FillName(string hex) =>
        Tables.Fills.FirstOrDefault(f => f.Fill.Equals(hex, StringComparison.OrdinalIgnoreCase)) is { } fill ? fill.NameKey[4..].ToLowerInvariant() : "#" + hex.ToUpperInvariant();

    /// <summary>A fill's colour (hex RRGGBB) by its name or as #RRGGBB; null for none, and (with <paramref name="known"/> false) for a name it does not know.</summary>
    public static string? FillHex(string? name, out bool known)
    {
        known = true;
        var given = name?.Trim() ?? "";
        if (given.Length == 0 || given.Equals("none", StringComparison.OrdinalIgnoreCase))
            return null;
        if (Tables.Fills.FirstOrDefault(f => f.NameKey[4..].Equals(given, StringComparison.OrdinalIgnoreCase) || (given.Equals("grey", StringComparison.OrdinalIgnoreCase) && f.NameKey == "FillGray")) is { } fill)
            return fill.Fill;
        var hex = given.TrimStart('#');
        if (hex.Length == 6 && hex.All(Uri.IsHexDigit))
            return hex.ToUpperInvariant();
        known = false;
        return null;
    }

    /// <summary>Where text stands across a cell, as the notation reads it: left, center or right (justified text stands left so).</summary>
    public static string Across(string? horizontal) => horizontal is "center" or "right" ? horizontal : "left";

    /// <summary>Where text stands down a cell as the notation names it (top, middle, bottom), and as the document does (top, center, bottom).</summary>
    public static string DownName(string vertical) => vertical == "center" ? "middle" : vertical;

    public static string Down(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "middle" or "center" => "center",
        "bottom" => "bottom",
        _ => "top",
    };

    /// <summary>Which way a cell's text runs: up the page (btLr), down it (tbRl), across (null).</summary>
    public static string? TurnName(string? direction) => direction switch { "btLr" => "up", "tbRl" => "down", _ => null };

    public static string? Direction(string? name) => name?.Trim().ToLowerInvariant() switch { "up" => "btLr", "down" => "tbRl", _ => null };

    /// <summary>
    /// The lines a cell's "lines" setting gives its sides: "outside:strong", "bottom:double,top:none" — which (top, bottom, left, right,
    /// outside, inside, across, upright, all) and their kind (fine, strong, double, dotted, dashed, none: null); what it cannot read, said.
    /// </summary>
    public static (List<(CellLines Which, LineKind? Kind)> Lines, List<string> Unread) Lines(string given)
    {
        var lines = new List<(CellLines, LineKind?)>();
        var unread = new List<string>();
        foreach (var part in given.Split(',', ';').Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            var halves = part.Split(':', 2);
            var (whichName, kindName) = halves.Length == 2 ? (halves[0].Trim(), halves[1].Trim()) : ("outside", halves[0].Trim());
            CellLines? which = whichName.ToLowerInvariant() switch
            {
                "top" => CellLines.Top,
                "bottom" => CellLines.Bottom,
                "left" => CellLines.Left,
                "right" => CellLines.Right,
                "outside" or "box" => CellLines.Outside,
                "inside" => CellLines.Inside,
                "across" => CellLines.Across,
                "upright" => CellLines.Upright,
                "all" => CellLines.All,
                _ => null,
            };
            var kind = Tables.Lines.FirstOrDefault(l => l.NameKey[4..].Equals(kindName, StringComparison.OrdinalIgnoreCase));
            var none = kindName.Equals("none", StringComparison.OrdinalIgnoreCase);
            if (which is not { } sides || kind == null && !none)
                unread.Add(part);
            else
                lines.Add((sides, kind));
        }
        return (lines, unread);
    }
}
