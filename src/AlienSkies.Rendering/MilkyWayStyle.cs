using System.ComponentModel;

namespace AlienSkies.Rendering;

/// <summary>How the Milky Way band is drawn.</summary>
public enum MilkyWayStyle
{
    /// <summary>A few nested, slightly lighter tints of the sky that show where the band is and where it is brightest.</summary>
    [Description("Soft tint (fast)")] Tint,
    /// <summary>The full texture with dust lanes and star clouds. Costs several times as much per frame.</summary>
    [Description("Detailed (slower)")] Detailed,
}
