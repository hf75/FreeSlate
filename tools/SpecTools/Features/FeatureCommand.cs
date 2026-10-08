using System.Text.Json;
using System.Text.RegularExpressions;
using SpecTools.Catalog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SpecTools.Features;

/// <summary>A feature with its element patterns resolved against the format catalog.</summary>
public sealed record ResolvedFeature(FeatureArea Area, Feature Feature, List<string> Elements, List<SpecRef> Spec, List<string> OfficeNotes);

/// <summary>The checked feature catalog of one document type, ready to be written.</summary>
public sealed record FeatureReport(
    FeatureDocument Document,
    List<ResolvedFeature> Features,
    HashSet<string> Universe,
    List<string> Unassigned,
    List<string> OutsideUniverse,
    List<string> Errors,
    IReadOnlyDictionary<string, ElementEntry> Elements);

/// <summary>Validates the curated feature catalogs (level 2) against the format catalog (level 1) and writes reports.</summary>
public static partial class FeatureCommand
{
    static readonly string[] Priorities = ["P0", "P1", "P2", "P3"];
    static readonly string[] Statuses = ["todo", "partial", "done"];

    public static string FeatureDir => Path.Combine(Repo.Catalog, "features");

    public static int Run()
    {
        var elements = LoadFormatCatalog();
        var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        var failed = false;

        foreach (var file in Directory.GetFiles(FeatureDir, "*.yaml").Order(StringComparer.Ordinal))
        {
            var document = deserializer.Deserialize<FeatureDocument>(File.ReadAllText(file));
            var report = Check(document, elements);
            FeatureWriter.Write(report, Path.GetFileName(file));

            var coverage = report.Universe.Count == 0 ? 0 : 100.0 * (report.Universe.Count - report.Unassigned.Count) / report.Universe.Count;
            Console.WriteLine($"{Path.GetFileName(file)}: {report.Features.Count} features, coverage {coverage:F1} % " +
                              $"({report.Universe.Count - report.Unassigned.Count} of {report.Universe.Count} elements), {report.Errors.Count} errors");
            foreach (var error in report.Errors)
                Console.WriteLine($"  ! {error}");
            failed |= report.Errors.Count > 0;
        }
        return failed ? 1 : 0;
    }

    static Dictionary<string, ElementEntry> LoadFormatCatalog()
    {
        var dir = Path.Combine(Repo.Catalog, "format", "namespaces");
        if (!Directory.Exists(dir))
            throw new InvalidOperationException("Format catalog missing; run `catalog` first.");

        var elements = new Dictionary<string, ElementEntry>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(dir, "*.json"))
        {
            var ns = JsonSerializer.Deserialize<NamespaceEntry>(File.ReadAllText(file), Json.Options)!;
            foreach (var element in ns.Elements)
                elements[element.Name] = element;
        }
        return elements;
    }

    static FeatureReport Check(FeatureDocument document, Dictionary<string, ElementEntry> elements)
    {
        var errors = new List<string>();
        var byPrefix = elements.Keys.GroupBy(Prefix).ToDictionary(g => g.Key, g => g.ToList());

        // Universe: closure over the children of the roots and of all elements of the listed namespaces.
        var excluded = document.Universe.Exclude.ToHashSet();
        var universe = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        foreach (var root in document.Universe.Roots)
        {
            if (elements.ContainsKey(root)) stack.Push(root);
            else errors.Add($"universe: unknown root {root}");
        }
        foreach (var prefix in document.Universe.Namespaces)
        {
            if (byPrefix.TryGetValue(prefix, out var names)) names.ForEach(stack.Push);
            else errors.Add($"universe: unknown namespace {prefix}");
        }
        while (stack.TryPop(out var name))
        {
            if (excluded.Contains(Prefix(name)) || !elements.TryGetValue(name, out var element) || !universe.Add(name))
                continue;
            foreach (var child in element.Children.Where(c => !c.StartsWith("any(", StringComparison.Ordinal)))
                stack.Push(child);
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var features = new List<ResolvedFeature>();
        var named = new HashSet<string>(StringComparer.Ordinal);
        foreach (var area in document.Areas)
        {
            if (!ids.Add(area.Id))
                errors.Add($"duplicate id {area.Id}");
            foreach (var feature in area.Features)
            {
                var where = $"{feature.Id}:";
                if (!feature.Id.StartsWith(area.Id + ".", StringComparison.Ordinal))
                    errors.Add($"{where} id does not start with the area id '{area.Id}.'");
                if (!ids.Add(feature.Id))
                    errors.Add($"{where} duplicate id");
                if (string.IsNullOrWhiteSpace(feature.Title))
                    errors.Add($"{where} missing title");
                if (!Priorities.Contains(feature.Priority))
                    errors.Add($"{where} priority must be one of {string.Join(", ", Priorities)}");
                if (feature.Difficulty is < 1 or > 5)
                    errors.Add($"{where} difficulty must be 1 to 5");
                if (!Statuses.Contains(feature.Status))
                    errors.Add($"{where} status must be one of {string.Join(", ", Statuses)}");

                var resolved = new List<string>();
                foreach (var pattern in feature.Elements)
                {
                    var matches = Expand(pattern, byPrefix);
                    if (matches.Count == 0)
                        errors.Add($"{where} unknown element {pattern}");
                    resolved.AddRange(matches);
                    named.UnionWith(matches);
                }
                foreach (var prefix in feature.Namespaces)
                {
                    if (byPrefix.TryGetValue(prefix, out var names)) resolved.AddRange(names);
                    else errors.Add($"{where} unknown namespace {prefix}");
                }
                resolved = resolved.Distinct().Order(StringComparer.Ordinal).ToList();

                var spec = resolved.SelectMany(e => elements[e].Spec)
                    .DistinctBy(r => (r.Doc, r.Section))
                    .OrderBy(r => r.Doc, StringComparer.Ordinal).ThenBy(r => SectionKey(r.Section), StringComparer.Ordinal)
                    .ToList();
                var notes = spec.SelectMany(r => r.OfficeNotes ?? []).Distinct().Order(StringComparer.Ordinal).ToList();
                features.Add(new ResolvedFeature(area, feature, resolved, spec, notes));
            }
        }

        var assigned = features.SelectMany(f => f.Elements).ToHashSet(StringComparer.Ordinal);
        var unassigned = universe.Where(e => !assigned.Contains(e)).Order(StringComparer.Ordinal).ToList();
        // Whole namespaces may contain elements of other document types; only named elements are suspicious.
        var outside = named.Where(e => !universe.Contains(e)).Order(StringComparer.Ordinal).ToList();
        return new FeatureReport(document, features, universe, unassigned, outside, errors, elements);
    }

    static List<string> Expand(string pattern, Dictionary<string, List<string>> byPrefix)
    {
        var prefix = Prefix(pattern);
        if (!byPrefix.TryGetValue(prefix, out var names))
            return [];
        if (!pattern.Contains('*') && !pattern.Contains('?'))
            return names.Contains(pattern) ? [pattern] : [];

        var regex = new Regex("^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$");
        return names.Where(n => regex.IsMatch(n)).ToList();
    }

    static string Prefix(string qualifiedName)
    {
        var colon = qualifiedName.IndexOf(':');
        return colon < 0 ? "" : qualifiedName[..colon];
    }

    /// <summary>Sort key that orders "17.3.1.9" before "17.3.1.10".</summary>
    public static string SectionKey(string section) =>
        string.Join('.', section.Split('.').Select(p => p.PadLeft(5, '0')));
}
