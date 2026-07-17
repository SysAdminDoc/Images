using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Controls;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;

namespace Images.Tests;

public sealed class SkiaBitmapPresenterTests
{
    private const double PinnedDpi = 96.0;
    private const byte ChannelTolerance = 1;

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

    [Fact]
    public void Draw_NearestNeighbor_MatchesPinnedDpiGoldenImage()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(2, 2, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.SetPixel(0, 0, SKColors.Red);
        bitmap.SetPixel(1, 0, SKColors.Lime);
        bitmap.SetPixel(0, 1, SKColors.Blue);
        bitmap.SetPixel(1, 1, SKColors.Yellow);
        var target = new SKImageInfo(
            DipsToPixels(4, PinnedDpi),
            DipsToPixels(4, PinnedDpi),
            SKColorType.Bgra8888,
            SKAlphaType.Premul);
        using var surface = SKSurface.Create(target);

        SkiaBitmapPresenter.Draw(surface.Canvas, target, bitmap, nearestNeighbor: true);

        using var snapshot = surface.Snapshot();
        using var actual = snapshot.PeekPixels();
        var goldenPath = Path.Combine(AppContext.BaseDirectory, "render", "skia-nearest-96dpi.ppm");
        using var expected = Image.Load<Rgba32>(goldenPath);
        AssertMatchesGolden(actual, expected, ChannelTolerance);
    }

    private static int DipsToPixels(double dips, double dpi)
        => checked((int)Math.Round(dips * dpi / 96.0, MidpointRounding.AwayFromZero));

    private static void AssertMatchesGolden(SKPixmap actual, Image<Rgba32> expected, byte tolerance)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);

        expected.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < expected.Height; y++)
            {
                var expectedRow = accessor.GetRowSpan(y);
                for (var x = 0; x < expected.Width; x++)
                {
                    var expectedPixel = expectedRow[x];
                    var actualPixel = actual.GetPixelColor(x, y);
                    AssertChannelWithinTolerance(x, y, "R", expectedPixel.R, actualPixel.Red, tolerance);
                    AssertChannelWithinTolerance(x, y, "G", expectedPixel.G, actualPixel.Green, tolerance);
                    AssertChannelWithinTolerance(x, y, "B", expectedPixel.B, actualPixel.Blue, tolerance);
                    AssertChannelWithinTolerance(x, y, "A", expectedPixel.A, actualPixel.Alpha, tolerance);
                }
            }
        });
    }

    private static void AssertChannelWithinTolerance(
        int x,
        int y,
        string channel,
        byte expected,
        byte actual,
        byte tolerance)
    {
        var delta = Math.Abs(expected - actual);
        Assert.True(
            delta <= tolerance,
            $"Pixel ({x}, {y}) {channel} differed by {delta}; expected {expected}, actual {actual}, tolerance {tolerance}.");
    }

    private static void AssertRow(SKPixmap pixels, int y, params SKColor[] expected)
    {
        for (var x = 0; x < expected.Length; x++)
            Assert.Equal(expected[x], pixels.GetPixelColor(x, y));
    }
}
