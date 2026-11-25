using System.Globalization;
using System.Text;
using System.Text.Json;
using FreeSlate.Editing;
using FreeSlate.Editor.Controls;

namespace FreeSlate.Editor.Asking;

/// <summary>
/// What Word keeps about the whole document besides its pages, as Claude reads and sets it (see Prompt): its properties (File > Info, the
/// properties tool, read as "Properties:"), how it may be edited (Restrict Editing, the page tool's protection, read on the "Page:" line),
/// and its mailings (the mailings tool, read as "Mailings:"): a form letter, an envelope before the letter, a sheet of labels, as Word's
/// Mailings tab makes them (see WordDocument.FormLetter, SetEnvelope, SetLabels). The envelope's and the sheet's paragraphs are not read
/// as the document's text: the "Mailings:" line says them, and the tool changes them.
/// </summary>
static class Mailings
{
    /// <summary>Word's kinds of Restrict Editing by the names the page tool takes.</summary>
    public static readonly IReadOnlyDictionary<string, string?> Protections = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    {
        ["forms"] = "forms", ["comments"] = "comments", ["changes"] = "trackedChanges", ["trackedchanges"] = "trackedChanges", ["readonly"] = "readOnly",
        ["read-only"] = "readOnly", ["none"] = null, ["off"] = null, ["no"] = null,
    };

    /// <summary>A kind of protection as the page tool names it.</summary>
    public static string ProtectionName(string kind) => kind switch { "trackedChanges" => "changes", "readOnly" => "readonly", _ => kind };

    /// <summary>A kind of protection said in words, as Word's Restrict Editing names it.</summary>
    public static string ProtectionSaid(string kind) => kind switch
    {
        "forms" => "filling in forms",
        "comments" => "comments",
        "trackedChanges" => "tracked changes",
        "readOnly" => "reading (no changes)",
        _ => kind,
    };

    /// <summary>The paragraphs the "Mailings:" line says instead of the text: an envelope's before the letter, a sheet of labels'.</summary>
    public static HashSet<int> Hidden(WordDocument word)
    {
        var hidden = new HashSet<int>(word.EnvelopeParagraphs());
        if (word.LabelsOf() is var (_, first, last, _))
        {
            for (int? paragraph = first; paragraph is { } p; paragraph = p == last ? null : word.Next(p))
                hidden.Add(p);
        }
        return hidden;
    }

    /// <summary>The document's properties as Claude reads them ("title "…", keywords "…""), empty where it has none.</summary>
    public static string PropertiesOf(WordDocument word)
    {
        var properties = word.Properties;
        var said = new List<string>();
        foreach (var (name, value) in new[]
                 {
                     ("title", properties.Title), ("subject", properties.Subject), ("author", properties.Author), ("keywords", properties.Keywords),
                     ("comments", properties.Comments), ("category", properties.Category), ("status", properties.Status), ("company", properties.Company),
                     ("manager", properties.Manager),
                 })
        {
            if (value is { Length: > 0 })
                said.Add(name + " \"" + value.Replace("\"", "''", StringComparison.Ordinal) + "\"");
        }
        return string.Join(", ", said);
    }

    /// <summary>The document's mailings as Claude reads them (a form letter, an envelope, a sheet of labels), empty where it has none.</summary>
    public static string MailingsOf(WordDocument word)
    {
        var said = new List<string>();
        if (word.FormLetter)
            said.Add("a form letter (merge: letters), its {merge …} fields filled from a data source the user picks in Word");
        if (word.EnvelopeOf() is { } envelope)
            said.Add($"an envelope {envelope.Size} before the letter, to \"{string.Join(" / ", envelope.To)}\"" + (envelope.From.Count > 0 ? $", from \"{string.Join(" / ", envelope.From)}\"" : ""));
        if (word.LabelsOf() is var (sheet, _, _, labels))
        {
            var filled = labels.Where(l => l.Trim().Length > 0).ToList();
            var distinct = filled.Distinct(StringComparer.Ordinal).ToList();
            said.Add($"the document is a sheet of labels {sheet.Product} ({sheet.Size}, {sheet.Across} × {sheet.Down} a page), "
                + (distinct.Count == 1 ? $"{filled.Count} of \"{distinct[0].Replace("\n", " / ", StringComparison.Ordinal)}\""
                    : $"{filled.Count} labels: " + string.Join("; ", filled.Select(l => "\"" + l.Replace("\n", " / ", StringComparison.Ordinal) + "\""))));
        }
        return string.Join("; ", said);
    }

