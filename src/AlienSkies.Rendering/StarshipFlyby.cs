using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Math;

namespace AlienSkies.Rendering;

/// <summary>
/// The easter egg: now and then the survey ship <see cref="ShipName"/> cruises across the sky and jumps to warp.
/// Its path is a great circle on the observer's horizon (a straight line on screen, since the camera is gnomonic),
/// through a point near the middle of the main view, so every monitor of a panorama sees the same ship.
/// </summary>
public sealed class StarshipFlyby
{
    /// <summary>An original ship of our own: a saucer, a secondary hull and two nacelles on pylons.</summary>
    public const string ShipName = "Wayfarer";

    /// <summary>Seconds of the warp exit: the nacelles charge, the ship stretches away, the flash fades.</summary>
    public const double WarpSeconds = 1.1;

    private const double CruiseRateRad = 8.5 * Units.Deg; // per second along the arc

    /// <summary>Middle of the path and the direction of travel there, unit vectors in (east, north, up).</summary>
    public Vector3d Middle { get; }
    public Vector3d Along { get; }

    /// <summary>Arc angle from <see cref="Middle"/> where the ship enters, and where it goes to warp.</summary>
    public double EnterAngle { get; }
    public double WarpAngle { get; }

    public TimeSpan StartedAt { get; }

    /// <summary>Seconds since the ship entered. The scene advances it; tools can set it to render any moment.</summary>
    public double Age { get; set; }

    public double CruiseSeconds => (WarpAngle - EnterAngle) / CruiseRateRad;
    public double DurationSeconds => CruiseSeconds + WarpSeconds;
    public bool IsOver => Age >= DurationSeconds;

    /// <summary>Seconds into the warp exit, or negative while still cruising.</summary>
    public double WarpAge => Age - CruiseSeconds;

    private StarshipFlyby(Vector3d middle, Vector3d along, double enter, double warp, TimeSpan startedAt)
    {
        (Middle, Along, EnterAngle, WarpAngle, StartedAt) = (middle, along, enter, warp, startedAt);
    }

    /// <summary>
    /// Where the ship is on its arc (local unit vector), <paramref name="ahead"/> cruise-seconds further on.
    /// It stops at the warp point: the jump itself is drawn on screen.
    /// </summary>
    public Vector3d Position(double ahead = 0)
    {
        double a = EnterAngle + (Math.Min(Age, CruiseSeconds) + ahead) * CruiseRateRad;
        return Middle * Math.Cos(a) + Along * Math.Sin(a);
    }

    /// <summary>A random pass across the view: from off one side, going to warp somewhere past the middle.</summary>
    public static StarshipFlyby Random(Random rng, SaverSettings s, TimeSpan now)
    {
        double halfFov = Math.Clamp(s.FieldOfViewDeg, 10, 160) / 2;
        double az = (s.ViewAzimuthDeg + (rng.NextDouble() - 0.5) * halfFov * 0.4) * Units.Deg;
        // Upper part of the view, but always clear of the ground.
        double alt = Math.Clamp(s.ViewAltitudeDeg + (rng.NextDouble() * 0.5 - 0.05) * halfFov * 0.6, 12, 70) * Units.Deg;
        var middle = new Vector3d(Math.Cos(alt) * Math.Sin(az), Math.Cos(alt) * Math.Cos(az), Math.Sin(alt));

        var east = new Vector3d(Math.Cos(az), -Math.Sin(az), 0);   // horizontal tangent, toward increasing azimuth
        var up = Vector3d.Cross(east, middle);                    // tangent toward the zenith
        if (up.Z < 0) up = -up;
        double enter = -Math.Min(halfFov + 12, 85) * Units.Deg;
        double warp = (rng.NextDouble() * 0.45 - 0.05) * halfFov * Units.Deg;
        bool left = rng.Next(2) == 0; // left to right or right to left

        // A gentle climb or descent, as long as both ends of the pass stay clear of the horizon. The path's height is a
        // sinusoid in the arc angle with its low point far outside the pass, so checking the two ends is enough.
        double minZ = Math.Sin(4 * Units.Deg);
        Vector3d Along(double climb) => (east * Math.Cos(climb) + up * Math.Sin(climb)) * (left ? -1 : 1);
        bool Clear(Vector3d along, double a) => (middle * Math.Cos(a) + along * Math.Sin(a)).Z >= minZ;
        for (int tries = 0; tries < 8; tries++)
        {
            var along = Along((rng.NextDouble() - 0.5) * 30 * Units.Deg);
            if (Clear(along, enter) && Clear(along, warp)) return new StarshipFlyby(middle, along, enter, warp, now);
        }
        // Low in a very wide view: fly level and enter a little later, where the path is still above the horizon.
        enter = -Math.Min(-enter, Math.Acos(Math.Min(minZ / middle.Z, 1)));
        return new StarshipFlyby(middle, Along(0), enter, warp, now);
    }

    /// <summary>Time until the next pass: 10 to 20 minutes, at random.</summary>
    public static TimeSpan NextInterval(Random rng) => TimeSpan.FromMinutes(10 + rng.NextDouble() * 10);
}
