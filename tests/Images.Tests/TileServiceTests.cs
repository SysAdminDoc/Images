using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using Images.Services;
using ImageMagick;

namespace Images.Tests;

public sealed class TileServiceTests
{
    [Fact]
    public void Preflight_ValidSmallImage_ReturnsSmallWithDimensions()
    {
        using var temp = TestDirectory.Create();
        var path = Path.Combine(temp.Path, "small.png");
        using (var image = new MagickImage(MagickColors.Red, 64, 32))
            image.Write(path);

        var result = TileService.Preflight(path);

        Assert.Equal(ImagePreflightStatus.Small, result.Status);
        Assert.Equal((uint)64, result.PixelWidth);
        Assert.Equal((uint)32, result.PixelHeight);
    }

    [Fact]
    public void Preflight_MalformedImage_ReturnsRejected()
    {
        using var temp = TestDirectory.Create();
        var path = temp.WriteFile("broken.png", "not an image");

        var result = TileService.Preflight(path);

        Assert.Equal(ImagePreflightStatus.Rejected, result.Status);
    }

    [Fact]
    public void Preflight_NonexistentFile_ReturnsUnknown()
    {
        var result = TileService.Preflight("/nonexistent/path.jpg");

        Assert.Equal(ImagePreflightStatus.Unknown, result.Status);
    }

    [Fact]
    public void Preflight_AtFileSizeThreshold_ReturnsLargeWithoutDimensionProbe()
    {
        var probeCalled = false;

        var result = TileService.Preflight(TileService.LargeImageThresholdBytes, () =>
        {
            probeCalled = true;
            return (1, 1);
        });

        Assert.Equal(ImagePreflightStatus.Large, result.Status);
        Assert.False(probeCalled);
    }

    [Theory]
    [InlineData(5_000, 5_000, ImagePreflightStatus.Small)]
    [InlineData(10_000, 5_000, ImagePreflightStatus.Large)]
    public void Preflight_ClassifiesPixelThresholdBoundary(
        uint width,
        uint height,
        ImagePreflightStatus expected)
    {
        var result = TileService.Preflight(1024, () => (width, height));

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Preflight_DeclaredDimensionBomb_ReturnsLargeWithoutOverflow()
    {
        var result = TileService.Preflight(1024, () => (uint.MaxValue, uint.MaxValue));

        Assert.Equal(ImagePreflightStatus.Large, result.Status);
    }

    [Fact]
    public void Preflight_IoFailure_ReturnsUnknown()
    {
        var result = TileService.Preflight(
            1024,
            () => throw new IOException("simulated short read"));

        Assert.Equal(ImagePreflightStatus.Unknown, result.Status);
    }

    [Fact]
    public void GetLevelDimensions_ReturnsCorrectTileCounts()
    {
        var pyramid = new TilePyramidInfo(
            SourcePath: "test.jpg",
            CacheDirectory: "/cache",
            SourceWidth: 1024,
            SourceHeight: 768,
            TileSize: 256,
            Overlap: 1,
            MaxLevel: 10,
            TotalTiles: 100);

        var (cols, rows) = TileService.GetLevelDimensions(pyramid, pyramid.MaxLevel);

        Assert.Equal(4, cols);
        Assert.Equal(3, rows);
    }

    [Fact]
    public void GetTilePath_MissingTile_ReturnsNull()
    {
        var pyramid = new TilePyramidInfo(
            SourcePath: "test.jpg",
            CacheDirectory: "/nonexistent/cache",
            SourceWidth: 1024,
            SourceHeight: 768,
            TileSize: 256,
            Overlap: 1,
            MaxLevel: 10,
            TotalTiles: 0);

        var path = TileService.GetTilePath(pyramid, new TileKey(10, 0, 0));

        Assert.Null(path);
    }

    [Fact]
    public void ClearCache_RemovesAllPageCachesForSource()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("source.tif", "not a real image");
        var other = temp.WriteFile("other.tif", "not this source");
        var cacheRoot = Path.Combine(temp.Path, "tiles");
        var page0 = Path.Combine(cacheRoot, "page0");
        var page1 = Path.Combine(cacheRoot, "page1");
        var unrelated = Path.Combine(cacheRoot, "unrelated");
        Directory.CreateDirectory(page0);
        Directory.CreateDirectory(page1);
        Directory.CreateDirectory(unrelated);
        File.WriteAllText(
            Path.Combine(page0, "pyramid.json"),
            System.Text.Json.JsonSerializer.Serialize(new TilePyramidInfo(source, page0, 100, 100, 256, 1, 7, 1)));
        File.WriteAllText(
            Path.Combine(page1, "pyramid.json"),
            System.Text.Json.JsonSerializer.Serialize(new TilePyramidInfo(source, page1, 100, 100, 256, 1, 7, 1)));
        File.WriteAllText(
            Path.Combine(unrelated, "pyramid.json"),
            System.Text.Json.JsonSerializer.Serialize(new TilePyramidInfo(other, unrelated, 100, 100, 256, 1, 7, 1)));

        TileService.ClearCache(source, cacheRoot);

        Assert.False(Directory.Exists(page0));
        Assert.False(Directory.Exists(page1));
        Assert.True(Directory.Exists(unrelated));
    }

