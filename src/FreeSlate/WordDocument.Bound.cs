namespace FreeSlate;

/// <summary>
/// Content controls bound to data as the data changes (w:dataBinding, see Docx.DataBinding): Word shows a bound control's node, not the
/// text the control keeps (measured in Word 365 by COM: a cover's Title control bound to the title shows the title set after it), so a
/// change of the document's properties or of a custom XML item (a cover's title set by SetProperties, its date by
/// SetCoverPageProperties) shows in the controls bound to them: the body read whole again where it holds one, the headers and footers
/// that hold one read anew, by a change and by undoing or doing it again alike.
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>Whether a part holds data a content control may be bound to: a custom XML item, the core or the extended properties.</summary>
    bool DataStore(string part) =>
        part.StartsWith("/customXml/", StringComparison.OrdinalIgnoreCase)
        || string.Equals(part, PackagePart("core-properties"), StringComparison.OrdinalIgnoreCase)
        || string.Equals(part, PackagePart("extended-properties"), StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the body as it reads now (its edits not read whole yet too) holds a content control bound to data.</summary>
    bool BodyBound() =>
        whole.AsSpan().IndexOf("dataBinding"u8) >= 0
        || islands.Any(island => island.Runs.Any(run => run.Source.AsSpan(run.From, run.Length).Contains("dataBinding", StringComparison.Ordinal)));

    /// <summary>The headers and footers read whose parts hold a content control bound to data.</summary>
    List<string> BoundStories() =>
        [.. reader.Stories.Keys.Where(part => package.Exists(part) && package.ReadBytes(part).AsSpan().IndexOf("dataBinding"u8) >= 0)];
}
