namespace FreeSlate.Docx;

/// <summary>
/// The text an EQ field shows (catalog: field.evaluation), laid out from its instruction as Word does: the text in the
/// formats of the instruction's runs, \s\up n and \s\do n raising and lowering their argument n points, \o of one
/// argument that argument; a fraction (\f) of other than two arguments or an integral (\i) of other than three make
/// the whole field Word's error in bold. Arguments part at the list separator of the regional settings, ";" in
/// German, where a comma is text (measured with docx/local/eqf-15 on a German Windows: \o(\s\up 12(E),\s\do 4(x))
/// prints "E" 12 points up, a comma and "x" 4 points down one after the other, \f(1,2) a bold "Fehler!"; cc-equal:
/// "x = \f(1,2)" and \i\su(i=0,n\s(2),\r(i)) print "Fehler!" alone; docx/external/libreoffice/extras-ooxmlexport/
/// tdf66401, extras-rtfexport/tdf138779, extras-odfexport/tdf130314). The instruction's \* switches print nothing, a
/// space after its last parenthesis prints. Fractions, arrays, radicals, brackets, displacements, integrals, lists, boxes and overstrikes of several
/// arguments are not drawn: such a field prints nothing.
/// </summary>
static class EqField
{
    abstract record Node;

    sealed record Character(char Value, RunFormat Format) : Node;

    sealed record Function(char Name, List<(string Name, string Value)> Options, List<List<Node>> Arguments) : Node;

    /// <summary>
    /// The text the instruction of an EQ field shows; the error text in the error format where a function has the
    /// wrong number of arguments; null where it uses what is not drawn here.
    /// </summary>
    public static List<Inline>? Inlines(List<(string Text, RunFormat Format)> runs, char separator, string error, RunFormat errorFormat)
    {
        var chars = runs.SelectMany(r => r.Text.Select(c => (C: c, F: r.Format))).ToList();
        var at = 0;
        while (at < chars.Count && char.IsWhiteSpace(chars[at].C))
            at++;
        if (at + 2 > chars.Count || !string.Concat(chars[at].C, chars[at + 1].C).Equals("EQ", StringComparison.OrdinalIgnoreCase))
            return null;
        at += 2;
        while (at < chars.Count && char.IsWhiteSpace(chars[at].C))
            at++;
        var parser = new Parser(chars, separator, at);
        if (parser.Sequence(argument: false) is not { } nodes || !parser.AtEnd)
            return null;
        if (Malformed(nodes))
            return [new TextInline(error, errorFormat)];
        var shown = new List<(char C, RunFormat F)>();
        var raised = new Dictionary<(RunFormat, double), RunFormat>();
        if (!Show(nodes, 0, shown, raised))
            return null;
        var inlines = new List<Inline>();
        for (var i = 0; i < shown.Count;)
        {
            var j = i;
            while (j < shown.Count && ReferenceEquals(shown[j].F, shown[i].F))
                j++;
            inlines.Add(new TextInline(new string(shown.Skip(i).Take(j - i).Select(s => s.C).ToArray()), shown[i].F));
            i = j;
        }
        return inlines;
    }

    /// <summary>Whether a fraction has other than its numerator and denominator, an integral other than its limits and integrand.</summary>
    static bool Malformed(List<Node> nodes) =>
        nodes.OfType<Function>().Any(f => f.Name == 'f' && f.Arguments.Count != 2 || f.Name == 'i' && f.Arguments.Count != 3
            || f.Arguments.Any(Malformed));

