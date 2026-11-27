using AlienSkies.Core.Math;

namespace AlienSkies.Core.Astronomy;

/// <summary>How a sibling planet looks from the viewer's planet at one moment.</summary>
/// <param name="Direction">Unit vector from the viewer toward the sibling, ICRS-aligned.</param>
/// <param name="PhaseAngle">Star–sibling–viewer angle in radians: 0 = full, π = new.</param>
/// <param name="TowardStar">Unit vector, perpendicular to <paramref name="Direction"/>, pointing toward the host star on the sky: the lit limb faces this way.</param>
public readonly record struct SiblingView(
    Vector3d Direction, double DistanceAu, double PhaseAngle, double Magnitude, double AngularRadius, Vector3d TowardStar)
{
    /// <summary>Fraction of the visible disc that is lit (1 = full).</summary>
    public double IlluminatedFraction => (1 + System.Math.Cos(PhaseAngle)) / 2;
}

/// <summary>
/// Another planet of the viewer's own system, such as TRAPPIST-1 f seen from TRAPPIST-1 e. It shines by reflected
/// starlight and wanders against the stars as both planets orbit, so it is evaluated every frame rather than placed
/// once like a star.
/// </summary>
public sealed class SiblingPlanet
{
    public const double EarthRadiusAu = 4.263_5e-5;

    private readonly PlanetFrame _frame;

    public string Name { get; }
    public PlanetConfig Config => _frame.Config;
    public double RadiusEarth { get; }

    /// <summary>Geometric albedo: 0.3 for rocky worlds (Earth ≈ 0.37), 0.5 for giants (Jupiter ≈ 0.54).</summary>
    public double GeometricAlbedo { get; }

    /// <summary>
    /// The archive has no orbital phases, so every planet would start at periapsis together, lined up in one
    /// direction. Each sibling instead starts at a fixed, name-derived fraction of its orbit.
    /// </summary>
    public double PhaseOffsetDays { get; }

    public SiblingPlanet(PlanetConfig config)
    {
        _frame = new PlanetFrame(config);
        Name = config.Planet.PlanetName;
        RadiusEarth = config.Planet.PlanetRadiusEarth is > 0 and var r ? r : 1.0;
        GeometricAlbedo = RadiusEarth > 6 ? 0.5 : 0.3;
        PhaseOffsetDays = StablePhase(Name) * config.OrbitalPeriodDays;
    }

    /// <summary>A fixed fraction in [0, 1) derived from the name (FNV-1a), so it is the same on every run and platform.</summary>
    public static double StablePhase(string name)
    {
        unchecked
        {
            uint h = 2_166_136_261;
            foreach (char c in name.ToUpperInvariant()) h = (h ^ c) * 16_777_619;
            return (h % 10_000) / 10_000.0;
        }
    }

    /// <summary>Position relative to the host star, AU.</summary>
    public Vector3d OffsetAu(double tDays) => _frame.PlanetOffsetAu(tDays + PhaseOffsetDays);

    /// <param name="viewerOffsetAu">The viewer's planet, relative to the host star, AU.</param>
    /// <param name="hostMagnitudeAt1Au">How bright the host star looks from 1 AU away.</param>
    public SiblingView Observe(double tDays, Vector3d viewerOffsetAu, double hostMagnitudeAt1Au)
    {
        var p = OffsetAu(tDays);
        var rel = p - viewerOffsetAu;
        double delta = System.Math.Max(rel.Length, 1e-7);
        double r = System.Math.Max(p.Length, 1e-7);
        var dir = rel / delta;

        // Phase angle at the sibling between the star (-p) and the viewer (-rel).
        double cosAlpha = System.Math.Clamp(Vector3d.Dot(-p / r, -dir), -1, 1);
        double alpha = System.Math.Acos(cosAlpha);

        double radiusAu = RadiusEarth * EarthRadiusAu;
        double mag = ReflectedMagnitude(hostMagnitudeAt1Au, radiusAu, GeometricAlbedo, r, delta, alpha);

        // Toward the star, as seen from the viewer, projected onto the sky at the sibling.
        var toStar = (-viewerOffsetAu).Normalized();
        var towardStar = toStar - dir * Vector3d.Dot(toStar, dir);
        towardStar = towardStar.Length < 1e-9 ? Vector3d.Zero : towardStar.Normalized();

        return new SiblingView(dir, delta, alpha, mag, System.Math.Atan(radiusAu / delta), towardStar);
    }

    /// <summary>
    /// Magnitude of a planet shining by reflected starlight: starlight falls off as 1/r² on the way out and the
    /// reflection as 1/Δ² on the way back. All lengths in AU; <paramref name="alpha"/> is the phase angle in radians.
    /// </summary>
    public static double ReflectedMagnitude(double hostMagnitudeAt1Au, double radiusAu, double geometricAlbedo,
                                            double starDistanceAu, double viewerDistanceAu, double alpha)
    {
        double ratio = geometricAlbedo * radiusAu * radiusAu * LambertPhase(alpha)
                       / (starDistanceAu * starDistanceAu * viewerDistanceAu * viewerDistanceAu);
        return ratio > 0 ? hostMagnitudeAt1Au - 2.5 * System.Math.Log10(ratio) : double.PositiveInfinity;
    }

    /// <summary>Lambert-sphere phase function: 1 at full phase, 1/π at quarter, 0 at new.</summary>
    public static double LambertPhase(double alpha)
    {
        alpha = System.Math.Clamp(alpha, 0, System.Math.PI);
        return (System.Math.Sin(alpha) + (System.Math.PI - alpha) * System.Math.Cos(alpha)) / System.Math.PI;
    }

    /// <summary>
    /// The other planets of the viewer's system among <paramref name="candidates"/>: same host star name, a different
    /// planet name. Siblings with an unknown inclination take the viewer's, since planets in one system are nearly
    /// coplanar, and all share the viewer's ascending node for the same reason.
    /// </summary>
    public static IReadOnlyList<SiblingPlanet> FindSiblings(PlanetConfig viewer, IEnumerable<PlanetConfig> candidates)
    {
        var list = new List<SiblingPlanet>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { viewer.Planet.PlanetName };
        foreach (var c in candidates)
        {
            if (!c.Planet.HostName.Equals(viewer.Planet.HostName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!seen.Add(c.Planet.PlanetName)) continue;
            if (c.OrbitalPeriodDays <= 0 || c.SemiMajorAxisAu <= 0) continue;
            list.Add(new SiblingPlanet(new PlanetConfig
            {
                Planet = c.Planet,
                SemiMajorAxisAu = c.SemiMajorAxisAu,
                OrbitalPeriodDays = c.OrbitalPeriodDays,
                Eccentricity = c.Eccentricity,
                InclinationDeg = c.Planet.InclinationDeg ?? (c.Planet.IsImaginary ? c.InclinationDeg : viewer.InclinationDeg),
                AscendingNodeDeg = viewer.AscendingNodeDeg,
                ArgumentOfPeriapsisDeg = c.ArgumentOfPeriapsisDeg,
                TidallyLocked = c.TidallyLocked,
                LatitudeDeg = 0,
                LongitudeDeg = 0,
            }));
        }
        return list;
    }
}
