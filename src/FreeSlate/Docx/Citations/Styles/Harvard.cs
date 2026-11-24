using System.Globalization;

namespace FreeSlate.Docx.Citations.Styles;

/// <summary>
/// Word's Harvard - Anglia style (see CitationStyle) as Word formats it. A citation names a source's creators by their last names and
/// its year ("(Smith, 2020, p. 33)", "Miller &amp; Brown", three or more "Lee, et al."), "Anon." where it names none (a film or a case by
/// its title instead), "n.d." where it has no year. The bibliography lists every source, sorted by what it begins with, in plain
/// paragraphs: "Smith, J., 2020. <i>Learning with Machines.</i> Berlin: Springer.", each kind of source as Word lays it out, a web page's
/// address underlined on a line of its own and the day it was seen on the next. German as Word translates it: "kein Datum", "Hrsg.",
/// "Zugriff am" …, the rest (page labels, "Available at:", the months as the source has them) as in English.
/// </summary>
public sealed class Harvard : CitationStyle
{
    public override string Name => "Harvard - Anglia";

    public override string File => @"\HarvardAnglia2008OfficeOnline.xsl";

    public override string? Version => null;

    public override IReadOnlyList<string> Aliases => ["Harvard"];

    /// <summary>The words Harvard writes in one language.</summary>
    sealed record Words(string NoDate, string Editor, string Editors, string Edition, string Volume, string Accessed, string DirectedBy,
        string SoundRecording, string Art, string PatentNumber);

    static readonly Words InEnglish = new("n.d.", "ed.", "eds.", "ed.", "Volume", "Accessed", "Directed by", "Sound Recording", "Art", "Patent No.");

    static readonly Words InGerman = new("kein Datum", "Hrsg.", "Hrsg.", "Hrsg.", "Band", "Zugriff am", "Regie:", "Tonaufnahme", "Kunst", "Patentnr.");

    static Words In(int lcid) => Common.German(lcid) ? InGerman : InEnglish;

