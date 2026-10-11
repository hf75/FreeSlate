namespace Fidelity.Word;

/// <summary>Generated documents of the structural features: lists, tabs, headers and footers, tables, breaks.</summary>
public static partial class DocumentGenerator
{
    /// <summary>Bulleted, numbered and multilevel lists, restarted numbering and list levels.</summary>
    static void Lists(Writer w)
    {
        // WdListGalleryType, WdDefaultListBehavior
        const int BulletGallery = 1, NumberGallery = 2, OutlineGallery = 3, Word10ListBehavior = 2;
        var app = w.Document.Application;
        w.Paragraph("Bullets", StyleHeading1);
        var bullets = Group(w, ["First bullet item.", "Second bullet item with more text, so that it wraps onto a second line and shows the hanging indent of the list.", "Third bullet item."]);
        w.Document.Range(bullets[0], bullets[^1]).ListFormat.ApplyListTemplateWithLevel(app.ListGalleries[BulletGallery].ListTemplates[1], false, 1, Word10ListBehavior);

        w.Paragraph("Numbers", StyleHeading1);
        var numbers = Group(w, ["First step.", "Second step, which takes a little longer to describe and therefore wraps onto the next line.", "Third step.", "Fourth step."]);
        w.Document.Range(numbers[0], numbers[^1]).ListFormat.ApplyListTemplateWithLevel(app.ListGalleries[NumberGallery].ListTemplates[1], false, 1, Word10ListBehavior);

        w.Paragraph("Multilevel", StyleHeading1);
        (string Text, int Level)[] outline = [("Chapter", 1), ("Section", 2), ("Subsection", 3), ("Another subsection", 3), ("Another section", 2), ("Next chapter", 1), ("Its section", 2)];
        var levels = Group(w, outline.Select(o => o.Text).ToArray());
        w.Document.Range(levels[0], levels[^1]).ListFormat.ApplyListTemplateWithLevel(app.ListGalleries[OutlineGallery].ListTemplates[1], false, 1, Word10ListBehavior);
        for (var i = 0; i < outline.Length; i++)
            w.Document.Range(levels[i], levels[i]).Paragraphs[1].Range.ListFormat.ListLevelNumber = outline[i].Level;

        w.Paragraph("Restarted numbering", StyleHeading1);
        var restarted = Group(w, ["One again.", "Two again."]);
        w.Document.Range(restarted[0], restarted[^1]).ListFormat.ApplyListTemplateWithLevel(app.ListGalleries[NumberGallery].ListTemplates[2], false, 1, Word10ListBehavior);
        w.Paragraph("A paragraph after the lists.");
    }

    /// <summary>
    /// Appends paragraphs and returns where each starts, and where the last ends as the last entry: Word's paragraph
    /// objects of the document's end move with it, positions stay.
    /// </summary>
    static List<int> Group(Writer w, string[] texts)
    {
        var starts = new List<int>();
        var end = 0;
        foreach (var text in texts)
        {
            var paragraph = w.Paragraph(text);
            starts.Add((int)paragraph.Range.Start);
            end = (int)paragraph.Range.End;
        }
        starts.Add(end);
        return starts;
    }

    /// <summary>Left, center, right, decimal and bar tab stops, leaders, default tab stops and a hanging indent.</summary>
    static void Tabs(Writer w)
    {
        // WdTabAlignment, WdTabLeader
        const int TabLeft = 0, TabCenter = 1, TabRight = 2, TabDecimal = 3, TabBar = 4;
        const int LeaderSpaces = 0, LeaderDots = 1, LeaderDashes = 2, LeaderLines = 3;
        w.Paragraph("Default tab stops", StyleHeading1);
        w.Paragraph("a\tb\tc\td\te\tf");
        w.Paragraph("Custom tab stops", StyleHeading1);
        foreach (var (name, alignment) in new[] { ("Left", TabLeft), ("Center", TabCenter), ("Right", TabRight), ("Decimal", TabDecimal) })
        {
            foreach (var value in new[] { "12.5", "1234.567", "no point" })
                w.Paragraph($"{name}\t{value}").TabStops.Add(Writer.Cm(8), alignment, LeaderSpaces);
        }
        w.Paragraph("Leaders", StyleHeading1);
        foreach (var (name, leader) in new[] { ("Dots", LeaderDots), ("Dashes", LeaderDashes), ("Lines", LeaderLines) })
            w.Paragraph($"{name}\t{name.Length}").TabStops.Add(Writer.Cm(15), TabRight, leader);
        var bar = w.Paragraph("Bar tab\tafter the bar");
        bar.TabStops.Add(Writer.Cm(4), TabBar, LeaderSpaces);
        bar.TabStops.Add(Writer.Cm(5), TabLeft, LeaderSpaces);
        var hanging = w.Paragraph("1.\tA hanging indent of 1 cm takes the text after the tab to the indent, also on the following lines of the paragraph.");
        hanging.LeftIndent = Writer.Cm(1);
        hanging.FirstLineIndent = -Writer.Cm(1);
    }

