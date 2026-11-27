using AlienSkies.Core.Math;

namespace AlienSkies.Core.Astronomy;

/// <summary>
/// A hand-built map of the Milky Way's surface brightness as seen from Earth, in galactic coordinates.
/// Not a survey: a smooth disc, the bulge and the bright star clouds, the main dark lanes (the Great Rift,
/// the Coalsack, the Ophiuchus clouds), the Magellanic Clouds, and fractal noise for clumpiness. It is meant
/// to read correctly to the eye at screen-saver resolution. Other viewers see it through
/// <see cref="Galactic.EarthDirectionOf"/>.
/// </summary>
public static class MilkyWayModel
{
    /// <summary>A bright (or dark) patch: Gaussian in longitude and latitude, in degrees.</summary>
    private readonly record struct Patch(double L, double B, double SigmaL, double SigmaB, double Amount, double Warmth = 0.3);

    private static readonly Patch[] Bright =
    [
        new(0, -3, 11, 7, 0.80, 1.0),   // the bulge, behind the Sagittarius star cloud
        new(4, -5, 4, 3, 0.35, 0.9),    // Large Sagittarius star cloud (Baade's window region)
        new(27, -2, 5, 3.5, 0.35, 0.6), // Scutum star cloud
        new(78, 1.5, 12, 5, 0.32, 0.3), // Cygnus star cloud
        new(287, -1, 8, 4, 0.32, 0.25), // Carina
        new(318, -1, 15, 4, 0.22, 0.35),// Centaurus / Norma
        new(125, 0, 30, 6, 0.10, 0.2),  // Cassiopeia / Perseus
    ];

    private static readonly Patch[] Clouds =
    [
        new(280.5, -32.9, 3.5, 2.6, 0.32, 0.25), // Large Magellanic Cloud
        new(302.8, -44.3, 1.8, 1.2, 0.20, 0.25), // Small Magellanic Cloud
    ];

    private static readonly Patch[] Dark =
    [
        new(301, -1, 2.5, 2.5, 0.75),  // Coalsack
        new(355, 16, 6, 5, 0.45),      // ρ Ophiuchi clouds
        new(357, 7, 4, 3, 0.45),       // Pipe Nebula
    ];

    /// <summary>
    /// Brightness (0 for empty sky, about 1 at the brightest part of the bulge) and warmth (0 for the blue-white
    /// disc, 1 for the yellowish bulge) at a galactic longitude and latitude in degrees, as seen from Earth.
    /// </summary>
    public static (double Intensity, double Warmth) Sample(double lDeg, double bDeg)
    {
        double l = Wrap180(lDeg);
        double b = System.Math.Clamp(bDeg, -90, 90);

        // The disc: brighter toward the centre, Gaussian in latitude, a little thicker toward the bulge.
        double toCentre = 0.5 + 0.5 * System.Math.Cos(l * Units.Deg);
        double height = 3.8 + 1.5 * toCentre;
        double disc = (0.22 + 0.28 * toCentre) * Gauss(b, height);
        double halo = 0.05 * Gauss(b, 16) * (0.4 + 0.6 * toCentre); // the faint glow either side of the band

        double intensity = disc + halo, warmWeight = (disc + halo) * 0.3;
        foreach (var p in Bright)
        {
            double v = p.Amount * Patchiness(l, b, p);
            intensity += v;
            warmWeight += v * p.Warmth;
        }
        double warmth = intensity > 1e-6 ? warmWeight / intensity : 0.3;

        // Dust: the Great Rift runs from Cygnus (l ≈ 80°) through Aquila to Ophiuchus, rising above the plane near the centre.
        double riftAlong = SmoothStep(-35, -15, l) * (1 - SmoothStep(65, 88, l));
        double riftB = 1.2 + System.Math.Max(0, 12 - l) * 0.3;
        double dust = 0.78 * riftAlong * Gauss(b - riftB, 2.2 + System.Math.Max(0, 12 - l) * 0.08);
        dust += 0.35 * Gauss(b + 0.5, 0.9) * (0.4 + 0.6 * toCentre); // thin mid-plane lane all along the band
        foreach (var p in Dark) dust += p.Amount * Patchiness(l, b, p);

        var dir = Galactic.FromLB(lDeg, bDeg);
        double clumps = 0.45 + 1.1 * Fbm(dir * 11, 5);
        double dustNoise = Fbm(dir * 24 + new Vector3d(17, 3, 5), 4);
        dust *= 0.5 + 1.0 * dustNoise;
        intensity *= clumps * System.Math.Max(0, 1 - System.Math.Min(dust, 0.95));

        foreach (var c in Clouds)
        {
            double v = c.Amount * Patchiness(l, b, c) * (0.7 + 0.6 * Fbm(dir * 40, 2));
            if (v > 1e-4)
            {
                warmth = (warmth * intensity + c.Warmth * v) / (intensity + v);
                intensity += v;
            }
        }
        return (System.Math.Clamp(intensity, 0, 1.2), System.Math.Clamp(warmth, 0, 1));
    }

