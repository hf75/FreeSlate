using SpecTools;
using SpecTools.Catalog;

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
    case "all":
        await Fetch.RunAsync();
        MsXsdExtractor.Run(NamespaceRegistry.Load());
        CatalogCommand.Run();
        break;
    default:
        Console.WriteLine("""
            SpecTools: builds the format catalog from the OOXML specifications.

              fetch        download ECMA-376 and the Microsoft specs to spec/downloads and unpack them
              extract-xsd  extract the Office extension schemas from the MS-* PDFs to spec/ms-xsd
              catalog      build the section index and the format catalog in catalog/
              all          all of the above
            """);
        break;
}
