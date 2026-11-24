namespace FreeSlate.Docx.Citations.Styles;

/// <summary>Word's Harvard - Anglia style (see CitationStyle): placeholder until made as Word formats it.</summary>
public sealed class Harvard : CitationStyle
{
    public override string Name => "Harvard - Anglia";

    public override string File => @"\HarvardAnglia2008OfficeOnline.xsl";

    public override string? Version => null;

    public override IReadOnlyList<string> Aliases => ["Harvard"];

    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context) => Simple.Cite(request);

    public override BibliographyList Bibliography(CitationContext context, int lcid) => Simple.Bibliography(context);
}
