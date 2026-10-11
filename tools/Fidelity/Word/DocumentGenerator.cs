using System.Runtime.Versioning;
using Fidelity.PowerPoint;

namespace Fidelity.Word;

/// <summary>
/// Builds the generated test documents through Word itself, so the files contain exactly the markup Word writes
/// (styles, theme fonts, section properties …). Each document isolates one group of features.
/// </summary>
[SupportedOSPlatform("windows")]
public static partial class DocumentGenerator
{
    // WdBuiltinStyle
    internal const int StyleNormal = -1, StyleHeading1 = -2, StyleHeading2 = -3, StyleHeading3 = -4, StyleTitle = -63;
    // WdParagraphAlignment
    internal const int AlignLeft = 0, AlignCenter = 1, AlignRight = 2, AlignJustify = 3, AlignDistribute = 4;
    // WdLineSpacing
    internal const int LineSingle = 0, LineOnePt5 = 1, LineDouble = 2, LineAtLeast = 3, LineExactly = 4, LineMultiple = 5;
    // WdBreakType
    internal const int SectionBreakNextPage = 2, SectionBreakContinuous = 3;

    /// <summary>The documents by name, in tests/corpus/docx/generated.</summary>
    static readonly (string Name, Action<Writer> Build)[] Documents =
    [
        ("text-basic", TextBasic),
        ("text-fonts", TextFonts),
        ("paragraph-alignment", ParagraphAlignment),
        ("paragraph-spacing", ParagraphSpacing),
        ("character-formats", CharacterFormats),
        ("page-setup", PageSetup),
        ("lists", Lists),
        ("tabs", Tabs),
        ("headers-footers", HeadersFooters),
        ("tables", Tables),
        ("breaks", Breaks),
        ("drawings", Drawings),
        ("textboxes", TextBoxes),
        ("notes", Notes),
        ("wrapping", Wrapping),
        ("wrap-types", WrapTypes),
        ("graphics", Graphics),
        ("charts", ChartsAndDiagrams),
        ("equations", Equations),
        ("pictures", Pictures),
        ("charts-more", MoreCharts),
        ("charts-more2", MoreCharts2),
        ("charts-more3", MoreCharts3),
        ("smartart", MoreSmartArt),
        ("shapes-effects", ShapesAndTextEffects),
        ("revisions", Revisions),
        ("content-controls", ContentControls),
        ("form-fields", FormFields),
    ];

    /// <summary>Generates the named documents (without folder and extension), or all of them.</summary>
    public static void Run(string outputDir, IReadOnlyCollection<string> only)
    {
        using var session = new WordSession();
        Console.WriteLine($"Word {session.Version}");
        foreach (var (name, build) in Documents)
        {
            if (only.Count > 0 && !only.Contains(name))
                continue;
            var path = Path.Combine(outputDir, name + ".docx");
            var document = session.Create();
            try
            {
                build(new Writer(document));
            }
            catch
            {
                WordSession.Close(document);
                throw;
            }
            WordSession.SaveAndClose(document, path);
            CorpusGenerator.Anonymize(path);
            Console.WriteLine($"  {Path.GetFileName(path)}");
        }
    }

    /// <summary>Appends paragraphs and sections to a new document through Word's object model.</summary>
    internal sealed class Writer(dynamic document)
    {
        /// <summary>Whether the document's last paragraph is still empty and takes the next text.</summary>
        bool lastEmpty = true;

        public dynamic Document => document;

        /// <summary>Points from centimeters.</summary>
        public static float Cm(double centimeters) => (float)(centimeters * 72 / 2.54);

        /// <summary>
        /// Appends a paragraph in a built-in style without direct formatting; runs with a format get it on their text.
        /// Returns the paragraph.
        /// </summary>
        public dynamic Paragraph(int style, params (string Text, Action<dynamic>? Font)[] runs)
        {
            if (!lastEmpty)
                document.Content.InsertParagraphAfter();
            lastEmpty = false;
            var paragraph = document.Paragraphs.Last;
            paragraph.Range.Style = style;
            paragraph.Range.ParagraphFormat.Reset();
            paragraph.Range.Font.Reset();
            int start = paragraph.Range.Start;
            paragraph.Range.InsertBefore(string.Concat(runs.Select(r => r.Text)));
            foreach (var (text, font) in runs)
            {
                if (font != null)
                    font(document.Range(start, start + text.Length).Font);
                start += text.Length;
            }
            return document.Paragraphs.Last;
        }

        public dynamic Paragraph(string text, int style = StyleNormal) => Paragraph(style, (text, null));

