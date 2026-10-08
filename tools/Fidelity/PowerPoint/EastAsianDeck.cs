using System.Runtime.Versioning;

namespace Fidelity.PowerPoint;

/// <summary>
/// East Asian line breaking (catalog: text.eastAsian): Japanese text with the kinsoku rules on and off (eaLnBrk),
/// with and without hanging punctuation (hangingPunct), Latin words broken anywhere (latinLnBrk) and two widths
/// that put punctuation at the end of a line. The options are patched (see Patch).
/// </summary>
[SupportedOSPlatform("windows")]
static class EastAsianDeck
{
    const int Japanese = 1041;   // msoLanguageIDJapanese

    // これは長い日本語の文章です、句読点の扱いを確認します。改行の位置が正しいかどうか。
    static readonly string Sentence = new([.. new[]
    {
        0x3053, 0x308C, 0x306F, 0x9577, 0x3044, 0x65E5, 0x672C, 0x8A9E, 0x306E, 0x6587, 0x7AE0, 0x3067, 0x3059, 0x3001,
        0x53E5, 0x8AAD, 0x70B9, 0x306E, 0x6271, 0x3044, 0x3092, 0x78BA, 0x8A8D, 0x3057, 0x307E, 0x3059, 0x3002, 0x6539,
        0x884C, 0x306E, 0x4F4D, 0x7F6E, 0x304C, 0x6B63, 0x3057, 0x3044, 0x304B, 0x3069, 0x3046, 0x304B, 0x3002,
    }.Select(c => (char)c)]);

    public static void Build(dynamic deck)
    {
        object presentation = deck;
        var slide = CorpusGenerator.AddSlide((dynamic)presentation, CorpusGenerator.LayoutBlank);
        (string Name, string Text, float Width)[] boxes =
        [
            ("ea-default", Sentence, 250), ("ea-nohang", Sentence, 250),
            ("ea-nokinsoku", Sentence, 250), ("ea-nokinsoku-nohang", Sentence, 250),
            ("ea-latin", $"{Sentence} Internationalization PowerPoint {Sentence}", 300), ("ea-latinbreak", $"{Sentence} Internationalization PowerPoint {Sentence}", 300),
        ];
        for (var i = 0; i < boxes.Length; i++)
        {
            var box = slide.Shapes.AddTextbox(1, 30 + (i % 2) * 470, 20 + (i / 2) * 170, boxes[i].Width, 150);
            box.Line.Visible = PowerPointSession.MsoTrue;
            box.Line.ForeColor.RGB = CorpusGenerator.Rgb(192, 192, 192);
            box.TextFrame.WordWrap = PowerPointSession.MsoTrue;
            box.TextFrame.AutoSize = 0;
            var range = box.TextFrame.TextRange;
            range.Text = boxes[i].Text;
            range.Font.Size = 18;
            range.LanguageID = Japanese;
            box.Name = boxes[i].Name;
        }
    }

    /// <summary>The line breaking options of the paragraphs, which PowerPoint's object model does not set.</summary>
    public static void Patch(string path)
    {
        static Action<System.Xml.Linq.XElement> Options(params (string Name, string Value)[] options) => shape =>
        {
            foreach (var (name, value) in options)
                ShapePatch.Paragraphs(shape, name, value);
        };
        ShapePatch.Apply(path, new Dictionary<string, Action<System.Xml.Linq.XElement>>
        {
            ["ea-nohang"] = Options(("hangingPunct", "0")),
            ["ea-nokinsoku"] = Options(("eaLnBrk", "0")),
            ["ea-nokinsoku-nohang"] = Options(("eaLnBrk", "0"), ("hangingPunct", "0")),
            ["ea-latinbreak"] = Options(("latinLnBrk", "1")),
        });
    }
}
