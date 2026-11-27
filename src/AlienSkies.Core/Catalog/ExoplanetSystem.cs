using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Math;

namespace AlienSkies.Core.Catalog;

/// <summary>
/// One planet row from the exoplanet catalog (NASA Exoplanet Archive pscomppars schema, trimmed).
/// Nullable fields are frequently unknown in the archive.
/// </summary>
public sealed record ExoplanetSystem(
    string PlanetName,
    string HostName,
    double RaDeg,
    double DecDeg,
    double DistanceParsecs,
    double? HostApparentV,
    double? HostRadiusSolar,
    double? HostMassSolar,
    double? SemiMajorAxisAu,
    double? OrbitalPeriodDays,
    double? InclinationDeg,
    double? Eccentricity,
    double? HostColorBV = null,
    bool IsImaginary = false)
{
    /// <summary>
    /// A made-up planet around any catalog star. The host's mass, radius and luminosity are estimated from its
    /// magnitude and colour (<see cref="StellarEstimates"/>), and the planet is placed where it gets Earth's
    /// sunlight, on a circular orbit whose period follows from Kepler's third law.
    /// </summary>
    public static ExoplanetSystem Imaginary(Star host, string? planetName = null)
    {
        var (ra, dec, d) = Equatorial.FromCartesian(host.Position);
        var est = StellarEstimates.Estimate(host.AbsoluteMagnitude, host.ColorBV);
        double mass = est.MassSolar;
        double a = StellarEstimates.EarthEquivalentDistanceAu(est.LuminositySolar);
        string hostName = host.Name ?? $"HIP {host.Id}";
        return new ExoplanetSystem(
            string.IsNullOrWhiteSpace(planetName) ? $"{hostName} (imaginary)" : planetName.Trim(),
            hostName, ra, dec, d,
            HostApparentV: Units.ApparentMagnitude(host.AbsoluteMagnitude, d),
            HostRadiusSolar: est.RadiusSolar,
            HostMassSolar: mass,
            SemiMajorAxisAu: a,
            OrbitalPeriodDays: PlanetConfig.KeplerPeriodDays(a, mass),
            InclinationDeg: null,
            Eccentricity: 0,
            HostColorBV: host.ColorBV,
            IsImaginary: true);
    }

    public Vector3d HostPosition => Equatorial.ToCartesian(RaDeg, DecDeg, DistanceParsecs);

    /// <summary>Absolute V of the host; falls back to solar if the archive has no V magnitude.</summary>
    public double HostAbsoluteV =>
        HostApparentV is double v ? Units.AbsoluteMagnitude(v, DistanceParsecs) : Units.SunAbsoluteV;

    public override string ToString() => PlanetName;
}
