using System.Text;
using System.Xml.Linq;
using FreeSlate.Drawing;
using FreeSlate.Text;

namespace FreeSlate.Equations;

/// <summary>The formatting a math run takes from its run properties: size in points and color.</summary>
/// <summary>A math run's size and color, and the text font its normal text (m:nor) is set in, if any.</summary>
public readonly record struct MathRunStyle(double Size, RgbColor Color, FontData? Font = null);

/// <summary>
/// Lays out Office Math (OMML, catalog: text.math) with the OpenType MATH table of the math font, the way TeX and
/// MathML Core do: atoms of runs, fractions, scripts, radicals, n-ary operators, delimiters, matrices, accents, bars,
/// group characters, limits, functions and boxes, spaced by their classes, in display, text, script and
/// script-script levels.
/// </summary>
public sealed class MathLayout
{
    public static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";
    static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    readonly FontData font;
    readonly MathTable table;
    readonly Func<XElement?, MathRunStyle> runStyle;
    readonly XName runProperties;

    /// <param name="runStyle">Size and color of a math run from its run properties (null: the paragraph's).</param>
    /// <param name="runProperties">The run properties of math runs: a:rPr in presentations (the default), w:rPr in documents.</param>
    public MathLayout(FontData font, MathTable table, Func<XElement?, MathRunStyle> runStyle, XName? runProperties = null)
    {
        this.font = font;
        this.table = table;
        this.runStyle = runStyle;
        this.runProperties = runProperties ?? A + "rPr";
    }

    /// <summary>Whether delimiters grow to the content's height (Word) rather than twice its reach from the axis (PowerPoint).</summary>
    public bool DelimitersByHeight { get; init; }

    /// <summary>Where integrals and the other n-ary operators put their limits when m:limLoc does not say (subSup, undOvr).</summary>
    public string IntegralLimits { get; init; } = "subSup";
    public string NaryLimits { get; init; } = "undOvr";

    enum Level { Display, Text, Script, ScriptScript }

    /// <param name="Inline">The equation is inline (m:oMath in a line of text) rather than display (m:oMathPara).</param>
    readonly record struct Env(Level Level, bool Cramped, bool Inline)
    {
        /// <summary>The level of superscripts and limits.</summary>
        public Env Sup => this with { Level = Level <= Level.Text ? Level.Script : Level.ScriptScript };

        /// <summary>The level of subscripts (cramped).</summary>
        public Env Sub => Sup with { Cramped = true };

        /// <summary>
        /// The numerator's level. Inline, a level smaller each time, as in TeX; in display equations Office keeps the
        /// size (measured: nested fractions stay full size, with the shifts of text style, and a fraction in a
        /// superscript keeps the script size).
        /// </summary>
        public Env Num => this with
        {
            Level = Level switch
            {
                Level.Display => Level.Text,
                Level.Text when Inline => Level.Script,
                Level.Script when Inline => Level.ScriptScript,
                var same => same,
            },
        };

        /// <summary>The denominator's level (cramped).</summary>
        public Env Den => Num with { Cramped = true };

        public Env Crampd => this with { Cramped = true };
    }

    enum AtomClass { Ord, Op, Bin, Rel, Open, Close, Punct, Inner }

    /// <summary>An atom of a row; its right side may space like another class (a delimited group: open, then close).</summary>
    sealed record Atom(MathItem Item, AtomClass Class)
    {
        public AtomClass Right { get; init; } = Class;

        /// <summary>An alignment point of an equation array row (an "&amp;" in its text), not drawn.</summary>
        public bool Marker { get; init; }

        /// <summary>A function's name: at least a thin space follows it, also before an opening delimiter.</summary>
        public bool FunctionName { get; init; }
    }

    /// <summary>
    /// An equation (m:oMath) in display style (inside m:oMathPara) or inline, with the math leading above it
    /// (measured: Office's equations stand MathLeading below the top of their line). A display equation reaches at
    /// least the math font's ascent and descent (measured: lines of letters with and without ascenders sit at the
    /// same height).
    /// </summary>
    public MathItem Equation(XElement oMath, bool display)
    {
        var size = Size(oMath);
        var item = Sequence(oMath.Elements(), new Env(display ? Level.Display : Level.Text, false, !display), size);
        return Finished(item, display, size);
    }

    MathItem Finished(MathItem item, bool display, double size)
    {
        if (display)
        {
            item.Ascent = Math.Max(item.Ascent, font.Ascent * size);
            item.Descent = Math.Max(item.Descent, font.Descent * size);
        }
        item.Ascent += U(table.MathLeading, size);
        return item;
    }

    /// <summary>
    /// A display equation broken into lines: the first no wider than <paramref name="width"/>, the others no wider
    /// than that less <paramref name="indent"/>, at the binary and relation operators of its top level, which keep
    /// their spacing at the start of a line (measured: Word breaks a long sum before a "+" and indents the next line
    /// by m:wrapIndent). An equation that fits, or has nowhere to break, stays one line.
    /// </summary>
    /// <param name="binary">
    /// Where a line breaks at a binary operator (m:brkBin): before it, after it, or at both (repeat), a repeated minus
    /// shown as <paramref name="subtraction"/> says (m:brkBinSub: "--", "-+" or "+-", the sign at the end and the one
    /// at the start; measured with docx/local/mathset-a to -c).
    /// </param>
    public List<MathItem> EquationLines(XElement oMath, double width, double indent, string binary = "before", string subtraction = "--")
    {
        var whole = Equation(oMath, true);
        if (whole.Width <= width + 0.01)
            return [whole];
        var size = Size(oMath);
        var env = new Env(Level.Display, false, false);
        var atoms = new List<(Atom Atom, XElement Source)>();
        foreach (var element in oMath.Elements())
            atoms.AddRange(Atoms(element, env, size).Select(a => (a, element)));
        // Segments from one operator to the next: the places a line may end.
        var segments = new List<List<(Atom Atom, XElement Source)>>();
        foreach (var atom in atoms)
        {
            if (segments.Count == 0 || atom.Atom.Class is AtomClass.Bin or AtomClass.Rel && segments[^1].Count > 0)
                segments.Add([]);
            segments[^1].Add(atom);
        }
        if (segments.Count < 2)
            return [whole];
        // A binary operator again as another sign: its run with the sign as its text.
        Atom Sign((Atom Atom, XElement Source) op, char sign)
        {
            if (op.Source.Name != M + "r")
                return op.Atom;
            var run = new XElement(op.Source);
            run.Elements(M + "t").Remove();
            run.Add(new XElement(M + "t", sign.ToString()));
            return Run(run, env).FirstOrDefault() ?? op.Atom;
        }
        var lines = new List<MathItem>();
        var current = segments[0].Select(a => a.Atom).ToList();
        for (var k = 1; k < segments.Count; k++)
        {
            var limit = lines.Count == 0 ? width : width - indent;
            var segment = segments[k].Select(a => a.Atom).ToList();
            var candidate = current.Concat(segment).ToList();
            if (Row([.. candidate], env, size, lines.Count > 0).Width <= limit + 0.01)
            {
                current = candidate;
                continue;
            }
            var op = segments[k][0];
            if (op.Atom.Class == AtomClass.Bin && binary is "after" or "repeat")
            {
                var minus = op.Source.Value.Trim() is "\u2212" or "-";
                var (atEnd, atStart) = binary == "repeat" && minus && subtraction.Length == 2
                    ? (Sign(op, subtraction[0] == '+' ? '+' : '\u2212'), Sign(op, subtraction[1] == '+' ? '+' : '\u2212'))
                    : (op.Atom, op.Atom);
                lines.Add(Finished(Row([.. current, atEnd], env, size, lines.Count > 0, broken: true), true, size));
                current = binary == "repeat" ? [atStart, .. segment.Skip(1)] : segment.Skip(1).ToList();
            }
            else
            {
                lines.Add(Finished(Row(current, env, size, lines.Count > 0), true, size));
                current = segment;
            }
        }
        lines.Add(Finished(Row(current, env, size, lines.Count > 0), true, size));
        return lines;
    }

    // ------------------------------------------------------------------ sequences and spacing

    /// <summary>The size of the base text where a construct has no run of its own: its control properties, else the first run's.</summary>
    double Size(XElement element)
    {
        var rPr = element.Descendants(M + "ctrlPr").Select(c => c.Element(runProperties)).FirstOrDefault(r => r != null)
                  ?? element.Descendants(M + "r").Select(r => r.Element(runProperties)).FirstOrDefault(r => r != null);
        return runStyle(rPr).Size;
    }

    double Scale(Env env) => env.Level switch
    {
        Level.Script => table.ScriptPercentScaleDown / 100.0,
        Level.ScriptScript => table.ScriptScriptPercentScaleDown / 100.0,
        _ => 1,
    };

    /// <summary>
    /// The size of a level from its base size: scripts and their scripts take the font's percentages of it in whole
    /// half points down, as Word sets them (scratch/triage/2026-10-14_agents AB-3, Word's PDFs: Cambria Math's 73 and
    /// 60 % of 12 pt print 8.5 and 7 pt, of 14 pt 10 and 8, of 11 pt 8 and 6.5, of 20 pt 14.5; docx/generated/
    /// equations, docx/external/libreoffice/extras-ooxmlexport/2120112713, math-lim, math-font-size).
    /// </summary>
    double Sized(double size, Env env) => env.Level is Level.Script or Level.ScriptScript ? Math.Floor(2 * size * Scale(env) + 1e-9) / 2 : size;

