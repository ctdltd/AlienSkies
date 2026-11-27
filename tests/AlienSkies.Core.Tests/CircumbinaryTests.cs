using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Catalog;
using AlienSkies.Core.Math;

namespace AlienSkies.Core.Tests;

public class CircumbinaryTests
{
    private static ExoplanetSystem Planet(string name) =>
        CatalogLoader.LoadExoplanets().Single(p => p.PlanetName == name);

    private static AlienSky Sky(ExoplanetSystem p) => AlienSky.Build(p, CatalogLoader.LoadStars());

    [Theory]
    [InlineData("Kepler-16 b", "Kepler-16 A", "Kepler-16 B")]
    [InlineData("Kepler-47 c", "Kepler-47 A", "Kepler-47 B")]
    [InlineData("TOI-1338 b", "TOI-1338 A", "TOI-1338 B")]
    public void CircumbinaryPlanets_HaveTwoSuns(string planet, string primary, string secondary)
    {
        var p = Planet(planet);
        var sky = Sky(p);
        Assert.NotNull(sky.Circumbinary);
        Assert.Equal(primary, sky.HostName);
        var b = Assert.Single(sky.Companions);
        Assert.Equal(secondary, b.Name);

        var frame = new PlanetFrame(PlanetConfig.FromCatalog(p));
        var suns = sky.SunsFromPlanet(frame.PlanetOffsetAu(10), 10);
        Assert.Equal(2, suns.Count);
        Assert.True(suns[0].IsHost);
        Assert.True(suns[0].ApparentMagnitude < suns[1].ApparentMagnitude); // the primary is the brighter
    }

    [Fact]
    public void HostNames_MatchWithOrWithoutAB()
    {
        Assert.NotNull(KnownBinaries.CircumbinaryFor("Kepler-16"));
        Assert.NotNull(KnownBinaries.CircumbinaryFor("kepler-16 AB"));
        Assert.Null(KnownBinaries.CircumbinaryFor("Kepler-160"));
        Assert.Null(KnownBinaries.CircumbinaryFor("alf Cen A"));
    }

    [Fact]
    public void Kepler16_StarsCircleTheirBarycentre()
    {
        var sky = Sky(Planet("Kepler-16 b"));
        var cb = sky.Circumbinary!;
        var b = sky.Companions.Single();
        double min = double.MaxValue, max = 0;
        for (double t = 0; t < cb.Orbit.PeriodDays; t += 0.25)
        {
            var a = sky.HostOffsetAu(t);
            var bo = b.OffsetAu(t);
            // Centre of mass stays put at the origin of the planet's orbit.
            Assert.True((a * cb.Primary.MassSolar + bo * cb.Secondary.MassSolar).Length < 1e-12);
            double sep = (bo - a).Length;
            Assert.Equal(sep, b.SeparationAu(t), 12);
            (min, max) = (System.Math.Min(min, sep), System.Math.Max(max, sep));
        }
        Assert.InRange(min, 0.1885, 0.1895); // a(1 − e)
        Assert.InRange(max, 0.2595, 0.2605); // a(1 + e)
        // Back where it started one 41-day period later.
        Assert.True((b.OffsetAu(3) - b.OffsetAu(3 + cb.Orbit.PeriodDays)).Length < 1e-9);
    }

    [Fact]
    public void Kepler16b_OrbitsBothStarsTogether()
    {
        // Kepler's third law with 0.892 M☉ (both stars) gives the measured 228.8-day period; 0.69 M☉ (A alone) would not.
        var p = Planet("Kepler-16 b");
        Assert.InRange(p.CentralMassSolar, 0.89, 0.895);
        Assert.InRange(PlanetConfig.KeplerPeriodDays(p.SemiMajorAxisAu!.Value, p.CentralMassSolar), 226, 232);
        Assert.InRange(PlanetConfig.KeplerPeriodDays(p.SemiMajorAxisAu!.Value, p.HostMassSolar!.Value), 255, 265);

        // With the period missing, FromCatalog fills it in from both stars.
        var noPeriod = p with { OrbitalPeriodDays = null };
        Assert.InRange(PlanetConfig.FromCatalog(noPeriod).OrbitalPeriodDays, 226, 232);
    }

