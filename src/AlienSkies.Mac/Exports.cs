using System.Runtime.InteropServices;
using System.Text;
using AlienSkies.Rendering;
using SkiaSharp;

namespace AlienSkies.Mac;

/// <summary>
/// C entry points of AlienSkiesCore.dylib, called by the Objective-C ScreenSaverView (Saver/AlienSkiesView.m).
/// Every call comes from the main thread of the screen saver host process (legacyScreenSaver on recent
/// macOS, or System Settings for the preview thumbnail). No exception may cross back into native code,
/// so each export catches everything, remembers the message for <c>as_last_error</c> and returns a failure value.
/// One scene (planet, clock, tour) is shared by every screen, as on Windows; each screen has its own renderer.
/// </summary>
public static unsafe class Exports
{
    private static SaverSettings? _settings;
    private static SkyScene? _scene;
    private static string _lastError = "";
    private static byte* _lastErrorUtf8;

    private sealed class View(int screenIndex, bool preview)
    {
        public readonly SkyRenderer Renderer = new();
        public readonly int ScreenIndex = screenIndex;
        public readonly bool Preview = preview;
    }

    internal static SaverSettings Settings => _settings ??= SaverSettings.Load();

    private static SkyScene Scene => _scene ??= new SkyScene(Settings);

    /// <summary>
    /// Points catalog and native-library lookups at the bundle. <paramref name="resourceDir"/> is the
    /// .saver's Contents/Resources (catalogs in its data folder); <paramref name="libDir"/> holds libSkiaSharp.dylib.
    /// Returns 0 on success.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "as_init")]
    public static int Init(byte* resourceDir, byte* libDir)
    {
        try
        {
            string resources = Utf8(resourceDir) ?? "";
            string libs = Utf8(libDir) ?? "";
            Platform.Initialize(resources, libs);
            return 0;
        }
        catch (Exception ex) { Record(ex); return -1; }
    }

    /// <summary>Creates the state for one screen. <paramref name="screenIndex"/> counts screens left to right.</summary>
    [UnmanagedCallersOnly(EntryPoint = "as_view_create")]
    public static nint ViewCreate(int screenIndex, int isPreview)
    {
        try
        {
            _ = Scene; // load catalogs now rather than on the first frame
            return GCHandle.ToIntPtr(GCHandle.Alloc(new View(screenIndex, isPreview != 0)));
        }
        catch (Exception ex) { Record(ex); return 0; }
    }

    /// <summary>
    /// Advances the clock and draws one frame into a caller-owned 32-bit premultiplied BGRA buffer
    /// (CoreGraphics: kCGImageAlphaPremultipliedFirst | kCGBitmapByteOrder32Little).
    /// <paramref name="density"/> is the screen's backing scale factor (2 on Retina). Returns 0 on success.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "as_view_render")]
    public static int ViewRender(nint view, byte* pixels, int width, int height, int stride, float density)
    {
        try
        {
            if (view == 0 || pixels is null || width <= 0 || height <= 0) return -2;
            var v = (View)GCHandle.FromIntPtr(view).Target!;
            var settings = Settings;
            var scene = Scene;
            scene.Tick();

            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, (nint)pixels, stride)
                ?? throw new InvalidOperationException("Could not wrap the frame buffer in a Skia surface.");
            double offset = settings.PanoramaAcrossMonitors && !v.Preview ? v.ScreenIndex * settings.FieldOfViewDeg : 0;
            v.Renderer.Draw(surface.Canvas, new SKSizeI(width, height), scene, settings, offset,
                v.Preview ? 0.5f : Math.Max(1f, density));
            surface.Canvas.Flush();
            return 0;
        }
        catch (Exception ex) { Record(ex); return -1; }
    }

    [UnmanagedCallersOnly(EntryPoint = "as_view_destroy")]
    public static void ViewDestroy(nint view)
    {
        try
        {
            if (view == 0) return;
            var handle = GCHandle.FromIntPtr(view);
            (handle.Target as View)?.Renderer.Dispose();
            handle.Free();
        }
        catch (Exception ex) { Record(ex); }
    }

    /// <summary>
    /// The settings sheet's contents as JSON: every simple setting with its group, label, help, kind and
    /// current value (see <see cref="SettingsSchema"/>). Free the result with <c>as_free</c>. Null on failure.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "as_settings_schema")]
    public static byte* SettingsSchemaJson()
    {
        try { return Alloc(SettingsSchema.Describe(Settings)); }
        catch (Exception ex) { Record(ex); return null; }
    }

    /// <summary>
    /// Applies a JSON object of <c>{"PropertyName": value}</c> pairs from the sheet, saves settings.json and
    /// restarts the scene so the change shows at once. Returns 0 on success.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "as_settings_apply")]
    public static int SettingsApply(byte* json)
    {
        try
        {
            SettingsSchema.Apply(Settings, Utf8(json) ?? "{}");
            Settings.Save();
            _scene = null;
            return 0;
        }
        catch (Exception ex) { Record(ex); return -1; }
    }

    /// <summary>Forgets in-memory settings and reloads settings.json (the sheet's Cancel, or after editing the file).</summary>
    [UnmanagedCallersOnly(EntryPoint = "as_settings_reload")]
    public static void SettingsReload()
    {
        _settings = null;
        _scene = null;
    }

    /// <summary>Full path of settings.json, for the sheet's "Open settings file" button. Free with <c>as_free</c>.</summary>
    [UnmanagedCallersOnly(EntryPoint = "as_settings_path")]
    public static byte* SettingsPath()
    {
        try
        {
            if (!File.Exists(SaverSettings.FilePath)) Settings.Save(); // so there is something to open
            return Alloc(SaverSettings.FilePath);
        }
        catch (Exception ex) { Record(ex); return null; }
    }

    /// <summary>Copyright, GPL notice and credits for the foot of the settings sheet. Free with <c>as_free</c>.</summary>
    [UnmanagedCallersOnly(EntryPoint = "as_about_text")]
    public static byte* AboutText()
    {
        try { return Alloc(AboutInfo.Text()); }
        catch (Exception ex) { Record(ex); return null; }
    }

    /// <summary>The message of the last failure, owned by the library (do not free).</summary>
    [UnmanagedCallersOnly(EntryPoint = "as_last_error")]
    public static byte* LastError()
    {
        if (_lastErrorUtf8 is null) _lastErrorUtf8 = Alloc(_lastError);
        return _lastErrorUtf8;
    }

    [UnmanagedCallersOnly(EntryPoint = "as_free")]
    public static void Free(void* p) => NativeMemory.Free(p);

    // ---- helpers ---------------------------------------------------------------

    private static void Record(Exception ex)
    {
        _lastError = ex.ToString();
        if (_lastErrorUtf8 is not null) NativeMemory.Free(_lastErrorUtf8);
        _lastErrorUtf8 = null;
    }

    private static string? Utf8(byte* s) => s is null ? null : Marshal.PtrToStringUTF8((nint)s);

    private static byte* Alloc(string s)
    {
        int n = Encoding.UTF8.GetByteCount(s);
        var p = (byte*)NativeMemory.Alloc((nuint)n + 1);
        Encoding.UTF8.GetBytes(s, new Span<byte>(p, n));
        p[n] = 0;
        return p;
    }
}
