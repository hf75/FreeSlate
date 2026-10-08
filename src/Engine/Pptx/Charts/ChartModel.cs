using System.Globalization;
using System.Xml.Linq;

namespace Engine.Pptx.Charts;

/// <summary>One data series (c:ser) with its cached values (ECMA-376 Part 1, §21.2.2).</summary>
public sealed class ChartSeries
{
    public required XElement Element { get; init; }
    public int Index { get; init; }
    public int Order { get; init; }
    public string Name { get; init; } = "";
    public List<string> Categories { get; init; } = [];
    public List<double?> Values { get; init; } = [];
    public List<double?> XValues { get; init; } = [];
    public string FormatCode { get; init; } = "General";
    public string CategoryFormatCode { get; init; } = "General";

    public XElement? SpPr => Element.Element(Ns.C + "spPr");

    /// <summary>Data point overrides (c:dPt) by point index.</summary>
    public Dictionary<int, XElement> Points { get; init; } = [];
}

/// <summary>A chart type group of the plot area (c:barChart, c:lineChart, c:pieChart …) and its series.</summary>
public sealed class ChartGroup
{
    public required XElement Element { get; init; }

    /// <summary>The element's local name, e.g. "barChart".</summary>
    public string Type => Element.Name.LocalName;

    public bool IsBar => Type is "barChart" or "bar3DChart";
    public bool IsLine => Type is "lineChart" or "line3DChart" or "stockChart";
    public bool IsArea => Type is "areaChart" or "area3DChart";
    public bool IsPie => Type is "pieChart" or "pie3DChart" or "doughnutChart" or "ofPieChart";
    public bool IsScatter => Type is "scatterChart" or "bubbleChart";

    /// <summary>Bars grow horizontally (c:barDir val="bar").</summary>
    public bool Horizontal { get; init; }

    /// <summary>clustered, stacked, percentStacked or standard.</summary>
    public string Grouping { get; init; } = "clustered";

    public bool Stacked => Grouping is "stacked" or "percentStacked";
    public bool PercentStacked => Grouping == "percentStacked";
    public bool VaryColors { get; init; }
    public double GapWidth { get; init; } = 150;
    public double Overlap { get; init; }
    public double FirstSliceAngle { get; init; }
    public double HoleSize { get; init; }
    public List<ChartSeries> Series { get; init; } = [];
    public List<int> AxisIds { get; init; } = [];
}

/// <summary>An axis (c:catAx, c:valAx, c:dateAx, c:serAx).</summary>
public sealed class ChartAxis
{
    public required XElement Element { get; init; }
    public int Id { get; init; }

    /// <summary>catAx, valAx, dateAx or serAx.</summary>
    public string Kind => Element.Name.LocalName;

    public bool IsValue => Kind == "valAx";

    /// <summary>b, l, r or t.</summary>
    public string Position { get; init; } = "l";

    public bool Deleted { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public bool Reversed { get; init; }
    public double? LogBase { get; init; }
    public double? MajorUnit { get; init; }
    public double? MinorUnit { get; init; }
    public string FormatCode { get; init; } = "General";
    public bool SourceLinked { get; init; }

    /// <summary>nextTo, high, low or none.</summary>
    public string TickLabelPosition { get; init; } = "nextTo";

    public string MajorTickMark { get; init; } = "cross";
    public string MinorTickMark { get; init; } = "cross";
    public int CrossAxisId { get; init; }

    /// <summary>autoZero, min or max; null when c:crossesAt gives a value.</summary>
    public string? Crosses { get; init; } = "autoZero";

    public double? CrossesAt { get; init; }

    /// <summary>between (values between the category ticks) or midCat (on them).</summary>
    public string CrossBetween { get; init; } = "between";

    public XElement? SpPr => Element.Element(Ns.C + "spPr");
    public XElement? TxPr => Element.Element(Ns.C + "txPr");
    public XElement? MajorGridlines => Element.Element(Ns.C + "majorGridlines");
    public XElement? MinorGridlines => Element.Element(Ns.C + "minorGridlines");
    public XElement? Title => Element.Element(Ns.C + "title");
    public bool Horizontal => Position is "b" or "t";
}

/// <summary>The parsed c:chartSpace.</summary>
public sealed class ChartModel
{
    public required XElement ChartSpace { get; init; }
    public required XElement Chart { get; init; }
    public List<ChartGroup> Groups { get; } = [];
    public List<ChartAxis> Axes { get; } = [];
    public XElement? PlotArea => Chart.Element(Ns.C + "plotArea");
    public XElement? Legend => Chart.Element(Ns.C + "legend");
    public XElement? Title => Chart.Element(Ns.C + "title");
    public bool AutoTitleDeleted { get; init; }
    public IEnumerable<ChartSeries> AllSeries => Groups.SelectMany(g => g.Series);

