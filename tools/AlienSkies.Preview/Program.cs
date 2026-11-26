using System.Globalization;
using AlienSkies.Rendering;
using SkiaSharp;

// Usage: dotnet run --project tools/AlienSkies.Preview -- [--planet "TRAPPIST-1 e"] [--lat 30] [--lon 0]
//        [--days 0] [--az 180] [--alt 25] [--fov 100] [--width 1600] [--height 900] [--density 1] [--out sky.png]
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
};
if (opts.TryGetValue("planet", out var planet)) settings.PlanetName = planet;

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
