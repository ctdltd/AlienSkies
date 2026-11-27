using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Math;
using SkiaSharp;

namespace AlienSkies.Rendering;

/// <summary>
/// SkiaSharp renderer: a rectilinear (gnomonic) camera standing on the planet.
/// Shared by every host (Windows screen saver, Android screen saver, Android live wallpaper);
/// each host just hands it an <see cref="SKCanvas"/>.
/// </summary>
public sealed partial class SkyRenderer : IDisposable
{
    private static readonly SKColor NightSky = new(3, 4, 12);
    private static readonly SKColor DaySky = new(78, 120, 190);
    private static readonly SKColor NightGround = new(14, 12, 10);
    private static readonly SKColor DayGround = new(52, 48, 40);
    private static readonly SKColor SolGold = new(255, 210, 90);

    private readonly SKTypeface _typeface =
        SKTypeface.FromFamilyName(OperatingSystem.IsWindows() ? "Segoe UI" : "sans-serif") ?? SKTypeface.Default;
    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    /// <param name="azimuthOffsetDeg">Extra yaw: the next monitor of a panorama, or a live-wallpaper home-screen swipe.</param>
    /// <param name="density">Device pixels per logical pixel (1 on a typical desktop, ~3 on phones).</param>
    public void Draw(SKCanvas canvas, SKSizeI size, SkyScene scene, SaverSettings s,
                     double azimuthOffsetDeg = 0, float density = 1f)
    {
        if (scene.Flight is { } flight) DrawFlight(canvas, size, flight, s, azimuthOffsetDeg, density);
        else DrawSurface(canvas, size, scene, s, azimuthOffsetDeg, density);
    }

    /// <summary>The view from a planet's surface (everything except flights).</summary>
    private void DrawSurface(SKCanvas canvas, SKSizeI size, SkyScene scene, SaverSettings s,
                             double azimuthOffsetDeg, float density)
    {
        double t = scene.TimeDays;
        var horizon = scene.Frame.Horizon(t);
        var planetOffset = scene.Frame.PlanetOffsetAu(t);
        // Host first, then companions: a second star in a wide binary, or the partner in a circumbinary pair.
        var suns = scene.Sky.SunsFromPlanet(planetOffset, t)
            .Select(v => new Sun(v.Name, horizon.ToLocal(v.Direction), v.DistanceAu, v.ApparentMagnitude, v.AngularRadius,
                                 BvToColor(v.ColorBV), v.VisibleFraction))
            .ToList();
        var hostColor = suns[0].Color;

        // Daylight: 0 at astronomical twilight (-18°), full once a sun is 6° up, scaled by how bright
        // it looks. Our Sun (mag -26.7) gives full daylight; a dim red dwarf seen from a close orbit
        // (TRAPPIST-1 from planet e, about -21) gives a deep-twilight sky, and so does Toliman (about -19)
        // standing alone in the sky of an α Cen A planet. With two suns up, the brighter one wins.
        double day = 0;
        var skyTint = hostColor;
        if (s.Atmosphere)
        {
            foreach (var sun in suns)
            {
                double d = SmoothStep(-18 * Units.Deg, 6 * Units.Deg, sun.Altitude) * sun.Strength;
                if (d > day) (day, skyTint) = (d, sun.Color);
            }
        }
        canvas.Clear(Lerp(NightSky, Lerp(DaySky, skyTint, 0.35), day));
        double limit = s.LimitingMagnitude - day * (s.LimitingMagnitude + 3.5); // daylight washes stars out

        var cam = new Camera(size, s.ViewAzimuthDeg + azimuthOffsetDeg, s.ViewAltitudeDeg, s.FieldOfViewDeg);

        if (s.ShowConstellations) DrawConstellations(canvas, scene, horizon, cam, s, day, density);
        DrawStars(canvas, scene, horizon, cam, s, limit, density);
        // Nearer suns pass in front. One crossing a hotter star's face shows as a dark disc against it.
        double brightestSurface = suns.Max(x => x.SurfaceBrightness);
        foreach (var sun in suns.OrderByDescending(x => x.DistanceAu))
        {
            bool inTransit = suns.Any(o => o.DistanceAu > sun.DistanceAu && o.VisibleFraction < 1);
            DrawSun(canvas, sun, cam, density, inTransit ? sun.SurfaceBrightness / brightestSurface : 1);
        }
        if (s.ShowGround) DrawGround(canvas, cam, size, day);
        if (s.ShowInfo) DrawInfo(canvas, size, scene, horizon, suns, s.MarkSol, density);
    }

