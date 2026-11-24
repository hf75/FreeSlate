using System.Globalization;

namespace FreeSlate.Docx.Citations.Styles;

/// <summary>
/// Word's IEEE style (see CitationStyle) as Word writes it: a citation is its sources' numbers in brackets, numbered in the order the
/// document first cites them ("[1]", "[1, p. 33, 14]"; of a citation's switches only its pages show); the bibliography is a table of
/// those numbers ("[1] ") and the entries in that order, people as initials and last name, every one of them named, a part's title in
/// quotes ("Assessment," in *Handbook of Teaching*), a film's, a recording's or a work of art's in italics with its kind in brackets.
/// </summary>
public sealed class Ieee : CitationStyle
{
    public override string Name => "IEEE";

    public override string File => @"\IEEE2006OfficeOnline.xsl";

    public override string? Version => null;

    /// <summary>A citation as Word writes it: "[1]", "[14, pp. 46-48]", "[1, 3]"; prefix, suffix, volume and what to leave out set aside.</summary>
    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context)
    {
        var items = request.Sources.Select(c => Number(context, c.Source) + (c.Pages is { Length: > 0 } pages ? ", " + Pages(pages) : ""));
        return [new Piece("[" + string.Join(", ", items) + "]")];
    }

    /// <summary>The bibliography as Word writes it: every source in the order of its number, the number in a cell of its own ("[1] ").</summary>
    public override BibliographyList Bibliography(CitationContext context, int lcid)
    {
        var words = Common.German(lcid) ? Words.German : Words.English;
        var entries = context.Sources.Sources.OrderBy(context.Number)
            .Select(s => new BibliographyEntry(Entry(s, words), [new Piece("[" + Number(context, s) + "] ")])).ToList();
        return new BibliographyList(entries, BibliographyLayout.NumberTable);
    }

    /// <summary>The words an entry is written with, in English or German as Word's IEEE has them (pages "p."/"pp." in both).</summary>
    sealed record Words(string And, string Editor, string Editors, string Edition, string Volume, string Issue, string Accessed, string Open,
        string Close, string Director, string Interviewee, string Composer, string Writer, string Artist, string SoundRecording)
    {
        public static Words English { get; } = new("and", "Ed.", "Eds.", "ed.", "vol.", "no.", "Accessed", "\"", "\"", "Director", "Interviewee",
            "Composer", "Writer", "Artist", "Sound Recording");

        public static Words German { get; } = new("und", "Hrsg.", "Hrsg.", "Hrsg.", "Bd.", "Nr.", "Zugriff am", "„", "“", "Regisseur", "Interviewee",
            "Komponist", "Autor", "Artist", "Tonaufnahme");
    }

    static string Number(CitationContext context, Source source) => context.Number(source).ToString(CultureInfo.InvariantCulture);

    /// <summary>Pages as Word writes them: "p. 33" for one, "pp. 46-48" for a range or a list.</summary>
    static string Pages(string pages) => (pages.IndexOfAny(['-', '–', '—', ',']) >= 0 ? "pp. " : "p. ") + pages;

    /// <summary>
    /// A source's entry as Word writes it by its kind: what it has of its kind's parts, in their order, mostly between commas. The
    /// entries of books, journals' and periodicals' articles, conferences, films, performances, works of art and miscellaneous sources
    /// end with a space after the period, as Word's do.
    /// </summary>
    static List<Piece> Entry(Source s, Words w)
    {
        var text = new List<Piece>();
        void Add(string? part, bool italic = false)
        {
            if (!string.IsNullOrEmpty(part))
                text.Add(new Piece(part, italic));
        }
        var authors = People(s.Role("Author"), w);
        var date = Date(s);
        switch (s.Type)
        {
            case "Book":
                Add(Then(Commas(authors ?? Editors(s, w), s["Title"], s["Edition"] is { } edition ? edition + " " + w.Edition : null,
                    s["Volume"] is { } volume ? w.Volume + " " + volume : null, Place(s), date), ". "));
                break;
            case "BookSection":
                Lead(authors, s["Title"], w, Add);
                Within(s["BookTitle"], Commas(Editors(s, w), s["City"], s["Publisher"], date, PagesOf(s)), ".", Add);
                break;
            case "JournalArticle":
            case "ArticleInAPeriodical":
                Lead(authors, s["Title"], w, Add);
                Add(s[s.Type == "JournalArticle" ? "JournalName" : "PeriodicalTitle"] is { } name ? name + ", " : null, italic: true);
                Add(Then(Commas(s["Volume"] is { } volume2 ? w.Volume + " " + volume2 : null, s["Issue"] is { } issue ? w.Issue + " " + issue : null,
                    PagesOf(s), date), ". "));
                break;
            case "ConferenceProceedings":
                Lead(authors, s["Title"], w, Add);
                Within(s["ConferenceName"], Commas(s["City"], date), ". ", Add);
                break;
            case "Report":
            case "ElectronicSource":
                Lead(authors, s["Title"], w, Add);
                Add(Then(Commas(s["Publisher"], s["City"], date), "."));
                break;
            case "InternetSite":
            case "DocumentFromInternetSite":
                Lead(authors, s["Title"], w, Add);
                Add(Then(date, ". "));
                Add("[Online]." + (s["URL"] is { } url ? " Available: " + url + "." : "")
                    + (Accessed(s) is { } accessed ? " [" + w.Accessed + " " + accessed + "]." : ""));
                break;
            case "Patent":
                Add(People(s.Role("Inventor"), w) is { } inventors ? inventors + ", " : null);
                Add(s["Title"] is { } invention ? w.Open + invention + w.Close + ". " : null);
                Add(Then(Commas(Join(" ", s["CountryRegion"], "Patent", s["PatentNumber"]), date), "."));
                break;
            case "Case":
                Add(s["Title"] is { } title ? title + (date != null ? ", " : ". ") : null, italic: true);
                Add(Then(date, "."));
                break;
            case "Film":
                Work(s, "Director", w.Director, "Film", w, Add);
                Add(Then(Commas(s["ProductionCompany"], date), ". "));
                break;
            case "Interview":
                Work(s, "Interviewee", w.Interviewee, "Interview", w, Add);
                Add(Then(date, "."));
                break;
            case "SoundRecording":
                Work(s, "Composer", w.Composer, w.SoundRecording, w, Add);
                Add(Then(s["ProductionCompany"], ". "));
                Add(Then(date, "."));
                break;
            case "Performance":
                Work(s, "Writer", w.Writer, "Performance", w, Add);
                Add(Then(date, ". "));
                break;
            case "Art":
                Work(s, "Artist", w.Artist, "Art", w, Add);
                Add(Then(Commas(s["Institution"], date), ". "));
                break;
            default:
                Add(authors != null ? authors + ", " : null);
                Add(s["Title"] is { } work ? work + ", " : null, italic: true);
                Add(Then(Commas(Place(s), date), ". "));
                break;
        }
        return text;
    }

    /// <summary>The authors and a part's title in quotes, the comma that follows it inside them: "S. Krüger, "Assessment," ".</summary>
    static void Lead(string? authors, string? title, Words w, Action<string?, bool> add)
    {
        add(authors != null ? authors + ", " : null, false);
        add(title != null ? w.Open + title + "," + w.Close + " " : null, false);
    }

    /// <summary>
    /// What a part stands in, "in" and its title in italics ("in *Handbook of Teaching*"), then the rest after a comma and the entry's
    /// end ("Munich, Beck, 2017, pp. 101-120."); the rest alone where the part stands in nothing named.
    /// </summary>
    static void Within(string? container, string rest, string end, Action<string?, bool> add)
    {
        if (container == null)
        {
            add(Then(rest, end), false);
            return;
        }
        add("in ", false);
        add(container, true);
        add((rest.Length > 0 ? ", " + rest : "") + end, false);
    }

    /// <summary>A work's maker with the role's name, its title in italics and its kind: "A. Lee, Director, *The Classroom. *[Film]. ".</summary>
    static void Work(Source s, string role, string roleName, string kind, Words w, Action<string?, bool> add)
    {
        add(People(s.Role(role), w) is { } makers ? makers + ", " + roleName + ", " : null, false);
        add(s["Title"] is { } title ? title + ". " : null, true);
        add("[" + kind + "]. ", false);
    }

    /// <summary>The editors where there are any: "M. Weber, Ed.", "M. Weber and I. Klein, Eds.".</summary>
    static string? Editors(Source s, Words w) =>
        s.Role("Editor") is { } editors && People(editors, w) is { } names ? names + ", " + (editors.People.Count > 1 ? w.Editors : w.Editor) : null;

    /// <summary>A book's place and publisher: "Berlin: Springer", either alone where the other is missing.</summary>
    static string? Place(Source s) => s["City"] is { } city && s["Publisher"] is { } publisher ? city + ": " + publisher : s["City"] ?? s["Publisher"];

    static string? PagesOf(Source s) => s["Pages"] is { } pages ? Pages(pages) : null;

    /// <summary>A date as Word's IEEE writes it, of what the source gives: "14 March 2022", "May 2024", "2020".</summary>
    static string? Date(Source s) => Join(" ", s["Day"], s["Month"], s["Year"]) is { Length: > 0 } date ? date : null;

    static string? Accessed(Source s) =>
        Join(" ", s["DayAccessed"], s["MonthAccessed"], s["YearAccessed"]) is { Length: > 0 } date ? date : null;

    /// <summary>
    /// People as Word's IEEE names them, all of them however many: "J. Smith", "A. B. Miller and T. Brown", "K. Lee, M. Garcia and
    /// P. Novak"; a corporate name as it is.
    /// </summary>
    static string? People(Contributors? names, Words w)
    {
        if (names == null)
            return null;
        if (names.Corporate != null)
            return names.Corporate;
        var people = names.People.Select(p => Common.Initials(p) is { Length: > 0 } initials ? initials + " " + p.Last : p.Last).ToList();
        return people.Count == 1 ? people[0] : string.Join(", ", people.Take(people.Count - 1)) + " " + w.And + " " + people[^1];
    }

    /// <summary>The parts given, between commas.</summary>
    static string Commas(params string?[] parts) => Join(", ", parts);

    /// <summary>A part and what follows it ("2020" ". "); null where the part is missing.</summary>
    static string? Then(string? part, string end) => string.IsNullOrEmpty(part) ? null : part + end;

    static string Join(string separator, params string?[] parts) => string.Join(separator, parts.Where(p => !string.IsNullOrEmpty(p)));
}