    /// <summary>Font units at the size of a level (points).</summary>
    double U(int units, double size) => units * size / font.UnitsPerEm;

    MathItem Sequence(IEnumerable<XElement> elements, Env env, double size, bool closes = true)
    {
        var atoms = new List<Atom>();
        foreach (var element in elements)
            atoms.AddRange(Atoms(element, env, size));
        return Row(atoms, env, size, closes: closes);
    }

    /// <summary>The atoms of one element of a sequence.</summary>
    IEnumerable<Atom> Atoms(XElement element, Env env, double size)
    {
        if (element.Name.Namespace != M)
            return [];
        switch (element.Name.LocalName)
        {
            case "r":
                return Run(element, env);
            case "f":
                return [new Atom(Fraction(element, env, Size(element)), AtomClass.Inner)];
            case "sSup" or "sSub" or "sSubSup":
                return [Scripts(element, env, Size(element))];
            case "sPre":
                return [new Atom(PreScripts(element, env, Size(element)), AtomClass.Ord)];
            case "rad":
                return [new Atom(Radical(element, env, Size(element)), AtomClass.Ord)];
            case "nary":
                return [Nary(element, env, Size(element))];
            case "d":
            {
                // After delimiters grown well past their own size the group spaces as an inner atom: a thin space
                // before an ordinary atom (measured with docx/local/mti-*, mth-*: ")" grown to 1.8 times its height
                // and more before "x" 1.7 to 1.85 pt in Cambria Math 11, displayed and inline; grown to 1.3 times or
                // not at all none).
                var (delimited, tall) = Delimited(element, env, Size(element));
                return [new Atom(delimited, AtomClass.Open) { Right = tall ? AtomClass.Inner : AtomClass.Close }];
            }
            case "m":
                return [new Atom(Matrix(element, env, Size(element)), AtomClass.Ord)];
            case "eqArr":
                return [new Atom(EquationArray(element, env, Size(element)), AtomClass.Ord)];
            case "acc":
                return [new Atom(Accent(element, env, Size(element)), AtomClass.Ord)];
            case "bar":
                return [new Atom(Bar(element, env, Size(element)), AtomClass.Ord)];
            // Group characters and limits space like ordinary atoms (measured: Office keeps a binary operator after
            // an underbrace with a limit binary).
            case "groupChr":
                return [new Atom(GroupCharacter(element, env, Size(element)), AtomClass.Ord)];
            case "limLow" or "limUpp":
                return [new Atom(Limit(element, env, Size(element)), AtomClass.Ord)];
            case "func":
                return Function(element, env, size);
            case "box":
                return [new Atom(Sequence(Child(element, "e"), env, size), AtomClass.Ord)];
            case "borderBox":
                return [new Atom(BorderBox(element, env, Size(element)), AtomClass.Ord)];
            case "phant":
                return [new Atom(Phantom(element, env, size), AtomClass.Ord)];
            case "e" or "oMath":
                return [new Atom(Sequence(element.Elements(), env, size), AtomClass.Ord)];
            default:
                return [];
        }
    }

    static IEnumerable<XElement> Child(XElement element, string name) => element.Element(M + name)?.Elements() ?? [];

    /// <summary>
    /// An argument of a construct (a script, a limit); one that is there but empty keeps the room of the placeholder
    /// Office shows for it while editing (measured: an integral with an empty upper limit, 0.9 em wide and 0.8 em
    /// tall at the limit's size).
    /// </summary>
    MathItem Argument(XElement element, string name, Env env, double baseSize)
    {
        if (element.Element(M + name) is { } argument && !HasContent(argument))
        {
            var em = Sized(baseSize, env);
            return new SpaceItem { Width = 0.9 * em, Ascent = 0.8 * em };
        }
        return Sequence(Child(element, name), env, baseSize);
    }

    /// <summary>
    /// A construct's base (m:e); one that is there but empty keeps the room of the placeholder Office shows for it
    /// (measured with docx/local/mph-11-*, mph-24-*: inside braces or each part between parentheses, under a sum,
    /// a root or an accent, after a function's name and before a superscript 0.92 em wide; docx/external/
    /// libreoffice/extras-ooxmlexport/2120112713_OpenBrace, tdf158023_export, math-placeholders).
    /// </summary>
    MathItem Base(XElement element, Env env, double baseSize, bool closes = true) =>
        element.Element(M + "e") is { } e && !HasContent(e) ? Placeholder(env, baseSize) : Sequence(Child(element, "e"), env, baseSize, closes);

    /// <summary>The room of an empty base's placeholder.</summary>
    SpaceItem Placeholder(Env env, double baseSize)
    {
        var em = Sized(baseSize, env);
        return new SpaceItem { Width = 0.92 * em, Ascent = 0.8 * em };
    }

    /// <summary>Whether an argument shows anything: text in a run, or a construct (which has placeholders of its own).</summary>
    static bool HasContent(XElement argument) =>
        argument.Elements().Any(e => e.Name.Namespace == M && e.Name.LocalName switch
        {
            "r" => e.Elements(M + "t").Any(t => t.Value.Length > 0),
            "argPr" or "ctrlPr" => false,
            _ => true,
        });

    static string? Property(XElement element, string properties, string name) =>
        (string?)element.Element(M + properties)?.Element(M + name)?.Attribute(M + "val");

    static bool On(string? value, bool fallback) => value switch { null => fallback, "off" or "0" or "false" => false, _ => true };

    /// <summary>
    /// Atoms side by side with the space their classes call for (TeX's table: thin, medium and thick spaces of 3, 4
    /// and 5/18 em; most of them only at display and text level), binary operators that cannot be binary turned
    /// into ordinary atoms.
    /// </summary>
    /// <param name="continued">The row continues a broken line: an operator at its start stays binary.</param>
    /// <param name="continued">Whether the row continues a broken equation: a binary operator at its start keeps its spacing.</param>
    /// <param name="broken">Whether the row ends where the equation breaks: a binary operator at its end keeps the spacing before it.</param>
    /// <param name="closes">
    /// Whether a box ends after the row (the equation, a delimiter's, a fraction's part, an accented base, a script),
    /// which keeps its last letter's italic correction; not after an n-ary's operand (LibreOffice's extras-ooxmlexport/
    /// math-nary: an equation ending in a sum over x centered as without x's correction; math-lim: one ending in a
    /// function's argument x centered with it).
    /// </param>
    MathItem Row(List<Atom> atoms, Env env, double size, bool continued = false, bool broken = false, bool closes = true)
    {
        for (var i = 0; i < atoms.Count; i++)
        {
            if (atoms[i].Class != AtomClass.Bin)
                continue;
            var before = i == 0 ? continued ? AtomClass.Ord : AtomClass.Open : atoms[i - 1].Right;
            var after = i + 1 < atoms.Count ? atoms[i + 1].Class : broken ? AtomClass.Ord : AtomClass.Close;
            if (before is AtomClass.Bin or AtomClass.Op or AtomClass.Rel or AtomClass.Open or AtomClass.Punct || after is AtomClass.Rel or AtomClass.Close or AtomClass.Punct)
                atoms[i] = atoms[i] with { Class = AtomClass.Ord, Right = AtomClass.Ord };
        }
        var row = new ListItem();
        var x = 0.0;
        var em = Sized(size, env);
        for (var i = 0; i < atoms.Count; i++)
        {
            // Spaces typed in a run stand as they are, without the spacing of their neighbors' classes.
            if (i > 0 && !IsSpace(atoms[i - 1]) && !IsSpace(atoms[i]))
                x += Math.Max(Spacing(atoms[i - 1].Right, atoms[i].Class, env), atoms[i - 1].FunctionName ? 3 : 0) * em / 18;
            row.Add(atoms[i].Item, x, 0);
            x += atoms[i].Item.Width;
            // An italic letter before an operator, a relation, a bracket, punctuation or a construct (scripts, a
            // fraction, …) keeps its italic correction, before another letter none (measured with docx/local/mth-*:
            // x+, y=, f(, x), c) at 11 pt within 0.06 pt of Cambria Math's corrections; xy and ac without;
            // powertools/RP/RP013: π before r² 0.32 pt), and so does the last letter of a row, whose box ends after
            // it: before a closing delimiter or separator, under a fraction's bar, under an accent, at the end of a
            // script and of the equation (LibreOffice's extras-ooxmlexport/math-d: x→| 0.33 pt, math-accents: a bar
            // over a 6.96 pt wide, docx/local/mth-d-close); not at the end of an n-ary's operand (closes).
            if (atoms[i].Class == AtomClass.Ord && atoms[i].Item is GlyphItem { Italic: > 0 } letter
                && (i + 1 == atoms.Count ? closes : atoms[i + 1].Class is AtomClass.Bin or AtomClass.Rel or AtomClass.Open or AtomClass.Close or AtomClass.Punct
                    || atoms[i + 1].Item is not GlyphItem))
                x += letter.Italic;
        }
        row.Width = x;
        // The last letter's correction is in the width already where the row closes a box.
        row.Italic = atoms.Count > 0 && !(closes && atoms[^1].Class == AtomClass.Ord && atoms[^1].Item is GlyphItem { Italic: > 0 }) ? atoms[^1].Item.Italic : 0;
        return row;
    }

    static bool IsSpace(Atom atom) => atom.Item is GlyphItem { Text: " " or " " };

