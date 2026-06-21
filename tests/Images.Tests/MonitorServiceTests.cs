using Images.Services;

namespace Images.Tests;

public sealed class MonitorServiceTests
{
    [Fact]
    public void GetAllMonitors_ReturnsNonNullList()
    {
        var monitors = MonitorService.GetAllMonitors();

        Assert.NotNull(monitors);
    }

    [Fact]
    public void GetAllMonitors_ReturnsAtLeastOneMonitor()
    {
        var monitors = MonitorService.GetAllMonitors();

        Assert.NotEmpty(monitors);
    }

    [Fact]
    public void GetAllMonitors_EachMonitorHasDeviceName()
    {
        var monitors = MonitorService.GetAllMonitors();

        foreach (var monitor in monitors)
        {
            Assert.False(string.IsNullOrEmpty(monitor.DeviceName));
        }
    }

    [Fact]
    public void SanitizeDeviceName_RemovesPrefix()
    {
        var sanitized = MonitorService.SanitizeDeviceName(@"\\.\DISPLAY1");

        Assert.Equal("DISPLAY1", sanitized);
    }
}
