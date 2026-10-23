using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>
/// Changes made as tracked changes (catalog: review.revisions), as Word makes them with Track Changes on: by an author
/// at a time, each change an element of its own (w:ins, w:del, w:rPrChange, w:pPrChange) with an id of its own.
/// </summary>
public sealed class Tracking(string author, DateTime date, int firstId)
{
    int next = firstId;

    /// <summary>Who makes the changes, and when.</summary>
    public string Author { get; } = author;
    public DateTime Date { get; } = date;

    /// <summary>The id the next change takes, one more than the last.</summary>
    public int NextId => next;

    /// <summary>A change's element, with its id, the author and the time (to the second, as Word writes it).</summary>
    internal XElement Mark(XNamespace w, string name) =>
        new(w + name, new XAttribute(w + "id", (next++).ToString(CultureInfo.InvariantCulture)), new XAttribute(w + "author", Author),
            new XAttribute(w + "date", Date.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)));

    /// <summary>A copy of a change's element with nothing in it and an id of its own (for a change split in two).</summary>
    internal XElement Copy(XElement change) =>
        new(change.Name, change.Attributes().Select(a => a.Name.LocalName == "id" ? new XAttribute(a.Name, (next++).ToString(CultureInfo.InvariantCulture)) : new XAttribute(a)));

    /// <summary>Whether a change is this author's.</summary>
    internal bool Own(XElement change) => (string?)change.Attribute(change.Name.Namespace + "author") == Author;
}
