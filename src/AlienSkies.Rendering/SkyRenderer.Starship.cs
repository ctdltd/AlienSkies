using SkiaSharp;

namespace AlienSkies.Rendering;

/// <summary>
/// Draws the passing starship (<see cref="StarshipFlyby"/>): a small side-on silhouette with glowing nacelles that
/// cruises along its arc, charges up, stretches into a warp streak and vanishes in a flash. Nothing at all is drawn,
/// or allocated, while no ship is passing.
/// </summary>
public sealed partial class SkyRenderer
{
    private static readonly SKColor HullLight = new(200, 206, 216);
    private static readonly SKColor HullShade = new(128, 136, 150);
    private static readonly SKColor HullFar = new(96, 104, 120);
    private static readonly SKColor HullLine = new(58, 64, 78);
    private static readonly SKColor NacelleGlow = new(120, 205, 255);
    private static readonly SKColor CollectorGlow = new(196, 150, 255);
    private static readonly SKColor DeflectorGlow = new(255, 196, 120);

    private const double ChargeSeconds = 0.4;  // nacelles brighten while the ship coasts on
    private const double StretchSeconds = 0.18; // the ship smears out along its heading and is gone

    // Ship drawing units: x forward, y down, about 114 long. Built once, on the first pass.
    private const float ShipLength = 114, ShipCenterX = 41, ShipCenterY = -1;
    private static readonly Lazy<ShipShapes> Shapes = new(ShipShapes.Build);

    private void DrawStarship(SKCanvas canvas, SKSizeI size, StarshipFlyby ship, Camera cam, float density)
    {
        double w = ship.WarpAge;
        double coast = Math.Clamp(w, 0, ChargeSeconds);
        if (cam.Project(ship.Position(coast)) is not SKPoint p ||
            cam.Project(ship.Position(coast + 0.25)) is not SKPoint q) return;
        var d = q - p;
        float len = d.Length;
        if (len < 1e-3f) return;
        var dir = new SKPoint(d.X / len, d.Y / len);

        float L = Math.Max(34 * density, Math.Min(size.Width, size.Height) / 22f); // ship length in pixels
        float scale = L / ShipLength;

        double jump = w - ChargeSeconds;                              // < 0 until the jump
        double k = Math.Clamp(jump / StretchSeconds, 0, 1);            // 0..1 through the stretch
        double after = StarshipFlyby.WarpSeconds - ChargeSeconds - StretchSeconds;
        double fade = jump <= StretchSeconds ? 1 : Math.Clamp(1 - (jump - StretchSeconds) / after, 0, 1);
        double glow = w < 0 ? 0.6 + 0.08 * Math.Sin(ship.Age * 5) : 0.6 + 0.4 * Math.Min(w / ChargeSeconds, 1);

        // Streak and flash first, so the ship (while it is still there) sits on top of them.
        if (jump > 0) DrawWarpStreak(canvas, p, dir, L, k, fade);

        if (k < 1)
        {
            float stretch = (float)(1 + 14 * k * k);
            float shove = (float)(L * 6 * k * k);
            byte alpha = (byte)Math.Round(255 * (1 - k));

            canvas.Save();
            canvas.Translate(p.X + dir.X * shove, p.Y + dir.Y * shove);
            // Keep the ship upright whichever way it flies: mirror it rather than turn it upside down.
            bool leftward = dir.X < 0;
            canvas.RotateRadians(MathF.Atan2(dir.Y, leftward ? -dir.X : dir.X));
            canvas.Scale(leftward ? -scale * stretch : scale * stretch, scale);
            canvas.Translate(-ShipCenterX, -ShipCenterY);
            Shapes.Value.Draw(canvas, alpha, glow, scale);
            canvas.Restore();
        }
    }

    private static void DrawWarpStreak(SKCanvas canvas, SKPoint p, SKPoint dir, float L, double k, double fade)
    {
        // A thin line of light from where the ship was to where it vanished, then the flash at its far end.
        float reach = L * (0.4f + 9 * (float)Math.Sqrt(k));
        var tip = new SKPoint(p.X + dir.X * reach, p.Y + dir.Y * reach);
        byte a = (byte)Math.Round(230 * fade);

        using (var shader = SKShader.CreateLinearGradient(p, tip,
                   [NacelleGlow.WithAlpha(0), NacelleGlow.WithAlpha((byte)(a * 0.7)), SKColors.White.WithAlpha(a)],
                   [0f, 0.6f, 1f], SKShaderTileMode.Clamp))
        using (var line = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, Shader = shader,
                                        StrokeCap = SKStrokeCap.Round, StrokeWidth = Math.Max(1.2f, L * 0.035f) })
            canvas.DrawLine(p, tip, line);

