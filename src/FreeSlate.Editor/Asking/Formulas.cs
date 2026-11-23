using System.Globalization;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// Formulas as the notation writes them (see Notation, Prompt) and as Word keeps them: LaTeX ($…$ among the text, $$…$$ in a paragraph of
/// their own) made Office Math (m:oMath), and Office Math read back as LaTeX. What LaTeX it reads: groups, superscripts and subscripts,
/// fractions and binomials, roots, brackets that grow (\left \right), sums, products and integrals with their limits, limits, functions
/// (\sin …), accents and bars, matrices and cases, text, Greek letters and the usual operators and arrows. Office Math it cannot say in
/// LaTeX reads as null (the formula then stays an object of its own, see Objects).
/// </summary>
static class Formulas
{
    static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";

    /// <summary>The symbols LaTeX names, as Office Math writes them.</summary>
    static readonly Dictionary<string, string> Symbols = new(StringComparer.Ordinal)
    {
        ["alpha"] = "α", ["beta"] = "β", ["gamma"] = "γ", ["delta"] = "δ", ["epsilon"] = "ϵ", ["varepsilon"] = "ε", ["zeta"] = "ζ", ["eta"] = "η",
        ["theta"] = "θ", ["vartheta"] = "ϑ", ["iota"] = "ι", ["kappa"] = "κ", ["lambda"] = "λ", ["mu"] = "μ", ["nu"] = "ν", ["xi"] = "ξ", ["pi"] = "π",
        ["varpi"] = "ϖ", ["rho"] = "ρ", ["sigma"] = "σ", ["varsigma"] = "ς", ["tau"] = "τ", ["upsilon"] = "υ", ["phi"] = "ϕ", ["varphi"] = "φ",
        ["chi"] = "χ", ["psi"] = "ψ", ["omega"] = "ω", ["Gamma"] = "Γ", ["Delta"] = "Δ", ["Theta"] = "Θ", ["Lambda"] = "Λ", ["Xi"] = "Ξ",
        ["Pi"] = "Π", ["Sigma"] = "Σ", ["Upsilon"] = "Υ", ["Phi"] = "Φ", ["Psi"] = "Ψ", ["Omega"] = "Ω",
        ["cdot"] = "·", ["times"] = "×", ["div"] = "÷", ["pm"] = "±", ["mp"] = "∓", ["le"] = "≤", ["leq"] = "≤", ["ge"] = "≥", ["geq"] = "≥",
        ["neq"] = "≠", ["ne"] = "≠", ["approx"] = "≈", ["equiv"] = "≡", ["sim"] = "∼", ["simeq"] = "≃", ["cong"] = "≅", ["propto"] = "∝",
        ["infty"] = "∞", ["to"] = "→", ["rightarrow"] = "→", ["leftarrow"] = "←", ["leftrightarrow"] = "↔", ["Rightarrow"] = "⇒",
        ["Leftarrow"] = "⇐", ["Leftrightarrow"] = "⇔", ["mapsto"] = "↦", ["in"] = "∈", ["notin"] = "∉", ["ni"] = "∋", ["subset"] = "⊂",
        ["subseteq"] = "⊆", ["supset"] = "⊃", ["supseteq"] = "⊇", ["cup"] = "∪", ["cap"] = "∩", ["setminus"] = "∖", ["emptyset"] = "∅",
        ["varnothing"] = "∅", ["forall"] = "∀", ["exists"] = "∃", ["neg"] = "¬", ["land"] = "∧", ["wedge"] = "∧", ["lor"] = "∨", ["vee"] = "∨",
        ["partial"] = "∂", ["nabla"] = "∇", ["angle"] = "∠", ["perp"] = "⊥", ["parallel"] = "∥", ["degree"] = "°", ["circ"] = "∘",
        ["ldots"] = "…", ["dots"] = "…", ["cdots"] = "⋯", ["vdots"] = "⋮", ["ddots"] = "⋱", ["prime"] = "′", ["ast"] = "∗", ["star"] = "⋆",
        ["bullet"] = "∙", ["ll"] = "≪", ["gg"] = "≫", ["mid"] = "∣", ["hbar"] = "ℏ", ["ell"] = "ℓ", ["Re"] = "ℜ", ["Im"] = "ℑ",
        ["aleph"] = "ℵ", ["triangle"] = "△", ["square"] = "□", ["therefore"] = "∴", ["because"] = "∵",
        ["{"] = "{", ["}"] = "}", ["%"] = "%", ["$"] = "$", ["#"] = "#", ["&"] = "&", ["_"] = "_", ["|"] = "‖", ["backslash"] = "\\",
        ["lbrace"] = "{", ["rbrace"] = "}", ["langle"] = "⟨", ["rangle"] = "⟩", ["lfloor"] = "⌊", ["rfloor"] = "⌋", ["lceil"] = "⌈", ["rceil"] = "⌉",
        [","] = " ", [";"] = " ", [":"] = " ", ["quad"] = " ", ["qquad"] = "  ", [" "] = " ", ["!"] = "",
    };

