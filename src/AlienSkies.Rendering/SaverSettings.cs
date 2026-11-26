using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlienSkies.Core.Catalog;

namespace AlienSkies.Rendering;

public enum RotationMode
{
    [Description("Auto (locked if orbit < 0.1 AU)")] Auto,
    TidallyLocked,
    Free,
}

/// <summary>
/// User-facing configuration, edited in a PropertyGrid and stored as JSON in
/// %APPDATA%\AlienSkies\settings.json. Category prefixes keep the grid in a sensible order.
/// </summary>
public sealed class SaverSettings
{
    // ---- Planet ------------------------------------------------------------
    [Category("1. Planet"), DisplayName("Planet"), TypeConverter(typeof(PlanetNameConverter))]
    [Description("Catalog planet to stand on, chosen from the exoplanet catalog. Ignored while an imaginary planet's host star is set.")]
    public string PlanetName { get; set; } = "alf Cen A (hypothetical Earth)";

    /// <summary>Value of <see cref="ImaginaryHostStar"/> meaning "no imaginary planet, use <see cref="PlanetName"/>".</summary>
    public const string NoImaginaryHost = "(none)";

    [Category("1. Planet"), DisplayName("Imaginary planet: host star"), TypeConverter(typeof(StarNameConverter))]
    [Description("Pick any catalog star to put your own made-up planet around it (overrides Planet). It starts where it gets Earth's sunlight, on a circular orbit; turn off 'Use catalog orbit' to set your own orbit below. (none) = use the catalog planet.")]
    public string ImaginaryHostStar { get; set; } = NoImaginaryHost;

    [Category("1. Planet"), DisplayName("Imaginary planet: name")]
    [Description("Optional name for your imaginary planet, shown in the info overlay.")]
    public string ImaginaryPlanetName { get; set; } = "";

    /// <summary>True when an imaginary planet's host star is chosen.</summary>
    [Browsable(false), JsonIgnore]
    public bool HasImaginaryPlanet =>
        !string.IsNullOrWhiteSpace(ImaginaryHostStar) && ImaginaryHostStar.Trim() != NoImaginaryHost;

    [Category("1. Planet"), DisplayName("Use catalog orbit")]
    [Description("True: orbit values come from the exoplanet catalog (unknowns get defaults); for an imaginary planet, an Earth-like circular orbit. False: use the overrides below.")]
    public bool UseCatalogOrbit { get; set; } = true;

    [Category("1. Planet"), DisplayName("Orbital radius (AU)")]
    [Description("Semi-major axis override. Sets how big and bright the host star looks.")]
    public double OrbitalRadiusAu { get; set; } = 1.0;

    [Category("1. Planet"), DisplayName("Orbital period (days)")]
    [Description("Period override. 0 = derive from Kepler's third law using the host star's mass (recommended: radius and period are not independent).")]
    public double OrbitalPeriodDays { get; set; }

    [Category("1. Planet"), DisplayName("Eccentricity")]
    public double Eccentricity { get; set; }

    [Category("1. Planet"), DisplayName("Inclination (°)")]
    [Description("Orbit tilt relative to Earth's line of sight, archive convention: 0 = face-on to us, 90 = edge-on. Transiting planets are ≈90°, which puts our Sun in their zodiac.")]
    public double InclinationDeg { get; set; } = 90;

    [Category("1. Planet"), DisplayName("Ascending node (°)")]
    [Description("Position angle of the orbit's ascending node on Earth's sky. Almost never measured; 0 is a guess.")]
    public double AscendingNodeDeg { get; set; }

    // ---- Spin --------------------------------------------------------------
    [Category("2. Spin"), DisplayName("Rotation")]
    [Description("Tidally locked worlds keep their star fixed in the sky; the stars still wheel past once per orbit.")]
    public RotationMode RotationMode { get; set; } = RotationMode.Auto;

    [Category("2. Spin"), DisplayName("Rotation period (hours)")]
    [Description("Sidereal day length for free-rotating planets. Negative = retrograde.")]
    public double RotationPeriodHours { get; set; } = 24;

    [Category("2. Spin"), DisplayName("Axial tilt (°)")]
    [Description("Obliquity of the spin axis to the orbit normal (Earth 23.44°). Sets which stars are circumpolar and gives seasons.")]
    public double ObliquityDeg { get; set; }

    [Category("2. Spin"), DisplayName("Axial tilt direction (°)")]
    [Description("Which way the axis leans, measured in the orbit plane from the ascending node.")]
    public double ObliquityAzimuthDeg { get; set; }

    // ---- Observer ----------------------------------------------------------
    [Category("3. Observer"), DisplayName("Latitude (°)")]
    public double LatitudeDeg { get; set; } = 30;

