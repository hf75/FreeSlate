using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>
/// Tracked changes accepted and rejected in the XML they stand in, as Word's Accept and Reject leave a document (measured in Word 365
/// by COM on probes of every kind, scratchpad gaps/A/revs.ps1: Revisions.AcceptAll, RejectAll and each revision alone):
/// - an insertion (w:ins, w:moveTo around runs) accepted is its runs, rejected gone with them (and with the changes within it); a
///   deletion (w:del, w:moveFrom) accepted is gone, rejected is its runs again, their w:delText w:t and w:delInstrText w:instrText;
/// - a paragraph's mark inserted and rejected, or deleted and accepted, joins the paragraph with the next one, which keeps its own
///   properties (measured: "Joined " of Heading 1 with its mark deleted and "here." aligned right accepted are "Joined here." aligned
///   right; an inserted paragraph of Heading 2 rejected leaves the next as it was); the last of its body or cell stays;
/// - a formatting change (w:rPrChange, w:pPrChange, w:sectPrChange, w:tblPrChange, w:tblGridChange, w:trPrChange, w:tcPrChange,
///   w:tblPrExChange) accepted is gone, rejected gives back the properties it kept (a mark's own changes, a paragraph's mark and section
///   properties, a section's headers and footers, a row's insertion or deletion staying);
/// - a row inserted (w:trPr/w:ins) and rejected, or deleted and accepted, is gone, the table with its last row; a cell likewise
///   (w:cellIns, w:cellDel);
/// - a move's range marks (w:moveFromRangeStart …) go either way.
/// </summary>
static partial class TextEdits
{
    /// <summary>The elements of tracked changes.</summary>
    static readonly HashSet<string> ChangeNames =
    [
        "ins", "del", "moveFrom", "moveTo", "rPrChange", "pPrChange", "sectPrChange", "tblPrChange", "tblGridChange", "trPrChange", "tcPrChange",
        "tblPrExChange", "numberingChange", "cellIns", "cellDel", "cellMerge",
    ];

    /// <summary>The marks of ranges of tracked changes: a move's, custom XML's inserted or deleted.</summary>
    static bool IsRangeMark(XElement e) => e.Name.LocalName is "moveFromRangeStart" or "moveFromRangeEnd" or "moveToRangeStart" or "moveToRangeEnd"
        or "customXmlInsRangeStart" or "customXmlInsRangeEnd" or "customXmlDelRangeStart" or "customXmlDelRangeEnd"
        or "customXmlMoveFromRangeStart" or "customXmlMoveFromRangeEnd" or "customXmlMoveToRangeStart" or "customXmlMoveToRangeEnd";

