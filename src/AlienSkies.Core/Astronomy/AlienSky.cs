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
/// It follows its binary orbit: the measured one where <see cref="KnownBinaries"/> has it, otherwise a
/// face-on circle at the catalog separation.
/// </summary>
public sealed class CompanionStar
{
    private readonly Vector3d _los, _north, _east;
    private readonly double _scale;
    private readonly double? _radiusSolar;

    public string Id { get; }
    public string? Name { get; }
    public double AbsoluteMagnitude { get; }
    public double ColorBV { get; }

    /// <summary>Relative orbit of this star around the host star (secondary minus primary).</summary>
    public BinaryOrbit Orbit { get; }

    /// <summary>True when <see cref="Orbit"/> is a published orbit rather than the face-on stand-in.</summary>
    public bool OrbitIsMeasured { get; }

    /// <param name="scale">Fraction of the relative orbit this star travels about the centre of the planet's orbit:
    /// 1 when the planet orbits the host, M_host / M_total when it orbits the pair's barycentre.</param>
    public CompanionStar(string id, string? name, BinaryOrbit orbit, bool orbitIsMeasured, double raDeg, double decDeg,
                         double absoluteMagnitude, double colorBV, double scale = 1, double? radiusSolar = null)
    {
        Id = id;
        Name = name;
        Orbit = orbit;
        OrbitIsMeasured = orbitIsMeasured;
        AbsoluteMagnitude = absoluteMagnitude;
        ColorBV = colorBV;
        (_los, _north, _east) = Equatorial.SkyPlaneBasis(raDeg, decDeg);
        _scale = scale;
        _radiusSolar = radiusSolar;
    }

    /// <summary>Position relative to the centre of the planet's orbit, in AU, ICRS-aligned, at time t (days since J2000.0).</summary>
    public Vector3d OffsetAu(double tDays) => Orbit.OffsetAu(tDays, _los, _north, _east) * _scale;

    /// <summary>Distance from the host star in AU.</summary>
    public double SeparationAu(double tDays) => Orbit.OffsetAu(tDays, _los, _north, _east).Length;

    /// <summary>Unit vector from the planet toward the companion, and the distance in AU.</summary>
    public (Vector3d Direction, double DistanceAu) FromPlanet(Vector3d planetOffsetAu, double tDays)
    {
        var rel = OffsetAu(tDays) - planetOffsetAu;
        double d = rel.Length;
        return (rel / d, d);
    }

    public double ApparentMagnitude(double distanceAu) =>
        Units.ApparentMagnitude(AbsoluteMagnitude, distanceAu * Units.ParsecsPerAu);

    /// <summary>
    /// Radius in solar radii: measured where known, otherwise estimated along the main sequence
    /// (L ∝ M⁴, R ∝ M⁰·⁸, so R ∝ L⁰·²). α Cen B (M_V 5.7) comes out at 0.85 R☉ against a measured 0.86.
    /// </summary>
    public double RadiusSolar => _radiusSolar ?? System.Math.Pow(10, -0.08 * (AbsoluteMagnitude - Units.SunAbsoluteV));

    /// <summary>Angular radius of the companion's disc in radians.</summary>
    public double AngularRadius(double distanceAu) => RadiusSolar * Units.SolarRadiusAu / distanceAu;
}

