using System.IO;
using System.Text.Json;
using Images.Services;

namespace Images.Tests;

public sealed class ObjectCliTests
{
    private static ObjectDetectionResult Result() => new(
        true, null, "dog.jpg", 100, 80, "CPU",
        [new ObjectDetection("dog", 16, 0.9, 10, 10, 50, 40)]);

    [Fact]
    public void Execute_EmitsCocoKeywordSuggestions()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = ObjectCli.Execute("dog.jpg", output, error, _ => Result());

        Assert.Equal(0, exitCode);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("object:dog", json.RootElement.GetProperty("suggestedKeywords")[0].GetString());
        Assert.Contains("No files were modified", error.ToString());
    }

    [Fact]
    public void ExecuteXmp_EmitsDcSubjectDraft()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = ObjectCli.ExecuteXmp("dog.jpg", output, error, _ => Result());

        Assert.Equal(0, exitCode);
        Assert.Contains("object:dog", output.ToString());
        Assert.Contains("dc:subject", output.ToString());
    }
}
