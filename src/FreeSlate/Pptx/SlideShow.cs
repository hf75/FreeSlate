using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using FreeSlate.Drawing;

namespace FreeSlate.Pptx;

/// <summary>
/// Slide show data of a slide for the player: the transition (catalog: transition.*) and the timing tree with the
/// build list (catalog: animation.*), as JSON. The timing tree keeps PowerPoint's structure (par, seq, excl time
/// containers with their conditions and the behaviours), so the player interprets every effect the same way.
/// </summary>
public static class SlideShow
{
    static readonly XNamespace P14 = "http://schemas.microsoft.com/office/powerpoint/2010/main";

    /// <summary>
    /// Recorded laser pointer traces of a slide (p14:laserTraceLst, catalog: animation.laser): every trace as its
    /// points [time in ms from the start of the slide's timeline, after the transition; x; y in px]; the pointer
    /// shows from a trace's first point to its last. Null without traces.
    /// </summary>
    public static JsonArray? Laser(SlideContext slide)
    {
        var list = slide.Slide.Root?.Element(Ns.P + "extLst")?.Descendants(P14 + "laserTraceLst").FirstOrDefault();
        var traces = new JsonArray();
        foreach (var trace in list?.Elements(P14 + "tracePtLst") ?? [])
        {
            var points = new JsonArray();
            foreach (var point in trace.Elements(P14 + "tracePt"))
            {
                if (TimeOffset(point.Str("t")) is { } t)
                    points.Add(new JsonArray(Math.Round(t, 1), Math.Round(Units.ToPx(point.Long("x") ?? 0), 2), Math.Round(Units.ToPx(point.Long("y") ?? 0), 2)));
            }
            if (points.Count > 0)
                traces.Add(points);
        }
        return traces.Count > 0 ? traces : null;
    }

    /// <summary>
    /// Media and trigger events recorded with the slide show (p14:showEvtLst in the slide's extLst, catalog:
    /// animation.media): [kind (play, stop, pause, resume, seek, trigger), time in ms from the start of the slide's
    /// timeline after the transition, the shape's id, the seek position in ms or the trigger event]. Null without.
    /// </summary>
    public static JsonArray? Events(SlideContext slide)
    {
        var list = slide.Slide.Root?.Element(Ns.P + "extLst")?.Descendants(P14 + "showEvtLst").FirstOrDefault();
        var events = new JsonArray();
        foreach (var record in list?.Elements() ?? [])
        {
            var kind = record.Name.LocalName switch
            {
                "playEvt" => "play", "stopEvt" => "stop", "pauseEvt" => "pause", "resumeEvt" => "resume",
                "seekEvt" => "seek", "triggerEvt" => "trigger", _ => null,
            };
            if (kind == null || TimeOffset(record.Str("time")) is not { } time || record.Str("objId") is not { } id)
                continue;
            var item = new JsonArray(kind, Math.Round(time, 1), id);
            if (kind == "seek" && TimeOffset(record.Str("seek")) is { } seek)
                item.Add(Math.Round(seek, 1));
            if (kind == "trigger")
                item.Add(record.Str("type") ?? "onClick");
            events.Add(item);
        }
        return events.Count > 0 ? events : null;
    }

