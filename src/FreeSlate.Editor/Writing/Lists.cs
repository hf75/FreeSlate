namespace FreeSlate.Editor.Writing;

/// <summary>
/// The numbered lists of a document, as its paragraphs' levels make them (see Levels): numbered points one after another, with the points
/// and subpoints among them, are one list, which counts from 1. Each list is a list of its own of the tone's numbered points (a numbering
/// instance of the tone's definition, see WordDocument.AddList), which each of its numbered paragraphs names, at its depth, as Word's
/// numbering gives a paragraph its list: a list that begins after text so starts at 1 however many lists came before, and text put
/// between two of its points leaves the points after it counting on.
/// </summary>
public static class Lists
{
    /// <summary>The tone's numbering definition of numbered points (Tones/*.xml, w:abstractNum 9002).</summary>
    public const string Definition = "9002";

    /// <summary>
    /// Settles the lists after paragraphs were given levels (as one step with that): each numbered one takes the list of the numbered
    /// points around it (the first of them with a list of its own, before or after it, points and subpoints between), or, where there is
    /// none, the list <paramref name="continuing"/> names (see Continued), else a new list starting at <paramref name="start"/>, all of them
    /// then in that list at their depths; each that is no numbered point any more gives up a numbered points' list of its own, so that its
    /// style says what it is.
    /// </summary>
    public static void Settle(IEditableDocument document, IEnumerable<int> paragraphs, int start = 1, string? continuing = null)
    {
        if (document is not WordDocument word)
            return;
        var settled = new HashSet<int>();
        foreach (var paragraph in paragraphs.Distinct().ToList())
        {
            if (settled.Contains(paragraph))
                continue;
            var level = Levels.Of(word.ParagraphStyle(paragraph));
            if (!level.Numbered)
            {
                if (word.OwnList(paragraph).List is { Length: > 0 } && word.ListDefinition(paragraph) == Definition)
                    word.Format(paragraph, new ParagraphFormatting { List = "" });
                continue;
            }
            // The run of points around it: the paragraphs of lists just before and after it, up to text or a table.
            var run = new List<int> { paragraph };
            for (var p = word.Before(paragraph); p is { } before && Levels.Of(word.ParagraphStyle(before)).Depth > 0; p = word.Before(before))
                run.Insert(0, before);
            for (var p = word.After(paragraph); p is { } after && Levels.Of(word.ParagraphStyle(after)).Depth > 0; p = word.After(after))
                run.Add(after);
            var numbered = run.Where(p => Levels.Of(word.ParagraphStyle(p)).Numbered).ToList();
            var list = numbered.Select(p => word.OwnList(p).List is { Length: > 0 } own && word.ListDefinition(p) == Definition ? own : null)
                .FirstOrDefault(own => own != null) ?? continuing;
            if (list == null)
            {
                try
                {
                    list = word.AddList(Definition, start);
                }
                catch (NotSupportedException)
                {
                    // A document without the tone's numbered points (its own styles): its List Number style numbers them as it does.
                    settled.UnionWith(numbered);
                    continue;
                }
            }
            foreach (var p in numbered)
            {
                var depth = Levels.Of(word.ParagraphStyle(p)).Depth - 1;
                if (word.OwnList(p) != (list, depth))
                    word.Format(p, new ParagraphFormatting { List = list, ListLevel = depth });
            }
            settled.UnionWith(numbered);
        }
    }

    /// <summary>
    /// The list of the numbered points right before a paragraph (points and subpoints between them and it), where it is one of the tone's,
    /// which a numbered point put there counts on in; null where none is.
    /// </summary>
    public static string? Before(IEditableDocument document, int paragraph)
    {
        if (document is not WordDocument word)
            return null;
        for (var p = word.Before(paragraph); p is { } before && Levels.Of(word.ParagraphStyle(before)) is { Depth: > 0 } level; p = word.Before(before))
        {
            if (level.Numbered && word.OwnList(before).List is { Length: > 0 } list && word.ListDefinition(before) == Definition)
                return list;
        }
        return null;
    }

    /// <summary>
    /// The list a numbered point goes on in that begins a list with a number of its own, as "4." after text says it: the list of the
    /// nearest numbered point before it, where that shows the number before; null where it does not (the point then begins a list of its
    /// own at its number), or for 1.
    /// </summary>
    public static string? Continued(IEditableDocument document, int paragraph, int? number)
    {
        if (number is not > 1 || document is not WordDocument word)
            return null;
        for (var p = word.Previous(paragraph); p is { } before; p = word.Previous(before))
        {
            if (Levels.Of(word.ParagraphStyle(before)) is not { Numbered: true, Depth: 1 })
                continue;
            var label = word.ListLabel(before) ?? "";
            var digits = new string([.. label.TakeWhile(char.IsAsciiDigit)]);
            return int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var shown)
                && shown + 1 == number && word.OwnList(before).List is { Length: > 0 } list && word.ListDefinition(before) == Definition ? list : null;
        }
        return null;
    }
}
