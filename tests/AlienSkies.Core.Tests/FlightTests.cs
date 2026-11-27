using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Catalog;
using AlienSkies.Core.Math;
using AlienSkies.Core.Travel;
using AlienSkies.Rendering;
using SkiaSharp;

namespace AlienSkies.Core.Tests;

public class FlightTimelineTests
{
    private static IEnumerable<double> Steps(int n = 2000) => Enumerable.Range(0, n + 1).Select(i => i / (double)n);

    [Fact]
    public void StartsAndEndsOnTheSurface_FacingThePlanetView()
    {
        Assert.Equal(1, FlightTimeline.SurfaceOpacity(0));
        Assert.Equal(1, FlightTimeline.SurfaceOpacity(1));
        Assert.Equal(0, FlightTimeline.HeadingBlend(0));
        Assert.Equal(0, FlightTimeline.HeadingBlend(1));
        Assert.Equal(0, FlightTimeline.Travelled(0));
        Assert.Equal(1, FlightTimeline.Travelled(1));
    }

    [Fact]
    public void FacesForwardAndIsInSpaceForTheWholeCruise()
    {
        foreach (double u in Steps().Where(u => u is >= FlightTimeline.TurnOutEnd and <= FlightTimeline.CruiseEnd))
        {
            Assert.Equal(1, FlightTimeline.HeadingBlend(u));
            Assert.Equal(0, FlightTimeline.SurfaceOpacity(u));
        }
    }

    [Fact]
    public void NeverMovesWhileTheSurfaceIsShowing_OrWhileTurning()
    {
        // Moving while the ground is visible would slide the stars against it; moving while turning would hide the start.
        foreach (double u in Steps())
        {
            if (FlightTimeline.SurfaceOpacity(u) > 0 || FlightTimeline.HeadingBlend(u) is > 0 and < 1)
                Assert.True(FlightTimeline.Travelled(u) is 0 or 1, $"moving at u={u}");
        }
    }

    [Fact]
    public void EverythingIsContinuous_SoTheViewNeverJumps()
    {
        Func<double, double>[] curves =
            [FlightTimeline.SurfaceOpacity, FlightTimeline.HeadingBlend, FlightTimeline.Travelled, FlightTimeline.SpeedFraction];
        foreach (var f in curves)
        {
            double prev = f(0);
            foreach (double u in Steps(10_000).Skip(1))
            {
                double v = f(u);
                Assert.True(System.Math.Abs(v - prev) < 0.01, $"jump of {v - prev} at u={u}");
                prev = v;
            }
        }
    }

    [Fact]
    public void DistanceOnlyIncreases_AndTopSpeedIsHalfway()
    {
        double prev = 0;
        foreach (double u in Steps())
        {
            double t = FlightTimeline.Travelled(u);
            Assert.True(t >= prev);
            prev = t;
        }
        double mid = (FlightTimeline.TurnOutEnd + FlightTimeline.CruiseEnd) / 2;
        Assert.Equal(1, FlightTimeline.SpeedFraction(mid), 9);
        Assert.Equal(0.5, FlightTimeline.Travelled(mid), 9);
        Assert.Equal(0, FlightTimeline.SpeedFraction(FlightTimeline.TurnOutEnd));
        Assert.Equal(0, FlightTimeline.SpeedFraction(FlightTimeline.CruiseEnd));
    }

    [Theory]
    [InlineData(0.0, FlightStage.LiftOff)]
    [InlineData(0.1, FlightStage.TurnOut)]
    [InlineData(0.5, FlightStage.Cruise)]
    [InlineData(0.85, FlightStage.TurnIn)]
    [InlineData(1.0, FlightStage.Landing)]
    public void Stages(double u, FlightStage stage) => Assert.Equal(stage, FlightTimeline.StageAt(u));
}

public class FlightSkyTests
{
    private static readonly IReadOnlyList<Star> Catalog = CatalogLoader.LoadStars();
    private static ExoplanetSystem Planet(string name) => CatalogLoader.LoadExoplanets().Single(p => p.PlanetName == name);

