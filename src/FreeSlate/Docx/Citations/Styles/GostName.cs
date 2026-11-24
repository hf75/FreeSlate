namespace FreeSlate.Docx.Citations.Styles;

/// <summary>Word's GOST - Name Sort style (see CitationStyle): placeholder until made as Word formats it.</summary>
public sealed class GostName : CitationStyle
{
    public override string Name => "GOST - Name Sort";

    public override string File => @"\GostName.XSL";

    public override string? Version => null;

    public override IReadOnlyList<string> Aliases => ["Gost - Namenssortierung", "GOST"];

    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context) => Simple.Cite(request);

    public override BibliographyList Bibliography(CitationContext context, int lcid) => Simple.Bibliography(context);
}
