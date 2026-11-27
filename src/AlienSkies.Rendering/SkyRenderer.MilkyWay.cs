using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Math;
using SkiaSharp;

namespace AlienSkies.Rendering;

/// <summary>
/// The Milky Way band. <see cref="MilkyWayModel"/> is painted once into an equirectangular texture in galactic
/// coordinates (Earth's view). A mesh in the viewer's galactic coordinates carries, at each vertex, the texture
/// coordinate of the patch Earth sees there (<see cref="Galactic.EarthDirectionOf"/>), so the band shifts by
/// parallax for distant systems. Each frame re-projects the mesh's ~5,000 vertices and fills it at low resolution.
/// </summary>
public sealed partial class SkyRenderer
{
    private const int TexWidth = 1024, TexHeight = 340;
    private const double TexMaxB = 60;    // texture covers galactic latitude -60..+60 (Earth view)
    private const double MeshStep = 3;    // degrees per mesh cell
    private const double MeshMaxB = 63;   // mesh covers the viewer's latitude -63..+63

    private static readonly Lazy<SKImage> MilkyWayTexture = new(BuildMilkyWayTexture);

    private MilkyWayMesh? _milkyWay;
    private SKSurface? _milkyWaySurface;
    private readonly SKPaint _milkyWayPaint = new() { IsAntialias = false };

    /// <summary>
    /// The band is drawn into a small offscreen surface, about 0.3° per pixel (no finer than the texture), then
    /// stretched over the screen with bilinear filtering. Skia's CPU rasteriser fills textured triangles slowly: at
    /// full HD this way costs ~20 ms a frame on a modest 4-core VM, against ~270 ms drawing the mesh at full resolution.
    /// </summary>
    private void DrawMilkyWay(SKCanvas canvas, SKSizeI size, SkyScene scene, HorizonBasis horizon, SaverSettings s,
                              double azimuthDeg, double day)
    {
        double opacity = System.Math.Clamp(s.MilkyWayBrightness, 0, 3) * 0.5 * System.Math.Pow(1 - day, 3);
        if (opacity < 0.01 || size.Width < 2 || size.Height < 2) return;

        if (_milkyWay is null || !ReferenceEquals(_milkyWay.Sky, scene.Sky))
            _milkyWay = new MilkyWayMesh(scene.Sky, scene.HostPositionParsecs);
        var mesh = _milkyWay;

        // Offscreen size: the long edge gets one pixel per ~0.3° of field of view, within sensible bounds.
        int longScreen = System.Math.Max(size.Width, size.Height);
        double fov = System.Math.Clamp(s.FieldOfViewDeg, 10, 160);
        double scale = System.Math.Clamp(fov / 0.3, 160, 720) / longScreen;
        if (scale > 1) scale = 1;
        var small = new SKSizeI(System.Math.Max(2, (int)System.Math.Round(size.Width * scale)),
                                System.Math.Max(2, (int)System.Math.Round(size.Height * scale)));
        if (_milkyWaySurface is null || _milkyWaySurface.Canvas.DeviceClipBounds.Width != small.Width ||
            _milkyWaySurface.Canvas.DeviceClipBounds.Height != small.Height)
        {
            _milkyWaySurface?.Dispose();
            _milkyWaySurface = SKSurface.Create(new SKImageInfo(small.Width, small.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
            if (_milkyWaySurface is null) return;
        }
        var cam = new Camera(small, azimuthDeg, s.ViewAltitudeDeg, s.FieldOfViewDeg);

        int n = mesh.Directions.Length;
        var points = mesh.Points;
        var ok = mesh.Visible;
        var above = mesh.Above;
        for (int i = 0; i < n; i++)
        {
            var local = horizon.ToLocal(mesh.Directions[i]);
            above[i] = local.Z > -0.05;
            if (cam.Project(local) is SKPoint p) { points[i] = p; ok[i] = true; }
            else ok[i] = false;
        }

        var indices = mesh.Indices;
        indices.Clear();
        int cols = mesh.Columns;
        for (int r = 0; r + 1 < mesh.Rows; r++)
        for (int c = 0; c + 1 < cols; c++)
        {
            int a = r * cols + c, b = a + 1, d = a + cols, e = d + 1;
            if (!(ok[a] && ok[b] && ok[d] && ok[e])) continue;
            if (s.ShowGround && !(above[a] || above[b] || above[d] || above[e])) continue; // hidden by the ground
            indices.Add((ushort)a); indices.Add((ushort)b); indices.Add((ushort)d);
            indices.Add((ushort)b); indices.Add((ushort)e); indices.Add((ushort)d);
        }
        if (indices.Count == 0) return;

        var off = _milkyWaySurface.Canvas;
        off.Clear(SKColors.Transparent);
        using (var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles, points, mesh.TexCoords, null, indices.ToArray()))
        using (var shader = MilkyWayTexture.Value.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Clamp,
                   new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None))) // nearest saves ~7 ms but shows steps on phones
        {
            _milkyWayPaint.Shader = shader;
            _milkyWayPaint.Color = SKColors.White.WithAlpha((byte)System.Math.Clamp(opacity * 255, 0, 255));
            off.DrawVertices(vertices, SKBlendMode.Modulate, _milkyWayPaint);
            _milkyWayPaint.Shader = null;
        }

