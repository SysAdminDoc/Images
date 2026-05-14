using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class ImageEffectsServiceTests
{
    [Fact]
    public void Normalize_ClampsInvalidRanges()
    {
        var plan = ImageEffectsService.Normalize(new ImageEffectsPlan(
            Sharpen: 130,
            NoiseReduction: double.NaN,
            Vignette: -10));

        Assert.Equal(100, plan.Sharpen);
        Assert.Equal(0, plan.NoiseReduction);
        Assert.Equal(0, plan.Vignette);
    }

    [Fact]
    public void ToEditParameters_RoundTripsThroughParser()
    {
        var plan = new ImageEffectsPlan(22.5, 35.25, 48.75);
        var parsed = ImageEffectsService.FromParameters(plan.ToEditParameters());

        Assert.Equal(plan.Sharpen, parsed.Sharpen);
        Assert.Equal(plan.NoiseReduction, parsed.NoiseReduction);
        Assert.Equal(plan.Vignette, parsed.Vignette);
    }

    [Fact]
    public void Apply_DefaultPlanLeavesImageWritable()
    {
        using var image = new MagickImage(MagickColors.Gray, 4, 4);

        ImageEffectsService.Apply(image, ImageEffectsPlan.Default);

        Assert.Equal((uint)4, image.Width);
        Assert.Equal((uint)4, image.Height);
    }

    [Fact]
    public void Apply_NonDefaultPlanChangesPixels()
    {
        using var image = new MagickImage(MagickColors.White, 48, 48);
        var before = image.ToByteArray(MagickFormat.Png);

        ImageEffectsService.Apply(image, new ImageEffectsPlan(35, 30, 85));

        Assert.Equal((uint)48, image.Width);
        Assert.Equal((uint)48, image.Height);
        Assert.False(before.SequenceEqual(image.ToByteArray(MagickFormat.Png)));
    }
}
