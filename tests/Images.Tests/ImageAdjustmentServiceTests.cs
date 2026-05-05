using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class ImageAdjustmentServiceTests
{
    [Fact]
    public void Normalize_ClampsInvalidRanges()
    {
        var plan = ImageAdjustmentService.Normalize(new ImageAdjustmentPlan(
            BlackPoint: 110,
            WhitePoint: -5,
            Gamma: 0,
            Curve: 90,
            Hue: 260,
            Saturation: -10,
            Lightness: 240));

        Assert.Equal(99, plan.BlackPoint);
        Assert.Equal(100, plan.WhitePoint);
        Assert.Equal(0.1, plan.Gamma);
        Assert.Equal(50, plan.Curve);
        Assert.Equal(180, plan.Hue);
        Assert.Equal(0, plan.Saturation);
        Assert.Equal(200, plan.Lightness);
    }

    [Fact]
    public void ToEditParameters_RoundTripsThroughParser()
    {
        var plan = new ImageAdjustmentPlan(2.5, 97.5, 1.2, -8, 15, 120, 95);
        var parsed = ImageAdjustmentService.FromParameters(plan.ToEditParameters());

        Assert.Equal(plan.BlackPoint, parsed.BlackPoint);
        Assert.Equal(plan.WhitePoint, parsed.WhitePoint);
        Assert.Equal(plan.Gamma, parsed.Gamma);
        Assert.Equal(plan.Curve, parsed.Curve);
        Assert.Equal(plan.Hue, parsed.Hue);
        Assert.Equal(plan.Saturation, parsed.Saturation);
        Assert.Equal(plan.Lightness, parsed.Lightness);
    }

    [Fact]
    public void Apply_DefaultPlanLeavesImageWritable()
    {
        using var image = new MagickImage(MagickColors.Gray, 2, 2);

        ImageAdjustmentService.Apply(image, ImageAdjustmentPlan.Default);

        Assert.Equal((uint)2, image.Width);
        Assert.Equal((uint)2, image.Height);
    }

    [Fact]
    public void Apply_NonDefaultPlanRunsAllAdjustmentFamilies()
    {
        using var image = new MagickImage(MagickColors.SteelBlue, 2, 2);
        var plan = new ImageAdjustmentPlan(4, 96, 1.1, 12, 30, 125, 105);

        ImageAdjustmentService.Apply(image, plan);

        Assert.Equal((uint)2, image.Width);
        Assert.Equal((uint)2, image.Height);
    }
}