    static double Spacing(AtomClass left, AtomClass right, Env env)
    {
        // 0 none, 1 thin, 2 medium, 3 thick; negative: only at display and text level. Unlike TeX, a closing
        // delimiter keeps a thin space before punctuation (measured: Office's "),").
        int[,] table =
        {
            //          Ord  Op  Bin  Rel Open Close Punct Inner
            /* Ord   */ { 0,  1,  -2,  -3,  0,   0,    0,   -1 },
            /* Op    */ { 1,  1,   0,  -3,  0,   0,    0,   -1 },
            /* Bin   */ { -2, -2,  0,   0, -2,   0,    0,   -2 },
            /* Rel   */ { -3, -3,  0,   0, -3,   0,    0,   -3 },
            /* Open  */ { 0,  0,   0,   0,  0,   0,    0,    0 },
            /* Close */ { 0,  1,  -2,  -3,  0,   0,   -1,   -1 },
            /* Punct */ { -1, -1,  0,  -1, -1,  -1,   -1,   -1 },
            /* Inner */ { -1, 1,  -2,  -3, -1,   0,   -1,   -1 },
        };
        var entry = table[(int)left, (int)right];
        if (entry < 0 && env.Level >= Level.Script)
            return 0;
        return Math.Abs(entry) switch { 1 => 3, 2 => 4, 3 => 5, _ => 0 };
    }

    // ------------------------------------------------------------------ runs

    /// <param name="markers">Whether "&amp;" marks an alignment point (in the rows of an equation array).</param>
    IEnumerable<Atom> Run(XElement run, Env env, bool markers = false)
    {
        // Text inserted or moved here in a tracked change (w:ins, w:moveTo inside the m:r) is printed with the run's,
        // deleted text (w:del, w:moveFrom) is not (docx/external/powertools/RP/RP014-Inserted-Math-Control-Char:
        // "A = 2πr²" with the 2 inserted).
        var inserted = run.Elements().Where(e => e.Name.LocalName is "ins" or "moveTo").ToList();
        var style = runStyle(run.Element(runProperties) ?? inserted.Select(i => i.Element(runProperties)).FirstOrDefault(p => p != null));
        var size = Sized(style.Size, env);
        var properties = run.Element(M + "rPr");
        var sty = (string?)properties?.Element(M + "sty")?.Attribute(M + "val");
        var scr = (string?)properties?.Element(M + "scr")?.Attribute(M + "val");
        var normal = On((string?)properties?.Element(M + "nor")?.Attribute(M + "val"), properties?.Element(M + "nor") != null);
        var literal = properties?.Element(M + "lit") != null;
        var text = string.Concat(run.Elements().SelectMany(e => e.Name == M + "t" ? [e] : inserted.Contains(e) ? e.Elements(M + "t") : []).Select(t => t.Value));
        var atoms = new List<Atom>();
        foreach (var rune in text.EnumerateRunes())
        {
            if (markers && rune.Value == '&')
            {
                atoms.Add(new Atom(new SpaceItem(), AtomClass.Ord) { Marker = true });
                continue;
            }
            // The hyphen-minus of math text is the minus sign (measured: Word draws Cambria Math's minus glyph); an
            // ellipsis after an operator stands on the axis (docx/external/libreoffice/extras-ooxmlexport/
            // EquationWithAboveAndBelowCaption, math-mso2k7: "+…" with U+22EF's glyph).
            var value = !normal && rune.Value == '-' ? 0x2212 : rune.Value;
            if (!normal && value == 0x2026 && atoms.Count > 0 && atoms[^1].Class is AtomClass.Bin or AtomClass.Rel)
                value = 0x22EF;
            var cp = normal ? value : Styled(value, sty, scr);
            // A styled form the math font lacks falls back to the character itself.
            if (cp != value && font.GlyphFor(cp) == 0)
                cp = value;
            // Normal text (m:nor) is set in the run's text font where it has the character (docx/external/
            // libreoffice/extras-ooxmlimport/tdf170171: "+" in Arial between Cambria Math's "0;" and "∞").
            var glyph = normal && style.Font is { } textFont && textFont != font && textFont.GlyphFor(value) is var plain and not 0
                ? TextGlyph(textFont, plain, value, size, style.Color, ScriptLevel(env))
                : Glyph(cp, size, style.Color, char.ConvertFromUtf32(value), ScriptLevel(env));
            var cls = literal || normal ? AtomClass.Ord : ClassOf(value);
            atoms.Add(new Atom(glyph, cls));
        }
        return atoms;
    }

    /// <summary>A glyph of a character, in its script-style form at script levels (1 and 2).</summary>
    GlyphItem Glyph(int codepoint, double size, RgbColor color, string? text = null, int level = 0)
    {
        var id = table.ScriptStyle(font.GlyphFor(codepoint), level);
        var item = GlyphOf(id, size, color, text ?? char.ConvertFromUtf32(codepoint));
        return new GlyphItem
        {
            Font = item.Font, Glyph = item.Glyph, Size = item.Size, Color = item.Color, Text = item.Text, Character = (codepoint, level),
            Width = item.Width, Ascent = item.Ascent, Descent = item.Descent, Italic = item.Italic,
        };
    }

    static int ScriptLevel(Env env) => env.Level switch { Level.Script => 1, Level.ScriptScript => 2, _ => 0 };

    /// <summary>A glyph of a text font, without italic correction.</summary>
    static GlyphItem TextGlyph(FontData textFont, uint id, int codepoint, double size, RgbColor color, int level)
    {
        var (_, bottom, _, top) = textFont.GlyphBox(id);
        var em = size / textFont.UnitsPerEm;
        return new GlyphItem
        {
            Font = textFont, Glyph = id, Size = size, Color = color, Text = char.ConvertFromUtf32(codepoint), Character = (codepoint, level),
            Width = textFont.GlyphAdvance(id) * em, Ascent = top * em, Descent = -bottom * em,
        };
    }

    GlyphItem GlyphOf(uint id, double size, RgbColor color, string? text = null, (int CodePoint, int Level)? character = null)
    {
        var (_, bottom, _, top) = font.GlyphBox(id);
        return new GlyphItem
        {
            Font = font, Glyph = id, Size = size, Color = color, Text = text, Character = character,
            Width = U(font.GlyphAdvance(id), size), Ascent = U(top, size), Descent = U(-bottom, size), Italic = U(table.ItalicsCorrection(id), size),
        };
    }

    /// <summary>
    /// A character in its math style: Latin and Greek letters italic by default (m:sty i), upright with p, bold with
    /// b, bold italic with bi; digits only change for bold. m:scr picks the script, fraktur, double-struck,
    /// sans-serif or monospace letters (and digits) of the Mathematical Alphanumeric Symbols, the letters Unicode
    /// keeps elsewhere included (measured: PowerPoint saves these runs as such characters). Characters already in
    /// that block stay.
    /// </summary>
    static int Styled(int cp, string? sty, string? scr = null)
    {
        sty ??= "i";
        var bold = sty is "b" or "bi";
        var italic = sty is "i" or "bi";
        var latin = cp is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
        var capital = cp is >= 'A' and <= 'Z';
        int Latin(int capitals, int smalls) => capital ? capitals + cp - 'A' : smalls + cp - 'a';
        switch (scr)
        {
            case "script" when latin:
                if (!bold && LetterlikeScript.TryGetValue(cp, out var script))
                    return script;
                return bold ? Latin(0x1D4D0, 0x1D4EA) : Latin(0x1D49C, 0x1D4B6);
            case "fraktur" when latin:
                if (!bold && LetterlikeFraktur.TryGetValue(cp, out var fraktur))
                    return fraktur;
                return bold ? Latin(0x1D56C, 0x1D586) : Latin(0x1D504, 0x1D51E);
            case "double-struck" when latin:
                return LetterlikeDoubleStruck.TryGetValue(cp, out var doubled) ? doubled : Latin(0x1D538, 0x1D552);
            case "double-struck" when cp is >= '0' and <= '9':
                return 0x1D7D8 + cp - '0';
            case "sans-serif" when latin:
                return (bold, italic) switch
                {
                    (true, true) => Latin(0x1D63C, 0x1D656),
                    (true, false) => Latin(0x1D5D4, 0x1D5EE),
                    (false, true) => Latin(0x1D608, 0x1D622),
                    _ => Latin(0x1D5A0, 0x1D5BA),
                };
            case "sans-serif" when cp is >= '0' and <= '9':
                return (bold ? 0x1D7EC : 0x1D7E2) + cp - '0';
            case "monospace" when latin:
                return Latin(0x1D670, 0x1D68A);
            case "monospace" when cp is >= '0' and <= '9':
                return 0x1D7F6 + cp - '0';
        }
        if (sty == "p")
            return cp;
        if (latin)
        {
            return (bold, italic) switch
            {
                (true, false) => Latin(0x1D400, 0x1D41A),
                (true, true) => Latin(0x1D468, 0x1D482),
                _ => cp == 'h' ? 0x210E : Latin(0x1D434, 0x1D44E),
            };
        }
        // Greek: α to ω and the capitals Α to Ω (the reserved U+03A2 falls on ϴ in the math alphabets), capitals
        // italic too (measured: PowerPoint saves a plain Γ as 𝛤).
        if (cp is >= 0x03B1 and <= 0x03C9)
            return (bold, italic) switch { (true, true) => 0x1D736, (true, false) => 0x1D6C2, _ => 0x1D6FC } + cp - 0x03B1;
        if (cp is >= 0x0391 and <= 0x03A9)
            return (bold, italic) switch { (true, true) => 0x1D71C, (true, false) => 0x1D6A8, _ => 0x1D6E2 } + cp - 0x0391;
        // The nabla after the capitals, the partial differential and the symbol variants ϵ ϑ ϰ ϕ ϱ ϖ after the small
        // letters (measured: Word draws ∇ and ∂ as 𝛻 and 𝜕).
        var after = cp switch { 0x2207 => -1, 0x2202 => 0, 0x03F5 => 1, 0x03D1 => 2, 0x03F0 => 3, 0x03D5 => 4, 0x03F1 => 5, 0x03D6 => 6, _ => (int?)null };
        if (after is { } k)
            return (bold, italic) switch { (true, true) => 0x1D74F, (true, false) => 0x1D6DB, _ => 0x1D715 } + (k < 0 ? -0x1A : k);
        if (cp is >= '0' and <= '9' && bold)
            return 0x1D7CE + cp - '0';
        return cp;
    }

