using AlienSkies.Core.Catalog;
using Android.Content;

namespace AlienSkies.Droid;

/// <summary>One-time wiring shared by the screen saver, the live wallpaper and the settings app.</summary>
internal static class AppSetup
{
    private static bool _done;

    /// <summary>
    /// Catalog search order on Android: the app's files folder (full catalogs copied in later),
    /// then APK assets under Assets/data (bundled at build time), then the embedded samples.
    /// </summary>
    public static void EnsureInitialized(Context context)
    {
        if (_done) return;
        _done = true;

        var assets = context.ApplicationContext!.Assets!;
        CatalogLoader.PlatformSource = fileName =>
        {
            try
            {
                // AssetManager streams aren't seekable; copy so callers can treat it like a file.
                using var asset = assets.Open("data/" + fileName);
                var copy = new MemoryStream();
                asset.CopyTo(copy);
                copy.Position = 0;
                return copy;
            }
            catch (Java.IO.FileNotFoundException)
            {
                return null;
            }
        };
    }
}
