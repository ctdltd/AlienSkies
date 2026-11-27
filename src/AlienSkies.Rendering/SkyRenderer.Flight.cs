using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Math;
using AlienSkies.Core.Travel;
using SkiaSharp;

namespace AlienSkies.Rendering;

/// <summary>
/// Flights between star systems (tour mode). The camera leaves the departure planet's own view, turns to face the
/// destination, flies there while every star is re-projected from the moving viewpoint, then turns to the arrival
/// planet's view. Both ends use exactly the camera and stars of the surface view, so the ground and daylight can simply
/// be dissolved in and out over them.
/// </summary>
public sealed partial class SkyRenderer
{
    private static readonly SKColor DestinationMark = new(140, 220, 255);

    private void DrawFlight(SKCanvas canvas, SKSizeI size, TourFlight flight, SaverSettings s,
                            double azimuthOffsetDeg, float density)
    {
        double u = flight.Progress;
        var path = flight.Path;
        var planetView = SurfaceCameraBasis(flight.Nearest, s, azimuthOffsetDeg);
        var cruiseView = FlightOrientation.Cruise(path.Heading, azimuthOffsetDeg * Units.Deg);
        var view = FlightOrientation.Blend(planetView, cruiseView, FlightTimeline.HeadingBlend(u));
        var cam = new Camera(size, 0, 0, s.FieldOfViewDeg); // looks along view.North
        flight.Sky.MoveSuns(flight.From.TimeDays, flight.To.TimeDays);
        var seen = flight.Sky.Observe(flight.Position, path.Heading, flight.Beta(s.FlightWarp));
        double limit = s.LimitingMagnitude;
        double space = 1 - FlightTimeline.SurfaceOpacity(u);

        canvas.Clear(NightSky);
        if (s.ShowConstellations)
        {
            _stroke.Color = new SKColor(110, 150, 220, 90);
            _stroke.StrokeWidth = 1f * density;
            foreach (var (a, b) in flight.ConstellationLines)
            {
                if (seen[a].ApparentMagnitude > limit || seen[b].ApparentMagnitude > limit) continue;
                if (cam.Project(view.ToLocal(seen[a].Direction)) is SKPoint pa &&
                    cam.Project(view.ToLocal(seen[b].Direction)) is SKPoint pb) canvas.DrawLine(pa, pb, _stroke);
            }
        }
        DrawFlightStars(canvas, flight, seen, view, cam, s, limit, density);
        if (s.ShowInfo && space > 0) DrawFlightInfo(canvas, size, flight, path, seen, view, cam, space, s.DriftInfo, density);

        // Lift-off and landing: the surface view, drawn with the very same camera, fading over the space view.
        double surface = 1 - space;
        if (surface > 0)
        {
            using var layer = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(surface * 255)) };
            canvas.SaveLayer(layer);
            DrawSurface(canvas, size, flight.Nearest, s, azimuthOffsetDeg, density);
            canvas.Restore();
        }
    }

    /// <summary>The surface view's camera (look azimuth and altitude on the planet's horizon), as an ICRS-aligned basis.</summary>
    private static HorizonBasis SurfaceCameraBasis(SkyScene scene, SaverSettings s, double azimuthOffsetDeg)
    {
        var horizon = scene.Frame.Horizon(scene.TimeDays);
        double az = (s.ViewAzimuthDeg + azimuthOffsetDeg) * Units.Deg;
        double alt = Math.Clamp(s.ViewAltitudeDeg, -89, 89) * Units.Deg;
        var forward = Camera.Local(az, alt);
        var right = Vector3d.Cross(forward, Vector3d.UnitZ).Normalized();
        var up = Vector3d.Cross(right, forward);
        Vector3d ToIcrs(Vector3d v) => horizon.East * v.X + horizon.North * v.Y + horizon.Zenith * v.Z;
        return FlightOrientation.Basis(ToIcrs(forward), ToIcrs(up));
    }

    private void DrawFlightStars(SKCanvas canvas, TourFlight flight, ReadOnlySpan<SeenStar> seen, HorizonBasis view,
                                 Camera cam, SaverSettings s, double limit, float density)
    {
        using var labelFont = new SKFont(_typeface, 12 * density);
        var stars = flight.Sky.Stars;
        for (int i = 0; i < seen.Length; i++)
        {
            var star = stars[i];
            ref readonly var look = ref seen[i];
            bool isSol = star.IsSol && s.MarkSol;
            if (look.ApparentMagnitude > limit && !isSol) continue;
            if (cam.Project(view.ToLocal(look.Direction)) is not SKPoint p) continue;

            if (look.ApparentMagnitude <= limit)
            {
                var color = BvToColor(look.ColorBV, s.ColorBoost);
                if (look.ApparentMagnitude < -2)
                {
                    // A star you are close to: a glow that shrinks as you leave it, or grows as you arrive.
                    float glowR = (float)Math.Min(60, (-2 - look.ApparentMagnitude) * 3) * density;
                    using var glow = SKShader.CreateRadialGradient(p, glowR, [color.WithAlpha(140), color.WithAlpha(0)], SKShaderTileMode.Clamp);
                    using var glowPaint = new SKPaint { IsAntialias = true, Shader = glow };
                    canvas.DrawCircle(p, glowR, glowPaint);
                }
                float r = (float)Math.Clamp((0.7 + (limit - look.ApparentMagnitude) * 0.45) * s.StarScale, 0.6, 7) * density;
                byte alpha = (byte)Math.Clamp((limit - look.ApparentMagnitude + 0.6) / 2.5 * 255, 60, 255);
                _fill.Color = color.WithAlpha(alpha);
                canvas.DrawCircle(p, r, _fill);
            }

            if (isSol)
            {
                _stroke.Color = SolGold.WithAlpha(200);
                _stroke.StrokeWidth = 1.2f * density;
                canvas.DrawCircle(p, 9 * density, _stroke);
                _fill.Color = SolGold;
                canvas.DrawText("Sun (Sol)", p.X + 12 * density, p.Y + 4 * density, SKTextAlign.Left, labelFont, _fill);
            }
            else if (star.Name is not null && look.ApparentMagnitude < s.LabelMagnitudeLimit &&
                     i != flight.Sky.ToHostIndex && i != flight.Sky.FromHostIndex)
            {
                _fill.Color = new SKColor(200, 210, 230, 170);
                canvas.DrawText(star.Name, p.X + 6 * density, p.Y + 4 * density, SKTextAlign.Left, labelFont, _fill);
            }
        }
    }

    private void DrawFlightInfo(SKCanvas canvas, SKSizeI size, TourFlight flight, FlightPath path, ReadOnlySpan<SeenStar> seen,
                                HorizonBasis view, Camera cam, double opacity, bool drift, float density)
    {
        byte a = (byte)Math.Round(opacity * 255);
        using var font = new SKFont(_typeface, 13 * density);
        double remaining = path.RemainingLightYears(flight.Progress);

        // Mark the destination star so first-time viewers know where they are heading.
        var dest = flight.Sky.Stars[flight.Sky.ToHostIndex];
        if (flight.Sky.ToHostIndex != flight.Sky.FromHostIndex &&
            cam.Project(view.ToLocal(seen[flight.Sky.ToHostIndex].Direction)) is SKPoint d)
        {
            _stroke.Color = DestinationMark.WithAlpha((byte)(a * 0.8));
            _stroke.StrokeWidth = 1.2f * density;
            canvas.DrawCircle(d, 14 * density, _stroke);
            _fill.Color = DestinationMark.WithAlpha(a);
            canvas.DrawText($"{dest.Name}  ·  {remaining:#,0.##} ly", d.X + 18 * density, d.Y + 4 * density, SKTextAlign.Left, font, _fill);
        }

        var from = flight.From.Sky.Planet;
        var to = flight.To.Sky.Planet;
        double fromEarth = flight.Position.Length * Units.LightYearsPerParsec;
        var lines = new[]
        {
            $"Flying from {from.PlanetName} to {to.PlanetName}",
            $"{remaining:#,0.##} of {path.LengthLightYears:#,0.##} ly to go  ·  {fromEarth:#,0.#} ly from Earth  ·  Earth's constellations reshape as you move",
        };
        float widest = lines.Max(l => font.MeasureText(l));
        float maxWidth = size.Width - 32 * density;
        if (widest > maxWidth) font.Size *= maxWidth / widest;

        _fill.Color = new SKColor(210, 215, 225, (byte)(160 * opacity));
        var (x, lift) = OverlayDrift(size, lines.Max(l => font.MeasureText(l)), density, drift);
        float y = size.Height - 16 * density - lift - (lines.Length - 1) * font.Spacing;
        foreach (var line in lines)
        {
            canvas.DrawText(line, x, y, SKTextAlign.Left, font, _fill);
            y += font.Spacing;
        }
    }
}
