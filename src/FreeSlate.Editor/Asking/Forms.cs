using System.Globalization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using FreeSlate.Editing;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// Forms, mail merge fields and the fields of the document's facts as the notation writes them among the text (see Prompt), made as Word
/// makes them and read back as written:
/// <list type="bullet">
/// <item>content controls as Word's Developer tab inserts them: "{checkbox}" ("{checkbox checked}"), "{text Your name}" (plain text
/// showing its placeholder; "{text Your name = Ann}" holding text, "{text}" Word's own placeholder), "{richtext …}" alike with meanings in
/// its text, "{choice Yes|No|Maybe}" a drop-down list ("= No" the one chosen), "{combo Red|Green}" a combo box, "{date dd.MM.yyyy}" a date
/// picker ("{date d MMMM yyyy en-GB = 2026-12-24}" in a language of its own, holding a date); their placeholders building blocks of the
/// glossary made at the write's end (see WordDocument.SettlePlaceholders);</item>
/// <item>Word's legacy form fields: "{formtext}" ("{formtext = Ann}"), "{formcheckbox}" ("{formcheckbox checked}"), "{formdropdown
/// A|B|C}" ("= B"), FORMTEXT, FORMCHECKBOX and FORMDROPDOWN fields with their form data and a bookmark of their name round them;</item>
/// <item>"{merge FirstName}": Word's MERGEFIELD, «FirstName» as Word shows it without data, the document set up as a form letter once the
/// write is in (see WordDocument.FormLetter);</item>
/// <item>"{title}", "{subject}", "{author}", "{keywords}", "{comments}", "{category}", "{company}", "{manager}", "{filename}", "{words}",
/// "{characters}", "{created}", "{saved}", "{printed}", "{time}", "{section}", "{sectionpages}": the fields that show the document's
/// properties and facts, their results set as Word shows them once it updates them (see WordDocument.RefreshPropertyFields).</item>
/// </list>
/// </summary>
static partial class Forms
{
    /// <summary>A piece of the notation this class makes and reads (see the class).</summary>
    public abstract record Form : Notation.Piece;

    /// <summary>A content control: its kind (checkbox, text, richtext, dropdown, combobox, date), its placeholder's text where it has one of its own, a list's items, what it holds (null where it shows its placeholder), a check box's state, a date's format and language.</summary>
    public sealed record Control(string Kind, string? Placeholder, IReadOnlyList<string> Items, string? Value, bool Checked = false, string? Format = null, string? Language = null) : Form;

    /// <summary>A legacy form field: its kind (text, checkbox, dropdown), a drop-down's entries, what it holds (a text's or the entry shown), a check box's state.</summary>
    public sealed record Legacy(string Kind, IReadOnlyList<string> Items, string? Value, bool Checked = false) : Form;

    /// <summary>A mail merge field of a data source's column.</summary>
    public sealed record Merge(string Name) : Form;

    /// <summary>A field of the document's properties or facts by the notation's name ("title", "words" …).</summary>
    public sealed record Fact(string Name) : Form;

    /// <summary>The facts' fields by the notation's names: their instructions as Word writes them.</summary>
    static readonly (string Name, string Instruction)[] Facts =
    [
        ("title", "TITLE"), ("subject", "SUBJECT"), ("author", "AUTHOR"), ("keywords", "KEYWORDS"), ("comments", "COMMENTS"),
        ("category", "DOCPROPERTY Category"), ("company", "DOCPROPERTY Company"), ("manager", "DOCPROPERTY Manager"), ("filename", "FILENAME"),
        ("words", "NUMWORDS"), ("characters", "NUMCHARS"), ("created", "CREATEDATE"), ("saved", "SAVEDATE"), ("printed", "PRINTDATE"),
        ("time", "TIME"), ("section", "SECTION"), ("sectionpages", "SECTIONPAGES"),
    ];

    /// <summary>
    /// The name of a new placeholder's building block, a GUID's 32 digits as Word names one of its own text. Word's own hints share one
    /// building block for each kind (DefaultPlaceholder_-1854013440 for text, …38 for lists, …37 for dates), and every control showing
    /// such a placeholder shows the first one's content (see ModelReader.Shown), which the editor cannot change where it stands; so each
    /// control written has a building block of its own, Word's hint its text (the same in Word, read back as "{text}").
    /// </summary>
    static string Part() => Guid.NewGuid().ToString("N").ToUpperInvariant();

