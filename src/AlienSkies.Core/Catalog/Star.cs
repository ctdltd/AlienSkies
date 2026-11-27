using AlienSkies.Core.Math;

namespace AlienSkies.Core.Catalog;

/// <summary>A catalog star, stored heliocentrically so it can be re-viewed from anywhere.</summary>
/// <param name="Id">Hipparcos number (or Gaia source id) as text.</param>
/// <param name="Position">Heliocentric ICRS position in parsecs.</param>
/// <param name="AbsoluteMagnitude">Absolute V magnitude (derived from apparent V and parallax).</param>
/// <param name="ColorBV">B−V colour index, used for rendering tint.</param>
public sealed record Star(string Id, string? Name, Vector3d Position, double AbsoluteMagnitude, double ColorBV)
{
    /// <summary>
    /// Finds a star by proper name (case-insensitive), catalog id, or "HIP n". Returns null when nothing matches.
    /// </summary>
    public static Star? Find(IEnumerable<Star> catalog, string? nameOrId)
    {
        var q = nameOrId?.Trim();
        if (string.IsNullOrEmpty(q)) return null;
        if (q.StartsWith("HIP", StringComparison.OrdinalIgnoreCase)) q = q[3..].Trim();
        Star? byId = null;
        foreach (var s in catalog)
        {
            if (string.Equals(s.Name, nameOrId!.Trim(), StringComparison.OrdinalIgnoreCase)) return s;
            byId ??= s.Id == q ? s : null;
        }
        return byId;
    }
}
