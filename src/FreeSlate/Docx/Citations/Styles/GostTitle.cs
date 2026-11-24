namespace FreeSlate.Docx.Citations.Styles;

/// <summary>
/// Word's GOST - Title Sort style (see CitationStyle) as Word formats it: citations as <see cref="Gost"/> writes them (the same as GOST -
/// Name Sort), the bibliography sorted by the sources' titles, each entry beginning with its title in bold and naming who made it after
/// it ("<b>Learning with Machines</b> [Book] / auth. Smith John. - Berlin : Springer, 2020.").
/// </summary>
public sealed class GostTitle : CitationStyle
{
    public override string Name => "GOST - Title Sort";

    public override string File => @"\GostTitle.XSL";

    public override string? Version => null;

    public override IReadOnlyList<string> Aliases => ["Gost - Titelsortierung"];

    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context) => Gost.Cite(request);

    public override BibliographyList Bibliography(CitationContext context, int lcid) => Gost.Bibliography(context, lcid, byTitle: true);
}