    // Letters of the math alphabets that Unicode encodes in the Letterlike Symbols block instead.
    static readonly Dictionary<int, int> LetterlikeScript = new()
    {
        ['B'] = 0x212C, ['E'] = 0x2130, ['F'] = 0x2131, ['H'] = 0x210B, ['I'] = 0x2110, ['L'] = 0x2112, ['M'] = 0x2133, ['R'] = 0x211B,
        ['e'] = 0x212F, ['g'] = 0x210A, ['o'] = 0x2134,
    };

    static readonly Dictionary<int, int> LetterlikeFraktur = new()
    {
        ['C'] = 0x212D, ['H'] = 0x210C, ['I'] = 0x2111, ['R'] = 0x211C, ['Z'] = 0x2128,
    };

    static readonly Dictionary<int, int> LetterlikeDoubleStruck = new()
    {
        ['C'] = 0x2102, ['H'] = 0x210D, ['N'] = 0x2115, ['P'] = 0x2119, ['Q'] = 0x211A, ['R'] = 0x211D, ['Z'] = 0x2124,
    };

    static AtomClass ClassOf(int cp) => cp switch
    {
        '+' or '-' or 0x2212 or 0x00B1 or 0x2213 or 0x00D7 or 0x00F7 or 0x22C5 or 0x00B7 or 0x2217 or 0x2218 or 0x2219 or 0x2295 or 0x2296
            or 0x2297 or 0x2298 or 0x2299 or 0x2227 or 0x2228 or 0x2229 or 0x222A or 0x2216 or 0x228E or 0x2293 or 0x2294 or '*' => AtomClass.Bin,
        '=' or '<' or '>' or 0x2260 or 0x2264 or 0x2265 or 0x2248 or 0x2261 or 0x223C or 0x2243 or 0x2245 or 0x221D or 0x2208 or 0x2209
            or 0x220B or 0x2282 or 0x2283 or 0x2286 or 0x2287 or 0x2190 or 0x2192 or 0x2194 or 0x21D0 or 0x21D2 or 0x21D4 or 0x21A6 or 0x2225
            or 0x22A5 or 0x226A or 0x226B or 0x227A or 0x227B or 0x2254 or 0x2255 or 0x2259 or 0x225C or 0x225D or 0x227C or 0x227D
            or 0x2266 or 0x2267 or 0x2A7D or 0x2A7E or ':' or 0x2223 or 0x2224 or 0x22A2 or 0x22A8 => AtomClass.Rel,
        '(' or '[' or '{' or 0x27E8 or 0x2308 or 0x230A or 0x27E6 or 0x2329 => AtomClass.Open,
        ')' or ']' or '}' or 0x27E9 or 0x2309 or 0x230B or 0x27E7 or 0x232A or '!' => AtomClass.Close,
        ',' or ';' => AtomClass.Punct,
        _ => AtomClass.Ord,
    };

    // ------------------------------------------------------------------ fractions

    MathItem Fraction(XElement f, Env env, double baseSize)
    {
        var type = Property(f, "fPr", "type") ?? "bar";
        var color = runStyle(f.Element(M + "fPr")?.Element(M + "ctrlPr")?.Element(runProperties)).Color;
        var size = Sized(baseSize, env);
        if (type == "lin")
        {
            // Linear: numerator, slash, denominator on the line.
            var atoms = new List<Atom>
            {
                new(Sequence(Child(f, "num"), env, baseSize), AtomClass.Ord),
                new(Glyph('/', size, color), AtomClass.Ord),
                new(Sequence(Child(f, "den"), env, baseSize), AtomClass.Ord),
            };
            return Row(atoms, env, baseSize);
        }
        var num = Argument(f, "num", env.Num, baseSize);
        var den = Argument(f, "den", env.Den, baseSize);
        if (type == "skw")
            return Skewed(num, den, size, color);
        var display = env.Level == Level.Display;
        var axis = U(table.AxisHeight, size);
        var thickness = type == "noBar" ? 0 : U(table.FractionRuleThickness, size);
        var numShift = U(display ? table.FractionNumeratorDisplayStyleShiftUp : table.FractionNumeratorShiftUp, size);
        var denShift = U(display ? table.FractionDenominatorDisplayStyleShiftDown : table.FractionDenominatorShiftDown, size);
        double up, down;
        if (type == "noBar")
        {
            var gap = U(display ? table.StackDisplayStyleGapMin : table.StackGapMin, size);
            up = U(display ? table.StackTopDisplayStyleShiftUp : table.StackTopShiftUp, size);
            down = U(display ? table.StackBottomDisplayStyleShiftDown : table.StackBottomShiftDown, size);
            var missing = gap - ((up - num.Descent) - (den.Ascent - down));
            if (missing > 0)
            {
                up += missing / 2;
                down += missing / 2;
            }
        }
        else
        {
            var numGap = U(display ? table.FractionNumDisplayStyleGapMin : table.FractionNumeratorGapMin, size);
            var denGap = U(display ? table.FractionDenomDisplayStyleGapMin : table.FractionDenominatorGapMin, size);
            up = Math.Max(numShift, num.Descent + numGap + axis + thickness / 2);
            down = Math.Max(denShift, den.Ascent + denGap - axis + thickness / 2);
        }
        var width = Math.Max(num.Width, den.Width);
        var result = new ListItem();
        result.Add(num, (width - num.Width) / 2, -up);
        result.Add(den, (width - den.Width) / 2, down);
        if (thickness > 0)
            result.Add(new RuleItem { Color = color, Width = width, Ascent = axis + thickness / 2, Descent = -(axis - thickness / 2) }, 0, 0);
        result.Width = width;
        return result;
    }

    /// <summary>
    /// A skewed fraction (measured: Office's): the numerator's ink ending SkewedFractionVerticalGap / 2 above the
    /// math axis and the denominator's starting as far below it, SkewedFractionHorizontalGap between them, and
    /// between those a fraction slash (U+2044) in the largest size not taller than their joint ink, centered on it.
    /// </summary>
    MathItem Skewed(MathItem num, MathItem den, double size, RgbColor color)
    {
        var axis = U(table.AxisHeight, size);
        var gap = U(table.SkewedFractionVerticalGap, size);
        var across = U(table.SkewedFractionHorizontalGap, size);
        var numY = -(axis + gap / 2 + num.Descent);
        var denY = den.Ascent - axis + gap / 2;
        var top = numY - num.Ascent;
        var bottom = denY + den.Descent;
        var slash = Stretched(0x2044, bottom - top, size, color, vertically: true, fit: true);
        var (inkLeft, inkRight) = (0.0, slash.Width);
        if (slash is GlyphItem glyph)
        {
            var (left, _, right, _) = font.GlyphBox(glyph.Glyph);
            (inkLeft, inkRight) = (U(left, size), U(right, size));
        }
        var result = new ListItem();
        result.Add(num, 0, numY);
        result.Add(den, num.Width + across, denY);
        result.Add(slash, num.Width + across / 2 - (inkLeft + inkRight) / 2, (top + bottom) / 2 - (slash.Descent - slash.Ascent) / 2);
        result.Width = num.Width + across + den.Width;
        return result;
    }

    // ------------------------------------------------------------------ scripts

    Atom Scripts(XElement element, Env env, double baseSize)
    {
        // An empty base takes the scripts as a letter would (mph-24-sup: "2" 0.36 em up).
        var empty = element.Element(M + "e") is { } scriptBase && !HasContent(scriptBase);
        var baseAtoms = empty
            ? [new Atom(Placeholder(env, baseSize), AtomClass.Ord)]
            : Child(element, "e").SelectMany(e => Atoms(e, env, baseSize)).ToList();
        var nucleus = baseAtoms.Count == 1 ? baseAtoms[0].Item : Row(baseAtoms, env, baseSize);
        var cls = baseAtoms.Count == 1 ? baseAtoms[0].Class : AtomClass.Ord;
        var right = baseAtoms.Count == 1 ? baseAtoms[0].Right : AtomClass.Ord;
        var sup = element.Name.LocalName is "sSup" or "sSubSup" ? Argument(element, "sup", env.Sup, baseSize) : null;
        var sub = element.Name.LocalName is "sSub" or "sSubSup" ? Argument(element, "sub", env.Sub, baseSize) : null;
        // A scripted delimiter still opens on its left and closes on its right.
        return new Atom(Attach(nucleus, sup, sub, env, baseSize, nucleus is GlyphItem || empty), cls) { Right = right };
    }

