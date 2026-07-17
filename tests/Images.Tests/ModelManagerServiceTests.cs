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
        Assert.Contains(snapshot.Models, model => model.Definition.Id == "opencv-face-detection-yunet-2023mar");
        Assert.Contains(snapshot.Models, model => model.Definition.Id == "opencv-face-recognition-sface-2021dec");
        Assert.Contains(snapshot.Models, model => model.Definition.Id == "opencv-object-detection-yolox-2022nov");
        Assert.Contains(snapshot.Models, model => model.Definition.Id == "fachuan-orientation-convnextv2-2026jun");
        Assert.Contains(snapshot.Models, model => model.Definition.Id == "idealo-nima-mobilenet-aesthetic");
        Assert.Contains(snapshot.Models, model => model.Definition.Id == "csail-places365-resnet18");
        Assert.All(snapshot.Models, model => Assert.Equal(LocalModelAvailability.Missing, model.Availability));
    }

    [Fact]
    public void ApprovedSceneModel_IsArtifactCommitSizeAndHashPinned()
    {
        var model = Assert.Single(ModelManagerService.ApprovedModels,
            item => item.Id == "csail-places365-resnet18");

        Assert.Contains("f064916ab8abb4816fc65b1d1b6bf1624466e6a9", model.DownloadUrl, StringComparison.Ordinal);
        Assert.Equal(45_445_531, model.ExpectedSizeBytes);
        Assert.Equal(SceneClassificationService.ArtifactSha256, model.ExpectedSha256);
        Assert.Contains("CC BY", model.License, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovedAestheticModel_IsArtifactCommitSizeAndHashPinned()
    {
        var model = Assert.Single(ModelManagerService.ApprovedModels,
            item => item.Id == "idealo-nima-mobilenet-aesthetic");

        Assert.Contains("4b7be8b54cb0969cc5e826f8c17557211de84358", model.DownloadUrl, StringComparison.Ordinal);
        Assert.Equal(12_842_033, model.ExpectedSizeBytes);
        Assert.Equal(AestheticScoringService.ArtifactSha256, model.ExpectedSha256);
        Assert.Contains("Apache", model.License, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApprovedOrientationModel_IsRevisionSizeAndHashPinned()
    {
        var model = Assert.Single(ModelManagerService.ApprovedModels,
            item => item.Id == "fachuan-orientation-convnextv2-2026jun");

        Assert.Contains("f21ab96006ad10e6388024751d1b829f5b8ab2c9", model.DownloadUrl, StringComparison.Ordinal);
        Assert.Equal(13_671_697, model.ExpectedSizeBytes);
        Assert.Equal("50ec8fd24fb08e23aaac8ae657f2756c9251b5f052b00a1e3af8c128e4796b54", model.ExpectedSha256);
        Assert.Contains("MIT", model.License, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApprovedFaceModels_PinReviewedOpenCvArtifacts()
    {
        var yunet = Assert.Single(ModelManagerService.ApprovedModels,
            model => model.Id == "opencv-face-detection-yunet-2023mar");
        var sface = Assert.Single(ModelManagerService.ApprovedModels,
            model => model.Id == "opencv-face-recognition-sface-2021dec");

        Assert.Equal(232_589, yunet.ExpectedSizeBytes);
        Assert.Equal("8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4", yunet.ExpectedSha256);
        Assert.Contains("3cc26e7f1014a5ee5d74a42acee58bafc9d0a310", yunet.DownloadUrl, StringComparison.Ordinal);
        Assert.Equal(38_696_353, sface.ExpectedSizeBytes);
        Assert.Equal("0ba9fbfa01b5270c96627c4ef784da859931e02f04419c829e83484087c34e79", sface.ExpectedSha256);
        Assert.Contains("3d7082438a6e4551e840c9b2bb60b71e8da4b524", sface.DownloadUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovedObjectModel_PinsReviewedOpenCvYoloXArtifact()
    {
        var model = Assert.Single(ModelManagerService.ApprovedModels,
            item => item.Id == "opencv-object-detection-yolox-2022nov");

        Assert.Equal(35_858_002, model.ExpectedSizeBytes);
        Assert.Equal("c5c2d13e59ae883e6af3b45daea64af4833a4951c92d116ec270d9ddbe998063", model.ExpectedSha256);
        Assert.Contains("78c368f74ce73ee28fc7a1be418a598c71b58b52", model.DownloadUrl, StringComparison.Ordinal);
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
    public void Snapshot_RehashesSameLengthModelWhenModifiedTimeDiffers()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "source.onnx");
        File.WriteAllText(source, "model-data");
        var originalSha = Sha256(source);
        var definition = TestDefinition(expectedSha256: originalSha);
        var service = CreateService(temp.Path, definition);
        var import = service.ImportLocalModel(definition.Id, source);
        var installedPath = import.Model!.InstalledPath!;
        var manifestPath = Path.Combine(temp.Path, "test", definition.Id, "model-manifest.json");

        File.WriteAllText(installedPath, "tamper!!!!");
        File.SetLastWriteTimeUtc(installedPath, DateTime.UtcNow.AddMinutes(5));

        var status = Assert.Single(service.GetSnapshot().Models);

        Assert.Equal(LocalModelAvailability.HashMismatch, status.Availability);
        Assert.False(status.IsReady);
        Assert.NotEqual(originalSha, status.Sha256);
        Assert.Equal(Sha256(installedPath), status.Sha256);
        Assert.Contains(status.Sha256!, File.ReadAllText(manifestPath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Snapshot_WhenManifestExceedsMetadataLimit_ReportsInvalidManifest()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "source.onnx");
        File.WriteAllText(source, "model-data");
        var definition = TestDefinition(expectedSha256: Sha256(source));
        var service = CreateService(temp.Path, definition);
        service.ImportLocalModel(definition.Id, source);
        var manifestPath = Path.Combine(temp.Path, "test", definition.Id, "model-manifest.json");
        using (var stream = new FileStream(manifestPath, FileMode.Create, FileAccess.Write, FileShare.None))
            stream.SetLength(BoundedTextFileReader.MaxServiceMetadataBytes + 1L);

        var status = Assert.Single(service.GetSnapshot().Models);

        Assert.Equal(LocalModelAvailability.ManifestInvalid, status.Availability);
        Assert.False(status.IsReady);
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