    private static (FlightSky Sky, AlienSky From, AlienSky To) Build(string from, string to)
    {
        var a = AlienSky.Build(Planet(from), Catalog);
        var b = AlienSky.Build(Planet(to), Catalog);
        return (new FlightSky(Catalog, a, b), a, b);
    }

    [Theory]
    [InlineData("alf Cen A (hypothetical Earth)", "TRAPPIST-1 e", true)]
    [InlineData("alf Cen A (hypothetical Earth)", "TRAPPIST-1 e", false)]
    [InlineData("eps Eri b", "Proxima Cen b", true)]
    [InlineData("eps Eri b", "Proxima Cen b", false)]
    public void AtEitherEnd_StarsMatchThatPlanetsOwnSky(string from, string to, bool atStart)
    {
        // This is what lets the surface view dissolve in and out without the stars jumping.
        var (sky, a, b) = Build(from, to);
        var (here, other) = atStart ? (a, b) : (b, a);
        var seen = sky.Observe(here.Planet.HostPosition, Vector3d.UnitX);
        var catalogById = Catalog.ToDictionary(c => c.Id);
        foreach (var star in here.Stars)
        {
            // The far system's own stars come from its exoplanet entry rather than the catalog, as they do while flying
            // toward it; seen from the other end that differs by no more than the catalog cross-match slop (< 0.2°).
            if (catalogById.TryGetValue(star.Id, out var c) &&
                (c.Position - other.Planet.HostPosition).Length < AlienSky.SelfRadiusParsecs(other.Planet))
            {
                // A companion keeps its catalog id; the host itself becomes the exoplanet entry's host star.
                int k = sky.IndexOf(star.Id);
                if (k < 0) k = sky.IndexOf("host:" + other.Planet.HostName);
                Assert.True(k >= 0, $"{star.Name ?? star.Id} missing");
                Assert.True(Vector3d.Dot(seen[k].Direction, star.Direction) > System.Math.Cos(0.2 * Units.Deg), star.Id);
                continue;
            }
            int i = sky.IndexOf(star.Id);
            Assert.True(i >= 0, $"{star.Id} missing from the flight sky");
            Assert.True(Vector3d.Dot(seen[i].Direction, star.Direction) > 1 - 1e-12, star.Id);
            Assert.Equal(star.ApparentMagnitude, seen[i].ApparentMagnitude, 9);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9000)]
    public void SecondSunsFollowTheirOrbits_AndMatchTheSurfaceView(double tDays)
    {
        // Toliman moves on its 80-year orbit; at lift-off the flight must show it where the surface view does.
        var (sky, a, _) = Build("alf Cen A (hypothetical Earth)", "TRAPPIST-1 e");
        var planetOffsetAu = new Vector3d(0.7, -0.5, 0.2);
        sky.MoveSuns(tDays, tDays);
        var seen = sky.Observe(FlightPath.PlanetPosition(a.Planet.HostPosition, planetOffsetAu), Vector3d.UnitX);
        var suns = a.SunsFromPlanet(planetOffsetAu, tDays);
        var toliman = suns.Single(x => x.Name == "Toliman");
        int i = sky.IndexOf(a.Companions.Single().Id);
        Assert.True(Vector3d.Dot(seen[i].Direction, toliman.Direction) > 1 - 1e-9);
        Assert.True(Vector3d.Dot(seen[sky.FromHostIndex].Direction, suns[0].Direction) > 1 - 1e-9);
        Assert.Equal(toliman.DistanceAu * Units.ParsecsPerAu, seen[i].DistanceParsecs, 12);
    }

    [Fact]
    public void HostsAndCompanionsAreNotDuplicatedFromTheCatalog()
    {
        var (sky, _, _) = Build("alf Cen A (hypothetical Earth)", "Proxima Cen b");
        Assert.Single(sky.Stars, s => s.Name == "Toliman");
        Assert.DoesNotContain(sky.Stars, s => s.Name == "Rigil Kentaurus" && !s.Id.StartsWith("host:"));
        Assert.Equal("alf Cen A", sky.Stars[sky.FromHostIndex].Name);
        Assert.Equal("Proxima Centauri", sky.Stars[sky.ToHostIndex].Name);
        Assert.Single(sky.Stars, s => s.IsSol);
    }

