using AlienSkies.Rendering;
using SkiaSharp;

namespace AlienSkies.Core.Tests;

public class MilkyWayStyleTests
{
    // Looking south from 30°S on the default planet: the band crosses the middle of the view.
    private static SaverSettings Settings(bool show, MilkyWayStyle style) => new()
    {
        TimeScale = 0,
        PlanetName = "alf Cen A (hypothetical Earth)",
        LatitudeDeg = -30, ViewAzimuthDeg = 180, ViewAltitudeDeg = 50,
        ShowInfo = false, ShowConstellations = false,
        ShowMilkyWay = show, MilkyWayStyle = style,
    };

    private static byte[] Luma(SaverSettings s)
    {
        var size = new SKSizeI(320, 180);
        using var surface = SKSurface.Create(new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var renderer = new SkyRenderer();
        renderer.Draw(surface.Canvas, size, new SkyScene(s), s, 0);
        using var img = surface.Snapshot();
        using var pixmap = img.PeekPixels();
        var px = pixmap.GetPixelSpan();
        var luma = new byte[size.Width * size.Height];
        for (int i = 0; i < luma.Length; i++) luma[i] = (byte)((px[4 * i] * 3 + px[4 * i + 1] * 6 + px[4 * i + 2]) / 10);
        return luma;
    }

    [Fact]
    public void DefaultStyle_IsTheCheapTint() => Assert.Equal(MilkyWayStyle.Tint, new SaverSettings().MilkyWayStyle);

    [Fact]
    public void Tint_LightsTheSameBandAsTheDetailedTexture()
    {
        var off = Luma(Settings(false, MilkyWayStyle.Tint));
        var tint = Luma(Settings(true, MilkyWayStyle.Tint));
        var detailed = Luma(Settings(true, MilkyWayStyle.Detailed));

        int bright = 0, alsoTinted = 0;
        for (int i = 0; i < off.Length; i++)
        {
            if (detailed[i] - off[i] < 12) continue; // clearly inside the detailed band
            bright++;
            if (tint[i] > off[i]) alsoTinted++;
        }
        Assert.True(bright > 1000, $"only {bright} band pixels in the detailed view");
        Assert.True(alsoTinted > 0.85 * bright, $"tint covers {alsoTinted} of {bright} band pixels");
        // At its brightest the tint is no brighter than the detailed band's brightest star cloud.
        int tintMost = 0, detailedMost = 0;
        for (int i = 0; i < off.Length; i++)
        {
            tintMost = System.Math.Max(tintMost, tint[i] - off[i]);
            detailedMost = System.Math.Max(detailedMost, detailed[i] - off[i]);
        }
        Assert.True(tintMost <= detailedMost + 10, $"tint lightens a pixel by {tintMost}, detailed by {detailedMost}");
    }
}
