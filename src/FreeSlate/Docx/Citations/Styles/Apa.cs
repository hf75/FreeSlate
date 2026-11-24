namespace FreeSlate.Docx.Citations.Styles;

/// <summary>Word's APA style (see CitationStyle): placeholder until made as Word formats it.</summary>
public sealed class Apa : CitationStyle
{
    public override string Name => "APA";

    public override string File => @"\APASixthEditionOfficeOnline.xsl";

    public override string? Version => "6";

    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context) => Simple.Cite(request);

    public override BibliographyList Bibliography(CitationContext context, int lcid) => Simple.Bibliography(context);
}
