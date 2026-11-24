using System.Globalization;

namespace FreeSlate.Docx.Citations.Styles;

/// <summary>
/// Word's GB7714 style (see CitationStyle) as Word formats it. A citation names a source by its first element and year ("(Smith, 2020
/// p. 33)", two or more creators "Miller, et al."); a journal article or a conference paper begins with its title ("(Feedback at scale,
/// 2021)"); a source with neither creator nor year shows its tag ("(Beo)", a page glued to it as Word does: "(Beop. 12)"). The
/// bibliography lists every source sorted by its first element in plain paragraphs, the creators and year in bold ("<b>Smith, John.
/// 2020.</b> <i>Learning with Machines.</i> Berlin : Springer, 2020."), "—" for the creators of the entry before, each kind of source as
/// Word lays it out. German as Word translates it ("und", "Bd.", "S.", "[Hrsg.]", "Zitat vom:" …, dates "14. March 2022"), the months as
/// the source has them.
/// </summary>
public sealed class Gb7714 : CitationStyle
{
    public override string Name => "GB7714";

    public override string File => @"\GB.XSL";

    public override string? Version => null;

    public override IReadOnlyList<string> Aliases => ["GB"];

    /// <summary>A no-break space and a colon, as Word sets a place before its publisher ("Berlin : Springer").</summary>
    const string Colon = "\u00A0: ";

    /// <summary>The words GB7714 writes in one language.</summary>
    sealed record Words(bool German, string And, string Page, string Pages, string Volume, string Cited, string Editor, string Translator,
        string BookAuthor, string Director, string Composer, string Performer, string Interviewer);

    static readonly Words InEnglish = new(false, "and", "p.", "pp.", "Vol.", "Cited:", "[ed.]", "[trans.]", "[book auth.]", "[dir.]",
        "[comps.]", "[perf.]", "[interv.]");

    static readonly Words InGerman = new(true, "und", "S.", "S.", "Bd.", "Zitat vom:", "[Hrsg.]", "[Übers.]", "[Buchverf.]", "[Reg.]",
        "[Komp.]", "[Interpr.]", "[Befragte Person]");

    static Words In(int lcid) => Common.German(lcid) ? InGerman : InEnglish;

