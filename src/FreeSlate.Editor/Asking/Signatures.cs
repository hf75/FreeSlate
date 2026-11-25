using System.Globalization;
using System.Security;
using System.Xml.Linq;
using SkiaSharp;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// A signature line as Word's Insert > Signature Line makes one: a fenced block ```signature signer="…" title="…" email="…" with the
/// instructions to the signer inside, made a VML picture in the line with Word's o:signatureline (its suggested signer, the signer's title
/// and e-mail, the instructions, whether the signer may add comments and whether the date shows when it is signed), 191.85 by 96.15
/// points as Word makes one unless its width says otherwise (its height in that proportion), its description Word's for a line not signed
/// yet, and a picture of the line as Word stores one for programs that do not draw it themselves (Word draws it itself whatever the picture,
/// see Docx.Vml.SignatureLine). Measured in Word 365 by COM (Signatures.AddSignatureLine, its setup dialog closed by a key: a v:shape
/// _x0000_i1025 of shape type 75, its picture an EMF, o:signatureline with v:ext, id, provid and issignatureline). Read back as the block.
/// </summary>
static class Signatures
{
    const string Image = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";

    /// <summary>A signature line at a place: in a line of its own, at the left unless its align says otherwise.</summary>
    public static Markup.Made Of(Notation.Fenced block, Markup.Place place)
    {
        var settings = block.Settings;
        var signer = settings.GetValueOrDefault("signer") ?? "";
        var title = settings.GetValueOrDefault("title") ?? "";
        var email = settings.GetValueOrDefault("email") ?? settings.GetValueOrDefault("e-mail") ?? "";
        var instructions = string.Join(" ", block.Body.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
        if (instructions.Length == 0)
            instructions = settings.GetValueOrDefault("instructions") ?? "";
        // Word's own size, 191.85 by 96.15 points.
        var width = Math.Clamp(Markup.Length(settings.GetValueOrDefault("width"), place.TextWidth) ?? 191.85, 72, place.TextWidth);
        var height = width * 96.15 / 191.85;
        static bool Flag(string? value, bool fallback) => value?.Trim().ToLowerInvariant() switch
        {
            "yes" or "true" or "on" => true,
            "no" or "false" or "off" or "none" => false,
            _ => fallback,
        };
        var showDate = Flag(settings.GetValueOrDefault("date"), true);
        var comments = Flag(settings.GetValueOrDefault("comments"), false);
        static string Escaped(string text) => SecurityElement.Escape(text) ?? "";
        static string Pt(double points) => points.ToString("0.##", CultureInfo.InvariantCulture);
        var line = "<o:signatureline v:ext=\"edit\" id=\"{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}\" provid=\"{00000000-0000-0000-0000-000000000000}\""
            + (signer.Length > 0 ? $" o:suggestedsigner=\"{Escaped(signer)}\"" : "")
            + (title.Length > 0 ? $" o:suggestedsigner2=\"{Escaped(title)}\"" : "")
            + (email.Length > 0 ? $" o:suggestedsigneremail=\"{Escaped(email)}\"" : "")
            + (instructions.Length > 0 ? $" o:signinginstructions=\"{Escaped(instructions)}\" signinginstructionsset=\"t\"" : "")
            + (comments ? " allowcomments=\"t\"" : "")
            + (showDate ? "" : " showsigndate=\"f\"")
            + " issignatureline=\"t\"/>";
        var run = "<w:r><w:pict>"
            + "<v:shapetype id=\"_x0000_t75\" coordsize=\"21600,21600\" o:spt=\"75\" o:preferrelative=\"t\" path=\"m@4@5l@4@11@9@11@9@5xe\" filled=\"f\" stroked=\"f\">"
            + "<v:stroke joinstyle=\"miter\"/><v:formulas><v:f eqn=\"if lineDrawn pixelLineWidth 0\"/><v:f eqn=\"sum @0 1 0\"/><v:f eqn=\"sum 0 0 @1\"/>"
            + "<v:f eqn=\"prod @2 1 2\"/><v:f eqn=\"prod @3 21600 pixelWidth\"/><v:f eqn=\"prod @3 21600 pixelHeight\"/><v:f eqn=\"sum @0 0 1\"/>"
            + "<v:f eqn=\"prod @6 1 2\"/><v:f eqn=\"prod @7 21600 pixelWidth\"/><v:f eqn=\"sum @8 21600 0\"/><v:f eqn=\"prod @7 21600 pixelHeight\"/>"
            + "<v:f eqn=\"sum @10 21600 0\"/></v:formulas><v:path o:extrusionok=\"f\" gradientshapeok=\"t\" o:connecttype=\"rect\"/>"
            + "<o:lock v:ext=\"edit\" aspectratio=\"t\"/></v:shapetype>"
            + $"<v:shape id=\"_x0000_i{Random.Shared.Next(1025, 9999).ToString(CultureInfo.InvariantCulture)}\" type=\"#_x0000_t75\" alt=\"{((Markup.CitationLanguage & 0x3FF) == 0x07 ? "Signaturzeile, nicht signiert" : "Signature line, not signed")}\" style=\"width:{Pt(width)}pt;height:{Pt(height)}pt\">"
            + "<v:imagedata r:id=\"{signature}\" o:title=\"\"/>"
            + "<o:lock v:ext=\"edit\" ungrouping=\"t\" rotation=\"t\" cropping=\"t\" verticies=\"t\" text=\"t\" grouping=\"t\"/>"
            + line + "</v:shape></w:pict></w:r>";
        var picture = Picture(width, height, [signer, title]);
        var placed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["align"] = settings.GetValueOrDefault("align") ?? "left" };
        return Markup.Placed(run, [new NewPart("signature", "/word/media/image1.png", "image/png", Image, picture)], placed, place);
    }

