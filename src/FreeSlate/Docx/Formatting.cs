namespace FreeSlate.Docx;

/// <summary>
/// Character formatting to give text (catalog: run.basic, run.decoration), as Word's buttons give it: each property
/// given is set as direct formatting of the text's runs (w:rPr), the others stay as they are.
/// </summary>
public sealed record TextFormatting
{
    /// <summary>Bold and italic (w:b, w:i, with their complex-script twins w:bCs, w:iCs, which Word's buttons set too).</summary>
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }

    /// <summary>The underline (w:u): single, double, thick, dotted, dash, wave, words and others, or none.</summary>
    public string? Underline { get; init; }

    /// <summary>The size in points (w:sz and w:szCs, in half points).</summary>
    public double? Size { get; init; }

    /// <summary>The font, for all scripts (w:rFonts ascii, hAnsi, eastAsia and cs, the theme's fonts taken away).</summary>
    public string? Font { get; init; }

    /// <summary>The color as RRGGBB, or auto (w:color, a theme color taken away).</summary>
    public string? Color { get; init; }

    /// <summary>The highlight (w:highlight): yellow, green, cyan, magenta, blue, red, darkBlue and others, or none.</summary>
    public string? Highlight { get; init; }
}

/// <summary>
/// Paragraph formatting to give a paragraph (catalog: paragraph.basic, paragraph.spacing): each property given is set
/// as the paragraph's direct formatting (w:pPr), the others stay as they are.
/// </summary>
public sealed record ParagraphFormatting
{
    /// <summary>The paragraph style, by its id (w:pStyle).</summary>
    public string? Style { get; init; }

    /// <summary>The alignment (w:jc): left, center, right, both or distribute.</summary>
    public string? Alignment { get; init; }

    /// <summary>The indents in points (w:ind): left, right, and the first line's from the left (negative: hanging).</summary>
    public double? Left { get; init; }
    public double? Right { get; init; }
    public double? FirstLine { get; init; }

    /// <summary>The space before and after in points, and the line spacing in lines (w:spacing, the rule auto).</summary>
    public double? Before { get; init; }
    public double? After { get; init; }
    public double? Lines { get; init; }
}
