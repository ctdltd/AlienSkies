using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Math;
using SkiaSharp;

namespace AlienSkies.Rendering;

/// <summary>
/// The cheap Milky Way: a few nested contours of the band's brightness (blurred over several degrees), each filled
/// with a faint flat tint straight onto the sky, so the overlaps step up towards the bright middle. The contours are
/// traced once per system on a 3° grid in the viewer's galactic coordinates (marching squares, with runs of whole cells merged into strips);
/// each frame only projects their vertices and fills them in solid colour. There is no texture and no offscreen
/// image to stretch, which is what makes the detailed band expensive on Skia's CPU rasteriser.
/// </summary>
public sealed partial class SkyRenderer
{
    private const double TintStep = 3, TintMaxB = 66;
    private const int MaxStripCells = 8; // a strip longer than this is more likely to be dropped as partly behind the camera

    /// <summary>Brightness thresholds of the nested contours, on the same 0..1 scale as the detailed texture's alpha.</summary>
    private static readonly double[] TintLevels = [0.07, 0.14, 0.24, 0.38, 0.55];

    private TintBands? _tint;
    private readonly SKPaint _tintPaint = new() { IsAntialias = false, Style = SKPaintStyle.Fill };

    private void DrawMilkyWayTint(SKCanvas canvas, SKSizeI size, SkyScene scene, HorizonBasis horizon, SaverSettings s,
                                  double azimuthDeg, double opacity)
    {
        if (_tint is null || !ReferenceEquals(_tint.Sky, scene.Sky))
            _tint = new TintBands(scene.Sky, scene.HostPositionParsecs);
        var cam = new Camera(size, azimuthDeg, s.ViewAltitudeDeg, s.FieldOfViewDeg);

        double covered = 0; // opacity already laid down by the outer levels
        foreach (var band in _tint.Levels)
        {
            // Each level brings the total up to (roughly) the brightness at its threshold.
            double target = System.Math.Min(0.95, band.Threshold * opacity * 1.2);
            double a = 1 - (1 - target) / (1 - covered);
            covered = target;
            if (a <= 0.004 || band.Directions.Length == 0) continue;

            int n = band.Directions.Length;
            for (int i = 0; i < n; i++)
            {
                var local = horizon.ToLocal(band.Directions[i]);
                band.Above[i] = local.Z > -0.1;
                if (cam.Project(local) is SKPoint p) { band.Points[i] = p; band.Visible[i] = true; }
                else band.Visible[i] = false;
            }
            // One path per level: the pieces overlap only along shared edges, so filling them together (non-zero winding)
            // gives one seamless shape. Skia fills a path far faster than a vertex mesh. Antialiasing is off: it would
            // nearly double the cost, and at these faint steps the hard edges barely show.
            using var builder = new SKPathBuilder();
            var polys = band.Polygons;
            var pts = band.Points;
            bool anyDrawn = false;
            for (int q = 0; q + 1 < polys.Length; q += 2)
            {
                int first = polys[q], count = polys[q + 1];
                bool visible = true, above = !s.ShowGround;
                for (int i = first; i < first + count; i++) { visible &= band.Visible[i]; above |= band.Above[i]; }
                if (!visible || !above) continue;
                builder.MoveTo(pts[first]);
                for (int i = first + 1; i < first + count; i++) builder.LineTo(pts[i]);
                builder.Close();
                anyDrawn = true;
            }
            if (!anyDrawn) continue;

            _tintPaint.Color = band.Color.WithAlpha((byte)System.Math.Clamp(a * 255, 1, 255));
            using var path = builder.Detach();
            canvas.DrawPath(path, _tintPaint);
        }
    }

    /// <summary>One system's contours: per level, fixed ICRS directions and polygons, plus per-frame scratch space.</summary>
    private sealed class TintBands
    {
        public readonly AlienSky Sky;
        public readonly List<Level> Levels = [];

        public sealed class Level(double threshold, SKColor color, Vector3d[] directions, int[] polygons)
        {
            public readonly double Threshold = threshold;
            public readonly SKColor Color = color;
            public readonly Vector3d[] Directions = directions;
            public readonly int[] Polygons = polygons; // (start, count) pairs into Directions
            public readonly SKPoint[] Points = new SKPoint[directions.Length];
            public readonly bool[] Visible = new bool[directions.Length], Above = new bool[directions.Length];
        }