    /// <summary>
    /// Scripts after a nucleus (OpenType MATH rules): the superscript at least SuperscriptShiftUp (cramped:
    /// …ShiftUpCramped) above the baseline, its bottom at least SuperscriptBottomMin, the subscript at least
    /// SubscriptShiftDown below, its top at most SubscriptTopMax; for a nucleus that is not a single glyph and
    /// reaches above the font's ascent (below its descent) also relative to its top (bottom; measured: Office
    /// scripts "(a+b)" with ordinary parentheses and an accented letter like a letter, a fraction and tall
    /// parentheses by their height); the two at least SubSuperscriptGapMin apart; the superscript after the
    /// nucleus's italic correction, SpaceAfterScript after both.
    /// </summary>
    MathItem Attach(MathItem nucleus, MathItem? sup, MathItem? sub, Env env, double baseSize, bool simple)
    {
        var size = Sized(baseSize, env);
        double supShift = 0, subShift = 0;
        if (sup != null)
        {
            supShift = U(env.Cramped ? table.SuperscriptShiftUpCramped : table.SuperscriptShiftUp, size);
            supShift = Math.Max(supShift, sup.Descent + U(table.SuperscriptBottomMin, size));
            if (!simple && nucleus.Ascent > font.Ascent * size)
                supShift = Math.Max(supShift, nucleus.Ascent - U(table.SuperscriptBaselineDropMax, size));
        }
        if (sub != null)
        {
            subShift = U(table.SubscriptShiftDown, size);
            subShift = Math.Max(subShift, sub.Ascent - U(table.SubscriptTopMax, size));
            if (!simple && nucleus.Descent > font.Descent * size)
                subShift = Math.Max(subShift, nucleus.Descent + U(table.SubscriptBaselineDropMin, size));
        }
        if (sup != null && sub != null)
        {
            var gap = (supShift - sup.Descent) - (sub.Ascent - subShift);
            var minimum = U(table.SubSuperscriptGapMin, size);
            if (gap < minimum)
            {
                subShift += minimum - gap;
                var maxBottom = U(table.SuperscriptBottomMaxWithSubscript, size);
                var lift = maxBottom - (supShift - sup.Descent);
                if (lift > 0)
                {
                    supShift += lift;
                    subShift -= lift;
                }
            }
        }
        var result = new ListItem();
        result.Add(nucleus, 0, 0);
        var after = U(table.SpaceAfterScript, size);
        var end = nucleus.Width;
        if (sup != null)
        {
            // A letter's top right math kern at the superscript's bottom moves it further (measured with
            // docx/local/mth-d-sup, msk-*: x^2, r^2 and A^2 54, 53 and -44 units right of the italic correction in
            // Cambria Math 11, its kerns 50, 55 and -40 at that height; e, f and k 13 to 22 units more than theirs).
            var kern = nucleus is GlyphItem glyph && glyph.Font == font
                ? U(table.Kern(glyph.Glyph, 0, (int)Math.Round((supShift - sup.Descent) * font.UnitsPerEm / glyph.Size)), glyph.Size) : 0;
            result.Add(sup, nucleus.Width + nucleus.Italic + kern, -supShift);
            end = Math.Max(end, nucleus.Width + nucleus.Italic + kern + sup.Width);
        }
        if (sub != null)
        {
            result.Add(sub, nucleus.Width, subShift);
            end = Math.Max(end, nucleus.Width + sub.Width);
        }
        result.Width = end + after;
        return result;
    }

    MathItem PreScripts(XElement element, Env env, double baseSize)
    {
        var size = Sized(baseSize, env);
        var nucleus = Sequence(Child(element, "e"), env, baseSize);
        var sup = Argument(element, "sup", env.Sup, baseSize);
        var sub = Argument(element, "sub", env.Sub, baseSize);
        var attached = (ListItem)Attach(new SpaceItem { Ascent = nucleus.Ascent, Descent = nucleus.Descent }, sup, sub, env, baseSize, false);
        var width = Math.Max(sup.Width, sub.Width);
        var result = new ListItem();
        foreach (var (item, _, y) in attached.Children.Skip(1))
            result.Add(item, width - item.Width, y);
        result.Add(nucleus, width + U(table.SpaceAfterScript, size) / 2, 0);
        return result;
    }

    // ------------------------------------------------------------------ radicals

    MathItem Radical(XElement rad, Env env, double baseSize)
    {
        var size = Sized(baseSize, env);
        var color = runStyle(rad.Element(M + "radPr")?.Element(M + "ctrlPr")?.Element(runProperties)).Color;
        var radicand = Base(rad, env.Crampd, baseSize);
        var display = env.Level == Level.Display;
        var gap = U(display ? table.RadicalDisplayStyleVerticalGap : table.RadicalVerticalGap, size);
        var thickness = U(table.RadicalRuleThickness, size);
        var extra = U(table.RadicalExtraAscender, size);
        var target = radicand.Ascent + radicand.Descent + gap + thickness;
        var surd = Stretched(0x221A, target, size, color, vertically: true);
        var height = surd.Ascent + surd.Descent;
        // Room left over goes half above the radicand.
        if (height > target)
            gap += (height - target) / 2;
        var top = radicand.Ascent + gap + thickness;
        var result = new ListItem();
        var x = 0.0;
        if (Property(rad, "radPr", "degHide") is not ("on" or "1" or "true") && rad.Element(M + "deg")?.Elements().Any() == true)
        {
            var degree = Sequence(Child(rad, "deg"), env with { Level = Level.ScriptScript }, baseSize);
            var before = U(table.RadicalKernBeforeDegree, size);
            var raise = height * table.RadicalDegreeBottomRaisePercent / 100.0;
            result.Add(degree, before, -(raise - (height - top)) - degree.Descent);
            x = Math.Max(0, before + degree.Width + U(table.RadicalKernAfterDegree, size));
        }
        result.Add(surd, x, -(top - surd.Ascent));
        result.Add(radicand, x + surd.Width, 0);
        result.Add(new RuleItem { Color = color, Width = radicand.Width, Ascent = top, Descent = -(top - thickness) }, x + surd.Width, 0);
        result.Ascent = Math.Max(result.Ascent, top + extra);
        return result;
    }

    // ------------------------------------------------------------------ stretchy glyphs

    /// <summary>
    /// A glyph at least <paramref name="target"/> points tall (or wide): its smallest size variant that is, else
    /// built from its assembly's parts, the extenders repeated and the overlaps spread to reach the size exactly.
    /// With <paramref name="fit"/> the largest variant that is not bigger than the target instead (Office's
    /// delimiters, slashes and accents; measured), the glyph itself when even that is, the assembly only once the
    /// target outgrows all variants.
    /// </summary>
    MathItem Stretched(int codepoint, double target, double size, RgbColor color, bool vertically, bool fit = false)
    {
        var glyph = font.GlyphFor(codepoint);
        var text = char.ConvertFromUtf32(codepoint);
        // The delimiter at its own size stands for its character, which Word's PDF holds as text (LibreOffice's
        // extras-ooxmlexport/math-d: "|" one black column in Word, two gray ones as an outline).
        var basic = GlyphOf(glyph, size, color, text, (codepoint, 0));
        double Extent(MathItem item) => vertically ? item.Ascent + item.Descent : item.Width;
        if (Extent(basic) >= target)
            return basic;
        var variants = table.Variants(glyph, vertically);
        if (fit)
        {
            var parts = table.Assembly(glyph, vertically);
            if (variants.Count == 0 || U(variants[^1].Size, size) < target && parts.Count > 0)
                return parts.Count > 0 ? Assembled(parts, target, size, color, text, vertically) : basic;
            var best = basic;
            foreach (var (variant, advance) in variants)
            {
                if (U(advance, size) > target)
                    break;
                best = GlyphOf(variant, size, color, text);
            }
            return best;
        }
        foreach (var (variant, advance) in variants)
        {
            if (U(advance, size) >= target)
                return GlyphOf(variant, size, color, text);
        }
        var assembly = table.Assembly(glyph, vertically);
        if (assembly.Count == 0)
            return variants.Count > 0 ? GlyphOf(variants[^1].Glyph, size, color, text) : basic;
        return Assembled(assembly, target, size, color, text, vertically);
    }

