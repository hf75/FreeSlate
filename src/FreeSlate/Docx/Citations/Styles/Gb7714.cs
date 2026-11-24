namespace FreeSlate.Docx.Citations.Styles;

/// <summary>Word's GB7714 style (see CitationStyle): placeholder until made as Word formats it.</summary>
public sealed class Gb7714 : CitationStyle
{
    public override string Name => "GB7714";

    public override string File => @"\GB.XSL";

    public override string? Version => null;

    public override IReadOnlyList<string> Aliases => ["GB"];

    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context) => Simple.Cite(request);

    public override BibliographyList Bibliography(CitationContext context, int lcid) => Simple.Bibliography(context);
}
