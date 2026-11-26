using System.Drawing.Imaging;
using SkiaSharp;

namespace AlienSkies.Screensaver;

public sealed class SkiaPaintEventArgs(SKCanvas canvas, SKSizeI size) : EventArgs
{
    public SKCanvas Canvas { get; } = canvas;
    public SKSizeI Size { get; } = size;
}

/// <summary>
/// Minimal WinForms host for SkiaSharp (SkiaSharp.Views.WindowsForms only ships for .NET Framework).
/// Skia draws straight into the pixels of a premultiplied-ARGB GDI bitmap, which is then blitted.
/// </summary>
public sealed class SkiaPanel : Control
{
    private Bitmap? _bitmap;

    public event EventHandler<SkiaPaintEventArgs>? PaintSurface;

    public SkiaPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        int w = Math.Max(1, ClientSize.Width), h = Math.Max(1, ClientSize.Height);
        if (_bitmap is null || _bitmap.Width != w || _bitmap.Height != h)
        {
            _bitmap?.Dispose();
            _bitmap = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        }

        var data = _bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, _bitmap.PixelFormat);
        try
        {
            var info = new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, data.Scan0, data.Stride);
            PaintSurface?.Invoke(this, new SkiaPaintEventArgs(surface.Canvas, new SKSizeI(w, h)));
            surface.Canvas.Flush();
        }
        finally
        {
            _bitmap.UnlockBits(data);
        }

        e.Graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        e.Graphics.DrawImageUnscaled(_bitmap, 0, 0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _bitmap?.Dispose();
        base.Dispose(disposing);
    }
}
