using System.Globalization;
using SpecTools;
using SpecTools.Catalog;
using SpecTools.Features;

// Generated files must not depend on the machine's culture (decimal separators, number grouping).
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var command = args.FirstOrDefault() ?? "help";
switch (command)
{
    case "fetch":
        await Fetch.RunAsync();
        break;
    case "extract-xsd":
        MsXsdExtractor.Run(NamespaceRegistry.Load());
        break;
    case "catalog":
        CatalogCommand.Run();
        break;
    case "features":
        return FeatureCommand.Run();
    case "text" when args.Length == 3:
        return SpecText.Print(args[1], args[2]);
    case "all":
        await Fetch.RunAsync();
        MsXsdExtractor.Run(NamespaceRegistry.Load());
        CatalogCommand.Run();
        return FeatureCommand.Run();
    default:
        Console.WriteLine("""
            SpecTools: builds the format catalog from the OOXML specifications.

              fetch        download ECMA-376 and the Microsoft specs to spec/downloads and unpack them
              extract-xsd  extract the Office extension schemas from the MS-* PDFs to spec/ms-xsd
              catalog      build the section index and the format catalog in catalog/
              features     check the curated feature catalogs (catalog/features/*.yaml) and write their reports
              text D S     print section S of document D (ECMA-376-1, ECMA-376-4, MS-OI29500 …); for MS-OI29500 S
                           may be the ISO/IEC 29500 section the notes are about
              all          all of the above
            """);
        break;
}
return 0;
