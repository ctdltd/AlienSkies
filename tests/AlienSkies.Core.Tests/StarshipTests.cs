using AlienSkies.Rendering;
using SkiaSharp;

namespace AlienSkies.Core.Tests;

public class StarshipTests
{
    [Theory]
    [InlineData(180, 25, 100)]
    [InlineData(0, 5, 160)]
    [InlineData(90, 80, 40)]
    public void Pass_StaysAboveTheHorizon_AndEndsInWarp(double az, double alt, double fov)
    {
        var s = new SaverSettings { ViewAzimuthDeg = az, ViewAltitudeDeg = alt, FieldOfViewDeg = fov };
        var rng = new Random(7);
        for (int i = 0; i < 200; i++)
        {
            var ship = StarshipFlyby.Random(rng, s, TimeSpan.Zero);
            Assert.InRange(ship.CruiseSeconds, 3, 30);
            for (ship.Age = 0; ship.Age <= ship.CruiseSeconds; ship.Age += 0.5)
                Assert.True(ship.Position().Z > 0, "the ship dipped below the horizon");
            ship.Age = ship.DurationSeconds;
            Assert.True(ship.IsOver);
        }
    }

    [Fact]
    public void NextPass_Is10To20MinutesAway()
    {
        var rng = new Random(3);
        for (int i = 0; i < 1000; i++)
            Assert.InRange(StarshipFlyby.NextInterval(rng).TotalMinutes, 10, 20);
    }

    [Fact]
    public void EveryMomentOfAPass_Renders()
    {
        var s = new SaverSettings { TimeScale = 0, ShowInfo = false };
        var scene = new SkyScene(s);
        var ship = scene.LaunchStarship(new Random(1));
        using var surface = SKSurface.Create(new SKImageInfo(320, 180));
        using var renderer = new SkyRenderer();
        for (ship.Age = 0; ship.Age <= ship.DurationSeconds + 0.1; ship.Age += 0.05)
            renderer.Draw(surface.Canvas, new SKSizeI(320, 180), scene, s);
    }
}
