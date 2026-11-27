using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Math;
using SkiaSharp;

namespace AlienSkies.Rendering;

/// <summary>
/// Sibling planets: the other worlds of the viewer's system. Far away they are steady points like Venus or Jupiter;
/// close enough to show a disc (TRAPPIST-1's planets pass within a few lunar sizes of each other) they are drawn with
/// their phase, lit on the side facing the host star.
/// </summary>
public sealed partial class SkyRenderer
{
    private static readonly SKColor PlanetLight = new(255, 244, 222);
    private static readonly SKColor PlanetNight = new(18, 18, 24);
    private static readonly SKColor PlanetLabel = new(235, 225, 185, 190);

    /// <summary>A sibling as drawn this frame, kept for the info overlay.</summary>
    private readonly record struct SiblingSighting(string Name, SiblingView View, double Altitude, double Azimuth);

    private readonly List<SiblingSighting> _siblingSightings = [];

    private void DrawSiblings(SKCanvas canvas, SkyScene scene, HorizonBasis horizon, Camera cam, SaverSettings s,
                              Vector3d planetOffsetAu, double limit, float density)
    {
        _siblingSightings.Clear();
        if (scene.Siblings.Count == 0) return;

        double t = scene.TimeDays;
        double hostAt1Au = AlienSky.HostApparentMagnitude(scene.Sky.Planet, 1.0);
        using var labelFont = new SKFont(_typeface, 12 * density);

        foreach (var sibling in scene.Siblings)
        {
            var view = sibling.Observe(t, planetOffsetAu, hostAt1Au);
            var (alt, az) = horizon.ToHorizontal(view.Direction);
            _siblingSightings.Add(new SiblingSighting(sibling.Name, view, alt, az));

            float discR = (float)(view.AngularRadius * cam.Focal);
            bool resolved = discR >= 2.5f * density;
            if (view.Magnitude > limit && !resolved) continue;

            var local = horizon.ToLocal(view.Direction);
            if (s.ShowGround && local.Z < 0) continue;
            if (cam.Project(local) is not SKPoint p) continue;

            float labelOffset;
            if (resolved)
            {
                DrawPhasedDisc(canvas, cam, horizon, view, local, p, discR);
                labelOffset = discR + 5 * density;
            }
            else
            {
                // Same brightness → size rule as stars, a little larger, so a planet as bright as Sirius looks like it.
                float r = (float)System.Math.Clamp((0.9 + (limit - view.Magnitude) * 0.45) * s.StarScale, 0.9, 8) * density;
                byte alpha = (byte)System.Math.Clamp((limit - view.Magnitude + 0.6) / 2.5 * 255, 80, 255);
                using (var glow = SKShader.CreateRadialGradient(p, r * 3, [PlanetLight.WithAlpha((byte)(alpha / 4)), PlanetLight.WithAlpha(0)], SKShaderTileMode.Clamp))
                using (var glowPaint = new SKPaint { IsAntialias = true, Shader = glow })
                    canvas.DrawCircle(p, r * 3, glowPaint);
                _fill.Color = PlanetLight.WithAlpha(alpha);
                canvas.DrawCircle(p, r, _fill);
                labelOffset = r + 5 * density;
            }

            if (s.LabelMagnitudeLimit > -30)
            {
                _fill.Color = PlanetLabel;
                canvas.DrawText(sibling.Name, p.X + labelOffset, p.Y + 4 * density,
                    SKTextAlign.Left, labelFont, _fill);
            }
        }
    }

    private void DrawPhasedDisc(SKCanvas canvas, Camera cam, HorizonBasis horizon, SiblingView view, Vector3d local, SKPoint p, float r)
    {
        // Which way is the star on screen? Project a point a little toward it along the sky.
        float angle = 0;
        if (view.TowardStar != Vector3d.Zero)
        {
            var nudged = (local + horizon.ToLocal(view.TowardStar) * 0.002).Normalized();
            if (cam.Project(nudged) is SKPoint q && (q - p).Length > 1e-4f)
                angle = (float)(System.Math.Atan2(q.Y - p.Y, q.X - p.X) / Units.Deg);
        }

        // Night side first (it hides the stars behind it), then the lit part.
        _fill.Color = PlanetNight;
        canvas.DrawCircle(p, r, _fill);

        double cosA = System.Math.Cos(view.PhaseAngle);
        float e = (float)(r * System.Math.Abs(cosA));
        using var builder = new SKPathBuilder();
        // The lit limb: the half of the disc facing the star (+x after rotation), top to bottom.
        builder.ArcTo(new SKRect(-r, -r, r, r), -90, 180, true);
        // The terminator: half an ellipse back to the top, bulging away from the star when gibbous, toward it as a crescent.
        builder.ArcTo(new SKRect(-e, -r, e, r), 90, cosA >= 0 ? 180 : -180, false);
        builder.Close();
        using var path = builder.Detach();

        canvas.Save();
        canvas.Translate(p.X, p.Y);
        canvas.RotateDegrees(angle);
        _fill.Color = PlanetLight;
        canvas.DrawPath(path, _fill);
        canvas.Restore();
    }
}
