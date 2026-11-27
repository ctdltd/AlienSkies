using System.Diagnostics;
using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Catalog;
using AlienSkies.Core.Math;

namespace AlienSkies.Rendering;

/// <summary>
/// The simulation state shared by every monitor: which planet, where the observer stands, and the clock.
/// Heavy work (re-projecting the catalog) happens only when the planet changes.
/// </summary>
public sealed class SkyScene
{
    private static readonly Lazy<IReadOnlyList<Star>> Catalog = new(CatalogLoader.LoadStars);
    private static readonly Lazy<IReadOnlyList<ExoplanetSystem>> Planets = new(CatalogLoader.LoadExoplanets);
    private static readonly Lazy<IReadOnlyList<(string, string, string)>> Lines = new(CatalogLoader.LoadConstellationLines);

    private readonly SaverSettings _settings;
    private readonly Stopwatch _clock;
    private readonly Random _random = new();
    private double _epochDays;
    private TimeSpan _lastHop;

    public AlienSky Sky { get; private set; } = null!;
    public PlanetFrame Frame { get; private set; } = null!;
    public PlanetConfig Config => Frame.Config;
    public IReadOnlyDictionary<string, VisibleStar> StarsById { get; private set; } = null!;
    public IReadOnlyList<(string Constellation, string FromId, string ToId)> ConstellationLines => Lines.Value;

    /// <summary>The flight under way between two systems in tour mode, or null when standing on a planet.</summary>
    public TourFlight? Flight { get; private set; }

    public SkyScene(SaverSettings settings)
    {
        _settings = settings;
        _clock = Stopwatch.StartNew();
        Load(ConfigFor(settings.PlanetName) ?? CatalogConfig(Planets.Value[0], settings.LatitudeDeg, settings.LongitudeDeg));
    }

    /// <summary>A second view of the same simulation (same settings and clock), used for the two ends of a flight.</summary>
    private SkyScene(SaverSettings settings, Stopwatch clock)
    {
        _settings = settings;
        _clock = clock;
    }

