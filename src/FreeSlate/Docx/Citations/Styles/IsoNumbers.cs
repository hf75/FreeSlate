using System.Globalization;

namespace FreeSlate.Docx.Citations.Styles;

/// <summary>
/// Word's ISO 690 - Numerical Reference style (see CitationStyle) as Word writes it: a citation is its sources' numbers in parentheses,
/// numbered in the order the document first cites them ("(1 p. 33; 14)"); the bibliography lists them in that order, each entry its
/// number, the first element in bold ("1. **Smith, John.**", an author's next entry a dash) and the rest as ISO 690 - First Element and
/// Date writes it (IsoDate.Parts).
/// </summary>
public sealed class IsoNumbers : CitationStyle
{
    public override string Name => "ISO 690 - Numerical Reference";

    public override string File => @"\ISO690Nmerical.XSL";

    public override string? Version => null;

    public override IReadOnlyList<string> Aliases => ["ISO 690 - Numerische Referenz", "ISO 690 numerical"];

    /// <summary>
    /// A citation as Word writes it: each source's prefix, number, its pages or volume and page after a space ("p. 33", "3: 12") and its
    /// suffix, several between semicolons; what to leave out (\n, \y, \t) does not matter to a number.
    /// </summary>
    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context)
    {
        var w = IsoDate.Words.Of(request.Lcid);
        var parts = request.Sources.Select(c =>
            c.Prefix + IsoDate.Join(" ", context.Number(c.Source).ToString(CultureInfo.InvariantCulture), IsoDate.Locator(c, w, ": ")) + c.Suffix);
        return [new Piece("(" + string.Join("; ", parts) + ")")];
    }

    /// <summary>
    /// The bibliography as Word writes it: every source in the order of its number ("1. "). Word's bold for the first element is not
    /// closed where a source has none: from where its first element would stand all of the bibliography is bold, the numbers of the
    /// entries after it too ("9. ***Beowulf. *****London : Penguin.**", "**10. Tolstoy, Leo. …**").
    /// </summary>
    public override BibliographyList Bibliography(CitationContext context, int lcid)
    {
        var w = IsoDate.Words.Of(lcid);
        var entries = new List<BibliographyEntry>();
        var bold = false;
        string? previous = null;
        foreach (var source in context.Sources.Sources.OrderBy(context.Number))
        {
            var names = IsoDate.Names(source, w);
            var (lead, body) = IsoDate.Parts(source, w);
            var text = new List<Piece> { new(context.Number(source).ToString(CultureInfo.InvariantCulture) + ". ", Bold: bold) };
            text.AddRange(lead.Select(p => p with { Bold = bold }));
            if (names == null)
                bold = true;
            else if (names == previous)
                text.Add(new Piece("—. ", Bold: bold));
            else
                text.AddRange([new Piece(IsoDate.Ended(names) ?? names, Bold: true), new Piece(" ", Bold: bold)]);
            text.AddRange(body.Select(p => p with { Bold = bold }));
            previous = names;
            entries.Add(new BibliographyEntry(text));
        }
        return new BibliographyList(entries, BibliographyLayout.Plain);
    }
}
