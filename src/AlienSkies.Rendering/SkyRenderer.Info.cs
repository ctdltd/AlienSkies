using AlienSkies.Core.Astronomy;
using SkiaSharp;

namespace AlienSkies.Rendering;

/// <summary>Info-overlay extras: the sibling-planet line and the edge arrow pointing toward our Sun.</summary>
public sealed partial class SkyRenderer
{
    /// <summary>One overlay line naming the sibling planets that are up, brightest first; null when the system has none.</summary>
    private string? SiblingSummary()
    {
        if (_siblingSightings.Count == 0) return null;
        var up = _siblingSightings.Where(x => x.Altitude > 0).OrderBy(x => x.View.Magnitude).ToList();
        int n = _siblingSightings.Count;
        string planets = n == 1 ? "1 other planet" : $"{n} other planets";
        if (up.Count == 0) return $"{planets} in this system, none above the horizon now";

        var shown = up.Take(3).Select(x =>
            $"{x.Name} mag {x.View.Magnitude:0.0} (az {x.Azimuth / Units.Deg:0}°, alt {x.Altitude / Units.Deg:0}°)");
        string more = up.Count > 3 ? $"  ·  {up.Count - 3} more up" : "";
        return $"{planets} in this system. Up now: {string.Join(", ", shown)}{more}";
    }

    /// <summary>When our Sun is out of view, an arrow at the screen edge shows which way to turn to find it.</summary>
    private void DrawSolPointer(SKCanvas canvas, SKSizeI size, SkyScene scene, HorizonBasis horizon, Camera cam,
                                float infoTop, float density)
    {
        if (!scene.StarsById.TryGetValue(AlienSky.SolId, out var sol)) return;
        var local = horizon.ToLocal(sol.Direction);

        // The arrow runs around a frame inset from the screen edges, with its bottom edge above the info text.
        float margin = 30 * density;
        float left = margin, right = size.Width - margin, top = margin;
        float bottom = System.Math.Max(top + 20 * density, System.Math.Min(size.Height - margin, infoTop - 14 * density));
        if (cam.Project(local) is SKPoint onScreen &&
            onScreen.X > left && onScreen.X < right && onScreen.Y > top && onScreen.Y < bottom) return; // in view: the Sol marker does the job

        var d = cam.ScreenDirection(local);
        float len = d.Length;
        if (len < 1e-6f) d = new SKPoint(0, 1); // dead behind: point down, either way works
        else d = new SKPoint(d.X / len, d.Y / len);

        // Walk from the screen centre along d to the frame.
        float cx = System.Math.Clamp(cam.CenterX, left, right), cy = System.Math.Clamp(cam.CenterY, top, bottom);
        float tx = d.X > 1e-6f ? (right - cx) / d.X : d.X < -1e-6f ? (left - cx) / d.X : float.MaxValue;
        float ty = d.Y > 1e-6f ? (bottom - cy) / d.Y : d.Y < -1e-6f ? (top - cy) / d.Y : float.MaxValue;
        float t = System.Math.Min(tx, ty);
        var tip = new SKPoint(cx + d.X * t, cy + d.Y * t);

        // A small chevron pointing outward along d.
        float s = 9 * density;
        var back = new SKPoint(tip.X - d.X * s * 1.6f, tip.Y - d.Y * s * 1.6f);
        var side = new SKPoint(-d.Y * s, d.X * s);
        using var builder = new SKPathBuilder();
        builder.MoveTo(tip);
        builder.LineTo(back.X + side.X, back.Y + side.Y);
        builder.LineTo(back.X - side.X, back.Y - side.Y);
        builder.Close();
        using var path = builder.Detach();
        _fill.Color = SolGold.WithAlpha(210);
        canvas.DrawPath(path, _fill);

        using var font = new SKFont(_typeface, 12 * density);
        string label = local.Z < 0 ? "Sun (below horizon)" : "Sun";
        float w = font.MeasureText(label);
        // Put the label on the screen side of the arrow, clamped inside the edges.
        var at = new SKPoint(back.X - d.X * 8 * density, back.Y - d.Y * 8 * density);
        float x = System.Math.Clamp(d.X > 0.3f ? at.X - w : d.X < -0.3f ? at.X : at.X - w / 2, 4 * density, size.Width - w - 4 * density);
        float y = System.Math.Clamp(d.Y > 0.3f ? at.Y - 4 * density : d.Y < -0.3f ? at.Y + font.Size : at.Y + font.Size / 3,
                                    font.Size, bottom);
        canvas.DrawText(label, x, y, SKTextAlign.Left, font, _fill);
    }
}