    /// <summary>Sources' first elements, years and titles compared as Word sorts them: letters' case aside.</summary>
    static readonly StringComparer Sorting = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);

    /// <summary>
    /// A citation as Word writes it: each source's prefix, first element and year joined by a comma, its place after a space (volume and
    /// page "3:12", a page "p. 33", pages "pp. 46-48"), then its suffix; several sources joined by semicolons in one pair of parentheses.
    /// </summary>
    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context)
    {
        var words = In(request.Lcid);
        return [new Piece("(" + string.Join("; ", request.Sources.Select(c => SourceText(c, words))) + ")")];
    }

    static string SourceText(Cited cited, Words words)
    {
        var source = cited.Source;
        var first = TitleFirst(source) ? source["Title"] ?? Short(source) : Short(source);
        var year = source["Year"];
        var parts = new List<string>();
        if (!cited.NoAuthor && first != null)
            parts.Add(first);
        if (!cited.NoYear && year != null)
            parts.Add(year);
        var shown = string.Join(", ", parts);
        // Without a first element and a year Word shows the source's tag, and the place follows it without a space.
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

    /// <summary>Whether a source begins with its title, before its creators: a journal article, a conference paper.</summary>
    static bool TitleFirst(Source source) => source.Type is "JournalArticle" or "ConferenceProceedings";

    /// <summary>
    /// Who a source is by as GB7714 names it first, with the mark it gives them there: its authors (a book with none its editors,
    /// "[ed.]"), a film's director, an interview's interviewee, a patent's inventor, a sound recording's performers ("[perf.]", else its
    /// composers, "[comps.]"), a performance's writer, a work of art's artist; null where it names none.
    /// </summary>
    static (Contributors? Names, string? Role, string? Mark) Creator(Source source, Words words) => source.Type switch
    {
        "Film" => (source.Role("Director"), "Director", null),
        "Interview" => (source.Role("Interviewee"), "Interviewee", null),
        "Patent" => (source.Role("Inventor"), "Inventor", null),
        "SoundRecording" => source.Role("Performer") is { } performers ? (performers, "Performer", words.Performer)
            : (source.Role("Composer"), "Composer", words.Composer),
        "Performance" => (source.Role("Writer"), "Writer", null),
        "Art" => (source.Role("Artist"), "Artist", null),
        _ => source.Role("Author") is { } author ? (author, "Author", null)
            : source.Type == "Book" && source.Role("Editor") is { } editors ? (editors, "Editor", words.Editor) : (null, null, null),
    };

    /// <summary>A citation's names: a last name, two or more "Miller, et al.", a corporate name; null where the source names none.</summary>
    static string? Short(Source source)
    {
        var (names, _, _) = Creator(source, InEnglish);
        if (names == null)
            return null;
        if (names.Corporate != null)
            return names.Corporate;
        return names.People.Count == 1 ? names.People[0].Last : names.People[0].Last + ", et al.";
    }

    /// <summary>
    /// Names as GB7714 lists them: last name first or first name first ("Smith, John" / "John Smith"), joined ("A and B", "A, B and C");
    /// more than three the first and "et al."; a corporate name as it is.
    /// </summary>
    static string Names(Contributors names, bool inverted, Words words)
    {
        if (names.Corporate != null)
            return names.Corporate;
        string Name(Person p) => Common.Given(p) is { Length: > 0 } given ? inverted ? p.Last + ", " + given : given + " " + p.Last : p.Last;
        var people = names.People;
        if (people.Count > 3)
            return Name(people[0]) + ", et al.";
        return people.Count <= 1 ? string.Concat(people.Select(Name))
            : string.Join(", ", people.Take(people.Count - 1).Select(Name)) + " " + words.And + " " + Name(people[^1]);
    }

    /// <summary>The creators an entry begins with ("Smith, John", "Weber, Max, [ed.]"); null where the source names none.</summary>
    static string? Heading(Source source, Words words) =>
        Creator(source, words) is ({ } names, _, var mark) ? Names(names, inverted: true, words) + (mark != null ? ", " + mark : "") : null;

    /// <summary>
    /// The bibliography: every source, sorted by its first element (its creators, a journal article's or a conference paper's title, the
    /// title where it names no one), then its year and title; plain paragraphs, an entry's creators "—" where the entry before has them.
    /// </summary>
    public override BibliographyList Bibliography(CitationContext context, int lcid)
    {
        var words = In(lcid);
        var sorted = context.Sources.Sources
            .OrderBy(s => (TitleFirst(s) ? s["Title"] : null) ?? Heading(s, words) ?? s["Title"] ?? "", Sorting)
            .ThenBy(s => s["Year"] ?? "", Sorting)
            .ThenBy(s => s["Title"] ?? "", Sorting)
            .ToList();
        var entries = new List<BibliographyEntry>();
        string? before = null;
        foreach (var source in sorted)
        {
            var heading = Heading(source, words);
            entries.Add(new BibliographyEntry(Entry(source, heading != null && heading == before ? "—" : heading, words)));
            before = heading;
        }
        return new BibliographyList(entries, BibliographyLayout.Plain);
    }

    /// <summary>
    /// One source's entry as GB7714 writes its kind: the creators and year in bold, each with a full stop ("Smith, John. 2020."), the
    /// title (in italics where it is the work itself; a journal article's and a conference paper's before the bold), then the kind's
    /// parts: other contributors with their marks ("[trans.] Richard Pevear and Larissa Volokhonsky"), the place, publisher and date
    /// ("Berlin : Springer, 2020", "s.l." and "s.n." standing in), volumes, pages, a web page's dates and address.
    /// </summary>
    static List<Piece> Entry(Source source, string? heading, Words words)
    {
        var text = new List<Piece>();
        void Add(string? value, bool italic = false, bool bold = false)
        {
            if (!string.IsNullOrEmpty(value))
                text.Add(new Piece(value, Italic: italic, Bold: bold));
        }
        var title = source["Title"];
        var year = source["Year"];
        var city = source["City"];
        var publisher = source["Publisher"];
        var (_, role, _) = Creator(source, words);
        var bold = string.Join(" ", new[] { heading, year }.Where(p => p != null).Select(p => p!.EndsWith('.') ? p : p + "."));
        if (TitleFirst(source))
            Add(Titled(title), italic: true);
        Add(bold, bold: true);
        if (bold.Length > 0)
            Add(" ");
        string? With(string mark, string contributors) => source.Role(contributors) is { } names && contributors != role
            ? mark + " " + Names(names, inverted: false, words) : null;
        var date = Date(year, source["Month"], source["Day"], words);
        switch (source.Type)
        {
            case "Book":
                Add(Titled(title), italic: true);
                Add(Sentences(With(words.Editor, "Editor"), With(words.Translator, "Translator"), source["Edition"], Published(city, publisher, year),
                    source["Volume"] is { } volume ? words.Volume + " " + volume : null));
                break;
            case "BookSection":
                Add(Spaced(Sentences(title, With(words.BookAuthor, "BookAuthor"), With(words.Editor, "Editor"), With(words.Translator, "Translator"))));
                Add(Titled(source["BookTitle"]), italic: true);
                Add(Listed(", ", Published(city, publisher, year), Pages(source, words)) + ".");
                break;
            case "JournalArticle":
                Add(Listed(", ", source["Issue"], date, source["JournalName"], source["Volume"] is { } number ? words.Volume + " " + number : null,
                    Pages(source, words)) + ".");
                break;
            case "ArticleInAPeriodical":
                Add(Titled(title));
                Add(Titled(source["PeriodicalTitle"]), italic: true);
                Add(Listed(", ", date, Pages(source, words)) + ".");
                break;
            case "ConferenceProceedings":
                Add(Sentences(Published(city, publisher, year), source["ConferenceName"], Pages(source, words)));
                break;
            case "Report":
                Add(Titled(title), italic: true);
                Add(Sentences(source["Institution"], Published(city, publisher, year), source["ThesisType"], source["StandardNumber"]));
                break;
            case "InternetSite":
            case "DocumentFromInternetSite":
            {
                Add(Titled(title));
                Add(Titled(source["InternetSiteTitle"]), italic: true);
                var accessed = Date(source["YearAccessed"], source["MonthAccessed"], source["DayAccessed"], words);
                Add(Listed(" ", "[Online]", date != null ? date + "." : null, accessed != null ? "[" + words.Cited + " " + accessed + ".]" : null,
                    source["URL"] is { } url ? url + "." : null));
                break;
            }
            case "Film":
                Add(Titled(title), italic: true);
                Add(Listed(", ", Listed("; ", source["ProductionCompany"], source["Distributor"]), year) + ".");
                break;
            case "Interview":
                Add(Sentences(title, With(words.Interviewer, "Interviewer"), Published(city, source["Broadcaster"], date)));
                break;
            case "Patent":
                Add(Titled(title) + (source["PatentNumber"] is { } number2 ? number2 + " " : ""), italic: true);
                Add(Listed(", ", source["CountryRegion"], date) + ".");
                break;
            case "Case":
                Add(Titled(title), italic: true);
                Add(Sentences(Listed(", ", source["CaseNumber"], Published(city, source["Court"], year)), source["Reporter"]));
                break;
            case "SoundRecording":
                Add(Spaced(Sentences(title, With(words.Composer, "Composer"))));
                Add(Titled(source["AlbumTitle"]), italic: true);
                Add((source["Medium"] is { } medium ? "[" + medium + "] " : "") + Published(city, source["ProductionCompany"], year) + ".");
                break;
            case "Performance":
                Add(Titled(title), italic: true);
                Add(Sentences(With(words.Director, "Director"), Published(Listed(", ", source["Theater"], city), publisher, year)));
                break;
            case "Art":
                Add(Titled(title), italic: true);
                Add(Listed(Colon, Listed(", ", source["Institution"], city), year) + ".");
                break;
            case "ElectronicSource":
                Add(Titled(title), italic: true);
                Add((source["Medium"] is { } medium2 ? "[" + medium2 + "] " : "") + Published(city, publisher, year) + ".");
                break;
            default:
                Add(Sentences(title, Published(city, publisher, year)));
                break;
        }
        return text;
    }

    /// <summary>A title with its full stop and a space ("Beowulf. "); null where there is none.</summary>
    static string? Titled(string? title) => title != null ? title + ". " : null;

    /// <summary>Parts that are there, each closed by a full stop ("Technical Report. KEI-2019-04."); null where none is.</summary>
    static string? Sentences(params string?[] parts) => Listed(". ", parts) is { } text ? text + "." : null;

    /// <summary>A part followed by a space where something follows it in another formatting; null where there is none.</summary>
    static string? Spaced(string? text) => text != null ? text + " " : null;

    /// <summary>Parts that are there joined by a separator; null where none is.</summary>
    static string? Listed(string separator, params string?[] parts) =>
        parts.Where(p => !string.IsNullOrEmpty(p)).ToList() is { Count: > 0 } there ? string.Join(separator, there) : null;

    /// <summary>Place, publisher and date as GB7714 writes them: "Berlin : Springer, 2020", "s.l." where it names no place, "s.n." no publisher.</summary>
    static string Published(string? place, string? publisher, string? date) =>
        (place ?? "s.l.") + Colon + (publisher ?? "s.n.") + (date != null ? ", " + date : "");

    /// <summary>A source's pages labelled ("pp. 101-120", German "S. 101-120"); null where it has none.</summary>
    static string? Pages(Source source, Words words) => source["Pages"] is { } pages ? Paged(pages, words) : null;

    /// <summary>
    /// A date as GB7714 writes it: "March 14, 2022", in German "14. March 2022" (the month as the source has it), a month and year
    /// "March 2022", a year alone; null where it has neither month nor year.
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
