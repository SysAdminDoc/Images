using System.IO;
using System.Security.Cryptography;
using Images.Services;

namespace Images.Tests;

public sealed class ModelManagerServiceTests
{
    [Fact]
    public void Snapshot_ListsApprovedModelsAndRuntimeStatus()
    {
        using var temp = TestDirectory.Create();
        var service = new ModelManagerService(
            () => temp.Path,
            getOsVersion: () => new Version(10, 0, 26100));

        var snapshot = service.GetSnapshot();

        Assert.Equal(temp.Path, snapshot.ModelRoot);
        Assert.True(snapshot.Runtime.WindowsMlOsCandidate);
        Assert.Contains(snapshot.Models, model => model.Definition.Id == "opencv-inpainting-lama-2025jan");
        Assert.Contains(snapshot.Models, model => model.Definition.Id == "carve-lama-fp32");
        Assert.All(snapshot.Models, model => Assert.Equal(LocalModelAvailability.Missing, model.Availability));
    }

    [Fact]
    public void ImportLocalModel_WhenHashMatchesMarksModelReady()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "source.onnx");
        File.WriteAllText(source, "model-data");
        var definition = TestDefinition(expectedSha256: Sha256(source));
        var service = CreateService(temp.Path, definition);

        var result = service.ImportLocalModel(definition.Id, source);

        Assert.Equal(LocalModelImportStatus.Imported, result.Status);
        Assert.NotNull(result.Model);
        Assert.True(result.Model.IsReady);
        Assert.True(File.Exists(result.Model.InstalledPath));
        Assert.Equal(Sha256(source), result.Model.Sha256);
    }

    [Fact]
    public void ImportLocalModel_WhenHashDiffersKeepsFileButBlocksReadiness()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "source.onnx");
        File.WriteAllText(source, "different-model-data");
        var definition = TestDefinition(expectedSha256: new string('a', 64));
        var service = CreateService(temp.Path, definition);

        var result = service.ImportLocalModel(definition.Id, source);

        Assert.Equal(LocalModelImportStatus.HashMismatch, result.Status);
        Assert.NotNull(result.Model);
        Assert.False(result.Model.IsReady);
        Assert.Equal(LocalModelAvailability.HashMismatch, result.Model.Availability);
        Assert.True(File.Exists(result.Model.InstalledPath));
    }

    [Fact]
    public void DeleteLocalModel_RemovesImportedModelDirectory()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "source.onnx");
        File.WriteAllText(source, "model-data");
        var definition = TestDefinition(expectedSha256: Sha256(source));
        var service = CreateService(temp.Path, definition);
        var imported = service.ImportLocalModel(definition.Id, source);

        var deleted = service.DeleteLocalModel(definition.Id, out var message);

        Assert.True(deleted, message);
        Assert.False(File.Exists(imported.Model!.InstalledPath));
        Assert.Equal(LocalModelAvailability.Missing, Assert.Single(service.GetSnapshot().Models).Availability);
    }

    [Fact]
    public void ImportLocalModel_RejectsUnknownModelId()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "source.onnx");
        File.WriteAllText(source, "model-data");
        var service = CreateService(temp.Path, TestDefinition(expectedSha256: Sha256(source)));

        var result = service.ImportLocalModel("unknown", source);

        Assert.Equal(LocalModelImportStatus.UnknownModel, result.Status);
        Assert.Null(result.Model);
    }

    private static ModelManagerService CreateService(string root, LocalModelDefinition definition)
        => new(
            () => root,
            () => new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero),
            () => new Version(10, 0, 19045),
            [definition]);

    private static LocalModelDefinition TestDefinition(string expectedSha256)
        => new(
            "test-model",
            "Test model",
            "Tests",
            "test",
            "https://example.invalid/model",
            "https://example.invalid/model.onnx",
            "Test license",
            "model.onnx",
            expectedSha256,
            10,
            "Test runtime",
            "Test notes");

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