    [Fact]
    public void Kepler16_LightSplit_AddsUpToTheSystemMagnitude()
    {
        var p = Planet("Kepler-16 b");
        var sky = Sky(p);
        double Flux(double m) => System.Math.Pow(10, -0.4 * m);
        double total = Flux(sky.HostAbsoluteV) + Flux(sky.Companions.Single().AbsoluteMagnitude);
        Assert.Equal(Flux(p.HostAbsoluteV), total, 9);
    }

    [Fact]
    public void Kepler16b_SeesAnOrangeDaySunAndARedSecondSun()
    {
        var p = Planet("Kepler-16 b");
        var sky = Sky(p);
        var frame = new PlanetFrame(PlanetConfig.FromCatalog(p));
        double widest = 0;
        for (double t = 0; t < 229; t += 0.5)
        {
            var suns = sky.SunsFromPlanet(frame.PlanetOffsetAu(t), t);
            if (suns[0].VisibleFraction < 1 || suns[1].VisibleFraction < 1) continue;
            Assert.InRange(suns[0].ApparentMagnitude, -25.6, -24.6); // about a fifth of our Sun's light
            Assert.InRange(suns[1].ApparentMagnitude, -20.3, -18.8); // like Toliman from α Cen A
            Assert.InRange(suns[0].AngularRadius / Units.Deg, 0.2, 0.32);
            widest = System.Math.Max(widest, System.Math.Acos(Vector3d.Dot(suns[0].Direction, suns[1].Direction)) / Units.Deg);
        }
        // The pair wheel up to about 20° apart and back every few weeks.
        Assert.InRange(widest, 15, 26);
    }

    [Fact]
    public void Kepler16b_SeesTheSunsEclipseEachOther()
    {
        // The planet's orbit is within half a degree of the stars' plane, so at every conjunction one passes in front.
        var p = Planet("Kepler-16 b");
        var sky = Sky(p);
        var frame = new PlanetFrame(PlanetConfig.FromCatalog(p));
        double minA = 1, minB = 1;
        for (double t = 0; t < 229; t += 0.01)
        {
            var suns = sky.SunsFromPlanet(frame.PlanetOffsetAu(t), t);
            minA = System.Math.Min(minA, suns[0].VisibleFraction);
            minB = System.Math.Min(minB, suns[1].VisibleFraction);
        }
        Assert.InRange(minA, 0.65, 0.95); // the small red dwarf, nearer to us than its partner, transits the orange star
        Assert.InRange(minB, 0, 0.01);    // the orange star hides the red dwarf completely
    }

    [Fact]
    public void Eclipse_DimsTheFartherSun()
    {
        var p = Planet("Kepler-16 b");
        var sky = Sky(p);
        var frame = new PlanetFrame(PlanetConfig.FromCatalog(p));
        for (double t = 0; t < 229; t += 0.01)
        {
            var suns = sky.SunsFromPlanet(frame.PlanetOffsetAu(t), t);
            foreach (var s in suns.Where(s => s.VisibleFraction < 1))
            {
                var other = suns.Single(o => o.IsHost != s.IsHost);
                Assert.True(other.DistanceAu < s.DistanceAu); // only a nearer sun can cover a farther one
                Assert.Equal(1, other.VisibleFraction);
            }
        }
    }

    [Theory]
    [InlineData(1.0, 0.5, 2.0, 0.0)]  // apart
    [InlineData(1.0, 2.0, 0.5, 1.0)]  // covered by a bigger disc
    [InlineData(1.0, 0.5, 0.2, 0.25)] // small disc inside: (0.5/1)²
    [InlineData(1.0, 1.0, 0.0, 1.0)]  // identical discs, centred
    public void CoveredFraction_LimitingCases(double r, double occulter, double sep, double expected) =>
        Assert.Equal(expected, AlienSky.CoveredFraction(r, occulter, sep), 9);

    [Fact]
    public void CoveredFraction_HalfOverlapOfEqualDiscs()
    {
        // Two unit discs one radius apart overlap by (2π/3 − √3/2) / π ≈ 39.1%.
        Assert.Equal((2 * System.Math.PI / 3 - System.Math.Sqrt(3) / 2) / System.Math.PI, AlienSky.CoveredFraction(1, 1, 1), 9);
        double prev = 1;
        for (double s = 0; s <= 2; s += 0.05)
        {
            double f = AlienSky.CoveredFraction(1, 0.6, s);
            Assert.True(f <= prev + 1e-12); // shrinks steadily as the discs slide apart
            prev = f;
        }
    }
}
