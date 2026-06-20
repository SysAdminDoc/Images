using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class CatalogQueryServiceTests
{
    [Fact]
    public void ListIndexedFolders_ReturnsDistinctFolders()
    {
        using var temp = TestDirectory.Create();
        var sub1 = Directory.CreateDirectory(Path.Combine(temp.Path, "photos")).FullName;
        var sub2 = Directory.CreateDirectory(Path.Combine(temp.Path, "scans")).FullName;
        WriteImage(sub1, "a.png", 4, 4);
        WriteImage(sub2, "b.png", 4, 4);
        var catalog = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        catalog.Rebuild([sub1, sub2]);
        var query = new CatalogQueryService(catalog);

        var folders = query.ListIndexedFolders();

        Assert.Equal(2, folders.Count);
        Assert.Contains(folders, f => f.Equals(sub1, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(folders, f => f.Equals(sub2, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void QueryByFolder_ReturnsOnlyMatchingAssets()
    {
        using var temp = TestDirectory.Create();
        var target = Directory.CreateDirectory(Path.Combine(temp.Path, "target")).FullName;
        var other = Directory.CreateDirectory(Path.Combine(temp.Path, "other")).FullName;
        WriteImage(target, "match.png", 4, 4);
        WriteImage(other, "nomatch.png", 4, 4);
        var catalog = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        catalog.Rebuild([target, other]);
        var query = new CatalogQueryService(catalog);

        var result = query.QueryByFolder(target);

        Assert.Equal(1, result.TotalMatched);
        Assert.Single(result.Assets);
        Assert.Contains("match.png", result.Assets[0].SourcePath, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.Truncated);
    }

    [Fact]
    public void Search_MatchesFileName()
    {
        using var temp = TestDirectory.Create();
        WriteImage(temp.Path, "sunset.png", 4, 4);
        WriteImage(temp.Path, "portrait.png", 4, 4);
        var catalog = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        catalog.Rebuild([temp.Path]);
        var query = new CatalogQueryService(catalog);

        var result = query.Search("sunset");

        Assert.Equal(1, result.TotalMatched);
        Assert.Contains("sunset.png", result.Assets[0].SourcePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsEmpty()
    {
        using var temp = TestDirectory.Create();
        WriteImage(temp.Path, "photo.png", 4, 4);
        var catalog = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        catalog.Rebuild([temp.Path]);
        var query = new CatalogQueryService(catalog);

        var result = query.Search("");

        Assert.Empty(result.Assets);
        Assert.Equal(0, result.TotalMatched);
    }

    [Fact]
    public void QueryByFolder_WithRedactPaths_HidesIntermediateSegments()
    {
        using var temp = TestDirectory.Create();
        var sub = Directory.CreateDirectory(Path.Combine(temp.Path, "deep", "nested")).FullName;
        WriteImage(sub, "photo.png", 4, 4);
        var catalog = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        catalog.Rebuild([sub]);
        var query = new CatalogQueryService(catalog);

        var result = query.QueryByFolder(sub, redactPaths: true);

        Assert.Single(result.Assets);
        Assert.Contains("***", result.Assets[0].SourcePath);
        Assert.EndsWith("photo.png", result.Assets[0].SourcePath);
    }

    private static string WriteImage(string folder, string name, uint width, uint height)
    {
        var path = Path.Combine(folder, name);
        using var image = new MagickImage(MagickColors.Red, width, height) { Format = MagickFormat.Png };
        image.Write(path);
        return path;
    }
}