    private void DrawStars(SKCanvas canvas, SkyScene scene, HorizonBasis horizon, Camera cam,
                           SaverSettings s, double limit, float density)
    {
        using var labelFont = new SKFont(_typeface, 12 * density);
        foreach (var star in scene.Sky.Stars)
        {
            bool isSol = star.IsSol && s.MarkSol;
            if (star.ApparentMagnitude > limit && !isSol) continue;

            var local = horizon.ToLocal(star.Direction);
            if (s.ShowGround && local.Z < 0) continue;
            if (cam.Project(local) is not SKPoint p) continue;

            if (star.ApparentMagnitude <= limit)
            {
                float r = (float)Math.Clamp((0.7 + (limit - star.ApparentMagnitude) * 0.45) * s.StarScale, 0.6, 7) * density;
                byte alpha = (byte)Math.Clamp((limit - star.ApparentMagnitude + 0.6) / 2.5 * 255, 60, 255);
                _fill.Color = BvToColor(star.ColorBV).WithAlpha(alpha);
                canvas.DrawCircle(p, r, _fill);
            }

            if (isSol)
            {
                _stroke.Color = SolGold.WithAlpha(200);
                _stroke.StrokeWidth = 1.2f * density;
                canvas.DrawCircle(p, 9 * density, _stroke);

                double ly = star.DistanceParsecs * Units.LightYearsPerParsec;
                _fill.Color = SolGold;
                float x = p.X + 12 * density;
                canvas.DrawText("Sun (Sol)", x, p.Y - 1 * density, SKTextAlign.Left, labelFont, _fill);
                canvas.DrawText($"{ly:#,0.#} ly · mag {star.ApparentMagnitude:0.0}", x, p.Y + labelFont.Spacing - 1 * density, SKTextAlign.Left, labelFont, _fill);
            }
            else if (star.Name is not null && star.ApparentMagnitude < s.LabelMagnitudeLimit)
            {
                _fill.Color = new SKColor(200, 210, 230, 170);
                canvas.DrawText(star.Name, p.X + 6 * density, p.Y + 4 * density, SKTextAlign.Left, labelFont, _fill);
            }
        }
    }

    private void DrawConstellations(SKCanvas canvas, SkyScene scene, HorizonBasis horizon, Camera cam,
                                    SaverSettings s, double day, float density)
    {
        _stroke.Color = new SKColor(110, 150, 220, (byte)(90 * (1 - day)));
        _stroke.StrokeWidth = 1f * density;
        foreach (var (_, fromId, toId) in scene.ConstellationLines)
        {
            if (!scene.StarsById.TryGetValue(fromId, out var a) || !scene.StarsById.TryGetValue(toId, out var b)) continue;
            var la = horizon.ToLocal(a.Direction);
            var lb = horizon.ToLocal(b.Direction);
            if (s.ShowGround && (la.Z < 0 || lb.Z < 0)) continue;
            if (cam.Project(la) is SKPoint pa && cam.Project(lb) is SKPoint pb) canvas.DrawLine(pa, pb, _stroke);
        }
    }

    /// <param name="relativeSurface">Surface brightness against the brightest sun's, for a sun crossing another's face,
    /// so a cool red dwarf in front of a hotter star shows dark. 1 otherwise.</param>
    private void DrawSun(SKCanvas canvas, Sun sun, Camera cam, float density, double relativeSurface = 1)
    {
        if (cam.Project(sun.Local) is not SKPoint p) return;
        float r = (float)Math.Max(3 * density, sun.AngularRadius * cam.Focal);
        // A fainter sun gets a tighter glow: 6 disc radii for a full-daylight star, 2 for one too faint to light the sky.
        float glowR = r * (float)(2 + 4 * sun.Strength);

        using (var glow = SKShader.CreateRadialGradient(p, glowR, [sun.Color.WithAlpha(150), sun.Color.WithAlpha(0)], SKShaderTileMode.Clamp))
        using (var glowPaint = new SKPaint { IsAntialias = true, Shader = glow })
            canvas.DrawCircle(p, glowR, glowPaint);

        var disc = Lerp(sun.Color, SKColors.White, 0.4); // the disc itself reads paler than its glow
        _fill.Color = Lerp(SKColors.Black, disc, Math.Clamp(Math.Pow(relativeSurface, 0.3), 0.3, 1));
        canvas.DrawCircle(p, r, _fill);
    }

