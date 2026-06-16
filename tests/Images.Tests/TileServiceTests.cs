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
}
