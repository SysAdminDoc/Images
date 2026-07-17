using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;

namespace Images.Tests;

public sealed class NimaModelArtifactTests
{
    [Fact]
    public void StagedArtifact_MatchesProvenanceAndReviewedContract()
    {
        var root = RepositoryRoot();
        var directory = Path.Combine(root, "models", "nima");
        var modelPath = Path.Combine(directory, "idealo-nima-mobilenet-aesthetic.onnx");
        var licensePath = Path.Combine(directory, "LICENSE.txt");
        using var provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "provenance.json")));
        var document = provenance.RootElement;

        Assert.Equal(document.GetProperty("artifactSizeBytes").GetInt64(), new FileInfo(modelPath).Length);
        Assert.Equal(document.GetProperty("artifactSha256").GetString(), Sha256(modelPath));
        Assert.Equal(document.GetProperty("licenseFileSha256").GetString(), Sha256(licensePath));
        Assert.Equal("Apache-2.0", document.GetProperty("license").GetString());

        using var session = new InferenceSession(modelPath);
        var input = Assert.Single(session.InputMetadata);
        var output = Assert.Single(session.OutputMetadata);
        Assert.Equal("input", input.Key);
        Assert.Equal([1, 224, 224, 3], input.Value.Dimensions);
        Assert.Equal([1, 10], output.Value.Dimensions);
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
