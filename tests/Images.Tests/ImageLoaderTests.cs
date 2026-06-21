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
}
