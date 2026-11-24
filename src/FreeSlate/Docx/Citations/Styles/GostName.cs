using System.Globalization;

namespace FreeSlate.Docx.Citations.Styles;

/// <summary>
/// Word's GOST - Name Sort style (see CitationStyle) as Word formats it: citations as <see cref="Gost"/> writes them, the bibliography
/// sorted by who the sources are by, each entry beginning with them in bold ("<b>Smith John</b> Learning with Machines [Book]. - Berlin :
/// Springer, 2020.").
/// </summary>
public sealed class GostName : CitationStyle
{
    public override string Name => "GOST - Name Sort";

    public override string File => @"\GostName.XSL";

    public override string? Version => null;

    public override IReadOnlyList<string> Aliases => ["Gost - Namenssortierung", "GOST"];

    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context) => Gost.Cite(request);

    public override BibliographyList Bibliography(CitationContext context, int lcid) => Gost.Bibliography(context, lcid, byTitle: false);
}

/// <summary>
/// What Word's two GOST styles (GOST - Name Sort, GOST - Title Sort) share. A citation names a source by its creators and year ("(Smith,
/// 2020 p. 33)", two or more "Wang, et al."); a source with neither shows its tag ("(Beo)", a page glued to it as Word does: "(Beop.
/// 12)"). An entry is a bibliographic description after GOST 7.1 as Word writes it, its areas separated by ". - " (a no-break space
/// before the dash, as before every ":", ";", "/", "//" and "="): the title with the kind of source in brackets ("[Book]"), statements
/// of who made it ("/ auth. Smith John", "; dir. Cohen Ruth"), the work it is part of ("// Journal of Learning"), then place, publisher
/// and year ("Berlin : Springer, 2020", "[s.l.]" and "[s.n.]" standing in), dates, numbers, pages. Name Sort begins an entry with its
/// creators in bold and leaves them out of the statements, Title Sort begins it with its title in bold. German as Word translates it
/// ("[Buch]", "Verf.", "Hrsg.", "und", "Bd.", "S.", dates "14. March 2022"), the months as the source has them.
/// </summary>
static class Gost
{
    /// <summary>A no-break space, as Word sets it before a punctuation of the description ("Berlin : Springer", ". - ").</summary>
    const string Space = "\u00A0";

    /// <summary>The words the GOST styles write in one language: the kinds of source, the contributors' roles, labels.</summary>
    sealed record Words(bool German, string And, string Page, string Pages, string Volume, IReadOnlyDictionary<string, string> Kinds,
        IReadOnlyDictionary<string, string> Roles);

    static readonly Words InEnglish = new(false, "and", "p.", "pp.", "Vol.",
        new Dictionary<string, string>
        {
            ["Book"] = "Book", ["BookSection"] = "Book Section", ["JournalArticle"] = "Journal", ["ArticleInAPeriodical"] = "Article",
            ["ConferenceProceedings"] = "Conference", ["Report"] = "Report", ["InternetSite"] = "Online", ["DocumentFromInternetSite"] = "Online",
            ["Interview"] = "Interview", ["Patent"] = "Patent", ["Case"] = "Case", ["SoundRecording"] = "Sound Recording",
            ["Performance"] = "Performance", ["Art"] = "Art",
        },
        new Dictionary<string, string>
        {
            ["Author"] = "auth.", ["Editor"] = "ed.", ["Translator"] = "trans.", ["BookAuthor"] = "book auth.", ["Director"] = "dir.",
            ["Writer"] = "writ.", ["Composer"] = "comps.", ["Performer"] = "perf.", ["Interviewee"] = "interv.", ["Inventor"] = "invent.",
            ["Artist"] = "art.",
        });