    public ChartAxis? Axis(int id) => Axes.FirstOrDefault(a => a.Id == id);

    public static ChartModel? Read(XDocument document)
    {
        var space = document.Root;
        var chart = space?.Element(Ns.C + "chart");
        if (space == null || chart == null)
            return null;

        var model = new ChartModel
        {
            ChartSpace = space,
            Chart = chart,
            AutoTitleDeleted = Flag(chart, "autoTitleDeleted", false),
        };
        var plotArea = chart.Element(Ns.C + "plotArea");
        foreach (var element in plotArea?.Elements() ?? [])
        {
            var name = element.Name.LocalName;
            if (name.EndsWith("Chart", StringComparison.Ordinal))
                model.Groups.Add(ReadGroup(element));
            else if (name is "catAx" or "valAx" or "dateAx" or "serAx")
                model.Axes.Add(ReadAxis(element));
        }
        return model;
    }

    static ChartGroup ReadGroup(XElement element)
    {
        var type = element.Name.LocalName;
        return new ChartGroup
        {
            Element = element,
            Horizontal = Val(element, "barDir") == "bar",
            Grouping = Val(element, "grouping") ?? (type.StartsWith("bar", StringComparison.Ordinal) ? "clustered" : "standard"),
            VaryColors = Flag(element, "varyColors", false),
            GapWidth = Number(element, "gapWidth") ?? 150,
            Overlap = Number(element, "overlap") ?? (Val(element, "grouping") is "stacked" or "percentStacked" ? 100 : 0),
            FirstSliceAngle = Number(element, "firstSliceAng") ?? 0,
            HoleSize = Number(element, "holeSize") ?? (type == "doughnutChart" ? 10 : 0),
            Series = element.Elements(Ns.C + "ser").Select(ReadSeries).OrderBy(s => s.Order).ToList(),
            AxisIds = element.Elements(Ns.C + "axId").Select(a => a.Int("val") ?? 0).ToList(),
        };
    }

    static ChartSeries ReadSeries(XElement ser)
    {
        var (values, format) = Numbers(ser.Element(Ns.C + "val") ?? ser.Element(Ns.C + "yVal"));
        var categories = ser.Element(Ns.C + "cat") ?? ser.Element(Ns.C + "xVal");
        var (xValues, _) = Numbers(ser.Element(Ns.C + "xVal"));
        return new ChartSeries
        {
            Element = ser,
            Index = ser.Element(Ns.C + "idx").Int("val") ?? 0,
            Order = ser.Element(Ns.C + "order").Int("val") ?? 0,
            Name = Strings(ser.Element(Ns.C + "tx")).FirstOrDefault() ?? "",
            Categories = Strings(categories),
            Values = values,
            XValues = xValues,
            FormatCode = format,
            CategoryFormatCode = Cache(categories)?.Element(Ns.C + "formatCode")?.Value ?? "General",
            Points = ser.Elements(Ns.C + "dPt").Where(p => p.Element(Ns.C + "idx") != null)
                .GroupBy(p => p.Element(Ns.C + "idx").Int("val") ?? 0).ToDictionary(g => g.Key, g => g.Last()),
        };
    }

