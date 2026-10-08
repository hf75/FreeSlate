namespace SpecTools;

/// <summary>Where the specification sources come from and where they live locally.</summary>
public static class Sources
{
    public sealed record Download(string Url, string FileName);

    const string Ecma = "https://ecma-international.org/wp-content/uploads/";
    const string MsOpenSpecs = "https://officeprotocoldocs-f5hpbjgea6b8gneq.b02.azurefd.net/files/";

    public static readonly Download[] Downloads =
    [
        new(Ecma + "ECMA-376-1_5th_edition_december_2016.zip", "ECMA-376-1_5th_edition_december_2016.zip"),
        new(Ecma + "ECMA-376-2_5th_edition_december_2021.zip", "ECMA-376-2_5th_edition_december_2021.zip"),
        new(Ecma + "ECMA-376-3_5th_edition_december_2015.zip", "ECMA-376-3_5th_edition_december_2015.zip"),
        new(Ecma + "ECMA-376-4_5th_edition_december_2016.zip", "ECMA-376-4_5th_edition_december_2016.zip"),
        new(MsOpenSpecs + "MS-OI29500/%5bMS-OI29500%5d.pdf", "MS-OI29500.pdf"),
        new(MsOpenSpecs + "MS-DOCX/%5bMS-DOCX%5d.pdf", "MS-DOCX.pdf"),
        new(MsOpenSpecs + "MS-PPTX/%5bMS-PPTX%5d.pdf", "MS-PPTX.pdf"),
        new(MsOpenSpecs + "MS-ODRAWXML/%5bMS-ODRAWXML%5d.pdf", "MS-ODRAWXML.pdf"),
        new("https://raw.githubusercontent.com/dotnet/Open-XML-SDK/main/data/namespaces.json", "openxml-sdk-namespaces.json"),
    ];

    /// <summary>Microsoft documents whose appendix contains full XML schemas of the Office extensions.</summary>
    public static readonly string[] MsSchemaDocs = ["MS-DOCX", "MS-PPTX", "MS-ODRAWXML"];

    public static string EcmaPart1Pdf => Path.Combine(Repo.Ecma, "part1", "Ecma Office Open XML Part 1 - Fundamentals And Markup Language Reference.pdf");
    public static string EcmaPart4Pdf => Path.Combine(Repo.Ecma, "part4", "Ecma Office Open XML Part 4 - Transitional Migration Features.pdf");
    public static string TransitionalXsdDir => Path.Combine(Repo.Ecma, "part4", "xsd-transitional");
    public static string OpcXsdDir => Path.Combine(Repo.Ecma, "part2", "xsd");
    public static string GeometriesDir => Path.Combine(Repo.Ecma, "part1", "geometries");
    public static string ArtBordersDir => Path.Combine(Repo.Ecma, "part1", "art-borders");
    public static string SdkNamespacesJson => Path.Combine(Repo.Downloads, "openxml-sdk-namespaces.json");
    public static string MsPdf(string doc) => Path.Combine(Repo.Downloads, doc + ".pdf");
}
