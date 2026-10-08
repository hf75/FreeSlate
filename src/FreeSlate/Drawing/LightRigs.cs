namespace FreeSlate.Drawing;

/// <summary>Light rig presets (a:lightRig/@rig) and materials (a:sp3d/@prstMaterial) fitted to PowerPoint's rendering.</summary>
public static class LightRigs
{
    static readonly Dictionary<string, Lighting> Rigs = new()
    {
        ["threePt"] = Lighting.Default,
    };

    /// <summary>The rig of a preset name, or null when unknown.</summary>
    public static Lighting? Get(string? name) => name != null && Rigs.TryGetValue(name, out var rig) ? rig : null;

    /// <summary>The rig as a material reflects it (warmMatte, the default, leaves it unchanged).</summary>
    public static Lighting WithMaterial(Lighting rig, string? material) => rig;
}
