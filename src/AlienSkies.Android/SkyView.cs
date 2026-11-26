using AlienSkies.Rendering;
using Android.Content;
using Android.Views;
using SkiaSharp.Views.Android;

namespace AlienSkies.Droid;

/// <summary>
/// An animated view of the alien sky, used by the screen saver and the preview screen.
/// Redraws on the display's vsync (Choreographer), throttled to about 30 frames per second.
/// </summary>
public sealed class SkyView : SKCanvasView, Choreographer.IFrameCallback
{
    private const long FrameIntervalNanos = 33_000_000;

    private readonly SaverSettings _settings;
    private readonly SkyScene _scene;
    private readonly SkyRenderer _renderer = new();
    private readonly float _density;
    private bool _running;
    private long _lastFrame;

    public SkyView(Context context, SaverSettings settings) : base(context)
    {
        AppSetup.EnsureInitialized(context);
        _settings = settings;
        _scene = new SkyScene(settings);
        _density = context.Resources!.DisplayMetrics!.Density;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        Choreographer.Instance!.PostFrameCallback(this);
    }

    public void Stop()
    {
        _running = false;
        Choreographer.Instance!.RemoveFrameCallback(this);
    }

    public void DoFrame(long frameTimeNanos)
    {
        if (!_running) return;
        if (frameTimeNanos - _lastFrame >= FrameIntervalNanos)
        {
            _lastFrame = frameTimeNanos;
            _scene.Tick();
            Invalidate();
        }
        Choreographer.Instance!.PostFrameCallback(this);
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e) =>
        _renderer.Draw(e.Surface.Canvas, e.Info.Size, _scene, _settings, 0, _density);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
            _renderer.Dispose();
        }
        base.Dispose(disposing);
    }
}
