using UglyToad.PdfPig.Content;

namespace SpecTools.Pdf;

/// <summary>A reconstructed line of text with its baseline position (PDF user space, origin bottom-left).</summary>
public sealed record TextLine(double Baseline, double Left, string Text);

/// <summary>Rebuilds reading-order text lines from the word positions of a PDF page.</summary>
public static class PdfLines
{
    const double BaselineTolerance = 2.0;

    public static List<TextLine> Read(Page page)
    {
        var words = page.GetWords()
            .Where(w => !string.IsNullOrWhiteSpace(w.Text) && w.Letters.Count > 0)
            .Select(w => (Word: w, Baseline: w.Letters[0].StartBaseLine.Y))
            .OrderByDescending(x => x.Baseline)
            .ThenBy(x => x.Word.BoundingBox.Left)
            .ToList();

        var groups = new List<List<(Word Word, double Baseline)>>();
        foreach (var item in words)
        {
            if (groups.Count > 0 && Math.Abs(groups[^1][0].Baseline - item.Baseline) <= BaselineTolerance)
                groups[^1].Add(item);
            else
                groups.Add([item]);
        }

        return groups
            .Select(g =>
            {
                var ordered = g.OrderBy(x => x.Word.BoundingBox.Left).ToList();
                return new TextLine(
                    ordered[0].Baseline,
                    ordered[0].Word.BoundingBox.Left,
                    string.Join(' ', ordered.Select(x => x.Word.Text)));
            })
            .ToList();
    }
}
