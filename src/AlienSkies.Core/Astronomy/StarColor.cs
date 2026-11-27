namespace AlienSkies.Core.Astronomy;

/// <summary>
/// Star colour from B−V: the sRGB colour of a black body at the star's temperature, white point D65
/// (the same method as Mitchell Charity's "What colour is a blackbody?" table), with an optional saturation boost.
/// </summary>
public static class StarColor
{
    public const double MinBV = -0.4, MaxBV = 2.0, Step = 0.05;

    /// <summary>
    /// sRGB (0–1, brightest channel 1) every 0.05 in B−V from −0.4 to 2.0. Temperature from B−V via
    /// <see cref="StellarEstimates.TemperatureFromBV"/>; Planck spectrum integrated against the CIE 1931 2° observer,
    /// converted to linear sRGB, normalised to its brightest channel and gamma-encoded.
    /// Kept as a constant span (not a static array) so NativeAOT can't trip over it.
    /// </summary>
    private static ReadOnlySpan<double> Table =>
    [
        0.6623, 0.7525, 1.0000, // -0.40  21707 K
        0.6780, 0.7640, 1.0000, // -0.35  18774 K
        0.6941, 0.7756, 1.0000, // -0.30  16602 K
        0.7107, 0.7875, 1.0000, // -0.25  14924 K
        0.7277, 0.7995, 1.0000, // -0.20  13585 K
        0.7451, 0.8117, 1.0000, // -0.15  12489 K
        0.7630, 0.8240, 1.0000, // -0.10  11573 K
        0.7814, 0.8365, 1.0000, // -0.05  10795 K
        0.8002, 0.8492, 1.0000, // +0.00  10125 K
        0.8194, 0.8620, 1.0000, // +0.05   9542 K
        0.8392, 0.8750, 1.0000, // +0.10   9028 K
        0.8594, 0.8882, 1.0000, // +0.15   8571 K
        0.8801, 0.9016, 1.0000, // +0.20   8163 K
        0.9013, 0.9151, 1.0000, // +0.25   7795 K
        0.9230, 0.9288, 1.0000, // +0.30   7462 K
        0.9452, 0.9426, 1.0000, // +0.35   7158 K
        0.9679, 0.9567, 1.0000, // +0.40   6880 K
        0.9911, 0.9709, 1.0000, // +0.45   6625 K
        1.0000, 0.9709, 0.9854, // +0.50   6389 K
        1.0000, 0.9623, 0.9623, // +0.55   6170 K
        1.0000, 0.9537, 0.9397, // +0.60   5968 K
        1.0000, 0.9452, 0.9177, // +0.65   5778 K
        1.0000, 0.9367, 0.8961, // +0.70   5602 K
        1.0000, 0.9284, 0.8750, // +0.75   5436 K
        1.0000, 0.9201, 0.8544, // +0.80   5281 K
        1.0000, 0.9119, 0.8343, // +0.85   5134 K
        1.0000, 0.9038, 0.8146, // +0.90   4996 K
        1.0000, 0.8958, 0.7953, // +0.95   4866 K
        1.0000, 0.8879, 0.7765, // +1.00   4743 K
        1.0000, 0.8800, 0.7580, // +1.05   4626 K
        1.0000, 0.8722, 0.7400, // +1.10   4515 K
        1.0000, 0.8645, 0.7223, // +1.15   4409 K
        1.0000, 0.8569, 0.7051, // +1.20   4309 K
        1.0000, 0.8494, 0.6882, // +1.25   4213 K
        1.0000, 0.8420, 0.6716, // +1.30   4121 K
        1.0000, 0.8347, 0.6554, // +1.35   4034 K
        1.0000, 0.8274, 0.6395, // +1.40   3950 K
        1.0000, 0.8202, 0.6240, // +1.45   3870 K
        1.0000, 0.8131, 0.6088, // +1.50   3794 K
        1.0000, 0.8061, 0.5938, // +1.55   3720 K
        1.0000, 0.7991, 0.5792, // +1.60   3649 K
        1.0000, 0.7923, 0.5648, // +1.65   3581 K
        1.0000, 0.7855, 0.5507, // +1.70   3516 K
        1.0000, 0.7787, 0.5369, // +1.75   3453 K
        1.0000, 0.7721, 0.5234, // +1.80   3392 K
        1.0000, 0.7655, 0.5101, // +1.85   3333 K
        1.0000, 0.7590, 0.4970, // +1.90   3277 K
        1.0000, 0.7526, 0.4841, // +1.95   3222 K
        1.0000, 0.7462, 0.4715, // +2.00   3169 K
    ];

    /// <summary>
    /// Colour for a B−V index, each channel 0–1. <paramref name="boost"/> scales saturation around the colour's own
    /// luma: 1 is the true black-body colour (star colours are subtle: the Sun is nearly white), 0 is grey-white,
    /// and values above 1 exaggerate the tint the way long-exposure photographs do.
    /// </summary>
    public static (double R, double G, double B) FromBV(double bv, double boost = 1)
    {
        double x = (System.Math.Clamp(double.IsFinite(bv) ? bv : 0.65, MinBV, MaxBV) - MinBV) / Step;
        int i = System.Math.Min((int)x, Table.Length / 3 - 2);
        double f = x - i;
        var t = Table;
        double r = t[3 * i] + (t[3 * i + 3] - t[3 * i]) * f;
        double g = t[3 * i + 1] + (t[3 * i + 4] - t[3 * i + 1]) * f;
        double b = t[3 * i + 2] + (t[3 * i + 5] - t[3 * i + 2]) * f;
        if (boost != 1)
        {
            boost = System.Math.Clamp(boost, 0, 4);
            double y = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            r = System.Math.Max(0, y + (r - y) * boost);
            g = System.Math.Max(0, y + (g - y) * boost);
            b = System.Math.Max(0, y + (b - y) * boost);
            double max = System.Math.Max(r, System.Math.Max(g, b));
            if (max > 0) (r, g, b) = (r / max, g / max, b / max);
        }
        return (r, g, b);
    }
}