    private void DrawGround(SKCanvas canvas, Camera cam, SKSizeI size, double day)
    {
        // The horizon is a great circle, so in a gnomonic projection it is a straight line.
        var a = cam.Project(Camera.Local(cam.AzimuthRad - 0.6, 0));
        var b = cam.Project(Camera.Local(cam.AzimuthRad + 0.6, 0));
        if (a is not SKPoint p1 || b is not SKPoint p2 || Math.Abs(p2.X - p1.X) < 1e-3) return;

        float slope = (p2.Y - p1.Y) / (p2.X - p1.X);
        float y0 = p1.Y - slope * p1.X, yW = y0 + slope * size.Width;
        using var builder = new SKPathBuilder();
        builder.MoveTo(0, y0);
        builder.LineTo(size.Width, yW);
        builder.LineTo(size.Width, size.Height + 1);
        builder.LineTo(0, size.Height + 1);
        builder.Close();
        using var path = builder.Detach();
        _fill.Color = Lerp(NightGround, DayGround, day);
        canvas.DrawPath(path, _fill);
    }

    private void DrawInfo(SKCanvas canvas, SKSizeI size, SkyScene scene, HorizonBasis horizon,
                          IReadOnlyList<Sun> suns, bool markSol, float density)
    {
        var c = scene.Config;
        var p = scene.Sky.Planet;
        string spin = c.TidallyLocked ? "tidally locked" : $"day {Math.Abs(c.RotationPeriodHours):0.#} h";
        var lines = new List<string>
        {
            $"The sky from {(p.IsImaginary && !p.PlanetName.Contains("imaginary", StringComparison.OrdinalIgnoreCase) ? "imaginary " : "")}{p.PlanetName}  ·  orbiting {p.HostName}, {scene.DistanceFromEarthLy:0.#} ly from Earth",
            $"Lat {c.LatitudeDeg:0.#}°  Lon {c.LongitudeDeg:0.#}°  ·  orbit {c.SemiMajorAxisAu:0.###} AU / {c.OrbitalPeriodDays:0.##} d  ·  {spin}  ·  " +
            $"{suns[0].Name} at {suns[0].Altitude / Units.Deg:0}° altitude, mag {suns[0].Magnitude:0.0}",
        };
        foreach (var sun in suns.Skip(1))
            lines.Add($"Second sun {sun.Name}: {sun.Altitude / Units.Deg:0}° altitude, mag {sun.Magnitude:0.0}, {sun.DistanceAu:0.#} AU away");

        // Where to look for home, even when it is off-screen or below the horizon.
        if (markSol && scene.StarsById.TryGetValue(AlienSky.SolId, out var sol))
        {
            var (alt, az) = horizon.ToHorizontal(sol.Direction);
            string where = alt < 0 ? "below the horizon" : $"azimuth {az / Units.Deg:0}°, altitude {alt / Units.Deg:0}°";
            lines.Add($"Sun (Sol): {where}  ·  this light left it {sol.DistanceParsecs * Units.LightYearsPerParsec:#,0.#} years ago");
        }

        using var font = new SKFont(_typeface, 13 * density);
        // Shrink to fit narrow screens (phones in portrait).
        float widest = lines.Max(l => font.MeasureText(l));
        float maxWidth = size.Width - 32 * density;
        if (widest > maxWidth) font.Size *= maxWidth / widest;

        _fill.Color = new SKColor(210, 215, 225, 160);
        float y = size.Height - 16 * density - (lines.Count - 1) * font.Spacing;
        foreach (var line in lines)
        {
            canvas.DrawText(line, 16 * density, y, SKTextAlign.Left, font, _fill);
            y += font.Spacing;
        }
    }

    // ---- helpers -----------------------------------------------------------

