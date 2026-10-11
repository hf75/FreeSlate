using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Fidelity.Word;

/// <summary>A glyph Word placed on a page: its text, baseline origin from the page's top left and its advance (points).</summary>
public sealed record PdfGlyph(int Page, string Text, double X, double Y, double Advance, double Size, string Font, string Color)
{
    /// <summary>The glyph's code in its PDF font: the glyph id for Word's Identity-H fonts, the character code otherwise.</summary>
    public int Code { get; init; }
}

/// <summary>A filled or stroked rectangle on a page (underlines, borders, shading), from the page's top left in points.</summary>
public sealed record PdfRect(int Page, double X, double Y, double Width, double Height, bool Filled, string Color);

/// <summary>A stroked straight line on a page (borders, rules, column separators), from the page's top left in points.</summary>
public sealed record PdfSegment(int Page, double X0, double Y0, double X1, double Y1, double Width, string Color);

/// <summary>The size of a page in points.</summary>
public sealed record PdfPage(int Number, double Width, double Height);

/// <summary>
/// Reads where Word put every glyph and rectangle in its PDF export of a document: a measuring instrument for Word's
/// layout, as COM values are for PowerPoint's. It understands what Word writes (objects in the clear, Flate streams,
/// Type0 fonts with Identity-H encoding and ToUnicode maps, simple TrueType fonts in WinAnsi encoding, text in BT/ET
/// with Tm and TJ) and no more.
/// </summary>
public sealed class PdfText
{
    readonly byte[] data;
    readonly Dictionary<int, int> offsets = new();
    readonly Dictionary<int, object?> cache = new();

    public List<PdfPage> Pages { get; } = [];
    public List<PdfGlyph> Glyphs { get; } = [];
    public List<PdfRect> Rects { get; } = [];
    public List<PdfSegment> Segments { get; } = [];

    /// <summary>Drawn XObjects (pictures and forms): the unit square under the current transformation, on the page.</summary>
    public List<PdfRect> Objects { get; } = [];

