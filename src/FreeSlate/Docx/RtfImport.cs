using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>
/// Imports an RTF chunk (w:altChunk, catalog: document.altChunk) as WordprocessingML, the way Word converts it when it
/// opens the document (measured with docx/local/altchunk-rtf, altchunk-rtf2). Word reads the RTF into a document of
/// its own and pastes that with the destination's styles: a paragraph whose style the document has (by name) takes
/// it, and of the RTF's formatting only what differs from the RTF's own definition of that style stays as direct
/// formatting; a style the document lacks comes along with all its formatting. The RTF's styles start from its
/// defaults (\*\defchp, \*\defpap, else the default font, 12 pt and no spacing); without a stylesheet its paragraphs
/// are in Word's own Normal (Aptos 12 pt, 8 pt after, 1.16 lines) unless the RTF names defaults. Character formatting
/// lasts until \plain or the end of its group, paragraph formatting until \pard; list labels come from \listtext and
/// \pntext where the paragraph is in a list; fields keep their instructions and results; pictures (PNG, JPEG, EMF,
/// WMF) stand in their runs; tables are built from their rows' cell boundaries, gaps, paddings, borders and shading.
/// </summary>
public static class RtfImport
{
    static readonly XNamespace W = Wml.W;

    /// <summary>The blocks (w:p, w:tbl) of an RTF document read as Latin-1 text (one character a byte).</summary>
    /// <param name="styleIdByName">The document's style id for a style name, or null where it has none.</param>
    /// <param name="addStyle">Adds a paragraph style of a name with its paragraph and run properties to the document,
    /// returning its id: a style the document lacks.</param>
    public static List<XElement> Convert(byte[] data, Func<string, string?> styleIdByName, Func<string, XElement, XElement, string> addStyle)
    {
        var reader = new Reader(Encoding.Latin1.GetString(data), styleIdByName, addStyle);
        reader.Read();
        return reader.Blocks;
    }

    /// <summary>The character and paragraph properties every RTF style and \plain or \pard start from.</summary>
    static readonly Dictionary<string, string> CharDefaults = new()
    {
        ["size"] = "24", ["b"] = "0", ["i"] = "0", ["u"] = "none", ["strike"] = "0", ["dstrike"] = "0", ["caps"] = "0", ["smallCaps"] = "0",
        ["vanish"] = "0", ["outline"] = "0", ["shadow"] = "0", ["emboss"] = "0", ["imprint"] = "0", ["color"] = "auto", ["highlight"] = "none",
        ["shd"] = "none", ["vertAlign"] = "baseline", ["position"] = "0", ["spacing"] = "0", ["w"] = "100",
    };

    static readonly Dictionary<string, string> ParaDefaults = new()
    {
        ["jc"] = "left", ["left"] = "0", ["right"] = "0", ["firstLine"] = "0", ["before"] = "0", ["after"] = "0", ["line"] = "240",
        ["lineRule"] = "auto", ["beforeAuto"] = "0", ["afterAuto"] = "0", ["keepLines"] = "0", ["keepNext"] = "0", ["pageBreakBefore"] = "0",
        ["contextual"] = "0", ["bidi"] = "0", ["tabs"] = "", ["borders"] = "", ["shading"] = "none", ["list"] = "0",
    };

    enum Destination { Body, Skip, FontTable, Font, ColorTable, StyleSheet, Style, FieldInstruction, Picture, Label, DefaultChar, DefaultPara }

    sealed class State
    {
        public Dictionary<string, string> Char = new();
        public Dictionary<string, string> Para = new();
        public int Style;
        public Destination Destination = Destination.Body;
        public int Uc = 1;
        public bool InTable;

        public State Clone() => new()
        {
            Char = new Dictionary<string, string>(Char), Para = new Dictionary<string, string>(Para), Style = Style,
            Destination = Destination, Uc = Uc, InTable = InTable,
        };
    }

    sealed record Font(string Name, int Charset);

    sealed record RtfStyle(int Number, string Kind, string Name, Dictionary<string, string> Char, Dictionary<string, string> Para, int? BasedOn);

    /// <summary>A piece of a paragraph: text in its formatting, a break or tab, a picture or a field mark.</summary>
    sealed record Piece(string Kind, string Text, Dictionary<string, string> Char);

    sealed record Cell(Dictionary<string, string> Props, int Right);

    sealed class Reader(string rtf, Func<string, string?> styleIdByName, Func<string, XElement, XElement, string> addStyle)
    {
        /// <summary>The document's ids of the RTF's styles it lacked, by name.</summary>
        readonly Dictionary<string, string> imported = new(StringComparer.OrdinalIgnoreCase);

        public List<XElement> Blocks { get; } = [];

        int position;
        State state = new();
        readonly Stack<State> stack = new();
        readonly Dictionary<int, Font> fonts = [];
        readonly List<string> colors = [];
        readonly Dictionary<int, RtfStyle> styles = [];
        int defaultFont;
        int codePage = 1252;
        bool hasStyleSheet;
        Dictionary<string, string>? defaultChar, defaultPara;

        // The paragraph being read, its list label and the field marks.
        readonly List<Piece> pieces = [];
        readonly List<Piece> label = [];
        readonly StringBuilder fieldInstruction = new();
        readonly Stack<int> fieldGroups = new();

        // The table being read: rows of cells (each a list of paragraphs), the row definition and the cell being defined.
        readonly List<(List<Cell> Cells, Dictionary<string, string> Row, List<List<XElement>> Content)> rows = [];
        List<Cell> rowCells = [];
        Dictionary<string, string> rowProps = new();
        Dictionary<string, string> cellProps = new();
        List<List<XElement>> rowContent = [];
        List<XElement> cellContent = [];

        // Pending font, style or picture of the destination being read.
        int fontNumber;
        int fontCharset;
        readonly StringBuilder destinationText = new();
        RtfStyle? pendingStyle;
        readonly StringBuilder pictureHex = new();
        Dictionary<string, string> pictureProps = new();
        readonly List<byte> pendingBytes = [];

        public void Read()
        {
            state.Char = CharStart();
            state.Para = new Dictionary<string, string>(ParaDefaults);
            while (position < rtf.Length)
            {
                var c = rtf[position];
                switch (c)
                {
                    case '{':
                        FlushBytes();
                        stack.Push(state.Clone());
                        position++;
                        break;
                    case '}':
                        FlushBytes();
                        EndGroup();
                        position++;
                        break;
                    case '\\':
                        ControlWord();
                        break;
                    case '\r' or '\n':
                        position++;
                        break;
                    default:
                        if (state.Destination == Destination.Picture)
                        {
                            if (Uri.IsHexDigit(c))
                                pictureHex.Append(c);
                        }
                        else if (c < 0x80)
                        {
                            FlushBytes();
                            Text(c.ToString());
                        }
                        else
                            pendingBytes.Add((byte)c);
                        position++;
                        break;
                }
            }
            FlushBytes();
            if (pieces.Count > 0)
                EndParagraph();
            EndTable();
        }

