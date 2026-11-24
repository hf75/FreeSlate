using System.Globalization;

namespace FreeSlate.Docx.Citations.Styles;

/// <summary>
/// Word's Chicago style (Chicago Manual of Style, Fifteenth Edition, see CitationStyle) as Word formats it, measured against Word's own
/// results. A citation names its sources by their authors' last names and the year ("(Miller and Brown 2019)"), four authors or more
/// the first and "et al." ("(Adams, et al. 2015)"), adds the title where another source has the same author and year, the volume and
/// pages after a comma ("(Kant 2011, 3:12)"); several sources stand apart by commas. The bibliography: every source in the list,
/// sorted by their first words, with a hanging indent; the first author inverted, the others upright with a comma before the last
/// ("Wang, Li, and Eva Fischer"), more than seven the first and "et al."; the same author as the entry before a dash ("—."); each kind
/// of source with its elements in Word's order. English, or German words where the field asks for them (the source's own text as it is).
/// </summary>
public sealed class Chicago : CitationStyle
{
    public override string Name => "Chicago";

    public override string File => @"\CHICAGO.XSL";

    public override string? Version => null;

    /// <summary>
    /// A citation as Word writes it: in parentheses each source's prefix, its author's last names (its title where it has no author or
    /// \n suppresses it), its title after a comma where another source has the same author and year (not with \t), its year (n.d.
    /// without one; none with \y), its volume and pages after a comma ("3:12"), its suffix; the sources apart by commas.
    /// </summary>
    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context)
    {
        var words = Words.For(request.Lcid);
        var authors = context.Sources.Sources.Select(s => (s.Tag, Author: Author(s, context, words), Year: s["Year"])).ToList();
        var parts = new List<string>();
        foreach (var cited in request.Sources)
        {
            var source = cited.Source;
            var author = Author(source, context, words);
            var title = cited.NoTitle ? null : source["Title"];
            // Another source of the same author and year: the title tells them apart.
            var shared = authors.Any(a => a.Author == author && a.Year == source["Year"] && !a.Tag.Equals(source.Tag, StringComparison.OrdinalIgnoreCase));
            var text = cited.NoAuthor || author == null ? title : shared && title != null ? author + ", " + title : author;
            if (!cited.NoYear)
                text = (text == null ? "" : text + " ") + (source["Year"] ?? words.NoDate);
            var locator = cited.Volume != null && cited.Pages != null ? cited.Volume + ":" + cited.Pages : cited.Volume ?? cited.Pages;
            if (locator != null)
                text = text == null ? locator : text + ", " + locator;
            parts.Add(cited.Prefix + text + cited.Suffix);
        }
        return [new Piece("(" + string.Join(", ", parts) + ")")];
    }

    /// <summary>
    /// The bibliography as Word lays it out: all sources sorted by the names or title they begin with (a film by its director's name,
    /// though it begins with its title; letters' case aside), then by title and year; a hanging indent; an entry whose author is the one
    /// of the entry before begins with a dash instead ("—.").
    /// </summary>
    public override BibliographyList Bibliography(CitationContext context, int lcid)
    {
        var words = Words.For(lcid);
        var order = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);
        var sorted = context.Sources.Sources.Select(s => (Source: s, Lead: Lead(s, words)))
            .OrderBy(e => (e.Source.Type == "Film" ? Names(e.Source.Role("Director"), words) : e.Lead?.Names) ?? e.Source["Title"] ?? "", order)
            .ThenBy(e => e.Source["Title"] ?? "", order).ThenBy(e => e.Source["Year"] ?? "", order).ToList();
        var entries = new List<BibliographyEntry>();
        string? previous = null;
        foreach (var (source, lead) in sorted)
        {
            var shown = lead == null ? null : (lead.Names == previous ? "—" : lead.Names) + lead.Role;
            entries.Add(new BibliographyEntry(Entry(source, shown, words)));
            previous = lead?.Names;
        }
        return new BibliographyList(entries, BibliographyLayout.Hanging);
    }

    /// <summary>The words Chicago writes, English or German.</summary>
    sealed record Words(bool German, string And, string NoDate, string Editor, string Editors, string EditedBy, string EditedByInside,
        string TranslatedBy, string TranslatedByInside, string Volume, string Volumes, string Issue, string InterviewBy, string DirectedBy,
        string ProducedBy, string Composer, string By, string Accessed, string Open, string Close)
    {
        public static Words For(int lcid) => Common.German(lcid)
            ? new(true, "und", "kein Datum", "Hrsg.", "Hrsg.", "Herausgeber:", "Herausgeber:", "Übersetzung:", "Übersetzung:", "Bd.", "Bde.",
                "Nr.", "Interview geführt von", "Regie:", "Produzent:", "Komp.", "von", "Zugriff am", "„", "“")
            : new(false, "and", "n.d.", "ed.", "eds.", "Edited by", "edited by", "Translated by", "translated by", "Vol.", "vols.", "no.",
                "interview by", "Directed by", "Produced by", "Comp.", "by", "accessed", "\"", "\"");
    }

    /// <summary>
    /// Whom a citation names a source by: its authors; a book's editors where it has no author; a film's director, an interview's
    /// interviewee, a patent's inventor, a recording's performer (its composer where it has none), a performance's writer, a work of
    /// art's artist; a case none (its title instead).
    /// </summary>
    static Contributors? Cited(Source source) => source.Type switch
    {
        "Book" => source.Role("Author") ?? source.Role("Editor"),
        "Film" => source.Role("Director"),
        "Interview" => source.Role("Interviewee"),
        "Patent" => source.Role("Inventor"),
        "SoundRecording" => source.Role("Performer") ?? source.Role("Composer"),
        "Performance" => source.Role("Writer"),
        "Art" => source.Role("Artist"),
        "Case" => null,
        _ => source.Role("Author"),
    };

    /// <summary>
    /// A source's author as its citations name it: a corporate name, up to three last names ("Lee, Garcia and Novak"), more the first
    /// and "et al." ("Adams, et al."); a single person's initials before the name where another source's first author has that last
    /// name with other given names ("A. Lee"). Null where the source names no one.
    /// </summary>
    static string? Author(Source source, CitationContext context, Words words)
    {
        if (Cited(source) is not { } who)
            return null;
        if (who.Corporate != null)
            return who.Corporate;
        if (who.People is [var person])
        {
            var namesake = person.Last.Length > 0 && context.Sources.Sources.Any(s => !s.Tag.Equals(source.Tag, StringComparison.OrdinalIgnoreCase)
                && Cited(s)?.People is [var first, ..] && first.Last == person.Last && Common.Given(first) != Common.Given(person));
            return namesake && Common.Initials(person) is { Length: > 0 } initials ? initials + " " + person.Last : Surname(person);
        }
        if (who.People.Count > 3)
            return Surname(who.People[0]) + ", et al.";
        var last = who.People.Select(Surname).ToList();
        return string.Join(", ", last.Take(last.Count - 1)) + " " + words.And + " " + last[^1];
    }

    /// <summary>A person as the first of an entry's names: "Miller, Anna B.".</summary>
    static string Inverted(Person person) => string.Join(", ", new[] { person.Last, Common.Given(person) }.Where(n => n.Length > 0));

    /// <summary>A person in their names' order: "Anna B. Miller".</summary>
    static string Upright(Person person) => string.Join(" ", new[] { Common.Given(person), person.Last }.Where(n => n.Length > 0));

    /// <summary>The name a citation gives a person by: the last name, the given names where there is none.</summary>
    static string Surname(Person person) => person.Last.Length > 0 ? person.Last : Common.Given(person);

    /// <summary>
    /// The names an entry begins with: the first person inverted, the others upright, a comma before the last ("Miller, Anna B., and
    /// Tom Brown"; "Lee, Kim, Maria Garcia, and Petr Novak"), more than seven the first and "et al."; a corporate name as it is.
    /// </summary>
    static string? Names(Contributors? who, Words words)
    {
        if (who == null)
            return null;
        if (who.Corporate != null)
            return who.Corporate;
        var people = who.People;
        if (people.Count > 7)
            return Inverted(people[0]) + ", et al.";
        if (people.Count == 1)
            return Inverted(people[0]);
        return string.Join(", ", people.Take(people.Count - 1).Select((p, k) => k == 0 ? Inverted(p) : Upright(p)))
               + ", " + words.And + " " + Upright(people[^1]);
    }

    /// <summary>
    /// Names upright, as an entry gives editors, translators, directors: "Max Weber", "Max Weber and Ina Klein", from three on a comma
    /// before the last; null where there are none.
    /// </summary>
    static string? UprightNames(Contributors? who, Words words)
    {
        if (who == null)
            return null;
        if (who.Corporate != null)
            return who.Corporate;
        var names = who.People.Select(Upright).ToList();
        return names.Count switch
        {
            1 => names[0],
            2 => names[0] + " " + words.And + " " + names[1],
            _ => string.Join(", ", names.Take(names.Count - 1)) + ", " + words.And + " " + names[^1],
        };
    }

    /// <summary>The names an entry begins with (compared with the entry before) and what follows them (", ed.").</summary>
    sealed record Leading(string Names, string Role = "");

    /// <summary>
    /// What an entry begins with: its authors; a book's editors where it has no author (", ed."); an interview's interviewee and its
    /// interviewer ("Goodall, Jane, interview by Marco Rossi"), a patent's inventor, a recording's performer (its composer where it has
    /// none), a performance's writer, a work of art's artist. Films and cases begin with their titles.
    /// </summary>
    static Leading? Lead(Source source, Words words)
    {
        Contributors? who;
        var role = "";
        switch (source.Type)
        {
            case "Book":
                if (source.Role("Author") is null && source.Role("Editor") is { } editors)
                    return new Leading(Names(editors, words)!, ", " + (editors.People.Count > 1 ? words.Editors : words.Editor));
                who = source.Role("Author");
                break;
            case "Film" or "Case":
                return null;
            case "Interview":
                who = source.Role("Interviewee");
                if (UprightNames(source.Role("Interviewer"), words) is { } interviewers)
                    role = ", " + words.InterviewBy + " " + interviewers;
                break;
            case "Patent":
                who = source.Role("Inventor");
                break;
            case "SoundRecording":
                who = source.Role("Performer") ?? source.Role("Composer");
                break;
            case "Performance":
                who = source.Role("Writer");
                break;
            case "Art":
                who = source.Role("Artist");
                break;
            default:
                who = source.Role("Author");
                break;
        }
        return Names(who, words) is { } names ? new Leading(names, role) : null;
    }

    /// <summary>
    /// An entry's elements in Word's order for its kind of source, each where the source has it, closed by a period (none added where
    /// it already ends in one); titles of books, journals, sites and works in italics with their period, of articles, chapters, songs
    /// and theses in quotation marks.
    /// </summary>
    static List<Piece> Entry(Source source, string? lead, Words words)
    {
        var line = new Line();
        line.Text(lead);
        var year = source["Year"] ?? words.NoDate;
        var date = Date(source["Year"], source["Month"], source["Day"], words) ?? words.NoDate;
        string? Prefixed(string word, string? text) => text == null ? null : word + " " + text;
        switch (source.Type)
        {
            case "Book":
                line.Italic(source["Title"]);
                if (source.Role("Author") != null)
                    line.Text(Prefixed(words.EditedBy, UprightNames(source.Role("Editor"), words)));
                line.Text(Prefixed(words.TranslatedBy, UprightNames(source.Role("Translator"), words)));
                line.Text(source["Edition"]);
                line.Text(Prefixed(words.Volume, source["Volume"]));
                line.Text(source["NumberVolumes"] is { } volumes ? volumes + " " + words.Volumes : null);
                line.Text(Publication(source["City"], source["Publisher"], year));
                break;
            case "BookSection":
            {
                line.Quoted(source["Title"], words);
                // "In *Handbook of Teaching*, edited by Max Weber and Ina Klein, 101-120."
                var inside = Listed(Prefixed(words.By, UprightNames(source.Role("BookAuthor"), words)),
                    Prefixed(words.EditedByInside, UprightNames(source.Role("Editor"), words)),
                    Prefixed(words.TranslatedByInside, UprightNames(source.Role("Translator"), words)), source["Pages"]);
                line.Within("In ", source["BookTitle"], inside == null ? null : ", " + inside);
                line.Text(Publication(source["City"], source["Publisher"], year));
                break;
            }
            case "JournalArticle":
            {
                line.Quoted(source["Title"], words);
                // "*Journal of Learning* 12, no. 3 (2021): 45-67."
                var volume = (source["Volume"] is { } v ? " " + v : "") + (source["Issue"] is { } i ? ", " + words.Issue + " " + i : "");
                var pages = source["Pages"] is { } p ? ": " + p : "";
                line.Within("", source["JournalName"], volume + " (" + year + ")" + pages);
                break;
            }
            case "ArticleInAPeriodical":
                line.Quoted(source["Title"], words);
                // "*Die Zeit*, March 14, 2022: 12."
                line.Within("", source["PeriodicalTitle"], ", " + date + (source["Pages"] is { } periodicalPages ? ": " + periodicalPages : ""));
                break;
            case "ConferenceProceedings":
                line.Quoted(source["Title"], words);
                line.Italic(source["ConferenceName"]);
                line.Text(Prefixed(words.EditedBy, UprightNames(source.Role("Editor"), words)));
                line.Text(Publication(source["City"], source["Publisher"], year));
                line.Text(source["Pages"]);
                break;
            case "Report" when source["Publisher"] == null && (source["ThesisType"] != null || source["Institution"] != null):
                // A thesis or an institution's report: "\"Homework and sleep.\" Technical Report, Korea Education Institute, Seoul, 2019."
                line.Quoted(source["Title"], words);
                line.Text(Listed(source["ThesisType"], source["Institution"], source["City"], year));
                break;
            case "Report":
                line.Italic(source["Title"]);
                line.Text(source["ThesisType"]);
                line.Text(Publication(source["City"], source["Publisher"], year));
                break;
            case "InternetSite":
                line.Italic(source["Title"]);
                line.Text(date);
                line.Text(Online(source, words));
                break;
            case "DocumentFromInternetSite":
                line.Quoted(source["Title"], words);
                line.Italic(source["InternetSiteTitle"]);
                line.Text(date);
                line.Text(Online(source, words));
                break;
            case "Film":
                line.Italic(source["Title"]);
                line.Text(Prefixed(words.DirectedBy, UprightNames(source.Role("Director"), words)));
                line.Text(Prefixed(words.ProducedBy, source["ProductionCompany"]));
                line.Text(source["Distributor"] is { } distributor ? distributor + ", " + year : year);
                break;
            case "Interview":
                // The title in italics without a period: "*On curiosity* BBC. Radio 4. June 5, 2021."
                line.Italic(source["Title"], stop: false);
                line.Text(source["Broadcaster"]);
                line.Text(source["Station"]);
                line.Text(date);
                break;
            case "Patent":
                line.Text(source["Title"]);
                // "United States Patent 10123456."
                var patent = "Patent" + (source["PatentNumber"] is { } number ? " " + number : "");
                line.Text(source["CountryRegion"] is { } country ? country + " " + patent : patent);
                line.Text(date);
                break;
            case "Case":
                line.Italic(source["Title"]);
                // The case number, then the court and the year: "347 U.S. 483 (Supreme Court, 1954)."
                line.Text(Spaced(source["CaseNumber"], "(" + Listed(source["Court"], year) + ")"));
                break;
            case "SoundRecording":
                line.Quoted(source["Title"], words);
                line.Italic(source["AlbumTitle"]);
                if (source.Role("Performer") != null)
                    line.Text(Prefixed(words.Composer, UprightNames(source.Role("Composer"), words)));
                line.Text(year);
                line.Text(source["Medium"]);
                break;
            case "Performance":
                line.Italic(source["Title"]);
                line.Text(Prefixed(words.DirectedBy, UprightNames(source.Role("Director"), words)));
                line.Text(Listed(source["Theater"], source["City"]));
                line.Text(date);
                break;
            case "Art":
                line.Italic(source["Title"]);
                line.Text(Listed(source["Institution"], source["City"]));
                break;
            case "ElectronicSource":
                line.Italic(source["Title"]);
                line.Text(source["Medium"]);
                line.Text(Publication(source["City"], source["Publisher"], year));
                break;
            default:
                line.Quoted(source["Title"], words);
                line.Text(Publication(source["City"], source["Publisher"], year));
                break;
        }
        return line.Pieces;
    }

    /// <summary>Where and by whom a work came out, and when: "Berlin: Springer, 2020", "Seoul, 2019" (no publisher).</summary>
    static string Publication(string? city, string? publisher, string year)
    {
        var place = city != null && publisher != null ? city + ": " + publisher : city ?? publisher;
        return place == null ? year : place + ", " + year;
    }

    /// <summary>A page's address and when it was seen: "https://… (accessed January 10, 2025)"; null where there is neither.</summary>
    static string? Online(Source source, Words words) =>
        Spaced(source["URL"], Date(source["YearAccessed"], source["MonthAccessed"], source["DayAccessed"], words) is { } accessed
            ? "(" + words.Accessed + " " + accessed + ")" : null);

    /// <summary>Parts apart by a comma ("Old Vic, London"); null where there are none.</summary>
    static string? Listed(params string?[] parts) => parts.Any(p => p != null) ? string.Join(", ", parts.Where(p => p != null)) : null;

    /// <summary>Parts apart by a space; null where there are none.</summary>
    static string? Spaced(params string?[] parts) => parts.Any(p => p != null) ? string.Join(" ", parts.Where(p => p != null)) : null;

    /// <summary>
    /// A date as Chicago writes it, the month as the source has it: "June 5, 2021" (German "5. June 2021"), "June 2021", "2021"; null
    /// without a year.
    /// </summary>
    static string? Date(string? year, string? month, string? day, Words words)
    {
        if (year == null)
            return null;
        if (month == null)
            return year;
        if (day == null)
            return month + " " + year;
        return words.German ? day + ". " + month + " " + year : month + " " + day + ", " + year;
    }

    /// <summary>The period that closes an element: none where its text already ends in a stop ("et al.", "n.d.", a question).</summary>
    static string Stop(string text) => text.EndsWith('.') || text.EndsWith('?') || text.EndsWith('!') ? "" : ".";

    /// <summary>An entry's elements as Word strings them: one after another apart by a space, each closed by a period.</summary>
    sealed class Line
    {
        public List<Piece> Pieces { get; } = [];

        void Next()
        {
            if (Pieces.Count > 0)
                Pieces.Add(new Piece(" "));
        }

        /// <summary>Plain text, closed by a period; nothing where there is none.</summary>
        public void Text(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            Next();
            Pieces.Add(new Piece(text + Stop(text)));
        }

        /// <summary>A title in italics, its period too ("*Six Authors Write.*"), or none where <paramref name="stop"/> is false.</summary>
        public void Italic(string? title, bool stop = true)
        {
            if (title == null)
                return;
            Next();
            Pieces.Add(new Piece(stop ? title + Stop(title) : title, Italic: true));
        }

        /// <summary>
        /// A title in italics within plain text that goes with it ("In " … ", by Lisa Hart, ix-xii"), the period upright; what follows
        /// the title alone where there is none.
        /// </summary>
        public void Within(string before, string? title, string? after)
        {
            if (title == null)
            {
                Text(after?.TrimStart(',', ' '));
                return;
            }
            Next();
            if (before.Length > 0)
                Pieces.Add(new Piece(before));
            Pieces.Add(new Piece(title, Italic: true));
            if (after + Stop(string.IsNullOrEmpty(after) ? title : after) is { Length: > 0 } closing)
                Pieces.Add(new Piece(closing));
        }

        /// <summary>A title in quotation marks, the period inside ("\"Feedback at scale.\"", German „…“).</summary>
        public void Quoted(string? title, Words words)
        {
            if (title == null)
                return;
            Next();
            Pieces.Add(new Piece(words.Open + title + Stop(title) + words.Close));
        }
    }
}
