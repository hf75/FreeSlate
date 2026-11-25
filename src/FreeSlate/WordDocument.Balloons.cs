using FreeSlate.Docx;
using FreeSlate.Docx.Layout;

namespace FreeSlate;

/// <summary>
/// The markup view's balloons on the document's pages, as DocxConverter draws them (catalog: review.revisions, review.comments): read
/// with its markup, the pages have the comments, and the gray pane of balloons beside them where the document has balloons (comments,
/// formatting changes of a run, a paragraph or a table), as Word shows all markup. Without them the pages of a document with markup had
/// its changes in the text but no balloons, where Word's PDF of the same document has them (Fidelity's markup-* documents).
/// </summary>
public sealed partial class WordDocument
{
    /// <summary>
    /// The renderer of the pages laid out from blocks a reader read: in the markup view with the comments and, where the document has
    /// balloons, their pane.
    /// </summary>
    PageRenderer NewRenderer(ParagraphLayout laying, ModelReader from, IEnumerable<Block> read)
    {
        var comments = markup ? from.Comments() : [];
        return new PageRenderer(context, new Drawings(context, culture), new TableLayout(context, laying))
        {
            Comments = comments,
            MarkupPane = markup && (comments.Count > 0 || Balloons(read)),
        };
    }

    /// <summary>Whether blocks hold a formatting change the markup view shows in a balloon: a run's, a paragraph's, a table's.</summary>
    static bool Balloons(IEnumerable<Block> blocks) => blocks.Any(block => block switch
    {
        ParagraphBlock paragraph => paragraph.Change != null || paragraph.Content.Any(inline => inline.Format.Change != null),
        TableBlock table => table.Table.Change != null || table.Table.Rows.Any(row => row.Cells.Any(cell => Balloons(cell.Blocks))),
        _ => false,
    });
}