    [Fact]
    public void DestinationBrightensAndConstellationsDistortOnTheWay()
    {
        var (sky, a, b) = Build("alf Cen A (hypothetical Earth)", "TRAPPIST-1 e");
        var path = new FlightPath(a.Planet.HostPosition, b.Planet.HostPosition);
        double far = sky.Observe(path.PositionAt(0.3), path.Heading)[sky.ToHostIndex].ApparentMagnitude;
        double near = sky.Observe(path.PositionAt(0.8), path.Heading)[sky.ToHostIndex].ApparentMagnitude;
        Assert.True(near < far - 3, $"TRAPPIST-1 went from {far:0.0} to {near:0.0}");

        // Vega–Deneb–Altair (the Summer Triangle): its angles change as the viewpoint moves 40 ly.
        double Angle(Vector3d p, string x, string y)
        {
            var seen = sky.Observe(p, path.Heading).ToArray();
            var s = Catalog.Where(c => c.Name == x || c.Name == y).Select(c => seen[sky.IndexOf(c.Id)].Direction).ToArray();
            return System.Math.Acos(Vector3d.Dot(s[0], s[1]));
        }
        double before = Angle(path.Start, "Vega", "Altair"), after = Angle(path.End, "Vega", "Altair");
        Assert.True(System.Math.Abs(before - after) > 0.5 * Units.Deg);
    }

    [Fact]
    public void Aberration_CrowdsStarsForward_AndBluesThem()
    {
        var heading = Vector3d.UnitX;
        var side = Vector3d.UnitY;
        var (dir, doppler) = FlightSky.Aberrate(side, heading, 0.6);
        // Classic result: a star at 90° appears at cos θ' = β.
        Assert.Equal(0.6, Vector3d.Dot(dir, heading), 9);
        Assert.Equal(1.25, doppler, 9); // γ = 1.25

        var (ahead, dAhead) = FlightSky.Aberrate(heading, heading, 0.6);
        Assert.Equal(heading, ahead);
        Assert.Equal(2.0, dAhead, 9);
        var (behind, dBehind) = FlightSky.Aberrate(-heading, heading, 0.6);
        Assert.Equal(-heading, behind);
        Assert.Equal(0.5, dBehind, 9);

        var (still, d0) = FlightSky.Aberrate(side, heading, 0);
        Assert.True(Vector3d.Dot(still, side) > 1 - 1e-12);
        Assert.Equal(1, d0, 12);
    }
}

public class FlightOrientationTests
{
    [Fact]
    public void Slerp_StaysUnitLength_AndHitsBothEnds()
    {
        var a = new Vector3d(1, 2, 3).Normalized();
        var b = new Vector3d(-2, 0.5, 1).Normalized();
        Assert.True(Vector3d.Dot(FlightOrientation.Slerp(a, b, 0), a) > 1 - 1e-12);
        Assert.True(Vector3d.Dot(FlightOrientation.Slerp(a, b, 1), b) > 1 - 1e-12);
        for (double t = 0; t <= 1; t += 0.1) Assert.Equal(1, FlightOrientation.Slerp(a, b, t).Length, 9);
        var opposite = FlightOrientation.Slerp(Vector3d.UnitX, -Vector3d.UnitX, 0.5);
        Assert.Equal(0, Vector3d.Dot(opposite, Vector3d.UnitX), 9);
    }

    [Fact]
    public void Basis_IsRightHanded_LikeTheHorizonFrame()
    {
        var basis = FlightOrientation.Basis(new Vector3d(0.3, -0.4, 0.2), new Vector3d(0, 0.1, 1));
        Assert.True(Vector3d.Dot(Vector3d.Cross(basis.East, basis.North), basis.Zenith) > 1 - 1e-12);
        Assert.Equal(new Vector3d(0, 1, 0), RoundVec(basis.ToLocal(basis.North)));
    }

