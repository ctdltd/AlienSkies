using AlienSkies.Core.Catalog;
using AlienSkies.Core.Math;

namespace AlienSkies.Core.Astronomy;

/// <summary>A star as it appears from the alien system: fixed direction (ICRS-aligned) and brightness.</summary>
public sealed record VisibleStar(string Id, string? Name, Vector3d Direction, double DistanceParsecs, double ApparentMagnitude, double ColorBV)
{
    public bool IsSol => Id == AlienSky.SolId;
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

    private AlienSky(ExoplanetSystem system, IReadOnlyList<VisibleStar> stars)
    {
        Planet = system;
        Stars = stars;
    }

    /// <param name="limitingMagnitude">Faintest magnitude to keep (≈6.5 for a dark sky, naked eye).</param>
    /// <param name="includeSol">Add our own Sun as a star in their sky.</param>
    public static AlienSky Build(ExoplanetSystem system, IEnumerable<Star> catalog, double limitingMagnitude = 6.5, bool includeSol = true)
    {
        var host = system.HostPosition;
        // Anything this close to the host position is taken to be the host itself (catalog cross-match slop).
        double selfRadius = System.Math.Max(0.02, system.DistanceParsecs * 0.005);

        var stars = new List<VisibleStar>();
        foreach (var s in catalog)
        {
            var rel = s.Position - host;
            double d = rel.Length;
            if (d < selfRadius) continue;
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
        return new AlienSky(system, stars);
    }

    /// <summary>Apparent magnitude of the host star seen from the planet.</summary>
    public static double HostApparentMagnitude(ExoplanetSystem system, double distanceAu) =>
        Units.ApparentMagnitude(system.HostAbsoluteV, distanceAu * Units.ParsecsPerAu);

    /// <summary>Angular radius of the host star's disc in radians.</summary>
    public static double HostAngularRadius(ExoplanetSystem system, double distanceAu) =>
        (system.HostRadiusSolar ?? 1.0) * Units.SolarRadiusAu / distanceAu;
}