    static readonly Words InGerman = new(true, "und", "S.", "S.", "Bd.",
        new Dictionary<string, string>
        {
            ["Book"] = "Buch", ["BookSection"] = "Buchabschnitt", ["JournalArticle"] = "Journal", ["ArticleInAPeriodical"] = "Artikel",
            ["ConferenceProceedings"] = "Konferenz", ["Report"] = "Bericht", ["InternetSite"] = "Online", ["DocumentFromInternetSite"] = "Online",
            ["Interview"] = "Interview", ["Patent"] = "Patent", ["Case"] = "Fall", ["SoundRecording"] = "Tonaufnahme",
            ["Performance"] = "Aufführung", ["Art"] = "Kunst",
        },
        new Dictionary<string, string>
        {
            ["Author"] = "Verf.", ["Editor"] = "Hrsg.", ["Translator"] = "Übers.", ["BookAuthor"] = "Buchverf.", ["Director"] = "Reg.",
            ["Writer"] = "Aut.", ["Composer"] = "Komp.", ["Performer"] = "Interpr.", ["Interviewee"] = "Befragte Person", ["Inventor"] = "Erfinder",
            ["Artist"] = "Interpret",
        });

    static Words In(int lcid) => Common.German(lcid) ? InGerman : InEnglish;

    /// <summary>Sources' names, titles and years compared as Word sorts them: letters' case aside.</summary>
    static readonly StringComparer Sorting = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);

    /// <summary>
    /// A citation as Word writes it: each source's prefix, creators and year joined by a comma, its place after a space (volume and page
    /// "3:12", a page "p. 33", pages "pp. 46-48", German "S."), then its suffix; several sources joined by semicolons in one pair of
    /// parentheses.
    /// </summary>
    public static IReadOnlyList<Piece> Cite(CitationRequest request)
    {
        var words = In(request.Lcid);
        return [new Piece("(" + string.Join("; ", request.Sources.Select(c => SourceText(c, words))) + ")")];
    }

    static string SourceText(Cited cited, Words words)
    {
        var source = cited.Source;
        var first = Short(source);
        var year = source["Year"];
        var parts = new List<string>();
        if (!cited.NoAuthor && first != null)
            parts.Add(first);
        if (!cited.NoYear && year != null)
            parts.Add(year);
        var shown = string.Join(", ", parts);
        // Without creators and a year Word shows the source's tag, and the place follows it without a space.
        var text = first == null && year == null ? source.Tag : shown;
        if (Locator(cited, words) is { } locator)
            text += (shown.Length > 0 ? " " : "") + locator;
        return cited.Prefix + text + cited.Suffix;
    }

    /// <summary>Where a citation points: "3:12" (volume and page), "p. 33", "pp. 46-48" (German "S."), a volume alone as given.</summary>
    static string? Locator(Cited cited, Words words)
    {
        var volume = string.IsNullOrEmpty(cited.Volume) ? null : cited.Volume;
        var pages = string.IsNullOrEmpty(cited.Pages) ? null : cited.Pages;
        if (volume != null)
            return pages != null ? volume + ":" + pages : volume;
        return pages != null ? Paged(pages, words) : null;
    }

    /// <summary>Pages labelled: "p. 12", "pp. 45-67" where they are a range or a list (German "S." for both).</summary>
    static string Paged(string pages, Words words) => (pages.IndexOfAny(['-', '–', '—', ',']) >= 0 ? words.Pages : words.Page) + " " + pages;

    /// <summary>
    /// Who a source is by as GOST names it first, and in which role: its authors (a book with none its editors), a film's director, an
    /// interview's interviewee, a patent's inventor, a sound recording's performers (else its composers), a performance's writer, a work of
    /// art's artist; no one where it names none.
    /// </summary>
    static (Contributors? Names, string? Role) Creator(Source source) => source.Type switch
    {
        "Film" => (source.Role("Director"), "Director"),
        "Interview" => (source.Role("Interviewee"), "Interviewee"),
        "Patent" => (source.Role("Inventor"), "Inventor"),
        "SoundRecording" => source.Role("Performer") is { } performers ? (performers, "Performer") : (source.Role("Composer"), "Composer"),
        "Performance" => (source.Role("Writer"), "Writer"),
        "Art" => (source.Role("Artist"), "Artist"),
        _ => source.Role("Author") is { } author ? (author, "Author")
            : source.Type == "Book" && source.Role("Editor") is { } editors ? (editors, "Editor") : (null, null),
    };

    /// <summary>A citation's names: a last name, two or more "Wang, et al.", a corporate name; null where the source names none.</summary>
    static string? Short(Source source)
    {
        var (names, _) = Creator(source);
        if (names == null)
            return null;
        if (names.Corporate != null)
            return names.Corporate;
        return names.People.Count == 1 ? names.People[0].Last : names.People[0].Last + ", et al.";
    }

    /// <summary>
    /// Names as GOST lists them: last name before first and middle names ("Miller Anna B."), joined ("A and B", "A, B and C"); more than
    /// three the first and "[et al.]"; a corporate name as it is.
    /// </summary>
    static string Names(Contributors names, Words words)
    {
        if (names.Corporate != null)
            return names.Corporate;
        static string Name(Person p) => Common.Given(p) is { Length: > 0 } given ? p.Last + " " + given : p.Last;
        var people = names.People;
        if (people.Count > 3)
            return Name(people[0]) + " [et al.]";
        return people.Count <= 1 ? string.Concat(people.Select(Name))
            : string.Join(", ", people.Take(people.Count - 1).Select(Name)) + " " + words.And + " " + Name(people[^1]);
    }

    /// <summary>
    /// The bibliography: every source, sorted by its creators (the title where it names none) or by its title, then its year and title or
    /// creators; plain paragraphs.
    /// </summary>
    public static BibliographyList Bibliography(CitationContext context, int lcid, bool byTitle)
    {
        var words = In(lcid);
        string Heading(Source s) => Creator(s).Names is { } names ? Names(names, words) : "";
        var sources = context.Sources.Sources;
        var sorted = byTitle
            ? sources.OrderBy(s => s["Title"] ?? "", Sorting).ThenBy(Heading, Sorting).ThenBy(s => s["Year"] ?? "", Sorting)
            : sources.OrderBy(s => Creator(s).Names != null ? Heading(s) : s["Title"] ?? "", Sorting).ThenBy(s => s["Year"] ?? "", Sorting)
                .ThenBy(s => s["Title"] ?? "", Sorting);
        return new BibliographyList([.. sorted.Select(s => new BibliographyEntry(Entry(s, words, byTitle)))], BibliographyLayout.Plain);
    }

    /// <summary>
    /// One source's entry: its creators in bold and its title (Name Sort; the title alone where it names no one) or its title in bold
    /// (Title Sort), then the rest of its description as its kind has it.
    /// </summary>
    static List<Piece> Entry(Source source, Words words, bool byTitle)
    {
        var (names, role) = Creator(source);
        var title = source["Title"] ?? "";
        if (byTitle)
            return [new Piece(title, Bold: true), new Piece(Description(source, words, null))];
        if (names == null)
            return [new Piece(title + Description(source, words, null))];
        return [new Piece(Names(names, words), Bold: true), new Piece(" " + title + Description(source, words, role))];
    }

    /// <summary>
    /// What follows a title in an entry, as Word writes each kind of source: the kind in brackets, statements of who made it (but the
    /// role <paramref name="heading"/> names, its people heading the entry), the work it is part of, then its areas each after ". - ".
    /// </summary>
    static string Description(Source source, Words words, string? heading)
    {
        var year = source["Year"];
        var city = source["City"];
        var publisher = source["Publisher"];
        var date = Date(year, source["Month"], source["Day"], words);
        var label = words.Kinds.GetValueOrDefault(source.Type) ?? (source.Type is "Film" or "ElectronicSource" or "Misc" ? source["Medium"] : null);
        var kind = label != null ? " [" + label + "]" : "";
        // A statement of who made it: "/ auth. Smith John", several roles "/ writ. Miller Arthur ; dir. Cohen Ruth".
        string Made(params string[] roles)
        {
            var made = roles.Where(r => r != heading && source.Role(r) != null).Select(r => words.Roles[r] + " " + Names(source.Role(r)!, words)).ToList();
            return made.Count > 0 ? Space + "/ " + string.Join(Space + "; ", made) : "";
        }
        string Part(string mark, string? value) => value != null ? Space + mark + " " + value : "";
        // A sound recording's composers: "/ comps. Glass Philip" (Title Sort); Name Sort writes them straight after the medium, without the "/".
        string Composers() => heading == null ? Made("Composer")
            : heading != "Composer" && source.Role("Composer") is { } composers ? words.Roles["Composer"] + " " + Names(composers, words) : "";
        var published = Published(city, publisher, year);
        var pages = source["Pages"] is { } p ? Paged(p, words) : null;
        return source.Type switch
        {
            "Book" => kind + Made("Author", "Editor") + Made("Translator")
                + Areas(published, source["Edition"], source["Volume"] is { } volume ? words.Volume + " " + volume + Part(":", source["NumberVolumes"]) : null),
            "BookSection" => kind + Made("Author") + Part("//", source["BookTitle"]) + Made("BookAuthor", "Editor") + Made("Translator") + Areas(published),
            "JournalArticle" => kind + Made("Author") + Part("//", source["JournalName"]) + Areas(date, Issued(source, words), pages),
            "ArticleInAPeriodical" => kind + Made("Author") + Part("//", source["PeriodicalTitle"]) + Areas(date, pages),
            "ConferenceProceedings" => kind + Made("Author") + Part("//", source["ConferenceName"]) + Areas(published, pages),
            "Report" => kind + Part(":", source["ThesisType"]) + Made("Author") + Part("/", source["Institution"]) + Areas(published, source["StandardNumber"]),
            "InternetSite" or "DocumentFromInternetSite" => kind + Made("Author") + Part("//", source["InternetSiteTitle"])
                + Areas(date, Date(source["YearAccessed"], source["MonthAccessed"], source["DayAccessed"], words), source["URL"]),
            "Film" => kind + Made("Director") + Areas(Listed(", ", Listed(Space + ": ", source["ProductionCompany"], source["Distributor"]), year)),
            "Interview" => kind + Made("Interviewee")
                + Areas(Published(city, Listed(Space + "; ", source["Broadcaster"], source["Station"]), date)),
            "Patent" => kind + Part(":", source["PatentNumber"]) + Made("Inventor") + Areas(Listed(", ", source["CountryRegion"], date)),
            "Case" => kind + Part("=", source["Reporter"]) + Part(":", source["CaseNumber"]) + Made("Author") + Areas(Published(city, source["Court"], year)),
            "SoundRecording" => kind + Part(":", source["Medium"]) + Composers() + Part("//", source["AlbumTitle"]) + Made("Performer")
                + Areas(Published(city, source["ProductionCompany"], year)),
            "Performance" => kind + Made("Writer", "Director") + Areas(Published(Listed(Space + "; ", source["Theater"], city), publisher, year)),
            "Art" => kind + Made("Artist") + Areas(Listed(Space + ": ", city, source["Institution"])),
            _ => kind + Made("Author") + Areas(published),
        };
    }

    /// <summary>A journal's issue and volume as GOST writes them: "3 : Vol. 12", "Vol. 57", an issue alone; null where it has neither.</summary>
    static string? Issued(Source source, Words words)
    {
        var volume = source["Volume"] is { } number ? words.Volume + " " + number : null;
        return Listed(Space + ": ", source["Issue"], volume);
    }

    /// <summary>The areas of a description that are there, each after ". - ", and the full stop that ends it.</summary>
    static string Areas(params string?[] areas) =>
        string.Concat(areas.Where(a => !string.IsNullOrEmpty(a)).Select(a => "." + Space + "- " + a)) + ".";

    /// <summary>Parts that are there joined by a separator; null where none is.</summary>
    static string? Listed(string separator, params string?[] parts) =>
        parts.Where(p => !string.IsNullOrEmpty(p)).ToList() is { Count: > 0 } there ? string.Join(separator, there) : null;

    /// <summary>Place, publisher and date as GOST writes them: "Berlin : Springer, 2020", "[s.l.]" where it names no place, "[s.n.]" no publisher.</summary>
    static string Published(string? place, string? publisher, string? date) =>
        (place ?? "[s.l.]") + Space + ": " + (publisher ?? "[s.n.]") + (date != null ? ", " + date : "");

    /// <summary>
    /// A date as GOST writes it: "March 14, 2022", in German "14. March 2022" (the month as the source has it), a month and year "March
    /// 2022", a year alone; null where it has neither month nor year.
    /// </summary>
    static string? Date(string? year, string? month, string? day, Words words)
    {
        if (month == null)
            return year;
        if (words.German)
            return Listed(" ", day != null ? day + "." : null, month, year);
        return day != null ? month + " " + day + (year != null ? ", " + year : "") : Listed(" ", month, year);
    }
}
