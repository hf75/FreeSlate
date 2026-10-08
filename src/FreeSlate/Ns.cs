using System.Globalization;
using System.Xml.Linq;

namespace FreeSlate;

/// <summary>XML namespaces and small helpers for reading Office Open XML with LINQ to XML.</summary>
public static class Ns
{
    public static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    public static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    public static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public static readonly XNamespace Asvg = "http://schemas.microsoft.com/office/drawing/2016/SVG/main";
    public static readonly XNamespace C = "http://schemas.openxmlformats.org/drawingml/2006/chart";
    public static readonly XNamespace Cs = "http://schemas.microsoft.com/office/drawing/2012/chartStyle";

    public static long? Long(this XElement? element, string attribute) =>
        element?.Attribute(attribute) is { } a && long.TryParse(a.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    public static int? Int(this XElement? element, string attribute) =>
        element?.Attribute(attribute) is { } a && int.TryParse(a.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    public static bool? Bool(this XElement? element, string attribute) =>
        element?.Attribute(attribute)?.Value switch
        {
            "1" or "true" or "on" => true,
            "0" or "false" or "off" => false,
            _ => null,
        };

    public static string? Str(this XElement? element, string attribute) => (string?)element?.Attribute(attribute);
}
