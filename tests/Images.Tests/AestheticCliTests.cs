using System.IO;
using System.Text.Json;
using Images.Services;

namespace Images.Tests;

public sealed class AestheticCliTests
{
    [Fact]
    public void Execute_RanksBatchAndEmitsVisibleProvenanceWithoutWrites()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        AestheticScoreResult[] results =
        [
            Success("ordinary.jpg", 4.5),
            Success("portfolio.jpg", 6.2),
        ];

        var exitCode = AestheticCli.Execute(["ordinary.jpg", "portfolio.jpg"], output, error, _ => results);

        Assert.Equal(0, exitCode);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("portfolio.jpg", json.RootElement.GetProperty("results")[0].GetProperty("sourcePath").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("results")[0].GetProperty("rank").GetInt32());
        Assert.Equal("Apache-2.0", json.RootElement.GetProperty("model").GetProperty("license").GetString());
        Assert.False(json.RootElement.GetProperty("automaticWrites").GetBoolean());
        Assert.Contains("No files or Pick/Reject labels were modified", error.ToString());
    }

    [Fact]
    public void Execute_PreservesSuccessfulScoresWhenOneImageFails()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        AestheticScoreResult[] results =
        [
            Success("good.jpg", 6),
            new(AestheticScoreStatus.Failed, "decode failed", "bad.jpg", 0, 0, null, 0, 0, []),
        ];

        var exitCode = AestheticCli.Execute(["good.jpg", "bad.jpg"], output, error, _ => results);

        Assert.Equal(1, exitCode);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Single(json.RootElement.GetProperty("results").EnumerateArray());
        Assert.Single(json.RootElement.GetProperty("failures").EnumerateArray());
    }

    [Fact]
    public void Execute_ReturnsModelUnavailableWhenNothingCanBeScored()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        AestheticScoreResult[] results =
        [
            new(AestheticScoreStatus.ModelUnavailable, "model missing", "photo.jpg", 0, 0, null, 0, 0, []),
        ];

        var exitCode = AestheticCli.Execute(["photo.jpg"], output, error, _ => results);

        Assert.Equal(2, exitCode);
        Assert.Contains("model missing", error.ToString());
    }

    [Fact]
    public void Execute_ReturnsDistinctExitCodeWhenModelLoadFails()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        AestheticScoreResult[] results =
        [
            new(AestheticScoreStatus.ModelLoadFailed, "model could not be loaded", "photo.jpg", 0, 0, null, 0, 0, []),
        ];

        var exitCode = AestheticCli.Execute(["photo.jpg"], output, error, _ => results);

        Assert.Equal(3, exitCode);
        Assert.Contains("could not be loaded", error.ToString());
    }

    private static AestheticScoreResult Success(string path, double mean) => new(
        AestheticScoreStatus.Success, null, path, 100, 80, "DirectML", mean, 1.2,
        [0.01, 0.02, 0.05, 0.15, 0.3, 0.25, 0.12, 0.06, 0.03, 0.01]);
}
