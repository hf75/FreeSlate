using System.Text;
using System.Xml.Linq;
using FreeSlate.Equations;

namespace FreeSlate.Pptx;

/// <summary>
/// The text of a slide paragraph (a:p) in which places count (see PlacesItem): a run's text (a:t) as its characters, a
/// line break (a:br) as "\v" (as PowerPoint's own text of a range has it), a field (a:fld) and an equation (a14:m) as
/// U+FFFC each, whatever they show; the paragraph's mark stands after it, at its length.
/// </summary>
public static class SlideText
{
    /// <summary>How many characters a child of a paragraph takes in its text.</summary>
    public static int Length(XElement element) => element.Name.LocalName switch
    {
        "r" when element.Name.Namespace == Ns.A => element.Element(Ns.A + "t")?.Value.Length ?? 0,
        "br" or "fld" when element.Name.Namespace == Ns.A => 1,
        "m" when element.Name.Namespace == Equation.A14 => 1,
        _ => 0,
    };

    /// <summary>A paragraph's text.</summary>
    public static string Of(XElement paragraph)
    {
        var text = new StringBuilder();
        foreach (var element in paragraph.Elements())
        {
            if (element.Name == Ns.A + "r")
                text.Append(element.Element(Ns.A + "t")?.Value);
            else if (element.Name == Ns.A + "br")
                text.Append('\v');
            else if (Length(element) > 0)
                text.Append('￼');
        }
        return text.ToString();
    }
}
