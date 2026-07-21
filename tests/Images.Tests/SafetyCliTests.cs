using System.IO;
using System.Text.Json;
using Images.Services;

namespace Images.Tests;

public sealed class SafetyCliTests
{
    [Fact]
    public void Execute_ExplicitlyExportsScoresWithoutAutomaticWrites()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        SafetyClassificationResult[] results =
        [
            new(
                SafetyClassificationStatus.Success, null, "photo.jpg", 100, 80, "DirectML",
                "SFW", 0.9,
                [new SafetyPrediction("SFW", 0.9), new SafetyPrediction("NSFW", 0.1)]),
        ];

        var exitCode = SafetyCli.Execute(["photo.jpg"], output, error, _ => results);

        Assert.Equal(0, exitCode);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("explicit-stdout", json.RootElement.GetProperty("scoreExport").GetString());
        Assert.False(json.RootElement.GetProperty("automaticWrites").GetBoolean());
        Assert.Contains("no threshold", json.RootElement.GetProperty("decisionPolicy").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("SFW", json.RootElement.GetProperty("results")[0].GetProperty("mostLikelyLabel").GetString());
        Assert.Contains("no files, metadata, labels, or logs", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_ReturnsModelUnavailableWhenNothingCanBeClassified()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        SafetyClassificationResult[] results =
        [
            new(SafetyClassificationStatus.ModelUnavailable, "model missing", "photo.jpg", 0, 0, null, null, 0, []),
        ];

        var exitCode = SafetyCli.Execute(["photo.jpg"], output, error, _ => results);

        Assert.Equal(2, exitCode);
        Assert.Contains("model missing", error.ToString());
    }

    [Fact]
    public void Execute_ReturnsDistinctExitCodeWhenModelLoadFails()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        SafetyClassificationResult[] results =
        [
            new(SafetyClassificationStatus.ModelLoadFailed, "model could not be loaded", "photo.jpg", 0, 0, null, null, 0, []),
        ];

        var exitCode = SafetyCli.Execute(["photo.jpg"], output, error, _ => results);

        Assert.Equal(3, exitCode);
        Assert.Contains("could not be loaded", error.ToString());
    }
}
