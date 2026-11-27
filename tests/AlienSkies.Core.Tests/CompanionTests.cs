using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Catalog;
using AlienSkies.Core.Math;

namespace AlienSkies.Core.Tests;

public class CompanionTests
{
    private static ExoplanetSystem Planet(string host) =>
        CatalogLoader.LoadExoplanets().First(p => p.HostName == host);

    private static AlienSky Sky(string host) => AlienSky.Build(Planet(host), CatalogLoader.LoadStars());

    [Fact]
    public void AlphaCenA_HasTolimanAsSecondSun()
    {
        var sky = Sky("alf Cen A");
        var toliman = Assert.Single(sky.Companions);
        Assert.Equal("Toliman", toliman.Name);

        // The sample catalog puts B about 18″ from A at 1.34 pc: ~24 AU projected (the real orbit spans 11–36 AU).
        Assert.InRange(toliman.SeparationAu, 20, 28);
        Assert.InRange(toliman.ApparentMagnitude(toliman.SeparationAu), -19.5, -18.5);
        Assert.InRange(toliman.RadiusSolar, 0.8, 0.9); // measured 0.86 R☉
    }

    [Fact]
    public void Companion_IsNotAlsoAFixedStar()
    {
        var sky = Sky("alf Cen A");
        Assert.DoesNotContain(sky.Stars, s => s.Name is "Toliman" or "Rigil Kentaurus");
        Assert.DoesNotContain(sky.Companions, c => c.Name == "Rigil Kentaurus"); // the host is not its own companion
    }

    [Fact]
    public void Companion_LiesInTheSkyPlane()
    {
        // Matching parallaxes say nothing about depth, so only the projected separation is used.
        var p = Planet("alf Cen A");
        var toliman = Sky("alf Cen A").Companions.Single();
        Assert.Equal(0, Vector3d.Dot(toliman.OffsetAu.Normalized(), p.HostPosition.Normalized()), 9);
    }

    [Fact]
    public void Companion_ShiftsAsThePlanetOrbits()
    {
        var p = Planet("alf Cen A");
        var toliman = Sky("alf Cen A").Companions.Single();
        var frame = new PlanetFrame(PlanetConfig.FromCatalog(p, 30, 0));
        double halfYear = frame.Config.OrbitalPeriodDays / 2;

        var (dir0, d0) = toliman.FromPlanet(frame.PlanetOffsetAu(0));
        var (dir1, d1) = toliman.FromPlanet(frame.PlanetOffsetAu(halfYear));

        // A 1.25 AU orbit against a ~24 AU separation: a few degrees of parallax and a little brightness change.
        double shiftDeg = System.Math.Acos(Vector3d.Dot(dir0, dir1)) / Units.Deg;
        Assert.InRange(shiftDeg, 0.5, 2 * System.Math.Asin(1.25 / toliman.SeparationAu) / Units.Deg + 0.01);
        Assert.True(System.Math.Abs(d1 - d0) <= 2 * 1.25 + 1e-9);
        Assert.Equal(1.0, dir0.Length, 9);
    }

    [Theory]
    [InlineData("TRAPPIST-1")]
    [InlineData("eps Eridani")]
    [InlineData("Proxima Centauri")] // α Cen A and B are ~13,000 AU away: bright stars, not suns
    public void SingleStars_HaveNoCompanions(string host)
    {
        var sky = Sky(host);
        Assert.Empty(sky.Companions);
        if (host == "Proxima Centauri")
            Assert.Contains(sky.Stars, s => s.Name == "Toliman" && s.ApparentMagnitude < -4);
    }

    [Fact]
    public void ApparentMagnitude_FollowsInverseSquare()
    {
        var c = new CompanionStar("x", null, new Vector3d(10, 0, 0), 5.0, 0.8);
        Assert.Equal(5 * System.Math.Log10(2), c.ApparentMagnitude(20) - c.ApparentMagnitude(10), 9);
        Assert.Equal(c.AngularRadius(10) / 2, c.AngularRadius(20), 12);
    }
}
