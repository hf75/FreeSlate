namespace FreeSlate.Docx.Citations.Styles;

/// <summary>Word's GOST - Title Sort style (see CitationStyle): placeholder until made as Word formats it.</summary>
public sealed class GostTitle : CitationStyle
{
    public override string Name => "GOST - Title Sort";

    public override string File => @"\GostTitle.XSL";

    public override string? Version => null;

    public override IReadOnlyList<string> Aliases => ["Gost - Titelsortierung"];

    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context) => Simple.Cite(request);

    public override BibliographyList Bibliography(CitationContext context, int lcid) => Simple.Bibliography(context);
}
