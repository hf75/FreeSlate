namespace FreeSlate.Docx.Citations.Styles;

/// <summary>
/// Word's SIST02 style (see CitationStyle), the Japanese standard, as Word writes it: full-width punctuation ("（", "，", "．", "）"),
/// people's names as last and first names without a space ("SmithJohn"); a citation is the first element (the authors' first last name,
/// "Milleret al." for more than one; a journal's or a conference's article by its title) and the year ("（Smith，2020）"), several in one
/// pair of parentheses without anything between them; the bibliography is sorted as ISO 690's (IsoDate.Sorted), each entry led by its
/// first element in bold, its parts as Word's ISO 690 has them with SIST02's punctuation.
/// </summary>
public sealed class Sist02 : CitationStyle
{
    public override string Name => "SIST02";

    public override string File => @"\SIST02.XSL";

    public override string? Version => null;

    public override IReadOnlyList<string> Aliases => ["SIST 02"];

    /// <summary>
    /// A citation as Word writes it: each source's prefix, first element and year ("Smith，2020"; the source's tag where it has neither),
    /// its pages ("p.33", "pp.46-48") or volume and page ("3，12") and its suffix; \n leaves out the first element, \y the year.
    /// </summary>
    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context)
    {
        var w = Words.Of(request.Lcid);
        var parts = request.Sources.Select(c =>
        {
            var main = Short(c.Source) == null && c.Source["Year"] == null
                ? c.Source.Tag
                : Join("，", c.NoAuthor ? null : Short(c.Source), c.NoYear ? null : c.Source["Year"]);
            var locator = c.Volume is { Length: > 0 } volume
                ? volume + (c.Pages is { Length: > 0 } page ? "，" + page : "")
                : c.Pages is { Length: > 0 } pages ? w.PagesOf(pages) : null;
            return c.Prefix + main + locator + c.Suffix;
        });
        return [new Piece("（" + string.Concat(parts) + "）")];
    }

    /// <summary>
    /// The bibliography as Word writes it: every source in ISO 690's order, each entry's first element in bold (an author's later entries a
    /// dash, "—．"); a space stands between two neighbouring parts in bold or italics, none between a part and plain text.
    /// </summary>
    public override BibliographyList Bibliography(CitationContext context, int lcid)
    {
        var w = Words.Of(lcid);
        var entries = new List<BibliographyEntry>();
        string? previous = null;
        foreach (var source in IsoDate.Sorted(context.Sources.Sources, IsoDate.Words.Of(lcid)))
        {
            var names = Names(source, w);
            var (lead, body) = Parts(source, w);
            List<Piece> parts = [.. lead];
            if (names != null)
                parts.Add(new Piece(names == previous ? "—．" : names, Bold: true));
            parts.AddRange(body);
            previous = names;
            var text = new List<Piece>();
            foreach (var part in parts)
            {
                if (text.Count > 0 && Marked(text[^1]) && Marked(part))
                    text.Add(new Piece(" "));
                text.Add(part);
            }
            entries.Add(new BibliographyEntry(text));
        }
        return new BibliographyList(entries, BibliographyLayout.Plain);
    }

    static bool Marked(Piece piece) => piece.Bold || piece.Italic;

    /// <summary>
    /// The words of Word's SIST02 in English or German ("Vol." "Bd.", "p." and "pp." "S."; dates "14 March 2022" in both), the marks of
    /// contributors' roles as Word writes them whatever their number ("ed." for one editor and for two).
    /// </summary>
    sealed record Words(string Editor, string Translator, string Interviewer, string BookAuthor, string Performer, string Composer, string Director,
        string Volume, string Page, string Pages, string Cited)
    {
        public static Words English { get; } = new("ed.", "trans.", "interv.", "book auth.", "perf.", "comps.", "dir.", "Vol.", "p.", "pp.", "Cited");

        public static Words German { get; } = new("Hrsg.", "Übers.", "Befragte Person", "Buchverf.", "Interpr.", "Komp.", "Reg.", "Bd.", "S.", "S.",
            "Zitat vom");

        public static Words Of(int lcid) => Common.German(lcid) ? German : English;

        /// <summary>Pages with their word, no space between: "p.33", "pp.46-48" (German "S.33", "S.46-48").</summary>
        public string PagesOf(string pages) => (pages.IndexOfAny(['-', '–', '—', ',']) >= 0 ? Pages : Page) + pages;
    }

    /// <summary>Whether Word leads a source's entry with its title: a journal's and a conference's articles.</summary>
    static bool TitleFirst(Source s) => s.Type is "JournalArticle" or "ConferenceProceedings";

    /// <summary>
    /// The people a source's first element names and the mark Word writes after them: the authors (a film's director, an interview's
    /// interviewee, a patent's inventor, a performance's writer, a work of art's artist), a recording's performers ("perf."), a book
    /// without authors its editors ("ed."); a case none.
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

    /// <summary>A person as SIST02 writes one: last, first and middle names without spaces between ("MillerAnnaB.").</summary>
    static string Person(Person p) => p.Last + p.First + p.Middle;

    /// <summary>
    /// People as SIST02 lists them: "WangLi, FischerEva", more than three the first and "et al." ("AdamsRuthet al."); a corporate name as
    /// it is.
    /// </summary>
    static string People(Contributors names) =>
        names.Corporate ?? (names.People.Count > 3 ? Person(names.People[0]) + "et al." : string.Join(", ", names.People.Select(Person)));

    /// <summary>
    /// The first element's names as the bibliography writes them, the mark after them in brackets ("WeberMax，[ed.]"); null where there
    /// are none.
    /// </summary>
    static string? Names(Source s, Words w)
    {
        var (names, mark) = First(s, w);
        return names == null ? null : People(names) + (mark != null ? "，[" + mark + "]" : "");
    }

    /// <summary>The first element as a citation writes it: the title (see TitleFirst), else "Smith", "Milleret al.", a corporate name.</summary>
    static string? Short(Source s)
    {
        if (TitleFirst(s) && s["Title"] is { } title)
            return title;
        var (names, _) = First(s, Words.English);
        if (names == null)
            return null;
        return names.Corporate ?? names.People[0].Last + (names.People.Count > 1 ? "et al." : "");
    }

    /// <summary>Other contributors with their role's mark after them: "WeberMax, KleinInaed.．".</summary>
    static string? Others(string mark, Contributors? names) => names == null ? null : People(names) + mark + "．";

    /// <summary>
    /// An entry's parts besides its first element as Word's SIST02 writes them by the source's kind: the lead before it (a journal's or a
    /// conference's article's title) and the body after it. Each part ends with "．"; place, publisher and date between "，" ("Berlin，
    /// Springer，2020．"), "s.l." and "s.n." where the city or the publisher is missing.
    /// </summary>
    static (List<Piece> Lead, List<Piece> Body) Parts(Source s, Words w)
    {
        var lead = new List<Piece>();
        var body = new List<Piece>();
        void Add(string? text, bool italic = false)
        {
            if (!string.IsNullOrEmpty(text))
                body.Add(new Piece(text, italic));
        }
        var title = Dot(s["Title"]);
        var date = Date(s["Day"], s["Month"], s["Year"]);
        var pages = s["Pages"] is { } given ? w.PagesOf(given) : null;
        string Published(string? publisher, params string?[] after) =>
            Join("，", [s["City"] ?? "s.l.", publisher ?? "s.n.", .. after]) + "．";
        switch (s.Type)
        {
            case "Book":
                Add(title, italic: true);
                if (First(s, w).Mark == null)
                    Add(Others(w.Editor, s.Role("Editor")));
                Add(Others(w.Translator, s.Role("Translator")));
                Add(Published(s["Publisher"], s["Edition"], date, s["Volume"] is { } volume ? w.Volume + " " + volume : null));
                break;
            case "BookSection":
                Add(title);
                Add(Others(w.BookAuthor, s.Role("BookAuthor")));
                Add(Others(w.Editor, s.Role("Editor")));
                Add(Others(w.Translator, s.Role("Translator")));
                Add(Dot(s["BookTitle"]), italic: true);
                Add(Published(s["Publisher"], date, pages));
                break;
            case "JournalArticle":
                if (title != null)
                    lead.Add(new Piece(title, Italic: true));
                Add(Dot(Join("，", s["Issue"], date, s["JournalName"], s["Volume"] is { } volume2 ? w.Volume + " " + volume2 : null, pages)));
                break;
            case "ArticleInAPeriodical":
                Add(title);
                Add(Dot(s["PeriodicalTitle"]), italic: true);
                Add(Dot(Join("，", date, pages)));
                break;
            case "ConferenceProceedings":
                if (title != null)
                    lead.Add(new Piece(title, Italic: true));
                Add(Published(s["Publisher"], date) + Dot(s["ConferenceName"]) + Dot(pages));
                break;
            case "Report":
                Add(title, italic: true);
                Add(Dot(s["Institution"]));
                Add(Published(s["Publisher"], date) + Dot(s["ThesisType"]) + (s["StandardNumber"] is { } number ? "（" + number + "）．" : null));
                break;
            case "InternetSite":
            case "DocumentFromInternetSite":
            {
                Add(title);
                Add(Dot(s["InternetSiteTitle"]), italic: true);
                var accessed = Date(s["DayAccessed"], s["MonthAccessed"], s["YearAccessed"]);
                Add("[Online]" + Dot(date) + (accessed != null ? "[" + w.Cited + ": " + accessed + "．]" : null) + Dot(s["URL"]));
                break;
            }
            case "Film":
                Add(title, italic: true);
                Add(Dot(Join("，", s["ProductionCompany"] + s["Distributor"], date)));
                break;
            case "Interview":
                Add(title);
                Add(Others(w.Interviewer, s.Role("Interviewer")));
                Add(Published(s["Broadcaster"], date));
                break;
            case "Patent":
                Add(title, italic: true);
                Add(s["PatentNumber"], italic: true);
                Add(Dot(Join("，", s["CountryRegion"], date)));
                break;
            case "Case":
                Add(title, italic: true);
                Add(Join("，", s["CaseNumber"], Published(s["Court"], date)) + Dot(s["Reporter"]));
                break;
            case "SoundRecording":
                Add(title);
                Add(Others(w.Composer, s.Role("Composer")));
                Add(Dot(s["AlbumTitle"]), italic: true);
                Add(s["Medium"] is { } medium ? "[" + medium + "]" : null);
                Add(Published(s["ProductionCompany"], date));
                break;
            case "Performance":
                Add(title, italic: true);
                Add(Others(w.Director, s.Role("Director")));
                Add(Join("，", s["Theater"], Published(s["Publisher"], date)));
                break;
            case "Art":
                Add(title, italic: true);
                Add(Dot(Join("，", s["Institution"], s["City"] ?? (date != null ? "s.l." : null), date)));
                break;
            case "ElectronicSource":
                Add(title, italic: true);
                Add(s["Medium"] is { } medium2 ? "[" + medium2 + "]" : null);
                Add(Published(s["Publisher"], date));
                break;
            default:
                Add(title);
                Add(Published(s["Publisher"], date));
                break;
        }
        return (lead, body);
    }

    /// <summary>A part with its full-width period ("Learning with Machines．"); null where it is missing.</summary>
    static string? Dot(string? text) => string.IsNullOrEmpty(text) ? null : text + "．";

    /// <summary>A date of what is given, as Word's SIST02 writes it in both languages: "14 March 2022", "May 2024", "2020".</summary>
    static string? Date(string? day, string? month, string? year) =>
        Join(" ", month != null ? day : null, month, year) is { Length: > 0 } date ? date : null;

    /// <summary>The parts given, between the separator.</summary>
    static string Join(string separator, params string?[] parts) => string.Join(separator, parts.Where(p => !string.IsNullOrEmpty(p)));
}
