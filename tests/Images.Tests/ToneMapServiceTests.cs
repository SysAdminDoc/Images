using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class ToneMapServiceTests
{
    [Fact]
    public void NativeRuntime_IsHdriEnabled()
    {
        Assert.Contains("HDRI", Convert.ToString(MagickNET.Features), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReinhardFloatBuffer_CompressesWithoutHardClippingHighlights()
    {
        float[] samples = [1f, 2f, 4f];

        ToneMapService.Apply(samples, ToneMapOperator.Reinhard, whitePoint: 4f);

        Assert.InRange(samples[0], 0f, 1f);
        Assert.True(samples[0] < samples[1]);
        Assert.True(samples[1] < samples[2]);
        Assert.Equal(1f, samples[2], precision: 5);
    }

    [Theory]
    [InlineData(ToneMapOperator.Reinhard)]
    [InlineData(ToneMapOperator.Hable)]
    [InlineData(ToneMapOperator.Aces)]
    public void Operators_ClampInvalidAndExtendedSamplesToSdr(ToneMapOperator toneMapOperator)
    {
        float[] samples = [float.NaN, -1f, 0f, 0.5f, 2f, 8f];

        ToneMapService.Apply(samples, toneMapOperator, whitePoint: 8f);

        Assert.All(samples, sample => Assert.InRange(sample, 0f, 1f));
        Assert.Equal(0f, samples[0]);
        Assert.Equal(0f, samples[1]);
        Assert.True(samples[3] < samples[4]);
        Assert.True(samples[4] < samples[5]);
    }

    [Fact]
    public void HdriImage_ReinhardPreservesValuesAboveQuantumRangeUntilTonemap()
    {
        using var image = new MagickImage(MagickColors.Black, 2, 1) { Depth = 16 };
        var pixels = image.GetPixelsUnsafe();
        pixels.SetPixel(0, 0, [Quantum.Max, Quantum.Max, Quantum.Max]);
        pixels.SetPixel(1, 0, [Quantum.Max * 4f, Quantum.Max * 4f, Quantum.Max * 4f]);

        var applied = ToneMapService.ApplyIfNeeded(image, ".exr");

        Assert.True(applied);
        var mappedPixels = image.GetPixelsUnsafe();
        var dark = mappedPixels.GetPixel(0, 0)!.ToArray()[0];
        var highlight = mappedPixels.GetPixel(1, 0)!.ToArray()[0];
        Assert.InRange(dark, 0f, Quantum.Max);
        Assert.InRange(highlight, 0f, Quantum.Max);
        Assert.True(dark < highlight);
    }
}