    /// <summary>A star close enough to show a disc and light the sky: the host, or a companion in a multiple system.</summary>
    private readonly record struct Sun(string Name, Vector3d Local, double DistanceAu, double Magnitude, double AngularRadius,
                                       SKColor Color, double VisibleFraction = 1)
    {
        /// <summary>Light per unit of disc area, eclipse aside (it depends only on the star's temperature).</summary>
        public double SurfaceBrightness =>
            Math.Pow(10, -0.4 * Magnitude) / Math.Max(VisibleFraction, 1e-4) / (AngularRadius * AngularRadius);

        public double Altitude => Math.Asin(Math.Clamp(Local.Z, -1, 1));

        /// <summary>0 for a star too faint to light the sky (mag -12), 1 for full daylight (mag -26).</summary>
        public double Strength => Math.Clamp((-Magnitude - 12) / 14.0, 0, 1);
    }

    private static SKColor BvToColor(double bv)
    {
        // Coarse B−V → RGB ramp: blue-white (−0.3) → white (0.3) → yellow (0.8) → orange-red (1.8+).
        bv = Math.Clamp(bv, -0.4, 2.0);
        (double r, double g, double b) = bv switch
        {
            < 0.0 => (0.70 + bv * 0.5, 0.80 + bv * 0.3, 1.0),
            < 0.4 => (0.85 + bv * 0.35, 0.90, 1.0 - bv * 0.3),
            < 1.0 => (1.0, 0.95 - (bv - 0.4) * 0.25, 0.88 - (bv - 0.4) * 0.6),
            _ => (1.0, 0.80 - (bv - 1.0) * 0.25, 0.52 - (bv - 1.0) * 0.3),
        };
        static byte B(double v) => (byte)Math.Clamp(v * 255, 0, 255);
        return new SKColor(B(r), B(g), B(b));
    }

    private static double SmoothStep(double e0, double e1, double x)
    {
        double t = Math.Clamp((x - e0) / (e1 - e0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static SKColor Lerp(SKColor a, SKColor b, double t) => new(
        (byte)(a.Red + (b.Red - a.Red) * t), (byte)(a.Green + (b.Green - a.Green) * t), (byte)(a.Blue + (b.Blue - a.Blue) * t));

    public void Dispose()
    {
        _fill.Dispose();
        _stroke.Dispose();
        _typeface.Dispose();
    }

    /// <summary>Rectilinear camera in the observer's local (east, north, up) frame.</summary>
    private readonly struct Camera
    {
        private readonly Vector3d _forward, _right, _up;
        private readonly float _cx, _cy;
        public readonly double Focal;
        public readonly double AzimuthRad;

        public Camera(SKSizeI size, double azimuthDeg, double altitudeDeg, double fovDeg)
        {
            AzimuthRad = azimuthDeg * Units.Deg;
            double alt = Math.Clamp(altitudeDeg, -89, 89) * Units.Deg;
            _forward = Local(AzimuthRad, alt);
            _right = Vector3d.Cross(_forward, Vector3d.UnitZ).Normalized();
            _up = Vector3d.Cross(_right, _forward);
            _cx = size.Width / 2f;
            _cy = size.Height / 2f;
            // Field of view applies to the longer screen edge, so portrait phones keep a wide sky.
            float halfLong = Math.Max(_cx, _cy);
            Focal = halfLong / Math.Tan(Math.Clamp(fovDeg, 10, 160) * Units.Deg / 2);
        }

        /// <summary>Unit vector in (east, north, up) for an azimuth (from north, toward east) and altitude.</summary>
        public static Vector3d Local(double az, double alt) =>
            new(Math.Cos(alt) * Math.Sin(az), Math.Cos(alt) * Math.Cos(az), Math.Sin(alt));

        public SKPoint? Project(Vector3d local)
        {
            double depth = Vector3d.Dot(local, _forward);
            if (depth <= 1e-3) return null;
            double x = Vector3d.Dot(local, _right) / depth * Focal;
            double y = Vector3d.Dot(local, _up) / depth * Focal;
            if (Math.Abs(x) > 4 * _cx || Math.Abs(y) > 4 * _cy) return null;
            return new SKPoint(_cx + (float)x, _cy - (float)y);
        }
    }
}