    static ChartAxis ReadAxis(XElement element)
    {
        var scaling = element.Element(Ns.C + "scaling");
        var numFmt = element.Element(Ns.C + "numFmt");
        var crossesAt = Number(element, "crossesAt");
        return new ChartAxis
        {
            Element = element,
            Id = element.Element(Ns.C + "axId").Int("val") ?? 0,
            Position = Val(element, "axPos") ?? "l",
            Deleted = Flag(element, "delete", false),
            Min = Number(scaling, "min"),
            Max = Number(scaling, "max"),
            Reversed = Val(scaling, "orientation") == "maxMin",
            LogBase = Number(scaling, "logBase"),
            MajorUnit = Number(element, "majorUnit"),
            MinorUnit = Number(element, "minorUnit"),
            FormatCode = numFmt.Str("formatCode") ?? "General",
            SourceLinked = numFmt.Bool("sourceLinked") ?? false,
            TickLabelPosition = Val(element, "tickLblPos") ?? "nextTo",
            MajorTickMark = Val(element, "majorTickMark") ?? "cross",
            MinorTickMark = Val(element, "minorTickMark") ?? "cross",
            CrossAxisId = element.Element(Ns.C + "crossAx").Int("val") ?? 0,
            Crosses = crossesAt == null ? Val(element, "crosses") ?? "autoZero" : null,
            CrossesAt = crossesAt,
            CrossBetween = Val(element, "crossBetween") ?? "between",
        };
    }

    /// <summary>A CT_Boolean child: absent gives <paramref name="absent"/>, present without val means true.</summary>
    public static bool Flag(XElement? parent, string name, bool absent)
    {
        var e = parent?.Element(Ns.C + name);
        if (e == null)
            return absent;
        return e.Attribute("val")?.Value is not ("0" or "false");
    }

    public static string? Val(XElement? parent, string name) => parent?.Element(Ns.C + name).Str("val");

    public static double? Number(XElement? parent, string name) =>
        double.TryParse(Val(parent, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    static XElement? Cache(XElement? data)
    {
        var source = data?.Elements().FirstOrDefault();
        return source?.Name.LocalName switch
        {
            "strRef" => source.Element(Ns.C + "strCache"),
            "numRef" => source.Element(Ns.C + "numCache"),
            "multiLvlStrRef" => source.Element(Ns.C + "multiLvlStrCache"),
            "strLit" or "numLit" => source,
            _ => null,
        };
    }

    /// <summary>Text values of c:tx or c:cat (the innermost level of multi-level categories).</summary>
    static List<string> Strings(XElement? data)
    {
        if (data?.Element(Ns.C + "v") is { } literal)
            return [literal.Value];
        var cache = Cache(data);
        if (cache == null)
            return [];
        if (cache.Name.LocalName == "multiLvlStrCache")
            cache = cache.Element(Ns.C + "lvl") is { } level ? new XElement(cache.Name, cache.Element(Ns.C + "ptCount"), level.Elements()) : cache;
        var format = cache.Element(Ns.C + "formatCode")?.Value;
        return Points(cache).Select(v => v == null ? "" : format != null && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? NumberFormat.Format(d, format) : v).ToList();
    }

    static (List<double?> Values, string Format) Numbers(XElement? data)
    {
        var cache = Cache(data);
        if (cache == null)
            return ([], "General");
        var values = Points(cache).Select(v => double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : (double?)null).ToList();
        return (values, cache.Element(Ns.C + "formatCode")?.Value ?? "General");
    }

    static List<string?> Points(XElement cache)
    {
        var points = cache.Elements(Ns.C + "pt").Select(p => (Index: p.Int("idx") ?? 0, Value: p.Element(Ns.C + "v")?.Value)).ToList();
        var count = cache.Element(Ns.C + "ptCount").Int("val") ?? (points.Count == 0 ? 0 : points.Max(p => p.Index) + 1);
        var result = new List<string?>(Enumerable.Repeat<string?>(null, Math.Max(0, count)));
        foreach (var (index, value) in points)
        {
            if (index >= 0 && index < result.Count)
                result[index] = value;
        }
        return result;
    }
}
