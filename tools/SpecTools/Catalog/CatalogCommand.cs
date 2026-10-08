namespace SpecTools.Catalog;

/// <summary>Builds the section index and the format catalog and writes both to catalog/.</summary>
public static class CatalogCommand
{
    public static void Run()
    {
        var registry = NamespaceRegistry.Load();

        Console.WriteLine("Reading specification outlines");
        var spec = SpecIndex.Build();
        Console.WriteLine($"  ECMA-376 sections: {spec.Ecma.Count}, MS-* sections: {spec.Ms.Count}, MS-OI29500 sections: {spec.OfficeNotes.Count}, field codes: {spec.FieldCodes.Count}");

        Console.WriteLine("Compiling schemas");
        var builder = new CatalogBuilder(registry, spec);
        var namespaces = builder.Build();
        Console.WriteLine($"  {namespaces.Count} namespaces, {builder.Errors.Count} compile errors");
        foreach (var error in builder.Errors)
            Console.WriteLine($"  ! {error}");

        var drawing = namespaces.Single(n => n.Uri == "http://schemas.openxmlformats.org/drawingml/2006/main");
        List<string> Values(string type) => drawing.SimpleTypes.Single(t => t.Name == type).Values ?? [];

        var data = new CatalogData(
            namespaces,
            spec,
            Extras.ReadPresets(Path.Combine(Sources.GeometriesDir, "presetShapeDefinitions.xml")),
            Extras.ReadPresets(Path.Combine(Sources.GeometriesDir, "presetTextWarpDefinitions.xml")),
            Extras.ReadArtBorders(Sources.ArtBordersDir),
            Values("a:ST_ShapeType"),
            Values("a:ST_TextShapeType"),
            builder.UsedOfficeNotes,
            builder.Errors,
            builder.Errata,
            builder.SchemaFiles,
            spec.MsVersions.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => (v.Key, v.Value)).ToList());

        CatalogWriter.Write(data);

        var inScope = namespaces.Where(n => n.InScope).Select(n => n.Counts!).ToList();
        Console.WriteLine($"""
            In scope: {inScope.Count} namespaces, {inScope.Sum(c => c.Elements)} elements, {inScope.Sum(c => c.Attributes + c.GlobalAttributes)} attributes,
                      {inScope.Sum(c => c.SimpleTypes)} simple types, {inScope.Sum(c => c.EnumValues)} enum values
            Presets: {data.PresetShapes.Count} shapes, {data.TextWarps.Count} text warps; {data.ArtBorders.Count} art borders; {spec.FieldCodes.Count} field codes
            Written to {Path.GetRelativePath(Repo.Root, Repo.Catalog)}
            """);
    }
}