    /// <summary>The big operators: their character, and whether their limits stand under and over them (else beside, as an integral's).</summary>
    static readonly Dictionary<string, (string Character, bool Over)> Operators = new(StringComparer.Ordinal)
    {
        ["sum"] = ("∑", true), ["prod"] = ("∏", true), ["coprod"] = ("∐", true), ["bigcup"] = ("⋃", true), ["bigcap"] = ("⋂", true),
        ["int"] = ("∫", false), ["iint"] = ("∬", false), ["iiint"] = ("∭", false), ["oint"] = ("∮", false),
    };

    /// <summary>The functions LaTeX names, set upright.</summary>
    static readonly HashSet<string> Functions = new(StringComparer.Ordinal)
    {
        "sin", "cos", "tan", "cot", "sec", "csc", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh", "log", "ln", "lg", "exp", "min", "max",
        "det", "dim", "gcd", "ker", "deg", "arg", "sup", "inf", "lim",
    };

    /// <summary>The accents LaTeX names, as combining characters.</summary>
    static readonly Dictionary<string, string> Accents = new(StringComparer.Ordinal)
    {
        ["vec"] = "⃗", ["hat"] = "̂", ["widehat"] = "̂", ["bar"] = "̅", ["dot"] = "̇", ["ddot"] = "̈", ["tilde"] = "̃", ["widetilde"] = "̃",
    };

    /// <summary>Office Math for LaTeX (the content of an m:oMath), as far as it reads it; what it does not know as its text.</summary>
    public static string Omml(string latex) => new Reader(latex).Expression(stop: null);

    /// <summary>A formula among the text: an m:oMath; one in a paragraph of its own: an m:oMathPara, centred.</summary>
    public static string Inline(string latex) => $"<m:oMath>{Omml(latex)}</m:oMath>";

    public static string Display(string latex) =>
        $"<w:p><w:pPr><w:jc w:val=\"center\"/></w:pPr><m:oMathPara><m:oMathParaPr><m:jc m:val=\"center\"/></m:oMathParaPr><m:oMath>{Omml(latex)}</m:oMath></m:oMathPara></w:p>";

    /// <summary>LaTeX read into Office Math, token by token.</summary>
    sealed class Reader(string text)
    {
        int at;

        /// <summary>Items up to a closing brace, an environment's end, a \right or the text's end (or the stop given).</summary>
        public string Expression(string? stop)
        {
            var built = new StringBuilder();
            while (true)
            {
                Skip();
                if (at >= text.Length)
                    break;
                if (text[at] == '}' || stop != null && Ahead(stop) || Ahead("\\right") || Ahead("\\end") || stop == "&" && (Ahead("&") || Ahead("\\\\")))
                    break;
                built.Append(Item());
            }
            return built.ToString();
        }

        bool Ahead(string token) => string.CompareOrdinal(text, at, token, 0, token.Length) == 0;

        void Skip()
        {
            while (at < text.Length && char.IsWhiteSpace(text[at]))
                at++;
        }

        /// <summary>An item: an atom with its scripts, a big operator with its limits and what it applies to, a function with its argument.</summary>
        string Item()
        {
            var (atom, kind) = Atom();
            if (kind == "operator")
                return atom;
            var (sub, sup) = Scripts();
            if (kind == "function")
            {
                var name = sub == null && sup == null ? atom : Scripted(atom, sub, sup);
                if (sub != null && atom.Contains(">lim<", StringComparison.Ordinal))
                    name = $"<m:limLow><m:e>{atom}</m:e><m:lim>{sub}</m:lim></m:limLow>";
                Skip();
                var argument = at < text.Length && text[at] != '}' && !Ahead("\\right") && !Ahead("&") ? Item() : "";
                return $"<m:func><m:fName>{name}</m:fName><m:e>{argument}</m:e></m:func>";
            }
            return Scripted(atom, sub, sup);
        }