        using var image = _milkyWaySurface.Snapshot();
        canvas.DrawImage(image, new SKRect(0, 0, size.Width, size.Height),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
    }

    private static SKImage BuildMilkyWayTexture()
    {
        var cool = (R: 170.0, G: 188.0, B: 235.0);
        var warm = (R: 240.0, G: 220.0, B: 185.0);
        using var bitmap = new SKBitmap(new SKImageInfo(TexWidth, TexHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        var pixels = new byte[TexWidth * TexHeight * 4];
        for (int y = 0; y < TexHeight; y++)
        {
            double b = TexMaxB - (y + 0.5) / TexHeight * 2 * TexMaxB;
            for (int x = 0; x < TexWidth; x++)
            {
                double l = (x + 0.5) / TexWidth * 360;
                var (intensity, warmth) = MilkyWayModel.Sample(l, b);
                double alpha = System.Math.Clamp(intensity * 0.85, 0, 1);
                int o = (y * TexWidth + x) * 4;
                // Premultiplied: colour channels carry the alpha.
                pixels[o + 0] = (byte)((cool.R + (warm.R - cool.R) * warmth) * alpha);
                pixels[o + 1] = (byte)((cool.G + (warm.G - cool.G) * warmth) * alpha);
                pixels[o + 2] = (byte)((cool.B + (warm.B - cool.B) * warmth) * alpha);
                pixels[o + 3] = (byte)(255 * alpha);
            }
        }
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        return SKImage.FromBitmap(bitmap);
    }

    /// <summary>The band's mesh for one system: fixed ICRS directions and texture coordinates, plus per-frame scratch space.</summary>
    private sealed class MilkyWayMesh
    {
        public readonly AlienSky Sky;
        public readonly int Columns, Rows;
        public readonly Vector3d[] Directions;
        public readonly SKPoint[] TexCoords, Points;
        public readonly bool[] Visible, Above;
        public readonly List<ushort> Indices = [];

        public MilkyWayMesh(AlienSky sky, Vector3d viewerPositionParsecs)
        {
            Sky = sky;
            Columns = (int)(360 / MeshStep) + 1; // the last column repeats the first, at l = 360
            Rows = (int)(2 * MeshMaxB / MeshStep) + 1;
            int n = Columns * Rows;
            Directions = new Vector3d[n];
            TexCoords = new SKPoint[n];
            Points = new SKPoint[n];
            Visible = new bool[n];
            Above = new bool[n];

            for (int r = 0; r < Rows; r++)
            {
                double bv = -MeshMaxB + r * MeshStep;
                for (int c = 0; c < Columns; c++)
                {
                    double lv = c * MeshStep;
                    var icrs = Galactic.ToIcrs(Galactic.FromLB(lv, bv));
                    var (le, be) = Galactic.ToLB(Galactic.FromIcrs(Galactic.EarthDirectionOf(icrs, viewerPositionParsecs)));
                    // Keep u continuous along the row (no jump at l = 0/360); the shader repeats horizontally.
                    double dl = le - lv;
                    dl -= 360 * System.Math.Round(dl / 360);
                    double u = (lv + dl) / 360 * TexWidth;
                    double v = (TexMaxB - be) / (2 * TexMaxB) * TexHeight;
                    int i = r * Columns + c;
                    Directions[i] = icrs;
                    TexCoords[i] = new SKPoint((float)u, (float)System.Math.Clamp(v, 0, TexHeight));
                }
            }
        }
    }
}
