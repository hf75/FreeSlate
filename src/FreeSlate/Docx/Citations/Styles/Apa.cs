using System.Globalization;

namespace FreeSlate.Docx.Citations.Styles;

/// <summary>
/// Word's APA style, sixth edition (Word's default; see CitationStyle), as Word formats it: citations "(Smith, 2020, p. 33)" in plain
/// text, the reference list sorted by its entries' first words, each with a hanging indent, the date in parentheses after the names and
/// the title italic where the work stands on its own ("Smith, J. (2020). *Learning with Machines.* Berlin: Springer.", here and below
/// *…* in italics, its period too). English and German (\l 1031) differ in their words only, and in how dates read.
/// </summary>
public sealed class Apa : CitationStyle
{
    public override string Name => "APA";

    public override string File => @"\APASixthEditionOfficeOnline.xsl";

    public override string? Version => "6";

    /// <summary>
    /// A citation as Word writes it: its sources in parentheses, separated by semicolons, each its prefix (\f), the names, the title
    /// where another source has the same names and year ("Smith, Learning with Machines, 2020", where APA would add letters to the year),
    /// the year, the page or volume and its suffix (\s), separated by commas. \n puts the title in the names' place, \y leaves out the
    /// year, \t the title.
    /// </summary>
    public override IReadOnlyList<Piece> Cite(CitationRequest request, CitationContext context)
    {
        var words = Common.German(request.Lcid) ? Words.German : Words.English;
        return [new Piece("(" + string.Join("; ", request.Sources.Select(c => Cite(c, context.Sources, words))) + ")")];
    }

