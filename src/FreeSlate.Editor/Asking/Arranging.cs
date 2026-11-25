using System.Globalization;
using System.Text.Json;
using FreeSlate.Editing;
using FreeSlate.Editor.Controls;
using FreeSlate.Editor.Writing;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// The tools Claude changes the document with besides writing (see Prompt, Talk): the picture tool does with an object what the ring does
/// with a picture (where it stands, its size, its crop, colours, frame, turn, mirror, tilt, caption and description, or takes it away),
/// each change a handling of the session's as the ring's are; the tone tool gives the document a tone as its buttons do.
/// </summary>
static class Arranging
{
    /// <summary>The ring's crops (see EditorView's Cropped): proportions of width to height, the whole picture, a circle, a rectangle.</summary>
    static readonly Dictionary<string, double> Proportions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1:1"] = 1, ["4:3"] = 4 / 3.0, ["3:4"] = 3 / 4.0, ["3:2"] = 3 / 2.0, ["2:3"] = 2 / 3.0, ["16:9"] = 16 / 9.0, ["9:16"] = 9 / 16.0,
    };

    /// <summary>The places the picture tool names (see Objects.Details), as the ring's placements.</summary>
    static readonly Dictionary<string, Placement> Places = new(StringComparer.OrdinalIgnoreCase)
    {
        ["inline"] = Placement.Inline, ["left"] = Placement.Left, ["right"] = Placement.Right, ["own"] = Placement.Own,
        ["behind"] = Placement.Behind, ["front"] = Placement.Front,
    };

    /// <summary>
    /// The picture tool: the object its number names (else the focus's) changed as its input says, in the order the ring's rings go (where
    /// it stands, its size, crop, colours, frame, turn, mirror, tilt, caption, description); what is told Claude (the object as it then is),
    /// and whether nothing could be done.
    /// </summary>
    public static (string Text, bool Error) Picture(EditSession editing, DocumentView sheets, Objects objects, Focus focus, string json)
    {
        JsonElement input;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            input = parsed.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ("The input of picture is a JSON object.", true);
        }
        if (input.ValueKind != JsonValueKind.Object)
            return ("The input of picture is a JSON object.", true);
        var document = editing.Document;
        // The object's number, also as its token or a text of it ("[picture 3]", "3").
        int? id = !input.TryGetProperty("object", out var given) ? null
            : given.ValueKind == JsonValueKind.Number && given.TryGetInt32(out var number) ? number
            : given.ValueKind == JsonValueKind.String && System.Text.RegularExpressions.Regex.Match(given.GetString() ?? "", @"\d+") is { Success: true } digits
                && int.TryParse(digits.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var read) ? read : null;
        (int Paragraph, int Offset)? place = id is { } wanted ? objects.Find(wanted)
            : focus.Kind == Focus.Kinds.Picture ? (focus.First, focus.Offset) : null;
        if (place is not var (paragraph, offset) || document.DrawingOf(paragraph, offset) == null)
            return (id is { } missing ? $"Object {missing.ToString(CultureInfo.InvariantCulture)} does not stand in the document (as a drawing)." : "Give the object's number: the focus is no object.", true);
        id ??= objects.IdAt(paragraph, offset);
        string? Text(string name) => input.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        // A flag or a number also where it comes as a text ("yes", "15°").
        bool? Flag(string name) => !input.TryGetProperty(name, out var value) ? null
            : value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean()
            : value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim().ToLowerInvariant() switch { "true" or "yes" or "on" => true, "false" or "no" or "off" or "none" => false, _ => null } : null;
        double? Number(string name) => !input.TryGetProperty(name, out var value) ? null
            : value.ValueKind == JsonValueKind.Number ? value.GetDouble()
            : value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString()?.Trim().TrimEnd('°'), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        var failed = new List<string>();
        var (removed, succeeded) = (false, 0);
        sheets.Edit(() =>
        {
            void Select()
            {
                editing.Place(new TextPlace(paragraph, offset));
                editing.Place(new TextPlace(paragraph, offset + 1), extend: true);
            }
            // Each change as the ring makes it, the drawing selected again after it where it moved (its character's place).
            void Do(string what, Func<bool> change)
            {
                try
                {
                    Select();
                    if (!change())
                    {
                        failed.Add(what);
                        return;
                    }
                    succeeded++;
                    if (editing.Selecting && editing.Selection is var (start, end) && start.Paragraph == end.Paragraph && end.Offset - start.Offset == 1
                        && document.DrawingOf(start.Paragraph, start.Offset) != null)
                        (paragraph, offset) = (start.Paragraph, start.Offset);
                }
                catch (Exception e) when (e is NotSupportedException or ArgumentException or InvalidOperationException)
                {
                    failed.Add($"{what} ({e.Message})");
                }
            }
            if (Flag("remove") == true)
            {
                Do("remove", () =>
                {
                    editing.Delete();
                    return true;
                });
                removed = !failed.Any();
                return;
            }
            if (Text("place") is { } where)
            {
                if (Places.TryGetValue(where.Trim(), out var placement))
                    Do("place", () =>
                    {
                        editing.Format(placement.Format);
                        return true;
                    });
                else
                    failed.Add($"place {where} (inline, left, right, own, behind or front)");
            }
            if (Text("width") is { } width)
            {
                var room = document is WordDocument word ? word.TextWidth(paragraph) : 450;
                if (Markup.Length(width, room) is { } points && document.DrawingOf(paragraph, offset) is { Width: > 0, Height: > 0 } shown)
                    Do("width", () => editing.SizeDrawing(points, points * shown.Height!.Value / shown.Width!.Value));
                else
                    failed.Add($"width {width} (in cm, mm, pt or % of the text's width)");
            }
            if (Text("crop") is { } crop)
            {
                var cut = crop.Trim().ToLowerInvariant();
                if (cut is "none" or "whole")
                    Do("crop", () => editing.CropPicture(0.0));
                else if (cut is "circle" or "round")
                    Do("crop", () => editing.RoundPicture(true));
                else if (cut is "rectangle")
                    Do("crop", () => editing.RoundPicture(false));
                else if (Proportions.TryGetValue(cut, out var ratio))
                    Do("crop", () => editing.CropPicture(ratio));
                else
                    failed.Add($"crop {crop} (none, 1:1, 4:3, 3:4, 3:2, 2:3, 16:9, 9:16, circle or rectangle)");
            }
            if (Text("color") is { } color)
            {
                if (Looks.All.FirstOrDefault(l => l.Recolor.Equals(color.Trim(), StringComparison.OrdinalIgnoreCase)).Recolor is { } recolor)
                    Do("color", () =>
                    {
                        editing.Format(new DrawingFormatting { Recolor = recolor });
                        return true;
                    });
                else
                    failed.Add($"color {color} (none, grayscale, blackWhite or washout)");
            }
            if (Flag("frame") is { } framed)
                Do("frame", () =>
                {
                    editing.Format(framed ? new DrawingFormatting { Outline = Placement.FrameWidth, OutlineColor = Placement.FrameColor } : new DrawingFormatting { Outline = 0 });
                    return true;
                });
            if (Number("turn") is { } turn)
                Do("turn", () =>
                {
                    editing.Format(new DrawingFormatting { Rotation = (turn % 360 + 360) % 360 });
                    return true;
                });
            if (Text("flip") is { } flip)
            {
                var way = flip.Trim().ToLowerInvariant();
                if (way is "none" or "across" or "down" or "both")
                    Do("flip", () =>
                    {
                        editing.Format(new DrawingFormatting { FlipHorizontal = way is "across" or "both", FlipVertical = way is "down" or "both" });
                        return true;
                    });
                else
                    failed.Add($"flip {flip} (none, across, down or both)");
            }
            if (Text("tilt") is { } tilt)
            {
                var name = tilt.Trim().ToLowerInvariant();
                if (name is "none" or "flat")
                    Do("tilt", () => editing.TiltPicture(PictureTilt.Flat));
                else if (Objects.Tilts.FirstOrDefault(t => t.Name == name).Camera is { } camera)
                    Do("tilt", () => editing.TiltPicture(new PictureTilt(camera)));
                else
                    failed.Add($"tilt {tilt} (none, above, rising, right, falling, below, lying or left)");
            }
            if (Text("caption") is { } caption)
            {
                var runs = Markup.RunsOf(Notation.Inline(caption.Trim(), null), caption: true).Runs;
                if (caption.Trim().Length == 0)
                    Do("caption", () => editing.Caption == null || editing.RemoveCaption());
                else
                    Do("caption", () =>
                    {
                        if (editing.Caption == null && !editing.AddCaption())
                            return false;
                        Select();
                        if (editing.Caption is not { } first)
                            return false;
                        // The caption's text as written, the cursor in it, so that the picture with it is as high as they then are.
                        editing.Handle(() =>
                        {
                            while (document.After(first) != null)
                                document.Join(first);
                            document.Replace(first, 0, document.Text(first).Length, "");
                            document.InsertMarkup(first, 0, runs);
                            return new TextPlace(first, document.Text(first).Length);
                        });
                        return true;
                    });
            }
            if (Text("description") is { } description)
                Do("description", () =>
                {
                    editing.Format(new DrawingFormatting { Description = description.Trim(), Decorative = false });
                    return true;
                });
            // A caption written or taken away, or the picture with it: the captions' numbers and the references to them saved as the
            // pages show them (see Writing.Refresh).
            if (document is WordDocument captioned && succeeded > 0 && (Text("caption") != null || removed))
            {
                try
                {
                    editing.Handle(() =>
                    {
                        captioned.RefreshFields();
                        return null;
                    });
                }
                catch (Exception e) when (e is NotSupportedException or ArgumentException or InvalidOperationException)
                {
                }
            }
            Select();
        });
        if (removed)
            return ($"Object {id!.Value.ToString(CultureInfo.InvariantCulture)} was taken away.", false);
        objects.Changed(id!.Value, (paragraph, offset));
        var told = "Now: " + objects.Details(id.Value);
        return failed.Count > 0 ? (told + " Not done: " + string.Join("; ", failed) + ".", succeeded == 0) : (told, false);
    }

    /// <summary>The paper sizes the page tool names, upright, in points.</summary>
    public static readonly Dictionary<string, (double Width, double Height)> Papers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A3"] = (841.9, 1190.6), ["A4"] = (595.3, 841.9), ["A5"] = (419.5, 595.3), ["Letter"] = (612, 792), ["Legal"] = (612, 1008),
    };

    /// <summary>
    /// The page tool: the document's page setup as its input says, in every section set up as its first one is (sections turned across for
    /// a table keep theirs): margins, paper, orientation, whether the first page has a header and footer of its own (a title page); and the
    /// header and footer every page shows, in the notation's text ({page}, {pages}, {date} as fields, a tab between parts putting the next in
    /// the middle and the last at the right, as Word's positional tabs do, whatever the margins). What is told Claude: the page as it is then.
    /// </summary>
    public static (string Text, bool Error) Page(EditSession editing, DocumentView sheets, Objects objects, string json)
    {
        if (editing.Document is not WordDocument word || word.Beginning is not { } first)
            return ("This document has no pages to set up.", true);
        Dictionary<string, string> input;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                return ("The input of page is a JSON object.", true);
            input = parsed.RootElement.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return ("The input of page is a JSON object.", true);
        }
        var failed = new List<string>();
        var format = new SectionFormatting();
        if (input.GetValueOrDefault("margins") is { } margins)
        {
            var lengths = margins.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Select(m => Markup.Length(m, 0)).ToList();
            if (lengths.Count is 1 or 2 or 4 && lengths.All(l => l is > 0 and < 300))
            {
                var (top, right, bottom, left) = lengths.Count switch
                {
                    1 => (lengths[0], lengths[0], lengths[0], lengths[0]),
                    2 => (lengths[0], lengths[1], lengths[0], lengths[1]),
                    _ => (lengths[0], lengths[1], lengths[2], lengths[3]),
                };
                format = format with { Top = top, Right = right, Bottom = bottom, Left = left };
            }
            else
                failed.Add($"margins {margins} (one, two or four lengths: 2cm, 2cm 2.5cm, 2cm 2.5cm 2cm 2.5cm)");
        }
        if (input.GetValueOrDefault("size") is { } size)
        {
            if (Papers.TryGetValue(size.Trim(), out var paper))
                format = format with { PageWidth = paper.Width, PageHeight = paper.Height };
            else
                failed.Add($"size {size} (A3, A4, A5, Letter or Legal)");
        }
        if (input.GetValueOrDefault("border") is { } borderText)
        {
            if (Border(borderText) is { } border)
                format = format with { Border = border };
            else
                failed.Add($"border {borderText} (single, double, thick, dotted, dashed, triple, wave, one of Word's art borders by name, or none; a colour #RRGGBB and a width in points after it)");
        }
        // Line numbers in the margin: every so many lines ("5", "every 5"), restarting on each page unless "each section" or
        // "continuous" says otherwise; none takes them away.
        if (input.GetValueOrDefault("line_numbers")?.Trim().ToLowerInvariant() is { } lineNumbers)
        {
            var every = System.Text.RegularExpressions.Regex.Match(lineNumbers, @"\d+");
            if (lineNumbers is "none" or "no" or "off" or "0")
                format = format with { LineNumbers = new LineNumbering(0) };
            else if (every.Success || lineNumbers is "yes" or "on" or "every line")
                format = format with
                {
                    LineNumbers = new LineNumbering(every.Success ? Math.Clamp(int.Parse(every.Value, CultureInfo.InvariantCulture), 1, 100) : 1,
                        lineNumbers.Contains("section", StringComparison.Ordinal) ? "newSection" : lineNumbers.Contains("continu", StringComparison.Ordinal) ? "continuous" : "newPage"),
                };
            else
                failed.Add($"line_numbers {lineNumbers} (every 1, every 5, each section, continuous, or none)");
        }
        // The page numbers' format ("1", "i", "I", "a", "A" for arabic, small and capital roman numbers and letters) and where they start
        // ("i from 1"; "from 5"; "on": going on from the section before).
        if (input.GetValueOrDefault("page_numbers")?.Trim() is { Length: > 0 } pageNumbers)
        {
            var parts = pageNumbers.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            var numberFormat = parts[0] switch
            {
                "1" or "arabic" or "decimal" => "decimal",
                "i" or "roman" => "lowerRoman",
                "I" or "ROMAN" => "upperRoman",
                "a" => "lowerLetter",
                "A" => "upperLetter",
                _ => null,
            };
            var from = parts.SkipWhile(p => p != "from").Skip(1).FirstOrDefault() is { } begin
                && int.TryParse(begin, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number >= 0 ? number : (int?)null;
            if (numberFormat == null && parts[0] != "from")
                failed.Add($"page_numbers {pageNumbers} (1, i, I, a or A, then \"from N\" to start at N)");
            else
                format = format with { PageNumberFormat = numberFormat ?? format.PageNumberFormat, PageNumberStart = from ?? format.PageNumberStart };
        }
        if (input.GetValueOrDefault("orientation")?.Trim().ToLowerInvariant() is { } orientation)
        {
            if (orientation is "portrait" or "landscape")
                format = format with { Orientation = orientation };
            else
                failed.Add($"orientation {orientation} (portrait or landscape)");
        }
        // A paper named lies as the orientation given says, else as the page lies now; turned alone, the page keeps its paper (the
        // document swaps its width and height, as Word turns it).
        if (format.PageWidth is { } across && format.PageHeight is { } down)
        {
            var lying = format.Orientation == "landscape"
                || format.Orientation == null && word.SectionOf(first) is { PageWidth: { } w, PageHeight: { } h } && w > h;
            format = format with
            {
                PageWidth = lying ? Math.Max(across, down) : Math.Min(across, down),
                PageHeight = lying ? Math.Min(across, down) : Math.Max(across, down),
                Orientation = lying ? "landscape" : "portrait",
            };
        }
        var done = new List<string>();
        sheets.Edit(() =>
        {
            void Do(string what, Action change)
            {
                try
                {
                    editing.Handle(() =>
                    {
                        change();
                        return null;
                    });
                    done.Add(what);
                }
                catch (Exception e) when (e is NotSupportedException or ArgumentException or InvalidOperationException)
                {
                    failed.Add($"{what} ({e.Message})");
                }
            }
            if (format != new SectionFormatting())
            {
                // Every section set up as the first one is: one of its paragraphs each.
                var upright = word.SectionOf(first) is { PageWidth: { } w0, PageHeight: { } h0 } ? w0 <= h0 : true;
                var sections = new Dictionary<int, int>();
                for (int? paragraph = first; paragraph is { } p; paragraph = word.Next(p))
                {
                    try
                    {
                        var end = word.SectionEnd(p) ?? -1;
                        sections.TryAdd(end, p);
                    }
                    catch (NotSupportedException)
                    {
                    }
                }
                Do("page setup", () =>
                {
                    foreach (var paragraph in sections.Values)
                    {
                        if (word.SectionOf(paragraph) is { PageWidth: { } w, PageHeight: { } h } && (w <= h) != upright)
                            continue;
                        word.Format(paragraph, format);
                    }
                });
            }
            if (input.GetValueOrDefault("first_page")?.Trim().ToLowerInvariant() is { } titled)
            {
                if (titled is "own" or "same")
                    Do("first page", () => word.Format(first, new SectionFormatting { TitlePage = titled == "own" }));
                else
                    failed.Add($"first_page {titled} (own or same)");
            }
            foreach (var (name, footer) in new[] { ("header", false), ("footer", true) })
            {
                if (input.GetValueOrDefault(name) is not { } text)
                    continue;
                Do(name, () => Story(word, first, footer, text));
            }
            // Words as Word's text watermark, or a picture of the document by its token (see Watermarks).
            if (input.GetValueOrDefault("watermark") is { } watermark)
                Do("watermark", () => Watermarks.Set(word, first, objects, watermark.Trim()));
            // Words hyphenated at the ends of lines, as Word's automatic hyphenation (a setting of the document's).
            if (input.GetValueOrDefault("hyphenation")?.Trim().ToLowerInvariant() is { } hyphenation)
            {
                if (hyphenation is "true" or "yes" or "on" or "automatic" or "false" or "no" or "off" or "none")
                    Do("hyphenation", () => word.AutoHyphenation = hyphenation is "true" or "yes" or "on" or "automatic");
                else
                    failed.Add($"hyphenation {hyphenation} (on or off)");
            }
            // The pages' colour as Word's Page Color gives it (see WordDocument.SetPageColor); none takes it away.
            if (input.GetValueOrDefault("color")?.Trim() is { } colored)
            {
                var hex = colored.TrimStart('#');
                if (colored.Equals("none", StringComparison.OrdinalIgnoreCase) || colored.Length == 0)
                    Do("color", () => word.SetPageColor(null));
                else if (hex.Length == 6 && hex.All(Uri.IsHexDigit))
                    Do("color", () => word.SetPageColor(hex));
                else
                    failed.Add($"color {colored} (#RRGGBB or none)");
            }
        });
        var told = "Now: " + Reading.PageOf(word, objects);
        if (failed.Count > 0)
            told += " Not done: " + string.Join("; ", failed) + ".";
        return (told, done.Count == 0 && failed.Count > 0);
    }

    /// <summary>
    /// The header (or the footer) every page shows given a text in the notation: one paragraph of its own, the parts between tabs placed by
    /// positional tabs (the next in the middle, the last at the right, wherever the margins are), {page} and its kin as fields.
    /// </summary>
    public static void Story(WordDocument word, int first, bool footer, string text)
    {
        var story = word.Header(first, "default", footer);
        // A watermark the header holds stays (see Watermark).
        var watermark = footer ? null : WatermarkText(word, story);
        while (word.After(story) is { } next)
            story = word.Join(story);
        word.Replace(story, 0, word.Text(story).Length, "");
        var parts = text.Replace("\\t", "\t", StringComparison.Ordinal).Split('\t').Select(p => p.Trim()).ToList();
        var runs = new System.Text.StringBuilder();
        for (var k = 0; k < parts.Count; k++)
        {
            if (k > 0)
            {
                var alignment = k == parts.Count - 1 ? "right" : "center";
                runs.Append($"<w:r><w:ptab w:relativeTo=\"margin\" w:alignment=\"{alignment}\" w:leader=\"none\"/></w:r>");
            }
            runs.Append(Markup.RunsOf(Notation.Inline(parts[k], null)).Runs);
        }
        if (runs.Length > 0)
            word.InsertMarkup(story, 0, runs.ToString());
        if (watermark != null)
            word.InsertMarkup(story, 0, WatermarkRun(word, first, watermark));
    }

    /// <summary>The name Word gives its text watermark (a WordArt of VML in a header), by which its Watermark dialog finds one.</summary>
    const string WatermarkName = "PowerPlusWaterMarkObject";

    /// <summary>
    /// The page tool's watermark: a word or two across every page behind the text, silver and half see-through, rising from the bottom
    /// left, as Word's Watermark dialog puts one in: a WordArt of VML in each header the first section's pages show (the first page's
    /// own and the even pages' too where they have their own), centred on the margins. An empty text takes it away.
    /// </summary>
    public static void Watermark(WordDocument word, int first, string text)
    {
        var kinds = new List<string> { "default" };
        if (word.SectionOf(first).TitlePage == true)
            kinds.Add("first");
        if (word.EvenAndOddHeaders)
            kinds.Add("even");
        foreach (var kind in kinds)
        {
            var story = word.Header(first, kind, false);
            for (int? paragraph = story; paragraph is { } at; paragraph = word.After(at))
            {
                while (WatermarkAt(word, at) is { } offset)
                    word.Replace(at, offset, 1, "");
            }
            if (text.Length > 0)
                word.InsertMarkup(story, 0, WatermarkRun(word, first, text));
        }
    }

    /// <summary>Where a paragraph of a header holds a watermark (see Watermark): its character's offset, or null.</summary>
    public static int? WatermarkAt(WordDocument word, int paragraph)
    {
        var text = word.Text(paragraph);
        for (var k = 0; k < text.Length; k++)
        {
            if (text[k] == '￼' && word.DrawingName(paragraph, k) is { } name
                && (name.StartsWith(WatermarkName, StringComparison.Ordinal) || name.StartsWith(Watermarks.PictureName, StringComparison.Ordinal)))
                return k;
        }
        return null;
    }

    /// <summary>The text of the watermark a header (its first paragraph given) shows, or null.</summary>
    public static string? WatermarkText(WordDocument word, int story)
    {
        for (int? paragraph = story; paragraph is { } at; paragraph = word.After(at))
        {
            if (WatermarkAt(word, at) is { } offset)
                return word.DrawingWords(at, offset) ?? "";
        }
        return null;
    }

    /// <summary>
    /// A watermark's run as Word's Watermark dialog writes it (a VML WordArt of shape type 136 behind the text, turned 315 degrees,
    /// centred on the margins), as long as the text is wide and as high as its letters make it, no higher than 0.45 of the width.
    /// </summary>
    static string WatermarkRun(WordDocument word, int first, string text)
    {
        var section = word.SectionOf(first);
        var across = (section.PageWidth ?? 595.3) - Math.Abs(section.Left ?? 72) - Math.Abs(section.Right ?? 72);
        var aspect = Math.Max(1.2, 0.56 * text.Length + 0.3);
        var (width, height) = (across * 0.92, across * 0.92 / aspect);
        if (height > across * 0.45)
            (width, height) = (across * 0.45 * aspect, across * 0.45);
        static string Pt(double points) => points.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        var id = Random.Shared.Next(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var spid = Random.Shared.Next(2049, 9999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return "<w:r><w:rPr><w:noProof/></w:rPr><w:pict>"
            + "<v:shapetype id=\"_x0000_t136\" coordsize=\"21600,21600\" o:spt=\"136\" adj=\"10800\" path=\"m@7,l@8,m@5,21600l@6,21600e\"><v:formulas>"
            + "<v:f eqn=\"sum #0 0 10800\"/><v:f eqn=\"prod #0 2 1\"/><v:f eqn=\"sum 21600 0 @1\"/><v:f eqn=\"sum 0 0 @2\"/><v:f eqn=\"sum 21600 0 @3\"/>"
            + "<v:f eqn=\"if @0 @3 0\"/><v:f eqn=\"if @0 21600 @1\"/><v:f eqn=\"if @0 0 @2\"/><v:f eqn=\"if @0 @4 21600\"/><v:f eqn=\"mid @5 @6\"/>"
            + "<v:f eqn=\"mid @8 @5\"/><v:f eqn=\"mid @7 @8\"/><v:f eqn=\"mid @6 @7\"/><v:f eqn=\"sum @6 0 @5\"/></v:formulas>"
            + "<v:path textpathok=\"t\" o:connecttype=\"custom\" o:connectlocs=\"@9,0;@10,10800;@11,21600;@12,10800\" o:connectangles=\"270,180,90,0\"/>"
            + "<v:textpath on=\"t\" fitshape=\"t\"/><v:handles><v:h position=\"#0,bottomRight\" xrange=\"6629,14971\"/></v:handles>"
            + "<o:lock v:ext=\"edit\" text=\"t\" shapetype=\"t\"/></v:shapetype>"
            + $"<v:shape id=\"{WatermarkName}{id}\" o:spid=\"_x0000_s{spid}\" type=\"#_x0000_t136\" style=\"position:absolute;margin-left:0;margin-top:0;"
            + $"width:{Pt(width)}pt;height:{Pt(height)}pt;rotation:315;z-index:-251657216;mso-position-horizontal:center;mso-position-horizontal-relative:margin;"
            + "mso-position-vertical:center;mso-position-vertical-relative:margin\" o:allowincell=\"f\" fillcolor=\"silver\" stroked=\"f\">"
            + "<v:fill opacity=\".5\"/>"
            + $"<v:textpath style=\"font-family:&quot;Calibri&quot;;font-size:1pt\" string=\"{System.Security.SecurityElement.Escape(text)}\"/>"
            + "<w10:wrap xmlns:w10=\"urn:schemas-microsoft-com:office:word\" anchorx=\"margin\" anchory=\"margin\"/></v:shape></w:pict></w:r>";
    }

    /// <summary>A page border as the page tool names it: a kind, then a colour (#RRGGBB) and a width in points if given; null for none known.</summary>
    static PageBorder? Border(string text)
    {
        var words = text.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return null;
        var kind = words[0].ToLowerInvariant() switch
        {
            "none" or "no" => "none",
            "single" or "line" or "box" => "single",
            "double" => "double",
            "thick" => "thick",
            "dotted" => "dotted",
            "dashed" => "dashed",
            "triple" => "triple",
            "wave" or "wavy" => "wave",
            "thin-thick" => "thinThickSmallGap",
            "thick-thin" => "thickThinSmallGap",
            // One of Word's art borders by its name (apples, celticKnotwork, stars …), drawn as tiles of its pictures (see ArtBorders).
            var name => FreeSlate.Docx.Layout.ArtBorders.Named(name),
        };
        if (kind == null)
            return null;
        string? color = null;
        var art = FreeSlate.Docx.Layout.ArtBorders.Names.Contains(kind);
        var width = art ? 20 : kind is "thick" ? 2.25 : kind is "double" or "triple" or "wave" ? 0.75 : 0.5;
        foreach (var word in words.Skip(1))
        {
            if (word.TrimStart('#') is { Length: 6 } hex && hex.All(Uri.IsHexDigit))
                color = hex.ToUpperInvariant();
            else if (double.TryParse(word.Replace("pt", "", StringComparison.OrdinalIgnoreCase), System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out var points) && points > 0 && points <= (art ? 31 : 12))
                width = points;
            else
                return null;
        }
        return new PageBorder(kind, width, color);
    }

    /// <summary>
    /// The header's and the footer's text in the notation (the parts between tabs), whether the first page has none, and the watermark's
    /// text (see Watermark), as the first section has them.
    /// </summary>
    public static (string Header, string Footer, bool Title, string? Watermark)? Stories(WordDocument word, Objects objects)
    {
        if (word.Beginning is not { } first)
            return null;
        string Of(bool footer)
        {
            int? story;
            try
            {
                story = word.ShownHeader(first, "default", footer);
            }
            catch (NotSupportedException)
            {
                return "";
            }
            var lines = new List<string>();
            for (var paragraph = story; paragraph is { } p; paragraph = word.After(p))
                lines.Add(Reading.StoryLine(word, p, objects));
            return string.Join(" ", lines).Trim();
        }
        try
        {
            return (Of(false), Of(true), word.SectionOf(first).TitlePage == true,
                word.ShownHeader(first, "default", false) is { } header ? WatermarkText(word, header) : null);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>The tone tool: the document given a tone as its buttons give it (see EditorView.Take), the pages fading into it.</summary>
    public static async Task<(string Text, bool Error)> Tone(EditSession editing, DocumentView sheets, string json)
    {
        string? key = null;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.TryGetProperty("tone", out var value) && value.ValueKind == JsonValueKind.String)
                key = value.GetString();
        }
        catch (JsonException)
        {
        }
        var tone = Tones.All.FirstOrDefault(t => t.Key.Equals(key?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (tone == null)
            return ("tone is one of " + string.Join(", ", Tones.All.Select(t => t.Key.ToLowerInvariant())) + ".", true);
        if (Tones.Of(editing.Document) == tone)
            return ($"The document has the tone {tone.Key.ToLowerInvariant()} already.", false);
        await EngineFonts.Prepare(tone.Families());
        var (styles, numbering) = tone.Definitions();
        sheets.Restyle(() => editing.Handle(() =>
        {
            editing.Document.DefineStyles(styles, numbering);
            return null;
        }));
        return ($"The document has the tone {tone.Key.ToLowerInvariant()} now.", false);
    }
}
