using Images.Services;

namespace Images.Tests;

public sealed class OcrCapabilityServiceTests
{
    [Fact]
    public void GetStatus_ReturnsNonNull()
    {
        var status = OcrCapabilityService.GetStatus();

        Assert.NotNull(status);
    }

    [Fact]
    public void GetStatus_HasIsAvailableProperty()
    {
        var status = OcrCapabilityService.GetStatus();

        // IsAvailable is a bool -- it's either true or false, both valid.
        Assert.IsType<bool>(status.IsAvailable);
    }

    [Fact]
    public void GetStatus_HasNonNullStatusTitle()
    {
        var status = OcrCapabilityService.GetStatus();

        Assert.NotNull(status.StatusTitle);
        Assert.NotEmpty(status.StatusTitle);
    }

    [Fact]
    public void GetStatus_HasNonNullBadgeText()
    {
        var status = OcrCapabilityService.GetStatus();

        Assert.NotNull(status.BadgeText);
        Assert.NotEmpty(status.BadgeText);
    }
}
