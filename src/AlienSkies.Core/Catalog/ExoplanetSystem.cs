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
    double? Eccentricity)
{
    public Vector3d HostPosition => Equatorial.ToCartesian(RaDeg, DecDeg, DistanceParsecs);

    /// <summary>Absolute V of the host; falls back to solar if the archive has no V magnitude.</summary>
    public double HostAbsoluteV =>
        HostApparentV is double v ? Units.AbsoluteMagnitude(v, DistanceParsecs) : Units.SunAbsoluteV;

    public override string ToString() => PlanetName;
}
