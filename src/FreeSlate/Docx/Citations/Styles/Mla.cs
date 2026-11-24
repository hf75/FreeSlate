using System.Globalization;

namespace FreeSlate.Docx.Citations.Styles;

/// <summary>
/// Word's MLA style (MLA Seventh Edition, see CitationStyle) as Word formats it, measured against Word's own results. A citation names
/// its sources by their authors' last names, up to three ("(Lee, Garcia and Novak)"), adds the title where another source has the same
/// author ("(Smith, Learning with Machines)"), the volume and pages after a space ("(Kant 3:12)"); several sources stand apart by "; ".
/// The works cited: every source in the list, sorted by their first words, with a hanging indent; the first author inverted, more
/// than three the first and "et al."; the same author as the entry before a dash ("—."); each kind of source with its elements in
/// Word's order, each closed by a period. English, or German words where the field asks for them (the source's own text as it is).
/// </summary>
public sealed class Mla : CitationStyle
{
    public override string Name => "MLA";

    public override string File => @"\MLASeventhEditionOfficeOnline.xsl";

    public override string? Version => null;

    /// <summary>
    /// A citation as Word writes it: in parentheses each source's prefix, its author's last names (its title where it has no author or
    /// \n suppresses it), its title after a comma where another source has the same author (not with \t), its volume and pages after a
    /// space ("3:12"), its suffix; the sources apart by "; ". MLA has no year, so \y changes nothing.
    /// </summary>
    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context)
    {
        var words = Words.For(request.Lcid);
        var authors = context.Sources.Sources.Select(s => (s.Tag, Author: Author(s, context, words))).ToList();
        var parts = new List<string>();
        foreach (var cited in request.Sources)
        {
            var source = cited.Source;
            var author = Author(source, context, words);
            var title = cited.NoTitle ? null : source["Title"];
            // Another source of the same author: the title tells them apart.
            var shared = authors.Any(a => a.Author == author && !a.Tag.Equals(source.Tag, StringComparison.OrdinalIgnoreCase));
            var text = cited.NoAuthor || author == null ? title : shared && title != null ? author + ", " + title : author;
            var locator = cited.Volume != null && cited.Pages != null ? cited.Volume + ":" + cited.Pages : cited.Volume ?? cited.Pages;
            if (locator != null)
                text = text == null ? locator : text + " " + locator;
            parts.Add(cited.Prefix + text + cited.Suffix);
        }
        return [new Piece("(" + string.Join("; ", parts) + ")")];
    }

    /// <summary>
    /// The works cited as Word lays them out: all sources sorted by the names or title they begin with (letters' case aside), then by
    /// title and year; a hanging indent; an entry whose author is the one of the entry before begins with a dash instead ("—.").
    /// </summary>
    public override BibliographyList Bibliography(CitationContext context, int lcid)
    {
        var words = Words.For(lcid);
        var order = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);
        var sorted = context.Sources.Sources.Select(s => (Source: s, Lead: Lead(s, words)))
            .OrderBy(e => e.Lead?.Names ?? e.Source["Title"] ?? "", order).ThenBy(e => e.Source["Title"] ?? "", order)
            .ThenBy(e => e.Source["Year"] ?? "", order).ToList();
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

    /// <summary>The words MLA writes, English or German.</summary>
    sealed record Words(bool German, string And, string NoDate, string Editor, string Editors, string EditedBy, string TranslatedBy,
        string Volume, string Volumes, string Number, string With, string By, string DirectedBy, string Open, string Close)
    {
        public static Words For(int lcid) => Common.German(lcid)
            ? new(true, "und", "kein Datum", "Hrsg.", "Hrsg.", "Hrsg.", "Übers.", "Bd.", "Bde.", "Nr.", "mit", "Von", "Reg.", "„", "“")
            : new(false, "and", "n.d.", "ed.", "eds.", "Ed.", "Trans.", "Vol.", "vols.", "No.", "with", "By", "Dir.", "\"", "\"");
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
    /// A source's author as its citations name it: a corporate name, or up to three last names ("Lee, Garcia and Novak"); a single
    /// person's initials before the name where another source's first author has that last name with other given names ("A. Lee").
    /// Null where the source names no one.
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
        return Joined([.. who.People.Take(3).Select(Surname)], words);
    }

    /// <summary>Names one after another as MLA joins them: "A and B", "A, B and C".</summary>
    static string Joined(IReadOnlyList<string> names, Words words) =>
        names.Count < 2 ? string.Join("", names) : string.Join(", ", names.Take(names.Count - 1)) + " " + words.And + " " + names[^1];

    /// <summary>A person as the first of an entry's names: "Miller, Anna B.".</summary>
    static string Inverted(Person person) => string.Join(", ", new[] { person.Last, Common.Given(person) }.Where(n => n.Length > 0));

    /// <summary>A person in their names' order: "Anna B. Miller".</summary>
    static string Upright(Person person) => string.Join(" ", new[] { Common.Given(person), person.Last }.Where(n => n.Length > 0));

    /// <summary>The name a citation gives a person by: the last name, the given names where there is none.</summary>
    static string Surname(Person person) => person.Last.Length > 0 ? person.Last : Common.Given(person);

    /// <summary>
    /// The names an entry begins with: the first person inverted, the others upright ("Lee, Kim, Maria Garcia and Petr Novak"), more
    /// than three the first and "et al."; a corporate name as it is.
    /// </summary>
    static string? Names(Contributors? who, Words words)
    {
        if (who == null)
            return null;
        if (who.Corporate != null)
            return who.Corporate;
        if (who.People.Count > 3)
            return Inverted(who.People[0]) + ", et al.";
        return Joined([.. who.People.Select((p, k) => k == 0 ? Inverted(p) : Upright(p))], words);
    }

    /// <summary>Names upright, as an entry gives editors, translators, directors ("Max Weber and Ina Klein"); null where there are none.</summary>
    static string? UprightNames(Contributors? who, Words words) =>
        who == null ? null : who.Corporate ?? Joined([.. who.People.Select(Upright)], words);

    /// <summary>The names an entry begins with (compared with the entry before) and what follows them (", ed.").</summary>
    sealed record Leading(string Names, string Role = "");

    /// <summary>
    /// What an entry begins with: its authors; a book's editors where it has no author (", ed."); an interview's interviewee, a
    /// patent's inventor, a recording's performer (its composer where it has none), a work of art's artist. Films, performances and
    /// cases begin with their titles.
    /// </summary>
    static Leading? Lead(Source source, Words words)
    {
        Contributors? who;
        switch (source.Type)
        {
            case "Book":
                if (source.Role("Author") is null && source.Role("Editor") is { } editors)
                    return new Leading(Names(editors, words)!, ", " + (editors.People.Count > 1 ? words.Editors : words.Editor));
                who = source.Role("Author");
                break;
            case "Film" or "Performance" or "Case":
                return null;
            case "Interview":
                who = source.Role("Interviewee");
                break;
            case "Patent":
                who = source.Role("Inventor");
                break;
            case "SoundRecording":
                who = source.Role("Performer") ?? source.Role("Composer");
                break;
            case "Art":
                who = source.Role("Artist");
                break;
            default:
                who = source.Role("Author");
                break;
        }
        return Names(who, words) is { } names ? new Leading(names) : null;
    }

    /// <summary>
    /// An entry's elements in Word's order for its kind of source, each where the source has it, closed by a period (none added where
    /// it already ends in one); titles of books, journals, sites and works in italics, of articles, chapters and songs in quotation marks.
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
                line.Text(Publication(source["City"], source["Publisher"], year));
                line.Text(source["Medium"]);
                line.Text(source["NumberVolumes"] is { } volumes ? volumes + " " + words.Volumes : null);
                break;
            case "BookSection":
                line.Quoted(source["Title"], words);
                line.Text(Names(source.Role("BookAuthor"), words));
                line.Italic(source["BookTitle"]);
                line.Text(Prefixed(words.EditedBy, UprightNames(source.Role("Editor"), words)));
                line.Text(Prefixed(words.TranslatedBy, UprightNames(source.Role("Translator"), words)));
                line.Text(source["Edition"]);
                line.Text(Prefixed(words.Volume, source["Volume"]));
                line.Text(Publication(source["City"], source["Publisher"], year));
                line.Text(source["Pages"]);
                line.Text(source["Medium"]);
                break;
            case "JournalArticle":
            {
                line.Quoted(source["Title"], words);
                // "*Journal of Learning* 12.3 (2021): 45-67."
                var volume = source["Volume"] is { } v ? v + (source["Issue"] is { } i ? "." + i : "") : source["Issue"];
                var pages = source["Pages"] is { } p ? ": " + p : "";
                line.Italic(source["JournalName"], (volume == null ? "" : " " + volume) + " (" + year + ")" + pages);
                line.Text(source["Medium"]);
                break;
            }
            case "ArticleInAPeriodical":
                line.Quoted(source["Title"], words);
                // "*Die Zeit* 14 March 2022: 12."
                line.Italic(source["PeriodicalTitle"], " " + date + (source["Pages"] is { } periodicalPages ? ": " + periodicalPages : ""));
                line.Text(source["Medium"]);
                break;
            case "ConferenceProceedings":
                line.Quoted(source["Title"], words);
                line.Italic(source["ConferenceName"]);
                line.Text(Prefixed(words.EditedBy, UprightNames(source.Role("Editor"), words)));
                line.Text(Publication(source["City"], source["Publisher"], year));
                line.Text(source["Pages"]);
                line.Text(source["Medium"]);
                break;
            case "Report":
                line.Italic(source["Title"]);
                line.Text(source["ThesisType"]);
                line.Text(source["Institution"]);
                line.Text(Publication(source["City"], source["Publisher"], year));
                line.Text(source["Medium"]);
                break;
            case "InternetSite":
                line.Italic(source["Title"]);
                line.Text(date);
                line.Text(source["Medium"]);
                line.Text(Date(source["YearAccessed"], source["MonthAccessed"], source["DayAccessed"], words));
                line.Text(source["URL"] is { } siteUrl ? "<" + siteUrl + ">" : null);
                break;
            case "DocumentFromInternetSite":
                line.Quoted(source["Title"], words);
                line.Text(date);
                line.ItalicClosed(source["InternetSiteTitle"]);
                line.Text(source["Medium"]);
                line.Text(Date(source["YearAccessed"], source["MonthAccessed"], source["DayAccessed"], words));
                line.Text(source["URL"] is { } documentUrl ? "<" + documentUrl + ">" : null);
                break;
            case "Film":
                line.Italic(source["Title"]);
                line.Text(Prefixed(words.DirectedBy, UprightNames(source.Role("Director"), words)));
                line.Text(source["ProductionCompany"]);
                line.Text(source["Distributor"] is { } distributor ? distributor + ", " + year : year);
                line.Text(source["Medium"]);
                break;
            case "Interview":
                line.Text(source["Title"]);
                line.Text(Prefixed(words.With, UprightNames(source.Role("Interviewer"), words)));
                line.Text(source["Broadcaster"]);
                line.Text(source["Station"]);
                line.Text(date);
                line.Text(source["Medium"]);
                break;
            case "Patent":
                line.Text(source["Title"]);
                // "United States: Patent 10123456."
                var patent = "Patent" + (source["PatentNumber"] is { } number ? " " + number : "");
                line.Text(source["CountryRegion"] is { } country ? country + ": " + patent : patent);
                line.Text(date);
                line.Text(source["Medium"]);
                break;
            case "Case":
                line.Text(source["Title"]);
                line.Text(Prefixed(words.Number, source["CaseNumber"]));
                line.Text(source["Court"]);
                line.Text(date);
                line.Text(source["Medium"]);
                break;
            case "SoundRecording":
                line.Quoted(source["Title"], words);
                line.Italic(source["AlbumTitle"]);
                if (source.Role("Performer") != null)
                    line.Text(Prefixed(words.By, UprightNames(source.Role("Composer"), words)));
                line.Text(Publication(source["City"], source["ProductionCompany"], year));
                line.Text(source["Medium"]);
                break;
            case "Performance":
                line.Italic(source["Title"]);
                line.Text(Prefixed(words.By, UprightNames(source.Role("Writer"), words)));
                line.Text(Prefixed(words.DirectedBy, UprightNames(source.Role("Director"), words)));
                line.Text(Listed(source["Theater"], source["City"]));
                line.Text(date);
                line.Text(source["Medium"]);
                break;
            case "Art":
                line.Italic(source["Title"]);
                line.Text(Listed(source["Institution"], source["City"]));
                line.Text(source["Medium"]);
                break;
            case "ElectronicSource":
                line.Italic(source["Title"]);
                line.Text(Publication(source["City"], source["Publisher"], year));
                line.Text(source["Medium"]);
                break;
            default:
                line.Quoted(source["Title"], words);
                line.Text(Publication(source["City"], source["Publisher"], year));
                line.Text(source["Medium"]);
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

    /// <summary>Parts apart by a comma ("Old Vic, London"); null where there are none.</summary>
    static string? Listed(params string?[] parts) => parts.Any(p => p != null) ? string.Join(", ", parts.Where(p => p != null)) : null;

    /// <summary>
    /// A date as MLA writes it, the day before the month as the source has it ("5 June 2021", German "5. June 2021", "June 2021",
    /// "2021"); null without a year.
    /// </summary>
    static string? Date(string? year, string? month, string? day, Words words)
    {
        if (year == null)
            return null;
        if (month == null)
            return year;
        return (day == null ? "" : day + (words.German ? ". " : " ")) + month + " " + year;
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

        /// <summary>A title in italics, then plain text that goes with it (" 12.3 (2021): 45-67"), the period upright.</summary>
        public void Italic(string? title, string after = "")
        {
            if (title == null)
            {
                Text(after.TrimStart() is { Length: > 0 } rest ? rest : null);
                return;
            }
            Next();
            Pieces.Add(new Piece(title, Italic: true));
            if (after + Stop(after.Length > 0 ? after : title) is { Length: > 0 } closing)
                Pieces.Add(new Piece(closing));
        }

        /// <summary>A title in italics with its period ("*Education Review.*").</summary>
        public void ItalicClosed(string? title)
        {
            if (title == null)
                return;
            Next();
            Pieces.Add(new Piece(title + Stop(title), Italic: true));
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
