namespace FreeSlate.Docx.Citations.Styles;

/// <summary>Word's Turabian style (see CitationStyle): placeholder until made as Word formats it.</summary>
public sealed class Turabian : CitationStyle
{
    public override string Name => "Turabian";

    public override string File => @"\TURABIAN.XSL";

    public override string? Version => null;

    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context) => Simple.Cite(request);

    public override BibliographyList Bibliography(CitationContext context, int lcid) => Simple.Bibliography(context);
}
