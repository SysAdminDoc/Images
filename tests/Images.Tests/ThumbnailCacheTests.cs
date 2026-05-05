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
}
