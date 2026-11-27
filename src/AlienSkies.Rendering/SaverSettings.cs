using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Catalog;

namespace AlienSkies.Rendering;

/// <summary>
/// User-facing configuration, edited in a PropertyGrid and stored as JSON in
/// %APPDATA%\AlienSkies\settings.json. Category prefixes keep the grid in a sensible order.
/// </summary>
public sealed class SaverSettings
{
    // ---- Planet ------------------------------------------------------------
    [Category("1. Planet"), DisplayName("Planet"), TypeConverter(typeof(PlanetNameConverter))]
    [Description("Planet to stand on: one of yours from Imaginary planets, or any planet in the exoplanet catalog.")]
    public string PlanetName { get; set; } = "alf Cen A (hypothetical Earth)";

    [Category("1. Planet"), DisplayName("Imaginary planets")]
    [Description("Your own made-up planets around any catalog star, each with its own orbit and spin. They appear in the Planet list, and tour mode can visit them.")]
    public List<ImaginaryPlanet> ImaginaryPlanets { get; set; } = [];

    /// <summary>The imaginary planet chosen as <see cref="PlanetName"/>, if any.</summary>
    public ImaginaryPlanet? SelectedImaginaryPlanet() =>
        ImaginaryPlanets.FirstOrDefault(p => p.DisplayName.Equals(PlanetName?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Keeps the selection on an imaginary planet that was renamed while editing: <paramref name="selected"/> and
    /// <paramref name="oldName"/> are what <see cref="SelectedImaginaryPlanet"/> returned before the edit.
    /// </summary>
    public void FollowRename(ImaginaryPlanet? selected, string? oldName)
    {
        if (selected is not null && ImaginaryPlanets.Contains(selected) &&
            string.Equals(PlanetName?.Trim(), oldName, StringComparison.OrdinalIgnoreCase))
            PlanetName = selected.DisplayName;
    }

    [Category("1. Planet"), DisplayName("Use catalog orbit")]
    [Description("Catalog planets only (imaginary planets have their own orbit). True: orbit values come from the exoplanet catalog, unknowns get defaults. False: use the overrides below.")]
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
    [Description("Catalog planets only (imaginary planets have their own spin). Tidally locked worlds keep their star fixed in the sky; the stars still wheel past once per orbit.")]
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

    [Category("4. View"), DisplayName("Star colour boost")]
    [Description("1 = true star colours (black-body, subtle: the Sun is nearly white). Higher values (up to 3) exaggerate the tints the way long-exposure photos do; 0 = white.")]
    public double ColorBoost { get; set; } = 1.5;

    [Category("4. View"), DisplayName("Atmosphere / daylight")]
    [Description("Brighten the sky and wash out stars when the host star is up. Off = airless moon.")]
    public bool Atmosphere { get; set; } = true;

    [Category("4. View"), DisplayName("Show ground")]
    public bool ShowGround { get; set; } = true;

    [Category("4. View"), DisplayName("Show Earth's constellations")]
    [Description("Draw Earth's constellation figures so you can watch them distort with distance.")]
    public bool ShowConstellations { get; set; } = true;

    [Category("4. View"), DisplayName("Show the Milky Way")]
    [Description("Draw the band of the Milky Way as it looks from this planet. From any nearby star it is almost the same as from Earth; from distant systems it shifts.")]
    public bool ShowMilkyWay { get; set; } = true;

    [Category("4. View"), DisplayName("Milky Way brightness")]
    [Description("1 = a dark country sky. Lower it for a subtler band, raise it (up to 3) for a long-exposure look.")]
    public double MilkyWayBrightness { get; set; } = 1.0;

    [Category("4. View"), DisplayName("Milky Way style")]
    [Description("Soft tint shades where the band is, brighter towards its middle, and is cheap enough for phones. Detailed adds the dust lanes and star clouds but costs several times as much per frame.")]
    public MilkyWayStyle MilkyWayStyle { get; set; } = MilkyWayStyle.Tint;

    [Category("4. View"), DisplayName("Show sibling planets")]
    [Description("Show the other planets of the same system as bright wandering points, with their phases when they are close enough to show a disc.")]
    public bool ShowSiblingPlanets { get; set; } = true;

    [Category("4. View"), DisplayName("Label stars brighter than")]
    [Description("Magnitude threshold for name labels. Set below -30 to turn labels off.")]
    public double LabelMagnitudeLimit { get; set; } = 1.5;

    [Category("4. View"), DisplayName("Mark our Sun")]
    public bool MarkSol { get; set; } = true;

    [Category("4. View"), DisplayName("Show info overlay")]
    [Description("Planet name, distance from Earth, the suns, the sibling planets, and where our Sun is, with an arrow at the screen edge when it is out of view.")]
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
    [Description("Visit a random planet (and random latitude) at a fixed interval.")]
    public bool TourMode { get; set; }

    [Category("6. Tour"), DisplayName("Include imaginary planets")]
    [Description("Let the tour visit your imaginary planets as well as catalog ones.")]
    public bool TourIncludesImaginary { get; set; } = true;

    [Category("6. Tour"), DisplayName("Tour interval (minutes)")]
    public double TourIntervalMinutes { get; set; } = 5;

    [Category("6. Tour"), DisplayName("Fly between stars")]
    [Description("Travel to each new planet instead of cutting to it: lift off, fly through space while the stars and constellations shift around you, and land.")]
    public bool TourFlights { get; set; } = true;

    [Category("6. Tour"), DisplayName("Flight length (seconds)")]
    [Description("How long each flight takes, lift-off to landing. The distance doesn't matter: a hop to the next star and a trip across the catalog take the same time.")]
    public double FlightSeconds { get; set; } = 30;

    [Category("6. Tour"), DisplayName("Near-light-speed look")]
    [Description("While flying, crowd the stars toward the way you're heading and tint them blue ahead and red behind, as they would look close to the speed of light.")]
    public bool FlightWarp { get; set; } = true;

    // ---- Persistence -------------------------------------------------------
    // Source-generated (see SaverSettingsJsonContext below) so the NativeAOT macOS build can read and write it too.
    private static JsonTypeInfo<SaverSettings> Json => SaverSettingsJsonContext.Default.SaverSettings;

    public static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlienSkies", "settings.json");

    public static SaverSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath), Json) ?? new();
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
        JsonSerializer.Deserialize(JsonSerializer.Serialize(this, Json), Json)!;
}