        Dictionary<string, string> CharStart() => new(CharDefaults);

        /// <summary>Character properties with the default font where they name none (\plain and the start).</summary>
        Dictionary<string, string> Resolved(Dictionary<string, string> c) =>
            c.ContainsKey("font") ? c : new Dictionary<string, string>(c) { ["font"] = fonts.TryGetValue(defaultFont, out var f) ? f.Name : "Times New Roman" };

        void EndGroup()
        {
            var closing = state;
            switch (closing.Destination)
            {
                case Destination.Font when !fonts.ContainsKey(fontNumber) || stack.Count > 0 && stack.Peek().Destination == Destination.FontTable:
                    var name = destinationText.ToString().Trim().TrimEnd(';').Trim();
                    if (name.Length > 0 && !fonts.ContainsKey(fontNumber))
                        fonts[fontNumber] = new Font(name, fontCharset);
                    destinationText.Clear();
                    break;
                case Destination.Style when pendingStyle != null && (stack.Count == 0 || stack.Peek().Destination == Destination.StyleSheet):
                    var styleName = destinationText.ToString().Trim().TrimEnd(';').Trim();
                    styles[pendingStyle.Number] = pendingStyle with { Name = styleName, Char = closing.Char, Para = closing.Para };
                    pendingStyle = null;
                    destinationText.Clear();
                    break;
                case Destination.Picture when stack.Count == 0 || stack.Peek().Destination != Destination.Picture:
                    Picture();
                    break;
                case Destination.FieldInstruction when stack.Count == 0 || stack.Peek().Destination != Destination.FieldInstruction:
                    pieces.Add(new Piece("fieldBegin", fieldInstruction.ToString().Trim(), new Dictionary<string, string>(closing.Char)));
                    pieces.Add(new Piece("fieldSeparate", "", new Dictionary<string, string>(closing.Char)));
                    fieldInstruction.Clear();
                    break;
                case Destination.DefaultChar when stack.Count == 0 || stack.Peek().Destination != Destination.DefaultChar:
                    defaultChar = closing.Char;
                    break;
                case Destination.DefaultPara when stack.Count == 0 || stack.Peek().Destination != Destination.DefaultPara:
                    defaultPara = closing.Para;
                    break;
            }
            if (fieldGroups.Count > 0 && fieldGroups.Peek() == stack.Count)
            {
                fieldGroups.Pop();
                pieces.Add(new Piece("fieldEnd", "", new Dictionary<string, string>(closing.Char)));
            }
            state = stack.Count > 0 ? stack.Pop() : state;
        }

