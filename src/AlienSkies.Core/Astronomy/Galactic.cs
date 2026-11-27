using AlienSkies.Core.Math;

namespace AlienSkies.Core.Astronomy;

/// <summary>
/// Galactic coordinates (l, b) and the parallax of the Milky Way itself.
/// The galactic frame is a fixed rotation of ICRS, so it is the same for every viewer; what changes with the
/// viewer's position is where the Milky Way's light appears in it (see <see cref="EarthDirectionOf"/>).
/// </summary>
public static class Galactic
{
    // ICRS → galactic rotation (Hipparcos catalogue, ESA 1997, vol. 1 §1.5.3). Rows are the galactic x, y, z axes.
    private static readonly Vector3d Gx = new(-0.054_875_560_4, -0.873_437_090_2, -0.483_835_015_5);
    private static readonly Vector3d Gy = new(0.494_109_427_9, -0.444_829_630_0, 0.746_982_244_5);
    private static readonly Vector3d Gz = new(-0.867_666_149_0, -0.198_076_373_4, 0.455_983_776_2);

    /// <summary>
    /// Typical distance of the stars and dust that make up the band we see, in parsecs. The band's light comes from
    /// a few hundred parsecs to several kiloparsecs away; 1.5 kpc is a middle value. It only matters for viewers far
    /// from the Sun, who see the band shift (and the galactic centre move) by parallax.
    /// </summary>
    public const double BandDistanceParsecs = 1500;

    /// <summary>The parallax model breaks down once the viewer is a sizeable fraction of <see cref="BandDistanceParsecs"/> away.</summary>
    public const double MaxViewerOffsetParsecs = 900;

    /// <summary>ICRS-aligned vector to galactic (x toward the galactic centre, z toward the north galactic pole).</summary>
    public static Vector3d FromIcrs(Vector3d v) => new(Vector3d.Dot(Gx, v), Vector3d.Dot(Gy, v), Vector3d.Dot(Gz, v));

    /// <summary>Galactic vector back to ICRS-aligned.</summary>
    public static Vector3d ToIcrs(Vector3d g) => Gx * g.X + Gy * g.Y + Gz * g.Z;

    /// <summary>Galactic longitude in [0, 360) and latitude in [-90, 90], degrees, of a galactic-frame direction.</summary>
    public static (double LDeg, double BDeg) ToLB(Vector3d g)
    {
        double len = g.Length;
        if (len == 0) return (0, 0);
        double l = Units.NormalizeRadians(System.Math.Atan2(g.Y, g.X)) / Units.Deg;
        double b = System.Math.Asin(System.Math.Clamp(g.Z / len, -1, 1)) / Units.Deg;
        return (l, b);
    }

    /// <summary>Galactic-frame unit vector for a longitude and latitude in degrees.</summary>
    public static Vector3d FromLB(double lDeg, double bDeg)
    {
        double l = lDeg * Units.Deg, b = bDeg * Units.Deg;
        return new Vector3d(System.Math.Cos(b) * System.Math.Cos(l), System.Math.Cos(b) * System.Math.Sin(l), System.Math.Sin(b));
    }

    /// <summary>
    /// For a viewer at <paramref name="viewerPositionParsecs"/> (heliocentric ICRS) looking along the ICRS direction
    /// <paramref name="viewerDirection"/>, the direction in which Earth sees the same patch of Milky Way. Treats the
    /// band as lying <see cref="BandDistanceParsecs"/> away, so for every catalog star within a few tens of parsecs this
    /// is the identity to within a degree or so: the Milky Way looks nearly the same from all of them. Viewers farther
    /// out than <see cref="MaxViewerOffsetParsecs"/> are clamped to that distance (in the same direction).
    /// </summary>
    public static Vector3d EarthDirectionOf(Vector3d viewerDirection, Vector3d viewerPositionParsecs)
    {
        double d = viewerPositionParsecs.Length;
        var x = d > MaxViewerOffsetParsecs ? viewerPositionParsecs * (MaxViewerOffsetParsecs / d) : viewerPositionParsecs;
        return (x + viewerDirection.Normalized() * BandDistanceParsecs).Normalized();
    }
}
