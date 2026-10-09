using System.IO.Compression;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

namespace Fidelity.PowerPoint;

/// <summary>
/// Equations (catalog: text.math): Office Math (OMML in a14:m) of every construct, in display paragraphs and inline
/// in text, at 20 pt and, for spacing, at 40 pt. The object model cannot build equations, so the text boxes hold
/// markers that Patch replaces with OMML; PowerPoint then saves the deck again, adding the fallback pictures.
/// </summary>
[SupportedOSPlatform("windows")]
static class MathDeck
{
    /// <summary>A slide: boxes (left, top, width, height in points) with their markers, and a line of text with an inline marker.</summary>
    record Slide((float X, float Y, float W, float H, string Marker)[] Boxes, string? Inline);

    static readonly Slide[] Slides =
    [
        new([(40, 40, 430, 100, "EQ1"), (490, 40, 430, 100, "EQ2"), (40, 170, 430, 100, "EQ3"), (490, 170, 430, 110, "EQ4"),
             (40, 300, 430, 100, "EQ5"), (490, 300, 430, 110, "EQ6")], "Energy EQ7 is mass times the speed of light squared."),
        new([(20, 20, 450, 80, "EQ8"), (490, 20, 450, 80, "EQ9"), (20, 120, 450, 90, "EQ10"), (490, 120, 450, 80, "EQ11"),
             (20, 230, 450, 110, "EQ12"), (490, 230, 450, 90, "EQ13"), (20, 350, 450, 90, "EQ14"), (490, 350, 450, 80, "EQ15")],
            "Inline EQ16 in a sentence."),
        new([(15, 10, 300, 90, "EQ17"), (330, 10, 300, 90, "EQ18"), (645, 10, 300, 90, "EQ19"), (15, 110, 300, 90, "EQ20"),
             (330, 110, 300, 90, "EQ21"), (645, 110, 300, 90, "EQ22"), (15, 210, 300, 90, "EQ23"), (330, 210, 300, 90, "EQ24"),
             (645, 210, 300, 90, "EQ25"), (15, 310, 300, 90, "EQ26"), (330, 310, 300, 90, "EQ27"), (645, 310, 300, 90, "EQ28")],
            "Inline EQ29 in a sentence."),
        new([(10, 10, 465, 255, "EQ30"), (485, 10, 465, 255, "EQ31"), (10, 275, 465, 125, "EQ32"), (485, 275, 465, 125, "EQ33")],
            "Text EQ34 end"),
        new([(10, 10, 465, 200, "EQ35"), (485, 10, 465, 200, "EQ36"), (10, 240, 465, 120, "EQ37"), (485, 240, 465, 120, "EQ38")], null),
    ];

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        foreach (var (boxes, inline) in Slides)
        {
            var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
            foreach (var (x, y, w, h, marker) in boxes)
            {
                var box = slide.Shapes.AddTextbox(1, x, y, w, h);
                box.TextFrame.TextRange.Text = marker;
                box.TextFrame.TextRange.ParagraphFormat.Alignment = 2;
            }
            if (inline != null)
            {
                var line = slide.Shapes.AddTextbox(1, 20, 470, 900, 50);
                line.TextFrame.TextRange.Text = inline;
                line.TextFrame.TextRange.Font.Size = 24;
            }
        }
    }

    const string A14 = "http://schemas.microsoft.com/office/drawing/2010/main";
    const string MathNs = "http://schemas.openxmlformats.org/officeDocument/2006/math";

    /// <summary>The run size in hundredths of a point: 20 pt, 40 pt on the last slide.</summary>
    static int size = 2000;

    static string Props(bool italic) => $"<a:rPr lang=\"en-US\" sz=\"{size}\" i=\"{(italic ? 1 : 0)}\"><a:latin typeface=\"Cambria Math\"/></a:rPr>";

    static string Ctrl => $"<m:ctrlPr>{Props(true)}</m:ctrlPr>";

    /// <summary>A math run: single letters italic, words (function names) upright, everything else as typed.</summary>
    static string R(string text, bool? italic = null)
    {
        var it = italic ?? (text.Length == 1 && char.IsLetter(text[0]));
        var sty = !it && text.All(char.IsLetter) ? "<m:rPr><m:sty m:val=\"p\"/></m:rPr>" : "";
        return $"<m:r>{sty}{Props(it)}<m:t>{text}</m:t></m:r>";
    }

    static string Letters(string text) => string.Concat(text.Select(c => R(c.ToString())));

    /// <summary>A run with math run properties (m:sty, m:scr).</summary>
    static string Styled(string text, string properties) => $"<m:r><m:rPr>{properties}</m:rPr>{Props(false)}<m:t>{text}</m:t></m:r>";

    static string Frac(string num, string den, string? type = null) =>
        $"<m:f><m:fPr>{(type != null ? $"<m:type m:val=\"{type}\"/>" : "")}{Ctrl}</m:fPr><m:num>{num}</m:num><m:den>{den}</m:den></m:f>";

    static string Sup(string e, string sup) => $"<m:sSup><m:sSupPr>{Ctrl}</m:sSupPr><m:e>{e}</m:e><m:sup>{sup}</m:sup></m:sSup>";

    static string Sub(string e, string sub) => $"<m:sSub><m:sSubPr>{Ctrl}</m:sSubPr><m:e>{e}</m:e><m:sub>{sub}</m:sub></m:sSub>";

    static string SubSup(string e, string sub, string sup) =>
        $"<m:sSubSup><m:sSubSupPr>{Ctrl}</m:sSubSupPr><m:e>{e}</m:e><m:sub>{sub}</m:sub><m:sup>{sup}</m:sup></m:sSubSup>";

    static string Pre(string e, string sub, string sup) =>
        $"<m:sPre><m:sPrePr>{Ctrl}</m:sPrePr><m:sub>{sub}</m:sub><m:sup>{sup}</m:sup><m:e>{e}</m:e></m:sPre>";

    static string Sqrt(string e) => $"<m:rad><m:radPr><m:degHide m:val=\"1\"/>{Ctrl}</m:radPr><m:deg/><m:e>{e}</m:e></m:rad>";

    static string Root(string degree, string e) => $"<m:rad><m:radPr>{Ctrl}</m:radPr><m:deg>{degree}</m:deg><m:e>{e}</m:e></m:rad>";

    /// <summary>An n-ary operator; null limits are hidden, empty ones are left empty.</summary>
    static string Nary(string op, string? sub, string? sup, string e, string limits = "undOvr")
    {
        var hide = (sub == null ? "<m:subHide m:val=\"1\"/>" : "") + (sup == null ? "<m:supHide m:val=\"1\"/>" : "");
        return $"<m:nary><m:naryPr><m:chr m:val=\"{op}\"/><m:limLoc m:val=\"{limits}\"/>{hide}{Ctrl}</m:naryPr>" +
               $"{(string.IsNullOrEmpty(sub) ? "<m:sub/>" : $"<m:sub>{sub}</m:sub>")}{(string.IsNullOrEmpty(sup) ? "<m:sup/>" : $"<m:sup>{sup}</m:sup>")}<m:e>{e}</m:e></m:nary>";
    }

    static string Delim(string[] parts, string begin = "(", string end = ")") =>
        $"<m:d><m:dPr><m:begChr m:val=\"{begin}\"/><m:endChr m:val=\"{end}\"/>{Ctrl}</m:dPr>{string.Concat(parts.Select(p => $"<m:e>{p}</m:e>"))}</m:d>";

    static string Delim(string part, string begin = "(", string end = ")") => Delim([part], begin, end);

    static string Matrix(string[][] rows) =>
        $"<m:m><m:mPr><m:mcs><m:mc><m:mcPr><m:count m:val=\"{rows[0].Length}\"/><m:mcJc m:val=\"center\"/></m:mcPr></m:mc></m:mcs>{Ctrl}</m:mPr>" +
        string.Concat(rows.Select(row => "<m:mr>" + string.Concat(row.Select(c => $"<m:e>{c}</m:e>")) + "</m:mr>")) + "</m:m>";

    static string EqArr(params string[] rows) => $"<m:eqArr><m:eqArrPr>{Ctrl}</m:eqArrPr>{string.Concat(rows.Select(r => $"<m:e>{r}</m:e>"))}</m:eqArr>";

    static string Func(string name, string e) => $"<m:func><m:funcPr>{Ctrl}</m:funcPr><m:fName>{R(name, false)}</m:fName><m:e>{e}</m:e></m:func>";

    static string FuncOf(string fName, string e) => $"<m:func><m:funcPr>{Ctrl}</m:funcPr><m:fName>{fName}</m:fName><m:e>{e}</m:e></m:func>";

    static string LimLow(string e, string lim) => $"<m:limLow><m:limLowPr>{Ctrl}</m:limLowPr><m:e>{e}</m:e><m:lim>{lim}</m:lim></m:limLow>";

    static string LimUpp(string e, string lim) => $"<m:limUpp><m:limUppPr>{Ctrl}</m:limUppPr><m:e>{e}</m:e><m:lim>{lim}</m:lim></m:limUpp>";

    static string Acc(string e, string chr) => $"<m:acc><m:accPr><m:chr m:val=\"{chr}\"/>{Ctrl}</m:accPr><m:e>{e}</m:e></m:acc>";

    static string Bar(string e, string pos) => $"<m:bar><m:barPr><m:pos m:val=\"{pos}\"/>{Ctrl}</m:barPr><m:e>{e}</m:e></m:bar>";

    static string Group(string e, string chr, string pos, string vertJc) =>
        $"<m:groupChr><m:groupChrPr><m:chr m:val=\"{chr}\"/><m:pos m:val=\"{pos}\"/><m:vertJc m:val=\"{vertJc}\"/>{Ctrl}</m:groupChrPr><m:e>{e}</m:e></m:groupChr>";

    static string Box(string e) => $"<m:box><m:boxPr>{Ctrl}</m:boxPr><m:e>{e}</m:e></m:box>";

    static string Border(string e) => $"<m:borderBox><m:borderBoxPr>{Ctrl}</m:borderBoxPr><m:e>{e}</m:e></m:borderBox>";

    static string Para(string content) =>
        $"<a14:m xmlns:a14=\"{A14}\"><m:oMathPara xmlns:m=\"{MathNs}\"><m:oMathParaPr><m:jc m:val=\"centerGroup\"/></m:oMathParaPr><m:oMath>{content}</m:oMath></m:oMathPara></a14:m>";

    static string Inline(string content) => $"<a14:m xmlns:a14=\"{A14}\"><m:oMath xmlns:m=\"{MathNs}\">{content}</m:oMath></a14:m>";

    /// <summary>The equations by marker: display ones (in paragraphs of their own) and inline ones (in a line of text).</summary>
    static (Dictionary<string, string> Display, Dictionary<string, string> Inline) Equations()
    {
        var display = new Dictionary<string, string>();
        var inline = new Dictionary<string, string>();
        size = 2000;
        // Slide 1: familiar equations.
        display["EQ1"] = Para(R("x") + R("=") + Frac(R("−") + R("b") + R("±") + Sqrt(Sup(R("b"), R("2")) + R("−") + R("4") + R("a") + R("c")), R("2") + R("a")));
        display["EQ2"] = Para(Nary("∑", R("i") + R("=") + R("1"), R("n"), R("i")) + R("=") + Frac(R("n") + Delim(R("n") + R("+") + R("1")), R("2")));
        display["EQ3"] = Para(Nary("∫", R("0"), R("∞"), Sup(R("e"), R("−") + Sup(R("x"), R("2"))) + R("d", false) + R("x"), "subSup") + R("=") + Frac(Sqrt(R("π")), R("2")));
        display["EQ4"] = Para(R("A") + R("=") + Delim(Matrix([[R("a"), R("b")], [R("c"), R("d")]]), "[", "]"));
        display["EQ5"] = Para(FuncOf(LimLow(R("lim", false), R("x") + R("→") + R("0")), Frac(Func("sin", R("x")), R("x"))) + R("=") + R("1"));
        display["EQ6"] = Para(Bar(R("x"), "top") + R("=") + Delim(Frac(R("1"), R("n")) + Nary("∑", R("k") + R("=") + R("1"), R("n"), Sub(R("x"), R("k")))) + R(",")
                              + Acc(R("v"), "⃗") + R("=") + Sup(Delim(R("a") + R("+") + R("b")), R("n")));
        inline["EQ7"] = Inline(R("E") + R("=") + R("m") + Sup(R("c"), R("2")));
        // Slide 2: fractions, roots, scripts, operators, delimiters, arrays, accents, limits, boxes.
        display["EQ8"] = Para(Frac(R("1"), R("1") + R("+") + Frac(R("1"), R("x"))) + R("=") + Frac(R("a"), R("b"), "lin") + R("+") + Frac(R("c"), R("d"), "skw"));
        display["EQ9"] = Para(Root(R("3"), R("x") + R("+") + R("1")) + R("+") + SubSup(R("x"), R("i"), R("2")) + R("−") + Pre(R("C"), R("n"), R("k")));
        display["EQ10"] = Para(Nary("∏", R("i") + R("=") + R("1"), R("n"), Sub(R("a"), R("i"))) + R("=") + Nary("∮", R("C"), "", R("f") + R("d", false) + R("z"), "subSup")
                               + R("+") + Nary("∬", null, null, R("g") + R("d", false) + R("A"), "subSup"));
        display["EQ11"] = Para(Delim([R("a"), R("b")]) + R("+") + Delim(R("x"), "{", "}") + R("+") + Delim(R("ψ"), "⟨", "⟩") + R("+") + Delim(R("x"), "|", "|")
                               + R("+") + Delim(R("y"), "⌊", "⌋"));
        display["EQ12"] = Para(R("M") + R("=") + Delim(Matrix([[R("1"), R("0"), R("0")], [R("0"), R("1"), R("0")], [R("0"), R("0"), R("1")]])));
        display["EQ13"] = Para(EqArr(R("x") + R("+") + R("y") + R("=") + R("2"), R("x") + R("−") + R("y") + R("=") + R("0")));
        display["EQ14"] = Para(Acc(R("a"), "̂") + R("+") + Acc(R("b"), "̃") + R("+") + Acc(R("c"), "̇") + R("+") + Acc(R("A") + R("B"), "⃗") + R("+")
                               + LimLow(Group(R("x") + R("+") + R("y") + R("+") + R("z"), "⏟", "bot", "top"), R("n")) + R("+") + Bar(R("w"), "bot"));
        display["EQ15"] = Para(LimUpp(R("max", false), R("t")) + Func("cos", R("θ")) + R("+") + Func("log", R("n")) + R("=") + Border(R("E") + R("=") + R("m") + Sup(R("c"), R("2")))
                               + R("+") + Box(R("k")));
        inline["EQ16"] = Inline(Frac(R("a"), R("b")) + R("+") + Nary("∑", R("i") + R("=") + R("1"), R("n"), R("i")) + R("+") + Sqrt(R("x")));
        // Slide 3: one construct each.
        display["EQ17"] = Para(Frac(R("1"), R("2"), "skw") + R("+") + Frac(Sup(R("x"), R("2")), R("y"), "skw") + R("+") + Frac(Frac(R("a"), R("b")), R("c"), "skw"));
        display["EQ18"] = Para(Nary("∫", null, null, R("x"), "subSup") + R("+") + Nary("∫", R("0"), R("1"), R("x"), "subSup"));
        display["EQ19"] = Para(Nary("∑", R("i") + R("=") + R("1"), R("n"), R("x")) + R("+") + Nary("∑", null, null, R("x")));
        display["EQ20"] = Para(Nary("∫", R("0"), "", R("x"), "subSup") + R("+") + R("y"));
        display["EQ21"] = Para(Acc(R("x"), "̂") + R("+") + Acc(R("y"), "̃") + R("+") + Acc(R("x"), "̅") + R("+") + Acc(R("x"), "̌"));
        display["EQ22"] = Para(Acc(R("x"), "⃗") + R("+") + Acc(R("x"), "̆") + R("+") + Acc(R("y"), "̈") + R("+") + Bar(R("a") + R("b"), "top"));
        display["EQ23"] = Para(Delim([R("a"), Frac(R("1"), R("2")), R("c")]));
        display["EQ24"] = Para(Border(R("x")) + R("+") + Border(Frac(R("a"), R("b"))));
        display["EQ25"] = Para(Group(Letters("a") + R("+") + R("b") + R("+") + R("c") + R("+") + R("d"), "⏞", "top", "bot") + R("+") + R("e"));
        display["EQ26"] = Para(Frac(R("1"), R("1") + R("+") + Frac(R("1"), R("1") + R("+") + Frac(R("1"), R("x")))));
        display["EQ27"] = Para(FuncOf(LimLow(R("lim", false), R("n") + R("→") + R("∞")), R("x")) + R("+") + Func("sin", R("x")));
        display["EQ28"] = Para(Sup(R("x"), Frac(R("a"), R("b"))) + R("+") + Sub(R("y"), Frac(R("1"), R("2"))));
        inline["EQ29"] = Inline(Nary("∫", R("0"), R("1"), R("x") + R("d", false) + R("x"), "subSup") + R("+") + Frac(R("a"), R("b")) + R("+") + Sup(R("x"), Frac(R("a"), R("b"))));
        // Slide 4: spacing at 40 pt.
        size = 4000;
        display["EQ30"] = Para(Nary("∫", R("0"), R("1"), R("x"), "subSup") + R("+") + Nary("∫", null, null, R("x"), "subSup") + R("+") + Nary("∑", null, null, R("x")));
        display["EQ31"] = Para(Nary("∑", R("i") + R("=") + R("1"), R("n"), R("x")) + R("+") + Delim(Frac(R("1"), R("2"))));
        display["EQ32"] = Para(Sub(R("x"), R("i") + R("=") + R("1")) + R("+") + Sup(R("y"), R("a") + R("+") + R("b")));
        display["EQ33"] = Para(Letters("abcdefgh"));
        inline["EQ34"] = Inline(Nary("∫", R("0"), R("1"), R("x"), "subSup") + R("+") + Nary("∑", R("i") + R("=") + R("1"), R("n"), R("x")));
        // Slide 5: equation arrays aligned at "&", math alphabets (m:scr, which PowerPoint saves as the characters
        // themselves) and bold Greek.
        size = 2800;
        display["EQ35"] = Para(EqArr(R("x") + R("&amp;=") + R("1"), R("x") + R("+") + R("y") + R("&amp;=") + R("12")));
        display["EQ36"] = Para(EqArr(R("x") + R("&amp;=") + R("1") + R("&amp;") + R("y") + R("&amp;=") + R("2"),
                                     R("a") + R("+") + R("b") + R("&amp;=") + R("3") + R("&amp;") + R("c") + R("&amp;=") + R("45")));
        display["EQ37"] = Para(Styled("RN", "<m:scr m:val=\"double-struck\"/><m:sty m:val=\"p\"/>") + R("+") + Styled("L", "<m:scr m:val=\"script\"/><m:sty m:val=\"p\"/>")
                               + R("+") + Styled("g", "<m:scr m:val=\"fraktur\"/><m:sty m:val=\"p\"/>") + R("+") + Styled("x", "<m:scr m:val=\"sans-serif\"/>")
                               + R("+") + Styled("y", "<m:scr m:val=\"monospace\"/><m:sty m:val=\"p\"/>"));
        display["EQ38"] = Para(Styled("α", "<m:sty m:val=\"b\"/>") + R("+") + Styled("Γ", "<m:sty m:val=\"b\"/>") + R("+") + Styled("Γ", "<m:sty m:val=\"bi\"/>")
                               + R("+") + R("Γ") + R("+") + Styled("x", "<m:sty m:val=\"bi\"/>"));
        return (display, inline);
    }

    public static void Patch(string path)
    {
        var (display, inline) = Equations();
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        foreach (var entry in zip.Entries.Where(e => Regex.IsMatch(e.FullName, @"^ppt/slides/slide\d+\.xml$")).ToList())
        {
            string xml;
            using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                xml = reader.ReadToEnd();
            foreach (var (marker, equation) in display)
            {
                // The paragraph holding the marker becomes the equation's paragraph.
                xml = Regex.Replace(xml, @"<a:p>(?:(?!</a:p>).)*?<a:t>" + marker + @"</a:t>(?:(?!</a:p>).)*?</a:p>", "<a:p>" + equation + "</a:p>", RegexOptions.Singleline);
            }
            foreach (var (marker, equation) in inline)
            {
                // The run holding the marker is split around the equation: "Energy EQ7 is …".
                xml = Regex.Replace(xml, @"(<a:r>(<a:rPr[^>]*?(?:/>|>.*?</a:rPr>))<a:t>)([^<]*?)" + marker + @"([^<]*)(</a:t></a:r>)",
                    m => m.Groups[1].Value + m.Groups[3].Value + "</a:t></a:r>" + equation + "<a:r>" + m.Groups[2].Value + "<a:t>" + m.Groups[4].Value + m.Groups[5].Value,
                    RegexOptions.Singleline);
            }
            var name = entry.FullName;
            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
            writer.Write(xml);
        }
    }
}