    /// <summary>Sources' first elements, years and titles compared as Word sorts them: letters' case aside.</summary>
    static readonly StringComparer Sorting = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);

    /// <summary>
    /// A citation as Word writes it: each source's prefix, creators, year and place (volume and page "3:12", a page "p. 33", pages
    /// "pp. 46-48") joined by commas, then its suffix; several sources joined by semicolons in one pair of parentheses.
    /// </summary>
    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context)
    {
        var words = In(request.Lcid);
        return [new Piece("(" + string.Join("; ", request.Sources.Select(c => SourceText(c, words))) + ")")];
    }

    static string SourceText(Cited cited, Words words)
    {
        var source = cited.Source;
        var parts = new List<string>();
        if (!cited.NoAuthor && Short(source) is { Length: > 0 } names)
            parts.Add(names);
        if (!cited.NoYear)
            parts.Add(source["Year"] ?? words.NoDate);
        if (Locator(cited) is { } locator)
            parts.Add(locator);
        return cited.Prefix + string.Join(", ", parts) + cited.Suffix;
    }

    /// <summary>Where a citation points: "3:12" (volume and page), "p. 33", "pp. 46-48" (in German as well), a volume alone as given.</summary>
    static string? Locator(Cited cited)
    {
        var volume = string.IsNullOrEmpty(cited.Volume) ? null : cited.Volume;
        var pages = string.IsNullOrEmpty(cited.Pages) ? null : cited.Pages;
        if (volume != null)
            return pages != null ? volume + ":" + pages : volume;
        return pages != null ? Paged(pages) : null;
    }

    /// <summary>Pages as Harvard labels them: "p. 12", "pp. 45-67" where they are a range or a list.</summary>
    static string Paged(string pages) => (pages.IndexOfAny(['-', '–', '—', ',']) >= 0 ? "pp. " : "p. ") + pages;

    /// <summary>
    /// Who a source is by as Harvard names it first: its authors (a book with none its editors, then marked "ed."), an interview's
    /// interviewee, a patent's inventor, a sound recording's composer (else its performers), a performance's writer, a work of art's
    /// artist; null where it names none. A film and a case are named by their titles instead (see <see cref="TitleFirst"/>).
    /// </summary>
    static (Contributors? Names, bool Editors) Creator(Source source) => source.Type switch
    {
        "Interview" => (source.Role("Interviewee"), false),
        "Patent" => (source.Role("Inventor"), false),
        "SoundRecording" => (source.Role("Composer") ?? source.Role("Performer"), false),
        "Performance" => (source.Role("Writer"), false),
        "Art" => (source.Role("Artist"), false),
        _ => source.Role("Author") is { } author ? (author, false)
            : source.Type == "Book" && source.Role("Editor") is { } editors ? (editors, true) : (null, false),
    };

    /// <summary>Whether Harvard begins a source with its title rather than who it is by: a film, a case.</summary>
    static bool TitleFirst(Source source) => source.Type is "Film" or "Case";

    /// <summary>A citation's names: last names ("Smith", "Miller &amp; Brown", three or more "Lee, et al."), a corporate name, else "Anon.".</summary>
    static string Short(Source source)
    {
        if (TitleFirst(source))
            return source["Title"] ?? "";
        var (names, _) = Creator(source);
        if (names == null)
            return "Anon.";
        if (names.Corporate != null)
            return names.Corporate;
        var people = names.People;
        return people.Count switch
        {
            1 => people[0].Last,
            2 => people[0].Last + " & " + people[1].Last,
            _ => people[0].Last + ", et al.",
        };
    }

    /// <summary>
    /// A bibliography entry's names: last names and initials ("Smith, J.", "Miller, A. B. &amp; Brown, T.", up to three; more "Adams, R.
    /// et al."), a corporate name, editors marked ("Weber, M., ed."); "Anon." where the source names none.
    /// </summary>
    static string Long(Source source, Words words)
    {
        var (names, editors) = Creator(source);
        if (names == null)
            return "Anon.";
        var text = names.Corporate ?? (names.People.Count > 3 ? Inverted(names.People[0]) + " et al." : Joined([.. names.People.Select(Inverted)]));
        return editors ? text + ", " + (names.People.Count > 1 ? words.Editors : words.Editor) : text;
    }

    /// <summary>A person as the bibliography begins with them: "Smith, J.", "Miller, A. B.".</summary>
    static string Inverted(Person person) => Common.Initials(person) is { Length: > 0 } initials ? person.Last + ", " + initials : person.Last;

    /// <summary>Names in a list as Harvard joins them: "A", "A &amp; B", "A, B &amp; C".</summary>
    static string Joined(IReadOnlyList<string> names) =>
        names.Count <= 1 ? string.Concat(names) : string.Join(", ", names.Take(names.Count - 1)) + " & " + names[^1];

    /// <summary>Contributors named inside an entry: initials before last names ("M. Weber &amp; I. Klein"), or full names ("Ang Lee").</summary>
    static string Named(Contributors names, bool full) =>
        names.Corporate ?? Joined([.. names.People.Select(p => (full ? Common.Given(p) : Common.Initials(p)) is { Length: > 0 } given
            ? given + " " + p.Last : p.Last)]);

    /// <summary>
    /// The bibliography: every source, sorted by what its entry begins with (names, "Anon." or a title), then its year and title; plain
    /// paragraphs.
    /// </summary>
    public override BibliographyList Bibliography(CitationContext context, int lcid)
    {
        var words = In(lcid);
        var entries = context.Sources.Sources
            .OrderBy(s => TitleFirst(s) ? s["Title"] ?? "" : Long(s, words), Sorting)
            .ThenBy(s => s["Year"] ?? "", Sorting)
            .ThenBy(s => s["Title"] ?? "", Sorting)
            .Select(s => new BibliographyEntry(Entry(s, words)))
            .ToList();
        return new BibliographyList(entries, BibliographyLayout.Plain);
    }

    /// <summary>
    /// One source's entry as Harvard writes its kind: names and year ("Smith, J., 2020. ", without a year "Anon., n.d. "), the title (in
    /// italics where it is the work itself), then what the kind has: place and publisher ("Berlin: Springer", "s.l." and "s.n." for the
    /// missing), an edition ("2 ed."), a chapter's book ("In: M. Weber &amp; I. Klein, eds."), a journal's volume and issue ("12(3)",
    /// "Volume 57") and pages, a web page's address and the day it was seen.
    /// </summary>
    static List<Piece> Entry(Source source, Words words)
    {
        var text = new List<Piece>();
        void Add(string? value, bool italic = false, bool underline = false)
        {
            if (!string.IsNullOrEmpty(value))
                text.Add(new Piece(value, Italic: italic, Underline: underline));
        }
        var title = source["Title"];
        var year = source["Year"];
        var city = source["City"];
        var publisher = source["Publisher"];
        var dated = year != null ? year + ". " : words.NoDate + " ";
        switch (source.Type)
        {
            case "Film":
                Add(Titled(title, "."), italic: true);
                Add(dated + "[Film] " + (source.Role("Director") is { } directors ? words.DirectedBy + " " + Named(directors, full: true) + ". " : "")
                    + Ended(Place(city, source["ProductionCompany"])));
                return text;
            case "Case":
                Add(Titled(title, ""), italic: true);
                Add("(" + (year ?? words.NoDate) + ") " + (source["Reporter"] is { } reporter ? reporter + ". " : ""));
                return text;
        }
        Add(Long(source, words) + ", " + dated);
        switch (source.Type)
        {
            case "Book":
                Add(Titled(title, "."), italic: true);
                Add(Edition(source, words) + Ended(Place(city, publisher)));
                break;
            case "BookSection":
                Add(Titled(title, ".") + "In: " + (source.Role("Editor") is { } editors
                    ? Named(editors, full: false) + ", " + (editors.People.Count > 1 ? words.Editors : words.Editor) + " " : ""));
                Add(Titled(source["BookTitle"], "."), italic: true);
                Add(Edition(source, words) + Ended(Listed(", ", Place(city, publisher), Pages(source))));
                break;
            case "JournalArticle":
            {
                Add(Titled(title, "."));
                var volume = source["Volume"];
                var issue = source["Issue"];
                var rest = Listed(", ", issue != null ? volume + "(" + issue + ")" : volume != null ? words.Volume + " " + volume : null, Pages(source));
                Add(source["JournalName"] is { } journal ? journal + (rest != null ? ", " : ". ") : null, italic: true);
                Add(Ended(rest));
                break;
            }
            case "ArticleInAPeriodical":
                Add(Titled(title, "."));
                Add(source["PeriodicalTitle"], italic: true);
                Add(Listed(", ", DayMonth(source["Day"], source["Month"], null), Pages(source)) is { } following ? Ended(", " + following) : ".");
                break;
            case "ConferenceProceedings":
                Add(Titled(title, "."), italic: true);
                Add(Ended(Listed(", ", city, publisher, Pages(source))));
                break;
            case "Report":
            case "ElectronicSource":
                Add(Titled(title, ","), italic: true);
                Add(Ended(Place(city, publisher)));
                break;
            case "InternetSite":
            case "DocumentFromInternetSite":
                Add(Titled(title, "."), italic: true);
                Add("[Online] " + (source["URL"] != null ? "\nAvailable at: " : ""));
                Add(source["URL"], underline: true);
                Add(DayMonth(source["DayAccessed"], source["MonthAccessed"], source["YearAccessed"]) is { } accessed
                    ? "\n[" + words.Accessed + " " + accessed + "]." : null);
                break;
            case "Interview":
                Add(Titled(title, ""), italic: true);
                Add("[Interview]" + (DayMonth(source["Day"], source["Month"], year) is { } date ? " (" + date + ")" : "") + ".");
                break;
            case "Patent":
                Add(Titled(title, "."), italic: true);
                Add(Ended(Listed(", ", source["CountryRegion"], source["PatentNumber"] is { } number ? words.PatentNumber + " " + number : null)));
                break;
            case "SoundRecording":
                Add(Titled(title, "."), italic: true);
                Add("[" + words.SoundRecording + "]" + (source["ProductionCompany"] is { } company ? " (" + company + ")" : "") + ".");
                break;
            case "Art":
                Add(Titled(title, "."), italic: true);
                Add("[" + words.Art + "]" + (source["Institution"] is { } institution ? " (" + institution + ")" : "") + ".");
                break;
            default:
                Add(Titled(title, "."), italic: true);
                Add(Ended(Place(city, publisher)));
                break;
        }
        return text;
    }

    /// <summary>A title with the mark that follows it and a space ("Beowulf. "); null where there is none.</summary>
    static string? Titled(string? title, string mark) => title != null ? title + mark + " " : null;

    /// <summary>The part that ends an entry, with a full stop where it has none ("Berlin: Springer.", "London: s.n."); empty where none is.</summary>
    static string Ended(string? text) => string.IsNullOrEmpty(text) ? "" : text.EndsWith('.') ? text : text + ".";

    /// <summary>Parts that are there joined by a separator; null where none is.</summary>
    static string? Listed(string separator, params string?[] parts) =>
        parts.Where(p => !string.IsNullOrEmpty(p)).ToList() is { Count: > 0 } there ? string.Join(separator, there) : null;

    /// <summary>Place and publisher as Harvard writes them: "Berlin: Springer", "s.l." where it names no place, "s.n." no publisher.</summary>
    static string Place(string? city, string? publisher) => (city ?? "s.l.") + ": " + (publisher ?? "s.n.");

    /// <summary>A book's edition as Harvard writes it, before its place: "2 ed. " (German "2 Hrsg. ").</summary>
    static string Edition(Source source, Words words) => source["Edition"] is { } edition ? edition + " " + words.Edition + " " : "";

    /// <summary>A source's pages labelled ("pp. 101-120"); null where it has none.</summary>
    static string? Pages(Source source) => source["Pages"] is { } pages ? Paged(pages) : null;

    /// <summary>A date as Harvard writes it, day before month before year ("5 June 2021", "14 March"); null where it has neither month nor year.</summary>
    static string? DayMonth(string? day, string? month, string? year) =>
        month == null ? year : Listed(" ", day, month, year);
}
