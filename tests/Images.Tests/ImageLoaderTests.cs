using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class ImageLoaderTests
{
    [Fact]
    public void QuickDimensions_NonexistentFile_ReturnsZeroPair()
    {
        var (w, h) = ImageLoader.QuickDimensions(@"C:\__nonexistent__\test.jpg");
        Assert.Equal(0, w);
        Assert.Equal(0, h);
    }

    [Fact]
    public void QuickDimensions_EmptyPath_ReturnsZeroPair()
    {
        var (w, h) = ImageLoader.QuickDimensions(string.Empty);
        Assert.Equal(0, w);
        Assert.Equal(0, h);
    }

    [Theory]
    [InlineData(2, 20)]
    [InlineData(1, 100)]
    public void Load_AnimatedGif_PreservesValidTwentyMillisecondDelay(int centiseconds, int expectedMilliseconds)
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "animated.gif");
        WriteAnimatedGif(path, centiseconds);

        var result = ImageLoader.Load(path);

        Assert.NotNull(result.Animation);
        Assert.All(
            result.Animation.Delays,
            delay => Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), delay));
    }

    private static void WriteAnimatedGif(string path, int centiseconds)
    {
        using var collection = new MagickImageCollection();
        var first = CreateGifFrame(MagickColors.Red, centiseconds);
        var second = CreateGifFrame(MagickColors.Blue, centiseconds);

        collection.Add(first);
        collection.Add(second);
        collection.Write(path);
    }

    private static MagickImage CreateGifFrame(IMagickColor<ushort> color, int centiseconds)
    {
        var frame = new MagickImage(color, 6, 4)
        {
            Format = MagickFormat.Gif,
            AnimationDelay = (uint)centiseconds,
            AnimationIterations = 0
        };
        return frame;
    }
}
