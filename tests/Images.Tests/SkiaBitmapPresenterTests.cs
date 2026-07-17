using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Controls;
using SkiaSharp;

namespace Images.Tests;

public sealed class SkiaBitmapPresenterTests
{
    [Fact]
    public void CopyToSkia_PreservesBgraAndPremultipliesAlpha()
    {
        var source = BitmapSource.Create(
            2, 1, 96, 96, PixelFormats.Bgra32, null,
            new byte[]
            {
                0xFF, 0x00, 0x00, 0xFF,
                0x00, 0xFF, 0x00, 0x80,
            },
            8);
        source.Freeze();

        using var bitmap = SkiaBitmapPresenter.CopyToSkia(source);
        var actual = new byte[8];
        Marshal.Copy(bitmap.GetPixels(), actual, 0, actual.Length);

        Assert.Equal(
            new byte[]
            {
                0xFF, 0x00, 0x00, 0xFF,
                0x00, 0x80, 0x00, 0x80,
            },
            actual);
    }

    [Fact]
    public void Draw_NearestNeighborGoldenFixture_IsUniformCenteredAndTransparentOutside()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(2, 1, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.SetPixel(0, 0, SKColors.Red);
        bitmap.SetPixel(1, 0, SKColors.Blue);
        var target = new SKImageInfo(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(target);

        SkiaBitmapPresenter.Draw(surface.Canvas, target, bitmap, nearestNeighbor: true);

        using var snapshot = surface.Snapshot();
        using var pixels = snapshot.PeekPixels();
        var transparentBlack = new SKColor(0, 0, 0, 0);
        AssertRow(pixels, 0, transparentBlack, transparentBlack, transparentBlack, transparentBlack);
        AssertRow(pixels, 1, SKColors.Red, SKColors.Red, SKColors.Blue, SKColors.Blue);
        AssertRow(pixels, 2, SKColors.Red, SKColors.Red, SKColors.Blue, SKColors.Blue);
        AssertRow(pixels, 3, transparentBlack, transparentBlack, transparentBlack, transparentBlack);
    }

    private static void AssertRow(SKPixmap pixels, int y, params SKColor[] expected)
    {
        for (var x = 0; x < expected.Length; x++)
            Assert.Equal(expected[x], pixels.GetPixelColor(x, y));
    }
}