        /// <summary>Starts a new section with a break of the given WdBreakType; returns the new section.</summary>
        public dynamic Section(int breakType = SectionBreakNextPage)
        {
            int end = document.Content.End - 1;
            document.Range(end, end).InsertBreak(breakType);
            lastEmpty = true;
            return document.Sections.Last;
        }
    }

    static Action<dynamic> Bold => f => f.Bold = -1;
    static Action<dynamic> Italic => f => f.Italic = -1;

    /// <summary>Titles, headings and body text in the default styles over three pages.</summary>
    static void TextBasic(Writer w)
    {
        w.Paragraph("Text basics", StyleTitle);
        w.Paragraph("Introduction", StyleHeading1);
        w.Paragraph(Prose.Decisions);
        w.Paragraph(Prose.Rules);
        w.Paragraph("Pangrams", StyleHeading2);
        w.Paragraph(Prose.Pangrams);
        w.Paragraph("Numbers and long words", StyleHeading2);
        w.Paragraph(Prose.Numbers);
        w.Paragraph(Prose.LongWord);
        w.Paragraph("Method", StyleHeading1);
        w.Paragraph(Prose.Measuring);
        w.Paragraph(Prose.Constraints);
        w.Paragraph(Prose.Short);
        for (var round = 1; round <= 3; round++)
        {
            w.Paragraph($"Repetition {round}", StyleHeading3);
            w.Paragraph(StyleNormal, ("Emphasis: ", Bold), (Prose.Decisions, null));
            w.Paragraph(StyleNormal, (Prose.Rules, null), (" Italic closing words.", Italic));
            w.Paragraph(Prose.Measuring);
            w.Paragraph(Prose.Constraints);
            w.Paragraph(Prose.Numbers);
        }
    }

    /// <summary>The same text in common fonts and in a range of sizes.</summary>
    static void TextFonts(Writer w)
    {
        string[] fonts = ["Aptos", "Calibri", "Arial", "Times New Roman", "Segoe UI", "Georgia", "Cambria", "Courier New", "Verdana"];
        w.Paragraph("Fonts", StyleHeading1);
        foreach (var font in fonts)
            w.Paragraph(StyleNormal, ($"{font}: {Prose.Pangrams}", f => f.Name = font));
        w.Paragraph("Sizes", StyleHeading1);
        foreach (var font in new[] { "Aptos", "Times New Roman" })
        {
            foreach (var size in new[] { 8f, 9f, 10f, 11f, 12f, 14f, 16f, 20f, 24f, 28f, 36f, 48f })
                w.Paragraph(StyleNormal, ($"{font} {size} pt: The quick brown fox jumps over the lazy dog.", f => { f.Name = font; f.Size = size; }));
        }
    }

    /// <summary>Alignment and indents.</summary>
    static void ParagraphAlignment(Writer w)
    {
        w.Paragraph("Alignment", StyleHeading1);
        foreach (var (alignment, name) in new[] { (AlignLeft, "Left"), (AlignCenter, "Centered"), (AlignRight, "Right"), (AlignJustify, "Justified"), (AlignDistribute, "Distributed") })
            w.Paragraph($"{name}. {Prose.Rules}").Alignment = alignment;
        w.Paragraph("Indents", StyleHeading1);
        w.Paragraph($"Left indent 2 cm. {Prose.Decisions}").LeftIndent = Writer.Cm(2);
        w.Paragraph($"Right indent 3 cm. {Prose.Decisions}").RightIndent = Writer.Cm(3);
        w.Paragraph($"First line 1.25 cm. {Prose.Decisions}").FirstLineIndent = Writer.Cm(1.25);
        var hanging = w.Paragraph($"Hanging 1 cm. {Prose.Decisions}");
        hanging.LeftIndent = Writer.Cm(1);
        hanging.FirstLineIndent = -Writer.Cm(1);
        var both = w.Paragraph($"Both sides 2.5 cm, justified. {Prose.Measuring}");
        both.LeftIndent = Writer.Cm(2.5);
        both.RightIndent = Writer.Cm(2.5);
        both.Alignment = AlignJustify;
        var negative = w.Paragraph($"Negative indents reach into the margins. {Prose.Measuring}");
        negative.LeftIndent = -Writer.Cm(1);
        negative.RightIndent = -Writer.Cm(1);
    }