    /// <summary>Headers and footers with page numbers, a different first page and odd and even pages.</summary>
    static void HeadersFooters(Writer w)
    {
        // WdHeaderFooterIndex, WdFieldType
        const int Primary = 1, FirstPage = 2, EvenPages = 3, FieldPage = 33, FieldNumPages = 26;
        var section = w.Document.Sections[1];
        section.PageSetup.DifferentFirstPageHeaderFooter = -1;
        w.Document.PageSetup.OddAndEvenPagesHeaderFooter = -1;
        section.Headers[FirstPage].Range.Text = "First page header";
        section.Headers[Primary].Range.Text = "Odd page header";
        section.Headers[EvenPages].Range.Text = "Even page header";
        Footer(section.Footers[Primary], ("Page ", 0), ("", FieldPage));
        Footer(section.Footers[EvenPages], ("Page ", 0), ("", FieldPage), (" of ", 0), ("", FieldNumPages));
        section.Footers[EvenPages].Range.ParagraphFormat.Alignment = AlignRight;
        w.Paragraph("Headers and footers", StyleHeading1);
        for (var i = 0; i < 14; i++)
        {
            w.Paragraph(Prose.Decisions);
            w.Paragraph(Prose.Constraints);
        }
    }

    /// <summary>Text and fields, one after the other, at the end of a header or footer (before its paragraph mark).</summary>
    static void Footer(dynamic story, params (string Text, int Field)[] parts)
    {
        story.Range.Text = "";
        foreach (var (text, field) in parts)
        {
            var end = story.Range;
            end.MoveEnd(1, -1);   // wdCharacter: before the paragraph mark
            end.Collapse(0);      // wdCollapseEnd
            if (field != 0)
                story.Range.Fields.Add(end, field);
            else
                end.InsertAfter(text);
        }
    }

    /// <summary>Tables: a simple grid, widths fitted to the content, merged cells and shading, a styled table with a header row.</summary>
    static void Tables(Writer w)
    {
        // WdAutoFitBehavior
        const int FitContent = 1, FitWindow = 2;
        w.Paragraph("Simple grid", StyleHeading1);
        var simple = AddTable(w, 3, 3);
        Fill((object)simple, (r, c) => $"Row {r}, column {c}");
        w.Paragraph("Fitted to the content", StyleHeading1);
        var fitted = AddTable(w, 3, 3);
        Fill((object)fitted, (r, c) => c == 2 ? "A longer cell text that needs more room" : $"{r}{c}");
        fitted.AutoFitBehavior(FitContent);
        w.Paragraph("Merged cells and shading", StyleHeading1);
        var merged = AddTable(w, 3, 4);
        Fill((object)merged, (r, c) => $"{r}.{c}");
        merged.Cell(1, 1).Merge(merged.Cell(1, 2));
        merged.Cell(2, 3).Merge(merged.Cell(3, 3));
        merged.Cell(1, 1).Shading.BackgroundPatternColor = Rgb(222, 235, 247);
        merged.AutoFitBehavior(FitWindow);
        w.Paragraph("Table style with header row", StyleHeading1);
        var styled = AddTable(w, 5, 3);
        Fill((object)styled, (r, c) => r == 1 ? $"Heading {c}" : $"Value {r * 10 + c}");
        const int GridTable4Accent1 = -284;   // WdBuiltinStyle wdStyleGridTable4_Accent1
        styled.Style = GridTable4Accent1;
        styled.Rows[1].HeadingFormat = -1;
        w.Paragraph("A paragraph after the tables.");
    }

    /// <summary>A table in a new paragraph at the end of the document; the writer goes on after it.</summary>
    static dynamic AddTable(Writer w, int rows, int columns)
    {
        // With Word 2000's table behavior Word gives the table the Table Grid style, as the user interface does.
        const int Word9TableBehavior = 1;
        var anchor = w.Paragraph("");
        var table = w.Document.Tables.Add(anchor.Range, rows, columns, Word9TableBehavior);
        return table;
    }

    static void Fill(object cells, Func<int, int, string> text)
    {
        dynamic table = cells;
        int rows = table.Rows.Count, columns = table.Columns.Count;
        for (var r = 1; r <= rows; r++)
        {
            for (var c = 1; c <= columns; c++)
            {
                try { table.Cell(r, c).Range.Text = text(r, c); }
                catch (System.Runtime.InteropServices.COMException) { }   // merged away
            }
        }
    }

    /// <summary>Keep with next, keep lines together, widow control, page breaks and page break before.</summary>
    static void Breaks(Writer w)
    {
        w.Paragraph("Breaks and keeps", StyleHeading1);
        for (var i = 0; i < 8; i++)
            w.Paragraph(Prose.Decisions);
        // A heading near the bottom of the page goes to the next page with its paragraph.
        w.Paragraph("A heading kept with its paragraph", StyleHeading2);
        w.Paragraph(Prose.Constraints);
        for (var i = 0; i < 3; i++)
            w.Paragraph(Prose.Measuring);
        w.Paragraph($"Kept together. {Prose.Rules} {Prose.Decisions}").KeepTogether = -1;
        w.Paragraph($"No widow control. {Prose.Rules} {Prose.Measuring}").WidowControl = 0;
        w.Paragraph("A page break follows.").Range.InsertAfter("\f");
        w.Paragraph($"Page break before. {Prose.Short}").PageBreakBefore = -1;
        w.Paragraph(Prose.Decisions);
    }
}
