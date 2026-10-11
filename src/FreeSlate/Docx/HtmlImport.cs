using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>
/// Imports an HTML chunk (w:altChunk, catalog: document.altChunk) as WordprocessingML, the way Word converts it when it
/// opens the document (measured with docx/local/altchunk): paragraphs and headings in Times New Roman (12 pt; headings
/// bold at 24, 18, 13.5, 12, 10 and 7.5 pt) with 14 pt of HTML auto spacing around them, none after the chunk's last
/// block; list items in the document's own font with a Symbol bullet or a number, 36 pt in and hanging 18 pt, no
/// space between them; tables with their cells; bold, italic, underline, strike, sub- and superscript, colors, sizes
/// and fonts of style attributes, links and line breaks.
/// </summary>
public static class HtmlImport
{
    static readonly XNamespace W = Wml.W;
    static readonly HashSet<string> Void = ["br", "hr", "img", "meta", "link", "input", "col", "area", "base", "wbr", "source", "param"];
    static readonly HashSet<string> Blocks = ["p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "ul", "ol", "li", "table", "tr", "td", "th",
        "blockquote", "pre", "body", "html", "thead", "tbody", "tfoot", "section", "article", "header", "footer", "dl", "dt", "dd"];

    /// <summary>An element of the parsed HTML: its name, attributes and children (elements or text).</summary>
    sealed class Node(string name)
    {
        public string Name { get; } = name;
        public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<object> Children { get; } = [];
        public Node? Parent { get; set; }
    }

    /// <summary>The blocks (w:p, w:tbl) of an HTML document.</summary>
    public static List<XElement> Convert(string html)
    {
        var root = Parse(html);
        var body = Find(root, "body") ?? root;
        var result = new List<XElement>();
        ConvertBlocks(body, new Run(), result, list: null);
        // No space after the chunk's last block.
        if (result.LastOrDefault(e => e.Name == W + "p") is { } last && last.Element(W + "pPr")?.Element(W + "spacing") is { } spacing)
        {
            spacing.SetAttributeValue(W + "after", "0");
            spacing.Attribute(W + "afterAutospacing")?.Remove();
        }
        return result;
    }

    /// <summary>Character formatting inherited from the elements around a run.</summary>
    sealed record Run
    {
        public bool Bold { get; init; }
        public bool Italic { get; init; }
        public bool Underline { get; init; }
        public bool Strike { get; init; }
        public string? Vertical { get; init; }
        public string? Color { get; init; }
        public double? Size { get; init; }
        public string? Font { get; init; }
        public string? Link { get; init; }
    }

    static void ConvertBlocks(Node node, Run run, List<XElement> output, (string Kind, int Level)? list)
    {
        var inline = new List<object>();
        void Flush(string style = "p")
        {
            if (inline.Count == 0 || inline.All(c => c is string s && string.IsNullOrWhiteSpace(s)))
            {
                inline.Clear();
                return;
            }
            output.Add(Paragraph(new Node(style) { }, inline.ToList(), run, null, 0));
            inline.Clear();
        }
        foreach (var child in node.Children)
        {
            if (child is Node element && Blocks.Contains(element.Name))
            {
                Flush();
                var styled = Styled(element, run);
                switch (element.Name)
                {
                    case "ul" or "ol":
                        var level = (list?.Level ?? -1) + 1;
                        ConvertBlocks(element, styled, output, (element.Name, level));
                        break;
                    case "li":
                        ConvertListItem(element, styled, output, list ?? ("ul", 0));
                        break;
                    case "table":
                        output.Add(Table(element, styled));
                        break;
                    case "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "pre" or "dt" or "dd":
                        if (element.Children.Any(c => c is Node n && Blocks.Contains(n.Name)))
                            ConvertBlocks(element, styled, output, list);
                        else
                            output.Add(Paragraph(element, element.Children, styled, null, 0));
                        break;
                    case "blockquote":
                    {
                        var start = output.Count;
                        ConvertBlocks(element, styled, output, list);
                        for (var k = start; k < output.Count; k++)
                            Indent(output[k], 36, 36);
                        break;
                    }
                    default:
                        ConvertBlocks(element, styled, output, list);
                        break;
                }
            }
            else
                inline.Add(child);
        }
        Flush();
    }