    /// <summary>Whether a placeholder's building block or text is one of Word's own hints (its name, or its text in English or German).</summary>
    static bool Default(string? part, string? text) =>
        part?.StartsWith("DefaultPlaceholder_", StringComparison.Ordinal) == true
        || text != null && (Defaults("en").Text == text || Defaults("en").List == text || Defaults("en").Date == text
            || Defaults("de").Text == text || Defaults("de").List == text || Defaults("de").Date == text);

    /// <summary>The start of a control's or field's mark: its word, then a space or its end; a date picker's always with its format, as "{date}" alone is today's date (see Notation.FieldMark).</summary>
    [GeneratedRegex(@"\G\{(?:(checkbox|richtext|text|choice|combo|formtext|formcheckbox|formdropdown|merge)(?=[\s}])|(date)(?=\s+\S))", RegexOptions.IgnoreCase)]
    private static partial Regex Opening();

    [GeneratedRegex(@"\G\{([a-z]+)\}")]
    private static partial Regex FactText();

    /// <summary>A language tag at the end of a date's format ("en-GB", "de-DE"), which names the date's language.</summary>
    [GeneratedRegex(@"^(.*?)\s*\b([a-z]{2,3}-[A-Za-z]{2,4})$")]
    private static partial Regex LanguageAtEnd();

    /// <summary>
    /// The piece of this class's notation a text holds at a place, and how many characters it takes there; null where none begins there.
    /// A control's mark runs to its closing brace, braces within it nested, "\}" a brace of its text.
    /// </summary>
    public static (Notation.Piece Piece, int Length)? Piece(string text, int at)
    {
        if (FactText().Match(text, at) is { Success: true } fact && Facts.Any(f => f.Name == fact.Groups[1].Value))
            return (new Fact(fact.Groups[1].Value), fact.Length);
        if (Opening().Match(text, at) is not { Success: true } opening)
            return null;
        var (depth, k) = (0, at);
        for (; k < text.Length; k++)
        {
            if (text[k] == '\\' && k + 1 < text.Length)
            {
                k++;
                continue;
            }
            if (text[k] == '{')
                depth++;
            else if (text[k] == '}' && --depth == 0)
                break;
        }
        if (k >= text.Length)
            return null;
        var kind = (opening.Groups[1].Success ? opening.Groups[1].Value : opening.Groups[2].Value).ToLowerInvariant();
        var inner = text[(at + opening.Length)..k].Trim();
        var length = k + 1 - at;
        // What it holds after the first " = " not escaped ("= Ann" alone where it names nothing before).
        var (before, value) = Split(inner);
        switch (kind)
        {
            case "checkbox":
                return (new Control("checkbox", null, [], null, Checked(inner)), length);
            case "formcheckbox":
                return (new Legacy("checkbox", [], null, Checked(inner)), length);
            case "text" or "richtext":
                return (new Control(kind, before.Length > 0 ? Unescaped(before) : null, [], value != null ? kind == "text" ? Unescaped(value) : value : null), length);
            case "choice" or "combo":
            {
                var items = Items(before);
                var chosen = value != null ? Unescaped(value) : null;
                return (new Control(kind == "choice" ? "dropdown" : "combobox", null, items, chosen), length);
            }
            case "date":
            {
                var (format, language) = LanguageAtEnd().Match(before) is { Success: true } named ? (named.Groups[1].Value, named.Groups[2].Value) : (before, (string?)null);
                return (new Control("date", null, [], value?.Trim(), Format: format.Length > 0 ? Unescaped(format) : null, Language: language), length);
            }
            case "formtext":
                return (new Legacy("text", [], value != null ? Unescaped(value) : null), length);
            case "formdropdown":
                return (new Legacy("dropdown", Items(before), value != null ? Unescaped(value) : null), length);
            case "merge":
                return inner.Length > 0 ? (new Merge(Unescaped(inner)), length) : null;
        }
        return null;
    }

