using System.Diagnostics;
using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Math;
using SkiaSharp;

namespace AlienSkies.Rendering;

/// <summary>Burn-in protection for the info text, and the planet's orbital plane (its ecliptic).</summary>
public sealed partial class SkyRenderer
{
    // Wall-clock, not simulated, time: the drift must keep going when the sky clock is frozen or racing.
    private readonly Stopwatch _overlayClock = Stopwatch.StartNew();

    /// <summary>Added to the overlay's drift clock, so a preview can show where the text will be later.</summary>
    public TimeSpan OverlayClockOffset { get; set; }

    /// <summary>How long one sweep of the info text across the bottom of the screen and back takes.</summary>
    private const double DriftSweepSeconds = 20 * 60;
    /// <summary>The up-and-down drift runs on a different period, so the text traces a slow zig-zag, not one line.</summary>
    private const double DriftLiftSeconds = 7 * 60;

    /// <summary>
    /// Where the info text goes: its left edge, and how far above its usual place. The text glides so slowly
    /// (a few pixels a second at most) that nobody sees it move, but no pixel stays lit by the same glyph for long.
    /// </summary>
    private (float X, float Lift) OverlayDrift(SKSizeI size, float textWidth, float density, bool drift)
    {
        float margin = 16 * density;
        if (!drift) return (margin, 0);
        double secs = (_overlayClock.Elapsed + OverlayClockOffset).TotalSeconds;
        float slack = Math.Max(0, size.Width - 2 * margin - textWidth);
        float maxLift = Math.Min(48 * density, size.Height * 0.08f);
        return (margin + slack * Triangle(secs / DriftSweepSeconds), maxLift * Triangle(secs / DriftLiftSeconds));
    }

    /// <summary>0 → 1 → 0 once per unit of <paramref name="t"/>, eased at the ends so it doesn't park at the edges.</summary>
    private static float Triangle(double t)
    {
        double f = t - Math.Floor(t);
        double tri = f < 0.5 ? 2 * f : 2 - 2 * f;
        return (float)(0.5 - 0.5 * Math.Cos(Math.PI * tri));
    }

    /// <summary>
    /// The planet's orbital plane: a faint great circle through its sun(s). Seen from the surface it is the planet's
    /// ecliptic, so the sun walks along it over a year and the sibling planets wander close to it. In this camera's
    /// rectilinear projection a great circle draws as a straight line, so a coarse sampling is exact.
    /// </summary>
    private void DrawOrbitPlane(SKCanvas canvas, SkyScene scene, HorizonBasis horizon, Camera cam, double day, float density)
    {
        var n = scene.Frame.OrbitNormal;
        // Any two unit vectors spanning the plane perpendicular to n.
        var a = Vector3d.Cross(n, Math.Abs(n.Z) < 0.9 ? Vector3d.UnitZ : new Vector3d(1, 0, 0)).Normalized();
        var b = Vector3d.Cross(n, a).Normalized();

        _stroke.Color = new SKColor(220, 180, 110, (byte)(80 * (1 - 0.6 * day)));
        _stroke.StrokeWidth = 1f * density;
        using var dash = SKPathEffect.CreateDash([6 * density, 5 * density], 0);
        _stroke.PathEffect = dash;
        try
        {
            using var builder = new SKPathBuilder();
            bool pen = false;
            const int steps = 180;
            for (int i = 0; i <= steps; i++)
            {
                double th = 2 * Math.PI * i / steps;
                var dir = a * Math.Cos(th) + b * Math.Sin(th);
                if (cam.Project(horizon.ToLocal(dir)) is SKPoint p)
                {
                    if (pen) builder.LineTo(p); else builder.MoveTo(p);
                    pen = true;
                }
                else pen = false;
            }
            using var path = builder.Detach();
            canvas.DrawPath(path, _stroke);
        }
        finally { _stroke.PathEffect = null; }
    }

    /// <summary>
    /// Time of day where the observer stands, as local solar time on a 24-hour clock: 12:00 when the host sun crosses
    /// the meridian, 00:00 at local midnight, whatever the planet's day length. Tidally locked worlds have no day, so
    /// they get the side of the planet instead.
    /// </summary>
    private static string LocalTimeText(SkyScene scene, HorizonBasis horizon, Sun host)
    {
        var c = scene.Config;
        if (c.TidallyLocked)
        {
            double alt = host.Altitude / Units.Deg;
            return alt > 3 ? "permanent day side" : alt < -18 ? "permanent night side" : "permanent twilight on the terminator";
        }

        var pole = scene.Frame.SpinPole;
        var sun = horizon.East * host.Local.X + horizon.North * host.Local.Y + horizon.Zenith * host.Local.Z;
        var zEq = horizon.Zenith - pole * Vector3d.Dot(horizon.Zenith, pole);
        var sEq = sun - pole * Vector3d.Dot(sun, pole);
        if (zEq.Length < 1e-6 || sEq.Length < 1e-6) return "no local time at the pole";

        // Hour angle: how far the spin has carried the meridian past the sun (positive in the afternoon).
        double hourAngle = Math.Atan2(Vector3d.Dot(pole, Vector3d.Cross(sEq, zEq)), Vector3d.Dot(sEq, zEq));
        if (c.RotationPeriodHours < 0) hourAngle = -hourAngle; // retrograde spin: the sun crosses the other way
        double hours = 12 + hourAngle / (2 * Math.PI) * 24;
        hours -= 24 * Math.Floor(hours / 24);
        int minutes = (int)(hours * 60) % (24 * 60);
        string text = $"local time {minutes / 60:00}:{minutes % 60:00}";

        // A solar day differs from the spin period because the planet also moves along its orbit.
        double spinDays = c.RotationPeriodHours / 24.0;
        double rate = 1 / spinDays - 1 / c.OrbitalPeriodDays; // turns of the sun around the sky per day, signed
        if (Math.Abs(rate) < 1e-9) return text;
        double solarHours = 24 / Math.Abs(rate);
        if (Math.Abs(solarHours - Math.Abs(c.RotationPeriodHours)) > 0.02 * Math.Abs(c.RotationPeriodHours))
            text += solarHours < 2400 ? $" (solar day {solarHours:0.#} h)" : $" (solar day {solarHours / 24:#,0} d)";
        return text;
    }
}
