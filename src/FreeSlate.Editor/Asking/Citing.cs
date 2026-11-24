using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FreeSlate.Docx.Citations;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// Citations and bibliographies as the notation writes them and Word keeps them (see WordDocument.Citations): the sources a ```bibtex block
/// gives (its style=… Word's citation style: APA, MLA, Chicago, IEEE …), merged into the document's by their keys (Word's tags), each
/// BibTeX entry type one of Word's seventeen kinds; "[@key]" in the text a citation (Word's CITATION field, see Notation.CiteItems); a line
/// "{bibliography}" Word's bibliography of all the sources, made once the write is in. The citations' and the bibliographies' text set as
/// the style formats them at the write's end. Read back as written: citations as "[@key …]", the bibliography as one line, the sources
/// after the document in a ```bibtex block.
/// </summary>
static partial class Citing
{
    // BibTeX's entry types and Word's kinds, the first of each the one written.
    static readonly (string Bibtex, string Word)[] Kinds =
    [
        ("book", "Book"), ("mvbook", "Book"), ("incollection", "BookSection"), ("inbook", "BookSection"), ("bookinbook", "BookSection"),
        ("article", "JournalArticle"), ("magazine", "ArticleInAPeriodical"), ("newspaper", "ArticleInAPeriodical"), ("periodical", "ArticleInAPeriodical"),
        ("inproceedings", "ConferenceProceedings"), ("conference", "ConferenceProceedings"), ("proceedings", "ConferenceProceedings"),
        ("report", "Report"), ("techreport", "Report"), ("thesis", "Report"), ("phdthesis", "Report"), ("mastersthesis", "Report"),
        ("online", "InternetSite"), ("www", "InternetSite"), ("webpage", "InternetSite"), ("webdocument", "DocumentFromInternetSite"),
        ("movie", "Film"), ("film", "Film"), ("video", "Film"), ("interview", "Interview"), ("patent", "Patent"),
        ("jurisdiction", "Case"), ("case", "Case"), ("legal", "Case"), ("audio", "SoundRecording"), ("music", "SoundRecording"),
        ("recording", "SoundRecording"), ("performance", "Performance"), ("artwork", "Art"), ("image", "Art"), ("electronic", "ElectronicSource"),
        ("software", "ElectronicSource"), ("dataset", "ElectronicSource"), ("misc", "Misc"), ("unpublished", "Misc"), ("manual", "Misc"), ("booklet", "Misc"),
    ];

    // BibTeX's fields and Word's (those that depend on the kind aside: booktitle, journal, number, organization, author).
    static readonly (string Bibtex, string Word)[] FieldNames =
    [
        ("title", "Title"), ("year", "Year"), ("month", "Month"), ("day", "Day"), ("address", "City"), ("location", "City"), ("city", "City"),
        ("publisher", "Publisher"), ("edition", "Edition"), ("volume", "Volume"), ("volumes", "NumberVolumes"), ("pages", "Pages"),
        ("chapter", "ChapterNumber"), ("url", "URL"), ("doi", "DOI"), ("institution", "Institution"), ("school", "Institution"), ("type", "ThesisType"),
        ("isbn", "StandardNumber"), ("issn", "StandardNumber"), ("note", "Comments"), ("country", "CountryRegion"), ("court", "Court"),
        ("reporter", "Reporter"), ("distributor", "Distributor"), ("studio", "ProductionCompany"), ("broadcaster", "Broadcaster"),
        ("station", "Station"), ("medium", "Medium"), ("theater", "Theater"), ("theatre", "Theater"), ("album", "AlbumTitle"),
        ("website", "InternetSiteTitle"), ("sitename", "InternetSiteTitle"), ("shorttitle", "ShortTitle"), ("department", "Department"),
        ("state", "StateProvince"),
    ];

    // BibTeX's fields of people and Word's roles ("author" is the kind's own, see MainRole).
    static readonly (string Bibtex, string Word)[] RoleNames =
    [
        ("editor", "Editor"), ("translator", "Translator"), ("bookauthor", "BookAuthor"), ("director", "Director"), ("interviewer", "Interviewer"),
        ("interviewee", "Interviewee"), ("inventor", "Inventor"), ("composer", "Composer"), ("performer", "Performer"), ("conductor", "Conductor"),
        ("writer", "Writer"), ("artist", "Artist"), ("producer", "ProducerName"), ("compiler", "Compiler"), ("counsel", "Counsel"),
    ];

