using AlienSkies.Core.Astronomy;
using AlienSkies.Core.Catalog;
using AlienSkies.Core.Math;

namespace AlienSkies.Core.Tests;

public class ConstellationTests
{
    // Figure stars whose Hipparcos parallax is unusable (distant supergiants, β Phe and similar),
    // so they have no 3-D position. Their segments are skipped at draw time.
    private static readonly HashSet<string> NoParallax = ["5165", "22783", "31216", "33165", "54463", "89341", "92202"];

    private static readonly IReadOnlyList<(string Constellation, string FromId, string ToId)> Lines =
        CatalogLoader.LoadConstellationLines();
    private static readonly IReadOnlyList<Star> Stars = CatalogLoader.LoadStars();

    [Fact]
    public void Embedded_HasAll88Constellations()
    {
        var names = Lines.Select(l => l.Constellation).ToHashSet();
        Assert.Equal(88, names.Count);
        Assert.Contains("Orion", names);
        Assert.Contains("Ursa Major", names);
        Assert.Contains("Crux", names);
        Assert.Contains("Serpens", names); // the one constellation in two parts
        Assert.True(Lines.Count > 650, $"only {Lines.Count} segments");
    }

    [Fact]
    public void Embedded_EveryFigureStarIsInTheSampleCatalog()
    {
        var ids = Stars.Select(s => s.Id).ToHashSet();
        var missing = Lines.SelectMany(l => new[] { l.FromId, l.ToId }).Where(id => !ids.Contains(id)).ToHashSet();
        Assert.Subset(NoParallax, missing);
    }

    [Fact]
    public void Embedded_HasNoDegenerateOrDuplicateSegments()
    {
        Assert.DoesNotContain(Lines, l => l.FromId == l.ToId);
        var keys = Lines.Select(l => string.CompareOrdinal(l.FromId, l.ToId) < 0 ? (l.FromId, l.ToId) : (l.ToId, l.FromId));
        Assert.Equal(Lines.Count, keys.Distinct().Count());
    }

    [Fact]
    public void FromEarth_OrionsBeltSpansAboutThreeDegrees()
    {
        var byId = Stars.ToDictionary(s => s.Id);
        // Mintaka to Alnitak, the ends of the belt: 2.7° on our sky.
        Assert.Equal(2.7, Separation(byId["25930"].Position.Normalized(), byId["26727"].Position.Normalized()), 1);
    }

    [Theory]
    [InlineData("Proxima Cen b", 0.0, 0.15)]   // 1.3 pc: figures still recognisable
    [InlineData("TRAPPIST-1 e", 0.3, 10.0)]    // 12 pc: most figures badly broken
    public void Figures_WarpMoreTheFartherYouGo(string planet, double minMedian, double maxMedian)
    {
        var system = CatalogLoader.LoadExoplanets().Single(p => p.PlanetName == planet);
        var alien = AlienSky.Build(system, Stars, limitingMagnitude: 99, includeSol: false)
            .Stars.ToDictionary(s => s.Id);
        var home = Stars.ToDictionary(s => s.Id);

        var changes = new List<double>();
        foreach (var (_, a, b) in Lines)
        {
            if (!alien.TryGetValue(a, out var aa) || !alien.TryGetValue(b, out var ab)) continue;
            double earth = Separation(home[a].Position.Normalized(), home[b].Position.Normalized());
            changes.Add(System.Math.Abs(Separation(aa.Direction, ab.Direction) - earth) / earth);
        }

        changes.Sort();
        Assert.InRange(changes[changes.Count / 2], minMedian, maxMedian);
    }

    private static double Separation(Vector3d u, Vector3d v) =>
        System.Math.Acos(System.Math.Clamp(Vector3d.Dot(u, v), -1, 1)) / Units.Deg;
}
