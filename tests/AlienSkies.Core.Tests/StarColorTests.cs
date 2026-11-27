using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Catalog;
using AlienSkies.Core.Math;

namespace AlienSkies.Core.Tests;

public class StarColorTests
{
    private static (int R, int G, int B) Rgb(double bv, double boost = 1)
    {
        var (r, g, b) = StarColor.FromBV(bv, boost);
        return ((int)System.Math.Round(r * 255), (int)System.Math.Round(g * 255), (int)System.Math.Round(b * 255));
    }

    [Fact]
    public void TrueColours_MatchBlackBodies()
    {
        // Reference: black-body sRGB, D65 white (Charity's table): the Sun is nearly white, Vega pale blue, M dwarfs pale orange.
        var sun = Rgb(0.65);
        Assert.Equal(255, sun.R);
        Assert.InRange(sun.G, 235, 247);
        Assert.InRange(sun.B, 225, 242);

        var vega = Rgb(0.0);
        Assert.True(vega.B == 255 && vega.R < 215 && vega.G < 225);

        var mDwarf = Rgb(1.85);
        Assert.Equal(255, mDwarf.R);
        Assert.InRange(mDwarf.G, 185, 205);
        Assert.InRange(mDwarf.B, 115, 145);
    }

    [Fact]
    public void Colour_IsSmooth_AndRedderWithBV()
    {
        // No jumps (the old ramp jumped 15% in red at B−V 0.0 and 5% in green at 0.4), and blue never rises with B−V.
        var prev = StarColor.FromBV(-0.4);
        for (double bv = -0.39; bv <= 2.0; bv += 0.01)
        {
            var c = StarColor.FromBV(bv);
            Assert.True(System.Math.Abs(c.R - prev.R) < 0.02 && System.Math.Abs(c.G - prev.G) < 0.02 && System.Math.Abs(c.B - prev.B) < 0.03, $"jump at {bv:0.00}");
            Assert.True(c.B <= prev.B + 1e-9, $"bluer at {bv:0.00}");
            prev = c;
        }
    }

    [Fact]
    public void Boost_ScalesSaturation()
    {
        var white = StarColor.FromBV(1.5, boost: 0);
        Assert.Equal(white.R, white.G, 9);
        Assert.Equal(white.G, white.B, 9);

        // A K star gets more orange with boost, keeping full brightness in its strongest channel.
        var k1 = Rgb(1.0, 1);
        var k2 = Rgb(1.0, 2);
        Assert.Equal(255, k2.R);
        Assert.True(k2.B < k1.B - 20);
        Assert.True(Rgb(1.0, 1.5).B is var mid && mid < k1.B && mid > k2.B);

        // Out-of-range and missing values don't throw.
        _ = StarColor.FromBV(double.NaN, 1.5);
        _ = StarColor.FromBV(9, 9);
    }

    [Fact]
    public void ArchiveHost_TakesItsColour_FromTheMatchingCatalogStar()
    {
        // An orange K giant host (B−V 1.2, M_V 0.5): the brightness estimate alone would call it blue-white.
        var hostPos = Equatorial.ToCartesian(100, 20, 60);
        var catalog = new[] { new Star("1", "Giant", hostPos, 0.5, 1.2) };
        var system = new ExoplanetSystem("Giant b", "Giant", 100, 20, 60, Units.ApparentMagnitude(0.5, 60),
            10, 1.5, 1, 365, 90, 0);

        Assert.True(system.HostColorBVOrEstimate < 0.4);
        Assert.Equal(1.2, AlienSky.Build(system, catalog, includeSol: false).HostColorBV, 9);

        // A catalog star much brighter or fainter than the archive's host isn't taken for it.
        var other = new[] { new Star("2", "Other", hostPos + new Vector3d(0.1, 0, 0), 5.5, 1.2) };
        Assert.Equal(system.HostColorBVOrEstimate, AlienSky.Build(system, other, includeSol: false).HostColorBV, 9);
    }

    [Fact]
    public void DistantHosts_AreFlaggedAsSparse()
    {
        ExoplanetSystem At(double pc) => new("x b", "x", 0, 0, pc, 10, 1, 1, 1, 365, 90, 0);
        Assert.False(AlienSky.Build(At(48), [], includeSol: false).IsSparse);
        Assert.True(AlienSky.Build(At(550), [], includeSol: false).IsSparse);
    }
}
