using System.Drawing.Drawing2D;
using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Math;
using AlienSkies.Screensaver.Settings;

namespace AlienSkies.Screensaver.Rendering;

/// <summary>
/// GDI+ renderer: a rectilinear (gnomonic) camera standing on the planet.
/// Deliberately simple so the scaffold runs anywhere; swap for a GPU renderer
/// (Silk.NET / Direct2D / WebView2+WebGL) once the catalog grows past ~50k visible stars.
/// </summary>
public sealed class SkyRenderer
{
    private static readonly Color NightSky = Color.FromArgb(3, 4, 12);
    private static readonly Color DaySky = Color.FromArgb(78, 120, 190);
    private static readonly Color Ground = Color.FromArgb(14, 12, 10);

    private readonly Font _labelFont = new("Segoe UI", 9f);
    private readonly Font _infoFont = new("Segoe UI", 10f);

    /// <param name="azimuthOffsetDeg">Extra yaw for this monitor when panning across several screens.</param>
    public void Draw(Graphics g, Size size, SkyScene scene, SaverSettings s, double azimuthOffsetDeg = 0)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        double t = scene.TimeDays;
        var horizon = scene.Frame.Horizon(t);
        var (hostDir, hostDistAu) = scene.Frame.HostStarFromPlanet(t);
        var hostLocal = horizon.ToLocal(hostDir);
        double hostAlt = Math.Asin(Math.Clamp(hostLocal.Z, -1, 1));

        // Daylight: 0 at astronomical twilight (-18°), 1 once the star is 6° up.
        double day = s.Atmosphere ? SmoothStep(-18 * Units.Deg, 6 * Units.Deg, hostAlt) : 0;
        g.Clear(Lerp(NightSky, DaySky, day));
        double limit = s.LimitingMagnitude - day * (s.LimitingMagnitude + 3.5); // daylight washes stars out

        var cam = new Camera(size, s.ViewAzimuthDeg + azimuthOffsetDeg, s.ViewAltitudeDeg, s.FieldOfViewDeg);