        static string Scripted(string atom, string? sub, string? sup) => (sub, sup) switch
        {
            (null, null) => atom,
            (not null, null) => $"<m:sSub><m:e>{atom}</m:e><m:sub>{sub}</m:sub></m:sSub>",
            (null, not null) => $"<m:sSup><m:e>{atom}</m:e><m:sup>{sup}</m:sup></m:sSup>",
            _ => $"<m:sSubSup><m:e>{atom}</m:e><m:sub>{sub}</m:sub><m:sup>{sup}</m:sup></m:sSubSup>",
        };

        /// <summary>The subscript and superscript after an atom, either first.</summary>
        (string? Sub, string? Sup) Scripts()
        {
            string? sub = null, sup = null;
            for (var k = 0; k < 2; k++)
            {
                Skip();
                if (at < text.Length && text[at] == '_' && sub == null)
                {
                    at++;
                    sub = Argument();
                }
                else if (at < text.Length && text[at] == '^' && sup == null)
                {
                    at++;
                    sup = Argument();
                }
                else if (at < text.Length && text[at] == '\'' && sup == null)
                {
                    var primes = 0;
                    while (at < text.Length && text[at] == '\'')
                    {
                        primes++;
                        at++;
                    }
                    sup = Run(new string('′', primes));
                }
            }
            return (sub, sup);
        }

        /// <summary>An argument: a group in braces, else one atom (a character, a command).</summary>
        string Argument()
        {
            Skip();
            if (at < text.Length && text[at] == '{')
                return Group();
            if (at >= text.Length)
                return "";
            return Atom().Built;
        }

        string Group()
        {
            at++;
            var inner = Expression(stop: null);
            if (at < text.Length && text[at] == '}')
                at++;
            return inner;
        }

        /// <summary>An atom: a group, a command, a number, a letter or a sign; and what kind of atom it is.</summary>
        (string Built, string Kind) Atom()
        {
            Skip();
            var c = text[at];
            if (c == '{')
                return (Group(), "group");
            if (c == '\\')
                return Command();
            if (char.IsDigit(c) || c == '.' && at + 1 < text.Length && char.IsDigit(text[at + 1]))
            {
                var start = at;
                while (at < text.Length && (char.IsDigit(text[at]) || text[at] is '.' or ',' && at + 1 < text.Length && char.IsDigit(text[at + 1])))
                    at++;
                return (Run(text[start..at]), "number");
            }
            at++;
            return (Run(c == '~' ? " " : c.ToString()), "sign");
        }

