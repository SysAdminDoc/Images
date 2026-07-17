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

    [Fact]
    public void ExecuteCluster_EmitsMembershipWithoutEmbeddingVectors()
    {
        var detection = new FaceDetection(10, 10, 50, 50, 0.95, []);
        FaceRecognitionResult Analyze(string path) => new(
            true,
            null,
            path,
            "CPU (ONNX Runtime)",
            [new FaceEmbedding(path, 0, detection, FaceEmbeddingQuality.Accepted, null, [1, 0])]);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = FaceCli.ExecuteCluster(["one.jpg", "two.jpg"], output, error, Analyze);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("vector", output.ToString(), StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal(2, json.RootElement.GetProperty("clusters")[0].GetProperty("members").GetArrayLength());
        Assert.Contains("No files were modified", error.ToString());
    }

    [Fact]
    public void ExecuteXmp_PrintsMwgDraftWithoutWritingAFile()
    {
        var result = new FaceDetectionResult(
            FaceDetectionStatus.Success,
            null,
            "portrait.jpg",
            100,
            100,
            "CPU",
            [new FaceDetection(20, 20, 40, 40, 0.95, [])]);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = FaceCli.ExecuteXmp("portrait.jpg", output, error, _ => result);

        Assert.Equal(0, exitCode);
        Assert.Contains("mwg-rs:Regions", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("No files were modified", error.ToString());
    }
}
