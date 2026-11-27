using System.Globalization;
using AlienSkies.Rendering;
using SkiaSharp;

// Usage: dotnet run --project tools/AlienSkies.Preview -- [--planet "TRAPPIST-1 e" | --star "Vega" [--name "Zorg"]] [--lat 30] [--lon 0]
//        [--days 0 | --year 2035.5] [--az 180] [--alt 25] [--fov 100] [--width 1600] [--height 900] [--density 1] [--out sky.png]
//        [--milkyway 1] [--milkyway-brightness 1] [--milkyway-style tint|detailed] [--siblings 1] [--info 1] [--ground 1] [--constellations 1] [--limit 6.5] [--color-boost 1.5]
//        [--bench 200] times frames instead of rendering one
//        [--to "Proxima Cen b" [--progress 0.5 | --frames 12] [--warp true]]
//        [--starship 3.5 [--seed 1] | --starship-frames 12] renders the passing starship easter egg, seconds into its pass
// --to renders a tour flight from the planet to another one: one frame at --progress (0 = lift-off, 1 = landing), or
// --frames evenly spaced frames written as <out>-00.png, <out>-01.png, ...
var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
for (int i = 0; i + 1 < args.Length; i += 2) opts[args[i].TrimStart('-')] = args[i + 1];

string Get(string key, string fallback) => opts.TryGetValue(key, out var v) ? v : fallback;
double D(string key, double fallback) => double.Parse(Get(key, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

var settings = new SaverSettings
{
    TimeScale = 0, // freeze the clock so the frame is reproducible
    // Day 0 is J2000.0; --year picks a calendar date instead (it matters for binary orbits such as α Cen AB).
    StartDays = opts.ContainsKey("year")
        ? AlienSkies.Core.Astronomy.BinaryOrbit.DaysSinceJ2000(D("year", 2000)) + D("days", 0)
        : D("days", 0),
    LatitudeDeg = D("lat", 30),
    LongitudeDeg = D("lon", 0),
    ViewAzimuthDeg = D("az", 180),
    ViewAltitudeDeg = D("alt", 25),
    FieldOfViewDeg = D("fov", 100),
    ShowMilkyWay = D("milkyway", 1) != 0,
    MilkyWayBrightness = D("milkyway-brightness", 1),
    MilkyWayStyle = Enum.Parse<MilkyWayStyle>(Get("milkyway-style", "Tint"), ignoreCase: true),
    ShowSiblingPlanets = D("siblings", 1) != 0,
    ShowInfo = D("info", 1) != 0,
    ShowGround = D("ground", 1) != 0,
    ShowConstellations = D("constellations", 1) != 0,
    LimitingMagnitude = D("limit", 6.5),
    ColorBoost = D("color-boost", 1.5),
};
if (opts.TryGetValue("planet", out var planet)) settings.PlanetName = planet;
if (opts.TryGetValue("star", out var star))   // imaginary planet around any catalog star
{
    var mine = new AlienSkies.Core.Astronomy.ImaginaryPlanet { HostStar = star, Name = Get("name", "") };
    settings.ImaginaryPlanets.Add(mine);
    settings.PlanetName = mine.DisplayName;
}

settings.FlightWarp = bool.Parse(Get("warp", "true"));

var size = new SKSizeI((int)D("width", 1600), (int)D("height", 900));
var scene = new SkyScene(settings);
using var surface = SKSurface.Create(new SKImageInfo(size.Width, size.Height));
using var renderer = new SkyRenderer();
string outPath = Get("out", "sky.png");

void Render(string path)
{
    renderer.Draw(surface.Canvas, size, scene, settings, 0, (float)D("density", 1));
    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 90);
    using (var file = File.Create(path)) data.SaveTo(file);
    string what = scene.Flight is { } f
        ? $"flight {f.From.Sky.Planet.PlanetName} -> {f.To.Sky.Planet.PlanetName} at {f.Progress:0.00}, {f.Stage}"
        : $"{scene.Sky.Planet.PlanetName}, {scene.Sky.Stars.Count} stars";
    Console.WriteLine($"Wrote {Path.GetFullPath(path)}  ({what})");
}

if (opts.ContainsKey("starship") || opts.ContainsKey("starship-frames"))
{
    var ship = scene.LaunchStarship(new Random((int)D("seed", 1)));
    Console.WriteLine($"Starship {StarshipFlyby.ShipName}: cruises {ship.CruiseSeconds:0.0} s, then {StarshipFlyby.WarpSeconds} s of warp exit");
    int frames = (int)D("starship-frames", 0);
    if (frames <= 0) { ship.Age = D("starship", ship.CruiseSeconds / 2); Render(outPath); }
    else
    {
        string stem = Path.Combine(Path.GetDirectoryName(outPath) ?? "", Path.GetFileNameWithoutExtension(outPath));
        for (int i = 0; i < frames; i++)
        {
            ship.Age = ship.DurationSeconds * i / Math.Max(frames - 1, 1);
            Render($"{stem}-{i:00}.png");
        }
    }
}
else if (opts.TryGetValue("to", out var destination))
{
    if (!scene.FlyTo(destination)) { Console.Error.WriteLine($"No planet named \"{destination}\"."); return 1; }
    int frames = (int)D("frames", 0);
    if (frames <= 0)
    {
        scene.Flight!.Progress = D("progress", 0.5);
        Render(outPath);
    }
    else
    {
        string stem = Path.Combine(Path.GetDirectoryName(outPath) ?? "", Path.GetFileNameWithoutExtension(outPath));
        for (int i = 0; i < frames; i++)
        {
            scene.Flight!.Progress = frames == 1 ? 0.5 : i / (double)(frames - 1);
            Render($"{stem}-{i:00}.png");
        }
    }
}
else if (opts.TryGetValue("bench", out var benchFrames))
{
    // --bench N: time N frames with the view slowly panning (as the saver does), report ms per frame.
    int n = int.Parse(benchFrames, CultureInfo.InvariantCulture);
    for (int i = 0; i < 5; i++) renderer.Draw(surface.Canvas, size, scene, settings, i * 0.01, (float)D("density", 1));
    var times = new List<double>(n);
    for (int i = 0; i < n; i++)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        renderer.Draw(surface.Canvas, size, scene, settings, i * 0.01, (float)D("density", 1));
        surface.Flush();
        times.Add(sw.Elapsed.TotalMilliseconds);
    }
    times.Sort();
    Console.WriteLine($"{n} frames {size.Width}x{size.Height}: p50 {times[n / 2]:0.0} ms, p90 {times[n * 9 / 10]:0.0} ms");
    Render(outPath);
}
else
{
    Render(outPath);
}
return 0;
