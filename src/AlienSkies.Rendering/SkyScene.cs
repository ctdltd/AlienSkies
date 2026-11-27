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
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Random _random = new();
    private double _epochDays;
    private TimeSpan _lastHop;

    public AlienSky Sky { get; private set; } = null!;
    public PlanetFrame Frame { get; private set; } = null!;
    public PlanetConfig Config => Frame.Config;
    public IReadOnlyDictionary<string, VisibleStar> StarsById { get; private set; } = null!;
    public IReadOnlyList<(string Constellation, string FromId, string ToId)> ConstellationLines => Lines.Value;

    public SkyScene(SaverSettings settings)
    {
        _settings = settings;
        double lat = settings.LatitudeDeg, lon = settings.LongitudeDeg;
        var config = settings.SelectedImaginaryPlanet()?.ToConfig(Catalog.Value, lat, lon)
                     ?? CatalogConfig(Planets.Value.FirstOrDefault(p =>
                            p.PlanetName.Equals(settings.PlanetName, StringComparison.OrdinalIgnoreCase)) ?? Planets.Value[0], lat, lon);
        Load(config);
    }

    /// <summary>Simulated time in days since the orbit epoch.</summary>
    public double TimeDays => _epochDays + _clock.Elapsed.TotalSeconds * _settings.TimeScale / 86_400.0;

    /// <summary>Call once per frame; handles tour hops.</summary>
    public void Tick()
    {
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
        Load(config);
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
