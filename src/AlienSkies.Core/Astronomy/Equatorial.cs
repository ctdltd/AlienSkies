using AlienSkies.Core.Math;

namespace AlienSkies.Core.Astronomy;

/// <summary>
/// Conversions between ICRS spherical coordinates (RA, Dec, distance) and
/// heliocentric ICRS Cartesian coordinates in parsecs.
/// Axes: +X toward RA 0/Dec 0, +Y toward RA 90°/Dec 0, +Z toward the north celestial pole.
/// </summary>
public static class Equatorial
{
    public static Vector3d ToCartesian(double raDeg, double decDeg, double distanceParsecs)
    {
        double ra = raDeg * Units.Deg, dec = decDeg * Units.Deg;
        double c = System.Math.Cos(dec);
        return new Vector3d(c * System.Math.Cos(ra), c * System.Math.Sin(ra), System.Math.Sin(dec)) * distanceParsecs;
    }

    public static (double RaDeg, double DecDeg, double DistanceParsecs) FromCartesian(Vector3d v)
    {
        double d = v.Length;
        if (d == 0) return (0, 0, 0);
        double ra = Units.NormalizeRadians(System.Math.Atan2(v.Y, v.X));
        double dec = System.Math.Asin(System.Math.Clamp(v.Z / d, -1, 1));
        return (ra / Units.Deg, dec / Units.Deg, d);
    }

    /// <summary>Parallax in milliarcseconds to distance in parsecs.</summary>
    public static double ParallaxToParsecs(double parallaxMas) => 1000.0 / parallaxMas;

    /// <summary>Sky-plane basis at a target as seen from Earth: (toward target, north, east).</summary>
    public static (Vector3d LineOfSight, Vector3d North, Vector3d East) SkyPlaneBasis(double raDeg, double decDeg)
    {
        double ra = raDeg * Units.Deg, dec = decDeg * Units.Deg;
        double sa = System.Math.Sin(ra), ca = System.Math.Cos(ra);
        double sd = System.Math.Sin(dec), cd = System.Math.Cos(dec);
        var los = new Vector3d(cd * ca, cd * sa, sd);
        var north = new Vector3d(-sd * ca, -sd * sa, cd);
        var east = new Vector3d(-sa, ca, 0);
        return (los, north, east);
    }
}