/// <summary>A sun as seen from the planet at one moment: the host star or a companion.</summary>
/// <param name="VisibleFraction">Share of the disc not hidden behind a nearer sun (1 outside eclipses).
/// <paramref name="ApparentMagnitude"/> already includes the dimming.</param>
public readonly record struct SunView(
    string Name, Vector3d Direction, double DistanceAu, double ApparentMagnitude, double AngularRadius,
    double ColorBV, bool IsHost, double VisibleFraction = 1);

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

    /// <summary>
    /// Other members of the host's multiple system (e.g. Toliman for a planet of α Cen A, or the second star of
    /// a circumbinary pair such as Kepler-16 B).
    /// </summary>
    public IReadOnlyList<CompanionStar> Companions { get; }

    /// <summary>The pair a circumbinary planet orbits, or null when the planet orbits a single host star.</summary>
    public CircumbinaryHost? Circumbinary { get; }

    /// <summary>The host star's name as shown in the sky: the primary's own name for a circumbinary pair.</summary>
    public string HostName => Circumbinary?.Primary.Name ?? Planet.HostName;

    /// <summary>Absolute V of the host star alone (for a circumbinary pair, without its partner's light).</summary>
    public double HostAbsoluteV { get; }

    public double HostRadiusSolar => Circumbinary?.Primary.RadiusSolar ?? Planet.HostRadiusSolar ?? 1.0;

    /// <summary>
    /// Host B−V: the circumbinary primary's, the planet definition's, else the measured colour of the catalog star at the
    /// host position, else an estimate from the host's brightness (<see cref="ExoplanetSystem.HostColorBVOrEstimate"/>).
    /// </summary>
    public double HostColorBV => Circumbinary?.Primary.ColorBV ?? Planet.HostColorBV ?? CatalogHostColorBV ?? Planet.HostColorBVOrEstimate;

    /// <summary>
    /// Beyond this distance from Earth the sky is noticeably incomplete: the star catalog only holds stars that look
    /// bright from Earth, so the host's own neighbourhood goes missing (about 3/4 of naked-eye stars remain at 180 pc,
    /// under 1/4 at 550 pc). See DESIGN.md.
    /// </summary>
    public const double SparseBeyondParsecs = 150;

    /// <summary>True when this sky is missing many of the stars a real observer there would see.</summary>
    public bool IsSparse => Planet.DistanceParsecs > SparseBeyondParsecs;

    /// <summary>B−V of the catalog star taken to be the host (the one nearest the host position), when there is one.</summary>
    public double? CatalogHostColorBV { get; private init; }

    private readonly Vector3d _los, _north, _east;

    private AlienSky(ExoplanetSystem system, IReadOnlyList<VisibleStar> stars, IReadOnlyList<CompanionStar> companions,
                     CircumbinaryHost? circumbinary)
    {
        Planet = system;
        Stars = stars;
        Companions = companions;
        Circumbinary = circumbinary;
        HostAbsoluteV = circumbinary is null
            ? system.HostAbsoluteV
            : system.HostAbsoluteV - 2.5 * System.Math.Log10(circumbinary.Primary.LightFractionV);
        (_los, _north, _east) = Equatorial.SkyPlaneBasis(system.RaDeg, system.DecDeg);
    }

    /// <summary>
    /// Host star position relative to the centre of the planet's orbit, in AU. Zero for an ordinary planet;
    /// for a circumbinary planet the host swings around the pair's barycentre.
    /// </summary>
    public Vector3d HostOffsetAu(double tDays) => Circumbinary is { } cb
        ? cb.Orbit.OffsetAu(tDays, _los, _north, _east) * (-cb.Secondary.MassSolar / cb.TotalMassSolar)
        : Vector3d.Zero;

    /// <summary>
    /// Every sun in the planet's sky at time t: the host first, then its companions. Where a nearer sun passes
    /// in front of a farther one, the farther one is dimmed by the share of its disc that is covered.
    /// </summary>
    /// <param name="planetOffsetAu">Planet position relative to the centre of its orbit (<see cref="PlanetFrame.PlanetOffsetAu"/>).</param>
    public IReadOnlyList<SunView> SunsFromPlanet(Vector3d planetOffsetAu, double tDays)
    {
        var suns = new SunView[1 + Companions.Count];
        var rel = HostOffsetAu(tDays) - planetOffsetAu;
        double d = rel.Length;
        suns[0] = new SunView(HostName, rel / d, d,
            Units.ApparentMagnitude(HostAbsoluteV, d * Units.ParsecsPerAu),
            HostRadiusSolar * Units.SolarRadiusAu / d, HostColorBV, IsHost: true);
        for (int i = 0; i < Companions.Count; i++)
        {
            var c = Companions[i];
            var (dir, dist) = c.FromPlanet(planetOffsetAu, tDays);
            suns[i + 1] = new SunView(c.Name ?? c.Id, dir, dist, c.ApparentMagnitude(dist), c.AngularRadius(dist), c.ColorBV, IsHost: false);
        }
        ApplyEclipses(suns);
        return suns;
    }

    private static void ApplyEclipses(SunView[] suns)
    {
        for (int i = 0; i < suns.Length; i++)
        {
            double visible = 1;
            for (int j = 0; j < suns.Length; j++)
            {
                if (j == i || suns[j].DistanceAu >= suns[i].DistanceAu) continue;
                double sep = System.Math.Acos(System.Math.Clamp(Vector3d.Dot(suns[i].Direction, suns[j].Direction), -1, 1));
                visible *= 1 - CoveredFraction(suns[i].AngularRadius, suns[j].AngularRadius, sep);
            }
            if (visible < 1)
            {
                visible = System.Math.Max(visible, 1e-4); // a total eclipse still leaves a corona's worth of light
                suns[i] = suns[i] with
                {
                    VisibleFraction = visible,
                    ApparentMagnitude = suns[i].ApparentMagnitude - 2.5 * System.Math.Log10(visible),
                };
            }
        }
    }

    /// <summary>
    /// Share of a disc of radius <paramref name="r"/> covered by a nearer disc of radius <paramref name="occulter"/>
    /// whose centre is <paramref name="separation"/> away (all small angles, uniform discs, no limb darkening).
    /// </summary>
    public static double CoveredFraction(double r, double occulter, double separation)
    {
        double R = occulter, s = separation;
        if (s >= r + R) return 0;
        if (s <= System.Math.Abs(R - r)) return R >= r ? 1 : R * R / (r * r);
        double a = r * r * System.Math.Acos(System.Math.Clamp((s * s + r * r - R * R) / (2 * s * r), -1, 1));
        double b = R * R * System.Math.Acos(System.Math.Clamp((s * s + R * R - r * r) / (2 * s * R), -1, 1));
        double c = 0.5 * System.Math.Sqrt(System.Math.Max(0, (-s + r + R) * (s + r - R) * (s - r + R) * (s + r + R)));
        return System.Math.Clamp((a + b - c) / (System.Math.PI * r * r), 0, 1);
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
        // A circumbinary pair comes from the binary table: its stars are too faint and too close together for the
        // star catalog, and any catalog entries at the host position are the pair itself.
        var circumbinary = KnownBinaries.CircumbinaryFor(system.HostName);
        var companions = circumbinary is { } cb
            ? [new CompanionStar(cb.Secondary.Name, cb.Secondary.Name, cb.Orbit, orbitIsMeasured: true, system.RaDeg, system.DecDeg,
                   system.HostAbsoluteV - 2.5 * System.Math.Log10(cb.Secondary.LightFractionV), cb.Secondary.ColorBV,
                   scale: cb.Primary.MassSolar / cb.TotalMassSolar, radiusSolar: cb.Secondary.RadiusSolar)]
            : FindCompanions(system, near);
        return new AlienSky(system, stars, companions, circumbinary)
        {
            // Only trust the match when its brightness agrees with the archive's host (a giant 2 pc off isn't it).
            CatalogHostColorBV = near.Where(n => System.Math.Abs(n.Star.AbsoluteMagnitude - system.HostAbsoluteV) < 1)
                .OrderBy(n => n.D).Select(n => (double?)n.Star.ColorBV).FirstOrDefault(),
        };
    }

    /// <summary>
    /// Inside the self radius, the catalog star nearest the host position is the host itself; the rest
    /// are its companions. Each follows its measured orbit when <see cref="KnownBinaries"/> has one. Otherwise
    /// its parallax is no better than the cross-match slop, so the line-of-sight offset is noise: keep only the
    /// sky-plane (projected) separation, which is also the closest the pair can be, and circle the host face-on
    /// at that distance with the period Kepler's third law gives for the two stars' estimated masses.
    /// </summary>
    private static List<CompanionStar> FindCompanions(ExoplanetSystem system, List<(Star Star, Vector3d Rel, double D)> near)
    {
        var companions = new List<CompanionStar>();
        if (near.Count < 2) return companions;

        var self = near.MinBy(n => n.D);
        var (_, north, east) = Equatorial.SkyPlaneBasis(system.RaDeg, system.DecDeg);
        double selfMass = StellarEstimates.Estimate(self.Star.AbsoluteMagnitude, self.Star.ColorBV).MassSolar;
        foreach (var (s, rel, _) in near)
        {
            if (ReferenceEquals(s, self.Star)) continue;
            double n = Vector3d.Dot(rel, north) * Units.AuPerParsec, e = Vector3d.Dot(rel, east) * Units.AuPerParsec;
            var known = KnownBinaries.VisualOrbit(self.Star.Id, s.Id);
            if (known is null && System.Math.Sqrt(n * n + e * e) < 1e-3) continue; // unresolved duplicate entry, not a companion

            var orbit = known?.Orbit ?? BinaryOrbit.FaceOnCircle(n, e,
                selfMass + StellarEstimates.Estimate(s.AbsoluteMagnitude, s.ColorBV).MassSolar, CatalogEpochDays);
            // Absolute magnitude stays as the catalog derived it: same distance, same luminosity.
            companions.Add(new CompanionStar(s.Id, s.Name, orbit, known is not null, system.RaDeg, system.DecDeg,
                s.AbsoluteMagnitude, s.ColorBV));
        }
        return companions;
    }

    /// <summary>
    /// Catalog stars this close to the host position are taken to be the host itself or its companions (catalog
    /// cross-match slop), not background stars.
    /// </summary>
    public static double SelfRadiusParsecs(ExoplanetSystem system) => System.Math.Max(0.02, system.DistanceParsecs * 0.005);

    /// <summary>When the star catalog's positions were measured (Hipparcos, J1991.25), in days since J2000.0.</summary>
    public static readonly double CatalogEpochDays = BinaryOrbit.DaysSinceJ2000(1991.25);

    /// <summary>Apparent magnitude of the host star seen from the planet.</summary>
    public static double HostApparentMagnitude(ExoplanetSystem system, double distanceAu) =>
        Units.ApparentMagnitude(system.HostAbsoluteV, distanceAu * Units.ParsecsPerAu);

    /// <summary>Angular radius of the host star's disc in radians.</summary>
    public static double HostAngularRadius(ExoplanetSystem system, double distanceAu) =>
        (system.HostRadiusSolar ?? 1.0) * Units.SolarRadiusAu / distanceAu;
}