        void ControlWord()
        {
            position++;
            if (position >= rtf.Length)
                return;
            var c = rtf[position];
            if (!char.IsAsciiLetter(c))
            {
                position++;
                switch (c)
                {
                    case '\\' or '{' or '}':
                        FlushBytes();
                        Text(c.ToString());
                        break;
                    case '\'':
                        if (position + 2 <= rtf.Length && int.TryParse(rtf.AsSpan(position, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                        {
                            if (state.Destination == Destination.Picture)
                                pictureHex.Append(rtf, position, 2);
                            else
                                pendingBytes.Add((byte)code);
                            position += 2;
                        }
                        break;
                    case '*':
                        // An optional destination: skipped unless known.
                        if (Peek() is { } next && !Known(next))
                            state.Destination = Destination.Skip;
                        break;
                    case '~':
                        FlushBytes();
                        Text("\u00A0");
                        break;
                    case '-':
                        FlushBytes();
                        Text("\u00AD");
                        break;
                    case '_':
                        FlushBytes();
                        Text("\u2011");
                        break;
                    case '\r' or '\n':
                        FlushBytes();
                        Paragraph();
                        break;
                }
                return;
            }
            var start = position;
            while (position < rtf.Length && char.IsAsciiLetter(rtf[position]))
                position++;
            var word = rtf[start..position];
            int? parameter = null;
            var negative = position < rtf.Length && rtf[position] == '-';
            if (negative)
                position++;
            var digits = position;
            while (position < rtf.Length && char.IsAsciiDigit(rtf[position]))
                position++;
            if (position > digits && long.TryParse(rtf.AsSpan(digits, position - digits), CultureInfo.InvariantCulture, out var number))
                parameter = (int)Math.Clamp(negative ? -number : number, int.MinValue, int.MaxValue);
            else if (negative)
                position--;
            if (position < rtf.Length && rtf[position] == ' ')
                position++;
            if (word != "u" && word != "bin")
                FlushBytes();
            Word(word, parameter);
        }

        /// <summary>The control word after "\*" in this group.</summary>
        string? Peek()
        {
            var p = position;
            while (p < rtf.Length && rtf[p] is ' ' or '\r' or '\n')
                p++;
            if (p >= rtf.Length || rtf[p] != '\\')
                return null;
            var s = ++p;
            while (p < rtf.Length && char.IsAsciiLetter(rtf[p]))
                p++;
            return rtf[s..p];
        }

        static bool Known(string word) => word is "shppict" or "fldinst" or "defchp" or "defpap" or "pn" or "cs" or "ts" or "ds";

        void Word(string word, int? parameter)
        {
            var on = parameter != 0;
            var value = parameter ?? 0;
            switch (state.Destination)
            {
                case Destination.Skip:
                    if (word == "bin" && value > 0)
                        position += value;
                    return;
                case Destination.FontTable or Destination.Font:
                    switch (word)
                    {
                        case "f":
                            state.Destination = Destination.Font;
                            fontNumber = value;
                            fontCharset = 0;
                            destinationText.Clear();
                            return;
                        case "fcharset":
                            fontCharset = value;
                            return;
                    }
                    return;
                case Destination.ColorTable:
                    // The entry being read; a semicolon ends it (see Text), an empty one is auto.
                    switch (word)
                    {
                        case "red": color.R = Math.Clamp(value, 0, 255); color.Set = true; break;
                        case "green": color.G = Math.Clamp(value, 0, 255); color.Set = true; break;
                        case "blue": color.B = Math.Clamp(value, 0, 255); color.Set = true; break;
                    }
                    return;
                case Destination.Picture:
                    switch (word)
                    {
                        case "bin" when value > 0:
                            pictureHex.Append(string.Concat(rtf.AsSpan(position, Math.Min(value, rtf.Length - position)).ToArray().Select(ch => ((int)ch).ToString("X2"))));
                            position += value;
                            return;
                        case "pngblip": pictureProps["type"] = "png"; return;
                        case "jpegblip": pictureProps["type"] = "jpeg"; return;
                        case "emfblip": pictureProps["type"] = "emf"; return;
                        case "wmetafile": pictureProps["type"] = "wmf"; return;
                        case "picw" or "pich" or "picwgoal" or "pichgoal" or "picscalex" or "picscaley" or "piccropl" or "piccropr" or "piccropt" or "piccropb":
                            pictureProps[word] = value.ToString(CultureInfo.InvariantCulture);
                            return;
                    }
                    return;
            }
            switch (word)
            {
                // Destinations.
                case "fonttbl": state.Destination = Destination.FontTable; return;
                case "colortbl": state.Destination = Destination.ColorTable; colors.Clear(); color = default; return;
                case "stylesheet": state.Destination = Destination.StyleSheet; hasStyleSheet = true; return;
                case "info" or "header" or "footer" or "headerl" or "headerr" or "headerf" or "footerl" or "footerr" or "footerf" or "footnote"
                    or "annotation" or "xe" or "tc" or "txe" or "rxe" or "nonshppict" or "listtable" or "listoverridetable" or "revtbl"
                    or "filetbl" or "pgdsctbl" or "rsidtbl" or "generator" or "template" or "latentstyles" or "themedata" or "colorschememapping"
                    or "datastore" or "xmlnstbl" or "mmathPr" or "pnseclvl" or "pn" or "fldtype" or "background" or "shp" or "shpinst" or "do" or "object"
                    or "nesttableprops" or "bkmkstart" or "bkmkend" or "atnid" or "atnauthor" or "atrfstart" or "atrfend":
                    state.Destination = Destination.Skip;
                    if (word == "pn")
                        state.Para["list"] = "1";
                    return;
                case "defchp":
                    state.Destination = Destination.DefaultChar;
                    state.Char = new Dictionary<string, string>();
                    return;
                case "defpap":
                    state.Destination = Destination.DefaultPara;
                    state.Para = new Dictionary<string, string>();
                    return;
                case "pict":
                    state.Destination = Destination.Picture;
                    pictureHex.Clear();
                    pictureProps = new Dictionary<string, string>();
                    return;
                case "shppict":
                    return;
                case "fldinst":
                    state.Destination = Destination.FieldInstruction;
                    fieldInstruction.Clear();
                    return;
                case "fldrslt":
                    return;
                case "field":
                    fieldGroups.Push(stack.Count);
                    return;
                case "listtext" or "pntext":
                    state.Destination = Destination.Label;
                    return;
                case "s" when state.Destination == Destination.StyleSheet || state.Destination == Destination.Style:
                case "cs" when state.Destination is Destination.StyleSheet or Destination.Style:
                case "ts" when state.Destination is Destination.StyleSheet or Destination.Style:
                case "ds" when state.Destination is Destination.StyleSheet or Destination.Style:
                    state.Destination = Destination.Style;
                    pendingStyle = new RtfStyle(value, word == "s" ? "paragraph" : word == "cs" ? "character" : "other", "", [], [], null);
                    destinationText.Clear();
                    return;
                case "sbasedon" when pendingStyle != null:
                    pendingStyle = pendingStyle with { BasedOn = value };
                    return;
                // Document properties.
                case "deff": defaultFont = value; return;
                case "ansicpg": codePage = value; return;
                case "uc": state.Uc = Math.Max(0, value); return;
                case "u":
                    FlushBytes();
                    Text(((char)(value < 0 ? value + 65536 : value)).ToString());
                    SkipFallback(state.Uc);
                    return;
                // Text.
                case "par":
                    Paragraph();
                    return;
                case "sect":
                    Paragraph();
                    return;
                case "line": Special("br"); return;
                case "page": Special("page"); return;
                case "column": Special("column"); return;
                case "tab": Special("tab"); return;
                case "emdash": Text("\u2014"); return;
                case "endash": Text("\u2013"); return;
                case "emspace": Text("\u2003"); return;
                case "enspace": Text("\u2002"); return;
                case "qmspace": Text("\u2005"); return;
                case "bullet": Text("\u2022"); return;
                case "lquote": Text("\u2018"); return;
                case "rquote": Text("\u2019"); return;
                case "ldblquote": Text("\u201C"); return;
                case "rdblquote": Text("\u201D"); return;
                case "zwj": Text("\u200D"); return;
                case "zwnj": Text("\u200C"); return;
                case "ltrmark": Text("\u200E"); return;
                case "rtlmark": Text("\u200F"); return;
                // Tables.
                case "trowd":
                    rowProps = new Dictionary<string, string>();
                    rowCells = [];
                    cellProps = new Dictionary<string, string>();
                    return;
                case "intbl": state.InTable = true; return;
                case "cell" or "nestcell":
                    EndParagraph(cell: true);
                    rowContent.Add(cellContent);
                    cellContent = [];
                    return;
                case "row":
                    if (cellContent.Count > 0)
                    {
                        rowContent.Add(cellContent);
                        cellContent = [];
                    }
                    rows.Add((rowCells, rowProps, rowContent));
                    rowContent = [];
                    return;
                case "cellx":
                    rowCells.Add(new Cell(cellProps, value));
                    cellProps = new Dictionary<string, string>();
                    return;
                case "trgaph": rowProps["gap"] = value.ToString(CultureInfo.InvariantCulture); return;
                case "trleft": rowProps["left"] = value.ToString(CultureInfo.InvariantCulture); return;
                case "trpaddl": rowProps["padL"] = value.ToString(CultureInfo.InvariantCulture); return;
                case "trpaddr": rowProps["padR"] = value.ToString(CultureInfo.InvariantCulture); return;
                case "trpaddt": rowProps["padT"] = value.ToString(CultureInfo.InvariantCulture); return;
                case "trpaddb": rowProps["padB"] = value.ToString(CultureInfo.InvariantCulture); return;
                case "trrh": rowProps["height"] = value.ToString(CultureInfo.InvariantCulture); return;
                case "trhdr": rowProps["header"] = "1"; return;
                case "trqc": rowProps["jc"] = "center"; return;
                case "trqr": rowProps["jc"] = "right"; return;
                case "trbrdrt": border = "row:top"; return;
                case "trbrdrl": border = "row:left"; return;
                case "trbrdrb": border = "row:bottom"; return;
                case "trbrdrr": border = "row:right"; return;
                case "trbrdrh": border = "row:insideH"; return;
                case "trbrdrv": border = "row:insideV"; return;
                case "clbrdrt": border = "cell:top"; return;
                case "clbrdrl": border = "cell:left"; return;
                case "clbrdrb": border = "cell:bottom"; return;
                case "clbrdrr": border = "cell:right"; return;
                case "clcbpat": cellProps["fill"] = Color(value); return;
                case "clvertalt": cellProps["vAlign"] = "top"; return;
                case "clvertalc": cellProps["vAlign"] = "center"; return;
                case "clvertalb": cellProps["vAlign"] = "bottom"; return;
                case "clmgf": cellProps["hMerge"] = "restart"; return;
                case "clmrg": cellProps["hMerge"] = "continue"; return;
                case "clvmgf": cellProps["vMerge"] = "restart"; return;
                case "clvmrg": cellProps["vMerge"] = "continue"; return;
                // Paragraph borders.
                case "brdrt": border = "para:top"; return;
                case "brdrb": border = "para:bottom"; return;
                case "brdrl": border = "para:left"; return;
                case "brdrr": border = "para:right"; return;
                case "brdrbtw": border = "para:between"; return;
                case "box": border = "para:box"; return;
                case "brdrs" or "brdrdb" or "brdrth" or "brdrdot" or "brdrdash" or "brdrhair" or "brdrdashsm" or "brdrdashd" or "brdrdashdd" or "brdrtriple" or "brdrwavy" or "brdrnone" or "brdrnil" or "brdrtbl":
                    Border("type", word switch
                    {
                        "brdrdb" => "double", "brdrth" => "thick", "brdrdot" => "dotted", "brdrdash" or "brdrdashsm" => "dashed", "brdrhair" => "single",
                        "brdrdashd" => "dotDash", "brdrdashdd" => "dotDotDash", "brdrtriple" => "triple", "brdrwavy" => "wave",
                        "brdrnone" or "brdrnil" or "brdrtbl" => "nil", _ => "single",
                    });
                    return;
                case "brdrw": Border("width", value.ToString(CultureInfo.InvariantCulture)); return;
                case "brdrcf": Border("color", Color(value)); return;
                case "brsp": Border("space", value.ToString(CultureInfo.InvariantCulture)); return;
            }
            if (state.Destination is Destination.Body or Destination.Label or Destination.Style or Destination.StyleSheet
                or Destination.DefaultChar or Destination.DefaultPara or Destination.FieldInstruction)
                Property(word, parameter, on, value);
        }

        // The border being defined: its owner and side.
        string border = "";

        // The color table's entry being read.
        (int R, int G, int B, bool Set) color;

        void Border(string key, string value)
        {
            if (border.Length == 0)
                return;
            var (owner, side) = (border[..border.IndexOf(':')], border[(border.IndexOf(':') + 1)..]);
            switch (owner)
            {
                case "row":
                    rowProps[$"border.{side}.{key}"] = value;
                    break;
                case "cell":
                    cellProps[$"border.{side}.{key}"] = value;
                    break;
                default:
                    foreach (var s in side == "box" ? new[] { "top", "left", "bottom", "right" } : [side])
                    {
                        var all = Parse(state.Para.GetValueOrDefault("borders", ""));
                        all[$"{s}.{key}"] = value;
                        state.Para["borders"] = string.Join(";", all.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
                    }
                    break;
            }
        }

        static Dictionary<string, string> Parse(string serialized) =>
            serialized.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)).Where(p => p.Length == 2)
                .ToDictionary(p => p[0], p => p[1]);

        string Color(int index) => index > 0 && index < colors.Count ? colors[index] : "auto";

        /// <summary>A character or paragraph property control word.</summary>
        void Property(string word, int? parameter, bool on, int value)
        {
            var c = state.Char;
            var p = state.Para;
            string V(bool flag) => flag ? "1" : "0";
            switch (word)
            {
                case "plain":
                    state.Char = CharStart();
                    return;
                case "pard":
                    state.Para = new Dictionary<string, string>(ParaDefaults);
                    state.Style = 0;
                    state.InTable = false;
                    return;
                case "s": state.Style = value; return;
                case "f":
                    c["font"] = fonts.TryGetValue(value, out var font) ? font.Name : c.GetValueOrDefault("font", "Times New Roman");
                    c["fontNumber"] = value.ToString(CultureInfo.InvariantCulture);
                    return;
                case "fs": c["size"] = Math.Max(1, value).ToString(CultureInfo.InvariantCulture); return;
                case "b": c["b"] = V(on); return;
                case "i": c["i"] = V(on); return;
                case "ul": c["u"] = on ? "single" : "none"; return;
                case "uld": c["u"] = "dotted"; return;
                case "uldb": c["u"] = "double"; return;
                case "uldash": c["u"] = "dash"; return;
                case "uldashd": c["u"] = "dotDash"; return;
                case "uldashdd": c["u"] = "dotDotDash"; return;
                case "ulth": c["u"] = "thick"; return;
                case "ulw": c["u"] = "words"; return;
                case "ulwave": c["u"] = "wave"; return;
                case "ulnone": c["u"] = "none"; return;
                case "strike": c["strike"] = V(on); return;
                case "striked": c["dstrike"] = V(on); return;
                case "caps": c["caps"] = V(on); return;
                case "scaps": c["smallCaps"] = V(on); return;
                case "v": c["vanish"] = V(on); return;
                case "outl": c["outline"] = V(on); return;
                case "shad": c["shadow"] = V(on); return;
                case "embo": c["emboss"] = V(on); return;
                case "impr": c["imprint"] = V(on); return;
                case "cf": c["color"] = Color(value); return;
                case "highlight": c["highlight"] = value == 0 ? "none" : Highlight(Color(value)); return;
                case "cb" or "chcbpat": c["shd"] = value == 0 ? "none" : Color(value); return;
                case "super": c["vertAlign"] = on ? "superscript" : "baseline"; return;
                case "sub": c["vertAlign"] = on ? "subscript" : "baseline"; return;
                case "nosupersub": c["vertAlign"] = "baseline"; return;
                case "up": c["position"] = (parameter ?? 6).ToString(CultureInfo.InvariantCulture); return;
                case "dn": c["position"] = (-(parameter ?? 6)).ToString(CultureInfo.InvariantCulture); return;
                case "expndtw": c["spacing"] = value.ToString(CultureInfo.InvariantCulture); return;
                case "expnd": c["spacing"] = (value * 5).ToString(CultureInfo.InvariantCulture); return;
                case "charscalex": c["w"] = value.ToString(CultureInfo.InvariantCulture); return;
                // Paragraph properties.
                case "ql": p["jc"] = "left"; return;
                case "qc": p["jc"] = "center"; return;
                case "qr": p["jc"] = "right"; return;
                case "qj": p["jc"] = "both"; return;
                case "qd": p["jc"] = "distribute"; return;
                case "li" or "lin": p["left"] = value.ToString(CultureInfo.InvariantCulture); return;
                case "ri" or "rin": p["right"] = value.ToString(CultureInfo.InvariantCulture); return;
                case "fi": p["firstLine"] = value.ToString(CultureInfo.InvariantCulture); return;
                case "sb": p["before"] = value.ToString(CultureInfo.InvariantCulture); return;
                case "sa": p["after"] = value.ToString(CultureInfo.InvariantCulture); return;
                case "sbauto": p["beforeAuto"] = V(on); return;
                case "saauto": p["afterAuto"] = V(on); return;
                case "sl":
                    if (value == 0)
                        (p["line"], p["lineRule"]) = ("240", "auto");
                    else
                        (p["line"], p["lineRule"]) = (Math.Abs(value).ToString(CultureInfo.InvariantCulture), value < 0 ? "exact" : "atLeast");
                    return;
                case "slmult":
                    if (on && p.GetValueOrDefault("lineRule") == "atLeast")
                        p["lineRule"] = "auto";
                    return;
                case "keep": p["keepLines"] = V(on); return;
                case "keepn": p["keepNext"] = V(on); return;
                case "pagebb": p["pageBreakBefore"] = V(on); return;
                case "contextualspace": p["contextual"] = V(on); return;
                case "rtlpar": p["bidi"] = "1"; return;
                case "ltrpar": p["bidi"] = "0"; return;
                case "ls": p["list"] = value > 0 ? "1" : "0"; return;
                case "cbpat": p["shading"] = Color(value); return;
                case "tx" or "tb":
                    var tabs = p.GetValueOrDefault("tabs", "");
                    var kind = word == "tb" ? "bar" : pendingTabKind;
                    p["tabs"] = (tabs.Length > 0 ? tabs + "," : "") + $"{kind}:{value}:{pendingLeader}";
                    (pendingTabKind, pendingLeader) = ("left", "none");
                    return;
                case "tqr": pendingTabKind = "right"; return;
                case "tqc": pendingTabKind = "center"; return;
                case "tqdec": pendingTabKind = "decimal"; return;
                case "tldot": pendingLeader = "dot"; return;
                case "tlhyph": pendingLeader = "hyphen"; return;
                case "tlul": pendingLeader = "underscore"; return;
                case "tlth": pendingLeader = "heavy"; return;
                case "tlmdot": pendingLeader = "middleDot"; return;
            }
        }

        string pendingTabKind = "left", pendingLeader = "none";

        /// <summary>The highlight Word names nearest a color.</summary>
        static string Highlight(string hex)
        {
            if (hex.Length != 6)
                return "none";
            (string Name, int R, int G, int B)[] names =
            [
                ("black", 0, 0, 0), ("blue", 0, 0, 255), ("cyan", 0, 255, 255), ("green", 0, 255, 0), ("magenta", 255, 0, 255), ("red", 255, 0, 0),
                ("yellow", 255, 255, 0), ("white", 255, 255, 255), ("darkBlue", 0, 0, 128), ("darkCyan", 0, 128, 128), ("darkGreen", 0, 128, 0),
                ("darkMagenta", 128, 0, 128), ("darkRed", 128, 0, 0), ("darkYellow", 128, 128, 0), ("darkGray", 128, 128, 128), ("lightGray", 192, 192, 192),
            ];
            var (r, g, b) = (System.Convert.ToInt32(hex[..2], 16), System.Convert.ToInt32(hex[2..4], 16), System.Convert.ToInt32(hex[4..], 16));
            return names.MinBy(n => (n.R - r) * (n.R - r) + (n.G - g) * (n.G - g) + (n.B - b) * (n.B - b)).Name;
        }

        /// <summary>Skips the fallback characters after \u: plain characters, \'hh escapes, whole control words.</summary>
        void SkipFallback(int count)
        {
            for (var k = 0; k < count && position < rtf.Length; k++)
            {
                var c = rtf[position];
                if (c is '{' or '}')
                    return;
                if (c == '\\')
                {
                    if (position + 1 < rtf.Length && rtf[position + 1] == '\'')
                        position += 4;
                    else
                    {
                        position++;
                        while (position < rtf.Length && char.IsAsciiLetter(rtf[position]))
                            position++;
                        while (position < rtf.Length && (char.IsAsciiDigit(rtf[position]) || rtf[position] == '-'))
                            position++;
                        if (position < rtf.Length && rtf[position] == ' ')
                            position++;
                    }
                }
                else
                    position++;
            }
        }

        /// <summary>Bytes of \'hh escapes and 8-bit characters, decoded in the current font's code page.</summary>
        void FlushBytes()
        {
            if (pendingBytes.Count == 0)
                return;
            var bytes = pendingBytes.ToArray();
            pendingBytes.Clear();
            var charset = state.Char.TryGetValue("fontNumber", out var n) && int.TryParse(n, out var number) && fonts.TryGetValue(number, out var font) ? font.Charset
                : fonts.TryGetValue(defaultFont, out var deff) ? deff.Charset : 0;
            if (charset == 2)
            {
                // Symbol fonts: their characters in the private use area, as Word stores them.
                Text(string.Concat(bytes.Select(b => (char)(0xF000 + b))));
                return;
            }
            var page = charset switch
            {
                128 => 932, 129 => 949, 130 => 1361, 134 => 936, 136 => 950, 161 => 1253, 162 => 1254, 163 => 1258, 177 => 1255, 178 => 1256,
                186 => 1257, 204 => 1251, 222 => 874, 238 => 1250, 77 => 10000, _ => codePage,
            };
            Text(Decode(bytes, page));
        }

        static string Decode(byte[] bytes, int page)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(page).GetString(bytes);
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException)
            {
                return Encoding.Latin1.GetString(bytes);
            }
        }

