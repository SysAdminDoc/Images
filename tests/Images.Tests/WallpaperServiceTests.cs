using System.IO;
using Images.Services;

namespace Images.Tests;

public sealed class WallpaperServiceTests
{
    [Theory]
    [InlineData(WallpaperLayout.Fill, "10", "0")]
    [InlineData(WallpaperLayout.Fit, "6", "0")]
    [InlineData(WallpaperLayout.Span, "22", "0")]
    [InlineData(WallpaperLayout.Tile, "0", "1")]
    public void RegistryStyleFor_ReturnsWindowsWallpaperValues(
        WallpaperLayout layout,
        string wallpaperStyle,
        string tileWallpaper)
    {
        var style = WallpaperService.RegistryStyleFor(layout);

        Assert.Equal(wallpaperStyle, style.WallpaperStyle);
        Assert.Equal(tileWallpaper, style.TileWallpaper);
    }

    [Fact]
    public void TryParseLayout_ParsesCaseInsensitiveLayoutNames()
    {
        Assert.True(WallpaperService.TryParseLayout("span", out var layout));
        Assert.Equal(WallpaperLayout.Span, layout);
        Assert.False(WallpaperService.TryParseLayout("stretch", out _));
    }

    [Fact]
    public void SetFromFileCore_CopiesToStableSlotAndAppliesLayoutBeforeWallpaper()
    {
        using var temp = TestDirectory.Create();
        var source = Path.Combine(temp.Path, "source.jpg");
        var wallpaperFolder = Path.Combine(temp.Path, "wallpaper");
        Directory.CreateDirectory(wallpaperFolder);
        File.WriteAllText(source, "image");
        var events = new List<string>();
        string? wallpaperPath = null;

        var stablePath = WallpaperService.SetFromFileCore(
            source,
            WallpaperLayout.Tile,
            () => wallpaperFolder,
            layout => events.Add("layout:" + layout),
            path =>
            {
                events.Add("wallpaper");
                wallpaperPath = path;
            });

        Assert.Equal(Path.Combine(wallpaperFolder, "current.jpg"), stablePath);
        Assert.Equal(stablePath, wallpaperPath);
        Assert.Equal("image", File.ReadAllText(stablePath));
        Assert.Equal(["layout:Tile", "wallpaper"], events);
    }
}
