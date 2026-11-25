using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FreeSlate.Docx;

namespace FreeSlate;

/// <summary>
/// Grouped shapes and linked text boxes as Word keeps them (catalog: drawing.groups, drawing.textBoxes): a group (wpg:wgp) holds shapes
/// (wps:wsp) in its own space (a:chOff, a:chExt mapped onto its extent), each with its text where it has one; text boxes whose text flows
/// from one into the next share a story, its text in the first (wps:txbx id) and each box after it naming the story and its place in the
/// chain (wps:linkedTxbx id, seq), anywhere in the body or within a group.
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>
    /// The paragraphs of each shape of the group at a place of a paragraph's text, in the order its shapes stand in it (wps:wsp in document
    /// order, nested groups' too); none for a shape without text of its own (one that goes on with a linked story too). Empty where no
    /// group stands there.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<int>> GroupTexts(int paragraph, int offset)
    {
        var p = Find(paragraph).Paragraph;
        var at = p.TextOffset;
        foreach (var inline in p.Content)
        {
            var length = ParagraphText.Length(inline);
            if (at == offset && length > 0)
            {
                var (drawing, children) = inline switch { DrawingInline d => (d.Drawing, d.ChildText), AnchorInline a => (a.Anchor, a.ChildText), _ => (null, null) };
                if (drawing?.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Drawings.Wpg + "wgp") is not { } group)
                    return [];
                return [.. group.Descendants(Drawings.Wps + "wsp").Select(shape =>
                    (IReadOnlyList<int>)(children?.FirstOrDefault(c => ReferenceEquals(c.Shape, shape)).Text is { } text ? text.SelectMany(Flat).Select(b => b.Id).ToList() : []))];
            }
            at += length;
        }
        return [];
    }

    /// <summary>
    /// The chain of linked text boxes the box at a place of a paragraph's text is in: its story (wps:txbx id of the first box, wps:linkedTxbx
    /// id of those after it), its place in the chain (0 the first), and whether a box after it goes on with the story; null for a box
    /// alone or another drawing.
    /// </summary>
    public (int Story, int Seq, bool More)? TextBoxLink(int paragraph, int offset)
    {
        if (DrawingAt(Find(paragraph).Paragraph, offset)?.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData")?.Element(Drawings.Wps + "wsp") is not { } shape)
            return null;
        int? Number(XElement? e, string name) => int.TryParse((string?)e?.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
        var linked = shape.Element(Drawings.Wps + "linkedTxbx");
        if ((linked != null ? Number(linked, "id") : Number(shape.Element(Drawings.Wps + "txbx"), "id")) is not { } story)
            return null;
        var seq = linked != null ? Number(linked, "seq") ?? 1 : 0;
        Read();
        var more = LinkedBoxes().Any(l => l.Story == story && l.Seq > seq);
        return more || seq > 0 ? (story, seq, more) : null;
    }

    /// <summary>The boxes of the main part that go on with a story (wps:linkedTxbx), each its story and place in the chain, as the part reads now.</summary>
    IEnumerable<(int Story, int Seq)> LinkedBoxes() =>
        islands.Select(i => i.Text).Prepend(wholeText.Text).SelectMany(text => LinkedBox().Matches(text))
            .Select(m => (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)));

    [GeneratedRegex(@"<wps:linkedTxbx\b[^>]*?\sid=""(\d{1,9})""[^>]*?\sseq=""(\d{1,9})""")]
    private static partial Regex LinkedBox();

    [GeneratedRegex(@"<wps:(?:txbx|linkedTxbx)\b[^>]*?\sid=""(\d{1,9})""")]
    private static partial Regex StoryId();

    /// <summary>A number for a new story of linked text boxes (wps:txbx id), one no story nor drawing of the main part has.</summary>
    public int NewStoryId()
    {
        Read();
        var used = islands.Select(i => i.Text).Prepend(wholeText.Text).SelectMany(text => StoryId().Matches(text))
            .Select(m => int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0).DefaultIfEmpty(0).Max();
        var id = NewDrawingId();
        while (id <= used)
            id = NewDrawingId();
        return id;
    }
}
