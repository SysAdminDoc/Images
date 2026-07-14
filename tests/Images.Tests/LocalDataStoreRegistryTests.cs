using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class LocalDataStoreRegistryTests
{
    [Fact]
    public void Definitions_CoverEveryCurrentAppLocalStore()
    {
        var paths = LocalDataStoreRegistry.Definitions
            .Select(definition => definition.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] required =
        [
            "catalog.db", "semantic-index.db", "thumbs", "tiles", "models", "diagnostics",
            "Logs", "recovery", "network-egress.jsonl", "wallpaper", "email-drafts",
            "keyword-sets.json", "writeback-backups", "quarantine", "clipboard",
            "animation-frames", "motion-video", "settings.db", "update-check.json"
        ];

        Assert.All(required, path => Assert.Contains(path, paths));
        Assert.Equal(LocalDataStoreRegistry.Definitions.Count, paths.Count);
    }

    [Fact]
    public void GetSnapshots_ReportsRealSemanticIndexFileAndStoreMetadata()
    {
        using var temp = TestDirectory.Create();
        var semanticPath = temp.WriteFile("semantic-index.db", "semantic payload");
        var registry = new LocalDataStoreRegistry(() => temp.Path);

        var semantic = Assert.Single(registry.GetSnapshots(), snapshot =>
            snapshot.Definition.Id == "semantic-index");

        Assert.Equal(semanticPath, semantic.FullPath);
        Assert.True(semantic.Exists);
        Assert.Equal(new FileInfo(semanticPath).Length, semantic.SizeBytes);
        Assert.True(semantic.Definition.Rebuildable);
        Assert.Equal(LocalDataClearAction.ClearOnPrivacyReset, semantic.Definition.ClearAction);
    }

    [Fact]
    public void ClearPrivacyResetStores_RemovesRegisteredTargetsAndPreservesUserContent()
    {
        using var temp = TestDirectory.Create();
        foreach (var definition in LocalDataStoreRegistry.Definitions)
        {
            var path = Path.Combine(temp.Path, definition.RelativePath);
            if (definition.IsDirectory)
            {
                Directory.CreateDirectory(path);
                File.WriteAllText(Path.Combine(path, "payload.bin"), definition.Id);
            }
            else
            {
                File.WriteAllText(path, definition.Id);
                if (definition.Id is "catalog" or "semantic-index")
                    File.WriteAllText(path + "-wal", "sqlite wal");
            }
        }
        var registry = new LocalDataStoreRegistry(() => temp.Path);

        var result = registry.ClearPrivacyResetStores();

        Assert.True(result.Succeeded);
        Assert.NotEqual(0, result.ClearedStores);
        foreach (var definition in LocalDataStoreRegistry.Definitions)
        {
            var path = Path.Combine(temp.Path, definition.RelativePath);
            if (definition.ClearAction == LocalDataClearAction.ClearOnPrivacyReset)
            {
                Assert.False(File.Exists(path), definition.Id);
                Assert.False(Directory.Exists(path), definition.Id);
                Assert.False(File.Exists(path + "-wal"), definition.Id + " wal");
            }
            else
            {
                Assert.True(File.Exists(path) || Directory.Exists(path), definition.Id);
            }
        }

        Assert.True(Directory.Exists(Path.Combine(temp.Path, "models")));
        Assert.True(Directory.Exists(Path.Combine(temp.Path, "writeback-backups")));
        Assert.True(Directory.Exists(Path.Combine(temp.Path, "quarantine")));
    }

    [Fact]
    public void PrivacyMetadata_CoversEveryRegisteredStoreAndLocalModelBehavior()
    {
        foreach (var definition in LocalDataStoreRegistry.Definitions)
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.RelativePath), definition.Id);
            Assert.False(string.IsNullOrWhiteSpace(definition.Purpose), definition.Id);
            Assert.True(Enum.IsDefined(definition.Sensitivity), definition.Id);
            Assert.True(Enum.IsDefined(definition.ClearAction), definition.Id);
        }

        var models = Assert.Single(LocalDataStoreRegistry.Definitions, definition => definition.Id == "models");
        Assert.Equal(LocalDataSensitivity.LocalModel, models.Sensitivity);
        Assert.Equal(LocalDataClearAction.UserManaged, models.ClearAction);
        Assert.Contains("imported", models.Purpose, StringComparison.OrdinalIgnoreCase);
    }
}
