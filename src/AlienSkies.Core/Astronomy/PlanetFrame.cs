using AlienSkies.Core.Math;

namespace AlienSkies.Core.Astronomy;

/// <summary>Local horizon basis for an observer on the planet, expressed as ICRS-aligned unit vectors.</summary>
public readonly record struct HorizonBasis(Vector3d Zenith, Vector3d North, Vector3d East)
{
    /// <summary>Altitude and azimuth (azimuth from north through east), both in radians.</summary>
    public (double Altitude, double Azimuth) ToHorizontal(Vector3d direction)
    {
        double up = Vector3d.Dot(direction, Zenith);
        double alt = System.Math.Asin(System.Math.Clamp(up, -1, 1));
        double az = Units.NormalizeRadians(System.Math.Atan2(Vector3d.Dot(direction, East), Vector3d.Dot(direction, North)));
        return (alt, az);
    }

    /// <summary>Direction as (east, north, up) components — the camera works in this local frame.</summary>
    public Vector3d ToLocal(Vector3d direction) =>
        new(Vector3d.Dot(direction, East), Vector3d.Dot(direction, North), Vector3d.Dot(direction, Zenith));
}

/// <summary>
/// Geometry of a planet's orbit and spin, built once from a <see cref="PlanetConfig"/>
/// and evaluated at any time t (days since epoch).
/// All axes are ICRS-aligned, so the same star directions can be used throughout.
/// </summary>
public sealed class PlanetFrame
{
    private readonly PlanetConfig _c;
    private readonly Vector3d _node, _inPlane, _orbitNormal; // orbit basis
    private readonly Vector3d _pole, _eq1, _eq2;             // body equator basis
    private readonly double _rotationPeriodDays, _rotationPhase0;

    public PlanetConfig Config => _c;
    public Vector3d OrbitNormal => _orbitNormal;
    public Vector3d SpinPole => _pole;

    public PlanetFrame(PlanetConfig config)
    {
        _c = config;
        var (los, north, east) = Equatorial.SkyPlaneBasis(config.Planet.RaDeg, config.Planet.DecDeg);

        double node = config.AscendingNodeDeg * Units.Deg, inc = config.InclinationDeg * Units.Deg;
        _node = (north * System.Math.Cos(node) + east * System.Math.Sin(node)).Normalized();
        var perp = (-north * System.Math.Sin(node) + east * System.Math.Cos(node)).Normalized();
        // i = 0: orbit normal points back toward Earth (face-on). i = 90°: normal lies in the sky plane.
        _orbitNormal = (-los * System.Math.Cos(inc) - perp * System.Math.Sin(inc)).Normalized();
        _inPlane = Vector3d.Cross(_orbitNormal, _node).Normalized();

        double obl = config.ObliquityDeg * Units.Deg, oblAz = config.ObliquityAzimuthDeg * Units.Deg;
        _pole = (_orbitNormal * System.Math.Cos(obl)
               + (_node * System.Math.Cos(oblAz) + _inPlane * System.Math.Sin(oblAz)) * System.Math.Sin(obl)).Normalized();

        // Equatorial reference axis: the node direction projected onto the body's equator.
        var e1 = _node - _pole * Vector3d.Dot(_node, _pole);
        if (e1.Length < 1e-6) e1 = _inPlane - _pole * Vector3d.Dot(_inPlane, _pole);
        _eq1 = e1.Normalized();
        _eq2 = Vector3d.Cross(_pole, _eq1).Normalized();

        if (config.TidallyLocked)
        {
            _rotationPeriodDays = config.OrbitalPeriodDays;
            _rotationPhase0 = System.Math.PI + config.ArgumentOfPeriapsisDeg * Units.Deg; // longitude 0 faces the star
        }
        else
        {
            _rotationPeriodDays = config.RotationPeriodHours / 24.0;
            _rotationPhase0 = 0;
        }
    }

    /// <summary>Planet position relative to its host star, in AU (Keplerian, no perturbations).</summary>
    public Vector3d PlanetOffsetAu(double tDays)
    {
        double e = _c.Eccentricity, a = _c.SemiMajorAxisAu;
        double meanAnomaly = 2 * System.Math.PI * tDays / _c.OrbitalPeriodDays;
        double E = SolveKepler(meanAnomaly, e);
        double x = a * (System.Math.Cos(E) - e);
        double y = a * System.Math.Sqrt(1 - e * e) * System.Math.Sin(E);
        double w = _c.ArgumentOfPeriapsisDeg * Units.Deg;
        var px = _node * System.Math.Cos(w) + _inPlane * System.Math.Sin(w);
        var py = Vector3d.Cross(_orbitNormal, px);
        return px * x + py * y;
    }

    /// <summary>Unit vector from the planet toward its host star, and the distance in AU.</summary>
    public (Vector3d Direction, double DistanceAu) HostStarFromPlanet(double tDays)
    {
        var offset = PlanetOffsetAu(tDays);
        return ((-offset).Normalized(), offset.Length);
    }

    public HorizonBasis Horizon(double tDays)
    {
        double lat = _c.LatitudeDeg * Units.Deg;
        double spin = _rotationPhase0 + 2 * System.Math.PI * tDays / _rotationPeriodDays + _c.LongitudeDeg * Units.Deg;

        var zenith = (_eq1 * System.Math.Cos(spin) + _eq2 * System.Math.Sin(spin)) * System.Math.Cos(lat)
                   + _pole * System.Math.Sin(lat);
        var east = Vector3d.Cross(_pole, zenith);
        east = east.Length < 1e-9 ? _eq2 : east.Normalized(); // standing on a pole: pick any "east"
        var north = Vector3d.Cross(zenith, east).Normalized();
        return new HorizonBasis(zenith.Normalized(), north, east);
    }

    private static double SolveKepler(double M, double e)
    {
        double E = e < 0.8 ? M : System.Math.PI;
        for (int i = 0; i < 12; i++)
        {
            double d = (E - e * System.Math.Sin(E) - M) / (1 - e * System.Math.Cos(E));
            E -= d;
            if (System.Math.Abs(d) < 1e-12) break;
        }
        return E;
    }
}
