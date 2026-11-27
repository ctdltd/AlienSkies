using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Catalog;
using AlienSkies.Core.Math;

namespace AlienSkies.Core.Travel;

/// <summary>A light source that can be seen from anywhere along a flight: a catalog star, a host, a companion or Sol.</summary>
/// <param name="PositionParsecs">Heliocentric ICRS position.</param>
public sealed record FlightStar(string Id, string? Name, Vector3d PositionParsecs, double AbsoluteMagnitude, double ColorBV)
{
    public bool IsSol => Id == AlienSky.SolId;
}

/// <summary>How a <see cref="FlightStar"/> looks from the moving viewpoint at one instant.</summary>
public struct SeenStar
{
    /// <summary>Unit vector from the viewpoint, ICRS-aligned, after aberration.</summary>
    public Vector3d Direction;
    public double DistanceParsecs;
    /// <summary>Apparent V magnitude, including the Doppler brightening ahead of the ship and dimming behind it.</summary>
    public double ApparentMagnitude;
    /// <summary>B−V, shifted bluer ahead and redder behind by the Doppler effect.</summary>
    public double ColorBV;
}

/// <summary>
/// The sky seen from any point between two star systems. <see cref="AlienSky"/> fixes the viewpoint at one host and
/// projects the catalog once; here the viewpoint moves, so every star's direction and brightness are recomputed per
/// frame. At either end the result matches that system's <see cref="AlienSky"/> (same stars, same directions), which
/// is what lets a flight dissolve seamlessly into and out of a planet's surface view.
/// </summary>
public sealed class FlightSky
{
    private readonly FlightStar[] _stars;
    private readonly SeenStar[] _seen;
    private readonly Dictionary<string, int> _index;

    public IReadOnlyList<FlightStar> Stars => _stars;

    /// <summary>Index of the departure and arrival host stars in <see cref="Stars"/>.</summary>
    public int FromHostIndex { get; }
    public int ToHostIndex { get; }

    private readonly AlienSky _from, _to;
    private readonly int _fromCount, _toCount; // host plus companions

    public FlightSky(IEnumerable<Star> catalog, AlienSky from, AlienSky to)
    {
        _from = from;
        _to = to;
        var a = from.Planet.HostPosition;
        var b = to.Planet.HostPosition;
        double ra = AlienSky.SelfRadiusParsecs(from.Planet), rb = AlienSky.SelfRadiusParsecs(to.Planet);

        // Catalog entries for the two hosts and their companions are replaced by the systems' own suns, exactly as
        // AlienSky.Build does, so both ends agree with the planet view.
        var stars = new List<FlightStar>();
        foreach (var s in catalog)
        {
            if ((s.Position - a).Length < ra || (s.Position - b).Length < rb) continue;
            stars.Add(new FlightStar(s.Id, s.Name, s.Position, s.AbsoluteMagnitude, s.ColorBV));
        }
        stars.Add(new FlightStar(AlienSky.SolId, "Sun (Sol)", Vector3d.Zero, Units.SunAbsoluteV, 0.65));

        FromHostIndex = stars.Count;
        AddSystem(stars, from);
        _fromCount = stars.Count - FromHostIndex;
        if ((a - b).Length < 1e-9)
        {
            ToHostIndex = FromHostIndex;
        }
        else
        {
            ToHostIndex = stars.Count;
            AddSystem(stars, to);
            _toCount = stars.Count - ToHostIndex;
        }

        _stars = [.. stars];
        _seen = new SeenStar[_stars.Length];
        _index = new Dictionary<string, int>(_stars.Length);
        for (int i = 0; i < _stars.Length; i++) _index.TryAdd(_stars[i].Id, i);
        MoveSuns(0, 0);
    }

    /// <summary>The host first, then its companions; their positions are filled in by <see cref="MoveSuns"/>.</summary>
    private static void AddSystem(List<FlightStar> stars, AlienSky sky)
    {
        var at = sky.Planet.HostPosition;
        stars.Add(new FlightStar("host:" + sky.Planet.HostName, sky.HostName, at, sky.HostAbsoluteV, sky.HostColorBV));
        foreach (var c in sky.Companions) stars.Add(new FlightStar(c.Id, c.Name, at, c.AbsoluteMagnitude, c.ColorBV));
    }

