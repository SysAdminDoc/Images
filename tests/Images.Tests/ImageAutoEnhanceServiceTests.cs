using ImageMagick;
using ImageMagick.Drawing;
using Images.Services;

namespace Images.Tests;

public sealed class ImageAutoEnhanceServiceTests
{
    [Fact]
    public void FromParameters_UsesBalancedFallbacks()
    {
        var plan = ImageAutoEnhanceService.FromParameters(new Dictionary<string, string>
        {
            ["profile"] = " ",
            ["version"] = "0"
        });

        Assert.Equal("balanced", plan.Profile);
        Assert.Equal(1, plan.Version);
    }

    [Fact]
    public void ToEditParameters_RoundTripsThroughParser()
    {
        var parsed = ImageAutoEnhanceService.FromParameters(ImageAutoEnhancePlan.Balanced.ToEditParameters());

        Assert.Equal(ImageAutoEnhancePlan.Balanced, parsed);
    }

    [Fact]
    public void Apply_BalancedPlanChangesPixels()
    {
        using var image = new MagickImage(MagickColors.Gray, 24, 24);
        var draw = new Drawables()
            .FillColor(MagickColors.DarkSlateBlue)
            .Rectangle(0, 0, 10, 23)
            .FillColor(MagickColors.LightGoldenrodYellow)
            .Rectangle(12, 0, 23, 23);
        draw.Draw(image);
        var before = image.ToByteArray(MagickFormat.Png);

        ImageAutoEnhanceService.Apply(image, ImageAutoEnhancePlan.Balanced);

        Assert.False(before.SequenceEqual(image.ToByteArray(MagickFormat.Png)));
    }
}