    static bool Checked(string inner) => inner.Trim().ToLowerInvariant() is "checked" or "x" or "yes" or "on" or "true";

    /// <summary>A mark's words before its first " = " (or "=" at its start) not escaped, and what follows it; null where there is none.</summary>
    static (string Before, string? Value) Split(string inner)
    {
        if (inner.StartsWith('='))
            return ("", inner[1..].Trim());
        for (var k = 0; k + 2 < inner.Length + 1; k++)
        {
            if (inner[k] == '\\')
            {
                k++;
                continue;
            }
            if (inner[k] == '=' && k > 0 && inner[k - 1] == ' ')
                return (inner[..k].Trim(), inner[(k + 1)..].Trim());
        }
        return (inner, null);
    }

    /// <summary>A list's items, split at the bars that are not escaped.</summary>
    static List<string> Items(string text)
    {
        var items = new List<string>();
        var item = new StringBuilder();
        for (var k = 0; k < text.Length; k++)
        {
            if (text[k] == '\\' && k + 1 < text.Length)
            {
                item.Append(text[++k]);
                continue;
            }
            if (text[k] == '|')
            {
                items.Add(item.ToString().Trim());
                item.Clear();
                continue;
            }
            item.Append(text[k]);
        }
        items.Add(item.ToString().Trim());
        return [.. items.Where(i => i.Length > 0)];
    }

    /// <summary>A text of a mark without its escapes ("\}" a brace, "\|" a bar, "\=" an equals sign).</summary>
    static string Unescaped(string text)
    {
        var plain = new StringBuilder();
        for (var k = 0; k < text.Length; k++)
        {
            if (text[k] == '\\' && k + 1 < text.Length)
                k++;
            plain.Append(text[k]);
        }
        return plain.ToString();
    }

    /// <summary>A text written into a mark, its braces, bars, equals signs and backslashes escaped.</summary>
    static string Escaped(string text)
    {
        var escaped = new StringBuilder();
        foreach (var c in text)
        {
            if (c is '{' or '}' or '|' or '=' or '\\')
                escaped.Append('\\');
            escaped.Append(c);
        }
        return escaped.ToString();
    }

    // What a write knows of the place it writes to (set as it begins, see Begin), and what it made.

    /// <summary>The style placeholders show in, the language the write's text is in (a tag, en-US), the document written to.</summary>
    static string placeholderStyle = "PlaceholderText", language = "en-US";
    static WordDocument? document;

    /// <summary>The placeholders the write made by the names of their building blocks, with their texts; whether it made controls, merge fields, facts' fields.</summary>
    static readonly Dictionary<string, string> made = new(StringComparer.Ordinal);
    static bool controls, merged, facts, dated;

    /// <summary>The legacy form fields' names the document has (Text1, Check1 …), found when the write first makes one.</summary>
    static HashSet<string>? names;

    /// <summary>The document's properties and counts as the write's first fact's field asks for them, which they show till its end.</summary>
    static DocumentProperties? properties;
    static (int Words, int Characters)? counts;

    static DocumentProperties Properties() => properties ??= document?.Properties ?? new DocumentProperties();

    static (int Words, int Characters) Counts() => counts ??= document?.Counts() ?? (0, 0);

    /// <summary>A write begins at a paragraph of a document: the style its placeholders show in, the language its text is in.</summary>
    public static void Begin(WordDocument word, int paragraph)
    {
        document = word;
        placeholderStyle = word.PlaceholderStyle;
        try
        {
            language = CultureInfo.GetCultureInfo(word.LanguageOf(paragraph)).Name;
        }
        catch (CultureNotFoundException)
        {
            language = "en-US";
        }
        made.Clear();
        (controls, merged, facts, dated, names, properties, counts) = (false, false, false, false, null, null, null);
    }