    /// <summary>
    /// Puts each system's suns where their binary orbits have them at that system's time (days since J2000.0, as in
    /// <see cref="AlienSky.SunsFromPlanet"/>). Call before <see cref="Observe"/> when the clock runs.
    /// </summary>
    public void MoveSuns(double fromTimeDays, double toTimeDays)
    {
        Place(_from, FromHostIndex, _fromCount, fromTimeDays);
        if (ToHostIndex != FromHostIndex) Place(_to, ToHostIndex, _toCount, toTimeDays);
    }

    private void Place(AlienSky sky, int first, int count, double t)
    {
        if (count == 0) return;
        var centre = sky.Planet.HostPosition; // centre of the planet's orbit
        _stars[first] = _stars[first] with { PositionParsecs = centre + sky.HostOffsetAu(t) * Units.ParsecsPerAu };
        for (int i = 1; i < count; i++)
        {
            var c = sky.Companions[i - 1];
            _stars[first + i] = _stars[first + i] with { PositionParsecs = centre + c.OffsetAu(t) * Units.ParsecsPerAu };
        }
    }

    /// <summary>Position in <see cref="Stars"/> of a star by id (catalog id, <see cref="AlienSky.SolId"/>, ...), or -1.</summary>
    public int IndexOf(string id) => _index.TryGetValue(id, out int i) ? i : -1;

    /// <summary>
    /// Re-projects every star for a viewer at <paramref name="observerParsecs"/> moving along <paramref name="heading"/>
    /// at <paramref name="beta"/> (a fraction of light speed; 0 for no warp). The returned buffer is reused by the next
    /// call and lines up with <see cref="Stars"/>.
    /// </summary>
    public ReadOnlySpan<SeenStar> Observe(Vector3d observerParsecs, Vector3d heading, double beta = 0)
    {
        beta = System.Math.Clamp(beta, 0, 0.99);
        for (int i = 0; i < _stars.Length; i++)
        {
            var s = _stars[i];
            var rel = s.PositionParsecs - observerParsecs;
            double d = System.Math.Max(rel.Length, 1e-12);
            var dir = rel / d;
            double m = Units.ApparentMagnitude(s.AbsoluteMagnitude, d);
            double bv = s.ColorBV;
            if (beta > 0)
            {
                (dir, double doppler) = Aberrate(dir, heading, beta);
                m -= 5 * System.Math.Log10(doppler);  // flux ∝ D² (a gentle, readable choice; D⁴ washes out the sky)
                bv -= 1.5 * System.Math.Log10(doppler);
            }
            _seen[i] = new SeenStar { Direction = dir, DistanceParsecs = d, ApparentMagnitude = m, ColorBV = bv };
        }
        return _seen;
    }

    /// <summary>
    /// Relativistic aberration: where a star at unit direction <paramref name="direction"/> appears to a viewer moving
    /// along <paramref name="heading"/> at <paramref name="beta"/>, and its Doppler factor (above 1 ahead, below 1 behind).
    /// </summary>
    public static (Vector3d Direction, double Doppler) Aberrate(Vector3d direction, Vector3d heading, double beta)
    {
        var h = heading.Normalized();
        double cos = System.Math.Clamp(Vector3d.Dot(direction, h), -1, 1);
        double cosSeen = (cos + beta) / (1 + beta * cos);
        double gamma = 1 / System.Math.Sqrt(1 - beta * beta);
        double doppler = gamma * (1 + beta * cos);

        var perp = direction - h * cos;
        double len = perp.Length;
        if (len < 1e-12) return (cos >= 0 ? h : -h, doppler); // dead ahead or dead astern: no sideways shift
        double sinSeen = System.Math.Sqrt(System.Math.Max(0, 1 - cosSeen * cosSeen));
        return ((h * cosSeen + perp / len * sinSeen).Normalized(), doppler);
    }
}