        void Text(string text)
        {
            switch (state.Destination)
            {
                case Destination.Body:
                    pieces.Add(new Piece("text", text, new Dictionary<string, string>(state.Char)));
                    break;
                case Destination.Label:
                    label.Add(new Piece("text", text, new Dictionary<string, string>(state.Char)));
                    break;
                case Destination.Font or Destination.Style:
                    destinationText.Append(text);
                    break;
                case Destination.ColorTable:
                    foreach (var _ in text.Where(ch => ch == ';'))
                    {
                        colors.Add(color.Set ? $"{color.R:X2}{color.G:X2}{color.B:X2}" : "auto");
                        color = default;
                    }
                    break;
                case Destination.FieldInstruction:
                    fieldInstruction.Append(text);
                    break;
            }
        }

        void Special(string kind)
        {
            if (state.Destination == Destination.Body)
                pieces.Add(new Piece(kind, "", new Dictionary<string, string>(state.Char)));
            else if (state.Destination == Destination.Label && kind == "tab")
                label.Add(new Piece("tab", "", new Dictionary<string, string>(state.Char)));
        }

        void Picture()
        {
            if (pictureHex.Length < 2 || !pictureProps.TryGetValue("type", out var type))
                return;
            var hex = pictureHex.ToString();
            var bytes = new byte[hex.Length / 2];
            for (var k = 0; k < bytes.Length; k++)
                bytes[k] = (byte)((Hex(hex[2 * k]) << 4) | Hex(hex[2 * k + 1]));
            static int Hex(char c) => c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;
            double Twips(string key) => pictureProps.TryGetValue(key, out var v) && double.TryParse(v, CultureInfo.InvariantCulture, out var d) ? d : 0;
            var (width, height) = (Twips("picwgoal") / 20, Twips("pichgoal") / 20);
            if (width <= 0 || height <= 0)
            {
                // Pixels of a bitmap at 96 dpi; a metafile's size is in hundredths of millimeters.
                var metafile = type is "emf" or "wmf";
                (width, height) = metafile ? (Twips("picw") / 2540 * 72, Twips("pich") / 2540 * 72) : (Twips("picw") * 0.75, Twips("pich") * 0.75);
            }
            var (sx, sy) = (pictureProps.ContainsKey("picscalex") ? Twips("picscalex") / 100 : 1, pictureProps.ContainsKey("picscaley") ? Twips("picscaley") / 100 : 1);
            width = width * sx - (Twips("piccropl") + Twips("piccropr")) / 20 * sx;
            height = height * sy - (Twips("piccropt") + Twips("piccropb")) / 20 * sy;
            if (width <= 0 || height <= 0)
                return;
            var mime = type switch { "png" => "image/png", "jpeg" => "image/jpeg", "emf" => "image/x-emf", _ => "image/x-wmf" };
            var uri = $"data:{mime};base64,{System.Convert.ToBase64String(bytes)}";
            pieces.Add(new Piece("picture", $"{uri}|{width.ToString(CultureInfo.InvariantCulture)}|{height.ToString(CultureInfo.InvariantCulture)}", new Dictionary<string, string>(state.Char)));
        }