        if (k < 1) return;
        // Flash: bright at the moment the ship is gone, swelling and fading after.
        float r = L * (0.45f + 0.9f * (float)(1 - fade));
        using (var flash = SKShader.CreateRadialGradient(tip, r,
                   [SKColors.White.WithAlpha(a), NacelleGlow.WithAlpha((byte)(a * 0.5)), NacelleGlow.WithAlpha(0)],
                   [0f, 0.35f, 1f], SKShaderTileMode.Clamp))
        using (var paint = new SKPaint { IsAntialias = true, Shader = flash })
            canvas.DrawCircle(tip, r, paint);
        using var spike = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = Math.Max(1f, L * 0.02f),
                                        Color = SKColors.White.WithAlpha((byte)(a * 0.8)) };
        float s = r * 1.6f;
        canvas.DrawLine(tip.X - s, tip.Y, tip.X + s, tip.Y, spike);
        canvas.DrawLine(tip.X, tip.Y - s * 0.5f, tip.X, tip.Y + s * 0.5f, spike);
    }

    /// <summary>
    /// The ship's outline, in ship units: an original design in the classic layout (saucer up front, a secondary hull
    /// slung below and behind it on a swept neck, two long nacelles raised on pylons). Seen from the side and a little
    /// above, so the far nacelle shows above and behind the near one.
    /// </summary>
    private sealed class ShipShapes
    {
        private SKPath _farNacelle = null!, _farPylon = null!, _hull = null!, _neck = null!, _nearPylon = null!, _nearNacelle = null!;
        private SKRect _saucerRim, _saucerTop, _dome, _deflector, _nearStrip, _farStrip, _nearCap, _farCap;

        public static ShipShapes Build()
        {
            static SKPath Poly(params SKPoint[] pts)
            {
                using var b = new SKPathBuilder();
                b.MoveTo(pts[0]);
                foreach (var pt in pts.AsSpan(1)) b.LineTo(pt);
                b.Close();
                return b.Detach();
            }
            static SKPath Capsule(float x0, float y0, float x1, float y1)
            {
                using var b = new SKPathBuilder();
                b.AddRoundRect(new SKRoundRect(new SKRect(x0, y0, x1, y1), (y1 - y0) / 2));
                return b.Detach();
            }

            SKPath hull;
            using (var b = new SKPathBuilder())
            {
                b.MoveTo(15, 7.5f);
                b.LineTo(52, 7.5f);
                b.QuadTo(63, 8, 63, 12.5f);
                b.QuadTo(63, 17, 52, 17.5f);
                b.LineTo(19, 16.5f);
                b.QuadTo(10, 16, 10, 12);
                b.QuadTo(10, 8, 15, 7.5f);
                b.Close();
                hull = b.Detach();
            }

            return new ShipShapes
            {
                _farNacelle = Capsule(-13, -21, 38, -14.5f),
                _farPylon = Poly(new(27, 8), new(34, 8), new(23, -16), new(17, -16)),
                _hull = hull,
                _neck = Poly(new(61, -2), new(71, -2), new(57, 9), new(47, 9)),
                _nearPylon = Poly(new(23, 9), new(31, 9), new(18, -10), new(12, -10)),
                _nearNacelle = Capsule(-16, -15.5f, 35, -8.5f),
                _saucerRim = new SKRect(40, -13, 98, 1),
                _saucerTop = new SKRect(41, -16, 97, -2),
                _dome = new SKRect(63, -18.5f, 75, -12.5f),
                _deflector = new SKRect(60, 10, 63.5f, 15),
                _nearStrip = new SKRect(-11, -13, 30, -10.5f),
                _farStrip = new SKRect(-8, -18.5f, 33, -16.5f),
                _nearCap = new SKRect(29, -15.5f, 36, -8.5f),
                _farCap = new SKRect(32, -21, 39, -14.5f),
            };
        }

        /// <param name="glow">Nacelle brightness, 0..1.</param>
        /// <param name="unitsToPixels">Ship units per pixel, so the outline and glow stay about a pixel or two wide.</param>
        public void Draw(SKCanvas canvas, byte alpha, double glow, float unitsToPixels)
        {
            float px = 1 / unitsToPixels; // one pixel, in ship units
            using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
            using var line = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.8f * px,
                                           Color = HullLine.WithAlpha(alpha) };
            using var blur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2.2f * px);
            using var halo = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, MaskFilter = blur };
            byte g = (byte)Math.Round(alpha * Math.Clamp(glow, 0, 1));

            void Shape(SKPath path, SKColor color)
            {
                fill.Color = color.WithAlpha(alpha);
                canvas.DrawPath(path, fill);
                canvas.DrawPath(path, line);
            }
            void Oval(SKRect r, SKColor color, bool outline = true)
            {
                fill.Color = color.WithAlpha(alpha);
                canvas.DrawOval(r, fill);
                if (outline) canvas.DrawOval(r, line);
            }
            void Glow(SKRect r, SKColor color, byte a, float spread)
            {
                halo.Color = color.WithAlpha(a);
                var outer = r;
                outer.Inflate(spread * px, spread * px);
                canvas.DrawOval(outer, halo);
                fill.Color = SKColors.White.WithAlpha((byte)(a * 0.85));
                canvas.DrawOval(r, fill);
            }

            // Far side first.
            Shape(_farNacelle, HullFar);
            Glow(_farStrip, NacelleGlow, (byte)(g * 0.7), 1.5f);
            Glow(_farCap, CollectorGlow, (byte)(g * 0.8), 2f);
            Shape(_farPylon, HullFar);

            Shape(_hull, HullShade);
            Glow(_deflector, DeflectorGlow, g, 1.5f);
            Shape(_neck, HullShade);

            Oval(_saucerRim, HullShade);
            Oval(_saucerTop, HullLight);
            Oval(_dome, HullLight);

            Shape(_nearPylon, HullLight);
            Shape(_nearNacelle, HullLight);
            Glow(_nearStrip, NacelleGlow, g, 2f);
            Glow(_nearCap, CollectorGlow, g, 2.5f);
        }
    }
}