    /// <summary>A time offset ("1.5s", "250ms", "2min"; milliseconds without a unit, [MS-PPTX] 2.3.4.6) in milliseconds.</summary>
    public static double? TimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim();
        var end = 0;
        while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.'))
            end++;
        if (!double.TryParse(text[..end], NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return null;
        return text[end..].Trim() switch
        {
            "h" => number * 3600000,
            "min" => number * 60000,
            "s" => number * 1000,
            "" or "ms" => number,
            "\u00B5s" or "\u03BCs" or "us" => number / 1000,
            "ns" => number / 1000000,
            _ => null,
        };
    }

    /// <summary>The slide's transition, or null when it has none.</summary>
    public static JsonObject? Transition(SlideContext slide)
    {
        var transition = slide.Slide.Root?.Element(Ns.P + "transition");
        if (transition == null)
            return null;
        var json = new JsonObject();
        var effect = transition.Elements().FirstOrDefault(e => e.Name.LocalName is not ("sndAc" or "extLst"));
        json["type"] = effect?.Name.LocalName ?? "none";
        foreach (var attribute in effect?.Attributes() ?? [])
        {
            if (!attribute.IsNamespaceDeclaration)
                json[attribute.Name.LocalName] = Value(attribute.Value);
        }
        // Duration: p14:dur in milliseconds, else the speed (measured: fast 0.5 s, medium 0.75 s, slow 1 s).
        json["dur"] = transition.Attribute(P14 + "dur") is { } dur && int.TryParse(dur.Value, out var ms)
            ? ms
            : (transition.Str("spd") ?? "fast") switch { "slow" => 1000, "med" => 750, _ => 500 };
        json["click"] = transition.Bool("advClick") ?? true;
        if (transition.Int("advTm") is { } after)
            json["after"] = after;
        if (transition.Element(Ns.P + "sndAc") is { } sound)
        {
            if (sound.Element(Ns.P + "stSnd") is { } start && Sound(slide, start.Element(Ns.P + "snd")) is { } src)
            {
                json["sound"] = src;
                if (start.Bool("loop") == true)
                    json["loopSound"] = true;
            }
            if (sound.Element(Ns.P + "endSnd") != null)
                json["stopSound"] = true;
        }
        return json;
    }

    /// <summary>The slide's timing tree and build list, or null without animations.</summary>
    public static JsonObject? Timing(SlideContext slide)
    {
        var timing = slide.Slide.Root?.Element(Ns.P + "timing");
        var root = timing?.Element(Ns.P + "tnLst")?.Elements().FirstOrDefault();
        if (root == null)
            return null;
        var json = new JsonObject { ["root"] = Node(root, slide) };
        if (timing!.Element(Ns.P + "bldLst") is { } builds)
        {
            var list = new JsonArray();
            foreach (var build in builds.Elements())
            {
                var item = new JsonObject { ["t"] = build.Name.LocalName };
                foreach (var attribute in build.Attributes())
                    item[attribute.Name.LocalName] = Value(attribute.Value);
                list.Add(item);
            }
            json["builds"] = list;
        }
        return json;
    }

    /// <summary>A time node: a container (par, seq, excl) or a behaviour (anim, set …).</summary>
    static JsonObject Node(XElement element, SlideContext slide)
    {
        var name = element.Name.LocalName;
        var json = new JsonObject { ["t"] = name };
        var behavior = element.Element(Ns.P + "cBhvr");
        var cTn = element.Element(Ns.P + "cTn") ?? behavior?.Element(Ns.P + "cTn") ?? element.Element(Ns.P + "cMediaNode")?.Element(Ns.P + "cTn");
        if (cTn != null)
            CommonTimeNode(cTn, json, slide);

        if (name == "seq")
        {
            Copy(element, json, "concurrent", "prevAc", "nextAc");
            if (Conditions(element.Element(Ns.P + "prevCondLst"), slide) is { } prev) json["prev"] = prev;
            if (Conditions(element.Element(Ns.P + "nextCondLst"), slide) is { } next) json["next"] = next;
        }
        // A bounce at the end of the behaviour (p14:bounceEnd, PowerPoint 2010).
        if (element.Attribute(P14 + "bounceEnd") is { } bounce)
            json["bounceEnd"] = Value(bounce.Value);
        if (behavior != null)
        {
            Copy(behavior, json, "additive", "accumulate", "xfrmType", "from", "to", "by", "rctx", "override");
            if (behavior.Element(Ns.P + "tgtEl") is { } target && Target(target, slide) is { } t)
                json["target"] = t;
            if (behavior.Element(Ns.P + "attrNameLst") is { } names)
                json["attrs"] = new JsonArray(names.Elements(Ns.P + "attrName").Select(a => (JsonNode?)JsonValue.Create(a.Value.Trim())).ToArray());
        }

        switch (name)
        {
            case "anim":
                Copy(element, json, "by", "from", "to", "calcmode", "valueType");
                if (element.Element(Ns.P + "tavLst") is { } tavs)
                {
                    var list = new JsonArray();
                    foreach (var tav in tavs.Elements(Ns.P + "tav"))
                    {
                        var item = new JsonObject { ["tm"] = tav.Str("tm") is { } tm ? Value(tm) : null };
                        if (tav.Str("fmla") is { Length: > 0 } formula) item["fmla"] = formula;
                        item["val"] = AnimValue(tav.Element(Ns.P + "val"), slide);
                        list.Add(item);
                    }
                    json["tav"] = list;
                }
                break;
            case "animClr":
                Copy(element, json, "clrSpc", "dir");
                foreach (var part in new[] { "by", "from", "to" })
                {
                    if (element.Element(Ns.P + part) is not { } value)
                        continue;
                    if (value.Element(Ns.P + "rgb") is { } rgb)
                        json[part] = new JsonObject { ["r"] = rgb.Int("r") ?? 0, ["g"] = rgb.Int("g") ?? 0, ["b"] = rgb.Int("b") ?? 0 };
                    else if (value.Element(Ns.P + "hsl") is { } hsl)
                        json[part] = new JsonObject { ["h"] = hsl.Int("h") ?? 0, ["s"] = hsl.Int("s") ?? 0, ["l"] = hsl.Int("l") ?? 0 };
                    else if (Color(value, slide) is { } color)
                        json[part] = color;
                }
                break;
            case "animEffect":
                Copy(element, json, "transition", "filter", "prLst");
                if (element.Element(Ns.P + "progress") is { } progress)
                    json["progress"] = AnimValue(progress, slide);
                break;
            case "animMotion":
                Copy(element, json, "origin", "path", "pathEditMode", "rAng", "ptsTypes");
                foreach (var part in new[] { "by", "from", "to", "rCtr" })
                {
                    if (element.Element(Ns.P + part) is { } point)
                        json[part] = new JsonArray(JsonValue.Create(Number(point.Str("x"))), JsonValue.Create(Number(point.Str("y"))));
                }
                break;
            case "animRot":
                Copy(element, json, "by", "from", "to");
                break;
            case "animScale":
                Copy(element, json, "zoomContents");
                foreach (var part in new[] { "by", "from", "to" })
                {
                    if (element.Element(Ns.P + part) is { } point)
                        json[part] = new JsonArray(JsonValue.Create(Number(point.Str("x"))), JsonValue.Create(Number(point.Str("y"))));
                }
                break;
            case "set":
                if (element.Element(Ns.P + "to") is { } to)
                    json["to"] = AnimValue(to, slide);
                break;
            case "cmd":
                Copy(element, json, "type", "cmd");
                break;
            case "audio" or "video":
                Copy(element, json, "isNarration", "fullScrn");
                if (element.Element(Ns.P + "cMediaNode") is { } media)
                {
                    Copy(media, json, "vol", "mute", "numSld", "showWhenStopped");
                    if (media.Element(Ns.P + "tgtEl") is { } target && Target(target, slide) is { } t)
                        json["target"] = t;
                }
                break;
        }
        return json;
    }

    /// <summary>p:cTn: timing attributes, start and end conditions, iteration and the child time nodes.</summary>
    static void CommonTimeNode(XElement cTn, JsonObject json, SlideContext slide)
    {
        foreach (var attribute in cTn.Attributes())
        {
            if (!attribute.IsNamespaceDeclaration)
                json[attribute.Name.LocalName] = Value(attribute.Value);
        }
        if (Conditions(cTn.Element(Ns.P + "stCondLst"), slide) is { } start) json["begin"] = start;
        if (Conditions(cTn.Element(Ns.P + "endCondLst"), slide) is { } end) json["end"] = end;
        if (cTn.Element(Ns.P + "endSync") is { } endSync && Conditions(new XElement("list", endSync), slide) is [var sync, ..] list)
        {
            list.Remove(sync);
            json["endSync"] = sync;
        }
        if (cTn.Element(Ns.P + "iterate") is { } iterate)
        {
            var item = new JsonObject { ["type"] = iterate.Str("type") ?? "el", ["backwards"] = iterate.Bool("backwards") ?? false };
            if (iterate.Element(Ns.P + "tmAbs") is { } absolute) item["abs"] = Value(absolute.Str("val") ?? "0");
            if (iterate.Element(Ns.P + "tmPct") is { } percent) item["pct"] = Value(percent.Str("val") ?? "0");
            json["iterate"] = item;
        }
        if (cTn.Element(Ns.P + "childTnLst") is { } children)
            json["children"] = new JsonArray(children.Elements().Select(c => (JsonNode?)Node(c, slide)).ToArray());
        if (cTn.Element(Ns.P + "subTnLst") is { } subs)
            json["sub"] = new JsonArray(subs.Elements().Select(c => (JsonNode?)Node(c, slide)).ToArray());
    }

    /// <summary>A condition list: events, delays, targets and time node references.</summary>
    static JsonArray? Conditions(XElement? list, SlideContext slide)
    {
        if (list == null)
            return null;
        var result = new JsonArray();
        foreach (var cond in list.Elements())
        {
            var item = new JsonObject();
            if (cond.Str("evt") is { } evt) item["evt"] = evt;
            if (cond.Str("delay") is { } delay) item["delay"] = Value(delay);
            if (cond.Element(Ns.P + "tgtEl") is { } target && Target(target, slide) is { } t) item["target"] = t;
            if (cond.Element(Ns.P + "tn") is { } tn) item["tn"] = tn.Int("val") ?? 0;
            if (cond.Element(Ns.P + "rtn") is { } rtn) item["rtn"] = rtn.Str("val");
            result.Add(item);
        }
        return result;
    }

    /// <summary>p:tgtEl: a shape (with text range, background or chart/diagram part), the slide, a sound or ink.</summary>
    static JsonObject? Target(XElement tgtEl, SlideContext slide)
    {
        if (tgtEl.Element(Ns.P + "sldTgt") != null)
            return new JsonObject { ["sld"] = true };
        if (tgtEl.Element(Ns.P + "sndTgt") is { } sound)
            return Sound(slide, sound) is { } src ? new JsonObject { ["snd"] = src, ["name"] = sound.Str("name") } : null;
        if (tgtEl.Element(Ns.P + "inkTgt") is { } ink)
            return new JsonObject { ["ink"] = ink.Str("spid") };
        // A media bookmark (p14:bmkTgt) that starts a trigger sequence when playback reaches it.
        if (tgtEl.Descendants().FirstOrDefault(e => e.Name.LocalName == "bmkTgt") is { } bookmark)
            return new JsonObject { ["spid"] = bookmark.Str("spid"), ["bmk"] = bookmark.Str("bmkName") ?? "" };
        if (tgtEl.Element(Ns.P + "spTgt") is not { } shape)
            return null;
        var json = new JsonObject { ["spid"] = shape.Str("spid") };
        if (shape.Element(Ns.P + "bg") != null)
            json["bg"] = true;
        if (shape.Element(Ns.P + "subSp") is { } sub)
            json["subSp"] = sub.Str("spid");
        if (shape.Element(Ns.P + "txEl") is { } text)
        {
            if (text.Element(Ns.P + "pRg") is { } paragraphs)
                json["pRg"] = new JsonArray(paragraphs.Int("st") ?? 0, paragraphs.Int("end") ?? 0);
            if (text.Element(Ns.P + "charRg") is { } characters)
                json["charRg"] = new JsonArray(characters.Int("st") ?? 0, characters.Int("end") ?? 0);
        }
        if (shape.Element(Ns.P + "graphicEl") is { } graphic)
        {
            var part = graphic.Elements().FirstOrDefault();
            var item = new JsonObject { ["kind"] = part?.Name.LocalName };
            foreach (var attribute in part?.Attributes() ?? [])
                item[attribute.Name.LocalName] = Value(attribute.Value);
            json["graphic"] = item;
        }
        if (shape.Element(Ns.P + "oleChartEl") is { } chart)
            json["chartEl"] = new JsonObject { ["type"] = chart.Str("type"), ["lvl"] = chart.Int("lvl") ?? 0 };
        return json;
    }

    /// <summary>p:val, p:to or p:progress: a string, boolean, integer, float or color value.</summary>
    static JsonNode? AnimValue(XElement? value, SlideContext slide)
    {
        var inner = value?.Elements().FirstOrDefault();
        return inner?.Name.LocalName switch
        {
            "strVal" => inner.Str("val"),
            "boolVal" => inner.Bool("val") ?? false,
            "intVal" => inner.Int("val") ?? 0,
            "fltVal" => Number(inner.Str("val")),
            "clrVal" => Color(inner, slide),
            _ => null,
        };
    }

    static JsonNode? Color(XElement parent, SlideContext slide) =>
        slide.Colors.ResolveChild(parent) is { } color ? color.ToHex() : null;

    /// <summary>An embedded sound (p:snd, p:sndTgt) as a data URI.</summary>
    static string? Sound(SlideContext slide, XElement? sound)
    {
        if (sound?.Attribute(Ns.R + "embed") is not { } embed)
            return null;
        var package = slide.Presentation.Package;
        var rel = package.Relationship(slide.SlidePart, embed.Value);
        if (rel is not { External: false } || !package.Exists(rel.Target))
            return null;
        var type = package.ContentType(rel.Target);
        if (string.IsNullOrEmpty(type) || type == "application/octet-stream") type = "audio/wav";
        return $"data:{type};base64,{Convert.ToBase64String(package.ReadBytes(rel.Target))}";
    }

    static void Copy(XElement element, JsonObject json, params string[] attributes)
    {
        foreach (var name in attributes)
        {
            if (element.Attribute(name) is { } attribute)
                json[name] = Value(attribute.Value);
        }
    }

    /// <summary>Numbers as numbers, booleans as booleans, everything else as strings.</summary>
    static JsonNode? Value(string value)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            return integer;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return number;
        return value switch { "true" or "1" => true, "false" or "0" => false, _ => value };
    }

    static double Number(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0;
}
