using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Services;

namespace Images.Tests;

public sealed class ScanFilterServiceTests
{
    [Fact]
    public void ApplyOldScanFilter_ConvertsToContrastGrayscaleAndPreservesAlpha()
    {
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 0x20, 0xB0, 0xF8, 0x80 },
            4);
        bitmap.Freeze();

        var filtered = ScanFilterService.ApplyOldScanFilter(bitmap);

        var pixel = new byte[4];
        filtered.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0);

        Assert.Equal(pixel[0], pixel[1]);
        Assert.Equal(pixel[1], pixel[2]);
        Assert.Equal(0x80, pixel[3]);
        Assert.True(pixel[0] > 0x20);
        Assert.True(filtered.IsFrozen);
    }
}