        if (s.ShowConstellations) DrawConstellations(g, scene, horizon, cam, s, day);
        DrawStars(g, scene, horizon, cam, s, limit);
        DrawHostStar(g, scene, hostLocal, hostDistAu, cam);
        if (s.ShowGround) DrawGround(g, cam, size, day);
        if (s.ShowInfo) DrawInfo(g, size, scene, horizon, hostAlt, hostDistAu, s.MarkSol);
    }

    private void DrawStars(Graphics g, SkyScene scene, HorizonBasis horizon, Camera cam, SaverSettings s, double limit)
    {
        using var labelBrush = new SolidBrush(Color.FromArgb(170, 200, 210, 230));
        foreach (var star in scene.Sky.Stars)
        {
            bool isSol = star.IsSol && s.MarkSol;
            if (star.ApparentMagnitude > limit && !isSol) continue;

            var local = horizon.ToLocal(star.Direction);
            if (s.ShowGround && local.Z < 0) continue;
            if (cam.Project(local) is not PointF p) continue;

            if (star.ApparentMagnitude <= limit)
            {
                float r = (float)Math.Clamp((0.7 + (limit - star.ApparentMagnitude) * 0.45) * s.StarScale, 0.6, 7);
                int alpha = (int)Math.Clamp((limit - star.ApparentMagnitude + 0.6) / 2.5 * 255, 60, 255);
                using var brush = new SolidBrush(Color.FromArgb(alpha, BvToColor(star.ColorBV)));
                g.FillEllipse(brush, p.X - r, p.Y - r, 2 * r, 2 * r);
            }

            if (isSol)
            {
                using var pen = new Pen(Color.FromArgb(200, 255, 210, 90), 1.2f);
                g.DrawEllipse(pen, p.X - 9, p.Y - 9, 18, 18);
                double ly = star.DistanceParsecs * Units.LightYearsPerParsec;
                g.DrawString($"Sun (Sol){'\n'}{ly:#,0.#} ly · mag {star.ApparentMagnitude:0.0}",
                             _labelFont, Brushes.Gold, p.X + 11, p.Y - 9);
            }
            else if (star.Name is not null && star.ApparentMagnitude < s.LabelMagnitudeLimit)
            {
                g.DrawString(star.Name, _labelFont, labelBrush, p.X + 6, p.Y - 6);
            }
        }
    }

    private static void DrawConstellations(Graphics g, SkyScene scene, HorizonBasis horizon, Camera cam, SaverSettings s, double day)
    {
        using var pen = new Pen(Color.FromArgb((int)(90 * (1 - day)), 110, 150, 220), 1f);
        foreach (var (_, fromId, toId) in scene.ConstellationLines)
        {
            if (!scene.StarsById.TryGetValue(fromId, out var a) || !scene.StarsById.TryGetValue(toId, out var b)) continue;
            var la = horizon.ToLocal(a.Direction);
            var lb = horizon.ToLocal(b.Direction);
            if (s.ShowGround && (la.Z < 0 || lb.Z < 0)) continue;
            if (cam.Project(la) is PointF pa && cam.Project(lb) is PointF pb) g.DrawLine(pen, pa, pb);
        }
    }

    private static void DrawHostStar(Graphics g, SkyScene scene, Vector3d hostLocal, double distAu, Camera cam)
    {
        if (cam.Project(hostLocal) is not PointF p) return;
        double angR = AlienSky.HostAngularRadius(scene.Sky.Planet, distAu);
        float r = (float)Math.Max(3, angR * cam.Focal);
        var color = Color.FromArgb(255, 244, 214);

        using (var glow = new GraphicsPath())
        {
            glow.AddEllipse(p.X - r * 6, p.Y - r * 6, r * 12, r * 12);
            using var pgb = new PathGradientBrush(glow)
            {
                CenterColor = Color.FromArgb(150, color),
                SurroundColors = [Color.FromArgb(0, color)],
            };
            g.FillPath(pgb, glow);
        }
        using var disc = new SolidBrush(color);
        g.FillEllipse(disc, p.X - r, p.Y - r, 2 * r, 2 * r);
    }

    private static void DrawGround(Graphics g, Camera cam, Size size, double day)
    {
        // The horizon is a great circle, so in a gnomonic projection it is a straight line.
        var a = cam.Project(Camera.Local(cam.AzimuthRad - 0.6, 0));
        var b = cam.Project(Camera.Local(cam.AzimuthRad + 0.6, 0));
        if (a is not PointF p1 || b is not PointF p2 || Math.Abs(p2.X - p1.X) < 1e-3) return;

        float slope = (p2.Y - p1.Y) / (p2.X - p1.X);
        float y0 = p1.Y - slope * p1.X, yW = y0 + slope * size.Width;
        PointF[] poly = [new(0, y0), new(size.Width, yW), new(size.Width, size.Height + 1), new(0, size.Height + 1)];
        using var brush = new SolidBrush(Lerp(Ground, Color.FromArgb(52, 48, 40), day));
        g.FillPolygon(brush, poly);
    }

    private void DrawInfo(Graphics g, Size size, SkyScene scene, HorizonBasis horizon, double hostAlt, double hostDistAu, bool markSol)
    {
        var c = scene.Config;
        var p = scene.Sky.Planet;
        string spin = c.TidallyLocked ? "tidally locked" : $"day {Math.Abs(c.RotationPeriodHours):0.#} h";
        string text =
            $"The sky from {p.PlanetName}  ·  orbiting {p.HostName}, {scene.DistanceFromEarthLy:0.#} ly from Earth\n" +
            $"Lat {c.LatitudeDeg:0.#}°  Lon {c.LongitudeDeg:0.#}°  ·  orbit {c.SemiMajorAxisAu:0.###} AU / {c.OrbitalPeriodDays:0.##} d  ·  {spin}  ·  " +
            $"{p.HostName} at {hostAlt / Units.Deg:0}° altitude, mag {AlienSky.HostApparentMagnitude(p, hostDistAu):0.0}";

        // Where to look for home, even when it is off-screen or below the horizon.
        if (markSol && scene.StarsById.TryGetValue(AlienSky.SolId, out var sol))
        {
            var (alt, az) = horizon.ToHorizontal(sol.Direction);
            string where = alt < 0 ? "below the horizon" : $"azimuth {az / Units.Deg:0}°, altitude {alt / Units.Deg:0}°";
            text += $"\nSun (Sol): {where}  ·  this light left it {sol.DistanceParsecs * Units.LightYearsPerParsec:#,0.#} years ago";
        }
        using var brush = new SolidBrush(Color.FromArgb(160, 210, 215, 225));
        g.DrawString(text, _infoFont, brush, 16, size.Height - 72);
    }

    // ---- helpers -----------------------------------------------------------

    private static Color BvToColor(double bv)
    {
        // Coarse B−V → RGB ramp: blue-white (−0.3) → white (0.3) → yellow (0.8) → orange-red (1.8+).
        bv = Math.Clamp(bv, -0.4, 2.0);
        (double r, double gr, double b) = bv switch
        {
            < 0.0 => (0.70 + bv * 0.5, 0.80 + bv * 0.3, 1.0),
            < 0.4 => (0.85 + bv * 0.35, 0.90, 1.0 - bv * 0.3),
            < 1.0 => (1.0, 0.95 - (bv - 0.4) * 0.25, 0.88 - (bv - 0.4) * 0.6),
            _ => (1.0, 0.80 - (bv - 1.0) * 0.25, 0.52 - (bv - 1.0) * 0.3),
        };
        static int B(double v) => (int)Math.Clamp(v * 255, 0, 255);
        return Color.FromArgb(B(r), B(gr), B(b));
    }

    private static double SmoothStep(double e0, double e1, double x)
    {
        double t = Math.Clamp((x - e0) / (e1 - e0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    /// <summary>Rectilinear camera in the observer's local (east, north, up) frame.</summary>
    private readonly struct Camera
    {
        private readonly Vector3d _forward, _right, _up;
        private readonly float _cx, _cy;
        public readonly double Focal;
        public readonly double AzimuthRad;

        public Camera(Size size, double azimuthDeg, double altitudeDeg, double fovDeg)
        {
            AzimuthRad = azimuthDeg * Units.Deg;
            double alt = Math.Clamp(altitudeDeg, -89, 89) * Units.Deg;
            _forward = Local(AzimuthRad, alt);
            _right = Vector3d.Cross(_forward, Vector3d.UnitZ).Normalized();
            _up = Vector3d.Cross(_right, _forward);
            _cx = size.Width / 2f;
            _cy = size.Height / 2f;
            Focal = _cx / Math.Tan(Math.Clamp(fovDeg, 10, 160) * Units.Deg / 2);
        }

        /// <summary>Unit vector in (east, north, up) for an azimuth (from north, toward east) and altitude.</summary>
        public static Vector3d Local(double az, double alt) =>
            new(Math.Cos(alt) * Math.Sin(az), Math.Cos(alt) * Math.Cos(az), Math.Sin(alt));

        public PointF? Project(Vector3d local)
        {
            double depth = Vector3d.Dot(local, _forward);
            if (depth <= 1e-3) return null;
            double x = Vector3d.Dot(local, _right) / depth * Focal;
            double y = Vector3d.Dot(local, _up) / depth * Focal;
            if (Math.Abs(x) > 4 * _cx || Math.Abs(y) > 4 * _cy) return null;
            return new PointF(_cx + (float)x, _cy - (float)y);
        }
    }
}