    /// <summary>
    /// An imaginary planet by display name, else a catalog planet by name, with the observer at the settings' latitude
    /// and longitude; null if nothing has that name.
    /// </summary>
    private PlanetConfig? ConfigFor(string? planetName)
    {
        double lat = _settings.LatitudeDeg, lon = _settings.LongitudeDeg;
        var name = planetName?.Trim();
        var mine = _settings.ImaginaryPlanets.FirstOrDefault(p => p.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (mine?.ToConfig(Catalog.Value, lat, lon) is { } config) return config;
        var planet = Planets.Value.FirstOrDefault(p => p.PlanetName.Equals(name, StringComparison.OrdinalIgnoreCase));
        return planet is null ? null : CatalogConfig(planet, lat, lon);
    }

    /// <summary>Simulated time in days since the orbit epoch.</summary>
    public double TimeDays => _epochDays + _clock.Elapsed.TotalSeconds * _settings.TimeScale / 86_400.0;

    /// <summary>Call once per frame; handles tour hops and flights.</summary>
    public void Tick()
    {
        if (Flight is { } flight)
        {
            flight.Progress = (_clock.Elapsed - flight.StartedAt).TotalSeconds / flight.DurationSeconds;
            if (flight.Progress < 1) return;
            Arrive(flight);
            return;
        }

        if (!_settings.TourMode || _settings.TourIntervalMinutes <= 0) return;
        if (_clock.Elapsed - _lastHop < TimeSpan.FromMinutes(_settings.TourIntervalMinutes)) return;

        _lastHop = _clock.Elapsed;
        // Uniform on the sphere, not uniform in latitude.
        double lat = Math.Asin(2 * _random.NextDouble() - 1) / Units.Deg;
        double lon = _random.NextDouble() * 360;

        // Every catalog planet and every valid imaginary one get an equal chance.
        var mine = _settings.TourIncludesImaginary ? _settings.ImaginaryPlanets : [];
        int pick = _random.Next(Planets.Value.Count + mine.Count);
        var config = pick < Planets.Value.Count
            ? CatalogConfig(Planets.Value[pick], lat, lon)
            : mine[pick - Planets.Value.Count].ToConfig(Catalog.Value, lat, lon)
              ?? CatalogConfig(Planets.Value[_random.Next(Planets.Value.Count)], lat, lon);
        if (_settings.TourFlights && _settings.FlightSeconds > 0) BeginFlight(config, _settings.FlightSeconds);
        else Load(config);
    }

    /// <summary>
    /// Starts a flight from the current planet to <paramref name="planetName"/> (an imaginary or catalog planet, observer
    /// at the settings' latitude and longitude). Returns false if no planet has that name. Hosts call <see cref="Tick"/>
    /// to advance it; tools can set <see cref="TourFlight.Progress"/> directly to render any moment of it.
    /// </summary>
    public bool FlyTo(string planetName, double? durationSeconds = null)
    {
        if (ConfigFor(planetName) is not { } config) return false;
        BeginFlight(config, durationSeconds ?? (_settings.FlightSeconds > 0 ? _settings.FlightSeconds : 30));
        return true;
    }

    private void BeginFlight(PlanetConfig config, double seconds)
    {
        var from = new SkyScene(_settings, _clock) { Sky = Sky, Frame = Frame, StarsById = StarsById, _epochDays = _epochDays };
        var to = new SkyScene(_settings, _clock);
        to.Load(config);
        Flight = new TourFlight(from, to, Catalog.Value, _clock.Elapsed, seconds);
    }

    /// <summary>The flight is over: this scene becomes the arrival planet, and the tour interval starts from now.</summary>
    private void Arrive(TourFlight flight)
    {
        (Sky, Frame, StarsById, _epochDays) = (flight.To.Sky, flight.To.Frame, flight.To.StarsById, flight.To._epochDays);
        Flight = null;
        _lastHop = _clock.Elapsed;
    }

    /// <summary>A catalog planet's orbit and spin, with the user's overrides from settings applied.</summary>
    private PlanetConfig CatalogConfig(ExoplanetSystem planet, double latitudeDeg, double longitudeDeg)
    {
        var s = _settings;
        var catalog = PlanetConfig.FromCatalog(planet, latitudeDeg, longitudeDeg);

        double a = s.UseCatalogOrbit ? catalog.SemiMajorAxisAu : s.OrbitalRadiusAu;
        double period = s.UseCatalogOrbit ? catalog.OrbitalPeriodDays
            : s.OrbitalPeriodDays > 0 ? s.OrbitalPeriodDays
            : PlanetConfig.KeplerPeriodDays(a, planet.HostMassSolar ?? 1.0);

        bool locked = s.RotationMode switch
        {
            RotationMode.TidallyLocked => true,
            RotationMode.Free => false,
            _ => catalog.TidallyLocked,
        };

        return new PlanetConfig
        {
            Planet = planet,
            SemiMajorAxisAu = a,
            OrbitalPeriodDays = period,
            Eccentricity = s.UseCatalogOrbit ? catalog.Eccentricity : s.Eccentricity,
            InclinationDeg = s.UseCatalogOrbit ? catalog.InclinationDeg : s.InclinationDeg,
            AscendingNodeDeg = s.AscendingNodeDeg,
            ObliquityDeg = s.ObliquityDeg,
            ObliquityAzimuthDeg = s.ObliquityAzimuthDeg,
            TidallyLocked = locked,
            RotationPeriodHours = s.RotationPeriodHours == 0 ? 24 : s.RotationPeriodHours,
            LatitudeDeg = latitudeDeg,
            LongitudeDeg = longitudeDeg,
        };
    }

    private void Load(PlanetConfig config)
    {
        var s = _settings;
        Frame = new PlanetFrame(config);
        Sky = AlienSky.Build(config.Planet, Catalog.Value, s.LimitingMagnitude);
        StarsById = Sky.Stars.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First());
        _epochDays = s.StartDays - _clock.Elapsed.TotalSeconds * s.TimeScale / 86_400.0;
    }

    public double DistanceFromEarthLy => Sky.Planet.DistanceParsecs * Units.LightYearsPerParsec;

    public Vector3d HostPositionParsecs => Sky.Planet.HostPosition;
}
