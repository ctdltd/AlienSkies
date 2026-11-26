using AlienSkies.Core.Catalog;

namespace AlienSkies.Core.Astronomy;

/// <summary>
/// Everything needed to stand on a (real or hypothetical) planet and look up.
/// Orbital elements follow exoplanet-archive conventions, i.e. they are measured relative
/// to the sky plane as seen from Earth — NOT relative to anything at the planet.
/// </summary>
public sealed class PlanetConfig
{
    public required ExoplanetSystem Planet { get; init; }

    // ---- Orbit -------------------------------------------------------------
    public double SemiMajorAxisAu { get; init; } = 1.0;
    public double OrbitalPeriodDays { get; init; } = 365.25;
    public double Eccentricity { get; init; }

    /// <summary>
    /// Inclination of the orbit to the plane of Earth's sky. 0° = face-on to us,
    /// 90° = edge-on (transiting planets are ≈90°, which puts our Sun in their zodiac).
    /// </summary>
    public double InclinationDeg { get; init; } = 90.0;

    /// <summary>Position angle of the ascending node, measured from celestial north through east. Rarely known; default 0.</summary>
    public double AscendingNodeDeg { get; init; }

    public double ArgumentOfPeriapsisDeg { get; init; }

    // ---- Spin --------------------------------------------------------------
    /// <summary>Tilt of the planet's spin axis away from its orbit normal (Earth = 23.44°).</summary>
    public double ObliquityDeg { get; init; }

    /// <summary>Which way (within the orbit plane, from the ascending node) the spin axis leans.</summary>
    public double ObliquityAzimuthDeg { get; init; }

    /// <summary>Tidally locked planets keep one face to their star; the "sun" never moves in the sky.</summary>
    public bool TidallyLocked { get; init; }

    /// <summary>Sidereal rotation period in hours (ignored when tidally locked). Negative = retrograde.</summary>
    public double RotationPeriodHours { get; init; } = 24.0;

    // ---- Observer ----------------------------------------------------------
    public double LatitudeDeg { get; init; } = 30.0;

    /// <summary>Longitude east of the prime meridian. On a locked planet 0° is the substellar point, 180° the far side.</summary>
    public double LongitudeDeg { get; init; } = 0.0;

    /// <summary>Orbital period implied by Kepler's third law for this semi-major axis and the host mass.</summary>
    public static double KeplerPeriodDays(double semiMajorAxisAu, double hostMassSolar) =>
        365.256_363 * System.Math.Sqrt(semiMajorAxisAu * semiMajorAxisAu * semiMajorAxisAu / hostMassSolar);

    /// <summary>Semi-major axis implied by Kepler's third law for this period and host mass.</summary>
    public static double KeplerSemiMajorAxisAu(double periodDays, double hostMassSolar) =>
        System.Math.Cbrt(hostMassSolar * System.Math.Pow(periodDays / 365.256_363, 2));

    /// <summary>Builds a config from the archive values, filling unknowns with sensible defaults.</summary>
    public static PlanetConfig FromCatalog(ExoplanetSystem s, double latitudeDeg = 30, double longitudeDeg = 0)
    {
        double mass = s.HostMassSolar ?? 1.0;
        double a = s.SemiMajorAxisAu ?? (s.OrbitalPeriodDays is double p ? KeplerSemiMajorAxisAu(p, mass) : 1.0);
        double period = s.OrbitalPeriodDays ?? KeplerPeriodDays(a, mass);

        // Close-in planets around small stars are almost certainly tidally locked.
        bool locked = a < 0.1;

        return new PlanetConfig
        {
            Planet = s,
            SemiMajorAxisAu = a,
            OrbitalPeriodDays = period,
            Eccentricity = s.Eccentricity ?? 0,
            InclinationDeg = s.InclinationDeg ?? 90,
            TidallyLocked = locked,
            LatitudeDeg = latitudeDeg,
            LongitudeDeg = longitudeDeg,
        };
    }
}