    /// <summary>
    /// The properties tool: the document's properties as its input gives them (an empty one taken away), never the user's name unless the
    /// wish gives it; the fields showing them then show them (see WordDocument.RefreshPropertyFields). What is told Claude: the properties
    /// as they then are.
    /// </summary>
    public static (string Text, bool Error) Properties(EditSession editing, DocumentView sheets, string json)
    {
        if (editing.Document is not WordDocument word)
            return ("This document has no properties.", true);
        if (Strings(json) is not { } input)
            return ("The input of properties is a JSON object.", true);
        var given = new DocumentProperties
        {
            Title = input.GetValueOrDefault("title"),
            Subject = input.GetValueOrDefault("subject"),
            Author = input.GetValueOrDefault("author"),
            Keywords = input.GetValueOrDefault("keywords"),
            Comments = input.GetValueOrDefault("comments") ?? input.GetValueOrDefault("description"),
            Category = input.GetValueOrDefault("category"),
            Status = input.GetValueOrDefault("status"),
            Company = input.GetValueOrDefault("company"),
            Manager = input.GetValueOrDefault("manager"),
        };
        string? refused = null;
        sheets.Edit(() =>
        {
            try
            {
                editing.Handle(() =>
                {
                    if (word.SetProperties(given))
                        word.RefreshPropertyFields();
                    return null;
                });
            }
            catch (Exception e) when (e is NotSupportedException or ArgumentException or InvalidOperationException)
            {
                refused = e.Message;
            }
        });
        if (refused != null)
            return ("Not done: " + refused, true);
        return ("Now: " + (PropertiesOf(word) is { Length: > 0 } now ? now : "no properties"), false);
    }

