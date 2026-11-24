using System.Globalization;

namespace FreeSlate.Docx.Citations.Styles;

/// <summary>
/// Word's ISO 690 - First Element and Date style (see CitationStyle) as Word writes it: a citation is the first element (the authors'
/// first last name, "Miller, et al." for more than one; a journal's or a conference's article by its title) and the year in parentheses
/// ("(Smith, 2020 p. 33)", several between semicolons); the bibliography is sorted by the first element, each entry led by it and the
/// year in bold ("**Smith, John. 2020.**", an author's later entries by a dash), place and publisher as ISO 690 has them ("Berlin :
/// Springer", "s.l." and "s.n." for those missing). ISO 690 - Numerical Reference writes its entries alike (see Parts).
/// </summary>
public sealed class IsoDate : CitationStyle
{
    public override string Name => "ISO 690 - First Element and Date";

    public override string File => @"\ISO690.XSL";

    public override string? Version => null;

    public override IReadOnlyList<string> Aliases => ["ISO 690 - Erstes Element und Datum", "ISO 690", "ISO690"];

    /// <summary>
    /// A citation as Word writes it: each source's prefix, first element and year ("Smith, 2020"), its pages or volume and page after a
    /// space ("p. 33", "3:12"), its suffix; \n leaves out the first element, \y the year, \t nothing.
    /// </summary>
    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context)
    {
        var w = Words.Of(request.Lcid);
        var parts = request.Sources.Select(c =>
            c.Prefix + Join(" ", Join(", ", c.NoAuthor ? null : Short(c.Source), c.NoYear ? null : c.Source["Year"]), Locator(c, w, ":")) + c.Suffix);
        return [new Piece("(" + string.Join("; ", parts) + ")")];
    }

    /// <summary>The bibliography as Word writes it: every source sorted by its first element, then year and title (see Sorted).</summary>
    public override BibliographyList Bibliography(CitationContext context, int lcid)
    {
        var w = Words.Of(lcid);
        var entries = new List<BibliographyEntry>();
        string? previous = null;
        foreach (var source in Sorted(context.Sources.Sources, w))
        {
            var names = Names(source, w);
            var (lead, body) = Parts(source, w);
            var head = Join(" ", Ended(names != null && names == previous ? "—" : names), Ended(source["Year"]));
            previous = names;
            List<Piece> text = [.. lead];
            if (head.Length > 0)
                text.AddRange([new Piece(head, Bold: true), new Piece(" ")]);
            text.AddRange(body);
            entries.Add(new BibliographyEntry(text));
        }
        return new BibliographyList(entries, BibliographyLayout.Plain);
    }

    /// <summary>
    /// The sources in the order of Word's ISO 690 bibliography: by the first element as the entry writes it (an article of a journal or a
    /// conference, a source without one by its title), then year and title, letters' case aside.
    /// </summary>
    internal static IEnumerable<Source> Sorted(IEnumerable<Source> sources, Words w)
    {
        var comparer = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);
        return sources.OrderBy(s => (TitleFirst(s) ? s["Title"] : null) ?? Names(s, w) ?? s["Title"] ?? "", comparer)
            .ThenBy(s => s["Year"] ?? "", comparer).ThenBy(s => s["Title"] ?? "", comparer);
    }

    /// <summary>
    /// The words of Word's ISO 690 styles in English or German (German's "S." for one page and several, its dates "14. March 2022"), the
    /// marks of contributors' roles in brackets as Word writes them whatever their number ("[ed.]" for one editor and for two).
    /// </summary>
    internal sealed record Words(bool GermanDates, string And, string Editor, string Translator, string Interviewer, string BookAuthor,
        string Performer, string Composer, string Director, string Volume, string Page, string Pages, string Cited)
    {
        public static Words English { get; } = new(false, "and", "[ed.]", "[trans.]", "[interv.]", "[book auth.]", "[perf.]", "[comps.]",
            "[dir.]", "Vol.", "p.", "pp.", "Cited");

        public static Words German { get; } = new(true, "und", "[Hrsg.]", "[Übers.]", "[Befragte Person]", "[Buchverf.]", "[Interpr.]",
            "[Komp.]", "[Reg.]", "Bd.", "S.", "S.", "Zitat vom");

        public static Words Of(int lcid) => Common.German(lcid) ? German : English;

        /// <summary>A date of what is given, as Word writes it: "March 14, 2022" (German "14. March 2022"), "May 2024", "2020".</summary>
        public string? Date(string? day, string? month, string? year)
        {
            if (month == null)
                day = null;
            var text = GermanDates
                ? Join(" ", day != null ? day + "." : null, month, year)
                : Join(day != null ? ", " : " ", Join(" ", month, day), year);
            return text.Length > 0 ? text : null;
        }

        /// <summary>Pages with their word: "p. 33", "pp. 46-48" (German "S. 33", "S. 46-48").</summary>
        public string PagesOf(string pages) => (pages.IndexOfAny(['-', '–', '—', ',']) >= 0 ? Pages : Page) + " " + pages;
    }

    /// <summary>
    /// A citation's locator as Word writes it: the volume and the pages ("3:12"; ISO 690 - Numerical Reference "3: 12" with its
    /// separator), else the pages with their word ("p. 33"); null where the citation gives neither.
    /// </summary>
    internal static string? Locator(Cited cited, Words w, string separator)
    {
        if (cited.Volume is { Length: > 0 } volume)
            return cited.Pages is { Length: > 0 } pages ? volume + separator + pages : volume;
        return cited.Pages is { Length: > 0 } page ? w.PagesOf(page) : null;
    }

    /// <summary>Whether Word's ISO 690 leads a source's entry with its title: a journal's and a conference's articles.</summary>
    static bool TitleFirst(Source s) => s.Type is "JournalArticle" or "ConferenceProceedings";

    /// <summary>
    /// The people a source's first element names and the mark Word writes after them: the authors (a film's director, an interview's
    /// interviewee, a patent's inventor, a performance's writer, a work of art's artist), a recording's performers ("[perf.]"), a book
    /// without authors its editors ("[ed.]"); a case none.
    /// </summary>
    static (Contributors? Names, string? Mark) First(Source s, Words w) => s.Type switch
    {
        "Case" => (null, null),
        "Film" => (s.Role("Director"), null),
        "Interview" => (s.Role("Interviewee"), null),
        "Patent" => (s.Role("Inventor"), null),
        "SoundRecording" => (s.Role("Performer"), w.Performer),
        "Performance" => (s.Role("Writer"), null),
        "Art" => (s.Role("Artist"), null),
        "Book" when s.Role("Author") == null && s.Role("Editor") != null => (s.Role("Editor"), w.Editor),
        _ => (s.Role("Author"), null),
    };

    /// <summary>
    /// The first element's names as the bibliography writes them: "Smith, John", "Miller, Anna B. and Brown, Tom", "Lee, Kim, Garcia,
    /// Maria and Novak, Petr", more than three the first and "et al." ("Adams, Ruth, et al."), a mark after them ("Weber, Max, [ed.]");
    /// null where there are none.
    /// </summary>
    internal static string? Names(Source s, Words w)
    {
        var (names, mark) = First(s, w);
        if (names == null)
            return null;
        var text = names.Corporate
                   ?? List(names.People.Select(p => Common.Given(p) is { Length: > 0 } given ? p.Last + ", " + given : p.Last).ToList(), w);
        return mark != null ? text + ", " + mark : text;
    }

    /// <summary>The first element as a citation writes it: the title (see TitleFirst), else "Smith", "Miller, et al.", a corporate name.</summary>
    static string? Short(Source s)
    {
        if (TitleFirst(s) && s["Title"] is { } title)
            return title;
        var (names, _) = First(s, Words.English);
        if (names == null)
            return null;
        return names.Corporate ?? names.People[0].Last + (names.People.Count > 1 ? ", et al." : "");
    }

    /// <summary>Names in a list: "A and B", "A, B and C", more than three the first and "et al." ("A, et al.").</summary>
    static string List(IReadOnlyList<string> names, Words w) => names.Count switch
    {
        1 => names[0],
        > 3 => names[0] + ", et al.",
        _ => string.Join(", ", names.Take(names.Count - 1)) + " " + w.And + " " + names[^1],
    };

    /// <summary>Other contributors after their role's mark, first names first: "[ed.] Max Weber and Ina Klein. ".</summary>
    static string? Others(string mark, Contributors? names, Words w) => names == null
        ? null
        : mark + " " + (names.Corporate ?? List(names.People.Select(p => Join(" ", Common.Given(p), p.Last)).ToList(), w)) + ". ";

    /// <summary>
    /// An entry's parts besides its first element, as Word's ISO 690 styles write them by the source's kind: the lead before it (a
    /// journal's or a conference's article's title) and the body after it, which follows the first element after a space. Each part
    /// ends with its period; place and publisher as "City : Publisher, Year", "s.l." and "s.n." where the city or the publisher is missing.
    /// </summary>
    internal static (List<Piece> Lead, List<Piece> Body) Parts(Source s, Words w)
    {
        var lead = new List<Piece>();
        var body = new List<Piece>();
        void Add(string? text, bool italic = false)
        {
            if (!string.IsNullOrEmpty(text))
                body.Add(new Piece(text, italic));
        }
        var title = Dot(s["Title"]);
        var date = w.Date(s["Day"], s["Month"], s["Year"]);
        var pages = s["Pages"] is { } given ? w.PagesOf(given) : null;
        string Published(string? publisher) => (s["City"] ?? "s.l.") + Colon + (publisher ?? "s.n.") + (date != null ? ", " + date : "");
        switch (s.Type)
        {
            case "Book":
                Add(title, italic: true);
                if (First(s, w).Mark == null)
                    Add(Others(w.Editor, s.Role("Editor"), w));
                Add(Others(w.Translator, s.Role("Translator"), w));
                Add(Dot(s["Edition"]));
                Add(Ended(Published(s["Publisher"])));
                Add(s["Volume"] is { } volume ? " " + w.Volume + " " + volume + "." : null);
                break;
            case "BookSection":
                Add(title);
                Add(Others(w.BookAuthor, s.Role("BookAuthor"), w));
                Add(Others(w.Editor, s.Role("Editor"), w));
                Add(Others(w.Translator, s.Role("Translator"), w));
                Add(Dot(s["BookTitle"]), italic: true);
                Add(Ended(Join(", ", Published(s["Publisher"]), pages)));
                break;
            case "JournalArticle":
                if (title != null)
                    lead.Add(new Piece(title, Italic: true));
                Add(Then(Join(", ", s["Issue"], date, s["JournalName"], s["Volume"] is { } volume2 ? w.Volume + " " + volume2 : null, pages), "."));
                break;
            case "ArticleInAPeriodical":
                Add(title);
                Add(Dot(s["PeriodicalTitle"]), italic: true);
                Add(Then(Join(", ", date, pages), "."));
                break;
            case "ConferenceProceedings":
                if (title != null)
                    lead.Add(new Piece(title, Italic: true));
                Add(Join(" ", Ended(Published(s["Publisher"])), Then(s["ConferenceName"], "."), Then(pages, ".")));
                break;
            case "Report":
                Add(title, italic: true);
                Add(Dot(s["Institution"]));
                Add(Join(" ", Ended(Published(s["Publisher"])), Then(s["ThesisType"], "."), Then(s["StandardNumber"], ".")));
                break;
            case "InternetSite":
            case "DocumentFromInternetSite":
                Add(title);
                Add(Dot(s["InternetSiteTitle"]), italic: true);
                Add(Join(" ", "[Online]", Then(date, "."),
                    w.Date(s["DayAccessed"], s["MonthAccessed"], s["YearAccessed"]) is { } accessed ? "[" + w.Cited + ": " + accessed + ".]" : null,
                    Then(s["URL"], ".")));
                break;
            case "Film":
                Add(title, italic: true);
                Add(Then(Join(", ", Join("; ", s["ProductionCompany"], s["Distributor"]), date), "."));
                break;
            case "Interview":
                Add(title);
                Add(Others(w.Interviewer, s.Role("Interviewer"), w));
                Add(Ended(Published(s["Broadcaster"])));
                break;
            case "Patent":
                Add(title + Then(s["PatentNumber"], " "), italic: true);
                Add(Then(Join(", ", s["CountryRegion"], date), "."));
                break;
            case "Case":
                Add(title, italic: true);
                Add(Ended(Join(", ", s["CaseNumber"], Published(s["Court"]))));
                Add(Then(s["Reporter"], ".") is { } reporter ? " " + reporter : null);
                break;
            case "SoundRecording":
                Add(title);
                Add(Others(w.Composer, s.Role("Composer"), w));
                Add(Dot(s["AlbumTitle"]), italic: true);
                Add(s["Medium"] is { } medium ? "[" + medium + "] " : null);
                Add(Ended(Published(s["ProductionCompany"])));
                break;
            case "Performance":
                Add(title, italic: true);
                Add(Others(w.Director, s.Role("Director"), w));
                Add(Ended(Join(", ", s["Theater"], Published(s["Publisher"]))));
                break;
            case "Art":
                Add(title, italic: true);
                Add(Then(Join(", ", s["Institution"], date != null ? (s["City"] ?? "s.l.") + Colon + date : s["City"]), "."));
                break;
            case "ElectronicSource":
                Add(title, italic: true);
                Add(s["Medium"] is { } medium2 ? "[" + medium2 + "] " : null);
                Add(Ended(Published(s["Publisher"])));
                break;
            default:
                Add(title);
                Add(Ended(Published(s["Publisher"])));
                break;
        }
        return (lead, body);
    }

    /// <summary>The colon between place and publisher as Word's ISO 690 writes it, a no-break space before it ("Berlin : Springer").</summary>
    const string Colon = "\u00A0: ";

    /// <summary>A part with its period and the space after it ("Learning with Machines. "); null where it is missing.</summary>
    static string? Dot(string? text) => Then(text, ". ");

    /// <summary>A part and what follows it ("2020" "."); null where the part is missing.</summary>
    static string? Then(string? part, string end) => string.IsNullOrEmpty(part) ? null : part + end;

    /// <summary>
    /// Names, a year or place and publisher with the period that ends them, none added to one they end with ("Adams, Ruth, et al.",
    /// "Rome : s.n."); null where missing.
    /// </summary>
    internal static string? Ended(string? text) => string.IsNullOrEmpty(text) ? null : text.EndsWith('.') ? text : text + ".";

    /// <summary>The parts given, between the separator.</summary>
    internal static string Join(string separator, params string?[] parts) => string.Join(separator, parts.Where(p => !string.IsNullOrEmpty(p)));
}