    /// <summary>A glyph built from its assembly's parts to <paramref name="target"/> points: extenders repeated, overlaps spread.</summary>
    ListItem Assembled(IReadOnlyList<GlyphPart> parts, double target, double size, RgbColor color, string text, bool vertically)
    {
        var minOverlap = table.MinConnectorOverlap;
        // Repeat the extenders until the parts reach the target with the smallest overlap.
        var repeats = 0;
        List<GlyphPart> chosen;
        double reach;
        do
        {
            chosen = parts.SelectMany(p => p.Extender ? Enumerable.Repeat(p, repeats) : [p]).ToList();
            reach = U(chosen.Sum(p => p.FullAdvance) - minOverlap * Math.Max(0, chosen.Count - 1), size);
            repeats++;
        } while (reach < target && repeats < 100);
        // Spread the extra overlap over the joints, as far as the connectors allow.
        var joints = Math.Max(1, chosen.Count - 1);
        var overlap = minOverlap + (reach - target) / joints * font.UnitsPerEm / size;
        var list = new ListItem();
        var position = 0.0;
        var (ascent, descent) = (double.MinValue, double.MinValue);
        for (var i = 0; i < chosen.Count; i++)
        {
            var part = GlyphOf(chosen[i].Glyph, size, color, i == 0 ? text : null);
            if (i > 0)
            {
                var allowed = Math.Min(overlap, Math.Min(chosen[i - 1].EndConnector, chosen[i].StartConnector));
                position -= U((int)Math.Round(Math.Max(minOverlap, allowed)), size);
            }
            // Vertical parts go upwards from the bottom one, each with its bottom where the previous one's advance
            // (less the overlap) ends; the assembly stands on the baseline, callers move it.
            if (vertically)
                list.Add(part, 0, -position - part.Descent);
            else
                list.Add(part, position, 0);
            (ascent, descent) = (Math.Max(ascent, part.Ascent), Math.Max(descent, part.Descent));
            position += U(chosen[i].FullAdvance, size);
        }
        // A horizontal assembly reaches as high and low as its parts' ink (an underbrace lies wholly below the
        // baseline), not down to the baseline as lists do.
        if (!vertically && chosen.Count > 0)
            (list.Ascent, list.Descent) = (ascent, descent);
        return list;
    }

    /// <summary>A stretched glyph centered on the math axis.</summary>
    MathItem OnAxis(MathItem glyph, double size)
    {
        var axis = U(table.AxisHeight, size);
        var shift = (glyph.Ascent - glyph.Descent) / 2 - axis;
        var list = new ListItem();
        list.Add(glyph, 0, shift);
        list.Italic = glyph.Italic;
        return list;
    }

    // ------------------------------------------------------------------ n-ary operators

    /// <summary>
    /// An n-ary operator with its limits and its operand, as one atom; in inline equations the limits always go
    /// beside the operator (measured: Office's inline ∑).
    /// </summary>
    Atom Nary(XElement nary, Env env, double baseSize)
    {
        var size = Sized(baseSize, env);
        var color = runStyle(nary.Element(M + "naryPr")?.Element(M + "ctrlPr")?.Element(runProperties)).Color;
        var chr = Property(nary, "naryPr", "chr") is { Length: > 0 } c ? char.ConvertToUtf32(c, 0) : 0x222B;
        var integral = chr is >= 0x222B and <= 0x2233 or >= 0x2A0B and <= 0x2A1C;
        var limLoc = env.Inline ? "subSup" : Property(nary, "naryPr", "limLoc") ?? (integral ? IntegralLimits : NaryLimits);
        var display = env.Level == Level.Display;
        var glyph = font.GlyphFor(chr);
        var op = GlyphOf(glyph, size, color, char.ConvertFromUtf32(chr));
        if (display)
        {
            // The display size: the first variant at least 1.5 × DisplayOperatorMinHeight tall (measured: Office's
            // display ∑ and ∫ in Cambria Math are their third sizes, 1.91 and 2.15 em).
            var minimum = U(table.DisplayOperatorMinHeight, size) * 1.5;
            foreach (var (variant, advance) in table.Variants(glyph, true))
            {
                op = GlyphOf(variant, size, color, char.ConvertFromUtf32(chr));
                if (U(advance, size) >= minimum)
                    break;
            }
        }
        var centered = OnAxis(op, size);
        var italic = op.Italic;
        var hideSub = On(Property(nary, "naryPr", "subHide"), false);
        var hideSup = On(Property(nary, "naryPr", "supHide"), false);
        var sub = hideSub ? null : Argument(nary, "sub", env.Sub, baseSize);
        var sup = hideSup ? null : Argument(nary, "sup", env.Sup, baseSize);
        // The operand follows a thin space after the operator and its limits, less what the display size adds to
        // the operator's italic correction, and at least SpaceAfterScript after limits beside it (measured: display
        // integrals take their operand right at their advance, inline ones and sums a thin space after).
        var gap = 3 * size / 18 - (italic - U(table.ItalicsCorrection(glyph), size));
        var result = new ListItem();
        double operandX;
        if (limLoc == "undOvr")
        {
            var block = UnderOver(centered, sup, sub, size, italic);
            result.Add(block, 0, 0);
            operandX = block.Width + gap;
            // Hidden limits keep the baselines they would stand on (measured: Office's line over a ∑ without
            // limits is as tall as UpperLimitBaselineRiseMin above it).
            if (hideSup)
                result.Ascent = Math.Max(result.Ascent, centered.Ascent + U(table.UpperLimitBaselineRiseMin, size));
            if (hideSub)
                result.Descent = Math.Max(result.Descent, centered.Descent + U(table.LowerLimitBaselineDropMin, size));
        }
        else
        {
            result.Add(centered, 0, 0);
            // Limits beside the operator go by the script rules, relative to its top and bottom only where it
            // reaches beyond the font's ascent and descent (an inline ∫ does, an inline ∑ does not); limits too
            // close move apart by half the shortfall each (measured: Office's inline ∑ with both limits).
            var shiftUp = 0.0;
            var shiftDown = 0.0;
            if (sup != null)
            {
                shiftUp = Math.Max(U(table.SuperscriptShiftUp, size), sup.Descent + U(table.SuperscriptBottomMin, size));
                if (centered.Ascent > font.Ascent * size)
                    shiftUp = Math.Max(shiftUp, centered.Ascent - U(table.SuperscriptBaselineDropMax, size));
            }
            if (sub != null)
            {
                shiftDown = Math.Max(U(table.SubscriptShiftDown, size), sub.Ascent - U(table.SubscriptTopMax, size));
                if (centered.Descent > font.Descent * size)
                    shiftDown = Math.Max(shiftDown, centered.Descent + U(table.SubscriptBaselineDropMin, size));
            }
            if (sup != null && sub != null)
            {
                var shortfall = U(table.SubSuperscriptGapMin, size) - ((shiftUp - sup.Descent) - (sub.Ascent - shiftDown));
                if (shortfall > 0)
                {
                    shiftUp += shortfall / 2;
                    shiftDown += shortfall / 2;
                }
            }
            // The upper limit after the operator's advance, the lower one its italic correction further left
            // (measured: Office's display integral).
            var scriptsEnd = 0.0;
            if (sup != null)
            {
                result.Add(sup, centered.Width, -shiftUp);
                scriptsEnd = centered.Width + sup.Width;
            }
            if (sub != null)
            {
                result.Add(sub, centered.Width - italic, shiftDown);
                scriptsEnd = Math.Max(scriptsEnd, centered.Width - italic + sub.Width);
            }
            operandX = sup != null || sub != null
                ? Math.Max(centered.Width, scriptsEnd) + Math.Max(gap, U(table.SpaceAfterScript, size))
                : centered.Width + gap;
        }
        var operand = Base(nary, env, baseSize, closes: false);
        if (nary.Element(M + "e") != null)
            result.Add(operand, operandX, 0);
        result.Italic = operand.Italic;
        return new Atom(result, AtomClass.Op) { Right = AtomClass.Ord };
    }

    /// <summary>Limits above and below a centered nucleus (UpperLimit…/LowerLimit… gaps and baseline shifts).</summary>
    MathItem UnderOver(MathItem nucleus, MathItem? over, MathItem? under, double size, double italic)
    {
        var width = Math.Max(nucleus.Width, Math.Max(over?.Width ?? 0, under?.Width ?? 0));
        var result = new ListItem();
        result.Add(nucleus, (width - nucleus.Width) / 2, 0);
        if (over != null)
        {
            // The limit's baseline at least UpperLimitBaselineRiseMin above the nucleus's top, its bottom at least
            // UpperLimitGapMin above it (measured: Office's ∑ limits).
            var rise = nucleus.Ascent + Math.Max(U(table.UpperLimitBaselineRiseMin, size), U(table.UpperLimitGapMin, size) + over.Descent);
            result.Add(over, (width - over.Width) / 2 + italic / 2, -rise);
        }
        if (under != null)
        {
            var drop = nucleus.Descent + Math.Max(U(table.LowerLimitBaselineDropMin, size), U(table.LowerLimitGapMin, size) + under.Ascent);
            result.Add(under, (width - under.Width) / 2 - italic / 2, drop);
        }
        result.Width = width;
        return result;
    }

    // ------------------------------------------------------------------ delimiters