    [Fact]
    public void BuildPyramid_ReleasesBuildLockAfterCompletionSoTheMapDoesNotGrow()
    {
        using var temp = TestDirectory.Create();
        var cacheRoot = Path.Combine(temp.Path, "tiles");

        // Building many distinct huge images in one session must not accumulate build-lock
        // entries for the process lifetime; each completed build releases its ref-counted gate.
        for (var i = 0; i < 5; i++)
        {
            var source = Path.Combine(temp.Path, $"source-{i}.png");
            using (var image = new MagickImage(MagickColors.Red, 64, 64))
                image.Write(source);
            var pyramid = TileService.BuildPyramid(source, cacheRoot, tileSize: 16);
            Assert.True(Directory.Exists(pyramid.CacheDirectory));
        }

        Assert.Equal(0, TileService.BuildLockCountForTests);
    }

    [Fact]
    public void EvictIfOverCap_NeverEvictsActivePyramidEvenWhenOverCap()
    {
        using var temp = TestDirectory.Create();
        var cacheRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "tiles")).FullName;
        var active = Directory.CreateDirectory(Path.Combine(cacheRoot, "active")).FullName;
        var stale = Directory.CreateDirectory(Path.Combine(cacheRoot, "stale")).FullName;

        // Each pyramid alone exceeds a tiny 1 KB cap.
        File.WriteAllBytes(Path.Combine(active, "tile.bin"), new byte[4096]);
        File.WriteAllBytes(Path.Combine(stale, "tile.bin"), new byte[4096]);
        // Make the active dir the newest so it would sort last anyway; the guard
        // is what protects it once it alone still exceeds the cap.
        Directory.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-2));

        TileService.EvictIfOverCap(cacheRoot, active, capBytes: 1024);

        Assert.True(Directory.Exists(active));
        Assert.False(Directory.Exists(stale));
    }

    [Fact]
    public void EvictIfOverCap_NeverEvictsAnyProtectedPyramid()
    {
        using var temp = TestDirectory.Create();
        var cacheRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "tiles")).FullName;
        var firstActive = Directory.CreateDirectory(Path.Combine(cacheRoot, "first-active")).FullName;
        var secondActive = Directory.CreateDirectory(Path.Combine(cacheRoot, "second-active")).FullName;
        var stale = Directory.CreateDirectory(Path.Combine(cacheRoot, "stale")).FullName;

        File.WriteAllBytes(Path.Combine(firstActive, "tile.bin"), new byte[4096]);
        File.WriteAllBytes(Path.Combine(secondActive, "tile.bin"), new byte[4096]);
        File.WriteAllBytes(Path.Combine(stale, "tile.bin"), new byte[4096]);
        Directory.SetLastWriteTimeUtc(firstActive, DateTime.UtcNow.AddDays(-4));
        Directory.SetLastWriteTimeUtc(secondActive, DateTime.UtcNow.AddDays(-3));
        Directory.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-2));

        TileService.EvictIfOverCap(cacheRoot, [firstActive, secondActive], capBytes: 1024);

        Assert.True(Directory.Exists(firstActive));
        Assert.True(Directory.Exists(secondActive));
        Assert.False(Directory.Exists(stale));
    }

    [Fact]
    public void BuildPyramid_WhenDecodeFails_RemovesPartialCacheDirectory()
    {
        using var temp = TestDirectory.Create();
        var source = temp.WriteFile("broken.tif", "not a real image");
        var cacheRoot = Path.Combine(temp.Path, "tiles");

        Assert.ThrowsAny<Exception>(() => TileService.BuildPyramid(source, cacheRoot));

        Assert.True(Directory.Exists(cacheRoot));
        Assert.Empty(Directory.GetDirectories(cacheRoot));
    }

    [Fact]
    public async Task BuildPyramid_ConcurrentRequestsReuseSingleCacheDirectory()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "source.png");
        using (var image = new MagickImage(MagickColors.Red, 64, 64))
            image.Write(source);

        var cacheRoot = Path.Combine(temp.Path, "tiles");
        var results = new TilePyramidInfo?[8];
        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, results.Length)
            .Select(index => Task.Run(() =>
            {
                start.Wait();
                results[index] = TileService.BuildPyramid(source, cacheRoot, tileSize: 16);
            }))
            .ToArray();

        start.Set();
        await Task.WhenAll(tasks);

        var completed = results.Select(result => Assert.IsType<TilePyramidInfo>(result)).ToArray();
        var cacheDirectory = Assert.Single(completed.Select(result => result.CacheDirectory).Distinct(StringComparer.OrdinalIgnoreCase));

        Assert.Equal(cacheDirectory, completed[0].CacheDirectory);
        Assert.Single(Directory.GetDirectories(cacheRoot));
        Assert.True(File.Exists(Path.Combine(cacheDirectory, "pyramid.json")));
        Assert.Empty(Directory.EnumerateFiles(cacheDirectory, "*.tmp", SearchOption.TopDirectoryOnly));
        Assert.All(completed, result => Assert.True(result.TotalTiles > 0));
    }

    [Fact]
    public void ChooseLevel_FitView_UsesLowerResolutionLevel()
    {
        var pyramid = new TilePyramidInfo(
            SourcePath: "huge.tif",
            CacheDirectory: "/cache",
            SourceWidth: 8192,
            SourceHeight: 4096,
            TileSize: 256,
            Overlap: 1,
            MaxLevel: 13,
            TotalTiles: 500);

        var level = TileService.ChooseLevel(pyramid, viewportWidth: 1024, viewportHeight: 512, zoomScale: 1);

        Assert.Equal(10, level);
    }

    [Fact]
    public void GetVisibleTiles_ZoomedView_ReturnsViewportSubset()
    {
        var pyramid = new TilePyramidInfo(
            SourcePath: "huge.tif",
            CacheDirectory: "/cache",
            SourceWidth: 4096,
            SourceHeight: 4096,
            TileSize: 256,
            Overlap: 1,
            MaxLevel: 12,
            TotalTiles: 400);

        var tiles = TileService.GetVisibleTiles(
            pyramid,
            level: 12,
            viewportWidth: 512,
            viewportHeight: 512,
            zoomScale: 8,
            translateX: 0,
            translateY: 0,
            rotation: 0,
            flipHorizontal: false,
            flipVertical: false);

        var allTilesAtLevel = TileService.GetLevelDimensions(pyramid, 12);
        Assert.NotEmpty(tiles);
        Assert.True(tiles.Count < allTilesAtLevel.Columns * allTilesAtLevel.Rows);
        Assert.All(tiles, tile => Assert.Equal(12, tile.Level));
    }

    [Fact]
    public void GetVisibleTiles_RotatedView_ReturnsWholeLevel()
    {
        var pyramid = new TilePyramidInfo(
            SourcePath: "huge.tif",
            CacheDirectory: "/cache",
            SourceWidth: 1024,
            SourceHeight: 1024,
            TileSize: 256,
            Overlap: 1,
            MaxLevel: 10,
            TotalTiles: 25);

        var tiles = TileService.GetVisibleTiles(
            pyramid,
            level: 10,
            viewportWidth: 512,
            viewportHeight: 512,
            zoomScale: 2,
            translateX: 0,
            translateY: 0,
            rotation: 90,
            flipHorizontal: false,
            flipVertical: false);

        Assert.Equal(16, tiles.Count);
    }

}
