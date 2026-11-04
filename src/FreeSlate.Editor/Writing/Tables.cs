using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;

namespace FreeSlate.Editor.Writing;

/// <summary>A style the tones give tables: its id in the document and its name's key.</summary>
public sealed record TableStyle(string Style, string NameKey);

/// <summary>
/// A kind of look for a table beside its tone's own: its table style's id, its name's key (the field's and the style's), its icon, and
/// its lines (w:tblBorders of the table and the line its head has, of its first row below it, of its first column right of it), in the
/// tone's colours for light lines and for rules (see Tables.Made).
/// </summary>
public sealed record TableKind(string Style, string NameKey, string Icon, string Borders, string Head);

/// <summary>
/// Tables as the tones have them: a table style for the table itself (its lines, its head) and a paragraph style for the text in its
/// cells, which every tone defines in its own way (see Tones), so that a table takes the look of the document it is in and changes
/// with its tone. The text needs a style of its own because the normal one of a tone is set for running text (spacing after,
/// justified in the magazine), and a table style cannot override a paragraph style (Word's order: table, then paragraph styles).
/// Beside the tone's own look a table can have one of the kinds (a grid, lines between rows, a book's rules, a frame), each the tone's
/// table style with other lines, so that it keeps the tone's type, margins and head.
/// </summary>
public static class Tables
{
    public static readonly TableStyle Table = new("Table", "StyleTable");

    public static readonly TableStyle Text = new("TableText", "StyleTableText");

    public static readonly IReadOnlyList<TableStyle> Styles = [Table, Text];

    /// <summary>The numbers of columns a new table can have, in the ring that puts one in.</summary>
    public static readonly IReadOnlyList<int> Columns = [2, 3, 4, 5, 6];

    /// <summary>The rows a new table has: the head and two more; Tab in the last cell adds more.</summary>
    public const int Rows = 3;

    const string Light = "w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"{light}\"", Rule = "w:val=\"single\" w:sz=\"{size}\" w:space=\"0\" w:color=\"{dark}\"";

    /// <summary>The kinds of look: a grid of light lines, light lines between the rows, a book's rules above and below, a frame.</summary>
    public static readonly IReadOnlyList<TableKind> Kinds =
    [
        new("TableLines", "StyleTableLines", Placement.TableIcon,
            $"<w:tblBorders><w:top {Light} /><w:left {Light} /><w:bottom {Light} /><w:right {Light} /><w:insideH {Light} /><w:insideV {Light} /></w:tblBorders>",
            $"<w:tcBorders><w:{{side}} {Rule.Replace("{size}", "6")} /></w:tcBorders>"),
        new("TableRows", "StyleTableRows", Placement.LookRowsIcon,
            $"<w:tblBorders><w:bottom {Light} /><w:insideH {Light} /></w:tblBorders>",
            $"<w:tcBorders><w:{{side}} {Rule.Replace("{size}", "6")} /></w:tcBorders>"),
        new("TableBook", "StyleTableBook", Placement.LookBookIcon,
            $"<w:tblBorders><w:top {Rule.Replace("{size}", "12")} /><w:bottom {Rule.Replace("{size}", "12")} /></w:tblBorders>",
            $"<w:tcBorders><w:{{side}} {Rule.Replace("{size}", "6")} /></w:tcBorders>"),
        new("TableFrame", "StyleTableFrame", Placement.LookFrameIcon,
            $"<w:tblBorders><w:top {Rule.Replace("{size}", "4")} /><w:left {Rule.Replace("{size}", "4")} /><w:bottom {Rule.Replace("{size}", "4")} /><w:right {Rule.Replace("{size}", "4")} /></w:tblBorders>",
            $"<w:tcBorders><w:{{side}} {Rule.Replace("{size}", "4")} /></w:tcBorders>"),
    ];

    /// <summary>
    /// A kind's table style made of the tone's own (see Kinds): the same type, margins and head, the table's lines the kind's, the head's
    /// line below its first row and right of its first column the kind's; named in the language of the interface.
    /// </summary>
    public static XElement Made(XElement own, TableKind kind, string light, string dark)
    {
        var w = own.Name.Namespace;
        XElement Parsed(string markup) =>
            new(XElement.Parse($"<x xmlns:w=\"{w}\">{markup.Replace("{light}", light).Replace("{dark}", dark)}</x>").Elements().First());
        var made = new XElement(own);
        made.SetAttributeValue(w + "styleId", kind.Style);
        made.Element(w + "name")?.SetAttributeValue(w + "val", Name(kind.Style, kind.NameKey));
        var tblPr = made.Element(w + "tblPr");
        if (tblPr == null)
        {
            tblPr = new XElement(w + "tblPr");
            made.Add(tblPr);
        }
        tblPr.Element(w + "tblBorders")?.Remove();
        tblPr.AddFirst(Parsed(kind.Borders));
        foreach (var part in made.Elements(w + "tblStylePr"))
        {
            if ((string?)part.Attribute(w + "type") is not ("firstRow" or "firstCol") || part.Attribute(w + "type") is not { } type)
                continue;
            var tcPr = part.Element(w + "tcPr");
            if (tcPr == null)
            {
                tcPr = new XElement(w + "tcPr");
                part.Add(tcPr);
            }
            tcPr.Element(w + "tcBorders")?.Remove();
            tcPr.AddFirst(Parsed(kind.Head.Replace("{side}", type.Value == "firstRow" ? "bottom" : "right")));
        }
        return made;
    }

    /// <summary>A style's name in the language of the interface.</summary>
    public static string Name(TableStyle style) => Name(style.Style, style.NameKey);

    static string Name(string style, string key) =>
        Application.Current?.TryFindResource(key, out var name) == true && name is string text ? text : style;
}
