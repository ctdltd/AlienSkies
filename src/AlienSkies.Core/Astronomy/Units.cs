namespace AlienSkies.Core.Astronomy;

public static class Units
{
    public const double Deg = System.Math.PI / 180.0;
    public const double AuPerParsec = 206_264.806;
    public const double ParsecsPerAu = 1.0 / AuPerParsec;
    public const double LightYearsPerParsec = 3.261_563_78;
    public const double SolarRadiusAu = 0.004_650_47;

    /// <summary>Absolute visual magnitude of the Sun.</summary>
    public const double SunAbsoluteV = 4.83;

    /// <summary>Distance modulus: apparent = absolute + DistanceModulus(d).</summary>
    public static double DistanceModulus(double distanceParsecs) =>
        5.0 * System.Math.Log10(distanceParsecs) - 5.0;

    public static double AbsoluteMagnitude(double apparent, double distanceParsecs) =>
        apparent - DistanceModulus(distanceParsecs);

    public static double ApparentMagnitude(double absolute, double distanceParsecs) =>
        absolute + DistanceModulus(distanceParsecs);

    public static double NormalizeRadians(double a)
    {
        a %= 2 * System.Math.PI;
        return a < 0 ? a + 2 * System.Math.PI : a;
    }
}