    /// <summary>
    /// The elements of the tracked changes under an element, in document order, of an author where one is given (with the range marks of
    /// that author's moves, by the ids of their starts).
    /// </summary>
    public static List<XElement> Changes(XElement root, string? author)
    {
        var w = root.Name.Namespace;
        var changes = new List<XElement>();
        var ranges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in root.Descendants())
        {
            if (e.Name.Namespace != w)
                continue;
            if (ChangeNames.Contains(e.Name.LocalName))
            {
                if (author == null || (string?)e.Attribute(w + "author") == author)
                    changes.Add(e);
            }
            else if (IsRangeMark(e))
            {
                var id = (string?)e.Attribute(w + "id") ?? "";
                if (author == null || (string?)e.Attribute(w + "author") == author && ranges.Add(e.Name.LocalName.Replace("Start", "", StringComparison.Ordinal) + id)
                    || ranges.Contains(e.Name.LocalName.Replace("End", "", StringComparison.Ordinal) + id))
                    changes.Add(e);
            }
        }
        return changes;
    }

    /// <summary>
    /// Changes accepted or rejected in a stretch (see Settle): the stretch's children they touched written anew, those gone written as
    /// nothing. Returns the stretch's new text.
    /// </summary>
    public static string Settle(Stretch stretch, List<XElement> changes, bool accept)
    {
        var holder = stretch.Xml.Root!.Elements().First();
        var children = holder.Elements().ToList();
        var touched = Settle(changes, accept);
        var written = new List<(XElement, IEnumerable<XElement>)>();
        foreach (var child in children)
        {
            if (child.Parent != holder)
                written.Add((child, []));
            else if (touched.Any(t => t == child || t.Ancestors().Contains(child)))
                written.Add((child, [child]));
        }
        return stretch.Written(written);
    }

    /// <summary>
    /// Changes accepted or rejected where they stand (see the class's summary): those within content an earlier one took away pass.
    /// Returns the elements it changed or put content into, which hold what is to be written anew.
    /// </summary>
    public static List<XElement> Settle(List<XElement> changes, bool accept)
    {
        var touched = new List<XElement>();
        var joins = new List<XElement>();
        var rows = new List<XElement>();
        var cells = new List<XElement>();
        foreach (var change in changes)
        {
            // Gone with what was around it.
            if (change.Parent is not { } parent || change.Document == null)
                continue;
            var w = change.Name.Namespace;
            touched.Add(parent);
            if (IsRangeMark(change))
            {
                change.Remove();
                continue;
            }
            var name = change.Name.LocalName;
            var inserting = name is "ins" or "moveTo";
            if (name is "ins" or "del" or "moveFrom" or "moveTo")
            {
                // Settled, it is there (an insertion accepted, a deletion rejected) or not.
                var stays = inserting == accept;
                if (parent.Name == w + "rPr" && parent.Parent is { } holder && holder.Name == w + "pPr")
                {
                    // A paragraph's mark: gone, it joins the paragraph with the next.
                    change.Remove();
                    if (!stays && holder.Parent is { } paragraph)
                        joins.Add(paragraph);
                }
                else if (parent.Name == w + "rPr" && parent.Parent is { } run)
                {
                    // A math run's own mark.
                    change.Remove();
                    if (!stays)
                        run.Remove();
                }
                else if (parent.Name == w + "trPr")
                {
                    change.Remove();
                    if (!stays && parent.Parent is { } row)
                        rows.Add(row);
                }
                else if (stays)
                {
                    if (!inserting)
                    {
                        foreach (var text in change.Descendants(w + "delText").ToList())
                            text.Name = w + "t";
                        foreach (var instruction in change.Descendants(w + "delInstrText").ToList())
                            instruction.Name = w + "instrText";
                    }
                    // Taken out first, so that they go in as themselves, not as copies (the changes within them stay the ones listed).
                    var nodes = change.Nodes().ToList();
                    foreach (var node in nodes)
                        node.Remove();
                    change.ReplaceWith(nodes);
                }
                else
                    change.Remove();
                continue;
            }
            switch (name)
            {
                case "cellIns" or "cellDel":
                    change.Remove();
                    if ((name == "cellIns") != accept && parent.Parent is { } cell)
                        cells.Add(cell);
                    continue;
                case "cellMerge" or "numberingChange":
                    change.Remove();
                    continue;
            }
            // A formatting change: rejected, the properties it kept in place of those now (what is not formatting staying).
            if (!accept)
            {
                var kept = change.Elements().FirstOrDefault();
                static bool Stays(string change, XElement e) => change switch
                {
                    "rPrChange" => e.Name.LocalName is "ins" or "del" or "moveFrom" or "moveTo",
                    "pPrChange" => e.Name.LocalName is "rPr" or "sectPr",
                    "sectPrChange" => e.Name.LocalName is "headerReference" or "footerReference",
                    "trPrChange" => e.Name.LocalName is "ins" or "del",
                    _ => false,
                };
                var staying = parent.Elements().Where(e => e != change && Stays(name, e)).ToList();
                var before = name == "tblGridChange" ? parent.Elements(w + "gridCol").ToList() : parent.Elements().Where(e => e != change).ToList();
                foreach (var e in before)
                    e.Remove();
                var given = kept?.Elements().Select(e => new XElement(e)).ToList() ?? [];
                // A mark's own changes first, a section's headers and footers first; a paragraph's mark and section, a row's changes after.
                if (name is "rPrChange" or "sectPrChange")
                    parent.AddFirst([.. staying, .. given]);
                else
                    change.AddBeforeSelf([.. given, .. staying]);
            }
            change.Remove();
        }
        // Paragraphs joined with the next one, the last first: what follows a join may join on.
        foreach (var paragraph in joins.Distinct().Reverse())
        {
            if (paragraph.Parent == null)
                continue;
            var w = paragraph.Name.Namespace;
            var next = paragraph.ElementsAfterSelf().FirstOrDefault(e => !IsMark(e));
            if (next == null || next.Name != w + "p")
            {
                // Before a table one without content goes (measured: a paragraph moved before a table, rejected, leaves none there).
                if (next?.Name == w + "tbl" && !paragraph.Elements().Where(e => e.Name.LocalName != "pPr").SelectMany(e => e.DescendantsAndSelf())
                        .Any(e => e.Name.LocalName is "t" or "delText" or "tab" or "ptab" or "br" or "cr" or "drawing" or "pict" or "object" or "fldChar" or "sym"
                            or "footnoteReference" or "endnoteReference" or "noBreakHyphen" or "softHyphen" or "oMath" or "oMathPara"))
                {
                    touched.Add(paragraph.Parent!);
                    paragraph.Remove();
                }
                continue;
            }
            var content = paragraph.Nodes().Where(n => n is not XElement { Name.LocalName: "pPr" }).ToList();
            foreach (var node in content)
                node.Remove();
            if (next.Element(w + "pPr") is { } properties)
                properties.AddAfterSelf(content);
            else
                next.AddFirst(content);
            paragraph.Remove();
            touched.Add(next);
        }
        foreach (var cell in cells.Where(c => c.Parent != null))
        {
            touched.Add(cell.Parent!);
            cell.Remove();
        }
        foreach (var row in rows.Where(r => r.Parent != null))
        {
            var table = row.Ancestors(row.Name.Namespace + "tbl").FirstOrDefault();
            touched.Add(row.Parent!);
            row.Remove();
            // A table without rows is gone.
            if (table != null && !table.Descendants(row.Name.Namespace + "tr").Any())
            {
                if (table.Parent is { } around)
                    touched.Add(around);
                table.Remove();
            }
        }
        return touched;
    }
}
