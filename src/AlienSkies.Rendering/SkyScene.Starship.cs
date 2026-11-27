namespace AlienSkies.Rendering;

/// <summary>Scheduling of the passing-starship easter egg (<see cref="StarshipFlyby"/>).</summary>
public sealed partial class SkyScene
{
    private TimeSpan? _nextStarshipAt;

    /// <summary>The starship crossing the sky right now, or null (almost always).</summary>
    public StarshipFlyby? Starship { get; private set; }

    /// <summary>Starts a pass now, whatever the setting and the schedule say (for tools and testing).</summary>
    public StarshipFlyby LaunchStarship(Random? rng = null) =>
        Starship = StarshipFlyby.Random(rng ?? _random, _settings, _clock.Elapsed);

    private void TickStarship()
    {
        var now = _clock.Elapsed;
        if (Starship is { } ship)
        {
            ship.Age = (now - ship.StartedAt).TotalSeconds;
            if (!ship.IsOver && Flight is null) return;
            Starship = null; // finished, or a tour flight took us away mid-pass
        }

        if (!_settings.StarshipFlybys) { _nextStarshipAt = null; return; }
        _nextStarshipAt ??= now + StarshipFlyby.NextInterval(_random);
        if (now < _nextStarshipAt || Flight is not null) return;

        _nextStarshipAt = now + StarshipFlyby.NextInterval(_random);
        LaunchStarship();
    }
}