/// <summary>settings.json format: indented, enums as names. New SaverSettings properties are picked up automatically.</summary>
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SaverSettings))]
internal sealed partial class SaverSettingsJsonContext : JsonSerializerContext;

/// <summary>
/// Offers the user's imaginary planets, then catalog planet names, as a drop-down (PropertyGrid on Windows, Spinner on
/// Android); only listed names are accepted. The imaginary ones come from the settings being edited (the context's instance).
/// </summary>
public sealed class PlanetNameConverter : StringConverter
{
    private static readonly Lazy<string[]> Names = new(() =>
        CatalogLoader.LoadExoplanets().Select(p => p.PlanetName).Order(StringComparer.OrdinalIgnoreCase).ToArray());

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;
    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        var mine = (context?.Instance as SaverSettings)?.ImaginaryPlanets.Select(p => p.DisplayName) ?? [];
        return new(mine.Concat(Names.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}

/// <summary>Minimal <see cref="ITypeDescriptorContext"/> that just names the object being edited (for hosts without a PropertyGrid).</summary>
public sealed class InstanceContext(object instance) : ITypeDescriptorContext
{
    public object Instance => instance;
    public IContainer? Container => null;
    public PropertyDescriptor? PropertyDescriptor => null;
    public bool OnComponentChanging() => true;
    public void OnComponentChanged() { }
    public object? GetService(Type serviceType) => null;
}
