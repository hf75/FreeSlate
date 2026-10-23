namespace FreeSlate;

/// <summary>
/// Character formatting to give text (catalog: run.basic, run.decoration), as Word's and PowerPoint's buttons give it:
/// each property given is set as direct formatting of the text's runs (w:rPr in a Word document, a:rPr on a slide), the
/// others stay as they are.
/// </summary>
public sealed record TextFormatting
{
    /// <summary>
    /// Bold and italic (w:b, w:i, with their complex-script twins w:bCs, w:iCs, which Word's buttons set too; a:rPr b,
    /// i).
    /// </summary>
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }

    /// <summary>
    /// The underline by Word's names (w:u): single, double, thick, dotted, dash, wave, words and others, or none; on a
    /// slide as DrawingML's (a:rPr u: sng, dbl, heavy, dotted, dash, wavy, words …), whose names it takes too.
    /// </summary>
    public string? Underline { get; init; }

    /// <summary>The size in points (w:sz and w:szCs, in half points; a:rPr sz, in hundredths).</summary>
    public double? Size { get; init; }

    /// <summary>
    /// The font: in a Word document for all scripts (w:rFonts ascii, hAnsi, eastAsia and cs, the theme's fonts taken
    /// away); on a slide for Latin text (a:latin), and for East Asian and complex-script text where a run has such (a:ea,
    /// a:cs).
    /// </summary>
    public string? Font { get; init; }

    /// <summary>The color as RRGGBB, or auto (w:color, a theme color taken away; a:rPr's fill, auto: none of its own).</summary>
    public string? Color { get; init; }

    /// <summary>
    /// The highlight by Word's names (w:highlight): yellow, green, cyan, magenta, blue, red, darkBlue and others, or none;
    /// on a slide in the color of that name, or as RRGGBB (a:highlight).
    /// </summary>
    public string? Highlight { get; init; }
}

/// <summary>
/// Paragraph formatting to give a paragraph (catalog: paragraph.basic, paragraph.spacing): each property given is set
/// as the paragraph's direct formatting (w:pPr in a Word document, a:pPr on a slide), the others stay as they are.
/// </summary>
public sealed record ParagraphFormatting
{
    /// <summary>The paragraph style, by its id (w:pStyle); slides have none.</summary>
    public string? Style { get; init; }

    /// <summary>The alignment (w:jc; a:pPr algn): left, center, right, both or distribute.</summary>
    public string? Alignment { get; init; }

    /// <summary>
    /// The indents in points (w:ind; a:pPr marL, marR, indent): left, right, and the first line's from the left
    /// (negative: hanging).
    /// </summary>
    public double? Left { get; init; }
    public double? Right { get; init; }
    public double? FirstLine { get; init; }

    /// <summary>
    /// The space before and after in points, and the line spacing in lines (w:spacing, the rule auto; a:spcBef and
    /// a:spcAft in points, a:lnSpc in percent).
    /// </summary>
    public double? Before { get; init; }
    public double? After { get; init; }
    public double? Lines { get; init; }
}
