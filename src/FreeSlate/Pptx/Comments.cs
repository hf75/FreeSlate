using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace FreeSlate.Pptx;

/// <summary>A comment or a reply with its author, time, text and, for modern comments, status, reactions and task.</summary>
public sealed record SlideComment(
    string Author, string Initials, int Color, string? Created, bool Utc, string Html, string Status,
    (double X, double Y)? Position, string? Label, List<SlideComment> Replies, List<(string Emoji, List<string> Names)> Reactions,
    CommentTask? Task);

/// <summary>A comment that is a task: its title, assignees, dates and progress (percent).</summary>
public sealed record CommentTask(string? Title, List<string> Assignees, string? Start, string? Due, double Complete);

/// <summary>
/// The review comments of a slide (catalog: review.comments, review.modernComments). Classic comments (p:cmLst,
/// authors in commentAuthors.xml, replies through p15:threadingInfo) sit at p:pos in 1/576 inch from the slide's
/// top left corner; modern comments (p188:cmLst, authors in authors.xml) sit at p188:pos in EMU from the top left
/// corner of what they are anchored to (the slide, a shape or a text range in it) and carry replies, a status,
/// reactions (p223) and task assignments (p228). PowerPoint never shows comments in the slide show; the document
/// shows them on request: markers on the slide and the threads below it.
/// </summary>
public static class Comments
{
    static readonly XNamespace P15 = "http://schemas.microsoft.com/office/powerpoint/2012/main";
    static readonly XNamespace P188 = "http://schemas.microsoft.com/office/powerpoint/2018/8/main";
    static readonly XNamespace P223 = "http://schemas.microsoft.com/office/powerpoint/2022/03/main";
    static readonly XNamespace P228 = "http://schemas.microsoft.com/office/powerpoint/2022/08/main";
    static readonly XNamespace Ac = "http://schemas.microsoft.com/office/drawing/2013/main/command";
    static readonly XNamespace A16 = "http://schemas.microsoft.com/office/drawing/2014/main";

    /// <summary>The comment threads of a slide in document order, classic ones first.</summary>
    public static List<SlideComment> Read(PresentationContext context, string slidePart)
    {
        var result = new List<SlideComment>();
        var package = context.Package;
        foreach (var relationship in package.Relationships(slidePart))
        {
            if (relationship.External || !package.Exists(relationship.Target))
                continue;
            if (relationship.Type == "http://schemas.openxmlformats.org/officeDocument/2006/relationships/comments"
                || relationship.Type == "http://purl.oclc.org/ooxml/officeDocument/relationships/comments")
                result.AddRange(Classic(context, context.Load(relationship.Target)));
            else if (relationship.Type == "http://schemas.microsoft.com/office/2018/10/relationships/comments")
                result.AddRange(Modern(context, slidePart, relationship.Target));
        }
        return result;
    }

    /// <summary>The authors of a presentation part by id, with their color index in order of appearance.</summary>
    static Dictionary<string, (string Name, string Initials, int Color)> Authors(PresentationContext context, string type, XName author)
    {
        var authors = new Dictionary<string, (string, string, int)>(StringComparer.OrdinalIgnoreCase);
        var part = context.Package.FirstRelationship(context.PresentationPart, type)?.Target;
        if (part == null || !context.Package.Exists(part))
            return authors;
        var index = 0;
        foreach (var a in context.Load(part).Root?.Elements(author) ?? [])
        {
            var name = a.Str("name") ?? "";
            authors[a.Str("id") ?? ""] = (name, a.Str("initials") ?? Initials(name), a.Int("clrIdx") ?? index);
            index++;
        }
        return authors;
    }

