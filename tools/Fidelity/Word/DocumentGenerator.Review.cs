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

public static partial class DocumentGenerator
{
    /// <summary>
    /// Content controls: rich text with its placeholder, plain text with content, check boxes (checked and not), a
    /// drop-down list, a combo box, a date picker, a picture control and a repeating section.
    /// </summary>
    static void ContentControls(Writer w)
    {
        // WdContentControlType
        const int RichText = 0, Text = 1, Picture = 2, ComboBox = 3, DropDown = 4, Date = 6, CheckBox = 8, Repeating = 9;
        var document = w.Document;
        w.Paragraph("Content controls", StyleHeading1);
        dynamic Control(int type, string label)
        {
            var paragraph = w.Paragraph(label + " ");
            var end = paragraph.Range;
            end.MoveEnd(1, -1);
            end.Collapse(0);
            return document.ContentControls.Add(type, end);
        }
        Control(RichText, "Empty rich text:");
        Control(Text, "Plain text:").Range.Text = "Typed into a plain text control";
        Control(CheckBox, "Unchecked box:");
        Control(CheckBox, "Checked box:").Checked = true;
        var list = Control(DropDown, "Drop-down list:");
        list.DropdownListEntries.Add("First choice", "1");
        list.DropdownListEntries.Add("Second choice", "2");
        list.DropdownListEntries.Item(list.DropdownListEntries.Count).Select();
        var combo = Control(ComboBox, "Combo box:");
        combo.DropdownListEntries.Add("Apple", "a");
        var date = Control(Date, "Date:");
        date.DateDisplayFormat = "dd.MM.yyyy";
        date.Range.Text = "11.10.2026";
        Control(Picture, "Picture:");
        var paragraph = w.Paragraph("A repeating section follows.");
        var section = w.Paragraph("Repeated item text.");
        document.ContentControls.Add(Repeating, section.Range);
    }
}

public static partial class DocumentGenerator
{
    /// <summary>Legacy form fields: check boxes (checked, unchecked, of a fixed size), text inputs and a drop-down.</summary>
    static void FormFields(Writer w)
    {
        // WdFieldType
        const int TextInput = 70, CheckBox = 71, DropDown = 83;
        var document = w.Document;
        w.Paragraph("Form fields", StyleHeading1);
        dynamic Field(int type, string label)
        {
            var paragraph = w.Paragraph(label + " ");
            var end = paragraph.Range;
            end.MoveEnd(1, -1);
            end.Collapse(0);
            return document.FormFields.Add(end, type);
        }
        Field(CheckBox, "Unchecked box:");
        Field(CheckBox, "Checked box:").CheckBox.Value = true;
        var sized = Field(CheckBox, "Box of 16 pt:");
        sized.CheckBox.AutoSize = false;
        sized.CheckBox.Size = 16f;
        sized.CheckBox.Value = true;
        Field(TextInput, "Empty text input:");
        Field(TextInput, "Filled text input:").Result = "Entered text";
        var list = Field(DropDown, "Drop-down:");
        list.DropDown.ListEntries.Add("First");
        list.DropDown.ListEntries.Add("Second");
        list.DropDown.Value = 2;
    }
}
