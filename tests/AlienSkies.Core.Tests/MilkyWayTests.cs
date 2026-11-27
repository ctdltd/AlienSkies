using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Catalog;
using AlienSkies.Core.Math;

namespace AlienSkies.Core.Tests;

public class MilkyWayTests
{
    private static double AngleDeg(Vector3d a, Vector3d b) =>
        System.Math.Acos(System.Math.Clamp(Vector3d.Dot(a.Normalized(), b.Normalized()), -1, 1)) / Units.Deg;

    [Fact]
    public void GalacticCentre_AndPole_LandWhereTheyShould()
    {
        // Sgr A* and the north galactic pole (J2000).
        var (l, b) = Galactic.ToLB(Galactic.FromIcrs(Equatorial.ToCartesian(266.41683, -29.00781, 1)));
        Assert.InRange(l < 180 ? l : l - 360, -0.2, 0.2);
        Assert.InRange(b, -0.2, 0.2);

        var (_, bPole) = Galactic.ToLB(Galactic.FromIcrs(Equatorial.ToCartesian(192.85948, 27.12825, 1)));
        Assert.InRange(bPole, 89.99, 90);
    }

    [Fact]
    public void Rotation_RoundTrips()
    {
        var v = new Vector3d(0.3, -0.5, 0.81).Normalized();
        var back = Galactic.ToIcrs(Galactic.FromIcrs(v));
        Assert.True(AngleDeg(v, back) < 1e-6);
        Assert.Equal(1, Galactic.FromIcrs(v).Length, 9);
    }

    [Fact]
    public void FromNearbyStars_TheBandLooksTheSameAsFromEarth()
    {
        // TRAPPIST-1 is 12 pc away: the Milky Way shifts by well under a degree.
        var trappist = CatalogLoader.LoadExoplanets().First(p => p.HostName == "TRAPPIST-1").HostPosition;
        for (double l = 0; l < 360; l += 30)
        {
            var dir = Galactic.ToIcrs(Galactic.FromLB(l, 0));
            Assert.True(AngleDeg(dir, Galactic.EarthDirectionOf(dir, trappist)) < 0.6);
        }
    }

    [Fact]
    public void FromHighAboveThePlane_TheBandDropsBelowTheGalacticEquator()
    {
        // 500 pc toward the north galactic pole with the band 1,500 pc away: the plane appears asin(1/3) ≈ 19.5° low.
        var viewer = Galactic.ToIcrs(new Vector3d(0, 0, 500));
        double bView = -System.Math.Asin(1.0 / 3) / Units.Deg;
        foreach (double l in new[] { 0.0, 90, 200 })
        {
            var earth = Galactic.EarthDirectionOf(Galactic.ToIcrs(Galactic.FromLB(l, bView)), viewer);
            var (_, b) = Galactic.ToLB(Galactic.FromIcrs(earth));
            Assert.InRange(b, -0.01, 0.01);
        }
    }

    [Fact]
    public void FarViewers_AreClamped()
    {
        var dir = Galactic.ToIcrs(Galactic.FromLB(45, 10));
        var far = Galactic.ToIcrs(new Vector3d(0, 0, 8000));
        var atLimit = Galactic.ToIcrs(new Vector3d(0, 0, Galactic.MaxViewerOffsetParsecs));
        Assert.True(AngleDeg(Galactic.EarthDirectionOf(dir, far), Galactic.EarthDirectionOf(dir, atLimit)) < 1e-9);
    }

    [Fact]
    public void Model_IsBrightestTowardTheCentre_AndDarkOffTheBand()
    {
        double centre = MilkyWayModel.Sample(3, -4).Intensity;
        double anticentre = MilkyWayModel.Sample(180, 0).Intensity;
        Assert.True(centre > 0.5, $"centre {centre}");
        Assert.True(centre > 2.5 * anticentre, $"centre {centre} vs anticentre {anticentre}");
        Assert.True(MilkyWayModel.Sample(3, -4).Warmth > 0.6); // the bulge is yellowish

        Assert.True(MilkyWayModel.Sample(90, 60).Intensity < 0.03);
        Assert.True(MilkyWayModel.Sample(200, -75).Intensity < 0.03);
    }

    [Fact]
    public void Model_HasTheCoalsackAndTheMagellanicClouds()
    {
        double coalsack = MilkyWayModel.Sample(301, -1).Intensity;
        double crux = (MilkyWayModel.Sample(294, -1).Intensity + MilkyWayModel.Sample(308, -1).Intensity) / 2;
        Assert.True(coalsack < 0.6 * crux, $"coalsack {coalsack} vs neighbours {crux}");

        double lmc = MilkyWayModel.Sample(280.5, -32.9).Intensity;
        Assert.True(lmc > 0.15 && lmc > 4 * MilkyWayModel.Sample(255, -32.9).Intensity, $"LMC {lmc}");
        Assert.True(MilkyWayModel.Sample(302.8, -44.3).Intensity > 0.08);
    }

    [Fact]
    public void Model_HasNoSeamAtLongitudeZero()
    {
        for (double b = -30; b <= 30; b += 7.5)
            Assert.Equal(MilkyWayModel.Sample(359.9999, b).Intensity, MilkyWayModel.Sample(0.0001, b).Intensity, 3);
    }
}
