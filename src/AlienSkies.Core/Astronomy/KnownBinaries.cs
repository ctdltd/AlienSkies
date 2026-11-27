namespace AlienSkies.Core.Astronomy;

/// <summary>One star of a close binary, as measured from its eclipses and radial velocities.</summary>
/// <param name="LightFractionV">Share of the pair's combined V-band light. The archive's <c>sy_vmag</c> is for both stars together.</param>
public sealed record BinaryMember(string Name, double MassSolar, double RadiusSolar, double LightFractionV, double ColorBV);

/// <summary>
/// A close binary that a planet orbits as a whole, around the pair's centre of mass (a circumbinary or
/// "P-type" planet, like Kepler-16 b). The planet's orbit in the exoplanet archive is around that barycentre.
/// </summary>
public sealed record CircumbinaryHost(string HostName, BinaryMember Primary, BinaryMember Secondary, BinaryOrbit Orbit)
{
    public double TotalMassSolar => Primary.MassSolar + Secondary.MassSolar;
}

/// <summary>
/// Measured binary orbits the catalogs don't carry. The star catalog has no orbits at all, and the exoplanet
/// archive flags circumbinary planets but doesn't describe the pair they orbit, so both come from here.
/// Unlisted companions get a <see cref="BinaryOrbit.FaceOnCircle"/> stand-in.
/// </summary>
public static class KnownBinaries
{
    /// <summary>Visual orbits keyed by the stars' catalog ids (HIP numbers), primary first.</summary>
    private static readonly (string PrimaryId, string SecondaryId, BinaryOrbit Orbit, double TotalMassSolar)[] Visual =
    [
        // α Cen B around A. Pourbaix & Boyajian 2016 (a = 17.66″, P = 79.91 yr, periastron 1875.66 and again 2035.5),
        // scaled to AU with the catalog parallax of 747.1 mas so the pair sits where the star catalog puts it.
        // Masses 1.105 + 0.937 M☉. Separation runs from 11.2 AU (2035) to 36 AU (1995).
        ("71683", "71681", new BinaryOrbit(
            PeriodDays: 79.91 * BinaryOrbit.DaysPerJulianYear, SemiMajorAxisAu: 17.66 / 0.7471, Eccentricity: 0.524,
            InclinationDeg: 79.32, AscendingNodeDeg: 205.06, ArgumentOfPeriapsisDeg: 232.3,
            PeriapsisDays: BinaryOrbit.DaysSinceJ2000(1875.66)), 2.042),
    ];

    /// <summary>
    /// Circumbinary planet hosts. The ascending nodes on our sky aren't measurable from transits and are set to 0,
    /// the same default the planets get, so each pair is as nearly coplanar with its planet as measured.
    /// Light fractions are main-sequence estimates; both stars' radii and masses are measured.
    /// </summary>
    private static readonly CircumbinaryHost[] Circumbinary =
    [
        // Doyle et al. 2011: the first known circumbinary planet, "Tatooine". An orange K dwarf and a red dwarf, 41 days apart.
        new("Kepler-16",
            new BinaryMember("Kepler-16 A", 0.6897, 0.6489, 0.994, 1.15),
            new BinaryMember("Kepler-16 B", 0.20255, 0.22623, 0.006, 1.6),
            new BinaryOrbit(41.079220, 0.22431, 0.15944, 90.3401, 0, 263.464, PeriapsisDays: 0)),
        // Orosz et al. 2012: a Sun-like star and a red dwarf 7.4 days apart, with planet c in the habitable zone.
        new("Kepler-47",
            new BinaryMember("Kepler-47 A", 1.043, 0.964, 0.996, 0.68),
            new BinaryMember("Kepler-47 B", 0.362, 0.3506, 0.004, 1.5),
            new BinaryOrbit(7.44838, 0.0836, 0.0234, 89.34, 0, 212.3, PeriapsisDays: 0)),
        // Kostov et al. 2020: TESS's first circumbinary planet, found by a summer intern.
        new("TOI-1338",
            new BinaryMember("TOI-1338 A", 1.127, 1.299, 0.9988, 0.55),
            new BinaryMember("TOI-1338 B", 0.3128, 0.3134, 0.0012, 1.55),
            new BinaryOrbit(14.608559, 0.1288, 0.15603, 89.696, 0, 2.08, PeriapsisDays: 0)),
    ];

    /// <summary>The measured orbit of <paramref name="secondaryId"/> around <paramref name="primaryId"/>, either way round.</summary>
    public static (BinaryOrbit Orbit, double TotalMassSolar)? VisualOrbit(string primaryId, string secondaryId)
    {
        foreach (var v in Visual)
        {
            if (v.PrimaryId == primaryId && v.SecondaryId == secondaryId) return (v.Orbit, v.TotalMassSolar);
            if (v.PrimaryId == secondaryId && v.SecondaryId == primaryId) return (v.Orbit.Reversed(), v.TotalMassSolar);
        }
        return null;
    }

    /// <summary>The binary a planet's host name refers to, if it is a known circumbinary system ("Kepler-16" or "Kepler-16 AB").</summary>
    public static CircumbinaryHost? CircumbinaryFor(string hostName)
    {
        string name = hostName.Trim();
        if (name.EndsWith(" AB", StringComparison.OrdinalIgnoreCase)) name = name[..^3].TrimEnd();
        return Circumbinary.FirstOrDefault(c => c.HostName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<CircumbinaryHost> CircumbinaryHosts => Circumbinary;
}
