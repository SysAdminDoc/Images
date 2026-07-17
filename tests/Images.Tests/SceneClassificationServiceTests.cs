using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class SceneClassificationServiceTests
{
    [Fact]
    public void Evaluate_ProducesTopScenesEnvironmentAndKeywords()
    {
        var labels = Enumerable.Range(0, 365)
            .Select(index => new SceneLabel(index, $"scene_{index}", $"scene {index}", index != 364))
            .ToArray();
        var logits = new float[365];
        logits[12] = 10;

        var result = SceneClassificationService.Evaluate("photo.jpg", 100, 80, "CPU", logits, labels);

        Assert.Equal(12, result.Predictions[0].ClassId);
        Assert.Equal("indoor", result.Environment);
        Assert.Contains("scene:scene 12", result.SuggestedKeywords);
        Assert.Contains("environment:indoor", result.SuggestedKeywords);
    }

    [Fact]
    public void Evaluate_RejectsUnexpectedOutputShape()
    {
        var labels = Enumerable.Range(0, 365)
            .Select(index => new SceneLabel(index, $"scene_{index}", $"scene {index}", true))
            .ToArray();

        Assert.Throws<InvalidDataException>(() =>
            SceneClassificationService.Evaluate("photo.jpg", 1, 1, "CPU", [1f], labels));
    }

    [Fact]
    public void InputTensor_UsesNchwRgbImageNetNormalization()
    {
        using var image = new MagickImage(MagickColors.Red, 1, 1);

        var tensor = SceneClassificationService.BuildInputTensor(image);
        var plane = SceneClassificationService.InputSize * SceneClassificationService.InputSize;

        Assert.Equal((1f - 0.485f) / 0.229f, tensor[0], 3);
        Assert.Equal((0f - 0.456f) / 0.224f, tensor[plane], 3);
        Assert.Equal((0f - 0.406f) / 0.225f, tensor[plane * 2], 3);
    }

    [Fact]
    public void EmbeddedLabels_AreAlignedAndHumanized()
    {
        Assert.Equal(365, SceneClassificationService.Labels.Count);
        Assert.Equal("airfield", SceneClassificationService.Labels[0].DisplayLabel);
        Assert.Equal("airplane cabin", SceneClassificationService.Labels[1].DisplayLabel);
        Assert.Equal("candy store", SceneClassificationService.Labels[80].DisplayLabel);
        Assert.Equal(Enumerable.Range(0, 365), SceneClassificationService.Labels.Select(label => label.ClassId));
    }

    [Fact]
    public void ParseLabels_RejectsMisalignedRows()
    {
        var categories = Enumerable.Range(0, 365).Select(index => $"/a/scene_{index} {index}").ToArray();
        var environments = Enumerable.Range(0, 365).Select(index => $"/a/scene_{index} 1").ToArray();
        environments[2] = "/a/wrong 1";

        Assert.Throws<InvalidDataException>(() =>
            SceneClassificationService.ParseLabels(categories, environments));
    }
}