    /// <summary>
    /// A picture of the signature line as Word draws it (see Docx.Vml.SignatureLine), at twice its size in points: on white an X, the line
    /// to sign on and under it the suggested signer and title (the e-mail Word does not show on the line).
    /// </summary>
    static byte[] Picture(double width, double height, string[] lines)
    {
        var (w, h) = ((int)Math.Round(width * 2), (int)Math.Round(height * 2));
        using var surface = SKSurface.Create(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        var (sx, sy) = (w / 256f, h / 128f);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        canvas.DrawRect(0, 75 * sy, w, 2 * sy, paint);
        using var cross = new SKFont(SKTypeface.FromFamilyName("Arial") ?? SKTypeface.Default, 32 * sy);
        canvas.DrawText("X", 10 * sx, 68 * sy, cross, paint);
        using var small = new SKFont(SKTypeface.FromFamilyName("Segoe UI") ?? SKTypeface.Default, 11 * sy);
        var baseline = 91f;
        foreach (var text in lines)
        {
            if (text.Length > 0)
                canvas.DrawText(text, 10 * sx, baseline * sy, small, paint);
            baseline += 16;
        }
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>A signature line as the notation writes it (see Of), read back from its VML: the signer, title, e-mail, its settings, the instructions.</summary>
    public static string Block(XElement element, DrawingFormatting? drawing)
    {
        if (element.Descendants().FirstOrDefault(e => e.Name.LocalName == "signatureline") is not { } line)
            return "```signature\n```";
        string? Value(string name) => (string?)line.Attributes().FirstOrDefault(a => a.Name.LocalName == name);
        static string Quoted(string text) => "\"" + text.Replace("\"", "''", StringComparison.Ordinal) + "\"";
        var settings = new List<string>();
        foreach (var (name, attribute) in new[] { ("signer", "suggestedsigner"), ("title", "suggestedsigner2"), ("email", "suggestedsigneremail") })
        {
            if (Value(attribute) is { Length: > 0 } value)
                settings.Add(name + "=" + Quoted(value));
        }
        if (Value("showsigndate") is "f" or "false")
            settings.Add("date=no");
        if (Value("allowcomments") is "t" or "true")
            settings.Add("comments=yes");
        if (drawing?.Width is { } width && Math.Abs(width - 191.85) > 0.5)
            settings.Add("width=" + (width / 72 * 2.54).ToString("0.#", CultureInfo.InvariantCulture) + "cm");
        return "```signature " + string.Join(" ", settings) + "\n" + (Value("signinginstructions") is { Length: > 0 } instructions ? instructions + "\n" : "") + "```";
    }
}
