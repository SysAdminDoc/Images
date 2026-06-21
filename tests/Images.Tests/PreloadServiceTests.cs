using Images.Services;

namespace Images.Tests;

public sealed class PreloadServiceTests
{
    [Fact]
    public void CreateAndDispose_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
        {
            using var service = new PreloadService();
        });

        Assert.Null(ex);
    }

    [Fact]
    public void TryGet_OnEmptyCache_ReturnsNull()
    {
        using var service = new PreloadService();

        var result = service.TryGet("nonexistent.jpg");

        Assert.Null(result);
    }

    [Fact]
    public void Reset_OnEmptyCache_DoesNotThrow()
    {
        using var service = new PreloadService();

        var ex = Record.Exception(() => service.Reset());

        Assert.Null(ex);
    }

    [Fact]
    public void TryGet_AfterDispose_ReturnsNull()
    {
        var service = new PreloadService();
        service.Dispose();

        var result = service.TryGet("test.jpg");

        Assert.Null(result);
    }
}
