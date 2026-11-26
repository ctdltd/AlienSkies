using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Catalog;
using AlienSkies.Core.Math;

namespace AlienSkies.Core.Tests;

public class AstronomyTests
{
    private static ExoplanetSystem AlphaCenA() =>
        CatalogLoader.LoadExoplanets().Single(p => p.HostName == "alf Cen A");

    [Theory]
    [InlineData(0, 0, 10)]
    [InlineData(101.287, -16.716, 2.637)]
    [InlineData(359.9, 89.9, 1000)]
    public void RaDec_RoundTrips(double ra, double dec, double d)
    {
        var (ra2, dec2, d2) = Equatorial.FromCartesian(Equatorial.ToCartesian(ra, dec, d));
        Assert.Equal(ra, ra2, 6);
        Assert.Equal(dec, dec2, 6);
        Assert.Equal(d, d2, 6);
    }

    [Fact]
    public void Kepler_OneAuAroundSun_IsOneYear() =>
        Assert.Equal(365.256, PlanetConfig.KeplerPeriodDays(1.0, 1.0), 2);

    [Fact]
    public void Sol_FromAlphaCentauri_IsZeroMagStarInCassiopeia()
    {
        var sky = AlienSky.Build(AlphaCenA(), CatalogLoader.LoadStars());
        var sol = sky.Stars.Single(s => s.IsSol);

        Assert.InRange(sol.ApparentMagnitude, 0.3, 0.6);       // ≈0.46
        var (ra, dec, _) = Equatorial.FromCartesian(sol.Direction);
        Assert.Equal(39.9, ra, 0);                              // antipode of α Cen's RA 219.9
        Assert.Equal(60.8, dec, 0);
    }

    [Fact]
    public void HostStar_IsRemovedFromItsOwnSky()
    {
        var sky = AlienSky.Build(AlphaCenA(), CatalogLoader.LoadStars());
        Assert.DoesNotContain(sky.Stars, s => s.Name == "Rigil Kentaurus");
        Assert.DoesNotContain(sky.Companions, s => s.Name == "Rigil Kentaurus");
        Assert.Contains(sky.Stars, s => s.Name == "Sirius");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.7)]
    [InlineData(4.4)]
    public void TidallyLocked_SubstellarPoint_HasSunOverhead(double tDays)
    {
        var trappist = CatalogLoader.LoadExoplanets().Single(p => p.PlanetName == "TRAPPIST-1 e");
        var cfg = PlanetConfig.FromCatalog(trappist, latitudeDeg: 0, longitudeDeg: 0);
        Assert.True(cfg.TidallyLocked);

        var frame = new PlanetFrame(cfg);
        var (dir, _) = frame.HostStarFromPlanet(tDays);
        var (alt, _) = frame.Horizon(tDays).ToHorizontal(dir);
        // Not exactly 90°: TRAPPIST-1 e's small eccentricity makes the star librate ~0.6° around the zenith.
        Assert.InRange(alt / Units.Deg, 89.0, 90.0);

        var farSide = new PlanetFrame(PlanetConfig.FromCatalog(trappist, 0, 180));
        Assert.InRange(farSide.Horizon(tDays).ToHorizontal(dir).Altitude / Units.Deg, -90.0, -89.0);
    }

    [Fact]
    public void NorthPole_ZenithIsSpinAxis()
    {
        var cfg = new PlanetConfig { Planet = AlphaCenA(), LatitudeDeg = 90, ObliquityDeg = 23.44 };
        var frame = new PlanetFrame(cfg);
        var z = frame.Horizon(123.4).Zenith;
        Assert.Equal(1.0, Vector3d.Dot(z, frame.SpinPole), 9);
    }

    [Fact]
    public void FaceOnOrbit_NormalPointsAtEarth()
    {
        var cfg = new PlanetConfig { Planet = AlphaCenA(), InclinationDeg = 0 };
        var frame = new PlanetFrame(cfg);
        var towardEarth = (-AlphaCenA().HostPosition).Normalized();
        Assert.Equal(1.0, Vector3d.Dot(frame.OrbitNormal, towardEarth), 9);
    }
}
