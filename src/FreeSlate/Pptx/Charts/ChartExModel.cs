using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate.Pptx.Charts;

/// <summary>
/// An Office 2016 chart (cx:chartSpace, catalog: chart.ex): its data blocks (category levels, the leaves first, and
/// numbers by dimension type), its series with their layouts (waterfall, funnel, clusteredColumn with binning,
/// paretoLine, boxWhisker, treemap, sunburst, regionMap) and its axes.
/// </summary>
sealed class ChartExModel
{
    public static readonly XNamespace Cx = "http://schemas.microsoft.com/office/drawing/2014/chartex";

    public required XElement Space { get; init; }
    public required XElement Chart { get; init; }
    public Dictionary<int, ExData> Data { get; } = [];
    public List<ExSeries> Series { get; } = [];

    public XElement? PlotArea => Chart.Element(Cx + "plotArea");
    public XElement? Title => Chart.Element(Cx + "title");
    public XElement? Legend => Chart.Element(Cx + "legend");

    /// <summary>An axis by id (0 categories, 1 values, 2 the secondary values of Pareto lines).</summary>
    public XElement? Axis(int id) => PlotArea?.Elements(Cx + "axis").FirstOrDefault(a => (int?)a.Attribute("id") == id);

    /// <summary>The layout of the chart: the first series' (waterfall, funnel, clusteredColumn …).</summary>
    public string Layout => Series.FirstOrDefault()?.Layout ?? "";

    public static ChartExModel? Read(XDocument document)
    {
        var space = document.Root;
        var chart = space?.Element(Cx + "chart");
        if (space == null || space.Name != Cx + "chartSpace" || chart == null)
            return null;
        var model = new ChartExModel { Space = space, Chart = chart };
        foreach (var data in space.Element(Cx + "chartData")?.Elements(Cx + "data") ?? [])
        {
            var block = new ExData();
            foreach (var dimension in data.Elements(Cx + "strDim"))
            {
                if ((string?)dimension.Attribute("type") is not ("cat" or null))
                    continue;
                foreach (var level in dimension.Elements(Cx + "lvl"))
                    block.Levels.Add(Points(level, v => v));
            }
            foreach (var dimension in data.Elements(Cx + "numDim"))
            {
                var type = (string?)dimension.Attribute("type") ?? "val";
                var level = dimension.Element(Cx + "lvl");
                if (level == null)
                    continue;
                block.Numbers[type] = Points(level, v => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : (double?)null);
                block.Formats[type] = (string?)level.Attribute("formatCode") ?? "General";
            }
            model.Data[(int?)data.Attribute("id") ?? 0] = block;
        }
        var index = 0;
        foreach (var series in model.PlotArea?.Element(Cx + "plotAreaRegion")?.Elements(Cx + "series") ?? [])
        {
            var name = series.Element(Cx + "tx")?.Element(Cx + "txData")?.Element(Cx + "v")?.Value;
            model.Series.Add(new ExSeries(series, (string?)series.Attribute("layoutId") ?? "", (int?)series.Element(Cx + "dataId")?.Attribute("val") ?? 0,
                name, (string?)series.Attribute("hidden") is "1" or "true", index++));
        }
        return model;
    }

    static List<T> Points<T>(XElement level, Func<string, T> parse)
    {
        var count = (int?)level.Attribute("ptCount") ?? 0;
        var points = Enumerable.Repeat(default(T)!, count).ToList();
        foreach (var pt in level.Elements(Cx + "pt"))
        {
            var idx = (int?)pt.Attribute("idx") ?? -1;
            if (idx >= 0 && idx < points.Count)
                points[idx] = parse(pt.Value);
        }
        return points;
    }
}

/// <summary>A data block: its category levels (leaves first) and numbers by type (val, size …).</summary>
sealed class ExData
{
    public List<List<string?>> Levels { get; } = [];
    public Dictionary<string, List<double?>> Numbers { get; } = [];
    public Dictionary<string, string> Formats { get; } = [];

    public List<double?> Values => Numbers.TryGetValue("val", out var v) ? v : Numbers.TryGetValue("size", out var s) ? s : Numbers.Values.FirstOrDefault() ?? [];

    public string Format => Formats.TryGetValue("val", out var f) ? f : Formats.TryGetValue("size", out var s) ? s : "General";

    public int Count => Math.Max(Values.Count, Levels.Count > 0 ? Levels[0].Count : 0);

    /// <summary>The leaf category of point <paramref name="i"/>.</summary>
    public string Category(int i) => Levels.Count > 0 && i < Levels[0].Count ? Levels[0][i] ?? "" : (i + 1).ToString(CultureInfo.InvariantCulture);
}

/// <summary>A series: its element, layout, data block, name, whether hidden, and its place.</summary>
sealed record ExSeries(XElement Element, string Layout, int DataId, string? Name, bool Hidden, int Index)
{
    public XElement? LayoutProperties => Element.Element(ChartExModel.Cx + "layoutPr");
}
