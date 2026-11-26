namespace AlienSkies.Core.Astronomy;

/// <summary>
/// Rough physical properties of a star from what a catalog row gives us (absolute V and B−V).
/// Good enough to size and place an imaginary planet; not a substitute for measured values.
/// Mass assumes a main-sequence star, so it comes out too high for giants (Arcturus ≈ 4 M☉ instead of 1.1).
/// </summary>
public static class StellarEstimates
{
    /// <summary>Estimated physical properties, all in solar units except temperature.</summary>
    public readonly record struct Properties(double TemperatureK, double LuminositySolar, double MassSolar, double RadiusSolar);

    /// <summary>
    /// Best guess at a star's temperature, luminosity, mass and radius from absolute V and B−V.
    /// Red dwarfs get their own path: B−V stops tracking temperature below ≈3500 K, which would make
    /// Proxima Centauri come out half its real size and its habitable zone too close in.
    /// </summary>
    public static Properties Estimate(double absoluteV, double bv)
    {
        double t = TemperatureFromBV(bv);
        if (absoluteV >= 9 && bv >= 1.3)
        {
            double m = RedDwarfMassSolar(absoluteV);
            double l = 0.23 * System.Math.Pow(m, 2.3);
            return new Properties(t, l, m, m); // R ≈ M for fully convective dwarfs (Proxima 0.12 M☉, 0.15 R☉)
        }
        double lum = LuminositySolar(absoluteV, bv);
        return new Properties(t, lum, MassSolar(lum), RadiusSolar(lum, t));
    }

    /// <summary>Delfosse et al. 2000 V-band mass–luminosity relation for M dwarfs (valid M_V 9–17).</summary>
    public static double RedDwarfMassSolar(double absoluteV)
    {
        double v = System.Math.Clamp(absoluteV, 9, 17);
        double log = 1e-3 * (0.3 + 1.87 * v + 7.614 * v * v - 1.698 * v * v * v + 0.060958 * v * v * v * v);
        return System.Math.Clamp(System.Math.Pow(10, log), 0.08, 0.7);
    }

    public const double SunTemperatureK = 5772;
    public const double SunAbsoluteBolometric = 4.74;

    /// <summary>Effective temperature from B−V (Ballesteros 2012, black-body fit). Sun (0.65) → ≈5780 K.</summary>
    public static double TemperatureFromBV(double bv)
    {
        bv = System.Math.Clamp(bv, -0.4, 2.2);
        return 4600 * (1 / (0.92 * bv + 1.7) + 1 / (0.92 * bv + 0.62));
    }

    /// <summary>
    /// Bolometric correction BC_V for a temperature (Flower 1996 polynomials as corrected by Torres 2010).
    /// Matters most for red dwarfs, which put most of their light out in the infrared.
    /// </summary>
    public static double BolometricCorrection(double temperatureK)
    {
        double x = System.Math.Log10(System.Math.Clamp(temperatureK, 2500, 50_000));
        double[] c = x < 3.70
            ? [-0.190537291496456e5, 0.155144866764412e5, -0.421278819301717e4, 0.381476328422343e3]
            : x < 3.90
                ? [-0.370510203809015e5, 0.385672629965804e5, -0.150651486316025e5, 0.261724637119416e4, -0.170623810323864e3]
                : [-0.118115450538963e6, 0.137145973583929e6, -0.636233812100225e5, 0.147412923562646e5, -0.170587278406872e4, 0.788731721804990e2];
        double bc = 0, p = 1;
        foreach (var k in c) { bc += k * p; p *= x; }
        return bc;
    }

    /// <summary>Bolometric luminosity in solar units.</summary>
    public static double LuminositySolar(double absoluteV, double bv)
    {
        double bolometric = absoluteV + BolometricCorrection(TemperatureFromBV(bv));
        return System.Math.Pow(10, -0.4 * (bolometric - SunAbsoluteBolometric));
    }

    /// <summary>Radius from Stefan–Boltzmann: L = R² T⁴ in solar units. Works for giants too.</summary>
    public static double RadiusSolar(double luminositySolar, double temperatureK) =>
        System.Math.Sqrt(luminositySolar) * System.Math.Pow(SunTemperatureK / temperatureK, 2);

    /// <summary>Main-sequence mass–luminosity relation (piecewise), clamped to 0.08–50 M☉.</summary>
    public static double MassSolar(double luminositySolar)
    {
        double l = luminositySolar;
        double m = l < 0.033 ? System.Math.Pow(l / 0.23, 1 / 2.3)   // below ≈0.43 M☉: L = 0.23 M^2.3
                 : l < 16 ? System.Math.Pow(l, 0.25)                 // 0.43–2 M☉: L = M^4
                 : System.Math.Pow(l / 1.4, 1 / 3.5);                // above 2 M☉: L = 1.4 M^3.5
        return System.Math.Clamp(m, 0.08, 50);
    }

    /// <summary>Distance at which the planet gets the same starlight Earth gets from the Sun.</summary>
    public static double EarthEquivalentDistanceAu(double luminositySolar) => System.Math.Sqrt(luminositySolar);
}
