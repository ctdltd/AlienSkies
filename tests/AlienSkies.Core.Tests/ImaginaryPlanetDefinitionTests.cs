using System.Text.Json;
using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Catalog;

namespace AlienSkies.Core.Tests;

public class ImaginaryPlanetDefinitionTests
{
    private static readonly IReadOnlyList<Star> Stars = CatalogLoader.LoadStars();

    [Fact]
    public void DisplayName_DefaultsToTheStar()
    {
        Assert.Equal("Vega (imaginary)", new ImaginaryPlanet { HostStar = " Vega " }.DisplayName);
        Assert.Equal("Zorg", new ImaginaryPlanet { HostStar = "Vega", Name = " Zorg " }.DisplayName);
    }

    [Fact]
    public void UnknownStar_GivesNoConfig() =>
        Assert.Null(new ImaginaryPlanet { HostStar = "No Such Star" }.ToConfig(Stars, 30, 0));

    [Fact]
    public void Defaults_AreEarthLikeAroundTheStar()
    {
        var c = new ImaginaryPlanet { HostStar = "Tau Ceti" }.ToConfig(Stars, 12, 34)!;
        var auto = ExoplanetSystem.Imaginary(Star.Find(Stars, "Tau Ceti")!);

        Assert.True(c.Planet.IsImaginary);
        Assert.Equal("Tau Ceti (imaginary)", c.Planet.PlanetName);
        Assert.Equal(auto.SemiMajorAxisAu!.Value, c.SemiMajorAxisAu, 9);
        Assert.Equal(auto.OrbitalPeriodDays!.Value, c.OrbitalPeriodDays, 6);
        Assert.Equal(23.44, c.ObliquityDeg);
        Assert.Equal(24, c.RotationPeriodHours);
        Assert.False(c.TidallyLocked);
        Assert.Equal(12, c.LatitudeDeg);
        Assert.Equal(34, c.LongitudeDeg);
    }

    [Fact]
    public void CustomRadius_GetsAKeplerPeriodFromTheStarsMass()
    {
        var def = new ImaginaryPlanet { HostStar = "Tau Ceti", OrbitalRadiusAu = 2 };
        var c = def.ToConfig(Stars, 0, 0)!;
        Assert.Equal(2, c.SemiMajorAxisAu);
        Assert.Equal(PlanetConfig.KeplerPeriodDays(2, c.Planet.HostMassSolar!.Value), c.OrbitalPeriodDays, 6);
        Assert.Equal(2, c.Planet.SemiMajorAxisAu); // the overlay and the frame agree
    }

    [Fact]
    public void ExplicitValues_AreUsedAsGiven()
    {
        var c = new ImaginaryPlanet
        {
            HostStar = "Vega", OrbitalRadiusAu = 3, OrbitalPeriodDays = 500, Eccentricity = 0.3, InclinationDeg = 10,
            RotationMode = RotationMode.Free, RotationPeriodHours = -40, ObliquityDeg = 80,
        }.ToConfig(Stars, 0, 0)!;

        Assert.Equal(500, c.OrbitalPeriodDays);
        Assert.Equal(0.3, c.Eccentricity);
        Assert.Equal(10, c.InclinationDeg);
        Assert.Equal(-40, c.RotationPeriodHours);
        Assert.Equal(80, c.ObliquityDeg);
        Assert.False(c.TidallyLocked);
    }

    [Theory]
    [InlineData("Proxima Centauri", RotationMode.Auto, true)]   // Earth-like orbit is 0.04 AU: locked
    [InlineData("Tau Ceti", RotationMode.Auto, false)]
    [InlineData("Tau Ceti", RotationMode.TidallyLocked, true)]
    [InlineData("Proxima Centauri", RotationMode.Free, false)]
    public void Rotation_AutoLocksCloseOrbits(string star, RotationMode mode, bool locked) =>
        Assert.Equal(locked, new ImaginaryPlanet { HostStar = star, RotationMode = mode }.ToConfig(Stars, 0, 0)!.TidallyLocked);

    [Fact]
    public void Eccentricity_IsClampedBelowOne() =>
        Assert.True(new ImaginaryPlanet { HostStar = "Vega", Eccentricity = 1.5 }.ToConfig(Stars, 0, 0)!.Eccentricity < 1);

    [Fact]
    public void SurvivesAJsonRoundTrip()
    {
        var def = new ImaginaryPlanet { HostStar = "Vega", Name = "Zorg", OrbitalRadiusAu = 7, RotationMode = RotationMode.Free };
        var back = JsonSerializer.Deserialize<ImaginaryPlanet>(JsonSerializer.Serialize(def))!;
        Assert.Equal("Zorg", back.DisplayName);
        Assert.Equal(7, back.OrbitalRadiusAu);
        Assert.Equal(RotationMode.Free, back.RotationMode);
    }
}
