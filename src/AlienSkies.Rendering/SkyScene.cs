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
        var planet = Planets.Value.FirstOrDefault(p => p.PlanetName.Equals(settings.PlanetName, StringComparison.OrdinalIgnoreCase))
                     ?? Planets.Value[0];
        Load(planet, settings.LatitudeDeg, settings.LongitudeDeg);
    }

    /// <summary>Simulated time in days since the orbit epoch.</summary>
    public double TimeDays => _epochDays + _clock.Elapsed.TotalSeconds * _settings.TimeScale / 86_400.0;

    /// <summary>Call once per frame; handles tour hops.</summary>
    public void Tick()
    {
        if (!_settings.TourMode || _settings.TourIntervalMinutes <= 0) return;
        if (_clock.Elapsed - _lastHop < TimeSpan.FromMinutes(_settings.TourIntervalMinutes)) return;

        _lastHop = _clock.Elapsed;
        var planet = Planets.Value[_random.Next(Planets.Value.Count)];
        // Uniform on the sphere, not uniform in latitude.
        double lat = Math.Asin(2 * _random.NextDouble() - 1) / Units.Deg;
        Load(planet, lat, _random.NextDouble() * 360);
    }

    private void Load(ExoplanetSystem planet, double latitudeDeg, double longitudeDeg)
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

        var config = new PlanetConfig
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

        Frame = new PlanetFrame(config);
        Sky = AlienSky.Build(planet, Catalog.Value, s.LimitingMagnitude);
        StarsById = Sky.Stars.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First());
        _epochDays = s.StartDays - _clock.Elapsed.TotalSeconds * s.TimeScale / 86_400.0;
    }

    public double DistanceFromEarthLy => Sky.Planet.DistanceParsecs * Units.LightYearsPerParsec;

    public Vector3d HostPositionParsecs => Sky.Planet.HostPosition;
}