        /// <summary>A command after its backslash.</summary>
        (string Built, string Kind) Command()
        {
            at++;
            var start = at;
            if (at < text.Length && char.IsLetter(text[at]))
            {
                while (at < text.Length && char.IsLetter(text[at]))
                    at++;
            }
            else if (at < text.Length)
                at++;
            var name = text[start..at];
            switch (name)
            {
                case "frac" or "dfrac" or "tfrac" or "cfrac":
                {
                    var (numerator, denominator) = (Argument(), Argument());
                    return ($"<m:f><m:num>{numerator}</m:num><m:den>{denominator}</m:den></m:f>", "fraction");
                }
                case "binom" or "dbinom" or "tbinom":
                {
                    var (n, k) = (Argument(), Argument());
                    return ($"<m:d><m:dPr><m:begChr m:val=\"(\"/><m:endChr m:val=\")\"/></m:dPr><m:e><m:f><m:fPr><m:type m:val=\"noBar\"/></m:fPr><m:num>{n}</m:num><m:den>{k}</m:den></m:f></m:e></m:d>", "fraction");
                }
                case "sqrt":
                {
                    Skip();
                    string? degree = null;
                    if (at < text.Length && text[at] == '[')
                    {
                        var close = text.IndexOf(']', at);
                        degree = new Reader(text[(at + 1)..(close < 0 ? text.Length : close)]).Expression(stop: null);
                        at = close < 0 ? text.Length : close + 1;
                    }
                    var radicand = Argument();
                    return (degree == null
                        ? $"<m:rad><m:radPr><m:degHide m:val=\"1\"/></m:radPr><m:deg/><m:e>{radicand}</m:e></m:rad>"
                        : $"<m:rad><m:deg>{degree}</m:deg><m:e>{radicand}</m:e></m:rad>", "root");
                }
                case "left":
                {
                    var open = Delimiter();
                    var inner = Expression(stop: null);
                    var shut = "";
                    if (Ahead("\\right"))
                    {
                        at += "\\right".Length;
                        shut = Delimiter();
                    }
                    return (Brackets(open, shut, inner), "group");
                }
                case "text" or "textrm" or "mbox" or "textup":
                    return (Run(Literal(), normal: true), "text");
                case "mathrm" or "operatorname" or "mathup":
                    return (Run(Literal(), style: "p"), "text");
                case "mathbf" or "textbf" or "boldsymbol":
                    return (Run(Literal(), style: "b"), "text");
                case "mathit" or "textit":
                    return (Run(Literal(), style: "i"), "text");
                case "overline":
                    return ($"<m:bar><m:barPr><m:pos m:val=\"top\"/></m:barPr><m:e>{Argument()}</m:e></m:bar>", "accent");
                case "underline":
                    return ($"<m:bar><m:barPr><m:pos m:val=\"bot\"/></m:barPr><m:e>{Argument()}</m:e></m:bar>", "accent");
                case "begin":
                    return (Environment(), "group");
                case "\\":
                    return (Run(" "), "sign");
            }
            if (Accents.TryGetValue(name, out var accent))
                return ($"<m:acc><m:accPr><m:chr m:val=\"{accent}\"/></m:accPr><m:e>{Argument()}</m:e></m:acc>", "accent");
            if (Operators.TryGetValue(name, out var big))
            {
                var (sub, sup) = Scripts();
                Skip();
                var operand = at < text.Length && text[at] != '}' && !Ahead("\\right") && !Ahead("&") ? Item() : "";
                var properties = $"<m:chr m:val=\"{big.Character}\"/><m:limLoc m:val=\"{(big.Over ? "undOvr" : "subSup")}\"/>"
                    + (sub == null ? "<m:subHide m:val=\"1\"/>" : "") + (sup == null ? "<m:supHide m:val=\"1\"/>" : "");
                return ($"<m:nary><m:naryPr>{properties}</m:naryPr><m:sub>{sub}</m:sub><m:sup>{sup}</m:sup><m:e>{operand}</m:e></m:nary>", "operator");
            }
            if (Functions.Contains(name))
                return (Run(name, style: "p"), "function");
            if (Symbols.TryGetValue(name, out var symbol))
                return (symbol.Length == 0 ? "" : Run(symbol), "sign");
            // A command it does not know: its name as text.
            return (Run(name, style: "p"), "sign");
        }

        /// <summary>A delimiter after \left or \right: a character, an escaped brace, a named one (\langle), or none (".").</summary>
        string Delimiter()
        {
            Skip();
            if (at >= text.Length)
                return "";
            if (text[at] == '\\')
            {
                at++;
                var start = at;
                while (at < text.Length && char.IsLetter(text[at]))
                    at++;
                if (at == start && at < text.Length)
                    at++;
                var name = text[start..at];
                return name switch { "{" or "lbrace" => "{", "}" or "rbrace" => "}", "|" => "‖", _ => Symbols.GetValueOrDefault(name, "") };
            }
            var c = text[at++];
            return c == '.' ? "" : c.ToString();
        }

        static string Brackets(string open, string shut, string inner) =>
            $"<m:d><m:dPr><m:begChr m:val=\"{Escape(open)}\"/><m:endChr m:val=\"{Escape(shut)}\"/></m:dPr><m:e>{inner}</m:e></m:d>";

        /// <summary>A group's text as it stands (for \text and its kin).</summary>
        string Literal()
        {
            Skip();
            if (at >= text.Length || text[at] != '{')
                return at < text.Length ? text[at++].ToString() : "";
            var depth = 0;
            var start = ++at;
            while (at < text.Length && (text[at] != '}' || depth > 0))
            {
                if (text[at] == '{')
                    depth++;
                else if (text[at] == '}')
                    depth--;
                at++;
            }
            var literal = text[start..Math.Min(at, text.Length)];
            if (at < text.Length)
                at++;
            return literal.Replace("\\ ", " ", StringComparison.Ordinal);
        }

