using FreeSlate.Text;

namespace Fidelity.Word;

/// <summary>
/// Measuring Word's text widths: compares where Word put the glyphs of each line (its PDF export, <see cref="PdfText"/>)
/// with the advances our fonts give under several hypotheses (font size as given or rounded to Word's 600 dpi device;
/// kerning from GPOS, from the legacy kern table or none), so that the rule Word follows shows as the hypothesis whose
/// positions match.
/// </summary>
public static class WordMetrics
{
    sealed record Hypothesis(string Name, bool Quantized, string Kerning, bool Ligatures, string? Language = null);

    static readonly Hypothesis[] Hypotheses =
    [
        new("nominal, GPOS kern", false, "gpos", true),
        new("nominal, GPOS kern, de", false, "gpos", true, "de"),
        new("nominal, GPOS kern, en", false, "gpos", true, "en"),
        new("nominal, GPOS kern, no liga", false, "gpos", false),
        new("600dpi size, GPOS kern", true, "gpos", true),
        new("nominal, kern table", false, "table", true),
        new("nominal, no kern", false, "none", true),
    ];

    /// <summary>
    /// For pairs of characters: the plain advance of the first, the GPOS kerning HarfBuzz applies and the kerning of the
    /// legacy kern table, in font units, to compare with what Word's positions imply.
    /// </summary>
    public static void Pairs(string family, IEnumerable<string> pairs)
    {
        var font = FontResolver.Default.ResolveTypeface(family, false, false).Data;
        foreach (var pair in pairs)
        {
            var plain = font.GlyphsWithFeatures(pair, ["-kern"]);
            var kerned = font.GlyphsWithFeatures(pair, ["kern"]);
            var gpos = (kerned[0].Advance - plain[0].Advance) * font.UnitsPerEm;
            var table = font.LegacyKerning(plain[0].Id, plain[1].Id) * font.UnitsPerEm;
            Console.WriteLine($"{pair}: advance {plain[0].Advance * font.UnitsPerEm:0} GPOS {gpos:0} kern table {table:0} (glyphs {plain[0].Id} {plain[1].Id})");
        }
    }

    /// <summary>The vertical metrics of fonts in em: hhea, OS/2 typo (with USE_TYPO_METRICS) and OS/2 win.</summary>
    public static void Metrics(IEnumerable<string> families)
    {
        foreach (var family in families)
        {
            var font = FontResolver.Default.ResolveTypeface(family, false, false).Data;
            var (ha, hd, hg) = font.Hhea;
            var typo = font.Typo;
            Console.WriteLine($"{family}\t{font.Family}\thhea {ha:0.0000} {hd:0.0000} {hg:0.0000}\ttypo {typo?.Ascender:0.0000} {typo?.Descender:0.0000} {typo?.LineGap:0.0000} use={typo?.Use}\twin {font.WinAscent:0.0000} {font.WinDescent:0.0000}");
        }
    }

    /// <summary>A font size rounded to whole pixels of a 600 dpi device, as Word's PDF shows them (11 pt → 11.04 pt).</summary>
    public static double DeviceSize(double points) => Math.Round(points * 600 / 72) * 72 / 600;