    (MathItem Item, bool Tall) Delimited(XElement d, Env env, double baseSize)
    {
        var size = Sized(baseSize, env);
        var color = runStyle(d.Element(M + "dPr")?.Element(M + "ctrlPr")?.Element(runProperties)).Color;
        var begin = DelimiterChar(d, "begChr", "(");
        var end = DelimiterChar(d, "endChr", ")");
        var separator = DelimiterChar(d, "sepChr", "|");
        var grow = On(Property(d, "dPr", "grow"), true);
        // The parts of a delimiter with separators are set in text style (measured: Office's "(a|½)" has a text
        // fraction where "(½)" has a display one). Each part spaces against the separators by the classes of its
        // first and last atoms (measured: thin spaces between "|" and a fraction).
        var partEnv = d.Elements(M + "e").Count() > 1 && env.Level == Level.Display ? env with { Level = Level.Text } : env;
        var parts = new List<Atom>();
        foreach (var e in d.Elements(M + "e"))
        {
            var partAtoms = HasContent(e) ? e.Elements().SelectMany(element => Atoms(element, partEnv, baseSize)).ToList()
                : [new Atom(Placeholder(partEnv, baseSize), AtomClass.Ord)];
            var row = Row(partAtoms, partEnv, baseSize);
            parts.Add(partAtoms.Count > 0 ? new Atom(row, partAtoms[0].Class) { Right = partAtoms[^1].Right } : new Atom(row, AtomClass.Ord));
        }
        var axis = U(table.AxisHeight, size);
        var ascent = parts.Count > 0 ? parts.Max(p => p.Item.Ascent) : 0;
        var descent = parts.Count > 0 ? parts.Max(p => p.Item.Descent) : 0;
        // The smallest size at least twice the content's reach from the axis less a shortfall of 630 units (0.31 em),
        // like TeX's \delimitershortfall (measured: the sizes PowerPoint picks around display and text fractions, sums
        // and matrices at 18 and 40 pt bound the shortfall to 613 to 645 units); Word takes the content's height
        // instead of twice its reach in displayed equations (measured with docx/local/ppt-math, the same equations in
        // Word), below display level twice the reach as well (docx/local/mti-i-*, mth-i-binom: an inline fraction in
        // parentheses grown to their third size, an inline binomial to their second, within 0.25 pt).
        var target = (DelimitersByHeight && env.Level == Level.Display ? ascent + descent : 2 * Math.Max(ascent - axis, descent + axis)) - U(630, size);
        MathItem Fence(string? chr)
        {
            if (string.IsNullOrEmpty(chr))
                return new SpaceItem();
            var cp = char.ConvertToUtf32(chr, 0);
            return grow ? OnAxis(Stretched(cp, target, size, color, vertically: true), size) : Glyph(cp, size, color);
        }
        // Separators space like ordinary atoms: none next to letters (measured: Office's "(a|b)").
        var atoms = new List<Atom> { new(Fence(begin), AtomClass.Open) };
        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0)
                atoms.Add(new Atom(Fence(separator), AtomClass.Ord));
            atoms.Add(parts[i]);
        }
        var closing = Fence(end);
        atoms.Add(new Atom(closing, AtomClass.Close));
        var own = string.IsNullOrEmpty(end) ? null : Glyph(char.ConvertToUtf32(end, 0), size, color);
        var tall = own != null && own.Ascent + own.Descent > 0 && closing.Ascent + closing.Descent > 1.5 * (own.Ascent + own.Descent);
        return (Row(atoms, env, baseSize), tall);
    }

    static string? DelimiterChar(XElement d, string name, string fallback)
    {
        var element = d.Element(M + "dPr")?.Element(M + name);
        if (element == null)
            return fallback;
        var value = (string?)element.Attribute(M + "val");
        return value ?? "";
    }

    // ------------------------------------------------------------------ matrices and arrays

    MathItem Matrix(XElement m, Env env, double baseSize)
    {
        var size = Sized(baseSize, env);
        var rows = m.Elements(M + "mr").Select(r => r.Elements(M + "e").Select(e => Sequence(e.Elements(), env, baseSize)).ToList()).ToList();
        var columns = rows.Count > 0 ? rows.Max(r => r.Count) : 0;
        var widths = Enumerable.Range(0, columns).Select(c => rows.Max(r => c < r.Count ? r[c].Width : 0)).ToArray();
        // Office's default column gap is one em (measured); m:cGapRule 1 and 2 make it one and a half and two, 3
        // sets it in twips (m:cGp).
        var columnGap = Property(m, "mPr", "cGapRule") switch
        {
            "1" => 1.5 * size,
            "2" => 2 * size,
            "3" => (double.TryParse(Property(m, "mPr", "cGp"), out var twips) ? twips : 0) / 20,
            _ => size,
        };
        var spacing = LineSpacing(m.Element(M + "mPr"));
        var justify = m.Element(M + "mPr")?.Element(M + "mcs")?.Elements(M + "mc").Select(mc => Property(mc, "mcPr", "mcJc") ?? "center").ToList() ?? [];
        var grid = new ListItem();
        var y = 0.0;
        var previous = 0.0;
        for (var r = 0; r < rows.Count; r++)
        {
            var ascent = rows[r].Count > 0 ? rows[r].Max(c => c.Ascent) : size * 0.7;
            var descent = rows[r].Count > 0 ? rows[r].Max(c => c.Descent) : size * 0.2;
            y += r > 0 ? RowPitch(previous, ascent, size) * spacing : ascent;
            previous = descent;
            var x = 0.0;
            for (var c = 0; c < columns; c++)
            {
                if (c < rows[r].Count)
                {
                    var cell = rows[r][c];
                    var jc = c < justify.Count ? justify[c] : "center";
                    var dx = jc switch { "left" => 0, "right" => widths[c] - cell.Width, _ => (widths[c] - cell.Width) / 2 };
                    grid.Add(cell, x + dx, y);
                }
                x += widths[c] + (c + 1 < columns ? columnGap : 0);
            }
        }
        return Centered(grid, size);
    }

    /// <summary>
    /// An equation array: rows centered, or, when their text holds "&amp;" alignment points, cut there into columns
    /// that line up, right-aligned before an odd point and left-aligned after it (measured: Office's "x&amp;=1" over
    /// "x+y&amp;=12" lines up the equals signs; a second pair of points starts a further pair of columns without
    /// extra space). Spacing runs across the points as if they were not there.
    /// </summary>
    MathItem EquationArray(XElement eqArr, Env env, double baseSize)
    {
        var size = Sized(baseSize, env);
        var rows = new List<ListItem>();
        var cuts = new List<List<double>>();
        foreach (var e in eqArr.Elements(M + "e"))
        {
            var atoms = new List<Atom>();
            var markers = new List<int>();
            foreach (var element in e.Elements())
            {
                var elementAtoms = element.Name == M + "r" ? Run(element, env, markers: true) : Atoms(element, env, baseSize);
                foreach (var atom in elementAtoms)
                {
                    if (atom.Marker)
                        markers.Add(atoms.Count);
                    else
                        atoms.Add(atom);
                }
            }
            var row = (ListItem)Row(atoms, env, baseSize);
            rows.Add(row);
            cuts.Add(markers.Select(m => m < row.Children.Count ? row.Children[m].X : row.Width).ToList());
        }
        var spacing = LineSpacing(eqArr.Element(M + "eqArrPr"));
        var aligned = cuts.Any(c => c.Count > 0);
        // The columns' widths: each row's pieces between its alignment points.
        var columns = new List<double>();
        List<double> Pieces(int r) => [.. Enumerable.Range(0, cuts[r].Count + 1).Select(j => (j < cuts[r].Count ? cuts[r][j] : rows[r].Width) - (j > 0 ? cuts[r][j - 1] : 0))];
        for (var r = 0; aligned && r < rows.Count; r++)
        {
            var pieces = Pieces(r);
            for (var j = 0; j < pieces.Count; j++)
            {
                if (j == columns.Count)
                    columns.Add(0);
                columns[j] = Math.Max(columns[j], pieces[j]);
            }
        }
        var width = aligned ? columns.Sum() : rows.Count > 0 ? rows.Max(r => r.Width) : 0;
        var grid = new ListItem();
        var y = 0.0;
        for (var r = 0; r < rows.Count; r++)
        {
            y += r > 0 ? RowPitch(rows[r - 1].Descent, rows[r].Ascent, size) * spacing : rows[r].Ascent;
            if (!aligned)
            {
                grid.Add(rows[r], (width - rows[r].Width) / 2, y);
                continue;
            }
            // Each piece moves to its column: the children between two points shift together.
            var pieces = Pieces(r);
            var shifts = new double[pieces.Count];
            for (var j = 0; j < pieces.Count; j++)
            {
                var start = columns.Take(j).Sum() + (j % 2 == 0 ? columns[j] - pieces[j] : 0);
                shifts[j] = start - (j > 0 ? cuts[r][j - 1] : 0);
            }
            var placed = new ListItem();
            foreach (var (item, x, dy) in rows[r].Children)
            {
                var piece = cuts[r].Count(c => c <= x + 1e-9);
                placed.Add(item, x + shifts[piece], dy);
            }
            grid.Add(placed, 0, y);
        }
        grid.Width = width;
        return Centered(grid, size);
    }

    /// <summary>
    /// The baseline distance of stacked rows at single spacing (measured: Office's matrices and equation arrays): a
    /// line of the math font, or more when the rows' ink would come closer than the font's line gap and the math
    /// leading.
    /// </summary>
    double RowPitch(double previousDescent, double nextAscent, double size) =>
        Math.Max((font.Ascent + font.Descent + font.LineGap) * size, previousDescent + nextAscent + font.LineGap * size + U(table.MathLeading, size));

    /// <summary>The row spacing of a matrix or equation array (m:rSpRule): single, one and a half or double.</summary>
    static double LineSpacing(XElement? properties) =>
        (string?)properties?.Element(M + "rSpRule")?.Attribute(M + "val") switch { "1" => 1.5, "2" => 2, _ => 1 };

    /// <summary>A block whose rows start at y = 0 from its top, moved so that its middle is on the math axis.</summary>
    MathItem Centered(ListItem block, double size)
    {
        var height = block.Ascent + block.Descent;
        var axis = U(table.AxisHeight, size);
        var result = new ListItem();
        // block.Ascent is the first row's ascent above y = 0 … top is −block.Ascent.
        var top = -block.Ascent;
        result.Add(block, 0, -(top + height / 2) - axis);
        return result;
    }

    // ------------------------------------------------------------------ accents, bars, group characters, limits

    MathItem Accent(XElement acc, Env env, double baseSize)
    {
        var size = Sized(baseSize, env);
        var color = runStyle(acc.Element(M + "accPr")?.Element(M + "ctrlPr")?.Element(runProperties)).Color;
        var chr = Property(acc, "accPr", "chr") is { Length: > 0 } c ? char.ConvertToUtf32(c, 0) : 0x0302;
        var nucleus = Base(acc, env.Crampd, baseSize);
        // Over a single character the accent keeps its size, centered on the character's top accent attachment
        // point (italic letters lean right); over more it grows to the largest size not wider than them, centered
        // (measured: Office's hat over a letter, arrow over two).
        var glyph = nucleus as GlyphItem ?? (nucleus is ListItem { Children: [(GlyphItem only, _, _)] } ? only : null);
        var accent = glyph != null ? Glyph(chr, size, color) : Stretched(chr, nucleus.Width, size, color, vertically: false, fit: true);
        // Combining marks sit over x-height letters as designed; taller bases raise them.
        var lift = Math.Max(0, nucleus.Ascent - U(table.AccentBaseHeight, size));
        var accentCenter = AccentCenter(accent, size);
        var baseCenter = glyph != null && table.TopAccentAttachment(glyph.Glyph) is { } attach ? U(attach, glyph.Size) : nucleus.Width / 2;
        var result = new ListItem();
        result.Add(nucleus, 0, 0);
        result.Add(accent, baseCenter - accentCenter, -lift);
        result.Width = nucleus.Width;
        result.Italic = nucleus.Italic;
        return result;
    }

    /// <summary>Where an accent glyph centers: its attachment point, else the middle of its ink.</summary>
    double AccentCenter(MathItem accent, double size)
    {
        if (accent is GlyphItem glyph)
        {
            if (table.TopAccentAttachment(glyph.Glyph) is { } attach)
                return U(attach, size);
            var (left, _, right, _) = font.GlyphBox(glyph.Glyph);
            return U((left + right) / 2, size);
        }
        return accent.Width / 2;
    }

    MathItem Bar(XElement bar, Env env, double baseSize)
    {
        var size = Sized(baseSize, env);
        var color = runStyle(bar.Element(M + "barPr")?.Element(M + "ctrlPr")?.Element(runProperties)).Color;
        var top = Property(bar, "barPr", "pos") == "top";
        var nucleus = Sequence(Child(bar, "e"), top ? env.Crampd : env, baseSize);
        var result = new ListItem();
        result.Add(nucleus, 0, 0);
        if (top)
        {
            var thickness = U(table.OverbarRuleThickness, size);
            var bottom = nucleus.Ascent + U(table.OverbarVerticalGap, size);
            result.Add(new RuleItem { Color = color, Width = nucleus.Width, Ascent = bottom + thickness, Descent = -bottom }, 0, 0);
            result.Ascent = Math.Max(result.Ascent, bottom + thickness + U(table.OverbarExtraAscender, size));
        }
        else
        {
            var thickness = U(table.UnderbarRuleThickness, size);
            var topEdge = nucleus.Descent + U(table.UnderbarVerticalGap, size);
            result.Add(new RuleItem { Color = color, Width = nucleus.Width, Ascent = -topEdge, Descent = topEdge + thickness }, 0, 0);
            result.Descent = Math.Max(result.Descent, topEdge + thickness + U(table.UnderbarExtraDescender, size));
        }
        result.Width = nucleus.Width;
        return result;
    }

    MathItem GroupCharacter(XElement group, Env env, double baseSize)
    {
        var size = Sized(baseSize, env);
        var color = runStyle(group.Element(M + "groupChrPr")?.Element(M + "ctrlPr")?.Element(runProperties)).Color;
        var chr = Property(group, "groupChrPr", "chr") is { Length: > 0 } c ? char.ConvertToUtf32(c, 0) : 0x23DF;
        var above = Property(group, "groupChrPr", "pos") == "top";
        // m:vertJc says which of the two stands on the baseline: the upper one (top, what Office reads when it is
        // missing) or the lower one. When that is the character, the base goes beside it like a limit, in script
        // size (measured: Office's ⏞ with vertJc top).
        var upperOnBaseline = Property(group, "groupChrPr", "vertJc") != "bot";
        if (above == upperOnBaseline)
        {
            var limit = Sequence(Child(group, "e"), above ? env.Sub : env.Sup, baseSize);
            var character = new ListItem();
            character.Add(Stretched(chr, limit.Width, size, color, vertically: false, fit: true), 0, 0);
            return above ? UnderOver(character, null, limit, size, 0) : UnderOver(character, limit, null, size, 0);
        }
        var nucleus = Sequence(Child(group, "e"), env, baseSize);
        var brace = Stretched(chr, nucleus.Width, size, color, vertically: false, fit: true);
        var width = Math.Max(nucleus.Width, brace.Width);
        var result = new ListItem();
        result.Add(nucleus, (width - nucleus.Width) / 2, 0);
        if (above)
            result.Add(brace, (width - brace.Width) / 2, -(nucleus.Ascent + U(table.StretchStackGapBelowMin, size) + brace.Descent));
        else
            result.Add(brace, (width - brace.Width) / 2, nucleus.Descent + U(table.StretchStackGapAboveMin, size) + brace.Ascent);
        result.Width = width;
        return result;
    }

    MathItem Limit(XElement limit, Env env, double baseSize)
    {
        var size = Sized(baseSize, env);
        var nucleus = Sequence(Child(limit, "e"), env, baseSize);
        var lim = Argument(limit, "lim", limit.Name.LocalName == "limLow" ? env.Sub : env.Sup, baseSize);
        return limit.Name.LocalName == "limLow" ? UnderOver(nucleus, null, lim, size, 0) : UnderOver(nucleus, lim, null, size, 0);
    }

    IEnumerable<Atom> Function(XElement func, Env env, double size)
    {
        var name = Sequence(Child(func, "fName"), env, size);
        // A thin space between the name and its argument, also an opening delimiter (measured: Word's "lim (1 + 1/n)"
        // and "cos nπx/L" keep 3/18 em between them).
        var atoms = new List<Atom> { new(name, AtomClass.Op) { FunctionName = true } };
        if (func.Element(M + "e") is { } argument && !HasContent(argument))
            atoms.Add(new Atom(Placeholder(env, size), AtomClass.Ord));
        else
            foreach (var element in Child(func, "e"))
                atoms.AddRange(Atoms(element, env, size));
        return atoms;
    }

    MathItem BorderBox(XElement box, Env env, double baseSize)
    {
        var size = Sized(baseSize, env);
        var color = runStyle(box.Element(M + "borderBoxPr")?.Element(M + "ctrlPr")?.Element(runProperties)).Color;
        var nucleus = Sequence(Child(box, "e"), env, baseSize);
        // Lines of the rule thickness three times their thickness away from the content's ink (measured: Office's
        // boxes around a letter, a fraction and an equation).
        var thickness = U(table.OverbarRuleThickness, size);
        var gap = 3 * thickness;
        var (w, a, d) = (nucleus.Width + 2 * gap, nucleus.Ascent + gap, nucleus.Descent + gap);
        bool Hidden(string side) => On(Property(box, "borderBoxPr", side), false);
        var result = new ListItem();
        result.Add(nucleus, thickness + gap, 0);
        var outer = w + 2 * thickness;
        if (!Hidden("hideTop")) result.Add(new RuleItem { Color = color, Width = outer, Ascent = a + thickness, Descent = -a }, 0, 0);
        if (!Hidden("hideBot")) result.Add(new RuleItem { Color = color, Width = outer, Ascent = -d, Descent = d + thickness }, 0, 0);
        if (!Hidden("hideLeft")) result.Add(new RuleItem { Color = color, Width = thickness, Ascent = a + thickness, Descent = d + thickness }, 0, 0);
        if (!Hidden("hideRight")) result.Add(new RuleItem { Color = color, Width = thickness, Ascent = a + thickness, Descent = d + thickness }, thickness + w, 0);
        // Strike-throughs: horizontal on the axis, vertical through the middle.
        if (On(Property(box, "borderBoxPr", "strikeH"), false))
        {
            var axis = U(table.AxisHeight, size);
            result.Add(new RuleItem { Color = color, Width = outer, Ascent = axis + thickness / 2, Descent = -(axis - thickness / 2) }, 0, 0);
        }
        if (On(Property(box, "borderBoxPr", "strikeV"), false))
            result.Add(new RuleItem { Color = color, Width = thickness, Ascent = a + thickness, Descent = d + thickness }, (outer - thickness) / 2, 0);
        result.Width = outer;
        return result;
    }

    MathItem Phantom(XElement phant, Env env, double size)
    {
        var nucleus = Sequence(Child(phant, "e"), env, size);
        var show = On(Property(phant, "phantPr", "show"), true);
        var item = show ? nucleus : new SpaceItem { Width = nucleus.Width, Ascent = nucleus.Ascent, Descent = nucleus.Descent };
        var result = new ListItem();
        result.Add(item, 0, 0);
        if (On(Property(phant, "phantPr", "zeroWid"), false)) result.Width = 0;
        if (On(Property(phant, "phantPr", "zeroAsc"), false)) result.Ascent = 0;
        if (On(Property(phant, "phantPr", "zeroDesc"), false)) result.Descent = 0;
        return result;
    }
}