    static void ConvertListItem(Node item, Run run, List<XElement> output, (string Kind, int Level) list)
    {
        var items = item.Parent?.Children.OfType<Node>().Where(n => n.Name == "li").ToList() ?? [item];
        var number = list.Kind == "ol" ? items.IndexOf(item) + 1 : (int?)null;
        var inline = item.Children.Where(c => c is not Node n || !Blocks.Contains(n.Name)).ToList();
        var paragraph = Paragraph(new Node("li"), inline, run, number, list.Level);
        // The list keeps the auto spacing of a block before its first and after its last item.
        var spacing = paragraph.Element(W + "pPr")!.Element(W + "spacing")!;
        if (ReferenceEquals(items[0], item) && list.Level == 0)
            spacing.SetAttributeValue(W + "beforeAutospacing", "1");
        if (ReferenceEquals(items[^1], item) && list.Level == 0)
            spacing.SetAttributeValue(W + "afterAutospacing", "1");
        output.Add(paragraph);
        foreach (var nested in item.Children.OfType<Node>().Where(n => Blocks.Contains(n.Name)))
            ConvertBlocks(new Node("div") { Children = { nested } }, run, output, list);
    }

    /// <summary>A paragraph for a block element and its inline content.</summary>
    static XElement Paragraph(Node block, List<object> inline, Run run, int? number, int level)
    {
        var heading = block.Name is ['h', >= '1' and <= '6'] ? block.Name[1] - '0' : 0;
        var pPr = new XElement(W + "pPr");
        var paragraphRun = run;
        if (heading > 0)
            paragraphRun = run with { Bold = true, Size = run.Size ?? new[] { 24, 18, 13.5, 12, 10, 7.5 }[heading - 1] };
        if (block.Name == "li")
        {
            pPr.Add(new XElement(W + "spacing", new XAttribute(W + "before", "0"), new XAttribute(W + "after", "0"), new XAttribute(W + "line", "240"), new XAttribute(W + "lineRule", "auto")));
            pPr.Add(new XElement(W + "ind", new XAttribute(W + "left", (720 + 360 * level).ToString(CultureInfo.InvariantCulture)), new XAttribute(W + "hanging", "360")));
        }
        else
        {
            pPr.Add(new XElement(W + "spacing", new XAttribute(W + "before", "100"), new XAttribute(W + "beforeAutospacing", "1"),
                new XAttribute(W + "after", "100"), new XAttribute(W + "afterAutospacing", "1"), new XAttribute(W + "line", "240"), new XAttribute(W + "lineRule", "auto")));
            // Paragraphs and headings are Times New Roman unless they say otherwise; list items keep the document's font.
            paragraphRun = paragraphRun with { Font = paragraphRun.Font ?? (block.Name == "pre" ? "Courier New" : "Times New Roman"), Size = paragraphRun.Size ?? (block.Name == "pre" ? 10 : 12) };
        }
        if (Style(block, "text-align") is { } align)
            pPr.Add(new XElement(W + "jc", new XAttribute(W + "val", align switch { "center" => "center", "right" => "right", "justify" => "both", _ => "left" })));
        var p = new XElement(W + "p", pPr);
        if (block.Name == "li")
        {
            var label = number is { } n ? $"{n}." : "\uF0B7";
            var labelRun = number != null ? Rpr(paragraphRun) : new XElement(W + "rPr", new XElement(W + "rFonts", new XAttribute(W + "ascii", "Symbol"), new XAttribute(W + "hAnsi", "Symbol")), new XElement(W + "sz", new XAttribute(W + "val", "20")));
            p.Add(new XElement(W + "r", labelRun, new XElement(W + "t", label)), new XElement(W + "r", Rpr(paragraphRun), new XElement(W + "tab")));
        }
        var pre = block.Name == "pre";
        Inline(inline, paragraphRun, p, pre, true);
        return p;
    }