    /// <summary>
    /// A write is in: the placeholders of the controls it made building blocks of the glossary (and the style they show in defined where
    /// the document has none), the document a form letter where it wrote merge fields, the facts' fields showing what Word shows once it
    /// updates them.
    /// </summary>
    public static void End(WordDocument word, Func<Func<TextPlace?>, bool, bool> step)
    {
        if (controls)
            step(() =>
            {
                word.SettlePlaceholders(made);
                return null;
            }, true);
        if (merged && !word.FormLetter)
            step(() =>
            {
                word.FormLetter = true;
                return null;
            }, true);
        // A document without the time it was made given one (as Word knows it of a new document), which {created} shows.
        if (dated && word.Properties.Created == null)
            step(() =>
            {
                word.SetProperties(new DocumentProperties { Created = DateTime.UtcNow });
                return null;
            }, true);
        if (facts || merged)
            step(() =>
            {
                word.RefreshPropertyFields();
                return null;
            }, true);
        (controls, merged, facts, dated, document) = (false, false, false, false, null);
    }

    /// <summary>Word's own placeholders' texts in a language (German, else English), as Word 365 writes them: text, a list's, a date's.</summary>
    static (string Text, string List, string Date) Defaults(string tag) => tag.StartsWith("de", StringComparison.OrdinalIgnoreCase)
        ? ("Klicken oder tippen Sie hier, um Text einzugeben.", "Wählen Sie ein Element aus.", "Klicken oder tippen Sie, um ein Datum einzugeben.")
        : ("Click or tap here to enter text.", "Choose an item.", "Click or tap to enter a date.");

    /// <summary>How many characters of the paragraph's text a piece makes as the model reads it.</summary>
    public static int Characters(Form form) => Shown(form).Length;

    /// <summary>What a piece shows: a control its text or its placeholder, a form field its text, a merge field «Name», a fact's field its result.</summary>
    static string Shown(Form form) => form switch
    {
        Control { Kind: "checkbox" } => "☐",
        Control control => control.Value != null ? control.Kind == "date" ? DateText(control) ?? control.Value : control.Kind == "richtext" ? PlainText(control.Value) : control.Value
            : control.Placeholder ?? (control.Kind is "dropdown" or "combobox" ? Defaults(language).List : control.Kind == "date" ? Defaults(language).Date : Defaults(language).Text),
        Legacy { Kind: "checkbox" } => "￼",
        Legacy { Kind: "dropdown" } legacy => legacy.Value ?? legacy.Items.FirstOrDefault() ?? "",
        Legacy legacy => legacy.Value ?? new string(' ', 5),
        Merge merge => "«" + merge.Name + "»",
        // A field the layout computes (a document property, a date, a section's number) is one character of the text, as the model reads it.
        Fact fact => FreeSlate.Docx.FieldInline.IsPageField(Facts.First(f => f.Name == fact.Name).Instruction) ? "￼" : FactResult(fact.Name) ?? "",
        _ => "",
    };

    static string PlainText(string notation) => string.Concat(Notation.Inline(notation, null).OfType<Notation.Run>().Select(r => r.Text));

    /// <summary>The result a fact's field shows as the write makes it (the model reads one the layout computes as one character, null here).</summary>
    static string? FactResult(string name) => name switch
    {
        "title" => Properties().Title ?? "",
        "subject" => Properties().Subject ?? "",
        "author" => Properties().Author ?? "",
        "keywords" => Properties().Keywords ?? "",
        "comments" => Properties().Comments ?? "",
        "category" => Properties().Category ?? "",
        "company" => Properties().Company ?? "",
        "manager" => Properties().Manager ?? "",
        "filename" => language.StartsWith("de", StringComparison.OrdinalIgnoreCase) ? "Dokument1" : "Document1",
        "words" => Counts().Words.ToString(CultureInfo.InvariantCulture),
        "characters" => Counts().Characters.ToString(CultureInfo.InvariantCulture),
        "created" => Minute(Properties().Created ?? DateTime.UtcNow).ToLocalTime().ToString(Culture().DateTimeFormat.ShortDatePattern + " " + Culture().DateTimeFormat.LongTimePattern, Culture()),
        _ => null,
    };

    static DateTime Minute(DateTime time) => new(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, time.Kind);

    /// <summary>A language's short date (dd.MM.yyyy in German), which a date picker without a format of its own shows.</summary>
    static string ShortDate(string tag)
    {
        try
        {
            return CultureInfo.GetCultureInfo(tag).DateTimeFormat.ShortDatePattern;
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture.DateTimeFormat.ShortDatePattern;
        }
    }

