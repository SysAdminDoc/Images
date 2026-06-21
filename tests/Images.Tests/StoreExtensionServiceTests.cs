using Images.Services;

namespace Images.Tests;

public sealed class StoreExtensionServiceTests
{
    [Fact]
    public void GetMissingExtension_ForJpg_ReturnsNull()
    {
        var result = StoreExtensionService.GetMissingExtension(".jpg");

        Assert.Null(result);
    }

    [Fact]
    public void GetMissingExtension_ForPng_ReturnsNull()
    {
        var result = StoreExtensionService.GetMissingExtension(".png");

        Assert.Null(result);
    }

    [Fact]
    public void IsStoreExtensionFormat_ForHeic_ReturnsTrue()
    {
        Assert.True(StoreExtensionService.IsStoreExtensionFormat(".heic"));
    }

    [Fact]
    public void IsStoreExtensionFormat_ForJpg_ReturnsFalse()
    {
        Assert.False(StoreExtensionService.IsStoreExtensionFormat(".jpg"));
    }

    [Fact]
    public void IsJxlFormat_ForJxl_ReturnsTrue()
    {
        Assert.True(StoreExtensionService.IsJxlFormat(".jxl"));
    }
}
