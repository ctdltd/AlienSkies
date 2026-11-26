using AlienSkies.Rendering;
using AlienSkies.Screensaver.Settings;

namespace AlienSkies.Screensaver;

/// <summary>
/// Windows screen saver entry point. Windows launches a .scr with:
///   /s            run full screen
///   /p &lt;hwnd&gt;     draw inside the Screen Saver Settings preview (also "/p:hwnd")
///   /c[:hwnd]     show settings (also what an argument-less double-click means)
/// Extra, for development:
///   /w            run in a normal resizable window (Esc closes)
/// Arguments may arrive upper-case, with ':' or a space before the handle.
/// </summary>
internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var (mode, handle) = ParseArgs(args);
        var settings = SaverSettings.Load();

        switch (mode)
        {
            case 's':
                RunFullScreen(settings);
                break;
            case 'p' when handle != IntPtr.Zero:
                Application.Run(new ScreenSaverForm(new SkyScene(settings), settings, SaverMode.Preview, Rectangle.Empty, 0, handle));
                break;
            case 'w':
                Application.Run(new ScreenSaverForm(new SkyScene(settings), settings, SaverMode.Windowed, Rectangle.Empty));
                break;
            default:
                Application.Run(new SettingsForm(settings));
                break;
        }
    }

    private static void RunFullScreen(SaverSettings settings)
    {
        var scene = new SkyScene(settings); // one shared clock/planet for all monitors
        var screens = Screen.AllScreens.OrderBy(s => s.Bounds.X).ThenBy(s => s.Bounds.Y).ToArray();
        var forms = screens.Select((screen, i) => new ScreenSaverForm(
            scene, settings, SaverMode.FullScreen, screen.Bounds,
            settings.PanoramaAcrossMonitors ? i * settings.FieldOfViewDeg : 0)).ToArray();

        foreach (var f in forms.Skip(1)) f.Show();
        Application.Run(forms[0]);
    }

    internal static (char Mode, IntPtr Handle) ParseArgs(string[] args)
    {
        if (args.Length == 0) return ('c', IntPtr.Zero);

        string first = args[0].Trim().ToLowerInvariant().TrimStart('/', '-');
        if (first.Length == 0) return ('c', IntPtr.Zero);

        char mode = first[0];
        string? handleText = first.Length > 2 && first[1] == ':' ? first[2..] : args.Length > 1 ? args[1] : null;
        IntPtr handle = long.TryParse(handleText, out var h) ? new IntPtr(h) : IntPtr.Zero;
        return (mode, handle);
    }
}
