using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace FreeSlate.Pptx;

/// <summary>
/// Text the slide show restyles (catalog: animation.behaviors): emphasis effects that make text bold, italic or
/// underlined, change its font or its size (style.fontWeight, style.fontStyle, style.textDecorationUnderline,
/// style.fontFamily, style.fontSize) show it laid out anew in that formatting, in the real faces (measured with
/// PowerPoint's video: Bold Flash draws the bold face, Change Font the other font). The text is drawn once more per
/// formatting the timing gives it; every paragraph (every character, for text built letter by letter) holds its
/// own drawing and the others (data-v), and the player shows the one its state asks for.
/// </summary>
static class TextVariants
{
    static readonly HashSet<string> Attributes =
        ["style.fontWeight", "style.fontStyle", "style.textDecorationUnderline", "style.fontFamily", "style.fontSize"];

    /// <summary>The formattings each shape's text takes in the slide show, by shape id.</summary>
    public static Dictionary<string, List<TextOverride>> Collect(XElement? timing)
    {
        var result = new Dictionary<string, List<TextOverride>>();
        if (timing == null)
            return result;
        // Every effect (a time node with a preset class) gives each shape it restyles the combinations of its values.
        foreach (var effect in timing.Descendants(Ns.P + "cTn").Where(c => c.Attribute("presetClass") != null))
        {
            var shapes = new Dictionary<string, Dictionary<string, List<string>>>();
            foreach (var behavior in effect.Descendants().Where(e => e.Name == Ns.P + "set" || e.Name == Ns.P + "anim"))
            {
                var cBhvr = behavior.Element(Ns.P + "cBhvr");
                var attribute = cBhvr?.Element(Ns.P + "attrNameLst")?.Elements(Ns.P + "attrName").Select(a => a.Value.Trim()).FirstOrDefault(Attributes.Contains);
                if (attribute == null || cBhvr!.Element(Ns.P + "tgtEl")?.Element(Ns.P + "spTgt")?.Attribute("spid")?.Value is not { } spid)
                    continue;
                var values = new List<string?> { Value(behavior.Element(Ns.P + "to")), behavior.Attribute("to")?.Value };
                values.AddRange(behavior.Element(Ns.P + "tavLst")?.Elements(Ns.P + "tav").Select(t => Value(t.Element(Ns.P + "val"))) ?? []);
                if (!shapes.TryGetValue(spid, out var attributes))
                    shapes[spid] = attributes = [];
                if (!attributes.TryGetValue(attribute, out var list))
                    attributes[attribute] = list = [];
                list.AddRange(values.OfType<string>().Where(v => !list.Contains(v)));
            }
            foreach (var (spid, attributes) in shapes)
            {
                if (!result.TryGetValue(spid, out var overrides))
                    result[spid] = overrides = [];
                foreach (var formatting in Combinations(attributes.ToList(), 0, new TextOverride(null, null, null, null)))
                {
                    if (formatting is not { Bold: null, Italic: null, Underline: null, Family: null, Size: 1 } && overrides.All(o => o.Key != formatting.Key))
                        overrides.Add(formatting);
                }
            }
        }
        foreach (var spid in result.Where(r => r.Value.Count == 0).Select(r => r.Key).ToList())
            result.Remove(spid);
        return result;
    }

    static IEnumerable<TextOverride> Combinations(List<KeyValuePair<string, List<string>>> attributes, int index, TextOverride so)
    {
        if (index == attributes.Count)
        {
            yield return so;
            yield break;
        }
        var (attribute, values) = (attributes[index].Key, attributes[index].Value);
        foreach (var value in values)
        {
            var next = attribute switch
            {
                "style.fontWeight" => so with { Bold = value.Equals("bold", StringComparison.OrdinalIgnoreCase) },
                "style.fontStyle" => so with { Italic = value.Equals("italic", StringComparison.OrdinalIgnoreCase) },
                "style.textDecorationUnderline" => so with { Underline = value.Equals("true", StringComparison.OrdinalIgnoreCase) },
                "style.fontFamily" => so with { Family = value },
                _ => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var size) && size > 0 ? so with { Size = size } : so,
            };
            foreach (var combination in Combinations(attributes, index + 1, next))
                yield return combination;
        }
    }

    /// <summary>The value of a p:to or p:val (strVal, fltVal, intVal, boolVal).</summary>
    static string? Value(XElement? holder) => holder?.Elements().FirstOrDefault()?.Attribute("val")?.Value;

    /// <summary>
    /// The text's drawing with its restyled drawings inside: each paragraph group (data-p; data-c with
    /// <paramref name="units"/>) gets its own drawing as data-v="" and every other one's as data-v="key", hidden; the
    /// other drawings' paints and filters (defs) come along. Markup that is no well-formed XML stays as it is.
    /// </summary>
    public static string Merge(string original, IReadOnlyList<(string Key, string Markup)> variants, bool units)
    {
        if (variants.Count == 0)
            return original;
        XElement root;
        try
        {
            root = XElement.Parse($"<r>{original}</r>", LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            return original;
        }
        var unit = units ? "data-c" : "data-p";
        var groups = root.Descendants("g").Where(g => g.Attribute(unit) != null).ToList();
        if (groups.Count == 0)
            return original;
        foreach (var group in groups)
        {
            var nodes = group.Nodes().ToList();
            group.RemoveNodes();
            group.Add(new XElement("g", new XAttribute("data-v", ""), nodes));
        }
        foreach (var (key, markup) in variants)
        {
            XElement other;
            try
            {
                other = XElement.Parse($"<r>{markup}</r>", LoadOptions.PreserveWhitespace);
            }
            catch (XmlException)
            {
                continue;
            }
            var drawn = other.Descendants("g").Where(g => g.Attribute(unit) != null)
                .GroupBy(g => g.Attribute(unit)!.Value).ToDictionary(g => g.Key, g => g.First());
            foreach (var group in groups)
            {
                if (!drawn.TryGetValue(group.Attribute(unit)!.Value, out var match))
                    continue;
                // One selectable text is enough: the restyled drawings leave theirs out.
                var nodes = match.Nodes().Where(n => n is not XElement { Name.LocalName: "text" } e || e.Attribute("class")?.Value != "sel").ToList();
                var part = new XElement("g", new XAttribute("data-v", key), new XAttribute("visibility", "hidden"), nodes);
                // A letter in another face takes the place of the letter it stands for (the player lines them up); one
                // in another size keeps the place the new layout gives it.
                if (units && key.Split('|') is [var weight, var style, _, var family, ""] && (weight + style + family).Length > 0)
                    part.SetAttributeValue("data-align", "");
                group.Add(part);
            }
            foreach (var defs in other.Elements("defs").ToList())
                root.AddFirst(defs);
        }
        return string.Concat(root.Nodes().Select(n => n.ToString(SaveOptions.DisableFormatting)));
    }
}
