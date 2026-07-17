using System.IO;
using System.Text.Json;
using Images.Services;

namespace Images.Tests;

public sealed class FaceCliTests
{
    [Fact]
    public void TryParse_RequiresExactlyOneImagePath()
    {
        Assert.True(FaceCli.TryParse(["--face-detect", "portrait.jpg"], out var path, out var error));
        Assert.Equal("portrait.jpg", path);
        Assert.Null(error);

        Assert.True(FaceCli.TryParse(["--face-detect"], out path, out error));
        Assert.Null(path);
        Assert.Contains("Usage:", error);
    }

    [Fact]
    public void Execute_PrintsReviewableJsonWithoutWritingMetadata()
    {
        var result = new FaceDetectionResult(
            FaceDetectionStatus.Success,
            null,
            @"C:\photos\portrait.jpg",
            100,
            80,
            "CPU (ONNX Runtime)",
            [new FaceDetection(20, 10, 40, 20, 0.95, [new FaceLandmark(30, 20)])]);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = FaceCli.Execute("ignored.jpg", output, error, _ => result);

        Assert.Equal(0, exitCode);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal(1, json.RootElement.GetProperty("faceCount").GetInt32());
        Assert.Equal(0.4, json.RootElement.GetProperty("faces")[0].GetProperty("mwgArea").GetProperty("x").GetDouble(), 6);
        Assert.Contains("No files were modified", error.ToString());
    }

    [Fact]
    public void Execute_WhenModelMissingUsesDistinctExitCode()
    {
        var result = new FaceDetectionResult(
            FaceDetectionStatus.ModelUnavailable,
            "Import YuNet first.",
            "portrait.jpg",
            0,
            0,
            null,
            []);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = FaceCli.Execute("portrait.jpg", output, error, _ => result);

        Assert.Equal(2, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains("Import YuNet first", error.ToString());
    }
}
