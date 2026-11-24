namespace FreeSlate.Docx.Citations.Styles;

/// <summary>Word's MLA style (see CitationStyle): placeholder until made as Word formats it.</summary>
public sealed class Mla : CitationStyle
{
    public override string Name => "MLA";

    public override string File => @"\MLASeventhEditionOfficeOnline.xsl";

    public override string? Version => null;

    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context) => Simple.Cite(request);

    public override BibliographyList Bibliography(CitationContext context, int lcid) => Simple.Bibliography(context);
}