        /// <summary>A paragraph mark: the paragraph ends, in a cell where it is in a table.</summary>
        void Paragraph()
        {
            if (state.Destination != Destination.Body)
                return;
            EndParagraph();
        }

        void EndParagraph(bool cell = false)
        {
            if (state.Destination != Destination.Body && !cell)
                return;
            var paragraph = Build();
            pieces.Clear();
            label.Clear();
            if (state.InTable || cell)
                cellContent.Add(paragraph);
            else
            {
                EndTable();
                Blocks.Add(paragraph);
            }
        }

        /// <summary>The source style of a paragraph: its stylesheet entry, or Word's Normal without a stylesheet.</summary>
        (Dictionary<string, string> Char, Dictionary<string, string> Para, string Name) SourceStyle(int number)
        {
            var baseChar = new Dictionary<string, string>(CharDefaults) { ["font"] = fonts.TryGetValue(defaultFont, out var f) ? f.Name : "Times New Roman" };
            var basePara = new Dictionary<string, string>(ParaDefaults);
            if (defaultChar != null || defaultPara != null)
            {
                foreach (var (k, v) in defaultChar ?? []) baseChar[k] = v;
                foreach (var (k, v) in defaultPara ?? []) basePara[k] = v;
            }
            if (!hasStyleSheet || !styles.ContainsKey(number))
            {
                if (!hasStyleSheet && defaultChar == null && defaultPara == null)
                {
                    // Word's own Normal: Aptos 12 pt, 8 pt after, 1.16 lines.
                    baseChar["font"] = "Aptos";
                    (basePara["after"], basePara["line"], basePara["lineRule"]) = ("160", "278", "auto");
                }
                return (baseChar, basePara, "Normal");
            }
            var chain = new List<RtfStyle>();
            for (int? n = number; n is { } k && styles.TryGetValue(k, out var s) && !chain.Contains(s) && chain.Count < 20; n = s.BasedOn)
                chain.Insert(0, s);
            foreach (var s in chain)
            {
                foreach (var (k, v) in s.Char) baseChar[k] = v;
                foreach (var (k, v) in s.Para) basePara[k] = v;
            }
            return (baseChar, basePara, styles[number].Name is { Length: > 0 } name ? name : "Normal");
        }