    static PdfText() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public PdfText(string path)
    {
        data = File.ReadAllBytes(path);
        var text = Encoding.Latin1.GetString(data);
        // Objects begin lines; matches inside compressed streams would not.
        foreach (Match m in Regex.Matches(text, @"(?<=^|[\r\n])(\d+)\s+0\s+obj\b"))
            offsets[int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)] = m.Index + m.Length;
        var catalog = offsets.Keys.Select(Object).OfType<Dictionary<string, object?>>().FirstOrDefault(d => Name(d, "Type") == "Catalog")
                      ?? throw new InvalidDataException("No catalog.");
        var number = 0;
        foreach (var page in PageObjects(Resolve(catalog["Pages"]) as Dictionary<string, object?>))
            ReadPage(page, ++number);
    }

    IEnumerable<Dictionary<string, object?>> PageObjects(Dictionary<string, object?>? node)
    {
        if (node == null)
            yield break;
        if (Name(node, "Type") == "Page")
        {
            yield return node;
            yield break;
        }
        if (Resolve(node.GetValueOrDefault("Kids")) is List<object?> kids)
        {
            foreach (var kid in kids)
            {
                foreach (var page in PageObjects(Resolve(kid) as Dictionary<string, object?>))
                    yield return page;
            }
        }
    }

    // ---- objects -------------------------------------------------------------------------------------------------

    sealed record Ref(int Number);
    sealed record Name_(string Value);
    sealed record Stream(Dictionary<string, object?> Dict, int Start, int Length);

    object? Object(int number)
    {
        if (cache.TryGetValue(number, out var known))
            return known;
        if (!offsets.TryGetValue(number, out var at))
            return null;
        var lexer = new Lexer(data, at);
        var value = lexer.Value();
        // A stream follows its dictionary.
        if (value is Dictionary<string, object?> dict && lexer.PeekKeyword() == "stream")
        {
            lexer.Keyword();
            var start = lexer.Position;
            if (data[start] == '\r') start++;
            if (data[start] == '\n') start++;
            var length = Convert.ToInt32(Resolve(dict.GetValueOrDefault("Length")) ?? 0, CultureInfo.InvariantCulture);
            value = new Stream(dict, start, length);
        }
        cache[number] = value;
        return value;
    }

    object? Resolve(object? value) => value is Ref r ? Object(r.Number) : value;

    static string? Name(Dictionary<string, object?> dict, string key) => dict.GetValueOrDefault(key) is Name_ n ? n.Value : null;

    byte[] Decode(Stream stream)
    {
        var raw = data.AsSpan(stream.Start, Math.Min(stream.Length, data.Length - stream.Start)).ToArray();
        var filter = Resolve(stream.Dict.GetValueOrDefault("Filter"));
        var filters = filter switch
        {
            Name_ n => [n.Value],
            List<object?> list => list.OfType<Name_>().Select(n => n.Value).ToList(),
            _ => new List<string>(),
        };
        foreach (var name in filters)
        {
            if (name != "FlateDecode")
                throw new NotSupportedException("PDF filter " + name);
            using var input = new ZLibStream(new MemoryStream(raw), CompressionMode.Decompress);
            using var output = new MemoryStream();
            input.CopyTo(output);
            raw = output.ToArray();
        }
        return raw;
    }

    // ---- fonts ---------------------------------------------------------------------------------------------------

    sealed class Font
    {
        public required string Name { get; init; }
        public bool TwoByte { get; init; }
        public Dictionary<int, string> Unicode { get; } = new();
        public Dictionary<int, double> Widths { get; } = new();
        public double DefaultWidth { get; init; } = 1000;
        public double Width(int code) => Widths.TryGetValue(code, out var w) ? w : DefaultWidth;
    }

    readonly Dictionary<int, Font> fonts = new();

    Font LoadFont(object? reference)
    {
        var key = reference is Ref r ? r.Number : -1;
        if (key >= 0 && fonts.TryGetValue(key, out var known))
            return known;
        var dict = Resolve(reference) as Dictionary<string, object?> ?? [];
        var baseName = Name(dict, "BaseFont") ?? "?";
        // Subset fonts are named ABCDEF+Family.
        if (baseName.Length > 7 && baseName[6] == '+')
            baseName = baseName[7..];
        Font font;
        if (Name(dict, "Subtype") == "Type0")
        {
            var descendant = Resolve((Resolve(dict.GetValueOrDefault("DescendantFonts")) as List<object?>)?.FirstOrDefault()) as Dictionary<string, object?> ?? [];
            font = new Font
            {
                Name = baseName,
                TwoByte = true,
                DefaultWidth = Convert.ToDouble(Resolve(descendant.GetValueOrDefault("DW")) ?? 1000, CultureInfo.InvariantCulture),
            };
            // W: c [w1 w2 …] or c_first c_last w.
            if (Resolve(descendant.GetValueOrDefault("W")) is List<object?> w)
            {
                for (var i = 0; i < w.Count;)
                {
                    var first = Convert.ToInt32(Resolve(w[i]), CultureInfo.InvariantCulture);
                    if (Resolve(w[i + 1]) is List<object?> widths)
                    {
                        for (var k = 0; k < widths.Count; k++)
                            font.Widths[first + k] = Convert.ToDouble(Resolve(widths[k]), CultureInfo.InvariantCulture);
                        i += 2;
                    }
                    else
                    {
                        var last = Convert.ToInt32(Resolve(w[i + 1]), CultureInfo.InvariantCulture);
                        var width = Convert.ToDouble(Resolve(w[i + 2]), CultureInfo.InvariantCulture);
                        for (var c = first; c <= last; c++)
                            font.Widths[c] = width;
                        i += 3;
                    }
                }
            }
        }
        else
        {
            font = new Font { Name = baseName, TwoByte = false };
            var firstChar = Convert.ToInt32(Resolve(dict.GetValueOrDefault("FirstChar")) ?? 0, CultureInfo.InvariantCulture);
            if (Resolve(dict.GetValueOrDefault("Widths")) is List<object?> widths)
            {
                for (var k = 0; k < widths.Count; k++)
                    font.Widths[firstChar + k] = Convert.ToDouble(Resolve(widths[k]), CultureInfo.InvariantCulture);
            }
            var ansi = Encoding.GetEncoding(1252, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
            for (var c = 32; c < 256; c++)
                font.Unicode[c] = ansi.GetString([(byte)c]);
        }
        if (Resolve(dict.GetValueOrDefault("ToUnicode")) is Stream toUnicode)
            ReadCMap(Encoding.Latin1.GetString(Decode(toUnicode)), font.Unicode);
        if (key >= 0)
            fonts[key] = font;
        return font;
    }

    static void ReadCMap(string cmap, Dictionary<int, string> map)
    {
        static string Utf16(string hex)
        {
            var bytes = Convert.FromHexString(hex.Length % 2 == 1 ? "0" + hex : hex);
            return Encoding.BigEndianUnicode.GetString(bytes);
        }
        foreach (Match block in Regex.Matches(cmap, @"beginbfchar(.*?)endbfchar", RegexOptions.Singleline))
        {
            foreach (Match m in Regex.Matches(block.Groups[1].Value, @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>"))
                map[Convert.ToInt32(m.Groups[1].Value, 16)] = Utf16(m.Groups[2].Value);
        }
        foreach (Match block in Regex.Matches(cmap, @"beginbfrange(.*?)endbfrange", RegexOptions.Singleline))
        {
            foreach (Match m in Regex.Matches(block.Groups[1].Value, @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*(<([0-9A-Fa-f]+)>|\[([^\]]*)\])"))
            {
                var low = Convert.ToInt32(m.Groups[1].Value, 16);
                var high = Convert.ToInt32(m.Groups[2].Value, 16);
                if (m.Groups[4].Success)
                {
                    var start = Utf16(m.Groups[4].Value);
                    for (var c = low; c <= high; c++)
                        map[c] = start[..^1] + (char)(start[^1] + (c - low));
                }
                else
                {
                    var targets = Regex.Matches(m.Groups[5].Value, @"<([0-9A-Fa-f]+)>").Select(t => Utf16(t.Groups[1].Value)).ToList();
                    for (var c = low; c <= high && c - low < targets.Count; c++)
                        map[c] = targets[c - low];
                }
            }
        }
    }

    // ---- pages ---------------------------------------------------------------------------------------------------

    void ReadPage(Dictionary<string, object?> page, int number)
    {
        var box = (Resolve(page.GetValueOrDefault("MediaBox")) as List<object?>)?.Select(v => Convert.ToDouble(Resolve(v), CultureInfo.InvariantCulture)).ToList()
                  ?? [0, 0, 612, 792];
        var height = box[3] - box[1];
        Pages.Add(new PdfPage(number, box[2] - box[0], height));
        var resources = Resolve(page.GetValueOrDefault("Resources")) as Dictionary<string, object?> ?? [];
        var fontRefs = Resolve(resources.GetValueOrDefault("Font")) as Dictionary<string, object?> ?? [];
        var contents = Resolve(page.GetValueOrDefault("Contents"));
        var streams = contents switch
        {
            Stream s => [s],
            List<object?> list => list.Select(Resolve).OfType<Stream>().ToList(),
            _ => new List<Stream>(),
        };
        var content = streams.SelectMany(Decode).ToArray();
        Interpret(content, number, height, fontRefs, box[0], box[1]);
    }

    /// <summary>Runs a content stream, recording glyphs and rectangles in page coordinates from the top left.</summary>
    void Interpret(byte[] content, int page, double height, Dictionary<string, object?> fontRefs, double originX, double originY)
    {
        var lexer = new Lexer(content, 0);
        var operands = new List<object?>();
        // Graphics state: current transformation matrix and colors; text state.
        var ctm = Matrix.Identity;
        // q saves the graphics state, the text state parameters included (Tc, Tw, Tz, TL, Tf, Ts).
        var stack = new Stack<(Matrix Ctm, string Fill, string Stroke, Font? Font, double Size, double CharSpacing, double WordSpacing, double Scale, double Leading, double Rise)>();
        string fill = "0 g", stroke = "0 G";
        Matrix tm = Matrix.Identity, tlm = Matrix.Identity;
        Font? font = null;
        double size = 0, charSpacing = 0, wordSpacing = 0, scale = 1, leading = 0, rise = 0;
        var path = new List<(double X, double Y, double W, double H)>();
        var segments = new List<(double X0, double Y0, double X1, double Y1)>();
        (double X, double Y) point = (0, 0);
        var lineWidth = 1.0;

        (double X, double Y) ToPage(double x, double y, Matrix m)
        {
            var (px, py) = m.Apply(x, y);
            var (dx, dy) = ctm.Apply(px, py);
            return (dx - originX, height - (dy - originY));
        }

        void Show(byte[] bytes)
        {
            if (font == null)
                return;
            var step = font.TwoByte ? 2 : 1;
            for (var i = 0; i + step - 1 < bytes.Length; i += step)
            {
                var code = font.TwoByte ? bytes[i] << 8 | bytes[i + 1] : bytes[i];
                var width = font.Width(code) / 1000;
                var (x, y) = ToPage(0, rise, tm);
                var text = font.Unicode.TryGetValue(code, out var u) ? u : "�";
                var advance = (width * size + charSpacing + (!font.TwoByte && code == 32 ? wordSpacing : 0)) * scale;
                Glyphs.Add(new PdfGlyph(page, text, x, y, advance * Math.Abs(tm.A * ctm.A), size * Math.Abs(tm.D * ctm.D), font.Name, fill) { Code = code });
                tm = Matrix.Translation(advance, 0).Times(tm);
            }
        }

        while (lexer.Next() is { } token)
        {
            if (token is not Lexer.Operator op)
            {
                operands.Add(token);
                continue;
            }
            double N(int i) => Convert.ToDouble(operands[i], CultureInfo.InvariantCulture);
            switch (op.Name)
            {
                case "q": stack.Push((ctm, fill, stroke, font, size, charSpacing, wordSpacing, scale, leading, rise)); break;
                case "Q": if (stack.Count > 0) (ctm, fill, stroke, font, size, charSpacing, wordSpacing, scale, leading, rise) = stack.Pop(); break;
                case "cm": ctm = new Matrix(N(0), N(1), N(2), N(3), N(4), N(5)).Times(ctm); break;
                case "g": case "rg": case "k": fill = string.Join(' ', operands.Select(o => Convert.ToString(o, CultureInfo.InvariantCulture))) + " " + op.Name; break;
                case "G": case "RG": case "K": stroke = string.Join(' ', operands.Select(o => Convert.ToString(o, CultureInfo.InvariantCulture))) + " " + op.Name; break;
                case "BT": tm = tlm = Matrix.Identity; break;
                case "Tf":
                    font = LoadFont(fontRefs.GetValueOrDefault(((Name_)operands[0]!).Value));
                    size = N(1);
                    break;
                case "Tc": charSpacing = N(0); break;
                case "Tw": wordSpacing = N(0); break;
                case "Tz": scale = N(0) / 100; break;
                case "TL": leading = N(0); break;
                case "Ts": rise = N(0); break;
                case "Tm": tm = tlm = new Matrix(N(0), N(1), N(2), N(3), N(4), N(5)); break;
                case "Td": tm = tlm = Matrix.Translation(N(0), N(1)).Times(tlm); break;
                case "TD": leading = -N(1); tm = tlm = Matrix.Translation(N(0), N(1)).Times(tlm); break;
                case "T*": tm = tlm = Matrix.Translation(0, -leading).Times(tlm); break;
                case "Tj": Show((byte[])operands[0]!); break;
                case "'":
                    tm = tlm = Matrix.Translation(0, -leading).Times(tlm);
                    Show((byte[])operands[0]!);
                    break;
                case "TJ":
                    foreach (var item in (List<object?>)operands[0]!)
                    {
                        if (item is byte[] bytes)
                            Show(bytes);
                        else
                            tm = Matrix.Translation(-Convert.ToDouble(item, CultureInfo.InvariantCulture) / 1000 * size * scale, 0).Times(tm);
                    }
                    break;
                case "re": path.Add((N(0), N(1), N(2), N(3))); break;
                case "m": point = (N(0), N(1)); break;
                case "l":
                    segments.Add((point.X, point.Y, N(0), N(1)));
                    point = (N(0), N(1));
                    break;
                case "w": lineWidth = N(0); break;
                case "f": case "F": case "f*": case "B": case "B*": case "S": case "s": case "b": case "b*":
                    foreach (var (x, y, w, h) in path)
                    {
                        var (x0, y0) = ToPage(x, y, Matrix.Identity);
                        var (x1, y1) = ToPage(x + w, y + h, Matrix.Identity);
                        var filled = op.Name is not ("S" or "s");
                        Rects.Add(new PdfRect(page, Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Abs(y1 - y0), filled, filled ? fill : stroke));
                    }
                    if (op.Name is "S" or "s" or "B" or "B*" or "b" or "b*")
                    {
                        foreach (var (sx0, sy0, sx1, sy1) in segments)
                        {
                            var (a0, b0) = ToPage(sx0, sy0, Matrix.Identity);
                            var (a1, b1) = ToPage(sx1, sy1, Matrix.Identity);
                            Segments.Add(new PdfSegment(page, a0, b0, a1, b1, lineWidth * Math.Abs(ctm.A), stroke));
                        }
                    }
                    path.Clear();
                    segments.Clear();
                    break;
                case "n": path.Clear(); segments.Clear(); break;
                case "Do":
                {
                    var (x0, y0) = ToPage(0, 0, Matrix.Identity);
                    var (x1, y1) = ToPage(1, 1, Matrix.Identity);
                    Objects.Add(new PdfRect(page, Math.Min(x0, x1), Math.Min(y0, y1), Math.Abs(x1 - x0), Math.Abs(y1 - y0), true, operands.Count > 0 ? Convert.ToString(operands[0], CultureInfo.InvariantCulture) ?? "" : ""));
                    break;
                }
            }
            operands.Clear();
        }
    }

    /// <summary>An affine matrix [a b c d e f] as in PDF: x' = a x + c y + e, y' = b x + d y + f.</summary>
    readonly record struct Matrix(double A, double B, double C, double D, double E, double F)
    {
        public static readonly Matrix Identity = new(1, 0, 0, 1, 0, 0);
        public static Matrix Translation(double x, double y) => new(1, 0, 0, 1, x, y);

        /// <summary>This transformation followed by <paramref name="m"/>.</summary>
        public Matrix Times(Matrix m) => new(
            A * m.A + B * m.C, A * m.B + B * m.D,
            C * m.A + D * m.C, C * m.B + D * m.D,
            E * m.A + F * m.C + m.E, E * m.B + F * m.D + m.F);

        public (double X, double Y) Apply(double x, double y) => (A * x + C * y + E, B * x + D * y + F);
    }

    /// <summary>A PDF tokenizer for objects and content streams.</summary>
    sealed class Lexer(byte[] data, int position)
    {
        public sealed record Operator(string Name);

        public int Position => position;

        static bool IsWhite(byte b) => b is 0 or 9 or 10 or 12 or 13 or 32;
        static bool IsDelimiter(byte b) => b is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

        void SkipWhite()
        {
            while (position < data.Length)
            {
                if (IsWhite(data[position]))
                    position++;
                else if (data[position] == '%')
                {
                    while (position < data.Length && data[position] is not (10 or 13))
                        position++;
                }
                else
                    break;
            }
        }

        public string? PeekKeyword()
        {
            var save = position;
            var token = Next();
            position = save;
            return token is Operator op ? op.Name : null;
        }

        public void Keyword() => Next();

        /// <summary>The next complete value (objects: numbers, references, names, strings, arrays, dictionaries).</summary>
        public object? Value()
        {
            var token = Next();
            return token is Operator { Name: "null" } ? null : token;
        }

        /// <summary>The next token: a value or an operator; null at the end.</summary>
        public object? Next()
        {
            SkipWhite();
            if (position >= data.Length)
                return null;
            var b = data[position];
            switch (b)
            {
                case (byte)'/':
                {
                    var start = ++position;
                    while (position < data.Length && !IsWhite(data[position]) && !IsDelimiter(data[position]))
                        position++;
                    var raw = Encoding.Latin1.GetString(data, start, position - start);
                    return new Name_(Regex.Replace(raw, "#([0-9A-Fa-f]{2})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString()));
                }
                case (byte)'(':
                    return LiteralString();
                case (byte)'<' when position + 1 < data.Length && data[position + 1] == '<':
                {
                    position += 2;
                    var dict = new Dictionary<string, object?>();
                    while (true)
                    {
                        SkipWhite();
                        if (position + 1 < data.Length && data[position] == '>' && data[position + 1] == '>')
                        {
                            position += 2;
                            return dict;
                        }
                        if (Next() is not Name_ key)
                            return dict;
                        dict[key.Value] = Value();
                    }
                }
                case (byte)'<':
                {
                    var start = ++position;
                    while (position < data.Length && data[position] != '>')
                        position++;
                    var hex = Regex.Replace(Encoding.Latin1.GetString(data, start, position - start), @"\s", "");
                    position++;
                    return Convert.FromHexString(hex.Length % 2 == 1 ? hex + "0" : hex);
                }
                case (byte)'[':
                {
                    position++;
                    var list = new List<object?>();
                    while (true)
                    {
                        SkipWhite();
                        if (position >= data.Length || data[position] == ']')
                        {
                            position++;
                            return list;
                        }
                        list.Add(Value());
                    }
                }
                case (byte)']' or (byte)'>' or (byte)')' or (byte)'{' or (byte)'}':
                    position++;
                    return new Operator(((char)b).ToString());
            }
            var begin = position;
            while (position < data.Length && !IsWhite(data[position]) && !IsDelimiter(data[position]))
                position++;
            var word = Encoding.Latin1.GetString(data, begin, position - begin);
            if (double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                // "n g R" is a reference.
                if (word.All(char.IsDigit))
                {
                    var save = position;
                    SkipWhite();
                    var g = position;
                    while (position < data.Length && char.IsDigit((char)data[position])) position++;
                    if (position > g)
                    {
                        SkipWhite();
                        if (position < data.Length && data[position] == 'R' && (position + 1 >= data.Length || IsWhite(data[position + 1]) || IsDelimiter(data[position + 1])))
                        {
                            position++;
                            return new Ref(int.Parse(word, CultureInfo.InvariantCulture));
                        }
                    }
                    position = save;
                }
                return number;
            }
            return word switch
            {
                "true" => true,
                "false" => false,
                _ => new Operator(word),
            };
        }

        byte[] LiteralString()
        {
            position++;
            var result = new List<byte>();
            var depth = 1;
            while (position < data.Length)
            {
                var b = data[position++];
                if (b == '\\' && position < data.Length)
                {
                    var e = data[position++];
                    switch (e)
                    {
                        case (byte)'n': result.Add(10); break;
                        case (byte)'r': result.Add(13); break;
                        case (byte)'t': result.Add(9); break;
                        case (byte)'b': result.Add(8); break;
                        case (byte)'f': result.Add(12); break;
                        case (byte)'\r': if (position < data.Length && data[position] == '\n') position++; break;
                        case (byte)'\n': break;
                        case >= (byte)'0' and <= (byte)'7':
                        {
                            var value = e - '0';
                            for (var k = 0; k < 2 && position < data.Length && data[position] is >= (byte)'0' and <= (byte)'7'; k++)
                                value = value * 8 + (data[position++] - '0');
                            result.Add((byte)value);
                            break;
                        }
                        default: result.Add(e); break;
                    }
                    continue;
                }
                if (b == '(') depth++;
                if (b == ')' && --depth == 0) break;
                result.Add(b);
            }
            return result.ToArray();
        }
    }

    /// <summary>
    /// The glyphs grouped into lines: glyphs of one page whose baselines lie within 0.01 pt of each other, left to
    /// right, a new line where text continues left of the last glyph's end.
    /// </summary>
    public List<List<PdfGlyph>> Lines()
    {
        var lines = new List<List<PdfGlyph>>();
        foreach (var group in Glyphs.GroupBy(g => (g.Page, Math.Round(g.Y, 1))).OrderBy(g => g.Key.Page).ThenBy(g => g.Key.Item2))
            lines.Add(group.OrderBy(g => g.X).ToList());
        return lines;
    }
}