    private static double Patchiness(double l, double b, Patch p)
    {
        double dl = Wrap180(l - p.L) * System.Math.Cos(b * Units.Deg);
        return System.Math.Exp(-0.5 * (dl * dl / (p.SigmaL * p.SigmaL) + (b - p.B) * (b - p.B) / (p.SigmaB * p.SigmaB)));
    }

    private static double Gauss(double x, double sigma) => System.Math.Exp(-0.5 * x * x / (sigma * sigma));

    private static double Wrap180(double deg)
    {
        deg %= 360;
        if (deg > 180) deg -= 360;
        if (deg <= -180) deg += 360;
        return deg;
    }

    private static double SmoothStep(double e0, double e1, double x)
    {
        double t = System.Math.Clamp((x - e0) / (e1 - e0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    // ---- noise ---------------------------------------------------------------
    // 3-D value noise sampled on the unit sphere, so there is no seam at l = 0 and no pinching at the poles.

    /// <summary>Fractal value noise in about [0, 1].</summary>
    private static double Fbm(Vector3d p, int octaves)
    {
        double sum = 0, amp = 0.5, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            sum += amp * ValueNoise(p);
            norm += amp;
            p = p * 2.03 + new Vector3d(1.7, 9.2, 4.1);
            amp *= 0.5;
        }
        return sum / norm;
    }

    private static double ValueNoise(Vector3d p)
    {
        int xi = (int)System.Math.Floor(p.X), yi = (int)System.Math.Floor(p.Y), zi = (int)System.Math.Floor(p.Z);
        double xf = p.X - xi, yf = p.Y - yi, zf = p.Z - zi;
        double u = Fade(xf), v = Fade(yf), w = Fade(zf);

        double Lerp(double a, double b, double t) => a + (b - a) * t;
        double c00 = Lerp(Hash(xi, yi, zi), Hash(xi + 1, yi, zi), u);
        double c10 = Lerp(Hash(xi, yi + 1, zi), Hash(xi + 1, yi + 1, zi), u);
        double c01 = Lerp(Hash(xi, yi, zi + 1), Hash(xi + 1, yi, zi + 1), u);
        double c11 = Lerp(Hash(xi, yi + 1, zi + 1), Hash(xi + 1, yi + 1, zi + 1), u);
        return Lerp(Lerp(c00, c10, v), Lerp(c01, c11, v), w);
    }

    private static double Fade(double t) => t * t * (3 - 2 * t);

    private static double Hash(int x, int y, int z)
    {
        unchecked
        {
            uint h = (uint)x * 374_761_393u + (uint)y * 668_265_263u + (uint)z * 2_147_483_647u;
            h = (h ^ (h >> 13)) * 1_274_126_177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (double)0xFFFFFF;
        }
    }
}
