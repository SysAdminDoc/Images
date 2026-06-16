using Images.Services;

namespace Images.Tests;

public sealed class BackgroundRemovalServiceTests
{
    [Fact]
    public void IsAvailable_ReturnsFalse_WhenNoModelsImported()
    {
        Assert.False(BackgroundRemovalService.IsAvailable());
    }

    [Fact]
    public void RemoveBackground_NoModel_ReturnsError()
    {
        var result = BackgroundRemovalService.RemoveBackground("test.jpg");

        Assert.False(result.Success);
        Assert.Contains("No approved segmentation model", result.ErrorMessage!);
    }
}
