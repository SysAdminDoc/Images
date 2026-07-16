using System.Windows.Media;
using System.Windows.Media.Imaging;
using Images.Services;

namespace Images.Tests;

public sealed class ImageInvertServiceTests
{
    [Fact]
    public void Invert_RedPixel_BecomesCyanAndPreservesAlpha()
    {
        var source = Solid(Color.FromArgb(200, 255, 0, 0));

        var inverted = ReadPixel(ImageInvertService.Invert(source));

        Assert.Equal(200, inverted.A);
        Assert.Equal(0, inverted.R);
        Assert.Equal(255, inverted.G);
        Assert.Equal(255, inverted.B);
    }

    [Fact]
    public void Invert_Twice_RestoresOriginalColour()
    {
        var source = Solid(Color.FromArgb(255, 30, 120, 200));

        var roundTrip = ReadPixel(ImageInvertService.Invert(ImageInvertService.Invert(source)));

        Assert.Equal(30, roundTrip.R);
        Assert.Equal(120, roundTrip.G);
        Assert.Equal(200, roundTrip.B);
    }

    [Fact]
    public void Invert_ProducesFrozenResultSoItCanCrossThreads()
        => Assert.True(ImageInvertService.Invert(Solid(Colors.Gray)).IsFrozen);

    private static BitmapSource Solid(Color color)
    {
        var stride = 4;
        var pixels = new byte[] { color.B, color.G, color.R, color.A };
        var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static Color ReadPixel(BitmapSource source)
    {
        var pixels = new byte[4];
        source.CopyPixels(pixels, 4, 0);
        return Color.FromArgb(pixels[3], pixels[2], pixels[1], pixels[0]);
    }
}
