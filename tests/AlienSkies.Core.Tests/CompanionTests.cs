using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Catalog;
using AlienSkies.Core.Math;

namespace AlienSkies.Core.Tests;

public class CompanionTests
{
    private const double AlfCenParallaxArcsec = 0.7471;

    private static ExoplanetSystem Planet(string host) =>
        CatalogLoader.LoadExoplanets().Single(p => p.HostName == host);

    private static AlienSky Sky(string host) => AlienSky.Build(Planet(host), CatalogLoader.LoadStars());

    private static (double North, double East) SkyPlane(Vector3d offset, ExoplanetSystem p)
    {
        var (_, north, east) = Equatorial.SkyPlaneBasis(p.RaDeg, p.DecDeg);
        return (Vector3d.Dot(offset, north), Vector3d.Dot(offset, east));
    }

    private static double Year(double y) => BinaryOrbit.DaysSinceJ2000(y);

    [Fact]
    public void AlphaCenA_HasTolimanAsSecondSun()
    {
        var sky = Sky("alf Cen A");
        var toliman = Assert.Single(sky.Companions);
        Assert.Equal("Toliman", toliman.Name);
        Assert.True(toliman.OrbitIsMeasured);

        // The real orbit runs from 11.2 AU (periastron) to 36 AU (apastron); it was near apastron when Hipparcos looked.
        double sep = toliman.SeparationAu(AlienSky.CatalogEpochDays);
        Assert.InRange(sep, 30, 36.1);
        Assert.InRange(toliman.ApparentMagnitude(sep), -19, -17.5);
        Assert.InRange(toliman.RadiusSolar, 0.8, 0.9); // measured 0.86 R☉
    }

    [Fact]
    public void AlphaCenOrbit_PutsTolimanWhereHipparcosSawIt()
    {
        // The sample star catalog has B 17.8″ from A at position angle 216° (J1991.25). The orbit must agree.
        var p = Planet("alf Cen A");
        var stars = CatalogLoader.LoadStars();
        var a = stars.Single(s => s.Id == "71683");
        var b = stars.Single(s => s.Id == "71681");
        var (cn, ce) = SkyPlane((b.Position - a.Position) * Units.AuPerParsec, p);

        var toliman = Sky("alf Cen A").Companions.Single();
        var (on, oe) = SkyPlane(toliman.OffsetAu(AlienSky.CatalogEpochDays), p);

        double catalogSep = System.Math.Sqrt(cn * cn + ce * ce), orbitSep = System.Math.Sqrt(on * on + oe * oe);
        Assert.InRange(orbitSep / catalogSep, 0.9, 1.1);
        double paCatalog = System.Math.Atan2(ce, cn) / Units.Deg, paOrbit = System.Math.Atan2(oe, on) / Units.Deg;
        Assert.InRange(System.Math.Abs(Units.NormalizeRadians((paOrbit - paCatalog) * Units.Deg + System.Math.PI) - System.Math.PI) / Units.Deg, 0, 6);
    }

    [Fact]
    public void AlphaCenOrbit_ClosesIn_AsObserved()
    {
        var p = Planet("alf Cen A");
        var toliman = Sky("alf Cen A").Companions.Single();

        // From Earth the pair closed to about 4″ in 2016 and are widening again.
        var (n, e) = SkyPlane(toliman.OffsetAu(Year(2016)), p);
        Assert.InRange(System.Math.Sqrt(n * n + e * e) * AlfCenParallaxArcsec, 3, 5.5);

        // Periastron in 2035: the closest the two stars ever get.
        Assert.InRange(toliman.SeparationAu(Year(2035.48)), 11.0, 11.5);
        Assert.InRange(toliman.SeparationAu(Year(1995.5)), 35.5, 36.2);

        // And round again 79.9 years later.
        var now = toliman.OffsetAu(Year(2026));
        var later = toliman.OffsetAu(Year(2026 + 79.91));
        Assert.True((now - later).Length < 1e-6);
    }

    [Fact]
    public void AlphaCenOrbit_ObeysKeplersThirdLaw()
    {
        var (orbit, mass) = KnownBinaries.VisualOrbit("71683", "71681")!.Value;
        double years = orbit.PeriodDays / BinaryOrbit.DaysPerJulianYear;
        double impliedMass = System.Math.Pow(orbit.SemiMajorAxisAu, 3) / (years * years);
        Assert.InRange(impliedMass / mass, 0.95, 1.05);
    }

    [Fact]
    public void VisualOrbit_WorksEitherWayRound()
    {
        var ab = KnownBinaries.VisualOrbit("71683", "71681")!.Value.Orbit;
        var ba = KnownBinaries.VisualOrbit("71681", "71683")!.Value.Orbit;
        var (los, n, e) = Equatorial.SkyPlaneBasis(219.9, -60.8);
        for (double t = -20_000; t < 20_000; t += 3_333)
            Assert.True((ab.OffsetAu(t, los, n, e) + ba.OffsetAu(t, los, n, e)).Length < 1e-9);
    }

