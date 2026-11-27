namespace AlienSkies.Core.Astronomy;

/// <summary>Kepler's equation and the anomalies that come out of it.</summary>
public static class Kepler
{
    /// <summary>Eccentric anomaly E for mean anomaly M (radians, any range) by Newton's method.</summary>
    public static double EccentricAnomaly(double meanAnomaly, double e)
    {
        double M = System.Math.IEEERemainder(meanAnomaly, 2 * System.Math.PI);
        double E = e < 0.8 ? M : System.Math.PI * System.Math.Sign(M == 0 ? 1 : M);
        for (int i = 0; i < 30; i++)
        {
            double d = (E - e * System.Math.Sin(E) - M) / (1 - e * System.Math.Cos(E));
            E -= d;
            if (System.Math.Abs(d) < 1e-12) break;
        }
        return E;
    }

    /// <summary>True anomaly ν from the eccentric anomaly.</summary>
    public static double TrueAnomaly(double eccentricAnomaly, double e) =>
        2 * System.Math.Atan2(System.Math.Sqrt(1 + e) * System.Math.Sin(eccentricAnomaly / 2),
                              System.Math.Sqrt(1 - e) * System.Math.Cos(eccentricAnomaly / 2));
}
