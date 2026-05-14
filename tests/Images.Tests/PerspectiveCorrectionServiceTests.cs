using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class PerspectiveCorrectionServiceTests
{
    [Fact]
    public void ToEditParameters_RoundTripsThroughParser()
    {
        var plan = new PerspectiveCorrectionPlan(
            new PerspectivePoint(3.5, 2.25),
            new PerspectivePoint(27, 5.5),
            new PerspectivePoint(29.25, 30),
            new PerspectivePoint(1, 26.75),
            OutputWidth: 28,
            OutputHeight: 24);

        var parsed = PerspectiveCorrectionService.FromParameters(plan.ToEditParameters(), 32, 32);

        Assert.Equal(3.5, parsed.TopLeft.X);
        Assert.Equal(2.25, parsed.TopLeft.Y);
        Assert.Equal(28, parsed.OutputWidth);
        Assert.Equal(24, parsed.OutputHeight);
    }

    [Fact]
    public void Normalize_ClampsCornersAndOutputSize()
    {
        var plan = PerspectiveCorrectionService.Normalize(
            new PerspectiveCorrectionPlan(
                new PerspectivePoint(double.NaN, -10),
                new PerspectivePoint(100, 5),
                new PerspectivePoint(100, 100),
                new PerspectivePoint(-4, 100),
                OutputWidth: 999,
                OutputHeight: 0),
            imageWidth: 20,
            imageHeight: 10);

        Assert.Equal(new PerspectivePoint(0, 0), plan.TopLeft);
        Assert.Equal(new PerspectivePoint(19, 5), plan.TopRight);
        Assert.Equal(new PerspectivePoint(19, 9), plan.BottomRight);
        Assert.Equal(new PerspectivePoint(0, 9), plan.BottomLeft);
        Assert.Equal(80, plan.OutputWidth);
        Assert.Equal(9, plan.OutputHeight);
    }

    [Fact]
    public void IsIdentity_ReturnsTrueOnlyForFullImageCorners()
    {
        var identity = PerspectiveCorrectionService.Identity(32, 24);
        var changed = identity with
        {
            TopLeft = new PerspectivePoint(2, 0)
        };

        Assert.True(PerspectiveCorrectionService.IsIdentity(identity, 32, 24));
        Assert.False(PerspectiveCorrectionService.IsIdentity(changed, 32, 24));
    }

    [Fact]
    public void Apply_NonIdentityPlanChangesCanvasSize()
    {
        using var image = new MagickImage(MagickColors.White, 32, 32);
        var plan = new PerspectiveCorrectionPlan(
            new PerspectivePoint(4, 0),
            new PerspectivePoint(31, 4),
            new PerspectivePoint(28, 31),
            new PerspectivePoint(0, 28),
            OutputWidth: 24,
            OutputHeight: 26);

        PerspectiveCorrectionService.Apply(image, plan);

        Assert.Equal((uint)24, image.Width);
        Assert.Equal((uint)26, image.Height);
    }
}
