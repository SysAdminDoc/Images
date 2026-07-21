using System.IO;
using ImageMagick;
using Images.Services;
using Microsoft.Data.Sqlite;

namespace Images.Tests;

[Collection("TimingSensitive")]
public sealed class SemanticSearchServiceTests
{
    [Fact]
    public void Rebuild_IndexesCatalogAssetsAndSearchesByLocalEmbedding()
    {
        using var temp = TestDirectory.Create();
        var sunset = WriteImage(temp.Path, "sunset-over-water.png", 16, 8);
        WriteImage(temp.Path, "receipt-paper-scan.png", 8, 16);
        var service = CreateService(temp.Path);

        var result = service.Rebuild([temp.Path]);
        var matches = service.Search("sunset water", limit: 5);

        Assert.Equal(2, result.IndexedCount);
        Assert.NotEmpty(matches);
        Assert.Equal(Path.GetFullPath(sunset), matches[0].SourcePath);
        Assert.True(matches[0].Score > 0);
    }

    [Fact]
    public void Search_RepeatQueryOnUnchangedIndex_DoesNotReReadVectorsFromDisk()
    {
        using var temp = TestDirectory.Create();
        WriteImage(temp.Path, "sunset-over-water.png", 16, 8);
        WriteImage(temp.Path, "receipt-paper-scan.png", 8, 16);
        var service = CreateService(temp.Path);
        service.Rebuild([temp.Path]);

        var first = service.Search("sunset water", limit: 5);
        var loadsAfterFirst = service.CacheLoadCountForTests;
        var second = service.Search("receipt paper", limit: 5);

        // The second query on the unchanged index reuses the cached vectors: no extra DB load.
        Assert.Equal(1, loadsAfterFirst);
        Assert.Equal(loadsAfterFirst, service.CacheLoadCountForTests);
        Assert.NotEmpty(first);
        Assert.NotEmpty(second);
    }

    [Fact]
    public void Search_AfterRebuild_ReloadsVectorCache()
    {
        using var temp = TestDirectory.Create();
        WriteImage(temp.Path, "sunset-over-water.png", 16, 8);
        var service = CreateService(temp.Path);
        service.Rebuild([temp.Path]);
        service.Search("sunset", limit: 5);
        var loadsBefore = service.CacheLoadCountForTests;

        service.Rebuild([temp.Path]);
        service.Search("sunset", limit: 5);

        // A Rebuild bumps the index generation, so the next search reloads rather than serving stale vectors.
        Assert.Equal(loadsBefore + 1, service.CacheLoadCountForTests);
    }