    /// <summary>Line spacing rules and space before and after paragraphs.</summary>
    static void ParagraphSpacing(Writer w)
    {
        w.Paragraph("Line spacing", StyleHeading1);
        foreach (var (rule, value, name) in new[]
                 {
                     (LineSingle, 0f, "Single"), (LineMultiple, 13.8f, "Multiple 1.15"), (LineOnePt5, 0f, "One and a half"),
                     (LineDouble, 0f, "Double"), (LineAtLeast, 20f, "At least 20 pt"), (LineExactly, 14f, "Exactly 14 pt"),
                     (LineExactly, 10f, "Exactly 10 pt"), (LineMultiple, 36f, "Multiple 3"),
                 })
        {
            var paragraph = w.Paragraph($"{name}. {Prose.Rules}");
            paragraph.LineSpacingRule = rule;
            if (value > 0)
                paragraph.LineSpacing = value;
        }
        w.Paragraph("Space before and after", StyleHeading1);
        foreach (var (before, after) in new[] { (0f, 0f), (6f, 6f), (12f, 0f), (0f, 24f), (24f, 12f) })
        {
            var paragraph = w.Paragraph($"Before {before} pt, after {after} pt. {Prose.Short}");
            paragraph.SpaceBefore = before;
            paragraph.SpaceAfter = after;
        }
        // The object model sets contextual spacing on styles only.
        const int StyleTypeParagraph = 1;
        var contextual = w.Document.Styles.Add("Contextual", StyleTypeParagraph);
        contextual.BaseStyle = StyleNormal;
        contextual.ParagraphFormat.SpaceBefore = 12f;
        contextual.ParagraphFormat.SpaceAfter = 12f;
        contextual.NoSpaceBetweenParagraphsOfSameStyle = -1;
        w.Paragraph("Same style, no space between", StyleHeading1);
        for (var i = 1; i <= 3; i++)
            w.Paragraph($"Contextual spacing {i}. {Prose.Short}").Style = "Contextual";
        w.Paragraph($"Back to Normal. {Prose.Short}");
    }

    /// <summary>Bold, italic, underlines, strike, caps, scripts, highlight, colors, spacing and scale.</summary>
    static void CharacterFormats(Writer w)
    {
        w.Paragraph("Character formats", StyleHeading1);
        w.Paragraph(StyleNormal, ("Regular, ", null), ("bold, ", Bold), ("italic, ", Italic), ("bold italic", f => { f.Bold = -1; f.Italic = -1; }), (".", null));
        // WdUnderline
        foreach (var (underline, name) in new[] { (1, "single"), (2, "words only"), (3, "double"), (4, "dotted"), (6, "thick"), (7, "dash"), (9, "dot dash"), (11, "wavy"), (43, "wavy double"), (39, "long dash") })
            w.Paragraph(StyleNormal, ("Underline ", null), ($"{name} under these words", f => f.Underline = underline), (" and then plain.", null));
        w.Paragraph(StyleNormal, ("Strikethrough", f => f.StrikeThrough = -1), (", ", null), ("double strikethrough", f => f.DoubleStrikeThrough = -1), (".", null));
        w.Paragraph(StyleNormal, ("Small caps", f => f.SmallCaps = -1), (" and ", null), ("all caps", f => f.AllCaps = -1), (" and ", null), ("hidden text", f => f.Hidden = -1), ("end.", null));
        w.Paragraph(StyleNormal, ("E = mc", null), ("2", f => f.Superscript = -1), (", H", null), ("2", f => f.Subscript = -1), ("O, raised ", null), ("3 pt", f => f.Position = 3), (", lowered ", null), ("3 pt", f => f.Position = -3), (".", null));
        w.Paragraph(StyleNormal, ("Shaded ", null), ("yellow", f => f.Shading.BackgroundPatternColor = Rgb(255, 255, 0)), (", text in ", null), ("red", f => f.Color = Rgb(192, 0, 0)), (", ", null), ("blue", f => f.Color = Rgb(0, 112, 192)), (" and ", null), ("green", f => f.Color = Rgb(0, 176, 80)), (".", null));
        w.Paragraph(StyleNormal, ("Spacing expanded by 2 pt", f => f.Spacing = 2f), (", condensed by 1 pt", f => f.Spacing = -1f), (", scaled 150 %", f => f.Scaling = 150), (", scaled 66 %", f => f.Scaling = 66), (".", null));
        w.Paragraph(StyleNormal, ("Outline", f => f.Outline = -1), (", ", null), ("shadow", f => f.Shadow = -1), (", ", null), ("emboss", f => f.Emboss = -1), (", ", null), ("engrave", f => f.Engrave = -1), (".", null));
        var highlight = w.Paragraph("Highlighter colors: yellow, bright green, turquoise.");
        int start = highlight.Range.Start;
        foreach (var (word, index) in new[] { ("yellow", 7), ("bright green", 4), ("turquoise", 3) })
        {
            var at = ((string)highlight.Range.Text).IndexOf(word, StringComparison.Ordinal);
            w.Document.Range(start + at, start + at + word.Length).HighlightColorIndex = index;
        }
    }