        /// <summary>An environment of rows and columns: a matrix in its brackets, or cases in a brace on the left.</summary>
        string Environment()
        {
            var name = Literal();
            var rows = new List<List<string>>();
            var row = new List<string>();
            while (at < text.Length)
            {
                row.Add(Expression(stop: "&"));
                Skip();
                if (Ahead("&"))
                {
                    at++;
                    continue;
                }
                if (Ahead("\\\\"))
                {
                    at += 2;
                    rows.Add(row);
                    row = [];
                    continue;
                }
                if (Ahead("\\end"))
                {
                    at += "\\end".Length;
                    Literal();
                }
                break;
            }
            if (row.Count > 0 && (row.Count > 1 || row[0].Length > 0))
                rows.Add(row);
            var columns = rows.Count == 0 ? 1 : rows.Max(r => r.Count);
            var matrix = new StringBuilder($"<m:m><m:mPr><m:mcs><m:mc><m:mcPr><m:count m:val=\"{columns}\"/><m:mcJc m:val=\"{(name.StartsWith("cases", StringComparison.Ordinal) ? "left" : "center")}\"/></m:mcPr></m:mc></m:mcs></m:mPr>");
            foreach (var cells in rows)
            {
                matrix.Append("<m:mr>");
                for (var k = 0; k < columns; k++)
                    matrix.Append("<m:e>").Append(k < cells.Count ? cells[k] : "").Append("</m:e>");
                matrix.Append("</m:mr>");
            }
            matrix.Append("</m:m>");
            var (open, shut) = name switch
            {
                "pmatrix" => ("(", ")"),
                "bmatrix" => ("[", "]"),
                "Bmatrix" => ("{", "}"),
                "vmatrix" => ("|", "|"),
                "Vmatrix" => ("‖", "‖"),
                "cases" => ("{", ""),
                _ => ("", ""),
            };
            return open.Length + shut.Length == 0 ? matrix.ToString() : Brackets(open, shut, matrix.ToString());
        }
    }

    /// <summary>A run of math text: italic as math is (the default), upright (p), bold (b) or italic (i), or normal text.</summary>
    static string Run(string text, string? style = null, bool normal = false)
    {
        if (text.Length == 0)
            return "";
        var properties = normal ? "<m:rPr><m:nor/></m:rPr>" : style != null ? $"<m:rPr><m:sty m:val=\"{style}\"/></m:rPr>" : "";
        return $"<m:r>{properties}<m:t xml:space=\"preserve\">{Escape(text)}</m:t></m:r>";
    }

    static string Escape(string text) => SecurityElement.Escape(text) ?? "";

