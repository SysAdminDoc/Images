using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

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
}
