using System.Globalization;
using System.Net;
using System.Xml.Linq;
using FreeSlate.Text;

namespace FreeSlate.Pptx;

/// <summary>
/// Hyperlinks and action settings (catalog: interactivity.hyperlinks): a:hlinkClick on shapes and text runs. URLs
/// and files become links; jumps to slides link to the slide's anchor; slide show actions (next, previous, first,
/// last, last viewed, end show, custom show, slide) are carried in data-action for the player.
/// </summary>
public static class Links
{
    const string ShowJump = "ppaction://hlinkshowjump?jump=";

    /// <summary>The link of an a:hlinkClick (or a:hlinkHover) element in a part, or null for unsupported actions.</summary>
    public static TextLink? Resolve(XElement? hlink, string part, PresentationContext context)
    {
        if (hlink == null)
            return null;
        var action = hlink.Str("action") ?? "";
        var tooltip = hlink.Str("tooltip");
        var rid = hlink.Attribute(Ns.R + "id")?.Value;
        var rel = string.IsNullOrEmpty(rid) ? null : context.Package.Relationship(part, rid);

        if (action.StartsWith(ShowJump, StringComparison.Ordinal))
        {
            return action[ShowJump.Length..] switch
            {
                "firstslide" => new TextLink("#slide-1", "first", tooltip),
                "lastslide" => new TextLink($"#slide-{context.SlideParts.Count}", "last", tooltip),
                "nextslide" => new TextLink(null, "next", tooltip),
                "previousslide" => new TextLink(null, "previous", tooltip),
                "lastslideviewed" => new TextLink(null, "last-viewed", tooltip),
                "endshow" => new TextLink(null, "end", tooltip),
                _ => null,
            };
        }
        if (action.StartsWith("ppaction://customshow", StringComparison.Ordinal))
        {
            var query = action.Contains('?') ? action[(action.IndexOf('?') + 1)..] : "";
            var id = query.Split('&').Select(p => p.Split('=')).FirstOrDefault(p => p.Length == 2 && p[0] == "id")?[1];
            return id != null ? new TextLink(null, "show:" + id, tooltip) : null;
        }
        if (rel == null)
            return null;
        if (!rel.External && context.SlideParts.IndexOf(rel.Target) is var index and >= 0)
            return new TextLink($"#slide-{(index + 1).ToString(CultureInfo.InvariantCulture)}", $"slide:{index + 1}", tooltip);
        if (rel.External && action is "" or "ppaction://hlinkfile")
            return new TextLink(rel.Target, null, tooltip);
        return null;   // programs, macros, OLE verbs and media actions are not run
    }

    /// <summary>The opening tag of an SVG link.</summary>
    public static string Open(TextLink link)
    {
        var tag = "<a";
        if (link.Href != null)
        {
            tag += $" href=\"{WebUtility.HtmlEncode(link.Href)}\"";
            if (link.Action == null) tag += " target=\"_blank\" rel=\"noopener\"";
        }
        if (link.Action != null) tag += $" data-action=\"{WebUtility.HtmlEncode(link.Action)}\"";
        if (link.Zoom != null) tag += $" data-zoom=\"{WebUtility.HtmlEncode(link.Zoom)}\"";
        if (link.Tooltip is { Length: > 0 } tooltip) tag += $"><title>{WebUtility.HtmlEncode(tooltip)}</title";
        return tag + ">";
    }
}
