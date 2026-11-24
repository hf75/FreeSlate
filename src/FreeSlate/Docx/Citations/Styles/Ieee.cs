namespace FreeSlate.Docx.Citations.Styles;

/// <summary>Word's IEEE style (see CitationStyle): placeholder until made as Word formats it.</summary>
public sealed class Ieee : CitationStyle
{
    public override string Name => "IEEE";

    public override string File => @"\IEEE2006OfficeOnline.xsl";

    public override string? Version => null;

    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context) => Simple.Cite(request);

    public override BibliographyList Bibliography(CitationContext context, int lcid) => Simple.Bibliography(context);
}
