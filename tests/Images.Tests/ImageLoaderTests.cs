using System.IO;
using System.Windows.Media.Imaging;
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

    [Fact]
    public void Load_MalformedRaster_FailsAtPreflightBeforeFullDecode()
    {
        using var temp = TestDirectory.Create();
        var path = temp.WriteFile("broken.png", "not an image");

        var error = Assert.Throws<InvalidOperationException>(() => ImageLoader.Load(path));

        Assert.Contains("refused to decode", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dimensions", error.Message, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void LoadPreviewImage_CapsLongestEdge()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "large.png");
        using (var image = new MagickImage(MagickColors.Red, 2000, 1000))
            image.Write(path);

        var preview = Assert.IsAssignableFrom<BitmapSource>(ImageLoader.LoadPreviewImage(path, maxPixelDimension: 320));

        Assert.Equal(320, Math.Max(preview.PixelWidth, preview.PixelHeight));
    }

    [Fact]
    public void LoadRasterBytes_PrePatchWicJpeg_UsesMagickSecurityFallback()
    {
        using var image = new MagickImage(MagickColors.Coral, 8, 6) { Format = MagickFormat.Jpeg };
        var bytes = image.ToByteArray();
        var prePatch = WicJpegSecurityPolicy.Evaluate(new Version(10, 0, 26100, 4945));

        var result = ImageLoader.LoadRasterBytes(bytes, "fixture.jpg", ".jpg", prePatch);

        Assert.True(result.WicJpegSecurityFallback);
        Assert.Contains("Magick.NET", result.DecoderUsed, StringComparison.Ordinal);
        Assert.Contains("Windows update recommended", result.DecoderUsed, StringComparison.Ordinal);
    }

    [Fact]
    public void LoadRasterBytes_PatchedWicJpeg_KeepsNativeDecode()
    {
        using var image = new MagickImage(MagickColors.Coral, 8, 6) { Format = MagickFormat.Jpeg };
        var bytes = image.ToByteArray();
        var patched = WicJpegSecurityPolicy.Evaluate(new Version(10, 0, 26100, 4946));

        var result = ImageLoader.LoadRasterBytes(bytes, "fixture.jpg", ".jpg", patched);

        Assert.False(result.WicJpegSecurityFallback);
        Assert.Equal("WIC", result.DecoderUsed);
    }

    [Fact]
    public void Load_SixteenBitTiff_ReportsTonemappedDecode()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "sixteen-bit.tiff");
        using (var image = new MagickImage(MagickColors.Gold, 4, 4) { Format = MagickFormat.Tiff, Depth = 16 })
            image.Write(path);

        var result = ImageLoader.Load(path);

        Assert.Contains("tonemapped to SDR", result.DecoderUsed, StringComparison.Ordinal);
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

    private static MagickImage CreateGifFrame(IMagickColor<float> color, int centiseconds)
    {
        var frame = new MagickImage(color, 6, 4)
        {
            Format = MagickFormat.Gif,
            AnimationDelay = (uint)centiseconds,
            AnimationIterations = 0
        };
        return frame;
    }

    [Fact]
    public void TransformToSrgbIfProfiled_UntaggedImage_ReturnsFalse()
    {
        using var image = new MagickImage(MagickColors.Red, 4, 4);

        Assert.Null(image.GetColorProfile());
        Assert.False(ImageLoader.TransformToSrgbIfProfiled(image));
    }

    [Fact]
    public void TransformToSrgbIfProfiled_WideGamutProfile_ConvertsToSrgb()
    {
        using var image = new MagickImage(MagickColors.Red, 4, 4);
        image.SetProfile(ColorProfiles.AdobeRGB1998);
        var before = ColorProfiles.AdobeRGB1998.ToByteArray();

        var managed = ImageLoader.TransformToSrgbIfProfiled(image);

        Assert.True(managed);
        var after = image.GetColorProfile();
        Assert.NotNull(after);
        // The embedded profile is now sRGB, not the original Adobe RGB.
        Assert.False(after!.ToByteArray().AsSpan().SequenceEqual(before));
    }

    [Fact]
    public void TransformForDisplayIfProfiled_LegacySdr_EmbedsMonitorDestination()
    {
        using var image = new MagickImage(MagickColors.Red, 4, 4);
        image.SetProfile(ColorProfiles.AdobeRGB1998);
        var destinationBytes = ColorProfiles.AppleRGB.ToByteArray();
        var display = DisplayColorService.CreateStateForTest(
            @"\\.\DISPLAY2",
            advancedColorKnown: true,
            advancedColorEnabled: false,
            profilePath: @"C:\Color\WideGamut.icc",
            profileData: destinationBytes);

        var target = ImageLoader.TransformForDisplayIfProfiled(image, display);

        Assert.Contains("monitor ICC", target, StringComparison.Ordinal);
        var embedded = Assert.IsAssignableFrom<IColorProfile>(image.GetColorProfile());
        Assert.True(embedded.ToByteArray().AsSpan().SequenceEqual(destinationBytes));
    }

    [Fact]
    public void TransformForDisplayIfProfiled_AdvancedColor_EmbedsSrgbDestination()
    {
        using var image = new MagickImage(MagickColors.Red, 4, 4);
        image.SetProfile(ColorProfiles.AdobeRGB1998);
        var sourceBytes = ColorProfiles.AdobeRGB1998.ToByteArray();
        var display = DisplayColorService.CreateStateForTest(
            @"\\.\DISPLAY1",
            advancedColorKnown: true,
            advancedColorEnabled: true,
            profilePath: @"C:\Color\WideGamut.icc",
            profileData: ColorProfiles.AppleRGB.ToByteArray(),
            bitsPerColorChannel: 10);

        var target = ImageLoader.TransformForDisplayIfProfiled(image, display);

        Assert.Equal("sRGB", target);
        var embedded = Assert.IsAssignableFrom<IColorProfile>(image.GetColorProfile());
        Assert.False(embedded.ToByteArray().AsSpan().SequenceEqual(sourceBytes));
    }
}
