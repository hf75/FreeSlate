using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Fidelity.Compare;

/// <summary>Writes the comparison as an HTML report with side-by-side images and as JSON.</summary>
public static class Report
{
    public static string Write(List<DeckResult> results)
    {
        Directory.CreateDirectory(Repo.Artifacts);
        var reportPath = Path.Combine(Repo.Artifacts, "report.html");
        string Rel(string path) => Path.GetRelativePath(Repo.Artifacts, path).Replace('\\', '/');
        static string Pct(double v) => (v * 100).ToString("0.0", CultureInfo.InvariantCulture) + " %";

        var overall = results.Count == 0 ? 0 : results.SelectMany(r => r.Slides).Average(s => s.Score.Score);
        var html = new StringBuilder();
        html.Append("""
            <!DOCTYPE html><html><head><meta charset="utf-8"><title>Fidelity report</title><style>
            body{font:14px system-ui,sans-serif;margin:24px;color:#222}
            table{border-collapse:collapse;margin-bottom:24px}td,th{padding:4px 10px;border-bottom:1px solid #ddd;text-align:left}
            td.num{text-align:right;font-variant-numeric:tabular-nums}
            .slide{display:grid;grid-template-columns:repeat(3,1fr);gap:8px;margin:12px 0 28px}
            .slide img{width:100%;border:1px solid #ccc}.cap{font-size:12px;color:#666}
            </style></head><body>
            """);
        html.Append($"<h1>Fidelity: {Pct(overall)}</h1>");
        html.Append("<p>Score: share of content pixels that match PowerPoint's rendering (tolerance 40/255, 1 px displacement). " +
                    "Diff: red = missing or different in our rendering, blue = only in ours.</p>");
        html.Append("<table><tr><th>Deck</th><th>Slides</th><th>Score</th><th>SSIM</th></tr>");
        foreach (var r in results)
            html.Append($"<tr><td><a href=\"#{WebUtility.HtmlEncode(r.Deck)}\">{WebUtility.HtmlEncode(r.Deck)}</a></td><td class=\"num\">{r.Slides.Count}</td><td class=\"num\">{Pct(r.Score)}</td><td class=\"num\">{r.Ssim:0.000}</td></tr>");
        html.Append("</table>");

        foreach (var r in results)
        {
            html.Append($"<h2 id=\"{WebUtility.HtmlEncode(r.Deck)}\">{WebUtility.HtmlEncode(r.Deck)}: {Pct(r.Score)}</h2>");
            html.Append($"<p><a href=\"{Rel(Path.Combine(Pipeline.OutputDir(r.Deck), "index.html"))}\">our HTML</a></p>");
            var document = Repo.IsDocument(r.Deck);
            foreach (var s in r.Slides)
            {
                var n = s.Number.ToString("000", CultureInfo.InvariantCulture);
                var image = References.ImageName(r.Deck, s.Number);
                var reference = Rel(Path.Combine(References.Dir(r.Deck), image));
                var ours = Rel(Path.Combine(Pipeline.OutputDir(r.Deck), image));
                var diff = Rel(Path.Combine(Pipeline.OutputDir(r.Deck), $"diff-{n}.png"));
                html.Append($"<h3>{(document ? "Page" : "Slide")} {s.Number}: {Pct(s.Score.Score)} <span class=\"cap\">SSIM {s.Score.Ssim:0.000}</span></h3><div class=\"slide\">");
                html.Append($"<div><a href=\"{reference}\"><img src=\"{reference}\"></a><div class=\"cap\">{(document ? "Word" : "PowerPoint")}</div></div>");
                html.Append($"<div><a href=\"{ours}\"><img src=\"{ours}\"></a><div class=\"cap\">ours</div></div>");
                html.Append($"<div><a href=\"{diff}\"><img src=\"{diff}\"></a><div class=\"cap\">difference</div></div></div>");
            }
        }
        html.Append("</body></html>");
        File.WriteAllText(reportPath, html.ToString());

        File.WriteAllText(Path.Combine(Repo.Artifacts, "scores.json"), JsonSerializer.Serialize(new
        {
            overall,
            decks = results.Select(r => new
            {
                deck = r.Deck,
                score = r.Score,
                ssim = r.Ssim,
                slides = r.Slides.Select(s => new { number = s.Number, score = s.Score.Score, ssim = s.Score.Ssim }),
            }),
        }, new JsonSerializerOptions { WriteIndented = true }));
        return reportPath;
    }
}
