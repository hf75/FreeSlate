namespace FreeSlate.Docx.Citations.Styles;

/// <summary>Word's ISO 690 - First Element and Date style (see CitationStyle): placeholder until made as Word formats it.</summary>
public sealed class IsoDate : CitationStyle
{
    public override string Name => "ISO 690 - First Element and Date";

    public override string File => @"\ISO690.XSL";

    public override string? Version => null;

    public override IReadOnlyList<string> Aliases => ["ISO 690 - Erstes Element und Datum", "ISO 690", "ISO690"];

    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context) => Simple.Cite(request);

    public override BibliographyList Bibliography(CitationContext context, int lcid) => Simple.Bibliography(context);
}
