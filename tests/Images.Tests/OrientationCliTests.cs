using System.IO;
using System.Text.Json;
using Images.Services;

namespace Images.Tests;

public sealed class OrientationCliTests
{
    [Fact]
    public void Execute_EmitsSuggestionWithoutWriting()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var result = new OrientationSuggestionResult(
            OrientationSuggestionStatus.Success, null, "receipt.png", 400, 600, "DirectML",
            3, 90, 0.95, 0.90, [0.01, 0.01, 0.03, 0.95], true);

        var exitCode = OrientationCli.Execute("receipt.png", output, error, _ => result);

        Assert.Equal(0, exitCode);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal(90, json.RootElement.GetProperty("suggestedCorrectionDegreesClockwise").GetInt32());
        Assert.Equal("rotate", json.RootElement.GetProperty("assessment").GetString());
        Assert.Contains("no files were modified", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_ReturnsModelUnavailableExitCode()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var result = new OrientationSuggestionResult(
            OrientationSuggestionStatus.ModelUnavailable, "missing", "photo.jpg", 0, 0, null,
            -1, 0, 0, 0, [], false);

        var exitCode = OrientationCli.Execute("photo.jpg", output, error, _ => result);

        Assert.Equal(2, exitCode);
        Assert.Contains("missing", error.ToString());
    }
}