    [Fact]
    public void Cruise_FacesTheHeading_AndYawTurnsRight()
    {
        var h = new Vector3d(1, 1, 0.2).Normalized();
        var cruise = FlightOrientation.Cruise(h);
        Assert.True(Vector3d.Dot(cruise.North, h) > 1 - 1e-12);
        var yawed = FlightOrientation.Cruise(h, 90 * Units.Deg);
        Assert.True(Vector3d.Dot(yawed.North, cruise.East) > 1 - 1e-9);
    }

    private static Vector3d RoundVec(Vector3d v) => new(System.Math.Round(v.X, 9), System.Math.Round(v.Y, 9), System.Math.Round(v.Z, 9));
}

public class TourFlightSceneTests
{
    private static SaverSettings Settings() => new()
    {
        TimeScale = 0,
        PlanetName = "alf Cen A (hypothetical Earth)",
        ShowInfo = true,
    };

    [Fact]
    public void FlyTo_UnknownPlanet_DoesNothing()
    {
        var scene = new SkyScene(Settings());
        Assert.False(scene.FlyTo("No Such Planet b"));
        Assert.Null(scene.Flight);
    }

    [Fact]
    public void Flight_KeepsTheDeparturePlanetUntilItLands()
    {
        var scene = new SkyScene(Settings());
        Assert.True(scene.FlyTo("TRAPPIST-1 e", durationSeconds: 1));
        var flight = scene.Flight!;
        Assert.Equal("alf Cen A (hypothetical Earth)", scene.Sky.Planet.PlanetName);
        Assert.Equal("alf Cen A (hypothetical Earth)", flight.From.Sky.Planet.PlanetName);
        Assert.Equal("TRAPPIST-1 e", flight.To.Sky.Planet.PlanetName);
        Assert.InRange(flight.Path.LengthLightYears, 35, 50);

        Thread.Sleep(1100);
        scene.Tick();
        Assert.Null(scene.Flight);
        Assert.Equal("TRAPPIST-1 e", scene.Sky.Planet.PlanetName);
    }

    [Fact]
    public void Flight_RendersEveryStage()
    {
        var settings = Settings();
        var scene = new SkyScene(settings);
        scene.FlyTo("Proxima Cen b");
        using var surface = SKSurface.Create(new SKImageInfo(320, 180));
        using var renderer = new SkyRenderer();
        foreach (double u in new[] { 0, 0.05, 0.13, 0.3, 0.5, 0.7, 0.87, 0.95, 1 })
        {
            scene.Flight!.Progress = u;
            renderer.Draw(surface.Canvas, new SKSizeI(320, 180), scene, settings, 0);
            renderer.Draw(surface.Canvas, new SKSizeI(320, 180), scene, settings, 120); // next monitor of a panorama
        }
    }

    [Fact]
    public void FirstFrameOfAFlight_LooksLikeTheSurface()
    {
        var settings = Settings();
        var size = new SKSizeI(320, 180);
        using var renderer = new SkyRenderer();
        byte[] Frame(SkyScene s)
        {
            using var surface = SKSurface.Create(new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            renderer.Draw(surface.Canvas, size, s, settings, 0);
            using var img = surface.Snapshot();
            using var pixmap = img.PeekPixels();
            return pixmap.GetPixelSpan().ToArray();
        }

        var before = Frame(new SkyScene(settings));
        var flying = new SkyScene(settings);
        flying.FlyTo("TRAPPIST-1 e");
        flying.Flight!.Progress = 0;
        Assert.Equal(before, Frame(flying));
    }

    [Fact]
    public void Tour_FliesWhenTheSettingIsOn_AndCutsWhenOff()
    {
        foreach (bool fly in new[] { true, false })
        {
            var settings = Settings();
            settings.TourMode = true;
            settings.TourIntervalMinutes = 1e-6;
            settings.TourFlights = fly;
            var scene = new SkyScene(settings);
            Thread.Sleep(5);
            scene.Tick();
            Assert.Equal(fly, scene.Flight is not null);
        }
    }
}
