using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate.Docx;

/// <summary>The WordprocessingML namespace and helpers for its attribute conventions (w:val, twips, half-points, ST_OnOff).</summary>
public static class Wml
{
    public static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public static readonly XNamespace W14 = "http://schemas.microsoft.com/office/word/2010/wordml";
    public static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>The value of a w: attribute.</summary>
    public static string? Attr(this XElement? element, string name) => (string?)element?.Attribute(W + name);

    /// <summary>The value of an attribute of another namespace (r:id).</summary>
    public static string? Attr(this XElement? element, XName name) => (string?)element?.Attribute(name);

    /// <summary>The w:val attribute.</summary>
    public static string? Val(this XElement? element) => element.Attr("val");

    /// <summary>A w: attribute as a number (ST_TwipsMeasure, ST_SignedTwipsMeasure, ST_DecimalNumber …); null when absent or not a number.</summary>
    public static double? Number(this XElement? element, string name)
    {
        var value = element.Attr(name);
        if (value == null)
            return null;
        // Measures may carry a unit (ST_UniversalMeasure: "12pt", "2.5cm"); the bare number is twips or the type's own unit.
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return number;
        return Universal(value) is { } points ? points * 20 : null;
    }

    /// <summary>A length with a unit (ST_UniversalMeasure) in points, or null.</summary>
    public static double? Universal(string value)
    {
        if (value.Length < 3)
            return null;
        var unit = value[^2..];
        if (!double.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return null;
        return unit switch
        {
            "pt" => number,
            "in" => number * 72,
            "cm" => number * 72 / 2.54,
            "mm" => number * 72 / 25.4,
            "pc" or "pi" => number * 12,
            _ => null,
        };
    }

    /// <summary>A w: attribute in twips (or with a unit) as points.</summary>
    public static double? Twips(this XElement? element, string name) => element.Number(name) is { } twips ? twips / 20 : null;

    /// <summary>
    /// An ST_OnOff property element: true when present without w:val or with true/1/on, false for false/0/off, null
    /// when the element is absent.
    /// </summary>
    public static bool? OnOff(this XElement? element) =>
        element == null ? null : element.Val() switch
        {
            "0" or "false" or "off" => false,
            _ => true,
        };

    public static XElement? Child(this XElement? element, string name) => element?.Element(W + name);
}
