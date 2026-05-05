using Images.Services;

namespace Images.Tests;

public sealed class OverlayWindowServiceTests
{
    [Fact]
    public void ExitHotKeyId_IsInsideApplicationHotKeyRange()
    {
        Assert.InRange(OverlayWindowService.ExitHotKeyId, 0x0000, 0xBFFF);
    }

    [Fact]
    public void BuildExtendedStyle_AddsTransparentAndLayeredForClickThrough()
    {
        var style = OverlayWindowService.BuildExtendedStyle(0, clickThrough: true);

        Assert.True(OverlayWindowService.IsClickThroughStyle(style));
    }

    [Fact]
    public void BuildExtendedStyle_RemovesOnlyTransparentWhenDisabling()
    {
        var enabled = OverlayWindowService.BuildExtendedStyle(0x00040000, clickThrough: true);

        var disabled = OverlayWindowService.BuildExtendedStyle(enabled, clickThrough: false);

        Assert.False(OverlayWindowService.IsClickThroughStyle(disabled));
        Assert.True((disabled & 0x00040000) == 0x00040000);
    }
}
