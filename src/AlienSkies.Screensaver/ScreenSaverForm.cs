using AlienSkies.Rendering;
using AlienSkies.Screensaver.Native;

namespace AlienSkies.Screensaver;

public enum SaverMode { FullScreen, Preview, Windowed }

/// <summary>
/// One window per monitor in full-screen mode; a child of the Control Panel thumbnail in preview mode;
/// a normal resizable window in windowed (/w) debug mode. Drawing is done by the shared
/// SkiaSharp <see cref="SkyRenderer"/> on a <see cref="SkiaPanel"/>.
/// </summary>
public sealed class ScreenSaverForm : Form
{
    private const int MouseWakeThresholdPx = 12;

    private readonly SkyScene _scene;
    private readonly SaverSettings _settings;
    private readonly SkyRenderer _renderer = new();
    private readonly SaverMode _mode;
    private readonly double _azimuthOffsetDeg;
    private readonly IntPtr _previewParent;
    private readonly SkiaPanel _canvas = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 33 };
    private Point? _mouseOrigin;

    public ScreenSaverForm(SkyScene scene, SaverSettings settings, SaverMode mode,
                           Rectangle bounds, double azimuthOffsetDeg = 0, IntPtr previewParent = default)
    {
        _scene = scene;
        _settings = settings;
        _mode = mode;
        _azimuthOffsetDeg = azimuthOffsetDeg;
        _previewParent = previewParent;

        Text = "Alien Skies";
        BackColor = Color.Black;
        KeyPreview = true;

        if (mode == SaverMode.Windowed)
        {
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1280, 720);
        }
        else
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            if (mode == SaverMode.FullScreen) TopMost = true;
        }

        _canvas.PaintSurface += OnPaintSurface;
        _canvas.MouseMove += (_, _) => OnUserMouseMove();
        _canvas.MouseDown += (_, _) => { if (_mode == SaverMode.FullScreen) Application.Exit(); };
        Controls.Add(_canvas);

        _timer.Tick += (_, _) =>
        {
            if (_mode == SaverMode.Preview && !NativeMethods.IsWindow(_previewParent)) { Application.Exit(); return; }
            _scene.Tick();
            _canvas.Invalidate();
        };
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (_mode == SaverMode.Preview)
        {
            // Re-parent into the little monitor picture in the Screen Saver Settings dialog.
            NativeMethods.SetParent(Handle, _previewParent);
            long style = NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GWL_STYLE).ToInt64();
            NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GWL_STYLE, new IntPtr(style | NativeMethods.WS_CHILD));
            NativeMethods.GetClientRect(_previewParent, out var rc);
            Location = Point.Empty;
            Size = new Size(rc.Right - rc.Left, rc.Bottom - rc.Top);
        }
        else if (_mode == SaverMode.FullScreen)
        {
            Cursor.Hide();
        }
        _timer.Start();
    }

    private void OnPaintSurface(object? sender, SkiaPaintEventArgs e)
    {
        var canvas = e.Canvas;
        try
        {
            float density = DeviceDpi / 96f;
            if (_mode == SaverMode.Preview) density = 0.5f; // the preview monitor is tiny
            _renderer.Draw(canvas, e.Size, _scene, _settings, _azimuthOffsetDeg, density);
        }
        catch (Exception ex) when (_mode != SaverMode.FullScreen)
        {
            using var paint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.OrangeRed };
            using var font = new SkiaSharp.SKFont { Size = 14 };
            canvas.Clear(SkiaSharp.SKColors.Black);
            canvas.DrawText(ex.Message, 8, 20, SkiaSharp.SKTextAlign.Left, font, paint);
        }
    }

    // ---- wake-up handling (full screen only) --------------------------------

    private void OnUserMouseMove()
    {
        if (_mode != SaverMode.FullScreen) return;
        var pos = Cursor.Position;
        _mouseOrigin ??= pos;
        if (Math.Abs(pos.X - _mouseOrigin.Value.X) > MouseWakeThresholdPx ||
            Math.Abs(pos.Y - _mouseOrigin.Value.Y) > MouseWakeThresholdPx)
            Application.Exit();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_mode == SaverMode.FullScreen || e.KeyCode == Keys.Escape) Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _renderer.Dispose();
        }
        base.Dispose(disposing);
    }
}
