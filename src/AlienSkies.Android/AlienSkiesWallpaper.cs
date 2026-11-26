using AlienSkies.Rendering;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Com.Certificationtoolanddie.Alienskies.Shim;
using SkiaSharp;

namespace AlienSkies.Droid;

/// <summary>
/// Live wallpaper: the same alien sky behind the home screen. Swiping between home-screen pages
/// pans the view across the horizon. Draws only while visible, at a lower frame rate than the
/// screen saver to go easy on the battery.
/// </summary>
/// <remarks>
/// The Engine itself is a small Java shim (Java/SkyWallpaperService.java) because
/// WallpaperService.Engine is a Java inner class that .NET for Android can't subclass cleanly;
/// everything else happens here, in <see cref="SkyEngine"/>.
/// </remarks>
[Service(
    Name = "com.certificationtoolanddie.alienskies.AlienSkiesWallpaper", // stable: Android remembers the chosen wallpaper by this
    Label = "Alien Skies",
    Exported = true,
    Permission = "android.permission.BIND_WALLPAPER")]
[IntentFilter(["android.service.wallpaper.WallpaperService"])]
[MetaData("android.service.wallpaper", Resource = "@xml/wallpaper")]
public sealed class AlienSkiesWallpaper : SkyWallpaperService
{
    protected override IEngineListener CreateEngineListener() => new SkyEngine(this);
}

/// <summary>Draws one wallpaper surface (the real wallpaper and the picker preview each get their own).</summary>
internal sealed class SkyEngine : Java.Lang.Object, SkyWallpaperService.IEngineListener, Choreographer.IFrameCallback
{
    private const long FrameIntervalNanos = 66_000_000; // ~15 fps
    /// <summary>How far (in degrees of azimuth) a swipe across all home-screen pages turns the view.</summary>
    private const double PanRangeDeg = 90;

    private readonly SkyRenderer _renderer = new();
    private readonly float _density;
    private SaverSettings _settings = null!;
    private SkyScene _scene = null!;
    private DateTime _settingsStamp;
    private ISurfaceHolder? _holder;
    private Bitmap? _bitmap;
    private double _panDeg;
    private bool _visible;
    private long _lastFrame;

    public SkyEngine(Context context)
    {
        _density = context.Resources!.DisplayMetrics!.Density;
        AppSetup.EnsureInitialized(context);
        LoadSettings();
    }

    /// <summary>Picks up changes made in the settings screen since the wallpaper was last shown.</summary>
    private void LoadSettings()
    {
        var stamp = File.Exists(SaverSettings.FilePath) ? File.GetLastWriteTimeUtc(SaverSettings.FilePath) : DateTime.MinValue;
        if (_scene is not null && stamp == _settingsStamp) return;
        _settingsStamp = stamp;
        _settings = SaverSettings.Load();
        _settings.ShowInfo = false; // keep the home screen uncluttered; the Sol marker still shows
        _scene = new SkyScene(_settings);
    }

    public void OnCreate(ISurfaceHolder? holder, bool isPreview) => _holder = holder;

    public void OnVisibilityChanged(bool visible)
    {
        _visible = visible;
        if (visible)
        {
            LoadSettings();
            Choreographer.Instance!.PostFrameCallback(this);
        }
        else
        {
            Choreographer.Instance!.RemoveFrameCallback(this);
        }
    }

    public void OnOffsetsChanged(float xOffset)
    {
        _panDeg = (xOffset - 0.5) * PanRangeDeg;
        if (_visible) DrawFrame();
    }

    public void OnSurfaceChanged(ISurfaceHolder? holder, int width, int height)
    {
        _holder = holder;
        _bitmap?.Dispose();
        _bitmap = null;
        DrawFrame();
    }

    public void OnSurfaceDestroyed(ISurfaceHolder? holder)
    {
        _visible = false;
        Choreographer.Instance!.RemoveFrameCallback(this);
        _holder = null;
    }

    public void OnDestroy()
    {
        Choreographer.Instance!.RemoveFrameCallback(this);
        _renderer.Dispose();
        _bitmap?.Dispose();
        _bitmap = null;
    }

    public void DoFrame(long frameTimeNanos)
    {
        if (!_visible) return;
        if (frameTimeNanos - _lastFrame >= FrameIntervalNanos)
        {
            _lastFrame = frameTimeNanos;
            _scene.Tick();
            DrawFrame();
        }
        Choreographer.Instance!.PostFrameCallback(this);
    }

    /// <summary>Skia draws into an Android bitmap's pixels, which is then posted to the wallpaper surface.</summary>
    private void DrawFrame()
    {
        var holder = _holder;
        var frame = holder?.SurfaceFrame;
        if (holder is null || frame is null || frame.Width() <= 0 || frame.Height() <= 0) return;
        int w = frame.Width(), h = frame.Height();

        if (_bitmap is null || _bitmap.Width != w || _bitmap.Height != h)
        {
            _bitmap?.Dispose();
            _bitmap = Bitmap.CreateBitmap(w, h, Bitmap.Config.Argb8888!)!;
        }

        IntPtr pixels = _bitmap.LockPixels();
        try
        {
            // Android's ARGB_8888 is stored R,G,B,A in memory, i.e. Skia's Rgba8888.
            var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, pixels, _bitmap.RowBytes);
            _renderer.Draw(surface.Canvas, new SKSizeI(w, h), _scene, _settings, _panDeg, _density);
            surface.Canvas.Flush();
        }
        finally
        {
            _bitmap.UnlockPixels();
        }

        Canvas? canvas = null;
        try
        {
            canvas = holder.LockHardwareCanvas();
            canvas?.DrawBitmap(_bitmap, 0, 0, null);
        }
        finally
        {
            if (canvas is not null) holder.UnlockCanvasAndPost(canvas);
        }
    }
}
