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
                var runs = Markup.RunsOf(Notation.Inline(caption.Trim(), null)).Runs;
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
            Select();
        });
        if (removed)
            return ($"Object {id!.Value.ToString(CultureInfo.InvariantCulture)} was taken away.", false);
        objects.Changed(id!.Value, (paragraph, offset));
        var told = "Now: " + objects.Details(id.Value);
        return failed.Count > 0 ? (told + " Not done: " + string.Join("; ", failed) + ".", succeeded == 0) : (told, false);
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
