using AlienSkies.Core.Catalog;
using AlienSkies.Core.Math;
using AlienSkies.Core.Travel;

namespace AlienSkies.Rendering;

/// <summary>
/// A flight from one planet to another in tour mode. <see cref="From"/> and <see cref="To"/> are complete scenes (they
/// share the tour's clock, so their skies keep turning), and the renderer dissolves between their surface views and
/// the moving <see cref="Sky"/> in between. See <see cref="FlightTimeline"/> for the stages.
/// </summary>
public sealed class TourFlight
{
    /// <summary>Top speed of the near-light-speed look, as a fraction of c. Purely visual: the trip itself is much faster.</summary>
    public const double WarpBeta = 0.6;

    public SkyScene From { get; }
    public SkyScene To { get; }
    public FlightSky Sky { get; }
    public TimeSpan StartedAt { get; }
    public double DurationSeconds { get; }

    /// <summary>0 at lift-off, 1 on landing. <see cref="SkyScene.Tick"/> advances it; tools may set it directly.</summary>
    public double Progress
    {
        get => _progress;
        set => _progress = Math.Clamp(value, 0, 1);
    }
    private double _progress;

    /// <summary>Index pairs into <see cref="FlightSky.Stars"/> for each constellation line both of whose stars exist.</summary>
    public IReadOnlyList<(int From, int To)> ConstellationLines { get; }

    internal TourFlight(SkyScene from, SkyScene to, IEnumerable<Star> catalog, TimeSpan startedAt, double durationSeconds)
    {
        From = from;
        To = to;
        Sky = new FlightSky(catalog, from.Sky, to.Sky);
        StartedAt = startedAt;
        DurationSeconds = Math.Max(1, durationSeconds);

        var lines = new List<(int, int)>();
        foreach (var (_, a, b) in from.ConstellationLines)
        {
            int i = Sky.IndexOf(a), j = Sky.IndexOf(b);
            if (i >= 0 && j >= 0) lines.Add((i, j));
        }
        ConstellationLines = lines;
    }

    public FlightStage Stage => FlightTimeline.StageAt(Progress);

    /// <summary>The planet whose surface the flight is leaving (first half) or approaching (second half).</summary>
    public SkyScene Nearest => FlightTimeline.IsOutbound(Progress) ? From : To;

    /// <summary>The route as of now. Its ends follow each planet along its orbit, a negligible few AU.</summary>
    public FlightPath Path => new(
        FlightPath.PlanetPosition(From.Sky.Planet.HostPosition, From.Frame.PlanetOffsetAu(From.TimeDays)),
        FlightPath.PlanetPosition(To.Sky.Planet.HostPosition, To.Frame.PlanetOffsetAu(To.TimeDays)));

    public Vector3d Position => Path.PositionAt(Progress);

    /// <summary>Speed for the warp effect, as a fraction of c (0 when it is turned off).</summary>
    public double Beta(bool warp) => warp ? WarpBeta * FlightTimeline.SpeedFraction(Progress) : 0;
}