    /// <summary>The role a kind's first people have, which BibTeX's "author" gives (an interview's interviewee, a patent's inventor …).</summary>
    static string MainRole(string kind) => kind switch
    {
        "Interview" => "Interviewee",
        "Patent" => "Inventor",
        "SoundRecording" => "Composer",
        "Performance" => "Writer",
        "Art" => "Artist",
        "Film" => "Director",
        _ => "Author",
    };

    static readonly string[] EnglishMonths = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
    static readonly string[] GermanMonths = ["Januar", "Februar", "März", "April", "Mai", "Juni", "Juli", "August", "September", "Oktober", "November", "Dezember"];

    /// <summary>A month as BibTeX may give it (jan, 3, March) in the language of a language id; as given where it is none of those.</summary>
    static string Month(string value, int lcid)
    {
        var months = (lcid & 0x3FF) == 0x07 ? GermanMonths : EnglishMonths;
        var trimmed = value.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number is >= 1 and <= 12)
            return months[number - 1];
        var short3 = trimmed.Length >= 3 ? trimmed[..3].ToLowerInvariant() : trimmed.ToLowerInvariant();
        var at = Array.FindIndex(EnglishMonths, m => m[..3].Equals(short3, StringComparison.OrdinalIgnoreCase));
        return at >= 0 && trimmed.Length <= 4 ? months[at] : trimmed;
    }

    /// <summary>The CITATION field's instruction for a citation's sources, as Word writes it (see CitationRequest.Instruction).</summary>
    public static string Instruction(IReadOnlyList<Notation.CiteItem> items, int lcid)
    {
        var text = new StringBuilder(" CITATION");
        for (var k = 0; k < items.Count; k++)
        {
            var item = items[k];
            text.Append(k == 0 ? " " : "  \\m ").Append(item.Tag);
            static string Quoted(string value) => value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
            if (item.Pages != null)
                text.Append(" \\p ").Append(Quoted(item.Pages));
            if (item.Volume != null)
                text.Append(" \\v ").Append(Quoted(item.Volume));
            if (item.Prefix != null)
                text.Append(" \\f \"").Append(item.Prefix.Replace("\"", "", StringComparison.Ordinal)).Append('"');
            if (item.Suffix != null)
                text.Append(" \\s \"").Append(item.Suffix.Replace("\"", "", StringComparison.Ordinal)).Append('"');
            if (item.NoAuthor)
                text.Append(" \\n");
            if (item.NoYear)
                text.Append(" \\y");
            if (item.NoTitle)
                text.Append(" \\t");
            if (k == 0)
                text.Append(" \\l ").Append(lcid.ToString(CultureInfo.InvariantCulture));
        }
        return text.Append(' ').ToString();
    }

    /// <summary>A CITATION field's sources as the notation writes them (see Notation.CiteItems), read from its instruction; null where it names none.</summary>
    public static IReadOnlyList<Notation.CiteItem>? ItemsOf(string instruction)
    {
        var tokens = Tokens().Matches(instruction).Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Value).ToList();
        if (tokens.Count < 2 || !tokens[0].Equals("CITATION", StringComparison.OrdinalIgnoreCase))
            return null;
        var items = new List<Notation.CiteItem>();
        Notation.CiteItem? current = null;
        for (var k = 1; k < tokens.Count; k++)
        {
            string? Next() => k + 1 < tokens.Count ? tokens[++k] : null;
            switch (tokens[k].ToLowerInvariant())
            {
                case "\\l":
                    Next();
                    break;
                case "\\m":
                    if (current != null)
                        items.Add(current);
                    current = Next() is { } tag ? new Notation.CiteItem(tag) : null;
                    break;
                case "\\p" when current != null:
                    current = current with { Pages = Next() };
                    break;
                case "\\v" when current != null:
                    current = current with { Volume = Next() };
                    break;
                case "\\f" when current != null:
                    current = current with { Prefix = Next() is { } prefix ? prefix.EndsWith(' ') ? prefix : prefix + " " : null };
                    break;
                case "\\s" when current != null:
                    current = current with { Suffix = Next() };
                    break;
                case "\\n" when current != null:
                    current = current with { NoAuthor = true };
                    break;
                case "\\y" when current != null:
                    current = current with { NoYear = true };
                    break;
                case "\\t" when current != null:
                    current = current with { NoTitle = true };
                    break;
                default:
                    if (k == 1)
                        current = new Notation.CiteItem(tokens[k]);
                    break;
            }
        }
        if (current != null)
            items.Add(current);
        return items.Count > 0 ? items : null;
    }

    [GeneratedRegex("\"([^\"]*)\"|\\S+")]
    private static partial Regex Tokens();

    /// <summary>
    /// The sources of a BibTeX text: each "@type{key, field = {value}, field = "value", field = 2020}" a source of Word's kind for the type
    /// (other types Misc), its key the tag, its fields Word's (months and dates in the language given); what could not be read said.
    /// </summary>
    public static (List<Source> Sources, List<string> Problems) Parse(string text, int lcid)
    {
        var sources = new List<Source>();
        var problems = new List<string>();
        var k = 0;
        while ((k = text.IndexOf('@', k)) >= 0)
        {
            var open = text.IndexOfAny(['{', '('], k);
            if (open < 0)
                break;
            var type = text[(k + 1)..open].Trim().ToLowerInvariant();
            var close = Closing(text, open);
            if (close < 0)
            {
                problems.Add($"The BibTeX entry @{type} is not closed.");
                break;
            }
            k = close + 1;
            if (type is "comment" or "preamble" or "string")
                continue;
            var body = text[(open + 1)..close];
            var comma = body.IndexOf(',');
            var key = (comma < 0 ? body : body[..comma]).Trim();
            if (key.Length == 0 || key.Any(char.IsWhiteSpace))
            {
                problems.Add($"A BibTeX entry @{type} has no key (its first part, \"@{type}{{key, …}}\").");
                continue;
            }
            var kind = Kinds.FirstOrDefault(t => t.Bibtex == type).Word;
            if (kind == null)
            {
                problems.Add($"@{type} is none of the BibTeX types Word has a kind for; {key} was taken as misc.");
                kind = "Misc";
            }
            var fields = comma < 0 ? [] : Fields(body[(comma + 1)..]);
            sources.Add(SourceOf(key, kind, type, fields, lcid));
        }
        return (sources, problems);
    }

    /// <summary>Where the brace (or parenthesis) at <paramref name="open"/> is closed, braces within counted; -1 where it is not.</summary>
    static int Closing(string text, int open)
    {
        var (depth, closer) = (0, text[open] == '(' ? ')' : '}');
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\')
            {
                i++;
                continue;
            }
            if (c == text[open] || c == '{' && closer == ')')
                depth++;
            else if (c == closer || c == '}' && closer == ')')
            {
                depth--;
                if (depth == 0 && c == closer)
                    return i;
            }
        }
        return -1;
    }

    /// <summary>An entry's fields, "name = {value}" (or "value", or a word or number), in order; names in lower case, values as written.</summary>
    static List<(string Name, string Value)> Fields(string text)
    {
        var fields = new List<(string, string)>();
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && (char.IsWhiteSpace(text[i]) || text[i] == ','))
                i++;
            var equals = text.IndexOf('=', i);
            if (equals < 0)
                break;
            var name = text[i..equals].Trim().ToLowerInvariant();
            i = equals + 1;
            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;
            if (i >= text.Length)
                break;
            string value;
            if (text[i] == '{')
            {
                var end = Closing(text, i);
                if (end < 0)
                    end = text.Length - 1;
                value = text[(i + 1)..end];
                i = end + 1;
            }
            else if (text[i] == '"')
            {
                var end = i + 1;
                var depth = 0;
                while (end < text.Length && !(text[end] == '"' && depth == 0 && text[end - 1] != '\\'))
                {
                    depth += text[end] == '{' ? 1 : text[end] == '}' ? -1 : 0;
                    end++;
                }
                value = text[(i + 1)..Math.Min(end, text.Length)];
                i = end + 1;
            }
            else
            {
                var end = i;
                while (end < text.Length && text[end] != ',' && !char.IsWhiteSpace(text[end]))
                    end++;
                value = text[i..end];
                i = end;
            }
            if (name.Length > 0)
                fields.Add((name, value.Trim()));
        }
        return fields;
    }

    /// <summary>A source of Word's from a BibTeX entry's fields (see Parse).</summary>
    static Source SourceOf(string key, string kind, string type, List<(string Name, string Value)> fields, int lcid)
    {
        var roles = new Dictionary<string, Contributors>(StringComparer.Ordinal);
        var values = new List<KeyValuePair<string, string>>();
        void Set(string name, string value)
        {
            if (value.Length == 0)
                return;
            values.RemoveAll(v => v.Key == name);
            values.Add(new(name, value));
        }
        foreach (var (name, raw) in fields)
        {
            var value = Plain(raw);
            if (name == "author")
                roles[MainRole(kind)] = Names(raw);
            else if (RoleNames.FirstOrDefault(r => r.Bibtex == name).Word is { } role)
                roles[role] = Names(raw);
            else if (name is "booktitle")
                Set(kind == "ConferenceProceedings" ? "ConferenceName" : "BookTitle", value);
            else if (name is "journal" or "journaltitle")
                Set(kind == "ArticleInAPeriodical" ? "PeriodicalTitle" : "JournalName", value);
            else if (name is "number" or "issue")
                Set(kind switch { "Patent" => "PatentNumber", "Case" => "CaseNumber", "Report" => "StandardNumber", _ => "Issue" }, value);
            else if (name == "organization")
                Set(kind == "Report" ? "Institution" : "Publisher", value);
            else if (name == "month")
                Set("Month", Month(value, lcid));
            else if (name is "date" or "urldate" && DateText().Match(value) is { Success: true } date)
            {
                var accessed = name == "urldate" ? "Accessed" : "";
                Set("Year" + accessed, date.Groups[1].Value);
                if (date.Groups[2].Success)
                    Set("Month" + accessed, Month(date.Groups[2].Value, lcid));
                if (date.Groups[3].Success)
                    Set("Day" + accessed, int.Parse(date.Groups[3].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
            }
            else if (name == "pages")
                Set("Pages", value.Replace("--", "-", StringComparison.Ordinal).Replace("–", "-", StringComparison.Ordinal));
            else if (FieldNames.FirstOrDefault(f => f.Bibtex == name).Word is { } mapped)
                Set(mapped, value);
            else if (WordFields.FirstOrDefault(w => w.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } word)
                Set(word, value);
        }
        if (type is "phdthesis" && values.All(v => v.Key != "ThesisType"))
            Set("ThesisType", (lcid & 0x3FF) == 0x07 ? "Dissertation" : "Doctoral dissertation");
        if (type is "mastersthesis" && values.All(v => v.Key != "ThesisType"))
            Set("ThesisType", (lcid & 0x3FF) == 0x07 ? "Masterarbeit" : "Master's thesis");
        return new Source(key, kind, roles, values);
    }

    /// <summary>Word's fields a BibTeX entry may name as Word does ("productioncompany = {…}"), which BibTeX has no name of its own for.</summary>
    static readonly string[] WordFields =
    [
        "AbbreviatedCaseNumber", "AlbumTitle", "BookTitle", "Broadcaster", "BroadcastTitle", "CaseNumber", "ChapterNumber", "City", "Comments",
        "ConferenceName", "CountryRegion", "Court", "Day", "DayAccessed", "Department", "Distributor", "Edition", "Institution", "InternetSiteTitle",
        "Issue", "JournalName", "Medium", "Month", "MonthAccessed", "NumberVolumes", "Pages", "PatentNumber", "PeriodicalTitle", "ProductionCompany",
        "PublicationTitle", "Publisher", "RecordingNumber", "Reporter", "ShortTitle", "StandardNumber", "StateProvince", "Station", "Theater",
        "ThesisType", "Title", "Type", "URL", "Version", "Volume", "Year", "YearAccessed", "DOI",
    ];

    [GeneratedRegex(@"^(\d{4})(?:-(\d{1,2})(?:-(\d{1,2}))?)?$")]
    private static partial Regex DateText();

    /// <summary>
    /// BibTeX's names ("Smith, John and Jones, Ann B." or "John Smith and Ann B. Jones"): people as Word keeps them (the last name, the
    /// first, the rest of the given names as the middle); one in braces as a whole ("{UNESCO}") an organization's.
    /// </summary>
    static Contributors Names(string raw)
    {
        var people = new List<Person>();
        string? corporate = null;
        foreach (var name in SplitAnd(raw))
        {
            var trimmed = name.Trim();
            if (trimmed.Length > 1 && trimmed[0] == '{' && Closing(trimmed, 0) == trimmed.Length - 1)
            {
                var whole = Plain(trimmed);
                if (corporate == null && people.Count == 0)
                    corporate = whole;
                else
                    people.Add(new Person(whole));
                continue;
            }
            var plain = Plain(trimmed);
            string last;
            string[] given;
            if (plain.Contains(','))
            {
                var at = plain.IndexOf(',');
                last = plain[..at].Trim();
                given = plain[(at + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                var words = plain.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                    continue;
                // Particles in lower case before the last name are part of it ("Ludwig van Beethoven").
                var start = words.Length - 1;
                while (start > 1 && char.IsLower(words[start - 1][0]))
                    start--;
                last = string.Join(" ", words[start..]);
                given = words[..start];
            }
            if (corporate != null && people.Count == 0)
            {
                people.Add(new Person(corporate));
                corporate = null;
            }
            people.Add(new Person(last, given.Length > 0 ? given[0] : null, given.Length > 1 ? string.Join(" ", given[1..]) : null));
        }
        return new Contributors(people, corporate);
    }

    /// <summary>A names field's names, split at " and " outside braces.</summary>
    static IEnumerable<string> SplitAnd(string raw)
    {
        var (depth, start) = (0, 0);
        for (var i = 0; i < raw.Length; i++)
        {
            depth += raw[i] == '{' ? 1 : raw[i] == '}' ? -1 : 0;
            if (depth == 0 && string.Compare(raw, i, " and ", 0, 5, StringComparison.OrdinalIgnoreCase) == 0)
            {
                yield return raw[start..i];
                start = i + 5;
                i += 4;
            }
        }
        yield return raw[start..];
    }

    /// <summary>A BibTeX value as text: braces that only keep letters' case left out, LaTeX's escapes and accents as their characters.</summary>
    static string Plain(string value)
    {
        var text = Accents().Replace(value, m =>
        {
            var letter = m.Groups[2].Value;
            var mark = m.Groups[1].Value switch { "\"" => "̈", "'" => "́", "`" => "̀", "^" => "̂", "~" => "̃", "c" => "̧", "v" => "̌", "=" => "̄", _ => "" };
            return (letter + mark).Normalize(NormalizationForm.FormC);
        });
        text = text.Replace("\\ss", "ß", StringComparison.Ordinal).Replace("\\&", "&", StringComparison.Ordinal).Replace("\\%", "%", StringComparison.Ordinal)
            .Replace("\\_", "_", StringComparison.Ordinal).Replace("\\$", "$", StringComparison.Ordinal).Replace("\\#", "#", StringComparison.Ordinal)
            .Replace("--", "–", StringComparison.Ordinal).Replace("~", " ", StringComparison.Ordinal);
        return Spaces().Replace(text.Replace("{", "", StringComparison.Ordinal).Replace("}", "", StringComparison.Ordinal), " ").Trim();
    }

    [GeneratedRegex(@"\{?\\([""'`^~cv=])\{?\s*([A-Za-z])\}?\}?")]
    private static partial Regex Accents();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    /// <summary>A source as a BibTeX entry, as Parse reads it back alike.</summary>
    public static string Bibtex(Source source)
    {
        var type = Kinds.First(k => k.Word == source.Type || k.Word == "Misc" && !Kinds.Any(x => x.Word == source.Type)).Bibtex;
        var lines = new List<string>();
        static string Value(string value) => "{" + value.Replace("{", "", StringComparison.Ordinal).Replace("}", "", StringComparison.Ordinal) + "}";
        static string NamesOf(Contributors names) =>
            names.Corporate != null && names.People.Count == 0 ? "{{" + names.Corporate + "}}"
                : "{" + string.Join(" and ", names.People.Select(p => p.First == null && p.Middle == null ? "{" + p.Last + "}" : p.Last + ", " + string.Join(" ", new[] { p.First, p.Middle }.Where(n => n != null)))) + "}";
        var main = MainRole(source.Type);
        if (source.Role(main) is { } author)
            lines.Add("author = " + NamesOf(author));
        foreach (var (role, names) in source.Roles.Where(r => r.Key != main && !r.Value.IsEmpty))
            lines.Add((RoleNames.FirstOrDefault(r => r.Word == role).Bibtex ?? role.ToLowerInvariant()) + " = " + NamesOf(names));
        var accessed = (source["YearAccessed"], source["MonthAccessed"], source["DayAccessed"]);
        foreach (var (name, value) in source.Fields)
        {
            if (value.Length == 0 || name is "Guid" or "RefOrder" or "LCID" or "Tag" or "SourceType" || name.EndsWith("Accessed", StringComparison.Ordinal))
                continue;
            var bibtex = name switch
            {
                "BookTitle" => "booktitle",
                "ConferenceName" => "booktitle",
                "JournalName" or "PeriodicalTitle" => "journal",
                "Issue" or "PatentNumber" or "CaseNumber" => "number",
                "StandardNumber" when source.Type == "Report" => "number",
                "ThesisType" => "type",
                _ => FieldNames.FirstOrDefault(f => f.Word == name).Bibtex ?? name.ToLowerInvariant(),
            };
            lines.Add(bibtex + " = " + Value(value));
        }
        if (accessed.Item1 != null)
        {
            var month = accessed.Item2 is { } m ? Array.FindIndex(EnglishMonths, e => e.Equals(m, StringComparison.OrdinalIgnoreCase)) is var en and >= 0 ? en + 1
                : Array.FindIndex(GermanMonths, g => g.Equals(m, StringComparison.OrdinalIgnoreCase)) + 1 : 0;
            if (month > 0 && (accessed.Item3 == null || int.TryParse(accessed.Item3, out _)))
                lines.Add("urldate = {" + accessed.Item1 + "-" + month.ToString("00", CultureInfo.InvariantCulture) + (accessed.Item3 is { } d ? "-" + int.Parse(d, CultureInfo.InvariantCulture).ToString("00", CultureInfo.InvariantCulture) : "") + "}");
            else
            {
                lines.Add("yearaccessed = " + Value(accessed.Item1));
                if (accessed.Item2 != null)
                    lines.Add("monthaccessed = " + Value(accessed.Item2));
                if (accessed.Item3 != null)
                    lines.Add("dayaccessed = " + Value(accessed.Item3));
            }
        }
        return $"@{type}{{{source.Tag},\n  " + string.Join(",\n  ", lines) + "\n}";
    }

    /// <summary>The document's sources as a ```bibtex block with their style, as the notation writes them (see Parse); null where it has none.</summary>
    public static string? Block(WordDocument word)
    {
        if (word.Sources is not { Sources.Count: > 0 } list)
            return null;
        return $"```bibtex style={StyleText(word.CitationStyleOf(list).Name)}\n" + string.Join("\n", list.Sources.Select(Bibtex)) + "\n```";
    }

    /// <summary>A style's name as a fence's setting takes it ("Harvard-Anglia", quoted where it has spaces).</summary>
    static string StyleText(string name) => name.Contains(' ', StringComparison.Ordinal) ? "\"" + name + "\"" : name;

    /// <summary>
    /// A ```bibtex block's sources put among the document's: one of a key the document has in its place, the others after them; the
    /// block's style=… the document's citation style (Word's name or style sheet: APA, MLA, Chicago, IEEE, "Harvard - Anglia", "ISO 690 -
    /// First Element and Date", "ISO 690 - Numerical Reference", Turabian, GB7714, "GOST - Name Sort", "GOST - Title Sort", SIST02).
    /// </summary>
    public static void Merge(WordDocument word, Notation.Fenced block, int lcid, List<string> problems)
    {
        var (sources, found) = Parse(block.Body, lcid);
        problems.AddRange(found);
        var list = word.Sources ?? new SourceList([]);
        var merged = list.Sources.Select(s => sources.LastOrDefault(n => n.Tag.Equals(s.Tag, StringComparison.OrdinalIgnoreCase)) ?? s).ToList();
        foreach (var source in sources.Where(n => !list.Sources.Any(s => s.Tag.Equals(n.Tag, StringComparison.OrdinalIgnoreCase))))
        {
            merged.RemoveAll(s => s.Tag.Equals(source.Tag, StringComparison.OrdinalIgnoreCase));
            merged.Add(source);
        }
        var style = word.CitationStyleOf(list);
        if (block.Settings.GetValueOrDefault("style") is { Length: > 0 } wanted)
        {
            if (CitationStyle.Named(wanted.Trim('"', '\'')) is { } named)
                style = named;
            else
                problems.Add($"style={wanted} is none of Word's citation styles ({string.Join(", ", CitationStyle.All.Select(s => s.Name))}); the sources kept {style.Name}.");
        }
        word.SetSources(new SourceList(merged, style.File, style.Name, style.Version));
    }

    /// <summary>The tags the document's citations name that none of its sources has.</summary>
    public static IReadOnlyList<string> Unknown(WordDocument word)
    {
        var list = word.Sources ?? new SourceList([]);
        return [.. word.CitationInstructions().SelectMany(i => ItemsOf(i) ?? []).Select(i => i.Tag).Where(t => list.Find(t) == null)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The id of the document's paragraph style for bibliographies (Word's Bibliography), defined as Word defines it where it has none.</summary>
    static string StyleId(WordDocument word)
    {
        if (word.StyleIdNamed("Bibliography") is { } known)
            return known;
        var normal = System.Security.SecurityElement.Escape(word.StyleIdNamed("Normal") ?? "Normal");
        word.DefineStyles("<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"
                          + $"<w:style w:type=\"paragraph\" w:styleId=\"Bibliography\"><w:name w:val=\"Bibliography\"/><w:basedOn w:val=\"{normal}\"/>"
                          + $"<w:next w:val=\"{normal}\"/><w:uiPriority w:val=\"37\"/><w:unhideWhenUsed/></w:style></w:styles>");
        return "Bibliography";
    }

    /// <summary>
    /// The bibliography made in place of a paragraph (the one its line put in, see Writing), in the language of that paragraph; Word's
    /// "There are no sources in the current document." where it has none. Returns its paragraphs' ids.
    /// </summary>
    public static IReadOnlyList<int> Make(WordDocument word, int placeholder)
    {
        var ids = word.InsertBlocks(placeholder, Bibliography(word, word.LanguageOf(placeholder)), before: true);
        word.Remove(placeholder);
        return ids;
    }

    /// <summary>A bibliography's markup in a language: the document's sources in their style, Word's notice where it has none.</summary>
    static string Bibliography(WordDocument word, int lcid)
    {
        var style = StyleId(word);
        return word.BibliographyMarkup(lcid, style)
               ?? CitationMarkup.Bibliography(new BibliographyList([new BibliographyEntry([new Piece((lcid & 0x3FF) == 0x07
                       ? "Im aktuellen Dokument sind keine Quellen vorhanden." : "There are no sources in the current document.")])], BibliographyLayout.Plain),
                   style, lcid, Random.Shared.Next(1, int.MaxValue), Random.Shared.Next(1, int.MaxValue));
    }

    /// <summary>
    /// The document's bibliographies made anew as its sources and citations now are (Word's Update Citations and Bibliography): each
    /// taken away whole and made in its place, in its language; those of the paragraphs given (made just now) aside.
    /// </summary>
    public static void Refresh(WordDocument word, IReadOnlySet<int>? made = null)
    {
        var done = new HashSet<int>(made ?? new HashSet<int>());
        var paragraph = word.Beginning;
        while (paragraph is { } at)
        {
            if (done.Contains(at) || Around(word, at) is not var (first, last) || word.Before(first) is not { } before || word.After(last) is not { } after)
            {
                paragraph = word.Next(at);
                continue;
            }
            var lcid = word.LanguageOf(first);
            word.RemoveBetween(before, after);
            var ids = word.InsertBlocks(after, Bibliography(word, lcid), before: true);
            done.UnionWith(ids);
            // On after the new one (the paragraph that followed the old one).
            paragraph = after;
        }
    }

    /// <summary>The bibliography a paragraph of the body stands in (Word's bibliography content control): its first and last paragraph, or null.</summary>
    public static (int First, int Last)? Around(WordDocument word, int paragraph)
    {
        if (!word.InBibliography(paragraph))
            return null;
        // In reading order: a table within it (IEEE's) too.
        var (first, last) = (paragraph, paragraph);
        while (word.Previous(first) is { } previous && word.InBibliography(previous))
            first = previous;
        while (word.Next(last) is { } next && word.InBibliography(next))
            last = next;
        return (first, last);
    }
}
