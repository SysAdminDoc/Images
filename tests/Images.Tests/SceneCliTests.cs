using System.IO;
using System.Text.Json;
using Images.Services;

namespace Images.Tests;

public sealed class SceneCliTests
{
    [Fact]
    public void Execute_EmitsPredictionsAttributionAndReviewOnlyKeywords()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        SceneClassificationResult[] results =
        [
            new(
                SceneClassificationStatus.Success, null, "patio.jpg", 100, 80, "DirectML",
                "outdoor", 0.9,
                [new ScenePrediction(1, "restaurant_patio", "restaurant patio", 0.7, "outdoor")],
                ["scene:restaurant patio", "environment:outdoor"]),
        ];

        var exitCode = SceneCli.Execute(["patio.jpg"], output, error, _ => results);

        Assert.Equal(0, exitCode);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Contains("Zhou", json.RootElement.GetProperty("model").GetProperty("attribution").GetString());
        Assert.False(json.RootElement.GetProperty("automaticWrites").GetBoolean());
        Assert.Equal("scene:restaurant patio", json.RootElement.GetProperty("results")[0].GetProperty("suggestedKeywords")[0].GetString());
        Assert.Contains("no files, metadata, or smart albums were modified", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_ReturnsModelUnavailableWhenNothingCanBeClassified()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        SceneClassificationResult[] results =
        [
            new(SceneClassificationStatus.ModelUnavailable, "model missing", "photo.jpg", 0, 0, null, null, 0, [], []),
        ];

        var exitCode = SceneCli.Execute(["photo.jpg"], output, error, _ => results);

        Assert.Equal(2, exitCode);
        Assert.Contains("model missing", error.ToString());
    }

    [Fact]
    public void Execute_ReturnsDistinctExitCodeWhenModelLoadFails()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        SceneClassificationResult[] results =
        [
            new(SceneClassificationStatus.ModelLoadFailed, "model could not be loaded", "photo.jpg", 0, 0, null, null, 0, [], []),
        ];

        var exitCode = SceneCli.Execute(["photo.jpg"], output, error, _ => results);

        Assert.Equal(3, exitCode);
        Assert.Contains("could not be loaded", error.ToString());
    }
}
