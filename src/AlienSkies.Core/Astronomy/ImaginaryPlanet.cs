using System.ComponentModel;
using System.Text.Json.Serialization;
using AlienSkies.Core.Catalog;

namespace AlienSkies.Core.Astronomy;

/// <summary>
/// A user-made planet around any catalog star, saved in settings. Each one carries its own orbit and spin;
/// zeros mean "work it out from the star". Edited in the Windows PropertyGrid's collection editor and in the
/// Android settings screen, both generated from these attributes.
/// </summary>
public sealed class ImaginaryPlanet
{
    [Category("Planet"), DisplayName("Host star"), TypeConverter(typeof(StarNameConverter))]
    [Description("Any catalog star, by name (or \"HIP 12345\" on Windows).")]
    public string HostStar { get; set; } = "Tau Ceti";

    [Category("Planet"), DisplayName("Name")]
    [Description("Shown in the Planet list and the info overlay. Blank = \"<star> (imaginary)\".")]
    public string Name { get; set; } = "";

    [Category("Orbit"), DisplayName("Orbital radius (AU)")]
    [Description("0 = where it gets the same starlight Earth gets from the Sun.")]
    public double OrbitalRadiusAu { get; set; }

    [Category("Orbit"), DisplayName("Orbital period (days)")]
    [Description("0 = from Kepler's third law and the star's estimated mass (recommended).")]
    public double OrbitalPeriodDays { get; set; }

    [Category("Orbit"), DisplayName("Eccentricity")]
    public double Eccentricity { get; set; }

    [Category("Orbit"), DisplayName("Inclination (°)")]
    [Description("Measured from Earth: 0 = face-on to us, 90 = edge-on, which puts our Sun in its zodiac.")]
    public double InclinationDeg { get; set; } = 90;

    [Category("Spin"), DisplayName("Rotation")]
    public RotationMode RotationMode { get; set; } = RotationMode.Auto;

    [Category("Spin"), DisplayName("Rotation period (hours)")]
    [Description("Day length when not tidally locked. Negative = retrograde.")]
    public double RotationPeriodHours { get; set; } = 24;

    [Category("Spin"), DisplayName("Axial tilt (°)")]
    [Description("Earth is 23.44°.")]
    public double ObliquityDeg { get; set; } = 23.44;

    /// <summary>The name used in the Planet list, the overlay and lookups.</summary>
    [Browsable(false), JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"{HostStar.Trim()} (imaginary)" : Name.Trim();

    public override string ToString() => DisplayName;

    /// <summary>The planet as a system, or null when the host star isn't in the catalog.</summary>
    public ExoplanetSystem? ToSystem(IEnumerable<Star> catalog) =>
        Star.Find(catalog, HostStar) is { } host ? ExoplanetSystem.Imaginary(host, DisplayName) : null;

    /// <summary>Full orbit and spin for an observer at the given spot, or null when the host star isn't in the catalog.</summary>
    public PlanetConfig? ToConfig(IEnumerable<Star> catalog, double latitudeDeg, double longitudeDeg)
    {
        if (ToSystem(catalog) is not { } system) return null;
        double mass = system.HostMassSolar ?? 1.0;
        double a = OrbitalRadiusAu > 0 ? OrbitalRadiusAu : system.SemiMajorAxisAu ?? 1.0;
        double period = OrbitalPeriodDays > 0 ? OrbitalPeriodDays : PlanetConfig.KeplerPeriodDays(a, mass);
        bool locked = RotationMode switch
        {
            RotationMode.TidallyLocked => true,
            RotationMode.Free => false,
            _ => a < 0.1,
        };
        return new PlanetConfig
        {
            Planet = system with { SemiMajorAxisAu = a, OrbitalPeriodDays = period },
            SemiMajorAxisAu = a,
            OrbitalPeriodDays = period,
            Eccentricity = System.Math.Clamp(Eccentricity, 0, 0.99),
            InclinationDeg = InclinationDeg,
            ObliquityDeg = ObliquityDeg,
            TidallyLocked = locked,
            RotationPeriodHours = RotationPeriodHours == 0 ? 24 : RotationPeriodHours,
            LatitudeDeg = latitudeDeg,
            LongitudeDeg = longitudeDeg,
        };
    }
}
