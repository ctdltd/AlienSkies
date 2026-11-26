using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Catalog;

namespace AlienSkies.Core.Tests;

public class ImaginaryPlanetTests
{
    private static readonly IReadOnlyList<Star> Stars = CatalogLoader.LoadStars();

    private static Star Named(string name) => Star.Find(Stars, name) ?? throw new Xunit.Sdk.XunitException($"{name} not in catalog");

    [Fact]
    public void SunLikeColour_GivesSunLikeTemperatureAndLuminosity()
    {
        Assert.InRange(StellarEstimates.TemperatureFromBV(0.65), 5700, 5850);
        Assert.InRange(StellarEstimates.BolometricCorrection(5772), -0.15, 0.0);
        Assert.InRange(StellarEstimates.LuminositySolar(Units.SunAbsoluteV, 0.65), 0.9, 1.1);
        Assert.InRange(StellarEstimates.MassSolar(1.0), 0.99, 1.01);
        Assert.InRange(StellarEstimates.RadiusSolar(1.0, 5772), 0.99, 1.01);
    }

    [Theory]
    [InlineData(15.53, 0.12)] // Proxima Centauri
    [InlineData(13.21, 0.16)] // Barnard's Star
    [InlineData(10.46, 0.39)] // Lalande 21185
    public void RedDwarfMass_MatchesMeasured(double absoluteV, double massSolar) =>
        Assert.InRange(StellarEstimates.RedDwarfMassSolar(absoluteV), massSolar * 0.75, massSolar * 1.3);

    [Theory]
    [InlineData("Proxima centauri")] // case-insensitive name
    [InlineData("70890")]            // Hipparcos number
    [InlineData("HIP 70890")]
    public void Find_MatchesNameOrHipNumber(string query) =>
        Assert.Equal("Proxima Centauri", Star.Find(Stars, query)?.Name);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("No Such Star")]
    public void Find_ReturnsNullForUnknown(string? query) => Assert.Null(Star.Find(Stars, query));

    [Fact]
    public void Imaginary_KeepsTheHostWhereItIs()
    {
        var vega = Named("Vega");
        var planet = ExoplanetSystem.Imaginary(vega);

        Assert.True(planet.IsImaginary);
        Assert.Equal("Vega (imaginary)", planet.PlanetName);
        Assert.Equal("Vega", planet.HostName);
        Assert.True((planet.HostPosition - vega.Position).Length < 1e-9);
        Assert.Equal(vega.AbsoluteMagnitude, planet.HostAbsoluteV, 6);
        Assert.Equal(vega.ColorBV, planet.HostColorBV);
    }

    [Fact]
    public void Imaginary_UsesTheGivenName() =>
        Assert.Equal("Zorg", ExoplanetSystem.Imaginary(Named("Vega"), "  Zorg ").PlanetName);

    [Fact]
    public void Imaginary_UnnamedStar_IsCalledByHipNumber()
    {
        var unnamed = Stars.First(s => s.Name is null);
        Assert.Equal($"HIP {unnamed.Id} (imaginary)", ExoplanetSystem.Imaginary(unnamed).PlanetName);
    }

    [Fact]
    public void Imaginary_AroundProxima_LandsNearItsRealHabitableZone()
    {
        // Proxima b, which sits in the habitable zone, orbits at 0.0486 AU every 11.2 days; Proxima is 0.12 M☉, 0.15 R☉.
        var planet = ExoplanetSystem.Imaginary(Named("Proxima Centauri"));
        // Proxima b gets ≈0.65× Earth's sunlight, so the Earth-equivalent orbit is a little inside it.
        Assert.InRange(planet.SemiMajorAxisAu!.Value, 0.03, 0.05);
        Assert.InRange(planet.HostMassSolar!.Value, 0.09, 0.16);
        Assert.InRange(planet.HostRadiusSolar!.Value, 0.1, 0.2);
        Assert.InRange(planet.OrbitalPeriodDays!.Value, 5, 12);

        // So close in that it should come out tidally locked by default.
        Assert.True(PlanetConfig.FromCatalog(planet).TidallyLocked);
    }

    [Fact]
    public void Imaginary_AroundTauCeti_IsAnEarthLikeOrbit()
    {
        // Tau Ceti: G8 V, ≈0.5 L☉, 0.78 M☉.
        var planet = ExoplanetSystem.Imaginary(Named("Tau Ceti"));
        Assert.InRange(planet.SemiMajorAxisAu!.Value, 0.6, 0.8);
        Assert.InRange(planet.OrbitalPeriodDays!.Value, 200, 350);
        Assert.False(PlanetConfig.FromCatalog(planet).TidallyLocked);
    }

    [Fact]
    public void Imaginary_AroundAGiant_IsFarOutAndTheStarLooksBig()
    {
        // Arcturus: ≈170 L☉, ≈25 R☉ — an Earth-equivalent orbit is ≈13 AU out.
        var planet = ExoplanetSystem.Imaginary(Named("Arcturus"));
        Assert.InRange(planet.SemiMajorAxisAu!.Value, 9, 16);
        Assert.InRange(planet.HostRadiusSolar!.Value, 15, 35);
    }

    [Fact]
    public void Imaginary_SkyHidesTheHostAndShowsTheSun()
    {
        var planet = ExoplanetSystem.Imaginary(Named("Tau Ceti"));
        var sky = AlienSky.Build(planet, Stars);

        Assert.DoesNotContain(sky.Stars, s => s.Name == "Tau Ceti");
        var sol = sky.Stars.Single(s => s.IsSol);
        Assert.Equal(planet.DistanceParsecs, sol.DistanceParsecs, 6);
    }
}
