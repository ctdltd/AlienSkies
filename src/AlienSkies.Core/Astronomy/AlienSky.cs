using AlienSkies.Core.Catalog;
using AlienSkies.Core.Math;

namespace AlienSkies.Core.Astronomy;

/// <summary>A star as it appears from the alien system: fixed direction (ICRS-aligned) and brightness.</summary>
public sealed record VisibleStar(string Id, string? Name, Vector3d Direction, double DistanceParsecs, double ApparentMagnitude, double ColorBV)
{
    public bool IsSol => Id == AlienSky.SolId;
}

/// <summary>
/// A star bound to the host (a binary or multiple-system companion), close enough that the planet's
/// own orbit changes its direction and brightness. Rendered as a second sun rather than a fixed star.
/// </summary>
/// <param name="OffsetAu">Position relative to the host star in AU, ICRS-aligned.</param>
public sealed record CompanionStar(string Id, string? Name, Vector3d OffsetAu, double AbsoluteMagnitude, double ColorBV)
{
    public double SeparationAu => OffsetAu.Length;

    /// <summary>Unit vector from the planet toward the companion, and the distance in AU.</summary>
    public (Vector3d Direction, double DistanceAu) FromPlanet(Vector3d planetOffsetAu)
    {
        var rel = OffsetAu - planetOffsetAu;
        double d = rel.Length;
        return (rel / d, d);
    }

    public double ApparentMagnitude(double distanceAu) =>
        Units.ApparentMagnitude(AbsoluteMagnitude, distanceAu * Units.ParsecsPerAu);

    /// <summary>
    /// Radius in solar radii, estimated along the main sequence (L ∝ M⁴, R ∝ M⁰·⁸, so R ∝ L⁰·²).
    /// α Cen B (M_V 5.7) comes out at 0.85 R☉ against a measured 0.86.
    /// </summary>
    public double RadiusSolar => System.Math.Pow(10, -0.08 * (AbsoluteMagnitude - Units.SunAbsoluteV));

    /// <summary>Angular radius of the companion's disc in radians.</summary>
    public double AngularRadius(double distanceAu) => RadiusSolar * Units.SolarRadiusAu / distanceAu;
}

/// <summary>
/// The heart of the idea: re-project a heliocentric catalog onto the sky of another star system.
/// Stellar directions are computed once per system — the planet's orbit (a few AU) is
/// negligible at interstellar distances — and only the planet's spin/orbit changes per frame.
/// </summary>
public sealed class AlienSky
{
    public const string SolId = "Sol";

    public ExoplanetSystem Planet { get; }
    public IReadOnlyList<VisibleStar> Stars { get; }

    /// <summary>Other members of the host's multiple system (e.g. Toliman for a planet of α Cen A).</summary>
    public IReadOnlyList<CompanionStar> Companions { get; }

    private AlienSky(ExoplanetSystem system, IReadOnlyList<VisibleStar> stars, IReadOnlyList<CompanionStar> companions)
    {
        Planet = system;
        Stars = stars;
        Companions = companions;
    }

    /// <param name="limitingMagnitude">Faintest magnitude to keep (≈6.5 for a dark sky, naked eye).</param>
    /// <param name="includeSol">Add our own Sun as a star in their sky.</param>
    public static AlienSky Build(ExoplanetSystem system, IEnumerable<Star> catalog, double limitingMagnitude = 6.5, bool includeSol = true)
    {
        var host = system.HostPosition;
        double selfRadius = SelfRadiusParsecs(system);

        var stars = new List<VisibleStar>();
        var near = new List<(Star Star, Vector3d Rel, double D)>();
        foreach (var s in catalog)
        {
            var rel = s.Position - host;
            double d = rel.Length;
            if (d < selfRadius)
            {
                near.Add((s, rel, d));
                continue;
            }
            double m = Units.ApparentMagnitude(s.AbsoluteMagnitude, d);
            if (m > limitingMagnitude) continue;
            stars.Add(new VisibleStar(s.Id, s.Name, rel / d, d, m, s.ColorBV));
        }

        if (includeSol)
        {
            var rel = Vector3d.Zero - host;
            double d = rel.Length;
            double m = Units.ApparentMagnitude(Units.SunAbsoluteV, d);
            // Sol is always kept (even if fainter than the limit) so it can be marked.
            stars.Add(new VisibleStar(SolId, "Sun (Sol)", rel / d, d, m, 0.65));
        }

        stars.Sort((a, b) => b.ApparentMagnitude.CompareTo(a.ApparentMagnitude)); // faint first, bright drawn on top
        return new AlienSky(system, stars, FindCompanions(host, near));
    }

    /// <summary>
    /// Inside the self radius, the catalog star nearest the host position is the host itself; the rest
    /// are its companions. Their parallaxes are no better than the cross-match slop, so the line-of-sight
    /// offset is noise: keep only the sky-plane (projected) separation, which is also the closest the pair
    /// can be, and place the companion at the host's distance.
    /// </summary>
    private static List<CompanionStar> FindCompanions(Vector3d host, List<(Star Star, Vector3d Rel, double D)> near)
    {
        var companions = new List<CompanionStar>();
        if (near.Count < 2) return companions;

        var self = near.MinBy(n => n.D);
        var los = host.Normalized();
        foreach (var (s, rel, _) in near)
        {
            if (ReferenceEquals(s, self.Star)) continue;
            var projected = (rel - los * Vector3d.Dot(rel, los)) * Units.AuPerParsec;
            if (projected.Length < 1e-3) continue; // unresolved duplicate entry, not a companion
            // Absolute magnitude stays as the catalog derived it: same distance, same luminosity.
            companions.Add(new CompanionStar(s.Id, s.Name, projected, s.AbsoluteMagnitude, s.ColorBV));
        }
        return companions;
    }

    /// <summary>
    /// Catalog stars this close to the host position are taken to be the host itself or its companions (catalog
    /// cross-match slop), not background stars.
    /// </summary>
    public static double SelfRadiusParsecs(ExoplanetSystem system) => System.Math.Max(0.02, system.DistanceParsecs * 0.005);

    /// <summary>Apparent magnitude of the host star seen from the planet.</summary>
    public static double HostApparentMagnitude(ExoplanetSystem system, double distanceAu) =>
        Units.ApparentMagnitude(system.HostAbsoluteV, distanceAu * Units.ParsecsPerAu);

    /// <summary>Angular radius of the host star's disc in radians.</summary>
    public static double HostAngularRadius(ExoplanetSystem system, double distanceAu) =>
        (system.HostRadiusSolar ?? 1.0) * Units.SolarRadiusAu / distanceAu;
}
