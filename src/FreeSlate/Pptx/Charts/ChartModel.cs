using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate.Pptx.Charts;

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

    /// <summary>The bubble sizes of a bubble chart series (c:bubbleSize).</summary>
    public List<double?> BubbleSizes { get; init; } = [];

    public string FormatCode { get; init; } = "General";
    public string CategoryFormatCode { get; init; } = "General";

    /// <summary>
    /// The outer levels of multi-level categories (c:multiLvlStrCache), innermost first after
    /// <see cref="Categories"/>: a level names a group at its first category and holds null for the others.
    /// </summary>
    public List<List<string?>> OuterCategories { get; init; } = [];

    public XElement? SpPr => Element.Element(Ns.C + "spPr");

    /// <summary>Data point overrides (c:dPt) by point index.</summary>
    public Dictionary<int, XElement> Points { get; init; } = [];

    /// <summary>
    /// The categories of a date axis no date falls on: no data points, unlike blank cells (lines and areas run on
    /// through them; measured: PowerPoint joins 5 and 12 January on a day axis with dispBlanksAs gap).
    /// </summary>
    public HashSet<int> Fillers { get; } = [];
}

/// <summary>A chart type group of the plot area (c:barChart, c:lineChart, c:pieChart …) and its series.</summary>
public sealed class ChartGroup
{
    public required XElement Element { get; init; }

    /// <summary>The element's local name, e.g. "barChart".</summary>
    public string Type => Element.Name.LocalName;

    /// <summary>The unit a date axis counts in (days, months, years), null without one.</summary>
    public string? TimeUnit { get; set; }

    public bool IsBar => Type is "barChart" or "bar3DChart";
    public bool IsLine => Type is "lineChart" or "line3DChart" or "stockChart";
    public bool IsArea => Type is "areaChart" or "area3DChart";
    public bool IsPie => Type is "pieChart" or "pie3DChart" or "doughnutChart" or "ofPieChart";
    public bool IsScatter => Type is "scatterChart" or "bubbleChart";
    public bool IsBubble => Type == "bubbleChart";
    public bool IsSurface => Type is "surfaceChart" or "surface3DChart";
    public bool IsRadar => Type == "radarChart";

    /// <summary>A radar chart of filled polygons (c:radarStyle val="filled").</summary>
    public bool IsFilledRadar => IsRadar && ChartModel.Val(Element, "radarStyle") == "filled";

