using AlienSkies.Core.Math;

namespace AlienSkies.Core.Astronomy;

/// <summary>
/// The relative orbit of a binary's secondary around its primary, in the Campbell elements that visual and
/// eclipsing-binary papers publish. Angles are measured on Earth's sky, as in <see cref="PlanetConfig"/>:
/// inclination 0° is face-on and below 90° the secondary's position angle increases (north through east);
/// the ascending node is where the secondary crosses the sky plane moving away from us.
/// </summary>
/// <param name="PeriodDays">Orbital period.</param>
/// <param name="SemiMajorAxisAu">Semi-major axis of the relative orbit (the separation, not each star's own orbit).</param>
/// <param name="PeriapsisDays">A time of periastron, in days since J2000.0 (the simulation clock's day 0).</param>
public sealed record BinaryOrbit(
    double PeriodDays,
    double SemiMajorAxisAu,
    double Eccentricity,
    double InclinationDeg,
    double AscendingNodeDeg,
    double ArgumentOfPeriapsisDeg,
    double PeriapsisDays)
{
    public const double DaysPerJulianYear = 365.25;

    /// <summary>Days since J2000.0 for a Julian year such as 2035.48.</summary>
    public static double DaysSinceJ2000(double julianYear) => (julianYear - 2000.0) * DaysPerJulianYear;

    /// <summary>Julian year for a time in days since J2000.0.</summary>
    public static double JulianYear(double daysSinceJ2000) => 2000.0 + daysSinceJ2000 / DaysPerJulianYear;

    public double PeriastronAu => SemiMajorAxisAu * (1 - Eccentricity);
    public double ApastronAu => SemiMajorAxisAu * (1 + Eccentricity);

    /// <summary>
    /// The secondary's position relative to the primary in AU, as (north, east, away from Earth): the
    /// Thiele–Innes projection used for visual binaries.
    /// </summary>
    public (double North, double East, double Away) SkyOffsetAu(double tDays)
    {
        double e = Eccentricity;
        double E = Kepler.EccentricAnomaly(2 * System.Math.PI * (tDays - PeriapsisDays) / PeriodDays, e);
        double r = SemiMajorAxisAu * (1 - e * System.Math.Cos(E));
        double u = Kepler.TrueAnomaly(E, e) + ArgumentOfPeriapsisDeg * Units.Deg;
        double node = AscendingNodeDeg * Units.Deg, inc = InclinationDeg * Units.Deg;
        double cu = System.Math.Cos(u), su = System.Math.Sin(u);
        double cn = System.Math.Cos(node), sn = System.Math.Sin(node), ci = System.Math.Cos(inc);
        return (r * (cu * cn - su * sn * ci), r * (cu * sn + su * cn * ci), r * su * System.Math.Sin(inc));
    }

    /// <summary>The same offset as an ICRS-aligned vector, given the sky-plane basis at the system.</summary>
    public Vector3d OffsetAu(double tDays, Vector3d lineOfSight, Vector3d north, Vector3d east)
    {
        var (n, ea, away) = SkyOffsetAu(tDays);
        return north * n + east * ea + lineOfSight * away;
    }

    /// <summary>The primary's orbit around the secondary: the same ellipse, half a turn round.</summary>
    public BinaryOrbit Reversed() => this with { ArgumentOfPeriapsisDeg = ArgumentOfPeriapsisDeg + 180 };

    /// <summary>
    /// A stand-in for a pair whose orbit nobody has measured: circular, face-on to Earth, with the catalog's
    /// projected separation as its radius and the secondary at the catalog position at <paramref name="atDays"/>.
    /// Face-on keeps the pair exactly as far apart as the catalog shows them, at every moment.
    /// </summary>
    public static BinaryOrbit FaceOnCircle(double northAu, double eastAu, double totalMassSolar, double atDays)
    {
        double a = System.Math.Sqrt(northAu * northAu + eastAu * eastAu);
        double positionAngle = System.Math.Atan2(eastAu, northAu) / Units.Deg;
        return new BinaryOrbit(PlanetConfig.KeplerPeriodDays(a, totalMassSolar), a, 0, 0, positionAngle, 0, atDays);
    }
}