        public TintBands(AlienSky sky, Vector3d viewerPositionParsecs)
        {
            Sky = sky;
            int cols = (int)(360 / TintStep) + 1, rows = (int)(2 * TintMaxB / TintStep) + 1;
            var dir = new Vector3d[rows, cols];
            var value = new double[rows, cols];
            var warmth = new double[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                double bv = -TintMaxB + r * TintStep;
                for (int c = 0; c < cols; c++)
                {
                    double lv = c * TintStep;
                    var icrs = Galactic.ToIcrs(Galactic.FromLB(lv, bv));
                    var (le, be) = Galactic.ToLB(Galactic.FromIcrs(Galactic.EarthDirectionOf(icrs, viewerPositionParsecs)));
                    // Blur: average the model over a 5x5 patch, ±4° around the point Earth sees there.
                    double sumI = 0, sumW = 0;
                    for (int j = -2; j <= 2; j++)
                    for (int k = -2; k <= 2; k++)
                    {
                        var (intensity, w) = MilkyWayModel.Sample(((le + 2 * k) % 360 + 360) % 360,
                                                                  System.Math.Clamp(be + 2 * j, -90, 90));
                        sumI += intensity; sumW += intensity * w;
                    }
                    dir[r, c] = icrs;
                    value[r, c] = System.Math.Clamp(sumI / 25 * 0.85, 0, 1);
                    warmth[r, c] = sumI > 0 ? sumW / sumI : 0;
                }
            }

            var cool = (R: 150.0, G: 170.0, B: 225.0);
            var warm = (R: 235.0, G: 215.0, B: 180.0);
            foreach (double t in TintLevels)
            {
                var dirs = new List<Vector3d>();
                var polys = new List<int>(); // (start, count) pairs into dirs
                double wSum = 0, wCount = 0;
                var poly = new List<Vector3d>(8);
                void AddPolygon(List<Vector3d> pts)
                {
                    if (pts.Count < 3) return;
                    polys.Add(dirs.Count); polys.Add(pts.Count);
                    dirs.AddRange(pts);
                }
                bool Inside(int rr, int cc) => value[rr, cc] >= t;
                bool Full(int rr, int cc) => Inside(rr, cc) && Inside(rr, cc + 1) && Inside(rr + 1, cc) && Inside(rr + 1, cc + 1);
                for (int r = 0; r + 1 < rows; r++)
                for (int c = 0; c + 1 < cols; c++)
                {
                    if (Full(r, c))
                    {
                        // A run of whole cells along the row becomes one strip: fewer edges for Skia to scan.
                        int end = c + 1;
                        while (end + 1 < cols && end - c < MaxStripCells && Full(r, end)) end++;
                        poly.Clear();
                        for (int x = c; x <= end; x++) { poly.Add(dir[r, x]); wSum += warmth[r, x]; wCount++; }
                        for (int x = end; x >= c; x--) poly.Add(dir[r + 1, x]);
                        AddPolygon(poly);
                        c = end - 1;
                        continue;
                    }
                    // A cell the contour crosses: its inside part (marching squares), corners in order around the cell.
                    Span<(int R, int C)> k = [(r, c), (r, c + 1), (r + 1, c + 1), (r + 1, c)];
                    poly.Clear();
                    for (int e = 0; e < 4; e++)
                    {
                        var (ar, ac) = k[e];
                        var (br, bc) = k[(e + 1) % 4];
                        double va = value[ar, ac], vb = value[br, bc];
                        if (va >= t) { poly.Add(dir[ar, ac]); wSum += warmth[ar, ac]; wCount++; }
                        if ((va >= t) != (vb >= t))
                        {
                            double f = (t - va) / (vb - va);
                            poly.Add((dir[ar, ac] * (1 - f) + dir[br, bc] * f).Normalized());
                        }
                    }
                    AddPolygon(poly);
                }
                double mw = wCount > 0 ? wSum / wCount : 0;
                var color = new SKColor((byte)(cool.R + (warm.R - cool.R) * mw), (byte)(cool.G + (warm.G - cool.G) * mw),
                                        (byte)(cool.B + (warm.B - cool.B) * mw));
                Levels.Add(new Level(t, color, [.. dirs], [.. polys]));
            }
        }
    }
}
