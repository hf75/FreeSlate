using System.Globalization;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// The page tool's watermarks (see Arranging.Page): a word or two as Word's text watermark (Arranging.Watermark), or a picture of the
/// document by its token ("[picture 3]") as Word's Design > Watermark > Picture puts one in: a VML picture named WordPictureWatermark… in
/// each header the first section's pages show, behind the text, centred on the margins, washed out (v:imagedata gain 19661f and blacklevel
/// 22938f) unless "vivid" follows, its scale automatic (as large as the margins hold, its share of its own size cut to a tenth of a percent:
/// a picture of 768 by 576 points on margins 451.3 points wide 450.8 by 338.1 in Word's) or a percentage of its own size ("[picture 3]
/// 50%"). An empty text takes the watermark away, of either kind.
/// </summary>
static partial class Watermarks
{
    /// <summary>The name Word gives its picture watermark, by which its Watermark dialog finds one.</summary>
    public const string PictureName = "WordPictureWatermark";

    [GeneratedRegex(@"^\[(?:picture|graphic|drawing|object)\s+(\d{1,5})\]\s*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex PictureToken();

    /// <summary>The page tool's watermark given (see the class); a problem with it thrown as ArgumentException.</summary>
    public static void Set(WordDocument word, int first, Objects objects, string text)
    {
        if (PictureToken().Match(text) is not { Success: true } token)
        {
            Arranging.Watermark(word, first, text);
            return;
        }
        var id = int.Parse(token.Groups[1].Value, CultureInfo.InvariantCulture);
        if (objects.Find(id) is not var (paragraph, offset) || word.PictureOf(paragraph, offset) is not { } bytes)
            throw new ArgumentException($"Object {id.ToString(CultureInfo.InvariantCulture)} is no picture of the document.");
        var rest = token.Groups[2].Value.ToLowerInvariant();
        var vivid = rest.Contains("vivid", StringComparison.Ordinal) || rest.Contains("no washout", StringComparison.Ordinal);
        double? scale = Regex.Match(rest, @"(\d{1,3})\s*%") is { Success: true } percent ? int.Parse(percent.Groups[1].Value, CultureInfo.InvariantCulture) / 100.0 : null;
        Picture(word, first, bytes, scale, !vivid);
    }

    /// <summary>
    /// A picture as the watermark of every page of the first section (see the class), in each header its pages show (the first page's own
    /// and the even pages' too where they have their own), in place of the watermark they had.
    /// </summary>
    public static void Picture(WordDocument word, int first, byte[] bytes, double? scale, bool washout)
    {
        var (type, extension) = bytes switch
        {
            [0x89, 0x50, 0x4E, 0x47, ..] => ("image/png", ".png"),
            [0xFF, 0xD8, 0xFF, ..] => ("image/jpeg", ".jpeg"),
            [0x47, 0x49, 0x46, ..] => ("image/gif", ".gif"),
            [0x42, 0x4D, ..] => ("image/bmp", ".bmp"),
            _ => throw new ArgumentException("The picture is of a kind a watermark does not take (PNG, JPEG, GIF or BMP)."),
        };
        // Its own size: its pixels at 96 to the inch, as Word takes a picture without a resolution of its own.
        using var codec = SKCodec.Create(new MemoryStream(bytes)) ?? throw new ArgumentException("The picture could not be read.");
        var (ownWidth, ownHeight) = (codec.Info.Width * 0.75, codec.Info.Height * 0.75);
        var section = word.SectionOf(first);
        var across = (section.PageWidth ?? 595.3) - Math.Abs(section.Left ?? 72) - Math.Abs(section.Right ?? 72);
        var down = (section.PageHeight ?? 841.9) - Math.Abs(section.Top ?? 72) - Math.Abs(section.Bottom ?? 72);
        // Automatic: as large as the margins hold, the share cut to a tenth of a percent (Word's 58.7 % for 768 points on 451.3).
        var share = scale ?? Math.Floor(Math.Min(across / ownWidth, down / ownHeight) * 1000) / 1000;
        var (width, height) = (ownWidth * share, ownHeight * share);
        var kinds = new List<string> { "default" };
        if (section.TitlePage == true)
            kinds.Add("first");
        if (word.EvenAndOddHeaders)
            kinds.Add("even");
        foreach (var kind in kinds)
        {
            var story = word.Header(first, kind, false);
            for (int? paragraph = story; paragraph is { } at; paragraph = word.After(at))
            {
                while (Arranging.WatermarkAt(word, at) is { } offset)
                    word.Replace(at, offset, 1, "");
            }
            word.InsertMarkup(story, 0, Run(width, height, washout), [new NewPart("watermark", "/word/media/image1" + extension, type,
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", bytes)]);
        }
    }

    /// <summary>The watermark's run as Word's Watermark dialog writes it (a VML picture of shape type 75 behind the text, centred on the margins).</summary>
    static string Run(double width, double height, bool washout)
    {
        static string Pt(double points) => points.ToString("0.##", CultureInfo.InvariantCulture);
        var id = Random.Shared.Next(10_000_000, 99_999_999).ToString(CultureInfo.InvariantCulture);
        var spid = Random.Shared.Next(2049, 9999).ToString(CultureInfo.InvariantCulture);
        return "<w:r><w:rPr><w:noProof/></w:rPr><w:pict>"
            + "<v:shapetype id=\"_x0000_t75\" coordsize=\"21600,21600\" o:spt=\"75\" o:preferrelative=\"t\" path=\"m@4@5l@4@11@9@11@9@5xe\" filled=\"f\" stroked=\"f\">"
            + "<v:stroke joinstyle=\"miter\"/><v:formulas><v:f eqn=\"if lineDrawn pixelLineWidth 0\"/><v:f eqn=\"sum @0 1 0\"/><v:f eqn=\"sum 0 0 @1\"/>"
            + "<v:f eqn=\"prod @2 1 2\"/><v:f eqn=\"prod @3 21600 pixelWidth\"/><v:f eqn=\"prod @3 21600 pixelHeight\"/><v:f eqn=\"sum @0 0 1\"/>"
            + "<v:f eqn=\"prod @6 1 2\"/><v:f eqn=\"prod @7 21600 pixelWidth\"/><v:f eqn=\"sum @8 21600 0\"/><v:f eqn=\"prod @7 21600 pixelHeight\"/>"
            + "<v:f eqn=\"sum @10 21600 0\"/></v:formulas><v:path o:extrusionok=\"f\" gradientshapeok=\"t\" o:connecttype=\"rect\"/>"
            + "<o:lock v:ext=\"edit\" aspectratio=\"t\"/></v:shapetype>"
            + $"<v:shape id=\"{PictureName}{id}\" o:spid=\"_x0000_s{spid}\" type=\"#_x0000_t75\" style=\"position:absolute;margin-left:0;margin-top:0;"
            + $"width:{Pt(width)}pt;height:{Pt(height)}pt;z-index:-251657216;mso-position-horizontal:center;mso-position-horizontal-relative:margin;"
            + "mso-position-vertical:center;mso-position-vertical-relative:margin\" o:allowincell=\"f\">"
            + "<v:imagedata r:id=\"{watermark}\" o:title=\"\"" + (washout ? " gain=\"19661f\" blacklevel=\"22938f\"" : "") + "/></v:shape></w:pict></w:r>";
    }

    /// <summary>How "Page:" says a header's watermark: its words in quotes, or that it is a picture (and whether washed out).</summary>
    public static string Said(WordDocument word, int story, string words)
    {
        for (int? paragraph = story; paragraph is { } at; paragraph = word.After(at))
        {
            if (Arranging.WatermarkAt(word, at) is { } offset && word.DrawingName(at, offset)?.StartsWith(PictureName, StringComparison.Ordinal) == true)
                return word.DrawingOf(at, offset)?.Recolor == "washout" ? "a picture, washed out" : "a picture";
        }
        return "\"" + words + "\"";
    }
}
