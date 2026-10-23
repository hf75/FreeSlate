namespace FreeSlate.Docx;

/// <summary>
/// A paragraph's text as places in it are counted (a cursor stands between two of its characters, the selection takes
/// some of them): its inlines one after the other, text as its characters, a tab, a break, a field the layout computes,
/// a drawing (inline or floating), an equation, a note reference, a check box, a ruby, a separator as one character
/// each, bookmarks, permissions and comment marks as none. An offset names the place before the character at it, the
/// paragraph's length the place before its mark.
/// </summary>
public static class ParagraphText
{
    /// <summary>How many characters an inline takes in its paragraph's text.</summary>
    public static int Length(Inline inline) => inline switch
    {
        TextInline text => text.Text.Length,
        BookmarkInline or BookmarkEndInline or PermissionInline or CommentInline => 0,
        _ => 1,
    };

    /// <summary>
    /// A paragraph's text: what its text inlines say, a tab as "\t", a line break as "\v", a page break as "\f", a column
    /// break as U+000E (as Word's own text of a range has them), every other inline that takes a character as U+FFFC.
    /// </summary>
    public static string Of(ParagraphBlock paragraph)
    {
        var text = new System.Text.StringBuilder();
        foreach (var inline in paragraph.Content)
        {
            switch (inline)
            {
                case TextInline t:
                    text.Append(t.Text);
                    break;
                case TabInline:
                    text.Append('\t');
                    break;
                case BreakInline b:
                    text.Append(b.Type switch { "page" => '\f', "column" => '\u000E', _ => '\v' });
                    break;
                default:
                    if (Length(inline) > 0)
                        text.Append('￼');
                    break;
            }
        }
        return text.ToString();
    }

    /// <summary>The offset at which an inline of the paragraph begins (its index in Content), from the paragraph's TextOffset.</summary>
    public static int Start(ParagraphBlock paragraph, int inline)
    {
        var at = paragraph.TextOffset;
        for (var i = 0; i < inline; i++)
            at += Length(paragraph.Content[i]);
        return at;
    }

    /// <summary>The offset of the paragraph's mark: the length of its text.</summary>
    public static int End(ParagraphBlock paragraph) => Start(paragraph, paragraph.Content.Count);
}
