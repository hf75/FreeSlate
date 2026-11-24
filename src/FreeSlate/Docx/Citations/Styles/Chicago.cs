namespace FreeSlate.Docx.Citations.Styles;

/// <summary>Word's Chicago style (see CitationStyle): placeholder until made as Word formats it.</summary>
public sealed class Chicago : CitationStyle
{
    public override string Name => "Chicago";

    public override string File => @"\CHICAGO.XSL";

    public override string? Version => null;

    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context) => Simple.Cite(request);

    public override BibliographyList Bibliography(CitationContext context, int lcid) => Simple.Bibliography(context);
}