    /// <summary>
    /// The mailings tool: the document a form letter or an ordinary one again (merge), an envelope before the letter given or taken away
    /// (envelope_to, envelope_from, envelope_size), the document made a sheet of labels (labels, labels_product), each as Word's Mailings
    /// tab makes it. What is told Claude: the mailings as they then are.
    /// </summary>
    public static (string Text, bool Error) Mailing(EditSession editing, DocumentView sheets, string json)
    {
        if (editing.Document is not WordDocument word)
            return ("This document has no mailings.", true);
        JsonElement root;
        try
        {
            using var parsed = JsonDocument.Parse(json);
            root = parsed.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ("The input of mailings is a JSON object.", true);
        }
        if (root.ValueKind != JsonValueKind.Object)
            return ("The input of mailings is a JSON object.", true);
        string? Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        static List<string> Lines(string text) => [.. text.Replace("\\n", "\n", StringComparison.Ordinal).Replace("\r", "", StringComparison.Ordinal).Split('\n').Select(l => l.Trim())];
        var failed = new List<string>();
        var done = 0;
        void Do(string what, Action change)
        {
            sheets.Edit(() =>
            {
                try
                {
                    editing.Handle(() =>
                    {
                        change();
                        return null;
                    });
                    done++;
                }
                catch (Exception e) when (e is NotSupportedException or ArgumentException or InvalidOperationException)
                {
                    failed.Add($"{what} ({e.Message})");
                }
            });
        }
        if (Text("merge")?.Trim().ToLowerInvariant() is { } merge)
        {
            if (merge is "letters" or "letter" or "form letter" or "none" or "off" or "normal")
                Do("merge", () => word.FormLetter = merge.StartsWith("letter", StringComparison.Ordinal) || merge == "form letter");
            else
                failed.Add($"merge {merge} (letters or none)");
        }
        if (Text("envelope_to") is { } to)
        {
            var size = Text("envelope_size")?.Trim() is { Length: > 0 } named ? named : word.EnvelopeOf()?.Size ?? "DL";
            // The fields that count the sections and pages (and the facts') showing what Word's update would show with or without it.
            if (to.Trim().Length == 0 || to.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
                Do("envelope", () =>
                {
                    word.SetEnvelope(null);
                    word.RefreshPropertyFields();
                });
            else if (!WordDocument.EnvelopeSizes.ContainsKey(size))
                failed.Add($"envelope_size {size} (DL, C4, C5 or C6)");
            else
            {
                var from = Text("envelope_from") is { } given ? Lines(given).Where(l => l.Length > 0).ToList() : (IReadOnlyList<string>?)word.EnvelopeOf()?.From ?? [];
                Do("envelope", () =>
                {
                    word.SetEnvelope(new Envelope(size.ToUpperInvariant(), [.. Lines(to).Where(l => l.Length > 0)], from));
                    word.RefreshPropertyFields();
                });
            }
        }
        else if (Text("envelope_size") is { } resized && word.EnvelopeOf() is { } had)
        {
            if (WordDocument.EnvelopeSizes.ContainsKey(resized.Trim()))
                Do("envelope", () => word.SetEnvelope(had with { Size = resized.Trim().ToUpperInvariant() }));
            else
                failed.Add($"envelope_size {resized} (DL, C4, C5 or C6)");
        }
        var labels = root.TryGetProperty("labels", out var list) ? list.ValueKind switch
        {
            JsonValueKind.Array => list.EnumerateArray().Where(l => l.ValueKind == JsonValueKind.String).Select(l => string.Join("\n", Lines(l.GetString() ?? ""))).ToList(),
            JsonValueKind.String => list.GetString()!.Replace("\\n", "\n", StringComparison.Ordinal).Replace("\r", "", StringComparison.Ordinal)
                .Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Select(l => string.Join("\n", Lines(l))).ToList(),
            _ => null,
        } : null;
        if (labels != null || Text("labels_product") != null)
        {
            var product = Text("labels_product")?.Trim().Replace("Avery Zweckform", "", StringComparison.OrdinalIgnoreCase).Replace("Avery", "", StringComparison.OrdinalIgnoreCase).Trim()
                ?? word.LabelsOf()?.Sheet.Product ?? "3474";
            var had = word.LabelsOf()?.Labels.Where(l => l.Trim().Length > 0).ToList();
            labels ??= had != null && had.Distinct(StringComparer.Ordinal).Count() == 1 ? [had[0]] : had;
            if (WordDocument.LabelSheets.FirstOrDefault(s => s.Product.Equals(product, StringComparison.OrdinalIgnoreCase)) is not { } sheet)
                failed.Add($"labels_product {product} (" + string.Join(", ", WordDocument.LabelSheets.Select(s => s.Product)) + ")");
            else if (labels is not { Count: > 0 })
                failed.Add("labels (give the labels' texts)");
            else
            {
                // A sheet of labels is a document of its own (Word makes a new one): what the document holds goes first, a table that
                // begins it taken away whole, as selecting all does not reach it; a sheet it is already is given anew in place.
                if (word.LabelsOf() == null)
                {
                    sheets.Edit(() =>
                    {
                        try
                        {
                            editing.SelectAll();
                            editing.Delete();
                        }
                        catch (Exception e) when (e is NotSupportedException or ArgumentException or InvalidOperationException)
                        {
                            failed.Add($"labels ({e.Message})");
                        }
                    });
                    for (var tables = 0; tables < 8 && word.Beginning is { } opening && word.TableOf(opening) is { } table; tables++)
                    {
                        var before = failed.Count;
                        Do("labels", () => word.RemoveTable(table));
                        if (failed.Count > before)
                            break;
                    }
                }
                Do("labels", () => word.SetLabels(sheet, labels));
            }
        }
        var told = "Now: " + (MailingsOf(word) is { Length: > 0 } now ? now : "no mailings");
        if (failed.Count > 0)
            told += " Not done: " + string.Join("; ", failed) + ".";
        return (told, done == 0 && failed.Count > 0);
    }

    /// <summary>A JSON object's string members by their names (any case), or null where the input is no object.</summary>
    static Dictionary<string, string>? Strings(string json)
    {
        try
        {
            using var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            return parsed.RootElement.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String)
                .ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
