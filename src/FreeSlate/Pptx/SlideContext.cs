using System.Xml.Linq;
using FreeSlate.Drawing;

namespace FreeSlate.Pptx;

/// <summary>One slide with its layout, master, theme and effective color map.</summary>
public sealed class SlideContext
{
    public SlideContext(PresentationContext presentation, string slidePart, int number)
    {
        Presentation = presentation;
        Number = number;
        SlidePart = slidePart;
        Slide = presentation.Load(slidePart);

        var package = presentation.Package;
        LayoutPart = package.FirstRelationship(slidePart, "slideLayout")?.Target;
        Layout = LayoutPart != null && package.Exists(LayoutPart) ? presentation.Load(LayoutPart) : null;
        MasterPart = LayoutPart != null ? package.FirstRelationship(LayoutPart, "slideMaster")?.Target : null;
        Master = MasterPart != null && package.Exists(MasterPart) ? presentation.Load(MasterPart) : null;
        Theme = MasterPart != null ? presentation.ThemeOf(MasterPart) : Theme.Load(null);
        foreach (var part in new[] { LayoutPart, slidePart })
        {
            var target = part != null ? package.FirstRelationship(part, "themeOverride")?.Target : null;
            if (target != null && package.Exists(target) && presentation.Load(target).Root is { } themeOverride)
                Theme = Theme.WithOverride(themeOverride, target);
        }

        // Color map: the master's p:clrMap, overridden by the layout's and then the slide's p:clrMapOvr.
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in Master?.Root?.Element(Ns.P + "clrMap")?.Attributes() ?? [])
            map[a.Name.LocalName] = a.Value;
        foreach (var doc in new[] { Layout, Slide })
        {
            foreach (var a in doc?.Root?.Element(Ns.P + "clrMapOvr")?.Element(Ns.A + "overrideClrMapping")?.Attributes() ?? [])
                map[a.Name.LocalName] = a.Value;
        }
        Colors = new ColorContext(Theme, map);
    }

    /// <summary>
    /// A context for the drawings of another kind of document (a Word document's part): its theme and color map, no
    /// layout or master (catalog: drawing.inline, shapes drawn by the slide renderer).
    /// </summary>
    public SlideContext(PresentationContext presentation, string part, Theme theme, IReadOnlyDictionary<string, string> colorMap)
    {
        Presentation = presentation;
        SlidePart = part;
        Slide = presentation.Load(part);
        Theme = theme;
        Colors = new ColorContext(theme, colorMap);
    }

    public PresentationContext Presentation { get; }
    public int Number { get; }
    public string SlidePart { get; }
    public string? LayoutPart { get; }
    public string? MasterPart { get; }
    public XDocument Slide { get; }
    public XDocument? Layout { get; }
    public XDocument? Master { get; }
    public Theme Theme { get; }
    public ColorContext Colors { get; }

    /// <summary>The image resolver for shapes of the given part (relationship ids are per part).</summary>
    public Func<string, string?> Images(string part) => id => Presentation.Image(part, id);

    /// <summary>Picture sizes for tiled fills of the given part.</summary>
    public Func<string, double, (double W, double H)> ImageSizes(string part) => (id, dpi) => Presentation.ImageSize(part, id, dpi);

    /// <summary>
    /// A fill of the theme's style matrix (fillRef, bgRef) in the given colors; its pictures come from the part the style
    /// is defined in (catalog: theme.formatScheme).
    /// </summary>
    public Fill ThemedFill(XElement style, ColorContext colors) =>
        Theme.PartOf(style) is { } part ? FillReader.Read(style, colors, Images(part), ImageSizes(part)) : FillReader.Read(style, colors);
}
