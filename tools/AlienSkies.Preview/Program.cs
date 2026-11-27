using System.Globalization;
using AlienSkies.Rendering;
using SkiaSharp;

// Usage: dotnet run --project tools/AlienSkies.Preview -- [--planet "TRAPPIST-1 e" | --star "Vega" [--name "Zorg"]] [--lat 30] [--lon 0]
//        [--days 0] [--az 180] [--alt 25] [--fov 100] [--width 1600] [--height 900] [--density 1] [--out sky.png]
//        [--milkyway 1] [--milkyway-brightness 1] [--siblings 1] [--info 1] [--ground 1] [--constellations 1] [--limit 6.5]
var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
for (int i = 0; i + 1 < args.Length; i += 2) opts[args[i].TrimStart('-')] = args[i + 1];

string Get(string key, string fallback) => opts.TryGetValue(key, out var v) ? v : fallback;
double D(string key, double fallback) => double.Parse(Get(key, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

var settings = new SaverSettings
{
    TimeScale = 0, // freeze the clock so the frame is reproducible
    StartDays = D("days", 0),
    LatitudeDeg = D("lat", 30),
    LongitudeDeg = D("lon", 0),
    ViewAzimuthDeg = D("az", 180),
    ViewAltitudeDeg = D("alt", 25),
    FieldOfViewDeg = D("fov", 100),
    ShowMilkyWay = D("milkyway", 1) != 0,
    MilkyWayBrightness = D("milkyway-brightness", 1),
    ShowSiblingPlanets = D("siblings", 1) != 0,
    ShowInfo = D("info", 1) != 0,
    ShowGround = D("ground", 1) != 0,
    ShowConstellations = D("constellations", 1) != 0,
    LimitingMagnitude = D("limit", 6.5),
};
if (opts.TryGetValue("planet", out var planet)) settings.PlanetName = planet;
if (opts.TryGetValue("star", out var star))   // imaginary planet around any catalog star
{
    var mine = new AlienSkies.Core.Astronomy.ImaginaryPlanet { HostStar = star, Name = Get("name", "") };
    settings.ImaginaryPlanets.Add(mine);
    settings.PlanetName = mine.DisplayName;
}

var size = new SKSizeI((int)D("width", 1600), (int)D("height", 900));
var scene = new SkyScene(settings);
using var surface = SKSurface.Create(new SKImageInfo(size.Width, size.Height));
using var renderer = new SkyRenderer();
renderer.Draw(surface.Canvas, size, scene, settings, 0, (float)D("density", 1));

string outPath = Get("out", "sky.png");
using var image = surface.Snapshot();
using var data = image.Encode(SKEncodedImageFormat.Png, 90);
using (var file = File.Create(outPath)) data.SaveTo(file);
Console.WriteLine($"Wrote {Path.GetFullPath(outPath)}  ({scene.Sky.Planet.PlanetName}, {scene.Sky.Stars.Count} stars)");
