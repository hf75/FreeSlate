using System.Xml.Linq;
using Engine.Drawing;

namespace Engine.Pptx;

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
}
