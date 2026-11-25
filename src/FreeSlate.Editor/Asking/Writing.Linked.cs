namespace FreeSlate.Editor.Asking;

/// <summary>
/// Boxes whose text flows from one into the next (```box link=next, see Groups): the write makes the first box of a chain hold the story,
/// and each box written after it, other blocks between them or not, go on with it, until one without link=next ends the chain.
/// </summary>
sealed partial class Writing
{
    /// <summary>The chain of linked boxes the write is making: its story and the place in it of the next box; null where none goes on.</summary>
    (int Story, int Seq)? chain;

    /// <summary>A block made at the place blocks go to now (see Markup.Of), a box linked into the chain going on or beginning one.</summary>
    Markup.Made Made(Notation.Block block)
    {
        if (word == null || block is not Notation.Fenced { Kind: "box" } box || chain == null && !Groups.LinksOn(box.Settings))
            return Markup.Of(block, PlaceAt());
        var (story, seq) = chain ?? (word.NewStoryId(), 0);
        var made = Groups.Linked(box, PlaceAt(), story, seq, problems);
        chain = Groups.LinksOn(box.Settings) ? (story, seq + 1) : null;
        return made;
    }
}