    static CultureInfo Culture()
    {
        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    /// <summary>A date picker's date as it shows it: in its format and language (Word's codes as .NET's, separators as written).</summary>
    static string? DateText(Control control)
    {
        if (!DateTime.TryParseExact(control.Value, ["yyyy-MM-dd", "yyyy-M-d"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && !DateTime.TryParse(control.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return null;
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(control.Language ?? language);
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.InvariantCulture;
        }
        var format = control.Format ?? culture.DateTimeFormat.ShortDatePattern;
        try
        {
            return date.ToString(format.Replace("/", "\\/", StringComparison.Ordinal).Replace(":", "\\:", StringComparison.Ordinal), culture);
        }
        catch (FormatException)
        {
            return date.ToString(culture);
        }
    }

    static string Text(string text) => "<w:t xml:space=\"preserve\">" + SecurityElement.Escape(text) + "</w:t>";

    static string Id() => Random.Shared.Next(int.MinValue, int.MaxValue).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The markup of a piece among runs: a content control as Word's Developer tab inserts one (measured with Word 365 by COM,
    /// ContentControls.Add, SetPlaceholderText, DropdownListEntries, DateDisplayFormat and DateDisplayLocale: w:sdt with a random w:id; its
    /// placeholder a building block of its own named by a GUID's 32 digits, Word's hints too (see Part); w:showingPlcHdr and the
    /// placeholder's text in the style Placeholder Text while it holds nothing; w:text for plain text, no kind of its own for rich text,
    /// w:dropDownList and w:comboBox with their w:listItem, w:date with its format, language, mapping as dateTime and Gregorian calendar,
    /// w:fullDate where it holds a date; a check box w14:checkbox with Word's glyphs, ☐ (2610) and ☒ (2612) in MS Gothic); a legacy form
    /// field as Word's Legacy Forms insert one (its form data, a bookmark of its name round its instruction and result, five en spaces in
    /// an empty text field); a merge field and a fact's field as Word saves them (complex fields, their results without proofing).
    /// </summary>
    public static string Markup(Form form)
    {
        switch (form)
        {
            case Control control:
                controls = true;
                return ControlMarkup(control);
            case Legacy legacy:
                return LegacyMarkup(legacy);
            case Merge merge:
                merged = true;
                return Field(" MERGEFIELD " + (merge.Name.Contains(' ', StringComparison.Ordinal) ? "\"" + merge.Name.Replace("\"", "", StringComparison.Ordinal) + "\"" : merge.Name) + " ",
                    "«" + merge.Name + "»", proofed: false);
            case Fact fact:
                facts = true;
                dated |= fact.Name == "created";
                var instruction = Facts.First(f => f.Name == fact.Name).Instruction;
                return Field(" " + instruction + " ", FactResult(fact.Name) ?? (fact.Name switch
                {
                    "section" or "sectionpages" => "1",
                    "time" => DateTime.Now.ToString(Culture().DateTimeFormat.ShortTimePattern, Culture()),
                    _ => DateTime.Now.ToString(Culture().DateTimeFormat.ShortDatePattern + " " + Culture().DateTimeFormat.LongTimePattern, Culture()),
                }), proofed: fact.Name is "title" or "subject" or "keywords" or "comments" or "section");
        }
        return "";
    }

    /// <summary>A complex field of an instruction with its result (in a run without proofing unless <paramref name="proofed"/>).</summary>
    static string Field(string instruction, string result, bool proofed) =>
        "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\">" + SecurityElement.Escape(instruction) + "</w:instrText></w:r>"
        + "<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>"
        + (result.Length > 0 ? "<w:r>" + (proofed ? "" : "<w:rPr><w:noProof/></w:rPr>") + Text(result) + "</w:r>" : "")
        + "<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>";

    static string ControlMarkup(Control control)
    {
        var properties = new StringBuilder("<w:sdtPr><w:id w:val=\"").Append(Id()).Append("\"/>");
        var content = new StringBuilder();
        string Placeholder(string own, string text)
        {
            properties.Append("<w:placeholder><w:docPart w:val=\"").Append(own).Append("\"/></w:placeholder>");
            made.TryAdd(own, text);
            return text;
        }
        string PlaceholderRun(string text) => "<w:r><w:rPr><w:rStyle w:val=\"" + SecurityElement.Escape(placeholderStyle) + "\"/></w:rPr>" + Text(text) + "</w:r>";
        var defaults = Defaults(language);
        switch (control.Kind)
        {
            case "checkbox":
                properties.Append("<w14:checkbox><w14:checked w14:val=\"").Append(control.Checked ? '1' : '0').Append("\"/><w14:checkedState w14:val=\"2612\" w14:font=\"MS Gothic\"/>")
                    .Append("<w14:uncheckedState w14:val=\"2610\" w14:font=\"MS Gothic\"/></w14:checkbox>");
                content.Append("<w:r><w:rPr><w:rFonts w:ascii=\"MS Gothic\" w:eastAsia=\"MS Gothic\" w:hAnsi=\"MS Gothic\" w:hint=\"eastAsia\"/></w:rPr>")
                    .Append(Text(control.Checked ? "☒" : "☐")).Append("</w:r>");
                break;
            case "text" or "richtext":
            {
                var shown = Placeholder(Part(), control.Placeholder is { Length: > 0 } own ? own : defaults.Text);
                if (control.Value == null)
                    properties.Append("<w:showingPlcHdr/>");
                if (control.Kind == "text")
                    properties.Append("<w:text/>");
                content.Append(control.Value == null ? PlaceholderRun(shown)
                    : control.Kind == "text" ? "<w:r>" + Text(control.Value) + "</w:r>"
                    : Asking.Markup.RunsOf(Notation.Inline(control.Value, null)).Runs);
                break;
            }
            case "dropdown" or "combobox":
            {
                var items = control.Items.ToList();
                // A choice not among a list's items becomes one of them (a combo box holds any text).
                if (control.Value is { Length: > 0 } chosen && control.Kind == "dropdown" && !items.Contains(chosen))
                    items.Add(chosen);
                var shown = Placeholder(Part(), defaults.List);
                if (control.Value == null)
                    properties.Append("<w:showingPlcHdr/>");
                var element = control.Kind == "dropdown" ? "dropDownList" : "comboBox";
                properties.Append("<w:").Append(element).Append('>');
                foreach (var item in items)
                    properties.Append("<w:listItem w:displayText=\"").Append(SecurityElement.Escape(item)).Append("\" w:value=\"").Append(SecurityElement.Escape(item)).Append("\"/>");
                properties.Append("</w:").Append(element).Append('>');
                content.Append(control.Value == null ? PlaceholderRun(shown) : "<w:r>" + Text(control.Value) + "</w:r>");
                break;
            }
            case "date":
            {
                var shown = Placeholder(Part(), defaults.Date);
                var text = control.Value != null ? DateText(control) : null;
                if (text == null)
                    properties.Append("<w:showingPlcHdr/>");
                var tag = control.Language ?? language;
                CultureInfo culture;
                try
                {
                    culture = CultureInfo.GetCultureInfo(tag);
                }
                catch (CultureNotFoundException)
                {
                    culture = CultureInfo.GetCultureInfo("en-US");
                }
                var format = control.Format ?? culture.DateTimeFormat.ShortDatePattern;
                properties.Append("<w:date");
                if (text != null && DateTime.TryParse(control.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    properties.Append(" w:fullDate=\"").Append(date.ToString("yyyy-MM-dd'T'00:00:00'Z'", CultureInfo.InvariantCulture)).Append('"');
                properties.Append("><w:dateFormat w:val=\"").Append(SecurityElement.Escape(format)).Append("\"/><w:lid w:val=\"").Append(SecurityElement.Escape(culture.Name))
                    .Append("\"/><w:storeMappedDataAs w:val=\"dateTime\"/><w:calendar w:val=\"gregorian\"/></w:date>");
                content.Append(text == null ? PlaceholderRun(shown) : "<w:r>" + Text(text) + "</w:r>");
                break;
            }
        }
        properties.Append("</w:sdtPr>");
        return "<w:sdt>" + properties + "<w:sdtContent>" + content + "</w:sdtContent></w:sdt>";
    }

    /// <summary>A legacy form field's name, unique in the document as Word numbers them (Text1, Check1, Dropdown1 …).</summary>
    static string Name(string stem)
    {
        if (names == null)
        {
            names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (document != null)
            {
                for (int? paragraph = document.Beginning; paragraph is { } p; paragraph = document.Next(p))
                {
                    foreach (var field in document.FormFieldsOf(p))
                        names.Add(field.Name);
                    foreach (var bookmark in document.BookmarksOf(p))
                        names.Add(bookmark);
                }
            }
        }
        var number = 1;
        while (names.Contains(stem + number.ToString(CultureInfo.InvariantCulture)))
            number++;
        var name = stem + number.ToString(CultureInfo.InvariantCulture);
        names.Add(name);
        return name;
    }

    static string LegacyMarkup(Legacy legacy)
    {
        var (stem, instruction) = legacy.Kind switch { "checkbox" => ("Check", "FORMCHECKBOX"), "dropdown" => ("Dropdown", "FORMDROPDOWN"), _ => ("Text", "FORMTEXT") };
        var name = Name(stem);
        var data = new StringBuilder("<w:ffData><w:name w:val=\"").Append(name).Append("\"/><w:enabled/><w:calcOnExit w:val=\"0\"/>");
        switch (legacy.Kind)
        {
            case "checkbox":
                data.Append("<w:checkBox><w:sizeAuto/><w:default w:val=\"").Append(legacy.Checked ? '1' : '0').Append("\"/></w:checkBox>");
                break;
            case "dropdown":
            {
                var items = legacy.Items.ToList();
                if (legacy.Value is { Length: > 0 } chosen && !items.Contains(chosen))
                    items.Add(chosen);
                data.Append("<w:ddList>");
                if (legacy.Value is { Length: > 0 } shown && items.IndexOf(shown) > 0)
                    data.Append("<w:result w:val=\"").Append(items.IndexOf(shown).ToString(CultureInfo.InvariantCulture)).Append("\"/>");
                foreach (var item in items)
                    data.Append("<w:listEntry w:val=\"").Append(SecurityElement.Escape(item)).Append("\"/>");
                data.Append("</w:ddList>");
                break;
            }
            default:
                data.Append("<w:textInput/>");
                break;
        }
        data.Append("</w:ffData>");
        var id = Random.Shared.Next(100_000_000, 999_999_999).ToString(CultureInfo.InvariantCulture);
        var result = legacy.Kind == "text"
            ? legacy.Value is { Length: > 0 } text ? "<w:r><w:rPr><w:noProof/></w:rPr>" + Text(text) + "</w:r>"
                : string.Concat(Enumerable.Repeat("<w:r><w:rPr><w:noProof/></w:rPr><w:t> </w:t></w:r>", 5))
            : "";
        return "<w:r><w:fldChar w:fldCharType=\"begin\">" + data + "</w:fldChar></w:r><w:bookmarkStart w:id=\"" + id + "\" w:name=\"" + name + "\"/>"
            + "<w:r><w:instrText xml:space=\"preserve\"> " + instruction + " </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>"
            + result + "<w:r><w:fldChar w:fldCharType=\"end\"/></w:r><w:bookmarkEnd w:id=\"" + id + "\"/>";
    }

    /// <summary>The paragraphs whose controls' text is being read as a control's (see Stretches), so that it does not read itself again.</summary>
    static readonly HashSet<(int Paragraph, int Start)> reading = [];

    /// <summary>
    /// The stretches of a paragraph's text this class's notation says, each with what it says: its content controls, legacy form fields,
    /// merge fields and facts' fields, an outer one where they overlap (a rich text control holding a field).
    /// </summary>
    public static IReadOnlyList<(int Start, int End, string Said)> Stretches(WordDocument word, int paragraph, Objects objects)
    {
        var found = new List<(int Start, int End, string Said)>();
        var text = word.Text(paragraph);
        string Of(int start, int end) => text[Math.Clamp(start, 0, text.Length)..Math.Clamp(end, 0, text.Length)];
        string tag;
        try
        {
            tag = CultureInfo.GetCultureInfo(word.LanguageOf(paragraph)).Name;
        }
        catch (CultureNotFoundException)
        {
            tag = "";
        }
        foreach (var control in word.ControlsOf(paragraph))
        {
            if (reading.Contains((paragraph, control.Start)))
                continue;
            var own = Default(control.PlaceholderPart, control.Placeholder) ? null : control.Placeholder;
            var said = control.Kind switch
            {
                "checkbox" => control.Checked ? "{checkbox checked}" : "{checkbox}",
                "text" => "{text" + (own is { Length: > 0 } ? " " + Escaped(own) : "") + (control.Showing ? "" : " = " + Escaped(Of(control.Start, control.End))) + "}",
                "richtext" => "{richtext" + (own is { Length: > 0 } ? " " + Escaped(own) : "") + (control.Showing ? "" : " = " + Inside(word, paragraph, control.Start, control.End, objects)) + "}",
                "dropdown" or "combobox" => "{" + (control.Kind == "dropdown" ? "choice " : "combo ") + string.Join("|", control.Items.Select(i => Escaped(i.Text)))
                    + (control.Showing ? "" : " = " + Escaped(Of(control.Start, control.End))) + "}",
                // A date picker's format always (without one Word shows the language's short date), as "{date}" alone is today's date.
                "date" => "{date " + Escaped(control.DateFormat is { Length: > 0 } format ? format : ShortDate(control.Language ?? tag))
                    + (control.Language is { Length: > 0 } lid && !lid.Equals(tag, StringComparison.OrdinalIgnoreCase) ? " " + lid : "")
                    + (control.Showing || control.Date is not { } date ? "" : " = " + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) + "}",
                _ => null,
            };
            if (said != null)
                found.Add((control.Start, control.End, said));
        }
        foreach (var field in word.FormFieldsOf(paragraph))
        {
            var shown = Of(field.Start, field.End);
            var said = field.Kind switch
            {
                "checkbox" => field.Checked ? "{formcheckbox checked}" : "{formcheckbox}",
                "dropdown" => "{formdropdown " + string.Join("|", field.Entries.Select(Escaped)) + (field.Selected > 0 && field.Selected < field.Entries.Count ? " = " + Escaped(field.Entries[field.Selected]) : "") + "}",
                _ => shown.Trim(' ', ' ').Length == 0 ? "{formtext}" : "{formtext = " + Escaped(shown) + "}",
            };
            found.Add((field.Start, field.End, said));
        }
        foreach (var (start, end, instruction) in word.FieldsOf(paragraph))
        {
            var words = instruction.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !w.Equals("\\*", StringComparison.Ordinal) && !w.Equals("MERGEFORMAT", StringComparison.OrdinalIgnoreCase)).ToList();
            if (words.Count == 0)
                continue;
            if (words[0].Equals("MERGEFIELD", StringComparison.OrdinalIgnoreCase) && words.Count > 1 && !words.Skip(1).Any(w => w.StartsWith('\\')))
                found.Add((start, end, "{merge " + Escaped(string.Join(" ", words.Skip(1)).Trim('"')) + "}"));
            else if (Facts.FirstOrDefault(f => f.Instruction.Equals(string.Join(" ", words), StringComparison.OrdinalIgnoreCase)) is { Name: { } name })
                found.Add((start, end, "{" + name + "}"));
        }
        // The outer where stretches overlap; in order.
        found.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.End.CompareTo(a.End));
        var kept = new List<(int Start, int End, string Said)>();
        foreach (var stretch in found)
        {
            if (kept.Count > 0 && stretch.Start < kept[^1].End)
                continue;
            kept.Add(stretch);
        }
        return kept;
    }

    /// <summary>A rich text control's text in the notation (its meanings, links, objects), the control itself not read again within it.</summary>
    static string Inside(WordDocument word, int paragraph, int start, int end, Objects objects)
    {
        reading.Add((paragraph, start));
        try
        {
            return Reading.Line(word, paragraph, start, end, objects);
        }
        finally
        {
            reading.Remove((paragraph, start));
        }
    }
}
