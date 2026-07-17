using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;

namespace Images.Tests;

public sealed class Places365ModelArtifactTests
{
    [Fact]
    public void StagedArtifactAndLabels_MatchProvenanceAndReviewedContract()
    {
        var directory = Path.Combine(RepositoryRoot(), "models", "places365");
        var modelPath = Path.Combine(directory, "places365-resnet18.onnx");
        using var provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "provenance.json")));
        var document = provenance.RootElement;

        Assert.Equal(document.GetProperty("artifactSizeBytes").GetInt64(), new FileInfo(modelPath).Length);
        Assert.Equal(document.GetProperty("artifactSha256").GetString(), Sha256(modelPath));
        Assert.Equal(
            document.GetProperty("labels").GetProperty("categoriesSha256").GetString(),
            Sha256(Path.Combine(directory, "categories_places365.txt")));
        Assert.Equal(
            document.GetProperty("labels").GetProperty("indoorOutdoorSha256").GetString(),
            Sha256(Path.Combine(directory, "IO_places365.txt")));
        Assert.Contains("CC BY", File.ReadAllText(Path.Combine(directory, "ATTRIBUTION.txt")), StringComparison.Ordinal);

        var categories = File.ReadAllLines(Path.Combine(directory, "categories_places365.txt"));
        var indoorOutdoor = File.ReadAllLines(Path.Combine(directory, "IO_places365.txt"));
        Assert.Equal(365, categories.Length);
        Assert.Equal(365, indoorOutdoor.Length);
        Assert.All(indoorOutdoor, line => Assert.Contains(line.Split(' ').Last(), new[] { "1", "2" }));

        using var session = new InferenceSession(modelPath);
        Assert.Equal([1, 3, 224, 224], Assert.Single(session.InputMetadata).Value.Dimensions);
        Assert.Equal([1, 365], Assert.Single(session.OutputMetadata).Value.Dimensions);
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
