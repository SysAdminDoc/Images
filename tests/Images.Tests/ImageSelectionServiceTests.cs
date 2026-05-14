using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Services;

namespace Images.Tests;

public sealed class ImageSelectionServiceTests
{
    [Fact]
    public void CreateSelection_NormalizesDragAndClampsToImageBounds()
    {
        var selection = ImageSelectionService.CreateSelection(
            new PixelCoordinate(4, 3),
            new PixelCoordinate(1, 1),
            pixelWidth: 3,
            pixelHeight: 3);

        Assert.Equal(new PixelSelection(1, 1, 2, 2), selection);
    }

    [Fact]
    public void Normalize_RejectsEmptyOrDimensionlessSelections()
    {
        Assert.Null(ImageSelectionService.Normalize(new PixelSelection(0, 0, 0, 2), 10, 10));
        Assert.Null(ImageSelectionService.Normalize(new PixelSelection(0, 0, 2, 2), 0, 10));
        Assert.Null(ImageSelectionService.Normalize(null, 10, 10));
    }

    [Fact]
    public void ExtractSelection_ReturnsBgraBitmapForSelectedPixels()
    {
        var source = BitmapSource.Create(
            3,
            2,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[]
            {
                0x01, 0x02, 0x03, 0xFF, 0x04, 0x05, 0x06, 0xFF, 0x07, 0x08, 0x09, 0xFF,
                0x0A, 0x0B, 0x0C, 0xFF, 0x0D, 0x0E, 0x0F, 0x80, 0x10, 0x11, 0x12, 0xFF
            },
            12);
        source.Freeze();

        var extracted = ImageSelectionService.ExtractSelection(source, new PixelSelection(1, 0, 2, 2));

        Assert.Equal(2, extracted.PixelWidth);
        Assert.Equal(2, extracted.PixelHeight);
        Assert.Equal(PixelFormats.Bgra32, extracted.Format);

        var pixels = new byte[16];
        extracted.CopyPixels(pixels, 8, 0);
        Assert.Equal(
            [
                0x04, 0x05, 0x06, 0xFF, 0x07, 0x08, 0x09, 0xFF,
                0x0D, 0x0E, 0x0F, 0x80, 0x10, 0x11, 0x12, 0xFF
            ],
            pixels);
    }
}