    /// <summary>Appends the characters the nodes show, raised by points; false where one is not drawn here.</summary>
    static bool Show(List<Node> nodes, double raise, List<(char C, RunFormat F)> shown, Dictionary<(RunFormat, double), RunFormat> raised)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case Character c:
                    shown.Add((c.Value, Raised(c.Format, raise, raised)));
                    break;
                case Function { Name: 'o', Arguments.Count: 1 } overstrike:
                    if (!Show(overstrike.Arguments[0], raise, shown, raised))
                        return false;
                    break;
                case Function { Name: 's', Arguments.Count: 1 } script when script.Options.Count > 0 && script.Options.All(o => o.Name is "up" or "do"):
                    // \up and \do without a number move 2 points.
                    var by = script.Options.Sum(o => (o.Name == "up" ? 1 : -1) * (double.TryParse(o.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var points) ? points : 2));
                    if (!Show(script.Arguments[0], raise + by, shown, raised))
                        return false;
                    break;
                default:
                    return false;
            }
        }
        return true;
    }

    static RunFormat Raised(RunFormat format, double raise, Dictionary<(RunFormat, double), RunFormat> raised)
    {
        if (raise == 0)
            return format;
        if (!raised.TryGetValue((format, raise), out var moved))
        {
            moved = format.Clone();
            moved.Position = (format.Position ?? 0) + raise;
            raised[(format, raise)] = moved;
        }
        return moved;
    }

    /// <summary>Reads an instruction's text and functions: \x, its options (\up 12, \co3, \bc\{) and its arguments in parentheses.</summary>
    sealed class Parser(List<(char C, RunFormat F)> chars, char separator, int at)
    {
        public bool AtEnd => at >= chars.Count;

        /// <summary>Text and functions to the end, or in an argument to its separator or closing parenthesis; null where malformed.</summary>
        public List<Node>? Sequence(bool argument)
        {
            var nodes = new List<Node>();
            var depth = 0;
            while (at < chars.Count)
            {
                var (c, format) = chars[at];
                if (c == '\\' && at + 1 < chars.Count)
                {
                    var next = chars[at + 1].C;
                    if (char.IsAsciiLetter(next))
                    {
                        if (Function() is not { } function)
                            return null;
                        nodes.Add(function);
                    }
                    else if (next == '*')
                        FormatSwitch();
                    else
                    {
                        // An escaped character: \( \) \, \; \\.
                        nodes.Add(new Character(next, chars[at + 1].F));
                        at += 2;
                    }
                    continue;
                }
                if (argument)
                {
                    if (c == ')' && depth == 0 || c == separator && depth == 0)
                        break;
                    if (c == '(')
                        depth++;
                    else if (c == ')')
                        depth--;
                }
                nodes.Add(new Character(c, format));
                at++;
            }
            return nodes;
        }

        /// <summary>A \* switch with its argument (jc0, "Font:…", hps16, MERGEFORMAT) and the spaces after it, which print nothing.</summary>
        void FormatSwitch()
        {
            at += 2;
            SkipSpaces();
            if (at < chars.Count && chars[at].C == '"')
            {
                at++;
                while (at < chars.Count && chars[at].C != '"')
                    at++;
                at++;
            }
            else
                while (at < chars.Count && !char.IsWhiteSpace(chars[at].C) && chars[at].C != '\\')
                    at++;
            SkipSpaces();
        }

        Function? Function()
        {
            at++;
            var name = char.ToLowerInvariant(chars[at].C);
            at++;
            if (at < chars.Count && char.IsAsciiLetter(chars[at].C))
                return null;
            var options = new List<(string Name, string Value)>();
            while (true)
            {
                SkipSpaces();
                if (at + 1 >= chars.Count || chars[at].C != '\\' || !char.IsAsciiLetter(chars[at + 1].C))
                    break;
                at++;
                var option = "";
                while (at < chars.Count && char.IsAsciiLetter(chars[at].C))
                    option += char.ToLowerInvariant(chars[at++].C);
                var value = "";
                if (option is "lc" or "rc" or "bc" or "fc" or "vc")
                {
                    // A character: \bc\{ or \bc[.
                    if (at + 1 < chars.Count && chars[at].C == '\\' && !char.IsAsciiLetter(chars[at + 1].C))
                        at++;
                    if (at < chars.Count && chars[at].C != '(')
                        value = chars[at++].C.ToString();
                }
                else
                {
                    SkipSpaces();
                    while (at < chars.Count && (char.IsAsciiDigit(chars[at].C) || chars[at].C == '.'))
                        value += chars[at++].C;
                }
                options.Add((option, value));
            }
            if (at >= chars.Count || chars[at].C != '(')
                return null;
            at++;
            var arguments = new List<List<Node>>();
            while (true)
            {
                if (Sequence(argument: true) is not { } argument || at >= chars.Count)
                    return null;
                arguments.Add(argument);
                if (chars[at++].C == ')')
                    return new Function(name, options, arguments);
            }
        }

        void SkipSpaces()
        {
            while (at < chars.Count && chars[at].C == ' ')
                at++;
        }
    }
}