    /// <summary>LaTeX for Office Math (an m:oMath or m:oMathPara, or an element within one); null where it holds what LaTeX here cannot say.</summary>
    public static string? Latex(XElement math)
    {
        try
        {
            return Written(math).Trim();
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    static readonly Dictionary<string, string> Named = Symbols.Where(s => s.Value.Length == 1 && s.Key.All(char.IsLetter))
        .GroupBy(s => s.Value).ToDictionary(g => g.Key, g => g.OrderBy(s => s.Key.Length).First().Key);

    static string Written(XElement element)
    {
        string Of(string name) => element.Element(M + name) is { } child ? Children(child) : "";
        string Children(XElement parent) => string.Concat(parent.Elements().Select(Written));
        string Property(string holder, string name) => (string?)element.Element(M + holder)?.Element(M + name)?.Attribute(M + "val") ?? "";
        switch (element.Name.LocalName)
        {
            case "oMathPara" or "oMath" or "e" or "num" or "den" or "sub" or "sup" or "deg" or "lim" or "fName":
                return Children(element);
            case "oMathParaPr" or "rPr" or "ctrlPr" or "argPr" or "dPr" or "fPr" or "radPr" or "naryPr" or "funcPr" or "sSubPr" or "sSupPr"
                or "sSubSupPr" or "accPr" or "barPr" or "mPr" or "limLowPr" or "limUppPr" or "bookmarkStart" or "bookmarkEnd" or "proofErr":
                return "";
            case "r":
            {
                var text = string.Concat(element.Elements(M + "t").Select(t => t.Value)) + string.Concat(element.Elements().Where(e => e.Name.LocalName == "t" && e.Name.Namespace != M).Select(t => t.Value));
                var upright = (string?)element.Element(M + "rPr")?.Element(M + "sty")?.Attribute(M + "val") == "p";
                var plain = element.Element(M + "rPr")?.Element(M + "nor") != null;
                if (plain)
                    return "\\text{" + text + "}";
                if (upright && text.Length > 1 && text.All(char.IsLetter))
                    return Functions.Contains(text) ? "\\" + text + " " : "\\mathrm{" + text + "}";
                return string.Concat(text.Select(c => c.ToString() is var s && Named.TryGetValue(s, out var name) ? "\\" + name + " " : c switch
                {
                    '{' => "\\{",
                    '}' => "\\}",
                    '%' => "\\%",
                    '#' => "\\#",
                    '&' => "\\&",
                    _ => s,
                }));
            }
            case "f":
            {
                var command = Property("fPr", "type") == "noBar" ? "\\binom" : "\\frac";
                return $"{command}{{{Of("num")}}}{{{Of("den")}}}";
            }
            case "sSup":
                return $"{Base(Of("e"))}^{Braced(Of("sup"))}";
            case "sSub":
                return $"{Base(Of("e"))}_{Braced(Of("sub"))}";
            case "sSubSup":
                return $"{Base(Of("e"))}_{Braced(Of("sub"))}^{Braced(Of("sup"))}";
            case "rad":
                return Property("radPr", "degHide") is "1" or "on" or "true" || Of("deg").Length == 0 ? $"\\sqrt{{{Of("e")}}}" : $"\\sqrt[{Of("deg")}]{{{Of("e")}}}";
            case "d":
            {
                var properties = element.Element(M + "dPr");
                var open = (string?)properties?.Element(M + "begChr")?.Attribute(M + "val") ?? "(";
                var shut = (string?)properties?.Element(M + "endChr")?.Attribute(M + "val") ?? ")";
                var parts = element.Elements(M + "e").Select(Written).ToList();
                if (parts.Count == 1 && element.Element(M + "e")?.Elements().FirstOrDefault() is { Name.LocalName: "m" } matrix && element.Element(M + "e")!.Elements().Count() == 1)
                {
                    var environment = (open, shut) switch { ("(", ")") => "pmatrix", ("[", "]") => "bmatrix", ("{", "}") => "Bmatrix", ("|", "|") => "vmatrix", ("{", "") => "cases", _ => "matrix" };
                    return $"\\begin{{{environment}}}{Rows(matrix)}\\end{{{environment}}}";
                }
                return $"\\left{Delimiter(open)}{string.Join(",", parts)}\\right{Delimiter(shut)}";
            }
            case "m":
                return $"\\begin{{matrix}}{Rows(element)}\\end{{matrix}}";
            case "nary":
            {
                var character = Property("naryPr", "chr");
                var name = Operators.FirstOrDefault(o => o.Value.Character == (character.Length > 0 ? character : "∫")).Key ?? "int";
                var sub = Property("naryPr", "subHide") is "1" or "on" ? "" : $"_{{{Of("sub")}}}";
                var sup = Property("naryPr", "supHide") is "1" or "on" ? "" : $"^{{{Of("sup")}}}";
                return $"\\{name}{sub}{sup} {Of("e")}";
            }
            case "func":
                return $"{Of("fName").TrimEnd()} {Of("e")}";
            case "limLow":
                return $"{Of("e").TrimEnd()}_{{{Of("lim")}}}";
            case "limUpp":
                return $"{Of("e").TrimEnd()}^{{{Of("lim")}}}";
            case "acc":
            {
                var character = Property("accPr", "chr");
                var name = Accents.FirstOrDefault(a => a.Value == (character.Length > 0 ? character : "̂")).Key ?? "hat";
                return $"\\{name}{{{Of("e")}}}";
            }
            case "bar":
                return Property("barPr", "pos") == "top" ? $"\\overline{{{Of("e")}}}" : $"\\underline{{{Of("e")}}}";
            case "box" or "borderBox" or "phant" or "groupChr":
                return Of("e");
            default:
                throw new NotSupportedException(element.Name.LocalName);
        }
    }

    /// <summary>A script's LaTeX: one character as it is, else in braces.</summary>
    static string Braced(string latex) => latex.Length == 1 ? latex : "{" + latex + "}";

    /// <summary>A base's LaTeX: one character or a command alone as it is, else in braces.</summary>
    static string Base(string latex)
    {
        var trimmed = latex.TrimEnd();
        return trimmed.Length == 1 || trimmed.Length > 1 && trimmed[0] == '\\' && trimmed.Skip(1).All(char.IsLetter) ? trimmed : "{" + latex + "}";
    }

    static string Rows(XElement matrix) =>
        string.Join("\\\\", matrix.Elements(M + "mr").Select(row => string.Join("&", row.Elements(M + "e").Select(Written))));

    static string Delimiter(string character) => character switch
    {
        "" => ".",
        "{" => "\\{",
        "}" => "\\}",
        "⟨" => "\\langle ",
        "⟩" => "\\rangle ",
        "‖" => "\\|",
        _ => character,
    };
}
