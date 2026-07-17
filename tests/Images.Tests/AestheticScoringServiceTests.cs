using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class AestheticScoringServiceTests
{
    [Fact]
    public void Evaluate_ComputesExpectedMeanAndDeviation()
    {
        var distribution = new float[10];
        distribution[3] = 1;

        var result = AestheticScoringService.Evaluate("photo.jpg", 100, 80, "CPU", distribution);

        Assert.Equal(4, result.MeanScore, 8);
        Assert.Equal(0, result.StandardDeviation, 8);
        Assert.Equal(1, result.Distribution.Sum(), 8);
    }

    [Fact]
    public void Evaluate_NormalizesFiniteDistribution()
    {
        var result = AestheticScoringService.Evaluate(
            "photo.jpg", 1, 1, "CPU", [0f, 0f, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f]);

        Assert.Equal(5.5, result.MeanScore, 8);
        Assert.Equal(0.5, result.StandardDeviation, 8);
    }

    [Fact]
    public void Evaluate_RejectsUnexpectedDistribution()
    {
        Assert.Throws<InvalidDataException>(() =>
            AestheticScoringService.Evaluate("photo.jpg", 1, 1, "CPU", [1f]));
        Assert.Throws<InvalidDataException>(() =>
            AestheticScoringService.Evaluate("photo.jpg", 1, 1, "CPU", new float[10]));
    }

    [Fact]
    public void InputTensor_UsesNhwcRgbMobileNetNormalization()
    {
        using var image = new MagickImage(MagickColors.Red, 1, 1);

        var tensor = AestheticScoringService.BuildInputTensor(image);

        Assert.Equal(1f, tensor[0], 3);
        Assert.Equal(-1f, tensor[1], 3);
        Assert.Equal(-1f, tensor[2], 3);
        Assert.Equal(AestheticScoringService.InputSize * AestheticScoringService.InputSize * 3, tensor.Length);
    }
}
