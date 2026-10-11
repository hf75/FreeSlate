namespace Fidelity.Word;

/// <summary>Generated documents of Office Math: display equations in paragraphs of their own and equations in text.</summary>
public static partial class DocumentGenerator
{
    /// <summary>
    /// Equations typed in Word's linear format and built up by Word: scripts, fractions, radicals, n-ary operators with
    /// limits, delimiters, matrices, accents, functions; display equations centered and left, and inline equations.
    /// </summary>
    static void Equations(Writer w)
    {
        w.Paragraph("Equations", StyleHeading1);
        w.Paragraph("Display equations stand in paragraphs of their own:");
        string[] display =
        [
            "a^2+b^2=c^2",
            "x=(-b±√(b^2-4ac))/2a",
            "∑_(i=1)^n▒i=(n(n+1))/2",
            "∫_0^∞▒e^(-x^2) dx=√π/2",
            "A=[■(1&0&0@0&1&0@0&0&1)]",
            "f(x)=a_0+∑_(n=1)^∞▒(a_n cos⁡〖nπx/L〗+b_n sin⁡〖nπx/L〗)",
            "lim┬(n→∞)⁡〖(1+1/n)^n〗=e",
            "∇×E=-∂B/∂t",
            "v⃗=(x̅,y̅)",
            "█(x+y=1@x-y=0)",
        ];
        foreach (var linear in display)
            Equation(w, w.Paragraph(linear), 0, linear.Length);
        w.Paragraph("Equations in text:", StyleHeading2);
        string[] inline = ["E=mc^2", "x^2", "√2", "a/b", "∑_(k=1)^n▒k"];
        foreach (var linear in inline)
        {
            var before = "The term ";
            var paragraph = w.Paragraph(before + linear + " stands in a line of text that continues after it for a while, so that the line breaks.");
            Equation(w, paragraph, before.Length, linear.Length);
        }
        w.Paragraph("A display equation too long for its line:", StyleHeading2);
        var longer = "y=a_0+a_1 x+a_2 x^2+a_3 x^3+a_4 x^4+a_5 x^5+a_6 x^6+a_7 x^7+a_8 x^8+a_9 x^9+a_10 x^10+a_11 x^11+a_12 x^12+a_13 x^13";
        Equation(w, w.Paragraph(longer), 0, longer.Length);
        w.Paragraph("A display equation aligned left:", StyleHeading2);
        var left = w.Paragraph("y=mx+b");
        Equation(w, left, 0, 6);
        w.Document.OMaths.Item(w.Document.OMaths.Count).Justification = 3;   // wdOMathJcLeft
    }

    /// <summary>Turns characters of a paragraph into an equation and builds it up from the linear format.</summary>
    static void Equation(Writer w, dynamic paragraph, int start, int length)
    {
        int from = paragraph.Range.Start + start;
        var range = w.Document.Range(from, from + length);
        var math = w.Document.OMaths.Add(range);
        math.OMaths.Item(1).BuildUp();
    }
}