    /// <summary>
    /// The reference list as Word writes it: every source of the document, sorted by the names they begin with (the title where there
    /// are none), then by year and title, with a hanging indent.
    /// </summary>
    public override BibliographyList Bibliography(CitationContext context, int lcid)
    {
        var words = Common.German(lcid) ? Words.German : Words.English;
        var order = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);
        return new([.. context.Sources.Sources.OrderBy(Sorted, order).ThenBy(s => s["Year"] ?? "", order).ThenBy(s => s["Title"] ?? "", order)
            .Select(s => new BibliographyEntry(Entry(s, words)))], BibliographyLayout.Hanging);
    }

    /// <summary>The words APA writes, in English and in German as Word has them (a month's name stays as the source gives it).</summary>
    sealed record Words(bool IsGerman, string NoDate, string Page, string Pages, string Volume, string Edition, string Editor, string Editors,
        string Translator, string Director, string Directors, string Interviewer, string Interviewers, string MotionPicture, string RecordedBy,
        string On, string In, string PatentNumber)
    {
        public static readonly Words English = new(false, "n.d.", "p.", "pp.", "Vol.", "ed.", "Ed.", "Eds.", "Trans.", "Director", "Directors",
            "Interviewer", "Interviewers", "Motion Picture", "Recorded by", "On", "In", "Patent No.");

        public static readonly Words German = new(true, "kein Datum", "S.", "S.", "Bd.", "Ausg.", "Hrsg.", "Hrsg.", "Übers.", "Regisseur", "Regisseure",
            "Interviewer", "Interviewer", "Kinofilm", "Aufgezeichnet von", "Auf", "In", "Patentnr.");
    }

    /// <summary>One source of a citation: "see Miller & Brown, 2019", "Smith, Learning with Machines, 2020, p. 33", "Kant, 2011, 3:12".</summary>
    static string Cite(Cited cited, SourceList all, Words words)
    {
        var source = cited.Source;
        var parts = new List<string>();
        var (names, titled) = cited.NoAuthor ? (source["Title"] ?? "", true) : Named(source, all, words);
        if (names.Length > 0)
            parts.Add(names);
        if (!titled && !cited.NoTitle && source["Title"] is { } title && Twin(source, names, all, words))
            parts.Add(title);
        // Web pages without a date say so; other sources leave the year out ("(Beowulf)").
        if (!cited.NoYear && (source["Year"] ?? (source.Type is "InternetSite" or "DocumentFromInternetSite" ? words.NoDate : null)) is { } year)
            parts.Add(year);
        var pages = cited.Pages is { Length: > 0 } p ? p : null;
        var volume = cited.Volume is { Length: > 0 } v ? v : null;
        if (volume != null && pages != null)
            parts.Add(volume + ":" + pages);
        else if (pages != null)
            parts.Add(PageWord(pages, words) + " " + pages);
        else if (volume != null)
            parts.Add(words.Volume + " " + volume);
        return cited.Prefix + string.Join(", ", parts) + cited.Suffix;
    }

    /// <summary>
    /// The names a citation gives a source, and whether they are its title: last names, "Miller & Brown" for two, all of them up to five
    /// ("Lee, Garcia, & Novak"), from six the first one's and "et al." ("Adams, et al."); a corporate name; a patent's country and number
    /// ("United States Patent No. 10123456"); the title where no one is named. A single author whose last name another source's first
    /// author has, with other given names, gets initials as Word writes them, a space after them ("Lee A. , 2019").
    /// </summary>
    static (string Names, bool Titled) Named(Source source, SourceList all, Words words)
    {
        if (source.Type == "Patent" && Patent(source, words) is { } patent)
            return (patent, false);
        if (Creator(source) is not { } creator)
            return (source["Title"] ?? "", true);
        if (creator.Names.Corporate is { } corporate)
            return (corporate, false);
        var people = creator.Names.People;
        return (people.Count switch
        {
            1 when Common.Initials(people[0]) is { Length: > 0 } initials && Initialed(source, people[0], all) => people[0].Last + " " + initials + " ",
            1 => people[0].Last,
            2 => people[0].Last + " & " + people[1].Last,
            < 6 => string.Join(", ", people.Take(people.Count - 1).Select(p => p.Last)) + ", & " + people[^1].Last,
            _ => people[0].Last + ", et al.",
        }, false);
    }

    /// <summary>Whether another source's first author (or what stands first in it) has this person's last name and other given names.</summary>
    static bool Initialed(Source source, Person person, SourceList all) =>
        all.Sources.Any(other => other != source && Creator(other) is { Names.People: [var first, ..] }
                                 && string.Equals(first.Last, person.Last, StringComparison.InvariantCultureIgnoreCase)
                                 && !string.Equals(Common.Given(first), Common.Given(person), StringComparison.InvariantCultureIgnoreCase));

    /// <summary>Whether another source has the same year and a citation would name it alike: Word then tells them apart by their titles.</summary>
    static bool Twin(Source source, string names, SourceList all, Words words) =>
        all.Sources.Any(other => other != source && other["Year"] == source["Year"]
                                 && Named(other, all, words) is (var others, false) && string.Equals(others, names, StringComparison.InvariantCultureIgnoreCase));

    /// <summary>
    /// Who a source is by as APA names it, and in which role: a film's director, an interview's interviewee, a patent's inventor, a
    /// recording's composer (else its performer), a performance's writer, a work of art's artist, otherwise its author or, but for a
    /// chapter (whose editors are the book's), its editor; null where no one is named (a court case never is).
    /// </summary>
    static (Contributors Names, string Role)? Creator(Source source)
    {
        string[] roles = source.Type switch
        {
            "Film" => ["Director"],
            "Interview" => ["Interviewee"],
            "Patent" => ["Inventor"],
            "SoundRecording" => ["Composer", "Performer"],
            "Performance" => ["Writer"],
            "Art" => ["Artist"],
            "Case" => [],
            "BookSection" => ["Author"],
            _ => ["Author", "Editor"],
        };
        foreach (var role in roles)
        {
            if (source.Role(role) is { } names)
                return (names, role);
        }
        return null;
    }

    /// <summary>What a reference list entry sorts by: the names it begins with as written, the title where there are none.</summary>
    static string Sorted(Source source) => source.Type != "Case" && Creator(source) is { } creator ? Inverted(creator.Names) : source["Title"] ?? "";

    /// <summary>
    /// A reference list entry: the names (and what they did where they are not the authors: "(Ed.)", "(Director)") or the title where
    /// there are none, the date in parentheses ("n.d." where there is none), then what the kind of source tells (the title, where it was
    /// published, by whom …), each part ending in a period. A court case and a patent go their own ways.
    /// </summary>
    static List<Piece> Entry(Source source, Words words)
    {
        if (source.Type == "Case")
            return [new Piece(Case(source))];
        var creator = Creator(source);
        List<Piece> entry = creator is { } named ? [new Piece(Period(Inverted(named.Names) + Role(named, words)))] : Title(source, words);
        entry.Add(new Piece((entry.Count > 0 ? " (" : "(") + (Date(source, words) ?? words.NoDate) + ")."));
        if (source.Type == "Patent")
        {
            // Word sets the patent's country and number in italics after the date, a space before them and after them, its title left out.
            if (Patent(source, words) is { } patent)
                entry.AddRange([new Piece(" " + Period(patent), Italic: true), new Piece(" ")]);
            return entry;
        }
        foreach (var part in Parts(source, words, creator == null))
        {
            entry.Add(new Piece(" "));
            entry.AddRange(part);
        }
        return entry;
    }

    /// <summary>
    /// The parts of an entry after its date, as Word writes them for each kind of source, the title first where names lead the entry:
    /// <list type="bullet">
    /// <item>a book: "*Title* (2 ed., Vol. 3). (R. Pevear, & L. Volokhonsky, Trans.) City: Publisher."</item>
    /// <item>a chapter: "Title. In M. Weber, & I. Klein (Eds.), *Book* (pp. 101-120). City: Publisher.", its book's authors in the editors'
    /// place where it has no editors ("In L. Hart, *Book* …")</item>
    /// <item>a journal article: "Title. *Journal, 12*(3), 45-67. doi:…"; a periodical's: "Title. *Periodical*, p. 12."</item>
    /// <item>a conference paper: "Title. *Conference* (pp. 1-10). City: Publisher."</item>
    /// <item>a report: "*Title.* Report type, Institution, City: Publisher."</item>
    /// <item>a web site: "*Title*. Retrieved January 10, 2025, from Site: URL"; a document from one the same, its title's period italic</item>
    /// <item>an electronic source or another kind: "Title. City: Publisher."</item>
    /// <item>a work of art: "*Title.* Institution, City."</item>
    /// <item>a recording: "Title [Recorded by M. Riesman]. On *Album* [CD]. City: Production company."</item>
    /// <item>a performance: "*Title.* (R. Cohen, Director) Theater, City."</item>
    /// <item>a film: "*Title* [Motion Picture]. Distributor."</item>
    /// <item>an interview: "Title. (M. Rossi, Interviewer) Broadcaster. Station."</item>
    /// </list>
    /// </summary>
    static List<List<Piece>> Parts(Source source, Words words, bool titled)
    {
        var parts = new List<List<Piece>>();
        void Add(params Piece[] pieces)
        {
            if (pieces.Any(p => p.Text.Length > 0))
                parts.Add([.. pieces.Where(p => p.Text.Length > 0)]);
        }
        void Plain(string? text)
        {
            if (!string.IsNullOrEmpty(text))
                parts.Add([new Piece(text)]);
        }
        if (!titled && Title(source, words) is { Count: > 0 } title)
            parts.Add(title);
        switch (source.Type)
        {
            case "Book":
                if (source.Role("Translator") is { } translators)
                    Plain("(" + Direct(translators) + ", " + words.Translator + ")");
                Plain(Closed(Place(source["City"], source["Publisher"])));
                break;
            case "BookSection":
                var editors = source.Role("Editor");
                var bookAuthors = source.Role("BookAuthor");
                var by = editors != null ? Direct(editors) + " (" + (Plural(editors) ? words.Editors : words.Editor) + "), "
                    : bookAuthors != null ? Direct(bookAuthors) + ", " : "";
                if (source["BookTitle"] is { } book)
                {
                    var details = Joined(", ", Edition(source, words), VolumeOf(source, words), PagesOf(source, words));
                    Add(new Piece(words.In + " " + by), new Piece(details == null ? Period(book) : book, Italic: true),
                        new Piece(details == null ? "" : " (" + details + ")."));
                }
                else if (by.Length > 0)
                    Plain(words.In + " " + Period(by.TrimEnd(',', ' ')));
                Plain(Closed(Place(source["City"], source["Publisher"])));
                break;
            case "JournalArticle":
                var journal = Joined(", ", source["JournalName"], source["Volume"]);
                var issue = source["Issue"] is { } number ? "(" + number + ")" : "";
                var pages = source["Pages"] is { } range ? ", " + range : "";
                if (journal != null || issue.Length + pages.Length > 0)
                    Add(new Piece(journal ?? "", Italic: true), new Piece(issue + (journal == null && issue.Length == 0 ? pages.TrimStart(',', ' ') : pages) + "."));
                if (source["DOI"] is { } doi)
                    Plain("doi:" + doi);
                break;
            case "ArticleInAPeriodical":
                var at = source["Pages"] is { } page ? PageWord(page, words) + " " + page : null;
                if (source["PeriodicalTitle"] is { } periodical)
                    Add(new Piece(periodical, Italic: true), new Piece((at != null ? ", " + at : "") + "."));
                else
                    Plain(Closed(at));
                break;
            case "ConferenceProceedings":
                if (source["ConferenceName"] is { } conference)
                {
                    var details = Joined(", ", VolumeOf(source, words), PagesOf(source, words));
                    Add(new Piece(details == null ? Period(conference) : conference, Italic: true), new Piece(details == null ? "" : " (" + details + ")."));
                }
                Plain(Closed(Place(source["City"], source["Publisher"])));
                break;
            case "Report":
                Plain(Closed(Joined(", ", source["ThesisType"], source["Institution"], Place(source["City"], source["Publisher"]))));
                break;
            case "InternetSite" or "DocumentFromInternetSite":
                Plain(Retrieved(source, words));
                break;
            case "Art":
                Plain(Closed(Joined(", ", source["Institution"], source["City"])));
                break;
            case "SoundRecording":
                if (source["AlbumTitle"] is { } album)
                    Add(new Piece(words.On + " "), new Piece(album, Italic: true), new Piece((source["Medium"] is { } medium ? " [" + medium + "]" : "") + "."));
                Plain(Closed(Place(source["City"], source["ProductionCompany"] ?? source["Publisher"])));
                break;
            case "Performance":
                if (source.Role("Director") is { } directors)
                    Plain("(" + Direct(directors) + ", " + (Plural(directors) ? words.Directors : words.Director) + ")");
                Plain(Closed(Joined(", ", source["Theater"], source["City"])));
                break;
            case "Film":
                Plain(Closed(source["Distributor"] ?? source["ProductionCompany"]));
                break;
            case "Interview":
                if (source.Role("Interviewer") is { } interviewers)
                    Plain("(" + Direct(interviewers) + ", " + (Plural(interviewers) ? words.Interviewers : words.Interviewer) + ")");
                Plain(Closed(source["Broadcaster"]));
                Plain(Closed(source["Station"]));
                break;
            default:
                Plain(Closed(Place(source["City"], source["Publisher"])));
                break;
        }
        return parts;
    }

    /// <summary>
    /// A source's title as its entry writes it, with what belongs to it: italic, its period too, for a work on its own (a book, a report,
    /// a document from a web site, a work of art, a performance), a book's edition and volume after it ("*Title* (2 ed.)."); a web site's
    /// italic without its period; a film's with its kind ("*Title* [Motion Picture]."); plain for a part of another work (a chapter, an
    /// article, a paper, an interview …), a recording's with its performers ("Title [Recorded by M. Riesman].").
    /// </summary>
    static List<Piece> Title(Source source, Words words)
    {
        if (source["Title"] is not { } title)
            return [];
        switch (source.Type)
        {
            case "Book":
                var details = Joined(", ", Edition(source, words), VolumeOf(source, words));
                return details == null ? [new Piece(Period(title), Italic: true)] : [new Piece(title, Italic: true), new Piece(" (" + details + ").")];
            case "Report" or "DocumentFromInternetSite" or "Art" or "Performance":
                return [new Piece(Period(title), Italic: true)];
            case "InternetSite":
                return [new Piece(title, Italic: true), new Piece(Period(title)[title.Length..])];
            case "Film":
                return [new Piece(title, Italic: true), new Piece(" [" + words.MotionPicture + "].")];
            case "SoundRecording" when source.Role("Composer") != null && source.Role("Performer") is { } performers:
                return [new Piece(title + " [" + words.RecordedBy + " " + Direct(performers) + "].")];
            default:
                return [new Piece(Period(title))];
        }
    }

    /// <summary>A court case as Word's APA writes it, plain: "Brown v. Board of Education, 347 U.S. 483 (Supreme Court 1954)."</summary>
    static string Case(Source source) =>
        Period(Joined(" ", Joined(", ", source["Title"], source["CaseNumber"]), Joined(" ", source["Court"], source["Year"]) is { } court ? "(" + court + ")" : null) ?? "");

    /// <summary>A patent's country and number as APA names it: "United States Patent No. 10123456" ("United States Patentnr. 10123456").</summary>
    static string? Patent(Source source, Words words) =>
        source["PatentNumber"] is { } number ? Joined(" ", source["CountryRegion"], words.PatentNumber, number) : null;

    /// <summary>
    /// Where a web source was found, as Word writes it: "Retrieved January 10, 2025, from TED: https://…" ("Abgerufen am 10. January 2025
    /// von TED: https://…"), the date it was accessed and the site's title as far as given, no period after the address.
    /// </summary>
    static string? Retrieved(Source source, Words words)
    {
        var (year, month, day) = (source["YearAccessed"], source["MonthAccessed"], source["DayAccessed"]);
        var accessed = month == null ? year
            : words.IsGerman ? (day != null ? day + ". " : "") + month + (year != null ? " " + year : "")
            : month + (day != null ? " " + day + (year != null ? "," : "") : "") + (year != null ? " " + year : "");
        var where = Joined(": ", source["InternetSiteTitle"], source["URL"]);
        if (accessed == null && where == null)
            return null;
        return words.IsGerman
            ? "Abgerufen" + (accessed != null ? " am " + accessed : "") + (where != null ? " von " + where : "")
            : "Retrieved" + (accessed != null ? " " + accessed + (where != null ? "," : "") : "") + (where != null ? " from " + where : "");
    }

    /// <summary>
    /// A source's date as the entry writes it in parentheses: the year, with the month and day where given ("2022, March 14"; in German
    /// "14. March 2022"); null where there is no year.
    /// </summary>
    static string? Date(Source source, Words words)
    {
        if (source["Year"] is not { } year)
            return null;
        if (source["Month"] is not { } month)
            return year;
        var day = source["Day"];
        return words.IsGerman ? (day != null ? day + ". " : "") + month + " " + year : year + ", " + month + (day != null ? " " + day : "");
    }

    /// <summary>
    /// Names as an entry begins with them: last name, comma and initials ("Miller, A. B."), a comma and an ampersand before the last
    /// even of two ("Miller, A. B., & Brown, T."); seven at most, from eight the first six, an ellipsis and the last ("…, Gray, F., . . .
    /// Ives, H."); a corporate name as it is.
    /// </summary>
    static string Inverted(Contributors names)
    {
        if (names.Corporate != null)
            return names.Corporate;
        var list = names.People.Select(p => Common.Initials(p) is { Length: > 0 } initials ? p.Last + ", " + initials : p.Last).ToList();
        return list.Count switch
        {
            1 => list[0],
            < 8 => string.Join(", ", list.Take(list.Count - 1)) + ", & " + list[^1],
            _ => string.Join(", ", list.Take(6)) + ", . . . " + list[^1],
        };
    }

    /// <summary>Names inside an entry (editors, translators, performers …): initials first ("M. Weber"), a comma and an ampersand before the last ("M. Weber, & I. Klein").</summary>
    static string Direct(Contributors names)
    {
        if (names.Corporate != null)
            return names.Corporate;
        var list = names.People.Select(p => Common.Initials(p) is { Length: > 0 } initials ? initials + " " + p.Last : p.Last).ToList();
        return list.Count == 1 ? list[0] : string.Join(", ", list.Take(list.Count - 1)) + ", & " + list[^1];
    }

    /// <summary>What the names leading an entry did where they are not its authors: " (Ed.)", " (Eds.)" (German " (Hrsg.)"), " (Director)".</summary>
    static string Role((Contributors Names, string Role) creator, Words words) => creator.Role switch
    {
        "Editor" => " (" + (Plural(creator.Names) ? words.Editors : words.Editor) + ")",
        "Director" => " (" + (Plural(creator.Names) ? words.Directors : words.Director) + ")",
        _ => "",
    };

    static bool Plural(Contributors names) => names.Corporate == null && names.People.Count > 1;

    /// <summary>"p." before one page, "pp." before a range or a list of them (German "S." before both).</summary>
    static string PageWord(string pages, Words words) => pages.IndexOfAny(['-', '–', ',']) >= 0 ? words.Pages : words.Page;

    static string? Edition(Source source, Words words) => source["Edition"] is { } edition ? edition + " " + words.Edition : null;

    static string? VolumeOf(Source source, Words words) => source["Volume"] is { } volume ? words.Volume + " " + volume : null;

    static string? PagesOf(Source source, Words words) => source["Pages"] is { } pages ? PageWord(pages, words) + " " + pages : null;

    /// <summary>A place of publication as APA writes it: "City: Publisher", either alone where the other is missing.</summary>
    static string? Place(string? city, string? publisher) => Joined(": ", city, publisher);

    /// <summary>Text ending in a period, none added after one or after a question or exclamation mark.</summary>
    static string Period(string text) => text.Length > 0 && text[^1] is '.' or '?' or '!' ? text : text + ".";

    static string? Closed(string? text) => string.IsNullOrEmpty(text) ? null : Period(text);

    /// <summary>The parts that are given, joined; null where none is.</summary>
    static string? Joined(string separator, params string?[] parts) =>
        parts.Where(p => !string.IsNullOrEmpty(p)).ToList() is { Count: > 0 } given ? string.Join(separator, given) : null;
}
