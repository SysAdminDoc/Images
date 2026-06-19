using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class TileServiceTests
{
    [Fact]
    public void ShouldUseTileEngine_SmallFile_ReturnsFalse()
    {
        using var temp = TestDirectory.Create();
        var smallFile = temp.WriteFile("small.txt", "not an image");

        Assert.False(TileService.ShouldUseTileEngine(smallFile));
    }

    [Fact]
    public void ShouldUseTileEngine_NonexistentFile_ReturnsFalse()
    {
        Assert.False(TileService.ShouldUseTileEngine("/nonexistent/path.jpg"));
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