    [Category("3. Observer"), DisplayName("Longitude (°)")]
    [Description("On a tidally locked world: 0 = star overhead (permanent noon), 90/270 = terminator (permanent sunset), 180 = permanent night.")]
    public double LongitudeDeg { get; set; }

    // ---- View --------------------------------------------------------------
    [Category("4. View"), DisplayName("Look azimuth (°)")]
    [Description("Compass direction to face: 0 = planet's north, 90 = east, 180 = south.")]
    public double ViewAzimuthDeg { get; set; } = 180;

    [Category("4. View"), DisplayName("Look altitude (°)")]
    public double ViewAltitudeDeg { get; set; } = 25;

    [Category("4. View"), DisplayName("Field of view (°)")]
    public double FieldOfViewDeg { get; set; } = 100;

    [Category("4. View"), DisplayName("Pan across monitors")]
    [Description("Each additional monitor continues the panorama to the right instead of repeating the view.")]
    public bool PanoramaAcrossMonitors { get; set; } = true;

    [Category("4. View"), DisplayName("Limiting magnitude")]
    [Description("Faintest star drawn. 6.5 ≈ dark-sky naked eye; raise it for a richer sky.")]
    public double LimitingMagnitude { get; set; } = 6.5;

    [Category("4. View"), DisplayName("Star size scale")]
    public double StarScale { get; set; } = 1.0;

    [Category("4. View"), DisplayName("Atmosphere / daylight")]
    [Description("Brighten the sky and wash out stars when the host star is up. Off = airless moon.")]
    public bool Atmosphere { get; set; } = true;

    [Category("4. View"), DisplayName("Show ground")]
    public bool ShowGround { get; set; } = true;

    [Category("4. View"), DisplayName("Show Earth's constellations")]
    [Description("Draw Earth's constellation figures so you can watch them distort with distance.")]
    public bool ShowConstellations { get; set; } = true;

    [Category("4. View"), DisplayName("Label stars brighter than")]
    [Description("Magnitude threshold for name labels. Set below -30 to turn labels off.")]
    public double LabelMagnitudeLimit { get; set; } = 1.5;

    [Category("4. View"), DisplayName("Mark our Sun")]
    public bool MarkSol { get; set; } = true;

    [Category("4. View"), DisplayName("Show info overlay")]
    public bool ShowInfo { get; set; } = true;

    // ---- Time --------------------------------------------------------------
    [Category("5. Time"), DisplayName("Time scale (×)")]
    [Description("Simulated seconds per real second. 600 = one Earth day every 2.4 minutes.")]
    public double TimeScale { get; set; } = 600;

    [Category("5. Time"), DisplayName("Start time (days)")]
    [Description("Orbital phase to start from, in days since periapsis/epoch.")]
    public double StartDays { get; set; }

    // ---- Tour --------------------------------------------------------------
    [Category("6. Tour"), DisplayName("Tour mode")]
    [Description("Hop to a random catalog planet (and random latitude) at a fixed interval.")]
    public bool TourMode { get; set; }

    [Category("6. Tour"), DisplayName("Tour interval (minutes)")]
    public double TourIntervalMinutes { get; set; } = 5;

    // ---- Persistence -------------------------------------------------------
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlienSkies", "settings.json");

    public static SaverSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<SaverSettings>(File.ReadAllText(FilePath), Json) ?? new();
        }
        catch (Exception)
        {
            // Corrupt or incompatible settings: fall back to defaults rather than crash a screen saver.
        }
        return new SaverSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
    }

    public SaverSettings Clone() =>
        JsonSerializer.Deserialize<SaverSettings>(JsonSerializer.Serialize(this, Json), Json)!;
}

/// <summary>
/// Offers named catalog stars as a drop-down, after "(none)". Not exclusive, so on Windows you can also type
/// "HIP 12345" for an unnamed star.
/// </summary>
public sealed class StarNameConverter : StringConverter
{
    private static readonly Lazy<string[]> Names = new(() =>
        new[] { SaverSettings.NoImaginaryHost }
            .Concat(CatalogLoader.LoadStars().Select(s => s.Name).OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
            .ToArray());

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) => new(Names.Value);
}

/// <summary>Offers catalog planet names as a drop-down (PropertyGrid on Windows, Spinner on Android); only listed names are accepted.</summary>
public sealed class PlanetNameConverter : StringConverter
{
    private static readonly Lazy<string[]> Names = new(() =>
        CatalogLoader.LoadExoplanets().Select(p => p.PlanetName).Order(StringComparer.OrdinalIgnoreCase).ToArray());

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) => new(Names.Value);
}
