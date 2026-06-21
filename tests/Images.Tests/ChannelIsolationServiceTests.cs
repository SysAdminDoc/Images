using Images.Services;

namespace Images.Tests;

public sealed class ChannelIsolationServiceTests
{
    [Fact]
    public void Isolate_NullSource_ReturnsNull()
    {
        var result = ChannelIsolationService.Isolate(null, ChannelMode.Red);
        Assert.Null(result);
    }

    [Fact]
    public void Isolate_NormalMode_ReturnsSameSource()
    {
        var result = ChannelIsolationService.Isolate(null, ChannelMode.Normal);
        Assert.Null(result);
    }
}