    /// <summary>Inline content into runs: whitespace collapsed (kept in pre), formatting from the elements.</summary>
    static bool Inline(List<object> content, Run run, XElement paragraph, bool pre, bool atStart)
    {
        foreach (var item in content)
        {
            if (item is string text)
            {
                var shown = pre ? text : Regex.Replace(text, "\\s+", " ");
                if (!pre && atStart)
                    shown = shown.TrimStart();
                if (shown.Length == 0)
                    continue;
                if (pre)
                {
                    var lines = shown.Replace("\r\n", "\n").Split('\n');
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (i > 0)
                            paragraph.Add(new XElement(W + "r", Rpr(run), new XElement(W + "br")));
                        if (lines[i].Length > 0)
                            paragraph.Add(TextRun(lines[i], run));
                    }
                }
                else
                    paragraph.Add(TextRun(shown, run));
                atStart = shown.EndsWith(' ');
                continue;
            }
            if (item is not Node node)
                continue;
            switch (node.Name)
            {
                case "br":
                    paragraph.Add(new XElement(W + "r", Rpr(run), new XElement(W + "br")));
                    atStart = true;
                    break;
                case "img" or "script" or "style" or "head" or "title":
                    break;
                default:
                    atStart = Inline(node.Children, Styled(node, run), paragraph, pre || node.Name == "pre", atStart);
                    break;
            }
        }
        return atStart;
    }

    static XElement TextRun(string text, Run run)
    {
        var r = new XElement(W + "r", Rpr(run), new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text));
        return r;
    }

    static XElement Rpr(Run run)
    {
        var rPr = new XElement(W + "rPr");
        if (run.Font is { } font)
            rPr.Add(new XElement(W + "rFonts", new XAttribute(W + "ascii", font), new XAttribute(W + "hAnsi", font), new XAttribute(W + "cs", font)));
        if (run.Bold) rPr.Add(new XElement(W + "b"));
        if (run.Italic) rPr.Add(new XElement(W + "i"));
        if (run.Strike) rPr.Add(new XElement(W + "strike"));
        if (run.Color is { } color) rPr.Add(new XElement(W + "color", new XAttribute(W + "val", color)));
        if (run.Size is { } size)
        {
            var half = ((int)Math.Round(size * 2)).ToString(CultureInfo.InvariantCulture);
            rPr.Add(new XElement(W + "sz", new XAttribute(W + "val", half)), new XElement(W + "szCs", new XAttribute(W + "val", half)));
        }
        if (run.Underline) rPr.Add(new XElement(W + "u", new XAttribute(W + "val", "single")));
        if (run.Vertical is { } vertical) rPr.Add(new XElement(W + "vertAlign", new XAttribute(W + "val", vertical)));
        return rPr;
    }

    /// <summary>The formatting an element gives its content: its tag and its style attribute.</summary>
    static Run Styled(Node node, Run run)
    {
        run = node.Name switch
        {
            "b" or "strong" or "th" => run with { Bold = true },
            "i" or "em" or "cite" or "var" => run with { Italic = true },
            "u" or "ins" => run with { Underline = true },
            "s" or "strike" or "del" => run with { Strike = true },
            "sub" => run with { Vertical = "subscript" },
            "sup" => run with { Vertical = "superscript" },
            "code" or "kbd" or "samp" or "tt" => run with { Font = "Courier New" },
            "font" => run with
            {
                Color = node.Attributes.TryGetValue("color", out var c) ? Color(c) ?? run.Color : run.Color,
                Font = node.Attributes.TryGetValue("face", out var face) ? face.Split(',')[0].Trim().Trim('\'', '"') : run.Font,
            },
            _ => run,
        };
        if (Style(node, "font-weight") is { } weight)
            run = run with { Bold = weight is "bold" or "bolder" || int.TryParse(weight, out var w) && w >= 600 };
        if (Style(node, "font-style") is { } style)
            run = run with { Italic = style is "italic" or "oblique" };
        if (Style(node, "text-decoration") is { } decoration)
            run = run with { Underline = decoration.Contains("underline"), Strike = decoration.Contains("line-through") };
        if (Style(node, "color") is { } color && Color(color) is { } hex)
            run = run with { Color = hex };
        if (Style(node, "font-size") is { } size && Length(size, run.Size ?? 12) is { } points)
            run = run with { Size = points };
        if (Style(node, "font-family") is { } family)
            run = run with { Font = family.Split(',')[0].Trim().Trim('\'', '"') };
        return run;
    }

    /// <summary>A table with its rows and cells, cells keeping their inline content in the document's font.</summary>
    static XElement Table(Node table, Run run)
    {
        var rows = Descendants(table).Where(n => n.Name == "tr").ToList();
        var columns = Math.Max(1, rows.Select(r => r.Children.OfType<Node>().Count(c => c.Name is "td" or "th")).DefaultIfEmpty(1).Max());
        var bordered = table.Attributes.TryGetValue("border", out var border) && border != "0";
        var tbl = new XElement(W + "tbl", new XElement(W + "tblPr", new XElement(W + "tblW", new XAttribute(W + "w", "0"), new XAttribute(W + "type", "auto"))));
        if (bordered)
        {
            // A bordered HTML table: an outset frame, inset cells 1.5 pt apart (the default cell spacing of 2 px), the
            // cells' padding 0.75 pt (1 px).
            var pr = tbl.Element(W + "tblPr")!;
            pr.Add(new XElement(W + "tblCellSpacing", new XAttribute(W + "w", "30"), new XAttribute(W + "type", "dxa")));
            pr.Add(new XElement(W + "tblBorders",
                new[] { "top", "left", "bottom", "right" }.Select(side =>
                    new XElement(W + side, new XAttribute(W + "val", "outset"), new XAttribute(W + "sz", "6"), new XAttribute(W + "space", "0"), new XAttribute(W + "color", "A0A0A0"))),
                new[] { "insideH", "insideV" }.Select(side =>
                    new XElement(W + side, new XAttribute(W + "val", "inset"), new XAttribute(W + "sz", "6"), new XAttribute(W + "space", "0"), new XAttribute(W + "color", "A0A0A0")))));
            pr.Add(new XElement(W + "tblCellMar", new[] { "top", "left", "bottom", "right" }.Select(side =>
                new XElement(W + side, new XAttribute(W + "w", "15"), new XAttribute(W + "type", "dxa")))));
        }
        tbl.Add(new XElement(W + "tblGrid", Enumerable.Range(0, columns).Select(_ => new XElement(W + "gridCol"))));
        foreach (var row in rows)
        {
            var tr = new XElement(W + "tr");
            foreach (var cell in row.Children.OfType<Node>().Where(c => c.Name is "td" or "th"))
            {
                var tc = new XElement(W + "tc", new XElement(W + "tcPr"));
                var blocks = new List<XElement>();
                var styled = Styled(cell, run);
                if (cell.Children.Any(c => c is Node n && Blocks.Contains(n.Name)))
                    ConvertBlocks(cell, styled, blocks, null);
                else
                {
                    var p = new XElement(W + "p", new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "before", "0"), new XAttribute(W + "after", "0"))));
                    Inline(cell.Children, styled, p, false, true);
                    blocks.Add(p);
                }
                if (blocks.Count == 0)
                    blocks.Add(new XElement(W + "p"));
                tc.Add(blocks);
                tr.Add(tc);
            }
            if (tr.HasElements)
                tbl.Add(tr);
        }
        return tbl;
    }

    static void Indent(XElement block, double left, double right)
    {
        if (block.Name != W + "p")
            return;
        var pPr = block.Element(W + "pPr") ?? new XElement(W + "pPr");
        if (pPr.Parent == null)
            block.AddFirst(pPr);
        var ind = pPr.Element(W + "ind");
        var (l, r) = (double.TryParse((string?)ind?.Attribute(W + "left"), out var a) ? a : 0, double.TryParse((string?)ind?.Attribute(W + "right"), out var b) ? b : 0);
        ind?.Remove();
        pPr.Add(new XElement(W + "ind", new XAttribute(W + "left", (l + left * 20).ToString(CultureInfo.InvariantCulture)), new XAttribute(W + "right", (r + right * 20).ToString(CultureInfo.InvariantCulture))));
    }

    static string? Style(Node node, string property)
    {
        if (!node.Attributes.TryGetValue("style", out var style))
            return null;
        foreach (var declaration in style.Split(';'))
        {
            var colon = declaration.IndexOf(':');
            if (colon > 0 && declaration[..colon].Trim().Equals(property, StringComparison.OrdinalIgnoreCase))
                return declaration[(colon + 1)..].Trim().ToLowerInvariant();
        }
        return null;
    }

    /// <summary>A CSS color as six hex digits: #rgb, #rrggbb, rgb() or a basic color name.</summary>
    static string? Color(string value)
    {
        value = value.Trim().ToLowerInvariant();
        if (Regex.Match(value, "^#([0-9a-f]{6})$") is { Success: true } six)
            return six.Groups[1].Value.ToUpperInvariant();
        if (Regex.Match(value, "^#([0-9a-f])([0-9a-f])([0-9a-f])$") is { Success: true } three)
            return string.Concat(three.Groups[1].Value, three.Groups[1].Value, three.Groups[2].Value, three.Groups[2].Value, three.Groups[3].Value, three.Groups[3].Value).ToUpperInvariant();
        if (Regex.Match(value, "^rgb\\(\\s*(\\d+)\\s*,\\s*(\\d+)\\s*,\\s*(\\d+)\\s*\\)$") is { Success: true } rgb)
            return string.Concat(rgb.Groups.Values.Skip(1).Select(g => Math.Clamp(int.Parse(g.Value, CultureInfo.InvariantCulture), 0, 255).ToString("X2", CultureInfo.InvariantCulture)));
        return value switch
        {
            "black" => "000000", "white" => "FFFFFF", "red" => "FF0000", "green" => "008000", "blue" => "0000FF",
            "yellow" => "FFFF00", "gray" or "grey" => "808080", "silver" => "C0C0C0", "maroon" => "800000",
            "navy" => "000080", "purple" => "800080", "teal" => "008080", "olive" => "808000", "orange" => "FFA500",
            _ => null,
        };
    }

    /// <summary>A CSS length or font size in points.</summary>
    static double? Length(string value, double inherited)
    {
        var m = Regex.Match(value, "^([0-9.]+)\\s*(pt|px|em|%|rem)?$");
        if (!m.Success || !double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return value switch
            {
                "xx-small" => 7.5, "x-small" => 7.5, "small" => 10, "medium" => 12, "large" => 13.5, "x-large" => 18, "xx-large" => 24,
                _ => null,
            };
        }
        return m.Groups[2].Value switch
        {
            "px" => number * 0.75,
            "em" or "rem" => number * inherited,
            "%" => number / 100 * inherited,
            _ => number,
        };
    }

    static Node? Find(Node node, string name) =>
        node.Name == name ? node : node.Children.OfType<Node>().Select(c => Find(c, name)).FirstOrDefault(n => n != null);

    static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.Children.OfType<Node>())
        {
            yield return child;
            if (child.Name != "table")
            {
                foreach (var nested in Descendants(child))
                    yield return nested;
            }
        }
    }

    /// <summary>A tolerant HTML parser: tags, attributes, text and entities; void elements and unclosed paragraphs, items and cells.</summary>
    static Node Parse(string html)
    {
        var root = new Node("#root");
        var current = root;
        var position = 0;
        var tag = new Regex("\\G<(/?)([A-Za-z][A-Za-z0-9:-]*)((?:\\s+[^\\s=>/]+(?:\\s*=\\s*(?:\"[^\"]*\"|'[^']*'|[^\\s>]+))?)*)\\s*(/?)>", RegexOptions.Compiled);
        var attribute = new Regex("([^\\s=>/]+)(?:\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+)))?");
        var text = new StringBuilder();
        void FlushText()
        {
            if (text.Length > 0)
            {
                current.Children.Add(WebUtility.HtmlDecode(text.ToString()));
                text.Clear();
            }
        }
        while (position < html.Length)
        {
            if (html[position] == '<')
            {
                if (string.CompareOrdinal(html, position, "<!--", 0, 4) == 0)
                {
                    var end = html.IndexOf("-->", position + 4, StringComparison.Ordinal);
                    position = end < 0 ? html.Length : end + 3;
                    continue;
                }
                if (position + 1 < html.Length && html[position + 1] is '!' or '?')
                {
                    var end = html.IndexOf('>', position);
                    position = end < 0 ? html.Length : end + 1;
                    continue;
                }
                var m = tag.Match(html, position);
                if (m.Success)
                {
                    FlushText();
                    var name = m.Groups[2].Value.ToLowerInvariant();
                    if (m.Groups[1].Value == "/")
                    {
                        // Close the innermost open element of that name.
                        for (var open = current; open != root; open = open.Parent!)
                        {
                            if (open.Name == name)
                            {
                                current = open.Parent!;
                                break;
                            }
                        }
                    }
                    else
                    {
                        // A new paragraph, item, row or cell closes the open one.
                        if (name is "p" or "li" or "tr" or "td" or "th" or "dt" or "dd" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
                        {
                            for (var open = current; open != root; open = open.Parent!)
                            {
                                if (open.Name == name || name is "td" or "th" && open.Name is "td" or "th" || open.Name == "p" && Blocks.Contains(name))
                                {
                                    current = open.Parent!;
                                    break;
                                }
                                if (open.Name is "table" or "ul" or "ol" or "div" or "body")
                                    break;
                            }
                        }
                        var node = new Node(name) { Parent = current };
                        foreach (Match a in attribute.Matches(m.Groups[3].Value))
                            node.Attributes[a.Groups[1].Value] = WebUtility.HtmlDecode(a.Groups[2].Success ? a.Groups[2].Value : a.Groups[3].Success ? a.Groups[3].Value : a.Groups[4].Value);
                        current.Children.Add(node);
                        if (name is "script" or "style")
                        {
                            // Raw text up to the closing tag.
                            var close = html.IndexOf("</" + name, m.Index + m.Length, StringComparison.OrdinalIgnoreCase);
                            position = close < 0 ? html.Length : html.IndexOf('>', close) + 1;
                            continue;
                        }
                        if (!Void.Contains(name) && m.Groups[4].Value != "/")
                            current = node;
                    }
                    position = m.Index + m.Length;
                    continue;
                }
            }
            text.Append(html[position]);
            position++;
        }
        FlushText();
        return root;
    }
}
