using AlienSkies.Core.Math;

namespace AlienSkies.Core.Catalog;

/// <summary>A catalog star, stored heliocentrically so it can be re-viewed from anywhere.</summary>
/// <param name="Id">Hipparcos number (or Gaia source id) as text.</param>
/// <param name="Position">Heliocentric ICRS position in parsecs.</param>
/// <param name="AbsoluteMagnitude">Absolute V magnitude (derived from apparent V and parallax).</param>
/// <param name="ColorBV">B−V colour index, used for rendering tint.</param>
public sealed record Star(string Id, string? Name, Vector3d Position, double AbsoluteMagnitude, double ColorBV);