    [Fact]
    public void Search_AppliesFolderFilter()
    {
        using var temp = TestDirectory.Create();
        var keepFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "keep")).FullName;
        var otherFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "other")).FullName;
        var expected = WriteImage(keepFolder, "dog-portrait.png", 8, 16);
        WriteImage(otherFolder, "dog-landscape.png", 16, 8);
        var service = CreateService(temp.Path);
        service.Rebuild([temp.Path]);

        var matches = service.Search("dog", folderFilter: keepFolder);

        var match = Assert.Single(matches);
        Assert.Equal(Path.GetFullPath(expected), match.SourcePath);
    }

    [Fact]
    public void Search_DeterministicFallbackRanksDocumentQuery()
    {
        using var temp = TestDirectory.Create();
        WriteImage(temp.Path, "sunset-over-water.png", 16, 8);
        var receipt = WriteImage(temp.Path, "receipt-paper-scan.png", 8, 16);
        var service = CreateService(temp.Path);
        service.Rebuild([temp.Path]);

        var matches = service.Search("document receipt", limit: 2);

        Assert.NotEmpty(matches);
        Assert.Equal(Path.GetFullPath(receipt), matches[0].SourcePath);
    }

    [Fact]
    public void Search_HonorsCandidateCeiling()
    {
        using var temp = TestDirectory.Create();
        WriteImage(temp.Path, "dog-one.png", 16, 8);
        WriteImage(temp.Path, "dog-two.png", 16, 8);
        WriteImage(temp.Path, "dog-three.png", 16, 8);
        var service = CreateService(temp.Path);
        service.Rebuild([temp.Path]);

        var exhaustive = service.Search("dog", limit: 50);
        var bounded = service.Search("dog", limit: 50, maxCandidates: 1);

        // Below the ceiling the scan is exhaustive; the ceiling caps how many candidates are scored.
        Assert.True(exhaustive.Count >= 2);
        Assert.True(bounded.Count <= 1);
    }

    [Fact]
    public void GetStatus_WhenClipProviderFallsBack_ReportsReason()
    {
        using var temp = TestDirectory.Create();
        var service = new SemanticSearchService(
            Path.Combine(temp.Path, "semantic-index.db"),
            new CatalogService(Path.Combine(temp.Path, "catalog.db")),
            clock: () => new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero),
            clipProviderFactory: () => (null, "CLIP fixture unavailable."));

        var status = service.GetStatus();

        Assert.Equal("deterministic-local-metadata", status.ProviderId);
        Assert.Equal("CLIP fixture unavailable.", status.ProviderFallbackReason);
        Assert.Contains("Deterministic local metadata", status.ProviderStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void Rebuild_WhenCanceledKeepsPreviousSemanticIndexUsable()
    {
        using var temp = TestDirectory.Create();
        WriteImage(temp.Path, "sunset-over-water.png", 16, 8);
        var service = CreateService(temp.Path);
        service.Rebuild([temp.Path]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => service.Rebuild([temp.Path], cts.Token));

        Assert.NotEmpty(service.Search("sunset water"));
    }

    [Fact]
    public void Clear_RemovesIndexedRows()
    {
        using var temp = TestDirectory.Create();
        WriteImage(temp.Path, "sunset-over-water.png", 16, 8);
        var service = CreateService(temp.Path);
        service.Rebuild([temp.Path]);

        service.Clear();

        Assert.Empty(service.Search("sunset water"));
        Assert.Equal(0, service.GetStatus().IndexedCount);
    }

    [Fact]
    public void Search_WhenTextEmbeddingFails_ReturnsEmptyMatches()
    {
        using var temp = TestDirectory.Create();
        WriteImage(temp.Path, "sunset-over-water.png", 16, 8);
        var service = new SemanticSearchService(
            Path.Combine(temp.Path, "semantic-index.db"),
            new CatalogService(Path.Combine(temp.Path, "catalog.db")),
            new ThrowingTextEmbeddingProvider(),
            clock: () => new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero));
        service.Rebuild([temp.Path]);

        var matches = service.Search("sunset");

        Assert.Empty(matches);
    }

    [Fact]
    public void ConnectionString_UsesPrivateCacheForWalConcurrency()
    {
        using var temp = TestDirectory.Create();
        var service = CreateService(temp.Path);

        var builder = new SqliteConnectionStringBuilder(service.ConnectionStringForTests);

        Assert.Equal(SqliteCacheMode.Private, builder.Cache);
    }

    private static SemanticSearchService CreateService(string root)
    {
        var catalog = new CatalogService(Path.Combine(root, "catalog.db"));
        return new SemanticSearchService(
            Path.Combine(root, "semantic-index.db"),
            catalog,
            clock: () => new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero));
    }

    private static string WriteImage(string folder, string name, uint width, uint height)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Red, width, height)
        {
            Format = MagickFormat.Png
        };
        image.Write(path);
        return path;
    }

    private sealed class ThrowingTextEmbeddingProvider : ISemanticEmbeddingProvider
    {
        public string ProviderId => "throwing-text";
        public string ModelId => "test";
        public int Dimensions => 2;
        public string StatusText => "Test provider";

        public IReadOnlyList<float> EmbedImage(SemanticAssetEmbeddingInput input) => [1.0f, 0.0f];

        public IReadOnlyList<float> EmbedText(string query)
            => throw new InvalidOperationException("text embedding failed");

        public string DescribeAsset(CatalogAssetRecord asset) => Path.GetFileName(asset.SourcePath);
    }
}