    /// <summary>Series drawn as lines with line keys in the legend: line (not 3D ribbons), stock, scatter and unfilled radar charts.</summary>
    public bool HasLines => IsLine && Type != "line3DChart" || IsScatter && !IsBubble || IsRadar && !IsFilledRadar;

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
        var culture = Culture(Val(space, "lang"));
        var plotArea = chart.Element(Ns.C + "plotArea");
        foreach (var element in plotArea?.Elements() ?? [])
        {
            var name = element.Name.LocalName;
            if (name.EndsWith("Chart", StringComparison.Ordinal))
                model.Groups.Add(ReadGroup(element, culture));
            else if (name is "catAx" or "valAx" or "dateAx" or "serAx")
                model.Axes.Add(ReadAxis(element, model.Groups));
        }
        var date1904 = Flag(space, "date1904", false);
        foreach (var group in model.Groups)
        {
            if (model.Axes.FirstOrDefault(a => a.Kind == "dateAx" && group.AxisIds.Contains(a.Id)) is { } dateAxis)
                SpreadDates(group, dateAxis, date1904);
            else if (date1904)
                ShiftDates(group);
        }
        return model;
    }

    /// <summary>The culture of the chart's c:lang: built-in date formats and decimal separators follow it.</summary>
    public static CultureInfo Culture(string? lang)
    {
        try { return lang != null ? CultureInfo.GetCultureInfo(lang) : CultureInfo.InvariantCulture; }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
    }

    static ChartGroup ReadGroup(XElement element, CultureInfo culture)
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
            Series = element.Elements(Ns.C + "ser").Select(s => ReadSeries(s, culture)).OrderBy(s => s.Order).ToList(),
            AxisIds = element.Elements(Ns.C + "axId").Select(a => a.Int("val") ?? 0).ToList(),
        };
    }

    /// <summary>
    /// A date axis (c:dateAx, catalog: chart.axes) counts days, months or years from the first date to the last
    /// (c:baseTimeUnit; without it days where two dates lie less than four weeks apart, months where less than a year,
    /// else years): every unit is a category, every point lands in the category of its date and covers earlier points
    /// there, empty units stay empty (measured: 1 and 15 January share January, May without data keeps its place).
    /// The categories become the units' first days as serial dates, so the axis's number format labels them; dates of
    /// the 1904 system (c:date1904) are moved to the 1900 one.
    /// </summary>
    static void SpreadDates(ChartGroup group, ChartAxis axis, bool date1904)
    {
        var withDates = group.Series.FirstOrDefault(s => s.Categories.Count > 0);
        if (withDates == null)
            return;
        var dates = Numbers(withDates.Element.Element(Ns.C + "cat")).Values.Select(d => d + (date1904 ? 1462 : 0)).ToList();
        var known = dates.Where(d => d != null).Select(d => Math.Floor(d!.Value)).Distinct().OrderBy(d => d).ToList();
        if (known.Count == 0 || dates.Any(d => d == null))
            return;
        var unit = Val(axis.Element, "baseTimeUnit");
        if (unit == null)
        {
            var step = known.Zip(known.Skip(1), (a, b) => b - a).DefaultIfEmpty(1).Min();
            unit = step < 28 ? "days" : step < 365 ? "months" : "years";
        }
        var first = DateTime.FromOADate(known[0]);
        int Slot(double serial)
        {
            var date = DateTime.FromOADate(Math.Floor(serial));
            return unit switch
            {
                "months" => (date.Year - first.Year) * 12 + date.Month - first.Month,
                "years" => date.Year - first.Year,
                _ => (int)(Math.Floor(serial) - known[0]),
            };
        }
        double Start(int slot) => unit switch
        {
            "months" => new DateTime(first.Year, first.Month, 1).AddMonths(slot).ToOADate(),
            "years" => new DateTime(first.Year, 1, 1).AddYears(slot).ToOADate(),
            _ => known[0] + slot,
        };
        var count = Slot(known[^1]) + 1;
        if (count <= 0 || count > 100000)
            return;
        foreach (var series in group.Series)
        {
            var values = new List<double?>(Enumerable.Repeat<double?>(null, count));
            for (var i = 0; i < series.Values.Count && i < dates.Count; i++)
            {
                if (series.Values[i] is { } v)
                    values[Slot(dates[i]!.Value)] = v;
            }
            series.Values.Clear();
            series.Values.AddRange(values);
            var taken = dates.Select(d => Slot(d!.Value)).ToHashSet();
            series.Fillers.Clear();
            series.Fillers.UnionWith(Enumerable.Range(0, count).Where(k => !taken.Contains(k)));
            series.Categories.Clear();
            series.Categories.AddRange(Enumerable.Range(0, count).Select(k => Start(k).ToString(CultureInfo.InvariantCulture)));
            var points = series.Points.Where(p => p.Key < dates.Count && dates[p.Key] != null).ToList();
            series.Points.Clear();
            foreach (var (index, point) in points)
                series.Points[Slot(dates[index]!.Value)] = point;
        }
        group.TimeUnit = unit;
    }

    /// <summary>Serial dates of the 1904 system (c:date1904) in the 1900 one, for date-formatted categories.</summary>
    static void ShiftDates(ChartGroup group)
    {
        foreach (var series in group.Series.Where(s => NumberFormat.IsDateFormat(s.CategoryFormatCode)))
        {
            for (var i = 0; i < series.Categories.Count; i++)
            {
                if (double.TryParse(series.Categories[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    series.Categories[i] = (d + 1462).ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    static ChartSeries ReadSeries(XElement ser, CultureInfo culture)
    {
        var (values, format) = Numbers(ser.Element(Ns.C + "val") ?? ser.Element(Ns.C + "yVal"));
        var categories = ser.Element(Ns.C + "cat") ?? ser.Element(Ns.C + "xVal");
        var (xValues, _) = Numbers(ser.Element(Ns.C + "xVal"));
        var (bubbleSizes, _) = Numbers(ser.Element(Ns.C + "bubbleSize"));
        return new ChartSeries
        {
            BubbleSizes = bubbleSizes,
            Element = ser,
            Index = ser.Element(Ns.C + "idx").Int("val") ?? 0,
            Order = ser.Element(Ns.C + "order").Int("val") ?? 0,
            Name = Strings(ser.Element(Ns.C + "tx"), culture).FirstOrDefault() ?? "",
            Categories = Strings(categories, culture),
            OuterCategories = OuterLevels(categories),
            Values = values,
            XValues = xValues,
            FormatCode = format,
            CategoryFormatCode = Cache(categories)?.Element(Ns.C + "formatCode")?.Value ?? "General",
            Points = ser.Elements(Ns.C + "dPt").Where(p => p.Element(Ns.C + "idx") != null)
                .GroupBy(p => p.Element(Ns.C + "idx").Int("val") ?? 0).ToDictionary(g => g.Key, g => g.Last()),
        };
    }

    /// <summary>
    /// An axis's side as its chart type has it: a bar chart's category axis is vertical and its value axis horizontal,
    /// column, line and area charts the other way round, whatever c:axPos says (measured: a bar chart saved with its
    /// category axis at "b" shows horizontal bars with the categories on the left). A side across is mirrored (b and l,
    /// t and r); scatter, bubble and radar charts keep theirs.
    /// </summary>
    static string Side(string position, string kind, ChartGroup? group)
    {
        if (group == null || kind == "serAx" || group.Element.Name.LocalName is not ("barChart" or "bar3DChart" or "lineChart"
                or "line3DChart" or "areaChart" or "area3DChart" or "stockChart"))
            return position;
        var vertical = kind is "catAx" or "dateAx" ? group.Horizontal : !group.Horizontal;
        if (vertical == (position is "l" or "r"))
            return position;
        return position switch { "b" => "l", "t" => "r", "l" => "b", "r" => "t", _ => position };
    }

    static ChartAxis ReadAxis(XElement element, IReadOnlyList<ChartGroup> groups)
    {
        var scaling = element.Element(Ns.C + "scaling");
        var numFmt = element.Element(Ns.C + "numFmt");
        var crossesAt = Number(element, "crossesAt");
        var id = element.Element(Ns.C + "axId").Int("val") ?? 0;
        return new ChartAxis
        {
            Element = element,
            Id = id,
            Position = Side(Val(element, "axPos") ?? "l", element.Name.LocalName, groups.FirstOrDefault(g => g.AxisIds.Contains(id))),
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
            "strLit" or "numLit" or "multiLvlStrLit" => source,
            _ => null,
        };
    }

    /// <summary>Text values of c:tx or c:cat (the innermost level of multi-level categories).</summary>
    static List<string> Strings(XElement? data, CultureInfo culture)
    {
        if (data?.Element(Ns.C + "v") is { } literal)
            return [literal.Value];
        var cache = Cache(data);
        if (cache == null)
            return [];
        if (cache.Name.LocalName is "multiLvlStrCache" or "multiLvlStrLit")
            cache = cache.Element(Ns.C + "lvl") is { } level ? new XElement(cache.Name, cache.Element(Ns.C + "ptCount"), level.Elements()) : cache;
        var format = cache.Element(Ns.C + "formatCode")?.Value;
        return Points(cache).Select(v => v == null ? "" : format != null && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? NumberFormat.Format(d, format, culture) : v).ToList();
    }

    /// <summary>The levels of multi-level categories beyond the innermost (c:multiLvlStrRef or c16ac:multiLvlStrLit).</summary>
    static List<List<string?>> OuterLevels(XElement? data)
    {
        var source = data?.Elements().FirstOrDefault();
        var cache = source?.Name.LocalName switch
        {
            "multiLvlStrRef" => source.Element(Ns.C + "multiLvlStrCache"),
            "multiLvlStrLit" => source,
            _ => null,
        };
        if (cache == null)
            return [];
        var count = cache.Element(Ns.C + "ptCount").Int("val") ?? 0;
        return cache.Elements(Ns.C + "lvl").Skip(1).Select(level =>
        {
            var names = new List<string?>(Enumerable.Repeat<string?>(null, count));
            foreach (var pt in level.Elements(Ns.C + "pt"))
            {
                if (pt.Int("idx") is { } i && i >= 0 && i < count)
                    names[i] = pt.Element(Ns.C + "v")?.Value;
            }
            return names;
        }).ToList();
    }

    /// <summary>The cached numbers of c:val, c:xVal, c:plus … (c:numRef or c:numLit) with their format code.</summary>
    public static (List<double?> Values, string Format) Numbers(XElement? data)
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
