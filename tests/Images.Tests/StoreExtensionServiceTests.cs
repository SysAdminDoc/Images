using System.Diagnostics;
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

    [Fact]
    public void OpenStorePage_WhenLauncherSucceeds_BuildsStoreStartInfoAndReturnsTrue()
    {
        var info = new StoreExtensionService.StoreExtensionInfo(
            "Test Extension",
            "abc123",
            "ms-windows-store://pdp/?productid=abc123");
        ProcessStartInfo? captured = null;

        var opened = info.OpenStorePage(startInfo => captured = startInfo);

        Assert.True(opened);
        Assert.NotNull(captured);
        Assert.Equal("ms-windows-store://pdp/?productid=abc123", captured!.FileName);
        Assert.True(captured.UseShellExecute);
    }

    [Fact]
    public void OpenStorePage_WhenLauncherFails_ReturnsFalse()
    {
        var info = new StoreExtensionService.StoreExtensionInfo(
            "Test Extension",
            "abc123",
            "ms-windows-store://pdp/?productid=abc123");

        var opened = info.OpenStorePage(_ => throw new InvalidOperationException("Store unavailable"));

        Assert.False(opened);
    }
}
