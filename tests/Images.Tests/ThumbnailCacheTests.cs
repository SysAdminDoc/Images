using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class ThumbnailCacheTests
{
    [Fact]
    public void GetHealth_WhenCacheContainsFiles_ReturnsDisposableCacheTotals()
    {
        using var temp = TestDirectory.Create();
        var partition = Path.Combine(temp.Path, "aa");
        Directory.CreateDirectory(partition);
        File.WriteAllBytes(Path.Combine(partition, "cached.webp"), new byte[1024]);
        File.WriteAllBytes(Path.Combine(partition, "cached.tmp-001.webp"), new byte[512]);
        var cache = new ThumbnailCache(temp.Path, ThumbnailCache.DefaultThumbSize, 4096);

        var health = cache.GetHealth();

        Assert.True(health.IsAvailable);
        Assert.Equal(temp.Path, health.Root);
        Assert.Equal(1024, health.Bytes);
        Assert.Equal(1, health.FileCount);
        Assert.Equal(1, health.TempFileCount);
        Assert.Equal(4096, health.CapBytes);
        Assert.Null(health.Error);
    }

    [Fact]
    public void GetHealth_WhenCacheRootIsMissing_ReturnsAvailableEmptyCache()
    {
        using var temp = TestDirectory.Create();
        var missing = Path.Combine(temp.Path, "thumbs");
        var cache = new ThumbnailCache(missing, ThumbnailCache.DefaultThumbSize, 4096);

        var health = cache.GetHealth();

        Assert.True(health.IsAvailable);
        Assert.Equal(missing, health.Root);
        Assert.Equal(0, health.Bytes);
        Assert.Equal(0, health.FileCount);
        Assert.Equal(0, health.TempFileCount);
    }

    [Fact]
    public void CreateDefault_WhenStorageRootIsUnavailable_ReturnsUnavailableCache()
    {
        var cache = ThumbnailCache.CreateDefault(() => null);

        var health = cache.GetHealth();

        Assert.False(health.IsAvailable);
        Assert.Null(health.Root);
        Assert.Equal(0, health.FileCount);
    }

    [Fact]
    public void CreateDefault_WhenStorageRootExists_ReturnsAvailableCache()
    {
        using var temp = TestDirectory.Create();

        var cache = ThumbnailCache.CreateDefault(() => temp.Path);

        var health = cache.GetHealth();
        Assert.True(health.IsAvailable);
        Assert.Equal(temp.Path, health.Root);
    }

    [Fact]
    public void Clear_WhenCacheContainsThumbnails_DeletesDisposableWebpFilesOnly()
    {
        using var temp = TestDirectory.Create();
        var partition = Path.Combine(temp.Path, "aa");
        Directory.CreateDirectory(partition);
        var cached = Path.Combine(partition, "cached.webp");
        var tempCached = Path.Combine(partition, "cached.tmp-001.webp");
        var ignored = Path.Combine(partition, "notes.txt");
        File.WriteAllBytes(cached, new byte[1024]);
        File.WriteAllBytes(tempCached, new byte[512]);
        File.WriteAllText(ignored, "not cache data");
        var cache = new ThumbnailCache(temp.Path, ThumbnailCache.DefaultThumbSize, 4096);

        var result = cache.Clear();

        Assert.True(result.IsAvailable);
        Assert.Equal(2, result.DeletedCount);
        Assert.Equal(1536, result.DeletedBytes);
        Assert.Equal(0, result.FailedCount);
        Assert.False(File.Exists(cached));
        Assert.False(File.Exists(tempCached));
        Assert.True(File.Exists(ignored));
    }

    [Fact]
    public void Clear_WhenCacheRootIsMissing_ReturnsEmptyAvailableResult()
    {
        using var temp = TestDirectory.Create();
        var missing = Path.Combine(temp.Path, "thumbs");
        var cache = new ThumbnailCache(missing, ThumbnailCache.DefaultThumbSize, 4096);

        var result = cache.Clear();

        Assert.True(result.IsAvailable);
        Assert.Equal(missing, result.Root);
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(0, result.DeletedBytes);
        Assert.Equal(0, result.FailedCount);
    }
}
