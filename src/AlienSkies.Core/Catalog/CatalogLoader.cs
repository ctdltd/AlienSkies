using System.Globalization;
using System.Reflection;
using AlienSkies.Core.Astronomy;

namespace AlienSkies.Core.Catalog;

/// <summary>
/// Loads star, exoplanet and constellation-line catalogs.
/// Search order for each file: %APPDATA%\AlienSkies\data, &lt;exe dir&gt;\data, then the embedded sample.
/// Drop full catalogs produced by tools/build_catalog.py into either folder to override the samples.
/// </summary>
public static class CatalogLoader
{
    public const string StarsFile = "stars.csv";
    public const string ExoplanetsFile = "exoplanets.csv";
    public const string ConstellationsFile = "constellations.csv";

    public static IEnumerable<string> SearchFolders()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlienSkies", "data");
        yield return Path.Combine(AppContext.BaseDirectory, "data");
    }

    public static IReadOnlyList<Star> LoadStars() => ParseStars(Open(StarsFile));
    public static IReadOnlyList<ExoplanetSystem> LoadExoplanets() => ParseExoplanets(Open(ExoplanetsFile));
    public static IReadOnlyList<(string Constellation, string FromId, string ToId)> LoadConstellationLines() =>
        ParseConstellations(Open(ConstellationsFile));

    private static TextReader Open(string fileName)
    {
        foreach (var folder in SearchFolders())
        {
            var path = Path.Combine(folder, fileName);
            if (File.Exists(path)) return new StreamReader(path);
        }
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AlienSkies.Data." + fileName)
            ?? throw new FileNotFoundException($"No catalog file '{fileName}' found on disk or embedded.");
        return new StreamReader(stream);
    }

    // stars.csv: hip,name,ra_deg,dec_deg,plx_mas,vmag,bv
    public static IReadOnlyList<Star> ParseStars(TextReader reader)
    {
        var stars = new List<Star>();
        foreach (var f in Rows(reader))
        {
            if (!TryD(f, 4, out var plx) || plx <= 0) continue; // no usable distance
            if (!TryD(f, 2, out var ra) || !TryD(f, 3, out var dec) || !TryD(f, 5, out var v)) continue;
            double bv = TryD(f, 6, out var b) ? b : 0.6;
            double d = Equatorial.ParallaxToParsecs(plx);
            stars.Add(new Star(f[0], Blank(f, 1), Equatorial.ToCartesian(ra, dec, d), Units.AbsoluteMagnitude(v, d), bv));
        }
        return stars;
    }

    // exoplanets.csv: pl_name,hostname,ra,dec,sy_dist,sy_vmag,st_rad,st_mass,pl_orbsmax,pl_orbper,pl_orbincl,pl_orbeccen
    public static IReadOnlyList<ExoplanetSystem> ParseExoplanets(TextReader reader)
    {
        var list = new List<ExoplanetSystem>();
        foreach (var f in Rows(reader))
        {
            if (!TryD(f, 2, out var ra) || !TryD(f, 3, out var dec) || !TryD(f, 4, out var dist) || dist <= 0) continue;
            list.Add(new ExoplanetSystem(f[0], f[1], ra, dec, dist,
                N(f, 5), N(f, 6), N(f, 7), N(f, 8), N(f, 9), N(f, 10), N(f, 11)));
        }
        return list;
    }

    // constellations.csv: constellation,from_id,to_id   (ids match stars.csv 'hip')
    public static IReadOnlyList<(string, string, string)> ParseConstellations(TextReader reader) =>
        Rows(reader).Where(f => f.Length >= 3).Select(f => (f[0], f[1], f[2])).ToList();

    private static IEnumerable<string[]> Rows(TextReader reader)
    {
        using (reader)
        {
            string? line;
            bool header = true;
            while ((line = reader.ReadLine()) != null)
            {
                if (header) { header = false; continue; }
                if (line.Length == 0 || line[0] == '#') continue;
                yield return line.Split(',').Select(s => s.Trim()).ToArray();
            }
        }
    }

    private static bool TryD(string[] f, int i, out double value)
    {
        value = 0;
        return i < f.Length && double.TryParse(f[i], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static double? N(string[] f, int i) => TryD(f, i, out var v) ? v : null;
    private static string? Blank(string[] f, int i) => i < f.Length && f[i].Length > 0 ? f[i] : null;
}