        XElement Build()
        {
            var (styleChar, stylePara, styleName) = SourceStyle(state.Style);
            // A style the document has stays its own; one it lacks comes along with all the RTF gives it. Of the
            // paragraph's formatting what differs from the RTF's definition of its style is direct formatting.
            var targetStyle = styleIdByName(styleName);
            if (targetStyle == null && !imported.TryGetValue(styleName, out targetStyle))
            {
                var stylePPr = ParagraphProperties(stylePara.Where(p => p.Key != "list").ToDictionary(), null);
                targetStyle = imported[styleName] = addStyle(styleName, stylePPr, RunProperties(styleChar.Where(p => p.Key != "fontNumber").ToDictionary()));
            }
            Dictionary<string, string> Differences(Dictionary<string, string> current, Dictionary<string, string> reference) =>
                Resolved(current).Where(p => p.Key != "fontNumber" && p.Key != "list" && reference.GetValueOrDefault(p.Key, DefaultOf(p.Key)) != p.Value).ToDictionary();
            static string DefaultOf(string key) => CharDefaults.GetValueOrDefault(key) ?? ParaDefaults.GetValueOrDefault(key) ?? "";
            var paragraph = new XElement(W + "p");
            var pPr = ParagraphProperties(Differences(state.Para, stylePara), targetStyle);
            var content = (state.Para.GetValueOrDefault("list") == "1" ? label : []).Concat(pieces).ToList();
            // The paragraph mark takes the formatting of the last run.
            var markChar = content.LastOrDefault()?.Char ?? state.Char;
            var markRun = RunProperties(Differences(markChar, styleChar));
            if (markRun.HasElements)
                pPr.Add(markRun);
            if (pPr.HasElements)
                paragraph.Add(pPr);
            foreach (var piece in content)
            {
                var rPr = RunProperties(Differences(piece.Char, styleChar));
                XElement Run(params object[] items) => new(W + "r", rPr.HasElements ? new XElement(rPr) : null, items);
                switch (piece.Kind)
                {
                    case "text":
                        paragraph.Add(Run(new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), piece.Text)));
                        break;
                    case "tab":
                        paragraph.Add(Run(new XElement(W + "tab")));
                        break;
                    case "br":
                        paragraph.Add(Run(new XElement(W + "br")));
                        break;
                    case "page" or "column":
                        paragraph.Add(Run(new XElement(W + "br", new XAttribute(W + "type", piece.Kind))));
                        break;
                    case "fieldBegin":
                        paragraph.Add(Run(new XElement(W + "fldChar", new XAttribute(W + "fldCharType", "begin"))));
                        paragraph.Add(Run(new XElement(W + "instrText", new XAttribute(XNamespace.Xml + "space", "preserve"), " " + piece.Text + " ")));
                        break;
                    case "fieldSeparate":
                        paragraph.Add(Run(new XElement(W + "fldChar", new XAttribute(W + "fldCharType", "separate"))));
                        break;
                    case "fieldEnd":
                        paragraph.Add(Run(new XElement(W + "fldChar", new XAttribute(W + "fldCharType", "end"))));
                        break;
                    case "picture":
                        var parts = piece.Text.Split('|');
                        paragraph.Add(Run(Drawing(parts[0], double.Parse(parts[1], CultureInfo.InvariantCulture), double.Parse(parts[2], CultureInfo.InvariantCulture))));
                        break;
                }
            }
            // Adjacent runs of one formatting become one.
            Merge(paragraph);
            return paragraph;
        }

        static void Merge(XElement paragraph)
        {
            XElement? previous = null;
            foreach (var run in paragraph.Elements(W + "r").ToList())
            {
                if (previous != null && run.Elements().Count(e => e.Name != W + "rPr") == 1 && run.Element(W + "t") is { } t
                    && previous.Elements().Count(e => e.Name != W + "rPr") == 1 && previous.Element(W + "t") is { } pt
                    && XNode.DeepEquals(run.Element(W + "rPr"), previous.Element(W + "rPr")) && previous.NextNode == run)
                {
                    pt.Value += t.Value;
                    run.Remove();
                    continue;
                }
                previous = run;
            }
        }

        static int Emu(double points) => (int)Math.Round(points * 12700);

        static XElement Drawing(string uri, double width, double height)
        {
            XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
            XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
            XNamespace pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
            XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            return new XElement(W + "drawing", new XElement(wp + "inline",
                new XElement(wp + "extent", new XAttribute("cx", Emu(width)), new XAttribute("cy", Emu(height))),
                new XElement(wp + "effectExtent", new XAttribute("l", 0), new XAttribute("t", 0), new XAttribute("r", 0), new XAttribute("b", 0)),
                new XElement(wp + "docPr", new XAttribute("id", 1), new XAttribute("name", "Picture")),
                new XElement(a + "graphic", new XElement(a + "graphicData", new XAttribute("uri", pic.NamespaceName),
                    new XElement(pic + "pic",
                        new XElement(pic + "nvPicPr", new XElement(pic + "cNvPr", new XAttribute("id", 0), new XAttribute("name", "")), new XElement(pic + "cNvPicPr")),
                        new XElement(pic + "blipFill", new XElement(a + "blip", new XAttribute(r + "embed", uri)), new XElement(a + "stretch", new XElement(a + "fillRect"))),
                        new XElement(pic + "spPr",
                            new XElement(a + "xfrm", new XElement(a + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
                                new XElement(a + "ext", new XAttribute("cx", Emu(width)), new XAttribute("cy", Emu(height)))),
                            new XElement(a + "prstGeom", new XAttribute("prst", "rect"), new XElement(a + "avLst"))))))));
        }

        static XElement ParagraphProperties(Dictionary<string, string> p, string? style)
        {
            var pPr = new XElement(W + "pPr");
            if (style != null)
                pPr.Add(new XElement(W + "pStyle", new XAttribute(W + "val", style)));
            void Flag(string key, string element)
            {
                if (p.TryGetValue(key, out var v))
                    pPr.Add(new XElement(W + element, new XAttribute(W + "val", v == "1" ? "1" : "0")));
            }
            Flag("keepNext", "keepNext");
            Flag("keepLines", "keepLines");
            Flag("pageBreakBefore", "pageBreakBefore");
            if (p.TryGetValue("borders", out var borders) && borders.Length > 0)
            {
                var all = Parse(borders);
                var pBdr = new XElement(W + "pBdr");
                foreach (var side in new[] { "top", "left", "bottom", "right", "between" })
                    if (all.TryGetValue($"{side}.type", out var type))
                        pBdr.Add(new XElement(W + side, new XAttribute(W + "val", type),
                            new XAttribute(W + "sz", Math.Max(2, (int)Math.Round(double.Parse(all.GetValueOrDefault($"{side}.width", "15"), CultureInfo.InvariantCulture) / 2.5))),
                            new XAttribute(W + "space", (int)Math.Round(double.Parse(all.GetValueOrDefault($"{side}.space", "0"), CultureInfo.InvariantCulture) / 20)),
                            new XAttribute(W + "color", all.GetValueOrDefault($"{side}.color", "auto"))));
                if (pBdr.HasElements)
                    pPr.Add(pBdr);
            }
            if (p.TryGetValue("shading", out var shading))
                pPr.Add(new XElement(W + "shd", new XAttribute(W + "val", "clear"), new XAttribute(W + "color", "auto"), new XAttribute(W + "fill", shading == "none" ? "auto" : shading)));
            if (p.TryGetValue("tabs", out var tabs))
            {
                var element = new XElement(W + "tabs");
                foreach (var tab in tabs.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Split(':')).Where(t => t.Length == 3))
                    element.Add(new XElement(W + "tab", new XAttribute(W + "val", tab[0]), new XAttribute(W + "leader", tab[2]), new XAttribute(W + "pos", tab[1])));
                pPr.Add(element);
            }
            var spacing = new XElement(W + "spacing");
            if (p.TryGetValue("before", out var before)) spacing.Add(new XAttribute(W + "before", before));
            if (p.TryGetValue("beforeAuto", out var beforeAuto)) spacing.Add(new XAttribute(W + "beforeAutospacing", beforeAuto));
            if (p.TryGetValue("after", out var after)) spacing.Add(new XAttribute(W + "after", after));
            if (p.TryGetValue("afterAuto", out var afterAuto)) spacing.Add(new XAttribute(W + "afterAutospacing", afterAuto));
            if (p.ContainsKey("line") || p.ContainsKey("lineRule"))
            {
                spacing.Add(new XAttribute(W + "line", p.GetValueOrDefault("line", "240")));
                spacing.Add(new XAttribute(W + "lineRule", p.GetValueOrDefault("lineRule", "auto")));
            }
            if (spacing.HasAttributes)
                pPr.Add(spacing);
            var ind = new XElement(W + "ind");
            if (p.TryGetValue("left", out var left)) ind.Add(new XAttribute(W + "left", left));
            if (p.TryGetValue("right", out var right)) ind.Add(new XAttribute(W + "right", right));
            if (p.TryGetValue("firstLine", out var first))
            {
                var value = int.Parse(first, CultureInfo.InvariantCulture);
                ind.Add(value < 0 ? new XAttribute(W + "hanging", -value) : new XAttribute(W + "firstLine", value));
            }
            if (ind.HasAttributes)
                pPr.Add(ind);
            Flag("contextual", "contextualSpacing");
            Flag("bidi", "bidi");
            if (p.TryGetValue("jc", out var jc))
                pPr.Add(new XElement(W + "jc", new XAttribute(W + "val", jc)));
            return pPr;
        }

        static XElement RunProperties(Dictionary<string, string> c)
        {
            var rPr = new XElement(W + "rPr");
            if (c.TryGetValue("font", out var font))
                rPr.Add(new XElement(W + "rFonts", new XAttribute(W + "ascii", font), new XAttribute(W + "hAnsi", font), new XAttribute(W + "cs", font), new XAttribute(W + "eastAsia", font)));
            void Flag(string key, string element)
            {
                if (c.TryGetValue(key, out var v))
                    rPr.Add(new XElement(W + element, new XAttribute(W + "val", v == "1" ? "1" : "0")));
            }
            Flag("b", "b");
            Flag("b", "bCs");
            Flag("i", "i");
            Flag("i", "iCs");
            Flag("caps", "caps");
            Flag("smallCaps", "smallCaps");
            Flag("strike", "strike");
            Flag("dstrike", "dstrike");
            Flag("outline", "outline");
            Flag("shadow", "shadow");
            Flag("emboss", "emboss");
            Flag("imprint", "imprint");
            Flag("vanish", "vanish");
            if (c.TryGetValue("color", out var color))
                rPr.Add(new XElement(W + "color", new XAttribute(W + "val", color)));
            if (c.TryGetValue("spacing", out var spacing))
                rPr.Add(new XElement(W + "spacing", new XAttribute(W + "val", spacing)));
            if (c.TryGetValue("w", out var scale))
                rPr.Add(new XElement(W + "w", new XAttribute(W + "val", scale)));
            if (c.TryGetValue("position", out var position))
                rPr.Add(new XElement(W + "position", new XAttribute(W + "val", position)));
            if (c.TryGetValue("size", out var size))
                rPr.Add(new XElement(W + "sz", new XAttribute(W + "val", size)), new XElement(W + "szCs", new XAttribute(W + "val", size)));
            if (c.TryGetValue("highlight", out var highlight))
                rPr.Add(new XElement(W + "highlight", new XAttribute(W + "val", highlight)));
            if (c.TryGetValue("u", out var underline))
                rPr.Add(new XElement(W + "u", new XAttribute(W + "val", underline)));
            if (c.TryGetValue("shd", out var shd))
                rPr.Add(new XElement(W + "shd", new XAttribute(W + "val", "clear"), new XAttribute(W + "color", "auto"), new XAttribute(W + "fill", shd == "none" ? "auto" : shd)));
            if (c.TryGetValue("vertAlign", out var vertAlign))
                rPr.Add(new XElement(W + "vertAlign", new XAttribute(W + "val", vertAlign)));
            return rPr;
        }

        /// <summary>Closes the table being read: its rows with their cells, widths from the cell boundaries.</summary>
        void EndTable()
        {
            if (cellContent.Count > 0 || rowContent.Count > 0)
            {
                if (cellContent.Count > 0)
                    rowContent.Add(cellContent);
                rows.Add((rowCells, rowProps, rowContent));
                (cellContent, rowContent) = ([], []);
            }
            if (rows.Count == 0)
                return;
            var first = rows[0];
            int Twips(Dictionary<string, string> props, string key, int fallback) =>
                props.TryGetValue(key, out var v) && int.TryParse(v, CultureInfo.InvariantCulture, out var n) ? n : fallback;
            var gap = Twips(first.Row, "gap", 108);
            var leftEdge = Twips(first.Row, "left", 0);
            var (padL, padR) = (Twips(first.Row, "padL", gap), Twips(first.Row, "padR", gap));
            var boundaries = rows.SelectMany(r => r.Cells.Select(c => c.Right)).Append(leftEdge).Distinct().Order().ToList();
            var table = new XElement(W + "tbl");
            var tblPr = new XElement(W + "tblPr",
                new XElement(W + "tblW", new XAttribute(W + "w", 0), new XAttribute(W + "type", "auto")),
                first.Row.TryGetValue("jc", out var jc) ? new XElement(W + "jc", new XAttribute(W + "val", jc)) : null,
                new XElement(W + "tblInd", new XAttribute(W + "w", leftEdge), new XAttribute(W + "type", "dxa")),
                Borders(W + "tblBorders", first.Row),
                new XElement(W + "tblLayout", new XAttribute(W + "type", "fixed")),
                new XElement(W + "tblCellMar",
                    new XElement(W + "top", new XAttribute(W + "w", Twips(first.Row, "padT", 0)), new XAttribute(W + "type", "dxa")),
                    new XElement(W + "left", new XAttribute(W + "w", padL), new XAttribute(W + "type", "dxa")),
                    new XElement(W + "bottom", new XAttribute(W + "w", Twips(first.Row, "padB", 0)), new XAttribute(W + "type", "dxa")),
                    new XElement(W + "right", new XAttribute(W + "w", padR), new XAttribute(W + "type", "dxa"))));
            table.Add(tblPr);
            table.Add(new XElement(W + "tblGrid", boundaries.Zip(boundaries.Skip(1)).Select(b => new XElement(W + "gridCol", new XAttribute(W + "w", b.Second - b.First)))));
            foreach (var (cells, rowProperties, content) in rows)
            {
                var tr = new XElement(W + "tr");
                var trPr = new XElement(W + "trPr");
                if (rowProperties.TryGetValue("height", out var h) && int.TryParse(h, out var height) && height != 0)
                    trPr.Add(new XElement(W + "trHeight", new XAttribute(W + "val", Math.Abs(height)), new XAttribute(W + "hRule", height < 0 ? "exact" : "atLeast")));
                if (rowProperties.ContainsKey("header"))
                    trPr.Add(new XElement(W + "tblHeader"));
                if (trPr.HasElements)
                    tr.Add(trPr);
                var previous = Twips(rowProperties, "left", leftEdge);
                for (var k = 0; k < cells.Count; k++)
                {
                    var cell = cells[k];
                    if (cell.Props.GetValueOrDefault("hMerge") == "continue")
                    {
                        previous = cell.Right;
                        continue;
                    }
                    // A merged run of cells spans their columns.
                    var right = cell.Right;
                    var next = k + 1;
                    while (next < cells.Count && cells[next].Props.GetValueOrDefault("hMerge") == "continue")
                        right = cells[next++].Right;
                    var span = boundaries.Count(b => b > previous && b <= right);
                    var tcPr = new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", right - previous), new XAttribute(W + "type", "dxa")));
                    if (span > 1)
                        tcPr.Add(new XElement(W + "gridSpan", new XAttribute(W + "val", span)));
                    if (cell.Props.TryGetValue("vMerge", out var vMerge))
                        tcPr.Add(new XElement(W + "vMerge", vMerge == "restart" ? new XAttribute(W + "val", "restart") : null));
                    if (Borders(W + "tcBorders", cell.Props) is { } tcBorders)
                        tcPr.Add(tcBorders);
                    if (cell.Props.TryGetValue("fill", out var fill) && fill != "auto")
                        tcPr.Add(new XElement(W + "shd", new XAttribute(W + "val", "clear"), new XAttribute(W + "color", "auto"), new XAttribute(W + "fill", fill)));
                    if (cell.Props.TryGetValue("vAlign", out var vAlign))
                        tcPr.Add(new XElement(W + "vAlign", new XAttribute(W + "val", vAlign)));
                    var tc = new XElement(W + "tc", tcPr);
                    var paragraphs = k < content.Count ? content[k] : [];
                    tc.Add(paragraphs.Count > 0 ? paragraphs : [new XElement(W + "p")]);
                    tr.Add(tc);
                    previous = right;
                    k = next - 1;
                }
                table.Add(tr);
            }
            Blocks.Add(table);
            rows.Clear();
        }

        static XElement? Borders(XName name, Dictionary<string, string> props)
        {
            var element = new XElement(name);
            foreach (var side in new[] { "top", "left", "bottom", "right", "insideH", "insideV" })
            {
                if (!props.TryGetValue($"border.{side}.type", out var type))
                    continue;
                var width = props.TryGetValue($"border.{side}.width", out var w) && int.TryParse(w, out var tw) ? tw : 15;
                element.Add(new XElement(W + side, new XAttribute(W + "val", type), new XAttribute(W + "sz", Math.Max(2, (int)Math.Round(width / 2.5))),
                    new XAttribute(W + "space", 0), new XAttribute(W + "color", props.GetValueOrDefault($"border.{side}.color", "auto"))));
            }
            return element.HasElements ? element : null;
        }
    }
}
