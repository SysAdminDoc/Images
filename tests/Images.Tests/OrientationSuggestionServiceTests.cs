using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class OrientationSuggestionServiceTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 180)]
    [InlineData(2, 270)]
    [InlineData(3, 90)]
    public void Evaluate_MapsReviewedClassToClockwiseCorrection(int predictedClass, int correction)
    {
        var logits = new float[4];
        logits[predictedClass] = 10;

        var result = OrientationSuggestionService.Evaluate("receipt.png", 400, 600, "CPU", logits);

        Assert.True(result.IsConfident);
        Assert.Equal(correction, result.SuggestedCorrectionDegreesClockwise);
    }

    [Fact]
    public void Evaluate_WithholdsAmbiguousSuggestion()
    {
        var result = OrientationSuggestionService.Evaluate(
            "photo.jpg", 100, 100, "CPU", [1f, 0.9f, 0f, 0f]);

        Assert.False(result.IsConfident);
        Assert.Null(result.SuggestedCorrectionDegreesClockwise);
        Assert.Equal("uncertain", result.Assessment);
    }

    [Fact]
    public void Evaluate_RejectsUnexpectedOutputShape()
    {
        Assert.Throws<InvalidDataException>(() =>
            OrientationSuggestionService.Evaluate("photo.jpg", 1, 1, "CPU", [1f]));
    }

    [Fact]
    public void InputTensor_UsesRgbImageNetNormalization()
    {
        using var image = new MagickImage(MagickColors.Red, 1, 1);

        var tensor = OrientationSuggestionService.BuildInputTensor(image);
        var plane = OrientationSuggestionService.InputSize * OrientationSuggestionService.InputSize;

        Assert.Equal((1f - 0.485f) / 0.229f, tensor[0], 3);
        Assert.Equal((0f - 0.456f) / 0.224f, tensor[plane], 3);
        Assert.Equal((0f - 0.406f) / 0.225f, tensor[plane * 2], 3);
    }
}
