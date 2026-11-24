namespace FreeSlate.Docx.Citations.Styles;

/// <summary>Word's SIST02 style (see CitationStyle): placeholder until made as Word formats it.</summary>
public sealed class Sist02 : CitationStyle
{
    public override string Name => "SIST02";

    public override string File => @"\SIST02.XSL";

    public override string? Version => null;

    public override IReadOnlyList<string> Aliases => ["SIST 02"];

    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context) => Simple.Cite(request);

    public override BibliographyList Bibliography(CitationContext context, int lcid) => Simple.Bibliography(context);
}
