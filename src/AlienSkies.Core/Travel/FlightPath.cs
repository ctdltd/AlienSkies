using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Math;

namespace AlienSkies.Core.Travel;

/// <summary>
/// The straight-line route between two planets, in heliocentric parsecs. Both ends sit on the planet itself (host
/// position plus the planet's orbital offset), so the first and last frames see exactly what its surface view sees.
/// </summary>
public readonly record struct FlightPath(Vector3d Start, Vector3d End)
{
    public double LengthParsecs => (End - Start).Length;
    public double LengthLightYears => LengthParsecs * Units.LightYearsPerParsec;

    /// <summary>Unit vector from start to end (any unit vector for a zero-length path).</summary>
    public Vector3d Heading
    {
        get
        {
            var d = End - Start;
            return d.Length < 1e-15 ? Vector3d.UnitX : d.Normalized();
        }
    }

    /// <summary>Where the viewer is at progress u.</summary>
    public Vector3d PositionAt(double u) => Start + (End - Start) * FlightTimeline.Travelled(u);

    /// <summary>Distance still to go at progress u, in light years.</summary>
    public double RemainingLightYears(double u) => LengthLightYears * (1 - FlightTimeline.Travelled(u));

    /// <summary>A planet's position: its host's, plus its orbital offset (AU) at that moment.</summary>
    public static Vector3d PlanetPosition(Vector3d hostParsecs, Vector3d offsetAu) => hostParsecs + offsetAu * Units.ParsecsPerAu;
}

/// <summary>Camera-orientation helpers for turning between a planet's view and the direction of travel.</summary>
public static class FlightOrientation
{
    /// <summary>Spherical interpolation between two unit vectors; picks a consistent axis when they are opposite.</summary>
    public static Vector3d Slerp(Vector3d a, Vector3d b, double t)
    {
        double dot = System.Math.Clamp(Vector3d.Dot(a, b), -1, 1);
        if (dot > 0.9999) return (a + (b - a) * t).Normalized();
        double theta = System.Math.Acos(dot);
        Vector3d mid;
        if (dot < -0.9999)
        {
            // Opposite: rotate about any axis perpendicular to a.
            var axis = Vector3d.Cross(a, System.Math.Abs(a.Z) < 0.9 ? Vector3d.UnitZ : Vector3d.UnitX).Normalized();
            mid = Vector3d.Cross(axis, a);
            return (a * System.Math.Cos(theta * t) + mid * System.Math.Sin(theta * t)).Normalized();
        }
        double s = System.Math.Sin(theta);
        return (a * (System.Math.Sin((1 - t) * theta) / s) + b * (System.Math.Sin(t * theta) / s)).Normalized();
    }

    /// <summary>
    /// The horizon basis of a camera looking along <paramref name="forward"/> with <paramref name="up"/> roughly up: its
    /// local north is forward and its zenith is up, so a camera at azimuth 0, altitude 0 in this basis sees that view.
    /// </summary>
    public static HorizonBasis Basis(Vector3d forward, Vector3d up)
    {
        var f = forward.Normalized();
        var u = up - f * Vector3d.Dot(up, f);
        if (u.Length < 1e-9) u = Vector3d.Cross(f, System.Math.Abs(f.Z) < 0.9 ? Vector3d.UnitZ : Vector3d.UnitX);
        u = u.Normalized();
        return new HorizonBasis(u, f, Vector3d.Cross(f, u));
    }

    /// <summary>Blend two camera orientations (forward and up vectors) by t.</summary>
    public static HorizonBasis Blend(HorizonBasis a, HorizonBasis b, double t) =>
        Basis(Slerp(a.North, b.North, t), Slerp(a.Zenith, b.Zenith, t));

    /// <summary>Facing along the route, with celestial north as up (stable for the whole cruise), turned by a yaw.</summary>
    public static HorizonBasis Cruise(Vector3d heading, double yawRad = 0)
    {
        var level = Basis(heading, Vector3d.UnitZ);
        var forward = level.North * System.Math.Cos(yawRad) + level.East * System.Math.Sin(yawRad);
        return Basis(forward, level.Zenith);
    }
}
