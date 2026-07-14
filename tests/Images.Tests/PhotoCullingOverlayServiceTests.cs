using Images.Services;

namespace Images.Tests;

public sealed class PhotoCullingOverlayServiceTests
{
    [Fact]
    public void AnalyzeBgra32_FocusPeakingMarksHighContrastEdges()
    {
        const int width = 5;
        const int height = 5;
        var source = SolidBgra(width, height, 24);
        for (var y = 0; y < height; y++)
        {
            for (var x = 3; x < width; x++)
                SetGray(source, width, x, y, 230);
        }

        var result = PhotoCullingOverlayService.AnalyzeBgra32(
            source,
            width,
            height,
            width * 4,
            PhotoCullingOverlayMode.FocusPeaking);

        var edge = Offset(width, 2, 2);
        Assert.Equal(76, result[edge]);
        Assert.Equal(255, result[edge + 1]);
        Assert.Equal(76, result[edge + 2]);
        Assert.Equal(220, result[edge + 3]);
        Assert.Equal(0, result[Offset(width, 1, 2) + 3]);
    }

    [Fact]
    public void AnalyzeBgra32_ExposureClippingMarksHighlightsAndShadowsWithDistinctColors()
    {
        const int width = 3;
        var source = SolidBgra(width, 1, 128);
        SetGray(source, width, 0, 0, 0);
        SetGray(source, width, 2, 0, 255);

        var result = PhotoCullingOverlayService.AnalyzeBgra32(
            source,
            width,
            1,
            width * 4,
            PhotoCullingOverlayMode.ExposureClipping);

        var shadow = Offset(width, 0, 0);
        Assert.Equal((byte)255, result[shadow]);
        Assert.Equal((byte)48, result[shadow + 2]);
        Assert.Equal((byte)220, result[shadow + 3]);

        Assert.Equal((byte)0, result[Offset(width, 1, 0) + 3]);

        var highlight = Offset(width, 2, 0);
        Assert.Equal((byte)62, result[highlight]);
        Assert.Equal((byte)255, result[highlight + 2]);
        Assert.Equal((byte)220, result[highlight + 3]);
    }

    [Fact]
    public void AnalyzeBgra32_IgnoresFullyTransparentPixels()
    {
        var source = SolidBgra(1, 1, 255);
        source[3] = 0;

        var result = PhotoCullingOverlayService.AnalyzeBgra32(
            source,
            1,
            1,
            4,
            PhotoCullingOverlayMode.ExposureClipping);

        Assert.Equal(new byte[4], result);
    }

    [Fact]
    public void AnalyzeBgra32_HonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            PhotoCullingOverlayService.AnalyzeBgra32(
                SolidBgra(8, 8, 128),
                8,
                8,
                32,
                PhotoCullingOverlayMode.FocusPeaking,
                cancellation.Token));
    }

    [Fact]
    public void AnalyzeBgra32_RejectsDisabledModeAndShortBuffers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhotoCullingOverlayService.AnalyzeBgra32(new byte[4], 1, 1, 4, PhotoCullingOverlayMode.None));
        Assert.Throws<ArgumentException>(() =>
            PhotoCullingOverlayService.AnalyzeBgra32(new byte[3], 1, 1, 4, PhotoCullingOverlayMode.FocusPeaking));
    }

    private static byte[] SolidBgra(int width, int height, byte gray)
    {
        var result = new byte[width * height * 4];
        for (var i = 0; i < result.Length; i += 4)
        {
            result[i] = gray;
            result[i + 1] = gray;
            result[i + 2] = gray;
            result[i + 3] = 255;
        }
        return result;
    }

    private static void SetGray(byte[] pixels, int width, int x, int y, byte gray)
    {
        var offset = Offset(width, x, y);
        pixels[offset] = gray;
        pixels[offset + 1] = gray;
        pixels[offset + 2] = gray;
        pixels[offset + 3] = 255;
    }

    private static int Offset(int width, int x, int y) => ((y * width) + x) * 4;
}
