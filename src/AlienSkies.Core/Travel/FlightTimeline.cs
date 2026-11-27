namespace AlienSkies.Core.Travel;

/// <summary>What the viewer sees during each part of a flight between two star systems.</summary>
public enum FlightStage
{
    /// <summary>The departure world's ground and daylight dissolve, leaving the same stars in black space.</summary>
    LiftOff,
    /// <summary>Still at the departure star, the view swings round to face the destination.</summary>
    TurnOut,
    /// <summary>Moving in a straight line; the stars re-project (and constellations distort) as the viewpoint moves.</summary>
    Cruise,
    /// <summary>Arrived, the view swings from the direction of travel to the arrival world's chosen view.</summary>
    TurnIn,
    /// <summary>The arrival world's ground and daylight fade in over the same stars.</summary>
    Landing,
}

/// <summary>
/// Pure timing for a flight, as functions of progress u in [0, 1] (elapsed / duration). The stages run back to back
/// and every quantity here is continuous across their boundaries, so the picture never jumps.
/// </summary>
public static class FlightTimeline
{
    public const double LiftOffEnd = 0.08, TurnOutEnd = 0.18, CruiseEnd = 0.82, TurnInEnd = 0.92;

    public static FlightStage StageAt(double u) => System.Math.Clamp(u, 0, 1) switch
    {
        < LiftOffEnd => FlightStage.LiftOff,
        < TurnOutEnd => FlightStage.TurnOut,
        < CruiseEnd => FlightStage.Cruise,
        < TurnInEnd => FlightStage.TurnIn,
        _ => FlightStage.Landing,
    };

    /// <summary>True while the departure world is the one the view relates to (before the midpoint).</summary>
    public static bool IsOutbound(double u) => u < 0.5;

    /// <summary>
    /// How much of a planet's surface view (ground, daylight, sun discs) is shown: 1 on the departure world at u = 0,
    /// 0 throughout the turns and the cruise, 1 again on the arrival world at u = 1.
    /// </summary>
    public static double SurfaceOpacity(double u)
    {
        u = System.Math.Clamp(u, 0, 1);
        if (u < LiftOffEnd) return 1 - SmoothStep(u / LiftOffEnd);
        if (u > TurnInEnd) return SmoothStep((u - TurnInEnd) / (1 - TurnInEnd));
        return 0;
    }

    /// <summary>
    /// How far the camera has turned from the planet's view (0) toward the direction of travel (1). It turns out after
    /// lift-off, faces forward for the whole cruise, and turns back in before landing.
    /// </summary>
    public static double HeadingBlend(double u)
    {
        u = System.Math.Clamp(u, 0, 1);
        if (u <= LiftOffEnd || u >= TurnInEnd) return 0;
        if (u < TurnOutEnd) return SmoothStep((u - LiftOffEnd) / (TurnOutEnd - LiftOffEnd));
        if (u <= CruiseEnd) return 1;
        return 1 - SmoothStep((u - CruiseEnd) / (TurnInEnd - CruiseEnd));
    }

    /// <summary>Fraction of the distance covered: 0 until the cruise starts, then a smooth start and stop, 1 after it.</summary>
    public static double Travelled(double u) => SmootherStep(CruiseFraction(u));

    /// <summary>
    /// Speed as a fraction of the cruise's top speed (reached halfway): the normalised derivative of
    /// <see cref="Travelled"/>, so the warp effect follows the actual motion.
    /// </summary>
    public static double SpeedFraction(double u)
    {
        double t = CruiseFraction(u);
        return 16 * t * t * (1 - t) * (1 - t);
    }

    private static double CruiseFraction(double u) =>
        System.Math.Clamp((u - TurnOutEnd) / (CruiseEnd - TurnOutEnd), 0, 1);

    public static double SmoothStep(double t)
    {
        t = System.Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    public static double SmootherStep(double t)
    {
        t = System.Math.Clamp(t, 0, 1);
        return t * t * t * (t * (6 * t - 15) + 10);
    }
}