    public static void Glyphs(IEnumerable<string> documents, bool verbose)
    {
        foreach (var document in documents)
        {
            var pdf = document.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? document : References.DocumentPdf(document);
            var layout = new PdfText(pdf);
            var totals = Hypotheses.ToDictionary(h => h, _ => (Sum: 0.0, Max: 0.0, Count: 0));
            Console.WriteLine(document);
            foreach (var line in layout.Lines())
            {
                foreach (var run in Runs(line).Where(r => r.Count >= 8))
                {
                    var first = run[0];
                    var (family, bold, italic) = ParseFont(first.Font);
                    // The PDF names fonts by their PostScript names (ArialMT, CourierNewPSMT, SegoeUI).
                    var font = FontResolver.Default.ResolveTypeface(family, bold, italic).Data;
                    var text = string.Concat(run.Select(g => g.Text));
                    var nominal = Math.Round(first.Size * 2) / 2;
                    var report = new List<string>();
                    foreach (var h in Hypotheses)
                    {
                        var size = h.Quantized ? DeviceSize(nominal) : nominal;
                        var x = Positions(font, text, size, h);
                        // Word's glyph positions at their characters' offsets, relative to the run's start.
                        double max = 0, end = 0;
                        var offset = 0;
                        foreach (var g in run)
                        {
                            var residual = g.X - first.X - x[Math.Min(offset, x.Length - 1)];
                            max = Math.Max(max, Math.Abs(residual));
                            end = residual;
                            offset += g.Text.Length;
                        }
                        var t = totals[h];
                        totals[h] = (t.Sum + Math.Abs(end), Math.Max(t.Max, max), t.Count + 1);
                        report.Add($"{h.Name}: end {end:+0.000;-0.000} max {max:0.000}");
                    }
                    if (Environment.GetEnvironmentVariable("FS_WORDS") == "1")
                    {
                        // Residuals at word starts along the line (nominal GPOS model).
                        var model = Positions(font, text, nominal, Hypotheses[int.TryParse(Environment.GetEnvironmentVariable("FS_HYPOTHESIS"), out var hy) ? hy : 0]);
                        var at = 0;
                        var residuals = new List<string>();
                        for (var i = 0; i < run.Count; i++)
                        {
                            if (i > 0 && run[i - 1].Text == " " && run[i].Text != " ")
                                residuals.Add($"{run[i].X - first.X - model[at]:+0.00;-0.00}");
                            at += run[i].Text.Length;
                        }
                        Console.WriteLine($"WORDS {font.Family} {nominal} {Clip(text)}");
                        Console.WriteLine($"   {string.Join(' ', residuals)}");
                    }
                    if (Environment.GetEnvironmentVariable("FS_ADVANCES") == "1")
                    {
                        // Per glyph: Word's advance and the nominal GPOS model's.
                        var model = Positions(font, text, nominal, Hypotheses[int.TryParse(Environment.GetEnvironmentVariable("FS_HYPOTHESIS"), out var hy) ? hy : 0]);
                        var at = 0;
                        for (var i = 0; i + 1 < run.Count; i++)
                        {
                            var next = at + run[i].Text.Length;
                            Console.WriteLine($"ADV	{font.Family}	{nominal}	{run[i].Text}	{run[i + 1].Text}	{run[i + 1].X - run[i].X:0.0000}	{model[next] - model[at]:0.0000}");
                            at = next;
                        }
                    }
                    if (verbose)
                        Console.WriteLine($"  p{first.Page} y={first.Y:0.00} {first.Font} {first.Size:0.##} → {font.Family} ({run.Count} glyphs) {Clip(text)}\n      {string.Join("\n      ", report)}");
                }
            }
            foreach (var (h, t) in totals)
                Console.WriteLine($"  {h.Name,-28} runs {t.Count,4}  mean |end| {(t.Count > 0 ? t.Sum / t.Count : 0),7:0.000} pt  max {t.Max,7:0.000} pt");
        }
    }

    static string Clip(string text) => text.Length > 60 ? text[..60] + "…" : text;

    /// <summary>The line's glyphs split where the font or the size changes.</summary>
    static List<List<PdfGlyph>> Runs(List<PdfGlyph> line)
    {
        var runs = new List<List<PdfGlyph>>();
        foreach (var g in line)
        {
            // Word writes spaces with a font of their own (a simple TrueType font of the same family); they belong to the run.
            if (runs.Count > 0 && (FamilyOf(runs[^1][0].Font) == FamilyOf(g.Font) && Math.Abs(runs[^1][0].Size - g.Size) < 0.001))
                runs[^1].Add(g);
            else
                runs.Add([g]);
        }
        return runs;
    }

    static string FamilyOf(string pdfFont) => pdfFont;

    /// <summary>"Aptos,BoldItalic" → (Aptos, bold, italic); PostScript names ("TimesNewRomanPS-BoldMT") keep their style words.</summary>
    public static (string Family, bool Bold, bool Italic) ParseFont(string pdfFont)
    {
        var comma = pdfFont.IndexOf(',');
        var family = comma < 0 ? pdfFont : pdfFont[..comma];
        var style = comma < 0 ? pdfFont : pdfFont[(comma + 1)..];
        var hyphen = family.IndexOf('-');
        if (hyphen > 0)
            family = family[..hyphen];
        return (family, style.Contains("Bold", StringComparison.Ordinal),
            style.Contains("Italic", StringComparison.Ordinal) || style.Contains("Oblique", StringComparison.Ordinal));
    }

    /// <summary>The x position (points from the run's start) of every character of <paramref name="text"/> under a hypothesis.</summary>
    static double[] Positions(FontData font, string text, double size, Hypothesis h)
    {
        var features = new List<string> { h.Kerning == "gpos" ? "kern" : "-kern" };
        if (!h.Ligatures)
            features.AddRange(["-liga", "-clig"]);
        var glyphs = font.GlyphsWithFeatures(text, features, language: h.Language);
        var x = new double[text.Length + 1];
        var position = 0.0;
        var assigned = new bool[text.Length + 1];
        for (var i = 0; i < glyphs.Length; i++)
        {
            var g = glyphs[i];
            if (g.Cluster < text.Length && !assigned[g.Cluster])
            {
                x[g.Cluster] = position;
                assigned[g.Cluster] = true;
            }
            var advance = g.Advance;
            if (h.Kerning == "table" && i + 1 < glyphs.Length)
                advance += font.LegacyKerning(g.Id, glyphs[i + 1].Id);
            position += advance * size;
        }
        // Characters inside a cluster take the cluster's position; the end is the total.
        for (var c = 1; c < text.Length; c++)
        {
            if (!assigned[c])
                x[c] = x[c - 1];
        }
        x[text.Length] = position;
        return x;
    }
}
