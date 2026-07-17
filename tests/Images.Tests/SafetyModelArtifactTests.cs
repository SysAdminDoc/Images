using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using ImageMagick;
using Images.Services;
using Microsoft.ML.OnnxRuntime;

namespace Images.Tests;

public sealed class SafetyModelArtifactTests
{
    [Fact]
    public void StagedArtifact_MatchesProvenanceAndReviewedContract()
    {
        var directory = Path.Combine(RepositoryRoot(), "models", "safety");
        var modelPath = Path.Combine(directory, "marqo-nsfw-image-detection-384.onnx");
        var licensePath = Path.Combine(directory, "LICENSE.txt");
        var configPath = Path.Combine(directory, "source-config.json");
        using var provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "provenance.json")));
        var document = provenance.RootElement;

        Assert.Equal(document.GetProperty("artifactSizeBytes").GetInt64(), new FileInfo(modelPath).Length);
        Assert.Equal(document.GetProperty("artifactSha256").GetString(), Sha256(modelPath));
        Assert.Equal(document.GetProperty("licenseFileSha256").GetString(), Sha256(licensePath));
        Assert.Equal(document.GetProperty("sourceConfigSha256").GetString(), Sha256(configPath));
        Assert.Equal("Apache-2.0", document.GetProperty("license").GetString());

        using var config = JsonDocument.Parse(File.ReadAllText(configPath));
        Assert.Equal(["NSFW", "SFW"],
            config.RootElement.GetProperty("label_names").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal([3, 384, 384],
            config.RootElement.GetProperty("pretrained_cfg").GetProperty("input_size")
                .EnumerateArray().Select(item => item.GetInt32()));

        using var session = new InferenceSession(modelPath);
        var input = Assert.Single(session.InputMetadata);
        var output = Assert.Single(session.OutputMetadata);
        Assert.Equal("input", input.Key);
        Assert.Equal([1, 3, 384, 384], input.Value.Dimensions);
        Assert.Equal("logits", output.Key);
        Assert.Equal([1, 2], output.Value.Dimensions);
    }

    [Fact]
    public void StagedArtifact_RunsBenignFixtureThroughSharedLocalRuntime()
    {
        var modelPath = Path.Combine(
            RepositoryRoot(), "models", "safety", "marqo-nsfw-image-detection-384.onnx");
        using var temp = TestDirectory.Create();
        var imagePath = Path.Combine(temp.Path, "benign.png");
        using (var image = new MagickImage(MagickColors.SkyBlue, 640, 480))
            image.Write(imagePath, MagickFormat.Png);

        using var session = OnnxRuntimeService.CreateSession(modelPath);
        var result = SafetyClassificationService.RunInference(imagePath, session);

        Assert.True(result.Success);
        Assert.Equal(2, result.Predictions.Count);
        Assert.Equal(1, result.Predictions.Sum(item => item.Probability), 8);
        Assert.All(result.Predictions, item => Assert.InRange(item.Probability, 0, 1));
        Assert.NotNull(result.Runtime);
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Images.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate repository root.");
    }
}