    [Fact]
    public void Toliman_SweepsAcrossTheSky_OverDecades()
    {
        var p = Planet("alf Cen A");
        var toliman = Sky("alf Cen A").Companions.Single();
        var frame = new PlanetFrame(PlanetConfig.FromCatalog(p, 30, 0));
        var (d0, _) = toliman.FromPlanet(frame.PlanetOffsetAu(Year(2000)), Year(2000));
        var (d1, _) = toliman.FromPlanet(frame.PlanetOffsetAu(Year(2035.48)), Year(2035.48));
        double moved = System.Math.Acos(Vector3d.Dot(d0, d1)) / Units.Deg;
        Assert.InRange(moved, 60, 180); // a fixed companion would move only a few degrees of parallax
    }

    [Fact]
    public void Companion_ShiftsAsThePlanetOrbits()
    {
        var p = Planet("alf Cen A");
        var toliman = Sky("alf Cen A").Companions.Single();
        var frame = new PlanetFrame(PlanetConfig.FromCatalog(p, 30, 0));
        double halfYear = frame.Config.OrbitalPeriodDays / 2;
        double sep = toliman.SeparationAu(0);

        // Toliman itself moves under 0.1° in half a year; the rest is parallax from a 1.25 AU orbit.
        var (dir0, d0) = toliman.FromPlanet(frame.PlanetOffsetAu(0), 0);
        var (dir1, d1) = toliman.FromPlanet(frame.PlanetOffsetAu(halfYear), halfYear);

        double shiftDeg = System.Math.Acos(Vector3d.Dot(dir0, dir1)) / Units.Deg;
        Assert.InRange(shiftDeg, 0.5, 2 * System.Math.Asin(1.25 / sep) / Units.Deg + 1.0);
        Assert.True(System.Math.Abs(d1 - d0) <= 2 * 1.25 + 0.1);
        Assert.Equal(1.0, dir0.Length, 9);
    }

    [Fact]
    public void UnmeasuredCompanion_CirclesFaceOn_AtTheCatalogSeparation()
    {
        // Two Sun-like stars 20.6 AU apart on the sky at 10 pc, with no published orbit.
        var hostPos = Equatorial.ToCartesian(100, 20, 10);
        var (_, north, east) = Equatorial.SkyPlaneBasis(100, 20);
        var catalog = new[]
        {
            new Star("A", "Primus", hostPos, Units.SunAbsoluteV, 0.65),
            new Star("B", "Secundus", hostPos + east * 1e-4, Units.SunAbsoluteV, 0.65),
        };
        var system = new ExoplanetSystem("Primus b", "Primus", 100, 20, 10, Units.ApparentMagnitude(Units.SunAbsoluteV, 10),
            1, 1, 1, 365.25, 90, 0);

        var sky = AlienSky.Build(system, catalog, includeSol: false);
        var c = Assert.Single(sky.Companions);
        Assert.False(c.OrbitIsMeasured);
        Assert.Empty(sky.Stars);

        double expected = 1e-4 * Units.AuPerParsec;
        var los = system.HostPosition.Normalized();
        for (double t = -5000; t < 5000; t += 777)
        {
            Assert.Equal(expected, c.SeparationAu(t), 6);
            Assert.Equal(0, Vector3d.Dot(c.OffsetAu(t), los), 6);
        }
        // At the catalog epoch it is exactly where the catalog puts it, and two Suns 20.6 AU apart take ~66 years.
        Assert.True((c.OffsetAu(AlienSky.CatalogEpochDays) - east * expected).Length < 1e-6);
        Assert.InRange(c.Orbit.PeriodDays / 365.25, 60, 75);
    }

    [Theory]
    [InlineData("TRAPPIST-1")]
    [InlineData("eps Eridani")]
    [InlineData("Proxima Centauri")] // α Cen A and B are ~13,000 AU away: bright stars, not suns
    public void SingleStars_HaveNoCompanions(string host)
    {
        var sky = Sky(host);
        Assert.Empty(sky.Companions);
        Assert.Null(sky.Circumbinary);
        Assert.Equal(Vector3d.Zero, sky.HostOffsetAu(1234));
        if (host == "Proxima Centauri")
            Assert.Contains(sky.Stars, s => s.Name == "Toliman" && s.ApparentMagnitude < -4);
    }

    [Fact]
    public void SingleStar_SunsFromPlanet_IsJustTheHost()
    {
        var p = Planet("TRAPPIST-1");
        var sky = Sky("TRAPPIST-1");
        var frame = new PlanetFrame(PlanetConfig.FromCatalog(p));
        var sun = Assert.Single(sky.SunsFromPlanet(frame.PlanetOffsetAu(1.5), 1.5));
        var (dir, dist) = frame.HostStarFromPlanet(1.5);
        Assert.True(sun.IsHost);
        Assert.Equal(1, sun.VisibleFraction);
        Assert.True((sun.Direction - dir).Length < 1e-12);
        Assert.Equal(AlienSky.HostApparentMagnitude(p, dist), sun.ApparentMagnitude, 9);
        Assert.Equal(AlienSky.HostAngularRadius(p, dist), sun.AngularRadius, 12);
    }

    [Fact]
    public void ApparentMagnitude_FollowsInverseSquare()
    {
        var orbit = BinaryOrbit.FaceOnCircle(10, 0, 2, 0);
        var c = new CompanionStar("x", null, orbit, false, 0, 0, 5.0, 0.8);
        Assert.Equal(5 * System.Math.Log10(2), c.ApparentMagnitude(20) - c.ApparentMagnitude(10), 9);
        Assert.Equal(c.AngularRadius(10) / 2, c.AngularRadius(20), 12);
    }
}
