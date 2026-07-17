using System.IO;
using ImageMagick;
using Images.Services;

namespace Images.Tests;

public sealed class CatalogWatchServiceTests
{
    [Fact]
    public async Task StartAsync_ScansRegisteredRootsAndWatcherAppliesFileDelta()
    {
        using var temp = TestDirectory.Create();
        var catalog = new CatalogService(Path.Combine(temp.Path, "catalog.db"));
        Assert.True(catalog.RegisterRoot(temp.Path));
        using var watcher = new CatalogWatchService(catalog, TimeSpan.FromMilliseconds(40), TimeSpan.FromHours(1));

        await watcher.StartAsync();
        var path = Path.Combine(temp.Path, "arrived.png");
        using (var image = new MagickImage(MagickColors.Blue, 4, 4) { Format = MagickFormat.Png })
            image.Write(path);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(8);
        while (catalog.GetByPath(path) is null && DateTime.UtcNow < deadline)
            await Task.Delay(50);

        Assert.NotNull(catalog.GetByPath(path));
    }
}
