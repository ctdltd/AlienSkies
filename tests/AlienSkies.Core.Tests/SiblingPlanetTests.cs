using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Catalog;
using AlienSkies.Core.Math;

namespace AlienSkies.Core.Tests;

public class SiblingPlanetTests
{
    private static readonly IReadOnlyList<ExoplanetSystem> Planets = CatalogLoader.LoadExoplanets();

    private static PlanetConfig Config(string name) => PlanetConfig.FromCatalog(Planets.Single(p => p.PlanetName == name));

    private static IReadOnlyList<SiblingPlanet> SiblingsOf(string name) =>
        SiblingPlanet.FindSiblings(Config(name), Planets.Select(p => PlanetConfig.FromCatalog(p)));

    [Fact]
    public void LambertPhase_FullQuarterNew()
    {
        Assert.Equal(1, SiblingPlanet.LambertPhase(0), 12);
        Assert.Equal(1 / System.Math.PI, SiblingPlanet.LambertPhase(System.Math.PI / 2), 12);
        Assert.Equal(0, SiblingPlanet.LambertPhase(System.Math.PI), 12);
    }

    [Theory]
    // Seen from Earth (the Sun is mag -26.74 at 1 AU): Jupiter at opposition and Venus near greatest brilliancy.
    [InlineData(11.21, 0.54, 5.20, 4.20, 0, -2.7)]
    [InlineData(0.950, 0.67, 0.723, 0.43, 118, -4.5)]
    public void ReflectedMagnitude_MatchesSolarSystemPlanets(double radiusEarth, double albedo, double r, double delta, double alphaDeg, double expected)
    {
        double m = SiblingPlanet.ReflectedMagnitude(-26.74, radiusEarth * SiblingPlanet.EarthRadiusAu, albedo, r, delta, alphaDeg * Units.Deg);
        Assert.InRange(m, expected - 0.35, expected + 0.35);
    }

    [Fact]
    public void Trappist1e_SeesTheOtherSixPlanets()
    {
        var siblings = SiblingsOf("TRAPPIST-1 e");
        Assert.Equal(["TRAPPIST-1 b", "TRAPPIST-1 c", "TRAPPIST-1 d", "TRAPPIST-1 f", "TRAPPIST-1 g", "TRAPPIST-1 h"],
                     siblings.Select(s => s.Name).Order());
    }

    [Fact]
    public void SingletonSystem_HasNoSiblings() => Assert.Empty(SiblingsOf("eps Eri b"));

    [Fact]
    public void Proxima_bAndD_SeeEachOther()
    {
        Assert.Equal("Proxima Cen d", Assert.Single(SiblingsOf("Proxima Cen b")).Name);
        Assert.Equal("Proxima Cen b", Assert.Single(SiblingsOf("Proxima Cen d")).Name);
    }

    [Fact]
    public void UnknownInclination_TakesTheViewers()
    {
        // Proxima d has no catalog inclination; it shares b's plane (the 90° default here, or whatever the user set).
        var viewer = Config("Proxima Cen b");
        var tilted = new PlanetConfig { Planet = viewer.Planet, InclinationDeg = 40, AscendingNodeDeg = 25, SemiMajorAxisAu = 0.0486, OrbitalPeriodDays = 11.19 };
        var d = Assert.Single(SiblingPlanet.FindSiblings(tilted, Planets.Select(p => PlanetConfig.FromCatalog(p))));
        Assert.Equal(40, d.Config.InclinationDeg);
        Assert.Equal(25, d.Config.AscendingNodeDeg);
    }

    [Fact]
    public void Trappist1f_FromE_IsBrilliantAndShowsADisc()
    {
        // e and f orbit 0.0293 and 0.0385 AU from the star: at their closest, under 0.01 AU apart (~4 lunar distances).
        var viewer = new PlanetFrame(Config("TRAPPIST-1 e"));
        var f = SiblingsOf("TRAPPIST-1 e").Single(s => s.Name == "TRAPPIST-1 f");
        double hostAt1Au = AlienSky.HostApparentMagnitude(viewer.Config.Planet, 1);

        double brightest = double.MaxValue, widest = 0, nearest = double.MaxValue;
        for (double t = 0; t < 60; t += 0.01)
        {
            var v = f.Observe(t, viewer.PlanetOffsetAu(t), hostAt1Au);
            brightest = System.Math.Min(brightest, v.Magnitude);
            widest = System.Math.Max(widest, 2 * v.AngularRadius / Units.Deg);
            nearest = System.Math.Min(nearest, v.DistanceAu);
            Assert.Equal(1, v.Direction.Length, 9);
        }
        Assert.InRange(nearest, 0.0085, 0.0100);
        Assert.InRange(widest, 0.5, 0.7);   // about the Moon's size from Earth (0.52°)
        Assert.InRange(brightest, -11, -6); // far brighter than Venus, though nowhere near our Moon (-12.7)
    }

    [Fact]
    public void Sibling_WandersAndPhaseFollowsGeometry()
    {
        var viewer = new PlanetFrame(Config("TRAPPIST-1 e"));
        var g = SiblingsOf("TRAPPIST-1 e").Single(s => s.Name == "TRAPPIST-1 g");
        double hostAt1Au = AlienSky.HostApparentMagnitude(viewer.Config.Planet, 1);
        var a = g.Observe(0, viewer.PlanetOffsetAu(0), hostAt1Au);
        var b = g.Observe(1, viewer.PlanetOffsetAu(1), hostAt1Au);
        Assert.True(System.Math.Acos(Vector3d.Dot(a.Direction, b.Direction)) > 1 * Units.Deg);

        // The lit limb faces the star: TowardStar is perpendicular to the line of sight.
        Assert.Equal(0, Vector3d.Dot(a.TowardStar, a.Direction), 9);
        Assert.InRange(a.IlluminatedFraction, 0, 1);
    }

    [Fact]
    public void PhaseOffsets_AreStableAndSpread()
    {
        Assert.Equal(SiblingPlanet.StablePhase("TRAPPIST-1 f"), SiblingPlanet.StablePhase("trappist-1 F"));
        var phases = SiblingsOf("TRAPPIST-1 e").Select(s => s.PhaseOffsetDays / s.Config.OrbitalPeriodDays).ToList();
        Assert.All(phases, p => Assert.InRange(p, 0, 1));
        Assert.True(phases.Distinct().Count() == phases.Count);
    }

    [Fact]
    public void ImaginaryPlanets_AroundOneStar_AreSiblings()
    {
        var stars = CatalogLoader.LoadStars();
        var inner = new ImaginaryPlanet { HostStar = "Tau Ceti", Name = "Inner", OrbitalRadiusAu = 0.4 };
        var outer = new ImaginaryPlanet { HostStar = "tau ceti", Name = "Outer", OrbitalRadiusAu = 1.6 };
        var elsewhere = new ImaginaryPlanet { HostStar = "Vega", Name = "Far" };
        var configs = new[] { inner, outer, elsewhere }.Select(p => p.ToConfig(stars, 0, 0)!).ToList();

        var siblings = SiblingPlanet.FindSiblings(configs[0], configs);
        Assert.Equal("Outer", Assert.Single(siblings).Name);
        Assert.Equal(1.6, siblings[0].Config.SemiMajorAxisAu, 9);
    }
}