    /// <summary>Sections with other paper sizes, orientation, margins, columns and vertical alignment.</summary>
    static void PageSetup(Writer w)
    {
        const int Landscape = 1, PaperA4 = 7, PaperLetter = 2, VerticalCenter = 1;
        var first = w.Document.Sections.First.PageSetup;
        first.PaperSize = PaperA4;
        w.Paragraph("A4 portrait, default margins", StyleHeading1);
        w.Paragraph(Prose.Decisions);

        var letter = w.Section().PageSetup;
        letter.PaperSize = PaperLetter;
        letter.Orientation = Landscape;
        w.Paragraph("Letter landscape", StyleHeading1);
        w.Paragraph(Prose.Rules);

        var narrow = w.Section().PageSetup;
        narrow.Orientation = 0;
        narrow.PaperSize = PaperA4;
        narrow.TopMargin = narrow.BottomMargin = narrow.LeftMargin = narrow.RightMargin = Writer.Cm(1.27);
        w.Paragraph("Narrow margins", StyleHeading1);
        w.Paragraph(Prose.Measuring);

        var centered = w.Section().PageSetup;
        centered.TopMargin = centered.BottomMargin = Writer.Cm(2.5);
        centered.LeftMargin = centered.RightMargin = Writer.Cm(2.5);
        centered.VerticalAlignment = VerticalCenter;
        w.Paragraph("Vertically centered", StyleHeading1);
        w.Paragraph(Prose.Short);

        var columns = w.Section().PageSetup;
        columns.VerticalAlignment = 0;
        columns.TextColumns.SetCount(2);
        w.Paragraph("Two columns", StyleHeading1);
        for (var i = 0; i < 4; i++)
        {
            w.Paragraph(Prose.Decisions);
            w.Paragraph(Prose.Constraints);
        }

        var three = w.Section(SectionBreakContinuous).PageSetup;
        three.TextColumns.SetCount(3);
        three.TextColumns.LineBetween = -1;
        w.Paragraph("Three columns with lines between, continuous", StyleHeading2);
        for (var i = 0; i < 3; i++)
            w.Paragraph(Prose.Measuring);
    }

    static int Rgb(int r, int g, int b) => r | (g << 8) | (b << 16);
}

/// <summary>Body text of the generated documents (written for this corpus).</summary>
internal static class Prose
{
    public const string Decisions =
        "Every page of a document is the result of many small decisions. Where does a line end, how much space goes " +
        "between two paragraphs, and which words move to the next page when the current one is full? A layout engine " +
        "answers these questions thousands of times for a single report.";

    public const string Rules =
        "Word breaks lines at spaces and after hyphens, keeps the last line of a paragraph from standing alone at the top " +
        "of a page, and pulls a heading onto the next page when the text that follows it would not fit. None of these " +
        "rules is complicated on its own; together they decide where every glyph ends up.";

    public const string Pangrams =
        "The quick brown fox jumps over the lazy dog. Pack my box with five dozen liquor jugs. How vexingly quick daft " +
        "zebras jump! Sphinx of black quartz, judge my vow.";

    public const string Numbers =
        "Numbers such as 1,234.56, dates like 11 October 2026 and identifiers such as ISO/IEC 29500-1:2016 test how a " +
        "line breaker treats punctuation, slashes and digits. Long words (internationalization, characteristically, " +
        "counterrevolutionaries) test what happens when a word barely fits.";

    public const string LongWord =
        "A paragraph with one very long word: Donaudampfschifffahrtsgesellschaftskapitaensmuetzenabzeichensammlung, " +
        "followed by ordinary text that continues for a while so that the line breaker has more to do after the long word.";

    public const string Measuring =
        "Measuring is better than guessing. A probe document varies one property at a time: the font size, the line " +
        "spacing, the width of the margins. Word lays it out and exports it, and the positions of the lines tell which " +
        "rule it followed.";

    public const string Constraints =
        "Tables, lists and footnotes add constraints of their own. A table row may refuse to split across pages, a list " +
        "number needs room before its text, and a footnote belongs on the page of its reference, which in turn changes " +
        "how much body text fits above it.";

    public const string Short = "A short paragraph.";
}
