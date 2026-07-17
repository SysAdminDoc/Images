using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class SafetyClassificationServiceTests
{
    [Fact]
    public void Evaluate_ProducesStableTwoClassProbabilityDistribution()
    {
        var result = SafetyClassificationService.Evaluate("photo.jpg", 100, 80, "CPU", [2f, 0f]);

        Assert.Equal("NSFW", result.MostLikelyLabel);
        Assert.Equal(0.880797, result.Confidence, 5);
        Assert.Equal(["NSFW", "SFW"], result.Predictions.Select(item => item.Label));
        Assert.Equal(1, result.Predictions.Sum(item => item.Probability), 8);
    }

    [Fact]
    public void Evaluate_RejectsUnexpectedOrNonFiniteOutput()
    {
        Assert.Throws<InvalidDataException>(() =>
            SafetyClassificationService.Evaluate("photo.jpg", 1, 1, "CPU", [1f]));
        Assert.Throws<InvalidDataException>(() =>
            SafetyClassificationService.Evaluate("photo.jpg", 1, 1, "CPU", [float.NaN, 1f]));
    }

    [Fact]
    public void InputTensor_UsesNchwRgbHalfRangeNormalization()
    {
        using var image = new MagickImage(MagickColors.Red, 1, 1);

        var tensor = SafetyClassificationService.BuildInputTensor(image);
        var plane = SafetyClassificationService.InputSize * SafetyClassificationService.InputSize;

        Assert.Equal(1f, tensor[0], 3);
        Assert.Equal(-1f, tensor[plane], 3);
        Assert.Equal(-1f, tensor[plane * 2], 3);
    }

    [Fact]
    public void InputTensor_CenterCropsAfterShortestSideResize()
    {
        using var image = new MagickImage(MagickColors.Red, 800, 400);
        image.Composite(new MagickImage(MagickColors.Blue, 200, 400), 0, 0, CompositeOperator.Copy);

        var tensor = SafetyClassificationService.BuildInputTensor(image);
        var center = (SafetyClassificationService.InputSize / 2) * SafetyClassificationService.InputSize +
                     SafetyClassificationService.InputSize / 2;

        Assert.True(tensor[center] > 0.9f);
    }
}
