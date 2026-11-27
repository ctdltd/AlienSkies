using System.Runtime.InteropServices;
using AlienSkies.Core.Catalog;
using SkiaSharp;

namespace AlienSkies.Mac;

/// <summary>
/// Wiring that differs from the other hosts. Inside a .saver, AppContext.BaseDirectory is the folder of the
/// process that loaded us (Apple's legacyScreenSaver), not our bundle, so both the catalogs and libSkiaSharp
/// are looked up in folders the Objective-C side passes in.
/// </summary>
internal static class Platform
{
    private static bool _done;

    public static void Initialize(string resourceDir, string libDir)
    {
        if (_done) return;
        _done = true;

        if (libDir.Length > 0)
        {
            NativeLibrary.SetDllImportResolver(typeof(SKObject).Assembly, (name, _, _) =>
                name.Contains("SkiaSharp", StringComparison.OrdinalIgnoreCase) &&
                NativeLibrary.TryLoad(Path.Combine(libDir, LibraryFile(name)), out var lib) ? lib : 0);
        }

        // GetFolderPath answers "" while the folder is missing (as ~/.config is in a fresh screen saver sandbox),
        // which would put settings.json in the working directory. Creating it here fixes that for every later call.
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create);

        // Search order on macOS: ~/.config/AlienSkies/data (inside the screen saver's sandbox container),
        // then the bundle's Contents/Resources/data, then the samples embedded in AlienSkies.Core.
        string data = Path.Combine(resourceDir, "data");
        CatalogLoader.PlatformSource = fileName =>
        {
            var path = Path.Combine(data, fileName);
            return File.Exists(path) ? File.OpenRead(path) : null;
        };
    }

    private static string LibraryFile(string name)
    {
        string ext = OperatingSystem.IsMacOS() ? ".dylib" : ".so";
        string file = name.StartsWith("lib", StringComparison.Ordinal) ? name : "lib" + name;
        return file.EndsWith(ext, StringComparison.Ordinal) ? file : file + ext;
    }
}
