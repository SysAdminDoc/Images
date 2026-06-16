using Images.Services;

namespace Images.Tests;

public sealed class SuperResolutionServiceTests
{
    [Fact]
    public void IsAvailable_ReturnsFalse_WhenNoModelsImported()
    {
        Assert.False(SuperResolutionService.IsAvailable());
    }

    [Fact]
    public void Upscale_NoModel_ReturnsError()
    {
        var result = SuperResolutionService.Upscale("test.jpg");

        Assert.False(result.Success);
        Assert.Contains("No approved super-resolution model", result.ErrorMessage!);
    }
}