    static string Initials(string name) => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpperInvariant(w[0])));

    static List<SlideComment> Classic(PresentationContext context, XDocument part)
    {
        var authors = Authors(context, "commentAuthors", Ns.P + "cmAuthor");
        var comments = new List<(XElement Element, SlideComment Comment, (string, int)? Parent)>();
        foreach (var cm in part.Root?.Elements(Ns.P + "cm") ?? [])
        {
            var authorId = cm.Str("authorId") ?? "";
            var (name, initials, color) = authors.TryGetValue(authorId, out var a) ? a : ("", "", 0);
            var pos = cm.Element(Ns.P + "pos");
            // 576 units per inch (PowerPoint's master units, [MS-OI29500] 2.1.1137).
            (double, double)? position = pos != null ? ((pos.Long("x") ?? 0) / 6.0, (pos.Long("y") ?? 0) / 6.0) : null;
            var text = cm.Element(Ns.P + "text")?.Value ?? "";
            var html = string.Join("", text.Replace("\r\n", "\n").Split('\n').Select(line => $"<p>{WebUtility.HtmlEncode(line)}</p>"));
            var parent = cm.Descendants(P15 + "parentCm").FirstOrDefault();
            var idx = cm.Str("idx");
            var comment = new SlideComment(name, initials, color, cm.Str("dt"), false, html, "active", position,
                initials + idx, [], [], null);
            comments.Add((cm, comment, parent != null ? (parent.Str("authorId") ?? "", parent.Int("idx") ?? 0) : null));
        }
        // Replies (p15:parentCm) join the thread of their parent; a reply to a missing parent stands alone.
        var threads = new List<SlideComment>();
        foreach (var (element, comment, parent) in comments)
        {
            var root = parent is { } p
                ? comments.FirstOrDefault(c => c.Element.Str("authorId") == p.Item1 && c.Element.Int("idx") == p.Item2 && c.Parent == null).Comment
                : null;
            if (root != null)
                root.Replies.Add(comment with { Position = null, Label = null });
            else
                threads.Add(comment);
        }
        return threads;
    }

    static List<SlideComment> Modern(PresentationContext context, string slidePart, string partName)
    {
        var part = context.Load(partName);
        var authors = Authors(context, "authors", P188 + "author");
        (string Name, string Initials, int Color) Author(string? id) => id != null && authors.TryGetValue(id, out var a) ? a : ("", "", 0);
        var threads = new List<SlideComment>();
        var number = 0;
        foreach (var cm in part.Root?.Elements(P188 + "cm") ?? [])
        {
            number++;
            var (name, initials, color) = Author(cm.Str("authorId"));
            var anchor = Anchor(context, slidePart, cm);
            var pos = cm.Element(P188 + "pos");
            (double, double)? position = anchor is { } origin
                ? (origin.X + Units.ToPx(pos.Long("x") ?? 0), origin.Y + Units.ToPx(pos.Long("y") ?? 0))
                : null;
            var replies = cm.Element(P188 + "replyLst")?.Elements(P188 + "reply").Select(reply =>
            {
                var (n, i, c) = Author(reply.Str("authorId"));
                return new SlideComment(n, i, c, reply.Str("created"), true, DeckInfo.ParagraphsHtml(reply.Element(P188 + "txBody"), partName, context.Package),
                    reply.Str("status") ?? "active", null, null, [], Reactions(reply, authors), null);
            }).ToList() ?? [];
            threads.Add(new SlideComment(name, initials, color, cm.Str("created"), true,
                DeckInfo.ParagraphsHtml(cm.Element(P188 + "txBody"), partName, context.Package), cm.Str("status") ?? "active", position,
                number.ToString(CultureInfo.InvariantCulture), replies, Reactions(cm, authors), Task(cm, authors)));
        }
        return threads;
    }

    /// <summary>The top left corner (px) of what a modern comment is anchored to: the slide or a shape on it.</summary>
    static (double X, double Y)? Anchor(PresentationContext context, string slidePart, XElement cm)
    {
        var moniker = cm.Element(Ac + "deMkLst") ?? cm.Element(Ac + "txMkLst");
        if (moniker == null)
            return cm.Element(P188 + "unknownAnchor") != null ? null : (0, 0);
        // The innermost drawing element moniker (ac:spMk, ac:picMk, ac:grpSpMk …) names the shape by id and creationId.
        var element = moniker.Elements().LastOrDefault(e => e.Name.Namespace == Ac && e.Name.LocalName.EndsWith("Mk") && e.Attribute("id") != null);
        if (element == null)
            return (0, 0);
        var tree = context.Load(slidePart).Root?.Element(Ns.P + "cSld")?.Element(Ns.P + "spTree");
        return Find(tree, element.Str("id"), element.Str("creationId"), (0, 0, 1, 1)) ?? (0, 0);
    }

    /// <summary>The slide position (px) of a shape's top left corner, found by its id or creation id inside nested groups.</summary>
    static (double X, double Y)? Find(XElement? tree, string? id, string? creationId, (double Ox, double Oy, double Sx, double Sy) map)
    {
        foreach (var shape in tree?.Elements() ?? [])
        {
            var nv = shape.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("nv"))?.Element(Ns.P + "cNvPr");
            var xfrm = shape.Name.LocalName == "graphicFrame" ? shape.Element(Ns.P + "xfrm")
                : shape.Elements().FirstOrDefault(e => e.Name.LocalName is "spPr" or "grpSpPr")?.Element(Ns.A + "xfrm");
            var off = xfrm?.Element(Ns.A + "off");
            var (x, y) = (map.Ox + (off.Long("x") ?? 0) * map.Sx, map.Oy + (off.Long("y") ?? 0) * map.Sy);
            var created = nv?.Descendants(A16 + "creationId").FirstOrDefault().Str("id");
            if (nv != null && ((creationId != null && string.Equals(created, creationId, StringComparison.OrdinalIgnoreCase)) || (creationId == null || created == null) && nv.Str("id") == id))
                return (Units.ToPx((long)x), Units.ToPx((long)y));
            if (shape.Name.LocalName == "grpSp" && xfrm != null)
            {
                var (ext, chOff, chExt) = (xfrm.Element(Ns.A + "ext"), xfrm.Element(Ns.A + "chOff"), xfrm.Element(Ns.A + "chExt"));
                var sx = (chExt.Long("cx") ?? 0) is > 0 and var cw ? (ext.Long("cx") ?? 0) / (double)cw : 1;
                var sy = (chExt.Long("cy") ?? 0) is > 0 and var ch ? (ext.Long("cy") ?? 0) / (double)ch : 1;
                var inner = (x - (chOff.Long("x") ?? 0) * sx * map.Sx, y - (chOff.Long("y") ?? 0) * sy * map.Sy, sx * map.Sx, sy * map.Sy);
                if (Find(shape, id, creationId, inner) is { } found)
                    return found;
            }
        }
        return null;
    }

    static List<(string, List<string>)> Reactions(XElement comment, Dictionary<string, (string Name, string Initials, int Color)> authors) =>
        comment.Descendants(P223 + "rxn").Select(r => (r.Str("type") ?? "",
            r.Elements(P223 + "instance").Select(i => i.Str("authorId") is { } id && authors.TryGetValue(id, out var a) ? a.Name : "").ToList()))
            .Where(r => r.Item1.Length > 0 && r.Item2.Count > 0).ToList();

    /// <summary>
    /// The task of a comment: its attributes (assignedTo, startDate, dueDate, complete, title) or else the replayed
    /// p228 history (assignments, title, dates, progress; undone events skipped).
    /// </summary>
    static CommentTask? Task(XElement cm, Dictionary<string, (string Name, string Initials, int Color)> authors)
    {
        string Name(string id) => authors.TryGetValue(id, out var a) ? a.Name : "";
        var assignees = (cm.Str("assignedTo") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var (title, start, due, complete) = (cm.Str("title") is { Length: > 0 } t ? t : null, cm.Str("startDate"), cm.Str("dueDate"), Percent(cm.Str("complete")));
        var history = cm.Descendants(P228 + "history").FirstOrDefault();
        if (history == null && assignees.Count == 0)
            return null;
        if (history != null && cm.Attribute("assignedTo") == null && cm.Attribute("title") == null && cm.Attribute("dueDate") == null)
        {
            var undone = history.Elements(P228 + "event").SelectMany(e => e.Elements(P228 + "undo")).Select(u => u.Str("id")).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var e in history.Elements(P228 + "event"))
            {
                if (undone.Contains(e.Str("id")))
                    continue;
                if (e.Element(P228 + "asgn") is { } assign && assign.Str("authorId") is { } who && !assignees.Contains(who))
                    assignees.Add(who);
                if (e.Element(P228 + "unasgnAll") != null) assignees.Clear();
                if (e.Element(P228 + "title") is { } titled) title = titled.Str("val");
                if (e.Element(P228 + "date") is { } date) (start, due) = (date.Str("stDt") ?? start, date.Str("endDt") ?? due);
                if (e.Element(P228 + "pcntCmplt") is { } progress) complete = Percent(progress.Str("val"));
            }
        }
        return new CommentTask(title, assignees.Select(Name).Where(n => n.Length > 0).ToList(), start, due, complete);
    }

    /// <summary>A percentage written as "50%" or in thousandths of a percent ("50000").</summary>
    static double Percent(string? value) =>
        value == null ? 0
        : value.EndsWith('%') ? double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : 0
        : double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v / 1000 : 0;

    /// <summary>Author colors by index (markers and avatars).</summary>
    static readonly string[] Palette = ["#2b6cb0", "#c0392b", "#2f855a", "#805ad5", "#c05621", "#00838f", "#b83280", "#5a6270"];

    static string ColorOf(int index) => Palette[((index % Palette.Length) + Palette.Length) % Palette.Length];

    /// <summary>Markers on the slide for the threads with a position (hidden until comments are shown).</summary>
    public static string Markers(IReadOnlyList<SlideComment> comments, int slide)
    {
        var html = new StringBuilder();
        for (var i = 0; i < comments.Count; i++)
        {
            if (comments[i].Position is not { } p)
                continue;
            var c = comments[i];
            html.Append($"<a class=\"cm-marker\" href=\"#cm-{slide}-{i + 1}\" style=\"left:{Units.Css(p.X)}px;top:{Units.Css(p.Y)}px;background:{ColorOf(c.Color)}\"" +
                        $" title=\"{WebUtility.HtmlEncode(c.Author)}\">{WebUtility.HtmlEncode(c.Label ?? c.Initials)}</a>");
        }
        return html.ToString();
    }

    /// <summary>The threads of a slide as a panel below it (hidden until comments are shown), or "" without comments.</summary>
    public static string Panel(IReadOnlyList<SlideComment> comments, int slide, double width)
    {
        if (comments.Count == 0)
            return "";
        var html = new StringBuilder($"<aside class=\"comments\" data-slide=\"{slide}\" style=\"width:{Units.Css(width)}px\">");
        for (var i = 0; i < comments.Count; i++)
        {
            var c = comments[i];
            html.Append($"<article class=\"cm\" id=\"cm-{slide}-{i + 1}\" data-status=\"{c.Status}\">");
            Entry(html, c);
            foreach (var reply in c.Replies)
            {
                html.Append($"<article class=\"cm-reply\" data-status=\"{reply.Status}\">");
                Entry(html, reply);
                html.Append("</article>");
            }
            html.Append("</article>");
        }
        return html.Append("</aside>").ToString();
    }

    static void Entry(StringBuilder html, SlideComment c)
    {
        var initials = WebUtility.HtmlEncode(c.Initials.Length > 0 ? c.Initials : Initials(c.Author));
        html.Append($"<header><span class=\"cm-avatar\" style=\"background:{ColorOf(c.Color)}\">{initials}</span><b>{WebUtility.HtmlEncode(c.Author)}</b>");
        var styles = DateTimeStyles.AllowWhiteSpaces | (c.Utc ? DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal : DateTimeStyles.None);
        if (c.Created is { } created && DateTime.TryParse(created, CultureInfo.InvariantCulture, styles, out var time))
        {
            var iso = time.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + (c.Utc ? "Z" : "");
            html.Append($"<time datetime=\"{iso}\">{time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}{(c.Utc ? " UTC" : "")}</time>");
        }
        if (c.Status is "resolved" or "closed")
            html.Append($"<span class=\"cm-status\">{(c.Status == "resolved" ? "Resolved" : "Closed")}</span>");
        html.Append("</header>");
        if (c.Task is { } task)
        {
            var parts = new List<string>();
            if (task.Title != null) parts.Add($"<b>{WebUtility.HtmlEncode(task.Title)}</b>");
            if (task.Assignees.Count > 0) parts.Add("Assigned to " + WebUtility.HtmlEncode(string.Join(", ", task.Assignees)));
            if (Day(task.Start) is { } start) parts.Add("Start " + start);
            if (Day(task.Due) is { } due) parts.Add("Due " + due);
            parts.Add($"{task.Complete.ToString("0.#", CultureInfo.InvariantCulture)} % complete");
            html.Append($"<div class=\"cm-task\">{string.Join(" · ", parts)}</div>");
        }
        html.Append($"<div class=\"cm-text\">{c.Html}</div>");
        if (c.Reactions.Count > 0)
        {
            html.Append("<div class=\"cm-reactions\">");
            foreach (var (emoji, names) in c.Reactions)
                html.Append($"<span title=\"{WebUtility.HtmlEncode(string.Join(", ", names))}\">{WebUtility.HtmlEncode(emoji)} {names.Count}</span>");
            html.Append("</div>");
        }
    }

    static string? Day(string? value) =>
        value != null && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var d) ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
}
