namespace FreeSlate.Docx.Citations.Styles;

/// <summary>Word's ISO 690 - Numerical Reference style (see CitationStyle): placeholder until made as Word formats it.</summary>
public sealed class IsoNumbers : CitationStyle
{
    public override string Name => "ISO 690 - Numerical Reference";

    public override string File => @"\ISO690Nmerical.XSL";

    public override string? Version => null;

    public override IReadOnlyList<string> Aliases => ["ISO 690 - Numerische Referenz", "ISO 690 numerical"];

    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context) => Simple.Cite(request);

    public override BibliographyList Bibliography(CitationContext context, int lcid) => Simple.Bibliography(context);
}
