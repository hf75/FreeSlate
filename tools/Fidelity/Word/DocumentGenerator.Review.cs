namespace Fidelity.Word;

/// <summary>Generated documents of review features: tracked changes and comments.</summary>
public static partial class DocumentGenerator
{
    /// <summary>
    /// Tracked changes Word records while it edits: inserted and deleted text, a formatting change, a deleted paragraph
    /// mark, a deleted and an inserted table row, moved text; and a comment. Printed without markup, as the fidelity
    /// tool exports.
    /// </summary>
    static void Revisions(Writer w)
    {
        w.Paragraph("Tracked changes", StyleHeading1);
        w.Paragraph("The quick brown fox jumps over the lazy dog. " + Prose.Decisions);
        w.Paragraph("This paragraph will be joined with the next one by deleting its mark.");
        w.Paragraph("The next paragraph continues here after the joined mark.");
        w.Paragraph(Prose.Rules);
        var table = w.Paragraph("");
        var t = w.Document.Tables.Add(table.Range, 3, 2);
        t.Borders.Enable = 1;
        for (var r = 1; r <= 3; r++)
            for (var c = 1; c <= 2; c++)
                t.Cell(r, c).Range.Text = $"Row {r} cell {c}";
        w.Paragraph("A sentence to move. The rest of the paragraph stays where it is.");
        w.Paragraph("The moved sentence lands at the end of this paragraph.");
        w.Paragraph("A comment marks this paragraph.");

        var document = w.Document;
        // The text a range shows, found anew before every edit (stored ranges move with the edits).
        dynamic Find(string text)
        {
            var range = document.Content;
            range.Find.ClearFormatting();
            if (!(bool)range.Find.Execute(FindText: text, MatchCase: true))
                throw new InvalidOperationException($"\"{text}\" is not in the document.");
            return range;
        }
        document.TrackRevisions = true;
        Find("quick ").Delete();
        Find("brown").InsertBefore("slow and steady ");
        Find("over the lazy dog").Font.Bold = -1;
        // The paragraph mark after "its mark.": deleting it joins the two paragraphs.
        var joined = Find("by deleting its mark.");
        document.Range((int)joined.End, (int)joined.End + 1).Delete();
        t.Rows.Item(2).Delete();
        var added = t.Rows.Add();
        added.Cells.Item(1).Range.Text = "Added row";
        Find("A sentence to move. ").Cut();
        var target = Find("at the end of this paragraph.");
        document.Range((int)target.End, (int)target.End).Paste();
        document.TrackRevisions = false;
        document.Comments.Add(Find("A comment marks"), "A comment on the paragraph.");
    }
}
